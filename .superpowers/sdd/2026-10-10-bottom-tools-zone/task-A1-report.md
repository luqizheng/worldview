# Task A1 报告：工具方块区 section 容器（ToolPaletteSection）

- 状态：**DONE_WITH_CONCERNS**（实现/测试/提交全部按 brief 完成；3 项环境与约定类问题见 §9）
- 提交：`fc706dc` `feat(ui): 新增工具方块区 section 容器（主界面布局规格 §4 row1）`（分支 `deepseek-build`，4 files / +321）
- 变更范围：纯新增，未触碰 `scripts/Ui/Chrome/BottomPalette.cs`、`scripts/Ui/Dock/Leaf/PanelState.cs` 及任何 `Files:` 之外的文件

---

## 1. 实现内容

严格照抄 brief 给出的代码（逐行 diff 见 §8），无任何增删：

| 文件 | 行数 | 内容 |
| --- | --- | --- |
| `scripts/Ui/Chrome/ToolPaletteSection.cs` | 229 | `public partial class ToolPaletteSection : PaletteContent`，namespace `WorldView.Ui.Chrome` |
| `scenes/ui/ToolPaletteSection.tscn` | 7 | `Control` 根节点 + `script` ext_resource → `res://scripts/Ui/Chrome/ToolPaletteSection.cs` |
| `scripts/Ui/Chrome/ToolPaletteSection.cs.uid` | 1 | `uid://ygxcwxlitbis`（由 Godot 生成，非手写） |
| `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs` | 84 | 7 个 `[Fact]` 源码护栏断言（同 `tests/WorldView.Tests/Ui/Layout/ViewMenuTests.cs` 风格） |

`ToolPaletteSection` 行为要点（与 brief 一致）：

- 分组常量 `public const string TerrainGroupId = "terrain";` / `public const string BuildingGroupId = "building";`，`SelectGroup(string)` 互斥切换 `_terrainGrid.Visible = isTerrain;` 与 `_buildPalette.Visible = !isTerrain;`，`ActiveGroup` 记录当前组。
- 地形组：`AdaptiveGrid`（`CellSize = 44` / `CellGap = 4`，均在 `AddChild` 之前赋值）按 `Enum.GetValues(typeof(TerrainType))` 全量生成 `TerrainSwatch`（`Name = "Swatch" + captured`、`TerrainType = captured`）。
- 点击色块：`context.SetPaintTerrain(terrain); context.SetTool(ToolState.Brush);`。
- `_Process(double delta)` 轮询 `context.PaintTerrain` 变化并 `SyncTerrain` 刷新选中态（`delta` 未用是 Godot override 的正常形态，按要求保留签名）。
- 建筑组：`res://scenes/ui/BuildPaletteSection.tscn` 惰性实例化（`ResourceLoader.Exists` + `InstantiateOrNull<BuildPaletteSection>()`），`ApplyState`/`CaptureState` 转发到 `_buildPalette`。
- 未使用 `Section` / `Row` / `GetParent()`，因此在 `PaletteSection` 之外嵌入安全（A2 接管默认配置的前置条件）。

## 2. 测试与结果

- 全量套件（命令见 §4）：`总计: 358 / 失败: 7 / 成功: 351 / 已跳过: 0`，退出码 2（既有约定）。
  - 基线（改动前）：`总计: 351 / 失败: 7 / 成功: 344`。差值 = 新增 7 个测试全部通过，失败数不变（仍为 7），即"破坏面为零"。
- 仅新测试类：`--filter-class "WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests"` → `总计: 7 / 失败: 0 / 成功: 7`，退出码 **0**。
- 7 个失败全部是改动前就存在的既有失败（未修复，按指令）：
  `BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells`、`BrushShapesTests.Round_Radius_2_Covers_Expected_Cells`、`UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric`、`UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue`、`ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only`、`YamlSchemasTest.Deserialize_GridCoord_As_Array`、`ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute`。

## 3. 构建计数（精确观测值）

