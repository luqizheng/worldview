using System;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Sessions;
using WorldView.Application.Settings;
using WorldView.Application.UseCases;
using WorldView.Desktop.ViewModels;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.IO;
using WorldView.Infrastructure.Logging;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using WorldView.Infrastructure.RecentProjects;
using WorldView.Infrastructure.Settings;

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
    /// Core Domain 边界端口（IProjectIdentityReader / IProjectCreationService 全局单例；
    /// IMapQueryService / IWorkspaceContextResolver 会话绑定——按 session.Database 构造）
    /// 自 CD-01 起接入正式 Core Domain 实现（Infrastructure 适配器 + Domain 服务）。
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

        // Application Bootstrap 仅负责包级元数据；Core Domain Schema 由 EF Core Migrations 创建。
        services.AddSingleton<ApplicationBootstrapper>(_ =>
            new ApplicationBootstrapper(log: null));

        // 会话注册表（实例由组合根创建，供停机关闭使用）
        services.AddSingleton<ProjectSessionRegistry>();

        // Core Domain 边界端口（正式实现；CD-01）
        services.AddSingleton<IProjectCreationService>(sp => new CoreDomainProjectCreationService(
            sp.GetRequiredService<IPackageDatabaseFactory>()));
        services.AddSingleton<IProjectIdentityReader, CoreDomainProjectIdentityReader>();
        services.AddSingleton<IProjectFieldPayloadRecovery, CoreDomainFieldPayloadRecovery>();
        services.AddSingleton<IProjectSettingsDocumentRecovery, CoreDomainSettingsDocumentRecovery>();

        // 用例
        services.AddSingleton<CreateProjectUseCase>(sp => new CreateProjectUseCase(
            sp.GetRequiredService<IFileSystem>(),
            sp.GetRequiredService<IPackageDatabaseFactory>(),
            sp.GetRequiredService<ApplicationBootstrapper>(),
            sp.GetRequiredService<IProjectCreationService>(),
            log: sp.GetRequiredService<IAppLog>()));
        services.AddSingleton<OpenProjectUseCase>(sp => new OpenProjectUseCase(
            sp.GetRequiredService<IFileSystem>(),
            sp.GetRequiredService<IPackageDatabaseFactory>(),
            sp.GetRequiredService<ApplicationBootstrapper>(),
            sp.GetRequiredService<IAssetStoreFactory>(),
            sp.GetRequiredService<ProjectSessionRegistry>(),
            sp.GetRequiredService<IProjectIdentityReader>(),
            log: sp.GetRequiredService<IAppLog>(),
            fieldPayloadRecovery: sp.GetRequiredService<IProjectFieldPayloadRecovery>(),
            settingsDocumentRecovery: sp.GetRequiredService<IProjectSettingsDocumentRecovery>()));

        // 启动面与工作区 ViewModel 工厂
        services.AddSingleton<StartViewModel>();
        services.AddSingleton<Func<ProjectSession, ProjectWorkspaceViewModel>>(sp => session =>
        {
            // 会话绑定的 Map 查询 / 创建 / Workspace 上下文解析：按本会话数据库构造（项目范围隔离）。
            IMapQueryService mapQuery = new CoreDomainMapQueryService(session.Database);
            IWorkspaceContextResolver contextResolver = new CoreDomainWorkspaceContextResolver(session.Database);
            IMapCreationService mapCreation = new CoreDomainMapCreationService(session.Database);
            var createMapUseCase = new CreateMapUseCase(session, mapCreation, log: sp.GetRequiredService<IAppLog>());
            return new ProjectWorkspaceViewModel(
                session,
                mapQuery,
                contextResolver,
                createMapUseCase,
                log: sp.GetRequiredService<IAppLog>());
        });

        return services.BuildServiceProvider();
    }
}
