using WorldView.Domain.Projects;

namespace WorldView.Domain.Tests.Projects;

public class MapProjectTests
{
    [Fact]
    public void Create_WithValidName_ReturnsProjectWithTrimmedName()
    {
        var project = MapProject.Create("  新项目  ");

        Assert.NotEqual(Guid.Empty, project.Id.Value);
        Assert.Equal("新项目", project.Name);
        Assert.True(project.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithBlankName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() => MapProject.Create(name));
    }

    [Fact]
    public void Create_ReturnsUniqueIds()
    {
        var first = MapProject.Create("A");
        var second = MapProject.Create("B");

        Assert.NotEqual(first.Id, second.Id);
    }
}
