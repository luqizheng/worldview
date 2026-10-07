using System;
using Godot;

namespace WorldView.Render;

/// <summary>
/// 四层坐标换算（见 tasks.md T13 + 地图编辑器规格 §5）。
///
/// 层次定义：
/// <list type="number">
///   <item><b>世界坐标</b>（world）：以工程 <c>base_unit</c> 为单位的连续坐标，Y 向下。</item>
///   <item><b>像素坐标</b>（pixel）：Godot 2D 画布坐标，= 世界坐标 × <see cref="PixelsPerUnit"/>。</item>
///   <item><b>网格坐标</b>（cell）：整数格，= floor(世界坐标 / cell_distance)。</item>
/// </list>
///
/// 关键不变量：<c>WorldToCell(CellToWorld(c)) == c</c>（含负格，因此必须用 <see cref="MathF.Floor"/> 而非截断）。
/// </summary>
public static class CoordTransform
{
    /// <summary>1 个世界单位对应的像素数（zoom = 1 时）。</summary>
    public const float PixelsPerUnit = 32f;

    // ---------------------------------------------------------------- 世界 ↔ 像素

    public static Vector2 WorldToPixel(Vector2 world) => world * PixelsPerUnit;

    public static Vector2 PixelToWorld(Vector2 pixel) => pixel / PixelsPerUnit;

    // ---------------------------------------------------------------- 世界 ↔ 网格

    /// <summary>世界坐标 → 网格坐标。必须 floor，负数才正确。</summary>
    public static Vector2I WorldToCell(Vector2 world, float cellDistance)
    {
        if (cellDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellDistance), "cell_distance 必须为正数。");
        }

        return new Vector2I(
            (int)MathF.Floor(world.X / cellDistance),
            (int)MathF.Floor(world.Y / cellDistance));
    }

    /// <summary>网格坐标 → 该格左上角的世界坐标（瓦片原点）。</summary>
    public static Vector2 CellToWorld(Vector2I cell, float cellDistance) =>
        new(cell.X * cellDistance, cell.Y * cellDistance);

    /// <summary>网格坐标 → 该格中心的世界坐标（放置标记 / 建筑用）。</summary>
    public static Vector2 CellCenterToWorld(Vector2I cell, float cellDistance) =>
        new((cell.X + 0.5f) * cellDistance, (cell.Y + 0.5f) * cellDistance);

    // ---------------------------------------------------------------- 网格 ↔ 像素

    public static Vector2I PixelToCell(Vector2 pixel, float cellDistance) =>
        WorldToCell(PixelToWorld(pixel), cellDistance);

    public static Vector2 CellToPixel(Vector2I cell, float cellDistance) =>
        WorldToPixel(CellToWorld(cell, cellDistance));

    // ---------------------------------------------------------------- 矩形换算

    /// <summary>
    /// 世界矩形 → 覆盖它的最小网格矩形（用于视口剔除 dirty_rect，见 spec.md §3.5）。
    /// 返回的 <c>Size</c> 至少为 1×1 且不含右下边界之外的格。
    /// </summary>
    public static Rect2I WorldRectToCellRect(Rect2 world, float cellDistance)
    {
        if (cellDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(cellDistance), "cell_distance 必须为正数。");
        }

        var min = WorldToCell(world.Position, cellDistance);
        var max = WorldToCell(world.End, cellDistance);

        // 世界矩形右/下边界正好落在格线上时，不该多算一格。
        if (Mathf.IsEqualApprox(world.End.X, max.X * cellDistance))
        {
            max.X -= 1;
        }

        if (Mathf.IsEqualApprox(world.End.Y, max.Y * cellDistance))
        {
            max.Y -= 1;
        }

        var size = new Vector2I(
            Math.Max(1, max.X - min.X + 1),
            Math.Max(1, max.Y - min.Y + 1));

        return new Rect2I(min, size);
    }

}
