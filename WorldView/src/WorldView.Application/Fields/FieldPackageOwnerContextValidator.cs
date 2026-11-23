using System;
using System.Text;
using System.Text.Json;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>
/// Stable machine-readable owner-context preflight failure codes. The WVPK_OWNER_* family
/// extends the ME-05-10 package error taxonomy for the Core Domain publication coordinator:
/// the binary reader stays format-only and never resolves these ownership relations itself.
/// </summary>
public static class FieldPackageOwnerContextErrorCodes
{
    /// <summary>The target Project does not exist in the opened project database.</summary>
    public const string ProjectMissing = "WVPK_OWNER_PROJECT_MISSING";
    /// <summary>The target Map does not exist or does not belong to the target Project.</summary>
    public const string MapMissing = "WVPK_OWNER_MAP_MISSING";
    /// <summary>The target Map row carries no stable Map Space identity (fail-closed data state).</summary>
    public const string MapSpaceUnset = "WVPK_OWNER_MAP_SPACE_UNSET";
    /// <summary>The target Map has not bound its initial immutable Spatial Reference yet.</summary>
    public const string SpatialReferenceUnbound = "WVPK_OWNER_SPATIAL_REFERENCE_UNBOUND";
    /// <summary>The Map's bound Spatial Reference definition row is absent in its Project (fail-closed).</summary>
    public const string SpatialReferenceMissing = "WVPK_OWNER_SPATIAL_REFERENCE_MISSING";
    /// <summary>Manifest bytes are not canonical UTF-8 JSON or fail semantic manifest validation.</summary>
    public const string ManifestInvalid = "WVPK_OWNER_MANIFEST_INVALID";
    /// <summary>Package references a Map Space other than the target Map's stable identity.</summary>
    public const string MapSpaceMismatch = "WVPK_OWNER_MAP_SPACE_MISMATCH";
    /// <summary>Package references a Spatial Reference other than the Map's active binding.</summary>
    public const string SpatialReferenceMismatch = "WVPK_OWNER_SPATIAL_REFERENCE_MISMATCH";
    /// <summary>Package snapshots a coordinate precision profile other than the bound definition's.</summary>
    public const string PrecisionProfileMismatch = "WVPK_OWNER_PRECISION_PROFILE_MISMATCH";
    /// <summary>Package vertical-reference identity does not match the bound definition's contract.</summary>
    public const string VerticalReferenceMismatch = "WVPK_OWNER_VERTICAL_REFERENCE_MISMATCH";
    /// <summary>The validator's explicit checks disagree with the publication authority (defect guard).</summary>
    public const string OwnerContextConflict = "WVPK_OWNER_CONTEXT_CONFLICT";
}

/// <summary>
/// Validated import target state: the typed semantic projection of a package manifest plus the
/// resolved owner rows it was validated against. Publication can proceed from this result
/// without re-resolving the Project / Map / Spatial Reference context.
/// </summary>
public sealed record FieldPackageOwnerContextValidation
{
    public required FieldRevisionMetadata Metadata { get; init; }
    public required MapRecord Map { get; init; }
    public required SpatialReferenceDefinitionRecord SpatialReference { get; init; }
}

/// <summary>
/// Core Domain owner-context preflight for importing an opened Field Package into a target
/// Project / Map. It validates the ownership relations between the package's declared context
/// (descriptor identity, Map Space, Spatial Reference, precision profile, vertical reference)
/// and the authoritative target rows, so invalid references can never reach formal domain
/// objects or publication. The binary reader remains format-only; all database resolution
/// happens here through the Core Domain ports.
/// </summary>
public sealed class FieldPackageOwnerContextValidator
{
    private readonly ICoreDomainDatabase _database;

