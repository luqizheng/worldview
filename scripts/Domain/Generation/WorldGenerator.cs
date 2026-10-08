using WorldView.Domain;

namespace WorldView.Domain.Generation;

/// <summary>
/// 世界生成器：把 <see cref="GenerationParams"/> 转成一张完整的初稿世界地图 cells。
///
/// <para>流水线（spec 2 §4.1，顺序固定）：</para>
/// <list type="number">
///   <item>大陆遮罩 fBm —— 2 octaves / gain 0.5，频率按图宽派生（2π/Width × 2）。</item>
///   <item>域扭曲 —— 用两个独立 fBm 场做 (warpX, warpY) 偏移后重采样遮罩。</item>
///   <item>细节叠加 —— 高频 fBm 5..7 octaves，按 <c>ruggedness</c> 衰减权重后加回遮罩。</item>
///   <item>分位数阈值 —— 把全图 mask 升序排，取第 <c>round((1-landRatio)*count)</c> 位作 τ；mask &gt; τ 为 land。</item>
///   <item>调 <see cref="CoastlineRefiner.Refine"/>（含形态学 + 重标 coast）—— 入参 <c>Morphology: true</c>。</item>
/// </list>
///
/// <para>输出口径（spec §4.1 + brief §5）：</para>
/// <list type="bullet">
///   <item>仅写陆地（plain + coast），水隐含默认。</item>
///   <item><see cref="GeneratedWorld.DefaultType"/> 恒为 <see cref="TerrainType.Water"/>。</item>
///   <item><see cref="GeneratedWorld.Cells"/> 按 <c>(Y, X)</c> 升序排（与 <c>EditorContext.SetTerrainCells</c> 一致）。</item>
///   <item><see cref="GeneratedWorld.Stats"/> 透传自 <see cref="CoastlineRefiner.Refine"/>。</item>
/// </list>
///
/// <para>硬约束（spec 2 §4.2 红线）：</para>
/// <list type="bullet">
///   <item>领域层纯 C#，不引用 <c>Godot.*</c>。</item>
///   <item>随机只用 <see cref="ValueNoise"/>，**不用** <c>System.Random</c> / <c>FastNoiseLite</c>。</item>
/// </list>
///
/// <para>确定性：同 <see cref="GenerationSpec"/> + 同 <see cref="GenerationParams"/> 必产**位级相同**的
/// <see cref="GeneratedWorld.Cells"/>（test <c>SameSeed_ProducesIdenticalCells</c> 锁住）。</para>
/// </summary>
public static class WorldGenerator
{
    /// <summary>当前算法版本（与 <see cref="GenerationSpec.Algorithm"/> 默认值一致）。</summary>
    public const string Algorithm = "coastline_v1";

    // ──── 内部常量（自选常数；理由见 task-5-report §8.3） ────────────────
    private const double WarpStrengthScale = 1.5;   // ruggedness 0..1 → 偏移 0..1.5 格
    private const double DetailMaxWeight = 0.20;   // ruggedness 0..1 → 细节振幅 0..0.20
    private const int DetailOctaves = 6;            // spec §4.1 范围 5..7，取中
    private const int MaskOctaves = 2;              // spec §4.1 步 ①

    /// <summary>
    /// 把 <paramref name="p"/> 转成一张 <see cref="GeneratedWorld"/>。
    /// 完整 5 步流水线，详见类注释。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 任一防御性检查越界（width/height ≤ 0，或 spec 字段越出 §9 区间）。
    /// </exception>
    public static GeneratedWorld Generate(GenerationParams p)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(p.Spec);

