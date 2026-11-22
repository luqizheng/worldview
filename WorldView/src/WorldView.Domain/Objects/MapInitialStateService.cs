using System;
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
        Registry.ObjectTypeRegistry.ValidateDefinition(type.TypeKey, canonicalPayload);

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
/// Map Initial State 服务（P3 存在层；CD-02-A §10）：每 Map 恰一个可变 time-zero
/// 基线；编辑是 baseline authoring 路径（非 Command）；CAS 以 expected fingerprint 守卫；
/// 成员全量重写；指纹由 Domain 规范化序列化计算（Handoff §5.6/§5.7）。
/// Branch 继承 root Map baseline：分叉不复制任何行（ADR-002/005；继承是解析时语义）。
/// </summary>
public static class MapInitialStateService
{
    /// <summary>结构层 baseline schema 版本（typed 内容 schema 为 open decision OD-1；V1 结构层 = 1）。</summary>
    public const long StructuralBaselineSchemaVersion = 1;

    /// <summary>
    /// CAS 编辑 Initial State（全量成员集合替换）。
    /// 基线不存在且 <paramref name="expectedFingerprint"/> 为 null → 建立基线；
    /// 基线存在 → expected 必须匹配当前指纹（null 视为冲突，防止并发建立竞态）。
    /// 每个成员身份必须 active 且已注册到该 Map（P3⇒P2，R-成员前置）。
    /// </summary>
    public static MapInitialStateRecord EditInitialState(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        string? expectedFingerprint,
        long baselineSchemaVersion,
        IReadOnlyList<InitialObjectWrite> members,
        long nowUtcMs)
    {
        ArgumentNullException.ThrowIfNull(members);

        var map = database.FindMap(projectId, mapId)
            ?? throw new CoreDomainConflictException($"Map {mapId} 不存在或不属于项目 {projectId}，拒绝编辑 Initial State。");

        // 成员身份/注册前置校验（服务层最低要求；事务内重读兜底并发）。
        var seen = new System.Collections.Generic.HashSet<Guid>();
        foreach (var member in members)
        {
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

        var existing = database.FindMapInitialState(projectId, mapId);
        if (existing is not null)
        {
            if (expectedFingerprint is null
                || !string.Equals(existing.ContentFingerprint, expectedFingerprint.Trim(), StringComparison.Ordinal))
            {
                throw new CoreDomainConflictException(
                    $"Initial State CAS 冲突：expected fingerprint 与 Map {mapId} 当前基线不匹配。");
            }

            database.EditMapInitialState(new EditInitialStatePlan
            {
                ProjectId = projectId,
                MapId = mapId,
                InitialStateId = existing.InitialStateId,
                ExpectedFingerprint = existing.ContentFingerprint,
                BaselineSchemaVersion = baselineSchemaVersion,
                Members = members,
                NowUtcMs = nowUtcMs,
            });

            return existing with { ContentFingerprint = ReadFingerprint(database, projectId, mapId), UpdatedAtUtcMs = nowUtcMs };
        }

        if (expectedFingerprint is not null)
        {
            throw new CoreDomainConflictException(
                $"Map {mapId} 尚无 Initial State 基线；建立时 expected fingerprint 必须为 null。");
        }

        var initialStateId = Guid.NewGuid();
        database.EditMapInitialState(new EditInitialStatePlan
        {
            ProjectId = projectId,
            MapId = mapId,
            InitialStateId = initialStateId,
            ExpectedFingerprint = null,
            BaselineSchemaVersion = baselineSchemaVersion,
            Members = members,
            NowUtcMs = nowUtcMs,
        });

        return new MapInitialStateRecord
        {
            InitialStateId = initialStateId,
            ProjectId = projectId,
            MapId = mapId,
            BaselineSchemaVersion = baselineSchemaVersion,
            ContentFingerprint = ReadFingerprint(database, projectId, mapId),
            CreatedAtUtcMs = nowUtcMs,
            UpdatedAtUtcMs = nowUtcMs,
        };
    }

    /// <summary>编辑后重读权威指纹（适配器事务内计算并写入；此处重读保证返回值与库内一致）。</summary>
    private static string ReadFingerprint(ICoreDomainDatabase database, Guid projectId, Guid mapId)
    {
        var updated = database.FindMapInitialState(projectId, mapId)
            ?? throw new CoreDomainDataException("Initial State 编辑事务已提交但基线行不可读（数据损坏）。");
        return updated.ContentFingerprint;
    }
}
