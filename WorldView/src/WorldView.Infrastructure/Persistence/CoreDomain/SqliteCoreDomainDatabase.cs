using System;
using System.Collections.Generic;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    private readonly CoreDomainEfSession _ef;

    public SqliteCoreDomainDatabase(IPackageDatabase database)
    {
        if (database is not ISqliteConnectionProvider provider)
        {
            throw new ArgumentException(
                "Core Domain 持久化需要真实 SQLite 包数据库（SqlitePackageDatabase），不接受替身。",
                nameof(database));
        }

        _access = provider.GetAccess();
        _ef = new CoreDomainEfSession(_access);
        _ef.ApplyMigrations();
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
        return _ef.Execute(db =>
        {
            var entity = db.Projects.AsNoTracking().SingleOrDefault(x => x.ProjectId == projectId);
            return entity is null ? null : MapProject(entity);
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
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // 一库一项目规则在事务内检查，避免并发创建窗口。
                if (db.Projects.Any())
                    throw new CoreDomainDataException("数据库已存在项目身份（一库一项目），拒绝再次创建。");

                var project = plan.Project;
                db.Projects.Add(new CoreProjectEntity
                {
                    ProjectId = project.ProjectId,
                    ProjectName = project.ProjectName,
                    ProjectFormatVersion = project.ProjectFormatVersion,
                    CreatedAtUtcMs = project.CreatedAtUtcMs,
                    UpdatedAtUtcMs = project.UpdatedAtUtcMs,
                });

                foreach (var type in plan.BuiltinObjectTypes)
                {
                    db.ObjectTypes.Add(new CoreObjectTypeEntity
                    {
                        object_type_id = type.ObjectTypeId,
                        project_id = type.ProjectId,
                        type_key = type.TypeKey,
                        display_name = type.DisplayName,
                        allocatable = type.Allocatable ? 1L : 0L,
                        allocation_format_json = type.AllocationFormatJson,
                        allocation_format_version = type.AllocationFormatVersion,
                        created_at_utc_ms = type.CreatedAtUtcMs,
                        updated_at_utc_ms = type.UpdatedAtUtcMs,
                    });

                    if (type.Allocatable)
                    {
                        db.ObjectIdAllocators.Add(new CoreObjectIdAllocatorEntity
                        {
                            project_id = type.ProjectId,
                            object_type_id = type.ObjectTypeId,
                            next_sequence = 1,
                            allocator_format_version = ObjectAllocationFormat.VersionMinimalDecimal,
                            created_at_utc_ms = type.CreatedAtUtcMs,
                            updated_at_utc_ms = type.UpdatedAtUtcMs,
                        });
                    }
                }

                db.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
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
        return _ef.Execute(db =>
        {
            var entity = db.Maps.AsNoTracking()
                .SingleOrDefault(x => x.ProjectId == projectId && x.MapId == mapId);
            return entity is null ? null : MapMap(entity);
        });
    }

    public IReadOnlyList<MapRecord> ListMaps(Guid projectId)
    {
        return _ef.Execute(db => (IReadOnlyList<MapRecord>)db.Maps.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.CreatedAtUtcMs)
            .ThenBy(x => x.MapId)
            .Select(x => new MapRecord
            {
                MapId = x.MapId,
                ProjectId = x.ProjectId,
                MapName = x.MapName,
                LifecycleStatus = x.LifecycleStatus,
                CreatedAtUtcMs = x.CreatedAtUtcMs,
                UpdatedAtUtcMs = x.UpdatedAtUtcMs,
            })
            .ToArray());
    }

    

    public void CreateMapStructure(MapCreationPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                db.Maps.Add(new CoreMapEntity
                {
                    MapId = plan.Map.MapId,
                    ProjectId = plan.Map.ProjectId,
                    MapName = plan.Map.MapName,
                    LifecycleStatus = plan.Map.LifecycleStatus,
                    CreatedAtUtcMs = plan.Map.CreatedAtUtcMs,
                    UpdatedAtUtcMs = plan.Map.UpdatedAtUtcMs,
                });
                db.Histories.Add(new CoreHistoryEntity
                {
                    history_id = plan.RootHistory.HistoryId,
                    project_id = plan.RootHistory.ProjectId,
                    history_kind = "map_root",
                    owner_map_id = plan.RootHistory.OwnerMapId,
                    created_at_utc_ms = plan.RootHistory.CreatedAtUtcMs,
                });
                db.Calendars.Add(new CoreCalendarEntity
                {
                    calendar_id = plan.DefaultCalendar.CalendarId,
                    project_id = plan.DefaultCalendar.ProjectId,
                    map_id = plan.DefaultCalendar.MapId,
                    calendar_role = "default",
                    calendar_type = plan.DefaultCalendar.CalendarType,
                    calendar_name = plan.DefaultCalendar.CalendarName,
                    created_at_utc_ms = plan.DefaultCalendar.CreatedAtUtcMs,
                    updated_at_utc_ms = plan.DefaultCalendar.UpdatedAtUtcMs,
                });
                db.CalendarConfigurations.Add(new CoreCalendarConfigurationEntity
                {
                    calendar_configuration_id = plan.DayZeroConfiguration.CalendarConfigurationId,
                    project_id = plan.DayZeroConfiguration.ProjectId,
                    map_id = plan.DayZeroConfiguration.MapId,
                    calendar_id = plan.DayZeroConfiguration.CalendarId,
                    owner_history_id = plan.DayZeroConfiguration.OwnerHistoryId,
                    source_command_id = plan.DayZeroConfiguration.SourceCommandId,
                    configuration_origin = "user_created",
                    effective_from_day_number = plan.DayZeroConfiguration.EffectiveFrom.DayNumber,
                    effective_from_offset_seconds = plan.DayZeroConfiguration.EffectiveFrom.OffsetSeconds,
                    effective_until_day_number = plan.DayZeroConfiguration.EffectiveUntil?.DayNumber,
                    effective_until_offset_seconds = plan.DayZeroConfiguration.EffectiveUntil?.OffsetSeconds,
                    rules_schema_version = plan.DayZeroConfiguration.RulesSchemaVersion,
                    rules_payload_json = plan.DayZeroConfiguration.RulesPayloadJson,
                    created_at_utc_ms = plan.DayZeroConfiguration.CreatedAtUtcMs,
                    updated_at_utc_ms = plan.DayZeroConfiguration.UpdatedAtUtcMs,
                });

                db.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
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
        return _ef.Execute(db =>
        {
            var entity = db.Histories.AsNoTracking()
                .SingleOrDefault(x => x.project_id == projectId && x.history_id == historyId);
            return entity is null ? null : MapHistory(entity);
        });
    }

    public HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId)
    {
        return _ef.Execute(db =>
        {
            var entity = db.Histories.AsNoTracking()
                .SingleOrDefault(x => x.project_id == projectId
                    && x.history_kind == "map_root" && x.owner_map_id == mapId);
            return entity is null ? null : MapHistory(entity);
        });
    }

    

    public BranchRecord? FindBranch(Guid projectId, Guid branchId)
    {
        return _ef.Execute(db =>
        {
            var entity = db.Branches.AsNoTracking()
                .SingleOrDefault(x => x.project_id == projectId && x.branch_id == branchId);
            return entity is null ? null : MapBranch(entity);
        });
    }

    public BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId)
    {
        return _ef.Execute(db =>
        {
            var entity = db.Branches.AsNoTracking()
                .SingleOrDefault(x => x.project_id == projectId && x.own_history_id == ownHistoryId);
            return entity is null ? null : MapBranch(entity);
        });
    }

    

    public void CreateBranchStructure(BranchCreationPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                db.Histories.Add(new CoreHistoryEntity
                {
                    history_id = plan.OwnHistory.HistoryId,
                    project_id = plan.OwnHistory.ProjectId,
                    history_kind = "branch_owned",
                    owner_map_id = null,
                    created_at_utc_ms = plan.OwnHistory.CreatedAtUtcMs,
                });
                db.Branches.Add(new CoreBranchEntity
                {
                    branch_id = plan.Branch.BranchId,
                    project_id = plan.Branch.ProjectId,
                    parent_history_id = plan.Branch.ParentHistoryId,
                    own_history_id = plan.Branch.OwnHistoryId,
                    parent_day_number = plan.Branch.ParentAt.DayNumber,
                    parent_offset_seconds = plan.Branch.ParentAt.OffsetSeconds,
                    branch_name = plan.Branch.BranchName,
                    lifecycle_status = plan.Branch.LifecycleStatus,
                    created_at_utc_ms = plan.Branch.CreatedAtUtcMs,
                    updated_at_utc_ms = plan.Branch.UpdatedAtUtcMs,
                });

                db.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
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
