using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// <see cref="SqliteCoreDomainDatabase"/> 的 Asset 权威元数据与 definition typed link
/// 写路径（CD-02-D）。单事务临界区模式与 Objects 部分一致。
/// </summary>
public sealed partial class SqliteCoreDomainDatabase
{
    public AssetRecord? FindAsset(Guid projectId, Guid assetId) =>
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            return TryReadAssetRow(transaction, projectId, assetId);
        });

    public AssetRecord? FindAssetByContentHash(Guid projectId, string hashAlgorithm, string contentHash)
    {
        const string sql = """
            SELECT asset_id, project_id, content_hash, hash_algorithm, byte_length,
                   media_type, format_version, relative_path, publication_state,
                   integrity_state, created_at_utc_ms
            FROM core_asset
            WHERE project_id = $projectId AND hash_algorithm = $hashAlgorithm AND content_hash = $contentHash;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$hashAlgorithm", hashAlgorithm);
            command.Parameters.AddWithValue("$contentHash", AssetRules.NormalizeContentHash(contentHash));
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapAsset(reader) : null;
        });
    }

    public IReadOnlyList<AssetRecord> ListAssets(Guid projectId)
    {
        const string sql = """
            SELECT asset_id, project_id, content_hash, hash_algorithm, byte_length,
                   media_type, format_version, relative_path, publication_state,
                   integrity_state, created_at_utc_ms
            FROM core_asset
            WHERE project_id = $projectId
            ORDER BY asset_id;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            var rows = new List<AssetRecord>();
            while (reader.Read())
            {
                rows.Add(MapAsset(reader));
            }

            return (IReadOnlyList<AssetRecord>)rows;
        });
    }

    public AssetRecord RegisterAsset(RegisterAssetPlan plan)
    {
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO core_asset
                            (asset_id, project_id, content_hash, hash_algorithm, byte_length,
                             media_type, format_version, relative_path, publication_state,
                             integrity_state, created_at_utc_ms)
                        VALUES ($assetId, $projectId, $contentHash, 'sha-256', $byteLength,
                                $mediaType, $formatVersion, $relativePath, 'published',
                                'unverified', $createdAt);
                        """;
                    insert.Parameters.AddWithValue("$assetId", Uuid(plan.AssetId));
                    insert.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    insert.Parameters.AddWithValue("$contentHash", AssetRules.NormalizeContentHash(plan.ContentHash));
                    insert.Parameters.AddWithValue("$byteLength", plan.ByteLength);
                    insert.Parameters.AddWithValue("$mediaType", plan.MediaType);
                    insert.Parameters.AddWithValue("$formatVersion", plan.FormatVersion);
                    insert.Parameters.AddWithValue("$relativePath", AssetRules.ContentAddressedRelativePath(plan.ContentHash));
                    insert.Parameters.AddWithValue("$createdAt", plan.CreatedAtUtcMs);
                    insert.ExecuteNonQuery();
                }

                var record = TryReadAssetRow(transaction, plan.ProjectId, plan.AssetId)
                    ?? throw new CoreDomainDataException("Asset 登记后不可读（数据损坏）。");
                transaction.Commit();
                return record;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public void UpdateAssetIntegrityState(Guid projectId, Guid assetId, AssetIntegrityState integrityState, long nowUtcMs)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var stateText = integrityState switch
                {
                    AssetIntegrityState.Verified => "verified",
                    AssetIntegrityState.Unverified => "unverified",
                    AssetIntegrityState.Corrupt => "corrupt",
                    AssetIntegrityState.Missing => "missing",
                    _ => throw new CoreDomainDataException($"integrity_state {integrityState} 不可识别。"),
                };
                using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE core_asset
                    SET integrity_state = $state, created_at_utc_ms = created_at_utc_ms
                    WHERE project_id = $projectId AND asset_id = $assetId;
                    """;
                update.Parameters.AddWithValue("$state", stateText);
                update.Parameters.AddWithValue("$projectId", Uuid(projectId));
                update.Parameters.AddWithValue("$assetId", Uuid(assetId));
                if (update.ExecuteNonQuery() != 1)
                {
                    throw new CoreDomainConflictException($"Asset {assetId} 不存在或不属于项目 {projectId}，无法更新完整性状态。");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId)
    {
        const string sql = """
            SELECT a.asset_id
            FROM core_asset a
            WHERE a.project_id = $projectId
              AND NOT EXISTS (
                  SELECT 1 FROM core_object_definition_asset l
                  WHERE l.project_id = a.project_id AND l.asset_id = a.asset_id)
            ORDER BY a.asset_id;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            using var reader = command.ExecuteReader();
            var ids = new List<Guid>();
            while (reader.Read())
            {
                ids.Add(ParseUuid(reader.GetString(0)));
            }

            return (IReadOnlyList<Guid>)ids;
        });
    }

    public ObjectDefinitionAssetLinkRecord? FindDefinitionAssetLink(Guid projectId, Guid objectIdentityId, Guid assetId, string role)
    {
        const string sql = """
            SELECT object_definition_asset_id, project_id, object_identity_id, asset_id, role, created_at_utc_ms
            FROM core_object_definition_asset
            WHERE project_id = $projectId AND object_identity_id = $identityId
              AND asset_id = $assetId AND role = $role;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
            command.Parameters.AddWithValue("$assetId", Uuid(assetId));
            command.Parameters.AddWithValue("$role", role);
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapDefinitionAssetLink(reader) : null;
        });
    }

    public IReadOnlyList<ObjectDefinitionAssetLinkRecord> ListDefinitionAssetLinks(Guid projectId, Guid objectIdentityId)
    {
        const string sql = """
            SELECT object_definition_asset_id, project_id, object_identity_id, asset_id, role, created_at_utc_ms
            FROM core_object_definition_asset
            WHERE project_id = $projectId AND object_identity_id = $identityId
            ORDER BY asset_id, role;
            """;
        return _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.Parameters.AddWithValue("$projectId", Uuid(projectId));
            command.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
            using var reader = command.ExecuteReader();
            var rows = new List<ObjectDefinitionAssetLinkRecord>();
            while (reader.Read())
            {
                rows.Add(MapDefinitionAssetLink(reader));
            }

            return (IReadOnlyList<ObjectDefinitionAssetLinkRecord>)rows;
        });
    }

    public void LinkDefinitionAsset(LinkDefinitionAssetPlan plan)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                // Re-read every mutable precondition inside the same coordinated write transaction.
                // Service-layer reads are advisory only; this closes the read/write race window.
                using (var precondition = connection.CreateCommand())
                {
                    precondition.Transaction = transaction;
                    precondition.CommandText = """
                        SELECT o.lifecycle_status, a.integrity_state, t.type_key,
                               a.content_hash, d.definition_payload_json
                        FROM core_object o
                        JOIN core_object_type t
                          ON t.project_id = o.project_id AND t.object_type_id = o.object_type_id
                        JOIN core_asset a
                          ON a.project_id = o.project_id
                        JOIN core_object_definition d
                          ON d.project_id = o.project_id AND d.object_identity_id = o.object_identity_id
                        WHERE o.project_id = $projectId
                          AND o.object_identity_id = $identityId
                          AND a.asset_id = $assetId;
                        """;
                    precondition.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                    precondition.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                    precondition.Parameters.AddWithValue("$assetId", Uuid(plan.AssetId));
                    using var reader = precondition.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new CoreDomainConflictException(
                            $"Object {plan.ObjectIdentityId} or Asset {plan.AssetId} does not exist in Project {plan.ProjectId}.");
                    }

                    var lifecycleStatus = reader.GetString(0);
                    var integrityState = reader.GetString(1);
                    var typeKey = reader.GetString(2);
                    var assetHash = reader.GetString(3);
                    var definitionPayload = reader.GetString(4);
                    if (!string.Equals(lifecycleStatus, "active", StringComparison.Ordinal))
                    {
                        throw new CoreDomainConflictException(
                            $"Object {plan.ObjectIdentityId} is {lifecycleStatus}; only active Objects may own formal Asset links.");
                    }

                    if (!string.Equals(integrityState, "verified", StringComparison.Ordinal))
                    {
                        throw new CoreDomainConflictException(
                            $"Asset {plan.AssetId} integrity_state is {integrityState}; only verified Assets may be linked.");
                    }

                    try
                    {
                        ObjectTypeRegistry.ValidateGeometryRole(typeKey, plan.Role);
                    }
                    catch (RegistryException ex)
                    {
                        throw new CoreDomainConflictException(ex.Message, ex);
                    }

                    // Building/Cave geometry is published only when the current owner manifest
                    // names the exact Asset, role and authoritative content hash. Keep this
                    // check in the same transaction as the typed-link insert.
                    var descriptor = ObjectTypeRegistry.Require(typeKey);
                    if (descriptor.HasTypedAggregateSchema)
                    {
                        using var definition = JsonDocument.Parse(definitionPayload);
                        if (!definition.RootElement.TryGetProperty("geometry_components", out var components)
                            || components.ValueKind != JsonValueKind.Array)
                        {
                            throw new CoreDomainConflictException(
                                $"Object {plan.ObjectIdentityId} has no geometry_components manifest; cannot publish a formal Asset link.");
                        }

                        var matches = 0;
                        foreach (var component in components.EnumerateArray())
                        {
                            if (component.ValueKind != JsonValueKind.Object
                                || !component.TryGetProperty("asset_id", out var assetIdElement)
                                || assetIdElement.ValueKind != JsonValueKind.String
                                || !component.TryGetProperty("geometry_role", out var roleElement)
                                || roleElement.ValueKind != JsonValueKind.String
                                || !component.TryGetProperty("content_hash", out var hashElement)
                                || hashElement.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            if (string.Equals(assetIdElement.GetString(), plan.AssetId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                                && string.Equals(roleElement.GetString(), plan.Role, StringComparison.Ordinal)
                                && string.Equals(hashElement.GetString(), assetHash, StringComparison.Ordinal))
                            {
                                matches++;
                            }
                        }

                        if (matches != 1)
                        {
                            throw new CoreDomainConflictException(
                                $"Object {plan.ObjectIdentityId} geometry manifest must contain exactly one component matching Asset {plan.AssetId}, role {plan.Role}, and core_asset hash.");
                        }
                    }
                }

                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO core_object_definition_asset
                        (object_definition_asset_id, project_id, object_identity_id, asset_id, role, created_at_utc_ms)
                    VALUES ($linkId, $projectId, $identityId, $assetId, $role, $createdAt);
                    """;
                insert.Parameters.AddWithValue("$linkId", Uuid(plan.ObjectDefinitionAssetId));
                insert.Parameters.AddWithValue("$projectId", Uuid(plan.ProjectId));
                insert.Parameters.AddWithValue("$identityId", Uuid(plan.ObjectIdentityId));
                insert.Parameters.AddWithValue("$assetId", Uuid(plan.AssetId));
                insert.Parameters.AddWithValue("$role", plan.Role);
                insert.Parameters.AddWithValue("$createdAt", plan.CreatedAtUtcMs);
                insert.ExecuteNonQuery();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    public void UnlinkDefinitionAsset(Guid projectId, Guid objectIdentityId, Guid assetId, string role)
    {
        _access.Execute(connection =>
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM core_object_definition_asset
                    WHERE project_id = $projectId AND object_identity_id = $identityId
                      AND asset_id = $assetId AND role = $role;
                    """;
                delete.Parameters.AddWithValue("$projectId", Uuid(projectId));
                delete.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
                delete.Parameters.AddWithValue("$assetId", Uuid(assetId));
                delete.Parameters.AddWithValue("$role", role);
                if (delete.ExecuteNonQuery() != 1)
                {
                    throw new CoreDomainConflictException("definition Asset link 不存在，无法移除。");
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    /// <summary>
    /// Existing formal links must remain represented by exactly one current owner manifest entry.
    /// Called by Definition CAS inside the same SQLite transaction as the payload update.
    /// </summary>
    private static void ValidateExistingDefinitionAssetLinks(
        SqliteTransaction transaction,
        Guid projectId,
        Guid objectIdentityId,
        string definitionPayloadJson)
    {
        const string typeSql = """
            SELECT t.type_key
            FROM core_object o
            JOIN core_object_type t
              ON t.project_id = o.project_id AND t.object_type_id = o.object_type_id
            WHERE o.project_id = $projectId AND o.object_identity_id = $identityId;
            """;
        string typeKey;
        using (var typeCommand = transaction.Connection!.CreateCommand())
        {
            typeCommand.Transaction = transaction;
            typeCommand.CommandText = typeSql;
            typeCommand.Parameters.AddWithValue("$projectId", Uuid(projectId));
            typeCommand.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
            typeKey = typeCommand.ExecuteScalar() as string
                ?? throw new CoreDomainDataException($"Object {objectIdentityId} type is missing while validating its Asset links.");
        }

        if (!ObjectTypeRegistry.Require(typeKey).HasTypedAggregateSchema)
        {
            return;
        }

        using var definition = JsonDocument.Parse(definitionPayloadJson);
        if (!definition.RootElement.TryGetProperty("geometry_components", out var components)
            || components.ValueKind != JsonValueKind.Array)
        {
            components = default;
        }

        const string linksSql = """
            SELECT l.asset_id, l.role, a.content_hash, a.integrity_state
            FROM core_object_definition_asset l
            JOIN core_asset a
              ON a.project_id = l.project_id AND a.asset_id = l.asset_id
            WHERE l.project_id = $projectId AND l.object_identity_id = $identityId;
            """;
        using var linksCommand = transaction.Connection!.CreateCommand();
        linksCommand.Transaction = transaction;
        linksCommand.CommandText = linksSql;
        linksCommand.Parameters.AddWithValue("$projectId", Uuid(projectId));
        linksCommand.Parameters.AddWithValue("$identityId", Uuid(objectIdentityId));
        using var reader = linksCommand.ExecuteReader();
        while (reader.Read())
        {
            var assetId = reader.GetString(0);
            var role = reader.GetString(1);
            var contentHash = reader.GetString(2);
            var integrityState = reader.GetString(3);
            if (!string.Equals(integrityState, "verified", StringComparison.Ordinal))
            {
                throw new CoreDomainConflictException(
                    $"Existing Asset link {assetId}/{role} is no longer verified; Definition cannot be edited until integrity is restored.");
            }

            var matches = 0;
            if (components.ValueKind == JsonValueKind.Array)
            {
                foreach (var component in components.EnumerateArray())
                {
                    if (component.ValueKind != JsonValueKind.Object
                        || !component.TryGetProperty("asset_id", out var assetElement)
                        || assetElement.ValueKind != JsonValueKind.String
                        || !component.TryGetProperty("geometry_role", out var roleElement)
                        || roleElement.ValueKind != JsonValueKind.String
                        || !component.TryGetProperty("content_hash", out var hashElement)
                        || hashElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    if (string.Equals(assetElement.GetString(), assetId, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(roleElement.GetString(), role, StringComparison.Ordinal)
                        && string.Equals(hashElement.GetString(), contentHash, StringComparison.Ordinal))
                    {
                        matches++;
                    }
                }
            }

            if (matches != 1)
            {
                throw new CoreDomainConflictException(
                    $"Definition edit would orphan or duplicate existing Asset link {assetId}/{role}; exactly one matching geometry manifest entry is required.");
            }
        }
    }

    // ---- 行读取辅助 ----

    private AssetRecord? TryReadAssetRow(SqliteTransaction transaction, Guid projectId, Guid assetId)
    {
        const string sql = """
            SELECT asset_id, project_id, content_hash, hash_algorithm, byte_length,
                   media_type, format_version, relative_path, publication_state,
                   integrity_state, created_at_utc_ms
            FROM core_asset
            WHERE project_id = $projectId AND asset_id = $assetId;
            """;
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$projectId", Uuid(projectId));
        command.Parameters.AddWithValue("$assetId", Uuid(assetId));
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAsset(reader) : null;
    }

    private static AssetRecord MapAsset(SqliteDataReader reader)
    {
        var integrityText = reader.GetString(9);
        var integrity = integrityText switch
        {
            "verified" => AssetIntegrityState.Verified,
            "unverified" => AssetIntegrityState.Unverified,
            "corrupt" => AssetIntegrityState.Corrupt,
            "missing" => AssetIntegrityState.Missing,
            _ => throw new CoreDomainDataException($"integrity_state \"{integrityText}\" 不可识别。"),
        };
        var hash = reader.GetString(2);
        if (!AssetRules.IsSha256Hex(hash))
        {
            throw new CoreDomainDataException($"content_hash \"{hash}\" 不是规范 SHA-256 表示（数据损坏）。");
        }

        return new AssetRecord
        {
            AssetId = ParseUuid(reader.GetString(0)),
            ProjectId = ParseUuid(reader.GetString(1)),
            ContentHash = hash,
            HashAlgorithm = reader.GetString(3),
            ByteLength = reader.GetInt64(4),
            MediaType = reader.GetString(5),
            FormatVersion = reader.GetString(6),
            RelativePath = AssetRules.NormalizeRelativePath(reader.GetString(7)),
            PublicationState = AssetPublicationState.Published,
            IntegrityState = integrity,
            CreatedAtUtcMs = reader.GetInt64(10),
        };
    }

    private static ObjectDefinitionAssetLinkRecord MapDefinitionAssetLink(SqliteDataReader reader) => new()
    {
        ObjectDefinitionAssetId = ParseUuid(reader.GetString(0)),
        ProjectId = ParseUuid(reader.GetString(1)),
        ObjectIdentityId = ParseUuid(reader.GetString(2)),
        AssetId = ParseUuid(reader.GetString(3)),
        Role = reader.GetString(4),
        CreatedAtUtcMs = reader.GetInt64(5),
    };
}
