using System;
using System.IO;
using System.Text;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>Real SQLite proof for ME-05 Field Dataset identity, immutable revisions and CAS publication.</summary>
public sealed class FieldDatasetPersistenceTests : IDisposable
{
    private const long Now = 10_000L;
    private readonly string _root;
    private readonly SqlitePackageDatabase _database;
    private readonly ApplicationBootstrapper _bootstrapper =
        new(new[] { new CoreDomainMigrationSource() });

    public FieldDatasetPersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-me05-field-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)new SqlitePackageDatabaseFactory()
            .CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        _bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    private (Guid ProjectId, Guid MapId, Guid MapBId) SeedProjectAndMaps()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("ME-05 Field Dataset", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var mapA = MapService.CreateMap(core, project.ProjectId, "Map A", Now + 1);
        var mapB = MapService.CreateMap(core, project.ProjectId, "Map B", Now + 2);
        return (project.ProjectId, mapA.Map.MapId, mapB.Map.MapId);
    }

    private Guid RegisterVerifiedAsset(Guid projectId, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = AssetRules.Sha256Hex(bytes);
        var core = Core();
        var asset = AssetService.RegisterAsset(
            core, projectId, Guid.NewGuid(), hash, bytes.Length, "application/vnd.worldview.field-package", "WVFLDPK-1.0", Now + 3);
        core.UpdateAssetIntegrityState(projectId, asset.AssetId, AssetIntegrityState.Verified, Now + 4);
        return asset.AssetId;
    }

    private static string Manifest(string fieldKey, string semanticsId) => $$"""
        {"byte_order":"little_endian","canonicalization":"RFC8785-JCS-UTF8","codec_registry":[{"codec_id":0,"codec_name":"none","version":"1","parameters":{}}],"coverage":{"mode":"full_grid"},"field":{"field_key":"{{fieldKey}}","schema_version":1,"semantics_id":"{{semanticsId}}"},"format":{"major":1,"minor":0},"grid":{"width":2,"height":2,"tile_width":2,"tile_height":2,"origin_x":"0","origin_y":"0","spacing_x":"1","spacing_y":"1","axis_orientation":"x_east_y_north","cell_registration":"area"},"manifest_schema":{"id":"world-view.field-package-manifest","version":1},"map_edge_semantics":"bounded_non_wrapping","no_data":{"policy":"explicit_validity_mask","mask_plane":{"plane_name":"validity_mask","valid_value":0,"invalid_value":1}},"spatial":{"map_space_id":"map-space-a","spatial_reference_id":"sr-local-a"},"tile_hash":{"algorithm":"SHA-256","version":1},"value_encoding":{"profile_id":"golden_fixture_only","profile_scope":"golden_fixture_only","planes":[{"name":"value","sample_format":"float32_ieee754_le","bytes_per_cell":4},{"name":"validity_mask","sample_format":"uint8","bytes_per_cell":1}]{{(fieldKey == "land_water" ? ",\\"category_codes\\":{\\"land\\":1,\\"water\\":2,\\"unknown\\":3,\\"outside_coverage\\":4}" : "")}}}}
        """;

    private static string Provenance(string sourceKind) => $$"""{"source_kind":"{{sourceKind}}","source_id":"source-1"}""";

    [Fact]
    public void PublishRevision_CreatesMapOwnedDatasetAndImmutableRevision()
    {
        var (projectId, mapId, _) = SeedProjectAndMaps();
        var assetId = RegisterVerifiedAsset(projectId, "field package v1");
        var core = Core();

        var revision = FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", assetId,
            Manifest("elevation", "world-view.field.elevation.v1"),
            Provenance("imported"), null, Now + 5);

        var dataset = core.FindFieldDataset(projectId, mapId, "elevation");
        Assert.NotNull(dataset);
        Assert.Equal(revision.FieldDatasetId, dataset!.FieldDatasetId);
        Assert.Equal(revision.FieldRevisionId, dataset.CurrentFieldRevisionId);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Null(revision.ParentRevisionId);
        Assert.Equal(revision, core.FindCurrentFieldRevision(projectId, mapId, "elevation"));
        Assert.Single(core.ListFieldRevisions(projectId, dataset.FieldDatasetId));
    }

    [Fact]
    public void PublishRevision_AdvancesMonotonicRevisionWithParentLineage()
    {
        var (projectId, mapId, _) = SeedProjectAndMaps();
        var asset1 = RegisterVerifiedAsset(projectId, "field package v1");
        var asset2 = RegisterVerifiedAsset(projectId, "field package v2");
        var core = Core();
        var first = FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", asset1,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("imported"), null, Now + 5);

        var second = FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", asset2,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("authored"),
            first.FieldRevisionId, Now + 6);

        Assert.Equal(first.FieldDatasetId, second.FieldDatasetId);
        Assert.Equal(2, second.RevisionNumber);
        Assert.Equal(first.FieldRevisionId, second.ParentRevisionId);
        Assert.Equal(second.FieldRevisionId, core.FindFieldDataset(projectId, mapId, "elevation")!.CurrentFieldRevisionId);
        Assert.Equal(2, core.ListFieldRevisions(projectId, first.FieldDatasetId).Count);
    }

    [Fact]
    public void PublishRevision_StaleExpectedCurrentRevisionRejectedWithoutPartialRows()
    {
        var (projectId, mapId, _) = SeedProjectAndMaps();
        var asset1 = RegisterVerifiedAsset(projectId, "field package v1");
        var asset2 = RegisterVerifiedAsset(projectId, "field package v2");
        var core = Core();
        var first = FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", asset1,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("imported"), null, Now + 5);

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", asset2,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("authored"),
            Guid.NewGuid(), Now + 6));

        Assert.Equal(first.FieldRevisionId, core.FindCurrentFieldRevision(projectId, mapId, "elevation")!.FieldRevisionId);
        Assert.Single(core.ListFieldRevisions(projectId, first.FieldDatasetId));
    }

    [Fact]
    public void SameAssetAcrossMaps_CreatesIndependentDatasetAuthorities()
    {
        var (projectId, mapA, mapB) = SeedProjectAndMaps();
        var assetId = RegisterVerifiedAsset(projectId, "identical shared bytes");
        var core = Core();
        var first = FieldDatasetService.PublishRevision(
            core, projectId, mapA, "land_water", assetId,
            Manifest("land_water", "world-view.field.land_water.v1"), Provenance("authored"), null, Now + 5);
        var second = FieldDatasetService.PublishRevision(
            core, projectId, mapB, "land_water", assetId,
            Manifest("land_water", "world-view.field.land_water.v1"), Provenance("authored"), null, Now + 6);

        Assert.NotEqual(first.FieldDatasetId, second.FieldDatasetId);
        Assert.Equal(assetId, first.FieldRevisionAssetId);
        Assert.Equal(assetId, second.FieldRevisionAssetId);
        Assert.Equal(first.FieldRevisionId, core.FindCurrentFieldRevision(projectId, mapA, "land_water")!.FieldRevisionId);
        Assert.Equal(second.FieldRevisionId, core.FindCurrentFieldRevision(projectId, mapB, "land_water")!.FieldRevisionId);
    }

    [Fact]
    public void PublishRevision_RequiresVerifiedAssetAndRegisteredDescriptor()
    {
        var (projectId, mapId, _) = SeedProjectAndMaps();
        var bytes = Encoding.UTF8.GetBytes("unverified");
        var asset = AssetService.RegisterAsset(Core(), projectId, Guid.NewGuid(), AssetRules.Sha256Hex(bytes),
            bytes.Length, "application/vnd.worldview.field-package", "WVFLDPK-1.0", Now + 3);

        Assert.Throws<CoreDomainConflictException>(() => FieldDatasetService.PublishRevision(
            Core(), projectId, mapId, "elevation", asset.AssetId,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("imported"), null, Now + 5));

        Assert.Throws<ArgumentException>(() => FieldDatasetService.PublishRevision(
            Core(), projectId, mapId, "soil_class", asset.AssetId,
            Manifest("soil_class", "world-view.field.soil_class.v1"), Provenance("imported"), null, Now + 5));
    }

    [Fact]
    public void PublishedRevision_RawUpdateAndDeleteAreBlockedByDatabase()
    {
        var (projectId, mapId, _) = SeedProjectAndMaps();
        var assetId = RegisterVerifiedAsset(projectId, "immutable field payload");
        var core = Core();
        var revision = FieldDatasetService.PublishRevision(
            core, projectId, mapId, "elevation", assetId,
            Manifest("elevation", "world-view.field.elevation.v1"), Provenance("imported"), null, Now + 5);
        var access = ((ISqliteConnectionProvider)_database).GetAccess();

        Assert.ThrowsAny<Exception>(() => access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE core_map_field_revision SET manifest_json = '{}' WHERE field_revision_id = $id;";
            command.Parameters.AddWithValue("$id", revision.FieldRevisionId.ToString("D"));
            command.ExecuteNonQuery();
        }));

        Assert.ThrowsAny<Exception>(() => access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM core_map_field_revision WHERE field_revision_id = $id;";
            command.Parameters.AddWithValue("$id", revision.FieldRevisionId.ToString("D"));
            command.ExecuteNonQuery();
        }));
        Assert.NotNull(core.FindFieldRevision(projectId, revision.FieldRevisionId));
    }
}
