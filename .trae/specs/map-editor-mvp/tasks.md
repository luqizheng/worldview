# Map Editor MVP — 任务拆解

> 栈:Godot 4.7.2 mono + C# (.NET 8) + YamlDotNet 16.3.0。
> 每个任务对应可编译 / 可运行的最小单位(C# 文件 / Godot 场景文件 / 工程配置 / 示例资产)。
> 完成定义(DoD):文件存在于仓库 + `dotnet build WorldView.sln` 成功 + 在 Godot 4.7.2 中按 F5 不报错。

---

## 阶段 A — 工程骨架(M1 基线)

> 目标:`dotnet build WorldView.sln` 在 Godot 4.7.2 mono 中可成功;`project.godot` 配置完整。

### T1 — 创建 `project.godot` + 解决方案与项目文件

- 创建 `D:\kudi-project\world-view\project.godot`(Godot 4.7.2 mono,`config_version=5`,`[application] config/name="World View"`、`run/main_scene="res://scenes/Main.tscn"`,`[dotnet] project/assembly_name="WorldView"`,`[rendering] renderer/rendering_method="forward_plus"`,`view_mode=2d`)。
- 创建 `D:\kudi-project\world-view\WorldView.sln`(Format 12.00 + VS 17.0,Project({FAE04EC0...}) = "WorldView" / "WorldView.csproj",Debug / ExportDebug / ExportRelease 三种配置)。
- 创建 `D:\kudi-project\world-view\WorldView.csproj`(`<Project Sdk="Godot.NET.Sdk/4.7.0">`,`TargetFramework=net8.0`,`RootNamespace=WorldView`,`LangVersion=latest`,`Nullable=enable`,`ImplicitUsings=enable`,`PackageReference YamlDotNet 16.3.0`)。
- 在 `.gitignore` 追加 Godot 4 + Mono 项(`.godot/` `.import/` `addons/` `*.翻译` `*.gd` `*.godot.uid` `*.godot.uid.json` + .NET 项)。
- 创建 `scripts/.gitkeep` + `scenes/.gitkeep` 占位。
- **DoD**:`dotnet build WorldView.sln` 成功(空项目可编译);Godot 编辑器能打开 `project.godot`。

### T2 — Godot skill 安装(本地,无生产代码)

从 `gamedev-skills/awesome-gamedev-agent-skills`(1348⭐,Apache-2.0)挑出并安装 7 个 Godot 核心 skill 到 `C:\Users\leo\.agents\skills\`:

- `godot-csharp`(6670 字节)、`godot-nodes-scenes`(5511)、`godot-tilemap`(5755)、`godot-ui-control`(5782)、`godot-resources`(4993)、`godot-signals-groups`(5053)、`godot-2d-movement`(6011)。
- 跳过 9 个:3d-essentials / animation / audio / export / gdscript / gdscript-headless-testing / multiplayer / physics / shaders。
- **DoD**:每个 skill 目录下存在 `SKILL.md`。

---

## 阶段 B — 示例工程

> 目标:`examples/sample_world/` 5 秒内可打开(D1 验收)。

### T3 — 示例工程根文件

- `examples/sample_world/project.yaml`(工程根)。
- `examples/sample_world/README.md`(说明示例用途 + 文件清单)。
- `examples/sample_world/maps/world/world_kael.yaml`(1 张世界地图,`grid 64×48`,含 1 个城市标记)。
- `examples/sample_world/maps/world/world_kael.terrain.yaml`(world 地形,sparse 编码,演示用 21 cells)。
- `examples/sample_world/maps/city/city_kael.yaml`(1 张城市地图,`grid 64×64`,含 2 个建筑占位)。
- `examples/sample_world/maps/city/city_kael.terrain.yaml`(city 地形,sparse,全平原)。
- `examples/sample_world/layers/terrain.yaml` + `road.yaml` + `building.yaml` + `marker.yaml`(4 类图层,各自 `id` / `type` / `z_order`)。
- **DoD**:D8 校验成功(所有字段合法;spec.md §2.2 字段对照表 100% 一致);手动启动 Godot 5 秒内可加载;checklist.md B1-B7 全 ✅。

---

## 阶段 C — 领域与存储层

> 目标:`Domain/` 与 `Storage/` 目录内文件全部可编译、可单元测试,**不引用 `Godot.*` 命名空间**。

### T4 — 领域模型

- `scripts/Domain/Terrain.cs`(enum 11 种地形 + 默认值 `DefaultTerrain = { type: plain, move_cost: 1, buildable: true }`)。
- `scripts/Domain/Building.cs`(record `Building(id, type, at, map_id)`)。
- `scripts/Domain/LayerDoc.cs`(`LayerDoc` record + `LayerKind` enum:terrain / road / building / marker)。
- `scripts/Domain/MapDoc.cs`(Map 文档 record,含 `encoding` / `grid` / `cell_distance` / `terrain_data` / `buildings` / `cities`)。
- `scripts/Domain/World.cs`(`World` record:`Project` + `Maps` + `Layers` + `Load(ref)` / `Save(ref)` 占位接口)。
- **DoD**:`dotnet build` 成功;`tests/Domain/*.cs` 至少 1 个 record equality 测试通过。

### T5 — 校验器

- `scripts/Domain/Validation/ValidationErrorCode.cs`(enum:`RequiredMissing` / `EnumOutOfRange` / `RefNotFound` / `IdDuplicate` / `TypeMismatch`)。
- `scripts/Domain/Validation/ValidationError.cs`(record `ValidationError(ref, fieldPath, code, message)`)。
- `scripts/Domain/Validation/ProjectValidator.cs`(`IReadOnlyList<ValidationError> Validate(World)` 静态方法,逐文件扫描,第一处错误即返回)。
- **DoD**:`tests/Validation/ProjectValidatorTests.cs` 覆盖 5 种错误码;D8 demo 通过。

### T6 — YAML 序列化

- `scripts/Storage/YamlSchemas.cs`(内嵌 `YamlDotNet.Serialization` 配置 + 类 ↔ YAML 映射;字段名走 snake_case)。
- `scripts/Storage/ProjectLoader.cs`(`World Load(string projectRoot)` 从 `project.yaml` 加载)。
- `scripts/Storage/ProjectSaver.cs`(`void Save(World world, string projectRoot)` 回写到原路径)。
- **DoD**:D7 demo 通过(逐字段比对);字段名与 `docs/存储与目录规范_v0.1.md` 完全一致。

---

## 阶段 D — 渲染与 UI 层

> 目标:Godot 节点树与场景文件就位,主场景可运行。

### T7 — 主场景与四区布局

- `scenes/Main.tscn`(`Control` 根,锚点布局:TopBar 占顶 32 px;ToolPanel 左侧 64 px;InspectorPanel 右侧 240 px;StatusBar 占底 24 px;中间余下为画布)。
- `scenes/TopBar.tscn`(顶栏 `HBoxContainer`:`FileMenu` 按钮 + `Breadcrumbs` 节点 + 「保存」按钮)。
- `scenes/ToolPanel.tscn`(工具栏:`VBox` 垂直排列的 `ToolButton`(selected / brush / building))。
- `scenes/InspectorPanel.tscn`(检视器:只读 `Label` 树,显示当前地图元数据)。
- `scenes/StatusBar.tscn`(状态栏:`HBoxContainer` 三段:地图名 + 鼠标坐标 + 选中对象)。
- `scenes/LayerPanel.tscn`(图层面板:左上侧 `VBox`,内含动态生成的 `CheckBox`)。
- **DoD**:Godot 编辑器 F5 运行,主场景显示四区;Ctrl+S 无副作用。

### T8 — 画布与 TileMapLayer 节点

- `scenes/WorldCanvas.tscn`(`Node2D` 根:`Camera2D` + 多个 `TileMapLayer`(terrain / road / building)+ `GridOverlay` 子节点)。
- **DoD**:场景可在编辑器中实例化,每个 TileMapLayer 引用同一 TileSet 资源(`Render/terrain.tres`)。

### T9 — UI 脚本

- `scripts/Ui/Main.cs`(`Main : Control`,`[Export] PackedScene WorldCanvasScene;` + `[Export] PackedScene AreaLayerPanelScene;`,等)。
- `scripts/Ui/TopBar.cs`(「打开示例工程」 `ButtonPressed → EditorContext.Instance.LoadProject(relsPath)`)。
- `scripts/Ui/LayerPanel.cs`(每图层一个 `CheckBox`,`Toggled → EditorContext.SetLayerVisible(layerId, pressed)`)。
- `scripts/Ui/ToolPanel.cs`(工具切换按钮:`ToolState`,`Pressed → EditorContext.SetTool(tool)`)。
- `scripts/Ui/InspectorPanel.cs`(订阅 `EditorContext.MapChanged` 信号,刷新 `Label`)。
- `scripts/Ui/StatusBar.cs`(订阅 `EditorContext.MouseMoved` + `EditorContext.SelectionChanged`)。
- `scripts/Ui/ErrorDialog.cs`(校验错误弹窗:`AcceptDialog` 子类 + `ShowValidationError(ValidationError)`)。
- `scripts/Ui/BuildingPlaceDialog.cs`(建筑放置确认弹窗:对 D6 的反馈,不阻塞)。
- **DoD**:每个 `*.cs` 编译通过,场景与脚本绑定关系正确(`Editor → Script = ...` 字段填对)。

---

## 阶段 E — 编辑能力(命令模式 / 笔刷 / 撤销)

> 目标:`Editing/` 目录全部就位,笔刷 + 撤销 / 重做 + 命令模式可用。

### T10 — 命令模式 + 撤销栈

- `scripts/Editing/IEditorCommand.cs`(接口:`Apply()` / `Revert()`)。
- `scripts/Editing/UndoStack.cs`(FIFO 上限 200:`Push(cmd)` 自动调用 `cmd.Apply()`,`Undo()` 回退栈顶,`Redo()` 重做)。
- **DoD**:xUnit 测试 `Editing.Tests/UndoStackTests.cs` 覆盖:
  - 连续 200 次 push 不溢出;
  - 第 201 次后,最旧命令被丢弃;
  - 撤销 + 重做循环 100 次字段无残留(R-UNDO-03)。

### T11 — 命令实现

- `scripts/Editing/ApplyBrushCommand.cs`(`ctor: EditorContext, MapId, BrushType, CenterCell, Radius;Apply() → SetCells;Revert() → SetCells(defaultTerrain)`)。
- `scripts/Editing/CreateCityMapCommand.cs`(`ctor: EditorContext, CityMapRef;Apply() → push to breadcrumbs;Revert() → pop`)。
- `scripts/Editing/PlaceBuildingCommand.cs`(`ctor: EditorContext, MapId, Cell, BuildingType;Build() → MapDoc.Buildings.Add(...);Revert() → MapDoc.Buildings.Remove(added)`)。
- **DoD**:每个命令类单独 xUnit 测试通过。

### T12 — 笔刷控制器(节流)

- `scripts/Editing/BrushController.cs`(状态:`LastStamp`(ulong);`bool TryStamp(Vector2I cell)` 若 `Time.GetTicksMsec() - LastStamp ≥ 100` 则通过,否则拒绝)。
- 集成到 `scripts/Render/WorldCanvas.cs`(`_Input` → BrushController.TryStamp → ApplyBrushCommand → UndoStack.Push)。
- **DoD**:D2 demo 通过(笔刷落点按 100 ms 节流);一帧内 dirty_rect 内格子全部更新。

---

## 阶段 F — 视图与坐标

> 目标:`Render/` 目录全部就位,视图与坐标转换集中。

### T13 — 坐标转换

- `scripts/Render/CoordTransform.cs`(静态类:`Vector2 WorldToPixel(Vector2 world) / Vector2 PixelToWorld(Vector2 pixel) / Vector2I WorldToCell(Vector2 world, float cellDistance) / Vector2 CellToWorld(Vector2I cell, float cellDistance) / Rect2I WorldRectToCellRect(Rect2 world)`)。
- **DoD**:xUnit 测试所有方法互逆性(`WorldToCell(CellToWorld(c)) == c`)。

### T14 — TileMapLayer 子类

- `scripts/Render/TerrainLayer.cs`(`: TileMapLayer`;`SetTerrainCell(Vector2I cell, TerrainType type)` 调 `SetCell(cell, atlasSourceId, atlasCoord)`;`GetDrawnCellCount() => TileMap.GetUsedCells().Count`)。
- `scripts/Render/RoadLayer.cs`(`: TileMapLayer`,路网渲染,MVP 只画 `Line2D` 中点连线)。
- `scripts/Render/BuildingLayer.cs`(`: Node2D`,建筑用 `Polygon2D` 占位矩形)。
- **DoD**:D2 demo 通过(笔刷刷过地形,画面更新)。

### T15 — 网格叠加

- `scripts/Render/GridOverlay.cs`(`: Node2D`,持 `Line2D`,按 `CellSize` 画边界线)。
- **DoD**:网格线在画布上可见,缩放时跟随更新。

### T16 — Camera2D 与缩放

- `scripts/Render/WorldCanvas.cs`(`: Node2D`,持 `Camera2D`;`_UnhandledInput` 处理鼠标中键拖拽 + 滚轮缩放;`_PhysicsProcess` 处理键盘 WASD 平移)。
- **DoD**:D2 / D5 demo 通过(平移 + 缩放可见)。

---

## 阶段 G — 城市下钻与导航

> 目标:城市下钻 + 面包屑可工作。

### T17 — 城市下钻

- `scripts/Editor/EditorController.cs`(`DrillToCity(string cityMapId) => EditorContext.ActiveMap = CityMap;` `DrillBack() => EditorContext.ActiveMap = PreviousMap;`)。
- `scripts/Ui/TopBar.cs` 面包屑子节点:`Breadcrumbs`(`: HBoxContainer`),订阅 `EditorContext.MapChanged` 信号,动态生成 `Button`。
- `scripts/Render/MarkerLayer.cs`(`: Node2D`,城市标记用 `Sprite2D` 占位圆点;`_Input` 监听双击 → EditorController.DrillToCity)。
- **DoD**:D5 demo 通过(双击城市 → 进入城市;Esc → 返回)。

### T18 — 工具切换 + 建筑落点约束

- `scripts/Editing/BuildingPlaceController.cs`(状态:当前选中的建筑类型;`TryPlace(Vector2I cell) => if Terrain.Buildable then PlaceBuildingCommand else StatusBar.ShowHint(...)`)。
- 集成到 `scripts/Render/WorldCanvas.cs`(`_Input` 处理建筑工具下的落点)。
- **DoD**:D6 demo 通过(放置成功 + 不可放置时状态栏提示)。

---

## 阶段 H — 保存与校验流程

> 目标:D7 / D8 demo 通过。

### T19 — 保存流程

- `scripts/Ui/TopBar.cs` 「保存」按钮:`Pressed → EditorContext.SaveProject()`。
- `scripts/Editor/EditorContext.cs`(`SaveProject() => ProjectSaver.Save(World, ProjectRoot)`)。
- **DoD**:D7 demo 通过(逐字段比对 `project.yaml` / `maps/*.yaml`)。

### T20 — 启动校验

- `scripts/Ui/Main.cs` `_Ready` 钩子:`if ProjectValidator.Validate(World).Any() then ErrorDialog.Show(); else WorldCanvas.Instantiate();`。
- **DoD**:D8 demo 通过(校验失败 → 弹错误对话框 → 禁止渲染)。

---

## 阶段 I — 验收

### T21 — D1–D8 录屏

- 在 Godot 4.7.2 mono 中按 F5,按 `docs/acceptance/map_editor_mvp_check.md` 中的步骤依次完成 D1–D8。
- 每段录屏保存到 `docs/acceptance/demos/`(可选)。
- **DoD**:8 段录屏齐全,每段覆盖各自 AC。

### T22 — 性能断言(J2 量化)

- 在 1024×1024 网格 + 128×128 视口下,调用 `WorldCanvas.GetDrawnCellCount()` 验证 ≤ 4915(R-UNDO-01 的同时性能断言)。
- **DoD**:xUnit 集成测试 `Render.Tests/ViewportCullingTests.cs` 通过。

---

## 阶段 J — 收尾

### T23 — 更新文档

- `AGENTS.md` §6.1 仓库布局段落更新为 Godot 栈。
- `docs/acceptance/map_editor_mvp_check.md` 同步更新为 Godot 操作步骤。
- `README.md` 添加「运行方式:`dotnet build` + Godot F5」段落。
- **DoD**:三份文档 `git diff` 干净,术语一致。

### T24 — 提交 + 标签

- `git add -A` → `git commit -m "M1 完成:Godot 4.7.2 mono + C# MVP 验收"`。
- `git tag -a mvp-v0.1 -m "M1 MVP 验收完成"`。
- **DoD**:`git log --oneline -n 5` 中能看到 M1 commit;tag `mvp-v0.1` 存在。

---

## 范围纪律(MVP 不做)

- ❌ `chunked` 编码 / 室内下钻 / 时间轴 / 运输 / 贴合 / 视觉增强 / 插件
- ❌ `Domain/` 下 `using Godot;`
- ❌ `ref` 中 `..` 或绝对路径
- ❌ `id` 与 `ref` 混用
- ❌ 跳过命令模式直接修改文档