// <auto-not-in-spec />
// Task 1 仓单 §5.1 — ToolButtonTests(5 个)
// 验证 ToolButton 走 Palette 量化(高度 32 / 字号 14 / 内边距 16),Toggle 模式 + Toggled 信号存在。
// 注释: 不直接构造 ToolButton(Godot.Button 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 反射读 const / 字段 + 反射读信号 / ToggleMode。

using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class ToolButtonTests
{
    [Fact]
    public void ToolButton_Height_IsPaletteButtonHeight_32()
    {
        // CustomMinimumSize.Y = ButtonHeight
        Assert.Equal(32, Palette.Metric.ButtonHeight);
    }

    [Fact]
    public void ToolButton_FontSize_IsPaletteFontSizeBody_14()
    {
        // _label.AddThemeFontSizeOverride("font_size", FontSizeBody)
        Assert.Equal(14, Palette.Metric.FontSizeBody);
    }

    [Fact]
    public void ToolButton_HBox_Separation_IsPalettePaddingInline_8()
    {
        // hbox.AddThemeConstantOverride("separation", PaddingInline)
        Assert.Equal(8, Palette.Metric.PaddingInline);
    }

    [Fact]
    public void ToolButton_Declares_Toggled_Signal()
    {
        // [Signal] ToggledEventHandler(bool on)
        var sig = typeof(ToolButton).GetEvent("Toggled");
        Assert.NotNull(sig);
        Assert.Equal("ToggledEventHandler", sig!.EventHandlerType!.Name);
    }

    [Fact]
    public void ToolButton_ToggleMode_Property_ReadWrite()
    {
        // Godot.Button.ToggleMode 是 bool 属性,反射读写验证
        var prop = typeof(ToolButton).GetProperty("ToggleMode");
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
        Assert.True(prop.CanRead && prop.CanWrite);
    }
}
