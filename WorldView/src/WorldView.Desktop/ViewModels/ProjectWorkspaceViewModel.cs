using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;

namespace WorldView.Desktop.ViewModels;

/// <summary>
/// 项目工作区 ViewModel：显示项目身份/地图列表并打开地图工作区会话。
/// 地图数据只经 Core Domain 查询端口获取；端口未实现时如实显示依赖门槛，
/// 绝不以占位数据冒充真实地图（AF-02 §4.6）。
/// 地图创建经会话绑定的 CreateMapUseCase（正式 Core Domain 路径，WP-20a）。
/// </summary>
public partial class ProjectWorkspaceViewModel : ObservableObject
{
    private readonly IMapQueryService? _mapQuery;
    private readonly IWorkspaceContextResolver? _contextResolver;
    private readonly IAppLog? _log;

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

    /// <summary>项目会话关闭事件（窗口层据此返回启动面）。</summary>
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

    /// <summary>为选定地图打开独立工作区会话。</summary>
    [RelayCommand]
    private async Task OpenWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (SelectedMap is null)
        {
            WorkspaceStatusText = "请先选择一个地图。";
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

            Workspaces.Add(workspace);
            WorkspaceStatusText = $"工作区 {workspace.WorkspaceId:N}[{workspace.Context.MapId}] 已打开。";
        }
        catch (Exception ex)
        {
            _log?.Warn("ProjectWorkspace", "打开工作区失败。", ex);
            WorkspaceStatusText = $"打开工作区失败：{ex.Message}";
        }
    }

    /// <summary>关闭选定工作区（不影响兄弟工作区与项目会话）。</summary>
    [RelayCommand]
    private async Task CloseWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var workspace = SelectedWorkspace;
        await Session.CloseWorkspaceAsync(workspace.WorkspaceId, cancellationToken).ConfigureAwait(true);
        Workspaces.Remove(workspace);
        await workspace.DisposeAsync().ConfigureAwait(true);
        WorkspaceStatusText = $"工作区 {workspace.WorkspaceId:N} 已关闭。";
    }

    [ObservableProperty]
    private MapWorkspaceSession? _selectedWorkspace;

    /// <summary>关闭项目会话（先关子工作区再释放资源；不删除包）。</summary>
    [RelayCommand]
    private async Task CloseProjectAsync(CancellationToken cancellationToken)
    {
        await Session.CloseAsync(cancellationToken).ConfigureAwait(true);
        ProjectClosed?.Invoke();
    }
}
