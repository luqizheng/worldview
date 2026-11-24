using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Sessions;
using WorldView.Presentation.Publication;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 视口生命周期协调的单一执行者（WP-30B ADR-07 / ADR-10，Desktop 内部具体类）：
/// 一个工作区视口私有的呈现管线所有权——持有 <see cref="ViewportPresentationRuntime"/>
/// （Publisher + Coordinator + 视口代计数）与无状态 <see cref="MapSceneRenderer"/>，
/// 提供 <see cref="CaptureFrame"/>（唯一捕获点，ADR-11）、<see cref="OnViewportChanged"/>
/// 与 <see cref="RequestClose"/>（B / C1 / C2 共用的关闭子序）。
///
/// 不实现接口、不注册 DI、不做全局单例；控件只持有本类一个引用（ADR-07 约束 7）。
/// 引用方向单向：控件 → Binding；Binding 不持有控件的对象图（Attach 仅登记引用，
/// 不创建控件），因此关闭编排期间即使可视树已拆也能安全执行（ADR-10 §7.2）。
///
/// 关闭子序（ADR-10 §7.1 步骤 1a–1d，WP-30A F-1 屏障保持）：
/// 置 volatile IsClosed → Coordinator.DisposeAsync（内含 CloseAsync：停新请求、
/// 协作取消，并在发布器锁内置位 F-1 关闭屏障——线性化点）→ Publisher.Dispose
/// （拒绝一切后续发布）→ Renderer.Dispose（M1 幂等空操作）。
/// 关闭顺序记录在 <see cref="CloseStageLog"/>（诊断 + 测试锚点）。
///
/// 暂时 Detach 不走本类任何关闭路径；重新 Attach 复用同一 Binding（ADR-10 生命周期 A）。
/// IsClosed 是终态，不可重置；已关闭的 Binding 拒绝重新 Attach（ADR-07 约束 10）。
/// </summary>
internal sealed class MapViewportBinding
{
    /// <summary>进程内诊断标识（日志与测试锚点用；非持久化身份）。</summary>
    internal Guid BindingId { get; } = Guid.NewGuid();

    private readonly ViewportPresentationRuntime _runtime;
    private readonly MapSceneRenderer _renderer;
    private readonly WorkspaceViewState _viewState;
    private readonly object _closeGate = new();
    private readonly List<string> _closeStageLog = new();

    // volatile：Render 回调入口的关闭检查必须无锁（ADR-08 Q9）。
    private volatile bool _isClosed;

    // 视口标量缓存：UI 线程单写者（控件 SizeChanged / ScalingChanged / Attach 时写入），
    // CaptureFrame 在 UI 线程读取——无 await 间隔的同步值拷贝（ADR-11 问题 8）。
    private double _width;
    private double _height;
    private double _renderScaling = 1.0;

    private MapViewportControl? _attachedControl;

    public MapViewportBinding(
        WorkspaceViewState viewState,
        ViewportPresentationRuntime? runtime = null,
        MapSceneRenderer? renderer = null)
    {
        _viewState = viewState ?? throw new ArgumentNullException(nameof(viewState));
        _runtime = runtime ?? ViewportPresentationRuntime.Create();
        _renderer = renderer ?? new MapSceneRenderer();
    }

    /// <summary>Binding 是否已永久关闭（终态，不可重置）。</summary>
    public bool IsClosed => _isClosed;

    /// <summary>当前是否有控件 Attach（暂时 Detach 会清空；永久关闭也会清空）。</summary>
    internal bool HasAttachedControl => Volatile.Read(ref _attachedControl) is not null;

    /// <summary>视口呈现运行时（诊断 / 测试锚点：关闭顺序断言用）。</summary>
    internal ViewportPresentationRuntime Runtime => _runtime;

    /// <summary>无状态渲染器（控件构造 draw operation 时引用；不转移所有权）。</summary>
    internal MapSceneRenderer Renderer => _renderer;

    /// <summary>关闭阶段日志（程序序锚点：诊断与生命周期测试的真实顺序证据）。</summary>
    internal IReadOnlyList<string> CloseStageLog
    {
        get { lock (_closeGate) { return _closeStageLog.ToArray(); } }
    }

