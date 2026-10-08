using WorldView.Domain.Generation;
using Xunit;

namespace WorldView.Tests.Generation;

/// <summary>
/// 值噪声 + fBm 测试（task-2-brief + spec 2 §4.2）。
///
/// 哈希格点值噪声 + 双线性插值 + fBm 倍频叠加。必须跨运行、跨 .NET 版本一致。
/// 禁止 <c>System.Random</c>、Godot 的 <c>FastNoiseLite</c>。
/// </summary>
public class ValueNoiseTests
{
    /// <summary>同参两次调用相等（位级）；防「混入 <c>Random</c> 之类的随机源」之类的退化实现。</summary>
    [Fact]
    public void Sample_IsDeterministic()
    {
        double a = ValueNoise.Sample(0.37, 5.112, 0xABCDEFuL);
        double b = ValueNoise.Sample(0.37, 5.112, 0xABCDEFuL);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// 1000 个随机采样点全部 ∈ [0, 1]。
    /// 注意是闭区间 [0, 1]（不是 [0, 1)）：分位数阈值要从「最小」与「最大」两端都截得到，
    /// 必须包括 0 与 1，否则陆地比例上限到不了 100%。
    /// </summary>
    [Fact]
    public void Sample_InUnitRange()
    {
        var rng = new DeterministicRandom(0x55AA55AA55AAuL);
        for (int i = 0; i < 1000; i++)
        {
            double x = rng.NextDouble() * 1000.0 - 500.0;
            double y = rng.NextDouble() * 1000.0 - 500.0;
            ulong seed = rng.NextUInt64();
            double s = ValueNoise.Sample(x, y, seed);
            Assert.True(s >= 0.0 && s <= 1.0, $"Sample({x},{y},{seed})={s} 不在 [0,1]。");
        }
    }

    /// <summary>
    /// 相邻格点的格点值必须不同（否则噪声「塌」成常数）。
    /// 至少对 <c>seed=1</c>，<c>Sample(0,0,1)</c> 与 <c>Sample(1,0,1)</c> 不等。
    /// </summary>
    [Fact]
    public void LatticePoints_DifferBetweenCells()
    {
        const ulong s = 1ul;
        double a = ValueNoise.Sample(0.0, 0.0, s);
        double b = ValueNoise.Sample(1.0, 0.0, s);

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// 插值必须严格在两端格点值之间（不外推、不震荡）。
    /// 中点 <c>(0.5, 0)</c> 应严格大于 <c>min</c> 且严格小于 <c>max</c>，
    /// 而且至少要在两端值之间（不要忽略插值直接返回常数）。
    /// </summary>
    [Fact]
    public void Interpolation_IsStrictlyBetweenLatticeValues()
    {
        const ulong s = 1ul;
        double v0 = ValueNoise.Sample(0.0, 0.0, s);
        double v1 = ValueNoise.Sample(1.0, 0.0, s);
        double mid = ValueNoise.Sample(0.5, 0.0, s);

        double lo = Math.Min(v0, v1);
        double hi = Math.Max(v0, v1);
        Assert.True(mid > lo && mid < hi,
            $"Sample(0.5,0)={mid} 不在 ({lo}, {hi}) 之间。");
    }

    /// <summary>同参两次 fBm 相等。</summary>
    [Fact]
    public void Fbm_IsDeterministic()
    {
        double a = ValueNoise.Fbm(2.0, 4.0, 0xCAFEuL, octaves: 5);
        double b = ValueNoise.Fbm(2.0, 4.0, 0xCAFEuL, octaves: 5);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// octaves=6 较 octaves=2 在同一批采样点上至少有 20% 不同（高 octaves 必须真引入细节）。
    /// 用一个固定种子 + 256 个采样点；20% ≈ 52 个，足以排除「octaves 参数被忽略」。
    /// </summary>
    [Fact]
    public void Fbm_MoreOctaves_AddsDetail()
    {
        const ulong seed = 0xBEEFDEADuL;
        var rng = new DeterministicRandom(seed);
        const int samples = 256;
        int differing = 0;

        for (int i = 0; i < samples; i++)
        {
            double x = rng.NextDouble() * 50.0;
            double y = rng.NextDouble() * 50.0;
            double a = ValueNoise.Fbm(x, y, seed, octaves: 2);
            double b = ValueNoise.Fbm(x, y, seed, octaves: 6);
            if (a != b)
            {
                differing++;
            }
        }

        // 20% 门槛（256 × 0.20 = 51.2）
        Assert.True(differing > samples * 0.20,
            $"octaves=6 vs octaves=2 仅 {differing}/{samples} 个采样点不同，期望 ≥20%。");
    }
}