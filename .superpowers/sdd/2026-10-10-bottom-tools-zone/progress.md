# SDD ledger — plan: docs/superpowers/plans/2026-10-10-bottom-tools-zone.md

Branch: `deepseek-build`. BASE before Task A1: `7af25a9`（计划提交）。备份 tag：`backup/ui-redesign-bottom-tools` → `6e7ee75`。
Spec（绑定权威）：`docs/主界面布局规格_v0.1.md`（§4 底部=工具区 / §9 增量 1、3、4、5 / §2 / §0.1 纪律）。
档位：**全档**（改 `scripts/`、`scenes/`、`tests/`、`.trae/`）。
执行方式：**Subagent-driven**（用户 m00154 选「1.」；其余三份计划按需再写）。

## Preflight scan（Task A1 之前）

**共享文件 / 接口交叉**

| Producer → Consumer | 交叉物 | Finding |
| --- | --- | --- |
| A1 → A2 | `scenes/ui/ToolPaletteSection.tscn`（A1 产）vs `CreateSection(..., "ToolPaletteSection")`（A2 消费，实现为 `res://scenes/ui/<name>.tscn`） | OK，路径与大小写一致 |
| A1 → A3 | `ToolPaletteSection.cs` 内 `new TerrainSwatch` / `context.SetPaintTerrain(terrain);`（A1 产）vs A3 测试对这两个串的 `Assert.Contains` | OK |
| A1 → A4 | 无 | OK |
| A2 → A4 | `BottomPalette.ToolStripSectionId`（A2 声明，A4 使用） | OK，见 R2 |
| A2 ↔ A4 | **同一文件 `scripts/Ui/Chrome/BottomPalette.cs`**：A2 改常量区 + `EnsureDefaultRows()`；A4 改 `BuildChrome()`、`SetCollapsed()`、`CaptureLayout()` | OK，文本区间不重叠（A2 约 :30-32 与 :151；A4 约 :34-37、:97-122、:280-302、:358） |
| A3 → A4 | `ToolStrip.SetCollapsedMode(bool)`（A3 产）vs `SectionOf(ToolStripSectionId)?.Content is ToolStrip` → 调 `toolStrip.SetCollapsedMode(collapsed)`（A4 消费） | OK；`DockPanel.Content` 是 `Control?`，`is ToolStrip` 模式匹配可用 |
| A3 ↔ A4 | 不同文件 | OK |
| A5 | 只改 `.trae/specs/ui-redesign-simgame/spec.md` | OK，无交叉 |

**各 task 自洽性**

| Task | 自洽性检查 | Finding |
| --- | --- | --- |
| A0 | 无测试、无代码，纯 git + 测量 | OK（A0 由控制器执行，见 R7） |
| A1 | 测试断言的 8 个串（`_terrainGrid.Visible = isTerrain;` / `_buildPalette.Visible = !isTerrain;` / `InstantiateOrNull<BuildPaletteSection>()` / `foreach (TerrainType terrain in Enum.GetValues(typeof(TerrainType)))` / `new TerrainSwatch { Name = "Swatch" + captured, TerrainType = captured }` / `context.SetPaintTerrain(terrain);` / `context.SetTool(ToolState.Brush);` / `_buildPalette?.ApplyState(state);` / `_buildPalette?.CaptureState(state);`）全部逐字出现在 A1 Step 4 的源码里 | OK |
| A2 | 测试断言 `CreateSection(ToolsSectionId, "工具", PaletteSection.SectionSizeMode.Flex, 1, false, "ToolPaletteSection")` 与 Step 3 的替换行逐字一致；`DoesNotContain("\"build_palette\"")` 只匹配带引号形式（`PanelState.cs` 里剩下的 `仅 build_palette` 注释不带引号，不会误伤） | OK，但 `BottomPalette.cs` **:301-329 段未逐行读**，若那里还有 `"build_palette"` 字面量，测试会红 → A2 实现者须按测试要求清干净 |
| A3 | 测试 `DoesNotContain` 的 4 个串（`TerrainStrip` / `TerrainSwatch` / `_terrainSwatches` / `TerrainOrder`）与 Step 3 删除清单一致；`Contains` 的 5 个保留项（`ToolState.Selected/Brush/Building` / `_radiusSpin` / `_coordLabel`）不在删除清单内 | OK |
| A4 | 测试 `DoesNotContain` 的 3 个串（`_collapsedBar` / `面板已折叠` / `Ctrl+B 展开 · T 播放条 · M 小地图`）与 Step 3 删除清单一致；`Contains` 的 6 个串与 Step 4/5 的替换文本逐字一致 | OK |
| A5 | 纯文档，无测试 | 见 R5 |

**Rubric 冲突**：本计划强制要求「测试 = 读源码断言子串」（A1/A2/A3/A4 各一个测试文件）与「4 个测试文件各自复制 `RepoRoot` helper」。reviewer rubric 会把这些当作 Important（assert nothing / 逐字重复逻辑块），已按 R3 / R4 裁定保留。二者都是本仓既有约定（`ViewModels`… 实为 `tests/WorldView.Tests/Ui/Layout/ViewMenuTests.cs`、`FullscreenPersistenceTests.cs`、`LayerDependencyTests.cs` 三例）。

## Rulings

