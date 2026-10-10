# UI 重新设计 — SimCity / RTS 风格（v0.2 增量）

> **目标**：把当前 4 区 IDE 风格的 `Main.tscn` 重做成「地图占满 + 功能集中底部 Palette」的可玩编辑器。
> **范围**：仅改 UI 与场景树；不动 `Domain/` 数据契约、不动 `Storage/`。`Render/` 只开**一个只读例外**（`TerrainPalette.cs`，见 §9 N7）。
> **栈**：Godot 4.7.2 mono + C#（沿用既有技术基调）。
> **位置**：本规格属任务级规格，存 `.trae/specs/ui-redesign-simgame/`；设计层在 `docs/界面与交互规格_v0.1.md` §1 加「已被本 spec 修订」标记，并在两处文末「变更记录」追加一行引用本 spec（见 §11）。
> **纪律**：
>
> - UI 状态（布局 / 显隐 / 折叠）属于 UI 私有，**不**进数据模型，不进 `project.yaml`。
> - 面板的拖放 / 折叠行为**只**改变 UI 状态，不修改任何地图 / 图层 / 文档数据。
> - Godot / C# / .NET 专有词只出现在 `scripts/Ui/`、`scripts/Render/` 与 `.tscn` 里，不污染 `docs/` 的字段定义。

---

## 1. Why（为什么要做）

### 1.1 现状的问题

当前 `scenes/Main.tscn` 是一个 4 区 IDE 布局：

- 顶栏（Open / Save / Undo / Redo / Breadcrumb） — 固定 32px 高
- 左侧 ToolPanel（180px 宽，工具 + 笔刷 + 地形 + 建筑按钮）
- 中间画布（CanvasArea + WorldCanvas）
- 右侧 RightColumn（240px 宽，Inspector + LayerList）
- 底栏 StatusBar（24px 高，鼠标坐标 + 选中 + 校验）

固定占用：左 180px + 右 240px + 顶 32px + 底 24px = **476px** 被 UI 永久吃掉。
1920×1080 窗口下画布只剩约 **1444×1024**，画布被「夹」在中间，根本没有 SimCity / RTS 那种「地图即世界」的感觉。

### 1.2 截图证据

`.worldview/fix_verify.png` 是一张 1280×720 的截屏：地图仅占约 700×500，左侧一长串按钮被「切掉一半」（文字溢出）。这就是当前的痛点。

### 1.3 用户诉求（原话）

> 主要画面应该是地图，有点像模拟城市，rts 游戏，主要是地图。其他事 drag & drop 的方式放置的。

后续追加（本轮收敛）：

> 绝大部分功能集中在下方 Palette 中。下方 Palette，从左至右是 1. 小地图　2. Building Palette，不一定是 9 宫格，应该根据大小调整图标排列。

> 事件播放条……放在下方 Palette 的上方。

翻译为产品需求：

1. **默认打开 → 地图占满**（RTS / SimCity 视感）。
2. **功能集中底部**：小地图、建筑面板、工具条、事件播放条全部收进一条 `BottomPalette`。
3. **建筑网格自适应**：列数 / 行数由可用宽高实时推导，不写死。
4. **面板可拖放**：拖到屏幕四边 → 停靠；拖到中间 → 浮动；F11 → 全屏地图模式。
5. **面板可关闭 / 折叠 / 重组**；布局可记忆。

---

## 2. Goals（验收指标）

| 编号 | 目标 | 验收手段 |
| --- | --- | --- |
| G1 | `BottomPalette` **展开态（200px）** 下 1920×1080 画布可视 ≥ 75%；**折叠态（40px）** 下 ≥ 90% | 截图 + 像素测量 |
| G2 | 小地图 / 建筑面板 / 工具条 / 事件播放条 **均可被拖动重组** | 手动拖动录屏 |
| G3 | 拖到屏幕左 / 右 / 上 / 下边 → 自动停靠 | 手动拖动录屏 |
| G4 | 拖到中央空白 → 成为浮动 Window | 手动验证 |
| G5 | 拖建筑图标到地图 → 在落点放置 | 单元 + 集成 |
| G6 | 关闭 / 重启 → 上次布局恢复 | 退出 → 启动比对 |
| G7 | F11 → 全屏地图模式（隐藏所有面板） | 手动验证 |
| G8 | `BottomPalette` 可折叠成 40px 紧凑工具行（`ToolStrip` 紧凑常驻变体） | 手动验证 |
| G9 | 编辑器核心（笔刷 / 撤销 / 切换工具 / 保存 / 时间）功能不变 | 既有 D1–D8 demo 仍通过 |
| G10 | 小地图显示全图 + 点击跳转 + 视口框实时同步 | 手动验证 + 截图 |
| G11 | 编辑地形后小地图 ≤ 200ms 内刷新 | 计时 |
| G12 | 建筑网格列数随窗口宽度实时重排，无需重启 | 拉窗口录屏 |
| G13 | 事件播放条与 `BottomPalette` 折叠状态**始终同步**，无错位 | 手动验证 |

**画布占比测算**（1920×1080，`TopStrip` 32px + `BottomPalette` 200px）：

| 状态 | 被占用 | 画布高度占比 |
| --- | --- | --- |
| `BottomPalette` 展开（28 + 152 + 20 = 200） | 32 + 200 = 232 | 848 / 1080 = **78.5%** |
| `BottomPalette` 折叠（40） | 32 + 40 = 72 | 1008 / 1080 = **93.3%** |
| 时间模式（播放条 overlay 120，不进布局） | 不变 | 仍 **78.5%** |

> 宽度方向：左右 Dock 默认完全收起，画布宽度 100%。
> 把播放条折进 `BottomPalette` **不额外消耗像素**——它占用的是原本给建筑网格的高度。

---

## 3. Layout 设计

### 3.1 默认打开的样子

