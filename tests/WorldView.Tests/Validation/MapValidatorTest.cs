using WorldView.Domain;
using WorldView.Domain.Validation;
using WorldView.Tests.Support;
using Xunit;

namespace WorldView.Tests.Validation;

/// <summary>
/// T5 DoD: MapValidator 关键规则（网格越界 / cells 重复 / ref 缺失 / ref 绝对路径 / ref ..）。
/// </summary>
public class MapValidatorTest
{
    // 一次性创建：ScratchDir 是计算属性，不能每次访问都新建 GUID 目录。
    private static readonly string ScratchDir = TestPaths.CreateScratchDir("worldview-validator-tests");

    [Fact]
    public void Valid_World_Map_Passes_All_Rules()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        // 在 scratch 里创建一个不存在的 layer ref,只测纯逻辑
        var map = new MapDoc(
            Id: "map_test",
            Name: "Test",
            Type: MapType.World,
            Grid: new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "maps/world/test.yaml", ScratchDir);
        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void Terrain_Cell_Out_Of_Bounds_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var cell = new TerrainCellEntry(new GridCoord(100, 100), TerrainType.Mountain); // 100 >= 64 (width)
        var map = new MapDoc(
            Id: "map_oob",
            Name: "OOB",
            Type: MapType.World,
            Grid: new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(Cells: new[] { cell }),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "maps/world/oob.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_GRID_OOB");
    }

    [Fact]
    public void Terrain_Cell_Duplicate_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var c1 = new TerrainCellEntry(new GridCoord(10, 10), TerrainType.Forest);
        var c2 = new TerrainCellEntry(new GridCoord(10, 10), TerrainType.Mountain);
        var map = new MapDoc(
            Id: "map_dup",
            Name: "Dup",
            Type: MapType.World,
            Grid: new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(Cells: new[] { c1, c2 }),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "maps/world/dup.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_TERRAIN_CELL_DUP");
    }

    [Fact]
    public void Ref_Absolute_Path_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        // CityRef.MapRef 设为 Windows 绝对路径
        var cityRef = new CityRef(
            Id: "city_1", Name: "X", X: 10, Y: 10,
            Entry: new GridCoord(10, 10),
            MapRef: "C:\\abs\\path\\city.yaml");
        var map = new MapDoc(
            Id: "m", Name: "M", Type: MapType.World,
            Grid: new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: new[] { cityRef },
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_ABSOLUTE");
    }

    [Fact]
    public void Ref_Parent_DotDot_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var cityRef = new CityRef(
            Id: "city_1", Name: "X", X: 10, Y: 10,
            Entry: new GridCoord(10, 10),
            MapRef: "../outside/city.yaml");
        var map = new MapDoc(
            Id: "m", Name: "M", Type: MapType.World,
            Grid: new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: new[] { cityRef },
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_PARENT");
    }

    [Fact]
    public void Grid_Warning_When_Exceeds_Sparse_Upper_Bound()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var map = new MapDoc(
            Id: "big", Name: "Big", Type: MapType.World,
            Grid: new GridSpec(2048, 2048, 1.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>());

        var result = new MapValidator().Validate(map, "big.yaml", ScratchDir);
        // 大于 sparse 上限不报错（仅警告）
        Assert.True(result.IsValid, result.ToString());
        Assert.Contains(result.Warnings, w => w.Code == "W_GRID_TOO_LARGE_SPARSE");
    }
}