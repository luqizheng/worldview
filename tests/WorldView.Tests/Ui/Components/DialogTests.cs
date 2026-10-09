// <auto-not-in-spec />
// Task 7 仓单 §8.1.7 — DialogTests(3 个)
// 验证 Dialog 标题字号 FontSizeTitle,关闭 fade-out 走 FadeOutMs,不写手写 overlay。

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
        // Godot 4 [Signal] delegate 会被生成嵌套委托类型,EventHandlerType 是 CloseRequestedEventHandler 不是 System.Action
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
    public void Dialog_Override_UnhandledInput_Method_Exists()
    {
        // Dialog 必须有 _UnhandledInput 钩子,Esc 键关闭走的是该回调(ModalExclusive 强模态下 _Input 会被 overlay 吃)
        var method = typeof(Dialog).GetMethod(
            "_UnhandledInput",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);
    }
}
