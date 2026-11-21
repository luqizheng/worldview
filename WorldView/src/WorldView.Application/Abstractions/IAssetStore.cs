using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// 项目 Asset Store 契约：内容寻址（SHA-256）、不可变的字节存取（AF-01 §3.1/§7.2）。
/// Core Domain 拥有 Asset 身份与正式引用；本契约只负责字节放置与取回。
/// 大几何/栅格负载不进入 SQLite 行（AF-01 §8.3）。
/// </summary>
public interface IAssetStore : IAsyncDisposable
{
    /// <summary>Asset Store 根目录（绝对路径）。</summary>
    string RootPath { get; }

    /// <summary>
    /// 写入不可变 Asset 字节：流式计算 SHA-256，先写临时文件再原子发布。
    /// 内容已存在时直接复用现有文件（内容寻址幂等）。
    /// </summary>
    Task<AssetPutResult> PutAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>打开已发布 Asset 的只读流；不存在时返回 null（缺失 Asset 是完整性错误，绝不静默替换，AF-01 §9）。</summary>
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>检查内容哈希对应的 Asset 是否已发布。</summary>
    bool Exists(string contentHash);
}

/// <summary>Asset 写入结果。</summary>
public sealed record AssetPutResult
{
    public required bool Success { get; init; }

    /// <summary>内容的 SHA-256（十六进制小写）。</summary>
    public string? ContentHash { get; init; }

    /// <summary>诊断说明（失败时）。</summary>
    public string? Diagnostic { get; init; }

    public static AssetPutResult Ok(string contentHash) => new() { Success = true, ContentHash = contentHash };

    public static AssetPutResult Failed(string diagnostic) => new() { Success = false, Diagnostic = diagnostic };
}

/// <summary>按包根创建 Asset Store 适配器。</summary>
public interface IAssetStoreFactory
{
    /// <summary>为指定包根创建 Asset Store（不校验包有效性；校验属包校验器职责）。</summary>
    IAssetStore CreateForPackage(string packageRootPath);
}
