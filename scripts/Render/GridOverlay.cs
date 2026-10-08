using Godot;

namespace WorldView.Render;

/// <summary>
/// 网格覆盖线（见 tasks.md T15）。
///
/// 性能纪律（spec.md §3.5）：<b>只绘制视口内的格线</b>。
/// 1024×1024 地图若全量绘制会超过 2000 条线段，拖垮帧率。
/// </summary>
public partial class GridOverlay : Node2D
{
    [Export]
    public int GridWidth { get; set; } = 64;

    [Export]
    public int GridHeight { get; set; } = 48;

    [Export]
    public float CellSize { get; set; } = 32f;

    [Export]
    public int MajorEvery { get; set; } = 8;

    [Export]
    public Color MinorColor { get; set; } = new(0f, 0f, 0f, 0.12f);

    [Export]
    public Color MajorColor { get; set; } = new(0f, 0f, 0f, 0.28f);

    [Export]
    public Color BorderColor { get; set; } = new(0.35f, 0.2f, 0.1f, 0.8f);

    /// <summary>切换地图时更新尺寸并重绘。</summary>
    public void Configure(int width, int height, float cellSize)
    {
        GridWidth = width;
        GridHeight = height;
        CellSize = cellSize;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (CellSize <= 0f || GridWidth <= 0 || GridHeight <= 0)
        {
            return;
        }

        // MVP:绘制整张地图的格线(性能上限 4096 格,M3+ 改为视口剔除)。
        var firstX = 0;
        var lastX = GridWidth;
        var firstY = 0;
        var lastY = GridHeight;

        var top = firstY * CellSize;
        var bottom = lastY * CellSize;
        var left = firstX * CellSize;
        var right = lastX * CellSize;

        // 竖线
        for (var x = firstX; x <= lastX; x++)
        {
            var px = x * CellSize;
            var color = x % MajorEvery == 0 ? MajorColor : MinorColor;
            DrawLine(new Vector2(px, top), new Vector2(px, bottom), color, width: 1f);
        }

        // 横线
        for (var y = firstY; y <= lastY; y++)
        {
            var py = y * CellSize;
            var color = y % MajorEvery == 0 ? MajorColor : MinorColor;
            DrawLine(new Vector2(left, py), new Vector2(right, py), color, width: 1f);
        }

        // 地图外边界
        DrawRect(
            new Rect2(Vector2.Zero, new Vector2(GridWidth * CellSize, GridHeight * CellSize)),
            BorderColor,
            filled: false,
            width: 2f);
    }
}
