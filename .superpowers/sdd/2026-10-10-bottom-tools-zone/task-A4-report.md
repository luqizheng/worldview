# Task A4 报告：折叠态改为 `tool_row` 常驻紧凑行（去掉两行文本提示）

- **计划**：`docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`
- **任务书**：`.superpowers/sdd/2026-10-10-bottom-tools-zone/task-A4-brief.md`
- **提交**：`706841c` `feat(ui): 折叠态改为 tool_row 常驻紧凑行并加落盘守卫（spec §4 C3=K1/§9-3、4）`
- **状态**：DONE（无遗留顾虑，仅 1 项环境性说明，见「问题与说明」）
- **报告编码**：UTF-8（LF），未混入其它编码

---

## 1. 实现内容（逐成员：删除 / 新增 / 修改）

改动集中在 `scripts/Ui/Chrome/BottomPalette.cs`（1 个文件，+35 / −36），另新增 1 个测试文件与其 `.uid`。

### 1.1 删除 `_collapsedBar`（brief Step 3）

| 项 | 处理 |
| --- | --- |
| 字段 `private HBoxContainer? _collapsedBar;` | **删除** |
| `BuildChrome()` 中 `_collapsedBar = new HBoxContainer { Name = "CollapsedBar", Visible = false };` 及其锚点 / offset 设置整段 | **删除** |
| `Label { Text = "面板已折叠", … }`（`collapsedHint`） | **删除** |
| `Label { Text = "Ctrl+B 展开 · T 播放条 · M 小地图", … }`（`expandHint`） | **删除** |
| `_rows` 的 `Visible = true` 不变、`OffsetRight = -28f` 保留 | **未动**（仍保留折叠按钮的 28px 让位） |

`BuildChrome()` 现在以 `_collapseButton.Pressed += () => SetCollapsed(!_collapsed);` 收尾，紧随其后即为 `// ---- 默认三行` 分节注释。

### 1.2 重写 `SetCollapsed(bool collapsed)`（brief Step 4）

按 brief 给的代码**逐字**替换（含 XML 文档注释）。新语义（K1 / spec §4 C3）：

1. `_collapsed = collapsed;`
2. `RowOf(TimelineRowId) is { } timelineRow` → `timelineRow.Visible = !collapsed;`
3. `RowOf(BuildRowId) is { } buildRow` → `buildRow.Visible = !collapsed;`
4. `RowOf(ToolRowId) is { } toolRow` → `toolRow.Visible = true;`（**常驻**）
5. `SectionOf(ToolStripSectionId)?.Content is ToolStrip toolStrip` → `toolStrip.SetCollapsedMode(collapsed);`（消费 A3 的紧凑变体入口，**A4 的接线点**）
6. `_rows` 上下各留 10px 使 20px 的 `tool_row` 在 40px 折叠高度里纵向居中：`_rows.OffsetTop = collapsed ? 10f : 0f;` / `_rows.OffsetBottom = collapsed ? -10f : 0f;`
7. `_collapseButton` 文本 `▴`/`▾` 与 tooltip 切换（保留原有）
8. `CustomMinimumSize = new Vector2(CustomMinimumSize.X, collapsed ? CollapsedHeight : _expandedHeight);`
9. `MarkLayoutChanged();`（保留，brief 第 2 条裁定要求保留）

**删除的旧行为**：`_rows.Visible = !collapsed;`（整体收起 Rows）与 `_collapsedBar.Visible = collapsed;`。

`_collapseButton` 的 `Ctrl+B` / `M` 键处理（`_UnhandledInput`）**一字未改**（brief 第 4 条裁定）。

### 1.3 `CaptureLayout` 落盘守卫（brief Step 5）

`CaptureLayout(PanelState state)` 行循环内：

```csharp
// before
rowState.Visible = row.Visible;
// after（逐字）
// 折叠态下 timeline_row / build_row 被 SetCollapsed 临时隐藏，这不是用户意图，
// 绝不能落盘（同 2026-10-10 F11 全屏事故）。折叠时一律记 true。
rowState.Visible = _collapsed ? true : row.Visible;
```

作用：折叠态退出时 `.worldview/layouts/<project>.yaml` 里 `timeline_row` / `build_row` 的 `visible` 仍写 `true`，避免下次启动底部缺行（与 2026-10-10 F11 全屏事故同源）。

### 1.4 R15 裁定：消除残留字面量（brief 之外的唯一一处改动）

`EnsureDefaultRows()` 中工具行 section 原为硬编码字面量，改为消费 A2 声明的常量：

```csharp
// before
toolRow.AddSection(CreateSection("tool_strip", "工具栏", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolStrip"));
// after
toolRow.AddSection(CreateSection(ToolStripSectionId, "工具栏", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolStrip"));
```

