# 地图大小配置 + 地形笔刷 设计规格（v0.2 · delta 版）

> Spec 路径: `docs/superpowers/specs/2026-10-08-map-size-and-terrain-brush-design.md`
> 状态: Draft v0.2（待用户审核）
> 路径依据: brainstorming skill 默认路径（2026-10-08 用户确认）
> 范围依据: 本轮只做「地图大小 UI + 地形笔刷半径」；LOD 缩放跳转 / 跨地图进入建筑 / 楼层设计另起 spec
>
> **v0.2 变更（2026-10-08 评审后全量改写）**：v0.1 把已实现能力当作待建项描述，且预设档位与配色表与项目真源冲突。v0.2 改为 **delta 规格**：先在 §2 列出仓库现状（含 `file:line`），此后各节**只写本轮净改动**。改写依据见 §11。

---

## 1. 目标与范围

### 1.1 目标

让用户用自然语言（公里、米）描述**笔刷半径**与**地图物理范围**，配合已有的地形调色板完成「打开地图 → 选地形 → 拖笔刷」的编辑闭环。

### 1.2 In scope（本轮净改动，共 3 项）

1. **笔刷半径输入改为物理单位**，并放开现有 1–8 格上限（§4.2 / §5.2）。
2. **地图尺寸与分辨率面板**（新建），含反算、实时预览、应用/取消（§4.3）。
3. **`ResizeMapCommand`**：地图尺寸变更进撤销栈 + 缩小前的越界统计确认（§5.4）。

配套：纯函数换算助手 `MapSizing`（§3.3）、对应单测（§7）。

### 1.3 Out of scope

| 项 | 理由 |
|---|---|
| LOD 缩放驱动的跨地图跳转 | 用户提出，2026-10-08 拆分另起 spec（m00062） |
| 点击建筑进入室内 + 选择楼层设计 | 同上 |
| 城市显示层级筛选 | 同上 |
| **新建地图向导**（选类型 / 命名 / 落盘） | 需先扩展 `MapType`（现仅 `World`/`City`，见 §2）与 `project.yaml` 的 `map_index` 写入；本轮只做**已加载地图**的尺寸配置（§4.4） |
| 笔刷非圆形模式（lasso / magic_wand / path / fill） | 《地图编辑器规格》§1.1；
 MVP 只做 round（已有实现见 §2） |
| 笔刷 `strength` / `smoothness` / `falloff` / `jitter` / `blend` | 同上，且 `smoothness`/`falloff` 在文档中仍标 `[待标定]` |
| **笔刷改高度 → `height_data`** | 《地图编辑器规格》§10 有此项，但 `MapDoc.cs:132` 明确 MVP 不实现 `height_data` |
| 画布上的实时笔刷预览圆环 | 手感优化，非闭环必需 |
| 11 种地形 swatch 的配色调整 | 已有唯一真源（§3.7），本轮不动 |
| 地形图标资源 | 用户选「纯文字 + 色块」（m00047），且已实现 |

### 1.4 已确认归属

跨地图跳转 + 楼层设计 → **未来 spec**：会扩展《数据模型规格》（新增城市显示层级字段、扩展 `building.floors` 语义为 N 个室内 sub-map 引用）。

---

## 2. 现状基线（已实现，本轮多数不动）

**本节是 v0.2 的核心修正**：v0.1 把下列能力全部写成待建，实际已在仓库中实现。凡本表标「不动」的，本轮**不得重写、不得新建同类**。

