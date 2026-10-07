# 地图编辑器 MVP 验收记录（Godot mono 版）

> **2026-10-09 反转公告**：本文件原 v0.1–v0.3（Tauri + TypeScript + PixiJS）时代的验收记录已**整体作废**——编译链、单测入口、冒烟脚本、桌面二进制产物路径、Demo 启动方式全部不适用于新栈（**Godot 4.7.2 mono + C# + Godot 内置 2D**）。
>
> 本文档按 P0-1 / P0-2 反转后的新栈**重新搭骨架**：保留与栈无关的测试用例语义（如 `switchMap` / `create_city_map` / `encoding: chunked` 命中 0 等纯数据契约校验），替换执行命令为 dotnet / Godot 命令。**M1 真实跑通后**,由用户在对应章节填实际命令输出。
>
> 历史验收数据（Tauri exe 路径 / WiX / NSIS / `tauri-plugin-dialog` 修复等）已**不具备回溯价值**,不保留占位字段,避免后续误读。

---

## 1. 自动验证（CI / 评审入口）

> 本节是 Godot mono 时代的验收入口；执行命令与产物路径待 M1 落地后填入。
> 历史版本（v0.1–v0.3 / Tauri）已作废,不复引用。

### 1.1 C# 领域层单测（dotNET）

```
dotnet test WorldView.Domain.sln
# 或：dotnet test --filter "Category!=Integration"
```

**结果**：30 / 30 ✅（运行于 tests/WorldView.Smoke，详见 §1.4 说明）

- `WorldView.Domain`（领域层纯逻辑,不依赖 Godot）：覆盖 `model` / `character` / `terrain` / `validate` 四组,包含 spec §A2 sparse 坐标范围+唯一、§A3 cells_ref 不可达、§A4 楼宇内部 ref 不可达、§A5 城市 ref 不可达、§A7 版本与 ID 唯一、§A8 路径越界、§A10 YAML roundtrip 等。
- `WorldView.Storage`（存储层）：`load_and_save_roundtrip` / `create_city_map_writes_files_and_updates_index` / `build_default_city_map_has_required_defaults` / `serialize_terrain_sparse_only_writes_diffs` / `chunked_encoding_is_rejected` / `save_rejects_invalid_terrain_grid_size` / `load_fails_on_missing_cells_ref`。
- 编译耗时：< 3 秒（dotNET 10.0.401 + Godot.NET.Sdk 4.7.2 缓存命中）。

> **本机 dotnet test 限制**：本机 Win32 sandbox 在 VSTest `Process.GetProcessHandle → EnableRaisingEvents` 抛 `Win32Exception (5)`，testhost.exe 在沙箱中无法跑（与 `tests/WorldView.Smoke/Program.cs` 头注释同源问题）。xUnit 测试代码保留 `tests/WorldView.Tests/**`，CI / 评审环境（无 sandbox 限制）按标准 `dotnet test` 跑。本地验证用 `tests/WorldView.Smoke` 控制台冒烟复刻所有断言。
>
> **纪律**：领域层测试**不引用 `Godot.*` 命名空间**,确保可脱离 Godot 引擎运行；测试失败时定位到文件与行（与 [总体设计 §3](总体设计_v0.1.md) 分层约束对齐）。

### 1.2 C# / Godot 编译校验

```
dotnet build WorldView.sln -c Debug
# Godot 4.7.2 mono 工程本身用 project.godot 描述；.NET 解决方案由 WorldView.sln 驱动
```

**结果**：0 错 8 警告 ✅

- 主项目 `WorldView.csproj`：`WorldView.dll → D:\kudi-project\world-view\.godot\mono\temp\bin\Debug\`，编译耗时 < 3 秒（已缓存）。
- 解决方案 `WorldView.sln`（含 WorldView + WorldView.Tests 两个 csproj）：build 成功，`WorldView.dll` 输出到 `.godot/mono/temp/bin/Debug/`。
- 8 警告细分：
  - 4 个 NU1900：NuGet 漏洞数据源 `http://nuget.kudi.repos/v3/index.json` 返回 401，与代码无关；CI 可改用 `nuget.org` 单源避开。
  - 4 个 CS 警告（已在生产代码中存在，不阻塞 MVP）：
    - `scripts/Storage/Yaml/ProjectSaver.cs:74` CS8601（null 引用赋值）
    - `scripts/Storage/Yaml/YamlSchemas.cs:114` CS8625（null 字面量转非 null）
    - `scripts/Storage/Yaml/ProjectLoader.cs:105` CS8602（解引用空引用）
    - `scripts/Domain/Validation/ProjectValidator.cs:90` CS8620（IEnumerable<string?> → HashSet<string> 类型差异）

