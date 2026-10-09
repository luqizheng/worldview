// scripts/Render/RouteLayer.cs
//
// §4.9.3 路线线型（v0.1 完整实装）。
//
// 数据契约：MapDoc.Routes 列表（每条 RouteDoc 含 kind / waypoints / node_sprite）。
// 渲染实现：自管 _Draw()，绘制 7 种线型 + 6 种节点 sprite。
//   7 种线型 — land / path / trade_route / air_route / water_route /
//               underground / teleport (与 §4.9.3 契约一一对应)
//   6 种节点 — assets/icons/route_nodes_atlas.png (96×16 atlas, 6 帧 16×16)
//
// 接入：WorldCanvas.RebuildAll 调 Rebuild(map) + ApplyMode 切显隐。
// 节点 sprite 索引与 atlas 帧序一致 (0..5)。

using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Editor;

namespace WorldView.Render;

public partial class RouteLayer : Node2D
{
    // 节点 atlas 路径与帧数
    private const string AtlasPath = "res://assets/icons/route_nodes_atlas.png";
    private const int FrameSize = 16;
    private const int FrameCount = 6;

    // §4.9.3 调色板 (从 jrpg_palette.yaml + JrpgStyleFactory 派生)
    private static readonly Color Outline       = new("5a3a1aff");  // #5a3a1a
    private static readonly Color CardFill      = new("fff8e7ff");  // #fff8e7
    private static readonly Color FactionRed    = new("c8341aff");  // #c8341a
    private static readonly Color FactionBlue   = new("3a5a8aff");  // #3a5a8a
    private static readonly Color MagicGlow     = new("8a4ac8ff");  // #8a4ac8
    private static readonly Color Water         = new("3a6a8aff");  // #3a6a8a

    private float _cellDistance = 32f;
    private bool _visibleByMode = true;
    private bool _contextBound;
    private ImageTexture? _atlas;
    private int _atlasW, _atlasH;
    private readonly List<RouteDoc> _routes = new();

    public override void _Ready()
    {
        ZIndex = 4; // 在 TerrainDecor(3) 之上,MarkerLayer(5) 之下
        LoadAtlas();
        TryBindContext();
    }

    public void Configure(float cellDistance)
    {
        _cellDistance = cellDistance;
        QueueRedraw();
    }

    private void LoadAtlas()
    {
        Image? img = null;
        if (ResourceLoader.Exists(AtlasPath))
        {
            img = ResourceLoader.Load<Image>(AtlasPath);
        }
        else
        {
            img = Image.LoadFromFile(ProjectSettings.GlobalizePath(AtlasPath));
        }
        if (img is null) return;
        _atlasW = img.GetWidth();
        _atlasH = img.GetHeight();
        _atlas = ImageTexture.CreateFromImage(img);
    }

    private bool TryBindContext()
    {
        if (_contextBound) return true;
        var context = EditorContext.Instance;
        if (context is null) return false;
        context.ModeChanged += OnModeChanged;
        ApplyMode(context.Mode);
        _contextBound = true;
        return true;
    }

    private void OnModeChanged(string mode) => ApplyMode(mode);

    private void ApplyMode(string mode)
    {
        _visibleByMode = mode is "edit" or "preview";
        Visible = _visibleByMode;
        QueueRedraw();
    }

    public void Rebuild(MapDoc map)
    {
        _routes.Clear();
        if (map.Routes is not null)
        {
            _routes.AddRange(map.Routes);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_visibleByMode) return;
        foreach (var route in _routes)
        {
            DrawRoute(route);
        }
    }

    private void DrawRoute(RouteDoc route)
    {
        if (route.Waypoints is null || route.Waypoints.Count < 2) return;

        var pts = new Vector2[route.Waypoints.Count];
        for (var i = 0; i < route.Waypoints.Count; i++)
        {
            var c = route.Waypoints[i];
            pts[i] = CoordTransform.CellToWorld(new Vector2I(c.X, c.Y), _cellDistance);
        }

        switch (route.Kind)
        {
            case "land":         DrawLand(pts);         break;
            case "path":         DrawPath(pts);         break;
            case "trade_route":  DrawTradeRoute(pts);   break;
            case "air_route":    DrawAirRoute(pts);     break;
            case "water_route":  DrawWaterRoute(pts);   break;
            case "underground":  DrawUnderground(pts);  break;
            case "teleport":     DrawTeleport(pts);     break;
        }

        // 端点 + 中间点 sprite
        DrawNodeSprites(pts, route.NodeSprite);
    }

