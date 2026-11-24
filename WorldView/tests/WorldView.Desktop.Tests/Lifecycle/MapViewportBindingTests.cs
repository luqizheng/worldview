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
    /// <summary>可观测 / 可门控的渲染器替身（记录 Dispose 次数与入口时刻的探针状态）。</summary>
    private sealed class ProbeRenderer : MapSceneRenderer
    {
        private readonly TaskCompletionSource? _disposeEnteredSource;
        private readonly TaskCompletionSource? _releaseSource;

        public int DisposeCount { get; private set; }

        public Task DisposeEntered => _disposeEnteredSource?.Task ?? Task.CompletedTask;

        public void ReleaseDispose() => _releaseSource?.TrySetResult();

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

        await binding.RequestClose();
        await binding.RequestClose();
        await binding.RequestClose();

        Assert.True(binding.IsClosed);
        Assert.Equal(1, renderer.DisposeCount); // 资源关闭只执行一次
        Assert.True(binding.Runtime.Coordinator.IsClosed);
        Assert.True(binding.Runtime.Publisher.IsDisposed);
        Assert.Equal(2, binding.CloseStageLog.Count(s => s == "already_closed"));
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
