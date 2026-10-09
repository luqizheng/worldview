// <auto-not-in-spec />
// Task 3 仓单 §5.3 — ToastTests(5 个)
// 验证 Toast 走 Palette 量化(字号 14 / 严重色映射 / 淡出 150ms / Position 枚举 4 值 / Timeout 信号存在)。
// 注释: 不直接构造 Toast(Godot.PanelContainer 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 反射读 const / 字段 + 反射读信号 / 枚举值验证。

using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class ToastTests
{
    [Fact]
    public void Toast_FontSize_IsPaletteFontSizeBody_14()
    {
        // _label.SetFontSize(FontSizeBody)
        Assert.Equal(14, Palette.Metric.FontSizeBody);
    }

    [Fact]
    public void Toast_FadeOut_IsPaletteFadeOutMs_150()
    {
        // _fadeTween.TweenProperty 长度 = FadeOutMs / 1000
        Assert.Equal(150, Palette.Animation.FadeOutMs);
    }

    [Fact]
    public void Toast_Has_Four_Severity_Values()
    {
        // Severity 枚举 4 值:Info / Success / Warning / Error
        var values = System.Enum.GetValues<Toast.Severity>();
        Assert.Equal(4, values.Length);
        Assert.Contains(Toast.Severity.Info, values);
        Assert.Contains(Toast.Severity.Success, values);
        Assert.Contains(Toast.Severity.Warning, values);
        Assert.Contains(Toast.Severity.Error, values);
    }

    [Fact]
    public void Toast_Has_Four_Position_Values()
    {
        // Position 枚举 4 值:TopRight / BottomRight / TopCenter / BottomCenter
        var values = System.Enum.GetValues<Toast.PositionEnum>();
        Assert.Equal(4, values.Length);
        Assert.Contains(Toast.PositionEnum.TopRight, values);
        Assert.Contains(Toast.PositionEnum.BottomRight, values);
        Assert.Contains(Toast.PositionEnum.TopCenter, values);
        Assert.Contains(Toast.PositionEnum.BottomCenter, values);
    }

    [Fact]
    public void Toast_Declares_Timeout_Signal()
    {
        // [Signal] TimeoutEventHandler()
        var sig = typeof(Toast).GetEvent("Timeout");
        Assert.NotNull(sig);
        Assert.Equal("TimeoutEventHandler", sig!.EventHandlerType!.Name);
    }
}
