# 世界生成与海岸线自然化 — 设计规格 v0.1

> 日期：2026-10-08
> 状态：待评审
> 主题：`world map generation + coastline refinement`
> 依赖规格：[`2026-10-08-map-size-and-terrain-brush-design.md`](./2026-10-08-map-size-and-terrain-brush-design.md)（下称 **spec 1**）
> 决策来源：见 §12 traceability

---

## 1. 目标与范围

### 1.1 目标

让用户**不必亲自关心海岸线怎么弯曲**就能得到一张自然的世界地图：

1. 新建世界地图时，一键按参数生成初稿（种子 + 陆地比例 + 曲折度），预览满意再落盘。
2. 之后用户用笔刷局部涂改时，边界**自动**保持自然（去碎岛 / 填碎海 / 补 `coast` 环）。
3. 同一种子 + 同一参数 → **同一张地图**（跨运行、跨机器可复现）。

### 1.2 In scope（本轮净改动）

| # | 改动 | 说明 |
|---|---|---|
| 1 | **世界生成器** | 纯 C# 流水线：大陆遮罩 → 多倍频噪声 + 域扭曲 → 分位数阈值 → 细化 → `coast` 环 |
| 2 | **共用细化 pass** | 生成器与笔刷共用同一个纯函数 `CoastlineRefiner.Refine(...)` |
| 3 | **`generation` 数据块** | `MapDoc` 追加可选块，落盘种子与参数（加法，旧工程可加载） |
| 4 | **新建世界地图对话框** | 兼作 **spec 1 §4.3 的「地图设置」**（同一个对话框的两态） |
| 5 | **笔触撤销分组** | `UndoStack` 支持批处理：`Ctrl+Z` 一次撤销整笔（笔触落点 + 细化） |
| 6 | **笔刷细化** | 抬起时对笔触并集包围盒跑一次细化，强度随笔刷尺寸缩放，可全局关闭 |

### 1.3 Out of scope（明确不做）

| 不做 | 原因 |
|---|---|
| **对已存在地图「重新生成」** | 需要把整张地图的替换承进撤销栈，且用户既有编辑会全部丢失。本轮生成只发生在**创建时刻**，见 §7.3 |
| 河流 / 侵蚀 / 落差 | 依赖高程，而地图级 `height_data` 在 MVP **未实现**（`MapDoc.cs:132` 明确列为未实现字段） |
| 生物群系（山/林/沙漠分布） | 本轮陆地一律 `plain` + `coast` 环，先把海岸线做对 |
| 紧凑编码（RLE `grid` / `chunked`） | 存储层本轮不动，体量用「警告 + 引导」处理（§3.3） |
| 渲染层方块感弱化（TileSet 边缘/拐角自动拼接） | 独立主题，见 §10 |
| 生成参数的可视化编辑（曲线、笔刷式塑形） | 本轮只有三个标量旋钮 |

### 1.4 与 spec 1 的关系

两份 spec **各自独立可实施、独立可验收**，但 spec 2 依赖 spec 1 的三项产物：`MapSizing`（物理长度 ↔ 格数）、`ResizeMapCommand`、以及「尺寸与分辨率」段的对话框。

**spec 2 显式改写的部分**：

| 被改写处 | 改写方式 |
|---|---|
| spec 1 §4.3「地图设置面板」 | 升格为**两态对话框**（新建 / 已存在地图），见 §7 |
| spec 1 §3.1「不修改字段原则」 | **放宽为「加法」**：允许给 `MapDoc` 追加可选块，仍不改动任何既有字段语义 |
| spec 1 §5.3「一笔 = 一个 `ApplyBrushCommand`」 | **更正**：一笔 = N 个落点命令 + 1 个细化命令，由笔触分组合成一个撤销单元，见 §6.2 |

---

## 2. 现状基线

### 2.1 已实现且本轮复用（不动）

