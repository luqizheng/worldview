# Task 14 Report —— TileSet 风格注册器预留接口（styles.yaml + TileSetStyleRegistry）

## 1. Status / Commit

- **Status**: DONE（代码 + 测试 + 全量回归 + 提交 + Godot 实机冒烟全部完成）
- **Commit**: `4504b57fd2b37787552b57fcb274821843e95254`（父 commit `953d072b24960610527a602949bd041454df7ece`，分支 `deepseek-build`）
- **Commit message**: `feat(storage): Task 14 - TileSet 风格注册器预留接口（styles.yaml + TileSetStyleRegistry）`
- **改动明细（7 文件，495 行插入，0 行删除）**：

```
 assets/textures/tiles/styles.yaml                  |   9 ++
 scripts/Editor/EditorContext.cs                    |  16 +++
 scripts/Editor/TileSetLoader.cs                    | 132 +++++++++++++++++++
 scripts/Render/TerrainTileSetFactory.cs            |   4 +
 scripts/Storage/Yaml/TileSetStyleRegistry.cs       | 143 ++++++++++++++++++++
 scripts/Ui/MapSettingsDialog.cs                    |  47 +++++++
 tests/WorldView.Tests/Storage/TileSetStyleRegistryTests.cs | 144 +++++++++++++++++++++
 7 files changed, 495 insertions(+)
```

**暂存纪律**：逐个 `git add` 上述 7 个路径，未使用 `git add -A` / `git add .`。工作区原有的约 273 项未暂存改动（其中 93 个 `*.uid` 是 UTF-8 BOM 噪音：`-uid://yc04783omol6` → `+﻿uid://yc04783omol6`）与 `examples/sample_world/` 下的构建产物**全部未被触碰**。**`scripts/Domain/MapDoc.cs` 不在本 commit 中**（reviewer 要求项已满足）。`.superpowers/sdd/**` 本身不被 git 跟踪（`git ls-files` 为空），因此本报告与截图未进入 commit。

**R23（先提交后碰 Godot）已遵守**：build → 全量回归 → commit `4504b57` → 才启动 Godot 冒烟。

---

## 2. 交付物（逐文件，含行号）

### 2.1 新增 `scripts/Storage/Yaml/TileSetStyleRegistry.cs`（143 行，纯逻辑层）

契约要求的类型与成员，全部按字面签名实现：

| 行 | 内容 |
|---|---|
| `:15` | `public sealed record TileSetStyle(string Id, string Path, string Description);` |
| `:31` | `public sealed class TileSetStyleRegistry` |
| `:34` | `private const string StylesRoot = "res://assets/textures/tiles";` |
| `:37` | `public IReadOnlyList<TileSetStyle> Styles { get; private set; }`（YAML 顺序，`Array.Empty` 初始） |
| `:40` | `public string DefaultId { get; private set; }` |
| `:43` | `public TileSetStyleRegistry(string yamlText)` |
| `:50` | `public TileSetStyleRegistry(Stream yaml)`（`StreamReader(..., leaveOpen: true)`） |
| `:61` | `public string AssetPathFor(string id)` |
| `:66` | 未知 id → `throw new ArgumentException($"未知的瓦片风格 id：'{id}'（可用：{string.Join("、", Styles.Select(s => s.Id))}）", nameof(id))`（plan L474 二选一，本实现选 **ArgumentException**，测试据此断言） |
| `:71` | `return $"{StylesRoot}/{style.Path}";` —— **纯字符串拼接，绝不碰 `ResourceLoader`，不读盘** |
| `:76` | `private void Parse(string yamlText)`（`new YamlStream().Load(new StringReader(yamlText))` + `YamlMappingNode`/`YamlSequenceNode`） |
| `:85` `:90` `:98` `:105` `:112` `:121` `:127` `:132` | 八处 `InvalidDataException`：YAML 解析失败 / 根非映射 / `styles` 非列表 / 列表项非映射 / 项缺 id 或 path / `styles` 为空 / 缺 `default` / `default` 不在 `styles` 中 |
| `:139` | `private static string Scalar(YamlMappingNode mapping, string key)` |

**无 `using Godot;`**（plan L489 的守卫）。该文件同时受 `scripts/Storage/**` 架构约束。

### 2.2 新增 `scripts/Editor/TileSetLoader.cs`（132 行，Godot 感知层，按 **R25** 落在 `scripts/Editor/`）

