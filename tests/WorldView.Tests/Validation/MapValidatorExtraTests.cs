using WorldView.Domain;
using WorldView.Domain.Validation;
using WorldView.Tests.Support;
using Xunit;

namespace WorldView.Tests.Validation;

/// <summary>
/// 补充 MapValidator 的边界场景(spec.md §6.5 + checklist I6)。
/// 已由 MapValidatorTest 覆盖的(OOB / 重复 / 绝对 / .. / 大网格警告)不复写。
/// 新增:
///  - E_GRID_SIZE_MISSING(width/height ≤ 0)
///  - E_LAYER_REF_MISSING(layers[].ref 指向不存在的文件)
///  - E_BUILDING_OOB(building.at 越出城市网格)
/// </summary>
public class MapValidatorExtraTests
{
    // 一次性创建：ScratchDir 是计算属性，不能每次访问都新建 GUID 目录。
    private static readonly string ScratchDir = TestPaths.CreateScratchDir("worldview-mv-extra-tests");

    [Fact]
    public void Grid_Zero_Width_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var map = MakeMap(grid: new GridSpec(0, 48, 10.0));

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_GRID_SIZE_MISSING");
    }

    [Fact]
    public void Grid_Zero_Height_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var map = MakeMap(grid: new GridSpec(64, 0, 10.0));

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_GRID_SIZE_MISSING");
    }

    [Fact]
    public void Layer_Ref_To_Missing_File_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var map = MakeMap(layers: new[]
        {
            new LayerRef("layers/no_such_layer.yaml"),
        });

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_LAYER_REF_MISSING");
    }

    [Fact]
    public void Layer_Ref_Absolute_Path_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var map = MakeMap(layers: new[]
        {
            new LayerRef(@"C:\abs\layer.yaml"),
        });

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_ABSOLUTE");
    }

    [Fact]
    public void Building_Out_Of_Bounds_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        // grid 64×64,building @ [100, 50] → 越界
        var building = new Building("b_oob", BuildingType.House, new GridCoord(100, 50), "m");
        var map = MakeMap(buildings: new[] { building });

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_BUILDING_OOB");
    }

    [Fact]
    public void Building_Negative_Coord_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var building = new Building("b_neg", BuildingType.House, new GridCoord(-1, 0), "m");
        var map = MakeMap(buildings: new[] { building });

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_BUILDING_OOB");
    }

    [Fact]
    public void City_Ref_To_Missing_File_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var cref = new CityRef(
            Id: "city_x", Name: "X", X: 10, Y: 10,
            Entry: new GridCoord(10, 10),
            MapRef: "maps/world/no_such_city.yaml");
        var map = MakeMap(cityRefs: new[] { cref });

        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_CITY_REF_MISSING");
    }

    [Fact]
    public void Null_Map_Throws()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        Assert.Throws<ArgumentNullException>(
            () => new MapValidator().Validate(null!, "m.yaml", ScratchDir));
    }

    [Fact]
    public void Empty_Path_Throws()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        Assert.Throws<ArgumentException>(
            () => new MapValidator().Validate(MakeMap(), "", ScratchDir));
    }

    // ---------------------------------------------------------------- helper

    private static MapDoc MakeMap(
        GridSpec? grid = null,
        IReadOnlyList<LayerRef>? layers = null,
        IReadOnlyList<CityRef>? cityRefs = null,
        IReadOnlyList<Building>? buildings = null,
        TerrainData? terrain = null) =>
        new MapDoc(
            Id: "m",
            Name: "M",
            Type: MapType.World,
            Grid: grid ?? new GridSpec(64, 48, 10.0),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: terrain ?? new TerrainData(),
            Layers: layers ?? Array.Empty<LayerRef>(),
            CityRefs: cityRefs ?? Array.Empty<CityRef>(),
            Buildings: buildings ?? Array.Empty<Building>());
}
