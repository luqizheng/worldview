using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Application.Fields;

/// <summary>Stable error codes for Field Package export failures.</summary>
public static class FieldPackageExportErrorCodes
{
    /// <summary>The requested Field Revision does not exist under the target Project/Dataset.</summary>
    public const string RevisionMissing = "WVPK_EXPORT_REVISION_MISSING";
    /// <summary>The revision has no registered Field Payload metadata (fail-closed).</summary>
    public const string PayloadMissing = "WVPK_EXPORT_PAYLOAD_MISSING";
    /// <summary>The registered payload is not an internal Field Payload V1 and cannot be repackaged.</summary>
    public const string PayloadFormatUnsupported = "WVPK_EXPORT_PAYLOAD_FORMAT";
    /// <summary>Payload byte length disagrees with the manifest-derived canonical tile plan.</summary>
    public const string PayloadLengthMismatch = "WVPK_EXPORT_PAYLOAD_LENGTH";
    /// <summary>The persisted manifest is not byte-identical RFC 8785 canonical UTF-8 (defect guard).</summary>
    public const string ManifestNotCanonical = "WVPK_EXPORT_MANIFEST_NOT_CANONICAL";
    /// <summary>The persisted manifest no longer satisfies the publication authority's closed-world validation (defect guard).</summary>
    public const string ManifestInvalid = "WVPK_EXPORT_MANIFEST_INVALID";
    /// <summary>The persisted manifest disagrees with the authoritative typed metadata columns (defect guard).</summary>
    public const string ManifestMetadataMismatch = "WVPK_EXPORT_MANIFEST_METADATA_MISMATCH";
}

/// <summary>One Field Package export request.</summary>
public sealed record ExportFieldPackagePlan
{
    public required Guid ProjectId { get; init; }
    public required Guid FieldDatasetId { get; init; }
    public required Guid FieldRevisionId { get; init; }
    /// <summary>
    /// Immutable destination file path. An existing file is never overwritten; a failed or
    /// cancelled export leaves only a removed temporary artifact, never the destination file.
    /// </summary>
    public required string DestinationPath { get; init; }
}

/// <summary>Authoritative result of one completed Field Package export.</summary>
public sealed record FieldPackageExportResult
{
    public required FieldRevisionRecord Revision { get; init; }
    /// <summary>Lowercase SHA-256 of the produced WVFLDPK interchange bytes.</summary>
    public required string PackageSha256 { get; init; }
    public required long PackageByteLength { get; init; }
    public required int TileCount { get; init; }
}

/// <summary>
/// Exports one immutable published Field Revision back into a WVFLDPK 1.0 interchange
/// package. Authority boundaries (ME-05-12 §6): SQLite is read-only for this adapter — it
/// never modifies revision rows or a Dataset current pointer; the package Writer never sees
/// the database; the export source is the internal Field Payload (decoded tiles) read through
/// the storage-kind-neutral <see cref="FieldPayloadReader"/>, never a previously imported
/// package file. The writer produces the package deterministically from the persisted
/// canonical manifest and the canonical tile sequence, so exporting the same revision yields
/// byte-identical packages.
///
/// The production profile gate applies to export as well: a golden_fixture_only revision is
/// rejected unless the instance policy explicitly allows fixture profiles, so export can
/// never be used to launder a fixture-only payload into production circulation.
/// </summary>
public sealed class FieldPackageExporter
{
    private readonly ICoreDomainDatabase _database;
    private readonly IFieldPayloadFileStore _fileStore;
    private readonly FieldPackagePolicy _policy;

    public FieldPackageExporter(
        ICoreDomainDatabase database,
        IFieldPayloadFileStore fileStore,
        FieldPackagePolicy? policy = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _policy = policy ?? new FieldPackagePolicy();
    }

