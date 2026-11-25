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
/// WP-UI-02：Project Session 关闭状态机与失败重试。所有失败路径均由可控制异常的替身注入，
/// 断言真实释放顺序、失败后状态、事件次数与 Registry 占位边界。
/// </summary>
public class ProjectSessionCloseTests
{
    private static (ProjectSession Session, FakePackageDatabase Database, FakeAssetStore Assets) BuildSession()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assets = new FakeAssetStore();
        var session = new ProjectSession(
            Guid.NewGuid(),
            ProjectPackageStructure.NormalizePackagePath(
                Path.Combine(Path.GetTempPath(), $"wv-close-{Guid.NewGuid():N}.wvproject")),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            database,
            assets,
            Guid.NewGuid());
        session.TransitionTo(ProjectSessionState.Ready);
        return (session, database, assets);
    }

    private static Task<MapWorkspaceSession> OpenWorkspaceAsync(ProjectSession session)
        => session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

    [Fact]
    public async Task CloseFromReady_TransitionsToClosed_AndDisposesInReverseOrder()
    {
        var (session, database, assets) = BuildSession();
        var callLog = new List<string>();
        assets.SharedLog = callLog;
        database.SharedLog = callLog;
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await session.CloseAsync();

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.True(callLog.IndexOf("AssetStore:Dispose") < callLog.IndexOf("Database:Dispose"));
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task CloseTwice_DoesNotDisposeTwice_AndRaisesEventOnce()
    {
        var (session, database, assets) = BuildSession();
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await session.CloseAsync();
        await session.CloseAsync();

        Assert.Equal(1, assets.DisposeAttempts);
        Assert.Equal(1, database.DisposeAttempts);
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task ConcurrentClose_SharesSameCloseTask_SingleExecution()
    {
        // 统一裁决 2.1：关闭执行期间的其他关闭请求共享同一执行 Task，
        // 不并行两套关闭流程、不重复释放任何资源、事件只触发一次。
        var (session, database, assets) = BuildSession();
        await OpenWorkspaceAsync(session);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        database.DisposeGate = gate;
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        var first = session.CloseAsync();   // 启动关闭：同步段直奔数据库释放门控并挂起
        var second = session.CloseAsync();  // Closing 期间进入：必须共享同一执行 Task

        Assert.Same(first, second);
        Assert.Equal(ProjectSessionState.Closing, session.State);
        Assert.Equal(0, database.DisposeSuccesses);

        gate.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses); // 关闭流程恰好执行一次
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task WorkspaceCloseFailure_DoesNotReachClosed_ContinuesSafeStages_KeepsWorkspaceForRetry()
    {
        var (session, database, assets) = BuildSession();
        var workspace = await OpenWorkspaceAsync(session);
        using var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Contains("工作区关闭失败", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("工作区关闭失败", session.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.Contains(session.Workspaces, w => w.WorkspaceId == workspace.WorkspaceId);
        // 统一裁决 2.3：工作区失败不阻断后续安全阶段——Asset Store 与数据库仍然释放，
        // 该工作区保留在列表中留待重试；SessionClosed 不触发（未全部成功）。
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.Equal(0, closedCount);
    }

    [Fact]
    public async Task RetryAfterWorkspaceFailure_ReleasesRemainingResources_AndRaisesEventOnce()
    {
        var (session, database, assets) = BuildSession();
        var workspace = await OpenWorkspaceAsync(session);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());
        failure.Dispose();

        await session.CloseAsync(); // 重试：工作区释放已完成，继续 Asset Store / 数据库

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(session.Workspaces);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.Equal(1, closedCount);
        Assert.Null(session.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task AssetStoreDisposeFailure_DoesNotReachClosed_StillDisposesDatabase()
    {
        var (session, database, assets) = BuildSession();
        assets.FailureHook = op => op == "AssetStore:Dispose"
            ? new IOException("注入：Asset Store 释放失败。")
            : null;
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await Assert.ThrowsAsync<IOException>(() => session.CloseAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Contains("Asset Store 释放失败", session.CloseFailureDiagnostic, StringComparison.Ordinal);
        // 统一裁决 2.3：Asset Store 失败不永久跳过数据库阶段（二者相互独立且安全）。
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.Equal(0, closedCount);
    }

    [Fact]
    public async Task DatabaseDisposeFailure_KeepsAssetStoreReleased_AndDoesNotRaiseEvent()
    {
        var (session, database, assets) = BuildSession();
        database.FailureHook = op => op == "Database:Dispose"
            ? new IOException("注入：数据库释放失败。")
            : null;
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Equal(1, assets.DisposeSuccesses);
        Assert.Equal(0, database.DisposeSuccesses);
        Assert.Equal(0, closedCount);

        database.FailureHook = null;
        await session.CloseAsync(); // 重试不得再次释放已成功的 Asset Store

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Equal(1, assets.DisposeAttempts);
        Assert.Equal(1, database.DisposeSuccesses);
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task SessionClosed_RaisedOnceAcrossFailureAndRetry()
    {
        var (session, database, _) = BuildSession();
        database.FailureHook = op => op == "Database:Dispose"
            ? new IOException("注入：数据库释放失败。")
            : null;
        var closedCount = 0;
        session.SessionClosed += (_, _) => closedCount++;

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());
        Assert.Equal(0, closedCount);

        database.FailureHook = null;
        await session.CloseAsync();
        await session.CloseAsync();
        Assert.Equal(1, closedCount);
    }

    [Fact]
    public async Task Registry_KeepsSessionOnFailure_AndRemovesOnlyAfterSuccess()
    {
        var (session, database, _) = BuildSession();
        var registry = new ProjectSessionRegistry();
        Assert.True(registry.TryRegister(session));
        database.FailureHook = op => op == "Database:Dispose"
            ? new IOException("注入：数据库释放失败。")
            : null;

        await Assert.ThrowsAnyAsync<Exception>(() => session.CloseAsync());

        Assert.Same(session, registry.FindByPackagePath(session.CanonicalPackagePath));
        Assert.NotEmpty(registry.Sessions);

        database.FailureHook = null;
        await session.CloseAsync();

        Assert.Null(registry.FindByPackagePath(session.CanonicalPackagePath));
        Assert.Empty(registry.Sessions);
    }

    [Fact]
    public async Task RegistryCloseAll_KeepsSessionRegisteredWhenCloseFails()
    {
        var (session, database, _) = BuildSession();
        var registry = new ProjectSessionRegistry();
        Assert.True(registry.TryRegister(session));
        database.FailureHook = op => op == "Database:Dispose"
            ? new IOException("注入：数据库释放失败。")
            : null;

        await Assert.ThrowsAnyAsync<Exception>(() => registry.CloseAllAsync());

        Assert.Equal(ProjectSessionState.CloseFailed, session.State);
        Assert.Same(session, registry.FindByPackagePath(session.CanonicalPackagePath));
    }
}