```
+================================================================+
|  TopStrip (32px)                                                |
|  [Open] [Save] | <面包屑> | 创世历 1042-3-12 | [撤销] [重做] [全屏] |
+================================================================+
|                                                                |
|                                                                |
|                                                                |
|                        MAP CANVAS                              |
|                     （约 78.5% 可视）                           |
|                                                                |
|                                                                |
|                                                                |
+================================================================+
|  BottomPalette (200px)                                          |
|                                                                |
|  ── row 0 · TimelineStrip (28px, 满宽) ──────────────────────  |
|  [▶] 创世历 1042-3-12 辰时  ━━━━━━●──────────────  年 ── 时     |
|                                                                |
|  ── row 1 · build_row (152px, 横向 sections) ────────────────  |
|  +-------------+ | [民居][城堡][宗教][军事][自然]  ◀ 1/3 ▶     |
|  |  小地图      | | +----+----+----+----+----+----+----+----+  |
|  |   104×104   | | |农舍|民居|联排|庄园|城堡|教堂|神社|宫殿|  |
|  |    ▢        | | +----+----+----+----+----+----+----+----+  |
|  |             | | |    |    |    |    |    |    |    |    |  |
|  +-------------+ | +----+----+----+----+----+----+----+----+  |
|                                                                |
|  ── row 2 · ToolStrip (20px, 满宽) ──────────────────────────  |
|  V B P E │ Brush:1 │ Terrain:Grass ▼ │ ⟲ ⟳ │ 坐标(32,24) │ 就绪 |
+================================================================+
```

**为什么是「3 行」而不是「3 个并列 section」**：建筑网格需要横向空间，事件播放条需要**满宽**（宽度 = 时间分辨率），工具条需要满宽且极扁。三者对空间的诉求互斥，必须是纵向堆叠的**行**，行内再横向排 **section**。

### 3.2 BottomPalette 内部结构（rows → sections）

```
BottomPalette (dock_container, edge=bottom)
├─ row 0  timeline_row    layout=full_width  28px   ← 事件播放条
├─ row 1  build_row       layout=horizontal  flex   ← [minimap | tools（工具方块区）]
└─ row 2  tool_row        layout=full_width  20px   ← 工具 / 笔刷 / 撤销
```

- `row`：纵向堆叠单元，`layout` 为 `full_width`（独占一行）或 `horizontal`（行内并排 section）
- `section`：横向排列单元，`size_mode` 为 `fixed`（固定宽）/ `flex`（按权重分剩余宽）/ `auto`（跟内容）
- `sections` 是**有序可追加数组**——后续图层面板、检视器可作为新 section 塞入，不必改容器代码

### 3.3 事件播放条 TimelineStrip（row 0）

**决策依据**：底部是唯一天然满宽的边；时间轴是横向语义（时间从左到右流动）；拖时间轴时**必须同时看地图**，因此要紧贴地图下边缘；时间轴是**全局状态**（改它 → 整张地图、图层、人物、势力都变），建筑面板是**局部动作**，全局状态压在局部动作之上符合「从一般到具体」。

三态设计：

| 状态 | 高度 | 内容 |
| --- | --- | --- |
| **常态（默认）** | 28px | 播放/暂停 + 当前时间 + 进度轨 + 播放头 |
| **展开**（点箭头 / 按 `T`） | 56px | 常态内容 + 事件刻度 + 快照标记 + 时间粒度缩放控件 |
| **时间模式**（`Shift+T`） | 120px | 多轨（事件轨 / 人物轨 / 势力轨），**以 overlay 浮在地图下半部之上，不进布局树** |

**本轮只做常态的「空骨架」**：位置、高度、折叠按钮、时间文本占位、空进度轨。时间轴数据与回放逻辑属 M6（见 §9 N5）。

**顶栏时间文本必须移除**：`TopStrip` 与新播放条同时显示同一时间会造成「两处显示同一个可操作状态」的不同步隐患。时间是可操作对象，只与操作控件同处一地。

### 3.4 小地图 MinimapSection（row 1 · section 0）

- 固定在最左，`size_mode: fixed`，宽度跟行高自动成方形（`width: auto`）
- 默认 104×104（= build_row 高 152 − 上下 padding 48），`[待标定]`
- 显示全图 + 当前视口框；视口框由主 `Camera2D` 的 transform 反算
- 交互：左键单击 → 主视图以该点为圆心居中；拖动视口框 → 平移主视图
- **滚轮缩放不做**（与画布滚轮冲突，需先定 modifier，见 §9 N9）
- 可折叠 / 可隐藏（`M` 键）

### 3.5 建筑面板 BuildPaletteSection（row 1 · section 1）

- 顶部：分类 `TabBar`（民居 / 城堡 / 宗教 / 军事 / 自然 / 设施 / 自定义 …）
- 中部：**自适应网格**（列数 / 行数由可用宽高实时推导，见 §3.5.1）
- 右下：分页器 `◀ N/M ▶`，**`PageCount <= 1` 时整个隐藏**（不显示 "1/1"）
- 横向空间充裕：1920 宽下 `cell_size=56` + `cell_gap=4` → 约 **27 列 × 2 行 = 54 格**，绝大多数分类一屏放得下，分页器基本不出现

#### 3.5.1 自适应网格算法

```csharp
// scripts/Ui/Palette/AdaptiveGrid.cs
public void Reflow(Vector2 avail)
{
    int cols = Mathf.Max(1, Mathf.FloorToInt((avail.X + Gap) / (CellSize + Gap)));
    int rows = Mathf.Max(1, Mathf.FloorToInt((avail.Y + Gap) / (CellSize + Gap)));
    if (cols == _cols && rows == _rows) return;   // 只在真正变化时重建，避免像素级 resize 抖动
    _cols = cols; _rows = rows;
    ItemsPerPage = cols * rows;
    Grid.Columns = cols;
    Page = Mathf.Clamp(Page, 0, Mathf.Max(0, PageCount - 1));
    Rebuild();
}
```

**六条硬约束**（不满足会出 bug）：

| # | 问题 | 处理 |
| --- | --- | --- |
| 1 | `_Ready()` 时 `Size` 为 0，算出 cols=1 | 首次 `Reflow` 必须 `CallDeferred`，或等 `Resized` 首次触发 |
| 2 | 拖窗口时像素级 resize 反复重建 → 卡顿 | 仅在 `cols/rows` 变化时重建 + 100ms 防抖 |
| 3 | `QueueFree` 重建有一帧残影 | 先 `RemoveChild` 再 `QueueFree`，或用对象池复用 `BuildingButton` |
| 4 | `GridContainer` 拉伸子节点，格子变长方形 | 每格 `CustomMinimumSize = new Vector2(CellSize, CellSize)` |
| 5 | 分页器显示 "1/1" | `PageCount <= 1` 时整体隐藏 |
| 6 | 切分类后页码错乱 | `pages_by_category` 按分类记录，切回时还原 |

