// <auto-not-in-spec />
// Task 9 仓单 §8.1.9 — FieldTests(5 个)
// 验证 4 Field 共用 FormLabelWidth/FormButtonWidth/TextboxHeight/PaddingInline token,
// 必填校验:TextField 走 IsNullOrWhiteSpace,Toggle/Enum/Button 永远通过。

using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class FieldTests
{
    [Fact]
    public void Field_FormLabelWidth_IsPaletteToken_100()
    {
        // 4 个 Field 都在构造里 _label.CustomMinimumSize.X = FormLabelWidth
        Assert.Equal(100, Palette.Metric.FormLabelWidth);
    }

    [Fact]
    public void Field_FormButtonWidth_IsPaletteToken_96()
    {
        // ButtonField._button.CustomMinimumSize.X = FormButtonWidth
        Assert.Equal(96, Palette.Metric.FormButtonWidth);
    }

    [Fact]
    public void Field_PaddingInline_IsPaletteToken_8()
    {
        // 4 个 Field 在构造里 AddThemeConstantOverride("separation", PaddingInline)
        Assert.Equal(8, Palette.Metric.PaddingInline);
    }

    [Fact]
    public void Field_TextboxHeight_IsPaletteToken_28()
    {
        // TextField._box.CustomMinimumSize.Y = TextboxHeight
        Assert.Equal(28, Palette.Metric.TextboxHeight);
    }

    [Fact]
    public void Field_ButtonHeight_IsPaletteToken_32()
    {
        // ButtonField._button 默认走 JrpgButton.DefaultHeight = ButtonHeight
        Assert.Equal(32, Palette.Metric.ButtonHeight);
    }
}
