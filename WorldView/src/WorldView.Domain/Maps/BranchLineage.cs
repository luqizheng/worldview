using System;
using System.Collections.Generic;

namespace WorldView.Domain.Maps;

/// <summary>Branch 链上的一级：一个 Branch 的自有历史与其父历史/分叉边界。</summary>
public sealed record BranchLineageEntry
{
    public required Guid BranchId { get; init; }

    public required Guid OwnHistoryId { get; init; }

    public required Guid ParentHistoryId { get; init; }

    /// <summary>本 Branch 的分叉时间（闭边界：父历史 ≤ 此时刻的内容被继承）。</summary>
    public required Time.HistoricalTime ParentAt { get; init; }
}

/// <summary>
/// 解析出的 Branch 世系：从 Map 根历史到选定 Branch 的完整链（根→叶排序）。
/// 历史继承按 (parent_history_id, parent_at) 动态计算，绝不复制祖先数据（ADR-002/005）。
/// </summary>
public sealed class BranchLineage
{
    /// <summary>链上 Branch 数（0 = 根历史本身）。</summary>
    public required int Depth { get; init; }

    /// <summary>Map 根历史 ID。</summary>
    public required Guid RootHistoryId { get; init; }

    /// <summary>根→叶顺序的分叉链；根历史时为空列表。</summary>
    public required IReadOnlyList<BranchLineageEntry> Entries { get; init; }

    /// <summary>选定 Branch（null = 根历史）。</summary>
    public Guid? SelectedBranchId => Depth == 0 ? null : Entries[^1].BranchId;
}

/// <summary>
/// Branch 世系解析（Core Domain 独占）：沿 parent_history_id 自选定 Branch 上溯至 Map 根历史。
/// 数据加载经委托注入（纯函数，可测）：环检测、悬空引用、branch_owned 历史无主、
/// 终止于其他 Map 的根历史（跨 Map）均拒绝。
/// </summary>
public static class BranchLineageResolver
{
    /// <summary>防御性深度上限（正常数据远低于此；超出视为数据损坏）。</summary>
    public const int MaxDepth = 1_000;

    /// <summary>
    /// 解析 <paramref name="branch"/> 自有历史之上的完整世系。
    /// 返回 null 与错误说明的情况：初始 Branch 自有历史缺失或非 branch_owned
    /// （CD-01-F1 §6.3：初始输入与自身 History 同样做完整一致性校验，不只校验上溯
    /// 中间节点）、父历史缺失、环、超深、branch_owned 历史无对应 Branch、
    /// 根历史属于其他 Map（<paramref name="expectedOwnerMapId"/> 不符）。
    /// </summary>
    public static BranchLineage? TryResolve(
        BranchRecord branch,
        Guid expectedOwnerMapId,
        Func<Guid, HistoryRecord?> findHistory,
        Func<Guid, BranchRecord?> findBranchByOwnHistory,
        out string? error)
    {
        // 初始 Branch 自身一致性：自有历史必须存在且为 branch_owned（委托按 Project
        // 范围查询，跨 Project 的自有历史在此表现为不存在）。
        var ownHistory = findHistory(branch.OwnHistoryId);
        if (ownHistory is null)
        {
            error = $"分支 {branch.BranchId} 的自有历史 {branch.OwnHistoryId} 不存在（数据不完整）。";
            return null;
        }

        if (ownHistory.Kind != HistoryKind.BranchOwned)
        {
            error = $"分支 {branch.BranchId} 的自有历史 {branch.OwnHistoryId} 不是 branch_owned（数据损坏）。";
            return null;
        }

        var chain = new List<BranchLineageEntry> { new()
        {
            BranchId = branch.BranchId,
            OwnHistoryId = branch.OwnHistoryId,
            ParentHistoryId = branch.ParentHistoryId,
            ParentAt = branch.ParentAt,
        } };
        var visitedHistories = new HashSet<Guid> { branch.OwnHistoryId };

        var currentParent = branch.ParentHistoryId;
        while (true)
        {
            if (visitedHistories.Contains(currentParent))
            {
                error = $"历史 {currentParent} 形成环（祖先链非法）。";
                return null;
            }

            var history = findHistory(currentParent);
            if (history is null)
            {
                error = $"父历史 {currentParent} 不存在（数据不完整）。";
                return null;
            }

            if (history.Kind == HistoryKind.MapRoot)
            {
                if (history.OwnerMapId != expectedOwnerMapId)
                {
                    error = $"分支世系终止于其他地图的根历史（{history.OwnerMapId}），跨地图分支被拒绝。";
                    return null;
                }

                chain.Reverse();
                error = null;
                return new BranchLineage
                {
                    Depth = chain.Count,
                    RootHistoryId = history.HistoryId,
                    Entries = chain,
                };
            }

            // branch_owned 历史：继续经拥有它的 Branch 上溯。
            visitedHistories.Add(history.HistoryId);
            var owner = findBranchByOwnHistory(history.HistoryId);
            if (owner is null)
            {
                error = $"branch_owned 历史 {history.HistoryId} 没有对应的 Branch（数据损坏）。";
                return null;
            }

            chain.Add(new BranchLineageEntry
            {
                BranchId = owner.BranchId,
                OwnHistoryId = owner.OwnHistoryId,
                ParentHistoryId = owner.ParentHistoryId,
                ParentAt = owner.ParentAt,
            });

            if (chain.Count > MaxDepth)
            {
                error = $"分支世系深度超过上限 {MaxDepth}（数据损坏）。";
                return null;
            }

            currentParent = owner.ParentHistoryId;
        }
    }
}
