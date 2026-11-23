using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Domain.Assets;

/// <summary>
/// 实测字节摘要——不可伪造的验证凭证（Task 002-A；创建权限经 Task 002-B 收紧）。
/// 摘要只能经 <see cref="MeasureAsync"/> 产生：完整读取真实字节流并现场计算
/// SHA-256 与实际长度。构造器私有、<see cref="MeasureAsync"/> 为 internal 且仅经
/// InternalsVisibleTo 授权给 Application 验证路径（VerifyAssetIntegrityUseCase）——
/// 任意调用方无法绕过 IAssetStore.OpenReadAsync 自行测量伪造摘要，也不存在任何
/// public 摘要创建工厂。摘要与 Asset 元数据的比对归持久化写事务
/// （MarkAssetVerified 事务内重读校验）；本类型只承诺"摘要来自真实字节"，
/// 不承担进程外密码学对抗。
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
    /// 测量真实字节流：读完整个流，返回 SHA-256 与实际长度。仅授权给 Application
    /// 的真实字节验证路径（VerifyAssetIntegrityUseCase，经 IAssetStore.OpenReadAsync
    /// 取流）与摘要行为/API Surface 测试程序集；普通外部调用方不可见——未经真实
    /// 字节读取就不存在摘要实例。
    /// </summary>
    internal static async Task<AssetByteDigest> MeasureAsync(
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
