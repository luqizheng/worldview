using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Objects;
using WorldView.Domain.Maps;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// Map-owned Field Dataset identity and immutable published revisions.
/// Revision insert and current-pointer CAS share one short SQLite transaction.
/// </summary>
public sealed partial class SqliteCoreDomainDatabase
{
    public FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey) =>
        _ef.Execute(db =>
        {
            var entity = db.FieldDatasets.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId && x.MapId == mapId && x.FieldKey == fieldKey);
            return entity is null ? null : MapFieldDataset(entity);
        });

    public FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldDatasetId, Guid fieldRevisionId) =>
        _ef.Execute(db =>
        {
            var entity = db.FieldRevisions.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId
                && x.FieldDatasetId == fieldDatasetId
                && x.FieldRevisionId == fieldRevisionId);
            return entity is null ? null : MapFieldRevision(db, entity);
        });

    public IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId) =>
        _ef.Execute(db => (IReadOnlyList<FieldRevisionRecord>)db.FieldRevisions.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.FieldDatasetId == fieldDatasetId)
            .OrderBy(x => x.RevisionNumber)
            .AsEnumerable()
            .Select(entity => MapFieldRevision(db, entity))
            .ToArray());

    public FieldPayloadRecord? FindFieldPayload(Guid projectId, Guid fieldRevisionId) =>
        _ef.Execute(db =>
        {
            var entity = db.FieldPayloads.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId && x.FieldRevisionId == fieldRevisionId);
            return entity is null ? null : MapFieldPayload(entity);
        });

    public IReadOnlyList<FieldPayloadRecord> ListFieldPayloads(Guid projectId) =>
        _ef.Execute(db => (IReadOnlyList<FieldPayloadRecord>)db.FieldPayloads.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.FieldPayloadId)
            .AsEnumerable()
            .Select(MapFieldPayload)
            .ToArray());

    public void SetFieldPayloadIntegrityState(
        Guid projectId,
        Guid fieldPayloadId,
        FieldPayloadIntegrityState state) =>
        _ef.Execute(db =>
        {
            var entity = db.FieldPayloads.SingleOrDefault(x =>
                x.ProjectId == projectId && x.FieldPayloadId == fieldPayloadId)
                ?? throw new CoreDomainConflictException("Field Payload 不存在或不属于当前 Project。");
            entity.IntegrityState = state switch
            {
                FieldPayloadIntegrityState.Verified => "verified",
                FieldPayloadIntegrityState.Corrupt => "corrupt",
                FieldPayloadIntegrityState.Missing => "missing",
                _ => throw new ArgumentOutOfRangeException(nameof(state), "Unknown Field Payload integrity state."),
            };
            SaveChangesOrThrowSqlite(db);
        });

    public byte[]? ReadFieldPayloadBlob(Guid projectId, Guid fieldRevisionId) =>
        _ef.Execute(db =>
        {
            var entity = db.FieldPayloads.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId && x.FieldRevisionId == fieldRevisionId);
            if (entity is null)
                return null;
            if (entity.StorageKind != "sqlite_blob")
                throw new InvalidOperationException("Field Payload is stored as ManagedFile, not SQLite BLOB.");
            if (entity.PayloadBlob is null || entity.PayloadBlob.LongLength != entity.ByteLength)
                throw new CoreDomainDataException("SQLite BLOB Field Payload is missing or has an invalid byte length.");
            var hash = Convert.ToHexString(SHA256.HashData(entity.PayloadBlob)).ToLowerInvariant();
            if (!string.Equals(hash, entity.ContentHash, StringComparison.Ordinal))
                throw new CoreDomainDataException("SQLite BLOB Field Payload SHA-256 verification failed.");
            return entity.PayloadBlob.ToArray();
        });

    public FieldRevisionRecord PublishFieldRevision(PublishFieldRevisionPlan plan) =>
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var mapEntity = db.Maps.SingleOrDefault(x => x.ProjectId == plan.ProjectId && x.MapId == plan.MapId)
                    ?? throw new CoreDomainConflictException("目标 Map 不存在或不属于当前 Project。");
                if (string.IsNullOrWhiteSpace(mapEntity.SpatialReferenceId))
                    throw new CoreDomainConflictException("Map 尚未绑定 Spatial Reference，不能发布 Field Revision。");
                var spatialReferenceEntity = db.SpatialReferences.SingleOrDefault(x =>
                    x.ProjectId == plan.ProjectId && x.SpatialReferenceId == mapEntity.SpatialReferenceId)
                    ?? throw new CoreDomainConflictException("Map 的 Spatial Reference 定义不存在或不属于当前 Project。");
                // The Field Manifest authority is RFC 8785 JCS (FieldDatasetService). The legacy
                // CanonicalJson has a different string-escaping contract (it escapes '+', HTML-
                // sensitive and non-ASCII characters), so it rejects valid canonical manifests.
                var canonicalManifest = FieldManifestCanonicalizer.Canonicalize(plan.ManifestJson);
                if (!string.Equals(canonicalManifest, plan.ManifestJson, StringComparison.Ordinal))
                    throw new CoreDomainDataException(
                        "Field Revision ManifestJson must be byte-identical RFC 8785 canonical UTF-8 before persistence.");
                var manifestMetadata = FieldRevisionMetadata.FromValidatedManifest(canonicalManifest);
                if (plan.Metadata is null || !manifestMetadata.StructurallyEquals(plan.Metadata))
                    throw new CoreDomainConflictException("Field Revision typed metadata does not match its canonical manifest.");
                if (!string.Equals(manifestMetadata.FieldKey, plan.FieldKey, StringComparison.Ordinal)
                    || manifestMetadata.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                    || !string.Equals(manifestMetadata.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                    throw new CoreDomainConflictException("Field Revision typed descriptor pins do not match the publication plan.");
                SpatialReferenceRules.ValidateFieldOwnerContext(
                    MapMap(mapEntity), MapSpatialReference(spatialReferenceEntity), manifestMetadata);

                var payloadRecord = FieldPayloadRules.Create(plan.Payload);
                if (payloadRecord.ProjectId != plan.ProjectId
                    || payloadRecord.FieldDatasetId != plan.FieldDatasetId
                    || payloadRecord.FieldRevisionId != plan.FieldRevisionId)
                    throw new CoreDomainConflictException("Field Payload owner IDs must match the Field Revision publication.");
                if (payloadRecord.StorageKind == FieldPayloadStorageKind.SqliteBlob)
                {
                    if (plan.PayloadBytes is null || plan.PayloadBytes.LongLength != payloadRecord.ByteLength)
                        throw new CoreDomainConflictException("SQLite BLOB payload bytes must be present and match the registered byte length.");
                    var actualHash = Convert.ToHexString(SHA256.HashData(plan.PayloadBytes)).ToLowerInvariant();
                    if (!string.Equals(actualHash, payloadRecord.ContentHash, StringComparison.Ordinal))
                        throw new CoreDomainConflictException("SQLite BLOB payload bytes do not match the registered SHA-256.");
                }
                else if (plan.PayloadBytes is not null)
                {
                    throw new CoreDomainConflictException("ManagedFile payload registration must not also carry inline BLOB bytes.");
                }

                var dataset = db.FieldDatasets.SingleOrDefault(x =>
                    x.ProjectId == plan.ProjectId
                    && x.MapId == plan.MapId
                    && x.FieldKey == plan.FieldKey);
                if (dataset is null)
                {
                    if (plan.ExpectedCurrentFieldRevisionId is not null)
                        throw new CoreDomainConflictException("首次 publication 的 expected current revision 必须为 null。");

                    dataset = new CoreMapFieldDatasetEntity
                    {
                        FieldDatasetId = plan.FieldDatasetId,
                        ProjectId = plan.ProjectId,
                        MapId = plan.MapId,
                        FieldKey = plan.FieldKey,
                        DescriptorSchemaVersion = plan.DescriptorSchemaVersion,
                        SemanticsId = plan.SemanticsId,
                        CurrentFieldRevisionId = null,
                        CreatedAtUtcMs = plan.CreatedAtUtcMs,
                        UpdatedAtUtcMs = plan.CreatedAtUtcMs,
                    };
                    db.FieldDatasets.Add(dataset);
                    SaveChangesOrThrowSqlite(db);
                }
                else
                {
                    if (dataset.FieldDatasetId != plan.FieldDatasetId)
                        throw new CoreDomainDataException("Field Dataset ID 与 Map + field key 的既有权威行不一致。");
                    if (dataset.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                        || !string.Equals(dataset.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                        throw new CoreDomainDataException("Field Dataset descriptor pin 不可变，拒绝不兼容 revision。");
                    if (dataset.CurrentFieldRevisionId != plan.ExpectedCurrentFieldRevisionId)
                        throw new CoreDomainConflictException("Field Revision publication CAS 冲突：current pointer 已变化。");
                }

                var nextRevisionNumber = db.FieldRevisions
                    .Where(x => x.ProjectId == plan.ProjectId && x.FieldDatasetId == plan.FieldDatasetId)
                    .Select(x => (long?)x.RevisionNumber)
                    .Max() ?? 0;
                nextRevisionNumber++;

                var revision = new CoreMapFieldRevisionEntity
                {
                    FieldRevisionId = plan.FieldRevisionId,
                    ProjectId = plan.ProjectId,
                    FieldDatasetId = plan.FieldDatasetId,
                    RevisionNumber = nextRevisionNumber,
                    ParentRevisionId = dataset.CurrentFieldRevisionId,
                    ContentFingerprint = plan.ContentFingerprint,
                    ManifestJson = plan.ManifestJson,
                    ProvenanceJson = plan.ProvenanceJson,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                    FieldKey = manifestMetadata.FieldKey,
                    DescriptorSchemaVersion = manifestMetadata.DescriptorSchemaVersion,
                    SemanticsId = manifestMetadata.SemanticsId,
                    Unit = manifestMetadata.Unit,
                    MapSpaceId = manifestMetadata.MapSpaceId,
                    SpatialReferenceId = manifestMetadata.SpatialReferenceId,
                    CoordinatePrecisionProfileId = manifestMetadata.CoordinatePrecisionProfileId,
                    VerticalReferenceId = manifestMetadata.VerticalReferenceId,
                    GridWidth = manifestMetadata.Grid.Width,
                    GridHeight = manifestMetadata.Grid.Height,
                    GridTileWidth = manifestMetadata.Grid.TileWidth,
                    GridTileHeight = manifestMetadata.Grid.TileHeight,
                    GridOriginX = manifestMetadata.Grid.OriginX,
                    GridOriginY = manifestMetadata.Grid.OriginY,
                    GridSpacingX = manifestMetadata.Grid.SpacingX,
                    GridSpacingY = manifestMetadata.Grid.SpacingY,
                    GridAxisOrientation = manifestMetadata.Grid.AxisOrientation,
                    GridCellRegistration = manifestMetadata.Grid.CellRegistration,
                    CoverageMode = manifestMetadata.Coverage.Mode,
                    CoverageMaskPlaneName = manifestMetadata.Coverage.MaskPlaneName,
                    MapEdgeSemantics = manifestMetadata.MapEdgeSemantics,
                    NoDataPolicy = manifestMetadata.NoData.Policy,
                    NoDataMaskPlaneName = manifestMetadata.NoData.MaskPlaneName,
                    NoDataValidValue = manifestMetadata.NoData.ValidValue,
                    NoDataInvalidValue = manifestMetadata.NoData.InvalidValue,
                    ValueProfileId = manifestMetadata.ValueProfileId,
                    ValueProfileScope = manifestMetadata.ValueProfileScope,
                    TileHashAlgorithm = manifestMetadata.TileHashAlgorithm,
                    TileHashVersion = manifestMetadata.TileHashVersion,
                };
                db.FieldRevisions.Add(revision);
                db.FieldRevisionPlanes.AddRange(manifestMetadata.Planes.Select((plane, ordinal) =>
                    new CoreMapFieldRevisionPlaneEntity
                    {
                        FieldRevisionId = revision.FieldRevisionId,
                        Ordinal = ordinal,
                        Name = plane.Name,
                        SampleFormat = plane.SampleFormat,
                        BytesPerCell = plane.BytesPerCell,
                    }));
                db.FieldRevisionCategoryCodes.AddRange(manifestMetadata.CategoryCodes.Select(pair =>
                    new CoreMapFieldRevisionCategoryCodeEntity
                    {
                        FieldRevisionId = revision.FieldRevisionId,
                        CategoryKey = pair.Key,
                        CategoryCode = pair.Value,
                    }));
                db.FieldRevisionCodecs.AddRange(manifestMetadata.Codecs.Select((codec, ordinal) =>
                    new CoreMapFieldRevisionCodecEntity
                    {
                        FieldRevisionId = revision.FieldRevisionId,
                        Ordinal = ordinal,
                        CodecId = codec.CodecId,
                        CodecName = codec.CodecName,
                        Version = codec.Version,
                        ParametersJson = codec.ParametersJson,
                    }));

                db.FieldPayloads.Add(new CoreFieldPayloadEntity
                {
                    FieldPayloadId = payloadRecord.FieldPayloadId,
                    ProjectId = payloadRecord.ProjectId,
                    FieldDatasetId = payloadRecord.FieldDatasetId,
                    FieldRevisionId = payloadRecord.FieldRevisionId,
                    StorageKind = payloadRecord.StorageKind == FieldPayloadStorageKind.SqliteBlob ? "sqlite_blob" : "managed_file",
                    PayloadFormat = payloadRecord.PayloadFormat,
                    FormatVersion = payloadRecord.FormatVersion,
                    HashAlgorithm = payloadRecord.HashAlgorithm,
                    ContentHash = payloadRecord.ContentHash,
                    ByteLength = payloadRecord.ByteLength,
                    PayloadBlob = plan.PayloadBytes?.ToArray(),
                    RelativePath = payloadRecord.RelativePath,
                    IntegrityState = "verified",
                    CreatedAtUtcMs = payloadRecord.CreatedAtUtcMs,
                });
                SaveChangesOrThrowSqlite(db);
                // CurrentFieldRevisionId is an EF concurrency token. The generated UPDATE
                // includes the original pointer (including IS NULL on first publication).
                dataset.CurrentFieldRevisionId = revision.FieldRevisionId;
                dataset.UpdatedAtUtcMs = plan.CreatedAtUtcMs;
                try
                {
                    SaveChangesOrThrowSqlite(db);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    throw new CoreDomainConflictException(
                        "Field Revision publication CAS 冲突：current pointer 已被其他写入推进。", ex);
                }

                var publishedRevision = MapFieldRevision(db, revision);
                transaction.Commit();
                return publishedRevision;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                transaction.Rollback();
                throw new CoreDomainConflictException(
                    "Field Revision publication CAS 冲突：current pointer 已被其他写入推进。", ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
            {
                transaction.Rollback();
                throw sqlite;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });

    private static FieldDatasetRecord MapFieldDataset(CoreMapFieldDatasetEntity entity)
    {
        if (!FieldDescriptorRegistry.TryGetSemanticsId(
                entity.FieldKey, entity.DescriptorSchemaVersion, out var expectedSemanticsId)
            || !string.Equals(entity.SemanticsId, expectedSemanticsId, StringComparison.Ordinal))
            throw new CoreDomainDataException(
                $"Field Dataset {entity.FieldDatasetId:D} contains an unknown or incompatible descriptor pin.");

        return new FieldDatasetRecord
        {
            FieldDatasetId = entity.FieldDatasetId,
            ProjectId = entity.ProjectId,
            MapId = entity.MapId,
            FieldKey = entity.FieldKey,
            DescriptorSchemaVersion = entity.DescriptorSchemaVersion,
            SemanticsId = entity.SemanticsId,
            CurrentFieldRevisionId = entity.CurrentFieldRevisionId,
            CreatedAtUtcMs = entity.CreatedAtUtcMs,
            UpdatedAtUtcMs = entity.UpdatedAtUtcMs,
        };
    }

    private static FieldRevisionRecord MapFieldRevision(CoreDomainDbContext db, CoreMapFieldRevisionEntity entity)
    {
        if (entity.RevisionNumber < 1 || !AssetRules.IsSha256Hex(entity.ContentFingerprint))
            throw new CoreDomainDataException(
                $"Field Revision {entity.FieldRevisionId:D} has an invalid revision number or content fingerprint.");

        var provenance = CanonicalJson.Canonicalize(entity.ProvenanceJson);
        if (!string.Equals(provenance, entity.ProvenanceJson, StringComparison.Ordinal))
            throw new CoreDomainDataException(
                $"Field Revision {entity.FieldRevisionId:D} contains non-canonical provenance JSON.");

        var planeRows = db.FieldRevisionPlanes.AsNoTracking()
            .Where(x => x.FieldRevisionId == entity.FieldRevisionId)
            .OrderBy(x => x.Ordinal)
            .ToArray();
        if (planeRows.Where((row, index) => row.Ordinal != index).Any())
            throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has non-contiguous plane ordinals.");

        var categoryRows = db.FieldRevisionCategoryCodes.AsNoTracking()
            .Where(x => x.FieldRevisionId == entity.FieldRevisionId)
            .ToArray()
            .OrderBy(x => x.CategoryKey, StringComparer.Ordinal)
            .ToArray();
        var categories = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in categoryRows)
        {
            if (string.IsNullOrWhiteSpace(row.CategoryKey) || !categories.TryAdd(row.CategoryKey, row.CategoryCode))
                throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has invalid or duplicate category-code rows.");
        }

        var codecRows = db.FieldRevisionCodecs.AsNoTracking()
            .Where(x => x.FieldRevisionId == entity.FieldRevisionId)
            .OrderBy(x => x.Ordinal)
            .ToArray();
        if (codecRows.Where((row, index) => row.Ordinal != index).Any())
            throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has non-contiguous codec ordinals.");

        var codecs = codecRows.Select(row =>
        {
            // ParametersJson is stored as the exact canonical-manifest bytes of the codec
            // parameters object (see FieldRevisionMetadata); verify against the same RFC 8785
            // authority rather than the legacy CanonicalJson, whose number spelling (1E-07)
            // and out-of-Int64 collapse do not round-trip JCS output.
            var parameters = FieldManifestCanonicalizer.Canonicalize(row.ParametersJson);
            if (!string.Equals(parameters, row.ParametersJson, StringComparison.Ordinal))
                throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has non-canonical codec parameters.");
            return new FieldCodecMetadata(row.CodecId, row.CodecName, row.Version, parameters);
        }).ToArray();

        if (!FieldDescriptorRegistry.TryGetSemanticsId(
                entity.FieldKey, entity.DescriptorSchemaVersion, out var expectedSemanticsId)
            || !string.Equals(entity.SemanticsId, expectedSemanticsId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entity.MapSpaceId)
            || string.IsNullOrWhiteSpace(entity.SpatialReferenceId)
            || string.IsNullOrWhiteSpace(entity.CoordinatePrecisionProfileId)
            || entity.GridWidth < 1 || entity.GridHeight < 1
            || entity.GridTileWidth < 1 || entity.GridTileHeight < 1
            || entity.TileHashVersion < 1)
            throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has invalid typed semantic columns.");

        var metadata = new FieldRevisionMetadata
        {
            FieldKey = entity.FieldKey,
            DescriptorSchemaVersion = entity.DescriptorSchemaVersion,
            SemanticsId = entity.SemanticsId,
            Unit = entity.Unit,
            MapSpaceId = entity.MapSpaceId,
            SpatialReferenceId = entity.SpatialReferenceId,
            CoordinatePrecisionProfileId = entity.CoordinatePrecisionProfileId,
            VerticalReferenceId = entity.VerticalReferenceId,
            Grid = new FieldGridMetadata(
                entity.GridWidth, entity.GridHeight, entity.GridTileWidth, entity.GridTileHeight,
                entity.GridOriginX, entity.GridOriginY, entity.GridSpacingX, entity.GridSpacingY,
                entity.GridAxisOrientation, entity.GridCellRegistration),
            Coverage = new FieldCoverageMetadata(entity.CoverageMode, entity.CoverageMaskPlaneName),
            MapEdgeSemantics = entity.MapEdgeSemantics,
            NoData = new FieldNoDataMetadata(
                entity.NoDataPolicy, entity.NoDataMaskPlaneName, entity.NoDataValidValue, entity.NoDataInvalidValue),
            ValueProfileId = entity.ValueProfileId,
            ValueProfileScope = entity.ValueProfileScope,
            Planes = Array.AsReadOnly(planeRows.Select(row =>
                new FieldValuePlaneMetadata(row.Name, row.SampleFormat, row.BytesPerCell)).ToArray()),
            CategoryCodes = new ReadOnlyDictionary<string, int>(categories),
            Codecs = Array.AsReadOnly(codecs),
            TileHashAlgorithm = entity.TileHashAlgorithm,
            TileHashVersion = entity.TileHashVersion,
        };

        var payloadEntity = db.FieldPayloads.AsNoTracking().SingleOrDefault(x =>
            x.ProjectId == entity.ProjectId && x.FieldRevisionId == entity.FieldRevisionId)
            ?? throw new CoreDomainDataException($"Field Revision {entity.FieldRevisionId:D} has no registered Field Payload.");
        var payloadRecord = MapFieldPayload(payloadEntity);

        return new FieldRevisionRecord
        {
            FieldRevisionId = entity.FieldRevisionId,
            ProjectId = entity.ProjectId,
            FieldDatasetId = entity.FieldDatasetId,
            RevisionNumber = entity.RevisionNumber,
            ParentRevisionId = entity.ParentRevisionId,
            Payload = payloadRecord,
            ContentFingerprint = entity.ContentFingerprint,
            ManifestJson = entity.ManifestJson,
            Metadata = metadata,
            ProvenanceJson = provenance,
            CreatedAtUtcMs = entity.CreatedAtUtcMs,
        };
    }

    private static FieldPayloadRecord MapFieldPayload(CoreFieldPayloadEntity entity)
    {
        var storageKind = entity.StorageKind switch
        {
            "sqlite_blob" => FieldPayloadStorageKind.SqliteBlob,
            "managed_file" => FieldPayloadStorageKind.ManagedFile,
            _ => throw new CoreDomainDataException($"Unknown Field Payload storage kind '{entity.StorageKind}'."),
        };
        var integrity = entity.IntegrityState switch
        {
            "verified" => FieldPayloadIntegrityState.Verified,
            "corrupt" => FieldPayloadIntegrityState.Corrupt,
            "missing" => FieldPayloadIntegrityState.Missing,
            _ => throw new CoreDomainDataException($"Unknown Field Payload integrity state '{entity.IntegrityState}'."),
        };
        var plan = new RegisterFieldPayloadPlan
        {
            FieldPayloadId = entity.FieldPayloadId,
            ProjectId = entity.ProjectId,
            FieldDatasetId = entity.FieldDatasetId,
            FieldRevisionId = entity.FieldRevisionId,
            StorageKind = storageKind,
            PayloadFormat = entity.PayloadFormat,
            FormatVersion = entity.FormatVersion,
            HashAlgorithm = entity.HashAlgorithm,
            ByteLength = entity.ByteLength,
            ContentHash = entity.ContentHash,
            RelativePath = entity.RelativePath,
            CreatedAtUtcMs = entity.CreatedAtUtcMs,
        };
        return FieldPayloadRules.Create(plan) with { IntegrityState = integrity };
    }
}
