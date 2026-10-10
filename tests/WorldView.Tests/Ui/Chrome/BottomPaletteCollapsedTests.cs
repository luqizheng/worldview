using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 折叠态 C3 = K1 / §9 增量 3、4）：
/// ① 折叠态不再是「面板已折叠」两行文本提示，也不是整体收起 Rows；
/// ② 折叠时只收起 timeline_row / build_row，tool_row 常驻；
/// ③ 折叠时 ToolStrip 切到紧凑变体；
/// ④ 折叠态下行的 visible 绝不能落盘（同 2026-10-10 F11 事故）；
/// ⑤ 折叠高度仍是 40px。
/// </summary>
public class BottomPaletteCollapsedTests
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

    private static string BottomPaletteSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "BottomPalette.cs"));

    [Fact]
    public void 折叠态不得再用两行文本提示()
    {
        Assert.DoesNotContain("_collapsedBar", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("面板已折叠", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+B 展开 · T 播放条 · M 小地图", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须常驻tool_row并收起另两行()
    {
        Assert.Contains("timelineRow.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("buildRow.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("toolRow.Visible = true;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_rows.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须让Rows纵向居中20px常驻行()
    {
        Assert.Contains("_rows.OffsetTop = collapsed ? 10f : 0f;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("_rows.OffsetBottom = collapsed ? -10f : 0f;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须切到ToolStrip紧凑变体()
    {
        Assert.Contains("toolStrip.SetCollapsedMode(collapsed);", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string ToolStripSectionId = \"tool_strip\";", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠时不得把行的invisible落盘()
    {
        Assert.Contains("rowState.Visible = _collapsed ? true : row.Visible;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠高度必须仍是40()
    {
        Assert.Contains("public const int CollapsedHeight = 40;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CustomMinimumSize = new Vector2(CustomMinimumSize.X, collapsed ? CollapsedHeight : _expandedHeight);", BottomPaletteSource, StringComparison.Ordinal);
    }
}
