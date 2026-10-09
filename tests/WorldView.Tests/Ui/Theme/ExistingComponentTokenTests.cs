// <auto-not-in-spec />
// Task 1 仓单 §8.1.3 — ExistingComponentTokenTests(4 个)
// 验证 spec §4 既有组件读 Palette.Metric.*,不再依赖硬编码 const。
// Task 2/3 阶段才能把 const 全部切走;本仓单先建好占位,等切换完成后跑通。

using System.Reflection;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Theme;

public class ExistingComponentTokenTests
{
    [Fact]
    public void Palette_Metric_ContainsRequiredKeys()
    {
        // spec §4.5.2 列出的 19 个 metric key 必须存在(命名 PascalCase 化后)。
        // 字段名由 Generator 决定,断言语义: 必须包含 *关键* 字段。
        var metric = typeof(Palette).GetNestedType("Metric",
            BindingFlags.Public | BindingFlags.NonPublic)!;
        var names = metric.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToHashSet();
        Assert.Contains("FontSizeBody", names);     // 14
        Assert.Contains("FontSizeTitle", names);    // 16
        Assert.Contains("LineHeight", names);       // 22
        Assert.Contains("CornerRadius", names);     // 2
        Assert.Contains("ButtonHeight", names);     // 32
        Assert.Contains("IconSizeMd", names);       // 32
    }

    [Fact]
    public void Palette_Metric_FontSizeBody_Is14()
    {
        var metric = typeof(Palette).GetNestedType("Metric",
            BindingFlags.Public | BindingFlags.NonPublic)!;
        var f = metric.GetField("FontSizeBody", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(14, f.GetValue(null));
    }

    [Fact]
    public void Palette_Shadow_OffsetX_Is1()
    {
        var shadow = typeof(Palette).GetNestedType("Shadow",
            BindingFlags.Public | BindingFlags.NonPublic)!;
        var f = shadow.GetField("OffsetX", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(1, f.GetValue(null));
    }

    [Fact]
    public void Palette_Animation_FadeOutMs_Is150()
    {
        var anim = typeof(Palette).GetNestedType("Animation",
            BindingFlags.Public | BindingFlags.NonPublic)!;
        var f = anim.GetField("FadeOutMs", BindingFlags.Public | BindingFlags.Static)!;
        Assert.Equal(150, f.GetValue(null));
    }
}