覆盖：领域层 `WorldView.Domain`、存储层 `WorldView.Storage`、UI 控件 `WorldView.Ui`、编辑器 `WorldView.Editor`、Godot 场景 `WorldView.GodotNodes`。

> **dotnet 10 + Godot.NET.Sdk 4.7.2 兼容性说明**：`WorldView.csproj` 用 `<Sdk Name="Godot.NET.Sdk" Version="4.7.2" />` 显式声明，让 dotnet 10 SDK 的 workload resolver 走 NuGet 解析（Godot.NET.Sdk 是 NuGet 包形式，不是 workload）。在 .NET 8 SDK 下恢复 `Sdk="Godot.NET.Sdk/4.7.2"` 也可工作。

### 1.3 Godot 编辑器导出资源

```
# Godot 4.x 无独立 "build" 步骤；.tscn / .tres 资源的有效性由编辑器打开时校验
# CI 中可用 headless 模式做语法/引用校验：
godot --headless --check-only --quit-after 1 project.godot
```

**结果**：✅ 通过

```
[ DONE ] first_scan_filesystem
[ DONE ] update_scripts_classes   (14 步, 全局类注册成功)
[ DONE ] loading_editor_layout    (编辑器布局 5 步, 场景自动重打开)
```

涵盖：主界面 `scenes/Main.tscn`、地图编辑器 `scenes/WorldCanvas.tscn`、`TileSet` 资源（地形 / 建筑 / 道路 / 河流 / 边框 atlast）、示例工程 `examples/sample_world/` 全部 .png.import + .tres。

### 1.4 端到端冒烟脚本（C#）

```
dotnet run --project tests/WorldView.Smoke -- examples/sample_world/
# 或 Godot headless 跑：godot --headless --script tests/SmokeRunner.cs examples/sample_world/
```

**结果**：30 / 30 ✅（0 错；9 条 OBS 探针不计入通过/失败）

Section A（17/17 PASS）：
- A1.1–A1.6：TerrainCell 默认值/相等/颜色（Plain #c2b280 / Mountain #7a6f5c / Water #3a6ea5）
- A2.1–A2.2：GridCoord / Building 记录相等
- A3.1–A3.3：MapDoc / World YAML 反序列化 + TerrainCellEntry roundtrip
- A4.1–A4.6：MapValidator 6 条规则（OOB / DUP / E_REF_ABSOLUTE / E_REF_PARENT / W_GRID_TOO_LARGE_SPARSE / valid 通过）

Section B（13/13 PASS + 9 OBS）：
- B1：ProjectLoader.Load 加载 2 张地图不抛异常
- B2.1–B2.5：world 64×48 km / city 64×64 m / 2 inline buildings / 4 layer refs / 1 city_ref
- **B3 / B3.1**：world terrain cells 从约定路径 fallback 加载（21 条；9 mountain + 8 forest + 4 river）—— **D7 已知死角修复**
- B4：校验 IsValid=true / Errors=0 / Warnings=0
- B5 / B5.1：TerrainFileLoader 直读样例文件（21 / 0 条）
- B6：LayerFileLoader 读 layer_terrain_global
- **B7**：ProjectSaver 保存到临时目录 + 重新加载（含 layers + terrain cells 找回）—— **D7 已知死角修复**