| 能力 | 证据 | 本轮 |
|---|---|---|
| 地形枚举 11 值（含 `coast`） | `scripts/Domain/Terrain.cs:10-23` | 不动，直接复用 `coast` |
| 地形配色真源（11 色 hex） | `TerrainDefaults.ColorHex`，`Terrain.cs:58-72` | 不动，预览取色也用它 |
| 颜色 → `Color` 预解析 | `scripts/Render/TerrainPalette.cs:18,27-31,33-48` | 不动（`Render/` 唯一只读例外） |
| **地图自有默认地形** | `EditorContext.cs:343` `map.TerrainData.Default?.Type ?? TerrainType.Plain` | **本轮依赖它**：世界图设 `default: water` |
| sparse 稀疏化与 (y,x) 排序 | `EditorContext.cs:334-388`（`:365-368` 移除等于默认值的格、`:375-378` 排序） | 不动 |
| 整图替换通道 | `EditorContext.ReplaceMap`，`EditorContext.cs:272-286` | **生成与细化都走它**（不要逐格 `SetTerrainCells`） |
| 笔刷命令（before/after） | `scripts/Editing/ApplyBrushCommand.cs:14,40,45-56,58-65` | 不动（细化是它的**追加**，不是替换） |
| 100 ms 节流 | `scripts/Editing/BrushController.cs:10-14,23-34` | 不动 |
| 笔触按下/抬起边界 | `scripts/Render/WorldCanvas.cs:324`（`_painting = true`）/ `:344`（`_painting = false`） | **本轮用作细化触发点** |
| 笔触落点 → 命令 | `WorldCanvas.cs:405-437`（`:436` 每戳各自 `Push`） | 改为包进笔触分组 |
| 撤销栈容量与 FIFO | `scripts/Editing/UndoStack.cs:17,20,37-48` | 容量不动，**新增批处理能力** |
| 1px/cell 缩略图手法 | `scripts/Ui/Palette/MinimapSection.cs`（`ImageTexture` + `TextureFilter = Nearest`） | 预览沿用同一手法 |
| 动态创建对话框先例 | `scripts/Editor/EditorController.cs:42,144-155,216` | 对话框挂载沿用 |
| 校验器 | `scripts/Domain/Validation/MapValidator.cs:35-37,47,59,64,129` | 追加一条警告，不改既有判据 |

### 2.2 现状缺口（本轮必须补）

| 缺口 | 证据 | 补法 |
|---|---|---|
| **没有任何创建世界地图的入口** | 全仓无 `*Wizard*` / `*NewMap*` / `MapSettings*` 文件 | §7 |
| `MapType` 只有 `World, City` | `MapDoc.cs:9-13` | **不需要扩枚举**（`World` 已存在）——这是本轮可做的前提 |
| `MapDoc` 无生成参数位 | `MapDoc.cs:134-159` record 字段里没有 | §3.1 追加 `generation` |
| `TerrainDefaults` 无 `Water` 常量 | `Terrain.cs:43-73` 只有 `Plain`（`:52`） | §3.2 新增 |
| 撤销栈无分组能力 | `UndoStack.cs` 仅有 `Push/Undo/Redo/Clear` | §6.2 |
| 无确定性噪声 / 无确定性 PRNG | 全仓无相关文件 | §4.2（不用 `System.Random`，见 §4.2 理由） |

---

## 3. 数据模型

### 3.1 `generation` 块（**加法**）

`MapDoc` 追加一个可选字段，手绘地图为 `null`：

```yaml
generation:
  algorithm: coastline_v1      # 算法版本号；改变算法时递增，保证旧种子仍可复现旧世界
  seed: 739201
  land_ratio: 0.35             # 目标陆地占比，0.05–0.80
  ruggedness: 0.5              # 曲折度，0–1（映射到倍频数与域扭曲强度）
  min_island_cells: 8          # 细化：小于此面积的孤立陆块删除
  min_lake_cells: 6            # 细化：小于此面积的封闭水域填平
  erosion: 1                   # 细化：腐蚀/膨胀次数（0 = 关闭形态学处理）
```

**为什么必须存 `algorithm`**：若将来改进生成算法，同一个 seed 会产出不同的世界。存版本号才能让「同种子 = 同世界」在算法演进后仍然成立（旧版本代码路径保留即可）。

**为什么存细化参数而不是只存「曲折度」**：细化阈值是用户可覆盖的独立旋钮，若只存一个综合的 `ruggedness`，用户手工调过的阈值会在重生成时丢失。

**契约影响**：这是**加法**——`MapDoc` 是 record，`YamlDotNet` 对缺失键走默认值，因此**旧工程照常加载**（与 `GridSpec` 的无参构造同理，`MapDoc.cs:19-33`）。需同步**追加**到 `docs/数据模型规格_v0.1.md`（追加式，不改既有字段定义），以满足 AGENTS.md L58「不得污染既有字段定义」的纪律。

### 3.2 世界地图的 `TerrainData.Default` = `water`

世界图 65% 以上是海。`TerrainData.Default` 设为 `water` 后：

- **海洋零成本**：不占 `cells` 条目。
- **画水域 = 删除条目**：`EditorContext.cs:343` 读的是地图自己的默认值，`:365-368` 据此移除条目——语义自洽，不是 bug。
- 存储量 ≈ 陆地格数（1024×1024 @ 35% ≈ 37 万条；512×512 @ 35% ≈ 9.2 万条）。

