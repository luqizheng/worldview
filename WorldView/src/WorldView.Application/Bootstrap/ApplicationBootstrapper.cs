using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;

namespace WorldView.Application.Bootstrap;

/// <summary>
/// Application package metadata initializer/validator.
/// Core Domain schema and migrations are owned exclusively by Infrastructure EF Core migrations.
/// This service does not create, inspect, or execute the retired SQL migration ledger.
/// </summary>
public sealed class ApplicationBootstrapper
{
    public const string MetadataKeyDatabaseFormatVersion = "database_format_version";
    public const string MetadataKeyCreatedAtUtc = "created_at_utc";

    private readonly IAppLog? _log;

    // The optional source parameter is temporarily retained for source compatibility inside
    // unreleased tests; it is intentionally ignored and no SQL migration source is executed.
    public ApplicationBootstrapper(IEnumerable<IMigrationSource>? migrationSources = null, IAppLog? log = null)
    {
        _ = migrationSources;
        _log = log;
    }

    public async Task<BootstrapResult> InitializeNewDatabaseAsync(
        IPackageDatabase database,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
            _log?.Error("Bootstrap", "初始化数据库包元数据失败。", ex);
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(PackageIssueCode.InternalError, "bootstrap-init", "初始化数据库包元数据失败。"),
            });
        }

        return BootstrapResult.Ok(Array.Empty<ModuleMigration>());
    }

    public async Task<BootstrapResult> ValidateExistingDatabaseAsync(
        IPackageDatabase database,
        CancellationToken cancellationToken = default)
    {
        BootstrapMetadata? metadata;
        try
        {
            metadata = await database.ReadMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.Error("Bootstrap", "读取数据库包元数据失败。", ex);
            return BootstrapResult.Failed(new[]
            {
                PackageIssue.Failure(PackageIssueCode.BootstrapMetadataMissing, "bootstrap", "bootstrap 元数据不可读。"),
            });
        }

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

        return BootstrapResult.Ok(Array.Empty<ModuleMigration>());
    }
}