        // ─── 防御性检查（task-5-brief §9） ───
        if (p.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Width, "Width 必须 > 0。");
        }
        if (p.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Height, "Height 必须 > 0。");
        }
        if (p.Spec.LandRatio < GenerationLimits.MinLandRatio
            || p.Spec.LandRatio > GenerationLimits.MaxLandRatio)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Spec.LandRatio,
                $"LandRatio 必须在 [{GenerationLimits.MinLandRatio:0.00}, {GenerationLimits.MaxLandRatio:0.00}] 区间。");
        }
        if (p.Spec.Ruggedness < GenerationLimits.MinRuggedness
            || p.Spec.Ruggedness > GenerationLimits.MaxRuggedness)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Spec.Ruggedness,
                $"Ruggedness 必须在 [{GenerationLimits.MinRuggedness:0.##}, {GenerationLimits.MaxRuggedness:0.##}] 区间。");
        }
        if (p.Spec.Erosion < GenerationLimits.MinErosion
            || p.Spec.Erosion > GenerationLimits.MaxErosion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Spec.Erosion,
                $"Erosion 必须在 [{GenerationLimits.MinErosion}, {GenerationLimits.MaxErosion}] 区间。");
        }
        if (p.Spec.MinIslandCells < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Spec.MinIslandCells, "MinIslandCells 必须 ≥ 0。");
        }
        if (p.Spec.MinLakeCells < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(p), p.Spec.MinLakeCells, "MinLakeCells 必须 ≥ 0。");
        }

        int width = p.Width;
        int height = p.Height;
        int count = width * height;

        // GenerationSpec.Seed 是 long；ValueNoise 要 ulong。负种子按 unchecked 折叠
        // （R5：T2 → T5 接口约定）。这与 splitmix64 在 ulong 上的等价类一致。
        ulong seed = unchecked((ulong)p.Spec.Seed);

        // ─── 频率常量（brief §4.1） ───
        // 2-4 周期/图 → lowFreq = 2π / Width × 2。
        // midFreq = lowFreq（spec 没细分；用 1× 频段即可，brief §4.1 步 ②）。
        // 5-7 倍频 → highFreq ≈ Width / 16；octaves = 6（中值）。
        double lowFreq = 2.0 * Math.PI / width * 2.0;
        double midFreq = lowFreq;
        double highFreq = (double)width / 16.0;

        // ─── ① 大陆遮罩 fBm ───
        // 2 octaves，gain 0.5（默认值；明确写出便于 reviewer 校对）。
        // Fbm(x*lowFreq, y*lowFreq, seed, octaves:2, lacunarity:2.0, gain:0.5)
        var mask = new double[count];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[y * width + x] = ValueNoise.Fbm(
                    x * lowFreq, y * lowFreq, seed,
                    octaves: MaskOctaves, lacunarity: 2.0, gain: 0.5);
            }
        }

        // ─── ② 域扭曲 ───
        // warpX = Fbm(seed+1, x*midFreq, y*midFreq, octaves:2) * warpStrength
        // warpY = Fbm(seed+2, x*midFreq, y*midFreq, octaves:2) * warpStrength
        // 重采样 mask at (x + warpX, y + warpY)。
        // warpStrength = ruggedness × 1.5（0..1 → 0..1.5 格的最大偏移）。
        double warpStrength = p.Spec.Ruggedness * WarpStrengthScale;
        if (warpStrength > 0.0)
        {
            var warped = new double[count];
            ulong seedWarpX = seed + 1UL;
            ulong seedWarpY = seed + 2UL;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double wx = ValueNoise.Fbm(
                        x * midFreq, y * midFreq, seedWarpX,
                        octaves: 2, lacunarity: 2.0, gain: 0.5) * warpStrength;
                    double wy = ValueNoise.Fbm(
                        x * midFreq, y * midFreq, seedWarpY,
                        octaves: 2, lacunarity: 2.0, gain: 0.5) * warpStrength;
                    warped[y * width + x] = SampleMask(mask, width, height, x + wx, y + wy);
                }
            }
            mask = warped;
        }

        // ─── ③ 细节叠加 ───
        // detail = Fbm(seed+3, x*highFreq, y*highFreq, octaves:6) * detailWeight
        // mask += detail
        // detailWeight = 0.20 * ruggedness（0..1 → 0..0.20）。
        // 理由（task-5-report §8.3）：spec §4.1 步 ③ 说「5–7 倍频」,ruggedness 0..1
        // 应当单调映射到「细节强度」上,与 warpStrength 同向——崎岖度越高,陆地边界
        // 越破碎,coast 格越多（HigherRuggedness_ProducesMoreCoastCells 测试约束）。
        double detailWeight = DetailMaxWeight * p.Spec.Ruggedness;
        if (detailWeight > 0.0)
        {
            ulong seedDetail = seed + 3UL;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double d = ValueNoise.Fbm(
                        x * highFreq, y * highFreq, seedDetail,
                        octaves: DetailOctaves, lacunarity: 2.0, gain: 0.5);
                    mask[y * width + x] += d * detailWeight;
                }
            }
        }

        // ─── ④ 分位数阈值 ───
        // 把 mask 升序排，取第 (int)Math.Round((1 - landRatio) * count) 位作 τ；
        // mask > τ 为 land。
        // 实际写法：找到 index = count - landTarget（landTarget = round(landRatio * count)），
        // 把 sortedMask[index - 1] 当 τ；这样刚好有 landTarget 个 mask > τ（无 tie 时）。
        // brief 容许 ±1（防四舍五入落在 tie 边界上）。
        var sorted = (double[])mask.Clone();
        Array.Sort(sorted);
        int landTarget = (int)Math.Round(p.Spec.LandRatio * count);
        // 钳制 landTarget 到合法范围 [0, count]
        if (landTarget < 0) landTarget = 0;
        if (landTarget > count) landTarget = count;
        // sorted 升序：sorted[0] 最小，sorted[count-1] 最大。
        // 我们要 landTarget 个最大者 = sorted[count-landTarget .. count-1]。
        // τ = sorted[count - landTarget - 1]（"最大者之前那一个"），mask > τ = land。
        int thresholdIdx = count - landTarget - 1;
        if (thresholdIdx < 0) thresholdIdx = 0;          // 全 land
        if (thresholdIdx >= count) thresholdIdx = count - 1; // 全 water
        double threshold = sorted[thresholdIdx];

        var isLand = new bool[count];
        for (int i = 0; i < count; i++)
        {
            isLand[i] = mask[i] > threshold;
        }

        // 构造 read 闭包：land → Plain，水 → Water（默认）。
        Func<int, int, TerrainType> read = (x, y) =>
            isLand[y * width + x] ? TerrainType.Plain : TerrainType.Water;

        // ─── ⑤ 调 CoastlineRefiner ───
        // brief §4.1：仅生成路径开启 Morphology。
        // Erosion=0 时形态学内部不执行（spec §5.2 / T4 实现）；即便 Erosion=0，Morphology=true
        // 仍是「生成路径」的语义，refiner 内部会跳过。
        var outcome = CoastlineRefiner.Refine(
            read,
            width, height,
            rangeMin: new GridCoord(0, 0),
            rangeMax: new GridCoord(width - 1, height - 1),
            defaultType: TerrainType.Water,
            options: new RefineOptions(
                MinIslandCells: p.Spec.MinIslandCells,
                MinLakeCells: p.Spec.MinLakeCells,
                Erosion: p.Spec.Erosion,
                Morphology: true));

        // ─── 收集最终 cells ───
        // 起点 = 原 isLand 中所有 land（标 Plain）；叠加 refiner 的 Changed：
        //   - 值 = defaultType (Water) → 删除（该 cell 不再是 land）
        //   - 值 = Plain 或 Coast      → 覆盖（来自 fill 或 relabel）
        // 终态：只含 land cells，类型 = Plain/Coast，无 Water。
        var finalLand = new Dictionary<GridCoord, TerrainType>(capacity: count);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (isLand[y * width + x])
                {
                    finalLand[new GridCoord(x, y)] = TerrainType.Plain;
                }
            }
        }
        foreach (var kvp in outcome.Changed)
        {
            if (kvp.Value == TerrainType.Water)
            {
                // refiner 标记删除：移除该格（无论是原 land 被去碎岛，还是原 water 标记保留——后者不会进来）
                finalLand.Remove(kvp.Key);
            }
            else
            {
                // Plain（填碎海）或 Coast（重标）：覆盖。
                finalLand[kvp.Key] = kvp.Value;
            }
        }

        // 按 (Y, X) 升序排（与 EditorContext.SetTerrainCells 一致）。
        var ordered = finalLand
            .OrderBy(kvp => kvp.Key.Y)
            .ThenBy(kvp => kvp.Key.X)
            .Select(kvp => new TerrainCellEntry(kvp.Key, kvp.Value))
            .ToList();

        return new GeneratedWorld(ordered, TerrainType.Water, outcome.Stats);
    }

    // ──── 内部辅助 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 对已计算好的 <paramref name="mask"/> 数组做双线性采样（连续坐标 → 离散格）。
    /// 越界用 clamp-to-edge（与 brief 一致：mask 之外的视为「与边同值」）。
    /// </summary>
    private static double SampleMask(double[] mask, int width, int height, double fx, double fy)
    {
        // clamp-to-edge：超出 [0, width) × [0, height) 的坐标钳到边界格。
        if (fx < 0) fx = 0;
        if (fy < 0) fy = 0;
        if (fx > width - 1) fx = width - 1;
        if (fy > height - 1) fy = height - 1;

        int x0 = (int)Math.Floor(fx);
        int y0 = (int)Math.Floor(fy);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        if (x1 >= width) x1 = width - 1;
        if (y1 >= height) y1 = height - 1;

        double tx = fx - x0;
        double ty = fy - y0;

        double v00 = mask[y0 * width + x0];
        double v10 = mask[y0 * width + x1];
        double v01 = mask[y1 * width + x0];
        double v11 = mask[y1 * width + x1];

        double a = v00 + (v10 - v00) * tx;
        double b = v01 + (v11 - v01) * tx;
        return a + (b - a) * ty;
    }
}

/// <summary>
/// 世界生成器的输出。
/// <para>约定：<see cref="Cells"/> 仅含陆地（plain + coast），水域**不写入**（隐含默认）。
/// 元素按 <c>(Y, X)</c> 升序排，与 <c>EditorContext.SetTerrainCells</c> 一致。</para>
/// </summary>
/// <param name="Cells">陆地 cells（plain + coast）按 (Y, X) 升序。</param>
/// <param name="DefaultType">隐含默认地形，硬编码为 <see cref="TerrainType.Water"/>。</param>
/// <param name="Stats">细化 pass 的统计（透传自 <see cref="CoastlineRefiner.Refine"/>）。</param>
public sealed record GeneratedWorld(
    IReadOnlyList<TerrainCellEntry> Cells,
    TerrainType DefaultType,
    RefineStats Stats);
