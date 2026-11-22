using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using WorldView.Domain.Workspace;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-01 真实持久化集成测试：真实 Microsoft.Data.Sqlite + 真实文件 + 真实 core-domain
/// 迁移与适配器（不使用测试替身冒充集成，CD-01 §8.2）。
/// </summary>
public sealed class CoreDomainPersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly SqlitePackageDatabase _database;

    public CoreDomainPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-cd01-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _ = new SqliteCoreDomainDatabase(_database);
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private void ExecuteRaw(string sql)
    {
        ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        });
    }

    private long CountRows(string table)
    {
        return ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return (long)command.ExecuteScalar()!;
        });
    }

    // ---- 迁移 ----

    [Fact]
    public void FreshBootstrap_AppliesCoreDomainMigrations()
    {
        Assert.Equal(1, CountRows("__EFMigrationsHistory"));
        foreach (var table in new[]
                 {
                     "core_project", "core_map", "core_history", "core_branch",
                     "core_calendar", "core_calendar_configuration",
                     "core_object_type", "core_object", "core_object_definition",
                     "core_object_id_allocator", "core_object_id_reservation",
                     "core_map_object_ref", "core_map_initial_state", "core_map_initial_object",
                     "core_asset", "core_object_definition_asset", "core_map_field_dataset", "core_map_field_revision",
                 })
        {
            Assert.Equal(0, CountRows(table));
        }
    }

    [Fact]
    public async Task Reopen_IsIdempotentAndPassesValidation()
    {
        var second = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath);
        await using (second.ConfigureAwait(false))
        {
            await second.OpenAsync();
            var result = await _bootstrapper.ValidateExistingDatabaseAsync(second);
            Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
            Assert.Empty(result.AppliedMigrations); // 无待执行迁移
        }
    }

    // ---- Project 身份 ----

    [Fact]
    public async Task ProjectIdentity_CreateThenReopen_ReadsSameIdentity()
    {
        var creation = new CoreDomainProjectCreationService(_factory);
        var created = await creation.CreateAsync(new ProjectCreationRequest
        {
            PackageRootPath = _root,
            DatabaseRelativePath = "project.sqlite",
            DisplayName = "  Alpha World  ",
        });
        Assert.True(created.Success, created.Diagnostic);
        Assert.Equal("Alpha World", created.Identity!.DisplayName);

        // 重开新句柄读取：身份一致（持久化证据，非内存回显）。
        var second = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath);
        await using (second.ConfigureAwait(false))
        {
            await second.OpenAsync();
            var reader = new CoreDomainProjectIdentityReader();
            var identity = await reader.ReadAsync(second);
            Assert.True(identity.Success, identity.Diagnostic);
            Assert.Equal(created.Identity.ProjectId, identity.Identity!.ProjectId);
        }
    }

    [Fact]
    public async Task ProjectIdentity_SecondCreate_IsRejected()
    {
        var creation = new CoreDomainProjectCreationService(_factory);
        Assert.True((await creation.CreateAsync(new ProjectCreationRequest
        {
            PackageRootPath = _root,
            DatabaseRelativePath = "project.sqlite",
            DisplayName = "Alpha",
        })).Success);

        var second = await creation.CreateAsync(new ProjectCreationRequest
        {
            PackageRootPath = _root,
            DatabaseRelativePath = "project.sqlite",
            DisplayName = "Beta",
        });
        Assert.False(second.Success);
        Assert.Null(second.Identity);
    }

    [Fact]
    public async Task ProjectIdentity_EscapeRelativePath_IsRejected()
    {
        var creation = new CoreDomainProjectCreationService(_factory);
        var result = await creation.CreateAsync(new ProjectCreationRequest
        {
            PackageRootPath = _root,
            DatabaseRelativePath = "../outside.sqlite",
            DisplayName = "Alpha",
        });
        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "outside.sqlite")));
    }

    [Fact]
    public async Task ProjectIdentity_BlankName_IsRejected()
    {
        var creation = new CoreDomainProjectCreationService(_factory);
        var result = await creation.CreateAsync(new ProjectCreationRequest
        {
            PackageRootPath = _root,
            DatabaseRelativePath = "project.sqlite",
            DisplayName = "   ",
        });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ProjectIdentity_NotEstablished_WhenNoRow()
    {
        var reader = new CoreDomainProjectIdentityReader();
        var identity = await reader.ReadAsync(_database);
        Assert.False(identity.Success);
        Assert.Equal(ProjectIdentityFailure.NotEstablished, identity.Failure);
    }

    [Fact]
    public async Task ProjectIdentity_MultipleRows_FailsClosed()
    {
        ExecuteRaw("""
            INSERT INTO core_project (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('11111111-1111-1111-1111-111111111111', 'A', 1, 1, 1),
                   ('22222222-2222-2222-2222-222222222222', 'B', 1, 1, 1);
            """);
        var reader = new CoreDomainProjectIdentityReader();
        var identity = await reader.ReadAsync(_database);
        Assert.False(identity.Success);
        Assert.Equal(ProjectIdentityFailure.MultipleProjects, identity.Failure);
    }

    [Fact]
    public async Task ProjectIdentity_InvalidUuidText_FailsClosed()
    {
        ExecuteRaw("""
            INSERT INTO core_project (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('not-a-uuid', 'A', 1, 1, 1);
            """);
        var reader = new CoreDomainProjectIdentityReader();
        var identity = await reader.ReadAsync(_database);
        Assert.False(identity.Success);
        Assert.Equal(ProjectIdentityFailure.Invalid, identity.Failure);
    }

    // ---- Map 持久化 ----

    private Guid CreateProject()
    {
        var core = Core();
        var record = ProjectRules.CreateProject("P", nowUtcMs: 1L);
        core.InsertProject(ProjectCreationPlan.Create(record));
        return record.ProjectId;
    }

    [Fact]
    public void MapPersistence_CreateAndQueryMultipleMaps()
    {
        var core = Core();
        var projectId = CreateProject();
        var first = MapService.CreateMap(core, projectId, "M1", nowUtcMs: 100L);
        MapService.CreateMap(core, projectId, "M2", nowUtcMs: 200L);
        MapService.CreateMap(core, projectId, "M3", nowUtcMs: 300L);

        var maps = core.ListMaps(projectId);
        Assert.Equal(3, maps.Count);
        Assert.Equal(new[] { "M1", "M2", "M3" }, maps.Select(m => m.MapName)); // 创建序稳定
        Assert.Equal(first.Map.MapId, core.FindMap(projectId, first.Map.MapId)!.MapId);
    }

    [Fact]
    public void MapPersistence_UnknownOrCrossProject_ReturnsNull()
    {
        var core = Core();
        var projectId = CreateProject();
        var plan = MapService.CreateMap(core, projectId, "M", nowUtcMs: 1L);

        Assert.Null(core.FindMap(projectId, Guid.NewGuid()));
        Assert.Null(core.FindMap(Guid.NewGuid(), plan.Map.MapId)); // 跨 Project 查询
        Assert.Empty(core.ListMaps(Guid.NewGuid()));
    }

    [Fact]
    public void MapPersistence_NonexistentProject_FkRejects()
    {
        var core = Core();
        var ghost = ProjectRules.CreateProject("Ghost", nowUtcMs: 1L); // 未持久化
        var plan = MapRules.CreateMap(ghost with { ProjectName = "Ghost" }, "M", nowUtcMs: 1L);

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => core.CreateMapStructure(plan));
        Assert.Equal(0, CountRows("core_map")); // 外键拒绝，无残留
    }

    [Fact]
    public void MapCreation_FailureRollsBackWholeStructure()
    {
        var core = Core();
        var projectId = CreateProject();
        var plan = MapRules.CreateMap(core.FindProject(projectId)!, "M", nowUtcMs: 1L);
        // 用一个无关的占用行预占 calendar_id：计划的 Map/History 插入成功后，
        // Calendar 插入在事务中途违反 UNIQUE → 整体回滚。
        var occupierMapId = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_map (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{occupierMapId}', '{projectId}', 'occupier', 'active', 1, 1);
            INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{plan.DefaultCalendar.CalendarId}', '{projectId}', '{occupierMapId}', 'auxiliary', NULL, 'occupied', 1, 1);
            """);

        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => core.CreateMapStructure(plan));

        // 事务回滚：计划的 Map/根 History/Configuration 不得有任何残留。
        Assert.Null(core.FindMap(projectId, plan.Map.MapId));
        Assert.Equal(0, CountRows("core_history"));
        Assert.Equal(0, CountRows("core_calendar_configuration"));
        // 预占数据不受影响（回滚只作用于本事务）。
        Assert.Equal(1, CountRows("core_calendar"));
    }

    [Fact]
    public void MapCreation_DefaultCalendarUniquePerMap_Enforced()
    {
        var core = Core();
        var projectId = CreateProject();
        var plan = MapService.CreateMap(core, projectId, "M", nowUtcMs: 1L); // 真实持久化结构

        // 同一 Map 的第二个 default Calendar 必须被 partial unique index 拒绝。
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ExecuteRaw($"""
            INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{plan.Map.MapId}', 'default', NULL, 'dup', 1, 1);
            """));

        // auxiliary 不受该 index 限制。
        ExecuteRaw($"""
            INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{Guid.NewGuid()}', '{projectId}', '{plan.Map.MapId}', 'auxiliary', NULL, 'aux-ok', 1, 1);
            """);
        Assert.Equal(2, CountRows("core_calendar"));
    }

    // ---- Branch / Workspace 上下文 ----

    [Fact]
    public void BranchPersistence_CreateAndResolveLineage()
    {
        var core = Core();
        var projectId = CreateProject();
        var map = MapService.CreateMap(core, projectId, "M", nowUtcMs: 1L);
        var b1 = BranchService.CreateBranch(core, projectId, map.RootHistory.HistoryId, "B1", new HistoricalTime(100, 0), nowUtcMs: 2L);
        var b2 = BranchService.CreateBranch(core, projectId, b1.OwnHistory.HistoryId, "B2", new HistoricalTime(200, 3600), nowUtcMs: 3L);

        var resolver = new CoreDomainWorkspaceContextResolver(_database);

        // 根上下文：null branch + null time → 纪元。
        var root = resolver.ResolveAsync(projectId, map.Map.MapId, null, null).GetAwaiter().GetResult();
        Assert.True(root.Success, root.Diagnostic);
        Assert.Null(root.Context!.BranchId);
        Assert.Equal("0:0", root.Context.CanonicalHistoricalTime);

        // Branch-of-Branch 上下文 + 显式时间。
        var branched = resolver.ResolveAsync(projectId, map.Map.MapId, b2.Branch.BranchId, "150:43200").GetAwaiter().GetResult();
        Assert.True(branched.Success, branched.Diagnostic);
        Assert.Equal(b2.Branch.BranchId, branched.Context!.BranchId);
        Assert.Equal("150:43200", branched.Context.CanonicalHistoricalTime);

        // 世系可上溯：B1 通过 own_history 也能解析。
        var first = resolver.ResolveAsync(projectId, map.Map.MapId, b1.Branch.BranchId, null).GetAwaiter().GetResult();
        Assert.True(first.Success);
        Assert.Equal(b1.Branch.BranchId, first.Context!.BranchId);
    }

    [Fact]
    public void BranchPersistence_FkRejectsUnknownParentHistory()
    {
        var core = Core();
        var projectId = CreateProject();
        Assert.Throws<InvalidOperationException>(() =>
            BranchService.CreateBranch(core, projectId, Guid.NewGuid(), "B", HistoricalTime.Epoch, nowUtcMs: 1L));
    }

    [Fact]
    public void WorkspaceResolver_RejectsForeignMapBranchAndBadTime()
    {
        var core = Core();
        var projectId = CreateProject();
        var main = MapService.CreateMap(core, projectId, "Main", nowUtcMs: 1L);
        var other = MapService.CreateMap(core, projectId, "Other", nowUtcMs: 2L);
        var foreignBranch = BranchService.CreateBranch(core, projectId, other.RootHistory.HistoryId, "FB", new HistoricalTime(5, 0), nowUtcMs: 3L);
        var resolver = new CoreDomainWorkspaceContextResolver(_database);

        // 未知地图。
        Assert.False(resolver.ResolveAsync(projectId, Guid.NewGuid(), null, null).GetAwaiter().GetResult().Success);
        // 跨 Project 地图。
        Assert.False(resolver.ResolveAsync(Guid.NewGuid(), main.Map.MapId, null, null).GetAwaiter().GetResult().Success);
        // 属于其他地图的分支。
        var crossMap = resolver.ResolveAsync(projectId, main.Map.MapId, foreignBranch.Branch.BranchId, null).GetAwaiter().GetResult();
        Assert.False(crossMap.Success);
        Assert.Contains("跨地图", crossMap.Diagnostic);
        // 非法时间格式。
        Assert.False(resolver.ResolveAsync(projectId, main.Map.MapId, null, "not-a-time").GetAwaiter().GetResult().Success);
        Assert.False(resolver.ResolveAsync(projectId, main.Map.MapId, null, "5:86400").GetAwaiter().GetResult().Success);
        // 未知分支。
        Assert.False(resolver.ResolveAsync(projectId, main.Map.MapId, Guid.NewGuid(), null).GetAwaiter().GetResult().Success);
    }

    [Fact]
    public void MapQueryAdapter_ProjectScoped()
    {
        var core = Core();
        var projectId = CreateProject();
        var plan = MapService.CreateMap(core, projectId, "M", nowUtcMs: 1L);
        var query = new CoreDomainMapQueryService(_database);

        var list = query.ListMapsAsync(projectId).GetAwaiter().GetResult();
        Assert.Single(list);
        Assert.Equal("M", list[0].DisplayName);

        Assert.NotNull(query.GetMapAsync(projectId, plan.Map.MapId).GetAwaiter().GetResult());
        Assert.Null(query.GetMapAsync(projectId, Guid.NewGuid()).GetAwaiter().GetResult());
        Assert.Null(query.GetMapAsync(Guid.NewGuid(), plan.Map.MapId).GetAwaiter().GetResult());
    }

    [Fact]
    public void MapQueryAdapter_RejectsFakeDatabase()
    {
        Assert.Throws<ArgumentException>(() => new CoreDomainMapQueryService(new FakeDb()));
    }

    private sealed class FakeDb : IPackageDatabase
    {
        public string DatabasePath => "fake";
        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default) => Task.FromResult<BootstrapMetadata?>(null);
        public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<System.Collections.Generic.IReadOnlyList<MigrationLedgerEntry>> ReadLedgerAsync(CancellationToken cancellationToken = default) => Task.FromResult<System.Collections.Generic.IReadOnlyList<MigrationLedgerEntry>>(Array.Empty<MigrationLedgerEntry>());
        public Task<bool> LedgerTableExistsAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task EnsureLedgerTableAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExecuteMigrationAsync(ModuleMigration migration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
