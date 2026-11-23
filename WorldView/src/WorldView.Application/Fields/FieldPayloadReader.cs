using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>
/// The single read path for immutable Field Revision bytes. SQLite owns the storage metadata;
/// BLOB bytes are verified by the database adapter, while ManagedFile bytes are verified
/// incrementally before a fresh stream is returned to the caller.
/// </summary>
public sealed class FieldPayloadReader
{
    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;

    public FieldPayloadReader(ICoreDomainDatabase database, IFieldPayloadFileStore fileStore)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    /// <summary>
    /// Opens a fully verified payload. Returns null only when the revision has no payload
    /// metadata; missing/corrupt bytes fail closed. The returned stream is owned by the caller.
    /// Managed files are streamed once for verification and reopened for consumption, avoiding
    /// loading large tile payloads into memory.
    /// </summary>
    public async Task<Stream?> OpenReadAsync(
        Guid projectId,
        Guid fieldRevisionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = _database.FindFieldPayload(projectId, fieldRevisionId);
        if (payload is null)
            return null;

        if (payload.IntegrityState != FieldPayloadIntegrityState.Verified)
            throw new CoreDomainDataException(
                $"Field Payload {payload.FieldPayloadId:D} is not verified (state: {payload.IntegrityState}).");

        switch (payload.StorageKind)
        {
            case FieldPayloadStorageKind.SqliteBlob:
            {
                var bytes = _database.ReadFieldPayloadBlob(projectId, fieldRevisionId);
                if (bytes is null)
                    throw new CoreDomainDataException("Registered SQLite BLOB Field Payload is missing.");
                if (bytes.LongLength != payload.ByteLength)
                    throw new CoreDomainDataException("SQLite BLOB Field Payload length does not match its metadata.");
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(hash, payload.ContentHash, StringComparison.Ordinal))
                    throw new CoreDomainDataException("SQLite BLOB Field Payload hash does not match its metadata.");
                return new MemoryStream(bytes, writable: false);
            }

            case FieldPayloadStorageKind.ManagedFile:
            {
                var expectedPath = FieldPayloadRules.NormalizeManagedPath(payload.RelativePath, payload.ContentHash);
                if (!string.Equals(expectedPath, payload.RelativePath, StringComparison.Ordinal))
                    throw new CoreDomainDataException("Managed Field Payload path is not canonical.");

                await using (var verificationStream = await _fileStore.OpenReadAsync(
                    payload.ContentHash, cancellationToken).ConfigureAwait(false)
                    ?? throw new CoreDomainDataException("Registered ManagedFile Field Payload is missing."))
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[128 * 1024];
                    long byteLength = 0;
                    while (true)
                    {
                        var read = await verificationStream.ReadAsync(
                            buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                            break;
                        hash.AppendData(buffer, 0, read);
                        byteLength = checked(byteLength + read);
                    }

                    var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                    if (byteLength != payload.ByteLength
                        || !string.Equals(actualHash, payload.ContentHash, StringComparison.Ordinal))
                        throw new CoreDomainDataException("Managed Field Payload length or SHA-256 verification failed.");
                }

                return await _fileStore.OpenReadAsync(payload.ContentHash, cancellationToken).ConfigureAwait(false)
                    ?? throw new CoreDomainDataException("Managed Field Payload disappeared after verification.");
            }

            default:
                throw new CoreDomainDataException("Field Payload contains an unknown storage kind.");
        }
    }
}