| 能力 | 已实现位置 | 本轮 |
|---|---|---|
| 地形笔刷命令（before/after + 混合地形还原） | `scripts/Editing/ApplyBrushCommand.cs:14`（`sealed class : IEditorCommand`）；`Apply()` 第 47-53 行 `_before.Count == 0` 守卫只捕一次原值；`Revert()` 第 58-65 行按类型分组还原 | **不动** |
| 命令模式撤销栈（容量 200 / FIFO 淘汰 / 新命令清 redo） | `scripts/Editing/UndoStack.cs:20`（`Capacity = 200`）、`:42-45`（`RemoveFirst` 淘汰）、`:47`（`_redo.Clear()`） | **不动** |
| 笔刷节流（同一格 100ms 只落一次笔） | `scripts/Editing/BrushController.cs:13`（`ThrottleMs = 100`） | **不动** |
| 笔刷形状（圆形/方形单元格集） | `scripts/Editing/BrushShapes.cs`、`BrushType.cs` | **不动** |
| 工具状态（`Selected`/`Brush`/`Building`）、`BrushRadius`（int，格） | `scripts/Editor/ToolState.cs:7-17`、`EditorContext.cs:118-126`（`BrushRadius` 第 123 行，默认 2） | 部分改（§4.2） |
| 地形调色板：11 色块横向排列、单选高亮、单击切换、可拖到画布 | `scripts/Ui/Palette/ToolStrip.cs:96-113`（11 个 `TerrainSwatch`）、`:404-414`（`OnSwatchPressed` → `SetPaintTerrain` + `SetTool(Brush)`）、`:416-422`（`SyncTerrain` 单选同步）；`scripts/Ui/Palette/TerrainSwatch.cs:53-62`（`SetSelected`）、`:65-75`（`_GetDragData`） | **不动** |
| 地形中文显示名 | `scripts/Ui/Palette/TerrainSwatch.cs:20-33` + `:41-42`（`NameOf`，UI 名唯一真源） | **不动** |
| 地形配色唯一真源 | `scripts/Domain/Terrain.cs:58-72`（`TerrainDefaults.ColorHex`） | **不动** |
| 配色只读共享表（`Render/` 唯一白名单例外） | `scripts/Render/TerrainPalette.cs:18`（hex→`Color` 预解析，注释第 14-16 行「颜色**不另立**」） | **不动** |
| 地形读写（含 sparse 剔除 default 值、(y,x) 升序） | `EditorContext.cs:307-328`（`GetTerrainAt`）、`:334-388`（`SetTerrainCells`，剔除见第 365-368 行，排序见第 375-378 行） | **不动** |
| 工具行三行骨架与 row 顺序 | `.trae/specs/ui-redesign-simgame/spec.md:127-129`：`row 0 timeline_row 28px` → `row 1 build_row flex(152px)` → `row 2 tool_row 20px` | 在 `row 2` 内改（§4.2） |
| 单格距离的只读展示 | `scripts/Ui/Inspector.cs:103`（`单格距离：{CellDistance} {DistanceUnit}`） | 扩展（§4.3） |
| 校验器：网格尺寸、越界判据、大网格警告 | `scripts/Domain/Validation/MapValidator.cs:35-37`（`GridSizeMissing` error）、`:47`（`GridTooLargeSparse` warning）、`:59`（`GridOutOfBounds` error）、`:129`（`BuildingOutOfBounds` error） | **不动**，但 `ResizeMapCommand` 复用它做预检（§5.4） |
| `grid` 字段（含 `cell_distance: double` / `distance_unit` / `base_unit` / `height_unit` / `scale_label`） | `scripts/Domain/MapDoc.cs:19-33`（`GridSpec` record） | **不动** |
| 地图类型枚举 | `scripts/Domain/MapDoc.cs:9-13`：**仅 `World` / `City`** | 不动（决定 §3.4 表口径） |
| 动态创建对话框的先例 | `scripts/Editor/EditorController.cs:144-155`（`CreateFileDialog`）、`:216` | 复用先例（§4.3） |

**尚不存在**（本轮若需要即为新建）：新建地图向导、地图设置面板、`ResizeMapCommand`、任何 `grid.cell_distance` 的**写入**型 UI。

---

## 3. 数据模型

### 3.1 不修改字段原则

**不新增、不修改任何 `Domain/` 字段。** 沿用《数据模型规格》§2.2 `grid` + §3.1 地形枚举 + §3.2 cell 编码。`GridSpec`（`MapDoc.cs:19-33`）已含本轮所需全部字段，因此**无 schema 变更、无 YAML 迁移、无校验器改动**。

### 3.2 单位与归一规则

- `grid.cell_distance` 的单位 = `grid.distance_unit`（开放取值 km/m/cm/mm/mile/ft/里/丈/尺 或自定义）。
- **任何比较与计算前，先把长度归一到 `grid.base_unit`**（《数据模型规格》§1）。UI 展示仍按 `distance_unit`。
- 例（世界地图 `distance_unit=km` / `base_unit=m` / `cell_distance=50`）：5000 km → 5,000,000 m；单格 50 km → 50,000 m；格数 = 5,000,000 / 50,000 = 100。
- `height_unit` / `scale_label` 本轮不写；`scale_label` 待 P0-4 视觉规范（§8）。

### 3.3 反算规则

新增纯函数助手 `scripts/Editor/MapSizing.cs`（静态、不依赖 `Godot.*`，可脱离引擎单测；与 `UndoStack`/`BrushShapes` 同属可单测层）：

