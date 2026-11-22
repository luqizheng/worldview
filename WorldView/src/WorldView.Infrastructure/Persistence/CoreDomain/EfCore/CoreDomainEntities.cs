namespace WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

/// <summary>EF persistence shape for the existing core_project table; not exposed to Domain.</summary>
internal sealed class CoreProjectEntity
{
    public Guid ProjectId { get; set; }
    public required string ProjectName { get; set; }
    public long ProjectFormatVersion { get; set; }
    public long CreatedAtUtcMs { get; set; }
    public long UpdatedAtUtcMs { get; set; }
}

/// <summary>EF persistence shape for the existing core_map table.</summary>
internal sealed class CoreMapEntity
{
    public Guid MapId { get; set; }
    public Guid ProjectId { get; set; }
    public required string MapSpaceId { get; set; }
    public string? SpatialReferenceId { get; set; }
    public required string MapName { get; set; }
    public required string LifecycleStatus { get; set; }
    public long CreatedAtUtcMs { get; set; }
    public long UpdatedAtUtcMs { get; set; }
}

/// <summary>EF persistence shape for the existing core_asset table.</summary>
internal sealed class CoreAssetEntity
{
    public Guid AssetId { get; set; }
    public Guid ProjectId { get; set; }
    public required string ContentHash { get; set; }
    public required string HashAlgorithm { get; set; }
    public long ByteLength { get; set; }
    public required string MediaType { get; set; }
    public required string FormatVersion { get; set; }
    public required string RelativePath { get; set; }
    public required string PublicationState { get; set; }
    public required string IntegrityState { get; set; }
    public long CreatedAtUtcMs { get; set; }
}


/// <summary>EF persistence shape for a Map-owned Field Dataset.</summary>
internal sealed class CoreMapFieldDatasetEntity
{
    public Guid FieldDatasetId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid MapId { get; set; }
    public required string FieldKey { get; set; }
    public int DescriptorSchemaVersion { get; set; }
    public required string SemanticsId { get; set; }
    public Guid? CurrentFieldRevisionId { get; set; }
    public long CreatedAtUtcMs { get; set; }
    public long UpdatedAtUtcMs { get; set; }
}

/// <summary>EF persistence shape for an immutable published Field Revision.</summary>
internal sealed class CoreMapFieldRevisionEntity
{
    public Guid FieldRevisionId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FieldDatasetId { get; set; }
    public long RevisionNumber { get; set; }
    public Guid? ParentRevisionId { get; set; }
    public Guid FieldRevisionAssetId { get; set; }
    public required string ContentFingerprint { get; set; }
    public required string ManifestJson { get; set; }
    public required string ProvenanceJson { get; set; }
    public long CreatedAtUtcMs { get; set; }
}

/// <summary>EF persistence shape for an immutable project-scoped Spatial Reference definition.</summary>
internal sealed class CoreSpatialReferenceEntity
{
    public Guid ProjectId { get; set; }
    public required string SpatialReferenceId { get; set; }
    public int DefinitionVersion { get; set; }
    public required string ReferenceKind { get; set; }
    public required string CoordinateUnit { get; set; }
    public required string AxisOrder { get; set; }
    public int Dimension { get; set; }
    public required string ValidDomainJson { get; set; }
    public required string WrapBehavior { get; set; }
    public string? VerticalReferenceId { get; set; }
    public required string PrecisionProfileId { get; set; }
    public int PrecisionProfileVersion { get; set; }
    public required string NumericRepresentation { get; set; }
    public required string MaximumAbsoluteQuantizationError { get; set; }
    public required string RoundingMode { get; set; }
    public required string MinimumAuthoringTolerance { get; set; }
    public required string SmallestMeaningfulFeatureSize { get; set; }
    public required string DefinitionParametersJson { get; set; }
    public required string MeasurementCapabilitiesJson { get; set; }
    public required string ProvenanceJson { get; set; }
    public required string DefinitionFingerprint { get; set; }
    public long CreatedAtUtcMs { get; set; }
}
