using System;
using System.Threading;
using Avalonia.Controls;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class ProjectWindow : Window
{
    private bool _allowClose;
    private bool _closeInProgress;

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
                await vm.CloseProjectCommand.ExecuteAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                vm.WorkspaceStatusText = $"关闭项目失败：{ex.Message}";
            }
            finally
            {
                // 成功后 ProjectClosed 会关闭窗口；若实现路径未触发事件也允许再次尝试关闭。
                _closeInProgress = false;
            }
        };

        // 项目会话关闭 → 关闭本窗口（返回启动面）。会话关闭次序由 ProjectSession 负责。
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(DataContext))
            {
                return;
            }

            if (e.OldValue is ProjectWorkspaceViewModel old)
            {
                old.ProjectClosed -= OnProjectClosed;
            }

            if (e.NewValue is ProjectWorkspaceViewModel fresh)
            {
                fresh.ProjectClosed += OnProjectClosed;
            }
        };

        // 窗口销毁时解除对 ViewModel 的事件订阅，避免长生命周期 VM 持有已关闭窗口。
        Closed += (_, _) =>
        {
            if (DataContext is ProjectWorkspaceViewModel closed)
            {
                closed.ProjectClosed -= OnProjectClosed;
            }
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

    /// <summary>
    /// 创建地图（WP-20a）：打开创建对话框，经会话绑定的 CreateMapUseCase 正式创建；
    /// 成功后只刷新地图列表（不自动打开工作区），取消/失败无任何持久化变化。
    /// </summary>
    private async void OnCreateMapClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
