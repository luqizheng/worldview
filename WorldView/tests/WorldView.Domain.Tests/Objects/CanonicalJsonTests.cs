using System;
using System.Collections.Generic;
using WorldView.Domain.Objects;
using WorldView.Domain.Persistence;
using Xunit;

namespace WorldView.Domain.Tests.Objects;

/// <summary>
/// canonical JSON 规范化与指纹确定性（Handoff §6：同内容同指纹、顺序无关）。
/// canonical 形式为 Domain 独占契约（键序/数值表示固定，不含审计字段）。
/// </summary>
public sealed class CanonicalJsonTests
{
    [Fact]
    public void Canonicalize_KeyOrderAndWhitespaceIndependent()
    {
        var a = CanonicalJson.Canonicalize("""{"b": 1, "a": "x"}""");
        var b = CanonicalJson.Canonicalize("""{ "a" : "x" , "b" : 1 }""");
        Assert.Equal(a, b);
        Assert.Equal("""{"a":"x","b":1}""", a);
    }

    [Fact]
    public void Canonicalize_RecursiveObjectKeySort()
    {
        var a = CanonicalJson.Canonicalize("""{"outer": {"z": [2, 1], "a": true}, "m": null}""");
        var b = CanonicalJson.Canonicalize("""{"m": null, "outer": {"a": true, "z": [2, 1]}}""");
        Assert.Equal(a, b);
        Assert.Equal("""{"m":null,"outer":{"a":true,"z":[2,1]}}""", a);
    }

    [Fact]
    public void Canonicalize_NumberRepresentationFixed()
    {
        // 整数以 Int64 表示；非整数以 double 往返表示（文化无关）。
        Assert.Equal("""{"d":0.5,"i":5}""", CanonicalJson.Canonicalize("""{"d": 0.5, "i": 5}"""));
    }

    [Fact]
    public void Canonicalize_InvalidJson_FailClosed()
    {
        Assert.Throws<CoreDomainDataException>(() => CanonicalJson.Canonicalize("{not json"));
        // 空白输入是调用方契约违反（区别于数据损坏）。
        Assert.Throws<ArgumentException>(() => CanonicalJson.Canonicalize(""));
    }

    [Fact]
    public void Canonicalize_IsIdempotent()
    {
        var canonical = CanonicalJson.Canonicalize("""{"c": [1, {"b": 2, "a": 3}], "a": "s"}""");
        Assert.Equal(canonical, CanonicalJson.Canonicalize(canonical));
    }

    [Fact]
    public void Sha256Hex_StableLowercaseHex()
    {
        var h1 = CanonicalJson.Sha256Hex("{}");
        var h2 = CanonicalJson.Sha256Hex("{}");
        Assert.Equal(h1, h2);
        Assert.Equal(64, h1.Length);
        Assert.Equal(h1, h1.ToLowerInvariant());
    }
}

/// <summary>Initial State fingerprint 组成契约（CD-02-A §10）。</summary>
public sealed class InitialStateFingerprintTests
{
    private static InitialObjectRecord Member(Guid identityId, string payload, string? placement = null, long? placementVersion = null) =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            MapId = Guid.NewGuid(),
            InitialStateId = Guid.NewGuid(),
            ObjectIdentityId = identityId,
            MembershipPayloadJson = payload,
            PlacementPayloadJson = placement,
            PlacementSchemaVersion = placementVersion,
            CreatedAtUtcMs = 12345,
        };

    [Fact]
    public void SameContent_DifferentOrder_SameFingerprint()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var version = MapInitialStateService.StructuralBaselineSchemaVersion;
        var members1 = new List<InitialObjectRecord>
        {
            Member(id1, """{"role":"anchor"}"""),
            Member(id2, """{"role":"extent"}"""),
        };
        var members2 = new List<InitialObjectRecord>
        {
            Member(id2, """{ "role" : "extent" }"""),
            Member(id1, """{"role":"anchor"}"""),
        };

        var f1 = InitialStateFingerprint.Compute(version, members1);
        var f2 = InitialStateFingerprint.Compute(version, members2);
        Assert.Equal(f1, f2);
    }

    [Fact]
    public void DifferentContent_DifferentFingerprint()
    {
        var version = MapInitialStateService.StructuralBaselineSchemaVersion;
        var id = Guid.NewGuid();
        var members1 = new List<InitialObjectRecord> { Member(id, """{"role":"anchor"}""") };
        var members2 = new List<InitialObjectRecord> { Member(id, """{"role":"extent"}""") };
        Assert.NotEqual(
            InitialStateFingerprint.Compute(version, members1),
            InitialStateFingerprint.Compute(version, members2));
    }

    [Fact]
    public void PlacementDifferences_ChangeFingerprint()
    {
        var version = MapInitialStateService.StructuralBaselineSchemaVersion;
        var id = Guid.NewGuid();
        var withPlacement = new List<InitialObjectRecord>
        {
            Member(id, """{"a":1}""", """{"x":0,"y":0}""", 1),
        };
        var withoutPlacement = new List<InitialObjectRecord>
        {
            Member(id, """{"a":1}"""),
        };
        Assert.NotEqual(
            InitialStateFingerprint.Compute(version, withPlacement),
            InitialStateFingerprint.Compute(version, withoutPlacement));
    }

    [Fact]
    public void BaselineSchemaVersion_PartOfComposition()
    {
        var id = Guid.NewGuid();
        var members = new List<InitialObjectRecord> { Member(id, """{"a":1}""") };
        Assert.NotEqual(
            InitialStateFingerprint.Compute(1, members),
            InitialStateFingerprint.Compute(2, members));
    }

    [Fact]
    public void EmptyMembership_ProducesStableFingerprint()
    {
        var version = MapInitialStateService.StructuralBaselineSchemaVersion;
        Assert.Equal(
            InitialStateFingerprint.Compute(version, new List<InitialObjectRecord>()),
            InitialStateFingerprint.Compute(version, new List<InitialObjectRecord>()));
    }
}
