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
/// SqlitePackageDatabase + ApplicationBootstrapper 集成测试（真实 SQLite 文件）。
/// 覆盖 fail-closed 语义：未知迁移、校验和不匹配、更高数据库版本、迁移失败回滚。
/// </summary>
public class SqlitePackageDatabaseTests : IDisposable
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
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static ModuleMigration Migration(string id, string sql) => new()
    {
        ModuleId = "application",
        MigrationId = id,
        Checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql))).ToLowerInvariant(),
        SqlScript = sql,
    };

    private static string SqlOf(string id) => $"CREATE TABLE IF NOT EXISTS {id} (id TEXT PRIMARY KEY);";

    private async Task InitializeAsync(params ModuleMigration[] migrations)
    {
        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(migrations) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var result = await bootstrapper.InitializeNewDatabaseAsync(db, DateTimeOffset.UtcNow);
        Assert.True(result.Success);
    }

    private sealed class FixedSource(params ModuleMigration[] migrations) : IMigrationSource
    {
        public IReadOnlyList<ModuleMigration> DiscoverMigrations() => migrations;
    }

    [Fact]
    public async Task FreshFile_BootstrapCreatesMetadataAndLedger()
    {
        await InitializeAsync();

        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();

        Assert.True(await db.LedgerTableExistsAsync());
        var metadata = await db.ReadMetadataAsync();
        Assert.NotNull(metadata);
        Assert.Equal(ProjectPackageFormat.SupportedDatabaseFormatVersion, metadata!.DatabaseFormatVersion);
        Assert.True(await db.IntegrityCheckAsync());
    }

    [Fact]
    public async Task Migration_ExecutesInTransaction_WithLedgerRow()
    {
        var migration = Migration("001", SqlOf("t_demo"));
        await InitializeAsync(migration);

        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var ledger = await db.ReadLedgerAsync();
        var entry = Assert.Single(ledger);
        Assert.Equal("application", entry.ModuleId);
        Assert.Equal("001", entry.MigrationId);
        Assert.Equal(migration.Checksum, entry.Checksum);

        // 脚本确实执行（表存在于数据库文件中，用独立连接验证）。
        using var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=false");
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='t_demo';";
        Assert.Equal(1L, check.ExecuteScalar());
    }

    [Fact]
    public async Task Revalidation_AppliesNoPendingMigrations_AndPasses()
    {
        var migration = Migration("001", SqlOf("t_demo"));
        await InitializeAsync(migration);

        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(migration) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();

        var result = await bootstrapper.ValidateExistingDatabaseAsync(db);

        Assert.True(result.Success);
        Assert.Empty(result.AppliedMigrations);
    }

    [Fact]
    public async Task NewMigration_AppliedOnRevalidation()
    {
        var m1 = Migration("001", SqlOf("t_one"));
        await InitializeAsync(m1);

        var m2 = Migration("002", SqlOf("t_two"));
        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(m1, m2) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var result = await bootstrapper.ValidateExistingDatabaseAsync(db);

        Assert.True(result.Success);
        Assert.Equal("002", Assert.Single(result.AppliedMigrations).MigrationId);
    }

    [Fact]
    public async Task LedgerChecksumMismatch_FailsClosed()
    {
        var migration = Migration("001", SqlOf("t_demo"));
        await InitializeAsync(migration);

        // 篡改 ledger 中的校验和（模拟迁移内容被替换）。
        using (var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=false"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE schema_migration SET checksum = 'deadbeef';";
            command.ExecuteNonQuery();
        }

        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(migration) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var result = await bootstrapper.ValidateExistingDatabaseAsync(db);

        Assert.False(result.Success);
        Assert.Equal(PackageIssueCode.LedgerChecksumMismatch, result.Issues[0].Code);
    }

    [Fact]
    public async Task UnknownLedgerMigration_FailsClosed()
    {
        await InitializeAsync();

        // ledger 中写入未登记迁移（模拟更高版本写入）。
        using (var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=false"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO schema_migration (module_id, migration_id, checksum, applied_at_utc_ms) VALUES ('core-domain', '900', 'x', 1);";
            command.ExecuteNonQuery();
        }

        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(Migration("001", SqlOf("t_demo"))) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();
        var result = await bootstrapper.ValidateExistingDatabaseAsync(db);

        Assert.False(result.Success);
        Assert.Equal(PackageIssueCode.LedgerUnknownMigration, result.Issues[0].Code);
    }

    [Fact]
    public async Task FailedMigration_RollsBackScriptAndLedger()
    {
        // 脚本先建表再引用不存在的表 → 失败 → 全事务回滚。
        var broken = Migration(
            "001",
            "CREATE TABLE t_before (id TEXT PRIMARY KEY);\nINSERT INTO nonexistent_table VALUES (1);");
        var bootstrapper = new ApplicationBootstrapper(new[] { new FixedSource(broken) });
        await using var db = _factory.CreateForFile(_dbPath);
        await db.OpenAsync();

        var result = await bootstrapper.InitializeNewDatabaseAsync(db, DateTimeOffset.UtcNow);

        Assert.False(result.Success);
        Assert.Empty(await db.ReadLedgerAsync());

        // 回滚验证：t_before 不存在（直接以独立连接检查）。
        using var connection = new SqliteConnection($"Data Source={_dbPath};Pooling=false");
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='t_before';";
        Assert.Equal(0L, check.ExecuteScalar());
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
            // 打开或检查阶段直接抛出同样视为“未通过完整性检查”。
        }
    }
}
