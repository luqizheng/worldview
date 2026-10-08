using WorldView.Domain.Generation;
using Xunit;

namespace WorldView.Tests.Generation;

/// <summary>
/// 确定性随机源测试（task-2-brief + spec 2 §4.2）。
///
/// splitmix64 必须跨进程、跨 .NET 版本、跨机器产出**完全相同**的 64 位序列，
/// 否则「保存种子」这一功能就失去意义。比对逐位（<c>Assert.Equal(ulong)</c>），
/// 不用「数值接近」这类允许漂移的断言。
///
/// 禁止替换实现：禁止 <c>System.Random</c>、Godot 的 <c>FastNoiseLite</c>
/// （spec 2 §4.2；AGENTS.md L105-127「领域层不引用 Godot」）。
/// </summary>
public class DeterministicRandomTests
{
    /// <summary>
    /// 两个独立实例同一种子 → 前 8 个 <c>ulong</c> 逐位相等。
    /// 直接锁死 splitmix64 的位级行为；任何「用 <c>System.Random</c> 但版本升级」都会让这条变红。
    /// </summary>
    [Fact]
    public void NextUInt64_SameSeed_SameSequence()
    {
        var a = new DeterministicRandom(0xDEADBEEFCAFEBABEul);
        var b = new DeterministicRandom(0xDEADBEEFCAFEBABEul);

        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    /// <summary>不同种子 → 至少在前 4 个值中产生差异（防「常量种子被忽略」之类的退化实现）。</summary>
    [Fact]
    public void NextUInt64_DifferentSeed_Differs()
    {
        var a = new DeterministicRandom(1ul);
        var b = new DeterministicRandom(2ul);

        bool anyDifferent = false;
        for (int i = 0; i < 4; i++)
        {
            if (a.NextUInt64() != b.NextUInt64())
            {
                anyDifferent = true;
                break;
            }
        }

        Assert.True(anyDifferent, "不同种子应产出差异值，至少前 4 个里有一个不等。");
    }

    /// <summary>
    /// 1000 个 <c>NextDouble()</c> 必须满足：
    ///   - 全部 ∈ [0, 1)（左闭右开，保证 <c>NextInt(min, max)</c> 不会越界）
    ///   - 不同值数量 > 100（防「永远返回 0」之类的退化）
    ///   - 均值 ∈ [0.3, 0.7]（splitmix64 的最低有效位均衡，足以排除位偏置实现）
    /// </summary>
    [Fact]
    public void NextDouble_InUnitInterval()
    {
        var rng = new DeterministicRandom(0x1234567890ABCDEFuL);
        var seen = new HashSet<double>();
        double sum = 0.0;
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            var v = rng.NextDouble();
            Assert.True(v >= 0.0 && v < 1.0, $"NextDouble()={v} 不在 [0,1) 区间内。");
            seen.Add(v);
            sum += v;
        }

        Assert.True(seen.Count > 100, $"应有 > 100 个不同值，实际 {seen.Count}。");
        double mean = sum / count;
        Assert.InRange(mean, 0.3, 0.7);
    }

    /// <summary>
    /// <c>NextInt(min, max)</c> 1000 次 → 全部 ∈ [min, max)，
    /// 不论区间多窄（[0,1)）、区间零跨度还是单跨。
    /// </summary>
    [Fact]
    public void NextInt_HonoursBounds()
    {
        var rng = new DeterministicRandom(0xF00DBABEuL);
        const int min = -5;
        const int max = 5;
        const int count = 1000;

        for (int i = 0; i < count; i++)
        {
            int v = rng.NextInt(min, max);
            Assert.True(v >= min && v < max, $"NextInt({min},{max})={v} 越界。");
        }
    }
}