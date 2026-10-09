# Task 8 实施报告

## 任务
plan L301-319：在 `map_index` 追加机制上打通「世界生成 → 加入工程 → 落盘」链路，并修复 YamlDotNet null-skip（R12）。

## 范围

**Create**
- `scripts/Storage/Yaml/WorldIndex.cs` — 纯函数 helper（无 Godot 依赖）
- `tests/WorldView.Tests/Storage/GeneratedWorldPersistenceTests.cs` — 5 个 `[Fact]`

**Modify**
- `scripts/Editor/EditorContext.cs` — 新增 `AddMap(MapDoc, string)` 方法
- `scripts/Storage/Yaml/YamlSchemas.cs` — `BuildSerializer()` 加 R12 修复

**未触碰**
- `Domain/World.cs`, `Domain/MapDoc.cs`, `Domain/Generation/GenerationSpec.cs`（T1-T6 锁定的领域模型）
- `ProjectSaver.cs`, `ProjectLoader.cs`（已能正确往返）
- `GridCoordYamlConverter`, `ReadOnlyListYamlConverter`（在 `YamlSchemas.cs`，明确出范围）
- 7 个已知失败测试

## 实施步骤

### Step 1 — RED
写 5 个测试 + 2 个辅助方法（`NewWorldWithMapIndex`, `MakeTinyMap`, `CreateScratchDir`, `DeleteDirectory`）。预期 build 报 `WorldIndex` / `AddMap` 缺失。
- 实际：`CS0103` 两个 `WorldIndex` 引用未解析。

### Step 2 — GREEN（不含 R12 修复）
1. 新建 `scripts/Storage/Yaml/WorldIndex.cs`：`public static class WorldIndex` + `public static World AppendMap(World world, string refPath)`，内部用 `with { MapIndex = ... }` 返回新实例，原 `MapIndex` 为 `null` 时按 `Array.Empty<>()` 处理。
2. `EditorContext.AddMap(MapDoc map, string refPath)`：先 `_maps.Add(map)`，再 `World = WorldIndex.AppendMap(World, refPath)`，最后 `EmitSignal(SignalName.MapChanged)`。三步顺序很关键——若漏掉 `World` 的赋值，新图进 `_maps` 但 `ProjectSaver` 按 `MapIndex` 遍历时会静默丢失。

**踩到的坑**：
- 初始 `NewWorldWithMapIndex` 用 `MapIndex = null!` 触发 `ProjectSaver.cs:36` 对 `world.DefaultTime` / `world.Era` 的 NRE。`World` 类的 positional record 走无参构造器才填好这些字段。改用 `var seed = new World(); return seed with { MapIndex = ... }` 解决。
- 测试用 `GridSpec(Width, Height, CellDistance)` 不传 `Origin`，命中 `GridCoordYamlConverter.WriteYaml` 在 `Origin == null` 时 NRE。现有 converter 在出范围清单中，所以测试侧改成 `Origin: new GridCoord(0, 0)`，符合 sample world 的实际用法。
- `null` 哨兵用 `params MapIndexEntry?[] entries` + `(MapIndexEntry?)null!` 显式 cast 区分「MapIndex = null」与「传 0 个元素」。

跑测试：4/5 通过，`Generation_OmittedFromYaml_WhenNull` 按预期失败（R12 未修）。

### Step 3 — R12 修复
按 Option A：在 `YamlSchemas.BuildSerializer()` 里追加一行：
```csharp
.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
```
放置在 `WithNamingConvention(...)` 与 `WithTypeConverter(...)` 之间，与 `scripts/Ui/Dock/PanelState.cs:148-151` 同一模式。XML doc 注释解释 R12 背景与「作用域为全部 Domain 记录」。

不采用 Option B（自定义 `ITypeInspector`）与 fallback（针对 `GenerationSpec?` 单独改），因为 Option A 在同代码库已落地且无副作用（其他 nullable 字段的语义本就该「未填时省略键」，与数据模型规格 §0 一致）。

跑测试：5/5 通过。

### Step 4 — REGRESSION
cwd = `examples\sample_world`（`ProjectLoaderTests` 从 cwd 向上找 `project.yaml`）。完整套件结果：

```
Total: 160, Errors: 0, Failed: 7, Skipped: 0, Time: 0.785s
```

7 个失败（与基线逐字一致）：
1. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`
2. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`
3. `WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`
4. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`
5. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`
6. `WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array`
7. `WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`

Δ 与基线 155/7 → 160/7 一致（+5 新测试，0 新失败）。

## 关键设计决策

### `WorldIndex.AppendMap` 是「唯一追加通道」
`World` 走 `with { ... }` 不可变 + `private set`，所以任何想改 `MapIndex` 的代码必须先拿到新 `World` 实例再赋回。集中在 `WorldIndex.AppendMap` 一处实现，规避散落各处的 `World = World with { MapIndex = world.MapIndex.Append(...).ToList() }`。

### `EditorContext.AddMap` 顺序
1. `_maps.Add(map)` — 编辑器侧的运行时缓存
2. `World = WorldIndex.AppendMap(...)` — 把 ref 写进工程索引
3. `EmitSignal(MapChanged)` — 通知 UI / 验证器刷新

如果先发信号，监听者可能从还没写索引的 `World` 读出 `MapIndex`，触发误判「索引与 _maps 不一致」之类的校验失败。

### R12 走 Option A 而非 Option B
Option A 与同代码库 `PanelState.cs` 一致；Option B 需要实现 `ITypeInspector` + `IPropertyDescriptor`，改动面大且需要逐字段过滤。`OmitNull` 的全局影响是良性的——本来就不应该把 null 字段写成 `key:` 空值。

## 未做的事

- 未修任何 7 个基线失败（明确出范围）
- 未在 `WorldGenerator` 里调用 `EditorContext.AddMap`（任务 8 范围只到「让 AddMap 通路可用 + 写测试」，实际接入是后续任务）
- 未在 `ProjectSaver` 里改「`maps.Count > MapIndex.Count` 时的策略」——这正是「silent data loss 陷阱」，但 brief 明确说不要改；测试 `MapsBeyondMapIndex_AreNotWritten` 只是把这个现状钉死。
- `cs.uid` 旁车文件、xunit 运行时 dll 等工具链产物未入仓（与 T7 一致）

## 后续 Task 8+ 提示

- `EditorContext.AddMap` 的调用方是 WorldGenerator（或类似的「生成按钮」UI），生成的 ref 路径建议走 `maps/world/<mapId>/main.yaml`（与 plan L301-319 约定一致）。
- 验证器（`ProjectValidator`）已知「`MapIndex` 与磁盘地图文件集合一致」是 warning 而非 error，所以沉默的 `MapsBeyondMapIndex` 暂时只被测试钉住。
