using System;
using SkiaSharp;
using WorldView.Presentation.Scene;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 无状态场景渲染器（WP-30B ADR-07 / ADR-09）：只使用传入的 SKCanvas 与不可变帧输入
/// 发出绘制命令。不持有任何字段、不持有长期 Skia 资源、不回读 Publisher / ViewState /
/// 控件——一次调用的全部输入都在帧输入值里。
///
/// 本阶段（WP-RA-02-A）真实消费捕获的快照（管线验收：Context → Request → Prepare →
/// Publish → Capture → Render 的 Render 步必须以发布结果为输入）：遍历快照图层中的
/// 中性背景项并按内置基线 token 解释背景色；快照为 null（从未发布）时绘制空态背景。
/// 仍不绘制任何地图几何——完整地图绘制属后续 B 阶段（WP-RA-02-A 边界）。
/// Dispose 是 M1 幂等空操作（M1 零长期 Skia 资源，无需释放任何对象），
/// 由 <see cref="MapViewportBinding"/> 在关闭子序的末步调用（ADR-10 §7.1 步骤 1d）。
/// </summary>
internal class MapSceneRenderer : IDisposable
{
    /// <summary>
    /// 以帧输入发出绘制命令。仅在 <c>ICustomDrawOperation.Render</c> 的 Skia lease
    /// 调用栈内被调用；回调内创建的 Skia 资源（如 SKPaint）必须 <c>using</c> 就地释放。
    /// </summary>
    public virtual void Draw(SKCanvas canvas, ViewportFrameInput frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        canvas.Clear(ResolveBackground(frame.Snapshot));
    }

    /// <summary>
    /// 从捕获的快照解析中性背景色（WP-RA-02-A：Render 步的真实快照消费点）。
    /// 快照为 null（从未发布）→ 空态背景；已发布 → 按背景层的中性背景 token
    /// 解释内置基线色（BuiltinNeutralV1 = 近白浅灰，见 NeutralBackgroundTokens）。
    /// 不读取数据库、不回读 Publisher——只消费帧输入里的不可变快照引用。
    /// </summary>
    private static SKColor ResolveBackground(SceneSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return EmptyStateBackground;
        }

        foreach (var layer in snapshot.Layers)
        {
            foreach (var item in layer.Items)
            {
                if (item is BlankBackgroundItem blank
                    && string.Equals(
                        blank.NeutralBackgroundToken,
                        NeutralBackgroundTokens.BuiltinNeutralV1,
                        StringComparison.Ordinal))
                {
                    return PublishedNeutralBackground;
                }
            }
        }

        // 未知 token：仍按内置基线解释（M1 无正式样式系统；不做占位欺骗）。
        return PublishedNeutralBackground;
    }

    /// <summary>M1 幂等空操作：零长期 Skia 资源，无需释放（ADR-09）。不得抛出。</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>空态背景（从未发布快照时的可视空态；与已发布背景可区分，便于人工与测试观察）。</summary>
    private static readonly SKColor EmptyStateBackground = new(0x20, 0x22, 0x26);

    /// <summary>已发布中性背景（builtin-neutral-background/v1：近白浅灰，按 token 文档解释）。</summary>
    private static readonly SKColor PublishedNeutralBackground = new(0xE8, 0xE9, 0xEB);
}
