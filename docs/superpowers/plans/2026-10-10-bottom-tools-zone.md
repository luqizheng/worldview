# 底部工具区重构（底部分区）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把底部 `build_row` 的 section 由「小地图 + 建造网格」改为「小地图 + 工具方块区」，把地形色块从 `ToolStrip` 迁入工具方块区，并把折叠态由「两行文本提示」改为 `ToolStrip` 紧凑常驻变体（高度仍 40px）。

**Architecture:** 不动 `rows→sections` 容器体系（`PaletteRow` / `PaletteSection` / `DockLayout` / `PanelState` 机制零改动），只做三件事：① 新增一个 `PaletteContent` 子类 `ToolPaletteSection` 承载「工具方块区」（内部分组：地形 / 建筑，遵循 K5「单一归属地 + 内部分组」）；② 改 `BottomPalette.EnsureDefaultRows()` 与 `PanelState.Default()` 的**默认 section 配置**；③ 给 `ToolStrip` 加 `SetCollapsedMode(bool)` 与三个可折叠分组容器，`BottomPalette.SetCollapsed` 折叠时只收起 `timeline_row` / `build_row`，保留 `tool_row` 常驻。

**Tech Stack:** Godot 4.7.2-stable mono + C#（net8.0）、Godot 内置 `Control` / `Container` 布局、xunit.v3 4.0.1 + Microsoft.Testing.Platform 2.4.0（源码 / 场景文本护栏测试，无 Godot 运行时）。

**Spec:** `docs/主界面布局规格_v0.1.md`（§4 底部=工具区、§9 增量 1 / 3 / 4 / 5、§2 布局总览、§0.1 已实现架构纪律）

## Global Constraints

- 技术栈 Godot 4.7.2-stable mono + C#；`.NET SDK 8.0.425`（`global.json` 固定）。
- `scripts/Domain/**` 是领域层，**不得引用 `Godot.*`**。本计划不修改领域层。
- 文档、注释、UI 文案用中文；类名 / 文件名 / YAML 键名用英文。
- `PanelState` / `LayoutState` 的 `schema_version` 保持 **1**，本轮**不改 schema**；新增 section 只沿用既有字段（`Id` / `Order` / `SizeMode` / `Flex` / `Visible`）。
- 只允许「在既有结构内改默认值或追加节点」：`DockLayout` / `DockZone` / `DockPanel` / `PaletteRow` / `PaletteSection` 的机制性代码不改，只允许新增 `PaletteContent` 子类、`ToolStrip` 的分组容器与折叠方法、`BottomPalette` 的默认配置与折叠分支。
- 场景策略：改现有场景，**不用 `.bak` 副本**；新增 `scenes/ui/ToolPaletteSection.tscn` 照抄既有最小场景模板。
- 新增 `scripts/**/*.cs` 后 Godot 会生成同名 `.cs.uid`，**必须 `git add` 入库**（AGENTS.md）。
- 构建：`dotnet build WorldView.sln -c Debug`
- 测试：直接裸跑 `tests/WorldView.Tests/bin/Debug/net8.0/WorldView.Tests.exe`（退出码 0 = 全绿）——**不要用 `dotnet test`**（MTP + xunit.v3，`OutputType=Exe`）。
- 无头校验：`& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path <repo> --check-only --quit-after 120`（**必须带 `--quit-after`**，`autoload/mcp_interaction_server.gd` 是常驻 TCP 服务）。
- UI 测试是**源码 / 场景文本护栏**：`File.ReadAllText` 读 `scripts/**/*.cs` 或 `scenes/*.tscn` 后断言子串，**不实例化 Godot 节点**（测试进程没有 Godot 运行时）。
- 验收基线（`docs/acceptance/baseline_v0.1.md`，P0-C2 done）：B1 = 0 error / 48 warnings（必须 `--no-incremental` 全量口径；2026-10-10 重复 glob 修复后重设）；B2 = 351 总 / 343 成功 / 稳定失败 7 / flaky 1~4；B3 = 退出码 0。

## Review Focus

1. **折叠时不得把行的 `visible: false` 落盘**——折叠态下 `timeline_row` / `build_row` 被 `SetCollapsed` 临时隐藏，若 `CaptureLayout` 照抄 `row.Visible` 就会把 `visible: false` 写进 `.worldview/layouts/<project>.yaml`，下次启动底部缺行（与 2026-10-10 F11 全屏事故同源）。**落在 Task A4 的 `CaptureLayout` 守卫与护栏测试。**
2. **section 内容场景缺失是静默降级**——`LoadContent` 找不到场景只 `GD.PushWarning` 并留空 section，`tools` 场景路径写错会静默变成空方块区。**落在 Task A1 的场景文件护栏与 Task A2 的默认 section 配置护栏。**
3. **两个内部分组不得同时可见**——`_groupHost` 里的地形网格与建筑面板都是全矩形锚点，若分组切换只设 `Visible = true` 而不设 `false`，两组会叠在一起。**落在 Task A1 的 `SelectGroup` 测试。**
4. **旧布局文件必须被跳过而不是崩**——`examples/sample_world/.worldview/layouts/sample_world.yaml`（未入库，运行时生成）里仍是 `build_palette` 节，`ApplySectionStates` 必须靠 `SectionOf(id) is null → continue` 跳过它。**落在 Task A2 的 `ApplySectionStates` 早退护栏。**
5. **`ToolStrip` 迁走地形后仍必须可用**——工具切换三键、笔刷半径 SpinBox、坐标标签、撤销 / 重做仍在。**落在 Task A3 的 ToolStrip 保留项测试。**
6. **折叠态不造成「零入口」**——折叠（40px）后必须有工具入口，且 `tool_row` 是唯一常驻行。**落在 Task A4 的折叠常驻行测试。**

