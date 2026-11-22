using System;
using System.Linq;
using WorldView.Domain.Assets;
using WorldView.Domain.Geometry;
using WorldView.Domain.Persistence;
using WorldView.Domain.Registry;
using Xunit;

namespace WorldView.Domain.Tests.Geometry;

/// <summary>
/// CD-02-D 领域规则测试：Asset hash/路径规则、Geometry Component manifest 校验
/// （descriptor 依据）、geometry asset envelope 结构校验（P2-09-25 有界实现）、
/// 操作策略（split/merge 显式拒绝）。
/// </summary>
public sealed class GeometryAndAssetRulesTests
{
    private const string ValidHash = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

    private static AssetRecord Asset(string hash = ValidHash) => new()
    {
        AssetId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        ContentHash = hash,
        HashAlgorithm = "sha-256",
        ByteLength = 10,
        MediaType = "application/json",
        FormatVersion = "1",
        RelativePath = $"Assets/sha256/{hash}",
        PublicationState = AssetPublicationState.Published,
        IntegrityState = AssetIntegrityState.Verified,
        CreatedAtUtcMs = 1,
    };

    private static GeometryComponentManifest Manifest(Guid? assetId = null, string role = "centerline", string family = "line_string") =>
        new()
        {
            GeometryComponentId = Guid.NewGuid(),
            GeometryRole = role,
            GeometryFamily = family,
            Dimension = 2,
            SpatialReferenceId = "proj-local-1",
            GeometrySchemaVersion = 1,
            AssetId = assetId ?? Guid.NewGuid(),
            ContentHash = ValidHash,
        };

    // ---- AssetRules ----

    [Fact]
    public void NormalizeContentHash_ValidLowercaseHex()
    {
        Assert.Equal(ValidHash, AssetRules.NormalizeContentHash(ValidHash));
        Assert.Equal(ValidHash, AssetRules.NormalizeContentHash("  " + ValidHash + " "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899")] // 大写
    [InlineData("aabb")] // 太短
    [InlineData("aabbccddeeff00112233445566778899aabbccddeeff0011223344556677889g")] // 非十六进制字符
    public void NormalizeContentHash_Invalid_Rejected(string hash)
    {
        Assert.Throws<ArgumentException>(() => AssetRules.NormalizeContentHash(hash));
    }

    [Fact]
    public void ContentAddressedRelativePath_FixedLayout()
    {
        Assert.Equal($"Assets/sha256/{ValidHash}", AssetRules.ContentAddressedRelativePath(ValidHash));
    }

    [Theory]
    [InlineData("/abs/path")]
    [InlineData("C:/x")]
    [InlineData("a/../b")]
    [InlineData("a//b")]
    [InlineData("a/ b/")]
    public void NormalizeRelativePath_RejectsTraversalAndAbsolute(string path)
    {
        Assert.Throws<ArgumentException>(() => AssetRules.NormalizeRelativePath(path));
    }

    [Fact]
    public void ValidateHashMatches_TamperedBytes_FailClosed()
    {
        var bytes = "payload"u8.ToArray();
        var hash = AssetRules.Sha256Hex(bytes);
        AssetRules.ValidateHashMatches(bytes, hash);
        Assert.Throws<CoreDomainDataException>(() => AssetRules.ValidateHashMatches("other"u8.ToArray(), hash));
    }

    // ---- GeometryRules.ValidateManifest ----

    [Fact]
    public void ValidateManifest_RiverCenterline_Pass()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        GeometryRules.ValidateManifest(descriptor, Manifest());
    }

    [Fact]
    public void ValidateManifest_EmptyComponentId_Rejected()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        var manifest = Manifest() with { GeometryComponentId = Guid.Empty };

        var ex = Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifest(descriptor, manifest));
        Assert.Contains("geometry_component_id", ex.Message);
    }

    [Fact]
    public void ValidateManifest_EmptyAssetId_Rejected()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        var manifest = Manifest() with { AssetId = Guid.Empty };

