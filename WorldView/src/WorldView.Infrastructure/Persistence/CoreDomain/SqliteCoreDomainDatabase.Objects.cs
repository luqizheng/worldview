using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// <see cref="SqliteCoreDomainDatabase"/> 的 Object/Reservation/Registration/Initial State
/// 写路径（CD-02-B；事务边界表见 CD-02-A §13.2）。全部复合写在同一临界区内
/// Begin → 事务内重读前置状态校验（状态机 UPDATE 不依赖调用方传值）→ 写 →
/// Commit/Rollback（CD-01-F1 条件 1 响应，CD-02-A §13.3）；任一步失败整体回滚、无半成品。
/// 指纹/哈希计算调用 Domain 拥有的 <see cref="CanonicalJson"/> /
/// <see cref="InitialStateFingerprint"/>（Domain 职责，Handoff §5.7）。
/// </summary>
public sealed partial class SqliteCoreDomainDatabase
{
    // ---- Object Type（Project 级配置权威） ----

    public IReadOnlyList<ObjectTypeRecord> ListObjectTypes(Guid projectId) =>
        _ef.Execute(db => (IReadOnlyList<ObjectTypeRecord>)db.ObjectTypes.AsNoTracking()
            .Where(x => x.project_id == projectId)
            .OrderBy(x => x.type_key)
            .AsEnumerable()
            .Select(MapObjectType)
            .ToArray());

