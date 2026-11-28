using System;
using System.Collections.Generic;

namespace WorldView.Domain.Objects;

/// <summary>
/// Immutable revision metadata for one complete time-zero Map snapshot.
/// Revision identity and lineage are distinct from the snapshot content fingerprint.
/// </summary>
public sealed record MapInitialStateRevisionRecord
{
    public required Guid RevisionId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required long RevisionNumber { get; init; }
    public Guid? ParentRevisionId { get; init; }
    public required long BaselineSchemaVersion { get; init; }
    public required string ContentFingerprint { get; init; }
    public string? OperationKind { get; init; }
    public string? OperationPayloadJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>The only mutable authoritative row in the baseline revision model.</summary>
public sealed record MapInitialStateCursorRecord
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required Guid ActiveRevisionId { get; init; }
    public Guid? SelectedRedoChildRevisionId { get; init; }
    public required long CursorVersion { get; init; }
    public required long UpdatedAtUtcMs { get; init; }
}

/// <summary>A complete immutable snapshot resolved from one revision.</summary>
public sealed record MapInitialStateRevisionSnapshot
{
    public required MapInitialStateRevisionRecord Revision { get; init; }
    public required IReadOnlyList<InitialObjectRecord> Members { get; init; }
}

/// <summary>Active revision/cursor pair returned as one logical read.</summary>
public sealed record MapInitialStateRevisionState
{
    public required MapInitialStateRevisionSnapshot Snapshot { get; init; }
    public required MapInitialStateCursorRecord Cursor { get; init; }
    public required IReadOnlyList<MapInitialStateRevisionRecord> RedoChildren { get; init; }
}

/// <summary>Append a complete child snapshot against an expected active cursor.</summary>
public sealed record AppendMapInitialStateRevisionPlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    /// <summary>新 revision 身份（Domain 预生成；持久化层不产生身份）。</summary>
    public required Guid RevisionId { get; init; }
    /// <summary>完整子快照成员集合（顺序无关；按 identity 去重校验）。</summary>
    public required IReadOnlyList<InitialObjectWrite> Members { get; init; }
    /// <summary>Domain 按提交行计算的权威指纹；持久化层事务内重算并拒绝不一致。</summary>
    public required string ContentFingerprint { get; init; }
    /// <summary>canonical 操作描述（added/removed/updated + 父子指纹）；持久化层重算并拒绝不一致。</summary>
    public required string OperationPayloadJson { get; init; }
    public required long NowUtcMs { get; init; }
}

/// <summary>Persist a redo branch choice without moving the active revision.</summary>
public sealed record SelectMapInitialStateRedoChildPlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    public required Guid ChildRevisionId { get; init; }
    public required long NowUtcMs { get; init; }
}

/// <summary>Move the active cursor to a parent (Undo) or direct child (Redo).</summary>
public sealed record MoveMapInitialStateCursorPlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid InitialStateId { get; init; }
    public required Guid ExpectedActiveRevisionId { get; init; }
    public required long ExpectedCursorVersion { get; init; }
    public required Guid TargetRevisionId { get; init; }
    public required bool IsRedo { get; init; }
    public required long NowUtcMs { get; init; }
}
