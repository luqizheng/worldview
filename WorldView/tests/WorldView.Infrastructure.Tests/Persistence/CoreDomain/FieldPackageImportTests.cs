using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Abstractions;
using WorldView.Application.Fields;
using WorldView.Application.Packaging;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// Real SQLite + real filesystem proof for Field Package import: validate-first ordering,
/// authoritative publication semantics, per-storage-kind failure strategy and orphan
/// reconciliation after a failed ManagedFile publication.
/// </summary>
public sealed class FieldPackageImportTests : IDisposable
{
    private const long Now = 5_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabase _database;
    private readonly FieldPayloadFileStore _fileStore;

    public FieldPackageImportTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-field-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _database = (SqlitePackageDatabase)new SqlitePackageDatabaseFactory()
            .CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        new ApplicationBootstrapper().InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
        _fileStore = new FieldPayloadFileStore(_root);
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    private SqliteCoreDomainDatabase Core() => new(_database);

    /// <summary>The golden fixtures declare golden_fixture_only profiles; tests opt in explicitly.</summary>
    private static FieldPackagePolicy FixturePolicy() => new() { AllowGoldenFixtureOnlyProfiles = true };

    private FieldPackageImporter Importer(FieldPackagePolicy? policy = null) =>
        new(Core(), _fileStore, policy: policy);

