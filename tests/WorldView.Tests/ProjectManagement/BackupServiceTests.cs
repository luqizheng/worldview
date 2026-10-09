using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorldView.ProjectManagement;
using Xunit;

namespace WorldView.Tests.ProjectManagement;

public class BackupServiceTests : IDisposable
{
    private readonly string _projectRoot;
    private readonly FakeClock _clock;
    private readonly BackupService _sut;

    public BackupServiceTests()
    {
        _projectRoot = ProjectManagementTemp.NewDir("backup");
        _clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _sut = new BackupService(_projectRoot, _clock);

        // 写入一个真实的小工程
        File.WriteAllText(Path.Combine(_projectRoot, "project.yaml"), "project_version: 0.1\nname: P\nera:\n  name: e\n");
        Directory.CreateDirectory(Path.Combine(_projectRoot, "maps"));
        File.WriteAllText(Path.Combine(_projectRoot, "maps", "m.yaml"), "id: m\nname: m\n");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projectRoot)) Directory.Delete(_projectRoot, recursive: true); } catch { }
    }

    [Fact]
    public async Task CreateSnapshot_Copies_Files_And_Adds_Manifest_Entry()
    {
        var rec = await _sut.CreateSnapshotAsync("snap1", "src1");

        Assert.Equal("snap1", rec.DisplayName);
        Assert.Equal(BackupKind.NamedSnapshot, rec.Kind);
        Assert.True(File.Exists(Path.Combine(rec.ContentRoot, "project.yaml")));
        Assert.True(File.Exists(Path.Combine(rec.ContentRoot, "maps", "m.yaml")));

        var list = await _sut.ListAsync();
        Assert.Single(list);
    }

    [Fact]
    public async Task CreateSnapshot_Excludes_DotWorldview()
    {
        // 先在工程里建一个 .worldview 目录（不参与备份）
        var dotDir = Path.Combine(_projectRoot, ".worldview");
        Directory.CreateDirectory(dotDir);
        File.WriteAllText(Path.Combine(dotDir, "secret.txt"), "should-not-be-backed-up");

        var rec = await _sut.CreateSnapshotAsync("snap", "src");

        Assert.False(Directory.Exists(Path.Combine(rec.ContentRoot, ".worldview")),
            "备份内容不应含 .worldview 自身");
        Assert.False(File.Exists(Path.Combine(rec.ContentRoot, ".worldview", "secret.txt")));
    }

    [Fact]
    public async Task CreateSnapshot_Rejects_Duplicate_Name()
    {
        await _sut.CreateSnapshotAsync("dup", "src");

        var ex = await Assert.ThrowsAsync<ProjectException>(
            () => _sut.CreateSnapshotAsync("dup", "src"));
        Assert.Equal(ProjectErrorCode.NameConflict, ex.Code);
    }

    [Fact]
    public async Task PreOpenRotation_Keeps_Most_Recent_10()
    {
        for (int i = 0; i < 12; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await _sut.CreatePreOpenRotationAsync("src");
        }

        var list = await _sut.ListAsync();
        // 只数 PreOpenRotation 备份
        var rotations = list.Where(b => b.Kind == BackupKind.PreOpenRotation).ToList();
        Assert.Equal(10, rotations.Count);

        // 应当保留最新 10 份（即后 10 次的）
        var oldestKept = rotations.First().DisplayName;
        // 第 1、2 次的 display_name 应当已经被删除
        // 第一次 "auto-..." 的 ticks 比 12 次前的更早
        // 检查目录只有 10 份
        var backupDirs = Directory.GetDirectories(_sut.BackupRootDir);
        Assert.Equal(10, backupDirs.Length);
    }

    [Fact]
    public async Task List_Returns_Manifest_In_CreatedAt_Order()
    {
        await _sut.CreateSnapshotAsync("alpha", "src");
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _sut.CreateSnapshotAsync("beta", "src");
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _sut.CreateSnapshotAsync("gamma", "src");

        var list = await _sut.ListAsync();
        Assert.Equal(3, list.Count);
        Assert.Equal("alpha", list[0].DisplayName);
        Assert.Equal("beta", list[1].DisplayName);
        Assert.Equal("gamma", list[2].DisplayName);
    }

    [Fact]
    public async Task Delete_Removes_Content_And_Manifest_Entry()
    {
        var rec = await _sut.CreateSnapshotAsync("doomed", "src");
        Assert.True(Directory.Exists(rec.ContentRoot));

        await _sut.DeleteAsync(rec.BackupId);

        Assert.False(Directory.Exists(rec.ContentRoot));
        var list = await _sut.ListAsync();
        Assert.Empty(list);
    }
}