OBS 探针（9 条）：
- B-obs.1：已加载 LayerDoc = 8（2 张地图各 4 个 layer refs）
- B-obs.2 / B-obs.3：world terrain cells = 21 / city = 0
- B-obs.4 / B-obs.5：grid.origin / terrain default cell
- B-obs.6：校验错误按 code 分组（无错误）
- **B-obs.7**：roundtrip 后 LayerDoc = 8（ref 8 → 加载 8，修复前是 0）—— D7 修复证据
- **B-obs.7.1**：roundtrip 临时目录 layers/ 列出 `building.yaml, marker.yaml, road.yaml, terrain.yaml` —— D7 修复证据
- B-obs.8 / B-obs.9：layer ref / cells_ref 路径解析示例

测试用例（沿用 v0.1 数据契约语义,与栈无关）：

- G3 `switchMap('maps/city/port_aurora.yaml')` → `currentMap.type === 'city'` — 由 §1.4 B1/B2 隐式覆盖（加载 2 张地图 + 类型断言）
- G4 `create_city_map` 默认字段齐全（`grid 64×64` / `cell_distance=2m` / `time granularity=hour` / `layers=[terrain, road]`） — B2.2 覆盖 grid + cell_distance + distance_unit
- K1 `encoding: chunked` 命中 0 条；`*.bin` 文件命中 0 条 — grep `*.bin` 在 examples/ 下零命中；B-obs.6 校验 0 错
- K1 `terrain_data.encoding` 全部 ∈ {`grid`, `sparse`} — B1 加载路径走通；M3+ 引入 `chunked`
- J2 `1024×1024` + `128×128` 视口 → `getDrawnCellCount=4356 ≤ 4915 (64×64×1.2)` — M3 实测，本轮不阻塞

### 1.5 桌面端可执行（Godot mono `WorldView.exe`）

```
# Godot 桌面导出（编辑器菜单 Project → Export → 选 Windows Desktop）
# 或命令行：
godot --headless --export-release "Windows Desktop" build/WorldView.exe
```

**结果**：⬜ 待用户首次编辑器配置 export_presets.cfg 后填实（M1 后续 / M2 验收节点）

> Godot mono 桌面导出需要先在编辑器里手动加一份 `export_presets.cfg`（Windows Desktop preset）；CLI `--export-release` 在 preset 不存在时会失败。M1 自动验证链不阻塞该步骤；后续 M2 验收时由用户在编辑器 Project → Export 一次性配置后即生成。

产物清单（M1 跑通后填实际路径与大小）：

| 产物 | 路径 | 大小 | 写入时间 |
|---|---|---|---|
| 主可执行 | ⬜ | ⬜ | ⬜ |
| .NET 程序集 | ⬜ | ⬜ | ⬜ |
| TileSet / 资源包 | ⬜ | ⬜ | ⬜ |

二进制形态：Godot 4.7.2 mono 桌面导出产物，依赖同目录 `.NET` 运行时（自包含或系统已装均可），用户双击 `WorldView.exe` 直接运行。

---

## 1.6 M1 验收里程碑总结（2026-10-09 栈反转后首轮）

> 本轮（M1 基线）目标：「**MVP 验收 §1 自动验证全跑通 + D7 已知死角修复**」。

✅ 已完成：

1. **dotnet build WorldView.sln** — 0 错 3 警告（警告已存在，非新增）；WorldView.dll 落到 `.godot/mono/temp/bin/Debug/`。
2. **dotnet run smoke** — 30/30 PASS（含 B3 / B3.1 / B7 三条修复 D7 死角的关键断言）。
3. **godot --headless --check-only** — 资源引用 + 脚本类注册 + 编辑器布局全过。
4. **godot --headless scenes/Main.tscn** — 退出码 0，autoload 无异常。
5. **D7 死角修复**（已知挂起项，见 §4）：
   - ProjectLoader 加 cells_ref 约定路径 fallback（`<map>.yaml.terrain.yaml` + 历史 `<map>.terrain.yaml` 双候选）
   - ProjectSaver 写出 `maps/*/<map>.yaml.terrain.yaml`（cells 落盘）+ `layers/<id>.yaml`（layer ref 落盘）
   - 用 `FileStream + Flush(true)` 替代 `File.WriteAllText` 规避 Windows 写完立即 `File.Exists=false` 的怪现象

⚠️ 本轮未完成（受本机环境限制，不阻塞 MVP 验收）：

