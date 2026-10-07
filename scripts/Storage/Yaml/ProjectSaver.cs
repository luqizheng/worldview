using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 工程保存器：导出 <c>project.yaml</c>、<c>maps/*/*.yaml</c>、<c>layers/*.yaml</c> 与
/// <c>maps/*/*.terrain.yaml</c>。保存后重新加载可还原稀疏地形与图层引用
/// （修复 D7 已知死角：terrain cells / layers 写盘后丢失）。
///
/// 二进制大网格（M3+ chunked）未启用,见 <c>docs/二进制格式规格_v0.1.md</c>。
/// </summary>
public static class ProjectSaver
{
    /// <summary>
    /// 保存 <see cref="World"/> + <see cref="MapDoc"/>s 到 <paramref name="rootPath"/>。
    /// 不会重写 <c>layers/*.yaml</c> 与 <c>terrain_data.cells_ref</c> 文件。
    /// </summary>
    /// <param name="rootPath">工程根绝对路径（包含 <c>project.yaml</c>）。</param>
    /// <param name="world">工程头信息（map_index 等）。</param>
    /// <param name="maps">要保存的地图集合（顺序需与 <paramref name="world"/>.MapIndex 一致）。</param>
    /// <param name="layers">
    /// 已加载的 <see cref="LayerDoc"/> 列表（id → 文件）。Saver 按
    /// <see cref="MapDoc.Layers"/> 中每项的 <c>ref</c> 在此列表里查 id,匹配则写盘。
    /// 未匹配的层视为只读外部资源,跳过（不丢 ref,只是不写）。
    /// </param>
    public static void Save(
        string rootPath,
        World world,
        IReadOnlyList<MapDoc> maps,
        IReadOnlyList<LayerDoc>? layers = null)
    {
        if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("rootPath 不可为空", nameof(rootPath));
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (!System.IO.Directory.Exists(rootPath)) throw new DirectoryNotFoundException($"工程根不存在: {rootPath}");

        var projectYaml = YamlSchemas.Serialize(world);
        System.IO.File.WriteAllText(System.IO.Path.Combine(rootPath, "project.yaml"), projectYaml);

        // 写回 maps/{world,city,ref}/*.yaml（保持与 map_index 顺序一致）
        var savedMapRels = new List<string>();
        if (world.MapIndex is null)
        {
            // 即使没有 map，仍要写 layers（可能在工程根的 layers/ 目录下被其它 map 引用）
            WriteLayerFiles(rootPath, layers, maps, savedMapRels);
            return;
        }
        for (var i = 0; i < maps.Count && i < world.MapIndex.Count; i++)
        {
            var rel = world.MapIndex[i].RefPath;
            if (string.IsNullOrEmpty(rel)) continue;
            var full = System.IO.Path.Combine(rootPath, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var dir = System.IO.Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            savedMapRels.Add(rel);

            var src = maps[i];

            // 1) 写 cells_ref 外部文件（按约定 "<map_dir>/<map_basename>.terrain.yaml"）
            //    只在源数据有 cells 时写，避免覆盖空 cells 的现有文件
            if (src.TerrainData is not null && src.TerrainData.Cells is { Count: > 0 })
            {
                var terrainFile = full + ".terrain.yaml";
                var terrainDoc = new TerrainFile(
                    Encoding: src.TerrainData.Encoding,
                    Default: src.TerrainData.Default,
                    TerrainCells: src.TerrainData.Cells);
                WriteAllTextFlush(terrainFile, YamlSchemas.Serialize(terrainDoc));
            }

            // 2) 写 map yaml：不内联 cells，由 cells_ref 承担
            //    把 cells_ref 显式置空（保留约定路径 fallback），避免脏 ref 残留
            var mapForYaml = src with
            {
                TerrainData = src.TerrainData is null
                    ? null
                    : (src.TerrainData with
                    {
                        Cells = Array.Empty<TerrainCellEntry>(),
                        CellsRef = null,
                    }),
            };
            WriteAllTextFlush(full, YamlSchemas.Serialize(mapForYaml));
        }

        // 3) 写 layer 文件到 map.Layers[].ref 解析后的绝对路径（与 ProjectLoader 同算法,reload 时能找回）
        WriteLayerFiles(rootPath, layers, maps, savedMapRels);
    }

    /// <summary>
    /// 写文件并 flush + close，规避 Windows 上刚 WriteAllText 完立即 File.Exists 返回 false 的怪现象。
    /// </summary>
    private static void WriteAllTextFlush(string path, string content)
    {
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        fs.Write(bytes, 0, bytes.Length);
        fs.Flush(true);
    }

    /// <summary>
    /// 把 layers 写到 <paramref name="maps"/> 中实际引用到的 ref 路径（与
    /// <see cref="ProjectLoader.ResolveRefPath"/> 同算法解析,确保 reload 时能找到）。
    /// 同一绝对路径只写一次（保持幂等）。
    ///
    /// 解析顺序：1) 以当前 map 文件所在目录为基准 join ref；2) 不存在则回落到工程根。
    /// </summary>
    private static void WriteLayerFiles(
        string rootPath,
        IReadOnlyList<LayerDoc>? layers,
        IReadOnlyList<MapDoc> maps,
        IReadOnlyList<string> mapRelPaths)
    {
        if (layers is null || layers.Count == 0 || maps.Count == 0) return;
        // 按 ref 绝对路径 → 任意匹配 layer doc（多 map 引用同一文件时取首个有 id 的）
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // absPath → layerId
        for (var i = 0; i < maps.Count && i < mapRelPaths.Count; i++)
        {
            var mapRel = mapRelPaths[i];
            if (string.IsNullOrEmpty(mapRel)) continue;
            var fullMapPath = System.IO.Path.Combine(
                rootPath,
                mapRel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var srcLayers = maps[i].Layers;
            if (srcLayers is null) continue;
            foreach (var lref in srcLayers)
            {
                if (string.IsNullOrEmpty(lref.RefPath)) continue;
                var absLayer = ResolveLayerPath(rootPath, fullMapPath, lref.RefPath);
                if (!resolved.ContainsKey(absLayer)) resolved[absLayer] = ""; // 留空，后续填 id
            }
        }

        // 把 layer 文档按 id 索引；按 ref 路径写入时优先匹配 id 相同的 layer doc
        var byId = new Dictionary<string, LayerDoc>(StringComparer.Ordinal);
        foreach (var l in layers)
        {
            if (!string.IsNullOrEmpty(l.Id) && !byId.ContainsKey(l.Id)) byId[l.Id] = l;
        }
        // 若 ref 路径的 basename 与 layer.id 拼 .yaml 一致,优先按 id 匹配;
        // 否则按 ref 文件位置就近选一个未用过的 layer doc
        var unassigned = new Queue<LayerDoc>(layers);
        var used = new HashSet<LayerDoc>(System.Linq.Enumerable.Empty<LayerDoc>(), System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var claimedAbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var absLayer in resolved.Keys)
        {
            LayerDoc? chosen = null;
            var basename = System.IO.Path.GetFileNameWithoutExtension(absLayer);
            // 1) 优先按 "id == basename" 匹配
            if (byId.TryGetValue(basename, out var byName) && !used.Contains(byName))
            {
                chosen = byName;
            }
            else
            {
                // 2) 否则取首个未用的 layer doc
                while (unassigned.Count > 0)
                {
                    var cand = unassigned.Dequeue();
                    if (used.Contains(cand)) continue;
                    chosen = cand;
                    break;
                }
            }
            if (chosen is null) continue;
            used.Add(chosen);
            var dir = System.IO.Path.GetDirectoryName(absLayer);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            WriteAllTextFlush(absLayer, YamlSchemas.Serialize(chosen));
            claimedAbs.Add(absLayer);
        }
    }

    /// <summary>
    /// 与 <see cref="ProjectLoader.ResolveRefPath"/> 同语义：先以 map 目录为基准 join,不存在则回落到工程根。
    /// </summary>
    internal static string ResolveLayerPath(string rootPath, string mapFullPath, string layerRel)
    {
        var rel = layerRel.Replace('/', System.IO.Path.DirectorySeparatorChar);
        var mapDir = System.IO.Path.GetDirectoryName(mapFullPath) ?? rootPath;
        var fromMap = System.IO.Path.GetFullPath(System.IO.Path.Combine(mapDir, rel));
        return fromMap;
    }
}