using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Abstractions;
using WorldView.Application.Fields;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// Real SQLite proof for the Field Package owner-context import preflight: descriptor
/// registry identity, Map Space, Spatial Reference binding, precision profile and vertical
/// reference must all match the authoritative target rows before any import proceeds.
/// </summary>
public sealed class FieldPackageOwnerContextValidationTests : IDisposable
{
    private const long Now = 3_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabase _database;

    public FieldPackageOwnerContextValidationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-owner-ctx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)new SqlitePackageDatabaseFactory()
            .CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        new ApplicationBootstrapper().InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private FieldPackageOwnerContextValidator Validator() => new(Core());

    private (ProjectRecord Project, Guid MapId) SeedMap(bool bindSpatialReference = true)
    {
        var core = Core();
        var project = ProjectRules.CreateProject("Owner context test", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var mapPlan = MapRules.CreateMap(project, "Map A", Now + 1);
        mapPlan = mapPlan with { Map = mapPlan.Map with { MapSpaceId = "fixture-map-space-001" } };
        core.CreateMapStructure(mapPlan);
        _ = SpatialReferenceService.RegisterDefinition(core, new SpatialReferenceDefinitionRecord
        {
            ProjectId = project.ProjectId,
            SpatialReferenceId = "fixture:local-cartesian-v1",
            DefinitionVersion = 1,
            Kind = SpatialReferenceKind.LocalCartesian,
            CoordinateUnit = "m",
            AxisOrder = SpatialAxisOrder.XY,
            Dimension = 2,
            ValidDomainJson = """{"max_x":"1000000","max_y":"1000000","min_x":"-1000000","min_y":"-1000000"}""",
            WrapBehavior = SpatialWrapBehavior.None,
            VerticalReferenceId = "fixture:vertical-reference-local-v1",
            PrecisionProfile = new CoordinatePrecisionProfileRecord
            {
                ProfileId = "fixture:coordinate-precision-v1",
                Version = 1,
                CoordinateUnit = "m",
                AxisOrder = SpatialAxisOrder.XY,
                NumericRepresentation = "decimal",
                MaximumAbsoluteQuantizationError = 0.000001m,
                RoundingMode = MidpointRounding.ToEven,
                MinimumAuthoringTolerance = 0.001m,
                SmallestMeaningfulFeatureSize = 0.01m,
            },
            DefinitionParametersJson = """{"origin_x":"0","origin_y":"0"}""",
            MeasurementCapabilitiesJson = """{"area":true,"distance":true}""",
            ProvenanceJson = """{"source":"test-fixture"}""",
            DefinitionFingerprint = "",
            CreatedAtUtcMs = Now + 1,
        });
        if (bindSpatialReference)
            _ = MapSpatialReferenceService.BindInitial(
                core, project.ProjectId, mapPlan.Map.MapId, "fixture:local-cartesian-v1", Now + 2);
        return (project, mapPlan.Map.MapId);
    }

    private static byte[] ManifestBytes(string manifest) => Encoding.UTF8.GetBytes(manifest);

    private static string BuildManifest(
        string fieldKey = "elevation",
        string semanticsId = "world-view.field.elevation.v1",
        string mapSpaceId = "fixture-map-space-001",
        string spatialReferenceId = "fixture:local-cartesian-v1",
        string precisionProfileId = "fixture:coordinate-precision-v1",
        bool includeVerticalReference = true,
        string verticalReferenceId = "fixture:vertical-reference-local-v1")
    {
        var isLandWater = fieldKey == "land_water";
        object[] planes = isLandWater
            ? new object[]
            {
                new { name = "categories", sample_format = "uint8_registered_category", bytes_per_cell = 1 },
                new { name = "validity_mask", sample_format = "uint8", bytes_per_cell = 1 },
            }
            : new object[]
            {
                new { name = "samples", sample_format = "float32_ieee754_le", bytes_per_cell = 4 },
                new { name = "validity_mask", sample_format = "uint8", bytes_per_cell = 1 },
            };
        object field = isLandWater
            ? new { field_key = fieldKey, schema_version = 1, semantics_id = semanticsId }
            : new { field_key = fieldKey, schema_version = 1, semantics_id = semanticsId, unit = "m" };

        var spatial = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["map_space_id"] = mapSpaceId,
            ["spatial_reference_id"] = spatialReferenceId,
            ["coordinate_precision_profile_id"] = precisionProfileId,
        };
        if (includeVerticalReference)
            spatial["vertical_reference_id"] = verticalReferenceId;

        object valueEncoding = isLandWater
            ? new
            {
                profile_id = "fixture-only:land-water-u8-mask-v1",
                profile_scope = "golden_fixture_only",
                planes,
                category_codes = new { land = 0, water = 1, unknown = 2, outside_coverage = 3 },
            }
            : new
            {
                profile_id = "fixture-only:elevation-f32-mask-v1",
                profile_scope = "golden_fixture_only",
                planes,
            };

        return FieldManifestCanonicalizer.Canonicalize(System.Text.Json.JsonSerializer.Serialize(new
        {
            byte_order = "little_endian",
            canonicalization = "RFC8785-JCS-UTF8",
            codec_registry = new[] { new { codec_id = 0, codec_name = "none", parameters = new { }, version = "1" } },
            coverage = new { mode = "full_grid" },
            field,
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
            spatial,
            tile_hash = new { algorithm = "SHA-256", version = 1 },
            value_encoding = valueEncoding,
        }));
    }

    [Fact]
    public void Validate_AcceptsManifestMatchingTargetOwnerContext()
    {
        var (project, mapId) = SeedMap();
        var result = Validator().Validate(ManifestBytes(BuildManifest()), project.ProjectId, mapId);

        Assert.Equal("elevation", result.Metadata.FieldKey);
        Assert.Equal(1, result.Metadata.DescriptorSchemaVersion);
        Assert.Equal("world-view.field.elevation.v1", result.Metadata.SemanticsId);
        Assert.Equal("fixture-map-space-001", result.Metadata.MapSpaceId);
        Assert.Equal("fixture:local-cartesian-v1", result.Metadata.SpatialReferenceId);
        Assert.Equal("fixture:coordinate-precision-v1", result.Metadata.CoordinatePrecisionProfileId);
        Assert.Equal("fixture:vertical-reference-local-v1", result.Metadata.VerticalReferenceId);
        Assert.Equal(mapId, result.Map.MapId);
        Assert.Equal("fixture:local-cartesian-v1", result.SpatialReference.SpatialReferenceId);
        Assert.Equal(2, result.Metadata.Planes.Count);
    }

    [Fact]
    public void Validate_AcceptsCanonicalGoldenFixtureManifest()
    {
        // The canonical golden manifest declares exactly the owner context seeded above.
        var (project, mapId) = SeedMap();
        var goldenManifest = ReadGoldenManifest("minimal-elevation-2x2.manifest.json");

        var result = Validator().Validate(goldenManifest, project.ProjectId, mapId);

        Assert.Equal("elevation", result.Metadata.FieldKey);
        Assert.Equal("fixture-map-space-001", result.Metadata.MapSpaceId);
        Assert.Equal("golden_fixture_only", result.Metadata.ValueProfileScope);
    }

    [Fact]
    public void Validate_RejectsNonCanonicalManifestBytes()
    {
        var (project, mapId) = SeedMap();
        // Same JSON content but with a trailing newline: valid JSON, not byte-identical JCS.
        var bytes = ManifestBytes(BuildManifest() + "\n");

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(bytes, project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.ManifestInvalid, error.Code);
    }

    [Fact]
    public void Validate_RejectsUnknownTargetProject()
    {
        var (_, mapId) = SeedMap();
        var unknownProjectId = Guid.NewGuid();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(ManifestBytes(BuildManifest()), unknownProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.ProjectMissing, error.Code);
    }

    [Fact]
    public void Validate_RejectsUnknownTargetMap()
    {
        var (project, _) = SeedMap();
        var unknownMapId = Guid.NewGuid();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(ManifestBytes(BuildManifest()), project.ProjectId, unknownMapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.MapMissing, error.Code);
    }

    // 跨项目 Map 引用在 V1 数据库中结构性不可达（一库一项目，InsertProject 拒绝第二个
    // Project 行），且端口契约已按 Project 范围隔离 FindMap；校验器内的显式 ProjectId
    // 比对与 unknown-map 路径走同一拒绝分支（WVPK_OWNER_MAP_MISSING）。

    [Fact]
    public void Validate_RejectsUnregisteredDescriptor()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(fieldKey: "humidity", semanticsId: "world-view.field.humidity.v1")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageErrorCodes.DescriptorMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsDescriptorSemanticsMismatch()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(semanticsId: "world-view.field.elevation.v2")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageErrorCodes.DescriptorMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsSemanticallyInvalidManifest()
    {
        var (project, mapId) = SeedMap();

        // grid.height 0 violates the semantic manifest contract (positive safe integers).
        var invalid = BuildManifest()
            .Replace("\"height\":2", "\"height\":0", StringComparison.Ordinal);
        Assert.True(FieldManifestCanonicalizer.RequireCanonicalUtf8(ManifestBytes(invalid)).Length > 0);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(ManifestBytes(invalid), project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.ManifestInvalid, error.Code);
    }

    [Fact]
    public void Validate_RejectsMapSpaceMismatch()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(mapSpaceId: "fixture-map-space-999")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.MapSpaceMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsTargetMapWithDifferentInitialSpatialReference()
    {
        var (project, mapId) = SeedMap(bindSpatialReference: false);

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(ManifestBytes(BuildManifest()), project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.SpatialReferenceUnbound, error.Code);
    }

    [Fact]
    public void Validate_RejectsSpatialReferenceMismatch()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(spatialReferenceId: "fixture:other-reference-v1")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.SpatialReferenceMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsPrecisionProfileMismatch()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(precisionProfileId: "fixture:coordinate-precision-v2")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.PrecisionProfileMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsElevationVerticalReferenceMismatch()
    {
        var (project, mapId) = SeedMap();

        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(verticalReferenceId: "fixture:vertical-reference-other-v1")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.VerticalReferenceMismatch, error.Code);
    }

    [Fact]
    public void Validate_RejectsCategoricalManifestCarryingVerticalReference()
    {
        var (project, mapId) = SeedMap();

        // The semantic manifest authority rejects the extra spatial property before the
        // owner-context comparison runs; publication would reject it identically.
        var error = Assert.Throws<FieldPackageValidationException>(() =>
            Validator().Validate(
                ManifestBytes(BuildManifest(fieldKey: "land_water", semanticsId: "world-view.field.land_water.v1")),
                project.ProjectId, mapId));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.ManifestInvalid, error.Code);
    }

    [Fact]
    public void Validate_AcceptsCategoricalManifestMatchingTargetOwnerContext()
    {
        var (project, mapId) = SeedMap();

        var result = Validator().Validate(
            ManifestBytes(BuildLandWaterManifestWithoutVerticalReference()),
            project.ProjectId, mapId);

        Assert.Equal("land_water", result.Metadata.FieldKey);
        Assert.Null(result.Metadata.VerticalReferenceId);
        Assert.Equal(4, result.Metadata.CategoryCodes.Count);
    }

    [Fact]
    public void Validate_PerformsNoWrites()
    {
        var (project, mapId) = SeedMap();
        var validator = Validator();

        _ = validator.Validate(ManifestBytes(BuildManifest()), project.ProjectId, mapId);
        _ = Assert.Throws<FieldPackageValidationException>(() =>
            validator.Validate(
                ManifestBytes(BuildManifest(mapSpaceId: "fixture-map-space-999")),
                project.ProjectId, mapId));

        // Rejected preflights leave no Field Dataset rows and no payload references.
        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
    }

    private static string BuildLandWaterManifestWithoutVerticalReference() =>
        BuildManifest(
            fieldKey: "land_water",
            semanticsId: "world-view.field.land_water.v1",
            includeVerticalReference: false);

    private static byte[] ReadGoldenManifest(string suffix)
    {
        var assembly = typeof(FieldPackageOwnerContextValidationTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Missing embedded golden manifest resource '{suffix}'.");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
