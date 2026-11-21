using System;
using WorldView.Domain.Projects;
using WorldView.Domain.Time;

namespace WorldView.Domain.Maps;

/// <summary>
/// Branch 创建计划：自有 History（branch_owned）与 Branch 行在同一事务内创建
/// （P2-09-11 §4.3：core_history 不回指 core_branch，避免循环 FK）。
/// </summary>
public sealed record BranchCreationPlan
{
    public required BranchRecord Branch { get; init; }

    public required HistoryRecord OwnHistory { get; init; }
}

/// <summary>
/// Branch 分叉规则（Core Domain 独占；ADR-005 已冻结）：
/// 1) 任意历史时间点可创建（parent_at ≥ 纪元，由 HistoricalTime 类型保证）；
/// 2) 继承分叉点之前（闭边界 ≤ parent_at）的父历史；
/// 3) 不复制祖先历史——继承经 parent_history_id + parent_at 动态计算；
/// 8) 分叉点为分叉时刻本身（parent ≠ own history）。
/// 自有 State Change 严格晚于 parent_at（规则 7）属 Command 写入校验，CD-01 无 Command 路径。
/// </summary>
public static class BranchRules
{
    /// <summary>分支名规范化：非空白，去首尾空白。</summary>
    public static string NormalizeName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new ArgumentException("分支名不能为空白。", nameof(branchName));
        }

        return branchName.Trim();
    }

    /// <summary>
    /// 构建 Branch 创建计划。调用方须已确认 parentHistory 存在于同一 Project
    /// 且其祖先链有效（见 <see cref="BranchLineageResolver"/>）；
    /// parent_at 合法性由 <see cref="HistoricalTime"/> 构造保证。
    /// </summary>
    public static BranchCreationPlan CreateBranch(
        ProjectRecord project,
        HistoryRecord parentHistory,
        string branchName,
        HistoricalTime parentAt,
        long nowUtcMs)
    {
        if (parentHistory.ProjectId != project.ProjectId)
        {
            throw new ArgumentException("父历史不属于该项目。", nameof(parentHistory));
        }

        var name = NormalizeName(branchName);
        var branchId = Guid.NewGuid();
        var ownHistoryId = Guid.NewGuid();

        return new BranchCreationPlan
        {
            Branch = new BranchRecord
            {
                BranchId = branchId,
                ProjectId = project.ProjectId,
                ParentHistoryId = parentHistory.HistoryId,
                OwnHistoryId = ownHistoryId,
                ParentAt = parentAt,
                BranchName = name,
                LifecycleStatus = MapLifecycle.ActiveStatus,
                CreatedAtUtcMs = nowUtcMs,
                UpdatedAtUtcMs = nowUtcMs,
            },
            OwnHistory = new HistoryRecord
            {
                HistoryId = ownHistoryId,
                ProjectId = project.ProjectId,
                Kind = HistoryKind.BranchOwned,
                OwnerMapId = null,
                CreatedAtUtcMs = nowUtcMs,
            },
        };
    }
}