### 3.6 Inspector / LayerPanel 的位置

本轮**默认浮动**在右侧（`kind: float`，位置见 §5.2），默认 `visible: false`。
它们也可被拖入 `BottomPalette` 成为新的 section，但 v0.1 不预设。

理由：它们是「查询型」面板（看一眼就走），不是「操作型」面板（长时间占用）。占满画布的操作型面板（建筑、工具）进底部；查询型的浮起来，用完即收。

### 3.7 浮动状态（用户拖出来）

```
+================================================================+
|  TopStrip (32px)                                                |
|                              [浮动的 BuildPalette]              |
|                              +--------------------+             |
|                              |  民居 / 城堡  x     |             |
|        MAP CANVAS             |  +--+--+--+--+      |             |
|       （约 78.5% 可视）        |  |  |  |  |  |      |             |
|                              |  +--+--+--+--+      |             |
|                              +--------------------+             |
|                                          [浮动的 Inspector]    |
|                                          +---------+           |
|                                          | 地图    x|           |
|                                          | 世界 Kael|          |
|                                          +---------+           |
+================================================================+
|  BottomPalette (200px)                                          |
+================================================================+
```

### 3.8 全屏模式（F11）

```
+================================================================+
|                                                                |
|                                                                |
|                  MAP CANVAS（100% 视口）                       |
|                                                                |
|                                                                |
+================================================================+
```

### 3.9 视觉风格（SimCity / RTS 暗色）

- 主背景：`Color(0.08, 0.08, 0.10)`（接近纯黑，比原 `0.13, 0.13, 0.16` 更暗）
- 面板背景：`Color(0.12, 0.12, 0.15, 0.92)`（半透明，地图能透出一点）
- 面板描边：`Color(0.30, 0.30, 0.36, 1)` 1px
- 选中 / hover：`Color(0.45, 0.55, 0.85, 1)` 高亮边
- 文字：主 `#E6E6E6`，次 `#9090A0`，警告 `#E6B450`，错误 `#E66B4B`
- 圆角：4 px（用 `StyleBoxFlat`）
- 建筑格：`72×72` 基准，图标居中，名称在下方 11px（`[待标定]`）
- 字体：Godot 默认 + 等宽备用
- 暗色主题资源：`scripts/Ui/Theme/Dark.tres`

---

## 4. 拖放机制

### 4.1 面板拖放（panel / section 与 dock zone）

**发起**：用户抓住面板或 section 顶部的 16px drag handle，按住左键移动 ≥ 4 px，触发 drag。

- 面板发起 `_GetDragData(Vector2 atPosition)` → 返回 `Variant.From(new PanelDragData { PanelId = "...", SourceZone = "..." })`
- 同时面板把自己 `Hide()`，生成一个 `DragPreview`（`Control`，宽高与原面板一致）跟随光标

```csharp
public partial class PanelDragData : RefCounted
{
    public string PanelId { get; set; } = "";
    public string SourceZone { get; set; } = "";
}
```

**接受方**：`DockLayout` 内有 5 个 DockZone 节点（left / right / top / bottom / center-float）。

- 每个 DockZone 监听 `_CanDropData()`：若 payload 类型 == `PanelDragData` 则返回 true
- 鼠标进入 zone → zone 显示高亮（半透明蓝边框）；离开 → 取消高亮

**释放**：`_DropData(Vector2 atPosition, Variant data)`：

- `EdgeDock(Left|Right|Top)` → 挂到对应 dock 的 `VBoxContainer` / `HBoxContainer`
- `EdgeDock(Bottom)` → 挂进 `BottomPalette`，规则如下：
  - 落在某 `row` 的 **section 空缺位** → 成为该行的新 section，按落点 X 排序插入
  - 落在某 `row` 的 **行间隙** → 成为新行插在该行之前 / 之后
  - `timeline_row` 只接受 `TimelineStrip`，拒绝其他面板（满宽语义，见 §3.1）
- `CenterFloat` → 从原父节点移除，挂到 `Window` 下；Window 位置 = `atPosition - dragHandleOffset`，大小 = 面板当前 size
- DragPreview 销毁；触发 `LayoutChanged` 信号

### 4.2 内容拖放（palette 与 map canvas）

**发起**：建筑面板里的 `BuildingButton` 触发 `_GetDragData()`：

```csharp
var preview = new TextureRect { Texture = GetPreviewTexture(buildingType) };
SetDragPreview(preview);
return Variant.From(new BuildingDragData { BuildingType = ..., MapId = context.ActiveMapId });
```

地形调色板的 `TerrainSwatch` 同样：`Variant.From(new TerrainDragData { TerrainType = ..., Brush = ..., Radius = ... })`。
两者 payload 类同样必须 `RefCounted` + `partial`。

**接受方**：`WorldCanvas` 监听：

- `_CanDropData(Vector2 atPosition, Variant data)`：`data.As<BuildingDragData>()` 或 `data.As<TerrainDragData>()` → true
- `_DropData(Vector2 atPosition, Variant data)`
  `atPosition` 是全局屏幕坐标，必须先转换到画布局部坐标再换算格子：

```csharp
// atPosition：全局屏幕坐标
var xform = GetGlobalTransformWithCanvas();
var local = xform.AffineInverse() * atPosition;
var cell  = LocalToCell(local);
// 走命令模式，不绕过 UndoStack
BuildingPlaceController.TryPlace(cell);   // 或 ApplyBrushCommand(cell)
```

漏掉这一步，拖放落点会偏移（尤其画布有平移 / 缩放时）。

**关键**：拖放放下时仍然走 `UndoStack`，不绕过命令模式（与现有 MVP 一致）。

### 4.3 小地图交互

| 操作 | 行为 | 备注 |
| --- | --- | --- |
| 左键单击 | 主视图以落点为中心 | 换算：`world = miniLocal / miniScale` |
| 按住拖动视口框 | 平移主视图 | 只改相机，不写数据 |
| `M` | 显示 / 隐藏小地图 | UI 状态 |
| 滚轮 | **不做** | 见 §9 N9 |

### 4.4 Godot 4 API 用法

