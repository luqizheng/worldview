using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using WorldView.Application.Sessions;
using WorldView.Desktop.Rendering;
using WorldView.Desktop.Tests.Testing;
using Xunit;

namespace WorldView.Desktop.Tests.Lifecycle;

/// <summary>
/// WP-30B Phase 3A — MapViewportBinding / MapViewportControl 生命周期测试
/// （ADR-07 / ADR-10 四分生命周期 + ADR-08 Q9 关闭检查边界）。
/// 全部以真实调用顺序 / 程序序锚点验证（CloseStageLog、Dispose 入口探针、门控），
/// 绝无 Sleep、轮询或调度运气。
/// </summary>
public class MapViewportBindingTests
{
    private static readonly TimeSpan _gateTimeout = TimeSpan.FromSeconds(10);

    /// <summary>可观测 / 可门控的渲染器替身（记录 Dispose 次数与入口时刻的探针状态）。</summary>
    private sealed class ProbeRenderer : MapSceneRenderer
    {
        private readonly TaskCompletionSource? _disposeEnteredSource;
        private readonly TaskCompletionSource? _releaseSource;

        public int DisposeCount { get; private set; }

        public Task DisposeEntered => _disposeEnteredSource?.Task ?? Task.CompletedTask;

        public void ReleaseDispose() => _releaseSource?.TrySetResult();

        /// <summary>Dispose 故障注入（DEF-01：验证首次关闭失败经共享 Task 原样传播）。</summary>
        public Exception? DisposeFailure { get; set; }

        /// <summary>Dispose 入口时刻的探针（由测试在构造 binding 后装配）。</summary>
        public Func<bool>? BindingClosedAtDisposeEntry { get; set; }

        public Func<bool>? CoordinatorClosedAtDisposeEntry { get; set; }

        public Func<bool>? PublisherDisposedAtDisposeEntry { get; set; }

        public bool BindingClosedObserved { get; private set; }

        public bool CoordinatorClosedObserved { get; private set; }

        public bool PublisherDisposedObserved { get; private set; }

        private readonly TimeSpan _gateTimeout = TimeSpan.FromSeconds(10);

