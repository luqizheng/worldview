using YamlDotNet.Serialization;

namespace WorldView.Domain;

/// <summary>
/// 世界生成参数（嵌套 <c>generation</c> 字段，见 spec.md §3 + 数据模型规格 §2.11）。
///
/// 七个字段分别控制：算法版本（保证同种子可复现）/ 随机种子 / 陆地占比 / 崎岖度 /
/// 最小岛屿格数 / 最小湖泊格数 / 海岸侵蚀步数。任一字段缺省即走默认行为；
/// 整块 <c>generation</c> 缺省时整张地图被视为「未指定生成参数」，UI 与保存都按空白处理。
/// </summary>
public sealed record GenerationSpec(
    [property: YamlMember(Alias = "algorithm")] string Algorithm = "coastline_v1",
    [property: YamlMember(Alias = "seed")] long Seed = 0,
    [property: YamlMember(Alias = "land_ratio")] double LandRatio = 0.35,
    [property: YamlMember(Alias = "ruggedness")] double Ruggedness = 0.5,
    [property: YamlMember(Alias = "min_island_cells")] int MinIslandCells = 8,
    [property: YamlMember(Alias = "min_lake_cells")] int MinLakeCells = 6,
    [property: YamlMember(Alias = "erosion")] int Erosion = 1)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public GenerationSpec() : this("coastline_v1", 0, 0.35, 0.5, 8, 6, 1) { }
}

/// <summary>
/// MVP 支持的地图类型（spec.md §2.2）。
/// 完整地图类型清单（region / street / building / underground）见 <c>docs/地图类型规格_v0.1.md</c>。
/// </summary>
public enum MapType
{
    World,
    City,
}

/// <summary>
/// 地图的网格参数（嵌套 <c>grid</c> 字段，见 spec.md §2.2 + 数据模型规格 §2）。
/// MVP 必填字段：<c>width</c> / <c>height</c> / <c>cell_distance</c> / <c>distance_unit</c> / <c>base_unit</c> / <c>height_unit</c>。
/// </summary>
public sealed record GridSpec(
    int Width,
    int Height,
    [property: YamlMember(Alias = "cell_distance")] double CellDistance,
    [property: YamlMember(Alias = "distance_unit")] string DistanceUnit = "m",
    [property: YamlMember(Alias = "base_unit")] string BaseUnit = "m",
    [property: YamlMember(Alias = "height_unit")] string HeightUnit = "m",
    GridCoord? Origin = null,
    bool Show = true,
    bool Snap = true,
    [property: YamlMember(Alias = "scale_label")] string? ScaleLabel = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public GridSpec() : this(0, 0, 1.0) { }
}

/// <summary>
/// 时间粒度（见 spec.md §2.2 + 数据模型规格 §2.3）。
/// world = month/day;city = hour/hour;building = minute/minute;underground = day/minute。
/// </summary>
public enum TimeGranularity
{
    Month,
    Day,
    Hour,
    Minute,
}

/// <summary>
/// 时间配置（嵌套 <c>time</c> 字段）。
/// </summary>
public sealed record TimeSpec(
    [property: YamlMember(Alias = "default_granularity")] TimeGranularity DefaultGranularity,
    [property: YamlMember(Alias = "min_granularity")] TimeGranularity MinGranularity,
    [property: YamlMember(Alias = "display_format")] string? DisplayFormat = null,
    [property: YamlMember(Alias = "inherit_calendar")] bool InheritCalendar = true,
    [property: YamlMember(Alias = "allow_fuzzy")] bool AllowFuzzy = true)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public TimeSpec() : this(TimeGranularity.Month, TimeGranularity.Day) { }
}

