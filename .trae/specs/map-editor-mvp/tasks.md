# Tasks — 地图编辑器 MVP

> 按 spec.md §7 验收 demo 自下而上拆任务；每条任务小、可验证、可串行或并行执行。
> 任务编号稳定，便于 review 与回溯。

---

## 阶段 A · 工程骨架（M1 基线）

- [ ] **Task 1：创建 Tauri 工程骨架**
  - [ ] SubTask 1.1：`pnpm create tauri-app .`（pnpm + TypeScript + React，在仓库根初始化 Tauri 项目，生成 `src-tauri/` 与 `src/`）
  - [ ] SubTask 1.2：**Workspace 结构（src-tauri 不进根 workspace）**：
    - 仓库根新建 `Cargo.toml`，`[workspace] members = ["crates/world-view-core", "crates/world-view-io"]`（**不含** `src-tauri`，避免 Tauri 工具链与 workspace resolver 冲突）
    - `src-tauri/Cargo.toml` **保持独立 crate**（`[package] name = "world-view"`），不写 `[workspace]`
    - `src-tauri/Cargo.toml` 加 `[dependencies] world-view-core = { path = "../crates/world-view-core" }` 与 `world-view-io = { path = "../crates/world-view-io" }`，按需启用 `[patch.crates-io]` 或 `path = "..."`
    - `crates/world-view-core/Cargo.toml` 与 `crates/world-view-io/Cargo.toml` **不引入** `tauri` 依赖（仅 `serde_yaml` / `serde` / `thiserror`）
    - 验证：`cd crates/world-view-core && cargo test` 与 `cd src-tauri && cargo check` 互不耦合
  - [ ] SubTask 1.3：确认 `pnpm tauri dev` 可启动空白窗口

- [ ] **Task 2：领域模型核心类型（world-view-core）**
  - [ ] SubTask 2.1：在 `world-view-core/src/model/` 下定义 `Project` / `Map` / `Layer` / `TerrainCell` / `CityRef` / `BuildingRef`（字段对齐数据模型规格 §1–§2，仅 MVP 用到的子集）
  - [ ] SubTask 2.2：定义 `TerrainEncoding` 枚举（`Grid`/`Sparse`）与对应序列化器（`grid.rs` / `sparse.rs`）
  - [ ] SubTask 2.3：定义 `ValidationError` 类型与最小校验函数 `validate(&Project) -> Result<(), Vec<ValidationError>>`，每条规则对应一个独立单测：
    - `test_validate_project_version` — `project_version = "0.1"`，版本不匹配返回 `InvalidProjectVersion`
    - `test_validate_refs_exist` — 任一 `map_ref` / `cells_ref` 指向不存在的路径返回 `RefUnreachable(path)`；含 `city_refs[i].map_ref` 为空合法、为非空不可达则报错两条独立用例
    - `test_validate_id_unique` — 跨 `Map.id` / `CityRef.id` / `BuildingRef.id` / `Character.id` 唯一；冲突返回 `DuplicateId(id, locations)`
    - `test_validate_terrain_grid_size` — `encoding: grid` 时 `cells.len() == grid.width * grid.height`；不匹配返回 `TerrainGridSizeMismatch`
    - `test_validate_city_refs_reachable` — `city_refs[i].map_ref` 非空时文件可达；可达性放在 `open_project` 加载时校验，`save_project` 阶段复用（避免重复 I/O）

- [ ] **Task 3：YAML 读写（world-view-core）**
  - [ ] SubTask 3.1：引入 `serde_yaml` / `serde` 派生，实现 `Project::from_yaml_str`、`Map::from_yaml_str` / `to_yaml`
  - [ ] SubTask 3.2：实现 `terrain` 文件的 `grid` ↔ `sparse` 互转（仅 MVP 范围：`grid` 必为完整 W×H 数组；`sparse` 为带默认值的 cells 列表）
  - [ ] SubTask 3.3：单元测试：`sample_world` 的 `project.yaml` / `main.yaml` / `main.terrain.yaml` 解析往返一致

