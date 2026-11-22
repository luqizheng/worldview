using System;
using System.Collections.Generic;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using Microsoft.Data.Sqlite;
using WorldView.Application.Abstractions;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// 向 Core Domain 适配器暴露连接所有者的统一访问协调器（仅限本程序集；真实适配器
/// 要求真实 <see cref="SqlitePackageDatabase"/>，不接受测试替身冒充正式持久化）。
/// CD-01-F1：适配器不再各自持有实例级锁——全部数据库访问经
/// <see cref="SqliteConnectionAccess"/> 的同一临界区串行执行。
/// </summary>
internal interface ISqliteConnectionProvider
{
    /// <summary>本数据库的统一连接访问协调器；未打开时由协调器在进入时拒绝。</summary>
    SqliteConnectionAccess GetAccess();
}

/// <summary>
/// <see cref="ICoreDomainDatabase"/> 的 Microsoft.Data.Sqlite 实现。
/// 复用 Application Bootstrap 打开的同一物理连接（PRAGMA foreign_keys=ON、
/// Pool=False、busy_timeout 有界；P2-09-12 V0.6 §3 provider 目标）。
/// 并发模型（CD-01-F1 V1）：所有读写经连接所有者的
/// <see cref="SqliteConnectionAccess"/> 统一串行——本类自身不持锁；多个本类
/// 实例（各适配器）包装同一连接时共享同一协调器，不存在互不相干的锁域。
/// 结构写操作各自单事务（脚本级原子，失败整体回滚；事务整体位于协调器
/// 临界区内）；行数据不可解析时抛 <see cref="CoreDomainDataException"/>
/// fail-closed。会话关闭后访问以 <see cref="ObjectDisposedException"/> 拒绝。
/// </summary>
public sealed partial class SqliteCoreDomainDatabase : ICoreDomainDatabase
{
    private readonly SqliteConnectionAccess _access;

    public SqliteCoreDomainDatabase(IPackageDatabase database)
    {
        if (database is not ISqliteConnectionProvider provider)
        {
            throw new ArgumentException(
                "Core Domain 持久化需要真实 SQLite 包数据库（SqlitePackageDatabase），不接受替身。",
                nameof(database));
        }

        _access = provider.GetAccess();

        // Bootstrap owns the immutable legacy baseline (001-010). EF Core owns all
        // post-baseline schema evolution and applies its own tracked migrations here.
        new CoreDomainEfSession(_access).ApplyMigrations();
    }

    /// <summary>
    /// 在统一临界区内执行一段多步 Core Domain 工作（CD-01-F1）。
    /// <see cref="SqliteConnectionAccess.Execute{T}"/> 的 Monitor 锁同线程可重入：
    /// work 内经本实例的嵌套读写调用安全且仍被同一临界区覆盖，因此
    /// 读-校验-读 之类的多步序列对外表现为单一原子快照（V1 无删除/更新路径，
    /// 该快照与写入串行化共同排除撕裂读）。供适配器组合 Domain 规则时使用。
    /// </summary>
    internal T InSnapshotScope<T>(Func<ICoreDomainDatabase, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return _access.Execute(_ => work(this));
    }

