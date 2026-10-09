using System.Collections.Generic;
using System.Text.RegularExpressions;
using WorldView.Domain;

namespace WorldView.ProjectManagement;

/// <summary>
/// 地图 ID 生成器。按 <see cref="MapType"/> 走三类前缀：
/// <c>map_world_</c> / <c>map_city_</c> / <c>map_region_</c> + 自增编号（从 1 起）。
///
/// MVP 不开线程安全（单线程 UI 调用）。
/// </summary>
public static class MapIdGenerator
{
    private static readonly Regex SuffixPattern =
        new(@"^map_(?<kind>world|city|region)_(?<n>\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// 计算下一个 ID。基于 <paramref name="existing"/> 的 <c>map_index</c>
    /// （与可选 <c>Maps</c> 字段,后向兼容)判断已用编号最大值,加 1 输出。
    /// 任一字段为 null 或空时按 0 处理。
    /// </summary>
    public static string NextId(World existing, MapType type)
    {
        var prefix = TypePrefix(type);
        var max = 0;

        if (existing is not null)
        {
            if (existing.MapIndex is { } index)
            {
                foreach (var entry in index)
                {
                    max = Math.Max(max, ExtractSuffixNumber(entry?.RefPath, prefix));
                }
            }
        }

        return $"{prefix}{max + 1}";
    }

    private static string TypePrefix(MapType type) => type switch
    {
        MapType.World => "map_world_",
        MapType.City => "map_city_",
        _ => "map_region_", // Region/Street/Building/Underground 走 region 前缀（MVP 无新增类型先吃掉）
    };

    private static int ExtractSuffixNumber(string? refPath, string prefix)
    {
        if (string.IsNullOrWhiteSpace(refPath)) return 0;
        // ref 通常是 "maps/world/<id>.yaml"，先取最后一段去扩展名
        var tail = refPath;
        var slash = refPath.LastIndexOf('/');
        if (slash >= 0) tail = refPath[(slash + 1)..];
        var dot = tail.LastIndexOf('.');
        if (dot > 0) tail = tail[..dot];

        var match = SuffixPattern.Match(tail);
        if (!match.Success) return 0;
        var kind = match.Groups["kind"].Value;
        if (kind == "world" && prefix == "map_world_") goto Parse;
        if (kind == "city" && prefix == "map_city_") goto Parse;
        if (kind == "region" && prefix == "map_region_") goto Parse;
        return 0;
    Parse:
        return int.TryParse(match.Groups["n"].Value, out var n) ? n : 0;
    }
}
