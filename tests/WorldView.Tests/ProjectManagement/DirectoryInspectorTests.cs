using System;
using System.IO;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class DirectoryInspectorTests : IDisposable
{
    private readonly string _root;

    public DirectoryInspectorTests()
    {
        _root = ProjectManagementTemp.NewDir("dirinspect");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void IsEmpty_True_For_NewDirectory()
    {
        var dir = Path.Combine(_root, "newdir");
        Directory.CreateDirectory(dir);
        Assert.True(DirectoryInspector.IsEmpty(dir));
    }

    [Fact]
    public void IsEmpty_True_For_NotYetCreated_Directory()
    {
        var dir = Path.Combine(_root, "ghost-dir");
        Assert.True(DirectoryInspector.IsEmpty(dir));
    }

    [Fact]
    public void IsEmpty_False_When_Any_Entry_Exists()
    {
        var dir = Path.Combine(_root, "occupied");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "note.txt"), "x");
        Assert.False(DirectoryInspector.IsEmpty(dir));
    }

    [Fact]
    public void IsEmpty_False_When_Hidden_Entry_Exists()
    {
        var dir = Path.Combine(_root, "hidden");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".gitkeep"), "");
        Assert.False(DirectoryInspector.IsEmpty(dir));
    }

    [Fact]
    public void IsEmpty_False_When_SubDir_Exists()
    {
        var dir = Path.Combine(_root, "withsub");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "child"));
        Assert.False(DirectoryInspector.IsEmpty(dir));
    }

    [Fact]
    public void ValidateForCreate_Throws_DirectoryNotEmpty_If_Any_File()
    {
        var dir = Path.Combine(_root, "non-empty");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "anything"), "x");

        var ex = Assert.Throws<ProjectException>(() => DirectoryInspector.ValidateForCreate(dir));
        Assert.Equal(ProjectErrorCode.DirectoryNotEmpty, ex.Code);
    }

    [Fact]
    public void ValidateForCreate_Throws_InvalidProject_If_Path_Is_File()
    {
        var filePath = Path.Combine(_root, "im-a-file.txt");
        File.WriteAllText(filePath, "x");

        var ex = Assert.Throws<ProjectException>(() => DirectoryInspector.ValidateForCreate(filePath));
        Assert.Equal(ProjectErrorCode.InvalidProject, ex.Code);
    }

    [Fact]
    public void ValidateForCreate_Throws_InvalidProject_If_ProjectYaml_Exists()
    {
        var dir = Path.Combine(_root, "has-yaml");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "project.yaml"), "name: x");

        var ex = Assert.Throws<ProjectException>(() => DirectoryInspector.ValidateForCreate(dir));
        Assert.Equal(ProjectErrorCode.InvalidProject, ex.Code);
    }

    [Fact]
    public void ValidateForCreate_Ok_For_Empty_NewDir()
    {
        var dir = Path.Combine(_root, "fresh");
        Directory.CreateDirectory(dir);
        DirectoryInspector.ValidateForCreate(dir); // 不抛异常
    }

    [Fact]
    public void ValidateForCreate_Ok_For_NotYetCreated_Dir()
    {
        var dir = Path.Combine(_root, "pending");
        DirectoryInspector.ValidateForCreate(dir); // 不抛异常
    }
}
