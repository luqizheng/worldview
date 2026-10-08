# 地图尺寸、笔刷物理半径与世界生成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让用户按「公里 / 米」而不是格数来设定地图尺寸与笔刷半径，并让世界地图由程序化海岸线生成器一次生成（自然的海湾 / 半岛 / 峡湾），生成与笔刷共用同一套海岸线细化 pass。

**Architecture:** 纯逻辑（单位换算、确定性随机与噪声、细化、生成、批处理、裁剪）全部放在不引用 `Godot.*` 的可单测类型里；Godot 相关的一层只做「读 UI 状态 → 调纯逻辑 → 走 `EditorContext.ReplaceMap` 写回」。命令模式不变：所有文档修改仍经由 `IEditorCommand`。

**Tech Stack:** Godot 4.7.2 mono + C#（.NET 8 目标，本机 SDK 10.0.401）、YamlDotNet 16.3.0、xunit 2.9.2、自实现 splitmix64 + 值噪声（禁 `System.Random` / Godot `FastNoiseLite`）。

**Spec:**
- spec 1 = `docs/superpowers/specs/2026-10-08-map-size-and-terrain-brush-design.md`（地图尺寸与笔刷物理半径，v0.2）
- spec 2 = `docs/superpowers/specs/2026-10-08-world-generation-and-coastline-design.md`（世界生成与海岸线自然化，v0.1）

本计划一份而不是两份：两份 spec 共用 `MapSettingsDialog` / `ToolStrip` / `EditorContext` / `UndoStack` 四个文件，且 spec 2 §11.1 明示 spec 1 的 a–e 是它的前置；拆成两份计划会让同一文件在两个计划里各改一次（违反「同一文件单一职责、单一 owner」）。任务顺序已按依赖排好：纯逻辑在前，Godot 集成在后。

---

## Global Constraints

- **语言**：文档与 UI 文案用中文；代码标识符、YAML 键用英文（AGENTS.md）。
- **分层**：`scripts/Domain/**` 与 `scripts/Storage/**` **不得出现 `using Godot;`**（冒烟工程直接编译这两棵树作为守门）。`scripts/Editor/**`、`scripts/Editing/**`、`scripts/Ui/**`、`scripts/Render/**` 可以引用 Godot。
- **命名空间**：沿用既有集合 `WorldView.Domain` / `WorldView.Domain.Validation` / `WorldView.Storage.Yaml` / `WorldView.Editing` / `WorldView.Editor` / `WorldView.Render` / `WorldView.Ui.Palette`；本计划新增 `WorldView.Domain.Generation`。
- **写盘唯一出口**：`EditorContext.ReplaceMap(MapDoc)`；`MapSizing` 在 `scripts/Editor/`（编辑层单位换算），生成器在 `scripts/Domain/Generation/`（领域数据）。**禁止**用 `EditorContext.SetTerrainCells` 做批量写入（它每次调用重建整表并重排序，且越界格被静默跳过，无法用来裁剪）。
- **`patterns` 兼容性**：所有 `Domain` 记录必须带无参构造（YamlDotNet 约定），新字段一律**加法**（老工程缺键仍能加载）。
- **固定值与口径**（不得自行改动）：
  - `UndoStack.Capacity = 200`（保留，批处理单位同样受此上限与 FIFO 淘汰约束）。
  - `MapSizing.MaxRadiusCells = 64`（暂定，兼性能上限）；`BrushRadius` 语义仍是**格**（`EditorContext.BrushRadius` 为 `int`，`TerrainDragData.Radius` 也是格）。
  - 生成参数范围：`land_ratio` ∈ [0.05, 0.80]；`ruggedness` ∈ [0, 1]；`erosion` ∈ [0, 3]；默认 `land_ratio 0.35` / `ruggedness 0.5` / `min_island_cells 8` / `min_lake_cells 6` / `erosion 1`；算法版本串 `coastline_v1`。
  - 世界默认档：`512 × 512 @ 25 km/格` = 12,800 km 见方。
  - 地形默认：世界图 `terrain_data.default.type = water`，海洋格**不写入** `cells`；陆地 `plain`，临水陆地 `coast`。
- **测试运行方式（本机特有，务必照抄）**：`dotnet test` 在本机不可用（VSTest 在 `Process.set_EnableRaisingEvents` 抛 `Win32Exception(5): 拒绝访问`）。改用 xunit 控制台运行器：

```powershell
# 1) 构建（仓库根 D:\kudi-project\world-view）
dotnet build D:\kudi-project\world-view\tests\WorldView.Tests\WorldView.Tests.csproj
# 2) 一次性把控制台运行器拷到测试输出目录
$out = "D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0"
Copy-Item "$env:USERPROFILE\.nuget\packages\xunit.runner.console\2.9.2\tools\net6.0\*" $out -Force
# 3) 跑单个测试（工作目录必须是 examples\sample_world，见下方基线）
$runner = "$out\xunit.console.dll"; $dll = "$out\WorldView.Tests.dll"
dotnet exec --runtimeconfig "$out\WorldView.Tests.runtimeconfig.json" --depsfile "$out\WorldView.Tests.deps.json" `
  $runner $dll -nologo -method "WorldView.Tests.<命名空间>.<类>.<方法>"
