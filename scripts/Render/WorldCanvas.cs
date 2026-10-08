using System;
using Godot;
using WorldView.Domain;
using WorldView.Editing;
using WorldView.Editor;

namespace WorldView.Render;

/// <summary>
/// 地图画布（见 tasks.md T12 / T16 / T18）。
///
/// 职责：
/// <list type="bullet">
///   <item>相机平移（中键拖拽 + WASD/方向键）与缩放（滚轮）。</item>
///   <item>把鼠标位置翻译成网格坐标并广播（StatusBar）。</item>
///   <item>按当前工具派发编辑：地形笔刷（100 ms 节流）/ 建筑放置。</item>
///   <item>城市标记双击下钻（D5）、Esc 返回上一层。</item>
///   <item>Ctrl+Z / Ctrl+Y / Ctrl+S 快捷键。</item>
/// </list>
///
/// 所有文档修改都经命令对象推入 <c>UndoStack</c>，本类不直接写文档。
/// </summary>
public partial class WorldCanvas : Node2D
{
    [Export]
    public float ZoomStep { get; set; } = 1.15f;

    [Export]
    public float MinZoom { get; set; } = 0.15f;

    [Export]
    public float MaxZoom { get; set; } = 8f;

    [Export]
    public float PanSpeed { get; set; } = 700f;

    private Camera2D? _camera;
    private TerrainLayer? _terrainLayer;
    private RoadLayer? _roadLayer;
    private BuildingLayer? _buildingLayer;
    private GridOverlay? _gridOverlay;
    private MarkerLayer? _markerLayer;

    private readonly BrushController _brush = new();
    private BuildingPlaceController? _placer;

    private bool _panning;
    private bool _painting;
    private Vector2I _lastPaintCell = new(int.MinValue, int.MinValue);

    /// <summary>
    /// 是否已绑定 <see cref="EditorContext"/> 信号。Godot 默认子先父后顺序：
    /// 本节点（Canvas 子树）_Ready 早于 EditorController._Ready，
    /// 第一次取 <c>EditorContext.Instance</c> 必为 null，故需延后到 _Process 里绑。
    /// </summary>
    private bool _contextBound;

    /// <summary>当前已绘制的地形瓦片数（T22 视口剔除验收指标）。</summary>
    public int GetDrawnCellCount() => _terrainLayer?.GetDrawnCellCount() ?? 0;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _terrainLayer = GetNodeOrNull<TerrainLayer>("TerrainLayer");
        _roadLayer = GetNodeOrNull<RoadLayer>("RoadLayer");
        _buildingLayer = GetNodeOrNull<BuildingLayer>("BuildingLayer");
        _gridOverlay = GetNodeOrNull<GridOverlay>("GridOverlay");
        _markerLayer = GetNodeOrNull<MarkerLayer>("MarkerLayer");

        // 第一次尝试：若 EditorController 已先 _Ready（场景里预设 EditorContext 节点时可能），
        // 直接绑信号 + 触发首次重建。
        TryBindContext();

