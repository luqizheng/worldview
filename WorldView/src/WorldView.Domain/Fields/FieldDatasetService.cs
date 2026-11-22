using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>
/// Publishes immutable Map Source Field Baseline revisions. Asset bytes must already be published
/// and verified; this service owns only Core Domain identity, provenance and current-pointer authority.
/// </summary>
public static class FieldDatasetService
{
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
        {
            throw new ArgumentException("manifest_json 不能为空白。", nameof(manifestJson));
        }

        using var document = JsonDocument.Parse(manifestJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("canonicalization", out var canonicalization)
            || canonicalization.GetString() != "RFC8785-JCS-UTF8"
            || !root.TryGetProperty("field", out var field)
            || field.ValueKind != JsonValueKind.Object
            || !field.TryGetProperty("field_key", out var key)
            || key.GetString() != descriptor.FieldKey
            || !field.TryGetProperty("schema_version", out var version)
            || version.GetInt32() != descriptor.SchemaVersion
            || !field.TryGetProperty("semantics_id", out var semantics)
            || semantics.GetString() != descriptor.SemanticsId
            || !root.TryGetProperty("spatial", out var spatial)
            || spatial.ValueKind != JsonValueKind.Object
            || !spatial.TryGetProperty("map_space_id", out var mapSpace)
            || string.IsNullOrWhiteSpace(mapSpace.GetString())
            || !spatial.TryGetProperty("spatial_reference_id", out var spatialReference)
            || string.IsNullOrWhiteSpace(spatialReference.GetString()))
        {
            throw new ArgumentException(
                "Field manifest 必须是已校验的 JCS V1 manifest，并匹配 Field Descriptor、Map Space 和 Spatial Reference。",
                nameof(manifestJson));
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
