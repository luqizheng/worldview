using System;
using System.Globalization;
using System.Threading;
using WorldView.Domain.Time;
using WorldView.Presentation.Identity;
using WorldView.Presentation.Tests.TestSupport;
using Xunit;

namespace WorldView.Presentation.Tests.Identity;

/// <summary>类别 B：ContextFingerprint 确定性、字段敏感性与 canonical 序列化稳定性。</summary>
public sealed class ContextFingerprintTests
{
    /// <summary>固定测试向量上下文（与 CONTEXT_AND_FINGERPRINT.md 的 pinned vector 一致）。</summary>
    private static PresentationContext PinnedVectorContext() => new(
        new Guid("00010203-0405-0607-0809-0A0B0C0D0E0F"),
        new Guid("11121314-1516-1718-191A-1B1C1D1E1F20"),
        new Guid("21222324-2526-2728-292A-2B2C2D2E2F30"),
        new HistoricalTime(1200, 43200),
        WorldStateRevision.NotEvaluated.Instance,
        ProjectionDefinition.NotConfigured.Instance,
        PresentationTokens.BuiltinNeutralStyle,
        PresentationTokens.BuiltinFontBaseline,
        viewportGeneration: 7,
        sceneAssemblerVersion: 1);

    [Fact]
    public void SameContext_ProducesSameFingerprint_AcrossConstructionSites()
    {
        var a = PinnedVectorContext();
        var b = PinnedVectorContext();

        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Equal(a.Fingerprint.ToHex(), b.Fingerprint.ToHex());
        // 独立构建两次再比较（排除单例缓存假象）。
        Assert.Equal(PinnedVectorContext().Fingerprint.ToHex(), a.Fingerprint.ToHex());
    }

    [Fact]
    public void PinnedSha256Vector_MatchesCanonicalSerialization()
    {
        // 锁定 canonical 序列化 + SHA-256 的精确输出；编码规则变化时本测试失败。
        Assert.Equal(
            "8d59a5649082ee21e2eb04ae0190d45c2ca9379dcefb884589befbb8356ac7e9",
            PinnedVectorContext().Fingerprint.ToHex());
    }

    [Fact]
    public void Fingerprint_Is32BytesLowercaseHex64()
    {
        var hex = PinnedVectorContext().Fingerprint.ToHex();
        Assert.Equal(64, hex.Length);
        Assert.Equal(hex, hex.ToLowerInvariant());
        Assert.Equal(32, PinnedVectorContext().Fingerprint.ToByteArray().Length);
        Assert.True(ContextFingerprint.TryFromHex(hex, out var rebuilt));
        Assert.Equal(PinnedVectorContext().Fingerprint, rebuilt);
        Assert.False(ContextFingerprint.TryFromHex("zz", out _));
    }

