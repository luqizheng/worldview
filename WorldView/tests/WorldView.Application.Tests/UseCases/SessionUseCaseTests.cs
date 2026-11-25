using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Domain.Time;
using Xunit;

namespace WorldView.Application.Tests.UseCases;

/// <summary>CreateProject 用例测试（AF-02 §6：不覆盖、临时清理、bootstrap 门槛、发布路径）。</summary>
public class CreateProjectUseCaseTests : IDisposable
{
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FakePackageDatabaseFactory _databaseFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly string _parent;

    public CreateProjectUseCaseTests()
    {
        _parent = Path.Combine(Path.GetTempPath(), $"wv-create-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(_parent);
    }

    public void Dispose() => _fileSystem.DeleteDirectory(_parent);

    private CreateProjectUseCase CreateUseCase(IProjectCreationService? creationService = null)
        => new(_fileSystem, _databaseFactory, _bootstrapper, creationService, TimeProvider.System);

    [Fact]
    public async Task WithoutCoreDomainService_StopsAtBootstrapBoundary_AndCleansTemp()
    {
        var result = await CreateUseCase().ExecuteAsync(_parent, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.CoreDomainProjectCreationUnavailable, result.Outcome);
        Assert.Null(result.PackageRootPath);
        // 不发布任何包文件，临时目录已清理。
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.EndsWith(".wvproject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithCoreDomainService_PublishesPackageViaRename()
    {
        var creationService = new RecordingProjectCreationService();
        var result = await CreateUseCase(creationService).ExecuteAsync(_parent, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.Succeeded, result.Outcome);
        Assert.Equal(Path.Combine(_parent, "我的世界.wvproject"), result.PackageRootPath);
        Assert.True(_fileSystem.DirectoryExists(result.PackageRootPath!));
        Assert.True(_fileSystem.FileExists(Path.Combine(result.PackageRootPath!, ProjectPackageFormat.ManifestFileName)));
        Assert.True(_fileSystem.DirectoryExists(Path.Combine(result.PackageRootPath!, ProjectPackageFormat.AssetsDirectoryName, ProjectPackageFormat.AssetHashDirectoryName)));
        Assert.True(_fileSystem.DirectoryExists(Path.Combine(result.PackageRootPath!, ProjectPackageFormat.StagingDirectoryName)));
        Assert.True(_fileSystem.DirectoryExists(Path.Combine(result.PackageRootPath!, ProjectPackageFormat.BackupsDirectoryName)));
        Assert.Equal(1, creationService.Requests.Count);
        // 创建服务收到的仍是临时路径：领域写入发生在发布之前。
        Assert.Contains(".tmp-", creationService.Requests[0].PackageRootPath, StringComparison.Ordinal);
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingDestination_NeverOverwritten()
    {
        var existing = Path.Combine(_parent, "我的世界.wvproject");
        _fileSystem.CreateDirectory(existing);
        _fileSystem.WriteAllText(Path.Combine(existing, "user-content.txt"), "keep");

        var result = await CreateUseCase(new RecordingProjectCreationService()).ExecuteAsync(_parent, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.DestinationExists, result.Outcome);
        Assert.Equal("keep", _fileSystem.ReadAllText(Path.Combine(existing, "user-content.txt")));
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedCoreDomainCreation_CleansTempOnly()
    {
        var creationService = new RecordingProjectCreationService(
            ProjectCreationResult.Failed("领域校验拒绝。"));

        var result = await CreateUseCase(creationService).ExecuteAsync(_parent, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.CoreDomainProjectCreationFailed, result.Outcome);
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.EndsWith(".wvproject", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.Contains(".tmp-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidInputs_Rejected()
    {
        Assert.Equal(CreateProjectOutcome.InvalidInput, (await CreateUseCase().ExecuteAsync(_parent, "", "x")).Outcome);
        Assert.Equal(CreateProjectOutcome.InvalidInput, (await CreateUseCase().ExecuteAsync(_parent, "a/b", "x")).Outcome);
        Assert.Equal(CreateProjectOutcome.InvalidInput, (await CreateUseCase().ExecuteAsync(Path.Combine(_parent, "不存在"), "x", "x")).Outcome);
    }

    [Fact]
    public async Task BootstrapFailure_CleansTemp()
    {
        // 注入失败：bootstrap 初始化期间抛异常。
        var factory = new FailingDatabaseFactory();
        var useCase = new CreateProjectUseCase(_fileSystem, factory, _bootstrapper, null, TimeProvider.System);

        var result = await useCase.ExecuteAsync(_parent, "我的世界", "我的世界");

        Assert.Equal(CreateProjectOutcome.IoFailure, result.Outcome);
        Assert.DoesNotContain(_fileSystem.SnapshotPaths(), p => p.Contains(".tmp-", StringComparison.Ordinal));
    }

    private sealed class RecordingProjectCreationService(
        ProjectCreationResult? forcedResult = null) : IProjectCreationService
    {
        public List<ProjectCreationRequest> Requests { get; } = new();

        public Task<ProjectCreationResult> CreateAsync(ProjectCreationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (forcedResult is not null)
            {
                return Task.FromResult(forcedResult);
            }

            return Task.FromResult(ProjectCreationResult.Ok(new ProjectIdentity
            {
                ProjectId = Guid.NewGuid(),
                DisplayName = request.DisplayName,
            }));
        }
    }

    private sealed class FailingDatabaseFactory : IPackageDatabaseFactory
    {
        public IPackageDatabase CreateForFile(string databasePath) =>
            new FailingDatabase(databasePath);

        private sealed class FailingDatabase(string path) : IPackageDatabase
        {
            public string DatabasePath => path;

            public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

            public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default) => Task.FromResult<BootstrapMetadata?>(null);

            public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
                => throw new IOException("注入失败：元数据写入失败。");

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

/// <summary>OpenProject 用例测试（AF-02 §6：重复打开、门槛、就绪、注册表）。</summary>
public class OpenProjectUseCaseTests : IDisposable
{
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FakePackageDatabaseFactory _databaseFactory = new();
    private readonly ApplicationBootstrapper _bootstrapper = new();
    private readonly FakeAssetStoreFactory _assetStoreFactory = new();
    private readonly ProjectSessionRegistry _registry = new();
    private readonly string _packageRoot;

    public OpenProjectUseCaseTests()
    {
        _packageRoot = Path.Combine(Path.GetTempPath(), $"wv-open-{Guid.NewGuid():N}.wvproject");
    }

    public void Dispose() => _fileSystem.DeleteDirectory(_packageRoot);

    private OpenProjectUseCase CreateUseCase(IProjectIdentityReader? reader = null)
        => new(_fileSystem, _databaseFactory, _bootstrapper, _assetStoreFactory, _registry, reader);

    private async Task<CreateProjectResult> BuildPackageAsync(IProjectCreationService? creationService = null)
    {
        _fileSystem.CreateDirectory(Path.GetTempPath());
        var create = new CreateProjectUseCase(_fileSystem, _databaseFactory, _bootstrapper, creationService, TimeProvider.System);
        var result = await create.ExecuteAsync(Path.GetTempPath(), Path.GetFileName(_packageRoot), "测试项目");
        if (result.Outcome == CreateProjectOutcome.Succeeded)
        {
            // 假数据库在文件系统层以占位文件表示；重命名发布后，数据库文件携带的
            // bootstrap 状态一并“迁移”到最终路径（真实 SQLite 文件天然如此）。
            _fileSystem.WriteAllText(
                Path.Combine(_packageRoot, ProjectPackageFormat.DatabaseFileName), string.Empty);
            var database = (FakePackageDatabase)_databaseFactory.CreateForFile(
                Path.Combine(_packageRoot, ProjectPackageFormat.DatabaseFileName));
            await database.OpenAsync();
            await database.WriteMetadataAsync(new BootstrapMetadata
            {
                DatabaseFormatVersion = ProjectPackageFormat.SupportedDatabaseFormatVersion,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await database.DisposeAsync();
        }

        return result;
    }

    [Fact]
    public async Task WithoutIdentityReader_ReportsGate_AndDoesNotExposeSession()
    {
        await BuildPackageAsync(new RecordingCreationService());

        var result = await CreateUseCase().ExecuteAsync(_packageRoot);

        Assert.Equal(OpenProjectOutcome.ProjectIdentityUnavailable, result.Outcome);
        Assert.Null(result.Session);
        Assert.Empty(_registry.Sessions);
    }

    [Fact]
    public async Task WithIdentityReader_SessionReadyAndRegistered()
    {
        await BuildPackageAsync(new RecordingCreationService());

        var result = await CreateUseCase(new FakeProjectIdentityReader()).ExecuteAsync(_packageRoot);

        Assert.Equal(OpenProjectOutcome.Succeeded, result.Outcome);
        Assert.NotNull(result.Session);
        Assert.True(result.Session!.IsReady);
        Assert.NotNull(result.Session.ProjectId);
        Assert.Single(_registry.Sessions);
    }

    [Fact]
    public async Task DuplicateOpen_Prevented_InSameProcess()
    {
        await BuildPackageAsync(new RecordingCreationService());
        var useCase = CreateUseCase(new FakeProjectIdentityReader());

        var first = await useCase.ExecuteAsync(_packageRoot);
        var second = await useCase.ExecuteAsync(_packageRoot);

        Assert.Equal(OpenProjectOutcome.Succeeded, first.Outcome);
        Assert.Equal(OpenProjectOutcome.DuplicateOpen, second.Outcome);
        Assert.Same(first.Session, second.ExistingSession);
        Assert.Single(_registry.Sessions);

        await first.Session!.CloseAsync();
    }

    [Fact]
    public async Task InvalidPackage_FailsClosed()
    {
        _fileSystem.CreateDirectory(_packageRoot);

        var result = await CreateUseCase(new FakeProjectIdentityReader()).ExecuteAsync(_packageRoot);

        Assert.Equal(OpenProjectOutcome.InvalidPackage, result.Outcome);
        Assert.True(result.Report!.HasFailures);
        Assert.Empty(_registry.Sessions);
    }

    [Fact]
    public async Task FailedOpen_RemovesRegistryPlaceholder()
    {
        await BuildPackageAsync(new RecordingCreationService());
        // 身份读取失败 → fail-closed，占位从注册表移除，可再次尝试。
        var failingReader = new FakeProjectIdentityReader
        {
            Result = ProjectIdentityReadResult.Failed(ProjectIdentityFailure.Invalid, "身份损坏。"),
        };

        var first = await CreateUseCase(failingReader).ExecuteAsync(_packageRoot);

        // 身份读取失败属于包校验失败（fail-closed），占位从注册表移除后可再次尝试。
        Assert.Equal(OpenProjectOutcome.InvalidPackage, first.Outcome);
        Assert.Equal(PackageIssueCode.ProjectIdentityInvalid, first.Report!.FirstFailureCode);
        Assert.Empty(_registry.Sessions);

        var second = await CreateUseCase(new FakeProjectIdentityReader()).ExecuteAsync(_packageRoot);
        Assert.Equal(OpenProjectOutcome.Succeeded, second.Outcome);
    }

    [Fact]
    public async Task CancelledOpen_WhileIdentityRead_DisposesResources_AndNeverReady()
    {
        await BuildPackageAsync(new RecordingCreationService());
        using var cts = new CancellationTokenSource();

        var reader = new FakeProjectIdentityReader
        {
            DelayHook = (_, token) =>
            {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
        };

        var result = await CreateUseCase(reader).ExecuteAsync(_packageRoot, cts.Token);

        Assert.Equal(OpenProjectOutcome.Cancelled, result.Outcome);
        Assert.Empty(_registry.Sessions);
        // 打开阶段建立的数据库实例（按已发布包的数据库文件路径缓存）已释放。
        var database = Assert.Single(_databaseFactory.Instances
            .Where(db => db.DatabasePath.Equals(
                Path.Combine(_packageRoot, ProjectPackageFormat.DatabaseFileName), StringComparison.OrdinalIgnoreCase)));
        Assert.Contains("Database:Dispose", database.CallLog);
    }

    private sealed class RecordingCreationService : IProjectCreationService
    {
        public Task<ProjectCreationResult> CreateAsync(ProjectCreationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(ProjectCreationResult.Ok(new ProjectIdentity
            {
                ProjectId = Guid.NewGuid(),
                DisplayName = request.DisplayName,
            }));
    }
}

/// <summary>会话生命周期与隔离测试（AF-02 §6）。</summary>
public class ProjectSessionLifecycleTests
{
    private static readonly ApplicationBootstrapper Bootstrapper = new();

    private static ProjectSession CreateSession(FakePackageDatabase database, FakeAssetStore assetStore)
        => new(Guid.NewGuid(), ProjectPackageStructure.NormalizePackagePath(Path.Combine(Path.GetTempPath(), $"wv-session-{Guid.NewGuid():N}.wvproject")),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow), database, assetStore, Guid.NewGuid());

    [Fact]
    public async Task CloseSession_ClosesChildrenFirst_ThenAssets_ThenDatabase()
    {
        var sharedLog = new List<string>();
        var database = new FakePackageDatabase("db.sqlite") { SharedLog = sharedLog };
        var assetStore = new FakeAssetStore { SharedLog = sharedLog };
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);
        var workspace = await session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        await session.CloseAsync();

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.True(workspace.IsClosed);
        Assert.True(workspace.IsDisposed); // 会话关闭统一释放子工作区的取消源
        // 逆序释放：Asset Store 在数据库之前。
        var assetDispose = sharedLog.IndexOf("AssetStore:Dispose");
        var databaseDispose = sharedLog.IndexOf("Database:Dispose");
        Assert.True(assetDispose >= 0 && databaseDispose >= 0 && assetDispose < databaseDispose);
    }

    [Fact]
    public async Task ClosingProject_ClosesAllChildWorkspaces_First()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);
        var w1 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());
        var w2 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        await session.CloseAsync();

        Assert.True(w1.IsClosed);
        Assert.True(w2.IsClosed);
        Assert.Equal(ProjectSessionState.Closed, session.State);
    }

    [Fact]
    public async Task ClosingOneWorkspace_KeepsSiblingAlive()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);
        var w1 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());
        var w2 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        await session.CloseWorkspaceAsync(w1.WorkspaceId);

        Assert.True(w1.IsClosed);
        Assert.False(w2.IsClosed);
        Assert.True(session.IsReady);
        Assert.Single(session.Workspaces);
    }

    [Fact]
    public async Task Workspaces_IndependentMutableState()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);
        var w1 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());
        var w2 = await session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        w1.ViewState.CameraX = 12;
        w1.ViewState.ActiveTool = "brush";
        w1.ViewState.SelectedItemIds.Add(Guid.NewGuid());

        Assert.Equal(0, w2.ViewState.CameraX);
        Assert.Equal(string.Empty, w2.ViewState.ActiveTool);
        Assert.Empty(w2.ViewState.SelectedItemIds);
        Assert.NotSame(w1.ViewState, w2.ViewState);
    }

    [Fact]
    public async Task LateCompletion_AfterClose_CannotResurrectSession()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);

        // 确定性编排：解析器在到达“可返回”点后暂停，主线程先关闭会话再放行解析结果。
        var started = new SemaphoreSlim(0, 1);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = FakeResolver.GatedAndPaused(started, resume.Task);
        var openTask = session.OpenWorkspaceAsync(Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), resolver);
        await started.WaitAsync();

        await session.CloseAsync(); // 解析等待期间关闭会话
        resume.SetResult();         // 放行“晚到达”的解析结果

        await Assert.ThrowsAsync<InvalidOperationException>(() => openTask);
        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.Empty(session.Workspaces);
    }

