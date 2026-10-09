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

    // ---- §4.9.1 等高线（默认关闭,需在 Configure 时传 heightUnit > 0 开启） ----

    /// <summary>等高线主线颜色（每 <c>HeightUnit * 5</c> 米一条主线）。</summary>
    [Export]
    public Color ContourMajorColor { get; set; } = new(0.35f, 0.2f, 0.06f, 0.7f);

    /// <summary>等高线辅线颜色（每 <c>HeightUnit</c> 米一条辅线,主线之间 4 条）。</summary>
    [Export]
    public Color ContourMinorColor { get; set; } = new(0.35f, 0.2f, 0.06f, 0.35f);

    /// <summary>每米多少像素（用于换算等高线间隔）。&lt;=0 表示禁用等高线。</summary>
    public float PixelsPerHeightUnit { get; private set; }

    /// <summary>切换地图时更新尺寸并重绘。</summary>
    /// <param name="width">网格宽（格数）</param>
    /// <param name="height">网格高（格数）</param>
    /// <param name="cellSize">每格像素边长</param>
    /// <param name="heightUnit">GridSpec.HeightUnit（每米多少 cell_distance）；&lt;=0 时不画等高线</param>
    public void Configure(int width, int height, float cellSize, float heightUnit = 0f)
    {
        GridWidth = width;
        GridHeight = height;
        CellSize = cellSize;
        PixelsPerHeightUnit = heightUnit > 0f ? cellSize / heightUnit : 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (CellSize <= 0f || GridWidth <= 0 || GridHeight <= 0)
        {
            return;
        }

        // 只取视口覆盖的格线范围。
        var visible = GetViewportRect();

        var firstX = Mathf.Max(0, Mathf.FloorToInt(visible.Position.X / CellSize));
        var lastX = Mathf.Min(GridWidth, Mathf.CeilToInt(visible.End.X / CellSize));
        var firstY = Mathf.Max(0, Mathf.FloorToInt(visible.Position.Y / CellSize));
        var lastY = Mathf.Min(GridHeight, Mathf.CeilToInt(visible.End.Y / CellSize));

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

        // 等高线（M3+ 等高线钩子 §4.9.1:仅在 PixelsPerHeightUnit > 0 时画）
        if (PixelsPerHeightUnit > 0f)
        {
            DrawContourLines(top, bottom, left, right, firstX, lastX, firstY, lastY);
        }
    }

    // ------------------------------------------------------------------ 等高线

    /// <summary>
    /// 在视口范围内画横/竖等高线。
    /// 规则：每 <c>PixelsPerHeightUnit</c> 像素一条线；每 5 条中第 1 条是主线（深），其余 4 条辅线（半透明）。
    /// <para>§4.9.1 实际高度数据 M3+ 才有（TerrainCell.Height），v0.1 用「按网格坐标推导伪高度」：
    /// 仅在 <c>Mountain</c> / <c>Hill</c>（借用 void）格子上叠加装饰，方位由 <c>entry.At.Y</c> 决定（高层在 y 小的方向），
    /// —— 但当前实现**没有 height 数据**，所以 v0.1 退化为"全图按固定间距画"——这是 v0.1 占位，
    /// 真正的"高度 → 等高线"留待 M3+。</para>
    /// </summary>
    private void DrawContourLines(float top, float bottom, float left, float right, int firstX, int lastX, int firstY, int lastY)
    {
        var step = PixelsPerHeightUnit;
        if (step <= 0f)
        {
            return;
        }

        // 竖线等高线（沿 x 轴）
        var xStart = Mathf.Floor(left / step) * step;
        for (var x = xStart; x <= right; x += step)
        {
            var index = Mathf.RoundToInt(x / step);
            var isMajor = (index % 5) == 0;
            var color = isMajor ? ContourMajorColor : ContourMinorColor;
            DrawLine(new Vector2(x, top), new Vector2(x, bottom), color, width: 1f);
        }

        // 横线等高线（沿 y 轴）
        var yStart = Mathf.Floor(top / step) * step;
        for (var y = yStart; y <= bottom; y += step)
        {
            var index = Mathf.RoundToInt(y / step);
            var isMajor = (index % 5) == 0;
            var color = isMajor ? ContourMajorColor : ContourMinorColor;
            DrawLine(new Vector2(left, y), new Vector2(right, y), color, width: 1f);
        }
    }
}
