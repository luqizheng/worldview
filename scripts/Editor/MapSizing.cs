using System;
using WorldView.Domain;

namespace WorldView.Editor;

/// <summary>
/// 物理长度 ↔ 网格格数换算（spec.md §2.2 + 地图尺寸/笔刷半径 UI 共用）。
///
/// 纯算术、不引用 Godot，便于脱离引擎单测。
/// 约定：格数一律向上取整且至少 1 格（不足一格也算一格，不能截断为 0）；
/// <c>length &lt;= 0</c> 属于调用方错误，由 UI 拦截并置灰「应用」，本类不做兜底。
/// </summary>
public static class MapSizing
{
    /// <summary>笔刷半径上限（格）：UI 输入 km/m 时的封顶值。</summary>
    public const int MaxRadiusCells = 64;

    /// <summary>
    /// 把物理长度换算为覆盖它的最小格数（向上取整，至少 1）。
    /// </summary>
    /// <param name="length">物理长度，与 <paramref name="cellDistance"/> 同单位。</param>
    /// <param name="cellDistance">单格代表的物理距离，必须为正数。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cellDistance"/> 不是正数。</exception>
    public static int CellsForLength(double length, double cellDistance)
    {
        if (cellDistance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellDistance), cellDistance, "格距离必须为正数。");
        }

        return Math.Max(1, (int)Math.Ceiling(length / cellDistance));
    }

    /// <summary>
    /// 把格数换算回物理长度（<c>cells * cellDistance</c>，不做取整）。
    /// </summary>
    /// <param name="cells">格数。</param>
    /// <param name="cellDistance">单格代表的物理距离，与返回长度同单位。</param>
    public static double LengthForCells(int cells, double cellDistance) => cells * cellDistance;

    /// <summary>
    /// 按物理范围（宽 × 高）与模板的格距离推导出新的网格参数。
    /// 只有 <see cref="GridSpec.Width"/> / <see cref="GridSpec.Height"/> 被改写，
    /// 单位、原点、显示开关等其余字段沿用模板。
    /// </summary>
    /// <param name="widthPhysical">地图物理宽度，与模板 <see cref="GridSpec.CellDistance"/> 同单位。</param>
    /// <param name="heightPhysical">地图物理高度，与模板 <see cref="GridSpec.CellDistance"/> 同单位。</param>
    /// <param name="template">提供格距离与其余字段的网格模板。</param>
    /// <exception cref="ArgumentOutOfRangeException">模板的格距离不是正数。</exception>
    public static GridSpec GridFromExtent(double widthPhysical, double heightPhysical, GridSpec template)
    {
        return template with
        {
            Width = CellsForLength(widthPhysical, template.CellDistance),
            Height = CellsForLength(heightPhysical, template.CellDistance),
        };
    }
}