    [Fact]
    public async Task OpenWorkspace_BeforeReady_Rejected()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success()));
    }

    [Fact]
    public async Task TryCompleteOpen_AfterAbort_ReturnsFalse()
    {
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.MarkAborted(ProjectSessionState.Failed);

        Assert.False(session.TryCompleteOpen());
        Assert.Equal(ProjectSessionState.Failed, session.State);
    }

    [Fact]
    public async Task Registry_CloseAll_StopsAcceptingNewSessions()
    {
        var registry = new ProjectSessionRegistry();
        var database = new FakePackageDatabase("db.sqlite");
        var assetStore = new FakeAssetStore();
        var session = CreateSession(database, assetStore);
        session.TransitionTo(ProjectSessionState.Ready);
        Assert.True(registry.TryRegister(session));

        await registry.CloseAllAsync();

        Assert.Equal(ProjectSessionState.Closed, session.State);
        Assert.False(registry.TryRegister(session));
        Assert.Empty(registry.Sessions);
    }
}

/// <summary>假 Asset Store（会话测试用；可与数据库共享调用序列以断言释放顺序）。</summary>
public sealed class FakeAssetStore : IAssetStore
{
    public List<string> CallLog { get; } = new();

    /// <summary>与数据库共享的调用序列（用于断言释放顺序）。</summary>
    public List<string>? SharedLog { get; set; }

