using System;

namespace WorldView.Domain.Fields;

public sealed record FieldDatasetRecord
{
    public required Guid FieldDatasetId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required string FieldKey { get; init; }
    public required int DescriptorSchemaVersion { get; init; }
    public required string SemanticsId { get; init; }
    public Guid? CurrentFieldRevisionId { get; init; }
    public required long CreatedAtUtcMs { get; init; }
    public required long UpdatedAtUtcMs { get; init; }
}

public sealed record FieldRevisionRecord
{
    public required Guid FieldRevisionId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid FieldDatasetId { get; init; }
    public required long RevisionNumber { get; init; }
    public Guid? ParentRevisionId { get; init; }
    public required Guid FieldRevisionAssetId { get; init; }
    public required string ContentFingerprint { get; init; }
    public required string ManifestJson { get; init; }
    public required string ProvenanceJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

public sealed record FieldRevisionPublishPlan
{
    public required Guid FieldRevisionId { get; init; }
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required string FieldKey { get; init; }
    public required int DescriptorSchemaVersion { get; init; }
    public required string SemanticsId { get; init; }
    public Guid? ExpectedCurrentRevisionId { get; init; }
    public required Guid FieldRevisionAssetId { get; init; }
    public required string ContentFingerprint { get; init; }
    public required string ManifestJson { get; init; }
    public required string ProvenanceJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}
