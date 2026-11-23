using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using Xunit;

namespace WorldView.Application.Tests.UseCases;

/// <summary>WP-20a Create Map 用例测试：会话/输入边界、端口编排、失败类别透传。</summary>
public class CreateMapUseCaseTests
{
    private static ProjectSession CreateReadySession(Guid? projectId)
    {
        var session = new ProjectSession(
            Guid.NewGuid(),
            "C:\\pkg\\demo.wvproject",
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            new FakePackageDatabase("project.sqlite"),
            new FakeAssetStore(),
            projectId);
        session.TransitionTo(ProjectSessionState.Ready);
        return session;
    }

    [Fact]
    public async Task ValidName_CallsPortWithTrimmedNameAndSessionProject()
    {
        var service = new FakeMapCreationService();
        var session = CreateReadySession(Guid.NewGuid());
        var useCase = new CreateMapUseCase(session, service);

        var result = await useCase.ExecuteAsync("  主地图  ");

        Assert.True(result.Success);
        var request = Assert.Single(service.Requests);
        Assert.Equal("主地图", request.MapName);
        Assert.Equal(session.ProjectId, request.ProjectId);
    }

    [Fact]
    public async Task EmptyName_RejectedWithoutPortCall()
    {
        var service = new FakeMapCreationService();
        var useCase = new CreateMapUseCase(CreateReadySession(Guid.NewGuid()), service);

        var result = await useCase.ExecuteAsync("");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.InvalidName, result.FailureKind);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task WhitespaceOnlyName_RejectedWithoutPortCall()
    {
        var service = new FakeMapCreationService();
        var useCase = new CreateMapUseCase(CreateReadySession(Guid.NewGuid()), service);

        var result = await useCase.ExecuteAsync("   ");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.InvalidName, result.FailureKind);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task DuplicateName_PortFailurePropagatesWithKind()
    {
        var service = new FakeMapCreationService
        {
            NextResult = MapCreationResult.Failed(MapCreationFailureKind.DuplicateName, "同名地图已存在。"),
        };
        var useCase = new CreateMapUseCase(CreateReadySession(Guid.NewGuid()), service);

        var result = await useCase.ExecuteAsync("主地图");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.DuplicateName, result.FailureKind);
        Assert.Equal("同名地图已存在。", result.Diagnostic);
        _ = Assert.Single(service.Requests);
    }

    [Fact]
    public async Task ServiceFailure_ReturnsClearFailureResult()
    {
        var service = new FakeMapCreationService
        {
            NextResult = MapCreationResult.Failed(MapCreationFailureKind.CreationFailed, "持久化失败。"),
        };
        var useCase = new CreateMapUseCase(CreateReadySession(Guid.NewGuid()), service);

        var result = await useCase.ExecuteAsync("主地图");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.CreationFailed, result.FailureKind);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public async Task PortException_FoldedIntoFailureResult()
    {
        var service = new ThrowingMapCreationService();
        var useCase = new CreateMapUseCase(CreateReadySession(Guid.NewGuid()), service);

        var result = await useCase.ExecuteAsync("主地图");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.CreationFailed, result.FailureKind);
        Assert.Contains("boom", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionNotReady_FailsWithoutPortCall()
    {
        var service = new FakeMapCreationService();
        var session = CreateReadySession(Guid.NewGuid());
        await session.CloseAsync();
        var useCase = new CreateMapUseCase(session, service);

        var result = await useCase.ExecuteAsync("主地图");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.CreationFailed, result.FailureKind);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task MissingProjectIdentity_FailsWithoutPortCall()
    {
        var service = new FakeMapCreationService();
        var useCase = new CreateMapUseCase(CreateReadySession(null), service);

        var result = await useCase.ExecuteAsync("主地图");

        Assert.False(result.Success);
        Assert.Equal(MapCreationFailureKind.CreationFailed, result.FailureKind);
        Assert.Empty(service.Requests);
    }

    private sealed class FakeMapCreationService : IMapCreationService
    {
        public List<MapCreationRequest> Requests { get; } = new();

        public MapCreationResult? NextResult { get; init; }

        public Task<MapCreationResult> CreateAsync(MapCreationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NextResult
                ?? MapCreationResult.Ok(new MapSummary
                {
                    MapId = Guid.NewGuid(),
                    DisplayName = request.MapName,
                }));
        }
    }

    private sealed class ThrowingMapCreationService : IMapCreationService
    {
        public Task<MapCreationResult> CreateAsync(MapCreationRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
