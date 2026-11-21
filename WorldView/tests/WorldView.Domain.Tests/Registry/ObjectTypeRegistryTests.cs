using System;
using System.Collections.Generic;
using System.Linq;
using WorldView.Domain.Objects;
using WorldView.Domain.Registry;
using Xunit;

namespace WorldView.Domain.Tests.Registry;

/// <summary>
/// CD-02-C Registry runtime 验证（P2-09-26 V0.2 descriptor 为数据源，不发明字段语义）：
/// 加载结构校验、pin 校验、typed definition payload 校验（含十类型程序化正向向量）、
/// geometry role 校验、种子目录与 registry 一致性。
/// </summary>
public sealed class ObjectTypeRegistryTests
{
    [Fact]
    public void Registry_LoadsTenDescriptors_StructurallyValid()
    {
        var all = ObjectTypeRegistry.All;
        Assert.Equal(10, all.Count);
        Assert.Equal(all.Select(d => d.TypeKey).Distinct(StringComparer.Ordinal).Count(), all.Count);

        foreach (var descriptor in all)
        {
            Assert.Equal(2, descriptor.SchemaVersion);
            Assert.Equal($"world-view.object-type.{descriptor.TypeKey}.v1", descriptor.SemanticsId);
            Assert.True(descriptor.Allocatable, descriptor.TypeKey);
            Assert.NotEmpty(descriptor.DefinitionFields);
            Assert.NotEmpty(descriptor.GeometryRoles);
            Assert.Contains("map_membership", descriptor.InitialStateIncludes);
            Assert.True(
                descriptor.FindDefinitionField("canonical_name")!.Kind
                    is DefinitionFieldKind.RequiredString or DefinitionFieldKind.OptionalString,
                descriptor.TypeKey);

            // Building/Cave 不在现行目录（P2-09-41 未完成 typed_v2；不得擅自加入）。
            Assert.NotEqual("building", descriptor.TypeKey);
            Assert.NotEqual("cave_system", descriptor.TypeKey);
        }

        Assert.Equal(
            new[] { "landmass", "mountain_range", "mountain_peak", "river", "lake", "terrain_region", "settlement", "road", "political_region", "point_of_interest" }
                .OrderBy(k => k, StringComparer.Ordinal),
            all.Select(d => d.TypeKey).OrderBy(k => k, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ValidatePin_AllTenDescriptors_Pass()
    {
        foreach (var descriptor in ObjectTypeRegistry.All)
        {
            ObjectTypeRegistry.ValidatePin(descriptor.TypeKey, descriptor.SchemaVersion, descriptor.SemanticsId);
        }
    }

    [Theory]
    [InlineData(3, "world-view.object-type.river.v1")]
    [InlineData(2, "world-view.object-type.river.v2")]
    public void ValidatePin_Mismatch_FailClosed(long version, string semanticsId)
    {
        var ex = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidatePin("river", version, semanticsId));
        Assert.Equal("pin_mismatch", ex.ErrorCode);
    }

    [Fact]
    public void Require_UnknownTypeKey_FailClosedWithStableCode()
    {
        foreach (var key in new[] { "building", "cave_system", "", "RIVER" })
        {
            var ex = Assert.Throws<RegistryException>(() => ObjectTypeRegistry.Require(key));
            Assert.Equal("unknown_type_key", ex.ErrorCode);
        }
    }

    /// <summary>十类型程序化正向向量：按 descriptor 声明构造最小合法 definition payload。</summary>
    [Fact]
    public void ValidateDefinitionPayload_AllTenTypes_MinimalValidPayloadsPass()
    {
        foreach (var descriptor in ObjectTypeRegistry.All)
        {
            var payload = BuildMinimalValidPayload(descriptor);
            ObjectTypeRegistry.ValidateDefinitionPayload(descriptor.TypeKey, payload);
        }
    }

    [Fact]
    public void ValidateDefinitionPayload_River_Vectors()
    {
        // 合法。
        ObjectTypeRegistry.ValidateDefinitionPayload("river", """{"canonical_name":"Danube","river_class":"perennial"}""");
        // 可选 canonical_name 不存在于任何类型？river 的 required——缺省被拒。
        var missing = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("river", """{"river_class":"other"}"""));
        Assert.Equal("definition_invalid", missing.ErrorCode);

        // 枚举越界。
        var badEnum = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("river", """{"canonical_name":"X","river_class":"unbounded"}"""));
        Assert.Equal("definition_invalid", badEnum.ErrorCode);

        // 未知字段（common_rules.unknown_fields=reject）。
        var unknown = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("river", """{"canonical_name":"X","river_class":"other","flow":1}"""));
        Assert.Equal("unknown_definition_field", unknown.ErrorCode);

