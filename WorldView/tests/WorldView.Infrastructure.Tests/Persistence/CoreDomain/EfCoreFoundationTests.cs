using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WorldView.Application.Bootstrap;
using WorldView.Domain.Assets;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// Phase-0 proof: EF Core maps the legacy SQLite schema, applies a non-destructive baseline,
/// and shares the existing serialized physical connection with the hand-written adapter.
/// </summary>
public sealed class EfCoreFoundationTests : IDisposable
{
    private const long Now = 50_000L;
    private readonly string _root;
    private readonly SqlitePackageDatabase _database;
    private readonly ApplicationBootstrapper _bootstrapper =
        new(new[] { new CoreDomainMigrationSource() });

    public EfCoreFoundationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-efcore-foundation-{Guid.NewGuid():N}");
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

    private CoreDomainEfSession EfSession() =>
        new(((ISqliteConnectionProvider)_database).GetAccess());

    [Fact]
    public void EfCore_BaselineIsNonDestructive_AndReadsLegacyProjectMapAndAssetRows()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("EF Core foundation", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var map = MapService.CreateMap(core, project.ProjectId, "EF Core Map", Now + 1);
        var asset = AssetService.RegisterAsset(
            core, project.ProjectId, Guid.NewGuid(),
            "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899",
            128, "application/json", "1", Now + 2);

        var session = EfSession();
        session.ApplyMigrations();

        var actualProject = session.Execute(db => db.Projects.AsNoTracking().Single(x => x.ProjectId == project.ProjectId));
        var actualMap = session.Execute(db => db.Maps.AsNoTracking().Single(x => x.MapId == map.Map.MapId));
        var actualAsset = session.Execute(db => db.Assets.AsNoTracking().Single(x => x.AssetId == asset.AssetId));

        Assert.Equal(project.ProjectId, actualProject.ProjectId);
        Assert.Equal(project.ProjectName, actualProject.ProjectName);
        Assert.Equal(map.Map.MapId, actualMap.MapId);
        Assert.Equal(project.ProjectId, actualMap.ProjectId);
        Assert.Equal(asset.AssetId, actualAsset.AssetId);
        Assert.Equal(asset.ContentHash, actualAsset.ContentHash);
        Assert.Equal("published", actualAsset.PublicationState);
        Assert.Equal(new[] { "202611220001_CoreDomainEfCoreBaseline" },
            session.Execute(db => db.Database.GetAppliedMigrations().ToArray()));

        // Disposing the short-lived DbContext must not close the package-owned connection.
        var foreignKeysEnabled = ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys;";
            return Convert.ToInt32(command.ExecuteScalar());
        });
        Assert.Equal(1, foreignKeysEnabled);

        // Existing SQL adapter and EF Core observe the same committed row on the same connection.
        Assert.Equal(project.ProjectId, core.FindProject(project.ProjectId)!.ProjectId);
    }

    [Fact]
    public void EfCore_GuidMappingRejectsNonCanonicalPersistedUuid()
    {
        var session = EfSession();
        session.ApplyMigrations();

        var rawId = Guid.NewGuid().ToString("D").ToUpperInvariant();
        ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO core_project
                    (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
                VALUES ($id, 'invalid UUID fixture', 1, 1, 1);
                """;
            command.Parameters.AddWithValue("$id", rawId);
            command.ExecuteNonQuery();
        });

        Assert.Throws<InvalidOperationException>(() =>
            session.Execute(db => db.Projects.AsNoTracking().Single()));
    }
}