| 输入 | 输出 | 规则 |
|---|---|---|
| 横向物理长度 `width_phys` | `grid.width` (int) | `CellsForLength(width_phys, cell_distance)` |
| 纵向物理长度 `height_phys` | `grid.height` (int) | `CellsForLength(height_phys, cell_distance)` |
| 分辨率 `cell_distance` | `grid.cell_distance` (double) | 取预设档值或自定义值（§3.4） |
| 笔刷半径 `radius_phys` | 运行时 `radius_cells` (int) | `CellsForLength(radius_phys, cell_distance)`，再按 §5.2 取上限 |

```
CellsForLength(length, cellDistance):
    if cellDistance <= 0: throw ArgumentOutOfRangeException   // 防御性；正常路径由 UI 拦截
    返回 max(1, (int)Math.Ceiling(length / cellDistance))
```

- `max(1, …)` 是必须的：否则 5 km 笔刷 ÷ 50 km/格 = 0 格。
- `length <= 0` 不下沉到本函数：UI 在提交前拦截并置灰「应用」（§4.3），避免把非法输入变成静默的 1 格。

### 3.4 预设档位（按地图类型）

**依据《地图类型规格》单格距离原文**（v0.1 的档位表与之不符，此处修正）：

| 地图类型 | `distance_unit` | `cell_distance` 档位 | 文档依据 | 代码可用性 |
|---|---|---|---|---|
| world 世界 | km | **10 / 25 / 50** | 《地图类型规格》L39「10–50 km」 | ✅ `MapType.World` |
| city 城市 | m | **1 / 2 / 5 / 10** | L67「1–10 m」 | ✅ `MapType.City` |
| region 区域 | km | 1 | L53「1 km」 | ⏳ 待扩展 `MapType` |
| street 街道 | m | 0.5 / 1 | L81「0.5–1 m」 | ⏳ |
| building 建筑 | m | 0.5 / 1 | L95「0.5–1 m」 | ⏳ |
| underground 地下 | m | 1 / 5 | L108「1–5 m」 | ⏳ |

- 每张表的末尾固定追加一项 **「自定义…」**：允许任意正数（含小数）。依据《数据模型规格》§2.2 `cell_distance`「支持任意小数（如 `1.32`），不限于整数」。
- **MVP 只有 `world` / `city` 两行可被选中**，其余四行是规格预留：UI 明细表可先写全，但下拉只在 `MapType` 扩展后才出现对应项（`MapDoc.cs:7` 注释已声明其余类型属文档层）。
- v0.1 的 `world 100 km` / `region 5,10 km` / `city 100,500,1000 m` / `street 5,10,20 m` / `building 2 m` 均**无文档依据且越出原文范围**，作废。

### 3.5 不变量

- `grid.width × grid.cell_distance ≥ width_phys`，且 `grid.width` 是满足该式的**最小整数**（`ceil` 保底，用户输入永不被截断）。
- 同理纵向。
- 实际物理范围可能略大于输入，UI 必须明示（如 5000 km ÷ 33 km/格 → `ceil(151.5)=152` 格 → 实际 5016 km）。
- 与《数据模型规格》§2.2「`cell_distance` 由数据定义决定」一致：YAML 存**格数 + 每格距离**，UI 提交时反算。

### 3.6 地形枚举与显示名（沿用）

`TerrainType` 11 值（`scripts/Domain/Terrain.cs:10-23`，顺序与《数据模型规格》§3.1 一致）：`Plain / Mountain / Forest / Hill / Water / Desert / Swamp / Tundra / Basin / Coast / River`。

**中文显示名以 `TerrainSwatch.NameOf`（`TerrainSwatch.cs:20-42`）为唯一真源**——注意与本轮 v0.1 稿不同：`Mountain = 山地`（不是「山」）、`Tundra = 苔原`（不是「冻原」）。本轮**不改名**（改名会同时触及已实现的 UI 与测试）。

### 3.7 配色（沿用，不另立）

**不新增配色表。** 颜色唯一真源是 `TerrainDefaults.ColorHex`（`scripts/Domain/Terrain.cs:58-72`）：Plain `#c2b280`、Mountain `#7a6f5c`、Forest `#3f7a3f`、Hill `#8a7a4a`、Water `#3a6ea5`、Desert `#d9c47a`、Swamp `#5a6a3a`、Tundra `#cfd6e0`、Basin `#7a8aa6`、Coast `#a8c3d6`、River `#5e8fc4`。

`Render/TerrainPalette.cs:44` 只做 hex→`Color` 预解析供主视图与小地图共用。**v0.1 稿另拟的 11 个 hex 作废**：那会造成第二套配色，正是 ui-redesign spec §12 列为中风险的「小地图与主视图调色板不一致」反模式。若将来要调色，改 `TerrainDefaults.ColorHex` 一处，UI 与渲染自动跟随。

