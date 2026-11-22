using System;

namespace WorldView.Domain.Fields;

/// <summary>Map-owned source Field Dataset identity and its current published revision pin.</summary>
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

/// <summary>Immutable published Field Revision metadata and typed Asset ownership.</summary>
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
    /// <summary>Authoritative typed semantic projection; ManifestJson is retained for interchange/diagnostics.</summary>
    public FieldRevisionMetadata? Metadata { get; init; }
    public required string ProvenanceJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}
