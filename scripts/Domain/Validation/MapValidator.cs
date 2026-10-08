using WorldView.Domain;

namespace WorldView.Domain.Validation;

/// <summary>
/// 地图文档校验器（针对 <see cref="MapDoc"/>）。
/// 校验规则（按 spec.md §6.5 + checklist B）：
/// <list type="number">
///   <item>grid.width × grid.height 非 0 且非负（M1 vs M2 唯一必填）</item>
///   <item>terrain_data.cells 中每个 (x, y) 在网格范围内（M2 必跑）</item>
///   <item>terrain_data.cells 中 (x, y) 不重复（M2 必跑）</item>
///   <item>city_refs.map_ref 指向的文件存在（M2 必跑）</item>
///   <item>layers[].ref 指向的文件存在（M2 必跑）</item>
///   <item>buildings[].at 在城市地图网格范围内（M2 必跑）</item>
///   <item>网格超过 1024 × 1024 提示改 chunked（M2 警告）</item>
///   <item>generation 参数超出 GenerationLimits 范围（仅警告，不阻塞 save）</item>
/// </list>
/// </summary>
public sealed class MapValidator : IValidator<MapDoc>
{
    /// <summary>
    /// sparse 编码下网格上限（见 spec.md §5.1.2：≤1024×1024 走 sparse,>1M 提示用户手动切 chunked）。
    /// </summary>
    public const int SparseUpperBound = 1024;

    public ValidationResult Validate(MapDoc map, string path, string rootPath)
    {
        if (map is null) throw new ArgumentNullException(nameof(map));
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("path 不可为空", nameof(path));
        if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("rootPath 不可为空", nameof(rootPath));

        var errors = new List<ValidationError>();
        var warnings = new List<ValidationError>();

        // 规则 1: 网格尺寸
        if (map.Grid is null || map.Grid.Width <= 0 || map.Grid.Height <= 0)
        {
            errors.Add(ValidationError.GridSizeMissing(path));
            return new ValidationResult(errors.ToArray(), warnings.ToArray());
        }

        var width = map.Grid.Width;
        var height = map.Grid.Height;

        // 规则 7: 网格超过 sparse 上限
        if (width > SparseUpperBound || height > SparseUpperBound)
        {
            warnings.Add(ValidationError.GridTooLargeSparse(path, width, height));
        }

        // 规则 8: generation 参数范围（仅在用户填了 generation 时校验）
        if (map.Generation is not null)
        {
            var violation = WorldView.Domain.Generation.GenerationLimits.DescribeViolation(map.Generation);
            if (violation is not null)
            {
                warnings.Add(new ValidationError(
                    Severity.Warning, "W_GENERATION_PARAM", path, violation));
            }
        }

        // 规则 2 + 3: terrain_data.cells 范围 + 唯一性
        if (map.TerrainData is not null && map.TerrainData.Cells is not null)
        {
            var seen = new HashSet<(int x, int y)>();
            foreach (var entry in map.TerrainData.Cells)
            {
                var at = entry.At;
                if (at.X < 0 || at.X >= width || at.Y < 0 || at.Y >= height)
                {
                    errors.Add(ValidationError.GridOutOfBounds(path, at.X, at.Y, width, height));
                    continue;
                }
                if (!seen.Add((at.X, at.Y)))
                {
                    errors.Add(ValidationError.TerrainCellDup(path, at.X, at.Y));
                }
            }
        }

        // 规则 4: city_refs.map_ref 文件存在
        if (map.CityRefs is not null)
        {
            foreach (var cref in map.CityRefs)
            {
                if (string.IsNullOrEmpty(cref.MapRef)) continue;
                if (IsAbsolutePath(cref.MapRef))
                {
                    errors.Add(ValidationError.RefAbsolute(path, $"city_refs[{cref.Id}].map_ref"));
                    continue;
                }
                var mapFullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    rootPath, path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                var fullPath = ResolveRefPath(rootPath, mapFullPath, cref.MapRef);
                if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(ValidationError.RefParent(path, $"city_refs[{cref.Id}].map_ref"));
                    continue;
                }
                if (!System.IO.File.Exists(fullPath))
                {
                    errors.Add(ValidationError.CityRefMissing(path, cref.Id, cref.MapRef));
                }
            }
        }

        // 规则 5: layers[].ref 文件存在
        if (map.Layers is not null)
        {
            foreach (var lref in map.Layers)
            {
                if (string.IsNullOrEmpty(lref.RefPath)) continue;
                if (IsAbsolutePath(lref.RefPath))
                {
                    errors.Add(ValidationError.RefAbsolute(path, "layers[].ref"));
                    continue;
                }
                // 规则语义是「解析后不得越出工程根」,允许 '..' 作为相对路径段。
                var mapFullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    rootPath, path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                var fullPath = ResolveRefPath(rootPath, mapFullPath, lref.RefPath);
                if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(ValidationError.RefParent(path, "layers[].ref"));
                    continue;
                }
                if (!System.IO.File.Exists(fullPath))
                {
                    errors.Add(ValidationError.LayerRefMissing(path, lref.RefPath));
                }
            }
        }

        // 规则 6: buildings[].at 在城市地图网格内
        if (map.Buildings is not null)
        {
            foreach (var b in map.Buildings)
            {
                if (b.At.X < 0 || b.At.X >= width || b.At.Y < 0 || b.At.Y >= height)
                {
                    errors.Add(ValidationError.BuildingOutOfBounds(path, b.Id, b.At.X, b.At.Y, width, height));
                }
            }
        }

        return new ValidationResult(errors.ToArray(), warnings.ToArray());
    }

    private static bool IsAbsolutePath(string p)
    {
        if (string.IsNullOrEmpty(p)) return false;
        // Windows 绝对路径以盘符开头或 UNC
        if (p.Length >= 3 && char.IsLetter(p[0]) && p[1] == ':' && (p[2] == '\\' || p[2] == '/')) return true;
        if (p.StartsWith(@"\\") || p.StartsWith("//")) return true;
        // Unix 绝对路径以 / 开头
        if (p.StartsWith("/") && !p.StartsWith("~/")) return true;
        return false;
    }

    /// <summary>
    /// 把 ref 字段解析为绝对路径（与 ProjectLoader.ResolveRefPath 同语义）。
    /// MVP 阶段：ref 通常相对当前 map 文件，map 文件在 <c>maps/world/</c> 时用 <c>../../layers/</c>。
    /// </summary>
    private static string ResolveRefPath(string rootPath, string mapFullPath, string relRef)
    {
        var mapDir = System.IO.Path.GetDirectoryName(mapFullPath) ?? rootPath;
        var fromMap = System.IO.Path.GetFullPath(System.IO.Path.Combine(mapDir, relRef));
        if (System.IO.File.Exists(fromMap)) return fromMap;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(
            rootPath,
            relRef.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }
}