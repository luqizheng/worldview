using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;

namespace WorldView.Application.Sessions;

/// <summary>Project Session 生命周期状态（AF-01 §5）。</summary>
public enum ProjectSessionState
{
    Opening,
    Validating,
    Migrating,
    Ready,
    Failed,
    Cancelled,
    Closing,
    Closed,
}

/// <summary>
/// 一个成功打开的 Project Package 的进程内会话（AF-01 §5）。
/// 拥有包路径、清单、数据库访问、Asset Store 适配器、子工作区与生命周期；
/// 不是持久化领域实体，Session ID 是进程内临时标识，绝不替代 Project ID。
///
/// 失败/取消的会话绝不会暴露为 Ready；关闭按 子工作区 → Asset Store → 数据库 的
/// 逆序释放资源；关闭永不删除包。
/// </summary>
public sealed class ProjectSession : IAsyncDisposable
{
    private readonly object _stateGate = new();
    private readonly List<MapWorkspaceSession> _workspaces = new();
    private ProjectSessionState _state = ProjectSessionState.Opening;

    public ProjectSession(
        Guid sessionId,
        string canonicalPackagePath,
        PackageManifest manifest,
        IPackageDatabase database,
        IAssetStore assetStore,
        Guid? projectId)
    {
        SessionId = sessionId;
        CanonicalPackagePath = canonicalPackagePath;
        Manifest = manifest;
        Database = database;
        AssetStore = assetStore;
        ProjectId = projectId;
    }

    /// <summary>进程内临时会话 ID。</summary>
    public Guid SessionId { get; }

    /// <summary>规范化的包根路径。</summary>
    public string CanonicalPackagePath { get; }

    /// <summary>已通过校验的 manifest。</summary>
    public PackageManifest Manifest { get; }

    /// <summary>包数据库访问（会话拥有并在关闭时释放）。</summary>
    public IPackageDatabase Database { get; }

    /// <summary>包 Asset Store 适配器（会话拥有并在关闭时释放）。</summary>
    public IAssetStore AssetStore { get; }

    /// <summary>项目身份（Core Domain 读取服务给出；未注册身份服务时为 null）。</summary>
    public Guid? ProjectId { get; private set; }

    /// <summary>项目显示名（来自 Core Domain；未注册身份服务时为 null）。</summary>
    public string? ProjectDisplayName { get; private set; }

    /// <summary>当前生命周期状态（原子读取）。</summary>
    public ProjectSessionState State
    {
        get { lock (_stateGate) { return _state; } }
    }

    /// <summary>会话是否处于 Ready（仅 Ready 暴露给 UI/用例使用）。</summary>
    public bool IsReady => State == ProjectSessionState.Ready;

    /// <summary>
    /// 会话完全关闭（资源已释放）后触发；注册表据此解除该包的占用，
    /// 使同一包可以被再次打开。
    /// </summary>
    internal event EventHandler? SessionClosed;

    /// <summary>打开的工作区快照。</summary>
    public IReadOnlyList<MapWorkspaceSession> Workspaces
    {
        get { lock (_stateGate) { return _workspaces.ToArray(); } }
    }

    /// <summary>设置项目身份（建立流程在身份读取成功后调用；仅供建立流程使用）。</summary>
    internal void AttachProjectIdentity(ProjectIdentity identity)
    {
        ProjectId = identity.ProjectId;
        ProjectDisplayName = identity.DisplayName;
    }

    internal void TransitionTo(ProjectSessionState newState)
    {
        lock (_stateGate)
        {
            _state = newState;
        }
    }

    /// <summary>
    /// 完成建立：进入 Ready。仅允许从 Opening/Validating/Migrating 转入；
    /// 在取消或失败后调用会被拒绝（晚完成的建立不能复活会话）。
    /// </summary>
    public bool TryCompleteOpen()
    {
        lock (_stateGate)
        {
            if (_state is ProjectSessionState.Opening or ProjectSessionState.Validating or ProjectSessionState.Migrating)
            {
                _state = ProjectSessionState.Ready;
                return true;
            }

            return false;
        }
    }

    /// <summary>标记失败/取消（从建立流程调用）。</summary>
    internal void MarkAborted(ProjectSessionState abortedState)
    {
        lock (_stateGate)
        {
            if (_state is ProjectSessionState.Opening or ProjectSessionState.Validating or ProjectSessionState.Migrating)
            {
                _state = abortedState;
            }
        }
    }