---

### Task A0: 备份与基线

**Files:**
- 无代码改动（只做 git 操作与基线记录）

**Interfaces:**
- Consumes: 现状 HEAD `6e7ee75`、分支 `deepseek-build`、干净工作区
- Produces: 备份 tag `backup/ui-redesign-bottom-tools`；基线数据记录在计划执行日志中（B1 / B2 / B3）

- [ ] **Step 1: 确认工作区干净并打备份 tag**

```powershell
cd D:\kudi-project\world-view
git status --short
git tag -a backup/ui-redesign-bottom-tools -m "备份：底部工具区重构（主界面布局规格 v0.1 增量 1/3/4/5）开始前的 HEAD"
git tag --list "backup/*"
```

期望：`git status --short` 无输出；`git tag` 列出新 tag。

- [ ] **Step 2: 记录构建基线（B1）**

```powershell
dotnet build WorldView.sln -c Debug --no-incremental 2>&1 | Select-String -Pattern "Warning|Error|警告|错误" | Select-Object -Last 8
```

期望：0 error / 184 warnings。**数值记进执行日志**；若与基线不符，先停下来报告，不要继续。

- [ ] **Step 3: 记录测试基线（B2 / B3）**

```powershell
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
$LASTEXITCODE
```

期望：退出码 0；351 总 / 343 成功 / 稳定失败 7 / flaky 1~4。**数值记进执行日志。**

- [ ] **Step 4: 记录无头校验基线**

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path D:\kudi-project\world-view --check-only --quit-after 120
$LASTEXITCODE
```

期望：退出码 0。

---

### Task A1: 新增「工具方块区」容器 `ToolPaletteSection`

**Files:**
- Create: `scripts/Ui/Chrome/ToolPaletteSection.cs`
- Create: `scenes/ui/ToolPaletteSection.tscn`
- Create（Godot 运行后生成，必须入库）: `scripts/Ui/Chrome/ToolPaletteSection.cs.uid`
- Test: `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs`

**Interfaces:**
- Consumes: `WorldView.Ui.Chrome.PaletteContent`（抽象基类，提供 `protected static EditorContext? Context`、`public virtual void ApplyState(PanelState?)`、`public virtual void CaptureState(PanelState)`）、`WorldView.Ui.Chrome.AdaptiveGrid`（`CellSize` / `CellGap` / `void SetItems(IEnumerable<Control>)`）、`WorldView.Ui.Chrome.TerrainSwatch`（`TerrainType` / `void SetSelected(bool)`）、`WorldView.Ui.Chrome.BuildPaletteSection`（场景 `res://scenes/ui/BuildPaletteSection.tscn`）、`WorldView.Domain.TerrainType`（11 值）、`WorldView.Editor.ToolState.Brush`、`WorldView.Ui.Dock.Leaf.PanelState`
- Produces:
  - `public partial class ToolPaletteSection : PaletteContent`
  - `public const string TerrainGroupId = "terrain";`
  - `public const string BuildingGroupId = "building";`
  - `public void SelectGroup(string groupId)`
  - `public string ActiveGroup { get; }`
  - `public override void ApplyState(PanelState? state)`
  - `public override void CaptureState(PanelState state)`

- [ ] **Step 1: 写失败测试 `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs`**

```csharp
using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 row1 的 <c>tools</c> 工具方块区 / §9 增量 1、5）：
/// ① 场景可被 <c>BottomPalette.LoadContent("ToolPaletteSection")</c> 加载且脚本路径正确；
/// ② 内部分组（地形 / 建筑）互斥可见，任何时刻只显示一个分组；
/// ③ 建筑面板的私有布局状态经本 section 转发进同一个 <c>PanelState</c>（schema 不变）。
/// </summary>
public class ToolPaletteSectionTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string ScriptSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolPaletteSection.cs"));

    private static string SceneSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scenes", "ui", "ToolPaletteSection.tscn"));

    [Fact]
    public void 场景必须存在并挂载工具方块区脚本()
    {
        Assert.Contains("path=\"res://scripts/Ui/Chrome/ToolPaletteSection.cs\"", SceneSource, StringComparison.Ordinal);
        Assert.Contains("type=\"Control\"", SceneSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 必须定义地形与建筑两个分组ID()
    {
        Assert.Contains("public const string TerrainGroupId = \"terrain\";", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("public const string BuildingGroupId = \"building\";", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 分组切换必须互斥设置可见性()
    {
        Assert.Contains("_terrainGrid.Visible = isTerrain;", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("_buildPalette.Visible = !isTerrain;", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 必须加载建筑面板场景()
    {
        Assert.Contains("\"res://scenes/ui/BuildPaletteSection.tscn\"", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("InstantiateOrNull<BuildPaletteSection>()", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 地形色块必须由TerrainType全量生成()
    {
        Assert.Contains("foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("new TerrainSwatch { Name = \"Swatch\" + captured, TerrainType = captured }", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 点地形色块必须切换成笔刷工具并设绘制地形()
    {
        Assert.Contains("context.SetPaintTerrain(terrain);", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("context.SetTool(ToolState.Brush);", ScriptSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 建筑面板状态必须转发进本section的PanelState()
    {
        Assert.Contains("_buildPalette?.ApplyState(state);", ScriptSource, StringComparison.Ordinal);
        Assert.Contains("_buildPalette?.CaptureState(state);", ScriptSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：编译通过但 `ToolPaletteSectionTests` 全部因文件不存在抛 `FileNotFoundException` / `DirectoryNotFoundException` 而失败（其它测试仍绿）。

- [ ] **Step 3: 创建场景 `scenes/ui/ToolPaletteSection.tscn`**

照抄 `scenes/ui/BuildPaletteSection.tscn` 的 7 行最小模板，把脚本路径与节点名换成 `ToolPaletteSection`：

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/Ui/Chrome/ToolPaletteSection.cs" id="1_toolpalette"]

[node name="ToolPaletteSection" type="Control"]
layout_mode = 2
script = ExtResource("1_toolpalette")
```