    public ProjectRecord? FindProject(Guid projectId)
    {
        const string sql = """
            SELECT project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_project
            WHERE project_id = $projectId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapProject(reader) : null;
        });
    }

    public IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows()
    {
        const string sql = """
            SELECT project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_project
            ORDER BY rowid;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<RawProjectIdentityRow>();
            while (reader.Read())
            {
                rows.Add(new RawProjectIdentityRow
                {
                    ProjectIdText = reader.GetString(0),
                    ProjectName = reader.GetString(1),
                    ProjectFormatVersion = reader.GetInt64(2),
                    CreatedAtUtcMs = reader.GetInt64(3),
                    UpdatedAtUtcMs = reader.GetInt64(4),
                });
            }

            return (IReadOnlyList<RawProjectIdentityRow>)rows;
        });
    }

    public void InsertProject(ProjectCreationPlan plan)
    {
        var project = plan.Project;
        const string sql = """
            INSERT INTO core_project
                (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ($projectId, $name, $formatVersion, $createdAt, $updatedAt);
            """;
        const string typeSql = """
            INSERT INTO core_object_type
                (object_type_id, project_id, type_key, display_name, allocatable,
                 allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ($typeId, $projectId, $typeKey, $displayName, $allocatable,
                    $formatJson, $formatVersion, $createdAt, $updatedAt);
            """;
        const string allocatorSql = """
            INSERT INTO core_object_id_allocator
                (project_id, object_type_id, next_sequence, allocator_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ($projectId, $typeId, 1, $formatVersion, $createdAt, $updatedAt);
            """;
        // 单个临界区覆盖“计数检查 + 身份/类型/allocator 插入 + 提交”全程（CD-01-F1）；
        // OD-4 建议案：Project 身份与内置类型种子同事务（避免 Project 存在而类型行缺失的窗口）。
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // 单行规则（一库一项目）在事务内检查，杜绝并发窗口。
                using (var count = connection.CreateCommand())
                {
                    count.Transaction = transaction;
                    count.CommandText = "SELECT COUNT(*) FROM core_project;";
                    var existing = (long)(count.ExecuteScalar() ?? 0L);
                    if (existing > 0)
                    {
                        throw new CoreDomainDataException("数据库已存在项目身份（一库一项目），拒绝再次创建。");
                    }
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = sql;
                    insert.Parameters.AddWithValue("$projectId", Uuid(project.ProjectId));
                    insert.Parameters.AddWithValue("$name", project.ProjectName);
                    insert.Parameters.AddWithValue("$formatVersion", project.ProjectFormatVersion);
                    insert.Parameters.AddWithValue("$createdAt", project.CreatedAtUtcMs);
                    insert.Parameters.AddWithValue("$updatedAt", project.UpdatedAtUtcMs);
                    insert.ExecuteNonQuery();
                }

                foreach (var type in plan.BuiltinObjectTypes)
                {
                    using (var insertType = connection.CreateCommand())
                    {
                        insertType.Transaction = transaction;
                        insertType.CommandText = typeSql;
                        insertType.Parameters.AddWithValue("$typeId", Uuid(type.ObjectTypeId));
                        insertType.Parameters.AddWithValue("$projectId", Uuid(type.ProjectId));
                        insertType.Parameters.AddWithValue("$typeKey", type.TypeKey);
                        insertType.Parameters.AddWithValue("$displayName", type.DisplayName);
                        insertType.Parameters.AddWithValue("$allocatable", type.Allocatable ? 1L : 0L);
                        insertType.Parameters.AddWithValue("$formatJson", type.AllocationFormatJson);
                        insertType.Parameters.AddWithValue("$formatVersion", type.AllocationFormatVersion);
                        insertType.Parameters.AddWithValue("$createdAt", type.CreatedAtUtcMs);
                        insertType.Parameters.AddWithValue("$updatedAt", type.UpdatedAtUtcMs);
                        insertType.ExecuteNonQuery();
                    }

                    if (type.Allocatable)
                    {
                        using var insertAllocator = connection.CreateCommand();
                        insertAllocator.Transaction = transaction;
                        insertAllocator.CommandText = allocatorSql;
                        insertAllocator.Parameters.AddWithValue("$projectId", Uuid(type.ProjectId));
                        insertAllocator.Parameters.AddWithValue("$typeId", Uuid(type.ObjectTypeId));
                        insertAllocator.Parameters.AddWithValue("$formatVersion", ObjectAllocationFormat.VersionMinimalDecimal);
                        insertAllocator.Parameters.AddWithValue("$createdAt", type.CreatedAtUtcMs);
                        insertAllocator.Parameters.AddWithValue("$updatedAt", type.UpdatedAtUtcMs);
                        insertAllocator.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public MapRecord? FindMap(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms
            FROM core_map
            WHERE project_id = $projectId AND map_id = $mapId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$mapId", Uuid(mapId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapMap(reader) : null;
        });
    }

    public IReadOnlyList<MapRecord> ListMaps(Guid projectId)
    {
        const string sql = """
            SELECT map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms
            FROM core_map
            WHERE project_id = $projectId
            ORDER BY created_at_utc_ms, map_id;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            var maps = new List<MapRecord>();
            while (reader.Read())
            {
                maps.Add(MapMap(reader));
            }

            return (IReadOnlyList<MapRecord>)maps;
        });
    }

    public void CreateMapStructure(MapCreationPlan plan)
    {
        // 依赖序写入：map → root history → calendar → configuration（P2-09-11 §4.1
        // 不变量，单事务；事务整体位于统一临界区内，CD-01-F1）。
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                Execute(transaction, """
                    INSERT INTO core_map (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($mapId, $projectId, $name, $status, $createdAt, $updatedAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$mapId", Uuid(plan.Map.MapId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.Map.ProjectId));
                        c.Parameters.AddWithValue("$name", plan.Map.MapName);
                        c.Parameters.AddWithValue("$status", plan.Map.LifecycleStatus);
                        c.Parameters.AddWithValue("$createdAt", plan.Map.CreatedAtUtcMs);
                        c.Parameters.AddWithValue("$updatedAt", plan.Map.UpdatedAtUtcMs);
                    });

                Execute(transaction, """
                    INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                    VALUES ($historyId, $projectId, 'map_root', $ownerMapId, $createdAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$historyId", Uuid(plan.RootHistory.HistoryId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.RootHistory.ProjectId));
                        c.Parameters.AddWithValue("$ownerMapId", Uuid(plan.RootHistory.OwnerMapId!.Value));
                        c.Parameters.AddWithValue("$createdAt", plan.RootHistory.CreatedAtUtcMs);
                    });

                Execute(transaction, """
                    INSERT INTO core_calendar (calendar_id, project_id, map_id, calendar_role, calendar_type, calendar_name, created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($calendarId, $projectId, $mapId, 'default', $calendarType, $name, $createdAt, $updatedAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$calendarId", Uuid(plan.DefaultCalendar.CalendarId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.DefaultCalendar.ProjectId));
                        c.Parameters.AddWithValue("$mapId", Uuid(plan.DefaultCalendar.MapId));
                        c.Parameters.AddWithValue("$calendarType", (object?)plan.DefaultCalendar.CalendarType ?? DBNull.Value);
                        c.Parameters.AddWithValue("$name", plan.DefaultCalendar.CalendarName);
                        c.Parameters.AddWithValue("$createdAt", plan.DefaultCalendar.CreatedAtUtcMs);
                        c.Parameters.AddWithValue("$updatedAt", plan.DefaultCalendar.UpdatedAtUtcMs);
                    });

                Execute(transaction, """
                    INSERT INTO core_calendar_configuration
                        (calendar_configuration_id, project_id, map_id, calendar_id, owner_history_id, source_command_id,
                         configuration_origin, effective_from_day_number, effective_from_offset_seconds,
                         effective_until_day_number, effective_until_offset_seconds,
                         rules_schema_version, rules_payload_json, created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($configId, $projectId, $mapId, $calendarId, $historyId, $sourceCommandId,
                            'user_created', $fromDay, $fromOffset, $untilDay, $untilOffset,
                            $rulesVersion, $rulesJson, $createdAt, $updatedAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$configId", Uuid(plan.DayZeroConfiguration.CalendarConfigurationId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.DayZeroConfiguration.ProjectId));
                        c.Parameters.AddWithValue("$mapId", Uuid(plan.DayZeroConfiguration.MapId));
                        c.Parameters.AddWithValue("$calendarId", Uuid(plan.DayZeroConfiguration.CalendarId));
                        c.Parameters.AddWithValue("$historyId", Uuid(plan.DayZeroConfiguration.OwnerHistoryId));
                        c.Parameters.AddWithValue("$sourceCommandId", (object?)plan.DayZeroConfiguration.SourceCommandId?.ToString("D") ?? DBNull.Value);
                        c.Parameters.AddWithValue("$fromDay", plan.DayZeroConfiguration.EffectiveFrom.DayNumber);
                        c.Parameters.AddWithValue("$fromOffset", plan.DayZeroConfiguration.EffectiveFrom.OffsetSeconds);
                        c.Parameters.AddWithValue("$untilDay", (object?)plan.DayZeroConfiguration.EffectiveUntil?.DayNumber ?? DBNull.Value);
                        c.Parameters.AddWithValue("$untilOffset", (object?)plan.DayZeroConfiguration.EffectiveUntil?.OffsetSeconds ?? DBNull.Value);
                        c.Parameters.AddWithValue("$rulesVersion", plan.DayZeroConfiguration.RulesSchemaVersion);
                        c.Parameters.AddWithValue("$rulesJson", plan.DayZeroConfiguration.RulesPayloadJson);
                        c.Parameters.AddWithValue("$createdAt", plan.DayZeroConfiguration.CreatedAtUtcMs);
                        c.Parameters.AddWithValue("$updatedAt", plan.DayZeroConfiguration.UpdatedAtUtcMs);
                    });

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public HistoryRecord? FindHistory(Guid projectId, Guid historyId)
    {
        const string sql = """
            SELECT history_id, project_id, history_kind, owner_map_id, created_at_utc_ms
            FROM core_history
            WHERE project_id = $projectId AND history_id = $historyId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$historyId", Uuid(historyId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapHistory(reader) : null;
        });
    }

    public HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT history_id, project_id, history_kind, owner_map_id, created_at_utc_ms
            FROM core_history
            WHERE project_id = $projectId AND history_kind = 'map_root' AND owner_map_id = $mapId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$mapId", Uuid(mapId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapHistory(reader) : null;
        });
    }

    public BranchRecord? FindBranch(Guid projectId, Guid branchId)
    {
        const string sql = """
            SELECT branch_id, project_id, parent_history_id, own_history_id,
                   parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                   created_at_utc_ms, updated_at_utc_ms
            FROM core_branch
            WHERE project_id = $projectId AND branch_id = $branchId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$branchId", Uuid(branchId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapBranch(reader) : null;
        });
    }

    public BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId)
    {
        const string sql = """
            SELECT branch_id, project_id, parent_history_id, own_history_id,
                   parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                   created_at_utc_ms, updated_at_utc_ms
            FROM core_branch
            WHERE project_id = $projectId AND own_history_id = $ownHistoryId;
            """;
        return _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$ownHistoryId", Uuid(ownHistoryId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapBranch(reader) : null;
        });
    }

    public void CreateBranchStructure(BranchCreationPlan plan)
    {
        // 依赖序写入：branch_owned history → branch（同事务；core_history 不回指
        // core_branch）；事务整体位于统一临界区内（CD-01-F1）。
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                Execute(transaction, """
                    INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                    VALUES ($historyId, $projectId, 'branch_owned', NULL, $createdAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$historyId", Uuid(plan.OwnHistory.HistoryId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.OwnHistory.ProjectId));
                        c.Parameters.AddWithValue("$createdAt", plan.OwnHistory.CreatedAtUtcMs);
                    });

                Execute(transaction, """
                    INSERT INTO core_branch
                        (branch_id, project_id, parent_history_id, own_history_id,
                         parent_day_number, parent_offset_seconds, branch_name, lifecycle_status,
                         created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($branchId, $projectId, $parentHistoryId, $ownHistoryId,
                            $parentDay, $parentOffset, $name, $status, $createdAt, $updatedAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$branchId", Uuid(plan.Branch.BranchId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.Branch.ProjectId));
                        c.Parameters.AddWithValue("$parentHistoryId", Uuid(plan.Branch.ParentHistoryId));
                        c.Parameters.AddWithValue("$ownHistoryId", Uuid(plan.Branch.OwnHistoryId));
                        c.Parameters.AddWithValue("$parentDay", plan.Branch.ParentAt.DayNumber);
                        c.Parameters.AddWithValue("$parentOffset", plan.Branch.ParentAt.OffsetSeconds);
                        c.Parameters.AddWithValue("$name", plan.Branch.BranchName);
                        c.Parameters.AddWithValue("$status", plan.Branch.LifecycleStatus);
                        c.Parameters.AddWithValue("$createdAt", plan.Branch.CreatedAtUtcMs);
                        c.Parameters.AddWithValue("$updatedAt", plan.Branch.UpdatedAtUtcMs);
                    });

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    // ---- 事务与行映射辅助 ----

    private static void Execute(
        SqliteTransaction scope,
        string sql,
        Action<SqliteCommand> configure)
    {
        using var command = scope.Connection!.CreateCommand();
        command.Transaction = scope;
        command.CommandText = sql;
        configure(command);
        command.ExecuteNonQuery();
    }

    private static string Uuid(Guid value) => value.ToString("D");

    private static Guid ParseUuid(string text) =>
        Guid.TryParseExact(text, "D", out var value)
            ? value
            : throw new CoreDomainDataException($"UUID 文本 \"{text}\" 不可解析（预期 canonical 小写连字符格式）。");

    private static ProjectRecord MapProject(SqliteDataReader reader) => new()
    {
        ProjectId = ParseUuid(reader.GetString(0)),
        ProjectName = reader.GetString(1),
        ProjectFormatVersion = reader.GetInt64(2),
        CreatedAtUtcMs = reader.GetInt64(3),
        UpdatedAtUtcMs = reader.GetInt64(4),
    };

    private static MapRecord MapMap(SqliteDataReader reader) => new()
    {
        MapId = ParseUuid(reader.GetString(0)),
        ProjectId = ParseUuid(reader.GetString(1)),
        MapName = reader.GetString(2),
        LifecycleStatus = reader.GetString(3),
        CreatedAtUtcMs = reader.GetInt64(4),
        UpdatedAtUtcMs = reader.GetInt64(5),
    };

    private static HistoryRecord MapHistory(SqliteDataReader reader)
    {
        var kindText = reader.GetString(2);
        var kind = kindText switch
        {
            "map_root" => HistoryKind.MapRoot,
            "branch_owned" => HistoryKind.BranchOwned,
            _ => throw new CoreDomainDataException($"history_kind \"{kindText}\" 不可识别。"),
        };
        Guid? ownerMap = reader.IsDBNull(3) ? null : ParseUuid(reader.GetString(3));
        if ((kind == HistoryKind.MapRoot && ownerMap is null)
            || (kind == HistoryKind.BranchOwned && ownerMap is not null))
        {
            throw new CoreDomainDataException($"历史 {reader.GetString(0)} 的 kind/owner 组合违反不变量。");
        }

        return new HistoryRecord
        {
            HistoryId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            Kind = kind,
            OwnerMapId = ownerMap,
            CreatedAtUtcMs = reader.GetInt64(4),
        };
    }

    private static BranchRecord MapBranch(SqliteDataReader reader)
    {
        var day = reader.GetInt64(4);
        var offset = reader.GetInt32(5);
        if (!HistoricalTime.TryCreate(day, offset, out var parentAt) || parentAt is null)
        {
            throw new CoreDomainDataException($"分支 {reader.GetString(0)} 的 parent_at ({day}:{offset}) 越界。");
        }

        return new BranchRecord
        {
            BranchId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            ParentHistoryId = ParseUuid(reader.GetString(2)),
            OwnHistoryId = ParseUuid(reader.GetString(3)),
            ParentAt = parentAt,
            BranchName = reader.GetString(6),
            LifecycleStatus = reader.GetString(7),
            CreatedAtUtcMs = reader.GetInt64(8),
            UpdatedAtUtcMs = reader.GetInt64(9),
        };
    }
}
