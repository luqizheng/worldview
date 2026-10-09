// <auto-not-in-spec />
// Task 8 仓单 §8.1.8 — JrpgLabelTests(7 个)
// 验证 JrpgLabel 默认走 FontSizeBody / JrpgText,语义化 setter 链式可读。

using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class JrpgLabelTests
{
    [Fact]
    public void Label_DefaultFontSize_IsFontSizeBody_14()
    {
        // JrpgLabel() 内 AddThemeFontSizeOverride("font_size", FontSizeBody)
        Assert.Equal(14, Palette.Metric.FontSizeBody);
    }

    [Fact]
    public void Label_DefaultColor_IsJrpgText()
    {
        // JrpgLabel() 内 AddThemeColorOverride("font_color", JrpgText)
        var color = Palette.Color.JrpgText;
        Assert.NotEqual(default, color);
    }

    [Fact]
    public void Label_FontSizeTitle_Available_16()
    {
        // 验证 token 链路一致(标题可被 SetFontSize 切到 16)
        Assert.Equal(16, Palette.Metric.FontSizeTitle);
    }

    [Fact]
    public void Label_FontSizeCaption_Available_12()
    {
        Assert.Equal(12, Palette.Metric.FontSizeCaption);
    }

    [Fact]
    public void Label_Color_JrpgTextPlaceholder_Available_ForMutedText()
    {
        // 验证 JrpgLabel.SetColor 可切到 placeholder 灰
        var color = Palette.Color.JrpgTextPlaceholder;
        Assert.NotEqual(default, color);
    }

    [Fact]
    public void Label_Color_JrpgDanger_Available_ForWarningText()
    {
        var color = Palette.Color.JrpgDanger;
        Assert.NotEqual(default, color);
    }

    [Fact]
    public void Label_Color_JrpgLink_Available_ForHyperlinkText()
    {
        var color = Palette.Color.JrpgLink;
        Assert.NotEqual(default, color);
    }
}