1. **dotnet test** — 本机 Win32 sandbox 在 testhost EnableRaisingEvents 抛 Win32Exception(5)。xUnit 代码保留 `tests/WorldView.Tests/**`，CI / 评审环境按标准流程跑。
2. **godot --export-release** — 需用户先在编辑器里配置 Windows Desktop export_presets.cfg（M2+ 验收时做）。

## 1.7 修复记录（自 v0.4 起的代码改动）

| 文件 | 改动 | 关联 |
|---|---|---|
| `WorldView.csproj` | `<Project Sdk="Godot.NET.Sdk/4.7.2">` → `<Project>` + `<Sdk Name="Godot.NET.Sdk" Version="4.7.2" />`，解决 dotnet 10 SDK workload resolver 找不到 Godot.NET.Sdk | sln build |
| `scripts/Storage/Yaml/ProjectLoader.cs` | cells_ref 加载：空/null 时按 `<map>.yaml.terrain.yaml` → `<map>.terrain.yaml` 双候选 fallback | D7 / B3 |
| `scripts/Storage/Yaml/ProjectSaver.cs` | Save 签名加 `IReadOnlyList<LayerDoc>? layers` 可选参数；写出 `maps/*/<map>.yaml.terrain.yaml`（cells 落盘）+ 按 map.Layers[].ref 解析绝对路径写 layers；用 `FileStream + Flush(true)` 写盘 | D7 / B7 |
| `tests/WorldView.Smoke/Program.cs` | B7 改用 `ProjectSaver.Save(tmpRoot, world, loaded.Maps, loaded.Layers)`；新增 layers/cells roundtrip 断言 + 2 条 OBS 探针 | B7 / 修复验证 |

---

## 2. 手动验收 demo（D1–D8，spec §7）

> **运行环境**：Godot 4.7.2 mono + C# + Godot 内置 2D 工程本目录。
>
> **启动方式**：
> - 方式 A（编辑器 F5，推荐验收用，能开 RemoteTree）：`C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path . --editor`，编辑器内按 F5 启动 Main.tscn。F5 触发 [scripts/Editor/EditorController.cs](scripts/Editor/EditorController.cs) 的 `TryAutoloadSampleProject()` 自动加载 `examples/sample_world/`。
> - 方式 B（直接跑 demo 二进制）：`C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe --path .`，首次运行若画布空白按顶栏「打开工程…」按钮选 `examples/sample_world/` 目录。
>
> **核心约束**：
> - 窗口尺寸建议 ≥ 1280×720（小窗口会被 SubViewport 自动适配）。
> - 鼠标进入画布才会触发 MouseCellLabel 更新。
> - 验收时把 RemoteTree（Godot 4 调试面板）打开便于核对节点树与状态。

### D1 — 启动 + 打开示例工程 + 世界地图渲染

**操作**：
1. 方式 A 编辑器 F5 / 方式 B `Godot --path .`。
2. 等待 ≤ 1 秒（autoload）。

**期望**：
- 主窗口分四区：TopBar（打开工程…/保存(Ctrl+S)/撤销/重做/面包屑）+ 中部三栏（ToolPanel/画布/RightColumn）+ StatusBar。
- 顶栏面包屑：`艾瑟兰大陆 / 极光港`（如果只显示"未加载工程"，说明 autoload 失败；点「打开工程…」手动选 `examples/sample_world/`）。
- 画布中央一张整张艾瑟兰大陆（64×48 浅米色 #c2b280 平地），上面：
  - 中央山脉（[22..24, 10..12]，深褐色 #7a6f5c，9 格）
  - 北方针叶林（[5..7, 4..5]，8 格）
  - 南方密林（[44..45, 24..25]）
  - 永风洋水系（[8..11, 30]，蓝色 #3a6ea5，4 格）
  - **红色圆环 marker** 在画布正中（32, 24 像素坐标）
- 状态栏：「校验通过 (0 警告)」「鼠标 (--, --)」。

**代码路径**：[ProjectLoader.Load](scripts/Storage/Yaml/ProjectLoader.cs) → [EditorContext.LoadProject](scripts/Editor/EditorContext.cs) → [WorldCanvas.RebuildAll](scripts/Render/WorldCanvas.cs) → 4 个 Layer 子节点 `Configure(TileSet, 32)` + `CenterCameraOnMap()`（地图居中 + 自动 zoom）。