- **R1 — 用 git 分支 + 备份 tag，不建 worktree。** 理由：Godot 工程与 `.godot/` 导入缓存按目录绑定，worktree 会强制全量重导入；spec §0.1 明确「改现有 `Main.tscn`，用 git 分支管理，不用 `.bak` 副本」。沿用现有 `deepseek-build`（非 main/master），只加 annotated tag 作还原点。Cost if wrong：若用户本意是独立特性分支，事后需一次 `git branch` + cherry-pick 分离。
- **R2 — A2 同时声明 `public const string ToolsSectionId` 与 `private const string ToolStripSectionId`（后者 A4 才用）。** 理由：C# 不对未被读取的 `private const` 报诊断（CS0414 只针对私有字段赋值未用，不含常量），B1 不受影响；拆到 A4 需改计划文本。Cost if wrong：若真产生新警告，B1 从 0 破线，回退只需把该行搬到 A4。
- **R3 — UI 测试沿用「源码 / 场景文本护栏」范式（读文件断言子串），不引入 Godot 运行时。** 理由：本仓既有三例同范式，且测试进程没有 Godot 运行时，无法实例化 `Control`。Cost if wrong：护栏对措辞敏感、无行为覆盖，重构即红——本仓既定取舍。
- **R4 — 4 个新测试文件各自复制 `RepoRoot` helper，不抽公共 helper。** 理由：与既有 3 个测试文件一致；抽公共层超出本计划范围。Cost if wrong：4 处重复，后续统一需一次小重构。
- **R5 — A5 修订 `.trae/specs/ui-redesign-simgame/spec.md` §3.2 但**不 bump 版本号**。** 理由：该文件是 `.trae` 任务规格，当前真源是 `docs/主界面布局规格_v0.1.md`。Cost if wrong：旧规格与新实现措辞不同步，需补一次文档编辑。
- **R6 — 不改 `examples/sample_world/.worldview/layouts/sample_world.yaml`。** 理由：该文件未被 git 跟踪（运行时生成），且 `BottomPalette.ApplySectionStates` 对 `SectionOf(id) is null` 会 `continue` 跳过失效 section，新版仍能启动。Cost if wrong：本机样例工程丢失 `active_category: military` 记忆（spec §8 明确本轮不要求持久化页签）。
- **R7 — A0 由控制器会话直接执行（git tag + 基线测量），不 dispatch implementer。** 理由：不产生 diff、无评审面，其输出（基线数字）是控制器级事实。Cost if wrong：无（无代码改动）。
- **R8 — 任务简报不叫 `task-N-brief.md` 而叫 `task-A<N>-brief.md`；技能脚本用 PowerShell 复刻。** 理由：本 harness 的 `bash.exe` 是 WSL 且报 `Bash/E_ACCESSDENIED`，三个技能脚本不可用；且计划的任务标题是 `### Task A0:`…`### Task A5:`，技能脚本的 awk 模式 `Task[ \t]+N` 匹配不到 `Task A1`。Cost if wrong：无（简报只是派单载体）。
- **R9 — 本 harness 的 `subagent` 工具没有 model/provider 参数，无法满足技能「每次 dispatch 必须显式指定模型」的要求。** 派单继承会话默认模型。（`workflow` 工具可指定 provider/model，但用户未要求 workflow，且技能对 1–2 个委派明确建议用普通 subagent 调用。）Cost if wrong：可能在简单誊写型任务上用了过贵的模型，只影响成本不影响质量。
- **R10 — 新测试文件的 `.uid` 必须一并提交。** 依据：AGENTS.md 规定 `.uid` = 提交跟踪；仓库已有 59 个 `tests/**/*.cs.uid` 入库且 `.uid` 未被 `.gitignore` 忽略。计划的 `Files:` 列表漏了它（计划缺陷）。Cost if wrong：跨机器 clone 后 Godot 重绑该测试脚本引用；并入下一次触及该文件的提交即可。
- **R11 — 本沙箱内 solution 级构建一律 `dotnet build WorldView.sln -c Debug -m:1`。** 依据：A1 实现者探针证明 `EnsureGeneratorBuilt` 的嵌套 MSBuild 跨节点通信被沙箱阻断，不带 `-m:1` 时 `exit 1` 且无诊断（`dotnet build WorldView.csproj -c Debug` 单工程正常）。计划 Global Constraints 里的构建命令在沙箱内不成立。Cost if wrong：无（`-m:1` 只是禁用并行节点，语义等价）。
- **R12 — CS2002 警告随新增 `scripts/**/*.cs` 每文件 +1，接受，不在本计划修。** 根因：`WorldView.csproj` 同时声明 `scripts/**/*.cs` 与 `scripts\**\*.cs` 两个 glob（`EnableDefaultCompileItems=false`），`scripts/` 下每个 `.cs` 都触发一次「源文件指定了多次」。修复要改 `WorldView.csproj`，超出本计划所有 task 的 `Files:` 范围。Cost if wrong：B1 警告数从 184 涨到 190（本计划新增 6 个 `scripts/**` 之外的 `.cs` 不算；实际只 A1 新增 1 个 `scripts/**/*.cs`）。已在收尾时交给全分支评审 triage。
- **R13 — `.uid` 生成口径 = `Godot_v4.7.2-stable_mono_win64_console.exe --headless --editor --path D:\kudi-project\world-view --quit-after 600`。** 依据：GUI 版 `Godot_v4.7.2-stable_mono_win64.exe` 在 PowerShell 下不等待进程、stdout 为空、不生成 `.uid`；console 版带 `--check-only` 也不生成（只解析、不触发编辑器资源扫描）；只有 console 版 `--headless --editor` 才生成。计划 Step 6 写的是 GUI 版 + `--check-only`，在沙箱内不成立。Cost if wrong：无（`.uid` 内容由 Godot 产出，未伪造）。
- **R14 — A5 扩容：除旧规格 `.trae/specs/ui-redesign-simgame/spec.md` §3.2 外，还要修正 `docs/主界面布局规格_v0.1.md:517` 的过时注解。** 原文「`ActiveCategory` 是 `build_palette` 专用，语义挪用会污染字段」在 A2 把该 section 改名为 `tools` 后与实现不符。改法：把该括号注改为「`ActiveCategory` 是建筑面板私有状态，经 `tools`（工具方块区）转发」，并在该文档「变更记录」追加一行 `补充`（AGENTS.md 允许同软件版本内补充：保留既有文件名、文末追加 `补充` 行，不原地覆盖语义）。Cost if wrong：权威规格里留一句与实现不符的注解，误导后续会话。
- **R15 — A4 顺带用常量替换硬编码字面量。** `BottomPalette.EnsureDefaultRows()` 里 `toolRow.AddSection(CreateSection("tool_strip", …))` 的 `"tool_strip"` 应改为 `ToolStripSectionId`（一行 DRY，同时让 A2 声明、A4 使用的常量真正被使用）。已核对 A4 的测试只断言 `private const string ToolStripSectionId = "tool_strip";` 与 `toolStrip.SetCollapsedMode(collapsed);`，不断言该字面量，改动安全。Cost if wrong：若产生意外红灯，回退只需恢复字面量。
- **R16 — A2 实现者对 brief 未预料处做的自主裁定予以认可。** `PanelState.cs:21` 的类级 `Id` 示例注释原先写作 `<c>build_palette</c>`（带引号形式），会被 brief 自带的 `DoesNotContain("\"build_palette\"")` 断言命中；实现者把它改成 `<c>minimap</c>`——**这是 brief 文本自身的缺陷，改动在 A2 的 `Files:` 范围内且最小**。Cost if wrong：无（注释示例换个 id 语义等价）。【A2 评审指出：该断言搜的是**带引号**的 15 字符串，而原注释 `<c>build_palette</c>` 不带引号，其实**永远不会命中**；实现者的改动理由不成立（RED 时 `:57` 先失败，`:58` 从未执行）。控制器裁定：**保留该编辑**（示例改成存在的 id 是改进），理由记录为「碰巧的正确」；它与 `PanelDragData.cs:15` 的措辞分叉登记为 deferred minor。】
- **R17 — 修 A2 评审 Important 1（空断言）。** `BottomToolsZoneDefaultsTests.cs:90` 的 `Assert.Contains("continue;", BottomPaletteSource, …)` 是空断言（`continue;` 在 `BottomPalette.cs` 出现 6 次：:237/:356/:438/:484/:530/:589），删掉「未知 section 跳过」守卫它照样通过。改为**位置耦合**断言：先取 `"if (section is null)"` 的下标，再断言其后 80 字符内出现 `"continue;"`。真正的行为测试不可能——`ApplySectionStates` 需要构造 Godot 节点，测试进程无 Godot 运行时。Cost if wrong：仍非行为测试，但至少非空且与守卫位置耦合。
- **R18 — 修 A2 评审 Important 2（「只测源码文本」）。** 新增真行为断言，测 `LayoutState.Default(projectRoot)`——它是**纯 .NET**（只经 `ProjectIdOf` → `Path.GetFileName`；`GD.PushWarning`/`ProjectSettings.GlobalizePath` 只在 `Load`/`LayoutPath` 里，不在 `Default` 的调用路径上），故可在无 Godot 运行时的测试进程直接调用。断言：`bottom_palette → build_row → Sections` 含 `Id == "tools"`（`SizeMode=="flex"`/`Flex==1`/`Order==1`/`CellSize==56`/`CellGap==4`/`ActiveCategory=="residences"`/`PagesByCategory` 六键齐全）、**不含** `build_palette`、`minimap` 仍 `SizeMode=="fixed"` 且 `AutoWidth`；`EnsureDefaultRows()` 那半保留源码护栏（构造 Godot 节点）。若 `LayoutState.Default()` 在测试进程抛异常，必须报告并只保留护栏，**不得 try/catch 吞掉**。Cost if wrong：若 Godot 类型加载在静态构造期就要求原生库，该测试会红——实现者须报告，回退只保留护栏。**结果：`LayoutState.Default()` 在测试进程可用、未抛异常；R18 落成真行为断言（`BottomToolsZoneDefaultsTests.cs:109-143`）。**
- **R19 — 控制器直接修复台账报告文件的编码，不走 implementer。** A2 的 fix 轮把 §8 段落以 GBK 追加进 `task-A2-report.md`，文件自字节 16461 起非法 UTF-8（`read` 工具报 invalid UTF-8），而它是 AGENTS.md 白名单**入库**台账。控制器按 GBK(936) 解码尾部、重存为 UTF-8（24793 字节，严格 UTF-8 校验通过，内容逐字保留）。理由：`.superpowers/sdd/**` 是控制器的协调产物、不是产品代码；技能「控制器不得自己修 findings」针对的是会绕过评审的**产品代码**修复。Cost if wrong：无（只改编码）。
- **R20 — A2 评审的 3 个 ⚠️ 处置。** ① `PanelState.cs:319-33x` 的 `PagesByCategory` 尾部被 hunk 截断 → 控制器已直读 `PanelState.cs:300-337` 核实：`Id="tools"`(:312) / `Order=1`(:313) / `SizeMode="flex"`(:314) / `Flex=1`(:315) / `CellSize=56`(:316) / `CellGap=4`(:317) / `ActiveCategory="residences"`(:318) / 六类键 `residences,castles,religion,military,nature,facilities` 全在(:321-326) → **非缺口**；② 「四个字段经 `tools` 转发」是 A1 的契约，A1 的 `ApplyState`/`CaptureState` 无条件转发给 `_buildPalette`、不硬编码任何 section id（A1 评审已核）→ **非缺口**；③ 185 警告与 7 失败基线取自实现者报告 —— 按技能，报告即本流程指定的测试证据 → **非缺口**。
- **R21 — A3 的两处 brief 偏离均予认可。** (a) `_stateGroup` 额外设 `SizeFlagsHorizontal = SizeFlags.ExpandFill`：**必要**，`_statusLabel` 的 `ExpandFill` 必须经容器链式生效，否则「校验」标签不再贴右（brief 只写了 `SizeFlagsVertical`，是 brief 的遗漏）；(b) 类级 XML doc-comment 原文仍列「地形」，随迁出更新为实况。两者都在 `ToolStrip.cs` 范围内、都是正确性/真实性改进。Cost if wrong：(a) 若判断有误，表现为校验标签不再贴右——A4 的 F5 冒烟可发现。
- **R22 — 台账报告与 `progress.md` 由控制器在收尾时统一提交。** 4 份 `task-A*-report.md` + `progress.md` 是 AGENTS.md 白名单入库项（`.superpowers/sdd/.gitignore` = `*` + `!*/` + `!.gitignore` + `!progress.md` + `!task-*-report.md`），任务提交里不夹带它们。Cost if wrong：跨机器 clone 查不到逐任务实况；本机与后续会话不受影响。
- **R23 — A3 评审的 3 个 ⚠️ 处置，以及 F5 目检的归属。** ① `.uid` provenance（是否由 console Godot 生成）→ 接受实现者报告所载命令与 exit 0（报告即本流程指定的测试证据），且评审已验其内容格式合法、全库唯一、与测试同提交；② 报告口径的构建/测试数字 → 同上，接受报告证据；③ **F5 视觉目检尚未执行**（20px 行高、校验标签贴右、折叠态 `[V][B][P] │ 坐标(-,-)`、折叠↔展开往返后布局文件 `visible` 仍为 `true`）→ 沙箱内无可靠 GUI 通道，**控制器裁定该目检归用户执行**（计划 A4 Step 7 原文即「手动冒烟（若可开窗口）」），代码级替代证据：折叠态可见子节点由 A3 评审逐行核实为 `[V][B][P] │ _coordLabel`。Cost if wrong：视觉回归要等用户 F5 才发现。
- **R24 — 折叠态下 `ToolStrip` 的独立分隔符保留。** brief 规定 `SetCollapsedMode` 只隐藏三个分组容器、工具三键与坐标常驻，因此折叠后仍显示 `[V][B][P] │ 坐标(-,-)`。控制器裁定**保留**（A3 评审亦判为可接受的视觉分隔）。Cost if wrong：折叠态多一个竖线；改法只是多加一次 `NewSeparator()` 的可见性切换。
- **R25 — 本沙箱的 B3 门替换口径。** A4 实测 `Godot_v4.7.2-stable_mono_win64.exe --headless --check-only --quit-after 120` 现在返回 **EXIT=-1**（沙箱不可写 `user://logs`、`Failed to read the root certificate store.`，外加既有 `scripts/Render/MarkerLayer.cs:83` / `RouteLayer.cs:61` 的 `CompressedTexture2D → Image` InvalidCastException）。A4 实现者用 `git stash -u` 回到 `784a5b6` 重跑，错误与退出码**完全一致** → 证明与本次改动无关（A0 采基线时该命令还是 exit 0，属沙箱环境漂移）。**替代门（本计划余下任务沿用）**：`Godot_v4.7.2-stable_mono_win64_console.exe --headless --editor --path <repo> --quit-after 600` = EXIT 0，且输出中无 `Parse Error` / `SCRIPT ERROR` / 目标文件命中。Cost if wrong：无（`--editor` 口径更严格，它真的加载并扫描资源）。
- **R26 — A4 的类级 XML 注释更新予以认可。** 原注释仍描述旧行为（「折叠时整行一起收起」），与 R21(b) 同类：随实现更新为实况，属真实性改进、且在 `BottomPalette.cs` 范围内。Cost if wrong：无（纯注释）。
- **R27 — A4 的 task reviewer 首次派单（`aa003cef`）在完成前失败且无报告 → 按技能「chase any that finished without reporting」，重派一次同规格 task review（`79e8cd5a`，同简报/报告/diff 路径）。** Cost if wrong：多一次评审开销。
- **R28 — A4 评审 Minor 2（折叠态下守卫对**每一行**写 `visible: true`；且 `SetCollapsed` 每次 `ApplyLayout` 都强制三行可见，手工改过 YAML 的行级 `visible: false` 会被静默抹掉）裁定为：**保留现状，不改**。** 理由：① brief 逐字规定该正文与 `true` 值，spec §4 的 K1 折叠态本来就要求由 `SetCollapsed` 控制行可见性；② 全仓**没有任何 UI 路径**能设置行级 `visible`（`Key.M` 只切 minimap **section** 的可见性，不是行），唯一触发方式是用文本编辑器手改布局 YAML；③ 改成「记住并恢复加载值」会引入新的会话状态与恢复逻辑，是本轮范围外的复杂度。Cost if wrong：手改过 YAML 行可见性的用户会失去该设置，补救只需把守卫改为写 `null`/保留加载值。
- **R29 — A4 评审的 2 个 ⚠️ 处置。** ① 构建/测试数字与 `.uid` provenance 取自实现者报告 → 按技能，报告即本流程指定的测试证据，接受；② **brief Step 7 的冒烟项 ④（Ctrl+B 后只剩一行紧凑工具行）与 ⑤（反复折叠/展开后退出、YAML 里行的 `visible` 仍为 `true`）未执行** → 归入 R23③ 交用户的 F5 目检清单（评审的静态判断：`_collapseButton` 是 `BottomPalette.cs:95` 的直接子节点，不随隐藏行消失，故 ④ 在静态上成立）。Cost if wrong：视觉/持久化回归要等用户 F5 才发现。
- **R30 — A5 的 Edit 1 改指向 §7 T8，并追加两处落点同步。** A5 实现者按 brief 的停止条件正确停下并报告：计划与 brief 都把折叠态描述标成 `.trae/specs/ui-redesign-simgame/spec.md` **§3.2**，实际在 **§7 T8（`spec.md:630-636`，标题「T8 — BottomPalette 折叠」，`:632`「折叠后只露一行图标」、`:633`「折叠时 `timeline_row` 一起收起」、`:636` DoD）**；§3.2（`spec.md:123-134`）是 rows→sections 内部结构，**零折叠态文字**；brief 里「显示两行文本提示」那半句描述的是旧 C# `_collapsedBar`，spec 里本就不存在。裁定：① Edit 1 改到 §7 T8，替换 `:632`/`:633` 为 K1 语义、`:636` 只改与之直接冲突的判定措辞（不扩写）；② 追加 `spec.md:128` 的树 `← [minimap | build_palette]` → `← [minimap | tools（工具方块区）]`；③ 追加 `docs/主界面布局规格_v0.1.md:401` 的「地形色块宜从 `ToolStrip` 迁入工具方块区」改为完成时（A1–A4 已落地）；④ 提交信息小幅扩展为 `docs: 折叠态描述、工具方块区落点与 ActiveCategory 注解同步为工具方块区实现`。仍**不 bump 任何版本号**（R5）。`git diff --stat` 目标仍是**恰好 2 个文件**。Cost if wrong：旧 `.trae` 规格里留一处与实现不符的折叠态/树描述，或提交信息少描述一处编辑。**结果：commit `980b956`，2 文件 / +6−5；`:636` DoD 经实现者说明理由后**未改**（该行只含「手动验证（G8 / G13）」与「折叠态画布 ≥ 90%」，无 K1 冲突措辞，且 K1 折叠仍 40px → `spec.md:77` 93.3% ≥ 90%），控制器认可。**
- **R31 — A5 的 fix round 1（评审回来后合并做一轮）追加四处措辞/指向收尾。** 前两处由实现者自己登记、后两处由 A5 评审在 out-of-scope 里新增，四处都在 A5 已改的两个文件内、都是本轮改动造成的陈旧表述：
  - (a) `docs/主界面布局规格_v0.1.md:401` 引导语「实现提示（落地时才做，本稿不展开）」→「实现提示（本轮 A1–A4 已落地，本稿不展开）」——同一行内将来时与改后的完成时自相矛盾；
  - (b) `.trae/specs/ui-redesign-simgame/spec.md:65` 的 G8「可折叠成 40px 图标条」→「可折叠成 40px 紧凑工具行（`ToolStrip` 紧凑常驻变体）」——K1 落地后已无「图标条」；
  - (c) `spec.md:129`（`:128` 那个 hunk 的上下文）仍把 `tool_row` 列成「工具 / 笔刷 / 地形 / 撤销」——地形已迁入 `tools` 工具方块区，须与 `:128` 的新树一致；
  - (d) `docs/主界面布局规格_v0.1.md:419`「落地需同步改 `BottomPalette` 折叠分支 + 修订 spec §3.2」→「已改 `BottomPalette` 折叠分支，并已修订旧规格 §7 T8（原稿此处误标为 §3.2）」——**评审指出这一行就是 brief 把 §3.2 标错的来源**，修正指向可防后续会话再被误导。
  - 另：订正 `task-A5-report.md:354` 写错的 `spec.md` blob 索引（`55a379c..a4f6d99` → 实际 `b6e5510`）。
  提交信息：`docs: 工具方块区措辞收尾（G8/tool_row/折叠分支指向 + 实现提示时态）（A5 fix 1）`。改带 BOM 的 `spec.md` 后必须复查首三字节 `EF BB BF` 且无 `spec.md:1` hunk（R32）。Cost if wrong：四处旧措辞留存，读者需自行判断。
