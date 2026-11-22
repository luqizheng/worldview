using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Application.Sessions;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class StartWindow : Window
{
    private readonly Func<ProjectSession, ProjectWorkspaceViewModel> _workspaceViewModelFactory;

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
        _ = vm.LoadRecentProjectsAsync();
    }

    /// <summary>会话就绪：打开工作区窗口；启动面保留（多项目并存的 V1 语义）。</summary>
    private void OnProjectOpened(ProjectSession session)
    {
        var workspaceWindow = new ProjectWindow
        {
            DataContext = _workspaceViewModelFactory(session),
        };
        workspaceWindow.Show(this);
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
