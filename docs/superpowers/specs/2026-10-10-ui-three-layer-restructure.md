# UI 三层依赖重构 (C 轮)

> B+A spec (2026-10-09) §「不修改项」中预留的 C 轮工作。本 spec 把"占位但不细化"的
> 那一段补成可实现的合同。

## 1. 背景

完成 A2 (ToolButton / Menu / Toast / Tooltip / Tab) 和 A4 (SliderField / ColorPickerField /
DateField) 后, scripts/Ui/ 下的代码已经长出大量跨层耦合, 反环依赖与不一致的
namespace 都阻碍后续功能 (例如新增 DockPanel 类型、给 Palette 加新行) 的安全演进。

### 1.1 现状(11 文件用 WorldView.Ui.Dock)

```
scripts/Ui/Inspector.cs          using WorldView.Ui.Dock;
scripts/Ui/LayerPanel.cs         using WorldView.Ui.Dock;
scripts/Ui/WorldDropSurface.cs   using WorldView.Ui.Dock;
scripts/Ui/Palette/TimelineStrip.cs           using WorldView.Ui.Dock;
scripts/Ui/Palette/TerrainSwatch.cs           using WorldView.Ui.Dock;
scripts/Ui/Palette/PaletteSection.cs          using WorldView.Ui.Dock;
scripts/Ui/Palette/PaletteContent.cs          using WorldView.Ui.Dock;
scripts/Ui/Palette/BottomPalette.cs           using WorldView.Ui.Dock;
scripts/Ui/Palette/BuildingButton.cs          using WorldView.Ui.Dock;
scripts/Ui/Palette/BuildPaletteSection.cs     using WorldView.Ui.Dock;
scripts/Editor/EditorController.cs            using WorldView.Ui.Dock;
```

### 1.2 反环依赖

`scripts/Ui/Dock/DockLayout.cs` (line 22 注释 + line 278) 通过 `IPaletteHost` 接口
**反向**访问 `WorldView.Ui.Palette`。`IPaletteHost` 现在定义在
`scripts/Ui/Dock/IPaletteHost.cs`(同 namespace)。这不是纯反环(有 interface seam),
但下游 (`WorldView.Ui.Palette.BottomPalette` 实现 `IPaletteHost`) 仍能引用
`DockLayout` 的 `HandlePanelDrop("bottom", ...)`, 因为 `BottomPalette.cs` 里直接
调 `DockLayout.Instance?.HandlePanelDrop(...)`(在 `scripts/Ui/Palette/BuildingButton.cs`
和 `TerrainSwatch.cs` 等文件)。这是**真**反环: Palette 调 Dock.DockLayout 的具体方法。

### 1.3 顶级 Ui 杂烩

`WorldView.Ui` 顶级下有 6 个职责完全不同的类:
- `Inspector`, `LayerPanel` (DockPanel 子类)
- `WorldDropSurface` (DockPanel 子类, 接收 drop)
- `MapSettingsDialog`, `AddMapDialog` (弹窗)
- `ProjectChromeEntry` (顶栏)

