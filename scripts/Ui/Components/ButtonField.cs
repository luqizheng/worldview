using System;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 按钮 Field（§4.5.2 契约 Form 行 / 行动作触发）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>) + 右 <see cref="JrpgButton"/>
/// (固定宽 <c>FormButtonWidth</c>=96)。
/// 不参与 <c>Form.Validate</c>(IsValid 永远 true)。</para>
/// </summary>
public partial class ButtonField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly JrpgButton _button = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>按钮文本。</summary>
    public string ButtonText
    {
        get => _button.Text;
        set => _button.Text = value;
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _button.Text;
        set => _button.Text = value;
    }

    /// <inheritdoc/>
    public override bool IsValid => true;

    /// <inheritdoc/>
    public ButtonField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        _button.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormButtonWidth, 0);
        AddChild(_label);
        AddChild(_button);
    }

    /// <summary>语义化:设置标签文本。</summary>
    public ButtonField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置按钮文本。</summary>
    public ButtonField SetButtonText(string text)
    {
        ButtonText = text;
        return this;
    }

    /// <summary>语义化:绑定按钮点击回调。</summary>
    public ButtonField OnClick(Action handler)
    {
        _button.Pressed += () => handler();
        return this;
    }
}
