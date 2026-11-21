using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorldView.Domain.Registry;

/// <summary>单条 schema 校验违规（keyword + 实例路径 + 说明）。</summary>
public sealed record SchemaViolation
{
    /// <summary>Draft 2020-12 关键词（如 additionalProperties/required/const/oneOf）。</summary>
    public required string Keyword { get; init; }

    /// <summary>实例 JSON 路径（/a/b/0 形式）。</summary>
    public required string InstancePath { get; init; }

    /// <summary>违规详情（含期望值摘要）。</summary>
    public required string Message { get; init; }

    /// <summary>required/additionalProperties 违规涉及的属性名（可空）。</summary>
    public string? PropertyName { get; init; }
}

/// <summary>
/// 有界 Draft 2020-12 JSON Schema 子集执行器（CD-02-E S3）。
/// 支持关键词（与 P2-09-38/39 V0.5 实际使用集一致）：type / properties / required /
/// additionalProperties(false) / items / enum / const / minLength / maxLength /
/// minimum / maximum / exclusiveMinimum / minItems / maxItems / uniqueItems /
/// pattern / oneOf / allOf / $ref(#/$defs/...) / $defs / dependentRequired /
/// maxProperties。schema 内出现任何**不支持**的关键词即 fail-closed（拒绝静默跳过，
/// 与 unknown_fields=reject 同一原则）。全量 Draft 2020-12 引擎与第三方包在 Domain
/// 禁用（无第三方依赖原则）；若未来 schema 引入新关键词，本类是扩展点。
/// </summary>
public static class JsonSchemaSubset
{
    private static readonly HashSet<string> SupportedKeywords = new(StringComparer.Ordinal)
    {
        "type", "properties", "required", "additionalProperties", "items", "enum", "const",
        "minLength", "maxLength", "minimum", "maximum", "exclusiveMinimum", "minItems",
        "maxItems", "uniqueItems", "pattern", "oneOf", "allOf", "$ref", "$defs",
        "dependentRequired", "maxProperties",
    };

