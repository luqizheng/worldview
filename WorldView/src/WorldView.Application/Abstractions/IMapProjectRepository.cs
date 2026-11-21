using WorldView.Domain.Projects;

namespace WorldView.Application.Abstractions;

/// <summary>
/// 地图项目仓储抽象。实现位于 Infrastructure 层。
/// </summary>
public interface IMapProjectRepository
{
    /// <summary>添加项目。</summary>
    void Add(MapProject project);

    /// <summary>按创建顺序列出全部项目。</summary>
    IReadOnlyList<MapProject> List();
}
