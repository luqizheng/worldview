using WorldView.Domain;
using WorldView.Domain.Generation;
using Xunit;

namespace WorldView.Tests.Generation;

/// <summary>
/// 世界生成器测试（task-5-brief + spec 2 §4）。
///
/// 流水线（顺序固定）：① 大陆遮罩 fBm → ② 域扭曲 → ③ 细节叠加 →
/// ④ 分位数阈值 → ⑤ 调 <see cref="CoastlineRefiner.Refine"/>。
///
/// 全部 <c>[Fact]</c> 名严格照抄 task-5-brief §6；新增的私有 const
/// <c>GoldenLandCount</c> 是漂移检测器（防误用 <c>System.Random</c> 导致的世界漂移），
/// 任何改动都必须同时改 <c>task-5-report.md §8.2</c> 的注释。
/// </summary>
public class WorldGeneratorTests
{
    // ──── 测试用辅助 ──────────────────────────────────────────────────────────

    private const int DefaultSeed = 12345;
    private const double DefaultLandRatio = 0.35;
    private const double DefaultRuggedness = 0.5;
    private const int DefaultErosion = 1;

    private static GenerationParams MakeParams(
        int seed = DefaultSeed,
        double landRatio = DefaultLandRatio,
        double ruggedness = DefaultRuggedness,
        int minIsland = 0,
        int minLake = 0,
        int erosion = DefaultErosion,
        int width = 128,
        int height = 128) => new(
        Spec: new GenerationSpec(
            Algorithm: "coastline_v1",
            Seed: seed,
            LandRatio: landRatio,
            Ruggedness: ruggedness,
            MinIslandCells: minIsland,
            MinLakeCells: minLake,
            Erosion: erosion),
        Width: width,
        Height: height,
        CellDistance: 1.0);

    // ──── 12 brief-required [Fact]s（顺序与 brief §6 对齐） ───────────────────

    /// <summary>
    /// 同一 <paramref name="params"/> 跑两次，<c>Cells</c> 序列**逐元素相等**
    ///（包括顺序、坐标、Type）—— 验证 spec §4.2「位级稳定」红线。
    /// </summary>
    [Fact]
    public void SameSeed_ProducesIdenticalCells()
    {
        var p = MakeParams();
        var a = WorldGenerator.Generate(p);
        var b = WorldGenerator.Generate(p);

        Assert.Equal(a.Cells.Count, b.Cells.Count);
        for (int i = 0; i < a.Cells.Count; i++)
        {
            Assert.Equal(a.Cells[i].At.X, b.Cells[i].At.X);
            Assert.Equal(a.Cells[i].At.Y, b.Cells[i].At.Y);
            Assert.Equal(a.Cells[i].Type, b.Cells[i].Type);
        }
    }

    /// <summary>
    /// 仅 <c>Spec.Seed</c> 不同的两次 <paramref name="params"/> →
    /// 至少 100 个 cell 的 (x,y,type) 三元组**不全相同**。
    /// </summary>
    [Fact]
    public void DifferentSeed_ProducesDifferentCells()
    {
        var a = WorldGenerator.Generate(MakeParams(seed: 1));
        var b = WorldGenerator.Generate(MakeParams(seed: 2));

        int diff = 0;
        var set = new HashSet<(int x, int y, TerrainType t)>(a.Cells.Count);
        foreach (var c in a.Cells) set.Add((c.At.X, c.At.Y, c.Type));

        foreach (var c in b.Cells)
        {
            if (!set.Contains((c.At.X, c.At.Y, c.Type))) diff++;
        }

        Assert.True(diff >= 100, $"期望至少 100 个 (x,y,type) 不相同，实测 {diff}。");
    }

    /// <summary>
    /// <c>land_ratio = 0.35</c> → 实际陆地占比 ∈ [0.335, 0.365]。
    /// spec §4.1 硬指标 ±1.5%。
    /// </summary>
    [Fact]
    public void LandRatio_Within1_5Percent_OfRequested()
    {
        var p = MakeParams(landRatio: 0.35, width: 256, height: 256);
        var r = WorldGenerator.Generate(p);
        double actual = (double)r.Cells.Count / (p.Width * p.Height);

        Assert.InRange(actual, 0.335, 0.365);
    }

    /// <summary>
    /// 极端 <c>land_ratio = 0.05</c> 与 <c>0.80</c> 都不抛、都有陆地，0.80 时仍有水。
    /// </summary>
    [Fact]
    public void LandRatio_Extremes_005_And_080_AreValid()
    {
        var low = WorldGenerator.Generate(MakeParams(landRatio: 0.05, width: 128, height: 128));
        Assert.NotEmpty(low.Cells);

        var high = WorldGenerator.Generate(MakeParams(landRatio: 0.80, width: 128, height: 128));
        Assert.NotEmpty(high.Cells);
        // 0.80 时仍有水（cells.Count < width*height）—— 默认 water 不写入 cells
        Assert.True(high.Cells.Count < 128 * 128,
            $"land_ratio=0.80 时应仍留 ~20% 水，实测陆地格 {high.Cells.Count}/{128 * 128}");
    }

    /// <summary>所有 cell 的 <c>X ∈ [0, width)</c>、<c>Y ∈ [0, height)</c>。</summary>
    [Fact]
    public void AllCoordinates_InBounds()
    {
        var p = MakeParams(width: 64, height: 48);
        var r = WorldGenerator.Generate(p);
        foreach (var c in r.Cells)
        {
            Assert.InRange(c.At.X, 0, p.Width - 1);
            Assert.InRange(c.At.Y, 0, p.Height - 1);
        }
    }

