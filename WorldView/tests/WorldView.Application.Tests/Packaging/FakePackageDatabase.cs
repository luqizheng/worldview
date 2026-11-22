using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;

namespace WorldView.Application.Tests;

/// <summary>包数据库测试替身：内存元数据/ledger，可注入失败与销毁顺序记录。</summary>
public sealed class FakePackageDatabase : IPackageDatabase
{
    private readonly object _gate = new();
    private bool _opened;
    private bool _ledgerTableExists;

    public FakePackageDatabase(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    /// <summary>操作调用序列（含 Dispose），用于断言逆序释放。</summary>
    public List<string> CallLog { get; } = new();

    /// <summary>与 Asset Store 共享的调用序列（用于断言释放顺序）。</summary>
    public List<string>? SharedLog { get; set; }

    /// <summary>失败注入：下次指定操作抛出异常。</summary>
    public Func<string, Exception?>? FailureHook { get; set; }

    public Func<CancellationToken, Task>? DelayHook { get; set; }

    public BootstrapMetadata? Metadata { get; set; }

    public List<MigrationLedgerEntry> Ledger { get; } = new();

    public List<ModuleMigration> ExecutedMigrations { get; } = new();

    public bool IsOpened => _opened;

    private void Record(string operation)
    {
        lock (_gate)
        {
            CallLog.Add(operation);
            SharedLog?.Add(operation);
            if (FailureHook is { } hook && hook(operation) is { } failure)
            {
                throw failure;
            }
        }
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DelayHook is not null)
        {
            return DelayHook(cancellationToken).ContinueWith(_ =>
            {
                Record("Open");
                _opened = true;
            });
        }

        Record("Open");
        _opened = true;
        return Task.CompletedTask;
    }

    public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        Record("IntegrityCheck");
        return Task.FromResult(_opened);
    }

    public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default)
    {
        Record("ReadMetadata");
        return Task.FromResult(Metadata);
    }

    public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
    {
        Record("WriteMetadata");
        Metadata = metadata;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MigrationLedgerEntry>> ReadLedgerAsync(CancellationToken cancellationToken = default)
    {
        Record("ReadLedger");
        IReadOnlyList<MigrationLedgerEntry> snapshot;
        lock (Ledger)
        {
            snapshot = Ledger.ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<bool> LedgerTableExistsAsync(CancellationToken cancellationToken = default)
    {
        Record("LedgerTableExists");
        return Task.FromResult(_ledgerTableExists);
    }

    public Task EnsureLedgerTableAsync(CancellationToken cancellationToken = default)
    {
        Record("EnsureLedgerTable");
        _ledgerTableExists = true;
        return Task.CompletedTask;
    }

    public Task ExecuteMigrationAsync(ModuleMigration migration, CancellationToken cancellationToken = default)
    {
        Record($"ExecuteMigration:{migration.ModuleId}/{migration.MigrationId}");
        ExecutedMigrations.Add(migration);
        lock (Ledger)
        {
            Ledger.Add(new MigrationLedgerEntry
            {
                ModuleId = migration.ModuleId,
                MigrationId = migration.MigrationId,
                Checksum = migration.Checksum,
                AppliedAtUtcMs = 42,
            });
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Record("Database:Dispose");
        return ValueTask.CompletedTask;
    }
}

/// <summary>按路径缓存实例的假数据库工厂（同一数据库文件路径 → 同一替身，便于断言隔离）。</summary>
public sealed class FakePackageDatabaseFactory : IPackageDatabaseFactory
{
    private readonly Dictionary<string, FakePackageDatabase> _instances = new(StringComparer.Ordinal);

    public IReadOnlyCollection<FakePackageDatabase> Instances => _instances.Values;

    public IPackageDatabase CreateForFile(string databasePath)
    {
        var key = System.IO.Path.GetFullPath(databasePath);
        if (!_instances.TryGetValue(key, out var instance))
        {
            instance = new FakePackageDatabase(key);
            _instances[key] = instance;
        }

        return instance;
    }
}

/// <summary>假迁移来源。</summary>
public sealed class FakeMigrationSource(params WorldView.Application.Bootstrap.ModuleMigration[] migrations) : IMigrationSource
{
    public IReadOnlyList<ModuleMigration> DiscoverMigrations() => migrations;
}

/// <summary>假项目身份读取器（仅限单元测试）。</summary>
public sealed class FakeProjectIdentityReader : IProjectIdentityReader
{
    public ProjectIdentityReadResult Result { get; set; } =
        ProjectIdentityReadResult.Ok(new ProjectIdentity { ProjectId = Guid.NewGuid(), DisplayName = "测试项目" });

    public Func<FakePackageDatabase, CancellationToken, Task>? DelayHook { get; set; }

    public List<IPackageDatabase> ReceivedDatabases { get; } = new();

    public async Task<ProjectIdentityReadResult> ReadAsync(IPackageDatabase database, CancellationToken cancellationToken = default)
    {
        ReceivedDatabases.Add(database);
        if (DelayHook is not null)
        {
            await DelayHook((FakePackageDatabase)database, cancellationToken);
        }

        return Result;
    }
}
