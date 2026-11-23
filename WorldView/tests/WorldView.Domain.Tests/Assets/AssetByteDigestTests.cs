using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using WorldView.Domain.Assets;
using Xunit;

namespace WorldView.Domain.Tests.Assets;

/// <summary>
/// Task 002-A：实测字节摘要是唯一可写入 Verified 的凭证。摘要只能经 MeasureAsync
/// 读取真实字节流产生；无公开构造器意味着普通调用方无法凭空捏造与元数据相等的
/// 摘要来伪造 Verified（未经真实字节读取就不存在摘要实例）。
/// </summary>
public sealed class AssetByteDigestTests
{
    [Fact]
    public async Task MeasureAsync_ComputesSha256AndLengthFromActualBytes()
    {
        var bytes = "measured-bytes"u8.ToArray();

        var digest = await AssetByteDigest.MeasureAsync(new MemoryStream(bytes));

        Assert.Equal(AssetRules.Sha256Hex(bytes), digest.ContentHash);
        Assert.Equal(bytes.LongLength, digest.ByteLength);
    }

    [Fact]
    public async Task MeasureAsync_LargeStream_StreamsIncrementally()
    {
        // 跨多个 81920 缓冲区：验证分块读取不会丢块（长度与 hash 均按全量字节）。
        var bytes = new byte[200_000];
        new Random(7).NextBytes(bytes);

        var digest = await AssetByteDigest.MeasureAsync(new MemoryStream(bytes));

        Assert.Equal(AssetRules.Sha256Hex(bytes), digest.ContentHash);
        Assert.Equal(bytes.LongLength, digest.ByteLength);
    }

    [Fact]
    public async Task MeasureAsync_EmptyStream_ProducesZeroLengthSha256()
    {
        var digest = await AssetByteDigest.MeasureAsync(new MemoryStream(Array.Empty<byte>()));

        Assert.Equal(AssetRules.Sha256Hex(Array.Empty<byte>()), digest.ContentHash);
        Assert.Equal(0, digest.ByteLength);
    }

    [Fact]
    public async Task MeasureAsync_NullStream_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AssetByteDigest.MeasureAsync(null!));
    }

    [Fact]
    public void Digest_CannotBeConstructedOutsideMeasurement()
    {
        // 防伪造边界：无公开实例构造器；公开静态成员只有 MeasureAsync。
        Assert.Empty(typeof(AssetByteDigest).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var publicFactories = typeof(AssetByteDigest)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();
        Assert.Equal(new[] { nameof(AssetByteDigest.MeasureAsync) }, publicFactories);
    }
}
