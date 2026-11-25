using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Application.Abstractions;
using WorldView.Application.Sessions;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class StartWindow : Window
{
    private readonly Func<ProjectSession, ProjectWorkspaceViewModel> _workspaceViewModelFactory;
    private ProjectWindow? _workspaceWindow;

    public StartWindow()
    {
        InitializeComponent();

        // 主窗口构造发生在应用启动期，此处使用组合根符合 AGENTS「App.Services 仅限启动期」约定。
        var services = App.Services
            ?? throw new InvalidOperationException("DI 容器未初始化：Program 必须在启动前为 App.Services 赋值。");
        _workspaceViewModelFactory = services.GetRequiredService<Func<ProjectSession, ProjectWorkspaceViewModel>>();

        var vm = services.GetRequiredService<StartViewModel>();
        DataContext = vm;
        vm.ProjectOpened += OnProjectOpened;
        Closed += (_, _) => vm.ProjectOpened -= OnProjectOpened;
        _ = ObserveRecentProjectsLoadAsync(vm);
    }

    /// <summary>
    /// 观察启动期最近项目加载任务：ViewModel 已把存储失败转为状态与日志，
    /// 这里兜底任何意外异常，保证未观察异常不会中断启动窗口。
    /// </summary>
    private static async Task ObserveRecentProjectsLoadAsync(StartViewModel vm)
    {
        try
        {
            await vm.LoadRecentProjectsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.Services?.GetService<IAppLog>()?.Error("StartWindow", "最近项目加载出现未处理异常。", ex);
            vm.ReportRecentProjectsLoadFailure(ex);
        }
    }

    /// <summary>项目会话就绪：隐藏项目首页并显示唯一的项目工作区窗口。</summary>
    private void OnProjectOpened(ProjectSession session)
    {
        // 同一会话的 DuplicateOpen 或重复点击不能创建第二个工作区窗口。
        if (_workspaceWindow is { IsVisible: true } existing)
        {
            existing.Activate();
            return;
        }

        var workspaceWindow = new ProjectWindow
        {
            DataContext = _workspaceViewModelFactory(session),
        };
        _workspaceWindow = workspaceWindow;

        // StartWindow 是应用 MainWindow，不能 Close；隐藏它可避免触发应用级退出。
        // 工作区关闭（包括标题栏关闭按钮）后再恢复首页。
        workspaceWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_workspaceWindow, workspaceWindow))
            {
                _workspaceWindow = null;
            }

            Show();
            Activate();
        };

        workspaceWindow.Show();
        Hide();
    }

    /// <summary>选择目标父目录（创建项目包）。</summary>
    private async void OnChooseCreateLocationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择项目包保存位置",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            ((StartViewModel)DataContext!).CreateParentDirectory = folders[0].Path.LocalPath;
        }
    }

    /// <summary>选择 .wvproject 包目录（打开项目）。</summary>
    private async void OnChoosePackageClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择 .wvproject 项目包目录",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            var vm = (StartViewModel)DataContext!;
            vm.SelectedPackagePath = folders[0].Path.LocalPath;
            RecentList.SelectedItem = vm.RecentProjects
                .FirstOrDefault(r => string.Equals(r.PackagePath, vm.SelectedPackagePath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
