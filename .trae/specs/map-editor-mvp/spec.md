# Map Editor MVP — 规格说明

> 范围:运行本文档示例定义的数据模型与目录规范的最小编辑器实现。
> 栈:**Godot 4.7.2(mono)+ C# (.NET 8)**;数据契约中立(YamlDotNet)。
> 编写目的:作为开发、测试与验收的共同基线。所有未在此文档中明确为「MVP 之内」或「MVP 之外」的功能,默认属于范围外。

***

## 1. Why

用户需要一个能在桌面端打开一个示例世界工程、切换地图、画地形笔刷、放置建筑、撤销重做、并能下钻到城市地图的最短闭环工具。M1 阶段不追求完整功能覆盖,只验证:

1. 数据契约(YAML / JSON 字段名与默认值)在新的 Godot + C# 栈上可以端到端跑通;
2. 编辑器交互回路能落地(笔刷落点 → 画 → 重绘 → 撤销栈 → 持久化);
3. 城市下钻 + 建筑放置可以演示「多地图结构」的最小用例。

后续 M2/M3 才会扩展为正式的世界编辑器。

***

## 1.1 IN MVP — 功能清单

| 编号  | 功能                                                    | 验收依据                             |
| --- | ----------------------------------------------------- | -------------------------------- |
| F01 | 打开 / 保存 `project.yaml`                                | D1(打开示例工程成功) + D7(保存回写 + 重新打开一致) |
| F02 | 顶栏 / 状态栏 / 工具栏 / 检视器 4 区布局                            | D1 / D2(切地形类型与笔刷) / D6(切城市下检)    |
| F03 | 顶栏工程下拉菜单(打开 / 关闭 / 另存为)                               | D1 / D7                          |
| F04 | 图层面板(图层可见性切换)                                         | D2(关 terrain 后地形不可见)             |
| F05 | 工具栏(选中 / 笔刷 / 建筑)                                     | D2 / D6                          |
| F06 | 检视器(地图元数据只读)                                          | D1                               |
| F07 | 状态栏(地图名 / 鼠标坐标 / 选中建筑)                                | D1 / D6                          |
| F08 | 画布节点树:`Camera2D` + `TileMapLayer` × N + `GridOverlay` | C2 / J2                          |
| F09 | 键盘平移(WASD / 方向键) + 鼠标滚轮缩放 + 中键拖拽                      | C3                               |
| F10 | 圆形笔刷 + 1 个地形类型;`ApplyBrushCommand`                    | D2 / D3 / D4                     |
| F11 | 撤销 / 重做(Ctrl+Z / Ctrl+Shift+Z,栈深 200)                 | D3 / D4                          |
| F12 | 双击城市 → 进入城市地图,顶栏面包屑返回                                 | D5 / D6                          |
| F13 | 在建筑数据可点击的位置放置建筑 + 「建筑占位」对话框                           | D6                               |
| F14 | 保存 + 启动时自动校验(YAML 必填字段、引用合法性、ID 唯一性)                  | D7 / D8                          |

***

## 1.2 NOT IN MVP — 范围外

| 编号  | 功能                      | 推迟到 |
| --- | ----------------------- | --- |
| N01 | 高度图 / 等高线               | M3+ |
| N02 | 套索 / 多选 / 选区编辑          | M4+ |
| N03 | 选区变换(旋转 / 缩放)           | M4+ |
| N04 | `chunked` 编码(> 1M 单元)   | M5  |
| N05 | 疆界贴合 / 国家边界             | M5  |
| N06 | 居民 / 人口统计               | M5+ |
| N07 | 室内下钻 / 房间系统             | M5+ |
| N08 | 时间轴 / 事件                | M5+ |
| N09 | 运输 / 商路 / 物流            | M6+ |
| N10 | 视觉增强(光照 / 阴影 / 后期)      | M6+ |
| N11 | 多地图标签页(同时打开多个工程)        | M7+ |
| N12 | 自动保存 / 崩溃恢复             | M7+ |
| N13 | Godot 导出(`.pck` / 平台打包) | M7+ |
| N14 | 人物 UI / 角色面板            | M7+ |
| N15 | 事件 UI / 日志面板            | M7+ |
| N16 | 插件系统 / 第三方扩展            | M7+ |

