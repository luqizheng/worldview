using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WorldView.Desktop.Rendering;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

/// <summary>
/// 项目工作区窗口（WP-30B 3A 统一生命周期）：
/// - 命令入口（CloseProjectCommand）与 Window.Closing 入口汇聚同一 C2 编排
///   （<see cref="ProjectWorkspaceViewModel.CloseProjectLifetimeAsync"/>）——两入口
///   并发触发时共享同一编排任务，不并行、不重复释放；
/// - Closing 先取消本次立即关闭，接入编排并等待：成功路径由 ProjectClosed（编排
///   步骤 4，会话已 Closed）置 <see cref="_allowClose"/> 后重入 Closing 放行；
///   失败路径保留窗口并复位在途标记，用户可再次关闭重试（统一裁决 2.2：失败不
///   永久锁死生命周期对象；窗口不强制关闭避免会话停留 CloseFailed 后无人重试）；
/// - 窗口销毁时按订阅时的实例退订，避免 DataContext 切换导致订阅泄漏（PR #34 模式）。
/// </summary>
public partial class ProjectWindow : Window
{
    private bool _allowClose;
    private bool _closeInProgress;
    private ProjectWorkspaceViewModel? _subscribedViewModel;

    public ProjectWindow()
    {
        InitializeComponent();

        // 标题栏关闭也必须走项目会话关闭流程，不能只销毁窗口而遗留活动会话。
        Closing += async (_, e) =>
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;
            if (_closeInProgress)
            {
                return;
            }

            if (DataContext is not ProjectWorkspaceViewModel vm)
            {
                _allowClose = true;
                Close();
                return;
            }

            _closeInProgress = true;
            try
            {
                // 与命令路径共享同一 C2 编排任务（幂等契约）：进行中的编排不重复启动。
                await vm.CloseProjectLifetimeAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // 编排体已记录诊断；此处呈现失败并保留窗口（重试入口）。
                // 编排失败后任务字段已复位，再次 Closing / 命令都会启动重试编排。
                vm.WorkspaceStatusText = $"项目关闭失败：{ex.Message}（可再次关闭重试；详见日志）";
            }
            finally
            {
                // 成功路径由 ProjectClosed 关闭窗口；失败路径取消本次关闭但保留再次尝试能力。
                // 所有分支都必须复位，否则窗口会永久拒绝关闭。
                _closeInProgress = false;
            }
        };

        // 项目会话关闭 → 允许并执行最终窗口关闭（返回启动面）。
        // ProjectClosed 仅在 C2 编排全部阶段成功时触发（此时会话已 Closed），
        // 因此该次 Close() 直接被 Closing 处理器放行，无重入循环。
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(DataContext))
            {
                return;
            }

            if (_subscribedViewModel is { } old)
            {
                old.ProjectClosed -= OnProjectClosed;
                old.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _subscribedViewModel = e.NewValue as ProjectWorkspaceViewModel;
            if (_subscribedViewModel is { } fresh)
            {
                fresh.ProjectClosed += OnProjectClosed;
                fresh.PropertyChanged += OnViewModelPropertyChanged;
            }
        };

        // 窗口销毁时按订阅时的实例退订，避免 DataContext 已切换导致订阅泄漏。
        Closed += (_, _) =>
        {
            if (_subscribedViewModel is { } closed)
            {
                closed.ProjectClosed -= OnProjectClosed;
                closed.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _subscribedViewModel = null;
        };

        // 地图列表在窗口加载后拉取（经 Core Domain 查询端口；未实现时显示依赖门槛）。
        Opened += (_, _) =>
        {
            if (DataContext is ProjectWorkspaceViewModel loaded)
            {
                _ = loaded.LoadMapsAsync();
            }
        };
    }

    private void OnProjectClosed()
    {
        _allowClose = true;
        Close();
    }

    /// <summary>选中工作区变化 → 刷新视口宿主（M1：单视口，1 工作区 = 1 Binding = 1 控件）。</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectWorkspaceViewModel.SelectedWorkspace))
        {
            RefreshViewportHost();
        }
    }

    /// <summary>为选中工作区创建/替换视口控件；无选中或 Binding 已关闭时清空宿主。</summary>
    private void RefreshViewportHost()
    {
        if (DataContext is not ProjectWorkspaceViewModel vm)
        {
            return;
        }

        var workspace = vm.SelectedWorkspace;
        var binding = workspace is null ? null : vm.GetViewportBinding(workspace);
        if (binding is null)
        {
            ViewportHost.Content = null;
            return;
        }

        var control = new MapViewportControl();
        control.Bind(binding);
        ViewportHost.Content = control;
    }

    /// <summary>
    /// 创建地图（WP-20a）：打开创建对话框，经会话绑定的 CreateMapUseCase 正式创建；
    /// 成功后只刷新地图列表（不自动打开工作区），取消/失败无任何持久化变化。
    /// </summary>
    private async void OnCreateMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectWorkspaceViewModel vm)
        {
            return;
        }

        try
        {
            var dialog = new CreateMapDialog
            {
                DataContext = new CreateMapDialogViewModel(vm.CreateMap),
            };
            var created = await dialog.ShowDialog<bool>(this);
            if (created)
            {
                await vm.LoadMapsAsync();
            }
        }
        catch (Exception ex)
        {
            // 创建错误已在对话框内呈现；此处只兜底对话框流程自身异常，不影响会话。
            vm.WorkspaceStatusText = $"创建地图流程异常：{ex.Message}";
        }
    }
}
