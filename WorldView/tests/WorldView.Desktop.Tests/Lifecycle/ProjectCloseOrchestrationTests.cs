using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Rendering;
using WorldView.Desktop.Tests.Testing;
using WorldView.Desktop.ViewModels;
using Xunit;

namespace WorldView.Desktop.Tests.Lifecycle;

/// <summary>
/// WP-30B Phase 3A — C1 / C2 关闭编排测试（ADR-10 §7.1 / §7.2 / R5 窗口关闭流程）。
/// 全部经真实 ProjectSession + 真实工作区 + 门控 renderer 替身验证真实调用顺序；
/// 交错用 TaskCompletionSource 门控制造，绝无 Sleep / 轮询 / 调度运气。
/// </summary>
public class ProjectCloseOrchestrationTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    /// <summary>可注入 Binding 工厂的 VM（门控 renderer 经工厂注入；测试程序集经 IvT 覆写 internal virtual）。</summary>
    private sealed class TestViewModel : ProjectWorkspaceViewModel
    {
        public TestViewModel(
            ProjectSession session,
            IMapQueryService? mapQuery,
            IWorkspaceContextResolver? contextResolver,
            CreateMapUseCase createMap)
            : base(session, mapQuery, contextResolver, createMap)
        {
        }

        public Func<MapWorkspaceSession, MapViewportBinding>? TestBindingFactory { get; set; }

        internal override MapViewportBinding CreateViewportBinding(MapWorkspaceSession workspace)
            => TestBindingFactory?.Invoke(workspace)
               ?? throw new InvalidOperationException("测试必须先安装 Binding 工厂。");
    }

    /// <summary>探针 / 门控 renderer：记录 Dispose 次数与入口时刻的 binding 状态。</summary>
    private sealed class ProbeRenderer : MapSceneRenderer
    {
        private readonly TaskCompletionSource? _disposeEnteredSource;
        private readonly TaskCompletionSource? _releaseSource;

        public ProbeRenderer(bool gate)
        {
            if (gate)
            {
                _disposeEnteredSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _releaseSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public int DisposeCount { get; private set; }

        /// <summary>Dispose 入口时刻探针（binding 构造完成后装配）。</summary>
        public Func<bool>? BindingClosedAtDisposeEntry { get; set; }

        public bool BindingClosedObserved { get; private set; }

        public Task DisposeEntered => _disposeEnteredSource?.Task ?? Task.CompletedTask;

        public void ReleaseDispose() => _releaseSource?.TrySetResult();

        public override void Dispose()
        {
            DisposeCount++;
            BindingClosedObserved = BindingClosedAtDisposeEntry?.Invoke() ?? false;
            _disposeEnteredSource?.TrySetResult();
            if (_releaseSource is { } release && !release.Task.Wait(GateTimeout))
            {
                throw new TimeoutException("renderer dispose gate 未在超时内放行。");
            }

            base.Dispose();
        }
    }

    private static (TestViewModel Vm, FakeMapQueryService Query, StubPackageDatabase Database, StubAssetStore Assets, ProjectSession Session)
    CreateVm()
    {
        var query = new FakeMapQueryService();
        query.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        var session = TestSessions.CreateReadySession();
        var vm = new TestViewModel(
            session,
            query,
            new FakeWorkspaceContextResolver(),
            new CreateMapUseCase(session, new FakeMapCreationService(query)));
        return (vm, query, (StubPackageDatabase)session.Database, (StubAssetStore)session.AssetStore, session);
    }

    private static async Task<List<MapWorkspaceSession>> OpenWorkspacesAsync(ProjectWorkspaceViewModel vm, FakeMapQueryService query, int count)
    {
        var opened = new List<MapWorkspaceSession>();
        for (var i = 0; i < count; i++)
        {
            vm.SelectedMap = query.Maps[0];
            await vm.OpenWorkspaceCommand.ExecuteAsync(null);
            opened.Add(vm.Workspaces[^1]);
        }

        return opened;
    }

    // ---- C1：单工作区关闭 ----

    [Fact]
    public async Task C1_ClosesTargetBinding_BeforeWorkspaceClose_OrderProof()
    {
        var (vm, query, _, _, session) = CreateVm();
        var gated = new ProbeRenderer(gate: true);
        vm.TestBindingFactory = ws =>
        {
            var binding = new MapViewportBinding(ws.ViewState, renderer: gated);
            gated.BindingClosedAtDisposeEntry = () => binding.IsClosed;
            return binding;
        };
        var workspaces = await OpenWorkspacesAsync(vm, query, 1);
        var binding = vm.GetViewportBinding(workspaces[0])!;
        vm.SelectedWorkspace = workspaces[0];

        // 门控测试：编排触发移至工作线程——真实 renderer.Dispose 在生产中是幂等空操作，
        // 不会阻塞；此处替身的门控用于制造确定性交错，必须有独立线程放行。
        var closeTask = Task.Run(() => vm.CloseWorkspaceCommand.ExecuteAsync(null));

        // 锚点：renderer.Dispose 已进入（Binding 子序进行中）。
        await gated.DisposeEntered.WaitAsync(GateTimeout);
        Assert.True(binding.IsClosed);                       // 子序步骤 1a 已置终态
        Assert.True(gated.BindingClosedObserved);

        // 顺序证据：Binding 子序仍在进行时，Application 侧（工作区/会话）尚未关闭。
        Assert.False(workspaces[0].IsClosed);
        Assert.Equal(ProjectSessionState.Ready, session.State);

        gated.ReleaseDispose();
        await closeTask;

        // C1 完整顺序：Binding 关闭 → 工作区关闭 → 集合移除 → 工作区 Dispose。
        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_disposed" },
            binding.CloseStageLog);
        Assert.True(workspaces[0].IsClosed);
        Assert.DoesNotContain(workspaces[0], vm.Workspaces);
        Assert.Throws<ObjectDisposedException>(() => _ = workspaces[0].DerivedWorkCancellationToken);
        Assert.Equal(ProjectSessionState.Ready, session.State); // 会话不受影响
    }

    [Fact]
    public async Task C1_SiblingWorkspaceAndSession_Unaffected()
    {
        var (vm, query, database, assets, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        var workspaces = await OpenWorkspacesAsync(vm, query, 2);
        var siblingBinding = vm.GetViewportBinding(workspaces[1])!;

        vm.SelectedWorkspace = workspaces[0];
        await vm.CloseWorkspaceCommand.ExecuteAsync(null);

        Assert.True(workspaces[0].IsClosed);
        Assert.False(workspaces[1].IsClosed);
        Assert.False(siblingBinding.IsClosed);
        Assert.Same(workspaces[1], vm.Workspaces.Single());
        Assert.Equal(ProjectSessionState.Ready, session.State);
        Assert.Equal(0, database.DisposeCount); // AssetStore / Database 不被提前关闭
        Assert.Equal(0, assets.DisposeCount);
    }

    // ---- C2：项目关闭编排 ----

    [Fact]
    public async Task C2_ClosesAllBindings_InOrder_BeforeSessionClose()
    {
        var (vm, query, database, _, session) = CreateVm();
        var gates = new List<ProbeRenderer>();
        var bindings = new List<MapViewportBinding>();
        vm.TestBindingFactory = ws =>
        {
            var renderer = new ProbeRenderer(gate: true);
            var binding = new MapViewportBinding(ws.ViewState, renderer: renderer);
            renderer.BindingClosedAtDisposeEntry = () => binding.IsClosed;
            gates.Add(renderer);
            bindings.Add(binding);
            return binding;
        };
        var workspaces = await OpenWorkspacesAsync(vm, query, 2);

        // 门控测试：编排触发移至工作线程（生产 renderer.Dispose 为空操作不阻塞；
        // 替身门控用于制造确定性交错，需要独立线程放行）。
        var closeTask = Task.Run(() => vm.CloseProjectLifetimeAsync());

        // 第一个 Binding 的子序进入 renderer.Dispose 后放行。
        await gates[0].DisposeEntered.WaitAsync(GateTimeout);
        Assert.True(bindings[0].IsClosed);
        gates[0].ReleaseDispose();

        // 第二个 Binding 的子序进入 renderer.Dispose 时，会话仍必须 Ready
        // ——真实程序序证据：全部 Binding 关闭先于 Session.CloseAsync。
        await gates[1].DisposeEntered.WaitAsync(GateTimeout);
        Assert.True(bindings[1].IsClosed);
        Assert.Equal(ProjectSessionState.Ready, session.State);

        gates[1].ReleaseDispose();
        await closeTask;

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(database.IsDisposed);
        Assert.Empty(vm.Workspaces);        // 步骤 3：集合清理
        Assert.Null(vm.GetViewportBinding(workspaces[0])); // 已关闭 Binding 不再可用
        Assert.Null(vm.GetViewportBinding(workspaces[1]));
        Assert.All(gates, g => Assert.Equal(1, g.DisposeCount));
    }

    [Fact]
    public async Task C2_SharedTask_CommandAndDirectCalls_ConvergeToSingleExecution()
    {
        var (vm, query, _, _, session) = CreateVm();
        var gated = new ProbeRenderer(gate: true);
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState, renderer: gated);
        await OpenWorkspacesAsync(vm, query, 1);

        var projectClosedCount = 0;
        vm.ProjectClosed += () => projectClosedCount++;

        // 门控测试：首次编排触发移至工作线程（同 C1/C2 顺序测试的理由）。
        var direct1 = Task.Run(() => vm.CloseProjectLifetimeAsync());

        // 编排已启动并挂起在 renderer.Dispose 门控内——此时重入调用立即返回同一任务
        // （测试线程不阻塞），命令路径也与直接调用共享同一任务，不产生第二套执行。
        await gated.DisposeEntered.WaitAsync(GateTimeout);
        var direct2 = vm.CloseProjectLifetimeAsync();
        var direct3 = vm.CloseProjectLifetimeAsync();
        // 幂等契约：直调重入返回同一任务实例（Task.Run 的 Unwrap 包装不参与实例比对，
        // 汇聚证据由下方"只执行一次"断言给出）。
        Assert.Same(direct2, direct3);

        var commandTask = vm.CloseProjectCommand.ExecuteAsync(null);

        // 编排被门控挂起期间，编排任务仍未完成，所有等待者都挂起（不并行、不跳过）。
        Assert.False(direct1.IsCompleted);
        Assert.False(vm.IsProjectCloseLifetimeFinished);

        gated.ReleaseDispose();
        await Task.WhenAll(direct1, direct2, commandTask);

        Assert.Equal(1, projectClosedCount); // ProjectClosed 全程只触发一次
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Equal(1, gated.DisposeCount); // C2 整体只执行一次
    }

    // ---- DEF-02：Finished 标志先于 C2 任务完成 ----

    [Fact]
    public async Task C2_Success_FinishedFlagSet_BeforeTaskCompletion()
    {
        // 不变量：任何观察者从 completion.Task 恢复（哪怕在 SetResult 调用栈内同步
        // 恢复——ExecuteSynchronously 是最早观察点）时，Finished 必须已为 true，
        // 窗口 Closing 收尾才不会再次取消最终关闭。
        var (vm, query, _, _, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        // 观察者经 TCS 确认已运行（RunContinuationsAsynchronously 会把
        // ExecuteSynchronously 续延也排队，必须显式等待观察点执行后再断言）。
        var observerRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishedSeenByCompletionObserver = false;
        var task = vm.CloseProjectLifetimeAsync();
        task.ContinueWith(
            _ =>
            {
                finishedSeenByCompletionObserver = vm.IsProjectCloseLifetimeFinished;
                observerRan.SetResult();
            },
            TaskContinuationOptions.ExecuteSynchronously);

        await task;
        await observerRan.Task.WaitAsync(GateTimeout);

        Assert.True(finishedSeenByCompletionObserver); // 完成时刻（最早观察点）Finished 已为 true
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Equal(ProjectSessionState.Closed, session.State);
    }

    [Fact]
    public async Task C2_Failure_FinishedFlagSet_BeforeTaskCompletion()
    {
        // 失败路径同序：SetException 之前 Finished 必须已为 true。
        var (vm, query, database, _, _) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);
        database.DisposeFailure = new InvalidOperationException("数据库释放失败（测试注入）。");

        var observerRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishedSeenByCompletionObserver = false;
        var task = vm.CloseProjectLifetimeAsync();
        task.ContinueWith(
            _ =>
            {
                finishedSeenByCompletionObserver = vm.IsProjectCloseLifetimeFinished;
                observerRan.SetResult();
            },
            TaskContinuationOptions.ExecuteSynchronously);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.WaitAsync(GateTimeout));
        await observerRan.Task.WaitAsync(GateTimeout);

        Assert.Equal("数据库释放失败（测试注入）。", ex.Message);
        Assert.True(finishedSeenByCompletionObserver); // 失败完成时刻 Finished 已为 true
        Assert.True(vm.IsProjectCloseLifetimeFinished);
    }

    [Fact]
    public async Task C2_ProjectClosedFires_BeforeOrchestrationTaskCompletes_R5WindowContract()
    {
        // R5 窗口契约的实现证据：ProjectClosed（步骤 4）触发时 C2 任务尚未完成——
        // 真实窗口的 Closing 处理器会取消该次 Close() 并等待编排结束后收尾重关。
        var (vm, query, _, _, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        Task? taskAtClosedEvent = null;
        var finishedAtClosedEvent = false;
        vm.ProjectClosed += () =>
        {
            taskAtClosedEvent = vm.CloseProjectLifetimeAsync(); // 重入路径拿到同一任务
            finishedAtClosedEvent = vm.IsProjectCloseLifetimeFinished;
        };

        var task = vm.CloseProjectLifetimeAsync();
        await task;

        // 交错时序断言（真实顺序，非终态推断）：
        Assert.NotNull(taskAtClosedEvent);
        Assert.Same(task, taskAtClosedEvent);        // 重入共享同一任务
        Assert.False(finishedAtClosedEvent);          // ProjectClosed 时编排未结束 → 窗口须取消该次 Close

        // 编排结束后：finished=true；重入关闭（模拟 Closing 重入）拿到已完成任务，无死锁。
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);
        Assert.Equal(ProjectSessionState.Closed, session.State);
    }

    [Fact]
    public async Task C2_Failure_ExceptionObserved_FinishedFlagSet_NoProjectClosed()
    {
        var (vm, query, database, assets, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        var projectClosedCount = 0;
        vm.ProjectClosed += () => projectClosedCount++;

        database.DisposeFailure = new InvalidOperationException("数据库释放失败（测试注入）。");

        // 等待者观察到异常（被观察，不留未观察异常）。
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout));

        Assert.Equal("数据库释放失败（测试注入）。", ex.Message);
        Assert.True(vm.IsProjectCloseLifetimeFinished);  // 失败也算"编排结束"→ 窗口放行关闭
        Assert.Equal(0, projectClosedCount);             // 步骤 4 未到达：不通知窗口

        // 共享任务：重复调用返回同一任务、观察到同一异常（继续被观察，不二次执行）。
        var second = vm.CloseProjectLifetimeAsync();
        Assert.Same(vm.CloseProjectLifetimeAsync(), second);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(GateTimeout));
        Assert.Equal(1, database.DisposeCount);

        _ = assets;
        _ = session;
    }

    [Fact]
    public async Task C2_PreStartCancellation_RejectedBeforeStart_NothingClosed()
    {
        var (vm, query, database, _, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => vm.CloseProjectLifetimeAsync(cts.Token));

        Assert.Equal(ProjectSessionState.Ready, session.State); // 启动前拒绝：未开始任何关闭
        Assert.False(database.IsDisposed);
        Assert.False(vm.IsProjectCloseLifetimeFinished);
    }

    [Fact]
    public async Task C2_CommandPath_CatchesFailure_StatusObservable()
    {
        var (vm, query, database, _, _) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        database.DisposeFailure = new InvalidOperationException("数据库释放失败（测试注入）。");

        // 命令路径：命令体捕获编排异常并呈现（异常被观察，不逃逸为未观察任务）。
        await vm.CloseProjectCommand.ExecuteAsync(null);

        Assert.Contains("项目关闭失败", vm.WorkspaceStatusText);
        Assert.True(vm.IsProjectCloseLifetimeFinished);
    }
}
