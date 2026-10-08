using WorldView.Domain;
using WorldView.Domain.Generation;
using WorldView.Storage.Yaml;
using Xunit;

namespace WorldView.Tests.Storage;

/// <summary>
/// Task 8 (plan L301-319) 闭环测试。
///
/// 目标：把新生成的 <see cref="World"/> 通过 <see cref="Editor.EditorContext.AddMap"/>
/// 追加进工程 <c>map_index</c>，并经 <see cref="ProjectSaver"/> 落盘 / <see cref="ProjectLoader"/>
/// 重载能完整还原。同时覆盖 ProjectSaver 的「silent data loss 陷阱」：<c>maps.Count &gt;
/// MapIndex.Count</c> 时多出来的图不写入。
///
/// R12（YamlDotNet null-skip）：<c>GenerationSpec? Generation = null</c> 序列化时不应
/// 出现 <c>generation:</c> 键——保证旧工程（无 generation 字段）能继续保持「缺省即未指定」的语义。
/// </summary>
public class GeneratedWorldPersistenceTests
{
    // ------------------------------------------------------------------ 1) AppendMap_AddsEntry_AtEnd

    [Fact]
    public void AppendMap_AddsEntry_AtEnd()
    {
        var world = NewWorldWithMapIndex(new MapIndexEntry("maps/world/existing.yaml"));
        var updated = WorldIndex.AppendMap(world, "maps/world/new.yaml");

        Assert.Equal(2, updated.MapIndex.Count);
        Assert.Equal("maps/world/existing.yaml", updated.MapIndex[0].RefPath);
        Assert.Equal("maps/world/new.yaml", updated.MapIndex[1].RefPath);

        // 不可变性：原 World 不应被改写。
        Assert.Single(world.MapIndex);
    }

    // ------------------------------------------------------------------ 2) AppendMap_OnNullIndex_CreatesIndex

    [Fact]
    public void AppendMap_OnNullIndex_CreatesIndex()
    {
        // MapIndex 是可空引用（Domain 端用 null! 占位），构造时显式传 null。
        // 用 (MapIndexEntry?)null! 强制作为 params 数组的一个元素，避免与「把 null 整个当数组」歧义。
        var world = NewWorldWithMapIndex((MapIndexEntry?)null!);
        var updated = WorldIndex.AppendMap(world, "maps/world/first.yaml");

        Assert.NotNull(updated.MapIndex);
        Assert.Single(updated.MapIndex);
        Assert.Equal("maps/world/first.yaml", updated.MapIndex[0].RefPath);
    }

    // ------------------------------------------------------------------ 3) SaveGeneratedWorld_RoundTrips

