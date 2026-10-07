# 地图编辑器 MVP 功能清单 Spec

> 本文档定义 World View **地图编辑器 MVP** 的功能范围、数据契约与验收口径。
> 字段语义以 [`docs/数据模型规格_v0.1.md`](../../../docs/数据模型规格_v0.1.md) 为唯一真源；
> 编辑能力定义以 [`docs/地图编辑器规格_v0.1.md`](../../../docs/地图编辑器规格_v0.1.md) 为准；
> 界面骨架以 [`docs/界面与交互规格_v0.1.md`](../../../docs/界面与交互规格_v0.1.md) 为准；
> 存储布局以 [`docs/存储与目录规范_v0.1.md`](../../../docs/存储与目录规范_v0.1.md) 为准；
> 二进制分块以 [`docs/二进制格式规格_v0.1.md`](../../../docs/二进制格式规格_v0.1.md) 为准；
> 技术选型以 [`docs/技术基调确认_v0.1.md`](../../../docs/技术基调确认_v0.1.md) 为准（Tauri + Rust + PixiJS）。

---

## Why

World View 已完成 v0.1 设计定稿（P0-1 / P0-2 / P0-3 已收敛），但**地图编辑器尚无可运行实现**。当前任务是把《地图编辑器规格 v0.1》中可独立验证的子集，先以 MVP 形态落出来，做到：

- 打开一个工程目录就能看到**一张世界地图**渲染出来；
- 用户能**平移 / 缩放 / 切换图层可见性**；
- 用户能**用笔刷在地形网格上落点**并**撤销 / 重做**；
- 用户能**为城市条目创建并下钻到一张城市地图**，再**放一栋带室内引用的建筑**；
- 整个编辑过程**只产生 YAML + 二进制分块**标准格式文件，可直接被 Godot 导出目标消费。

MVP 不做完整 M1–M8 范围，**只做能跑通主路径的最小集合**；非主路径能力（影响力热力图、贴合算法、回放、运输等）显式 **NOT IN MVP**。

## What Changes

- **NEW**：Tauri + Rust + TypeScript + PixiJS 工程骨架（M1 基线）。
- **NEW**：领域层 `Map` / `Project` / `Layer` / `TerrainCell` 纯逻辑类型 + 序列化器（YAML + 二进制分块）。
- **NEW**：存储层 `ProjectStore`：按 `project.yaml` + `map_index` 加载、写入、校验。
- **NEW**：渲染层 PixiJS 画布：网格 + 地形色块 + 图层合成 + 视口平移 / 缩放。
- **NEW**：编辑层 `Brush`（圆形 / 方形）+ 命令模式 `UndoStack`。
- **NEW**：UI 层三栏布局（顶栏 / 左侧工具 / 中间画布 / 右侧选中对象信息面板 + 状态栏）。
- **NEW**：城市下钻（点击 `city_refs[i]` → 切换到 `map_ref` 指向的城市地图）+ 建筑放置（含 `interior_ref`）。
- **NEW**：示例工程 `examples/sample_world/`（一张世界地图 + 一张城市地图 + 一栋建筑 + 一个居民）。
- **BREAKING**：暂无（仓库目前没有代码）。

## Impact

- 受影响规格（**只读取，不修改字段定义**）：
  - `docs/地图编辑器规格_v0.1.md`（MVP 范围对齐 §1.1 笔刷、§2.1 地形绘制、§3.1 建筑放置、§4 多地图引用）
  - `docs/界面与交互规格_v0.1.md`（MVP 范围对齐 §1 主界面布局、§2.1 世界→城市下钻）
  - `docs/图层系统规格_v0.1.md`（MVP 仅使用 §2.1 地形层 + §2.4 道路层最小集）
  - `docs/存储与目录规范_v0.1.md`（MVP 走 §1 标准目录布局 + §5.1 `grid`/`sparse` 编码；**MVP 不引入 `chunked`**）
- 受影响代码（**新建**）：
  - `crates/world-view-core/` — Rust 核心（领域模型 + 序列化 + 校验）
  - `crates/world-view-io/` — Rust I/O（Tauri commands）
  - `src/` — TypeScript 前端（PixiJS 渲染 + 编辑 + UI）
  - `examples/sample_world/` — 示例工程

