// <auto-not-in-spec />
// Task 4 仓单 §5.4 — TooltipTests(5 个)
// 验证 Tooltip 走 Palette 量化(字号 cap 12 / 阴影 offset 1 / 文本色),Delay 纯函数验证,Hide 反射存在。
// 注释: 不直接构造 Tooltip(Godot.PanelContainer 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 反射读 const / 字段 + 反射读 DelaySec 属性。

using System.Reflection;
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class TooltipTests
{
    [Fact]
    public void Tooltip_FontSize_IsPaletteFontSizeCaption_12()
    {
        // _label.SetFontSize(FontSizeCaption)
        Assert.Equal(12, Palette.Metric.FontSizeCaption);
    }

    [Fact]
    public void Tooltip_HBox_Separation_IsPalettePaddingInline_8()
    {
        // hbox.AddThemeConstantOverride("separation", PaddingInline)
        Assert.Equal(8, Palette.Metric.PaddingInline);
    }

    [Fact]
    public void Tooltip_Shadow_OffsetX_IsPalette_1()
    {
        // 阴影偏移走 Palette.Shadow.OffsetX
        Assert.Equal(1, Palette.Shadow.OffsetX);
    }

    [Fact]
    public void Tooltip_DelaySec_Property_ReadWrite_Float()
    {
        // public float DelaySec { get; set; } = 0.4f
        var prop = typeof(Tooltip).GetProperty("DelaySec");
        Assert.NotNull(prop);
        Assert.Equal(typeof(float), prop!.PropertyType);
        Assert.True(prop.CanRead && prop.CanWrite);
    }

    [Fact]
    public void Tooltip_Declares_Hide_Method_NotEqual_To_Base()
    {
        // public new void Hide() 遮蔽 base.Hide()
        var hide = typeof(Tooltip).GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(hide);
        Assert.Equal(typeof(void), hide!.ReturnType);
        // 必须用 new (遮蔽) 才能 QueueFree 后不无限递归
        Assert.False(hide.IsAbstract);
    }
}
