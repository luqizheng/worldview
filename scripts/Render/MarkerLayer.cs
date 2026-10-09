// scripts/Render/MarkerLayer.cs
//
// §4.9.2 标记与图标图层。
//
// 双层数据契约：
//   1. MapDoc.CityRefs  (已有) —— 7 帧 settlement marker (village / town / capital /
//      ruin_site / quest_marker / portal_arch / faction_marker)，
//      走 icons_atlas.png (PixiJS spritesheet 协议)，用 Sprite2D 渲染。
//   2. MapDoc.Markers   (新增，§4.9.2) —— 6 类 marker
//      (event_point / task_marker / danger_point / character_dot /
//      vehicle_icon / army_group)，
//      走 assets/icons/markers/*.png 独立 sprite，按 JRPG 像素画 6 个独立 Sprite2D。
//
// 渲染实现：所有 marker 节点为 Sprite2D 子节点,Rebuild 时按
// CellSize 缩放(Z = atlas_size / cellSize 系数)。City marker 沿用旧 _Draw
// 风格时同步仍可见——本轮把 city 改用 icons_atlas 帧渲染(读取 assets/icons/icons_atlas.json 帧坐标)。
//
// 接入：WorldCanvas 调 Rebuild(map) 时传入完整 MapDoc；
//       ApplyMode("edit" | "preview") 控制显隐。

using System.Collections.Generic;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

public partial class MarkerLayer : Node2D
{
    // §4.9.2 6 类 marker 资源路径
    private const string MarkerDir = "res://assets/icons/markers/";
    private static readonly Dictionary<string, string> MarkerTexturePaths = new()
    {
        { "event_point",   MarkerDir + "event_point.png" },
        { "task_marker",   MarkerDir + "task_marker.png" },
        { "danger_point",  MarkerDir + "danger_point.png" },
        { "character_dot", MarkerDir + "character_dot.png" },
        { "vehicle_icon",  MarkerDir + "vehicle_icon.png" },
        { "army_group",    MarkerDir + "army_group.png" },
    };

    // §4.9.2 sprite 真实像素尺寸（裁剪后）
    private static readonly Dictionary<string, int> MarkerSpriteSize = new()
    {
        { "event_point",   16 },
        { "task_marker",   16 },
        { "danger_point",  16 },
        { "character_dot", 8  },
        { "vehicle_icon",  12 },
        { "army_group",    16 },
    };

    [Export]
    public float CellSize { get; set; } = 32f;

    [Export]
    public float HitRadius { get; set; } = 14f;

    // 旧 _Draw 风格:city marker 同心圆占位(同先前实现,保留)
    [Export]
    public Color RingColor { get; set; } = new(0.85f, 0.75f, 0.35f, 0.95f);

    [Export]
    public Color CoreColor { get; set; } = new(0.6f, 0.15f, 0.1f, 0.95f);

    // 6 marker 节点缓存:每类一个 Sprite2D 模板，clone 渲染
    private readonly Dictionary<string, Sprite2D> _templates = new();
    private readonly List<Sprite2D> _citySprites = new();
    private readonly List<Sprite2D> _markerSprites = new();
    private readonly List<CityRef> _cities = new();

    public int MarkerCount => _cities.Count;

    public override void _Ready()
    {
        ZIndex = 5;
        // 预加载 6 marker template (1 次,后续 clone)
        // 用 Image.Load 绕过 .import 缺失,直接构造 ImageTexture
        foreach (var (kind, path) in MarkerTexturePaths)
        {
            Image? img = null;
            if (ResourceLoader.Exists(path))
            {
                img = ResourceLoader.Load<Image>(path);
            }
            else
            {
                img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            }
            if (img is null) continue;
            var tex = ImageTexture.CreateFromImage(img);
            var sprite = new Sprite2D { Texture = tex, Visible = false };
            _templates[kind] = sprite;
        }
    }

    /// <summary>用当前地图的 city_refs + markers 重建。</summary>
    public void Rebuild(MapDoc map)
    {
        // 1. 清旧
        foreach (var s in _citySprites) s.QueueFree();
        foreach (var s in _markerSprites) s.QueueFree();
        _citySprites.Clear();
        _markerSprites.Clear();
        _cities.Clear();

        // 2. 渲染 city marker (旧 _Draw 风格占位)
        if (map.CityRefs is not null)
        {
            _cities.AddRange(map.CityRefs);
        }
        QueueRedraw();

        // 3. 渲染 §4.9.2 6 marker
        if (map.Markers is not null)
        {
            foreach (var m in map.Markers)
            {
                AddMarkerSprite(m);
            }
        }
    }

    private void AddMarkerSprite(MarkerDoc m)
    {
        if (!_templates.TryGetValue(m.Kind, out var template)) return;
        var sprite = (Sprite2D)template.Duplicate();
        sprite.Position = MarkerPosition(m.X, m.Y);
        // 按 sprite 真实尺寸缩放:目标显示尺寸 = 2×cellSize/3 (16×16 sprite 在 cellSize=32 → 21.3px)
        if (MarkerSpriteSize.TryGetValue(m.Kind, out var srcSize))
        {
            var displaySize = CellSize * 0.66f;
            var scale = displaySize / srcSize;
            sprite.Scale = new Vector2(scale, scale);
        }
        sprite.Visible = true;
        AddChild(sprite);
        _markerSprites.Add(sprite);
    }

    private Vector2 MarkerPosition(int x, int y) =>
        new((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);

    public string? HitTest(Vector2 localPosition)
    {
        for (var i = _cities.Count - 1; i >= 0; i--)
        {
            if (MarkerPosition(_cities[i].X, _cities[i].Y).DistanceTo(localPosition) <= HitRadius)
            {
                return _cities[i].Id;
            }
        }
        return null;
    }

    public CityRef? FindCity(string cityId)
    {
        foreach (var city in _cities)
        {
            if (city.Id == cityId) return city;
        }
        return null;
    }

    public override void _Draw()
    {
        // 旧 _Draw 占位:city marker 仍用同心圆
        foreach (var city in _cities)
        {
            var center = MarkerPosition(city.X, city.Y);
            DrawCircle(center, HitRadius, RingColor);
            DrawCircle(center, HitRadius * 0.45f, CoreColor);
        }
    }
}