**需补常量**：`TerrainDefaults` 目前只有 `Plain`（`Terrain.cs:52`），要新增 `Water => new(TerrainType.Water, <move_cost>, <buildable>)`。**具体数值待标定**（§10）：`buildable` 应为 `false`；`move_cost` 需与《数据模型规格》§3.1 一致，若该文档未定义则暂取不可通行的高值。

### 3.3 体量与「警告 + 引导」口径

网格大小是**推导结果**，不是用户输入：用户给「横向 5000 km + 50 km/格」，格数 = 100 由 `MapSizing.CellsForLength`（spec 1 §3.3）算出。

**策略（已定）**：不硬阻止超大网格，但预览区显著提示，并把分辨率档位往上推荐。

```
预览：1024 格 × 1024 格 = 1,048,576 单元格
      ⚠ 预计写盘约 37 万条记录（约 13 MB），保存与加载会明显变慢。
        建议把分辨率调粗到 25 km/格（得到 512 × 512）。
```

**默认世界档（建议值，评审时确认）**：`512 × 512` @ `25 km/格` = **12,800 × 12,800 km**，与地球直径（12,742 km）同量级，且 `25 km` 落在《地图类型规格》L39 世界图的 `10–50 km` 区间内。陆地 35% ≈ 9.2 万条 ≈ 3 MB。

**本轮不实现紧凑编码**：`chunked` 未实现（`MapDoc.cs:79` 注释「MVP 仅 `encoding: sparse`」），编码阈值口径仍按《存储与目录规范》§5.1.2 供将来参照（`MapValidator.cs:47` 的 `GridTooLargeSparse` 警告已存在）。

---

## 4. 生成流水线

### 4.1 五步

```
WorldGenerator.Generate(width, height, params) → TerrainData

  ① 大陆遮罩   mask(x,y) = fBm_低(2–4 周期/图)          ∈ [0,1]
  ② 域扭曲     采样 mask 时坐标加 warp = fBm_中(x,y) × warpStrength(ruggedness)
  ③ 细节叠加   mask += fBm_高(5–7 倍频) × detailWeight
  ④ 分位数阈值 按 land_ratio 精确取阈值 τ，陆地 = mask > τ
  ⑤ 细化      CoastlineRefiner.Refine(...)  ← 与笔刷共用的同一函数
```

**为什么第 ④ 步必须用分位数而不是拍一个阈值**：直接写 `mask > 0.5` 的话，用户把「陆地比例」从 35% 调到 40% 时实际结果可能几乎不动——阈值与噪声分布不匹配。取分位数才能让旋钮**精确命中**目标比例。

**为什么第 ② 步（域扭曲）不可省**：直接阈值化噪声得到的是毛球状边缘；域扭曲（用另一层噪声扰动采样坐标）才会产生**海湾、半岛、峡湾**这些真正让海岸线显得自然的大尺度凹凸。这是本流水线里唯一「看起来像技巧、实际是必需品」的一步。

**陆地格写入什么**：陆地一律 `plain`，紧邻水域的陆地格写 `coast`；水域不写（隐含默认值 `water`，§3.2）。

### 4.2 确定性要求

**同一 `seed` + 同一参数 + 同一 `algorithm` 版本 → 逐格相同的结果**，跨运行、跨机器、跨 .NET 版本一致。这条是硬要求（否则「种子可复现」毫无意义），因此：

| 不得使用 | 理由 |
|---|---|
| `System.Random(seed)` | .NET 的种子算法在版本间**不保证稳定**，换运行时即产出不同世界 |
| Godot `FastNoiseLite` | 属 `Godot.*`；生成器按 AGENTS.md L105-127「领域层纯逻辑不引用 `Godot.*`」须可脱离引擎单测 |

**替代**：自实现（合计约 80–120 行，全部可单测）

- `DeterministicRandom` —— splitmix64，纯整数位运算
- `ValueNoise` —— 哈希格点上的值噪声 + fBm（倍频叠加）

### 4.3 代码归属

新建目录 `scripts/Domain/Generation/`，命名空间 **`WorldView.Domain.Generation`**。

**为什么放 `Domain` 而不是新开 `WorldView.Generation`**：AGENTS.md L105-127 已把命名空间集合定为 `WorldView.{Domain, Storage, Render, Ui, Editor}`，并规定领域层为纯逻辑——生成器正好符合（纯计算、无 I/O、无 `Godot.*`），放进 `Domain` 既满足纯洁性要求又不需改动 AGENTS.md 的命名空间契约。

