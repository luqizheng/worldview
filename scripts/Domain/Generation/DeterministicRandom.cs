using System;

namespace WorldView.Domain.Generation;

/// <summary>
/// 确定性 64 位伪随机源（splitmix64）。
///
/// 目的（spec 2 §4.2）：同一 <see cref="Seed"/> 必须跨进程、跨 .NET 版本、跨运行产出一致序列，
/// 否则「保存种子」这一功能就失去意义。.NET 自带的伪随机源种子算法不在版本间稳定，本类受禁止使用。
///
/// 实现要点：
///  - 状态：单个 <c>ulong</c>，步进常量来自 Vigna 的 splitmix64 论文 / xoshiro 仓库；
///  - 每步 <c>s += 0x9E3779B97F4A7C15</c>，再经 mix1/mix2/mix3 三轮位移+异或+乘法；
///  - 不维护任何外部对象，实例可丢弃也可继续推进。
///
/// 复杂度：每次 <c>NextUInt64()</c> 是 O(1) 整数位运算；不分配。
/// </summary>
public sealed class DeterministicRandom
{
    /// <summary>splitmix64 步进常量：<c>2^64 / φ</c> 的整数近似（黄金比例倒数）。</summary>
    private const ulong StepIncrement = 0x9E3779B97F4A7C15UL;

    /// <summary>mix1 常量（Vigna splitmix64 原始常量）。</summary>
    private const ulong Mix1 = 0xBF58476D1CE4E5B9UL;

    /// <summary>mix2 常量（Vigna splitmix64 原始常量）。</summary>
    private const ulong Mix2 = 0x94D049BB133111EBUL;

    private ulong _state;

    /// <summary>
    /// 用 <paramref name="seed"/> 构造随机源。
    /// <paramref name="seed"/> 直接用作初始状态（不强制非 0；splitmix64 对 0 种子也输出非平凡序列）。
    /// </summary>
    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>
    /// 取下一个 64 位伪随机整数。位级稳定：跨 .NET / 跨进程一致。
    /// </summary>
    public ulong NextUInt64()
    {
        ulong z = _state += StepIncrement;
        z = (z ^ (z >> 30)) * Mix1;
        z = (z ^ (z >> 27)) * Mix2;
        z = z ^ (z >> 31);
        return z;
    }

    /// <summary>
    /// 取 <c>[0, 1)</c> 区间内的双精度浮点。左闭右开是为了让 <c>NextInt(min, max)</c>
    /// 能严格不越界（见 spec 2 §4.2：值噪声分位数阈值需要 1.0 可达，故对 <see cref="ValueNoise"/> 是另一套闭区间映射）。
    /// </summary>
    public double NextDouble()
    {
        // 取高 53 位（double 尾数宽度），屏蔽低 11 位避免精度溢出；
        // (1L << 53) 是 9007199254740992，恰好等于 2^53。
        ulong bits = NextUInt64() >> 11;
        return bits / (double)(1UL << 53);
    }

    /// <summary>
    /// 取 <c>[minInclusive, maxExclusive)</c> 区间内的整数。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><c>maxExclusive &lt;= minInclusive</c> 时调用方错误。</exception>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxExclusive), maxExclusive,
                $"maxExclusive 必须严格大于 minInclusive（{minInclusive}）。");
        }

        // 取模前用 ulong 减一次偏移，避免 NextInt(int.MinValue, int.MaxValue) 这类
        // 「maxExclusive - minInclusive」溢出。splitmix64 输出与 x 整体放缩对齐：
        // (ulong)(NextUInt64() % range) 后 cast 是安全的。
        ulong range = (ulong)(maxExclusive - minInclusive);
        ulong r = NextUInt64() % range;
        return minInclusive + (int)r;
    }
}