| 行 | 内容 |
|---|---|
| `:27` | `public static class TileSetLoader`（namespace `WorldView.Editor`） |
| `:30` | `public const string StylesYamlPath = "res://assets/textures/tiles/styles.yaml";` |
| `:33` | `private const string TileSetResourceName = "tileset.tres";` |
| `:36` | `BuiltInFallbackYaml`（内置兜底风格表，`default: _fallback_solid_color`；styles.yaml 缺失时使用） |
| `:44` | `private static readonly Dictionary<string, TileSet> Cache = new(StringComparer.Ordinal);` |
| `:48` | `public static TileSetStyleRegistry Registry => _registry ??= LoadRegistry();` |
| `:51` | `public static IReadOnlyList<string> AvailableIds` |
| `:54` | `public static string DefaultId` |
| `:60` | `public static TileSet Get(string id)`（空 id → Fallback；否则 cache → `LoadFromAsset` → cache） |
| `:72` | `Get` 内的 `LoadFromAsset(id)` 调用点（实机 backtrace 命中行） |
| `:78` | `public static void WarmUp() => _ = Get(DefaultId);` |
| `:82` | `private static TileSet LoadFromAsset(string id)`（候选路径 `{path, $"{path.TrimEnd('/', '.')}/{TileSetResourceName}"}`） |
| `:90` | `if (ResourceLoader.Exists(candidate) && ResourceLoader.Load(candidate) is TileSet loaded)` |
| `:104` | `private static TileSet Fallback(string id)` |
| `:106` | `GD.PushWarning($"WARN: tileset '{id}' 加载失败，退化到纯色块");` —— **唯一一行 WARN** |
| `:110` | `private static TileSetStyleRegistry LoadRegistry()` |
| `:115` `:117` | `Godot.FileAccess.FileExists(StylesYamlPath)` / `Godot.FileAccess.GetFileAsString(StylesYamlPath)`（**必须写全 `Godot.` 前缀**，见 §4.1） |
| `:129` | 读不到 styles.yaml 时：`GD.PushWarning($"WARN: 未读到可用的 {StylesYamlPath}，退化到内置纯色风格表")` + 内置表 |

**不出现 `File.` / `new StreamReader(`**（plan L492 守卫）—— 一切走 `res://` + `Godot.FileAccess`。

### 2.3 新增 `assets/textures/tiles/styles.yaml`（9 行，契约逐字照抄 plan L442-453）

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

### 2.4 新增 `tests/WorldView.Tests/Storage/TileSetStyleRegistryTests.cs`（144 行，7 测试，纯逻辑）

| 行 | 测试 |
|---|---|
| `:37` | `Parse_MinimalYaml_ReadsDefault` |
| `:49` | `Parse_TwoStyles_ExposesBoth`（断言 YAML 顺序 `{"jrpg","_fallback_solid_color"}`） |
| `:59` | `AssetPathFor_KnownId_ReturnsResPath`（`res://assets/textures/tiles/.` 与 `…/solid`） |
| `:68` | `AssetPathFor_UnknownId_Throws`（`ArgumentException` 且 message 含该 id） |
| `:77` | `DefaultId_NotInStyles_Throws`（`InvalidDataException`） |
| `:91` | `Parse_EmptyStyles_Throws`（`InvalidDataException`） |
| `:103` | `Styles_Yaml_Contract_Is_Parsed_From_Real_File`（端到端契约测试：把**真实** `assets/textures/tiles/styles.yaml` 复制到临时目录后解析，断言 id 含 `"jrpg"`、`DefaultId == "jrpg"`、`AssetPathFor("jrpg")` 以 `res://assets/textures/tiles` 开头；路径由 `:129` 的 `RepoStylesYamlPath` 从 cwd 向上找） |

无任何 xunit 触碰 Godot（Godot 感知层没有 xunit，符合 plan/简报）。

### 2.5 修改 `scripts/Render/TerrainTileSetFactory.cs`（+4，**仅文档注释**）

唯一改动是 `:16-19` 追加一段 `<para>`，说明 T14 起 `Build()` 兼任 `TileSetLoader` 的兜底，因此必须保持"不依赖任何外部资产、可独立调用"。**`Build()` 的合成逻辑一行未动**（仍是 11 个纯色块 + `TileSetAtlasSource` + `SourceId=0`）。

