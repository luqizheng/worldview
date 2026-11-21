using WorldView.Application.Projects;

namespace WorldView.Application.Abstractions;

/// <summary>
/// 地图项目应用服务。供 UI / ViewModel 调用，封装用例流程；
/// UI 层不得绕过本接口直接操作领域模型或仓储。
/// </summary>
public interface IMapProjectService
{
    /// <summary>用例：创建地图项目。</summary>
    CreateMapProjectResult CreateMapProject(CreateMapProjectCommand command);

    /// <summary>按创建顺序列出全部项目。</summary>
    IReadOnlyList<MapProjectDto> ListProjects();
}
