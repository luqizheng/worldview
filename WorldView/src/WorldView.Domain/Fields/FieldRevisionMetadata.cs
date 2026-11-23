using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace WorldView.Domain.Fields;

/// <summary>
/// Typed immutable Field Revision semantic metadata.
/// It is built from a validated manifest on publication and persisted as relational scalar/child rows;
/// ManifestJson remains an interchange/diagnostic copy, not the source for typed reads.
/// </summary>
public sealed record FieldRevisionMetadata
{
    public required string FieldKey { get; init; }
    public required int DescriptorSchemaVersion { get; init; }
    public required string SemanticsId { get; init; }
    public string? Unit { get; init; }
    public required string MapSpaceId { get; init; }
    public required string SpatialReferenceId { get; init; }
    public required string CoordinatePrecisionProfileId { get; init; }
    public string? VerticalReferenceId { get; init; }
    public required FieldGridMetadata Grid { get; init; }
    public required FieldCoverageMetadata Coverage { get; init; }
    public required string MapEdgeSemantics { get; init; }
    public required FieldNoDataMetadata NoData { get; init; }
    public required string ValueProfileId { get; init; }
    public required string ValueProfileScope { get; init; }
    public required IReadOnlyList<FieldValuePlaneMetadata> Planes { get; init; }
    public IReadOnlyDictionary<string, int> CategoryCodes { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);
    public required IReadOnlyList<FieldCodecMetadata> Codecs { get; init; }
    public required string TileHashAlgorithm { get; init; }
    public required int TileHashVersion { get; init; }

    /// <summary>
    /// Structural equality of the typed projection. The record's default value equality is
    /// not sufficient here because Planes / CategoryCodes / Codecs are collection members
    /// compared by reference; this method compares every semantic member element-wise with
    /// ordinal string semantics, exactly the contract the persistence layer enforces between
    /// a canonical manifest and the typed columns at publication.
    /// </summary>
    public bool StructurallyEquals(FieldRevisionMetadata? other)
    {
        if (other is null)
            return false;
        return string.Equals(FieldKey, other.FieldKey, StringComparison.Ordinal)
            && DescriptorSchemaVersion == other.DescriptorSchemaVersion
            && string.Equals(SemanticsId, other.SemanticsId, StringComparison.Ordinal)
            && string.Equals(Unit, other.Unit, StringComparison.Ordinal)
            && string.Equals(MapSpaceId, other.MapSpaceId, StringComparison.Ordinal)
            && string.Equals(SpatialReferenceId, other.SpatialReferenceId, StringComparison.Ordinal)
            && string.Equals(CoordinatePrecisionProfileId, other.CoordinatePrecisionProfileId, StringComparison.Ordinal)
            && string.Equals(VerticalReferenceId, other.VerticalReferenceId, StringComparison.Ordinal)
            && Grid == other.Grid
            && Coverage == other.Coverage
            && string.Equals(MapEdgeSemantics, other.MapEdgeSemantics, StringComparison.Ordinal)
            && NoData == other.NoData
            && string.Equals(ValueProfileId, other.ValueProfileId, StringComparison.Ordinal)
            && string.Equals(ValueProfileScope, other.ValueProfileScope, StringComparison.Ordinal)
            && Planes.SequenceEqual(other.Planes)
            && CategoryCodes.Count == other.CategoryCodes.Count
            && CategoryCodes.All(pair =>
                other.CategoryCodes.TryGetValue(pair.Key, out var value) && value == pair.Value)
            && Codecs.SequenceEqual(other.Codecs)
            && string.Equals(TileHashAlgorithm, other.TileHashAlgorithm, StringComparison.Ordinal)
            && TileHashVersion == other.TileHashVersion;
    }

    /// <summary>Builds the typed projection after FieldDatasetService has validated the manifest.</summary>
    public static FieldRevisionMetadata FromValidatedManifest(string canonicalManifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalManifest);
        using var document = JsonDocument.Parse(canonicalManifest);
        var root = document.RootElement;
        var field = root.GetProperty("field");
        var spatial = root.GetProperty("spatial");
        var grid = root.GetProperty("grid");
        var coverage = root.GetProperty("coverage");
        var noData = root.GetProperty("no_data");
        var mask = noData.GetProperty("mask_plane");
        var encoding = root.GetProperty("value_encoding");
        var tileHash = root.GetProperty("tile_hash");

        var planes = encoding.GetProperty("planes").EnumerateArray().Select(plane =>
            new FieldValuePlaneMetadata(
                plane.GetProperty("name").GetString()!,
                plane.GetProperty("sample_format").GetString()!,
                plane.GetProperty("bytes_per_cell").GetInt32())).ToArray();