- [ ] **Task 4：Tauri commands（world-view-io）**
  - [ ] SubTask 4.1：实现 `#[tauri::command] open_project(dir: PathBuf) -> Result<Project, IoError>`：
    - 解析 `project.yaml`
    - 按 `map_index` 顺序加载每张地图
    - **全失败语义**：任一地图加载失败（文件不存在 / YAML 解析错 / `cells_ref` 不可达），整单返回 `Err(IoError::MapLoadFailed { path, cause })`，**不返回部分 `Project`**
    - 失败时已读入的 `Map` 实例全部丢弃（不写入内存）
    - `IoError` 序列化时含文件路径 + 底层 `cause` 字符串，前端原样展示
  - [ ] SubTask 4.2：实现 `#[tauri::command] save_project(dir: PathBuf, project: Project) -> Result<(), Vec<ValidationError>>`（先校验再写盘）
  - [ ] SubTask 4.3：实现 `#[tauri::command] create_city_map(dir: PathBuf, parent_world: Map, city: CityRefEntry) -> Result<Map, IoError>`：
    - 生成 `maps/city/<city.id>.yaml`（最小可加载骨架，字段定义以数据模型规格 §2 为准）
    - 默认值：`grid = { width: 64, height: 64, cell_distance: 2, distance_unit: "m", base_unit: "m", height_unit: "m", show: true, snap: true }`
    - 默认值：`time = { default_granularity: "hour", min_granularity: "hour", inherit_calendar: true, allow_fuzzy: true }`
    - 默认值：`presentation = { default_mode: "edit", view_mode: "2d", theme: "parchment", show_scale_bar: true, show_coordinates: true }`
    - 默认值：`layers = [terrain, road]`（按 §2.6 字段）
    - 默认值：`terrain_data = { encoding: "sparse", default: { type: "plain", move_cost: 1, buildable: true }, cells: [] }` 并写一份空的 `maps/city/<id>.terrain.yaml`（仅有 `encoding: sparse` + `default`，无 `cells`）
    - 同步：`parent_world.city_refs[i].map_ref = "maps/city/<city.id>.yaml"`；`project.map_index` 追加新路径；返回值：新建的 `Map` 对象
  - [ ] SubTask 4.4：**注册 commands + capability**：
    - `src-tauri/src/lib.rs`：`tauri::generate_handler![open_project, save_project, create_city_map]`
    - `src-tauri/capabilities/default.json`：在 `permissions` 加 `core:default` 与自定义 capability（`world-view:allow-open-project` / `world-view:allow-save-project` / `world-view:allow-create-city-map`）；`fs.scope` 声明对所选工程目录子树的 `readFile` / `writeFile` / `readDir` / `mkdir` 权限
    - `src-tauri/tauri.conf.json`：`app.security.csp` `connect-src` 含 `ipc://localhost` 与 `http://ipc.localhost`

---

## 阶段 B · 示例工程

- [ ] **Task 5：创建 `examples/sample_world/`**
  - [ ] SubTask 5.1：编写 `project.yaml`（按 spec §2.6 风格，`map_index` 含 1 张世界 + 1 张城市；`character_index` 含 `characters/lin_yu.yaml` 作数据契约样例）
  - [ ] SubTask 5.2：编写 `maps/world/main.yaml`（64×48，`city_refs` 含 `极光港`）
  - [ ] SubTask 5.3：编写 `maps/world/main.terrain.yaml`（`encoding: grid`；**具体分布**）：
    - `mountain`：矩形 `(x:20..30, y:10..20)`（共 110 格连片），`move_cost=4, buildable=false`
    - `river`：横向折线 `(x:10..54, y=40)`（共 45 格直线，与若干森林交错），`move_cost=-1, buildable=false, resources=[freshwater]`
    - `forest`：散点 `(x:5..15, y:30..38)` + `(x:45..55, y:5..12)`（共 ≥ 50 格），`move_cost=2, buildable=true, resources=[timber]`
    - 其余 = `plain`，`move_cost=1, buildable=true`
    - 默认 `default = { type: plain, move_cost: 1, buildable: true }`（与 sparse 一致）
  - [ ] SubTask 5.4：编写 `maps/city/port_aurora.yaml`（64×64，`building_refs` 空；`layers` 配 `terrain` + `road`）
  - [ ] SubTask 5.5：编写 `maps/city/port_aurora.terrain.yaml`（全平原）
  - [ ] SubTask 5.6：编写 `characters/_templates.yaml`（1 条 HP 模板）— **数据契约样例，非 UI 数据**
  - [ ] SubTask 5.7：编写 `characters/lin_yu.yaml`（`residence.map_ref: maps/city/port_aurora.yaml`）— **数据契约样例，非 UI 数据**
  - [ ] SubTask 5.8：编写 `examples/sample_world/README.md`，首段显式声明：「本示例工程的人物 / 事件文件为**数据契约样例**，MVP 不加载到 UI；人物编辑 / 关系网 UI 在 M5 落地」

