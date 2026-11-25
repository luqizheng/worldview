using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.Tests.UseCases;
using WorldView.Domain.Time;
using Xunit;

namespace WorldView.Application.Tests.Sessions;

/// <summary>
/// WP-UI-04B B1/B2/B5：MapWorkspaceSession 生命周期状态机。
/// 覆盖 lifecycle-state-machine.md §2 全部状态边与 §3 各状态允许的操作矩阵；
/// 失败注入沿用 ProjectSessionCloseTests 的替身模式（取消回调注入关闭失败、
/// DisposeFailureHookForTests 注入释放失败）。
/// </summary>
public class MapWorkspaceSessionLifecycleTests
{
    private static ProjectSession BuildProjectSession()
    {
        var session = new ProjectSession(
            Guid.NewGuid(),
            ProjectPackageStructure.NormalizePackagePath(
                Path.Combine(Path.GetTempPath(), $"wv-mws-{Guid.NewGuid():N}.wvproject")),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            new FakePackageDatabase("db.sqlite"),
            new FakeAssetStore(),
            Guid.NewGuid());
        session.TransitionTo(ProjectSessionState.Ready);
        return session;
    }

    /// <summary>经正式打开路径创建的 Ready 工作区（MapName 来自真实地图数据）。</summary>
    private static Task<MapWorkspaceSession> OpenReadyAsync(ProjectSession session, FakeMapQuery? query = null)
        => session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, query ?? FakeMapQuery.WithAnyMap(), FakeResolver.Success());

    /// <summary>直接构造（不挂载）的工作区，用于覆盖 Created 状态的守卫语义。</summary>
    private static MapWorkspaceSession BuildCreated(ProjectSession projectSession)
        => new(
            projectSession,
            new ValidatedWorkspaceContext
            {
                MapId = Guid.NewGuid(),
                BranchId = null,
                RootHistoryId = FakeResolver.DefaultRootHistoryId,
                HistoricalTime = HistoricalTime.Epoch,
            },
            "直构地图");

    // ---------- B2：MapName ----------

    [Fact]
    public async Task OpenWorkspace_PreservesRealMapDisplayName_NotGuid()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project, FakeMapQuery.WithDisplayName("东海之滨"));

        Assert.Equal("东海之滨", workspace.MapName);
        Assert.NotEqual(workspace.Context.MapId.ToString(), workspace.MapName);
    }

    [Fact]
    public async Task Constructor_EmptyOrWhitespaceMapName_Rejected()
    {
        await using var project = BuildProjectSession();
        var context = new ValidatedWorkspaceContext
        {
            MapId = Guid.NewGuid(),
            RootHistoryId = FakeResolver.DefaultRootHistoryId,
            HistoricalTime = HistoricalTime.Epoch,
        };

        Assert.Throws<ArgumentException>(() => _ = new MapWorkspaceSession(project, context, ""));
        Assert.Throws<ArgumentException>(() => _ = new MapWorkspaceSession(project, context, "   "));
    }

    [Fact]
    public async Task OpenWorkspace_EmptyDisplayNameFromMapData_Rejected()
    {
        var project = BuildProjectSession();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OpenReadyAsync(project, FakeMapQuery.WithDisplayName("")));

        Assert.Contains("显示名称", error.Message, StringComparison.Ordinal);
        Assert.Empty(project.Workspaces); // 不产生半挂载的工作区
    }

    // ---------- B1：状态机基本转换 ----------

    [Fact]
    public async Task OpenWorkspace_SessionIsReadyAfterMount()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);

        Assert.Equal(MapWorkspaceState.Ready, workspace.LifecycleState);
        Assert.False(workspace.IsClosed);
        Assert.False(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task Constructor_StateIsCreated()
    {
        await using var project = BuildProjectSession();
        var workspace = BuildCreated(project);

        Assert.Equal(MapWorkspaceState.Created, workspace.LifecycleState);
    }

    [Fact]
    public async Task Created_DirectClose_ReachesClosed_AndReleasesResources()
    {
        await using var project = BuildProjectSession();
        var workspace = BuildCreated(project);

        await workspace.CloseAsync();

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsClosed);
        Assert.True(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task Ready_Close_ReachesClosed_AndReleasesResources()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);

        await workspace.CloseAsync();

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
    }

    [Fact]
    public async Task Closed_RepeatedClose_IsIdempotentTerminal()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        await workspace.CloseAsync();

        var hookCount = 0;
        workspace.DisposeFailureHookForTests = () => hookCount++;
        await workspace.CloseAsync();
        await workspace.CloseAsync();
        await workspace.DisposeAsync();

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState); // 终态，不得离开
        Assert.True(workspace.IsDisposed);
        Assert.Equal(0, hookCount); // 幂等：不产生任何新的释放副作用
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task Closed_SwitchContext_IsDiscarded()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var originalRoot = workspace.Context.RootHistoryId;
        await workspace.CloseAsync();

        await workspace.SwitchContextAsync(null, null, FakeResolver.Success(Guid.NewGuid()));

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.Equal(originalRoot, workspace.Context.RootHistoryId); // 晚到达丢弃，不复活
    }

    // ---------- B5：失败、进度保留与重试 ----------

    [Fact]
    public async Task CloseFailure_KeepsSessionInCloseFailed_WithDiagnosticAndPartialProgress()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        using var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());

        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.Contains("工作区关闭失败", workspace.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.False(workspace.IsDisposed); // 释放阶段未执行（关闭阶段失败）
        Assert.Contains("工作区关闭失败", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetryAfterCloseFailure_ReachesClosed_AndClearsDiagnostic()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));

        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());
        failure.Dispose();
        await workspace.CloseAsync(); // CloseFailed → Closing → Closed

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task CloseSucceeded_DisposeFails_RetrySkipsCompletedClose_AndCompletesDispose()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var cancelCount = 0;
        using var observer = workspace.DerivedWorkCancellationToken.Register(() => cancelCount++);
        workspace.DisposeFailureHookForTests = () => throw new IOException("注入：资源释放失败。");

        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());

        // 关闭阶段已成功、释放阶段失败：进度被独立记录。
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.True(workspace.IsClosed);
        Assert.False(workspace.IsDisposed);
        Assert.Contains("资源释放失败", workspace.CloseFailureDiagnostic, StringComparison.Ordinal);

        workspace.DisposeFailureHookForTests = null;
        await workspace.CloseAsync(); // 重试：跳过已完成的关闭，仅继续释放

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
        Assert.Equal(1, cancelCount); // 关闭阶段未重复执行
    }

    [Fact]
    public async Task DisposeAsync_FromCloseFailed_ContinuesRelease_AndReachesClosed()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));
        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());
        failure.Dispose();

        await workspace.DisposeAsync(); // 续段：不经过 CloseAsync 也从失败点继续

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_BeforeClose_IsRejected()
    {
        await using var project = BuildProjectSession();
        var created = BuildCreated(project);

        var createdError = await Assert.ThrowsAsync<InvalidOperationException>(() => created.DisposeAsync().AsTask());
        Assert.Contains("必须先经 CloseAsync", createdError.Message, StringComparison.Ordinal);

        var ready = await OpenReadyAsync(project);
        var readyError = await Assert.ThrowsAsync<InvalidOperationException>(() => ready.DisposeAsync().AsTask());
        Assert.Contains("必须先经 CloseAsync", readyError.Message, StringComparison.Ordinal);

        Assert.Equal(MapWorkspaceState.Ready, ready.LifecycleState); // 拒绝不改变状态
    }

    [Fact]
    public async Task DisposeAsync_AfterClosed_IsIdempotent()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        await workspace.CloseAsync();

        await workspace.DisposeAsync();

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
    }

    // ---------- R1 P2-01：CloseAsync 的 CancellationToken 语义 ----------

    [Fact]
    public async Task CloseAsync_CancelledBeforeClose_StateUnchanged_NoRelease()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => workspace.CloseAsync(cts.Token));

        // 进入 Closing 之前取消：状态不变、不释放资源、不进 CloseFailed。
        Assert.Equal(MapWorkspaceState.Ready, workspace.LifecycleState);
        Assert.False(workspace.IsClosed);
        Assert.False(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    [Fact]
    public async Task CloseAsync_CancelledBeforeClose_FromCloseFailed_StaysCloseFailed()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));
        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());
        failure.Dispose();
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => workspace.CloseAsync(cts.Token));

        // 重试入口上取消：停留在 CloseFailed（可再次重试），不误入其他状态。
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.Contains("工作区关闭失败", workspace.CloseFailureDiagnostic, StringComparison.Ordinal);
        Assert.False(workspace.IsDisposed);
    }

    [Fact]
    public async Task CloseAsync_CancelledDuringClose_CompletesFully_NotCloseFailed()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        using var cts = new CancellationTokenSource();
        // 进入 Closing 后（派生工作取消瞬间）才取消调用方令牌——验证 Closing 阶段
        // 不再接受取消：释放执行到底，进入 Closed 而非 CloseFailed。
        using var trigger = workspace.DerivedWorkCancellationToken.Register(() => cts.Cancel());

        await workspace.CloseAsync(cts.Token);

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Null(workspace.CloseFailureDiagnostic);
    }

    // ---------- R1 P2-02：释放阶段标志与真实资源状态一致 ----------

    [Fact]
    public async Task DisposeHookFailure_ResourceNotYetDisposed_FlagMatchesReality()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var cancelCount = 0;
        using var observer = workspace.DerivedWorkCancellationToken.Register(() => cancelCount++);
        workspace.DisposeFailureHookForTests = () => throw new IOException("注入：资源释放失败。");

        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());

        // 标志与真实资源状态一致：失败时释放步骤尚未执行，CTS 仍然存活（令牌可观察）。
        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.False(workspace.IsDisposed);
        Assert.Equal(1, cancelCount); // 关闭阶段（取消）已成功执行
        _ = workspace.DerivedWorkCancellationToken; // 不抛 ObjectDisposedException

        workspace.DisposeFailureHookForTests = null;
        await workspace.CloseAsync(); // 重试：完整执行释放步骤

        Assert.Equal(MapWorkspaceState.Closed, workspace.LifecycleState);
        Assert.True(workspace.IsDisposed);
        Assert.Equal(1, cancelCount); // 已成功阶段不重复执行
        // 真实释放结果与标志一致：CTS 已释放，令牌不可再观察。
        Assert.Throws<ObjectDisposedException>(() => _ = workspace.DerivedWorkCancellationToken);
    }

    // ---------- B5：SwitchContext 状态守卫 ----------

    [Fact]
    public async Task SwitchContext_InReady_ReplacesContext_AndCancelsDerivedWork()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var cancelled = false;
        using var observer = workspace.DerivedWorkCancellationToken.Register(() => cancelled = true);

        var otherRoot = Guid.NewGuid();
        await workspace.SwitchContextAsync(null, null, FakeResolver.Success(otherRoot));

        Assert.Equal(otherRoot, workspace.Context.RootHistoryId);
        Assert.True(cancelled);
        Assert.Equal(MapWorkspaceState.Ready, workspace.LifecycleState);
    }

    [Fact]
    public async Task SwitchContext_InCloseFailed_IsDiscarded()
    {
        var project = BuildProjectSession();
        var workspace = await OpenReadyAsync(project);
        var failure = workspace.DerivedWorkCancellationToken.Register(
            () => throw new InvalidOperationException("注入：工作区关闭失败。"));
        await Assert.ThrowsAnyAsync<Exception>(() => workspace.CloseAsync());
        var originalRoot = workspace.Context.RootHistoryId;

        await workspace.SwitchContextAsync(null, null, FakeResolver.Success(Guid.NewGuid()));

        Assert.Equal(MapWorkspaceState.CloseFailed, workspace.LifecycleState);
        Assert.Equal(originalRoot, workspace.Context.RootHistoryId); // 晚到达丢弃
        failure.Dispose();
    }

    [Fact]
    public async Task MarkReady_FromNonCreated_IsRejected()
    {
        await using var project = BuildProjectSession();
        var workspace = BuildCreated(project);
        workspace.MarkReady(); // Created → Ready

        var error = Assert.Throws<InvalidOperationException>(() => workspace.MarkReady());
        Assert.Contains("拒绝标记就绪", error.Message, StringComparison.Ordinal);
        Assert.Equal(MapWorkspaceState.Ready, workspace.LifecycleState);
    }
}
