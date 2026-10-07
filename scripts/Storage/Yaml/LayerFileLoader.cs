using WorldView.Domain;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 图层文件加载辅助（供未来编辑器单独加载图层；MVP 由 <see cref="ProjectLoader"/> 一并加载）。
/// </summary>
public static class LayerFileLoader
{
    /// <summary>
    /// 加载单个 <c>layers/&lt;kind&gt;.yaml</c> 文件。
    /// </summary>
    public static LayerDoc Load(string layerFilePath)
    {
        if (string.IsNullOrEmpty(layerFilePath)) throw new ArgumentException("path 不可为空", nameof(layerFilePath));
        if (!System.IO.File.Exists(layerFilePath)) throw new FileNotFoundException($"图层文件不存在: {layerFilePath}");
        var yaml = System.IO.File.ReadAllText(layerFilePath);
        return YamlSchemas.Deserialize<LayerDoc>(yaml);
    }
}