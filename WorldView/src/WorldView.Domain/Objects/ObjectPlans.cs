using System;
using System.Collections.Generic;

namespace WorldView.Domain.Objects;

/// <summary>
/// Project 创建计划（OD-4 建议案）：Project 身份行 + 十内置 Object Type 行 +
/// 各可分配类型的 allocator 行，在同一事务内写入（避免 Project 存在而类型行缺失的窗口）。
/// 由 <see cref="ObjectTypeSeedingService.BuildCreationPlan"/> 构建类型行。
/// </summary>
public sealed record ProjectCreationPlan
{
    public required Projects.ProjectRecord Project { get; init; }

    /// <summary>内置类型种子行（Project 创建事务内插入；P2-09-21 V0.1 / P2-09-18 §4.2）。</summary>
    public required IReadOnlyList<ObjectTypeRecord> BuiltinObjectTypes { get; init; }

    /// <summary>便捷构建：为既有身份记录补齐内置类型种子行。</summary>
    public static ProjectCreationPlan Create(Projects.ProjectRecord project) => new()
    {
        Project = project,
        BuiltinObjectTypes = ObjectTypeSeedingService.BuildSeedRows(project.ProjectId, project.CreatedAtUtcMs),
    };
}

/// <summary>Object ID 分配请求（Reserve 写路径；allocator 前进 + reservation 插入同事务）。</summary>
public sealed record ObjectIdReservationRequest
{
    public required Guid ProjectId { get; init; }

    public required Guid ObjectTypeId { get; init; }

    /// <summary>逻辑分配请求键（规范化后非空时 (project, request_key) 幂等对账，P2-09-04 §6）。</summary>
    public string? RequestKey { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>分配结果：新增分配或按 request_key 幂等对账返回既有分配（TX-21/22 语义）。</summary>
public sealed record ReservationAllocation
{
    public required ReservationRecord Reservation { get; init; }

    /// <summary>true = 同 request_key 命中既有 reservation（未新消耗 ID）。</summary>
    public required bool RecoveredByRequestKey { get; init; }
}

/// <summary>Reserved→Bound 请求：身份键由 Domain 服务生成；pin 由服务按内置目录推导。</summary>
public sealed record ObjectBindRequest
{
    public required Guid ProjectId { get; init; }

    public required Guid ReservationId { get; init; }

    /// <summary>Core Domain 生成的身份键（canonical UUID；与 reservation 1:1 绑定）。</summary>
    public required Guid ObjectIdentityId { get; init; }

    public required ObjectSharingScope SharingScope { get; init; }

    /// <summary>仅 MapLocal 非空（R-scope 配对由服务与 CHECK 双保险）。</summary>
    public Guid? OwnerMapId { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>Insert 路径的可选 Map 注册（与正式创建同事务写入）。</summary>
public sealed record MapObjectRegistration
{
    /// <summary>注册行 ID（Domain 服务生成）。</summary>
    public required Guid MapObjectRefId { get; init; }

    public required Guid MapId { get; init; }
}

/// <summary>Insert 路径的可选 time-zero 成员（必须有同 Map 注册；P3⇒P2）。</summary>
public sealed record InitialObjectMembership
{
    /// <summary>time-zero 初始事实 payload（canonical JSON；Domain 规范化后写入）。</summary>
    public required string MembershipPayloadJson { get; init; }

    /// <summary>可选初始 placement payload（与 schema version 成对）。</summary>
    public string? PlacementPayloadJson { get; init; }

    public long? PlacementSchemaVersion { get; init; }
}

/// <summary>
/// 正式创建计划（Bound→Inserted 原子边界，P2-09-12 TX-55）：identity draft→active +
/// reservation→Inserted + definition 首行 + 可选注册 + 可选 Initial State 成员，
/// 全部同一事务；失败整体回滚、无半成品。
/// </summary>
public sealed record ObjectInsertPlan
{
    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    public required Guid ReservationId { get; init; }

    /// <summary>首行定义（definition_schema_version 必须与身份 pin 一致；payload canonical + hash 由适配器经 Domain 规范化计算）。</summary>
    public required ObjectDefinitionRecord Definition { get; init; }

    /// <summary>可选 Map 注册（map_local 仅 owner Map）。</summary>
    public MapObjectRegistration? Registration { get; init; }

    /// <summary>可选 time-zero 成员（指定时 Registration 必须同 Map；基线缺失则同事务建立）。</summary>
    public InitialObjectMembership? InitialMembership { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>Definition CAS 编辑计划（expected hash 不符即 typed conflict，P2-09-37 §3.3 模式）。</summary>
public sealed record ObjectDefinitionEditPlan
{
    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    /// <summary>当前定义哈希（调用方先读后改的 CAS 凭据；不匹配拒绝）。</summary>
    public required string ExpectedDefinitionHash { get; init; }

    /// <summary>新 payload（规范化后写入并重算 hash）。</summary>
    public required string DefinitionPayloadJson { get; init; }

    public required long DefinitionSchemaVersion { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>
/// Building/Cave Definition 与完整 Geometry Asset link set 的原子发布计划。
/// link set 从 canonical Definition payload 派生，不接受调用方提供的部分 link 清单。
/// </summary>
public sealed record ObjectDefinitionPublicationPlan
{
    public required Guid ProjectId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    public required string ExpectedDefinitionHash { get; init; }

    public required string DefinitionPayloadJson { get; init; }

    public required long DefinitionSchemaVersion { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>Map 注册计划（单行写；scope 规则服务层 + 触发器双保险）。</summary>
public sealed record MapObjectRegistrationPlan
{
    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required Guid ObjectIdentityId { get; init; }

    /// <summary>注册行 ID（Domain 服务生成）。</summary>
    public required Guid MapObjectRefId { get; init; }

    public required long NowUtcMs { get; init; }
}

/// <summary>Initial State 成员写入项（完整替换集合的元素）。</summary>
public sealed record InitialObjectWrite
{
    public required Guid ObjectIdentityId { get; init; }

    /// <summary>time-zero 初始事实 payload（canonical JSON）。</summary>
    public required string MembershipPayloadJson { get; init; }

    public string? PlacementPayloadJson { get; init; }

    public long? PlacementSchemaVersion { get; init; }
}

/// <summary>Map creation's immutable empty baseline root and initial cursor.</summary>
public sealed record MapInitialStateCreationPlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required Guid RevisionZeroId { get; init; }
    public required long BaselineSchemaVersion { get; init; }
    public required string EmptyContentFingerprint { get; init; }
    public required long NowUtcMs { get; init; }
}

/// <summary>Append one complete immutable snapshot as a child of the expected active revision.</summary>
public sealed record EditInitialStatePlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    public required Guid NewRevisionId { get; init; }
    public required long BaselineSchemaVersion { get; init; }
    public required IReadOnlyList<InitialObjectWrite> Members { get; init; }
    public required string OperationPayloadJson { get; init; }
    public required string ContentFingerprint { get; init; }
    public required long NowUtcMs { get; init; }
}

/// <summary>Move the active time-zero cursor to the active revision's parent.</summary>
public sealed record UndoInitialStatePlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    public required long NowUtcMs { get; init; }
}

/// <summary>Move the active time-zero cursor to an explicitly selected direct child.</summary>
public sealed record RedoInitialStatePlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    public required Guid SelectedChildRevisionId { get; init; }
    public required long NowUtcMs { get; init; }
}
