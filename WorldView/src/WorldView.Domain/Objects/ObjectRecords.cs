using System;

namespace WorldView.Domain.Objects;

/// <summary>
/// Object Type 配置行（core_object_type，P2-09-11 §4.2；CD-02-A §7/§8）。
/// 仅承载 Project 级配置与稳定 Object Type 身份；可执行 descriptor schema/semantics
/// 由编译期 registry 提供（CD-02-B 尚无 registry runtime，语义按 P2-09-26 V0.2
/// descriptor 的显式声明对待，见 <see cref="BuiltinObjectTypes"/>）。
/// </summary>
public sealed record ObjectTypeRecord
{
    public required Guid ObjectTypeId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>稳定类型键（十内置类型取 P2-09-21 V0.1 种子目录；永不改写既有语义）。</summary>
    public required string TypeKey { get; init; }

    /// <summary>可变显示名（显示名变更不改写 type_key 与已分配 ID）。</summary>
    public required string DisplayName { get; init; }

    /// <summary>是否可分配新 Object ID（废弃类型保留解析能力时置 false）。</summary>
    public required bool Allocatable { get; init; }

    /// <summary>分配格式定义（V1 最小格式占位，见 <see cref="ObjectAllocationFormat"/>；OD-3）。</summary>
    public required string AllocationFormatJson { get; init; }

