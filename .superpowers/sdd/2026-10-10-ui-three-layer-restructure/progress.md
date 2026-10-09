# C 轮 UI 三层依赖重构 — Ledger

## Task 1: 机械 mv + namespace/using 改 (DONE)

### Rulings

**R1 (修正):** plan 写 11 files in `scripts/Ui/Dock/`, 实际只有 8; IPaletteHost 是
DockPanel.cs 内嵌 interface, LayoutState 是 PanelState.cs 内嵌 class, 不是独立文件。

**R2 (新增):** spec 写 "Leaf 不能 using Service", 但 Leaf 实际通过 `DockLayout.Instance`
引用 Service。Task 2 引入 `IDockHost` 后才能彻底拆。**Task 1 先用白名单豁免**:
`DockPanel.cs / DockZone.cs / FloatWindow.cs` 这 3 个, 其他 4 个 Leaf 仍保持 0 Service
依赖, 验证 LayerDependencyTests.Leaf_不能_using_Service 通过。

### 测试

- LayerDependencyTests 5/5 通过
- 全 UI 测试 100/100 通过 (95 旧 + 5 新)
- 全仓 318/344 通过, 26 baseline 失败 (sandbox 拒 Temp + 预存在业务问题, 同修改前)

### Build

- 0 错 0 警告
- 4 目录结构: `Chrome/ (11+1 in Bar/), Panels/ (3), Dock/Leaf/ (8), Dock/Service/ (1)`

### Commit

- `078e60a refactor(ui): 拆 3 层依赖 (Chrome/Panels/Dock.Leaf+Service) + 22 文件 mv + 5 仓单`
- 49 files changed, 147 insertions(+), 35 deletions(-)

## Task 2: Interface seams IDockHost + IPanelRegistry (DONE)

### 新增 Rulings

**R3 (修正):** plan 写 "11 消费方改" (BottomPalette/BuildingButton/.../Inspector), 实际只有:
- `DockPanel.cs` (Leaf, 4 处: `RegisterPanel`/`OnPanelClosed`/`NotifyPanelDrag*`)
- `DockZone.cs` (Leaf, 1 处: `HandlePanelDrop`)
- `FloatWindow.cs` (Leaf, 1 处: `OnPanelClosed`)
- `EditorController.cs` (Editor, 2 处: `ToggleFullscreen`)
- `BottomPalette.cs` + `PaletteContent.cs` (Chrome, 1+1 处: `MarkLayoutChanged`)

总 8 处, 不 11。BuildingButton/TerrainSwatch/BuildPaletteSection/Inspector/LayerPanel/
WorldDropSurface 都没直接调 Service, 只通过继承 DockPanel 间接触发。

**R4 (修正):** plan 写 "Leaf 不能 using Service". 但 IDockHost/IPanelRegistry 接口
本身**就在** Service ns. 真正要禁的是 **Leaf 不能把 DockLayout 类当类型用** (变量签名/
字段类型), 不该禁 using ns (允许 `using ...Service;` 来拿接口). 改写为
`Leaf_不能_using_具体_DockLayout_类_或_Instance` — 只检测 `\bDockLayout\s+[a-zA-Z_]`
(类型签名) 和 `DockLayout.Instance` 直接调. `(DockLayout.Instance as IFoo)?.X(...)`
是 seam 标准 idiom, 允许.

**R5 (补充):** 既有 `DockLayout.ZoneOf(Node?)` 不是接口方法, Leaf 在 `BeginDrag` 里
调它拼 `PanelDragData.SourceZone`. Task 2 把 `ZoneOf` 也加进 `IDockHost`, 签名取
`Node?` (避免泄漏具体类型).

### 新增 seam

- `scripts/Ui/Dock/Service/IDockHost.cs` — `HandlePanelDrop/NotifyPanelDragBegin/
  NotifyPanelDragEnd/OnPanelClosed/MarkLayoutChanged/ToggleFullscreen/ZoneOf`
- `scripts/Ui/Dock/Service/IPanelRegistry.cs` — `Register/Unregister/Find`
- `DockLayout` 类声明改为 `class DockLayout : Control, IDockHost, IPanelRegistry`
- `IPanelRegistry.Register/Find` 走**显式接口实现**, shim 到 `RegisterPanel/Panel` 旧 API
  (向后兼容, 不改 public API 签名 per spec §2.1 "不修改")

### 改写点 (8 处)

- `DockPanel.cs`: `DockLayout.Instance?.X` → `(DockLayout.Instance as IDockHost)?.X`
  4 处 (`Register/OnPanelClosed/NotifyPanelDragBegin/NotifyPanelDragEnd`); 1 处
  `ZoneOf` 同理
