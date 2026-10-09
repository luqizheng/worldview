using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 对话框（§4.5.2 契约 / 模态确认 / 阻塞底层输入）
///
/// <para>继承 Godot <c>Window</c> 节点(不手写 overlay),自动获得:</para>
/// <list type="bullet">
///   <item>模态:打开时阻塞同一 Window 树下其他 UI 节点输入(由 <c>Window.PopupCentered</c> 实现)</item>
///   <item>独立 viewport:对话框内容不与主场景画布混淆</item>
///   <item>Esc 关闭(由 <c>Window</c> 内置 <c>CloseRequested</c> 信号触发)</item>
/// </list>
///
/// <para>构造参数 <c>minSize</c> = 360x180(确认对话框常见尺寸)。Body 区域走
/// <c>Palette.Color.JrpgCard</c>,确认 / 取消按钮由调用方添加到 <see cref="BodyRoot"/>。</para>
///
/// <para>关闭走 <see cref="CloseButton.TriggerClose"/> 同款 fade-out 淡出后 <c>Hide()</c>。</para>
/// </summary>
public partial class Dialog : Window
{
    /// <summary>Body 根容器,调用方 AddChild(自己的内容 / 按钮)。</summary>
    public VBoxContainer BodyRoot { get; }

    private readonly Label _titleLabel = new();
    private readonly PanelContainer _titleBar = new();
    private readonly HBoxContainer _titleRow = new();
    private readonly CloseButton _closeButton = new();

    /// <inheritdoc/>
    public Dialog()
    {
        Title = "Dialog";
        MinSize = new Vector2I(360, 180);
        WrapControls = true; // 自动 wrap 子节点到 body
        Transient = true;   // 不抢占主窗口焦点
        Exclusive = false;  // 不独占(允许非模态使用)

        BodyRoot = new VBoxContainer();
        BodyRoot.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing);

        // 标题条
        _titleBar.CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.PanelTitleHeight);
        var titleStyle = new StyleBoxFlat
        {
            BgColor = WorldView.Ui.Theme.Palette.Color.JrpgFill,
            BorderColor = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
            BorderWidthBottom = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
        };
        _titleBar.AddThemeStyleboxOverride("panel", titleStyle);

        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeTitle);
        _titleLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        _titleLabel.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        _titleRow.AddChild(_titleLabel);
        _titleRow.AddChild(_closeButton);
        _titleBar.AddChild(_titleRow);
        BodyRoot.AddChild(_titleBar);

        AddChild(BodyRoot);

        // Esc / 关闭请求 → fade-out + Hide
        CloseRequested += OnCloseRequested;
    }

    /// <summary>语义化:设置对话框标题(显示在顶部 + OS 标题栏)。</summary>
    public Dialog SetTitle(string title)
    {
        Title = title;
        _titleLabel.Text = title;
        return this;
    }

    /// <summary>语义化:屏幕居中显示 + 模态弹出。</summary>
    public Dialog PopupModal()
    {
        PopupCentered();
        return this;
    }

    /// <summary>语义化:fade-out 淡出后关闭(对齐 CloseButton 行为)。</summary>
    public void TriggerClose()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000f);
        tween.TweenCallback(Callable.From(Hide));
    }

    private void OnCloseRequested()
    {
        TriggerClose();
    }
}