    /// <summary>分配格式版本；格式变更永不改写既有 allocated_id_value（P2-09-04 §8）。</summary>
    public required long AllocationFormatVersion { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>Object 共享范围（core_object.sharing_scope；CHECK + 服务双保险，R-scope）。</summary>
public enum ObjectSharingScope
{
    /// <summary>Project 共享：owner_map_id 必为空，可被 Project 内任意 Map 注册（P2-09-07 §2）。</summary>
    ProjectShared,

    /// <summary>Map 本地：owner_map_id 必非空，仅可注册到 owner Map。</summary>
    MapLocal,
}

/// <summary>
/// Object 身份级生命周期（core_object.lifecycle_status；R-状态机）。
/// 只表达身份级状态——post-zero 运行时可用性属 History 级在场（CD-02-A §4.4），永不写此列。
/// </summary>
public enum ObjectLifecycleStatus
{
    /// <summary>已绑定未插入：不得出现在 Map membership / Initial State（P2-09-04 §5）。</summary>
    Draft,

    /// <summary>正式创建（经 Insert 路径 draft→active，同事务）。</summary>
    Active,

    /// <summary>墓碑：Bound 后放弃的身份行；永不物理删除、永不复用。</summary>
    Abandoned,

    /// <summary>退役（受控操作；V1 无服务入口，仅保留枚举与 CHECK）。</summary>
    Retired,
}

/// <summary>
/// Object 身份行（core_object，P1 存在层）。含 Bound 时钉住的 Object Type
/// schema/semantics 兼容 pin——既有 Object 永不被最新 descriptor 静默重释（P2-09-18 §10）。
/// </summary>
public sealed record ObjectRecord
{
    /// <summary>身份存储键（canonical 小写连字符 UUID；项目内全局唯一；关系键，非领域可见 ID）。</summary>
    public required Guid ObjectIdentityId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid ObjectTypeId { get; init; }

    /// <summary>Bound 时钉住的 Object Type schema 版本（CD-02-B 十内置类型固定为 2）。</summary>
    public required long ObjectTypeSchemaVersion { get; init; }

    /// <summary>Bound 时钉住的语义标识（world-view.object-type.&lt;key&gt;.v1）。</summary>
    public required string ObjectTypeSemanticsId { get; init; }

    public required ObjectSharingScope SharingScope { get; init; }

    /// <summary>仅 MapLocal 非空（CHECK 强制配对）。</summary>
    public Guid? OwnerMapId { get; init; }

    public required ObjectLifecycleStatus LifecycleStatus { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>
/// 当前共享定义行（core_object_definition，权威层③）：每 identity 至多一行；
/// active Object 恰一行；无可回放版本链（P2-09-07 §2；P2-09-16 §8）。
/// </summary>
public sealed record ObjectDefinitionRecord
{
    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    /// <summary>定义 schema 版本；必须与身份行钉住的 object_type_schema_version 一致（R-定义）。</summary>
    public required long DefinitionSchemaVersion { get; init; }

    /// <summary>canonical JSON 定义 payload（写入时由 Core Domain 规范化）。</summary>
    public required string DefinitionPayloadJson { get; init; }

    /// <summary>canonical payload 的 SHA-256（小写十六进制；CAS 冲突检测基准）。</summary>
    public required string DefinitionHash { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>Reservation 状态（core_object_id_reservation.reservation_state；P2-09-04 §5/§7）。</summary>
public enum ReservationState
{
    /// <summary>已分配未绑定：object_identity_id 必为空。</summary>
    Reserved,

    /// <summary>已绑定 draft 身份：object_identity_id 必非空。</summary>
    Bound,

    /// <summary>正式创建（终态）。</summary>
    Inserted,

    /// <summary>已放弃（终态；ID 永不复用）。</summary>
    Abandoned,
}

/// <summary>
/// Object ID Reservation 行（core_object_id_reservation，P2-09-04/§8）。
/// allocated_id_value 是领域可见的正式 Object ID（永不复用、永不改写）；
/// object_identity_id 是数据库关系键（Bound 时生成）。
/// </summary>
public sealed record ReservationRecord
{
    public required Guid ReservationId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid ObjectTypeId { get; init; }

    /// <summary>仅 Bound/Inserted 非空（Abandoned 可保留墓碑溯源或为空）。</summary>
    public Guid? ObjectIdentityId { get; init; }

    /// <summary>分配序号（(project, type) 内唯一；allocator 前进即消耗）。</summary>
    public required long AllocatedSequence { get; init; }

    /// <summary>领域可见正式 Object ID（V1 最小格式 = 十进制 sequence 文本，OD-3）。</summary>
    public required string AllocatedIdValue { get; init; }

    public required ReservationState State { get; init; }

    /// <summary>逻辑分配请求键（提供时 (project, request_key) 唯一，幂等对账依据）。</summary>
    public string? RequestKey { get; init; }

    /// <summary>放弃原因（仅 Abandoned 可非空）。</summary>
    public string? AbandonReason { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>Map 对正式 Object 的注册行（core_map_object_ref，P2 存在层；不复制 scope/owner）。</summary>
public sealed record MapObjectRefRecord
{
    public required Guid MapObjectRefId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>
/// Map Initial State 基线身份行（core_map_initial_state，FOUNDATION-01B §3.1）：
/// 每 Map 恰一条稳定 baseline identity；可变指纹/schema 版本字段已移入不可变
/// revision 行。编辑不是 Command/Event/State Change（CD-02-A §10；FOUNDATION-01B）。
/// </summary>
public sealed record MapInitialStateBaselineRecord
{
    public required Guid InitialStateId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>
/// Initial State 成员内容的公共形状（指纹/canonical diff 只依赖这四个字段）；
/// <see cref="InitialObjectRecord"/>（已提交行）与 <see cref="InitialObjectWrite"/>（写入项）共用。
/// </summary>
public interface IInitialObjectContent
{
    Guid ObjectIdentityId { get; }

    string MembershipPayloadJson { get; }

    string? PlacementPayloadJson { get; }

    long? PlacementSchemaVersion { get; }
}

/// <summary>
/// Initial State revision 成员行（core_map_initial_state_revision_member，P3 存在层）：
/// 必有对应 core_map_object_ref（P3⇒P2，复合 FK 数据库层强制）；提交后不可变。
/// payload 只含 Map 特有初始事实，不是共享定义副本（P2-09-07 §3）。
/// </summary>
public sealed record InitialObjectRecord : IInitialObjectContent
{
    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required Guid InitialStateId { get; init; }

    /// <summary>所属不可变 revision（快照解析键；revision 提交后成员行永不改写）。</summary>
    public required Guid RevisionId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    /// <summary>time-zero 初始事实 payload（membership/placement/初始值；canonical JSON）。</summary>
    public required string MembershipPayloadJson { get; init; }

    /// <summary>可选初始 placement payload（与 schema version 成对）。</summary>
    public string? PlacementPayloadJson { get; init; }

    public long? PlacementSchemaVersion { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}