**与 spec 1 的 `MapSizing` 位置差异的说明**：`MapSizing` 放在 `scripts/Editor/`（spec 1 §3.3），因为它是**编辑器层的单位换算**（服务于输入控件）；而生成器**从参数产出领域数据**（`TerrainData`），属域逻辑，故放 `Domain/`。两者都不引用 `Godot.*`，都可单测。

### 4.4 性能与线程

生成 512×512 = 26 万格，噪声 + 细化在 C# 里预计远低于 1 秒，可在对话框内同步跑（「重新掷骰」即时反馈）。**但需实测**（§10）：若超过约 200 ms 就应改为可显示进度的方式，否则对话框会明显卡顿。

---

## 5. 共用细化 pass

### 5.1 函数

```
CoastlineRefiner.Refine(cells, width, height, params) → cells

  1. 去碎岛   删除面积 < min_island_cells 的孤立陆块
  2. 填碎海   填平面积 < min_lake_cells 的封闭水域
  3. 形态学   按 erosion 次数做腐蚀/膨胀，削平 1 格宽毛刺与单格凸起
  4. 重标环   与任一 water 格八邻接的陆地格 → coast；其余陆地 → plain
```

### 5.2 三条硬约束

**（a）边界连通的连通块永不删除。** 当细化作用于一个**局部范围**（笔触包围盒）时，一块横跨范围边界的大陆在范围内看起来会是一个「小连通块」——若照面积阈值删除，就会把大陆切掉一角。**与范围边界相接的连通块一律视为「外部大陆的一部分」，无条件保留。**

**（b）幂等。** 同一输入重复跑，结果不再变化。理由：细化按「每戳一次」的粒度挂载时会重入重叠区域（§6.3），幂等保证重复执行不产生漂移。第 1、2 步天然幂等（处理后范围内不再存在低于阈值的连通块）；第 3 步的腐蚀/膨胀**不幂等**，故规定：**形态学步只在生成路径执行**，笔刷路径跳过（由 `params.erosion = 0` 传入，见 §6.4）。

**（c）局部性。** 细化只管传入的 `cells` 集合，**绝不全局重算**。全局重算会让「我涂了一笔，远处地形却变了」——撤销语义无法向用户解释，且 104 万格上每笔全图重算必然卡死。

**`coast` 环重标的范围必须比笔触范围多扩 1 格**：包围盒边界上的陆地格，其邻居在盒外，不扩 1 格就会漏标或错标。

---

## 6. 笔刷集成与撤销分组

### 6.1 触发时机：笔触**抬起**时

**只在 `WorldCanvas.cs:344`（`_painting = false`）触发一次**，不在拖动中途触发。

中途触发有确定的翻车方式：用户正一格一格往外画半岛，尚未画完的 3 格凸起会被当场判成碎块删掉。

实现：`WorldCanvas` 在按下时（`:322-325`）开始累积本笔的**格坐标并集包围盒**，抬起时（`:342-345`）对「包围盒外扩 N 格」跑一次细化。

### 6.2 撤销分组（`UndoStack` 新增批处理）

**现状问题**：一笔 = N 个落点命令，`UndoStack.Capacity = 200`（`UndoStack.cs:20`）意味着约 20 秒连续拖动就填满撤销栈，且 `Ctrl+Z` 只能一次一戳地回退。

**改法**：`UndoStack` 增加批处理边界，`WorldCanvas` 在按下时 `BeginBatch()`、抬起时在细化命令之后 `EndBatch()`，把**本笔的 N 个落点命令 + 1 个细化命令**合成一个撤销单元。

```
UndoStack.BeginBatch(description)   开始归集
UndoStack.EndBatch()                归集结束，压入一个复合单元
Undo()                              一次撤销整笔（含细化）
```

**这同时落实了**《地图编辑器规格》§1.3「操作粒度 = 一次落笔」——原实现与该条并不一致。

**归集期间每戳仍须立即 `Apply()`**（笔触要实时可见），因此复合单元的回退必须**按逆序**逐个 `Revert()`。

**空批处理必须安全**：按下后未产生任何戳（例如点了一下但被同格守卫 `WorldCanvas.cs:415-418` 挡掉）就抬起，`EndBatch()` 不得压入空单元，否则 `Ctrl+Z` 会出现「撤销了但什么都没变」。

### 6.3 细化的命令化

细化作为**独立命令** `RefineCoastlineCommand`（实现 `IEditorCommand`），紧跟本笔的落点命令之后、在 `EndBatch()` 之前入栈：

