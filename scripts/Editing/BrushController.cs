using Godot;

namespace WorldView.Editing;

/// <summary>
/// 笔刷节流器（见 tasks.md T12 + spec.md §3.5）。
///
/// 约束：同一格 100 ms 内只落一次笔，避免拖动时把撤销栈刷爆。
/// </summary>
public sealed class BrushController
{
    /// <summary>节流窗口（毫秒）。</summary>
    public const int ThrottleMs = 100;

    private ulong _lastStampMsec;

    /// <summary>上次成功落笔的时间戳（毫秒）。</summary>
    public ulong LastStampMsec => _lastStampMsec;

    /// <summary>
    /// 尝试落笔：距上次成功落笔 ≥ <see cref="ThrottleMs"/> 才返回 true 并记录时间戳。
    /// </summary>
    public bool TryStamp()
    {
        var now = Time.GetTicksMsec();

        if (_lastStampMsec != 0 && now - _lastStampMsec < ThrottleMs)
        {
            return false;
        }

        _lastStampMsec = now;
        return true;
    }

    /// <summary>重置节流窗口（切换工具 / 切换地图时调用）。</summary>
    public void Reset() => _lastStampMsec = 0;
}
