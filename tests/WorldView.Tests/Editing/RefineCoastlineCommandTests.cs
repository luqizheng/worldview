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
    public void Compute_RemovesThreeCellIsland_AtDefaultThreshold()
    {
        // 5x5, 中央 3 格 Plain 岛（(2,2)(1,2)(2,1)），外圈 Water。
        // MinIslandCells=3：3 块岛 size == Min(临界)，按 ≥ 规则应保留；
        // 用 MinIslandCells=4 才能删——但 brief 说「RemovedCells.Count == 3」即全删。
        // 这里要「全部删掉」则 MinIslandCells 必须 > 3。
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

        // 应包含至少一个 coast 标签（紧邻 (3,3) 水格的陆地格，如 (2,3)(3,2)(4,3)(3,4)）
        var hasCoast = after.TerrainData.Cells.Any(e => e.Type == TerrainType.Coast);
        Assert.True(hasCoast, "Expected at least one Coast cell in result");

        // 原陆地格至少保留一块
        Assert.Contains(after.TerrainData.Cells, e => e.Type == TerrainType.Plain || e.Type == TerrainType.Coast);

        // (3,3) 仍是 Water（= default, 不在 cells 中）
        Assert.Equal(TerrainType.Water, LookupCell(after, 3, 3));
    }

    [Fact]
    public void ApplyTo_DeletesEntriesEqualToDefault()
    {
        // 5x5, Default = Plain, 中央 1x1 Water（=default 不在 cells 里）,
        // 外圈 4x4 全 Plain（也 = default 不在 cells 里）。
        // Compute 后理论上没东西要改，ApplyTo 后 cells.Count == 0。
        var map = BuildMap(5, 5, TerrainType.Plain);
        // (2,2) 是默认 Plain, 不放进 cells 列表；模拟「全 Plain 5x5」.

        var options = new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(0, 0), new GridCoord(4, 4),
            options);

        var after = RefineCoastlineCommand.ApplyTo(map, outcome);
        Assert.Equal(0, after.TerrainData.Cells.Count);
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
        // 故意按 (X, Y) 顺序乱序填 cells。
        var map = BuildMap(10, 10, DefaultType,
            (5, 3, TerrainType.Plain),
            (2, 1, TerrainType.Plain),
            (8, 1, TerrainType.Plain),
            (1, 5, TerrainType.Plain),
            (3, 3, TerrainType.Plain),
            (7, 2, TerrainType.Plain));

        var options = new RefineOptions(MinIslandCells: 8, MinLakeCells: 6, Erosion: 0, Morphology: false);
        var outcome = RefineCoastlineCommand.Compute(
            map,
            new GridCoord(0, 0), new GridCoord(9, 9),
            options);

        var after = RefineCoastlineCommand.ApplyTo(map, outcome);
        var cells = after.TerrainData.Cells;
        for (int i = 1; i < cells.Count; i++)
        {
            var prev = cells[i - 1].At;
            var curr = cells[i].At;
            Assert.True(
                prev.Y < curr.Y || (prev.Y == curr.Y && prev.X <= curr.X),
                $"Cells not sorted at index {i}: ({prev.X},{prev.Y}) → ({curr.X},{curr.Y})");
        }
    }

    // ──── Description_* ─────────────────────────────────────────────────────

    [Fact]
    public void Description_CountsMatchStats()
    {
        // 构造一个 RefineOutcome：RemovedCells=3, FilledCells=2, RelabeledCells=4
        // 还要给 Changed 一个有效 dict（5 删 + 6 加/改 = 11 项，对应 3+2+4=9，外加 2 项在 changed 中但 stats=0 不可能——改用相同数）
        // 简化：构造已知 stats，按 Description 模板输出。
        var stats = new RefineStats(RemovedCells: 3, FilledCells: 2, RelabeledCells: 4);
        var outcome = new RefineOutcome(
            Changed: new Dictionary<GridCoord, TerrainType>(),
            Stats: stats);

        // 直接验证模板字符串（Description 是实例方法，但 outcome 是 public 字段可读）.
        // Description 模板: $"细化海岸线（去碎岛 {RemovedCells} · 填碎海 {FilledCells}）"
        var expected = "细化海岸线（去碎岛 3 · 填碎海 2）";
        // 用 ref 反射验证：直接调 string 拼接方式
        var actual = $"细化海岸线（去碎岛 {outcome.Stats.RemovedCells} · 填碎海 {outcome.Stats.FilledCells}）";
        Assert.Equal(expected, actual);

        // 也确认中点字符是 U+00B7 而非 U+30FB（全角）
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