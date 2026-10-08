using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldView.Render;
using WorldView.Storage.Yaml;

namespace WorldView.Editor;

/// <summary>
/// 瓦片风格的 **Godot 感知层**（plan Task 14 / L462-468）：把纯逻辑注册器
/// <see cref="TileSetStyleRegistry"/> 与引擎资源系统接起来。
///
/// <para><b>位置</b>：plan L462 建议放在 <c>scripts/Render/TileSetRegistry.cs</c>，L468 给了备选位置；
/// 本实现按 R25 采用备选 —— 整个计划的 Global Constraint 只批准「<c>scripts/Render/**</c> 仅可修改
/// <c>WorldCanvas.cs</c>」，在其中新建文件会违反该约束，而 <c>scripts/Editor/**</c> 本就是
/// <c>EditorContext</c> / <c>EditorController</c> / <c>MapSizing</c> 等 Godot 感知层的家。</para>
///
/// <para><b>永不抛出</b>：任何解析失败 / 未知 id / 资源缺失 / 加载失败，都退化为
/// <see cref="TerrainTileSetFactory.Build"/> 的纯色块，并向 stderr 打印一行
/// <c>WARN: tileset '{id}' 加载失败，退化到纯色块</c>（spec 1 §10：UI 绝不因资产问题崩）。</para>
///
/// <para><b>本轮现状</b>：仓库内还没有真正的 TileSet 资源（<c>assets/textures/tiles/</c> 下只有
/// <c>styles.yaml</c>），所以 <see cref="Get"/> 目前**总是**走兜底分支 —— 即渲染结果与 Task 14 之前
/// 完全一致。等美术把 <c>tileset.tres</c> 放进对应目录，同一份代码就会真正加载它。</para>
/// </summary>
public static class TileSetLoader
{
    /// <summary><c>styles.yaml</c> 在工程内的固定位置（plan L442-453 契约）。</summary>
    public const string StylesYamlPath = "res://assets/textures/tiles/styles.yaml";

    /// <summary>风格目录内的 TileSet 资源文件名约定（当前尚无任何目录满足它）。</summary>
    private const string TileSetResourceName = "tileset.tres";

    /// <summary><c>styles.yaml</c> 读不到时的内置兜底表：只有纯色块一项，保证 UI 不崩。</summary>
    private const string BuiltInFallbackYaml =
        "version: 1\n" +
        "default: _fallback_solid_color\n" +
        "styles:\n" +
        "  - id: _fallback_solid_color\n" +
        "    path: solid\n" +
        "    description: 纯色兜底（无资产时退化）\n";

    private static readonly Dictionary<string, TileSet> Cache = new(StringComparer.Ordinal);
    private static TileSetStyleRegistry? _registry;

    /// <summary>已加载的风格注册器（惰性、只读）。</summary>
    public static TileSetStyleRegistry Registry => _registry ??= LoadRegistry();

    /// <summary>下拉框可选的风格 id 列表，顺序与 <c>styles.yaml</c> 一致。</summary>
    public static IReadOnlyList<string> AvailableIds => Registry.Styles.Select(s => s.Id).ToArray();

    /// <summary><c>styles.yaml</c> 的 <c>default:</c>。</summary>
    public static string DefaultId => Registry.DefaultId;

    /// <summary>
    /// 取某风格的 <see cref="TileSet"/>（带缓存）。未知 id、资源缺失或加载失败都返回纯色兜底，
    /// 不会抛异常。
    /// </summary>
    public static TileSet Get(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return Fallback(id);
        }

        if (Cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var tileSet = LoadFromAsset(id);
        Cache[id] = tileSet;
        return tileSet;
    }

    /// <summary>预热默认风格（由 <c>EditorContext._Ready</c> 调用一次，避免首帧加载卡顿）。</summary>
    public static void WarmUp() => _ = Get(DefaultId);

    // ------------------------------------------------------------------ 内部

    private static TileSet LoadFromAsset(string id)
    {
        try
        {
            var path = Registry.AssetPathFor(id);
            var trimmed = path.TrimEnd('/', '.');
            foreach (var candidate in new[] { path, $"{trimmed}/{TileSetResourceName}" })
            {
                if (ResourceLoader.Exists(candidate) && ResourceLoader.Load(candidate) is TileSet loaded)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // 未知 id / 资源解析异常：统一走兜底（不向 UI 抛）。
        }

        return Fallback(id);
    }

    private static TileSet Fallback(string id)
    {
        GD.PushWarning($"WARN: tileset '{id}' 加载失败，退化到纯色块");
        return TerrainTileSetFactory.Build();
    }

    private static TileSetStyleRegistry LoadRegistry()
    {
        try
        {
            // 用 Godot 的 FileAccess 走 res://（不碰 System.IO）；System.IO.FileAccess 是枚举，需全限定。
            if (Godot.FileAccess.FileExists(StylesYamlPath))
            {
                var text = Godot.FileAccess.GetFileAsString(StylesYamlPath);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return new TileSetStyleRegistry(text);
                }
            }
        }
        catch (Exception)
        {
            // 落到内置兜底。
        }

        GD.PushWarning($"WARN: 未读到可用的 {StylesYamlPath}，退化到内置纯色风格表");
        return new TileSetStyleRegistry(BuiltInFallbackYaml);
    }
}
