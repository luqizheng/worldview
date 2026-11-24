using System;

namespace WorldView.Presentation.Scene;

/// <summary>
/// 渲染中立的场景项基类（PRA-01 契约 §4.2）。M1 只允许空白场景项
/// （中性背景描述）；不包含任何几何、标签、地形、道路、建筑或地图装饰。
/// 所有项携带稳定 ItemId（不依赖枚举顺序或完成顺序）。
/// </summary>
public abstract record SceneItem
{
    /// <summary>稳定项 ID（schema 版本化）。</summary>
    public string ItemId { get; }

    protected SceneItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("场景项 ID 不能为空白。", nameof(itemId));
        }

        ItemId = itemId;
    }
}

/// <summary>
/// M1 空白场景的中性背景描述：渲染中立 token，实际颜色解释归渲染后端
/// （WP-30B）。M1 Profile §5.1 要求唯一背景权威——背景以场景内描述存在
/// （非渲染器私有 clear 行为），由本类型与 <see cref="SceneLayerIds.Background"/> 承载。
/// </summary>
public sealed record BlankBackgroundItem : SceneItem
{
    /// <summary>中性背景 token（由渲染后端按内置基线解释）。</summary>
    public string NeutralBackgroundToken { get; }

    public BlankBackgroundItem(string itemId, string neutralBackgroundToken)
        : base(itemId)
    {
        if (string.IsNullOrWhiteSpace(neutralBackgroundToken))
        {
            throw new ArgumentException("中性背景 token 不能为空白。", nameof(neutralBackgroundToken));
        }

        NeutralBackgroundToken = neutralBackgroundToken;
    }
}

/// <summary>稳定场景项 ID。</summary>
public static class SceneItemIds
{
    /// <summary>M1 中性背景项。</summary>
    public const string BlankBackground = "scene.item.background.neutral.v1";
}

/// <summary>内置中性背景 token（M1 基线；影响输出时递增版本）。</summary>
public static class NeutralBackgroundTokens
{
    /// <summary>内置中性空白背景（近白浅灰；由渲染后端解释，非 Domain 数据）。</summary>
    public const string BuiltinNeutralV1 = "builtin-neutral-background/v1";
}