| 命令 | 退出码 | 警告 | 错误 |
| --- | --- | --- | --- |
| `dotnet build WorldView.sln -c Debug`（brief 原文口径） | **1** | 0 | 0（"生成失败。"无任何诊断文本） |
| `dotnet build WorldView.sln -c Debug -m:1` | 0 | 185 | 0 |
| `dotnet build WorldView.sln -c Debug -m:1 --no-incremental`（变更前，A1 文件全部移出） | 0 | **184** | 0 |
| `dotnet build WorldView.sln -c Debug -m:1 --no-incremental`（变更后） | 0 | **185** | 0 |

- 报告口径的严格比较是"警告文案集合差集"：unique 184 → 185，唯一新增一条
  `CSC : warning CS2002: 源文件“...\scripts\Ui\Chrome\ToolPaletteSection.cs”指定了多次`。
  该告警是 `WorldView.csproj` 同时写 `scripts/**/*.cs` 与 `scripts\**\*.cs` 两个 glob 的既有结构产物——`scripts/` 下 138 个 `.cs` 每个都有一条，不是新代码缺陷。
- 未引入任何新的告警**类别**；新代码本身零告警；错误恒为 0。
- 过程中另观测到一次 177 警告读数（增量口径下的重复行合并效应），因此以差集结果为准，不以总数波动为准。

## 4. TDD 证据

按 brief 顺序执行：先写测试 → 确认 RED → 建 scene → 建 script → GREEN → Godot 校验 → 提交。

### RED

先只落 `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs`（verbatim），`scripts/Ui/Chrome/ToolPaletteSection.cs` 与 `scenes/ui/ToolPaletteSection.tscn` 尚不存在。

```
dotnet build WorldView.sln -c Debug -m:1      # exit 0
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe --filter-class "WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests"
```

```
总计: 7
失败: 7
成功: 0
```

失败原文（节选，7 条同型）：

```
失败 WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests.必须定义地形与建筑两个分组ID (16ms)
  Xunit.MicrosoftTestingPlatform.XunitException: System.IO.FileNotFoundException : Could not find file 'D:\kudi-project\world-view\scripts\Ui\Chrome\ToolPaletteSection.cs'.
    在 System.IO.File.ReadAllText(String path, Encoding encoding)
    在 WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests.get_ScriptSource() 位于 D:\kudi-project\world-view\tests\WorldView.Tests\Ui\Chrome\ToolPaletteSectionTests.cs:30
    在 WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests.必须定义地形与建筑两个分组ID() 位于 D:\kudi-project\world-view\tests\WorldView.Tests\Ui\Chrome\ToolPaletteSectionTests.cs:46
```

首轮 RED（仅测试文件、连 `.uid` 都尚未生成时）还同时出现 `get_SceneSource()`（测试文件 :33）抛出的 `DirectoryNotFoundException`（`scenes/ui/` 下不存在该 `.tscn`）。上表输出为提交后复现：临时移除已提交的两个产物（`Remove-Item` + `git checkout --` 还原）再跑，得到同一组 `FileNotFoundException`。

**为什么这个失败是预期的**：这 7 个测试是"源码文本护栏"，通过 `File.ReadAllText` 断言目标产物的具体代码行。目标文件不存在时必然抛 `FileNotFoundException`，说明测试确实在断言尚未存在的交付物（而非恒真断言）；同时其余 344 个既有测试保持通过，证明测试本身可正常编译执行。

### GREEN

创建 `scenes/ui/ToolPaletteSection.tscn` 与 `scripts/Ui/Chrome/ToolPaletteSection.cs`（verbatim）后：

```
dotnet build WorldView.sln -c Debug -m:1                              # exit 0，185 警告 / 0 错误
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe --filter-class "WorldView.Tests.Ui.Chrome.ToolPaletteSectionTests"
```

```
测试运行摘要: 已通过! - D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll (net8.0|x64)
  总计: 7
  失败: 0
  成功: 7
  已跳过: 0
  持续时间: 407ms
```

全量：

```
测试运行摘要: 已失败! - D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.dll (net8.0|x64)
  总计: 358
  失败: 7
  成功: 351
  已跳过: 0
  持续时间: 1s 502ms
```

## 5. Godot 无头校验与 `.uid`

