using System;
using System.Threading;
using System.Threading.Tasks;

namespace WorldView.ProjectManagement;

/// <summary>自动保存失败事件载荷。</summary>
public sealed class AutosaveFailedEventArgs : EventArgs
{
    public Exception Exception { get; }
    public DateTime AtUtc { get; }

    public AutosaveFailedEventArgs(Exception exception, DateTime atUtc)
    {
        Exception = exception;
        AtUtc = atUtc;
    }
}

/// <summary>
/// 自动保存服务。30 秒延迟合并（debounce）：在 <see cref="Delay"/> 时间内重复触发
/// <see cref="ScheduleAutoSave"/> 只会延后截止时间，不会创建新任务。
/// <para>线程模型：所有公开方法均线程安全。Timer 回调发生在 threadpool 上。</para>
/// </summary>
public sealed class AutosaveService : IDisposable
{
    private readonly IClock _clock;
    private readonly DirtyStateService _dirty;
    private readonly TimeSpan _delay;
    private readonly object _lock = new();

    private Timer? _timer;
    private Func<CancellationToken, Task<bool>>? _saveCallback;
    private TaskCompletionSource<bool>? _pendingTcs;
    private DateTime _dueTimeUtc;
    private bool _disposed;

    public TimeSpan Delay => _delay;

    /// <summary>自动保存失败时触发，附带异常与时间。</summary>
    public event EventHandler<AutosaveFailedEventArgs>? AutosaveFailed;

    public AutosaveService(IClock clock, DirtyStateService dirty, TimeSpan? delay = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _dirty = dirty ?? throw new ArgumentNullException(nameof(dirty));
        _delay = delay ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 注册/延后一次自动保存。<paramref name="saveAsync"/> 会在 timer 到期或
    /// <see cref="FlushNowAsync"/> 时被调用，返回 true 表示成功（自动 MarkClean 当前版本），
    /// 返回 false 或抛异常表示失败（不清脏并触发 <see cref="AutosaveFailed"/>）。
    /// <list type="bullet">
    ///   <item>若当前无 pending 任务：记录 callback + 启动 Timer 到 <c>now + Delay</c>。</item>
    ///   <item>若已有 pending 任务：仅更新截止时间为 <c>now + Delay</c>，重置 Timer。</item>
    /// </list>
    /// </summary>
    public void ScheduleAutoSave(Func<CancellationToken, Task<bool>> saveAsync)
    {
        if (saveAsync is null) throw new ArgumentNullException(nameof(saveAsync));

        lock (_lock)
        {
            if (_disposed) return;

            _saveCallback = saveAsync;
            _dueTimeUtc = _clock.UtcNow + _delay;
            _pendingTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RestartTimerLocked();
        }
    }

    /// <summary>
    /// 立即执行保存（不取消 pending timer；如有 pending 在到期时仍会再次触发）。
    /// 返回保存结果（true=成功，false=失败）。
    /// </summary>
    public Task<bool> FlushNowAsync(CancellationToken cancellationToken)
    {
        Func<CancellationToken, Task<bool>>? callback;
        lock (_lock) { callback = _saveCallback; }
        if (callback is null)
        {
            return Task.FromResult(false);
        }
        return ExecuteSaveAsync(callback, cancellationToken);
    }

    /// <summary>取消 pending 自动保存（关闭/切换工程时调用）。</summary>
    public void Cancel()
    {
        lock (_lock)
        {
            DisposeTimerLocked();
            if (_pendingTcs is { } tcs && !tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(false);
            }
            _pendingTcs = null;
            _saveCallback = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            DisposeTimerLocked();
            if (_pendingTcs is { } tcs && !tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(false);
            }
            _pendingTcs = null;
            _saveCallback = null;
        }
    }

    /// <summary>仅测试可见：等待当前 pending 任务结束（用于断言）。</summary>
    internal Task? PendingTask
    {
        get
        {
            lock (_lock) { return _pendingTcs?.Task; }
        }
    }

    private void RestartTimerLocked()
    {
        DisposeTimerLocked();
        var dueIn = _dueTimeUtc - _clock.UtcNow;
        if (dueIn < TimeSpan.Zero) dueIn = TimeSpan.Zero;
        _timer = new Timer(OnTimer, null, dueIn, Timeout.InfiniteTimeSpan);
    }

    private void DisposeTimerLocked()
    {
        if (_timer is not null)
        {
            _timer.Dispose();
            _timer = null;
        }
    }

    private void OnTimer(object? state)
    {
        Func<CancellationToken, Task<bool>>? callback;
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _pendingTcs;
            callback = _saveCallback;
            _pendingTcs = null;
            DisposeTimerLocked();
        }
        if (tcs is null) return;
        tcs.TrySetResult(true);

        // timer 已到期，触发自动保存
        if (callback is not null)
        {
            // 异步执行保存：保存期间允许新的 Schedule 来再次注册
            _ = ExecuteSaveAsync(callback, CancellationToken.None);
        }
    }

    private async Task<bool> ExecuteSaveAsync(Func<CancellationToken, Task<bool>> callback, CancellationToken ct)
    {
        var version = _dirty.DirtyVersion;
        try
        {
            var ok = await callback(ct).ConfigureAwait(false);
            if (ok)
            {
                _dirty.MarkClean(version);
            }
            return ok;
        }
        catch (Exception ex)
        {
            AutosaveFailed?.Invoke(this, new AutosaveFailedEventArgs(ex, _clock.UtcNow));
            return false;
        }
    }

    /// <summary>仅测试可见：让 timer 立即触发而不需要等真实时间流逝。</summary>
    internal void FireTimerForTest()
    {
        // 取出 callback + tcs 之后模拟 OnTimer
        Func<CancellationToken, Task<bool>>? callback;
        TaskCompletionSource<bool>? tcs;
        lock (_lock)
        {
            tcs = _pendingTcs;
            callback = _saveCallback;
            _pendingTcs = null;
            DisposeTimerLocked();
        }
        tcs?.TrySetResult(true);
        if (callback is not null)
        {
            _ = ExecuteSaveAsync(callback, CancellationToken.None);
        }
    }
}
