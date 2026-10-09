using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;

namespace WorldView.Render;

/// <summary>
/// 地形装饰层（见 docs/美术资源规范_v0.1.md §4.9.1）。
///
/// <para>本层叠加在 <see cref="TerrainLayer"/> 之上，提供 5 个 <c>TerrainType</c> 派生视觉：</para>
/// <list type="bullet">
///   <item>高度阴影：<c>Mountain</c> / <c>Hill</c>（借用 void）右上 1px 暗影 + 左下 1px 高光</item>
///   <item>海岸线浪花：<c>Water</c> 与 <c>Coast</c> 边界处 1px 暖白浪花</item>
///   <item>植被点缀：<c>Forest</c> 内 30% 密度 1px 树冠（accent.vegetation_highlight）</item>
///   <item>河流动画：<c>River</c>（借用 snow）上 2 帧循环 1px 主流 + 1px 高光</item>
///   <item>等高线：见 <see cref="GridOverlay"/></item>
/// </list>
///
/// <para>本层独立于 <see cref="TerrainLayer"/>（后者继承 <c>TileMapLayer</c>，不能 override <c>_Draw</c>），
/// 由 <see cref="WorldCanvas"/> 在 <c>RebuildAll</c> 链路上同步刷新。</para>
///
/// <para>颜色**硬编码**为 jrpg_palette.yaml 中 27 hex 的 v0.1 副本，PaletteLoader（T23+）实现后切换为
/// <c>Color.FromHtml(PaletteLoader.Get("jrpg").Marker.X)</c> 形式。</para>
/// </summary>
public partial class TerrainDecor : Node2D
{
    /// <summary>一格在画布上的像素边长（与 TerrainLayer 同步）。</summary>
    [Export]
    public float CellPixels { get; set; } = TerrainTileSetFactory.TilePixels;

    // ---- §4.9.1 调色板（硬编码 v0.1,27 hex 子集） ----

    private static readonly Color JrpgOutline = Color.FromHtml("#5a3a1aff");          // §4.5 --jrpg-outline
    private static readonly Color JrpgTextDisabled = Color.FromHtml("#8a7a5aff");     // 30% 透明暗影
    private static readonly Color CoastFoam = Color.FromHtml("#fff8e7ff");            // 浪花暖白（= --jrpg-card）
    private static readonly Color RiverMain = Color.FromHtml("#3a6a8aff");            // 河流主流（深水同色）
    private static readonly Color RiverHighlight = Color.FromHtml("#7aacacff");       // 河流动画高光（= coast 借用色）
    private static readonly Color VegetationHighlight = Color.FromHtml("#a8c89aff");  // 植被高亮

    private const float HeightShadowAlpha = 0.5f;   // 高度阴影 / 高光 1px
    private const float CoastLineWidth = 1f;
    private const float RiverLineWidth = 1f;
    private const float VegetationPointRadius = 1f;

    /// <summary>动画相位 0/1,每 0.5s 切换一次（§4.9.1 2 fps）。</summary>
    private int _riverPhase;

    /// <summary>预生成的植被点缀位置（每张地图只算一次,Rebuild 时重算）。</summary>
    private readonly List<(Vector2 pos, Color color)> _vegetationDots = new();

    private IReadOnlyList<TerrainCellEntry>? _cells;
    private int _width;
    private int _height;

    public override void _Ready()
    {
        // §4.9.1 河流动画 2 fps:用 _Process 累计时间,每 0.5s 翻一帧。
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        // 累计 0.5s 翻一次相位;避免 _Draw 重绘以外的副作用。
        if (Engine.GetPhysicsFrames() % 30 == 0)
        {
            _riverPhase = (_riverPhase + 1) % 2;
            QueueRedraw();
        }
    }

    /// <summary>用当前地图的 sparse 单元重建装饰层。</summary>
    public void Rebuild(MapDoc map)
    {
        _cells = map.TerrainData.Cells;
        _width = map.Grid.Width;
        _height = map.Grid.Height;

        BuildVegetationDots();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_cells is null || CellPixels <= 0f)
        {
            return;
        }