---

## 阶段 C · 前端骨架与状态

- [ ] **Task 6：前端依赖与目录**
  - [ ] SubTask 6.1：**钉版本**（`package.json` + `pnpm-lock.yaml`）：
    - `pixi.js@^8.6.0`（2D WebGL 渲染，8.x 是当前稳定线）
    - `zustand@^5.0.0`（轻量 store）
    - `react@^18.3.0` / `react-dom@^18.3.0`（与 Tauri 模板默认一致）
    - `@tauri-apps/api@^2.0.0`（Tauri v2 IPC）
    - TypeScript `@^5.4.0` + `@tauri-apps/cli@^2.0.0`
    - **禁止**使用 `latest` / 浮动版本；`package.json` 的依赖必须带 `^` 锁主版本号
  - [ ] SubTask 6.2：建立 `src/app/`、`src/render/`、`src/editing/`、`src/ui/`、`src/state/`、`src/bridge/` 目录

- [ ] **Task 7：领域 store（Zustand）**
  - [ ] SubTask 7.1：实现 `useDomainStore`，含 `project` / `currentMapId` / `currentMapPath` / `dirtyMapIds`；只持有从 IPC 拿到的纯数据对象
  - [ ] SubTask 7.2：实现 action：`openProject(dir)` / `saveProject()` / `createCityMap(parentRef, path)` / `switchMap(mapId)`

- [ ] **Task 8：UI store（Zustand）**
  - [ ] SubTask 8.1：实现 `useUiStore`，含 `activeTool`（`pan`/`zoom`/`brush`/`placeBuilding`）、`brushParams`、`viewport`（`x`/`y`/`scale`）、`selectedObjectId`、`showGrid`、`showCoordinates`、`layerPanelOverrides`
  - [ ] SubTask 8.2：实现 action：`setTool(tool)` / `setBrushParams` / `setViewport` / `toggleGrid` / `toggleCoordinates` / `setLayerVisible(layerId, visible)` / `setLayerOpacity(layerId, opacity)`

- [ ] **Task 9：IPC bridge**
  - [ ] SubTask 9.1：在 `src/bridge/tauri.ts` 封装 `invoke('open_project')` / `invoke('save_project')` / `invoke('create_city_map')`，类型与 `world-view-io` 对齐

---

## 阶段 D · 主界面与视图

- [ ] **Task 10：主界面布局**
  - [ ] SubTask 10.1：实现 `src/app/App.tsx`：顶栏 + 左工具栏 + 中央画布 + 右面板 + 状态栏（按界面规格 §1 ASCII 布局）
  - [ ] SubTask 10.2：实现 `src/ui/TopBar.tsx`（项目名 / 保存按钮 / 模式切换 MVP 占位）
  - [ ] SubTask 10.3：实现 `src/ui/ToolPanel.tsx`（pan / 圆 / 建筑放置 / 测量 — MVP 占位）
  - [ ] SubTask 10.4：实现 `src/ui/LayerPanel.tsx`（列出当前地图 `layers[]` + 可见性 / 锁定 / 透明度 / z_order 控件 → 调用 `useUiStore` + 触发 `saveProject` 提示）
  - [ ] SubTask 10.5：实现 `src/ui/InspectorPanel.tsx`（显示选中对象详情，MVP 仅显示「当前地图基础信息」+ 选中单元的 `type/move_cost/buildable`）
  - [ ] SubTask 10.6：实现 `src/ui/StatusBar.tsx`（面包屑 + 当前地图名 + 比例尺 + 坐标）
  - [ ] SubTask 10.7：**保存错误对话框**（`src/ui/ErrorDialog.tsx`）：
    - 全局挂载在 `App.tsx` 顶层；订阅一个独立的 `useErrorStore`（zustand，承载 `{ dialog: { title, errors[] } | null }`）
    - 错误列表渲染：`{kind, path?, message}` 三列；点击单项可复制纯文本到剪贴板
    - `useDomainStore.saveProject` 在返回 `Err(Vec<ValidationError>)` 时调用 `useErrorStore.getState().show({ title: '保存校验未通过', errors })`，**不抛异常**
    - 对话框关闭按钮 + Esc 键均可关闭
    - 该组件**同时被** T16.4 / T4.x 错误路径复用（统一 UI 入口）

