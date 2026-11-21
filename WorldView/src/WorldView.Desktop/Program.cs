using System;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Application.Abstractions;
using WorldView.Application.Projects;
using WorldView.Desktop.ViewModels;
using WorldView.Infrastructure.Projects;

namespace WorldView.Desktop;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var services = BuildServices();
        App.Services = services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// 组合根：应用全部依赖在此装配。各层仅通过接口注册，保持依赖方向单向。
    /// </summary>
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // Application 层用例服务
        services.AddSingleton<IMapProjectService, MapProjectService>();

        // Infrastructure 层实现（正式持久化方案确定前使用内存实现）
        services.AddSingleton<IMapProjectRepository, InMemoryMapProjectRepository>();

        // ViewModel
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