***

## 2. 数据契约(MVP 投影)

> **MVP 投影说明**：本节是 `docs/数据模型规格_v0.1.md` 与 `docs/存储与目录规范_v0.1.md` 的 MVP 投影。
> 字段定义 / 枚举 / 引用规则的唯一真源是《数据模型规格》§2;本节只说明 MVP 关心的字段范围与默认值,以及 MVP 暂时不实现的字段(按规格默认值或不写入 YAML)。
> 字段对照见 §2.7 末尾的「字段对照表」。
>
> 引用规则:`ref` = 相对工程根(`project.yaml` 所在目录)的路径;`id` = 稳定 ID;两者**不可混用**;`ref` 中不得含 `..`,不得为绝对路径。

### 2.1 Project(工程)

```yaml
project:
  project_version: "0.1"  # 必填
  name: string            # 必填,非空
  default_time:
    year: int             # 必填
    month: int            # 可选(1..months_per_year)
    day: int              # 可选(1..days_per_month[month-1])
  era:
    name: string
    start_year: int
    months_per_year: int
    days_per_month: [int, ...]
    hours_per_day: int
    minutes_per_hour: int
    seconds_per_minute: int
    leap: { every: int, month: int, add_days: int }
  map_index:
    - ref: maps/<file>.yaml
  character_index:        # MVP 仅作数据契约样例,前端 UI 不加载
    - ref: characters/<file>.yaml
  assets:
    textures:
      tiles:
        manifest: assets/textures/tiles/index.yaml
  settings:
    autosave: bool
    backup_keep: int
```

### 2.2 Map(地图)

```yaml
map:
  id: string                # 必填,工程内唯一(稳定 ID,跨文件引用用此)
  name: string              # 必填,非空
  type: "world" | "city"    # 必填;MVP 仅支持 world / city(region/street/building/underground 见 M3+)
  parent_map: { id: string }  # 可选;MVP 默认为 null(根级地图)

  grid:                     # 必填
    width: int              # 必填,> 0
    height: int             # 必填,> 0
    cell_distance: number   # 必填,> 0
    distance_unit: "m"      # 必填,字面量 "m"
    base_unit: "m"          # 必填,字面量 "m"
    height_unit: "m"        # 必填,字面量 "m"
    origin: { x: 0, y: 0 }  # 可选
    show: true              # 可选
    snap: true              # 可选
    scale_label: string     # 可选

  time:                     # 必填
    default_granularity: "hour"   # 必填(world=month/day; city=hour/hour; building=minute/minute; 见数据模型规格 §2.3)
    min_granularity: "hour"       # 必填
    display_format: string       # 可选
    inherit_calendar: true        # 可选
    allow_fuzzy: true             # 可选

  presentation:             # 可选
    default_mode: "edit"
    view_mode: "2d"          # MVP 唯一字面量
    theme: "parchment"
    show_scale_bar: true
    show_coordinates: true
    show_minimap: false

  layers:                   # 必填,至少 1 项
    - ref: ../layers/<file>.yaml   # 跨文件引用必须用 ref

  city_refs:                # type="world" 时可选(MVP 至少 1 项以验证 D5)
    - id: string            # 工程内唯一,与目标城市地图 id 一致
      name: string
      x: int                # 世界地图网格坐标
      y: int
      entry: { x: int, y: int }  # 进入城市地图时的初始焦点(城市坐标)
      map_ref: ../maps/<city>.yaml
      population: int
      faction: string
      notes: string

  building_refs: []         # MVP 不使用(建筑走 inline,见 §2.5)

  terrain_data:             # 必填
    encoding: "sparse"      # MVP 仅支持 sparse(grid 不引二进制分块;chunked 见 N04,推迟 M5)
    default:                # 必填
      type: "plain"
      move_cost: 1
      buildable: true
    cells_ref: ../maps/<...>.terrain.yaml  # 可选;外部地形文件(sparse)
    cells: []               # 内联地形单元(sparse 编码);MVP 通常为空,数据走 cells_ref
```

