# Task A2 报告：默认 section 配置 `build_palette` → `tools`

- 计划：`docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`（Task A2）
- 任务书：`.superpowers/sdd/2026-10-10-bottom-tools-zone/task-A2-brief.md`
- 提交：`e4b758f`（单一提交，含测试 `.uid`）
- 状态：DONE

---

## 1. 实现了什么

### 1.1 `scripts/Ui/Chrome/BottomPalette.cs`

常量区（`TimelineRowId` / `BuildRowId` / `ToolRowId` 下方）按任务书原文追加两个常量：

```csharp
    /// <summary>工具方块区 section 的 ID（spec §4 row1：小地图 + 工具方块区）。</summary>
    public const string ToolsSectionId = "tools";

    /// <summary>工具行 section 的 ID（折叠态由它常驻）。</summary>
    private const string ToolStripSectionId = "tool_strip";
```

`EnsureDefaultRows()`（原第 151 行，现 `scripts/Ui/Chrome/BottomPalette.cs:157`）替换为任务书给出的那一行：

```csharp
        buildRow.AddSection(CreateSection(ToolsSectionId, "工具", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolPaletteSection"));
```

替换前的旧行为：

```csharp
        buildRow.AddSection(CreateSection("build_palette", "建筑", PaletteSection.SectionSizeMode.Flex, 1, false, "BuildPaletteSection"));
```

其余行（`timeline_row` / `build_row` / `tool_row`、`minimap`、`tool_strip`）一行未动；`ApplySectionStates` 的「未知 section → `SectionOf` 返回 null → `continue`」早退护栏本来就存在（`scripts/Ui/Chrome/BottomPalette.cs:475-479`、`484`），本任务未修改。A1 的文件（`scripts/Ui/Chrome/ToolPaletteSection.cs`、`scenes/ui/ToolPaletteSection.tscn`）未触碰。

### 1.2 `scripts/Ui/Dock/Leaf/PanelState.cs`

- `LayoutState.Default(string projectRoot)` 的 `build_row` section 列表里，`Id = "build_palette"` → `Id = "tools"`（现 `scripts/Ui/Dock/Leaf/PanelState.cs:312`）。该项上原有的 `Order = 1` / `SizeMode = "flex"` / `Flex = 1` / `CellSize = 56` / `CellGap = 4` / `ActiveCategory = "residences"` / `PagesByCategory = {residences, castles, religion, military, nature, facilities}` **逐项保留不动**——它们仍是建筑面板的私有状态，只是改由 `ToolPaletteSection` 经 `tools` section 转发读写。
- 4 个字段的 XML 注释按任务书改写（`scripts/Ui/Dock/Leaf/PanelState.cs:74-84`），「仅 `build_palette`」→「建筑面板私有状态，内嵌在 `tools` 工具方块区，经 ToolPaletteSection 转发」，并保留原有的 `[待标定]` 40–96 与「`items_per_page` 不落盘」附注：

```csharp
    /// <summary>建筑格边长（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发；<c>[待标定]</c> 40–96）。</summary>
    public int? CellSize { get; set; }

    /// <summary>建筑格间距（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发）。</summary>
    public int? CellGap { get; set; }

    /// <summary>当前分类（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发）。</summary>
    public string? ActiveCategory { get; set; }

    /// <summary>每分类记住的页码（建筑面板私有状态，内嵌在 <c>tools</c> 工具方块区，经 ToolPaletteSection 转发；<c>items_per_page</c> 不落盘）。</summary>
    public Dictionary<string, int>? PagesByCategory { get; set; }
```

- **额外的一处改动（任务书 Step 4 未点名，但任务书的测试强制要求）**：类级 `Id` 的示例注释 `PanelState.cs:21` 原本是

  ```csharp
    /// <summary>面板稳定 ID（如 <c>build_palette</c>），跨会话复用。</summary>
  ```

  已改为

  ```csharp
    /// <summary>面板稳定 ID（如 <c>minimap</c>），跨会话复用。</summary>
  ```

  理由见 §2 的 `DoesNotContain` 语义分析。这是本任务在任务书文字之外唯一的改动。

- 未新增任何持久化字段（遵守 spec §8：`schema_version` 冻结在 1，内部分组选择不落盘）。`LayoutState.CurrentSchemaVersion` 仍为 `1`。

---