- `Control._GetDragData(Vector2 atPosition)` → `Variant`
- `Control._CanDropData(Vector2 atPosition, Variant data)` → `bool`
- `Control._DropData(Vector2 atPosition, Variant data)` → `void`
- `Control.SetDragPreview(Control preview)` — 参数是 `Control`，不是 `Texture2D`
- `Control.ForceDrag(Variant data, Control preview)` — 手动触发拖放
- `Window` 用于浮动面板（`Transient = true`、`Exclusive = false`、`Unresizable = false`）
- `AcceptDialog` / `Popup` 用于模态
- 拖动判定：`_GuiInput` + `(e.ButtonMask & MouseButtonMask.Left) != 0`，位移阈值 4 px
  - [FIX] 枚举是 `MouseButtonMask.Left`，不是 `MouseButton.Left`
- 自适应网格：`GridContainer.Columns` 在 `_Ready` 时不可信，须等首次 `Resized`
- 小地图纹理：`ImageTexture.CreateFromImage(Image)`，`TextureFilter = Nearest`（避免缩小时的色块糊掉）

---

## 5. 数据结构

### 5.1 **不**进数据契约

面板布局 / 浮动状态 / 可见性 / 折叠状态 / 当前分类 / 当前页码 / 小地图尺寸**只**是 UI 状态，**不**写进 `MapDoc` / `World` / `Project`。

理由（与 `docs/界面与交互规格_v0.1.md §6` 一致）：

- 数据契约只承载领域事实（地图、图层、地形、人物、事件）；
- 视图状态 / 工具状态 / 布局状态属于 UI 层；
- 关闭工程 / 切工程时 UI 状态按工程隔离；切回工程时还原。

多工程隔离：按工程分文件。

```text
.worldview/layouts/<projectId>.yaml
```

`projectId` 从 `EditorContext.ActiveProject.Id` 取（或工程根目录名做 fallback）。无对应文件则用默认布局，不报错。

> **注意**：布局文件写在 `.worldview/layouts/`（**既有约定**），不是早期草稿里的 `.worldview/ui_layout.yaml`，§8 工程验收以本行为准。

### 5.2 UI 私有文件：`.worldview/layouts/<projectId>.yaml`

```yaml
# 由 BottomPalette / DockLayout 自动序列化，反序列化时容错
schema_version: 1
project_id: sample_world

root:
  - id: top_strip
    kind: dock_edge
    edge: top
    visible: true
    height: 32
    # 注意：不再包含时间文本（时间唯一由 timeline_row 承载）

  - id: bottom_palette
    kind: dock_container
    edge: bottom
    visible: true
    collapsed: false        # false = 展开 200px；true = 折叠 40px
    height: 200
    collapsed_height: 40
    rows:
      - id: timeline_row
        order: 0
        layout: full_width
        height: 28          # 常态
        expanded_height: 56 # 展开
        mode_height: 120    # 时间模式（overlay，不进布局）
        expanded: false

      - id: build_row
        order: 1
        layout: horizontal
        flex: 1
        sections:
          - id: minimap
            order: 0
            size_mode: fixed
            width: auto     # auto = 跟行高成方形
            visible: true

          - id: build_palette
            order: 1
            size_mode: flex
            flex: 1
            cell_size: 56   # [待标定] 40–96
            cell_gap: 4
            active_category: residences
            pages_by_category:   # 每分类记住自己的页码；items_per_page 不落盘（推导值）
              residences: 0
              castles: 0
              religion: 0
              military: 1
              nature: 0
              facilities: 0

      - id: tool_row
        order: 2
        layout: full_width
        height: 20

  - id: inspector
    kind: float             # 默认浮动、默认隐藏，见 §3.6
    x: 1620
    y: 80
    width: 280
    height: 420
    visible: false

  - id: layer_panel
    kind: float
    x: 1620
    y: 520
    width: 280
    height: 320
    visible: false
```

**写时机**：

1. 每次 `LayoutChanged` 信号后，设置脏标记 + 启动一次性 `Timer`（500 ms 防抖），超时则同步写盘。
2. 每次退出时，`_ExitTree` 里**同步（不 await）**立即写一次，防止退出丢布局。
3. 若需更稳妥，可在 `_Notification(NotificationWMCloseRequest)` 里拦截退出流程：`GetTree().AutoAcceptQuit = false;` → 保存 → `GetTree().Quit()`。

**读时机**：

1. 启动时若 `.worldview/layouts/<projectId>.yaml` 存在且 `schema_version` 匹配 → 还原；否则用默认布局，不报错。
2. `schema_version` 不匹配 → 回退默认布局，**不报错、不删除旧文件**（便于排查）。

### 5.3 字段汇总（仅 UI 层私有）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `kind` | enum `dock_edge` / `dock_container` / `dock_child` / `float` / `section` | 面板存在形态 |
| `edge` | enum `top` / `bottom` / `left` / `right` | 停靠边（仅 `dock_edge` / `dock_container`） |
| `layout` | enum `full_width` / `horizontal` | 行内布局（仅 `row`） |
| `size_mode` | enum `fixed` / `flex` / `auto` | 横向尺寸策略（仅 `section`） |
| `flex` | int | 权重（仅 `size_mode: flex`） |
| `x`, `y` | int | 浮动窗口位置（仅 `float`） |
| `width`, `height` | int | 面板尺寸；`width: auto` 表示跟随行高成方形（仅 minimap） |
| `collapsed_height` | int | 折叠态高度（仅 `dock_container`） |
| `expanded_height` / `mode_height` | int | 播放条的展开态 / 时间模式高度（仅 `timeline_row`） |
| `visible` | bool | 是否显示 |
| `collapsed` | bool | 是否折叠（仅 `dock_container`） |
| `expanded` | bool | 是否展开（仅 `timeline_row`，28 ↔ 56） |
| `cell_size` / `cell_gap` | int | 建筑格边长 / 间距（仅 `build_palette`；`[待标定]`） |
| `active_category` | string | 当前分类（仅 `build_palette`） |
| `pages_by_category` | map<string,int> | 每分类页码（仅 `build_palette`） |
| `order` | int | 在 row / section 内的顺序 |
| `id` | string | 面板稳定 ID（如 `build_palette`），跨会话复用 |
| `schema_version` | int | 顶层字段；不匹配时回退默认布局 |
| `project_id` | string | 顶层字段；用于多工程隔离校验 |