MVP 默认参数:**世界地图** `grid 64×48 / cell_distance = 10 km`,**城市地图** `grid 64×64 / cell_distance = 2 m`,二者 `presentation.theme = parchment`,`layers = [terrain, road, building, marker]`(4 类,见 R-SAMPLE-02),`terrain_data.encoding = sparse`,`default = {type: plain, move_cost: 1, buildable: true}`。

### 2.3 Layer(图层)

> Layer 文件以独立 YAML 形式存放在 `layers/<kind>.yaml`,字段定义见 `docs/数据模型规格_v0.1.md` §4 + `docs/图层系统规格_v0.1.md`。
> Map 通过 `layers: - ref: ../layers/<file>.yaml` 引用;不允许在 Map 内 inline。

```yaml
layer:
  id: string              # 必填,工程内唯一
  name: string            # 必填,非空
  type: "terrain" | "road" | "building" | "marker"   # 必填(MVP 4 类)
  visible: bool           # 必填
  opacity: number         # 必填,0..1
  z_order: int            # 必填,MVP 推荐 0(terrain) / 10(road) / 20(building) / 30(marker)
  locked: bool            # 可选,MVP 默认 false
  default_style: "flat" | "line" | "solid" | "ring"   # 可选,MVP 默认值随 type 变化
```

MVP 关心:`terrain` / `road` / `building` / `marker` 四类;面板只切显隐,不切不透明度。

### 2.4 TerrainType(地形)

MVP 支持 11 种:

| 值          | 显示色       |
| ---------- | --------- |
| `plain`    | `#c2b280` |
| `mountain` | `#7a6f5c` |
| `forest`   | `#3f7a3f` |
| `hill`     | `#8a7a4a` |
| `water`    | `#3a6ea5` |
| `desert`   | `#d9c47a` |
| `swamp`    | `#5a6a3a` |
| `tundra`   | `#cfd6e0` |
| `basin`    | `#7a8aa6` |
| `coast`    | `#a8c3d6` |
| `river`    | `#5e8fc4` |

完整地形字段见 `docs/存储与目录规范_v0.1.md §3.4`。MVP `default = {type: plain, move_cost: 1, buildable: true}`,**不携带** `height` 字段。

### 2.5 Building(建筑,MVP 占位)

> **MVP 投影**：建筑以 **inline 列表** 登记在城市地图 YAML 内(`buildings: [...]` 字段)。
> 数据模型规格 §2 定义的 `building_refs`(跨文件 ref 到独立 `buildings/<id>.yaml`)推迟到 M5 落地。
> MVP 字段:`id`(8 字符 UUID 短串)/ `type` / `at` / `map_id`。

```yaml
building:
  id: string              # 必填,工程内唯一(8 字符 UUID 短串;R-BUILD-03 自动生成)
  type: "house" | "tower" | "wall_gate"   # 必填;MVP 仅 3 种占位
  at: [x, y]              # 必填,城市地图网格坐标(0 ≤ x,y < grid.width/height)
  map_id: string          # 必填,引用父城市地图 id
```

MVP 只放 3 种占位建筑;`at` 是城市地图**网格坐标**,非世界地图坐标,也非米。

### 2.6 YAML 示例(城市地图)

```yaml
# 极光港(city_kael)MVP 示例
# 字段定义见 `docs/数据模型规格_v0.1.md` §2;MVP 投影见本规范 §2.2。
id: map_city_kael
name: 极光港
type: city

grid:
  width: 64
  height: 64
  cell_distance: 2
  distance_unit: m
  base_unit: m
  height_unit: m
  origin: { x: 0, y: 0 }
  show: true
  snap: true
  scale_label: "1 格 = 2 m"

time:
  default_granularity: hour
  min_granularity: hour
  display_format: "YYYY-MM-DD HH:mm"
  inherit_calendar: true
  allow_fuzzy: true

presentation:
  default_mode: edit
  view_mode: 2d
  theme: parchment
  show_scale_bar: true
  show_coordinates: true
  show_minimap: false

terrain_data:
  encoding: sparse
  default:
    type: plain
    move_cost: 1
    buildable: true
  cells_ref: maps/city/city_kael.terrain.yaml
  cells: []

# 4 类图层(参见 R-SAMPLE-02):terrain / road / building / marker
layers:
  - ref: ../layers/terrain.yaml
  - ref: ../layers/road.yaml
  - ref: ../layers/building.yaml
  - ref: ../layers/marker.yaml

city_refs: []

# MVP 投影:建筑以 inline 列表登记(数据模型规格 §2 的 building_refs 推迟 M5)。
buildings:
  - id: b_kael_house_01
    type: house
    at: [20, 30]
    map_id: map_city_kael
  - id: b_kael_tower_01
    type: tower
    at: [32, 32]
    map_id: map_city_kael
```

