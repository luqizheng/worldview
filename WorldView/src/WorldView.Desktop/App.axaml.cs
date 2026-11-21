using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Desktop.ViewModels;
using WorldView.Desktop.Views;

namespace WorldView.Desktop;

public partial class App : Application
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