    /// <summary>
    /// Exports the requested revision to a new package file. Cancellation is honored before
    /// the export begins, per tile while reading the payload, and inside the writer (tile
    /// ingestion, payload copy, self-validation, before publication); a cancelled or failed
    /// export never leaves the destination file or a partial package behind.
    /// </summary>
    public async Task<FieldPackageExportResult> ExportAsync(
        ExportFieldPackagePlan plan,
        FieldPackageWriterOptions? writerOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan.DestinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Authoritative revision: export reads only; nothing in SQLite is modified.
        var revision = _database.FindFieldRevision(plan.ProjectId, plan.FieldDatasetId, plan.FieldRevisionId)
            ?? throw Error(FieldPackageExportErrorCodes.RevisionMissing,
                $"Field Revision {plan.FieldRevisionId:D} does not exist under Dataset {plan.FieldDatasetId:D} in Project {plan.ProjectId:D}.");
        var metadata = revision.Metadata
            ?? throw new CoreDomainDataException(
                $"Field Revision {plan.FieldRevisionId:D} has no typed manifest projection; export fails closed.");

        // 2. Production profile gate (ME-05-12 §4): export must not bypass it.
        FieldPackagePolicyGuard.EnsureProfileScopeAllowed(metadata.ValueProfileScope, _policy, "Export");

        // 3. Only an internal Field Payload V1 can be repackaged; the payload stream is read
        //    through the storage-kind-neutral reader, which verifies length and SHA-256
        //    against the authoritative payload metadata before returning bytes.
        if (!string.Equals(revision.Payload.PayloadFormat, FieldInternalPayloadFormat.PayloadFormat, StringComparison.Ordinal)
            || !string.Equals(revision.Payload.FormatVersion, FieldInternalPayloadFormat.FormatVersion, StringComparison.Ordinal))
            throw Error(FieldPackageExportErrorCodes.PayloadFormatUnsupported,
                $"Field Revision {plan.FieldRevisionId:D} payload '{revision.Payload.PayloadFormat}' v{revision.Payload.FormatVersion} " +
                "is not an internal Field Payload V1 and cannot be exported as a WVFLDPK package.");

        // 4. Canonical manifest snapshot: the persisted manifest is byte-identical canonical
        //    UTF-8 by publication contract; export fails closed if it ever is not, because the
        //    manifest bytes are written verbatim into the package.
        var manifestUtf8 = RequireCanonicalManifestUtf8(revision.ManifestJson);

        // 4b. Publication-authority consistency: the persisted manifest must still pass the
        //     same closed-world validation the publication path applies AND still agree with
        //     the authoritative typed metadata columns. The database's immutability trigger is
        //     the first-line defense against post-publication manifest tampering; this check is
        //     the independent export-side guard for a defense that was bypassed (e.g. a
        //     tampered backup restore or trigger loss), so a manifest that silently contradicts
        //     the typed revision can never be repackaged into circulation. The manifest is
        //     never rebuilt from typed metadata to mask tampering.
        RequireManifestConsistentWithTypedMetadata(revision.ManifestJson, metadata);


        // 5. Canonical tile plan from the typed projection; the verified payload length must
        //    match it exactly before any output file is touched.
        var payloadPlan = FieldInternalPayloadPlan.FromMetadata(metadata.Grid, metadata.Planes);
        if (payloadPlan.TotalByteLength != revision.Payload.ByteLength)
            throw Error(FieldPackageExportErrorCodes.PayloadLengthMismatch,
                $"Field Revision {plan.FieldRevisionId:D} payload length {revision.Payload.ByteLength} does not match " +
                $"the manifest-derived internal payload length {payloadPlan.TotalByteLength}.");

        await using var payloadStream = await new FieldPayloadReader(_database, _fileStore)
            .OpenReadAsync(plan.ProjectId, plan.FieldRevisionId, cancellationToken).ConfigureAwait(false)
            ?? throw Error(FieldPackageExportErrorCodes.PayloadMissing,
                $"Field Revision {plan.FieldRevisionId:D} has no registered Field Payload metadata.");

        // 6. Publication. The writer consumes the verified payload tiles sequentially in
        //    canonical order, spools to disk, writes the package deterministically, re-opens
        //    its own output with the production reader for self-validation and finally
        //    publishes by atomic rename (never overwriting an existing file). The Dataset
        //    current pointer and every revision row are untouched.
        cancellationToken.ThrowIfCancellationRequested();
        var result = FieldPackageWriter.WriteFileAtomically(
            plan.DestinationPath,
            manifestUtf8,
            EnumeratePayloadTiles(payloadStream, payloadPlan.Tiles, cancellationToken),
            writerOptions,
            cancellationToken);

        if (result.TileCount != payloadPlan.Tiles.Count)
            throw new CoreDomainDataException(
                "Field Package export wrote a tile count that differs from the canonical tile plan.");

        return new FieldPackageExportResult
        {
            Revision = revision,
            PackageSha256 = result.PackageSha256,
            PackageByteLength = result.PackageLength,
            TileCount = result.TileCount,
        };
    }

