using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 道路图层（见 tasks.md T14）。
///
/// MVP 以瓦片方式呈现道路格；M3+ 再切换为 <c>Line2D</c> 折线真实路网（spec.md §3.3）。
/// 与 <see cref="TerrainLayer"/> 共用同一 TileSet，只是取不同的图集坐标。
/// </summary>
public partial class RoadLayer : TileMapLayer
{
    /// <summary>道路瓦片使用的图集列（复用地形图集，取 Hill 位置的暖色块作占位）。</summary>
    private static readonly Vector2I RoadAtlasCoords = TerrainTileSetFactory.AtlasCoordsOf(TerrainType.Desert);

    public float CellPixels { get; private set; } = TerrainTileSetFactory.TilePixels;

    public void Configure(TileSet tileSet, float cellPixels)
    {
        TileSet = tileSet;
        CellPixels = cellPixels;
    }

    public void SetRoadCell(Vector2I cell)
    {
        SetCell(cell, TerrainTileSetFactory.SourceId, RoadAtlasCoords);
    }

    public void Rebuild(MapDoc map)
    {
        Clear();
        // MVP 道路数据仍在 road 图层文件中（M3 落地），此处保持空层即可。
        _ = map;
    }

    public int GetDrawnCellCount() => GetUsedCells().Count;
}