至此 `private const string ToolStripSectionId = "tool_strip";`（A2 声明）同时被**生产代码**（本节）与**测试**（`折叠态必须切到ToolStrip紧凑变体`）读取，不再有「声明了没读者」的悬空常量。该改动 1 行，且在同一文件范围（brief Files 已列 `BottomPalette.cs`）；A3/A4 的测试都未断言该字面量，安全。

### 1.5 附带：类级 XML 文档同步（1 处，2 行）

类头原描述「折叠时整行（含 timeline_row）一起收起」已被本任务推翻，若不改会留下与实现矛盾的注释：

```csharp
// before
/// 折叠时整行（含 timeline_row）一起收起，由本容器统一驱动。
// after
/// 折叠态（spec §4 C3 = K1）不是整体收起，而是只收起 <c>timeline_row</c> / <c>build_row</c>，
/// 由常驻的 <c>tool_row</c> 显示 <see cref="ToolStrip"/> 紧凑变体（工具切换图标 + 坐标）。
```

注释修正，无行为变化。

---

## 2. 测试内容与结果

测试文件 `tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs` **逐字**取自 brief Step 1（6 个 `[Fact]`，源码文本护栏；`File.ReadAllText` 读 `scripts/Ui/Chrome/BottomPalette.cs` 后断言子串，不实例化 Godot 节点）：

| # | 测试 | 断言要点 |
| --- | --- | --- |
| 1 | `折叠态不得再用两行文本提示` | 源码不含 `_collapsedBar` / `面板已折叠` / `Ctrl+B 展开 · T 播放条 · M 小地图` |
| 2 | `折叠态必须常驻tool_row并收起另两行` | 含 `timelineRow.Visible = !collapsed;` / `buildRow.Visible = !collapsed;` / `toolRow.Visible = true;`，且不含 `_rows.Visible = !collapsed;` |
| 3 | `折叠态必须让Rows纵向居中20px常驻行` | 含 `_rows.OffsetTop = collapsed ? 10f : 0f;` / `_rows.OffsetBottom = collapsed ? -10f : 0f;` |
| 4 | `折叠态必须切到ToolStrip紧凑变体` | 含 `toolStrip.SetCollapsedMode(collapsed);` + `private const string ToolStripSectionId = "tool_strip";` |
| 5 | `折叠时不得把行的invisible落盘` | 含 `rowState.Visible = _collapsed ? true : row.Visible;` |
| 6 | `折叠高度必须仍是40` | 含 `public const int CollapsedHeight = 40;` + 折叠高度三元表达式 |

**结果**：全绿。聚焦跑 6/6 成功；全量跑 375 总计 / 7 失败 / 368 成功（失败数与 A3 后基线一致，未新增失败）。

---

## 3. TDD 证据

### 3.1 RED（先写测试 → 确认失败）

命令：

```powershell
dotnet build WorldView.sln -c Debug -m:1 --no-incremental
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe --filter-class WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests
```

相关失败输出（节选）：

```
失败 WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests.折叠时不得把行的invisible落盘 (11ms)
  Assert.Contains() Failure: Sub-string not found
  Not found: "rowState.Visible = _collapsed ? true : row.Visible"···
失败 WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests.折叠态必须常驻tool_row并收起另两行 (0ms)
  Assert.Contains() Failure: Sub-string not found
  Not found: "timelineRow.Visible = !collapsed;"
失败 WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests.折叠态必须切到ToolStrip紧凑变体 (0ms)
  Not found: "toolStrip.SetCollapsedMode(collapsed);"
失败 WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests.折叠态必须让Rows纵向居中20px常驻行 (0ms)
  Not found: "_rows.OffsetTop = collapsed ? 10f : 0f;"
失败 WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests.折叠态不得再用两行文本提示 (0ms)
  Assert.DoesNotContain() Failure: Sub-string found
  Found:  "_collapsedBar"

测试运行摘要: 失败!
  总计: 6
  失败: 5
  成功: 1
EXIT=2
```

**为何该失败是预期的**：5 个失败对应实现尚未落地——① `_collapsedBar` 字段与两行文本提示仍在（`折叠态不得再用两行文本提示` 因 `Assert.DoesNotContain` 命中 `_collapsedBar` 而失败）；② `SetCollapsed` 仍是旧实现（整体 `_rows.Visible = !collapsed`），既无 `timelineRow`/`buildRow`/`toolRow` 三分支，也无 `SetCollapsedMode` 接线与 offset 居中；③ `CaptureLayout` 仍写 `rowState.Visible = row.Visible;`，无折叠守卫。第 6 个测试（`折叠高度必须仍是40`）在实现前即通过，因为 `CollapsedHeight = 40` 与折叠高度三元表达式是 A2 之前就存在的既有代码、本任务不改——这正是 brief Step 2 只列出 3 个失败名、实际 5 失败的原因（brief 列的是「关键」失败，非穷举）。

