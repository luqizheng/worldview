# UI 重新设计 — SimCity / RTS 风格（v0.1 增量）

> **目标**：把当前 4 区 IDE 风格的 `Main.tscn` 重做成「地图占满 + 面板可拖放」的可玩编辑器。
> **范围**：仅改 UI 与场景树；不动 `Domain/` 数据契约、不动 `Storage/`、不动 `Render/` 渲染核心。
> **栈**：Godot 4.7.2 mono + C#（沿用既有技术基调）。
> **位置**：本规格属任务级规格，存 `.trae/specs/ui-redesign-simgame/`；设计层只在 `docs/界面与交互规格_v0.1.md` 末尾追加「v0.1 (补丁：面板可拖放)」一行，并把这个 spec 引用过去。
> **纪律**：
>
> - UI 状态（布局 / 显隐 / 折叠）属于 UI 私有，**不**进数据模型，不进 `project.yaml`。
> - 面板的拖放行为**只**改变 UI 状态，不修改任何地图 / 图层 / 文档数据。
> - Godot / C# / .NET 专有词只出现在 `scripts/Ui/` 与 `.tscn` 里，不污染 `docs/`。

---

### 1. Why（为什么要做）

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

### 1.3 用户诉求

> 主要画面应该是地图，有点像模拟城市，rts 游戏，主要是地图。其他事 drag & drop 的方式放置的。

翻译为产品需求：

1. **默认打开 → 地图占满**（RTS / SimCity 视感）。
2. **所有面板可拖放**：拖到屏幕四边 → 停靠；拖到中间 → 浮动；按一个键 → 全屏地图模式。
3. **面板可关闭 / 折叠 / 重组**。
4. **布局可记忆**：下次启动恢复上次布局。

---

## 2. Goals（验收指标）

| 编号 | 目标                                                       | 验收手段               |
| ---- | ---------------------------------------------------------- | ---------------------- |
| G1   | 默认 1920×1080 下画布可视区 ≥ 85%                          | 截图 + 像素测量        |
| G2   | 工具栏 / 检视器 / 图层面板 / 建筑面板 均可被拖动           | 手动拖动录屏           |
| G3   | 拖到屏幕左 / 右 / 上 / 下边 → 自动停靠                     | 手动拖动录屏           |
| G4   | 拖到中央空白 → 成为浮动 Window                             | 手动验证               |
| G5   | 拖建筑图标到地图 → 在落点放置                              | 单元 + 集成            |
| G6   | 关闭 / 重启 → 上次布局恢复                                 | 退出 → 启动比对        |
| G7   | F11 → 全屏地图模式（隐藏所有面板）                         | 手动验证               |
| G8   | 底栏可折叠成单行图标                                       | 手动验证               |
| G9   | 编辑器核心（笔刷 / 撤销 / 切换工具 / 保存 / 时间）功能不变 | 既有 D1–D8 demo 仍通过 |

---

## 3. Layout 设计

### 3.1 默认打开的样子

```
+================================================================+
+================================================================+
|  TopStrip (32px)                                                |
|  [Open] [Save] | <面包屑> | 时钟 创世历 1042-3-12 | [撤销] [重做] [全屏] |
+---+-----------+----------------------------------+------------+
|   |           |                                  |            |
| L |           |                                  | R          |
| e |           |                                  | i          |
| f |           |                                  | g          |
| t |           |                                  | h          |
|   |           |                                  | t          |
|Do |           |          MAP CANVAS               | Do         |
|ck|           |        （占据主视口）              | ck         |
|   |           |                                  |            |
| B |           |                                  | In         |
| u |           |                                  | sp         |
| i |           |                                  | ec         |
| l |           |                                  | tor        |
| d |           |                                  |            |
|   |           |                                  | Lay        |
| P |           |                                  | ers        |
| a |           |                                  |            |
| l |           |                                  |            |
| e |           |                                  |            |
| t |           |                                  |            |
| t |           |                                  |            |
| e |           |                                  |            |
+---+-----------+----------------------------------+------------+
|  BottomStrip (24px)                                             |
|  工具:V/B/P  缩放:1.0x  坐标:(32,24) 选中:- 提示:就绪        |
+================================================================+
```

但**默认状态**下 LeftDock / RightDock 处于**折叠**状态（仅留一个 TabBar），实际可视区域：

