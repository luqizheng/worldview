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
            CreateMapUseCase createMap,
            IAppLog? log = null)
            : base(session, mapQuery, contextResolver, createMap, log)
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

        /// <summary>Dispose 故障注入（DEF-01/DEF-03：验证关闭失败经共享 Task 传播且不阻断编排）。</summary>
        public Exception? DisposeFailure { get; set; }

        public override void Dispose()
        {
            DisposeCount++;
            BindingClosedObserved = BindingClosedAtDisposeEntry?.Invoke() ?? false;
            _disposeEnteredSource?.TrySetResult();
            if (_releaseSource is { } release && !release.Task.Wait(GateTimeout))
            {
                throw new TimeoutException("renderer dispose gate 未在超时内放行。");
            }

            if (DisposeFailure is { } failure)
            {
                throw failure;
            }

            base.Dispose();
        }
    }

    private static (TestViewModel Vm, FakeMapQueryService Query, StubPackageDatabase Database, StubAssetStore Assets, ProjectSession Session)
        CreateVm(FakeAppLog? log = null)
    {
        var query = new FakeMapQueryService();
        query.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        var session = TestSessions.CreateReadySession();
        var vm = new TestViewModel(
            session,
            query,
            new FakeWorkspaceContextResolver(),
            new CreateMapUseCase(session, new FakeMapCreationService(query)),
            log);
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

    /// <summary>
    /// GetMapAsync 可门控的查询替身（P1-01）：把"打开工作区"确定性停在 Application
    /// 调用中途（进入 GetMapAsync 即挂起，直到测试放行），用于构造"C2 启动前已开始、
    /// 尚未完成的 OpenWorkspace"的真实交错——以 TaskCompletionSource 控制顺序，
    /// 绝无 Sleep / 轮询 / 调度运气。
    /// </summary>
    private sealed class GatedMapQueryService : IMapQueryService
    {
        private readonly FakeMapQueryService _inner = new();
        private readonly TaskCompletionSource _getMapEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseGetMap =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<MapSummary> Maps => _inner.Maps;

        /// <summary>门控开关：false 时 GetMapAsync 直通（首次常规打开用）；true 时挂起待放行。</summary>
        public bool GateEnabled { get; set; }

        public Task GetMapEntered => _getMapEntered.Task;

        public void ReleaseGetMap() => _releaseGetMap.TrySetResult();

        public async Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default)
        {
            if (!GateEnabled)
            {
                return await _inner.GetMapAsync(projectId, mapId, cancellationToken);
            }

            _getMapEntered.TrySetResult();
            await _releaseGetMap.Task.WaitAsync(cancellationToken);
            return await _inner.GetMapAsync(projectId, mapId, cancellationToken);
        }

        public Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => _inner.ListMapsAsync(projectId, cancellationToken);
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

    // ---- DEF-03：日志异常不得破坏生命周期收尾 ----

    [Fact]
    public async Task C2_BindingCloseFails_WithFailingLog_SessionStillCloses_ButAttemptFailsForRetry()
    {
        // 统一裁决 2.3/2.5：Binding 关闭失败 + Warn 日志自身抛异常 → 编排必须继续
        // 关闭其余 Binding 与 ProjectSession（后续安全阶段不被跳过），但本轮 C2 以
        // 聚合异常失败（未成功关闭的 Binding 绝不被静默丢弃），失败 Binding 留待重试；
        // ProjectClosed 不触发、集合不清理。
        var log = new FakeAppLog { WarnFailure = new InvalidOperationException("日志实现故障（测试注入）。") };
        var (vm, query, database, _, session) = CreateVm(log);
        var failingRenderer = new ProbeRenderer(gate: false)
        {
            DisposeFailure = new InvalidOperationException("renderer 释放失败（测试注入）。"),
        };
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState, renderer: failingRenderer);
        await OpenWorkspacesAsync(vm, query, 2);

        var projectClosedCount = 0;
        vm.ProjectClosed += () => projectClosedCount++;

        var task = vm.CloseProjectLifetimeAsync();
        var observed = await Assert.ThrowsAsync<AggregateException>(() => task.WaitAsync(GateTimeout));

        Assert.Equal(2, observed.InnerExceptions.Count); // 两个 Binding 失败按阶段聚合
        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Equal(ProjectSessionState.Closed, session.State); // 步骤 2 照常执行
        Assert.True(database.IsDisposed);
        Assert.Equal(0, projectClosedCount);       // 未全部成功：不通知窗口
        Assert.True(log.WarnCount >= 2);           // 每个 Binding 的失败都尝试过记日志（且都失败）
        Assert.Equal(2, vm.Workspaces.Count);      // 步骤 3 集合清理被跳过（不清未关闭 Binding）

        // 重试（统一裁决 2.2）：任务字段已复位 → 新一轮编排；Session.CloseAsync 幂等
        // 返回（已 Closed，代价为零），Binding 只补 renderer 失败阶段。
        failingRenderer.DisposeFailure = null;
        var retry = vm.CloseProjectLifetimeAsync();
        Assert.NotSame(task, retry);
        await retry.WaitAsync(GateTimeout);

        Assert.True(vm.IsProjectCloseLifetimeFinished);
        Assert.Equal(1, projectClosedCount);       // 补完成功：ProjectClosed 恰好一次
        Assert.Empty(vm.Workspaces);
        Assert.Same(retry, vm.CloseProjectLifetimeAsync()); // 成功终态：重复调用返回同一 Task
    }

    [Fact]
    public async Task C2_SessionCloseFails_WithFailingLog_WaiterObservesOriginalException()
    {
        // 外层：Session.CloseAsync 失败 + Warn 日志自身抛异常 → 原始生命周期异常
        // 传递给等待者（不被日志异常覆盖），Finished 置位，收尾不被跳过。
        var log = new FakeAppLog { WarnFailure = new InvalidOperationException("日志实现故障（测试注入）。") };
        var (vm, query, database, _, session) = CreateVm(log);
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        var projectClosedCount = 0;
        vm.ProjectClosed += () => projectClosedCount++;

        var originalFailure = new InvalidOperationException("数据库释放失败（测试注入）。");
        database.DisposeFailure = originalFailure;

        var task = vm.CloseProjectLifetimeAsync();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => task.WaitAsync(GateTimeout));

        Assert.Same(originalFailure, observed);   // 原始异常实例，而非日志异常
        Assert.NotSame(log.WarnFailure, observed);
        Assert.True(vm.IsProjectCloseLifetimeFinished); // 日志异常未绕过收尾
        Assert.Equal(0, projectClosedCount);            // 失败路径不通知窗口（语义不变）
        Assert.Equal(1, database.DisposeCount);

        // 重试契约（统一裁决 2.2）：任务字段已复位 → 重复调用启动新一轮编排
        // （不共享 faulted Task），故障未解除则再次观察到同一原始异常实例。
        var retry = vm.CloseProjectLifetimeAsync();
        Assert.NotSame(task, retry);
        var observedAgain = await Assert.ThrowsAsync<InvalidOperationException>(
            () => retry.WaitAsync(GateTimeout));
        Assert.Same(originalFailure, observedAgain);
        Assert.Equal(2, database.DisposeCount);

        // 故障解除后再重试：只补数据库阶段 → 全部成功，ProjectClosed 恰好一次。
        database.DisposeFailure = null;
        await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);
        Assert.Equal(1, projectClosedCount);
        Assert.Equal(3, database.DisposeCount);
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
        Assert.True(vm.IsProjectCloseLifetimeFinished);  // 失败也算"编排结束"→ 窗口收尾
        Assert.Equal(0, projectClosedCount);             // 步骤 4 未到达：不通知窗口

        // 重试契约（统一裁决 2.2）：失败后任务字段复位 → 重复调用启动新一轮编排，
        // 故障未解除则再次失败（被观察，不留未观察异常）、不伪装为成功。
        var second = vm.CloseProjectLifetimeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(GateTimeout));
        Assert.Equal(2, database.DisposeCount);

        // 故障解除后重试：补完数据库阶段 → 成功终态。
        database.DisposeFailure = null;
        await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);
        Assert.Equal(1, projectClosedCount);

        _ = assets;
        _ = session;
    }

    [Fact]
    public async Task C2_FailureThenRetry_CloseLifetimeStartSticky_NewOpenStillRejected()
    {
        // 统一裁决 2.4：编排失败进入重试不会撤销"关闭已启动"的不可逆状态——
        // C2 失败（任务字段复位为重试入口）后，新的 OpenWorkspace 仍被拒绝
        // （关闭可重试，但绝不回到可新建工作区的开放状态）。
        var (vm, query, database, _, session) = CreateVm();
        vm.TestBindingFactory = ws => new MapViewportBinding(ws.ViewState);
        await OpenWorkspacesAsync(vm, query, 1);

        database.DisposeFailure = new InvalidOperationException("数据库释放失败（测试注入）。");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout));

        Assert.True(vm.IsProjectCloseLifetimeFinished); // 本轮编排已结束
        Assert.True(vm.IsProjectCloseLifetimeStarted);  // sticky：仍处不可逆关闭状态

        vm.SelectedMap = query.Maps[0];
        await vm.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.Contains("无法打开", vm.WorkspaceStatusText); // 失败后依然拒绝新建工作区

        database.DisposeFailure = null;
        await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);

        Assert.True(vm.IsProjectCloseLifetimeStarted);  // 全程 sticky
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(vm.Workspaces);
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
        Assert.False(vm.IsProjectCloseLifetimeStarted); // P1-01：启动前取消不算"已启动"，打开不被拒绝
    }

    // ---- P1-01：关闭期间禁止创建工作区 ----

    [Fact]
    public async Task C2_InProgress_NewOpenWorkspaceRejected_ImmediatelyAndWithoutSideEffects()
    {
        var (vm, query, _, _, session) = CreateVm();
        var gated = new ProbeRenderer(gate: true);
        var factoryCalls = 0;
        vm.TestBindingFactory = ws =>
        {
            factoryCalls++;
            return new MapViewportBinding(ws.ViewState, renderer: gated);
        };
        await OpenWorkspacesAsync(vm, query, 1);

        // C2 启动并挂起在 Binding 的 renderer.Dispose 门控内（关闭进行中、未结束）。
        var closeTask = Task.Run(() => vm.CloseProjectLifetimeAsync());
        await gated.DisposeEntered.WaitAsync(GateTimeout);

        // 线性化点已过：C2 一启动即不可逆关闭状态（即使编排体尚未跑完）。
        Assert.True(vm.IsProjectCloseLifetimeStarted);
        Assert.False(vm.IsProjectCloseLifetimeFinished);

        // 此刻发起新的打开：必须被立即拒绝，且无任何副作用（不触达 Application、
        // 不创建 Binding、不登记集合）。
        vm.SelectedMap = query.Maps[0];
        await vm.OpenWorkspaceCommand.ExecuteAsync(null);

        Assert.Contains("无法打开", vm.WorkspaceStatusText);
        Assert.Equal(1, factoryCalls);                          // 未创建新 Binding
        Assert.Single(vm.Workspaces);                           // 未登记新工作区
        Assert.Single(session.Workspaces);                      // Application 侧无新挂载

        gated.ReleaseDispose();
        await closeTask;

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(vm.Workspaces);
    }

    [Fact]
    public async Task C2_InFlightOpenWorkspace_StartedBeforeClose_IsSalvagedAndNotRegistered()
    {
        // 在途打开的确定性交错：OpenWorkspace 停在 GetMapAsync 门控内 → C2 启动并
        // 挂起在 Binding 关闭门控内（会话仍 Ready）→ 放行打开：Application 侧创建
        // 成功，但 VM 注册前复查发现 C2 已启动 → 就地关闭该工作区，不登记、不展示。
        var gatedQuery = new GatedMapQueryService();
        gatedQuery.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        var session = TestSessions.CreateReadySession();
        var vm = new TestViewModel(
            session,
            gatedQuery,
            new FakeWorkspaceContextResolver(),
            new CreateMapUseCase(session, new FakeMapCreationService()));
        var gated = new ProbeRenderer(gate: true);
        var factoryCalls = 0;
        vm.TestBindingFactory = ws =>
        {
            factoryCalls++;
            return new MapViewportBinding(ws.ViewState, renderer: gated);
        };

        // 先打开一个常规工作区（VM 集合与 Application 会话各 1 个）。
        vm.SelectedMap = gatedQuery.Maps[0];
        await vm.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.Single(vm.Workspaces);
        Assert.Single(session.Workspaces);
        var firstWorkspace = vm.Workspaces[0];

        // 第二次打开：停在 GetMapAsync 门控内（打开已开始、尚未完成）。
        gatedQuery.GateEnabled = true;
        vm.SelectedMap = gatedQuery.Maps[0];
        var openTask = vm.OpenWorkspaceCommand.ExecuteAsync(null);
        await gatedQuery.GetMapEntered.WaitAsync(GateTimeout);

        // 打开仍在途时启动 C2：编排挂在第一个 Binding 的 renderer.Dispose 门控内，
        // 会话尚未关闭（步骤 2 未到达）→ 在途打开仍能创建工作区（真实交错窗口）。
        var closeTask = Task.Run(() => vm.CloseProjectLifetimeAsync());
        await gated.DisposeEntered.WaitAsync(GateTimeout);

        // 放行在途打开：Application 侧创建成功，但注册前复查发现 C2 已启动 → 就地回收。
        gatedQuery.ReleaseGetMap();
        await openTask.WaitAsync(GateTimeout);

        Assert.Contains("拦截", vm.WorkspaceStatusText);
        Assert.Equal(1, factoryCalls);               // 孤儿工作区未创建 Binding
        Assert.Single(vm.Workspaces);                // 未登记进 VM 集合
        Assert.Single(session.Workspaces);           // 已从 Application 会话侧移除（就地关闭）
        var orphan = session.Workspaces.SingleOrDefault(w => !vm.Workspaces.Contains(w));
        Assert.Null(orphan);

        // 放行 C2：排空既有 Binding → 关会话 → 清集合，编排正常完成。
        gated.ReleaseDispose();
        await closeTask.WaitAsync(GateTimeout);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(vm.Workspaces);
        Assert.Empty(session.Workspaces);
        Assert.Null(vm.GetViewportBinding(firstWorkspace)); // 关闭后 Binding 不再可用
    }

    [Fact]
    public async Task C2_CompletedDuringInFlightOpen_OpenFailsGracefullyWithNoLeak()
    {
        // 更晚到达的变体：C2 在在途打开完成前已整体结束（会话 Closed）→
        // Application 晚到达保护拒绝挂载 → 命令路径捕获并如实呈现，无任何泄漏。
        var gatedQuery = new GatedMapQueryService();
        gatedQuery.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        var session = TestSessions.CreateReadySession();
        var vm = new TestViewModel(
            session,
            gatedQuery,
            new FakeWorkspaceContextResolver(),
            new CreateMapUseCase(session, new FakeMapCreationService()));
        var factoryCalls = 0;
        vm.TestBindingFactory = ws =>
        {
            factoryCalls++;
            return new MapViewportBinding(ws.ViewState);
        };

        // 打开停在 GetMapAsync 门控内；此时项目内还没有任何工作区。
        gatedQuery.GateEnabled = true;
        vm.SelectedMap = gatedQuery.Maps[0];
        var openTask = vm.OpenWorkspaceCommand.ExecuteAsync(null);
        await gatedQuery.GetMapEntered.WaitAsync(GateTimeout);

        // C2 无门控整体跑完：会话 Closed。
        var closeTask = Task.Run(() => vm.CloseProjectLifetimeAsync());
        await closeTask.WaitAsync(GateTimeout);
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(vm.IsProjectCloseLifetimeStarted);

        // 放行在途打开：晚到达保护抛出 → 被命令路径捕获，不崩溃、不登记。
        gatedQuery.ReleaseGetMap();
        await openTask.WaitAsync(GateTimeout);

        Assert.Contains("打开工作区失败", vm.WorkspaceStatusText);
        Assert.Equal(0, factoryCalls);               // 未创建任何 Binding
        Assert.Empty(vm.Workspaces);
        Assert.Empty(session.Workspaces);
    }

    [Fact]
    public async Task C2_InFlightOpenWorkspace_SalvagePath_WithFailingLog_StillCleansUpOrphan()
    {
        // P2-01：孤儿回收路径上的日志必须 best-effort——日志实现的 Warn 抛异常时，
        // 绝不允许提前退出，否则 Session.CloseWorkspaceAsync / workspace.DisposeAsync
        // 都不会执行，孤儿工作区清理被日志故障阻断（同 DEF-03 日志纪律）。
        //
        // 孤儿 Dispose 的直接可观察证据（本测试核心）：
        // MapWorkspaceSession 由 ProjectSession 内部创建且回收后即从会话移除、引用不可达，
        // 故经 WorkspaceCreatedHookForTests 在创建点捕获实例（回调同步、仅捕获引用），
        // 并以 MapWorkspaceSession.DisposeCount + DerivedWorkCancellationToken 的
        // ObjectDisposedException 双重证据直接断言孤儿 DisposeAsync 恰好执行一次。
        // 交错方式与 C2_InFlightOpenWorkspace_StartedBeforeClose 测试一致：
        // 打开停在 GetMapAsync 门控 → C2 启动并挂在 Binding 关闭门控 → 放行打开，
        // 全程 TaskCompletionSource 门控，无 Sleep / 轮询 / 调度运气。
        var log = new FakeAppLog { WarnFailure = new InvalidOperationException("日志实现故障（测试注入）。") };
        var gatedQuery = new GatedMapQueryService();
        gatedQuery.Maps.Add(new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" });
        var session = TestSessions.CreateReadySession();
        var vm = new TestViewModel(
            session,
            gatedQuery,
            new FakeWorkspaceContextResolver(),
            new CreateMapUseCase(session, new FakeMapCreationService()),
            log);

        // 创建点捕获：created[0] = 先打开的常规工作区，created[1] = 被拦截的孤儿。
        var created = new List<MapWorkspaceSession>();
        session.WorkspaceCreatedHookForTests = created.Add;

        var gated = new ProbeRenderer(gate: true);
        var factoryCalls = 0;
        vm.TestBindingFactory = ws =>
        {
            factoryCalls++;
            return new MapViewportBinding(ws.ViewState, renderer: gated);
        };

        // 先打开一个常规工作区（C2 需要一个存活 Binding 才会挂起在关闭门控内）。
        vm.SelectedMap = gatedQuery.Maps[0];
        await vm.OpenWorkspaceCommand.ExecuteAsync(null);
        Assert.Single(vm.Workspaces);
        Assert.Single(session.Workspaces);
        var firstWorkspace = vm.Workspaces[0];
        Assert.Single(created);
        Assert.Same(firstWorkspace, created[0]); // 捕获列表与正式集合一一对应

        // 第二次打开：停在 GetMapAsync 门控内（打开已开始、尚未完成）。
        gatedQuery.GateEnabled = true;
        vm.SelectedMap = gatedQuery.Maps[0];
        var openTask = vm.OpenWorkspaceCommand.ExecuteAsync(null);
        await gatedQuery.GetMapEntered.WaitAsync(GateTimeout);

        // 打开仍在途时启动 C2：编排挂在第一个 Binding 的 renderer.Dispose 门控内。
        var closeTask = Task.Run(() => vm.CloseProjectLifetimeAsync());
        await gated.DisposeEntered.WaitAsync(GateTimeout);
        Assert.True(vm.IsProjectCloseLifetimeStarted);

        // 放行在途打开：回收路径执行，且日志异常不得使其中途退出。
        gatedQuery.ReleaseGetMap();
        await openTask.WaitAsync(GateTimeout); // 若日志异常外溢，此命令任务将 fault，测试即失败

        // 1) 日志确实被调用且确实抛出了异常（best-effort 吞掉，而非未调用或未抛出）。
        Assert.Equal(1, log.WarnCount);
        Assert.Equal(1, log.WarnThrowCount);
        Assert.Contains("已被拦截，就地关闭", log.WarnMessages[0]);

        // 2) 回收路径未因日志异常提前退出：状态为拦截文案而非"打开工作区失败"。
        Assert.Contains("拦截", vm.WorkspaceStatusText);
        Assert.DoesNotContain("打开工作区失败", vm.WorkspaceStatusText);

        // 3) 孤儿实例已创建（第二个），且与常规工作区是不同实例——计数互不误计的前提。
        Assert.Equal(2, created.Count);
        var orphan = created[1];
        Assert.NotSame(firstWorkspace, orphan);
        Assert.False(orphan.WorkspaceId == firstWorkspace.WorkspaceId);

        // 4) Session.CloseWorkspaceAsync 确实执行的确定性证据：C2 仍挂在第一个 Binding 的
        //    关闭门控内（其 Session.CloseAsync 尚未到达步骤 2），而孤儿已不在 Application
        //    会话集合中——只有回收路径的 CloseWorkspaceAsync 能把它移除。
        Assert.Single(session.Workspaces);
        Assert.DoesNotContain(orphan, session.Workspaces);
        Assert.Single(vm.Workspaces);
        Assert.DoesNotContain(orphan, vm.Workspaces); // 未登记进 VM 正式集合

        // 5) 孤儿 DisposeAsync 确实执行且恰好一次（直接计数断言，不靠集合数量推断）。
        Assert.Equal(1, orphan.DisposeCount);

        // 6) 独立佐证：DisposeAsync 释放派生工作取消源后，访问其 Token 抛
        //    ObjectDisposedException——Dispose 副作用的直接可观察后果。
        Assert.Throws<ObjectDisposedException>(() => orphan.DerivedWorkCancellationToken);

        // 7) 常规工作区此刻尚未被释放（C2 的 Session.CloseAsync 还没到），证明
        //    orphan.DisposeCount 不是把第一个工作区的释放误计到了孤儿头上。
        Assert.Equal(0, firstWorkspace.DisposeCount);

        // 放行 C2：编排正常完成，全项目关闭，无任何孤儿资源泄漏。
        gated.ReleaseDispose();
        await closeTask.WaitAsync(GateTimeout);

        // 8) C2 完成后：孤儿计数不变（已从会话移除，Session.CloseAsync 不会二次释放）；
        //    常规工作区由 Session.CloseAsync 释放恰好一次。
        Assert.Equal(1, orphan.DisposeCount);
        Assert.Equal(1, firstWorkspace.DisposeCount);

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(vm.Workspaces);
        Assert.Empty(session.Workspaces);   // 无孤儿残留
        Assert.Equal(1, factoryCalls);      // 孤儿未创建 Binding
        Assert.Equal(1, log.WarnCount);     // 日志异常未引发连锁失败
        Assert.Equal(1, log.WarnThrowCount);
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
