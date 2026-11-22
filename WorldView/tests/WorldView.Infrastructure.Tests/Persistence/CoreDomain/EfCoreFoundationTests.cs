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
        // Seed rows using only the legacy schema, before the EF migration ledger exists.
        var projectId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        const string contentHash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using (var project = connection.CreateCommand())
            {
                project.Transaction = transaction;
                project.CommandText = """
                    INSERT INTO core_project
                        (project_id, project_name, project_format_version, created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($id, 'EF Core foundation', 1, 50000, 50000);
                    """;
                project.Parameters.AddWithValue("$id", projectId.ToString("D"));
                project.ExecuteNonQuery();
            }
            using (var map = connection.CreateCommand())
            {
                map.Transaction = transaction;
                map.CommandText = """
                    INSERT INTO core_map
                        (map_id, project_id, map_name, lifecycle_status, created_at_utc_ms, updated_at_utc_ms)
                    VALUES ($id, $projectId, 'EF Core Map', 'active', 50001, 50001);
                    """;
                map.Parameters.AddWithValue("$id", mapId.ToString("D"));
                map.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
                map.ExecuteNonQuery();
            }
            using (var asset = connection.CreateCommand())
            {
                asset.Transaction = transaction;
                asset.CommandText = """
                    INSERT INTO core_asset
                        (asset_id, project_id, content_hash, hash_algorithm, byte_length, media_type,
                         format_version, relative_path, publication_state, integrity_state, created_at_utc_ms)
                    VALUES ($id, $projectId, $hash, 'sha-256', 128, 'application/json',
                            '1', $path, 'published', 'verified', 50002);
                    """;
                asset.Parameters.AddWithValue("$id", assetId.ToString("D"));
                asset.Parameters.AddWithValue("$projectId", projectId.ToString("D"));
                asset.Parameters.AddWithValue("$hash", contentHash);
                asset.Parameters.AddWithValue("$path", $"Assets/sha256/{contentHash}");
                asset.ExecuteNonQuery();
            }
            transaction.Commit();
        });

        var session = EfSession();
        session.ApplyMigrations();

        var actualProject = session.Execute(db => db.Projects.AsNoTracking().Single(x => x.ProjectId == projectId));
        var actualMap = session.Execute(db => db.Maps.AsNoTracking().Single(x => x.MapId == mapId));
        var actualAsset = session.Execute(db => db.Assets.AsNoTracking().Single(x => x.AssetId == assetId));

        Assert.Equal(projectId, actualProject.ProjectId);
        Assert.Equal("EF Core foundation", actualProject.ProjectName);
        Assert.Equal(mapId, actualMap.MapId);
        Assert.Equal(projectId, actualMap.ProjectId);
        Assert.Equal(assetId, actualAsset.AssetId);
        Assert.Equal(contentHash, actualAsset.ContentHash);
        Assert.Equal("published", actualAsset.PublicationState);
        Assert.Equal(new[]
        {
            "202611220001_CoreDomainEfCoreBaseline",
            "202611220002_FieldDatasetRevision",
        }, session.Execute(db => db.Database.GetAppliedMigrations().ToArray()));

        // Disposing the short-lived DbContext must not close the package-owned connection.
        var foreignKeysEnabled = ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys;";
            return Convert.ToInt32(command.ExecuteScalar());
        });
        Assert.Equal(1, foreignKeysEnabled);

        // The legacy adapter and EF Core read the same pre-existing row.
        Assert.Equal(projectId, Core().FindProject(projectId)!.ProjectId);
    }

    [Fact]
    public void EfCore_RejectsUnknownFutureMigrationHistory()
    {
        var session = EfSession();
        session.ApplyMigrations();

        ((ISqliteConnectionProvider)_database).GetAccess().Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES ('299901010000_FutureMigration', '99.0.0');
                """;
            command.ExecuteNonQuery();
        });

        Assert.Throws<CoreDomainDataException>(() => Core());
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