## 2. 「`build_palette"` 字样清零」的执行与发现

任务书 Step 4 只要求改 4 个字段注释；但它的测试用的是**转义后的带引号字面量**：

- `Assert.DoesNotContain("\"build_palette\"", BottomPaletteSource, ...)`

语义要点：该断言匹配的是 C# 源码里字面出现的 `"build_palette"`（含双引号），**不匹配** XML 注释里的 `<c>build_palette</c>`。因此：

| 文件 | 原文中带引号的 `"build_palette"` 出现位置 | 处理 |
|---|---|---|
| `scripts/Ui/Chrome/BottomPalette.cs` | 仅 1 处：`EnsureDefaultRows()` 的 `CreateSection("build_palette", ...)` | 已被 `ToolsSectionId` 替换；全文件复查后**再无** `"build_palette"` 字样（`CaptureLayout` 与所有注释均无命中） |
| `scripts/Ui/Dock/Leaf/PanelState.cs` | 2 处：`Id = "build_palette"`（`:312`）与类级 `Id` 示例注释（`:21`） | 前者按 Step 4 改为 `Id = "tools"`；**后者任务书未点名，但保留会导致 `DoesNotContain` 失败**，故一并去除 |

任务书**未预期到的**只有 `PanelState.cs:21` 这一处（类级 `Id` 的示例注释），已按「让护栏语义成立」的最小方式处理（换成仍然真实存在的 `minimap`）。

**本任务范围外、现已确认仍然存在的 `build_palette` 字样**（均不参与这两个断言，故未改动）：

- `scripts/Ui/Dock/Leaf/PanelDragData.cs:15` —— 同一句类级 `Id` 示例注释的副本。
- `docs/主界面布局规格_v0.1.md:517` —— spec §8 中「`ActiveCategory` 是 `build_palette` 专用」一句。该说法在本任务之后**语义已过时**（字段现在归属 `tools` section），但任务书未要求改文档，故留给文档任务处理（见 §7 关注点）。
- `docs/superpowers/specs/2026-10-08-map-size-and-terrain-brush-design.md:174`、`docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`（计划原文）—— 历史文档。

`examples/sample_world/.worldview/layouts/sample_world.yaml` 确认仍是含 `build_palette` 节的旧布局（未入库、运行时生成，1531 字节）；按计划 §Task A2 说明本轮不迁移它，由 `ApplySectionStates` 的 `SectionOf(id) is null → continue` 跳过。

---

## 3. 测试

### 3.1 新增测试文件

`tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs` —— 与任务书 Step 1 的代码逐字一致（6 个 `[Fact]`：`默认布局必须用工具方块区替换建造网格`、`默认布局的_build_row_必须含_tools_节且不再含_build_palette`、`小地图必须仍是_fixed_auto_方形`、`三行结构必须不变`、`高度与schema版本必须不变`、`未知section必须被跳过而不是崩`）。

### 3.2 TDD 证据

**RED** —— 命令：

```
dotnet build WorldView.sln -c Debug -m:1
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
```

编译通过（0 错误）；测试摘要与相关失败：

```
失败 WorldView.Tests.Ui.Chrome.BottomToolsZoneDefaultsTests.默认布局必须用工具方块区替换建造网格 (9ms)
    在 WorldView.Tests.Ui.Chrome.BottomToolsZoneDefaultsTests.默认布局必须用工具方块区替换建造网格() 位于 D:\kudi-project\world-view\tests\W
orldView.Tests\Ui\Chrome\BottomToolsZoneDefaultsTests.cs:49
失败 WorldView.Tests.Ui.Chrome.BottomToolsZoneDefaultsTests.默认布局的_build_row_必须含_tools_节且不再含_build_palette (1ms)
    在 WorldView.Tests.Ui.Chrome.BottomToolsZoneDefaultsTests.默认布局的_build_row_必须含_tools_节且不再含_build_palette() 位于 D:\kudi
-project\world-view\tests\WorldView.Tests\Ui\Chrome\BottomToolsZoneDefaultsTests.cs:57
测试运行摘要: 失败! - ...\WorldView.Tests.dll (net8.0|x64)
  总计: 364
  失败: 9
  成功: 355
  已跳过: 0
EXIT=2
```