---

## 4. UI 形态

### 4.1 地形调色板（已实现，本轮不动）

`scripts/Ui/Palette/ToolStrip.cs:96-113` 已在 `tool_row` 内实现：`地形` caption + 11 个 `TerrainSwatch`（横排，间距 2px），单击切为当前绘制地形并自动切到笔刷工具（`:404-414`），单选态高亮由 `SyncTerrain`（`:416-422`）统一维护，**点画布不清除**、切走再切回保留（符合 D9）。

**不新增子 row。** v0.1 稿的「直接加一个子 row：地形调色板」作废——`tool_row` 只有 20px（`ToolStrip.cs:50` `CustomMinimumSize = new Vector2(0f, 20f)`，且 `:205-208` 注释说明该行已需用 `ApplyCompactStyle` 紧凑样式盒才不被 `ClipContents` 裁掉）。

### 4.2 笔刷半径输入（本轮主改动）

**现状**：`ToolStrip.cs:76-92` 是 `笔刷` caption + `HSlider`（`MinValue=1, MaxValue=8, Step=1`）+ 格数标签。

**改为**：

- 物理单位输入（`SpinBox` 或 `LineEdit` + 校验），单位随 `grid.distance_unit` 切换（按 §3.4 的映射：world/region → km，city/street/building/underground → m；MVP 实际只会出现 world / city 两种）。
- 旁边派生显示：`= N 格`（N 由 `MapSizing.CellsForLength` 反算）。
- 保留一个「实际生效值」提示：输入超上限时（§5.2）显示截断后的真实物理半径，而不是静默改数。
- 控件类型（数字框 / 滑杆 / 两者并用）待 P0-4 视觉规范（§8），但**必须**能表达 >8 格的值。

**约束**：

- `EditorContext.BrushRadius` **保持 `int`（格）不变**（`EditorContext.cs:123`）。换算只在 UI 层做，域内统一以「格」为口径。
- `TerrainDragData.Radius`（`TerrainSwatch.cs:73` 传入）**语义仍是格**，所以拖放链路（ui-redesign T5 / G5「拖地形到地图 → 涂」）无需改动，也不会产生单位歧义。
- 无活动地图或 `GridSpec` 缺失时，退回纯格数输入（不阻塞编辑）。

```
BottomPalette（自上而下；真源 .trae/specs/ui-redesign-simgame/spec.md:127-129）
  row 0   timeline_row    28px    事件播放条
  row 1   build_row      152px    [minimap | build_palette]
  row 2   tool_row        20px    ← 本轮只改这一行内部
          [V][B][P] │ 笔刷 [ 200 ] km (= 4 格) │ 地形 ■×11 │ ⟲ ⟳ │ 坐标 │ 状态
                     └─ 替换原有的「1–8 格」HSlider ─┘
```

### 4.3 地图设置面板 —「尺寸与分辨率」段（本轮新建）

**新建** `scripts/Ui/MapSettingsDialog.cs`（`AcceptDialog` 派生），由 `EditorController` 动态创建挂载——复用 `EditorController.cs:144-155` 创建 `FileDialog` 的既有先例。**不扩展 `Inspector.cs`**：`Inspector` 是即改即显的检视面板（当前仅 `:103` 一行只读展示单格距离），而尺寸变更需要「编辑态 → 实时预览 → 应用/取消」的两阶段语义。

```
地图设置 · 尺寸与分辨率
─────────────────────────────────────────
横向 (km)：  [ 5000 ]        (100 格)
纵向 (km)：  [ 3000 ]        ( 60 格)
分辨率：     [ 50 km/格  ▼]   ← §3.4 档位 + 「自定义…」
─────────────────────────────────────────
预览：100 格 × 60 格 = 6,000 单元格
      实际物理范围 5000 × 3000 km（与输入一致）
      或：实际物理范围 5016 km（比输入大 0.3%）
─────────────────────────────────────────
                          [ 应用 ]  [ 取消 ]
```

- 分辨率变更时**保持物理范围不变、重算格数**（与 D1/D4「用户说物理单位」一致），并把实际范围实时回显。
- 用户输入 ≤0 或为负时，`应用` 置灰 + 行内提示（不弹错）。
- 「应用」把新 `GridSpec` 交给 `ResizeMapCommand`（§5.4）；「取消」丢弃编辑态。

### 4.4 新建地图向导（明确 out-of-scope）

v0.1 稿的「第二步：物理范围」向导骨架**不存在于仓库**（全 `scripts/` 搜索仅有 `EditorController.cs:146` 的 `FileDialog`，用于打开工程目录）。本轮不新建，理由：

