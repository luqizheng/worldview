using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Desktop.Views;

namespace WorldView.Desktop;

public partial class App : Avalonia.Application
{
    /// <summary>
    /// 组合根的服务容器。由 Program 在应用启动前注入；仅供启动阶段解析根对象使用，
    /// 其余代码应通过构造函数注入获取依赖。
    /// </summary>
    internal static IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 启动面（创建/打开/最近项目）为唯一主窗口；项目工作区以独立窗口打开。
            // 项目会话的关闭由 Program.Main 在生命周期结束后统一编排。
            desktop.MainWindow = new StartWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