        DrawHeightShadows();
        DrawCoastline();
        DrawRiverAnimation();
        DrawVegetationDots();
    }

    // ------------------------------------------------------------------ 1. 高度阴影

    /// <summary>
    /// Mountain / Hill（借用 void）上画 1px 暗影（右下）+ 1px 高光（左上）。
    /// 简化为对角 1px 线 + 1px 偏置,非"真实 3D"。
    /// </summary>
    private void DrawHeightShadows()
    {
        foreach (var entry in _cells!)
        {
            if (entry.Type != TerrainType.Mountain && entry.Type != TerrainType.Hill)
            {
                continue;
            }

            var px = entry.At.X * CellPixels;
            var py = entry.At.Y * CellPixels;
            var s = CellPixels;

            // 左上 1px 高光（暖白 = --jrpg-card / CoastFoam,但实际用 30% 透明 outline）
            DrawLine(new Vector2(px, py), new Vector2(px + s, py), JrpgTextDisabled with { A = HeightShadowAlpha });
            DrawLine(new Vector2(px, py), new Vector2(px, py + s), JrpgTextDisabled with { A = HeightShadowAlpha });

            // 右下 1px 暗影（深棕 outline）
            DrawLine(new Vector2(px, py + s), new Vector2(px + s, py + s), JrpgOutline with { A = HeightShadowAlpha });
            DrawLine(new Vector2(px + s, py), new Vector2(px + s, py + s), JrpgOutline with { A = HeightShadowAlpha });
        }
    }

    // ------------------------------------------------------------------ 2. 海岸线

    /// <summary>
    /// 在 <c>Water</c> 单元格朝 non-water 邻居的边界上画 1px 浪花线。
    /// </summary>
    private void DrawCoastline()
    {
        foreach (var entry in _cells!)
        {
            if (entry.Type != TerrainType.Water)
            {
                continue;
            }

            var x = entry.At.X;
            var y = entry.At.Y;
            var px = x * CellPixels;
            var py = y * CellPixels;
            var s = CellPixels;

            // 上边界
            if (GetTypeAt(x, y - 1) != TerrainType.Water)
            {
                DrawLine(new Vector2(px, py), new Vector2(px + s, py), CoastFoam, CoastLineWidth);
            }

            // 下边界
            if (GetTypeAt(x, y + 1) != TerrainType.Water)
            {
                DrawLine(new Vector2(px, py + s), new Vector2(px + s, py + s), CoastFoam, CoastLineWidth);
            }

            // 左边界
            if (GetTypeAt(x - 1, y) != TerrainType.Water)
            {
                DrawLine(new Vector2(px, py), new Vector2(px, py + s), CoastFoam, CoastLineWidth);
            }

            // 右边界
            if (GetTypeAt(x + 1, y) != TerrainType.Water)
            {
                DrawLine(new Vector2(px + s, py), new Vector2(px + s, py + s), CoastFoam, CoastLineWidth);
            }
        }
    }

    // ------------------------------------------------------------------ 3. 河流动画

    /// <summary>
    /// <c>River</c> 借用 snow,2 帧循环:phase 0 画主流 + 高光偏移 -1,phase 1 偏移 +1。
    /// </summary>
    private void DrawRiverAnimation()
    {
        var offset = _riverPhase == 0 ? -1f : 1f;

        foreach (var entry in _cells!)
        {
            if (entry.Type != TerrainType.River)
            {
                continue;
            }

            var px = entry.At.X * CellPixels;
            var py = entry.At.Y * CellPixels;
            var s = CellPixels;

            // 主流 1px（深水色）
            DrawLine(new Vector2(px, py + s / 2f), new Vector2(px + s, py + s / 2f), RiverMain, RiverLineWidth);

            // 高光 1px 偏置 1px（coast 借用色）
            DrawLine(
                new Vector2(px, py + s / 2f + offset),
                new Vector2(px + s, py + s / 2f + offset),
                RiverHighlight,
                RiverLineWidth);
        }
    }

    // ------------------------------------------------------------------ 4. 植被点缀

    private const float VegetationDensity = 0.3f;  // §4.9.1 30% 密度

    private void BuildVegetationDots()
    {
        _vegetationDots.Clear();

        if (_cells is null)
        {
            return;
        }

        foreach (var entry in _cells)
        {
            if (entry.Type != TerrainType.Forest)
            {
                continue;
            }

            // 用 entry.At 的哈希做稳定伪随机,保证 Rebuild 后位置不变（避免抖动）。
            var seed = unchecked((uint)((entry.At.X * 73856093) ^ (entry.At.Y * 19349663)));
            var rng = new RandomNumberGenerator();
            rng.Seed = seed;

            if (rng.Randf() > VegetationDensity)
            {
                continue;
            }

            // 1px 树冠位置:格内均匀随机,落到中心 ±25% 范围。
            var fracX = 0.25f + rng.Randf() * 0.5f;
            var fracY = 0.25f + rng.Randf() * 0.5f;
            var pos = new Vector2(
                entry.At.X * CellPixels + CellPixels * fracX,
                entry.At.Y * CellPixels + CellPixels * fracY);

            _vegetationDots.Add((pos, VegetationHighlight));
        }
    }

    private void DrawVegetationDots()
    {
        foreach (var (pos, color) in _vegetationDots)
        {
            DrawCircle(pos, VegetationPointRadius, color);
        }
    }

    // ------------------------------------------------------------------ 邻居查询

    private TerrainType GetTypeAt(int x, int y)
    {
        if (_cells is null || x < 0 || y < 0 || x >= _width || y >= _height)
        {
            return TerrainType.Plain;
        }

        foreach (var entry in _cells)
        {
            if (entry.At.X == x && entry.At.Y == y)
            {
                return entry.Type;
            }
        }

        return TerrainType.Plain;
    }
}