- **R32 — BOM 教训（本仓 `edit` 工具会静默剥离文件 UTF-8 BOM）。** `.trae/specs/ui-redesign-simgame/spec.md` 带 BOM（首三字节 `EF BB BF`）；A5 首次编辑因此多出 `spec.md:1` 越界 hunk（`--stat` 曾读作 `8 ++++----`），实现者读全量 diff 时发现并逐字节写回修复（44608 → 44611 字节），提交前已复原（控制器复核提交后文件首三字节 = 239,187,191）。**规则：任何编辑带 BOM 的文件之后，必须复查首个 hunk 与首三字节。** Cost if wrong：BOM 被无声删除、diff 多出无关行、破坏该文件既有 BOM 约定。**A5 fix round 1 再次复现同一现象**（编辑工具又把 `spec.md` 剥成 44641 B / `35,32,85`），实现者再次逐字节写回 44644 B / `239,187,191`，提交后控制器复核首三字节 = 239,187,191、全部 hunk 无 `:1`。规则得到第二次验证。
- **R33 — `spec.md:636` 的 DoD 行确认保持不改。** 复核 R30 的裁定：该行只含「手动验证（G8 / G13）」与「折叠态画布 ≥ 90%」，不含任何与 K1 冲突的措辞；K1 折叠仍 40px（`spec.md:58`/`:77` 给出 1008/1080 = 93.3% ≥ 90%），G13「播放条与折叠状态始终同步」在 `timeline_row` 与 `build_row` 一同收起后依然成立。A5 评审也判该决定「正确而非回避」。Cost if wrong：无（无冲突可不改）。


