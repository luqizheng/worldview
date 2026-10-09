# Task 3 Report: `generation` 数据契约（含文档）

## Contract (5 lines)
- Created: `scripts/Domain/Generation/GenerationParams.cs`（runtime 包：Spec + Width + Height + CellDistance）, `scripts/Domain/Generation/GenerationLimits.cs`（MinLandRatio=0.05 / MaxLandRatio=0.80 / MinRuggedness=0 / MaxRuggedness=1 / MaxErosion=3 / IsWithinRanges / DescribeViolation 中文）
- Modified: `scripts/Domain/MapDoc.cs`（新增 `GenerationSpec` record + 在 `MapDoc` 记录末尾追加 10 参 `GenerationSpec? Generation = null`，并把 9→10 参的 no-arg ctor 同步到 `Generation: null`），`docs/数据模型规格_v0.1.md`（§2 字段表加 `generation` 一行 + 新增 §2.11「世界生成参数（`generation`，可选）」子节 + YAML 示例）
- Tests: `tests/WorldView.Tests/Generation/GenerationSpecYamlTests.cs`（4 测试）
- Commit: `ed17650e9908404557ea6d85db89fa2125450b9b` on top of `486f6b4`
- Class pass: `GenerationSpecYamlTests` 4/4；regression Total **121** / Failed **7**（名字未变）；smoke-runner **30 passed / 0 failed**（与基线一致）

## TDD evidence

### RED（首跑 — 无任何新文件时）

> 提交时 `GenerationSpec` / `GenerationLimits` / `MapDoc.Generation` 都还不存在，编译期就被 xunit 拒收。

```
GenerationSpecYamlTests.cs(43,24): error CS0246: 未能找到类型或命名空间名"GenerationSpec"
GenerationSpecYamlTests.cs(77,25): error CS1061: "MapDoc"不包含"Generation"的定义
GenerationSpecYamlTests.cs(98,21): error CS0103: 当前上下文中不存在名称"GenerationLimits"
...（每个测试方法 × 2–3 行错）
    3 个错误
```

### GREEN（实现完成 + CWD 修正后，class 跑通）

> 第一个 GREEN 跑仍然 FAIL 1/4，因为 CWD 不在 sample world 下导致 walk-up 找不到 `project.yaml`；把 CWD 切到 `D:\kudi-project\world-view\examples\sample_world` 后全绿。

```
  Discovering: WorldView.Tests
  Discovered:  WorldView.Tests
  Starting:    WorldView.Tests
  Finished:    WorldView.Tests
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 4, Errors: 0, Failed: 0, Skipped: 0, Time: 0.165s
```

### Regression（full xunit run）

```
   WorldView.Tests  Total: 121, Errors: 0, Failed: 7, Skipped: 0, Time: 0.385s
```

7 个 FAIL 名字与 Task 2 收尾时**逐字相同**（已 diff 校验）：

```
BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells
BrushShapesTests.Round_Radius_2_Covers_Expected_Cells
UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue
UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric
YamlSchemasTest.Deserialize_GridCoord_As_Array
ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute
ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only
```

数学：prior 117（Task 2 收尾） + 4 new = **121** ✓ / Failed **7** ✓。

### Smoke

```
=== 汇总 ===
passed 30 / failed 0
```

与 Task 2 收尾时的 smoke 数完全一致；smoke runner 的 round-trip 路径经过 `MapDoc` 序列化 → 临时目录 → 再加载，新 `Generation` 字段全程不破坏 round-trip。

## MapDoc append detail

`MapDoc` 现为 10 参 record（仅新增尾部第 10 参，**未改动前 9 参的别名/默认值/类型**）：

```csharp
public sealed record MapDoc(
    string Id,
    string Name,
    MapType Type,
    GridSpec Grid,
    TimeSpec Time,
    [property: YamlMember(Alias = "presentation")] PresentationSpec Presentation = null!,
    [property: YamlMember(Alias = "terrain_data")] TerrainData TerrainData = null!,
    [property: YamlMember(Alias = "layers")] IReadOnlyList<LayerRef> Layers = null!,
    [property: YamlMember(Alias = "city_refs")] IReadOnlyList<CityRef> CityRefs = null!,
    [property: YamlMember(Alias = "buildings")] IReadOnlyList<Building> Buildings = null!,
    [property: YamlMember(Alias = "generation")] GenerationSpec? Generation = null)
```

No-arg ctor 同步扩展到 10 参（防御性默认值不破坏反序列化路径）：

```csharp
public MapDoc() : this(
    Id: "",
    Name: "",
    Type: MapType.World,
    Grid: new GridSpec(64, 64, 1.0),
    Time: new TimeSpec(TimeGranularity.Month, TimeGranularity.Day),
    Presentation: new PresentationSpec(),
    TerrainData: new TerrainData(),
    Layers: Array.Empty<LayerRef>(),
    CityRefs: Array.Empty<CityRef>(),
    Buildings: Array.Empty<Building>(),
    Generation: null) { }
```

`GenerationSpec` 自身（同文件内，位于 `MapDoc` 之上，按 brief 要求「与 `GridSpec` 同处」）：

