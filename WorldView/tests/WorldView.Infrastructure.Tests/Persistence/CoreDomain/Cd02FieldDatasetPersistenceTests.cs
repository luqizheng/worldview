using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
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
/// Real SQLite proof for Map-owned Field Dataset publication: revision lineage, immutable
/// published rows, current-pointer CAS, typed Asset retention, and transaction rollback.
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
        core.CreateMapStructure(mapPlan);
        return (project, mapPlan.Map.MapId);
    }

    private AssetRecord RegisterVerifiedAsset(Guid projectId, string hash, long now)
    {
        var core = Core();
        var asset = AssetService.RegisterAsset(
            core, projectId, Guid.NewGuid(), hash, 1024, "application/octet-stream", "field-package-v1", now);
        core.UpdateAssetIntegrityState(projectId, asset.AssetId, AssetIntegrityState.Verified, now + 1);
        return core.FindAsset(projectId, asset.AssetId)!;
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

        return CanonicalJson.Canonicalize(System.Text.Json.JsonSerializer.Serialize(new
        {
            byte_order = "little_endian",
            canonicalization = "RFC8785-JCS-UTF8",
            codec_registry = new[] { new { codec_id = 0, codec_name = "none", parameters = new { }, version = "1" } },
            coverage = new { mode = "full_grid" },
            field = new { field_key = fieldKey, schema_version = 1, semantics_id = semanticsId },
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
            spatial = new { map_space_id = "fixture-map-space-001", spatial_reference_id = "fixture:local-cartesian-v1" },
            tile_hash = new { algorithm = "SHA-256", version = 1 },
            value_encoding = valueEncoding,
        }));
    }

    private static PublishFieldRevisionPlan Plan(
        Guid projectId,
        Guid mapId,
        Guid datasetId,
        Guid revisionId,
        Guid assetId,
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
        FieldRevisionAssetId = assetId,
        ExpectedCurrentFieldRevisionId = expectedRevision,
        ContentFingerprint = fingerprint,
        ManifestJson = BuildManifest("elevation", "world-view.field.elevation.v1"),
        ProvenanceJson = """{"source":"test-fixture"}""",
        CreatedAtUtcMs = now,
    };

    [Fact]
    public void PublishRevision_CreatesDatasetThenAppendsImmutableLineage()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        var asset1 = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var first = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset1.AssetId, null, Fingerprint1, Now + 4));

        Assert.Equal(1, first.RevisionNumber);
        Assert.Null(first.ParentRevisionId);
        Assert.Equal(Fingerprint1, first.ContentFingerprint);
        Assert.Contains("world-view.field.elevation.v1", first.ManifestJson);

        var asset2 = RegisterVerifiedAsset(project.ProjectId, Fingerprint2, Now + 5);
        var second = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset2.AssetId, first.FieldRevisionId, Fingerprint2, Now + 7));

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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var canonical = BuildManifest("elevation", "world-view.field.elevation.v1");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
        {
            ManifestJson = canonical + "\n",
        };

        var revision = FieldDatasetService.PublishRevision(core, plan);

        Assert.Equal(canonical, revision.ManifestJson);
    }

    [Fact]
    public void PublishRevision_RejectsDuplicateManifestObjectKeys()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1")
            .Replace("\"byte_order\":\"little_endian\"",
                "\"byte_order\":\"little_endian\",\"byte_order\":\"little_endian\"");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with { ManifestJson = manifest };

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void PublishRevision_RejectsNaNManifestNumbers()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var manifest = BuildManifest("elevation", "world-view.field.elevation.v1")
            .Replace("\"width\":2", "\"width\":NaN");
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with { ManifestJson = manifest };

        Assert.Throws<CoreDomainDataException>(() => FieldDatasetService.PublishRevision(core, plan));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    [Fact]
    public void CanonicalFieldManifest_IsIdempotentAndPropertyOrderIndependent()
    {
        var canonical = BuildManifest("elevation", "world-view.field.elevation.v1");
        Assert.Equal(canonical, CanonicalJson.Canonicalize(canonical));
        Assert.Equal(
            CanonicalJson.Canonicalize("{\"z\":1,\"a\":2}"),
            CanonicalJson.Canonicalize("{\"a\":2,\"z\":1}"));
    }

    [Fact]
    public void PublishRevision_PreservesExactDecimalStringPrecision()
    {
        const string originX = "0.12345678901234567890123456789";
        var (project, mapId) = SeedMap();
        var core = Core();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
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
        var asset1 = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var first = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset1.AssetId, null, Fingerprint1, Now + 4));
        var asset2 = RegisterVerifiedAsset(project.ProjectId, Fingerprint2, Now + 5);
        var second = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset2.AssetId, first.FieldRevisionId, Fingerprint2, Now + 7));

        var staleAsset = RegisterVerifiedAsset(project.ProjectId,
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", Now + 8);
        Assert.Throws<CoreDomainConflictException>(() => core.PublishFieldRevision(Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), staleAsset.AssetId,
            first.FieldRevisionId, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc", Now + 10)));

        Assert.Equal(2, core.ListFieldRevisions(project.ProjectId, datasetId).Count);
        Assert.Equal(second.FieldRevisionId,
            core.FindFieldDataset(project.ProjectId, mapId, "elevation")!.CurrentFieldRevisionId);
    }

    [Fact]
    public void PublishedRevisionRows_AreImmutable()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var datasetId = Guid.NewGuid();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var revision = FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset.AssetId, null, Fingerprint1, Now + 4));

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
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
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
            project.ProjectId, mapId, datasetId, Guid.NewGuid(), asset.AssetId, null, Fingerprint1, Now + 4)));
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldRevisions(project.ProjectId, datasetId));
    }

    [Fact]
    public void FieldRevisionAsset_IsNotReportedAsOrphan()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        FieldDatasetService.PublishRevision(core, Plan(
            project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(), asset.AssetId, null, Fingerprint1, Now + 4));

        Assert.DoesNotContain(asset.AssetId, core.FindOrphanAssetIds(project.ProjectId));
    }
}
