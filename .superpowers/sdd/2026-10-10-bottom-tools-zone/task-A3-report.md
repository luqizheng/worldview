# Task A3 报告：地形色块迁入工具方块区 + `ToolStrip` 分组化

- 计划：`docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`（Task A3）
- 任务书：`.superpowers/sdd/2026-10-10-bottom-tools-zone/task-A3-brief.md`（181 行）
- 代码提交：`784a5b6` `refactor(ui): 地形色块迁入工具方块区，ToolStrip 分组化并加折叠紧凑变体（spec §4/§9-1）`（分支 `deepseek-build`）
- 结论：**DONE**。三项门禁全部达标——构建 `0 错误 / 185 警告`（= 基线 185）；单测 `369 总 / 7 失败 / 362 成功`（= 基线 7 失败）；新增 4 条 `ToolZoneMigrationTests` 全绿。

---

## 1. 实现内容

### 1.1 `scripts/Ui/Chrome/ToolStrip.cs` 删除的成员（逐项）

| # | 删除的成员 | 迁移前位置（任务书行号） | 备注 |
|---|---|---|---|
| 1 | `private readonly List<TerrainSwatch> _terrainSwatches = new();` | 字段区 | 地形色块列表 |
| 2 | `private static readonly TerrainType[] TerrainOrder = (TerrainType[])Enum.GetValues(typeof(TerrainType));`（含其上 `/// <summary>地形取值顺序…`） | `:45` | 枚举顺序表 |
| 3 | `BuildUi()` 内整段地形 UI：`AddSeparator();` + `_bar.AddChild(MakeCaption("地形"));` + `var terrainStrip = new HBoxContainer { Name = "TerrainStrip", … }; terrainStrip.AddThemeConstantOverride("separation", 2); _bar.AddChild(terrainStrip);` + `foreach (var terrain in TerrainOrder) { var swatch = new TerrainSwatch { Name = "Swatch" + terrain, TerrainType = terrain }; var captured = terrain; swatch.Pressed += () => OnSwatchPressed(captured); terrainStrip.AddChild(swatch); _terrainSwatches.Add(swatch); }` | `:127`（`new TerrainSwatch` 在其中） | 含该段首的分隔符 |
| 4 | `private void OnSwatchPressed(TerrainType terrain)` | `:570` | 点色块 → `SetPaintTerrain` + `SetTool(Brush)` + `SyncTerrain` |
| 5 | `private void SyncTerrain(TerrainType terrain)` | `:582` | 把选中态刷到 `_terrainSwatches` |
| 6 | `Bind(EditorContext context)` 内 `SyncTerrain(context.PaintTerrain);`（含其后空行） | `Bind` 尾部 | 绑定时的地形同步 |
| 7 | `private void AddSeparator()` | `:215` 附近 | 按任务书第 4 步拆成 `NewSeparator()`；删除后全文件无调用点（原 5 处调用全在 `BuildUi()`，已改 `NewSeparator()`） |
| 8 | 类注释里的「地形、」字样（改为「地形色块已迁入工具方块区（`ToolPaletteSection`），本类不再承载」） | 类 `<para>` 注释 | 避免注释与实现不一致 |

删除后 `TerrainType` / `TerrainSwatch` / `TerrainStrip` / `TerrainOrder` / `_terrainSwatches` / `OnSwatchPressed` / `SyncTerrain` / `AddSeparator` 在该文件**零残留**（grep 全仓确认，仅命中 `ToolPaletteSection.cs` 与新的测试文件字符串断言）。

`using` 处理：`using WorldView.Domain;` **保留**（该命名空间仍为必需——`UnitOf(MapDoc? map)` 与 `MapType.World` 都要它；任务书「若变为未使用才移除」，此处并未变为未使用）。`using System;` / `System.Collections.Generic;` 保留（`Action` / `Math` / `Dictionary` 仍用）。`using WorldView.Editing;` 与基线一致，未动（`UndoStack` 经 `EditorContext` 属性使用）。