失败原因与预期完全一致：两个断言分别卡在改代码前必然不满足的第 49 行（`ToolsSectionId` 常量尚不存在 / `CreateSection(ToolsSectionId, "工具", ..., "ToolPaletteSection")` 尚未出现）与第 57 行（`PanelState.cs` 里仍有 `Id = "build_palette"` 且没有 `Id = "tools"`）。其余 4 个 Fact 在 RED 阶段即通过（它们守护的是不该变的三行结构、高度、schema 版本与未知 section 护栏）。`9 = 7 既有 + 2 新`。

**GREEN** —— 命令同上（重新 `dotnet build` 后）：

```
测试运行摘要: 失败! - ...\WorldView.Tests.dll (net8.0|x64)
  总计: 364
  失败: 7
  成功: 357
  已跳过: 0
EXIT=2
BottomToolsZone failure lines: 0
```

`BottomToolsZone` 相关失败行数为 0（新 6 个测试全绿），失败数 7 = 既有稳定失败，成功由 355 → 357（+2，即 RED 阶段的 2 个失败转绿）。

**基线对照（额外验证，为核对「失败数仍为 7」的口径）**：临时移出新增测试文件并重建后：

```
  总计: 358
  失败: 7
  成功: 351
  已跳过: 0
EXIT=2
```

即：**不含本任务测试的基线恰为 358 总 / 7 失败**（与本任务书给出的「当前总数为 358」逐字吻合），本任务新增 6 个测试 → 364 总；失败数 7 → 7 不变。随后已把测试文件移回原位、重新构建并复跑确认为 364/7/357。

7 个既有失败（本任务前后完全相同，非本任务引入）：

```
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue
WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only
WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array
WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute
```

---

## 4. 构建计数（实测）

`dotnet build WorldView.sln -c Debug -m:1 --no-incremental`（全量口径，因为增量构建的尾部摘要按项目分别计数、单看末行会误导）：

```
    185 个警告
    0 个错误
```

- 与任务书基线的 **185 警告 / 0 错误逐字一致**，本任务**未新增任何警告**。
- 本任务没有新增 `scripts/**/*.cs` 文件（唯一新增的 `.cs` 在 `tests/` 下），因此 `scripts/` 侧的警告构成不变。
- 另作对照：在不含新测试文件的基线上，`--no-incremental` 全量构建同样是 185 警告 / 0 错误。
- 观察到的唯一计数陷阱（记录以免后人误判）：**增量构建**的尾部摘要会只统计本轮实际重编译的项目（例如 177 或 19），并非全量真值；判定门禁必须用 `--no-incremental`。

---

## 5. 测试 `.uid`

- 已生成：`tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs.uid`
- 生成方式（任务书指定，**console** 构建）：

  ```
  & "C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --editor --path D:\kudi-project\world-view --quit-after 600
  ```

  进程退出码 0，`.uid` 于该次运行中生成，**未手写**。stderr 只有两类无害报错：(a) `HotReloadAssemblyWatcher` 的 `Unable to start the timer because it's not inside the scene tree`（headless editor 下的既有噪声）；(b) 沙箱下无法写 `C:/Users/leo/AppData/Roaming/Godot/editor_settings-4.7.tres`（工作区外的编辑器偏好，与本任务无关，也不影响 `.uid` 生成）。
- 内容（单行 + 结尾 LF，共 20 字节）：

  ```
  uid://cinjjohy7lnrt
  ```

- 已与测试同批提交（任务书 Step 6 的 `git add` 列表未含 `.uid`，按控制器 R10 裁定补入同一提交，因此**没有**额外的 `chore(tests):` 提交）。
- 生成后 `git status` 无任何其他新增/改动文件（没有意外 `.import` 或编辑器产物）。

---

## 6. 变更文件

单一提交 `e4b758f`，4 个文件、+106 / -7：

| 文件 | 变更 |
|---|---|
| `scripts/Ui/Chrome/BottomPalette.cs` | +6/-1：新增 `ToolsSectionId`（public const）与 `ToolStripSectionId`（private const）；`build_palette` section 创建行改为 `ToolsSectionId` + `"工具"` + `"ToolPaletteSection"` |
| `scripts/Ui/Dock/Leaf/PanelState.cs` | +6/-6：`Default()` 中 `Id = "build_palette"` → `Id = "tools"`；4 个字段 XML 注释改写；类级 `Id` 示例注释去引号字样 |
| `tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs` | 新增 92 行（任务书逐字） |
| `tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs.uid` | 新增 1 行：`uid://cinjjohy7lnrt` |

