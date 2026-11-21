using WorldView.Application.Abstractions;
using WorldView.Domain.Projects;

namespace WorldView.Infrastructure.Projects;

/// <summary>
/// 基于内存列表的地图项目仓储（架构示例）。
/// 正式持久化与项目文件格式确定后，由真实实现替换。
/// </summary>
public sealed class InMemoryMapProjectRepository : IMapProjectRepository
{
    private readonly object _gate = new();
    private readonly List<MapProject> _projects = [];

    public void Add(MapProject project)
    {
        lock (_gate)
        {
            _projects.Add(project);
        }
    }

    public IReadOnlyList<MapProject> List()
    {
        lock (_gate)
        {
            // 返回快照，避免外部在枚举期间触发内部集合变更。
            return _projects.ToArray();
        }
    }
}
