namespace WorldView.Domain;

/// <summary>
/// MVP 支持的 3 种建筑占位类型（见 spec.md §2.5）。
/// 完整建筑字段（含 faction / owner / state 等）见 <c>docs/数据模型规格_v0.1.md</c>；M5 落地。
/// </summary>
public enum BuildingType
{
    House,
    Tower,
    WallGate,
}

/// <summary>
/// 单个建筑占位（MVP 投影）。
/// 字段定义见 spec.md §2.5：<c>id</c> / <c>type</c> / <c>at</c> / <c>map_id</c>。
///
/// MVP 注：建筑以 <c>inline 列表</c> 登记在城市地图 YAML 内（<c>buildings: [...]</c>）。
/// 数据模型规格 §2 的 <c>building_refs</c>（独立 <c>buildings/<id>.yaml</c>）推迟到 M5。
/// </summary>
public sealed record Building(
    string Id,
    BuildingType Type,
    GridCoord At,
    string MapId)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public Building() : this("", BuildingType.House, default!, "") { }
}

/// <summary>
/// 二维网格坐标（<c>(x, y)</c>）。
/// MVP 范围：<c>0 ≤ x < grid.width</c> 且 <c>0 ≤ y < grid.height</c>。
/// 字段在 YAML 中序列化为 <c>[x, y]</c>（见 spec.md §2.5）。
/// </summary>
public readonly record struct GridCoord(int X, int Y);