### 基线（2026-10-10 实测）

| 门 | 文档基线 | 本轮实测 | 说明 |
| --- | --- | --- | --- |
| B1 | 0 error / 184 warnings | **0 error / 184 warnings（`-m:1` + `--no-incremental`）** | ⚠️ **控制器此前的「0 警告」测量是错的**：`dotnet build WorldView.sln -c Debug` 在本沙箱**必然 exit 1**（`WorldView.csproj` 的 `EnsureGeneratorBuilt` 目标对生成器工程发嵌套 MSBuild，跨节点通信被沙箱阻断），输出只有「生成失败 / 0 个警告 / 0 个错误」且无诊断 → 我过滤到的正是这条假的汇总。A1 实现者用 stash 探针证明与改动无关，并给出可用口径：**solution 级构建一律加 `-m:1`**。**自检门 = 0 error 且新增警告只允许来自 CS2002（每个新增 `scripts/**/*.cs` 恰好 +1）。** |
| B2 | 351 总 / 343 成功 / 稳定失败 7 / flaky 1~4 | **351 总 / 344 成功 / 失败 7 / 跳过 0 / 1.15s / exit 2** | 裸 exe 直跑可用；`dotnet exec <dll>` 回落路径同样可用。⇒ **exit 2 是预期值**，自检门是「失败数仍为 7 且新增测试全绿」。 |
| B3 | 退出码 0 | **exit 0** | `--headless --check-only --quit-after 120`，输出 `McpInteractionServer: Stopped`；stderr 的 `Godot.GD.PushError` 异常栈是既有噪音。 |

