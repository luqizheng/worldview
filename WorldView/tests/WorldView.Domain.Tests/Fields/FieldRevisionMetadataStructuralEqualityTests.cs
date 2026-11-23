using System.Collections.ObjectModel;
using WorldView.Domain.Fields;
using Xunit;

namespace WorldView.Domain.Tests.Fields;

/// <summary>
/// Structural equality of the typed Field Revision projection: the single contract used both
/// by the persistence layer (manifest vs typed columns at publication) and by the export path
/// (persisted manifest vs typed columns after publication). Collection members compare
/// element-wise — the record's reference-based collection equality must never leak through.
/// </summary>
public sealed class FieldRevisionMetadataStructuralEqualityTests
{
    private static FieldRevisionMetadata Sample() =>
        FieldRevisionMetadata.FromValidatedManifest(
            FieldManifestCanonicalizer.Canonicalize(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    byte_order = "little_endian",
                    canonicalization = "RFC8785-JCS-UTF8",
                    codec_registry = new object[]
                    {
                        new { codec_id = 0, codec_name = "none", parameters = new { }, version = "1" },
                        new { codec_id = 1, codec_name = "test", parameters = new { quantum = 1e-7 }, version = "2" },
                    },
                    coverage = new { mode = "full_grid" },
                    field = new { field_key = "elevation", schema_version = 1, semantics_id = "world-view.field.elevation.v1", unit = "m" },
                    format = new { major = 1, minor = 0 },
                    grid = new
                    {
                        axis_orientation = "x_east_y_north",
                        cell_registration = "area",
                        height = 2,
                        origin_x = "0",
                        origin_y = "0",
                        spacing_x = "1",
                        spacing_y = "1",
                        tile_height = 2,
                        tile_width = 2,
                        width = 2,
                    },
                    manifest_schema = new { id = "world-view.field-package-manifest", version = 1 },
                    map_edge_semantics = "bounded_non_wrapping",
                    no_data = new
                    {
                        mask_plane = new { invalid_value = 1, plane_name = "validity_mask", valid_value = 0 },
                        policy = "explicit_validity_mask",
                    },
                    spatial = new
                    {
                        coordinate_precision_profile_id = "fixture:coordinate-precision-v1",
                        map_space_id = "fixture-map-space-001",
                        spatial_reference_id = "fixture:local-cartesian-v1",
                        vertical_reference_id = "fixture:vertical-reference-local-v1",
                    },
                    tile_hash = new { algorithm = "SHA-256", version = 1 },
                    value_encoding = new
                    {
                        planes = new object[]
                        {
                            new { name = "samples", sample_format = "float32_ieee754_le", bytes_per_cell = 4 },
                            new { name = "validity_mask", sample_format = "uint8", bytes_per_cell = 1 },
                        },
                        profile_id = "fixture-only:elevation-f32-mask-v1",
                        profile_scope = "golden_fixture_only",
                    },
                })));

    [Fact]
    public void IndependentlyBuiltProjectionsOfSameManifestAreStructurallyEqual()
    {
        Assert.True(Sample().StructurallyEquals(Sample()));
        Assert.True(Sample().StructurallyEquals(Sample() with { }));
    }

    [Fact]
    public void NullAndCrossManifestProjectionsAreNotEqual()
    {
        Assert.False(Sample().StructurallyEquals(null));

        var other = FieldRevisionMetadata.FromValidatedManifest(
            FieldManifestCanonicalizer.Canonicalize(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    byte_order = "little_endian",
                    canonicalization = "RFC8785-JCS-UTF8",
                    codec_registry = new object[] { new { codec_id = 0, codec_name = "none", parameters = new { }, version = "1" } },
                    coverage = new { mode = "full_grid" },
                    field = new { field_key = "land_water", schema_version = 1, semantics_id = "world-view.field.land_water.v1" },
                    format = new { major = 1, minor = 0 },
                    grid = new
                    {
                        axis_orientation = "x_east_y_north",
                        cell_registration = "area",
                        height = 2,
                        origin_x = "0",
                        origin_y = "0",
                        spacing_x = "1",
                        spacing_y = "1",
                        tile_height = 2,
                        tile_width = 2,
                        width = 2,
                    },
                    manifest_schema = new { id = "world-view.field-package-manifest", version = 1 },
                    map_edge_semantics = "bounded_non_wrapping",
                    no_data = new
                    {
                        mask_plane = new { invalid_value = 1, plane_name = "validity_mask", valid_value = 0 },
                        policy = "explicit_validity_mask",
                    },
                    spatial = new
                    {
                        coordinate_precision_profile_id = "fixture:coordinate-precision-v1",
                        map_space_id = "fixture-map-space-001",
                        spatial_reference_id = "fixture:local-cartesian-v1",
                    },
                    tile_hash = new { algorithm = "SHA-256", version = 1 },
                    value_encoding = new
                    {
                        category_codes = new { land = 0, outside_coverage = 3, unknown = 2, water = 1 },
                        planes = new object[]
                        {
                            new { name = "categories", sample_format = "uint8_registered_category", bytes_per_cell = 1 },
                            new { name = "validity_mask", sample_format = "uint8", bytes_per_cell = 1 },
                        },
                        profile_id = "fixture-only:land-water-u8-mask-v1",
                        profile_scope = "golden_fixture_only",
                    },
                })));

        Assert.False(Sample().StructurallyEquals(other));
        Assert.False(other.StructurallyEquals(Sample()));
    }

    [Fact]
    public void EverySemanticMemberParticipatesInEquality()
    {
        var sample = Sample();

        Assert.False(sample.StructurallyEquals(sample with { FieldKey = "land_water" }));
        Assert.False(sample.StructurallyEquals(sample with { DescriptorSchemaVersion = 2 }));
        Assert.False(sample.StructurallyEquals(sample with { SemanticsId = "world-view.field.elevation.v2" }));
        Assert.False(sample.StructurallyEquals(sample with { Unit = "ft" }));
        Assert.False(sample.StructurallyEquals(sample with { MapSpaceId = "other" }));
        Assert.False(sample.StructurallyEquals(sample with { SpatialReferenceId = "other" }));
        Assert.False(sample.StructurallyEquals(sample with { CoordinatePrecisionProfileId = "other" }));
        Assert.False(sample.StructurallyEquals(sample with { VerticalReferenceId = "other" }));
        Assert.False(sample.StructurallyEquals(sample with
        {
            Grid = sample.Grid with { OriginX = "7" },
        }));
        Assert.False(sample.StructurallyEquals(sample with { Coverage = sample.Coverage with { Mode = "explicit_mask" } }));
        Assert.False(sample.StructurallyEquals(sample with { MapEdgeSemantics = "wrap_x" }));
        Assert.False(sample.StructurallyEquals(sample with
        {
            NoData = sample.NoData with { ValidValue = 2 },
        }));
        Assert.False(sample.StructurallyEquals(sample with { ValueProfileId = "other" }));
        Assert.False(sample.StructurallyEquals(sample with { ValueProfileScope = "production" }));
        Assert.False(sample.StructurallyEquals(sample with { TileHashAlgorithm = "SHA-512" }));
        Assert.False(sample.StructurallyEquals(sample with { TileHashVersion = 2 }));
    }

    [Fact]
    public void CollectionMembersCompareElementWiseNotByReference()
    {
        var sample = Sample();

        // Same elements in a fresh collection instance: structurally equal despite different
        // collection references (record equality alone would compare references).
        Assert.True(sample.StructurallyEquals(sample with
        {
            Planes = new ReadOnlyCollection<FieldValuePlaneMetadata>(new[]
            {
                sample.Planes[0], sample.Planes[1],
            }),
        }));

        // Reordered planes: payload layout order is semantic, so inequality.
        Assert.False(sample.StructurallyEquals(sample with
        {
            Planes = new ReadOnlyCollection<FieldValuePlaneMetadata>(new[]
            {
                sample.Planes[1], sample.Planes[0],
            }),
        }));

        // One codec parameter byte changed: inequality (the codec parameters are compared as
        // canonical bytes, so any re-spelling disagrees).
        Assert.False(sample.StructurallyEquals(sample with
        {
            Codecs = new ReadOnlyCollection<FieldCodecMetadata>(new[]
            {
                sample.Codecs[0],
                sample.Codecs[1] with { ParametersJson = "{\"quantum\":2e-7}" },
            }),
        }));
    }
}