### 1.2 新增 / 改写的成员（`scripts/Ui/Chrome/ToolStrip.cs`，当前行号）

- **新增字段**（`:35-37`，任务书第 4 步代码逐字）：
  `private HBoxContainer? _brushGroup;` / `private HBoxContainer? _historyGroup;` / `private HBoxContainer? _stateGroup;`
- **新增方法 `SetCollapsedMode`**（`:63-79`，任务书代码逐字，含中文 XML 注释「折叠态紧凑变体（spec §4 折叠态 C3 = K1）…」）：三分组各自 `Visible = !collapsed`，均带 `is not null` 空值保护。A3 只定义、**不调用**（A4 才会接线 `SectionOf("tool_strip")?.Content is ToolStrip → SetCollapsedMode(collapsed)`）；公开方法未被调用不产生任何编译器诊断，故警告门禁不受影响。
- **新增 `private static Label NewSeparator()`**（`:217-222`，任务书代码逐字）：造 `MakeCaption("│")` 并 `AddThemeColorOverride("font_color", new Color(0.30f, 0.30f, 0.36f))` 后返回。
- **改写 `BuildUi()` 后半段为三个分组容器**（`new HBoxContainer { Name = "BrushGroup" / "HistoryGroup" / "StateGroup" }`，各 `AddThemeConstantOverride("separation", 6)`）：
  - `_brushGroup`（`:101-143`）= `[NewSeparator(), MakeCaption("笔刷"), _radiusSpin, _radiusUnit, _radiusValue, _refineCheck]`
  - `_historyGroup`（`:145-159`）= `[NewSeparator(), _undoButton, _redoButton]`
  - `_stateGroup`（`:167-187`）= `[NewSeparator(), _statusLabel, _validationLabel]`
- 三个容器均设 `SizeFlagsVertical = SizeFlags.ShrinkCenter`（与原先单个控件的竖向取值一致，保证 row 仍是 20px 布局）；`_stateGroup` 另设 `SizeFlagsHorizontal = SizeFlags.ExpandFill`（见 §4 自检第 3 条）。
- **保留未动**：`_toolButtons` / `_radiusSpin` / `_radiusUnit` / `_radiusValue` / `_refineCheck` / `_undoButton` / `_redoButton` / `_coordLabel` / `_statusLabel` / `_validationLabel` / `_bar` / `_context` / `_bound` / `_syncing` / `_lastValidation` / `_lastCanUndo` / `_lastCanRedo` / `_lastActiveMapId`，以及 `AddToolButton` / `MakeCaption` / `MakeIconButton` / `ApplyCompactStyle` / `CompactStyle` 等辅助方法。

### 1.3 `_bar` 最终子节点顺序（与任务书一致）

`[V][B][P]` → `_brushGroup` → `_historyGroup` → 独立 `NewSeparator()`（`:161`）→ `_coordLabel` → `_stateGroup`

---

## 2. 测试与结果

### 2.1 新增测试文件

`tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs`（71 行 / 2974 字节，UTF-8 无 BOM，LF）

- **逐字一致性已机械化验证**：把任务书第 14–84 行取出来与文件做逐行 `-cne` 比较 → `expect_lines=71 test_lines=71`，结果 **「TEST FILE VERBATIM MATCH (brief lines 14-84)」**，无任何一行差异。
- 4 条 `[Fact]`：`ToolStrip不得再承载地形色块`、`工具方块区必须承载地形色块`、`ToolStrip必须保留工具切换三键与笔刷半径与坐标`、`ToolStrip必须提供折叠紧凑变体`。

### 2.2 TDD 证据

#### RED（实现前）

命令：

