using WorldView.Domain.Time;

namespace WorldView.Domain.Tests.Time;

public class HistoricalTimeTests
{
    [Fact]
    public void Constructor_AcceptsEpochAndDayBoundary()
    {
        var epoch = new HistoricalTime(0, 0);
        Assert.Equal(0, epoch.DayNumber);
        Assert.Equal(0, epoch.OffsetSeconds);

        var dayEnd = new HistoricalTime(12, HistoricalTime.MaxOffsetSeconds);
        Assert.Equal(86_399, dayEnd.OffsetSeconds);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 86_400)]
    [InlineData(5, 86_399 + 1)]
    public void Constructor_RejectsOutOfRange(long day, int offset)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new HistoricalTime(day, offset));
    }

    [Fact]
    public void TryCreate_MatchesConstructor()
    {
        Assert.True(HistoricalTime.TryCreate(3, 100, out var ok));
        Assert.NotNull(ok);
        Assert.False(HistoricalTime.TryCreate(-1, 0, out var bad));
        Assert.Null(bad);
        Assert.False(HistoricalTime.TryCreate(0, 86_400, out bad));
        Assert.Null(bad);
    }

    [Theory]
    [InlineData("0:0", 0, 0)]
    [InlineData("1200:43200", 1200, 43200)]
    [InlineData("0005:0009", 5, 9)] // 前导零可解析，规范化后不带
    public void TryParse_AcceptsCanonicalFormat(string text, long expectedDay, int expectedOffset)
    {
        Assert.True(HistoricalTime.TryParse(text, out var value));
        Assert.NotNull(value);
        Assert.Equal(expectedDay, value.DayNumber);
        Assert.Equal(expectedOffset, value.OffsetSeconds);
        Assert.Equal($"{expectedDay}:{expectedOffset}", value.Canonical());
    }

    [Theory]
    [InlineData("")]
    [InlineData("5")]
    [InlineData(":5")]
    [InlineData("5:")]
    [InlineData("a:b")]
    [InlineData("-1:5")]
    [InlineData("5:-1")]
    [InlineData(" 5:5")]
    [InlineData("5: 5")]
    [InlineData("5:5 ")]
    [InlineData("+5:5")]
    [InlineData("5:86400")]
    [InlineData("18446744073709551616:0")] // long 溢出
    [InlineData("5:5:5")]
    public void TryParse_RejectsInvalidInput(string text)
    {
        Assert.False(HistoricalTime.TryParse(text, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(HistoricalTime.TryParse(null, out var value));
        Assert.Null(value);
        Assert.False(HistoricalTime.TryParse(string.Empty, out value));
        Assert.Null(value);
    }

    [Fact]
    public void Comparison_IsLexicographicByDayThenOffset()
    {
        var a = new HistoricalTime(5, 0);
        var b = new HistoricalTime(5, 1);
        var c = new HistoricalTime(6, 0);

        Assert.True(a.IsBefore(b));
        Assert.True(b.IsBefore(c));
        Assert.True(c.IsAfter(a));
        Assert.True(a.IsAfter(a) == false);
        Assert.Equal(0, a.CompareTo(a));
        Assert.Equal(0, a.CompareTo(new HistoricalTime(5, 0)));
    }

    [Fact]
    public void Equality_ByDayAndOffset()
    {
        var a = new HistoricalTime(7, 100);
        var b = new HistoricalTime(7, 100);
        var c = new HistoricalTime(7, 101);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Canonical_NoLeadingZeros()
    {
        Assert.Equal("5:9", new HistoricalTime(5, 9).Canonical());
        Assert.Equal("0:0", HistoricalTime.Epoch.Canonical());
    }
}
