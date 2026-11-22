using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>Results of one authoritative database-to-filesystem integrity pass.</summary>
public sealed record FieldPayloadIntegrityReport
{
    public required IReadOnlyList<Guid> MissingPayloadIds { get; init; }
    public required IReadOnlyList<Guid> CorruptPayloadIds { get; init; }
    public required IReadOnlyList<string> OrphanContentHashes { get; init; }
    public required int VerifiedPayloadCount { get; init; }
    public int CleanedStagingFileCount { get; init; }
}

/// <summary>
/// Reconciles immutable payload metadata with actual bytes. Orphans are reported separately;
/// collection is an explicit second operation and always takes a fresh database reference scan.
/// </summary>
public sealed class FieldPayloadIntegrityReconciler
{
    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;

    public FieldPayloadIntegrityReconciler(ICoreDomainDatabase database, IFieldPayloadFileStore fileStore)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    public async Task<FieldPayloadIntegrityReport> ReconcileAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payloads = _database.ListFieldPayloads(projectId);
        var missing = new List<Guid>();
        var corrupt = new List<Guid>();
        var verified = 0;

        foreach (var payload in payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await VerifyAsync(payload, cancellationToken).ConfigureAwait(false);
            if (state != payload.IntegrityState)
                _database.SetFieldPayloadIntegrityState(projectId, payload.FieldPayloadId, state);

            switch (state)
            {
                case FieldPayloadIntegrityState.Verified:
                    verified++;
                    break;
                case FieldPayloadIntegrityState.Missing:
                    missing.Add(payload.FieldPayloadId);
                    break;
                case FieldPayloadIntegrityState.Corrupt:
                    corrupt.Add(payload.FieldPayloadId);
                    break;
            }
        }

        var referencedHashes = payloads
            .Where(x => x.StorageKind == FieldPayloadStorageKind.ManagedFile)
            .Select(x => x.ContentHash)
            .ToHashSet(StringComparer.Ordinal);
        var orphans = _fileStore.ListContentHashes()
            .Where(hash => !referencedHashes.Contains(hash))
            .ToArray();

        return new FieldPayloadIntegrityReport
        {
            MissingPayloadIds = missing,
            CorruptPayloadIds = corrupt,
            OrphanContentHashes = orphans,
            VerifiedPayloadCount = verified,
        };
    }

    /// <summary>
    /// Reconciles first, then performs a fresh authoritative reference scan before deleting
    /// unreferenced content-addressed files. Referenced files are retained regardless of their
    /// integrity state; staging files are outside the enumerated hash namespace.
    /// </summary>
    public async Task<IReadOnlyList<string>> CollectOrphanFilesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        _ = await ReconcileAsync(projectId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var referencedHashes = _database.ListFieldPayloads(projectId)
            .Where(x => x.StorageKind == FieldPayloadStorageKind.ManagedFile)
            .Select(x => x.ContentHash)
            .ToHashSet(StringComparer.Ordinal);

        var deleted = new List<string>();
        foreach (var hash in _fileStore.ListContentHashes())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!referencedHashes.Contains(hash) && _fileStore.Delete(hash))
                deleted.Add(hash);
        }
        return deleted;
    }

    private async Task<FieldPayloadIntegrityState> VerifyAsync(
        FieldPayloadRecord payload,
        CancellationToken cancellationToken)
    {
        if (payload.StorageKind == FieldPayloadStorageKind.SqliteBlob)
        {
            try
            {
                var bytes = _database.ReadFieldPayloadBlob(payload.ProjectId, payload.FieldRevisionId);
                if (bytes is null)
                    return FieldPayloadIntegrityState.Missing;
                if (bytes.LongLength != payload.ByteLength)
                    return FieldPayloadIntegrityState.Corrupt;
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                return string.Equals(hash, payload.ContentHash, StringComparison.Ordinal)
                    ? FieldPayloadIntegrityState.Verified
                    : FieldPayloadIntegrityState.Corrupt;
            }
            catch (CoreDomainDataException)
            {
                return FieldPayloadIntegrityState.Corrupt;
            }
        }

        if (payload.StorageKind != FieldPayloadStorageKind.ManagedFile)
            return FieldPayloadIntegrityState.Corrupt;

        try
        {
            _ = FieldPayloadRules.NormalizeManagedPath(payload.RelativePath, payload.ContentHash);
            if (!_fileStore.Exists(payload.ContentHash))
                return FieldPayloadIntegrityState.Missing;

            await using var stream = await _fileStore.OpenReadAsync(
                payload.ContentHash, cancellationToken).ConfigureAwait(false);
            if (stream is null)
                return FieldPayloadIntegrityState.Missing;

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long byteLength = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                    break;
                hash.AppendData(buffer, 0, read);
                byteLength = checked(byteLength + read);
            }

            var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            return byteLength == payload.ByteLength
                && string.Equals(actualHash, payload.ContentHash, StringComparison.Ordinal)
                    ? FieldPayloadIntegrityState.Verified
                    : FieldPayloadIntegrityState.Corrupt;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or CoreDomainDataException)
        {
            return FieldPayloadIntegrityState.Corrupt;
        }
    }
}
