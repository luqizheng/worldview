using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;

namespace WorldView.Application.Settings;

/// <summary>Summary of one Project Settings startup reconciliation pass.</summary>
public sealed record SettingsDocumentRecoveryReport
{
    public required int VerifiedDocumentCount { get; init; }
    public required int CleanedStagingFileCount { get; init; }
    public required IReadOnlyList<string> CollectedOrphanContentHashes { get; init; }
}

/// <summary>
/// Startup recovery for Project and Map Settings. Recovery runs after migrations and identity
/// loading, but before the Project session is exposed as Ready.
/// </summary>
public interface IProjectSettingsDocumentRecovery
{
    Task<SettingsDocumentRecoveryReport> ReconcileAsync(
        IPackageDatabase database,
        string packageRoot,
        Guid projectId,
        CancellationToken cancellationToken = default);
}
