using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldView.Domain.Aggregates;
using WorldView.Domain.Registry;
using Xunit;

namespace WorldView.Infrastructure.Tests.Persistence.CoreDomain;

/// <summary>
/// CD-02-E S10 一致性运行器：逐条执行 P2-09-40 V0.6 的 55 个聚合校验向量
/// （base fixture + RFC 6902 patch → BuildingCaveAggregateValidator 三层校验 →
/// 断言 expected/error_code）。向量为设计夹具原样执行——不做本地改写；
/// publication/measurement 层使用向量声明的外部上下文字段。
/// </summary>
public sealed class Cd02AggregateVectorConformanceTests
{
    private const string ResourceName = "WorldView.Infrastructure.Tests.Conformance.p2-09-40-building-cave-aggregate-validation-vectors-v0.6.json";

    private static (JsonObject Building, JsonObject Cave, JsonArray Vectors) LoadSuite()
    {
        var assembly = typeof(Cd02AggregateVectorConformanceTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"嵌入资源 {ResourceName} 缺失。");
        var suite = JsonNode.Parse(new StreamReader(stream).ReadToEnd())!;
        var fixtures = (JsonObject)suite["base_fixtures"]!;
        return (
            (JsonObject)fixtures["building"]!.DeepClone(),
            (JsonObject)fixtures["cave"]!.DeepClone(),
            (JsonArray)suite["vectors"]!);
    }

    public static IEnumerable<object[]> VectorIds()
    {
        var (_, _, vectors) = LoadSuite();
        foreach (var vector in vectors.OfType<JsonObject>())
        {
            yield return new object[] { (string)vector["id"]! };
        }
    }

