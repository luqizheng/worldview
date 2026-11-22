using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WorldView.Domain.Aggregates;
using WorldView.Domain.Assets;
using Microsoft.Data.Sqlite;
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

    public IReadOnlyList<ObjectTypeRecord> ListObjectTypes(Guid projectId)
    {
        const string sql = """
            SELECT object_type_id, project_id, type_key, display_name, allocatable,
                   allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_object_type
            WHERE project_id = $projectId
            ORDER BY type_key;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            var rows = new List<ObjectTypeRecord>();
            while (reader.Read())
            {
                rows.Add(MapObjectType(reader));
            }

            return (IReadOnlyList<ObjectTypeRecord>)rows;
        });
    }

    public ObjectTypeRecord? FindObjectType(Guid projectId, Guid objectTypeId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadObjectTypeRow(transaction, projectId, objectTypeId);
        });

    public ObjectTypeRecord? FindObjectTypeByTypeKey(Guid projectId, string typeKey)
    {
        const string sql = """
            SELECT object_type_id, project_id, type_key, display_name, allocatable,
                   allocation_format_json, allocation_format_version, created_at_utc_ms, updated_at_utc_ms
            FROM core_object_type
            WHERE project_id = $projectId AND type_key = $typeKey;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$typeKey", ObjectRules.NormalizeTypeKey(typeKey));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapObjectType(reader) : null;
        });
    }

    // ---- Object ID Reservation ----

    public ReservationRecord? FindReservation(Guid projectId, Guid reservationId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadReservationRow(transaction, projectId, reservationId);
        });

    public ReservationAllocation ReserveObjectId(ObjectIdReservationRequest request)
    {
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // request_key 幂等对账（P2-09-04 §6；TX-21/22）：响应丢失的重试返回同一分配。
                if (request.RequestKey is not null)
                {
                    var recovered = TryReadReservationByRequestKey(transaction, request.ProjectId, request.RequestKey);
                    if (recovered is not null)
                    {
                        if (recovered.ObjectTypeId != request.ObjectTypeId)
                        {
                            throw new CoreDomainConflictException(
                                $"request_key \"{request.RequestKey}\" 已属于不同 Object Type 的分配，拒绝幂等复用。");
                        }

                        transaction.Commit();
                        return new ReservationAllocation { Reservation = recovered, RecoveredByRequestKey = true };
                    }
                }

                var type = ReadObjectTypeRow(transaction, request.ProjectId, request.ObjectTypeId);
                if (!type.Allocatable)
                {
                    throw new CoreDomainConflictException($"Object Type \"{type.TypeKey}\" 不可分配（allocatable=false）。");
                }

                // allocator 读改写：next_sequence 仅在本事务内前进、永不回退（P2-09-11 §4.2）。
                long nextSequence;
                using (var read = connection.CreateCommand())
                {
                    read.Transaction = transaction;
                    read.CommandText = """
                        SELECT next_sequence FROM core_object_id_allocator
                        WHERE project_id = $projectId AND object_type_id = $typeId;
                        """;
                    read.Parameters.AddWithValue("$projectId", Uuid(request.ProjectId));
                    read.Parameters.AddWithValue("$typeId", Uuid(request.ObjectTypeId));
                    var value = read.ExecuteScalar();
                    if (value is null)
                    {
                        throw new CoreDomainDataException(
                            $"Object Type {request.ObjectTypeId} 缺少 allocator 行（应随 Project 播种建立，数据损坏）。");
                    }

                    nextSequence = (long)value;
                }

                using (var advance = connection.CreateCommand())
                {
                    advance.Transaction = transaction;
                    advance.CommandText = """
                        UPDATE core_object_id_allocator
                        SET next_sequence = $next, updated_at_utc_ms = $now
                        WHERE project_id = $projectId AND object_type_id = $typeId;
                        """;
                    advance.Parameters.AddWithValue("$next", nextSequence + 1);
                    advance.Parameters.AddWithValue("$now", request.NowUtcMs);
                    advance.Parameters.AddWithValue("$projectId", Uuid(request.ProjectId));
                    advance.Parameters.AddWithValue("$typeId", Uuid(request.ObjectTypeId));
                    advance.ExecuteNonQuery();
                }

                var reservationId = Guid.NewGuid();
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO core_object_id_reservation
                            (reservation_id, project_id, object_type_id, object_identity_id,
                             allocated_sequence, allocated_id_value, reservation_state, request_key,
                             abandon_reason, created_at_utc_ms, updated_at_utc_ms)
                        VALUES ($reservationId, $projectId, $typeId, NULL,
                                $sequence, $idValue, 'Reserved', $requestKey,
                                NULL, $now, $now);
                        """;
                    insert.Parameters.AddWithValue("$reservationId", Uuid(reservationId));
                    insert.Parameters.AddWithValue("$projectId", Uuid(request.ProjectId));
                    insert.Parameters.AddWithValue("$typeId", Uuid(request.ObjectTypeId));
                    insert.Parameters.AddWithValue("$sequence", nextSequence);
                    insert.Parameters.AddWithValue("$idValue", ObjectAllocationFormat.DeriveAllocatedIdValue(nextSequence));
                    insert.Parameters.AddWithValue("$requestKey", (object?)request.RequestKey ?? DBNull.Value);
                    insert.Parameters.AddWithValue("$now", request.NowUtcMs);
                    insert.ExecuteNonQuery();
                }

                var reservation = ReadReservationRow(transaction, request.ProjectId, reservationId);
                transaction.Commit();
                return new ReservationAllocation { Reservation = reservation, RecoveredByRequestKey = false };
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
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // 事务内重读前置状态（CD-02-A §13.3：状态机 UPDATE 不依赖调用方传值）。
                var reservation = ReadReservationRow(transaction, request.ProjectId, request.ReservationId);
                ReservationRules.ValidateTransition(reservation.State, ReservationState.Bound);

                var type = ReadObjectTypeRow(transaction, request.ProjectId, reservation.ObjectTypeId);
                // pin 按 descriptor registry（CD-02-C 编译期权威）。
                var descriptor = ObjectTypeRegistry.Require(type.TypeKey);
                var semanticsId = descriptor.SemanticsId;
                ObjectRules.ValidatePin(type.TypeKey, descriptor.SchemaVersion, semanticsId);
                ObjectRules.ValidateScopePairing(request.SharingScope, request.OwnerMapId);

                if (request.SharingScope == ObjectSharingScope.MapLocal
                    && TryReadMapRow(transaction, request.ProjectId, request.OwnerMapId!.Value) is null)
                {
                    throw new CoreDomainConflictException(
                        $"owner Map {request.OwnerMapId.Value} 不存在或不属于项目 {request.ProjectId}。");
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO core_object
                            (object_identity_id, project_id, object_type_id,
                             object_type_schema_version, object_type_semantics_id,
                             sharing_scope, owner_map_id, lifecycle_status,
                             created_at_utc_ms, updated_at_utc_ms)
                        VALUES ($identityId, $projectId, $typeId,
                                $schemaVersion, $semanticsId,
                                $scope, $ownerMapId, 'draft',
                                $now, $now);
                        """;
                    insert.Parameters.AddWithValue("$identityId", Uuid(request.ObjectIdentityId));
                    insert.Parameters.AddWithValue("$projectId", Uuid(request.ProjectId));
                    insert.Parameters.AddWithValue("$typeId", Uuid(reservation.ObjectTypeId));
                    insert.Parameters.AddWithValue("$schemaVersion", descriptor.SchemaVersion);
                    insert.Parameters.AddWithValue("$semanticsId", semanticsId);
                    insert.Parameters.AddWithValue(
                        "$scope",
                        request.SharingScope == ObjectSharingScope.ProjectShared ? "project_shared" : "map_local");
                    insert.Parameters.AddWithValue(
                        "$ownerMapId",
                        request.OwnerMapId is null ? DBNull.Value : Uuid(request.OwnerMapId.Value));
                    insert.Parameters.AddWithValue("$now", request.NowUtcMs);
                    insert.ExecuteNonQuery();
                }

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = """
                        UPDATE core_object_id_reservation
                        SET object_identity_id = $identityId, reservation_state = 'Bound', updated_at_utc_ms = $now
                        WHERE project_id = $projectId AND reservation_id = $reservationId;
                        """;
                    update.Parameters.AddWithValue("$identityId", Uuid(request.ObjectIdentityId));
                    update.Parameters.AddWithValue("$now", request.NowUtcMs);
                    update.Parameters.AddWithValue("$projectId", Uuid(request.ProjectId));
                    update.Parameters.AddWithValue("$reservationId", Uuid(request.ReservationId));
                    update.ExecuteNonQuery();
                }

                var bound = ReadReservationRow(transaction, request.ProjectId, request.ReservationId);
                transaction.Commit();
                return bound;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public ReservationRecord AbandonReservation(Guid projectId, Guid reservationId, string? abandonReason, long nowUtcMs)
    {
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var reservation = ReadReservationRow(transaction, projectId, reservationId);
                ReservationRules.ValidateTransition(reservation.State, ReservationState.Abandoned);

                if (reservation.ObjectIdentityId is not null)
                {
                    // Bound→Abandoned：identity 行保留为 abandoned 墓碑（仅 draft 可降级）。
                    using var tombstone = connection.CreateCommand();
                    tombstone.Transaction = transaction;
                    tombstone.CommandText = """
                        UPDATE core_object
                        SET lifecycle_status = 'abandoned', updated_at_utc_ms = $now
                        WHERE project_id = $projectId AND object_identity_id = $identityId
                          AND lifecycle_status = 'draft';
                        """;
                    tombstone.Parameters.AddWithValue("$now", nowUtcMs);
                    tombstone.Parameters.AddWithValue("$projectId", Uuid(projectId));
                    tombstone.Parameters.AddWithValue("$identityId", Uuid(reservation.ObjectIdentityId.Value));
                    if (tombstone.ExecuteNonQuery() != 1)
                    {
                        throw new CoreDomainConflictException(
                            $"绑定身份 {reservation.ObjectIdentityId.Value} 不是 draft 状态，无法放弃（状态不一致，fail-closed）。");
                    }
                }

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE core_object_id_reservation
                    SET reservation_state = 'Abandoned', abandon_reason = $reason, updated_at_utc_ms = $now
                    WHERE project_id = $projectId AND reservation_id = $reservationId;
                    """;
                update.Parameters.AddWithValue("$reason", (object?)abandonReason ?? DBNull.Value);
                update.Parameters.AddWithValue("$now", nowUtcMs);
                update.Parameters.AddWithValue("$projectId", Uuid(projectId));
                update.Parameters.AddWithValue("$reservationId", Uuid(reservationId));
                update.ExecuteNonQuery();

                var abandoned = ReadReservationRow(transaction, projectId, reservationId);
                transaction.Commit();
                return abandoned;
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
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadObjectRow(transaction, projectId, objectIdentityId);
        });

    public ObjectDefinitionRecord? ReadObjectDefinition(Guid projectId, Guid objectIdentityId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadDefinitionRow(transaction, projectId, objectIdentityId);
        });

    public void InsertObject(ObjectInsertPlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // 事务内重读前置状态（TX-55：正式创建与 Bound→Inserted 原子）。
                var reservation = ReadReservationRow(transaction, plan.ProjectId, plan.ReservationId);
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

                var identity = ReadObjectRow(transaction, plan.ProjectId, plan.ObjectIdentityId);
                if (identity.LifecycleStatus != ObjectLifecycleStatus.Draft)
                {
                    throw new CoreDomainConflictException(
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态，仅 draft 可正式创建。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.Definition.DefinitionSchemaVersion);

                var mapId = plan.Registration?.MapId;
                if (plan.Registration is not null)
                {
                    RegisterInTransaction(transaction, plan.ProjectId, plan.Registration, identity);
                }

                // definition 首行（canonical payload + hash；每 identity 至多一行由 PK 保证）。
                var (canonicalPayload, definitionHash) = CanonicalJson.CanonicalizeAndHash(plan.Definition.DefinitionPayloadJson);
                using (var insertDefinition = connection.CreateCommand())
                {
                    insertDefinition.Transaction = transaction;
                    insertDefinition.CommandText = """
                        INSERT INTO core_object_definition
                            (project_id, object_identity_id, definition_schema_version,
                             definition_payload_json, definition_hash, updated_at_utc_ms)
                        VALUES ($projectId, $identityId, $schemaVersion, $payload, $hash, $now);
                        """;
                    insertDefinition.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insertDefinition.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                    insertDefinition.Parameters.AddWithValue("$schemaVersion", plan.Definition.DefinitionSchemaVersion);
                    insertDefinition.Parameters.AddWithValue("$payload", canonicalPayload);
                    insertDefinition.Parameters.AddWithValue("$hash", definitionHash);
                    insertDefinition.Parameters.AddWithValue("$now", plan.NowUtcMs);
                    insertDefinition.ExecuteNonQuery();
                }

                if (plan.InitialMembership is not null)
                {
                    // P3⇒P2：成员行必须有对应注册行（本事务刚写入）。
                    if (mapId is null
                        || TryReadMapObjectRefRow(transaction, plan.ProjectId, mapId.Value, plan.ObjectIdentityId) is null)
                    {
                        throw new CoreDomainConflictException("Initial State 成员缺少同 Map 注册行（P3⇒P2），拒绝插入。");
                    }

                    AppendInitialMemberInTransaction(
                        transaction,
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
                using (var activate = connection.CreateCommand())
                {
                    activate.Transaction = transaction;
                    activate.CommandText = """
                        UPDATE core_object
                        SET lifecycle_status = 'active', updated_at_utc_ms = $now
                        WHERE project_id = $projectId AND object_identity_id = $identityId
                          AND lifecycle_status = 'draft';
                        """;
                    activate.Parameters.AddWithValue("$now", plan.NowUtcMs);
                    activate.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    activate.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                    if (activate.ExecuteNonQuery() != 1)
                    {
                        throw new CoreDomainConflictException("身份激活失败（draft 前置不满足），事务回滚。");
                    }
                }

                using (var insertTransition = connection.CreateCommand())
                {
                    insertTransition.Transaction = transaction;
                    insertTransition.CommandText = """
                        UPDATE core_object_id_reservation
                        SET reservation_state = 'Inserted', updated_at_utc_ms = $now
                        WHERE project_id = $projectId AND reservation_id = $reservationId
                          AND reservation_state = 'Bound';
                        """;
                    insertTransition.Parameters.AddWithValue("$now", plan.NowUtcMs);
                    insertTransition.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insertTransition.Parameters.AddWithValue("$reservationId", Uuid(plan.ReservationId));
                    if (insertTransition.ExecuteNonQuery() != 1)
                    {
                        throw new CoreDomainConflictException("Reservation→Inserted 转换失败（Bound 前置不满足），事务回滚。");
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

    public void EditObjectDefinition(ObjectDefinitionEditPlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var identity = ReadObjectRow(transaction, plan.ProjectId, plan.ObjectIdentityId);
                if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
                {
                    throw new CoreDomainConflictException(
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态；仅 active Object 可编辑定义。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);

                // CAS 在事务内比对（先读后写在同一临界区，无并发窗口）。
                var current = TryReadDefinitionRow(transaction, plan.ProjectId, plan.ObjectIdentityId)
                    ?? throw new CoreDomainDataException(
                        $"active Object {plan.ObjectIdentityId} 缺少定义行（数据损坏，R-定义单行）。");
                if (!string.Equals(current.DefinitionHash, plan.ExpectedDefinitionHash, StringComparison.Ordinal))
                {
                    throw new CoreDomainConflictException("定义 CAS 冲突：expected hash 与当前定义不匹配。");
                }

                var (canonicalPayload, definitionHash) = CanonicalJson.CanonicalizeAndHash(plan.DefinitionPayloadJson);
                var typeKey = ReadObjectTypeKey(transaction, plan.ProjectId, plan.ObjectIdentityId);
                var descriptor = ObjectTypeRegistry.Require(typeKey);
                ObjectTypeRegistry.ValidateDefinition(typeKey, canonicalPayload);
                if (descriptor.HasTypedAggregateSchema)
                {
                    if (!string.Equals(
                        ReadGeometryComponents(current.DefinitionPayloadJson),
                        ReadGeometryComponents(canonicalPayload),
                        StringComparison.Ordinal))
                    {
                        throw new CoreDomainConflictException(
                            "Building/Cave geometry_components 变更必须通过 PublishObjectDefinitionWithGeometryAssets 原子发布。");
                    }

                    var currentLinks = ReadDefinitionAssetLinks(transaction, plan.ProjectId, plan.ObjectIdentityId);
                    ValidateGeometryPublicationContext(
                        transaction, plan.ProjectId, typeKey, current.DefinitionPayloadJson, currentLinks, requireVerifiedAssets: true);
                    ValidateGeometryPublicationContext(
                        transaction, plan.ProjectId, typeKey, canonicalPayload, currentLinks, requireVerifiedAssets: true);
                }
                else
                {
                    ValidateExistingDefinitionAssetLinks(transaction, plan.ProjectId, plan.ObjectIdentityId, canonicalPayload);
                }

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE core_object_definition
                    SET definition_schema_version = $schemaVersion,
                        definition_payload_json = $payload,
                        definition_hash = $hash,
                        updated_at_utc_ms = $now
                    WHERE project_id = $projectId AND object_identity_id = $identityId;
                    """;
                update.Parameters.AddWithValue("$schemaVersion", plan.DefinitionSchemaVersion);
                update.Parameters.AddWithValue("$payload", canonicalPayload);
                update.Parameters.AddWithValue("$hash", definitionHash);
                update.Parameters.AddWithValue("$now", plan.NowUtcMs);
                update.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                update.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
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

    public void PublishObjectDefinitionWithGeometryAssets(ObjectDefinitionPublicationPlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var identity = ReadObjectRow(transaction, plan.ProjectId, plan.ObjectIdentityId);
                if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
                {
                    throw new CoreDomainConflictException(
                        $"身份 {plan.ObjectIdentityId} 处于 {identity.LifecycleStatus} 状态；仅 active Object 可发布 Geometry Definition。");
                }

                ObjectRules.ValidateDefinitionSchemaVersion(identity, plan.DefinitionSchemaVersion);
                var typeKey = ReadObjectTypeKey(transaction, plan.ProjectId, plan.ObjectIdentityId);
                var descriptor = ObjectTypeRegistry.Require(typeKey);
                if (!descriptor.HasTypedAggregateSchema)
                {
                    throw new CoreDomainConflictException(
                        $"Object Type {typeKey} 没有 typed Geometry Component owner schema，拒绝原子 Geometry Asset 发布。");
                }

                var current = TryReadDefinitionRow(transaction, plan.ProjectId, plan.ObjectIdentityId)
                    ?? throw new CoreDomainDataException(
                        $"active Object {plan.ObjectIdentityId} 缺少定义行（数据损坏，R-定义单行）。");
                if (!string.Equals(current.DefinitionHash, plan.ExpectedDefinitionHash, StringComparison.Ordinal))
                {
                    throw new CoreDomainConflictException("定义 CAS 冲突：expected hash 与当前定义不匹配。");
                }

                var (canonicalPayload, definitionHash) = CanonicalJson.CanonicalizeAndHash(plan.DefinitionPayloadJson);
                ObjectTypeRegistry.ValidateDefinition(typeKey, canonicalPayload);

                // Refuse to silently repair or reinterpret an existing partial/legacy link set.
                var currentLinks = ReadDefinitionAssetLinks(transaction, plan.ProjectId, plan.ObjectIdentityId);
                ValidateGeometryPublicationContext(
                    transaction, plan.ProjectId, typeKey, current.DefinitionPayloadJson, currentLinks, requireVerifiedAssets: false);

                var desiredAssets = ReadGeometryAssetMap(canonicalPayload);
                var desiredLinks = desiredAssets.Keys
                    .Select(assetId => (AssetId: assetId.ToString("D"), Role: "geometry_component"))
                    .ToArray();
                ValidateGeometryPublicationContext(
                    transaction, plan.ProjectId, typeKey, canonicalPayload, desiredLinks, requireVerifiedAssets: true);

                // Replace only the adopted geometry link purpose. Other explicitly typed
                // definition link purposes are preserved for future descriptor extensions.
                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = """
                        DELETE FROM core_object_definition_asset
                        WHERE project_id = $projectId
                          AND object_identity_id = $identityId
                          AND role = 'geometry_component';
                        """;
                    delete.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    delete.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                    delete.ExecuteNonQuery();
                }

                foreach (var assetId in desiredAssets.Keys.OrderBy(id => id))
                {
                    using var insertLink = connection.CreateCommand();
                    insertLink.Transaction = transaction;
                    insertLink.CommandText = """
                        INSERT INTO core_object_definition_asset
                            (object_definition_asset_id, project_id, object_identity_id, asset_id, role, created_at_utc_ms)
                        VALUES ($linkId, $projectId, $identityId, $assetId, 'geometry_component', $createdAt);
                        """;
                    insertLink.Parameters.AddWithValue("$linkId", Uuid(Guid.NewGuid()));
                    insertLink.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insertLink.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                    insertLink.Parameters.AddWithValue("$assetId", Uuid(assetId));
                    insertLink.Parameters.AddWithValue("$createdAt", plan.NowUtcMs);
                    insertLink.ExecuteNonQuery();
                }

                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE core_object_definition
                    SET definition_schema_version = $schemaVersion,
                        definition_payload_json = $payload,
                        definition_hash = $hash,
                        updated_at_utc_ms = $now
                    WHERE project_id = $projectId AND object_identity_id = $identityId;
                    """;
                update.Parameters.AddWithValue("$schemaVersion", plan.DefinitionSchemaVersion);
                update.Parameters.AddWithValue("$payload", canonicalPayload);
                update.Parameters.AddWithValue("$hash", definitionHash);
                update.Parameters.AddWithValue("$now", plan.NowUtcMs);
                update.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                update.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                if (update.ExecuteNonQuery() != 1)
                {
                    throw new CoreDomainDataException("Definition 原子发布未能更新唯一 owner 行。");
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

    private static string ReadObjectTypeKey(SqliteTransaction transaction, Guid projectId, Guid objectIdentityId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT t.type_key
            FROM core_object o
            JOIN core_object_type t
              ON t.project_id = o.project_id AND t.object_type_id = o.object_type_id
            WHERE o.project_id = $projectId AND o.object_identity_id = $identityId;
            """;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        return command.ExecuteScalar() as string
            ?? throw new CoreDomainDataException($"Object {objectIdentityId} 的 Object Type 缺失（数据损坏）。");
    }

    private static string ReadGeometryComponents(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("geometry_components", out var components)
            ? components.GetRawText()
            : "null";
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

    private static IReadOnlyList<(string AssetId, string Role)> ReadDefinitionAssetLinks(
        SqliteTransaction transaction, Guid projectId, Guid objectIdentityId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT asset_id, role
            FROM core_object_definition_asset
            WHERE project_id = $projectId AND object_identity_id = $identityId
            ORDER BY asset_id, role;
            """;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = command.ExecuteReader();
        var links = new List<(string AssetId, string Role)>();
        while (reader.Read())
        {
            links.Add((reader.GetString(0), reader.GetString(1)));
        }

        return links;
    }

    private void ValidateGeometryPublicationContext(
        SqliteTransaction transaction,
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
            // Validate only the previous owner/link shape. A corrupt/missing old Asset
            // must not prevent an atomic replacement/removal that repairs the Definition.
            catalog.AddRange(assets.Select(asset => (asset.Key.ToString("D"), asset.Value)));
        }
        else
        {
            foreach (var assetId in assets.Keys)
            {
                var asset = TryReadAssetRow(transaction, projectId, assetId);
                if (asset is null)
                {
                    continue; // Aggregate validator emits the stable missing-Asset issue.
                }

                if (asset.IntegrityState != AssetIntegrityState.Verified)
                {
                    throw new CoreDomainConflictException(
                        $"Asset {assetId} integrity_state is {asset.IntegrityState}; only Verified Assets can be published in a formal Geometry Definition.");
                }

                catalog.Add((asset.AssetId.ToString("D"), asset.ContentHash));
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
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadMapObjectRefRow(transaction, projectId, mapId, objectIdentityId);
        });

    public IReadOnlyList<MapObjectRefRecord> ListMapObjectRefs(Guid projectId, Guid mapId)
    {
        const string sql = """
            SELECT map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms
            FROM core_map_object_ref
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
            var rows = new List<MapObjectRefRecord>();
            while (reader.Read())
            {
                rows.Add(MapMapObjectRef(reader));
            }

            return (IReadOnlyList<MapObjectRefRecord>)rows;
        });
    }

    public void RegisterMapObject(MapObjectRegistrationPlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var identity = ReadObjectRow(transaction, plan.ProjectId, plan.ObjectIdentityId);
                RegisterInTransaction(
                    transaction,
                    plan.ProjectId,
                    new MapObjectRegistration { MapObjectRefId = plan.MapObjectRefId, MapId = plan.MapId },
                    identity);
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
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                if (TryReadMapObjectRefRow(transaction, projectId, mapId, objectIdentityId) is null)
                {
                    throw new CoreDomainConflictException($"Object {objectIdentityId} 未注册到 Map {mapId}，无法取消。");
                }

                // P3⇒P2：仍有 Initial State 成员行时拒绝（须先经 Initial State 编辑移除成员）。
                if (TryReadInitialObjectRow(transaction, projectId, mapId, objectIdentityId) is not null)
                {
                    throw new CoreDomainConflictException(
                        $"Object {objectIdentityId} 仍是 Map {mapId} Initial State 成员，须先移除成员再取消注册（P3⇒P2）。");
                }

                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM core_map_object_ref
                    WHERE project_id = $projectId AND map_id = $mapId AND object_identity_id = $identityId;
                    """;
                delete.Parameters.AddWithValue("$projectId", Uuid(projectId));
                delete.Parameters.AddWithValue("$mapId", Uuid(mapId));
                delete.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
                delete.ExecuteNonQuery();

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

    /// <summary>注册写入（InsertObject 与 RegisterMapObject 共用）；scope 规则事务内校验 + 触发器纵深防御。</summary>
    private void RegisterInTransaction(SqliteTransaction transaction, Guid projectId, MapObjectRegistration registration, ObjectRecord identity)
    {
        if (TryReadMapRow(transaction, projectId, registration.MapId) is null)
        {
            throw new CoreDomainConflictException($"Map {registration.MapId} 不存在或不属于项目 {projectId}，拒绝注册。");
        }

        if (identity.SharingScope == ObjectSharingScope.MapLocal && identity.OwnerMapId != registration.MapId)
        {
            throw new CoreDomainConflictException("map_local Object 只能注册到 owner Map（R-注册）。");
        }

        if (TryReadMapObjectRefRow(transaction, projectId, registration.MapId, identity.ObjectIdentityId) is not null)
        {
            throw new CoreDomainConflictException(
                $"Object {identity.ObjectIdentityId} 已注册到 Map {registration.MapId}，拒绝重复注册。");
        }

        using var insert = transaction.Connection!.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO core_map_object_ref
                (map_object_ref_id, project_id, map_id, object_identity_id, created_at_utc_ms)
            VALUES ($refId, $projectId, $mapId, $identityId, $now);
            """;
        insert.Parameters.AddWithValue("$refId", Uuid(registration.MapObjectRefId));
        insert.Parameters.AddWithValue("$projectId", Uuid(projectId));
        insert.Parameters.AddWithValue("$mapId", Uuid(registration.MapId));
        insert.Parameters.AddWithValue("$identityId", Uuid(identity.ObjectIdentityId));
        insert.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        insert.ExecuteNonQuery();
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
