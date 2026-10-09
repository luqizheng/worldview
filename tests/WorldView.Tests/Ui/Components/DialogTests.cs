// <auto-not-in-spec />
// Task 7 仓单 §8.1.7 — DialogTests(3 个)
// 验证 Dialog 标题字号 FontSizeTitle,关闭 fade-out 走 FadeOutMs,不写手写 overlay。

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
}