    /// <summary><c>Cells</c> 中无重复 <c>(X, Y)</c>。</summary>
    [Fact]
    public void NoDuplicateCoordinates()
    {
        var r = WorldGenerator.Generate(MakeParams(width: 96, height: 96));
        var seen = new HashSet<(int, int)>(r.Cells.Count);
        foreach (var c in r.Cells)
        {
            Assert.True(seen.Add((c.At.X, c.At.Y)),
                $"重复坐标 ({c.At.X}, {c.At.Y})。");
        }
    }

    /// <summary>
    /// <c>Cells</c> 严格按 <c>Y</c> 升序、<c>Y</c> 相同时按 <c>X</c> 升序。
    /// 与 <c>EditorContext.SetTerrainCells</c> 一致。
    /// </summary>
    [Fact]
    public void Cells_SortedByYThenX()
    {
        var r = WorldGenerator.Generate(MakeParams(width: 96, height: 96));
        for (int i = 1; i < r.Cells.Count; i++)
        {
            var prev = r.Cells[i - 1].At;
            var cur = r.Cells[i].At;
            Assert.True(prev.Y < cur.Y || (prev.Y == cur.Y && prev.X < cur.X),
                $"Cells 在 i={i} 处不升序: ({prev.X},{prev.Y}) → ({cur.X},{cur.Y})");
        }
    }

    /// <summary><c>result.DefaultType == TerrainType.Water</c>。</summary>
    [Fact]
    public void DefaultType_Is_Water()
    {
        var r = WorldGenerator.Generate(MakeParams());
        Assert.Equal(TerrainType.Water, r.DefaultType);
    }

    /// <summary><c>Cells</c> 中**没有** <c>TerrainType.Water</c>（水隐含默认）。</summary>
    [Fact]
    public void Ocean_IsNotWritten()
    {
        var r = WorldGenerator.Generate(MakeParams());
        Assert.DoesNotContain(r.Cells, c => c.Type == TerrainType.Water);
    }

    /// <summary>
    /// 关掉 refiner 的影响（<c>MinIslandCells=0, MinLakeCells=0, Erosion=0</c>，默认
    /// <c>Morphology</c> = false via spec，且若生成器内部硬编 <c>Morphology: true</c>，
    /// 因 Erosion=0 也不会触发形态学）→ <c>Cells.Count</c> 应等于分位数预期值 ±1。
    /// 证明 ④ 步分位数确实精确命中。
    /// </summary>
    [Fact]
    public void QuantileThreshold_HitsLandCountExactly_WhenRefineDisabled()
    {
        int w = 64, h = 64;
        int expected = (int)Math.Round(0.35 * w * h);  // = 1434
        var p = MakeParams(landRatio: 0.35, minIsland: 0, minLake: 0, erosion: 0,
                           width: w, height: h);
        var r = WorldGenerator.Generate(p);

        Assert.InRange(r.Cells.Count, expected - 1, expected + 1);
    }

    // 冻结值：用于捕捉未来误用 System.Random 导致的世界漂移。
    // 任何算法变更必须更新此常量 + 同步更新 task-5-report.md §8.2。
    // 注：放在方法体里 `private` 修饰无效，故提到类级；仅本 [Fact] 读它。
    // 实测于 T5 第一次 GREEN run（spec 2 §4.2 红线：固定算法+种子+参数必须逐格相同）。
    private const int GoldenLandCount = 5734;

    /// <summary>
    /// 漂移检测器（spec §4.2 红线）：固定 <c>seed=12345, 128×128, land_ratio=0.35,
    /// ruggedness=0.5, erosion=0</c>（关形态学保证纯净），跑一次冻结。
    /// 任何算法变化（哪怕是噪声常数微调）都会让 <c>Cells.Count</c> 变 → 测试失败 = 强制 changelog。
    /// </summary>
    [Fact]
    public void Golden_LandCount_For_PinnedSeed()
    {
        var p = new GenerationParams(
            Spec: new GenerationSpec(
                Algorithm: "coastline_v1",
                Seed: 12345,
                LandRatio: 0.35,
                Ruggedness: 0.5,
                MinIslandCells: 0,
                MinLakeCells: 0,
                Erosion: 0),
            Width: 128,
            Height: 128,
            CellDistance: 1.0);
        var r = WorldGenerator.Generate(p);
        Assert.Equal(GoldenLandCount, r.Cells.Count);
    }

    /// <summary>
    /// <c>land_ratio=0.35, erosion=0</c>（关形态学，纯测域扭曲），
    /// <c>ruggedness=0.0</c> vs <c>ruggedness=1.0</c> → 后者 <c>coast</c> 格数应**严格多于**前者
    ///（域扭曲让海陆交错更频繁 → 临水陆地更多）。
    /// </summary>
    [Fact]
    public void HigherRuggedness_ProducesMoreCoastCells()
    {
        var smooth = WorldGenerator.Generate(MakeParams(ruggedness: 0.0, erosion: 0,
                                                        width: 256, height: 256));
        var rough = WorldGenerator.Generate(MakeParams(ruggedness: 1.0, erosion: 0,
                                                       width: 256, height: 256));

        int coastSmooth = Count(smooth.Cells, TerrainType.Coast);
        int coastRough = Count(rough.Cells, TerrainType.Coast);

        Assert.True(coastRough > coastSmooth,
            $"ruggedness=1.0 的 coast 数 ({coastRough}) 应 > ruggedness=0.0 ({coastSmooth})。");
    }

    private static int Count(System.Collections.Generic.IEnumerable<TerrainCellEntry> cells, TerrainType t)
    {
        int n = 0;
        foreach (var c in cells) if (c.Type == t) n++;
        return n;
    }
}