    [Fact]
    public void SaveGeneratedWorld_RoundTrips()
    {
        var scratch = CreateScratchDir();
        try
        {
            // 构造一组非默认 GenerationSpec（覆盖 7 字段）。
            const long pinnedSeed = unchecked((long)0xCAFEBABEDEADBEEFul);
            var spec = new GenerationSpec(
                Algorithm: "coastline_v1",
                Seed: pinnedSeed,
                LandRatio: 0.42,
                Ruggedness: 0.6,
                MinIslandCells: 12,
                MinLakeCells: 9,
                Erosion: 2);

            // 模拟 WorldGenerator 产出的稀疏 cells（不是默认值 Water 的也混几个）。
            var generatedCells = new List<TerrainCellEntry>
            {
                new(new GridCoord(0, 0), TerrainType.Plain),
                new(new GridCoord(5, 7), TerrainType.Mountain),
                new(new GridCoord(3, 4), TerrainType.Forest),
            };

            var map = new MapDoc(
                Id: "map_world_gen",
                Name: "生成世界测试",
                Type: MapType.World,
                Grid: new GridSpec(
                    Width: 64,
                    Height: 48,
                    CellDistance: 10.0,
                    Origin: new GridCoord(0, 0)),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(
                    Encoding: "sparse",
                    Default: TerrainDefaults.Water,
                    CellsRef: null,
                    Cells: generatedCells),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: Array.Empty<CityRef>(),
                Buildings: Array.Empty<Building>(),
                Generation: spec);

            var world = NewWorldWithMapIndex(
                new MapIndexEntry("maps/world/gen_test/main.yaml"));

            // 落盘 + 重载
            ProjectSaver.Save(scratch, world, new[] { map });
            var loaded = ProjectLoader.Load(scratch);

            // 仅 1 张图（与 MapIndex 同步）。
            Assert.Single(loaded.Maps);
            var loadedMap = loaded.Maps[0];

            // cells 数量与内容一致（顺序由 saver 决定，转换为集合比较）。
            Assert.NotNull(loadedMap.TerrainData.Cells);
            Assert.Equal(generatedCells.Count, loadedMap.TerrainData.Cells!.Count);
            var expectedCells = generatedCells.Select(c => (c.At, c.Type)).ToHashSet();
            var actualCells = loadedMap.TerrainData.Cells!.Select(c => (c.At, c.Type)).ToHashSet();
            Assert.True(expectedCells.SetEquals(actualCells),
                $"cells 集合不一致: 期望 {string.Join(",", expectedCells)} 实际 {string.Join(",", actualCells)}");

            // Generation 字段逐字段一致。
            Assert.NotNull(loadedMap.Generation);
            Assert.Equal(spec.Algorithm, loadedMap.Generation!.Algorithm);
            Assert.Equal(spec.Seed, loadedMap.Generation.Seed);
            Assert.Equal(spec.LandRatio, loadedMap.Generation.LandRatio);
            Assert.Equal(spec.Ruggedness, loadedMap.Generation.Ruggedness);
            Assert.Equal(spec.MinIslandCells, loadedMap.Generation.MinIslandCells);
            Assert.Equal(spec.MinLakeCells, loadedMap.Generation.MinLakeCells);
            Assert.Equal(spec.Erosion, loadedMap.Generation.Erosion);

            // Default 与 Encoding 守门。
            Assert.Equal(TerrainType.Water, loadedMap.TerrainData.Default?.Type);
            Assert.Equal("sparse", loadedMap.TerrainData.Encoding);
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    // ------------------------------------------------------------------ 4) MapsBeyondMapIndex_AreNotWritten

    [Fact]
    public void MapsBeyondMapIndex_AreNotWritten()
    {
        var scratch = CreateScratchDir();
        try
        {
            // MapIndex 只有 1 项，但传入 2 张图：saver 只写索引里的那张。
            var world = NewWorldWithMapIndex(
                new MapIndexEntry("maps/world/only_one/main.yaml"));

            var mapA = MakeTinyMap("map_a", "地图A");
            var mapB = MakeTinyMap("map_b", "地图B");

            ProjectSaver.Save(scratch, world, new[] { mapA, mapB });
            var loaded = ProjectLoader.Load(scratch);

            // 钉死「silent data loss 陷阱」：mapB 不会落盘。
            Assert.Single(loaded.Maps);
            Assert.Equal("map_a", loaded.Maps[0].Id);
            Assert.DoesNotContain(loaded.Maps, m => m.Id == "map_b");

            // 索引也只多出 1 项。
            Assert.Single(loaded.World.MapIndex);
        }
        finally
        {
            DeleteDirectory(scratch);
        }
    }

    // ------------------------------------------------------------------ 5) Generation_OmittedFromYaml_WhenNull (R12)

    [Fact]
    public void Generation_OmittedFromYaml_WhenNull()
    {
        var map = new MapDoc(
            Id: "map_test",
            Name: "无 generation 测试",
            Type: MapType.World,
            Grid: new GridSpec(
                Width: 8,
                Height: 8,
                CellDistance: 1.0,
                Origin: new GridCoord(0, 0)),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>(),
            Generation: null);

        var yaml = YamlSchemas.Serialize(map);

        // R12：nullable 字段为 null 时序列化应**完全省略 key**（snake_case 命名约定）。
        Assert.DoesNotContain("generation:", yaml);
    }

    // ------------------------------------------------------------------ helpers

    private static World NewWorldWithMapIndex(params MapIndexEntry?[] entries)
    {
        // 哨兵：当 entries[0] 为 null 且仅一个元素时，构造 MapIndex = null 的 World。
        var seed = new World(); // 走无参构造器，DefaultTime / Era / CharacterIndex 全部填好默认
        if (entries.Length == 1 && entries[0] is null)
        {
            return seed with { MapIndex = null! };
        }

        var list = new List<MapIndexEntry>();
        foreach (var e in entries)
        {
            if (e is not null) list.Add(e);
        }
        return seed with { MapIndex = list };
    }

    private static MapDoc MakeTinyMap(string id, string name) =>
        new(
            Id: id,
            Name: name,
            Type: MapType.World,
            Grid: new GridSpec(
                Width: 8,
                Height: 8,
                CellDistance: 1.0,
                Origin: new GridCoord(0, 0)),
            Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
            Presentation: new PresentationSpec(),
            TerrainData: new TerrainData(),
            Layers: Array.Empty<LayerRef>(),
            CityRefs: Array.Empty<CityRef>(),
            Buildings: Array.Empty<Building>(),
            Generation: null);

    private static string CreateScratchDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "worldview-gen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteDirectory(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
