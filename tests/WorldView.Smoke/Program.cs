using System.Collections;
using System.Text;
using WorldView.Domain;
using WorldView.Domain.Validation;
using WorldView.Storage.Yaml;

namespace WorldView.Smoke;

/// <summary>
/// WorldView 控制台冒烟运行器（Section A: 复刻既有单测断言；Section B: 真实样例工程端到端）。
///
/// 为什么不用 <c>dotnet test</c>：本机 VSTest 在
/// <c>Microsoft.VisualStudio.TestPlatform.TestHost.DefaultEngineInvoker.SetParentProcessExitCallback</c>
/// → <c>Process.GetProcessHandle</c> → <c>EnableRaisingEvents</c> 处抛
/// <c>System.ComponentModel.Win32Exception (5): 拒绝访问</c>；workspace-write 与一次性
/// danger-full-access 下表现一致，且 <c>dotnet &lt;testdll&gt;.dll</c> 无入口点（静默 exit 0）。
/// <c>dotnet build</c> / <c>dotnet run</c> 均正常，故改用本运行器。
///
/// 用法：<c>dotnet run --project tests\WorldView.Smoke</c>（从仓库根运行）
/// 退出码：全部通过 0，否则 1。
/// </summary>
internal static class Program
{
    /// <summary>真实样例工程根。</summary>
    private const string SampleRoot = @"D:\kudi-project\world-view\examples\sample_world";

    private static int _passed;
    private static int _failed;
    private static readonly List<string> Failures = new();

    private static int Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { /* 重定向时可能失败，忽略 */ }

