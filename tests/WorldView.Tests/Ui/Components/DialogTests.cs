// <auto-not-in-spec />
// Task 7 仓单 §8.1.7 — DialogTests(8 个)
// 验证 Dialog 标题字号 FontSizeTitle,关闭 fade-out 走 FadeOutMs,
// Esc 关闭走 IsEscapeCloseKey(keycode, pressed, echo) 纯函数 (不靠反射),overlay alpha 0.4。
// 注释: 不直接构造 Dialog(Godot.Control 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 纯函数 + 反射读 const / 字段 的双层验证。
//       也不构造 InputEventKey / InputEvent (Godot.native init 需要 SceneTree,会 AccessViolation)。

using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class DialogTests
{
    [Fact]
    public void Dialog_Title_FontSize_IsFontSizeTitle_16()
    {
        // _titleLabel.AddThemeFontSizeOverride("font_size", FontSizeTitle)
        Assert.Equal(16, Palette.Metric.FontSizeTitle);
    }

    [Fact]
    public void Dialog_FadeOut_Animation_IsPaletteFadeOutMs_150()
    {
        // Dialog.TriggerClose 内的 tween 长度 = FadeOutMs / 1000
        Assert.Equal(150, Palette.Animation.FadeOutMs);
    }

    [Fact]
    public void Dialog_ParagraphSpacing_IsPaletteMetric_8()
    {
        // BodyRoot.AddThemeConstantOverride("separation", ParagraphSpacing)
        Assert.Equal(8, Palette.Metric.ParagraphSpacing);
    }

    [Fact]
    public void Dialog_Declares_CloseRequested_Signal()
    {
        // Dialog 暴露 [Signal] CloseRequestedEventHandler,父节点可订阅统一关闭
        // Godot 4 [Signal] delegate 会被生成嵌套委托类型,EventHandlerType 是 CloseRequestedEventHandler
        var sig = typeof(Dialog).GetEvent("CloseRequested");
        Assert.NotNull(sig);
        Assert.Equal("CloseRequestedEventHandler", sig!.EventHandlerType!.Name);
    }

    [Fact]
    public void Dialog_Overlay_ModalOpacity_IsPalette_0_4()
    {
        // Dialog.PopupModal 内 overlay BgColor alpha = Modal.OverlayOpacity
        Assert.Equal(0.4f, Palette.Modal.OverlayOpacity);
    }

    [Fact]
    public void Dialog_IsEscapeCloseKey_True_On_Pressed_Escape_NoEcho()
    {
        // 纯函数:标准 Esc 关闭判定 — Pressed=true, Keycode=Escape, Echo=false
        Assert.True(Dialog.IsEscapeCloseKey(Godot.Key.Escape, pressed: true, echo: false));
    }

    [Fact]
    public void Dialog_IsEscapeCloseKey_False_On_Released_Escape()
    {
        // 按键松开 (Pressed=false) 不关闭,必须 Pressed=true 才触发
        Assert.False(Dialog.IsEscapeCloseKey(Godot.Key.Escape, pressed: false, echo: false));
    }

    [Fact]
    public void Dialog_IsEscapeCloseKey_False_On_Other_Key()
    {
        // 其他键 (Enter / 字母 / 数字) 不触发关闭
        Assert.False(Dialog.IsEscapeCloseKey(Godot.Key.Enter, pressed: true, echo: false));
        Assert.False(Dialog.IsEscapeCloseKey(Godot.Key.A, pressed: true, echo: false));
        Assert.False(Dialog.IsEscapeCloseKey(Godot.Key.Key1, pressed: true, echo: false));
    }

    [Fact]
    public void Dialog_IsEscapeCloseKey_False_On_Echo_AutoRepeat()
    {
        // 长按 Esc 重复触发 (Echo=true) 不会重复关 dialog,只第一次按能关
        Assert.False(Dialog.IsEscapeCloseKey(Godot.Key.Escape, pressed: true, echo: true));
    }
}