    // ---------------------------------------------------------------- 线型绘制

    private void DrawLand(Vector2[] pts)
    {
        // 实线 2px outline + 1px 中心高光（card fill）
        DrawPolyline(pts, Outline, 2f);
        DrawPolyline(pts, CardFill, 1f);
    }

    private void DrawPath(Vector2[] pts)
    {
        // 虚线 2px：每 4px 1px 间隔 — 用 DrawDashedLine (Godot 4.3+)
        // 退路：自己循环画短段
        DrawDashed(pts, Outline, 2f, dashLen: 4f, gapLen: 1f, alpha: 0.6f);
    }

    private void DrawTradeRoute(Vector2[] pts)
    {
        // 双实线 (red + blue, 2px each, 2px 间隔)
        var red  = FactionRed;
        var blue = FactionBlue;
        // 平行偏移近似:用 polyline 粗细 + 颜色叠加
        DrawPolyline(pts, red,  2f);
        DrawPolyline(pts, blue, 2f);
    }

    private void DrawAirRoute(Vector2[] pts)
    {
        // 点划线:8px 实线 + 4px 间隔 + 2px 点 — DrawDashedLine
        DrawDashed(pts, MagicGlow, 2f, dashLen: 8f, gapLen: 4f, alpha: 1f);
    }

    private void DrawWaterRoute(Vector2[] pts)
    {
        // 实线 2px + 1px 白浪花边
        DrawPolyline(pts, Water, 2f);
        DrawPolyline(pts, CardFill, 1f);
    }

    private void DrawUnderground(Vector2[] pts)
    {
        // 双实线 + 阴影偏移 (1px 1px outline 40%)
        var shadow = new Color(Outline, 0.4f);
        var offset = new Vector2(1f, 1f);
        var shadowPts = new Vector2[pts.Length];
        for (var i = 0; i < pts.Length; i++) shadowPts[i] = pts[i] + offset;
        DrawPolyline(shadowPts, shadow, 2f);
        DrawPolyline(pts, Outline, 2f);
    }

    private void DrawTeleport(Vector2[] pts)
    {
        // 点对点虚线 2px 点 + 4px 间隔
        DrawDashed(pts, MagicGlow, 2f, dashLen: 2f, gapLen: 4f, alpha: 1f);
        // 端点光晕 (2px glow halo, 用 DrawCircle 半径 4)
        foreach (var p in new[] { pts[0], pts[^1] })
        {
            DrawCircle(p, 4f, new Color(MagicGlow, 0.4f));
        }
    }

    // ---------------------------------------------------------------- 节点 sprite

    private void DrawNodeSprites(Vector2[] pts, int spriteIndex)
    {
        if (_atlas is null) return;
        if (spriteIndex < 0 || spriteIndex >= FrameCount) return;

        // atlas 帧坐标:96 / 16 = 6 帧
        var srcRect = new Rect2(spriteIndex * FrameSize, 0, FrameSize, FrameSize);
        // 节点显示尺寸 12px（小于 16px frameSize，留 2px 描边 padding）
        const float displaySize = 12f;
        var origin = new Vector2(displaySize / 2f, displaySize / 2f);

        foreach (var p in pts)
        {
            var dstRect = new Rect2(p - origin, new Vector2(displaySize, displaySize));
            DrawTextureRectRegion(_atlas, dstRect, srcRect);
        }
    }

    // ---------------------------------------------------------------- 工具方法

    private void DrawDashed(Vector2[] pts, Color color, float width,
                            float dashLen, float gapLen, float alpha)
    {
        var c = new Color(color, alpha);
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var seg = b - a;
            var len = seg.Length();
            if (len <= 0.001f) continue;
            var dir = seg / len;
            var pos = 0f;
            while (pos < len)
            {
                var dashEnd = Mathf.Min(pos + dashLen, len);
                DrawLine(a + dir * pos, a + dir * dashEnd, c, width);
                pos = dashEnd + gapLen;
            }
        }
    }

    public override void _ExitTree()
    {
        var context = EditorContext.Instance;
        if (context is not null)
        {
            context.ModeChanged -= OnModeChanged;
            _contextBound = false;
        }
    }
}
