using System.Collections.Generic;
using System.Linq;
using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 地形笔刷命令（见 tasks.md T11 + D2）。
///
/// Apply 记录受影响格子的<b>原始地形</b>后再统一绘制，因此 Revert 能精确复原
/// 局部笔刷（不会把原本就是该地形的格子误改，也不会破坏混合地形）。
/// </summary>
public sealed class ApplyBrushCommand : IEditorCommand
{
    private readonly EditorContext _context;
    private readonly string _mapId;
    private readonly BrushType _brush;
    private readonly GridCoord _center;
    private readonly int _radius;
    private readonly TerrainType _type;

    private readonly List<GridCoord> _cells;
    private readonly Dictionary<GridCoord, TerrainType> _before = new();

    public ApplyBrushCommand(
        EditorContext context,
        string mapId,
        BrushType brush,
        GridCoord center,
        int radius,
        TerrainType type)
    {
        _context = context;
        _mapId = mapId;
        _brush = brush;
        _center = center;
        _radius = radius;
        _type = type;
        _cells = BrushShapes.Cells(center, radius, brush);
    }

    public string Description => $"笔刷 {_type} @ ({_center.X},{_center.Y}) r={_radius}";

    public void Apply()
    {
        if (_before.Count == 0)
        {
            foreach (var cell in _cells)
            {
                _before[cell] = _context.GetTerrainAt(_mapId, cell);
            }
        }

        _context.SetTerrainCells(_mapId, _cells, _type);
    }

    public void Revert()
    {
        // 原始地形可能混合，按类型分组批量写回。
        foreach (var group in _before.GroupBy(kv => kv.Value))
        {
            _context.SetTerrainCells(_mapId, group.Select(kv => kv.Key).ToList(), group.Key);
        }
    }
}