```csharp
public sealed record GenerationSpec(
    [property: YamlMember(Alias = "algorithm")] string Algorithm = "coastline_v1",
    [property: YamlMember(Alias = "seed")] long Seed = 0,
    [property: YamlMember(Alias = "land_ratio")] double LandRatio = 0.35,
    [property: YamlMember(Alias = "ruggedness")] double Ruggedness = 0.5,
    [property: YamlMember(Alias = "min_island_cells")] int MinIslandCells = 8,
    [property: YamlMember(Alias = "min_lake_cells")] int MinLakeCells = 6,
    [property: YamlMember(Alias = "erosion")] int Erosion = 1)
{
    /// <summary>YamlDotNet 反序列化所需的无参构造器。</summary>
    public GenerationSpec() : this("coastline_v1", 0, 0.35, 0.5, 8, 6, 1) { }
}
```

## Docs append detail

`docs/数据模型规格_v0.1.md` 共两处新增（皆**纯追加**，未修改任何已有字段/章节/行号）：

### §2 字段表（追加于 `terrain_data`/`height_data` 之后、`layers` 之前）

```
| `generation` | object | | 世界生成参数（算法版本 / 种子 / 陆地占比 / 崎岖度 / 最小岛屿 / 最小湖泊 / 侵蚀步数），见 §2.11；**缺省即不存在**：整张地图未指定生成参数时整块不写入 |
```

### §2.11（新增子节，插于 §2.10「坐标映射与锚点」之后、`--- ## 3. 地形 ---` 之前）

> §2.9 已被既有「YAML 示例」占用，故使用 §2.11 避免重号。

包含：

- 字段表（`algorithm` / `seed` / `land_ratio` / `ruggedness` / `min_island_cells` / `min_lake_cells` / `erosion`，含类型、必填、默认值、说明）
- 4 条纪律（缺省即不存在 / 算法版本与种子绑定 / 细化参数分离 / 范围校验由 `GenerationLimits` 集中维护）
- YAML 示例：

```yaml
# 出现在 maps/world/world_kael.yaml（或其他地图文件）的顶层，
# 与 terrain_data / layers 同级。
generation:
  algorithm: coastline_v1
  seed: 14631366567179368864        # 64 位种子（示例值）
  land_ratio: 0.42
  ruggedness: 0.6
  min_island_cells: 12
  min_lake_cells: 9
  erosion: 2
```

## Concerns (1-3 lines max per item, only if real)

- **测试运行 CWD 依赖**：`AbsentGeneration_Deserializes_To_Null` 通过 `Directory.GetCurrentDirectory()` 向上 walk-up 找 `project.yaml`；这要求 xunit.console 启动时 CWD 在 sample world 工程内（与既有的 `ProjectLoaderTests` 完全一致，沿用 R4 判定）。如果未来有人从任意位置跑这套测试，需要在 README 注明「`cd examples\sample_world` 后再跑 xunit.console」。**未引入新依赖**。
- **smoke runner round-trip 写出空 `generation:`**：`MapDoc.Generation` 在反序列化为 `null` 后，`YamlSchemas.Serialize` 会写出 `generation: null`（YAML 流里即 `generation:` 空值），smoke round-trip 临时目录里能看到这一行。这**不破坏**任何测试（`examples/sample_world/` 没被改），且「缺省即不存在」是**作者**的纪律，序列化时仍按 YamlDotNet 默认行为写 `null` 字段。**提议**留待 T8 round-trip 任务时统一加 `DefaultValuesHandling.OmitNull | Ignore` 之类的约定（影响面超出 Task 3 范围，故本任务不动）。

## Self-review (5 bullets)

- **范围纪律**：`git diff 486f6b4` 只增 2 文件 + 改 2 文件，全部落在 brief 的 Files 列表内（`MapDoc.cs` 修改 / `GenerationParams.cs` 新建 / `GenerationLimits.cs` 新建 / `数据模型规格_v0.1.md` 追加 / `GenerationSpecYamlTests.cs` 新建）。`docs/superpowers/**` 与所有 `.uid` 文件**未**入提交（untracked），符合 brief。
- **TDD 顺序**：先写测试拿到 RED（编译期 CS0246/CS0103/CS1061 三个错类）→ 再写 `GenerationSpec` + `GenerationLimits` + 改 `MapDoc` → 4/4 GREEN。中间一次 CWD 失误导致 GREEN 看似失败，已在 `D:\kudi-project\world-view\examples\sample_world` 跑确认。
- **基线零漂移**：Total 121 = 117 + 4 ✓；Failed 7 名字逐字对齐 plan L29-40 列表；smoke 30/0 与 T2 收尾一致。7 个 pre-existing 失败**未触碰、未尝试修复**。
- **接口契约 vs 实现**：`GenerationSpec`（持久化形态，挂在 `MapDoc` 上） ≠ `GenerationParams`（运行时形态，喂 `WorldGenerator`）。两者解耦明确：前者走 YamlDotNet，后者纯 C# 不可变 record 不挂 `YamlMember`，与 T2 的 `DeterministicRandom` 同层、不混 YamlDotNet 元数据。
- **范围校验集中**：`GenerationLimits.IsWithinRanges` / `DescribeViolation` 把 land_ratio/ruggedness/erosion 的闭区间集中在一处（plan L29 阈值），返回中文（spec §3 用户文案），T6 告警 UI 直接复用 `DescribeViolation` 即可，避免阈值散落各处。
