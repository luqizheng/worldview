using System;

namespace WorldView.Domain.Fields;

/// <summary>
/// Metadata registration request after the selected storage adapter has written and
/// verified the exact payload bytes. The persistence implementation must verify revision
/// ownership and enforce one payload per Field Revision in the same publication transaction.
/// </summary>
public sealed record RegisterFieldPayloadPlan
{
    public required Guid FieldPayloadId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid FieldDatasetId { get; init; }
    public required Guid FieldRevisionId { get; init; }
    public required FieldPayloadStorageKind StorageKind { get; init; }
    public required long ByteLength { get; init; }
    public required string ContentHash { get; init; }
    public string? RelativePath { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}