    /// <summary>失败注入：指定操作抛出异常。</summary>
    public Func<string, Exception?>? FailureHook { get; set; }

    /// <summary>Dispose 调用次数（含失败尝试），用于验证失败后重试不重复释放已成功资源。</summary>
    public int DisposeAttempts { get; private set; }

    /// <summary>Dispose 成功次数。</summary>
    public int DisposeSuccesses { get; private set; }

    private void Record(string operation)
    {
        CallLog.Add(operation);
        SharedLog?.Add(operation);
    }

    public string RootPath => "/fake-assets";

    public Task<AssetPutResult> PutAsync(Stream content, CancellationToken cancellationToken = default)
    {
        Record("Put");
        return Task.FromResult(AssetPutResult.Ok("0" + Guid.NewGuid().ToString("N")));
    }

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        Record("OpenRead");
        return Task.FromResult<Stream?>(null);
    }

    public bool Exists(string contentHash)
    {
        Record("Exists");
        return false;
    }

    public ValueTask DisposeAsync()
    {
        DisposeAttempts++;
        Record("AssetStore:Dispose");
        if (FailureHook?.Invoke("AssetStore:Dispose") is { } failure)
        {
            throw failure;
        }

        DisposeSuccesses++;
        return ValueTask.CompletedTask;
    }
}

