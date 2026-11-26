using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Publication;
using WorldView.Presentation.Results;

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
/// 聚合（InnerExceptions 保持阶段顺序）。进行中的重复 RequestClose 共享同一关闭 Task
/// （DEF-01）；失败后的重试只补未成功阶段（阶段级完成标记，统一裁决 2.3），
/// 已成功阶段绝不重复释放。
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

    // 阶段级完成标记（WP-30B 3A 统一裁决 2.3）：只在对应释放步骤成功后置位。
    // 失败后的重试只重新执行未置位阶段，已成功阶段绝不重复释放。
    private bool _coordinatorClosed;
    private bool _publisherDisposed;
    private bool _rendererDisposed;

    // 关闭尝试 Task。进行中共享同一 Task（DEF-01）；上一轮尝试失败后，下一次
    // RequestClose 启动新尝试（只补未成功阶段）并替换本字段。全部访问经 _closeGate 串行化。
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

    /// <summary>
    /// 关闭子序是否已全部成功完成（Coordinator → Publisher → Renderer 三阶段
    /// 完成标记全部置位）。统一裁决 2.3：C2 编排据此区分"关闭成功"与
    /// "已启动但存在未完成清理工作"——后者可经 RequestClose 重试补完。
    /// </summary>
    public bool IsCloseSucceeded
    {
        get { lock (_closeGate) { return _coordinatorClosed && _publisherDisposed && _rendererDisposed; } }
    }

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
    /// 永久关闭当前视口的呈现资源（B / C1 / C2 共用子序）。
    /// WP-30B 3A 统一裁决（2.1/2.2/2.3）：
    /// - <strong>进行中共享</strong>：关闭尝试执行期间，重复调用返回同一 Task 并等待
    ///   首次操作（DEF-01）——所有调用者等待相同的关闭结果与进度，不并行两套关闭流程；
    /// - <strong>失败可重试</strong>：上一轮尝试失败（faulted）后，下一次调用启动新尝试，
    ///   只重新执行未成功的阶段（阶段级完成标记）；已成功阶段绝不重复释放；
    /// - <strong>成功终态</strong>：全部阶段成功后，后续调用幂等返回同一成功 Task，
    ///   不重复执行任何释放步骤。
    /// 不关闭 Workspace / ProjectSession（那是拥有者 C1 / C2 编排的职责）。
    /// </summary>
    public Task RequestClose()
    {
        lock (_closeGate)
        {
            if (_closeTask is { } existing)
            {
                if (!existing.IsCompleted)
                {
                    // 关闭进行中：共享同一 Task（DEF-01）。
                    _closeStageLog.Add("already_closed");
                    return existing;
                }

                if (existing.IsCompletedSuccessfully)
                {
                    // 已全部成功（终态）：幂等返回，不重复释放。
                    _closeStageLog.Add("already_closed");
                    return existing;
                }

                // 上一轮尝试失败：存在未完成的清理阶段 → 启动重试尝试（新 Task）。
                _closeStageLog.Add("close_retry");
            }
            else
            {
                // 1a. 置终态（volatile 写；此后 Render 入口立即拒绝新帧）。
                _isClosed = true;
                _closeStageLog.Add("is_closed");
                _attachedControl = null;
            }

            // 锁内创建、锁外执行同步段（Task.Yield 延迟启动）：并发调用者不会在本锁上
            // 被门控阻塞，而是立即拿到同一 Task 等待首次结果。
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

    /// <summary>
    /// 步骤 1c 调用缝：直通 <see cref="SceneSnapshotPublisher.Dispose"/>。
    /// 仅 Debug 构建在此处检查一次性验收故障（ADR-10 阶段级完成标记使重试只补
    /// 本阶段：前序 Coordinator 已成功 → 重试时 coordinator_skipped，不重复释放）；
    /// Release 编译后本方法只有下面这一条直调语句，无状态、无分支。
    /// </summary>
    internal virtual void DisposePublisherStep()
    {
#if DEBUG
        if (DebugCloseFault.Consume(DebugCloseFault.FaultStage) is { } injected)
        {
            throw injected;
        }
#endif
        _runtime.Publisher.Dispose();
    }

    /// <summary>
    /// 步骤 1b–1d（ADR-10 §7.1）：屏障 → 发布器 → 渲染器，顺序不可重排。
    /// P1-02：任一阶段失败仍继续执行后续释放阶段；所有异常收集后统一聚合，
    /// 在全部释放步骤结束后一次性抛出（经 CloseDeferredStartAsync 使本轮关闭 Task
    /// 进入 faulted 状态，由所有等待者观察同一结果）。
    /// WP-30B 3A 统一裁决 2.3：已成功阶段由完成标记跳过（重试不重复释放），
    /// 失败阶段在下一轮重试中重新执行。
    /// </summary>
    private async Task ClosePipelineAsync()
    {
        List<Exception>? failures = null;

        // 1b. Coordinator.DisposeAsync：先 CloseAsync——停新请求、协作取消未完成请求，
        //     并在发布器锁内置位 F-1 关闭屏障（WP-30A 线性化点）；再释放协调器取消源。
        //     阶段日志如实记录：成功记 coordinator_closed，失败记 coordinator_failed；
        //     上一轮已成功的阶段记 coordinator_skipped（不重复执行）。
        if (Volatile.Read(ref _coordinatorClosed))
        {
            RecordStage("coordinator_skipped");
        }
        else
        {
            try
            {
                await CloseCoordinatorStepAsync().ConfigureAwait(false);
                Volatile.Write(ref _coordinatorClosed, true);
                RecordStage("coordinator_closed");
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
                RecordStage("coordinator_failed");
            }
        }

        // 1c. Publisher.Dispose：此后一切发布与候选版本分配被拒绝；
        //     已发布快照的逻辑数据在被引用期间保持有效（PRA-01 §8）。
        if (Volatile.Read(ref _publisherDisposed))
        {
            RecordStage("publisher_skipped");
        }
        else
        {
            try
            {
                DisposePublisherStep();
                Volatile.Write(ref _publisherDisposed, true);
                RecordStage("publisher_disposed");
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
                RecordStage("publisher_failed");
            }
        }

        // 1d. Renderer.Dispose（M1 幂等空操作）。
        if (Volatile.Read(ref _rendererDisposed))
        {
            RecordStage("renderer_skipped");
        }
        else
        {
            try
            {
                _renderer.Dispose();
                Volatile.Write(ref _rendererDisposed, true);
                RecordStage("renderer_disposed");
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
                RecordStage("renderer_failed");
            }
        }

        // 异常安全收尾的传播点：本轮全部释放步骤已结束，才向等待者传播。
        // 单个异常原样保留（同一实例）；多个异常按阶段顺序聚合。
        // 本轮失败不影响下一轮重试（RequestClose 启动新尝试补完剩余阶段）。
        if (failures is not null)
        {
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
        }
    }

    /// <summary>最近一次场景请求的最终结果记录（WP-RA-02-A：失败可观察的诊断锚点）。未发起过请求时为 null。</summary>
    internal PresentationRequestOutcome? LastRequestOutcome => Volatile.Read(ref _lastOutcome);

    private PresentationRequestOutcome? _lastOutcome;

    /// <summary>最近一次场景请求发起时刻的上下文指纹十六进制（诊断锚点；未发起过为 null）。</summary>
    internal string? LastRequestedFingerprintHex { get; private set; }

    /// <summary>
    /// 以已校验工作区上下文发起一次真实场景请求（WP-RA-02-A 接线：桌面生产路径）：
    /// 经 <see cref="PresentationContextFactory.FromValidatedWorkspace"/> 构建不可变
    /// PresentationContext（捕获当前视口代），委托本 Binding 私有的
    /// <see cref="ViewportPresentationRuntime.RequestSceneAsync"/>——完整走
    /// Context → Request → Prepare → Publish 管线；渲染侧经 <see cref="CaptureFrame"/>
    /// 无锁捕获发布结果。请求结束记录 <see cref="LastRequestOutcome"/> 供宿主与测试观察。
    /// 取消令牌只供调用方在发起前撤回；进入管线后的生命周期正确性由协调器的
    /// 关闭屏障与 latest-request-wins 语义保证（WP-30A/WP-30B）。
    /// </summary>
    internal async Task<PresentationRequestOutcome> RequestSceneAsync(
        ValidatedWorkspaceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 请求创建（激活/取代/发布器激活）经协调器内部锁串行化；此处只保证
        // LastRequestOutcome 记录与请求发起的对应关系（诊断一致性）。
        var presentationContext = PresentationContextFactory.FromValidatedWorkspace(
            context, _runtime.ViewportGeneration.Current);
        LastRequestedFingerprintHex = presentationContext.Fingerprint.ToHex();
        var outcome = await _runtime.RequestSceneAsync(presentationContext, cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _lastOutcome, outcome);
        return outcome;
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
