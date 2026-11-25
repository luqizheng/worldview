using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;

namespace WorldView.Application.Sessions;

/// <summary>
/// 地图工作区会话：一个 Project Session 内的 Map/Branch/历史时间工作上下文（AF-01 §6）。
/// 不持有独立数据库连接、不复制权威地图模型、不拥有第二历史引擎；
/// 拥有独立的视图/选择/工具等瞬态状态与自己的取消源。
/// </summary>
public sealed class MapWorkspaceSession : IAsyncDisposable
{
    private readonly object _gate = new();
    private bool _closed;
    private readonly CancellationTokenSource _derivedWorkCts = new();

    public MapWorkspaceSession(ProjectSession projectSession, ValidatedWorkspaceContext context)
    {
        WorkspaceId = Guid.NewGuid();
        ProjectSession = projectSession;
        Context = context;
        ViewState = new WorkspaceViewState();
    }

    /// <summary>进程内临时工作区 ID。</summary>
    public Guid WorkspaceId { get; }

    /// <summary>所属项目会话。</summary>
    public ProjectSession ProjectSession { get; }

    /// <summary>经 Core Domain 校验的 Map/Branch/历史时间上下文（不透明占位，正式类型随领域实现落地）。</summary>
    public ValidatedWorkspaceContext Context { get; private set; }

    /// <summary>独立的瞬态视图状态（与其他工作区互不共享）。</summary>
    public WorkspaceViewState ViewState { get; }

    /// <summary>工作区派生工作的取消令牌（上下文切换或关闭时取消）。</summary>
    public CancellationToken DerivedWorkCancellationToken => _derivedWorkCts.Token;

    public bool IsClosed
    {
        get { lock (_gate) { return _closed; } }
    }

    /// <summary>上下文切换：经 Core Domain 重新解析并替换上下文，取消旧的派生工作。</summary>
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
            if (_closed)
            {
                // 晚到达：等待期间工作区已关闭 → 丢弃结果。
                return;
            }

            Context = resolution.Context;
            _derivedWorkCts.Cancel();
        }
    }

    /// <summary>关闭工作区：取消派生工作；不影响兄弟工作区与项目会话。幂等。</summary>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return Task.CompletedTask;
            }

            _closed = true;
        }

        _derivedWorkCts.Cancel();
        return Task.CompletedTask;
    }

    /// <summary>测试 seam（WorldView.Desktop.Tests 经 IvT 访问）：<see cref="DisposeAsync"/>
    /// 实际执行计数。默认 0，仅递增、无任何行为影响；生命周期测试据此断言释放恰好一次。</summary>
    internal int DisposeCount { get; private set; }

    public ValueTask DisposeAsync()
    {
        _derivedWorkCts.Dispose();
        DisposeCount++;
        return ValueTask.CompletedTask;
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