# 4) 跑全量（回归用）
dotnet exec --runtimeconfig "$out\WorldView.Tests.runtimeconfig.json" --depsfile "$out\WorldView.Tests.deps.json" $runner $dll -nologo
```

- **测试基线（2026-10-08 实测，工作目录 `D:\kudi-project\world-view\examples\sample_world`）**：全量 **98 个，7 个既有失败**，总耗时 < 1 s。既有失败就是下面这 7 个，**不属于本计划范围，不要去修**：

  1. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`
  2. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`
  3. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`
  4. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`
  5. `WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array`
  6. `WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`
  7. `WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`

  **判定口径**：每个任务只要求「自己新增的测试通过」+「全量失败数仍为 7」。**禁止**把失败数改成 0 当作验收标准。特别是第 1、2 条：它们期望 `Push` 之后 `RedoCount` 累加到 100，而现有 `UndoStack.Push` 会 `_redo.Clear()`（标准撤销语义，**正确**）；这两条测试是**过时断言**，绝不可为了让它变绿去改 `Push` 的清除行为。
- **冒烟运行器**：`dotnet run --project tests\WorldView.Smoke`（仓库根运行）。它的 csproj 只编译 `scripts/Domain/**` 与 `scripts/Storage/**`（因此生成器 / 细化器 / 序列化改动都在它的覆盖内）。`scripts/Render/RoadLayer.cs:37`、`scripts/Render/TerrainLayer.cs:55` 的编译错误注释**已过时**——实测 `dotnet build WorldView.csproj` 0 错 0 警告，`tests/WorldView.Tests` 也能正常构建并运行 xunit 测试；不要照抄那份注释去回避 `scripts/Editing/**` 的测试。
- **spec 勘误（以实测代码为准，不是 spec 文本）**：spec 1 §5.5 / §7 有一句「落盘文件是内联 `cells`（不是独立 `<map>.terrain.yaml`）」与实现不符——`ProjectSaver` 在 `TerrainData.Cells` 非空时**会**写出 `<map 文件名>.terrain.yaml`，并把 map yaml 里的 `cells` 清空、`cells_ref` 置 `null`；`ProjectLoader` 已有同名约定 fallback（先试 `<base>.yaml.terrain.yaml`，再试 `<base>.terrain.yaml`）。所以本计划**不改** `Storage/`（spec 2 §11 第 9 项经实测确认 `YamlSchemas` 无需改动；第 10 项改为新增 `Storage/Yaml/WorldIndex.cs`），Task 8 的落盘重载闭环测试就是这条真实路径的验收。
- **`Render/` 例外**：spec 2 §11.2 第 12 项批准修改 `scripts/Render/WorldCanvas.cs`。这**刻意覆盖**了 `.trae/specs/ui-redesign-simgame/spec.md` §9 N7（那次只开 `TerrainPalette.cs` 一个只读例外）——spec 2 是更晚且经用户批准的 spec，以它为准；除此之外不要多动 `Render/`。
- **UI 行高**：`tool_row` 只有 **20px**（`BottomPalette` 行序：row0 `timeline_row` 28px 在上、row1 `build_row` 152px、row2 `tool_row` 20px 在下）。新增控件必须继续走 `ToolStrip.ApplyCompactStyle` 的紧凑样式，否则会被 `ClipContents` 裁掉底部。

## Review Focus

以下 5 类是被两份 spec 隐含、但任何单个任务的测试都不会自然覆盖、且一旦出错最容易被用户直接感知的失效模式；每一条都已在下方「归属任务」里各加了一个专门测试。

1. **同种子产不出同一世界**（跨进程/跨实例漂移）：只要有人顺手用 `System.Random` 或 Godot 的 `FastNoiseLite`，同一 `seed` 在不同运行时就会给出不同海岸线，「保存种子」这个功能就失去意义。→ 归属 Task 2（同种子跨实例同序列 + 禁用符号的源码 grep 步骤）与 Task 5（同种子同 cells）。
2. **大陆被「去碎岛」切掉一个角**：细化只看到笔触/生成范围的局部，一块横跨范围边界的陆地看起来就像小岛；一旦按 `min_island_cells` 删掉，用户看到的是地图边缘凭空多出直角缺口。→ 归属 Task 4（边界连通块必须保留且不被裁剪）。
3. **拖动中途就细化，或一次拖动变成 N 个撤销步**：中途细化的瞬间，尚未画完的半岛会被当成碎岛删掉；而 N 个撤销步会让用户按一次 Ctrl+Z 只退掉一戳，与《地图编辑器规格》§1.3「操作粒度 = 一次落笔」不符。→ 归属 Task 7（批处理单位）与 Task 11（仅释放时细化）。
4. **缩小地图后撤销，数据回不来**：越界地形格 / 建筑是校验 **Error**，所以必须裁剪；但如果 `Revert()` 没把裁掉的格与建筑还回去，用户按 Ctrl+Z 只会看到尺寸变化、内容永久丢失且无声无息。→ 归属 Task 10（裁剪 + 撤销全量恢复）。
5. **新建的世界其实没进 `map_index`，重开就没了**：`ProjectSaver.Save` 是**按下标对位**写盘的（`maps[i]` ↔ `world.MapIndex[i]`），比 `map_index` 多的图会被静默丢弃，而 `EditorContext.World` 是 `private set`，外部无法追加索引。→ 归属 Task 8（`WorldIndex.AppendMap` + 落盘重载闭环）。

## 文件结构

新增：

- `scripts/Editor/MapSizing.cs` — 纯单位换算（长度 ↔ 格、物理范围 → 网格、半径上限）。职责单一：只做算术，不认识 Godot，也不认识 UI。
- `scripts/Domain/Generation/DeterministicRandom.cs` — splitmix64 随机源，跨平台/跨版本稳定。
- `scripts/Domain/Generation/ValueNoise.cs` — 哈希格点值噪声 + fBm（含域扭曲所需的偏移采样）。
- `scripts/Domain/Generation/GenerationParams.cs` — 生成请求（网格 + 参数）与参数合法范围判定。
- `scripts/Domain/Generation/CoastlineRefiner.cs` — 共用的海岸线细化 pass（去碎岛 / 填碎海 / 形态学 / 重标 `coast`）。
- `scripts/Domain/Generation/WorldGenerator.cs` — 五步生成流水线，输出完整地形 cells。
- `scripts/Storage/Yaml/WorldIndex.cs` — 往 `World.MapIndex` 追加索引项的纯函数（补上缺失的写入 API）。
- `scripts/Editing/CompositeCommand.cs` — 批处理命令载体（逆序 `Revert`）。
- `scripts/Editing/RefineCoastlineCommand.cs` — 笔刷释放时的一次性细化命令。
- `scripts/Editing/ResizeMapCommand.cs` — 地图尺寸调整命令（含纯裁剪 / 丢失量描述静态方法）。
- `scripts/Ui/MapSettingsDialog.cs` — 两态对话框（新建世界 / 已存在地图改尺寸），按 `EditorController.CreateFileDialog()` 的方式动态创建挂载。
- `scripts/Storage/Yaml/TileSetStyleRegistry.cs` — 纯 YAML 解析器，读 `assets/textures/tiles/styles.yaml` → 风格 id 列表 + 默认 id + 每个风格的 `res://` 路径模板（不引用 `Godot.*`，可单测）。
- `scripts/Render/TileSetRegistry.cs` — Godot 感知层（**不新增 `Render/` 例外**的写法：与既有 `TerrainTileSetFactory` 共处 `scripts/Render/`，按 ui-redesign spec §9 N7 / spec 2 §11 例外列表的解读，把 `TileSetRegistry` 视作 `TerrainPalette.cs` 之后第二个只读例外 —— 它**只持有 `TileSet` 缓存与查找逻辑，不写场景、不持有节点**；如果 reviewer 反对这个解读，就把它挪到 `scripts/Storage/Yaml/` 的纯层 + 一个 `scripts/Editor/TileSetLoader.cs` 的薄包装，详见 Task 14 内的「Render 例外折中」备选）。暴露 `AvailableIds`、`DefaultId`、`Get(string name)` 三个静态成员。
- `assets/textures/tiles/styles.yaml` — 风格清单（首版内容：`- id: jrpg` + `- id: _fallback_solid_color`，前者引用既有 `terrain_atlas.png` 等，后者兜底退回 `TerrainTileSetFactory.Build()` 的纯色块）。
- 测试：`tests/WorldView.Tests/Editor/MapSizingTests.cs`、`tests/WorldView.Tests/Generation/{DeterministicRandomTests,ValueNoiseTests,GenerationSpecYamlTests,CoastlineRefinerTests,WorldGeneratorTests}.cs`、`tests/WorldView.Tests/Validation/MapValidatorGenerationTests.cs`、`tests/WorldView.Tests/Editing/{UndoStackBatchTests,RefineCoastlineCommandTests,ResizeMapCommandTests}.cs`、`tests/WorldView.Tests/Storage/{GeneratedWorldPersistenceTests,TileSetStyleRegistryTests}.cs`。

修改：

- `scripts/Domain/MapDoc.cs` — `MapDoc` 加 `GenerationSpec? Generation`；新增 `GenerationSpec` 记录。
- `scripts/Domain/Terrain.cs` — 补 `TerrainDefaults.Water`。
- `scripts/Domain/Validation/ValidationError.cs` — 加 `GenerationParamOutOfRange` 工厂。
- `scripts/Domain/Validation/MapValidator.cs` — 加生成参数范围告警规则。
- `scripts/Storage/…` — **无需改动**：`YamlSchemas` 不需要为 `GenerationSpec` 注册类型（下划线命名约定 + `IgnoreUnmatchedProperties` 已覆盖，spec 2 §11 第 9 项实测不必要）；`ProjectSaver` 的落盘约定（`<map>.yaml.terrain.yaml`）已存在且可用，本计划不动它。
- `scripts/Editing/UndoStack.cs` — 加 `BeginBatch` / `EndBatch` / `IsBatching`。
- `scripts/Editor/EditorContext.cs` — 加 `AddMap(MapDoc, string)` 与 `RefineOnStroke`（默认 true）。
- `scripts/Render/WorldCanvas.cs` — 按下时开批、释放时细化并关批。
- `scripts/Render/TerrainTileSetFactory.cs` — 改为**委托给 `TileSetRegistry`**：保留同名 `Build()`（返回默认 id 的 `TileSet`，避免 T11/T13 回归），新加的 `Build(string name)` 走注册器。
- `scripts/Ui/MapSettingsDialog.cs` — 加 `OptionButton` 「Tile Style」，选项 = `TileSetRegistry.AvailableIds`，默认 = `TileSetRegistry.DefaultId`（仅 UI 状态，不落盘 — 见 Task 14 内的「持久化延后」说明）。
- `scripts/Ui/Palette/ToolStrip.cs` — 笔刷半径改物理单位输入 + 细化开关。
- `scripts/Editor/EditorController.cs` — 创建 / 弹出 `MapSettingsDialog`，提供触发入口。
- `docs/数据模型规格_v0.1.md` — 追加 `generation` 块字段定义（纯加法）。

---

### Task 1: MapSizing 纯单位换算

**Files:**
- Create: `scripts/Editor/MapSizing.cs`
- Test: `tests/WorldView.Tests/Editor/MapSizingTests.cs`

**Interfaces:**
- Consumes: `WorldView.Domain.GridSpec`（`scripts/Domain/MapDoc.cs`）。
- Produces:
  - `public static class WorldView.Editor.MapSizing`
  - `public const int MaxRadiusCells = 64;`
  - `public static int CellsForLength(double length, double cellDistance)` → `Math.Max(1, (int)Math.Ceiling(length / cellDistance))`；`cellDistance <= 0` 时抛 `ArgumentOutOfRangeException`。
  - `public static double LengthForCells(int cells, double cellDistance)` → `cells * cellDistance`。
  - `public static GridSpec GridFromExtent(double widthPhysical, double heightPhysical, GridSpec template)` → `template with { Width = CellsForLength(widthPhysical, template.CellDistance), Height = CellsForLength(heightPhysical, template.CellDistance) }`。
  - 约定：`length <= 0` 不由本类处理（UI 拦截并置灰「应用」）。

- [ ] 写失败测试 `tests/WorldView.Tests/Editor/MapSizingTests.cs`（命名空间 `WorldView.Tests.Editor`），断言逐条为：

```csharp
Assert.Equal(100, MapSizing.CellsForLength(5000, 50));      // 5000 km ÷ 50 km
Assert.Equal(152, MapSizing.CellsForLength(5000, 33));      // ceil(151.5)，且 152*33=5016 ≥ 5000
Assert.Equal(1,   MapSizing.CellsForLength(5, 50));         // 不能算成 0
Assert.Equal(4,   MapSizing.CellsForLength(200, 50));
Assert.Throws<ArgumentOutOfRangeException>(() => MapSizing.CellsForLength(100, 0));
var g = MapSizing.GridFromExtent(12800, 12800, new GridSpec(0, 0, 25.0, "km", "m", "m"));
Assert.Equal(512, g.Width); Assert.Equal(512, g.Height);
Assert.Equal("km", g.DistanceUnit); Assert.Equal("m", g.BaseUnit);   // 其余字段不动
Assert.Equal(64, MapSizing.MaxRadiusCells);
Assert.True(MapSizing.LengthForCells(152, 33) >= 5000);
```

- [ ] 跑它，确认失败：`-method "WorldView.Tests.Editor.MapSizingTests.CellsForLength_5000km_at_50km_Is_100"` → 编译不过是预期的第一步失败（类型还不存在）。
- [ ] 实现 `scripts/Editor/MapSizing.cs`（`namespace WorldView.Editor;`，`using System;` + `using WorldView.Domain;`，**不得** `using Godot;`）。
- [ ] 再跑整个测试类，确认全绿：`-class "WorldView.Tests.Editor.MapSizingTests"`。
- [ ] 全量回归：失败数仍为 7。

### Task 2: 确定性随机与值噪声

**Files:**
- Create: `scripts/Domain/Generation/DeterministicRandom.cs`、`scripts/Domain/Generation/ValueNoise.cs`
- Test: `tests/WorldView.Tests/Generation/DeterministicRandomTests.cs`、`tests/WorldView.Tests/Generation/ValueNoiseTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class WorldView.Domain.Generation.DeterministicRandom`：`DeterministicRandom(ulong seed)`、`ulong NextUInt64()`（splitmix64）、`double NextDouble()` ∈ [0,1)、`int NextInt(int minInclusive, int maxExclusive)`。
  - `public static class WorldView.Domain.Generation.ValueNoise`：`double Sample(double x, double y, ulong seed)` ∈ [0,1]（哈希格点 + 双线性平滑插值）、`double Fbm(double x, double y, ulong seed, int octaves, double lacunarity = 2.0, double gain = 0.5)`。
  - 两者都**不得**用 `System.Random` 或 Godot `FastNoiseLite`（spec 2 §4.2：.NET 各种子算法不保证跨版本稳定）。

- [ ] 写失败测试。`DeterministicRandomTests`：`NextUInt64_SameSeed_SameSequence`（两个独立实例各取 8 个值，逐位相等）；`NextUInt64_DifferentSeed_Differs`；`NextDouble_InUnitInterval`（取 1000 个，全部 ∈ [0,1)，且 `distinct > 100`，均值 ∈ [0.3, 0.7]）；`NextInt_HonoursBounds`（1000 次都 ∈ [min,max)）。`ValueNoiseTests`：`Sample_IsDeterministic`（同参两次相等）；`Sample_InUnitRange`；`LatticePoints_DifferBetweenCells`（`Sample(0,0,s) != Sample(1,0,s)` 至少对 `s=1` 成立）；`Interpolation_IsStrictlyBetweenLatticeValues`（`Sample(0.5,0,s)` 严格位于 `Sample(0,0,s)` 与 `Sample(1,0,s)` 之间）；`Fbm_IsDeterministic`；`Fbm_MoreOctaves_AddsDetail`（octaves 6 与 2 在同一批采样点上至少有 20% 不同）。
- [ ] 跑它，确认失败（类型不存在）。
- [ ] 实现两个类（`namespace WorldView.Domain.Generation;`，**不得** `using Godot;`；splitmix64 常量 `0x9E3779B97F4A7C15` / `0xBF58476D1CE4E5B9` / `0x94D049BB133111EB`）。
- [ ] 跑两个测试类，确认全绿。
- [ ] **禁用符号守卫**（源码级，防止后人换实现）：

```powershell
Select-String -Path D:\kudi-project\world-view\scripts\Domain\Generation\*.cs -Pattern "System\.Random|Godot\.|FastNoiseLite" 
# 期望：无匹配输出
```

### Task 3: `generation` 数据契约（含文档）

**Files:**
- Modify: `scripts/Domain/MapDoc.cs`
- Create: `scripts/Domain/Generation/GenerationParams.cs`
- Modify: `docs/数据模型规格_v0.1.md`（只在 §2 地图定义处**追加** `generation` 块，不改既有字段）
- Test: `tests/WorldView.Tests/Generation/GenerationSpecYamlTests.cs`

**Interfaces:**
- Consumes: `WorldView.Storage.Yaml.YamlSchemas.Serialize/Deserialize`。
- Produces:
  - 在 `scripts/Domain/MapDoc.cs` 内新增（与 `GridSpec` 同处，属持久化 spec 的惯例位置）：

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
    public GenerationSpec() : this("coastline_v1", 0, 0.35, 0.5, 8, 6, 1) { }   // YamlDotNet 反序列化所需
}
```

  - `MapDoc` 记录**末尾**追加一个可选参数：`GenerationSpec? Generation = null`（加法；老 `main.yaml` 无此键 → `null`）。
  - `public sealed record WorldView.Domain.Generation.GenerationParams(GenerationSpec Spec, int Width, int Height, double CellDistance);`
  - `public static class GenerationLimits`：`MinLandRatio = 0.05`、`MaxLandRatio = 0.80`、`MinRuggedness = 0`、`MaxRuggedness = 1`、`MaxErosion = 3`、`bool IsWithinRanges(GenerationSpec spec)`、`string? DescribeViolation(GenerationSpec spec)`（中文描述，供 Task 6 的告警复用）。
  - 注意 `[property: …]` 前缀：位置记录参数上的特性只有加 `property:` 才落到属性上（`TerrainCell.Height` 用的是这个写法）。

- [ ] 写失败测试 `GenerationSpecYamlTests`：`Roundtrip_PreservesAllFields`（构造非默认值 → `Serialize` → `Deserialize` → 逐字段相等）；`AbsentGeneration_Deserializes_To_Null`（读 `examples/sample_world/maps/world/main.yaml` 的文本 → `Generation` 为 `null`）；`Serialized_Keys_UseUnderscores`（序列化结果包含 `land_ratio:`、`min_island_cells:`、`min_lake_cells:`、`seed:`、`algorithm: coastline_v1`）；`IsWithinRanges_RejectsOutOfRange`（`land_ratio 0.9` false、`ruggedness 1.5` false、`erosion 5` false、默认值 true）。
- [ ] 跑它，确认失败。
- [ ] 改 `MapDoc.cs`、新建 `GenerationParams.cs`。
- [ ] 更新 `docs/数据模型规格_v0.1.md`（追加 `generation` 块示例 + 字段表：`algorithm`/`seed`/`land_ratio`/`ruggedness`/`min_island_cells`/`min_lake_cells`/`erosion`，并注明「缺省即不存在；版本串用于保证同种子可复现」）。
- [ ] 跑测试类确认全绿；跑 `dotnet run --project tests\WorldView.Smoke` 确认示例工程仍能加载（应为既有通过数不变）。

### Task 4: 共用海岸线细化 pass

**Files:**
- Create: `scripts/Domain/Generation/CoastlineRefiner.cs`
- Test: `tests/WorldView.Tests/Generation/CoastlineRefinerTests.cs`

**Interfaces:**
- Consumes: `WorldView.Domain.{GridCoord, TerrainType}`。
- Produces:
  - `public sealed record RefineOptions(int MinIslandCells = 8, int MinLakeCells = 6, int Erosion = 0, bool Morphology = false);`
  - `public sealed record RefineStats(int RemovedCells, int FilledCells, int RelabeledCells);`
  - `public sealed record RefineOutcome(IReadOnlyDictionary<GridCoord, TerrainType> Changed, RefineStats Stats);`
  - `public static RefineOutcome CoastlineRefiner.Refine(Func<int,int,TerrainType> read, int width, int height, GridCoord rangeMin, GridCoord rangeMax, TerrainType defaultType, RefineOptions options)`
- 语义（spec 2 §5.1 / §5.2，逐条都要在实现里成立）：
  1. 扫描范围 = `[rangeMin - 1, rangeMax + 1]` 与网格求交（重标 `coast` 需要比改动区多扩 1 格）。
  2. 步骤顺序：去碎岛 → 填碎海 → 形态学（仅 `options.Morphology && options.Erosion > 0`）→ 重标 `coast`。
  3. 连通性：岛屿 / 湖泊的**连通块用 4 邻接**；`coast` 判定用 **8 邻接**（陆地格只要有 1 个水邻格就是 `coast`，否则 `plain`）。
  4. 陆地 = `read(...) != defaultType`；水 = 等于 `defaultType`。
  5. **硬约束 A**：任何触及扫描盒边界或网格边界的连通块**永不被删除 / 填充**（否则横跨范围的大陆会被切角）。
  6. **硬约束 B**：`Morphology == false` 时整个 pass **幂等**（再跑一次 `Changed` 为空）。
  7. **硬约束 C**：绝不重算扫描范围之外的内容。
  8. `Changed` 同时表达「写」与「删」：值为 `defaultType` 表示调用方应删除该 sparse 条目。

- [ ] 写失败测试（`read` 用 `Dictionary<GridCoord,TerrainType>` 闭包或二维数组；辅助 `Cells(...)` / `TerrainTypes(...)` 构造局部地形）：
  `ThreeCellIsland_IsRemoved_AtThreshold8`；`BoundaryTouching_Component_Is_Preserved`（3 格陆地贴着扫描盒边界 → `Changed` 中不含它）；`FourCellLake_IsFilled_AtThreshold6`；`FirstCellWideCanal_IsNotFilled`（贯穿扫描盒的水道必须保留，因为它触边界）；`CoastRing_IsRelabeled_IncludingExpandedRing`（改动区外 1 格内的新陆地也要 `coast`）；`SecondPass_WithMorphologyOff_ChangesNothing`；`NothingOutsideRange_IsChanged`；`ErosionZero_DoesNotRunMorphology`；`ReturnedDeletion_IsReportedAsDefaultType`；`StatsCounts_MatchChangedSize`。
- [ ] 跑它，确认失败。
- [ ] 实现 `CoastlineRefiner`（纯 C#，无 Godot）。
- [ ] 跑测试类确认全绿；全量回归失败数仍为 7。

### Task 5: WorldGenerator 五步流水线

**Files:**
- Create: `scripts/Domain/Generation/WorldGenerator.cs`
- Test: `tests/WorldView.Tests/Generation/WorldGeneratorTests.cs`

**Interfaces:**
- Consumes: `GenerationParams`、`GenerationSpec`、`DeterministicRandom`、`ValueNoise`、`CoastlineRefiner`。
- Produces:
  - `public sealed record GeneratedWorld(IReadOnlyList<TerrainCellEntry> Cells, TerrainType DefaultType, RefineStats Stats);`
  - `public static class WorldGenerator`：`public const string Algorithm = "coastline_v1";`、`public static GeneratedWorld Generate(GenerationParams p)`。
- 流水线（spec 2 §4.1）：① 大陆遮罩（低频 fBm，2–4 个周期）→ ② 域扭曲（中频 fBm 偏移采样坐标；产生海湾 / 半岛 / 峡湾的唯一来源）→ ③ 多倍频细节（5–7 octaves）→ ④ **分位数阈值**（按 `LandRatio` 取分位，保证旋钮命中准确）→ ⑤ `CoastlineRefiner.Refine(..., Morphology: true, Erosion: Spec.Erosion)` 全图范围。
- 输出口径：陆地一律 `plain`，细化后才把临水陆地标 `coast`；水**不写入** `Cells`；`DefaultType = TerrainType.Water`；`Cells` 按 `(y, x)` 升序。
- 本任务只依赖 `TerrainType.Water` 这个枚举值（已存在），**不要**依赖 `TerrainDefaults.Water` 常量——那个常量由 Task 6 补上，本任务排在它前面。

- [ ] 写失败测试：`SameSeed_ProducesIdenticalCells`（两个独立调用逐元素相等）；`DifferentSeed_ProducesDifferentCells`；`LandRatio_Within1_5Percent_OfRequested`（`land_ratio 0.35` → 实际陆地占比 ∈ [0.335, 0.365]）；`LandRatio_Extremes_005_And_080_AreValid`（都不抛、都有陆地、0.80 时仍有水）；`AllCoordinates_InBounds`；`NoDuplicateCoordinates`；`Cells_SortedByYThenX`；`DefaultType_Is_Water`；`Ocean_IsNotWritten`（返回的 cells 中不含 `Water` 类型）；`QuantileThreshold_HitsLandCountExactly_WhenRefineDisabled`（用 `MinIslandCells=0, MinLakeCells=0, Erosion=0` 的参数跑，`Cells.Count == (int)Math.Round(ratio * width * height)`）；`Golden_LandCount_For_PinnedSeed`（固定 `seed=12345, 128×128, land_ratio 0.35, ruggedness 0.5, erosion 0`，把首次实测到的 `Cells.Count` 写成常量并加注释「冻结值：用于捕捉未来误用 System.Random 导致的世界漂移」）；`HigherRuggedness_ProducesMoreCoastCells`。
- [ ] 跑它，确认失败。
- [ ] 实现 `WorldGenerator`（纯 C#；尺寸 > 0 与参数范围在此做防御性检查，越界抛 `ArgumentOutOfRangeException`）。
- [ ] 跑测试类确认全绿。
- [ ] 量一下耗时（spec 2 §4.4 目标 < 200 ms，仅记录、不做断言以免抖动）：在冒烟运行器或临时 `dotnet run` 中跑一次 `seed=12345` 的 `512×512` 生成，把实测毫秒数记进任务留言；若 > 200 ms，在此任务内加进度反馈的**待办条目**并上报，不在此处改 UI。

### Task 6: `TerrainDefaults.Water` 与生成参数校验

**Files:**
- Modify: `scripts/Domain/Terrain.cs`、`scripts/Domain/Validation/ValidationError.cs`、`scripts/Domain/Validation/MapValidator.cs`
- Test: `tests/WorldView.Tests/Validation/MapValidatorGenerationTests.cs`

**Interfaces:**
- Consumes: `GenerationLimits.DescribeViolation`（Task 3）、`GenerationSpec`。
- Produces:
  - `TerrainDefaults.Water` → `new(TerrainType.Water, 999, false)`（`move_cost` / `buildable` 为 MVP 占位值，注释标 `[待标定]`；**不可建造**是硬要求）。
  - `ValidationError.GenerationParamOutOfRange(string path, string field, string value, string range)` → `Severity.Warning`，`Code = "W_GENERATION_PARAM"`，消息形如 `生成参数 {field}={value} 超出范围 {range}`。
  - `MapValidator` 新增规则：`map.Generation is not null` 且 `GenerationLimits.DescribeViolation(...) is not null` → 追加该告警（**只告警，不报 Error**，不阻塞保存）。

- [ ] 写失败测试：`Water_Constant_Is_NotBuildable`（`Type==Water`、`Buildable==false`、`MoveCost==999`）；`GenerationParam_OutOfRange_Produces_Warning`（`land_ratio 0.9` → `Warnings` 含 `W_GENERATION_PARAM`，且 `Errors` 为空）；`GenerationParam_InRange_Produces_No_Warning`；`MapWithout_Generation_Produces_No_Warning`（用 `examples/sample_world` 的 world map 文本反序列化后校验）。
- [ ] 跑它，确认失败。
- [ ] 实现三处改动。
- [ ] 跑测试类确认全绿；全量回归失败数仍为 7（`MapValidatorTest` / `ProjectValidatorTests` 不得因新规则变红——新规则只在 `Generation is not null` 时触发）。

### Task 7: `UndoStack` 批处理 + `CompositeCommand`

**Files:**
- Create: `scripts/Editing/CompositeCommand.cs`
- Modify: `scripts/Editing/UndoStack.cs`
- Test: `tests/WorldView.Tests/Editing/UndoStackBatchTests.cs`

**Interfaces:**
- Consumes: `IEditorCommand`（`Description` / `Apply()` / `Revert()`）。
- Produces:
  - `public sealed class WorldView.Editing.CompositeCommand : IEditorCommand`：`CompositeCommand(string description)`、`string Description { get; }`、`int Count { get; }`、`void Add(IEditorCommand command)`、`Apply()`（**正序**）、`Revert()`（**逆序**）。
  - `UndoStack` 新增：`void BeginBatch(string description)`、`void EndBatch()`、`bool IsBatching { get; }`、`string? BatchDescription { get; }`。
- 语义（spec 2 §6.2）：
  - 批处理开启期间，`Push(command)` **仍然立即 `command.Apply()`**，并把它追加进当前 `CompositeCommand`，而不是直接进撤销表。
  - `EndBatch()`：批内有 ≥1 条 → 把 `CompositeCommand` 作为**一个单位**登记（此时**不再重放** `Apply`）；批内 0 条 → **什么都不压**（否则 Ctrl+Z 会出现「按了没反应」的空步）。
  - 无 `BeginBatch` 的 `EndBatch()` = 空操作；批处理中再次 `BeginBatch` = 忽略（沿用外层描述）。
  - 批处理单位与单命令共享 `Capacity = 200` 与 FIFO 淘汰；`UndoDescription` 在批处理中返回批描述。

- [ ] 写失败测试（用既有 `FakeCommand` / `CounterCommand` 式的本地桩，**不得**依赖 `EditorContext`——它继承 `Godot.Node`，无法裸测）：`Batch_Of_Three_Pushes_Is_OneUndoUnit`（`UndoCount == 1`，三条都已 `Apply`）；`Undo_RevertsBatch_InReverseOrder`（日志顺序为 c3,c2,c1）；`Redo_AppliesBatch_InForwardOrder`；`EmptyBatch_PushesNothing_AndUndoReturnsFalse`；`EndBatch_WithoutBegin_IsNoOp`；`NestedBegin_KeepsOuterDescription`；`Batch_CountsAsOneAgainstCapacity`（压 250 条单命令 → `UndoCount == UndoStack.Capacity`，既有淘汰行为不变）；`Push_OutsideBatch_StillWorks`（回归）。
- [ ] 跑它，确认失败。
- [ ] 实现 `CompositeCommand` + `UndoStack` 改动。
- [ ] 跑 `-class "WorldView.Tests.Editing.UndoStackBatchTests"` 确认全绿。
- [ ] 全量回归：**失败数仍为 7，且失败名单与基线完全一致**——尤其不得为了 Task 5 的 `Apply_Undo_Loop_100_Times_Has_No_Residue` 变绿去改 `Push` 的 `_redo.Clear()` 行为。

### Task 8: `map_index` 追加与生成世界落盘 / 重载闭环

**Files:**
- Create: `scripts/Storage/Yaml/WorldIndex.cs`
- Modify: `scripts/Editor/EditorContext.cs`（只加方法）
- Test: `tests/WorldView.Tests/Storage/GeneratedWorldPersistenceTests.cs`

**Interfaces:**
- Consumes: `World` / `MapIndexEntry`（`scripts/Domain/World.cs`）、`ProjectSaver.Save`、`ProjectLoader.Load`、`WorldGenerator.Generate`。
- Produces:
  - `public static class WorldView.Storage.Yaml.WorldIndex`：`public static World AppendMap(World world, string refPath)` → 返回 `world with { MapIndex = (world.MapIndex ?? Array.Empty<MapIndexEntry>()).Append(new MapIndexEntry(refPath)).ToList() }`。
  - `EditorContext.AddMap(MapDoc map, string refPath)`：`_maps.Add(map)`；`World = WorldIndex.AppendMap(World, refPath)`；`EmitSignal(SignalName.MapChanged)`。**这是 `World` 为 `private set` 时唯一能让新图落盘的通道**。
  - 落盘 ref 约定：`maps/world/<id>/main.yaml`（与既有 `examples/sample_world` 一致；地形文件由 `ProjectSaver` 按 `<map 文件名>.terrain.yaml` 约定写出，`ProjectLoader` 已有同名 fallback，不需要改）。

- [ ] 写失败测试 `GeneratedWorldPersistenceTests`：`AppendMap_AddsEntry_AtEnd`；`AppendMap_OnNullIndex_CreatesIndex`；`SaveGeneratedWorld_RoundTrips`（临时目录：`World` 追加索引 → `MapDoc { Type = World, Generation = spec, TerrainData { Default = TerrainDefaults.Water, Encoding = "sparse", Cells = 生成结果 } }` → `ProjectSaver.Save` → `ProjectLoader.Load` → cells 数量与内容一致、`Generation` 逐字段一致、`Default.Type == Water`、`Encoding == "sparse"`）；`MapsBeyondMapIndex_AreNotWritten`（`maps.Count > MapIndex.Count` 时多出来的图不会落盘——把这个**陷阱**固化成测试，防止后人以为「加进 `_maps` 就等于存了」）。
- [ ] 跑它，确认失败。
- [ ] 实现 `WorldIndex` 与 `EditorContext.AddMap`。
- [ ] 跑测试类确认全绿；`dotnet run --project tests\WorldView.Smoke` 仍全绿。
- [ ] **冒烟验证 `AddMap` 本尊**（它依赖 Godot，单测覆盖不到）：在 Godot 中打开工程、加载示例、调用一次 `AddMap`（可用 `godot_command` 的 `eval` 或临时按钮），确认 `project.yaml` 的 `map_index` 多出一项且重开后能加载。

### Task 9: `RefineCoastlineCommand`

**Files:**
- Create: `scripts/Editing/RefineCoastlineCommand.cs`
- Test: `tests/WorldView.Tests/Editing/RefineCoastlineCommandTests.cs`

**Interfaces:**
- Consumes: `CoastlineRefiner.Refine`、`RefineOptions`、`MapDoc`、`EditorContext`。
- Produces:
  - `public sealed class WorldView.Editing.RefineCoastlineCommand : IEditorCommand`：
    - `RefineCoastlineCommand(EditorContext context, string mapId, GridCoord rangeMin, GridCoord rangeMax, int radiusCells, GenerationSpec? spec = null)`（**勘误（R21）**：原文漏了可选的 `spec`；`OptionsFor` 的 >8 档需要 `spec.Erosion`）
    - `public static RefineOptions OptionsFor(int radiusCells, GenerationSpec? spec)` —— 分档（spec 2 §6.4）：半径 1–2 → `new(0, 0, 0, Morphology: false)`（只重标 `coast`，用户在做精细活）；3–8 → `new(8, 6, 0, false)`（温和清理小碎块）；>8 → `new(8, 6, spec?.Erosion ?? 1, true)`（完整）。
    - `public static RefineOutcome Compute(MapDoc map, GridCoord rangeMin, GridCoord rangeMax, RefineOptions options)`（纯）
    - `public static MapDoc ApplyTo(MapDoc map, RefineOutcome outcome)`（纯）
    - `public static MapDoc RevertTo(MapDoc map, RefineOutcome outcome, IReadOnlyList<TerrainCellEntry> beforeCells)`（纯；**`beforeCells` 必填**）。**勘误（R20）**：原文写 2 参 `RevertTo(MapDoc, RefineOutcome)`，但 `RefineOutcome.Changed` 是**单向**的——被删除的格不记录原类型，无法从 outcome 反推前态，2 参版本只能是静默 no-op。故改为 3 参必填，`outcome` 仅为与 `ApplyTo` 对称保留、不参与计算。
    - `public static string Describe(RefineOutcome outcome)`（纯；`Description` 属性委托它。**勘误（R21）**：`Description` 需要 `EditorContext`（Godot `Node`）才能构造实例，纯 .NET 测试无法覆盖属性本身，故把格式化逻辑抽成纯静态以便裸测）
    - `Description` = `$"细化海岸线（去碎岛 {RemovedCells} · 填碎海 {FilledCells}）"`。
  - 写回必须走 `EditorContext.ReplaceMap`，`Cells` 保持 `(y, x)` 升序；值为 `Default.Type` 的格**从 `cells` 中删除**（沿用 `SetTerrainCells` 的既有口径）。

- [ ] 写失败测试：`OptionsFor_Radius1_And_2_RelabelOnly`；`OptionsFor_Radius8_Gentle`；`OptionsFor_Radius32_Full_UsesSpecErosion`；`Compute_RemovesThreeCellIsland_AtDefaultThreshold`；`ApplyTo_WritesCoastRing`；`ApplyTo_DeletesEntriesEqualToDefault`；`RevertTo_RestoresExactBeforeState`（混合地形：含需删回水面的格与需写回的陆地格）；`ApplyTo_KeepsCellsSortedByYThenX`；`Description_CountsMatchStats`。测试只走静态方法（`EditorContext` 不参与）。
- [ ] 跑它，确认失败。
- [ ] 实现命令（实例路径用首次 `Apply()` 快照 / `Revert()` 还原，与 `ApplyBrushCommand` 的 `_before` 口径一致）。
- [ ] 跑测试类确认全绿；全量回归失败数仍为 7。

### Task 10: `ResizeMapCommand`（含纯裁剪逻辑）

**Files:**
- Create: `scripts/Editing/ResizeMapCommand.cs`
- Test: `tests/WorldView.Tests/Editing/ResizeMapCommandTests.cs`

**Interfaces:**
- Consumes: `MapDoc`、`GridSpec`、`Building`、`EditorContext.ReplaceMap`。
- Produces:
  - `public sealed class WorldView.Editing.ResizeMapCommand : IEditorCommand`：
    - `ResizeMapCommand(EditorContext context, string mapId, GridSpec newGrid)`（**勘误（R21）**：构造函数**不得**调用 `context.FindMap(...)`；`Description` 懒求值。测试不能构造 `EditorContext`）
    - `public static MapDoc Resize(MapDoc map, GridSpec newGrid)`（纯：换 `Grid` + 裁剪越界地形格与建筑，其余字段原样保留，含 `Encoding` / `Default` / `Generation`）
    - `public static IReadOnlyList<TerrainCellEntry> CropCells(IReadOnlyList<TerrainCellEntry> cells, int width, int height)`
    - `public static IReadOnlyList<Building> CropBuildings(IReadOnlyList<Building> buildings, int width, int height)`
    - `public static string DescribeLoss(MapDoc map, GridSpec newGrid)`（纯：`"缩小到 80 × 60 格将丢弃 1,240 个地形格与 3 个建筑。"`；放大时 `"扩大到 96 × 96 格不会丢弃任何数据。"`）
    - `public static string Describe(GridSpec oldGrid, GridSpec newGrid)`（纯；`Description` 属性委托它。**勘误（R21）**：同 Task 9——`Description` 需要 Godot `Node` 才能构造实例，纯 .NET 测试无法覆盖属性本身）
    - `Description` = `$"调整地图尺寸：{oldW}×{oldH} → {newW}×{newH} 格"`。
  - 写回走 `ReplaceMap`；**不得**用 `SetTerrainCells`（越界格被静默跳过，裁不掉）。命令自身不弹确认框——预检确认由调用方（Task 13）在 `Push` 之前完成。

- [ ] 写失败测试：`CropCells_DropsOutOfBounds_KeepsOrder`；`CropBuildings_DropsOutOfBounds`；`Resize_GrowsGrid_KeepsAllCells`；`Resize_ShrinksGrid_CropsCellsAndBuildings`；`Resize_PreservesEncodingDefaultAndGeneration`；`DescribeLoss_MatchesCounts`；`DescribeLoss_Growing_ReportsNoLoss`；`Description_FormatsOldToNew`。
- [ ] 跑它，确认失败。
- [ ] 实现命令（实例路径：首次 `Apply()` 快照原 `MapDoc`，`Revert()` 用 `ReplaceMap` 写回原文档——**必须把裁掉的格与建筑一并还原**）。
- [ ] 跑测试类确认全绿；全量回归失败数仍为 7。

### Task 11: `WorldCanvas` 笔触批处理与释放时细化

**Files:**
- Modify: `scripts/Render/WorldCanvas.cs`（按下 `:322-341`、释放 `:342-345`、`TryPaint` `:405-437`）
- Modify: `scripts/Editor/EditorContext.cs`（加 `RefineOnStroke` 属性。**勘误（R22）**：原 Files 清单漏了本文件，但下面 Produces 明确要求本任务产出 `EditorContext.RefineOnStroke`）

**Interfaces:**
- Consumes: `UndoStack.BeginBatch/EndBatch`（Task 7）、`RefineCoastlineCommand`（Task 9）。
- Produces: `EditorContext.RefineOnStroke { get; set; } = true;`（本任务加，Task 12 只给它接 UI 开关）+ 拖动一次的撤销语义 = **1 步**，细化只发生在释放时。

- [ ] 改动按下分支（`WorldCanvas.cs:322-325`）：`Tool == ToolState.Brush` 时先 `context.UndoStack.BeginBatch("笔刷绘制")`，并把 `_strokeMin` / `_strokeMax` 置为本次落点。
- [ ] 改动 `TryPaint`（`:405-437`）：每次落笔后用笔刷覆盖范围更新 `_strokeMin` / `_strokeMax`（`cell ± context.BrushRadius`）；仍照旧 `Push(new ApplyBrushCommand(...))`。
- [ ] 改动释放分支（`:342-345`）：若此前是笔刷且 `context.RefineOnStroke`，构造 `rangeMin = _strokeMin - 1`、`rangeMax = _strokeMax + 1`（与网格求交），`context.UndoStack.Push(new RefineCoastlineCommand(context, map.Id, rangeMin, rangeMax, context.BrushRadius))`，随后**无论是否有细化命令**都 `EndBatch()`；清 `_painting`。细化**绝不**在 `HandleMouseMotion` 中触发。
- [ ] `dotnet build D:\kudi-project\world-view\WorldView.csproj` → 0 错。
- [ ] 无头冒烟：用 `godot_run_headless`（项目路径 `D:\kudi-project\world-view`，`--quit-after` 若干帧）确认启动无异常输出（若工具报未配置引擎路径，先向用户询问 Godot 可执行文件位置并用 `godot_set_engine_path` 保存，**不要**自行搜索文件系统）。
- [ ] 交互验证：`godot_run_project` 打开示例工程，选笔刷沿一条边拖一笔 → `godot_command` 取 `screenshot`；再 `eval` 读 `EditorContext.UndoStack.UndoCount`，确认**一次拖动只 +1**；按一次 Ctrl+Z 内容整体还原。

### Task 12: `ToolStrip` 物理单位半径输入 + 细化开关

**Files:**
- Modify: `scripts/Ui/Palette/ToolStrip.cs`（半径滑块 `:77-88`、`OnRadiusChanged` `:390-402`）——`EditorContext.RefineOnStroke` 由 Task 11 添加，本任务只**读取**它接 UI 开关。

**Interfaces:**
- Consumes: `MapSizing.CellsForLength` / `LengthForCells` / `MaxRadiusCells`、`EditorContext.ActiveMap`、`EditorContext.RefineOnStroke`（Task 11 已加）。
- Produces:
  - `ToolStrip` 的新半径控件：`SpinBox`（`MinValue = 0.1`、`MaxValue = 5000`、`Step = 0.1`、`CustomMinimumSize = new Vector2(72f, 16f)`）+ 单位 `Label`（World → `km`，City → `m`，无地图 → 退回纯格数显示）+ 派生 `Label` 形如 `= 100 格`。
  - 换算与显示口径（spec 1 §5.2）：`cells = Mathf.Clamp(MapSizing.CellsForLength(physical, map.Grid.CellDistance), 1, MapSizing.MaxRadiusCells)`，写入 `EditorContext.BrushRadius`；派生标签显示**实际生效**的格数；当 `cells > 32` 时标签后缀 `（较大，落笔较慢）`。无活动地图时控件退回 `1–64` 的格数直选。
  - 细化开关：紧凑 `CheckButton`（沿用 `ApplyCompactStyle`），绑定 `RefineOnStroke`。

- [ ] **删掉**既有的 1–8 格 `HSlider`（`ToolStrip.cs:77-88`）与 `OnRadiusChanged` 的 `Mathf.Clamp((int)value, 1, 8)`，换成 `MapSizing` 换算驱动的物理单位 `SpinBox` 与两个标签（保留 `CustomMinimumSize = new Vector2(0f, 20f)` 与 `ApplyCompactStyle`，`tool_row` 只有 20px）。
- [ ] 加 `EditorContext.RefineOnStroke` 与 `ToolStrip` 的 `CheckButton`（绑定它，默认开）。
- [ ] `dotnet build D:\kudi-project\world-view\WorldView.csproj` → 0 错。
- [ ] 交互验证：`godot_run_project` + `screenshot`：世界图（25 km/格 或示例值）下输入 `100 km` → 派生标签显示对应格数，`eval get_property` 读 `EditorContext.BrushRadius` 与标签一致；切到城市图 → 单位变 `m`；输入 `100000` → 被钳到 `64` 且标签显示实际值。

### Task 13: `MapSettingsDialog` 两态对话框

**Files:**
- Create: `scripts/Ui/MapSettingsDialog.cs`
- Modify: `scripts/Editor/EditorController.cs`（沿用 `CreateFileDialog()` 的动态创建 / 挂载方式）

**Interfaces:**
- Consumes: `WorldGenerator.Generate`、`MapSizing.GridFromExtent`、`GenerationLimits`、`WorldIndex.AppendMap`（经 `EditorContext.AddMap`）、`ResizeMapCommand`、`Render.TerrainPalette.ColorOf`、`Ui.Palette.MinimapSection` 的缩略图手法。
- Produces: `public partial class WorldView.Ui.MapSettingsDialog : AcceptDialog`，两个状态：
  - **新建世界**：尺寸（物理长 × 宽 + 单位）、分辨率（单格距离，`cell_distance` 预设 + 自定义）、生成参数（陆地比例 / 曲折度 / 种子 + 🎲 重新掷骰）、缩略图预览、细化统计行、`[创建] [取消]`。
  - **已存在地图**：只可改尺寸 / 分辨率，生成参数**只读**、重新掷骰禁用，`[应用] [取消]`；应用前用 `ResizeMapCommand.DescribeLoss` 弹确认，确认后 `Push(new ResizeMapCommand(...))`（可获得撤销）。
- 关键口径：
  - **分辨率预设档**（spec 1 §3.4，每档末项固定为「自定义…」，选它才允许手输任意小数；MVP 只有 `World` / `City` 可选，其余档位只作为预设数据存在，因为 `MapType` 目前只有这两个值）：`world` km 10 / 25 / 50；`city` m 1 / 2 / 5 / 10；`region` km 1；`street` m 0.5 / 1；`building` m 0.5 / 1；`underground` m 1 / 5。
  - **实际覆盖必须显示**（spec 1 §3.5）：输入的是物理边长，实际覆盖 = `Width × CellDistance`，可能略大于输入，界面要写出来（如 `12800 km 见方（512 格 × 25 km）`）。
  - **地形名与颜色不另立**：地形显示名一律取 `Ui.Palette.TerrainSwatch.NameOf(type)`（山地 / 苔原等既有译名），颜色一律取 `Render.TerrainPalette.ColorOf(type)`（底层仍是 `TerrainDefaults.ColorHex`）。
  - **不自动生成 `scale_label`**（spec 1 §8 第 5 项已定）。
  - **不做「新地图向导」**（spec 1 §4.4 范围外）：本对话框只负责「新建**世界**地图」与「改已存在地图尺寸」两件事，不新增 `MapType` 取值、不写 `map_index` 之外的工程结构。
  - 生成走 `WorldGenerator.Generate` → `MapDoc { Id = ..., Type = MapType.World, Grid = GridFromExtent(...), Generation = spec, TerrainData = new TerrainData { Encoding = "sparse", Default = TerrainDefaults.Water, Cells = 排序后的生成结果 } }` → `context.AddMap(map, $"maps/world/{id}/main.yaml")` → `context.DrillTo(id)`；**逐格 `SetTerrainCells` 禁止使用**。
  - 超大网格**只警告不阻止**：`width*height > 1024*1024` 时显示 `⚠ 预计写盘约 {cells/1000*1000} 条记录（约 {MB} MB）… 建议把分辨率调粗到 25 km/格`。
  - 缩略图沿用 `MinimapSection.RebuildTexture()` 的手法：`Image.CreateEmpty(w, h, false, Image.Format.Rgba8)` + `Fill(TerrainPalette.ColorOf(default))` + 逐格 `SetPixel` + `ImageTexture.CreateFromImage`，旧贴图 `Dispose()`；取色用 `Render/TerrainPalette.ColorOf`，**不新增** `Render/` 文件。
  - 生成不进撤销栈（它发生在地图存在之前）；已存在地图**不提供**「重新生成」。
  - 触发入口：顶栏 `TopStrip` 加一个按钮（与 `OpenBtn`/`SaveBtn` 同排），`EditorController` 里 `CreateMapSettingsDialog()` + `_dialog.Popup(...)`，与 `OnOpenPressed` 同风格。

- [ ] 实现对话框两个状态与预览（Godot 部分；纯逻辑已由 Task 1/5/10 覆盖）。
- [ ] 在 `EditorController` 里创建并挂载对话框、接上触发按钮（`_Ready` 中 `CreateFileDialog()` 之后调用）。
- [ ] `dotnet build D:\kudi-project\world-view\WorldView.csproj` → 0 错。
- [ ] 交互验证（`godot_run_project` + `godot_command screenshot`）：新建世界用默认档 → 预览出现大陆轮廓 → 创建 → 画布显示新图且顶栏面包屑为它；`eval` 读 `project.yaml` 的 `map_index` 计数 +1；关掉工程重开（`LoadProject` 重载）仍能看到该图。
- [ ] 交互验证缩小路径：对已存在图把尺寸改小 → 确认框数字与 `DescribeLoss` 一致 → 应用后越界内容消失 → 一次 Ctrl+Z **连同越界内容一起回来**；`eval` 跑一次校验确认无 `E_GRID_OOB` / `E_BUILDING_OOB`。

### Task 14: 多 TileSet 风格注册器（3 风格预留接口）

**目的：** 用户在 m00001 后段提出「要做多套瓦片图案（JRPG 风格为主）」。本轮**只做预留接口**，**不**实装 3 套；确保未来新增 3 套仅是「把 `terrain_atlas.png` 换成 `assets/textures/tiles/<style_id>/...` 的一组 PNG + 在 `styles.yaml` 加一行 + 在下拉里多一项」，**不需要**再回本轮改 `MapDoc`、改 storage、改 generation、改 dialog 骨架。**3 套实装推迟到独立的下轮 spec / plan**（本轮不立新 spec，因为 MapDoc 字段、存储、UI 都已确定不动，本任务就是「装钩子」）。

**Files:**
- Create: `scripts/Storage/Yaml/TileSetStyleRegistry.cs`、`assets/textures/tiles/styles.yaml`、`tests/WorldView.Tests/Storage/TileSetStyleRegistryTests.cs`
- Modify: `scripts/Render/TerrainTileSetFactory.cs`（让 `Build()` 走注册器）、`scripts/Ui/MapSettingsDialog.cs`（Task 13 的对话框上加一个 `OptionButton` 「Tile Style」）、`scripts/Editor/EditorContext.cs`（加一个**仅进程内**的 `CurrentTilesetId` 字段，默认 = `TileSetStyleRegistry.DefaultId`）。

**持久化延后：** 本轮**不**把 `tileset_id` 写进 `MapDoc` / 不落盘。理由：m00672 用户明确选择「最小代价、零重返制、MapDoc 不动」；新增可空字段虽然纯加法，但需要 (a) 修 YamlSchemas、(b) 修 ProjectValidator、(c) 写一个「未知 id 退 fallback」的错误处理 + 测一次、(d) `data-model` 规格 v0.2 推一版；用户没要这些。等到下一轮实装 3 套时再一起推 v0.2。本轮只保证：关掉工程再开，画布仍用**默认**风格显示（`DefaultId` 是 `jrpg`，等于现状）。

**`styles.yaml` 契约（首版示例）：**
```yaml
version: 1
default: jrpg
styles:
  - id: jrpg
    path: .                          # 指向 assets/textures/tiles/ 自身（既有资源）
    description: JRPG 16×16 风格
  - id: _fallback_solid_color
    path: solid                      # 若工程内不存在该目录，注册器降级到 TerrainTileSetFactory.Build() 的纯色块
    description: 纯色兜底（无资产时退化）
```

**Interfaces（产出的纯逻辑层，可单测）：**
- `public sealed record WorldView.Storage.Yaml.TileSetStyle(string Id, string Path, string Description);`
- `public sealed class WorldView.Storage.Yaml.TileSetStyleRegistry`：
  - `public IReadOnlyList<TileSetStyle> Styles { get; }`（按 YAML 顺序）
  - `public string DefaultId { get; }`（YAML `default:` 字段）
  - `public string AssetPathFor(string id) => $"res://assets/textures/tiles/{styles.First(s => s.Id == id).Path}";` —— **只生成字符串**，不触发任何 `ResourceLoader`，这样**单测不需要 Godot**。
  - 构造器签名：仅一个 `(Stream yaml)` 或 `(string yamlText)`，**不**读盘——测试可直接灌入字符串。
- `public static class WorldView.Render.TileSetRegistry`（**Godot 感知层，只读例外**：本类**只**持 `TileSet` 缓存 + 转发到 `TileSetStyleRegistry`，**不**写场景、不持有节点；与 `TerrainTileSetFactory` 同样是「纯查找 + 缓存」责任，故归入 `Render/` 例外；如果 reviewer 反对本例外，把这个静态类挪到 `scripts/Editor/TileSetLoader.cs` —— 备选方案详见 reviewer 反馈处理）：
  - `public static IReadOnlyList<string> AvailableIds { get; }` —— 从 `TileSetStyleRegistry.Styles` 投影。
  - `public static string DefaultId { get; }`
  - `public static TileSet Get(string id)` —— `_cache.GetOrAdd(id, _ => LoadFromAsset(TileSetStyleRegistry.AssetPathFor(id)))`；若 `AssetPathFor` 对应的 `res://` 路径解析失败或 `ResourceLoader.Load` 抛错，**静默退化到 `TerrainTileSetFactory.Build()`** 并在 stderr 打印 `WARN: tileset '{id}' 加载失败，退化到纯色块`（spec 1 §10 错误处理一脉相承：UI 不崩、用户看到颜色而不是黑屏）。
  - `static void WarmUp()` —— `ProjectSettings` 加载时由 `EditorContext._Ready` 调一次，触发默认项预热（避免首帧卡顿）。

**`Render` 例外折中备选：** 若 reviewer 认定本类违反 ui-redesign §9 N7 渲染层规则，则备选：把 `TileSetRegistry` 整体挪到 `scripts/Storage/Yaml/`（成为纯逻辑 + 一个 `string` 缓存 `Dictionary<string, TileSet>`，在 `Editor/_Ready` 里被预热；该文件**必须**用 `partial class` + `[Tool]` 才能访问 `ResourceLoader`——这是 Godot 4 唯一合法的 C# 文件出现 `using Godot;` 但仍在非 `Render/` 路径的方式）。本任务让 implementer 先按上面「Interfaces 产出的纯逻辑层」写，再决定 Godot 感知层的物理位置。

**Tests（`tests/WorldView.Tests/Storage/TileSetStyleRegistryTests.cs`，纯逻辑层，可全跑）：**
- `Parse_MinimalYaml_ReadsDefault` —— 灌入 `version: 1\ndefault: jrpg\nstyles:\n  - id: jrpg\n    path: .` → `DefaultId == "jrpg"`，`Styles.Count == 1`。
- `Parse_TwoStyles_ExposesBoth` —— 灌入两条 → 顺序保留，`AssetPathFor("jrpg")` 与 `AssetPathFor("_fallback_solid_color")` 不相等。
- `AssetPathFor_KnownId_ReturnsResPath` —— `AssetPathFor("jrpg") == "res://assets/textures/tiles/."`（注：末尾 `/` 经实现决定，本测试只验证前缀 + 含 id 的目录名）。
- `AssetPathFor_UnknownId_Throws` —— 抛 `KeyException`（或 `ArgumentException`，实现二选一，单测断言其一）。
- `DefaultId_NotInStyles_Throws` —— `default: ghost\nstyles:\n  - id: jrpg ...` → 构造时抛。
- `Parse_EmptyStyles_Throws` —— `styles: []` → 抛。
- **端到端契约（任务级冒烟，`dotnet exec` xunit 跑得动）：** 把 `assets/textures/tiles/styles.yaml` **真实文件**复制到测试临时目录、构造注册器、断言 `AvailableIds` 包含 `"jrpg"`、`DefaultId == "jrpg"`、`AssetPathFor("jrpg")` 以 `"res://assets/textures/tiles"` 开头。

**Tests（Godot 感知层 `Render/TileSetRegistry.cs`）：** Godot 部分按既有惯例**只**走 `godot_run_project` + `godot_command eval` 冒烟（spec 1 §11 风格一致）：`eval TileSetRegistry.Get("jrpg")` 返 `TileSet`；`eval TileSetRegistry.Get("__nonexistent__")` 不抛、返 `TerrainTileSetFactory.Build()` 的 fallback，并在 stderr 看见一行 `WARN`。**不**为这部分加 xunit —— 避免任务被 Godot 测试运行器卡住（已知 `dotnet test` 坏掉，Godot 集成测试也走 `godot_command`）。

- [ ] 写 `styles.yaml`（首版两条：`jrpg` + `_fallback_solid_color`）。
- [ ] 写失败测试 `TileSetStyleRegistryTests`（6+ 条 + 端到端契约）。
- [ ] 跑它，确认失败。
- [ ] 实现 `TileSetStyleRegistry`（纯逻辑层；**只**用 `YamlStream` / `YamlMapping` / 现有 `YamlSchemas` 的解析手段——`scripts/Storage/Yaml/YamlSchemas.cs` 已暴露工具，照 T2 写法）。
- [ ] 跑测试，确认全绿。
- [ ] 实现 `Render.TileSetRegistry`（Godot 感知层；`#if TOOLS` 或 `partial class` 以保持 C# 编译时不绑 Godot——查 `scripts/Editor/MapSizing.cs` 与 `scripts/Render/TerrainPalette.cs` 的 `using` 写法沿用），并改 `TerrainTileSetFactory.Build()` 内部改为 `return TileSetRegistry.Get(TileSetRegistry.DefaultId);`（**行为不变**：默认 id 是 `jrpg`，资源位置是既有位置，回归风险 ≈ 0）。
- [ ] 改 `MapSettingsDialog`（Task 13 已建）：在两个状态各加一行 `[HBox] Label("Tile Style") | OptionButton`；`OptionButton` 选项 = `TileSetRegistry.AvailableIds`，默认选中 `TileSetRegistry.DefaultId`；用户改了不落盘，只写 `EditorContext.CurrentTilesetId`（进程内）。
- [ ] `EditorContext` 加 `public string CurrentTilesetId { get; set; } = TileSetRegistry.DefaultId;`；`WorldCanvas` 在 `_Ready` 中读它、初始化 `TileSet` 时把当前 id 传进 `TileSetRegistry.Get(id)`。
- [ ] **禁用符号守卫**：
  ```powershell
  Select-String -Path D:\kudi-project\world-view\scripts\Storage\Yaml\TileSetStyleRegistry.cs -Pattern "Godot\.|ResourceLoader" 
  # 期望：无匹配输出（纯逻辑层不允许引 Godot）
  Select-String -Path D:\kudi-project\world-view\scripts\Render\TileSetRegistry.cs -Pattern "File\.|new StreamReader\(" 
  # 期望：无匹配输出（Godot 感知层不允许直接读盘；走 res://）
  ```
- [ ] `dotnet build D:\kudi-project\world-view\WorldView.csproj` → 0 错。
- [ ] xunit 跑 `WorldView.Tests.Storage.TileSetStyleRegistryTests` 全绿。
- [ ] 跑全量 xunit（cwd = `examples\sample_world`）→ 失败数 ≤ 7（与基线一致；不引入新失败）。
- [ ] 冒烟：`godot_run_project` + `godot_command eval TileSetRegistry.Get("jrpg")` → 返回 `TileSet` 实例；`eval TileSetRegistry.Get("__nonexistent__")` → 不抛、stderr 有 WARN；`eval TileSetRegistry.AvailableIds` → 数组含 `"jrpg"` 与 `"_fallback_solid_color"`；`eval EditorContext.CurrentTilesetId` → `"jrpg"`。

**任务评审重点（reviewer 看这些）：**
1. **纯逻辑层不引 Godot**（`Select-String` 守卫生效）。
2. **`Render` 例外是否合理**（本任务已写明备选；reviewer 不同意就触发备选迁移，本任务被退回修订）。
3. **fallback 路径真的不崩**（冒烟用例覆盖）。
4. **持久化确实没动**（`MapDoc.cs` 这次**不应该**出现在 git diff 里——除非 reviewer 改动，否则本任务不允许修改它）。
5. **T13 没被破坏**（MapSettingsDialog 的两个状态仍然按 Task 13 的口径工作；只是多了个 OptionButton）。

**前置：**
- Task 1（MapSizing）✅（提供 `ProjectSettings` 路径常量等次要工具）。
- Task 13（MapSettingsDialog）必须完成（依赖它的 `partial class` 文件存在）。

---

## 待 P0-4 / 待标定（本轮按下列占位值实现，P0-4 视觉规范或实测数据出来后回改）

| 项 | 本轮占位值 | 依据 |
| --- | --- | --- |
| 笔刷半径控件类型 | `SpinBox` + 单位标签 + 派生格数标签 | spec 1 §8 第 1 项 |
| `MapSizing.MaxRadiusCells` | `64` | spec 1 §3.3（暂定，同时是性能上限） |
| 对话框样式 | 复用 Godot 默认 `AcceptDialog` 主题 | spec 1 §8 第 3 项 |
| 地形色块形状 | 沿用既有 `TerrainSwatch` 方块 | spec 1 §8 第 4 项 |
| `scale_label` | 不自动生成 | spec 1 §8 第 5 项 |
| `TerrainDefaults.Water` 的 `move_cost` / `buildable` | `999` / `false` | spec 2 §3.2（`buildable: false` 是硬要求，`move_cost` 待标定） |
| 世界默认档 | `512 × 512 @ 25 km`、`land_ratio 0.35` | spec 2 §3.3 |
| `erosion` | 默认 `1`，上限 `3` | spec 2 §3.1 / §10 |
| 512² 生成耗时 | 实测后记录（目标 < 200 ms） | spec 2 §4.4 |

另有两项**刻意不在本轮做**，留作后续独立工作：`height_data`（河流 / 侵蚀的前置，`scripts/Domain/MapDoc.cs:132` 已标注 MVP 未实现）与 `chunked` 编码（超大网格目前只告警、不自动切换）。
