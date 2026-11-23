using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Maps;

/// <summary>
/// Immutable, project-scoped coordinate-reference definition. A definition is self-contained:
/// it does not depend on an online EPSG lookup or on renderer/viewport state.
/// </summary>
public sealed record SpatialReferenceDefinitionRecord
{
    public required Guid ProjectId { get; init; }
    public required string SpatialReferenceId { get; init; }
    public required int DefinitionVersion { get; init; }
    public required SpatialReferenceKind Kind { get; init; }
    public required string CoordinateUnit { get; init; }
    public required SpatialAxisOrder AxisOrder { get; init; }
    public required int Dimension { get; init; }
    public required string ValidDomainJson { get; init; }
    public required SpatialWrapBehavior WrapBehavior { get; init; }
    public string? VerticalReferenceId { get; init; }
    public required CoordinatePrecisionProfileRecord PrecisionProfile { get; init; }
    public required string DefinitionParametersJson { get; init; }
    public required string MeasurementCapabilitiesJson { get; init; }
    public required string ProvenanceJson { get; init; }
    public required string DefinitionFingerprint { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

public enum SpatialReferenceKind
{
    Geographic,
    Projected,
    LocalCartesian,
}

public enum SpatialAxisOrder
{
    XY,
    YX,
}

public enum SpatialWrapBehavior
{
    None,
    WrapX,
    WrapY,
    WrapXY,
}

/// <summary>
/// Versioned precision policy owned by a Spatial Reference. Decimal values are used so the
/// profile itself never passes through binary floating-point during validation or persistence.
/// </summary>
public sealed record CoordinatePrecisionProfileRecord
{
    public required string ProfileId { get; init; }
    public required int Version { get; init; }
    public required string CoordinateUnit { get; init; }
    public required SpatialAxisOrder AxisOrder { get; init; }
    public required string NumericRepresentation { get; init; }
    public required decimal MaximumAbsoluteQuantizationError { get; init; }
    public required MidpointRounding RoundingMode { get; init; }
    public required decimal MinimumAuthoringTolerance { get; init; }
    public required decimal SmallestMeaningfulFeatureSize { get; init; }
}

/// <summary>Construction and integrity rules for immutable Spatial Reference definitions.</summary>
public static class SpatialReferenceRules
{
    public static SpatialReferenceDefinitionRecord Create(SpatialReferenceDefinitionRecord candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var normalized = NormalizeAndValidate(candidate);
        return normalized with { DefinitionFingerprint = ComputeFingerprint(normalized) };
    }

    public static void ValidateFieldOwnerContext(
        MapRecord map,
        SpatialReferenceDefinitionRecord spatialReference,
        FieldRevisionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(spatialReference);
        ArgumentNullException.ThrowIfNull(metadata);
        if (map.ProjectId != spatialReference.ProjectId)
            throw new CoreDomainConflictException("Spatial Reference belongs to a different Project than the owning Map.");
        if (string.IsNullOrWhiteSpace(map.MapSpaceId)
            || !string.Equals(metadata.MapSpaceId, map.MapSpaceId, StringComparison.Ordinal))
            throw new CoreDomainConflictException("Field Revision Map Space ID does not match its owning Map.");
        if (string.IsNullOrWhiteSpace(map.SpatialReferenceId)
            || !string.Equals(metadata.SpatialReferenceId, map.SpatialReferenceId, StringComparison.Ordinal)
            || !string.Equals(spatialReference.SpatialReferenceId, map.SpatialReferenceId, StringComparison.Ordinal))
            throw new CoreDomainConflictException("Field Revision Spatial Reference ID does not match the Map's active definition.");
        if (!string.Equals(metadata.CoordinatePrecisionProfileId,
                spatialReference.PrecisionProfile.ProfileId, StringComparison.Ordinal))
            throw new CoreDomainConflictException("Field Revision coordinate precision profile does not match its Spatial Reference.");
        if (metadata.FieldKey == "elevation")
        {
            if (spatialReference.VerticalReferenceId is null
                || !string.Equals(metadata.VerticalReferenceId, spatialReference.VerticalReferenceId, StringComparison.Ordinal))
                throw new CoreDomainConflictException("Elevation Field Revision vertical reference does not match its Spatial Reference.");
        }
        else if (metadata.VerticalReferenceId is not null)
        {
            throw new CoreDomainConflictException("Categorical Field Revision cannot carry a vertical reference.");
        }
    }

    public static SpatialReferenceDefinitionRecord Validate(SpatialReferenceDefinitionRecord definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var normalized = NormalizeAndValidate(definition);
        var expected = ComputeFingerprint(normalized);
        if (!string.Equals(definition.DefinitionFingerprint, expected, StringComparison.Ordinal))
            throw new ArgumentException("Spatial Reference definition fingerprint does not match its immutable content.", nameof(definition));
        return normalized;
    }

    private static SpatialReferenceDefinitionRecord NormalizeAndValidate(SpatialReferenceDefinitionRecord value)
    {
        if (value.ProjectId == Guid.Empty)
            throw new ArgumentException("Spatial Reference Project ID must be a non-empty UUID.", nameof(value));
        RequireIdentifier(value.SpatialReferenceId, 512, "spatial_reference_id");
        if (value.DefinitionVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Spatial Reference definition version must be >= 1.");
        if (!Enum.IsDefined(value.Kind) || !Enum.IsDefined(value.AxisOrder) || !Enum.IsDefined(value.WrapBehavior))
            throw new ArgumentException("Spatial Reference kind, axis order or wrap behavior is unsupported.", nameof(value));
        RequireIdentifier(value.CoordinateUnit, 64, "coordinate_unit");
        if (value.Dimension is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(value), "Spatial Reference dimension must be 2 or 3.");
        if (value.CreatedAtUtcMs < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Spatial Reference creation time must be non-negative.");
        if (value.VerticalReferenceId is not null)
            RequireIdentifier(value.VerticalReferenceId, 512, "vertical_reference_id");

        var profile = value.PrecisionProfile ?? throw new ArgumentException("Coordinate precision profile is required.", nameof(value));
        RequireIdentifier(profile.ProfileId, 160, "coordinate_precision_profile_id");
        if (profile.Version < 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Coordinate precision profile version must be >= 1.");
        RequireIdentifier(profile.CoordinateUnit, 64, "precision_coordinate_unit");
        if (profile.AxisOrder != value.AxisOrder
            || !string.Equals(profile.CoordinateUnit, value.CoordinateUnit, StringComparison.Ordinal))
            throw new ArgumentException("Coordinate precision profile unit/axis must match its Spatial Reference.", nameof(value));
        RequireIdentifier(profile.NumericRepresentation, 64, "numeric_representation");
        if (!Enum.IsDefined(profile.RoundingMode))
            throw new ArgumentException("Coordinate precision rounding mode is unsupported.", nameof(value));
        if (profile.MaximumAbsoluteQuantizationError <= 0
            || profile.MinimumAuthoringTolerance <= 0
            || profile.SmallestMeaningfulFeatureSize <= 0)
            throw new ArgumentException("Coordinate precision profile values must be positive.", nameof(value));

        return value with
        {
            ValidDomainJson = CanonicalObject(value.ValidDomainJson, "valid_domain"),
            DefinitionParametersJson = CanonicalObject(value.DefinitionParametersJson, "definition_parameters"),
            MeasurementCapabilitiesJson = CanonicalObject(value.MeasurementCapabilitiesJson, "measurement_capabilities"),
            ProvenanceJson = CanonicalObject(value.ProvenanceJson, "provenance"),
        };
    }

    private static string CanonicalObject(string json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException($"{name} must be a JSON object.", name);
        var canonical = FieldManifestCanonicalizer.Canonicalize(json);
        using var document = JsonDocument.Parse(canonical);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{name} must be a JSON object.", name);
        return canonical;
    }

    private static void RequireIdentifier(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException($"{name} must be a non-empty, trimmed identifier of at most {maxLength} characters.", name);
    }

    private static string ComputeFingerprint(SpatialReferenceDefinitionRecord value)
    {
        var profile = value.PrecisionProfile;
        var payload = JsonSerializer.Serialize(new
        {
            project_id = value.ProjectId.ToString("D"),
            spatial_reference_id = value.SpatialReferenceId,
            definition_version = value.DefinitionVersion,
            kind = value.Kind.ToString(),
            coordinate_unit = value.CoordinateUnit,
            axis_order = value.AxisOrder.ToString(),
            dimension = value.Dimension,
            valid_domain = JsonDocument.Parse(value.ValidDomainJson).RootElement,
            wrap_behavior = value.WrapBehavior.ToString(),
            vertical_reference_id = value.VerticalReferenceId,
            precision_profile = new
            {
                profile_id = profile.ProfileId,
                version = profile.Version,
                coordinate_unit = profile.CoordinateUnit,
                axis_order = profile.AxisOrder.ToString(),
                numeric_representation = profile.NumericRepresentation,
                maximum_absolute_quantization_error = profile.MaximumAbsoluteQuantizationError,
                rounding_mode = profile.RoundingMode.ToString(),
                minimum_authoring_tolerance = profile.MinimumAuthoringTolerance,
                smallest_meaningful_feature_size = profile.SmallestMeaningfulFeatureSize,
            },
            definition_parameters = JsonDocument.Parse(value.DefinitionParametersJson).RootElement,
            measurement_capabilities = JsonDocument.Parse(value.MeasurementCapabilitiesJson).RootElement,
            provenance = JsonDocument.Parse(value.ProvenanceJson).RootElement,
        });
        var canonical = FieldManifestCanonicalizer.Canonicalize(payload);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
