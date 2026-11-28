using System;
using System.Text.Json;
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
/// Map Initial State 修订服务（P3 存在层；FOUNDATION-01B 设计 §6/§10）：
/// 每 Map 恰一条 baseline identity、一棵不可变完整快照 revision 树、一条可变 cursor。
/// 编辑是 baseline authoring 路径（非 Command/Event/State Change）；每次修改追加新
/// immutable revision；Undo/Redo 只移动 cursor（校验 expected active revision +
/// cursor version，stale 请求 fail-closed，不留部分数据）。指纹由 Domain 规范化
/// 序列化计算（<see cref="InitialStateFingerprint"/>），持久化层事务内重算并拒绝
/// 不一致（FOUNDATION-01B §7）。Branch 继承 root Map baseline：分叉不复制任何行
/// （ADR-002/005；继承是解析时语义）。UI-local Undo 栈永不成为权威。
/// </summary>
public static class MapInitialStateService
{
    /// <summary>结构层 baseline schema 版本（typed 内容 schema 为 open decision OD-1；V1 结构层 = 1）。</summary>
    public const long StructuralBaselineSchemaVersion = 1;

    /// <summary>
    /// 空 revision zero 快照的确定性指纹（composition v1 + 结构层 schema 版本 + 空成员集）。
    /// Map 创建与迁移（FOUNDATION-01B §5/§9）共用同一常量，保证无时钟/顺序依赖。
    /// </summary>
    public static string EmptySnapshotFingerprint { get; } =
        InitialStateFingerprint.Compute(StructuralBaselineSchemaVersion, []);

    /// <summary>
    /// 读取活动 revision 快照 + cursor + 活动 revision 的直接子 revision（redo 分支候选）。
    /// baseline/cursor 缺失是数据损坏（Map 创建事务内建立，无 lazy 建立）。
    /// </summary>
    public static MapInitialStateRevisionState ReadActiveState(ICoreDomainDatabase database, Guid projectId, Guid mapId)
    {
        RequireMap(database, projectId, mapId);
        return database.FindMapInitialStateRevisionState(projectId, mapId)
            ?? throw new CoreDomainDataException(
                $"Map {mapId} 缺少 Initial State baseline/revision zero/cursor（数据损坏；Map 创建必须同事务建立）。");
    }

    /// <summary>按 revision 身份读取任意不可变快照（历史展示/校验用；只读，无 cursor 语义）。</summary>
    public static MapInitialStateRevisionSnapshot ReadRevisionSnapshot(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid revisionId)
    {
        RequireMap(database, projectId, mapId);
        return database.FindMapInitialStateRevisionSnapshot(projectId, mapId, revisionId)
            ?? throw new CoreDomainConflictException(
                $"Revision {revisionId} 不存在于 Map {mapId} 的 baseline（不存在或跨 baseline，拒绝读取）。");
    }

    /// <summary>列出 baseline 全部 revision 元数据（revision_number 升序；历史展示用）。</summary>
    public static IReadOnlyList<MapInitialStateRevisionRecord> ListRevisions(ICoreDomainDatabase database, Guid projectId, Guid mapId)
    {
        RequireMap(database, projectId, mapId);
        return database.ListMapInitialStateRevisions(projectId, mapId);
    }

    /// <summary>
    /// 追加一次 time-zero 编辑：以 expected active revision + cursor version 为 CAS 凭据，
    /// 在活动 revision 下创建新 immutable child（完整快照），cursor 前移并清除 redo 选择。
    /// 全部旧 revision 与后代原样保留（edit-after-undo 分支语义）。内容与父快照相同的
    /// 编辑（空 diff）不被特殊禁止：契约未定义"必须拒绝"，由调用方决定是否提交。
    /// </summary>
    public static MapInitialStateRevisionState AppendRevision(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        IReadOnlyList<InitialObjectWrite> members,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(members);

        RequireMap(database, projectId, mapId);
        var state = ReadActiveState(database, projectId, mapId);
        ValidateExpectedCursor(state, expectedActiveRevisionId, expectedCursorVersion);
        ValidateMembers(database, projectId, mapId, members);

        var parentMembers = state.Snapshot.Members;
        var parentFingerprint = state.Snapshot.Revision.ContentFingerprint;
        var (childFingerprint, operationPayload) = BuildChildContent(
            state.Snapshot.Revision.BaselineSchemaVersion, parentFingerprint, parentMembers, members);

        database.AppendMapInitialStateRevision(new AppendMapInitialStateRevisionPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = state.Cursor.InitialStateId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            RevisionId = Guid.NewGuid(),
            Members = members,
            ContentFingerprint = childFingerprint,
            OperationPayloadJson = operationPayload,
            NowUtcMs = nowUtcMs,
        });

