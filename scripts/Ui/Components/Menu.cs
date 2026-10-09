using System;
using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 下拉菜单（§4.2 spec 契约 A2 — Menu）
///
/// <para>继承 <see cref="PanelContainer"/> 而非 <see cref="Popup"/>,避免走 OS 主题
/// (沿用 Dialog 改 PanelContainer 经验)。手动按 viewport 居中或挂触发位置。</para>
///
/// <para>用法: <c>menu.AddItem("打开", () => DoOpen()); menu.AddItem("保存", () => DoSave()); menu.AddSeparator();</c>
/// 点击 Item 触发 <see cref="ItemSelected"/> 信号 + 自动 <see cref="Hide"/>。</para>
///
/// <para>token 接入:行高 28 / 字号 14 / 边框 JrpgBorder / 背景 JrpgBg / 悬停 JrpgFillHover / 文字 JrpgText / 分隔条 JrpgBorderSubtle。</para>
/// </summary>
public partial class Menu : PanelContainer
{
    [Signal] public delegate void ItemSelectedEventHandler(int index);

    private readonly VBoxContainer _root = new();
    private readonly List<MenuItem> _items = new();

    /// <inheritdoc/>
    public Menu()
    {
        // 走 token,不走 OS Popup 主题
        _root.AddThemeConstantOverride("separation", 0);
        _root.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_root);
    }

    /// <summary>当前 Item 数(含 separator)。</summary>
    public int Count => _items.Count;

    /// <summary>添加菜单项(可选 icon 形态)。</summary>
    public Menu AddItem(string label, Action onSelect, JrpgIcon.Shape? icon = null)
    {
        var item = new MenuItem(label, icon, _items.Count, isSeparator: false);
        item.InnerButton.Pressed += () =>
        {
            EmitSignal(SignalName.ItemSelected, item.Index);
            onSelect?.Invoke();
            Hide();
        };
        _items.Add(item);
        _root.AddChild(item);
        return this;
    }

    /// <summary>添加分隔条(走 Palette.Color.JrpgOutline)。</summary>
    public Menu AddSeparator()
    {
        var sep = new MenuItem(string.Empty, icon: null, _items.Count, isSeparator: true);
        _items.Add(sep);
        _root.AddChild(sep);
        return this;
    }

    /// <summary>在指定坐标弹出(绝对屏幕坐标)。</summary>
    public void PopupAt(Vector2 screenPosition)
    {
        Position = screenPosition;
        Show();
    }

    /// <summary>在 host 中央弹出。</summary>
    public void PopupCentered(Control host)
    {
        var hostSize = host.Size;
        var menuSize = new Vector2(160, _items.Count * WorldView.Ui.Theme.Palette.Metric.ListRowHeight + 8);
        Position = host.GlobalPosition + (hostSize - menuSize) * 0.5f;
        Show();
    }

    /// <summary>内部:菜单项 (Button 子类 或 分隔条 Panel)。</summary>
    private partial class MenuItem : PanelContainer
    {
        private readonly bool _isSeparator;
        private readonly Button _btn = new();

        public int Index { get; }

        /// <summary>menu item 内部 button,测试 / 父级菜单可以挂信号。</summary>
        public Button InnerButton => _btn;

        public MenuItem(string label, JrpgIcon.Shape? icon, int index, bool isSeparator)
        {
            Index = index;
            _isSeparator = isSeparator;
            CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.ListRowHeight);

            if (isSeparator)
            {
                // 1px 高分隔条,背景色 = JrpgOutline
                CustomMinimumSize = new Vector2(0, 1);
                // 用 ColorRect 填充背景
                var rect = new ColorRect
                {
                    Color = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                rect.AnchorRight = 1.0f;
                rect.AnchorBottom = 1.0f;
                AddChild(rect);
            }
            else
            {
                _btn.Text = label;
                _btn.Flat = true;
                _btn.MouseFilter = MouseFilterEnum.Stop;
                _btn.AnchorRight = 1.0f;
                _btn.AnchorBottom = 1.0f;
                _btn.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeBody);
                AddChild(_btn);
            }
        }
    }
}