行尾：4 个文件均为 **LF**（0 个 CRLF），符合仓库约定。提交后 `git status` 干净（仅剩 `.superpowers/sdd/2026-10-10-bottom-tools-zone/` 未跟踪，这是白名单台账目录，按 AGENTS.md 规则由后续台账提交处理，不属于本任务）。

`git diff --cached --stat` 实测：

```
 scripts/Ui/Chrome/BottomPalette.cs                 |  8 +-
 scripts/Ui/Dock/Leaf/PanelState.cs                 | 12 +--
 .../Ui/Chrome/BottomToolsZoneDefaultsTests.cs      | 92 ++++++++++++++++++++++
 .../Ui/Chrome/BottomToolsZoneDefaultsTests.cs.uid  |  1 +
 4 files changed, 106 insertions(+), 7 deletions(-)
```

---

## 7. 自审与关注点

**自审结论：对照任务书完整、无无关改动、无过度构建。**

- 完整性：Step 1–6 全部按顺序执行；两处代码替换与测试代码均逐字取自任务书；`ToolsSectionId` / `ToolStripSectionId` 两个常量都按任务书原文声明（尽管后者本任务未使用——`private const` 未使用不产生编译诊断，实测 185 警告未变，门禁不受影响）。
- YAGNI：未新增持久化字段、未改 schema 版本、未顺手迁移 `examples/sample_world` 的旧布局文件、未改 A1 的文件、未改任务书未要求的 `PanelDragData.cs` 注释或任何文档。唯一的任务书外改动是 `PanelState.cs:21` 注释里的一处引号字样，属任务书测试强制要求（见 §2）。
- 无副作用：(1) 我之前的 `--no-incremental` 全量构建曾写出 `obj/` `bin/` 产物，属 gitignore 范围；(2) 曾临时移出新增测试文件核对基线，现已移回并通过 `git status` 确认工作区与提交内容完全一致。

**关注点 / 需要控制器或后续任务决策**：

1. **`ToolStripSectionId` 未使用（按设计）**：当前 `scripts/Ui/Chrome/BottomPalette.cs:158` 创建工具行 section 时仍硬编码字面量 `CreateSection("tool_strip", "工具栏", ...)`，而新常量 `ToolStripSectionId = "tool_strip"` 与之重复。任务书明确规定本任务只声明常量、由其消费方（Task A4）使用，故我**故意没有**把第 158 行改成 `ToolStripSectionId`（那会超出任务书给定的替换范围）。建议 Task A4 顺手收口这处重复字面量。
2. **文档语义过时（本任务范围外）**：`docs/主界面布局规格_v0.1.md:517` 仍写「`ActiveCategory` 是 `build_palette` 专用」。经本任务后该字段归属 `tools` section，句中的 `build_palette` 已不存在。任务书未授权改文档，我未改；建议由文档/收口任务与 `scripts/Ui/Dock/Leaf/PanelDragData.cs:15` 的同类示例注释一并处理。
3. **任务书 Step 4 的措辞与它的测试不完全自洽（已按测试语义执行，并向控制器报备）**：Step 4 只说改那 4 个字段注释，但同任务书的 `Assert.DoesNotContain("\"build_palette\"")` 要求 `PanelState.cs` 全文不再出现带引号的该字样；`:21` 的类级注释因此必须一并处理。任务书原文「Report if you find any occurrence the brief did not anticipate」所指即此一处。
4. **`--no-incremental` 计数口径**：增量构建尾部摘要（如 177 / 19）会低估真实警告数，门禁判定请一律用全量构建（185 警告 / 0 错误）。这一点在我执行过程中出现过一次误读，已在 §4 记录。
5. **基线数字勘误**：任务书写「Current total is 358（A1 之后）」；实测不含本任务测试的基线为 **358 总 / 7 失败 / 351 成功**，与任务书吻合。任务书另一处提到的「351 before A1」与实测的 A1 前口径存在出入（A1 的测试文件另有 7 个 `[Fact]`），但**本任务的门禁（新增测试全绿 + 失败数仍为 7）已按实测独立证实**，不受该出入影响。

---

## 8. 评审修复轮（控制器裁定 R17 / R18）