- [ ] **Task 11：PixiJS 画布与视口**
  - [ ] SubTask 11.1：实现 `src/render/MapCanvas.tsx`：创建 `Application`（`{width, height, backgroundColor, antialias: true}`），挂到中央容器
    - **React StrictMode 兼容**：`useEffect` cleanup 必须调用 `app.destroy(true, { children: true, texture: true })`，避免双初始化时 WebGL 上下文泄漏
    - 用 `useRef` 持有 `Application` 实例；初始化用空依赖 `[]`，销毁 cleanup 在依赖变化 / 卸载时触发
    - 主容器 div 必须有固定尺寸（CSS `width: 100%; height: 100%` + 父级 flex 撑高），否则 PixiJS 自动 resize 失效
  - [ ] SubTask 11.2：实现 `src/render/Viewport.ts`：维护 `screen→grid` 与 `grid→screen` 互转；`pan(dx,dy)` / `zoom(factor, focusScreen)` / `fitTo(map)`；订阅 `useUiStore.viewport`
  - [ ] SubTask 11.3：实现 `src/render/GridLayer.ts`：按 `map.grid` 绘制网格线（订阅 `showGrid`）
  - [ ] SubTask 11.4：实现 `src/render/TerrainLayer.ts`：把 `terrain.cells` 渲染为色块（按 §3.1 枚举映射 MVP 默认色板，所有 11 种地形均需覆盖）。MVP 色板：

    | terrain_type | hex color | 中文名 |
    |---|---|---|
    | `plain` | `#9ab87a` | 平原 |
    | `mountain` | `#7a6f64` | 山 |
    | `forest` | `#3f6b3a` | 森林 |
    | `hill` | `#a89866` | 丘陵 |
    | `water` | `#3a6a9a` | 水域 |
    | `desert` | `#d8c890` | 沙漠 |
    | `swamp` | `#5e6e44` | 沼泽 |
    | `tundra` | `#cfd9d4` | 冻原 |
    | `basin` | `#8a8a8a` | 盆地 |
    | `coast` | `#9bb4d6` | 海岸 |
    | `river` | `#3a6a9a` | 河流 |

    单元默认色 = `{type: plain, move_cost: 1, buildable: true}`（与 sparse 编码 `default` 一致）。`hill` / `desert` / `swamp` / `tundra` / `basin` / `coast` 在 MVP 阶段**只保证能显示**，不保证有编辑入口。

    **脏区重绘策略**（统一约定）：
      - 脏区 = **矩形包围盒**（`{colMin, colMax, rowMin, rowMax}`，包含笔刷所有落点的最小外接矩形 + 1 格边带冗余）
      - **拖拽期间**不重绘（100ms 节流期间只更新内存与脏区标记）
      - **mouseup** 后用一条 `dirty_rect` 触发 TerrainLayer 局部刷新（仅清除 + 重绘该矩形内的单元）
      - 视口变化（pan/zoom）触发整层重绘（视为整屏 dirty）
      - 单格原地刷新的实现细节由 TerrainLayer 自行决定（销毁重建或单 sprite 改 fill 都可），但必须满足「1024×1024 + 128×128 视口 → `getDrawnCellCount() ≤ 64×64×1.2`」（见 J2）
  - [ ] SubTask 11.5：实现 `src/render/MarkerLayer.ts`：渲染 `city_refs[]` 标记为圆点 + 文字；点击触发 `selectMarker`
  - [ ] SubTask 11.6：实现 `src/render/BuildingLayer.ts`：渲染 `building_refs[]` 为带边框矩形 + 文字
  - [ ] SubTask 11.7：实现 `src/render/CoordinateOverlay.ts`：在状态栏下方绘制鼠标所在网格坐标 + 距离单位（订阅 `useUiStore.showCoordinates`）

