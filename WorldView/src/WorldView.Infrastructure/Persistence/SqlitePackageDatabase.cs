using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;

namespace WorldView.Infrastructure.Persistence;

/// <summary>
/// 供 Application Bootstrap 使用的物理 DDL（应用拥有的元数据，非领域 schema）：
/// - schema_migration 迁移 ledger（列名对齐 P2-09-11 V0.11 §7）；
/// - wv_bootstrap_metadata 键值元数据（数据库格式版本、创建时间）。
/// Core Domain / AI Assistant 领域表绝不在此创建（P2-09-13 §2）。
/// </summary>
internal static class SqliteBootstrapSchema
{
    public const string LedgerTableName = "schema_migration";
    public const string MetadataTableName = "wv_bootstrap_metadata";

    public const string CreateLedgerTableSql =
        """
        CREATE TABLE IF NOT EXISTS schema_migration (
            module_id        TEXT    NOT NULL,
            migration_id     TEXT    NOT NULL,
            checksum         TEXT    NOT NULL,
            applied_at_utc_ms INTEGER NOT NULL,
            min_app_version  TEXT    NULL,
            max_app_version  TEXT    NULL,
            PRIMARY KEY (module_id, migration_id)
        )
        """;

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
/// 完全确定；每个连接显式启用外键（PRAGMA foreign_keys=ON，对齐 P2-09-12 TX-03）。
/// 单实例串行使用：同一会话内所有操作经由本实例顺序执行（V1 单写者约束）。
/// </summary>
public sealed class SqlitePackageDatabase : IPackageDatabase
{
    private readonly object _gate = new();
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqlitePackageDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径缺失。", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is not null)
            {
                return Task.CompletedTask;
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = DatabasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            };
            var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            using (var fk = connection.CreateCommand())
            {
                fk.CommandText = "PRAGMA foreign_keys = ON;";
                fk.ExecuteNonQuery();
            }

            _connection = connection;
            return Task.CompletedTask;
        }
    }

    public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteScalarAsync("PRAGMA quick_check;", cancellationToken, static scalar =>
            scalar is string result && result.Equals("ok", StringComparison.OrdinalIgnoreCase));
    }

    public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null || !TableExists(_connection, SqliteBootstrapSchema.MetadataTableName))
            {
                return Task.FromResult<BootstrapMetadata?>(null);
            }

            using var command = _connection.CreateCommand();
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
                return Task.FromResult<BootstrapMetadata?>(null);
            }

            BootstrapMetadata metadata = new() { DatabaseFormatVersion = formatVersion.Value, CreatedAtUtc = createdAt };
            return Task.FromResult<BootstrapMetadata?>(metadata);
        }
    }

    public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteNonQueryAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {SqliteBootstrapSchema.MetadataTableName} (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            INSERT OR REPLACE INTO {SqliteBootstrapSchema.MetadataTableName} (key, value) VALUES
                ('{ApplicationBootstrapper.MetadataKeyDatabaseFormatVersion}', $format),
                ('{ApplicationBootstrapper.MetadataKeyCreatedAtUtc}', $created);
            """,
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("$format", metadata.DatabaseFormatVersion.ToString(CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$created", metadata.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            });
    }

    public Task<IReadOnlyList<MigrationLedgerEntry>> ReadLedgerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null || !TableExists(_connection, SqliteBootstrapSchema.LedgerTableName))
            {
                return Task.FromResult<IReadOnlyList<MigrationLedgerEntry>>(Array.Empty<MigrationLedgerEntry>());
            }

            using var command = _connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT module_id, migration_id, checksum, applied_at_utc_ms, min_app_version, max_app_version
                FROM {SqliteBootstrapSchema.LedgerTableName}
                ORDER BY applied_at_utc_ms, module_id, migration_id;
                """;
            using var reader = command.ExecuteReader();
            var entries = new List<MigrationLedgerEntry>();
            while (reader.Read())
            {
                entries.Add(new MigrationLedgerEntry
                {
                    ModuleId = reader.GetString(0),
                    MigrationId = reader.GetString(1),
                    Checksum = reader.GetString(2),
                    AppliedAtUtcMs = reader.GetInt64(3),
                    MinAppVersion = reader.IsDBNull(4) ? null : reader.GetString(4),
                    MaxAppVersion = reader.IsDBNull(5) ? null : reader.GetString(5),
                });
            }

            return Task.FromResult<IReadOnlyList<MigrationLedgerEntry>>(entries);
        }
    }

    public Task<bool> LedgerTableExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(TableExists(_connection, SqliteBootstrapSchema.LedgerTableName));
        }
    }

    public Task EnsureLedgerTableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ExecuteNonQueryAsync(SqliteBootstrapSchema.CreateLedgerTableSql, cancellationToken);
    }

    public async Task ExecuteMigrationAsync(ModuleMigration migration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                throw new InvalidOperationException("数据库未打开。");
            }
        }

        // 单事务：迁移脚本 + ledger 插入（P2-09-11 §7）。失败整体回滚。
        await using var transaction = await _connection!.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var scriptCommand = _connection.CreateCommand();
            scriptCommand.Transaction = (SqliteTransaction)transaction;
            scriptCommand.CommandText = migration.SqlScript;
            await scriptCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await using var ledgerCommand = _connection.CreateCommand();
            ledgerCommand.Transaction = (SqliteTransaction)transaction;
            ledgerCommand.CommandText =
                $"""
                INSERT INTO {SqliteBootstrapSchema.LedgerTableName}
                    (module_id, migration_id, checksum, applied_at_utc_ms, min_app_version, max_app_version)
                VALUES ($module, $migration, $checksum, $appliedAt, $minVersion, $maxVersion)
                """;
            ledgerCommand.Parameters.AddWithValue("$module", migration.ModuleId);
            ledgerCommand.Parameters.AddWithValue("$migration", migration.MigrationId);
            ledgerCommand.Parameters.AddWithValue("$checksum", migration.Checksum);
            ledgerCommand.Parameters.AddWithValue("$appliedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            ledgerCommand.Parameters.AddWithValue("$minVersion", (object?)migration.MinAppVersion ?? DBNull.Value);
            ledgerCommand.Parameters.AddWithValue("$maxVersion", (object?)migration.MaxAppVersion ?? DBNull.Value);
            await ledgerCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection? connection;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            connection = _connection;
            _connection = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }

    // 说明：Microsoft.Data.Sqlite 的同步 ADO.NET 调用在单连接串行模型下使用；
    // 这里以同步执行保证句柄/事务行为确定，方法仍以 Task 形式对齐异步契约。

    private Task<T?> ExecuteScalarAsync<T>(
        string sql,
        CancellationToken cancellationToken,
        Func<object?, T?> project)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                throw new InvalidOperationException("数据库未打开。");
            }

            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            return Task.FromResult(project(command.ExecuteScalar()));
        }
    }

    private Task ExecuteNonQueryAsync(
        string sql,
        CancellationToken cancellationToken,
        Action<SqliteCommand>? configure = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection is null)
            {
                throw new InvalidOperationException("数据库未打开。");
            }

            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            configure?.Invoke(command);
            command.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }
}

/// <summary>按文件路径创建包数据库访问实例。</summary>
public sealed class SqlitePackageDatabaseFactory : IPackageDatabaseFactory
{
    public IPackageDatabase CreateForFile(string databasePath) => new SqlitePackageDatabase(databasePath);
}
