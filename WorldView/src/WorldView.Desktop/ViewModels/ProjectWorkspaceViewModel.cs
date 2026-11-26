using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.Rendering;

namespace WorldView.Desktop.ViewModels;

/// <summary>
/// 项目工作区 ViewModel：显示项目身份/地图列表并打开地图工作区会话。
/// 地图数据只经 Core Domain 查询端口获取；端口未实现时如实显示依赖门槛，
/// 绝不以占位数据冒充真实地图（AF-02 §4.6）。
/// 地图创建经会话绑定的 CreateMapUseCase（正式 Core Domain 路径，WP-20a）。
///
/// 视口生命周期（WP-30B ADR-10）：
/// - 每个工作区一个 <see cref="MapViewportBinding"/>（工作区打开时创建，创建者唯一）；
/// - C1（<see cref="CloseWorkspaceCommand"/>）：先关目标 Binding → 关工作区 → 移除集合 → Dispose；
/// - C2（<see cref="CloseProjectLifetimeAsync"/>）：唯一业务编排入口——先关全部存活
///   Binding → 关会话 → 清集合 → ProjectClosed 一次；进行中重复调用共享同一编排任务
///   （幂等契约）；失败后可再次调用进入重试（重试只补未完成阶段，已成功释放的
///   资源不重复释放），但关闭的不可逆性不随重试撤销（启动即 sticky）。
///
/// P1-01 关闭期间不可逆契约：C2 启动（共享任务字段的 CAS 成功）即线性化点，此后
/// 项目进入不可逆关闭状态——新的 OpenWorkspace 一律拒绝（<see
/// cref="IsProjectCloseLifetimeStarted"/>）；C2 启动前已开始、尚未完成的 OpenWorkspace
/// 在注册前复查该状态，已启动则就地关闭该工作区（不登记、不展示），防止泄漏。
/// Binding 登记 / 移除与关闭状态检查共用 <see cref="_lifecycleGate"/>（锁内无 await），
/// C2 步骤 1 以排空循环关闭全部存活 Binding，清集合前断言"无未关闭 Binding"
/// （不变量破坏即失败，绝不静默清掉未关闭资源）。
/// </summary>
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly IMapQueryService? _mapQuery;
    private readonly IWorkspaceContextResolver? _contextResolver;
    private readonly IAppLog? _log;

    /// <summary>存活视口 Binding（workspaceId → binding）。仅用于枚举的私有小集合：
    /// 不是全局注册表、不注册 DI、不拥有释放权（释放归 binding 自身的幂等关闭，ADR-10 §8B 第 25 条）。
    /// 全部增删与"C2 是否已启动"的联合判定经 <see cref="_lifecycleGate"/> 串行化。</summary>
    private readonly Dictionary<Guid, MapViewportBinding> _viewportBindings = new();

    /// <summary>Binding 登记 / 移除与 C2 启动状态联合判定的串行化门（锁内不含任何 await）。</summary>
    private readonly object _lifecycleGate = new();

    /// <summary>C2 当前编排任务（非 null 即"当前有一次编排已启动且未失败"）。
    /// 失败后重置为 null（重试入口）；成功后保留（终态，重复调用返回同一任务）。
    /// 线性化点语义见 <see cref="_closeLifetimeStarted"/>。</summary>
    private Task? _projectCloseLifetimeTask;

    /// <summary>C2 是否曾经启动（sticky，一旦置位永不复位）。独立于任务字段：
    /// 失败重试会重置任务字段，但关闭的不可逆性（禁止新建 Workspace）不随之撤销。</summary>
    private bool _closeLifetimeStarted;

    /// <summary>最近一次 C2 编排是否已结束（成功或失败均算结束；按次复位）。</summary>
    private volatile bool _projectCloseLifetimeFinished;

    public ProjectWorkspaceViewModel(
        ProjectSession session,
        IMapQueryService? mapQuery,
        IWorkspaceContextResolver? contextResolver,
        CreateMapUseCase createMap,
        IAppLog? log = null)
    {
        Session = session;
        _mapQuery = mapQuery;
        _contextResolver = contextResolver;
        CreateMap = createMap;
        _log = log;
    }

    /// <summary>项目会话关闭事件（窗口层据此返回启动面）。C2 编排全程只触发一次。</summary>
    public event Action? ProjectClosed;

    public ProjectSession Session { get; }

    /// <summary>地图创建用例（会话绑定；窗口层创建对话框经此走正式创建路径）。</summary>
    public CreateMapUseCase CreateMap { get; }

    /// <summary>项目显示名（Core Domain 身份读取给出）。</summary>
    public string ProjectDisplayName => Session.ProjectDisplayName ?? $"（未命名项目 {Session.ProjectId}）";

    /// <summary>项目 ID（Core Domain 权威身份）。</summary>
    public string ProjectId => Session.ProjectId?.ToString() ?? "—";

    /// <summary>会话 ID（进程内临时标识，绝非 Project ID）。</summary>
    public string SessionId => Session.SessionId.ToString();

    /// <summary>地图摘要列表（Core Domain 读投影）。</summary>
    public ObservableCollection<MapSummary> Maps { get; } = new();

    /// <summary>已打开的工作区。</summary>
    public ObservableCollection<MapWorkspaceSession> Workspaces { get; } = new();

    /// <summary>最近一次 C2 编排是否已结束（成功或失败）。按次复位：重试启动新编排时置回 false。</summary>
    public bool IsProjectCloseLifetimeFinished => _projectCloseLifetimeFinished;

    /// <summary>
    /// C2 是否已启动（P1-01：启动即不可逆关闭状态）。线性化点为
    /// <see cref="CloseProjectLifetimeAsync"/> 中 CAS 成功时刻——该时刻之后新的
    /// OpenWorkspace 必须被拒绝，在途打开必须在注册前就地回收。
    /// sticky 语义（WP-30B 3A 统一裁决 2.4）：编排失败进入重试不会复位本标记——
    /// "关闭可重试"不等于"关闭可撤销"，已启动关闭的项目永不回到可新建工作区状态。
    /// 启动前取消（Token 已取消）不置位：未启动任何关闭动作。
    /// </summary>
    public bool IsProjectCloseLifetimeStarted => Volatile.Read(ref _closeLifetimeStarted);

    /// <summary>地图列表状态说明。</summary>
    [ObservableProperty]
    private string _mapsStatusText = "尚未加载。";

    /// <summary>工作区操作状态。</summary>
    [ObservableProperty]
    private string _workspaceStatusText = string.Empty;

    [ObservableProperty]
    private MapSummary? _selectedMap;

    /// <summary>经 Core Domain 查询服务加载地图列表。</summary>
    public async Task LoadMapsAsync()
    {
        if (_mapQuery is null || Session.ProjectId is not { } projectId)
        {
            MapsStatusText = "依赖门槛：Core Domain 地图查询服务尚未实现，无法列出地图（不以占位数据冒充真实列表）。";
            return;
        }

        try
        {
            Maps.Clear();
            var maps = await _mapQuery.ListMapsAsync(projectId).ConfigureAwait(true);
            foreach (var map in maps)
            {
                Maps.Add(map);
            }

            MapsStatusText = $"共 {Maps.Count} 个地图。";
        }
        catch (Exception ex)
        {
            _log?.Warn("ProjectWorkspace", "地图列表加载失败。", ex);
            MapsStatusText = "地图列表加载失败（详见日志）。";
        }
    }

    /// <summary>
    /// 为选定地图打开独立工作区会话（同时创建该工作区唯一的视口 Binding）。
    /// P1-01：C2 已启动后一律拒绝（关闭不可逆）；C2 启动前已开始、尚未完成的打开，
    /// 在注册前复查关闭状态——已启动则就地关闭该次打开的工作区，不登记、不展示，
    /// 防止"会话已收尾、工作区仍存活"的泄漏。
    /// </summary>
    [RelayCommand]
    private async Task OpenWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (SelectedMap is null)
        {
            WorkspaceStatusText = "请先选择一个地图。";
            return;
        }

        // C2 已启动：项目处于不可逆关闭状态，禁止新的打开（启动前拒绝，无副作用）。
        if (IsProjectCloseLifetimeStarted)
        {
            WorkspaceStatusText = "项目正在关闭，无法打开新的工作区。";
            return;
        }

        try
        {
            var workspace = await Session.OpenWorkspaceAsync(
                SelectedMap.MapId,
                branchId: null,
                historicalTimeInput: null,
                mapQueryService: _mapQuery ?? throw new InvalidOperationException(
                    "Core Domain 地图查询服务尚未实现，无法打开工作区。"),
                contextResolver: _contextResolver ?? throw new InvalidOperationException(
                    "Core Domain Branch/历史时间解析服务尚未实现，无法打开工作区。"),
                cancellationToken).ConfigureAwait(true);

            // 注册前复查（P1-01 第 3 条）：打开进行期间 C2 可能已经启动。判定与登记
            // 在同一锁内完成，保证与 C2 的"启动判定 + 排空 + 清集合"互斥交错。
            bool registered;
            lock (_lifecycleGate)
            {
                registered = !IsProjectCloseLifetimeStarted;
                if (registered)
                {
                    Workspaces.Add(workspace);
                    _viewportBindings.Add(workspace.WorkspaceId, CreateViewportBinding(workspace));
                }
            }

            if (registered)
            {
                WorkspaceStatusText = $"工作区 {workspace.WorkspaceId:N}[{workspace.Context.MapId}] 已打开。";
            }
            else
            {
                await DiscardOrphanWorkspaceAsync(workspace).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _log?.Warn("ProjectWorkspace", "打开工作区失败。", ex);
            WorkspaceStatusText = $"打开工作区失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 就地回收"C2 启动后才完成打开"的孤儿工作区（P1-01 第 3 条）：该工作区已在
    /// Application 侧创建（会话仍 Ready 时挂载成功），但未进入任何 ViewModel 集合，
    /// 也不会被 C2 排空循环看到——必须在此处关闭并释放，否则既泄漏工作区资源，
    /// 又会以"已打开"的假象出现在 Application 的会话状态里。
    /// 会话整体可能正在 / 已经关闭，故每步均为 best-effort：失败记诊断后继续，
    /// 保证 DisposeAsync 一定被尝试（日志异常不参与控制流，同 DEF-03 纪律）。
    /// </summary>
    private async Task DiscardOrphanWorkspaceAsync(MapWorkspaceSession workspace)
    {
        WorkspaceStatusText = "项目正在关闭：本次打开的工作区已被拦截并就地关闭。";

        // P2-01：本方法内的一切日志必须走 best-effort——日志实现自身抛异常时
        // 绝不允许提前退出，否则 CloseWorkspaceAsync / DisposeAsync 都不会执行，
        // 孤儿工作区清理被日志故障阻断（违反 DEF-03 纪律：日志不参与生命周期
        // 收尾的控制流）。
        WarnBestEffort($"C2 启动后完成的工作区打开 {workspace.WorkspaceId:N} 已被拦截，就地关闭。");

        try
        {
            // 会话侧关闭（None：正在关闭的项目不接受中途取消，避免半关闭状态）。
            await Session.CloseWorkspaceAsync(workspace.WorkspaceId, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WarnBestEffort("拦截工作区的会话侧关闭失败（会话可能已整体关闭）。", ex);
        }

        try
        {
            await workspace.DisposeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WarnBestEffort("拦截工作区的资源释放失败。", ex);
        }
    }

    /// <summary>关闭选定工作区（C1：先关 Binding → 关工作区 → 移除集合 → Dispose；
    /// 不影响兄弟工作区与项目会话，ADR-10 §7.1）。</summary>
    [RelayCommand]
    private async Task CloseWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var workspace = SelectedWorkspace;

        // 步骤 1：先关闭该工作区对应的视口 Binding（presentation 先于 Application，ADR-10 §7.1）。
        MapViewportBinding? binding;
        lock (_lifecycleGate)
        {
            _viewportBindings.TryGetValue(workspace.WorkspaceId, out binding);
        }

        if (binding is not null)
        {
            await binding.RequestClose().ConfigureAwait(true);
        }

        // 步骤 2–3：Application 侧关闭 + 本地集合移除（次序不可交换）。
        await Session.CloseWorkspaceAsync(workspace.WorkspaceId, cancellationToken).ConfigureAwait(true);
        Workspaces.Remove(workspace);

        // 步骤 4：释放派生工作资源；同步移除 Binding 登记。
        await workspace.DisposeAsync().ConfigureAwait(true);
        lock (_lifecycleGate)
        {
            _viewportBindings.Remove(workspace.WorkspaceId);
        }

        WorkspaceStatusText = $"工作区 {workspace.WorkspaceId:N} 已关闭。";
    }

    [ObservableProperty]
    private MapWorkspaceSession? _selectedWorkspace;

    /// <summary>取工作区对应的存活 Binding（窗口层宿主视口控件用；无或已关闭则 null）。</summary>
    internal MapViewportBinding? GetViewportBinding(MapWorkspaceSession workspace)
    {
        lock (_lifecycleGate)
        {
            return _viewportBindings.TryGetValue(workspace.WorkspaceId, out var binding) && !binding.IsClosed
                ? binding
                : null;
        }
    }

    /// <summary>视口 Binding 工厂（创建者唯一：工作区打开时创建，每工作区一个，M1 1:1）。</summary>
    internal virtual MapViewportBinding CreateViewportBinding(MapWorkspaceSession workspace)
        => new(workspace.ViewState);

    /// <summary>
    /// 关闭项目会话命令：只委托唯一编排方法（ADR-10 §7.2；不得保留旧的
    /// "先 Session.CloseAsync 再 ProjectClosed"顺序）。失败呈现诊断并保留重试能力
    /// （WP-30B 3A 统一裁决 2.2：失败不永久锁死生命周期对象）。
    /// </summary>
    [RelayCommand]
    private async Task CloseProjectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CloseProjectLifetimeAsync(cancellationToken).ConfigureAwait(true);
            WorkspaceStatusText = "项目会话已关闭。";
        }
        catch (Exception ex)
        {
            // 异常已在编排内记录；此处保证命令路径也观察到异常（不留未观察异常），
            // 并如实呈现失败。失败后编排任务已复位，用户可再次触发重试。
            _log?.Error("ProjectWorkspace", "项目关闭失败。", ex);
            WorkspaceStatusText = $"项目关闭失败：{ex.Message}（可再次关闭重试；详见日志）。";
        }
    }

    /// <summary>
    /// C2 唯一业务编排入口（ADR-10 §7.2 + WP-30B 3A 统一裁决）：关闭该会话下全部存活
    /// Binding → 关闭 ProjectSession → 清理视图集合 → ProjectClosed 一次。
    ///
    /// 幂等契约（行为级）：进行中重复调用（含"关闭项目"命令与 Window.Closing 的并发
    /// 触发）共享并等待同一编排任务，不并行、不跳过、不重复释放。
    /// 重试契约（统一裁决 2.2/2.3）：编排失败后任务字段复位为 null——下一次调用启动
    /// 新一轮编排，只补未完成的阶段（Binding 失败阶段与 Session 失败阶段均有各自的
    /// 阶段级完成标记，已成功释放的资源不会重复释放）；成功为终态，任务字段保留。
    /// P1-01：CAS 成功即"不可逆关闭状态"的启动点（<see cref="_closeLifetimeStarted"/>
    /// sticky 置位）——此后新的 OpenWorkspace 被拒绝、在途打开被就地回收，重试不复位。
    /// 实现约定：先把"已启动"任务写入共享字段，编排体才开始执行——即使编排体在当前
    /// 调用栈内同步完成（ProjectClosed → Window.Close → Closing 重入链），重入也只会
    /// 看到已启动并等待同一任务，不会第二次执行。
    /// </summary>
    public Task CloseProjectLifetimeAsync(CancellationToken cancellationToken = default)
    {
        // 取消令牌只用于启动前的拒绝（ADR-10 幂等契约 6：启动后跑到结束，不中途放弃）。
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var existing = Volatile.Read(ref _projectCloseLifetimeTask);
        if (existing is not null)
        {
            return existing;
        }

        // RunContinuationsAsynchronously：等待者（窗口 Closing 处理器）不得在编排体
        // 执行栈内同步恢复，避免重入链上的任何同步等待自旋。
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var raced = Interlocked.CompareExchange(ref _projectCloseLifetimeTask, completion.Task, null);
        if (raced is not null)
        {
            return raced;
        }

        // sticky 启动标记：CAS 成功即置位、永不复位（统一裁决 2.4 关闭不可逆）。
        Volatile.Write(ref _closeLifetimeStarted, true);
        _projectCloseLifetimeFinished = false;

        // 编排体的返回 Task 永不 fault（全部异常路由进 completion，由等待者观察），
        // 因此丢弃安全：不留未观察的后台任务。
        _ = ExecuteProjectCloseLifetimeAsync(completion);
        return completion.Task;
    }

    /// <summary>
    /// C2 编排体（ADR-10 §7.2 步骤 1–4，按依赖方向不可重排；WP-30B 3A 统一裁决 2.3）。
    /// 步骤 1 为单遍排空（P1-01 第 4 条）：对本轮启动时所有"关闭尚未成功"的 Binding
    /// 各尝试一次；单个失败记诊断、收集异常后继续其余 Binding（阶段隔离）——该 Binding
    /// 留待下一次编排重试（失败阶段可重新执行，不在此轮内死循环）。
    /// 步骤 2 的会话关闭不因 Binding 失败而跳过（后续阶段不因前一阶段异常被永久跳过）；
    /// 会话关闭失败时跳过步骤 3/4（集合状态与会话真实状态一致性依赖会话关闭成功），
    /// 留待重试。
    /// 异常传播（统一裁决 2.3/2.5）：单异常原样保留，多异常按阶段顺序聚合为
    /// AggregateException；原始异常信息不丢失。
    /// 日志纪律（DEF-03）：本方法内的所有日志都是 best-effort——日志实现自身的
    /// 异常绝不参与生命周期收尾的控制流，原始生命周期异常优先传递给等待者。
    /// </summary>
    private async Task ExecuteProjectCloseLifetimeAsync(TaskCompletionSource completion)
    {
        List<Exception>? failures = null;
        try
        {
            // 步骤 1：单遍排空本轮启动时所有"关闭尚未成功"的 Binding（B 子序；
            // 失败收集后继续其余 Binding，失败者留待重试——ADR-10 §7.2 阶段隔离）。
            MapViewportBinding[] pending;
            lock (_lifecycleGate)
            {
                pending = _viewportBindings.Values.Where(b => !b.IsCloseSucceeded).ToArray();
            }

            foreach (var binding in pending)
            {
                try
                {
                    await binding.RequestClose().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    // best-effort 日志：即使 Warn 失败也必须继续关闭其余 Binding。
                    WarnBestEffort($"视口 Binding {binding.BindingId:N} 关闭失败，继续关闭其余视口。", ex);
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            // 步骤 2：关闭 ProjectSession（以 None 跑到结束——中途取消会留下
            // "Binding 已关、会话未关"的半关闭状态）。Binding 失败不阻断本步骤
            // （统一裁决 2.3：后续阶段不因前一阶段异常被永久跳过）。
            try
            {
                await Session.CloseAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                WarnBestEffort("项目会话关闭失败，留待重试。", ex);
                (failures ??= new List<Exception>()).Add(ex);

                // 会话未关闭：跳过步骤 3/4（跳过不等于永久跳过——重试会重新到达）。
                throw BuildCloseFailure(failures);
            }

            // 步骤 2.5：会话已关闭但步骤 1 存在 Binding 失败——不得清空集合
            // （未成功关闭的呈现资源绝不被静默丢弃），以聚合异常失败本轮，
            // 失败 Binding 留待重试（届时 Session.CloseAsync 幂等返回，代价为零）。
            if (failures is not null)
            {
                throw BuildCloseFailure(failures);
            }

            // 步骤 3：清理视图集合（Session.CloseAsync 只清 Application 的 _workspaces，
            // 不会清 ViewModel 的 ObservableCollection）。
            // P1-01 第 5 条：清空 Binding 集合前断言"无未成功关闭的 Binding"——到达
            // 本步骤时步骤 1 已排空且全部成功，仍存在未关闭 Binding 属实现缺陷
            // （排空快照遗漏），宁可让 C2 失败，也绝不静默清掉未关闭的呈现资源。
            lock (_lifecycleGate)
            {
                var unclosed = _viewportBindings.Values.Where(b => !b.IsCloseSucceeded).ToList();
                if (unclosed.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"C2 收尾不变量被破坏：清空 Binding 集合时仍有 {unclosed.Count} 个未成功关闭的 Binding。");
                }

                _viewportBindings.Clear();
            }

            Workspaces.Clear();

            // 步骤 4：通知窗口（ProjectClosed → Window 允许最终关闭）。仅在全部阶段
            // 成功时触发（异常路径在此前已抛出）；因此 C2 全程 ProjectClosed 至多一次。
            // P2-02：逐订阅者隔离异常——订阅者失败既不阻断其余订阅者，也不把已经
            // 成功完成的资源关闭重新判定为失败（否则重试会重复触发通知，
            // 违反"至多通知一次"契约；详见 <see cref="NotifyProjectClosedSafely"/>）。
            NotifyProjectClosedSafely();
        }
        catch (Exception ex)
        {
            // DEF-03：先保存原始生命周期异常，再做 best-effort 日志——日志失败
            // 既不覆盖原始异常，也不会让下方 Finished / SetException 收尾被跳过。
            WarnBestEffort("项目生命周期关闭失败。", ex);
            FailCloseLifetime(completion, ex);
            return;
        }

        // DEF-02：先置 Finished（volatile 写）再完成任务。完成顺序不可交换——
        // 从 completion.Task 恢复的等待者在观察到任务完成时，必然已经能读到
        // Finished == true。成功与失败同序。
        _projectCloseLifetimeFinished = true;
        completion.SetResult();
    }

    /// <summary>
    /// C2 失败收尾（统一裁决 2.2）：置 Finished → 复位任务字段（重试入口）→
    /// 向等待者传播原始异常。任务字段复位必须晚于 Finished 置位且只复位自己的任务
    /// （比较后交换）：重试启动的新任务绝不会被旧编排的失败收尾误杀。
    /// </summary>
    private void FailCloseLifetime(TaskCompletionSource completion, Exception failure)
    {
        _projectCloseLifetimeFinished = true;
        Interlocked.CompareExchange(ref _projectCloseLifetimeTask, null, completion.Task);
        completion.SetException(failure);
    }

    /// <summary>按统一裁决 2.3/2.5 聚合本轮收集的异常：单个原样返回，多个按阶段顺序聚合。</summary>
    private static Exception BuildCloseFailure(List<Exception> failures)
        => failures.Count == 1 ? failures[0] : new AggregateException(failures);

    /// <summary>
    /// 按订阅注册顺序逐一通知 <see cref="ProjectClosed"/>；单个订阅者抛异常就地隔离
    /// （best-effort 日志），不阻断后续订阅者，也不改变 C2 已经成功的关闭结果。
    /// 设计依据：到达本方法时步骤 1–3（Binding 关闭、会话关闭、集合清理）已全部
    /// 成功——通知是"资源已全部关闭"的完成信号而非关闭阶段，没有可重试的未完成
    /// 阶段。若订阅者异常外泄导致 C2 判定失败，重试会在资源全部就绪的状态下再次
    /// 执行到本方法，造成第二次通知，违反"至多通知一次"契约（P2-02）。故唯一
    /// 一致的处置是：隔离 + 记录，C2 成功收尾（Finished / SetResult 照常执行）。
    /// 日志纪律（DEF-03）：单个订阅者异常经 <see cref="WarnBestEffort"/> 记录，
    /// 日志实现自身的异常同样不可能阻断通知循环或收尾。
    /// </summary>
    private void NotifyProjectClosedSafely()
    {
        var handlers = ProjectClosed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception ex)
            {
                WarnBestEffort("ProjectClosed 订阅者通知失败（已隔离，不影响其余订阅者与关闭结果）。", ex);
            }
        }
    }

    /// <summary>
    /// best-effort 警告日志（DEF-03）：日志实现的异常就地吞掉、不向外传播——
    /// 只限 C2 编排路径使用；日志失败不改变任何关闭决策。
    /// P2-01：<paramref name="exception"/> 可省略（无异常上下文的诊断日志同样
    /// 经此调用，保证孤儿工作区回收路径上的任何日志都不可能阻断清理流程）。
    /// </summary>
    private void WarnBestEffort(string message, Exception? exception = null)
    {
        try
        {
            _log?.Warn("ProjectWorkspace", message, exception);
        }
        catch
        {
            // 日志失败本身不可恢复也不可观察（再记一次日志只会级联失败）；
            // 生命周期收尾（Finished / SetException）不依赖本调用。
        }
    }
}