**明确不落盘**（推导值，写进 YAML 会与分辨率脱钩）：

- `items_per_page`（= `cols × rows`，运行时算）
- `cols` / `rows`（运行时算）
- 视口框位置 / 主相机 transform

---

## 6. 文件 / 目录变更

```
world-view/
├── scenes/
│   ├── Main.tscn                        [重写] map-first + BottomPalette
│   ├── WorldCanvas.tscn                 [不变]
│   └── ui/
│       ├── BottomPalette.tscn           [新增] 三段式容器（rows）
│       ├── TimelineStrip.tscn           [新增] 事件播放条（空骨架）
│       ├── MinimapSection.tscn          [新增] 小地图
│       ├── BuildPaletteSection.tscn     [新增] 建筑面板（分类 + 自适应网格 + 分页）
│       ├── ToolStrip.tscn               [新增] 工具行
│       ├── DockPanel.tscn               [新增] 可停靠面板通用 scene
│       ├── DockZone.tscn                [新增] 边缘 dock 接受区
│       └── preview/                     [新增] 拖放 ghost 预览纹理
│           ├── building_preview.png
│           └── terrain_preview.png
│
├── scripts/
│   ├── Editor/
│   │   ├── EditorController.cs          [大改] 瘦身；只负责全局信号 + 生命周期
│   │   ├── EditorContext.cs             [小改] 加 `LayoutChanged` 信号
│   │   └── ToolState.cs                 [不变]
│   ├── Ui/
│   │   ├── Dock/                        [新增]
│   │   │   ├── DockLayout.cs            主停靠容器（5 个 DockZone + dock tree）
│   │   │   ├── DockPanel.cs             可停靠面板基类（标题栏、drag、close）
│   │   │   ├── DockZone.cs              dock 接受区（_CanDropData / _DropData）
│   │   │   ├── FloatWindow.cs           浮动 Window 包装
│   │   │   ├── PanelState.cs            序列化 / 反序列化
│   │   │   ├── PanelDragData.cs         payload（: RefCounted, partial）
│   │   │   ├── BuildingDragData.cs      payload（: RefCounted, partial）
│   │   │   └── TerrainDragData.cs       payload（: RefCounted, partial）
│   │   ├── Palette/                     [新增]
│   │   │   ├── BottomPalette.cs         主容器：rows 管理 + 折叠
│   │   │   ├── PaletteRow.cs            行（full_width / horizontal）
│   │   │   ├── PaletteSection.cs        section 基类（尺寸策略、拖动、隐藏）
│   │   │   ├── AdaptiveGrid.cs          自适应网格：cols/rows 推导 + 防抖重建
│   │   │   ├── BuildPaletteSection.cs   分类 Tab + 网格 + 分页
│   │   │   ├── MinimapSection.cs        小地图
│   │   │   ├── TimelineStrip.cs         事件播放条（本轮空骨架）
│   │   │   └── ToolStrip.cs             工具行
│   │   ├── Theme/Dark.tres              [新增]
│   │   ├── Inspector.cs                 [新增，默认浮动] 从 EditorController 抽出
│   │   └── LayerPanel.cs                [新增，默认浮动] 从 EditorController 抽出
│   ├── Render/
│   │   ├── WorldCanvas.cs               [小改] 加 _CanDropData / _DropData
│   │   ├── TerrainPalette.cs            [新增] 地形类型 → 代表色，**只读共享表**
│   │   └── (其他不变)
│   └── Editing/
│       └── (不变)
│
└── .worldview/
    └── layouts/                         [运行时生成] 每工程一份布局
        └── <projectId>.yaml
```

**新增目录**：

- `scripts/Ui/Dock/`：停靠系统
- `scripts/Ui/Palette/`：底部 Palette 的行 / 节体系
- `scenes/ui/`：UI 场景（与 `scenes/WorldCanvas.tscn` 同级）

**`Render/TerrainPalette.cs` 的例外说明**：本轮唯一一次触碰 `Render/`。它是一个**纯只读常量表**（地形类型 ID → RGB），供主视图与小地图共用，**不含任何渲染逻辑、不持有节点、不写状态**。存在的唯一理由是避免小地图与主视图各维护一份调色板导致颜色不一致。

**不**修改：

- `scripts/Domain/**`
- `scripts/Storage/**`
- `scripts/Editing/**`
- `scripts/Render/**` 除 `WorldCanvas.cs`（仅加 drag handler）与新增 `TerrainPalette.cs`
- `scripts/Editor/EditorContext.cs`（仅加 `LayoutChanged` 信号）/ `ToolState.cs` / `UndoStack.cs`
- `examples/sample_world/**`
- 任何 `docs/*.md` 的**字段定义**（仅按 §11 追加变更记录行 + §1 加修订标记）

---

## 7. 任务分解（DoD：每条任务对应一个可验证的产出）

### T1 — DockLayout 骨架（M2 起点）

- [ ] `scripts/Ui/Dock/DockLayout.cs`（partial Control 节点）
- [ ] `scripts/Ui/Dock/DockPanel.cs`（partial Control，含标题栏 + close 按钮 + drag handle）
- [ ] `scripts/Ui/Dock/DockZone.cs`（partial Control，5 个 dock 接受区；`MouseFilter = Stop`）
- [ ] `scripts/Ui/Dock/FloatWindow.cs`（Window 包装；`Transient = true`, `Exclusive = false`, `Unresizable = false`）
- [ ] `scripts/Ui/Dock/PanelState.cs`（serialize / deserialize YAML）
- [ ] `PanelDragData.cs` / `BuildingDragData.cs` / `TerrainDragData.cs`（全部 `public partial class ... : RefCounted`）
- [ ] `EditorContext` 加信号 `[Signal] public delegate void LayoutChangedEventHandler();`

**DoD**：`dotnet build` 通过；空 DockLayout 场景在 Godot 编辑器能加载；`LayoutChanged` 可 emit。

### T2 — 重写 Main.tscn 为 map-first

- [ ] `scenes/Main.tscn` 改为：`DockLayout` 根 + `WorldCanvas` 占满主视口
- [ ] `TopStrip`（32px，**不含时间文本**）+ `BottomPalette`（`dock_container`, edge=bottom, 200px）
- [ ] 左右 Dock 默认完全收起
- [ ] `BottomPalette` 三行骨架就位：`timeline_row` 28 / `build_row` flex / `tool_row` 20