- [ ] **Step 4: 创建 `scripts/Ui/Chrome/ToolPaletteSection.cs`**

```csharp
using System;
using System.Collections.Generic;
using Godot;
using WorldView.Domain;
using WorldView.Editor;
using WorldView.Ui.Dock.Leaf;

namespace WorldView.Ui.Chrome;

/// <summary>
/// 工具方块区（spec §4 row1 的 <c>tools</c> section / §9 增量 1、5）。
///
/// <para>行内与「小地图」并排：小地图（fixed / width=auto 方形，默认 104×104）+ 本区（flex）。
/// 内部分为「地形 / 建筑」两个分组（K5：单一归属地 + 内部分组，同一时刻只显示一个分组），
/// 分组按钮在区头，内容区复用既有 <see cref="AdaptiveGrid"/> 与「分类 + 分页」范式。</para>
///
/// <para>地形分组由 <see cref="TerrainType"/> 的 11 个值生成 11 个 <see cref="TerrainSwatch"/> 方块
/// （原先在 <c>ToolStrip</c> 的 <c>TerrainStrip</c>，本轮迁到这里）；建筑分组内嵌
/// <c>scenes/ui/BuildPaletteSection.tscn</c>，其私有布局状态（分类 / 分页）原样写回本 section 的
/// <see cref="PanelState"/>——spec §8 要求 schema 不变、字段语义不挪用，所以只转发不新增字段。</para>
/// </summary>
public partial class ToolPaletteSection : PaletteContent
{
    /// <summary>地形分组 ID。</summary>
    public const string TerrainGroupId = "terrain";

    /// <summary>建筑分组 ID。</summary>
    public const string BuildingGroupId = "building";

    private const string BuildingScenePath = "res://scenes/ui/BuildPaletteSection.tscn";

    private readonly List<TerrainSwatch> _swatches = new();
    private readonly Dictionary<string, Button> _groupButtons = new();
    private HBoxContainer? _groupBar;
    private Control? _groupHost;
    private AdaptiveGrid? _terrainGrid;
    private BuildPaletteSection? _buildPalette;
    private string _activeGroup = TerrainGroupId;
    private bool _syncing;
    private bool _bound;
    private TerrainType _lastTerrain = TerrainType.Plain;

    /// <summary>当前分组 ID。</summary>
    public string ActiveGroup => _activeGroup;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ClipContents = true;
        CustomMinimumSize = new Vector2(180f, 72f);

        var root = new VBoxContainer { Name = "Root" };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.OffsetLeft = 2f;
        root.OffsetTop = 1f;
        root.OffsetRight = -2f;
        root.OffsetBottom = -1f;
        root.AddThemeConstantOverride("separation", 2);
        AddChild(root);

        _groupBar = new HBoxContainer { Name = "GroupBar", CustomMinimumSize = new Vector2(0f, 20f) };
        _groupBar.AddThemeConstantOverride("separation", 4);
        root.AddChild(_groupBar);

        var group = new ButtonGroup();
        AddGroupButton(group, TerrainGroupId, "地形");
        AddGroupButton(group, BuildingGroupId, "建筑");

        _groupHost = new Control
        {
            Name = "Groups",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_groupHost);

        _terrainGrid = new AdaptiveGrid
        {
            Name = "TerrainGrid",
            CellSize = 44,
            CellGap = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _terrainGrid.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _groupHost.AddChild(_terrainGrid);
        _terrainGrid.SetItems(BuildTerrainItems());

        if (ResourceLoader.Exists(BuildingScenePath))
        {
            _buildPalette = GD.Load<PackedScene>(BuildingScenePath)?.InstantiateOrNull<BuildPaletteSection>();
        }

        if (_buildPalette is not null)
        {
            _buildPalette.Name = "BuildPalette";
            _buildPalette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _groupHost.AddChild(_buildPalette);
        }
        else
        {
            GD.PushWarning($"缺少 UI 场景，工具方块区的建筑分组为空：{BuildingScenePath}");
        }

        SelectGroup(_activeGroup);
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        if (!_bound)
        {
            _bound = true;
            _lastTerrain = context.PaintTerrain;
            SyncTerrain(_lastTerrain);
            return;
        }

        if (context.PaintTerrain != _lastTerrain)
        {
            _lastTerrain = context.PaintTerrain;
            SyncTerrain(_lastTerrain);
        }
    }

    /// <summary>切换内部分组（同一时刻只显示一个分组）。</summary>
    public void SelectGroup(string groupId)
    {
        _activeGroup = groupId == BuildingGroupId ? BuildingGroupId : TerrainGroupId;

        var isTerrain = _activeGroup == TerrainGroupId;
        if (_terrainGrid is not null)
        {
            _terrainGrid.Visible = isTerrain;
        }

        if (_buildPalette is not null)
        {
            _buildPalette.Visible = !isTerrain;
        }

        _syncing = true;
        foreach (var pair in _groupButtons)
        {
            pair.Value.ButtonPressed = pair.Key == _activeGroup;
        }

        _syncing = false;
    }

    public override void ApplyState(PanelState? state)
    {
        _buildPalette?.ApplyState(state);
    }

    public override void CaptureState(PanelState state)
    {
        _buildPalette?.CaptureState(state);
    }

    private void AddGroupButton(ButtonGroup group, string groupId, string title)
    {
        var captured = groupId;
        var button = new Button
        {
            Name = "Group_" + groupId,
            Text = title,
            ToggleMode = true,
            ButtonGroup = group,
            CustomMinimumSize = new Vector2(48f, 20f),
            TooltipText = $"{title}工具",
        };
        button.AddThemeFontSizeOverride("font_size", 10);
        button.Toggled += pressed =>
        {
            if (_syncing || !pressed)
            {
                return;
            }

            SelectGroup(captured);
        };

        _groupButtons[groupId] = button;
        _groupBar?.AddChild(button);
    }

    private List<Control> BuildTerrainItems()
    {
        var items = new List<Control>();
        foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
        {
            var captured = terrain;
            var swatch = new TerrainSwatch { Name = "Swatch" + captured, TerrainType = captured };
            swatch.Pressed += () => OnSwatchPressed(captured);
            _swatches.Add(swatch);
            items.Add(swatch);
        }

        return items;
    }

    private void OnSwatchPressed(TerrainType terrain)
    {
        var context = Context;
        if (context is null)
        {
            return;
        }

        context.SetPaintTerrain(terrain);
        context.SetTool(ToolState.Brush);
        SyncTerrain(terrain);
    }

    private void SyncTerrain(TerrainType active)
    {
        foreach (var swatch in _swatches)
        {
            swatch.SetSelected(swatch.TerrainType == active);
        }
    }
}
```

