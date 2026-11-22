using System;
using WorldView.Domain.Time;

namespace WorldView.Domain.Maps;

/// <summary>Map 实体（core_map）：Core Domain 聚合根，属于恰好一个 Project（组合键防跨项目引用）。</summary>
public sealed record MapRecord
{
    public required Guid MapId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>地图名（非空白，去首尾空白后保存）。</summary>
    public required string MapName { get; init; }

    /// <summary>
    /// 生命周期状态。取值词汇表尚未在文档冻结（P2-09-11 open gate）；
    /// CD-01 由 Core Domain 服务统一写 <see cref="MapLifecycle.ActiveStatus"/>。
    /// </summary>
    public required string LifecycleStatus { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>生命周期状态常量（CD-01 词汇；正式枚举待后续 ADR 冻结）。</summary>
public static class MapLifecycle
{
    /// <summary>唯一由 CD-01 创建路径写入的状态。</summary>
    public const string ActiveStatus = "active";
}

/// <summary>History 种类（ADR-002：Map 与 Branch 使用同一种 History 实体）。</summary>
public enum HistoryKind
{
    /// <summary>Map 根历史：恰好归属一个 Map（owner_map_id 非空且项目内唯一）。</summary>
    MapRoot,

    /// <summary>Branch 自有历史：由 core_branch.own_history_id 反向链接，不指回 Map。</summary>
    BranchOwned,
}

/// <summary>History 实体（core_history）：Command/Event/顺序约束的归属作用域（ADR-002）。</summary>
public sealed record HistoryRecord
{
    public required Guid HistoryId { get; init; }

    public required Guid ProjectId { get; init; }

    public required HistoryKind Kind { get; init; }

    /// <summary>仅 MapRoot 非空；BranchOwned 必为 null。</summary>
    public Guid? OwnerMapId { get; init; }

    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>
/// Branch 实体（core_branch）：父是 History（不直接指向 Map，ADR-001/002），
/// 分叉时间 parent_at 记录祖先历史的闭边界（≤ parent_at 继承，ADR-005 规则 2）；
/// 不复制祖先历史（规则 3）。
/// </summary>
public sealed record BranchRecord
{
    public required Guid BranchId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>父 History（根历史或另一 Branch 的自有历史；非本 Branch 自有历史）。</summary>
    public required Guid ParentHistoryId { get; init; }

    /// <summary>本 Branch 自有历史（同事务创建；history_kind=branch_owned）。</summary>
    public required Guid OwnHistoryId { get; init; }

    /// <summary>分叉时间（Map 相对时间轴；Branch 完整继承根 Map 时间轴）。</summary>
    public required HistoricalTime ParentAt { get; init; }

    public required string BranchName { get; init; }

    public required string LifecycleStatus { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>Calendar 角色（每 Map 恰好一个 default；auxiliary 仅为显示备选）。</summary>
public enum CalendarRole
{
    Default,

    Auxiliary,
}

/// <summary>
/// Calendar 实体（core_calendar）：Map 拥有的身份实体（P2-09-14：Calendar 不是枚举）。
/// CD-01 仅建立结构性身份；calendar_type 与规则内容由后续 Calendar 设计阶段填入。
/// </summary>
public sealed record CalendarRecord
{
    public required Guid CalendarId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required CalendarRole Role { get; init; }

    /// <summary>日历类型标识；CD-01 结构占位阶段为 null（类型词汇表未冻结）。</summary>
    public string? CalendarType { get; init; }

    public required string CalendarName { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>Calendar Configuration 来源（command_managed 必须关联源 Command；CD-01 仅产生 user_created）。</summary>
public enum CalendarConfigurationOrigin
{
    UserCreated,

    CommandManaged,
}

/// <summary>
/// Calendar Configuration（core_calendar_configuration）：按 Historical Time 生效的显示规则。
/// CD-01 仅为新建 Map 写入 day-zero（effective_from = 纪元）的结构占位行
/// （rules_schema_version=0、rules_payload_json="{}"），满足 P2-09-11 §4.1 创建不变量；
/// 规则语义留待 P2-09-14 gates 关闭后正式化。
/// </summary>
public sealed record CalendarConfigurationRecord
{
    public required Guid CalendarConfigurationId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required Guid CalendarId { get; init; }

    public required Guid OwnerHistoryId { get; init; }

    /// <summary>仅 CommandManaged 非空（CD-01 无 Command，恒为 null）。</summary>
    public Guid? SourceCommandId { get; init; }

    public required CalendarConfigurationOrigin Origin { get; init; }

    /// <summary>生效起点（含）；day-zero 配置为纪元 (0,0)。</summary>
    public required HistoricalTime EffectiveFrom { get; init; }

    /// <summary>生效终点（不含）；成对可空 = 开放端点。CD-01 的 day-zero 配置为 null。</summary>
    public HistoricalTime? EffectiveUntil { get; init; }

    /// <summary>规则 schema 版本；0 = 结构性占位（无规则内容）。</summary>
    public required long RulesSchemaVersion { get; init; }

    /// <summary>规则 JSON；结构占位阶段为 "{}"。</summary>
    public required string RulesPayloadJson { get; init; }

    public required long CreatedAtUtcMs { get; init; }

    public required long UpdatedAtUtcMs { get; init; }
}
