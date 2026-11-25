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

namespace WorldView.Desktop.Tests.Testing;

/// <summary>
/// Desktop ViewModel 测试用的最小替身（本工程不引用 Application.Tests 的替身，
/// 按端口契约自备；仅覆盖 ViewModel 路径所需的成员）。
/// </summary>
public static class TestSessions
{
    /// <summary>构造 Ready 会话（公开 API：TryCompleteOpen 使 Opening→Ready）。</summary>
    public static ProjectSession CreateReadySession(Guid? projectId = null)
    {
        var session = new ProjectSession(
            Guid.NewGuid(),
            "C:\\pkg\\demo.wvproject",
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            new StubPackageDatabase(),
            new StubAssetStore(),
            projectId ?? Guid.NewGuid());
        Assert.True(session.TryCompleteOpen());
        return session;
    }
}

public sealed class StubPackageDatabase : IPackageDatabase
{
    public string DatabasePath => "project.sqlite";

    public bool IsDisposed { get; private set; }

    /// <summary>DisposeAsync 调用计数（生命周期测试：断言不提前释放 / 只释放一次）。</summary>
    public int DisposeCount { get; private set; }

    /// <summary>失败注入：DisposeAsync 抛出该异常（C2 失败路径测试用）。</summary>
    public Exception? DisposeFailure { get; set; }

    public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> IntegrityCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<BootstrapMetadata?> ReadMetadataAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<BootstrapMetadata?>(null);

    public Task WriteMetadataAsync(BootstrapMetadata metadata, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        DisposeCount++;
        if (DisposeFailure is { } failure)
        {
            return ValueTask.FromException(failure);
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class StubAssetStore : IAssetStore
{
    public string RootPath => "/stub-assets";

    /// <summary>DisposeAsync 调用计数（生命周期测试：断言不提前释放）。</summary>
    public int DisposeCount { get; private set; }

    public Task<AssetPutResult> PutAsync(Stream content, CancellationToken cancellationToken = default)
        => Task.FromResult(AssetPutResult.Ok(Guid.NewGuid().ToString("N")));

    public Task<Stream?> OpenReadAsync(string contentHash, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(null);

    public bool Exists(string contentHash) => false;

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
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

/// <summary>
/// 工作区上下文解析替身：默认成功（默认/主历史 + 纪元时间）；可注入失败诊断。
/// 与 Application.Tests 的 FakeResolver 同契约、自备实现（Desktop.Tests 不引用其替身）。
/// </summary>
public sealed class FakeWorkspaceContextResolver : IWorkspaceContextResolver
{
    public bool Succeed { get; set; } = true;

    public string? FailureDiagnostic { get; set; } = "Branch/历史时间解析未通过（测试注入）。";

    public Task<WorkspaceContextResolution> ResolveAsync(
        Guid projectId,
        Guid mapId,
        Guid? branchId,
        string? historicalTimeInput,
        CancellationToken cancellationToken = default)
    {
        if (!Succeed)
        {
            return Task.FromResult(WorkspaceContextResolution.Failed(FailureDiagnostic!));
        }

        return Task.FromResult(WorkspaceContextResolution.Ok(new ValidatedWorkspaceContext
        {
            MapId = mapId,
            BranchId = branchId,
            RootHistoryId = Guid.NewGuid(),
            HistoricalTime = WorldView.Domain.Time.HistoricalTime.Epoch,
        }));
    }
}

/// <summary>
/// 可注入故障的日志替身（DEF-03）：Warn 可被配置为主动抛异常，
/// 用于验证 C2 编排路径的日志异常不参与生命周期收尾的控制流。
/// </summary>
public sealed class FakeAppLog : IAppLog
{
    public int WarnCount { get; private set; }

    /// <summary>Warn 实际抛出异常的次数（与 WarnCount 分离：调用过 ≠ 抛出过）。</summary>
    public int WarnThrowCount { get; private set; }

    public List<string> WarnMessages { get; } = new();

    /// <summary>非 null 时 Warn 调用抛出该异常（模拟日志实现自身故障）。</summary>
    public Exception? WarnFailure { get; set; }

    public void Info(string category, string message)
    {
    }

    public void Warn(string category, string message, Exception? exception = null)
    {
        WarnCount++;
        WarnMessages.Add(message);
        if (WarnFailure is { } failure)
        {
            WarnThrowCount++;
            throw failure;
        }
    }

    public void Error(string category, string message, Exception? exception = null)
    {
    }
}