---

## 1. MVP 范围声明

### 1.1 IN MVP（必须实现并验收）

| # | 能力 | 引用 |
|---|---|---|
| F01 | **打开 / 创建工程**：选择目录（含 `project.yaml` 则打开；否则创建新工程骨架） | 存储 §1；界面 §2 |
| F02 | **加载一张世界地图**：从 `map_index[0]` 读取 YAML，渲染到画布 | 地图编辑器 §2 |
| F03 | **平移 / 缩放 / 全图适配 / 显示网格 / 显示坐标 / 显示比例尺** | 地图编辑器 §5.2 / §8.3 |
| F04 | **图层可见性 / 锁定 / 透明度 / z_order 调整**（仅地形层 + 道路层） | 图层系统 §1 |
| F05 | **圆形笔刷绘制地形**：size / 强度可调；落点吸附网格；写入 `terrain_data`（`grid`/`sparse` 编码） | 地图编辑器 §1.1 / §2.1 |
| F06 | **撤销 / 重做**（命令模式，至少 50 步；含笔刷命令） | 地图编辑器 §1.3 |
| F07 | **城市条目下钻**：点击 `city_refs[i]` 卡片 → 打开 `map_ref` → 切换到城市地图 | 地图编辑器 §3.5 / §4；界面 §2.1 |
| F08 | **创建城市地图**：`city_refs[i].map_ref` 为空时弹「创建」对话框，生成 `maps/city/<id>.yaml`。**默认值**：`grid` = `{ width: 64, height: 64, cell_distance: 2, distance_unit: "m", base_unit: "m", origin: {x:0,y:0}, show: true, snap: true }`；`time` = `{ default_granularity: "hour", min_granularity: "hour" }`；`type = city`；`layers` 默认含 `terrain` + `road` 两层；`terrain_data.encoding: sparse`、`default: {type: plain, move_cost: 1, buildable: true}`、`cells_ref: maps/city/<id>.terrain.yaml`；同步更新 `project.map_index` 与 `world_map.city_refs[i].map_ref` | 地图编辑器 §2.4 |
| F09 | **建筑放置**：选择建筑类型 → 拖拽占地 → 写入 `building_refs[]`；可指定 `interior_ref`（可为空） | 地图编辑器 §3.1 |
| F10 | **面包屑**（世界 › 城市）+ 逐级返回 | 界面 §2.1 |
| F11 | **校验器（最小集）**：保存时检查 `ref` 存在 + ID 唯一 + 必填字段 | 存储 §7 |
| F12 | **保存工程**：将所有修改写回 YAML（地形**加载支持 grid 与 sparse，保存统一写 sparse**，落 `maps/.../main.terrain.yaml`） | 存储 §1 / §3 / §5.1.2 |
| F13 | **示例工程**：内置一份 `examples/sample_world/`，开箱即可加载 | — |
| F14 | **人物 / 事件数据契约样例**（非功能范围，**非 IN MVP UI**）：示例工程保留 `characters/_templates.yaml` + `characters/lin_yu.yaml` 作为**数据契约样例**；MVP 不加载人物到 UI、不暴露任何人物 UI、不纳入验收 demo；仅供领域层单测验证 `Character` / `CharacterTemplate` 的 YAML 解析与 `residence.map_ref` 跨文件引用格式 | 事件与人物登记 §2 |

### 1.2 NOT IN MVP（**显式不做**，留待后续迭代）

