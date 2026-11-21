using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Packaging;

namespace WorldView.Application.Bootstrap;

/// <summary>
/// Bootstrap 元数据：数据库格式版本与创建时间等 Application Bootstrap 拥有的元数据。
/// 不包含任何 Core Domain 项目身份或领域状态（manifest 不做第二身份权威，库内元数据同理）。
/// </summary>
public sealed record BootstrapMetadata
{
    /// <summary>数据库格式版本（当前支持 1）。</summary>
    public required int DatabaseFormatVersion { get; init; }

    /// <summary>数据库创建时间（UTC）。</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

/// <summary>
/// 一条已应用迁移的 ledger 记录。列名对齐 P2-09-11 V0.11 §7 的
/// schema_migration(module_id, migration_id, checksum, applied_at_utc_ms,
/// min_app_version, max_app_version, PRIMARY KEY(module_id, migration_id))。
/// </summary>
public sealed record MigrationLedgerEntry
{
    public required string ModuleId { get; init; }

    public required string MigrationId { get; init; }

    public required string Checksum { get; init; }

    /// <summary>应用时间（UTC，毫秒精度）。</summary>
    public required long AppliedAtUtcMs { get; init; }

    public string? MinAppVersion { get; init; }

    public string? MaxAppVersion { get; init; }
}

/// <summary>
/// 一个模块迁移的声明（module-owned migration）。模块（Core Domain / AI Assistant）
/// 拥有自己的迁移内容；Application Bootstrap 只负责排序与执行（AF-01 §11）。
/// </summary>
public sealed record ModuleMigration
{
    /// <summary>固定模块命名空间，如 "core-domain"、"ai-assistant"、"application"。</summary>
    public required string ModuleId { get; init; }

    public required string MigrationId { get; init; }

    /// <summary>脚本内容的 SHA-256（十六进制小写）；与 ledger 中已应用记录比对实现 fail-closed。</summary>
    public required string Checksum { get; init; }

    /// <summary>SQL 脚本内容。</summary>
    public required string SqlScript { get; init; }

    public string? MinAppVersion { get; init; }

    public string? MaxAppVersion { get; init; }
}

/// <summary>迁移来源：模块向 Application Bootstrap 登记自己的迁移（按登记顺序执行）。</summary>
public interface IMigrationSource
{
    System.Collections.Generic.IReadOnlyList<ModuleMigration> DiscoverMigrations();
}

/// <summary>
/// Application Bootstrap 编排结果。
/// </summary>
public sealed class BootstrapResult
{
    private readonly List<PackageIssue> _issues = new();

    public bool Success => _issues.Count == 0;

    public IReadOnlyList<PackageIssue> Issues => _issues;

    public IReadOnlyList<ModuleMigration> AppliedMigrations { get; private init; }
        = Array.Empty<ModuleMigration>();

    public static BootstrapResult Ok(IReadOnlyList<ModuleMigration> applied) => new()
    {
        AppliedMigrations = applied,
    };

    public static BootstrapResult Failed(IEnumerable<PackageIssue> issues)
    {
        var result = new BootstrapResult();
        result._issues.AddRange(issues);
        return result;
    }
}
