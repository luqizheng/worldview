using WorldView.Application.Abstractions;
using WorldView.Domain.Projects;

namespace WorldView.Application.Projects;

/// <summary>
/// <see cref="IMapProjectService"/> 的默认实现：协调领域模型与仓储（架构示例）。
/// </summary>
public sealed class MapProjectService : IMapProjectService
{
    private readonly IMapProjectRepository _repository;

    public MapProjectService(IMapProjectRepository repository)
    {
        _repository = repository;
    }

    public CreateMapProjectResult CreateMapProject(CreateMapProjectCommand command)
    {
        try
        {
            var project = MapProject.Create(command.Name);
            _repository.Add(project);
            return CreateMapProjectResult.Ok(ToDto(project));
        }
        catch (ArgumentException ex)
        {
            // 领域拒绝输入属于正常业务分支，转为用例失败结果，不向上抛异常。
            return CreateMapProjectResult.Fail(ex.Message);
        }
    }

    public IReadOnlyList<MapProjectDto> ListProjects() =>
        _repository.List().Select(ToDto).ToArray();

    private static MapProjectDto ToDto(MapProject project) =>
        new(project.Id.Value, project.Name, project.CreatedAt);
}
