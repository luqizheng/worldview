using System;
using System.Collections.Generic;
using WorldView.Domain.Maps;
using WorldView.Domain.Projects;

namespace WorldView.Domain.Persistence;

/// <summary>
/// Core Domain 持久化端口（Domain 拥有；由 Infrastructure 以 SQLite 实现）。
/// 结构写操作（InsertProject / CreateMapStructure / CreateBranchStructure）各自构成
/// 一个原子事务，失败整体回滚，不留半成品（事务边界归实现方，AF-02 端口契约）。
/// 读操作按 Project 范围隔离；行数据损坏时实现方抛 <see cref="CoreDomainDataException"/>
/// （fail-closed，不静默修补）。
/// </summary>
public interface ICoreDomainDatabase
{
    /// <summary>按 ID 查找 Project；不存在返回 null。</summary>
    ProjectRecord? FindProject(Guid projectId);

    /// <summary>读取 core_project 全部原始行（身份校验规则归 ProjectRules）。</summary>
    IReadOnlyList<RawProjectIdentityRow> ReadProjectIdentityRows();

    /// <summary>事务：插入 Project 身份行。已存在任何行时抛异常并回滚（一库一项目）。</summary>
    void InsertProject(ProjectRecord project);

    /// <summary>查找属于指定 Project 的 Map；不存在或跨 Project 返回 null。</summary>
    MapRecord? FindMap(Guid projectId, Guid mapId);

    /// <summary>列出指定 Project 的全部 Map（按创建时间、ID 稳定排序）。</summary>
    IReadOnlyList<MapRecord> ListMaps(Guid projectId);

    /// <summary>
    /// 事务：一次写入 Map 创建计划的全部结构（Map、根 History、default Calendar、
    /// day-zero Configuration）；任一失败整体回滚（P2-09-11 §4.1 不变量）。
    /// </summary>
    void CreateMapStructure(MapCreationPlan plan);

    /// <summary>按 ID 查找 History；不存在返回 null。</summary>
    HistoryRecord? FindHistory(Guid projectId, Guid historyId);

    /// <summary>解析 Map 的根 History（history_kind=map_root 且 owner_map_id=map）；缺失返回 null。</summary>
    HistoryRecord? FindRootHistoryByMap(Guid projectId, Guid mapId);

    /// <summary>按 ID 查找 Branch；不存在返回 null。</summary>
    BranchRecord? FindBranch(Guid projectId, Guid branchId);

    /// <summary>按自有历史查找 Branch（世系上溯用）；无对应 Branch 返回 null。</summary>
    BranchRecord? FindBranchByOwnHistory(Guid projectId, Guid ownHistoryId);

    /// <summary>事务：一次写入 Branch 自有 History 与 Branch 行；失败整体回滚。</summary>
    void CreateBranchStructure(BranchCreationPlan plan);
}

/// <summary>Core Domain 数据损坏（行不可解析、不变量破坏、结构缺失）：fail-closed，不静默修补。</summary>
public sealed class CoreDomainDataException : Exception
{
    public CoreDomainDataException(string message)
        : base(message)
    {
    }

    public CoreDomainDataException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
