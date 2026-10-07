using System.Collections.Generic;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 城市标记图层（见 tasks.md T17 + D5）。
///
/// 绘制 <c>MapDoc.city_refs</c> 的城市标记；命中测试供 <c>WorldCanvas</c> 处理双击下钻。
/// MVP 用同心圆占位，不引入图标资产（视觉增强属 N10 范围外）。
/// </summary>
public partial class MarkerLayer : Node2D
{
    private readonly List<CityRef> _cities = new();

    [Export]
    public float CellSize { get; set; } = 32f;

    [Export]
    public float HitRadius { get; set; } = 14f;

    [Export]
    public Color RingColor { get; set; } = new(0.85f, 0.75f, 0.35f, 0.95f);

    [Export]
    public Color CoreColor { get; set; } = new(0.6f, 0.15f, 0.1f, 0.95f);

    public int MarkerCount => _cities.Count;

    /// <summary>用当前地图的 <c>city_refs</c> 重建标记。</summary>
    public void Rebuild(MapDoc map)
    {
        _cities.Clear();

        if (map.CityRefs is not null)
        {
            _cities.AddRange(map.CityRefs);
        }

        QueueRedraw();
    }

    /// <summary>把城市标记的格坐标换算成本层局部坐标（格中心）。</summary>
    private Vector2 MarkerPosition(CityRef city) =>
        new((city.X + 0.5f) * CellSize, (city.Y + 0.5f) * CellSize);

    /// <summary>
    /// 命中测试：返回鼠标（局部坐标）命中的城市 id，未命中返回 null。
    /// </summary>
    public string? HitTest(Vector2 localPosition)
    {
        // 由后往前，优先命中后绘制的标记。
        for (var i = _cities.Count - 1; i >= 0; i--)
        {
            if (MarkerPosition(_cities[i]).DistanceTo(localPosition) <= HitRadius)
            {
                return _cities[i].Id;
            }
        }

        return null;
    }

    /// <summary>按城市 id 取其 <c>map_ref</c> 解析出的地图 id；未找到返回 null。</summary>
    public CityRef? FindCity(string cityId)
    {
        foreach (var city in _cities)
        {
            if (city.Id == cityId)
            {
                return city;
            }
        }

        return null;
    }

    public override void _Draw()
    {
        foreach (var city in _cities)
        {
            var center = MarkerPosition(city);
            DrawCircle(center, HitRadius, RingColor);
            DrawCircle(center, HitRadius * 0.45f, CoreColor);
        }
    }
}