### 2.7 字段对照表(spec.md §2.2 ↔ 数据模型规格 §2)

| spec.md §2.2 字段                                                                                      | 数据模型规格 §2 字段                                                                                         | 说明                          |
| ---------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- | --------------------------- |
| `id` / `name` / `type`                                                                               | `id` / `name` / `type`                                                                               | 完全一致                        |
| `grid.{width,height,cell_distance,distance_unit,base_unit,height_unit,origin,show,snap,scale_label}` | `grid.{width,height,cell_distance,distance_unit,base_unit,height_unit,origin,show,snap,scale_label}` | 完全一致                        |
| `time.{default_granularity,min_granularity,display_format,inherit_calendar,allow_fuzzy}`             | `time.{default_granularity,min_granularity,...}`                                                     | 完全一致                        |
| `terrain_data.encoding: "sparse"`                                                                    | `terrain_data.encoding: "sparse"\|"grid"\|"chunked"`                                                 | MVP 仅取 sparse               |
| `city_refs`                                                                                          | `city_refs`(id / name / x / y / entry / map\_ref / population / faction / notes)                     | 完全一致                        |
| `layers: - ref: ../layers/<file>.yaml`                                                               | `layers: - ref: layers/<file>.yaml`(数据模型规格 §4 描述)                                                    | 引用规则一致,ref 相对工程根            |
| `buildings: - id / type / at / map_id`(inline)                                                       | `building_refs: - ref: buildings/<id>.yaml`                                                          | **MVP 投影**:inline vs M5+ 外键 |
| —                                                                                                    | `parent_map` / `anchor` / `extent` / `markers` / `height_data` / `timeline_sync`                     | **MVP 不实现**:按规格默认值或不写入 YAML |

***

## 3. ADDED Requirements(本规范新增)

### 3.1 工程加载与保存

* **AC 工程加载**(R-LOAD-01):启动后,顶栏「打开示例工程」点击后,**5 秒内** 完成对 `examples/sample_world/project.yaml` 的解析,主场景切到第一张地图。

  * **Then**:主场景展示该地图的画布、图层面板、检视器、状态栏四区。

* **AC 工程保存**(R-SAVE-01):点击「保存」后,所有被修改的地图 / 图层 / 建筑文件**按原路径回写**;重新打开后字段一致(逐字段比对,允许键序差异)。

* **AC 「另存为」**(R-SAVE-02):支持把当前工程另存到 `examples/sample_world/` 之外的目录(便于 D7 / D8 写到临时目录做对照)。

### 3.2 视图与坐标

* **AC 视图类型**(R-VIEW-01):MVP 只支持 2D 俯视正交视图;`view_mode = "2d"` 是唯一字面量。

* **AC 坐标转换**(R-VIEW-02):世界坐标 ↔ 像素坐标 ↔ 网格坐标三向互转在 `WorldView.Render.CoordTransform` 静态类中集中,所有渲染层必须调用,不得直接做乘法。

* **AC 缩放范围**(R-VIEW-03):滚轮缩放范围 0.25× \~ 4.0×,步长 1.2×;钳制在视口内,无溢出 / 反向。

### 3.3 图层面板

* **AC 图层显隐**(R-LAYER-01):勾选切换 `layer.visible` 时,对应 `TileMapLayer` 的 `Visible` 同步切换,**刷新时间 ≤ 16 ms**(一个帧)。