```
+================================================================+
|  TopStrip (32px)                                                |
+---+---+--------------------------------------------+---+------+
| L | ^ |                                            | R |  ^   |
| e | | |                                            | i |  |   |
| f | v |                                            | g |  v   |
| t |   |           MAP CANVAS                       | h |      |
|   |   |                                            | t |      |
| ^ |   |                                            | ^ |      |
| | |   |                                            | | |      |
| v |   |                                            | v |      |
+---+---+--------------------------------------------+---+------+
|  BottomStrip (24px)                                             |
+================================================================+
```

收起 LeftDock / RightDock 后，画布实际可视面积 ≈ 95%（仅留 32px 顶栏 + 24px 底栏 + 左右各 24px dock handle）。

### 3.2 浮动状态（用户拖出来）

```
+================================================================+
|  TopStrip (32px)                                                |
|                              [浮动的 BuildPalette]              |
|                              +--------------------+             |
|                              |  House          x |             |
|        MAP CANVAS             |  Castle         x |             |
|       （约 95% 视口）         |  Tree           x |             |
|                              | ...                |             |
|                              +--------------------+             |
|                                                                |
|                                          [浮动的 Inspector]    |
|                                          +---------+           |
|                                          | 地图    x|           |
|                                          | 世界 Kael|          |
|                                          | ...     |           |
|                                          +---------+           |
+================================================================+
|  BottomStrip (24px)                                             |
+================================================================+
```

### 3.3 全屏模式（F11）

```
+================================================================+
|                                                                |
|                                                                |
|                                                                |
|                  MAP CANVAS（100% 视口）                       |
|                                                                |
|                                                                |
|                                                                |
+================================================================+
```

### 3.4 视觉风格（SimCity / RTS 暗色）

- 主背景：`Color(0.08, 0.08, 0.10)`（接近纯黑，比原 `0.13, 0.13, 0.16` 更暗）
- 面板背景：`Color(0.12, 0.12, 0.15, 0.92)`（半透明，地图能透出一点）
- 面板描边：`Color(0.30, 0.30, 0.36, 1)` 1px
- 选中 / hover：`Color(0.45, 0.55, 0.85, 1)` 高亮边
- 文字：主 `#E6E6E6`，次 `#9090A0`，警告 `#E6B450`，错误 `#E66B4B`
- 圆角：4 px（用 `StyleBoxFlat`）
- 字体：Godot 默认 + 等宽备用
- 暗色主题资源：`scripts/Ui/Theme/Dark.tres`

---

## 4. 拖放机制

### 4.1 面板拖放（panel 与 dock zone）

**发起**：用户抓住面板顶部的 16px drag handle，按住左键移动 ≥ 4 px，触发 drag。

- 面板发起 `_GetDragData(Vector2 atPosition)` → 返回 `Variant.From(new PanelDragData { PanelId = "...", SourceZone = "..." })`
- 同时面板把自己 `Hide()`，生成一个 `DragPreview`（`Control`，宽高与原面板一致）跟随光标

```csharp
public partial class PanelDragData : RefCounted
{
    public string PanelId { get; set; } = "";
    public string SourceZone { get; set; } = "";
}
```

**接受方**：DockLayout 内有 5 个 DockZone 节点（left / right / top / bottom / center-float）。

- 每个 DockZone 监听 `_CanDropData()`：若 payload 类型 == `PanelDragData` 则返回 true
- 鼠标进入 zone → zone 显示高亮（半透明蓝边框）
- 鼠标离开 zone → 取消高亮

**释放**：`_DropData(Vector2 atPosition, Variant data)`：

- 当前 hover 的 zone 处理：
  - `EdgeDock(Left|Right|Top|Bottom)` → 把面板节点挂到对应 dock 的 `VBoxContainer`/`HBoxContainer` 中，按 dock 内顺序追加
  - `CenterFloat` → 把面板从原 dock 树移除，挂到一个 `Window` 节点下；Window 位置 = `atPosition  - dragHandleOffset`，大小 = 面板当前 size
- DragPreview 销毁
- 触发 `LayoutChanged` 信号（持久化监听）

### 4.2 内容拖放（palette 与 map canvas）

**发起**：建筑面板里的 `BuildingButton` 触发 `_GetDragData()`：

```csharp
var preview = new TextureRect { Texture = GetPreviewTexture(buildingType) };
SetDragPreview(preview);
return Variant.From(new BuildingDragData { BuildingType = ..., MapId = context.ActiveMapId });
```

