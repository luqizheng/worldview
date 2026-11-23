using System;
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
/// Authority-boundary proof for Field Package import/export (ME-05-12 §4, §6, §7): the
/// internal Field Payload is the decoded tile concatenation (never the WVFLDPK file), the
/// package digest stays separate from payload hashes, export reconstructs packages from the
/// authoritative revision deterministically, and the golden fixtures round-trip byte for
/// byte. Real SQLite + real filesystem throughout.
/// </summary>
public sealed class FieldPackageImportExportAuthorityTests : IDisposable
{
    private const long Now = 9_000L;

    private readonly string _root;
    private readonly SqlitePackageDatabase _database;
    private readonly FieldPayloadFileStore _fileStore;

    public FieldPackageImportExportAuthorityTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-field-authority-{Guid.NewGuid():N}");
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

    private static FieldPackagePolicy FixturePolicy() => new() { AllowGoldenFixtureOnlyProfiles = true };

    private FieldPackageImporter Importer(FieldPackagePolicy? policy = null) =>
        new(Core(), _fileStore, policy: policy);

    private FieldPackageExporter Exporter(FieldPackagePolicy? policy = null) =>
        new(Core(), _fileStore, policy: policy);

    private (Guid ProjectId, Guid MapId) SeedMap()
    {
        var core = Core();
        var project = ProjectRules.CreateProject("Authority test", Now);
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
        return (project.ProjectId, mapPlan.Map.MapId);
    }

    private static ImportFieldPackagePlan ImportPlan(Guid projectId, Guid mapId, FieldPayloadStorageKind kind) => new()
    {
        ProjectId = projectId,
        MapId = mapId,
        StorageKind = kind,
        ProvenanceJson = """{"source":"field-authority-test"}""",
        CreatedAtUtcMs = Now + 5,
    };

    private static byte[] ReadResource(string suffix)
    {
        var assembly = typeof(FieldPackageImportExportAuthorityTests).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(suffix, StringComparison.Ordinal))!;
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Concatenated decoded tiles of a package in canonical order, read via the production reader.</summary>
    private static byte[] CanonicalPayloadOf(byte[] packageBytes)
    {
        using var reader = FieldPackageReader.Open(new MemoryStream(packageBytes), leaveOpen: true);
        using var payload = new MemoryStream();
        var columns = (reader.GridWidth + reader.TileWidth - 1) / reader.TileWidth;
        var rows = (reader.GridHeight + reader.TileHeight - 1) / reader.TileHeight;
        for (uint y = 0; y < rows; y++)
            for (uint x = 0; x < columns; x++)
                payload.Write(reader.ReadTile(x, y));
        return payload.ToArray();
    }

    private string NewExportPath(string name) => Path.Combine(_root, $"{name}-{Guid.NewGuid():N}.wvfld");

    // ---- Round trip: golden package → import → internal payload → export → golden package ----

    [Fact]
    public async Task RoundTrip_GoldenMinimal_BlobImport_ExportIsByteIdentical()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("minimal-elevation-2x2.wvfld");
        var goldenManifest = ReadResource("minimal-elevation-2x2.manifest.json");

        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        // Internal payload: decoded tile concatenation, independent digest.
        var expectedPayload = CanonicalPayloadOf(golden);
        Assert.NotEqual(golden, expectedPayload);
        Assert.Equal(expectedPayload, Core().ReadFieldPayloadBlob(projectId, import.Revision.FieldRevisionId));
        Assert.Equal(Sha256(expectedPayload), import.PayloadSha256);
        Assert.Equal(expectedPayload.LongLength, import.PayloadByteLength);
        Assert.NotEqual(import.PackageSha256, import.PayloadSha256);
        Assert.Equal(goldenManifest, Encoding.UTF8.GetBytes(import.Revision.ManifestJson));

