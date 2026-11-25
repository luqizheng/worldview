using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.ViewModels;

/// <summary>
/// 启动面空状态投影测试：首页列表为空/非空时，供 XAML 绑定的投影必须同步翻转。
/// </summary>
public class StartViewModelTests
{
    [Fact]
    public async Task LoadRecentProjectsAsync_WithoutEntries_ExposesEmptyState()
    {
        var vm = new StartViewModel(null!, null!, new StubRecentProjectsStore());

        await vm.LoadRecentProjectsAsync();

        Assert.Empty(vm.RecentProjects);
        Assert.False(vm.HasRecentProjects);
        Assert.True(vm.IsRecentProjectsEmpty);
    }

    [Fact]
    public async Task LoadRecentProjectsAsync_WithEntries_HidesEmptyState()
    {
        var store = new StubRecentProjectsStore
        {
            Entries = new[]
            {
                new RecentProjectEntry
                {
                    PackagePath = "/projects/demo.wvproject",
                    LastOpenedAtUtc = DateTimeOffset.UtcNow,
                },
            },
        };
        var vm = new StartViewModel(null!, null!, store);

        await vm.LoadRecentProjectsAsync();

        Assert.Single(vm.RecentProjects);
        Assert.True(vm.HasRecentProjects);
        Assert.False(vm.IsRecentProjectsEmpty);
    }

    [Fact]
    public async Task LoadRecentProjectsAsync_StoreFailure_ExposesDiagnostic_AndLogs()
    {
        var log = new RecordingLog();
        var vm = new StartViewModel(null!, null!, new FailingRecentProjectsStore(), log);

        await vm.LoadRecentProjectsAsync(); // 不抛出：启动窗口仍可用

        Assert.Empty(vm.RecentProjects);
        Assert.True(vm.IsRecentProjectsEmpty);
        Assert.False(vm.HasRecentProjects);
        Assert.Contains("最近项目加载失败", vm.StatusText, StringComparison.Ordinal);
        Assert.Contains("最近项目加载失败", vm.RecentProjectsLoadDiagnostic, StringComparison.Ordinal);
        Assert.Single(log.Errors);
    }

    private sealed class FailingRecentProjectsStore : IRecentProjectsStore
    {
        public Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default)
            => throw new IOException("注入：最近项目存储读取失败。");

        public Task RecordOpenedAsync(
            string packagePath,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingLog : IAppLog
    {
        public List<string> Errors { get; } = new();

        public void Info(string category, string message) { }

        public void Warn(string category, string message, Exception? exception = null) { }

        public void Error(string category, string message, Exception? exception = null)
            => Errors.Add($"{category}: {message}: {exception?.Message}");
    }

    private sealed class StubRecentProjectsStore : IRecentProjectsStore
    {
        public IReadOnlyList<RecentProjectEntry> Entries { get; init; } = Array.Empty<RecentProjectEntry>();

        public Task<IReadOnlyList<RecentProjectEntry>> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Entries);

        public Task RecordOpenedAsync(
            string packagePath,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
