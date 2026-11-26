using System;
using Avalonia.Threading;

namespace WorldView.Desktop.Ui;

/// <summary>
/// UI 线程调度抽象（WP-RA-02-A R1-P1-01）：Desktop 表现层的 UI Dispatcher 边界。
/// 由 <c>ContextSwitched</c> 等后台异步路径触发的 ViewModel 属性更新必须经本抽象
/// 投递到 UI 线程执行——事件源线程取决于异步解析的完成位置（可能为 ThreadPool），
/// 且后台线程不存在 Avalonia SynchronizationContext，<c>ConfigureAwait(true)</c>
/// 不能提供任何 UI 线程保证。生产组合根注入 <see cref="AvaloniaUiDispatcher"/>；
/// 测试宿主注入受控调度器以确定性验证投递边界。
/// 本类型不得被 Application / Domain 引用（保持无 UI 依赖，AGENTS.md C 节）。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>当前线程是否可直接执行 UI 更新。</summary>
    bool CheckAccess();

    /// <summary>
    /// 把回调投递到 UI 线程异步执行（非阻塞，保持调用顺序）。
    /// 回调自身的异常由回调内部处理并就地记录——Post 不传播回调异常，
    /// 避免后台异常在 Dispatcher 层变成不可观察的未处理异常。
    /// </summary>
    void Post(Action action);
}

/// <summary>
/// Avalonia UI Dispatcher 生产实现（WP-RA-02-A R1-P1-01）：
/// <see cref="Dispatcher.UIThread.Post"/> 保持 FIFO 投递顺序，不阻塞调用线程。
/// 仅由组合根（Program.cs）注入 ViewModel；测试宿主不得使用（无运行中的
/// Avalonia 消息循环，投递的回调永远不会执行）。
/// </summary>
internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public static AvaloniaUiDispatcher Instance { get; } = new();

    private AvaloniaUiDispatcher()
    {
    }

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }
}

/// <summary>
/// 内联调度器（测试宿主专用，WP-RA-02-A R1-P1-01）：在调用线程同步执行投递回调。
/// 行为等价于"当前线程即 UI 线程"——测试宿主无 Avalonia 消息循环，内联执行使
/// 既有 ViewModel 测试（无 Dispatcher 注入路径）保持确定性；投递边界本身的
/// 正确性由注入队列式受控调度器的 R1 专项测试验证。
/// </summary>
internal sealed class InlineUiDispatcher : IUiDispatcher
{
    public static InlineUiDispatcher Instance { get; } = new();

    private InlineUiDispatcher()
    {
    }

    public bool CheckAccess() => true;

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
