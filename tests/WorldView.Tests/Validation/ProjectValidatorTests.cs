using WorldView.Domain;
using WorldView.Domain.Validation;
using WorldView.Tests.Support;
using Xunit;

namespace WorldView.Tests.Validation;

/// <summary>
/// ProjectValidator 关键规则(spec.md §6.5 + checklist K1/K3)。
///
/// 覆盖:
///  - E_PROJECT_VERSION_MISSING(project_version 为空)
///  - E_MAP_INDEX_MISSING(map_index 引用的文件不存在)
///  - E_REF_ABSOLUTE / E_REF_PARENT(map_index 引用违规)
///  - W_CHARACTER_INDEX_MISSING(character 引用文件不存在,不阻塞 save)
/// </summary>
public class ProjectValidatorTests
{
    private static string ScratchDir => TestPaths.CreateScratchDir("worldview-projectvalidator-tests");

    [Fact]
    public void Valid_Minimal_World_Passes()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "测试",
            DefaultTime: new DefaultTime(1042, 3, 12),
            Era: new EraSpec(),
            MapIndex: Array.Empty<MapIndexEntry>(),
            CharacterIndex: Array.Empty<MapIndexEntry>());

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.True(result.IsValid, result.ToString());
    }

    [Fact]
    public void Missing_ProjectVersion_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: Array.Empty<MapIndexEntry>(),
            CharacterIndex: Array.Empty<MapIndexEntry>());

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_PROJECT_VERSION_MISSING");
    }

    [Fact]
    public void MapIndex_Ref_To_Missing_File_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: new[] { new MapIndexEntry("maps/does_not_exist.yaml") },
            CharacterIndex: Array.Empty<MapIndexEntry>());

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_MAP_INDEX_MISSING");
    }

    [Fact]
    public void MapIndex_Absolute_Path_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: new[] { new MapIndexEntry(@"C:\abs\path\map.yaml") },
            CharacterIndex: Array.Empty<MapIndexEntry>());

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_ABSOLUTE");
    }

    [Fact]
    public void MapIndex_DotDot_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: new[] { new MapIndexEntry("../outside/map.yaml") },
            CharacterIndex: Array.Empty<MapIndexEntry>());

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_PARENT");
    }

    [Fact]
    public void CharacterIndex_Missing_File_Is_Warning_Only()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: Array.Empty<MapIndexEntry>(),
            CharacterIndex: new[] { new MapIndexEntry("characters/nobody.yaml") });

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        // MVP 决策:character 缺失不阻塞 save(spec.md §6.5 规则 5)
        Assert.True(result.IsValid, result.ToString());
        Assert.Contains(result.Warnings, w => w.Code == "W_CHARACTER_INDEX_MISSING");
    }

    [Fact]
    public void CharacterIndex_Absolute_Path_Fails()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var world = new World(
            ProjectVersion: "0.1",
            Name: "x",
            DefaultTime: new DefaultTime(1),
            Era: new EraSpec(),
            MapIndex: Array.Empty<MapIndexEntry>(),
            CharacterIndex: new[] { new MapIndexEntry("/etc/passwd") });

        var result = new ProjectValidator().Validate(world, "project.yaml", ScratchDir);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "E_REF_ABSOLUTE");
    }
}
