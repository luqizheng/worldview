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
/// validates it fully (binary format + owner context + production profile gate) before any
/// persistence content is prepared, then publishes exactly one immutable Field Revision with
/// its dedicated internal Field Payload (decoded tiles, never the WVFLDPK file itself).
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
    /// <summary>Lowercase SHA-256 of the exact imported WVFLDPK interchange bytes. Interchange
    /// integrity only; never used as payload metadata or content fingerprint.</summary>
    public required string PackageSha256 { get; init; }
    public required long PackageByteLength { get; init; }
    /// <summary>Lowercase SHA-256 of the internal decoded-tile payload stored for the revision.</summary>
    public required string PayloadSha256 { get; init; }
    public required long PayloadByteLength { get; init; }
}

/// <summary>
/// Imports a WVFLDPK 1.0 Field Package into the internal Field Dataset / Revision domain
/// model. The package never leaks into the domain model: the importer decodes the package's
/// tiles and stores their canonical concatenation as the internal Field Payload
/// (<see cref="FieldInternalPayloadFormat"/>) under the storage kind the caller selected;
/// only the typed manifest projection is persisted semantically. SQLite stays authoritative
/// for Dataset / Revision identity and payload metadata; the payload bytes (BLOB or
/// ManagedFile) stay authoritative for runtime content. The package digest is used solely
/// for interchange verification and is never conflated with payload hashes (ME-05-12 §6, §7).
///
/// Ordering is validate-first: binary open, owner-context preflight, production profile gate
/// and full per-tile verification all run before any persistence content is prepared, so an
/// invalid package can never stage bytes or create a revision.
///
/// Failure strategy per storage kind:
/// - <see cref="FieldPayloadStorageKind.SqliteBlob"/>: payload row, revision and Dataset CAS
///   advance in one SQLite transaction; failure rolls back everything and creates no artifacts.
/// - <see cref="FieldPayloadStorageKind.ManagedFile"/>: the internal payload streams tile by
///   tile into the content-addressed store (staging file deleted on failure; verified before
///   atomic rename, never fully loaded into memory); a later SQLite failure deliberately
///   retains the published file as an orphan for reconciliation (SQLite cannot roll back the
///   filesystem) — it never silently overwrites existing content-addressed artifacts and
///   never leaves a formal revision behind.
/// </summary>
public sealed class FieldPackageImporter
{
    /// <summary>V1 internal payload format identity; the layout is decoded tiles in canonical order.</summary>
    public const string PayloadFormat = FieldInternalPayloadFormat.PayloadFormat;
    public const string PayloadFormatVersion = FieldInternalPayloadFormat.FormatVersion;

    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;
    private readonly FieldPackageOwnerContextValidator _ownerContextValidator;
    private readonly FieldPackagePolicy _policy;

    public FieldPackageImporter(
        ICoreDomainDatabase database,
        IFieldPayloadFileStore fileStore,
        FieldPackageOwnerContextValidator? ownerContextValidator = null,
        FieldPackagePolicy? policy = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _ownerContextValidator = ownerContextValidator ?? new FieldPackageOwnerContextValidator(database);
        _policy = policy ?? new FieldPackagePolicy();
    }

    /// <summary>
    /// Imports one package stream. The stream must be readable and seekable; on success it is
    /// positioned anywhere (callers should not rely on position). Validation failures throw
    /// before any byte is staged; publication failures leave no formal revision. Cancellation
    /// is checked between validation stages, per tile during verification/assembly and per tile
    /// during ManagedFile staging; a cancelled import leaves no revision, no staged file and no
    /// formal artifact.
    /// Validation order: binary open → owner-context preflight → production profile gate →
    /// full tile verification and internal payload hashing → publication.
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
        var ownerContext = _ownerContextValidator.Validate(reader.ManifestUtf8, plan.ProjectId, plan.MapId);

        // 3. Production profile gate (ME-05-12 §4): golden_fixture_only profiles import only
        //    under an explicitly allowing policy; there is no global override.
        FieldPackagePolicyGuard.EnsureProfileScopeAllowed(ownerContext.Metadata.ValueProfileScope, _policy, "Import");

        // 4. Canonical internal payload layout from the validated typed projection.
        var payloadPlan = FieldInternalPayloadPlan.FromMetadata(ownerContext.Metadata.Grid, ownerContext.Metadata.Planes);

        // 5. Package digest pass over the exact interchange bytes. Computed independently of
        //    the payload content; used only for interchange verification in the result.
        packageStream.Position = 0;
        var packageSha256 = Convert.ToHexString(SHA256.HashData(packageStream)).ToLowerInvariant();
        var packageByteLength = packageStream.Length;

