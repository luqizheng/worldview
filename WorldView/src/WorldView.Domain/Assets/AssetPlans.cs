using System;

namespace WorldView.Domain.Assets;

/// <summary>Asset 元数据登记计划（字节层已发布并 hash 验证之后的权威登记）。</summary>
public sealed record RegisterAssetPlan
{
    public required Guid AssetId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>内容 SHA-256（小写十六进制；发布流程中由字节层计算并验证）。</summary>
    public required string ContentHash { get; init; }

    public required long ByteLength { get; init; }

    public required string MediaType { get; init; }

    public required string FormatVersion { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>Definition Asset link 建立计划。</summary>
public sealed record LinkDefinitionAssetPlan
{
    public required Guid ObjectDefinitionAssetId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    public required Guid AssetId { get; init; }

    /// <summary>已声明的 geometry role（registry 校验）。</summary>
    public required string Role { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}
