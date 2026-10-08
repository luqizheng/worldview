using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldView.Domain;
using WorldView.Domain.Validation;
using WorldView.Editing;
using WorldView.Storage.Yaml;

namespace WorldView.Editor;

/// <summary>
/// 编辑器全局上下文（单例节点，挂在 <c>/root/Editor</c>）。
///
/// 职责（见 tasks.md T19 + spec.md §3.4）：
/// <list type="bullet">
///   <item>持有已加载的工程数据（<see cref="World"/> / 地图 / 图层 / 校验结果）。</item>
///   <item>维护"当前视图"状态（活动地图、面包屑导航栈）。</item>
///   <item>维护工具状态（<see cref="ToolState"/> / 笔刷 / 当前绘制地形）。</item>
///   <item>作为唯一写入通道：所有文档修改经 <see cref="ReplaceMap"/> / Set* 方法，配合 <see cref="UndoStack"/>。</item>
///   <item>通过 Godot 信号向 UI / 渲染层广播变化，UI 不轮询。</item>
/// </list>
///
/// 本类位于 <c>scripts/Editor/</c>，允许引用 Godot；<c>scripts/Domain/</c> 保持纯净。
/// </summary>
public partial class EditorContext : Node
{
    // ------------------------------------------------------------------ 单例

