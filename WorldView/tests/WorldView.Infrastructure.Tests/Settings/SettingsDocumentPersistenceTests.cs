using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;
using WorldView.Application.Bootstrap;
using WorldView.Application.Settings;
using WorldView.Domain.Maps;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using WorldView.Infrastructure.Settings;

namespace WorldView.Infrastructure.Tests.Settings;

public sealed class SettingsDocumentPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wv-settings-{Guid.NewGuid():N}");
    private readonly SqlitePackageDatabase _database;
    private readonly SqliteCoreDomainDatabase _core;

    public SettingsDocumentPersistenceTests()
    {
        Directory.CreateDirectory(_root);
        var factory = new SqlitePackageDatabaseFactory();
        _database = (SqlitePackageDatabase)factory.CreateForFile(Path.Combine(_root, "project.sqlite"));
        _database.OpenAsync().GetAwaiter().GetResult();
        new ApplicationBootstrapper().InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow)
            .GetAwaiter().GetResult();
        _core = new SqliteCoreDomainDatabase(_database);
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task ProjectAndMapSettings_PublishImmutableRevisionsAndLoadCurrentDocuments()
    {
        var project = ProjectRules.CreateProject("Settings Test", 1000);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var map = MapService.CreateMap(_core, project.ProjectId, "Settings Map", 1001).Map;

        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);

        var first = await service.SaveProjectAsync(project.ProjectId,
            JObject.Parse("{\"ui\":{\"theme\":\"dark\"}}"));
        var second = await service.SaveProjectAsync(project.ProjectId,
            JObject.Parse("{\"ui\":{\"theme\":\"light\"}}"));
        var mapRevision = await service.SaveMapAsync(project.ProjectId, map.MapId,
            JObject.Parse("{\"viewport\":{\"zoom\":2.5}}"));

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(1, mapRevision.Version);
        Assert.Equal(2, repository.ListProjectSettingsRevisions(project.ProjectId).Count);
        Assert.Single(repository.ListMapSettingsRevisions(project.ProjectId));

        var loadedProject = await service.LoadCurrentProjectAsync(project.ProjectId);
        var loadedMap = await service.LoadCurrentMapAsync(project.ProjectId, map.MapId);
        Assert.Equal("light", (string?)loadedProject!.Settings["ui"]?["theme"]);
        Assert.Equal(2.5m, (decimal?)loadedMap!.Settings["viewport"]?["zoom"]);
    }

    [Fact]
    public async Task StartupRecovery_PreservesEveryReferencedRevisionAndCollectsOnlyOrphans()
    {
        var project = ProjectRules.CreateProject("Settings Recovery", 2000);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);

        var revision = await service.SaveProjectAsync(project.ProjectId, JObject.Parse("{\"name\":\"baseline\"}"));
        await service.SaveProjectAsync(project.ProjectId, JObject.Parse("{\"name\":\"newer\"}"));

        await using var orphanInput = new MemoryStream(Encoding.UTF8.GetBytes("orphan"));
        var orphan = await fileStore.PutAsync(orphanInput);

        var recovery = new CoreDomainSettingsDocumentRecovery();
        var report = await recovery.ReconcileAsync(_database, _root, project.ProjectId);

        Assert.Equal(2, report.VerifiedDocumentCount);
        Assert.Contains(orphan.ContentHash, report.CollectedOrphanContentHashes);
        Assert.DoesNotContain(orphan.ContentHash, fileStore.ListPublishedContentHashes());
        Assert.Contains(revision.ContentHash, fileStore.ListPublishedContentHashes());
    }

    [Fact]
    public async Task CancellationAfterFilePublication_LeavesARecoverableOrphan()
    {
        var project = ProjectRules.CreateProject("Settings Cancellation", 2400);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var innerStore = new SettingsDocumentFileStore(_root);
        using var cancellation = new System.Threading.CancellationTokenSource();
        var cancellingStore = new CancelAfterPutFileStore(innerStore, cancellation);
        var service = new SettingsDocumentService(repository, cancellingStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SaveProjectAsync(project.ProjectId, JObject.Parse("{\"cancelled\":true}"), cancellation.Token));

        Assert.Empty(repository.ListProjectSettingsRevisions(project.ProjectId));
        var orphanHashes = innerStore.ListPublishedContentHashes();
        Assert.Single(orphanHashes);

        var report = await new CoreDomainSettingsDocumentRecovery()
            .ReconcileAsync(_database, _root, project.ProjectId);
        Assert.Contains(orphanHashes[0], report.CollectedOrphanContentHashes);
        Assert.Empty(innerStore.ListPublishedContentHashes());
    }

    [Fact]
    public async Task FailedMetadataPublication_LeavesARecoverableOrphanWithoutChangingCurrentReference()
    {
        var project = ProjectRules.CreateProject("Settings Rollback", 2500);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);
        var missingMapId = Guid.NewGuid();

        await Assert.ThrowsAsync<CoreDomainDataException>(() =>
            service.SaveMapAsync(project.ProjectId, missingMapId,
                JObject.Parse("{\"orphan\":true}")));

        Assert.Empty(repository.ListMapSettingsRevisions(project.ProjectId));
        var orphanHashes = fileStore.ListPublishedContentHashes();
        Assert.Single(orphanHashes);

        var report = await new CoreDomainSettingsDocumentRecovery()
            .ReconcileAsync(_database, _root, project.ProjectId);
        Assert.Contains(orphanHashes[0], report.CollectedOrphanContentHashes);
        Assert.Empty(fileStore.ListPublishedContentHashes());
    }

    [Fact]
    public async Task FailedCurrentPointerUpdate_RollsBackNewRevisionAndPreservesPreviousCurrentDocument()
    {
        var project = ProjectRules.CreateProject("Settings Transaction Rollback", 2800);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);

        var first = await service.SaveProjectAsync(project.ProjectId,
            JObject.Parse("""{"theme":"dark"}"""));

        // Force the second SaveChanges (current-pointer UPDATE) to fail after the
        // immutable revision INSERT has already executed inside the same transaction.
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _database.DatabasePath,
            Pooling = false,
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TRIGGER TR_test_reject_project_settings_current_update
                BEFORE UPDATE ON core_project_settings_current
                BEGIN
                    SELECT RAISE(ABORT, 'injected current-pointer failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveProjectAsync(project.ProjectId,
            JObject.Parse("""{"theme":"light"}""")));

        Assert.Single(repository.ListProjectSettingsRevisions(project.ProjectId));
        var current = await service.LoadCurrentProjectAsync(project.ProjectId);
        Assert.Equal("dark", (string?)current!.Settings["theme"]);

        var hashes = fileStore.ListPublishedContentHashes();
        Assert.Equal(2, hashes.Count);
        Assert.Contains(first.ContentHash, hashes);
        var orphanHash = Assert.Single(hashes.Where(hash => hash != first.ContentHash));

        var report = await new CoreDomainSettingsDocumentRecovery()
            .ReconcileAsync(_database, _root, project.ProjectId);
        Assert.Contains(orphanHash, report.CollectedOrphanContentHashes);
        Assert.Contains(first.ContentHash, fileStore.ListPublishedContentHashes());
    }

    [Fact]
    public async Task StartupRecovery_FailsClosedWhenAReferencedRevisionIsMissing()
    {
        var project = ProjectRules.CreateProject("Missing Settings", 3000);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);
        var revision = await service.SaveProjectAsync(project.ProjectId, JObject.Parse("{\"name\":\"required\"}"));

        Assert.True(fileStore.Delete(revision.ContentHash));
        var recovery = new CoreDomainSettingsDocumentRecovery();
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            recovery.ReconcileAsync(_database, _root, project.ProjectId));
    }
    [Fact]
    public async Task StartupRecovery_FailsClosedWhenAReferencedRevisionIsCorrupt()
    {
        var project = ProjectRules.CreateProject("Corrupt Settings", 3100);
        _core.InsertProject(ProjectCreationPlan.Create(project));
        var repository = new CoreDomainSettingsDocumentRepository(_database);
        var fileStore = new SettingsDocumentFileStore(_root);
        var service = new SettingsDocumentService(repository, fileStore);
        var revision = await service.SaveProjectAsync(project.ProjectId,
            JObject.Parse("""{"name":"must remain intact"}"""));

        var path = Path.Combine(_root, revision.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        await File.WriteAllTextAsync(path, "{corrupt}");

        var recovery = new CoreDomainSettingsDocumentRecovery();
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            recovery.ReconcileAsync(_database, _root, project.ProjectId));
    }

    private sealed class CancelAfterPutFileStore(
        ISettingsDocumentFileStore inner,
        System.Threading.CancellationTokenSource cancellation) : ISettingsDocumentFileStore
    {
        public async Task<SettingsDocumentFilePutResult> PutAsync(
            Stream content, System.Threading.CancellationToken cancellationToken = default)
        {
            var result = await inner.PutAsync(content, cancellationToken);
            cancellation.Cancel();
            return result;
        }

        public Task<Stream?> OpenReadAsync(string contentHash, System.Threading.CancellationToken cancellationToken = default) =>
            inner.OpenReadAsync(contentHash, cancellationToken);

        public System.Collections.Generic.IReadOnlyList<string> ListPublishedContentHashes() =>
            inner.ListPublishedContentHashes();

        public int CleanupStagingFiles() => inner.CleanupStagingFiles();
        public bool Delete(string contentHash) => inner.Delete(contentHash);
    }

}
