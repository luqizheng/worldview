using WorldView.Domain;
using Xunit;

namespace WorldView.Tests.Domain;

/// <summary>
/// MapDoc / GridSpec / TimeSpec / PresentationSpec / TerrainData 的防御性默认值,
/// 保证 YamlDotNet 反序列化不会触发 NRE(spec.md §6.5)。
/// 对应 checklist A5(领域层无 Godot 依赖)+ 项目根目录 default ctor 一致性。
/// </summary>
public class MapDocDefaultsTest
{
    [Fact]
    public void MapDoc_DefaultCtor_Is_Safe_To_Access_All_Fields()
    {
        var doc = new MapDoc();
        Assert.Equal(string.Empty, doc.Id);
        Assert.Equal(string.Empty, doc.Name);
        Assert.Equal(MapType.World, doc.Type);
        Assert.NotNull(doc.Grid);
        Assert.NotNull(doc.Time);
        Assert.NotNull(doc.Presentation);
        Assert.NotNull(doc.TerrainData);
        Assert.NotNull(doc.Layers);
        Assert.NotNull(doc.CityRefs);
        Assert.NotNull(doc.Buildings);
        Assert.Empty(doc.Layers);
        Assert.Empty(doc.CityRefs);
        Assert.Empty(doc.Buildings);
    }

    [Fact]
    public void GridSpec_DefaultCtor_Has_Safe_Defaults()
    {
        var g = new GridSpec();
        Assert.Equal(0, g.Width);
        Assert.Equal(0, g.Height);
        Assert.Equal(1.0, g.CellDistance);
        Assert.Equal("m", g.DistanceUnit);
        Assert.Equal("m", g.BaseUnit);
        Assert.Equal("m", g.HeightUnit);
        Assert.Null(g.Origin);
        Assert.True(g.Show);
        Assert.True(g.Snap);
        Assert.Null(g.ScaleLabel);
    }

    [Fact]
    public void TimeSpec_DefaultCtor_Matches_World_Defaults()
    {
        var t = new TimeSpec();
        Assert.Equal(TimeGranularity.Month, t.DefaultGranularity);
        Assert.Equal(TimeGranularity.Day, t.MinGranularity);
        Assert.Null(t.DisplayFormat);
        Assert.True(t.InheritCalendar);
        Assert.True(t.AllowFuzzy);
    }

    [Fact]
    public void PresentationSpec_DefaultCtor_Uses_MVP_Literals()
    {
        var p = new PresentationSpec();
        Assert.Equal("edit", p.DefaultMode);
        Assert.Equal("2d", p.ViewMode); // MVP 唯一字面量,见 spec.md §2.2
        Assert.Equal("parchment", p.Theme);
        Assert.True(p.ShowScaleBar);
        Assert.True(p.ShowCoordinates);
        Assert.False(p.ShowMinimap);
    }

    [Fact]
    public void TerrainData_DefaultCtor_Uses_Sparse_With_Plain_Default()
    {
        var t = new TerrainData();
        Assert.Equal("sparse", t.Encoding);
        Assert.NotNull(t.Default);
        Assert.Equal(TerrainType.Plain, t.Default!.Type);
        Assert.Null(t.CellsRef);
        Assert.Empty(t.Cells);
    }

    [Fact]
    public void World_DefaultCtor_Is_Safe()
    {
        var w = new World();
        Assert.Equal("0.1", w.ProjectVersion);
        Assert.Equal(string.Empty, w.Name);
        Assert.NotNull(w.DefaultTime);
        Assert.NotNull(w.Era);
        Assert.NotNull(w.MapIndex);
        Assert.Empty(w.MapIndex);
    }

    [Fact]
    public void EraSpec_DefaultCtor_Uses_Gregorian_Calendar()
    {
        var era = new EraSpec();
        Assert.Equal(12, era.MonthsPerYear);
        Assert.Equal(24, era.HoursPerDay);
        Assert.Equal(60, era.MinutesPerHour);
        Assert.Equal(60, era.SecondsPerMinute);
        Assert.Equal(12, era.DaysPerMonth.Count);
        Assert.Equal(31, era.DaysPerMonth[0]);
        Assert.Equal(28, era.DaysPerMonth[1]);
        Assert.NotNull(era.Leap);
        Assert.Equal(4, era.Leap!.Every);
        Assert.Equal(2, era.Leap.Month);
        Assert.Equal(1, era.Leap.AddDays);
    }
}