- [ ] **Step 5: 构建 + 跑测试确认通过**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：退出码 0，`ToolPaletteSectionTests` 全绿。

- [ ] **Step 6: 让 Godot 生成 `.uid` 并做无头校验**

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path D:\kudi-project\world-view --check-only --quit-after 120
Get-ChildItem scripts\Ui\Chrome\ToolPaletteSection.cs.uid
```

期望：退出码 0；`.uid` 文件存在。

- [ ] **Step 7: Commit**

```powershell
git add scripts/Ui/Chrome/ToolPaletteSection.cs scripts/Ui/Chrome/ToolPaletteSection.cs.uid scenes/ui/ToolPaletteSection.tscn tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs
git commit -m "feat(ui): 新增工具方块区 section 容器（主界面布局规格 §4 row1）"
```

---

### Task A2: 默认 section 配置 `build_palette` → `tools`

**Files:**
- Modify: `scripts/Ui/Chrome/BottomPalette.cs`（常量区、`EnsureDefaultRows()`）
- Modify: `scripts/Ui/Dock/Leaf/PanelState.cs`（`Default()` 的 `build_row` section 列表 + 4 个字段的注释）
- Test: `tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs`

**Interfaces:**
- Consumes: Task A1 的 `ToolPaletteSection`（场景名 `ToolPaletteSection`）、`BottomPalette.CreateSection(string sectionId, string title, PaletteSection.SectionSizeMode mode, int flex, bool square, string contentScene)`、`BottomPalette.SectionOf(string)`、`PaletteSection.SectionSizeMode`
- Produces: `public const string BottomPalette.ToolsSectionId = "tools";`；`PanelState.Default()` 的 `build_row` 里出现 `Id = "tools"` 且不再有 `build_palette`

- [ ] **Step 1: 写失败测试 `tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs`**

```csharp
using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 row1 / §8 / §9 增量 1、5）：
/// ① 默认 section 配置里 building row 由 <c>build_palette</c> 换成 <c>tools</c>（工具方块区）；
/// ② 小地图仍是 fixed / width=auto 的方形；
/// ③ 旧布局文件里已不存在的 section（如 <c>build_palette</c>）必须被跳过而不是崩；
/// ④ schema 版本不变、折叠 / 展开高度不变。
/// </summary>
public class BottomToolsZoneDefaultsTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string Read(params string[] parts)
    {
        var path = RepoRoot;
        foreach (var part in parts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string BottomPaletteSource => Read("scripts", "Ui", "Chrome", "BottomPalette.cs");

    private static string PanelStateSource => Read("scripts", "Ui", "Dock", "Leaf", "PanelState.cs");

    [Fact]
    public void 默认布局必须用工具方块区替换建造网格()
    {
        Assert.Contains("public const string ToolsSectionId = \"tools\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CreateSection(ToolsSectionId, \"工具\", PaletteSection.SectionSizeMode.Flex, 1, false, \"ToolPaletteSection\")", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"build_palette\"", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 默认布局的_build_row_必须含_tools_节且不再含_build_palette()
    {
        Assert.Contains("Id = \"tools\"", PanelStateSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"build_palette\"", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 小地图必须仍是_fixed_auto_方形()
    {
        Assert.Contains("CreateSection(\"minimap\", \"小地图\", PaletteSection.SectionSizeMode.Fixed, 1, true, \"MinimapSection\")", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("Width = \"auto\"", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 三行结构必须不变()
    {
        Assert.Contains("private const string TimelineRowId = \"timeline_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string BuildRowId = \"build_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string ToolRowId = \"tool_row\";", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CreateRow(BuildRowId, fullWidth: false, height: 0, flex: 1, order: 1)", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 高度与schema版本必须不变()
    {
        Assert.Contains("public const int ExpandedHeight = 200;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("public const int CollapsedHeight = 40;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("public const int CurrentSchemaVersion = 1;", PanelStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 未知section必须被跳过而不是崩()
    {
        Assert.Contains("var section = SectionOf(sectionState.Id);", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("if (section is null)", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("continue;", BottomPaletteSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：编译通过；`默认布局必须用工具方块区替换建造网格` 与 `默认布局的_build_row_必须含_tools_节且不再含_build_palette` 失败（其余通过）。

- [ ] **Step 3: 改 `scripts/Ui/Chrome/BottomPalette.cs`**

在常量区（`TimelineRowId` / `BuildRowId` / `ToolRowId` 下方）追加：

```csharp
    /// <summary>工具方块区 section 的 ID（spec §4 row1：小地图 + 工具方块区）。</summary>
    public const string ToolsSectionId = "tools";

    /// <summary>工具行 section 的 ID（折叠态由它常驻）。</summary>
    private const string ToolStripSectionId = "tool_strip";
```

把 `EnsureDefaultRows()` 里第 151 行

```csharp
        buildRow.AddSection(CreateSection("build_palette", "建筑", PaletteSection.SectionSizeMode.Flex, 1, false, "BuildPaletteSection"));
```

替换为：

```csharp
        buildRow.AddSection(CreateSection(ToolsSectionId, "工具", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolPaletteSection"));
```

- [ ] **Step 4: 改 `scripts/Ui/Dock/Leaf/PanelState.cs`**

在 `LayoutState.Default(string projectRoot)` 的 `build_row` 的 `Sections` 列表里，把 `Id = "build_palette"` 的那一项改成 `Id = "tools"`；该项上原有的 `CellSize = 56` / `CellGap = 4` / `ActiveCategory = "residences"` / `PagesByCategory = ...` **保留不动**（它们仍是建筑面板的私有状态，只是现在经过 `tools` section 由 `ToolPaletteSection` 转发读写）。

同时把这 4 个字段的 XML 注释里的「仅 build_palette」改为「建筑面板私有状态（内嵌在 tools 工具方块区，经 ToolPaletteSection 转发）」。

- [ ] **Step 5: 构建 + 跑测试确认通过**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：退出码 0，`BottomToolsZoneDefaultsTests` 全绿。

- [ ] **Step 6: Commit**

```powershell
git add scripts/Ui/Chrome/BottomPalette.cs scripts/Ui/Dock/Leaf/PanelState.cs tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs
git commit -m "feat(ui): 底部 build_row 默认 section 改为工具方块区（spec §4/§9-1）"
```

---

### Task A3: 地形色块迁入工具方块区 + `ToolStrip` 分组化

**Files:**
- Modify: `scripts/Ui/Chrome/ToolStrip.cs`（删地形段、加三个分组容器与 `SetCollapsedMode`）
- Test: `tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs`

**Interfaces:**
- Consumes: Task A1 的 `ToolPaletteSection`（已承载地形色块）、`ToolStrip._bar`（`HBoxContainer`）、`ToolStrip.MakeCaption(string)`、现有 `_radiusSpin` / `_radiusUnit` / `_radiusValue` / `_refineCheck` / `_undoButton` / `_redoButton` / `_coordLabel` / `_statusLabel` / `_validationLabel`
- Produces: `public void ToolStrip.SetCollapsedMode(bool collapsed)`；私有字段 `_brushGroup` / `_historyGroup` / `_stateGroup`（`HBoxContainer?`）；私有 `static Label NewSeparator()`

- [ ] **Step 1: 写失败测试 `tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs`**

```csharp
using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 实现提示「地形色块宜从 ToolStrip 迁入工具方块区，ToolStrip 只留笔刷半径/坐标/状态/校验」）：
/// ① <c>ToolStrip</c> 不再承载地形色块；
/// ② 工具方块区承载地形色块；
/// ③ <c>ToolStrip</c> 保留工具切换三键与笔刷半径 / 坐标；
/// ④ 折叠态（K1）必须有紧凑变体入口。
/// </summary>
public class ToolZoneMigrationTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string ToolStripSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolStrip.cs"));

    private static string ToolPaletteSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "ToolPaletteSection.cs"));

    [Fact]
    public void ToolStrip不得再承载地形色块()
    {
        Assert.DoesNotContain("TerrainStrip", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerrainSwatch", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_terrainSwatches", ToolStripSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TerrainOrder", ToolStripSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 工具方块区必须承载地形色块()
    {
        Assert.Contains("new TerrainSwatch", ToolPaletteSource, StringComparison.Ordinal);
        Assert.Contains("context.SetPaintTerrain(terrain);", ToolPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolStrip必须保留工具切换三键与笔刷半径与坐标()
    {
        Assert.Contains("ToolState.Selected", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("ToolState.Brush", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("ToolState.Building", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_radiusSpin", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_coordLabel", ToolStripSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolStrip必须提供折叠紧凑变体()
    {
        Assert.Contains("public void SetCollapsedMode(bool collapsed)", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_brushGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_historyGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
        Assert.Contains("_stateGroup.Visible = !collapsed;", ToolStripSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：`ToolStrip不得再承载地形色块` 与 `ToolStrip必须提供折叠紧凑变体` 失败。

- [ ] **Step 3: 从 `ToolStrip.cs` 移除地形色块**

精确删除以下四处（行号按迁移前）：

1. 第 45 行：`private static readonly TerrainType[] TerrainOrder = (TerrainType[])Enum.GetValues(typeof(TerrainType));`
2. `_terrainSwatches` 字段声明。
3. `BuildUi()` 里地形那一段：`AddSeparator();` + `MakeCaption("地形");` + `terrainStrip = new HBoxContainer { Name = "TerrainStrip" };` 起、到遍历 `TerrainOrder` 建 `TerrainSwatch` 并 `terrainStrip.AddChild(swatch)` 结束的整段（第 127 行 `var swatch = new TerrainSwatch ...` 在其中）。
4. 方法 `OnSwatchPressed(TerrainType terrain)`（第 570 行）与 `SyncTerrain(TerrainType terrain)`（第 582 行），以及 `Bind(EditorContext context)` 里调用 `SyncTerrain(...)` 的那一行。

删除后若 `using WorldView.Domain;` 变为未使用则一并移除（`TerrainType` 在本类不再出现）。

- [ ] **Step 4: 给 `ToolStrip` 加分组容器与 `SetCollapsedMode`**

新增字段：

```csharp
    private HBoxContainer? _brushGroup;
    private HBoxContainer? _historyGroup;
    private HBoxContainer? _stateGroup;
```

在 `BuildUi()` 里把原先直接加到 `_bar` 的三部分改成先装进分组容器，再把分组容器按顺序加到 `_bar`。**目标子节点顺序**（折叠 / 展开共用同一顺序）：

`[V][B][P]` → `_brushGroup` → `_historyGroup` → 独立分隔符 → `_coordLabel` → `_stateGroup`

- `_brushGroup` = `[分隔符, MakeCaption("笔刷"), _radiusSpin, _radiusUnit, _radiusValue, _refineCheck]`
- `_historyGroup` = `[分隔符, _undoButton, _redoButton]`
- `_stateGroup` = `[分隔符, _statusLabel, _validationLabel]`

把现有的 `AddSeparator()` 拆成「造一个分隔符」与「加到某容器」两步：

```csharp
    private static Label NewSeparator()
    {
        var separator = MakeCaption("│");
        separator.AddThemeColorOverride("font_color", new Color(0.30f, 0.30f, 0.36f));
        return separator;
    }
```

（`AddSeparator()` 若保留则实现为 `_bar!.AddChild(NewSeparator())`；三个分组容器内的分隔符一律用 `NewSeparator()` 直接 `AddChild` 进组。）

新增方法（折叠态 = K1：只留工具切换图标 + 坐标）：

```csharp
    /// <summary>
    /// 折叠态紧凑变体（spec §4 折叠态 C3 = K1）：只保留工具切换三键与坐标，
    /// 隐藏笔刷半径、撤销 / 重做、状态与校验。展开态恢复全部。
    /// </summary>
    public void SetCollapsedMode(bool collapsed)
    {
        if (_brushGroup is not null)
        {
            _brushGroup.Visible = !collapsed;
        }

        if (_historyGroup is not null)
        {
            _historyGroup.Visible = !collapsed;
        }

        if (_stateGroup is not null)
        {
            _stateGroup.Visible = !collapsed;
        }
    }
```

- [ ] **Step 5: 构建 + 跑测试确认通过**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：退出码 0，`ToolZoneMigrationTests` 全绿。

- [ ] **Step 6: Commit**

```powershell
git add scripts/Ui/Chrome/ToolStrip.cs tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs
git commit -m "refactor(ui): 地形色块迁入工具方块区，ToolStrip 分组化并加折叠紧凑变体（spec §4/§9-1）"
```

---

### Task A4: 折叠态改为 `tool_row` 常驻紧凑行（去掉两行文本提示）

**Files:**
- Modify: `scripts/Ui/Chrome/BottomPalette.cs`（`BuildChrome()` 删 `_collapsedBar`、`SetCollapsed()` 重写、`CaptureLayout()` 加落盘守卫）
- Test: `tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs`

**Interfaces:**
- Consumes: Task A2 的 `BottomPalette.ToolsSectionId` 与 `ToolStripSectionId`、Task A3 的 `ToolStrip.SetCollapsedMode(bool)`、`BottomPalette.RowOf(string)` / `SectionOf(string)` / `MarkLayoutChanged()`
- Produces: 折叠态只收起 `timeline_row` / `build_row`，`tool_row` 常驻；`CaptureLayout` 在折叠态一律记 `Visible = true`

- [ ] **Step 1: 写失败测试 `tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs`**

```csharp
using System;
using System.IO;
using Xunit;

namespace WorldView.Tests.Ui.Chrome;

/// <summary>
/// 护栏（spec §4 折叠态 C3 = K1 / §9 增量 3、4）：
/// ① 折叠态不再是「面板已折叠」两行文本提示，也不是整体收起 Rows；
/// ② 折叠时只收起 timeline_row / build_row，tool_row 常驻；
/// ③ 折叠时 ToolStrip 切到紧凑变体；
/// ④ 折叠态下行的 visible 绝不能落盘（同 2026-10-10 F11 事故）；
/// ⑤ 折叠高度仍是 40px。
/// </summary>
public class BottomPaletteCollapsedTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WorldView.sln")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }

    private static string BottomPaletteSource => File.ReadAllText(
        Path.Combine(RepoRoot, "scripts", "Ui", "Chrome", "BottomPalette.cs"));

    [Fact]
    public void 折叠态不得再用两行文本提示()
    {
        Assert.DoesNotContain("_collapsedBar", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("面板已折叠", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl+B 展开 · T 播放条 · M 小地图", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须常驻tool_row并收起另两行()
    {
        Assert.Contains("timelineRow.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("buildRow.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("toolRow.Visible = true;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_rows.Visible = !collapsed;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须让Rows纵向居中20px常驻行()
    {
        Assert.Contains("_rows.OffsetTop = collapsed ? 10f : 0f;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("_rows.OffsetBottom = collapsed ? -10f : 0f;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠态必须切到ToolStrip紧凑变体()
    {
        Assert.Contains("toolStrip.SetCollapsedMode(collapsed);", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("private const string ToolStripSectionId = \"tool_strip\";", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠时不得把行的invisible落盘()
    {
        Assert.Contains("rowState.Visible = _collapsed ? true : row.Visible;", BottomPaletteSource, StringComparison.Ordinal);
    }

    [Fact]
    public void 折叠高度必须仍是40()
    {
        Assert.Contains("public const int CollapsedHeight = 40;", BottomPaletteSource, StringComparison.Ordinal);
        Assert.Contains("CustomMinimumSize = new Vector2(CustomMinimumSize.X, collapsed ? CollapsedHeight : _expandedHeight);", BottomPaletteSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：`折叠态不得再用两行文本提示` / `折叠态必须常驻tool_row并收起另两行` / `折叠时不得把行的invisible落盘` 失败。

- [ ] **Step 3: 删除 `_collapsedBar`**

在 `scripts/Ui/Chrome/BottomPalette.cs` 里：

1. 删字段 `private HBoxContainer? _collapsedBar;`
2. 删 `BuildChrome()` 里第 97-122 行整段（`_collapsedBar = new HBoxContainer { Name = "CollapsedBar", Visible = false };` 与其中两个 `Label`：`"面板已折叠"`、`"Ctrl+B 展开 · T 播放条 · M 小地图"`）。

保留 `_rows` 始终 `Visible = true`（不再整体收起），保留 `OffsetRight = -28f` 给折叠按钮。

- [ ] **Step 4: 重写 `SetCollapsed(bool collapsed)`**

整段替换为：

```csharp
    /// <summary>
    /// 切换折叠态（展开 200px ↔ 折叠 40px）。
    ///
    /// <para>K1（spec §4）：折叠态 = <see cref="ToolStrip"/> 紧凑常驻变体。
    /// 折叠时<b>不</b>整体收起 <c>Rows</c>，而是只收起 <c>timeline_row</c> 与 <c>build_row</c>，
    /// 保留 <c>tool_row</c> 常驻并让 <see cref="ToolStrip"/> 切到紧凑变体（工具切换图标 + 坐标）；
    /// 这样折叠后仍有工具入口，避免「属性面板默认隐藏 + 底部折叠 = 零入口」。</para>
    /// </summary>
    public void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;

        if (RowOf(TimelineRowId) is { } timelineRow)
        {
            timelineRow.Visible = !collapsed;
        }

        if (RowOf(BuildRowId) is { } buildRow)
        {
            buildRow.Visible = !collapsed;
        }

        if (RowOf(ToolRowId) is { } toolRow)
        {
            toolRow.Visible = true;
        }

        if (SectionOf(ToolStripSectionId)?.Content is ToolStrip toolStrip)
        {
            toolStrip.SetCollapsedMode(collapsed);
        }

        if (_rows is not null)
        {
            // 折叠态 Rows 高 40px 而 tool_row 只占 20px：上下各留 10px 让它纵向居中。
            _rows.OffsetTop = collapsed ? 10f : 0f;
            _rows.OffsetBottom = collapsed ? -10f : 0f;
        }

        if (_collapseButton is not null)
        {
            _collapseButton.Text = collapsed ? "▴" : "▾";
            _collapseButton.TooltipText = collapsed ? "展开面板 (Ctrl+B)" : "折叠面板 (Ctrl+B)";
        }

        CustomMinimumSize = new Vector2(CustomMinimumSize.X, collapsed ? CollapsedHeight : _expandedHeight);
        MarkLayoutChanged();
    }
```

- [ ] **Step 5: 给 `CaptureLayout` 加折叠落盘守卫**

在 `CaptureLayout(PanelState state)` 的行循环里，把

```csharp
            rowState.Visible = row.Visible;
```

替换为：

```csharp
            // 折叠态下 timeline_row / build_row 被 SetCollapsed 临时隐藏，这不是用户意图，
            // 绝不能落盘（同 2026-10-10 F11 全屏事故）。折叠时一律记 true。
            rowState.Visible = _collapsed ? true : row.Visible;
```

- [ ] **Step 6: 构建 + 跑测试确认通过**

```powershell
dotnet build WorldView.sln -c Debug
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

期望：退出码 0，`BottomPaletteCollapsedTests` 全绿。

- [ ] **Step 7: 无头校验 + 手动冒烟（若可开窗口）**

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --headless --path D:\kudi-project\world-view --check-only --quit-after 120
```

期望：退出码 0。手动冒烟清单（有窗口时执行）：① 展开态底部是 28px 时间轴 + 152px（小地图 + 工具方块区）+ 20px 工具栏；② 工具方块区「地形 / 建筑」两个分组按钮切换时只有一个分组可见；③ 点地形色块后画布可绘制，且工具切到笔刷；④ Ctrl+B 折叠后只剩一行紧凑工具行（V/B/P + 坐标 + ▴），点 ▴ 展开恢复；⑤ 反复折叠 / 展开后退出，检查 `.worldview/layouts/<project>.yaml` 里 `timeline_row` / `build_row` 的 `visible` 仍是 `true`。

- [ ] **Step 8: Commit**

```powershell
git add scripts/Ui/Chrome/BottomPalette.cs tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs
git commit -m "feat(ui): 折叠态改为 tool_row 常驻紧凑行并加落盘守卫（spec §4 C3=K1/§9-3、4）"
```

---

### Task A5: 同步修订旧规格 §3.2 的折叠态描述

**Files:**
- Modify: `.trae/specs/ui-redesign-simgame/spec.md`（§3.2 折叠态那一段）

**Interfaces:**
- Consumes: Task A4 确立的折叠语义
- Produces: 文档描述与实现一致（无代码）

- [ ] **Step 1: 读 §3.2 找到折叠态描述**

```powershell
Select-String -Path .trae\specs\ui-redesign-simgame\spec.md -Pattern "折叠|展开|CollapsedBar|面板已折叠"
```

- [ ] **Step 2: 把折叠态描述改为新语义**

把「折叠后整行（含 `timeline_row`）一起收起 / 显示两行文本提示」的表述改为：

> 折叠态（40px）= `ToolStrip` 紧凑常驻变体：`timeline_row` 与 `build_row`（小地图 + 工具方块区）收起，`tool_row` 常驻，其中 `ToolStrip` 只保留工具切换图标与坐标；展开恢复全部。折叠态下行的 `visible` 不落盘。

**不要** bump 本文件版本号（它是 `.trae` 下的旧规格，`docs/主界面布局规格_v0.1.md` 才是当前真源）。

- [ ] **Step 3: Commit**

```powershell
git add .trae/specs/ui-redesign-simgame/spec.md
git commit -m "docs: 旧规格 §3.2 折叠态描述同步为 ToolStrip 紧凑常驻变体"
```

---

## Self-Review

**1. Spec 覆盖**

| spec 要求 | 覆盖位置 |
| --- | --- |
| §4 row1 `build_row` = 小地图 + 工具方块区 | A2（默认 section 配置）+ A1（容器） |
| §4 小地图 fixed / width=auto 方形 104×104 | A2 Step 1 护栏断言（代码不动，`MinimapSection` 已满足） |
| §4 地形色块从 `ToolStrip` 迁入工具方块区 | A3 |
| §4 `ToolStrip` 只留笔刷半径 / 坐标 / 状态 / 校验 | A3 Step 4 分组化 + 保留项测试 |
| §4 折叠高度 40px | A4（`CollapsedHeight = 40` 未变 + 护栏） |
| §4 折叠态 C3 = K1（ToolStrip 紧凑常驻变体） | A3 `SetCollapsedMode` + A4 `SetCollapsed` |
| §4 折叠态落盘安全 | A4 Step 5 守卫 + 护栏 |
| §4「不动容器代码，只调默认 section 配置」 | A2 只改 `EnsureDefaultRows()` / `Default()`；A1/A3 只新增节点与方法 |
| §4 同步修订旧规格 §3.2 | A5 |
| §8 不改 schema、不持久化当前页签 | A2（`CurrentSchemaVersion = 1` 护栏）+ A1（状态只转发，不新增字段） |
| §9 增量 1 / 3 / 4 / 5 | A1+A2+A3 / A4 / A4 / A1+A2 |
| §0.1 只做「既有结构内改默认值或追加节点」 | 全任务（`DockLayout` / `PaletteRow` / `PaletteSection` / `PanelState` 结构未改） |

**2. 步骤粒度**：每个 Task 都是「写失败测试 → 跑测试确认失败 → 实现 → 跑测试确认通过 → commit」，唯一例外是 A0（备份 / 基线，无测试对象）与 A5（纯文档）。

**3. 类型一致性**：`ToolPaletteSection` 走 `PaletteContent` 抽象基类（`ApplyState` / `CaptureState` 均为 `virtual`，签名 `PanelState?` / `PanelState` 与 `DockPanel.Content` 的 `PaletteContent` 判定一致）；`ToolStrip.SetCollapsedMode(bool)` 由 `BottomPalette.SetCollapsed` 通过 `SectionOf(ToolStripSectionId)?.Content is ToolStrip` 调用（`DockPanel.Content` 是 `Control?`，用 `is` 模式匹配无需强转）；`ToolsSectionId` 为 `public const string`，测试用文本断言读它。

**4. Review Focus 逐条落点**：① → A4 Step 5 + `折叠时不得把行的invisible落盘`；② → A1 Step 1 `场景必须存在并挂载工具方块区脚本` + A2 Step 1 `默认布局必须用工具方块区替换建造网格`；③ → A1 Step 1 `分组切换必须互斥设置可见性`；④ → A2 Step 1 `未知section必须被跳过而不是崩`；⑤ → A3 Step 1 `ToolStrip必须保留工具切换三键与笔刷半径与坐标`；⑥ → A4 Step 1 `折叠态必须常驻tool_row并收起另两行`。

**5. 篇幅比例**：A1（新容器，最多新代码）与 A4（折叠语义变更，含落盘风险）各占约 1/4，A2 / A3 各约 1/6，A0 / A5 为流程与文档。

**6. 明确不在本计划范围（后续独立计划）**：

- 浮动属性面板与选区身份管道（spec §5 / §9-2 / §9-6 / §10 的 P0、P1、P1.5）。
- 顶栏图层板入口（Ctrl+L）与事件入口（Ctrl+E）按钮（spec §3 / §9-2）——**注意 spec 内部冲突：Ctrl+L 在 §3/§10 给图层板，在 §5/§7 给属性面板，需用户裁定后再落地。**
- 世界 / 城市分层、Zone 实体与区域自动生长、选择器工具、下钻改口（spec §9-7 / §9-8 / §9-9）。
- 顶栏当前时间（M6 预留）。
- `examples/sample_world/.worldview/layouts/sample_world.yaml` 的迁移：该文件未入库、运行时生成，且 `ApplySectionStates` 已能跳过失效 section，**本轮不改它**（用户下次打开该工程时，`build_palette` 节被跳过，`tools` 节由默认配置建好）。

---

## Execution Handoff

计划已完成并保存到 `docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`。

请选择执行方式：

1. **Subagent-driven（推荐）**：每个 Task 派一个独立 subagent 实现，Task 之间做代码审查，主会话只做协调与集成验证。
2. **Native**：在当前会话里按 Task 顺序直接实现（`superpowers:executing-plans`），每个 Task 完成后停下来报告。

同时请确认两个待决点：

- **是否认可本计划先做「底部分区」（A0–A5）**，再另开计划做浮动属性面板与选区管道（spec §9-2 依赖 P0/P1，工作量最大且会碰 `ToolState` / `WorldCanvas` / `EditorContext` 三个热点）。
- **Ctrl+L 归属**（spec §3/§10 给图层板 vs §5/§7 给属性面板）需要一个裁定，否则图层板入口按钮无法落地。
