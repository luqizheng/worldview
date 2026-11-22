using System;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;

namespace WorldView.Domain.Workspace;

/// <summary>经 Core Domain 校验的 Map Workspace 上下文（域形式）。</summary>
public sealed record ResolvedWorkspaceContext
{
    public required Guid ProjectId { get; init; }

    public required Guid MapId { get; init; }

    public required string MapName { get; init; }

    /// <summary>Map 根历史 ID。</summary>
    public required Guid RootHistoryId { get; init; }

    /// <summary>选定 Branch；null = 根历史（默认/主历史）。</summary>
    public Guid? BranchId { get; init; }

    /// <summary>解析出的分叉世系（根→选定 Branch；根历史时深度为 0）。</summary>
    public required BranchLineage Lineage { get; init; }

    /// <summary>规范历史时间（null 输入解析为纪元）。</summary>
    public required HistoricalTime Time { get; init; }
}

/// <summary>
/// Workspace 上下文解析（Core Domain 独占，AF-01 §6 规则 2）：
/// 验证 Map 归属 → 解析根 History → 解析/校验 Branch 世系（环/跨 Map/悬空拒绝）
/// → 解析规范历史时间。本阶段只做上下文解析，不做 Event Replay / World State 重建。
/// </summary>
public static class WorkspaceContextRules
{
    /// <summary>
    /// 解析并校验。失败返回错误说明（不抛业务异常）；行级数据损坏经
    /// <see cref="CoreDomainDataException"/> fail-closed。
    /// </summary>
    public static bool TryResolve(
        ICoreDomainDatabase database,
        Guid projectId,
        Guid mapId,
        Guid? branchId,
        string? historicalTimeInput,
        out ResolvedWorkspaceContext? context,
        out string? error)
    {
        // 1. Map 存在且属于当前 Project（跨 Project 查询在此被拒绝）。
        var map = database.FindMap(projectId, mapId);
        if (map is null)
        {
            context = null;
            error = $"地图 {mapId} 不存在或不属于项目 {projectId}。";
            return false;
        }

        // 2. 根 History（P2-09-11：经 core_history.owner_map_id 唯一解析；缺失=数据损坏）。
        var rootHistory = database.FindRootHistoryByMap(projectId, mapId);
        if (rootHistory is null)
        {
            context = null;
            error = $"地图 {mapId} 缺少根历史（数据不完整）。";
            return false;
        }

        // 3. Branch 世系（null = 根历史）。
        BranchLineage lineage = new() { Depth = 0, RootHistoryId = rootHistory.HistoryId, Entries = Array.Empty<BranchLineageEntry>() };
        if (branchId is { } selectedBranchId)
        {
            var branch = database.FindBranch(projectId, selectedBranchId);
            if (branch is null)
            {
                context = null;
                error = $"分支 {selectedBranchId} 不存在或不属于项目 {projectId}。";
                return false;
            }

            var resolved = BranchLineageResolver.TryResolve(
                branch,
                expectedOwnerMapId: mapId,
                findHistory: historyId => database.FindHistory(projectId, historyId),
                findBranchByOwnHistory: ownHistoryId => database.FindBranchByOwnHistory(projectId, ownHistoryId),
                out var lineageError);
            if (resolved is null)
            {
                context = null;
                error = lineageError;
                return false;
            }

            lineage = resolved;
        }

        // 4. 规范历史时间（null → 纪元；时间轴为 Map 相对，Branch 完整继承）。
        HistoricalTime time = HistoricalTime.Epoch;
        if (historicalTimeInput is not null)
        {
            if (!HistoricalTime.TryParse(historicalTimeInput, out var parsed) || parsed is null)
            {
                context = null;
                error = $"历史时间输入 \"{historicalTimeInput}\" 不是合法的规范格式 \"day:offset\"（如 \"1200:43200\"）。";
                return false;
            }

            time = parsed;
        }

        context = new ResolvedWorkspaceContext
        {
            ProjectId = projectId,
            MapId = mapId,
            MapName = map.MapName,
            RootHistoryId = rootHistory.HistoryId,
            BranchId = branchId,
            Lineage = lineage,
            Time = time,
        };
        error = null;
        return true;
    }
}
