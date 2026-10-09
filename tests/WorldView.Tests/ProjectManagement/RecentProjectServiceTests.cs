using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class RecentProjectServiceTests : IDisposable
{
    private readonly string _appDataDir;
    private readonly FakeClock _clock;
    private readonly IAppDataLocator _locator;
    private readonly FakeFileSystem _fs;

    public RecentProjectServiceTests()
    {
        _appDataDir = ProjectManagementTemp.NewDir("recent");
        _clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _locator = new FixedAppDataLocator(_appDataDir);
        _fs = new FakeFileSystem();
    }

    private RecentProjectService NewSut() => new(_locator, _clock, _fs);

    public void Dispose()
    {
        try { if (Directory.Exists(_appDataDir)) Directory.Delete(_appDataDir, recursive: true); } catch { }
    }

    [Fact]
    public void RecordOpened_Appends_New_Entry()
    {
        _fs.Exists(@"C:\projA");
        var sut = NewSut();
        sut.RecordOpened(@"C:\projA", "A");
        var list = sut.List();
        Assert.Single(list);
        Assert.Equal(@"C:\projA", list[0].ProjectRoot);
        Assert.Equal("A", list[0].DisplayName);
        Assert.False(list[0].IsMissing);
    }

    [Fact]
    public void RecordOpened_Dedupes_By_Path()
    {
        _fs.Exists(@"C:\projA");
        var sut = NewSut();
        sut.RecordOpened(@"C:\projA", "A");
        _clock.Advance(TimeSpan.FromHours(1));
        sut.RecordOpened(@"C:\projA", "A-renamed");
        var list = sut.List();
        Assert.Single(list);
        Assert.Equal("A-renamed", list[0].DisplayName);
        // last_opened_at 推进
        Assert.Equal(new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc), list[0].LastOpenedAtUtc);
    }

    [Fact]
    public void RecordOpened_Caps_At_10_Oldest_Evicted()
    {
        for (int i = 0; i < 12; i++) _fs.Exists($@"C:\proj{i:D2}");
        var sut = NewSut();
        for (int i = 0; i < 12; i++)
        {
            sut.RecordOpened($@"C:\proj{i:D2}", "P" + i);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }
        var list = sut.List();
        Assert.Equal(10, list.Count);
        // 最早的 proj00 / proj01 应被淘汰
        Assert.DoesNotContain(list, e => e.ProjectRoot == @"C:\proj00");
        Assert.DoesNotContain(list, e => e.ProjectRoot == @"C:\proj01");
        Assert.Contains(list, e => e.ProjectRoot == @"C:\proj11");
    }

    [Fact]
    public void List_Marks_Missing_Paths()
    {
        // 不向 _fs 注册 C:\does\not\exist, 仍走 SystemFileSystem 默认 (List_Marks_Missing_Paths 验证默认路径走真实目录检查)
        var sut = new RecentProjectService(_locator, _clock);
        sut.RecordOpened(@"C:\does\not\exist", "ghost");
        var list = sut.List();
        Assert.Single(list);
        Assert.True(list[0].IsMissing);
    }

    [Fact]
    public void Corrupted_File_Surfaces_Exception_Without_Overwriting()
    {
        _fs.Exists(@"C:\projA");
        var sut = NewSut();
        // 先写一个合法条目
        sut.RecordOpened(@"C:\projA", "A");
        var list = sut.List();
        Assert.Single(list);
        // 手动破坏清单文件
        var manifestPath = Path.Combine(_appDataDir, ".worldview", "recent_projects.yaml");
        File.WriteAllText(manifestPath, "this is: not: valid yaml [[[");
        // List 应抛 RecentProjectsCorrupted
        var ex = Assert.Throws<ProjectException>(() => sut.List());
        Assert.Equal(ProjectErrorCode.RecentProjectsCorrupted, ex.Code);
        // 原文件不应被覆盖
        var stillThere = File.ReadAllText(manifestPath);
        Assert.Contains("not: valid yaml", stillThere);
    }

    [Fact]
    public void RecordOpened_Normalizes_Path()
    {
        var normalized = Path.GetFullPath(@"C:\Proj\.\Sub");
        _fs.Exists(normalized);
        var sut = NewSut();
        sut.RecordOpened(@"C:\Proj\.\Sub", "S");
        var list = sut.List();
        // 期望路径被 Path.GetFullPath 规范化（去 ".\"）
        Assert.Single(list);
        Assert.Equal(normalized, list[0].ProjectRoot);
    }
}

internal sealed class FixedAppDataLocator : IAppDataLocator
{
    private readonly string _root;
    public FixedAppDataLocator(string root) { _root = root; }
    public string GetUserConfigDir()
    {
        Directory.CreateDirectory(_root);
        return _root;
    }
}
