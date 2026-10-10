using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 row1 的 <c>tools</c> 工具方块区 / §9 增量 1、5）：
/// ① 场景可被 <c>BottomPalette.LoadContent("ToolPaletteSection")</c> 加载且脚本路径正确；
/// ② 内部分组（地形 / 建筑）互斥可见，任何时刻只显示一个分组；
/// ③ 建筑面板的私有布局状态经本 section 转发进同一个 <c>PanelState</c>（schema 不变）。
/// </summary>
public class ToolPaletteSectionTests
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

    private static string ScriptSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolPaletteSection.cs"));

    private static string SceneSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scenes", "ui", "ToolPaletteSection.tscn"));

    [Fact]
    public void 场景必须存在并挂载工具方块区脚本()
    {
        Assert.Contains("path=\"res://scripts/Ui/Chrome/ToolPaletteSection.cs\"", SceneSource, StringComparison.Ordinal);
        Assert.Contains("type=\"Control\"", SceneSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 必须定义地形与建筑两个分组ID()
    {
        Assert.Contains("public const string TerrainGroupId = \"terrain\";", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("public const string BuildingGroupId = \"building\";", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 分组切换必须互斥设置可见性()
    {
        Assert.Contains("_terrainGrid.Visible = isTerrain;", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("_buildPalette.Visible = !isTerrain;", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 必须加载建筑面板场景()
    {
        Assert.Contains("\"res://scenes/ui/BuildPaletteSection.tscn\"", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("InstantiateOrNull<BuildPaletteSection>()", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 地形色块必须由TerrainType全量生成()
    {
        Assert.Contains("foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("new TerrainSwatch { Name = \"Swatch\" + captured, TerrainType = captured }", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 点地形色块必须切换成笔刷工具并设绘制地形()
    {
        Assert.Contains("context.SetPaintTerrain(terrain);", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("context.SetTool(ToolState.Brush);", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 建筑面板状态必须转发进本section的PanelState()
    {
        Assert.Contains("_buildPalette?.ApplyState(state);", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("_buildPalette?.CaptureState(state);", ScriptSource, StringComparison.Ordinal);
    }
}
