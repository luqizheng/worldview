using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 建筑放置控制器（见 tasks.md T18 + D6）。
///
/// 只负责"能否放置 + 生成命令"，不直接改文档；越界或不可建造时通过状态栏提示而非弹窗。
/// </summary>
public sealed class BuildingPlaceController
{
    private readonly EditorContext _context;

    public BuildingPlaceController(EditorContext context)
    {
        _context = context;
    }

    /// <summary>在指定格放置当前选中的建筑类型。成功返回 true。</summary>
    public bool TryPlace(GridCoord cell)
    {
        var map = _context.ActiveMap;

        if (map is null)
        {
            return false;
        }

        if (!_context.IsBuildableAt(map.Id, cell))
        {
            _context.NotifyHint($"该格不可建造：({cell.X},{cell.Y})");
            return false;
        }

        var command = new PlaceBuildingCommand(_context, map.Id, cell, _context.PlaceBuildingType);
        _context.UndoStack.Push(command);
        _context.NotifyHint($"已放置 {_context.PlaceBuildingType} @ ({cell.X},{cell.Y})");
        return true;
    }
}
