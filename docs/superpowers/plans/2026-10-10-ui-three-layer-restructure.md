# UI 三层依赖重构 (C 轮) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize `scripts/Ui/` into 4 layer + 1 seam structure, breaking the reverse `Palette → Dock` dependency, while keeping all 95/95 UI tests green and F5 visuals identical.

**Architecture:** Mechanical file move (12 files → 4 new sub-folders) + namespace rename + 2 new interface seams (IDockHost, IPanelRegistry) that all `Instance` callers go through. No new features; no API breakage; class names preserved so .tscn paths keep working.

**Tech Stack:** Godot 4.7.2 mono, C# / .NET 8.0, xunit.v3 + Microsoft.Testing.Platform 2.4.0, Palette Generator (Roslyn IIncrementalGenerator).

**Spec:** `docs/superpowers/specs/2026-10-10-ui-three-layer-restructure.md`

## Global Constraints

- Godot 4.7.2 mono installed at `C:\App\Godot_v4.7.2-stable_mono_win64\` (AGENTS.md line 152-181)
- `dotnet` SDK 8.0.425 (pinned by `global.json`)
- `WorldView.csproj` uses `LangVersion=latest`, `EnableDefaultCompileItems=false`, `<Compile Include="scripts/**/*.cs" />` (must be supplemented with explicit subdir includes)
- `tests/WorldView.Tests` uses xunit.v3 4.0.1 + Microsoft.Testing.Platform 2.4.0, run via `dotnet run --project tests/WorldView.Tests/WorldView.Tests.csproj -- --filter "..."`
- Naming: `Jrpg*` for Theme components, no prefix for `Field`, `Tab`/`Menu`/`Toast`/`Tooltip`/`ToolButton` for A2 components (post-A2 contract)
- All UI tokens flow through `WorldView.Ui.Theme.Palette.*`; **zero** `Color.FromHtml(` in `scripts/Ui` (grep invariant)
- `MapSettingsDialog.cs` and `AddMapDialog.cs` keep `WorldView.Ui` namespace, internal code untouched (B+A spec §7 invariant)
- `.tscn` files use type names; class rename breaks scenes — keep all public class names identical
- Demo scene `scenes/_demo/DemoUiAtoms.tscn` is gitignored, locally only

## Review Focus

These inputs are real but the spec and tests don't explicitly cover them. Each gets a step in the task that owns the code.

- **F5 after refactor (no .tscn test)**: a successful build + 95/95 tests passing is necessary but not sufficient. The testhost can't construct Godot.Control, so Inspector / LayerPanel / BottomPalette drag-drop paths only verify on F5. Plan adds an explicit F5 checklist task.
- **Reverse `using` after mechanical rename**: a single missed `using WorldView.Ui.Dock;` (instead of `using WorldView.Ui.Dock.Leaf;`) in a Chrome file compiles but inverts the layer. Plan adds 4 `LayerDependencyTests` (spec §7.1) to catch it.
- **Layout persistence after refactor (LayoutState.Load/Save round-trip)**: a passing `dotnet test` doesn't open a project, so layout file migration can regress silently. Plan adds a manual F5 step "open sample project, drag Inspector to Left, F11, exit, reopen, verify restored".
- **DropSurface drag after move**: `WorldDropSurface` accepts a `PanelDragData` payload — moving its file but missing the `DockPanel` base import breaks drag. Plan adds a manual F5 step for that flow too.

---

## File Structure (before / after)

### Before

```
scripts/Ui/
├── Dock/
│   ├── DockLayout.cs         (WorldView.Ui.Dock)
│   ├── DockPanel.cs          (WorldView.Ui.Dock)
│   ├── DockZone.cs           (WorldView.Ui.Dock)
│   ├── FloatWindow.cs        (WorldView.Ui.Dock)
│   ├── IPaletteHost.cs       (WorldView.Ui.Dock)
│   ├── PanelState.cs         (WorldView.Ui.Dock)
│   ├── PanelDragData.cs      (WorldView.Ui.Dock)
│   ├── BuildingDragData.cs   (WorldView.Ui.Dock)
│   ├── TerrainDragData.cs    (WorldView.Ui.Dock)
│   └── LayoutState.cs        (WorldView.Ui.Dock)
├── Palette/                  (WorldView.Ui.Palette, 10 files)
├── Inspector.cs              (WorldView.Ui)
├── LayerPanel.cs             (WorldView.Ui)
├── WorldDropSurface.cs       (WorldView.Ui)
├── MapSettingsDialog.cs      (WorldView.Ui, FROZEN)
├── AddMapDialog.cs           (WorldView.Ui, FROZEN)
└── ProjectChromeEntry.cs     (WorldView.Ui)
```

### After

```
scripts/Ui/
├── Dock/
│   ├── Service/
│   │   ├── DockLayout.cs     (WorldView.Ui.Dock.Service)
│   │   ├── IDockHost.cs      (NEW, WorldView.Ui.Dock.Service)
│   │   ├── IPanelRegistry.cs (NEW, WorldView.Ui.Dock.Service)
│   │   └── IPaletteHost.cs   (WorldView.Ui.Dock.Service, moved)
│   └── Leaf/
│       ├── DockPanel.cs      (WorldView.Ui.Dock.Leaf)
│       ├── DockZone.cs       (WorldView.Ui.Dock.Leaf)
│       ├── FloatWindow.cs    (WorldView.Ui.Dock.Leaf)
│       ├── PanelState.cs     (WorldView.Ui.Dock.Leaf)
│       ├── PanelDragData.cs  (WorldView.Ui.Dock.Leaf)
│       ├── BuildingDragData.cs (WorldView.Ui.Dock.Leaf)
│       ├── TerrainDragData.cs  (WorldView.Ui.Dock.Leaf)
│       └── LayoutState.cs    (WorldView.Ui.Dock.Leaf)
├── Panels/
│   ├── Inspector.cs          (WorldView.Ui.Panels, MOVED)
│   ├── LayerPanel.cs         (WorldView.Ui.Panels, MOVED)
│   └── WorldDropSurface.cs   (WorldView.Ui.Panels, MOVED)
├── Chrome/
│   ├── BottomPalette.cs      (WorldView.Ui.Chrome, MOVED)
│   ├── PaletteRow.cs         (WorldView.Ui.Chrome, MOVED)
│   ├── PaletteSection.cs     (WorldView.Ui.Chrome, MOVED)
│   ├── PaletteContent.cs     (WorldView.Ui.Chrome, MOVED)
│   ├── TimelineStrip.cs      (WorldView.Ui.Chrome, MOVED)
│   ├── TerrainSwatch.cs      (WorldView.Ui.Chrome, MOVED)
│   ├── BuildingButton.cs     (WorldView.Ui.Chrome, MOVED)
│   ├── BuildPaletteSection.cs (WorldView.Ui.Chrome, MOVED)
│   ├── AdaptiveGrid.cs       (WorldView.Ui.Chrome, MOVED)
│   ├── MinimapSection.cs     (WorldView.Ui.Chrome, MOVED)
│   ├── ToolStrip.cs          (WorldView.Ui.Chrome, MOVED)
│   └── Bar/
│       └── ProjectChromeEntry.cs (WorldView.Ui.Chrome.Bar, MOVED)
├── MapSettingsDialog.cs      (WorldView.Ui, FROZEN)
├── AddMapDialog.cs           (WorldView.Ui, FROZEN)
├── Theme/                    (unchanged, WorldView.Ui.Theme)
└── Components/               (unchanged, WorldView.Ui.Components)
```

`scripts/Ui/Palette/` directory deleted entirely.

---

## Task Decomposition

| # | Task | Files | Risk |
|---|------|-------|------|
| 1 | Mechanical move (12 files, namespaces) + using rewrite | 13 + 11 consumer | medium |
| 2 | 2 new interfaces + DockLayout implements | 3 + N consumer | low |
| 3 | LayerDependencyTests 4 + F5 + commit | 1 test file + 1 commit | low |

Splitting Task 1 lets Task 1 fail loudly (build breaks) on the mechanical move before any interface work, and Task 2's interface migration can be reviewed against a green build.

---

## Task 1: Mechanical file move + namespace rewrite

**Files:**
- Move: 12 files per §6.1 in spec (3 Panels, 8 Chrome from `Palette/`, 1 Chrome/Bar)
- Move: 8 files into `scripts/Ui/Dock/Leaf/`, 3 files into `scripts/Ui/Dock/Service/`
- Modify: `WorldView.csproj` (add explicit `<Compile Include>` for 4 new subdirs)
- Modify: 11 consumer files (those that had `using WorldView.Ui.Dock;` or `using WorldView.Ui.Palette;`)
- Modify: `MapSettingsDialog.cs` (top of file: `using WorldView.Ui.Palette;` → `using WorldView.Ui.Chrome;`)

**Interfaces:** No new APIs in this task. `DockLayout` keeps public signature (Static `Instance` retained, all method names unchanged).

### Step 1: Move 11 Dock files (8 Leaf + 3 Service)

```bash
cd D:\kudi-project\world-view
mkdir scripts\Ui\Dock\Leaf scripts\Ui\Dock\Service
# 8 Leaf files
git mv scripts\Ui\Dock\DockPanel.cs        scripts\Ui\Dock\Leaf\DockPanel.cs
git mv scripts\Ui\Dock\DockZone.cs         scripts\Ui\Dock\Leaf\DockZone.cs
git mv scripts\Ui\Dock\FloatWindow.cs      scripts\Ui\Dock\Leaf\FloatWindow.cs
git mv scripts\Ui\Dock\PanelState.cs       scripts\Ui\Dock\Leaf\PanelState.cs
git mv scripts\Ui\Dock\PanelDragData.cs    scripts\Ui\Dock\Leaf\PanelDragData.cs
git mv scripts\Ui\Dock\BuildingDragData.cs scripts\Ui\Dock\Leaf\BuildingDragData.cs
git mv scripts\Ui\Dock\TerrainDragData.cs  scripts\Ui\Dock\Leaf\TerrainDragData.cs
git mv scripts\Ui\Dock\LayoutState.cs      scripts\Ui\Dock\Leaf\LayoutState.cs
# 3 Service files
git mv scripts\Ui\Dock\IPaletteHost.cs     scripts\Ui\Dock\Service\IPaletteHost.cs
git mv scripts\Ui\Dock\DockLayout.cs       scripts\Ui\Dock\Service\DockLayout.cs
```

Expected: 11 files moved; `scripts\Ui\Dock\` becomes empty.

- [ ] **Step 1.5: Verify Dock empty**

```bash
ls scripts\Ui\Dock\
```

Expected: only `Leaf/` and `Service/` directories.

### Step 2: Move 3 Panels + 11 Chrome files

```bash
cd D:\kudi-project\world-view
mkdir scripts\Ui\Panels scripts\Ui\Chrome scripts\Ui\Chrome\Bar
# 3 Panels
git mv scripts\Ui\Inspector.cs      scripts\Ui\Panels\Inspector.cs
git mv scripts\Ui\LayerPanel.cs     scripts\Ui\Panels\LayerPanel.cs
git mv scripts\Ui\WorldDropSurface.cs scripts\Ui\Panels\WorldDropSurface.cs
# 10 Chrome
git mv scripts\Ui\Palette\BottomPalette.cs      scripts\Ui\Chrome\BottomPalette.cs
git mv scripts\Ui\Palette\PaletteRow.cs         scripts\Ui\Chrome\PaletteRow.cs
git mv scripts\Ui\Palette\PaletteSection.cs     scripts\Ui\Chrome\PaletteSection.cs
git mv scripts\Ui\Palette\PaletteContent.cs     scripts\Ui\Chrome\PaletteContent.cs
git mv scripts\Ui\Palette\TimelineStrip.cs      scripts\Ui\Chrome\TimelineStrip.cs
git mv scripts\Ui\Palette\TerrainSwatch.cs      scripts\Ui\Chrome\TerrainSwatch.cs
git mv scripts\Ui\Palette\BuildingButton.cs     scripts\Ui\Chrome\BuildingButton.cs
git mv scripts\Ui\Palette\BuildPaletteSection.cs scripts\Ui\Chrome\BuildPaletteSection.cs
git mv scripts\Ui\Palette\AdaptiveGrid.cs       scripts\Ui\Chrome\AdaptiveGrid.cs
git mv scripts\Ui\Palette\MinimapSection.cs     scripts\Ui\Chrome\MinimapSection.cs
git mv scripts\Ui\Palette\ToolStrip.cs          scripts\Ui\Chrome\ToolStrip.cs
# 1 Chrome/Bar
git mv scripts\Ui\ProjectChromeEntry.cs         scripts\Ui\Chrome\Bar\ProjectChromeEntry.cs
# Remove empty old dir
rmdir scripts\Ui\Palette
```

Expected: 14 files moved; `scripts\Ui\Palette\` deleted.

- [ ] **Step 2.5: Verify old dirs gone**

```bash
ls scripts\Ui\
ls scripts\Ui\Palette 2>&1
```

Expected first: `Components/  Dock/  Panels/  Chrome/  Theme/  MapSettingsDialog.cs  AddMapDialog.cs`. Expected second: "No such file or directory".

### Step 3: Add explicit Compile includes in WorldView.csproj

Edit `WorldView.csproj` after the existing `<Compile Include="scripts/**/*.cs" />` line:

```xml
<Compile Include="scripts/**/*.cs" />
<Compile Include="scripts/Ui/Dock/Leaf/**/*.cs" />
<Compile Include="scripts/Ui/Dock/Service/**/*.cs" />
<Compile Include="scripts/Ui/Panels/**/*.cs" />
<Compile Include="scripts/Ui/Chrome/**/*.cs" />
```

(Glob 已在,但显式 include 防御 .csproj glob 漏配 — spec §8 风险。)

- [ ] **Step 3.5: Verify build picks up moved files**

```bash
cd D:\kudi-project\world-view
dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "error" | Select-Object -First 5
```

Expected: errors about unresolved namespaces (since we haven't updated namespaces yet) — this is OK.

### Step 4: Rewrite namespaces in all 27 moved files

For each of the 27 moved files, change the `namespace` declaration:

| Old namespace | New namespace | Files |
|---------------|---------------|-------|
| `WorldView.Ui.Dock` | `WorldView.Ui.Dock.Leaf` | 8 |
| `WorldView.Ui.Dock` | `WorldView.Ui.Dock.Service` | 3 (DockLayout, IPaletteHost, IDockHost — but IDockHost not moved yet) |
| `WorldView.Ui` | `WorldView.Ui.Panels` | 3 |
| `WorldView.Ui.Palette` | `WorldView.Ui.Chrome` | 10 |
| `WorldView.Ui` | `WorldView.Ui.Chrome.Bar` | 1 (ProjectChromeEntry) |

After this step, the moved files have correct namespace but reference types that still live at old paths. Build will fail with CS0246 ("type or namespace 'X' could not be found") for every cross-file reference. That's expected.

- [ ] **Step 4.5: Verify build errors are namespace-related**

```bash
cd D:\kudi-project\world-view
dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "CS0246" | Measure-Object | Select-Object Count
```

Expected: 20+ errors (every `using WorldView.Ui.Dock;` / `using WorldView.Ui.Palette;` in consumer files).

### Step 5: Rewrite `using` directives in 12 consumer files

For each consumer file (11 from grep + MapSettingsDialog), update `using` directives:

| File | Old | New |
|------|-----|-----|
| `scripts/Ui/Panels/Inspector.cs` | `using WorldView.Ui.Dock;` | `using WorldView.Ui.Dock.Leaf; using WorldView.Ui.Dock.Service;` |
| `scripts/Ui/Panels/LayerPanel.cs` | same | same |
| `scripts/Ui/Panels/WorldDropSurface.cs` | same | same |
| `scripts/Ui/Chrome/TimelineStrip.cs` | same | same |
| `scripts/Ui/Chrome/TerrainSwatch.cs` | same | same |
| `scripts/Ui/Chrome/PaletteSection.cs` | same | same |
| `scripts/Ui/Chrome/PaletteContent.cs` | same | same |
| `scripts/Ui/Chrome/BottomPalette.cs` | same | same |
| `scripts/Ui/Chrome/BuildingButton.cs` | same | same |
| `scripts/Ui/Chrome/BuildPaletteSection.cs` | same | same |
| `scripts/Editor/EditorController.cs` | same | same |
| `scripts/Ui/MapSettingsDialog.cs` | `using WorldView.Ui.Palette;` | `using WorldView.Ui.Chrome;` |

For files inside `scripts/Ui/Chrome/`, the previous `using WorldView.Ui.Dock;` becomes:
- `using WorldView.Ui.Dock.Leaf;` (for DockPanel, FloatWindow, DockZone, etc.)
- `using WorldView.Ui.Dock.Service;` (for DockLayout, IPaletteHost)

For files inside `scripts/Ui/Panels/`, same as Chrome.

`MapSettingsDialog` does NOT need to import Dock — its current `using WorldView.Ui.Palette;` is for `BottomPalette` (now in `WorldView.Ui.Chrome`).

### Step 6: Rewrite type references in DockLayout.cs

Inside `scripts/Ui/Dock/Service/DockLayout.cs`, references to `DockPanel`, `FloatWindow`, `DockZone` are now in `WorldView.Ui.Dock.Leaf`. The C# compiler will find them through the new `using WorldView.Ui.Dock.Leaf;` already added in Step 5 — but the file itself needs `using` updates too.

Edit `scripts/Ui/Dock/Service/DockLayout.cs`:
- Add `using WorldView.Ui.Dock.Leaf;` at the top
- Existing `using WorldView.Ui.Dock;` is no longer needed (it was the old namespace) but harmless — keep it for now, remove in Task 2

### Step 7: Build clean

```bash
cd D:\kudi-project\world-view
dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "error" | Select-Object -First 5
```

Expected: 0 errors. May have new warnings (CS0108 / CS8602) — track in commit message.

- [ ] **Step 7.5: Run full UI tests**

```bash
cd D:\kudi-project\world-view
dotnet run --project tests/WorldView.Tests/WorldView.Tests.csproj -c Debug --no-build -- --filter "WorldView.Tests.Ui" 2>&1 | Select-String "总计|失败|成功" | Select-Object -First 3
```

Expected: 95/95 passing (same as pre-refactor; Task 1 only does mechanical move).

### Step 8: Commit

```bash
cd D:\kudi-project\world-view
git add scripts/Ui/ WorldView.csproj
git -c core.autocrlf=false commit -m "refactor(ui): 4 layer + 1 seam — move 27 files, 12 consumer using 改

Task 1 of C spec, mechanical move (no API change, no .tscn path change)
- 8 Dock files → scripts/Ui/Dock/Leaf/ (namespace WorldView.Ui.Dock.Leaf)
- 3 Dock files → scripts/Ui/Dock/Service/ (namespace WorldView.Ui.Dock.Service)
- 3 Inspector/LayerPanel/WorldDropSurface → scripts/Ui/Panels/
- 10 Palette/* → scripts/Ui/Chrome/ (old WorldView.Ui.Palette dir deleted)
- 1 ProjectChromeEntry → scripts/Ui/Chrome/Bar/
- 12 consumer files: using WorldView.Ui.Dock → using WorldView.Ui.Dock.Leaf/Service
- MapSettingsDialog: using WorldView.Ui.Palette → using WorldView.Ui.Chrome

Build 0 错 (新增 warning 数待验证), UI 测试 95/95 通过"
```

---

## Task 2: Interface seams (IDockHost + IPanelRegistry)

**Files:**
- Create: `scripts/Ui/Dock/Service/IDockHost.cs` (3 methods, ~30 lines)
- Create: `scripts/Ui/Dock/Service/IPanelRegistry.cs` (3 methods, ~25 lines)
- Modify: `scripts/Ui/Dock/Service/DockLayout.cs` (declare `class DockLayout : IDockHost, IPanelRegistry`)
- Modify: consumer files (replace `DockLayout.Instance?.HandlePanelDrop` with `(DockLayout.Instance as IDockHost)?.HandleBottomDrop`)

**Interfaces produced:**
- `IDockHost.HandleBottomDrop(string panelId, Vector2 atPosition)` — replaces Palette's direct `DockLayout.Instance?.HandlePanelDrop("bottom", ...)` call (the only call site that uses "bottom" edge today).
- `IDockHost.NotifyPanelDragBegin(string panelId)` / `NotifyPanelDragEnd()` — already exist on DockLayout, just exposed.
- `IPanelRegistry.Register(DockPanel panel)` / `Unregister(DockPanel)` / `Find(string panelId)` — replace `DockLayout.Instance?.RegisterPanel(this)` in panels.

### Step 1: Write IDockHost interface

Create `scripts/Ui/Dock/Service/IDockHost.cs`:

```csharp
using Godot;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Dock.Service;

/// <summary>
/// Chrome 拿到这个接口后就能触发落点,但不需要知道 DockLayout 类型。
/// 替代 BottomPalette.cs / BuildingButton.cs 中直接调 DockLayout.Instance?.HandlePanelDrop(...) 的反环。
/// </summary>
public interface IDockHost
{
    void HandleBottomDrop(string panelId, Vector2 atPosition);
    void NotifyPanelDragBegin(string panelId);
    void NotifyPanelDragEnd();
}
```

### Step 2: Write IPanelRegistry interface

Create `scripts/Ui/Dock/Service/IPanelRegistry.cs`:

```csharp
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Dock.Service;

/// <summary>
/// 面板向布局系统登记自己。替代 Inspector / LayerPanel / WorldDropSurface 中
/// 直接调 DockLayout.Instance?.RegisterPanel(this) 的反环。
/// </summary>
public interface IPanelRegistry
{
    void Register(DockPanel panel);
    void Unregister(DockPanel panel);
    DockPanel? Find(string panelId);
}
```

### Step 3: Make DockLayout implement the interfaces

Edit `scripts/Ui/Dock/Service/DockLayout.cs`:

```csharp
public partial class DockLayout : Control, IDockHost, IPanelRegistry
```

Existing methods already match (HandlePanelDrop is the implementation, but it takes `(string edge, ...)` — we need an overload that takes the bottom-specific signature). Add:

```csharp
// IDockHost
public void HandleBottomDrop(string panelId, Vector2 atPosition) =>
    HandlePanelDrop("bottom", panelId, atPosition);

public void NotifyPanelDragBegin(string panelId) =>
    NotifyPanelDragBegin(Panel(panelId) ?? throw new InvalidOperationException($"Panel '{panelId}' not found"));

public void NotifyPanelDragEnd() => NotifyPanelDragEnd();
```

For `IPanelRegistry`, add thin shims:

```csharp
// IPanelRegistry
public void Register(DockPanel panel) => RegisterPanel(panel);
public void Unregister(DockPanel panel) { /* no-op today, future hook */ }
public DockPanel? Find(string panelId) => Panel(panelId);
```

### Step 4: Replace direct calls in BottomPalette.cs

In `scripts/Ui/Chrome/BottomPalette.cs`, find every `DockLayout.Instance?.HandlePanelDrop("bottom", ...)` and replace with `(DockLayout.Instance as IDockHost)?.HandleBottomDrop(...)`.

Also add `using WorldView.Ui.Dock.Service;` if not already present.

### Step 5: Replace direct calls in BuildingButton.cs, TerrainSwatch.cs, BuildPaletteSection.cs

Same pattern: `DockLayout.Instance?.HandlePanelDrop("bottom", panelId, atPosition)` → `(DockLayout.Instance as IDockHost)?.HandleBottomDrop(panelId, atPosition)`.

### Step 6: Replace direct calls in Inspector / LayerPanel / WorldDropSurface

In each Panel file, find `DockLayout.Instance?.RegisterPanel(this)` and replace with `(DockLayout.Instance as IPanelRegistry)?.Register(this)`.

Also add `using WorldView.Ui.Dock.Service;` if not already present.

### Step 7: Build + test

```bash
cd D:\kudi-project\world-view
dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "error" | Select-Object -First 5
dotnet run --project tests/WorldView.Tests/WorldView.Tests.csproj -c Debug --no-build -- --filter "WorldView.Tests.Ui" 2>&1 | Select-String "总计|失败|成功" | Select-Object -First 3
```

Expected: 0 errors, 95/95 passing.

### Step 8: Commit

```bash
cd D:\kudi-project\world-view
git add scripts/Ui/Dock/Service/IDockHost.cs scripts/Ui/Dock/Service/IPanelRegistry.cs scripts/Ui/Dock/Service/DockLayout.cs scripts/Ui/Chrome/BottomPalette.cs scripts/Ui/Chrome/BuildingButton.cs scripts/Ui/Chrome/TerrainSwatch.cs scripts/Ui/Chrome/BuildPaletteSection.cs scripts/Ui/Panels/Inspector.cs scripts/Ui/Panels/LayerPanel.cs scripts/Ui/Panels/WorldDropSurface.cs
git -c core.autocrlf=false commit -m "refactor(ui): IDockHost + IPanelRegistry seam — break Palette→Dock 反环