1. `MapType` 只有 `World` / `City`（`MapDoc.cs:9-13`），向导的「世界/区域/城市/街道/建筑」5 选项有 4 个无法表示；
2. 新建地图还需同时定义文件命名、`project.yaml` 的 `map_index` 写入、`MapType` 扩展——独立话题。

本轮的需求覆盖方式是：**打开工程 → 地图设置面板**调整尺寸（含首次设定），完全满足「地图大小用户可设」。

---

## 5. 行为规则

### 5.1 笔刷绘制流程（复用既有实现）

```
鼠标按下 → 收集起点 (x_grid, y_grid)
鼠标移动 → BrushController.TryStamp() 节流（100ms）
          → 每帧生成一个 ApplyBrushCommand（center, radius_cells, terrain）
          → context.UndoStack.Push(cmd)（Push 内部自行 Apply，见 UndoStack.cs:11 注释）
鼠标释放 → 结束本笔
```

**不要新建 `BrushCommand` 类。** `ApplyBrushCommand` 已实现本流程所需的全部语义（§2 第 1 行），本轮对笔刷路径的唯一改动是**半径的来源**（由格数滑杆改为物理单位换算），命令本身不动。

v0.1 稿的 GDScript 伪代码段作废，三个理由：① 项目是 Godot 4.7.2 mono + **C#**（`AGENTS.md` P0-1），不写 GDScript；② 其中 `Ref` 是 Godot 3 API（Godot 4 为 `RefCounted`）；③ 缺少 100ms 节流约束。

**`before` 捕获时机**：命令生命周期内**只捕一次**，即首次 `Apply()` 时（`ApplyBrushCommand.cs:47-53` 已如此实现）。同一笔的后续帧是**独立的命令**，各自持有自己的 before 快照——因此实现者不得跨命令复用 `_before` 字典。

### 5.2 笔刷半径上限

现行上限是滑杆的 8 格（`ToolStrip.cs:79-80`），在 50 km/格 的世界地图下等于 400 km——**画不出 1000 km 尺度的地貌**，与本轮「物理单位输入」目标直接冲突。

- 新增 `MapSizing.MaxRadiusCells = 64`（暂定）：50 km/格 下 = 3200 km，512×512 世界地图的 1/8 边长。
- `radius_cells = clamp(ceil(radius_phys / cell_distance), 1, MaxRadiusCells)`。
- 超上限时 UI 显示实际生效值（如「已按最大半径 3200 km 生效」），不静默改数。
- 上限同时是性能参数：半径 64 的圆形单元格集约 12.8k 格/次落笔，而 `SetTerrainCells` 每次会重建整表字典（`EditorContext.cs:345-353`）。**最终上限值待性能实测标定**（§8），标定前若半径 >32 格则给出「大笔刷可能卡顿」提示。

### 5.3 撤销 / 重做

- **一格一戳，一戳一个命令**：一次拖动经 100ms 节流产生 N 个 `ApplyBrushCommand`，每戳各自 `UndoStack.Push`（`WorldCanvas.cs:405-437`，`:436` 入栈）。因此**一次拖动 = N 个撤销步**，与《地图编辑器规格》§1.3「操作粒度 = 一次落笔」**不一致**——该缺口的修法（笔触分组）见 spec 2 §6.2，本轮不在此处理。
- `Ctrl+Z` 还原该笔所有被修改格子的旧值；`Ctrl+Shift+Z` 重做。
- **容量与溢出已实现**：`UndoStack.Capacity = 200`，超限 FIFO 丢弃最旧（`UndoStack.cs:20,42-45`）。本轮不改容量；连续拖拽每分钟可产生约 600 条命令（100ms 节流），200 步约等于 20 秒连续绘制，属已知限制，记录在案不做优化。

### 5.4 地图尺寸变更：`ResizeMapCommand`（本轮新增的唯一命令类）

`MapDoc` / `GridSpec` 是不可变 record（`MapDoc.cs:19-33`），尺寸变更只能以 `map with { Grid = grid with { … } }` 表达，因此需要一个命令来承载「执行 + 反演」：

```
sealed class ResizeMapCommand : IEditorCommand
    构造(EditorContext ctx, string mapId, GridSpec newGrid)

    Apply():
      首次执行时快照：
        _oldGrid                 原 GridSpec
        _croppedCells            越界被裁掉的地形条目（TerrainCellEntry）
        _croppedBuildings        越界被裁掉的建筑（Building）
      写入：Grid = newGrid，TerrainData = 裁剪后，Buildings = 裁剪后

    Revert():
      写回 _oldGrid + _croppedCells + _croppedBuildings

    Description: $"调整地图尺寸：{oldW}×{oldH} → {newW}×{newH} 格"
```

