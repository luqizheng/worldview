using Godot;
using System.Collections.Generic;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用列表（§4.5.2 契约 List 行）
///
/// <para>内部封装 Godot <see cref="ItemList"/>，自动应用 jrpg theme，
/// 暴露 items 列表化 API。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>行高 28px，圆角 0，选中行填 #e8c89a + 2px 金黄描边</item>
///   <item>行间分隔 1px 焦褐 30% 透明</item>
///   <item>字号 14px 衬线，左对齐 + 8px 内边距</item>
///   <item>滚动条 8px 宽，thumb #d4c4a1，track 羊皮纸底</item>
/// </list>
///
/// <para>3 状态：normal / selected / focus（由 ItemList 内置驱动）</para>
/// </summary>
/// <remarks>
/// 拒绝清单（§4.5.5 #13）：DropDown 展开列表**不**走 JrpgList，独立组件 JrpgDropDown。
/// </remarks>
public partial class JrpgList : ItemList
{
    /// <summary>JRPG 列表行高。</summary>
    public const int RowHeight = 28;

    /// <summary>JRPG 列表字号。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>选中事件（语义化包装）。</summary>
    [Signal] public delegate void ItemSelectedEventHandler(int index);

    private readonly List<string> _items = new();

    /// <summary>JRPG 风格：自动加载 theme 并设置默认样式。</summary>
    public JrpgList()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置条目（替换全部）。</summary>
    public JrpgList SetItems(IEnumerable<string> items)
    {
        Clear();
        _items.Clear();
        foreach (var text in items)
        {
            AddItem(text);
            _items.Add(text);
        }
        return this;
    }

    /// <summary>语义化：获取当前选中索引（-1 表示无）。</summary>
    public int SelectedIndex
    {
        get
        {
            var indices = GetSelectedItems();
            return indices.Length > 0 ? indices[0] : -1;
        }
    }

    /// <summary>语义化：获取当前选中文本。</summary>
    public string? SelectedText
    {
        get
        {
            var idx = SelectedIndex;
            return idx >= 0 && idx < _items.Count ? _items[idx] : null;
        }
    }

    /// <summary>语义化：按索引选中。</summary>
    public new JrpgList Select(int index)
    {
        if (index >= 0 && index < ItemCount)
        {
            base.Select(index);
            // Godot 4: ItemList 没有 EnsureCurrentVisible,通过 ScrollContainer 父级滚动
            // 滚动逻辑由父容器负责
        }
        return this;
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
        AddThemeFontSizeOverride("font_size", DefaultFontSize);
        // 行高通过 fixed_icon_size 控制最小尺寸（Godot ItemList 用 size override 而非 line height）
        // 实际行高由 font_size + content_margin 共同决定
        FixedIconSize = new Vector2I(0, RowHeight - DefaultFontSize - 8);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
        ItemSelected += idx => EmitSignal(SignalName.ItemSelected, idx);
    }
}
