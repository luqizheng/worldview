using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Editing;
using WorldView.Render;
using WorldView.Ui.Dock;

namespace WorldView.Ui;

/// <summary>
/// 画布拖放落点（spec §4.2 / tasks.md T5）。
///
/// <para><b>为什么不是直接给 <c>WorldCanvas</c> 加 <c>_CanDropData</c> / <c>_DropData</c></b>：
/// <c>WorldCanvas</c> 是 <c>Node2D</c>（见 <c>scenes/WorldCanvas.tscn</c>），而 Godot 的拖放只发给
/// <b>Control 链</b>上的控件（<c>Viewport::gui_find_control</c> 只沿 Control 子节点下潜），
/// 因此 Node2D 上的这两个回调永远不会被调用。这里用覆盖整个画布区的 Control 承担落点，
/// <c>scripts/Render/**</c> 保持不动（比 spec §6 允许的唯一例外更严格）。</para>
///
/// <para><b>顺带转交输入</b>：本控件 <c>MouseFilter = Stop</c>，会先于
/// <c>WorldCanvas._UnhandledInput</c> 拿到鼠标事件；不转交的话笔刷 / 平移 / 缩放 / 下钻全部失效。</para>
///
/// <para><b>落点换算</b>：<c>atPosition</c> 是本控件局部坐标，必须先
/// <c>GetGlobalTransformWithCanvas()</c> 转全局，再用 WorldCanvas 的全局变换求逆——
/// 与 <c>WorldCanvas.GetLocalMousePosition()</c> 完全等价，画布平移 / 缩放时不会偏移（spec §4.2）。
/// 落下一步同样走 <see cref="UndoStack"/>，不绕过命令模式。</para>
/// </summary>
public partial class WorldDropSurface : Control
{
    /// <summary>WorldCanvas 节点路径（默认取同名子节点）。</summary>
    [Export]
    public NodePath? WorldCanvasPath { get; set; }

    private CanvasItem? _canvas;

    public override void _Ready()
    {
        // 必须是 Stop：IGNORE 的控件不会被 gui_find_control 选中，也就收不到拖放。
        MouseFilter = MouseFilterEnum.Stop;
        _canvas = ResolveCanvas();
    }

    private CanvasItem? ResolveCanvas()
    {
        if (WorldCanvasPath is not null && !WorldCanvasPath.IsEmpty)
        {
            return GetNodeOrNull<CanvasItem>(WorldCanvasPath);
        }

        // Main.tscn 里 WorldCanvas 已移到 CanvasArea 之外（见 DockPanel 顶部注释与 Main.tscn 分层结构），
        // 因此优先按唯一名查找，再退回旧的同名子节点写法。
        return GetNodeOrNull<CanvasItem>("%WorldCanvas")
               ?? GetNodeOrNull<CanvasItem>("WorldCanvas");
    }

    /// <inheritdoc/>
    public override void _GuiInput(InputEvent @event)
    {
        // WorldCanvas 是 Node2D，只有 _UnhandledInput 一条输入通道；本控件会先消费掉鼠标事件，
        // 因此显式转交一次（Godot 不会重复投递：Stop 已阻断 _unhandled_input）。
        // 只转交鼠标事件：键盘事件由 Viewport 直接投递到 _unhandled_input，转交会造成重复处理。
        if (@event is InputEventMouse && _canvas is Node canvas && IsInstanceValid(canvas))
        {
            canvas._UnhandledInput(@event);
        }
    }

    /// <inheritdoc/>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => DragPayload.Get<BuildingDragData>(data) is not null
           || DragPayload.Get<TerrainDragData>(data) is not null;

    /// <inheritdoc/>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var context = EditorContext.Instance;
        var map = context?.ActiveMap;
        if (context is null || map is null)
        {
            return;
        }

        var cell = ToCell(atPosition);

        if (DragPayload.Get<BuildingDragData>(data) is { } building)
        {
            if (!context.IsBuildableAt(map.Id, cell))
            {
                context.NotifyHint($"该格不可建造：({cell.X},{cell.Y})");
                return;
            }

            context.UndoStack.Push(new PlaceBuildingCommand(context, map.Id, cell, building.BuildingType));
            context.NotifyHint($"已放置 {building.BuildingType} @ ({cell.X},{cell.Y})");
            return;
        }

        if (DragPayload.Get<TerrainDragData>(data) is { } terrain)
        {
            context.UndoStack.Push(new ApplyBrushCommand(
                context,
                map.Id,
                terrain.Brush,
                cell,
                terrain.Radius,
                terrain.TerrainType));
            context.NotifyHint($"已绘制 {terrain.TerrainType} @ ({cell.X},{cell.Y})");
        }
    }

    /// <summary>把本控件局部坐标换算成网格坐标（与 WorldCanvas 内部换算等价）。</summary>
    private GridCoord ToCell(Vector2 atPosition)
    {
        var global = GetGlobalTransformWithCanvas() * atPosition;
        var local = _canvas is CanvasItem item && IsInstanceValid(item)
            ? item.GetGlobalTransformWithCanvas().AffineInverse() * global
            : global;

        var size = (float)TerrainTileSetFactory.TilePixels;
        return new GridCoord(
            Mathf.FloorToInt(local.X / size),
            Mathf.FloorToInt(local.Y / size));
    }
}