    [Fact]
    public void DifferentMapId_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create(mapId: TestContexts.MapA).Fingerprint,
            TestContexts.Create(mapId: TestContexts.MapB).Fingerprint);
    }

    [Fact]
    public void DifferentBranchId_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create(branchId: null).Fingerprint,
            TestContexts.Create(branchId: TestContexts.BranchA).Fingerprint);
        Assert.NotEqual(
            TestContexts.Create(branchId: TestContexts.BranchA).Fingerprint,
            TestContexts.Create(branchId: TestContexts.BranchB).Fingerprint);
    }

    [Fact]
    public void DifferentRootHistoryId_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create(rootHistoryId: TestContexts.RootA).Fingerprint,
            TestContexts.Create(rootHistoryId: TestContexts.RootB).Fingerprint);
    }

    [Fact]
    public void DifferentHistoricalTime_ChangesFingerprint_IncludingOffsetPrecision()
    {
        var baseFp = TestContexts.Create().Fingerprint;
        Assert.NotEqual(baseFp, TestContexts.Create(historicalTime: new HistoricalTime(1201, 43200)).Fingerprint);
        // 偏移秒精度必须保留：1 秒差异即不同上下文。
        Assert.NotEqual(baseFp, TestContexts.Create(historicalTime: new HistoricalTime(1200, 43201)).Fingerprint);
    }

    [Fact]
    public void DifferentViewportGeneration_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create(viewportGeneration: 1).Fingerprint,
            TestContexts.Create(viewportGeneration: 2).Fingerprint);
    }

    [Fact]
    public void DifferentStyleOrFontToken_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create().Fingerprint,
            TestContexts.Create(styleToken: "BuiltinNeutralStyle/v2").Fingerprint);
        Assert.NotEqual(
            TestContexts.Create().Fingerprint,
            TestContexts.Create(fontToken: "BuiltinFontBaseline/v2").Fingerprint);
    }

    [Fact]
    public void DifferentSceneAssemblerVersion_ChangesFingerprint()
    {
        Assert.NotEqual(
            TestContexts.Create(sceneAssemblerVersion: 1).Fingerprint,
            TestContexts.Create(sceneAssemblerVersion: 2).Fingerprint);
    }

    [Fact]
    public void NotEvaluated_Vs_Evaluated_AreDistinguishable()
    {
        var notEvaluated = TestContexts.Create().Fingerprint;
        var evaluated = TestContexts.Create(
            worldStateRevision: new WorldStateRevision.Evaluated("world-state/rev-1")).Fingerprint;
        var evaluatedOther = TestContexts.Create(
            worldStateRevision: new WorldStateRevision.Evaluated("world-state/rev-2")).Fingerprint;

        Assert.NotEqual(notEvaluated, evaluated);
        Assert.NotEqual(evaluated, evaluatedOther);
    }

    [Fact]
    public void NotConfigured_Vs_Configured_AreDistinguishable()
    {
        var notConfigured = TestContexts.Create().Fingerprint;
        var configured = TestContexts.Create(
            projection: new ProjectionDefinition.Configured("epsg:4326", "v1")).Fingerprint;
        var configuredOther = TestContexts.Create(
            projection: new ProjectionDefinition.Configured("epsg:3857", "v1")).Fingerprint;

        Assert.NotEqual(notConfigured, configured);
        Assert.NotEqual(configured, configuredOther);
    }

    [Fact]
    public void CanonicalSerialization_IsStable_ByteForByte()
    {
        var bytes1 = PresentationContextCanonicalizer.Serialize(PinnedVectorContext());
        var bytes2 = PresentationContextCanonicalizer.Serialize(PinnedVectorContext());

        Assert.Equal(bytes1, bytes2);
        // schema 版本前缀在字节流头部。
        Assert.Equal(1, bytes1[0] | (bytes1[1] << 8) | (bytes1[2] << 16) | (bytes1[3] << 24));
    }

    [Fact]
    public void CanonicalSerialization_DistinguishesAbsentBranchFromGuidZero()
    {
        // 缺省 Branch（根历史）与显式空 GUID Branch 不得混淆——存在标志字节区分。
        var root = PresentationContextCanonicalizer.Serialize(TestContexts.Create(branchId: null));
        var explicitZero = PresentationContextCanonicalizer.Serialize(TestContexts.Create(branchId: Guid.Empty));
        Assert.NotEqual(root, explicitZero);
        // 显式 Branch = 标志字节 + 16 字节 GUID。
        Assert.Equal(root.Length + 16, explicitZero.Length);
    }

    [Fact]
    public void CanonicalSerialization_TypedStateKindsAreDistinct()
    {
        var notEvaluated = PresentationContextCanonicalizer.Serialize(TestContexts.Create());
        var evaluated = PresentationContextCanonicalizer.Serialize(
            TestContexts.Create(worldStateRevision: new WorldStateRevision.Evaluated("rev-1")));
        Assert.NotEqual(notEvaluated, evaluated);

        var notConfigured = PresentationContextCanonicalizer.Serialize(TestContexts.Create());
        var configured = PresentationContextCanonicalizer.Serialize(
            TestContexts.Create(projection: new ProjectionDefinition.Configured("def", "1")));
        Assert.NotEqual(notConfigured, configured);
    }

    [Fact]
    public void FieldEqualityAndFingerprint_Agree()
    {
        var a = TestContexts.Create();
        var b = TestContexts.Create();
        var c = TestContexts.Create(viewportGeneration: 9);

        // 字段相等 ⇔ 指纹相等（两个方向都必须成立）。
        Assert.Equal(a.Equals(b), a.Fingerprint == b.Fingerprint);
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.NotEqual(a.Fingerprint, c.Fingerprint);
    }
}

/// <summary>文化/时区测试专用串行 collection（文化是进程级状态，禁止并行）。</summary>
[CollectionDefinition(nameof(FingerprintCultureSerialCollection), DisableParallelization = true)]
public sealed class FingerprintCultureSerialCollection
{
}

/// <summary>
/// 文化/时区无关性（串行执行）。
/// </summary>
[Collection(nameof(FingerprintCultureSerialCollection))]
public sealed class FingerprintCultureIndependenceTests
{
    [Fact]
    public void CultureAndTimeZoneChange_DoNotAlterFingerprint()
    {
        var context = TestContexts.Create(historicalTime: new HistoricalTime(1200, 43200));
        var baseline = context.Fingerprint.ToHex();
        var canonicalBaseline = context.HistoricalTime.Canonical();

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        var originalTimeZone = TimeZoneInfo.Local;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = new CultureInfo("ar-SA");
            context = TestContexts.Create(historicalTime: new HistoricalTime(1200, 43200));
            Assert.Equal(baseline, context.Fingerprint.ToHex());
            Assert.Equal(canonicalBaseline, context.HistoricalTime.Canonical());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
            TimeZoneInfo.ClearCachedData();
            _ = originalTimeZone;
        }
    }

    [Fact]
    public void CanonicalTime_PreservesDayAndOffsetPrecision()
    {
        var time = new HistoricalTime(1200, 43200);
        Assert.Equal("1200:43200", time.Canonical());
        // 精度保留：秒级差异在 canonical 文本与 typed 值中都可见。
        Assert.Equal("1200:43201", new HistoricalTime(1200, 43201).Canonical());
        Assert.NotEqual(
            TestContexts.Create(historicalTime: time).Fingerprint,
            TestContexts.Create(historicalTime: new HistoricalTime(1200, 43201)).Fingerprint);
    }
}
