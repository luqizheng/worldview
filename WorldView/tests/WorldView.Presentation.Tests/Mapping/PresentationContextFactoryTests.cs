using System;
using WorldView.Application.Abstractions;
using WorldView.Domain.Time;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Mapping;

/// <summary>
/// 类别 F（Presentation 侧）：ValidatedWorkspaceContext → PresentationContext 映射。
/// 权威链路侧（真实解析 → 会话 → 边界保留）由 Application.Tests/Infrastructure.Tests 验证；
/// 此处验证映射不丢失身份、不伪造 M1 语义态。
/// </summary>
public sealed class PresentationContextFactoryTests
{
    private static ValidatedWorkspaceContext Validated(
        Guid? mapId = null,
        Guid? branchId = null,
        Guid? rootHistoryId = null,
        HistoricalTime? historicalTime = null)
        => new()
        {
            MapId = mapId ?? TestContexts.MapA,
            BranchId = branchId,
            RootHistoryId = rootHistoryId ?? TestContexts.RootA,
            HistoricalTime = historicalTime ?? new HistoricalTime(1300, 43200),
        };

    [Fact]
    public void Mapping_PreservesMapBranchRootHistoryAndTypedTime()
    {
        var time = new HistoricalTime(1300, 43200);
        var context = PresentationContextFactory.FromValidatedWorkspace(
            Validated(branchId: TestContexts.BranchA, historicalTime: time), viewportGeneration: 2);

        Assert.Equal(TestContexts.MapA, context.MapId);
        Assert.Equal(TestContexts.BranchA, context.BranchId);
        Assert.Equal(TestContexts.RootA, context.RootHistoryId);
        Assert.Equal(time, context.HistoricalTime);   // typed 值按规范值比较
        Assert.Same(time, context.HistoricalTime);    // 不经字符串往返重建
        Assert.Equal("1300:43200", context.HistoricalTime.Canonical());
        Assert.Equal(2, context.ViewportGeneration);
    }

    [Fact]
    public void Mapping_RootBranchSemantics_Preserved()
    {
        var context = PresentationContextFactory.FromValidatedWorkspace(
            Validated(branchId: null, historicalTime: HistoricalTime.Epoch), viewportGeneration: 1);

        Assert.Null(context.BranchId);
        Assert.Equal(HistoricalTime.Epoch, context.HistoricalTime);
    }

    [Fact]
    public void Mapping_WorldStateRevision_IsNotEvaluated_NeverFabricated()
    {
        var context = PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 1);
        Assert.Same(WorldStateRevision.NotEvaluated.Instance, context.WorldStateRevision);
        // 无任何伪造 revision：不存在以时间戳/ID/哈希冒充的 Evaluated。
        Assert.IsNotType<WorldStateRevision.Evaluated>(context.WorldStateRevision);
    }

    [Fact]
    public void Mapping_Projection_IsNotConfigured_NeverGuessed()
    {
        var context = PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 1);
        Assert.Same(ProjectionDefinition.NotConfigured.Instance, context.Projection);
        // 未引入任何隐式 CRS/默认地理投影。
        Assert.IsNotType<ProjectionDefinition.Configured>(context.Projection);
    }

    [Fact]
    public void Mapping_StyleFontTokens_AreBuiltinBaselines()
    {
        var context = PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 1);
        Assert.Equal(PresentationTokens.BuiltinNeutralStyle, context.StyleToken);
        Assert.Equal(PresentationTokens.BuiltinFontBaseline, context.FontToken);
        Assert.Equal(PresentationAssemblerVersions.BlankScene, context.SceneAssemblerVersion);
    }

    [Fact]
    public void Mapping_NullValidatedContext_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PresentationContextFactory.FromValidatedWorkspace(null!, viewportGeneration: 1));
    }

    [Fact]
    public void Mapping_EmptyRootHistoryId_FailClosed()
    {
        // Application 边界丢失 RootHistoryId 属架构回归：工厂 fail-closed，不接受。
        Assert.Throws<ArgumentException>(() =>
            PresentationContextFactory.FromValidatedWorkspace(
                Validated(rootHistoryId: Guid.Empty), viewportGeneration: 1));
    }

    [Fact]
    public void Mapping_GenerationBelowOne_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 0));
    }

    [Fact]
    public void Mapping_SameInputs_SameFingerprint()
    {
        var a = PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 3);
        var b = PresentationContextFactory.FromValidatedWorkspace(Validated(), viewportGeneration: 3);
        Assert.Equal(a.Fingerprint, b.Fingerprint);
    }
}
