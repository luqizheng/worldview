using Godot;
using System.Collections.Generic;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 通用选择控件合集（§4.5.2 契约 CheckBox + DropDown）
///
/// <para>CheckBox：16x16 方框，2px 焦褐描边（圆角 0），
/// unchecked 空 / checked #e8c34a + 1px 黑色对号。</para>
///
/// <para>DropDown：封装 Godot <see cref="OptionButton"/>，
/// 高 28px，2px 焦褐描边（焦点态替换为金黄），右侧 16x16 ▼ 焦褐箭头。
/// 展开列表**不**复用 JrpgList（§4.5.5 #13 拒绝），由 OptionButton 内置 Popup 承担。</para>
/// </summary>
public partial class JrpgCheckBox : CheckBox
{
    /// <summary>JRPG 复选框字号。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>状态变化事件。</summary>
    [Signal] public delegate void ToggledEventHandler(bool pressed);

    /// <summary>JRPG 风格：自动加载 theme。</summary>
    public JrpgCheckBox()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置标签。</summary>
    public JrpgCheckBox SetLabel(string text)
    {
        Text = text;
        return this;
    }

    /// <summary>语义化：设置选中状态。</summary>
    public JrpgCheckBox SetChecked(bool checkedState)
    {
        ButtonPressed = checkedState;
        return this;
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
        AddThemeFontSizeOverride("font_size", DefaultFontSize);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
        Toggled += pressed =>
        {
            EmitSignal(SignalName.Toggled, pressed);
        };
    }
}

/// <summary>
/// JRPG 风格下拉框（§4.5.2 契约 DropDown 行）
/// </summary>
public partial class JrpgDropDown : OptionButton
{
    /// <summary>JRPG 下拉框高度。</summary>
    public const int DefaultHeight = 28;

    /// <summary>JRPG 下拉框字号。</summary>
    public const int DefaultFontSize = 14;

    /// <summary>选择变化事件。</summary>
    [Signal] public delegate void SelectionChangedEventHandler(int index);

    private readonly List<string> _items = new();

    /// <summary>JRPG 风格：自动加载 theme。</summary>
    public JrpgDropDown()
    {
        ApplyJrpgTheme();
    }

    /// <summary>语义化：设置选项（替换全部）。</summary>
    public JrpgDropDown SetOptions(IEnumerable<string> options)
    {
        Clear();
        _items.Clear();
        var idx = 0;
        foreach (var opt in options)
        {
            AddItem(opt);
            _items.Add(opt);
            idx++;
        }
        if (idx > 0) Selected = 0;
        return this;
    }

    /// <summary>语义化：按索引选中。</summary>
    public new JrpgDropDown Select(int index)
    {
        if (index >= 0 && index < ItemCount) Selected = index;
        return this;
    }

    /// <summary>当前选中索引。</summary>
    public int SelectedIndex => Selected;

    /// <summary>当前选中文本。</summary>
    public string? SelectedText
    {
        get
        {
            var idx = Selected;
            return idx >= 0 && idx < _items.Count ? _items[idx] : null;
        }
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
        CustomMinimumSize = new Vector2(0, DefaultHeight);
        AddThemeFontSizeOverride("font_size", DefaultFontSize);
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
        ItemSelected += idx => EmitSignal(SignalName.SelectionChanged, idx);
    }
}