稳定失败样例：`WorldView.Tests.Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute()`（`tests/WorldView.Tests/Storage/ProjectLoaderTests.cs:162`）。

### 交用户的 F5 目检清单（R23③/R29②，沙箱无法代做）

1. 展开态底部分行：`timeline_row` 28px + `build_row` 152px（小地图 auto-square ≈104×104 + 工具方块区）+ `tool_row` 20px，合计 200px。
2. 工具方块区「地形 / 建筑」两个分组按钮：任一时刻只有一个分组内容可见；点地形色块后画布可绘制且工具切到笔刷。
3. `tool_row` 的「校验」标签是否仍贴右（A3 给 `_stateGroup` 补了 `SizeFlagsHorizontal = ExpandFill` 的展开链）。
4. Ctrl+B 折叠后只剩一行紧凑工具行 `[V][B][P] │ 坐标(-,-)` 与右上角 ▴；展开恢复。
5. 反复折叠 / 展开后退出，检查 `.worldview/layouts/<projectId>.yaml` 里 `timeline_row` / `build_row` 的 `visible` 仍为 `true`（不得被折叠态写坏）。
6. 旧布局文件（含 `build_palette` 节）仍能正常启动：`build_palette` 被跳过、工具方块区正常显示。
7. 折叠态下 `ToolStrip` 的可见构成确实只有 `[V][B][P] │ 坐标(-,-)` 与右上角 ▴（A3 评审的静态判断，需人眼确认）。