```
dotnet build WorldView.sln -c Debug -m:1 --no-incremental
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

构建：`185 个警告 / 0 个错误`（= 基线，说明测试文件本身不引入警告）。测试关键输出：

```
失败 WorldView.Tests.Ui.Chrome.ToolZoneMigrationTests.ToolStrip必须提供折叠紧凑变体 (5ms)
    在 WorldView.Tests.Ui.Chrome.ToolZoneMigrationTests.ToolStrip必须提供折叠紧凑变体() 位于 D:\kudi-project\world-view\tests\WorldView.Tests\Ui\Chrome\ToolZoneMigrationTests.cs:66
失败 WorldView.Tests.Ui.Chrome.ToolZoneMigrationTests.ToolStrip不得再承载地形色块 (1ms)
    在 WorldView.Tests.Ui.Chrome.ToolZoneMigrationTests.ToolStrip不得再承载地形色块() 位于 D:\kudi-project\world-view\tests\WorldView.Tests\Ui\Chrome\ToolZoneMigrationTests.cs:40
  总计: 369
  失败: 9
  成功: 360
RED_EXIT=2
```

**为什么这两个失败是预期的**：任务书 Step 2 明确「期望 `ToolStrip不得再承载地形色块` 与 `ToolStrip必须提供折叠紧凑变体` 失败」——前者因为迁移前 `ToolStrip.cs` 仍含 `TerrainStrip` / `TerrainSwatch` / `_terrainSwatches` / `TerrainOrder`；后者因为 `SetCollapsedMode` 与 `_brushGroup` 都还不存在。另两条事实在 RED 时已通过：`工具方块区必须承载地形色块` 由 A1 已落成的 `ToolPaletteSection` 满足，`ToolStrip必须保留工具切换三键…` 在删除地形段前后都成立（这正是「只删地形、不动三键」的护栏）。失败数 9 = 基线 7 + 这 2 条。

**RED 二次复现（提交后回归验证）**：用 `git checkout f4a7bd8 -- scripts/Ui/Chrome/ToolStrip.cs` 临时还原迁移前实现（保留新测试），`dotnet build WorldView.sln -c Debug -m:1` 后重跑测试，复现**完全相同**的两条失败与行号（`:66` / `:40`），`369 总 / 9 失败 / 360 成功`；随后 `git checkout HEAD -- scripts/Ui/Chrome/ToolStrip.cs` 复原并 `git status --short` 确认工作区干净。这一步证明这两条护栏确实会因缺少实现而失败（不是恒真断言）。

#### GREEN

命令（最终、在**已提交内容**上执行的全量重编译）：

```
dotnet build WorldView.sln -c Debug -m:1 --no-incremental
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe --filter-class WorldView.Tests.Ui.Chrome.ToolZoneMigrationTests
```

构建：`185 个警告 / 0 个错误`（三个项目均实际重编译：日志含 `WorldView.Generator -> …dll`、`WorldView -> …WorldView.dll`、`WorldView.Tests -> …WorldView.Tests.dll`）。

全量测试：

```
  总计: 369
  失败: 7
  成功: 362
  已跳过: 0
FINAL_EXIT=2
```

（`exit code 2` 是 7 条**预存在**失败的既定表现；输出中无任何 `ToolZoneMigration` 相关失败行。）

新增 4 条单测（隔离运行，证明它们真的被 xunit 发现并执行）：

```
测试运行摘要: 已通过! - D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll (net8.0|x64)
  总计: 4
  失败: 0
  成功: 4
  已跳过: 0
  持续时间: 443ms
