using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.Tests.UseCases;
using Xunit;

namespace WorldView.Application.Tests.Sessions;

/// <summary>
/// WP-UI-04B B3/B4：ProjectSession 同图去重、Close/Dispose/Remove 原子化，
/// 以及项目级关闭与子工作区新生命周期的兼容性（验收 A-1/A-2/A-3/A-6~A-11）。
/// 失败注入沿用取消回调（关闭失败）与 DisposeFailureHookForTests（释放失败）。
/// </summary>
public class ProjectSessionWorkspaceLifecycleTests
{
    private static (ProjectSession Session, FakePackageDatabase Database, FakeAssetStore Assets) BuildSession()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assets = new FakeAssetStore();
        var session = new ProjectSession(
            Guid.NewGuid(),
            ProjectPackageStructure.NormalizePackagePath(
                Path.Combine(Path.GetTempPath(), $"wv-wsl-{Guid.NewGuid():N}.wvproject")),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            database,
            assets,
            Guid.NewGuid());
        session.TransitionTo(ProjectSessionState.Ready);
        return (session, database, assets);
    }

    private static Task<MapWorkspaceSession> OpenAsync(ProjectSession session, FakeMapQuery? query = null)
        => session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, query ?? FakeMapQuery.WithAnyMap(), FakeResolver.Success());

    // ---------- B3：同图去重（A-1 / A-2 / A-3） ----------

    [Fact]
    public async Task SameMap_Reopen_ReturnsSameSession_WithoutDuplicate()
    {
        var (session, _, _) = BuildSession();
        var query = FakeMapQuery.WithDisplayName("世界总览");
        var mapId = Guid.NewGuid();

        var first = await session.OpenWorkspaceAsync(mapId, null, null, query, FakeResolver.Success());
        var second = await session.OpenWorkspaceAsync(mapId, null, null, query, FakeResolver.Success());

        Assert.Same(first, second);
        Assert.Equal("世界总览", second.MapName);
        Assert.Single(session.Workspaces);
    }

    [Fact]
    public async Task DifferentMaps_IndependentSessions()
    {
        var (session, _, _) = BuildSession();
        var first = await OpenAsync(session); // 各自独立 MapId
        var second = await OpenAsync(session);

        Assert.NotSame(first, second);
        Assert.Equal(2, session.Workspaces.Count);
    }

    [Fact]
    public async Task SameMapId_InDifferentProjects_NoConflict()
    {
        var (projectA, _, _) = BuildSession();
        var (projectB, _, _) = BuildSession();
        var sharedMapId = Guid.NewGuid();

        var workspaceA = await projectA.OpenWorkspaceAsync(
            sharedMapId, null, null, FakeMapQuery.WithDisplayName("世界总览"), FakeResolver.Success());
        var workspaceB = await projectB.OpenWorkspaceAsync(
            sharedMapId, null, null, FakeMapQuery.WithDisplayName("世界总览"), FakeResolver.Success());

        Assert.NotSame(workspaceA, workspaceB);
        Assert.Single(projectA.Workspaces);
        Assert.Single(projectB.Workspaces);
    }

    [Fact]
    public async Task ClosedWorkspace_RemovedThenSameMap_ReopensAsNewSession()
    {
        var (session, _, _) = BuildSession();
        var mapId = Guid.NewGuid();
        var query = FakeMapQuery.WithAnyMap();
        var original = await session.OpenWorkspaceAsync(mapId, null, null, query, FakeResolver.Success());

        Assert.True(await session.CloseWorkspaceAsync(original.WorkspaceId));
        Assert.Empty(session.Workspaces);

        var reopened = await session.OpenWorkspaceAsync(mapId, null, null, query, FakeResolver.Success());

        Assert.NotSame(original, reopened);
        Assert.NotEqual(original.WorkspaceId, reopened.WorkspaceId);
        Assert.Single(session.Workspaces);
        Assert.Equal(MapWorkspaceState.Ready, reopened.LifecycleState); // 已关闭对象未被复用
    }

    [Fact]
    public async Task CloseFailedWorkspace_ReopenSameMap_Rejected()
    {
        var (session, _, _) = BuildSession();
        var mapId = Guid.NewGuid();
        var workspace = await session.OpenWorkspaceAsync(mapId, null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        Assert.False(await session.CloseWorkspaceAsync(workspace.WorkspaceId)); // 失败，保留

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.OpenWorkspaceAsync(mapId, null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success()));

        Assert.Contains("拒绝重复打开", error.Message, StringComparison.Ordinal);
        Assert.Single(session.Workspaces); // 不得通过重建 Session 绕开失败状态
        failure.Dispose();
    }

    [Fact]
    public async Task CloseWorkspace_UnknownId_ReturnsFalse()
    {
        var (session, _, _) = BuildSession();
        Assert.False(await session.CloseWorkspaceAsync(Guid.NewGuid()));
    }

    // ---------- B4：Close / Dispose / Remove 原子化（A-6 / A-7 / A-8） ----------

    [Fact]
    public async Task CloseWorkspace_Success_FullyReleases_ThenRemoves()
    {
        var (session, database, assets) = BuildSession();
        var workspace = await OpenAsync(session);

        var closed = await session.CloseWorkspaceAsync(workspace.WorkspaceId);

        Assert.True(closed);
        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Empty(session.Workspaces);
        Assert.Equal(ProjectSessionState.Ready, session.State); // 单工作区关闭不影响项目
        Assert.Equal(0, assets.DisposeAttempts); // 项目级资源不受单工作区关闭影响
        Assert.Equal(0, database.DisposeAttempts);
    }

    [Fact]
    public async Task CloseWorkspace_CloseFailure_KeepsWorkspace_AndDoesNotRemove()
    {
        var (session, database, assets) = BuildSession();
        var workspace = await OpenAsync(session);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        var closed = await session.CloseWorkspaceAsync(workspace.WorkspaceId);

        Assert.False(closed);
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.Contains("工作区关闭失败", workspace.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.Contains(session.Workspaces, w => w.WorkspaceId == workspace.WorkspaceId); // 保留可重试
        Assert.Equal(0, assets.DisposeAttempts);
        Assert.Equal(0, database.DisposeAttempts);
        failure.Dispose();
    }

    [Fact]
    public async Task CloseWorkspace_DisposeFailure_KeepsWorkspace_RetrySkipsCompletedClose()
    {
        var (session, _, _) = BuildSession();
        var workspace = await OpenAsync(session);
        var cancelCount = 0;
        using var observer = workspace.DerivedWorkCancellationToken.Register(() => cancelCount++);
        workspace.DisposeFailureHookForTests = () => throw new IOException("注入：资源释放失败。");

        Assert.False(await session.CloseWorkspaceAsync(workspace.WorkspaceId));
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.Contains("资源释放失败", workspace.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.Single(session.Workspaces);

        workspace.DisposeFailureHookForTests = null;
        Assert.True(await session.CloseWorkspaceAsync(workspace.WorkspaceId)); // 重试

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.Empty(session.Workspaces);
        Assert.Equal(1, cancelCount); // 已成功的关闭阶段不重复执行
    }

    [Fact]
    public async Task CloseWorkspace_RetryAfterFailure_Succeeds_AndClearsDiagnostic()
    {
        var (session, _, _) = BuildSession();
        var workspace = await OpenAsync(session);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        Assert.False(await session.CloseWorkspaceAsync(workspace.WorkspaceId));
        failure.Dispose();

        Assert.True(await session.CloseWorkspaceAsync(workspace.WorkspaceId));

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.Null(workspace.CloseFailureDiagnostic);
        Assert.Empty(session.Workspaces);
    }

    // ---------- 项目级关闭兼容性（A-9 / A-10 / A-11） ----------

    [Fact]
    public async Task ProjectClose_ClosesAllWorkspaces_InLifoOrder_ThenProjectResources()
    {
        var (session, database, assets) = BuildSession();
        var callLog = new List<string>();
        assets.SharedLog = callLog;
        database.SharedLog = callLog;

        var first = await OpenAsync(session);
        var second = await OpenAsync(session);
        first.DerivedWorkCancellationToken.Register(() => callLog.Add("Workspace:Cancel:first"));
        second.DerivedWorkCancellationToken.Register(() => callLog.Add("Workspace:Cancel:second"));

        await session.CloseAsync();

        Assert.Empty(session.Workspaces);
        Assert.Equal(MapWorkspaceState.Closed, first.LifecycleState);
        Assert.Equal(MapWorkspaceState.Closed, second.LifecycleState);
        Assert.True(first.IsDisposed);
        Assert.True(second.IsDisposed);
        // LIFO：后打开的工作区先关闭（second 的取消先于 first）；随后 Asset Store → 数据库。
        Assert.True(callLog.IndexOf("Workspace:Cancel:second") < callLog.IndexOf("Workspace:Cancel:first"));
        Assert.True(callLog.IndexOf("Workspace:Cancel:first") < callLog.IndexOf("AssetStore:Dispose"));
        Assert.True(callLog.IndexOf("AssetStore:Dispose") < callLog.IndexOf("Database:Dispose"));
    }

    [Fact]
    public async Task ChildCloseFailure_ProjectDoesNotReachClosed_FailedChildKeptForRetry()
    {
        var (session, database, assets) = BuildSession();
        var first = await OpenAsync(session);
        var second = await OpenAsync(session);
        var failure = second.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Contains("工作区关闭失败", session.CloseFailureDiagnostic, StringComparison.Ordinal);
        // LIFO：second（后开）先关闭并失败——first 完全未被触及。
        Assert.Contains(session.Workspaces, w => w.WorkspaceId == second.WorkspaceId);
        Assert.Equal(MapWorkspaceState.CloseFailed, second.LifecycleState);
        Assert.Equal(MapWorkspaceState.Ready, first.LifecycleState);
        Assert.Equal(0, assets.DisposeAttempts);
        Assert.Equal(0, database.DisposeAttempts);
        failure.Dispose();
    }

    [Fact]
    public async Task ProjectCloseRetry_DoesNotDoubleReleaseCompletedWorkspaces()
    {
        var (session, database, assets) = BuildSession();
        var first = await OpenAsync(session);
        var second = await OpenAsync(session);
        var firstCancelCount = 0;
        var secondCancelCount = 0;
        first.DerivedWorkCancellationToken.Register(() => firstCancelCount++);
        var secondFailure = second.DerivedWorkCancellationToken.Register(() =>
        {
            secondCancelCount++;
            throw new InvalidOperationException("注入：工作区关闭失败。");
        });

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        secondFailure.Dispose();
        await session.CloseAsync(); // 重试：first 已完成释放的不重复执行，second 从失败点继续

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(session.Workspaces);
        Assert.Equal(MapWorkspaceState.Closed, first.LifecycleState);
        Assert.Equal(MapWorkspaceState.Closed, second.LifecycleState);
        Assert.Equal(1, firstCancelCount); // first 未被重复关闭
        Assert.Equal(1, secondCancelCount); // second 的关闭阶段同样只执行一次
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.Null(session.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task ChildDisposeFailure_ProjectCloseFails_AndRetryCompletes()
    {
        var (session, database, assets) = BuildSession();
        var workspace = await OpenAsync(session);
        workspace.DisposeFailureHookForTests = () => throw new IOException("注入：资源释放失败。");

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Contains("资源释放失败", session.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.True(workspace.IsClosed); // 关闭阶段已成功——进度被独立记录
        Assert.False(workspace.IsDisposed);
        Assert.Single(session.Workspaces);

        workspace.DisposeFailureHookForTests = null;
        await session.CloseAsync(); // 重试：跳过已完成的关闭，仅继续释放

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(workspace.IsDisposed);
        Assert.Empty(session.Workspaces);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses);
    }
}