        public ProbeRenderer(bool gate)
        {
            if (gate)
            {
                _disposeEnteredSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _releaseSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public override void Dispose()
        {
            DisposeCount++;
            BindingClosedObserved = BindingClosedAtDisposeEntry?.Invoke() ?? false;
            CoordinatorClosedObserved = CoordinatorClosedAtDisposeEntry?.Invoke() ?? false;
            PublisherDisposedObserved = PublisherDisposedAtDisposeEntry?.Invoke() ?? false;
            _disposeEnteredSource?.TrySetResult();

            if (_releaseSource is { } release
                && !release.Task.Wait(_gateTimeout))
            {
                // 门未放行 = 确定性交错锚点未满足，必须失败而不是挂死测试。
                throw new TimeoutException("renderer dispose gate 未在超时内放行。");
            }

            if (DisposeFailure is { } failure)
            {
                throw failure;
            }

            base.Dispose();
        }

        /// <summary>装配入口探针（binding 构造完成后调用）。</summary>
        public void AttachProbes(MapViewportBinding binding)
        {
            BindingClosedAtDisposeEntry = () => binding.IsClosed;
            CoordinatorClosedAtDisposeEntry = () => binding.Runtime.Coordinator.IsClosed;
            PublisherDisposedAtDisposeEntry = () => binding.Runtime.Publisher.IsDisposed;
        }
    }

    private static (MapViewportBinding Binding, ProbeRenderer Renderer) CreateBinding(bool gate = false)
    {
        var renderer = new ProbeRenderer(gate);
        var binding = new MapViewportBinding(new WorkspaceViewState(), renderer: renderer);
        renderer.AttachProbes(binding);
        return (binding, renderer);
    }

    /// <summary>
    /// 阶段故障注入 Binding（P1-02）：覆写 Coordinator / Publisher 阶段调用缝注入失败
    /// （Renderer 故障经构造注入 ProbeRenderer 即可）。记录各阶段尝试次数，
    /// 用于断言"失败后仍执行后续阶段"与"重复关闭不重复释放"。
    /// </summary>
    private sealed class FaultInjectingBinding : MapViewportBinding
    {
        public Exception? CoordinatorFailure { get; set; }

        public Exception? PublisherFailure { get; set; }

        public int CoordinatorAttempts { get; private set; }

        public int PublisherAttempts { get; private set; }

        public FaultInjectingBinding(WorkspaceViewState viewState, MapSceneRenderer? renderer = null)
            : base(viewState, renderer: renderer)
        {
        }

        internal override async ValueTask CloseCoordinatorStepAsync()
        {
            CoordinatorAttempts++;
            if (CoordinatorFailure is { } failure)
            {
                throw failure;
            }

            await base.CloseCoordinatorStepAsync();
        }

        internal override void DisposePublisherStep()
        {
            PublisherAttempts++;
            if (PublisherFailure is { } failure)
            {
                throw failure;
            }

            base.DisposePublisherStep();
        }
    }

    // ---- 创建 / Attach ----

    [Fact]
    public void CreatedBinding_NotClosed_AndReusable()
    {
        var (binding, renderer) = CreateBinding();

        Assert.False(binding.IsClosed);
        Assert.False(binding.Runtime.Coordinator.IsClosed);
        Assert.False(binding.Runtime.Publisher.IsDisposed);
        Assert.Empty(binding.CloseStageLog);
        _ = renderer;
    }

    [Fact]
    public void Attach_NormalBinding_RegistersControl()
    {
        var (binding, _) = CreateBinding();
        var control = new MapViewportControl();

        control.Bind(binding);
        // 真实接线中 Attach 由控件的 OnAttachedToVisualTree 调用；无头环境显式模拟。
        binding.Attach(control);

        Assert.Same(binding, control.Binding);
        Assert.True(binding.HasAttachedControl);
    }

    [Fact]
    public void Bind_RejectsClosedBinding_WithDiagnosticFailure()
    {
        var (binding, _) = CreateBinding();
        var control = new MapViewportControl();
        binding.RequestClose().GetAwaiter().GetResult();

        var ex = Assert.Throws<InvalidOperationException>(() => control.Bind(binding));
        Assert.Contains("永久关闭", ex.Message);
        Assert.False(binding.HasAttachedControl);
    }

    // ---- 暂时 Detach（生命周期 A）----

    [Fact]
    public void Detach_ReleasesControlReference_ButClosesNothing()
    {
        var (binding, renderer) = CreateBinding();
        var control = new MapViewportControl();
        control.Bind(binding);

        binding.Detach();

        Assert.False(binding.HasAttachedControl);
        Assert.False(binding.IsClosed);
        Assert.False(binding.Runtime.Coordinator.IsClosed);
        Assert.False(binding.Runtime.Publisher.IsDisposed);
        Assert.Equal(0, renderer.DisposeCount);
        Assert.Empty(binding.CloseStageLog);
    }

    [Fact]
    public void Detach_Reattach_ReusesSameBindingInstance()
    {
        var (binding, renderer) = CreateBinding();
        var control = new MapViewportControl();

        control.Bind(binding);
        binding.Detach();
        Assert.False(binding.IsClosed);

        // 重新挂载：复用同一 Binding（不新建 Runtime / Renderer）。
        binding.Attach(control);
        Assert.Same(binding, control.Binding);
        Assert.True(binding.HasAttachedControl);
        Assert.Empty(binding.CloseStageLog);
        Assert.Equal(0, renderer.DisposeCount);
    }

    // ---- 永久关闭（生命周期 B 子序）----

    [Fact]
    public async Task Close_RecordsExactStageOrder_AndProbesAtDisposeEntry()
    {
        var (binding, renderer) = CreateBinding();
        renderer.AttachProbes(binding);

        await binding.RequestClose();

        Assert.True(binding.IsClosed);
        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_disposed" },
            binding.CloseStageLog);
        Assert.Equal(1, renderer.DisposeCount);

        // Renderer.Dispose 入口时刻：binding 已关闭、F-1 屏障已置位、发布器已释放
        // ——真实程序序证据，而非只验终态。
        Assert.True(renderer.BindingClosedObserved);
        Assert.True(renderer.CoordinatorClosedObserved);
        Assert.True(renderer.PublisherDisposedObserved);
    }

