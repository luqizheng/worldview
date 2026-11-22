using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Abstractions;

/// <summary>
/// Content-addressed managed-file storage for Field payload bytes. This port deliberately
/// does not mutate Core Domain rows: a caller must register the returned immutable metadata
/// in the same publication workflow as the Field Revision. Unreferenced files are retained
/// until a database-backed reconciliation pass proves they are safe to collect.
/// </summary>
public interface IFieldPayloadFileStore
{
    /// <summary>Absolute package-managed Field Payload root.</summary>
    string RootPath { get; }

    /// <summary>
    /// Streams exact raw bytes into a same-directory staging file, flushes and verifies
    /// SHA-256/length, then atomically publishes the content-addressed final file.
    /// The input stream remains owned by the caller and need not be seekable.
    /// </summary>
    Task<FieldPayloadFilePutResult> PutAsync(Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens a published payload by its lowercase SHA-256; absent content returns null.</summary>
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default);

    /// <summary>Checks whether a content-addressed payload file exists.</summary>
    bool Exists(string contentHash);
}

/// <summary>Verified result of publishing a managed Field Payload file.</summary>
public sealed record FieldPayloadFilePutResult
{
    public required string ContentHash { get; init; }
    public required long ByteLength { get; init; }
    public required string RelativePath { get; init; }
}
