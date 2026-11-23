using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Settings;

namespace WorldView.Infrastructure.Settings;

/// <summary>
/// Verifies every referenced historical settings revision and collects only unreferenced
/// content-addressed files after taking a fresh SQLite reference snapshot.
/// </summary>
public sealed class CoreDomainSettingsDocumentRecovery : IProjectSettingsDocumentRecovery
{
    public async Task<SettingsDocumentRecoveryReport> ReconcileAsync(
        IPackageDatabase database,
        string packageRoot,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var repository = new CoreDomainSettingsDocumentRepository(database);
        var fileStore = new SettingsDocumentFileStore(packageRoot);
        var cleanedStaging = fileStore.CleanupStagingFiles();

        var revisions = ReadAllRevisions(repository, projectId);
        var verified = 0;
        foreach (var revision in revisions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await VerifyRevisionAsync(fileStore, revision, projectId, cancellationToken).ConfigureAwait(false);
            verified++;
        }

        // Re-read every reference immediately before collection. Historical revisions count
        // as live references even when they are not the owner's current version.
        cancellationToken.ThrowIfCancellationRequested();
        var freshReferences = ReadAllRevisions(repository, projectId)
            .Select(x => x.ContentHash)
            .ToHashSet(StringComparer.Ordinal);

        var collected = new List<string>();
        foreach (var hash in fileStore.ListPublishedContentHashes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!freshReferences.Contains(hash) && fileStore.Delete(hash))
                collected.Add(hash);
        }

        return new SettingsDocumentRecoveryReport
        {
            VerifiedDocumentCount = verified,
            CleanedStagingFileCount = cleanedStaging,
            CollectedOrphanContentHashes = collected,
        };
    }

    private static IReadOnlyList<SettingsDocumentRevision> ReadAllRevisions(
        ISettingsDocumentRepository repository, Guid projectId)
    {
        return repository.ListProjectSettingsRevisions(projectId)
            .Concat(repository.ListMapSettingsRevisions(projectId))
            .ToArray();
    }

    private static async Task VerifyRevisionAsync(
        ISettingsDocumentFileStore fileStore,
        SettingsDocumentRevision revision,
        Guid expectedProjectId,
        CancellationToken cancellationToken)
    {
        if (revision.ProjectId != expectedProjectId
            || revision.SchemaVersion != SettingsDocumentFormat.InitialSchemaVersion
            || !string.Equals(revision.HashAlgorithm, "sha-256", StringComparison.Ordinal)
            || revision.ByteLength <= 0
            || revision.ByteLength > SettingsDocumentFormat.MaximumDocumentBytes
            || !string.Equals(revision.RelativePath,
                SettingsDocumentPaths.GetRelativePath(revision.ContentHash), StringComparison.Ordinal))
            throw new InvalidDataException("Settings revision metadata failed startup validation.");

        await using var input = await fileStore.OpenReadAsync(revision.ContentHash, cancellationToken)
            .ConfigureAwait(false);
        if (input is null)
            throw new FileNotFoundException("A referenced settings document is missing.", revision.RelativePath);

        using var output = new MemoryStream(checked((int)revision.ByteLength));
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long length = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            length = checked(length + read);
            if (length > revision.ByteLength || length > SettingsDocumentFormat.MaximumDocumentBytes)
                throw new InvalidDataException("Settings document exceeds its recorded byte length.");
            digest.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var actualHash = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        if (length != revision.ByteLength
            || !string.Equals(actualHash, revision.ContentHash, StringComparison.Ordinal))
            throw new InvalidDataException("A referenced settings document failed SHA-256 or length verification.");

        _ = SettingsDocument.Parse(output.ToArray(), expectedProjectId, revision.MapId);
    }
}