FILTER_EXIT=0
```

`--list-tests` 亦列出全部 4 条：`ToolStrip不得再承载地形色块` / `工具方块区必须承载地形色块` / `ToolStrip必须保留工具切换三键与笔刷半径与坐标` / `ToolStrip必须提供折叠紧凑变体`。
（附注：`--filter` 的 VSTest 语法 `/*/*/ToolZoneMigrationTests/*` 在本 runner 上匹配到 **0 条**并以 exit 8 退出；`-class`、`--output detailed` 是无效选项（exit 5）。本 runner（xunit.v3 + MTP）应使用 `--filter-class` / `--filter-method` / `--filter-namespace`。）

### 2.3 构建警告/错误计数（精确值）

| 阶段 | 命令 | 结果 |
|---|---|---|
| 基线（改动前，本会话实测） | `dotnet build WorldView.sln -c Debug -m:1 --no-incremental` | **0 错误 / 185 警告** |
| RED（新测试 + 旧实现） | 同上 | **0 错误 / 185 警告** |
| GREEN（新测试 + 新实现，全量重编译） | 同上 | **0 错误 / 185 警告** |

即：**警告数与基线完全一致（185），无新增警告**；全量构建日志中也没有任何指向 `ToolStrip.cs` / `ToolZoneMigrationTests.cs` 的**新增**告警（详见 §4 第 6 条对 CS2002 的说明）。

### 2.4 新增测试 `.uid`

- 生成命令（用 `_console.exe`，非 GUI exe）：

```
& "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --editor --path D:\kudi-project\world-view --quit-after 600
```

  退出码 `0`；stderr 只有本仓固有的编辑器噪声（`GodotTools.HotReloadAssemblyWatcher.RestartTimer` 的 `Unable to start the timer because it's not inside the scene tree`、`Cannot save file 'C:/Users/leo/AppData/Roaming/Godot/editor_settings-4.7.tres'`），与任务无关。
- 生成结果：`tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs.uid`（20 字节，LF，无 BOM）
- 内容（1 行）：

```
uid://cgltnr53v4ora
```

- 运行 Godot 后 `git status --short` 只有本任务预期的三个路径，说明 Godot 没有顺带改动任何已跟踪文件（`project.godot` / `*.tscn` 均未被改）。
- 该 `.uid` 与测试文件**同一次提交**入库（`784a5b6`，符合 AGENTS.md §6.1「`.uid` = 提交跟踪」）。

### 2.5 文件变更

| 文件 | 变更 | 状态 |
|---|---|---|
| `scripts/Ui/Chrome/ToolStrip.cs` | 改（+67 / −61 行；589 → 595 行） | 已提交 `784a5b6` |
| `tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs` | 新增（71 行） | 已提交 `784a5b6` |
| `tests/WorldView.Tests/Ui/Chrome/ToolZoneMigrationTests.cs.uid` | 新增（1 行） | 已提交 `784a5b6` |
| `.superpowers/sdd/2026-10-10-bottom-tools-zone/task-A3-report.md` | 新增（本文件） | **未提交**（见 §5） |

提交统计：`3 files changed, 139 insertions(+), 61 deletions(-)`。未触碰 `ToolPaletteSection.cs` / `TerrainSwatch.cs` / `BottomPalette.cs` / `PanelState.cs` 及任务书 `Files:` 之外的任何文件。

---

## 3. 提交

```
784a5b6 refactor(ui): 地形色块迁入工具方块区，ToolStrip 分组化并加折叠紧凑变体（spec §4/§9-1）
f4a7bd8 fix(tests): 默认工具方块区测试补真行为断言并修空断言（评审 R17/R18）   ← 本任务起点
```

提交信息与任务书 Step 6 逐字一致（经 `git commit -F` 以 UTF-8 消息文件提交，避免 Windows 控制台代码页改写中文）；`git add` 在任务书列出的两个路径之外**加上** `.uid`（任务书 Step 6 未列，但总体要求「生成 + 提交测试 `.uid`，且与测试同一提交」优先）。

---

## 4. 自检发现

1. **无残留引用**：全仓 grep `TerrainStrip|TerrainOrder|_terrainSwatches|OnSwatchPressed|SyncTerrain|TerrainType|TerrainSwatch` —— `ToolStrip.cs` 中 0 命中；命中仅在 `ToolPaletteSection.cs`（A1 的新家）与新测试文件的断言字符串里。
2. **删除的成员无一仍在被引用**：`OnSwatchPressed` / `SyncTerrain` / `AddSeparator` 原为 `private`，删除后全仓无调用点；`_terrainSwatches` 唯一写入点在同一段被删代码内。
3. **对任务书的一处增补（需评审确认）**：`_stateGroup` 额外设了 `SizeFlagsHorizontal = SizeFlags.ExpandFill`（附带中文注释「状态标签原先靠 ExpandFill 顶到行右端；嵌套一层后要靠容器继续把它撑开」）。理由：`_statusLabel` 原来靠自身 `ExpandFill` 把「校验」列顶到行右端；包进容器后若容器不 expand，链条断掉，`_validationLabel` 会紧贴状态文字而非行尾。任务书只点名了 `SizeFlagsVertical`，属未覆盖情形，按「保持既有视觉行为」的意图补齐。
4. **类注释同步**：原注释列举本类承载物时含「地形」，已改为「地形色块已迁入工具方块区（`ToolPaletteSection`），本类不再承载」，避免与新实现矛盾。
5. **测试逐字性与纯净度**：新测试文件与任务书代码块逐行机械比对一致；`dotnet build` 未对新测试文件报任何警告（该文件不使用 `CancellationToken` 相关 API，因此不触发仓库里普遍的 `xUnit1051`）。
6. **CS2002 属预存在瑕疵（非本任务引入）**：全量构建日志含 276 行 `warning CS2002: 源文件“…ToolStrip.cs”指定了多次` 之类告警，根因是 `WorldView.csproj:25-26` 同时写了两个等价 glob（`scripts/**/*.cs` 与 `scripts\**\*.cs`），导致同一批文件被重复 Include。本提交未触碰 `WorldView.csproj`（`git show --stat 784a5b6` 仅 3 个文件），且警告总数与基线相等（185），故为既有配置瑕疵；顺带提示：真正要「降警告」可清理这两行 glob 之一，但那超出 A3 范围。
7. **一次 `177 警告` 假象**：把若干构建串在一条命令里连续执行时，其中一次 `dotnet build … -m:1 --no-incremental` 打印了 `177 个警告`（部分项目被判 up-to-date，摘要只统计了实际重编译的项目）。**单独重跑同一命令**即回到 `185 个警告 / 0 个错误`，且日志确认三个项目都输出了 `-> …dll`。判定门禁一律以上述全量重编译的 185 为准（这与任务书提示的「增量摘要会少算」现象一致）。

---

## 5. 问题 / 遗留 / 交给上层决策

1. **`SetCollapsedMode` 尚无行为级测试**：本任务书只要求字符串存在性断言，故 A3 的护栏无法发现「折叠后布局是否真正确」这类语义问题。A4 接线后建议补行为断言（例如构造/反射取 `_brushGroup`，断言 `SetCollapsedMode(true)` 后 `Visible == false` 且工具三键与 `_coordLabel` 仍可见）。
2. **未做 F5 人工目检**：三容器套一层后的 20px 行高、`_stateGroup` 的右端对齐、以及折叠/展开两种形态下的真实排布，只经代码审查与编译验证，未经实际运行目检。建议纳入本计划的目检清单（A4 或 P0-C1 类项）。
3. **本报告未提交**：`.superpowers/sdd/2026-10-10-bottom-tools-zone/` 整个目录目前仍是未跟踪状态（`task-A1-report.md` / `task-A2-report.md` 也未提交，只有 `progress.md` 等由总控维护）。按「任务书只要求代码 + `.uid` 同提交」的口径，A3 未额外提交本报告，请总控在台账提交时一并纳入（该目录 `.gitignore` 已白名单 `!task-*-report.md`，可正常入库）。
4. **本任务未做的事**（刻意）：没有改 `ToolPaletteSection.cs` / `TerrainSwatch.cs` / `BottomPalette.cs` / `PanelState.cs`；没有清理 `WorldView.csproj` 的重复 glob；没有为 `SetCollapsedMode` 增加调用点。
