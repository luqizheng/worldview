using WorldView.Domain;
using WorldView.Storage.Yaml;
using WorldView.Tests.Support;
using Xunit;

namespace WorldView.Tests.Storage;

/// <summary>
/// 示例工程端到端加载测试。对应 checklist B1-B7 + check §1.4 冒烟脚本 G3/G4。
///
/// 加载真实 <c>examples/sample_world/</c>,断言:
///  - Validation.IsValid == true(checklist D8 反例通过的前提)
///  - 字段齐全(B3-B7):world+city、4 类图层、city_ref、2 建筑
///  - 创建城市地图默认值(G4):grid 64×64 / cell_distance=2 / granularity=hour
/// </summary>
public class ProjectLoaderTests
{
    private static string SampleWorldDir => TestPaths.SampleWorldDir;

    [Fact]
    public void Load_Sample_World_Passes_All_Validators()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        Assert.True(result.Validation.IsValid,
            $"示例工程校验应通过,但发现 {result.Validation.Errors.Count} 个错误:\n{result.Validation}");
    }

    [Fact]
    public void Load_Sample_World_Has_Two_Maps()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        Assert.Equal(2, result.Maps.Count);

        var world = Assert.Single(result.Maps, m => m.Type == MapType.World);
        var city = Assert.Single(result.Maps, m => m.Type == MapType.City);

        Assert.Equal("map_world_kael", world.Id);
        Assert.Equal("艾瑟兰大陆", world.Name);
        Assert.Equal("map_city_kael", city.Id);
        Assert.Equal("极光港", city.Name);
    }

    [Fact]
    public void Load_Sample_World_Grid_Defaults_Match_Spec()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        var world = result.Maps.First(m => m.Type == MapType.World);
        Assert.Equal(64, world.Grid.Width);
        Assert.Equal(48, world.Grid.Height);
        Assert.Equal(10.0, world.Grid.CellDistance);

        var city = result.Maps.First(m => m.Type == MapType.City);
        Assert.Equal(64, city.Grid.Width);
        Assert.Equal(64, city.Grid.Height);
        Assert.Equal(2.0, city.Grid.CellDistance);
        Assert.Equal("m", city.Grid.DistanceUnit);
        Assert.Equal(TimeGranularity.Hour, city.Time.DefaultGranularity);
        Assert.Equal(TimeGranularity.Hour, city.Time.MinGranularity);
    }

    [Fact]
    public void Load_Sample_World_Has_Four_Layers()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        Assert.True(result.Layers.Count >= 4,
            $"应至少 4 类图层,实际 {result.Layers.Count}");

        var kinds = result.Layers.Select(l => l.Type).Distinct().ToHashSet();
        Assert.Contains(LayerKind.Terrain, kinds);
        Assert.Contains(LayerKind.Road, kinds);
        Assert.Contains(LayerKind.Building, kinds);
        Assert.Contains(LayerKind.Marker, kinds);
    }

    [Fact]
    public void Load_Sample_World_Has_One_City_Ref_To_City_Map()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        var world = result.Maps.First(m => m.Type == MapType.World);
        Assert.Single(world.CityRefs);

        var cityRef = world.CityRefs[0];
        Assert.Equal("city_kael", cityRef.Id);
        Assert.Equal("极光港", cityRef.Name);
        Assert.Equal(32, cityRef.X);
        Assert.Equal(24, cityRef.Y);
        Assert.Equal("maps/city/city_kael.yaml", cityRef.MapRef);
    }

    [Fact]
    public void Load_Sample_World_Has_Two_Buildings_In_City()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        var city = result.Maps.First(m => m.Type == MapType.City);
        Assert.Equal(2, city.Buildings.Count);

        var house = city.Buildings.First(b => b.Type == BuildingType.House);
        Assert.Equal("b_kael_house_01", house.Id);
        Assert.Equal(new GridCoord(20, 30), house.At);
        Assert.Equal("map_city_kael", house.MapId);

        var tower = city.Buildings.First(b => b.Type == BuildingType.Tower);
        Assert.Equal("b_kael_tower_01", tower.Id);
        Assert.Equal(new GridCoord(32, 32), tower.At);
    }

    [Fact]
    public void Load_Sample_World_Presentation_Is_2D_Parchment()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        foreach (var map in result.Maps)
        {
            Assert.Equal("2d", map.Presentation.ViewMode);
            Assert.Equal("parchment", map.Presentation.Theme);
        }
    }

    [Fact]
    public void Load_Sample_World_Terrain_Encoding_Is_Sparse()
    {
        var result = ProjectLoader.Load(SampleWorldDir);

        foreach (var map in result.Maps)
        {
            Assert.Equal("sparse", map.TerrainData.Encoding);
            Assert.NotNull(map.TerrainData.Default);
            Assert.Equal(TerrainType.Plain, map.TerrainData.Default!.Type);
        }
    }

    [Fact]
    public void Load_Sample_World_No_Chunked_Encoding_In_Any_Map()
    {
        // K1:示例工程与 scripts/Domain/ 不能出现 chunked 字样
        var result = ProjectLoader.Load(SampleWorldDir);
        foreach (var map in result.Maps)
        {
            Assert.NotEqual("chunked", map.TerrainData.Encoding);
        }
    }

    [Fact]
    public void Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute()
    {
        // K3:K3 `ref` 不含 `..` 或绝对路径
        var result = ProjectLoader.Load(SampleWorldDir);

        Assert.All(result.World.MapIndex, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.RefPath));
            Assert.DoesNotContain("..", entry.RefPath);
            Assert.False(Path.IsPathRooted(entry.RefPath));
        });

        Assert.All(result.Maps.SelectMany(m => m.Layers), lref =>
        {
            Assert.DoesNotContain("..", lref.RefPath);
            Assert.False(Path.IsPathRooted(lref.RefPath));
        });

        Assert.All(result.Maps.SelectMany(m => m.CityRefs), cref =>
        {
            Assert.DoesNotContain("..", cref.MapRef);
            Assert.False(Path.IsPathRooted(cref.MapRef));
        });
    }

    [Fact]
    public void Load_Nonexistent_Root_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => ProjectLoader.Load(Path.Combine(TestPaths.RepoRoot, "nonexistent-dir-for-test")));
    }

    [Fact]
    public void Load_Root_Without_ProjectYaml_Throws()
    {
        var dir = TestPaths.CreateScratchDir("worldview-empty-dir");
        try
        {
            Assert.Throws<FileNotFoundException>(() => ProjectLoader.Load(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