- `Apply()`：首次执行时快照受影响范围内所有格子的原值（`Dictionary<GridCoord, TerrainType>`），然后写入细化结果
- `Revert()`：按快照逐格写回
- `Description`：`"细化海岸线（去碎岛 431 · 填碎海 12）"` —— 把黑箱变成用户能看懂的东西

**写入通道**：走 `EditorContext.ReplaceMap`（`EditorContext.cs:272`）。**不要**用 `SetTerrainCells`——它把越界格静默跳过（`:360-363`），且每调一次都重建整张字典、强制 `string "sparse"`（`:382-385`）；细化一次可能改动上千格，逐格调用会退化成 O(n²)。

### 6.4 细化强度随笔刷尺寸缩放 + 全局开关（已定）

| 笔刷半径（格） | 细化行为 |
|---|---|
| 1–2 | **不细化**（只重标 `coast` 环）。用户在做精修：运河、礁石、单格细节 |
| 3–8 | 温和：只清 `min_island_cells` / `min_lake_cells` 的小碎块 |
| > 8 | 完整：含 §5.1 全部步骤（`erosion` 仍为 0，见 §5.2b） |

**阈值随笔刷尺寸缩放的理由**：大笔刷意味着用户在宏观塑形，边界毛糙是必然副产品，应当被平滑；小笔刷意味着用户在精修，此时任何自动改动都会变成「系统抢走我的意图」。

**全局开关**：`tool_row` 内一个「细化」开关（与 spec 1 的笔刷半径控件同排，预算之内）。关闭后笔刷只重标 `coast` 环，不做任何删除/填充。

`ToolStrip.cs` 的 20px 行预算已经很紧（`ToolStrip.cs:50`，且 `:205-208` 注释说明该行已需 `CompactStyle` 才不被 `ClipContents` 裁掉），因此开关应做成**紧凑图标按钮**而非带文字的复选框。具体形态待 P0-4 视觉规范（§10）。

---

## 7. UI：新建世界地图 / 地图设置（两态对话框）

### 7.1 同一对话框，两种状态

| 状态 | 进入方式 | 可用控件 |
|---|---|---|
| **新建** | 菜单 / 触发入口（待 P0-4 定位置） | 尺寸与分辨率 **+** 生成参数（可编辑）+ 预览 + 重新掷骰 |
| **已存在地图** | 对当前打开的地图打开 | 尺寸与分辨率（`ResizeMapCommand`，spec 1 §5.4）；生成参数**只读展示** + 「重新生成」**禁用**（§1.3 Out of scope） |

### 7.2 新建态布局

```
新建世界地图
─────────────────────────────────────────────
尺寸与分辨率    横向 [ 12800 ] km   纵向 [ 12800 ] km
                分辨率 [ 25 km/格 ▼]   → 512 × 512 格
生成参数        陆地比例 [ 35% ]   曲折度 [ 中 ]   种子 [ 739201 ] [🎲]
─────────────────────────────────────────────
预览   [ 缩略图 ]   陆地 34.7% · 陆地块 12 · 已去碎岛 431 · 已填碎海 12
─────────────────────────────────────────────
                  [ 重新掷骰 ]   [ 创建 ]   [ 取消 ]
```

- 预览缩略图：沿用 `MinimapSection` 的 1px/cell `ImageTexture` + `TextureFilter = Nearest` 手法，取色用 `Render/TerrainPalette.ColorOf`（唯一真源，不另立配色）
- 统计行：把细化 pass 暴露成可读数字。否则用户只会看到「海面很干净」而不知道系统做了什么
- **「重新掷骰」不写任何东西**：生成发生在地图存在之前，预览只活在对话框内（§7.3）

### 7.3 生成不进撤销栈

生成发生在「地图还不存在」的时刻，因此：

- **`GenerateWorldCommand` 这个类不需要存在**（同理，spec 1 的 `ResizeMapCommand` 是作用于**已存在**地图，两者不可混同）
- 点「创建」时才构造完整 `MapDoc` 并落盘；取消则什么都没发生
- 已存在地图不提供「重新生成」（§1.3）：那需要在撤销栈里承载整张地图的替换，且用户既有编辑会全丢

### 7.4 创建一张世界地图的完整写入

```
1. 构造 GridSpec（width/height 由 MapSizing 反算，cell_distance/distance_unit = km，base_unit = m）
2. 生成 TerrainData：
     Default = TerrainDefaults.Water
     Encoding = "sparse"
     Cells    = 陆地格（plain）+ 海岸环（coast），按 (y,x) 升序
3. MapDoc { Type = MapType.World, Generation = <params>, … }
4. 写入工程：`project.yaml` 的 `map_index` 追加一条 + `maps/world/<id>/main.yaml`
5. 打开新地图
```

