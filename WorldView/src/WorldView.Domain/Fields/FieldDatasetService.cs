using System;
using System.Text.Json;
using WorldView.Domain.Assets;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>
/// Core Domain authority for Map-owned source Field Datasets. Staging and byte publication
/// happen outside this service; this service validates the formal owner boundary and asks
/// the database to atomically append an immutable revision and advance its current pointer.
/// </summary>
public static class FieldDatasetService
{
    public static FieldRevisionRecord PublishRevision(
        ICoreDomainDatabase database,
        PublishFieldRevisionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(plan);

        if (!FieldDescriptorRegistry.TryGetSemanticsId(
                plan.FieldKey, plan.DescriptorSchemaVersion, out var expectedSemanticsId))
            throw new CoreDomainConflictException(
                $"Field descriptor {plan.FieldKey}@{plan.DescriptorSchemaVersion} is not registered in the V1 registry.");
        if (!string.Equals(plan.SemanticsId, expectedSemanticsId, StringComparison.Ordinal))
            throw new CoreDomainConflictException(
                $"Field descriptor semantics_id must be {expectedSemanticsId} for {plan.FieldKey}@{plan.DescriptorSchemaVersion}.");
        if (plan.FieldDatasetId == Guid.Empty || plan.FieldRevisionId == Guid.Empty
            || plan.FieldRevisionAssetId == Guid.Empty || plan.ProjectId == Guid.Empty || plan.MapId == Guid.Empty)
            throw new ArgumentException("Field Dataset、Revision、Asset、Project 和 Map ID 均必须为非空 UUID。", nameof(plan));

        var fingerprint = AssetRules.NormalizeContentHash(plan.ContentFingerprint);
        var manifest = CanonicalJson.Canonicalize(plan.ManifestJson);
        var provenance = CanonicalJson.Canonicalize(plan.ProvenanceJson);
        ValidateManifestIdentity(manifest, plan, fingerprint);

        if (database.FindMap(plan.ProjectId, plan.MapId) is null)
            throw new CoreDomainConflictException($"Map {plan.MapId} 不存在或不属于项目 {plan.ProjectId}。");

        var asset = database.FindAsset(plan.ProjectId, plan.FieldRevisionAssetId)
            ?? throw new CoreDomainConflictException(
                $"Field Revision Asset {plan.FieldRevisionAssetId} 不存在或不属于项目 {plan.ProjectId}。");
        if (asset.IntegrityState != AssetIntegrityState.Verified)
            throw new CoreDomainConflictException(
                $"Field Revision Asset {asset.AssetId} 必须先通过字节完整性验证；当前状态为 {asset.IntegrityState}。");

        var current = database.FindFieldDataset(plan.ProjectId, plan.MapId, plan.FieldKey);
        if (current is not null)
        {
            if (current.FieldDatasetId != plan.FieldDatasetId
                || current.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                || !string.Equals(current.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                throw new CoreDomainDataException("Field Dataset identity 或 descriptor pin 与既有权威行不一致。");
            if (current.CurrentFieldRevisionId != plan.ExpectedCurrentFieldRevisionId)
                throw new CoreDomainConflictException("Field Revision publication CAS 冲突：current revision 已变化。");
        }
        else if (plan.ExpectedCurrentFieldRevisionId is not null)
        {
            throw new CoreDomainConflictException("Field Dataset 尚不存在；首次 publication 的 expected current revision 必须为 null。");
        }

        return database.PublishFieldRevision(plan with
        {
            ContentFingerprint = fingerprint,
            ManifestJson = manifest,
            ProvenanceJson = provenance,
        });
    }

    private static void ValidateManifestIdentity(
        string canonicalManifest,
        PublishFieldRevisionPlan plan,
        string expectedFingerprint)
    {
        using var document = JsonDocument.Parse(canonicalManifest);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new CoreDomainConflictException("Field Revision manifest 必须是 JSON object。");

        static string RequiredString(JsonElement value, string propertyName)
        {
            if (!value.TryGetProperty(propertyName, out var property)
                || property.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(property.GetString()))
                throw new CoreDomainConflictException(
                    $"Field Revision manifest 缺少有效的 {propertyName}。");
            return property.GetString()!;
        }

        var fieldKey = RequiredString(root, "field_key");
        var mapId = RequiredString(root, "map_id");
        var fingerprint = RequiredString(root, "content_fingerprint");
        if (!string.Equals(fieldKey, plan.FieldKey, StringComparison.Ordinal))
            throw new CoreDomainConflictException("Field Revision manifest field_key 与 Dataset 不一致。");
        if (!Guid.TryParseExact(mapId, "D", out var manifestMapId) || manifestMapId != plan.MapId)
            throw new CoreDomainConflictException("Field Revision manifest map_id 与目标 Map 不一致。");
        if (!string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal))
            throw new CoreDomainConflictException("Field Revision manifest content_fingerprint 与发布计划不一致。");
    }
}
