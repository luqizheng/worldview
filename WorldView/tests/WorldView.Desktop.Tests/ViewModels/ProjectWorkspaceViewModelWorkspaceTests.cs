using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.ViewModels;

/// <summary>
/// WP-UI-04B B4（G3 UI 侧）：ViewModel 消费 ProjectSession.CloseWorkspaceAsync 的结果，
/// 仅成功才从 Workspaces 集合移除；不再自行调用 DisposeAsync（生命周期责任唯一归属
/// ProjectSession）；Session 层同图去重返回既有工作区时 UI 集合不重复添加。
/// </summary>
public class ProjectWorkspaceViewModelWorkspaceTests
{
    private static (ProjectWorkspaceViewModel Vm, ProjectSession Session, FakeMapQueryService MapQuery, StubAssetStore Assets) Build()
    {
        var assets = new StubAssetStore();
        var session = TestSessions.CreateReadySession(assetStore: assets);
        var mapQuery = new FakeMapQueryService();
        var vm = new ProjectWorkspaceViewModel(
            session,
            mapQuery: mapQuery,
            contextResolver: new StubContextResolver(),
            createMap: new CreateMapUseCase(session, new FakeMapCreationService(mapQuery)));
        return (vm, session, mapQuery, assets);
    }

    private static async Task<MapWorkspaceSession> OpenViaCommandAsync(
        ProjectWorkspaceViewModel vm, FakeMapQueryService mapQuery, MapSummary map)
    {
        mapQuery.Maps.Add(map);
        vm.SelectedMap = map;
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);
        return vm.Workspaces[0];
    }

    [Fact]
    public async Task OpenWorkspace_ShowsRealMapName_AndAddsUiItemOnce()
    {
        var (vm, session, mapQuery, _) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "南海群岛" };

        var workspace = await OpenViaCommandAsync(vm, mapQuery, map);

        Assert.Single(vm.Workspaces);
        Assert.Single(session.Workspaces);
        Assert.Equal("南海群岛", workspace.MapName);
        Assert.NotEqual(map.MapId.ToString(), workspace.MapName); // 显示名不是 GUID
        Assert.Contains("南海群岛", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenWorkspace_SameMapTwice_KeepsSingleUiItem_AndReusesSession()
    {
        var (vm, session, mapQuery, _) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "世界总览" };

        var first = await OpenViaCommandAsync(vm, mapQuery, map);
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.Workspaces); // UI 集合不重复添加
        Assert.Single(session.Workspaces); // Session 层去重：无第二个 Session
        Assert.Same(first, vm.Workspaces[0]);
        Assert.Contains("复用现有工作区", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseWorkspace_Success_RemovesUiItem_ProjectStaysReady()
    {
        var (vm, session, mapQuery, _) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "东海之滨" };
        var workspace = await OpenViaCommandAsync(vm, mapQuery, map);
        vm.SelectedWorkspace = workspace;

        await vm.CloseWorkspaceCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.Workspaces); // 仅成功才移除 UI 集合项
        Assert.Empty(session.Workspaces);
        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed); // 释放由 ProjectSession 完成（Close → Dispose → Remove）
        Assert.Equal(ProjectSessionState.Ready, session.State); // 关闭工作区不关闭项目（A-5）
        Assert.Contains("已关闭", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("东海之滨", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseWorkspace_Failure_KeepsUiItem_AndShowsDiagnostic_WithoutSelfDispose()
    {
        var (vm, session, mapQuery, _) = Build();
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "西域荒原" };
        var workspace = await OpenViaCommandAsync(vm, mapQuery, map);
        vm.SelectedWorkspace = workspace;
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        await vm.CloseWorkspaceCommand.ExecuteAsync(CancellationToken.None);

        // 失败：Session 列表与 UI 集合同步保留，不出现分叉（B4.10）。
        Assert.Single(vm.Workspaces);
        Assert.Single(session.Workspaces);
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        // 若 VM 仍自行调用 DisposeAsync，续段语义会把 CloseFailed 推进为 Closed——
        // 状态保持 CloseFailed 即证明 ViewModel 未再承担释放责任（B4.7）。
        Assert.False(workspace.IsDisposed);
        Assert.Contains("关闭工作区失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("工作区关闭失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        failure.Dispose();
    }
}
