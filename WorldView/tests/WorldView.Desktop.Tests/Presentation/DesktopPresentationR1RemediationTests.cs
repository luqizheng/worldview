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
/// R3（发布原子性 + 确定性测试）：UI 状态发布的"身份判定 + 状态写入"经
/// Binding 的 <c>PublishIfRequestCurrent</c> 在 <c>_stateGate</c> 同一临界区内
/// 原子完成（与请求身份分配共用同一把锁）——新请求的身份分配不可能插入旧请求
/// 的判定与写入之间。过期结果丢弃经 Info 留痕（确定性完成信号），固定
/// <c>Task.Delay</c> 已全部移除。R3 路径测试的完成信号全部为 TCS
/// （<c>WaitPostedAsync</c> / <c>WaitForInfoAsync</c>），无轮询。
///
/// R3-R2（Test A 拆分 + 测试边界）：原 Test A 一分为二——
/// <list type="bullet">
/// <item><c>R3P0_PublishActionHoldsIdentityGate_UntilActionCompletes</c>：Binding
///   底层原子性测试，直接驱动 <c>PublishIfRequestCurrent</c>，暂停点在 publish
///   Action <strong>内部</strong>（Action 内暂停期间正确实现仍持锁；PropertyChanged
///   订阅者触发时 backing field 已更新，暂停位置过晚，不再用作暂停点）。</item>
/// <item><c>R3P0_ViewModelPublishesContextStatusThroughBindingGuard</c>：ViewModel
///   UI 发布接线测试，不承担底层锁原子性的证明责任。</item>
/// </list>
/// 底层测试的锁竞争证明经 <see cref="GateProbeResolver"/> 对真实
/// <c>_stateGate</c>（经生产侧 <c>StateGateForTesting</c> 观察入口，R3-R2 经用户
/// 裁决批准的唯一生产改动）在 B 的线程上执行 <c>Monitor.TryEnter</c>：锁被 A 持有
/// = 正确实现（B 确定性钉在竞争位置）；锁空闲 = 错误实现签名（断言立即稳定失败）。
/// 纯黑盒方案曾实测系统性漏检（错误实现 8/8 通过——A 唤醒先于 B 分配），
/// 故探测必须针对真实锁。<c>WaitUntilAsync</c>（有界条件轮询）仅为
/// R1/R2 既有测试保留。
///
/// 竞态构造全部经 TCS 门控的 ScenePreparation、门控/工作线程解析替身与队列式
/// UI 调度器（DrainOne 精确控制回调执行时刻），绝无 Thread.Sleep。
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

        private TaskCompletionSource? _postSignal;

        /// <summary>
        /// 确定性等待下一次投递（R3-R1-02）：注册时队列已非空则立即完成（消除
        /// "投递先于注册发生"的丢失窗口）；否则由下一次 Post 触发 TCS。无轮询。
        /// </summary>
        public Task WaitPostedAsync(TimeSpan timeout)
        {
            TaskCompletionSource? toSignal = null;
            lock (_gate)
            {
                if (_queue.Count > 0)
                {
                    return Task.CompletedTask;
                }

                _postSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                toSignal = _postSignal;
            }

            return toSignal.Task.WaitAsync(timeout);
        }

        public void Post(Action action)
        {
            TaskCompletionSource? toSignal = null;
            lock (_gate)
            {
                _queue.Enqueue(action);
                toSignal = _postSignal;
                _postSignal = null;
            }

            toSignal?.TrySetResult();
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

            // R3-02：先等门、再抛出——(Gate: true, Throw: true) 语义为"门控挂起，
            // 释放后失败"，与各测试脚本注释的意图一致；(Gate: false, Throw: true)
            // 仍为立即失败，既有脚本行为不变。
            if (step.Gate)
            {
                await step.Release.Task.ConfigureAwait(false);
            }

            if (step.Throw)
            {
                throw new InvalidOperationException("准备失败注入（R1 专项测试）。");
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

    /// <summary>
    /// 有界条件轮询（R1/R2 既有测试的传播等待，带超时上限）。R3-R1-02 起：
    /// R3 路径测试不得使用本方法——其完成信号一律为 TCS
    /// （<c>QueueingUiDispatcher.WaitPostedAsync</c> / <c>FakeAppLog.WaitForInfoAsync</c>）。
    /// </summary>
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

    /// <summary>
    /// 锁竞争位置探测解析器（R3-R2，方案一）：在 B 的线程上对
    /// <see cref="MapViewportBinding.StateGateForTesting"/>（即生产字段
    /// <c>_stateGate</c> 的同一实例）执行 <c>Monitor.TryEnter(0)</c>，
    /// 把"A 的 publish Action 执行期间真实锁是否被持有"的观测结果经 TCS
    /// 交给测试线程，然后照常返回——B 继续沿真实链路推进到
    /// <c>RequestSceneAsync</c> 的身份分配（正确实现下在此被 A 持有的锁阻塞）。
    /// <para>判定语义（本场景锁只有两个持有者：A 的发布临界区、B 即将进入的
    /// 分配临界区；B 自身在探测时刻位于分配锁之前，不可能持锁）：</para>
    /// <list type="bullet">
    /// <item>TryEnter 失败（false）⇒ 锁被 A 持有 ⇒ 正确实现；B 的真实
    ///   <c>Monitor.Enter</c> 必然阻塞——B 已被确定性钉在锁竞争位置；</item>
    /// <item>TryEnter 成功（true）⇒ A 在 Action 内（已由 publishEntered 证明）
    ///   而锁却空闲 ⇒ 错误实现签名（判定与写入脱离同一临界区）。</item>
    /// </list>
    /// 探测先于信号（TrySetResult 在探测之后）⇒ 测试观察到信号时探测已执行；
    /// 探测到释放严格遵循 finally，不遗留锁状态。判定与后续断言无时序依赖，
    /// 对正确/错误实现均为确定性结论。
    /// </summary>
    private sealed class GateProbeResolver : IWorkspaceContextResolver
    {
        private readonly ValidatedWorkspaceContext _context;
        private readonly MapViewportBinding _binding;

        public GateProbeResolver(ValidatedWorkspaceContext context, MapViewportBinding binding)
        {
            _context = context;
            _binding = binding;
        }

        /// <summary>true = TryEnter 成功（探测时锁空闲）；false = 锁被持有（B 将阻塞于真实分配锁）。</summary>
        public TaskCompletionSource<bool> LockFreeObserved { get; }
            = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<WorkspaceContextResolution> ResolveAsync(
            Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
        {
            var gate = _binding.StateGateForTesting;
            var lockFree = false;
            try
            {
                lockFree = Monitor.TryEnter(gate, TimeSpan.Zero);
            }
            finally
            {
                if (lockFree)
                {
                    Monitor.Exit(gate);
                }
            }

            LockFreeObserved.TrySetResult(lockFree);
            return Task.FromResult(WorkspaceContextResolution.Ok(_context));
        }
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
    /// R2 规格 Test 1（R3 强化）：旧请求晚到失败，不得覆盖新请求成功状态。
    /// 时序（确定性）：A（切换 1）门控挂起 → B（切换 2）发起并成功发布（状态可见）
    /// → A 晚到失败（门控准备异常，经协调器归一化为 Failed 结果）→ A 的失败结果
    /// 到达统一发布入口时投递前身份守卫丢弃（A 已被 B 取代）——失败回调根本不入队。
    /// 失败本身经协调器计数如实可观察（不静默）。
    /// R3-02：投递前守卫丢弃时生产代码经 Info 留痕（"已丢弃（投递前守卫）"）——
    /// 该留痕即"身份判定已执行且结果为丢弃"的确定性完成信号（判定与留痕之间无
    /// 任何 await/调度点，留痕可见 ⇔ 判定已完成），此后该 continuation 不可能再
    /// 投递任何回调；本测试据信号断言 <c>PostedCount == 0</c>，不再依赖固定延迟。
    /// </summary>
    [Fact]
    public async Task R2P0_LateFailureAfterNewRequestPublished_IsDiscardedBeforeDispatch()
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
        await dispatcher.WaitPostedAsync(_timeout);
        dispatcher.Drain();
        var statusOfRequest3 = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfRequest3, StringComparison.Ordinal);
        Assert.Contains("已发布", statusOfRequest3, StringComparison.Ordinal);

        // A 晚到失败（门控释放后准备异常 → 协调器归一化 Failed 结果，身份 2 已过期）。
        prep!.Release(1);

        // R3-R1-02 确定性完成信号：投递前守卫的丢弃留痕经 FakeAppLog 的 TCS 等待器
        // 通知（注册时先查已有消息，无丢失窗口；无轮询）。该留痕在协调器计数之后
        // 同一 continuation 内发生，故其可见即"失败已落定 + 身份判定已执行且结果为
        // 丢弃"——此后该 continuation 不可能再投递任何回调。
        await log.WaitForInfoAsync(
            m => m.Contains("已丢弃（投递前守卫）", StringComparison.Ordinal), _timeout);

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

    // ---- R3：发布原子性（身份分配与 UI 发布串行化）+ 确定性测试 ----

    /// <summary>
    /// R3 规格 Test A · Binding 底层原子性（R3-R2 方案一）：直接驱动
    /// <see cref="MapViewportBinding.PublishIfRequestCurrent"/>——A 在 publish Action
    /// <strong>内部</strong>暂停（身份判定已通过、动作尚未返回），此时另一线程 B 经
    /// 真实链路 <c>SwitchContextAsync → Resolver → ContextSwitched → ViewModel →
    /// RequestSceneAsync</c> 发起请求。正确实现下 A 在整个 Action 期间持有
    /// <c>_stateGate</c>，B 的身份分配必须等到 A 的 Action 返回之后。
    ///
    /// 暂停点：Action 内部（判定之后、返回之前），覆盖"判定 → 写入"全程。此前的
    /// 暂停点是 PropertyChanged 订阅者，而属性赋值先更新 backing field 再触发事件，
    /// 订阅者接管时写入<strong>已经完成</strong>，无法验证判定与写入之间的原子性。
    ///
    /// <strong>锁竞争证明（确定性，双分支）</strong>：B 的解析器
    /// <see cref="GateProbeResolver"/> 在 B 的线程上对真实
    /// <c>_stateGate</c>（经 <c>StateGateForTesting</c>）执行
    /// <c>Monitor.TryEnter(0)</c>，探测先于信号、B 探测后继续真实链路：
    /// <list type="number">
    /// <item>TryEnter 失败 ⇒ 锁被 A 持有（本场景唯一可能持锁者）⇒ 正确实现；
    ///   B 的真实分配锁必然阻塞——B 已被确定性钉在竞争位置。放行 A 后，A 在
    ///   Action 内（仍持锁）读取最新身份必为自身身份，B 在 A 返回后（锁释放的
    ///   happens-before）才取得 identityA+1——两项断言由锁所有权证明，与调度无关。</item>
    /// <item>TryEnter 成功 ⇒ A 在 Action 内而锁空闲 ⇒ 错误实现签名，断言
    ///   <strong>立即稳定失败</strong>（无调度窗口）。实测佐证：纯黑盒方案曾因
    ///   "A 唤醒先于 B 分配"系统性漏检（错误实现 8/8 通过），故探测必须针对
    ///   真实锁。</item>
    /// </list>
    /// 同步点全部为 TCS（带超时保护），无 Task.Delay / Thread.Sleep / 轮询。
    /// </summary>
    [Fact]
    public async Task R3P0_PublishActionHoldsIdentityGate_UntilActionCompletes()
    {
        var mapId = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextB = MakeContext(mapId, branchB);
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, _) = BuildR1(bindingFactory: ws =>
        {
            // 请求 1（初始）与请求 2（B）均立即成功（单步脚本对后续请求同样生效）。
            prep = new GatedPreparation((Gate: false, Throw: false));
            return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
        });

        var map = new MapSummary { MapId = mapId, DisplayName = "R3-A" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var identityA = binding.LatestInitiatedRequestIdentity;
        Assert.Equal(1, identityA);

        var publishEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePublish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long identityInsidePublish = -1;
        int aInsidePublish = 0; // 1 = A 的 publish Action 正在执行（Volatile 读写）
        var staleInitiationObserved = false;

        // B 在身份分配之后的观察点：测试自有的 ContextSwitched 订阅者。VM 处理器先于
        // 本订阅者注册，且其同步前缀包含身份分配，故本回调在 B 的线程上严格晚于分配执行。
        workspace.ContextSwitched += _ =>
        {
            if (Volatile.Read(ref aInsidePublish) == 1)
            {
                staleInitiationObserved = true;
            }
        };

        // Act 1：A 进入 PublishIfRequestCurrent 并在 publish Action 内部暂停。
        // 正确实现：此刻持有 _stateGate，直到 Action 返回才释放。
        var publishTask = Task.Run(() => binding.PublishIfRequestCurrent(identityA, () =>
        {
            Volatile.Write(ref aInsidePublish, 1);
            publishEntered.TrySetResult();

            // 暂停在 Action 内部：身份判定已通过、动作尚未返回。超时仅为防挂保护。
            releasePublish.Task.Wait(_timeout);

            // 放行后、返回前读取最新身份（正确实现下此刻仍持锁）。
            identityInsidePublish = binding.LatestInitiatedRequestIdentity;
            Volatile.Write(ref aInsidePublish, 0);
        }));
        await publishEntered.Task.WaitAsync(_timeout);

        // Act 2：B 在另一线程经真实切换链路发起请求，其解析器在 B 的线程上对真实
        // _stateGate 探测锁状态（探测先于信号，探测后 B 继续推进到身份分配）。
        var resolver = new GateProbeResolver(contextB, binding);
        var switchTask = Task.Run(() => workspace.SwitchContextAsync(
            branchB, historicalTimeInput: null, resolver));

        // 核心不变量：A 的 publish Action 正在执行（publishEntered 已置位）期间，
        // 真实 _stateGate 必须被 A 持有。锁空闲 = 判定与写入脱离同一临界区（错误实现
        // 签名）——确定性失败，无调度窗口。TryEnter 成功时已在解析器 finally 中释放。
        var lockFree = await resolver.LockFreeObserved.Task.WaitAsync(_timeout);
        Assert.False(
            lockFree,
            "A 的 publish Action 执行期间身份门锁空闲：身份判定与状态写入未受同一锁保护（错误实现签名）。");

        // 正确实现：B 已被确定性钉在分配锁竞争位置（其真实 Monitor.Enter 阻塞中）。
        releasePublish.TrySetResult();                // 放行 → A 完成 Action → 释放锁
        var published = await publishTask.WaitAsync(_timeout);
        await switchTask.WaitAsync(_timeout);          // B 在 A 返回后取得身份并完成切换

        // Assert：各项均由锁的所有权 / happens-before 证明，与线程调度无关。
        Assert.True(published, "A 是最新请求，发布应当执行。");
        Assert.False(
            staleInitiationObserved,
            "B 的请求在 A 的 publish Action 尚未返回时即已发起（B 在分配之后观察到 A 仍在 Action 内）。");
        Assert.Equal(identityA, identityInsidePublish);
        Assert.Equal(identityA + 1, binding.LatestInitiatedRequestIdentity); // B 在 A 的 Action 返回之后才取得新身份
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome!.State);
    }

    /// <summary>
    /// R3 规格 Test A · ViewModel UI 发布接线（R3-R2 拆分）：只验证 UI 发布接线，
    /// <strong>不承担</strong>底层锁原子性的证明责任（见
    /// <see cref="R3P0_PublishActionHoldsIdentityGate_UntilActionCompletes"/>）。覆盖：
    /// <list type="number">
    /// <item><c>ContextSwitched</c> 触发 ViewModel 重请求（Binding 身份递增）；</item>
    /// <item>状态更新经 UI Dispatcher 投递：执行前 UI 状态不变，执行发生在 Dispatcher 线程；</item>
    /// <item>Dispatcher 执行后，当前请求的成功状态、失败状态均正常发布；</item>
    /// <item>旧请求不得覆盖新请求：晚到结果在投递前被丢弃；已入队的回调在回调执行时
    ///   被 Binding 的有效请求身份守卫拒绝。</item>
    /// </list>
    /// 同步点全部为 TCS（<c>WaitPostedAsync</c> / <c>WaitForInfoAsync</c>），无轮询。
    /// </summary>
    [Fact]
    public async Task R3P0_ViewModelPublishesContextStatusThroughBindingGuard()
    {
        var mapId = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var contextA = MakeContext(mapId, branchA);
        var contextB = MakeContext(mapId, branchB);
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
                // 请求 1（初始）立即；请求 2（A）门控挂起后失败；请求 3 起立即成功。
                prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: true), (Gate: false, Throw: false));
                return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            });

        var map = new MapSummary { MapId = mapId, DisplayName = "R3-VM" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;
        var statusBefore = vm.WorkspaceStatusText;

        // A（身份 2）：挂起，稍后失败。
        await workspace.SwitchContextAsync(branchA, historicalTimeInput: null, new ScriptedResolver(contextA));
        await prep!.WaitEnteredAsync(1);

        // B（身份 3）：ContextSwitched 触发重请求 → 立即成功 → 回调入队。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        Assert.Equal(3, binding.LatestInitiatedRequestIdentity);
        await dispatcher.WaitPostedAsync(_timeout);

        // 投递不等于执行：Dispatcher 执行前 UI 状态不变；执行发生在 Drain 调用线程。
        Assert.Equal(statusBefore, vm.WorkspaceStatusText);
        Assert.True(dispatcher.DrainOne());
        var statusOfB = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfB, StringComparison.Ordinal);
        Assert.Contains("已发布", statusOfB, StringComparison.Ordinal);
        Assert.All(dispatcher.ExecutionThreadIds, id => Assert.Equal(Environment.CurrentManagedThreadId, id));

        // A 晚到失败（身份 2 已过期）：投递前守卫丢弃——旧请求不得覆盖新请求。
        prep!.Release(1);
        await log.WaitForInfoAsync(m => m.Contains("已丢弃（投递前守卫）", StringComparison.Ordinal), _timeout);
        Assert.Equal(0, dispatcher.PostedCount);
        Assert.Equal(statusOfB, vm.WorkspaceStatusText);

        // C（身份 4，非法上下文）：当前请求的失败正常发布（不为过滤过期请求而误丢）。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        Assert.Equal(4, binding.LatestInitiatedRequestIdentity);
        Assert.Equal(1, dispatcher.PostedCount); // 同步链：返回时失败回调已入队
        Assert.True(dispatcher.DrainOne());
        Assert.Contains("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);

        // D（身份 5）：成功，回调入队但不执行；E（身份 6，非法上下文）：失败回调入队。
        await workspace.SwitchContextAsync(branchA, historicalTimeInput: null, new ScriptedResolver(contextA));
        Assert.Equal(5, binding.LatestInitiatedRequestIdentity);
        await dispatcher.WaitPostedAsync(_timeout);
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        Assert.Equal(6, binding.LatestInitiatedRequestIdentity);
        Assert.Equal(2, dispatcher.PostedCount);

        // 执行两个回调：D 已过期 → 回调执行时身份守卫拒绝（状态发布经 Binding 守卫）；
        // E 为当前请求 → 失败正常发布。最终 UI 状态对应最新有效请求。
        dispatcher.Drain();
        Assert.Equal(
            1,
            log.InfoMessages.Count(m => m.Contains("已丢弃（回调执行时身份守卫）", StringComparison.Ordinal)));
        Assert.Contains("场景请求失败", vm.WorkspaceStatusText, StringComparison.Ordinal);
        Assert.Equal(PresentationRequestState.Failed, binding.LastRequestOutcome!.State);
        Assert.Equal(6, binding.LatestInitiatedRequestIdentity);
    }

    /// <summary>
    /// R3 规格 Test B（最新请求成功）：B 成功发布后状态可见；旧请求 A 的晚到成功
    /// （Superseded 完成）被投递前守卫丢弃——成功结果同样不得倒退覆盖。丢弃经
    /// Info 留痕作为确定性完成信号（R3-02），无固定延迟。
    /// </summary>
    [Fact]
    public async Task R3P0_LatestRequestSuccess_StaleLateCompletionCannotOverwrite()
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
                // 请求 1（初始）立即；请求 2（切换 A）门控挂起后晚到成功；请求 3（切换 B）立即成功。
                prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false), (Gate: false, Throw: false));
                return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            });

        var map = new MapSummary { MapId = mapId, DisplayName = "R3-B" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        // A：发起并挂起（身份 2）。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(contextB));
        await prep!.WaitEnteredAsync(1);

        // B：发起并立即成功（身份 3）→ 状态可见（TCS 投递信号，无轮询）。
        await workspace.SwitchContextAsync(branchC, historicalTimeInput: null, new ScriptedResolver(contextC));
        await dispatcher.WaitPostedAsync(_timeout);
        Assert.True(dispatcher.DrainOne());
        var statusOfB = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfB, StringComparison.Ordinal);
        Assert.Contains("已发布", statusOfB, StringComparison.Ordinal);
        Assert.Equal(3, binding.LatestInitiatedRequestIdentity);

        // A 晚到成功（Superseded 完成）→ 投递前身份守卫丢弃。留痕经 TCS 等待器
        // 等待（无轮询）；留痕在协调器 Record 之后同一 continuation 内发生，故其
        // 可见即 Superseded 已计数。
        prep!.Release(1);
        await log.WaitForInfoAsync(
            m => m.Contains("已丢弃（投递前守卫）", StringComparison.Ordinal), _timeout);

        // A 的回调从未入队；B 的状态与结果不被覆盖（晚到成功同样不得倒退）。
        Assert.Equal(0, dispatcher.PostedCount);
        dispatcher.Drain();
        Assert.Equal(statusOfB, vm.WorkspaceStatusText);
        Assert.Equal(3, binding.LastRequestOutcome!.Sequence);
        Assert.Equal(PresentationRequestState.Published, binding.LastRequestOutcome.State);
        Assert.Equal(1, binding.Runtime.Coordinator.Counters.Superseded);
    }

    /// <summary>
    /// R3 规格 Test C（最新请求失败）：最新请求 B 的失败正常发布（不为过滤过期请求
    /// 而错误丢弃当前请求的失败结果）；旧请求 A 的晚到成功不得覆盖 B 的失败状态。
    /// </summary>
    [Fact]
    public async Task R3P0_LatestRequestFailure_IsPublished_StaleLateSuccessCannotOverwrite()
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
        var contextA = MakeContext(mapId, Guid.NewGuid());
        var log = new FakeAppLog();
        GatedPreparation? prep = null;
        var (vm, _, mapQuery, dispatcher) = BuildR1(
            log: log,
            bindingFactory: ws =>
            {
                // 请求 1（初始）立即；请求 2（切换 A）门控挂起后晚到成功；
                // 请求 3（切换 B，非法上下文）在请求创建阶段失败，不消耗准备步。
                prep = new GatedPreparation((Gate: false, Throw: false), (Gate: true, Throw: false));
                return new MapViewportBinding(ws.ViewState, ViewportPresentationRuntime.Create(prep.PrepareAsync));
            });

        var map = new MapSummary { MapId = mapId, DisplayName = "R3-C" };
        var workspace = await OpenAsync(vm, mapQuery, map);
        var binding = vm.GetViewportBinding(workspace)!;

        // A：发起并挂起（身份 2）。
        await workspace.SwitchContextAsync(Guid.NewGuid(), historicalTimeInput: null, new ScriptedResolver(contextA));
        await prep!.WaitEnteredAsync(1);

        // B：非法上下文切换（身份 3 = 最新）→ 请求创建失败归一化为 Failed → 当期最新
        // 请求的失败正常发布（守卫放行）。SwitchContextAsync 的整条链（Invoke →
        // 处理器同步前缀 → RequestSceneAsync 同步失败路径 → Post）为同步执行，
        // 返回时身份已分配、回调已入队——直接断言，无轮询。
        await workspace.SwitchContextAsync(branchB, historicalTimeInput: null, new ScriptedResolver(invalidContext));
        Assert.Equal(3, binding.LatestInitiatedRequestIdentity);
        await dispatcher.WaitPostedAsync(_timeout);
        Assert.True(dispatcher.DrainOne());
        var statusOfB = vm.WorkspaceStatusText;
        Assert.Contains("语义切换", statusOfB, StringComparison.Ordinal);
        Assert.Contains("场景请求失败", statusOfB, StringComparison.Ordinal);
        Assert.True(log.WarnCount >= 1); // 管线异常被 VM 观察记录（Binding 归一化不吞异常）
        Assert.Equal(3, binding.LatestInitiatedRequestIdentity);
        Assert.Equal(PresentationRequestState.Failed, binding.LastRequestOutcome!.State);

        // A 晚到成功（身份 2 已过期）：B 因非法上下文在请求创建阶段失败，从未注册
        // 进协调器——协调器无从知晓身份 3，A 在协调器侧仍是其已知最新请求（成功
        // 正常落定）；过期判定的权威是 Binding 的最新发起身份（R3-01 正确性层）。
        // 投递前守卫丢弃留痕经 TCS 等待器等待（无轮询；判定与留痕间无 await/调度点）。
        prep!.Release(1);
        await log.WaitForInfoAsync(
            m => m.Contains("已丢弃（投递前守卫）", StringComparison.Ordinal), _timeout);

        // B 的失败状态保留：不被晚到成功覆盖（失败不冒充成功、过期不得倒退）。
        Assert.Equal(0, dispatcher.PostedCount);
        dispatcher.Drain();
        Assert.Equal(statusOfB, vm.WorkspaceStatusText);
        Assert.Equal(PresentationRequestState.Failed, binding.LastRequestOutcome!.State);
        Assert.Equal(3, binding.LatestInitiatedRequestIdentity);
    }

    /// <summary>
    /// R3 规格 Test D（关闭状态，Binding 原语级）：Binding 关闭（终态）后，
    /// <c>PublishIfRequestCurrent</c> 拒绝发布——尚未执行的发布回调不会修改任何状态。
    /// VM 级关闭场景由 R1/R2 既有两个关闭测试覆盖（回调执行时关闭守卫丢弃）。
    /// </summary>
    [Fact]
    public async Task R3P0_PublishIfRequestCurrent_ClosedBinding_IsRejected()
    {
        var mapId = Guid.NewGuid();
        var prep = new GatedPreparation((Gate: false, Throw: false));
        var binding = new MapViewportBinding(
            new WorkspaceViewState(), ViewportPresentationRuntime.Create(prep.PrepareAsync));

        var request = await binding.RequestSceneAsync(MakeContext(mapId, Guid.NewGuid()));
        Assert.False(binding.IsClosed);
        Assert.True(binding.PublishIfRequestCurrent(request.RequestIdentity, () => { }));

        await binding.RequestClose();
        Assert.True(binding.IsClosed);

        var executed = false;
        Assert.False(binding.PublishIfRequestCurrent(request.RequestIdentity, () => executed = true));
        Assert.False(executed);
    }

    /// <summary>原语直测（R3-01）：当前有效请求 → 发布执行并返回 true。</summary>
    [Fact]
    public async Task R3P0_PublishIfRequestCurrent_CurrentIdentity_ExecutesAndReturnsTrue()
    {
        var mapId = Guid.NewGuid();
        var prep = new GatedPreparation((Gate: false, Throw: false));
        var binding = new MapViewportBinding(
            new WorkspaceViewState(), ViewportPresentationRuntime.Create(prep.PrepareAsync));
        var request = await binding.RequestSceneAsync(MakeContext(mapId, Guid.NewGuid()));

        var executed = false;
        Assert.True(binding.PublishIfRequestCurrent(request.RequestIdentity, () => executed = true));
        Assert.True(executed);
    }

    /// <summary>原语直测（R3-01）：已被取代的过期请求 → 不执行发布并返回 false。</summary>
    [Fact]
    public async Task R3P0_PublishIfRequestCurrent_SupersededIdentity_IsRejected()
    {
        var mapId = Guid.NewGuid();
        var prep = new GatedPreparation((Gate: false, Throw: false), (Gate: false, Throw: false));
        var binding = new MapViewportBinding(
            new WorkspaceViewState(), ViewportPresentationRuntime.Create(prep.PrepareAsync));
        var first = await binding.RequestSceneAsync(MakeContext(mapId, Guid.NewGuid()));
        var second = await binding.RequestSceneAsync(MakeContext(mapId, Guid.NewGuid()));
        Assert.NotEqual(first.RequestIdentity, second.RequestIdentity);

        var executed = false;
        Assert.False(binding.PublishIfRequestCurrent(first.RequestIdentity, () => executed = true));
        Assert.False(executed);
        Assert.True(binding.PublishIfRequestCurrent(second.RequestIdentity, () => { }));
    }
}
