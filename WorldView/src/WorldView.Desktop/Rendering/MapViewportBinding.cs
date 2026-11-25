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
///
/// P1-02 异常安全收尾：关闭子序（1b→1c→1d）既定顺序不可重排；任一阶段失败后仍继续
/// 执行其余阶段（释放失败不阻断后续释放），全部异常就地收集，待所有释放步骤结束后
/// 一次性向等待者传播——单个失败原样保留原始异常，多个失败以 AggregateException
/// 聚合（InnerExceptions 保持阶段顺序）。重复 RequestClose 共享同一关闭 Task（DEF-01），
/// 不重复执行任何释放步骤。
/// </summary>
internal class MapViewportBinding
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

    // DEF-01：唯一关闭 Task。第一次 RequestClose 创建并缓存；后续调用（无论进行中
    // 还是已完成）都返回同一 Task——所有调用者等待相同的关闭结果，第一次关闭的
    // 失败也通过同一 faulted Task 原样传播，不会被伪装成成功。
    private Task? _closeTask;

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
    /// 永久关闭当前视口的呈现资源（B / C1 / C2 共用子序）。幂等且<strong>共享关闭结果</strong>：
    /// 第一次调用创建唯一关闭 Task，后续调用返回同一 Task 并等待首次操作完成
    /// （DEF-01）——不仅"已启动"，而是共享进行中的进度与最终结果；首次关闭失败时，
    /// 后续调用者观察到同一异常。不关闭 Workspace / ProjectSession（那是拥有者
    /// C1 / C2 编排的职责）。
    /// </summary>
    public Task RequestClose()
    {
        lock (_closeGate)
        {
            if (_closeTask is not null)
            {
                // 关闭已启动（进行中或已完成）：返回同一 Task，等待者共享结果。
                // 重复调用只记录 already_closed 诊断，不重复执行任何资源关闭。
                _closeStageLog.Add("already_closed");
                return _closeTask;
            }

            // 1a. 置终态（volatile 写；此后 Render 入口立即拒绝新帧）。
            _isClosed = true;
            _closeStageLog.Add("is_closed");
            _attachedControl = null;

            // 创建并缓存唯一关闭 Task。CloseDeferredStartAsync 先 Task.Yield 把
            // 流水线的同步段（Coordinator.CloseAsync → Publisher.Dispose → Renderer.Dispose）
            // 调度出当前锁范围：并发调用者不会在 _closeGate 上被门控阻塞的
            // Renderer.Dispose 卡住，而是立即拿到同一 Task 等待首次结果。
            var task = CloseDeferredStartAsync();
            _closeTask = task;
            return task;
        }
    }

    /// <summary>延迟启动包装（DEF-01）：见 <see cref="RequestClose"/> 内注释——锁内创建、锁外执行同步段。</summary>
    private async Task CloseDeferredStartAsync()
    {
        await Task.Yield();
        await ClosePipelineAsync();
    }

    // ---- 关闭子序阶段调用缝（P1-02 故障注入点） ----
    // Coordinator / Publisher 为 Presentation 层的 sealed 具体类且 Dispose 成员非 virtual，
    // 无法经构造注入替身；Renderer 已可经构造注入 ProbeRenderer 覆写。因此为前两个阶段
    // 提供 internal virtual 调用缝（模式同 ProjectWorkspaceViewModel.CreateViewportBinding，
    // 测试程序集经 InternalsVisibleTo 覆写）；生产实现一律直通真实资源，不含任何分支。

    /// <summary>步骤 1b 调用缝：直通 <see cref="PresentationRequestCoordinator.DisposeAsync"/>。</summary>
    internal virtual ValueTask CloseCoordinatorStepAsync()
        => _runtime.Coordinator.DisposeAsync();

    /// <summary>步骤 1c 调用缝：直通 <see cref="SceneSnapshotPublisher.Dispose"/>。</summary>
    internal virtual void DisposePublisherStep()
        => _runtime.Publisher.Dispose();

    /// <summary>
    /// 步骤 1b–1d（ADR-10 §7.1）：屏障 → 发布器 → 渲染器，顺序不可重排。
    /// P1-02：任一阶段失败仍继续执行后续释放阶段；所有异常收集后统一聚合，
    /// 在全部释放步骤结束后一次性抛出（经 CloseDeferredStartAsync 使共享关闭 Task
    /// 进入 faulted 状态，由所有等待者观察同一结果）。
    /// </summary>
    private async Task ClosePipelineAsync()
    {
        List<Exception>? failures = null;

        // 1b. Coordinator.DisposeAsync：先 CloseAsync——停新请求、协作取消未完成请求，
        //     并在发布器锁内置位 F-1 关闭屏障（WP-30A 线性化点）；再释放协调器取消源。
        //     阶段日志如实记录：成功记 coordinator_closed，失败记 coordinator_failed。
        try
        {
            await CloseCoordinatorStepAsync().ConfigureAwait(false);
            RecordStage("coordinator_closed");
        }
        catch (Exception ex)
        {
            (failures ??= new List<Exception>()).Add(ex);
            RecordStage("coordinator_failed");
        }

        // 1c. Publisher.Dispose：此后一切发布与候选版本分配被拒绝；
        //     已发布快照的逻辑数据在被引用期间保持有效（PRA-01 §8）。
        try
        {
            DisposePublisherStep();
            RecordStage("publisher_disposed");
        }
        catch (Exception ex)
        {
            (failures ??= new List<Exception>()).Add(ex);
            RecordStage("publisher_failed");
        }

        // 1d. Renderer.Dispose（M1 幂等空操作）。
        try
        {
            _renderer.Dispose();
            RecordStage("renderer_disposed");
        }
        catch (Exception ex)
        {
            (failures ??= new List<Exception>()).Add(ex);
            RecordStage("renderer_failed");
        }

        // 异常安全收尾的传播点：全部释放步骤已结束，才向等待者传播。
        // 单个异常原样保留（同一实例）；多个异常按阶段顺序聚合。
        if (failures is not null)
        {
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
        }
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
