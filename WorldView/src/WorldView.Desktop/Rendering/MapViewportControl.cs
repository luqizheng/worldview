using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WorldView.Desktop.Rendering;

/// <summary>
/// 地图视口控件（WP-30B ADR-07）：只保留 Avalonia 本职——布局、输入/焦点、
/// 尺寸/DPI 观察及转发、<see cref="InvalidateVisual"/>、生命周期事件转发。
/// 持有单个 <see cref="MapViewportBinding"/> 引用；不得直接持有 Runtime / Renderer、
/// 不得直接读取 Publisher、不得推进 generation、不得组装帧输入（唯一捕获点在
/// <see cref="MapViewportBinding.CaptureFrame"/>，ADR-11）。
///
/// 生命周期（ADR-10 四分模型）：
/// - 暂时 Detach（<see cref="OnDetachedFromVisualTree"/>）：只解除 Binding 登记，
///   不关闭、不释放；重新挂载复用同一 Binding。
/// - 永久关闭：由拥有者（ViewModel 的 C1 / C2 编排）显式触发，控件不自作主张。
/// </summary>
internal sealed class MapViewportControl : Control
{
    private MapViewportBinding? _binding;
    private TopLevel? _topLevel;

    public MapViewportControl()
    {
        SizeChanged += OnSizeChanged;
    }

    /// <summary>当前关联的 Binding（诊断 / 测试锚点）。</summary>
    internal MapViewportBinding? Binding => _binding;

    /// <summary>
    /// 关联 Binding。已永久关闭的 Binding 被拒绝（终态：需新建 Binding + Runtime，
    /// ADR-07 约束 10）。同一控件可在暂时 Detach 后重新 Bind 未关闭的 Binding。
    /// </summary>
    public void Bind(MapViewportBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.IsClosed)
        {
            throw new InvalidOperationException(
                $"Binding {binding.BindingId:N} 已永久关闭，不能 Attach（终态，需新建 Binding）。");
        }

        _binding = binding;
        PushViewportSize();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnTopLevelScalingChanged;
        }

        // 已关闭的 Binding 在此抛出（终态拒绝）；未关闭则登记引用并恢复绘制。
        _binding?.Attach(this);
        PushViewportSize();
        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 生命周期 A（暂时 Detach）：标签切换 / reparent / 模板重建都会走到这里——
        // 绝不在此关闭 Binding、关闭 Runtime 或触发任何关闭编排（ADR-10 §2 结论）。
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnTopLevelScalingChanged;
            _topLevel = null;
        }

        _binding?.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// 绘制入口。<b>关闭检查必须是第一件事</b>（ADR-08 Q9）：volatile 读
    /// <see cref="MapViewportBinding.IsClosed"/>，读到 true 立即返回——不创建
    /// draw operation、不获取 lease、不发绘制命令；读到 false 则继续，之后即使
    /// 关闭置位，本在飞帧允许以已捕获数据自然结束（不保证零 post-close 绘制）。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var binding = _binding;
        if (binding is null || binding.IsClosed)
        {
            return;
        }

        base.Render(context);

        // 唯一捕获点（ADR-11）：控件只调用 CaptureFrame 并转发，不自行组装帧输入。
        var frame = binding.CaptureFrame();
        context.Custom(new MapDrawOperation(
            frame,
            binding.Renderer,
            new Rect(0, 0, frame.Width, frame.Height)));
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        PushViewportSize();
        InvalidateVisual();
    }

    private void OnTopLevelScalingChanged(object? sender, EventArgs e)
    {
        PushViewportSize();
        InvalidateVisual();
    }

    /// <summary>尺寸 / DPI 变化 → 经 binding.OnViewportChanged 缓存并推进视口代。</summary>
    private void PushViewportSize()
    {
        if (_binding is null)
        {
            return;
        }

        var scaling = _topLevel?.RenderScaling ?? 1.0;
        _binding.OnViewportChanged(Bounds.Width, Bounds.Height, scaling);
    }
}
