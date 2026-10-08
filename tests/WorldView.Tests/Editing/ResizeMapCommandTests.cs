using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Editing;
using Xunit;

namespace WorldView.Tests.Editing;

/// <summary>
/// T10：把 plan L344-365 Task 10 的 <see cref="ResizeMapCommand"/> 包到裸测里。
///
/// 不构造 Godot 节点（避免 Godot 依赖）；只覆盖静态 API + Description 模板格式。
/// <c>Apply</c>/<c>Revert</c> 走 <c>ReplaceMap</c> 的写回路径，由 T13 调用方负责触发。
/// </summary>
public class ResizeMapCommandTests
{
    // ──── 测试用辅助 ──────────────────────────────────────────────────────────

    /// <summary>构造一张 <paramref name="width"/>×<paramref name="height"/> 的稀疏地图；
    /// cells / buildings 留空列表，由调用方塞入。</summary>
    private static MapDoc BuildMap(
        int width, int height,
        IReadOnlyList<TerrainCellEntry>? cells = null,
        IReadOnlyList<Building>? buildings = null,
        TerrainData? terrainData = null,
        GenerationSpec? generation = null)
    {
        var td = terrainData ?? new TerrainData(
            Encoding: "sparse",
            Default: new TerrainCell(TerrainType.Plain),
            CellsRef: null,
            Cells: cells ?? (IReadOnlyList<TerrainCellEntry>)System.Array.Empty<TerrainCellEntry>());
        return new MapDoc(
            Id: "test-map",
            Name: "test",
            Type: MapType.World,
            Grid: new GridSpec(width, height, 1.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: td,
            Layers: System.Array.Empty<LayerRef>(),
            CityRefs: System.Array.Empty<CityRef>(),
            Buildings: buildings ?? System.Array.Empty<Building>(),
            Generation: generation);
    }

    private static TerrainCellEntry Cell(int x, int y, TerrainType t) =>
        new(new GridCoord(x, y), t);

    private static Building BuildAt(int x, int y) =>
        new($"b-{x}-{y}", BuildingType.House, new GridCoord(x, y), "test-map");

    // ──── 1. CropCells ────────────────────────────────────────────────────────

    [Fact]
    public void CropCells_DropsOutOfBounds_KeepsOrder()
    {
        // 输入按 (y 升序, x 升序) 排列：约定 (T9 确认).
        // 裁剪谓词是严格小于（x < width && y < height），故 width-1 / height-1 边界内保留。
        var input = new List<TerrainCellEntry>
        {
            Cell(0, 0, TerrainType.Plain),    // keep
            Cell(3, 0, TerrainType.Plain),    // keep (x == width-1，边界内)
            Cell(0, 1, TerrainType.Plain),    // keep
            Cell(5, 1, TerrainType.Plain),    // drop (x=5 >= 4)
            Cell(2, 2, TerrainType.Plain),    // keep (y == height-1，边界内)
            Cell(1, 3, TerrainType.Plain),    // drop (y=3 >= 3)
        };

        var result = ResizeMapCommand.CropCells(input, 4, 3).ToList();

        Assert.Equal(4, result.Count);
        Assert.Equal(new GridCoord(0, 0), result[0].At);
        Assert.Equal(new GridCoord(3, 0), result[1].At);
        Assert.Equal(new GridCoord(0, 1), result[2].At);
        Assert.Equal(new GridCoord(2, 2), result[3].At);

        // 顺序应保留：(y 升序, x 升序).
        for (int i = 0; i < result.Count; i++)
        {
            Assert.True(result[i].At.X < 4, $"x out of range at {i}");
            Assert.True(result[i].At.Y < 3, $"y out of range at {i}");
        }
    }

    // ──── 2. CropBuildings ───────────────────────────────────────────────────

    [Fact]
    public void CropBuildings_DropsOutOfBounds()
    {
        var input = new List<Building>
        {
            BuildAt(2, 2),    // keep (w=5,h=4)
            BuildAt(6, 2),    // drop (x>=5)
            BuildAt(1, 4),    // drop (y>=4)
            BuildAt(0, 0),    // keep
        };

        var result = ResizeMapCommand.CropBuildings(input, 5, 4).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(new GridCoord(2, 2), result[0].At);
        Assert.Equal(new GridCoord(0, 0), result[1].At);
    }

    // ──── 3. Resize 放大 ────────────────────────────────────────────────────

    [Fact]
    public void Resize_GrowsGrid_KeepsAllCells()
    {
        // 8x6 → 12x10，所有原格都落在新范围内.
        var cells = new List<TerrainCellEntry>
        {
            Cell(7, 0, TerrainType.Plain),
            Cell(0, 5, TerrainType.Plain),
            Cell(3, 3, TerrainType.Plain),
        };
        var buildings = new List<Building>
        {
            BuildAt(7, 5),
            BuildAt(0, 0),
        };
        var map = BuildMap(8, 6, cells, buildings);
        var newGrid = new GridSpec(12, 10, 1.0);

        var resized = ResizeMapCommand.Resize(map, newGrid);

        Assert.Equal(newGrid, resized.Grid);
        Assert.Equal(3, resized.TerrainData.Cells.Count);
        Assert.Equal(2, resized.Buildings.Count);
        // GridSpec record equality: Width/Height/CellDistance 等
        Assert.Equal(12, resized.Grid.Width);
        Assert.Equal(10, resized.Grid.Height);
        // 顺序保持
        Assert.Equal(new GridCoord(7, 0), resized.TerrainData.Cells[0].At);
        Assert.Equal(new GridCoord(0, 5), resized.TerrainData.Cells[1].At);
        Assert.Equal(new GridCoord(3, 3), resized.TerrainData.Cells[2].At);
    }

    // ──── 4. Resize 缩小 ────────────────────────────────────────────────────

    [Fact]
    public void Resize_ShrinksGrid_CropsCellsAndBuildings()
    {
        // 10x10 → 4x4：越界格 / 建筑数 = 越界坐标计数.
        var cells = new List<TerrainCellEntry>
        {
            Cell(0, 0, TerrainType.Plain),   // keep
            Cell(3, 3, TerrainType.Plain),   // keep
            Cell(8, 0, TerrainType.Plain),   // drop
            Cell(0, 9, TerrainType.Plain),   // drop
            Cell(5, 5, TerrainType.Plain),   // drop
        };
        var buildings = new List<Building>
        {
            BuildAt(2, 2),    // keep
            BuildAt(7, 1),    // drop
            BuildAt(0, 0),    // keep
            BuildAt(4, 8),    // drop
        };
        var map = BuildMap(10, 10, cells, buildings);
        var newGrid = new GridSpec(4, 4, 1.0);

        var resized = ResizeMapCommand.Resize(map, newGrid);

        Assert.Equal(newGrid, resized.Grid);
        Assert.Equal(2, resized.TerrainData.Cells.Count);
        Assert.Equal(new GridCoord(0, 0), resized.TerrainData.Cells[0].At);
        Assert.Equal(new GridCoord(3, 3), resized.TerrainData.Cells[1].At);

        Assert.Equal(2, resized.Buildings.Count);
        Assert.Equal(new GridCoord(2, 2), resized.Buildings[0].At);
        Assert.Equal(new GridCoord(0, 0), resized.Buildings[1].At);
    }

    // ──── 5. Resize 保留 Encoding / Default / Generation ────────────────────

    [Fact]
    public void Resize_PreservesEncodingDefaultAndGeneration()
    {
        var defaultCell = new TerrainCell(TerrainType.Water, MoveCost: 2, Buildable: false);
        var terrain = new TerrainData(
            Encoding: "chunked",
            Default: defaultCell,
            CellsRef: "terrain/height.bin",
            Cells: new List<TerrainCellEntry> { Cell(0, 0, TerrainType.Plain) });
        var gen = new GenerationSpec(
            Algorithm: "coastline_v1",
            Seed: 42,
            LandRatio: 0.5,
            Ruggedness: 0.7,
            MinIslandCells: 16,
            MinLakeCells: 12,
            Erosion: 2);
        var map = BuildMap(100, 100, terrainData: terrain, generation: gen);
        var newGrid = new GridSpec(50, 50, 1.0);

        var resized = ResizeMapCommand.Resize(map, newGrid);

        Assert.Equal("chunked", resized.TerrainData.Encoding);
        Assert.Equal("terrain/height.bin", resized.TerrainData.CellsRef);
        Assert.Equal(defaultCell, resized.TerrainData.Default);

        Assert.NotNull(resized.Generation);
        Assert.Equal("coastline_v1", resized.Generation!.Algorithm);
        Assert.Equal(42L, resized.Generation.Seed);
        Assert.Equal(0.5, resized.Generation.LandRatio);
        Assert.Equal(0.7, resized.Generation.Ruggedness);
        Assert.Equal(16, resized.Generation.MinIslandCells);
        Assert.Equal(12, resized.Generation.MinLakeCells);
        Assert.Equal(2, resized.Generation.Erosion);

        // 仍然有 1 个 cell 保留（(0,0) 在 50x50 内）
        Assert.Single(resized.TerrainData.Cells);
    }

    // ──── 6. DescribeLoss 数字 ──────────────────────────────────────────────

    [Fact]
    public void DescribeLoss_MatchesCounts()
    {
        // 制造 1,234 个越界地形格 + 2 个建筑（其中 1 个越界）.
        // 1,234 个越界坐标用一个简单的循环塞入 cells（保持 (y,x) 升序约定）.
        var cells = new List<TerrainCellEntry>(1300);
        // 在范围内的 1 个 + 越界 1,234 个
        cells.Add(Cell(0, 0, TerrainType.Plain));   // in-bounds
        for (int i = 0; i < 1234; i++)
        {
            // (x=1000+i%10, y=1000+i/10) → 全越界（> 10x10）
            cells.Add(Cell(1000 + (i % 10), 1000 + (i / 10), TerrainType.Plain));
        }
        var buildings = new List<Building>
        {
            BuildAt(2, 2),       // keep
            BuildAt(50, 50),     // drop
        };
        var map = BuildMap(10, 10, cells, buildings);
        var newGrid = new GridSpec(10, 10, 1.0);   // 等大也算「无丢」? 不 — 有越界格，所以缩小到 10x10 仍是有丢失报告
        // 把 newGrid 改成更小（比如 5x5）保证所有 1,234 都算「丢」.
        newGrid = new GridSpec(5, 5, 1.0);

        var desc = ResizeMapCommand.DescribeLoss(map, newGrid);

        // 数字必须含 N0 格式的 "1,234"（自造数）和建筑丢 1。
        Assert.Contains("1,234", desc);
        Assert.Contains("1 个建筑", desc);
        // 必须提到「缩小」字样
        Assert.Contains("缩小", desc);
        // 注：DescribeLoss 模板（plan L359）没有中点 ·——那是 RefineCoastlineCommand.Description（T9）的格式。
        // 校验 zh-CN 数字格式
        var sample = 1234.ToString("N0", CultureInfo.GetCultureInfo("zh-CN"));
        Assert.Equal("1,234", sample);
    }

    // ──── 7. DescribeLoss 放大 = 无丢 ───────────────────────────────────────

    [Fact]
    public void DescribeLoss_Growing_ReportsNoLoss()
    {
        // 任意 cells + buildings 都比"无丢"分支配大.
        var cells = new List<TerrainCellEntry> { Cell(2, 2, TerrainType.Plain) };
        var buildings = new List<Building> { BuildAt(1, 1) };
        var map = BuildMap(8, 8, cells, buildings);
        var newGrid = new GridSpec(16, 16, 1.0);   // 放大

        var desc = ResizeMapCommand.DescribeLoss(map, newGrid);

        Assert.Contains("扩大到 16 × 16 格不会丢弃任何数据。", desc);
        // 无丢分支的措辞是「不会丢弃」——正向断言该短语，同时确认没有「将丢弃」的丢失措辞
        Assert.Contains("不会丢弃", desc);
        Assert.DoesNotContain("将丢弃", desc);
    }

    // ──── 8. Description 模板 ────────────────────────────────────────────────

    [Fact]
    public void Description_FormatsOldToNew()
    {
        // Description 属性需要 Godot Node，纯 .NET 测试无法构造；
        // 这里覆盖它委托的纯静态格式化函数（生产代码路径）。
        var desc = ResizeMapCommand.Describe(new GridSpec(8, 8, 1.0), new GridSpec(96, 96, 1.0));
        Assert.Equal("调整地图尺寸：8×8 → 96×96 格", desc);
    }
}
