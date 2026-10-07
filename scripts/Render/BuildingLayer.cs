using System.Collections.Generic;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 建筑图层（见 tasks.md T14）。
///
/// MVP 以矩形占位呈现建筑，不做真实轮廓（贴合属 N11 范围外）。
/// 用 <c>_Draw</c> 直接绘制而非每栋一个 <c>Polygon2D</c> 子节点：
/// 建筑数量大时节点开销显著，绘制调用更轻。
/// </summary>
public partial class BuildingLayer : Node2D
{
    private readonly List<Building> _buildings = new();

    [Export]
    public float CellSize { get; set; } = 32f;

    [Export]
    public Color FillColor { get; set; } = new(0.55f, 0.35f, 0.22f, 0.9f);

    [Export]
    public Color OutlineColor { get; set; } = new(0.15f, 0.1f, 0.06f, 1f);

    public int BuildingCount => _buildings.Count;

    /// <summary>用文档中的建筑列表重建（仅绘制属于当前地图的建筑）。</summary>
    public void Rebuild(MapDoc map)
    {
        _buildings.Clear();

        if (map.Buildings is not null)
        {
            _buildings.AddRange(map.Buildings);
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var building in _buildings)
        {
            var origin = new Vector2(building.At.X * CellSize, building.At.Y * CellSize);
            var rect = new Rect2(origin, new Vector2(CellSize, CellSize));

            DrawRect(rect, FillColor, filled: true);
            DrawRect(rect, OutlineColor, filled: false, width: 1f);
        }
    }
}
