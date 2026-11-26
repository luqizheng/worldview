using System;
using SkiaSharp;
using WorldView.Presentation.Scene;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 无状态场景渲染器（WP-30B ADR-07 / ADR-09）：只使用传入的 SKCanvas 与不可变帧输入
/// 发出绘制命令。不持有任何字段、不持有长期 Skia 资源、不回读 Publisher / ViewState /
/// 控件——一次调用的全部输入都在帧输入值里。
///
/// 本阶段（Phase 3A）为最小实现：只绘制空态背景，完整地图绘制属后续阶段。
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

        // M1 空态：中性背景填充。快照为 null（从未发布）时同样成立——
        // 生命周期正确性不依赖任何场景内容。
        canvas.Clear(NeutralBackground);
    }

    /// <summary>M1 幂等空操作：零长期 Skia 资源，无需释放（ADR-09）。不得抛出。</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>M1 中性背景（builtin-neutral-background/v1 的占位值；正式色值随渲染阶段落地）。</summary>
    private static readonly SKColor NeutralBackground = new(0x20, 0x22, 0x26);
}
