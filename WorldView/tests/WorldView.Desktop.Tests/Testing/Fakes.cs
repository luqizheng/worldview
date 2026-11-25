using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Bootstrap;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Domain.Time;

namespace WorldView.Desktop.Tests.Testing;

/// <summary>
/// Desktop ViewModel 测试用的最小替身（本工程不引用 Application.Tests 的替身，
/// 按端口契约自备；仅覆盖 ViewModel 路径所需的成员）。
/// </summary>
public static class TestSessions
{
    /// <summary>构造 Ready 会话（公开 API：TryCompleteOpen 使 Opening→Ready）。</summary>
    public static ProjectSession CreateReadySession(
        Guid? projectId = null,
        IPackageDatabase? database = null,
        IAssetStore? assetStore = null)
    {
        var session = new ProjectSession(
            Guid.NewGuid(),
            "C:\\pkg\\demo.wvproject",
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            database ?? new StubPackageDatabase(),
            assetStore ?? new StubAssetStore(),
            projectId ?? Guid.NewGuid());
        Assert.True(session.TryCompleteOpen());
        return session;
    }
}

public sealed class StubPackageDatabase : IPackageDatabase
{
    public string DatabasePath => "project.sqlite";

    public bool IsDisposed { get; private set; }

    public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<BootstrapMetadata?>(null);

    public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class StubAssetStore : IAssetStore
{
    /// <summary>失败注入：指定操作抛出异常（用于关闭重试测试）。</summary>
    public Func<string, Exception?>? FailureHook { get; set; }

    public int DisposeAttempts { get; private set; }

    public int DisposeSuccesses { get; private set; }

    public string RootPath => "/stub-assets";

    public Task<AssetPutResult> PutAsync(Stream content, CancellationToken cancellationToken = default)
        => Task.FromResult(AssetPutResult.Ok(Guid.NewGuid().ToString("N")));

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    public bool Exists(string contentHash) => false;

    public ValueTask DisposeAsync()
    {
        DisposeAttempts++;
        if (FailureHook?.Invoke("AssetStore:Dispose") is { } failure)
        {
            throw failure;
        }

        DisposeSuccesses++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>可编排结果的地图创建替身；成功时把创建的地图写入可选的查询替身（模拟正式持久化）。</summary>
public sealed class FakeMapCreationService : IMapCreationService
{
    private readonly FakeMapQueryService? _linkedQuery;

    public FakeMapCreationService(FakeMapQueryService? linkedQuery = null)
    {
        _linkedQuery = linkedQuery;
    }

    public List<MapCreationRequest> Requests { get; } = new();

    public MapCreationResult? NextResult { get; set; }

    /// <summary>设置后 CreateAsync 挂起直到释放（重复提交守卫测试用）。</summary>
    public TaskCompletionSource? Gate { get; set; }

    public int InFlight { get; private set; }

    public async Task<MapCreationResult> CreateAsync(MapCreationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        InFlight++;
        try
        {
            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (NextResult is { } injected)
            {
                return injected;
            }

            var map = new MapSummary { MapId = Guid.NewGuid(), DisplayName = request.MapName.Trim() };
            _linkedQuery?.Maps.Add(map);
            return MapCreationResult.Ok(map);
        }
        finally
        {
            InFlight--;
        }
    }
}

public sealed class FakeMapQueryService : IMapQueryService
{
    public List<MapSummary> Maps { get; } = new();

    public Task<IReadOnlyList<MapSummary>> ListMapsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MapSummary>>(Maps.ToArray());

    public Task<MapSummary?> GetMapAsync(Guid projectId, Guid mapId, CancellationToken cancellationToken = default)
        => Task.FromResult<MapSummary?>(Maps.FirstOrDefault(m => m.MapId == mapId));
}

/// <summary>固定成功的上下文解析替身（WP-UI-04B：VM 打开工作区路径测试用）。</summary>
public sealed class StubContextResolver : IWorkspaceContextResolver
{
    public Task<WorkspaceContextResolution> ResolveAsync(
        Guid projectId, Guid mapId, Guid? branchId, string? historicalTimeInput, CancellationToken cancellationToken = default)
        => Task.FromResult(WorkspaceContextResolution.Ok(new ValidatedWorkspaceContext
        {
            MapId = mapId,
            BranchId = branchId,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = HistoricalTime.Epoch,
        }));
}