* **AC 图层顺序**(R-LAYER-02):图层顺序与 `project.yaml` 中 `layers:` 列表顺序一致,顺序不可拖动。

### 3.4 圆形笔刷

* **AC 落点节流**(R-BRUSH-01):同一笔刷连续落点之间间隔\*\*≥ 100 ms\*\*,落点超过节流阈值时只提交最后一次;通过 `Editor.BrushController` 集中计时。

* **AC 笔刷覆盖范围**(R-BRUSH-02):以鼠标命中格为中心,半径 `r` 格(默认 1)的**欧几里得圆盘**(`sqrt(dx²+dy²) ≤ r`)内所有格都被覆盖;空过格不报错。MVP 默认半径 = 1,即「十字 + 中心」5 格。

* **AC 笔刷重绘**(R-BRUSH-03):笔刷落点后,`TileMapLayer` 对应 `dirty_rect`(包围盒 + 1 格边带)内格子在**一个帧内** 重绘完成。

### 3.5 撤销与重做

* **AC 命令模式**(R-UNDO-01):所有会改变文档状态的操作必须通过 `IEditorCommand` 接口(`Apply()` / `Revert()`)实现;UndoStack 容量 ≤ 200,超出后 FIFO 丢弃最旧命令。

* **AC 快捷键**(R-UNDO-02):`Ctrl+Z` = 撤销,`Ctrl+Shift+Z` = 重做;连续撤销直到栈空为止,不可越过空栈。

* **AC 命令回放一致性**(R-UNDO-03):撤销 + 重做组合执行后,所有被影响字段必须**逐字段** 回到原值(测试:循环执行 `for _ in range(100): apply + undo` 不留下任何残留)。

### 3.6 城市下钻

* **AC 双击进入**(R-T-ZOOM-01):在**世界地图**的 marker 图层(或 `city_refs` 数据驱动的标记)上双击后,场景切到对应城市地图,`breadcrumbs` 增加一项「世界 / <城市名>」,顶栏面包屑可见。

* **AC 返回上一级**(R-T-ZOOM-02):点击面包屑中的「世界」或按 `Esc`,场景回到上一级地图(只回退一级,不直接回根)。

* **AC 最深层反馈**(R-T-ZOOM-03):当前地图已是最深层时双击 marker = 状态栏提示「已在最深层」,不弹错误对话框(参见 checklist.md G6)。

### 3.7 建筑放置

* **AC 工具切换**(R-BUILD-01):工具栏点击「建筑」后,鼠标光标变为「可放置」状态;非城市地图下该工具禁用。

* **AC 落点约束**(R-BUILD-02):只允许在 `terrain.buildable = true` 的格子上放置;否则提示「此处不可放置建筑」(状态栏文字反馈,无弹窗)。

* **AC 建筑登记**(R-BUILD-03):放置后,该建筑作为新对象加入当前 `map.buildings` 列表,`id` 自动生成(递增 UUID 短串,长足 8),`type` 取自工具栏。

### 3.9 校验器

* **AC 启动校验**(R-VAL-01):工程打开 → 渲染前,对整个工程做字段合法性扫描;第一处错误就停止,弹错误对话框(`ErrorDialog`),不允许部分渲染。

* **AC 错误消息**(R-VAL-02):错误对话框包含:

  1. 文件相对路径(`ref` 形式);
  2. 字段路径(点号分隔,如 `map.grid.cols`);
  3. 错误类型(`required_missing` / `enum_out_of_range` / `ref_not_found` / `id_duplicate` / `type_mismatch`)。

* **AC 错误恢复**(R-VAL-03):校验失败时,顶栏保持可见,用户可点「关闭」回到空场景;不可强制进入「半渲染」状态。

### 3.10 示例工程

* **AC 示例存在**(R-SAMPLE-01):`examples/sample_world/project.yaml` 必须随仓库提供,可在 5 秒内打开(D1 验收)。

* **AC 示例内容**(R-SAMPLE-02):示例工程至少包含:1 张世界地图(`grid 64×64`)+ 1 张城市地图(`grid 64×64`)+ terrain / road / building 三个图层 + 1 个城市下钻标记 + 2 个建筑占位。