**写入通道**：走 `EditorContext.ReplaceMap(map with { … })`（`EditorContext.cs:272`，公开方法），**不要**用 `SetTerrainCells`——它把越界格静默跳过（`EditorContext.cs:360-363`），因此**无法**用来裁剪越界条目，会静默失败。也不需要给 `EditorContext` 新增方法。

**缩小前的预检（不进撤销栈）**：用 `MapValidator` 已有的越界判据（`MapValidator.cs:59` `GridOutOfBounds` / `:129` `BuildingOutOfBounds`）先统计越界的**地形格数 N** 与**建筑数 M**，弹确认：

> 缩小到 80 × 60 格将丢弃 1,240 个地形格与 3 个建筑。继续？[继续] [取消]

**为什么必须裁剪而不能"保留越界数据"**：`MapValidator` 把越界地形格与越界建筑判为 **error**（`:59` / `:129`），保留会使工程校验失败（§2 表末行）。

**非破坏性保证**：被裁剪的数据完整存在于命令的 before 快照中，`Ctrl+Z` 可全量恢复——满足「非破坏编辑」原则；同时避免与校验器冲突。

### 5.5 持久化（MVP 口径）

- **本轮不动存储层。** `EditorContext.SetTerrainCells` 强制 `Encoding = "sparse"`、`CellsRef = null`、内联 `Cells`（`EditorContext.cs:380-385`，按 (y,x) 排序见 `:375-378`）——即 MVP **内联 sparse、不写独立 `<map>.terrain.yaml`**（`MapDoc.cs:79` 注释：「MVP 仅 `encoding: sparse`；`grid` 与 `chunked` 见 N04（M5+）」）。
- 落盘时机仍是显式 `SaveProject()`（`EditorContext.cs:197-217`），**不是**每次落笔实时写盘。
- 因此 v0.1 稿的「实时写回 `maps/.../<map>.terrain.yaml`」与「编码迁移提示」在 MVP **不成立，作废**。
- 编码阈值口径（供将来启用时参照，《存储与目录规范》§5.1.2）：`width × height ≤ 1024` → `grid`；`1024 < N ≤ 1024×1024` → `sparse`；`> 1024×1024` → **只提示、不自动切换**（chunked 未实现，不得默默改写）。代码侧对应 `MapValidator.cs:47` 的 `GridTooLargeSparse` 警告。
- **跟进项（另开 ticket，不在本轮）**：代码内联 sparse 与《存储与目录规范》§1「`main.terrain.yaml` 独立文件 + `cells_ref`」不一致；`TerrainFileLoader` 能读独立文件，保存路径却从不产出。

---

## 6. 与现有系统集成

| 依赖 | 真实状态 | 本轮动作 |
|---|---|---|
| `tool_row`（`row 2`，20px） | ✅ 已实现（`ToolStrip.cs:44-140`） | 在既有行内**替换半径控件**，不新增 row |
| 地形调色板 + 单选 | ✅ 已实现（`ToolStrip.cs:96-113`、`TerrainSwatch.cs:53-62`） | 不动 |
| 地形名 / 配色 | ✅ 唯一真源（`TerrainSwatch.NameOf`、`TerrainDefaults.ColorHex`） | 不动，且禁止另立 |
| 笔刷命令 + 节流 | ✅ 已实现（`ApplyBrushCommand.cs:14`、`BrushController.cs:13`） | 不动；仅改半径来源 |
| 撤销栈 | ✅ 已实现（`UndoStack.cs`，容量 200） | 不动；`ResizeMapCommand` 通过 `Push` 接入 |
| 地图设置面板 | ❌ **不存在**（`§1.5` 是《界面与交互规格》的**文档章节号**，不是已实现控件） | **新建** `MapSettingsDialog.cs`（§4.3） |
| 新建地图向导 | ❌ **不存在** | 明确 out-of-scope（§4.4） |
| 越界判据 | ✅ 已实现（`MapValidator.cs:59,129`） | 复用做缩小预检（§5.4） |
| 对话框动态创建先例 | ✅ 已实现（`EditorController.cs:144-155`） | 复用 |
| `Render/` 只读例外 | ✅ 仅 `TerrainPalette.cs` 一个文件（ui-redesign spec §10） | **本轮不新增任何 `Render/` 文件** |
| `Domain/` / `Storage/` | — | **本轮不改**（ui-redesign spec §9 N7 的同类约束） |

---

## 7. 测试要点

