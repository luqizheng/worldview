# UI Redesign — SimCity/RTS 任务清单

> 与 [spec.md](spec.md) 一一对应。每条 T# 都给出最小可验证产出。
> 栈：Godot 4.7.2 mono + C#。依赖 [map-editor-mvp/tasks.md](../map-editor-mvp/tasks.md) 已完成（T1–T24）。

---

## 阶段 W1 — map-first 骨架

### T1 — DockLayout 系统骨架
- `scripts/Ui/Dock/DockLayout.cs`：Control 子类；管理 5 个 DockZone
- `scripts/Ui/Dock/DockPanel.cs`：Control 子类；标题栏 + close + drag handle
- `scripts/Ui/Dock/DockZone.cs`：Control 子类；`_CanDropData`/`_DropData`/高亮
- `scripts/Ui/Dock/FloatWindow.cs`：Window 子类；承载浮动面板
- `scripts/Ui/Dock/PanelState.cs`：YAML 序列化（schema_version 字段）
- `scripts/Ui/Dock/PanelDragData.cs`：record + Variant 互转
- **DoD**：`dotnet build` 通过；空 DockLayout 场景可加载。

### T2 — 重写 Main.tscn
- `scenes/Main.tscn` 改为 DockLayout 根
- TopStrip（32px dock_edge）+ BottomStrip（24px dock_edge）
- WorldCanvas 占满主视口
- 左右 DockContainer 默认折叠
- **DoD**：F5 后 192# UI Redesign — SimCity/RTS 任务清单（v0.2）

> 与 [spec.md](spec.md) 一一对应。每条 T# 都给出最小可验证产出。
> 栈：Godot 4.x mono + C#。依赖 [map-editor-mvp/tasks.md](../map-editor-mvp/tasks.md) 已完成（T1–T24）。
> **API 签名以 Godot 4.x 通用约定为准**：`_GetDragData(Vector2)` / `_CanDropData(Vector2, Variant)` / `_DropData(Vector2, Variant)`。

---

## 阶段 W1 — map-first 骨架

### T1 — DockLayout 系统骨架
- `scripts/Ui/Dock/DockLayout.cs`：Control 子类；管理 5 个 DockZone
- `scripts/Ui/Dock/DockPanel.cs`：Control 子类；标题栏 + close + drag handle
- `scripts/Ui/Dock/DockZone.cs`：Control 子类；`_CanDropData` / `_DropData` / 高亮；`MouseFilter = Stop`
- `scripts/Ui/Dock/FloatWindow.cs`：Window 子类；承载浮动面板；`Transient = true`, `Exclusive = false`, `Unresizable = false`
- `scripts/Ui/Dock/PanelState.cs`：YAML 序列化（`schema_version` / `project_id` 字段）
- `scripts/Ui/Dock/PanelDragData.cs`：`public partial class PanelDragData : RefCounted`
- `scripts/Ui/Dock/BuildingDragData.cs`：`public partial class BuildingDragData : RefCounted`
- `scripts/Ui/Dock/TerrainDragData.cs`：`public partial class TerrainDragData : RefCounted`
- `EditorContext` 加 `[Signal] public delegate void LayoutChangedEventHandler();`
- **DoD**：`dotnet build` 通过；空 DockLayout 场景可加载；`LayoutChanged` 可 emit。

### T2 — 重写 Main.tscn
- `scenes/Main.tscn` 改为 DockLayout 根
- TopStrip（32px dock_edge）+ BottomStrip（24px dock_edge）
- WorldCanvas 占满主视口
- 左右 DockContainer 默认折叠
- **DoD**：F5 后 1920×1080 下画布可视 ≥ 85%。

### T9 — 暗色主题
- `scripts/Ui/Theme/Dark.tres`
- Main.tscn 根节点挂主题
- **DoD**：所有 UI 元素应用同一主题。

---

## 阶段 W2 — 面板迁出 + 拖放

### T3 — 抽出 ToolPalette / Inspector / LayerPanel
- `scripts/Ui/ToolPalette.cs`（继承 DockPanel）
- `scripts/Ui/Inspector.cs`（继承 DockPanel）
- `scripts/Ui/LayerPanel.cs`（继承 DockPanel）
- EditorController 瘦身：移除三个面板的构建代码
- **DoD**：D1 / D2 / D6 demo 仍通过。

### T4 — 面板拖放
- `DockPanel._GuiInput`：标题栏 `(e.ButtonMask & MouseButtonMask.Left) != 0` 且位移 ≥ 4px → `ForceDrag(data, preview)`
- `DockPanel._GetDragData(Vector2 atPosition)` → `Variant.From(new PanelDragData{...})`
- `DockPanel.SetDragPreview(Control preview)`（`TextureRect` 包装，不是 `Texture2D`）
- `DockZone._CanDropData(Vector2 atPosition, Variant data)` / `_DropData(Vector2 atPosition, Variant data)`：挂载到 dock 树
- **DoD**：G2 / G3 / G4 手动录屏。

---

## 阶段 W3 — 内容拖放 + 布局持久化

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
- **DoD**：拖建筑到地图放置；拖地形到地图涂刷。

### T6 — 布局持久化
- `PanelState.Save(rootPath)` / `Load(rootPath)`：写 `.worldview/ui_layout.yaml`
- `DockLayout._ExitTree` 防抖 500ms 保存
- `DockLayout._LayoutChanged` 后 500ms 防抖 Timer → 同步写盘
- `DockLayout._Ready` 启动时还原（容错：缺字段 → 默认）
- **DoD**：调布局 → 退出 → 重启 → 恢复。

---

## 阶段 W4 — 全屏 / 折叠 / 验收

### T7 — 全屏模式
- TopStrip 加 ? 按钮 + F11 快捷键
- `DockLayout.SetAllPanelsVisible(bool)` + 隐藏 TopStrip/BottomStrip
- 保存可见性快照用于还原
- **DoD**：F11 切换全屏。

### T8 — 底栏折叠
- `BottomStrip` 折叠 / 展开按钮
- 折叠后只露 24px 图标行
- **DoD**：手动验证。

### T10 — 验收模拟录屏
- D1–D8 demo 重跑一遍
- D9（拖放）+ D10–D13 录屏
- 截图保存 `.worldview/ui_redesign_demos/`
- **DoD**：D1–D13 全通过。

---

## 范围外
见 [spec.md §9](spec.md)。