    /// <summary>全局唯一实例；未挂载时为 null。</summary>
    public static EditorContext? Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance is not null && Instance != this)
        {
            GD.PushWarning("EditorContext：已存在实例，销毁重复节点。");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------ 信号

    /// <summary>活动地图切换、或当前地图文档被修改。</summary>
    [Signal]
    public delegate void MapChangedEventHandler();

    /// <summary>图层列表或图层可见性变化。</summary>
    [Signal]
    public delegate void LayersChangedEventHandler();

    /// <summary>鼠标所在网格坐标变化（StatusBar 用）。</summary>
    [Signal]
    public delegate void MouseMovedEventHandler(Vector2I cell);

    /// <summary>选中对象变化，参数为可读描述（StatusBar 用）。</summary>
    [Signal]
    public delegate void SelectionChangedEventHandler(string description);

    /// <summary>状态栏提示文本。</summary>
    [Signal]
    public delegate void StatusHintEventHandler(string text);

    /// <summary>工具切换，参数为 <see cref="ToolState"/> 的整数值。</summary>
    [Signal]
    public delegate void ToolChangedEventHandler(int tool);

    /// <summary>
    /// 面板布局变化（停靠边 / 浮动 / 折叠 / 可见性 / 行节顺序）。
    /// 只承载 <b>UI 私有状态</b>，与地图 / 图层 / 文档数据无关（spec §5.1）。
    /// </summary>
    [Signal]
    public delegate void LayoutChangedEventHandler();

    // ------------------------------------------------------------------ 工程数据

    /// <summary>工程根目录（绝对路径）。</summary>
    public string ProjectRoot { get; private set; } = string.Empty;

    /// <summary>已加载的工程文档；未加载时为 null。</summary>
    public World World { get; private set; } = null!;

    public IReadOnlyList<MapDoc> Maps => _maps;

    public IReadOnlyList<LayerDoc> Layers => _layers;

    /// <summary>加载时得到的校验结果（D8 依据）。</summary>
    public ValidationResult Validation { get; private set; } = ValidationResult.Ok;

    public bool IsLoaded => World is not null;

    // ------------------------------------------------------------------ 当前视图

    /// <summary>当前活动地图 id；未加载时为空串。</summary>
    public string ActiveMapId { get; private set; } = string.Empty;

    /// <summary>当前活动地图文档（找不到返回 null）。</summary>
    public MapDoc? ActiveMap => FindMap(ActiveMapId);

    /// <summary>下钻导航栈（存 map id，栈底为最外层）。</summary>
    public IReadOnlyList<string> Breadcrumbs => _breadcrumbs;

    public bool CanDrillBack => _breadcrumbs.Count > 0;

    // ------------------------------------------------------------------ 工具状态

    public ToolState Tool { get; private set; } = ToolState.Selected;

    public BrushType Brush { get; private set; } = BrushType.Round;

    /// <summary>笔刷半径（格）。</summary>
    public int BrushRadius { get; set; } = 2;

    /// <summary>笔刷当前绘制的地形类型。</summary>
    public TerrainType PaintTerrain { get; private set; } = TerrainType.Mountain;

    /// <summary>建筑工具当前放置的类型。</summary>
    public BuildingType PlaceBuildingType { get; private set; } = BuildingType.House;

    /// <summary>撤销 / 重做栈（容量 200）。</summary>
    public UndoStack UndoStack { get; } = new();

    // ------------------------------------------------------------------ 私有状态

    private readonly List<MapDoc> _maps = new();
    private readonly List<LayerDoc> _layers = new();
    private readonly List<string> _breadcrumbs = new();

    // ------------------------------------------------------------------ 工程加载 / 保存

    /// <summary>
    /// 把 <c>res://</c> 路径转成真实文件系统路径；其它路径原样返回。
    /// 导出后的工程 <c>res://</c> 为只读，MVP 用绝对路径读写。
    /// </summary>
    public static string ResolvePath(string path) =>
        path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            ? ProjectSettings.GlobalizePath(path)
            : path;

    /// <summary>
    /// 加载工程（D1）。失败只报错不抛出，避免打断 Godot 主循环。
    /// </summary>
    public void LoadProject(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        var resolved = ResolvePath(rootPath);

        try
        {
            var result = ProjectLoader.Load(resolved);

            ProjectRoot = resolved;
            World = result.World;

            _maps.Clear();
            _maps.AddRange(result.Maps);

            _layers.Clear();
            _layers.AddRange(result.Layers);

            Validation = result.Validation;
            _breadcrumbs.Clear();
            UndoStack.Clear();
            ActiveMapId = PickInitialMapId();

            EmitSignal(SignalName.MapChanged);
            EmitSignal(SignalName.LayersChanged);
            EmitSignal(
                SignalName.StatusHint,
                $"已加载工程 {World.Name}：{_maps.Count} 张地图，{Validation.Errors.Count} 个错误");
        }
        catch (Exception ex)
        {
            GD.PushError($"加载工程失败：{resolved} — {ex.Message}");
            EmitSignal(SignalName.StatusHint, $"加载失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 保存工程（D7）。注意 <c>ProjectSaver.Save</c> 的实参顺序为 (rootPath, world, maps)。
    /// </summary>
    public bool SaveProject()
    {
        if (!IsLoaded)
        {
            EmitSignal(SignalName.StatusHint, "尚未加载工程，无法保存。");
            return false;
        }

        try
        {
            ProjectSaver.Save(ProjectRoot, World, _maps);
            EmitSignal(SignalName.StatusHint, $"已保存工程：{ProjectRoot}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"保存工程失败：{ex.Message}");
            EmitSignal(SignalName.StatusHint, $"保存失败：{ex.Message}");
            return false;
        }
    }

    private string PickInitialMapId()
    {
        var world = _maps.FirstOrDefault(m => m.Type == MapType.World);
        return (world ?? _maps.FirstOrDefault())?.Id ?? string.Empty;
    }

    // ------------------------------------------------------------------ 查询

    public MapDoc? FindMap(string mapId) =>
        string.IsNullOrEmpty(mapId) ? null : _maps.FirstOrDefault(m => m.Id == mapId);

    public LayerDoc? FindLayer(string layerId) =>
        string.IsNullOrEmpty(layerId) ? null : _layers.FirstOrDefault(l => l.Id == layerId);

    public LayerDoc? FindLayerByKind(LayerKind kind) =>
        _layers.FirstOrDefault(l => l.Type == kind);

    /// <summary>地图显示名，找不到则回落到 id。</summary>
    public string DisplayNameOf(string mapId) => FindMap(mapId)?.Name ?? mapId;

    /// <summary>
    /// 把工程内相对路径（如 <c>maps/city/city_kael.yaml</c>）解析成地图 id。
    ///
    /// 依据：<c>ProjectLoader</c> 按 <c>project.yaml</c> 的 <c>map_index</c> 顺序填充 <c>Maps</c>，
    /// 因此下标与 <see cref="World"/>.<c>MapIndex</c> 一一对应。
    /// </summary>
    public string? ResolveMapIdByRef(string refPath)
    {
        if (string.IsNullOrWhiteSpace(refPath) || World?.MapIndex is null)
        {
            return null;
        }

        var normalized = refPath.Replace('\\', '/').TrimStart('.', '/');

        for (var i = 0; i < World.MapIndex.Count && i < _maps.Count; i++)
        {
            var candidate = (World.MapIndex[i].RefPath ?? string.Empty)
                .Replace('\\', '/')
                .TrimStart('.', '/');

            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return _maps[i].Id;
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ 文档写入

    /// <summary>按 id 替换（或追加）一张地图文档，并广播 <c>MapChanged</c>。</summary>
    public void ReplaceMap(MapDoc map)
    {
        var index = _maps.FindIndex(m => m.Id == map.Id);

        if (index < 0)
        {
            _maps.Add(map);
        }
        else
        {
            _maps[index] = map;
        }

        EmitSignal(SignalName.MapChanged);
    }

    public void SetLayerVisible(string layerId, bool visible)
    {
        var index = _layers.FindIndex(l => l.Id == layerId);

        if (index < 0 || _layers[index].Visible == visible)
        {
            return;
        }

        _layers[index] = _layers[index] with { Visible = visible };
        EmitSignal(SignalName.LayersChanged);
    }

    public bool IsLayerVisible(string layerId) => FindLayer(layerId)?.Visible ?? false;

    public bool IsLayerVisible(LayerKind kind) => FindLayerByKind(kind)?.Visible ?? true;

    // ------------------------------------------------------------------ 地形读写

    public TerrainType GetTerrainAt(string mapId, GridCoord cell)
    {
        var map = FindMap(mapId);

        if (map is null)
        {
            return TerrainType.Plain;
        }

        if (map.TerrainData.Cells is not null)
        {
            foreach (var entry in map.TerrainData.Cells)
            {
                if (entry.At == cell)
                {
                    return entry.Type;
                }
            }
        }

        return map.TerrainData.Default?.Type ?? TerrainType.Plain;
    }

    /// <summary>
    /// 批量写入地形（笔刷 Apply / Revert 共用）。越界格静默跳过；
    /// 写回值等于 default 的格子从 sparse 列表中移除（保持稀疏）。
    /// </summary>
    public void SetTerrainCells(string mapId, IReadOnlyList<GridCoord> cells, TerrainType type)
    {
        var map = FindMap(mapId);

        if (map is null || cells.Count == 0)
        {
            return;
        }

        var defaultType = map.TerrainData.Default?.Type ?? TerrainType.Plain;

        var byCoord = new Dictionary<GridCoord, TerrainCellEntry>();

        if (map.TerrainData.Cells is not null)
        {
            foreach (var entry in map.TerrainData.Cells)
            {
                byCoord[entry.At] = entry;
            }
        }

        var width = map.Grid.Width;
        var height = map.Grid.Height;

        foreach (var cell in cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
            {
                continue;
            }

            if (type == defaultType)
            {
                byCoord.Remove(cell);
            }
            else
            {
                byCoord[cell] = new TerrainCellEntry(cell, type);
            }
        }

        var ordered = byCoord.Values
            .OrderBy(e => e.At.Y)
            .ThenBy(e => e.At.X)
            .ToList();

        var terrain = map.TerrainData with
        {
            Encoding = "sparse",
            CellsRef = null,
            Cells = ordered,
        };

        ReplaceMap(map with { TerrainData = terrain });
    }

    // ------------------------------------------------------------------ 建筑读写

    public void AddBuilding(string mapId, Building building)
    {
        var map = FindMap(mapId);

        if (map is null)
        {
            return;
        }

        if (map.Buildings.Any(b => b.Id == building.Id))
        {
            EmitSignal(SignalName.StatusHint, $"建筑 id 重复：{building.Id}");
            return;
        }

        var list = map.Buildings.ToList();
        list.Add(building);
        ReplaceMap(map with { Buildings = list });
    }

    public void RemoveBuilding(string mapId, string buildingId)
    {
        var map = FindMap(mapId);

        if (map is null)
        {
            return;
        }

        var list = map.Buildings.Where(b => b.Id != buildingId).ToList();

        if (list.Count == map.Buildings.Count)
        {
            return;
        }

        ReplaceMap(map with { Buildings = list });
    }

    /// <summary>该格是否可建造（读地形 buildable；越界返回 false）。</summary>
    public bool IsBuildableAt(string mapId, GridCoord cell)
    {
        var map = FindMap(mapId);

        if (map is null || cell.X < 0 || cell.Y < 0 ||
            cell.X >= map.Grid.Width || cell.Y >= map.Grid.Height)
        {
            return false;
        }

        if (map.TerrainData.Cells is not null)
        {
            foreach (var entry in map.TerrainData.Cells)
            {
                if (entry.At == cell)
                {
                    return entry.Buildable ?? true;
                }
            }
        }

        return map.TerrainData.Default?.Buildable ?? true;
    }

    // ------------------------------------------------------------------ 城市引用

    public void AddCityRef(string mapId, CityRef cityRef)
    {
        var map = FindMap(mapId);

        if (map is null || map.CityRefs.Any(c => c.Id == cityRef.Id))
        {
            return;
        }

        var list = map.CityRefs.ToList();
        list.Add(cityRef);
        ReplaceMap(map with { CityRefs = list });
    }

    public void RemoveCityRef(string mapId, string cityRefId)
    {
        var map = FindMap(mapId);

        if (map is null)
        {
            return;
        }

        var list = map.CityRefs.Where(c => c.Id != cityRefId).ToList();

        if (list.Count == map.CityRefs.Count)
        {
            return;
        }

        ReplaceMap(map with { CityRefs = list });
    }

    // ------------------------------------------------------------------ 下钻导航

    /// <summary>切入某张地图（D5）。</summary>
    public void DrillTo(string mapId)
    {
        if (FindMap(mapId) is null)
        {
            EmitSignal(SignalName.StatusHint, $"找不到地图：{mapId}");
            return;
        }

        if (ActiveMapId == mapId)
        {
            return;
        }

        if (!string.IsNullOrEmpty(ActiveMapId))
        {
            _breadcrumbs.Add(ActiveMapId);
        }

        ActiveMapId = mapId;
        EmitSignal(SignalName.MapChanged);
    }

    /// <summary>返回上一层（R-T-ZOOM-03：已在最深层时给提示而非报错）。</summary>
    public void DrillBack()
    {
        if (!CanDrillBack)
        {
            EmitSignal(SignalName.StatusHint, "已在最深层，无法继续返回。");
            return;
        }

        ActiveMapId = _breadcrumbs[^1];
        _breadcrumbs.RemoveAt(_breadcrumbs.Count - 1);
        EmitSignal(SignalName.MapChanged);
    }

    /// <summary>直接切换活动地图（不改变导航栈）。</summary>
    public void SetActiveMap(string mapId)
    {
        if (FindMap(mapId) is null || ActiveMapId == mapId)
        {
            return;
        }

        ActiveMapId = mapId;
        EmitSignal(SignalName.MapChanged);
    }

    // ------------------------------------------------------------------ 工具

    public void SetTool(ToolState tool)
    {
        if (Tool == tool)
        {
            return;
        }

        Tool = tool;
        EmitSignal(SignalName.ToolChanged, (int)tool);
        EmitSignal(SignalName.StatusHint, tool switch
        {
            ToolState.Brush => $"笔刷工具：{PaintTerrain}（半径 {BrushRadius}）",
            ToolState.Building => $"建筑工具：{PlaceBuildingType}",
            _ => "选择工具",
        });
    }

    public void SetBrush(BrushType brush) => Brush = brush;

    public void SetPaintTerrain(TerrainType terrain)
    {
        PaintTerrain = terrain;
        EmitSignal(SignalName.StatusHint, $"绘制地形：{terrain}");
    }

    public void SetPlaceBuildingType(BuildingType type)
    {
        PlaceBuildingType = type;
        EmitSignal(SignalName.StatusHint, $"放置建筑：{type}");
    }

    // ------------------------------------------------------------------ UI 上报入口

    public void NotifyMouseCell(Vector2I cell) => EmitSignal(SignalName.MouseMoved, cell);

    public void NotifySelection(string description) => EmitSignal(SignalName.SelectionChanged, description);

    public void NotifyHint(string text) => EmitSignal(SignalName.StatusHint, text);

    /// <summary>
    /// 广播 UI 布局变化。唯一的发出方是 <c>scripts/Ui/Dock/DockLayout.cs</c>，
    /// 由它驱动 500ms 防抖落盘（spec §5.2 写时机）。
    /// </summary>
    public void NotifyLayoutChanged() => EmitSignal(SignalName.LayoutChanged);
}
