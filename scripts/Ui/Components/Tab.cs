using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 横向标签栏（§4.5 spec 契约 A2 — Tab）
///
/// <para>通用可复用横向标签栏,Item 可点击切换 active。供 LayerPanel / Inspector 子面板 / 设置面板切换用。</para>
///
/// <para>继承 <see cref="HBoxContainer"/>(横向容器)。每项一个 <see cref="TabItem"/>
/// (PanelContainer + 下边线),active 项走 <c>Palette.Color.JrpgAccent</c>,inactive 走 <c>JrpgTextPlaceholder</c>。</para>
///
/// <para>token 接入:高度 24 / 字号 14 / 内边距 8 / 文本色 / 边框 / 背景。</para>
/// </summary>
public partial class Tab : HBoxContainer
{
    [Signal] public delegate void TabChangedEventHandler(int index);

    private readonly List<TabItem> _items = new();
    private int _activeIndex = -1;

    /// <summary>当前 active 项 (0-based, -1 表示无)。</summary>
    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            if (value == _activeIndex) return;
            if (value < 0 || value >= _items.Count) return;
            _activeIndex = value;
            RefreshAll();
            EmitSignal(SignalName.TabChanged, value);
        }
    }

    /// <summary>当前 Item 数。</summary>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public Tab()
    {
        AddThemeConstantOverride("separation", 0);
    }

    /// <summary>添加 Tab 项(可选 icon 形态)。</summary>
    public Tab AddTab(string label, JrpgIcon.Shape? icon = null)
    {
        var item = new TabItem(label, icon, _items.Count);
        item.InnerButton.Pressed += () => ActiveIndex = item.Index;
        _items.Add(item);
        AddChild(item);
        if (_activeIndex < 0) ActiveIndex = 0; // 第一项默认 active
        return this;
    }

    /// <summary>移除 Tab 项(超出索引则忽略)。</summary>
    public Tab RemoveTab(int index)
    {
        if (index < 0 || index >= _items.Count) return this;
        var item = _items[index];
        _items.RemoveAt(index);
        item.QueueFree();
        if (_activeIndex >= _items.Count) _activeIndex = _items.Count - 1;
        RefreshAll();
        return this;
    }

    private void RefreshAll()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].SetActive(i == _activeIndex);
        }
    }

    /// <summary>内部:Tab 项 (PanelContainer 子类)。</summary>
    private partial class TabItem : PanelContainer
    {
        public int Index { get; }
        private readonly Button _btn = new();
        private readonly ColorRect _underline = new();

        public TabItem(string label, JrpgIcon.Shape? icon, int index)
        {
            Index = index;
            CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.PanelTitleHeight);
            _btn.Text = label;
            _btn.Flat = true;
            _btn.AnchorRight = 1.0f;
            _btn.AnchorBottom = 1.0f;
            _btn.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeBody);
            AddChild(_btn);

            // 下边线
            _underline.MouseFilter = MouseFilterEnum.Ignore;
            _underline.AnchorTop = 1.0f;
            _underline.AnchorRight = 1.0f;
            _underline.OffsetTop = -2;
            _underline.CustomMinimumSize = new Vector2(0, 2);
            AddChild(_underline);

            SetActive(false);
        }

        /// <summary>外部设置 active 态(active=true 走 JrpgLink, false 走 JrpgTextPlaceholder)。</summary>
        public void SetActive(bool active)
        {
            _underline.Color = active
                ? WorldView.Ui.Theme.Palette.Color.JrpgLink
                : WorldView.Ui.Theme.Palette.Color.JrpgOutline;
            _btn.Modulate = active
                ? WorldView.Ui.Theme.Palette.Color.JrpgLink
                : WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder;
        }

        public Button InnerButton => _btn;
    }
}
