using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Linq;
using WorldView.Domain.Registry;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>
/// Publishes immutable Map Source Field Baseline revisions. Asset bytes must already be published
/// and verified; this service owns only Core Domain identity, provenance and current-pointer authority.
/// </summary>
public static class FieldDatasetService
{
    private static readonly Lazy<JsonDocument> ManifestSchema = new(LoadManifestSchema);

    private static JsonDocument LoadManifestSchema()
    {
        var assembly = typeof(FieldDatasetService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(
                "p2-09-34-field-package-canonical-manifest-schema-v0.1.json",
                StringComparison.Ordinal));
        if (resourceName is null)
            throw new RegistryException("field_manifest_schema_missing", "P2-09-34 canonical manifest schema resource is missing.");
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new RegistryException("field_manifest_schema_missing", "P2-09-34 canonical manifest schema resource cannot be opened.");
        using var reader = new StreamReader(stream);
        return JsonDocument.Parse(reader.ReadToEnd());
    }

    public static FieldRevisionRecord PublishRevision(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        string fieldKey,
        Guid fieldRevisionAssetId,
        string manifestJson,
        string provenanceJson,
        Guid? expectedCurrentRevisionId,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (projectId == Guid.Empty || mapId == Guid.Empty || fieldRevisionAssetId == Guid.Empty)
        {
            throw new ArgumentException("Project、Map 和 Asset identity 必须是非空 UUID。");
        }

        var descriptor = FieldDescriptorRegistry.Require(fieldKey);
        ValidateManifest(manifestJson, descriptor);
        ValidateProvenance(provenanceJson);

        var map = database.FindMap(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 不存在或不属于 Project {projectId}。");
        _ = map;

        var asset = database.FindAsset(projectId, fieldRevisionAssetId)
            ?? throw new CoreDomainConflictException($"Field payload Asset {fieldRevisionAssetId} 不存在或不属于 Project {projectId}。");
        if (asset.PublicationState != AssetPublicationState.Published
            || asset.IntegrityState != AssetIntegrityState.Verified)
        {
            throw new CoreDomainConflictException(
                $"Field payload Asset 必须为 Published + Verified；当前状态为 {asset.PublicationState}/{asset.IntegrityState}。");
        }

        var fingerprint = ComputeContentFingerprint(
            asset.ContentHash, descriptor, manifestJson, provenanceJson);

        return database.PublishFieldRevision(new FieldRevisionPublishPlan
        {
            FieldRevisionId = Guid.NewGuid(),
            ProjectId = projectId,
            MapId = mapId,
            FieldKey = descriptor.FieldKey,
            DescriptorSchemaVersion = descriptor.SchemaVersion,
            SemanticsId = descriptor.SemanticsId,
            ExpectedCurrentRevisionId = expectedCurrentRevisionId,
            FieldRevisionAssetId = fieldRevisionAssetId,
            ContentFingerprint = fingerprint,
            ManifestJson = manifestJson,
            ProvenanceJson = provenanceJson,
            CreatedAtUtcMs = nowUtcMs,
        });
    }

    public static string ComputeContentFingerprint(
        string assetContentHash,
        FieldDescriptor descriptor,
        string manifestJson,
        string provenanceJson)
    {
        // Length-prefix each UTF-8 component to avoid delimiter ambiguity. manifestJson is required
        // to be the exact RFC 8785 JCS byte sequence produced/validated by the package boundary.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "world-view.field-revision-fingerprint.v1");
        Append(hash, assetContentHash);
        Append(hash, descriptor.FieldKey);
        Append(hash, descriptor.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, descriptor.SemanticsId);
        Append(hash, manifestJson);
        Append(hash, provenanceJson);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void ValidateManifest(string manifestJson, FieldDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new ArgumentException("manifest_json 不能为空白。", nameof(manifestJson));
        if (manifestJson[0] == '\uFEFF' || manifestJson.EndsWith("\n", StringComparison.Ordinal)
            || manifestJson.EndsWith("\r", StringComparison.Ordinal))
            throw new ArgumentException("Canonical Field Manifest 不允许 BOM 或尾随换行。", nameof(manifestJson));

        using var document = JsonDocument.Parse(manifestJson);
        RejectDuplicateProperties(document.RootElement, string.Empty);
        var violations = JsonSchemaSubset.Validate(ManifestSchema.Value.RootElement, document.RootElement);
        if (violations.Count != 0)
        {
            var first = violations[0];
            throw new ArgumentException(
                $"Field manifest 不符合 P2-09-34：{first.Keyword} at {first.InstancePath}: {first.Message}",
                nameof(manifestJson));
        }

        var field = document.RootElement.GetProperty("field");
        if (!string.Equals(field.GetProperty("field_key").GetString(), descriptor.FieldKey, StringComparison.Ordinal)
            || field.GetProperty("schema_version").GetInt32() != descriptor.SchemaVersion
            || !string.Equals(field.GetProperty("semantics_id").GetString(), descriptor.SemanticsId, StringComparison.Ordinal))
            throw new ArgumentException("Field manifest 与 Core Field Descriptor pin 不匹配。", nameof(manifestJson));
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new ArgumentException($"Field manifest 存在重复 JSON 属性：{path}/{property.Name}");
                RejectDuplicateProperties(property.Value, $"{path}/{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                RejectDuplicateProperties(item, $"{path}/{index++}");
        }
    }

    private static void ValidateProvenance(string provenanceJson)
    {
        if (string.IsNullOrWhiteSpace(provenanceJson))
        {
            throw new ArgumentException("provenance_json 不能为空白。", nameof(provenanceJson));
        }

        using var document = JsonDocument.Parse(provenanceJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("source_kind", out var sourceKind)
            || sourceKind.ValueKind != JsonValueKind.String
            || sourceKind.GetString() is not ("authored" or "imported" or "adopted_derived"))
        {
            throw new ArgumentException(
                "Field provenance 必须声明 source_kind=authored/imported/adopted_derived。",
                nameof(provenanceJson));
        }
    }
}
