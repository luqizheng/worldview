using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using WorldView.Desktop.Rendering;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class ProjectWindow : Window
{
    /// <summary>已有更早的 Closing 请求在等待 C2 编排完成（重入 Closing 只等待，不重复收尾）。</summary>
    private bool _closingAwaitedOnce;

    /// <summary>唯一收尾重关是否已发出（最终窗口关闭只有一个成功执行者，ADR-10 §7.2）。</summary>
    private bool _finalCloseIssued;

    public ProjectWindow()
    {
        InitializeComponent();

        // 项目会话关闭 → 关闭本窗口（返回启动面）。会话关闭次序由 ProjectSession 负责。
        // 注意：ProjectClosed 在 C2 编排任务完成之前触发（ADR-10 §7.2 步骤 4 先于
        // 编排结束），因此该次 Close() 会被下方 Closing 处理器取消并改由唯一收尾
        // 执行者完成——本订阅保留为"编排收尾通知"，不承担最终关闭职责。
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(DataContext))
            {
                return;
            }

            if (e.OldValue is ProjectWorkspaceViewModel old)
            {
                old.ProjectClosed -= Close;
                old.PropertyChanged -= OnViewModelPropertyChanged;
            }

            if (e.NewValue is ProjectWorkspaceViewModel fresh)
            {
                fresh.ProjectClosed += Close;
                fresh.PropertyChanged += OnViewModelPropertyChanged;
            }
        };

        // WP-30B R5 窗口入口：用户点 X / 系统关闭 / 程序性 Close() 一律先进 Closing。
        Closing += OnWindowClosing;

        // 地图列表在窗口加载后拉取（经 Core Domain 查询端口；未实现时显示依赖门槛）。
        Opened += (_, _) =>
        {
            if (DataContext is ProjectWorkspaceViewModel loaded)
            {
                _ = loaded.LoadMapsAsync();
            }
        };
    }

    /// <summary>
    /// 窗口关闭流程（WP-30B R5：取消本次关闭 → 接入唯一 C2 编排 → 完成后收尾重关）：
    /// - C2 编排已结束（成功或失败）→ 放行：本次 Close() 即最终关闭的唯一执行者；
    /// - 编排未结束 → 取消本次立即关闭，接入 <see cref="ProjectWorkspaceViewModel.CloseProjectLifetimeAsync"/>
    ///   并等待完成；此后由首个等待者收尾重关。
    /// 失败策略：编排异常已被编排体观察并记录；此处兜底观察后仍收尾关闭——
    /// 不允许出现关不掉的窗口，也不留未观察异常（ADR-10 §7.2 流程裁决 4）。
    /// 本处理器保持同步（无 async void）：等待与收尾都在后台 Task 中完成。
    /// </summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not ProjectWorkspaceViewModel vm)
        {
            return;
        }

        if (vm.IsProjectCloseLifetimeFinished)
        {
            // 放行（成功收尾或失败收尾后的最终关闭）。
            return;
        }

        // 取消本次立即关闭：窗口（进而应用，OnLastWindowClose）在 C2 结束前保持存活。
        e.Cancel = true;

        var isFirstWaiter = !_closingAwaitedOnce;
        _closingAwaitedOnce = true;

        // 后台等待编排完成并收尾重关；返回 Task 永不 fault（内部全捕获），丢弃安全。
        _ = AwaitCloseLifetimeAndRecloseAsync(vm, isFirstWaiter);
    }

    /// <summary>等待 C2 编排完成后的收尾：只有首个等待者执行重关（唯一收尾执行者）。</summary>
    private async Task AwaitCloseLifetimeAndRecloseAsync(ProjectWorkspaceViewModel vm, bool isFirstWaiter)
    {
        try
        {
            // 与命令路径共享同一个编排任务（幂等契约）；编排抛出的异常在此被观察。
            await vm.CloseProjectLifetimeAsync().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // 编排体已记录诊断；此处仅保证观察。失败关闭策略：仍收尾关闭窗口
            // （ADR-10 §7.2 裁决 4），残余状态由诊断暴露。
        }

        // 重入的 Closing（ProjectClosed → Close 在编排结束前触发的那次）只等待，不收尾。
        if (!isFirstWaiter || _finalCloseIssued)
        {
            return;
        }

        _finalCloseIssued = true;
        try
        {
            Close(); // → Closing（此时编排已结束）放行 → Closed。
        }
        catch (Exception)
        {
            // 收尾关闭自身失败不再重试（避免无限重复 Close）；如实发生于窗口已销毁等
            // 终态场景，无资源残余风险（编排已完成或已失败并记录）。
        }
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