public sealed class FakeAssetStoreFactory : IAssetStoreFactory
{
    public List<FakeAssetStore> Created { get; } = new();

    public IAssetStore CreateForPackage(string packageRootPath)
    {
        var store = new FakeAssetStore();
        Created.Add(store);
        return store;
    }
}

public sealed class FakeMapQuery : IMapQueryService
{
    private readonly MapSummary[] _maps;
    private readonly bool _matchAnyId;

    private FakeMapQuery(bool matchAnyId, params MapSummary[] maps)
    {
        _matchAnyId = matchAnyId;
        _maps = maps;
    }

    /// <summary>任意 Map ID 均可命中（会话生命周期测试关注生命周期而非查询语义）。</summary>
    public static FakeMapQuery WithAnyMap()
    {
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" };
        return new FakeMapQuery(matchAnyId: true, map);
    }

    public static FakeMapQuery WithOneMap()
    {
        var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = "主地图" };
        return new FakeMapQuery(matchAnyId: false, map);
    }

    public Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MapSummary>>(_maps);

    public Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default)
        => Task.FromResult(_matchAnyId ? _maps.FirstOrDefault() : _maps.FirstOrDefault(m => m.MapId == mapId));
}

public sealed class FakeResolver : IWorkspaceContextResolver
{
    /// <summary>默认 RootHistoryId（固定值；供身份保持断言使用）。</summary>
    public static readonly Guid DefaultRootHistoryId = new("0F4A9C2E-5B71-4D63-8EA2-C31D90B7A554");