| 命令 | 结果 |
| --- | --- |
| brief 原文：`Godot_v4.7.2-stable_mono_win64.exe --headless --path ... --check-only --quit-after 120` | 该 `.exe` 是 GUI 子系统程序，PowerShell 不等待、`$LASTEXITCODE` 为空、stdout 为空；**未**生成 `.uid` |
| 同目录 console 版：`..._console.exe --headless --path ... --check-only --quit-after 120` | 退出码 **0**；仍未生成 `.uid`（`--check-only` 只做解析、不触发编辑器资源扫描） |
| `..._console.exe --headless --editor --path D:\kudi-project\world-view --quit-after 600` | 退出码 **0**；生成 `scripts/Ui/Chrome/ToolPaletteSection.cs.uid` = `uid://ygxcwxlitbis`（由 Godot 写入，未手工伪造） |

- 该次编辑器扫描额外为测试文件生成了 `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid`（见 §9 问题 1）。
- 扫描未改动任何已跟踪文件：`git status --porcelain` 仅列出 4 个新增路径（`.godot/` 等构建产物按 `.gitignore` 忽略）。
- 无头运行中的既存报错（均来自 A1 未触碰的既有代码/沙箱限制，与本次改动无关）：`scripts/Render/MarkerLayer.cs:83` 与 `scripts/Render/RouteLayer.cs:61` 的 `InvalidCastException: Unable to cast object of type 'Godot.CompressedTexture2D' to type 'Godot.Image'`；`user://logs/godot.log` 与 `editor_settings-4.7.tres` 写入失败（沙箱不可写 `%APPDATA%\Godot`）。

## 6. 变更文件（提交 `fc706dc`）

```
scenes/ui/ToolPaletteSection.tscn                  |   7 +
scripts/Ui/Chrome/ToolPaletteSection.cs            | 229 +++++++++++++++++++++
scripts/Ui/Chrome/ToolPaletteSection.cs.uid        |   1 +
tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs |  84 ++++++++
4 files changed, 321 insertions(+)
```

`git add` 与提交信息与 brief Step 7 完全一致（提交信息逐字符比较 `match=True`）。行尾约定无变化：仓库 `core.autocrlf=true`、索引与工作区均 LF，新增文件同为 LF（git 的 LF→CRLF 提示为泛化警告，未改变实际内容）。

## 7. 自检结果

- **与 brief 逐行一致**：把 brief 的三个代码块（test `L23-106`、scene `L123-129`、script `L135-363`）抽成临时文件与我的产物做 `Compare-Object` → 三者均 **IDENTICAL**。
- **命名准确**：类名/命名空间/两个常量值/字段名（`_terrainGrid`、`_buildPalette`、`Swatch` 前缀）/场景节点名与脚本路径均与 brief 及测试断言一致。
- **YAGNI**：无额外成员、无额外场景节点、无额外依赖，未顺手改动任何邻近文件；未修复既有 7 个失败测试。
- **测试输出干净**：新增 7 个测试全绿、失败总数不变；警告仅新增 1 条既有结构型 CS2002。
- **前置约束遵守**：未触碰 `BottomPalette.cs` / `PanelState.cs` / `PaletteContent.cs` / `AdaptiveGrid.cs` / `TerrainSwatch.cs`；未生成子代理。

## 8. 遗留与风险（concerns）

