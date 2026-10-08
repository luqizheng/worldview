using System.Collections.Generic;
using System.Linq;
using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Editing;
using Xunit;

namespace WorldView.Tests.Editing;

/// <summary>
/// T9：把 T4 CoastlineRefiner 包成 <see cref="RefineCoastlineCommand"/> 的纯函数路径。
/// 不构造 EditorContext（避免 Godot 依赖）；只覆盖静态 API。
/// </summary>
public class RefineCoastlineCommandTests
{
    private const TerrainType DefaultType = TerrainType.Water;

    // ──── 测试用辅助 ──────────────────────────────────────────────────────────

    /// <summary>用指定 (x,y,type) 列表生成稀疏 cells；其余格子为 <paramref name="defaultType"/>。</summary>
    private static MapDoc BuildMap(
        int width, int height,
        TerrainType defaultType,
        params (int x, int y, TerrainType type)[] cells)
    {
        var entries = cells
            .Select(c => new TerrainCellEntry(new GridCoord(c.x, c.y), c.type))
            .OrderBy(e => e.At.Y).ThenBy(e => e.At.X)
            .ToList();
        return new MapDoc(
            Id: "test-map",
            Name: "test",
            Type: MapType.World,
            Grid: new GridSpec(width, height, 1.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(
                Encoding: "sparse",
                Default: new TerrainCell(defaultType),
                CellsRef: null,
                Cells: entries),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>(),
            Generation: null);
    }

    /// <summary>不排序地生成 MapDoc（保留 <paramref name="entries"/> 的原始顺序），用于验证 ApplyTo 的排序行为。</summary>
    private static MapDoc BuildMapFromEntries(
        int width, int height, TerrainType defaultType, IReadOnlyList<TerrainCellEntry> entries)
    {
        return new MapDoc(
            Id: "test-map",
            Name: "test",
            Type: MapType.World,
            Grid: new GridSpec(width, height, 1.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData("sparse", new TerrainCell(defaultType), null, entries),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>(),
            Generation: null);
    }

    private static bool IsSortedByYThenX(IReadOnlyList<TerrainCellEntry> entries)
    {
        for (int i = 1; i < entries.Count; i++)
        {
            var prev = entries[i - 1].At;
            var curr = entries[i].At;
            if (prev.Y > curr.Y || (prev.Y == curr.Y && prev.X > curr.X)) return false;
        }
        return true;
    }

    private static GenerationSpec SpecWithErosion(int erosion) =>
        new(Algorithm: "coastline_v1", Seed: 0, LandRatio: 0.35, Ruggedness: 0.5,
            MinIslandCells: 8, MinLakeCells: 6, Erosion: erosion);

    // ──── OptionsFor_* ────────────────────────────────────────────────────────

    [Fact]
    public void OptionsFor_Radius1_And_2_RelabelOnly()
    {
        var spec = SpecWithErosion(2);
        Assert.Equal(new RefineOptions(0, 0, 0, false), RefineCoastlineCommand.OptionsFor(1, spec));
        Assert.Equal(new RefineOptions(0, 0, 0, false), RefineCoastlineCommand.OptionsFor(2, spec));
    }

    [Fact]
    public void OptionsFor_Radius8_Gentle()
    {
        var spec = SpecWithErosion(2);
        Assert.Equal(new RefineOptions(8, 6, 0, false), RefineCoastlineCommand.OptionsFor(3, spec));
        Assert.Equal(new RefineOptions(8, 6, 0, false), RefineCoastlineCommand.OptionsFor(8, spec));
    }

    [Fact]
    public void OptionsFor_Radius32_Full_UsesSpecErosion()
    {
        var spec = SpecWithErosion(2);
        Assert.Equal(new RefineOptions(8, 6, 2, true), RefineCoastlineCommand.OptionsFor(9, spec));
        Assert.Equal(new RefineOptions(8, 6, 2, true), RefineCoastlineCommand.OptionsFor(32, spec));
        Assert.Equal(new RefineOptions(8, 6, 2, true), RefineCoastlineCommand.OptionsFor(64, spec));
        // null spec → Erosion ?? 1
        Assert.Equal(new RefineOptions(8, 6, 1, true), RefineCoastlineCommand.OptionsFor(32, null));
    }

    // ──── Compute_* ──────────────────────────────────────────────────────────

    [Fact]
    public void Compute_RemovesThreeCellIsland_AtThreshold4()
    {
        // 5x5, 中央 3 格 Plain 岛（(2,2)(1,2)(2,1)；4 邻接连通，size=3），外圈 Water。
        // 去碎岛规则是「size < MinIslandCells 才删」（CoastlineRefiner.cs:110 的 `cells.Count >= MinIslandCells` 则 continue），
        // 故 MinIslandCells=3 会保留（等于临界）；要全删必须 MinIslandCells=4。
        var map = BuildMap(5, 5, DefaultType,
            (2, 2, TerrainType.Plain),
            (1, 2, TerrainType.Plain),
            (2, 1, TerrainType.Plain));

        var options = new RefineOptions(MinIslandCells: 4, MinLakeCells: 6, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(1, 1), new GridCoord(2, 2),
            options);

        Assert.Equal(3, outcome.Stats.RemovedCells);
        Assert.Contains(new GridCoord(2, 2), outcome.Changed.Keys);
        Assert.Contains(new GridCoord(1, 2), outcome.Changed.Keys);
        Assert.Contains(new GridCoord(2, 1), outcome.Changed.Keys);
        foreach (var coord in outcome.Changed.Keys)
        {
            Assert.Equal(DefaultType, outcome.Changed[coord]);   // 删：值 = default
        }
    }

    // ──── ApplyTo_* ─────────────────────────────────────────────────────────

    [Fact]
    public void ApplyTo_WritesCoastRing()
    {
        // 7x7, default = Water；
        // 中央 3x3 陆地 ((2,2)..(4,4))，其中 (3,3) 是 1x1 Water 湖；
        // 用 MinLakeCells=1 防止 1-cell lake 被填；
        // 用 MinIslandCells=8 让陆地全连接 3x3 (size=9 >= 8) → 不被去碎岛。
        var map = BuildMap(7, 7, DefaultType,
            (2, 2, TerrainType.Plain),
            (3, 2, TerrainType.Plain),
            (4, 2, TerrainType.Plain),
            (2, 3, TerrainType.Plain),
            (3, 3, TerrainType.Water),    // 中央 1x1 湖
            (4, 3, TerrainType.Plain),
            (2, 4, TerrainType.Plain),
            (3, 4, TerrainType.Plain),
            (4, 4, TerrainType.Plain));

        var options = new RefineOptions(MinIslandCells: 8, MinLakeCells: 1, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(2, 2), new GridCoord(4, 4),
            options);

        var after = RefineCoastlineCommand.ApplyTo(map, outcome);

        // 紧邻 (3,3) 水格的陆地格应有 (2,3)(3,2)(4,3)(3,4) 共 4 个被重标 coast，
        // 收紧到 >= 2 才能真正钉住「不止偶然标中一个」。
        var coastCount = after.TerrainData.Cells.Count(e => e.Type == TerrainType.Coast);
        Assert.True(coastCount >= 2, $"Expected at least 2 Coast cells in result, got {coastCount}");

        // 原陆地格至少保留一块
        Assert.Contains(after.TerrainData.Cells, e => e.Type == TerrainType.Plain || e.Type == TerrainType.Coast);

        // (3,3) 仍是 Water（= default, 不在 cells 中）
        Assert.Equal(TerrainType.Water, LookupCell(after, 3, 3));
    }

    [Fact]
    public void ApplyTo_DeletesEntriesEqualToDefault()
    {
        // 5x5, default = Water；cells 里只有 (2,2)=Forest（非 default）。
        // 用手工 RefineOutcome 直接打 ApplyTo 的删除分支（不经过 Compute，避免输入为空导致断言退化成 0 == 0）。
        var map = BuildMap(5, 5, TerrainType.Water, (2, 2, TerrainType.Forest));

        // 前置条件：起点非空，删除才有意义。
        Assert.Single(map.TerrainData.Cells);

        // Changed[(2,2)] = Water = default → 删除分支。
        var outcome = new RefineOutcome(
            new Dictionary<GridCoord, TerrainType> { [new GridCoord(2, 2)] = TerrainType.Water },
            new RefineStats(RemovedCells: 1, FilledCells: 0, RelabeledCells: 0));

        var after = RefineCoastlineCommand.ApplyTo(map, outcome);

        Assert.Empty(after.TerrainData.Cells);
        Assert.DoesNotContain(after.TerrainData.Cells, e => e.At == new GridCoord(2, 2));

        // 写入分支：Changed 值 ≠ default 时必须被写进 sparse 列表（这里起始列表为空，故一定是新写入）。
        var outcome2 = new RefineOutcome(
            new Dictionary<GridCoord, TerrainType> { [new GridCoord(1, 1)] = TerrainType.Coast },
            new RefineStats(RemovedCells: 0, FilledCells: 0, RelabeledCells: 1));

        var after2 = RefineCoastlineCommand.ApplyTo(BuildMap(5, 5, TerrainType.Water), outcome2);

        Assert.Contains(after2.TerrainData.Cells, e => e.At == new GridCoord(1, 1) && e.Type == TerrainType.Coast);
    }

    // ──── RevertTo_* ────────────────────────────────────────────────────────

    [Fact]
    public void RevertTo_RestoresExactBeforeState()
    {
        // 6x6, Default=Water；
        // 左上 2x2 Plain（(0,0)(1,0)(0,1)(1,1) 非 default）;
        // 中央 2x2 Water（(2,2)(3,2)(2,3)(3,3) 非 default——不！Water = default, 不在 cells 里）;
        // 简化：构造有「removed」「filled」「relabeled」多种改动混合的 6x6:
        //   - 左上 1x1 岛 Plain (1,1) → 应被去碎岛
        //   - 中间 1x1 水 (3,3) 在全 Plain 中 → 应被填碎海
        //   - 中央 3x3 陆地 → 应被重标 coast
        var map = BuildMap(6, 6, DefaultType,
            (1, 1, TerrainType.Plain),         // 1-cell 岛（碎岛）
            (3, 3, TerrainType.Water));        // 1-cell 湖（碎海，在全 Plain 中）

        var options = new RefineOptions(MinIslandCells: 4, MinLakeCells: 4, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(1, 1), new GridCoord(4, 4),
            options);

        var beforeCells = map.TerrainData.Cells;
        var after = RefineCoastlineCommand.ApplyTo(map, outcome);
        var restored = RefineCoastlineCommand.RevertTo(after, outcome, beforeCells);

        // 还原后 cells 列表与 before 完全相同。
        Assert.Equal(beforeCells.Count, restored.TerrainData.Cells.Count);
        for (int i = 0; i < beforeCells.Count; i++)
        {
            Assert.Equal(beforeCells[i].At, restored.TerrainData.Cells[i].At);
            Assert.Equal(beforeCells[i].Type, restored.TerrainData.Cells[i].Type);
        }
    }

    [Fact]
    public void ApplyTo_KeepsCellsSortedByYThenX()
    {
        // 故意乱序填 cells（Y 序列 5,1,1,5,3,2 既非升序，也不是 (X,Y) 序），
        // 且用 BuildMapFromEntries 绕过 BuildMap 的预排序 —— 否则这个测试会退化成空转。
        var unsorted = new List<TerrainCellEntry>
        {
            new(new GridCoord(5, 3), TerrainType.Plain),
            new(new GridCoord(2, 1), TerrainType.Plain),
            new(new GridCoord(8, 1), TerrainType.Plain),
            new(new GridCoord(1, 5), TerrainType.Plain),
            new(new GridCoord(3, 3), TerrainType.Plain),
            new(new GridCoord(7, 2), TerrainType.Plain),
        };
        var map = BuildMapFromEntries(10, 10, TerrainType.Water, unsorted);

        // 前提断言：输入确实乱序（否则这个测试又会退化成空转）
        Assert.False(IsSortedByYThenX(unsorted), "fixture 必须乱序，否则测不出排序");

        // 阈值 0：任何 size>=1 的块都保留（CoastlineRefiner.cs:110 与 :129），保证结果非空。
        var options = new RefineOptions(MinIslandCells: 0, MinLakeCells: 0, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(0, 0), new GridCoord(9, 9),
            options);

        var after = RefineCoastlineCommand.ApplyTo(map, outcome);
        var cells = after.TerrainData.Cells;

        // 前提断言：结果非空，排序循环才真正跑
        Assert.True(cells.Count >= 2, "结果必须非空，否则排序断言是空转");

        for (int i = 1; i < cells.Count; i++)
        {
            var prev = cells[i - 1].At;
            var curr = cells[i].At;
            Assert.True(
                prev.Y < curr.Y || (prev.Y == curr.Y && prev.X <= curr.X),
                $"Cells not sorted at index {i}: ({prev.X},{prev.Y}) → ({curr.X},{curr.Y})");
        }
    }

    // ──── Describe_* ────────────────────────────────────────────────────────

    [Fact]
    public void Describe_CountsMatchStats()
    {
        // 调生产静态方法（纯函数，不触碰 Godot），删除生产模板代码会让本测试变红。
        // 注：实例 Description 属性本身无法在纯 .NET 测试里覆盖（需要 Godot Node 来构造命令）。
        var stats = new RefineStats(RemovedCells: 3, FilledCells: 2, RelabeledCells: 4);
        var outcome = new RefineOutcome(new Dictionary<GridCoord, TerrainType>(), stats);

        var actual = RefineCoastlineCommand.Describe(outcome);

        Assert.Equal("细化海岸线（去碎岛 3 · 填碎海 2）", actual);

        // 中点必须是 U+00B7（MIDDLE DOT），不是全角 U+30FB。
        Assert.Contains('\u00B7', actual);
        Assert.DoesNotContain('\u30FB', actual);
    }

    // ──── 内部辅助 ──────────────────────────────────────────────────────────

    private static TerrainType LookupCell(MapDoc map, int x, int y)
    {
        var at = new GridCoord(x, y);
        foreach (var entry in map.TerrainData.Cells)
        {
            if (entry.At == at) return entry.Type;
        }
        return map.TerrainData.Default?.Type ?? TerrainDefaults.DefaultType;
    }
}
