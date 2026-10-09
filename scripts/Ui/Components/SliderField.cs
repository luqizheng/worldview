using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 数值滑块 Field（§4.5.5 A4 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>=100) +
/// 右 <c>HBoxContainer</c>( <c>JrpgLabel value</c> + <c>HSlider</c> + <c>JrpgLabel min..max</c> )。</para>
///
/// <para>校验: <see cref="Value"/> 必在 <c>[<see cref="Min"/>, <see cref="Max"/>]</c> 区间内。</para>
/// </summary>
public partial class SliderField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly JrpgLabel _valueLabel = new();
    private readonly HSlider _slider = new();
    private readonly JrpgLabel _rangeLabel = new();
    private readonly HBoxContainer _rightBox = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>滑块最小值(含)。默认 0。</summary>
    public int Min { get; set; }

    /// <summary>滑块最大值(含)。默认 100。</summary>
    public int Max { get; set; } = 100;

    /// <summary>步长。默认 1。</summary>
    public int Step { get; set; } = 1;

    /// <inheritdoc/>
    public override string Value
    {
        get => _valueLabel.Text;
        set
        {
            if (int.TryParse(value, out var v))
                SetValueInternal(v, false);
        }
    }

    /// <summary>滑块当前值。setter 同步 HSlider + 标签 + 触发 <see cref="ValueChanged"/>。</summary>
    public int SliderValue
    {
        get => (int)Math.Round(_slider.Value);
        set => SetValueInternal(value, false);
    }

    /// <inheritdoc/>
    public override bool IsValid => SliderValue >= Min && SliderValue <= Max;

    /// <summary>滑块拖动时实时发射。</summary>
    [Signal] public delegate void ValueChangedEventHandler(int value);

    /// <inheritdoc/>
    public SliderField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;

        _rightBox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInlineButton);
        _valueLabel.CustomMinimumSize = new Vector2(40, 0);
        _valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _valueLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);

        _slider.MinValue = Min;
        _slider.MaxValue = Max;
        _slider.Step = Step;
        _slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _slider.ValueChanged += OnSliderChanged;

        _rangeLabel.Text = $"{Min}..{Max}";
        _rangeLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder);
        _rangeLabel.CustomMinimumSize = new Vector2(80, 0);

        _rightBox.AddChild(_valueLabel);
        _rightBox.AddChild(_slider);
        _rightBox.AddChild(_rangeLabel);

        AddChild(_label);
        AddChild(_rightBox);
    }

    private void OnSliderChanged(double value)
    {
        var v = (int)Math.Round(value);
        _valueLabel.Text = v.ToString();
        EmitSignal(SignalName.ValueChanged, v);
    }

    private void SetValueInternal(int v, bool emit)
    {
        _slider.Value = v;
        _valueLabel.Text = v.ToString();
        if (emit)
            EmitSignal(SignalName.ValueChanged, v);
    }

    /// <summary>语义化:设置标签。</summary>
    public SliderField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置滑块范围 + 步长。</summary>
    public SliderField SetRange(int min, int max, int step = 1)
    {
        Min = min;
        Max = max;
        Step = Math.Max(1, step);
        _slider.MinValue = Min;
        _slider.MaxValue = Max;
        _slider.Step = Step;
        _rangeLabel.Text = $"{Min}..{Max}";
        return this;
    }

    /// <summary>语义化:设置初始值。</summary>
    public SliderField SetValue(int value)
    {
        SliderValue = value;
        return this;
    }
}
