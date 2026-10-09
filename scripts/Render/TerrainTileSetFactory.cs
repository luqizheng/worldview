using System;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 运行期构建地形 TileSet（见 tasks.md T8 / T14）。
///
/// 设计取舍：MVP 不引入外部 PNG 图集与 <c>.import</c> 元数据，而是用代码生成
/// 11 个 <see cref="TerrainType"/> 对应的纯色瓦片，装进同一个 <see cref="TileSet"/> 实例。
/// 这样"所有 TileMapLayer 共用同一 TileSet"的约束依然成立，且工程无需二进制资产、
/// 可在 <c>--headless</c> 下工作。
///
/// 后续接入美术贴图时，仅需替换本工厂，图层代码不变。
///
/// <para>T14 起本工厂还兼任**兜底**：<c>WorldView.Editor.TileSetLoader</c> 在 <c>styles.yaml</c>
/// 指到的风格目录里找不到 TileSet 资源（或解析失败）时，一律回落到 <see cref="Build"/>，
/// 因此 <see cref="Build"/> 必须保持「不依赖任何外部资产、可独立调用」。</para>
/// </summary>
public static class TerrainTileSetFactory
{
    /// <summary>地形瓦片的 TileSet source id。</summary>
    public const int SourceId = 0;

    /// <summary>
    /// 单块瓦片像素边长（= 1 个世界单位，见 <see cref="CoordTransform.PixelsPerUnit"/>）。
    /// 与 <c>res://assets/textures/tiles/tileset.tres</c> 的 <c>tile_size</c> 保持一致（64）。
    /// 资源真源：<c>examples/sample_world/assets/textures/tiles/terrain_atlas.png</c>（64×64 帧）。
    /// </summary>
    public const int TilePixels = 64;

    /// <summary>图集列顺序；索引即 <c>atlas_coords.x</c>。</summary>
    private static readonly TerrainType[] Order =
    {
        TerrainType.Plain,
        TerrainType.Mountain,
        TerrainType.Forest,
        TerrainType.Hill,
        TerrainType.Water,
        TerrainType.Desert,
        TerrainType.Swamp,
        TerrainType.Tundra,
        TerrainType.Basin,
        TerrainType.Coast,
        TerrainType.River,
    };

    private static TileSet? _cached;

    /// <summary>把地形类型映射到图集坐标。</summary>
    public static Vector2I AtlasCoordsOf(TerrainType type)
    {
        var index = Array.IndexOf(Order, type);
        return new Vector2I(index < 0 ? 0 : index, 0);
    }

    /// <summary>构建（并缓存）共用 TileSet。</summary>
    public static TileSet Build()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var image = Image.CreateEmpty(TilePixels * Order.Length, TilePixels, false, Image.Format.Rgba8);

        for (var i = 0; i < Order.Length; i++)
        {
            var color = new Color(TerrainDefaults.ColorHex(Order[i]));
            image.FillRect(new Rect2I(i * TilePixels, 0, TilePixels, TilePixels), color);
        }

        var texture = ImageTexture.CreateFromImage(image);

        var atlas = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(TilePixels, TilePixels),
        };

        for (var i = 0; i < Order.Length; i++)
        {
            atlas.CreateTile(new Vector2I(i, 0));
        }

        var tileSet = new TileSet
        {
            TileSize = new Vector2I(TilePixels, TilePixels),
        };

        tileSet.AddSource(atlas, SourceId);
        _cached = tileSet;
        return tileSet;
    }

    /// <summary>丢弃缓存（用于测试或热重载）。</summary>
    public static void Reset() => _cached = null;
}
