using WorldView.Domain;
using WorldView.Domain.Validation;
using Xunit;

namespace WorldView.Tests.Validation;

public class MapValidatorGenerationTests
{
    private static string ScratchDir => Path.Combine(Path.GetTempPath(), "worldview-validator-gen-tests");
    private static readonly GridSpec DefaultGrid = new(64, 48, 10.0);
    private static readonly TimeSpec DefaultTime = new(TimeGranularity.Month, TimeGranularity.Day);
    private static readonly PresentationSpec DefaultPres = new();
    private static readonly TerrainData DefaultTerrain = new();
    private static readonly LayerRef[] NoLayers = Array.Empty<LayerRef>();
    private static readonly CityRef[] NoCityRefs = Array.Empty<CityRef>();
    private static readonly Building[] NoBuildings = Array.Empty<Building>();

    [Fact]
    public void Water_Constant_Is_NotBuildable()
    {
        var w = TerrainDefaults.Water;
        Assert.Equal(TerrainType.Water, w.Type);
        Assert.False(w.Buildable);
        Assert.Equal(999, w.MoveCost);
    }

    [Fact]
    public void GenerationParam_OutOfRange_Produces_Warning()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        // land_ratio 0.9 超出 [0.05, 0.80]
        var spec = new GenerationSpec(
            Algorithm: "coastline_v1",
            Seed: 12345,
            LandRatio: 0.9,
            Ruggedness: 0.5,
            MinIslandCells: 8,
            MinLakeCells: 6,
            Erosion: 1);
        var map = new MapDoc(
            Id: "m", Name: "M", Type: MapType.World,
            Grid: DefaultGrid, Time: DefaultTime, Presentation: DefaultPres,
            TerrainData: DefaultTerrain, Layers: NoLayers, CityRefs: NoCityRefs, Buildings: NoBuildings,
            Generation: spec);
        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.True(result.IsValid, result.ToString());              // 警告不阻塞
        Assert.Empty(result.Errors);
        Assert.Contains(result.Warnings, w => w.Code == "W_GENERATION_PARAM");
    }

    [Fact]
    public void GenerationParam_InRange_Produces_No_Warning()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        var spec = new GenerationSpec(
            Algorithm: "coastline_v1",
            Seed: 12345,
            LandRatio: 0.35,
            Ruggedness: 0.5,
            MinIslandCells: 8,
            MinLakeCells: 6,
            Erosion: 1);
        var map = new MapDoc(
            Id: "m", Name: "M", Type: MapType.World,
            Grid: DefaultGrid, Time: DefaultTime, Presentation: DefaultPres,
            TerrainData: DefaultTerrain, Layers: NoLayers, CityRefs: NoCityRefs, Buildings: NoBuildings,
            Generation: spec);
        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "W_GENERATION_PARAM");
    }

    [Fact]
    public void MapWithout_Generation_Produces_No_Warning()
    {
        if (!Directory.Exists(ScratchDir)) Directory.CreateDirectory(ScratchDir);
        // 不传 Generation（默认 null）
        var map = new MapDoc(
            Id: "m", Name: "M", Type: MapType.World,
            Grid: DefaultGrid, Time: DefaultTime, Presentation: DefaultPres,
            TerrainData: DefaultTerrain, Layers: NoLayers, CityRefs: NoCityRefs, Buildings: NoBuildings);
        var result = new MapValidator().Validate(map, "m.yaml", ScratchDir);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "W_GENERATION_PARAM");
    }
}