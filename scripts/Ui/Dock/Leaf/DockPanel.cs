using System;
using Godot;
using WorldView.Ui.Dock.Service;

namespace WorldView.Ui.Dock.Leaf;

/// <summary>
/// 可停靠面板基类（spec §4.1 / tasks.md T1 + T4）。
///
/// <para>自带 chrome：顶部 **drag handle**（普通模式 22px 带标题 + 关闭按钮）。
/// 抓住 handle 按住左键移动 ≥ 4px 触发 <see cref="Control.ForceDrag"/>，
/// payload 为 <see cref="PanelDragData"/>。</para>
///
/// <para><b>compact 模式为什么用左侧纵向握把而不是顶部 16px 横条</b>：BottomPalette 的行高由
/// spec §3.2 钉死为 28 / 152 / 20px，20px 的工具行若再让顶部 16px 横条吃掉鼠标区域，
/// 就只剩 4px 可点，控件无法使用。因此 compact section 的握把贴在**左边缘**（
/// <see cref="CompactGripWidth"/> px 宽、整行高），纵向不占空间；spec §4.1 的
/// 「顶部 16px drag handle」保留给普通（非 compact）面板。拖动判定阈值仍是 4px +
/// <c>MouseButtonMask.Left</c>，D10 / D11 行为不变。</para>
///
/// <para><b>为什么不 <c>Hide()</c></b>：Godot 会在拖放源不可见时取消拖放，
/// 所以拖动中把自身降到 30% 透明度（视觉上等价「拿起」，但不打断拖放），
/// 收到 <c>NOTIFICATION_DRAG_END</c> 后恢复。</para>
/// </summary>
public partial class DockPanel : Control
{
    /// <summary>拖动判定阈值（px，spec §4.4）。</summary>
    public const float DragThreshold = 4f;

    /// <summary>compact 模式的拖动握把宽度（左侧纵向握把）。</summary>
    public const int CompactGripWidth = 10;

    /// <summary>普通模式的标题栏高度。</summary>
    public const int NormalHandleHeight = 22;

    /// <summary>面板稳定 ID，跨会话复用（写进布局 YAML 的 <c>id</c>）。</summary>
    [Export]
    public string PanelId { get; set; } = "panel";

    /// <summary>标题栏文字。</summary>
    [Export]
    public string PanelTitle { get; set; } = string.Empty;

    /// <summary>
    /// compact = true：只留 16px 无标题 handle（用于 BottomPalette 行内的 section，避免吃掉行高）。
    /// </summary>
    [Export]
    public bool Compact { get; set; }

    /// <summary>是否显示关闭按钮（仅非 compact 模式）。</summary>
    [Export]
    public bool Closable { get; set; } = true;

    private Control? _content;
    private Panel? _handle;
    private Label? _titleLabel;
    private Button? _closeButton;
    private StyleBoxFlat? _handleStyle;
    private bool _pressing;
    private bool _dragging;
    private Vector2 _pressGlobal;

    /// <summary>handle 高度（compact 用左侧握把，纵向不占高度）。</summary>
    public int HandleHeight => Compact ? 0 : NormalHandleHeight;

    /// <summary>面板内容（无内容时为 null）。</summary>
    public Control? Content => _content;

    public override void _Ready()
    {
        EnsureChrome();
        DockLayout.Instance?.RegisterPanel(this);
    }

    // ------------------------------------------------------------------ chrome

