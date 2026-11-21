using System;
using System.Globalization;

namespace WorldView.Domain.Time;

/// <summary>
/// 规范历史时间（P2-09-11 V0.11 §3.3 / P2-09-14 V0.2）：Map 相对离散整数对
/// （day_number ≥ 0，offset_seconds ∈ [0, 86399]），字典序即全序；
/// day zero 是 Map 相对的，Branch 完整继承根 Map 时间轴。
/// 事务时间戳/UTC/宿主本地时间绝不是 Historical Time。
/// 端口输入/输出采用规范文本格式 "{day_number}:{offset_seconds}"（如 "1200:43200"）；
/// 解析与规范化由本类型独占拥有（AF-02 端口契约：Application 层不得重新实现领域规则）。
/// </summary>
public sealed class HistoricalTime : IComparable<HistoricalTime>, IEquatable<HistoricalTime>
{
    /// <summary>一天内的秒数上限（不含）。</summary>
    public const int MaxOffsetSeconds = 86_399;

    /// <summary>Map 纪元（第 0 天 0 秒）；合法查询下限。</summary>
    public static HistoricalTime Epoch { get; } = new(0, 0);

    /// <summary>从纪元起的天数（Map 相对）。</summary>
    public long DayNumber { get; }

    /// <summary>当天内的秒偏移。</summary>
    public int OffsetSeconds { get; }

    /// <summary>已通过校验的历史时间；违反范围时抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
    public HistoricalTime(long dayNumber, int offsetSeconds)
    {
        if (dayNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayNumber), "day_number 不能为负。");
        }

        if (offsetSeconds is < 0 or > MaxOffsetSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds), $"offset_seconds 必须在 0..{MaxOffsetSeconds}。");
        }

        DayNumber = dayNumber;
        OffsetSeconds = offsetSeconds;
    }

    /// <summary>尝试构造；范围非法时返回 false。</summary>
    public static bool TryCreate(long dayNumber, int offsetSeconds, out HistoricalTime? value)
    {
        if (dayNumber < 0 || offsetSeconds is < 0 or > MaxOffsetSeconds)
        {
            value = null;
            return false;
        }

        value = new HistoricalTime(dayNumber, offsetSeconds);
        return true;
    }

    /// <summary>
    /// 解析规范文本格式 "{day}:{offset}"（十进制非负整数，无符号、无空白；
    /// 允许前导零，规范化输出不带前导零）。格式或范围非法时返回 false。
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> text, out HistoricalTime? value)
    {
        value = null;
        var separator = text.IndexOf(':');
        if (separator <= 0 || separator == text.Length - 1)
        {
            return false;
        }

        var daySpan = text[..separator];
        var offsetSpan = text[(separator + 1)..];
        if (daySpan.Length == 0 || offsetSpan.Length == 0)
        {
            return false;
        }

        if (!long.TryParse(daySpan, NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || !int.TryParse(offsetSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
        {
            return false;
        }

        return TryCreate(day, offset, out value);
    }

    /// <summary>解析字符串重载（null/空白返回 false）。</summary>
    public static bool TryParse(string? text, out HistoricalTime? value)
    {
        value = null;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return TryParse(text.AsSpan(), out value);
    }

    /// <summary>规范文本形式 "{day}:{offset}"。</summary>
    public string Canonical() => string.Create(
        CultureInfo.InvariantCulture,
        $"{DayNumber}:{OffsetSeconds}");

    /// <summary>字典序比较：(day_number, offset_seconds)。</summary>
    public int CompareTo(HistoricalTime? other)
    {
        if (other is null)
        {
            return 1;
        }

        var day = DayNumber.CompareTo(other.DayNumber);
        return day != 0 ? day : OffsetSeconds.CompareTo(other.OffsetSeconds);
    }

    /// <summary>本时间是否严格早于 <paramref name="other"/>。</summary>
    public bool IsBefore(HistoricalTime other) => CompareTo(other) < 0;

    /// <summary>本时间是否严格晚于 <paramref name="other"/>（ADR-005 规则 7：Branch 自有变更 &gt; parent_at）。</summary>
    public bool IsAfter(HistoricalTime other) => CompareTo(other) > 0;

    public bool Equals(HistoricalTime? other) =>
        other is not null && DayNumber == other.DayNumber && OffsetSeconds == other.OffsetSeconds;

    public override bool Equals(object? obj) => Equals(obj as HistoricalTime);

    public override int GetHashCode() => HashCode.Combine(DayNumber, OffsetSeconds);

    public override string ToString() => Canonical();
}