### 3.2 GREEN（实现后 → 确认通过）

命令：

```powershell
dotnet build WorldView.sln -c Debug -m:1 --no-incremental
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe --filter-class WorldView.Tests.Ui.Chrome.BottomPaletteCollapsedTests
```

输出：

```
测试运行摘要: 已通过! - D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll (net8.0|x64)
  总计: 6
  失败: 0
  成功: 6
  已跳过: 0
  持续时间: 335ms
EXIT=0
```

全量回归（提交后复跑一次确认）：

```
测试运行摘要: 失败! - …\WorldView.Tests.dll (net8.0|x64)
  总计: 375
  失败: 7
  成功: 368
  已跳过: 0
EXIT=2
```

### 3.3 7 个失败均为既有失败（与本任务无关）

```
失败 WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells
失败 WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric
失败 WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells
失败 WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue
失败 WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only
失败 WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array
失败 WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute
```

全部落在 `Editing` / `Validation` / `Storage`，与底部 UI 折叠态无关；数量仍为 7，符合门禁。

---

## 4. 构建计数（实测）

| 时点 | 命令 | 结果 |
| --- | --- | --- |
| A4 前半（仅加测试，未改实现） | `dotnet build WorldView.sln -c Debug -m:1 --no-incremental` | **0 个错误 / 185 个警告** |
| A4 完成后（实现已落地） | 同上 | **0 个错误 / 185 个警告** |

`185` 与任务下达的基线一致（A3 后 = 0 error / 185 warnings）；本任务未新增 `scripts/**/*.cs`，警告数不变。两次构建均加了 `-m:1` 与 `--no-incremental` 两个必需标志。

---

## 5. 新增测试 `.uid`

**已生成，且已随测试一起提交进 `706841c`。**

- 路径：`tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs.uid`
- 内容（19 字节，LF，无 CR）：

```
uid://p6jyugkh1clo
```

- 生成命令（Godot 4.7.2 mono **console** 构建的编辑器模式；GUI `.exe` 不阻塞、`--check-only` 也不生成，均未使用）：

```powershell
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --editor --path D:\kudi-project\world-view --quit-after 600
```

- **退出码 = 0**。
- stderr 上有两条与 `.uid` 生成无关的环境性告警：`Unable to start the timer because it's not inside the scene tree`（引擎 hot-reload watcher 在无界面编辑器下启动定时器）与 `Cannot save file 'C:/Users/leo/AppData/Roaming/Godot/editor_settings-4.7.tres'`（沙箱不可写 `%APPDATA%`，即用户编辑器设置）；二者不影响 `.uid` 生成，且不落在仓库内。
- 未手写 `.uid`（AGENTS.md 要求，已遵守）。

---

## 6. 变更文件

| 文件 | 状态 | 说明 |
| --- | --- | --- |
| `scripts/Ui/Chrome/BottomPalette.cs` | 已改（+35 / −36） | 删 `_collapsedBar`；重写 `SetCollapsed`；`CaptureLayout` 加守卫；R15 常量一行；类头注释同步 |
| `tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs` | 新增（78 行） | brief Step 1 逐字 |
| `tests/WorldView.Tests/Ui/Chrome/BottomPaletteCollapsedTests.cs.uid` | 新增（1 行） | `uid://p6jyugkh1clo` |

提交哈希：`706841c`。分支 `deepseek-build`。commit 只含上述 3 个文件（`git status --short` 在提交后仅余未跟踪的 `.superpowers/sdd/…` 台账目录，见 §7 第 4 条）。

---

## 7. 自审发现

