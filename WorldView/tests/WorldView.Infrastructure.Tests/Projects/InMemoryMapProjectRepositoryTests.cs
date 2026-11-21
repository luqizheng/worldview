using WorldView.Domain.Projects;
using WorldView.Infrastructure.Projects;

namespace WorldView.Infrastructure.Tests.Projects;

public class InMemoryMapProjectRepositoryTests
{
    [Fact]
    public void Add_ThenList_ReturnsAddedProject()
    {
        var repository = new InMemoryMapProjectRepository();
        var project = MapProject.Create("内存项目");

        repository.Add(project);

        var listed = repository.List();
        Assert.Single(listed);
        Assert.Equal(project.Id, listed[0].Id);
    }

    [Fact]
    public void List_ReturnsSnapshot_IndependentOfLaterChanges()
    {
        var repository = new InMemoryMapProjectRepository();
        repository.Add(MapProject.Create("早"));

        var firstSnapshot = repository.List();
        repository.Add(MapProject.Create("晚"));

        Assert.Single(firstSnapshot);
        Assert.Equal(2, repository.List().Count);
    }

    [Fact]
    public void Instances_AreIndependent()
    {
        var first = new InMemoryMapProjectRepository();
        var second = new InMemoryMapProjectRepository();

        first.Add(MapProject.Create("仅在第一个仓库"));

        Assert.Single(first.List());
        Assert.Empty(second.List());
    }
}