    [Fact]
    public async Task Close_IsIdempotent_SecondCallRecordsAlreadyClosed()
    {
        var (binding, renderer) = CreateBinding();

        var first = binding.RequestClose();
        await first;
        var second = binding.RequestClose();
        var third = binding.RequestClose();

        // DEF-01：所有调用者共享同一关闭 Task（含已完成状态）。
        Assert.Same(first, second);
        Assert.Same(first, third);
        await second;
        await third;
        Assert.True(binding.IsClosed);
        Assert.Equal(1, renderer.DisposeCount); // 资源关闭只执行一次
        Assert.True(binding.Runtime.Coordinator.IsClosed);
        Assert.True(binding.Runtime.Publisher.IsDisposed);
        Assert.Equal(2, binding.CloseStageLog.Count(s => s == "already_closed"));
    }

    // ---- DEF-01：进行中的关闭共享同一 Task ----

    [Fact]
    public async Task Close_InProgress_RepeatCallReturnsSameTask_AndWaitsForFirstCompletion()
    {
        var (binding, renderer) = CreateBinding(gate: true);

        // 第一次调用：关闭启动并挂起在 renderer.Dispose 门控内。
        var first = binding.RequestClose();
        await renderer.DisposeEntered.WaitAsync(_gateTimeout);

        // 第二次调用在关闭进行中到达：立即拿到同一 Task（不阻塞、不重复执行），
        // 等待它即等待第一次关闭完成。
        var second = binding.RequestClose();
        Assert.Same(first, second);
        Assert.False(second.IsCompleted); // 首次关闭未完成 → 共享任务仍未完成
        Assert.True(binding.IsClosed);    // 终态已置，但子序仍在进行

        renderer.ReleaseDispose();
        await Task.WhenAll(first, second).WaitAsync(_gateTimeout);

        // 资源关闭只执行一次；两个等待者观察到同一完成结果。
        Assert.Equal(1, renderer.DisposeCount);
        Assert.Equal(1, binding.CloseStageLog.Count(s => s == "is_closed"));
        Assert.Equal(1, binding.CloseStageLog.Count(s => s == "renderer_disposed"));
    }

    [Fact]
    public async Task Close_Failure_RepeatCallStartsRetry_OnlyFailedStageReruns_FinallySucceeds()
    {
        var (binding, renderer) = CreateBinding();
        renderer.DisposeFailure = new InvalidOperationException("renderer 释放失败（测试注入）。");

        // 第一次关闭失败：异常经共享 Task 传播给首个调用者。
        var first = binding.RequestClose();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => first.WaitAsync(_gateTimeout));
        Assert.Equal(1, renderer.DisposeCount);

        // 统一裁决 2.2/2.3：失败后的重复调用启动重试尝试（新 Task），
        // 失败阶段重新执行、已成功阶段不重复释放——失败绝不被伪装为成功。
        var retry = binding.RequestClose();
        Assert.NotSame(first, retry);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(
            () => retry.WaitAsync(_gateTimeout));
        Assert.Contains("renderer 释放失败", ex2.Message, StringComparison.Ordinal);
        Assert.Equal(2, renderer.DisposeCount); // 失败阶段重新执行
        Assert.Contains("close_retry", binding.CloseStageLog);
        Assert.False(binding.IsCloseSucceeded);
        Assert.True(binding.IsClosed);          // 终态语义不变

