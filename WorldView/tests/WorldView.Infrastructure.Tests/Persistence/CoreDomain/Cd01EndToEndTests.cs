using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Domain.Maps;
using WorldView.Domain.Time;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.IO;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-01 端到端真实链路测试（§8.3）：真实 SQLite + 真实文件系统 + 真实 Core Domain
/// 服务（镜像 Program.cs 组合根装配，不使用任何端口替身）：
/// Create Project Package → Create Core Domain Project → Persist Identity → Close →
/// Reopen → Read Identity → Query Maps → Resolve Workspace Context → Open Workspace。
/// </summary>
public sealed class Cd01EndToEndTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystem _fileSystem = new();
    private readonly SqlitePackageDatabaseFactory _databaseFactory = new();
    private readonly FileAssetStoreFactory _assetStoreFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();

    public Cd01EndToEndTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-cd01-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private (CreateProjectUseCase Create, OpenProjectUseCase Open, ProjectSessionRegistry Registry) BuildUseCases()
    {
        var registry = new ProjectSessionRegistry();
        var create = new CreateProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper,
            new CoreDomainProjectCreationService(_databaseFactory));
        var open = new OpenProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, registry,
            new CoreDomainProjectIdentityReader());
        return (create, open, registry);
    }

    [Fact]
    public async Task FullChain_CreatePersistReopenQueryResolveWorkspace()
    {
        var (create, open, _) = BuildUseCases();

        // 1. 创建：真实 Core Domain 项目创建（含身份持久化）→ 包发布。
        var created = await create.ExecuteAsync(_root, "我的世界", "  我的世界  ");
        Assert.True(created.Outcome == CreateProjectOutcome.Succeeded, created.Diagnostic);
        Assert.NotNull(created.Identity);
        var packageRoot = created.PackageRootPath!;
        Assert.True(File.Exists(Path.Combine(packageRoot, "project.sqlite")));
        var projectId = created.Identity!.ProjectId;

        // 2. 打开：真实身份读取 → Ready 会话。
        var opened = await open.ExecuteAsync(packageRoot);
        Assert.True(opened.Outcome == OpenProjectOutcome.Succeeded, opened.Diagnostic);
        var session = opened.Session!;
        Assert.True(session.IsReady);
        Assert.Equal(projectId, session.ProjectId);
        Assert.Equal("我的世界", session.ProjectDisplayName);

        // 3. 经 Core Domain 领域服务建立地图与分支（同库同事务结构）。
        var core = new SqliteCoreDomainDatabase(session.Database);
        var map = MapService.CreateMap(core, projectId, "主地图", nowUtcMs: 100L);
        MapService.CreateMap(core, projectId, "南方边境", nowUtcMs: 200L);
        var branch = BranchService.CreateBranch(
            core, projectId, map.RootHistory.HistoryId, "二战线", new HistoricalTime(1200, 0), nowUtcMs: 300L);

        // 4. 会话绑定的 Map 查询（项目范围）。
        BindSessionServices(session, out var mapQuery, out var resolver);
        var maps = await mapQuery.ListMapsAsync(projectId);
        Assert.Equal(2, maps.Count);
        Assert.Contains(maps, m => m.DisplayName == "主地图");

        // 5. 打开工作区：真实 Branch/历史时间解析 → 校验后的上下文。
        var workspace = await session.OpenWorkspaceAsync(
            map.Map.MapId, branch.Branch.BranchId, "1300:43200", mapQuery, resolver);
        Assert.Equal(map.Map.MapId, workspace.Context.MapId);
        Assert.Equal(branch.Branch.BranchId, workspace.Context.BranchId);
        Assert.Equal("1300:43200", workspace.Context.CanonicalHistoricalTime);

        // 6. 关闭会话（释放数据库资源）→ 重开同一包：身份一致。
        await session.CloseAsync();
        var reopened = await open.ExecuteAsync(packageRoot);
        Assert.True(reopened.Outcome == OpenProjectOutcome.Succeeded, reopened.Diagnostic);
        Assert.Equal(projectId, reopened.Session!.ProjectId);

        // 7. 重开后地图数据仍在（真实持久化，非内存回显）。
        var mapsAfterReopen = await new CoreDomainMapQueryService(reopened.Session!.Database)
            .ListMapsAsync(projectId);
        Assert.Equal(2, mapsAfterReopen.Count);

        await reopened.Session.CloseAsync();
    }

    [Fact]
    public async Task Isolation_TwoProjects_DoNotCrossContaminate()
    {
        var (create, open, registry) = BuildUseCases();

        var createdA = await create.ExecuteAsync(_root, "世界A", "世界A");
        var createdB = await create.ExecuteAsync(_root, "世界B", "世界B");
        Assert.True(createdA.Outcome == CreateProjectOutcome.Succeeded, createdA.Diagnostic);
        Assert.True(createdB.Outcome == CreateProjectOutcome.Succeeded, createdB.Diagnostic);
        Assert.NotEqual(createdA.Identity!.ProjectId, createdB.Identity!.ProjectId);

        // 双会话并发打开（同一注册表）。
        var openedA = await open.ExecuteAsync(createdA.PackageRootPath!);
        var openedB = await open.ExecuteAsync(createdB.PackageRootPath!);
        Assert.True(openedA.Outcome == OpenProjectOutcome.Succeeded, openedA.Diagnostic);
        Assert.True(openedB.Outcome == OpenProjectOutcome.Succeeded, openedB.Diagnostic);
        var sessionA = openedA.Session!;
        var sessionB = openedB.Session!;
        var projectA = sessionA.ProjectId!.Value;
        var projectB = sessionB.ProjectId!.Value;
        Assert.Equal(createdA.Identity.ProjectId, projectA);
        Assert.Equal(createdB.Identity.ProjectId, projectB);

        // 各自建图：A 两张、B 一张。
        var coreA = new SqliteCoreDomainDatabase(sessionA.Database);
        var coreB = new SqliteCoreDomainDatabase(sessionB.Database);
        var mapA1 = MapService.CreateMap(coreA, projectA, "A1", nowUtcMs: 1L);
        MapService.CreateMap(coreA, projectA, "A2", nowUtcMs: 2L);
        var mapB1 = MapService.CreateMap(coreB, projectB, "B1", nowUtcMs: 1L);

        // 查询互不影响。
        var queryA = new CoreDomainMapQueryService(sessionA.Database);
        var queryB = new CoreDomainMapQueryService(sessionB.Database);
        Assert.Equal(2, (await queryA.ListMapsAsync(projectA)).Count);
        Assert.Single(await queryB.ListMapsAsync(projectB));
        Assert.Null(await queryB.GetMapAsync(projectB, mapA1.Map.MapId)); // 跨项目 Map 查询被拒绝
        Assert.Null(await queryA.GetMapAsync(projectA, mapB1.Map.MapId));

        // Workspace 上下文不跨项目污染：A 的分支在 B 的解析器下失败。
        var branchA = BranchService.CreateBranch(
            coreA, projectA, mapA1.RootHistory.HistoryId, "A 分支", new HistoricalTime(10, 0), nowUtcMs: 3L);
        var resolverB = new CoreDomainWorkspaceContextResolver(sessionB.Database);
        var crossResolution = await resolverB.ResolveAsync(projectB, mapA1.Map.MapId, branchA.Branch.BranchId, null);
        Assert.False(crossResolution.Success);

        // 关闭 A：B 的查询与会话不受影响；A 可再次打开。
        await sessionA.CloseAsync();
        Assert.True(sessionB.IsReady);
        Assert.Single(await queryB.ListMapsAsync(projectB));

        var reopenA = await open.ExecuteAsync(createdA.PackageRootPath!);
        Assert.True(reopenA.Outcome == OpenProjectOutcome.Succeeded, reopenA.Diagnostic);
        Assert.Equal(2, (await new CoreDomainMapQueryService(reopenA.Session!.Database).ListMapsAsync(projectA)).Count);

        await reopenA.Session.CloseAsync();
        await sessionB.CloseAsync();
        await registry.CloseAllAsync();
    }

    private static void BindSessionServices(ProjectSession session, out CoreDomainMapQueryService mapQuery, out CoreDomainWorkspaceContextResolver resolver)
    {
        mapQuery = new CoreDomainMapQueryService(session.Database);
        resolver = new CoreDomainWorkspaceContextResolver(session.Database);
    }
}