        // 字符串字段给了数值。
        Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("river", """{"canonical_name":42,"river_class":"other"}"""));
    }

    [Fact]
    public void ValidateDefinitionPayload_MountainPeak_NumericAndReferenceVectors()
    {
        // 合法（含 non_negative_decimal + required stable reference）。
        ObjectTypeRegistry.ValidateDefinitionPayload(
            "mountain_peak",
            """{"canonical_name":"Peak","elevation_value":8848.5,"elevation_unit":"m","vertical_reference_id":"ref-datum-1"}""");

        // 负数高程拒绝。
        var negative = Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload(
                "mountain_peak",
                """{"canonical_name":"Peak","elevation_value":-1,"elevation_unit":"m","vertical_reference_id":"ref"}"""));
        Assert.Equal("definition_invalid", negative.ErrorCode);

        // elevation_unit 枚举越界（"m"/"ft" 之外）。
        Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload(
                "mountain_peak",
                """{"canonical_name":"Peak","elevation_value":1,"elevation_unit":"km","vertical_reference_id":"ref"}"""));

        // 缺少 vertical_reference_id。
        Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload(
                "mountain_peak",
                """{"canonical_name":"Peak","elevation_value":1,"elevation_unit":"ft"}"""));
    }

    [Fact]
    public void ValidateDefinitionPayload_TerrainRegion_OptionalFields()
    {
        // terrain_region：canonical_name 可选 + source_dataset_id 可选 Asset 引用。
        ObjectTypeRegistry.ValidateDefinitionPayload("terrain_region", """{"terrain_class":"plain"}""");
        ObjectTypeRegistry.ValidateDefinitionPayload(
            "terrain_region",
            """{"canonical_name":"Lowlands","terrain_class":"wetland","source_dataset_id":"asset-elevation-1"}""");

        // terrain_class 缺失（required enum）。
        Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("terrain_region", """{"canonical_name":"X"}"""));
    }

    [Fact]
    public void ValidateDefinitionPayload_Road_OptionalCanonicalName()
    {
        // road：canonical_name optional（P2-09-24/registry 一致）→ 仅 road_class 合法。
        ObjectTypeRegistry.ValidateDefinitionPayload("road", """{"road_class":"trail"}""");
        ObjectTypeRegistry.ValidateDefinitionPayload("road", """{"canonical_name":"Old Road","road_class":"highway"}""");
        // 非法枚举。
        Assert.Throws<RegistryException>(
            () => ObjectTypeRegistry.ValidateDefinitionPayload("road", """{"road_class":"express_lane"}"""));
    }

    [Fact]
    public void ValidateDefinitionPayload_NonObjectRoot_Rejected()
    {
        Assert.Throws<RegistryException>(() => ObjectTypeRegistry.ValidateDefinitionPayload("river", "[1,2]"));
        Assert.Throws<RegistryException>(() => ObjectTypeRegistry.ValidateDefinitionPayload("river", "null"));
    }

    [Fact]
    public void ValidateGeometryRole_DeclaredRolesOnly()
    {
        ObjectTypeRegistry.ValidateGeometryRole("river", "centerline");
        ObjectTypeRegistry.ValidateGeometryRole("river", "reach");
        ObjectTypeRegistry.ValidateGeometryRole("settlement", "anchor");

        var unknown = Assert.Throws<RegistryException>(() => ObjectTypeRegistry.ValidateGeometryRole("river", "extent"));
        Assert.Equal("unknown_geometry_role", unknown.ErrorCode);

        // mountain_range combined_min 是合计下限，不是 role。
        Assert.Throws<RegistryException>(() => ObjectTypeRegistry.ValidateGeometryRole("mountain_range", "combined_min"));
        // 声明本身：mountain_range extent 0..N + ridge 0..N + combined_min=1。
        var range = ObjectTypeRegistry.Require("mountain_range");
        Assert.Null(range.FindGeometryRole("extent")!.Max);
        Assert.Equal(1, range.GeometryCombinedMin);
    }

    [Fact]
    public void BuiltinSeedCatalog_ConsistentWithRegistry()
    {
        // 种子目录（播种数据源）与 registry（语义权威）十 key 完全一致。
        Assert.Equal(
            ObjectTypeRegistry.All.Select(d => d.TypeKey).OrderBy(k => k, StringComparer.Ordinal),
            BuiltinObjectTypes.TypeKeys.OrderBy(k => k, StringComparer.Ordinal));

        foreach (var key in BuiltinObjectTypes.TypeKeys)
        {
            var descriptor = ObjectTypeRegistry.Require(key);
            Assert.Equal(descriptor.SemanticsId, BuiltinObjectTypes.SemanticsIdFor(key));
            Assert.Equal(descriptor.SchemaVersion, BuiltinObjectTypes.DescriptorSchemaVersionFor(key));
        }
    }

    /// <summary>按 descriptor 声明构造最小合法 definition payload（校验声明可满足，不发明值）。</summary>
    private static string BuildMinimalValidPayload(ObjectTypeDescriptor descriptor)
    {
        var fields = new List<string>();
        foreach (var field in descriptor.DefinitionFields)
        {
            switch (field.Kind)
            {
                case DefinitionFieldKind.RequiredString:
                    fields.Add($"\"{field.Field}\":\"demo-{descriptor.TypeKey}\"");
                    break;
                case DefinitionFieldKind.OptionalString:
                case DefinitionFieldKind.OptionalAssetReference:
                    break;
                case DefinitionFieldKind.Enum:
                    fields.Add($"\"{field.Field}\":\"{field.AllowedValues![0]}\"");
                    break;
                case DefinitionFieldKind.NonNegativeDecimal:
                    fields.Add($"\"{field.Field}\":0");
                    break;
                case DefinitionFieldKind.RequiredStableReference:
                    fields.Add($"\"{field.Field}\":\"ref-demo\"");
                    break;
                default:
                    throw new InvalidOperationException($"未覆盖的声明类别 {field.Kind}。");
            }
        }

        return "{" + string.Join(",", fields) + "}";
    }
}