- [ ] **Task 12：键盘 / 鼠标交互**
  - [ ] SubTask 12.1：左键按下+拖拽：根据 `activeTool` 分发（`pan` / `brush` / `placeBuilding`）
  - [ ] SubTask 12.2：中键 / 右键拖拽 → `pan`；滚轮 → `zoom(focusScreen)`
  - [ ] SubTask 12.3：快捷键（画布聚焦时生效）：
    - `F` 全图适配 / `G` 切网格 / `C` 切坐标 / `Esc` 回到 pan
    - `Ctrl+S` 保存 / `Ctrl+Z` 撤销 / `Ctrl+Y` 重做
    - 非画布聚焦时**不响应**（不破坏浏览器默认行为）
  - [ ] SubTask 12.4：**Esc 取消当前工具**：
    - 当前工具 ≠ `pan` 时，Esc 复位到 `pan` 工具
    - 清空**临时拖拽状态**（笔刷拖拽未提交的、放置建筑 dragRect 还没确认的、选区笔刷候选框等）
    - **不撤销**已提交的命令（撤销归 `Ctrl+Z`）
    - 关闭所有模态对话框（CityCard / 建筑属性框）

---

## 阶段 E · 编辑能力

- [ ] **Task 13：命令模式与撤销栈**
  - [ ] SubTask 13.1：实现 `src/editing/commands.ts`：`Command` 接口（`apply()` / `revert()`） + 三种命令（命名固定，与 spec §6.3 一致）：
    - `ApplyBrushCommand`（笔刷落点；前称 `BrushCommand` 在 spec 中已统一为 `ApplyBrushCommand`）
    - `CreateCityMapCommand`（创建城市地图）
    - `PlaceBuildingCommand`（放置建筑）
  - [ ] SubTask 13.2：实现 `src/editing/UndoStack.ts`：FIFO 上限 200；`undo()` / `redo()` / `push(cmd)`；新命令入栈时清空 redo 栈
  - [ ] SubTask 13.3：实现 `useEditingStore`（zustand）：持有 `UndoStack` 实例 + `runCommand(cmd)` / `undo()` / `redo()`；每次执行触发 `dirtyMapIds` 标记

- [ ] **Task 14：圆形笔刷**
  - [ ] SubTask 14.1：实现 `src/editing/brush.ts`：`applyRoundBrush(centerCol, centerRow, size, terrainType, strength) -> ApplyBrushCommand`；构造 `before: Snapshot`（仅记录会变化的单元）+ `After`
  - [ ] SubTask 14.2：左键拖拽期间按 **100ms 节流**落点（`requestAnimationFrame` + 时间戳门控，落点之间不补插值）；**`mouseup` 强制提交一次**（不论最后一次落点距 `mouseup` 是否到 100ms，落点 + mouseup 时刻的 hit 都并入同一条 `ApplyBrushCommand`）
  - [ ] SubTask 14.3：落点完成后触发 `TerrainLayer` 局部刷新（`dirty_rect`）

- [ ] **Task 15：建筑放置**
  - [ ] SubTask 15.1：选 `placeBuilding` + 选类型 → 进入「拖拽占地」状态
  - [ ] SubTask 15.2：左键按下记录起点 `(x,y)`、拖拽中显示当前 `geometry`，松开弹出属性对话框（`name` 必填，`interior_ref` 可选）
  - [ ] SubTask 15.3：确认后构造 `PlaceBuildingCommand`（`before_index = building_refs.len()`），执行；取消则不入栈

---

## 阶段 F · 下钻与导航

