using System;
using WorldView.Domain.Time;

namespace WorldView.Presentation.Identity;

/// <summary>
/// 语义工作区身份：MapId + BranchId + RootHistoryId + typed HistoricalTime 四元组
/// （M1 Profile §6.4 的语义上下文切换判定依据）。视口代、样式/字体 token、装配版本
/// 等呈现参数不参与语义身份——它们的变化是同一语义上下文内的普通刷新
/// （保留 last-known-good），不触发快照清空。
/// </summary>
public readonly struct WorkspaceSemanticIdentity : IEquatable<WorkspaceSemanticIdentity>
{
    /// <summary>权威 Map 身份。</summary>
    public Guid MapId { get; }

    /// <summary>选定 Branch；null = 根历史（默认/主历史）。</summary>
    public Guid? BranchId { get; }

    /// <summary>Map 根历史 ID。</summary>
    public Guid RootHistoryId { get; }

    /// <summary>typed 规范历史时间（保留 day/offset 全精度）。</summary>
    public HistoricalTime HistoricalTime { get; }

    public WorkspaceSemanticIdentity(Guid mapId, Guid? branchId, Guid rootHistoryId, HistoricalTime historicalTime)
    {
        MapId = mapId;
        BranchId = branchId;
        RootHistoryId = rootHistoryId;
        HistoricalTime = historicalTime ?? throw new ArgumentNullException(nameof(historicalTime));
    }

    public bool Equals(WorkspaceSemanticIdentity other)
        => MapId == other.MapId
            && BranchId == other.BranchId
            && RootHistoryId == other.RootHistoryId
            && HistoricalTime.Equals(other.HistoricalTime);

    public override bool Equals(object? obj) => obj is WorkspaceSemanticIdentity other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(MapId, BranchId, RootHistoryId, HistoricalTime);

    public override string ToString()
        => $"{MapId}/{(BranchId?.ToString() ?? "root-history")}/{RootHistoryId}/{HistoricalTime.Canonical()}";

    public static bool operator ==(WorkspaceSemanticIdentity left, WorkspaceSemanticIdentity right) => left.Equals(right);

    public static bool operator !=(WorkspaceSemanticIdentity left, WorkspaceSemanticIdentity right) => !left.Equals(right);
}
