using WorldView.Domain;
using Xunit;

namespace WorldView.Tests.Domain;

/// <summary>
/// T4 DoD: Building + GridCoord record equality + 默认值。
/// </summary>
public class BuildingRecordTest
{
    [Fact]
    public void GridCoord_Equality_And_Hash()
    {
        var a = new GridCoord(3, 4);
        var b = new GridCoord(3, 4);
        var c = new GridCoord(4, 3);

        // 值相等
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a == c);
        Assert.True(a != c);

        // 哈希一致
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Building_Record_Equality_By_All_Fields()
    {
        var a = new Building("b_kael_house_01", BuildingType.House, new GridCoord(20, 30), "map_city_kael");
        var b = new Building("b_kael_house_01", BuildingType.House, new GridCoord(20, 30), "map_city_kael");
        var c = new Building("b_kael_tower_01", BuildingType.Tower, new GridCoord(32, 32), "map_city_kael");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
    }
}