    public ObjectTypeRecord? FindObjectType(Guid projectId, Guid objectTypeId) =>
        _ef.Execute(db =>
        {
            var entity = db.ObjectTypes.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.object_type_id == objectTypeId);
            return entity is null ? null : MapObjectType(entity);
        });

    public ObjectTypeRecord? FindObjectTypeByTypeKey(Guid projectId, string typeKey)
    {
        var normalizedTypeKey = ObjectRules.NormalizeTypeKey(typeKey);
        return _ef.Execute(db =>
        {
            var entity = db.ObjectTypes.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.type_key == normalizedTypeKey);
            return entity is null ? null : MapObjectType(entity);
        });
    }

    // ---- Object ID Reservation ----

    public ReservationRecord? FindReservation(Guid projectId, Guid reservationId) =>
        _ef.Execute(db =>
        {
            var entity = db.ObjectIdReservations.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.reservation_id == reservationId);
            return entity is null ? null : MapReservation(entity);
        });

    public ReservationAllocation ReserveObjectId(ObjectIdReservationRequest request)
    {
        return _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // request_key 幂等对账（P2-09-04 §6；TX-21/22）：响应丢失的重试返回同一分配。
                if (request.RequestKey is not null)
                {
                    var existing = db.ObjectIdReservations.SingleOrDefault(x =>
                        x.project_id == request.ProjectId && x.request_key == request.RequestKey);
                    if (existing is not null)
                    {
                        var recovered = MapReservation(existing);
                        if (recovered.ObjectTypeId != request.ObjectTypeId)
                        {
                            throw new CoreDomainConflictException(
                                $"request_key \"{request.RequestKey}\" 已属于不同 Object Type 的分配，拒绝幂等复用。");
                        }

                        transaction.Commit();
                        return new ReservationAllocation
                        {
                            Reservation = recovered,
                            RecoveredByRequestKey = true,
                        };
                    }
                }

                var typeEntity = db.ObjectTypes.SingleOrDefault(x =>
                    x.project_id == request.ProjectId && x.object_type_id == request.ObjectTypeId);
                if (typeEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Object Type {request.ObjectTypeId} 不存在或不属于项目 {request.ProjectId}。");
                }

                var type = MapObjectType(typeEntity);
                if (!type.Allocatable)
                {
                    throw new CoreDomainConflictException(
                        $"Object Type \"{type.TypeKey}\" 不可分配（allocatable=false）。");
                }

                // allocator 读改写：next_sequence 仅在本事务内前进、永不回退（P2-09-11 §4.2）。
                var allocator = db.ObjectIdAllocators.SingleOrDefault(x =>
                    x.project_id == request.ProjectId && x.object_type_id == request.ObjectTypeId);
                if (allocator is null)
                {
                    throw new CoreDomainDataException(
                        $"Object Type {request.ObjectTypeId} 缺少 allocator 行（应随 Project 播种建立，数据损坏）。");
                }

                var nextSequence = allocator.next_sequence;
                allocator.next_sequence = checked(nextSequence + 1);
                allocator.updated_at_utc_ms = request.NowUtcMs;

                var reservation = new CoreObjectIdReservationEntity
                {
                    reservation_id = Guid.NewGuid(),
                    project_id = request.ProjectId,
                    object_type_id = request.ObjectTypeId,
                    object_identity_id = null,
                    allocated_sequence = nextSequence,
                    allocated_id_value = ObjectAllocationFormat.DeriveAllocatedIdValue(nextSequence),
                    reservation_state = "Reserved",
                    request_key = request.RequestKey,
                    abandon_reason = null,
                    created_at_utc_ms = request.NowUtcMs,
                    updated_at_utc_ms = request.NowUtcMs,
                };
                db.ObjectIdReservations.Add(reservation);

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
                return new ReservationAllocation
                {
                    Reservation = MapReservation(reservation),
                    RecoveredByRequestKey = false,
                };
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public ReservationRecord BindReservation(ObjectBindRequest request)
    {
        return _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // 事务内重读前置状态（CD-02-A §13.3：状态机 UPDATE 不依赖调用方传值）。
                var reservationEntity = db.ObjectIdReservations.SingleOrDefault(x =>
                    x.project_id == request.ProjectId && x.reservation_id == request.ReservationId);
                if (reservationEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Reservation {request.ReservationId} 不存在或不属于项目 {request.ProjectId}。");
                }

                var reservation = MapReservation(reservationEntity);
                ReservationRules.ValidateTransition(reservation.State, ReservationState.Bound);

                var typeEntity = db.ObjectTypes.SingleOrDefault(x =>
                    x.project_id == request.ProjectId && x.object_type_id == reservation.ObjectTypeId);
                if (typeEntity is null)
                {
                    throw new CoreDomainDataException(
                        $"Reservation {request.ReservationId} 引用的 Object Type 不存在（数据损坏）。");
                }

                var type = MapObjectType(typeEntity);
                // pin 按 descriptor registry（CD-02-C 编译期权威）。
                var descriptor = ObjectTypeRegistry.Require(type.TypeKey);
                var semanticsId = descriptor.SemanticsId;
                ObjectRules.ValidatePin(type.TypeKey, descriptor.SchemaVersion, semanticsId);
                ObjectRules.ValidateScopePairing(request.SharingScope, request.OwnerMapId);

                if (request.SharingScope == ObjectSharingScope.MapLocal
                    && !db.Maps.Any(x => x.ProjectId == request.ProjectId && x.MapId == request.OwnerMapId!.Value))
                {
                    throw new CoreDomainConflictException(
                        $"owner Map {request.OwnerMapId.Value} 不存在或不属于项目 {request.ProjectId}。");
                }

                db.Objects.Add(new CoreObjectEntity
                {
                    object_identity_id = request.ObjectIdentityId,
                    project_id = request.ProjectId,
                    object_type_id = reservation.ObjectTypeId,
                    object_type_schema_version = descriptor.SchemaVersion,
                    object_type_semantics_id = semanticsId,
                    sharing_scope = request.SharingScope == ObjectSharingScope.ProjectShared
                        ? "project_shared"
                        : "map_local",
                    owner_map_id = request.OwnerMapId,
                    lifecycle_status = "draft",
                    created_at_utc_ms = request.NowUtcMs,
                    updated_at_utc_ms = request.NowUtcMs,
                });

                reservationEntity.object_identity_id = request.ObjectIdentityId;
                reservationEntity.reservation_state = "Bound";
                reservationEntity.updated_at_utc_ms = request.NowUtcMs;

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
                return MapReservation(reservationEntity);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public ReservationRecord AbandonReservation(
        Guid projectId,
        Guid reservationId,
        string? abandonReason,
        long nowUtcMs)
    {
        return _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var reservationEntity = db.ObjectIdReservations.SingleOrDefault(x =>
                    x.project_id == projectId && x.reservation_id == reservationId);
                if (reservationEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Reservation {reservationId} 不存在或不属于项目 {projectId}。");
                }

                var reservation = MapReservation(reservationEntity);
                ReservationRules.ValidateTransition(reservation.State, ReservationState.Abandoned);

                if (reservation.ObjectIdentityId is not null)
                {
                    // Bound→Abandoned：identity 行保留为 abandoned 墓碑（仅 draft 可降级）。
                    var identity = db.Objects.SingleOrDefault(x =>
                        x.project_id == projectId
                        && x.object_identity_id == reservation.ObjectIdentityId.Value);
                    if (identity is null || identity.lifecycle_status != "draft")
                    {
                        throw new CoreDomainConflictException(
                            $"绑定身份 {reservation.ObjectIdentityId.Value} 不是 draft 状态，无法放弃（状态不一致，fail-closed）。");
                    }

                    identity.lifecycle_status = "abandoned";
                    identity.updated_at_utc_ms = nowUtcMs;
                }

                reservationEntity.reservation_state = "Abandoned";
                reservationEntity.abandon_reason = abandonReason;
                reservationEntity.updated_at_utc_ms = nowUtcMs;

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
                return MapReservation(reservationEntity);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    // ---- Object identity / Definition ----

    public ObjectRecord? FindObject(Guid projectId, Guid objectIdentityId) =>
        _ef.Execute(db =>
        {
            var entity = db.Objects.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.object_identity_id == objectIdentityId);
            if (entity is null)
            {
                return null;
            }

            var typeKey = db.ObjectTypes.AsNoTracking()
                .Where(x => x.project_id == projectId && x.object_type_id == entity.object_type_id)
                .Select(x => x.type_key)
                .SingleOrDefault()
                ?? throw new CoreDomainDataException(
                    $"Object {objectIdentityId} 引用的 Object Type 不存在（数据损坏）。");

            return MapObject(entity, typeKey);
        });

    public ObjectDefinitionRecord? ReadObjectDefinition(Guid projectId, Guid objectIdentityId) =>
        _ef.Execute(db =>
        {
            var entity = db.ObjectDefinitions.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.object_identity_id == objectIdentityId);
            if (entity is null)
            {
                return null;
            }

            var record = new ObjectDefinitionRecord
            {
                ProjectId = entity.project_id,
                ObjectIdentityId = entity.object_identity_id,
                DefinitionSchemaVersion = entity.definition_schema_version,
                DefinitionPayloadJson = entity.definition_payload_json,
                DefinitionHash = entity.definition_hash,
                UpdatedAtUtcMs = entity.updated_at_utc_ms,
            };

            // R-定义：读侧校验 hash 与 payload 一致（fail-closed）。
            var expectedHash = CanonicalJson.Sha256Hex(record.DefinitionPayloadJson);
            if (!string.Equals(expectedHash, record.DefinitionHash, StringComparison.Ordinal))
            {
                throw new CoreDomainDataException(
                    $"Object {record.ObjectIdentityId} 的 definition_hash 与 payload 不一致（数据损坏）。");
            }

            return record;
        });

    public void InsertObject(ObjectInsertPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            var sqliteTransaction = (SqliteTransaction)transaction.GetDbTransaction();
            try
            {
                // 事务内重读前置状态（TX-55：正式创建与 Bound→Inserted 原子）。
                var reservationEntity = db.ObjectIdReservations.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.reservation_id == plan.ReservationId);
                if (reservationEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Reservation {plan.ReservationId} 不存在或不属于项目 {plan.ProjectId}。");
                }

                var reservation = MapReservation(reservationEntity);
                if (reservation.State != ReservationState.Bound || reservation.ObjectIdentityId is null)
                {
                    throw new CoreDomainConflictException(
                        $"Reservation {plan.ReservationId} 处于 {reservation.State} 状态，仅 Bound 可正式创建。");
                }

                if (reservation.ObjectIdentityId.Value != plan.ObjectIdentityId)
                {
                    throw new CoreDomainDataException(
                        $"Insert 计划的身份 {plan.ObjectIdentityId} 与 reservation 溯源 {reservation.ObjectIdentityId.Value} 不一致（fail-closed）。");
                }

                var identityEntity = db.Objects.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId);
                if (identityEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Object {plan.ObjectIdentityId} 不存在或不属于项目 {plan.ProjectId}。");
                }

                var typeKey = db.ObjectTypes
                    .Where(x => x.project_id == plan.ProjectId && x.object_type_id == identityEntity.object_type_id)
                    .Select(x => x.type_key)
                    .SingleOrDefault()
                    ?? throw new CoreDomainDataException(
                        $"Object {plan.ObjectIdentityId} 引用的 Object Type 不存在（数据损坏）。");
                var identity = MapObject(identityEntity, typeKey);
                if (identity.LifecycleStatus != ObjectLifecycleStatus.Draft)
                {
                    throw new CoreDomainConflictException(
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态，仅 draft 可正式创建。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.Definition.DefinitionSchemaVersion);

                var mapId = plan.Registration?.MapId;
                if (plan.Registration is not null)
                {
                    RegisterInDbContext(
                        db,
                        plan.ProjectId,
                        plan.Registration,
                        identity);
                }

                // definition 首行（canonical payload + hash；每 identity 至多一行由 PK 保证）。
                var (canonicalPayload, definitionHash) =
                    CanonicalJson.CanonicalizeAndHash(plan.Definition.DefinitionPayloadJson);
                db.ObjectDefinitions.Add(new CoreObjectDefinitionEntity
                {
                    project_id = plan.ProjectId,
                    object_identity_id = plan.ObjectIdentityId,
                    definition_schema_version = plan.Definition.DefinitionSchemaVersion,
                    definition_payload_json = canonicalPayload,
                    definition_hash = definitionHash,
                    updated_at_utc_ms = plan.NowUtcMs,
                });

                if (plan.InitialMembership is not null)
                {
                    // P3⇒P2：成员行必须有对应注册行（本事务刚写入）。
                    if (mapId is null
                        || TryReadMapObjectRefRow(
                            sqliteTransaction,
                            plan.ProjectId,
                            mapId.Value,
                            plan.ObjectIdentityId) is null)
                    {
                        throw new CoreDomainConflictException(
                            "Initial State 成员缺少同 Map 注册行（P3⇒P2），拒绝插入。");
                    }

                    AppendInitialMemberInTransaction(
                        sqliteTransaction,
                        plan.ProjectId,
                        mapId.Value,
                        plan.ObjectIdentityId,
                        new InitialObjectWrite
                        {
                            ObjectIdentityId = plan.ObjectIdentityId,
                            MembershipPayloadJson = plan.InitialMembership.MembershipPayloadJson,
                            PlacementPayloadJson = plan.InitialMembership.PlacementPayloadJson,
                            PlacementSchemaVersion = plan.InitialMembership.PlacementSchemaVersion,
                        },
                        plan.NowUtcMs);
                }

                // 状态转换（同事务原子）：identity draft→active + reservation Bound→Inserted。
                identityEntity.lifecycle_status = "active";
                identityEntity.updated_at_utc_ms = plan.NowUtcMs;
                reservationEntity.reservation_state = "Inserted";
                reservationEntity.updated_at_utc_ms = plan.NowUtcMs;

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public void EditObjectDefinition(ObjectDefinitionEditPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var identityEntity = db.Objects.AsNoTracking().SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId);
                if (identityEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Object {plan.ObjectIdentityId} 不存在或不属于项目 {plan.ProjectId}。");
                }

                var typeKey = db.ObjectTypes.AsNoTracking()
                    .Where(x => x.project_id == plan.ProjectId && x.object_type_id == identityEntity.object_type_id)
                    .Select(x => x.type_key)
                    .SingleOrDefault()
                    ?? throw new CoreDomainDataException(
                        $"Object {plan.ObjectIdentityId} 引用的 Object Type 不存在（数据损坏）。");
                var identity = MapObject(identityEntity, typeKey);
                if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
                {
                    throw new CoreDomainConflictException(
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态；仅 active Object 可编辑定义。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);

                // CAS 在事务内比对（先读后写在同一临界区，无并发窗口）。
                var current = db.ObjectDefinitions.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId)
                    ?? throw new CoreDomainDataException(
                        $"active Object {plan.ObjectIdentityId} 缺少定义行（数据损坏，R-定义单行）。");
                var currentHash = CanonicalJson.Sha256Hex(current.definition_payload_json);
                if (!string.Equals(currentHash, current.definition_hash, StringComparison.Ordinal))
                {
                    throw new CoreDomainDataException(
                        $"Object {plan.ObjectIdentityId} 的 definition_hash 与 payload 不一致（数据损坏）。");
                }

                if (!string.Equals(current.definition_hash, plan.ExpectedDefinitionHash, StringComparison.Ordinal))
                {
                    throw new CoreDomainConflictException("定义 CAS 冲突：expected hash 与当前定义不匹配。");
                }

                var (canonicalPayload, definitionHash) =
                    CanonicalJson.CanonicalizeAndHash(plan.DefinitionPayloadJson);
                current.definition_schema_version = plan.DefinitionSchemaVersion;
                current.definition_payload_json = canonicalPayload;
                current.definition_hash = definitionHash;
                current.updated_at_utc_ms = plan.NowUtcMs;

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    // ---- Map 注册（P2 存在层） ----

    public MapObjectRefRecord? FindMapObjectRef(Guid projectId, Guid mapId, Guid objectIdentityId) =>
        _ef.Execute(db =>
        {
            var entity = db.MapObjectRefs.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId
                && x.map_id == mapId
                && x.object_identity_id == objectIdentityId);
            return entity is null ? null : MapMapObjectRef(entity);
        });

    public IReadOnlyList<MapObjectRefRecord> ListMapObjectRefs(Guid projectId, Guid mapId) =>
        _ef.Execute(db => (IReadOnlyList<MapObjectRefRecord>)db.MapObjectRefs.AsNoTracking()
            .Where(x => x.project_id == projectId && x.map_id == mapId)
            .OrderBy(x => x.object_identity_id)
            .AsEnumerable()
            .Select(MapMapObjectRef)
            .ToArray());

    public void RegisterMapObject(MapObjectRegistrationPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var identityEntity = db.Objects.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId);
                if (identityEntity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Object {plan.ObjectIdentityId} 不存在或不属于项目 {plan.ProjectId}。");
                }

                var typeKey = db.ObjectTypes
                    .Where(x => x.project_id == plan.ProjectId && x.object_type_id == identityEntity.object_type_id)
                    .Select(x => x.type_key)
                    .SingleOrDefault()
                    ?? throw new CoreDomainDataException(
                        $"Object {plan.ObjectIdentityId} 引用的 Object Type 不存在（数据损坏）。");
                var identity = MapObject(identityEntity, typeKey);

                RegisterInDbContext(
                    db,
                    plan.ProjectId,
                    new MapObjectRegistration
                    {
                        MapObjectRefId = plan.MapObjectRefId,
                        MapId = plan.MapId,
                    },
                    identity);

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public void UnregisterMapObject(Guid projectId, Guid mapId, Guid objectIdentityId)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var registration = db.MapObjectRefs.SingleOrDefault(x =>
                    x.project_id == projectId
                    && x.map_id == mapId
                    && x.object_identity_id == objectIdentityId);
                if (registration is null)
                {
                    throw new CoreDomainConflictException(
                        $"Object {objectIdentityId} 未注册到 Map {mapId}，无法取消。");
                }

                // P3⇒P2：仍有 Initial State 成员行时拒绝（须先经 Initial State 编辑移除成员）。
                if (db.MapInitialObjects.Any(x =>
                    x.project_id == projectId
                    && x.map_id == mapId
                    && x.object_identity_id == objectIdentityId))
                {
                    throw new CoreDomainConflictException(
                        $"Object {objectIdentityId} 仍是 Map {mapId} Initial State 成员，须先移除成员再取消注册（P3⇒P2）。");
                }

                db.MapObjectRefs.Remove(registration);
                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    // ---- Map Initial State（P3 存在层） ----

    public MapInitialStateRecord? FindMapInitialState(Guid projectId, Guid mapId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadInitialStateRow(transaction, projectId, mapId);
        });

    public IReadOnlyList<InitialObjectRecord> ListInitialObjects(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT project_id, map_id, initial_state_id, object_identity_id,
                   initial_membership_payload_json, initial_placement_payload_json,
                   initial_placement_schema_version, created_at_utc_ms
            FROM core_map_initial_object
            WHERE project_id = $projectId AND map_id = $mapId
            ORDER BY object_identity_id;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$mapId", Uuid(mapId));
            using var reader = command.ExecuteReader();
            var rows = new List<InitialObjectRecord>();
            while (reader.Read())
            {
                rows.Add(MapInitialObject(reader));
            }

            return (IReadOnlyList<InitialObjectRecord>)rows;
        });
    }

    public void EditMapInitialState(EditInitialStatePlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var baseline = TryReadInitialStateRow(transaction, plan.ProjectId, plan.MapId);
                if (baseline is not null)
                {
                    // CAS：expected fingerprint 必须匹配当前值（事务内比对，无并发窗口）。
                    if (plan.ExpectedFingerprint is null
                        || !string.Equals(baseline.ContentFingerprint, plan.ExpectedFingerprint, StringComparison.Ordinal))
                    {
                        throw new CoreDomainConflictException("Initial State CAS 冲突：expected fingerprint 与当前基线不匹配。");
                    }

                    if (baseline.InitialStateId != plan.InitialStateId)
                    {
                        throw new CoreDomainDataException(
                            $"编辑计划的基线 {plan.InitialStateId} 与 Map 既有基线 {baseline.InitialStateId} 不一致（fail-closed）。");
                    }
                }
                else if (plan.ExpectedFingerprint is not null)
                {
                    throw new CoreDomainConflictException(
                        $"Map {plan.MapId} 尚无 Initial State 基线；expected fingerprint 必须为 null（建立语义）。");
                }
                else
                {
                    using var create = connection.CreateCommand();
                    create.Transaction = transaction;
                    create.CommandText = """
                        INSERT INTO core_map_initial_state
                            (initial_state_id, project_id, map_id, baseline_schema_version,
                             content_fingerprint, created_at_utc_ms, updated_at_utc_ms)
                        VALUES ($stateId, $projectId, $mapId, $schemaVersion, '-', $now, $now);
                        """;
                    create.Parameters.AddWithValue("$stateId", Uuid(plan.InitialStateId));
                    create.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    create.Parameters.AddWithValue("$mapId", Uuid(plan.MapId));
                    create.Parameters.AddWithValue("$schemaVersion", plan.BaselineSchemaVersion);
                    create.Parameters.AddWithValue("$now", plan.NowUtcMs);
                    create.ExecuteNonQuery();
                }

                // 事务内重读成员前置（active + P3⇒P2），canonical 化 payload。
                var writes = new List<(Guid IdentityId, string MembershipJson, string? PlacementJson, long? PlacementVersion)>();
                foreach (var member in plan.Members)
                {
                    var identity = ReadObjectRow(transaction, plan.ProjectId, member.ObjectIdentityId);
                    if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
                    {
                        throw new CoreDomainConflictException(
                            $"成员 {member.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态，不得进入 Initial State（R-成员前置）。");
                    }

                    if (TryReadMapObjectRefRow(transaction, plan.ProjectId, plan.MapId, member.ObjectIdentityId) is null)
                    {
                        throw new CoreDomainConflictException(
                            $"成员 {member.ObjectIdentityId} 缺少 Map {plan.MapId} 注册行（P3⇒P2），拒绝写入。");
                    }

                    writes.Add((
                        member.ObjectIdentityId,
                        CanonicalJson.Canonicalize(member.MembershipPayloadJson),
                        member.PlacementPayloadJson is null ? null : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
                        member.PlacementSchemaVersion));
                }

                // 全量重写（P2-09-11 §4.1 "baseline/成员行重写"）：delete + insert。
                using (var clear = connection.CreateCommand())
                {
                    clear.Transaction = transaction;
                    clear.CommandText = """
                        DELETE FROM core_map_initial_object
                        WHERE project_id = $projectId AND map_id = $mapId;
                        """;
                    clear.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    clear.Parameters.AddWithValue("$mapId", Uuid(plan.MapId));
                    clear.ExecuteNonQuery();
                }

                foreach (var (identityId, membershipJson, placementJson, placementVersion) in writes)
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO core_map_initial_object
                            (project_id, map_id, initial_state_id, object_identity_id,
                             initial_membership_payload_json, initial_placement_payload_json,
                             initial_placement_schema_version, created_at_utc_ms)
                        VALUES ($projectId, $mapId, $stateId, $identityId,
                                $membership, $placement, $placementVersion, $now);
                        """;
                    insert.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insert.Parameters.AddWithValue("$mapId", Uuid(plan.MapId));
                    insert.Parameters.AddWithValue("$stateId", Uuid(plan.InitialStateId));
                    insert.Parameters.AddWithValue("$identityId", Uuid(identityId));
                    insert.Parameters.AddWithValue("$membership", membershipJson);
                    insert.Parameters.AddWithValue("$placement", (object?)placementJson ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$placementVersion", (object?)placementVersion ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$now", plan.NowUtcMs);
                    insert.ExecuteNonQuery();
                }

                // 指纹由 Domain 规范化序列化计算（同内容同指纹、顺序无关、不含审计字段）。
                var members = ListInitialObjectRows(transaction, plan.ProjectId, plan.MapId);
                var fingerprint = InitialStateFingerprint.Compute(plan.BaselineSchemaVersion, members);
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE core_map_initial_state
                    SET baseline_schema_version = $schemaVersion,
                        content_fingerprint = $fingerprint,
                        updated_at_utc_ms = $now
                    WHERE project_id = $projectId AND map_id = $mapId;
                    """;
                update.Parameters.AddWithValue("$schemaVersion", plan.BaselineSchemaVersion);
                update.Parameters.AddWithValue("$fingerprint", fingerprint);
                update.Parameters.AddWithValue("$now", plan.NowUtcMs);
                update.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                update.Parameters.AddWithValue("$mapId", Uuid(plan.MapId));
                update.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    // ---- 事务内写路径辅助 ----

    /// <summary>
    /// Register an Object in a Map inside the caller's EF transaction.
    /// Map-local ownership and duplicate-registration rules are checked before SaveChanges.
    /// </summary>
    private static void RegisterInDbContext(
        CoreDomainDbContext db,
        Guid projectId,
        MapObjectRegistration registration,
        ObjectRecord identity)
    {
        if (!db.Maps.Any(x => x.ProjectId == projectId && x.MapId == registration.MapId))
        {
            throw new CoreDomainConflictException(
                $"Map {registration.MapId} 不存在或不属于项目 {projectId}，拒绝注册。");
        }

        if (identity.SharingScope == ObjectSharingScope.MapLocal
            && identity.OwnerMapId != registration.MapId)
        {
            throw new CoreDomainConflictException("map_local Object 只能注册到 owner Map（R-注册）。");
        }

        if (db.MapObjectRefs.Any(x =>
            x.project_id == projectId
            && x.map_id == registration.MapId
            && x.object_identity_id == identity.ObjectIdentityId))
        {
            throw new CoreDomainConflictException(
                $"Object {identity.ObjectIdentityId} 已注册到 Map {registration.MapId}，拒绝重复注册。");
        }

        db.MapObjectRefs.Add(new CoreMapObjectRefEntity
        {
            map_object_ref_id = registration.MapObjectRefId,
            project_id = projectId,
            map_id = registration.MapId,
            object_identity_id = identity.ObjectIdentityId,
            created_at_utc_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });
    }

    /// <summary>
    /// Initial State 成员追加（InsertObject 路径）：基线缺失则同事务建立；成员行插入后
    /// 按全量成员集合重算指纹（Domain 计算并写回基线行）。
    /// </summary>
    private void AppendInitialMemberInTransaction(
        SqliteTransaction transaction,
        Guid projectId,
        Guid mapId,
        Guid objectIdentityId,
        InitialObjectWrite member,
        long nowUtcMs)
    {
        var baseline = TryReadInitialStateRow(transaction, projectId, mapId);
        var baselineVersion = baseline?.BaselineSchemaVersion ?? MapInitialStateService.StructuralBaselineSchemaVersion;
        if (baseline is null)
        {
            using var create = transaction.Connection!.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = """
                INSERT INTO core_map_initial_state
                    (initial_state_id, project_id, map_id, baseline_schema_version,
                     content_fingerprint, created_at_utc_ms, updated_at_utc_ms)
                VALUES ($stateId, $projectId, $mapId, $schemaVersion, '-', $now, $now);
                """;
            create.Parameters.AddWithValue("$stateId", Guid.NewGuid().ToString("D"));
            create.Parameters.AddWithValue("$projectId", Uuid(projectId));
            create.Parameters.AddWithValue("$mapId", Uuid(mapId));
            create.Parameters.AddWithValue("$schemaVersion", baselineVersion);
            create.Parameters.AddWithValue("$now", nowUtcMs);
            create.ExecuteNonQuery();
        }

        using (var insert = transaction.Connection!.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO core_map_initial_object
                    (project_id, map_id, initial_state_id, object_identity_id,
                     initial_membership_payload_json, initial_placement_payload_json,
                     initial_placement_schema_version, created_at_utc_ms)
                VALUES ($projectId, $mapId,
                        (SELECT initial_state_id FROM core_map_initial_state
                         WHERE project_id = $projectId AND map_id = $mapId),
                        $identityId, $membership, $placement, $placementVersion, $now);
                """;
            insert.Parameters.AddWithValue("$projectId", Uuid(projectId));
            insert.Parameters.AddWithValue("$mapId", Uuid(mapId));
            insert.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
            insert.Parameters.AddWithValue(
                "$membership",
                CanonicalJson.Canonicalize(member.MembershipPayloadJson));
            insert.Parameters.AddWithValue(
                "$placement",
                member.PlacementPayloadJson is null ? DBNull.Value : CanonicalJson.Canonicalize(member.PlacementPayloadJson));
            insert.Parameters.AddWithValue("$placementVersion", (object?)member.PlacementSchemaVersion ?? DBNull.Value);
            insert.Parameters.AddWithValue("$now", nowUtcMs);
            insert.ExecuteNonQuery();
        }

        var members = ListInitialObjectRows(transaction, projectId, mapId);
        var fingerprint = InitialStateFingerprint.Compute(baselineVersion, members);
        using var update = transaction.Connection!.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE core_map_initial_state
            SET content_fingerprint = $fingerprint, updated_at_utc_ms = $now
            WHERE project_id = $projectId AND map_id = $mapId;
            """;
        update.Parameters.AddWithValue("$fingerprint", fingerprint);
        update.Parameters.AddWithValue("$now", nowUtcMs);
        update.Parameters.AddWithValue("$projectId", Uuid(projectId));
        update.Parameters.AddWithValue("$mapId", Uuid(mapId));
        update.ExecuteNonQuery();
    }

    // ---- 行读取辅助（事务感知；行映射 + fail-closed 校验） ----

    private MapRecord? TryReadMapRow(SqliteTransaction transaction, Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms
            FROM core_map
            WHERE project_id = $projectId AND map_id = $mapId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMap(reader) : null;
    }

    private ObjectTypeRecord? TryReadObjectTypeRow(SqliteTransaction transaction, Guid projectId, Guid objectTypeId)
    {
        const string sql = """
            SELECT object_type_id, project_id, type_key, display_name, allocatable,
                   allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_object_type
            WHERE project_id = $projectId AND object_type_id = $typeId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$typeId", Uuid(objectTypeId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapObjectType(reader) : null;
    }

    private ObjectTypeRecord ReadObjectTypeRow(SqliteTransaction transaction, Guid projectId, Guid objectTypeId) =>
        TryReadObjectTypeRow(transaction, projectId, objectTypeId)
        ?? throw new CoreDomainConflictException($"Object Type {objectTypeId} 不存在或不属于项目 {projectId}。");

    private ReservationRecord? TryReadReservationRow(SqliteTransaction transaction, Guid projectId, Guid reservationId)
    {
        const string sql = """
            SELECT reservation_id, project_id, object_type_id, object_identity_id,
                   allocated_sequence, allocated_id_value, reservation_state, request_key,
                   abandon_reason, created_at_utc_ms, updated_at_utc_ms
            FROM core_object_id_reservation
            WHERE project_id = $projectId AND reservation_id = $reservationId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$reservationId", Uuid(reservationId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReservation(reader) : null;
    }

    private ReservationRecord ReadReservationRow(SqliteTransaction transaction, Guid projectId, Guid reservationId) =>
        TryReadReservationRow(transaction, projectId, reservationId)
        ?? throw new CoreDomainConflictException($"Reservation {reservationId} 不存在或不属于项目 {projectId}。");

    private ReservationRecord? TryReadReservationByRequestKey(SqliteTransaction transaction, Guid projectId, string requestKey)
    {
        const string sql = """
            SELECT reservation_id, project_id, object_type_id, object_identity_id,
                   allocated_sequence, allocated_id_value, reservation_state, request_key,
                   abandon_reason, created_at_utc_ms, updated_at_utc_ms
            FROM core_object_id_reservation
            WHERE project_id = $projectId AND request_key = $requestKey;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$requestKey", requestKey);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapReservation(reader) : null;
    }

    private ObjectRecord? TryReadObjectRow(SqliteTransaction transaction, Guid projectId, Guid objectIdentityId)
    {
        const string sql = """
            SELECT o.object_identity_id, o.project_id, o.object_type_id,
                   o.object_type_schema_version, o.object_type_semantics_id,
                   o.sharing_scope, o.owner_map_id, o.lifecycle_status,
                   o.created_at_utc_ms, o.updated_at_utc_ms, t.type_key
            FROM core_object o
            JOIN core_object_type t ON t.project_id = o.project_id AND t.object_type_id = o.object_type_id
            WHERE o.project_id = $projectId AND o.object_identity_id = $identityId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapObject(reader) : null;
    }

    private ObjectRecord ReadObjectRow(SqliteTransaction transaction, Guid projectId, Guid objectIdentityId) =>
        TryReadObjectRow(transaction, projectId, objectIdentityId)
        ?? throw new CoreDomainConflictException($"Object {objectIdentityId} 不存在或不属于项目 {projectId}。");

    private ObjectDefinitionRecord? TryReadDefinitionRow(SqliteTransaction transaction, Guid projectId, Guid objectIdentityId)
    {
        const string sql = """
            SELECT project_id, object_identity_id, definition_schema_version,
                   definition_payload_json, definition_hash, updated_at_utc_ms
            FROM core_object_definition
            WHERE project_id = $projectId AND object_identity_id = $identityId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var record = new ObjectDefinitionRecord
        {
            ProjectId = ParseUuid(reader.GetString(0)),
            ObjectIdentityId = ParseUuid(reader.GetString(1)),
            DefinitionSchemaVersion = reader.GetInt64(2),
            DefinitionPayloadJson = reader.GetString(3),
            DefinitionHash = reader.GetString(4),
            UpdatedAtUtcMs = reader.GetInt64(5),
        };

        // R-定义：读侧校验 hash 与 payload 一致（fail-closed）。
        var expectedHash = CanonicalJson.Sha256Hex(record.DefinitionPayloadJson);
        if (!string.Equals(expectedHash, record.DefinitionHash, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException(
                $"Object {record.ObjectIdentityId} 的 definition_hash 与 payload 不一致（数据损坏）。");
        }

        return record;
    }

    private MapObjectRefRecord? TryReadMapObjectRefRow(SqliteTransaction transaction, Guid projectId, Guid mapId, Guid objectIdentityId)
    {
        const string sql = """
            SELECT map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms
            FROM core_map_object_ref
            WHERE project_id = $projectId AND map_id = $mapId AND object_identity_id = $identityId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapMapObjectRef(reader) : null;
    }

    private MapInitialStateRecord? TryReadInitialStateRow(SqliteTransaction transaction, Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT initial_state_id, project_id, map_id, baseline_schema_version,
                   content_fingerprint, created_at_utc_ms, updated_at_utc_ms
            FROM core_map_initial_state
            WHERE project_id = $projectId AND map_id = $mapId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapInitialState(reader) : null;
    }

    private InitialObjectRecord? TryReadInitialObjectRow(SqliteTransaction transaction, Guid projectId, Guid mapId, Guid objectIdentityId)
    {
        const string sql = """
            SELECT project_id, map_id, initial_state_id, object_identity_id,
                   initial_membership_payload_json, initial_placement_payload_json,
                   initial_placement_schema_version, created_at_utc_ms
            FROM core_map_initial_object
            WHERE project_id = $projectId AND map_id = $mapId AND object_identity_id = $identityId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapInitialObject(reader) : null;
    }

    private List<InitialObjectRecord> ListInitialObjectRows(SqliteTransaction transaction, Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT project_id, map_id, initial_state_id, object_identity_id,
                   initial_membership_payload_json, initial_placement_payload_json,
                   initial_placement_schema_version, created_at_utc_ms
            FROM core_map_initial_object
            WHERE project_id = $projectId AND map_id = $mapId
            ORDER BY object_identity_id;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        using var reader = command.ExecuteReader();
        var rows = new List<InitialObjectRecord>();
        while (reader.Read())
        {
            rows.Add(MapInitialObject(reader));
        }

        return rows;
    }

    private static ObjectTypeRecord MapObjectType(CoreObjectTypeEntity entity) => new()
    {
        ObjectTypeId = entity.object_type_id,
        ProjectId = entity.project_id,
        TypeKey = ObjectRules.NormalizeTypeKey(entity.type_key),
        DisplayName = ObjectRules.NormalizeDisplayName(entity.display_name),
        Allocatable = entity.allocatable != 0,
        AllocationFormatJson = entity.allocation_format_json,
        AllocationFormatVersion = entity.allocation_format_version,
        CreatedAtUtcMs = entity.created_at_utc_ms,
        UpdatedAtUtcMs = entity.updated_at_utc_ms,
    };

    private static ReservationRecord MapReservation(CoreObjectIdReservationEntity entity)
    {
        var state = entity.reservation_state switch
        {
            "Reserved" => ReservationState.Reserved,
            "Bound" => ReservationState.Bound,
            "Inserted" => ReservationState.Inserted,
            "Abandoned" => ReservationState.Abandoned,
            _ => throw new CoreDomainDataException(
                $"reservation_state \"{entity.reservation_state}\" 不可识别。"),
        };

        if ((state == ReservationState.Reserved && entity.object_identity_id is not null)
            || (state is ReservationState.Bound or ReservationState.Inserted && entity.object_identity_id is null))
        {
            throw new CoreDomainDataException(
                $"Reservation {entity.reservation_id} 的 state/identity 组合违反不变量。");
        }

        return new ReservationRecord
        {
            ReservationId = entity.reservation_id,
            ProjectId = entity.project_id,
            ObjectTypeId = entity.object_type_id,
            ObjectIdentityId = entity.object_identity_id,
            AllocatedSequence = entity.allocated_sequence,
            AllocatedIdValue = entity.allocated_id_value,
            State = state,
            RequestKey = entity.request_key,
            AbandonReason = entity.abandon_reason,
            CreatedAtUtcMs = entity.created_at_utc_ms,
            UpdatedAtUtcMs = entity.updated_at_utc_ms,
        };
    }

    // ---- 行映射（fail-closed 解析） ----

    private static ObjectTypeRecord MapObjectType(SqliteDataReader reader) => new()
    {
        ObjectTypeId = ParseUuid(reader.GetString(0)),
        ProjectId = ParseUuid(reader.GetString(1)),
        TypeKey = ObjectRules.NormalizeTypeKey(reader.GetString(2)),
        DisplayName = ObjectRules.NormalizeDisplayName(reader.GetString(3)),
        Allocatable = reader.GetInt64(4) != 0,
        AllocationFormatJson = reader.GetString(5),
        AllocationFormatVersion = reader.GetInt64(6),
        CreatedAtUtcMs = reader.GetInt64(7),
        UpdatedAtUtcMs = reader.GetInt64(8),
    };

    private static ReservationRecord MapReservation(SqliteDataReader reader)
    {
        var stateText = reader.GetString(6);
        var state = stateText switch
        {
            "Reserved" => ReservationState.Reserved,
            "Bound" => ReservationState.Bound,
            "Inserted" => ReservationState.Inserted,
            "Abandoned" => ReservationState.Abandoned,
            _ => throw new CoreDomainDataException($"reservation_state \"{stateText}\" 不可识别。"),
        };
        Guid? identityId = reader.IsDBNull(3) ? null : ParseUuid(reader.GetString(3));

        // CHECK 约束的读侧复核（防御未来 schema 变更绕过；轻量）。
        if ((state == ReservationState.Reserved && identityId is not null)
            || (state is ReservationState.Bound or ReservationState.Inserted && identityId is null))
        {
            throw new CoreDomainDataException($"Reservation {reader.GetString(0)} 的 state/identity 组合违反不变量。");
        }

        return new ReservationRecord
        {
            ReservationId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            ObjectTypeId = ParseUuid(reader.GetString(2)),
            ObjectIdentityId = identityId,
            AllocatedSequence = reader.GetInt64(4),
            AllocatedIdValue = reader.GetString(5),
            State = state,
            RequestKey = reader.IsDBNull(7) ? null : reader.GetString(7),
            AbandonReason = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAtUtcMs = reader.GetInt64(9),
            UpdatedAtUtcMs = reader.GetInt64(10),
        };
    }

    private static ObjectRecord MapObject(CoreObjectEntity entity, string typeKey)
    {
        var schemaVersion = entity.object_type_schema_version;
        var semanticsId = entity.object_type_semantics_id;

        // 读侧 fail-closed：未知 pin 拒绝解释（Handoff §5.4；P2-09-18 §10）。
        ObjectRules.ValidateKnownPin(typeKey, schemaVersion, semanticsId);

        var scope = entity.sharing_scope switch
        {
            "project_shared" => ObjectSharingScope.ProjectShared,
            "map_local" => ObjectSharingScope.MapLocal,
            _ => throw new CoreDomainDataException(
                $"sharing_scope \"{entity.sharing_scope}\" 不可识别。"),
        };
        var lifecycle = entity.lifecycle_status switch
        {
            "draft" => ObjectLifecycleStatus.Draft,
            "active" => ObjectLifecycleStatus.Active,
            "abandoned" => ObjectLifecycleStatus.Abandoned,
            "retired" => ObjectLifecycleStatus.Retired,
            _ => throw new CoreDomainDataException(
                $"lifecycle_status \"{entity.lifecycle_status}\" 不可识别。"),
        };
        if ((scope == ObjectSharingScope.ProjectShared && entity.owner_map_id is not null)
            || (scope == ObjectSharingScope.MapLocal && entity.owner_map_id is null))
        {
            throw new CoreDomainDataException(
                $"Object {entity.object_identity_id} 的 scope/owner 组合违反不变量（R-scope）。");
        }

        return new ObjectRecord
        {
            ObjectIdentityId = entity.object_identity_id,
            ProjectId = entity.project_id,
            ObjectTypeId = entity.object_type_id,
            ObjectTypeSchemaVersion = schemaVersion,
            ObjectTypeSemanticsId = semanticsId,
            SharingScope = scope,
            OwnerMapId = entity.owner_map_id,
            LifecycleStatus = lifecycle,
            CreatedAtUtcMs = entity.created_at_utc_ms,
            UpdatedAtUtcMs = entity.updated_at_utc_ms,
        };
    }

    private static ObjectRecord MapObject(SqliteDataReader reader)
    {
        var identityId = ParseUuid(reader.GetString(0));
        var schemaVersion = reader.GetInt64(3);
        var semanticsId = reader.GetString(4);
        var typeKey = reader.GetString(10);

        // 读侧 fail-closed：未知 pin 拒绝解释（Handoff §5.4；P2-09-18 §10）。
        ObjectRules.ValidateKnownPin(typeKey, schemaVersion, semanticsId);

        var scopeText = reader.GetString(5);
        var scope = scopeText switch
        {
            "project_shared" => ObjectSharingScope.ProjectShared,
            "map_local" => ObjectSharingScope.MapLocal,
            _ => throw new CoreDomainDataException($"sharing_scope \"{scopeText}\" 不可识别。"),
        };
        var lifecycleText = reader.GetString(7);
        var lifecycle = lifecycleText switch
        {
            "draft" => ObjectLifecycleStatus.Draft,
            "active" => ObjectLifecycleStatus.Active,
            "abandoned" => ObjectLifecycleStatus.Abandoned,
            "retired" => ObjectLifecycleStatus.Retired,
            _ => throw new CoreDomainDataException($"lifecycle_status \"{lifecycleText}\" 不可识别。"),
        };
        Guid? ownerMapId = reader.IsDBNull(6) ? null : ParseUuid(reader.GetString(6));
        if ((scope == ObjectSharingScope.ProjectShared && ownerMapId is not null)
            || (scope == ObjectSharingScope.MapLocal && ownerMapId is null))
        {
            throw new CoreDomainDataException($"Object {identityId} 的 scope/owner 组合违反不变量（R-scope）。");
        }

        return new ObjectRecord
        {
            ObjectIdentityId = identityId,
            ProjectId = ParseUuid(reader.GetString(1)),
            ObjectTypeId = ParseUuid(reader.GetString(2)),
            ObjectTypeSchemaVersion = schemaVersion,
            ObjectTypeSemanticsId = semanticsId,
            SharingScope = scope,
            OwnerMapId = ownerMapId,
            LifecycleStatus = lifecycle,
            CreatedAtUtcMs = reader.GetInt64(8),
            UpdatedAtUtcMs = reader.GetInt64(9),
        };
    }

    private static MapObjectRefRecord MapMapObjectRef(CoreMapObjectRefEntity entity) => new()
    {
        MapObjectRefId = entity.map_object_ref_id,
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        ObjectIdentityId = entity.object_identity_id,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

    private static MapObjectRefRecord MapMapObjectRef(SqliteDataReader reader) => new()
    {
        MapObjectRefId = ParseUuid(reader.GetString(0)),
        ProjectId = ParseUuid(reader.GetString(1)),
        MapId = ParseUuid(reader.GetString(2)),
        ObjectIdentityId = ParseUuid(reader.GetString(3)),
        CreatedAtUtcMs = reader.GetInt64(4),
    };

    private static MapInitialStateRecord MapInitialState(SqliteDataReader reader) => new()
    {
        InitialStateId = ParseUuid(reader.GetString(0)),
        ProjectId = ParseUuid(reader.GetString(1)),
        MapId = ParseUuid(reader.GetString(2)),
        BaselineSchemaVersion = reader.GetInt64(3),
        ContentFingerprint = reader.GetString(4),
        CreatedAtUtcMs = reader.GetInt64(5),
        UpdatedAtUtcMs = reader.GetInt64(6),
    };

    private static InitialObjectRecord MapInitialObject(SqliteDataReader reader)
    {
        long? placementVersion = reader.IsDBNull(6) ? null : reader.GetInt64(6);
        return new InitialObjectRecord
        {
            ProjectId = ParseUuid(reader.GetString(0)),
            MapId = ParseUuid(reader.GetString(1)),
            InitialStateId = ParseUuid(reader.GetString(2)),
            ObjectIdentityId = ParseUuid(reader.GetString(3)),
            MembershipPayloadJson = reader.GetString(4),
            PlacementPayloadJson = reader.IsDBNull(5) ? null : reader.GetString(5),
            PlacementSchemaVersion = placementVersion,
            CreatedAtUtcMs = reader.GetInt64(7),
        };
    }
}