地形调色板的 TerrainSwatch 同样：Variant.From(new TerrainDragData { TerrainType = ..., Brush = ..., Radius = ... })。
两者 payload 类同样必须 RefCounted + partial。

**接受方**：`WorldCanvas`监听：

- `_CanDropData(Vector2 atPosition, Variant data)`：`data.As<BuildingDragData>()` 或 `data.As<TerrainDragData>()` → true
- `_DropData(Vector2 atPosition, Variant data)`
  atPosition 是全局屏幕坐标，必须先转换到画布局部坐标再换算格子：

```csharp
// atPosition：全局屏幕坐标
var xform = GetGlobalTransformWithCanvas();
var local = xform.AffineInverse() * atPosition;
var cell  = LocalToCell(local);
// 走命令模式，不绕过 UndoStack
BuildingPlaceController.TryPlace(cell);   // 或 ApplyBrushCommand(cell)
```

漏掉这一步，拖放落点会偏移（尤其画布有平移 / 缩放时）。

**关键**：拖放放下时仍然走 UndoStack，不绕过命令模式（与现有 MVP 一致）

### 4.3 Godot 4 API 用法

- Control.\_GetDragData(Vector2 atPosition) → Variant
- Control.\_CanDropData(Vector2 atPosition, Variant data) → bool
- Control.\_DropData(Vector2 atPosition, Variant data) → void
- Control.SetDragPreview(Control preview) — 参数是 Control，不是 Texture2D
- Control.ForceDrag(Variant data, Control preview) — 手动触发拖放
- Window 用于浮动面板（Transient = true、Exclusive = false、Unresizable = false）
- AcceptDialog / Popup 用于模态
- 拖动判定：\_GuiInput + (e.ButtonMask & MouseButtonMask.Left) != 0，位移阈值 4 px
  - [FIX] 枚举是 MouseButtonMask.Left，不是 MouseButton.Left

---

## 5. 数据结构

### 5.1 **不**进数据契约

面板布局 / 浮动状态 / 可见性 / 折叠状态**只**是 UI 状态，**不**写进 `MapDoc` / `World` / `Project`。

理由（与 `docs/界面与交互规格_v0.1.md §6` 一致）：

- 数据契约只承载领域事实（地图、图层、地形、人物、事件）；
- 视图状态 / 工具状态 / 布局状态属于 UI 层；
- 关闭工程 / 切工程时 UI 状态按工程隔离；切回工程时还原。

多工程隔离：采用按工程分文件的策

```text
.worldview/layouts/<projectId>.yaml
```

projectId 从 EditorContext.ActiveProject.Id 取（或工程根目录名做 fallback）。切换工程时切换加载的文件；无对应文件则用默认布局。这样避免多工程布局互相覆盖，也避免单文件跨工程匹配失效。

### 5.2 UI 私有文件：`.worldview/layouts/<projectId>.yaml`

存到运行时私有目录（.worldview/ 已在 .gitignore），不进版本库。

```yaml
# 由 DockLayout 自动序列化，反序列化时容错
schema_version: 1
project_id: sample_world
panels:
  - id: top_strip
    kind: dock_edge
    edge: top
    visible: true
    height: 32

  - id: bottom_strip
    kind: dock_edge
    edge: bottom
    visible: true
    height: 24
    collapsed: false

  - id: left_dock
    kind: dock_container
    edge: left
    visible: true
    collapsed: true # 默认折叠，只露 DockHandle 条
    width: 200
    children:
      - id: tool_palette
        kind: dock_child
        order: 0

  - id: right_dock
    kind: dock_container
    edge: right
    visible: true
    collapsed: true
    width: 240
    children:
      - id: inspector
        kind: dock_child
        order: 0
      - id: layer_list
        kind: dock_child
        order: 1

  - id: build_palette
    kind: float # 浮动形态，不再出现在 left_dock.children
    x: 1200
    y: 200
    width: 200
    height: 360
    visible: true

  - id: brush_panel
    kind: float
    x: 100
    y: 400
    width: 220
    height: 180
    visible: true
```

**写时机**:
1， 每次 LayoutChanged 信号后，设置脏标记 + 启动一次性 Timer（500 ms 防抖），超时则同步写盘。
2， 每次退出时， DockLayout 的 \_ExitTree 里：同步（不 await）立即写一次，防止退出丢布局。
3， 若需要更稳妥，可在 \_Notification(NotificationWMCloseRequest) 里拦截退出流程：GetTree().AutoAcceptQuit = false; → 保存 → GetTree().Quit()。

