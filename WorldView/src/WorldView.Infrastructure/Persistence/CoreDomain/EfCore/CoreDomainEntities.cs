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

    // Typed immutable semantic snapshot. ManifestJson remains an interchange/diagnostic copy,
    // not the source used to reconstruct Domain metadata.
    public required string FieldKey { get; set; }
    public int DescriptorSchemaVersion { get; set; }
    public required string SemanticsId { get; set; }
    public string? Unit { get; set; }
    public required string MapSpaceId { get; set; }
    public required string SpatialReferenceId { get; set; }
    public required string CoordinatePrecisionProfileId { get; set; }
    public string? VerticalReferenceId { get; set; }
    public long GridWidth { get; set; }
    public long GridHeight { get; set; }
    public int GridTileWidth { get; set; }
    public int GridTileHeight { get; set; }
    public required string GridOriginX { get; set; }
    public required string GridOriginY { get; set; }
    public required string GridSpacingX { get; set; }
    public required string GridSpacingY { get; set; }
    public required string GridAxisOrientation { get; set; }
    public required string GridCellRegistration { get; set; }
    public required string CoverageMode { get; set; }
    public string? CoverageMaskPlaneName { get; set; }
    public required string MapEdgeSemantics { get; set; }
    public required string NoDataPolicy { get; set; }
    public required string NoDataMaskPlaneName { get; set; }
    public int NoDataValidValue { get; set; }
    public int NoDataInvalidValue { get; set; }
    public required string ValueProfileId { get; set; }
    public required string ValueProfileScope { get; set; }
    public required string TileHashAlgorithm { get; set; }
    public int TileHashVersion { get; set; }
}

internal sealed class CoreMapFieldRevisionPlaneEntity
{
    public Guid FieldRevisionId { get; set; }
    public int Ordinal { get; set; }
    public required string Name { get; set; }
    public required string SampleFormat { get; set; }
    public int BytesPerCell { get; set; }
}

internal sealed class CoreMapFieldRevisionCategoryCodeEntity
{
    public Guid FieldRevisionId { get; set; }
    public required string CategoryKey { get; set; }
    public int CategoryCode { get; set; }
}

internal sealed class CoreMapFieldRevisionCodecEntity
{
    public Guid FieldRevisionId { get; set; }
    public int Ordinal { get; set; }
    public int CodecId { get; set; }
    public required string CodecName { get; set; }
    public required string Version { get; set; }
    public required string ParametersJson { get; set; }
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
    public required string PrecisionCoordinateUnit { get; set; }
    public required string PrecisionAxisOrder { get; set; }
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

/// <summary>
/// Physical storage metadata and optional inline bytes for one immutable Field Revision.
/// Exactly one row belongs to a revision; payload bytes are raw and never Base64/JSON wrapped.
/// </summary>
internal sealed class CoreFieldPayloadEntity
{
    public Guid FieldPayloadId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FieldDatasetId { get; set; }
    public Guid FieldRevisionId { get; set; }
    public required string StorageKind { get; set; }
    public required string PayloadFormat { get; set; }
    public required string FormatVersion { get; set; }
    public required string HashAlgorithm { get; set; }
    public required string ContentHash { get; set; }
    public long ByteLength { get; set; }
    public byte[]? PayloadBlob { get; set; }
    public string? RelativePath { get; set; }
    public required string IntegrityState { get; set; }
    public long CreatedAtUtcMs { get; set; }
}
