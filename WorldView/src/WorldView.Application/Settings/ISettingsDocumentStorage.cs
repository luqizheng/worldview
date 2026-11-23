using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.Application.Settings;

/// <summary>
/// Package-bound immutable settings byte store. It publishes verified bytes but never mutates
/// SQLite; the repository commits the metadata/current pointer after file publication.
/// </summary>
public interface ISettingsDocumentFileStore
{
    Task<SettingsDocumentFilePutResult> PutAsync(Stream content, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default);
    IReadOnlyList<string> ListPublishedContentHashes();
    int CleanupStagingFiles();
    bool Delete(string contentHash);
}

public sealed record SettingsDocumentFilePutResult
{
    public required string ContentHash { get; init; }
    public required long ByteLength { get; init; }
    public required string RelativePath { get; init; }
}

/// <summary>One immutable settings revision's SQLite metadata.</summary>
public sealed record SettingsDocumentRevision
{
    public required Guid RevisionId { get; init; }
    public required Guid ProjectId { get; init; }
    public Guid? MapId { get; init; }
    public required long Version { get; init; }
    public required int SchemaVersion { get; init; }
    public required string RelativePath { get; init; }
    public required string HashAlgorithm { get; init; }
    public required string ContentHash { get; init; }
    public required long ByteLength { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>
/// SQLite metadata port. Publish operations insert immutable revision metadata and update the
/// owner's current-reference row in one transaction.
/// </summary>
public interface ISettingsDocumentRepository
{
    SettingsDocumentRevision? FindCurrentProjectSettings(Guid projectId);
    SettingsDocumentRevision? FindCurrentMapSettings(Guid projectId, Guid mapId);
    IReadOnlyList<SettingsDocumentRevision> ListProjectSettingsRevisions(Guid projectId);
    IReadOnlyList<SettingsDocumentRevision> ListMapSettingsRevisions(Guid projectId);
    SettingsDocumentRevision PublishProjectSettings(Guid projectId, SettingsDocumentFilePutResult file, int schemaVersion, long createdAtUtcMs);
    SettingsDocumentRevision PublishMapSettings(Guid projectId, Guid mapId, SettingsDocumentFilePutResult file, int schemaVersion, long createdAtUtcMs);
}
