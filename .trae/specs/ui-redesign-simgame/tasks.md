# UI Redesign — SimCity/RTS 任务清单（v0.3）

> 与 [spec.md](spec.md) 一一对应。每条 T# 都给出最小可验证产出。
> 栈：Godot 4.7.2 mono + C#。依赖 [map-editor-mvp/tasks.md](../map-editor-mvp/tasks.md) 已完成（T1–T24）。
> **API 签名以 Godot 4.7 为准**：`_GetDragData(Vector2)` / `_CanDropData(Vector2, Variant)` / `_DropData(Vector2, Variant)`。

---

## 阶段 W1 — map-first 骨架

### T1 — DockLayout 系统骨架

- `scripts/Ui/Dock/DockLayout.cs`：Control 子类；管理 5 个 DockZone
- `scripts/Ui/Dock/DockPanel.cs`：Control 子类；标题栏 + close + drag handle
- `scripts/Ui/Dock/DockZone.cs`：Control 子类；`_CanDropData` / `_DropData` / 高亮；`MouseFilter = Stop`
- `scripts/Ui/Dock/FloatWindow.cs`：Window 子类；`Transient = true`, `Exclusive = false`, `Unresizable = false`
- `scripts/Ui/Dock/PanelState.cs`：YAML 序列化（`schema_version` / `project_id` 字段）
- `scripts/Ui/Dock/PanelDragData.cs`：`public partial class PanelDragData : RefCounted`
- `scripts/Ui/Dock/BuildingDragData.cs`：`public partial class BuildingDragData : RefCounted`
- `scripts/Ui/Dock/TerrainDragData.cs`：`public partial class TerrainDragData : RefCounted`
- `EditorContext` 加 `[Signal] public delegate void LayoutChangedEventHandler();`

**DoD**：`dotnet build` 通过；空 DockLayout 场景可加载；`LayoutChanged` 可 emit。

### T2 — 重写 Main.tscn（map-first + BottomPalette）

- `scenes/Main.tscn` 改为 `DockLayout` 根 + `WorldCanvas` 占满主视口
- `TopStrip` 32px（**移除时间文本**，时间唯一由 `timeline_row` 承载）
- `BottomPalette`（`dock_container`, `edge=bottom`, `height=200`）
- 左右 Dock 默认完全收起
- `BottomPalette` 三行骨架就位：`timeline_row` 28 / `build_row` flex / `tool_row` 20

**DoD**：F5 后 1920×1080 下画布可视 ≥ 75%（展开态）。

### T9 — 暗色主题

- `scripts/Ui/Theme/Dark.tres`
- `Main.tscn` 根节点挂主题
- 建筑格 `StyleBoxFlat`：圆角 4、padding 8、hover 高亮

**DoD**：所有 UI 元素应用同一主题；颜色与 [spec §3.9](spec.md) 一致。

---

## 阶段 W2 — BottomPalette 行列体系 + 面板迁出 + 拖放

### T11 — BottomPalette 行 / 节容器

- `scripts/Ui/Palette/BottomPalette.cs`：`rows` 纵向管理 + `collapsed` 切换（200 ↔ 40）
- `scripts/Ui/Palette/PaletteRow.cs`：`full_width` / `horizontal` 两种行布局
- `scripts/Ui/Palette/PaletteSection.cs`：`size_mode`（`fixed` / `flex` / `auto`）+ 隐藏
- `scenes/ui/BottomPalette.tscn` + `TimelineStrip.tscn` + `ToolStrip.tscn`
- **`timeline_row` 必须是 `BottomPalette` 的 row 0，不能做成独立顶层节点**（否则折叠会错位）

**DoD**：三行正确堆叠；调整 `rows` 顺序 → 界面随之变化。

### T3 — 抽出 ToolStrip / Inspector / LayerPanel

- `scripts/Ui/Palette/ToolStrip.cs`（进 `tool_row`）
- `scripts/Ui/Inspector.cs`（默认浮动、默认隐藏）
- `scripts/Ui/LayerPanel.cs`（默认浮动、默认隐藏）
- `EditorController` 瘦身：移除三个面板的构建代码

**DoD**：D1 / D2 / D6 demo 仍通过。

### T4 — 面板拖放

- `DockPanel._GuiInput`：标题栏 `(e.ButtonMask & MouseButtonMask.Left) != 0` 且位移 ≥ 4px → `ForceDrag(data, preview)`
- `DockPanel._GetDragData(Vector2 atPosition)` → `Variant.From(new PanelDragData{...})`
- `DockPanel.SetDragPreview(Control preview)`（`TextureRect` 包装，不是 `Texture2D`）
- `DockZone._CanDropData(Vector2, Variant)` / `_DropData(Vector2, Variant)`：挂载到 dock 树
- `BottomPalette` 落点规则：section 空缺位 → 插入该行；行间隙 → 新建行；`timeline_row` 拒收非 TimelineStrip

**DoD**：G2 / G3 / G4 手动录屏。

---

## 阶段 W3 — 自适应网格 + 内容拖放 + 布局持久化

### T12 — 自适应网格 + 分页