**绝不逐格调 `SetTerrainCells`**（`EditorContext.cs:334-388` 每次调用重建整张字典）：26 万格逐格调用会直接不可用。必须在内存里构造完整 `TerrainData` 后**一次性**走 `ReplaceMap`（`:272`）。

**`project.yaml` 的 `map_index` 写入是本轮唯一的新存储动作**，需要确认 `ProjectSaver`（`EditorContext.cs:197-217` 调用处）是否已支持追加地图；若需新 API，列为 §11 的改动项。

---

## 8. 与现有系统集成

| 系统 | 现状 | 本轮 |
|---|---|---|
| `MapDoc`（record） | `MapDoc.cs:134-159` | **加法**：追加 `GenerationSpec? Generation` |
| `MapType` 枚举 | `MapDoc.cs:9-13`（`World, City`） | **不动**（`World` 已存在） |
| `TerrainDefaults` | `Terrain.cs:43-73`，只有 `Plain` | 追加 `Water` 常量 |
| `TerrainData.Default` | `MapDoc.cs:83-93`，字段已存在且被 `EditorContext.cs:343` 正确使用 | **不动**，世界图使用它 |
| `MapValidator` | `MapValidator.cs:35-37,47,59,64,129` | 追加 1 条**警告**（`generation` 参数越界），不改既有判据 |
| `YamlSchemas` | `scripts/Storage/Yaml/YamlSchemas.cs` | 注册 `GenerationSpec` 类型映射 |
| `ProjectLoader` / `ProjectSaver` | `scripts/Storage/Yaml/` | 除 `map_index` 追加外不动；旧的 `main.yaml` 无 `generation` 键 → 走默认 `null` |
| `UndoStack` | `UndoStack.cs:17-79` | **新增**批处理（§6.2）；容量与 FIFO 不动 |
| `WorldCanvas` | `:322-345` 输入、`:405-437` 落笔 | 改为批处理包裹 + 抬起触发细化 |
| `ApplyBrushCommand` | `ApplyBrushCommand.cs:14` | **不动**（细化是追加的独立命令） |
| `BrushController` | `BrushController.cs:10-37` | 不动 |
| `Render/` 只读例外 | 仅 `TerrainPalette.cs` 一个文件 | **不新增** `Render/` 文件；预览属 `Ui/` |
| `MapValidator` 越界判据 | 越界地形格/建筑 = error | 生成的网格天然在界内；细化不改尺寸 |
| `height_data` | MVP 未实现（`MapDoc.cs:132`）；但单元格级 `TerrainCell.Height` 字段**已存在**（`Terrain.cs:34`） | 本轮不碰高程 |

---

## 9. 测试要点

### 9.1 生成器（纯单测，无需引擎）

| 用例 | 断言 |
|---|---|
| **确定性** | 同 seed + 同参数跑两次 → 逐格相同；不同 seed → 不同 |
| **跨运行稳定** | 同 seed 的哈希/陆地格数固定为常量（防止将来误用 `System.Random` 导致静默改变） |
| **陆地比例命中** | `land_ratio = 0.35` → 实际陆地占比在 ±1.5% 内（分位数阈值生效） |
| 边界 | `land_ratio` 取 0.05 / 0.80 时仍有陆有海；`ruggedness = 0` 与 `1` 不抛异常 |
| 网格边界 | 生成的格坐标全部 `0 ≤ x < width`、`0 ≤ y < height`；无重复条目 |
| 排序契约 | `Cells` 按 `(y, x)` 升序（与 `EditorContext.cs:375-378` 一致） |
| 默认地形 | `TerrainData.Default.Type == Water`；海洋格不出现在 `Cells` 中 |

### 9.2 细化 pass

| 用例 | 断言 |
|---|---|
| 去碎岛 | 3 格孤立陆块（阈值 8）被删除 |
| **边界连通豁免** | 横跨范围的陆块被保留**且不被削角**（§5.2a，本组最重要的一条） |
| 填碎海 | 4 格封闭水域（阈值 6）被填平 |
| **幂等** | 同输入跑两次结果相同（§5.2b） |
| `coast` 环 | 与水域八邻接的陆地格全为 `coast`；其余陆地为 `plain`；扩 1 格边界不漏标 |
| 局部性 | 传入范围外的格子**一个都不变**（§5.2c） |
| 开关 | `erosion` 档位变化按预期改变毛刺数量 |