**背景**：Task A2 评审结论 Spec ? / Task quality Approved，但带 2 条 Important（均标 plan-mandated）。计划作者（控制器）裁定**不接受**这两条的原样修法，另行给出 R17 / R18。本轮按 R17 / R18 实施。

### 8.1 R17 —— 把空断言改成位置耦合断言

**原问题**：`tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs` 的 `未知section必须被跳过而不是崩` 第三条断言

```csharp
Assert.Contains("continue;", BottomPaletteSource, StringComparison.Ordinal);
```

是**空断言**：`continue;` 在 `scripts/Ui/Chrome/BottomPalette.cs` 中共出现 6 次（`:237`、`:356`、`:438`、`:484`、`:530`、`:589`），即使把「未知 section 跳过」守卫整段删掉，该断言照样通过 —— 它对该守卫没有任何位置耦合。而它声称守护的恰是本任务引入的唯一真实运行时风险（`examples/sample_world/.worldview/layouts/sample_world.yaml` 里真的还留着 `build_palette` 节）。

**修法**（已按要求实施）：删掉那条游离断言，改为定位 `"if (section is null)"` 的下标，再断言其后 80 个字符内出现 `"continue;"`。已确认 `scripts/Ui/Chrome/BottomPalette.cs` 中 `if (section is null)` 只出现 1 次（`:482`），守卫本身位于 `:481-485`：

```csharp
    [Fact]
    public void 未知section必须被跳过而不是崩()
    {
        // 只保留源码护栏：ApplySectionStates 要构造 Godot 节点，测试进程没有 Godot 运行时，
        // 无法用行为断言覆盖这条守卫。
        Assert.Contains("var section = SectionOf(sectionState.Id);", BottomPaletteSource, StringComparison.Ordinal);

        // 「守卫必须真的执行 continue」不能写成 DoesContain("continue;")：本文件里 continue; 有 6 处
        // （:237 :356 :438 :484 :530 :589），删掉守卫那条断言照样通过（空断言）。
        // 因此锚成位置耦合断言：守卫被删时 IndexOf 返回 -1，断言必然失败。
        var guard = BottomPaletteSource.IndexOf("if (section is null)", StringComparison.Ordinal);
        Assert.True(guard >= 0, "ApplySectionStates 里的「未知 section」守卫 `if (section is null)` 不见了");

        var afterGuard = BottomPaletteSource.Substring(guard, Math.Min(80, BottomPaletteSource.Length - guard));
        Assert.Contains("continue;", afterGuard, StringComparison.Ordinal);
    }
```

**为什么不用真行为测试**（与裁定一致）：`ApplySectionStates` 是 `BottomPalette` 的实例私有方法，要构造 `PaletteRow` / prompt Godot 节点，测试进程没有 Godot 运行时，无法实例化 —— 这半只能靠源码护栏。该理由已写进测试文件的类级 XML 注释。

### 8.2 R18 —— 新增一组真行为断言（`LayoutState.Default`）

**原问题**：整个测试文件（`:56-58`、`:64-65`、`:71-74`、`:80-82`）验证的是**源码文本而非行为**，`File.ReadAllText` + 子串匹配抓不到「id 字符串对了但 section 从未被构造」或「运行时 `SectionOf(ToolsSectionId)` 返回 null」这类接线回归。

**关键事实（本轮已实测证实）**：`LayoutState.Default(string projectRoot)` 是**纯 .NET** —— 它只经 `ProjectIdOf` → `Path.GetFileName`（`scripts/Ui/Dock/Leaf/PanelState.cs:159-169`、`:248-372`），`Load` / `LayoutPath` 路径上的 `GD.PushWarning`（`:200`、`:218`、`:241`）与 `ProjectSettings.GlobalizePath`（`:177`）都不在 `Default` 的调用路径上。因此可以在测试进程里直接调用，**未抛任何异常**（`GodotSharp.dll` 已在 `tests/WorldView.Tests/bin/Debug/net8.0/` 内随 `ProjectReference` 传递到位）。

**新增 Fact**（`默认布局文档的tools节必须接线正确`）直接读对象树，断言 `LayoutState.Default("D:/tmp/sample_world")`：

