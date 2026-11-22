using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using WorldView.Domain.Persistence;

namespace WorldView.Domain.Geometry;

/// <summary>
/// `worldview.geometry-json` Asset 负载 envelope 校验（P2-09-25 §2/§3 Recommended V1
/// 方向的有界实现；该决策非冻结 ADR——若编码决策变更，本类是唯一替换点）。
/// 校验为结构层：format_id/format_version/spatial_reference/axis_order/unit/dimension/
/// family/precision profile 存在性与类型、body 与 family 形状一致、坐标为有限数值、
/// 环闭合（polygon）、未知曲线段类型拒绝（不得静默线性化）。
/// 精度量化与 ring orientation 深校验待 spatial-reference 契约关门（P2-09-25 §5）。
/// </summary>
public static class GeometryAssetEnvelope
{
    public const string FormatId = "worldview.geometry-json";
    public const long CurrentFormatVersion = 1;

    /// <summary>校验 envelope JSON 文本（通常为已发布 Asset 字节的 UTF-8 文本）。</summary>
    public static void Validate(string envelopeJson, string expectedFamily, int expectedDimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeJson);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(envelopeJson);
        }
        catch (JsonException ex)
        {
            throw new CoreDomainDataException($"geometry envelope 不可解析：{ex.Message}", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CoreDomainDataException("geometry envelope 必须为 JSON object。");
            }

            var formatId = RequireString(root, "format_id");
            if (!string.Equals(formatId, FormatId, StringComparison.Ordinal))
            {
                throw new CoreDomainDataException($"geometry envelope format_id \"{formatId}\" 不是 {FormatId}。");
            }

            var formatVersion = RequireInt64(root, "format_version");
            if (formatVersion < 1)
            {
                throw new CoreDomainDataException("geometry envelope format_version 必须 >= 1。");
            }

            RequireString(root, "spatial_reference_id");
            RequireString(root, "axis_order");
            RequireString(root, "unit");
            RequireString(root, "coordinate_precision_profile_id");
            RequireString(root, "geometry_family");

            var dimension = RequireInt64(root, "dimension");
            if (dimension != expectedDimension)
            {
                throw new CoreDomainDataException(
                    $"geometry envelope dimension {dimension} 与期望 {expectedDimension} 不一致。");
            }

            if (dimension != GeometryRules.SupportedDimension)
            {
                throw new CoreDomainDataException($"geometry envelope dimension {dimension} 不受 V1 支持（仅 2D）。");
            }

            var family = RequireString(root, "geometry_family");
            if (!string.Equals(family, expectedFamily, StringComparison.Ordinal))
            {
                throw new CoreDomainDataException(
                    $"geometry envelope family \"{family}\" 与期望 \"{expectedFamily}\" 不一致。");
            }

            if (!root.TryGetProperty("body", out var body))
            {
                throw new CoreDomainDataException("geometry envelope 缺少 body。");
            }

            ValidateBody(family, body);
        }
    }

    private static void ValidateBody(string family, JsonElement body)
    {
        switch (family)
        {
            case "point":
                ValidatePosition(body);
                break;
            case "line_string":
                ValidateLineString(body);
                break;
            case "polygon":
                ValidatePolygon(body);
                break;
            case "multi_point":
                RequireArray(body, "multi_point body");
                foreach (var position in body.EnumerateArray())
                {
                    ValidatePosition(position);
                }

                break;
            case "multi_line_string":
                RequireArray(body, "multi_line_string body");
                foreach (var line in body.EnumerateArray())
                {
                    ValidateLineString(line);
                }

                break;
            case "multi_polygon":
                RequireArray(body, "multi_polygon body");
                foreach (var polygon in body.EnumerateArray())
                {
                    ValidatePolygon(polygon);
                }

                break;
            case "curve":
                ValidateCurve(body);
                break;
            default:
                throw new CoreDomainDataException($"geometry family \"{family}\" 不在 V1 词汇内。");
        }
    }

    private static void ValidatePosition(JsonElement position)
    {
        if (position.ValueKind != JsonValueKind.Array)
        {
            throw new CoreDomainDataException("坐标必须为 [x, y] 数组。");
        }

        var count = position.GetArrayLength();
        if (count != GeometryRules.SupportedDimension)
        {
            throw new CoreDomainDataException($"坐标分量数 {count} 与 dimension {GeometryRules.SupportedDimension} 不一致。");
        }

        foreach (var component in position.EnumerateArray())
        {
            // JSON 规范不允许 NaN/Infinity（Parse 已拒绝）；此处校验分量确为数值。
            if (component.ValueKind != JsonValueKind.Number)
            {
                throw new CoreDomainDataException("坐标分量必须为数值（拒绝 NaN/Inf/非数值，P2-09-25 §7）。");
            }

            var value = component.GetDouble();
            if (!IsFinite(value))
            {
                throw new CoreDomainDataException("坐标分量必须为有限数值。");
            }
        }
    }

    private static void ValidateLineString(JsonElement lineString)
    {
        RequireArray(lineString, "line_string body");
        if (lineString.GetArrayLength() < 2)
        {
            throw new CoreDomainDataException("line_string 至少需要 2 个坐标。");
        }

        foreach (var position in lineString.EnumerateArray())
        {
            ValidatePosition(position);
        }
    }

    private static void ValidatePolygon(JsonElement polygon)
    {
        RequireArray(polygon, "polygon body");
        if (polygon.GetArrayLength() < 1)
        {
            throw new CoreDomainDataException("polygon 至少需要 1 个环（exterior）。");
        }

        foreach (var ring in polygon.EnumerateArray())
        {
            RequireArray(ring, "polygon ring");
            if (ring.GetArrayLength() < 4)
            {
                throw new CoreDomainDataException("环至少需要 4 个位置（闭合）。");
            }

            var positions = new List<(double X, double Y)>();
            foreach (var position in ring.EnumerateArray())
            {
                ValidatePosition(position);
                positions.Add((position[0].GetDouble(), position[1].GetDouble()));
            }

            var first = positions[0];
            var last = positions[^1];
            if (first.X != last.X || first.Y != last.Y)
            {
                throw new CoreDomainDataException("polygon 环未闭合（首尾坐标不一致）。");
            }
        }
    }

    private static void ValidateCurve(JsonElement curve)
    {
        if (curve.ValueKind != JsonValueKind.Object || !curve.TryGetProperty("segments", out var segments))
        {
            throw new CoreDomainDataException("curve body 必须为含 segments 的 object。");
        }

        RequireArray(segments, "curve segments");
        if (segments.GetArrayLength() < 1)
        {
            throw new CoreDomainDataException("curve 至少需要 1 个段。");
        }

        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.ValueKind != JsonValueKind.Object || !segment.TryGetProperty("type", out var type))
            {
                throw new CoreDomainDataException("curve segment 缺少 type。");
            }

            var segmentType = type.GetString();
            switch (segmentType)
            {
                case "line":
                    if (!segment.TryGetProperty("points", out var points))
                    {
                        throw new CoreDomainDataException("line segment 缺少 points。");
                    }

                    RequireArray(points, "line segment points");
                    if (points.GetArrayLength() < 2)
                    {
                        throw new CoreDomainDataException("line segment 至少需要 2 个坐标。");
                    }

                    foreach (var position in points.EnumerateArray())
                    {
                        ValidatePosition(position);
                    }

                    break;
                case "cubic_bezier":
                    if (!segment.TryGetProperty("control_points", out var controlPoints))
                    {
                        throw new CoreDomainDataException("cubic_bezier segment 缺少 control_points。");
                    }

                    RequireArray(controlPoints, "cubic_bezier control_points");
                    if (controlPoints.GetArrayLength() != 4)
                    {
                        throw new CoreDomainDataException("cubic_bezier 恰需要 4 个控制点。");
                    }

                    foreach (var position in controlPoints.EnumerateArray())
                    {
                        ValidatePosition(position);
                    }

                    break;
                default:
                    // 未知段类型拒绝，不得静默线性化（P2-09-25 §3）。
                    throw new CoreDomainDataException(
                        $"curve segment type \"{segmentType ?? "(null)"}\" 不在 V1 词汇（line/cubic_bezier）内，拒绝。");
            }
        }
    }

    private static void RequireArray(JsonElement value, string what)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new CoreDomainDataException($"{what} 必须为数组。");
        }
    }

    private static string RequireString(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            return element.GetString()!;
        }

        throw new CoreDomainDataException($"geometry envelope 缺少合法字符串属性 \"{property}\"。");
    }

    private static long RequireInt64(JsonElement root, string property)
    {
        if (root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Number)
        {
            return element.GetInt64();
        }

        throw new CoreDomainDataException($"geometry envelope 缺少合法数值属性 \"{property}\"。");
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && !double.IsNegativeInfinity(value) && !double.IsPositiveInfinity(value);
}
