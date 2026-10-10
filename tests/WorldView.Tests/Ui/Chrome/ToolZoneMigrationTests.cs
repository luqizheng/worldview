using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 实现提示「地形色块宜从 ToolStrip 迁入工具方块区，ToolStrip 只留笔刷半径/坐标/状态/校验」）：
/// ① <c>ToolStrip</c> 不再承载地形色块；
/// ② 工具方块区承载地形色块；
/// ③ <c>ToolStrip</c> 保留工具切换三键与笔刷半径 / 坐标；
/// ④ 折叠态（K1）必须有紧凑变体入口。
/// </summary>
public class ToolZoneMigrationTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string ToolStripSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolStrip.cs"));

    private static string ToolPaletteSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolPaletteSection.cs"));

    [Fact]
    public void ToolStrip不得再承载地形色块()
    {
        Assert.DoesNotContain("TerrainStrip", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerrainSwatch", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_terrainSwatches", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerrainOrder", ToolStripSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 工具方块区必须承载地形色块()
    {
        Assert.Contains("new TerrainSwatch", ToolPaletteSource, StringComparison.Ordinal);
        Assert.Contains("context.SetPaintTerrain(terrain);", ToolPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolStrip必须保留工具切换三键与笔刷半径与坐标()
    {
        Assert.Contains("ToolState.Selected", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("ToolState.Brush", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("ToolState.Building", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_radiusSpin", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_coordLabel", ToolStripSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolStrip必须提供折叠紧凑变体()
    {
        Assert.Contains("public void SetCollapsedMode(bool collapsed)", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_brushGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_historyGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_stateGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
    }
}
