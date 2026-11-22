using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Fields;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// Real SQLite proof for Map-owned Field Dataset publication: revision lineage, immutable
/// published rows, current-pointer CAS, dedicated payload ownership, and transaction rollback.
/// </summary>
public sealed class Cd02FieldDatasetPersistenceTests : IDisposable
{
    private const long Now = 2_000L;
    private const string Fingerprint1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Fingerprint2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly string _root;
    private readonly SqlitePackageDatabase _database;

    public Cd02FieldDatasetPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-field-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)new SqlitePackageDatabaseFactory()
            .CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        new ApplicationBootstrapper().InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _ = new SqliteCoreDomainDatabase(_database);
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private SqliteConnectionAccess Access() => ((ISqliteConnectionProvider)_database).GetAccess();

    private (ProjectRecord Project, Guid MapId) SeedMap()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("Field test", Now);
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
        _ = MapSpatialReferenceService.BindInitial(
            core, project.ProjectId, mapPlan.Map.MapId, "fixture:local-cartesian-v1", Now + 2);
        return (project, mapPlan.Map.MapId);
    }

    private static string BuildManifest(string fieldKey, string semanticsId, string originX = "0")
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
        object spatial = isLandWater
            ? new { map_space_id = "fixture-map-space-001", spatial_reference_id = "fixture:local-cartesian-v1",
                coordinate_precision_profile_id = "fixture:coordinate-precision-v1" }
            : new { map_space_id = "fixture-map-space-001", spatial_reference_id = "fixture:local-cartesian-v1",
                coordinate_precision_profile_id = "fixture:coordinate-precision-v1",
                vertical_reference_id = "fixture:vertical-reference-local-v1" };
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
                origin_x = originX,
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

    private static PublishFieldRevisionPlan Plan(
        Guid projectId,
        Guid mapId,
        Guid datasetId,
        Guid revisionId,
        Guid? expectedRevision,
        string fingerprint,
        long now) => new()
    {
        ProjectId = projectId,
        MapId = mapId,
        FieldDatasetId = datasetId,
        FieldKey = "elevation",
        DescriptorSchemaVersion = 1,
        SemanticsId = "world-view.field.elevation.v1",
        FieldRevisionId = revisionId,
        Payload = CreatePayloadPlan(projectId, datasetId, revisionId, now),
        PayloadBytes = new byte[] { 0x57, 0x56, 0x46, 0x50 },
        ExpectedCurrentFieldRevisionId = expectedRevision,
        ContentFingerprint = fingerprint,
        ManifestJson = BuildManifest("elevation", "world-view.field.elevation.v1"),
        Metadata = FieldRevisionMetadata.FromValidatedManifest(
            BuildManifest("elevation", "world-view.field.elevation.v1")),
        ProvenanceJson = """{"source":"test-fixture"}""",
        CreatedAtUtcMs = now,
    };

    private static RegisterFieldPayloadPlan CreatePayloadPlan(
        Guid projectId, Guid datasetId, Guid revisionId, long now)
    {
        var bytes = new byte[] { 0x57, 0x56, 0x46, 0x50 };
        return new RegisterFieldPayloadPlan
        {
            FieldPayloadId = Guid.NewGuid(),
            ProjectId = projectId,
            FieldDatasetId = datasetId,
            FieldRevisionId = revisionId,
            StorageKind = FieldPayloadStorageKind.SqliteBlob,
            PayloadFormat = "application/vnd.worldview.field-payload",
            FormatVersion = "1",
            HashAlgorithm = "sha-256",
            ByteLength = bytes.LongLength,
            ContentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            CreatedAtUtcMs = now,
        };
    }

    [Fact]
    public void SpatialReferenceDefinition_PersistsTypedProfileAndMapBinding()
    {
        var (project, mapId) = SeedMap();
        var core = Core();

        var map = core.FindMap(project.ProjectId, mapId)!;
        var definition = core.FindSpatialReferenceDefinition(project.ProjectId, "fixture:local-cartesian-v1");

        Assert.NotNull(definition);
        Assert.Equal("fixture-map-space-001", map.MapSpaceId);
        Assert.Equal("fixture:local-cartesian-v1", map.SpatialReferenceId);
        Assert.Equal("fixture:coordinate-precision-v1", definition!.PrecisionProfile.ProfileId);
        Assert.Equal("m", definition.PrecisionProfile.CoordinateUnit);
        Assert.Equal(SpatialAxisOrder.XY, definition.PrecisionProfile.AxisOrder);
        Assert.Equal("fixture:vertical-reference-local-v1", definition.VerticalReferenceId);
        Assert.Equal(64, definition.DefinitionFingerprint.Length);
        Assert.Null(core.FindSpatialReferenceDefinition(Guid.NewGuid(), "fixture:local-cartesian-v1"));

        Assert.Throws<CoreDomainConflictException>(() =>
            core.BindMapSpatialReference(project.ProjectId, mapId, "another:reference", Now + 3));
    }

    [Theory]
    [InlineData("map-space")]
    [InlineData("spatial-reference")]
    [InlineData("precision-profile")]
    [InlineData("vertical-reference")]
    public void PublishRevision_RejectsSpatialIdentityThatDoesNotMatchMapOwner(string mismatch)
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1");
        manifest = mismatch switch
        {
            "map-space" => manifest.Replace("fixture-map-space-001", "another-map-space"),
            "spatial-reference" => manifest.Replace("fixture:local-cartesian-v1", "another:spatial-reference"),
            "precision-profile" => manifest.Replace("fixture:coordinate-precision-v1", "another:precision-profile"),
            "vertical-reference" => manifest.Replace("fixture:vertical-reference-local-v1", "another:vertical-reference"),
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch)),
        };
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with { ManifestJson = manifest };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_CreatesDatasetThenAppendsImmutableLineage()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        var first = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), null, Fingerprint1, Now + 4));

        Assert.Equal(1, first.RevisionNumber);
        Assert.Null(first.ParentRevisionId);
        Assert.Equal(Fingerprint1, first.ContentFingerprint);
        Assert.Contains("world-view.field.elevation.v1", first.ManifestJson);

        var second = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), first.FieldRevisionId, Fingerprint2, Now + 7));

        Assert.Equal(2, second.RevisionNumber);
        Assert.Equal(first.FieldRevisionId, second.ParentRevisionId);
        Assert.Equal(second.FieldRevisionId,
            core.FindFieldDataset(project.ProjectId, mapId, "elevation")!.CurrentFieldRevisionId);
        Assert.Equal(new[] { first.FieldRevisionId, second.FieldRevisionId },
            core.ListFieldRevisions(project.ProjectId, datasetId).Select(x => x.FieldRevisionId).ToArray());
    }

    [Fact]
    public void PublishRevision_CanonicalizesManifestBeforePersistence()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var canonical = BuildManifest("elevation", "world-view.field.elevation.v1");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = canonical + "\n",
        };

        var revision = FieldDatasetService.PublishRevision(core, plan);

        Assert.Equal(canonical, revision.ManifestJson);
        Assert.NotNull(revision.Metadata);
        Assert.Equal("elevation", revision.Metadata!.FieldKey);
        Assert.Equal("m", revision.Metadata.Unit);
        Assert.Equal("fixture:vertical-reference-local-v1", revision.Metadata.VerticalReferenceId);
        Assert.Equal("fixture:coordinate-precision-v1", revision.Metadata.CoordinatePrecisionProfileId);
        Assert.Equal(2, revision.Metadata.Grid.Width);
        Assert.Equal("0", revision.Metadata.Grid.OriginX);
        Assert.Equal("1", revision.Metadata.Grid.SpacingX);
        Assert.Contains(revision.Metadata.Planes,
            plane => plane.Name == "samples" && plane.SampleFormat == "float32_ieee754_le");
        var reloaded = core.FindFieldRevision(project.ProjectId, revision.FieldDatasetId, revision.FieldRevisionId);
        Assert.NotNull(reloaded?.Metadata);
        Assert.Equal(revision.Metadata!.FieldKey, reloaded!.Metadata!.FieldKey);
        Assert.Equal(revision.Metadata.Grid, reloaded.Metadata.Grid);
        Assert.Equal(revision.Metadata.Planes, reloaded.Metadata.Planes);
        Assert.Equal(revision.Metadata.Codecs, reloaded.Metadata.Codecs);
    }

    [Fact]
    public void PublishRevision_RejectsDuplicateManifestObjectKeys()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1")
            .Replace("\"byte_order\":\"little_endian\"",
                "\"byte_order\":\"little_endian\",\"byte_order\":\"little_endian\"");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with { ManifestJson = manifest };

        Assert.Throws<CoreDomainDataException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsNaNManifestNumbers()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1")
            .Replace("\"width\":2", "\"width\":NaN");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with { ManifestJson = manifest };

        Assert.Throws<CoreDomainDataException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void CanonicalFieldManifest_IsIdempotentAndPropertyOrderIndependent()
    {
        var canonical = BuildManifest("elevation", "world-view.field.elevation.v1");
        Assert.Equal(canonical, CanonicalJson.Canonicalize(canonical));
        Assert.Equal(
            FieldManifestCanonicalizer.Canonicalize("{\"z\":1,\"a\":2}"),
            FieldManifestCanonicalizer.Canonicalize("{\"a\":2,\"z\":1}"));
    }

    [Fact]
    public void PublishRevision_PreservesExactDecimalStringPrecision()
    {
        const string originX = "0.12345678901234567890123456789";
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = BuildManifest("elevation", "world-view.field.elevation.v1", originX),
        };

        var revision = FieldDatasetService.PublishRevision(core, plan);

        Assert.Contains(originX, revision.ManifestJson);
    }

    [Fact]
    public void PublishRevision_AcceptsRegisteredLandWaterManifest()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            FieldKey = "land_water",
            SemanticsId = "world-view.field.land_water.v1",
            ManifestJson = BuildManifest("land_water", "world-view.field.land_water.v1"),
        };

        var revision = FieldDatasetService.PublishRevision(core, plan);

        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal("land_water",
            core.FindFieldDataset(project.ProjectId, mapId, "land_water")!.FieldKey);
    }

    [Fact]
    public void PublishRevision_RejectsManifestMissingRequiredSchema()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = "{}",
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsUnregisteredDescriptorSemantics()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            SemanticsId = "world-view.field.elevation.experimental",
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsManifestDescriptorMismatch()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = BuildManifest("elevation", "world-view.field.land_water.v1"),
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsStaleExpectedCurrentRevision()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        var first = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), null, Fingerprint1, Now + 4));
        var second = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), first.FieldRevisionId, Fingerprint2, Now + 7));

        Assert.Throws<CoreDomainConflictException>(() => core.PublishFieldRevision(Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), first.FieldRevisionId, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", Now + 10)));

        Assert.Equal(2, core.ListFieldRevisions(project.ProjectId, datasetId).Count);
        Assert.Equal(second.FieldRevisionId,
            core.FindFieldDataset(project.ProjectId, mapId, "elevation")!.CurrentFieldRevisionId);
    }

    [Fact]
    public void PublishRevision_PersistsAndReadsExactSqliteBlobPayload()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4);
        var expectedBytes = plan.PayloadBytes!.ToArray();

        var revision = FieldDatasetService.PublishRevision(core, plan);

        var payload = core.FindFieldPayload(project.ProjectId, revision.FieldRevisionId);
        Assert.NotNull(payload);
        Assert.Equal(FieldPayloadStorageKind.SqliteBlob, payload!.StorageKind);
        Assert.Equal(expectedBytes.LongLength, payload.ByteLength);
        Assert.Equal(expectedBytes, core.ReadFieldPayloadBlob(project.ProjectId, revision.FieldRevisionId));
        Assert.Equal(payload.FieldPayloadId, revision.Payload.FieldPayloadId);
    }

    [Fact]
    public async Task FieldPayloadReader_OpensVerifiedManagedFileThroughUnifiedReadPath()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var store = new FieldPayloadFileStore(_root);
        var bytes = Enumerable.Range(0, 256 * 1024).Select(i => (byte)(i % 251)).ToArray();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4);
        var publisher = new FieldPayloadPublisher(core, store);
        var revision = await publisher.PublishManagedFileRevisionAsync(plan, new MemoryStream(bytes));
        var reader = new FieldPayloadReader(core, store);

        await using var opened = await reader.OpenReadAsync(project.ProjectId, revision.FieldRevisionId);
        Assert.NotNull(opened);
        using var copy = new MemoryStream();
        await opened!.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task FieldPayloadPublisher_RetainsPublishedFileWhenDatabasePublicationFails()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var store = new FieldPayloadFileStore(_root);
        var bytes = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };
        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with { MapId = Guid.NewGuid() };
        var publisher = new FieldPayloadPublisher(core, store);

        await Assert.ThrowsAsync<CoreDomainConflictException>(() =>
            publisher.PublishManagedFileRevisionAsync(plan, new MemoryStream(bytes)));

        Assert.True(store.Exists(expectedHash));
        Assert.Null(core.FindFieldPayload(project.ProjectId, plan.FieldRevisionId));
    }

    [Fact]
    public async Task FieldPayloadReader_RejectsManagedFileWithSameLengthWrongHash()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var store = new FieldPayloadFileStore(_root);
        var bytes = new byte[] { 0x57, 0x56, 0x46, 0x50 };
        var stored = await store.PutAsync(new MemoryStream(bytes));
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4);
        plan = plan with
        {
            Payload = plan.Payload with
            {
                StorageKind = FieldPayloadStorageKind.ManagedFile,
                ContentHash = stored.ContentHash,
                ByteLength = stored.ByteLength,
                RelativePath = stored.RelativePath,
            },
            PayloadBytes = null,
        };
        var revision = FieldDatasetService.PublishRevision(core, plan);
        await File.WriteAllBytesAsync(Path.Combine(store.RootPath, stored.ContentHash),
            new byte[] { 0x00, 0x00, 0x00, 0x00 });
        var reader = new FieldPayloadReader(core, store);

        await Assert.ThrowsAsync<CoreDomainDataException>(async () =>
            await reader.OpenReadAsync(project.ProjectId, revision.FieldRevisionId));
    }

    [Fact]
    public void ReadFieldPayloadBlob_RejectsSameLengthBytesWithWrongHash()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var revision = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4));

        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE core_field_payload SET payload_blob = x'00000000' WHERE field_revision_id = $id;";
            command.Parameters.AddWithValue("$id", revision.FieldRevisionId.ToString("D"));
            command.ExecuteNonQuery();
        });

        Assert.Throws<CoreDomainDataException>(() =>
            core.ReadFieldPayloadBlob(project.ProjectId, revision.FieldRevisionId));
    }

    [Fact]
    public void FieldRevisionSchema_UsesDedicatedPayloadWithoutGenericAssetForeignKey()
    {
        var columns = Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('core_map_field_revision');";
            using var reader = command.ExecuteReader();
            var names = new System.Collections.Generic.List<string>();
            while (reader.Read())
                names.Add(reader.GetString(1));
            return names;
        });
        var foreignTables = Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_list('core_map_field_revision');";
            using var reader = command.ExecuteReader();
            var names = new System.Collections.Generic.List<string>();
            while (reader.Read())
                names.Add(reader.GetString(2));
            return names;
        });

        Assert.DoesNotContain("field_revision_asset_id", columns);
        Assert.DoesNotContain("core_asset", foreignTables);
        Assert.Contains("core_map_field_dataset", foreignTables);
        Assert.Contains("core_map_field_revision", foreignTables);
    }

    [Fact]
    public void PublishedRevisionRows_AreImmutable()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        var revision = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), null, Fingerprint1, Now + 4));

        Assert.Throws<SqliteException>(() => Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE core_map_field_revision SET provenance_json = '{}' WHERE field_revision_id = $id;";
            command.Parameters.AddWithValue("$id", revision.FieldRevisionId.ToString("D"));
            command.ExecuteNonQuery();
        }));
        Assert.Throws<SqliteException>(() => Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM core_map_field_revision WHERE field_revision_id = $id;";
            command.Parameters.AddWithValue("$id", revision.FieldRevisionId.ToString("D"));
            command.ExecuteNonQuery();
        }));
        Assert.NotNull(core.FindFieldRevision(project.ProjectId, datasetId, revision.FieldRevisionId));
    }

    [Fact]
    public void PublishRevision_FailureRollsBackNewDatasetAndRevision()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        Access().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER trg_test_reject_field_revision
                BEFORE INSERT ON core_map_field_revision
                BEGIN
                    SELECT RAISE(ABORT, 'test publication failure');
                END;
                """;
            command.ExecuteNonQuery();
        });

        Assert.Throws<SqliteException>(() => core.PublishFieldRevision(Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), null, Fingerprint1, Now + 4)));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldRevisions(project.ProjectId, datasetId));
    }



    [Theory]
    [InlineData("MNF-003")]
    [InlineData("MNF-004")]
    [InlineData("MNF-005")]
    [InlineData("MNF-006")]
    [InlineData("MNF-007")]
    [InlineData("MNF-008")]
    [InlineData("MNF-009")]
    [InlineData("MNF-010")]
    [InlineData("MNF-011")]
    [InlineData("MNF-012")]
    [InlineData("MNF-013")]
    [InlineData("MNF-014")]
    [InlineData("MNF-015")]
    [InlineData("MNF-016")]
    [InlineData("MNF-017")]
    [InlineData("MNF-018")]
    [InlineData("MNF-019")]
    [InlineData("MNF-020")]
    public void PublishedValidationVector_RejectsInvalidManifest(string vectorId)
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var root = JsonNode.Parse(BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();

        switch (vectorId)
        {
            case "MNF-003":
                root["unexpected"] = true;
                break;
            case "MNF-004":
                root["manifest_schema"]!["version"] = 2;
                break;
            case "MNF-005":
                root["format"]!["minor"] = 1;
                break;
            case "MNF-006":
                root["field"]!["field_key"] = "unknown_field";
                break;
            case "MNF-007":
                root["field"]!["semantics_id"] = "world-view.field.elevation.experimental";
                break;
            case "MNF-008":
                root["spatial"]!.AsObject().Remove("map_space_id");
                break;
            case "MNF-009":
                root["grid"]!["width"] = 0;
                break;
            case "MNF-010":
                root["grid"]!["tile_width"] = 65536;
                break;
            case "MNF-011":
                root["grid"]!["spacing_x"] = "1e-3";
                break;
            case "MNF-012":
                root["grid"]!["axis_orientation"] = "x_north_y_east";
                break;
            case "MNF-013":
                root["coverage"]!["mode"] = "sparse";
                break;
            case "MNF-014":
                root["map_edge_semantics"] = "wrap_everything";
                break;
            case "MNF-015":
                root["no_data"]!["policy"] = "zero_is_nodata";
                break;
            case "MNF-016":
                root["value_encoding"]!["planes"]![0]!["bytes_per_cell"] = 2;
                break;
            case "MNF-017":
                root["field"]!["field_key"] = "land_water";
                root["field"]!["semantics_id"] = "world-view.field.land_water.v1";
                root["field"]!.AsObject().Remove("unit");
                root["spatial"]!.AsObject().Remove("vertical_reference_id");
                root["value_encoding"]!["planes"]![0]!["name"] = "categories";
                root["value_encoding"]!["planes"]![0]!["sample_format"] = "uint8_registered_category";
                root["value_encoding"]!["planes"]![0]!["bytes_per_cell"] = 1;
                root["value_encoding"]!["category_codes"] = new JsonObject
                {
                    ["land"] = 1, ["water"] = 1, ["unknown"] = 2, ["outside_coverage"] = 3,
                };
                break;
            case "MNF-018":
                root["value_encoding"]!["category_codes"] = new JsonObject
                {
                    ["land"] = 0, ["water"] = 1, ["unknown"] = 2, ["outside_coverage"] = 3,
                };
                break;
            case "MNF-019":
                root["codec_registry"] = new JsonArray();
                break;
            case "MNF-020":
                root["tile_hash"]!["algorithm"] = "MD5";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(vectorId), vectorId, "Unknown P2-09-35 vector.");
        }

        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            FieldKey = vectorId == "MNF-017" ? "land_water" : "elevation",
            SemanticsId = vectorId == "MNF-017"
                ? "world-view.field.land_water.v1"
                : "world-view.field.elevation.v1",
            ManifestJson = root.ToJsonString(),
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(
            project.ProjectId, mapId, vectorId == "MNF-017" ? "land_water" : "elevation"));
    }

    [Fact]
    public void PublishRevision_PersistsManifestUsingRfc8785CanonicalBytes()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var parsed = JsonNode.Parse(
            BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        var reordered = new JsonObject();
        foreach (var property in parsed.Reverse())
            reordered[property.Key] = property.Value?.DeepClone();

        var rawManifest = reordered.ToJsonString();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = rawManifest,
        };

        var revision = FieldDatasetService.PublishRevision(core, plan);

        Assert.NotEqual(rawManifest, revision.ManifestJson);
        Assert.Equal(
            FieldManifestCanonicalizer.Canonicalize(rawManifest),
            revision.ManifestJson);
        Assert.Equal(
            BuildManifest("elevation", "world-view.field.elevation.v1"),
            revision.ManifestJson);
    }

    [Fact]
    public void PublishedValidationVector_Mnf023CanonicalizationIsIdempotent()
    {
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1");
        Assert.Equal(manifest, FieldManifestCanonicalizer.Canonicalize(manifest));
    }

    [Fact]
    public void PublishedValidationVector_Mnf024PropertyOrderDoesNotChangeCanonicalBytes()
    {
        Assert.Equal(
            FieldManifestCanonicalizer.Canonicalize("{\"z\":1,\"a\":2}"),
            FieldManifestCanonicalizer.Canonicalize("{\"a\":2,\"z\":1}"));
    }

    [Fact]
    public void PublishedValidationVector_Mnf025PreservesDecimalLexicalPrecision()
    {
        PublishRevision_PreservesExactDecimalStringPrecision();
    }

    [Fact]
    public void PublishedValidationVector_Mnf027AllowsFixtureOnlyProfileWithoutPromotingIt()
    {
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1");
        Assert.Contains("\"profile_scope\":\"golden_fixture_only\"", manifest);
    }


    [Theory]
    [InlineData("missing-unit")]
    [InlineData("invalid-unit")]
    [InlineData("missing-vertical-reference")]
    [InlineData("missing-coordinate-precision")]
    [InlineData("categorical-unit")]
    [InlineData("categorical-vertical-reference")]
    public void PublishRevision_EnforcesFieldAndSpatialSemanticRequirements(string scenario)
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var root = JsonNode.Parse(BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        var fieldKey = "elevation";
        var semanticsId = "world-view.field.elevation.v1";

        switch (scenario)
        {
            case "missing-unit":
                root["field"]!.AsObject().Remove("unit");
                break;
            case "invalid-unit":
                root["field"]!["unit"] = "cm";
                break;
            case "missing-vertical-reference":
                root["spatial"]!.AsObject().Remove("vertical_reference_id");
                break;
            case "missing-coordinate-precision":
                root["spatial"]!.AsObject().Remove("coordinate_precision_profile_id");
                break;
            case "categorical-unit":
            case "categorical-vertical-reference":
                fieldKey = "land_water";
                semanticsId = "world-view.field.land_water.v1";
                root["field"]!["field_key"] = fieldKey;
                root["field"]!["semantics_id"] = semanticsId;
                root["field"]!.AsObject().Remove("unit");
                if (scenario == "categorical-unit")
                {
                    root["field"]!["unit"] = "m";
                    root["spatial"]!.AsObject().Remove("vertical_reference_id");
                }
                else
                {
                    root["spatial"]!["vertical_reference_id"] = "fixture:vertical-reference-local-v1";
                }
                break;
        }

        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            FieldKey = fieldKey,
            SemanticsId = semanticsId,
            ManifestJson = root.ToJsonString(),
        };
        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, fieldKey));
    }

    [Fact]
    public void PublishRevision_RejectsManifestStringsOutsideSchemaBounds()
    {
        var (project, mapId) = SeedMap();
        var core = Core();

        var spatialManifest = JsonNode.Parse(
            BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        spatialManifest["spatial"]!["spatial_reference_id"] = new string('s', 513);
        var spatialPlan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = spatialManifest.ToJsonString(),
        };
        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, spatialPlan));

        var planeManifest = JsonNode.Parse(
            BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        planeManifest["value_encoding"]!["planes"]![0]!["name"] = new string('p', 65);
        var planePlan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 5) with
        {
            ManifestJson = planeManifest.ToJsonString(),
        };
        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, planePlan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }


    [Fact]
    public void PublishRevision_RejectsMissingManifestSchemaIdentity()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var manifest = JsonNode.Parse(
            BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        manifest["manifest_schema"]!.AsObject().Remove("id");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = manifest.ToJsonString(),
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsUnknownDescriptorSchemaVersion()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            DescriptorSchemaVersion = 2,
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsDuplicateCodecIds()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var manifest = JsonNode.Parse(
            BuildManifest("elevation", "world-view.field.elevation.v1"))!.AsObject();
        manifest["codec_registry"]!.AsArray().Add(new JsonObject
        {
            ["codec_id"] = 0,
            ["codec_name"] = "none",
            ["version"] = "1",
            ["parameters"] = new JsonObject(),
        });
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            null, Fingerprint1, Now + 4) with
        {
            ManifestJson = manifest.ToJsonString(),
        };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

}
