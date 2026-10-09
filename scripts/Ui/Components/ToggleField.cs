using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 开关 Field（§4.5.2 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>) + 右 <see cref="CheckBox"/>。
/// 必填校验: 始终通过(spec 默认勾选已确认即合法)。</para>
/// </summary>
public partial class ToggleField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly JrpgCheckBox _check = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _check.ButtonPressed ? "true" : "false";
        set => _check.ButtonPressed = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override bool IsValid => true;

    /// <summary>语义化:设置是否勾选(bool 版本)。</summary>
    public ToggleField SetChecked(bool checked_)
    {
        _check.ButtonPressed = checked_;
        return this;
    }

    /// <inheritdoc/>
    public ToggleField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_label);
        AddChild(_check);
    }

    /// <summary>语义化:设置标签文本。</summary>
    public ToggleField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置初始值。</summary>
    public ToggleField SetValue(string value)
    {
        Value = value;
        return this;
    }
}
