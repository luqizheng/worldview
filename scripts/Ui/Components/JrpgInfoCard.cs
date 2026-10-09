using Godot;
using System.Collections.Generic;
using WorldView.Ui.Theme;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 地图视图层基础信息卡（§4.9.6 契约）
///
/// <para>世界地图点击城市 marker / 城市地图点击建筑 marker 时弹出。
/// 宽 280px,高按内容自适应(最大 360px),焦点态金黄描边,
/// 顶部 24px 势力色标题条 + 字段行 + 底部按钮三段式布局。</para>
///
/// <para>视觉契约：</para>
/// <list type="bullet">
///   <item>宽 280px(高自适应,最大 360px)</item>
///   <item>底色 #fff8e7,2px 焦褐描边,焦点态金黄</item>
///   <item>顶部 24px 势力色标题条 + #fff8e7 文字 14px 粗体衬线</item>
///   <item>字段行 8px 内边距,左 label #a89878 + 右 value #3a2810</item>
///   <item>底部按钮「进入编辑」/「关闭」</item>
///   <item>阴影 1px 1px 焦褐 40% 偏移</item>
/// </list>
///
/// <para>关闭：点击卡片外 / 按 Esc / 点击「关闭」</para>
/// </summary>
public partial class JrpgInfoCard : PanelContainer
{
    /// <summary>信息卡固定宽度（§4.9.6 契约）。</summary>
    public const int CardWidth = 280;

    /// <summary>信息卡最大高度。</summary>
    public const int MaxCardHeight = 360;

    /// <summary>信息卡字号（与 §4.5 一致）。</summary>
    public const int DefaultFontSize = WorldView.Ui.Theme.Palette.Metric.FontSizeBody;

    /// <summary>势力色（可在 Show 时覆盖,默认朱红以匹配世界地图首都 marker 配色）。</summary>
    public Color FactionColor { get; set; } = WorldView.Ui.Theme.Palette.Color.JrpgDanger;

    /// <summary>关闭事件。</summary>
    [Signal] public delegate void CardClosedEventHandler();

    /// <summary>「进入编辑」点击事件。</summary>
    [Signal] public delegate void EditRequestedEventHandler();

    private readonly Label _titleLabel = new();
    private readonly PanelContainer _titleBar = new();
    private readonly VBoxContainer _fieldRoot = new();
    private readonly HBoxContainer _buttonRow = new();
    private readonly Button _editButton = new();
    private readonly Button _closeButton = new();
    private readonly VBoxContainer _contentRoot = new();

    /// <summary>JRPG 风格：自动加载 theme 并构建三段式布局。</summary>
    public JrpgInfoCard()
    {
        ApplyJrpgTheme();
        BuildLayout();
    }

    /// <summary>语义化：设置势力色（影响标题条底色）。</summary>
    public JrpgInfoCard SetFactionColor(Color color)
    {
        FactionColor = color;
        ApplyTitleBarColor();
        return this;
    }

    /// <summary>语义化：设置标题。</summary>
    public JrpgInfoCard SetTitle(string title)
    {
        _titleLabel.Text = title;
        return this;
    }

    /// <summary>语义化：设置字段行（label + value）。</summary>
    public JrpgInfoCard SetFields(IEnumerable<(string Label, string Value)> fields)
    {
        // 清空旧字段
        foreach (var child in _fieldRoot.GetChildren())
        {
            child.QueueFree();
        }
        foreach (var (label, value) in fields)
        {
            _fieldRoot.AddChild(BuildFieldRow(label, value));
        }
        return this;
    }

    /// <summary>语义化：绑定「进入编辑」回调。</summary>
    public JrpgInfoCard BindEdit(System.Action handler)
    {
        var captured = handler;
        _editButton.Pressed += () => captured();
        return this;
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(CardWidth, 0);
        // Panel 自身描边/底色由 theme 提供

        // 标题条
        _titleBar.CustomMinimumSize = new Vector2(0, JrpgPanel.TitleBarHeight);
        ApplyTitleBarColor();
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        _titleLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgCard);
        _titleBar.AddChild(_titleLabel);

        // 字段区
        _fieldRoot.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing);
        _fieldRoot.SizeFlagsVertical = SizeFlags.ExpandFill;

        // 按钮行
        _buttonRow.AddThemeConstantOverride("separation", 8);
        _buttonRow.Alignment = BoxContainer.AlignmentMode.End;
        _editButton.Text = "进入编辑";
        _closeButton.Text = "关闭";
        _editButton.Pressed += () => EmitSignal(SignalName.EditRequested);
        _closeButton.Pressed += CloseCard;
        _buttonRow.AddChild(_editButton);
        _buttonRow.AddChild(_closeButton);

        _contentRoot.AddThemeConstantOverride("separation", 8);
        _contentRoot.AddChild(_titleBar);
        _contentRoot.AddChild(_fieldRoot);
        _contentRoot.AddChild(_buttonRow);

        AddChild(_contentRoot);
    }

    private HBoxContainer BuildFieldRow(string label, string value)
    {
        var row = new HBoxContainer();
        var lbl = new Label
        {
            Text = label,
            Modulate = WorldView.Ui.Theme.Palette.Color.JrpgTextPlaceholder,
        };
        lbl.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        lbl.CustomMinimumSize = new Vector2(80, 0);
        var val = new Label
        {
            Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        val.AddThemeFontSizeOverride("font_size", DefaultFontSize);
        val.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        row.AddChild(lbl);
        row.AddChild(val);
        return row;
    }

    private void ApplyTitleBarColor()
    {
        var style = new StyleBoxFlat
        {
            BgColor = FactionColor,
            BorderWidthBottom = 2,
            BorderColor = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
        _titleBar.AddThemeStyleboxOverride("panel", style);
    }

    /// <summary>语义化：关闭卡片（外部调用 / Esc / 点关闭按钮都会触发）。</summary>
    public void CloseCard()
    {
        EmitSignal(SignalName.CardClosed);
        QueueFree();
    }

    /// <inheritdoc/>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            CloseCard();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyJrpgTheme()
    {
        Theme = JrpgThemeCache.GetTheme();
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        if (Theme == null) ApplyJrpgTheme();
    }
}
