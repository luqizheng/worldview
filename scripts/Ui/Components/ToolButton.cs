using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// 工具按钮（§4.1 spec 契约 A2 — ToolButton）
///
/// <para>复合按钮:内嵌 <see cref="JrpgIcon"/> + 可选文字 + 可切换 active 态。
/// 用途:工作区顶部工具条 / 视图模式切换 / 工具激活按钮。</para>
///
/// <para>token 接入:高度 / 字号 / 内边距 / active 底色 / 文字色全走 <c>Palette.*</c>。
/// active 态通过 Modulate 覆盖实现,不写硬编码颜色。</para>
///
/// <para>命名说明:叫 <c>ToolButton</c> 不带 <c>Jrpg</c> 前缀(对齐 Field 不带 Jrpg 约定)。
/// 内部 icon 字段叫 <c>_icon</c> 不叫 <c>Icon</c>,避免与 Godot <c>Button.Icon</c>(Texture2D 属性)类型解析冲突。</para>
/// </summary>
public partial class ToolButton : Button
{
    [Signal] public delegate void ToggledEventHandler(bool on);

    private readonly JrpgIcon _icon = new();
    private readonly Label _label = new();
    private JrpgIcon.Shape _iconShape = JrpgIcon.Shape.Close;
    private bool _toggledOn;

    /// <summary>当前 icon 形态。</summary>
    public JrpgIcon.Shape Icon
    {
        get => _iconShape;
        set
        {
            _iconShape = value;
            _icon.SetShape(value);
        }
    }

    /// <summary>按钮文字(可空,空则只显 icon)。</summary>
    public new string Text
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            _label.Visible = !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>当前 active 态(只在 ToggleMode=true 时有效)。</summary>
    public bool ToggledOn
    {
        get => _toggledOn;
        set
        {
            if (!ToggleMode) return;
            if (_toggledOn == value) return;
            _toggledOn = value;
            Modulate = value
                ? WorldView.Ui.Theme.Palette.Color.JrpgFillPressed
                : Colors.White;
            EmitSignal(SignalName.Toggled, value);
        }
    }

    /// <inheritdoc/>
    public ToolButton()
    {
        ToggleMode = false;
        _icon.MouseFilter = MouseFilterEnum.Ignore;
        _label.MouseFilter = MouseFilterEnum.Ignore;

        // 横向排列:左 icon + 右 label
        var hbox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        hbox.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.PaddingInline);
        hbox.AddChild(_icon);
        hbox.AddChild(_label);
        AddChild(hbox);

        // token 量化
        CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.ButtonHeight);
        _label.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeBody);
        _icon.SetSizeTier(JrpgIcon.SizeTier.Sm);

        // 内部不响应点击,按钮本身响应
        hbox.MouseFilter = MouseFilterEnum.Ignore;

        Toggled += pressed => ToggledOn = pressed;
    }

    /// <summary>语义化:设置 icon 形态。</summary>
    public ToolButton SetIcon(JrpgIcon.Shape shape)
    {
        Icon = shape;
        return this;
    }

    /// <summary>语义化:设置文字。</summary>
    public ToolButton SetText(string text)
    {
        Text = text;
        return this;
    }

    /// <summary>语义化:开启切换模式。</summary>
    public ToolButton AsToggle()
    {
        ToggleMode = true;
        return this;
    }
}