Task 2 of C spec, 接口层引入
- 新增 scripts/Ui/Dock/Service/IDockHost.cs (HandleBottomDrop / NotifyPanelDrag*)
- 新增 scripts/Ui/Dock/Service/IPanelRegistry.cs (Register / Unregister / Find)
- DockLayout 同时实现两个接口
- 11 消费方改: DockLayout.Instance?.X(...) → (DockLayout.Instance as I*Host)?.X(...)

依赖单向: Chrome → Service, Panels → Service; Service 不 using Chrome/Panels.
Build 0 错, UI 测试 95/95 通过"
```

---

## Task 3: LayerDependencyTests + F5 + final commit

**Files:**
- Create: `tests/WorldView.Tests/Ui/Layout/LayerDependencyTests.cs` (4 facts)

### Step 1: Write LayerDependencyTests

```csharp
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Layout;

public class LayerDependencyTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static string[] FilesUnder(string relativeDir) =>
        Directory.GetFiles(Path.Combine(RepoRoot, "scripts", "Ui", relativeDir), "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void Leaf_不能_using_Service()
    {
        foreach (var file in FilesUnder("Dock/Leaf"))
        {
            var content = File.ReadAllText(file);
            Assert.False(
                content.Contains("using WorldView.Ui.Dock.Service"),
                $"{file} must not use WorldView.Ui.Dock.Service (leaf layer is dependency-bottom)");
        }
    }

    [Fact]
    public void Leaf_不能_using_Chrome_或_Panels()
    {
        foreach (var file in FilesUnder("Dock/Leaf"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Chrome"), $"{file} must not use WorldView.Ui.Chrome");
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }

    [Fact]
    public void Service_不能_using_Chrome_或_Panels()
    {
        foreach (var file in FilesUnder("Dock/Service"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Chrome"), $"{file} must not use WorldView.Ui.Chrome");
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }

    [Fact]
    public void Chrome_不能_using_Panels()
    {
        foreach (var file in FilesUnder("Chrome"))
        {
            var content = File.ReadAllText(file);
            Assert.False(content.Contains("using WorldView.Ui.Panels"), $"{file} must not use WorldView.Ui.Panels");
        }
    }
}
```

### Step 2: Run tests

```bash
cd D:\kudi-project\world-view
dotnet build WorldView.sln -c Debug --nologo 2>&1 | Select-String "error" | Select-Object -First 3
dotnet run --project tests/WorldView.Tests/WorldView.Tests.csproj -c Debug --no-build -- --filter "WorldView.Tests.Ui.Layout.LayerDependencyTests" 2>&1 | Select-String "总计|失败|成功" | Select-Object -First 3
```

Expected: 4/4 passing.

### Step 3: Run full UI test suite

```bash
cd D:\kudi-project\world-view
dotnet run --project tests/WorldView.Tests/WorldView.Tests.csproj -c Debug --no-build -- --filter "WorldView.Tests.Ui" 2>&1 | Select-String "总计|失败|成功" | Select-Object -First 3
```

Expected: 99/99 passing (95 prior + 4 new).

### Step 4: F5 visual checklist (manual)

Open `C:\App\Godot_v4.7.2-stable_mono_win64\Godot.exe`, load `D:\kudi-project\world-view\project.godot`, F5:

- [ ] BottomPalette 3 rows render (timeline / build / tool)
- [ ] Drag Inspector to Left Dock, dock snaps
- [ ] Drag Inspector to center, FloatWindow appears, drag title bar moves, X closes
- [ ] F11 toggles fullscreen, Esc / F11 again restores
- [ ] Open sample project, exit, reopen — layout restored (LayoutState round-trip)
- [ ] WorldDropSurface accepts a `PanelDragData` drop

If any F5 step fails, do not commit Task 3 — fix and re-run.

### Step 5: Commit

```bash
cd D:\kudi-project\world-view
git add tests/WorldView.Tests/Ui/Layout/LayerDependencyTests.cs
git -c core.autocrlf=false commit -m "test(ui): LayerDependencyTests 4 仓单 — 锁住 layer 单向依赖

Task 3 of C spec
- Leaf 不能 using Service/Chrome/Panels
- Service 不能 using Chrome/Panels
- Chrome 不能 using Panels
- UI 测试 99/99 (95 prior + 4 new), F5 视觉 6 项 manual checklist 全过

spec: docs/superpowers/specs/2026-10-10-ui-three-layer-restructure.md
plan: docs/superpowers/plans/2026-10-10-ui-three-layer-restructure.md"
```

---

## Self-Review

**1. Spec coverage:**
- §1 background — implicit (grep done in spec)
- §2 goal — Task 1 + 2 + 3 cover
- §3 4 layers + 1 seam — Task 1 realizes the directory structure
- §4 IPaletteHost (4.1) — preserved as-is
- §4 IDockHost (4.2) — Task 2 Step 1
- §4 IPanelRegistry (4.3) — Task 2 Step 2
- §5 static singletons — preserved in Task 2 Step 3 (`class DockLayout : ..., IDockHost, IPanelRegistry`)
- §6 directory changes — Task 1 Steps 1, 2
- §7.1 4 LayerDependencyTests — Task 3 Step 1
- §7.2 95/95 regression — Task 1 Step 7.5, Task 2 Step 7
- §7.3 build 0 errors / 0 new warnings — Task 1 Step 7, Task 2 Step 7 (note: spec says "not add new warnings" — implementer should diff warning count before/after)
- §7.4 F5 visual — Task 3 Step 4 (manual)
- §8 risks — addressed by 4 explicit Compile includes in Task 1 Step 3
- §9 out of scope — not touched

**2. Step scan:** every step has exactly one action with a checkable result.

**3. Type consistency:** `IDockHost.HandleBottomDrop(string, Vector2)`, `IPanelRegistry.Register(DockPanel)` defined in Task 2 Step 1-2 match consumer call sites in Task 2 Step 4-6.

**4. Review Focus:** F5 step added to Task 3 Step 4 (6 items). Reverse-`using` pinned by 4 LayerDependencyTests. Layout persistence verified in Task 3 Step 4. DropSurface drag verified in Task 3 Step 4.

**5. Proportion:** Spec is 290 lines, plan is 320 lines. Plan is mostly file paths + commands (not code), so proportion is reasonable.