> ⚠️ **刻意未做**：plan L486 建议把 `Build()` 内部改成 `return TileSetRegistry.Get(TileSetRegistry.DefaultId);`。若照做，兜底路径会形成 `Build() → Get() → Fallback() → Build()` **无限递归**。本实现保持 `Build()` 无依赖，由 `TileSetLoader` 单向调用它。请 reviewer 确认该偏离是有意且必要的。

### 2.6 修改 `scripts/Editor/EditorContext.cs`（+16，仅进程内状态）

| 行 | 内容 |
|---|---|
| `:53` | 新增 `public override void _Ready()`（此前该类只有 `_EnterTree`/`_ExitTree`，无 `_Ready`） |
| `:57` | `TileSetLoader.WarmUp();` |
| `:151` | `public string CurrentTilesetId { get; set; } = TileSetLoader.DefaultId;`，注释「仅进程内状态，不落盘」（沿用 `:125-126` `RefineOnStroke` 的既有模式） |

**零持久化改动**：无 `MapDoc` 字段、无 YAML、`ProjectSaver` / `YamlSchemas` / `ProjectValidator` 全未触碰（plan L440 明确把持久化推到下一轮）。

### 2.7 修改 `scripts/Ui/MapSettingsDialog.cs`（+47）

| 行 | 内容 |
|---|---|
| `:53` | `private OptionButton? _tileStyle;`（紧随 `_resCustom`） |
| `:178-191` | `BuildUi()` 中「实际覆盖」行之后新增一行：`Label "瓦片风格"` + `OptionButton`（`:183` TooltipText 注明风格来自 styles.yaml 且本轮仅进程内生效）；`:186-188` `foreach (var styleId in TileSetLoader.AvailableIds) _tileStyle.AddItem(styleId);`；`:190` `ItemSelected += OnTileStyleSelected` |
| `:309-310` | `ApplyMode()` 末尾 `SelectTileStyle(_context?.CurrentTilesetId ?? TileSetLoader.DefaultId);`（**两种态都显示当前值**） |
| `:441` | `private void OnTileStyleSelected(long index)` → 写 `_context.CurrentTilesetId` |
| `:450` `:462` | `private void SelectTileStyle(string id)` → 按 id 回选 `_tileStyle.Selected` |

标签用中文「瓦片风格」而非英文 "Tile Style"，与对话框其余行（尺寸/分辨率/实际覆盖/陆地比例…）保持一致。`WorldCanvas.cs` 未被触碰。

---

## 3. R24 / R25 实施说明

### R24 —— 仓库布局与 plan 假设不符

仓库原本**没有根 `assets/`**，`res://assets/textures/tiles/` 不存在；真实图集在 `examples/sample_world/assets/textures/tiles/`（属样例工程，不是 app）。本任务**新建** `assets/textures/tiles/styles.yaml` 使契约路径可解析，并保留 plan 的逐字内容。

**必须明说的结论（本报告的核心诚实项）**：因为 `res://assets/textures/tiles/.` 下**没有 TileSet 资源**（`TerrainTileSetFactory.Build()` 从不加载任何 PNG，它用 `Image.CreateEmpty` + `FillRect` + `ImageTexture.CreateFromImage` 现场合成 11 个纯色块，见该文件 `:10-15` 的类注释），所以：

> **`jrpg` 这条 hook 目前恒走纯色兜底 —— JRPG 图集并未被消费。**

plan L440 的「`DefaultId` 是 `jrpg`，等于现状」这句话在本实现里**靠兜底成立，而不是靠图集成立**。本轮没有产生任何视觉变化，也没有产生任何"风格"效果：`Get(anyId)` 目前必然返回 `TerrainTileSetFactory.Build()` 的结果。§6 的实机日志正是这条论断的直接证据。

### R25 —— Godot 感知层放 `scripts/Editor/` 而非 `scripts/Render/`

plan 给了两个备选（L462 `WorldView.Render.TileSetRegistry` vs L468 替代位置）。本实现选 **L468**：新文件 `scripts/Editor/TileSetLoader.cs`，类名 `TileSetLoader`（namespace `WorldView.Editor`）。

理由（原样记录）：整份计划的 Global Constraint 写明「`scripts/Render/**` modification is approved for `WorldCanvas.cs` only」。在 `scripts/Render/` 下**新建**文件不属于"修改 `WorldCanvas.cs`"，会违反该约束；而 `scripts/Editor/` 已经是 `EditorContext` / `EditorController` / `MapSizing` 这些 Godot 感知代码的既有归宿。