| 能力 | 计划里程碑 |
|---|---|
| 高度图绘制 / 平滑 / 等高线 | M2 后续 |
| 套索 / 魔术棒笔刷 / 沿路径笔刷 / 填充 | M2 后续 |
| 选区变换（旋转 / 缩放 / 对齐 / 吸附边缘） | M3 后续 |
| 二进制分块（`encoding: chunked`） | M1 仅引入分块索引声明，**MVP 不生成 `.bin`**；MVP 只用 `grid` + `sparse` |
| 疆界自动贴合 / 影响力衰减 / 裁剪 | M3 |
| 城市居民生成 / 街道绘制 / 巷子 / 桥梁 / 城墙 | M4 |
| 建筑内放置家具 / 门 / 入口编辑 | M5 后续 |
| 室内图下钻（点击带 `interior_ref` 的建筑→切到 `building` 图） | **MVP 不做**（下钻链路只到 `city`） |
| 时间轴联动 / 回放 / 快照 | M6 |
| 运输 / 路线 / 载具 | M8 |
| 视觉增强（昼夜 / 光照 / 战争迷雾 / 粒子） | M3+ |
| 多地图标签页 / 自定义面板布局 | M7 |
| 自动保存 / 备份 / 撤销栈持久化 | M7 |
| Godot 导出模块 | M7 |
| **人物详情面板 / 人物编辑 / 关系网 UI / 属性模板 UI** | **MVP 不做**（F14 仅作数据契约样例，不进 UI；M5 完整人物登记） |
| **事件登记面板 / 时间轴 UI / 路线编辑面板 / 载具登记面板** | **MVP 不做**（不进 UI；M5/M6/M8 分别落地） |
| 第三方插件 | 不在 v0.1 范围 |

---

## 2. 数据契约（中立字段；**实现只读取，不修改《数据模型规格》字段**）

### 2.1 Project（`project.yaml`）

MVP 只用到以下字段子集（字段定义见数据模型规格 §1）：

- 必填：`project_version`、`name`、`era`、`default_time`、`map_index`
- 可选：`character_index`（**仅作示例工程的数据契约样例**，MVP 不加载到 UI、不验证有效性）、`settings.autosave: false`

**F14「人物数据契约样例」明确定位**：

- **不做**：人物 UI / 人物编辑 / 关系网 / 属性模板编辑 / 事件登记面板 / 时间轴 UI / 路线编辑面板 / 载具登记面板
- **做**：示例工程保留 `characters/_templates.yaml` + `characters/lin_yu.yaml`；MVP 不加载到 UI store；仅领域层单测中按 `from_yaml_str` / `to_yaml` 解析往返，验证 `Character` / `CharacterTemplate` 字段定义与 `residence.map_ref` 跨文件引用格式
- **验收**：F14 **不进**验收 demo（D1–D8 不覆盖）；单测覆盖即可
- **防误解**：示例工程 `README.md`（或本节末尾）须写明「人物 / 事件仅作数据契约样例，M5+ 才进 UI」，避免用户加载后困惑

### 2.2 Map（`maps/**/*.yaml`）

MVP 必填子集：

- `id` / `name` / `type`
- `grid`（完整字段：`width`/`height`/`cell_distance`/`distance_unit`/`base_unit`/`origin`/`show`/`snap`）
- `time`（完整字段：`default_granularity`/`min_granularity`）
- `layers`（MVP 只配 `terrain` 与 `road` 两层；其余图层允许出现但渲染层忽略）
- `terrain_data`（`encoding: grid` 或 `sparse` + `cells_ref`）
- `city_refs`（仅 `world` 类型）
- `building_refs`（仅 `city` 类型）

MVP 不写：`height_data`（可空）、`markers`（可空）、`presentation`（可空但建议带默认）、`travel`（可空）。

**MVP 透传字段约定**：`presentation` / `markers` / `travel` / `height_data` / `parent_map` / `child_maps` / `anchor` / `extent` / `custom_attributes` 等字段**MVP 阶段只透传保留**——加载时存入 `LoadedMap.map` 的对应字段，**不读取其语义**渲染；保存时原样写回。

**MVP 地图尺寸约束**：

