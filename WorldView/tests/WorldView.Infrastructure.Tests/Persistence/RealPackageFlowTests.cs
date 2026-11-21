using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Infrastructure.Assets;
using WorldView.Infrastructure.IO;
using WorldView.Infrastructure.Persistence;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence;

/// <summary>
/// 真实 Microsoft.Data.Sqlite + 真实文件系统的包/会话端到端测试（AF-02 §6 真实集成）。
/// Core Domain 读取/创建服务仍以测试替身提供（正式实现不存在）；真实 Map 读路径
/// 的阻塞状态在 AF-02 实施报告中单独记录。
/// </summary>
public class RealPackageFlowTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystem _fileSystem = new();
    private readonly SqlitePackageDatabaseFactory _databaseFactory = new();
    private readonly FileAssetStoreFactory _assetStoreFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper;

    public RealPackageFlowTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"wv-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _bootstrapper = new ApplicationBootstrapper(Array.Empty<IMigrationSource>());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 清理失败不影响测试结论。
        }
    }

    private static async Task<byte[]> RandomBytesAsync(int count)
    {
        var bytes = new byte[count];
        RandomNumberGenerator.Fill(bytes);
        await Task.CompletedTask;
        return bytes;
    }

    [Fact]
    public async Task CreateWithoutCoreDomainService_NothingPublished_TempCleaned()
    {
        var useCase = new CreateProjectUseCase(_fileSystem, _databaseFactory, _bootstrapper, null);

        var result = await useCase.ExecuteAsync(_root, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.CoreDomainProjectCreationUnavailable, result.Outcome);
        Assert.Empty(Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CreateWithCoreDomainService_PublishesPackage_AndOpensToReadySession()
    {
        // 1. 创建（核心创建服务以替身提供——正式实现不存在）。
        var create = new CreateProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, new FakeCreationService());
        var created = await create.ExecuteAsync(_root, "我的世界", "我的世界");
        Assert.Equal(CreateProjectOutcome.Succeeded, created.Outcome);
        var packageRoot = created.PackageRootPath!;

        // 包布局（AF-01 §3.1）。
        Assert.True(File.Exists(Path.Combine(packageRoot, ProjectPackageFormat.ManifestFileName)));
        Assert.True(File.Exists(Path.Combine(packageRoot, ProjectPackageFormat.DatabaseFileName)));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, ProjectPackageFormat.AssetsDirectoryName, ProjectPackageFormat.AssetHashDirectoryName)));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, ProjectPackageFormat.StagingDirectoryName)));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, ProjectPackageFormat.BackupsDirectoryName)));

        // 2. 打开（身份读取以替身提供——正式 Core Domain 读取服务不存在）。
        var registry = new ProjectSessionRegistry();
        var open = new OpenProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, registry,
            new FakeIdentityReader());
        var opened = await open.ExecuteAsync(packageRoot);
        Assert.Equal(OpenProjectOutcome.Succeeded, opened.Outcome);
        var session = opened.Session!;
        Assert.True(session.IsReady);
        Assert.Equal("我的世界", session.ProjectDisplayName);

        // 3. 地图列表与工作区（经 Core Domain 端口替身）。
        var maps = await FakeMapQuery.WithMaps("主地图", "南方边境").ListMapsAsync(session.ProjectId!.Value);
        Assert.Equal(2, maps.Count);

        // 4. 关闭：会话关闭且数据库句柄释放后，包可被再次打开。
        await session.CloseAsync();
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(registry.Sessions);

        var reopened = await open.ExecuteAsync(packageRoot);
        Assert.Equal(OpenProjectOutcome.Succeeded, reopened.Outcome);
        await reopened.Session!.CloseAsync();
    }

    [Fact]
    public async Task OpenWithoutIdentityReader_ReportsGate()
    {
        var create = new CreateProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, new FakeCreationService());
        var created = await create.ExecuteAsync(_root, "我的世界", "我的世界");
        Assert.Equal(CreateProjectOutcome.Succeeded, created.Outcome);

        var registry = new ProjectSessionRegistry();
        var open = new OpenProjectUseCase(
            _fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, registry, identityReader: null);
        var opened = await open.ExecuteAsync(created.PackageRootPath!);

        Assert.Equal(OpenProjectOutcome.ProjectIdentityUnavailable, opened.Outcome);
        Assert.Empty(registry.Sessions);
    }

    [Fact]
    public async Task AssetStore_ContentAddressed_Immutable_MissingAssetReportedNotSubstituted()
    {
        var packageRoot = Path.Combine(_root, "asset-demo.wvproject");
        Directory.CreateDirectory(packageRoot);
        await using var store = new FileAssetStore(packageRoot);

        var bytes = await RandomBytesAsync(4096);
        AssetPutResult put1;
        using (var stream = new MemoryStream(bytes))
        {
            put1 = await store.PutAsync(stream);
        }

        Assert.True(put1.Success);
        Assert.Equal(64, put1.ContentHash!.Length);
        Assert.True(store.Exists(put1.ContentHash));

        // 同内容再次写入 → 内容寻址幂等，复用同一文件。
        AssetPutResult put2;
        using (var stream = new MemoryStream(bytes))
        {
            put2 = await store.PutAsync(stream);
        }

        Assert.Equal(put1.ContentHash, put2.ContentHash);

        // 取回内容一致。
        await using (var read = await store.OpenReadAsync(put1.ContentHash!) ?? throw new InvalidOperationException("Asset 应存在"))
        {
            using var memory = new MemoryStream();
            await read.CopyToAsync(memory);
            Assert.Equal(bytes, memory.ToArray());
        }

        // 缺失 Asset：返回 null（完整性错误由调用方上报），绝不静默替换。
        var missingHash = Convert.ToHexString(SHA256.HashData(await RandomBytesAsync(64))).ToLowerInvariant();
        Assert.Null(await store.OpenReadAsync(missingHash));
        Assert.False(store.Exists(missingHash));

        // 非法哈希（路径注入）被拒绝。
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync("../evil"));
    }

    [Fact]
    public async Task CorruptDatabase_RejectedByValidator()
    {
        var packageRoot = Path.Combine(_root, "corrupt.wvproject");
        Directory.CreateDirectory(packageRoot);
        await File.WriteAllTextAsync(
            Path.Combine(packageRoot, ProjectPackageFormat.ManifestFileName),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow).ToJson());
        Directory.CreateDirectory(Path.Combine(packageRoot, ProjectPackageFormat.AssetsDirectoryName, ProjectPackageFormat.AssetHashDirectoryName));

        // 写入非 SQLite 的垃圾字节。
        await File.WriteAllBytesAsync(
            Path.Combine(packageRoot, ProjectPackageFormat.DatabaseFileName),
            await RandomBytesAsync(2048));

        var validator = new ProjectPackageValidator(
            _fileSystem, _databaseFactory, _bootstrapper, identityReader: null);
        var outcome = await validator.ValidateAsync(packageRoot);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.DatabaseCorrupt, outcome.Report.FirstFailureCode);
        // 校验器不改写包内容（损坏文件保持原样）。
        var bytesAfter = await File.ReadAllBytesAsync(Path.Combine(packageRoot, ProjectPackageFormat.DatabaseFileName));
        Assert.Equal(2048, bytesAfter.Length);
    }

    [Fact]
    public async Task ManifestWithoutPackageSuffix_Rejected()
    {
        var plainDir = Path.Combine(_root, "not-a-package");
        Directory.CreateDirectory(plainDir);

        var validator = new ProjectPackageValidator(
            _fileSystem, _databaseFactory, _bootstrapper, identityReader: null);
        var outcome = await validator.ValidateAsync(plainDir);

        Assert.True(outcome.Report.HasFailures);
        Assert.Equal(PackageIssueCode.PackageSuffixMissing, outcome.Report.FirstFailureCode);
    }

    private sealed class FakeCreationService : IProjectCreationService
    {
        public Task<ProjectCreationResult> CreateAsync(ProjectCreationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectCreationResult.Ok(new ProjectIdentity
            {
                ProjectId = Guid.NewGuid(),
                DisplayName = request.DisplayName,
            }));
    }

    private sealed class FakeIdentityReader : IProjectIdentityReader
    {
        public Task<ProjectIdentityReadResult> ReadAsync(IPackageDatabase database, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectIdentityReadResult.Ok(new ProjectIdentity
            {
                ProjectId = Guid.NewGuid(),
                DisplayName = "我的世界",
            }));
    }

    private sealed class FakeMapQuery : IMapQueryService
    {
        private readonly MapSummary[] _maps;

        private FakeMapQuery(params string[] names) => _maps = names
            .Select(n => new MapSummary { MapId = Guid.NewGuid(), DisplayName = n })
            .ToArray();

        public static FakeMapQuery WithMaps(params string[] names) => new(names);

        public Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MapSummary>>(_maps);

        public Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default)
            => Task.FromResult(_maps.FirstOrDefault(m => m.MapId == mapId));
    }
}