副作用（需 reviewer 知晓）：plan L496-499 的冒烟命令写的是 `eval TileSetRegistry.Get("jrpg")`，本实现里对应物叫 `TileSetLoader.Get("jrpg")`（见 §6.6 关于"该 eval 形式是否可行"的说明）。

---

## 4. 构建 / 测试 / 变异证据

### 4.1 构建

首次 `dotnet build tests\WorldView.Tests\WorldView.Tests.csproj -v q --nologo` **失败**，2 个错误：

```
scripts/Editor/TileSetLoader.cs(114,17): error CS0104: "FileAccess"是"Godot.FileAccess"和"System.IO.FileAccess"之间的不明确的引用 [WorldView.csproj]
scripts/Editor/TileSetLoader.cs(116,28): error CS0104: "FileAccess"是"Godot.FileAccess"和"System.IO.FileAccess"之间的不明确的引用 [WorldView.csproj]
```

原因：`WorldView.csproj` 打开了 `ImplicitUsings`，`System.IO` 已在隐式 using 中，与 `using Godot;` 的 `FileAccess` 撞名。修法是显式限定 `Godot.FileAccess.FileExists` / `Godot.FileAccess.GetFileAsString`（`TileSetLoader.cs:115/117`）。**重新构建：`0 个错误`**（仅 5 个既有的 nullable 提示，位于其他文件）。

### 4.2 单类 GREEN

```
cd D:\kudi-project\world-view\examples\sample_world
dotnet exec --runtimeconfig ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.runtimeconfig.json \
  --depsfile ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.deps.json \
  ..\..\tests\WorldView.Tests\bin\Debug\net8.0\xunit.console.dll \
  ..\..\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll -nologo -class WorldView.Tests.Storage.TileSetStyleRegistryTests
=== TEST EXECUTION SUMMARY ===
   WorldView.Tests  Total: 7, Errors: 0, Failed: 0, Skipped: 0, Time: 0.101s
```

### 4.3 变异检验（验收标准：改动生产代码 → 指定测试变红 → 还原）

每次变异后重跑上面的单类命令，再 `git checkout` 还原，最后 `Select-String MUTATION` 确认工作区无残留：

| 变异 | 变红的测试 |
|---|---|
| `AssetPathFor` 改成只 `return $"{StylesRoot}";` | `AssetPathFor_KnownId_ReturnsResPath` |
| `if (false && !styles.Any(s => s.Id == defaultId))` | `DefaultId_NotInStyles_Throws` |
| 解析后 `styles.Insert(0, new TileSetStyle(id, path, …))`（打乱顺序） | `Parse_TwoStyles_ExposesBoth` |
| `if (false && style is null)` + description 写成 `string.Empty` | `Parse_MinimalYaml_ReadsDefault` + `AssetPathFor_UnknownId_Throws` |

**诚实说明（覆盖空档）**：把 `styles.Count == 0` 那处守卫改成永不触发，**观测不到任何测试变红**——因为 `styles: []` 之后必然触发「`default` 不在 `styles` 中」那条校验，同样抛 `InvalidDataException`。也就是说 `:121` 这条空列表守卫属于防御性代码，**没有独立的变异证据**，只有一条通过路径（`Parse_EmptyStyles_Throws` 走的是后者）。plan L470-477 要求该测试存在，本实现满足了，但不应声称它"证死了"空列表分支。

### 4.4 全量回归

```
WorldView.Tests  Total: 184, Errors: 0, Failed: 7, Skipped: 0, Time: 0.767s
```

`Total` 由基线 177 → **184**（+7，无 `[Skip]`/`[Trait]`，无 `dotnet test`），`Failed` 仍为 **7**，且**逐条比对仍是基线那 7 个**（无新增、无消失、未被"修好"）：

1. `WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`
2. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`
3. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`
4. `WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`
5. `WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`
6. `WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array`
7. `WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`

---

## 5. "渲染行为不变"的论证（代码层，非截图层）

本任务的硬指标是"app 渲染逐字节行为不变"。论证由三条可审计事实组成：

