using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using WorldView.Application.Abstractions;
using WorldView.Application.Settings;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain.EfCore;

namespace WorldView.Infrastructure.Settings;

/// <summary>
/// EF Core implementation of the settings metadata port. Every publication updates the
/// immutable revision row and owner current-reference row in one SQLite transaction.
/// </summary>
public sealed class CoreDomainSettingsDocumentRepository : ISettingsDocumentRepository
{
    private readonly CoreDomainEfSession _session;

    public CoreDomainSettingsDocumentRepository(IPackageDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        if (database is not ISqliteConnectionProvider provider)
            throw new ArgumentException("Settings metadata requires the package's shared SQLite connection.", nameof(database));
        _session = new CoreDomainEfSession(provider.GetAccess());
    }

    public SettingsDocumentRevision? FindCurrentProjectSettings(Guid projectId) =>
        _session.Execute(context =>
            (from current in context.ProjectSettingsCurrent.AsNoTracking()
             join revision in context.ProjectSettingsRevisions.AsNoTracking()
                 on new { current.ProjectId, current.ProjectSettingsRevisionId }
                 equals new { revision.ProjectId, revision.ProjectSettingsRevisionId }
             where current.ProjectId == projectId
             select ToRecord(revision, null)).SingleOrDefault());

    public SettingsDocumentRevision? FindCurrentMapSettings(Guid projectId, Guid mapId) =>
        _session.Execute(context =>
            (from current in context.MapSettingsCurrent.AsNoTracking()
             join revision in context.MapSettingsRevisions.AsNoTracking()
                 on new { current.ProjectId, current.MapId, current.MapSettingsRevisionId }
                 equals new { revision.ProjectId, revision.MapId, revision.MapSettingsRevisionId }
             where current.ProjectId == projectId && current.MapId == mapId
             select ToRecord(revision)).SingleOrDefault());

