using System;
using System.IO;
using System.Text;
using WorldView.ProjectManagement;
using WorldView.Storage.Yaml;
using WorldView.Domain;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class ProjectWriteCoordinatorTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectWriteCoordinator _sut;

    public ProjectWriteCoordinatorTests()
    {
        _root = ProjectManagementTemp.NewDir("writecoord");
        _sut = new ProjectWriteCoordinator();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    // ---------- 单文件原子写入 ----------

    [Fact]
    public void WriteFileAtomic_Creates_Target_With_Exact_Content()
    {
        var target = Path.Combine(_root, "sub", "file.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        _sut.WriteFileAtomic(target, "hello world");

        Assert.True(File.Exists(target));
        Assert.Equal("hello world", File.ReadAllText(target));
    }

    [Fact]
    public void WriteFileAtomic_Overwrites_Existing_File()
    {
        var target = Path.Combine(_root, "file.txt");
        File.WriteAllText(target, "old");
        Assert.Equal("old", File.ReadAllText(target));

        _sut.WriteFileAtomic(target, "new");

        Assert.Equal("new", File.ReadAllText(target));
    }

    [Fact]
    public void WriteFileAtomic_Cleans_Temp_On_Simulated_Failure()
    {
        // 使用一个被 read-only 占用的子目录，让 WriteAllText 失败；验证临时文件被清理。
        var sub = Path.Combine(_root, "readonly-dir");
        Directory.CreateDirectory(sub);
        // 在 Windows 上对目录加 ACL 不太方便；用更简单的方式：先让文件占用——但这要在 Windows 上靠 attributes。
        // 简化方案：让 writer lambda 抛异常。给 WriteFileAtomic 注入一个会抛的 writer。
        // 由于当前 WriteFileAtomic 不接受注入的 writer，我们构造一个无法写入的目标（路径指向一个已存在但内容不能改的场景）。
        // 这里用更可靠的方式：传入一个"writer 抛异常" 的扩展调用：通过反射或者新加一个内部接口。
        // 简洁起见：直接写一个会抛的辅助方法 ThrowingWriter 调用底层 writer 后清理临时文件。
        // 但实际上，sandbox 写不动 read-only 目录不会触发——因为 Windows 进程对 .NET 没天然只读目录。
        // 替代方案：先创建一个目标文件，然后把它占住（FileShare.None）。
        var target = Path.Combine(_root, "locked.txt");
        File.WriteAllText(target, "old");
        using (var fs = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // File.Replace 需要目标存在并能 swap；由于 fs 占用 target，replace 会失败。
            var ex = Assert.Throws<ProjectException>(() => _sut.WriteFileAtomic(target, "new"));
            Assert.Equal(ProjectErrorCode.SaveFailed, ex.Code);
        }

        // 验证：没有遗留临时文件（命名以 .tmp- 开头）
        var leftovers = Directory.GetFiles(_root, ".tmp-*.tmp");
        Assert.Empty(leftovers);
    }

    // ---------- 多文件工程原子写入 ----------

    [Fact]
    public void WriteProjectAtomic_Stages_Then_Replaces()
    {
        // 准备一个真实有效的小工程：用 MapFactory 生成空白 Map，再让 World 持有它。
        // 这样保证 schema 字段全部正确（width/height, default_granularity, encoding 等）。
        var targetRoot = Path.Combine(_root, "project");

        _sut.WriteProjectAtomic(targetRoot, staging =>
        {
            File.WriteAllText(Path.Combine(staging, "project.yaml"), BuildValidProjectYaml());
            var mapDir = Path.Combine(staging, "maps", "world");
            Directory.CreateDirectory(mapDir);
            File.WriteAllText(Path.Combine(mapDir, "map_world_1.yaml"), BuildValidMapYaml());
        });

        Assert.True(File.Exists(Path.Combine(targetRoot, "project.yaml")));
        Assert.True(File.Exists(Path.Combine(targetRoot, "maps", "world", "map_world_1.yaml")));

        // 替换后 Loader 应当能加载并通过校验
        var reloaded = ProjectLoader.Load(targetRoot);
        Assert.Equal(1, reloaded.World.MapIndex?.Count ?? 0);
        Assert.True(reloaded.Validation.IsValid,
            "validation failed: " + string.Join("; ",
                reloaded.Validation.Errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    private static string BuildValidProjectYaml()
    {
        var world = new World(
            Name: "TestWorld",
            DefaultTime: new DefaultTime(Year: 1, Month: 1, Day: 1),
            Era: new EraSpec(
                Name: "test_era",
                StartYear: 1,
                MonthsPerYear: 12,
                DaysPerMonth: new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 },
                HoursPerDay: 24,
                MinutesPerHour: 60,
                SecondsPerMinute: 60),
            MapIndex: new[] { new MapIndexEntry("maps/world/map_world_1.yaml") },
            CharacterIndex: Array.Empty<MapIndexEntry>(),
            Assets: null,
            Settings: new WorldSettings(Autosave: false, BackupKeep: 5));
        return YamlSchemas.Serialize(world);
    }

    private static string BuildValidMapYaml()
    {
        var map = MapFactory.CreateBlankMap(
            type: MapType.World,
            id: "map_world_1",
            displayName: "Demo World",
            defaultLayers: Array.Empty<LayerRef>());
        return YamlSchemas.Serialize(map);
    }

    [Fact]
    public void WriteProjectAtomic_Rolls_Back_When_Loader_Fails()
    {
        var targetRoot = Path.Combine(_root, "project");
        Directory.CreateDirectory(targetRoot);
        // 先用合法 yaml 写一个老工程作为基线
        File.WriteAllText(Path.Combine(targetRoot, "project.yaml"), BuildValidProjectYaml());
        var originalContent = File.ReadAllText(Path.Combine(targetRoot, "project.yaml"));

        // 回调故意写出一个 Validation 失败的工程：map_index 引用不存在的 map 文件。
        var ex = Assert.Throws<ProjectException>(() => _sut.WriteProjectAtomic(targetRoot, staging =>
        {
            File.WriteAllText(Path.Combine(staging, "project.yaml"), BuildValidProjectYaml());
            // 注意：故意不创建 map_index 引用 maps/world/map_world_1.yaml → 校验失败。
        }));
        Assert.Equal(ProjectErrorCode.SaveFailed, ex.Code);

        // 验证：原始文件未被破坏
        Assert.True(File.Exists(Path.Combine(targetRoot, "project.yaml")));
        Assert.Equal(originalContent, File.ReadAllText(Path.Combine(targetRoot, "project.yaml")));

        // 验证：staging 目录被清理
        var stagingLeftovers = Directory.GetDirectories(_root, "project.tmp-*");
        Assert.Empty(stagingLeftovers);
    }

    [Fact]
    public void WriteProjectAtomic_Throws_SaveFailed_With_InnerException_On_Disk_Error()
    {
        // 传入一个 writer 让其内部抛 IOException，验证被包装为 SaveFailed。
        var targetRoot = Path.Combine(_root, "project");
        Directory.CreateDirectory(targetRoot);

        var ex = Assert.Throws<ProjectException>(() => _sut.WriteProjectAtomic(targetRoot, staging =>
        {
            throw new IOException("simulated disk failure");
        }));
        Assert.Equal(ProjectErrorCode.SaveFailed, ex.Code);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<IOException>(ex.InnerException);
    }
}
