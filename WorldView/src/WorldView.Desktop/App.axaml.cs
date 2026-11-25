using Avalonia;
using Avalonia.Controls;
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
            // 隐藏首页进入项目工作区时，不能让最后一个可见窗口的关闭触发进程退出；
            // 以 MainWindow 关闭为唯一隐式退出条件，项目窗口关闭后首页才能可靠恢复。
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new StartWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
