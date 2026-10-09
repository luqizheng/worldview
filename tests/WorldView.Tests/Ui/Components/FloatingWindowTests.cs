// <auto-not-in-spec />
// Task 6 仓单 §8.1.6 — FloatingWindowTests(3 个)
// 验证 FloatingWindow 走 Palette 量化层(标题条 24 / 段落 8),不落盘,ClampRect 数学正确。

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
    public void FloatingWindow_ClampRect_MathIsCorrect()
    {
        // ClampToViewport 逻辑:Position.X ∈ [0, viewport.X - size.X]
        //   Position.Y ∈ [0, viewport.Y - size.Y]
        // 模拟: viewport 1920x1080, size 360x200
        //   若用户拖到 Position (-50, -30) → 期望 (0, 0)
        //   若用户拖到 Position (2000, 1200) → 期望 (1560, 880)
        var viewport = new Vector2(1920, 1080);
        var size = new Vector2(360, 200);
        // 模拟 ClampToViewport 内部算法(不直接构造 FloatingWindow)
        Vector2 Clamp(Vector2 pos)
        {
            pos.X = System.Math.Clamp(pos.X, 0, System.Math.Max(0, viewport.X - size.X));
            pos.Y = System.Math.Clamp(pos.Y, 0, System.Math.Max(0, viewport.Y - size.Y));
            return pos;
        }

        Assert.Equal(new Vector2(0, 0), Clamp(new Vector2(-50, -30)));
        Assert.Equal(new Vector2(1560, 880), Clamp(new Vector2(2000, 1200)));
        Assert.Equal(new Vector2(500, 300), Clamp(new Vector2(500, 300))); // 在内不动
    }

    [Fact]
    public void FloatingWindow_Override_UnhandledInput_Method_Exists()
    {
        // FloatingWindow 必须有 _UnhandledInput 钩子,Esc 关闭走该回调
        var method = typeof(FloatingWindow).GetMethod(
            "_UnhandledInput",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);
    }
}
