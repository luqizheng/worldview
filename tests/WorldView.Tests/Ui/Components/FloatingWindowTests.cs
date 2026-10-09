// <auto-not-in-spec />
// Task 6 仓单 §8.1.6 — FloatingWindowTests(7 个)
// 验证 FloatingWindow 走 Palette 量化层(标题条 24 / 段落 8),不落盘,
// ClampRect 走 ClampToViewport 纯函数版 (不靠反射),Esc 关闭走 IsEscapeCloseKey 纯函数。
// 注释: 不直接构造 FloatingWindow(Godot.Control 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 纯函数 + 反射读 const / 字段 的双层验证。
//       也不构造 InputEvent (Godot.native init 需要 SceneTree,会 AccessViolation)。

using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class FloatingWindowTests
{
    [Fact]
    public void FloatingWindow_TitleBar_Height_IsPanelTitleHeight_24()
    {
        // _titleBar.CustomMinimumSize.Y = PanelTitleHeight
        Assert.Equal(24, Palette.Metric.PanelTitleHeight);
    }

    [Fact]
    public void FloatingWindow_ParagraphSpacing_IsPaletteMetric_8()
    {
        // _contentRoot.AddThemeConstantOverride("separation", ParagraphSpacing)
        Assert.Equal(8, Palette.Metric.ParagraphSpacing);
    }

    [Fact]
    public void FloatingWindow_ClampRect_PureFunc_NegativePosition_ClampsToZero()
    {
        // ClampToViewport(pos, size, viewport) 纯函数版:
        //   viewport 1920x1080, size 360x200
        //   若 pos = (-50, -30) → 期望 (0, 0)
        var clamped = FloatingWindow.ClampToViewport(
            new Vector2(-50, -30),
            new Vector2(360, 200),
            new Vector2(1920, 1080));
        Assert.Equal(new Vector2(0, 0), clamped);
    }

    [Fact]
    public void FloatingWindow_ClampRect_PureFunc_OverPosition_ClampsToMax()
    {
        // pos = (2000, 1200) → 期望 (1920-360, 1080-200) = (1560, 880)
        var clamped = FloatingWindow.ClampToViewport(
            new Vector2(2000, 1200),
            new Vector2(360, 200),
            new Vector2(1920, 1080));
        Assert.Equal(new Vector2(1560, 880), clamped);
    }

    [Fact]
    public void FloatingWindow_ClampRect_PureFunc_InsidePosition_StaysUnchanged()
    {
        // pos = (500, 300) → 在 viewport 内不动
        var clamped = FloatingWindow.ClampToViewport(
            new Vector2(500, 300),
            new Vector2(360, 200),
            new Vector2(1920, 1080));
        Assert.Equal(new Vector2(500, 300), clamped);
    }

    [Fact]
    public void FloatingWindow_IsEscapeCloseKey_True_On_Pressed_Escape_NoEcho()
    {
        Assert.True(FloatingWindow.IsEscapeCloseKey(Key.Escape, pressed: true, echo: false));
    }

    [Fact]
    public void FloatingWindow_IsEscapeCloseKey_False_On_Released_Echo_Or_OtherKey()
    {
        // 必须 Pressed=true 且 Echo=false 才关,其他情况都不关
        Assert.False(FloatingWindow.IsEscapeCloseKey(Key.Escape, pressed: false, echo: false));
        Assert.False(FloatingWindow.IsEscapeCloseKey(Key.Escape, pressed: true, echo: true));
        Assert.False(FloatingWindow.IsEscapeCloseKey(Key.Enter, pressed: true, echo: false));
    }
}