***

## 4. MODIFIED Requirements

无。

***

## 5. REMOVED Requirements

无。

***

## 6. 实现架构

### 6.1 仓库布局(Godot / C# 栈)

```
world-view/
├── project.godot              # Godot 工程根(主场景配置、dotnet assembly_name)
├── WorldView.sln              # .NET 解决方案
├── WorldView.csproj           # C# 项目(Godot.NET.Sdk/4.7.0,TargetFramework=net8.0)
├── scenes/                    # Godot 场景文件(.tscn),节点文件引用
│   ├── Main.tscn              # 主场景(Control 根)
│   ├── WorldCanvas.tscn       # 画布(Camera2D + TileMapLayer × N + GridOverlay)
│   ├── LayerPanel.tscn        # 图层面板
│   ├── ToolPanel.tscn         # 工具栏
│   ├── InspectorPanel.tscn    # 检视器
│   ├── StatusBar.tscn         # 状态栏
│   └── TopBar.tscn            # 顶栏(包含 FileMenu + Breadcrumbs)
├── scripts/                   # C# 脚本,挂到场景节点上
│   ├── Domain/                # 纯领域逻辑(无 Godot.* 依赖)
│   │   ├── World.cs           # World 根模型
│   │   ├── MapDoc.cs          # Map 文档(加载/保存/校验)
│   │   ├── LayerDoc.cs        # Layer 文档
│   │   ├── Terrain.cs         # 地形枚举 + 默认值
│   │   ├── Building.cs        # 建筑模型
│   │   └── Validation/        # 校验器
│   │       ├── ValidationError.cs
│   │       ├── ValidationErrorCode.cs
│   │       └── ProjectValidator.cs
│   ├── Storage/               # YamlDotNet 读写
│   │   ├── ProjectLoader.cs
│   │   ├── ProjectSaver.cs
│   │   └── YamlSchemas.cs     # 类 ↔ YAML 映射
│   ├── Editing/               # 命令模式 + 撤销栈 + 笔刷
│   │   ├── IEditorCommand.cs
│   │   ├── UndoStack.cs
│   │   ├── ApplyBrushCommand.cs
│   │   ├── CreateCityMapCommand.cs
│   │   ├── PlaceBuildingCommand.cs
│   │   └── BrushController.cs # 节流 + dirty_rect
│   ├── Render/                # Godot 节点与绘制
│   │   ├── CoordTransform.cs  # 世界 ↔ 像素 ↔ 网格
│   │   ├── WorldCanvas.cs     # Camera2D + TileMapLayer 容器
│   │   ├── TerrainLayer.cs    # TileMapLayer 子类
│   │   ├── RoadLayer.cs
│   │   ├── BuildingLayer.cs
│   │   ├── GridOverlay.cs     # Line2D 网格叠加
│   │   └── TerrainPalette.cs  # 11 色 → TileSet
│   ├── Ui/                    # Godot Control 节点
│   │   ├── Main.cs            # 主场景脚本
│   │   ├── TopBar.cs          # 顶栏 + FileMenu + Breadcrumbs
│   │   ├── LayerPanel.cs
│   │   ├── ToolPanel.cs
│   │   ├── InspectorPanel.cs
│   │   ├── StatusBar.cs
│   │   ├── ErrorDialog.cs     # 校验错误弹窗
│   │   └── BuildingPlaceDialog.cs # 建筑放置弹窗
│   └── Editor/                # 编辑器协调器
│       ├── EditorContext.cs   # 当前工程 + 地图 + 选中状态
│       └── EditorController.cs # 命令分发 / 撤销 / 重做
├── crates/                    # Rust 可选加速层(M1 不参与,空骨架保留)
├── examples/
│   └── sample_world/          # 示例工程
│       ├── project.yaml
│       ├── maps/
│       ├── layers/
│       ├── assets/
│       └── README.md
├── docs/                      # 设计文档
├── _tools/                    # 辅助脚本(make_icons.py 等)
├── AGENTS.md
├── README.md
└── .gitignore
```