        var exportPath = NewExportPath("minimal");
        var export = await Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = import.Revision.FieldDatasetId,
            FieldRevisionId = import.Revision.FieldRevisionId,
            DestinationPath = exportPath,
        });

        // Byte-identical golden reproduction, including the package digest.
        Assert.Equal(golden, File.ReadAllBytes(exportPath));
        Assert.Equal(Sha256(golden), export.PackageSha256);
        Assert.Equal(golden.LongLength, export.PackageByteLength);
        Assert.Equal(1, export.TileCount);

        // The exported package reopens with the production reader and the tiles are identical.
        using var reopened = FieldPackageReader.Open(new MemoryStream(File.ReadAllBytes(exportPath)), leaveOpen: true);
        using var original = FieldPackageReader.Open(new MemoryStream(golden), leaveOpen: true);
        Assert.Equal(original.ReadTile(0, 0), reopened.ReadTile(0, 0));
        Assert.Equal(original.ManifestUtf8.ToArray(), reopened.ManifestUtf8.ToArray());
    }

    [Fact]
    public async Task RoundTrip_GoldenEdge_ManagedFileImport_ExportIsByteIdentical()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("edge-elevation-3x2.wvfld");

        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.ManagedFile));

        // Two edge tiles (2x2 and 1x2): the stored ManagedFile is exactly their concatenation.
        var expectedPayload = CanonicalPayloadOf(golden);
        Assert.Equal(expectedPayload.LongLength, import.PayloadByteLength);
        await using var stored = _fileStore.OpenReadAsync(import.Revision.Payload.ContentHash).GetAwaiter().GetResult()!;
        using var storedBytes = new MemoryStream();
        stored.CopyTo(storedBytes);
        Assert.Equal(expectedPayload, storedBytes.ToArray());
        Assert.NotEqual(golden, storedBytes.ToArray());

        var exportPath = NewExportPath("edge");
        await Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = import.Revision.FieldDatasetId,
            FieldRevisionId = import.Revision.FieldRevisionId,
            DestinationPath = exportPath,
        });

        Assert.Equal(golden, File.ReadAllBytes(exportPath));
        Assert.Equal(Sha256(golden), import.PackageSha256);

        // Per-tile equality after the full round trip.
        using var reopened = FieldPackageReader.Open(new MemoryStream(File.ReadAllBytes(exportPath)), leaveOpen: true);
        using var original = FieldPackageReader.Open(new MemoryStream(golden), leaveOpen: true);
        Assert.Equal(original.ReadTile(0, 0), reopened.ReadTile(0, 0));
        Assert.Equal(original.ReadTile(1, 0), reopened.ReadTile(1, 0));
        Assert.Equal(original.ManifestUtf8.ToArray(), reopened.ManifestUtf8.ToArray());
    }

    // ---- Export semantics ----

    [Fact]
    public async Task Export_DoesNotChangeDatasetCurrentPointerOrRevisionRows()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("minimal-elevation-2x2.wvfld");
        var first = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob));
        var second = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob) with
            {
                FieldDatasetId = first.Revision.FieldDatasetId,
                ExpectedCurrentFieldRevisionId = first.Revision.FieldRevisionId,
            });

        var datasetBefore = Core().FindFieldDataset(projectId, mapId, "elevation")!;

        await Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = second.Revision.FieldDatasetId,
            FieldRevisionId = second.Revision.FieldRevisionId,
            DestinationPath = NewExportPath("pointer"),
        });

        var core = Core();
        var datasetAfter = core.FindFieldDataset(projectId, mapId, "elevation")!;
        Assert.Equal(datasetBefore.CurrentFieldRevisionId, datasetAfter.CurrentFieldRevisionId);
        Assert.Equal(second.Revision.FieldRevisionId, datasetAfter.CurrentFieldRevisionId);
        Assert.Equal(2, core.ListFieldRevisions(projectId, second.Revision.FieldDatasetId).Count);
        Assert.Equal(2, core.ListFieldPayloads(projectId).Count);
    }

    [Fact]
    public async Task Export_RepeatedExportsOfSameRevisionAreDeterministic()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("edge-elevation-3x2.wvfld");
        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.ManagedFile));

        var pathOne = NewExportPath("det-a");
        var pathTwo = NewExportPath("det-b");
        var plan = new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = import.Revision.FieldDatasetId,
            FieldRevisionId = import.Revision.FieldRevisionId,
            DestinationPath = pathOne,
        };
        var first = await Exporter(FixturePolicy()).ExportAsync(plan with { DestinationPath = pathOne });
        var second = await Exporter(FixturePolicy()).ExportAsync(plan with { DestinationPath = pathTwo });

        Assert.Equal(File.ReadAllBytes(pathOne), File.ReadAllBytes(pathTwo));
        Assert.Equal(first.PackageSha256, second.PackageSha256);
        Assert.Equal(first.PackageByteLength, second.PackageByteLength);
    }

    [Fact]
    public async Task Export_RefusesToOverwriteExistingPackage()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("minimal-elevation-2x2.wvfld");
        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        var exportPath = NewExportPath("immutable");
        await Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = import.Revision.FieldDatasetId,
            FieldRevisionId = import.Revision.FieldRevisionId,
            DestinationPath = exportPath,
        });
        var published = File.ReadAllBytes(exportPath);

        Assert.Throws<IOException>(() => Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
        {
            ProjectId = projectId,
            FieldDatasetId = import.Revision.FieldDatasetId,
            FieldRevisionId = import.Revision.FieldRevisionId,
            DestinationPath = exportPath,
        }).GetAwaiter().GetResult());
        Assert.Equal(published, File.ReadAllBytes(exportPath));
    }

    [Fact]
    public async Task Export_DefaultPolicyRejectsGoldenFixtureRevisionAndCreatesNoFile()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("minimal-elevation-2x2.wvfld");
        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        var exportPath = NewExportPath("gated");
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Exporter().ExportAsync(new ExportFieldPackagePlan
            {
                ProjectId = projectId,
                FieldDatasetId = import.Revision.FieldDatasetId,
                FieldRevisionId = import.Revision.FieldRevisionId,
                DestinationPath = exportPath,
            }));
        Assert.Equal(FieldPackagePolicyErrorCodes.GoldenFixtureProfileRejected, error.Code);
        Assert.False(File.Exists(exportPath));
    }

    [Fact]
    public async Task Export_MissingRevisionFailsTyped()
    {
        var (projectId, mapId) = SeedMap();
        var exportPath = NewExportPath("missing");
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Exporter().ExportAsync(new ExportFieldPackagePlan
            {
                ProjectId = projectId,
                FieldDatasetId = Guid.NewGuid(),
                FieldRevisionId = Guid.NewGuid(),
                DestinationPath = exportPath,
            }));
        Assert.Equal(FieldPackageExportErrorCodes.RevisionMissing, error.Code);
        Assert.False(File.Exists(exportPath));
    }

    [Fact]
    public async Task Export_PayloadLengthMismatchWithGridPlanFailsTyped()
    {
        var (projectId, mapId) = SeedMap();
        // Publish a revision whose payload holds only the first edge tile while the manifest
        // grid requires two tiles: the export must catch the plan/length disagreement.
        var edgeManifest = Encoding.UTF8.GetString(ReadResource("edge-elevation-3x2.manifest.json"));
        var golden = ReadResource("edge-elevation-3x2.wvfld");
        var firstTileOnly = CanonicalPayloadOf(golden).AsSpan(0, 20).ToArray();
        var revisionId = PublishRawRevision(projectId, mapId, edgeManifest, firstTileOnly,
            FieldInternalPayloadFormat.PayloadFormat);

        var exportPath = NewExportPath("mismatch");
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
            {
                ProjectId = projectId,
                FieldDatasetId = Core().FindFieldDataset(projectId, mapId, "elevation")!.FieldDatasetId,
                FieldRevisionId = revisionId,
                DestinationPath = exportPath,
            }));
        Assert.Equal(FieldPackageExportErrorCodes.PayloadLengthMismatch, error.Code);
        Assert.False(File.Exists(exportPath));
    }

    [Fact]
    public async Task Export_UnknownPayloadFormatFailsTyped()
    {
        var (projectId, mapId) = SeedMap();
        var edgeManifest = Encoding.UTF8.GetString(ReadResource("edge-elevation-3x2.manifest.json"));
        var golden = ReadResource("edge-elevation-3x2.wvfld");
        var revisionId = PublishRawRevision(projectId, mapId, edgeManifest, CanonicalPayloadOf(golden),
            "application/vnd.worldview.unknown-payload");

        var exportPath = NewExportPath("format");
        var error = await Assert.ThrowsAsync<FieldPackageValidationException>(() =>
            Exporter(FixturePolicy()).ExportAsync(new ExportFieldPackagePlan
            {
                ProjectId = projectId,
                FieldDatasetId = Core().FindFieldDataset(projectId, mapId, "elevation")!.FieldDatasetId,
                FieldRevisionId = revisionId,
                DestinationPath = exportPath,
            }));
        Assert.Equal(FieldPackageExportErrorCodes.PayloadFormatUnsupported, error.Code);
        Assert.False(File.Exists(exportPath));
    }

    [Fact]
    public async Task Export_PreCancelledTokenLeavesNoDestinationFile()
    {
        var (projectId, mapId) = SeedMap();
        var golden = ReadResource("minimal-elevation-2x2.wvfld");
        var import = await Importer(FixturePolicy()).ImportAsync(
            new MemoryStream(golden), ImportPlan(projectId, mapId, FieldPayloadStorageKind.SqliteBlob));

        var exportPath = NewExportPath("cancel");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Exporter(FixturePolicy()).ExportAsync(
                new ExportFieldPackagePlan
                {
                    ProjectId = projectId,
                    FieldDatasetId = import.Revision.FieldDatasetId,
                    FieldRevisionId = import.Revision.FieldRevisionId,
                    DestinationPath = exportPath,
                },
                cancellationToken: new CancellationToken(canceled: true)));

        Assert.False(File.Exists(exportPath));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.wvfld.*"));
    }

    /// <summary>
    /// Publishes a revision directly through the publication authority with an arbitrary
    /// payload/format, to exercise export defect guards that the import path cannot produce.
    /// </summary>
    private Guid PublishRawRevision(
        Guid projectId, Guid mapId, string manifestJson, byte[] payloadBytes, string payloadFormat)
    {
        using var document = System.Text.Json.JsonDocument.Parse(manifestJson);
        var field = document.RootElement.GetProperty("field");
        var datasetId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        _ = FieldDatasetService.PublishRevision(Core(), new PublishFieldRevisionPlan
        {
            ProjectId = projectId,
            MapId = mapId,
            FieldDatasetId = datasetId,
            FieldKey = field.GetProperty("field_key").GetString()!,
            DescriptorSchemaVersion = field.GetProperty("schema_version").GetInt32(),
            SemanticsId = field.GetProperty("semantics_id").GetString()!,
            FieldRevisionId = revisionId,
            Payload = new RegisterFieldPayloadPlan
            {
                FieldPayloadId = Guid.NewGuid(),
                ProjectId = projectId,
                FieldDatasetId = datasetId,
                FieldRevisionId = revisionId,
                StorageKind = FieldPayloadStorageKind.SqliteBlob,
                PayloadFormat = payloadFormat,
                FormatVersion = FieldInternalPayloadFormat.FormatVersion,
                HashAlgorithm = "sha-256",
                ByteLength = payloadBytes.LongLength,
                ContentHash = Sha256(payloadBytes),
                CreatedAtUtcMs = Now + 6,
            },
            PayloadBytes = payloadBytes,
            ContentFingerprint = Sha256(payloadBytes),
            ManifestJson = manifestJson,
            ProvenanceJson = """{"source":"field-authority-raw"}""",
            CreatedAtUtcMs = Now + 6,
        });
        return revisionId;
    }
}
