using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Infrastructure.Persistence;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence;

/// <summary>
/// Package database tests: package metadata and SQLite integrity only.
/// Core Domain schema creation is covered by CoreDomainInitialMigrationTests through EF Core.
/// </summary>
public sealed class SqlitePackageDatabaseTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;
    private readonly SqlitePackageDatabaseFactory _factory = new();

    public SqlitePackageDatabaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "project.sqlite");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task FreshFile_BootstrapCreatesMetadataButNoLegacyMigrationLedger()
    {
        var bootstrapper = new ApplicationBootstrapper();
        await using (var db = _factory.CreateForFile(_dbPath))
        {
            await db.OpenAsync();
            var result = await bootstrapper.InitializeNewDatabaseAsync(db, DateTimeOffset.UtcNow);
            Assert.True(result.Success);
        }

        await using var reopened = _factory.CreateForFile(_dbPath);
        await reopened.OpenAsync();
        var metadata = await reopened.ReadMetadataAsync();
        Assert.NotNull(metadata);
        Assert.Equal(ProjectPackageFormat.SupportedDatabaseFormatVersion, metadata!.DatabaseFormatVersion);
        Assert.True(await reopened.IntegrityCheckAsync());

        using var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=false");
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_migration';";
        Assert.Equal(0L, check.ExecuteScalar());
    }

    [Fact]
    public async Task Revalidation_AcceptsSupportedPackageMetadata()
    {
        var bootstrapper = new ApplicationBootstrapper();
        await using (var db = _factory.CreateForFile(_dbPath))
        {
            await db.OpenAsync();
            Assert.True((await bootstrapper.InitializeNewDatabaseAsync(db, DateTimeOffset.UtcNow)).Success);
        }

        await using var reopened = _factory.CreateForFile(_dbPath);
        await reopened.OpenAsync();
        var result = await bootstrapper.ValidateExistingDatabaseAsync(reopened);
        Assert.True(result.Success);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task MissingMetadata_FailsClosed()
    {
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var result = await new ApplicationBootstrapper().ValidateExistingDatabaseAsync(db);
        Assert.False(result.Success);
        Assert.Equal(PackageIssueCode.BootstrapMetadataMissing, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task NewerDatabaseFormat_FailsClosed()
    {
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        await db.WriteMetadataAsync(new BootstrapMetadata
        {
            DatabaseFormatVersion = ProjectPackageFormat.SupportedDatabaseFormatVersion + 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        var result = await new ApplicationBootstrapper().ValidateExistingDatabaseAsync(db);
        Assert.False(result.Success);
        Assert.Equal(PackageIssueCode.DatabaseFormatUnsupported, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public async Task CorruptFile_QuickCheckFails()
    {
        await File.WriteAllBytesAsync(_dbPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        await using var db = _factory.CreateForFile(_dbPath);
        try
        {
            await db.OpenAsync();
            Assert.False(await db.IntegrityCheckAsync());
        }
        catch (SqliteException)
        {
            // Opening or checking the malformed file also constitutes a failed integrity check.
        }
    }
}