/// <summary>
/// 表现层配置（嵌套 <c>presentation</c> 字段）。
/// MVP <c>view_mode</c> 唯一字面量为 <c>"2d"</c>。
/// </summary>
public sealed record PresentationSpec(
    [property: YamlMember(Alias = "default_mode")] string DefaultMode = "edit",
    [property: YamlMember(Alias = "view_mode")] string ViewMode = "2d",
    string Theme = "parchment",
    [property: YamlMember(Alias = "show_scale_bar")] bool ShowScaleBar = true,
    [property: YamlMember(Alias = "show_coordinates")] bool ShowCoordinates = true,
    [property: YamlMember(Alias = "show_minimap")] bool ShowMinimap = false)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public PresentationSpec() : this("edit", "2d") { }
}

/// <summary>
/// 地形数据（嵌套 <c>terrain_data</c> 字段）。
/// MVP 仅 <c>encoding: "sparse"</c>；<c>grid</c> 与 <c>chunked</c> 见 spec.md §1.2 N04（M5+）。
///
/// <c>cells_ref</c> 字段指向外部地形文件路径（相对工程根）；MVP 单元格走 <c>cells_ref</c> 文件，内联 <c>cells</c> 通常为空。
/// </summary>
public sealed record TerrainData(
    string Encoding = "sparse",
    [property: YamlMember(Alias = "default")] TerrainCell? Default = null,
    [property: YamlMember(Alias = "cells_ref")] string? CellsRef = null,
    [property: YamlMember(Alias = "cells")] IReadOnlyList<TerrainCellEntry> Cells = null!)
{
    /// <summary>
    /// MVP 默认 <c>default = { type: plain, move_cost: 1, buildable: true }</c>。
    /// </summary>
    public TerrainData() : this("sparse", TerrainDefaults.Plain, null, Array.Empty<TerrainCellEntry>()) { }
}

/// <summary>
/// 内联地形单元条目（<c>at: [x, y], type: <terrain_type></c>）。
/// sparse 编码下仅记录非默认单元；其余格子按 <c>default</c> 填充。
/// </summary>
public sealed record TerrainCellEntry(
    [property: YamlMember(Alias = "at")] GridCoord At,
    TerrainType Type,
    [property: YamlMember(Alias = "move_cost")] int? MoveCost = null,
    [property: YamlMember(Alias = "buildable")] bool? Buildable = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public TerrainCellEntry() : this(default!, TerrainType.Plain) { }
}

/// <summary>
/// 城市标记（嵌套 <c>city_refs</c> 列表项，仅 <c>type="world"</c> 的地图使用）。
/// 字段定义见 spec.md §2.2；<c>map_ref</c> 相对工程根。
/// </summary>
public sealed record CityRef(
    string Id,
    string Name,
    int X,
    int Y,
    [property: YamlMember(Alias = "entry")] GridCoord Entry,
    [property: YamlMember(Alias = "map_ref")] string MapRef,
    int? Population = null,
    string? Faction = null,
    string? Notes = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public CityRef() : this("", "", 0, 0, default!, "") { }
}

/// <summary>
/// 地图文档（MVP 投影）。
/// 字段定义见 spec.md §2.2 + 数据模型规格 §2；与《数据模型规格》字段对照表见 spec.md §2.7。
///
/// MVP 不实现的字段（按规格默认值或不写入 YAML）：<c>parent_map</c> / <c>anchor</c> / <c>extent</c> / <c>markers</c> / <c>height_data</c> / <c>timeline_sync</c>。
/// </summary>
public sealed record MapDoc(
    string Id,
    string Name,
    MapType Type,
    GridSpec Grid,
    TimeSpec Time,
    [property: YamlMember(Alias = "presentation")] PresentationSpec Presentation = null!,
    [property: YamlMember(Alias = "terrain_data")] TerrainData TerrainData = null!,
    [property: YamlMember(Alias = "layers")] IReadOnlyList<LayerRef> Layers = null!,
    [property: YamlMember(Alias = "city_refs")] IReadOnlyList<CityRef> CityRefs = null!,
    [property: YamlMember(Alias = "markers")] IReadOnlyList<MarkerDoc> Markers = null!,
    [property: YamlMember(Alias = "routes")] IReadOnlyList<RouteDoc> Routes = null!,
    [property: YamlMember(Alias = "buildings")] IReadOnlyList<Building> Buildings = null!,
    [property: YamlMember(Alias = "generation")] GenerationSpec? Generation = null)
{
    /// <summary>
    /// 防御性默认值（防止反序列化时 null 引用）。
    /// </summary>
    public MapDoc() : this(
        Id: "",
        Name: "",
        Type: MapType.World,
        Grid: new GridSpec(64, 64, 1.0),
        Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
        Presentation: new PresentationSpec(),
        TerrainData: new TerrainData(),
        Layers: Array.Empty<LayerRef>(),
        CityRefs: Array.Empty<CityRef>(),
        Markers: Array.Empty<MarkerDoc>(),
        Routes: Array.Empty<RouteDoc>(),
        Buildings: Array.Empty<Building>(),
        Generation: null) { }
}

