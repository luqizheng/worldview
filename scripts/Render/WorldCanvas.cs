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

    /// <summary>
    /// 相机拟合的目标区（= UI 层 <c>CanvasArea</c> 的屏幕矩形）。默认为空表示沿用整个 viewport（旧行为）。
    /// 由 <c>scripts/Editor/EditorController.cs</c> 赋值。之所以需要它：UI 在 <c>CanvasLayer</c> 内不受相机缩放，
    /// 而地图仍受相机控制，若继续按窗口拟合并把地图中心对准窗口中心，地图会比画布区大一截且整体偏下、
    /// 下半被底栏遮住。
    /// </summary>
    public Rect2 FitRect { get; set; }

    /// <summary>按当前 <see cref="FitRect"/> 重新拟合相机（供 UI 侧做「初始拟合」）。</summary>
    public void RefitCamera()
    {
        if (EditorContext.Instance?.ActiveMap is { } map)
        {
            CenterCameraOnMap(map);
        }
    }

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

    /// <summary>本次笔迹覆盖的最小格（按笔刷覆盖盒扩张，见 <see cref="TryPaint"/>）。</summary>
    private GridCoord _strokeMin;

    /// <summary>本次笔迹覆盖的最大格。</summary>
    private GridCoord _strokeMax;

    /// <summary>本节点的按下分支是否开了一个撤销批（只有笔刷工具会开）。</summary>
    private bool _strokeBatchOpen;

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

        // 经注册器取 TileSet，使「瓦片风格」下拉真正驱动渲染（plan L488）。
        // 当前 assets/textures/tiles/ 下尚无 tileset.tres，TileSetLoader 会降级到
        // TerrainTileSetFactory.Build() 的纯色块 —— 与接线前同观感，但路径已通，
        // 下一轮放入真实 TileSet 资源后无需再改本文件。
        var tileSet = TileSetLoader.Get(context.CurrentTilesetId);
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

            _strokeMin = cell;
            _strokeMax = cell;
            _strokeBatchOpen = false;

            // 只有笔刷才开批：建筑 / 选择工具若也开批，释放时会误关别人的批。
            if (context.Tool == ToolState.Brush)
            {
                context.UndoStack.BeginBatch("笔刷绘制");
                _strokeBatchOpen = true;
            }

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

            if (_strokeBatchOpen)
            {
                // 细化只在释放时发生一次，覆盖整条笔迹外扩 1 格（CoastlineRefiner 的扫描盒还会再外扩 1）。
                if (context.RefineOnStroke)
                {
                    var refineMin = new GridCoord(_strokeMin.X - 1, _strokeMin.Y - 1);
                    var refineMax = new GridCoord(_strokeMax.X + 1, _strokeMax.Y + 1);

                    // 与网格求交，避免越界范围。
                    refineMin = new GridCoord(Math.Max(0, refineMin.X), Math.Max(0, refineMin.Y));
                    refineMax = new GridCoord(
                        Math.Min(map.Grid.Width - 1, refineMax.X),
                        Math.Min(map.Grid.Height - 1, refineMax.Y));

                    if (refineMin.X <= refineMax.X && refineMin.Y <= refineMax.Y)
                    {
                        context.UndoStack.Push(new RefineCoastlineCommand(
                            context, map.Id, refineMin, refineMax, context.BrushRadius));
                    }
                }

                // 无论是否真的推了细化命令，都必须关批 —— 否则撤销栈会一直处于批模式。
                context.UndoStack.EndBatch();
                _strokeBatchOpen = false;
            }
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

        // 笔迹范围按「笔刷覆盖盒」扩张，且在节流 early-return 之前更新 ——
        // 保证细化范围覆盖整条拖动轨迹（宁可多覆盖也不能漏，漏了会留下未细化的海岸）。
        var r = context.BrushRadius;
        if (cell.X - r < _strokeMin.X) _strokeMin = new GridCoord(cell.X - r, _strokeMin.Y);
        if (cell.Y - r < _strokeMin.Y) _strokeMin = new GridCoord(_strokeMin.X, cell.Y - r);
        if (cell.X + r > _strokeMax.X) _strokeMax = new GridCoord(cell.X + r, _strokeMax.Y);
        if (cell.Y + r > _strokeMax.Y) _strokeMax = new GridCoord(_strokeMax.X, cell.Y + r);

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
        _camera.MakeCurrent();

        // 拟合目标：优先用 UI 侧给的画布区矩形（FitRect，见 EditorController），否则退回整个 viewport
        var viewportSize = GetViewportRect().Size;
        var useFit = FitRect.Size.X > 1f && FitRect.Size.Y > 1f;
        var fitSize = useFit ? FitRect.Size : viewportSize;
        if (fitSize.X <= 0f || fitSize.Y <= 0f) return;

        var zX = (fitSize.X * 0.9f) / mapSizePx.X;
        var zY = (fitSize.Y * 0.9f) / mapSizePx.Y;
        var z = Mathf.Clamp(Mathf.Min(zX, zY), MinZoom, MaxZoom);

        // 相机默认把自己的位置对准整个 viewport 中心；这里改为对准拟合区中心，
        // 否则地图在画布区里整体偏下（画布区上方是 TopStrip、下方是 BottomPalette）。
        var fitCenter = useFit ? FitRect.Position + FitRect.Size / 2f : viewportSize / 2f;
        _camera.Position = mapSizePx / 2f - (fitCenter - viewportSize / 2f) / z;
        _camera.Zoom = new Vector2(z, z);
    }
}