        return ReadActiveState(database, projectId, mapId);
    }

    /// <summary>
    /// Undo：cursor 移动到活动 revision 的 parent；不创建/不改写任何 revision；
    /// selected redo child 指向刚离开的 revision（close/reopen 后可确定性 Redo）。
    /// revision zero（root）不可 Undo。
    /// </summary>
    public static MapInitialStateRevisionState Undo(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        long nowUtcMs)
    {
        RequireMap(database, projectId, mapId);
        var state = ReadActiveState(database, projectId, mapId);
        ValidateExpectedCursor(state, expectedActiveRevisionId, expectedCursorVersion);

        var parentRevisionId = state.Snapshot.Revision.ParentRevisionId
            ?? throw new CoreDomainConflictException(
                $"Revision zero（baseline root）不可 Undo（Map {mapId} 无 parent revision）。");

        database.MoveMapInitialStateCursor(new MoveMapInitialStateCursorPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = state.Cursor.InitialStateId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            TargetRevisionId = parentRevisionId,
            IsRedo = false,
            NowUtcMs = nowUtcMs,
        });

        return ReadActiveState(database, projectId, mapId);
    }

    /// <summary>
    /// Redo：cursor 移动到活动 revision 的一个直接子 revision。<paramref name="childRevisionId"/>
    /// 为 null 时解析持久化的 selected redo child；仅一个子 revision 时可直接 Redo；
    /// 多个子且无选择时必须显式选择（SelectRedoChild）。完成后清除 redo 选择。
    /// </summary>
    public static MapInitialStateRevisionState Redo(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        Guid? childRevisionId,
        long nowUtcMs)
    {
        RequireMap(database, projectId, mapId);
        var state = ReadActiveState(database, projectId, mapId);
        ValidateExpectedCursor(state, expectedActiveRevisionId, expectedCursorVersion);

        var target = childRevisionId
            ?? InitialStateRevisionRules.ResolveRedoTarget(state.RedoChildren, state.Cursor.SelectedRedoChildRevisionId);
        InitialStateRevisionRules.ValidateDirectChild(
            state.Cursor.ActiveRevisionId,
            target,
            ResolveParent(database, projectId, mapId, target));

        database.MoveMapInitialStateCursor(new MoveMapInitialStateCursorPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = state.Cursor.InitialStateId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            TargetRevisionId = target,
            IsRedo = true,
            NowUtcMs = nowUtcMs,
        });

        return ReadActiveState(database, projectId, mapId);
    }

    /// <summary>
    /// 持久化显式 redo 分支选择（不动 cursor）：目标必须是活动 revision 的直接子；
    /// 选择在 close/reopen 后保留（FOUNDATION-01B §6）。
    /// </summary>
    public static MapInitialStateRevisionState SelectRedoChild(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion,
        Guid childRevisionId,
        long nowUtcMs)
    {
        RequireMap(database, projectId, mapId);
        var state = ReadActiveState(database, projectId, mapId);
        ValidateExpectedCursor(state, expectedActiveRevisionId, expectedCursorVersion);
        InitialStateRevisionRules.ValidateDirectChild(
            state.Cursor.ActiveRevisionId,
            childRevisionId,
            ResolveParent(database, projectId, mapId, childRevisionId));

        database.SelectMapInitialStateRedoChild(new SelectMapInitialStateRedoChildPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = state.Cursor.InitialStateId,
            ExpectedActiveRevisionId = expectedActiveRevisionId,
            ExpectedCursorVersion = expectedCursorVersion,
            ChildRevisionId = childRevisionId,
            NowUtcMs = nowUtcMs,
        });

        return ReadActiveState(database, projectId, mapId);
    }

    /// <summary>
    /// InsertObject 路径的 revision 追加上下文：读取活动 revision/cursor，推导"父快照 +
    /// 单个新成员"的子快照内容（指纹与操作描述由 Domain 计算；持久化层事务内重算拒绝
    /// 不一致）。新成员身份的激活与 reservation 转换与 revision 追加同事务原子提交。
    /// </summary>
    public static ObjectInsertInitialRevisionAppend BuildInsertObjectAppend(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid objectIdentityId,
        InitialObjectMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        var state = ReadActiveState(database, projectId, mapId);

        if (membership.PlacementPayloadJson is null != membership.PlacementSchemaVersion is null)
        {
            throw new ArgumentException("初始 placement payload 与 schema version 必须成对提供。", nameof(membership));
        }

        var newMember = new InitialObjectWrite
        {
            ObjectIdentityId = objectIdentityId,
            MembershipPayloadJson = membership.MembershipPayloadJson,
            PlacementPayloadJson = membership.PlacementPayloadJson,
            PlacementSchemaVersion = membership.PlacementSchemaVersion,
        };

        var parentMembers = state.Snapshot.Members;
        if (parentMembers.Any(m => m.ObjectIdentityId == objectIdentityId))
        {
            throw new CoreDomainConflictException(
                $"身份 {objectIdentityId} 已是 Map {mapId} 活动 revision 的 Initial State 成员，拒绝重复追加。");
        }

        var childMembers = new List<InitialObjectWrite>(parentMembers.Count + 1)
        {
            Capacity = parentMembers.Count + 1,
        };
        childMembers.AddRange(parentMembers.Select(m => new InitialObjectWrite
        {
            ObjectIdentityId = m.ObjectIdentityId,
            MembershipPayloadJson = m.MembershipPayloadJson,
            PlacementPayloadJson = m.PlacementPayloadJson,
            PlacementSchemaVersion = m.PlacementSchemaVersion,
        }));
        childMembers.Add(newMember);

        var (childFingerprint, operationPayload) = BuildChildContent(
            state.Snapshot.Revision.BaselineSchemaVersion,
            state.Snapshot.Revision.ContentFingerprint,
            parentMembers,
            childMembers);

        return new ObjectInsertInitialRevisionAppend
        {
            InitialStateId = state.Cursor.InitialStateId,
            ExpectedActiveRevisionId = state.Cursor.ActiveRevisionId,
            ExpectedCursorVersion = state.Cursor.CursorVersion,
            NewRevisionId = Guid.NewGuid(),
            ContentFingerprint = childFingerprint,
            OperationPayloadJson = operationPayload,
        };
    }

    /// <summary>由父快照成员与写入集合计算子快照指纹与 canonical 操作描述（Domain 职责）。</summary>
    private static (string ChildFingerprint, string OperationPayload) BuildChildContent(
        long baselineSchemaVersion,
        string parentFingerprint,
        IReadOnlyList<InitialObjectRecord> parentMembers,
        IReadOnlyList<InitialObjectWrite> childMembers)
    {
        var childFingerprint = InitialStateFingerprint.Compute(baselineSchemaVersion, childMembers);
        var operationPayload = InitialStateRevisionRules.BuildOperationPayload(
            parentFingerprint, childFingerprint, parentMembers, childMembers);
        return (childFingerprint, operationPayload);
    }

    /// <summary>成员前置校验：唯一身份、active、已注册到该 Map（P3⇒P2）；事务内重读兜底并发。</summary>
    private static void ValidateMembers(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        IReadOnlyList<InitialObjectWrite> members)
    {
        var seen = new HashSet<Guid>();
        foreach (var member in members)
        {
            if (member.PlacementPayloadJson is null != member.PlacementSchemaVersion is null)
            {
                throw new ArgumentException(
                    $"成员 {member.ObjectIdentityId} 的 placement payload 与 schema version 必须成对提供。");
            }

            if (!seen.Add(member.ObjectIdentityId))
            {
                throw new CoreDomainConflictException($"成员集合含重复身份 {member.ObjectIdentityId}，拒绝。");
            }

            var identity = database.FindObject(projectId, member.ObjectIdentityId)
                ?? throw new CoreDomainConflictException($"成员 {member.ObjectIdentityId} 不存在或不属于项目 {projectId}。");

            ObjectRules.ValidateUsableForMapPresence(identity);
            if (database.FindMapObjectRef(projectId, mapId, member.ObjectIdentityId) is null)
            {
                throw new CoreDomainConflictException(
                    $"成员 {member.ObjectIdentityId} 缺少 Map {mapId} 的注册行（P3⇒P2），拒绝写入 Initial State。");
            }
        }
    }

    private static void ValidateExpectedCursor(
        MapInitialStateRevisionState state,
        Guid expectedActiveRevisionId,
        long expectedCursorVersion)
    {
        if (state.Cursor.ActiveRevisionId != expectedActiveRevisionId
            || state.Cursor.CursorVersion != expectedCursorVersion)
        {
            throw new CoreDomainConflictException(
                $"Initial State cursor 冲突：expected (revision {expectedActiveRevisionId:D}, version {expectedCursorVersion}) "
                + $"与当前 (revision {state.Cursor.ActiveRevisionId:D}, version {state.Cursor.CursorVersion}) 不匹配。");
        }
    }

    private static Guid? ResolveParent(ICoreDomainDatabase database, Guid projectId, Guid mapId, Guid revisionId)
    {
        return ListRevisions(database, projectId, mapId).SingleOrDefault(x => x.RevisionId == revisionId)
            is { } revision ? revision.ParentRevisionId : throw new CoreDomainConflictException(
                $"Revision {revisionId} 不存在于 Map {mapId} 的 baseline（不存在或跨 baseline）。");
    }

    private static void RequireMap(ICoreDomainDatabase database, Guid projectId, Guid mapId)
    {
        _ = database.FindMap(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 不存在或不属于项目 {projectId}，拒绝 Initial State 操作。");
    }
}
