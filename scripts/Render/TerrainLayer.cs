using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 地形图层（见 tasks.md T14）。
///
/// 世界坐标 ↔ 网格坐标由 <see cref="CoordTransform"/> 统一负责；
/// 本层只做"网格坐标 + 地形类型 → 瓦片"的映射。
/// </summary>
public partial class TerrainLayer : TileMapLayer
{
    /// <summary>一格在画布上的像素边长（= <see cref="TerrainTileSetFactory.TilePixels"/>）。</summary>
    public float CellPixels { get; private set; } = TerrainTileSetFactory.TilePixels;

    /// <summary>绑定共用 TileSet 并登记每格像素尺寸；由 <c>WorldCanvas</c> 在地图切换时调用。</summary>
    public void Configure(TileSet tileSet, float cellPixels)
    {
        TileSet = tileSet;
        CellPixels = cellPixels;
    }

    /// <summary>写入一格地形瓦片。</summary>
    public void SetTerrainCell(Vector2I cell, TerrainType type)
    {
        SetCell(
            cell,
            TerrainTileSetFactory.SourceId,
            TerrainTileSetFactory.AtlasCoordsOf(type));
    }

    /// <summary>用文档中的 sparse 单元重建整层（先清空）。</summary>
    public void Rebuild(MapDoc map)
    {
        Clear();

        var defaultType = map.TerrainData.Default?.Type ?? TerrainType.Plain;

        if (map.TerrainData.Cells is null)
        {
            return;
        }

        foreach (var entry in map.TerrainData.Cells)
        {
            SetTerrainCell(new Vector2I(entry.At.X, entry.At.Y), entry.Type);
        }

        // 默认地形不写入瓦片，避免 1024×1024 地图产生 100 万个节点级瓦片。
        _ = defaultType;
    }

    /// <summary>已绘制瓦片数（T22 视口剔除验收指标）。</summary>
    
    private void FillDefault(MapDoc map, TerrainType defaultType)
    {
        var w = map.Grid.Width;
        var h = map.Grid.Height;
        if (w <= 0 || h <= 0 || w * h > 4096) return; // MVP 保护
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                SetTerrainCell(new Vector2I(x, y), defaultType);
            }
        }
    }public int GetDrawnCellCount() => GetUsedCells().Count;
}
