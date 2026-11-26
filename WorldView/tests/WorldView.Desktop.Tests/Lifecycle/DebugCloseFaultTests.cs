#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
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
/// 注入开关是进程级一次性状态：新测试类独占串行集合，避免与并行测试类互相消费
/// 武装状态（不修改任何既有测试的集合归属或断言）。
/// </summary>
[CollectionDefinition("DebugFaultSerial", DisableParallelization = true)]
public sealed class DebugFaultSerialCollection;

/// <summary>
/// WP-30B Phase 3A — Debug 一次性关闭故障注入入口（仅 Debug 编译）测试。
///
/// 覆盖：武装/一次性消费、真实 Binding 阶段故障与阶段级重试、C2 失败不通知窗口
/// （窗口保留重试）、重试成功、多关闭入口共享编排、故障信息进入日志与状态栏。
/// 交错全部由 TaskCompletionSource 门控制造，无 Sleep / 轮询 / 随机 / 时间竞争。
/// 原有生命周期测试全部保留，本文件不替代任何既有断言。
/// </summary>
[Collection("DebugFaultSerial")]
public class DebugCloseFaultTests
{
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    /// <summary>记录异常文本的日志替身：证明故障信息可经日志识别。</summary>
    private sealed class RecordingLog : IAppLog
    {
        public List<string> WarnMessages { get; } = new();
        public List<string> WarnExceptionMessages { get; } = new();

        public void Info(string category, string message) { }

        public void Warn(string category, string message, Exception? exception = null)
        {
            WarnMessages.Add(message);
            if (exception is not null)
            {
                WarnExceptionMessages.Add(exception.ToString());
            }
        }

        public void Error(string category, string message, Exception? exception = null) { }
    }

    /// <summary>计数 Publisher 阶段尝试次数的 Binding（覆写调用缝，仍经真实基类执行注入）。</summary>
    private sealed class TrackingBinding : MapViewportBinding
    {
        public int PublisherAttempts { get; private set; }

        public TrackingBinding(WorkspaceViewState viewState, MapSceneRenderer? renderer = null)
            : base(viewState, renderer: renderer)
        {
        }

        internal override void DisposePublisherStep()
        {
            PublisherAttempts++;
            base.DisposePublisherStep(); // 注入点在真实生产调用缝内，不是测试替身路径
        }
    }

    /// <summary>可门控渲染器：在 renderer.Dispose 阶段制造确定性交错。</summary>
    private sealed class ProbeRenderer : MapSceneRenderer
    {
        private readonly TaskCompletionSource? _entered;
        private readonly TaskCompletionSource? _release;

        public ProbeRenderer(bool gate)
        {
            if (gate)
            {
                _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public int DisposeCount { get; private set; }
        public Task Entered => _entered?.Task ?? Task.CompletedTask;
        public void Release() => _release?.TrySetResult();

        public override void Dispose()
        {
            DisposeCount++;
            _entered?.TrySetResult();
            if (_release is { } release && !release.Task.Wait(GateTimeout))
            {
                throw new TimeoutException("renderer dispose gate 未在超时内放行。");
            }

            base.Dispose();
        }
    }

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

    private static (TestViewModel Vm, FakeMapQueryService Query, ProjectSession Session)
        CreateVm(RecordingLog? log = null)
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
        return (vm, query, session);
    }

    private static async Task OpenOneWorkspaceAsync(TestViewModel vm, FakeMapQueryService query)
    {
        vm.SelectedMap = query.Maps[0];
        await vm.OpenWorkspaceCommand.ExecuteAsync(null);
    }

    // ---- 1. 武装与一次性语义 ----