1. **测试文件的 `.uid` 未入库**：`tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid` 已由 Godot 生成，但 brief 的 `Files:` 与 Step 7 `git add` 列表都只列了 `scripts/.../ToolPaletteSection.cs.uid`，故未提交（工作区中它仍是未跟踪文件）。仓库既有 **59** 个 `tests/**/*.cs.uid` 均已入库，且 `.uid` 未被 `.gitignore` 忽略，因此按仓库约定它应当入库——是否补提交请 controller 决定（一条 `git add` + `--amend`/后续提交即可）。
2. **本沙箱内 brief 原文的构建命令必然失败**：`dotnet build WorldView.sln -c Debug` 退出码 1、输出"生成失败 / 0 个警告 / 0 个错误"且无诊断。已用探针证明与 A1 无关：把 A1 的 4 个文件全部 `git stash` 移出（工作区回到 HEAD）后，同一条命令仍然 exit 1；失败点在 `WorldView.csproj` 的 `EnsureGeneratorBuilt` 目标内部对生成器工程发起的嵌套 `MSBuild` 任务（跨节点通信被沙箱限制）。**解决方案：所有 solution 级构建加 `-m:1`**（单工程 `dotnet build WorldView.csproj -c Debug` 亦正常）。后续任务建议沿用该参数。
3. **新增文件带来 1 条既有结构型告警 CS2002**：根因是 `WorldView.csproj` 同时包含 `scripts/**/*.cs` 与 `scripts\**\*.cs` 两个 glob（`EnableDefaultCompileItems=false`），使 `scripts/` 下每个 `.cs` 都触发一次"指定了多次"。修复需改 `WorldView.csproj`，超出 brief 的 `Files:` 范围，故未改动；如需在 A2 或后续任务中清理，建议单独一个任务处理（可顺带把 2 条重复 glob 合并）。
4. **`.uid` 生成方式与原命令口径不一致（已记录）**：brief Step 6 的命令（GUI 版 `.exe` + `--check-only`）在本环境既不等待进程也不生成 `.uid`；改用 console 版二进制 + `--headless --editor` 才生成。`.uid` 内容由 Godot 产出，未伪造。未提交的 `.superpowers/sdd/` 报告文件按 AGENTS.md 的 `.superpowers/sdd/` 白名单约定由 controller 侧处理。

---

## 9. 追加记录：按控制器裁定补交测试文件的 `.uid`（fix 1）

**改动内容**：补交 `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid`（唯一内容 `uid://difgoskro127p`）。此文件由 Godot 生成，brief 的 `Files:` 与 Step 7 `git add` 列表均漏列（计划缺陷，非越界）。控制器裁定：AGENTS.md 规定 `.uid` 入库，且仓库既有 59 个 `tests/**/*.cs.uid` 均已入库、`.uid` 未被 `.gitignore` 忽略，因此必须以独立提交补入。

**执行命令与输出**：

```
git add tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid
git diff --cached --name-only
  tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid
git commit -m "chore(tests): 提交 ToolPaletteSectionTests 的 .uid（AGENTS.md：.uid 入库）"
  [deepseek-build 9e25157] chore(tests): 提交 ToolPaletteSectionTests 的 .uid（AGENTS.md：.uid 入库）
   1 file changed, 1 insertion(+)
   create mode 100644 tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid

git log --oneline -2
  9e25157 chore(tests): 提交 ToolPaletteSectionTests 的 .uid（AGENTS.md：.uid 入库）
  fc706dc feat(ui): 新增工具方块区 section 容器（主界面布局规格 §4 row1）

git status --short
  ?? .superpowers/sdd/2026-10-10-bottom-tools-zone/        # 仅台账目录未跟踪，符合预期
git diff --cached --name-only                              # 空：暂存区已清空
git ls-files --error-unmatch tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid
  tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs.uid   # 确认已跟踪
git ls-files "tests/*.uid" | Measure-Object -Line → 60（补交前 59）
```

提交信息逐字符校验：`match=True`。HEAD = `9e25157218c06138401ea58b2c4079735402c5b8`。

**无回归验证**（提交后重跑，未改动任何源码，只新增一个已跟踪的 `.uid`）：

```
dotnet build WorldView.sln -c Debug -m:1     # exit 0（沿用裁定后的单节点口径）
& .\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe
  exit=2
  总计: 358
  失败: 7
  成功: 351
  已跳过: 0
  持续时间: 1s 498ms
```

与提交前一致：358 总计 / 7 失败（全部为既有失败）/ 351 成功，`ToolPaletteSectionTests` 7/7 绿。

**controller 裁定采纳的其它三项（不需要改代码，记录备查）**：concern 2（solution 级构建一律 `-m:1`，沙箱阻断嵌套 MSBuild）、concern 3（告警 184 → 185 的唯一增量 CS2002 来自 `WorldView.csproj` 双 glob 既有缺陷，本计划内接受不修）、concern 4（`.uid` 生成口径 `_console.exe --headless --editor --quit-after 600`，后续任务沿用）均已写入控制器台账。因此本次 fix 后 A1 无遗留 concern。
