using WorldView.Domain;
using Xunit;

namespace WorldView.Tests.Domain;

/// <summary>
/// 11 种地形类型的默认色（spec.md §2.4）+ 默认地形单元字段断言。
/// 对应 checklist F1 + spec.md R-BRUSH-02(笔刷显示色来自 TerrainPalette,而 Palette 走 TerrainDefaults.ColorHex)。
/// </summary>
public class TerrainDefaultsTest
{
    [Theory]
    [InlineData(TerrainType.Plain,    "#c2b280")]
    [InlineData(TerrainType.Mountain, "#7a6f5c")]
    [InlineData(TerrainType.Forest,   "#3f7a3f")]
    [InlineData(TerrainType.Hill,     "#8a7a4a")]
    [InlineData(TerrainType.Water,    "#3a6ea5")]
    [InlineData(TerrainType.Desert,   "#d9c47a")]
    [InlineData(TerrainType.Swamp,    "#5a6a3a")]
    [InlineData(TerrainType.Tundra,   "#cfd6e0")]
    [InlineData(TerrainType.Basin,    "#7a8aa6")]
    [InlineData(TerrainType.Coast,    "#a8c3d6")]
    [InlineData(TerrainType.River,    "#5e8fc4")]
    public void ColorHex_Matches_Spec(TerrainType type, string expected)
    {
        Assert.Equal(expected, TerrainDefaults.ColorHex(type));
    }

    [Fact]
    public void All_TerrainTypes_Have_NonEmpty_Color()
    {
        foreach (var type in Enum.GetValues<TerrainType>())
        {
            var hex = TerrainDefaults.ColorHex(type);
            // hex 以 '#' 开头 + 6 位 hex
            Assert.Matches("^#[0-9a-fA-F]{6}$", hex);
        }
    }

    [Fact]
    public void Default_Plain_Cell_Has_Spec_Defaults()
    {
        var plain = TerrainDefaults.Plain;
        Assert.Equal(TerrainType.Plain, plain.Type);
        Assert.Equal(1, plain.MoveCost);
        Assert.True(plain.Buildable);
        Assert.Null(plain.Height); // MVP 不写 height(spec.md N01 推迟到 M3+)
    }

    [Fact]
    public void Default_Constants_Match_Spec()
    {
        Assert.Equal(TerrainType.Plain, TerrainDefaults.DefaultType);
        Assert.Equal(1, TerrainDefaults.DefaultMoveCost);
        Assert.True(TerrainDefaults.DefaultBuildable);
    }
}
