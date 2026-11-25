using System;
using System.Collections.Generic;
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
