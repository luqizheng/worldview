using System;
using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 日期选择 Field（§4.5.5 A4 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>=100) +
/// 右 <c>HBoxContainer</c>( <c>JrpgLabel date</c> + <c>JrpgButton "‹"</c> + <c>JrpgButton "›"</c> )。</para>
///
/// <para>prev/next 按 1 天步进;越界不更新。校验: <see cref="Value"/> 必在
/// <c>[<see cref="Min"/>, <see cref="Max"/>]</c> 区间内。</para>
/// </summary>
public partial class DateField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly HBoxContainer _rightBox = new();
    private readonly JrpgLabel _dateLabel = new();
    private readonly JrpgButton _prevBtn = new();
    private readonly JrpgButton _nextBtn = new();

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>最小日期(含)。默认 10 年前。</summary>
    public DateOnly Min { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-10));

    /// <summary>最大日期(含)。默认 10 年后。</summary>
    public DateOnly Max { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(10));

    /// <summary>当前日期。setter 同步 date 标签 + 触发 <see cref="DateChanged"/>。</summary>
    public DateOnly Date
    {
        get => _value;
        set => SetDateInternal(value, true);
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _dateLabel.Text;
        set
        {
            if (DateOnly.TryParse(value, out var d))
                SetDateInternal(d, true);
        }
    }

    /// <inheritdoc/>
    public override bool IsValid => Date >= Min && Date <= Max;

    /// <summary>日期变化时发射(prev/next/SetDate)。signal 参数是 ISO 字符串(Godot 不支持 DateOnly)。</summary>
    [Signal] public delegate void DateChangedEventHandler(string date);

    private DateOnly _value = DateOnly.FromDateTime(DateTime.Today);

    /// <inheritdoc/>
    public DateField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;

        _rightBox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInlineButton);

        _dateLabel.Text = ToIso(_value);
        _dateLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        _dateLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _prevBtn.Text = "\u2039"; // ‹
        _prevBtn.Pressed += () => StepByDays(-1);

        _nextBtn.Text = "\u203A"; // ›
        _nextBtn.Pressed += () => StepByDays(1);

        _rightBox.AddChild(_dateLabel);
        _rightBox.AddChild(_prevBtn);
        _rightBox.AddChild(_nextBtn);

        AddChild(_label);
        AddChild(_rightBox);
    }

    private void StepByDays(int days)
    {
        var candidate = _value.AddDays(days);
        if (candidate < Min || candidate > Max)
            return;
        SetDateInternal(candidate, true);
    }

    private void SetDateInternal(DateOnly d, bool emit)
    {
        _value = d;
        _dateLabel.Text = ToIso(d);
        if (emit)
            EmitSignal(SignalName.DateChanged, ToIso(d));
    }

    /// <summary>语义化:设置标签。</summary>
    public DateField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置日期范围。</summary>
    public DateField SetRange(DateOnly min, DateOnly max)
    {
        Min = min;
        Max = max;
        return this;
    }

    /// <summary>语义化:设置初始日期。</summary>
    public DateField SetValue(DateOnly d)
    {
        Date = d;
        return this;
    }

    /// <summary>DateOnly → yyyy-MM-dd (ISO 8601)。</summary>
    public static string ToIso(DateOnly d) => d.ToString("yyyy-MM-dd");
}