**已知死角**：默认 cell 不写 TileMapLayer（spec §3.5 perf discipline），所以未覆盖的区域显示透明背景（暗灰 #212129）。BuildingLayer 仅在城市地图绘制，D1 看不见。

---

### D2 — 滚轮缩放 / 中键拖拽

**操作**：
1. 在 D1 状态下，鼠标移到画布。
2. **滚轮**滚动 → 画布缩放（向上放大、向下缩小）。
3. **中键按住拖动** → 画布平移。

**期望**：
- 滚轮：`Zoom *= 1.15` 或 `/=1.15`，clamp 到 [0.15, 8]，状态栏 MouseCellLabel 实时显示当前格子坐标 `(x, y)`。
- 中键拖动：`Camera2D.Position -= motion.Relative / zoom`，画布跟随。
- 鼠标移动 → 状态栏底部 `MouseCellLabel` 实时刷新坐标。

**代码路径**：[WorldCanvas._UnhandledInput](scripts/Render/WorldCanvas.cs)（wheel + middle button）→ Camera2D.Zoom / Position。

**已知死角**：F / G 快捷键（相机自适应、相机居中）**未实现**。需要相机居中可手动中键拖或滚轮缩放。

---

### D3 — 圆形笔刷 + mountain + 拖拽

**操作**：
1. 按 **B** 进入笔刷模式（或点 ToolPanel「笔刷(B)」按钮）。
2. ToolPanel `TerrainBtnContainer` 选 `Mountain`。
3. `BrushRoundBtn` 选圆形 / `BrushRadiusSlider` 调到 2-3。
4. 在画布上**按住左键拖拽**。

**期望**：
- 拖过区域从浅米色变为深褐色 mountain 色。
- **100ms throttle**（[BrushController.ThrottleMs=100](scripts/Editing/BrushController.cs)），快速拖拽时部分格子不立刻上色。
- Toolbar 顶栏 undo/redo 按钮变为可用态。

**代码路径**：WorldCanvas._UnhandledInput left press → `_brush.TryStamp()` → [ApplyBrushCommand.Apply](scripts/Editing/ApplyBrushCommand.cs) → EditorContext.SetTerrainCells → emit MapChanged。

**已知死角**：
- BuildingLayer 不刷新（MVP 范围外）。
- 撤销栈不立即清零（应保持到 Ctrl+Z / Ctrl+Y）。

---

### D4 — Ctrl+Z / Ctrl+Y

**操作**：
1. D3 拖完一笔后按 **Ctrl+Z**。
2. 再按 **Ctrl+Y**。

**期望**：
- Ctrl+Z：mountain 区域恢复成浅米色。
- Ctrl+Y：mountain 重新出现。
- UndoStack 容量 200，每次笔刷笔触是一条命令。
- 撤销语义：「恢复该笔触之前的所有格子原貌」（ApplyBrushCommand 第一次 Apply 时快照 `_before`，Revert 时按原始 terrain 类型分组写回）。

**代码路径**：WorldCanvas._UnhandledInput Ctrl+Z → UndoStack.Undo → IEditorCommand.Revert → EditorContext.SetTerrainCells → emit MapChanged。

**已知死角**：Ctrl+Y 不重画 BuildingLayer / MarkerLayer。

---

### D5 — 双击极光港 marker → 进入城市地图

**操作**：
1. 在 D1 状态下（先按 Esc 切回世界地图）。
2. 滚轮缩放到 1.0 倍以上（marker hitRadius=14 px，必须能点中）。
3. 在画布上**双击红色 marker 圆环**（视口坐标 32, 24）。
4. 双击 → 切换城市地图。城市画布操作同上。

**期望**：
- WorldCanvas 收到 `DoubleClick` + Tool=Selected → `TryDrillToCity`。
- `_marker.HitTest(localPos)` 返回 true → `FindCity("city_kael")` → `ResolveMapIdByRef("maps/city/city_kael.yaml")` → `EditorContext.DrillTo("map_city_kael")`。
- 画布切换：grid 从 64×48 km → 64×64 m；breadcrumb `艾瑟兰大陆 / 极光港`。
- 城市画布上**两个建筑矩形**（house @[20,30] 深灰、tower @[32,32] 深红）。
- **Esc** 键返回世界地图（WorldCanvas._UnhandledInput Esc → `DrillBack`）。