        var codecs = root.GetProperty("codec_registry").EnumerateArray().Select(codec =>
            new FieldCodecMetadata(
                codec.GetProperty("codec_id").GetInt32(),
                codec.GetProperty("codec_name").GetString()!,
                codec.GetProperty("version").GetString()!,
                CanonicalizeJson(codec.GetProperty("parameters")))).ToArray();

        var categoryCodes = new Dictionary<string, int>(StringComparer.Ordinal);
        if (encoding.TryGetProperty("category_codes", out var categories))
        {
            foreach (var property in categories.EnumerateObject())
                categoryCodes.Add(property.Name, property.Value.GetInt32());
        }

        return new FieldRevisionMetadata
        {
            FieldKey = field.GetProperty("field_key").GetString()!,
            DescriptorSchemaVersion = field.GetProperty("schema_version").GetInt32(),
            SemanticsId = field.GetProperty("semantics_id").GetString()!,
            Unit = field.TryGetProperty("unit", out var unit) ? unit.GetString() : null,
            MapSpaceId = spatial.GetProperty("map_space_id").GetString()!,
            SpatialReferenceId = spatial.GetProperty("spatial_reference_id").GetString()!,
            CoordinatePrecisionProfileId = spatial.GetProperty("coordinate_precision_profile_id").GetString()!,
            VerticalReferenceId = spatial.TryGetProperty("vertical_reference_id", out var verticalReference)
                ? verticalReference.GetString()
                : null,
            Grid = new FieldGridMetadata(
                grid.GetProperty("width").GetInt64(),
                grid.GetProperty("height").GetInt64(),
                grid.GetProperty("tile_width").GetInt32(),
                grid.GetProperty("tile_height").GetInt32(),
                grid.GetProperty("origin_x").GetString()!,
                grid.GetProperty("origin_y").GetString()!,
                grid.GetProperty("spacing_x").GetString()!,
                grid.GetProperty("spacing_y").GetString()!,
                grid.GetProperty("axis_orientation").GetString()!,
                grid.GetProperty("cell_registration").GetString()!),
            Coverage = new FieldCoverageMetadata(
                coverage.GetProperty("mode").GetString()!,
                coverage.TryGetProperty("mask_plane_name", out var coverageMask)
                    ? coverageMask.GetString()
                    : null),
            MapEdgeSemantics = root.GetProperty("map_edge_semantics").GetString()!,
            NoData = new FieldNoDataMetadata(
                noData.GetProperty("policy").GetString()!,
                mask.GetProperty("plane_name").GetString()!,
                mask.GetProperty("valid_value").GetInt32(),
                mask.GetProperty("invalid_value").GetInt32()),
            ValueProfileId = encoding.GetProperty("profile_id").GetString()!,
            ValueProfileScope = encoding.GetProperty("profile_scope").GetString()!,
            Planes = Array.AsReadOnly(planes),
            CategoryCodes = new ReadOnlyDictionary<string, int>(categoryCodes),
            Codecs = Array.AsReadOnly(codecs),
            TileHashAlgorithm = tileHash.GetProperty("algorithm").GetString()!,
            TileHashVersion = tileHash.GetProperty("version").GetInt32(),
        };
    }

    private static string CanonicalizeJson(JsonElement value)
    {
        // Codec parameters are retained as a small canonical JSON extension point; all core
        // Field semantics around them remain typed. The stored form is the exact RFC 8785
        // canonical bytes from the validated manifest, so typed rows and ManifestJson never
        // disagree on spelling (System.Text.Json would otherwise re-escape strings and the
        // legacy CanonicalJson would rewrite exponent-form numbers such as 1e-7 as 1E-07).
        return FieldManifestCanonicalizer.Canonicalize(value.GetRawText());
    }
}

public sealed record FieldGridMetadata(
    long Width,
    long Height,
    int TileWidth,
    int TileHeight,
    string OriginX,
    string OriginY,
    string SpacingX,
    string SpacingY,
    string AxisOrientation,
    string CellRegistration);

public sealed record FieldCoverageMetadata(string Mode, string? MaskPlaneName);

public sealed record FieldNoDataMetadata(
    string Policy,
    string MaskPlaneName,
    int ValidValue,
    int InvalidValue);

public sealed record FieldValuePlaneMetadata(string Name, string SampleFormat, int BytesPerCell);

public sealed record FieldCodecMetadata(int CodecId, string CodecName, string Version, string ParametersJson);
