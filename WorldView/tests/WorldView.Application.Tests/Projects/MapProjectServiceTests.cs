using WorldView.Application.Projects;

namespace WorldView.Application.Tests.Projects;

public class MapProjectServiceTests
{
    [Fact]
    public void CreateMapProject_WithValidName_AddsProjectAndReturnsDto()
    {
        var repository = new FakeMapProjectRepository();
        var service = new MapProjectService(repository);

        var result = service.CreateMapProject(new CreateMapProjectCommand("  春秋地图  "));

        Assert.True(result.Success);
        Assert.NotNull(result.Project);
        Assert.Equal("春秋地图", result.Project.Name);
        Assert.NotEqual(Guid.Empty, result.Project.Id);
        Assert.True(repository.HasAddBeenCalled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateMapProject_WithBlankName_FailsWithoutTouchingRepository(string name)
    {
        var repository = new FakeMapProjectRepository();
        var service = new MapProjectService(repository);

        var result = service.CreateMapProject(new CreateMapProjectCommand(name));

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.False(repository.HasAddBeenCalled);
    }

    [Fact]
    public void ListProjects_ReturnsAllProjectsInCreationOrder()
    {
        var repository = new FakeMapProjectRepository();
        var service = new MapProjectService(repository);

        service.CreateMapProject(new CreateMapProjectCommand("A"));
        service.CreateMapProject(new CreateMapProjectCommand("B"));

        var projects = service.ListProjects();
        Assert.Equal(2, projects.Count);
        Assert.Equal("A", projects[0].Name);
        Assert.Equal("B", projects[1].Name);
    }
}
