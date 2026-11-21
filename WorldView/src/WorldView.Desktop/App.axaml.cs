using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Desktop.ViewModels;
using WorldView.Desktop.Views;

namespace WorldView.Desktop;

// 注意：必须完整限定 Avalonia.Application——裸写 Application 会被解析为同层级的
// WorldView.Application 命名空间（CS0118）。
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
            var services = Services
                ?? throw new InvalidOperationException("DI 容器未初始化：Program 必须在启动前为 App.Services 赋值。");

            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