- [ ] **Task 16：城市下钻**
  - [ ] SubTask 16.1：实现 `src/ui/CityCard.tsx`：显示 `city_refs[i]` 基础信息 + 「进入编辑」/「创建城市地图」按钮（依据 `map_ref` 是否为空切换）
  - [ ] SubTask 16.2：点击 `MarkerLayer` 上的城市标记 → 弹出 `CityCard`
  - [ ] SubTask 16.3：点击「进入编辑」→ `useDomainStore.switchMap(city_refs[i].map_ref)`
  - [ ] SubTask 16.4：点击「创建城市地图」→ `invoke('create_city_map')`：
    - `Ok(Map)` → 自动 `switchMap(newMap.id)` + 关闭 CityCard
    - `Err(IoError)` → 弹错误对话框（复用 10.7 的 `ErrorDialog` 组件），**不调用** `switchMap`，CityCard 保持打开让用户重试
    - 按钮在调用期间禁用 + 显示「创建中…」避免重复点击

- [ ] **Task 17：面包屑**
  - [ ] SubTask 17.1：`useDomainStore` 维护 `breadcrumb: Array<{label, mapPath}>`；`switchMap` 时压栈，`switchToIndex(i)` 时截断
  - [ ] SubTask 17.2：`StatusBar` 渲染面包屑，点击任一段调用 `switchToIndex(i)`
  - [ ] SubTask 17.3：**视口恢复**（per-map viewport history）：
    - `useDomainStore` 维护 `viewportHistory: Map<mapPath, Viewport>`，键 = `Map.map_ref`（或 map.id）
    - `switchMap(newPath)` 离开当前地图时：把当前 `useUiStore.viewport` 写入 `viewportHistory[currentPath]`；再切到 `newPath` 时，从 `viewportHistory` 读出并 `useUiStore.setViewport(...)`；若不存在则调用 `Viewport.fitTo(map)`
    - 同一地图内 pan/zoom **不**写入历史（避免无限增长）；仅在「切走瞬间」写一次
    - breadcrumb 的 `switchToIndex(i)` 同样走视口恢复：压栈路径中第 i 段的目标 viewport 作为新视口
    - 单测：`viewportHistory_roundtrip.test.ts` — 模拟 world → city → world 来回，断言第二次进入 world 时视口 == 第一次离开时的视口

---

## 阶段 G · 验收

- [ ] **Task 18：跑通验收 demo D1–D8**
  - [ ] SubTask 18.1：依次按 D1–D8 操作并截图 / 录屏，记录到 **`docs/acceptance/map_editor_mvp_check.md`**（**不放** `examples/sample_world/` 内，避免 `open_project` 扫描时把验收证据误当成工程文件）
  - [ ] SubTask 18.2：跑 `cargo test --workspace`，领域层单测全绿
  - [ ] SubTask 18.3：跑 `pnpm tsc --noEmit`，前端类型零错
  - [ ] SubTask 18.4：**冒烟脚本**（覆盖 G3 / G4 / J2 / K1）：
    - `examples/sample_world/_smoke/run.ts` 用 Node 直跑，**不强制** Playwright；
    - 加载 `examples/sample_world/` → 调 `switchMap('maps/city/port_aurora.yaml')` → 断言 `breadcrumb.length === 3` 且 `currentMap.type === 'city'`；
    - 准备临时工程（`city_refs[0].map_ref` 为空）→ 调 `create_city_map` → 断言生成的 YAML 存在且 `project.map_index` 已同步；
    - 扫工程目录断言 `*.bin` 命中 0 条 + `encoding: chunked` 命中 0 条；
    - 视口裁剪测试：渲染 1024×1024 地图 + 128×128 视口 → `getDrawnCellCount() ≤ 64 × 64 × 1.2`
    - 脚本被 `pnpm run smoke` 调用；CI / 评审脚本一律以本脚本为入口

---

## 任务依赖图

```
T1 ──▶ T2 ──▶ T3 ──▶ T4 ──▶ T5 ──▶ T6 ──▶ T7 ──▶ T9 ──▶ T10 ──▶ T11 ──▶ T13 ──▶ T14 ──▶ T16 ──▶ T18
                                                          │            │
                                                          └──▶ T8 ─────┴──▶ T12 ──▶ T15 ──▶ T17 ──┘
```

- T2、T3、T5、T6 可并行；T7/T8 依赖 T4/T9 完成；T11 依赖 T7；T14/T15 依赖 T13；T16 依赖 T11。
- 阶段 G 必须在所有上游任务通过后才进入。