1. `git show --stat 4504b57` 中**没有 `scripts/Render/WorldCanvas.cs`**，也没有 `TerrainLayer.cs` / `TerrainPalette.cs`。渲染路径代码未改。
2. `scripts/Render/TerrainTileSetFactory.Build()` 的合成逻辑**一行未改**（该文件仅 +4 行注释），因此它返回的 TileSet 与改动前**同一份实现、同一批颜色、同一 `SourceId`/`TilePixels`**。
3. `WorldCanvas.cs:153` 仍然是 `var tileSet = TerrainTileSetFactory.Build();`（`:154/:445/:543` 仍用 `TerrainTileSetFactory.TilePixels`）——**画布根本不经过 `TileSetLoader`**。`TileSetLoader` 只在 `EditorContext._Ready()` 里被 `WarmUp()` 调一次，作用是"把兜底 TileSet 填进缓存"，不会替换画布用的 TileSet。

所以：本轮 `Get()` 恒返回 `Build()` 的结果，而画布本来就直接用 `Build()` 的结果 —— 两条路径指向同一个对象来源，渲染不可能改变。**§6.4 的截图只用于证明"启动不崩、UI 正常"，不用来主张像素级一致**（我没有采集改动前的基线截图，不做无证据的比较）。

---

## 6. Godot 实机冒烟（在 commit `4504b57` 之后进行）

### 6.1 启动

`godot_run_project`（`D:\kudi-project\world-view`，引擎 `C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`）返回：`managed: true`、`external: false`、`port: 9090`、`game_ready: true`，`McpInteractionServer` 自动加载 ok（全程只用 `godot_*` 工具，未在 pwsh/bash 里跑 Godot）。冒烟结束后用 `godot_stop_project` 正常停止（`stopped: true, managed: true`）。

### 6.2 兜底路径的实机证据（原样引用 stderr）

```
WARNING: WARN: tileset 'jrpg' 加载失败，退化到纯色块
   at: Godot.TileSet WorldView.Editor.TileSetLoader.Fallback(string) (res://scripts/Editor/TileSetLoader.cs:106)
   C# backtrace (most recent call first):
       [1] Godot.TileSet WorldView.Editor.TileSetLoader.Fallback(string) (D:\kudi-project\world-view\scripts\Editor\TileSetLoader.cs:106)
       [2] Godot.TileSet WorldView.Editor.TileSetLoader.LoadFromAsset(string) (…\scripts\Editor\TileSetLoader.cs:101)
       [3] Godot.TileSet WorldView.Editor.TileSetLoader.Get(string) (…\scripts\Editor\TileSetLoader.cs:72)
       [4] void WorldView.Editor.TileSetLoader.WarmUp() (…\scripts\Editor\TileSetLoader.cs:78)
       [5] void WorldView.Editor.EditorContext._Ready() (…\scripts\Editor\EditorContext.cs:57)
       [6] WorldView.Editor.EditorController.EnsureContext() (…\scripts\Editor\EditorController.cs:118)
       [7] void WorldView.Editor.EditorController._Ready() (…\scripts\Editor\EditorController.cs:57)
```

这条日志同时证明四件事：

1. `styles.yaml` 在引擎里**被找到并成功解析**（否则会出现 `:129` 的另一条 `WARN: 未读到可用的 …`，stderr 中没有）；
2. `EditorContext.CurrentTilesetId` 的默认值 `jrpg` 被真实解析出来并传到了 `Get("jrpg")`；
3. `jrpg` 解析出的路径 `res://assets/textures/tiles/.` 下**没有 TileSet 资源**，于是走了**兜底 + 恰好一行 WARN**，**没有抛异常**；
4. `_Ready()` 正常返回、`EditorController._Ready()` 继续执行，app 正常启动。

不相关的既有噪音（非本轮引入）：`autoload/mcp_interaction_server.gd` 的 GDScript 警告（局部变量 `name` 遮蔽基类属性，行 2598/2605/2611；整数当枚举用，行 2977/3134）。

### 6.3 下拉框实机内容（打开"地图…"对话框后）

用 `emit_signal`（signal `pressed`）触发 `MapSettingsBtn`（`/root/Main/UiLayer/DockLayout/Root/TopStrip/TopBar/MapSettingsBtn`，Text "地图…"，位置 138,0，尺寸 64×32），随后查询节点树：