        RebuildAll();
    }

    public override void _ExitTree()
    {
        var context = EditorContext.Instance;

        if (context is not null)
        {
            context.MapChanged -= OnMapChanged;
            context.LayersChanged -= ApplyLayerVisibility;
            _contextBound = false;
        }
    }

    /// <summary>
    /// 尝试绑定到 <see cref="EditorContext"/>。必须可重入安全：首次 _Ready 与后续 _Process
    /// 都会调用。绑定成功后会立刻 <see cref="RebuildAll"/>，避免错过 LoadProject 后才发出的信号。
    /// </summary>
    private bool TryBindContext()
    {
        if (_contextBound)
        {
            return true;
        }

        var context = EditorContext.Instance;
        if (context is null)
        {
            return false;
        }

        _placer ??= new BuildingPlaceController(context);
        context.MapChanged += OnMapChanged;
        context.LayersChanged += ApplyLayerVisibility;
        _contextBound = true;
        return true;
    }

    // ------------------------------------------------------------------ 重建

    private void OnMapChanged() => RebuildAll();

    private void RebuildAll()
    {
        var context = EditorContext.Instance;
        var map = context?.ActiveMap;

        if (context is null || map is null)
        {
            return;
        }

        var tileSet = TerrainTileSetFactory.Build();
        var cellPixels = (float)TerrainTileSetFactory.TilePixels;

        _terrainLayer?.Configure(tileSet, cellPixels);
        _terrainLayer?.Rebuild(map);

        _roadLayer?.Configure(tileSet, cellPixels);
        _roadLayer?.Rebuild(map);

        if (_buildingLayer is not null)
        {
            _buildingLayer.CellSize = cellPixels;
            _buildingLayer.Rebuild(map);
        }

        _gridOverlay?.Configure(map.Grid.Width, map.Grid.Height, cellPixels);

        if (_markerLayer is not null)
        {
            _markerLayer.CellSize = cellPixels;
            _markerLayer.Rebuild(map);
        }

        // 加载地图后：让 Camera2D 居中到地图中心，并把当前 viewport 缩放到「整张地图刚好填满可见区」
        CenterCameraOnMap(map);

        ApplyLayerVisibility();
        _brush.Reset();
        _lastPaintCell = new Vector2I(int.MinValue, int.MinValue);
    }

    private void ApplyLayerVisibility()
    {
        var context = EditorContext.Instance;

        if (context is null)
        {
            return;
        }

        if (_terrainLayer is not null)
        {
            _terrainLayer.Visible = context.IsLayerVisible(LayerKind.Terrain);
        }

        if (_roadLayer is not null)
        {
            _roadLayer.Visible = context.IsLayerVisible(LayerKind.Road);
        }

        if (_buildingLayer is not null)
        {
            _buildingLayer.Visible = context.IsLayerVisible(LayerKind.Building);
        }

        if (_markerLayer is not null)
        {
            _markerLayer.Visible = context.IsLayerVisible(LayerKind.Marker);
        }
    }

    // ------------------------------------------------------------------ 每帧平移

    public override void _Process(double delta)
    {
        // 延迟绑定 EditorContext：WorldCanvas._Ready 早于 EditorController._Ready，
        // 此时 EditorContext.Instance 仍为 null，需要在 _Process 里重试直到绑上。
        // 绑上后立即 RebuildAll，把首次 LoadProject 后设置的 ActiveMap 画出来。
        if (!_contextBound && TryBindContext())
        {
            RebuildAll();
        }

        if (_camera is null || !IsVisibleInTree())
        {
            return;
        }

        var direction = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            direction.X -= 1f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            direction.X += 1f;
        }

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            direction.Y -= 1f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            direction.Y += 1f;
        }

        if (direction != Vector2.Zero)
        {
            var zoom = Mathf.Max(0.001f, _camera.Zoom.X);
            _camera.Position += direction.Normalized() * PanSpeed * (float)delta / zoom;
        }
    }

    // ------------------------------------------------------------------ 输入

    public override void _UnhandledInput(InputEvent @event)
    {
        var context = EditorContext.Instance;

        if (context is null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton button:
                HandleMouseButton(context, button);
                break;

            case InputEventMouseMotion motion:
                HandleMouseMotion(context, motion);
                break;

            case InputEventKey key when key.Pressed && !key.Echo:
                HandleKey(context, key);
                break;
        }
    }

    private void HandleMouseButton(EditorContext context, InputEventMouseButton button)
    {
        // 中键：拖拽平移。
        if (button.ButtonIndex == MouseButton.Middle)
        {
            _panning = button.Pressed;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
        {
            ApplyZoom(ZoomStep);
            return;
        }

        if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
        {
            ApplyZoom(1f / ZoomStep);
            return;
        }

        if (button.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        var map = context.ActiveMap;

        if (map is null)
        {
            return;
        }

        var local = GetLocalMousePosition();
        var cell = LocalToCell(local);

        // 双击城市标记 → 下钻（D5）。
        if (button.Pressed && button.DoubleClick && context.Tool == ToolState.Selected)
        {
            TryDrillToCity(context, local);
            return;
        }

        if (button.Pressed)
        {
            _painting = true;
            _lastPaintCell = new Vector2I(int.MinValue, int.MinValue);

            switch (context.Tool)
            {
                case ToolState.Brush:
                    TryPaint(context, cell);
                    break;

                case ToolState.Building:
                    _placer?.TryPlace(cell);
                    break;

                default:
                    context.NotifySelection($"格 ({cell.X},{cell.Y})");
                    break;
            }
        }
        else
        {
            _painting = false;
        }
    }

    private void HandleMouseMotion(EditorContext context, InputEventMouseMotion motion)
    {
        if (_panning && _camera is not null)
        {
            _camera.Position -= motion.Relative / _camera.Zoom;
        }

        var local = GetLocalMousePosition();
        var cell = LocalToCell(local);
        context.NotifyMouseCell(new Vector2I(cell.X, cell.Y));

        if (_painting && context.Tool == ToolState.Brush)
        {
            TryPaint(context, cell);
        }
    }

    private void HandleKey(EditorContext context, InputEventKey key)
    {
        if (key.Keycode == Key.Escape)
        {
            context.DrillBack();
            return;
        }

        if (!key.CtrlPressed)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Z:
                context.NotifyHint(context.UndoStack.Undo() ? "已撤销" : "没有可撤销的操作");
                break;

            case Key.Y:
                context.NotifyHint(context.UndoStack.Redo() ? "已重做" : "没有可重做的操作");
                break;

            case Key.S:
                context.SaveProject();
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    // ------------------------------------------------------------------ 辅助

    private static GridCoord LocalToCell(Vector2 local)
    {
        var size = (float)TerrainTileSetFactory.TilePixels;
        return new GridCoord(
            Mathf.FloorToInt(local.X / size),
            Mathf.FloorToInt(local.Y / size));
    }

    private void TryPaint(EditorContext context, GridCoord cell)
    {
        var map = context.ActiveMap;

        if (map is null)
        {
            return;
        }

        // 同一格不重复落笔。
        if (cell.X == _lastPaintCell.X && cell.Y == _lastPaintCell.Y)
        {
            return;
        }

        // 100 ms 节流。
        if (!_brush.TryStamp())
        {
            return;
        }

        _lastPaintCell = new Vector2I(cell.X, cell.Y);

        var command = new ApplyBrushCommand(
            context,
            map.Id,
            context.Brush,
            cell,
            context.BrushRadius,
            context.PaintTerrain);

        context.UndoStack.Push(command);
    }

    private void TryDrillToCity(EditorContext context, Vector2 local)
    {
        if (_markerLayer is null)
        {
            return;
        }

        var cityId = _markerLayer.HitTest(local);

        if (cityId is null)
        {
            return;
        }

        var city = _markerLayer.FindCity(cityId);

        if (city is null)
        {
            return;
        }

        var mapId = context.ResolveMapIdByRef(city.MapRef);

        if (mapId is null)
        {
            context.NotifyHint($"找不到城市地图：{city.MapRef}");
            return;
        }

        context.DrillTo(mapId);
    }

    private void ApplyZoom(float factor)
    {
        if (_camera is null)
        {
            return;
        }

        var zoom = Mathf.Clamp(_camera.Zoom.X * factor, MinZoom, MaxZoom);
        _camera.Zoom = new Vector2(zoom, zoom);
    }

    /// <summary>
    /// 把 Camera2D 居中到地图中心，并按当前 viewport 尺寸缩放，
    /// 使整张地图刚好填满画布（保留少量边距）。
    /// </summary>
    private void CenterCameraOnMap(MapDoc map)
    {
        if (_camera is null) return;
        var cellPixels = (float)TerrainTileSetFactory.TilePixels;
        var mapSizePx = new Vector2(map.Grid.Width * cellPixels, map.Grid.Height * cellPixels);
        _camera.Position = mapSizePx / 2f;
        _camera.MakeCurrent();

        // 按 viewport 尺寸选择缩放比，使整张地图可见
        var viewport = GetViewportRect();
        if (viewport.Size.X <= 0 || viewport.Size.Y <= 0) return;
        var zX = (viewport.Size.X * 0.9f) / mapSizePx.X;
        var zY = (viewport.Size.Y * 0.9f) / mapSizePx.Y;
        var z = Mathf.Min(zX, zY);
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        _camera.Zoom = new Vector2(z, z);
    }
}