    /// <summary>校验 <paramref name="instance"/> 是否满足 schema；返回全部违规（空 = 通过）。</summary>
    public static IReadOnlyList<SchemaViolation> Validate(JsonElement schema, JsonElement instance)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new RegistryException("schema_invalid", "schema 根必须为 object。");
        }

        EnsureSupportedKeywords(schema, "$");
        var context = new ValidationContext(schema);
        var violations = new List<SchemaViolation>();
        // 路径为标准 JSON Pointer（根 = 空串）；$ 仅用于 EnsureSupportedKeywords 的展示。
        ApplySchema(schema, instance, string.Empty, violations, context);
        return violations;
    }

    /// <summary>标注类元关键词（不参与断言；Draft 2020-12 annotation/meta + P2-09-38/39 文件级 schema_version 元数据）。</summary>
    private static readonly HashSet<string> IgnoredKeywords = new(StringComparer.Ordinal)
    {
        "$schema", "$id", "$comment", "title", "description", "examples", "$vocabulary", "schema_version",
    };

    /// <summary>schema 是否声明了本执行器不支持的关键词（部署/版本防线）。</summary>
    public static void EnsureSupportedKeywords(JsonElement schemaNode, string path)
    {
        if (schemaNode.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in schemaNode.EnumerateObject())
        {
            if (property.Name.StartsWith("x-", StringComparison.Ordinal) || IgnoredKeywords.Contains(property.Name))
            {
                continue;
            }

            if (!SupportedKeywords.Contains(property.Name))
            {
                throw new RegistryException(
                    "schema_invalid",
                    $"schema 于 {path} 使用本执行器不支持的关键词 \"{property.Name}\"，fail-closed。");
            }

            if (property.Name is "properties" or "dependentRequired" && property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in property.Value.EnumerateObject())
                {
                    EnsureSupportedKeywords(sub.Value, $"{path}/{property.Name}/{sub.Name}");
                }
            }
            else if (property.Name is "items" or "additionalProperties" && property.Value.ValueKind == JsonValueKind.Object)
            {
                EnsureSupportedKeywords(property.Value, $"{path}/{property.Name}");
            }
            else if (property.Name is "oneOf" or "allOf" && property.Value.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var branch in property.Value.EnumerateArray())
                {
                    EnsureSupportedKeywords(branch, $"{path}/{property.Name}/{index}");
                    index++;
                }
            }
        }
    }

    private sealed class ValidationContext
    {
        public ValidationContext(JsonElement root) => Root = root;

        public JsonElement Root { get; }
    }

    private static void ApplySchema(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<SchemaViolation> violations,
        ValidationContext context)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // $ref（仅内部 #/$defs/... 引用；嵌套引用由调用序递归）。
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var resolved = ResolveRef(reference.GetString() ?? string.Empty, context.Root);
            ApplySchema(resolved, instance, path, violations, context);
            // Draft 2020-12：$ref 与兄弟关键词可并存——本 schema 集未用兄弟并置，但按规范继续。
        }

        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var branch in allOf.EnumerateArray())
            {
                ApplySchema(branch, instance, path, violations, context);
            }
        }

        if (schema.TryGetProperty("type", out var type))
        {
            ValidateType(type, instance, path, violations);
        }

        if (schema.TryGetProperty("enum", out var enumElement))
        {
            var match = enumElement.EnumerateArray().Any(v => JsonDeepEquals(v, instance));
            if (!match)
            {
                violations.Add(new SchemaViolation
                {
                    Keyword = "enum",
                    InstancePath = path,
                    Message = $"值 {instance.GetRawText()} 不在枚举 {enumElement.GetRawText()} 内。",
                });
            }
        }

        if (schema.TryGetProperty("const", out var constElement) && !JsonDeepEquals(constElement, instance))
        {
            violations.Add(new SchemaViolation
            {
                Keyword = "const",
                InstancePath = path,
                Message = $"值 {instance.GetRawText()} ≠ const {constElement.GetRawText()}。",
            });
        }

        if (instance.ValueKind == JsonValueKind.String)
        {
            var text = instance.GetString() ?? string.Empty;
            if (schema.TryGetProperty("minLength", out var minLength) && text.Length < minLength.GetInt64())
            {
                violations.Add(new SchemaViolation { Keyword = "minLength", InstancePath = path, Message = $"长度 {text.Length} < minLength {minLength.GetInt64()}。" });
            }

            if (schema.TryGetProperty("maxLength", out var maxLength) && text.Length > maxLength.GetInt64())
            {
                violations.Add(new SchemaViolation { Keyword = "maxLength", InstancePath = path, Message = $"长度 {text.Length} > maxLength {maxLength.GetInt64()}。" });
            }

            if (schema.TryGetProperty("pattern", out var pattern))
            {
                if (!Regex.IsMatch(text, pattern.GetString() ?? string.Empty, RegexOptions.CultureInvariant))
                {
                    violations.Add(new SchemaViolation { Keyword = "pattern", InstancePath = path, Message = $"不匹配 pattern {pattern.GetString()}。" });
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Number)
        {
            var value = instance.GetDouble();
            if (schema.TryGetProperty("minimum", out var minimum) && value < minimum.GetDouble())
            {
                violations.Add(new SchemaViolation { Keyword = "minimum", InstancePath = path, Message = $"{value} < minimum {minimum.GetDouble()}。" });
            }

            if (schema.TryGetProperty("maximum", out var maximum) && value > maximum.GetDouble())
            {
                violations.Add(new SchemaViolation { Keyword = "maximum", InstancePath = path, Message = $"{value} > maximum {maximum.GetDouble()}。" });
            }

            if (schema.TryGetProperty("exclusiveMinimum", out var exclusiveMinimum) && value <= exclusiveMinimum.GetDouble())
            {
                violations.Add(new SchemaViolation { Keyword = "exclusiveMinimum", InstancePath = path, Message = $"{value} <= exclusiveMinimum {exclusiveMinimum.GetDouble()}。" });
            }
        }

        if (instance.ValueKind == JsonValueKind.Array)
        {
            var count = instance.GetArrayLength();
            if (schema.TryGetProperty("minItems", out var minItems) && count < minItems.GetInt64())
            {
                violations.Add(new SchemaViolation { Keyword = "minItems", InstancePath = path, Message = $"项数 {count} < minItems {minItems.GetInt64()}。" });
            }

            if (schema.TryGetProperty("maxItems", out var maxItems) && count > maxItems.GetInt64())
            {
                violations.Add(new SchemaViolation { Keyword = "maxItems", InstancePath = path, Message = $"项数 {count} > maxItems {maxItems.GetInt64()}。" });
            }

            if (schema.TryGetProperty("uniqueItems", out var uniqueItems) && uniqueItems.ValueKind == JsonValueKind.True)
            {
                var seen = new List<JsonElement>();
                var duplicate = false;
                foreach (var item in instance.EnumerateArray())
                {
                    if (seen.Any(v => JsonDeepEquals(v, item)))
                    {
                        duplicate = true;
                        break;
                    }

                    seen.Add(item);
                }

                if (duplicate)
                {
                    violations.Add(new SchemaViolation { Keyword = "uniqueItems", InstancePath = path, Message = "数组含重复项。" });
                }
            }

            if (schema.TryGetProperty("items", out var items))
            {
                var index = 0;
                foreach (var item in instance.EnumerateArray())
                {
                    ApplySchema(items, item, $"{path}/{index}", violations, context);
                    index++;
                }
            }
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            var propertyCount = 0;
            foreach (var _ in instance.EnumerateObject())
            {
                propertyCount++;
            }

            if (schema.TryGetProperty("maxProperties", out var maxProperties) && propertyCount > maxProperties.GetInt64())
            {
                violations.Add(new SchemaViolation { Keyword = "maxProperties", InstancePath = path, Message = $"属性数 {propertyCount} > maxProperties {maxProperties.GetInt64()}。" });
            }

            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var name in required.EnumerateArray())
                {
                    var propertyName = name.GetString() ?? string.Empty;
                    if (!instance.TryGetProperty(propertyName, out _))
                    {
                        violations.Add(new SchemaViolation
                        {
                            Keyword = "required",
                            InstancePath = path,
                            Message = $"缺少必填属性 \"{propertyName}\"。",
                            PropertyName = propertyName,
                        });
                    }
                }
            }

            if (schema.TryGetProperty("dependentRequired", out var dependentRequired))
            {
                foreach (var dependency in dependentRequired.EnumerateObject())
                {
                    if (!instance.TryGetProperty(dependency.Name, out _))
                    {
                        continue;
                    }

                    foreach (var dependent in dependency.Value.EnumerateArray())
                    {
                        var dependentName = dependent.GetString() ?? string.Empty;
                        if (!instance.TryGetProperty(dependentName, out _))
                        {
                            violations.Add(new SchemaViolation
                            {
                                Keyword = "dependentRequired",
                                InstancePath = path,
                                Message = $"存在 \"{dependency.Name}\" 时缺少依赖属性 \"{dependentName}\"。",
                                PropertyName = dependentName,
                            });
                        }
                    }
                }
            }

            var hasProperties = schema.TryGetProperty("properties", out var properties);
            var closedObject = schema.TryGetProperty("additionalProperties", out var additionalProperties)
                && additionalProperties.ValueKind == JsonValueKind.False;
            foreach (var property in instance.EnumerateObject())
            {
                if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
                {
                    ApplySchema(propertySchema, property.Value, $"{path}/{property.Name}", violations, context);
                }
                else if (closedObject)
                {
                    violations.Add(new SchemaViolation
                    {
                        Keyword = "additionalProperties",
                        InstancePath = path,
                        Message = $"含未声明属性 \"{property.Name}\"（unknown_fields=reject）。",
                        PropertyName = property.Name,
                    });
                }
            }
        }

        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var branches = oneOf.EnumerateArray().ToArray();
            var satisfied = 0;
            var branchViolationLists = new List<List<SchemaViolation>>();
            foreach (var branch in branches)
            {
                var branchViolations = new List<SchemaViolation>();
                ApplySchema(branch, instance, path, branchViolations, context);
                branchViolationLists.Add(branchViolations);
                if (branchViolations.Count == 0)
                {
                    satisfied++;
                }
            }

            if (satisfied != 1)
            {
                // Draft 2020-12：oneOf 失败时传播分支违规（供错误码映射定位分支原因）；
                // satisfied == 1（合法）时不传播——其余分支的失败不是实例违规。
                violations.Add(new SchemaViolation
                {
                    Keyword = "oneOf",
                    InstancePath = path,
                    Message = $"oneOf 分支满足数 {satisfied} ≠ 1（{branches.Length} 分支）。",
                });
                foreach (var branchViolations in branchViolationLists)
                {
                    violations.AddRange(branchViolations);
                }
            }
        }
    }

    private static void ValidateType(JsonElement type, JsonElement instance, string path, List<SchemaViolation> violations)
    {
        var expected = type.ValueKind == JsonValueKind.String ? type.GetString() : null;
        var ok = expected switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "number" => instance.ValueKind == JsonValueKind.Number,
            "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => throw new RegistryException("schema_invalid", $"不支持的 type \"{expected}\"，fail-closed。"),
        };
        if (!ok)
        {
            violations.Add(new SchemaViolation { Keyword = "type", InstancePath = path, Message = $"类型需为 {expected}，实际 {instance.ValueKind}。" });
        }
    }

    private static JsonElement ResolveRef(string reference, JsonElement root)
    {
        const string prefix = "#/$defs/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new RegistryException("schema_invalid", $"不支持的外部 $ref \"{reference}\"（仅 #/$defs/ 内部引用）。");
        }

        var defName = reference[prefix.Length..];
        if (!root.TryGetProperty("$defs", out var defs) || !defs.TryGetProperty(defName, out var resolved))
        {
            throw new RegistryException("schema_invalid", $"$ref \"{reference}\" 目标不存在。");
        }

        return resolved;
    }

    private static bool JsonDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var aCount = 0;
                foreach (var property in a.EnumerateObject())
                {
                    aCount++;
                    if (!b.TryGetProperty(property.Name, out var bValue) || !JsonDeepEquals(property.Value, bValue))
                    {
                        return false;
                    }
                }

                var bCount = 0;
                foreach (var _ in b.EnumerateObject())
                {
                    bCount++;
                }

                return aCount == bCount;
            }

            case JsonValueKind.Array:
            {
                if (a.GetArrayLength() != b.GetArrayLength())
                {
                    return false;
                }

                using var aEnumerator = a.EnumerateArray().GetEnumerator();
                using var bEnumerator = b.EnumerateArray().GetEnumerator();
                while (aEnumerator.MoveNext() && bEnumerator.MoveNext())
                {
                    if (!JsonDeepEquals(aEnumerator.Current, bEnumerator.Current))
                    {
                        return false;
                    }
                }

                return true;
            }

            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return false;
        }
    }
}