    /// <summary>
    /// 永久关闭当前视口的呈现资源（B / C1 / C2 共用子序）。幂等：重复调用只记录
    /// already_closed，不重复执行任何资源关闭。不关闭 Workspace / ProjectSession
    /// （那是拥有者 C1 / C2 编排的职责）。
    /// </summary>
    public Task RequestClose()
    {
        lock (_closeGate)
        {
            if (_isClosed)
            {
                _closeStageLog.Add("already_closed");
                return Task.CompletedTask;
            }

            // 1a. 置终态（volatile 写；此后 Render 入口立即拒绝新帧）。
            _isClosed = true;
            _closeStageLog.Add("is_closed");
            _attachedControl = null;
        }

        return ClosePipelineAsync();
    }

    /// <summary>步骤 1b–1d（ADR-10 §7.1）：屏障 → 发布器 → 渲染器，顺序不可重排。</summary>
    private async Task ClosePipelineAsync()
    {
        // 1b. Coordinator.DisposeAsync：先 CloseAsync——停新请求、协作取消未完成请求，
        //     并在发布器锁内置位 F-1 关闭屏障（WP-30A 线性化点）；再释放协调器取消源。
        await _runtime.Coordinator.DisposeAsync().ConfigureAwait(false);
        RecordStage("coordinator_closed");

        // 1c. Publisher.Dispose：此后一切发布与候选版本分配被拒绝；
        //     已发布快照的逻辑数据在被引用期间保持有效（PRA-01 §8）。
        _runtime.Publisher.Dispose();
        RecordStage("publisher_disposed");

        // 1d. Renderer.Dispose（M1 幂等空操作）。
        _renderer.Dispose();
        RecordStage("renderer_disposed");
    }

    /// <summary>
    /// 控件挂载到视觉树时登记引用。已关闭的 Binding 拒绝 Attach（终态：
    /// 需新建 Binding + Runtime，不得"重新打开"，ADR-07 约束 10）。
    /// </summary>
    internal void Attach(MapViewportControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (_isClosed)
        {
            throw new InvalidOperationException(
                $"Binding {BindingId:N} 已永久关闭，拒绝重新 Attach（终态，需新建 Binding）。");
        }

        Volatile.Write(ref _attachedControl, control);
    }

    /// <summary>
    /// 控件脱离视觉树（暂时 Detach）：只解除引用，不置 IsClosed、不关闭 Runtime、
    /// 不释放任何资源；重新挂载时复用同一 Binding（ADR-10 生命周期 A）。
    /// </summary>
    internal void Detach()
    {
        Volatile.Write(ref _attachedControl, null);
    }

    /// <summary>
    /// 视口相关呈现输入变化（逻辑尺寸 / DPI）：缓存标量并推进视口代
    /// （ADR-11 问题 10；代变化 → 同语义刷新语义，由 Presentation 指纹机制消费）。
    /// UI 线程调用。
    /// </summary>
    internal void OnViewportChanged(double width, double height, double renderScaling)
    {
        _width = width;
        _height = height;
        _renderScaling = renderScaling;
        _runtime.ViewportGeneration.Advance();
    }

    /// <summary>
    /// 唯一捕获点（ADR-11）：UI 线程一次同步值拷贝——快照引用（一次原子读取）
    /// + 缓存的视口标量 + ViewState 相机标量 + 当前视口代。不保存"当前帧"状态，
    /// 不复制场景内容。返回后控件只把帧输入交给 draw operation。
    /// </summary>
    internal ViewportFrameInput CaptureFrame()
    {
        return new ViewportFrameInput(
            Snapshot: _runtime.Publisher.Current,
            Width: _width,
            Height: _height,
            RenderScaling: _renderScaling,
            CameraX: _viewState.CameraX,
            CameraY: _viewState.CameraY,
            Zoom: _viewState.Zoom,
            ViewportGeneration: _runtime.ViewportGeneration.Current);
    }

    private void RecordStage(string stage)
    {
        lock (_closeGate)
        {
            _closeStageLog.Add(stage);
        }
    }
}