### 9.3 撤销分组

| 用例 | 断言 |
|---|---|
| 一次撤销整笔 | 按下→拖动产生 N 戳→抬起，`Ctrl+Z` **一次**后地形回到落笔前，`UndoCount` 减 1 |
| 复合单元逆序回退 | 混合地形（多种类型）经整笔 + 细化后一次回退，逐格与初始完全一致 |
| **空批处理安全** | 按下未产生任何戳就抬起 → `UndoCount` 不变，`Undo()` 返回 false（§6.2） |
| 容量与 FIFO | 批处理单元与普通命令共用 `Capacity = 200`，超限仍 FIFO 丢弃最旧 |
| 重做 | 撤销整笔后 `Ctrl+Y` 重做，结果与撤销前逐格一致 |

### 9.4 笔刷细化

| 用例 | 断言 |
|---|---|
| 尺寸缩放 | 半径 2 只重标 `coast` 环（不删格）；半径 8 会清除碎块 |
| 全局开关 | 关闭后任何笔刷都不删除/填充格子 |
| 运河保护 | 半径 1 画的 1 格宽运河，抬起后**仍然存在** |
| 只影响本笔包围盒 | 远处地形不变 |
| 仅抬起触发 | 拖动过程中不产生细化命令（`UndoCount` 只在抬起时 +1 复合单元） |

### 9.5 持久化（回归 + 新增）

| 用例 | 断言 |
|---|---|
| 旧工程照常加载 | 不含 `generation` 键的 `main.yaml` 加载后 `Generation == null`，且校验通过 |
| 往返 | 带 `generation` 的 `MapDoc` 存→读后各字段逐一相等 |
| 世界图落盘 | 生成的 `main.yaml` 中 `default.type == water`，海洋格不出现；`Encoding == "sparse"` |
| `map_index` | 新建世界地图后 `project.yaml` 含该地图条目，重载工程后能找到它 |

---

## 10. 待标定 / 待 P0-4

| # | 项 | 说明 |
|---|---|---|
| 1 | `TerrainDefaults.Water` 的 `move_cost` / `buildable` | 需与《数据模型规格》§3.1 一致；文档未定义则暂取高 `move_cost` + `buildable: false` |
| 2 | `min_island_cells` / `min_lake_cells` 默认值 | 现拟 8 / 6，需在 512×512 图上实看效果后定 |
| 3 | `ruggedness` 低/中/高 ↔ 倍频数与域扭曲强度的映射 | 需实看，可能改为离散三档而非连续 0–1 |
| 4 | 生成耗时 | 目标 < 200 ms（512×512）；超了要改进度反馈方式 |
| 5 | 细化强度随笔刷半径的分档边界（1–2 / 3–8 / >8） | 需在实机上试手感 |
| 6 | 「细化」开关与笔刷半径控件的具体形态 | 20px 行预算已紧（`ToolStrip.cs:50,205-208`），待 P0-4 视觉规范 |
| 7 | 新建入口挂在哪（菜单 / `tool_row` 尾部） | 同 spec 1 §9 第 7 项，两处应一次定案 |
| 8 | 缩略图的更新节流 | 每改一个参数就重生成 + 重绘 26 万格预览可能过频，需节流（如 150 ms 防抖） |
| 9 | 渲染层方块感弱化（TileSet 边缘/拐角自动拼接） | 独立主题，本轮不做 |

---

## 11. 本轮净改动清单（实现顺序）

### 11.1 spec 1 已定（本轮一并实施，不重复设计）

| # | 文件 | 动作 |
|---|---|---|
| a | `scripts/Editor/MapSizing.cs` | 新增（物理长度 ↔ 格数） |
| b | `scripts/Editing/ResizeMapCommand.cs` | 新增（`ResizeMapCommand`） |
| c | `scripts/Ui/Palette/ToolStrip.cs` | 修改（笔刷半径改物理单位输入） |
| d | `scripts/Ui/MapSettingsDialog.cs` | 新增（spec 1 §4.3；本 spec §7 将其升格为两态） |
| e | `scripts/Editor/EditorController.cs` | 修改（挂载对话框 + 触发入口） |

### 11.2 spec 2 新增（建议顺序）

