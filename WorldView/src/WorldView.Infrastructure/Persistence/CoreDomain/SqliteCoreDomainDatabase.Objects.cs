using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using WorldView.Domain.Aggregates;
using WorldView.Domain.Assets;
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

                // Flush pending EF rows inside the still-open transaction so the
                // transaction-scoped SQLite Initial State helper can observe a new registration.
                SaveChangesOrThrowSqlite(db);

                if (plan.InitialMembership is not null)
                {
                    // P3⇒P2：成员行必须有对应注册行（本事务刚写入）。
                    if (mapId is null
                        || plan.InitialRevisionAppend is null
                        || !db.MapObjectRefs.Any(x =>
                            x.project_id == plan.ProjectId
                            && x.map_id == mapId.Value
                            && x.object_identity_id == plan.ObjectIdentityId))
                    {
                        throw new CoreDomainConflictException(
                            "Initial State 成员缺少同 Map 注册行或 revision 追加上下文（P3⇒P2；FOUNDATION-01B §10），拒绝插入。");
                    }

                    AppendInsertObjectRevisionInDbContext(db, plan, mapId.Value);
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

    public void PublishObjectDefinitionWithGeometryAssets(ObjectDefinitionPublicationPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // 事务内重读 Object 生命周期与类型（不信任调用方快照）。
                var identityEntity = db.Objects.AsNoTracking().SingleOrDefault(x =>
                    x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId)
                    ?? throw new CoreDomainConflictException(
                        $"Object {plan.ObjectIdentityId} 不存在或不属于项目 {plan.ProjectId}。");
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
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态；仅 active Object 可发布 Geometry Definition。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);
                var descriptor = ObjectTypeRegistry.Require(typeKey);
                if (!descriptor.HasTypedAggregateSchema || typeKey is not ("building" or "cave_system"))
                {
                    throw new CoreDomainConflictException(
                        $"Object Type {typeKey} 没有已采纳的 Building/Cave typed Geometry publication adapter。");
                }

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
                ObjectTypeRegistry.ValidateDefinition(typeKey, canonicalPayload);

                // 校验既有 link set（仅形状/集合一致性），允许通过本次发布修复旧的不一致。
                var currentLinks = db.ObjectDefinitionAssets.AsNoTracking()
                    .Where(x => x.project_id == plan.ProjectId && x.object_identity_id == plan.ObjectIdentityId)
                    .OrderBy(x => x.asset_id)
                    .ThenBy(x => x.role)
                    .AsEnumerable()
                    .Select(x => (AssetId: x.asset_id.ToString("D"), Role: x.role))
                    .ToArray();
                ValidateGeometryPublicationContext(
                    db, plan.ProjectId, typeKey, current.definition_payload_json, currentLinks, requireVerifiedAssets: false);

                var desiredAssets = ReadGeometryAssetMap(canonicalPayload);
                var desiredLinks = desiredAssets.Keys
                    .OrderBy(id => id)
                    .Select(assetId => (AssetId: assetId.ToString("D"), Role: "geometry_component"))
                    .ToArray();
                ValidateGeometryPublicationContext(
                    db, plan.ProjectId, typeKey, canonicalPayload, desiredLinks, requireVerifiedAssets: true);

                // Building/Cave typed schema 只拥有 geometry_component links；其他 role 已在上面 fail-closed。
                var existingGeometryLinks = db.ObjectDefinitionAssets
                    .Where(x => x.project_id == plan.ProjectId
                        && x.object_identity_id == plan.ObjectIdentityId
                        && x.role == "geometry_component")
                    .ToList();
                db.ObjectDefinitionAssets.RemoveRange(existingGeometryLinks);
                SaveChangesOrThrowSqlite(db);

                foreach (var assetId in desiredAssets.Keys.OrderBy(id => id))
                {
                    db.ObjectDefinitionAssets.Add(new CoreObjectDefinitionAssetEntity
                    {
                        object_definition_asset_id = Guid.NewGuid(),
                        project_id = plan.ProjectId,
                        object_identity_id = plan.ObjectIdentityId,
                        asset_id = assetId,
                        role = "geometry_component",
                        created_at_utc_ms = plan.NowUtcMs,
                    });
                }

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

    private static Dictionary<Guid, string> ReadGeometryAssetMap(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("geometry_components", out var components)
            || components.ValueKind != JsonValueKind.Array)
        {
            throw new CoreDomainConflictException("typed Geometry Definition 缺少 geometry_components 数组。");
        }

        var assets = new Dictionary<Guid, string>();
        foreach (var component in components.EnumerateArray())
        {
            if (!component.TryGetProperty("asset_id", out var assetElement)
                || assetElement.ValueKind != JsonValueKind.String
                || !Guid.TryParseExact(assetElement.GetString(), "D", out var assetId)
                || !component.TryGetProperty("content_hash", out var hashElement)
                || hashElement.ValueKind != JsonValueKind.String
                || !AssetRules.IsSha256Hex(hashElement.GetString()!))
            {
                throw new CoreDomainConflictException("Geometry Component 的 asset_id/content_hash 格式非法。");
            }

            var contentHash = hashElement.GetString()!;
            if (assets.TryGetValue(assetId, out var existingHash))
            {
                if (!string.Equals(existingHash, contentHash, StringComparison.Ordinal))
                {
                    throw new CoreDomainConflictException(
                        $"Asset {assetId} 被同一 owner 的多个 Geometry Component 固定到不同 content_hash。");
                }
            }
            else
            {
                assets.Add(assetId, contentHash);
            }
        }

        return assets;
    }

    private static void ValidateGeometryPublicationContext(
        CoreDomainDbContext db,
        Guid projectId,
        string typeKey,
        string payloadJson,
        IReadOnlyList<(string AssetId, string Role)> links,
        bool requireVerifiedAssets)
    {
        var assets = ReadGeometryAssetMap(payloadJson);
        var catalog = new List<(string AssetId, string ContentHash)>();
        if (!requireVerifiedAssets)
        {
            // 仅校验既有 owner/link 形状。旧的 corrupt/missing Asset 不应阻止一次
            // 原子替换/移除来修复 Definition。
            catalog.AddRange(assets.Select(asset => (asset.Key.ToString("D"), asset.Value)));
        }
        else
        {
            foreach (var assetId in assets.Keys)
            {
                var asset = db.Assets.AsNoTracking().SingleOrDefault(x =>
                    x.ProjectId == projectId && x.AssetId == assetId);

                // 事务内重读 integrity state：只有 Verified 的 Asset 可进入正式发布。
                if (asset is not null && !string.Equals(asset.IntegrityState, "verified", StringComparison.Ordinal))
                {
                    throw new CoreDomainConflictException(
                        $"Asset {assetId} integrity_state 为 {asset.IntegrityState}；只有 Verified Asset 可发布到正式 Geometry Definition。");
                }

                if (asset is not null)
                {
                    catalog.Add((asset.AssetId.ToString("D"), asset.ContentHash));
                }

                // asset 不存在时由 aggregate validator 产生稳定的 missing-asset 错误。
            }
        }

        var issues = BuildingCaveAggregateValidator.Validate(
            typeKey,
            payloadJson,
            new AggregatePublicationContext
            {
                AssetLinks = links,
                AssetCatalog = catalog,
            });
        if (issues.Count > 0)
        {
            throw new CoreDomainConflictException(
                "Geometry Definition publication validation failed: "
                + string.Join("; ", issues.Select(issue => $"{issue.ErrorCode}: {issue.Message}")));
        }
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

                // P3⇒P2：任一 revision 快照仍含该身份成员时拒绝（revision 树不可变，
                // 成员行复合 FK 指向注册行，永久保留；移除成员须经 Initial State 编辑）。
                if (db.MapInitialStateRevisionMembers.Any(x =>
                    x.project_id == projectId
                    && x.map_id == mapId
                    && x.object_identity_id == objectIdentityId))
                {
                    throw new CoreDomainConflictException(
                        $"Object {objectIdentityId} 仍是 Map {mapId} 某个 Initial State revision 的成员，取消注册被拒绝（P3⇒P2）。");
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

    // ---- Map Initial State 修订（P3 存在层；FOUNDATION-01B §3/§6/§7/§10） ----

    public MapInitialStateBaselineRecord? FindMapInitialStateBaseline(Guid projectId, Guid mapId) =>
        _ef.Execute(db =>
        {
            var entity = db.MapInitialStates.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.map_id == mapId);
            return entity is null ? null : MapInitialStateBaseline(entity);
        });

    public MapInitialStateRevisionSnapshot? FindMapInitialStateRevisionSnapshot(
        Guid projectId, Guid mapId, Guid revisionId) =>
        _ef.Execute(db =>
        {
            var revision = db.MapInitialStateRevisions.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId
                && x.map_id == mapId
                && x.revision_id == revisionId);
            if (revision is null)
            {
                return null;
            }

            return new MapInitialStateRevisionSnapshot
            {
                Revision = MapInitialStateRevision(revision),
                Members = ListRevisionMembersInDbContext(db, projectId, mapId, revisionId),
            };
        });

    public IReadOnlyList<MapInitialStateRevisionRecord> ListMapInitialStateRevisions(Guid projectId, Guid mapId) =>
        _ef.Execute(db => (IReadOnlyList<MapInitialStateRevisionRecord>)db.MapInitialStateRevisions.AsNoTracking()
            .Where(x => x.project_id == projectId && x.map_id == mapId)
            .OrderBy(x => x.revision_number)
            .AsEnumerable()
            .Select(MapInitialStateRevision)
            .ToArray());

    public MapInitialStateRevisionState? FindMapInitialStateRevisionState(Guid projectId, Guid mapId) =>
        _ef.Execute(db =>
        {
            var baseline = db.MapInitialStates.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.map_id == mapId);
            if (baseline is null)
            {
                return null;
            }

            var cursor = db.MapInitialStateCursors.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.map_id == mapId)
                ?? throw new CoreDomainDataException(
                    $"Map {mapId} 存在 Initial State baseline 但缺少 cursor 行（数据损坏；FOUNDATION-01B §3.4）。");

            var activeRevision = db.MapInitialStateRevisions.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId
                && x.map_id == mapId
                && x.initial_state_id == cursor.initial_state_id
                && x.revision_id == cursor.active_revision_id)
                ?? throw new CoreDomainDataException(
                    $"Map {mapId} 的 cursor 指向不存在的 active revision {cursor.active_revision_id:D}（数据损坏）。");

            var redoChildren = db.MapInitialStateRevisions.AsNoTracking()
                .Where(x => x.project_id == projectId
                    && x.map_id == mapId
                    && x.initial_state_id == cursor.initial_state_id
                    && x.parent_revision_id == cursor.active_revision_id)
                .OrderBy(x => x.revision_number)
                .AsEnumerable()
                .Select(MapInitialStateRevision)
                .ToArray();

            return new MapInitialStateRevisionState
            {
                Snapshot = new MapInitialStateRevisionSnapshot
                {
                    Revision = MapInitialStateRevision(activeRevision),
                    Members = ListRevisionMembersInDbContext(db, projectId, mapId, cursor.active_revision_id),
                },
                Cursor = MapInitialStateCursor(cursor),
                RedoChildren = redoChildren,
            };
        });

    public void AppendMapInitialStateRevision(AppendMapInitialStateRevisionPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var cursor = RequireCursorInDbContext(db, plan.ProjectId, plan.MapId);
                ValidateExpectedCursorInDbContext(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
                if (cursor.initial_state_id != plan.InitialStateId)
                {
                    throw new CoreDomainDataException(
                        $"追加计划的 baseline {plan.InitialStateId} 与 Map 既有 baseline {cursor.initial_state_id} 不一致（fail-closed）。");
                }

                var parent = db.MapInitialStateRevisions.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId
                    && x.map_id == plan.MapId
                    && x.initial_state_id == cursor.initial_state_id
                    && x.revision_id == cursor.active_revision_id)
                    ?? throw new CoreDomainDataException(
                        $"Map {plan.MapId} 的 cursor 指向不存在的 active revision {cursor.active_revision_id:D}（数据损坏）。");

                var parentMembers = ListRevisionMembersInDbContext(db, plan.ProjectId, plan.MapId, parent.revision_id);
                var childMembers = CanonicalizeChildMembers(db, plan.ProjectId, plan.MapId, plan.Members);

                // 按提交行重算指纹与操作描述并拒绝不一致（FOUNDATION-01B §7）。
                var childFingerprint = InitialStateFingerprint.Compute(parent.baseline_schema_version, childMembers);
                RequireComputedContent(
                    "fingerprint", plan.ContentFingerprint, childFingerprint,
                    $"Map {plan.MapId} revision {plan.RevisionId:D}");
                var operationPayload = InitialStateRevisionRules.BuildOperationPayload(
                    parent.content_fingerprint, childFingerprint, parentMembers, childMembers);
                RequireComputedContent(
                    "operation payload", plan.OperationPayloadJson, operationPayload,
                    $"Map {plan.MapId} revision {plan.RevisionId:D}");

                AppendRevisionInDbContext(
                    db, plan.ProjectId, plan.MapId, plan.InitialStateId,
                    parentRevisionId: parent.revision_id,
                    newRevisionId: plan.RevisionId,
                    baselineSchemaVersion: parent.baseline_schema_version,
                    contentFingerprint: childFingerprint,
                    operationPayloadJson: operationPayload,
                    members: childMembers,
                    parentMembers: parentMembers,
                    nowUtcMs: plan.NowUtcMs);

                SetCursorInDbContext(
                    db, cursor,
                    activeRevisionId: plan.RevisionId,
                    selectedRedoChildRevisionId: null,
                    cursorVersion: plan.ExpectedCursorVersion + 1,
                    nowUtcMs: plan.NowUtcMs);

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

    public void MoveMapInitialStateCursor(MoveMapInitialStateCursorPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var cursor = RequireCursorInDbContext(db, plan.ProjectId, plan.MapId);
                ValidateExpectedCursorInDbContext(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
                if (cursor.initial_state_id != plan.InitialStateId)
                {
                    throw new CoreDomainDataException(
                        $"cursor 计划的 baseline {plan.InitialStateId} 与 Map 既有 baseline {cursor.initial_state_id} 不一致（fail-closed）。");
                }

                var current = db.MapInitialStateRevisions.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId
                    && x.map_id == plan.MapId
                    && x.initial_state_id == cursor.initial_state_id
                    && x.revision_id == cursor.active_revision_id)
                    ?? throw new CoreDomainDataException(
                        $"Map {plan.MapId} 的 cursor 指向不存在的 active revision {cursor.active_revision_id:D}（数据损坏）。");

                if (plan.IsRedo)
                {
                    // Redo 目标必须是活动 revision 的直接子且同 baseline（复合 FK + 显式校验）。
                    var target = db.MapInitialStateRevisions.SingleOrDefault(x =>
                        x.project_id == plan.ProjectId
                        && x.map_id == plan.MapId
                        && x.initial_state_id == cursor.initial_state_id
                        && x.revision_id == plan.TargetRevisionId);
                    InitialStateRevisionRules.ValidateDirectChild(
                        current.revision_id, plan.TargetRevisionId, target?.parent_revision_id);
                    if (target is null)
                    {
                        throw new CoreDomainConflictException(
                            $"Redo 目标 revision {plan.TargetRevisionId:D} 不存在于 Map {plan.MapId} 的 baseline。");
                    }

                    SetCursorInDbContext(
                        db, cursor,
                        activeRevisionId: target.revision_id,
                        selectedRedoChildRevisionId: null,
                        cursorVersion: plan.ExpectedCursorVersion + 1,
                        nowUtcMs: plan.NowUtcMs);
                }
                else
                {
                    if (current.parent_revision_id is null)
                    {
                        throw new CoreDomainConflictException(
                            "Revision zero（baseline root）不可 Undo：无 parent revision。");
                    }

                    // Undo：cursor 移到 parent；selected redo child 指向刚离开的 revision
                    // （direct-child 谓词由 cursor 触发器与本次写入天然满足）。
                    SetCursorInDbContext(
                        db, cursor,
                        activeRevisionId: current.parent_revision_id.Value,
                        selectedRedoChildRevisionId: current.revision_id,
                        cursorVersion: plan.ExpectedCursorVersion + 1,
                        nowUtcMs: plan.NowUtcMs);
                }

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

    public void SelectMapInitialStateRedoChild(SelectMapInitialStateRedoChildPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var cursor = RequireCursorInDbContext(db, plan.ProjectId, plan.MapId);
                ValidateExpectedCursorInDbContext(cursor, plan.ExpectedActiveRevisionId, plan.ExpectedCursorVersion);
                if (cursor.initial_state_id != plan.InitialStateId)
                {
                    throw new CoreDomainDataException(
                        $"redo 选择计划的 baseline {plan.InitialStateId} 与 Map 既有 baseline {cursor.initial_state_id} 不一致（fail-closed）。");
                }

                var child = db.MapInitialStateRevisions.SingleOrDefault(x =>
                    x.project_id == plan.ProjectId
                    && x.map_id == plan.MapId
                    && x.initial_state_id == cursor.initial_state_id
                    && x.revision_id == plan.ChildRevisionId);
                InitialStateRevisionRules.ValidateDirectChild(
                    cursor.active_revision_id, plan.ChildRevisionId, child?.parent_revision_id);
                if (child is null)
                {
                    throw new CoreDomainConflictException(
                        $"redo 选择目标 revision {plan.ChildRevisionId:D} 不存在于 Map {plan.MapId} 的 baseline。");
                }

                SetCursorInDbContext(
                    db, cursor,
                    activeRevisionId: cursor.active_revision_id,
                    selectedRedoChildRevisionId: plan.ChildRevisionId,
                    cursorVersion: plan.ExpectedCursorVersion + 1,
                    nowUtcMs: plan.NowUtcMs);

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

    // ---- 事务内写路径辅助 ----

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
    /// InsertObject 事务内的 Initial State child revision 追加（FOUNDATION-01B §10）：
    /// 事务内重读 cursor 重验 expected active revision + cursor version；从权威父快照
    /// 派生子快照（父成员 + 本事务新成员），重算指纹/操作描述并拒绝与 Domain 计划不一致，
    /// 同事务写入 revision、成员行与 cursor 前移。任何失败随外层事务整体回滚。
    /// </summary>
    private static void AppendInsertObjectRevisionInDbContext(
        CoreDomainDbContext db,
        ObjectInsertPlan plan,
        Guid mapId)
    {
        var append = plan.InitialRevisionAppend!;
        var cursor = RequireCursorInDbContext(db, plan.ProjectId, mapId);
        ValidateExpectedCursorInDbContext(cursor, append.ExpectedActiveRevisionId, append.ExpectedCursorVersion);
        if (cursor.initial_state_id != append.InitialStateId)
        {
            throw new CoreDomainDataException(
                $"Insert 计划的 baseline {append.InitialStateId} 与 Map 既有 baseline {cursor.initial_state_id} 不一致（fail-closed）。");
        }

        var parent = db.MapInitialStateRevisions.SingleOrDefault(x =>
            x.project_id == plan.ProjectId
            && x.map_id == mapId
            && x.initial_state_id == cursor.initial_state_id
            && x.revision_id == cursor.active_revision_id)
            ?? throw new CoreDomainDataException(
                $"Map {mapId} 的 cursor 指向不存在的 active revision {cursor.active_revision_id:D}（数据损坏）。");

        var parentMembers = ListRevisionMembersInDbContext(db, plan.ProjectId, mapId, parent.revision_id);
        if (parentMembers.Any(m => m.ObjectIdentityId == plan.ObjectIdentityId))
        {
            throw new CoreDomainConflictException(
                $"身份 {plan.ObjectIdentityId} 已是 Map {mapId} 活动 revision 的 Initial State 成员，拒绝重复追加。");
        }

        var newMember = new InitialObjectWrite
        {
            ObjectIdentityId = plan.ObjectIdentityId,
            MembershipPayloadJson = CanonicalJson.Canonicalize(plan.InitialMembership!.MembershipPayloadJson),
            PlacementPayloadJson = plan.InitialMembership.PlacementPayloadJson is null
                ? null
                : CanonicalJson.Canonicalize(plan.InitialMembership.PlacementPayloadJson),
            PlacementSchemaVersion = plan.InitialMembership.PlacementSchemaVersion,
        };
        if (newMember.PlacementPayloadJson is null != newMember.PlacementSchemaVersion is null)
        {
            throw new CoreDomainConflictException(
                $"成员 {plan.ObjectIdentityId} 的 placement payload 与 schema version 必须成对提供。");
        }

        var childMembers = parentMembers
            .Select(m => new InitialObjectWrite
            {
                ObjectIdentityId = m.ObjectIdentityId,
                MembershipPayloadJson = m.MembershipPayloadJson,
                PlacementPayloadJson = m.PlacementPayloadJson,
                PlacementSchemaVersion = m.PlacementSchemaVersion,
            })
            .Append(newMember)
            .ToArray();

        var childFingerprint = InitialStateFingerprint.Compute(parent.baseline_schema_version, childMembers);
        RequireComputedContent(
            "fingerprint", append.ContentFingerprint, childFingerprint,
            $"Map {mapId} revision {append.NewRevisionId:D}");
        var operationPayload = InitialStateRevisionRules.BuildOperationPayload(
            parent.content_fingerprint, childFingerprint, parentMembers, childMembers);
        RequireComputedContent(
            "operation payload", append.OperationPayloadJson, operationPayload,
            $"Map {mapId} revision {append.NewRevisionId:D}");

        AppendRevisionInDbContext(
            db, plan.ProjectId, mapId, cursor.initial_state_id,
            parentRevisionId: parent.revision_id,
            newRevisionId: append.NewRevisionId,
            baselineSchemaVersion: parent.baseline_schema_version,
            contentFingerprint: childFingerprint,
            operationPayloadJson: operationPayload,
            members: childMembers,
            parentMembers: parentMembers,
            nowUtcMs: plan.NowUtcMs);

        SetCursorInDbContext(
            db, cursor,
            activeRevisionId: append.NewRevisionId,
            selectedRedoChildRevisionId: null,
            cursorVersion: append.ExpectedCursorVersion + 1,
            nowUtcMs: plan.NowUtcMs);
    }

    // ---- Initial State revision 事务内辅助（调用方已持有事务与连接协调器临界区） ----

    /// <summary>事务内重读 cursor；baseline/cursor 缺失是数据损坏（无 lazy 建立）。</summary>
    private static CoreMapInitialStateCursorEntity RequireCursorInDbContext(
        CoreDomainDbContext db, Guid projectId, Guid mapId)
    {
        if (!db.MapInitialStates.Any(x => x.project_id == projectId && x.map_id == mapId))
        {
            throw new CoreDomainDataException(
                $"Map {mapId} 缺少 Initial State baseline 身份行（数据损坏；Map 创建必须同事务建立）。");
        }

        return db.MapInitialStateCursors.SingleOrDefault(x => x.project_id == projectId && x.map_id == mapId)
            ?? throw new CoreDomainDataException(
                $"Map {mapId} 存在 Initial State baseline 但缺少 cursor 行（数据损坏；FOUNDATION-01B §3.4）。");
    }

    /// <summary>CAS：expected active revision + cursor version 必须与当前行一致（stale 拒绝）。</summary>
    private static void ValidateExpectedCursorInDbContext(
        CoreMapInitialStateCursorEntity cursor,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion)
    {
        if (cursor.active_revision_id != expectedActiveRevisionId
            || cursor.cursor_version != expectedCursorVersion)
        {
            throw new CoreDomainConflictException(
                $"Initial State cursor 冲突：expected (revision {expectedActiveRevisionId:D}, version {expectedCursorVersion}) "
                + $"与当前 (revision {cursor.active_revision_id:D}, version {cursor.cursor_version}) 不匹配。");
        }
    }

    /// <summary>cursor 是修订模型唯一的可变权威行；每次 mutation version 单调递增。</summary>
    private static void SetCursorInDbContext(
        CoreDomainDbContext db,
        CoreMapInitialStateCursorEntity cursor,
        Guid activeRevisionId,
        Guid? selectedRedoChildRevisionId,
        long cursorVersion,
        long nowUtcMs)
    {
        cursor.active_revision_id = activeRevisionId;
        cursor.selected_redo_child_revision_id = selectedRedoChildRevisionId;
        cursor.cursor_version = cursorVersion;
        cursor.updated_at_utc_ms = nowUtcMs;
        db.MapInitialStateCursors.Update(cursor);
    }

    /// <summary>
    /// 事务内校验并 canonical 化完整子快照成员集合：唯一身份、placement 成对、
    /// identity 存在且 active、有本 Map 注册行（P3⇒P2）。
    /// </summary>
    private static List<InitialObjectWrite> CanonicalizeChildMembers(
        CoreDomainDbContext db,
        Guid projectId,
        Guid mapId,
        IReadOnlyList<InitialObjectWrite> members)
    {
        var seen = new HashSet<Guid>();
        var result = new List<InitialObjectWrite>(members.Count);
        foreach (var member in members)
        {
            if (member.PlacementPayloadJson is null != member.PlacementSchemaVersion is null)
            {
                throw new CoreDomainConflictException(
                    $"成员 {member.ObjectIdentityId} 的 placement payload 与 schema version 必须成对提供。");
            }

            if (!seen.Add(member.ObjectIdentityId))
            {
                throw new CoreDomainConflictException($"Initial State 包含重复成员 {member.ObjectIdentityId}。");
            }

            var identity = db.Objects.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId && x.object_identity_id == member.ObjectIdentityId);
            if (identity is null || identity.lifecycle_status != "active")
            {
                throw new CoreDomainConflictException(
                    $"成员 {member.ObjectIdentityId} 不存在或不是 active 状态，不得进入 Initial State。");
            }

            if (!db.MapObjectRefs.Any(x =>
                x.project_id == projectId
                && x.map_id == mapId
                && x.object_identity_id == member.ObjectIdentityId))
            {
                throw new CoreDomainConflictException(
                    $"成员 {member.ObjectIdentityId} 缺少 Map {mapId} 注册行（P3⇒P2），拒绝写入。");
            }

            result.Add(new InitialObjectWrite
            {
                ObjectIdentityId = member.ObjectIdentityId,
                MembershipPayloadJson = CanonicalJson.Canonicalize(member.MembershipPayloadJson),
                PlacementPayloadJson = member.PlacementPayloadJson is null
                    ? null
                    : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
                PlacementSchemaVersion = member.PlacementSchemaVersion,
            });
        }

        return result;
    }

    /// <summary>
    /// 插入 immutable child revision 行与完整快照成员行。成员行的 created_at 保留
    /// 父快照中内容未变成员的原值（provenance：成员关系首次建立时刻），内容新增或
    /// 变化的成员取本次 revision 时刻；指纹不含时间戳，两种取值都不影响内容校验。
    /// </summary>
    private static void AppendRevisionInDbContext(
        CoreDomainDbContext db,
        Guid projectId,
        Guid mapId,
        Guid initialStateId,
        Guid parentRevisionId,
        Guid newRevisionId,
        long baselineSchemaVersion,
        string contentFingerprint,
        string operationPayloadJson,
        IReadOnlyList<InitialObjectWrite> members,
        IReadOnlyList<InitialObjectRecord> parentMembers,
        long nowUtcMs)
    {
        var nextRevisionNumber = db.MapInitialStateRevisions
            .Where(x => x.project_id == projectId
                && x.map_id == mapId
                && x.initial_state_id == initialStateId)
            .AsEnumerable()
            .Select(x => x.revision_number)
            .DefaultIfEmpty(0)
            .Max() + 1;

        db.MapInitialStateRevisions.Add(new CoreMapInitialStateRevisionEntity
        {
            revision_id = newRevisionId,
            project_id = projectId,
            map_id = mapId,
            initial_state_id = initialStateId,
            revision_number = nextRevisionNumber,
            parent_revision_id = parentRevisionId,
            baseline_schema_version = baselineSchemaVersion,
            content_fingerprint = contentFingerprint,
            operation_kind = InitialStateRevisionRules.SnapshotReplaceOperationKind,
            operation_payload_json = operationPayloadJson,
            created_at_utc_ms = nowUtcMs,
        });

        var parentByPayload = parentMembers.ToDictionary(
            m => m.ObjectIdentityId,
            m => (m.MembershipPayloadJson, m.PlacementPayloadJson, m.PlacementSchemaVersion, m.CreatedAtUtcMs));
        foreach (var member in members)
        {
            var preservedCreatedAt = nowUtcMs;
            if (parentByPayload.TryGetValue(member.ObjectIdentityId, out var parent)
                && string.Equals(parent.MembershipPayloadJson, member.MembershipPayloadJson, StringComparison.Ordinal)
                && string.Equals(parent.PlacementPayloadJson, member.PlacementPayloadJson, StringComparison.Ordinal)
                && parent.PlacementSchemaVersion == member.PlacementSchemaVersion)
            {
                preservedCreatedAt = parent.CreatedAtUtcMs;
            }

            db.MapInitialStateRevisionMembers.Add(new CoreMapInitialStateRevisionMemberEntity
            {
                project_id = projectId,
                map_id = mapId,
                initial_state_id = initialStateId,
                revision_id = newRevisionId,
                object_identity_id = member.ObjectIdentityId,
                initial_membership_payload_json = member.MembershipPayloadJson,
                initial_placement_payload_json = member.PlacementPayloadJson,
                initial_placement_schema_version = member.PlacementSchemaVersion,
                created_at_utc_ms = preservedCreatedAt,
            });
        }
    }

    /// <summary>按提交行重算结果与 Domain 计划不一致即 fail-closed（FOUNDATION-01B §7）。</summary>
    private static void RequireComputedContent(
        string kind, string planned, string computed, string subject)
    {
        if (!string.Equals(planned, computed, StringComparison.Ordinal))
        {
            throw new CoreDomainDataException(
                $"{subject} 的 {kind} 与 Domain 计划不一致（planned≠computed，fail-closed）："
                + $"planned={planned}, computed={computed}。");
        }
    }

    private static IReadOnlyList<InitialObjectRecord> ListRevisionMembersInDbContext(
        CoreDomainDbContext db, Guid projectId, Guid mapId, Guid revisionId) =>
        db.MapInitialStateRevisionMembers.AsNoTracking()
            .Where(x => x.project_id == projectId
                && x.map_id == mapId
                && x.revision_id == revisionId)
            .OrderBy(x => x.object_identity_id)
            .AsEnumerable()
            .Select(MapInitialRevisionMember)
            .ToArray();

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

    private static MapObjectRefRecord MapMapObjectRef(CoreMapObjectRefEntity entity) => new()
    {
        MapObjectRefId = entity.map_object_ref_id,
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        ObjectIdentityId = entity.object_identity_id,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

    private static MapInitialStateBaselineRecord MapInitialStateBaseline(CoreMapInitialStateEntity entity) => new()
    {
        InitialStateId = entity.initial_state_id,
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

    private static MapInitialStateRevisionRecord MapInitialStateRevision(CoreMapInitialStateRevisionEntity entity) => new()
    {
        RevisionId = entity.revision_id,
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        InitialStateId = entity.initial_state_id,
        RevisionNumber = entity.revision_number,
        ParentRevisionId = entity.parent_revision_id,
        BaselineSchemaVersion = entity.baseline_schema_version,
        ContentFingerprint = entity.content_fingerprint,
        OperationKind = entity.operation_kind,
        OperationPayloadJson = entity.operation_payload_json,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

    private static MapInitialStateCursorRecord MapInitialStateCursor(CoreMapInitialStateCursorEntity entity) => new()
    {
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        InitialStateId = entity.initial_state_id,
        ActiveRevisionId = entity.active_revision_id,
        SelectedRedoChildRevisionId = entity.selected_redo_child_revision_id,
        CursorVersion = entity.cursor_version,
        UpdatedAtUtcMs = entity.updated_at_utc_ms,
    };

    private static InitialObjectRecord MapInitialRevisionMember(CoreMapInitialStateRevisionMemberEntity entity) => new()
    {
        ProjectId = entity.project_id,
        MapId = entity.map_id,
        InitialStateId = entity.initial_state_id,
        RevisionId = entity.revision_id,
        ObjectIdentityId = entity.object_identity_id,
        MembershipPayloadJson = entity.initial_membership_payload_json,
        PlacementPayloadJson = entity.initial_placement_payload_json,
        PlacementSchemaVersion = entity.initial_placement_schema_version,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

}