**读时机**:
1， 启动时若 .worldview/layouts/<projectId>.yaml 存在且 schema_version 匹配 → 还原；否则用默认布局，不报错。

### 5.3 字段汇总（仅 UI 层私有）

| 字段              | 类型                                                         | 说明                                                |
| ----------------- | ------------------------------------------------------------ | --------------------------------------------------- |
| `kind`            | enum `dock_edge` / `dock_container` / `dock_child` / `float` | 面板存在形态                                        |
| `edge`            | enum `top` / `bottom` / `left` / `right`                     | 停靠边（仅 dock_edge / dock_container 有）          |
| `x`, `y`          | int                                                          | 浮动窗口位置（仅 float 有）                         |
| `width`, `height` | int                                                          | 面板尺寸                                            |
| `visible`         | bool                                                         | 是否显示                                            |
| `collapsed`       | bool                                                         | 是否折叠（仅 dock_container 有，折叠后只露 TabBar） |
| `order`           | int                                                          | 在 dock 内的子顺序（仅 dock_child 有）              |
| `id`              | string                                                       | 面板稳定 ID（如 `tool_palette`），跨会话复用        |
| `schema_version`  | int                                                          | 顶层字段；不匹配时回退默认布局）                    |
| `project_id`      | string                                                       | 顶层字段；用于多工程隔离校验                        |

---

## 6. 文件 / 目录变更

```
world-view/
├── scenes/
│   ├── Main.tscn                    [重写] map-first 布局
│   ├── WorldCanvas.tscn             [不变]
│   ├── ui/
│   │   ├── DockLayout.tscn          [新增] 主停靠根节点
│   │   ├── DockPanel.tscn           [新增] 可停靠面板通用 scene
│   │   ├── DockZone.tscn            [新增] 边缘 dock 区预览
│   │   ├── TopStrip.tscn            [新增] 顶栏
│   │   ├── BottomStrip.tscn         [新增] 底栏
│   │   ├── ToolPalette.tscn         [新增] 工具 / 笔刷 / 建筑面板
│   │   ├── Inspector.tscn           [新增] 检视器面板
│   │   └── LayerPanel.tscn          [新增] 图层面板
│   └── ui/preview/                  [新增] 拖放 ghost 预览纹理
│       ├── building_preview.png
│       └── terrain_preview.png
│
├── scripts/
│   ├── Editor/
│   │   ├── EditorController.cs      [大改] 瘦身；只负责全局信号 + 生命周期
│   │   ├── EditorContext.cs         [小改] 加 `LayoutChanged` 信号
│   │   └── ToolState.cs             [不变]
│   ├── Ui/
│   │   ├── Dock/                    [新增]
│   │   │   ├── DockLayout.cs        主停靠容器（5 个 DockZone + dock tree）
│   │   │   ├── DockPanel.cs         可停靠面板基类（标题栏、drag、close）
│   │   │   ├── DockZone.cs          dock 接受区（_CanDropData / _DropData）
│   │   │   ├── FloatWindow.cs       浮动 Window 包装
│   │   │   ├── PanelState.cs        序列化 / 反序列化
│   │   │   ├── PanelDragData.cs     payload（: RefCounted, partial）
│   │   │   ├── BuildingDragData.cs  payload（: RefCounted, partial）
│   │   │   └── TerrainDragData.cs   payload（: RefCounted, partial）
│   │   ├── TopStrip.cs              [新增] 顶栏
│   │   ├── BottomStrip.cs           [新增] 底栏
│   │   ├── ToolPalette.cs           [从 EditorController 抽出]
│   │   ├── Inspector.cs             [从 EditorController 抽出]
│   │   └── LayerPanel.cs            [从 EditorController 抽出]
│   └── Editing/
│       └── (不变)
│
├── scripts/Render/
│   ├── WorldCanvas.cs               [小改] 加 _CanDropData / _DropData 处理内容拖放
│   └── (其他不变)
│
└── .worldview/
    └── layouts/                      [运行时生成] 每工程一份布局
        └── <projectId>.yaml
```

**新增目录**：

