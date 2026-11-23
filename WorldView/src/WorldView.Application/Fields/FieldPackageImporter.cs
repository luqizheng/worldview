using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>
/// One Field Package import request. The package is an interchange artifact; the importer
/// validates it fully (binary format + owner context) before any persistence content is
/// prepared, then publishes exactly one immutable Field Revision with its dedicated payload.
/// </summary>
public sealed record ImportFieldPackagePlan
{
    public required Guid ProjectId { get; init; }
    public required Guid MapId { get; init; }
    /// <summary>
    /// Dataset identity for the imported Field. Null allocates a fresh identity (first import
    /// into the Map/Field); importing into an existing Dataset requires its exact existing ID.
    /// </summary>
    public Guid? FieldDatasetId { get; init; }
    /// <summary>Null allocates a fresh immutable revision identity.</summary>
    public Guid? FieldRevisionId { get; init; }
    /// <summary>
    /// Current-revision pin observed by the caller; null only for the Dataset's first revision.
    /// A stale pin fails the publication CAS and the import leaves no published revision.
    /// </summary>
    public Guid? ExpectedCurrentFieldRevisionId { get; init; }
    /// <summary>Explicit physical placement decision; no implicit size threshold exists.</summary>
    public required FieldPayloadStorageKind StorageKind { get; init; }
    /// <summary>Import provenance JSON (canonicalized by the publication authority).</summary>
    public required string ProvenanceJson { get; init; }
    public required long CreatedAtUtcMs { get; init; }
}

/// <summary>Authoritative result of one completed Field Package import.</summary>
public sealed record FieldPackageImportResult
{
    public required FieldRevisionRecord Revision { get; init; }
    /// <summary>Lowercase SHA-256 of the exact imported package bytes.</summary>
    public required string PackageSha256 { get; init; }
    public required long PackageByteLength { get; init; }
}

/// <summary>
/// Imports a WVFLDPK 1.0 Field Package into the internal Field Dataset / Revision domain
/// model. The package never leaks into the domain model: bytes are stored as an opaque
/// verified payload and only the typed manifest projection is persisted semantically.
/// Ordering is validate-first: binary open, owner-context preflight, and only then payload
/// staging/publication, so an invalid package can never stage bytes or create a revision.
///
/// Failure strategy per storage kind:
/// - <see cref="FieldPayloadStorageKind.SqliteBlob"/>: payload row, revision and Dataset CAS
///   advance in one SQLite transaction; failure rolls back everything and creates no artifacts.
/// - <see cref="FieldPayloadStorageKind.ManagedFile"/>: bytes stream to the content-addressed
///   store first (staging file deleted on failure; verified before atomic rename); a later
///   SQLite failure deliberately retains the published file as an orphan for reconciliation
///   (SQLite cannot roll back the filesystem) — it never silently overwrites existing
///   content-addressed artifacts and never leaves a formal revision behind.
/// </summary>
public sealed class FieldPackageImporter
{
    /// <summary>V1 payload format identity for imported WVFLDPK 1.0 packages.</summary>
    public const string PayloadFormat = "application/vnd.worldview.field-payload";
    public const string PayloadFormatVersion = "1";

    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;
    private readonly FieldPackageOwnerContextValidator _ownerContextValidator;

    public FieldPackageImporter(
        ICoreDomainDatabase database,
        IFieldPayloadFileStore fileStore,
        FieldPackageOwnerContextValidator? ownerContextValidator = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _ownerContextValidator = ownerContextValidator ?? new FieldPackageOwnerContextValidator(database);
    }

    /// <summary>
    /// Imports one package stream. The stream must be readable and seekable; on success it is
    /// positioned anywhere (callers should not rely on position). Validation failures throw
    /// before any byte is staged; publication failures leave no formal revision.
    /// Validation order: binary open → owner-context preflight → full tile verification →
    /// payload preparation/publication.
    /// </summary>
    public async Task<FieldPackageImportResult> ImportAsync(
        Stream packageStream,
        ImportFieldPackagePlan plan,
        FieldPackageReaderOptions? readerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageStream);
        ArgumentNullException.ThrowIfNull(plan);
        if (!packageStream.CanRead || !packageStream.CanSeek)
            throw new ArgumentException(
                "Field Package import requires a readable, seekable stream.", nameof(packageStream));
        if (plan.StorageKind is not (FieldPayloadStorageKind.SqliteBlob or FieldPayloadStorageKind.ManagedFile))
            throw new ArgumentException(
                $"Unknown Field Payload storage kind {plan.StorageKind}.", nameof(plan));
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Format-level validation only; the reader stays free of domain knowledge.
        using var reader = FieldPackageReader.Open(packageStream, readerOptions, leaveOpen: true);

        // 2. Owner-context preflight against the authoritative target rows. Runs before any
        //    persistence content is prepared, so invalid references never stage bytes.
        _ = _ownerContextValidator.Validate(reader.ManifestUtf8, plan.ProjectId, plan.MapId);

        // 3. Full tile verification before publication: reader tile hashes are verified per
        //    read, and import is the formal publication gate, so every tile must verify now.
        //    A corrupt tile fails the import with no staged bytes and no revision.
        var columns = (uint)Math.DivRem(reader.GridWidth, reader.TileWidth, out var remainderWidth);
        var rows = (uint)Math.DivRem(reader.GridHeight, reader.TileHeight, out var remainderHeight);
        if (remainderWidth > 0) columns++;
        if (remainderHeight > 0) rows++;
        for (uint tileY = 0; tileY < rows; tileY++)
            for (uint tileX = 0; tileX < columns; tileX++)
                _ = reader.ReadTile(tileX, tileY);
        cancellationToken.ThrowIfCancellationRequested();