    /// <summary>
    /// Supplies the payload tiles to the writer lazily, reading each slice sequentially from
    /// the verified payload stream. No database or filesystem logic lives here beyond the
    /// caller-owned stream; the writer stays a pure package formatter.
    /// </summary>
    private static IEnumerable<FieldPackageTileInput> EnumeratePayloadTiles(
        Stream payload,
        IReadOnlyList<FieldInternalTileSlice> tiles,
        CancellationToken cancellationToken)
    {
        foreach (var slice in tiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = new byte[checked((int)slice.ByteLength)];
            var read = 0;
            while (read < bytes.Length)
            {
                var chunk = payload.Read(bytes, read, bytes.Length - read);
                if (chunk == 0)
                    throw new FieldPackageValidationException(
                        FieldPackageExportErrorCodes.PayloadLengthMismatch,
                        "Field Payload ended before the canonical tile plan was satisfied.");
                read += chunk;
            }

            yield return new FieldPackageTileInput(
                slice.TileX, slice.TileY, slice.ValidWidth, slice.ValidHeight, bytes);
        }
    }

    private static byte[] RequireCanonicalManifestUtf8(string manifestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        var manifestUtf8 = Encoding.UTF8.GetBytes(manifestJson);
        try
        {
            _ = FieldManifestCanonicalizer.RequireCanonicalUtf8(manifestUtf8);
        }
        catch (CoreDomainDataException ex)
        {
            throw new FieldPackageValidationException(
                FieldPackageExportErrorCodes.ManifestNotCanonical,
                "Persisted Field Revision manifest is not byte-identical RFC 8785 canonical UTF-8; export fails closed.",
                ex);
        }

        return manifestUtf8;
    }

    /// <summary>
    /// Re-establishes the publication invariant between the persisted manifest bytes and the
    /// typed metadata columns. The manifest is re-validated with the exact publication
    /// authority (<see cref="FieldDatasetService.ValidateManifest"/>, which rebuilds the typed
    /// projection from the manifest itself) and compared member-wise with the authoritative
    /// columns. Any divergence — a semantically valid but different manifest, a numeric
    /// re-spelling of codec parameters, a changed grid origin string — fails the export with a
    /// typed error before any output file is touched.
    /// </summary>
    private static void RequireManifestConsistentWithTypedMetadata(string manifestJson, FieldRevisionMetadata metadata)
    {
        FieldRevisionMetadata manifestProjection;
        try
        {
            manifestProjection = FieldDatasetService.ValidateManifest(
                manifestJson, metadata.FieldKey, metadata.DescriptorSchemaVersion, metadata.SemanticsId);
        }
        catch (Exception ex) when (ex is CoreDomainConflictException or CoreDomainDataException or JsonException)
        {
            throw new FieldPackageValidationException(
                FieldPackageExportErrorCodes.ManifestInvalid,
                "Persisted Field Revision manifest no longer satisfies the publication authority's manifest validation; export fails closed.",
                ex);
        }

        if (!manifestProjection.StructurallyEquals(metadata))
            throw new FieldPackageValidationException(
                FieldPackageExportErrorCodes.ManifestMetadataMismatch,
                $"Persisted Field Revision manifest disagrees with its authoritative typed metadata " +
                $"(descriptor {metadata.FieldKey}@{metadata.DescriptorSchemaVersion}); export fails closed instead of repackaging inconsistent data.");
    }

    private static FieldPackageValidationException Error(string code, string message) => new(code, message);
}
