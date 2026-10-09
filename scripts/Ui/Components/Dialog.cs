using Godot;

namespace WorldView.Ui.Components;

/// <summary>
/// JRPG 对话框（§4.5.2 契约 / 模态确认 / 阻塞底层输入）
///
/// <para>继承 <see cref="PanelContainer"/>,跟 <see cref="FloatingWindow"/> 同构,
/// 100% 走 Palette token,不被 OS Window 主题污染。</para>
///
/// <para>模态行为:在父 <see cref="CanvasLayer"/> 之上覆盖一层半透明 dim overlay
/// (<c>Palette.Modal.OverlayOpacity</c>)+ 截获 overlay 的 <c>GuiInput</c> 拦截所有底层点击,
/// 关闭时 overlay 跟 dialog 一起淡出 + QueueFree。</para>
///
/// <para>用法:<c>new Dialog().SetTitle(...).SetContent(...).PopupModal(parent)</c>
/// (parent 必须是 <see cref="Control"/> 或 <see cref="CanvasLayer"/>,dialog 会挂在它下面)。</para>
/// </summary>
public partial class Dialog : PanelContainer
{
    /// <summary>Body 根容器,调用方 AddChild(自己的内容 / 按钮)。</summary>
    public VBoxContainer BodyRoot { get; }

    /// <summary>关闭请求信号 —— 用户点 X / Esc / overlay 时发,父节点可订阅。</summary>
    [Signal] public delegate void CloseRequestedEventHandler();

    private readonly Label _titleLabel = new();
    private readonly PanelContainer _titleBar = new();
    private readonly HBoxContainer _titleRow = new();
    private readonly CloseButton _closeButton = new();
    private PanelContainer? _overlay;
    private bool _modalOpen;

    // 拖动状态 — 标题条按下时锁定
    private bool _dragging;
    private Vector2 _dragOffset;

    /// <inheritdoc/>
    public Dialog()
    {
        CustomMinimumSize = new Vector2(360, 180);
        Size = new Vector2(360, 180);
        FocusMode = FocusModeEnum.None;

        // Body 根容器
        BodyRoot = new VBoxContainer();
        BodyRoot.AddThemeConstantOverride("separation", WorldView.Ui.Theme.Palette.Metric.ParagraphSpacing);

        // 标题条 (跟 FloatingWindow 一致)
        _titleBar.CustomMinimumSize = new Vector2(0, WorldView.Ui.Theme.Palette.Metric.PanelTitleHeight);
        _titleBar.MouseFilter = MouseFilterEnum.Pass; // 让自己接收 GuiInput 来拖动
        var titleStyle = new StyleBoxFlat
        {
            BgColor = WorldView.Ui.Theme.Palette.Color.JrpgFill,
            BorderColor = WorldView.Ui.Theme.Palette.Color.JrpgOutline,
            BorderWidthBottom = WorldView.Ui.Theme.Palette.Metric.BorderWidth,
        };
        _titleBar.AddThemeStyleboxOverride("panel", titleStyle);
        // 标题条自己处理 GuiInput → 拖动 (不是整个 Dialog,避免吃掉 body 内 button 点击)
        _titleBar.GuiInput += OnTitleBarGuiInput;

        _titleLabel.VerticalAlignment = VerticalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", WorldView.Ui.Theme.Palette.Metric.FontSizeTitle);
        _titleLabel.AddThemeColorOverride("font_color", WorldView.Ui.Theme.Palette.Color.JrpgText);
        _titleLabel.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        _titleRow.AddChild(_titleLabel);
        _titleRow.AddChild(_closeButton);
        // CloseButton 关闭请求 → 整个对话框淡出 + QueueFree
        _closeButton.CloseRequested += TriggerClose;
        _titleBar.AddChild(_titleRow);
        BodyRoot.AddChild(_titleBar);

        // 对话框 body 整体走 JrpgCard 背景
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

        AddChild(BodyRoot);
    }

    /// <summary>语义化:设置对话框标题(显示在顶部标题条)。</summary>
    public Dialog SetTitle(string title)
    {
        _titleLabel.Text = title;
        return this;
    }

    /// <summary>语义化:模态弹出 — overlay + 居中 + 截获输入。</summary>
    public Dialog PopupModal(Control parent)
    {
        if (_modalOpen) return this;
        _modalOpen = true;

        // 1. 先建半透明 dim overlay,铺满父容器,吃掉所有鼠标点击
        _overlay = new PanelContainer { Name = "DialogOverlay" };
        _overlay.MouseFilter = MouseFilterEnum.Stop; // 拦截
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        var overlayStyle = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, WorldView.Ui.Theme.Palette.Modal.OverlayOpacity),
        };
        _overlay.AddThemeStyleboxOverride("panel", overlayStyle);
        // overlay 渲染在最底层
        parent.AddChild(_overlay);
        parent.MoveChild(_overlay, 0);

        // 2. 把 dialog 挂到 parent (overlay 之上)
        parent.AddChild(this);

        // 3. 居中
        PopupCentered(parent.Size);

        return this;
    }

    /// <summary>语义化:fade-out 淡出后关闭(对齐 CloseButton 行为)。</summary>
    public void TriggerClose()
    {
        EmitSignal(SignalName.CloseRequested);

        var tween = CreateTween();
        tween.SetParallel(true);
        // dialog 淡出
        tween.TweenProperty(this, "modulate:a", 0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000f);
        // overlay 淡出
        if (_overlay != null && IsInstanceValid(_overlay))
        {
            tween.TweenProperty(_overlay, "modulate:a", 0f, WorldView.Ui.Theme.Palette.Animation.FadeOutMs / 1000f);
        }
        tween.SetParallel(false);
        tween.TweenCallback(Callable.From(QueueFreeAll));
    }

    private void QueueFreeAll()
    {
        if (_overlay != null && IsInstanceValid(_overlay))
        {
            _overlay.QueueFree();
        }
        QueueFree();
    }

    /// <summary>在父 Control 内居中(对齐 FloatingWindow 的 PopupCentered)。</summary>
    private void PopupCentered(Vector2 parentSize)
    {
        Size = new Vector2(360, 180);
        Position = new Vector2(
            (parentSize.X - Size.X) / 2f,
            (parentSize.Y - Size.Y) / 2f);
    }

    /// <summary>标题条捕获输入 → 拖动 Dialog。Body 区域不受影响(button 点击正常)。</summary>
    private void OnTitleBarGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            _dragging = true;
            // 记录鼠标相对 dialog 自身的偏移(标题条内点击位置)
            _dragOffset = GetLocalMousePosition() - _titleBar.Position;
            return;
        }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            _dragging = false;
            return;
        }
        if (@event is InputEventMouseMotion && _dragging)
        {
            var parent = GetParentControl();
            if (parent == null) return;
            var globalMouse = GetGlobalMousePosition();
            var parentGlobal = parent.GlobalPosition;
            var newPos = globalMouse - parentGlobal - _dragOffset;
            Position = newPos;
            // 防止拖出父容器
            var viewport = parent.Size;
            Position = new Vector2(
                Mathf.Clamp(newPos.X, 0, Mathf.Max(0, viewport.X - Size.X)),
                Mathf.Clamp(newPos.Y, 0, Mathf.Max(0, viewport.Y - Size.Y)));
        }
    }
}