**DoD**：F5 启动后画布可视区域 ≥ 75%（G1，展开态）。

### T3 — 抽出 ToolStrip / Inspector / LayerPanel

- [ ] `scripts/Ui/Palette/ToolStrip.cs`：工具 / 笔刷 / 地形 / 撤销（进 `tool_row`）
- [ ] `scripts/Ui/Inspector.cs`：抽出 `RebuildInspector`（默认浮动）
- [ ] `scripts/Ui/LayerPanel.cs`：抽出 `RebuildLayerList`（默认浮动）
- [ ] `EditorController` 移除三个面板的构建代码

**DoD**：三个面板功能与重构前一致（D1 / D2 / D6 demo 仍通过）。

### T4 — 面板拖放

- [ ] `DockPanel._GuiInput`：按住标题栏左键 4 px 后调用 `ForceDrag`
- [ ] `DockPanel._GetDragData`：返回 `PanelDragData`
- [ ] `DockPanel.SetDragPreview`：生成 ghost Control
- [ ] `DockZone._CanDropData` / `_DropData`：判定 + 重新挂载
- [ ] `BottomPalette` 的落点规则（section 位 / 行间隙 / timeline_row 拒收，见 §4.1）

**DoD**：手动录屏证明左 / 右 / 上 / 下 dock 与浮动（G2 / G3 / G4）。

### T5 — 内容拖放（建筑 / 地形）

- [ ] `BuildingButton._GetDragData`：返回 `BuildingDragData`
- [ ] `TerrainSwatch._GetDragData`：返回 `TerrainDragData`
- [ ] `WorldCanvas._CanDropData` / `_DropData`：分别走 `BuildingPlaceController.TryPlace` / `ApplyBrushCommand`

**DoD**：拖建筑到地图 → 放置；拖地形到地图 → 涂（G5）。

### T6 — 布局持久化

- [ ] `PanelState.Save(rootPath)`：写 `.worldview/layouts/<projectId>.yaml`
- [ ] `PanelState.Load(rootPath)`：读并容错（缺字段用默认；`schema_version` 不匹配回退默认）
- [ ] `DockLayout._ExitTree`：**同步立即**保存（禁止 await）
- [ ] `LayoutChanged` 后 500 ms 防抖 Timer → 同步写盘
- [ ] `DockLayout._Ready`：启动时还原

**DoD**：手动调整布局 → 关闭 → 重启 → 布局恢复（G6）。

### T7 — 全屏模式

- [ ] `TopStrip` 加全屏按钮 + `F11` 快捷键
- [ ] 切换时 `DockLayout.SetAllPanelsVisible(bool)` + 隐藏 `TopStrip` / `BottomPalette`
- [ ] 退出全屏恢复上次可见性快照

**DoD**：F11 切换全屏（G7）。

### T8 — BottomPalette 折叠

- [ ] 折叠态（40px）= `ToolStrip` 紧凑常驻变体：`timeline_row` 与 `build_row`（小地图 + 工具方块区）收起，`tool_row` 常驻，其中 `ToolStrip` 只保留工具切换图标与坐标；展开恢复全部。
- [ ] 折叠态下行的 `visible` 不落盘。
- [ ] 折叠 / 展开按钮在 palette 右上角；快捷键 `Ctrl+B`

**DoD**：手动验证（G8 / G13）。折叠态画布 ≥ 90%。

### T9 — 暗色主题资源

- [ ] `scripts/Ui/Theme/Dark.tres`（Godot Theme 资源）
- [ ] 全局应用到 `Main.tscn` 根
- [ ] 建筑格 `StyleBoxFlat`：圆角 4、padding 8、hover 高亮

**DoD**：所有 UI 元素使用同一主题；颜色与 §3.9 一致。

### T10 — 验收模拟录屏

- [ ] 在 Godot 4.7.2 mono F5，按既有 D1–D8 走一遍，截图保存到 `.worldview/ui_redesign_demos/`
- [ ] 新增 D9–D24 演示（拖放 / 停靠 / 浮动 / 全屏 / 持久化 / 分页 / 小地图 / 重排）

**DoD**：D1–D24 全部通过（G9）。

### T11 — BottomPalette 行 / 节容器

- [ ] `scripts/Ui/Palette/BottomPalette.cs`：`rows` 纵向管理 + `collapsed` 切换 + 40/200 高度
- [ ] `scripts/Ui/Palette/PaletteRow.cs`：`full_width` / `horizontal` 两种布局
- [ ] `scripts/Ui/Palette/PaletteSection.cs`：`size_mode`（fixed / flex / auto）+ 隐藏
- [ ] `scenes/ui/BottomPalette.tscn` + `TimelineStrip.tscn` + `ToolStrip.tscn`

**DoD**：三行正确堆叠；改 `rows` 顺序 → 界面随之变化。

### T12 — 自适应网格 + 分页

- [ ] `scripts/Ui/Palette/AdaptiveGrid.cs`：`Reflow()` + 100 ms 防抖 + 仅在 cols/rows 变化时重建
- [ ] `scripts/Ui/Palette/BuildPaletteSection.cs`：分类 `TabBar` + 网格 + 分页器 + `pages_by_category`
- [ ] 六条硬约束全部落实（§3.5.1）

**DoD**：拉宽 / 拉窄窗口 → 行列实时重排（G12）；`cell_size` 从 56 改到 96 → 立即重排；分页器在 `PageCount <= 1` 时隐藏。

### T13 — 小地图

- [ ] `scripts/Render/TerrainPalette.cs`：地形类型 → RGB 只读常量表
- [ ] `scripts/Ui/Palette/MinimapSection.cs`：由地形格生成 `ImageTexture`（1px/cell，`TextureFilter = Nearest`）
- [ ] 视口框：主 `Camera2D` transform → 小地图坐标
- [ ] 左键单击 → 主视图居中；拖动视口框 → 平移主视图
- [ ] `M` 键显示 / 隐藏
- [ ] 刷新：监听 `TerrainChanged` / `MapChanged` → 标脏 → 下一帧重建（按 chunk 局部重建优先）

**DoD**：小地图显示全图（G10）；点击跳转正确（G10）；编辑地形后 ≤ 200ms 刷新（G11）。