    [Fact]
    public void ArmOnce_InjectsExactlyOnce_AndDisarms()
    {
        try
        {
            Assert.False(DebugCloseFault.IsArmed);
            DebugCloseFault.ArmOnce();
            DebugCloseFault.ArmOnce(); // 重复武装不累积
            Assert.True(DebugCloseFault.IsArmed);

            var first = DebugCloseFault.Consume(DebugCloseFault.FaultStage);
            Assert.NotNull(first);
            Assert.Contains("[DEBUG-FAULT]", first!.Message);
            Assert.Contains(DebugCloseFault.FaultStage, first.Message);
            Assert.False(DebugCloseFault.IsArmed);

            Assert.Null(DebugCloseFault.Consume(DebugCloseFault.FaultStage)); // 第二次不再注入
            Assert.Null(DebugCloseFault.Consume("other_stage"));              // 非目标阶段不消费
        }
        finally
        {
            DebugCloseFault.Disarm();
        }
    }

    // ---- 2/4/5/6. 真实 Binding 关闭流水线：故障一次、重试只补失败阶段 ----

    [Fact]
    public async Task InjectedPublisherFailure_FirstCloseFails_RetryOnlyRedoesPublisher_AndSucceeds()
    {
        try
        {
            DebugCloseFault.ArmOnce();
            var renderer = new ProbeRenderer(gate: false);
            var binding = new TrackingBinding(new WorkspaceViewState(), renderer: renderer);

            var first = binding.RequestClose();
            var injected = await Assert.ThrowsAsync<DebugCloseInjectionException>(
                () => first.WaitAsync(GateTimeout));

            Assert.Contains("[DEBUG-FAULT]", injected.Message);
            // 前序阶段已成功、后续阶段仍执行（正式关闭顺序与异常安全收尾不变）。
            Assert.Equal(
                new[] { "is_closed", "coordinator_closed", "publisher_failed", "renderer_disposed" },
                binding.CloseStageLog);
            Assert.True(binding.Runtime.Coordinator.IsClosed);
            Assert.False(binding.Runtime.Publisher.IsDisposed); // 故障发生在真正 Dispose 之前
            Assert.Equal(1, binding.PublisherAttempts);
            Assert.Equal(1, renderer.DisposeCount);
            Assert.True(binding.IsClosed);
            Assert.False(binding.IsCloseSucceeded);

            // 第二次关闭：无故障 → 只补 Publisher 阶段，已成功阶段不重复释放。
            var retry = binding.RequestClose();
            Assert.NotSame(first, retry);
            await retry.WaitAsync(GateTimeout);

            Assert.True(binding.IsCloseSucceeded);
            Assert.Equal(2, binding.PublisherAttempts);   // 仅失败阶段重试
            Assert.Equal(1, renderer.DisposeCount);       // 已成功阶段不重复释放
            Assert.True(binding.Runtime.Publisher.IsDisposed);
            Assert.Contains("coordinator_skipped", binding.CloseStageLog);
            Assert.Contains("renderer_skipped", binding.CloseStageLog);
            Assert.Null(DebugCloseFault.Consume(DebugCloseFault.FaultStage)); // 故障确实只注入一次
        }
        finally
        {
            DebugCloseFault.Disarm();
        }
    }

    // ---- 3/4/5. 窗口生命周期契约：首次失败不通知窗口关闭，第二次成功 ----

    [Fact]
    public async Task FirstCloseFailure_NoProjectClosed_WindowStaysOpen_ThenRetryCloses()
    {
        var log = new RecordingLog();
        try
        {
            var (vm, query, session) = CreateVm(log);
            var bindings = new List<MapViewportBinding>();
            vm.TestBindingFactory = ws =>
            {
                var binding = new TrackingBinding(ws.ViewState);
                bindings.Add(binding);
                return binding;
            };
            await OpenOneWorkspaceAsync(vm, query);

            var projectClosedCount = 0;
            vm.ProjectClosed += () => projectClosedCount++;
            DebugCloseFault.ArmOnce();

            // 第一次 X：故障经真实 C2 编排传播；Binding 失败不阻断 Session 关闭，
            // 但步骤 3/4 被跳过 —— ProjectClosed 不触发即"窗口保留"的可自动化证据。
            var failure = await Assert.ThrowsAsync<DebugCloseInjectionException>(
                () => vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout));

            Assert.Equal(0, projectClosedCount);
            Assert.True(vm.IsProjectCloseLifetimeFinished);
            Assert.True(vm.IsProjectCloseLifetimeStarted);  // sticky：失败不撤销关闭不可逆状态
            Assert.Equal(ProjectSessionState.Closed, session.State);
            Assert.Single(vm.Workspaces);                   // 未清理：窗口仍持有可重试工作区
            Assert.Contains("publisher_failed", ((TrackingBinding)bindings[0]).CloseStageLog);

            // 故障信息可经日志识别（C2 的 best-effort Warn 携带原始注入异常）。
            Assert.Contains(log.WarnMessages, m => m.Contains("关闭失败"));
            Assert.Contains(log.WarnExceptionMessages, m => m.Contains("[DEBUG-FAULT]"));

            // 第二次 X：C2 任务字段已复位 → 新编排；故障不再注入 → 正常完成。
            await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);

