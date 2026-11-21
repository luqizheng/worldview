using WorldView.Application.Abstractions;
using WorldView.Domain.Projects;

namespace WorldView.Application.Tests.Projects;

/// <summary>测试用内存仓储，记录 Add 调用以供断言。</summary>
public sealed class FakeMapProjectRepository : IMapProjectRepository
{
    public List<MapProject> Added { get; } = [];

    public bool HasAddBeenCalled => Added.Count > 0;

    public void Add(MapProject project) => Added.Add(project);

    public IReadOnlyList<MapProject> List() => Added;
}
