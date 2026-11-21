using Avalonia.Controls;
using WorldView.Desktop.ViewModels;

namespace WorldView.Desktop.Views;

public partial class ProjectWindow : Window
{
    public ProjectWindow()
    {
        InitializeComponent();

        // 项目会话关闭 → 关闭本窗口（返回启动面）。会话关闭次序由 ProjectSession 负责。
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(DataContext))
            {
                return;
            }

            if (e.OldValue is ProjectWorkspaceViewModel old)
            {
                old.ProjectClosed -= Close;
            }

            if (e.NewValue is ProjectWorkspaceViewModel fresh)
            {
                fresh.ProjectClosed += Close;
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
}
