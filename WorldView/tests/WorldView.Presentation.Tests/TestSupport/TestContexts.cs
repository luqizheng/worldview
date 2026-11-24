using System;
using WorldView.Presentation.Identity;
using WorldView.Domain.Time;

namespace WorldView.Presentation.Tests.TestSupport;

/// <summary>测试用确定性上下文构建器（全部身份字段可独立变化）。</summary>
internal static class TestContexts
{
    public static readonly Guid MapA = new("00010203-0405-0607-0809-0A0B0C0D0E0F");
    public static readonly Guid MapB = new("AAAA0203-0405-0607-0809-0A0B0C0D0E0F");
    public static readonly Guid BranchA = new("11121314-1516-1718-191A-1B1C1D1E1F20");
    public static readonly Guid BranchB = new("11121314-1516-1718-191A-1B1C1D1E1FFF");
    public static readonly Guid RootA = new("21222324-2526-2728-292A-2B2C2D2E2F30");
    public static readonly Guid RootB = new("21222324-2526-2728-292A-2B2C2D2E2F99");

    /// <summary>标准 M1 测试上下文（全部字段显式）。</summary>
    public static PresentationContext Create(
        Guid? mapId = null,
        Guid? branchId = null,
        Guid? rootHistoryId = null,
        HistoricalTime? historicalTime = null,
        WorldStateRevision? worldStateRevision = null,
        ProjectionDefinition? projection = null,
        string? styleToken = null,
        string? fontToken = null,
        long viewportGeneration = 1,
        int sceneAssemblerVersion = PresentationAssemblerVersions.BlankScene)
        => new(
            mapId ?? MapA,
            branchId,
            rootHistoryId ?? RootA,
            historicalTime ?? new HistoricalTime(1200, 43200),
            worldStateRevision ?? WorldStateRevision.NotEvaluated.Instance,
            projection ?? ProjectionDefinition.NotConfigured.Instance,
            styleToken ?? PresentationTokens.BuiltinNeutralStyle,
            fontToken ?? PresentationTokens.BuiltinFontBaseline,
            viewportGeneration,
            sceneAssemblerVersion);
}