        // 6. Full tile verification before publication: reader tile hashes and masks are
        //    verified per read, and import is the formal publication gate, so every tile must
        //    verify now. The validated tile bytes are hashed (BLOB: also assembled) exactly in
        //    canonical order, producing the independent internal payload digest. A corrupt tile
        //    fails the import with no staged bytes and no revision.
        var payloadSha256 = VerifyTilesAndHashPayload(reader, payloadPlan, plan.StorageKind, out var assembledBlob, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // 7. Publication. Package digest and payload digest travel separately from here.
        var datasetId = plan.FieldDatasetId ?? Guid.NewGuid();
        var revisionId = plan.FieldRevisionId ?? Guid.NewGuid();
        var payloadId = Guid.NewGuid();
        var manifestJson = System.Text.Encoding.UTF8.GetString(reader.ManifestUtf8.Span);

        if (plan.StorageKind == FieldPayloadStorageKind.SqliteBlob)
        {
            var payloadBytes = assembledBlob!.ToArray();
            var revision = FieldDatasetService.PublishRevision(_database, new PublishFieldRevisionPlan
            {
                ProjectId = plan.ProjectId,
                MapId = plan.MapId,
                FieldDatasetId = datasetId,
                FieldKey = ownerContext.Metadata.FieldKey,
                DescriptorSchemaVersion = ownerContext.Metadata.DescriptorSchemaVersion,
                SemanticsId = ownerContext.Metadata.SemanticsId,
                FieldRevisionId = revisionId,
                Payload = BlobPayloadPlan(plan, datasetId, revisionId, payloadId, payloadSha256, payloadBytes.LongLength),
                PayloadBytes = payloadBytes,
                ExpectedCurrentFieldRevisionId = plan.ExpectedCurrentFieldRevisionId,
                ContentFingerprint = payloadSha256,
                ManifestJson = manifestJson,
                ProvenanceJson = plan.ProvenanceJson,
                CreatedAtUtcMs = plan.CreatedAtUtcMs,
            });
            return new FieldPackageImportResult
            {
                Revision = revision,
                PackageSha256 = packageSha256,
                PackageByteLength = packageByteLength,
                PayloadSha256 = payloadSha256,
                PayloadByteLength = payloadBytes.LongLength,
            };
        }

        // ManagedFile: the verified tiles stream tile by tile into the content-addressed store
        // (staging + verify + atomic rename; bounded memory), then publish through the
        // authoritative SQLite transaction. The staging stream re-verifies every tile on read
        // and its own length/hash bookkeeping against the values computed above, so the bytes
        // that reach the store are proven equal to the validated payload. A failed publication
        // retains the file as an orphan; reconciliation owns collection.
        var revisionRecord = await new FieldPayloadPublisher(_database, _fileStore)
            .PublishManagedFileRevisionAsync(
                new PublishFieldRevisionPlan
                {
                    ProjectId = plan.ProjectId,
                    MapId = plan.MapId,
                    FieldDatasetId = datasetId,
                    FieldKey = ownerContext.Metadata.FieldKey,
                    DescriptorSchemaVersion = ownerContext.Metadata.DescriptorSchemaVersion,
                    SemanticsId = ownerContext.Metadata.SemanticsId,
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
                        ByteLength = payloadPlan.TotalByteLength,
                        ContentHash = payloadSha256,
                        CreatedAtUtcMs = plan.CreatedAtUtcMs,
                    },
                    PayloadBytes = null,
                    ExpectedCurrentFieldRevisionId = plan.ExpectedCurrentFieldRevisionId,
                    ContentFingerprint = payloadSha256,
                    ManifestJson = manifestJson,
                    ProvenanceJson = plan.ProvenanceJson,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                },
                new DecodedTilePayloadStream(reader, payloadPlan, payloadSha256, cancellationToken),
                cancellationToken).ConfigureAwait(false);

        // Defect guards: the staged and published payload must be exactly the validated
        // internal payload, and the fingerprint must be the payload digest (never the package
        // digest).
        if (!string.Equals(revisionRecord.Payload.ContentHash, payloadSha256, StringComparison.Ordinal)
            || revisionRecord.Payload.ByteLength != payloadPlan.TotalByteLength
            || !string.Equals(revisionRecord.ContentFingerprint, payloadSha256, StringComparison.Ordinal))
            throw new CoreDomainDataException(
                "Field Package import published a payload that differs from the validated internal payload.");

