using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 工程保存器（MVP 暂只导出 <c>project.yaml</c> 与 <c>maps/*/*.yaml</c>；<c>terrain_data.cells</c> M3+ 按 spec.md §1.2 N04 切 chunked,MVP 写回原文件）。
/// </summary>
public static class ProjectSaver
{
    /// <summary>
    /// 保存 <see cref="World"/> + <see cref="MapDoc"/>s 到 <paramref name="rootPath"/>。
    /// 不会重写 <c>layers/*.yaml</c> 与 <c>terrain_data.cells_ref</c> 文件。
    /// </summary>
    public static void Save(string rootPath, World world, IReadOnlyList<MapDoc> maps)
    {
        if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("rootPath 不可为空", nameof(rootPath));
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (!System.IO.Directory.Exists(rootPath)) throw new DirectoryNotFoundException($"工程根不存在: {rootPath}");

        var projectYaml = YamlSchemas.Serialize(world);
        System.IO.File.WriteAllText(System.IO.Path.Combine(rootPath, "project.yaml"), projectYaml);

        // 写回 maps/{world,city,ref}/*.yaml（保持与 map_index 顺序一致）
        if (world.MapIndex is null) return;
        for (var i = 0; i < maps.Count && i < world.MapIndex.Count; i++)
        {
            var rel = world.MapIndex[i].RefPath;
            if (string.IsNullOrEmpty(rel)) continue;
            var full = System.IO.Path.Combine(rootPath, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var dir = System.IO.Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            // 写 map 时不内联 cells（cells 由 cells_ref 文件承担）
            var mapForYaml = maps[i] with
                {
                        TerrainData = maps[i].TerrainData is null ? null : (maps[i].TerrainData! with { Cells = Array.Empty<TerrainCellEntry>() }),
                    };
            var yaml = YamlSchemas.Serialize(mapForYaml);
            System.IO.File.WriteAllText(full, yaml);
        }
    }
}