using System;

namespace WorldView.Presentation.Scene;

/// <summary>
/// M1 空白场景子系统的输出载荷：仅中性背景描述。
/// 不包含任何地图几何、bounds/extent、标签、地形、道路、建筑或装饰——
/// 不为展示效果创建 Domain 不存在的地图数据（M1 Profile §5.1）。
/// </summary>
public sealed record BlankSceneContent
{
    /// <summary>中性背景描述。</summary>
    public BlankBackgroundItem Background { get; }

    public BlankSceneContent(BlankBackgroundItem background)
    {
        Background = background ?? throw new ArgumentNullException(nameof(background));
    }
}
