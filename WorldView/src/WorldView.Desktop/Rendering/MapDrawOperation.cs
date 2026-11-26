using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 每帧短生命周期的自定义绘制操作（WP-30B ADR-07 方案 A，收窄为 ICustomDrawOperation 形式）：
/// 只持有不可变帧输入与无状态渲染器引用，一帧一值。<see cref="Render"/> 内经
/// <see cref="ISkiaSharpApiLeaseFeature"/> 租借 Skia 资源并用 <c>using</c> 在回调内释放
/// （lease 只在 Render 调用栈内存活，ADR-09）；不创建任何长期 Skia 对象。
/// 控件 Render 入口已做 Binding 关闭检查（ADR-08 Q9）——进入本操作的帧都是
/// "已通过入口检查"的在飞帧，允许继续并以已捕获数据自然结束。
/// </summary>
internal sealed class MapDrawOperation : ICustomDrawOperation
{
    private readonly ViewportFrameInput _frame;
    private readonly MapSceneRenderer _renderer;

    public MapDrawOperation(ViewportFrameInput frame, MapSceneRenderer renderer, Rect bounds)
    {
        _frame = frame;
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Bounds = bounds;
    }

    /// <summary>绘制目标区域（来自捕获时的视口逻辑尺寸）。</summary>
    public Rect Bounds { get; }

    /// <summary>M1 无输入命中需求（输入/焦点属控件职责，WP-30C）；恒 false。</summary>
    public bool HitTest(Point p) => false;

    /// <summary>
    /// 渲染回调（可能发生在 OS 渲染线程，ADR-08：线程不敏感——只允许访问帧输入与
    /// 无状态渲染器）。非 Skia 后端时不绘制（M1 不提供其他后端实现）。
    /// </summary>
    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
        {
            return;
        }

        // lease 实现 IDisposable：只在 Render 调用栈内存活，using 就地释放，不交给 GC。
        using var lease = feature.Lease();
        _renderer.Draw(lease.SkCanvas, _frame);
    }

    public void Dispose()
    {
    }

    /// <summary>
    /// 相等语义 = 帧输入值相等（record struct 逐字段比较：快照引用 + 尺寸 + DPI + 相机 + 代）。
    /// 失效策略的完整裁决与测试锁定属实现期后续项（Phase 2 开放问题 4）；M1 每帧新建本操作，
    /// 不依赖失效合并。
    /// </summary>
    public bool Equals(ICustomDrawOperation? other)
        => other is MapDrawOperation op && _frame.Equals(op._frame);
}
