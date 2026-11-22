using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Assets;
using WorldView.Domain.Fields;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Persistence.CoreDomain;

/// <summary>
/// EF Core implementation of Map-owned Field Dataset identity and immutable Revision persistence.
/// Raw SQL is limited to the EF migration's SQLite-specific deferred FK and immutability triggers.
/// </summary>
public sealed partial class SqliteCoreDomainDatabase
{
    private CoreDomainEfSession FieldEfSession() => new(_access);

    public FieldDatasetRecord? FindFieldDataset(Guid projectId, Guid mapId, string fieldKey)
    {
        var row = FieldEfSession().Execute(db => db.FieldDatasets.AsNoTracking()
            .SingleOrDefault(x => x.ProjectId == projectId && x.MapId == mapId && x.FieldKey == fieldKey));
        return row is null ? null : MapFieldDataset(row);
    }

    public FieldRevisionRecord? FindCurrentFieldRevision(Guid projectId, Guid mapId, string fieldKey)
    {
        return FieldEfSession().Execute(db =>
        {
            var dataset = db.FieldDatasets.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId && x.MapId == mapId && x.FieldKey == fieldKey);
            if (dataset?.CurrentFieldRevisionId is not Guid revisionId)
                return null;

            var revision = db.FieldRevisions.AsNoTracking().SingleOrDefault(x =>
                x.ProjectId == projectId
                && x.FieldDatasetId == dataset.FieldDatasetId
                && x.FieldRevisionId == revisionId);
            return revision is null ? null : MapFieldRevision(revision);
        });
    }

    public FieldRevisionRecord? FindFieldRevision(Guid projectId, Guid fieldRevisionId)
    {
        var row = FieldEfSession().Execute(db => db.FieldRevisions.AsNoTracking()
            .SingleOrDefault(x => x.ProjectId == projectId && x.FieldRevisionId == fieldRevisionId));
        return row is null ? null : MapFieldRevision(row);
    }

    public IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId)
    {
        return FieldEfSession().Execute(db => (IReadOnlyList<FieldRevisionRecord>)db.FieldRevisions
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.FieldDatasetId == fieldDatasetId)
            .OrderBy(x => x.RevisionNumber)
            .Select(x => new FieldRevisionRecord
            {
                FieldRevisionId = x.FieldRevisionId,
                ProjectId = x.ProjectId,
                FieldDatasetId = x.FieldDatasetId,
                RevisionNumber = x.RevisionNumber,
                ParentRevisionId = x.ParentRevisionId,
                FieldRevisionAssetId = x.FieldRevisionAssetId,
                ContentFingerprint = x.ContentFingerprint,
                ManifestJson = x.ManifestJson,
                ProvenanceJson = x.ProvenanceJson,
                CreatedAtUtcMs = x.CreatedAtUtcMs,
            })
            .ToList());
    }

    public FieldRevisionRecord PublishFieldRevision(FieldRevisionPublishPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return FieldEfSession().Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                // All mutable preconditions are re-read inside the same write transaction.
                var mapExists = db.Maps.AsNoTracking().Any(x =>
                    x.ProjectId == plan.ProjectId && x.MapId == plan.MapId);
                if (!mapExists)
                    throw new CoreDomainConflictException("目标 Map 不存在或不属于当前 Project。");

                var asset = db.Assets.AsNoTracking().SingleOrDefault(x =>
                    x.ProjectId == plan.ProjectId && x.AssetId == plan.FieldRevisionAssetId);
                if (asset is null)
                    throw new CoreDomainConflictException("Field Revision Asset 不存在或不属于当前 Project。");
                if (!string.Equals(asset.PublicationState, "published", StringComparison.Ordinal)
                    || !string.Equals(asset.IntegrityState, "verified", StringComparison.Ordinal))
                    throw new CoreDomainConflictException("Field Revision Asset 必须为 Published + Verified。");

                var dataset = db.FieldDatasets.SingleOrDefault(x =>
                    x.ProjectId == plan.ProjectId && x.MapId == plan.MapId && x.FieldKey == plan.FieldKey);
                if (dataset is null)
                {
                    if (plan.ExpectedCurrentRevisionId is not null)
                        throw new CoreDomainConflictException("首次 Field Dataset 发布的 expected current revision 必须为 null。");

                    dataset = new CoreMapFieldDatasetEntity
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
                    db.FieldDatasets.Add(dataset);
                    db.SaveChanges();
                }
                else
                {
                    if (dataset.DescriptorSchemaVersion != plan.DescriptorSchemaVersion
                        || !string.Equals(dataset.SemanticsId, plan.SemanticsId, StringComparison.Ordinal))
                        throw new CoreDomainConflictException("现有 Field Dataset 的 descriptor pin 与发布请求不一致。");

                    if (dataset.CurrentFieldRevisionId != plan.ExpectedCurrentRevisionId)
                        throw new CoreDomainConflictException("Field Dataset current revision 已变化；拒绝过期发布（CAS conflict）。");
                }

                var maxRevisionNumber = db.FieldRevisions
                    .Where(x => x.ProjectId == plan.ProjectId && x.FieldDatasetId == dataset.FieldDatasetId)
                    .Select(x => (long?)x.RevisionNumber)
                    .Max() ?? 0L;

                var revision = new CoreMapFieldRevisionEntity
                {
                    FieldRevisionId = plan.FieldRevisionId,
                    ProjectId = plan.ProjectId,
                    FieldDatasetId = dataset.FieldDatasetId,
                    RevisionNumber = checked(maxRevisionNumber + 1),
                    ParentRevisionId = dataset.CurrentFieldRevisionId,
                    FieldRevisionAssetId = plan.FieldRevisionAssetId,
                    ContentFingerprint = plan.ContentFingerprint,
                    ManifestJson = plan.ManifestJson,
                    ProvenanceJson = plan.ProvenanceJson,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                };
                db.FieldRevisions.Add(revision);
                db.SaveChanges();

                // CurrentFieldRevisionId is an EF concurrency token: UPDATE includes the
                // originally-read value, preserving expected-current CAS across connections.
                dataset.CurrentFieldRevisionId = revision.FieldRevisionId;
                dataset.UpdatedAtUtcMs = plan.CreatedAtUtcMs;
                db.SaveChanges();

                transaction.Commit();
                return MapFieldRevision(revision);
            }
            catch (DbUpdateConcurrencyException)
            {
                transaction.Rollback();
                throw new CoreDomainConflictException("Field Dataset current revision 已被并发发布推进；本次发布已回滚。");
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                transaction.Rollback();
                throw new CoreDomainConflictException("Field Dataset 发布违反唯一性或所有权约束；本次发布已回滚。");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        });
    }

    private static FieldDatasetRecord MapFieldDataset(CoreMapFieldDatasetEntity row) => new()
    {
        FieldDatasetId = row.FieldDatasetId,
        ProjectId = row.ProjectId,
        MapId = row.MapId,
        FieldKey = row.FieldKey,
        DescriptorSchemaVersion = row.DescriptorSchemaVersion,
        SemanticsId = row.SemanticsId,
        CurrentFieldRevisionId = row.CurrentFieldRevisionId,
        CreatedAtUtcMs = row.CreatedAtUtcMs,
        UpdatedAtUtcMs = row.UpdatedAtUtcMs,
    };

    private static FieldRevisionRecord MapFieldRevision(CoreMapFieldRevisionEntity row)
    {
        if (row.ContentFingerprint.Length != 64
            || row.ContentFingerprint.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))))
            throw new CoreDomainDataException("Field Revision content_fingerprint 不是规范 SHA-256。");

        return new FieldRevisionRecord
        {
            FieldRevisionId = row.FieldRevisionId,
            ProjectId = row.ProjectId,
            FieldDatasetId = row.FieldDatasetId,
            RevisionNumber = row.RevisionNumber,
            ParentRevisionId = row.ParentRevisionId,
            FieldRevisionAssetId = row.FieldRevisionAssetId,
            ContentFingerprint = row.ContentFingerprint,
            ManifestJson = row.ManifestJson,
            ProvenanceJson = row.ProvenanceJson,
            CreatedAtUtcMs = row.CreatedAtUtcMs,
        };
    }
}
