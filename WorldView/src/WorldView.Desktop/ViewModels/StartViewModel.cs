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

    public StartViewModel(
        CreateProjectUseCase createProjectUseCase,
        OpenProjectUseCase openProjectUseCase,
        IRecentProjectsStore recentProjectsStore)
    {
        _createProjectUseCase = createProjectUseCase;
        _openProjectUseCase = openProjectUseCase;
        _recentProjectsStore = recentProjectsStore;
    }

    public event Action<ProjectSession>? ProjectOpened;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private string _newProjectName = "我的世界";

    [ObservableProperty]
    private string? _createParentDirectory;

    [ObservableProperty]
    private string? _selectedPackagePath;

    [ObservableProperty]
    private RecentProjectEntry? _selectedRecentItem;

    public ObservableCollection<RecentProjectEntry> RecentProjects { get; } = new();

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public bool IsRecentProjectsEmpty => RecentProjects.Count == 0;

    public async Task LoadRecentProjectsAsync()
    {
        RecentProjects.Clear();
        var recents = await _recentProjectsStore.LoadAsync().ConfigureAwait(true);
        foreach (var entry in recents)
        {
            RecentProjects.Add(entry);
        }

        OnPropertyChanged(nameof(HasRecentProjects));
        OnPropertyChanged(nameof(IsRecentProjectsEmpty));
    }

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