        // 故障解除后再重试：仅补 renderer 阶段 → 全部阶段成功（IsCloseSucceeded 终态）。
        renderer.DisposeFailure = null;
        await binding.RequestClose().WaitAsync(_gateTimeout);
        Assert.True(binding.IsCloseSucceeded);
        Assert.Equal(3, renderer.DisposeCount);
    }

    [Fact]
    public async Task Close_InProgressFailure_RepeatCallSharesFaultedTask()
    {
        // 失败发生在门控放行后：关闭"进行中"的重复调用者在失败后同样观察到原始异常。
        var (binding, renderer) = CreateBinding(gate: true);
        renderer.DisposeFailure = new InvalidOperationException("renderer 释放失败（测试注入）。");

        var first = binding.RequestClose();
        await renderer.DisposeEntered.WaitAsync(_gateTimeout);

        var second = binding.RequestClose();
        Assert.Same(first, second);

        renderer.ReleaseDispose();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Task.WhenAll(first, second).WaitAsync(_gateTimeout));

        Assert.Contains("renderer 释放失败", ex.Message);
        Assert.Equal(1, renderer.DisposeCount);
    }

    [Fact]
    public async Task ClosedBinding_IsTerminal_CannotBeReattachedOrReused()
    {
        var (binding, renderer) = CreateBinding();
        var control = new MapViewportControl();
        await binding.RequestClose();

        // 终态不可重置：Attach / Bind 一律拒绝。
        Assert.Throws<InvalidOperationException>(() => binding.Attach(control));
        Assert.Throws<InvalidOperationException>(() => control.Bind(binding));

        // 拥有者视角：已关闭 Binding 不再可用（VM 的 GetViewportBinding 返回 null）。
        Assert.False(binding.HasAttachedControl);
        _ = renderer;
    }

    [Fact]
    public async Task Close_DetachesControl_ButDetachAloneNeverCloses()
    {
        var (binding, _) = CreateBinding();
        var control = new MapViewportControl();
        control.Bind(binding);

        await binding.RequestClose();

        Assert.True(binding.IsClosed);
        Assert.False(binding.HasAttachedControl); // 关闭解除登记
    }

    // ---- P1-02：故障注入——单阶段失败仍执行后续释放阶段 ----

    [Fact]
    public async Task Close_CoordinatorFails_PublisherAndRendererStillDisposed_OriginalExceptionPropagates()
    {
        var renderer = new ProbeRenderer(gate: false);
        var binding = new FaultInjectingBinding(new WorkspaceViewState(), renderer: renderer);
        var coordinatorFailure = new InvalidOperationException("coordinator 释放失败（测试注入）。");
        binding.CoordinatorFailure = coordinatorFailure;

        var closeTask = binding.RequestClose();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => closeTask.WaitAsync(_gateTimeout));

        // 单个异常：原始异常实例原样传播，不被包装、不被吞掉。
        Assert.Same(coordinatorFailure, ex);

        // 后续阶段仍被执行：真实 Publisher 已释放、Renderer 已 Dispose（各恰好一次）。
        Assert.True(binding.Runtime.Publisher.IsDisposed);
        Assert.Equal(1, renderer.DisposeCount);
        Assert.Equal(1, binding.PublisherAttempts);

        // 顺序证据：is_closed → coordinator_failed → publisher_disposed → renderer_disposed。
        Assert.Equal(
            new[] { "is_closed", "coordinator_failed", "publisher_disposed", "renderer_disposed" },
            binding.CloseStageLog);

        // 重复 RequestClose（统一裁决 2.2/2.3）：启动重试——coordinator 阶段重新执行
        // （仍未成功），已成功的 publisher / renderer 阶段跳过、不重复释放。
        var retry = binding.RequestClose();
        Assert.NotSame(closeTask, retry);
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => retry.WaitAsync(_gateTimeout));
        Assert.Same(coordinatorFailure, ex2);
        Assert.Equal(2, binding.CoordinatorAttempts);  // 失败阶段重新执行
        Assert.Equal(1, binding.PublisherAttempts);    // 已成功阶段不重复释放
        Assert.Equal(1, renderer.DisposeCount);
        Assert.Contains("close_retry", binding.CloseStageLog);
        Assert.Contains("publisher_skipped", binding.CloseStageLog);
        Assert.Contains("renderer_skipped", binding.CloseStageLog);

        // 故障解除后再重试：coordinator 补完 → IsCloseSucceeded 终态。
        binding.CoordinatorFailure = null;
        await binding.RequestClose().WaitAsync(_gateTimeout);
        Assert.True(binding.IsCloseSucceeded);
        Assert.Equal(3, binding.CoordinatorAttempts);
        Assert.Equal(1, binding.PublisherAttempts);
        Assert.Equal(1, renderer.DisposeCount);
    }

    [Fact]
    public async Task Close_PublisherFails_RendererStillDisposed_OriginalExceptionPropagates()
    {
        var renderer = new ProbeRenderer(gate: false);
        var binding = new FaultInjectingBinding(new WorkspaceViewState(), renderer: renderer);
        var publisherFailure = new InvalidOperationException("publisher 释放失败（测试注入）。");
        binding.PublisherFailure = publisherFailure;

        var closeTask = binding.RequestClose();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => closeTask.WaitAsync(_gateTimeout));

        Assert.Same(publisherFailure, ex);

        // 前序阶段成功（真实 Coordinator 已关闭）、后续阶段仍执行（Renderer 已释放）。
        Assert.True(binding.Runtime.Coordinator.IsClosed);
        Assert.Equal(1, renderer.DisposeCount);

        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_failed", "renderer_disposed" },
            binding.CloseStageLog);

        // 重复 RequestClose（统一裁决 2.2/2.3）：启动重试——publisher 阶段重新执行
        // （仍未成功），已成功的 coordinator / renderer 阶段跳过。
        var retry = binding.RequestClose();
        Assert.NotSame(closeTask, retry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => retry.WaitAsync(_gateTimeout));
        Assert.Equal(1, binding.CoordinatorAttempts); // 已成功阶段不重复释放
        Assert.Equal(2, binding.PublisherAttempts);   // 失败阶段重新执行
        Assert.Equal(1, renderer.DisposeCount);
        Assert.Contains("coordinator_skipped", binding.CloseStageLog);
        Assert.Contains("close_retry", binding.CloseStageLog);

        // 故障解除后再重试：publisher 补完 → IsCloseSucceeded 终态。
        binding.PublisherFailure = null;
        await binding.RequestClose().WaitAsync(_gateTimeout);
        Assert.True(binding.IsCloseSucceeded);
        Assert.Equal(1, binding.CoordinatorAttempts);
        Assert.Equal(3, binding.PublisherAttempts);
        Assert.Equal(1, renderer.DisposeCount);
    }

    [Fact]
    public async Task Close_RendererFails_CoordinatorAndPublisherStillDisposed()
    {
        var (binding, renderer) = CreateBinding();
        renderer.DisposeFailure = new InvalidOperationException("renderer 释放失败（测试注入）。");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => binding.RequestClose().WaitAsync(_gateTimeout));

        // Renderer 是最后一阶段：前序两个真实资源仍必须已释放。
        Assert.True(binding.Runtime.Coordinator.IsClosed);
        Assert.True(binding.Runtime.Publisher.IsDisposed);
        Assert.Equal(1, renderer.DisposeCount);

        Assert.Equal(
            new[] { "is_closed", "coordinator_closed", "publisher_disposed", "renderer_failed" },
            binding.CloseStageLog);
    }

    [Fact]
    public async Task Close_AllStagesFail_AllStagesAttempted_AggregateExceptionInStageOrder()
    {
        var renderer = new ProbeRenderer(gate: false);
        var binding = new FaultInjectingBinding(new WorkspaceViewState(), renderer: renderer);
        var coordinatorFailure = new InvalidOperationException("coordinator 释放失败（测试注入）。");
        var publisherFailure = new InvalidOperationException("publisher 释放失败（测试注入）。");
        var rendererFailure = new InvalidOperationException("renderer 释放失败（测试注入）。");
        binding.CoordinatorFailure = coordinatorFailure;
        binding.PublisherFailure = publisherFailure;
        renderer.DisposeFailure = rendererFailure;

        var closeTask = binding.RequestClose();
        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => closeTask.WaitAsync(_gateTimeout));

        // 多个异常：按阶段顺序聚合，InnerExceptions 保留原始异常实例。
        Assert.Equal(3, aggregate.InnerExceptions.Count);
        Assert.Same(coordinatorFailure, aggregate.InnerExceptions[0]);
        Assert.Same(publisherFailure, aggregate.InnerExceptions[1]);
        Assert.Same(rendererFailure, aggregate.InnerExceptions[2]);

        // 三个阶段都被尝试，且各恰好一次。
        Assert.Equal(1, binding.CoordinatorAttempts);
        Assert.Equal(1, binding.PublisherAttempts);
        Assert.Equal(1, renderer.DisposeCount);

        Assert.Equal(
            new[] { "is_closed", "coordinator_failed", "publisher_failed", "renderer_failed" },
            binding.CloseStageLog);

        // 重复 RequestClose（统一裁决 2.2/2.3）：三个阶段均未成功 → 全部重新执行，
        // 原始异常实例保持（新聚合容器，InnerExceptions 仍为同一批异常）。
        var retry = binding.RequestClose();
        Assert.NotSame(closeTask, retry);
        var aggregate2 = await Assert.ThrowsAsync<AggregateException>(() => retry.WaitAsync(_gateTimeout));
        Assert.Equal(3, aggregate2.InnerExceptions.Count);
        Assert.Same(coordinatorFailure, aggregate2.InnerExceptions[0]);
        Assert.Same(publisherFailure, aggregate2.InnerExceptions[1]);
        Assert.Same(rendererFailure, aggregate2.InnerExceptions[2]);
        Assert.Equal(2, binding.CoordinatorAttempts);
        Assert.Equal(2, binding.PublisherAttempts);
        Assert.Equal(2, renderer.DisposeCount);
        Assert.Contains("close_retry", binding.CloseStageLog);

        // 故障全部解除后再重试：三个阶段补完 → IsCloseSucceeded 终态。
        binding.CoordinatorFailure = null;
        binding.PublisherFailure = null;
        renderer.DisposeFailure = null;
        await binding.RequestClose().WaitAsync(_gateTimeout);
        Assert.True(binding.IsCloseSucceeded);
        Assert.Equal(3, binding.CoordinatorAttempts);
        Assert.Equal(3, binding.PublisherAttempts);
        Assert.Equal(3, renderer.DisposeCount);
    }

    [Fact]
    public void CaptureFrame_CombinesSnapshotViewportScalarsAndGeneration()
    {
        var (binding, _) = CreateBinding();
        binding.OnViewportChanged(320, 240, 1.5);

        var frame = binding.CaptureFrame();

        Assert.Null(frame.Snapshot); // M1 不发起场景请求，Publisher 从未发布
        Assert.Equal(320, frame.Width);
        Assert.Equal(240, frame.Height);
        Assert.Equal(1.5, frame.RenderScaling);
        Assert.Equal(binding.Runtime.ViewportGeneration.Current, frame.ViewportGeneration);
    }

    [Fact]
    public void OnViewportChanged_AdvancesGeneration()
    {
        var (binding, _) = CreateBinding();
        var before = binding.Runtime.ViewportGeneration.Current;

        binding.OnViewportChanged(100, 80, 1.0);

        Assert.True(binding.Runtime.ViewportGeneration.Current > before);
    }
}
