using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using WorldView.Domain.Workspace;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-01-F1 领域不变量验证（真实 SQLite）：Project 隔离、Map/Branch 归属、
/// BranchLineageResolver 对 planted 腐败数据的 fail-closed 行为、以及正式
/// Migration DDL 的数据库级约束实证（CHECK/FK/UNIQUE/partial index）。
/// 腐败数据分两类注入：FK 开启的协调器通道（服务路径不可达的行仍可手工插入）、
/// 以及独立裸连接（默认 foreign_keys=OFF，模拟外部工具破坏，验证读取侧防御）。
/// </summary>
public sealed class PersistenceDomainIntegrityTests : IDisposable
{
    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new(new[] { new CoreDomainMigrationSource() });
    private readonly SqlitePackageDatabase _database;

    public PersistenceDomainIntegrityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-f1i-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
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

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private (ProjectRecord Project, MapCreationPlan Map) SeedProjectAndMap(string projectName, string mapName)
    {
        var core = Core();
        var project = ProjectRules.CreateProject(projectName, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        core.InsertProject(project);
        return (project, MapService.CreateMap(core, project.ProjectId, mapName, 10L));
    }

    private void ExecuteRaw(string sql)
    {
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        });
    }

    private static string U(Guid value) => value.ToString("D");

    /// <summary>rules_payload_json 占位值（原始插值字符串内不能内联连续大括号字面量）。</summary>
    private const string JsonEmpty = "'{}'";

    /// <summary>独立裸连接（显式 foreign_keys=OFF——Microsoft.Data.Sqlite 默认开启）：
    /// 模拟外部工具写入服务路径不可达的悬空数据，验证读取侧防御。</summary>
    private void PlantWithoutForeignKeys(Action<SqliteConnection> plant)
    {
        using var connection = new SqliteConnection(
            $"Data Source={_database.DatabasePath};Pooling=False;Foreign Keys=False");
        connection.Open();
        plant(connection);
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private void AssertRawInsertRejected(string sql)
    {
        Assert.Throws<SqliteException>(() => ExecuteRaw(sql));
    }

    // ---- 6.1 Project Isolation ----

    [Fact]
    public async Task CrossProject_Reads_ReturnNullOrRefusalOnEveryPort()
    {
        // 两个独立包（一库一项目）：A 与 B。任何 A 侧端口拿到 B 的 ID 都必须
        // 得到 null / 解析失败，且不泄露 B 的存在性。
        var databaseB = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "b.sqlite"));
        await using (databaseB.ConfigureAwait(false))
        {
            await databaseB.OpenAsync().ConfigureAwait(false);
            await _bootstrapper.InitializeNewDatabaseAsync(databaseB, DateTimeOffset.UtcNow).ConfigureAwait(false);

            var coreA = Core();
            var (projectA, mapA) = SeedProjectAndMap("A", "MapA");
            var branchA = BranchService.CreateBranch(
                coreA, projectA.ProjectId, mapA.RootHistory.HistoryId, "BA", HistoricalTime.Epoch, 20L);

            var coreB = new SqliteCoreDomainDatabase(databaseB);
            var projectB = ProjectRules.CreateProject("B", 1L);
            coreB.InsertProject(projectB);
            var mapB = MapService.CreateMap(coreB, projectB.ProjectId, "MapB", 10L);
            var branchB = BranchService.CreateBranch(
                coreB, projectB.ProjectId, mapB.RootHistory.HistoryId, "BB", HistoricalTime.Epoch, 20L);

            // A 不能查到 B 的 Map / History / Branch。
            Assert.Null(coreA.FindMap(projectA.ProjectId, mapB.Map.MapId));
            Assert.Null(coreA.FindHistory(projectA.ProjectId, mapB.RootHistory.HistoryId));
            Assert.Null(coreA.FindBranch(projectA.ProjectId, branchB.Branch.BranchId));
            Assert.Null(coreA.FindRootHistoryByMap(projectA.ProjectId, mapB.Map.MapId));
            Assert.Null(coreA.FindBranchByOwnHistory(projectA.ProjectId, branchB.OwnHistory.HistoryId));

            // A 的 Workspace 解析：B 的 mapId / branchId / 组合均被拒绝（错误不区分原因）。
            var resolverA = new CoreDomainWorkspaceContextResolver(_database);
            Assert.False((await resolverA.ResolveAsync(projectA.ProjectId, mapB.Map.MapId, null, null).ConfigureAwait(false)).Success);
            Assert.False((await resolverA.ResolveAsync(projectA.ProjectId, mapA.Map.MapId, branchB.Branch.BranchId, null).ConfigureAwait(false)).Success);
            Assert.False((await resolverA.ResolveAsync(projectA.ProjectId, mapB.Map.MapId, branchB.Branch.BranchId, null).ConfigureAwait(false)).Success);

            // A 的查询服务同样拒绝 B 的范围。
            var queryA = new CoreDomainMapQueryService(_database);
            Assert.Null(await queryA.GetMapAsync(projectA.ProjectId, mapB.Map.MapId).ConfigureAwait(false));
            Assert.DoesNotContain(
                mapB.Map.MapId,
                (await queryA.ListMapsAsync(projectA.ProjectId).ConfigureAwait(false)).Select(m => m.MapId));

            // 反向亦然；A 自身不受影响。
            var resolverB = new CoreDomainWorkspaceContextResolver(databaseB);
            Assert.False((await resolverB.ResolveAsync(projectB.ProjectId, mapA.Map.MapId, branchA.Branch.BranchId, null).ConfigureAwait(false)).Success);
            var okResolution = await resolverA.ResolveAsync(
                projectA.ProjectId, mapA.Map.MapId, branchA.Branch.BranchId, "5:0").ConfigureAwait(false);
            Assert.True(okResolution.Success, okResolution.Diagnostic);
        }
    }

    [Fact]
    public void CrossProject_Write_IsRejectedByCompositeSchemaConstraints()
    {
        // 直接把其他 Project 的 ID 传给写入端口：复合外键 (project_id, …) 在数据库层拒绝。
        var core = Core();
        var (projectA, mapA) = SeedProjectAndMap("A", "MapA");

        // A 库内伪造 B 的项目行 + B 的 map（服务层一库一项目规则管不到手工行；
        // 数据库允许存在 → 用于证明复合 FK 的项目维度防护）。
        var projectB = Guid.NewGuid();
        var mapB = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_project (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(projectB)}', 'B', 1, 1, 1);
            INSERT INTO core_map (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(mapB)}', '{U(projectB)}', 'MapB', 'active', 1, 1);
            """);

        // 声称属于 A 项目、却挂在 B 的 map 上 → 复合 FK (project_id, owner_map_id) 拒绝。
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(projectA.ProjectId)}', 'map_root', '{U(mapB)}', 1);
            """);

        // A 项目的 Branch 指向 B 项目的历史 → 复合 FK (project_id, parent_history_id) 拒绝。
        var historyB = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(historyB)}', '{U(projectB)}', 'map_root', '{U(mapB)}', 1);
            """);
        AssertRawInsertRejected($"""
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(projectA.ProjectId)}', '{U(historyB)}', '{U(Guid.NewGuid())}',
                    0, 0, 'X', 'active', 1, 1);
            """);

        // 服务层：MapService 对 A 项目的 CreateMap 计划若手动指向 B（构造期校验外），
        // 数据库 FK 是最后防线 —— 属于 B 项目的 map 行在 A 库中可见但被端口按 project 过滤。
        Assert.Null(core.FindMap(projectA.ProjectId, mapB));
    }

    // ---- 6.2/6.3 Map/Branch Ownership 与 BranchLineageResolver ----

    [Fact]
    public void Lineage_ThreeLevelChain_ResolvesRootToLeafOrder()
    {
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");
        var b1 = BranchService.CreateBranch(core, project.ProjectId, map.RootHistory.HistoryId, "B1", new HistoricalTime(10, 0), 20L);
        var b2 = BranchService.CreateBranch(core, project.ProjectId, b1.OwnHistory.HistoryId, "B2", new HistoricalTime(20, 0), 30L);
        var b3 = BranchService.CreateBranch(core, project.ProjectId, b2.OwnHistory.HistoryId, "B3", new HistoricalTime(30, 0), 40L);

        var ok = WorkspaceContextRules.TryResolve(
            core, project.ProjectId, map.Map.MapId, b3.Branch.BranchId, null, out var context, out var error);
        Assert.True(ok, error);
        Assert.Equal(3, context!.Lineage.Depth);
        Assert.Equal(map.RootHistory.HistoryId, context.Lineage.RootHistoryId);
        // 根→叶：B1, B2, B3。
        Assert.Equal(b1.Branch.BranchId, context.Lineage.Entries[0].BranchId);
        Assert.Equal(b2.Branch.BranchId, context.Lineage.Entries[1].BranchId);
        Assert.Equal(b3.Branch.BranchId, context.Lineage.Entries[2].BranchId);
        Assert.Equal(b3.Branch.BranchId, context.BranchId);
    }

    [Fact]
    public void Lineage_PlantedCycle_IsRejected()
    {
        var core = Core();
        var (project, _) = SeedProjectAndMap("P", "M");

        // 服务路径造不出环（创建时祖先链校验）；手工 SQL 可造（FK 满足）。
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(h1)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1),
                   ('{U(h2)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1);
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(h2)}', '{U(h1)}', 0, 0, 'C1', 'active', 1, 1),
                   ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(h1)}', '{U(h2)}', 0, 0, 'C2', 'active', 1, 1);
            """);

        var cycleBranch = core.FindBranchByOwnHistory(project.ProjectId, h1);
        Assert.NotNull(cycleBranch);
        Assert.Null(BranchLineageResolver.TryResolve(
            cycleBranch!, mapOwnerGuess(), id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("环", error);

        static Guid mapOwnerGuess() => Guid.NewGuid();
    }

    [Fact]
    public void Lineage_DanglingParent_PlantedWithoutFk_IsRejectedOnRead()
    {
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");

        // 外部工具在 FK 关闭的连接上写入悬空 parent → 读取侧必须 fail-closed。
        var own = Guid.NewGuid();
        var dangling = Guid.NewGuid();
        PlantWithoutForeignKeys(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                VALUES ('{U(own)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1);
                INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                         parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                         created_at_utc_ms, updated_at_utc_ms)
                VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(dangling)}', '{U(own)}', 0, 0, 'D', 'active', 1, 1);
                """;
            command.ExecuteNonQuery();
        });

        var branch = core.FindBranchByOwnHistory(project.ProjectId, own);
        Assert.NotNull(branch);
        Assert.Null(BranchLineageResolver.TryResolve(
            branch!, map.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("不存在", error);
    }

    [Fact]
    public void Lineage_OwnerlessBranchOwnedHistory_IsRejected()
    {
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");

        // branch_owned 历史存在但没有拥有它的 Branch；另一分支指向它 → 数据损坏。
        var orphan = Guid.NewGuid();
        var own = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(orphan)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1),
                   ('{U(own)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1);
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(orphan)}', '{U(own)}', 0, 0, 'X', 'active', 1, 1);
            """);

        var branch = core.FindBranchByOwnHistory(project.ProjectId, own);
        Assert.Null(BranchLineageResolver.TryResolve(
            branch!, map.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("没有对应的 Branch", error);
    }

    [Fact]
    public void Lineage_RootOfDifferentMap_IsRejected()
    {
        var core = Core();
        var (project, map1) = SeedProjectAndMap("P", "M1");
        var map2 = MapService.CreateMap(core, project.ProjectId, "M2", 11L);

        // 同项目跨 Map：分支挂在 M2 的根历史上、却以 M1 语境解析 → 拒绝。
        var own = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(own)}', '{U(project.ProjectId)}', 'branch_owned', NULL, 1);
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map2.RootHistory.HistoryId)}', '{U(own)}',
                    0, 0, 'Cross', 'active', 1, 1);
            """);

        var branch = core.FindBranchByOwnHistory(project.ProjectId, own);
        Assert.Null(BranchLineageResolver.TryResolve(
            branch!, map1.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("跨地图", error);
    }

    [Fact]
    public void Lineage_InitialBranchOwnHistoryMissing_IsRejected()
    {
        // CD-01-F1 §6.3：初始 Branch 自身一致性——自有历史缺失（FK 关闭外部写入）。
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");
        var missingOwn = Guid.NewGuid();
        PlantWithoutForeignKeys(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                         parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                         created_at_utc_ms, updated_at_utc_ms)
                VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map.RootHistory.HistoryId)}', '{U(missingOwn)}',
                        0, 0, 'NoOwn', 'active', 1, 1);
                """;
            command.ExecuteNonQuery();
        });

        var branch = Access().Execute(c =>
        {
            using var command = c.CreateCommand();
            command.CommandText = $"SELECT branch_id FROM core_branch WHERE own_history_id = '{U(missingOwn)}';";
            var branchId = Guid.Parse((string)command.ExecuteScalar()!);
            return core.FindBranch(project.ProjectId, branchId)!;
        });
        Assert.NotNull(branch);

        Assert.Null(BranchLineageResolver.TryResolve(
            branch, map.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("自有历史", error);
        Assert.Contains("不存在", error);
    }

    [Fact]
    public void Lineage_InitialBranchOwnHistoryIsMapRoot_IsRejected()
    {
        // 初始 Branch 的自有历史指向 map_root 历史（schema 允许：own_history 只要存在即可，
        // 服务路径造不出；parent 用合法的既有 branch_owned 历史避开 parent≠own CHECK）：
        // 解析必须拒绝——这正是补齐的初始一致性校验。
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");
        var legit = BranchService.CreateBranch(
            core, project.ProjectId, map.RootHistory.HistoryId, "Legit", HistoricalTime.Epoch, 20L);
        var branchId = Guid.NewGuid();
        ExecuteRaw($"""
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(branchId)}', '{U(project.ProjectId)}', '{U(legit.OwnHistory.HistoryId)}', '{U(map.RootHistory.HistoryId)}',
                    0, 0, 'OwnIsRoot', 'active', 1, 1);
            """);

        var branch = core.FindBranch(project.ProjectId, branchId);
        Assert.NotNull(branch);
        Assert.Null(BranchLineageResolver.TryResolve(
            branch!, map.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("不是 branch_owned", error);

        // 经 Workspace 解析端口同样拒绝（不泄露内部细节，只给失败）。
        var resolver = new CoreDomainWorkspaceContextResolver(_database);
        var resolution = resolver.ResolveAsync(project.ProjectId, map.Map.MapId, branchId, null)
            .GetAwaiter().GetResult();
        Assert.False(resolution.Success);
    }

    [Fact]
    public void Lineage_DepthBeyondLimit_IsRejected()
    {
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");

        // 手工铺 MaxDepth+1 级链（服务路径每次 O(depth) 校验太慢，且服务会成功创建——
        // 深度本身不是写约束，超深由解析器防御）。
        const int depth = BranchLineageResolver.MaxDepth + 1;
        var builder = new StringBuilder();
        var parent = map.RootHistory.HistoryId;
        Guid deepestOwn = Guid.Empty;
        for (var i = 0; i < depth; i++)
        {
            var historyId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            if (i == depth - 1)
            {
                deepestOwn = historyId;
            }

            builder.AppendLine($"""
                INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                VALUES ('{U(historyId)}', '{U(project.ProjectId)}', 'branch_owned', NULL, {i + 1});
                INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                         parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                         created_at_utc_ms, updated_at_utc_ms)
                VALUES ('{U(branchId)}', '{U(project.ProjectId)}', '{U(parent)}', '{U(historyId)}', 0, 0, 'L{i}', 'active', {i + 1}, {i + 1});
                """);
            parent = historyId;
        }

        ExecuteRaw(builder.ToString());

        var deepest = core.FindBranchByOwnHistory(project.ProjectId, deepestOwn);
        Assert.NotNull(deepest);
        Assert.Null(BranchLineageResolver.TryResolve(
            deepest!, map.Map.MapId, id => core.FindHistory(project.ProjectId, id),
            id => core.FindBranchByOwnHistory(project.ProjectId, id), out var error));
        Assert.Contains("深度超过上限", error);
    }

    // ---- 6.4 Schema Constraints（真实 DDL 实证） ----

    [Fact]
    public void Schema_ForeKeysAreEnforcedOnSessionConnection()
    {
        // FK 防护前提：会话连接 PRAGMA foreign_keys=ON。
        var enabled = Access().Execute(c =>
        {
            using var command = c.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys;";
            return (long)command.ExecuteScalar()!;
        });
        Assert.Equal(1, enabled);
    }

    [Fact]
    public void Schema_ForeignKeyViolations_AreRejected()
    {
        var (project, map) = SeedProjectAndMap("P", "M");

        // core_map.project_id → core_project：未知项目。
        AssertRawInsertRejected($"""
            INSERT INTO core_map (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(Guid.NewGuid())}', 'X', 'active', 1, 1);
            """);

        // core_history.project_id → core_project。
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(Guid.NewGuid())}', 'branch_owned', NULL, 1);
            """);

        // core_history 复合 FK (project_id, owner_map_id)：owner_map 不属于本项目的 map。
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', 'map_root', '{U(Guid.NewGuid())}', 1);
            """);

        // core_branch.parent_history_id → 既有历史。
        AssertRawInsertRejected($"""
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(Guid.NewGuid())}', '{U(Guid.NewGuid())}',
                    0, 0, 'X', 'active', 1, 1);
            """);

        // core_calendar_configuration.owner_history_id → 既有历史（复合 FK 含
        // (project_id, map_id, calendar_id) 归属，此处 calendar 合法、历史悬空）。
        AssertRawInsertRejected($"""
            INSERT INTO core_calendar_configuration
                (calendar_configuration_id, project_id, map_id, calendar_id, owner_history_id, source_command_id,
                 configuration_origin, effective_from_day_number, effective_from_offset_seconds,
                 effective_until_day_number, effective_until_offset_seconds,
                 rules_schema_version, rules_payload_json, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map.Map.MapId)}', '{U(map.DefaultCalendar.CalendarId)}',
                    '{U(Guid.NewGuid())}', NULL, 'user_created', 1, 0, NULL, NULL, 0, {JsonEmpty}, 1, 1);
            """);
    }

    [Fact]
    public void Schema_CheckConstraints_AreRejected()
    {
        var (project, map) = SeedProjectAndMap("P", "M");
        var root = map.RootHistory.HistoryId;

        // history_kind 词汇表 + kind/owner 组合 CHECK。
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', 'weird', NULL, 1);
            """);
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', 'map_root', NULL, 1);
            """);
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', 'branch_owned', '{U(map.Map.MapId)}', 1);
            """);

        // parent_day/offset 范围 + parent ≠ own。
        AssertRawInsertRejected(BranchSql(project.ProjectId, root, day: -1));
        AssertRawInsertRejected(BranchSql(project.ProjectId, root, offset: 86400));
        AssertRawInsertRejected(BranchSql(project.ProjectId, root, existingOwn: root));

        // calendar_role 词汇表。
        AssertRawInsertRejected($"""
            INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map.Map.MapId)}', 'wrong', NULL, 'X', 1, 1);
            """);

        // 配置 origin/source_command 配对。
        AssertRawInsertRejected(ConfigSql(project.ProjectId, map, origin: "'command_managed'", source: "NULL"));
        AssertRawInsertRejected(ConfigSql(project.ProjectId, map, origin: "'user_created'", source: "'11111111-1111-1111-1111-111111111111'"));
        // effective_until 成对可空。
        AssertRawInsertRejected(ConfigSql(project.ProjectId, map, untilDay: "5", untilOffset: "NULL"));
        AssertRawInsertRejected(ConfigSql(project.ProjectId, map, untilDay: "NULL", untilOffset: "5"));
        // effective_from 非负。
        AssertRawInsertRejected(ConfigSql(project.ProjectId, map, fromDay: "-1"));

        static string BranchSql(Guid projectId, Guid parent, int day = 0, int offset = 0, Guid? existingOwn = null)
        {
            var own = existingOwn ?? Guid.NewGuid();
            // existingOwn 指向既有历史（如根历史）时不插新 history，避免主键冲突
            // 掩盖真正要测的 CHECK。
            var historyInsert = existingOwn is null
                ? $"""
                  INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                  VALUES ('{U(own)}', '{U(projectId)}', 'branch_owned', NULL, 1);
                  """
                : string.Empty;
            return $"""
                {historyInsert}
                INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                         parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                         created_at_utc_ms, updated_at_utc_ms)
                VALUES ('{U(Guid.NewGuid())}', '{U(projectId)}', '{U(parent)}', '{U(own)}',
                        {day}, {offset}, 'X', 'active', 1, 1);
                """;
        }

        static string ConfigSql(
            Guid projectId, MapCreationPlan map, string? origin = null, string? source = null,
            string? fromDay = null, string? untilDay = null, string? untilOffset = null) => $"""
            INSERT INTO core_calendar_configuration
                (calendar_configuration_id, project_id, map_id, calendar_id, owner_history_id, source_command_id,
                 configuration_origin, effective_from_day_number, effective_from_offset_seconds,
                 effective_until_day_number, effective_until_offset_seconds,
                 rules_schema_version, rules_payload_json, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(projectId)}', '{U(map.Map.MapId)}', '{U(map.DefaultCalendar.CalendarId)}',
                    '{U(map.RootHistory.HistoryId)}', {source ?? "NULL"}, {origin ?? "'user_created'"},
                    {fromDay ?? "1"}, 0, {untilDay ?? "NULL"}, {untilOffset ?? "NULL"}, 0, {JsonEmpty}, 1, 1);
            """;
    }

    [Fact]
    public void Schema_UniqueConstraints_AreRejected()
    {
        var core = Core();
        var (project, map) = SeedProjectAndMap("P", "M");

        // 同项目重复 map_id（主键 + UNIQUE(project_id, map_id) 双保险）。
        AssertRawInsertRejected($"""
            INSERT INTO core_map (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(map.Map.MapId)}', '{U(project.ProjectId)}', 'Dup', 'active', 1, 1);
            """);

        // 同 Map 第二条 map_root 历史（UNIQUE(project_id, owner_map_id)）。
        AssertRawInsertRejected($"""
            INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', 'map_root', '{U(map.Map.MapId)}', 1);
            """);

        // 同 Map 第二个 default Calendar（partial unique index）。
        AssertRawInsertRejected($"""
            INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map.Map.MapId)}', 'default', NULL, 'Second', 1, 1);
            """);

        // auxiliary 不受 partial unique 限制（合法），但 own_history 被另一 Branch 占用时拒绝
        //（UNIQUE(project_id, own_history_id)）。
        var branch = BranchService.CreateBranch(
            core, project.ProjectId, map.RootHistory.HistoryId, "B", HistoricalTime.Epoch, 20L);
        AssertRawInsertRejected($"""
            INSERT INTO core_branch (branch_id, project_id, parent_history_id, own_history_id,
                                     parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                                     created_at_utc_ms, updated_at_utc_ms)
            VALUES ('{U(Guid.NewGuid())}', '{U(project.ProjectId)}', '{U(map.RootHistory.HistoryId)}', '{U(branch.OwnHistory.HistoryId)}',
                    0, 0, 'StealOwn', 'active', 1, 1);
            """);

        // partial unique index 实际存在于 schema。
        var indexCount = Access().Execute(c => Scalar(c,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'ux_core_calendar_default_per_map'"));
        Assert.Equal(1, indexCount);
    }

    [Fact]
    public void Schema_SingleProjectRule_IsEnforcedByServiceInTransaction()
    {
        // 一库一项目：数据库无跨行 CHECK（SQLite 表 CHECK 不可引用其他行），由 Core Domain
        // 服务在事务内强制（读取侧 0 行→NotEstablished / >1 行→MultipleProjects 同样 fail-closed）。
        var core = Core();
        var project = SeedProjectAndMap("P", "M").Project;

        Assert.Throws<CoreDomainDataException>(
            () => core.InsertProject(ProjectRules.CreateProject("second", 1L)));
        Assert.Single(core.ReadProjectIdentityRows());
    }
}