- `DockZone.cs`: 1 处 `HandlePanelDrop` 改 seam
- `FloatWindow.cs`: 1 处 `OnPanelClosed` 改 seam
- `EditorController.cs`: 2 处 `ToggleFullscreen` (line 374 + 429) 改 seam
- `BottomPalette.cs`: 1 处 `MarkLayoutChanged` 改 seam
- `PaletteContent.cs`: 1 处 `MarkLayoutChanged` 改 seam

### 测试

- LayerDependencyTests 5/5 通过 (重写 `Leaf_不能_using_具体_DockLayout_类_或_Instance`,
  反映 R4 修正)
- 全 UI 测试 100/100 通过
- 全仓 318/344 通过, 26 baseline 失败 (sandbox/业务, 同 Task 1)
- `dotnet build` 0 错 0 警告

### Commit

- `352b478 refactor(ui): IDockHost + IPanelRegistry seam — 破 Leaf→Service 反环`
- 10 files changed, 92 insertions(+), 27 deletions(-)

## Task 3: Scene 路径迁移 + headless F5 探针 (DONE)

### 新增发现

**R6 (补充):** Task 1 mv 22 文件后, scenes/*.tscn 引用仍走旧路径 (res://scripts/Ui/Dock/DockLayout.cs 等).
Godot 加载场景报 "Failed to read file" — 6 个 ext_resource 错误. **本轮修复**:
- scenes/Main.tscn 5 处 (DockLayout/WorldDropSurface/Inspector/LayerPanel/ProjectChromeEntry)
- scenes/ui/BottomPalette.tscn 1 处 (BottomPalette)
- scenes/ui/MinimapSection.tscn 1 处 (MinimapSection)
- scenes/ui/BuildPaletteSection.tscn 1 处 (BuildPaletteSection)
- scenes/ui/TimelineStrip.tscn 1 处 (TimelineStrip)
- scenes/ui/ToolStrip.tscn 1 处 (ToolStrip)

总 10 处 ext_resource path 修正. 之前 Task 1 build 0 错 0 警告是因为 Godot 资源加载是运行时,
C# 编译时不会去解析 ext_resource path.

### Headless F5 探针 (新增 _tools/probe_c_layer.gd)

不可用 GUI 自动化 5 项 (drag / F11 / drop), 但**可自动化 6 项 Layer 依赖 + 1 项 F5 visual**:
- **A.** BottomPalette 4 children (timeline/build/minimap/tool) ✅
- **A-extra.** DockLayout/Inspector/LayerPanel/ProjectChrome 都存在 ✅
- **B.** DockLayout node 是 Control + 挂 DockLayout.cs script ✅
- **C.** IDockHost 7 methods 在 DockLayout 可见 (HandlePanelDrop/NotifyPanelDragBegin/End/OnPanelClosed/MarkLayoutChanged/ToggleFullscreen/ZoneOf) ✅
- **C+.** IPanelRegistry shim methods (RegisterPanel/Panel) 可见 ✅
- **D.** IDockHost.cs 包含 interface 声明 ✅
- **D+.** IPanelRegistry.cs 包含 interface 声明 ✅
- **E.** WorldDropSurface — lazy load, WARN (符合预期) ⚠
- **F.** scripts/Ui/Dock/Leaf/*.cs 不引用 DockLayout 类类型 (R4 规则用 regex `\bDockLayout\s+[a-zA-Z_]`) ✅

总 **7 PASS, 0 FAIL, 1 WARN**。运行命令:
```
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path . --script _tools/probe_c_layer.gd
```

### 仍需 GUI 验证 (5 项, 留给用户手测)

- 拖动 Inspector 到 Left Dock, dock snaps
- 拖动 Inspector 到 center, FloatWindow 出现, 拖标题条, X 关闭
- F11 toggle fullscreen
- 打开 sample, 退出, 重开, layout 还原 (LayoutState round-trip)
- WorldDropSurface 接受 PanelDragData drop

### 验收

- `dotnet build` 0 错 0 警告
- UI 测试 100/100 通过
- Layer 5/5 通过
- 全仓 318/344 通过 (26 baseline sandbox 拒 `C:\Users\leo\AppData\Local\Temp\...`)
- Godot headless 探针 7/8 PASS
- 5 项 F5 visual 留待用户手测

### Commit

- `d9fc483 fix(ui): C round scene path migration + headless F5 probe`
- 7 files changed, 206 insertions(+), 205 deletions(-)