### 6.2 数据流(替代旧 Zustand 分层)

**旧栈**用 Zustand 分 4 个 store(`useDomainStore` / `useEditingStore` / `useUiStore` / `useErrorStore`)。
**新栈**统一为 `WorldView.Editor.EditorContext` 单例\*\*,挂在 `/root/Editor` 节点上,Godot 信号(`[Signal]` 属性)承担「通知」职责:

* `Domain.*` → 纯 C# 数据模型,无 Godot 依赖,可在 Godot 之外做单元测试。

* `Editor.EditorContext` → 持有当前 `World`(工程根) + `MapDoc`(当前地图) + 选中状态,发出 `Domain/KeyChanged`、`MapChanged`、`SelectionChanged` 等信号。

* `Render.*` → 监听 `EditorContext` 信号,**只渲染** 当前地图;不直接持有数据。

* `Ui.*` → 监听 `EditorContext` 信号 + 用户输入事件,调用 `EditorController` 派发命令。

* `Editing.*` → 命令模式与撤销栈,不依赖 Godot 信号,但通过 `EditorContext` 触达数据。

**核心原则:领域层不引用** **`Godot.*`** **命名空间。** 所有 Godot 概念(`Node2D` / `TileMap` / `TileSet` / `Control` / `[Signal]`)只出现在 `Render/Ui/Editor` 三个目录。

### 6.3 命令模式

| 命令                     | 触发            | 影响的字段                             |
| ---------------------- | :------------ | :-------------------------------- |
| `ApplyBrushCommand`    | 笔刷落点          | `MapDoc.TerrainData` 中命中格的 `type` |
| `CreateCityMapCommand` | 双击城市标记 → 进入城市 | `World.MapCrumbs` 增加一项            |
| `PlaceBuildingCommand` | 在城市地图放置建筑     | `MapDoc.Buildings` 列表新增一项         |

`UndoStack`(`scripts/Editing/UndoStack.cs`)FIFO 上限 200 条;满后从最旧端丢弃。
`UndoStack.WithCommand(IEditorCommand)` 会执行 `command.Apply()`,失败则不记录。
`UndoStack.Redo()` 会重做栈顶命令;`UndoStack.Undo()` 会回退。

### 6.4 性能纪律

* 笔刷落点 100 ms 节流(`Editor.BrushController` 集中计时,见 R-BRUSH-01)。

* `dirty_rect` 矩形包围盒 + 1 格边带(`Render.TerrainLayer.QueueRedraw(Rect2I)`)。

* 视图裁剪:**视口外的格子不调用** **`SetCell`**,只画视口内的(参见 `Render.TerrainLayer.GetVisibleCellBounds()` → `WorldView.Render.CoordTransform.WorldRectToCellRect`)。

* TileSet 单一来源:`Render.TerrainPalette` 静态类根据 terrain 类型返回 `Vector2I` 索引;切换地形不重建 TileSet。

### 6.5 测试策略

* 领域层 + 编辑层:`dotnet test` 运行 `tests/` 下的 xUnit 套件(无 Godot 依赖,可 CI 跑)。

* 渲染层 + UI 层:Godot 4.7.2 mono 自带的 `SceneTree.ProcessFrame` 事件做手动 smoke test(D1–D8 录屏,见 §7)。

* 视觉断言:D7 / D8 的「保存 + 重新打开」用「逐字段比对 YAML 文本」做(去除注释差异、信任差异无关)。

***

## 7. 验收 demo(MVP D1–D8)

> 演示环境:Godot 4.7.2 mono 编辑器中按 F5 运行,主场景 `res://scenes/Main.tscn`。

### D1 — 启动 + 打开示例工程

操作:启动 → 顶栏「文件 → 打开示例工程」 → 5 秒内主场景切到 `world_kael`。
验收:画布、图层面板、检视器、状态栏四区显示正确;检视器展示 `world_kael` 元数据。

### D2 — 切地形类型与笔刷

操作:工具栏选中「笔刷」→ 顶栏下拉选「forest」 → 在画布中画 20 格。
验收:对应格子变为 forest 色(参见 R-BRUSH-02 / R-BRUSH-03);状态栏显示当前鼠标坐标。

