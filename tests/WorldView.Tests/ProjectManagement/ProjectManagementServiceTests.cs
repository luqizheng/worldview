using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WorldView.Domain;
using WorldView.ProjectManagement;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

/// <summary>
/// ProjectManagementService 测试套件（W5）。
///
/// <para>覆盖强约束：C-N2 / C-I1 / C-S2 / C-D1 / C-M内 / C-M跨 / C-C1 / C-C2。</para>
///
/// <para>设计：服务层依赖 <see cref="IProjectOperations"/> 抽象；
/// 本套件提供 <c>FakeProjectOperations</c> 内存实现，无需启动 Godot 主循环。</para>
/// </summary>
public class ProjectManagementServiceTests : IDisposable
{
    private readonly string _appDataDir;
    private readonly FakeClock _clock;
    private readonly IAppDataLocator _locator;
    private readonly RecentProjectService _recent;
    private readonly FakeProjectOperations _ops;

    public ProjectManagementServiceTests()
    {
        _appDataDir = ProjectManagementTemp.NewDir("pmsvc");
        _clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _locator = new FixedAppDataLocator(_appDataDir);
        _recent = new RecentProjectService(_locator, _clock);
        _ops = new FakeProjectOperations();
        // 重置：避免上一个测试的静态状态污染
        ProjectManagementService.ResetForTest();
        ProjectManagementService.Initialize(_ops, _recent, backupFactory: null, dirty: null);
    }

    public void Dispose()
    {
        ProjectManagementService.ResetForTest();
        try { if (Directory.Exists(_appDataDir)) Directory.Delete(_appDataDir, recursive: true); } catch { /* 吞 */ }
    }

    // ---------------------------------------------------------------- C-N2

    /// <summary>
    /// C-N2：NewProject 在干净目录创建最小骨架（project.yaml + 一张空白世界地图）；
    /// 不复用 examples/sample_world；map_index 含 1 项。
    /// </summary>
    [Fact]
    public void NewProject_Creates_Minimal_Skeleton()
    {
        var parent = ProjectManagementTemp.NewDir("n2-parent");

        ProjectManagementService.NewProject(parent, "world_one");

        var root = Path.Combine(parent, "world_one");
        Assert.True(Directory.Exists(root), "工程根应被创建");
        Assert.True(File.Exists(Path.Combine(root, "project.yaml")), "project.yaml 必须存在（C-N2）");

        // maps/ 目录下应有一张世界地图
        var mapsDir = Path.Combine(root, "maps", "world");
        Assert.True(Directory.Exists(mapsDir), "maps/world/ 目录必须存在");
        var mapDirs = Directory.GetDirectories(mapsDir);
        Assert.Single(mapDirs);
        Assert.True(File.Exists(Path.Combine(mapDirs[0], "main.yaml")), "空白世界地图 main.yaml 必须存在");

        // 解析后 map_index 应含 1 项，指向新建地图
        var world = YamlSchemas.Deserialize<World>(File.ReadAllText(Path.Combine(root, "project.yaml")));
        Assert.NotNull(world);
        Assert.Single(world!.MapIndex);
        Assert.StartsWith("maps/world/", world.MapIndex[0].RefPath);
        Assert.EndsWith("/main.yaml", world.MapIndex[0].RefPath);

        // 加载后 _ops 反映 Loaded
        Assert.True(_ops.IsLoaded);
        Assert.NotNull(_ops.World);
        Assert.Equal("world_one", _ops.World!.Name);
    }

    // ---------------------------------------------------------------- C-I1

    /// <summary>
    /// C-I1：打开不含 project.yaml 的目录 → 抛 ProjectException.InvalidProject。
    /// </summary>
    [Fact]
    public void OpenProject_Missing_Yaml_Throws_Invalid()
    {
        var emptyDir = ProjectManagementTemp.NewDir("i1-empty");

        var ex = Assert.Throws<ProjectException>(
            () => ProjectManagementService.OpenProject(emptyDir));
        Assert.Equal(ProjectErrorCode.InvalidProject, ex.Code);
    }

    /// <summary>
    /// C-I1 补充：不存在的根目录 → 抛 ProjectException.InvalidProject。
    /// </summary>
    [Fact]
    public void OpenProject_Missing_Dir_Throws_Invalid()
    {
        var ghost = Path.Combine(AppContext.BaseDirectory, "pmsvc-ghost-" + Guid.NewGuid().ToString("N"));
        var ex = Assert.Throws<ProjectException>(
            () => ProjectManagementService.OpenProject(ghost));
        Assert.Equal(ProjectErrorCode.InvalidProject, ex.Code);
    }

    // ---------------------------------------------------------------- C-S2

    /// <summary>
    /// C-S2：打开失败时抛 ProjectException.InvalidProject 且消息含引导性字样。
    /// </summary>
    [Fact]
    public void OpenProject_Failure_Message_Guides_New_Project()
    {
        var emptyDir = ProjectManagementTemp.NewDir("s2-empty");

        var ex = Assert.Throws<ProjectException>(
            () => ProjectManagementService.OpenProject(emptyDir));
        Assert.Equal(ProjectErrorCode.InvalidProject, ex.Code);
        Assert.True(
            ex.Message.Contains("不是工程") || ex.Message.Contains("新建项目"),
            $"错误消息应引导用户走'新建项目'流程，实际：{ex.Message}");
    }