### T14 — TimelineStrip 空骨架

- [ ] `scripts/Ui/Palette/TimelineStrip.cs`：播放/暂停按钮 + 时间文本占位 + 空进度轨 + 播放头
- [ ] 28 ↔ 56 展开切换（`T` 键）
- [ ] 数据源：只读 `EditorContext` 的当前时间显示；**不接入时间轴回放逻辑**
- [ ] `TopStrip` 移除时间文本

**DoD**：播放条随 `BottomPalette` 一起折叠 / 展开，无错位（G13）；`T` 键切换 28/56。

> **边界**：T14 只交付**位置与骨架**。时间轴的数据模型、刻度、事件挂载、回放、快照全部属 M6，不在本轮。

---

## 8. 验收标准（DoD 汇总）

### 8.1 功能验收

- [ ] **D1 / G1**：启动 + 打开示例工程 → 画布 ≥ 75%（展开态）
- [ ] **D2**：原 D2 demo 仍通过：切地形类型 + 笔刷绘制（G9）
- [ ] **D3 / D4**：原 D3 / D4 demo 仍通过：撤销 / 重做（G9）
- [ ] **D5**：原 D5 demo 仍通过：双击城市下钻
- [ ] **D6**：原 D6 demo 仍通过：建筑放置（面板按钮**或**拖放）+ 越界提示（G9）
- [ ] **D7**：原 D7 demo 仍通过：保存 + 重启一致性
- [ ] **D8**：原 D8 demo 仍通过：校验失败弹窗
- [ ] **D9 / G5**：拖建筑图标到地图 → 放置
- [ ] **D10 / G3**：拖面板到四边 → 停靠
- [ ] **D11 / G4**：拖到中央 → 浮动
- [ ] **D12 / G7**：F11 全屏切换
- [ ] **D13 / G6**：布局持久化
- [ ] **D14**：建筑面板分页器在 `PageCount > 1` 时出现、`<= 1` 时隐藏
- [ ] **D15**：切换分类 → 网格内容刷新，且页码按分类还原
- [ ] **D16**：`cell_size` 改动 → 网格立即重排
- [ ] **D17 / G10**：小地图显示全图
- [ ] **D18 / G10**：点击小地图 → 主视图跳转到该点
- [ ] **D19**：`M` 键显示 / 隐藏小地图
- [ ] **D20 / G11**：编辑地形 → 小地图 ≤ 200ms 刷新
- [ ] **D21 / G12**：窗口拉宽 / 拉窄 → 建筑网格行列实时重排
- [ ] **D22**：建筑数超出可视格数 → 分页器出现且可翻页
- [ ] **D23 / G8**：`BottomPalette` 折叠 / 展开，折叠态画布 ≥ 90%
- [ ] **D24 / G13**：播放条随 `BottomPalette` 一起折叠 / 展开，无错位

### 8.2 工程验收

- [ ] `dotnet build WorldView.sln` 成功
- [ ] Godot 4.7.2 mono `--headless --check-only` 通过
- [ ] `scripts/Domain/` 与 `scripts/Storage/` 仍**无** `using Godot;`
- [ ] `scripts/Render/` 的改动**仅限** `WorldCanvas.cs`（drag handler）与新增 `TerrainPalette.cs`
- [ ] `TerrainPalette.cs` 不含渲染逻辑、不持有节点、不写状态（§6 例外说明）
- [ ] `.worldview/layouts/` 不进版本库（已 `gitignored`）
- [ ] 现有 `docs/` 任何**字段定义**未被修改
- [ ] 既有信号 `MapChanged` / `LayersChanged` / `MouseMoved` 等**不**被改名；仅新增 `LayoutChanged`

---

## 9. 范围外（明确不做）

| 编号 | 不做 | 理由 |
| --- | --- | --- |
| N1 | 面板同位置合并为 Tab 群组 | 本轮先做单 panel / 单 section；多 panel 同位置用纵向堆叠 |
| N2 | 浮动面板拖出主窗口 | Godot 的 Window 可独立但与主窗口无视觉协调，v0.1 不开 |
| N3 | 主题切换 / 多套皮肤 | 暗色一套够用；切换留到主题系统 |
| N4 | ~~缩略地图（minimap）~~ | **已移出范围外**：小地图进 M2（见 T13）。它是导航基础设施，缺了它大世界地图编辑体验不成立 |
| N5 | 时间轴的**数据模型 / 刻度 / 事件挂载 / 回放 / 快照** | 仍归 M6。本轮只交付 `TimelineStrip` 的**位置与空骨架**（28px 常态、可展开、可折叠），不接时间轴逻辑 |
| N6 | 面板搜索 / 命令面板 | v0.1 不开 |
| N7 | 修改 `Domain/` / `Storage/` 数据契约 | 数据契约中立。`Render/` 只开一个例外：新增 `TerrainPalette.cs` 只读常量表（见 §6） |
| N8 | 多工程同窗口 | M7+；本轮一个工程一个布局 |
| N9 | 小地图滚轮缩放、多轨时间轴编辑 | 滚轮缩放与画布滚轮冲突，需先定 modifier；多轨属 M6 |

---

## 10. 变更纪律（与 `AGENTS.md §2 / §7` 对齐）

- [ ] 不污染第七条设计原则（特别是「目录即工程」「YAML 为主格式」）
  - UI 布局不进 `project.yaml`，只进 `.worldview/layouts/<projectId>.yaml`
- [ ] 框架专有概念只出现在 `scripts/Ui/`、`scripts/Render/` 与 `.tscn` 内
  - 不污染 `Domain/` 与 `Storage/`
- [ ] 既有 `EditorController.cs` 信号**不**删，只加 `LayoutChanged`
- [ ] 数据契约**不动**；本 spec 的字段表 §5.3 全部是 UI 私有
- [ ] `examples/sample_world/` 不改；D7 demo 不变
- [ ] 既有 D1–D8 demo 必须继续通过
- [ ] `Render/` 例外**只许** `TerrainPalette.cs` 一个文件，且必须保持纯只读常量表

---

## 11. 文档同步