### D3 — 撤销

操作:`Ctrl+Z` 一次。
验收:刚画的 20 格全部还原为 `plain`(参见 R-UNDO-01 / R-UNDO-03)。

### D4 — 重做

操作:`Ctrl+Shift+Z` 一次。
验收:刚才撤销的 20 格恢复为 forest(R-UNDO-02)。

### D5 — 城市下钻

操作:在画布中双击城市标记。
验收:场景切到对应城市地图(`city_kael`);顶栏面包屑显示「世界 / Kael」(参见 R-T-ZOOM-01 / R-T-ZOOM-02)。

### D6 — 建筑放置

操作:工具栏切到「建筑」→ 选 `house` → 在 buildable 格子上点击。
验收:建筑出现在点击位置;弹「建筑占位」对话框(参见 R-BUILD-01 / R-BUILD-02 / R-BUILD-03)。

### D7 — 保存 + 重新打开

操作:顶栏「文件 → 保存」 → 关闭工程 → 重新打开。
验收:`map.buildings` 包含刚才放置的建筑;逐字段比对 `project.yaml` / `maps/<id>.yaml` 内容(允许键序差异)。

### D8 — 校验失败

操作:手动编辑示例工程的 `maps/city_kael.yaml`,把 `grid.cols` 改成 `-1`(非法) → 启动。
验收:弹错误对话框,显示:`maps/city_kael.yaml / map.grid.cols / enum_out_of_range`(参见 R-VAL-01 / R-VAL-02 / R-VAL-03)。

***

## 8. 风险与回退

| 风险                                         | 影响          | 回退策略                                                      |
| ------------------------------------------ | ----------- | --------------------------------------------------------- |
| Godot 4.7.2 mono 在 Windows 上的 .NET 8 兼容性   | 主流程无法跑      | 锁定 `Godot.NET.Sdk/4.7.0` + `net8.0`,CI 上跑 dotnet build 验证 |
| TileMapLayer 的 `SetCell` 性能不够              | J2 量化断言失败   | 退回 `MapCanvas.setCell` 显式循环 + dirty\_rect 裁剪              |
| 笔刷节流不到位                                    | 一帧落点过多,Drop | F=20 帧节流(`Engine.MaxFps` 钳制) + `BrushController` 集中节流     |
| YamlDotNet 16.3.0 对 nullable value 类型 < 严格 | 校验失败被误判     | `YamlSchemas` 集中收口,所有 nullable 字段给默认值                     |
| 示例工程缺图块 / 图层引用错                            | D1 失败       | D1 录制前在 `examples/sample_world/` 内做完整性自检                  |

***

## 9. 范围纪律

* **不得**在 MVP 中引入 `chunked` 编码 / 室内下钻 / 时间轴 / 运输 / 贴合 / 视觉增强 / 插件。

* **不得**在 `Domain/` 目录下 `using Godot;`(领域层纯逻辑,无 Godot 依赖)。

* **不得**用 `..` 或绝对路径写 `ref`;只允许相对工程根。

* **不得**让 `id` 与 `ref` 混用(同类型用 `id`,跨文件用 `ref`)。

* **不得**跳过命令模式直接修改文档状态(必须通过 `IEditorCommand`)。

***

## 10. 变更记录

| 日期         | 版本   | 变更                                                                                                                                                                                                                                                                                       |
| ---------- | ---- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-10-07 | v0.1 | 初版,Tauri + Rust + TS + PixiJS 栈(M1 基线)                                                                                                                                                                                                                                                   |
| 2026-10-07 | v0.2 | 重大重写:**栈切换为 Godot 4.7.2 mono + C# (.NET 8) + YamlDotNet**。移除 `crates/world-view-core/`、`crates/world-view-io/`、`src/`、`src-tauri/`、Zustand、PixiJS、Tauri commands;新增 `project.godot`、`WorldView.sln`、`WorldView.csproj`、`scenes/`、`scripts/`。验收 demo 同步切换到 Godot 节点粒度;性能断言保留(J2 量化 4915)。 |

