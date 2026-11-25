using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;

namespace WorldView.Application.Sessions;

/// <summary>
/// MapWorkspaceSession 生命周期状态（WP-UI-04A `lifecycle-state-machine.md` §1）。
/// 命名对齐既有 <see cref="ProjectSessionState"/> 的地图级状态机。
/// </summary>
public enum MapWorkspaceState
{
    /// <summary>对象已构造，上下文已校验，编辑器宿主未加载（OpenWorkspaceAsync 内 new）。</summary>
    Created,
    /// <summary>
    /// 编辑器宿主/地图工作资源加载中（为未来 Editor 预留；当前实现无异步加载，
    /// 构造后立即经 <c>MarkReady</c> 进入 Ready，本状态不进入——不过早实现）。
    /// </summary>
    Opening,
    /// <summary>可正常使用；允许 SwitchContext、允许关闭。</summary>
    Ready,
    /// <summary>关闭执行中：派生工作取消、会话资源释放中。重复关闭请求幂等短路。</summary>
    Closing,
    /// <summary>关闭/释放中发生异常：资源可能部分释放；保留各阶段完成标记供重试。</summary>
    CloseFailed,
    /// <summary>全部资源释放成功；终态，不得复用、不得重新打开。</summary>
    Closed,
}

/// <summary>
/// 地图工作区会话：一个 Project Session 内的 Map/Branch/历史时间工作上下文（AF-01 §6）。
/// 不持有独立数据库连接、不复制权威地图模型、不拥有第二历史引擎；
/// 拥有独立的视图/选择/工具等瞬态状态与自己的取消源。
///
/// 生命周期（WP-UI-04B）：状态机为实际执行逻辑——CloseAsync 驱动固定释放顺序
/// （① 派生工作取消 → ② 编辑器资源释放（未来）→ ③ 会话资源释放），
/// 每步成功才置位完成标记；任一步失败进入 CloseFailed 并保留进度，
/// 重试（CloseAsync / DisposeAsync）跳过已成功完成的步骤。Close 与 Dispose
/// 是独立记录的阶段：允许"关闭成功、释放失败"，重试不重复执行已完成的关闭。
/// </summary>
public sealed class MapWorkspaceSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _derivedWorkCts = new();
    private MapWorkspaceState _state = MapWorkspaceState.Created;
    private bool _closeCompleted;
    private bool _disposeCompleted;
    private string? _closeFailureDiagnostic;

    public MapWorkspaceSession(ProjectSession projectSession, ValidatedWorkspaceContext context, string mapName)
    {
        // 显示名必须真实可用：空名称明确拒绝，绝不静默产生误导性显示名称（WP-UI-04B B2）。
        ArgumentException.ThrowIfNullOrWhiteSpace(mapName);
        WorkspaceId = Guid.NewGuid();
        ProjectSession = projectSession;
        Context = context;
        MapName = mapName;
        ViewState = new WorkspaceViewState();
    }

    /// <summary>进程内临时工作区 ID。</summary>
    public Guid WorkspaceId { get; }

    /// <summary>所属项目会话。</summary>
    public ProjectSession ProjectSession { get; }

    /// <summary>经 Core Domain 校验的 Map/Branch/历史时间上下文（不透明占位，正式类型随领域实现落地）。</summary>
    public ValidatedWorkspaceContext Context { get; private set; }

    /// <summary>地图显示名（创建时来自真实地图数据 map.DisplayName；不从 MapId 推导）。</summary>
    public string MapName { get; }

    /// <summary>独立的瞬态视图状态（与其他工作区互不共享）。</summary>
    public WorkspaceViewState ViewState { get; }

    /// <summary>工作区派生工作的取消令牌（上下文切换或关闭时取消）。</summary>
    public CancellationToken DerivedWorkCancellationToken => _derivedWorkCts.Token;

    /// <summary>当前生命周期状态（原子读取；状态转换只经 CloseAsync / DisposeAsync / MarkReady 明确入口）。</summary>
    public MapWorkspaceState LifecycleState
    {
        get { lock (_gate) { return _state; } }
    }

    /// <summary>最近一次关闭/释放失败的诊断（异常类型与消息）；重试成功后清空。</summary>
    public string? CloseFailureDiagnostic
    {
        get { lock (_gate) { return _closeFailureDiagnostic; } }
    }

    /// <summary>关闭流程是否已发起（Closing / CloseFailed / Closed；对齐替换前的 _closed 布尔语义）。</summary>
    public bool IsClosed
    {
        get { lock (_gate) { return _state is MapWorkspaceState.Closing or MapWorkspaceState.CloseFailed or MapWorkspaceState.Closed; } }
    }

    /// <summary>会话资源（派生工作取消源）是否已成功释放（会话关闭时统一释放）。</summary>
    public bool IsDisposed
    {
        get { lock (_gate) { return _disposeCompleted; } }
    }

    /// <summary>
    /// 测试注入点：会话资源释放步骤完成后调用（真实释放已执行），可抛异常模拟
    /// "释放步骤失败"。对齐 FakeAssetStore.FailureHook 的注入模式；生产代码不得设置。
    /// </summary>
    internal Action? DisposeFailureHookForTests { get; set; }

    /// <summary>
    /// 完成挂载：Created → Ready（当前实现无异步编辑器资源加载，构造即就绪，OQ-7）。
    /// 仅 ProjectSession.OpenWorkspaceAsync 在挂载后调用。
    /// </summary>
    internal void MarkReady()
    {
        lock (_gate)
        {
            if (_state != MapWorkspaceState.Created)
            {
                throw new InvalidOperationException($"工作区已不在 Created 状态（当前 {_state}），拒绝标记就绪。");
            }

            _state = MapWorkspaceState.Ready;
        }
    }

    /// <summary>
    /// 上下文切换：经 Core Domain 重新解析并替换上下文，取消旧的派生工作。
    /// 仅 Ready 允许生效；解析等待期间工作区进入关闭流程（Closing/CloseFailed/Closed）时，
    /// 晚到达的结果被丢弃（不复活工作区，WP-UI-04B B5 纳入状态机）。
    /// </summary>
    public async Task SwitchContextAsync(
        Guid? branchId,
        string? historicalTimeInput,
        IWorkspaceContextResolver contextResolver,
        CancellationToken cancellationToken = default)
    {
        var resolution = await contextResolver.ResolveAsync(
            ProjectSession.ProjectId
                ?? throw new InvalidOperationException("项目身份缺失，无法切换上下文。"),
            Context.MapId,
            branchId,
            historicalTimeInput,
            cancellationToken).ConfigureAwait(false);

        if (!resolution.Success || resolution.Context is null)
        {
            throw new InvalidOperationException(resolution.Diagnostic ?? "Branch/历史时间解析未通过（Core Domain）。");
        }

        lock (_gate)
        {
            if (_state != MapWorkspaceState.Ready)
            {
                // 晚到达：等待期间工作区已进入关闭流程或已关闭 → 丢弃结果。
                return;
            }

            Context = resolution.Context;
            _derivedWorkCts.Cancel();
        }
    }

    /// <summary>
    /// 关闭工作区：取消派生工作并释放会话资源；不影响兄弟工作区与项目会话。
    /// Closed 幂等成功；Closing 幂等短路（已有一次关闭在执行）；CloseFailed 为重试入口。
    /// Created 可直接关闭（跳过 Opening 语义）。失败进入 CloseFailed 并保留进度，可重试。
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Closed：幂等成功（终态，不重复释放）；Closing：已有一次关闭在执行
            // （单机程序无并发关闭入口，对齐 ProjectSession.CloseAsync 的既定裁定）。
            // CloseFailed：唯一出口是重试——经此回到 Closing，从失败点继续。
            if (_state is MapWorkspaceState.Closed or MapWorkspaceState.Closing)
            {
                return;
            }

            _state = MapWorkspaceState.Closing;
        }

        await ExecuteReleaseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 释放续段（对齐 ProjectSession.DisposeAsync 的续段模式）：Closed 幂等返回；
    /// CloseFailed 继续释放剩余资源（跳过已完成步骤）；Closing 由 CloseAsync 流程驱动、
    /// 直接返回；Created/Ready 拒绝——释放责任唯一归属关闭流程，不得绕过 CloseAsync 直接释放。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            switch (_state)
            {
                case MapWorkspaceState.Closed:
                case MapWorkspaceState.Closing:
                    return;
                case MapWorkspaceState.Created:
                case MapWorkspaceState.Ready:
                    throw new InvalidOperationException(
                        $"工作区尚未关闭（当前 {_state}），拒绝直接释放：必须先经 CloseAsync 关闭。");
                case MapWorkspaceState.CloseFailed:
                    _state = MapWorkspaceState.Closing;
                    break;
            }
        }

        await ExecuteReleaseAsync().ConfigureAwait(false);
    }

    /// <summary>固定释放顺序：① 派生工作取消 → ② 编辑器资源释放（未来）→ ③ 会话资源释放。</summary>
    private async Task ExecuteReleaseAsync()
    {
        try
        {
            if (!_closeCompleted)
            {
                _derivedWorkCts.Cancel();
                lock (_gate)
                {
                    _closeCompleted = true;
                }
            }

            if (!_disposeCompleted)
            {
                _derivedWorkCts.Dispose();
                DisposeFailureHookForTests?.Invoke();
                lock (_gate)
                {
                    _disposeCompleted = true;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _state = MapWorkspaceState.CloseFailed;
                _closeFailureDiagnostic = $"{ex.GetType().Name}: {ex.Message}";
            }

            throw;
        }

        lock (_gate)
        {
            _state = MapWorkspaceState.Closed;
            _closeFailureDiagnostic = null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>
/// 工作区瞬态视图状态（选择、活动工具、相机）。每个工作区独立持有；
/// 属应用/编辑器所有，不是正式世界数据（AF-01 §8.3）。
/// </summary>
public sealed class WorkspaceViewState
{
    /// <summary>相机 X（占位单位）。</summary>
    public double CameraX { get; set; }

    /// <summary>相机 Y（占位单位）。</summary>
    public double CameraY { get; set; }

    /// <summary>缩放（占位）。</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>活动工具名（占位）。</summary>
    public string ActiveTool { get; set; } = string.Empty;

    /// <summary>选中项 ID（占位）。</summary>
    public HashSet<Guid> SelectedItemIds { get; } = new();
}
