using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object Definition 编辑服务（retroactive current-definition，P2-09-07 §2/P2-09-16 §2）。
/// expected-hash CAS：不匹配即 typed conflict（stale 更新拒绝）；无 revision 链；
/// 编辑不是 Command/Event，不改 History 运行时状态。
/// </summary>
public static class ObjectDefinitionService
{
    /// <summary>
    /// CAS 编辑当前定义。流程：读当前行 → 校验 expected hash（不匹配抛冲突）→
    /// 规范化新 payload 并计算 hash → 事务内重验 CAS 后重写。
    /// </summary>
    public static ObjectDefinitionRecord EditDefinition(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid objectIdentityId,
        string expectedDefinitionHash,
        string definitionPayloadJson,
        long definitionSchemaVersion,
        long nowUtcMs)
    {
        var identity = database.FindObject(projectId, objectIdentityId)
            ?? throw new CoreDomainConflictException($"Object {objectIdentityId} 不存在或不属于项目 {projectId}，拒绝编辑定义。");

        if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
        {
            throw new CoreDomainConflictException(
                $"身份 {objectIdentityId} 处于 {identity.LifecycleStatus} 状态；仅 active Object 可编辑定义（draft/abandoned 无定义行语义）。");
        }

        var current = database.ReadObjectDefinition(projectId, objectIdentityId)
            ?? throw new CoreDomainDataException($"active Object {objectIdentityId} 缺少定义行（数据损坏，R-定义单行）。");

        if (!string.Equals(current.DefinitionHash, expectedDefinitionHash?.Trim(), StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException(
                $"定义 CAS 冲突：expected hash 与当前定义不匹配（对象 {objectIdentityId} 已被并发编辑或凭据过期）。");
        }

        // 规范化 + hash 在服务层计算（Domain 职责，Handoff §5.7）；schema 版本与 pin 一致性
        // 校验 + typed 字段/聚合校验经 descriptor registry 统一路由（CD-02-C/E）。
        ObjectRules.ValidateDefinitionSchemaVersion(identity, definitionSchemaVersion);
        var type = database.FindObjectType(projectId, identity.ObjectTypeId)
            ?? throw new CoreDomainDataException($"身份 {objectIdentityId} 引用的 Object Type {identity.ObjectTypeId} 不存在（数据损坏）。");
        var (canonicalPayload, hash) = CanonicalJson.CanonicalizeAndHash(definitionPayloadJson);
        var descriptor = Registry.ObjectTypeRegistry.Require(type.TypeKey);
        Registry.ObjectTypeRegistry.ValidateDefinition(type.TypeKey, canonicalPayload);
        if (descriptor.HasTypedAggregateSchema
            && !string.Equals(
                ReadGeometryComponents(current.DefinitionPayloadJson),
                ReadGeometryComponents(canonicalPayload),
                StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException(
                "Building/Cave geometry_components 变更必须通过 PublishDefinitionWithGeometryAssets 原子发布 Definition 与完整 Asset links。");
        }

        database.EditObjectDefinition(new ObjectDefinitionEditPlan
        {
            ProjectId = projectId,
            ObjectIdentityId = objectIdentityId,
            ExpectedDefinitionHash = current.DefinitionHash,
            DefinitionPayloadJson = canonicalPayload,
            DefinitionSchemaVersion = definitionSchemaVersion,
            NowUtcMs = nowUtcMs,
        });

        return current with
        {
            DefinitionPayloadJson = canonicalPayload,
            DefinitionHash = hash,
            UpdatedAtUtcMs = nowUtcMs,
        };
    }

    /// <summary>
    /// Building/Cave typed aggregate 的正式 Geometry Component 发布。新 Definition 与
    /// 完整 geometry_component typed-link set 必须在同一 SQLite 事务提交；link set 从
    /// canonical payload 派生，不接受调用方提供的部分清单。
    /// </summary>
    public static ObjectDefinitionRecord PublishDefinitionWithGeometryAssets(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid objectIdentityId,
        string expectedDefinitionHash,
        string definitionPayloadJson,
        long definitionSchemaVersion,
        long nowUtcMs)
    {
        var identity = database.FindObject(projectId, objectIdentityId)
            ?? throw new CoreDomainConflictException($"Object {objectIdentityId} 不存在或不属于项目 {projectId}，拒绝发布定义。");

        if (identity.LifecycleStatus != ObjectLifecycleStatus.Active)
        {
            throw new CoreDomainConflictException($"Object {objectIdentityId} 非 active 状态，拒绝发布 Geometry Definition。");
        }

        var current = database.ReadObjectDefinition(projectId, objectIdentityId)
            ?? throw new CoreDomainDataException($"active Object {objectIdentityId} 缺少定义行（数据损坏）。");
        if (!string.Equals(current.DefinitionHash, expectedDefinitionHash?.Trim(), StringComparison.Ordinal))
        {
            throw new CoreDomainConflictException($"定义 CAS 冲突：Object {objectIdentityId} 已被并发编辑或凭据过期。");
        }

        ObjectRules.ValidateDefinitionSchemaVersion(identity, definitionSchemaVersion);
        var type = database.FindObjectType(projectId, identity.ObjectTypeId)
            ?? throw new CoreDomainDataException($"Object {objectIdentityId} 的 Object Type 不存在（数据损坏）。");
        var descriptor = Registry.ObjectTypeRegistry.Require(type.TypeKey);
        if (!descriptor.HasTypedAggregateSchema || type.TypeKey is not ("building" or "cave_system"))
        {
            throw new CoreDomainConflictException(
                $"Object Type {type.TypeKey} 尚无已采纳的 Building/Cave typed Geometry publication adapter。");
        }

        var (canonicalPayload, hash) = CanonicalJson.CanonicalizeAndHash(definitionPayloadJson);
        Registry.ObjectTypeRegistry.ValidateDefinition(type.TypeKey, canonicalPayload);

        database.PublishObjectDefinitionWithGeometryAssets(new ObjectDefinitionPublicationPlan
        {
            ProjectId = projectId,
            ObjectIdentityId = objectIdentityId,
            ExpectedDefinitionHash = current.DefinitionHash,
            DefinitionPayloadJson = canonicalPayload,
            DefinitionSchemaVersion = definitionSchemaVersion,
            NowUtcMs = nowUtcMs,
        });

        return current with
        {
            DefinitionPayloadJson = canonicalPayload,
            DefinitionHash = hash,
            UpdatedAtUtcMs = nowUtcMs,
        };
    }

    private static string ReadGeometryComponents(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("geometry_components", out var components)
            ? components.GetRawText()
            : "null";
    }
}

/// <summary>
/// Map 注册服务（P2 存在层；CD-02-A §9）：注册只表达"Map 纳入"，
/// ≠ Initial State 成员；map_local 仅 owner Map；project_shared 可多 Map。
/// </summary>
public static class MapRegistrationService
{
    /// <summary>注册 Object 到 Map（重复注册拒绝；scope/存在性校验后事务内单行插入）。</summary>
    public static MapObjectRefRecord Register(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid objectIdentityId,
        long nowUtcMs)
    {
        var map = database.FindMap(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 不存在或不属于项目 {projectId}，拒绝注册。");

        var identity = database.FindObject(projectId, objectIdentityId)
            ?? throw new CoreDomainConflictException($"Object {objectIdentityId} 不存在或不属于项目 {projectId}，拒绝注册。");

        ObjectRules.ValidateUsableForMapPresence(identity);
        if (identity.SharingScope == ObjectSharingScope.MapLocal && identity.OwnerMapId != mapId)
        {
            throw new CoreDomainConflictException("map_local Object 只能注册到 owner Map（R-注册）。");
        }

        if (database.FindMapObjectRef(projectId, mapId, objectIdentityId) is not null)
        {
            throw new CoreDomainConflictException($"Object {objectIdentityId} 已注册到 Map {mapId}（UNIQUE 语义），拒绝重复注册。");
        }

        var plan = new MapObjectRegistrationPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            ObjectIdentityId = objectIdentityId,
            MapObjectRefId = Guid.NewGuid(),
            NowUtcMs = nowUtcMs,
        };
        database.RegisterMapObject(plan);
        return new MapObjectRefRecord
        {
            MapObjectRefId = plan.MapObjectRefId,
            ProjectId = projectId,
            MapId = mapId,
            ObjectIdentityId = objectIdentityId,
            CreatedAtUtcMs = nowUtcMs,
        };
    }

    /// <summary>取消注册：存在 Initial State 成员行时拒绝（P3⇒P2；须先移除成员）。</summary>
    public static void Unregister(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid objectIdentityId)
    {
        if (database.FindMapObjectRef(projectId, mapId, objectIdentityId) is null)
        {
            throw new CoreDomainConflictException($"Object {objectIdentityId} 未注册到 Map {mapId}，无法取消。");
        }

        database.UnregisterMapObject(projectId, mapId, objectIdentityId);
    }
}

/// <summary>
/// Map time-zero baseline revision service. Every edit appends an immutable full snapshot;
/// Core Domain owns the active cursor and deterministic Undo/Redo semantics.
/// </summary>
public static class MapInitialStateService
{
    /// <summary>V1 structural baseline schema version.</summary>
    public const long StructuralBaselineSchemaVersion = 1;

    public static MapInitialStateRecord EditInitialState(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        long baselineSchemaVersion,
        IReadOnlyList<InitialObjectWrite> members,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(members);

        _ = database.FindMap(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 不存在或不属于当前 Project。");
        var baseline = database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainDataException($"Map {mapId} 缺少 time-zero baseline/cursor。");
        ValidateExpectedCursor(baseline, expectedActiveRevisionId, expectedCursorVersion);
        if (baseline.BaselineSchemaVersion != baselineSchemaVersion)
            throw new CoreDomainConflictException("Initial State baseline schema version cannot change inside an edit.");

        var normalized = ValidateAndNormalizeMembers(database, projectId, mapId, members);
        var parentMembers = database.ListInitialObjects(projectId, mapId);
        var revisionId = Guid.NewGuid();
        var childMembers = normalized.Select(member => new InitialObjectRecord
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = baseline.InitialStateId,
            RevisionId = revisionId,
            ObjectIdentityId = member.ObjectIdentityId,
            MembershipPayloadJson = member.MembershipPayloadJson,
            PlacementPayloadJson = member.PlacementPayloadJson,
            PlacementSchemaVersion = member.PlacementSchemaVersion,
            CreatedAtUtcMs = nowUtcMs,
        }).ToArray();
        var fingerprint = InitialStateFingerprint.Compute(baselineSchemaVersion, childMembers);
        if (string.Equals(fingerprint, baseline.ContentFingerprint, StringComparison.Ordinal))
            return baseline; // No state change: do not create a redundant revision.
        var operationPayload = InitialStateOperation.ComputePayload(
            parentMembers, normalized, baseline.ContentFingerprint, fingerprint);

        database.AppendMapInitialStateRevision(new EditInitialStatePlan
        {
            ProjectId = projectId,
            MapId = mapId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            NewRevisionId = revisionId,
            BaselineSchemaVersion = baselineSchemaVersion,
            Members = normalized,
            OperationPayloadJson = operationPayload,
            ContentFingerprint = fingerprint,
            NowUtcMs = nowUtcMs,
        });

        return database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainDataException("Initial State revision 已提交但 active cursor 不可读。");
    }

    public static IReadOnlyList<MapInitialStateRevisionRecord> ListRevisions(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.ListMapInitialStateRevisions(projectId, mapId);
    }

    public static IReadOnlyList<InitialObjectRecord> ReadRevision(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid revisionId)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.ListInitialStateRevisionObjects(projectId, mapId, revisionId);
    }

    public static MapInitialStateRecord Undo(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(database);
        var baseline = database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 尚无 Initial State baseline。");
        ValidateExpectedCursor(baseline, expectedActiveRevisionId, expectedCursorVersion);
        database.UndoMapInitialStateCursor(new UndoInitialStatePlan
        {
            ProjectId = projectId,
            MapId = mapId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            NowUtcMs = nowUtcMs,
        });
        return database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainDataException("Undo 已提交但 Initial State cursor 不可读。");
    }

    public static MapInitialStateRecord Redo(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        Guid selectedChildRevisionId,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (selectedChildRevisionId == Guid.Empty)
            throw new ArgumentException("Redo 必须显式选择一个 revision child。", nameof(selectedChildRevisionId));
        var baseline = database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 尚无 Initial State baseline。");
        ValidateExpectedCursor(baseline, expectedActiveRevisionId, expectedCursorVersion);
        database.RedoMapInitialStateCursor(new RedoInitialStatePlan
        {
            ProjectId = projectId,
            MapId = mapId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            SelectedChildRevisionId = selectedChildRevisionId,
            NowUtcMs = nowUtcMs,
        });
        return database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainDataException("Redo 已提交但 Initial State cursor 不可读。");
    }

    private static void ValidateExpectedCursor(
        MapInitialStateRecord baseline,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion)
    {
        if (baseline.ActiveRevisionId != expectedActiveRevisionId
            || baseline.CursorVersion != expectedCursorVersion)
            throw new CoreDomainConflictException(
                "Initial State cursor stale：expected active revision/cursor version 与当前值不匹配。");
    }

    private static IReadOnlyList<InitialObjectWrite> ValidateAndNormalizeMembers(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        IReadOnlyList<InitialObjectWrite> members)
    {
        var seen = new System.Collections.Generic.HashSet<Guid>();
        var normalized = new List<InitialObjectWrite>(members.Count);
        foreach (var member in members)
        {
            if (member.ObjectIdentityId == Guid.Empty || !seen.Add(member.ObjectIdentityId))
                throw new CoreDomainConflictException("Initial State 成员身份为空或重复。");

            var identity = database.FindObject(projectId, member.ObjectIdentityId)
                ?? throw new CoreDomainConflictException($"成员 {member.ObjectIdentityId} 不存在或不属于当前 Project。");
            ObjectRules.ValidateUsableForMapPresence(identity);
            if (database.FindMapObjectRef(projectId, mapId, member.ObjectIdentityId) is null)
                throw new CoreDomainConflictException($"成员 {member.ObjectIdentityId} 缺少 Map {mapId} 的注册行。");

            if ((member.PlacementPayloadJson is null) != (member.PlacementSchemaVersion is null))
                throw new ArgumentException("Placement payload 与 schema version 必须同时存在或同时缺省。", nameof(members));
            if (member.PlacementSchemaVersion is < 1)
                throw new ArgumentOutOfRangeException(nameof(members), "Placement schema version 必须 >= 1。");

            normalized.Add(new InitialObjectWrite
            {
                ObjectIdentityId = member.ObjectIdentityId,
                MembershipPayloadJson = CanonicalJson.Canonicalize(member.MembershipPayloadJson),
                PlacementPayloadJson = member.PlacementPayloadJson is null
                    ? null
                    : CanonicalJson.Canonicalize(member.PlacementPayloadJson),
                PlacementSchemaVersion = member.PlacementSchemaVersion,
            });
        }

        return normalized;
    }
}
