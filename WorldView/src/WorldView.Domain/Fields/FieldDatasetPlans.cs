using System;

namespace WorldView.Domain.Fields;

/// <summary>
/// Publication request. The caller pins the current revision observed when staging began;
/// the persistence transaction rechecks that pin before advancing the Map-wide source baseline.
/// </summary>
public sealed record PublishFieldRevisionPlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    public required Guid FieldDatasetId { get; init; }
    public required string FieldKey { get; init; }
    public required int DescriptorSchemaVersion { get; init; }
    public required string SemanticsId { get; init; }
    public required Guid FieldRevisionId { get; init; }
    public required Guid FieldRevisionAssetId { get; init; }
    public Guid? ExpectedCurrentFieldRevisionId { get; init; }
    public required string ContentFingerprint { get; init; }
    public required string ManifestJson { get; init; }
    public required string ProvenanceJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}
