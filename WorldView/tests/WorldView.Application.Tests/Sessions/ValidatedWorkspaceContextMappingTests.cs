using System;
using System.IO;
using System.Threading.Tasks;
using WorldView.Application.Abstractions;
using WorldView.Application.Packaging;
using WorldView.Application.Sessions;
using WorldView.Application.UseCases;
using WorldView.Domain.Time;
using WorldView.Application.Tests.UseCases;
using Xunit;

namespace WorldView.Application.Tests.Sessions;

/// <summary>
/// WP-30A（PRA-01 M1 Profile §4.1）：Application 校验上下文映射的聚焦测试——
/// 权威解析链路给出的 RootHistoryId 与 typed HistoricalTime 必须在
/// <see cref="ValidatedWorkspaceContext"/> 及会话挂载的工作区上下文中完整保留
/// （不降级为字符串、不丢弃身份字段）。解析规则本身归 Core Domain（Infrastructure/Domain 测试），
/// 此处验证 Application 边界不再丢失信息。
/// </summary>
public sealed class ValidatedWorkspaceContextMappingTests
{
    private static ProjectSession CreateReadySession()
        => new(
            Guid.NewGuid(),
            ProjectPackageStructure.NormalizePackagePath(
                Path.Combine(Path.GetTempPath(), $"wv-mapping-{Guid.NewGuid():N}.wvproject")),
            PackageManifest.CreateV1(DateTimeOffset.UtcNow),
            new FakePackageDatabase("mapping.sqlite"),
            new FakeAssetStore(),
            Guid.NewGuid());

    [Fact]
    public async Task OpenWorkspace_PreservesRootHistoryIdAndTypedHistoricalTime()
    {
        await using var session = CreateReadySession();
        session.TransitionTo(ProjectSessionState.Ready);
        var mapId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var resolver = FakeResolver.Success();

        var workspace = await session.OpenWorkspaceAsync(
            mapId, branchId, "1300:43200", FakeMapQuery.WithAnyMap(), resolver);

        Assert.Equal(mapId, workspace.Context.MapId);
        Assert.Equal(branchId, workspace.Context.BranchId);
        Assert.Equal(FakeResolver.DefaultRootHistoryId, workspace.Context.RootHistoryId);
        // typed 时间完整保留（day + offset 全精度），而非字符串降级。
        Assert.Equal(new HistoricalTime(1300, 43200), workspace.Context.HistoricalTime);
        Assert.Equal("1300:43200", workspace.Context.CanonicalHistoricalTime);
    }

    [Fact]
    public async Task OpenWorkspace_NullTimeInput_ResolvesToTypedEpoch()
    {
        await using var session = CreateReadySession();
        session.TransitionTo(ProjectSessionState.Ready);

        var workspace = await session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        Assert.Equal(HistoricalTime.Epoch, workspace.Context.HistoricalTime);
        Assert.Equal("0:0", workspace.Context.CanonicalHistoricalTime);
    }

    [Fact]
    public async Task SwitchContext_ReplacesRootHistoryIdAndTypedTime()
    {
        await using var session = CreateReadySession();
        session.TransitionTo(ProjectSessionState.Ready);
        var workspace = await session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, null, FakeMapQuery.WithAnyMap(), FakeResolver.Success());
        Assert.Equal(HistoricalTime.Epoch, workspace.Context.HistoricalTime);

        var otherRoot = Guid.NewGuid();
        await workspace.SwitchContextAsync(null, "1200:43200", FakeResolver.Success(otherRoot));

        Assert.Equal(otherRoot, workspace.Context.RootHistoryId);
        Assert.Equal(new HistoricalTime(1200, 43200), workspace.Context.HistoricalTime);
    }

    [Fact]
    public async Task CanonicalTextIsDerivedFromTypedTime_NeverSecondSource()
    {
        await using var session = CreateReadySession();
        session.TransitionTo(ProjectSessionState.Ready);

        // Domain 允许前导零输入；canonical 文本由 typed 值计算，不带前导零。
        var workspace = await session.OpenWorkspaceAsync(
            Guid.NewGuid(), null, "0007:000120", FakeMapQuery.WithAnyMap(), FakeResolver.Success());

        Assert.Equal(new HistoricalTime(7, 120), workspace.Context.HistoricalTime);
        Assert.Equal("7:120", workspace.Context.CanonicalHistoricalTime);
        Assert.Equal(workspace.Context.HistoricalTime.Canonical(), workspace.Context.CanonicalHistoricalTime);
    }
}