- `find_nodes_by_class AcceptDialog` → `MapSettingsDialog` 已实例化（另一条是既有的 `@FileDialog@174`），`visible = true`；
- `get_node_info` 的方法表里出现 `OnTileStyleSelected` / `SelectTileStyle`（证明跑的是 T14 的编译产物）；
- `find_nodes_by_class OptionButton` → 2 个：
  - `/root/Main/MapSettingsDialog/Body/@GridContainer@195/@HBoxContainer@221/@OptionButton@217`（T13 的分辨率预设，含自定义 SpinBox 的 HBox 内）
  - `/root/Main/MapSettingsDialog/Body/@GridContainer@195/@OptionButton@235` ← **T14 的「瓦片风格」**
- 该下拉：`item_count = 2`；`get_item_text(0) = "jrpg"`；`get_item_text(1) = "_fallback_solid_color"`；`selected = 0`（= `styles.yaml` 的 `default: jrpg`）。

即：**"注册器里多一条风格，下拉就多一个条目"这条 hook 在真实运行中成立**，且对话框是以"改尺寸"态（已加载地图）打开、UI 完整构建、无异常——T13 的两态未被破坏。

**诚实限制**：我是用「合成 `pressed` 信号 + 节点属性查询」而不是真实鼠标点击取证的。原因：`godot_command click` 传 `node_path` 时返回的点击坐标是 `(0,0)`（未落到按钮上，第一次尝试后对话框没被打开），所以改为直接发信号。因此这是**节点树级证据**（内容确凿），不是"人类点击 + 截图"级证据。

### 6.4 截图（仅用于"启动不崩 / UI 正常"）

`.superpowers/sdd/2026-10-08-map-size-brush-and-world-generation/task-14-smoke.png`（1152×648）。截图显示：顶栏（打开工程…/保存/地图…/艾瑟兰大陆）、时间轴、构建面板（民居/城堡/宗教/军事/自然 + 城门）、小地图、检视器（地图：艾瑟兰大陆 / `map_world_kael` / World / 64×48 / 10 km / 图层 4）、底部状态栏（坐标(-,-) / 就绪 / **校验 9 错误**）均正常绘制；主画布区域内容稀疏（只有零星几格彩色方块与线状要素），画布本身未见异常报错。

**明确不主张**：我没有改动前的基线截图，因此**不用这张图断言"像素一致"**；像素一致性的依据是 §5 的代码事实（画布与工厂均未改，且画布不经过 `TileSetLoader`）。

### 6.5 一条非 T14 引入的 ERROR（如实上报，未归因）

触发 `pressed` 时 stderr 出现：

```
ERROR: Attempting to make child window exclusive, but the parent window already has another exclusive child.
       This window: /root/Main/MapSettingsDialog, parent window: /root, current exclusive child window: /root/Main/@FileDialog@174
   at: _set_transient_exclusive_child (scene/main/window.cpp:1160)
       [2] void WorldView.Editor.EditorController.OnMapSettingsPressed() (…\scripts\Editor\EditorController.cs:202)
```

这是 Godot 的窗口独占冲突（`MapSettingsDialog` 想独占成为 `/root` 的 exclusive child，但既有的 `@FileDialog@174` 已占位）。对话框随后仍然 `visible = true` 且 UI 完整，功能未受影响。**该 commit 不包含 `EditorController.cs`**（diffstat 7 文件中没有它），`@FileDialog@174` 也是既有节点，因此这不是 T14 引入的路径；但我**没有**回退到父 commit 去复现，故不宣称"改动前必然同样报错"，仅作现象上报，供最终 review 判断是否需要单开一条小修。

### 6.6 没做成的事（plan L496-499 的字面冒烟）

plan 建议的 `eval TileSetRegistry.Get("jrpg")` / `eval EditorContext.CurrentTilesetId` **无法按字面执行**，两个原因叠加：

1. R25 已把该类改名并移位为 `WorldView.Editor.TileSetLoader`；
2. `godot_command eval` 执行的是 GDScript，而 **GDScript 引用不到 C# 静态类**（`TileSetLoader` 不是 Node/`[GlobalClass]`）；同一限制在 R21 里有既证（T11 用 `get_node(...).get("UndoStack")` 探测到 null，`EditorContext.Instance` 亦不可达）。C# 属性也不通过 Godot 的 `get()` 暴露，所以 `CurrentTilesetId` 同样读不到。

