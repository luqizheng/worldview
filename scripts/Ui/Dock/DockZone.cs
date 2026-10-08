using Godot;

namespace WorldView.Ui.Dock;

/// <summary>
/// 边缘停靠接受区（spec §4.1 / tasks.md T1）。
///
/// <para>5 个实例分别代表 <c>left</c> / <c>right</c> / <c>top</c> / <c>bottom</c> /
/// <c>center</c>（中央 = 浮动）。平时整片隐藏（<see cref="CanvasItem.Visible"/> = false），
/// 只有面板拖放进行中才由 <see cref="DockLayout"/> 亮起，避免吞掉画布的鼠标事件。</para>
///
/// <para><see cref="Control.MouseFilter"/> 必须是 <c>Stop</c>，否则 <c>_DropData</c>
/// 根本不会被调用。</para>
/// </summary>
public partial class DockZone : Control
{
    /// <summary>区域 ID，仅用于调试与持久化。</summary>
    [Export]
    public string ZoneId { get; set; } = "left";

    /// <summary>目标停靠边：<c>left</c> / <c>right</c> / <c>top</c> / <c>bottom</c> / <c>center</c>。</summary>
    [Export]
    public string Edge { get; set; } = "left";

    private Panel? _highlight;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.45f, 0.55f, 0.85f, 0.18f),
            BorderColor = new Color(0.45f, 0.55f, 0.85f, 1f),
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);

        _highlight = new Panel
        {
            Name = "Highlight",
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _highlight.AddThemeStyleboxOverride("panel", style);
        AddChild(_highlight);
        _highlight.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 鼠标进入该区 → 显示半透明蓝高亮（spec §4.1）；离开 → 只留可见区、取消高亮。
        MouseEntered += () => SetActive(Visible, true);
        MouseExited += () => SetActive(Visible, false);

        Visible = false;
    }

    /// <summary>拖放进行中：整区可见 + 可选高亮。</summary>
    public void SetActive(bool active, bool highlighted = false)
    {
        Visible = active;
        if (_highlight is not null)
        {
            _highlight.Visible = active && highlighted;
        }
    }

    /// <summary>只接受 <see cref="PanelDragData"/>（面板拖放），内容拖放由 WorldCanvas 处理。</summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => DragPayload.Get<PanelDragData>(data) is not null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (DragPayload.Get<PanelDragData>(data) is not { } payload)
        {
            return;
        }

        DockLayout.Instance?.HandlePanelDrop(Edge, payload.PanelId, atPosition);
    }
}
