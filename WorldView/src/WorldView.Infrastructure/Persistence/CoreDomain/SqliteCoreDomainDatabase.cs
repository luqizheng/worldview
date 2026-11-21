using System;
using System.Collections.Generic;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using Microsoft.Data.Sqlite;
using WorldView.Application.Abstractions;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// 向 Core Domain 适配器暴露底层已打开连接（仅限本程序集；真实适配器要求
/// 真实 <see cref="SqlitePackageDatabase"/>，不接受测试替身冒充正式持久化）。
/// </summary>
internal interface ISqliteConnectionProvider
{
    /// <summary>已打开的连接；未打开/已释放时抛 InvalidOperationException。</summary>
    SqliteConnection GetOpenConnection();
}

/// <summary>
/// <see cref="ICoreDomainDatabase"/> 的 Microsoft.Data.Sqlite 实现。
/// 复用 Application Bootstrap 打开的同一物理连接（PRAGMA foreign_keys=ON、
/// Pool=False、busy_timeout 有界；P2-09-12 V0.6 §3 provider 目标）。
/// 实例内串行访问（V1 单写者，对齐 P2-09-12 TX-04/TX-30 的进程内简化形态；
/// 完整 Serialized Writer Coordinator 证明属 P2-09-12 测试范围，CD-01 不声称）。
/// 结构写操作各自单事务（脚本级原子，失败整体回滚）；行数据不可解析时抛
/// <see cref="CoreDomainDataException"/> fail-closed。
/// </summary>
public sealed class SqliteCoreDomainDatabase : ICoreDomainDatabase
{
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;

    public SqliteCoreDomainDatabase(IPackageDatabase database)
    {
        if (database is not ISqliteConnectionProvider provider)
        {
            throw new ArgumentException(
                "Core Domain 持久化需要真实 SQLite 包数据库（SqlitePackageDatabase），不接受替身。",
                nameof(database));
        }

        _connection = provider.GetOpenConnection();
    }

    public ProjectRecord? FindProject(Guid projectId)
    {
        const string sql = """
            SELECT project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_project
            WHERE project_id = $projectId;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapProject(reader) : null;
        }
    }

    public IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows()
    {
        const string sql = """
            SELECT project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_project
            ORDER BY rowid;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
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

            return rows;
        }
    }

    public void InsertProject(ProjectRecord project)
    {
        const string sql = """
            INSERT INTO core_project
                (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
            VALUES ($projectId, $name, $formatVersion, $createdAt, $updatedAt);
            """;
        lock (_gate)
        {
            ExecuteInTransaction(scope =>
            {
                // 单行规则（一库一项目）在事务内检查，杜绝并发窗口。
                using (var count = _connection.CreateCommand())
                {
                    count.Transaction = scope;
                    count.CommandText = "SELECT COUNT(*) FROM core_project;";
                    var existing = (long)(count.ExecuteScalar() ?? 0L);
                    if (existing > 0)
                    {
                        throw new CoreDomainDataException("数据库已存在项目身份（一库一项目），拒绝再次创建。");
                    }
                }

                using var insert = _connection.CreateCommand();
                insert.Transaction = scope;
                insert.CommandText = sql;
                insert.Parameters.AddWithValue("$projectId", Uuid(project.ProjectId));
                insert.Parameters.AddWithValue("$name", project.ProjectName);
                insert.Parameters.AddWithValue("$formatVersion", project.ProjectFormatVersion);
                insert.Parameters.AddWithValue("$createdAt", project.CreatedAtUtcMs);
                insert.Parameters.AddWithValue("$updatedAt", project.UpdatedAtUtcMs);
                insert.ExecuteNonQuery();
            });
        }
    }

    public MapRecord? FindMap(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms
            FROM core_map
            WHERE project_id = $projectId AND map_id = $mapId;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$mapId", Uuid(mapId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapMap(reader) : null;
        }
    }

    public IReadOnlyList<MapRecord> ListMaps(Guid projectId)
    {
        const string sql = """
            SELECT map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms
            FROM core_map
            WHERE project_id = $projectId
            ORDER BY created_at_utc_ms, map_id;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            var maps = new List<MapRecord>();
            while (reader.Read())
            {
                maps.Add(MapMap(reader));
            }

            return maps;
        }
    }

    public void CreateMapStructure(MapCreationPlan plan)
    {
        lock (_gate)
        {
            // 依赖序写入：map → root history → calendar → configuration（P2-09-11 §4.1 不变量，单事务）。
            ExecuteInTransaction(scope =>
            {
                Execute(scope, """
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

                Execute(scope, """
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

                Execute(scope, """
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

                Execute(scope, """
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
            });
        }
    }

    public HistoryRecord? FindHistory(Guid projectId, Guid historyId)
    {
        const string sql = """
            SELECT history_id, project_id, history_kind, owner_map_id, created_at_utc_ms
            FROM core_history
            WHERE project_id = $projectId AND history_id = $historyId;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$historyId", Uuid(historyId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapHistory(reader) : null;
        }
    }

    public HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT history_id, project_id, history_kind, owner_map_id, created_at_utc_ms
            FROM core_history
            WHERE project_id = $projectId AND history_kind = 'map_root' AND owner_map_id = $mapId;
            """;
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$mapId", Uuid(mapId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapHistory(reader) : null;
        }
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
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$branchId", Uuid(branchId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapBranch(reader) : null;
        }
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
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$ownHistoryId", Uuid(ownHistoryId));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapBranch(reader) : null;
        }
    }

    public void CreateBranchStructure(BranchCreationPlan plan)
    {
        lock (_gate)
        {
            // 依赖序写入：branch_owned history → branch（同事务；core_history 不回指 core_branch）。
            ExecuteInTransaction(scope =>
            {
                Execute(scope, """
                    INSERT INTO core_history (history_id, project_id, history_kind, owner_map_id, created_at_utc_ms)
                    VALUES ($historyId, $projectId, 'branch_owned', NULL, $createdAt);
                    """,
                    c =>
                    {
                        c.Parameters.AddWithValue("$historyId", Uuid(plan.OwnHistory.HistoryId));
                        c.Parameters.AddWithValue("$projectId", Uuid(plan.OwnHistory.ProjectId));
                        c.Parameters.AddWithValue("$createdAt", plan.OwnHistory.CreatedAtUtcMs);
                    });

                Execute(scope, """
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
            });
        }
    }

    // ---- 事务与行映射辅助 ----

    private void ExecuteInTransaction(Action<SqliteTransaction> action)
    {
        using var transaction = _connection.BeginTransaction();
        try
        {
            action(transaction);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

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