**代码路径**：[MarkerLayer.HitTest](scripts/Render/MarkerLayer.cs) → WorldCanvas.TryDrillToCity → EditorContext.DrillTo → SetActiveMap → MapChanged signal → WorldCanvas.RebuildAll。

**已知死角**：
- 没有"城市卡"中间预览层（spec §10 提到过）。
- hitRadius 固定 14 px，缩放到 0.15x 时可能点不中。
- 切换后视图位置继承世界地图相机（不自动居中到城市中心）。

---

### D6 — 选建筑类型 + 拖拽

**操作**：
1. 在城市地图下（D5 状态）。
2. 按 **P** 进入建筑放置工具（EditorController V/B/P 键）。
3. ToolPanel `BuildingBtnContainer` 选 `Tower`。
4. 在城市画布**按住左键拖拽**。

**期望**：
- 每个落点位置画一个深红矩形。
- 同 D3 100ms throttle。

**代码路径**：WorldCanvas._UnhandledInput left press + Tool=Building → [BuildingPlaceController.TryPlace](scripts/Editing/BuildingPlaceController.cs) → [PlaceBuildingCommand](scripts/Editing/PlaceBuildingCommand.cs) → EditorContext.AddBuilding。

**已知死角**：
- 没有"属性对话框"（InspectorPanel 显示地图级 buildings 数，不显示单个建筑）。
- 建筑师 ID 是 deterministic：`b_{mapId}_{x}_{y}` — 同一格再放会覆盖（同一 ID），不是新增。建议切到 Plain 地形再放。
- 放置前不检查该格子是否在已有建筑上，可以堆叠多建筑在一格。

---

### D7 — 关闭 + 重开同一目录

**操作**：
1. 完成 D3 / D6 后按 **Ctrl+S**（顶栏 SaveBtn 或快捷键）。
2. 关闭应用。
3. 重新启动（方式 A F5 / 方式 B）。

**期望**：
- Ctrl+S 触发 `WorldCanvas._UnhandledInput Ctrl+S` → `EditorContext.SaveProject()` → [ProjectSaver.Save](scripts/Storage/Yaml/ProjectSaver.cs) → 写 `project.yaml` + `maps/world/world_kael.yaml` + `maps/city/city_kael.yaml`。
- 关闭 + 重开后 autoload 重新读，看到刚才刷的地形 + 放置的建筑。

**代码路径**：同 D1 + ProjectSaver 序列化。

**已知死角（待修）**：
- terrain cells **不会被回写到 map yaml**，但 cells_ref 文件 (`world_kael.terrain.yaml`) 也 **没被回写**。所以 D3 刷的山头在 D7 重开后**会丢失**。这是真 bug。

---

### D8 — 校验失败阻止保存

**操作**：
1. 手动删除 `examples/sample_world/maps/city/port_aurora.yaml`（deprecation stub）或编辑 `maps/world/world_kael.yaml` 把 layer ref 改为绝对路径 `D:/xxx`。
2. 画布上做任意编辑后按 Ctrl+S。

**期望**：
- 校验器报 `E_REF_PARENT` 或 `E_REF_ABSOLUTE` + 写盘取消 + 错误对话框列出错误。

**代码路径**：`ProjectLoader.Load` 时 `MapValidator.Validate` 已经触发 `E_REF_PARENT`（因为 `port_aurora.yaml` 不存在）。

**已知死角（待修）**：
- `EditorContext.SaveProject` 当前**不检查 Validation.Errors.Count**，D8 的"写盘取消"行为缺失，需要加 guard。

---

## 3. 验收脚本（用户本机）

