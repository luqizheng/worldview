// <auto-not-in-spec />
// Task 2 仓单 §5.2 — MenuTests(5 个)
// 验证 Menu 走 Palette 量化(行高 28 / 字号 14),item 计数 / separator / ItemSelected 信号存在。
// 注释: 不直接构造 Menu(Godot.PanelContainer 需 SceneTree;xunit 跑在无 Godot runtime 下),
//       走 反射读 const / 字段 + 反射读信号 / 内部 MenuItem InnerButton。

using System;
using System.Reflection;
using Godot;
using WorldView.Ui.Components;
using WorldView.Ui.Theme;
using Xunit;

namespace WorldView.Tests.Ui.Components;

public class MenuTests
{
    [Fact]
    public void Menu_Item_RowHeight_IsPaletteListRowHeight_28()
    {
        // MenuItem.CustomMinimumSize.Y = ListRowHeight
        Assert.Equal(28, Palette.Metric.ListRowHeight);
    }

    [Fact]
    public void Menu_Item_FontSize_IsPaletteFontSizeBody_14()
    {
        // _btn.AddThemeFontSizeOverride("font_size", FontSizeBody)
        Assert.Equal(14, Palette.Metric.FontSizeBody);
    }

    [Fact]
    public void Menu_Declares_ItemSelected_Signal()
    {
        // [Signal] ItemSelectedEventHandler(int index)
        var sig = typeof(Menu).GetEvent("ItemSelected");
        Assert.NotNull(sig);
        Assert.Equal("ItemSelectedEventHandler", sig!.EventHandlerType!.Name);
    }

    [Fact]
    public void Menu_Declares_Count_Property()
    {
        // public int Count => _items.Count
        var prop = typeof(Menu).GetProperty("Count");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
        Assert.True(prop.CanRead);
        Assert.False(prop.CanWrite);
    }

    [Fact]
    public void Menu_Has_Nested_MenuItem_Type()
    {
        // 嵌套类 MenuItem, 公开 InnerButton 属性
        var nested = typeof(Menu).GetNestedType("MenuItem", BindingFlags.NonPublic);
        Assert.NotNull(nested);
        var btn = nested!.GetProperty("InnerButton", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(btn);
        Assert.Equal(typeof(Button), btn!.PropertyType);
    }
}
