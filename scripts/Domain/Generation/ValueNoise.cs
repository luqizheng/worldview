using System;

namespace WorldView.Domain.Generation;

/// <summary>
/// 值噪声 + fBm（倍频叠加）。
///
/// 目的（spec 2 §4.2）：世界地图生成器在采样坐标上加方向偏移、做大陆遮罩、做细节层，
/// 全部基于这里产出的 2D 标量场。同一坐标 + 同一 <c>seed</c> 必须产出**完全相同**的值，
/// 跨进程、跨 .NET 版本一致；因此依赖 <see cref="DeterministicRandom"/>（splitmix64）做格点哈希。
///
/// 算法（典型 Ken Perlin 改进值噪声）：
///  - 对每个采样点 (x, y)，找到围绕它的 4 个整数格点 (x0,y0)/(x1,y0)/(x0,y1)/(x1,y1)；
///  - 每个格点用 (seed, ix, iy) → <see cref="DeterministicRandom"/> 哈希到一个 [0, 1] 实数；
///  - 在 x 方向做两次平滑插值（缓和函数 <c>s(t) = t*t*(3 - 2t)</c>），
///    再在 y 方向做一次，得到平滑的双线性插值结果；
///  - <see cref="Fbm"/> 把多 octaves 加权叠加（<c>lacunarity</c> 倍频 / <c>gain</c> 振幅），
///    各 octave 自带 <c>seed + offset</c> 隔离避免共振。
///
/// 注：本实现纯位运算，不依赖任何 Godot 噪声类（spec 2 §4.2：领域层不引用 Godot）。
/// </summary>
public static class ValueNoise
{
    /// <summary>
    /// 在 2D 平面上采样一点。返回值 ∈ [0, 1]（闭区间，便于分位数阈值命中 1.0）。
    /// </summary>
    /// <param name="x">横向坐标（浮点格；不要求整数）。</param>
    /// <param name="y">纵向坐标（浮点格）。</param>
    /// <param name="seed">种子，决定格点哈希；相同坐标 + 相同 seed → 完全相同值。</param>
    public static double Sample(double x, double y, ulong seed)
    {
        int x0 = FloorToInt(x);
        int y0 = FloorToInt(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        // 局部坐标 ∈ [0, 1]，再做缓和曲线消除单调线性插值的视觉轴线。
        double tx = Smooth(t: x - x0);
        double ty = Smooth(t: y - y0);

        // 4 个格点值（每个用 (seed, ix, iy) 哈希）。
        double v00 = LatticeValue(x0, y0, seed);
        double v10 = LatticeValue(x1, y0, seed);
        double v01 = LatticeValue(x0, y1, seed);
        double v11 = LatticeValue(x1, y1, seed);

        // 双线性插值（先 x 后 y）。
        double ix0 = Lerp(v00, v10, tx);
        double ix1 = Lerp(v01, v11, tx);
        return Lerp(ix0, ix1, ty);
    }

    /// <summary>
    /// 倍频叠加（fractal Brownian motion）。
    /// 各 octave 的频率按 <paramref name="lacunarity"/> 倍增、振幅按 <paramref name="gain"/> 衰减；
    /// 总和归一化回 [0, 1]，保证调用方拿到与 <see cref="Sample"/> 同一值域。
    /// </summary>
    /// <param name="x">横向坐标。</param>
    /// <param name="y">纵向坐标。</param>
    /// <param name="seed">种子；各 octave 通过加 <c>offset</c> 隔离。</param>
    /// <param name="octaves">层数；必须 ≥ 1。</param>
    /// <param name="lacunarity">频率倍率（默认 2.0；典型 Perlin 值）。</param>
    /// <param name="gain">振幅倍率（默认 0.5；典型 Perlin 值）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="octaves"/> &lt; 1 或
    ///     <paramref name="lacunarity"/> / <paramref name="gain"/> 非正数。</exception>
    public static double Fbm(
        double x,
        double y,
        ulong seed,
        int octaves,
        double lacunarity = 2.0,
        double gain = 0.5)
    {
        if (octaves < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(octaves), octaves, "octaves 必须 ≥ 1。");
        }
        if (lacunarity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lacunarity), lacunarity, "lacunarity 必须为正数。");
        }
        if (gain <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gain), gain, "gain 必须为正数。");
        }

        double sum = 0.0;
        double amplitude = 1.0;
        double frequency = 1.0;
        // 把 gain 几何级数归一化用：每层贡献是 gain^i，
        // 把 gain^i 求和后做分母，保证「所有 octave 总能量归一」到 [0, 1]。
        double amplitudeSum = 0.0;

        // 偏移常量：把 octave i 的 seed 隔离（避免同一 (x,y) 的格点哈希值跨 octave 撞车）。
        // 各 octave 用 seed + i 作为哈希源，格点频率按 lacunarity 倍增。
        for (int i = 0; i < octaves; i++)
        {
            double sx = x * frequency;
            double sy = y * frequency;
            // 用 i + 7 作 seed 偏移（避免被外部 seed 选择锁定）。
            // 注意：seed 必须为 ulong，这里用位运算拼偏移保证不会溢出。
            ulong octaveSeed = MixSeed(seed, (ulong)i + 7UL);

            // Sample 输出 [0,1]；振幅乘 gain 后总和归一。
            sum += Sample(sx, sy, octaveSeed) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        // 归一化到 [0, 1]。Sample() 输出恒为 [0, 1]，加权平均后也在该区间。
        return sum / amplitudeSum;
    }

    // ──── 内部辅助 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 把双重哈希的格点值映射到 [0, 1]。
    /// 用 (seed, ix, iy) 喂 <see cref="DeterministicRandom"/>，取首个 <c>NextUInt64()</c>，
    /// 按 53 位尾数宽度归一化到 [0, 1]。
    /// </summary>
    private static double LatticeValue(int ix, int iy, ulong seed)
    {
        // 混合 (seed, ix, iy) 到一个 64 位状态，再用 splitmix64 取一个输出。
        // 不必两次展开：ix/iy 各自拆 seed 已经在 MixSeed 里做了。
        ulong s0 = MixSeed(seed, (ulong)(uint)ix + 0x9E3779B97F4A7C15UL);
        ulong s1 = MixSeed(s0, (ulong)(uint)iy + 0xBF58476D1CE4E5B9UL);

        var rng = new DeterministicRandom(s1);
        // 取高 53 位（double 尾数宽度），与 DeterministicRandom.NextDouble 口径一致；
        // 这样格点值与 fBm 内插值在同一定义域内，输出在 [0, 1]。
        ulong bits = rng.NextUInt64() >> 11;
        return bits / (double)(1UL << 53);
    }

    /// <summary>把 seed 与扰动值做一次 splitmix64 风格的混合，避免直接相加产生简单线性依赖。</summary>
    private static ulong MixSeed(ulong seed, ulong perturb)
    {
        ulong z = seed + perturb + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return z;
    }

    /// <summary>
    /// Perlin 缓和曲线：<c>t*t*(3 - 2t)</c>。一阶导数 0/1 端为 0，
    /// 消除线性插值的折线，格点拼接处一阶可导。
    /// </summary>
    private static double Smooth(double t) => t * t * (3.0 - 2.0 * t);

    /// <summary>线性插值；参数 <paramref name="t"/> = 1 时返回 end，= 0 时返回 start。</summary>
    private static double Lerp(double start, double end, double t) => start + (end - start) * t;

    /// <summary>
    /// 浮点 → 整数「向下」取整，手写以避免对 <c>Math.Floor</c> 的 double↔long 转换样板。
    /// 注意 <c>Math.Floor</c> 在负数端也能正确向下；这里等价于 <c>(int)Math.Floor(x)</c>，
    /// 但省了一次 double→long→double 的往返，噪声采样这种热点路径值得优化。
    /// </summary>
    private static int FloorToInt(double x) => (int)Math.Floor(x);
}