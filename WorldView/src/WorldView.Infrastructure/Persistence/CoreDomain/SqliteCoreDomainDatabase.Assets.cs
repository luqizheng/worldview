using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// Asset authoritative metadata and typed definition links, persisted through the
/// Core Domain EF model. Asset bytes remain in the external content-addressed store.
/// Every compound write is serialized by the package connection coordinator and committed
/// as one SQLite transaction.
/// </summary>
public sealed partial class SqliteCoreDomainDatabase
{
    public AssetRecord? FindAsset(Guid projectId, Guid assetId) =>
        _ef.Execute(db =>
        {
            var entity = db.Assets.AsNoTracking()
                .SingleOrDefault(x => x.ProjectId == projectId && x.AssetId == assetId);
            return entity is null ? null : MapAsset(entity);
        });

    public AssetRecord? FindAssetByContentHash(Guid projectId, string hashAlgorithm, string contentHash)
    {
        var normalizedHash = AssetRules.NormalizeContentHash(contentHash);
        return _ef.Execute(db =>
        {
            var entity = db.Assets.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId
                && x.HashAlgorithm == hashAlgorithm
                && x.ContentHash == normalizedHash);
            return entity is null ? null : MapAsset(entity);
        });
    }

    public IReadOnlyList<AssetRecord> ListAssets(Guid projectId) =>
        _ef.Execute(db => (IReadOnlyList<AssetRecord>)db.Assets.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.AssetId)
            .AsEnumerable()
            .Select(MapAsset)
            .ToArray());

    public AssetRecord RegisterAsset(RegisterAssetPlan plan)
    {
        return _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var entity = new CoreAssetEntity
                {
                    AssetId = plan.AssetId,
                    ProjectId = plan.ProjectId,
                    ContentHash = AssetRules.NormalizeContentHash(plan.ContentHash),
                    HashAlgorithm = "sha-256",
                    ByteLength = plan.ByteLength,
                    MediaType = plan.MediaType,
                    FormatVersion = plan.FormatVersion,
                    RelativePath = AssetRules.ContentAddressedRelativePath(plan.ContentHash),
                    PublicationState = "published",
                    IntegrityState = "unverified",
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                };

                db.Assets.Add(entity);
                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
                return MapAsset(entity);
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
    }

    public void RecordAssetIntegrityFailure(
        Guid projectId,
        Guid assetId,
        AssetIntegrityState integrityState,
        long nowUtcMs)
    {
        _ = nowUtcMs; // The V1 asset metadata schema intentionally has no updated_at column.
        var stateText = integrityState switch
        {
            AssetIntegrityState.Corrupt => "corrupt",
            AssetIntegrityState.Missing => "missing",
            _ => throw new CoreDomainConflictException(
                "完整性失败接口只接受 Corrupt 或 Missing，Verified 必须通过字节验证证据写入。"),
        };

        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var entity = db.Assets.SingleOrDefault(x =>
                    x.ProjectId == projectId && x.AssetId == assetId);
                if (entity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Asset {assetId} 不存在或不属于项目 {projectId}，无法记录完整性失败。");
                }

                entity.IntegrityState = stateText;
                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
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
    }

    public void MarkAssetVerified(
        Guid projectId,
        Guid assetId,
        AssetByteDigest digest,
        long nowUtcMs)
    {
        _ = nowUtcMs; // The V1 asset metadata schema intentionally has no updated_at column.
        ArgumentNullException.ThrowIfNull(digest);

        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var entity = db.Assets.SingleOrDefault(x =>
                    x.ProjectId == projectId && x.AssetId == assetId);
                if (entity is null)
                {
                    throw new CoreDomainConflictException(
                        $"Asset {assetId} 不存在或不属于项目 {projectId}，无法标记 Verified。");
                }

                // 写事务内重读当前元数据：只接受与当前 content_hash/byte_length 完全一致的
                // 实测摘要（摘要本身只能来自真实字节流测量，无法凭空构造——Task 002-A）。
                if (!string.Equals(entity.HashAlgorithm, "sha-256", StringComparison.Ordinal)
                    || !string.Equals(entity.ContentHash, digest.ContentHash, StringComparison.Ordinal)
                    || entity.ByteLength != digest.ByteLength)
                {
                    throw new CoreDomainConflictException(
                        "Asset 实测字节摘要与当前元数据不匹配（metadata 已变化或摘要过期），拒绝写入 Verified。");
                }

                entity.IntegrityState = "verified";
                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
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
    }

    public IReadOnlyList<Guid> FindOrphanAssetIds(Guid projectId) =>
        _ef.Execute(db => (IReadOnlyList<Guid>)db.Assets.AsNoTracking()
            .Where(asset => asset.ProjectId == projectId
                && !db.ObjectDefinitionAssets.Any(link =>
                    link.project_id == asset.ProjectId && link.asset_id == asset.AssetId))
            .OrderBy(asset => asset.AssetId)
            .Select(asset => asset.AssetId)
            .ToArray());

    public ObjectDefinitionAssetLinkRecord? FindDefinitionAssetLink(
        Guid projectId,
        Guid objectIdentityId,
        Guid assetId,
        string role) =>
        _ef.Execute(db =>
        {
            var entity = db.ObjectDefinitionAssets.AsNoTracking().SingleOrDefault(x =>
                x.project_id == projectId
                && x.object_identity_id == objectIdentityId
                && x.asset_id == assetId
                && x.role == role);
            return entity is null ? null : MapDefinitionAssetLink(entity);
        });

    public IReadOnlyList<ObjectDefinitionAssetLinkRecord> ListDefinitionAssetLinks(
        Guid projectId,
        Guid objectIdentityId) =>
        _ef.Execute(db => (IReadOnlyList<ObjectDefinitionAssetLinkRecord>)db.ObjectDefinitionAssets
            .AsNoTracking()
            .Where(x => x.project_id == projectId && x.object_identity_id == objectIdentityId)
            .OrderBy(x => x.asset_id)
            .ThenBy(x => x.role)
            .AsEnumerable()
            .Select(MapDefinitionAssetLink)
            .ToArray());

    public void LinkDefinitionAsset(LinkDefinitionAssetPlan plan)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                db.ObjectDefinitionAssets.Add(new CoreObjectDefinitionAssetEntity
                {
                    object_definition_asset_id = plan.ObjectDefinitionAssetId,
                    project_id = plan.ProjectId,
                    object_identity_id = plan.ObjectIdentityId,
                    asset_id = plan.AssetId,
                    role = plan.Role,
                    created_at_utc_ms = plan.CreatedAtUtcMs,
                });

                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
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
    }

    public void UnlinkDefinitionAsset(Guid projectId, Guid objectIdentityId, Guid assetId, string role)
    {
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                var entity = db.ObjectDefinitionAssets.SingleOrDefault(x =>
                    x.project_id == projectId
                    && x.object_identity_id == objectIdentityId
                    && x.asset_id == assetId
                    && x.role == role);
                if (entity is null)
                {
                    throw new CoreDomainConflictException("definition Asset link 不存在，无法移除。");
                }

                db.ObjectDefinitionAssets.Remove(entity);
                SaveChangesOrThrowSqlite(db);
                transaction.Commit();
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
    }

    private static AssetRecord MapAsset(CoreAssetEntity entity)
    {
        var integrity = entity.IntegrityState switch
        {
            "verified" => AssetIntegrityState.Verified,
            "unverified" => AssetIntegrityState.Unverified,
            "corrupt" => AssetIntegrityState.Corrupt,
            "missing" => AssetIntegrityState.Missing,
            _ => throw new CoreDomainDataException(
                $"integrity_state \"{entity.IntegrityState}\" 不可识别。"),
        };

        if (!AssetRules.IsSha256Hex(entity.ContentHash))
        {
            throw new CoreDomainDataException(
                $"content_hash \"{entity.ContentHash}\" 不是规范 SHA-256 表示（数据损坏）。");
        }

        if (entity.PublicationState != "published")
        {
            throw new CoreDomainDataException(
                $"publication_state \"{entity.PublicationState}\" 不可识别。");
        }

        return new AssetRecord
        {
            AssetId = entity.AssetId,
            ProjectId = entity.ProjectId,
            ContentHash = entity.ContentHash,
            HashAlgorithm = entity.HashAlgorithm,
            ByteLength = entity.ByteLength,
            MediaType = entity.MediaType,
            FormatVersion = entity.FormatVersion,
            RelativePath = AssetRules.NormalizeRelativePath(entity.RelativePath),
            PublicationState = AssetPublicationState.Published,
            IntegrityState = integrity,
            CreatedAtUtcMs = entity.CreatedAtUtcMs,
        };
    }

    private static ObjectDefinitionAssetLinkRecord MapDefinitionAssetLink(
        CoreObjectDefinitionAssetEntity entity) => new()
    {
        ObjectDefinitionAssetId = entity.object_definition_asset_id,
        ProjectId = entity.project_id,
        ObjectIdentityId = entity.object_identity_id,
        AssetId = entity.asset_id,
        Role = entity.role,
        CreatedAtUtcMs = entity.created_at_utc_ms,
    };

    private static void SaveChangesOrThrowSqlite(CoreDomainDbContext db)
    {
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlite)
        {
            throw sqlite;
        }
    }
}
