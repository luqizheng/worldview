using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.IO;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// WP-20a Map 创建切片真实链路测试：真实 SQLite + 真实文件系统，经会话绑定的
/// CoreDomainMapCreationService 走正式 Domain MapService（与组合根装配一致）：
/// 创建成功 → 可查询 → 结构不变量（根 History/default Calendar/day-zero 可解析）；
/// 名称唯一性在非 UI 路径被拒绝且不留半创建数据；跨项目同名不冲突。
/// </summary>
public sealed class Wp20aMapCreationTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystem _fileSystem = new();
    private readonly SqlitePackageDatabaseFactory _databaseFactory = new();
    private readonly FileAssetStoreFactory _assetStoreFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();

    public Wp20aMapCreationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-wp20a-{Guid.NewGuid():N}");
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

    /// <summary>镜像组合根装配：创建包 → 正式打开 → 返回 Ready 会话与身份。</summary>
    private async Task<(ProjectSession Session, Guid ProjectId, string PackageRoot)> CreateAndOpenAsync(string packageName)
    {
        var registry = new ProjectSessionRegistry();
        var create = new CreateProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper,
            new CoreDomainProjectCreationService(_databaseFactory));
        var open = new OpenProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, registry,
            new CoreDomainProjectIdentityReader());

        var created = await create.ExecuteAsync(_root, packageName, packageName);
        Assert.True(created.Outcome == CreateProjectOutcome.Succeeded, created.Diagnostic);

        var opened = await open.ExecuteAsync(created.PackageRootPath!);
        Assert.True(opened.Outcome == OpenProjectOutcome.Succeeded, opened.Diagnostic);
        return (opened.Session!, created.Identity!.ProjectId, created.PackageRootPath!);
    }

    [Fact]
    public async Task CreateViaSessionAdapter_Succeeds_MapQueryableAndInvariantPreserved()
    {
        var (session, projectId, packageRoot) = await CreateAndOpenAsync("世界A");
        var creation = new CoreDomainMapCreationService(session.Database);

        var result = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "  主地图  " });
        Assert.True(result.Success, result.Diagnostic);
        Assert.NotNull(result.Map);
        Assert.Equal("主地图", result.Map!.DisplayName); // 名称已规范化（去首尾空白）

        // 正式读投影可查询到新地图。
        var query = new CoreDomainMapQueryService(session.Database);
        var maps = await query.ListMapsAsync(projectId);
        _ = Assert.Single(maps);
        Assert.Equal(result.Map.MapId, maps[0].MapId);
        Assert.Equal("主地图", maps[0].DisplayName);

        // 结构不变量保持：新 Map 的根 History/default Calendar/day-zero 经正式解析
        // 服务可解析（WorkspaceContextRules 需要 map → 根 History → default Calendar → 纪元）。
        var resolver = new CoreDomainWorkspaceContextResolver(session.Database);
        var resolution = await resolver.ResolveAsync(projectId, result.Map.MapId, null, null);
        Assert.True(resolution.Success, resolution.Diagnostic);
        Assert.Equal(result.Map.MapId, resolution.Context!.MapId);

        // Map 创建事务同时创建并绑定默认坐标框架；不虚构精度、容差或有效范围。
        var core = new SqliteCoreDomainDatabase(session.Database);
        var persistedMap = core.FindMap(projectId, result.Map.MapId)!;
        Assert.False(string.IsNullOrWhiteSpace(persistedMap.SpatialReferenceId));
        var spatialReference = core.FindSpatialReferenceDefinition(projectId, persistedMap.SpatialReferenceId!)!;
        Assert.Equal(SpatialReferenceKind.LocalCartesian, spatialReference.Kind);
        Assert.Equal("world-unit", spatialReference.CoordinateUnit);
        Assert.Equal(SpatialAxisOrder.XY, spatialReference.AxisOrder);
        Assert.Equal(2, spatialReference.Dimension);
        Assert.Equal(SpatialWrapBehavior.None, spatialReference.WrapBehavior);
        Assert.Null(spatialReference.VerticalReferenceId);
        Assert.Null(spatialReference.PrecisionProfile);
        Assert.Equal("{\"origin\":\"map-local\"}", spatialReference.DefinitionParametersJson);

        // 创建的 Map 可经现有打开流程打开工作区（M1 验收 5 的会话路径）。
        var workspace = await session.OpenWorkspaceAsync(result.Map.MapId, null, null, query, resolver);
        Assert.Equal(result.Map.MapId, workspace.Context.MapId);
        await session.CloseWorkspaceAsync(workspace.WorkspaceId);

        await session.CloseAsync();

        // 关闭后重新打开包，Map 与 SR 绑定及定义必须仍然完整可解析。
        var registryAfterReopen = new ProjectSessionRegistry();
        var reopen = new OpenProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, registryAfterReopen,
            new CoreDomainProjectIdentityReader());
        var reopened = await reopen.ExecuteAsync(packageRoot);
        Assert.Equal(OpenProjectOutcome.Succeeded, reopened.Outcome);
        var reopenedCore = new SqliteCoreDomainDatabase(reopened.Session!.Database);
        var reopenedMap = reopenedCore.FindMap(projectId, result.Map.MapId)!;
        Assert.Equal(persistedMap.SpatialReferenceId, reopenedMap.SpatialReferenceId);
        Assert.NotNull(reopenedCore.FindSpatialReferenceDefinition(projectId, reopenedMap.SpatialReferenceId!));
        await reopened.Session.CloseAsync();
    }

    [Fact]
    public async Task DuplicateName_RejectedOnNonUiPath_NoPartialData()
    {
        var (session, projectId, _) = await CreateAndOpenAsync("世界A");
        var creation = new CoreDomainMapCreationService(session.Database);

        var first = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "Main Map" });
        Assert.True(first.Success, first.Diagnostic);

        // 大小写变体与空白变体均被拒绝（正式写路径，非 UI 校验）。
        var duplicateExact = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "Main Map" });
        Assert.False(duplicateExact.Success);
        Assert.Equal(MapCreationFailureKind.DuplicateName, duplicateExact.FailureKind);

        var duplicateCase = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "main map" });
        Assert.False(duplicateCase.Success);
        Assert.Equal(MapCreationFailureKind.DuplicateName, duplicateCase.FailureKind);

        var duplicateTrim = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "  MAIN MAP  " });
        Assert.False(duplicateTrim.Success);
        Assert.Equal(MapCreationFailureKind.DuplicateName, duplicateTrim.FailureKind);

        // 无半创建数据：列表仍只有一张地图，会话仍可用（可继续创建不同名地图）。
        var query = new CoreDomainMapQueryService(session.Database);
        _ = Assert.Single(await query.ListMapsAsync(projectId));
        var other = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "南方边境" });
        Assert.True(other.Success, other.Diagnostic);
        Assert.Equal(2, (await query.ListMapsAsync(projectId)).Count);

        await session.CloseAsync();
    }

    [Fact]
    public async Task PersistenceGate_RejectsDuplicate_BypassingServicePrecheck()
    {
        // 直写持久化端口（绕过 MapService 预检）：事务内校验是权威约束。
        var (session, projectId, _) = await CreateAndOpenAsync("世界A");
        var core = new SqliteCoreDomainDatabase(session.Database);
        var project = core.FindProject(projectId) ?? throw new InvalidOperationException("项目应存在");

        var first = MapRules.CreateMap(project, "World", nowUtcMs: 1L);
        core.CreateMapStructure(first);

        var duplicate = MapRules.CreateMap(project, "WORLD", nowUtcMs: 2L);
        Assert.Throws<CoreDomainConflictException>(() => core.CreateMapStructure(duplicate));
        _ = Assert.Single(core.ListMaps(projectId));

        await session.CloseAsync();
    }

    [Fact]
    public async Task MapInsertFailure_RollsBackDefaultSpatialReferenceCreation()
    {
        var (session, projectId, _) = await CreateAndOpenAsync("世界A");
        var core = new SqliteCoreDomainDatabase(session.Database);
        var project = core.FindProject(projectId)!;

        var first = MapRules.CreateMap(project, "First", nowUtcMs: 10L);
        core.CreateMapStructure(first);
        var secondCandidate = MapRules.CreateMap(project, "Second", nowUtcMs: 11L);
        var second = secondCandidate with
        {
            DefaultCalendar = secondCandidate.DefaultCalendar with { CalendarId = first.DefaultCalendar.CalendarId },
        };

        Assert.ThrowsAny<Exception>(() => core.CreateMapStructure(second));
        Assert.Single(core.ListMaps(projectId));
        Assert.NotNull(core.FindMap(projectId, first.Map.MapId));
        Assert.NotNull(core.FindSpatialReferenceDefinition(projectId, first.DefaultSpatialReference.SpatialReferenceId));
        Assert.Null(core.FindMap(projectId, second.Map.MapId));
        Assert.Null(core.FindSpatialReferenceDefinition(projectId, second.DefaultSpatialReference.SpatialReferenceId));
        Assert.Null(core.FindMapInitialState(projectId, second.Map.MapId));

        await session.CloseAsync();
    }

    [Fact]
    public async Task SameName_DifferentProjects_NoConflict()
    {
        var (sessionA, projectIdA, _) = await CreateAndOpenAsync("世界A");
        var (sessionB, projectIdB, _) = await CreateAndOpenAsync("世界B");
        Assert.NotEqual(projectIdA, projectIdB);

        var creationA = new CoreDomainMapCreationService(sessionA.Database);
        var creationB = new CoreDomainMapCreationService(sessionB.Database);

        var mapA = await creationA.CreateAsync(new MapCreationRequest { ProjectId = projectIdA, MapName = "Main Map" });
        var mapB = await creationB.CreateAsync(new MapCreationRequest { ProjectId = projectIdB, MapName = "main map" });
        Assert.True(mapA.Success, mapA.Diagnostic);
        Assert.True(mapB.Success, mapB.Diagnostic);

        Assert.Equal(1, (await new CoreDomainMapQueryService(sessionA.Database).ListMapsAsync(projectIdA)).Count);
        Assert.Equal(1, (await new CoreDomainMapQueryService(sessionB.Database).ListMapsAsync(projectIdB)).Count);

        await sessionA.CloseAsync();
        await sessionB.CloseAsync();
    }

    [Fact]
    public async Task InvalidBlankName_RejectedByPort_NoDataWritten()
    {
        var (session, projectId, _) = await CreateAndOpenAsync("世界A");
        var creation = new CoreDomainMapCreationService(session.Database);

        var result = await creation.CreateAsync(new MapCreationRequest { ProjectId = projectId, MapName = "   " });
        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.InvalidName, result.FailureKind);
        Assert.Empty(await new CoreDomainMapQueryService(session.Database).ListMapsAsync(projectId));

        await session.CloseAsync();
    }
}
