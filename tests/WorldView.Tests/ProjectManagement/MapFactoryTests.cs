using System.Collections.Generic;
using WorldView.Domain;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class MapFactoryTests
{
    [Fact]
    public void Create_World_Has_SpecDefaults()
    {
        var map = MapFactory.CreateBlankMap(
            MapType.World,
            id: "map_world_1",
            displayName: "测试世界",
            defaultLayers: new List<LayerRef>());

        Assert.Equal(MapType.World, map.Type);
        Assert.Equal(64, map.Grid.Width);
        Assert.Equal(48, map.Grid.Height);
        Assert.Equal(10.0, map.Grid.CellDistance);
        Assert.Equal("km", map.Grid.DistanceUnit);
        Assert.Equal(TimeGranularity.Month, map.Time.DefaultGranularity);
        Assert.Equal(TimeGranularity.Day, map.Time.MinGranularity);
    }

    [Fact]
    public void Create_City_Has_SpecDefaults()
    {
        var map = MapFactory.CreateBlankMap(
            MapType.City,
            id: "map_city_1",
            displayName: "测试城市",
            defaultLayers: new List<LayerRef>());

        Assert.Equal(MapType.City, map.Type);
        Assert.Equal(64, map.Grid.Width);
        Assert.Equal(64, map.Grid.Height);
        Assert.Equal(2.0, map.Grid.CellDistance);
        Assert.Equal("m", map.Grid.DistanceUnit);
        Assert.Equal(TimeGranularity.Hour, map.Time.DefaultGranularity);
        Assert.Equal(TimeGranularity.Hour, map.Time.MinGranularity);
    }

    [Fact]
    public void Create_Respects_Id_And_Name()
    {
        var map = MapFactory.CreateBlankMap(
            MapType.World,
            id: "map_xxx",
            displayName: "测试",
            defaultLayers: new List<LayerRef>());
        Assert.Equal("map_xxx", map.Id);
        Assert.Equal("测试", map.Name);
    }

    [Fact]
    public void Create_Has_No_Cells_And_Default_Terrain()
    {
        var map = MapFactory.CreateBlankMap(
            MapType.World,
            id: "map_world_1",
            displayName: "X",
            defaultLayers: new List<LayerRef>());

        Assert.Equal("sparse", map.TerrainData.Encoding);
        Assert.NotNull(map.TerrainData.Default);
        Assert.Equal(TerrainType.Plain, map.TerrainData.Default!.Type);
        Assert.Empty(map.TerrainData.Cells);
        Assert.Null(map.TerrainData.CellsRef);
        Assert.Null(map.Generation);
        Assert.Empty(map.CityRefs);
        Assert.Empty(map.Buildings);
    }

    [Fact]
    public void Create_Honors_DefaultLayers()
    {
        var layers = new List<LayerRef>
        {
            new("../layers/terrain.yaml"),
            new("../layers/road.yaml"),
        };
        var map = MapFactory.CreateBlankMap(
            MapType.World,
            id: "map_world_1",
            displayName: "X",
            defaultLayers: layers);
        Assert.Equal(2, map.Layers.Count);
        Assert.Equal("../layers/terrain.yaml", map.Layers[0].RefPath);
    }
}
