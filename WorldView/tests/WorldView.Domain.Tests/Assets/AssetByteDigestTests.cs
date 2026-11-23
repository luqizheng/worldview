using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WorldView.Domain.Assets;
using Xunit;

namespace WorldView.Domain.Tests.Assets;

/// <summary>
/// Task 002-A/002-B：实测字节摘要是唯一可写入 Verified 的凭证。002-B 收紧后
/// 构造器私有、MeasureAsync internal——public 面不存在任何摘要创建入口；
/// 创建能力仅经 InternalsVisibleTo 授予 Application 验证路径与摘要测试程序集
/// （本测试程序集为授权 friend，直接调用 MeasureAsync 本身就是 C.4 授权生效的证明）。
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
    public void Digest_CannotBeConstructedOutsideAuthorizedPath()
    {
        // Task 002-B API Surface 锁定（Task 002-A 只有 public MeasureAsync；002-B 起全空）：
        // 1) 无公开实例构造器；
        // 2) 无任何 public 静态成员——MeasureAsync 与其他任何 public 摘要创建工厂均不存在；
        // 3) 唯一创建入口是 internal MeasureAsync（非 public、非 private）。
        Assert.Empty(typeof(AssetByteDigest).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(AssetByteDigest).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly));

        var factory = typeof(AssetByteDigest).GetMethod(
            "MeasureAsync", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.NotNull(factory);
        Assert.True(factory!.IsAssembly, "MeasureAsync 必须保持 internal 可见性。");
    }

    [Fact]
    public void MeasureCreation_IsAuthorizedOnlyToApplicationVerificationPathAndTestAssemblies()
    {
        // Task 002-B 精确授权锁定：friend 集合必须恰为 Application 验证路径 + 两个
        // 摘要测试程序集；任何新增 friend（如 Infrastructure/Application 生产程序集）
        // 都会让本测试失败，防止摘要创建能力悄悄扩散。
        var friends = typeof(AssetByteDigest).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToHashSet(StringComparer.Ordinal);

        var expected = new[]
        {
            "WorldView.Application",
            "WorldView.Domain.Tests",
            "WorldView.Infrastructure.Tests",
        }.ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected, friends);
    }
}
