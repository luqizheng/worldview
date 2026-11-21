using System;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Desktop.ViewModels;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.IO;
using WorldView.Infrastructure.Logging;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.RecentProjects;

namespace WorldView.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 进程级异常边界（AF-01 §4.1）：详细异常进日志/诊断，不弹原始异常框。
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"[FATAL] UnhandledException: {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Console.Error.WriteLine($"[WARN] UnobservedTaskException: {e.Exception}");
        };

        using var services = BuildServices();
        App.Services = services;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        // 停机（AF-01 §4.2）：UI 生命周期已结束后，停止接受新工作并关闭全部会话
        // （先关子工作区，再逆序释放资源；不放弃未完成事务）。消息循环已退出，
        // 此处无 SynchronizationContext，同步等待关闭完成是确定且安全的。
        var registry = services.GetService<ProjectSessionRegistry>();
        registry?.CloseAllAsync().GetAwaiter().GetResult();
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
    /// Core Domain 边界端口（IProjectIdentityReader / IMapQueryService /
    /// IWorkspaceContextResolver / IProjectCreationService）当前无真实实现，
    /// 刻意不注册——用例会在对应门槛处 fail-closed 并报告阻塞项（AF-02）。
    /// </summary>
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // 基础设施适配
        services.AddSingleton<IAppLog, FileAppLog>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IRecentProjectsStore, JsonRecentProjectsStore>();
        services.AddSingleton<IPackageDatabaseFactory, SqlitePackageDatabaseFactory>();
        services.AddSingleton<IAssetStoreFactory, FileAssetStoreFactory>();

        // Application Bootstrap（迁移来源：正式 Core Domain / AI Assistant 模块迁移出现后登记）
        services.AddSingleton<ApplicationBootstrapper>(_ => new ApplicationBootstrapper(
            Array.Empty<IMigrationSource>(), log: null));

        // 会话注册表（实例由组合根创建，供停机关闭使用）
        services.AddSingleton<ProjectSessionRegistry>();

        // 用例（Core Domain 依赖端口为 null：门槛由用例如实报告）
        services.AddSingleton<CreateProjectUseCase>(sp => new CreateProjectUseCase(
            sp.GetRequiredService<IFileSystem>(),
            sp.GetRequiredService<IPackageDatabaseFactory>(),
            sp.GetRequiredService<ApplicationBootstrapper>(),
            projectCreationService: null,
            log: sp.GetRequiredService<IAppLog>()));
        services.AddSingleton<OpenProjectUseCase>(sp => new OpenProjectUseCase(
            sp.GetRequiredService<IFileSystem>(),
            sp.GetRequiredService<IPackageDatabaseFactory>(),
            sp.GetRequiredService<ApplicationBootstrapper>(),
            sp.GetRequiredService<IAssetStoreFactory>(),
            sp.GetRequiredService<ProjectSessionRegistry>(),
            identityReader: null,
            log: sp.GetRequiredService<IAppLog>()));

        // 启动面与工作区 ViewModel 工厂
        services.AddSingleton<StartViewModel>();
        services.AddSingleton<Func<ProjectSession, ProjectWorkspaceViewModel>>(sp => session =>
            new ProjectWorkspaceViewModel(
                session,
                mapQuery: null,
                contextResolver: null,
                log: sp.GetRequiredService<IAppLog>()));

        return services.BuildServiceProvider();
    }
}
