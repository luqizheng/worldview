using YamlDotNet.Serialization;

namespace WorldView.Domain;

/// <summary>
/// MVP 支持的 11 种地形类型。
/// 字段定义以 <c>docs/数据模型规格_v0.1.md §3</c> 为唯一真源。
/// 颜色值以 <c>spec.md §2.4</c> 为准。
/// </summary>
public enum TerrainType
{
    Plain,
    Mountain,
    Forest,
    Hill,
    Water,
    Desert,
    Swamp,
    Tundra,
    Basin,
    Coast,
    River,
}

/// <summary>
/// 单个地形单元的完整描述。
/// MVP 默认值 <c>DefaultTerrain = { type: plain, move_cost: 1, buildable: true }</c>（见 spec.md §2.2 + §2.4）。
/// 字段 <c>Height</c> MVP 不读写（M3+ 等高线见 N01）。
/// </summary>
public sealed record TerrainCell(
    TerrainType Type,
    int MoveCost = 1,
    bool Buildable = true,
    [property: YamlMember(Alias = "height")] int? Height = null)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public TerrainCell() : this(TerrainType.Plain, 1, true) { }
}

/// <summary>
/// 地形默认值常量（无 YAML 依赖；所有地图继承）。
/// </summary>
public static class TerrainDefaults
{
    public const TerrainType DefaultType = TerrainType.Plain;
    public const int DefaultMoveCost = 1;
    public const bool DefaultBuildable = true;

    /// <summary>
    /// MVP 默认地形单元：<c>{ type: plain, move_cost: 1, buildable: true }</c>。
    /// </summary>
    public static TerrainCell Plain => new(TerrainType.Plain, 1, true);

    /// <summary>
    /// MVP 默认水域单元：<c>{ type: water, move_cost: 999, buildable: false }</c>。
    /// 不可建造是硬要求（spec 2 §3.2 含义）；move_cost 是占位值 [待标定]。
    /// </summary>
    public static TerrainCell Water => new(TerrainType.Water, 999, false);

    /// <summary>
    /// 11 种地形类型 → spec.md §2.4 显示色（hex）。
    /// Render/TerrainPalette.cs（M3+ 实现）按此映射取 TileSet 帧。
    /// </summary>
    public static string ColorHex(TerrainType type) => type switch
    {
        TerrainType.Plain => "#c2b280",
        TerrainType.Mountain => "#7a6f5c",
        TerrainType.Forest => "#3f7a3f",
        TerrainType.Hill => "#8a7a4a",
        TerrainType.Water => "#3a6ea5",
        TerrainType.Desert => "#d9c47a",
        TerrainType.Swamp => "#5a6a3a",
        TerrainType.Tundra => "#cfd6e0",
        TerrainType.Basin => "#7a8aa6",
        TerrainType.Coast => "#a8c3d6",
        TerrainType.River => "#5e8fc4",
        _ => "#000000",
    };
}