- `scripts/Ui/`：UI 层（Godot 依赖，C#）
- `scripts/Ui/Dock/`：停靠系统
- `scenes/ui/`：UI 场景（与 `scenes/WorldCanvas.tscn` 同级）

**不**修改：

- `scripts/Domain/**`
- `scripts/Storage/**`
- `scripts/Editing/**`
- `scripts/Render/**` 除 WorldCanvas.cs（该文件仅加 drag handler）
- `scripts/Editor/EditorContext.cs`（仅加 LayoutChanged 信号）/ `ToolState.cs` / `UndoStack.cs`
- `examples/sample_world/**`
- 任何 `docs/*.md`（除 `界面与交互规格_v0.1.md` 文末追加 1 行引用）

---

## 7. 任务分解（DoD：每条任务对应一个可验证的产出）

### T1 — DockLayout 骨架（M2 起点）

- [ ] 新建 scripts/Ui/Dock/DockLayout.cs（partial Control 节点）
- [ ] 新建 scripts/Ui/Dock/DockPanel.cs（partial Control，含标题栏 + close 按钮 + drag handle）
- [ ] 新建 scripts/Ui/Dock/DockZone.cs（partial Control，5 个 dock 接受区；MouseFilter = Stop）
- [ ] 新建 scripts/Ui/Dock/FloatWindow.cs（Window 包装；Transient = true, Exclusive = false, Unresizable = false）
- [ ] 新建 scripts/Ui/Dock/PanelState.cs（serialize / deserialize YAML）
- [ ] 新建 scripts/Ui/Dock/PanelDragData.cs、BuildingDragData.cs、TerrainDragData.cs（全部 public partial class ... : RefCounted）
- [ ] EditorContext 加信号 [Signal] public delegate void LayoutChangedEventHandler();
      DoD：dotnet build 通过；空 DockLayout 场景在 Godot 编辑器能加载；LayoutChanged 可 emit。

### T2 — 重写 Main.tscn 为 map-first

- [ ] `scenes/Main.tscn` 改为：`DockLayout` 根 + `WorldCanvas` 占满主视口
- [ ] 顶栏 TopStrip（32px）+ 底栏 BottomStrip（24px）作为 `dock_edge` 不可拖动
- [ ] 左右 DockContainer 默认折叠
- **DoD**：F5 启动后画布可视区域 ≥ 85%（G1）。

### T3 — 抽出 ToolPalette / Inspector / LayerPanel

- [ ] `scripts/Ui/ToolPalette.cs`：从 EditorController 抽出 `RebuildTerrainBtns` / `RebuildBuildingBtns` / `RebuildBrushBtns`
- [ ] `scripts/Ui/Inspector.cs`：抽出 `RebuildInspector`
- [ ] `scripts/Ui/LayerPanel.cs`：抽出 `RebuildLayerList`
- **DoD**：三个面板在 Main.tscn 实例化后功能与重构前一致（D1 / D2 / D6 demo 仍通过）。

### T4 — 面板拖放

- [ ] `DockPanel._GuiInput`：按住标题栏左键 4 px 后调用 `ForceDrag`
- [ ] `DockPanel._GetDragData`：返回 `PanelDragData`
- [ ] `DockPanel.SetDragPreview`：生成 ghost Control
- [ ] `DockZone._CanDropData` / `_DropData`：判定 + 重新挂载
- **DoD**：手动录屏证明左 / 右 / 上 / 下 dock 与浮动（G2 / G3 / G4）。

### T5 — 内容拖放（建筑 / 地形）

- [ ] `ToolPalette` 内的 `BuildingButton._GetDragData`：返回 `BuildingDragData`
- [ ] `ToolPalette` 内的 `TerrainSwatch._GetDragData`：返回 `TerrainDragData`
- [ ] `WorldCanvas._CanDropData` / `_DropData`：分别走 `BuildingPlaceController.TryPlace` / `ApplyBrushCommand`
- **DoD**：拖建筑到地图 → 放置；拖地形到地图 → 涂（G5）。

### T6 — 布局持久化

- [ ] `PanelState.Save(string rootPath)`：写 `.worldview/ui_layout.yaml`
- [ ] `PanelState.Load(string rootPath)`：读并容错（缺字段用默认）
- [ ] `DockLayout._ExitTree`：触发保存（防抖 500 ms）
- [ ] `DockLayout._Ready`：启动时还原
- **DoD**：手动调整布局 → 关闭 → 重启 → 布局恢复（G6）。

