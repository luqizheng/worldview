using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WorldView.Application.Settings;

/// <summary>
/// Application workflow coordinating immutable file publication with the authoritative
/// SQLite revision/current-reference transaction.
/// </summary>
public sealed class SettingsDocumentService
{
    private readonly ISettingsDocumentRepository _repository;
    private readonly ISettingsDocumentFileStore _fileStore;

    public SettingsDocumentService(
        ISettingsDocumentRepository repository,
        ISettingsDocumentFileStore fileStore)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
    }

    public async Task<SettingsDocumentRevision> SaveProjectAsync(
        Guid projectId, JObject settings, CancellationToken cancellationToken = default)
    {
        var document = SettingsDocument.ForProject(projectId, settings);
        var bytes = document.ToUtf8Bytes();
        using var input = new MemoryStream(bytes, writable: false);
        var file = await _fileStore.PutAsync(input, cancellationToken).ConfigureAwait(false);
        // A cancellation here intentionally leaves an unreferenced immutable file for recovery.
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.PublishProjectSettings(projectId, file,
            SettingsDocumentFormat.InitialSchemaVersion, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public async Task<SettingsDocumentRevision> SaveMapAsync(
        Guid projectId, Guid mapId, JObject settings, CancellationToken cancellationToken = default)
    {
        var document = SettingsDocument.ForMap(projectId, mapId, settings);
        var bytes = document.ToUtf8Bytes();
        using var input = new MemoryStream(bytes, writable: false);
        var file = await _fileStore.PutAsync(input, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return _repository.PublishMapSettings(projectId, mapId, file,
            SettingsDocumentFormat.InitialSchemaVersion, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    public Task<SettingsDocument?> LoadCurrentProjectAsync(
        Guid projectId, CancellationToken cancellationToken = default) =>
        LoadAsync(_repository.FindCurrentProjectSettings(projectId), projectId, null, cancellationToken);

    public Task<SettingsDocument?> LoadCurrentMapAsync(
        Guid projectId, Guid mapId, CancellationToken cancellationToken = default) =>
        LoadAsync(_repository.FindCurrentMapSettings(projectId, mapId), projectId, mapId, cancellationToken);

    private async Task<SettingsDocument?> LoadAsync(
        SettingsDocumentRevision? revision,
        Guid projectId,
        Guid? mapId,
        CancellationToken cancellationToken)
    {
        if (revision is null)
            return null;
        if (revision.ProjectId != projectId || revision.MapId != mapId
            || revision.SchemaVersion != SettingsDocumentFormat.InitialSchemaVersion
            || !string.Equals(revision.HashAlgorithm, "sha-256", StringComparison.Ordinal)
            || revision.ByteLength <= 0
            || revision.ByteLength > SettingsDocumentFormat.MaximumDocumentBytes
            || !SettingsDocumentPaths.TryResolvePublishedPath(
                _fileStore is null ? string.Empty : GetPackageRootNotAvailable(),
                revision.RelativePath, revision.ContentHash, out _))
        {
            // The store itself resolves only the canonical hash path. Metadata still has to
            // agree with the canonical path before the bytes are opened.
            if (revision.ByteLength <= 0 || revision.ByteLength > SettingsDocumentFormat.MaximumDocumentBytes
                || !string.Equals(revision.RelativePath,
                    SettingsDocumentPaths.GetRelativePath(revision.ContentHash), StringComparison.Ordinal))
                throw new InvalidDataException("Settings revision metadata is invalid or outside the V1 contract.");
        }

        await using var input = await _fileStore.OpenReadAsync(revision.ContentHash, cancellationToken)
            .ConfigureAwait(false);
        if (input is null)
            throw new FileNotFoundException("Referenced settings document bytes are missing.", revision.RelativePath);

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
                throw new InvalidDataException("Settings document exceeds its recorded or allowed byte length.");
            digest.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var hash = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        if (length != revision.ByteLength || !string.Equals(hash, revision.ContentHash, StringComparison.Ordinal))
            throw new InvalidDataException("Settings document length or SHA-256 does not match SQLite metadata.");

        return SettingsDocument.Parse(output.ToArray(), projectId, mapId);
    }

    // Kept separate to avoid exposing package paths through the Application storage port.
    // The file-store contract guarantees that OpenReadAsync resolves a validated content hash.
    private static string GetPackageRootNotAvailable() => string.Empty;
}