    private (ProjectRecord Project, Guid MapId) SeedMap()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("Import test", Now);
        core.InsertProject(ProjectCreationPlan.Create(project));
        var mapPlan = MapRules.CreateMap(project, "Map A", Now + 1);
        mapPlan = mapPlan with { Map = mapPlan.Map with { MapSpaceId = "fixture-map-space-001" } };
        core.CreateMapStructure(mapPlan);
        _ = SpatialReferenceService.RegisterDefinition(core, new SpatialReferenceDefinitionRecord
        {
            ProjectId = project.ProjectId,
            SpatialReferenceId = "fixture:local-cartesian-v1",
            DefinitionVersion = 1,
            Kind = SpatialReferenceKind.LocalCartesian,
            CoordinateUnit = "m",
            AxisOrder = SpatialAxisOrder.XY,
            Dimension = 2,
            ValidDomainJson = """{"max_x":"1000000","max_y":"1000000","min_x":"-1000000","min_y":"-1000000"}""",
            WrapBehavior = SpatialWrapBehavior.None,
            VerticalReferenceId = "fixture:vertical-reference-local-v1",
            PrecisionProfile = new CoordinatePrecisionProfileRecord
            {
                ProfileId = "fixture:coordinate-precision-v1",
                Version = 1,
                CoordinateUnit = "m",
                AxisOrder = SpatialAxisOrder.XY,
                NumericRepresentation = "decimal",
                MaximumAbsoluteQuantizationError = 0.000001m,
                RoundingMode = MidpointRounding.ToEven,
                MinimumAuthoringTolerance = 0.001m,
                SmallestMeaningfulFeatureSize = 0.01m,
            },
            DefinitionParametersJson = """{"origin_x":"0","origin_y":"0"}""",
            MeasurementCapabilitiesJson = """{"area":true,"distance":true}""",
            ProvenanceJson = """{"source":"test-fixture"}""",
            DefinitionFingerprint = "",
            CreatedAtUtcMs = Now + 1,
        });
        _ = MapSpatialReferenceService.BindInitial(
            core, project.ProjectId, mapPlan.Map.MapId, "fixture:local-cartesian-v1", Now + 2);
        return (project, mapPlan.Map.MapId);
    }

    private static string BuildManifest() =>
        FieldManifestCanonicalizer.Canonicalize(System.Text.Json.JsonSerializer.Serialize(new
        {
            byte_order = "little_endian",
            canonicalization = "RFC8785-JCS-UTF8",
            codec_registry = new[] { new { codec_id = 0, codec_name = "none", parameters = new { }, version = "1" } },
            coverage = new { mode = "full_grid" },
            field = new { field_key = "elevation", schema_version = 1, semantics_id = "world-view.field.elevation.v1", unit = "m" },
            format = new { major = 1, minor = 0 },
            grid = new
            {
                axis_orientation = "x_east_y_north",
                cell_registration = "area",
                height = 2,
                origin_x = "0",
                origin_y = "0",
                spacing_x = "1",
                spacing_y = "1",
                tile_height = 2,
                tile_width = 2,
                width = 2,
            },
            manifest_schema = new { id = "world-view.field-package-manifest", version = 1 },
            map_edge_semantics = "bounded_non_wrapping",
            no_data = new
            {
                mask_plane = new { invalid_value = 1, plane_name = "validity_mask", valid_value = 0 },
                policy = "explicit_validity_mask",
            },
            spatial = new
            {
                map_space_id = "fixture-map-space-001",
                spatial_reference_id = "fixture:local-cartesian-v1",
                coordinate_precision_profile_id = "fixture:coordinate-precision-v1",
                vertical_reference_id = "fixture:vertical-reference-local-v1",
            },
            tile_hash = new { algorithm = "SHA-256", version = 1 },
            value_encoding = new
            {
                profile_id = "fixture-only:elevation-f32-mask-v1",
                profile_scope = "golden_fixture_only",
                planes = new object[]
                {
                    new { name = "samples", sample_format = "float32_ieee754_le", bytes_per_cell = 4 },
                    new { name = "validity_mask", sample_format = "uint8", bytes_per_cell = 1 },
                },
            },
        }));

    /// <summary>One 2x2 elevation tile: 4 float32 samples + 4 validity bytes (20 bytes total).</summary>
    private static byte[] BuildTileBytes(float sample0 = 1.5f)
    {
        var tile = new byte[20];
        BinaryPrimitives.WriteSingleLittleEndian(tile.AsSpan(0), sample0);
        BinaryPrimitives.WriteSingleLittleEndian(tile.AsSpan(4), 2.5f);
        BinaryPrimitives.WriteSingleLittleEndian(tile.AsSpan(8), 3.5f);
        BinaryPrimitives.WriteSingleLittleEndian(tile.AsSpan(12), 4.5f);
        // validity bytes (16..19) stay 0 = valid.
        return tile;
    }

    private static byte[] BuildPackageBytes(string manifest, float sample0 = 1.5f)
    {
        var tile = BuildTileBytes(sample0);

        const int headerLength = 72;
        const int directoryLength = 16 + 72;
        var directoryOffset = (headerLength + manifest.Length + 7) & ~7;
        var payloadOffset = (directoryOffset + directoryLength + 7) & ~7;
        var packageLength = payloadOffset + tile.Length;
        var result = new byte[packageLength];

        Encoding.ASCII.GetBytes("WVFLDPK").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(24), headerLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(32), (ulong)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(40), (ulong)directoryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(48), directoryLength);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(56), (ulong)payloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(64), (ulong)packageLength);
        Encoding.UTF8.GetBytes(manifest).CopyTo(result, headerLength);

        var directory = result.AsSpan(directoryOffset, directoryLength);
        BinaryPrimitives.WriteUInt16LittleEndian(directory, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(directory.Slice(4), 72);
        BinaryPrimitives.WriteUInt64LittleEndian(directory.Slice(8), 1);
        var entry = directory.Slice(16, 72);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(10), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(12), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(16), (ulong)payloadOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(24), (ulong)tile.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(32), (ulong)tile.Length);
        SHA256.HashData(tile).CopyTo(entry.Slice(40));
        tile.CopyTo(result, payloadOffset);
        return result;
    }

    private static byte[] ValidPackage() => BuildPackageBytes(BuildManifest());

    private static ImportFieldPackagePlan Plan(
        Guid projectId, Guid mapId, FieldPayloadStorageKind storageKind,
        Guid? expectedRevision = null, Guid? datasetId = null) => new()
    {
        ProjectId = projectId,
        MapId = mapId,
        FieldDatasetId = datasetId,
        StorageKind = storageKind,
        ProvenanceJson = """{"source":"field-package-import-test"}""",
        CreatedAtUtcMs = Now + 5,
        ExpectedCurrentFieldRevisionId = expectedRevision,
    };

    [Fact]
    public async Task Import_SqliteBlobPath_PublishesInternalPayloadNotRawPackage()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        var expectedPackageHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        var tileBytes = BuildTileBytes();
        var expectedPayloadHash = Convert.ToHexString(SHA256.HashData(tileBytes)).ToLowerInvariant();

        var result = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        // Package digest belongs to interchange verification only.
        Assert.Equal(expectedPackageHash, result.PackageSha256);
        Assert.Equal(packageBytes.LongLength, result.PackageByteLength);
        // The stored payload is the decoded tile concatenation, never the WVFLDPK file.
        Assert.Equal(expectedPayloadHash, result.PayloadSha256);
        Assert.Equal(tileBytes.LongLength, result.PayloadByteLength);
        Assert.NotEqual(expectedPackageHash, result.PayloadSha256);

        Assert.Equal(1, result.Revision.RevisionNumber);
        Assert.Null(result.Revision.ParentRevisionId);
        // The content fingerprint is the internal payload digest, not the package digest.
        Assert.Equal(expectedPayloadHash, result.Revision.ContentFingerprint);
        Assert.Equal(expectedPayloadHash, result.Revision.Payload.ContentHash);

        var core = Core();
        var dataset = core.FindFieldDataset(project.ProjectId, mapId, "elevation");
        Assert.NotNull(dataset);
        Assert.Equal(result.Revision.FieldDatasetId, dataset!.FieldDatasetId);
        Assert.Equal(result.Revision.FieldRevisionId, dataset.CurrentFieldRevisionId);

        var bytes = core.ReadFieldPayloadBlob(project.ProjectId, result.Revision.FieldRevisionId);
        Assert.NotNull(bytes);
        Assert.Equal(tileBytes, bytes);
        Assert.NotEqual(packageBytes, bytes);
        Assert.Equal(FieldPayloadIntegrityState.Verified, result.Revision.Payload.IntegrityState);
        Assert.Equal(FieldPayloadStorageKind.SqliteBlob, result.Revision.Payload.StorageKind);
        Assert.Equal("elevation", result.Revision.Metadata!.FieldKey);
    }

    [Fact]
    public async Task Import_ManagedFilePath_PublishesInternalPayloadNotRawPackage()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        var tileBytes = BuildTileBytes();
        var payloadHash = Convert.ToHexString(SHA256.HashData(tileBytes)).ToLowerInvariant();
        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();

        var result = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile));

        // Package digest and payload digest stay separate.
        Assert.Equal(packageHash, result.PackageSha256);
        Assert.Equal(packageBytes.LongLength, result.PackageByteLength);
        Assert.Equal(payloadHash, result.PayloadSha256);
        Assert.Equal(tileBytes.LongLength, result.PayloadByteLength);
        Assert.Equal(payloadHash, result.Revision.Payload.ContentHash);
        Assert.Equal(payloadHash, result.Revision.ContentFingerprint);
        Assert.Equal(FieldPayloadStorageKind.ManagedFile, result.Revision.Payload.StorageKind);
        Assert.Equal($"FieldPayloads/sha256/{payloadHash}", result.Revision.Payload.RelativePath);

        // The file store holds exactly one content-addressed file with the decoded tiles,
        // not the raw package bytes.
        Assert.Equal(new[] { payloadHash }, _fileStore.ListContentHashes());
        await using var stored = _fileStore.OpenReadAsync(payloadHash).GetAwaiter().GetResult()!;
        using var storedBytes = new MemoryStream();
        stored.CopyTo(storedBytes);
        Assert.Equal(tileBytes, storedBytes.ToArray());
        Assert.NotEqual(packageBytes, storedBytes.ToArray());

        // The storage-kind-neutral read path verifies and returns the internal payload.
        await using var readStream = await new FieldPayloadReader(Core(), _fileStore)
            .OpenReadAsync(project.ProjectId, result.Revision.FieldRevisionId);
        Assert.NotNull(readStream);
        using var readBytes = new MemoryStream();
        readStream!.CopyTo(readBytes);
        Assert.Equal(tileBytes, readBytes.ToArray());
    }

    [Fact]
    public async Task Import_RejectsCorruptTileBeforeAnyPersistence()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        packageBytes[^1] ^= 0x01; // corrupt final tile payload byte → tile hash mismatch

        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob)));
        Assert.Equal(FieldPackageBinaryErrorCodes.TileHash, error.Code);

        // The pre-publication tile verification failed: nothing entered the domain or the store.
        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
    }

    [Fact]
    public async Task Import_RejectsStructurallyBrokenPackageWithoutSideEffects()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        packageBytes[0] = 0; // invalid magic

        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Magic, error.Code);

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
        Assert.Equal(0, _fileStore.CleanupStagingFiles());
    }

    [Fact]
    public async Task Import_RejectsOwnerContextMismatchBeforeStagingAnyBytes()
    {
        var (project, mapId) = SeedMap();
        // Manifest references another Map Space: owner preflight must reject before staging.
        var manifest = BuildManifest().Replace(
            "\"map_space_id\":\"fixture-map-space-001\"",
            "\"map_space_id\":\"fixture-map-space-999\"",
            StringComparison.Ordinal);
        var packageBytes = BuildPackageBytes(manifest);

        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile)));
        Assert.Equal(FieldPackageOwnerContextErrorCodes.MapSpaceMismatch, error.Code);

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
    }

    [Fact]
    public async Task Import_SqliteBlobPublicationCasFailureLeavesNoRevision()
    {
        var (project, mapId) = SeedMap();
        var importer = Importer(FixturePolicy());
        var first = await importer.ImportAsync(
            new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        // Stale CAS pin: publication must reject wholesale and create no second revision.
        await Assert.ThrowsAsync<CoreDomainConflictException>(() =>
            importer.ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob,
                    expectedRevision: Guid.NewGuid(),
                    datasetId: first.Revision.FieldDatasetId)));

        var core = Core();
        var dataset = core.FindFieldDataset(project.ProjectId, mapId, "elevation")!;
        Assert.Equal(first.Revision.FieldRevisionId, dataset.CurrentFieldRevisionId);
        var revisions = core.ListFieldRevisions(project.ProjectId, dataset.FieldDatasetId);
        Assert.Single(revisions);
        Assert.Single(core.ListFieldPayloads(project.ProjectId));
    }

    [Fact]
    public async Task Import_ManagedFilePublicationCasFailureRetainsOrphanForReconciliation()
    {
        var (project, mapId) = SeedMap();
        var importer = Importer(FixturePolicy());
        var first = await importer.ImportAsync(
            new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile));

        // Identical package bytes → same content hash → no second file is created by staging;
        // the publication CAS still fails on the stale pin.
        await Assert.ThrowsAsync<CoreDomainConflictException>(() =>
            importer.ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile,
                    expectedRevision: Guid.NewGuid(),
                    datasetId: first.Revision.FieldDatasetId)));

        var core = Core();
        var dataset = core.FindFieldDataset(project.ProjectId, mapId, "elevation")!;
        Assert.Equal(first.Revision.FieldRevisionId, dataset.CurrentFieldRevisionId);
        Assert.Single(core.ListFieldRevisions(project.ProjectId, dataset.FieldDatasetId));

        // Distinct decoded tile content (different sample) staged by a failed import remains
        // as an orphan file with no authoritative reference, until reconciliation collects it.
        // (A manifest-only difference would produce the identical internal payload and
        // therefore no second file at all.)
        var otherPackage = BuildPackageBytes(BuildManifest(), sample0: 7.5f);
        Assert.NotEqual(
            Convert.ToHexString(SHA256.HashData(ValidPackage())).ToLowerInvariant(),
            Convert.ToHexString(SHA256.HashData(otherPackage)).ToLowerInvariant());
        await Assert.ThrowsAsync<CoreDomainConflictException>(() =>
            importer.ImportAsync(
                new MemoryStream(otherPackage),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile,
                    expectedRevision: Guid.NewGuid(),
                    datasetId: first.Revision.FieldDatasetId)));

        var reconciler = new FieldPayloadIntegrityReconciler(Core(), _fileStore);
        var collected = await reconciler.CollectOrphanFilesAsync(project.ProjectId);
        Assert.Single(collected);

        // The referenced payload of the successful import is retained.
        Assert.Equal(
            new[] { first.Revision.Payload.ContentHash },
            _fileStore.ListContentHashes());
    }

    [Fact]
    public async Task Import_IntoExistingDatasetWithWrongIdentityFailsWithoutSideEffects()
    {
        var (project, mapId) = SeedMap();
        var importer = Importer(FixturePolicy());
        var first = await importer.ImportAsync(
            new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        // Same descriptor into a fresh (allocated) dataset identity conflicts with the
        // authoritative row; nothing partial is written.
        await Assert.ThrowsAsync<CoreDomainDataException>(() =>
            importer.ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob,
                    expectedRevision: first.Revision.FieldRevisionId)));

        var core = Core();
        var revisions = core.ListFieldRevisions(project.ProjectId, first.Revision.FieldDatasetId);
        Assert.Single(revisions);
    }

    [Fact]
    public async Task Import_SequentialRevisionsBuildLineage()
    {
        var (project, mapId) = SeedMap();
        var importer = Importer(FixturePolicy());
        var first = await importer.ImportAsync(
            new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob));
        var second = await importer.ImportAsync(
            new MemoryStream(ValidPackage()),
            Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob,
                expectedRevision: first.Revision.FieldRevisionId,
                datasetId: first.Revision.FieldDatasetId));

        Assert.Equal(2, second.Revision.RevisionNumber);
        Assert.Equal(first.Revision.FieldRevisionId, second.Revision.ParentRevisionId);
        Assert.NotEqual(first.Revision.FieldRevisionId, second.Revision.FieldRevisionId);

        var core = Core();
        var dataset = core.FindFieldDataset(project.ProjectId, mapId, "elevation")!;
        Assert.Equal(second.Revision.FieldRevisionId, dataset.CurrentFieldRevisionId);
        Assert.Equal(2, core.ListFieldRevisions(project.ProjectId, dataset.FieldDatasetId).Count);
    }

    [Fact]
    public async Task Import_ManagedFileStagingFailureLeavesNoStagingOrRevision()
    {
        var (project, mapId) = SeedMap();
        // A package source whose staged tile reads fail mid-pass exercises the staging
        // cleanup path of the file store: validation succeeds, the ManagedFile staging pass
        // re-reads the tiles (cumulative reads then exceed two full package lengths) and the
        // injected failure fires inside the staging stream.
        var packageBytes = ValidPackage();
        var failingStream = new MidStagingFailureStream(packageBytes);

        // The staged tile reads are served through the production reader, so the injected
        // I/O failure surfaces typed (WVPK_IO); the staging cleanup semantics are unchanged.
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                failingStream, Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile)));
        Assert.Equal(FieldPackageBinaryErrorCodes.Io, error.Code);

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
        Assert.Equal(0, _fileStore.CleanupStagingFiles());
    }

    [Fact]
    public async Task Import_RejectsUnknownStorageKindAndUnreadableStream()
    {
        var (project, mapId) = SeedMap();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, (FieldPayloadStorageKind)99)));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                new NonSeekableStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob)));
    }

    // ---- Production profile gate (ME-05-12 §4) ----

    [Fact]
    public async Task Import_DefaultPolicyRejectsGoldenFixtureOnlyProfileWithoutSideEffects()
    {
        var (project, mapId) = SeedMap();
        // The built fixture manifest declares golden_fixture_only; the fail-closed default
        // policy must reject it before any byte is staged or any row is written.
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer().ImportAsync(
                new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob)));
        Assert.Equal(FieldPackagePolicyErrorCodes.GoldenFixtureProfileRejected, error.Code);

        error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Importer().ImportAsync(
                new MemoryStream(ValidPackage()), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile)));
        Assert.Equal(FieldPackagePolicyErrorCodes.GoldenFixtureProfileRejected, error.Code);

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
        Assert.Equal(0, _fileStore.CleanupStagingFiles());
    }

    [Fact]
    public async Task Import_ProductionProfileImportsUnderDefaultPolicy()
    {
        var (project, mapId) = SeedMap();
        // A production-scope package requires no special policy and imports normally.
        var manifest = BuildManifest()
            .Replace("\"profile_id\":\"fixture-only:elevation-f32-mask-v1\"", "\"profile_id\":\"prod:elevation-f32-mask-v1\"", StringComparison.Ordinal)
            .Replace("\"profile_scope\":\"golden_fixture_only\"", "\"profile_scope\":\"production\"", StringComparison.Ordinal);
        var packageBytes = BuildPackageBytes(manifest);

        var result = await Importer().ImportAsync(
            new MemoryStream(packageBytes), Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        Assert.Equal("production", result.Revision.Metadata!.ValueProfileScope);
        var core = Core();
        Assert.NotNull(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
    }

    // ---- Import cancellation (WRT-007 counterparts on the import path) ----

    [Fact]
    public async Task Import_PreCancelledTokenProducesNoSideEffects()
    {
        var (project, mapId) = SeedMap();
        var importer = Importer(FixturePolicy());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importer.ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob),
                cancellationToken: new CancellationToken(canceled: true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            importer.ImportAsync(
                new MemoryStream(ValidPackage()),
                Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile),
                cancellationToken: new CancellationToken(canceled: true)));

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
        Assert.Equal(0, _fileStore.CleanupStagingFiles());
    }

    [Fact]
    public async Task Import_CancelledDuringTileValidationLeavesNoSideEffects()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        var payloadOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(packageBytes.AsSpan(56)));
        // Open() reads exactly up to the payload section and the digest pass reads the whole
        // file; crossing five bytes into the verification tile read cancels inside the tile
        // verification stage.
        using var cts = new CancellationTokenSource();
        using var source = new ThresholdCancellationStream(
            packageBytes, payloadOffset + packageBytes.LongLength + 5, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                source, Plan(project.ProjectId, mapId, FieldPayloadStorageKind.SqliteBlob), cancellationToken: cts.Token));

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
    }

    [Fact]
    public async Task Import_CancelledDuringManagedFileStagingCleansStagingAndLeavesNoRevision()
    {
        var (project, mapId) = SeedMap();
        var packageBytes = ValidPackage();
        // Open + package digest consume the file twice; the staging pass re-reads the tiles.
        // Crossing five bytes into the staged tile stream cancels mid-staging.
        using var cts = new CancellationTokenSource();
        using var source = new ThresholdCancellationStream(packageBytes, 2 * packageBytes.LongLength + 5, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Importer(FixturePolicy()).ImportAsync(
                source, Plan(project.ProjectId, mapId, FieldPayloadStorageKind.ManagedFile), cancellationToken: cts.Token));

        var core = Core();
        Assert.Null(core.FindFieldDataset(project.ProjectId, mapId, "elevation"));
        Assert.Empty(core.ListFieldPayloads(project.ProjectId));
        Assert.Empty(_fileStore.ListContentHashes());
        Assert.Equal(0, _fileStore.CleanupStagingFiles());
    }

    /// <summary>
    /// Cancels the attached token once the cumulative bytes served by Read cross a threshold,
    /// so import cancellation can be staged deterministically inside a specific phase.
    /// </summary>
    private sealed class ThresholdCancellationStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly long _threshold;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private long _served;

        public ThresholdCancellationStream(byte[] bytes, long threshold, CancellationTokenSource cancellationTokenSource)
        {
            _inner = new MemoryStream(bytes);
            _threshold = threshold;
            _cancellationTokenSource = cancellationTokenSource;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            _served += read;
            if (_served >= _threshold)
                _cancellationTokenSource.Cancel();
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) => _inner.Dispose();
    }

    /// <summary>
    /// Succeeds for the open, package-digest and tile-verification passes (about two full
    /// package lengths of cumulative reads), then fails with an I/O error once the
    /// ManagedFile staging pass re-reads the payload tiles.
    /// </summary>
    private sealed class MidStagingFailureStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly long _threshold;
        private long _served;

        public MidStagingFailureStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes);
            _threshold = 2 * bytes.LongLength + 5;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            _served += read;
            if (_served >= _threshold)
                throw new IOException("Simulated mid-staging tile read failure.");
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) => _inner.Dispose();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] bytes) => _inner = new MemoryStream(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
