using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;

namespace WorldView.Application.Bootstrap;

/// <summary>
/// Application Bootstrap 编排器（AF-01 §11 / P2-09-11 §7）：
/// 拥有共享 schema_migration ledger、bootstrap 元数据与模块迁移的排序执行；
/// 不拥有、不创建任何领域迁移内容。所有校验失败一律 fail-closed。
/// </summary>
public sealed class ApplicationBootstrapper
{
    public const string MetadataKeyDatabaseFormatVersion = "database_format_version";
    public const string MetadataKeyCreatedAtUtc = "created_at_utc";

    private readonly IReadOnlyList<IMigrationSource> _migrationSources;
    private readonly IAppLog? _log;

    public ApplicationBootstrapper(IEnumerable<IMigrationSource> migrationSources, IAppLog? log = null)
    {
        _migrationSources = migrationSources.ToArray();
        _log = log;
    }

    /// <summary>
    /// 新建数据库初始化：创建 ledger + bootstrap 元数据，应用全部已登记迁移。
    /// 每个迁移与 ledger 插入在同一事务内执行（P2-09-11 §7）。
    /// </summary>
    public async Task<BootstrapResult> InitializeNewDatabaseAsync(
        IPackageDatabase database,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await database.EnsureLedgerTableAsync(cancellationToken).ConfigureAwait(false);
            await database.WriteMetadataAsync(
                new BootstrapMetadata
                {
                    DatabaseFormatVersion = ProjectPackageFormat.SupportedDatabaseFormatVersion,
                    CreatedAtUtc = createdAtUtc,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error("Bootstrap", "初始化数据库 bootstrap 结构失败。", ex);
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(PackageIssueCode.InternalError, "bootstrap-init", "初始化 bootstrap 元数据/ledger 失败。"),
            });
        }

        return await ApplyPendingMigrationsAsync(database, applied: new Dictionary<(string, string), MigrationLedgerEntry>(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 既有数据库校验：元数据存在且版本受支持、ledger 存在、已应用迁移与登记迁移一致
    /// （未知迁移/校验和不匹配 fail-closed），然后应用待执行迁移。
    /// </summary>
    public async Task<BootstrapResult> ValidateExistingDatabaseAsync(
        IPackageDatabase database,
        CancellationToken cancellationToken = default)
    {
        var metadata = await database.ReadMetadataAsync(cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(PackageIssueCode.BootstrapMetadataMissing, "bootstrap", "bootstrap 元数据不可读（表缺失或损坏）。"),
            });
        }

        if (metadata.DatabaseFormatVersion > ProjectPackageFormat.SupportedDatabaseFormatVersion)
        {
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(
                    PackageIssueCode.DatabaseFormatUnsupported,
                    "bootstrap",
                    $"数据库格式版本 {metadata.DatabaseFormatVersion} 高于本应用支持版本 {ProjectPackageFormat.SupportedDatabaseFormatVersion}。"),
            });
        }

        var ledger = await database.ReadLedgerAsync(cancellationToken).ConfigureAwait(false);
        if (!(await database.LedgerTableExistsAsync(cancellationToken).ConfigureAwait(false)))
        {
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(PackageIssueCode.LedgerMissing, "bootstrap", "迁移 ledger 缺失。"),
            });
        }

        var applied = ledger.ToDictionary(e => (e.ModuleId, e.MigrationId));

        // 未知迁移：ledger 中存在但任何登记来源都没有 → fail-closed。
        var registered = _migrationSources.SelectMany(s => s.DiscoverMigrations())
            .ToDictionary(m => (m.ModuleId, m.MigrationId));
        var unknown = applied.Keys.Except(registered.Keys).ToList();
        if (unknown.Count > 0)
        {
            var first = unknown[0];
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(
                    PackageIssueCode.LedgerUnknownMigration,
                    "bootstrap",
                    $"ledger 存在未登记迁移 {first.Item1}/{first.Item2}（可能由更高版本写入），拒绝打开。"),
            });
        }

        // 校验和不匹配：已应用迁移与登记脚本不一致 → fail-closed。
        var mismatch = applied.FirstOrDefault(a =>
            registered.TryGetValue(a.Key, out var migration)
            && !string.Equals(migration.Checksum, a.Value.Checksum, StringComparison.Ordinal));
        if (!string.IsNullOrEmpty(mismatch.Key.Item1))
        {
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(
                    PackageIssueCode.LedgerChecksumMismatch,
                    "bootstrap",
                    $"迁移 {mismatch.Key.Item1}/{mismatch.Key.Item2} 的内容校验和与 ledger 记录不一致。"),
            });
        }

        return await ApplyPendingMigrationsAsync(database, applied, cancellationToken).ConfigureAwait(false);
    }

    private async Task<BootstrapResult> ApplyPendingMigrationsAsync(
        IPackageDatabase database,
        IReadOnlyDictionary<(string ModuleId, string MigrationId), MigrationLedgerEntry> applied,
        CancellationToken cancellationToken)
    {
        var pending = _migrationSources.SelectMany(s => s.DiscoverMigrations())
            .Where(m => !applied.ContainsKey((m.ModuleId, m.MigrationId)))
            .ToList();

        foreach (var migration in pending)
        {
            try
            {
                await database.ExecuteMigrationAsync(migration, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log?.Error("Bootstrap", $"迁移 {migration.ModuleId}/{migration.MigrationId} 执行失败。", ex);
                return BootstrapResult.Failed(new[]
                {
                    PackageIssue.Failure(
                        PackageIssueCode.InternalError,
                        "bootstrap-migrate",
                        $"迁移 {migration.ModuleId}/{migration.MigrationId} 执行失败，已回滚。"),
                });
            }
        }

        return BootstrapResult.Ok(pending);
    }
}
