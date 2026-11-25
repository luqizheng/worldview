using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;

namespace WorldView.Desktop.ViewModels;

/// <summary>
/// 启动面 ViewModel（AF-01 §4.1 第 6 步）：创建项目 / 打开项目 / 最近项目。
/// 只通过 Application 用例交互；不做任何领域 SQL 或文件写入。
/// </summary>
public partial class StartViewModel : ObservableObject
{
    private readonly CreateProjectUseCase _createProjectUseCase;
    private readonly OpenProjectUseCase _openProjectUseCase;
    private readonly IRecentProjectsStore _recentProjectsStore;
    private readonly IAppLog? _log;

    public StartViewModel(
        CreateProjectUseCase createProjectUseCase,
        OpenProjectUseCase openProjectUseCase,
        IRecentProjectsStore recentProjectsStore,
        IAppLog? log = null)
    {
        _createProjectUseCase = createProjectUseCase;
        _openProjectUseCase = openProjectUseCase;
        _recentProjectsStore = recentProjectsStore;
        _log = log;
    }

    /// <summary>会话就绪事件：窗口层切换到项目工作区。</summary>
    public event Action<ProjectSession>? ProjectOpened;

    /// <summary>状态栏文本（操作结果与诊断摘要）。</summary>
    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>新建项目名（不含后缀时可自动补 .wvproject）。</summary>
    [ObservableProperty]
    private string _newProjectName = "我的世界";

    /// <summary>由窗口层文件/文件夹选择器设置的目标父目录与待打开包路径。</summary>
    [ObservableProperty]
    private string? _createParentDirectory;

    [ObservableProperty]
    private string? _selectedPackagePath;

    /// <summary>最近项目列表当前选中项。</summary>
    [ObservableProperty]
    private RecentProjectEntry? _selectedRecentItem;

    /// <summary>最近项目列表。</summary>
    public ObservableCollection<RecentProjectEntry> RecentProjects { get; } = new();

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public bool IsRecentProjectsEmpty => RecentProjects.Count == 0;

    /// <summary>最近项目读取失败时的可见诊断（成功加载后清空）。</summary>
    [ObservableProperty]
    private string _recentProjectsLoadDiagnostic = string.Empty;

    /// <summary>
    /// 启动时加载最近项目，并刷新空状态投影。存储读取失败转为状态与日志，
    /// 不向启动窗口抛出，也不影响新建/手动打开项目。
    /// </summary>
    public async Task LoadRecentProjectsAsync()
    {
        RecentProjects.Clear();
        try
        {
            var recents = await _recentProjectsStore.LoadAsync().ConfigureAwait(true);
            foreach (var entry in recents)
            {
                RecentProjects.Add(entry);
            }

            RecentProjectsLoadDiagnostic = string.Empty;
            if (StatusText.StartsWith("最近项目加载失败", StringComparison.Ordinal))
            {
                StatusText = "就绪";
            }
        }
        catch (Exception ex)
        {
            ReportRecentProjectsLoadFailure(ex);
        }
        finally
        {
            OnPropertyChanged(nameof(HasRecentProjects));
            OnPropertyChanged(nameof(IsRecentProjectsEmpty));
        }
    }

    /// <summary>把最近项目加载失败落为可见状态并写入应用日志（供启动窗口兜底路径复用）。</summary>
    internal void ReportRecentProjectsLoadFailure(Exception ex)
    {
        _log?.Error("StartWindow", "最近项目加载失败。", ex);
        RecentProjectsLoadDiagnostic = "最近项目加载失败（详见日志）。";
        StatusText = RecentProjectsLoadDiagnostic;
    }

    /// <summary>创建项目包（目标父目录由窗口层选择）。</summary>
    [RelayCommand]
    private async Task CreateProjectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CreateParentDirectory))
        {
            StatusText = "请先选择保存位置。";
            return;
        }

        StatusText = "正在创建项目包…";
        var result = await _createProjectUseCase.ExecuteAsync(
            CreateParentDirectory, NewProjectName.Trim(), NewProjectName.Trim(), cancellationToken)
            .ConfigureAwait(true);

        switch (result.Outcome)
        {
            case CreateProjectOutcome.Succeeded:
                StatusText = $"项目包已创建：{result.PackageRootPath}";
                await OpenPackageAsync(result.PackageRootPath!, cancellationToken).ConfigureAwait(true);
                break;
            case CreateProjectOutcome.CoreDomainProjectCreationUnavailable:
                StatusText = "已停止在 bootstrap 边界（依赖门槛）：Core Domain 项目创建服务尚未实现，"
                    + "未生成任何包文件，详见 AF-02 实施报告。";
                break;
            case CreateProjectOutcome.DestinationExists:
                StatusText = result.Diagnostic ?? "目标路径已存在。";
                break;
            default:
                StatusText = result.Diagnostic ?? "创建失败。";
                break;
        }
    }

    /// <summary>打开选定包路径（由窗口层选择）。</summary>
    [RelayCommand]
    private async Task OpenProjectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SelectedPackagePath))
        {
            StatusText = "请先选择 .wvproject 包目录。";
            return;
        }

        await OpenPackageAsync(SelectedPackagePath, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>打开最近项目。</summary>
    [RelayCommand]
    private async Task OpenRecentAsync(RecentProjectEntry? entry, CancellationToken cancellationToken)
    {
        if (entry is null)
        {
            return;
        }

        await OpenPackageAsync(entry.PackagePath, cancellationToken).ConfigureAwait(true);
    }

    private async Task OpenPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        StatusText = $"正在打开 {Path.GetFileName(packagePath.TrimEnd(Path.DirectorySeparatorChar))} …";
        var result = await _openProjectUseCase.ExecuteAsync(packagePath, cancellationToken).ConfigureAwait(true);

        switch (result.Outcome)
        {
            case OpenProjectOutcome.Succeeded:
                StatusText = $"已打开：{result.Session!.ProjectDisplayName ?? result.Session.CanonicalPackagePath}";
                await _recentProjectsStore.RecordOpenedAsync(
                    result.Session.CanonicalPackagePath, DateTimeOffset.UtcNow).ConfigureAwait(true);
                ProjectOpened?.Invoke(result.Session);
                break;

            case OpenProjectOutcome.ProjectIdentityUnavailable:
                StatusText = "打开已 fail-closed（依赖门槛）：Core Domain 项目身份读取服务尚未实现，"
                    + "包结构与数据库校验已通过；正式打开需待 Core Domain 持久化落地。详见 AF-02 实施报告。";
                break;

            case OpenProjectOutcome.InvalidPackage:
                StatusText = result.Report?.ToSummary() ?? "包校验失败。";
                break;

            case OpenProjectOutcome.DuplicateOpen:
                StatusText = result.Diagnostic ?? "该项目包已打开。";
                ProjectOpened?.Invoke(result.ExistingSession!);
                break;

            default:
                StatusText = result.Diagnostic ?? "打开失败。";
                break;
        }
    }
}
