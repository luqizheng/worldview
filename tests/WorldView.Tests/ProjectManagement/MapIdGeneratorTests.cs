using WorldView.Domain;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class MapIdGeneratorTests
{
    [Fact]
    public void NextId_World_When_Empty_Returns_1()
    {
        var world = new World(
            MapIndex: new List<MapIndexEntry>(),
            CharacterIndex: new List<MapIndexEntry>());
        Assert.Equal("map_world_1", MapIdGenerator.NextId(world, MapType.World));
    }

    [Fact]
    public void NextId_City_Skips_Used_Numbers()
    {
        var world = new World(
            MapIndex: new List<MapIndexEntry>
            {
                new("maps/city/city_kael.yaml"),     // 解析失败（不是 map_city_3 形式）
                new("maps/city/map_city_3.yaml"),    // 命中后缀 3
                new("maps/world/map_world_2.yaml"),  // 跨类型不计入
            },
            CharacterIndex: new List<MapIndexEntry>());
        Assert.Equal("map_city_4", MapIdGenerator.NextId(world, MapType.City));
    }

    [Fact]
    public void NextId_Handles_MissingMapIndex()
    {
        // World 防御性默认值：MapIndex 是 Array.Empty()。
        // 即便如此，下一次也要返回 map_world_1。
        var world = new World();
        Assert.Equal("map_world_1", MapIdGenerator.NextId(world, MapType.World));
    }

    [Fact]
    public void NextId_Region_Does_Not_Affect_World_Pool()
    {
        // region 类型的 ID 不会污染 world 池子：world 仍从 1 起算
        var world = new World(
            MapIndex: new List<MapIndexEntry> { new("maps/region/map_region_5.yaml") },
            CharacterIndex: new List<MapIndexEntry>());
        Assert.Equal("map_world_1", MapIdGenerator.NextId(world, MapType.World));
    }
}
