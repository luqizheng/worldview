using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;

namespace WorldView.Infrastructure.Assets;

/// <summary>
/// 目录式内容寻址 Asset Store（AF-01 §3.1：Assets/sha256/&lt;content-hash&gt;）。
/// 写入流程：临时文件 → 流式 SHA-256 → 同名发布（已存在即复用，内容寻址幂等）。
/// 哈希不匹配的既有文件（外部篡改/损坏）在发布前即被拒绝。
/// </summary>
public sealed class FileAssetStore : IAssetStore
{
    private readonly object _gate = new();
    private bool _disposed;

    public FileAssetStore(string packageRootPath)
    {
        RootPath = ProjectPackageStructure.AssetHashRoot(packageRootPath);
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public async Task<AssetPutResult> PutAsync(Stream content, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        var tempPath = Path.Combine(RootPath, $".staging-{Guid.NewGuid():N}.tmp");
        string contentHash;
        try
        {
            await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                content.Position = 0;
                contentHash = await ComputeHashAndCopyAsync(content, target, cancellationToken).ConfigureAwait(false);
            }

            var finalPath = Path.Combine(RootPath, contentHash);
            if (File.Exists(finalPath))
            {
                // 内容寻址幂等：验证既有文件哈希一致后复用；不一致说明既有文件损坏/被篡改。
                await using var existing = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var existingHash = await ComputeHashAsync(existing, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(existingHash, contentHash, StringComparison.Ordinal))
                {
                    return AssetPutResult.Failed("既有 Asset 文件内容与哈希名不符（疑似损坏或被篡改），写入已拒绝。");
                }
            }
            else
            {
                File.Move(tempPath, finalPath);
            }
        }
        catch (OperationCanceledException)
        {
            SafeDeleteTemp(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            SafeDeleteTemp(tempPath);
            return AssetPutResult.Failed($"Asset 写入失败：{ex.Message}");
        }
        finally
        {
            SafeDeleteTemp(tempPath);
        }

        return AssetPutResult.Ok(contentHash);
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        if (!IsSafeContentHash(contentHash))
        {
            throw new ArgumentException("非法内容哈希。", nameof(contentHash));
        }

        var path = Path.Combine(RootPath, contentHash);
        if (!File.Exists(path))
        {
            // 缺失 Asset 是完整性错误；调用方必须按完整性失败处理，绝不静默替换（AF-01 §9）。
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public bool Exists(string contentHash)
    {
        if (!IsSafeContentHash(contentHash))
        {
            return false;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return File.Exists(Path.Combine(RootPath, contentHash));
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }

    private static async Task<string> ComputeHashAndCopyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        using var hashAlgorithm = SHA256.Create();
        await using (var cryptoStream = new CryptoStream(destination, hashAlgorithm, CryptoStreamMode.Write))
        {
            await source.CopyToAsync(cryptoStream, cancellationToken).ConfigureAwait(false);
            await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
        }

        return ToHex(hashAlgorithm.Hash ?? throw new InvalidOperationException("哈希计算失败。"));
    }

    private static async Task<string> ComputeHashAsync(Stream source, CancellationToken cancellationToken)
    {
        using var hashAlgorithm = SHA256.Create();
        await using (var cryptoStream = new CryptoStream(Stream.Null, hashAlgorithm, CryptoStreamMode.Write, leaveOpen: false))
        {
            await source.CopyToAsync(cryptoStream, cancellationToken).ConfigureAwait(false);
            await cryptoStream.FlushFinalBlockAsync(cancellationToken).ConfigureAwait(false);
        }

        return ToHex(hashAlgorithm.Hash ?? throw new InvalidOperationException("哈希计算失败。"));
    }

    private static string ToHex(byte[] hash) => Convert.ToHexString(hash).ToLowerInvariant();

    /// <summary>内容哈希必须是 64 位十六进制小写串（防路径注入）。</summary>
    private static bool IsSafeContentHash(string contentHash)
    {
        if (contentHash.Length != 64)
        {
            return false;
        }

        foreach (var ch in contentHash)
        {
            var isHex = (ch is >= '0' and <= '9') || (ch is >= 'a' and <= 'f');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static void SafeDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // 临时文件清理失败不掩盖主流程结果。
        }
    }
}

/// <summary>按包根创建文件 Asset Store。</summary>
public sealed class FileAssetStoreFactory : IAssetStoreFactory
{
    public IAssetStore CreateForPackage(string packageRootPath) => new FileAssetStore(packageRootPath);
}
