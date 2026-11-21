using System;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;

namespace WorldView.Domain.Maps;

/// <summary>
/// Map 创建服务（Core Domain）：校验 Project 存在 → 构建创建计划（含根 History、
/// default Calendar、day-zero Configuration，P2-09-11 §4.1 不变量）→ 单事务持久化。
/// </summary>
public static class MapService
{
    /// <summary>创建 Map；Project 不存在抛 <see cref="InvalidOperationException"/>。</summary>
    public static MapCreationPlan CreateMap(
        ICoreDomainDatabase database,
        Guid projectId,
        string mapName,
        long nowUtcMs)
    {
        var project = database.FindProject(projectId)
            ?? throw new InvalidOperationException($"项目 {projectId} 不存在，无法创建地图。");

        var plan = MapRules.CreateMap(project, mapName, nowUtcMs);
        database.CreateMapStructure(plan);
        return plan;
    }
}

/// <summary>
/// Branch 创建服务（Core Domain）：父 History 必须存在于同一 Project 且祖先链有效
/// （可溯至某 Map 根历史、无环）→ 构建计划（自有 History + Branch 同事务）→ 单事务持久化。
/// ADR-005 规则 1（任意历史时间点可创建）由 <see cref="HistoricalTime"/> 类型保证。
/// </summary>
public static class BranchService
{
    /// <summary>创建 Branch；父历史缺失或祖先链非法抛 <see cref="InvalidOperationException"/>。</summary>
    public static BranchCreationPlan CreateBranch(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid parentHistoryId,
        string branchName,
        HistoricalTime parentAt,
        long nowUtcMs)
    {
        var project = database.FindProject(projectId)
            ?? throw new InvalidOperationException($"项目 {projectId} 不存在，无法创建分支。");

        var parentHistory = database.FindHistory(projectId, parentHistoryId)
            ?? throw new InvalidOperationException($"父历史 {parentHistoryId} 不存在或不属于项目 {projectId}。");

        // 祖先链预检：父历史必须上溯到某个 Map 根历史（无环、无悬空、无主分支历史）。
        ValidateAncestry(database, projectId, parentHistory);

        var plan = BranchRules.CreateBranch(project, parentHistory, branchName, parentAt, nowUtcMs);
        database.CreateBranchStructure(plan);
        return plan;
    }

    /// <summary>上溯校验：终止于任一 Map 根历史即合法（分叉目标 Map 由调用方语境决定）。</summary>
    private static void ValidateAncestry(ICoreDomainDatabase database, Guid projectId, HistoryRecord parentHistory)
    {
        var visited = new System.Collections.Generic.HashSet<Guid> { parentHistory.HistoryId };
        var current = parentHistory;
        while (current.Kind != HistoryKind.MapRoot)
        {
            var owner = database.FindBranchByOwnHistory(projectId, current.HistoryId)
                ?? throw new InvalidOperationException(
                    $"branch_owned 历史 {current.HistoryId} 没有对应的 Branch（数据损坏），拒绝创建分支。");

            if (!visited.Add(owner.ParentHistoryId))
            {
                throw new InvalidOperationException("父历史祖先链存在环，拒绝创建分支。");
            }

            current = database.FindHistory(projectId, owner.ParentHistoryId)
                ?? throw new InvalidOperationException(
                    $"父历史 {owner.ParentHistoryId} 不存在（数据不完整），拒绝创建分支。");
        }
    }
}
