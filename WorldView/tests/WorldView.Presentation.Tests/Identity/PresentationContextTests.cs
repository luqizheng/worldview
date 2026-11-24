using System;
using WorldView.Domain.Time;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Identity;

/// <summary>类别 A：PresentationContext 合法构建、必需字段校验、身份保持、M1 固定语义。</summary>
public sealed class PresentationContextTests
{
    [Fact]
    public void LegalContext_Builds_AndPreservesIdentity()
    {
        var time = new HistoricalTime(1300, 43200);
        var context = new PresentationContext(
            TestContexts.MapA, TestContexts.BranchA, TestContexts.RootA, time,
            WorldStateRevision.NotEvaluated.Instance, ProjectionDefinition.NotConfigured.Instance,
            PresentationTokens.BuiltinNeutralStyle, PresentationTokens.BuiltinFontBaseline,
            viewportGeneration: 3, sceneAssemblerVersion: 1);

        Assert.Equal(TestContexts.MapA, context.MapId);
        Assert.Equal(TestContexts.BranchA, context.BranchId);
        Assert.Equal(TestContexts.RootA, context.RootHistoryId);
        // HistoricalTime 保持 typed 值（day/offset 全精度），非字符串降级。
        Assert.Equal(time, context.HistoricalTime);
        Assert.Equal(1300, context.HistoricalTime.DayNumber);
        Assert.Equal(43200, context.HistoricalTime.OffsetSeconds);
        Assert.Equal(3, context.ViewportGeneration);
        Assert.Equal(1, context.SceneAssemblerVersion);
        // 语义身份四元组一致（Map/Branch/RootHistory/时间）。
        Assert.Equal(new WorkspaceSemanticIdentity(TestContexts.MapA, TestContexts.BranchA, TestContexts.RootA, time), context.SemanticIdentity);
    }

    [Fact]
    public void NullBranch_MeansRootHistory_DefaultBranch()
    {
        var context = TestContexts.Create(branchId: null);
        Assert.Null(context.BranchId);
    }

    [Fact]
    public void M1FactoryDefaults_WorldStateNotEvaluated()
    {
        var context = TestContexts.Create();
        Assert.IsType<WorldStateRevision.NotEvaluated>(context.WorldStateRevision);
        Assert.Same(WorldStateRevision.NotEvaluated.Instance, context.WorldStateRevision);
    }

    [Fact]
    public void M1FactoryDefaults_ProjectionNotConfigured()
    {
        var context = TestContexts.Create();
        Assert.IsType<ProjectionDefinition.NotConfigured>(context.Projection);
        Assert.Same(ProjectionDefinition.NotConfigured.Instance, context.Projection);
    }

    [Fact]
    public void StyleAndFontTokens_AreBuiltinBaseline()
    {
        var context = TestContexts.Create();
        Assert.Equal("BuiltinNeutralStyle/v1", context.StyleToken);
        Assert.Equal("BuiltinFontBaseline/v1", context.FontToken);
        Assert.Equal(PresentationTokens.BuiltinNeutralStyle, context.StyleToken);
        Assert.Equal(PresentationTokens.BuiltinFontBaseline, context.FontToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ViewportGeneration_MustBePositive(long generation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TestContexts.Create(viewportGeneration: generation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SceneAssemblerVersion_MustBePositive(int version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TestContexts.Create(sceneAssemblerVersion: version));
    }

    [Fact]
    public void EmptyMapId_Rejected()
    {
        Assert.Throws<ArgumentException>(() => TestContexts.Create(mapId: Guid.Empty));
    }

    [Fact]
    public void EmptyRootHistoryId_Rejected()
    {
        Assert.Throws<ArgumentException>(() => TestContexts.Create(rootHistoryId: Guid.Empty));
    }

    [Fact]
    public void NullHistoricalTime_Rejected()
    {
        // 直接构造（绕过测试助手的纪元回退）：显式 null 必须 fail-closed。
        Assert.Throws<ArgumentNullException>(() => new PresentationContext(
            TestContexts.MapA, null, TestContexts.RootA, null!,
            WorldStateRevision.NotEvaluated.Instance, ProjectionDefinition.NotConfigured.Instance,
            PresentationTokens.BuiltinNeutralStyle, PresentationTokens.BuiltinFontBaseline, 1, 1));
    }

    [Fact]
    public void NullWorldStateRevision_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PresentationContext(
            TestContexts.MapA, null, TestContexts.RootA, HistoricalTime.Epoch,
            null!, ProjectionDefinition.NotConfigured.Instance,
            PresentationTokens.BuiltinNeutralStyle, PresentationTokens.BuiltinFontBaseline, 1, 1));
    }

    [Fact]
    public void NullProjection_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PresentationContext(
            TestContexts.MapA, null, TestContexts.RootA, HistoricalTime.Epoch,
            WorldStateRevision.NotEvaluated.Instance, null!,
            PresentationTokens.BuiltinNeutralStyle, PresentationTokens.BuiltinFontBaseline, 1, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankStyleToken_Rejected(string token)
    {
        Assert.Throws<ArgumentException>(() => TestContexts.Create(styleToken: token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankFontToken_Rejected(string token)
    {
        Assert.Throws<ArgumentException>(() => TestContexts.Create(fontToken: token));
    }

    [Fact]
    public void ValueEquality_SameFieldsEqual_DifferentFieldNotEqual()
    {
        var a = TestContexts.Create();
        var b = TestContexts.Create();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var differentGen = TestContexts.Create(viewportGeneration: 2);
        Assert.NotEqual(a, differentGen);

        var differentTime = TestContexts.Create(historicalTime: new HistoricalTime(1200, 43201));
        Assert.NotEqual(a, differentTime);
    }

    [Fact]
    public void EvaluatedIdentity_EqualityDistinguishes()
    {
        var evaluated1 = TestContexts.Create(worldStateRevision: new WorldStateRevision.Evaluated("revision-1"));
        var evaluated1Again = TestContexts.Create(worldStateRevision: new WorldStateRevision.Evaluated("revision-1"));
        var evaluated2 = TestContexts.Create(worldStateRevision: new WorldStateRevision.Evaluated("revision-2"));

        Assert.Equal(evaluated1, evaluated1Again);
        Assert.NotEqual(evaluated1, evaluated2);
        Assert.NotEqual(evaluated1, TestContexts.Create());
    }

    [Fact]
    public void Evaluated_BlankIdentityRejected()
    {
        Assert.Throws<ArgumentException>(() => new WorldStateRevision.Evaluated("  "));
    }

    [Fact]
    public void ConfiguredProjection_BlankPartsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ProjectionDefinition.Configured("", "1"));
        Assert.Throws<ArgumentException>(() => new ProjectionDefinition.Configured("epsg:4326", " "));
    }

    [Fact]
    public void ConfiguredProjection_Builds()
    {
        var projection = new ProjectionDefinition.Configured("epsg:4326", "v2");
        var context = TestContexts.Create(projection: projection);
        Assert.Equal(projection, context.Projection);
    }
}