    private readonly SemaphoreSlim? _startedSignal;
    private readonly Task? _blockOnTask;
    private readonly Guid _rootHistoryId;

    private FakeResolver(SemaphoreSlim? startedSignal = null, Task? blockOnTask = null, Guid? rootHistoryId = null)
    {
        _startedSignal = startedSignal;
        _blockOnTask = blockOnTask;
        _rootHistoryId = rootHistoryId ?? DefaultRootHistoryId;
    }

    /// <summary>成功解析（RootHistoryId 可覆盖，默认 <see cref="DefaultRootHistoryId"/>）。</summary>
    public static FakeResolver Success(Guid? rootHistoryId = null) => new(rootHistoryId: rootHistoryId);

    /// <summary>通知“解析已到达返回点”后暂停，直到放行任务完成——用于晚到达保护的确定性测试。</summary>
    public static FakeResolver GatedAndPaused(SemaphoreSlim startedSignal, Task blockOnTask) => new(startedSignal, blockOnTask);

    public async Task<WorkspaceContextResolution> ResolveAsync(
        Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
    {
        if (_startedSignal is not null)
        {
            _startedSignal.Release();
        }

        if (_blockOnTask is not null)
        {
            await _blockOnTask;
        }

        // 与真实 Domain 语义一致：null 输入解析为纪元；非法输入拒绝（不伪造 typed 值）。
        HistoricalTime time = HistoricalTime.Epoch;
        if (historicalTimeInput is not null)
        {
            if (!HistoricalTime.TryParse(historicalTimeInput, out var parsed) || parsed is null)
            {
                return WorkspaceContextResolution.Failed(
                    $"历史时间输入 \"{historicalTimeInput}\" 不是合法的规范格式 \"day:offset\"。");
            }

            time = parsed;
        }

        return WorkspaceContextResolution.Ok(new ValidatedWorkspaceContext
        {
            MapId = mapId,
            BranchId = branchId,
            RootHistoryId = _rootHistoryId,
            HistoricalTime = time,
        });
    }
}