- `scripts/Ui/Palette/AdaptiveGrid.cs`：`Reflow()` + 100ms 防抖 + **cols/rows 未变直接早退**
- `scripts/Ui/Palette/BuildPaletteSection.cs`：分类 `TabBar` + 网格 + 分页器 + `pages_by_category`
- 六条硬约束全部落实（[spec §3.5.1](spec.md)）：
  1. 首次 `Reflow` 走 `CallDeferred`（`_Ready` 时 `Size` 为 0）
  2. 仅在 cols/rows 变化时重建
  3. 先 `RemoveChild` 再 `QueueFree`，或对象池复用
  4. 每格 `CustomMinimumSize = new Vector2(CellSize, CellSize)`
  5. `PageCount <= 1` 时隐藏分页器
  6. `pages_by_category` 按分类记页码

**DoD**：拉宽/拉窄窗口 → 行列实时重排；`cell_size` 56 → 96 立即重排；分页器按需隐藏。

### T5 — 内容拖放

- `ToolPalette` 内 `BuildingButton` / `TerrainSwatch` 子类
- `BuildingDragData` / `TerrainDragData`（见 T1；`: RefCounted`）
- `WorldCanvas._CanDropData(Vector2, Variant)` / `_DropData(Vector2, Variant)`：
  ```csharp
  var local = GetGlobalTransformWithCanvas().AffineInverse() * atPosition;
  var cell  = LocalToCell(local);
  // 走命令模式，不绕过 UndoStack
  BuildingPlaceController.TryPlace(cell);   // 或 ApplyBrushCommand(cell)
  ```

**DoD**：拖建筑到地图放置；拖地形到地图涂刷。

### T6 — 布局持久化

- `PanelState.Save(rootPath)` / `Load(rootPath)`：写/读 `.worldview/layouts/<projectId>.yaml`
- `Load` 容错：缺字段用默认；`schema_version` 不匹配回退默认布局，**不报错、不删旧文件**
- `DockLayout._ExitTree`：**同步立即**刷盘（禁止 `await`）
- `LayoutChanged` 后 500ms 防抖 Timer → 同步写盘
- `DockLayout._Ready` 启动时还原
- **不落盘**：`items_per_page` / `cols` / `rows` / 主相机 transform

**DoD**：调布局 → 退出 → 重启 → 恢复。

---

## 阶段 W4 — 小地图 / 播放条骨架 / 全屏 / 折叠 / 验收

### T13 — 小地图

- `scripts/Render/TerrainPalette.cs`：地形类型 → RGB **只读常量表**（不含节点、不含渲染逻辑、不写状态）
- `scripts/Ui/Palette/MinimapSection.cs`：由地形格生成 `ImageTexture`（1px/cell，`TextureFilter = Nearest`）
- 视口框：主 `Camera2D` transform → 小地图坐标
- 左键单击 → 主视图以该点为圆心居中；拖动视口框 → 平移主视图
- `M` 键显示 / 隐藏
- 刷新：监听 `TerrainChanged` / `MapChanged` → 标脏 → 下一帧重建（优先按 chunk 局部重建）
- **小地图禁止本地再定义一份调色板**，只读 `TerrainPalette`

**DoD**：D17 显示全图 / D18 点击跳转 / D19 M 键开关 / D20 编辑后 ≤200ms 刷新。

### T14 — TimelineStrip 空骨架

- `scripts/Ui/Palette/TimelineStrip.cs`：播放/暂停 + 时间文本占位 + 空进度轨 + 播放头
- 28 ↔ 56 展开切换（`T` 键）
- 数据源：只读 `EditorContext` 当前时间显示；**不接入时间轴回放逻辑**
- `TopStrip` 移除时间文本

**DoD**：D24 随 `BottomPalette` 一起折叠/展开无错位；`T` 键切换 28/56。

> **边界**：T14 只交付**位置与骨架**。时间轴数据模型、刻度、事件挂载、回放、快照全部属 M6。

### T7 — 全屏模式

- `TopStrip` 加全屏按钮 + `F11` 快捷键
- `DockLayout.SetAllPanelsVisible(bool)` + 隐藏 `TopStrip`/`BottomPalette`
- 保存可见性快照用于还原

**DoD**：F11 切换全屏。

### T8 — BottomPalette 折叠

- `BottomPalette` 展开 200px ↔ 折叠 40px（折叠后只露一行图标）
- 折叠时 `timeline_row` 一起收起（同容器驱动）
- 折叠/展开按钮在 palette 右上角；快捷键 `Ctrl+B`

**DoD**：D23 手动验证，折叠态画布 ≥ 90%；D24 无错位。

### T10 — 验收模拟录屏

- D1–D8 demo 重跑一遍
- D9–D24 全部录屏 / 截图
- 截图保存 `.worldview/ui_redesign_demos/`

**DoD**：D1–D24 全通过。

---

## 范围外

见 [spec.md §9](spec.md)。重点两条：

- **N5**：本轮只交付 `TimelineStrip` 的位置与空骨架，时间轴逻辑归 M6
- **N7**：`Render/` 例外白名单只有 `TerrainPalette.cs` 一个文件