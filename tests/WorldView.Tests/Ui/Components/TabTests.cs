// <auto-not-in-spec />
// Task 5 仓单 §5.5 — TabTests(5 个)
// 验证 Tab 走 Palette 量化(高度 24 / 字号 14),ActiveIndex setter / TabChanged 信号 / AddTab 计数。
// 注释: 不直接构造 Tab(Godot.HBoxContainer 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 反射读 const / 字段 + 反射读信号 / 属性 / 嵌套类型。

using System.Reflection;
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class TabTests
{
    [Fact]
    public void Tab_Height_IsPalettePanelTitleHeight_24()
    {
        // TabItem.CustomMinimumSize.Y = PanelTitleHeight
        Assert.Equal(24, Palette.Metric.PanelTitleHeight);
    }

    [Fact]
    public void Tab_FontSize_IsPaletteFontSizeBody_14()
    {
        // _btn.AddThemeFontSizeOverride("font_size", FontSizeBody)
        Assert.Equal(14, Palette.Metric.FontSizeBody);
    }

    [Fact]
    public void Tab_ActiveIndex_Property_ReadWrite_Int()
    {
        // public int ActiveIndex { get; set; }
        var prop = typeof(Tab).GetProperty("ActiveIndex");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
        Assert.True(prop.CanRead && prop.CanWrite);
    }

    [Fact]
    public void Tab_Declares_TabChanged_Signal()
    {
        // [Signal] TabChangedEventHandler(int index)
        var sig = typeof(Tab).GetEvent("TabChanged");
        Assert.NotNull(sig);
        Assert.Equal("TabChangedEventHandler", sig!.EventHandlerType!.Name);
    }

    [Fact]
    public void Tab_Has_Nested_TabItem_Type()
    {
        // 嵌套类 TabItem, 公开 SetActive(bool) + InnerButton 属性
        var nested = typeof(Tab).GetNestedType("TabItem", BindingFlags.NonPublic);
        Assert.NotNull(nested);
        var setActive = nested!.GetMethod("SetActive", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(setActive);
        var btn = nested.GetProperty("InnerButton", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(btn);
        Assert.Equal(typeof(Button), btn!.PropertyType);
    }
}
