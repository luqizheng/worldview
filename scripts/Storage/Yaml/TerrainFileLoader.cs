using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 稀疏地形 cells 文件的 YAML 包装结构。
/// 实际文件布局：<c>encoding + default + terrain_cells: [...]</c>。
/// </summary>
public sealed record TerrainFile(
    string Encoding = "sparse",
    TerrainCell? Default = null,
    [property: YamlDotNet.Serialization.YamlMember(Alias = "cells")] IReadOnlyList<TerrainCellEntry> TerrainCells = null!)
{
    public TerrainFile() : this("sparse", null, System.Array.Empty<TerrainCellEntry>()) { }
}

/// <summary>
/// 地形稀疏 cells 文件加载辅助（供未来编辑器单独加载；MVP 由 <see cref="ProjectLoader"/> 一并加载）。
/// </summary>
public static class TerrainFileLoader
{
    /// <summary>
    /// 加载单个 <c>*.terrain.yaml</c> 文件，返回其中的 <c>terrain_cells</c> 列表。
    /// </summary>
    public static IReadOnlyList<TerrainCellEntry> Load(string terrainFilePath)
    {
        if (string.IsNullOrEmpty(terrainFilePath)) throw new ArgumentException("path 不可为空", nameof(terrainFilePath));
        if (!System.IO.File.Exists(terrainFilePath)) throw new FileNotFoundException($"地形文件不存在: {terrainFilePath}");
        var yaml = System.IO.File.ReadAllText(terrainFilePath);
        return YamlSchemas.Deserialize<TerrainFile>(yaml).TerrainCells ?? new System.Collections.Generic.List<TerrainCellEntry>();
    }
}