## 收尾段（全部 task 完成后填写）

**结论：本计划 A0–A5 全部完成，最终全分支评审的结论填在下方。**

### 交付物

| 项 | 路径 |
| --- | --- |
| spec（绑定权威） | `docs/主界面布局规格_v0.1.md` |
| plan | `docs/superpowers/plans/2026-10-10-bottom-tools-zone.md` |
| 台账 | `.superpowers/sdd/2026-10-10-bottom-tools-zone/` |
| 备份 tag | `backup/ui-redesign-bottom-tools` → `6e7ee75` |

### 提交链（分支 `deepseek-build`）

| # | commit | 内容 |
| --- | --- | --- |
| — | `7af25a9` | 计划入库 |
| A1 | `fc706dc` + `9e25157` | 新增 `ToolPaletteSection` + 场景 + 护栏测试（+ 测试 `.uid`） |
| A2 | `e4b758f` + `f4a7bd8` | 默认 section 配置 `build_palette` → `tools`（+ R17/R18 fix） |
| A3 | `784a5b6` | 地形色块迁入工具方块区；`ToolStrip` 分组化 + `SetCollapsedMode` |
| A4 | `706841c` | 折叠态改 `tool_row` 常驻紧凑行 + `CaptureLayout` 落盘守卫 + R15 常量替换 |
| A5 | `980b956`（+ fix 1） | 文档同步：§7 T8 折叠态、`spec.md:128` 树、`docs:401/517/721` |

### 门（每个 task 之后复测）

| 门 | 结果 |
| --- | --- |
| 构建 | `dotnet build WorldView.sln -c Debug -m:1 --no-incremental` = **0 error / 185 warnings**（基线 184 + A1 新增脚本的 CS2002 一条，见 R12） |
| 测试 | **375 总 / 稳定失败 7（与基线同名，未变）/ 368 成功 / exit 2（预期）** |
| 无头校验 | 见 R25：`--check-only` 沙箱漂移为 EXIT=-1；改用 console `--headless --editor --quit-after 600` = EXIT 0 |
| `.uid` | 3 个新 `.uid` 全部由 console Godot 生成并入库（`ToolPaletteSection.cs.uid`、`ToolPaletteSectionTests.cs.uid`、`ToolZoneMigrationTests.cs.uid`、`BottomPaletteCollapsedTests.cs.uid` 共 4 个） |

## Ledger

