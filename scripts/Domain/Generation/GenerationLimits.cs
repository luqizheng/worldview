using WorldView.Domain;

namespace WorldView.Domain.Generation;

/// <summary>
/// <c>GenerationSpec</c> 字段合法范围（plan L29 + spec §3）。
///
/// <para>集中维护阈值，避免散落在各调用点；任何越界都会阻断生成任务并通过
/// <see cref="DescribeViolation"/> 提供中文描述（供 Task 6 告警 UI 复用）。</para>
///
/// <para>区间开闭：均为**闭区间**。边界值（如 <c>land_ratio = 0.05</c>）视为合法。</para>
/// </summary>
public static class GenerationLimits
{
    public const double MinLandRatio = 0.05;
    public const double MaxLandRatio = 0.80;

    public const double MinRuggedness = 0;
    public const double MaxRuggedness = 1;

    public const int MinErosion = 0;
    public const int MaxErosion = 3;

    /// <summary>
    /// 判断 <paramref name="spec"/> 全部字段是否在合法区间内。
    /// 任一字段越界返回 <c>false</c>。
    /// </summary>
    public static bool IsWithinRanges(GenerationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.LandRatio >= MinLandRatio
            && spec.LandRatio <= MaxLandRatio
            && spec.Ruggedness >= MinRuggedness
            && spec.Ruggedness <= MaxRuggedness
            && spec.Erosion >= MinErosion
            && spec.Erosion <= MaxErosion;
    }

    /// <summary>
    /// 找出 <paramref name="spec"/> 的**第一个**越界字段并返回中文描述；
    /// 全部在区间内则返回 <c>null</c>。仅报告第一个违例便于 UI 一次性弹窗修一个。
    /// </summary>
    public static string? DescribeViolation(GenerationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (spec.LandRatio < MinLandRatio || spec.LandRatio > MaxLandRatio)
        {
            return $"land_ratio 必须在 [{MinLandRatio:0.00}, {MaxLandRatio:0.00}] 区间内，当前值 {spec.LandRatio:0.###}";
        }
        if (spec.Ruggedness < MinRuggedness || spec.Ruggedness > MaxRuggedness)
        {
            return $"ruggedness 必须在 [{MinRuggedness:0.##}, {MaxRuggedness:0.##}] 区间内，当前值 {spec.Ruggedness:0.###}";
        }
        if (spec.Erosion < MinErosion || spec.Erosion > MaxErosion)
        {
            return $"erosion 必须在 [{MinErosion}, {MaxErosion}] 区间内，当前值 {spec.Erosion}";
        }
        return null;
    }
}
