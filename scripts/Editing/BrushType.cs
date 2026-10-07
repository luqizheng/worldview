namespace WorldView.Editing;

/// <summary>
/// 笔刷形状（见 tasks.md T11 ApplyBrushCommand + spec.md §3.2）。
/// </summary>
public enum BrushType
{
    /// <summary>圆形：以中心格为圆心，半径内切比雪夫距离 ≤ radius。</summary>
    Round,

    /// <summary>方形：以中心格为中心的 (2r+1)² 方块。</summary>
    Square,
}
