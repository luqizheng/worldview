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
        ManifestJson = "{\\"field_key\\":\\"elevation\\",\\"map_id\\":\\"" + mapId.ToString("D") + "\\",\\"content_fingerprint\\":\\"" + fingerprint + "\\",\\"unit\\":\\"m\\"}",
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
        Assert.Contains("\\"map_id\\":\\"" + mapId.ToString("D") + "\\"", first.ManifestJson);
        Assert.Contains("\\"content_fingerprint\\":\\"" + Fingerprint1 + "\\"", first.ManifestJson);

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
    public void PublishRevision_RejectsManifestForDifferentMap()
    {
        var (project, mapId) = SeedMap();
        var core = Core();
        var asset = RegisterVerifiedAsset(project.ProjectId, Fingerprint1, Now + 2);
        var plan = Plan(project.ProjectId, mapId, Guid.NewGuid(), Guid.NewGuid(),
            asset.AssetId, null, Fingerprint1, Now + 4) with
        {
            ManifestJson = "{\\"field_key\\":\\"elevation\\",\\"map_id\\":\\"" + Guid.NewGuid().ToString("D") + "\\",\\"content_fingerprint\\":\\"" + Fingerprint1 + "\\"}",
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
