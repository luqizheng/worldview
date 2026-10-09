using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 浮窗（§4.5.2 契约 Dialog 轻量替代 / 临时提示 / 不落盘）
///
/// <para>继承 <see cref="PanelContainer"/>,特性:</para>
/// <list type="bullet">
///   <item>可拖动:鼠标按住标题区域移动窗口位置</item>
///   <item><c>ClampRect</c>:窗口位置始终在视口内,防止拖出屏幕</item>
///   <item>不落盘:对齐 [界面与交互规格 §6] UI 状态仅内存纪律,关闭即销毁</item>
///   <item>右上角内嵌 <see cref="CloseButton"/>(<c>TriggerClose</c> 淡出销毁)</item>
/// </list>
///
/// <para>用法:继承 <c>FloatingWindow</c> 或直接 <c>new FloatingWindow().SetTitle(...).SetContent(...)</c>,
/// 调 <see cref="PopupCentered(Vector2)"/> 显示。</para>
/// </summary>
public partial class FloatingWindow : PanelContainer
{
    private readonly Label _titleLabel = new();
    private readonly PanelContainer _titleBar = new();
    private readonly VBoxContainer _contentRoot = new();
    private readonly HBoxContainer _titleRow = new();
    private readonly CloseButton _closeButton = new();
    private bool _dragging;
    private Vector2 _dragOffset;

    /// <summary>标题(语义化)。</summary>
    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    /// <inheritdoc/>
    public FloatingWindow()
    {
        CustomMinimumSize = new Vector2(360, 200);
        Size = new Vector2I(360, 200);
        FocusMode = FocusModeEnum.None;

        BuildLayout();
    }

    private void BuildLayout()
    {
        // 标题条:标题左对齐 + 关闭按钮右对齐
        _titleBar.CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.PanelTitleHeight);
        var titleStyle = new StyleBoxFlat
        {
            BgColor = WorldView.Ui.Theme.Palette.Color.JrpgFill,
            BorderColor = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
            BorderWidthBottom = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
        };
        _titleBar.AddThemeStyleboxOverride("panel", titleStyle);

        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeBody);
        _titleLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titleRow.AddChild(_titleLabel);
        _titleRow.AddChild(_closeButton);
        // CloseButton 关闭请求 → 整个浮窗淡出 + QueueFree(而不是只 free CloseButton 本身)
        _closeButton.CloseRequested += TriggerClose;

        _titleBar.AddChild(_titleRow);

        // 内容区
        _contentRoot.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing);
        _contentRoot.AddChild(_titleBar);
        // 留给调用方 AddChild(自己的内容) 到 _contentRoot

        // 浮窗整体加描边
        var bodyStyle = new StyleBoxFlat
        {
            BgColor = WorldView.Ui.Theme.Palette.Color.JrpgCard,
            BorderColor = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
            BorderWidthLeft = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
            BorderWidthTop = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
            BorderWidthRight = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
            BorderWidthBottom = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
        };
        AddThemeStyleboxOverride("panel", bodyStyle);

        AddChild(_contentRoot);
    }

    /// <summary>语义化:触发浮窗淡出 + QueueFree(对齐 CloseButton 行为)。</summary>
    public void TriggerClose()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>语义化:设置标题。</summary>
    public FloatingWindow SetTitle(string title)
    {
        Title = title;
        return this;
    }

    /// <summary>语义化:挂载自定义内容控件。</summary>
    public FloatingWindow SetContent(Control content)
    {
        _contentRoot.AddChild(content);
        return this;
    }

    /// <summary>语义化:屏幕居中显示(在父 Control 内可见区域内居中)。
    /// <para><paramref name="windowSize"/> 是想要的目标尺寸(对应 Godot <c>Window.PopupCentered</c> 的 size 参数,不是 viewport)。</para>
    /// </summary>
    public FloatingWindow PopupCentered(Vector2 windowSize)
    {
        Size = windowSize;
        // 计算父容器可视区域大小,作为居中/clamp 基准
        var parent = GetParentControl();
        var viewport = parent?.Size ?? GetViewportRect().Size;
        Position = new Vector2(
            (viewport.X - Size.X) / 2f,
            (viewport.Y - Size.Y) / 2f);
        ClampToViewport(viewport);
        return this;
    }

    /// <summary>ClampRect:把窗口位置 clamp 到父容器视口内(防止拖出屏幕)。</summary>
    public void ClampToViewport(Vector2 viewportSize)
    {
        var size = Size;
        var pos = Position;
        pos.X = Mathf.Clamp(pos.X, 0, Mathf.Max(0, viewportSize.X - size.X));
        pos.Y = Mathf.Clamp(pos.Y, 0, Mathf.Max(0, viewportSize.Y - size.Y));
        Position = pos;
    }

    /// <inheritdoc/>
    public override void _GuiInput(InputEvent @event)
    {
        // 拖动逻辑:鼠标左键在标题条按下 → 拖动 → 释放
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            _dragging = true;
            _dragOffset = GetLocalMousePosition();
            return;
        }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dragging = false;
            return;
        }
        if (@event is InputEventMouseMotion && _dragging)
        {
            // 父节点坐标系下的新位置 = 父节点全局鼠标位置 - 初始偏移
            var parent = GetParentControl();
            if (parent == null) return;
            var globalMouse = GetGlobalMousePosition();
            var parentGlobal = parent.GlobalPosition;
            var newPos = globalMouse - parentGlobal - _dragOffset;
            Position = newPos;

            // Clamp 到父容器视口
            var viewport = parent.Size;
            ClampToViewport(viewport);
        }
    }
}
