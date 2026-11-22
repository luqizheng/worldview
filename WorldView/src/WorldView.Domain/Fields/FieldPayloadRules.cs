using System;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Fields;

/// <summary>Validation rules for immutable Field Payload storage metadata.</summary>
public static class FieldPayloadRules
{
    private const string ManagedPayloadPrefix = "FieldPayloads/sha256/";

    public static FieldPayloadRecord Create(RegisterFieldPayloadPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.FieldPayloadId == Guid.Empty || plan.ProjectId == Guid.Empty
            || plan.FieldDatasetId == Guid.Empty || plan.FieldRevisionId == Guid.Empty)
            throw new ArgumentException("Field Payload、Project、Dataset 和 Revision ID 均必须为非空 UUID。", nameof(plan));
        if (plan.ByteLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(plan), "Field Payload byte length 必须大于 0。");

        var hash = AssetRules.NormalizeContentHash(plan.ContentHash);
        string? relativePath;
        switch (plan.StorageKind)
        {
            case FieldPayloadStorageKind.SqliteBlob:
                if (plan.RelativePath is not null)
                    throw new CoreDomainDataException("SQLite BLOB Field Payload 不允许携带外部文件路径。");
                relativePath = null;
                break;
            case FieldPayloadStorageKind.ManagedFile:
                relativePath = NormalizeManagedPath(plan.RelativePath, hash);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), "未知 Field Payload storage kind。");
        }

        if (plan.CreatedAtUtcMs < 0)
            throw new ArgumentOutOfRangeException(nameof(plan), "Field Payload created_at 时间戳不能为负数。");

        return new FieldPayloadRecord
        {
            FieldPayloadId = plan.FieldPayloadId,
            ProjectId = plan.ProjectId,
            FieldDatasetId = plan.FieldDatasetId,
            FieldRevisionId = plan.FieldRevisionId,
            StorageKind = plan.StorageKind,
            ByteLength = plan.ByteLength,
            ContentHash = hash,
            RelativePath = relativePath,
            CreatedAtUtcMs = plan.CreatedAtUtcMs,
        };
    }

    /// <summary>
    /// Managed paths are content-addressed and package-relative. Requiring the exact
    /// canonical path prevents path aliases, traversal, drive roots and platform-specific
    /// separator ambiguity from entering authoritative metadata.
    /// </summary>
    public static string NormalizeManagedPath(string? relativePath, string contentHash)
    {
        var hash = AssetRules.NormalizeContentHash(contentHash);
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Managed File Field Payload 必须提供相对路径。", nameof(relativePath));

        var path = relativePath.Trim();
        var expected = ManagedPayloadPrefix + hash;
        if (!string.Equals(path, expected, StringComparison.Ordinal))
            throw new CoreDomainDataException($"Managed Field Payload path 必须为 {expected}。");
        return path;
    }
}
