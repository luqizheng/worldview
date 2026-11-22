using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorldView.Domain.Fields;
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
            return entity is null ? null : MapFieldRevision(entity);
        });

    public IReadOnlyList<FieldRevisionRecord> ListFieldRevisions(Guid projectId, Guid fieldDatasetId) =>
        _ef.Execute(db => (IReadOnlyList<FieldRevisionRecord>)db.FieldRevisions.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.FieldDatasetId == fieldDatasetId)
            .OrderBy(x => x.RevisionNumber)
            .AsEnumerable()
            .Select(MapFieldRevision)
            .ToArray());

    public FieldRevisionRecord PublishFieldRevision(PublishFieldRevisionPlan plan) =>
        _ef.Execute(db =>
        {
            using var transaction = db.Database.BeginTransaction();
            try
            {
                if (!db.Maps.Any(x => x.ProjectId == plan.ProjectId && x.MapId == plan.MapId))
                    throw new CoreDomainConflictException("目标 Map 不存在或不属于当前 Project。");

                var asset = db.Assets.SingleOrDefault(x =>
                    x.ProjectId == plan.ProjectId && x.AssetId == plan.FieldRevisionAssetId);
                if (asset is null)
                    throw new CoreDomainConflictException("Field Revision Asset 不存在或不属于当前 Project。");
                if (asset.IntegrityState != "verified")
                    throw new CoreDomainConflictException(
                        $"Field Revision Asset 必须为 verified，当前状态为 {asset.IntegrityState}。");

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
                    FieldRevisionAssetId = plan.FieldRevisionAssetId,
                    ContentFingerprint = plan.ContentFingerprint,
                    ManifestJson = plan.ManifestJson,
                    ProvenanceJson = plan.ProvenanceJson,
                    CreatedAtUtcMs = plan.CreatedAtUtcMs,
                };
                db.FieldRevisions.Add(revision);
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

                transaction.Commit();
                return MapFieldRevision(revision);
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

    private static FieldDatasetRecord MapFieldDataset(CoreMapFieldDatasetEntity entity) => new()
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

    private static FieldRevisionRecord MapFieldRevision(CoreMapFieldRevisionEntity entity) => new()
    {
        FieldRevisionId = entity.FieldRevisionId,
        ProjectId = entity.ProjectId,
        FieldDatasetId = entity.FieldDatasetId,
        RevisionNumber = entity.RevisionNumber,
        ParentRevisionId = entity.ParentRevisionId,
        FieldRevisionAssetId = entity.FieldRevisionAssetId,
        ContentFingerprint = entity.ContentFingerprint,
        ManifestJson = entity.ManifestJson,
        ProvenanceJson = entity.ProvenanceJson,
        CreatedAtUtcMs = entity.CreatedAtUtcMs,
    };
}
