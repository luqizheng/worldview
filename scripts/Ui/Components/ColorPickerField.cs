using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 颜色选择 Field（§4.5.5 A4 契约 Form 行）
///
/// <para>行布局:左 <see cref="JrpgLabel"/>(<c>FormLabelWidth</c>=100) +
/// 右 <c>HBoxContainer</c>( <c>ColorRect swatch</c> 20×20 + <c>JrpgLabel hex</c> + <c>JrpgButton "..."</c> )。</para>
///
/// <para>点击按钮 → 弹浮窗含 Godot 内建 <c>ColorPicker</c>; <c>ColorChanged</c> 信号实时推
/// 浮窗 ColorPicker → 本 Field swatch / hex / Value。</para>
///
/// <para>校验: <see cref="Value"/> 永远 valid(只要非 null)。</para>
/// </summary>
public partial class ColorPickerField : Field
{
    private readonly JrpgLabel _label = new();
    private readonly HBoxContainer _rightBox = new();
    private readonly ColorRect _swatch = new();
    private readonly JrpgLabel _hexLabel = new();
    private readonly JrpgButton _openBtn = new();

    private PanelContainer? _pickerWindow;
    private Action<Color>? _onChange;

    /// <inheritdoc/>
    public override string Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <inheritdoc/>
    public override string Value
    {
        get => _hexLabel.Text;
        set
        {
            if (value.StartsWith("#") && value.Length == 7)
            {
                var c = new Color(value);
                SetColorInternal(c, false);
            }
        }
    }

    /// <summary>当前颜色。setter 同步 swatch + hex + 触发 <see cref="ColorChanged"/>。</summary>
    public Color Color
    {
        get => _swatch.Color;
        set => SetColorInternal(value, true);
    }

    /// <inheritdoc/>
    public override bool IsValid => true;

    /// <summary>颜色变化时发射(浮窗 ColorPicker 拖动 / SetColor)。</summary>
    [Signal] public delegate void ColorChangedEventHandler(Color color);

    /// <inheritdoc/>
    public ColorPickerField()
    {
        AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        _label.CustomMinimumSize = new Vector2(WorldView.Ui.Theme.Palette.Metric.FormLabelWidth, 0);
        _label.VerticalAlignment = VerticalAlignment.Center;

        _rightBox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInlineButton);

        _swatch.CustomMinimumSize = new Vector2(20, 20);
        _swatch.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _swatch.Color = new Color(1, 1, 1, 1);
        _swatch.SetMeta("border_color", WorldView.Ui.Theme.Palette.Color.JrpgOutline);

        _hexLabel.Text = "#FFFFFF";
        _hexLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        _hexLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _openBtn.Text = "...";
        _openBtn.Pressed += OpenPicker;

        _rightBox.AddChild(_swatch);
        _rightBox.AddChild(_hexLabel);
        _rightBox.AddChild(_openBtn);

        AddChild(_label);
        AddChild(_rightBox);
    }

    private void SetColorInternal(Color c, bool emit)
    {
        _swatch.Color = c;
        _hexLabel.Text = ToHex(c);
        if (emit)
        {
            EmitSignal(SignalName.ColorChanged, c);
            _onChange?.Invoke(c);
        }
    }

    /// <summary>打开 ColorPicker 浮窗 (PanelContainer + ColorPicker + CloseButton)。</summary>
    public void OpenPicker()
    {
        if (_pickerWindow is not null && IsInstanceValid(_pickerWindow))
            return;

        _pickerWindow = new PanelContainer();
        _pickerWindow.CustomMinimumSize = new Vector2(380, 460);

        var root = new VBoxContainer();
        var titleBar = new HBoxContainer();
        var closeBtn = new CloseButton();
        closeBtn.CloseRequested += () => _pickerWindow?.QueueFree();
        titleBar.AddChild(closeBtn);
        titleBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var picker = new ColorPicker();
        picker.Color = _swatch.Color;
        picker.ColorChanged += c => SetColorInternal(c, true);
        picker.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        root.AddChild(titleBar);
        root.AddChild(picker);
        _pickerWindow.AddChild(root);

        var parent = GetTree().Root;
        parent.AddChild(_pickerWindow);
        var viewport = parent.GetViewport().GetVisibleRect().Size;
        _pickerWindow.Position = new Vector2(
            (viewport.X - _pickerWindow.CustomMinimumSize.X) / 2,
            (viewport.Y - _pickerWindow.CustomMinimumSize.Y) / 2);
    }

    /// <summary>语义化:设置标签。</summary>
    public ColorPickerField SetLabel(string label)
    {
        Label = label;
        return this;
    }

    /// <summary>语义化:设置初始颜色。</summary>
    public ColorPickerField SetValue(Color c)
    {
        Color = c;
        return this;
    }

    /// <summary>语义化:设置颜色变化回调。</summary>
    public ColorPickerField OnChange(Action<Color> callback)
    {
        _onChange = callback;
        return this;
    }

    /// <summary>RGB → #RRGGBB 格式。</summary>
    public static string ToHex(Color c) => $"#{(int)(c.R * 255):X2}{(int)(c.G * 255):X2}{(int)(c.B * 255):X2}";
}