### T7 — 全屏模式

- [ ] `TopStrip` 加全屏按钮 + `F11` 快捷键
- [ ] 切换时 `DockLayout.SetAllPanelsVisible(bool)` + 隐藏 TopStrip / BottomStrip
- [ ] 退出全屏恢复上次可见性快照
- **DoD**：F11 切换全屏（G7）。

### T8 — 底栏可折叠

- [ ] `BottomStrip` 默认展开 24px，折叠成 24px 图标单行（仅显示工具状态）
- [ ] 折叠 / 展开按钮在底栏右上角
- **DoD**：手动验证（G8）。

### T9 — 暗色主题资源

- [ ] `scripts/Ui/Theme/Dark.tres`（Godot Theme 资源）
- [ ] 全局应用到 Main.tscn 根
- **DoD**：所有 UI 元素使用同一主题；颜色与 §3.4 一致。

### T10 — 验收模拟录屏

- [ ] 在 Godot 4.7.2 mono F5，按既有 D1–D8 走一遍，截图保存到 `.worldview/ui_redesign_demos/`
- [ ] 新增 D9：拖放演示（录屏 + 截图）
- **DoD**：D1–D8 demo 全部仍通过；D9 拖放验证通过（G9）。

---

## 8. 验收标准（DoD 汇总）

### 8.1 功能验收

- [ ] D1：启动 + 打开示例工程 → 地图占 ≥ 85%（G1）
- [ ] D2：原 D2 demo 仍通过：切地形类型 + 笔刷绘制（G9）
- [ ] D3 / D4：原 D3 / D4 demo 仍通过：撤销 / 重做（G9）
- [ ] D5：原 D5 demo 仍通过：双击城市下钻
- [ ] D6：原 D6 demo 仍通过：建筑放置（通过面板按钮**或**拖放）
- [ ] D7：原 D7 demo 仍通过：保存 + 重启一致性
- [ ] D8：原 D8 demo 仍通过：校验失败弹窗
- [ ] **D9 新增**：拖建筑图标到地图 → 放置（G5）
- [ ] **D10 新增**：拖面板到四边 → 停靠（G3）
- [ ] **D11 新增**：拖到中央 → 浮动（G4）
- [ ] **D12 新增**：F11 全屏切换（G7）
- [ ] **D13 新增**：布局持久化（G6）

### 8.2 工程验收

- [ ] `dotnet build WorldView.sln` 成功
- [ ] Godot 4.7.2 mono `--headless --check-only` 通过
- [ ] `scripts/Domain/` 与 `scripts/Storage/` 仍**无** `using Godot;`
- [ ] `.worldview/ui_layout.yaml` 不进版本库（已 `gitignored`）
- [ ] 现有 `docs/` 任何字段定义**未**被修改

---

## 9. 范围外（明确不做）

| 编号 | 不做                                             | 理由                                                      |
| ---- | ------------------------------------------------ | --------------------------------------------------------- |
| N1   | 面板同位置合并为 Tab 群组                        | T3 / T4 先做单 panel；多 panel 同位置 v0.1 简化用纵向堆叠 |
| N2   | 浮动面板拖出主窗口                               | Godot 的 Window 可独立但与主窗口无视觉协调，v0.1 不开     |
| N3   | 主题切换 / 多套皮肤                              | 暗色一套够用；切换留到主题系统                            |
| N4   | 缩略地图（minimap）                              | M3+ 再加，本轮只把现有地图区放大                          |
| N5   | 时间轴面板的可拖放                               | 时间轴（M6）还没做；本轮 TopStrip 仅显示时间              |
| N6   | 面板搜索 / 命令面板                              | v0.1 不开                                                 |
| N7   | 修改 `Domain/` / `Storage/` / `Render/` 数据契约 | 数据契约中立；本轮只动 UI 层                              |
| N8   | 多工程同窗口                                     | M7+；本轮一个工程一个布局                                 |

---

## 10. 变更纪律（与 `AGENTS.md §2 / §7` 对齐）

- [ ] 不污染第七条设计原则（特别是「目录即工程」「YAML 为主格式」）
  - UI 布局不进 `project.yaml`，只进 `.worldview/ui_layout.yaml`
- [ ] 框架专有概念只出现在 `scripts/Ui/` 与 `.tscn` 内
  - 不污染 `Domain/` 与 `Storage/`
