using System;
using System.IO;
using System.Linq;
using WorldView.Ui.Dock.Leaf;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 row1 / §8 / §9 增量 1、5）：
/// ① 默认 section 配置里 building row 由 <c>build_palette</c> 换成 <c>tools</c>（工具方块区）；
/// ② 小地图仍是 fixed / width=auto 的方形；
/// ③ 旧布局文件里已不存在的 section（如 <c>build_palette</c>）必须被跳过而不是崩；
/// ④ schema 版本不变、折叠 / 展开高度不变。
///
/// <para><b>两类护栏</b>：<see cref="LayoutState.Default"/> 是纯 .NET（不碰 Godot 运行时），
/// 所以默认布局文档用<b>真行为断言</b>直接读对象树（<c>默认布局文档的tools节必须接线正确</c>）；
/// 而 <c>BottomPalette.EnsureDefaultRows()</c> 要构造 Godot 节点、测试进程无法实例化，
/// 只能退化为<b>源码文本护栏</b>（见本类其余 Fact）。</para>
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
        // 只保留源码护栏：ApplySectionStates 要构造 Godot 节点，测试进程没有 Godot 运行时，
        // 无法用行为断言覆盖这条守卫。
        Assert.Contains("var section = SectionOf(sectionState.Id);", BottomPaletteSource, StringComparison.Ordinal);

        // 「守卫必须真的执行 continue」不能写成 DoesContain("continue;")：本文件里 continue; 有 6 处
        // （:237 :356 :438 :484 :530 :589），删掉守卫那条断言照样通过（空断言）。
        // 因此锚成位置耦合断言：守卫被删时 IndexOf 返回 -1，断言必然失败。
        var guard = BottomPaletteSource.IndexOf("if (section is null)", StringComparison.Ordinal);
        Assert.True(guard >= 0, "ApplySectionStates 里的「未知 section」守卫 `if (section is null)` 不见了");

        var afterGuard = BottomPaletteSource.Substring(guard, Math.Min(80, BottomPaletteSource.Length - guard));
        Assert.Contains("continue;", afterGuard, StringComparison.Ordinal);
    }

    [Fact]
    public void 默认布局文档的tools节必须接线正确()
    {
        // 行为护栏：LayoutState.Default 是纯 .NET（只经 ProjectIdOf → Path.GetFileName），
        // 可以在测试进程里直接构造并读对象树 —— 这能抓住「id 字符串对了但接线错 / 缺字段」的回归，
        // 而源码子串断言抓不到。projectRoot 取一个不存在的路径即可（Default 不读盘）。
        var layout = LayoutState.Default("D:/tmp/sample_world");
        Assert.Equal(LayoutState.CurrentSchemaVersion, layout.SchemaVersion);
        Assert.Equal("sample_world", layout.ProjectId);

        var bottomPalette = Assert.Single(layout.Root, entry => entry.Id == "bottom_palette");
        var buildRow = Assert.Single(bottomPalette.Rows!, row => row.Id == "build_row");

        var tools = Assert.Single(buildRow.Sections!, section => section.Id == "tools");
        Assert.Equal("flex", tools.SizeMode);
        Assert.Equal(1, tools.Flex);
        Assert.Equal(1, tools.Order);

        // 建筑面板的私有状态仍挂在这一项上（只是归属从 build_palette 换成 tools）。
        Assert.Equal(56, tools.CellSize);
        Assert.Equal(4, tools.CellGap);
        Assert.Equal("residences", tools.ActiveCategory);
        Assert.NotNull(tools.PagesByCategory);
        Assert.Equal(
            new[] { "castles", "facilities", "military", "nature", "religion", "residences" },
            tools.PagesByCategory!.Keys.OrderBy(key => key, StringComparer.Ordinal));
        Assert.All(tools.PagesByCategory, page => Assert.Equal(0, page.Value));

        Assert.DoesNotContain(buildRow.Sections!, section => section.Id == "build_palette");

        // 小地图仍必须是 fixed + width=auto（用 AutoWidth 读属性，别比 Width 的装箱类型）。
        var minimap = Assert.Single(buildRow.Sections!, section => section.Id == "minimap");
        Assert.Equal("fixed", minimap.SizeMode);
        Assert.True(minimap.AutoWidth);
    }
}
