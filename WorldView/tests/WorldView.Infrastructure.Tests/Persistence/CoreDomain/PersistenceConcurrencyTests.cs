using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-01-F1 Persistence Integrity 并发测试（真实 SQLite 文件 + 真实适配器，
/// 不用 Mock 锁或模拟对象；Barrier 对齐起跑，不依赖 Sleep）。
///
/// 背景：旧实现中两个适配器各持独立实例级 _gate 却共享同一 SqliteConnection。
/// 仓库外复现实验（CD-01-F1 报告 §4）测得旧模型 Write∥Write 约 50% 写入以
/// SqliteException('cannot start a transaction within a transaction') 失败、
/// Dispose 竞争 99/100 在飞读撞上原生 ObjectDisposedException('SQLitePCL.sqlite3')。
/// 本文件以同构场景验证 V1 统一协调器（SqliteConnectionAccess）消除上述缺陷。
/// </summary>
public sealed class PersistenceConcurrencyTests : IDisposable
{
    private readonly string _root;
    private readonly SqlitePackageDatabaseFactory _factory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new(new[] { new CoreDomainMigrationSource() });
    private readonly SqlitePackageDatabase _database;

    public PersistenceConcurrencyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-f1-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = CreateInitializedDatabase("project.sqlite");
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

    private SqlitePackageDatabase CreateInitializedDatabase(string fileName)
    {
        var database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, fileName));
        database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        return database;
    }

    /// <summary>仅建 ledger + 元数据、不应用任何模块迁移的数据库（迁移失败测试用）。</summary>
    private SqlitePackageDatabase CreateLedgerOnlyDatabase(string fileName)
    {
        var database = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, fileName));
        database.OpenAsync().GetAwaiter().GetResult();
        database.EnsureLedgerTableAsync().GetAwaiter().GetResult();
        database.WriteMetadataAsync(new BootstrapMetadata
        {
            DatabaseFormatVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        }).GetAwaiter().GetResult();
        return database;
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private static SqliteConnectionAccess AccessOf(SqlitePackageDatabase database) =>
        ((ISqliteConnectionProvider)database).GetAccess();

    private ProjectRecord SeedProject(SqliteCoreDomainDatabase core, string name = "P")
    {
        var record = ProjectRules.CreateProject(name, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        core.InsertProject(ProjectCreationPlan.Create(record));
        return record;
    }

    private static Exception? Capture(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Capture 的异步形式（async lambda 不可作 async void Action 捕获）。</summary>
    private static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>并发体只允许两类结果：成功，或协调器入口拒绝（ObjectDisposedException，
    /// ObjectName=协调器类型名——区别于旧模型的原生 'SQLitePCL.sqlite3' 失控异常）。</summary>
    private static void AssertDefinedOutcome(Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        var disposed = Assert.IsType<ObjectDisposedException>(exception);
        Assert.Equal("SqliteConnectionAccess", disposed.ObjectName);
    }

    // ---- 5.1 Shared Connection Tests ----

    [Fact]
    public async Task Concurrent_CompositionRootAdapterPair_QueriesSucceedOnSharedConnection()
    {
        // 组合根同构配对（Program.cs）：MapQueryService 与 WorkspaceContextResolver
        // 各自包装同一 session.Database —— 旧模型下两者锁互不相干。
        var core = Core();
        var project = SeedProject(core);
        var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);
        var branchPlan = BranchService.CreateBranch(
            core, project.ProjectId, mapPlan.RootHistory.HistoryId, "B1", new HistoricalTime(10, 0), 20L);

        var mapQuery = new CoreDomainMapQueryService(_database);
        var resolver = new CoreDomainWorkspaceContextResolver(_database);

        var barrier = new Barrier(2);
        var queryTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < 150 && failure is null; i++)
            {
                failure = Capture(() =>
                {
                    var maps = mapQuery.ListMapsAsync(project.ProjectId).GetAwaiter().GetResult();
                    Assert.Single(maps);
                });
            }

            return failure;
        });
        var resolveTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < 150 && failure is null; i++)
            {
                failure = Capture(() =>
                {
                    var resolution = resolver.ResolveAsync(
                        project.ProjectId, mapPlan.Map.MapId, branchPlan.Branch.BranchId, null).GetAwaiter().GetResult();
                    Assert.True(resolution.Success, resolution.Diagnostic);
                    Assert.Equal(branchPlan.Branch.BranchId, resolution.Context!.BranchId);
                });
            }

            return failure;
        });

        var failures = (await Task.WhenAll(queryTask, resolveTask).ConfigureAwait(false))
            .Where(f => f is not null).ToList();
        Assert.All(failures, AssertDefinedOutcome);
        Assert.Empty(failures); // 连接未被关闭，任何失败都是缺陷
    }

    [Fact]
    public async Task Concurrent_TwoWritersOnSameConnection_BothCommitFully()
    {
        // 旧模型杀手场景（复现实验场景 1 同构）：两个 Core 实例（旧实现=独立 _gate）并发写。
        var core = Core();
        var project = SeedProject(core);
        var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);

        var coreA = new SqliteCoreDomainDatabase(_database);
        var coreB = new SqliteCoreDomainDatabase(_database);
        const int rounds = 100;
        var barrier = new Barrier(2);
        var writerA = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < rounds && failure is null; i++)
            {
                failure = Capture(() => BranchService.CreateBranch(
                    coreA, project.ProjectId, mapPlan.RootHistory.HistoryId, $"A{i}", new HistoricalTime(i, 0), 100L + i));
            }

            return failure;
        });
        var writerB = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < rounds && failure is null; i++)
            {
                failure = Capture(() => BranchService.CreateBranch(
                    coreB, project.ProjectId, mapPlan.RootHistory.HistoryId, $"B{i}", new HistoricalTime(i, 1), 100L + i));
            }

            return failure;
        });

        var failures = (await Task.WhenAll(writerA, writerB).ConfigureAwait(false))
            .Where(f => f is not null).ToList();
        Assert.Empty(failures); // 双写者全部成功（旧模型此处约 50% 失败）

        // 全部提交且结构完整：branch_owned history 与 branch 一一对应（无半成品）。
        Assert.Equal(2 * rounds + 1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_history")));
        Assert.Equal(2 * rounds, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_branch")));
    }

    [Fact]
    public async Task Concurrent_InsertProjectRace_ExactlyOneWinsWithDefinedDomainFailure()
    {
        // 一库一项目在统一临界区 + 事务内检查：恰好一个成功，另一个得到定义良好的
        // CoreDomainDataException（而非旧模型的连接级 SqliteException）。
        var coreA = new SqliteCoreDomainDatabase(_database);
        var coreB = new SqliteCoreDomainDatabase(_database);
        var barrier = new Barrier(2);

        var a = Task.Run(() =>
        {
            barrier.SignalAndWait();
            return Capture(() => coreA.InsertProject(ProjectCreationPlan.Create(ProjectRules.CreateProject("A", 1L))));
        });
        var b = Task.Run(() =>
        {
            barrier.SignalAndWait();
            return Capture(() => coreB.InsertProject(ProjectCreationPlan.Create(ProjectRules.CreateProject("B", 1L))));
        });

        var failures = (await Task.WhenAll(a, b).ConfigureAwait(false))
            .Where(f => f is not null).ToList();
        Assert.Single(failures); // 恰好一个失败
        Assert.IsType<CoreDomainDataException>(failures[0]);
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_project")));
    }

    [Fact]
    public async Task Concurrent_ReadersAndWriters_NeverObservePartialMapStructure()
    {
        var core = Core();
        var project = SeedProject(core);
        MapService.CreateMap(core, project.ProjectId, "seed", 10L);

        const int writerMaps = 30;
        const int readerCount = 3;
        var barrier = new Barrier(1 + readerCount);

        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < writerMaps; i++)
            {
                MapService.CreateMap(core, project.ProjectId, $"M{i}", 100L + i);
            }
        });
        var readers = Enumerable.Range(0, readerCount).Select(r => Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            var readerCore = new SqliteCoreDomainDatabase(_database);
            for (var i = 0; i < 120 && failure is null; i++)
            {
                failure = Capture(() =>
                {
                    var maps = readerCore.ListMaps(project.ProjectId);
                    foreach (var map in maps)
                    {
                        // 部分写入不可见：任何被 ListMaps 观察到的 Map，其根历史必须同时可见
                        //（CreateMapStructure 单事务，读在同一协调器上不可能切进事务中缝）。
                        Assert.NotNull(readerCore.FindRootHistoryByMap(project.ProjectId, map.MapId));
                    }
                });
            }

            return failure;
        })).ToArray();

        await writer.ConfigureAwait(false); // 写侧失败直接抛出（即缺陷）
        var readerFailures = await Task.WhenAll(readers).ConfigureAwait(false);
        Assert.All(readerFailures, f => Assert.Null(f)); // 读侧不得有任何失败

        // 终态完整：map/root history/default calendar/day-zero config 一一对应。
        var mapsCount = Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_map"));
        Assert.Equal(writerMaps + 1, mapsCount);
        Assert.Equal(mapsCount, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_history WHERE history_kind = 'map_root'")));
        Assert.Equal(mapsCount, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar")));
        Assert.Equal(mapsCount, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar_configuration")));
    }

    [Fact]
    public async Task Concurrent_PackageDbOperationsAndCoreAccess_ShareSingleCoordinator()
    {
        // 包数据库自身操作（ledger/完整性检查）与 Core 适配器并发——全部经同一协调器，
        // 不得出现 'cannot start a transaction within a transaction' 类连接错乱。
        var core = Core();
        var project = SeedProject(core);
        MapService.CreateMap(core, project.ProjectId, "M", 10L);

        var barrier = new Barrier(3);
        var ledgerTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < 60 && failure is null; i++)
            {
                failure = await CaptureAsync(async () =>
                {
                    var entries = await _database.ReadLedgerAsync().ConfigureAwait(false);
                    Assert.Equal(10, entries.Count);
                }).ConfigureAwait(false);
            }

            return failure;
        });
        var integrityTask = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            for (var i = 0; i < 60 && failure is null; i++)
            {
                failure = await CaptureAsync(async () =>
                    Assert.True(await _database.IntegrityCheckAsync().ConfigureAwait(false))).ConfigureAwait(false);
            }

            return failure;
        });
        var coreTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            Exception? failure = null;
            var query = new CoreDomainMapQueryService(_database);
            for (var i = 0; i < 60 && failure is null; i++)
            {
                failure = Capture(() =>
                    Assert.Single(query.ListMapsAsync(project.ProjectId).GetAwaiter().GetResult()));
            }

            return failure;
        });

        var failures = (await Task.WhenAll(ledgerTask, integrityTask, coreTask).ConfigureAwait(false))
            .Where(f => f is not null).ToList();
        Assert.Empty(failures);
    }

    // ---- 5.2 Transaction Tests ----

    [Fact]
    public async Task ConnectionPolicy_EveryOpenedPackageConnectionEnablesForeignKeysAndBoundedTimeouts()
    {
        // TX-03: PRAGMA and command defaults are per physical connection, not process-wide.
        await using var second = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath);
        await second.OpenAsync();

        foreach (var access in new[] { Access(), AccessOf(second) })
        {
            Assert.Equal(1L, access.Execute(c => Scalar(c, "PRAGMA foreign_keys;")));
            Assert.Equal(5000L, access.Execute(c => Scalar(c, "PRAGMA busy_timeout;")));
            Assert.Equal(5, access.Execute(c =>
            {
                using var command = c.CreateCommand();
                return command.CommandTimeout;
            }));
        }

        Access().Execute(c =>
        {
            using var command = c.CreateCommand();
            command.CommandText = """
                CREATE TABLE tx03_parent (id INTEGER PRIMARY KEY);
                CREATE TABLE tx03_child (
                    id INTEGER PRIMARY KEY,
                    parent_id INTEGER NOT NULL REFERENCES tx03_parent(id)
                );
                """;
            command.ExecuteNonQuery();
        });

        var foreignKeyFailure = Assert.Throws<SqliteException>(() =>
            AccessOf(second).Execute(c =>
            {
                using var command = c.CreateCommand();
                command.CommandText = "INSERT INTO tx03_child (id, parent_id) VALUES (1, 999);";
                command.ExecuteNonQuery();
            }));

        Assert.Equal(19, foreignKeyFailure.SqliteErrorCode); // SQLITE_CONSTRAINT
        Assert.Equal(787, foreignKeyFailure.SqliteExtendedErrorCode); // SQLITE_CONSTRAINT_FOREIGNKEY
        Assert.Equal(0L, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM tx03_child;")));
    }

    [Fact]
    public void BusyTimeout_ExternalWriterLock_FailsWithinBoundWithoutPartialWrite()
    {
        const string tableName = "tx32_busy_probe";

        // TX-32: the production connection policy sets a bounded 5-second busy timeout.
        Assert.Equal(5000L, Access().Execute(c => Scalar(c, "PRAGMA busy_timeout;")));
        Assert.Equal(5, Access().Execute(c =>
        {
            using var command = c.CreateCommand();
            return command.CommandTimeout;
        }));

        Access().Execute(c =>
        {
            using var create = c.CreateCommand();
            create.CommandText = $"CREATE TABLE {tableName} (value INTEGER NOT NULL);";
            create.ExecuteNonQuery();

            // Keep the production default assertion above, then shorten only this test's
            // contention window so the negative path remains fast and deterministic.
            using var timeout = c.CreateCommand();
            timeout.CommandText = "PRAGMA busy_timeout = 100;";
            timeout.ExecuteNonQuery();
        });

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _database.DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ConnectionString;

        using var external = new SqliteConnection(connectionString);
        external.Open();
        using var externalTransaction = external.BeginTransaction();
        using (var heldWrite = external.CreateCommand())
        {
            heldWrite.Transaction = externalTransaction;
            heldWrite.CommandText = $"INSERT INTO {tableName} (value) VALUES (1);";
            heldWrite.ExecuteNonQuery();
        }

        var stopwatch = Stopwatch.StartNew();
        var exception = Assert.Throws<SqliteException>(() => Access().Execute(c =>
        {
            using var competingWrite = c.CreateCommand();
            competingWrite.CommandText = $"INSERT INTO {tableName} (value) VALUES (2);";
            competingWrite.CommandTimeout = 1;
            competingWrite.ExecuteNonQuery();
        }));
        stopwatch.Stop();

        Assert.Equal(5, exception.SqliteErrorCode); // SQLITE_BUSY
        Assert.InRange(stopwatch.ElapsedMilliseconds, 500, 3000);

        externalTransaction.Rollback();
        Assert.Equal(0L, Access().Execute(c => Scalar(c, $"SELECT COUNT(*) FROM {tableName};")));
    }

    [Fact]
    public void Transaction_Commit_PersistsWholeMapStructure()
    {
        var core = Core();
        var project = SeedProject(core);
        var plan = MapService.CreateMap(core, project.ProjectId, "M", 10L);

        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_map")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, $"SELECT COUNT(*) FROM core_history WHERE history_id = '{plan.RootHistory.HistoryId:D}'")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar_configuration")));
    }

    [Fact]
    public void Transaction_MidwayUniqueViolation_RollsBackEntirelyAndConnectionStaysUsable()
    {
        var core = Core();
        var project = SeedProject(core);
        var first = MapService.CreateMap(core, project.ProjectId, "M1", 10L);

        // 篡改计划：新 Map/新根历史/新配置，但 default Calendar 复用既有 CalendarId
        // → 第 3 条语句违反 UNIQUE(project_id, calendar_id)，事务必须整体回滚。
        var newMapId = Guid.NewGuid();
        var newRootHistoryId = Guid.NewGuid();
        var tampered = first with
        {
            Map = first.Map with { MapId = newMapId, MapName = "M2" },
            RootHistory = first.RootHistory with { HistoryId = newRootHistoryId, OwnerMapId = newMapId },
            DefaultCalendar = first.DefaultCalendar with { MapId = newMapId }, // CalendarId 保留 → 唯一冲突
            DayZeroConfiguration = first.DayZeroConfiguration with
            {
                CalendarConfigurationId = Guid.NewGuid(),
                MapId = newMapId,
                OwnerHistoryId = newRootHistoryId,
            },
        };

        Assert.Throws<SqliteException>(() => core.CreateMapStructure(tampered));

        // 回滚后与操作前完全一致：仍只有 first 的结构，不留半成品。
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_map")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_history")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar")));
        Assert.Equal(1, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_calendar_configuration")));
        Assert.Null(core.FindMap(project.ProjectId, newMapId));

        // 事务资源已释放：同一连接上可继续成功写入。
        MapService.CreateMap(core, project.ProjectId, "M3", 20L);
        Assert.Equal(2, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_map")));
    }

    [Fact]
    public async Task Transaction_InProgress_BranchOwnedHistoriesAlwaysPairedWithBranch()
    {
        // 事务隔离边界（可观察形式）：写事务进行中，其他读不可能观察到
        // “history 已插入、branch 未插入”的中间态。两个 COUNT 必须在同一快照内
        //（InSnapshotScope 单临界区），否则写提交落在两次读之间会伪造失败。
        var core = Core();
        var project = SeedProject(core);
        var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);

        const int branches = 60;
        var barrier = new Barrier(2);
        var writer = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < branches; i++)
            {
                BranchService.CreateBranch(
                    core, project.ProjectId, mapPlan.RootHistory.HistoryId, $"B{i}", new HistoricalTime(i, 0), 100L + i);
            }
        });
        var reader = Task.Run(() =>
        {
            barrier.SignalAndWait();
            var readerCore = new SqliteCoreDomainDatabase(_database);
            for (var observed = 0; observed < 500; observed++)
            {
                (long Histories, long Branches) pair = readerCore.InSnapshotScope(_ => (
                    Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_history WHERE history_kind = 'branch_owned'")),
                    Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_branch"))));
                Assert.Equal(pair.Histories, pair.Branches); // 成对不变量
                if (pair.Branches == branches)
                {
                    break;
                }
            }
        });

        await Task.WhenAll(writer, reader).ConfigureAwait(false);
        Assert.Equal(branches, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_branch")));
    }

    [Fact]
    public void Transaction_RollbackOnDuplicateProject_LeavesDatabaseIdentical()
    {
        var core = Core();
        var project = SeedProject(core);
        MapService.CreateMap(core, project.ProjectId, "M", 10L);
        var before = SnapshotAllCounts();

        Assert.Throws<CoreDomainDataException>(
            () => core.InsertProject(ProjectCreationPlan.Create(ProjectRules.CreateProject("second", 2L))));

        Assert.Equal(before, SnapshotAllCounts());
    }

    private Dictionary<string, long> SnapshotAllCounts()
    {
        return Access().Execute(connection =>
        {
            var counts = new Dictionary<string, long>();
            foreach (var table in new[]
                     {
                         "core_project", "core_map", "core_history", "core_branch",
                         "core_calendar", "core_calendar_configuration", "schema_migration",
                         "core_object_type", "core_object", "core_object_definition",
                         "core_object_id_allocator", "core_object_id_reservation",
                         "core_map_object_ref", "core_map_initial_state", "core_map_initial_object",
                         "core_asset", "core_object_definition_asset",
                     })
            {
                counts[table] = Scalar(connection, $"SELECT COUNT(*) FROM {table}");
            }

            return counts;
        });
    }

    // ---- 5.3 Dispose Race Tests ----

    [Fact]
    public async Task Dispose_Afterwards_AdapterOperationsRefusedWithCoordinatorException()
    {
        var core = Core();
        var project = SeedProject(core);
        var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);
        BranchService.CreateBranch(
            core, project.ProjectId, mapPlan.RootHistory.HistoryId, "B", HistoricalTime.Epoch, 20L);

        var mapQuery = new CoreDomainMapQueryService(_database);
        var resolver = new CoreDomainWorkspaceContextResolver(_database);
        await _database.DisposeAsync().ConfigureAwait(false);

        // 会话关闭后新数据库操作被拒绝：协调器入口 ObjectDisposedException
        //（ObjectName=SqliteConnectionAccess，非原生 SQLitePCL.sqlite3 失控异常）。
        var queryEx = Assert.Throws<ObjectDisposedException>(
            () => mapQuery.ListMapsAsync(project.ProjectId).GetAwaiter().GetResult());
        Assert.Equal("SqliteConnectionAccess", queryEx.ObjectName);
        var resolveEx = Assert.Throws<ObjectDisposedException>(
            () => resolver.ResolveAsync(project.ProjectId, mapPlan.Map.MapId, null, null).GetAwaiter().GetResult());
        Assert.Equal("SqliteConnectionAccess", resolveEx.ObjectName);
        var coreEx = Assert.Throws<ObjectDisposedException>(() => core.FindProject(project.ProjectId));
        Assert.Equal("SqliteConnectionAccess", coreEx.ObjectName);

        // 重复关闭为幂等空操作；已提交数据在文件中完好。
        await _database.DisposeAsync().ConfigureAwait(false);
        await using (var verify = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath))
        {
            await verify.OpenAsync().ConfigureAwait(false);
            Assert.True(await verify.IntegrityCheckAsync().ConfigureAwait(false));
            Assert.Single(new SqliteCoreDomainDatabase(verify).ListMaps(project.ProjectId));
        }
    }

    [Fact]
    public async Task Dispose_DuringInFlightOperations_OutcomesAreControlledAndFileStaysIntact()
    {
        // 复现实验场景 3 同构：读/写在飞时关闭。允许的结果仅有两种——
        // 操作完整完成，或协调器入口拒绝；不允许任何 SqliteException /
        // 原生句柄级异常；库文件最终通过完整性检查且无半成品写入。
        var core = Core();
        var project = SeedProject(core);
        var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);

        const int workers = 4;
        const int rounds = 25;
        var barrier = new Barrier(workers + 1);
        var exceptions = new List<Exception>();

        var workersTasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var workerCore = new SqliteCoreDomainDatabase(_database);
            var workerQuery = new CoreDomainMapQueryService(_database);
            for (var i = 0; i < rounds; i++)
            {
                var failure = Capture(() =>
                {
                    if (i % 2 == 0)
                    {
                        BranchService.CreateBranch(
                            workerCore, project.ProjectId, mapPlan.RootHistory.HistoryId,
                            $"w{w}-{i}", new HistoricalTime(i, w), 1000L + i);
                    }
                    else
                    {
                        _ = workerQuery.ListMapsAsync(project.ProjectId).GetAwaiter().GetResult();
                    }
                });
                if (failure is not null)
                {
                    lock (exceptions) exceptions.Add(failure);
                }
            }
        })).ToArray();
        var disposer = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await _database.DisposeAsync().ConfigureAwait(false);
        });

        await Task.WhenAll(workersTasks.Append(disposer).ToArray()).ConfigureAwait(false);

        Assert.All(exceptions, AssertDefinedOutcome); // 只允许入口拒绝

        // 库文件完好，且写事务原子性保证无半成品：branch 行与 branch_owned history 一一对应。
        await using (var verify = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath))
        {
            await verify.OpenAsync().ConfigureAwait(false);
            Assert.True(await verify.IntegrityCheckAsync().ConfigureAwait(false));
            var access = AccessOf(verify);
            var branches = access.Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_branch"));
            // 成功提交的写数量取决于 dispose 与写者的交错（0..workers*rounds/2 均合法），
            // 但任何已提交的写必须完整（branch 行与 branch_owned history 一一对应）。
            Assert.True(branches is >= 0 and <= workers * ((rounds + 1) / 2), $"branches={branches}");
            Assert.Equal(
                branches,
                access.Execute(c => Scalar(c, "SELECT COUNT(*) FROM core_history WHERE history_kind = 'branch_owned'")));
        }
    }

    [Fact]
    public async Task Dispose_ConcurrentDoubleDispose_CompletesWithoutError()
    {
        var core = Core();
        _ = SeedProject(core);

        var barrier = new Barrier(2);
        var first = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await _database.DisposeAsync().ConfigureAwait(false);
        });
        var second = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await _database.DisposeAsync().ConfigureAwait(false);
        });
        await Task.WhenAll(first, second).ConfigureAwait(false); // 均不得抛出

        await using (var verify = (SqlitePackageDatabase)_factory.CreateForFile(_database.DatabasePath))
        {
            await verify.OpenAsync().ConfigureAwait(false);
            Assert.True(await verify.IntegrityCheckAsync().ConfigureAwait(false));
        }
    }

    // ---- 5.4 Migration Tests ----

    private sealed class InlineMigrationSource : IMigrationSource
    {
        private readonly ModuleMigration _migration;

        public InlineMigrationSource(ModuleMigration migration) => _migration = migration;

        public IReadOnlyList<ModuleMigration> DiscoverMigrations() => new[] { _migration };
    }

    [Fact]
    public async Task Migration_PendingApplied_ConcurrentlyWithCoreReads_BothSucceed()
    {
        // 迁移入口与其他数据库操作并发：同一数据库实例上全部经同一协调器，
        // 迁移事务不可能被其他操作切入。暂存库已应用 core-domain 全部 4 条迁移，
        // 再以“core-domain + 1 条自定义迁移”的完整来源做校验 → 恰有 1 条 pending，
        // 其应用与 Core 读并发执行。
        var extra = new ModuleMigration
        {
            ModuleId = "cd01-f1-test",
            MigrationId = "001_extra",
            Checksum = "cafe01",
            SqlScript = "CREATE TABLE cd_f1_extra (id INTEGER PRIMARY KEY);",
        };
        var staged = CreateInitializedDatabase("staged.sqlite");
        try
        {
            var core = new SqliteCoreDomainDatabase(staged);
            var project = ProjectRules.CreateProject("P", 1L);
            core.InsertProject(ProjectCreationPlan.Create(project));
            var mapPlan = MapService.CreateMap(core, project.ProjectId, "M", 10L);
            BranchService.CreateBranch(
                core, project.ProjectId, mapPlan.RootHistory.HistoryId, "B", HistoricalTime.Epoch, 20L);

            var fullBootstrapper = new ApplicationBootstrapper(new IMigrationSource[]
            {
                new CoreDomainMigrationSource(), new InlineMigrationSource(extra),
            });
            var barrier = new Barrier(2);
            var migrate = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await CaptureAsync(async () =>
                {
                    var result = await fullBootstrapper.ValidateExistingDatabaseAsync(staged).ConfigureAwait(false);
                    Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
                    Assert.Single(result.AppliedMigrations); // 仅 001_extra
                }).ConfigureAwait(false);
            });
            var read = Task.Run(() =>
            {
                barrier.SignalAndWait();
                Exception? failure = null;
                var reader = new CoreDomainMapQueryService(staged);
                for (var i = 0; i < 80 && failure is null; i++)
                {
                    failure = Capture(() =>
                        Assert.Single(reader.ListMapsAsync(project.ProjectId).GetAwaiter().GetResult()));
                }

                return failure;
            });

            var failures = (await Task.WhenAll(migrate, read).ConfigureAwait(false))
                .Where(f => f is not null).ToList();
            Assert.Empty(failures);

            var ledger = await staged.ReadLedgerAsync().ConfigureAwait(false);
            Assert.Equal(11, ledger.Count); // 10 core-domain + 1 extra，无重复应用
            Assert.Equal(1, AccessOf(staged).Execute(c => Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'cd_f1_extra'")));
            Assert.True(await staged.IntegrityCheckAsync().ConfigureAwait(false));
        }
        finally
        {
            await staged.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Migration_FailingScript_RollsBackSchemaAndLedger()
    {
        var failing = new ModuleMigration
        {
            ModuleId = "test-module",
            MigrationId = "001_bad",
            Checksum = "deadbeef",
            SqlScript = "CREATE TABLE cd_f1_probe (id INTEGER PRIMARY KEY); CREATE TABLE cd_f1_probe (id INTEGER PRIMARY KEY);",
        };
        var database = CreateLedgerOnlyDatabase("failing.sqlite");
        try
        {
            var bootstrapper = new ApplicationBootstrapper(new[] { new InlineMigrationSource(failing) });
            var result = await bootstrapper.ValidateExistingDatabaseAsync(database).ConfigureAwait(false);
            Assert.False(result.Success);
            Assert.Contains(result.Issues, i => i.Code == PackageIssueCode.InternalError);

            var access = AccessOf(database);
            Assert.Equal(0, access.Execute(c => Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'cd_f1_probe'")));
            Assert.Equal(0, access.Execute(c => Scalar(c, "SELECT COUNT(*) FROM schema_migration WHERE module_id = 'test-module'")));

            // 回滚后连接可用：core-domain 正式迁移仍可正常补齐（schema 版本正确）。
            var repair = await _bootstrapper.ValidateExistingDatabaseAsync(database).ConfigureAwait(false);
            Assert.True(repair.Success, string.Join("; ", repair.Issues.Select(i => i.Message)));
            Assert.Equal(10, repair.AppliedMigrations.Count);
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Migration_TwoConnectionsSameFile_ConcurrentInitialize_AppliedExactlyOnce()
    {
        var first = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "race.sqlite"));
        var second = (SqlitePackageDatabase)_factory.CreateForFile(Path.Combine(_root, "race.sqlite"));
        await first.OpenAsync().ConfigureAwait(false);
        await second.OpenAsync().ConfigureAwait(false);
        try
        {
            var barrier = new Barrier(2);
            var results = new BootstrapResult[2];
            var t1 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                results[0] = await _bootstrapper.InitializeNewDatabaseAsync(first, DateTimeOffset.UtcNow).ConfigureAwait(false);
            });
            var t2 = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                results[1] = await _bootstrapper.InitializeNewDatabaseAsync(second, DateTimeOffset.UtcNow).ConfigureAwait(false);
            });
            await Task.WhenAll(t1, t2).ConfigureAwait(false);

            // 跨连接（文件级）竞争：ledger 主键 + SQLite 文件锁保证每条迁移恰好应用一次；
            // 双成功（后者读到 0 pending）或一成功一 fail-closed 均为合法结局。
            Assert.Contains(results, r => r.Success);

            await using var verify = (SqlitePackageDatabase)_factory.CreateForFile(first.DatabasePath);
            await verify.OpenAsync().ConfigureAwait(false);
            Assert.True(await verify.IntegrityCheckAsync().ConfigureAwait(false));
            var access = AccessOf(verify);
            Assert.Equal(10, access.Execute(c => Scalar(c, "SELECT COUNT(*) FROM schema_migration")));
            Assert.Equal(10, access.Execute(
                c => Scalar(c, "SELECT COUNT(DISTINCT migration_id) FROM schema_migration")));
        }
        finally
        {
            await first.DisposeAsync().ConfigureAwait(false);
            await second.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Migration_SecondValidation_AppliesNothing()
    {
        var result = await _bootstrapper.ValidateExistingDatabaseAsync(_database).ConfigureAwait(false);
        Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(10, Access().Execute(c => Scalar(c, "SELECT COUNT(*) FROM schema_migration")));
    }

    // ---- 辅助 ----

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }
}