- MVP **推荐**单张地图 ≤ 1024 × 1024 单元。
- 单图超过 1024 × 1024 时（不自动切换；MVP 不引 `chunked`）：保存时**提示**「该路径未启用 chunked，建议缩到 1024×1024 内或等待 MVP`；笔刷不阻塞。
- 单图超过 1024 × 1024 时，**仍允许加载与渲染**，仅 Git diff 噪声与解析性能可能退化。

**MVP 阶段 `height` 约定**：

- `height_data` **MVP 不读取、不写入**；示例工程中所有地图 `height_data` 字段省略。
- 地形单元 `terrain.height`（[数据模型规格 §3.2](file:///d:/kudi-project/world-view/docs/数据模型规格_v0.1.md)）在 MVP 阶段**不参与计算**；值为 `0` 表示「无高度信息」。
- 所有依赖高度的能力（地形阴影 / 河流方向 / 疆界贴合 / 通行成本对高度的修正）MVP 阶段一律**关闭**，对应字段标 `[待标定]`、对应 UI 不展示。
- 不与 `default.height` 冲突：`sparse.default` MVP 阶段**只填** `{type: plain, move_cost: 1, buildable: true}`，**不带 `height`**（height 留 M2+ 处理）。

### 2.3 Layer（`layers[]`）

MVP 只渲染**可见且未被时间过滤**的图层（时间过滤留空，所有图层视为全程可见）。MVP 用到的字段：

- `id` / `name` / `type`（仅识别 `terrain` 与 `road`）/ `visible` / `locked` / `opacity` / `z_order`
- `valid_from` / `valid_to` **MVP 忽略**（按可见处理）

### 2.4 Terrain（`maps/.../<map>.terrain.yaml`）

MVP **加载**支持 `encoding: grid`（二维数组：行优先 `cells[row*W+col] = {type, move_cost, buildable}`）与 `encoding: sparse`（cells 列表）；**保存统一写 sparse**（按 [存储规范 §5.1.2](file:///d:/kudi-project/world-view/docs/存储与目录规范_v0.1.md) 决策阈值：≤ 1024 单元仍可写 grid，但 MVP 默认走 sparse 以降低 Git diff 噪声）。`chunked` MVP **不生成**。

地形类型枚举以 `data_model §3.1` 为准；MVP 默认全部填默认值 `{type: plain, move_cost: 1, buildable: true}`。

### 2.5 引用规则

MVP 严格遵守《存储与目录规范》§3：

- 跨文件引用 → `ref`（相对工程根路径）
- 同类型对象之间 → `id`
- 所有 `ref` 不得含 `..`、不得是绝对路径

### 2.6 YAML 示例（示例工程中的世界地图）

```yaml
id: map_world_01
name: 艾瑟兰大陆
type: world

grid:
  width: 64
  height: 48
  cell_distance: 10
  distance_unit: km
  base_unit: m
  height_unit: m
  origin: { x: 0, y: 0 }
  show: true
  snap: true
  scale_label: "1 格 = 10 km"

time:
  default_granularity: month
  min_granularity: day
  inherit_calendar: true
  allow_fuzzy: true

presentation:
  default_mode: edit
  view_mode: 2d
  theme: parchment
  show_scale_bar: true
  show_coordinates: true

terrain_data:
  encoding: grid
  cells_ref: maps/world/main.terrain.yaml

layers:
  - id: layer_terrain
    name: 基础地形
    type: terrain
    visible: true
    locked: false
    opacity: 1.0
    z_order: 0
  - id: layer_road
    name: 道路
    type: road
    visible: true
    locked: false
    opacity: 1.0
    z_order: 10

city_refs:
  - id: city_port_aurora
    name: 极光港
    x: 32
    y: 24
    entry: { x: 32, y: 24 }
    map_ref: maps/city/port_aurora.yaml
    population: 24000
    faction: fac_merchant_league
