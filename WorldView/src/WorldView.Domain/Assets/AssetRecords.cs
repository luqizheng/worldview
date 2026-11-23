using System;
using System.Security.Cryptography;
using System.Text;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Assets;

/// <summary>
/// Core Asset 权威元数据行（core_asset，P2-09-11 §4.6）。Asset 字节不可变且存于
/// SQLite 之外（内容寻址 FileAssetStore：Assets/sha256/&lt;content-hash&gt;）；本行只承载
/// 语义元数据与完整性状态——不是第二份字节权威。
/// </summary>
public sealed record AssetRecord
{
    public required Guid AssetId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>内容 SHA-256（小写十六进制；与字节层一致，发布前已由字节层验证）。</summary>
    public required string ContentHash { get; init; }

    /// <summary>V1 恒为 sha-256（CHECK 强制）。</summary>
    public required string HashAlgorithm { get; init; }

    public required long ByteLength { get; init; }

    /// <summary>负载媒体类型（如 application/json；与 format_version 共同识别编码）。</summary>
    public required string MediaType { get; init; }

    /// <summary>负载格式版本（编码自有版本，独立于 Object Type/Command payload 版本）。</summary>
    public required string FormatVersion { get; init; }

    /// <summary>包内相对路径（内容寻址约定：Assets/sha256/&lt;hash&gt;；路径圈禁由规则校验）。</summary>
    public required string RelativePath { get; init; }

    /// <summary>V1 恒为 Published（AI staging 永不进入 core_asset，P2-09-06 §5/§6）。</summary>
    public AssetPublicationState PublicationState { get; init; }

    /// <summary>完整性扫描状态（服务管理；非权威写入路径的直接输入）。</summary>
    public required AssetIntegrityState IntegrityState { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>
/// 字节验证工作流的摘要证据。持久化适配器会在写事务内校验该证据与当前元数据一致；
/// 此 DTO 不是抵御恶意进程伪造摘要的密码学凭证。
/// </summary>
public sealed record AssetVerificationEvidence(
    string ExpectedHash,
    string ActualHash,
    long ExpectedByteLength,
    long ActualByteLength);

/// <summary>发布状态（V1 单态：core_asset 只登记已发布 Asset）。</summary>
public enum AssetPublicationState
{
    Published,
}

/// <summary>完整性状态（扫描结果；corrupt/missing 不得作为正式引用的新目标）。</summary>
public enum AssetIntegrityState
{
    /// <summary>字节层已验证与 hash 一致。</summary>
    Verified,

    /// <summary>尚未经字节级验证（登记默认值）。</summary>
    Unverified,

    /// <summary>字节存在但 hash 不符（损坏）。</summary>
    Corrupt,

    /// <summary>字节缺失。</summary>
    Missing,
}

/// <summary>Definition geometry typed link 行（core_object_definition_asset；P2-09-19 §9）。
/// 仅 owner keys + asset_id + role——绝不携带 content hash 或组件 ID（CD-02-A §12）。</summary>
public sealed record ObjectDefinitionAssetLinkRecord
{
    public required Guid ObjectDefinitionAssetId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    public required Guid AssetId { get; init; }

    /// <summary>typed-link purpose。Building/Cave typed aggregate V1 使用 geometry_component；legacy compact descriptors 保留其声明的 role。</summary>
    public required string Role { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>Core Asset 领域规则（hash 格式/路径圈禁/状态规则）。</summary>
public static class AssetRules
{
    /// <summary>SHA-256 内容 hash 规范形式：64 位小写十六进制。</summary>
    public static string NormalizeContentHash(string? contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("content_hash 不能为空白。", nameof(contentHash));
        }

        var hash = contentHash.Trim();
        if (hash.Length != 64 || !IsLowerHex(hash))
        {
            throw new ArgumentException(
                $"content_hash \"{hash}\" 不是 64 位小写十六进制 SHA-256 表示。", nameof(contentHash));
        }

        return hash;
    }

    /// <summary>验证既有 hash（读侧 fail-closed 用）。</summary>
    public static bool IsSha256Hex(string text) => text.Length == 64 && IsLowerHex(text);

    private static bool IsLowerHex(string text)
    {
        foreach (var c in text)
        {
            var isLowerHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!isLowerHex)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>内容寻址包内相对路径（Assets/sha256/&lt;hash&gt;；拒绝遍历段与绝对路径）。</summary>
    public static string ContentAddressedRelativePath(string contentHash)
    {
        var normalized = NormalizeContentHash(contentHash);
        return $"Assets/sha256/{normalized}";
    }

    /// <summary>relative_path 校验：包内相对路径（无盘符/根段/..遍历；正斜杠规范化）。</summary>
    public static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("relative_path 不能为空白。", nameof(relativePath));
        }

        var path = relativePath.Trim().Replace('\\', '/');
        if (path.StartsWith('/') || path.EndsWith('/') || path.Contains("//")
            || path.Contains(':') || path.Contains(".."))
        {
            throw new ArgumentException($"relative_path \"{path}\" 非法（仅允许包内正斜杠相对路径）。", nameof(relativePath));
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0)
            {
                throw new ArgumentException($"relative_path \"{path}\" 含空段。", nameof(relativePath));
            }
        }

        return path;
    }

    /// <summary>SHA-256（小写十六进制）——Domain 职责的字节摘要计算（与 FileAssetStore 一致）。</summary>
    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>hash 与字节摘要不一致 → 数据损坏（fail-closed）。</summary>
    public static void ValidateHashMatches(byte[] bytes, string expectedHash)
    {
        var actual = Sha256Hex(bytes);
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException(
                $"Asset 字节 hash 与元数据不一致（预期 {expectedHash}，实际 {actual}）：内容损坏或被篡改。");
        }
    }
}