            Assert.Equal(1, projectClosedCount);
            Assert.Empty(vm.Workspaces);
            Assert.True(bindings[0].IsCloseSucceeded);
            Assert.Equal(2, ((TrackingBinding)bindings[0]).PublisherAttempts);
        }
        finally
        {
            DebugCloseFault.Disarm();
        }
    }

    // ---- 7. 多关闭入口并发：窗口 X 与命令共享同一编排，失败/重试不重复释放 ----

    [Fact]
    public async Task ConcurrentWindowAndCommandEntries_ShareOneFailedOrchestration_ThenOneRetrySucceeds()
    {
        try
        {
            var (vm, query, _) = CreateVm();
            var gate = new ProbeRenderer(gate: true);
            TrackingBinding? tracked = null;
            vm.TestBindingFactory = ws => tracked = new TrackingBinding(ws.ViewState, renderer: gate);
            await OpenOneWorkspaceAsync(vm, query);
            Assert.NotNull(tracked);

            var projectClosedCount = 0;
            vm.ProjectClosed += () => projectClosedCount++;
            DebugCloseFault.ArmOnce();

            // 窗口 X 路径（直接编排）在工作线程启动，门控停在 renderer.Dispose。
            var windowClose = Task.Run(() => vm.CloseProjectLifetimeAsync());
            await gate.Entered.WaitAsync(GateTimeout);

            // 编排进行中：直调重入与命令入口都到达，共享同一 C2 任务。
            var reentry = vm.CloseProjectLifetimeAsync();
            var command = vm.CloseProjectCommand.ExecuteAsync(null);
            Assert.Same(reentry, vm.CloseProjectLifetimeAsync());
            Assert.False(windowClose.IsCompleted);

            gate.Release();

            // 三个等待者观察到同一注入异常；命令路径就地观察并呈现状态文本。
            var injected = await Assert.ThrowsAsync<DebugCloseInjectionException>(
                () => windowClose.WaitAsync(GateTimeout));
            var reentryFailure = await Assert.ThrowsAsync<DebugCloseInjectionException>(
                () => reentry.WaitAsync(GateTimeout));
            Assert.Same(injected, reentryFailure);
            await command.WaitAsync(GateTimeout);

            Assert.Contains("[DEBUG-FAULT]", vm.WorkspaceStatusText);
            Assert.Equal(0, projectClosedCount);       // 失败不通知窗口关闭
            Assert.Equal(1, tracked!.PublisherAttempts);
            Assert.Equal(1, gate.DisposeCount);        // 单一编排，不重复释放
            Assert.Contains("coordinator_closed", tracked.CloseStageLog);

            // 第二次 X：只补 Publisher，整体一次成功。
            await vm.CloseProjectLifetimeAsync().WaitAsync(GateTimeout);
            Assert.Equal(1, projectClosedCount);
            Assert.Equal(2, tracked.PublisherAttempts);
            Assert.Equal(1, gate.DisposeCount);
            Assert.True(tracked.IsCloseSucceeded);
        }
        finally
        {
            DebugCloseFault.Disarm();
        }
    }
}
#endif