| Task | 状态 | 产出 | commit |
| --- | --- | --- | --- |
| A0 备份 tag + 基线 | ✅ 完成 | 本台账「基线」节 | —（仅 tag） |
| A1 新增 `ToolPaletteSection` | ✅ **complete**（commits `fc706dc`..`9e25157`，review clean：Spec ✅ / Task quality Approved，0 Critical / 0 Important / 4 Minor 已 deferred） | `task-A1-report.md` / `review-7af25a9..9e25157.diff` | `fc706dc` + `9e25157` |
| A2 默认 section 配置 `build_palette` → `tools` | ✅ **complete**（commits `e4b758f`..`f4a7bd8`，review clean：首轮 Spec ✅ / Approved + 2 Important plan-mandated → R17/R18 fix round 1 → re-review「All findings addressed, no new Critical/Important breakage」） | `task-A2-report.md` / `review-9e25157..e4b758f.diff` / `review-e4b758f..f4a7bd8.diff` | `e4b758f` + `f4a7bd8` |
| A3 地形色块迁移 + `ToolStrip` 分组化 | ✅ **complete**（commits `f4a7bd8`..`784a5b6`，review clean：Spec ✅ / Approved，0 Critical / 0 Important / 3 Minor 已 deferred；两处 brief 偏离经评审判为 correct & minimal → R21） | `task-A3-report.md` / `review-f4a7bd8..784a5b6.diff` | `784a5b6` |
| A4 折叠态改 `tool_row` 常驻 + 落盘守卫（含 R15：用 `ToolStripSectionId` 替换 `:158` 的 `"tool_strip"` 字面量） | ✅ **complete**（commit `784a5b6`..`706841c`，review clean：Spec ✅ / Approved，0 Critical / 0 Important / 3 Minor 已 deferred；R15 落在 `:131`，守卫落在 `:361-363`） | `task-A4-report.md` / `review-784a5b6..706841c.diff` | `706841c` |
| A5 修订旧规格 §3.2 + R14 修正 `docs/主界面布局规格_v0.1.md:517` 过时注解 + 该文档「变更记录」追加 `补充` 行 | ✅ **complete**（commits `706841c`..`d9d1f2d`，review clean：首轮 Spec ✅ / Approved（0 Critical / 0 Important）+ R31 fix round 1 → scoped re-review「All 5 findings ADDRESSED, no new Critical/Important breakage」；`spec.md:636` DoD 经裁定保持不改，见 R33） | `task-A5-report.md` / `review-706841c..980b956.diff` / `review-980b956..d9d1f2d.diff` | `980b956` + `d9d1f2d` |
| 最终全分支评审 + 收尾 | 🔄 进行中（range `7af25a9`..`d9d1f2d`，13 commits；评审已派） | `review-final-7af25a9..d9d1f2d.diff` | — |

### A0 结果摘要（2026-10-10）

- `git tag -a backup/ui-redesign-bottom-tools -m "backup: HEAD before bottom tools-zone rework (spec v0.1 increments 1/3/4/5)"` → tag 指向 `6e7ee75`。
- 计划已入库：`7af25a9 docs(plan): 底部工具区重构实施计划（主界面布局规格 v0.1 增量 1/3/4/5）`。
- 台账工作区：`.superpowers/sdd/2026-10-10-bottom-tools-zone/`（`plan-path` = `docs/superpowers/plans/2026-10-10-bottom-tools-zone.md`）。
- 简报已抽出：`task-A0-brief.md`(48) / `task-A1-brief.md`(392) / `task-A2-brief.md`(165) / `task-A3-brief.md`(181) / `task-A4-brief.md`(208) / `task-A5-brief.md`(32)。
- 环境：`bash.exe`（WSL）报 `Bash/E_ACCESSDENIED`；`pwsh` 不在 PATH（后台任务里 `pwsh -File` 报 `CommandNotFoundException`）→ 命令一律内联进 `pwsh` 工具调用。

### A1 结果（2026-10-10）

`Task A1: complete (commits fc706dc..9e25157, review clean)`

- 交付：`scripts/Ui/Chrome/ToolPaletteSection.cs`(229) + `.cs.uid`(`uid://ygxcwxlitbis`) / `scenes/ui/ToolPaletteSection.tscn`(7) / `tests/WorldView.Tests/Ui/Chrome/ToolPaletteSectionTests.cs`(84，7 个 `[Fact]`) + 测试 `.cs.uid`(`uid://difgoskro127p`)。5 个新文件 / +322，**零修改**（`BottomPalette.cs` / `PanelState.cs` / `DockLayout` / `DockZone` / `DockPanel` / `PaletteRow` / `PaletteSection` / `scripts/Domain/**` 全未触碰）。
- 公开面与简报 `Interfaces / Produces` 逐项一致：`TerrainGroupId`(:25) / `BuildingGroupId`(:28) / `ActiveGroup`(:44) / `SelectGroup(string)`(:133) / `ApplyState(PanelState?)`(:157) / `CaptureState(PanelState)`(:162)。
- 互斥已核实：`_terrainGrid.Visible = isTerrain;`(:140) 与 `_buildPalette.Visible = !isTerrain;`(:145)，`_Ready`(:105) 即调用，`ButtonGroup` + `_syncing`(:148-154, :180-186) 防重入。
- 证据：`dotnet build WorldView.sln -c Debug -m:1` exit 0，警告 184 → **185**（唯一新增为 CS2002，即 R12 预接受项）；裸 exe 全量 **358 总 / 失败 7（不变）/ 成功 351 / exit 2**；`--filter-class ToolPaletteSectionTests` 7/7 绿。TDD RED 已证实（仅测试文件时 7/7 失败于 `get_ScriptSource()` 的 `FileNotFoundException`）。
- 评审 ⚠️ 三项由控制器裁定为**非缺口**：① 运行时行为无法由 diff 验证 → A4 Step 7 已含 F5 手动冒烟清单；② `.uid` 取值只能由 Godot 解析确认 → 两个 `.uid` 均由 `_console.exe --headless --editor` 产出，未伪造；③ A2 的默认配置接线属 A2 范围。
- 评审提出的**命名风险**均由 reviewer 自查清空：把 `PaletteContent` 内嵌进普通 `Control`（`Section`/`Row` 为 null）安全，因 `BuildPaletteSection` 不引用 `Section`/`Row`/`GetParent`、只用 `MarkLayoutChanged()`（`PaletteContent.cs:35` 独立解析 `DockLayout.Instance`）；`ApplyState` 在 `_Ready` 之后才被调用（`BottomPalette.cs:504` → `:508`）。