1. **`_collapsedBar` 残留 = 0**：对整个 `scripts/Ui/Chrome/BottomPalette.cs` 做 `grep` 三个模式 `_collapsedBar` / `面板已折叠` / `Ctrl+B 展开` 与 `_rows\.Visible` → **全部零命中**。两行文本提示的两个 `Label`、字段、`BuildChrome()` 整段（原 97–122 行）均已移除。
2. **守卫逐字在位**：`rowState.Visible = _collapsed ? true : row.Visible;` 与注释在 `CaptureLayout` 行循环内、`FindState(existing, row.RowId)` 之后，位置与 brief Step 5 一致。
3. **无多余改动**：`git diff --stat` = 1 文件 / +35 / −36；`_UnhandledInput` 的 `Ctrl+B` 与 `M` 处理零改动；`ExpandedHeight = 200` / `CollapsedHeight = 40` / `ToolsSectionId` 均未动；`DockLayout` / `PaletteRow` / `PaletteSection` / `PanelState` 结构零改动（符合计划 Global Constraints「只在既有结构内改默认值或追加节点」）。
4. **`ToolStripSectionId` 已落地为「有读者的常量」**：生产代码（`EnsureDefaultRows` + `SetCollapsed`）2 处 + 测试断言 1 处。
5. **行尾**：`scripts/Ui/Chrome/BottomPalette.cs`、测试 `.cs`、`.uid` 三者在盘上均为纯 LF（实测 CRLF 计数 = 0；`BottomPalette.cs` LF 总数 666）。git 提示 `LF will be replaced by CRLF` 源于本机 `core.autocrlf = true`（检出时转换），仓库内 `.gitattributes` 对本文件 `text`/`eol` 均为 unspecified；与既有文件行为一致，**入库内容仍是 LF**，未违反仓库 LF 约定。本次未手工改写任何行尾。
6. **测试输出纯净**：聚焦跑 6/6 成功、无跳过；全量跑失败名全部为 §3.3 列出的 7 个既有失败，无新增。
7. **构建两次均为 185/0**；未新增 `scripts/**/*.cs`，无 `.uid` 遗漏（唯一新增测试文件的 `.uid` 已入库）。

---

## 8. 问题与说明

1. **`--check-only` 在本环境返回退出码 −1（非本任务引入，已对照基线确认）**。计划 brief Step 7 与计划 Global Constraints 都要求 `--headless --check-only --quit-after 120` 期望退出码 0；实测该命令报：
   - `ERROR: Failed to open 'user://logs/godot2026-10-10T11.06.49.log'` / `Failed to open log file for writing: user://logs/godot.log`（沙箱下 `user://` 不可写）
   - `ERROR: Failed to read the root certificate store.`
   - 运行期 C# 异常 `System.InvalidCastException: Unable to cast object of type 'Godot.CompressedTexture2D' to type 'Godot.Image'.`

   **为排除是本任务引入的回归，我 `git stash -u` 回退到 HEAD（A4 之前）跑了同一条命令：完全相同的三组错误、相同退出码 −1。** 故这是既有环境/工程问题（`user://` 日志目录在沙箱不可写，外加一个既有的纹理类型转换缺陷），**不是 A4 造成的**。我未放宽任何门禁，也未把它当作通过。

   **替代验证（同样能覆盖 A4 真正需要证明的东西）**：`--headless --editor --quit-after 600` **退出码 0**，stderr 中 `BottomPalette` 相关零命中，无 `Parse Error` / `SCRIPT ERROR` / `Failed to load script`——即改动后的 `BottomPalette.cs` 能被引擎正常解析，且 `.uid` 正常生成。这比 `--check-only` 更能证明脚本可解析（`--check-only` 不生成 `.uid`，本次实测在该模式下确实没生成）。

2. **F5 人工冒烟未执行**（任务第 2 条明确禁止把它当作替代品；且本会话无可用窗口）。计划 Step 7 的 5 项冒烟清单（展开态 28+152+20 行高、工具方块区分组互斥、地形色块绘制、Ctrl+B 折叠后仅剩紧凑工具行、反复折叠后 `visible` 仍为 `true`）**留待控制器安排的人工目检**；其中第 5 项已由 `折叠时不得把行的invisible落盘` 在源码层加护栏，第 4 项的接线已由 `折叠态必须切到ToolStrip紧凑变体` 覆盖。
3. **未启动任何 subagent / reviewer**（任务第 4 条）。
4. **`.superpowers/sdd/…` 目录仍是未跟踪状态**：该目录的 `.gitignore` 是 `*` + 反向例外（`!task-*-report.md` / `!progress.md`），`git status` 因目录内含被忽略的 `task-A4-brief.md` 而只显示目录级 `??`。本报告与 A4 的行已就位，按「白名单入库」策略需要显式 `git add` 这两个文件；这属于台账流程，不在 brief Step 8 的 `git add` 清单内，故未混入代码提交。

---

## 9. 结论

brief Step 1–6 + 8 全部按序完成：测试先红后绿；`_collapsedBar` 与两行文本提示彻底移除；`SetCollapsed` 改为「只收起 `timeline_row` / `build_row`，`tool_row` 常驻 + `ToolStrip` 紧凑变体 + Rows 上下 10px 居中」；`CaptureLayout` 折叠落盘守卫逐字在位；R15 的 `tool_strip` 字面量已换成常量；构建 0 错误 / 185 警告（与基线一致）；新测试 6/6 全绿；全量 375 / 7 失败（数量不变）；测试 `.uid`（`uid://p6jyugkh1clo`）已生成并随测试同一提交入库。唯一偏离是 `--check-only` 退出码 −1，已用 HEAD 基线证明其为既有环境问题，并以 `--editor` 退出码 0 作为脚本可解析性的等价证据。