    public IReadOnlyList<SettingsDocumentRevision> ListProjectSettingsRevisions(Guid projectId) =>
        _session.Execute(context => (IReadOnlyList<SettingsDocumentRevision>)context.ProjectSettingsRevisions
            .AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Version).Select(x => ToRecord(x, null)).ToArray());

    public IReadOnlyList<SettingsDocumentRevision> ListMapSettingsRevisions(Guid projectId) =>
        _session.Execute(context => (IReadOnlyList<SettingsDocumentRevision>)context.MapSettingsRevisions
            .AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.MapId).ThenBy(x => x.Version).Select(x => ToRecord(x)).ToArray());

    public SettingsDocumentRevision PublishProjectSettings(Guid projectId,
        SettingsDocumentFilePutResult file, int schemaVersion, long createdAtUtcMs)
    {
        ValidateFile(file, schemaVersion);
        return _session.Execute(context =>
        {
            using var transaction = context.Database.BeginTransaction();
            if (!context.Projects.Any(x => x.ProjectId == projectId))
                throw new CoreDomainDataException("Cannot publish Project Settings for an unknown Project.");

            var version = context.ProjectSettingsRevisions.Where(x => x.ProjectId == projectId)
                .Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
            var revision = new CoreProjectSettingsRevisionEntity
            {
                ProjectSettingsRevisionId = Guid.NewGuid(),
                ProjectId = projectId,
                Version = version,
                SchemaVersion = schemaVersion,
                RelativePath = file.RelativePath,
                HashAlgorithm = "sha-256",
                ContentHash = file.ContentHash,
                ByteLength = file.ByteLength,
                CreatedAtUtcMs = createdAtUtcMs,
            };
            context.ProjectSettingsRevisions.Add(revision);
            context.SaveChanges();

            var current = context.ProjectSettingsCurrent.SingleOrDefault(x => x.ProjectId == projectId);
            if (current is null)
            {
                context.ProjectSettingsCurrent.Add(new CoreProjectSettingsCurrentEntity
                {
                    ProjectId = projectId,
                    ProjectSettingsRevisionId = revision.ProjectSettingsRevisionId,
                });
            }
            else
            {
                current.ProjectSettingsRevisionId = revision.ProjectSettingsRevisionId;
            }

            context.SaveChanges();
            transaction.Commit();
            return ToRecord(revision, null);
        });
    }

    public SettingsDocumentRevision PublishMapSettings(Guid projectId, Guid mapId,
        SettingsDocumentFilePutResult file, int schemaVersion, long createdAtUtcMs)
    {
        ValidateFile(file, schemaVersion);
        return _session.Execute(context =>
        {
            using var transaction = context.Database.BeginTransaction();
            if (!context.Maps.Any(x => x.ProjectId == projectId && x.MapId == mapId))
                throw new CoreDomainDataException("Cannot publish Map Settings for a Map outside the specified Project.");

            var version = context.MapSettingsRevisions
                .Where(x => x.ProjectId == projectId && x.MapId == mapId)
                .Select(x => x.Version).DefaultIfEmpty(0).Max() + 1;
            var revision = new CoreMapSettingsRevisionEntity
            {
                MapSettingsRevisionId = Guid.NewGuid(),
                ProjectId = projectId,
                MapId = mapId,
                Version = version,
                SchemaVersion = schemaVersion,
                RelativePath = file.RelativePath,
                HashAlgorithm = "sha-256",
                ContentHash = file.ContentHash,
                ByteLength = file.ByteLength,
                CreatedAtUtcMs = createdAtUtcMs,
            };
            context.MapSettingsRevisions.Add(revision);
            context.SaveChanges();

            var current = context.MapSettingsCurrent.SingleOrDefault(
                x => x.ProjectId == projectId && x.MapId == mapId);
            if (current is null)
            {
                context.MapSettingsCurrent.Add(new CoreMapSettingsCurrentEntity
                {
                    ProjectId = projectId,
                    MapId = mapId,
                    MapSettingsRevisionId = revision.MapSettingsRevisionId,
                });
            }
            else
            {
                current.MapSettingsRevisionId = revision.MapSettingsRevisionId;
            }

            context.SaveChanges();
            transaction.Commit();
            return ToRecord(revision);
        });
    }

    private static void ValidateFile(SettingsDocumentFilePutResult file, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (schemaVersion != SettingsDocumentFormat.InitialSchemaVersion
            || file.ByteLength <= 0
            || !string.Equals(file.RelativePath, SettingsDocumentPaths.GetRelativePath(file.ContentHash), StringComparison.Ordinal))
            throw new ArgumentException("Settings revision metadata is not a valid V1 content-addressed file reference.", nameof(file));
    }

    private static SettingsDocumentRevision ToRecord(CoreProjectSettingsRevisionEntity entity, Guid? mapId) => new()
    {
        RevisionId = entity.ProjectSettingsRevisionId,
        ProjectId = entity.ProjectId,
        MapId = mapId,
        Version = entity.Version,
        SchemaVersion = entity.SchemaVersion,
        RelativePath = entity.RelativePath,
        HashAlgorithm = entity.HashAlgorithm,
        ContentHash = entity.ContentHash,
        ByteLength = entity.ByteLength,
        CreatedAtUtcMs = entity.CreatedAtUtcMs,
    };

    private static SettingsDocumentRevision ToRecord(CoreMapSettingsRevisionEntity entity) => new()
    {
        RevisionId = entity.MapSettingsRevisionId,
        ProjectId = entity.ProjectId,
        MapId = entity.MapId,
        Version = entity.Version,
        SchemaVersion = entity.SchemaVersion,
        RelativePath = entity.RelativePath,
        HashAlgorithm = entity.HashAlgorithm,
        ContentHash = entity.ContentHash,
        ByteLength = entity.ByteLength,
        CreatedAtUtcMs = entity.CreatedAtUtcMs,
    };
}