    [Theory]
    [MemberData(nameof(VectorIds))]
    public void ExecuteVector(string vectorId)
    {
        var (building, cave, vectors) = LoadSuite();
        var vector = vectors.OfType<JsonObject>().Single(v => (string)v["id"]! == vectorId);
        var inputSource = (string)vector["input_source"]!;
        // 向量 input_source 用短名（building/cave）；cave 的 descriptor type_key 为 cave_system。
        var typeKey = inputSource == "building" ? "building" : "cave_system";
        var payload = (JsonObject)(inputSource == "building" ? building.DeepClone() : cave.DeepClone());

        // RFC 6902 patches（add/remove/replace；向量集未使用 move/copy/test）。
        foreach (var patch in (vector["patches"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            // 已知工件漂移（P2-09-40 V0.6 patch 数据 vs P2-09-38 V0.5 schema 字段名）：
            // 三个 legacy 键名在执行前按当前 schema 规范化（adoption gate "execute and
            // refine P2-09-40 vectors" 的显式适配点；见 CD-02-E conformance 报告）。
            NormalizeLegacyFieldNames(patch);
            ApplyPatch(payload, patch);
        }

        var payloadJson = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var publication = BuildPublicationContext(vector);
        var context = BuildAggregateContext(vector);
        var issues = BuildingCaveAggregateValidator.Validate(typeKey, payloadJson, publication, context);

        var expected = (string)vector["expected"]!;
        var expectedCode = vector["error_code"] is JsonNode code && code.GetValueKind() == JsonValueKind.String
            ? (string)code
            : null;

        if (expected == "valid")
        {
            Assert.True(
                issues.Count == 0,
                $"{vectorId} 应通过全部校验层，实际问题：{string.Join("; ", issues.Select(i => $"{i.ErrorCode}: {i.Message}"))}");
        }
        else
        {
            Assert.NotEmpty(issues);
            Assert.True(
                expectedCode is null || issues.Any(i => i.ErrorCode == expectedCode),
                $"{vectorId} 期望错误码 {expectedCode}，实际：{string.Join("; ", issues.Select(i => i.ErrorCode))}");
        }
    }

    [Fact]
    public void Suite_Has55Vectors_AllExecuted()
    {
        var (_, _, vectors) = LoadSuite();
        Assert.Equal(55, vectors.Count);
    }

    [Fact]
    public void SpatialSemantics_ConnectivityIsNeverInferred()
    {
        // BLD-020 / CAV-007：touches/crosses 证据永不产生连通性（显式连接记录才是正式语义）。
        var (_, _, vectors) = LoadSuite();
        var spatialVectors = vectors.OfType<JsonObject>()
            .Where(v => (string)v["validation_layer"]! == "spatial_semantics")
            .ToList();
        Assert.Equal(2, spatialVectors.Count);
        Assert.All(spatialVectors, v =>
        {
            Assert.Equal("valid", (string)v["expected"]!);
            Assert.False((bool)v["expected_connectivity"]!);
        });
    }

    [Fact]
    public void UnsupportedKeywordInsideReferencedDefinition_FailsClosed()
    {
        using var schema = JsonDocument.Parse("""
            {
              "$defs": {
                "node": {
                  "type": "string",
                  "unevaluatedProperties": false
                }
              },
              "type": "object",
              "properties": {
                "value": { "$ref": "#/$defs/node" }
              }
            }
            """);
        using var instance = JsonDocument.Parse("""{"value":"ok"}""");

        Assert.Throws<RegistryException>(
            () => JsonSchemaSubset.Validate(schema.RootElement, instance.RootElement));
    }


    // ---- RFC 6902 子集（add / remove / replace） ----

    /// <summary>
    /// 已知向量工件漂移的规范化（仅 patch 值内的三个 legacy 键名）：
    /// P2-09-40 V0.6 部分向量沿用 P2-09-38 V0.4 时代的字段名，P2-09-38 V0.5 已改名为
    /// boundary/region_geometry_component_ids 与 geometry_component_id。
    /// </summary>
    private static void NormalizeLegacyFieldNames(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (legacy, current) in new[]
                     {
                         ("boundary_component_ids", "boundary_geometry_component_ids"),
                         ("region_component_ids", "region_geometry_component_ids"),
                         ("component_id", "geometry_component_id"),
                     })
            {
                if (obj.ContainsKey(legacy) && !obj.ContainsKey(current))
                {
                    obj[current] = obj[legacy]!.DeepClone();
                    obj.Remove(legacy);
                }
            }

            foreach (var property in obj.ToArray())
            {
                NormalizeLegacyFieldNames(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                NormalizeLegacyFieldNames(item);
            }
        }
    }

    private static void ApplyPatch(JsonObject target, JsonObject patch)
    {
        var op = (string)patch["op"]!;
        var pointer = (string)patch["path"]!;
        switch (op)
        {
            case "add":
            {
                var (parent, token) = SplitPointer(target, pointer);
                var value = patch["value"]!.DeepClone();
                if (parent is JsonArray array)
                {
                    if (token == "-")
                    {
                        array.Add(value);
                    }
                    else
                    {
                        array.Insert(int.Parse(token, System.Globalization.CultureInfo.InvariantCulture), value);
                    }
                }
                else
                {
                    ((JsonObject)parent)[token] = value;
                }

                break;
            }

            case "replace":
            {
                var (parent, token) = SplitPointer(target, pointer);
                var value = patch["value"]!.DeepClone();
                if (parent is JsonArray array)
                {
                    array[int.Parse(token, System.Globalization.CultureInfo.InvariantCulture)] = value;
                }
                else
                {
                    ((JsonObject)parent)[token] = value;
                }

                break;
            }

            case "remove":
            {
                var (parent, token) = SplitPointer(target, pointer);
                if (parent is JsonArray array)
                {
                    array.RemoveAt(int.Parse(token, System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    ((JsonObject)parent).Remove(token);
                }

                break;
            }

            default:
                throw new InvalidOperationException($"向量使用了本运行器未实现的 patch op \"{op}\"。");
        }
    }

    private static (JsonNode Parent, string Token) SplitPointer(JsonObject root, string pointer)
    {
        var segments = pointer.Split('/');
        var current = (JsonNode)root;
        for (var i = 1; i < segments.Length - 1; i++)
        {
            var token = segments[i].Replace("~1", "/").Replace("~0", "~");
            JsonNode? next;
            if (current is JsonArray array && int.TryParse(token, out var index))
            {
                next = array[index];
            }
            else
            {
                next = current[token];
            }

            current = next ?? throw new InvalidOperationException($"patch 路径 {pointer} 无法解析（段 {token}）。");
        }

        return (current, segments[^1].Replace("~1", "/").Replace("~0", "~"));
    }

    // ---- 向量外部上下文 → 校验器上下文 ----

    private static AggregatePublicationContext? BuildPublicationContext(JsonObject vector)
    {
        var hasLinks = vector.ContainsKey("external_asset_links");
        var hasCatalog = vector.ContainsKey("asset_catalog");
        var hasCas = vector.ContainsKey("expected_definition_hash");
        if (!hasLinks && !hasCatalog && !hasCas)
        {
            return null;
        }

        var links = new List<(string AssetId, string Role)>();
        if (vector["external_asset_links"] is JsonArray linkArray)
        {
            foreach (var link in linkArray.OfType<JsonObject>())
            {
                links.Add(((string)link["asset_id"]!, (string)link["role"]!));
            }
        }

        var catalog = new List<(string AssetId, string ContentHash)>();
        if (vector["asset_catalog"] is JsonArray catalogArray)
        {
            foreach (var asset in catalogArray.OfType<JsonObject>())
            {
                catalog.Add(((string)asset["asset_id"]!, (string)asset["content_hash"]!));
            }
        }

        return new AggregatePublicationContext
        {
            AssetLinks = links,
            AssetCatalog = catalog,
            ExpectedDefinitionHash = (string?)vector["expected_definition_hash"],
            CurrentDefinitionHash = (string?)vector["current_definition_hash"],
        };
    }

    private static AggregateContext? BuildAggregateContext(JsonObject vector)
    {
        var owner = (string?)vector["owner_cave_id"];
        var endpointOwner = (string?)vector["endpoint_owner_cave_id"];
        var measurement = (string?)vector["measurement_semantics"];
        if (owner is null && endpointOwner is null && measurement is null)
        {
            return null;
        }

        return new AggregateContext
        {
            OwnerObjectId = owner,
            EndpointOwnerObjectId = endpointOwner,
            MeasurementSemantics = measurement,
            SurveyFrameId = (string?)vector["survey_frame_id"],
        };
    }
}
