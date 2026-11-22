using System;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;

namespace WorldView.Domain.Maps;

/// <summary>
/// Map 创建计划：一次 Core Domain 创建事务必须写入的全部结构
/// （P2-09-11 §4.1 不变量：恰好一条根 History、一个 default Calendar、
/// 该 Calendar 的 day-zero Configuration，缺一即完整性错误）。
/// 所有 ID 预生成，持久化适配器在同一事务内按依赖序写入。
/// </summary>
public sealed record MapCreationPlan
{
    public required MapRecord Map { get; init; }

    public required HistoryRecord RootHistory { get; init; }

    public required CalendarRecord DefaultCalendar { get; init; }

    public required CalendarConfigurationRecord DayZeroConfiguration { get; init; }
}

/// <summary>Map 创建规则（Core Domain 独占）。</summary>
public static class MapRules
{
    /// <summary>day-zero Calendar Configuration 的规则 schema 占位版本（无规则内容）。</summary>
    public const long PlaceholderRulesSchemaVersion = 0;

    /// <summary>day-zero Calendar Configuration 的占位规则 payload。</summary>
    public const string PlaceholderRulesPayloadJson = "{}";

    /// <summary>地图名规范化：非空白，去首尾空白。</summary>
    public static string NormalizeName(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            throw new ArgumentException("地图名不能为空白。", nameof(mapName));
        }

        return mapName.Trim();
    }

    /// <summary>
    /// 构建 Map 创建计划：生成 Map/根 History/default Calendar/day-zero Configuration 的
    /// 全部权威 UUID。分支时间轴（含 day zero）由此根结构确立；Branch 不创建独立时间轴。
    /// </summary>
    public static MapCreationPlan CreateMap(ProjectRecord project, string mapName, long nowUtcMs)
    {
        var name = NormalizeName(mapName);
        var mapId = Guid.NewGuid();
        var rootHistoryId = Guid.NewGuid();
        var calendarId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();

        return new MapCreationPlan
        {
            Map = new MapRecord
            {
                MapId = mapId,
                ProjectId = project.ProjectId,
                MapSpaceId = Guid.NewGuid().ToString("D"),
                SpatialReferenceId = null,
                MapName = name,
                LifecycleStatus = MapLifecycle.ActiveStatus,
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            },
            RootHistory = new HistoryRecord
            {
                HistoryId = rootHistoryId,
                ProjectId = project.ProjectId,
                Kind = HistoryKind.MapRoot,
                OwnerMapId = mapId,
                CreatedAtUtcMs = nowUtcMs,
            },
            DefaultCalendar = new CalendarRecord
            {
                CalendarId = calendarId,
                ProjectId = project.ProjectId,
                MapId = mapId,
                Role = CalendarRole.Default,
                CalendarType = null,
                CalendarName = "Default",
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            },
            DayZeroConfiguration = new CalendarConfigurationRecord
            {
                CalendarConfigurationId = configurationId,
                ProjectId = project.ProjectId,
                MapId = mapId,
                CalendarId = calendarId,
                OwnerHistoryId = rootHistoryId,
                SourceCommandId = null,
                Origin = CalendarConfigurationOrigin.UserCreated,
                EffectiveFrom = HistoricalTime.Epoch,
                EffectiveUntil = null,
                RulesSchemaVersion = PlaceholderRulesSchemaVersion,
                RulesPayloadJson = PlaceholderRulesPayloadJson,
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            },
        };
    }
}
