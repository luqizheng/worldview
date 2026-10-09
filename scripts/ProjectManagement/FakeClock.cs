using System;

namespace WorldView.ProjectManagement;

/// <summary>
/// 测试用可调时钟。构造时给一个起点；
/// 测试代码可调用 <see cref="Advance"/> 或 <see cref="Set"/> 推动时间。
/// </summary>
public sealed class FakeClock : IClock
{
    private DateTime _current;

    public FakeClock(DateTime initialUtc)
    {
        _current = initialUtc.Kind switch
        {
            DateTimeKind.Utc => initialUtc,
            DateTimeKind.Local => initialUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(initialUtc, DateTimeKind.Utc),
        };
    }

    public DateTime UtcNow => _current;

    /// <summary>向前推进时间。</summary>
    public void Advance(TimeSpan delta) => _current = _current + delta;

    /// <summary>显式设置时间（传入 UTC 时刻）。</summary>
    public void Set(DateTime utcMoment)
    {
        _current = utcMoment.Kind switch
        {
            DateTimeKind.Utc => utcMoment,
            DateTimeKind.Local => utcMoment.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcMoment, DateTimeKind.Utc),
        };
    }
}