    // ---------------------------------------------------------------- C-D1

    /// <summary>
    /// C-D1：NewProject 后 RecentProjectService 包含新项，display_name = name（目录名）；
    /// 不弹输入框（默认值走目录名）。
    /// </summary>
    [Fact]
    public void NewProject_Records_Recent_With_Default_DisplayName()
    {
        var parent = ProjectManagementTemp.NewDir("d1-parent");

        ProjectManagementService.NewProject(parent, "alpha_world");

        var recents = _recent.List();
        Assert.Single(recents);
        Assert.Equal("alpha_world", recents[0].DisplayName); // C-D1：display_name = 目录名
        Assert.Equal(Path.GetFullPath(Path.Combine(parent, "alpha_world")), recents[0].ProjectRoot);
    }

    // ---------------------------------------------------------------- C-M内

    /// <summary>
    /// C-M内：AddMap 后 RecentProjectService 不变（不动 recents / last_opened_at）。
    /// </summary>
    [Fact]
    public void AddMap_Does_Not_Mutate_Recents()
    {
        var parent = ProjectManagementTemp.NewDir("minternal-parent");
        ProjectManagementService.NewProject(parent, "beta_world");
        var beforeRecents = _recent.List();

        var newMap = ProjectManagementService.AddMap(MapType.World, "新大陆", parentMapId: null);

        Assert.NotNull(newMap);
        var afterRecents = _recent.List();

        // recents 列表引用一致（未变）
        Assert.Equal(beforeRecents.Count, afterRecents.Count);
        Assert.Equal(beforeRecents[0].DisplayName, afterRecents[0].DisplayName);
        Assert.Equal(beforeRecents[0].ProjectRoot, afterRecents[0].ProjectRoot);
        Assert.Equal(beforeRecents[0].LastOpenedAt, afterRecents[0].LastOpenedAt); // C-M内：last_opened_at 不变
    }

    // ---------------------------------------------------------------- C-C1

    /// <summary>
    /// C-C1：project.yaml 含 kind: world_view_project + schema_version: 1。
    /// </summary>
    [Fact]
    public void NewProject_Writes_Kind_And_SchemaVersion_In_ProjectYaml()
    {
        var parent = ProjectManagementTemp.NewDir("cc1-parent");

        ProjectManagementService.NewProject(parent, "gamma_world");

        var yamlPath = Path.Combine(parent, "gamma_world", "project.yaml");
        var text = File.ReadAllText(yamlPath);
        Assert.Contains("kind:", text);
        Assert.Contains("world_view_project", text);
        Assert.Contains("schema_version:", text);

        // 反序列化能读回
        var world = YamlSchemas.Deserialize<World>(text);
        Assert.NotNull(world);
        Assert.Equal("world_view_project", world!.Kind);
        Assert.Equal(1, world.SchemaVersion);
    }

    // ---------------------------------------------------------------- C-C2

    /// <summary>
    /// C-C2：maps/ 目录即清单；不维护"显式以外的 map_index"。
    /// 这里检查：map_index 字段包含 1 项（C-C2 在 v0.1 实际由 ProjectLoader 读取 → 必须写）。
    /// </summary>
    [Fact]
    public void NewProject_Writes_MapIndex_With_One_Entry()
    {
        var parent = ProjectManagementTemp.NewDir("cc2-parent");

        ProjectManagementService.NewProject(parent, "delta_world");

        var world = YamlSchemas.Deserialize<World>(
            File.ReadAllText(Path.Combine(parent, "delta_world", "project.yaml")));
        Assert.NotNull(world);
        Assert.Single(world!.MapIndex);
        Assert.EndsWith("/main.yaml", world.MapIndex[0].RefPath);
    }

    // ---------------------------------------------------------------- C-M跨

    /// <summary>
    /// C-M跨：SwitchProject 在 dirty 状态下无决策 → 抛 ProjectHasUnsavedChanges。
    /// </summary>
    [Fact]
    public void SwitchProject_Dirty_Without_Decision_Throws()
    {
        // 重新 Initialize 以传入 DirtyStateService
        var parent1 = ProjectManagementTemp.NewDir("mcross-p1");
        var parent2 = ProjectManagementTemp.NewDir("mcross-p2");
        var firstRoot = Path.Combine(parent1, "first");
        var secondRoot = Path.Combine(parent2, "second");
        // 先建好 first + second 工程
        ProjectWriteCoordinatorHelpers.CreateMinimalProject(firstRoot, "first");
        ProjectWriteCoordinatorHelpers.CreateMinimalProject(secondRoot, "second");

        var dirty = new DirtyStateService();
        ProjectManagementService.ResetForTest();
        ProjectManagementService.Initialize(_ops, _recent, backupFactory: null, dirty: dirty);

        // 加载 first（无脏）
        ProjectManagementService.OpenProject(firstRoot);
        Assert.Equal("first", _ops.World!.Name);

        // 注入 dirty
        dirty.MarkDirty();

        var ex = Assert.Throws<ProjectException>(
            () => ProjectManagementService.SwitchProject(secondRoot));
        Assert.Equal(ProjectErrorCode.ProjectHasUnsavedChanges, ex.Code);
    }

