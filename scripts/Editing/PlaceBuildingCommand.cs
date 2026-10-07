using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 建筑放置命令（见 tasks.md T11 + D6）。
///
/// Apply 生成确定性 id 并加入地图；Revert 按同一 id 删除，不依赖列表下标。
/// </summary>
public sealed class PlaceBuildingCommand : IEditorCommand
{
    private readonly EditorContext _context;
    private readonly string _mapId;
    private readonly GridCoord _cell;
    private readonly BuildingType _type;
    private readonly string _buildingId;

    public PlaceBuildingCommand(EditorContext context, string mapId, GridCoord cell, BuildingType type)
    {
        _context = context;
        _mapId = mapId;
        _cell = cell;
        _type = type;
        _buildingId = $"b_{mapId}_{cell.X}_{cell.Y}";
    }

    public string Description => $"放置 {_type} @ ({_cell.X},{_cell.Y})";

    public void Apply()
    {
        _context.AddBuilding(_mapId, new Building(_buildingId, _type, _cell, _mapId));
    }

    public void Revert()
    {
        _context.RemoveBuilding(_mapId, _buildingId);
    }
}