- [ ] 既有 `EditorController.cs` 信号**不**删，只加 `LayoutChanged`
- [ ] 数据契约**不**动；本 spec 的字段表 §5.2 全部是 UI 私有
- [ ] `examples/sample_world/` 不改；D7 demo 不变
- [ ] 既有 D1–D8 demo 必须继续通过

---

## 11. 文档同步

| 文档                                           | 改动                                                         |
| ---------------------------------------------- | ------------------------------------------------------------ |
| `AGENTS.md` §6.2 进度                          | 追加「UI redesign (M2)」一行；本 spec 已落盘                 |
| `docs/界面与交互规格_v0.1.md` 文末「变更记录」 | 追加一行「v0.1 (补丁：面板可拖放)」并引用本 spec             |
| `docs/总体设计_v0.1.md` §3.4 UI 层             | 把「固定左右栏」改为「DockLayout + 可拖放面板」；引用本 spec |
| `.trae/specs/map-editor-mvp/checklist.md`      | 不改（本轮不动 MVP 任务清单）                                |

**不**改：

- `docs/数据模型规格_v0.1.md`（数据契约中立）
- `docs/存储与目录规范_v0.1.md`
- `docs/二进制格式规格_v0.1.md`
- `docs/地图类型规格_v0.1.md`
- `docs/地图编辑器规格_v0.1.md` §1 / §2 / §3（编辑能力不变，只换容器）
- `docs/图层系统规格_v0.1.md`
- `docs/时间轴系统规格_v0.1.md`
- `docs/事件与人物登记规格_v0.1.md`

---

## 12. 风险与对策

| 风险                                              | 概率 | 影响 | 对策                                                                                                                                              |
| ------------------------------------------------- | ---- | ---- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| 拖放性能在画布大时抖动                            | 低   | 中   | WorldCanvas 只在 `_CanDropData` 命中后做 hit-test；预览 Control 用 TextureRect 缓存                                                               |
| Godot 的 Window 在不同平台焦点不同                | 中   | 低   | 浮动面板默认 `Transient = true`、`Exclusive = false`；W11 / macOS / Linux 各跑一次手动验收                                                        |
| 布局文件与工程版本错位                            | 中   | 中   | `schema_version` 字段；不匹配时回退到默认布局，不报错                                                                                             |
| 既有 Demo（D1–D8）破坏                            | 低   | 高   | T3 / T10 阶段每次跑一遍既有 demo，回归失败立即停                                                                                                  |
| 用户期望的拖放粒度与本文不一致                    | 中   | 中   | 本 spec 落地前先让用户评审 §3–§5；任何偏离都先改 spec 再写代码                                                                                    |
| C# payload 类忘记 `partial` / 未继承 `RefCounted` | 中   | 高   | `PanelDragData` / `BuildingDragData` / `TerrainDragData` 必须 `public partial class X : RefCounted`；否则 `Variant.From(...)` 失败或拖放静默 null |
| 拖放落点未换算坐标系                              | 中   | 高   | `_DropData` 里必须先 `GetGlobalTransformWithCanvas().AffineInverse() * atPosition`，再 `LocalToCell(local)`                                       |
| `_ExitTree` 里 `await` 保存导致丢失               | 中   | 中   | 防抖保存在 `LayoutChanged` 时触发；退出时同步立即刷盘，禁止 `await`                                                                               |

---

## 13. 落地节奏（建议）

| 周次 | 任务          | 产出                                          |
| ---- | ------------- | --------------------------------------------- |
| W1   | T1 + T2 + T9  | map-first 主框架可启动；画布占 85%+；暗色主题 |
| W2   | T3 + T4       | 现有面板迁出；面板可拖动 + 四边 dock + 浮动   |
| W3   | T5 + T6       | 内容拖放；布局持久化                          |
| W4   | T7 + T8 + T10 | 全屏模式 + 底栏折叠 + 验收录屏                |

**依赖**：本 spec 通过用户评审 → W1 开工。

---

## 14. 变更记录

| 版本 | 日期       | 变更                                                                                                                |
| ---- | ---------- | ------------------------------------------------------------------------------------------------------------------- |
| v0.1 | 2026-10-08 | 初版：基于 `docs/界面与交互规格_v0.1.md` 与现有 `Main.tscn`，设计 SimCity / RTS 风格的 map-first + 可拖放 UI 重设计 |
