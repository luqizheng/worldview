using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>SQLite persistence for Map-owned Field Dataset identity and immutable Field Revisions.</summary>
public sealed partial class SqliteCoreDomainDatabase
{
    public FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadFieldDataset(transaction, projectId, mapId, fieldKey);
        });

    public FieldRevisionRecord? FindCurrentFieldRevision(Guid projectId, Guid mapId, string fieldKey) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            var dataset = TryReadFieldDataset(transaction, projectId, mapId, fieldKey);
            return dataset?.CurrentFieldRevisionId is Guid revisionId
                ? TryReadFieldRevision(transaction, projectId, revisionId)
                : null;
        });

    public FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldRevisionId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadFieldRevision(transaction, projectId, fieldRevisionId);
        });

    public IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId)
    {
        const string sql = """
            SELECT field_revision_id, project_id, field_dataset_id, revision_number,
                   parent_revision_id, field_revision_asset_id, content_fingerprint,
                   manifest_json, provenance_json, created_at_utc_ms
            FROM core_map_field_revision
            WHERE project_id = $projectId AND field_dataset_id = $datasetId
            ORDER BY revision_number;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$datasetId", Uuid(fieldDatasetId));
            using var reader = command.ExecuteReader();
            var rows = new List<FieldRevisionRecord>();
            while (reader.Read())
            {
                rows.Add(MapFieldRevision(reader));
            }

            return (IReadOnlyList<FieldRevisionRecord>)rows;
        });
    }

    public FieldRevisionRecord PublishFieldRevision(FieldRevisionPublishPlan plan)
    {
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // Re-read every mutable precondition inside the write transaction.
                if (TryReadMapRow(transaction, plan.ProjectId, plan.MapId) is null)
                {
                    throw new CoreDomainConflictException("目标 Map 不存在或不属于当前 Project。");
                }

                var asset = TryReadAssetRow(transaction, plan.ProjectId, plan.FieldRevisionAssetId)
                    ?? throw new CoreDomainConflictException("Field Revision Asset 不存在或不属于当前 Project。");
                if (asset.PublicationState != WorldView.Domain.Assets.AssetPublicationState.Published
                    || asset.IntegrityState != WorldView.Domain.Assets.AssetIntegrityState.Verified)
                {
                    throw new CoreDomainConflictException("Field Revision Asset 必须为 Published + Verified。");
                }

                var dataset = TryReadFieldDataset(transaction, plan.ProjectId, plan.MapId, plan.FieldKey);
                var isNewDataset = dataset is null;
                if (isNewDataset)
                {
                    if (plan.ExpectedCurrentRevisionId is not null)
                    {
                        throw new CoreDomainConflictException("首次 Field Dataset 发布的 expected current revision 必须为 null。");
                    }

                    dataset = new FieldDatasetRecord
                    {
                        FieldDatasetId = Guid.NewGuid(),
                        ProjectId = plan.ProjectId,
                        MapId = plan.MapId,
                        FieldKey = plan.FieldKey,
                        DescriptorSchemaVersion = plan.DescriptorSchemaVersion,
                        SemanticsId = plan.SemanticsId,
                        CurrentFieldRevisionId = null,
                        CreatedAtUtcMs = plan.CreatedAtUtcMs,
                        UpdatedAtUtcMs = plan.CreatedAtUtcMs,
                    };

                    using var insertDataset = connection.CreateCommand();
                    insertDataset.Transaction = transaction;
                    insertDataset.CommandText = """
                        INSERT INTO core_map_field_dataset
                            (field_dataset_id, project_id, map_id, field_key,
                             descriptor_schema_version, semantics_id, current_field_revision_id,
                             created_at_utc_ms, updated_at_utc_ms)
                        VALUES ($datasetId, $projectId, $mapId, $fieldKey,
                                $schemaVersion, $semanticsId, NULL, $createdAt, $updatedAt);
                        """;
                    insertDataset.Parameters.AddWithValue("$datasetId", Uuid(dataset.FieldDatasetId));
                    insertDataset.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insertDataset.Parameters.AddWithValue("$mapId", Uuid(plan.MapId));
                    insertDataset.Parameters.AddWithValue("$fieldKey", plan.FieldKey);
                    insertDataset.Parameters.AddWithValue("$schemaVersion", plan.DescriptorSchemaVersion);
                    insertDataset.Parameters.AddWithValue("$semanticsId", plan.SemanticsId);
                    insertDataset.Parameters.AddWithValue("$createdAt", plan.CreatedAtUtcMs);
                    insertDataset.Parameters.AddWithValue("$updatedAt", plan.CreatedAtUtcMs);
                    insertDataset.ExecuteNonQuery();
                }
                else
                {
                    if (dataset!.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                        || !string.Equals(dataset.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                    {
                        throw new CoreDomainConflictException("现有 Field Dataset 的 descriptor pin 与发布请求不一致。");
                    }

                    if (dataset.CurrentFieldRevisionId != plan.ExpectedCurrentRevisionId)
                    {
                        throw new CoreDomainConflictException("Field Dataset current revision 已变化；拒绝过期发布（CAS conflict）。");
                    }
                }

                var revisionNumber = ReadNextFieldRevisionNumber(transaction, plan.ProjectId, dataset!.FieldDatasetId);
                var revision = new FieldRevisionRecord
                {
                    FieldRevisionId = plan.FieldRevisionId,
                    ProjectId = plan.ProjectId,
                    FieldDatasetId = dataset.FieldDatasetId,
                    RevisionNumber = revisionNumber,
                    ParentRevisionId = plan.ExpectedCurrentRevisionId,
                    FieldRevisionAssetId = plan.FieldRevisionAssetId,
                    ContentFingerprint = plan.ContentFingerprint,
                    ManifestJson = plan.ManifestJson,
                    ProvenanceJson = plan.ProvenanceJson,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                };

                using (var insertRevision = connection.CreateCommand())
                {
                    insertRevision.Transaction = transaction;
                    insertRevision.CommandText = """
                        INSERT INTO core_map_field_revision
                            (field_revision_id, project_id, field_dataset_id, revision_number,
                             parent_revision_id, field_revision_asset_id, content_fingerprint,
                             manifest_json, provenance_json, created_at_utc_ms)
                        VALUES ($revisionId, $projectId, $datasetId, $revisionNumber,
                                $parentRevisionId, $assetId, $fingerprint,
                                $manifestJson, $provenanceJson, $createdAt);
                        """;
                    insertRevision.Parameters.AddWithValue("$revisionId", Uuid(revision.FieldRevisionId));
                    insertRevision.Parameters.AddWithValue("$projectId", Uuid(revision.ProjectId));
                    insertRevision.Parameters.AddWithValue("$datasetId", Uuid(revision.FieldDatasetId));
                    insertRevision.Parameters.AddWithValue("$revisionNumber", revision.RevisionNumber);
                    insertRevision.Parameters.AddWithValue("$parentRevisionId", (object?)revision.ParentRevisionId is Guid parent ? Uuid(parent) : DBNull.Value);
                    insertRevision.Parameters.AddWithValue("$assetId", Uuid(revision.FieldRevisionAssetId));
                    insertRevision.Parameters.AddWithValue("$fingerprint", revision.ContentFingerprint);
                    insertRevision.Parameters.AddWithValue("$manifestJson", revision.ManifestJson);
                    insertRevision.Parameters.AddWithValue("$provenanceJson", revision.ProvenanceJson);
                    insertRevision.Parameters.AddWithValue("$createdAt", revision.CreatedAtUtcMs);
                    insertRevision.ExecuteNonQuery();
                }

                using (var advance = connection.CreateCommand())
                {
                    advance.Transaction = transaction;
                    advance.CommandText = """
                        UPDATE core_map_field_dataset
                        SET current_field_revision_id = $revisionId, updated_at_utc_ms = $updatedAt
                        WHERE project_id = $projectId AND field_dataset_id = $datasetId
                          AND ((current_field_revision_id IS NULL AND $expectedRevisionId IS NULL)
                               OR current_field_revision_id = $expectedRevisionId);
                        """;
                    advance.Parameters.AddWithValue("$revisionId", Uuid(revision.FieldRevisionId));
                    advance.Parameters.AddWithValue("$updatedAt", plan.CreatedAtUtcMs);
                    advance.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    advance.Parameters.AddWithValue("$datasetId", Uuid(dataset.FieldDatasetId));
                    advance.Parameters.AddWithValue("$expectedRevisionId",
                        (object?)plan.ExpectedCurrentRevisionId is Guid expected ? Uuid(expected) : DBNull.Value);
                    if (advance.ExecuteNonQuery() != 1)
                    {
                        throw new CoreDomainConflictException("Field Dataset current revision CAS 更新失败。");
                    }
                }

                transaction.Commit();
                return revision;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    private FieldDatasetRecord? TryReadFieldDataset(
        SqliteTransaction transaction, Guid projectId, Guid mapId, string fieldKey)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT field_dataset_id, project_id, map_id, field_key, descriptor_schema_version,
                   semantics_id, current_field_revision_id, created_at_utc_ms, updated_at_utc_ms
            FROM core_map_field_dataset
            WHERE project_id = $projectId AND map_id = $mapId AND field_key = $fieldKey;
            """;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$mapId", Uuid(mapId));
        command.Parameters.AddWithValue("$fieldKey", fieldKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new FieldDatasetRecord
        {
            FieldDatasetId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            MapId = ParseUuid(reader.GetString(2)),
            FieldKey = reader.GetString(3),
            DescriptorSchemaVersion = checked((int)reader.GetInt64(4)),
            SemanticsId = reader.GetString(5),
            CurrentFieldRevisionId = reader.IsDBNull(6) ? null : ParseUuid(reader.GetString(6)),
            CreatedAtUtcMs = reader.GetInt64(7),
            UpdatedAtUtcMs = reader.GetInt64(8),
        };
    }

    private FieldRevisionRecord? TryReadFieldRevision(SqliteTransaction transaction, Guid projectId, Guid revisionId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT field_revision_id, project_id, field_dataset_id, revision_number,
                   parent_revision_id, field_revision_asset_id, content_fingerprint,
                   manifest_json, provenance_json, created_at_utc_ms
            FROM core_map_field_revision
            WHERE project_id = $projectId AND field_revision_id = $revisionId;
            """;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$revisionId", Uuid(revisionId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapFieldRevision(reader) : null;
    }

    private long ReadNextFieldRevisionNumber(SqliteTransaction transaction, Guid projectId, Guid datasetId)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COALESCE(MAX(revision_number), 0) + 1
            FROM core_map_field_revision
            WHERE project_id = $projectId AND field_dataset_id = $datasetId;
            """;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$datasetId", Uuid(datasetId));
        return (long)command.ExecuteScalar()!;
    }

    private static FieldRevisionRecord MapFieldRevision(SqliteDataReader reader)
    {
        var fingerprint = reader.GetString(6);
        if (fingerprint.Length != 64 || !IsLowerHex(fingerprint))
            throw new CoreDomainDataException("Field Revision content_fingerprint 不是规范 SHA-256。");
        return new FieldRevisionRecord
        {
            FieldRevisionId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            FieldDatasetId = ParseUuid(reader.GetString(2)),
            RevisionNumber = reader.GetInt64(3),
            ParentRevisionId = reader.IsDBNull(4) ? null : ParseUuid(reader.GetString(4)),
            FieldRevisionAssetId = ParseUuid(reader.GetString(5)),
            ContentFingerprint = fingerprint,
            ManifestJson = reader.GetString(7),
            ProvenanceJson = reader.GetString(8),
            CreatedAtUtcMs = reader.GetInt64(9),
        };
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var c in value)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }
}