| 类别 | 用例 |
|---|---|
| 反算（格数） | 5000 km ÷ 50 km/格 → `grid.width = 100`；3000 km → 60 |
| 反算（向上取整） | 5000 km ÷ 33 km/格（自定义档）→ 152 格（`ceil(151.5)`）；不变量 `152 × 33 = 5016 ≥ 5000` ✅ |
| 反算（保底） | 5 km 笔刷 ÷ 50 km/格 → **1** 格（`max(1, …)`，不是 0） |
| 边界 | 物理长度 0 / 负数 → UI 拒绝且 `应用` 置灰；`cell_distance ≤ 0` 被 UI 拦截，不进 `MapSizing` |
| 笔刷格数 | 200 km 笔刷 ÷ 50 km/格 → 4 格；超上限输入 → 截断到 `MaxRadiusCells` 且 UI 显示实际生效值 |
| 单位切换 | world 地图单位标签为 km，city 地图为 m；换算按 §3.2 先归一到 `base_unit` |
| 撤销（笔刷，回归） | 一笔 100 格 → `Ctrl+Z` 全还原；连按 3 次撤销 3 笔；`Ctrl+Shift+Z` 全部重做 |
| 地形切换（回归） | 选中「山地」画一笔 → 切「森林」画另一笔 → 撤销顺序与绘制顺序相反 |
| 撤销（尺寸，新增） | 放大 → 撤销 → 回到原 `GridSpec`；**缩小并裁剪 N 格地形 + M 个建筑 → 撤销 → 地形与建筑全量恢复** |
| 尺寸预检（新增） | 缩小到会越界时弹出确认框，文案含 N/M 数字；取消则 `GridSpec` 不变 |
| 校验（回归） | 尺寸变更后 `MapValidator` 无 `GridSizeMissing` / `GridOutOfBounds` / `BuildingOutOfBounds` error |
| 持久化（回归） | 画一笔 → `SaveProject()` → 重载工程 → 地形保留；且落盘文件是**内联 `cells`**（不是独立 `.terrain.yaml`） |
| 集成（回归） | 切到「选择」再切回「笔刷」，保留上次选中地形；拖 `TerrainSwatch` 到画布仍以**格**为单位生效 |
| 编译期 | `scripts/Domain/**` 不引用 `Godot.*`（`AGENTS.md` §6.1）；`MapSizing` 无 Godot 依赖，可单测 |

---

## 8. 待 P0-4 视觉规范决定的项

| 项 | 缺省（可先实现） | 影响 |
|---|---|---|
| 笔刷半径控件类型 | 数字输入框 + 派生格数标签 | §4.2 |
| 半径上限 `MaxRadiusCells` | 64（暂定） | §5.2；需性能实测标定 |
| `MapSettingsDialog` 的视觉样式 | 跟随 `parchment_theme.tres`（**2026-10-09 更新**：原计划"暗色主题"已下线） | §4.3 |
| swatch 形状与描边 | 沿用现状（圆角矩形 + 1px 描边，选中 2px 白色） | §4.1 |
| `grid.scale_label` 是否随尺寸变更自动生成 | 不写（留空） | §3.2 |

---

## 9. 本轮净改动清单（实现顺序）

| # | 文件 | 动作 |
|---|---|---|
| 1 | `scripts/Editor/MapSizing.cs` | **新增**：`CellsForLength` / `LengthForCells` / `GridFromExtent` / `MaxRadiusCells`（纯静态，无 Godot 依赖） |
| 2 | `tests/WorldView.Tests/Editor/MapSizingTests.cs` | **新增**：§7 反算 / 取整 / 保底 / 边界用例 |
| 3 | `scripts/Editing/ResizeMapCommand.cs` | **新增**：`IEditorCommand` 实现 + 越界快照（§5.4） |
| 4 | `tests/WorldView.Tests/Editing/ResizeMapCommandTests.cs` | **新增**：裁剪 + 撤销全量恢复 + 预检 |
| 5 | `scripts/Ui/MapSettingsDialog.cs` | **新增**：尺寸与分辨率段 + 实时预览（§4.3） |
| 6 | `scripts/Ui/Palette/ToolStrip.cs` | **修改**：半径控件由 1–8 格滑杆改为物理单位输入 + 派生显示；接入 `MapSizing` |
| 7 | `scripts/Editor/EditorController.cs` | **修改**：按 `CreateFileDialog`（`:144-155`）先例动态创建并挂载 `MapSettingsDialog`；触发入口（菜单 / 工具行尾部按钮）待 P0-4 定位置 |
| — | 其余（`ApplyBrushCommand` / `UndoStack` / `BrushController` / `TerrainSwatch` / `TerrainPalette` / `TerrainDefaults` / `MapDoc` / `GridSpec` / `SetTerrainCells` / `MapValidator` / `Storage/**`） | **不动** |

