using WorldView.Domain;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.Storage;

/// <summary>
/// T6 DoD: YamlSchemas.Deserialize/Serialize + GridCoord 双向转换。
/// </summary>
public class YamlSchemasTest
{
    [Fact]
    public void Deserialize_GridCoord_As_Array()
    {
        const string yaml = "id: test\nname: T\ntype: world\ngrid:\n  width: 64\n  height: 48\n  cell_distance: 10.0\n  distance_unit: m\nbase_unit: m\nheight_unit: m\ntime:\n  default_granularity: month\n  min_granularity: day\npresentation: {}\nterrain_data:\n  encoding: sparse\n  default:\n    type: plain\n    move_cost: 1\n    buildable: true\nlayers: []\ncity_refs: []\nbuildings: []\n";
        var map = YamlSchemas.Deserialize<MapDoc>(yaml);
        Assert.Equal("map_test", map.Id);
        Assert.Equal(64, map.Grid.Width);
        Assert.Equal(48, map.Grid.Height);
        Assert.Equal("m", map.Grid.DistanceUnit);
    }

    [Fact]
    public void Roundtrip_TerrainCell_Entry_Preserves_At()
    {
        const string yaml = "- at: [10, 20]\n  type: mountain\n  move_cost: 4\n  buildable: false\n- at: [3, 4]\n  type: forest\n";
        var cells = YamlSchemas.Deserialize<List<TerrainCellEntry>>(yaml);
        Assert.Equal(2, cells.Count);
        Assert.Equal(new GridCoord(10, 20), cells[0].At);
        Assert.Equal(TerrainType.Mountain, cells[0].Type);
        Assert.Equal(4, cells[0].MoveCost);
        Assert.False(cells[0].Buildable!.Value);

        // 写回应保留 [x, y] 数组格式
        var roundtrip = YamlSchemas.Serialize(cells);
        Assert.Contains("[10, 20]", roundtrip);
        Assert.Contains("[3, 4]", roundtrip);
    }

    [Fact]
    public void Deserialize_World_Project_Header()
    {
        const string yaml = "project_version: \"0.1\"\nname: 测试\ndefault_time:\n  year: 1042\n  month: 3\n  day: 12\nera:\n  name: 创世历\n  start_year: 1\n  months_per_year: 12\n  days_per_month: [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]\nmap_index:\n  - ref: maps/world/main.yaml\ncharacter_index: []\n";
        var world = YamlSchemas.Deserialize<World>(yaml);
        Assert.Equal("0.1", world.ProjectVersion);
        Assert.Equal("测试", world.Name);
        Assert.Equal(1042, world.DefaultTime.Year);
        Assert.Equal(12, world.Era.DaysPerMonth.Count);
        Assert.Single(world.MapIndex);
        Assert.Equal("maps/world/main.yaml", world.MapIndex[0].RefPath);
    }
}