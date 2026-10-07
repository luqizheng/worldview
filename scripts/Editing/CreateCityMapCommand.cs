using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Editing;

/// <summary>
/// 切入城市地图命令（见 tasks.md T11 + D5）。
///
/// Apply 下钻到目标城市地图；Revert 退回上一层。
/// 记录进入前的活动地图 id，保证 Revert 精确回到原处（而非依赖 <c>DrillBack</c> 的栈顶）。
/// </summary>
public sealed class CreateCityMapCommand : IEditorCommand
{
    private readonly EditorContext _context;
    private readonly string _cityMapId;
    private string _previousMapId = string.Empty;

    public CreateCityMapCommand(EditorContext context, string cityMapId)
    {
        _context = context;
        _cityMapId = cityMapId;
    }

    public string Description => $"下钻城市：{_context.DisplayNameOf(_cityMapId)}";

    public void Apply()
    {
        _previousMapId = _context.ActiveMapId;
        _context.DrillTo(_cityMapId);
    }

    public void Revert()
    {
        if (string.IsNullOrEmpty(_previousMapId))
        {
            return;
        }

        _context.SetActiveMap(_previousMapId);
    }
}