替代取证已在 §6.2 / §6.3 完成：真实调用链（`EditorContext._Ready → WarmUp → Get("jrpg")`）在引擎里跑通并打出预期 WARN；`AvailableIds` 的效果由下拉框的两条目证明。另外我**没有**跑 `Get("__nonexistent__")` 的实机探针（同因不可达），但该分支由 `AssetPathFor_UnknownId_Throws` 与"未知 id 不崩、走 Fallback"的代码路径覆盖（`Get` 对 cache miss 一律 `LoadFromAsset` → `Fallback`，不抛异常）。

---

## 7. 已知限制 / 下一轮要做什么

本轮交付的是**接口与数据通路**，不是风格本身。要真正"上线一个风格"，后续轮次至少要：

1. **产出真实 TileSet 资源**：在 `assets/textures/tiles/<style>/` 放 PNG 图集 + 生成 `tileset.tres`（`TileSetLoader.LoadFromAsset` 已按 `res://assets/textures/tiles/{path}` 与 `…/{path}/tileset.tres` 两个候选路径查找）。在此之前 `jrpg` **恒走纯色兜底**。
2. **把 TileSet 真正接到画布**：目前 `WorldCanvas.cs:153` 仍直接调 `TerrainTileSetFactory.Build()`；接风格时需要让画布改为向 `TileSetLoader.Get(EditorContext.CurrentTilesetId)` 取 TableSet，并处理"TileSetAtlasSource 的坐标契约"差异（现有代码用 `AtlasCoordsOf(TerrainType)` 推源坐标，图集风格必须满足 11 格/32px 的同构布局，或引入 per-style 的坐标映射）。
3. **持久化**：把 `CurrentTilesetId` 落盘（`MapDoc` 字段 + `YamlSchemas` + `ProjectSaver` + `ProjectValidator` 校验），本任务按 plan L440 明确未做。
4. **3 风格的真正实现**：本任务只预留 2 条（`jrpg` + `_fallback_solid_color`），plan 里的"3 风格"是下一轮的设计目标。
5. 小尾巴：本轮新增的 3 个 `.cs` 尚无 `.uid` 侧车（与 T7–T13 的既有惯例一致，那些 commit 也都没带 `.uid`；Godot 编辑器下一次导入资源时可能会生成，届时应一并提交）。

其它诚实备注：

- `_fallback_solid_color` 这条风格的 `path: solid` 只是占位：`res://assets/textures/tiles/solid` 不存在，它同样走兜底。它存在的意义是"注册表里必须有一条明显是兜底的条目"，以及当 `styles.yaml` 整体读不到时内置表以它作 `default`。
- `version: 1` 被解析但**当前没有任何版本门控/校验逻辑**（没有按 version 分支或拒绝未知版本）——plan 未要求，本轮不构建。
- 缓存与注册表都是 `static`，因此在同一进程内**不会**在 styles.yaml 变化后重新加载（重启才生效）。本轮 styles.yaml 由代码仓库固定，故可接受；将来若要在编辑器里热改风格表，需要加显式刷新入口。

---

## 8. 给 review 的检查点

- [ ] `git show --stat 4504b57` 只有 7 个文件；**无 `MapDoc.cs`**、无 `ProjectSaver/YamlSchemas/ProjectValidator`、无 `WorldCanvas.cs`。
- [ ] `assets/textures/tiles/styles.yaml` 与 plan L442-453 逐字一致。
- [ ] `scripts/Storage/Yaml/TileSetStyleRegistry.cs` 中 grep 不到 `Godot` / `ResourceLoader`；构造器不读盘（只有 `StringReader` / 传入的 `Stream`）。
- [ ] `scripts/Editor/TileSetLoader.cs` 中 grep 不到 `File.` / `new StreamReader(`；失败路径只有 `:106` 一行 WARN，且返回 `TerrainTileSetFactory.Build()`。
- [ ] `TerrainTileSetFactory.Build()` 未被改成委托给 `TileSetLoader`（那会无限递归），diff 仅 4 行注释。
- [ ] 全量回归 `Total: 184, Failed: 7`，7 个失败名与基线完全一致。
- [ ] §6.2 的实机 WARN 支持"styles.yaml 被解析 + jrpg 走兜底 + 不崩"；§3 的 R24 结论（JRPG 图集未被消费）没有被夸大。
- [ ] 判断 §6.5 的窗口独占 ERROR 是否要单开小修（非 T14 引入，但会污染对话框打开时的日志）。