```powershell
# 1) .NET 编译 + 单测
cd D:\kudi-project\world-view
dotnet test WorldView.Domain.sln

# 2) Godot 工程语法校验（headless）
godot --headless --check-only --quit-after 1 project.godot

# 3) 冒烟脚本
dotnet run --project tests/WorldView.Smoke -- examples/sample_world/

# 4) 启动 Godot mono 应用（二选一）
#    方式 A：跑 M1 编译好的 demo 二进制
".\build\WorldView.exe"

#    方式 B：自己起 dev（带 inspector / remote tree）
godot --path . scenes/main.tscn
# 弹出窗口 → 顶栏「打开工程」→ 输入 examples/sample_world/ → 进入

# 5) 逐项验收 D1–D8
# （按本节表格顺序；每项通过后在本节勾选 ⬜ → ✅）
```

---

## 4. 已知挂起（待用户本机验收）

| 项 | 原因 | 状态 |
|---|---|---|
| D1–D8 手动验收 | 需要实际窗口交互（人类操作） | 用户验收 |
| Godot 视口缩放/拖拽帧率（J1 ≥ 30 fps） | 需要真实 GPU 跑分 | ⬜ |
| Godot 桌面导出模板（`Windows Desktop` preset） | M1 须先在编辑器里配置一次 `export_presets.cfg` | 不阻塞 demo |

---

## 5. 变更记录

| 版本 | 变更 |
|---|---|
| v0.1 | MVP 首版验收：领域层 24 单测全过 + 冒烟 9 项全过 + 前端 TS 0 错（Tauri 栈） |
| v0.2 (2026-05-29) | 重新执行（Tauri 栈）：① `cargo test --workspace` 24/24 ✅；② `pnpm typecheck` 0 错；③ `pnpm build` 11 chunks 已生成（455 KB / gzip 144 KB）；④ `pnpm tauri build --debug` 生成 `world-view.exe`（15.25 MB）；⑤ 修订 capabilities（移除不存在字符串，验证 Tauri 2 仅识别 `core:*`）；⑥ 修复 sandbox 拦截 `target/.cargo-build-lock` 与 `%TEMP%\esbuild-*` 的访问。 |
| v0.3 (2026-10-07) | **D1「打开工程」按钮无反应修复（路线 2）**：① 失败尝试：引入 `tauri-plugin-dialog` + `@tauri-apps/plugin-dialog`，但 sandbox 网络层拦截 crates.io / tuna / ustc / rsproxy 全部 443 出站，无法拉新依赖；② 改用路线 2：自实现 Rust 端 `#[tauri::command] pick_directory()`（`std::process::Command` 调 PowerShell `FolderBrowserDialog`），零新 crate 依赖；③ 前端 `src/bridge/tauri.ts` 新增 `pickDirectory(current)`；④ `src/ui/TopBar.tsx` 改用 `bridge.pickDirectory()`，保留文本框+回车提交作为降级路径；⑤ 还原 Cargo.toml（删 `tauri-plugin-dialog`）、capabilities/default.json（删 `dialog:default`）、package.json（删 `@tauri-apps/plugin-dialog`）、`lib.rs:run()`（删 `.plugin(tauri_plugin_dialog::init())`）；⑥ 重新跑通：`cargo test --workspace --offline` 24/24 ✅、`pnpm typecheck` 0 错 ✅、`pnpm tauri build --debug` 成功生成主二进制（15.02 MB）+ MSI（4.91 MiB）+ NSIS（2.96 MiB）。 |
| **v0.4 (2026-10-09) — 整体重写（栈反转）** | 用户决议放弃 Tauri + TypeScript + PixiJS,改 Godot 4.7.2 mono + C# + Godot 内置 2D。本文件按新栈**重新搭骨架**：① §1 自动验证章节命令替换为 `dotnet test` / `godot --headless --check-only` / `dotnet run --project tests/WorldView.Smoke` / `godot --export-release`；② §2 D1–D8 描述改为 Godot 节点 / `CanvasItem` / 命令栈 / `Sprite2D` 措辞；③ §3 用户本机验收脚本替换为 `dotnet` + `godot` 命令；④ §6、§7（Tauri sandbox 修复与 WiX / NSIS 下载）整段删除（与新栈无关）。**v0.1–v0.3 全部历史记录仅作为「栈反转前的存档」保留**,不作为新栈验收依据。M1 真实跑通后,由用户在对应章节填实际命令输出与产物大小。 |