**deferred minors（交最终全分支评审 triage）**

- `Task A1: minor (deferred): plan-mandated — 7 个测试断言源码文本而非运行时行为（`ToolPaletteSectionTests.cs:36-83`）；test 3 (:50-55) 在 `SelectGroup` 从未被调用或那两行落在死代码里时仍会通过。` 裁定 R3 已接受该范式；若要真覆盖，需补一个在 `SceneTree` 下实例化场景的运行时测试，或在 A2/A4 用 F5 冒烟覆盖。
- `Task A1: minor (deferred): plan-mandated — `ActiveGroup` 不持久化：`ApplyState`/`CaptureState`(:157-165) 只转发内层建筑面板状态，故「地形/建筑」选择在布局还原后回到「地形」。` 这是 schema v1 冻结（spec §8「不改 schema」）的直接后果，**有意接受**。
- `Task A1: minor (deferred): `SetProcess(true)`(:106) 冗余（Godot 在覆写 `_Process` 时自动启用处理），无害。`
- `Task A1: minor (deferred): `_Process` 每帧轮询 `context.PaintTerrain`(:109-127) 刷新色块选中态；用 `EditorContext` 信号更省更清晰。` 与迁出前的 `ToolStrip` 同范式，属一致性而非新气味。
- `Task A2: minor (deferred): `scripts/Ui/Dock/Leaf/PanelDragData.cs:15` 的示例注释仍以 `build_palette` 作 id 示例，与 A2 之后的事实不符（不在 A2 的 `Files:` 范围内）。`
- `Task A2: minor (deferred): `BottomPalette.cs:38` 的 `private const string ToolStripSectionId = "tool_strip";` 声明后未被使用，而 `:158` 仍硬编码同一字面量（死常量 + 活字面量重复）→ 归 R15 / Task A4 用常量替换 `:158`。`
- `Task A2: minor (deferred): `PanelState.cs:21` 的类级 `Id` 示例注释被改为 `<c>minimap</c>`，与 `PanelDragData.cs:15` 的 `<c>build_palette</c>` 分叉；控制器保留该编辑（R16，示例改成存在的 id 是改进），但评审指出其理由不成立（断言搜的是**带引号**字符串，原注释不含引号故永不命中，RED 时 `:57` 先失败、`:58` 从未执行）。`
- `Task A3: minor (deferred): plan-mandated — `ToolZoneMigrationTests.cs:37-70` 的 4 个 Fact 全为源码文本断言；`:63-70` 只 grep `_brushGroup.Visible = !collapsed;` 等串，落在注释里也会通过，`SetCollapsedMode` 的运行时效果无测试。非空（RED 证 `:40`/`:66` 缺实现即失败）。` 裁定 R3 接受该范式；报告 §5.1 已请求 A4 补行为断言。
- `Task A3: minor (deferred): 折叠态下 `ToolStrip.cs:161` 的独立分隔符仍可见（`SetCollapsedMode` 只隐藏三个分组），折叠后为 `[V][B][P] │ 坐标(-,-)`。` 与 brief 规定的结构一致；已加入 A4 的 F5 冒烟项复核。
- `Task A3: minor (deferred): `SetCollapsedMode` 在 A3 中未被调用（A4 才接线）——可接受，三组默认 `Visible=true`，当前渲染无变化。`
- `Task A3: minor (deferred): AGENTS.md §6.2 与 `docs/acceptance/baseline_v0.1.md` 冻结的 B1 = 184 警告，在 A1 新增一个 `scripts/**/*.cs` 后合法变为 185（CS2002 双 glob 每新增脚本 +1，见 R12）。文档口径与实况需在收尾时由最终评审 triage（是否单开一个修 `WorldView.csproj` 双 glob 的工作项）。`
- `Task A4: minor (deferred): plan-mandated — `BottomPaletteCollapsedTests.cs:32` 起 6 个 Fact 全为 `File.ReadAllText` + 字符串断言，没有任何测试真正执行 `SetCollapsed` / `CaptureLayout`，故 brief 自称的「真正的陷阱」（折叠 → flush → 重载）只被字符串存在性守着。` 裁定 R3 接受；若要真覆盖，需用 fake host 做 `CaptureLayout`/`ApplyRowStates` 往返测试。
- `Task A4: minor (deferred): `BottomPalette.cs:363` 折叠时对**每一行**写 `visible: true`，且 `SetCollapsed` 每次 `ApplyLayout` 都强制三行可见（`:270-283`），因此手工改过 YAML 的行级 `visible: false` 会被抹掉（A4 之前只切 `_rows.Visible`，行级值会保留）。` 裁定 R28 保留现状；仅影响手改 YAML。
- `Task A4: minor (deferred): `BottomPaletteCollapsedTests.cs:59-62` 的精确源码断言会在 `dotnet format` / 重命名后变成假失败（全仓护栏共性）。`
