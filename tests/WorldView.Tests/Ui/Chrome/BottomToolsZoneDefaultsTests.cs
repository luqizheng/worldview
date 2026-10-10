using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 row1 / §8 / §9 增量 1、5）：
/// ① 默认 section 配置里 building row 由 <c>build_palette</c> 换成 <c>tools</c>（工具方块区）；
/// ② 小地图仍是 fixed / width=auto 的方形；
/// ③ 旧布局文件里已不存在的 section（如 <c>build_palette</c>）必须被跳过而不是崩；
/// ④ schema 版本不变、折叠 / 展开高度不变。
/// </summary>
public class BottomToolsZoneDefaultsTests
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

    private static string Read(params string[] parts)
    {
        var path = RepoRoot;
        foreach (var part in parts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string BottomPaletteSource => Read("scripts", "Ui", "Chrome", "BottomPalette.cs");

    private static string PanelStateSource => Read("scripts", "Ui", "Dock", "Leaf", "PanelState.cs");

    [Fact]
    public void 默认布局必须用工具方块区替换建造网格()
    {
        Assert.Contains("public const string ToolsSectionId = \"tools\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CreateSection(ToolsSectionId, \"工具\", PaletteSection.SectionSizeMode.Flex, 1, false, \"ToolPaletteSection\")", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"build_palette\"", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 默认布局的_build_row_必须含_tools_节且不再含_build_palette()
    {
        Assert.Contains("Id = \"tools\"", PanelStateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"build_palette\"", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 小地图必须仍是_fixed_auto_方形()
    {
        Assert.Contains("CreateSection(\"minimap\", \"小地图\", PaletteSection.SectionSizeMode.Fixed, 1, true, \"MinimapSection\")", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("Width = \"auto\"", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 三行结构必须不变()
    {
        Assert.Contains("private const string TimelineRowId = \"timeline_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string BuildRowId = \"build_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string ToolRowId = \"tool_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CreateRow(BuildRowId, fullWidth: false, height: 0, flex: 1, order: 1)", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 高度与schema版本必须不变()
    {
        Assert.Contains("public const int ExpandedHeight = 200;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("public const int CollapsedHeight = 40;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("public const int CurrentSchemaVersion = 1;", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 未知section必须被跳过而不是崩()
    {
        Assert.Contains("var section = SectionOf(sectionState.Id);", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("if (section is null)", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("continue;", BottomPaletteSource, StringComparison.Ordinal);
    }
}
