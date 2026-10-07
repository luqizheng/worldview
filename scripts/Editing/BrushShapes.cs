using System;
using System.Collections.Generic;

namespace WorldView.Editing;

/// <summary>
/// 笔刷形状枚举（见 tasks.md T11 + spec.md §3.2）。
///
/// 放在 <c>Editing</c> 层而非 <c>Render</c> 层：编辑命令不应反向依赖渲染层。
/// </summary>
public static class BrushShapes
{
    /// <summary>
    /// 列出笔刷覆盖的全部网格坐标（不做地图边界裁剪，由 <c>EditorContext.SetTerrainCells</c> 静默跳过越界格）。
    /// </summary>
    public static List<Domain.GridCoord> Cells(Domain.GridCoord center, int radius, BrushType brush)
    {
        var r = Math.Max(0, radius);
        var result = new List<Domain.GridCoord>((2 * r + 1) * (2 * r + 1));

        for (var dy = -r; dy <= r; dy++)
        {
            for (var dx = -r; dx <= r; dx++)
            {
                if (brush == BrushType.Round)
                {
                    // 圆：切比雪夫距离 ≤ r 且欧氏距离 ≤ r + 0.5，视觉上更接近圆而非方块。
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > r)
                    {
                        continue;
                    }

                    if (MathF.Sqrt((dx * dx) + (dy * dy)) > r + 0.5f)
                    {
                        continue;
                    }
                }

                result.Add(new Domain.GridCoord(center.X + dx, center.Y + dy));
            }
        }

        return result;
    }
}
