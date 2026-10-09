using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 文本输入 Field（§4.5.2 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>=100) + 右 <see cref="JrpgTextBox"/>
/// 共享一行高 <c>TextboxHeight</c>(28)。</para>
///
/// <para>必填校验: <see cref="IsRequired"/> = true 时,空白阻止 commit
/// (在 <see cref="Form"/> 父节点调 <c>Validate</c> 时触发)。</para>
/// </summary>
public partial class TextField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly JrpgTextBox _box = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _box.Text;
        set => _box.Text = value;
    }

    /// <summary>是否必填(空白时 <see cref="IsValid"/> 返 false)。</summary>
    public bool IsRequired { get; set; }

    /// <inheritdoc/>
    public override bool IsValid => !IsRequired || !string.IsNullOrWhiteSpace(_box.Text);

    /// <inheritdoc/>
    public TextField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;
        _box.CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.TextboxHeight);
        _box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_label);
        AddChild(_box);
    }

    /// <summary>语义化:设置标签文本。</summary>
    public TextField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置初始值。</summary>
    public TextField SetValue(string value)
    {
        Value = value;
        return this;
    }

    /// <summary>语义化:标记必填。</summary>
    public TextField Required()
    {
        IsRequired = true;
        _label.Text += " *";
        return this;
    }
}
