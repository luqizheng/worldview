using WorldView.Domain;
using Xunit;

namespace WorldView.Tests.Domain;

/// <summary>
/// T4 DoD: Domain 层 record equality + TerrainDefaults 默认值。
/// </summary>
public class TerrainCellRecordTest
{
    [Fact]
    public void Plain_DefaultCell_Has_Expected_Values()
    {
        var cell = TerrainDefaults.Plain;
        Assert.Equal(TerrainType.Plain, cell.Type);
        Assert.Equal(1, cell.MoveCost);
        Assert.True(cell.Buildable);
    }

    [Fact]
    public void RecordEquality_Two_Plain_Cells_Are_Equal()
    {
        var a = new TerrainCell(TerrainType.Plain, 1, true);
        var b = new TerrainCell(TerrainType.Plain, 1, true);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RecordInequality_Different_Terrain_Not_Equal()
    {
        var a = new TerrainCell(TerrainType.Plain, 1, true);
        var b = new TerrainCell(TerrainType.Mountain, 2, false);
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(TerrainType.Plain, "#c2b280")]
    [InlineData(TerrainType.Mountain, "#7a6f5c")]
    [InlineData(TerrainType.Water, "#3a6ea5")]
    public void ColorHex_Returns_Spec_Color(TerrainType type, string expected)
    {
        Assert.Equal(expected, TerrainDefaults.ColorHex(type));
    }
}