    public FieldPackageOwnerContextValidator(ICoreDomainDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>
    /// Validates a package manifest's owner context against the target Project and Map.
    /// The manifest bytes must be the exact bytes returned by <c>FieldPackageReader.ManifestUtf8</c>
    /// (RFC 8785 canonical UTF-8); non-canonical bytes fail closed instead of being repaired.
    /// Every failure names the mismatched reference and both sides' values.
    /// </summary>
    public FieldPackageOwnerContextValidation Validate(
        ReadOnlyMemory<byte> manifestUtf8,
        Guid projectId,
        Guid mapId)
    {
        if (_database.FindProject(projectId) is null)
            throw Error(FieldPackageOwnerContextErrorCodes.ProjectMissing,
                $"Target Project {projectId:D} does not exist in the opened project database.");

        var map = _database.FindMap(projectId, mapId);
        if (map is null || map.ProjectId != projectId)
            throw Error(FieldPackageOwnerContextErrorCodes.MapMissing,
                $"Target Map {mapId:D} does not exist or does not belong to Project {projectId:D}.");
        // The explicit ProjectId comparison documents the cross-project rejection rule even
        // though the port contract already scopes FindMap by Project (and the V1 database
        // holds exactly one Project).

        var manifest = ReadCanonicalManifest(manifestUtf8);
        var (fieldKey, descriptorSchemaVersion, semanticsId) = ReadDescriptorIdentity(manifest);

        if (!FieldDescriptorRegistry.TryGetSemanticsId(fieldKey, descriptorSchemaVersion, out var registeredSemanticsId))
            throw Error(FieldPackageErrorCodes.DescriptorMismatch,
                $"Field descriptor {fieldKey}@{descriptorSchemaVersion} declared by the package is not registered in the Field Descriptor registry.");
        if (!string.Equals(semanticsId, registeredSemanticsId, StringComparison.Ordinal))
            throw Error(FieldPackageErrorCodes.DescriptorMismatch,
                $"Package field.semantics_id '{semanticsId}' does not match the registered semantics_id '{registeredSemanticsId}' for descriptor {fieldKey}@{descriptorSchemaVersion}.");

        // The full semantic manifest validation is deliberately the same authority the
        // publication path uses, so import preflight and publication cannot drift apart.
        FieldRevisionMetadata metadata;
        try
        {
            metadata = FieldDatasetService.ValidateManifest(manifest, fieldKey, descriptorSchemaVersion, semanticsId);
        }
        catch (Exception ex) when (ex is CoreDomainConflictException or CoreDomainDataException or JsonException)
        {
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                $"Field Package manifest failed semantic validation: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(map.MapSpaceId))
            throw Error(FieldPackageOwnerContextErrorCodes.MapSpaceUnset,
                $"Target Map {mapId:D} carries no stable Map Space identity; Field data cannot be imported.");
        if (!string.Equals(metadata.MapSpaceId, map.MapSpaceId, StringComparison.Ordinal))
            throw Error(FieldPackageOwnerContextErrorCodes.MapSpaceMismatch,
                $"Package spatial.map_space_id '{metadata.MapSpaceId}' does not match target Map {mapId:D} map_space_id '{map.MapSpaceId}'.");

        if (string.IsNullOrWhiteSpace(map.SpatialReferenceId))
            throw Error(FieldPackageOwnerContextErrorCodes.SpatialReferenceUnbound,
                $"Target Map {mapId:D} has no active Spatial Reference binding; Field data cannot be imported.");
        if (!string.Equals(metadata.SpatialReferenceId, map.SpatialReferenceId, StringComparison.Ordinal))
            throw Error(FieldPackageOwnerContextErrorCodes.SpatialReferenceMismatch,
                $"Package spatial.spatial_reference_id '{metadata.SpatialReferenceId}' does not match target Map {mapId:D} active spatial_reference_id '{map.SpatialReferenceId}'.");

        // FindDefinition revalidates the immutable fingerprint on read (fail-closed), matching
        // the exact resolution path publication uses.
        var spatialReference = SpatialReferenceService.FindDefinition(_database, projectId, map.SpatialReferenceId)
            ?? throw Error(FieldPackageOwnerContextErrorCodes.SpatialReferenceMissing,
                $"Spatial Reference '{map.SpatialReferenceId}' bound to Map {mapId:D} has no definition row in Project {projectId:D}.");

        if (!string.Equals(metadata.CoordinatePrecisionProfileId,
                spatialReference.PrecisionProfile.ProfileId, StringComparison.Ordinal))
            throw Error(FieldPackageOwnerContextErrorCodes.PrecisionProfileMismatch,
                $"Package spatial.coordinate_precision_profile_id '{metadata.CoordinatePrecisionProfileId}' does not match Spatial Reference '{spatialReference.SpatialReferenceId}' pinned profile '{spatialReference.PrecisionProfile.ProfileId}'.");

        ValidateVerticalReference(metadata, spatialReference);

        // Backstop: the publication authority must accept exactly what this preflight accepts.
        // A throw here can only be a validator defect, never tolerated package input.
        try
        {
            SpatialReferenceRules.ValidateFieldOwnerContext(map, spatialReference, metadata);
        }
        catch (CoreDomainConflictException ex)
        {
            throw Error(FieldPackageOwnerContextErrorCodes.OwnerContextConflict,
                $"Owner-context preflight disagrees with the publication authority: {ex.Message}", ex);
        }

        return new FieldPackageOwnerContextValidation
        {
            Metadata = metadata,
            Map = map,
            SpatialReference = spatialReference,
        };
    }

    private static void ValidateVerticalReference(
        FieldRevisionMetadata metadata,
        SpatialReferenceDefinitionRecord spatialReference)
    {
        if (metadata.FieldKey == "elevation")
        {
            if (spatialReference.VerticalReferenceId is null
                || !string.Equals(metadata.VerticalReferenceId, spatialReference.VerticalReferenceId, StringComparison.Ordinal))
                throw Error(FieldPackageOwnerContextErrorCodes.VerticalReferenceMismatch,
                    $"Package spatial.vertical_reference_id '{metadata.VerticalReferenceId}' does not match Spatial Reference '{spatialReference.SpatialReferenceId}' vertical_reference_id '{spatialReference.VerticalReferenceId}'.");
            return;
        }

        if (metadata.VerticalReferenceId is not null)
            throw Error(FieldPackageOwnerContextErrorCodes.VerticalReferenceMismatch,
                $"Categorical field '{metadata.FieldKey}' must not declare spatial.vertical_reference_id '{metadata.VerticalReferenceId}'.");
    }

    private static string ReadCanonicalManifest(ReadOnlyMemory<byte> manifestUtf8)
    {
        if (manifestUtf8.IsEmpty)
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest is empty.");
        try
        {
            _ = FieldManifestCanonicalizer.RequireCanonicalUtf8(manifestUtf8.Span);
        }
        catch (CoreDomainDataException ex)
        {
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest bytes are not byte-identical RFC 8785 canonical UTF-8.", ex);
        }

        return Encoding.UTF8.GetString(manifestUtf8.Span);
    }

    private static (string FieldKey, int SchemaVersion, string SemanticsId) ReadDescriptorIdentity(string manifest)
    {
        JsonElement field;
        try
        {
            using var document = JsonDocument.Parse(manifest);
            field = document.RootElement.Clone();
            if (field.ValueKind != JsonValueKind.Object || !field.TryGetProperty("field", out field))
                throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                    "Field Package manifest does not declare a field descriptor identity object.");
        }
        catch (JsonException ex)
        {
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest could not be parsed as JSON.", ex);
        }

        if (field.ValueKind != JsonValueKind.Object
            || !field.TryGetProperty("field_key", out var fieldKeyElement)
            || fieldKeyElement.ValueKind != JsonValueKind.String
            || fieldKeyElement.GetString() is not { Length: > 0 } fieldKey)
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest field.field_key is missing or not a non-empty string.");
        if (!field.TryGetProperty("schema_version", out var schemaVersionElement)
            || schemaVersionElement.ValueKind != JsonValueKind.Number
            || !schemaVersionElement.TryGetInt32(out var schemaVersion))
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest field.schema_version is missing or not an integer.");
        if (!field.TryGetProperty("semantics_id", out var semanticsIdElement)
            || semanticsIdElement.ValueKind != JsonValueKind.String
            || semanticsIdElement.GetString() is not { Length: > 0 } semanticsId)
            throw Error(FieldPackageOwnerContextErrorCodes.ManifestInvalid,
                "Field Package manifest field.semantics_id is missing or not a non-empty string.");

        return (fieldKey, schemaVersion, semanticsId);
    }

    private static FieldPackageValidationException Error(string code, string message, Exception? inner = null)
        => new(code, message, inner);
}
