using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Objects;

/// <summary>
/// canonical JSON 规范化与确定性哈希（Domain 职责，P2-09-16 §4 / CD-02-A §14 R-baseline）。
/// 规范化形式：对象键按 ordinal 升序递归排序、无空白、整数以 Int64 表示、
/// 非整数以 double 往返表示、字符串转义由 Utf8JsonWriter 决定。同一语义内容
/// （仅键序/空白不同）得到同一字节序列；数值拼写差异（1 与 1.0）按上述规则固定。
/// 本规范化由 Core Domain 独占：Application/UI 永不自算指纹（Handoff §5.7）。
/// </summary>
public static class CanonicalJson
{
    /// <summary>
    /// 规范化 JSON 文本；输入不可解析时抛 <see cref="CoreDomainDataException"/>
    /// （fail-closed，R-fail-closed：payload 结构层拒绝）。
    /// </summary>
    public static string Canonicalize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new CoreDomainDataException($"JSON payload 不可解析：{ex.Message}", ex);
        }

        using (document)
        using (var buffer = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = false }))
            {
                WriteCanonicalValue(writer, document.RootElement);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }

    /// <summary>canonical 文本的 SHA-256（小写十六进制；CAS 冲突检测基准）。</summary>
    public static string Sha256Hex(string canonicalText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalText);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>规范化并计算哈希（编辑/创建路径的标准入口）。</summary>
    public static (string CanonicalJson, string Sha256Hex) CanonicalizeAndHash(string json)
    {
        var canonical = Canonicalize(json);
        return (canonical, Sha256Hex(canonical));
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                // 属性按名称 ordinal 升序重排（同键重复为非法 JSON，Parse 已拒绝）。
                var properties = new List<(string Name, JsonElement Value)>();
                foreach (var property in value.EnumerateObject())
                {
                    properties.Add((property.Name, property.Value));
                }

                properties.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
                writer.WriteStartObject();
                foreach (var (name, propertyValue) in properties)
                {
                    writer.WritePropertyName(name);
                    WriteCanonicalValue(writer, propertyValue);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (value.TryGetInt64(out var integer))
                {
                    writer.WriteNumberValue(integer);
                }
                else
                {
                    writer.WriteNumberValue(value.GetDouble());
                }

                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new CoreDomainDataException($"JSON 值种类 {value.ValueKind} 不在 canonical 化支持范围内。");
        }
    }
}

/// <summary>
/// Map Initial State content_fingerprint 的组成契约（CD-02-A §10；P2-09-16 §4）：
/// 成员集合按 object_identity_id（canonical 小写连字符文本）升序，逐行写
/// identity + canonical membership payload + canonical placement payload（缺省 "-"）+
/// placement schema version（缺省 "-"）；首行写组成版本标记与 baseline_schema_version。
/// 不含时间戳/审计字段；同内容同指纹、顺序无关。
/// </summary>
public static class InitialStateFingerprint
{
    private const string CompositionVersion = "initial-state-fingerprint-v1";

    public static string Compute(long baselineSchemaVersion, IReadOnlyList<IInitialObjectContent> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var builder = new StringBuilder(256);
        builder.Append(CompositionVersion)
            .Append('\n')
            .Append(baselineSchemaVersion.ToString(CultureInfo.InvariantCulture))
            .Append('\n');

        var sorted = new List<IInitialObjectContent>(members);
        sorted.Sort(static (a, b) => string.CompareOrdinal(
            a.ObjectIdentityId.ToString("D"),
            b.ObjectIdentityId.ToString("D")));

        foreach (var member in sorted)
        {
            builder.Append(member.ObjectIdentityId.ToString("D"))
                .Append('\t')
                .Append(CanonicalJson.Canonicalize(member.MembershipPayloadJson))
                .Append('\t')
                .Append(member.PlacementPayloadJson is null ? "-" : CanonicalJson.Canonicalize(member.PlacementPayloadJson))
                .Append('\t')
                .Append(member.PlacementSchemaVersion?.ToString(CultureInfo.InvariantCulture) ?? "-")
                .Append('\n');
        }

        return CanonicalJson.Sha256Hex(builder.ToString());
    }
}
