using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>
/// Coordinates the non-transactional filesystem write with the authoritative SQLite
/// Field Revision publication. A file published before a failed DB commit is deliberately
/// retained as an orphan; only a later DB-backed reconciliation may collect it.
/// </summary>
public sealed class FieldPayloadPublisher
{
    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;

    public FieldPayloadPublisher(ICoreDomainDatabase database, IFieldPayloadFileStore fileStore)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    /// <summary>
    /// Streams bytes to the content-addressed store, then atomically registers the verified
    /// payload metadata with its Field Revision and Dataset current-pointer update in SQLite.
    /// The input stream remains owned by the caller. On DB failure the published file is
    /// intentionally preserved for safe reconciliation; it is never eagerly deleted.
    /// </summary>
    public async Task<FieldRevisionRecord> PublishManagedFileRevisionAsync(
        PublishFieldRevisionPlan plan,
        Stream payloadContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(payloadContent);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = await _fileStore.PutAsync(payloadContent, cancellationToken).ConfigureAwait(false);
        var payload = plan.Payload with
        {
            StorageKind = FieldPayloadStorageKind.ManagedFile,
            HashAlgorithm = "sha-256",
            ByteLength = stored.ByteLength,
            ContentHash = stored.ContentHash,
            RelativePath = stored.RelativePath,
        };

        return FieldDatasetService.PublishRevision(_database, plan with
        {
            Payload = payload,
            PayloadBytes = null,
        });
    }
}