```

---

## 3. ADDED Requirements

### Requirement: 工程加载与保存

`world-view-io` SHALL 提供 `open_project(dir)` / `save_project(project, dir)` 两个 Tauri command；`open_project` 必须解析 `project.yaml` 并按 `map_index` 顺序加载每张地图（地形文件按 `cells_ref` 解析为 `grid`/`sparse`）；`save_project` 必须把内存中的 `Project` 完整写回磁盘（含所有引用闭包）。

#### Scenario: 打开示例工程

- **WHEN** 用户选择目录 `examples/sample_world/`
- **THEN** `open_project` 在 200ms 内返回 `Project{ name: "示例世界", maps: [world_map], characters: [...] }`
- **AND** 前端将 `world_map` 渲染到画布
- **AND** 状态栏显示面包屑「示例世界 › 艾瑟兰大陆」

#### Scenario: open_project 加载失败

- **WHEN** 任一 `map_index[i]` 加载失败（文件不存在 / YAML 解析错 / `cells_ref` 不可达）
- **THEN** `open_project` 返回 `Err(IoError::MapLoadFailed { path, cause })`，**不返回部分 `Project`**（已读入但未完成加载的 `Map` 实例全部丢弃）

#### Scenario: 保存工程

- **WHEN** 用户点击「保存」且当前有未保存修改
- **THEN** `save_project` 把所有修改过的 YAML 写回原路径
- **AND** 写回前校验：`ref` 存在 / ID 唯一 / 必填字段；不通过时返回错误并阻止保存
- **AND** 写回后清空「未保存」标记

### Requirement: 视图与坐标

渲染层 SHALL 提供平移（右键 / 中键拖拽）、缩放（滚轮）、全图适配（`F` 键）、显示网格（`G` 切换）、显示坐标（`C` 切换）、显示比例尺（默认开启）。坐标系遵守 §5.3：屏幕 → 网格 → 地图 → 世界四层互转。

**Esc 取消当前工具**：当前工具 ≠ `pan` 时按 Esc 复位到 `pan`；同时清空临时拖拽状态（未提交笔刷、放置建筑 dragRect 候选框、选区笔刷候选框）；**不撤销**已提交命令（撤销走 `Ctrl+Z`）；关闭所有模态对话框。

#### Scenario: 全图适配

- **WHEN** 用户按下 `F`
- **THEN** 画布视口调整为地图 `grid.width × grid.height` 完全可见 + 留 8% 内边距

#### Scenario: 缩放至网格内

- **WHEN** 用户在画布 `(sx, sy)` 滚轮放大
- **THEN** 缩放围绕 `(sx, sy)` 保持该像素位置下的网格点不动
- **AND** 缩放比例 clamp 到 [0.25, 8.0]

### Requirement: 图层面板

UI SHALL 在右侧提供图层面板：列出当前地图的所有 `layers[]`，每行显示 `name` + `type` + `visible`/`locked`/`opacity`/`z_order` 控件；变更后立刻同步到渲染层。

#### Scenario: 隐藏地形层

- **WHEN** 用户取消勾选「地形」图层的 `visible`
- **THEN** 渲染层立即移除地形色块
- **AND** 「未保存」标记亮起

### Requirement: 圆形笔刷地形绘制

编辑层 SHALL 提供圆形笔刷：参数 `size`（格，1–16，默认 3）、`terrain_type`（默认 `plain`）、`strength`（0.1–1.0，默认 1.0）。鼠标按下 + 拖拽期间持续落点；每个落点向画布下达一条 `ApplyBrushCommand`。

#### Scenario: 绘制山地

- **WHEN** 用户选择地形 `mountain` + `size=4` + `strength=1.0`，在 `(30,30)`–`(40,40)` 区域拖拽
- **THEN** 落点范围内所有单元 `type` 写为 `mountain`，`move_cost=4`，`buildable=false`
- **AND** 渲染层立即刷新该区域
- **AND** 一条 `ApplyBrushCommand` 入撤销栈

#### Scenario: 吸附网格

- **WHEN** `grid.snap = true`
- **THEN** 笔刷中心始终对齐到最近格点；落点坐标按整数格计算

### Requirement: 撤销与重做

`UndoStack` SHALL 支持 ≥ 50 步撤销 / 重做；MVP 仅持久化到内存（不落盘）。每条命令实现 `apply()` / `revert()`；笔刷命令的 `revert()` 必须把单元恢复成落点前的快照。

#### Scenario: 撤销笔刷

- **WHEN** 用户按下 `Ctrl+Z` 且撤销栈非空
- **THEN** 栈顶命令 `revert()` 生效，渲染层刷新该区域
- **AND** 命令从撤销栈移到重做栈

#### Scenario: 重做笔刷

- **WHEN** 用户按下 `Ctrl+Y` 且重做栈非空
- **THEN** 栈顶命令 `apply()` 生效，渲染层刷新该区域

### Requirement: 城市下钻

UI SHALL 在世界地图上把 `city_refs[i]` 渲染为可点击标记；点击后弹出基础信息卡，含「进入编辑」按钮：

- 当 `map_ref` 存在 → 切换到该城市地图（写入「当前地图」状态）；
- 当 `map_ref` 为空 → 按钮文字变为「创建城市地图」，点击后由 `world-view-io` 生成 `maps/city/<id>.yaml`（字段自动按城市地图默认：grid 64×64 / `cell_distance=2 m` / `default_granularity: hour`），再切换。

#### Scenario: 下钻到已有城市

- **WHEN** 用户点击「极光港」标记 → 弹出基础信息卡 → 点击「进入编辑」
- **THEN** 当前地图切到 `maps/city/port_aurora.yaml`
- **AND** 面包屑变为「示例世界 › 艾瑟兰大陆 › 极光港」
- **AND** 画布重置为该城市地图的视口

#### Scenario: 创建缺失城市地图

- **WHEN** 用户点击某 `city_refs[i].map_ref` 为空的条目 → 点击「创建城市地图」
- **THEN** `world-view-io` 在 `maps/city/<id>.yaml` 写出最小可加载骨架（**默认值**：`grid 64×64 / cell_distance=2 m / distance_unit=m / base_unit=m / time.default_granularity=hour / time.min_granularity=hour / presentation.theme=parchment / layers=[terrain, road] / terrain_data.encoding=sparse, default={type:plain}`）
- **AND** 同时写一份空的 `<id>.terrain.yaml`（仅有 `encoding: sparse` + `default`）
- **AND** `project.map_index` 与 `world_map.city_refs[i].map_ref` 同步更新
- **AND** 立即下钻到该城市地图
- **AND** 若 `create_city_map` 返回 `Err(IoError)`：弹错误对话框（§ErrorDialog），**不下钻**，CityCard 保持打开

### Requirement: 建筑放置

在城市地图画布上，UI SHALL 提供「放置建筑」工具：从类型列表（住宅 / 商店 / 酒馆 / 神庙 / 城堡 / 兵营 / 市场 / 码头 / 监狱 / 魔法塔）选一项 → 鼠标按下并拖拽决定 `geometry = {x, y, w, h}` → 松开后弹出属性对话框（含 `name`、`type`、`interior_ref` 可选） → 确认后写入 `building_refs[]`。

#### Scenario: 放置住宅

- **WHEN** 用户选 `type=住宅`、在 `(10,10)` 拖拽至 `(14,14)`、属性对话框 `name=林语居`、留空 `interior_ref`
- **THEN** `city_map.building_refs` 追加 `{id: bld_<uuid>, name: "林语居", type: "住宅", geometry: {x:10,y:10,w:4,h:4}, rotation: 0, floors: 1, state: intact}`

#### Scenario: 放置带 interior_ref 的建筑

- **WHEN** 属性对话框 `interior_ref` 留空（不创建室内图）→ 确认
- **THEN** `interior_ref` 字段不写入（**MVP 不支持室内下钻**；保留字段以维持与数据模型一致）

### Requirement: 面包屑

UI SHALL 在状态栏左侧显示面包屑 `项目 › 地图1 › 地图2 ...`；点击任一段可跳回该层级；MVP 仅支持两级（世界 / 城市）。

**视口恢复**：领域 store 维护 `viewportHistory: Map<mapPath, Viewport>`；`switchMap(newPath)` 离开当前地图时把当前视口写入历史，切到 `newPath` 时优先从历史恢复（无则 `fitTo`）；同一地图内的 pan/zoom 不写入历史，避免 map 内存无限增长。

#### Scenario: 返回世界

- **WHEN** 用户在城市地图中点击面包屑的「艾瑟兰大陆」
- **THEN** 当前地图切回世界地图
- **AND** 视口回到进入城市前的位置（来自 `viewportHistory[world_path]`）

### Requirement: 校验器

`world-view-io` SHALL 在 `save_project` 前执行最小校验：

1. `project.yaml` 存在且 `project_version = "0.1"`；
2. 所有 `map_ref` / `interior_ref` / `cells_ref` 指向的文件存在；
3. `id` 工程内全局唯一（跨类型）；
4. **`grid` 编码**：`terrain_data.cells` 行数 == `grid.height`、列数 == `grid.width`；**`sparse` 编码**：每个 cell 的 `(col, row)` 落在 `[0, width) × [0, height)` 内，且 `(col, row)` 唯一不重复；
5. `city_refs[i].map_ref` 为空时合法（表示未创建城市地图）；非空时路径可达。

#### Scenario: 校验失败阻止写入

- **WHEN** 用户删除示例工程中的 `maps/city/port_aurora.yaml` 后再保存
- **THEN** 校验器报错「`map_ref maps/city/port_aurora.yaml` 不可达」
- **AND** 弹出错误对话框，写盘动作被取消

### Requirement: 示例工程

仓库 SHALL 内置 `examples/sample_world/`，开箱即可被 `open_project` 加载：

- `project.yaml`（`name: 示例世界`，1 张世界地图 + 1 张城市地图索引）
- `maps/world/main.yaml`（64×48，1 个城市 `极光港`）
- `maps/world/main.terrain.yaml`（`grid` 编码，含若干山地 / 河流示例单元）
- `maps/city/port_aurora.yaml`（64×64，1 栋建筑 `林语居`）
- `maps/city/port_aurora.terrain.yaml`（默认全平原）
- `characters/_templates.yaml`（含 1 条 HP 模板，演示模板机制）
- `characters/lin_yu.yaml`（1 个示例人物，居住地 `residence.map_ref` 指向 `port_aurora.yaml`、`building_ref: bld_xx`）

#### Scenario: 开箱加载

- **WHEN** 用户首次启动应用并选择「打开示例工程」
- **THEN** 加载成功，画布显示世界地图，地形色块正确（山地灰偏暗、河流蓝色）
- **AND** 「极光港」标记可见，点击可下钻

---

## 4. MODIFIED Requirements

无（仓库目前没有代码，不存在「修改既有实现」）。

## 5. REMOVED Requirements

无。

---

## 6. 实现架构（仅约束实现层，不污染数据契约）

### 6.1 仓库布局

```
world-view/
├── crates/
│   ├── world-view-core/        # 领域层：Project/Map/Layer/Terrain 类型 + YAML 解析
│   └── world-view-io/          # Tauri commands + 文件 I/O + 校验器
├── src/                        # TypeScript 前端
│   ├── app/                    # 应用入口、根布局
│   ├── render/                 # PixiJS 渲染层（地形、图层、视口）
│   ├── editing/                # 笔刷、命令模式、撤销栈
│   ├── ui/                     # 顶栏 / 左工具栏 / 右面板 / 状态栏
│   ├── state/                  # Zustand：领域 store + UI store（严格分层）
│   └── bridge/                 # 与 Tauri commands 的 IPC 封装
├── examples/
│   └── sample_world/           # 示例工程（开箱可用）
├── docs/                       # 既有设计文档（不动）
└── ...
```

### 6.2 状态分层

- **领域 store**（Zustand）：持有 `Project` / 所有已加载的 `LoadedMap` / `currentMapId`（当前地图 ID）/ `currentMapPath`（当前地图文件路径）/ `dirtyMapIds` / `breadcrumb: Array<{label, mapPath}>`。**「当前地图」归属在领域 store**——它决定渲染层订阅哪张地图；UI store 不持有 `currentMapId`。
- **UI store**（Zustand）：当前工具 / 笔刷参数 / 视口 / 选区 / 图层面板展开状态。
- **视口栈**（`viewportHistory: Map<mapId, Viewport>`）：放在领域 store；下钻前快照当前视口与对应 mapId，回退时恢复。MVP 仅支持两级（世界 / 城市），所以 MVP 可用 `lastWorldViewport: Viewport | null` 简化实现。
- **渲染缓存**：仅 `render/` 模块内持有；不入 store。

### 6.3 命令模式

- `ApplyBrushCommand { stroke: Stroke, before: Snapshot, after: Snapshot, dirty_rect }`（统一命名，与 tasks.md T13.1 / checklist E4 一致）
- `CreateCityMapCommand { new_path, initial_map, parent_world_id, city_id }`
- `PlaceBuildingCommand { map_id, building, before_index }`
- 撤销栈：FIFO 上限 200；内存持有；MVP 不持久化。

### 6.4 性能纪律（MVP 适用部分）

- 视口外不渲染（MVP 实现 `cull`：基于视口网格范围跳过非可见单元）；
- 笔刷落点仅触发**脏区**重绘（`dirty_rect`）；
- 笔刷拖拽期间按 **100ms 节流**（`requestAnimationFrame` + 时间戳门控）落点，落点之间不补插值；松开提交一条完整 `ApplyBrushCommand`；
- **视口裁剪**：渲染层对每帧**只绘制视口覆盖的网格单元**（加 1 格冗余边带）；不在视口的单元**不构造 PixiJS 图形对象**；`TerrainLayer` 暴露 `getDrawnCellCount()` 用于量化断言；
- `terrain_data` MVP 走 `grid`/`sparse`；超过 1024×1024 时**手动提示**「MVP 阶段未启用 chunked，建议使用 sparse」。

---

## 7. 验收口径（demo）

| # | 验收 demo | 通过条件 |
|---|---|---|
| D1 | 启动应用 → 打开示例工程 → 世界地图渲染 | 画布显示 64×48 网格、地形色块、`极光港`标记 |
| D2 | 滚轮缩放 / 鼠标拖拽平移 / `F` 全图适配 / `G` 切网格 | 视口响应正确，坐标显示随指针更新 |
| D3 | 选圆形笔刷 + `mountain` + 在画布拖拽 → 看到山地刷出 | 笔刷落点写为 `mountain`，色阶变深 |
| D4 | `Ctrl+Z` 撤销、`Ctrl+Y` 重做 | 山地区域恢复 / 重画 |
| D5 | 点击「极光港」标记 → 基础信息卡 → 「进入编辑」 | 切到城市地图，面包屑更新 |
| D6 | 选建筑类型「住宅」→ 在城市地图拖拽 → 弹属性框 → 确认 | `building_refs` 新增条目，建筑以色块渲染 |
| D7 | 关闭应用 → 重新打开同一目录 | 所有修改保留（YAML 落盘正确） |
| D8 | 删除 `port_aurora.yaml` 后点保存 | 校验器报错，写盘取消 |

---

## 8. 风险与回退

| 风险 | 对策 |
|---|---|
| PixiJS 在 Tauri WebView 中 GPU 加速不稳定 | 启动时检测 WebGL2 失败则降级到 Canvas（性能略低但可运行） |
| YAML 解析性能（64×48 地图） | MVP 网格上限建议 ≤ 1024×1024；超过提示改 chunked（MVP 不实现） |
| 命令模式内存增长 | 撤销栈上限 200；超出丢弃最旧 |
| `terrain_data` 大数据回写慢 | MVP 用 `sparse` 编码写出（仅写非默认值单元） |

---

## 9. 范围纪律（再次声明）

- MVP **不做**二进制分块（`chunked`）；仅 `grid` / `sparse`。
- MVP **不做**室内图下钻（`building` 层级保留字段但不实现跳转）。
- MVP **不做**时间轴联动、回放、快照。
- MVP **不做**运输 / 路线 / 载具。
- MVP **不做**疆界自动贴合 / 影响力裁剪 / 居民生成。
- MVP **不做**视觉增强（昼夜 / 光照 / 战争迷雾）。
- MVP **不做**第三方插件、分支时间线、协作。

以上能力均归属 M3+，按《总体设计 v0.1》§7 排期推进；本 MVP 仅提供 M1/M2/M4 子集的可运行基线，作为后续里程碑的起点。

---

## 10. 变更记录

| 版本 | 变更 |
|---|---|
| v0.1 | 首版：MVP 范围声明（§1）、数据契约（§2）、ADDED Requirements（§3）、仓库布局（§6）、验收 demo（§7）、风险（§8）、范围纪律（§9）。 |
| v0.1（补充：P0 评审修复） | §2.2 增加「MVP 阶段 `height` 约定」：height_data 不读写；terrain.height = 0；高度相关能力关闭；sparse.default 不带 height。 |
| v0.1（补充：P1 评审修复） | §1.1 增加 F14「人物索引只读展示」；§1.2 增加「人物 / 事件 / 路线 / 载具 UI 面板不做」三行；F12 改为「加载 grid+sparse，保存统一写 sparse」；F08 列默认值；§2.2 增加「MVP 透传字段约定」与「MVP 地图尺寸约束」；§2.4 区分加载与保存的编码策略；§3 Requirement: 校验器 第 4 条按 grid/sparse 分两支；§6.2 增加 viewportHistory、明确 currentMapId / currentMapPath 归属领域 store。 |