        var ex = Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifest(descriptor, manifest));
        Assert.Contains("asset_id", ex.Message);
    }

    [Fact]
    public void ValidateManifest_UndeclaredRole_Rejected()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        var ex = Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifest(descriptor, Manifest(role: "footprint")));
        Assert.Contains("footprint", ex.Message);
    }

    [Fact]
    public void ValidateManifest_FamilyNotInRole_Rejected()
    {
        // river.centerline 仅 line_string/multi_line_string；point 拒绝。
        var descriptor = ObjectTypeRegistry.Require("river");
        Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifest(descriptor, Manifest(family: "point")));
    }

    [Fact]
    public void ValidateManifest_Dimension3_Rejected()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        var manifest = Manifest() with { Dimension = 3 };
        Assert.Throws<CoreDomainConflictException>(() => GeometryRules.ValidateManifest(descriptor, manifest));
    }

    [Fact]
    public void ValidateManifest_BadHash_Rejected()
    {
        var descriptor = ObjectTypeRegistry.Require("river");
        var manifest = Manifest() with { ContentHash = "not-a-hash" };
        Assert.Throws<CoreDomainConflictException>(() => GeometryRules.ValidateManifest(descriptor, manifest));
    }

    [Fact]
    public void ValidateManifest_CaveSurveyStation_PointRole_Pass()
    {
        // Cave descriptor（P2-09-41 设计态）不在现行 registry——验证用 building 系声明不可用；
        // 改验证 river.reach + settlement.anchor 的已声明组合。
        var settlement = ObjectTypeRegistry.Require("settlement");
        GeometryRules.ValidateManifest(settlement, Manifest(role: "anchor", family: "point"));
    }

    // ---- ValidateManifestConsistency ----

    [Fact]
    public void ManifestConsistency_HashRoleMismatch_Rejected()
    {
        var asset = Asset();
        var manifest = Manifest(assetId: asset.AssetId) with { ContentHash = ValidHash };
        // hash 一致（asset hash == ValidHash == manifest hash）。
        GeometryRules.ValidateManifestConsistency(asset, null, manifest);

        // link role 不一致拒绝。
        var link = new ObjectDefinitionAssetLinkRecord
        {
            ObjectDefinitionAssetId = Guid.NewGuid(),
            ProjectId = asset.ProjectId,
            ObjectIdentityId = Guid.NewGuid(),
            AssetId = asset.AssetId,
            Role = "reach",
            CreatedAtUtcMs = 1,
        };
        Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifestConsistency(asset, link, manifest));

        // asset hash 与 manifest 不一致拒绝。
        var otherAsset = Asset() with { AssetId = asset.AssetId, ContentHash = ValidHash[..63] + "0" };
        Assert.Throws<CoreDomainConflictException>(
            () => GeometryRules.ValidateManifestConsistency(otherAsset, null, manifest));
    }

    // ---- GeometryOperationRules ----

    [Fact]
    public void OperationPolicy_IdentityPreservation()
    {
        Assert.True(GeometryOperationRules.PreservesComponentIdentity(GeometryOperationKind.InPlaceAdjust));
        Assert.True(GeometryOperationRules.PreservesComponentIdentity(GeometryOperationKind.Refinement));
        Assert.False(GeometryOperationRules.PreservesComponentIdentity(GeometryOperationKind.AddComponent));
        Assert.True(GeometryOperationRules.AllocatesNewComponentIdentity(GeometryOperationKind.AddComponent));
    }

    [Fact]
    public void SplitMerge_ExplicitlyRejected()
    {
        Assert.Throws<CoreDomainConflictException>(() => GeometryOperationRules.RejectSplitMerge());
    }

    // ---- GeometryAssetEnvelope ----

    private const string EnvelopeHead = """{"format_id":"worldview.geometry-json","format_version":1,"spatial_reference_id":"sr-1","axis_order":"x_y","unit":"meter","coordinate_precision_profile_id":"p1","dimension":2,"geometry_family":""";
    private const string EnvelopeTail = "}";

    [Fact]
    public void Envelope_Point_Valid()
    {
        GeometryAssetEnvelope.Validate(EnvelopeHead + "\"point\",\"body\":[10,20]" + EnvelopeTail, "point", 2);
    }

    [Fact]
    public void Envelope_LineString_Valid()
    {
        GeometryAssetEnvelope.Validate(
            EnvelopeHead + "\"line_string\",\"body\":[[0,0],[5,5],[10,0]]" + EnvelopeTail,
            "line_string", 2);
    }

    [Fact]
    public void Envelope_Polygon_ClosedRing_Valid()
    {
        GeometryAssetEnvelope.Validate(
            EnvelopeHead + "\"polygon\",\"body\":[[[0,0],[4,0],[4,4],[0,4],[0,0]]]" + EnvelopeTail,
            "polygon", 2);
    }

    [Fact]
    public void Envelope_Polygon_OpenRing_Rejected()
    {
        Assert.Throws<CoreDomainDataException>(
            () => GeometryAssetEnvelope.Validate(
                EnvelopeHead + "\"polygon\",\"body\":[[[0,0],[4,0],[4,4],[0,4]]]" + EnvelopeTail,
                "polygon", 2));
    }

    [Fact]
    public void Envelope_MultiPolygon_Valid()
    {
        GeometryAssetEnvelope.Validate(
            EnvelopeHead + "\"multi_polygon\",\"body\":[[[[0,0],[1,0],[1,1],[0,1],[0,0]]],[[[5,5],[6,5],[6,6],[5,6],[5,5]]]]" + EnvelopeTail,
            "multi_polygon", 2);
    }

    [Fact]
    public void Envelope_Curve_LineAndBezier_Valid()
    {
        GeometryAssetEnvelope.Validate(
            EnvelopeHead
            + "\"curve\",\"body\":{\"segments\":[{\"type\":\"line\",\"points\":[[0,0],[1,1]]},{\"type\":\"cubic_bezier\",\"control_points\":[[1,1],[2,2],[3,0],[4,0]]}]}"
            + EnvelopeTail,
            "curve", 2);
    }

    [Fact]
    public void Envelope_Curve_UnknownSegmentType_Rejected()
    {
        Assert.Throws<CoreDomainDataException>(
            () => GeometryAssetEnvelope.Validate(
                EnvelopeHead + "\"curve\",\"body\":{\"segments\":[{\"type\":\"quadratic\",\"control_points\":[[0,0],[1,1],[2,0]]}]}" + EnvelopeTail,
                "curve", 2));
    }

    [Fact]
    public void Envelope_FamilyMismatch_Rejected()
    {
        Assert.Throws<CoreDomainDataException>(
            () => GeometryAssetEnvelope.Validate(
                EnvelopeHead + "\"line_string\",\"body\":[[0,0],[1,1]]" + EnvelopeTail,
                "polygon", 2));
    }

    [Fact]
    public void Envelope_DimensionMismatch_Rejected()
    {
        Assert.Throws<CoreDomainDataException>(
            () => GeometryAssetEnvelope.Validate(
                EnvelopeHead + "\"point\",\"body\":[10,20]" + EnvelopeTail,
                "point", 3));
    }

    [Fact]
    public void Envelope_WrongFormatId_Rejected()
    {
        var envelope = """{"format_id":"other.format","format_version":1,"spatial_reference_id":"s","axis_order":"x_y","unit":"m","coordinate_precision_profile_id":"p","dimension":2,"geometry_family":"point","body":[0,0]}""";
        Assert.Throws<CoreDomainDataException>(() => GeometryAssetEnvelope.Validate(envelope, "point", 2));
    }

    [Fact]
    public void Envelope_NonFiniteCoordinate_LiteralRejected()
    {
        // JSON 规范拒绝 NaN/Infinity 字面量（fail-closed 解析）。
        Assert.ThrowsAny<Exception>(
            () => GeometryAssetEnvelope.Validate(
                EnvelopeHead + "\"point\",\"body\":[NaN,20]" + EnvelopeTail, "point", 2));
    }
}
