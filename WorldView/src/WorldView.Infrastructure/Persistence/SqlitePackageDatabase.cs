using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Infrastructure.Persistence.CoreDomain;

namespace WorldView.Infrastructure.Persistence;

/// <summary>
/// 包级元数据表 DDL。领域 Schema 完全由 EF Core Migrations 管理。
/// </summary>
internal static class SqliteBootstrapSchema
{
    public const string MetadataTableName = "wv_bootstrap_metadata";

    public const string CreateMetadataTableSql =
        """
        CREATE TABLE IF NOT EXISTS wv_bootstrap_metadata (
            key   TEXT NOT NULL PRIMARY KEY,
            value TEXT NOT NULL
        )
        """;
}

/// <summary>
/// Microsoft.Data.Sqlite 实现的包数据库访问。
/// 连接策略：禁用连接池（Pool=False）——打开/关闭/目录重命名与临时清理的句柄行为
/// 完全确定；每个连接显式启用外键（PRAGMA foreign_keys=ON，对齐 P2-09-12 TX-03）
/// 与有界 busy_timeout=5000ms（P2-09-12 §10：有界且与单写流程一致；journal_mode
/// 保持默认——WAL 依赖备份/恢复证明，属未关闭的 open gate，CD-01 不启用）。
///
/// 并发模型（CD-01-F1 V1）：本实例拥有的唯一物理连接由
/// <see cref="SqliteConnectionAccess"/> 统一协调——本类全部操作（含 Dispose）经同一临界区串行执行；Core Domain 适配器经
/// <see cref="ISqliteConnectionProvider.GetAccess"/> 共享同一协调器，不存在
/// 互不相干的实例级锁。跨实例（同一文件的多个连接）由 SQLite 文件级锁兜底 fail-closed。
/// </summary>
public sealed class SqlitePackageDatabase : IPackageDatabase, ISqliteConnectionProvider
{
    private readonly SqliteConnectionAccess _access;

    public SqlitePackageDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径缺失。", nameof(databasePath));
        }

        DatabasePath = System.IO.Path.GetFullPath(databasePath);
        _access = new SqliteConnectionAccess(DatabasePath);
    }

    public string DatabasePath { get; }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _access.Open();
        return Task.CompletedTask;
    }

    /// <summary>向同程序集的 Core Domain 适配器/测试暴露统一协调器（显式接口实现，不进入公共面）。</summary>
    SqliteConnectionAccess ISqliteConnectionProvider.GetAccess()
    {
        return _access;
    }

    public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _access.Execute(static connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            return command.ExecuteScalar() is string value
                && value.Equals("ok", StringComparison.OrdinalIgnoreCase);
        });
        return Task.FromResult(result);
    }

    public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = _access.Execute(connection =>
        {
            if (!TableExists(connection, SqliteBootstrapSchema.MetadataTableName))
            {
                return null;
            }

            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT key, value FROM {SqliteBootstrapSchema.MetadataTableName} WHERE key IN ($format, $created);";
            command.Parameters.AddWithValue("$format", ApplicationBootstrapper.MetadataKeyDatabaseFormatVersion);
            command.Parameters.AddWithValue("$created", ApplicationBootstrapper.MetadataKeyCreatedAtUtc);

            int? formatVersion = null;
            string? createdAtRaw = null;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);
                if (key == ApplicationBootstrapper.MetadataKeyDatabaseFormatVersion && int.TryParse(value, out var parsed))
                {
                    formatVersion = parsed;
                }
                else if (key == ApplicationBootstrapper.MetadataKeyCreatedAtUtc)
                {
                    createdAtRaw = value;
                }
            }

            if (formatVersion is null || createdAtRaw is null
                || !DateTimeOffset.TryParse(createdAtRaw, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var createdAt))
            {
                return null;
            }

            return new BootstrapMetadata { DatabaseFormatVersion = formatVersion.Value, CreatedAtUtc = createdAt };
        });
        return Task.FromResult(metadata);
    }

    public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _access.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                CREATE TABLE IF NOT EXISTS {SqliteBootstrapSchema.MetadataTableName} (
                    key   TEXT NOT NULL PRIMARY KEY,
                    value TEXT NOT NULL
                );
                INSERT OR REPLACE INTO {SqliteBootstrapSchema.MetadataTableName} (key, value) VALUES
                    ('{ApplicationBootstrapper.MetadataKeyDatabaseFormatVersion}', $format),
                    ('{ApplicationBootstrapper.MetadataKeyCreatedAtUtc}', $created);
                """;
            command.Parameters.AddWithValue("$format", metadata.DatabaseFormatVersion.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$created", metadata.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        });
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // 统一协调关闭（CD-01-F1）：锁内翻转状态 → 在飞操作（持同一锁）退出 →
        // 连接释放；之后的进入以 ObjectDisposedException fail-closed。
        return _access.DisposeAsync();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }
}

/// <summary>按文件路径创建包数据库访问实例。</summary>
public sealed class SqlitePackageDatabaseFactory : IPackageDatabaseFactory
{
    public IPackageDatabase CreateForFile(string databasePath) => new SqlitePackageDatabase(databasePath);
}
