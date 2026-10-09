using System.Collections.Generic;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 枚举 Field（§4.5.2 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>) + 右 <see cref="JrpgDropDown"/>。
/// 必填校验: 始终通过(下拉框始终有选中项)。</para>
/// </summary>
public partial class EnumField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly JrpgDropDown _drop = new();
    private readonly List<string> _options = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>当前选中索引。</summary>
    public int SelectedIndex
    {
        get => _drop.Selected;
        set => _drop.Selected = value;
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _drop.Selected >= 0 && _drop.Selected < _options.Count
            ? _options[_drop.Selected]
            : string.Empty;
        set
        {
            var idx = _options.IndexOf(value);
            if (idx >= 0) _drop.Selected = idx;
        }
    }

    /// <inheritdoc/>
    public override bool IsValid => true;

    /// <inheritdoc/>
    public EnumField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        _drop.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_label);
        AddChild(_drop);
    }

    /// <summary>语义化:设置标签文本。</summary>
    public EnumField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:填入枚举项。</summary>
    public EnumField SetOptions(IReadOnlyList<string> options)
    {
        _options.Clear();
        _drop.Clear();
        foreach (var opt in options)
        {
            _options.Add(opt);
            _drop.AddItem(opt);
        }
        return this;
    }

    /// <summary>语义化:设置初始选中索引。</summary>
    public EnumField SetSelectedIndex(int index)
    {
        SelectedIndex = index;
        return this;
    }
}
