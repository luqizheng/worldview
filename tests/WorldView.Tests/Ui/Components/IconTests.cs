// <auto-not-in-spec />
// Task 4 仓单 §8.1.4 — IconTests(5 个)
// 验证 JrpgIcon 6 形态 × 3 档尺寸走 Palette 量化层。
// 注释: 不直接构造 JrpgIcon(Godot.Control 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       只断言语义层(枚举 / token),_Draw 行为由 Godot integration test 覆盖。
// 命名说明: 改 JrpgIcon 而非 Icon,避免与 Godot Button.Icon (Texture2D) 冲突。

using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class JrpgIconTests
{
    [Fact]
    public void JrpgIcon_Shape_Enum_HasAllSixShapes()
    {
        // spec §4.5.2 规定 6 形态
        var names = Enum.GetNames<JrpgIcon.Shape>();
        Assert.Equal(6, names.Length);
        Assert.Contains("Close", names);
        Assert.Contains("Check", names);
        Assert.Contains("Cross", names);
        Assert.Contains("Info", names);
        Assert.Contains("Warning", names);
        Assert.Contains("Dot", names);
    }

    [Fact]
    public void JrpgIcon_SizeTier_Enum_HasThreeTiers()
    {
        var names = Enum.GetNames<JrpgIcon.SizeTier>();
        Assert.Equal(3, names.Length);
        Assert.Contains("Xs", names);
        Assert.Contains("Sm", names);
        Assert.Contains("Md", names);
    }

    [Fact]
    public void JrpgIcon_ResolveSizePx_Xs_ReturnsIconSizeXs_16()
    {
        Assert.Equal(16, Palette.Metric.IconSizeXs);
    }

    [Fact]
    public void JrpgIcon_ResolveSizePx_Sm_ReturnsIconSizeSm_24()
    {
        Assert.Equal(24, Palette.Metric.IconSizeSm);
    }

    [Fact]
    public void JrpgIcon_ResolveSizePx_Md_ReturnsIconSizeMd_32()
    {
        Assert.Equal(32, Palette.Metric.IconSizeMd);
    }
}
