using WorldView.Domain;
using WorldView.Editor;
using Xunit;

namespace WorldView.Tests.Editor;

/// <summary>
/// 地图尺寸单位换算测试（spec.md §2.2 + task-1-brief）。
///
/// MapSizing 是纯 C# 算术（不引用 Godot），可直接裸测。
/// 关键约定：格数向上取整且至少 1 格；长度 / 格距离为非正数时由 UI 拦截，本类不处理。
/// </summary>
public class MapSizingTests
{
    /// <summary>5000 km 按 50 km/格切分，恰好 100 格。</summary>
    [Fact]
    public void CellsForLength_5000km_at_50km_Is_100()
    {
        Assert.Equal(100, MapSizing.CellsForLength(5000, 50));
    }

    /// <summary>5000 km 按 33 km/格切分，向上取整为 152 格；152×33 = 5016 ≥ 5000，即不丢长度。</summary>
    [Fact]
    public void CellsForLength_5000km_at_33km_Is_152_Covering_Requested_Length()
    {
        Assert.Equal(152, MapSizing.CellsForLength(5000, 33));
        Assert.True(MapSizing.LengthForCells(152, 33) >= 5000);
    }

    /// <summary>不足一格时仍算 1 格，不能截断为 0。</summary>
    [Fact]
    public void CellsForLength_5km_at_50km_Is_One_Not_Zero()
    {
        Assert.Equal(1, MapSizing.CellsForLength(5, 50));
    }

    /// <summary>整除场景：200 km 按 50 km/格切分为 4 格。</summary>
    [Fact]
    public void CellsForLength_200km_at_50km_Is_4()
    {
        Assert.Equal(4, MapSizing.CellsForLength(200, 50));
    }

    /// <summary>格距离非正数属于调用方错误，直接抛出。</summary>
    [Fact]
    public void CellsForLength_Zero_CellDistance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapSizing.CellsForLength(100, 0));
    }

    /// <summary>12800 km 按 25 km/格切分为 512 格（宽高一致，呈正方形网格）。</summary>
    [Fact]
    public void GridFromExtent_12800km_at_25km_Is_512x512()
    {
        var g = MapSizing.GridFromExtent(12800, 12800, new GridSpec(0, 0, 25.0, "km", "m", "m"));

        Assert.Equal(512, g.Width);
        Assert.Equal(512, g.Height);
    }

    /// <summary>只有宽高被改写，其余字段（单位）保持模板原值。</summary>
    [Fact]
    public void GridFromExtent_Keeps_Other_Template_Fields()
    {
        var g = MapSizing.GridFromExtent(12800, 12800, new GridSpec(0, 0, 25.0, "km", "m", "m"));

        Assert.Equal("km", g.DistanceUnit);
        Assert.Equal("m", g.BaseUnit);
    }

    /// <summary>笔刷半径上限为 64 格（UI 输入 km/m 时的封顶值）。</summary>
    [Fact]
    public void MaxRadiusCells_Is_64()
    {
        Assert.Equal(64, MapSizing.MaxRadiusCells);
    }

    /// <summary>格数 × 格距离换算回长度，向上取整的结果必须覆盖原长度。</summary>
    [Fact]
    public void LengthForCells_152_Cells_at_33km_Covers_5000km()
    {
        Assert.True(MapSizing.LengthForCells(152, 33) >= 5000);
    }
}