| 文档 | 改动 |
| --- | --- |
| `AGENTS.md` §6.2 进度 | 追加「UI redesign (M2)：底部 Palette 架构」一行，引用本 spec |
| `docs/界面与交互规格_v0.1.md` §1 | 在 §1 顶部插入「**已被 [ui-redesign-simgame](../.trae/specs/ui-redesign-simgame/spec.md) 修订**」标记，列出 5 处差异（下方固定栏 → 底部 Palette / 顶栏时间轴 → 底部播放条 / 左工具栏 → 底部 tool_row / 右面板 → 默认浮动 / 小地图入 palette）。**不删除原 §1 文本**（保留历史语义） |
| `docs/界面与交互规格_v0.1.md` §7 变更记录 | 追加一行「v0.1（补丁：底部 Palette 架构）」，引用本 spec |
| `docs/总体设计_v0.1.md` | **仅**在 §10 变更记录追加一行，引用本 spec。§3 分层架构的界面层描述仍成立（抽象层级未变），**不改正文** |
| `.trae/specs/map-editor-mvp/checklist.md` | 不改（本轮不动 MVP 任务清单） |

**不**改：

- `docs/数据模型规格_v0.1.md`（数据契约中立）
- `docs/存储与目录规范_v0.1.md`
- `docs/二进制格式规格_v0.1.md`
- `docs/地图类型规格_v0.1.md`
- `docs/地图编辑器规格_v0.1.md` §1 / §2 / §3（编辑能力不变，只换容器）
- `docs/图层系统规格_v0.1.md`
- `docs/时间轴系统规格_v0.1.md`
- `docs/事件与人物登记规格_v0.1.md`

> 注：原 v0.1 草稿里写的「`docs/总体设计_v0.1.md` §3.4 UI 层」**不存在该小节**，已修正为「仅追加 §10 变更记录」。

---

## 12. 风险与对策

| 风险 | 概率 | 影响 | 对策 |
| --- | --- | --- | --- |
| 拖放性能在画布大时抖动 | 低 | 中 | `WorldCanvas` 只在 `_CanDropData` 命中后做 hit-test；预览 Control 用 TextureRect 缓存 |
| Godot 的 Window 在不同平台焦点不同 | 中 | 低 | 浮动面板默认 `Transient = true`、`Exclusive = false`；Win / macOS / Linux 各跑一次手动验收 |
| 布局文件与工程版本错位 | 中 | 中 | `schema_version` 字段；不匹配时回退默认布局，不报错、不删旧文件 |
| 既有 Demo（D1–D8）破坏 | 低 | 高 | T3 / T10 阶段每次跑一遍既有 demo，回归失败立即停 |
| 用户期望的拖放粒度与本文不一致 | 中 | 中 | 本 spec 落地前先让用户评审 §3–§5；任何偏离都先改 spec 再写代码 |
| C# payload 类忘记 `partial` / 未继承 `RefCounted` | 中 | 高 | 三个 payload 必须 `public partial class X : RefCounted`；否则 `Variant.From(...)` 失败或拖放静默 null |
| 拖放落点未换算坐标系 | 中 | 高 | `_DropData` 里必须先 `GetGlobalTransformWithCanvas().AffineInverse() * atPosition`，再 `LocalToCell(local)` |
| `_ExitTree` 里 `await` 保存导致丢失 | 中 | 中 | 防抖保存在 `LayoutChanged` 时触发；退出时同步立即刷盘，禁止 `await` |
| **自适应网格在拖窗口时反复重建 → 卡顿** | 高 | 中 | `Reflow` 内早退（cols/rows 未变直接 return）+ 100 ms 防抖 + 对象池复用格子 |
| **`GridContainer.Columns` 首帧为 0，算出单列** | 高 | 中 | 首次 `Reflow` 走 `CallDeferred`，或等 `Resized` 首次触发 |
| **小地图每帧重建纹理 → 掉帧** | 中 | 高 | 只在 `TerrainChanged` / `MapChanged` 后标脏，下一帧重建一次；优先按 chunk 局部重建 |
| **小地图与主视图调色板不一致** | 中 | 中 | 共用 `Render/TerrainPalette.cs`；小地图**只读**该表，禁止本地再定义一份颜色 |
| **`Render/` 只读例外被滥用** | 中 | 高 | 例外白名单只有 `TerrainPalette.cs` 一个文件；review 时检查它不含节点、不含渲染逻辑 |
| **`TimelineStrip` 与 `BottomPalette` 折叠状态不同步** | 中 | 中 | 播放条是 `BottomPalette` 的 `row 0`，同属一个容器，折叠由容器统一驱动，**禁止做成独立顶层节点** |

---

## 13. 落地节奏（建议）

| 周次 | 任务 | 产出 |
| --- | --- | --- |
| W1 | T1 + T2 + T9 | map-first 主框架可启动；底部三行骨架就位；暗色主题 |
| W2 | T11 + T3 + T4 | `BottomPalette` 行列体系；面板迁出；面板可拖动 + 四边 dock + 浮动 |
| W3 | T12 + T5 + T6 | 自适应网格 + 分页；内容拖放；布局持久化 |
| W4 | T13 + T14 + T7 + T8 + T10 | 小地图；播放条骨架；全屏；palette 折叠；验收录屏 |

**依赖**：本 spec 通过用户评审 → W1 开工。

---

## 14. 变更记录

| 版本 | 日期 | 变更 |
| --- | --- | --- |
| v0.1 | 2026-10-08 | 初版：基于 `docs/界面与交互规格_v0.1.md` 与现有 `Main.tscn`，设计 SimCity / RTS 风格的 map-first + 可拖放 UI 重设计 |
| v0.2 | 2026-10-08 | 架构收敛为**单条 `BottomPalette`**：① 功能集中底部，palette 内部分 **rows → sections** 两层；② 建筑面板由固定 3×3 九宫格改为**自适应网格**（列数 / 行数运行时推导，`items_per_page` 不再落盘），保留分页；③ 小地图**移入 palette 左侧 section**，由范围外（原 N4）改为 M2 交付，新增 `Render/TerrainPalette.cs` 只读例外；④ 事件播放条定为 **palette 的 row 0**（满宽、双态 28/56、时间模式 120 overlay），`TopStrip` 移除时间文本；⑤ 新增 G10–G13、T11–T14、D14–D24；⑥ G1 口径重写为「展开 ≥ 75% / 折叠 ≥ 90%」；⑦ 修正「`docs/总体设计_v0.1.md` §3.4」不存在的问题 |