- `SchemaVersion == LayoutState.CurrentSchemaVersion`、`ProjectId == "sample_world"`；
- `Root` 的 `bottom_palette` → `build_row` → `Sections` 中存在 `Id == "tools"`，且 `SizeMode == "flex"`、`Flex == 1`、`Order == 1`；
- 该节仍带建筑面板私有状态：`CellSize == 56`、`CellGap == 4`、`ActiveCategory == "residences"`、`PagesByCategory` 六键齐全（`castles / facilities / military / nature / religion / residences`，值全为 0）；
- **不存在** `Id == "build_palette"` 的 section；
- `minimap` 仍是 `SizeMode == "fixed"` 且 `AutoWidth == true`（用 `PanelState.AutoWidth` 读属性，未比 `Width` 的装箱类型）。

`projectRoot` 取不存在的假路径 `"D:/tmp/sample_world"`（`Default` 不读盘），避开测试机上的真实路径依赖。

`BottomPalette.EnsureDefaultRows()` 那半**按裁定保留源码护栏**（它构造 Godot 节点，测试进程无法实例化），理由已写入类级 XML 注释。

### 8.3 控制器已核实、本轮无需动作的项

`scripts/Ui/Dock/Leaf/PanelState.cs:312-327` 的 `tools` 项：六类 `PagesByCategory` 键齐全（`:321-326`），`SizeMode="flex"` / `Flex=1` / `CellSize=56` / `CellGap=4` / `ActiveCategory="residences"` 全在。R18 的断言把这组事实从「源码文本」升级成了「真行为」。

### 8.4 明确不做的事（遵循裁定）

1. `PanelState.cs:21` 注释那条 Minor：**保留**我上一轮的编辑（控制器认可）；与 `scripts/Ui/Dock/Leaf/PanelDragData.cs:15` 的措辞分叉记入台账。`PanelDragData.cs` 超出 A2 的 `Files:` 范围，**未改**。
2. `ToolStripSectionId` 未使用 + `scripts/Ui/Chrome/BottomPalette.cs:158` 硬编码 `"tool_strip"`：已归 Task A4 用常量替换，本轮**未动**。

### 8.5 门禁复跑命令与输出（评审修复轮）

命令与输出（本节为 append，遵循「命令与输出 append 到报告末尾」的裁定）：

```
dotnet build WorldView.sln -c Debug -m:1 --no-incremental
    185 个警告
    0 个错误
```

（与任务基线逐字一致：185 警告 / 0 错误，本修复轮未新增任何警告。）

```
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
测试运行摘要: 失败! - D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll (net8.0|x64)
  总计: 365
  失败: 7
  成功: 358
  已跳过: 0
EXIT=2
```

数字变化说明：上一轮 GREEN 为 364 总 / 7 失败 / 357 成功；本轮**新增 1 个 Fact**（`默认布局文档的tools节必须接线正确`）→ **365 总**；失败仍为 **7**（既有稳定失败，逐条名单与 §3.2 完全一致，本轮无任何变化）；成功 357 → **358**。`BottomToolsZone` 相关失败行数为 **0**（新测试全绿）。

本轮全量失败名单（与上一轮 7 条逐条相同，无增无减）：

```
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric
WorldView.Tests.Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells
WorldView.Tests.Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue
WorldView.Tests.Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only
WorldView.Tests.Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array
WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute
```

### 8.6 `LayoutState.Default()` 在测试进程是否可用

**可用，未抛异常。** 上表 365/7/358 即在该 Fact 真实执行 `LayoutState.Default("D:/tmp/sample_world")` 的条件下取得，且该 Fact 通过。因此**不需要**降级为「只保留源码护栏」，也没有用任何 `try/catch` 吞掉失败。

### 8.7 本轮变更文件

仅 1 个文件：`tests/WorldView.Tests/Ui/Chrome/BottomToolsZoneDefaultsTests.cs`（+1 个 Fact、R17 改写、类级注释补充、新增 `using System.Linq;` 与 `using WorldView.Ui.Dock.Leaf;`）。无新增 `.cs` 文件 → **不需要**新的 `.uid`（原 `.uid` `uid://cinjjohy7lnrt` 保持有效，未被改动）。提交：单独一个 `fix(tests): …` 提交（见 §8.8）。

### 8.8 修复轮提交

```
fix(tests): 默认工具方块区测试补真行为断言并修空断言（评审 R17/R18）
```

单一提交，仅含上述测试文件；**未**创建一个新的 `chore(tests):` 提交（无新 `.uid`）。