        return new FieldPackageImportResult
        {
            Revision = revisionRecord,
            PackageSha256 = packageSha256,
            PackageByteLength = packageByteLength,
            PayloadSha256 = payloadSha256,
            PayloadByteLength = payloadPlan.TotalByteLength,
        };
    }

    /// <summary>
    /// Reads every tile in canonical order (each read verifies the tile hash and mask bytes),
    /// hashes the exact concatenation incrementally and — for SQLite BLOB storage — assembles
    /// the internal payload bytes. No step loads tiles beyond the current one outside the
    /// caller-selected BLOB assembly.
    /// </summary>
    private static string VerifyTilesAndHashPayload(
        FieldPackageReader reader,
        FieldInternalPayloadPlan payloadPlan,
        FieldPayloadStorageKind storageKind,
        out MemoryStream? assembledBlob,
        CancellationToken cancellationToken)
    {
        assembledBlob = storageKind == FieldPayloadStorageKind.SqliteBlob ? new MemoryStream() : null;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long payloadByteLength = 0;
        foreach (var slice in payloadPlan.Tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = reader.ReadTile(slice.TileX, slice.TileY);
            if (decoded.LongLength != slice.ByteLength)
                throw new CoreDomainDataException(
                    $"Tile ({slice.TileX},{slice.TileY}) decoded length does not match the internal payload plan.");
            hash.AppendData(decoded);
            payloadByteLength = checked(payloadByteLength + decoded.LongLength);
            assembledBlob?.Write(decoded, 0, decoded.Length);
        }

        if (payloadByteLength != payloadPlan.TotalByteLength)
            throw new CoreDomainDataException(
                "Internal payload assembly length does not match the manifest-derived tile plan.");

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Streams the verified decoded tiles in canonical order as the internal payload, without
    /// holding more than one tile in memory. Every tile read re-verifies its directory hash and
    /// mask bytes; at the end of the stream the delivered length and independently recomputed
    /// SHA-256 must equal the values established by the validation pass, so the staged bytes
    /// are provably the validated payload. The stream is forward-only; the underlying package
    /// reader stays exclusively owned by the import while the stream is consumed.
    /// </summary>
    private sealed class DecodedTilePayloadStream : Stream
    {
        private readonly FieldPackageReader _reader;
        private readonly FieldInternalPayloadPlan _plan;
        private readonly string _expectedSha256;
        private readonly CancellationToken _cancellationToken;
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int _nextTile;
        private byte[]? _currentTile;
        private int _currentOffset;
        private long _delivered;
        private bool _finished;

        public DecodedTilePayloadStream(
            FieldPackageReader reader,
            FieldInternalPayloadPlan plan,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _expectedSha256 = expectedSha256;
            _cancellationToken = cancellationToken;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _delivered; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);

            var delivered = 0;
            while (count > 0)
            {
                if (_currentTile is null || _currentOffset >= _currentTile.Length)
                {
                    if (!Advance()) break;
                }

                var copy = Math.Min(count, _currentTile!.Length - _currentOffset);
                Buffer.BlockCopy(_currentTile, _currentOffset, buffer, offset, copy);
                _hash.AppendData(buffer.AsSpan(offset, copy));
                _currentOffset += copy;
                offset += copy;
                count -= copy;
                _delivered = checked(_delivered + copy);
                delivered += copy;
            }

            return delivered;
        }

        /// <summary>Fetches the next canonical tile; completes the stream at plan exhaustion.</summary>
        private bool Advance()
        {
            if (_finished) return false;
            _cancellationToken.ThrowIfCancellationRequested();
            if (_nextTile >= _plan.Tiles.Count)
            {
                _finished = true;
                if (_delivered != _plan.TotalByteLength)
                    throw new CoreDomainDataException(
                        "Staged internal Field Payload length does not match the validated payload.");
                var actual = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
                if (!string.Equals(actual, _expectedSha256, StringComparison.Ordinal))
                    throw new CoreDomainDataException(
                        "Staged internal Field Payload SHA-256 does not match the validated payload.");
                return false;
            }

            var slice = _plan.Tiles[_nextTile++];
            var decoded = _reader.ReadTile(slice.TileX, slice.TileY);
            if (decoded.LongLength != slice.ByteLength)
                throw new CoreDomainDataException(
                    $"Tile ({slice.TileX},{slice.TileY}) decoded length does not match the internal payload plan.");
            _currentTile = decoded;
            _currentOffset = 0;
            return true;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _finished = true;
                _hash.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private static RegisterFieldPayloadPlan BlobPayloadPlan(
        ImportFieldPackagePlan plan,
        Guid datasetId,
        Guid revisionId,
        Guid payloadId,
        string payloadSha256,
        long payloadByteLength) => new()
    {
        FieldPayloadId = payloadId,
        ProjectId = plan.ProjectId,
        FieldDatasetId = datasetId,
        FieldRevisionId = revisionId,
        StorageKind = FieldPayloadStorageKind.SqliteBlob,
        PayloadFormat = PayloadFormat,
        FormatVersion = PayloadFormatVersion,
        HashAlgorithm = "sha-256",
        ByteLength = payloadByteLength,
        ContentHash = payloadSha256,
        RelativePath = null,
        CreatedAtUtcMs = plan.CreatedAtUtcMs,
    };
}
