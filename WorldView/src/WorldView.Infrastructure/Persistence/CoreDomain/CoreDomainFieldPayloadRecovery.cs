using System;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Fields;
using WorldView.Infrastructure.Assets;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>Creates the package-bound Core Domain and managed-file adapters for startup reconciliation.</summary>
public sealed class CoreDomainFieldPayloadRecovery : IProjectFieldPayloadRecovery
{
    public Task<FieldPayloadIntegrityReport> ReconcileAsync(
        IPackageDatabase database,
        string packageRoot,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();
        var core = new SqliteCoreDomainDatabase(database);
        var fileStore = new FieldPayloadFileStore(packageRoot);
        var cleanedStagingFiles = fileStore.CleanupStagingFiles();
        var reconciler = new FieldPayloadIntegrityReconciler(core, fileStore);
        return ReconcileAndCollectAsync(reconciler, projectId, cleanedStagingFiles, cancellationToken);
    }

    private static async Task<FieldPayloadIntegrityReport> ReconcileAndCollectAsync(
        FieldPayloadIntegrityReconciler reconciler,
        Guid projectId,
        int cleanedStagingFiles,
        CancellationToken cancellationToken)
    {
        var report = await reconciler.ReconcileAsync(projectId, cancellationToken).ConfigureAwait(false);
        // Reconciliation has established the complete authoritative reference set. The second
        // operation rescans it immediately before deleting only unreferenced content-addressed files.
        var collectedOrphans = await reconciler.CollectOrphanFilesAsync(projectId, cancellationToken).ConfigureAwait(false);
        return report with
        {
            CleanedStagingFileCount = cleanedStagingFiles,
            CollectedOrphanContentHashes = collectedOrphans,
        };
    }
}
