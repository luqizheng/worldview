using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace WorldView.Storage.Yaml;

/// <summary>
/// 单条瓦片风格（plan Task 14 / L456）。
///
/// <para><see cref="Path"/> 是 <c>assets/textures/tiles/</c> 目录下的相对片段；
/// <c>.</c> 表示该目录自身，其它值表示子目录（或某个资源文件名）。</para>
/// </summary>
public sealed record TileSetStyle(string Id, string Path, string Description);

/// <summary>
/// 瓦片风格注册器（plan Task 14 / L457-461，契约见 L442-453 的 <c>styles.yaml</c>）。
///
/// <para><b>纯逻辑层</b>：位于 <c>scripts/Storage/**</c>，**不引用 <c>Godot</c>**，<see cref="AssetPathFor"/>
/// 只拼字符串、不做任何 <c>ResourceLoader</c> 解析，因此可脱离引擎单测。</para>
///
/// <para><b>不读盘</b>：构造器只接受 YAML 文本（或 <see cref="Stream"/>），路径解析与文件读取留给
/// Godot 感知层 <c>WorldView.Editor.TileSetLoader</c>（plan L468 备选方案，见 R25）。</para>
///
/// <para><b>校验</b>：<c>styles</c> 缺失 / 非列表 / 为空、风格项缺 <c>id</c> 或 <c>path</c>、
/// <c>default</c> 缺失或不在 <c>styles</c> 中 —— 一律抛 <see cref="InvalidDataException"/>；
/// 查询未知 id 抛 <see cref="ArgumentException"/>（plan L474 二选一取后者）。
/// <c>version</c> 本轮只容忍存在、不做版本分支（首版唯一取值 <c>1</c>）。</para>
/// </summary>
public sealed class TileSetStyleRegistry
{
    /// <summary><c>res://</c> 下的瓦片资产根目录（plan L448 / L451 的相对基准）。</summary>
    private const string StylesRoot = "res://assets/textures/tiles";

    /// <summary>按 YAML 出现顺序排列的风格表（plan L458）。</summary>
    public IReadOnlyList<TileSetStyle> Styles { get; private set; } = Array.Empty<TileSetStyle>();

    /// <summary>YAML <c>default:</c> 字段（plan L459）；构造时已校验其存在于 <see cref="Styles"/>。</summary>
    public string DefaultId { get; private set; } = string.Empty;

    /// <summary>从 YAML 文本构造（不读盘）。</summary>
    public TileSetStyleRegistry(string yamlText)
    {
        ArgumentNullException.ThrowIfNull(yamlText);
        Parse(yamlText);
    }

    /// <summary>从 YAML 流构造（不读盘；流不负责关闭）。</summary>
    public TileSetStyleRegistry(Stream yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        using var reader = new StreamReader(yaml, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        Parse(reader.ReadToEnd());
    }

    /// <summary>
    /// 某风格的 <c>res://</c> 资产路径（plan L460）：<c>res://assets/textures/tiles/{path}</c>。
    /// 只拼字符串，**不**触发 <c>ResourceLoader</c>；未知 id 抛 <see cref="ArgumentException"/>。
    /// </summary>
    public string AssetPathFor(string id)
    {
        var style = Styles.FirstOrDefault(s => s.Id == id);
        if (style is null)
        {
            throw new ArgumentException(
                $"未知的瓦片风格 id：'{id}'（可用：{string.Join("、", Styles.Select(s => s.Id))}）",
                nameof(id));
        }

        return $"{StylesRoot}/{style.Path}";
    }

    // ------------------------------------------------------------------ 解析

    private void Parse(string yamlText)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yamlText));
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"styles.yaml 解析失败：{ex.Message}", ex);
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException("styles.yaml 解析失败：根节点必须是映射（version / default / styles）。");
        }

        var styles = new List<TileSetStyle>();
        if (root.Children.TryGetValue(new YamlScalarNode("styles"), out var stylesNode))
        {
            if (stylesNode is not YamlSequenceNode sequence)
            {
                throw new InvalidDataException("styles.yaml 解析失败：styles 必须是列表。");
            }

            foreach (var item in sequence.Children)
            {
                if (item is not YamlMappingNode mapping)
                {
                    throw new InvalidDataException("styles.yaml 解析失败：styles 的每一项必须是映射（id / path / description）。");
                }

                var id = Scalar(mapping, "id");
                var path = Scalar(mapping, "path");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(path))
                {
                    throw new InvalidDataException($"styles.yaml 解析失败：风格项缺少 id 或 path（id='{id}'，path='{path}'）。");
                }

                styles.Add(new TileSetStyle(id, path, Scalar(mapping, "description")));
            }
        }

        if (styles.Count == 0)
        {
            throw new InvalidDataException("styles.yaml 解析失败：styles 不能为空（至少一条风格）。");
        }

        var defaultId = Scalar(root, "default");
        if (string.IsNullOrWhiteSpace(defaultId))
        {
            throw new InvalidDataException("styles.yaml 解析失败：缺少 default。");
        }

        if (!styles.Any(s => s.Id == defaultId))
        {
            throw new InvalidDataException($"styles.yaml 解析失败：default='{defaultId}' 不在 styles 中。");
        }

        Styles = styles.ToArray();
        DefaultId = defaultId;
    }

    private static string Scalar(YamlMappingNode mapping, string key) =>
        mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value ?? string.Empty
            : string.Empty;
}
