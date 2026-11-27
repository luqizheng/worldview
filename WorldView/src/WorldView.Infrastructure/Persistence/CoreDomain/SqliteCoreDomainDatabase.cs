using System;
using System.Collections.Generic;
using System.Linq;
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
        _access.ApplyCoreDomainMigrationsOnce(_ef.ApplyMigrations);
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



    public IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows() =>
        _ef.Execute(db => (IReadOnlyList<RawProjectIdentityRow>)db.Projects.AsNoTracking()
            .OrderBy(x => x.ProjectId)
            .Select(x => new RawProjectIdentityRow
            {
                ProjectIdText = x.ProjectId.ToString("D"),
                ProjectName = x.ProjectName,
                ProjectFormatVersion = x.ProjectFormatVersion,
                CreatedAtUtcMs = x.CreatedAtUtcMs,
                UpdatedAtUtcMs = x.UpdatedAtUtcMs,
            })
            .ToArray());

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
                MapSpaceId = x.MapSpaceId,
                SpatialReferenceId = x.SpatialReferenceId,
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
                // WP-20a 名称唯一性（权威写路径约束，语义归 MapRules）：同 Project 内
                // 名称唯一（MapRules.MapNameComparer，序数大小写不敏感）。SQLite 唯一索引
                // 的 BINARY/NOCASE 排序无法表达该语义（NOCASE 仅折叠 ASCII），故在创建
                // 事务内读回同 Project 的名称做内存比较；事务整体位于连接协调器临界区内，
                // 校验与写入对进程内全部写路径原子（单机 V1 约束，见端口契约）。
                var existingNames = db.Maps
                    .Where(x => x.ProjectId == plan.Map.ProjectId)
                    .Select(x => x.MapName)
                    .ToArray();
                if (existingNames.Any(n => MapRules.MapNameComparer.Equals(n, plan.Map.MapName)))
                {
                    throw new CoreDomainConflictException(
                        $"同一项目内已存在同名地图「{plan.Map.MapName}」（名称唯一，不区分大小写）。");
                }

                // Map 与其不可变坐标框架属于同一个创建事务。先落定义以满足 FK，
                // 后续任一步失败都会回滚定义、Map、History 与 Calendar 全部结构。
                var spatialReference = SpatialReferenceRules.ValidateDefaultLocalCartesian(plan.DefaultSpatialReference, plan.Map.MapId);
                if (spatialReference.ProjectId != plan.Map.ProjectId
                    || !string.Equals(spatialReference.SpatialReferenceId, plan.Map.SpatialReferenceId, StringComparison.Ordinal))
                    throw new CoreDomainConflictException("Map 的默认 Spatial Reference 与 Map 所属 Project/绑定 ID 不一致。");
                db.SpatialReferences.Add(new CoreSpatialReferenceEntity
                {
                    ProjectId = spatialReference.ProjectId,
                    SpatialReferenceId = spatialReference.SpatialReferenceId,
                    DefinitionVersion = spatialReference.DefinitionVersion,
                    ReferenceKind = spatialReference.Kind.ToString(),
                    CoordinateUnit = spatialReference.CoordinateUnit,
                    AxisOrder = spatialReference.AxisOrder.ToString(),
                    Dimension = spatialReference.Dimension,
                    ValidDomainJson = spatialReference.ValidDomainJson,
                    WrapBehavior = spatialReference.WrapBehavior.ToString(),
                    VerticalReferenceId = spatialReference.VerticalReferenceId,
                    PrecisionProfileId = null,
                    PrecisionProfileVersion = null,
                    PrecisionCoordinateUnit = null,
                    PrecisionAxisOrder = null,
                    NumericRepresentation = null,
                    MaximumAbsoluteQuantizationError = null,
                    RoundingMode = null,
                    MinimumAuthoringTolerance = null,
                    SmallestMeaningfulFeatureSize = null,
                    DefinitionParametersJson = spatialReference.DefinitionParametersJson,
                    MeasurementCapabilitiesJson = spatialReference.MeasurementCapabilitiesJson,
                    ProvenanceJson = spatialReference.ProvenanceJson,
                    DefinitionFingerprint = spatialReference.DefinitionFingerprint,
                    CreatedAtUtcMs = spatialReference.CreatedAtUtcMs,
                });
                db.SaveChanges();

                db.Maps.Add(new CoreMapEntity
                {
                    MapId = plan.Map.MapId,
                    ProjectId = plan.Map.ProjectId,
                    MapSpaceId = plan.Map.MapSpaceId,
                    SpatialReferenceId = plan.Map.SpatialReferenceId,
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



    private static ProjectRecord MapProject(CoreProjectEntity entity) => new()
    {
        ProjectId = entity.ProjectId,
        ProjectName = entity.ProjectName,
        ProjectFormatVersion = entity.ProjectFormatVersion,
        CreatedAtUtcMs = entity.CreatedAtUtcMs,
        UpdatedAtUtcMs = entity.UpdatedAtUtcMs,
    };

    private static MapRecord MapMap(CoreMapEntity entity) => new()
    {
        MapId = entity.MapId,
        ProjectId = entity.ProjectId,
        MapSpaceId = entity.MapSpaceId,
        SpatialReferenceId = entity.SpatialReferenceId,
        MapName = entity.MapName,
        LifecycleStatus = entity.LifecycleStatus,
        CreatedAtUtcMs = entity.CreatedAtUtcMs,
        UpdatedAtUtcMs = entity.UpdatedAtUtcMs,
    };

    private static HistoryRecord MapHistory(CoreHistoryEntity entity)
    {
        var kind = entity.history_kind switch
        {
            "map_root" => HistoryKind.MapRoot,
            "branch_owned" => HistoryKind.BranchOwned,
            _ => throw new CoreDomainDataException($"history_kind \"{entity.history_kind}\" 不可识别。"),
        };
        if ((kind == HistoryKind.MapRoot && entity.owner_map_id is null)
            || (kind == HistoryKind.BranchOwned && entity.owner_map_id is not null))
            throw new CoreDomainDataException($"历史 {entity.history_id:D} 的 kind/owner 组合违反不变量。");

        return new HistoryRecord
        {
            HistoryId = entity.history_id,
            ProjectId = entity.project_id,
            Kind = kind,
            OwnerMapId = entity.owner_map_id,
            CreatedAtUtcMs = entity.created_at_utc_ms,
        };
    }

    private static BranchRecord MapBranch(CoreBranchEntity entity)
    {
        if (entity.parent_day_number < 0 || entity.parent_offset_seconds is < 0 or > 86399
            || !HistoricalTime.TryCreate(entity.parent_day_number, (int)entity.parent_offset_seconds, out var parentAt)
            || parentAt is null)
            throw new CoreDomainDataException($"分支 {entity.branch_id:D} 的 parent_at 越界。");

        return new BranchRecord
        {
            BranchId = entity.branch_id,
            ProjectId = entity.project_id,
            ParentHistoryId = entity.parent_history_id,
            OwnHistoryId = entity.own_history_id,
            ParentAt = parentAt,
            BranchName = entity.branch_name,
            LifecycleStatus = entity.lifecycle_status,
            CreatedAtUtcMs = entity.created_at_utc_ms,
            UpdatedAtUtcMs = entity.updated_at_utc_ms,
        };
    }

}