        Console.WriteLine("=== WorldView smoke runner ===");
        Console.WriteLine($"version  : {typeof(Program).Assembly.GetName().Version}");
        Console.WriteLine($"cwd      : {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"sample   : {SampleRoot}");

        RunSectionA();
        RunSectionB();

        Console.WriteLine();
        Console.WriteLine("=== 汇总 ===");
        if (Failures.Count > 0)
        {
            Console.WriteLine($"失败项（{Failures.Count}）:");
            foreach (var f in Failures) Console.WriteLine($"  - {f}");
        }

        Console.WriteLine($"passed {_passed} / failed {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // SECTION A —— 复刻 tests/WorldView.Tests 下的既有断言
    // ------------------------------------------------------------------

    private static void RunSectionA()
    {
        Console.WriteLine();
        Console.WriteLine("--- SECTION A: Domain / Storage / Validation 单测断言复刻 ---");

        // ---- A1: tests/WorldView.Tests/Domain/TerrainCellRecordTest.cs ----

        Check("A1.1 Plain_DefaultCell_Has_Expected_Values", () =>
        {
            var cell = TerrainDefaults.Plain;
            Eq(TerrainType.Plain, cell.Type, "cell.Type");
            Eq(1, cell.MoveCost, "cell.MoveCost");
            True(cell.Buildable, "cell.Buildable 应为 true");
        });

        Check("A1.2 RecordEquality_Two_Plain_Cells_Are_Equal", () =>
        {
            var a = new TerrainCell(TerrainType.Plain, 1, true);
            var b = new TerrainCell(TerrainType.Plain, 1, true);
            Eq(a, b, "TerrainCell.Equals");
            True(a == b, "operator ==");
            Eq(a.GetHashCode(), b.GetHashCode(), "GetHashCode");
        });

        Check("A1.3 RecordInequality_Different_Terrain_Not_Equal", () =>
        {
            var a = new TerrainCell(TerrainType.Plain, 1, true);
            var b = new TerrainCell(TerrainType.Mountain, 2, false);
            Ne(a, b, "TerrainCell");
        });

        // [Theory] + [InlineData] 三条展开为三个独立检查
        Check("A1.4 ColorHex_Returns_Spec_Color(Plain, #c2b280)", () =>
            Eq("#c2b280", TerrainDefaults.ColorHex(TerrainType.Plain), "ColorHex(Plain)"));

        Check("A1.5 ColorHex_Returns_Spec_Color(Mountain, #7a6f5c)", () =>
            Eq("#7a6f5c", TerrainDefaults.ColorHex(TerrainType.Mountain), "ColorHex(Mountain)"));

        Check("A1.6 ColorHex_Returns_Spec_Color(Water, #3a6ea5)", () =>
            Eq("#3a6ea5", TerrainDefaults.ColorHex(TerrainType.Water), "ColorHex(Water)"));

        // ---- A2: tests/WorldView.Tests/Domain/BuildingRecordTest.cs ----

        Check("A2.1 GridCoord_Equality_And_Hash", () =>
        {
            var a = new GridCoord(3, 4);
            var b = new GridCoord(3, 4);
            var c = new GridCoord(4, 3);
            Eq(a, b, "GridCoord.Equals");
            True(a == b, "operator ==");
            False(a == c, "operator == 应为 false");
            True(a != c, "operator != 应为 true");
            Eq(a.GetHashCode(), b.GetHashCode(), "GetHashCode");
        });

        Check("A2.2 Building_Record_Equality_By_All_Fields", () =>
        {
            var a = new Building("b_kael_house_01", BuildingType.House, new GridCoord(20, 30), "map_city_kael");
            var b = new Building("b_kael_house_01", BuildingType.House, new GridCoord(20, 30), "map_city_kael");
            var c = new Building("b_kael_tower_01", BuildingType.Tower, new GridCoord(32, 32), "map_city_kael");
            Eq(a, b, "Building.Equals");
            True(a == b, "operator ==");
            Ne(a, c, "Building 不同字段");
        });

        // ---- A3: tests/WorldView.Tests/Storage/YamlSchemasTest.cs ----

        // 说明：上游 fixture（YamlSchemasTest.cs:15）里写的是 `id: test`，
        // 但同一测试第 17 行断言的是 "map_test"。此处按 fixture 真值断言，差异见报告。
        const string mapFixtureYaml =
            "id: test\nname: T\ntype: world\ngrid:\n  width: 64\n  height: 48\n  cell_distance: 10.0\n  distance_unit: m\nbase_unit: m\nheight_unit: m\ntime:\n  default_granularity: month\n  min_granularity: day\npresentation: {}\nterrain_data:\n  encoding: sparse\n  default:\n    type: plain\n    move_cost: 1\n    buildable: true\nlayers: []\ncity_refs: []\nbuildings: []\n";

        Check("A3.1 Deserialize_MapDoc_Grid_And_Header", () =>
        {
            var map = YamlSchemas.Deserialize<MapDoc>(mapFixtureYaml);
            Eq("test", map.Id, "map.Id（fixture 真值）");
            Eq(64, map.Grid.Width, "map.Grid.Width");
            Eq(48, map.Grid.Height, "map.Grid.Height");
            Eq("m", map.Grid.DistanceUnit, "map.Grid.DistanceUnit");
        });

        Check("A3.2 Roundtrip_TerrainCell_Entry_Preserves_At", () =>
        {
            const string yaml = "- at: [10, 20]\n  type: mountain\n  move_cost: 4\n  buildable: false\n- at: [3, 4]\n  type: forest\n";
            var cells = YamlSchemas.Deserialize<List<TerrainCellEntry>>(yaml);
            Eq(2, cells.Count, "cells.Count");
            Eq(new GridCoord(10, 20), cells[0].At, "cells[0].At");
            Eq(TerrainType.Mountain, cells[0].Type, "cells[0].Type");
            Eq(4, cells[0].MoveCost, "cells[0].MoveCost");
            False(cells[0].Buildable!.Value, "cells[0].Buildable");

            var roundtrip = YamlSchemas.Serialize(cells);
            True(roundtrip.Contains("[10, 20]"), $"写回应含 \"[10, 20]\"，实际:\n{roundtrip}");
            True(roundtrip.Contains("[3, 4]"), $"写回应含 \"[3, 4]\"，实际:\n{roundtrip}");
        });

        Check("A3.3 Deserialize_World_Project_Header", () =>
        {
            const string yaml = "project_version: \"0.1\"\nname: 测试\ndefault_time:\n  year: 1042\n  month: 3\n  day: 12\nera:\n  name: 创世历\n  start_year: 1\n  months_per_year: 12\n  days_per_month: [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]\nmap_index:\n  - ref: maps/world/main.yaml\ncharacter_index: []\n";
            var world = YamlSchemas.Deserialize<World>(yaml);
            Eq("0.1", world.ProjectVersion, "world.ProjectVersion");
            Eq("测试", world.Name, "world.Name");
            Eq(1042, world.DefaultTime.Year, "world.DefaultTime.Year");
            Eq(12, world.Era.DaysPerMonth.Count, "world.Era.DaysPerMonth.Count");
            Eq(1, world.MapIndex.Count, "world.MapIndex.Count");
            Eq("maps/world/main.yaml", world.MapIndex[0].RefPath, "world.MapIndex[0].RefPath");
        });

        // ---- A4: tests/WorldView.Tests/Validation/MapValidatorTest.cs ----

        var scratch = ResolveScratchDir();
        Console.WriteLine($"  scratch  : {scratch}");

        Check("A4.1 Valid_World_Map_Passes_All_Rules", () =>
        {
            Directory.CreateDirectory(scratch);
            var map = new MapDoc(
                Id: "map_test", Name: "Test", Type: MapType.World,
                Grid: new GridSpec(64, 48, 10.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: Array.Empty<CityRef>(),
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "maps/world/test.yaml", scratch);
            True(result.IsValid, $"期望 IsValid，实际:\n{result}");
        });

        Check("A4.2 Terrain_Cell_Out_Of_Bounds_Fails -> E_GRID_OOB", () =>
        {
            Directory.CreateDirectory(scratch);
            var cell = new TerrainCellEntry(new GridCoord(100, 100), TerrainType.Mountain);
            var map = new MapDoc(
                Id: "map_oob", Name: "OOB", Type: MapType.World,
                Grid: new GridSpec(64, 48, 10.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(Cells: new[] { cell }),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: Array.Empty<CityRef>(),
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "maps/world/oob.yaml", scratch);
            False(result.IsValid, "期望 IsValid == false");
            True(result.Errors.Any(e => e.Code == "E_GRID_OOB"), $"期望含 E_GRID_OOB，实际错误码: {Codes(result)}");
        });

        Check("A4.3 Terrain_Cell_Duplicate_Fails -> E_TERRAIN_CELL_DUP", () =>
        {
            Directory.CreateDirectory(scratch);
            var c1 = new TerrainCellEntry(new GridCoord(10, 10), TerrainType.Forest);
            var c2 = new TerrainCellEntry(new GridCoord(10, 10), TerrainType.Mountain);
            var map = new MapDoc(
                Id: "map_dup", Name: "Dup", Type: MapType.World,
                Grid: new GridSpec(64, 48, 10.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(Cells: new[] { c1, c2 }),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: Array.Empty<CityRef>(),
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "maps/world/dup.yaml", scratch);
            False(result.IsValid, "期望 IsValid == false");
            True(result.Errors.Any(e => e.Code == "E_TERRAIN_CELL_DUP"), $"期望含 E_TERRAIN_CELL_DUP，实际错误码: {Codes(result)}");
        });

        Check("A4.4 Ref_Absolute_Path_Fails -> E_REF_ABSOLUTE", () =>
        {
            Directory.CreateDirectory(scratch);
            var cityRef = new CityRef(
                Id: "city_1", Name: "X", X: 10, Y: 10,
                Entry: new GridCoord(10, 10),
                MapRef: "C:\\abs\\path\\city.yaml");
            var map = new MapDoc(
                Id: "m", Name: "M", Type: MapType.World,
                Grid: new GridSpec(64, 48, 10.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: new[] { cityRef },
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "m.yaml", scratch);
            False(result.IsValid, "期望 IsValid == false");
            True(result.Errors.Any(e => e.Code == "E_REF_ABSOLUTE"), $"期望含 E_REF_ABSOLUTE，实际错误码: {Codes(result)}");
        });

        Check("A4.5 Ref_Parent_DotDot_Fails -> E_REF_PARENT", () =>
        {
            Directory.CreateDirectory(scratch);
            var cityRef = new CityRef(
                Id: "city_1", Name: "X", X: 10, Y: 10,
                Entry: new GridCoord(10, 10),
                MapRef: "../outside/city.yaml");
            var map = new MapDoc(
                Id: "m", Name: "M", Type: MapType.World,
                Grid: new GridSpec(64, 48, 10.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: new[] { cityRef },
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "m.yaml", scratch);
            False(result.IsValid, "期望 IsValid == false");
            True(result.Errors.Any(e => e.Code == "E_REF_PARENT"), $"期望含 E_REF_PARENT，实际错误码: {Codes(result)}");
        });

        Check("A4.6 Grid_Warning_When_Exceeds_Sparse_Upper_Bound -> W_GRID_TOO_LARGE_SPARSE", () =>
        {
            Directory.CreateDirectory(scratch);
            var map = new MapDoc(
                Id: "big", Name: "Big", Type: MapType.World,
                Grid: new GridSpec(2048, 2048, 1.0),
                Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
                Presentation: new PresentationSpec(),
                TerrainData: new TerrainData(),
                Layers: Array.Empty<LayerRef>(),
                CityRefs: Array.Empty<CityRef>(),
                Buildings: Array.Empty<Building>());

            var result = new MapValidator().Validate(map, "big.yaml", scratch);
            True(result.IsValid, $"超过 sparse 上限不应报错，实际:\n{result}");
            True(result.Warnings.Any(w => w.Code == "W_GRID_TOO_LARGE_SPARSE"),
                $"期望含 W_GRID_TOO_LARGE_SPARSE，实际警告码: {Codes(result, warnings: true)}");
        });
    }

    // ------------------------------------------------------------------
    // SECTION B —— 真实样例工程端到端
    // ------------------------------------------------------------------

    private static void RunSectionB()
    {
        Console.WriteLine();
        Console.WriteLine("--- SECTION B: 真实样例工程端到端（examples/sample_world）---");

        ProjectLoader.LoadResult? loaded = null;

        Check("B1 ProjectLoader.Load(sample_world) 不抛异常且加载 2 张地图", () =>
        {
            loaded = ProjectLoader.Load(SampleRoot);
            Eq(2, loaded.Maps.Count, "loaded.Maps.Count");
            Eq("map_world_kael", loaded.Maps[0].Id, "Maps[0].Id");
            Eq("map_city_kael", loaded.Maps[1].Id, "Maps[1].Id");
            Eq("示例世界", loaded.World.Name, "World.Name");
        });

        if (loaded is null)
        {
            Console.WriteLine("!! B1 加载失败，Section B 剩余检查已跳过（详见上方 FAIL）");
            return;
        }

        var world = loaded.World;
        var worldMap = loaded.Maps[0];
        var cityMap = loaded.Maps[1];

        Check("B2.1 world 地图网格 64×48 / cell_distance 10 / distance_unit km", () =>
        {
            Eq(64, worldMap.Grid.Width, "worldMap.Grid.Width");
            Eq(48, worldMap.Grid.Height, "worldMap.Grid.Height");
            Eq(10.0, worldMap.Grid.CellDistance, "worldMap.Grid.CellDistance");
            Eq("km", worldMap.Grid.DistanceUnit, "worldMap.Grid.DistanceUnit");
            Eq(MapType.World, worldMap.Type, "worldMap.Type");
        });

        Check("B2.2 city 地图网格 64×64 / cell_distance 2 / distance_unit m", () =>
        {
            Eq(64, cityMap.Grid.Width, "cityMap.Grid.Width");
            Eq(64, cityMap.Grid.Height, "cityMap.Grid.Height");
            Eq(2.0, cityMap.Grid.CellDistance, "cityMap.Grid.CellDistance");
            Eq("m", cityMap.Grid.DistanceUnit, "cityMap.Grid.DistanceUnit");
            Eq(MapType.City, cityMap.Type, "cityMap.Type");
        });

        Check("B2.3 city 地图 2 个 inline buildings（house@[20,30] + tower@[32,32]）", () =>
        {
            Eq(2, cityMap.Buildings.Count, "cityMap.Buildings.Count");
            var h = cityMap.Buildings[0];
            Eq("b_kael_house_01", h.Id, "buildings[0].Id");
            Eq(BuildingType.House, h.Type, "buildings[0].Type");
            Eq(new GridCoord(20, 30), h.At, "buildings[0].At");
            Eq("map_city_kael", h.MapId, "buildings[0].MapId");
            var t = cityMap.Buildings[1];
            Eq("b_kael_tower_01", t.Id, "buildings[1].Id");
            Eq(BuildingType.Tower, t.Type, "buildings[1].Type");
            Eq(new GridCoord(32, 32), t.At, "buildings[1].At");
            Eq("map_city_kael", t.MapId, "buildings[1].MapId");
        });

        Check("B2.4 两张地图各 4 个 layer refs（world=../layers/*.yaml）", () =>
        {
            Eq(4, worldMap.Layers.Count, "worldMap.Layers.Count");
            Eq(4, cityMap.Layers.Count, "cityMap.Layers.Count");
            Eq("../../layers/terrain.yaml", worldMap.Layers[0].RefPath, "worldMap.Layers[0].RefPath");
            Eq("../../layers/road.yaml", worldMap.Layers[1].RefPath, "worldMap.Layers[1].RefPath");
            Eq("../../layers/building.yaml", worldMap.Layers[2].RefPath, "worldMap.Layers[2].RefPath");
            Eq("../../layers/marker.yaml", worldMap.Layers[3].RefPath, "worldMap.Layers[3].RefPath");
        });

        Check("B2.5 world 地图 1 个 city_ref(city_kael) / city 地图 city_refs 为空", () =>
        {
            Eq(1, worldMap.CityRefs.Count, "worldMap.CityRefs.Count");
            var c = worldMap.CityRefs[0];
            Eq("city_kael", c.Id, "cityRefs[0].Id");
            Eq("极光港", c.Name, "cityRefs[0].Name");
            Eq(32, c.X, "cityRefs[0].X");
            Eq(24, c.Y, "cityRefs[0].Y");
            Eq(new GridCoord(32, 24), c.Entry, "cityRefs[0].Entry");
            Eq("maps/city/city_kael.yaml", c.MapRef, "cityRefs[0].MapRef");
            Eq(24000, c.Population, "cityRefs[0].Population");
            Eq(0, cityMap.CityRefs.Count, "cityMap.CityRefs.Count");
        });

        // B3 —— terrain cells 实际从 cells_ref 加载
        Check("B3 world 地图 terrain cells 已从 cells_ref 加载（期望 21 条）", () =>
        {
            True(worldMap.TerrainData is not null, "worldMap.TerrainData 为 null");
            Eq(21, worldMap.TerrainData!.Cells.Count, "worldMap.TerrainData.Cells.Count");
        });

        Check("B3.1 world 地图 terrain 9 mountain / 8 forest / 4 river", () =>
        {
            var cells = worldMap.TerrainData!.Cells;
            Eq(9, cells.Count(c => c.Type == TerrainType.Mountain), "mountain 条数");
            Eq(8, cells.Count(c => c.Type == TerrainType.Forest), "forest 条数");
            Eq(4, cells.Count(c => c.Type == TerrainType.River), "river 条数");
        });

        // B4 —— 校验器
        Console.WriteLine();
        Console.WriteLine("  [校验明细]");
        Console.WriteLine($"  IsValid  = {loaded.Validation.IsValid}");
        Console.WriteLine($"  Errors   = {loaded.Validation.Errors.Count}");
        foreach (var e in loaded.Validation.Errors)
            Console.WriteLine($"    ERROR  {e.Code}  @ {e.Path}  : {e.Message}");
        Console.WriteLine($"  Warnings = {loaded.Validation.Warnings.Count}");
        foreach (var w in loaded.Validation.Warnings)
            Console.WriteLine($"    WARN   {w.Code}  @ {w.Path}  : {w.Message}");
        Console.WriteLine();

        Check("B4 校验 IsValid == true 且 Errors 为 0", () =>
        {
            Eq(0, loaded.Validation.Errors.Count, "Validation.Errors.Count");
            True(loaded.Validation.IsValid, "Validation.IsValid");
        });

        // B5 —— 独立的 TerrainFileLoader 直读样例地形文件
        var worldTerrainPath = Path.Combine(SampleRoot, "maps", "world", "world_kael.terrain.yaml");
        var cityTerrainPath = Path.Combine(SampleRoot, "maps", "city", "city_kael.terrain.yaml");

        Check("B5 TerrainFileLoader.Load(world_kael.terrain.yaml) 可读样例地形文件（期望 21 条）", () =>
        {
            var cells = TerrainFileLoader.Load(worldTerrainPath);
            Eq(21, cells.Count, "cells.Count");
        });

        Check("B5.1 TerrainFileLoader.Load(city_kael.terrain.yaml) 可读样例地形文件（期望 0 条）", () =>
        {
            var cells = TerrainFileLoader.Load(cityTerrainPath);
            Eq(0, cells.Count, "cells.Count");
        });

        // B6 —— LayerFileLoader 直读样例图层文件
        Check("B6 LayerFileLoader.Load(layers/terrain.yaml) 读出 layer_terrain_global", () =>
        {
            var layer = LayerFileLoader.Load(Path.Combine(SampleRoot, "layers", "terrain.yaml"));
            Eq("layer_terrain_global", layer.Id, "layer.Id");
            Eq("基础地形", layer.Name, "layer.Name");
            Eq(LayerKind.Terrain, layer.Type, "layer.Type");
            Eq(0, layer.ZOrder, "layer.ZOrder");
            Eq("flat", layer.DefaultStyle, "layer.DefaultStyle");
        });

        // B7 —— 保存 + 往返重载
        var tmpRoot = Path.Combine(Directory.GetCurrentDirectory(), ".worldview-smoke-roundtrip");

        Check("B7 ProjectSaver 保存到临时目录 + 重新加载成功（round-trip）", () =>
        {
            if (Directory.Exists(tmpRoot))
            {
                try { Directory.Delete(tmpRoot, recursive: true); }
                catch (UnauthorizedAccessException) { /* 上次运行残留，ProjectSaver.Save 会覆盖文件 */ }
            }
            Directory.CreateDirectory(tmpRoot);

            // 必传 layers：Saver 才会写出 layers/* 文件，roundtrip 后 reload 才找得到图层
            ProjectSaver.Save(tmpRoot, world, loaded.Maps, loaded.Layers);

            True(File.Exists(Path.Combine(tmpRoot, "project.yaml")), "临时目录未生成 project.yaml");
            True(File.Exists(Path.Combine(tmpRoot, "maps", "world", "world_kael.yaml")), "未生成 maps/world/world_kael.yaml");
            True(File.Exists(Path.Combine(tmpRoot, "maps", "city", "city_kael.yaml")), "未生成 maps/city/city_kael.yaml");

            var reloaded = ProjectLoader.Load(tmpRoot);
            Eq(2, reloaded.Maps.Count, "reloaded.Maps.Count");
            Eq("map_world_kael", reloaded.Maps[0].Id, "reloaded.Maps[0].Id");
            Eq(64, reloaded.Maps[0].Grid.Width, "reloaded.Maps[0].Grid.Width");
            Eq(48, reloaded.Maps[0].Grid.Height, "reloaded.Maps[0].Grid.Height");
            Eq("map_city_kael", reloaded.Maps[1].Id, "reloaded.Maps[1].Id");
            Eq(64, reloaded.Maps[1].Grid.Width, "reloaded.Maps[1].Grid.Width");
            Eq(2, reloaded.Maps[1].Buildings.Count, "reloaded.Maps[1].Buildings.Count");
            Eq(new GridCoord(20, 30), reloaded.Maps[1].Buildings[0].At, "reloaded house At");
            Eq(new GridCoord(32, 32), reloaded.Maps[1].Buildings[1].At, "reloaded tower At");
            Eq("示例世界", reloaded.World.Name, "reloaded.World.Name");
            // D7 死角修复：layers 与 cells 都得 roundtrip 找回
            Eq(loaded.Layers.Count, reloaded.Layers.Count, "reloaded.Layers.Count（Saver 必须写出 layers/*.yaml）");
            // PROBE: 调试 roundtrip cells 为何 0
            Console.WriteLine($"  PROBE world TerrainData null? {reloaded.Maps[0].TerrainData is null}");
            Console.WriteLine($"  PROBE world cells={reloaded.Maps[0].TerrainData?.Cells?.Count} ref='{reloaded.Maps[0].TerrainData?.CellsRef}'");
            Eq(21, reloaded.Maps[0].TerrainData!.Cells.Count, "reloaded world terrain cells（Saver 必须写出 *.terrain.yaml）");
        });

        // ---- 观测项：不影响通过/失败，只输出客观事实 ----

        Console.WriteLine();
        Console.WriteLine("  [观测项 OBS —— 不影响 pass/fail]");
        Observe("B-obs.1 已加载 LayerDoc 总数（2 张地图各引用同一组 4 个文件）", () => loaded.Layers.Count.ToString());
        Observe("B-obs.2 world 地图 terrain cells 实际条数", () => worldMap.TerrainData?.Cells?.Count.ToString() ?? "TerrainData 为 null");
        Observe("B-obs.3 city 地图 terrain cells 实际条数", () => cityMap.TerrainData?.Cells?.Count.ToString() ?? "TerrainData 为 null");
        Observe("B-obs.4 world 地图 grid.origin（YAML 中写作 { x: 0, y: 0 } 映射）", () => worldMap.Grid.Origin?.ToString() ?? "null");
        Observe("B-obs.5 world 地图 terrain default cell", () => worldMap.TerrainData?.Default?.ToString() ?? "null");
        Observe("B-obs.6 校验错误按 code 分组", () =>
            loaded.Validation.Errors.Count == 0
                ? "(无错误)"
                : string.Join(", ", loaded.Validation.Errors.GroupBy(e => e.Code).Select(g => $"{g.Key}×{g.Count()}")));
        Observe("B-obs.7 roundtrip 后 LayerDoc 数（Saver 写 layers/，Loader 能 reload 找回）", () =>
        {
            if (!Directory.Exists(tmpRoot)) return "临时目录不存在";
            var r = ProjectLoader.Load(tmpRoot);
            var refs = r.Maps.Sum(m => m.Layers?.Count ?? 0);
            return $"map 的 layer refs 共 {refs} 个，实际加载到 {r.Layers.Count} 个 LayerDoc";
        });
        Observe("B-obs.7.1 roundtrip 临时目录 layers/ 文件清单", () =>
        {
            if (!Directory.Exists(tmpRoot)) return "临时目录不存在";
            var layersDir = Path.Combine(tmpRoot, "layers");
            if (!Directory.Exists(layersDir)) return "layers/ 目录未生成";
            return string.Join(", ", Directory.GetFiles(layersDir, "*.yaml").Select(Path.GetFileName)!);
        });
        Observe("B-obs.8 样例根下 ProjectLoader 解析出的 layer ref 绝对路径首项", () =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.Combine(SampleRoot, "maps", "world", "world_kael.yaml"))!, "../layers/terrain.yaml")));
        Observe("B-obs.9 cells_ref 按 map 相对解析得到的路径（ProjectLoader.cs:82-84 的算法）", () =>
            Path.Combine(Path.GetDirectoryName(Path.Combine(SampleRoot, "maps", "world", "world_kael.yaml"))!, "maps/world/world_kael.terrain.yaml"));
    }

    // ------------------------------------------------------------------
    // 断言脚手架
    // ------------------------------------------------------------------

    private sealed class CheckFailed : Exception
    {
        public CheckFailed(string message) : base(message) { }
    }

    private static string ResolveScratchDir()
    {
        // 上游 MapValidatorTest.cs:12 用 Path.GetTempPath()；本环境下该路径可能不可写，
        // 故按顺序探测，第一个可写的胜出（不改变被测生产代码的语义：校验器只做 File.Exists 探测）。
        var candidates = new[]
        {
            Path.Combine(Path.GetTempPath(), "worldview-validator-tests"),
            Path.Combine(Directory.GetCurrentDirectory(), "tests", "WorldView.Smoke", ".scratch"),
        };
        foreach (var c in candidates)
        {
            try
            {
                Directory.CreateDirectory(c);
                return c;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [scratch 候选不可用] {c} — {ex.GetType().Name}: {ex.Message}");
            }
        }
        throw new CheckFailed("找不到可写的 scratch 目录");
    }

    private static void Check(string name, Action body)
    {
        try
        {
            body();
            _passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            var msg = Describe(ex);
            Failures.Add($"{name} — {msg}");
            Console.WriteLine($"FAIL {name}");
            Console.WriteLine($"      {msg.Replace("\n", "\n      ")}");
        }
    }

    private static void Observe(string name, Func<string> probe)
    {
        try
        {
            Console.WriteLine($"OBS  {name} — {probe()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OBS  {name} — 抛异常 {Describe(ex)}");
        }
    }

    /// <summary>展开异常链（YamlDotNet 会把真实原因包在 inner exception 里）。</summary>
    private static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        Exception? cur = ex;
        for (var depth = 0; cur is not null && depth < 8; depth++)
        {
            if (depth > 0) sb.Append("\n  └ ");
            sb.Append(cur is CheckFailed ? "" : $"{cur.GetType().Name}: ");
            sb.Append(cur.Message);
            if (cur is YamlDotNet.Core.YamlException ye && ye.Start.Line > 0)
                sb.Append($"  (line {ye.Start.Line}, col {ye.Start.Column})");
            cur = cur.InnerException;
        }
        return sb.ToString();
    }

    private static void True(bool condition, string what)
    {
        if (!condition) throw new CheckFailed(what);
    }

    private static void False(bool condition, string what) => True(!condition, what);

    private static void Eq<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new CheckFailed($"{what}: 期望 <{Show(expected)}>，实际 <{Show(actual)}>");
    }

    private static void Ne<T>(T notExpected, T actual, string what)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
            throw new CheckFailed($"{what}: 不应等于 <{Show(notExpected)}>");
    }

    private static string Codes(ValidationResult r, bool warnings = false)
    {
        var list = warnings ? r.Warnings : r.Errors;
        return list.Count == 0 ? "(空)" : string.Join(", ", list.Select(e => e.Code));
    }

    private static string Show(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        IEnumerable e => "[" + string.Join(", ", e.Cast<object?>().Select(Show)) + "]",
        _ => value.ToString() ?? "null",
    };
}