---

## 10. 设计决策来源（traceability）

| 决策 | 来源 | message ref |
|---|---|---|
| D1 沿用现有字段，UI 反算 | 用户选 A 方案 | m00029 |
| D2 独立横向 × 纵向 | 用户选「独立的横向 × 纵向」 | m00032 |
| D3 `cell_distance` 从地图类型预设取 | 用户选「从地图类型预设里选」 | m00035 |
| D4 笔刷大小以物理单位为主 | 用户选「笔刷半径（公里/米）为主」 | m00038 |
| D5 笔刷 → 格数向上取整 | 用户选「向上取整」 | m00041 |
| D6 调色板水平嵌入 `BottomPalette.tool_row` | 用户选「水平调色板」 | m00044 |
| D7 地形显示用纯文字 + 色块 | 用户选「纯文字 + 色块」 | m00047 |
| D8 工作流先开地图后选工具 | 用户选「先开地图，后选工具」 | m00053 |
| D9 地形切换单选 + 跨笔刷持久化 | 用户选「上次选中状态保存」 | m00056 |
| 范围拆分（LOD / 楼层另起 spec） | 用户选「收收进同一轮（推荐，但要拆 spec）」 | m00062 |
| Spec 路径 `docs/superpowers/specs/` | 用户选「docs/superpowers/specs/...（推荐）」 | m00062 |
| D3 档位放宽为「预设 + 自定义」 | 评审发现预设与《地图类型规格》冲突，且 §2.2 允许任意小数 | m00146 §B2/G5 |
| 新建地图向导移出 scope | 评审发现 `MapType` 仅 World/City、向导不存在 | m00146 §B3/B9 |
| 配色/地形名改为沿用既有真源 | 评审发现 `TerrainDefaults.ColorHex` / `TerrainSwatch.NameOf` 已是唯一真源 | m00146 §B7/B8 |

---

## 11. v0.1 → v0.2 修正对照

| 编号 | v0.1 稿 | v0.2 修正 |
|---|---|---|
| B1 | §5.1/§6「新增 `BrushCommand`」 | 已存在 `ApplyBrushCommand`（`ApplyBrushCommand.cs:14`）；GDScript 伪代码作废 |
| B2 | §3.3 档位 world 100 / region 5,10 / city 100–1000m / street 5–20m | 全部按《地图类型规格》L39/L53/L67/L81/L95/L108 重写，并补 underground |
| B3 | 假定 6 种地图类型可选 | 明确 MVP 仅 `World`/`City` 两行可选（`MapDoc.cs:9-13`） |
| B4 | `tool_row` 画在最上、可加子 row | 行顺序改为 timeline/build/tool（ui-redesign spec §3.2），20px 内不新增 row |
| B5 | §5.4 实时写 `<map>.terrain.yaml` | MVP 内联 sparse（`EditorContext.cs:380-385`），不承诺独立文件 |
| B6 | §7「1024（sparse）」「仍 sparse→chunked」 | 阈值改为「≤1024 grid / ≤1024² sparse / >1024² 只提示不切换」 |
| B7 | §3.5 `mountain 山`、`tundra 冻原` | 改为 `山地` / `苔原`（`TerrainSwatch.cs:20-33`） |
| B8 | §3.6 另拟 11 色 | 作废，沿用 `TerrainDefaults.ColorHex` |
| B9 | §6「复用现有向导骨架 / 现有面板新增一段」 | 向导不存在、§1.5 是文档章节号；两者均为新建/移出 scope |
| G1 | 未提半径上限 | §5.2 新增上限策略（原 8 格上限封死了物理单位 UI） |
| G2 | 未提拖放链路单位 | §4.2 明确 `TerrainDragData.Radius` 仍为格 |
| G3 | 未提尺寸变更的撤销与越界 | §5.4 新增 `ResizeMapCommand` + 裁剪预检 + 非破坏保证 |
| G4 | 未提单位归一 | §3.2 新增归一规则 |
| G5 | 「只能从预设选」 | §3.4 增加「自定义…」档 |
| G6 | 伪代码未写 before 时机 | §5.1 明确「命令内只捕一次」 |
| G7 | 未提撤销栈容量 | §5.3 记录容量 200 + FIFO 溢出 |
| G8 | out-of-scope 漏 `height_data` | §1.3 补入 |
| G9 | §9「正文 8 节」 | 修正为 11 节 |
