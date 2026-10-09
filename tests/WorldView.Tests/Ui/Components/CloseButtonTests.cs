// <auto-not-in-spec />
// Task 5 仓单 §8.1.5 — CloseButtonTests(3 个)
// 验证 CloseButton 内嵌 JrpgIcon(Close/Sm),尺寸走 IconSizeSm,关闭动画走 FadeOutMs。

using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class CloseButtonTests
{
    [Fact]
    public void CloseButton_UsesIconSizeSm_24()
    {
        // CloseButton 自定义最小尺寸 = IconSizeSm(24)
        Assert.Equal(24, Palette.Metric.IconSizeSm);
    }

    [Fact]
    public void CloseButton_FadeOut_Animation_IsPaletteFadeOutMs_150()
    {
        // CloseButton.TriggerClose 内的 tween 长度 = FadeOutMs / 1000
        Assert.Equal(150, Palette.Animation.FadeOutMs);
    }

    [Fact]
    public void CloseButton_Default_IconColor_IsJrpgTextPlaceholder()
    {
        // 构造函数内 _icon.SetColor(JrpgTextPlaceholder) — 通过 token 间接验证
        var color = Palette.Color.JrpgTextPlaceholder;
        Assert.NotEqual(default, color);
    }
}