| # | 文件 | 动作 |
|---|---|---|
| 1 | `scripts/Domain/Generation/DeterministicRandom.cs` | 新增（splitmix64） |
| 2 | `scripts/Domain/Generation/ValueNoise.cs` | 新增（值噪声 + fBm） |
| 3 | `scripts/Domain/Generation/GenerationParams.cs` | 新增＝`generation` 块的承载类型 `GenerationSpec` |
| 4 | `scripts/Domain/Generation/CoastlineRefiner.cs` | 新增（共用细化 pass，纯 C#） |
| 5 | `scripts/Domain/Generation/WorldGenerator.cs` | 新增（五步流水线） |
| 6 | `scripts/Domain/MapDoc.cs` | 修改（加法：`GenerationSpec? Generation`） |
| 7 | `scripts/Domain/Terrain.cs` | 修改（加法：`TerrainDefaults.Water`） |
| 8 | `scripts/Domain/Validation/MapValidator.cs` | 修改（加 1 条 `generation` 参数警告） |
| 9 | `scripts/Storage/Yaml/YamlSchemas.cs` | 修改（注册 `GenerationSpec`） |
| 10 | `scripts/Storage/Yaml/ProjectSaver.cs` | 修改（**仅当** `map_index` 追加尚无 API 时；需先核实） |
| 11 | `scripts/Editing/CompositeCommand.cs` | 新增（批处理载体） |
| 12 | `scripts/Editing/UndoStack.cs` | 修改（`BeginBatch` / `EndBatch`） |
| 13 | `scripts/Editing/RefineCoastlineCommand.cs` | 新增 |
| 14 | `scripts/Render/WorldCanvas.cs` | 修改（批处理包裹 + 抬起触发细化 + 累积笔触包围盒） |
| 15 | `scripts/Ui/MapSettingsDialog.cs` | 修改（升级为新建/设置两态 + 生成参数 + 预览） |
| 16 | `scripts/Ui/Palette/ToolStrip.cs` | 修改（「细化」开关） |
| 17 | `docs/数据模型规格_v0.1.md` | 修改（**追加** `generation` 块定义） |
| 18 | `tests/WorldView.Tests/Generation/**` | 新增（§9.1 / §9.2） |
| 19 | `tests/WorldView.Tests/Editing/**` | 扩充（§9.3 / §9.4） |
| — | 其余（`ApplyBrushCommand` / `BrushController` / `TerrainPalette` / `TerrainSwatch` / 既有 `Render/` 文件 / 既有校验判据） | **不动** |

---

## 12. 决策来源（traceability）

| 决策 | 内容 | 来源 |
|---|---|---|
| S1 | 生成 + 笔刷涂改**共用**同一条细化 pass | 用户选择「两者都要」 |
| S2 | 生成器**兼作**新建世界地图入口；与「地图设置」合并为两态对话框 | 用户选择「生成器兼作入口」 |
| S3 | 生成参数**落盘** `MapDoc` 的 `generation` 块 | 用户选择「落盘到 MapDoc」 |
| S4 | 超大网格 → **警告 + 引导，不硬阻止**；世界图 `Default = water`；存储层不动 | 用户选择「警告 + 引导」 |
| S5 | 细化强度**随笔刷尺寸缩放** + 全局开关 | 用户选择「随笔刷尺寸缩放 + 全局开关」 |
| S6 | `UndoStack` **加笔触分组**，一次撤销整笔 | 用户选择「加笔触分组」 |
| — | 局部性原则 / 边界连通豁免 / 幂等 / 分位数阈值 / 域扭曲必要 / `algorithm` 版本号 / `Domain.Generation` 命名空间 / 生成不进撤销栈 / 自实现噪声与 PRNG | 本 spec 的设计判断，理由见对应小节 |

---

## 13. 对 spec 1 的更正

| # | spec 1 处 | 更正 | spec 1 是否已就地改好 |
|---|---|---|---|
| C1 | §5.3「一笔 = 一个 `ApplyBrushCommand`」 | **不准确**。一笔 = N 个落点命令（`WorldCanvas.cs:421-436`，每戳各自 `Push`）+ 1 个细化命令。原表述会让人误以为拖动只产生一个撤销单元，从而低估「200 容量 ≈ 20 秒拖动」的问题 | ✅ 已改 |
| C2 | §3.1「不修改字段原则」 | **放宽为「加法」**：允许给 `MapDoc` 追加可选块（`generation`），既有字段定义仍一字不改 | ⛔ 不改——该原则对 spec 1 自身范围仍然成立，由本 spec 扩展，不改写原文 |
| C3 | §4.3「地图设置面板」 | 升格为**两态对话框**（新建 / 已存在地图），新增态多出生成参数与预览两组 | ⛔ 不改——由本 spec §7 覆盖该节 |

---

*本 spec 与 spec 1 均须经用户评审通过后，才可进入 `writing-plans`。*