`MapSettingsDialog` 还引 5 个其他 domain namespace (`Domain`, `Domain.Generation`,
`Editing`, `Editor`, `Render`, `Ui.Palette`) — 显然是个混合体, 但本轮**不**改它
(用户在 2026-10-09 spec §7 已明确"现有 MapSettingsDialog / AddMapDialog 内部不重构,
新调用点不迁移")。

## 2. 目标

把 `scripts/Ui/` 重新组织为 4 个层次 + 1 个 seam 层, 依赖单向, 静态访问通过接口
隔离。本轮**只动组织 + 接口**, 不动 .tscn / project.godot / Godot 节点路径
(`Inspector` `LayerPanel` `WorldDropSurface` 类名保留, .tscn 仍能找到)。

### 2.1 不在范围

- **不**改 `MapSettingsDialog.cs` / `AddMapDialog.cs` 内部任何代码 (B+A spec §7)
- **不**改 `project.godot` / 任何 .tscn / `Main.tscn` 节点路径
- **不**改 `DockLayout` 的现有 public API 签名 (签名保持向后兼容, 实现内部走
  IDockHost / IPaletteHost 接口)
- **不**改 `BottomPalette` 的运行时行为 (折叠 200/40、行布局、IPaletteHost 接口
  签名保持)
- **不**重构 `DockLayout` 的 690 行主体 (本轮只拆 seam)
- **不**修改 `Dialog` / `FloatingWindow` (A1 收尾已完成)

## 3. 目标四层结构

```
WorldView.Ui.Leaf          ──  无依赖的纯叶节点: FloatWindow, DockPanel, DockZone,
WorldView.Ui.Dock.Leaf       PanelState, PanelDragData, BuildingDragData,
                              TerrainDragData, LayoutState
                              (独立成一个子 ns, 不再放在 WorldView.Ui.Dock 顶级)
                              ↑ seam 在这层之下

WorldView.Ui.Dock.Service  ──  含 DockLayout, IPaletteHost, IDockHost, IPanelRegistry
                              (DockLayout 内部全部走接口, 不再直接调具体类)

WorldView.Ui.Chrome        ──  BottomPalette, PaletteRow, PaletteSection, PaletteContent,
                              TimelineStrip, TerrainSwatch, BuildingButton, BuildPaletteSection,
                              AdaptiveGrid, MinimapSection, ToolStrip
                              (Chrome 自己实现 IPaletteHost, 不再 using WorldView.Ui.Dock
                              的具体类, 只 using WorldView.Ui.Dock.Service + WorldView.Ui.Leaf)

WorldView.Ui.Panels        ──  Inspector, LayerPanel, WorldDropSurface
                              (Panels using WorldView.Ui.Leaf 拿基类, using
                              WorldView.Ui.Dock.Service 拿 IPanelRegistry 注册)

WorldView.Ui.Dialogs       ──  MapSettingsDialog, AddMapDialog (原 namespace 不动,
                              但作为"对话框"在 directory 上归入这里)
                              (实际上**不**改这两个文件, 只是为下一轮预留)

WorldView.Ui.Chrome.Bar    ──  ProjectChromeEntry (顶级 chrome 入口)
```

**关键**: 跨层只能向下, 不能向上。`WorldView.Ui.Chrome` 不能 using `WorldView.Ui.Panels`;
`WorldView.Ui.Panels` 不能 using `WorldView.Ui.Chrome`。

## 4. 接口 seam (新增 3 个, 全部定义在 WorldView.Ui.Dock.Service)

### 4.1 IPaletteHost (已存在, 微调)

`scripts/Ui/Dock/IPaletteHost.cs` 现有签名保留; 迁到
`scripts/Ui/Dock/Service/IPaletteHost.cs` 路径。**所有实现方继续保留**。

### 4.2 IDockHost (新增, 替代 Palette → DockLayout.Instance 的直接调)

```csharp
namespace WorldView.Ui.Dock.Service;

/// <summary>Chrome 拿到这个接口后, 就能触发落点, 但不需要知道 DockLayout 类型。</summary>
public interface IDockHost
{
    /// <summary>触发面板落点处理 (Palette 的 "bottom" 边缘专用)。</summary>
    void HandleBottomDrop(string panelId, Vector2 atPosition);

    /// <summary>面板拖放开始 / 结束通知 (亮起 DockZone)。</summary>
    void NotifyPanelDragBegin(string panelId);
    void NotifyPanelDragEnd();
}
```

**实现方**: `DockLayout` (在 `WorldView.Ui.Dock.Service` 里实现)。
**消费方**: `WorldView.Ui.Chrome.BottomPalette` 拿 `IDockHost` 而非 `DockLayout.Instance`。

### 4.3 IPanelRegistry (新增, 替代 Panels → DockLayout.Instance 的直接调)

```csharp
namespace WorldView.Ui.Dock.Service;

/// <summary>面板向布局系统登记自己 (替代直接调用 DockLayout.Instance.RegisterPanel)。</summary>
public interface IPanelRegistry
{
    void Register(DockPanel panel);
    void Unregister(DockPanel panel);
    DockPanel? Find(string panelId);
}
```

**实现方**: `DockLayout` (在 `WorldView.Ui.Dock.Service` 里实现)。
**消费方**: `DockPanel._Ready()` 拿 `IPanelRegistry` 而非 `DockLayout.Instance`。
DockLayout 在 `_EnterTree` 时把自己注册到 Godot 的
`GetTree().Root.GetNode("/root/DockHost")` 之类的全局服务节点 (单例),
其他面板通过 `GetTree().Root.GetNode<IDockHost>(...)` 拿。

**或者更简单**: 保留 `DockLayout.Instance` 静态单例, 但所有消费方代码改成
`(DockLayout.Instance as IDockHost)?.HandleBottomDrop(...)`, 这样语义一致 + 不引
入新 Godot 节点注册机制。**本 spec 选这个**。

## 5. 静态单例保留 + 接口化

### 5.1 保留的 `Instance` 单例 (3 个)

| 类 | 路径 | 用途 |
|----|------|------|
| `DockLayout.Instance` | `scripts/Ui/Dock/Service/DockLayout.cs` | 浮窗 / 全屏 / 布局落盘 |
| `EditorContext.Instance` | `scripts/Editor/EditorContext.cs` | Inspector / LayerPanel 订阅 |
| `DockPanel` 的 `Layout` 字段 | `scripts/Ui/Dock/Leaf/DockPanel.cs` | 子面板访问 (仍是字段) |

### 5.2 哪些 `Instance` 调用要改?

- `BottomPalette.cs` 里 `DockLayout.Instance?.HandlePanelDrop(...)` → 改成
  `(DockLayout.Instance as IDockHost)?.HandleBottomDrop(...)`
- `BuildingButton.cs` / `TerrainSwatch.cs` 等 Palette 子文件如有同款调用 → 同改
- `Inspector._Process` / `LayerPanel._Process` 里 `DockLayout.Instance` 调
  `RegisterPanel` / `ToggleFloat` → 改用 `IPanelRegistry`
- `DockPanel._Ready` 里 `DockLayout.Instance?.RegisterPanel(this)` → 同改

## 6. 目录变化

### 6.1 新增

```
scripts/Ui/Dock/Service/
    DockLayout.cs           (从 Dock/DockLayout.cs 迁过来)
    IDockHost.cs            (新增)
    IPanelRegistry.cs       (新增)
    IPaletteHost.cs         (从 Dock/IPaletteHost.cs 迁过来)

scripts/Ui/Dock/Leaf/
    DockPanel.cs            (从 Dock/DockPanel.cs 迁过来)
    DockZone.cs             (从 Dock/DockZone.cs 迁过来)
    FloatWindow.cs          (从 Dock/FloatWindow.cs 迁过来)
    PanelState.cs           (从 Dock/PanelState.cs 迁过来)
    PanelDragData.cs        (从 Dock/PanelDragData.cs 迁过来)
    BuildingDragData.cs     (从 Dock/BuildingDragData.cs 迁过来)
    TerrainDragData.cs      (从 Dock/TerrainDragData.cs 迁过来)
    LayoutState.cs          (从 Dock/LayoutState.cs 迁过来)

scripts/Ui/Panels/
    Inspector.cs            (从 Ui/Inspector.cs 迁过来, namespace 改)
    LayerPanel.cs           (从 Ui/LayerPanel.cs 迁过来, namespace 改)
    WorldDropSurface.cs     (从 Ui/WorldDropSurface.cs 迁过来, namespace 改)

scripts/Ui/Chrome/
    BottomPalette.cs        (从 Ui/Palette/BottomPalette.cs 迁过来, namespace 改)
    PaletteRow.cs           (从 Ui/Palette/PaletteRow.cs 迁过来, namespace 改)
    PaletteSection.cs       (从 Ui/Palette/PaletteSection.cs 迁过来, namespace 改)
    PaletteContent.cs       (从 Ui/Palette/PaletteContent.cs 迁过来, namespace 改)
    TimelineStrip.cs        (从 Ui/Palette/TimelineStrip.cs 迁过来, namespace 改)
    TerrainSwatch.cs        (从 Ui/Palette/TerrainSwatch.cs 迁过来, namespace 改)
    BuildingButton.cs       (从 Ui/Palette/BuildingButton.cs 迁过来, namespace 改)
    BuildPaletteSection.cs  (从 Ui/Palette/BuildPaletteSection.cs 迁过来, namespace 改)
    AdaptiveGrid.cs         (从 Ui/Palette/AdaptiveGrid.cs 迁过来, namespace 改)
    MinimapSection.cs       (从 Ui/Palette/MinimapSection.cs 迁过来, namespace 改)
    ToolStrip.cs            (从 Ui/Palette/ToolStrip.cs 迁过来, namespace 改)

scripts/Ui/Chrome/Bar/
    ProjectChromeEntry.cs   (从 Ui/ProjectChromeEntry.cs 迁过来, namespace 改)
```

### 6.2 删除

- `scripts/Ui/Dock/IPaletteHost.cs` (迁到 Service)
- `scripts/Ui/Dock/DockLayout.cs` (迁到 Service)
- `scripts/Ui/Dock/DockPanel.cs` / `DockZone.cs` / `FloatWindow.cs` / `PanelState.cs` /
  `PanelDragData.cs` / `BuildingDragData.cs` / `TerrainDragData.cs` / `LayoutState.cs`
  (迁到 Leaf)
- `scripts/Ui/Inspector.cs` / `LayerPanel.cs` / `WorldDropSurface.cs` (迁到 Panels)
- `scripts/Ui/ProjectChromeEntry.cs` (迁到 Chrome/Bar)
- `scripts/Ui/Palette/` 整目录 (迁到 Chrome)

### 6.3 不动

- `scripts/Ui/MapSettingsDialog.cs` (namespace 保留 `WorldView.Ui`)
- `scripts/Ui/AddMapDialog.cs` (namespace 保留 `WorldView.Ui`)
- `scripts/Ui/Theme/*` (JrpgButton 等 11 组件, 本 spec 不动)
- `scripts/Ui/Components/*` (A1/A2/A4 11 新组件, 本 spec 不动)

## 7. 验收 (xUnit + 视觉)

### 7.1 静态层依赖 (新加 4 仓单)

- `LayerDependencyTests.Leaf_不能_using_Service`
- `LayerDependencyTests.Leaf_不能_using_Chrome_或_Panels`
- `LayerDependencyTests.Service_不能_using_Chrome_或_Panels`
- `LayerDependencyTests.Chrome_不能_using_Panels`

测试方法: `grep "using WorldView.Ui" -r scripts/Ui/<layer>` 找不到**反向**依赖。

### 7.2 现有 UI 测试不回归

- `dotnet run --project tests/WorldView.Tests --filter "WorldView.Tests.Ui"`
  必须仍是 95/95 通过 (A2 + A4 累计)

### 7.3 Build

- `dotnet build WorldView.sln -c Debug` 必须 0 错 0 警告(在当前 166 警告基线
  上**不增加**新警告)

### 7.4 F5 视觉

- 启动 `Main.tscn`, 确认 BottomPalette 3 行 (timeline_row / build_row / tool_row)
  正常渲染
- 拖 Inspector 到 Left Dock, 确认能停靠
- 拖到中心 (浮窗), 确认浮窗拖动 + 关闭按钮正常
- F11 全屏切换正常
- 退出工程, 再打开, 确认布局持久化 (LayoutState 落盘) 正确

## 8. 风险 + 缓解

| 风险 | 缓解 |
|------|------|
| 移动文件后 .csproj glob 漏配 | 显式 `<Compile Include="scripts/Ui/Dock/Service/**/*.cs" />` 等 4 个 |
| Inspector / LayerPanel 静态 `Instance` 访问跨 .tscn 边界 | 保留类名 (Inspector), .tscn 仍找得到 |
| `WorldView.Ui.Palette` 引用断 | 用 grep 全文搜, 凡是引用 `WorldView.Ui.Palette.X` 的 全部改 `WorldView.Ui.Chrome.X` |
| `MapSettingsDialog` 引用 `using WorldView.Ui.Palette;` | 在 MapSettingsDialog.cs 顶部加 `using WorldView.Ui.Chrome;` 走兼容路径 (本 spec 不动 MapSettingsDialog 内部, 顶部 using 必加) |
| 现有 690 行 DockLayout 重组后 bug | 重构后 build 0 错 0 警告 + 现有 95/95 UI 测试 + F5 视觉 4 项全过 才算完 |

## 9. 不在本轮 (留给 D 轮)

- `MapSettingsDialog.cs` / `AddMapDialog.cs` 内部重构 (用户 2026-10-09 明确)
- `Dialog` / `FloatingWindow` 接入 DockLayout (让弹窗可停靠)
- Inspector / LayerPanel 拆更细 (Panels 内部再分 Inspector / LayerPanel 子目录)
- 静态 `Instance` 改依赖注入 (D 轮才上)
- 全工程 namespace 重新划分 (Scripts/Editor / Scripts/Storage 等)

## 10. 估算

- 12 文件迁目录 + namespace 改 (机械, 1-2h)
- 11 文件 using 改 (机械, 1h)
- 2 个新接口 + DockLayout 实现 (30 行, 1h)
- 4 个 LayerDependencyTests (1h)
- 验收 + 修复 + F5 视觉 (1-2h)

合计 5-7h work, 拆 3 task:
- **Task 1**: 4 个新目录 + 文件移动 + namespace 改 + using 改 (无新接口, 全机械)
- **Task 2**: IDockHost + IPanelRegistry 接口加 + DockLayout 实现 + 消费方切换
- **Task 3**: LayerDependencyTests 4 仓单 + 验收 + F5 视觉
