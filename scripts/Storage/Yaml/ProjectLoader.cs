using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 工程加载器（核心入口之一）。
/// MVP 流程：
/// <list type="number">
///   <item>读 <c>{rootPath}/project.yaml</c> → <see cref="World"/></item>
///   <item>遍历 <c>map_index[].ref</c>，依次加载每个 <c>MapDoc</c></item>
///   <item>遍历每个 <c>MapDoc.layers[].ref</c>，加载 <see cref="LayerDoc"/></item>
///   <item>遍历每个 <c>MapDoc.terrain_data.cells_ref</c>，加载稀疏地形 cells</item>
///   <item>调用 <c>ProjectValidator + MapValidator</c>，把错误聚合进 <see cref="LoadResult"/></item>
/// </list>
/// </summary>
public static class ProjectLoader
{
    /// <summary>
    /// 单次加载结果：World + 所有 MapDoc + 所有 LayerDoc + 校验结果。
    /// MVP 暂不包含 Characters（M5 落地，按 checklist B 暂只校验 ref 文件存在）。
    /// </summary>
    public sealed record LoadResult(
        World World,
        IReadOnlyList<MapDoc> Maps,
        IReadOnlyList<LayerDoc> Layers,
        WorldView.Domain.Validation.ValidationResult Validation);

    /// <summary>
    /// 加载工程根 <c>project.yaml</c> + 所有索引项。
    /// </summary>
    /// <param name="rootPath">工程根绝对路径（包含 <c>project.yaml</c>）。</param>
    public static LoadResult Load(string rootPath)
    {
        if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("rootPath 不可为空", nameof(rootPath));
        if (!System.IO.Directory.Exists(rootPath)) throw new DirectoryNotFoundException($"工程根不存在: {rootPath}");

        var projectYamlPath = System.IO.Path.Combine(rootPath, "project.yaml");
        if (!System.IO.File.Exists(projectYamlPath))
            throw new FileNotFoundException($"project.yaml 未在工程根找到: {rootPath}");

        var yaml = System.IO.File.ReadAllText(projectYamlPath);
        var world = YamlSchemas.Deserialize<World>(yaml);

        // 加载 MapDoc 列表
        var maps = new List<MapDoc>();
        var layers = new List<LayerDoc>();
        var allErrors = new List<WorldView.Domain.Validation.ValidationError>();
        var allWarnings = new List<WorldView.Domain.Validation.ValidationError>();

        if (world.MapIndex is not null)
        {
            foreach (var entry in world.MapIndex)
            {
                if (string.IsNullOrEmpty(entry.RefPath)) continue;
                var fullMapPath = System.IO.Path.Combine(rootPath, entry.RefPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(fullMapPath)) continue; // ProjectValidator 已经报错,这里跳过

                var mapYaml = System.IO.File.ReadAllText(fullMapPath);
                var map = YamlSchemas.Deserialize<MapDoc>(mapYaml);
                maps.Add(map);

                // 加载 layer refs
                if (map.Layers is not null)
                {
                    foreach (var lref in map.Layers)
                    {
                        if (string.IsNullOrEmpty(lref.RefPath)) continue;
                        var layerRel = lref.RefPath.Replace('/', System.IO.Path.DirectorySeparatorChar);
                        var layerFullPath = ResolveRefPath(rootPath, fullMapPath, layerRel);
                        if (!System.IO.File.Exists(layerFullPath)) continue;
                        var layerYaml = System.IO.File.ReadAllText(layerFullPath);
                        var layer = YamlSchemas.Deserialize<LayerDoc>(layerYaml);
                        layers.Add(layer);
                    }
                }

                // 加载 terrain_data.cells_ref（先记录原索引，再替换）
                var mapIdx = maps.Count - 1;
                if (map.TerrainData is not null && !string.IsNullOrEmpty(map.TerrainData.CellsRef))
                {
                    var cellsRel = map.TerrainData.CellsRef.Replace('/', System.IO.Path.DirectorySeparatorChar);
                    var cellsFullPath = ResolveRefPath(rootPath, fullMapPath, cellsRel);
                    if (System.IO.File.Exists(cellsFullPath))
                    {
                        var cells = TerrainFileLoader.Load(cellsFullPath);
                        map = map with { TerrainData = map.TerrainData with { Cells = cells, CellsRef = null } };
                        maps[mapIdx] = map; // map 是 record（值相等）,IndexOf 也能找到,但直接用索引更稳
                    }
                }
            }
        }

        // 校验
        var projectValidator = new WorldView.Domain.Validation.ProjectValidator();
        var mapValidator = new WorldView.Domain.Validation.MapValidator();
        var projResult = projectValidator.Validate(world, "project.yaml", rootPath);
        // map_index 第 i 项对应 maps[i]（保持同序）
        // path 给 MapValidator 仅用于错误信息显示（map_rel 形式），所以保留 mapRel；
        // ResolveRefPath 内部按 rootPath 解析。
        for (var i = 0; i < maps.Count && i < (world.MapIndex?.Count ?? 0); i++)
        {
            var mapRel = world.MapIndex![i].RefPath ?? "unknown.yaml";
            var mapResult = mapValidator.Validate(maps[i], mapRel, rootPath);
            allErrors.AddRange(mapResult.Errors);
            allWarnings.AddRange(mapResult.Warnings);
        }
        allErrors.AddRange(projResult.Errors);
        allWarnings.AddRange(projResult.Warnings);

        var finalResult = new WorldView.Domain.Validation.ValidationResult(
            allErrors.OrderBy(e => e.Code, StringComparer.Ordinal).ToArray(),
            allWarnings.OrderBy(e => e.Code, StringComparer.Ordinal).ToArray());

        return new LoadResult(world, maps, layers, finalResult);
    }

    /// <summary>
    /// 把 <c>ref</c> 字段解析为绝对路径。
    /// 规则（按 <c>存储与目录规范 §3</c>）：
    /// <list type="number">
    ///   <item>优先尝试以当前 map 文件所在目录为基准 join ref（spec 写 ref 时一般
    ///         表达"相对当前文件"，例如 <c>../../layers/terrain.yaml</c>）</item>
    ///   <item>若不存在,fallback 到以工程根为基准 join ref（兼容把 ref 写为相对工程根的写法）</item>
    ///   <item>都不存在时,以工程根 join 的版本作为返回值（让上层 File.Exists 报错）</item>
    /// </list>
    /// </summary>
    internal static string ResolveRefPath(string rootPath, string mapFullPath, string relRef)
    {
        var mapDir = System.IO.Path.GetDirectoryName(mapFullPath) ?? rootPath;
        var fromMap = System.IO.Path.GetFullPath(System.IO.Path.Combine(mapDir, relRef));
        if (System.IO.File.Exists(fromMap))
        {
            return fromMap;
        }
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(
            rootPath,
            relRef.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }
}