    /// <summary>
    /// 打开一个地图工作区。仅 Ready 状态允许；Branch/时间经 Core Domain 解析端口校验。
    /// 解析等待期间会话被关闭时，晚到达的结果会被丢弃（不复活已关闭会话）。
    /// </summary>
    public async Task<MapWorkspaceSession> OpenWorkspaceAsync(
        Guid mapId,
        Guid? branchId,
        string? historicalTimeInput,
        IMapQueryService mapQueryService,
        IWorkspaceContextResolver contextResolver,
        CancellationToken cancellationToken = default)
    {
        EnsureReadyForNewWork();

        var map = await mapQueryService.GetMapAsync(
            RequireProjectId(), mapId, cancellationToken).ConfigureAwait(false);
        if (map is null)
        {
            throw new InvalidOperationException("地图不存在或不可访问（Core Domain 查询无此 Map）。");
        }

        var resolution = await contextResolver.ResolveAsync(
            RequireProjectId(), mapId, branchId, historicalTimeInput, cancellationToken).ConfigureAwait(false);
        if (!resolution.Success || resolution.Context is null)
        {
            throw new InvalidOperationException(resolution.Diagnostic ?? "Branch/历史时间解析未通过（Core Domain）。");
        }

        lock (_stateGate)
        {
            // 晚到达保护：等待期间会话进入 Closing/Closed/Failed → 拒绝挂载。
            if (_state != ProjectSessionState.Ready)
            {
                throw new InvalidOperationException($"会话已不在 Ready 状态（当前 {_state}），拒绝打开工作区。");
            }

            var workspace = new MapWorkspaceSession(this, resolution.Context);
            _workspaces.Add(workspace);
            return workspace;
        }
    }

    /// <summary>关闭单个工作区（兄弟工作区与项目会话不受影响）。</summary>
    public async Task<bool> CloseWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        MapWorkspaceSession? workspace;
        lock (_stateGate)
        {
            workspace = _workspaces.Find(w => w.WorkspaceId == workspaceId);
            if (workspace is null)
            {
                return false;
            }
        }

        await workspace.CloseAsync(cancellationToken).ConfigureAwait(false);
        lock (_stateGate)
        {
            _workspaces.Remove(workspace);
        }

        return true;
    }

    /// <summary>
    /// 关闭会话：先关闭全部子工作区，再按逆序释放 Asset Store 与数据库资源。
    /// 关闭后停止接受新工作；重复关闭为幂等空操作。永不删除包。
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateGate)
        {
            if (_state is ProjectSessionState.Closed or ProjectSessionState.Closing)
            {
                return;
            }

            if (_state == ProjectSessionState.Ready)
            {
                _state = ProjectSessionState.Closing;
            }
        }

        // 1. 先关子工作区（AF-01 §8.2），逐个移除。
        while (true)
        {
            MapWorkspaceSession? workspace;
            lock (_stateGate)
            {
                if (_workspaces.Count == 0)
                {
                    break;
                }

                workspace = _workspaces[^1];
                _workspaces.RemoveAt(_workspaces.Count - 1);
            }

            await workspace.CloseAsync(cancellationToken).ConfigureAwait(false);
            // WP-30B Phase 3A（P2 缺口修复）：关闭后释放派生工作取消源。
            // CTS.Dispose 幂等；先 Close（取消派生工作）后 Dispose 的次序不可交换
            // （ADR-10 §3C 核验第 6 项）。
            await workspace.DisposeAsync().ConfigureAwait(false);
        }

        // 2. 逆序释放资源：Asset Store → 数据库。
        await AssetStore.DisposeAsync().ConfigureAwait(false);
        await Database.DisposeAsync().ConfigureAwait(false);

        Action? handlers;
        lock (_stateGate)
        {
            _state = ProjectSessionState.Closed;
            handlers = () => SessionClosed?.Invoke(this, EventArgs.Empty);
        }

        handlers?.Invoke();
    }

    /// <summary>IAsyncDisposable 等价于 CloseAsync（失败状态会话同样释放已占用资源）。</summary>
    public ValueTask DisposeAsync()
    {
        if (State is ProjectSessionState.Closed or ProjectSessionState.Closing)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(CloseAsync(CancellationToken.None));
    }

    private void EnsureReadyForNewWork()
    {
        lock (_stateGate)
        {
            if (_state != ProjectSessionState.Ready)
            {
                throw new InvalidOperationException($"会话不在 Ready 状态（当前 {_state}），拒绝新工作。");
            }
        }
    }

    private Guid RequireProjectId()
    {
        if (ProjectId is not { } projectId)
        {
            throw new InvalidOperationException("项目身份缺失：会话未通过 Core Domain 身份读取建立。");
        }

        return projectId;
    }
}