        // 4. Determine the descriptor identity from the validated manifest (registry-checked
        //    by the preflight) and allocate import identities.
        var (fieldKey, descriptorSchemaVersion, semanticsId) = ReadDescriptorIdentity(reader.ManifestUtf8);
        var datasetId = plan.FieldDatasetId ?? Guid.NewGuid();
        var revisionId = plan.FieldRevisionId ?? Guid.NewGuid();
        var payloadId = Guid.NewGuid();

        if (plan.StorageKind == FieldPayloadStorageKind.SqliteBlob)
        {
            var packageBytes = await ReadAllAsync(packageStream, cancellationToken).ConfigureAwait(false);
            var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
            var revision = FieldDatasetService.PublishRevision(_database, new PublishFieldRevisionPlan
            {
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                FieldDatasetId = datasetId,
                FieldKey = fieldKey,
                DescriptorSchemaVersion = descriptorSchemaVersion,
                SemanticsId = semanticsId,
                FieldRevisionId = revisionId,
                Payload = BlobPayloadPlan(plan, datasetId, revisionId, payloadId, packageBytes, packageHash),
                PayloadBytes = packageBytes,
                ExpectedCurrentFieldRevisionId = plan.ExpectedCurrentFieldRevisionId,
                ContentFingerprint = packageHash,
                ManifestJson = System.Text.Encoding.UTF8.GetString(reader.ManifestUtf8.Span),
                ProvenanceJson = plan.ProvenanceJson,
                CreatedAtUtcMs = plan.CreatedAtUtcMs,
            });
            return new FieldPackageImportResult
            {
                Revision = revision,
                PackageSha256 = packageHash,
                PackageByteLength = packageBytes.LongLength,
            };
        }

        // ManagedFile: stream to the content-addressed store (staging + verify + atomic
        // rename), then publish through the authoritative SQLite transaction. A failed
        // publication retains the file as an orphan; reconciliation owns collection.
        packageStream.Position = 0;
        var packageDigest = Convert.ToHexString(SHA256.HashData(packageStream)).ToLowerInvariant();
        packageStream.Position = 0;
        var publisher = new FieldPayloadPublisher(_database, _fileStore);
        var revisionRecord = await publisher.PublishManagedFileRevisionAsync(
            new PublishFieldRevisionPlan
            {
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                FieldDatasetId = datasetId,
                FieldKey = fieldKey,
                DescriptorSchemaVersion = descriptorSchemaVersion,
                SemanticsId = semanticsId,
                FieldRevisionId = revisionId,
                Payload = new RegisterFieldPayloadPlan
                {
                    FieldPayloadId = payloadId,
                    ProjectId = plan.ProjectId,
                    FieldDatasetId = datasetId,
                    FieldRevisionId = revisionId,
                    StorageKind = FieldPayloadStorageKind.ManagedFile,
                    PayloadFormat = PayloadFormat,
                    FormatVersion = PayloadFormatVersion,
                    HashAlgorithm = "sha-256",
                    ByteLength = 0,
                    ContentHash = string.Empty,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                },
                PayloadBytes = null,
                ExpectedCurrentFieldRevisionId = plan.ExpectedCurrentFieldRevisionId,
                ContentFingerprint = packageDigest,
                ManifestJson = System.Text.Encoding.UTF8.GetString(reader.ManifestUtf8.Span),
                ProvenanceJson = plan.ProvenanceJson,
                CreatedAtUtcMs = plan.CreatedAtUtcMs,
            },
            packageStream,
            cancellationToken).ConfigureAwait(false);

        return new FieldPackageImportResult
        {
            Revision = revisionRecord,
            PackageSha256 = packageDigest,
            PackageByteLength = revisionRecord.Payload.ByteLength,
        };
    }

    private static RegisterFieldPayloadPlan BlobPayloadPlan(
        ImportFieldPackagePlan plan,
        Guid datasetId,
        Guid revisionId,
        Guid payloadId,
        byte[] packageBytes,
        string packageHash) => new()
    {
        FieldPayloadId = payloadId,
        ProjectId = plan.ProjectId,
        FieldDatasetId = datasetId,
        FieldRevisionId = revisionId,
        StorageKind = FieldPayloadStorageKind.SqliteBlob,
        PayloadFormat = PayloadFormat,
        FormatVersion = PayloadFormatVersion,
        HashAlgorithm = "sha-256",
        ByteLength = packageBytes.LongLength,
        ContentHash = packageHash,
        RelativePath = null,
        CreatedAtUtcMs = plan.CreatedAtUtcMs,
    };

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var output = new MemoryStream();
        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    private static (string FieldKey, int SchemaVersion, string SemanticsId) ReadDescriptorIdentity(
        ReadOnlyMemory<byte> manifestUtf8)
    {
        using var document = System.Text.Json.JsonDocument.Parse(manifestUtf8);
        var field = document.RootElement.GetProperty("field");
        return (
            field.GetProperty("field_key").GetString()!,
            field.GetProperty("schema_version").GetInt32(),
            field.GetProperty("semantics_id").GetString()!);
    }
}
