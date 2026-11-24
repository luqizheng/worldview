using System.Threading;

namespace WorldView.Presentation.Publication;

/// <summary>
/// 视口代计数器（M1 Profile §3.5）：单个视口实例拥有的单调递增代。
/// 视口相关呈现输入（逻辑尺寸、DPI、相机变换、可见区域）变化时递增。
/// 进程内呈现状态——绝不由墙钟时间或随机值生成，绝不在独立视口/窗口间共享；
/// 请求创建时把代捕获为不可变值传入上下文。
/// </summary>
public sealed class ViewportGenerationTracker
{
    private long _generation = 1;

    /// <summary>当前代（从 1 开始）。</summary>
    public long Current => Volatile.Read(ref _generation);

    /// <summary>递增并返回新代。</summary>
    public long Advance() => Interlocked.Increment(ref _generation);
}
