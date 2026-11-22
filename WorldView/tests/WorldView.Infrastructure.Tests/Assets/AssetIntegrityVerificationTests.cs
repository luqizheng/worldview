using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WorldView.Application.Bootstrap;
using WorldView.Application.UseCases;
using WorldView.Domain.Assets;
using WorldView.Domain.Persistence;
using WorldView.Domain.Projects;
using WorldView.Domain.Persistence;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence;
using WorldView.Infrastructure.Persistence.CoreDomain;
using Xunit;

namespace WorldView.Infrastructure.Tests.Assets;

public sealed class AssetIntegrityVerificationTests : IDisposable
{
    private const long Now = 1_000L;
    private readonly string _root;
    private readonly SqlitePackageDatabase _database;
    private readonly SqliteCoreDomainDatabase _core;
    private readonly ProjectRecord _project;

    public AssetIntegrityVerificationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-asset-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var factory = new SqlitePackageDatabaseFactory();
        _database = (SqlitePackageDatabase)factory.CreateForFile(Path.Combine(_root, "data", "project.sqlite"));
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        _database.OpenAsync().GetAwaiter().GetResult();

        var bootstrapper = new ApplicationBootstrapper(new[] { new CoreDomainMigrationSource() });
        bootstrapper.InitializeNewDatabaseAsync(_database, DateTimeOffset.UtcNow).GetAwaiter().GetResult();

        _core = new SqliteCoreDomainDatabase(_database);
        _project = ProjectRules.CreateProject("Asset verification test", Now);
        _core.InsertProject(ProjectCreationPlan.Create(_project));
    }

    public void Dispose()
    {
        _database.DisposeAsync().AsTask().GetAwaiter().GetResult();
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task ExecuteAsync_ValidBytes_MarksAssetVerified()
    {
        var bytes = Encoding.UTF8.GetBytes("immutable geometry payload");
        await using var store = new FileAssetStore(_root);
        var put = await store.PutAsync(new MemoryStream(bytes));
        Assert.True(put.Success);

        var asset = AssetService.RegisterAsset(
            _core, _project.ProjectId, Guid.NewGuid(), put.ContentHash!, bytes.Length,
            "application/octet-stream", "1", Now);

        var result = await new VerifyAssetIntegrityUseCase(_core, store).ExecuteAsync(_project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Verified, result.Outcome);
        Assert.Equal(put.ContentHash, result.ActualHash);
        Assert.Equal(AssetIntegrityState.Verified, _core.FindAsset(_project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task ExecuteAsync_MissingBytes_MarksAssetMissing()
    {
        var bytes = Encoding.UTF8.GetBytes("not published");
        var hash = AssetRules.Sha256Hex(bytes);
        var asset = AssetService.RegisterAsset(
            _core, _project.ProjectId, Guid.NewGuid(), hash, bytes.Length,
            "application/octet-stream", "1", Now);
        await using var store = new FileAssetStore(_root);

        var result = await new VerifyAssetIntegrityUseCase(_core, store).ExecuteAsync(_project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Missing, result.Outcome);
        Assert.Equal(AssetIntegrityState.Missing, _core.FindAsset(_project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task ExecuteAsync_ChangedBytes_MarksAssetCorrupt()
    {
        var bytes = Encoding.UTF8.GetBytes("original payload");
        await using var store = new FileAssetStore(_root);
        var put = await store.PutAsync(new MemoryStream(bytes));
        Assert.True(put.Success);

        var asset = AssetService.RegisterAsset(
            _core, _project.ProjectId, Guid.NewGuid(), put.ContentHash!, bytes.Length,
            "application/octet-stream", "1", Now);
        await File.WriteAllBytesAsync(Path.Combine(store.RootPath, put.ContentHash!), Encoding.UTF8.GetBytes("tampered"));

        var result = await new VerifyAssetIntegrityUseCase(_core, store).ExecuteAsync(_project.ProjectId, asset.AssetId);

        Assert.Equal(AssetIntegrityVerificationOutcome.Corrupt, result.Outcome);
        Assert.NotEqual(asset.ContentHash, result.ActualHash);
        Assert.Equal(AssetIntegrityState.Corrupt, _core.FindAsset(_project.ProjectId, asset.AssetId)!.IntegrityState);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAsset_DoesNotMutateDatabase()
    {
        await using var store = new FileAssetStore(_root);
        var unknownId = Guid.NewGuid();

        var result = await new VerifyAssetIntegrityUseCase(_core, store).ExecuteAsync(_project.ProjectId, unknownId);

        Assert.Equal(AssetIntegrityVerificationOutcome.AssetNotFound, result.Outcome);
        Assert.Null(_core.FindAsset(_project.ProjectId, unknownId));
    }
}
