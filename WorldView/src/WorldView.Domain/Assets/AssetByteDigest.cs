using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Domain.Assets;

/// <summary>
/// 实测字节摘要——不可伪造的验证凭证（Task 002-A）。唯一产生途径是
/// <see cref="MeasureAsync"/>：完整读取真实字节流并现场计算 SHA-256 与实际长度。
/// 构造器对 Domain 程序集外不可见，普通调用方无法凭空捏造与 Asset 元数据相等的
/// 摘要来伪造 Verified；Verified 写入因此只能来自真实字节验证流程
/// （IAssetStore.OpenReadAsync → MeasureAsync → MarkAssetVerified）。
/// 摘要与 Asset 元数据的比对归持久化写事务（MarkAssetVerified 事务内重读校验），
/// 本类型只承诺"摘要来自真实字节"，不承担进程外密码学对抗。
/// </summary>
public sealed class AssetByteDigest
{
    private AssetByteDigest(string contentHash, long byteLength)
    {
        ContentHash = contentHash;
        ByteLength = byteLength;
    }

    /// <summary>实测 SHA-256（小写十六进制；由真实字节流计算）。</summary>
    public string ContentHash { get; }

    /// <summary>实测字节长度（由真实字节流计算）。</summary>
    public long ByteLength { get; }

    /// <summary>
    /// 测量真实字节流：读完整个流，返回 SHA-256 与实际长度。这是获得本类型的唯一
    /// 公开途径——未经真实字节读取就不存在摘要实例。
    /// </summary>
    public static async Task<AssetByteDigest> MeasureAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        while (true)
        {
            var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            length = checked(length + read);
        }

        return new AssetByteDigest(
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            length);
    }
}