    private void EnsureChrome()
    {
        if (_handle is not null)
        {
            return;
        }

        _handleStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.16f, 0.20f, Compact ? 0f : 0.95f),
            BorderColor = new Color(0.30f, 0.30f, 0.36f, Compact ? 0.25f : 1f),
        };
        _handleStyle.SetBorderWidthAll(1);
        _handleStyle.SetCornerRadiusAll(4);

        _handle = new Panel
        {
            Name = "Handle",
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = string.IsNullOrEmpty(PanelTitle) ? PanelId : PanelTitle,
        };
        _handle.AddThemeStyleboxOverride("panel", _handleStyle);
        AddChild(_handle);

        if (Compact)
        {
            // 左侧纵向握把：整行高、10px 宽，纵向不占空间。
            _handle.SetAnchorsPreset(LayoutPreset.LeftWide);
            _handle.OffsetLeft = 0;
            _handle.OffsetRight = CompactGripWidth;
            _handle.OffsetTop = 0;
            _handle.OffsetBottom = 0;

            var grip = new Label
            {
                Name = "Grip",
                Text = "⋮",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            grip.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
            grip.AddThemeFontSizeOverride("font_size", 11);
            _handle.AddChild(grip);
            grip.SetAnchorsPreset(LayoutPreset.FullRect);
        }
        else
        {
            _handle.SetAnchorsPreset(LayoutPreset.TopWide);
            _handle.OffsetBottom = NormalHandleHeight;
        }

        if (!Compact)
        {
            _titleLabel = new Label
            {
                Name = "Title",
                Text = PanelTitle,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.56f, 0.56f, 0.63f));
            _titleLabel.AddThemeFontSizeOverride("font_size", 11);
            _handle.AddChild(_titleLabel);
            _titleLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            _titleLabel.OffsetLeft = 6;
            _titleLabel.OffsetRight = Closable ? -22 : -6;

            if (Closable)
            {
                _closeButton = new Button
                {
                    Name = "Close",
                    Text = "×",
                    Flat = true,
                    TooltipText = "关闭面板",
                };
                _handle.AddChild(_closeButton);
                _closeButton.SetAnchorsPreset(LayoutPreset.TopRight);
                _closeButton.OffsetLeft = -20;
                _closeButton.OffsetRight = -2;
                _closeButton.OffsetTop = 2;
                _closeButton.OffsetBottom = HandleHeight - 2;
                _closeButton.Pressed += OnClosePressed;
            }
        }

        _handle.GuiInput += OnHandleGuiInput;
        _handle.MouseEntered += () => SetHandleHover(true);
        _handle.MouseExited += () => SetHandleHover(false);

        // handle 永远在最上层，否则点击会被内容吃掉。
        MoveChild(_handle, GetChildCount() - 1);
    }

    /// <summary>装载内容；内容自动填满 handle 以下区域。</summary>
    public void SetContent(Control content)
    {
        if (!ReferenceEquals(_content, content))
        {
            if (_content is not null && IsInstanceValid(_content))
            {
                RemoveChild(_content);
            }

            _content = content;
            AddChild(content);
        }

        // 必须用 SetAnchorsAndOffsetsPreset：SetAnchorsPreset 默认 keep_offsets=false，
        // 会把 OffsetRight/OffsetBottom 算成 -父尺寸 以保持旧矩形，导致内容宽度恒为 0
        // （被钳制到最小尺寸 → Label 自动换行后逐字竖排）。
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = Compact ? CompactGripWidth : 0;
        content.OffsetTop = Compact ? 0 : NormalHandleHeight;
        content.OffsetRight = 0;
        content.OffsetBottom = 0;

        EnsureChrome();
        if (_handle is not null)
        {
            MoveChild(_handle, GetChildCount() - 1);
        }
    }

    /// <summary>摘出内容（重新挂载到别的宿主时用）。</summary>
    public Control? TakeContent()
    {
        var content = _content;
        if (content is not null && IsInstanceValid(content))
        {
            RemoveChild(content);
        }

        _content = null;
        return content;
    }

    private void SetHandleHover(bool hover)
    {
        if (_handleStyle is null)
        {
            return;
        }

        _handleStyle.BgColor = Compact
            ? new Color(0.45f, 0.55f, 0.85f, hover ? 0.35f : 0f)
            : new Color(0.16f, 0.16f, 0.20f, 0.95f);
        _handleStyle.BorderColor = hover
            ? new Color(0.45f, 0.55f, 0.85f, 1f)
            : new Color(0.30f, 0.30f, 0.36f, Compact ? 0.25f : 1f);
    }

    private void OnClosePressed()
    {
        Visible = false;
        DockLayout.Instance?.OnPanelClosed(PanelId);
    }

    // ------------------------------------------------------------------ 拖动

    private void OnHandleGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton button when button.ButtonIndex == MouseButton.Left:
                _pressing = button.Pressed;
                if (button.Pressed)
                {
                    _pressGlobal = button.GlobalPosition;
                }

                break;

            case InputEventMouseMotion motion when _pressing && !_dragging:
                // 注意：是 MouseButtonMask.Left，不是 MouseButton.Left（spec §4.4 [FIX]）
                if ((motion.ButtonMask & MouseButtonMask.Left) == 0)
                {
                    break;
                }

                if ((motion.GlobalPosition - _pressGlobal).Length() < DragThreshold)
                {
                    break;
                }

                BeginDrag();
                _handle?.AcceptEvent();
                break;
        }
    }

    private void BeginDrag()
    {
        _dragging = true;

        DockLayout layout = DockLayout.Instance!;
        layout?.NotifyPanelDragBegin(this);

        var payload = new PanelDragData
        {
            PanelId = PanelId,
            SourceZone = DockLayout.Instance?.ZoneOf(this) ?? string.Empty,
        };

        var data = Variant.From(payload);
        var preview = BuildDragPreview();
        SetDragPreview(preview);
        ForceDrag(data, preview);

        Modulate = new Color(1f, 1f, 1f, 0.3f);
    }

    private Control BuildDragPreview()
    {
        var preview = new PanelContainer
        {
            Modulate = new Color(1f, 1f, 1f, 0.85f),
            CustomMinimumSize = new Vector2(
                Mathf.Max(72f, Size.X),
                Mathf.Max(24f, Size.Y)),
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.45f, 0.55f, 0.85f, 0.22f),
            BorderColor = new Color(0.45f, 0.55f, 0.85f, 1f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        preview.AddThemeStyleboxOverride("panel", style);
        preview.AddChild(new Label
        {
            Text = string.IsNullOrEmpty(PanelTitle) ? PanelId : PanelTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        return preview;
    }

    public override void _Notification(int what)
    {
        if (what == (int)NotificationDragEnd || what == (int)NotificationDragBegin)
        {
            if (what == (int)NotificationDragEnd)
            {
                _dragging = false;
                _pressing = false;
                Modulate = Colors.White;
                DockLayout.Instance?.NotifyPanelDragEnd();
            }
        }
    }

    /// <summary>拖动结束 / 被取消后恢复外观。</summary>
    public void RestoreAfterDrag()
    {
        _dragging = false;
        _pressing = false;
        Modulate = Colors.White;
    }
}

/// <summary>
/// BottomPalette 作为「底部停靠容器」对外暴露的接口。
/// 让 <see cref="DockLayout"/> 不必反向依赖 <c>WorldView.Ui.Palette</c> 命名空间。
/// </summary>
public interface IPaletteHost
{
    /// <summary>把面板落到 palette 里（section 空缺位 → 该行新 section；行间隙 → 新行）。</summary>
    void AcceptPanel(DockPanel panel, Vector2 atPosition);

    /// <summary>当前折叠态。</summary>
    bool Collapsed { get; }

    /// <summary>切换折叠态（200 ↔ 40）。</summary>
    void SetCollapsed(bool collapsed);

    /// <summary>按布局文档重建行 / 节（启动还原时由 <see cref="DockLayout"/> 调用）。</summary>
    void ApplyLayout(PanelState? state);

    /// <summary>把当前行 / 节状态写回布局文档（保存前由 <see cref="DockLayout"/> 调用）。</summary>
    void CaptureLayout(PanelState state);
}