/// <summary>
/// §4.9.2 标记与图标扩展（数据模型规格）。
///
/// 6 类 sprite 标记 — kind 取值:
/// <list type="bullet">
///   <item><c>event_point</c>   16×16 事件点（卷轴+金星+红蜡封）</item>
///   <item><c>task_marker</c>   16×16 任务标记（金色卷轴+羽毛笔，放大 1.5× 视觉强调）</item>
///   <item><c>danger_point</c>  16×16 危险点（红骷髅）</item>
///   <item><c>character_dot</c> 8×8  人物点（暗棕实心点）</item>
///   <item><c>vehicle_icon</c>  12×12 载具（马车/手推车）</item>
///   <item><c>army_group</c>    16×16 军队群（3 红点+红三角旗）</item>
/// </list>
///
/// v0.1 简化：time_window / faction / payload 等扩展字段留到 M5 路线（与 Faction 实体同批）。
/// </summary>
public sealed record MarkerDoc(
    [property: YamlMember(Alias = "kind")] string Kind = "",
    [property: YamlMember(Alias = "x")] int X = 0,
    [property: YamlMember(Alias = "y")] int Y = 0,
    [property: YamlMember(Alias = "label")] string? Label = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public MarkerDoc() : this("", 0, 0, null) { }
}

/// <summary>
/// §4.9.3 路线（数据模型规格）。
///
/// 7 种线型 — kind 取值:
/// <list type="bullet">
///   <item><c>land</c>        陆上实线 2px + 1px 中心高光</item>
///   <item><c>path</c>        小径虚线 2px（每 4px 1px 间隔）</item>
///   <item><c>trade_route</c> 商路双实线（red + blue）</item>
///   <item><c>air_route</c>   航线点划线（8px 实 + 4px 间隔 + 2px 点）magic_glow</item>
///   <item><c>water_route</c> 水上实线 + 1px 浪花边</item>
///   <item><c>underground</c> 地下双实线 + 阴影偏移</item>
///   <item><c>teleport</c>    传送点对点虚线 + 两端发光</item>
/// </list>
///
/// waypoints 是有序格坐标列表（≥ 2 个点）；每条路线的端点用
/// node_sprite (0-5) 标注 — 见 assets/icons/route_nodes_atlas.png (6 帧 atlas)。
///
/// v0.1 简化：time_window / faction / 控制点偏移 留到 M3+（与道路数据契约同批）。
/// </summary>
public sealed record RouteDoc(
    [property: YamlMember(Alias = "kind")] string Kind = "",
    [property: YamlMember(Alias = "waypoints")] IReadOnlyList<GridCoord> Waypoints = null!,
    [property: YamlMember(Alias = "node_sprite")] int NodeSprite = 0,
    [property: YamlMember(Alias = "label")] string? Label = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public RouteDoc() : this("", Array.Empty<GridCoord>(), 0, null) { }
}
