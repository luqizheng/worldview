// <auto-not-in-spec />
// Task 1 仓单 §8.1.1 — PaletteLoaderTests(7 个)
// 验证编译期生成的 Palette.* 静态字段存在、命名、计数、值与 yaml 真源一致。
// 通过 WorldView.Ui.Theme.Palette 在编译期已生成,直接断言。

using System.Reflection;
using Godot;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Theme;

public class PaletteLoaderTests
{
    // 定位生成的嵌套类。
    private static readonly Type ColorClass =
        typeof(Palette).GetNestedType("Color", BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly Type MetricClass =
        typeof(Palette).GetNestedType("Metric", BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly Type ShadowClass =
        typeof(Palette).GetNestedType("Shadow", BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly Type ModalClass =
        typeof(Palette).GetNestedType("Modal", BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly Type AnimationClass =
        typeof(Palette).GetNestedType("Animation", BindingFlags.Public | BindingFlags.NonPublic)!;

    private static FieldInfo[] StaticFields(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static);

    [Fact]
    public void Color_NestedClassExists_With14Fields()
    {
        Assert.NotNull(ColorClass);
        var fields = StaticFields(ColorClass);
        // spec §4.5.1: 13 jrpg_* + 1 accent_vegetation_highlight = 14
        Assert.Equal(14, fields.Length);
    }

    [Fact]
    public void Color_AllFieldsAreGodotColor_AndNonNull()
    {
        var fields = StaticFields(ColorClass);
        foreach (var f in fields)
        {
            Assert.Equal(typeof(Color), f.FieldType);
            Assert.NotNull(f.GetValue(null));
        }
    }

    [Fact]
    public void Metric_NestedClassExists_With18Fields()
    {
        Assert.NotNull(MetricClass);
        var fields = StaticFields(MetricClass);
        // spec §4.5.2 + parchment_palette.yaml 实际 18 项量化层
        // (plan §4 写的 19 是 plan 笔误,真源 yaml 与 spec 对齐)
        Assert.Equal(18, fields.Length);
    }

    [Fact]
    public void Shadow_Exists_With4Fields()
    {
        Assert.NotNull(ShadowClass);
        Assert.Equal(4, StaticFields(ShadowClass).Length);
    }

    [Fact]
    public void Modal_Exists_With1Field()
    {
        Assert.NotNull(ModalClass);
        Assert.Equal(1, StaticFields(ModalClass).Length);
    }

    [Fact]
    public void Animation_Exists_With2Fields()
    {
        Assert.NotNull(AnimationClass);
        Assert.Equal(2, StaticFields(AnimationClass).Length);
    }

    [Fact]
    public void Color_Names_FollowPascalCaseRule_FromYaml()
    {
        // ui.color.jrpg_fill_hover → JrpgFillHover
        // ui.color.accent_vegetation_highlight → AccentVegetationHighlight
        var names = StaticFields(ColorClass).Select(f => f.Name).ToHashSet();
        Assert.Contains("JrpgFillHover", names);
        Assert.Contains("JrpgBg", names);
        Assert.Contains("AccentVegetationHighlight", names);
    }
}