    /// <summary>
    /// C-M跨：SwitchProject + Cancel → 早返回，不切换。
    /// </summary>
    [Fact]
    public void SwitchProject_Dirty_Cancel_EarlyReturns()
    {
        var parent1 = ProjectManagementTemp.NewDir("mcross-cancel-p1");
        var parent2 = ProjectManagementTemp.NewDir("mcross-cancel-p2");
        var firstRoot = Path.Combine(parent1, "first");
        var secondRoot = Path.Combine(parent2, "second");
        ProjectWriteCoordinatorHelpers.CreateMinimalProject(firstRoot, "first");
        ProjectWriteCoordinatorHelpers.CreateMinimalProject(secondRoot, "second");

        var dirty = new DirtyStateService();
        ProjectManagementService.ResetForTest();
        ProjectManagementService.Initialize(_ops, _recent, backupFactory: null, dirty: dirty);

        ProjectManagementService.OpenProject(firstRoot);
        Assert.Equal("first", _ops.World!.Name);

        dirty.MarkDirty();
        // 不抛
        ProjectManagementService.SwitchProject(secondRoot, ProjectCloseDecision.Cancel);
        // 仍为 first
        Assert.Equal("first", _ops.World!.Name);
    }

    // ---------------------------------------------------------------- Reset

    /// <summary>
    /// ResetForTest 清静态状态：unbind 后 ListRecents 走空 fallback，cache 重置。
    /// </summary>
    [Fact]
    public void ResetForTest_Clears_Static_State()
    {
        // 触发一次 cache
        var firstList = ProjectManagementService.ListRecents();
        Assert.NotNull(firstList);

        // Reset：清静态字段（含 cache）
        ProjectManagementService.ResetForTest();
        var empty = ProjectManagementService.ListRecents();
        Assert.Empty(empty); // unbind → 空 fallback

        // 重新 Initialize 后正常
        ProjectManagementService.Initialize(_ops, _recent, backupFactory: null, dirty: null);
        var recents = ProjectManagementService.ListRecents();
        Assert.NotNull(recents);
    }
}

/// <summary>
/// 内存 IProjectOperations 实现；用于不启动 Godot 主循环的测试。
/// </summary>
internal sealed class FakeProjectOperations : IProjectOperations
{
    private World? _world;
    private readonly List<MapDoc> _maps = new();
    private bool _isDirty;

    public bool IsLoaded => _world is not null;
    public bool IsDirty => _isDirty;

    public string? ProjectRoot => _projectRoot;
    private string? _projectRoot;

    public World? World => _world;
    public IReadOnlyList<MapDoc> Maps => _maps;

    public void LoadProject(string path)
    {
        // 真实加载走 ProjectLoader；本测试只读 project.yaml
        var yamlPath = Path.Combine(path, "project.yaml");
        if (!File.Exists(yamlPath))
        {
            throw new DirectoryNotFoundException($"project.yaml 不存在：{yamlPath}");
        }
        _projectRoot = path;
        _world = YamlSchemas.Deserialize<World>(File.ReadAllText(yamlPath));
        _maps.Clear();
    }

    public bool SaveProject() => true;

    public void ReplaceMap(MapDoc map)
    {
        for (int i = 0; i < _maps.Count; i++)
        {
            if (_maps[i].Id == map.Id)
            {
                _maps[i] = map;
                return;
            }
        }
        _maps.Add(map);
    }

    public void AddMap(MapDoc map, string refPath)
    {
        _maps.Add(map);
        if (_world is not null)
        {
            _world = _world with
            {
                MapIndex = new List<MapIndexEntry>(_world.MapIndex) { new MapIndexEntry(refPath) }.AsReadOnly(),
            };
        }
    }

    public void DrillTo(string mapId)
    {
        // no-op
    }

    public void UnloadProject()
    {
        _projectRoot = null;
        _world = null;
        _maps.Clear();
        _isDirty = false;
    }

    public void NotifyHint(string text)
    {
        // no-op
    }
}

/// <summary>
/// 测试辅助：构造一个最小有效工程目录（含 project.yaml + 一张空白世界地图）。
/// </summary>
internal static class ProjectWriteCoordinatorHelpers
{
    public static void CreateMinimalProject(string root, string name)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.yaml"),
            $"name: {name}\nkind: world_view_project\nschema_version: 1\n");
        var mapDir = Path.Combine(root, "maps", "world", "map_world_001");
        Directory.CreateDirectory(mapDir);
        File.WriteAllText(Path.Combine(mapDir, "main.yaml"),
            "id: map_world_001\nname: blank\ntype: world\n");
    }
}
