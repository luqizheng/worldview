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
using WorldView.Desktop.Ui;
using WorldView.Desktop.ViewModels;
using WorldView.Domain.Time;
using WorldView.Presentation.Publication;
using Xunit;

namespace WorldView.Desktop.Tests.Presentation;

/// <summary>
/// WP-RA-02-A R1 修复专项测试（R1-P1-01 / R1-P2-01）+ R2 修复专项测试（R2-P0）。
///
/// R1-P1-01（异步线程边界）：ContextSwitched 可能在 ThreadPool 线程触发
/// （真实异步 Context Resolver 的完成线程）；验证由事件触发的 ViewModel 属性
/// 更新确定性经过 UI Dispatcher 投递——不在事件源线程直接执行，投递执行时
/// 工作区已关闭 / 请求已过期的情况被正确跳过，异常可观察可记录。
///
/// R1-P2-01（请求身份）：每 Binding 实例私有请求身份；晚到的过期请求
/// （Superseded / 迟到失败 / 关闭后的迟到完成）不得覆盖当前请求的
/// LastRequestOutcome 与状态文本；不同 Workspace 互不影响；关闭再重新打开后
/// 旧实例的请求不能污染新实例。
///
/// R2-P0（失败路径请求身份保护，统一发布路径）：失败结果（含 Binding 归一化的
/// 管线异常）与成功结果共用同一套 IsRequestCurrent 守卫——投递前与回调执行时
/// 双重身份检查。旧请求晚到失败（投递前守卫）与已入队失败回调在新请求启动后
/// 执行（回调内守卫）均不得覆盖新请求的 UI 状态；当前最新请求的失败正常发布。
///
/// 竞态构造全部经 TCS 门控的 ScenePreparation、门控/工作线程解析替身与队列式
/// UI 调度器（DrainOne 精确控制回调执行时刻），绝无 Thread.Sleep；轮询仅等待
/// "已被门控确定的离散状态"的传播，带超时上限。
/// </summary>
public class DesktopPresentationR1RemediationTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    // ---- 测试支持 ----

    /// <summary>
    /// 队列式受控 UI 调度器：Post 只入队不执行；Drain 由测试在受控时刻执行，
    /// 并记录执行线程——投递边界（不在事件源线程执行 UI 更新）的确定性验证点。
    /// </summary>
    private sealed class QueueingUiDispatcher : IUiDispatcher
    {
        private readonly object _gate = new();
        private readonly Queue<Action> _queue = new();

        public int PostedCount
        {
            get { lock (_gate) { return _queue.Count; } }
        }

        public int ExecutedCount { get; private set; }

        /// <summary>投递回调的实际执行线程 ID（全部应为 Drain 调用线程）。</summary>
        public List<int> ExecutionThreadIds { get; } = new();

        public bool CheckAccess() => false;

        public void Post(Action action)
        {
            lock (_gate)
            {
                _queue.Enqueue(action);
            }
        }

        public void Drain()
        {
            while (true)
            {
                Action? next;
                lock (_gate)
                {
                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    next = _queue.Dequeue();
                }

                ExecutedCount++;
                ExecutionThreadIds.Add(Environment.CurrentManagedThreadId);
                next();
            }
        }

        /// <summary>
        /// 只执行队首的一个投递（R2 专项）：精确控制回调执行时刻，确定性构造
        /// "失败回调已入队 → 新请求启动 → 旧失败回调才执行"的交错。队列为空时
        /// 返回 false 且不执行任何动作。
        /// </summary>
        public bool DrainOne()
        {
            Action? next;
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    return false;
                }

                next = _queue.Dequeue();
            }

            ExecutedCount++;
            ExecutionThreadIds.Add(Environment.CurrentManagedThreadId);
            next();
            return true;
        }
    }

    /// <summary>
    /// 真异步上下文解析替身：解析任务由 ThreadPool 线程完成（非 Task.FromResult
    /// 的同步完成），并记录完成线程——构造 ContextSwitched 在非 UI 线程触发的场景。
    /// </summary>
    private sealed class WorkerThreadContextResolver : IWorkspaceContextResolver
    {
        private readonly Queue<ValidatedWorkspaceContext> _results;

        public WorkerThreadContextResolver(params ValidatedWorkspaceContext[] results)
        {
            _results = new Queue<ValidatedWorkspaceContext>(results);
        }

        /// <summary>解析完成线程 ID（Task.Run 线程池线程，必然非测试主线程）。</summary>
        public List<int> CompletionThreadIds { get; } = new();

        public Task<WorkspaceContextResolution> ResolveAsync(
            Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
        {
            var context = _results.Dequeue();
            var completion = new TaskCompletionSource<WorkspaceContextResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task.Run(() =>
            {
                CompletionThreadIds.Add(Environment.CurrentManagedThreadId);
                completion.TrySetResult(WorkspaceContextResolution.Ok(context));
            });
            return completion.Task;
        }
    }

    /// <summary>
    /// 门控准备函数：按请求序消费脚本步（立即完成 / 门控挂起 / 抛异常）；
    /// 门控步不响应取消令牌（对齐既有 A-xx 测试的迟到交错构造方式）。
    /// </summary>
    private sealed class GatedPreparation
    {
        private sealed class Step
        {
            public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly bool Gate;
            public readonly bool Throw;

            public Step(bool gate, bool thr0w)
            {
                Gate = gate;
                Throw = thr0w;
            }
        }

        private readonly List<Step> _steps;
        private int _index;

        public GatedPreparation(params (bool Gate, bool Throw)[] scripts)
        {
            _steps = scripts.Select(s => new Step(s.Gate, s.Throw)).ToList();
        }

        public Task WaitEnteredAsync(int index) => _steps[index].Entered.Task.WaitAsync(_timeout);

        public void Release(int index) => _steps[index].Release.TrySetResult();

        public async Task<ScenePreparationOutput> PrepareAsync(PresentationRequest request, CancellationToken cancellationToken)
        {
            var step = _steps[Math.Min(_index, _steps.Count - 1)];
            Interlocked.Increment(ref _index);
            step.Entered.TrySetResult();

            if (step.Throw)
            {
                throw new InvalidOperationException("准备失败注入（R1 专项测试）。");
            }

            if (step.Gate)
            {
                await step.Release.Task.ConfigureAwait(false);
            }

            var content = new WorldView.Presentation.Scene.BlankSceneContent(new WorldView.Presentation.Scene.BlankBackgroundItem(
                WorldView.Presentation.Scene.SceneItemIds.BlankBackground, WorldView.Presentation.Scene.NeutralBackgroundTokens.BuiltinNeutralV1));
            var result = WorldView.Presentation.Results.PresentationResult<WorldView.Presentation.Scene.BlankSceneContent>.Completed(
                request.Fingerprint, WorldView.Presentation.Assembly.PresentationSubsystems.BlankScene, WorldView.Presentation.Assembly.PresentationResultSchemas.BlankScene, content);
            return new ScenePreparationOutput(result, ExplicitlyOptionalLayers: null);
        }
    }

    private static ValidatedWorkspaceContext MakeContext(Guid mapId, Guid? branchId)
        => new()
        {
            MapId = mapId,
            BranchId = branchId,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        };

    /// <summary>R1 专项组合：真实会话 + 真实 VM + 队列式 UI 调度器 + 门控 Binding。</summary>
    private static (TestVm Vm, ProjectSession Session, FakeMapQueryService MapQuery, QueueingUiDispatcher Dispatcher) BuildR1(
        Func<MapWorkspaceSession, MapViewportBinding>? bindingFactory = null,
        IWorkspaceContextResolver? resolver = null,
        FakeAppLog? log = null)
    {
        var session = TestSessions.CreateReadySession();
        var mapQuery = new FakeMapQueryService();
        var dispatcher = new QueueingUiDispatcher();
        var vm = new TestVm(
            session,
            mapQuery: mapQuery,
            contextResolver: resolver ?? new StubContextResolver(),
            createMap: new CreateMapUseCase(session, new FakeMapCreationService(mapQuery)),
            log: log,
            uiDispatcher: dispatcher,
            bindingFactory: bindingFactory);
        return (vm, session, mapQuery, dispatcher);
    }

    private sealed class TestVm : ProjectWorkspaceViewModel
    {
        private readonly Func<MapWorkspaceSession, MapViewportBinding>? _bindingFactory;

        public TestVm(
            ProjectSession session,
            IMapQueryService? mapQuery,
            IWorkspaceContextResolver? contextResolver,
            CreateMapUseCase createMap,
            IAppLog? log = null,
            IUiDispatcher? uiDispatcher = null,
            Func<MapWorkspaceSession, MapViewportBinding>? bindingFactory = null)
            : base(session, mapQuery, contextResolver, createMap, log, uiDispatcher)
        {
            _bindingFactory = bindingFactory;
        }

        internal override MapViewportBinding CreateViewportBinding(MapWorkspaceSession workspace)
            => _bindingFactory is not null ? _bindingFactory(workspace) : base.CreateViewportBinding(workspace);
    }

    private static async Task<MapWorkspaceSession> OpenAsync(TestVm vm, FakeMapQueryService mapQuery, MapSummary map)
    {
        mapQuery.Maps.Add(map);
        vm.SelectedMap = map;
        await vm.OpenWorkspaceCommand.ExecuteAsync(CancellationToken.None);
        return vm.Workspaces[^1];
    }

    private static async Task CloseViaCommandAsync(TestVm vm, MapWorkspaceSession workspace)
    {
        vm.SelectedWorkspace = workspace;
        await vm.CloseWorkspaceCommand.ExecuteAsync(CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"等待超时：{description}");
            }

            await Task.Delay(10);
        }
    }

    // ---- R1-P1-01：ContextSwitched 异步线程边界 ----

    /// <summary>
    /// 真异步 Context Resolver（ThreadPool 线程完成）触发 ContextSwitched：
    /// 属性更新必须经 UI Dispatcher 投递——不随事件源线程执行；Drain 后状态
    /// 才落地，且执行线程为调度器执行线程（非解析完成线程）。
    /// </summary>
    [Fact]
    public async Task R1P1_01_SwitchCompletedOnWorkerThread_StatusUpdateMarshalsThroughUiDispatcher()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            prep = new GatedPreparation((Gate: false, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P1-A" };
        var workspace = await OpenAsync(vm, mapQuery, map);

        var resolver = new WorkerThreadContextResolver(contextB);
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, resolver);

        // 解析任务确实异步完成于独立线程池工作项（非 Task.FromResult 的同步完成）。
        var workerThreads = resolver.CompletionThreadIds.ToList();
        Assert.NotEmpty(workerThreads);

        // 重请求完成后状态更新进入 Dispatcher 队列（确定性：已投递）。
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "语义切换状态更新投递");
        Assert.DoesNotContain("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal); // 未投递前不落地

        dispatcher.Drain();

        // 属性更新在 Drain（调度器执行线程）发生，绝不发生在解析完成线程。
        Assert.Contains("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("已发布", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.True(dispatcher.ExecutedCount >= 1);
        Assert.All(dispatcher.ExecutionThreadIds, id => Assert.Equal(Environment.CurrentManagedThreadId, id));
    }

    /// <summary>投递已入队后工作区关闭：Drain 时投递回调复查守卫生效，不修改已关闭工作区的状态。</summary>
    [Fact]
    public async Task R1P1_01_QueuedUpdateAfterWorkspaceClose_IsSkippedOnDispatch()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P1-B" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await prep!.WaitEnteredAsync(1);
        prep!.Release(1);

        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "切换状态更新已入队（工作区尚存活）");

        // 投递执行前关闭工作区（C1：Binding → Session → 集合移除）。
        await CloseViaCommandAsync(vm, workspace);
        Assert.True(binding.IsClosed);
        var statusAfterClose = vm.WorkspaceStatusText;
        Assert.Contains("已关闭", statusAfterClose, StringComparison.Ordinal);

        dispatcher.Drain();

        // 延迟投递被投递执行时的关闭守卫跳过：已关闭工作区状态不被覆盖。
        Assert.Equal(statusAfterClose, vm.WorkspaceStatusText);
        Assert.DoesNotContain("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 重请求抛出真实异常（请求创建阶段校验失败）。R2-P0 统一发布语义：异常由
    /// Binding 归一化为携带本请求身份的 Failed 结果（异常对象原样保留在
    /// SceneRequestResult.Failure 上）——VM 观察记录（日志）、失败状态经统一发布
    /// 守卫可视化（本请求是当前最新请求，守卫放行），不留未观察异常；本请求自己的
    /// Failed 结果成为 LastRequestOutcome（失败可观察，失败不冒充成功）。
    /// </summary>
    [Fact]
    public async Task R1P1_01_RequestSceneException_ExceptionObservedLoggedAndVisualized()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        // 空 MapId 的"已校验"上下文：请求创建阶段（PresentationContextFactory）抛出真实异常。
        var invalidContext = new ValidatedWorkspaceContext
        {
            MapId = Guid.Empty,
            BranchId = branchB,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        };
        var log = new FakeAppLog();
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(
            log: log,
            bindingFactory: ws =>
            {
                prep = new GatedPreparation((Gate: false, Throw: false));
                return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P1-C" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var statusBefore = vm.WorkspaceStatusText;

        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "失败状态投递");
        dispatcher.Drain();

        // 失败状态经统一发布路径可视化（该请求是最新请求 → 守卫放行）。
        Assert.Contains("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.NotEqual(statusBefore, vm.WorkspaceStatusText);
        // 异常被 VM 观察记录（Binding 归一化不吞异常）。
        Assert.True(log.WarnCount >= 1);
        Assert.Contains(log.WarnMessages, m => m.Contains("语义切换重请求失败", StringComparison.Ordinal));
        // 归一化的 Failed 结果以本请求身份记录（R2-P0：失败可观察的诊断锚点）。
        Assert.Equal(PresentationRequestState.Failed, binding.LastRequestOutcome!.State);
        Assert.Equal(2, binding.LatestInitiatedRequestIdentity);
    }

    /// <summary>脚本化同步解析替身（切换路径用；与既有 A-xx 测试同模式）。</summary>
    private sealed class ScriptedResolver : IWorkspaceContextResolver
    {
        private readonly Queue<ValidatedWorkspaceContext> _results;

        public ScriptedResolver(params ValidatedWorkspaceContext[] results)
        {
            _results = new Queue<ValidatedWorkspaceContext>(results);
        }

        public Task<WorkspaceContextResolution> ResolveAsync(
            Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
            => Task.FromResult(WorkspaceContextResolution.Ok(_results.Dequeue()));
    }

    // ---- R1-P2-01：请求完成乱序 / 状态倒退防护 ----

    /// <summary>
    /// A 先发起、B 后发起、B 先完成、A 后完成（Superseded 晚到）：
    /// 过期请求 A 不得覆盖 B 的 LastRequestOutcome 与状态文本。
    /// </summary>
    [Fact]
    public async Task R1P2_01_LateSupersededCompletion_DoesNotRegressOutcomeOrStatus()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var branchC = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        var contextC = MakeContext(mapId, branchC);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            // 请求 1（初始）立即；请求 2（切换 1）门控挂起；请求 3（切换 2）立即。
            prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false), (Gate: false, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P2-A" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        Assert.Equal(1, binding.LatestInitiatedRequestIdentity);

        // 请求 2 发起（门控挂起）→ 请求 3 发起并立即完成（取代 2）。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await prep!.WaitEnteredAsync(1);
        Assert.Equal(2, binding.LatestInitiatedRequestIdentity);
        Assert.Null(binding.Runtime.Publisher.Current); // 切换清空旧语义快照

        await workspace.SwitchContextAsync(branchC, historicalTimeInput: null, new ScriptedResolver(contextC));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "请求 3 状态投递");
        dispatcher.Drain();

        // 请求 3 为当前有效请求：状态与结果均指向它。
        var statusOfRequest3 = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfRequest3, StringComparison.Ordinal);
        Assert.Contains("已发布", statusOfRequest3, StringComparison.Ordinal);
        var outcome3 = binding.LastRequestOutcome!;
        Assert.Equal(3, outcome3.Sequence);
        Assert.Equal(PresentationRequestState.Published, outcome3.State);
        Assert.NotNull(binding.Runtime.Publisher.Current);

        // 请求 2 晚到完成（Superseded）。
        prep!.Release(1);
        await WaitUntilAsync(
            () => binding.Runtime.Coordinator.Counters.Superseded == 1,
            "过期请求晚到落定");
        dispatcher.Drain(); // 若存在任何过期投递也一并排空

        // 过期请求不得覆盖：状态与 LastRequestOutcome 仍指向请求 3。
        Assert.Equal(statusOfRequest3, vm.WorkspaceStatusText);
        Assert.DoesNotContain("取代", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Same(outcome3, binding.LastRequestOutcome);
        Assert.Equal(3, binding.LastRequestOutcome!.Sequence);
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome.State);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Superseded); // 协调器如实记录该次晚到
    }

    /// <summary>两个 Workspace 的请求身份与结果互不影响（每实例身份，非全局静态）。</summary>
    [Fact]
    public async Task R1P2_01_WorkspacesIndependent_IdentityOutcomeAndSnapshotIsolated()
    {
        var mapId = Guid.NewGuid();
        var branchB1 = Guid.NewGuid();
        var branchB2 = Guid.NewGuid();
        var contextB1 = MakeContext(mapId, branchB1);
        var contextB2 = MakeContext(mapId, branchB2);
        var bindings = new Dictionary<Guid, MapViewportBinding>();
        GatedPreparation? prep1 = null;
        GatedPreparation? prep2 = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            // 每工作区独立脚本：请求 1（初始）立即；请求 2（切换）——ws1 门控，ws2 立即。
            var isWs1 = bindings.Count == 0;
            var prep = new GatedPreparation((Gate: false, Throw: false), (Gate: isWs1, Throw: false));
            if (isWs1) { prep1 = prep; } else { prep2 = prep; }
            var binding = new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            bindings[ws.WorkspaceId] = binding;
            return binding;
        });

        var map1 = new MapSummary { MapId = mapId, DisplayName = "R1P2-WS1" };
        var map2 = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "R1P2-WS2" };
        var workspace1 = await OpenAsync(vm, mapQuery, map1);
        var workspace2 = await OpenAsync(vm, mapQuery, map2);
        var binding1 = bindings[workspace1.WorkspaceId];
        var binding2 = bindings[workspace2.WorkspaceId];
        Assert.Equal(1, binding1.LatestInitiatedRequestIdentity);
        Assert.Equal(1, binding2.LatestInitiatedRequestIdentity);

        // ws1 切换（门控挂起）；ws2 切换（立即完成）。
        var switch1 = workspace1.SwitchContextAsync(branchB1, historicalTimeInput: null, new ScriptedResolver(contextB1));
        await prep1!.WaitEnteredAsync(1);
        await workspace2.SwitchContextAsync(branchB2, historicalTimeInput: null, new ScriptedResolver(contextB2));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "ws2 切换状态投递");
        dispatcher.Drain();
        await switch1;

        // ws2 已完成且状态落地；ws1 请求仍挂起。
        Assert.Contains("R1P2-WS2", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Equal(2, binding2.LatestInitiatedRequestIdentity);
        Assert.NotNull(binding2.Runtime.Publisher.Current);
        Assert.Equal(contextB2.BranchId, binding2.Runtime.Publisher.Current!.Context.BranchId);

        // ws1 晚到完成 → 只影响 ws1 自己的实例。
        prep1!.Release(1);
        await WaitUntilAsync(() => binding1.LastRequestOutcome is not null && binding1.LatestInitiatedRequestIdentity == 2
            && binding1.LastRequestOutcome.Sequence == 2, "ws1 切换请求落定");
        dispatcher.Drain();

        Assert.Equal(PresentationRequestState.Published, binding1.LastRequestOutcome!.State);
        Assert.Equal(contextB1.BranchId, binding1.Runtime.Publisher.Current!.Context.BranchId);

        // 互不影响：ws2 的结果、快照与身份未被 ws1 的完成触碰。
        Assert.Equal(2, binding2.LatestInitiatedRequestIdentity);
        Assert.Equal(2, binding2.LastRequestOutcome!.Sequence);
        Assert.Equal(contextB2.BranchId, binding2.Runtime.Publisher.Current!.Context.BranchId);
        Assert.NotSame(binding1.Runtime.Publisher.Current, binding2.Runtime.Publisher.Current);
        Assert.NotSame(binding1.Runtime.Publisher, binding2.Runtime.Publisher);
    }

    /// <summary>Workspace 关闭后旧请求不能更新状态；关闭再重新打开后旧实例不能污染新实例。</summary>
    [Fact]
    public async Task R1P2_01_ClosedThenReopened_OldInstanceCannotPolluteNewInstance()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        var bindings = new List<MapViewportBinding>();
        var preps = new List<GatedPreparation>();
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            // 第一个 Binding：请求 1 立即 + 请求 2 门控；后续 Binding（重开）：立即。
            var first = bindings.Count == 0;
            var prep = first
                ? new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false))
                : new GatedPreparation((Gate: false, Throw: false));
            preps.Add(prep);
            var binding = new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            bindings.Add(binding);
            return binding;
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P2-B" };
        var workspace1 = await OpenAsync(vm, mapQuery, map);
        var binding1 = bindings[0];

        await workspace1.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await preps[0].WaitEnteredAsync(1);
        Assert.Equal(2, binding1.LatestInitiatedRequestIdentity);

        // 关闭工作区（请求 2 仍挂起在门控准备中）。
        await CloseViaCommandAsync(vm, workspace1);
        Assert.True(binding1.IsClosed);

        // 旧实例的挂起请求晚到完成（协调器已关闭 → RejectedClosed）。
        preps[0].Release(1);
        await WaitUntilAsync(
            () => binding1.LastRequestOutcome is { } o && o.Sequence == 2,
            "旧实例迟到结果落定");
        dispatcher.Drain();

        // 关闭守卫：旧实例的迟到结果不得污染状态文本。
        Assert.DoesNotContain("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Equal(PresentationRequestState.RejectedClosed, binding1.LastRequestOutcome!.State);

        // 重新打开同一地图：全新 Workspace / Binding / Runtime，身份从 1 独立计数。
        var workspace2 = await OpenAsync(vm, mapQuery, map);
        var binding2 = bindings[1];
        Assert.NotSame(workspace1, workspace2);
        Assert.NotSame(binding1, binding2);
        Assert.Equal(1, binding2.LatestInitiatedRequestIdentity);
        Assert.Equal(PresentationRequestState.Published, binding2.LastRequestOutcome!.State);
        Assert.Equal(1, binding2.LastRequestOutcome.Sequence);
        Assert.NotNull(binding2.Runtime.Publisher.Current);

        // 旧实例保持冻结：其身份与结果不被新实例触碰。
        Assert.Equal(2, binding1.LatestInitiatedRequestIdentity);
        Assert.Equal(2, binding1.LastRequestOutcome.Sequence);
        Assert.True(binding1.Runtime.Coordinator.IsClosed);
    }

    /// <summary>旧请求异常晚到（准备失败）不得覆盖当前请求状态。</summary>
    [Fact]
    public async Task R1P2_01_LateFailedRequest_DoesNotOverwriteCurrentOutcome()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var branchC = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        var contextC = MakeContext(mapId, branchC);
        var log = new FakeAppLog();
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(
            log: log,
            bindingFactory: ws =>
            {
                // 请求 1 立即；请求 2（切换 1）门控挂起后抛异常；请求 3（切换 2）立即。
                prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: true), (Gate: false, Throw: false));
                return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            });

        var map = new MapSummary { MapId = mapId, DisplayName = "R1P2-C" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await prep!.WaitEnteredAsync(1);
        await workspace.SwitchContextAsync(branchC, historicalTimeInput: null, new ScriptedResolver(contextC));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "请求 3 状态投递");
        dispatcher.Drain();
        var statusOfRequest3 = vm.WorkspaceStatusText;
        Assert.Contains("已发布", statusOfRequest3, StringComparison.Ordinal);

        // 请求 2 晚到并失败（准备异常）。
        prep!.Release(1);
        await WaitUntilAsync(
            () => binding.Runtime.Coordinator.Counters.PreparationFailures == 1,
            "旧请求晚到失败落定");
        dispatcher.Drain();

        // 当前请求状态不被覆盖（准备异常由协调器落定为 Failed 结果，非抛出路径，
        // 故过期请求不会进入失败状态投递；异常经协调器计数可观察）。
        Assert.Equal(statusOfRequest3, vm.WorkspaceStatusText);
        Assert.Equal(3, binding.LastRequestOutcome!.Sequence);
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome.State);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.PreparationFailures);
    }

    // ---- R2-P0：失败路径的请求身份保护（统一发布守卫） ----

    /// <summary>
    /// R2 规格 Test 1：旧请求晚到失败，不得覆盖新请求成功状态。
    /// 时序（确定性）：A（切换 1）门控挂起 → B（切换 2）发起并成功发布（状态可见）
    /// → A 晚到失败（门控准备异常，经协调器归一化为 Failed 结果）→ A 的失败结果
    /// 到达统一发布入口时投递前身份守卫丢弃（A 已被 B 取代）——失败回调根本不入队。
    /// 失败本身经协调器计数如实可观察（不静默）。
    /// </summary>
    [Fact]
    public async Task R2P0_LateFailureAfterNewRequestPublished_IsDiscardedBeforeDispatch()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var branchC = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        var contextC = MakeContext(mapId, branchC);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            // 请求 1（初始）立即；请求 2（切换 A）门控挂起后抛异常；请求 3（切换 B）立即。
            prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: true), (Gate: false, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R2-A" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await prep!.WaitEnteredAsync(1);
        await workspace.SwitchContextAsync(branchC, historicalTimeInput: null, new ScriptedResolver(contextC));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "请求 3 状态投递");
        dispatcher.Drain();
        var statusOfRequest3 = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfRequest3, StringComparison.Ordinal);
        Assert.Contains("已发布", statusOfRequest3, StringComparison.Ordinal);

        // A 晚到失败（门控释放后准备异常 → 协调器归一化 Failed 结果，身份 2 已过期）。
        prep!.Release(1);
        await WaitUntilAsync(
            () => binding.Runtime.Coordinator.Counters.PreparationFailures == 1,
            "旧请求晚到失败落定");
        // 等待 VM 后台 continuation 走完发布判定（此处延迟仅做传播落定，不构造竞态；
        // 竞态时序已由门控确定）。断言对象为不变量：过期失败绝不入队、绝不覆盖。
        await Task.Delay(100);

        // 投递前身份守卫：A 的失败回调未入队。
        Assert.Equal(0, dispatcher.PostedCount);
        dispatcher.Drain();

        // B 的成功状态与结果记录不被覆盖；A 的失败经协调器计数可观察（不静默）。
        Assert.Equal(statusOfRequest3, vm.WorkspaceStatusText);
        Assert.DoesNotContain("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Equal(3, binding.LastRequestOutcome!.Sequence);
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome.State);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.PreparationFailures);
    }

    /// <summary>
    /// R2 规格 Test 2：旧请求失败回调已投递（入队），但执行前新请求启动——回调执行时
    /// 身份守卫丢弃过期失败，不覆盖当前 UI 状态。时序（确定性）：A 用非法上下文切换
    /// （请求创建异常 → Binding 归一化 Failed，A 身份 2 为当期最新 → 投递前守卫放行
    /// → 回调入队）→ B 切换（门控挂起，身份 3，已启动未完成）→ DrainOne 精确执行
    /// A 的回调（身份 2 ≠ 3 → 丢弃）→ 释放 B → B 成功发布。修复前旧失败路径只检查
    /// 关闭状态，A 的失败会在此时覆盖状态文本（回归锚点）。
    /// </summary>
    [Fact]
    public async Task R2P0_QueuedFailureCallbackExecutesAfterNewRequestStarted_IsDiscardedInCallback()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var invalidContext = new ValidatedWorkspaceContext
        {
            MapId = Guid.Empty,
            BranchId = branchB,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        };
        var contextB = MakeContext(mapId, branchB);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            // 请求 1（初始）立即；请求 2（切换 B）门控挂起——B 已启动、未完成。
            // A（非法上下文切换）在请求创建阶段即失败，不消耗准备步。
            prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R2-B" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var statusAfterOpen = vm.WorkspaceStatusText;
        Assert.Equal(1, binding.LatestInitiatedRequestIdentity);

        // A：非法上下文切换——请求创建异常归一化为 Failed（身份 2 = 最新）→ 回调入队。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        await WaitUntilAsync(() => binding.LatestInitiatedRequestIdentity == 2, "A 请求发起");
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "A 失败回调入队");

        // B：启动并成为最新请求（门控挂起，尚未完成，不产生新投递）。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await WaitUntilAsync(() => binding.LatestInitiatedRequestIdentity == 3, "B 请求发起");
        await prep!.WaitEnteredAsync(1);
        Assert.Equal(1, dispatcher.PostedCount); // 队列中只有 A 的失败回调

        // 执行 A 的失败回调：回调内身份守卫（2 ≠ 3）丢弃——状态不被覆盖。
        Assert.True(dispatcher.DrainOne());
        Assert.Equal(statusAfterOpen, vm.WorkspaceStatusText);
        Assert.DoesNotContain("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);

        // B 完成 → 成功发布（修复不得把新请求的成功也丢掉）。
        prep!.Release(1);
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "B 状态投递");
        Assert.True(dispatcher.DrainOne());
        Assert.Contains("语义切换", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Contains("已发布", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }

    /// <summary>
    /// R2 规格 Test 3（当前最新请求失败正常发布）由
    /// <see cref="R1P1_01_RequestSceneException_ExceptionObservedLoggedAndVisualized"/>
    /// 承担：失败请求即当期最新请求（守卫放行）→ 失败状态可见、异常被日志观察。
    /// 本测试覆盖规格附注场景：失败回调已入队后工作区关闭——回调执行时关闭守卫
    /// 丢弃（已关闭工作区的失败状态不得更新 UI）。
    /// </summary>
    [Fact]
    public async Task R2P0_QueuedFailureCallbackAfterWorkspaceClose_IsDiscarded()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var invalidContext = new ValidatedWorkspaceContext
        {
            MapId = Guid.Empty,
            BranchId = branchB,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        };
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(bindingFactory: ws =>
        {
            prep = new GatedPreparation((Gate: false, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R2-C" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        // A：非法上下文切换（身份 2 = 最新）→ 失败回调入队。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        await WaitUntilAsync(() => dispatcher.PostedCount >= 1, "A 失败回调入队");

        // 回调执行前关闭工作区（C1：Binding → Session → 集合移除）。
        await CloseViaCommandAsync(vm, workspace);
        Assert.True(binding.IsClosed);
        var statusAfterClose = vm.WorkspaceStatusText;
        Assert.Contains("已关闭", statusAfterClose, StringComparison.Ordinal);

        // 执行 A 的失败回调：关闭守卫丢弃——已关闭工作区的状态不被覆盖。
        Assert.True(dispatcher.DrainOne());
        Assert.Equal(statusAfterClose, vm.WorkspaceStatusText);
        Assert.DoesNotContain("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
    }
}
