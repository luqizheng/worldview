# World View v0.1 验收基线（baseline_v0.1）

> **本文件是什么**：工程**基线状态**的唯一对照真源。用途是让《长期执行计划》§0.5 的 B1–B4 自检从「凭印象」变为「有数可对」。
> **本文件不是什么**：不是「允许失败」的授权书。下文每一条失败都是**已知债务**，各自归属到修复工作项，不是豁免。
> **采集日期**：2026-10-10（+0800）· **采集者**：`P0-C2` 工作项（台账 `.superpowers/sdd/2026-10-10-p0-c2-acceptance-baseline/`）
> **维护规则**：基线变化（新增/消除失败、警告数变动）必须同步本文件，并在《长期执行计划》§12 追加变更行。改基线须有命令输出佐证。

---

## 1. B1 构建基线

### 命令

```bash
dotnet build D:\kudi-project\world-view\WorldView.sln -c Debug --no-incremental
```

> **`--no-incremental` 是强制项**（Ruling R5）。增量构建只报被重编项目的警告，数量随「哪些项目恰好被重编」浮动，与全量口径不可比（同一棵树可差一个量级）。任何「警告数不高于基线」的判据都必须基于全量口径。

### 结果（2026-10-10 采集）

| 项 | 值 |
| --- | --- |
| **error** | **0** |
| **warning（基线值）** | **48** |
| 构建耗时 | 约 1 分钟（全量） |

### 警告构成

48 = **1（CS2002）** + **47（其余 11 类）**

| 警告码 | 条数 | 含义 | 归属 | 处置 |
| --- | --- | --- | --- | --- |
| **CS2002** | **1** | 源文件指定多次（`scripts/Ui/Theme/Palette.cs` 同时被 `scripts/**/*.cs` 与显式 Include 项收录） | 配置缺陷（主因已修） | 2026-10-10：删掉 `WorldView.csproj` 的两条反斜杠重复 include，**138 → 1**；余 1 条恒定，不随脚本数增长 |
| xUnit1051 | 18 | 未使用 `TestContext.Current.CancellationToken` | `WorldView.Tests` | 未排期 |
| CS0108 | 14 | 成员隐藏继承成员（缺 `new`） | `WorldView` | 未排期 |
| RS2008 | 7 | 分析器规则缺 release tracking | `scripts/Generator` | 未排期 |
| xUnit2013 | 1 | 集合长度应改用 `Assert.Single` | `WorldView.Tests` | 未排期 |
| CS8625 / CS8620 / CS8604 / CS8602 / CS8601 | 各 1 | 可空性相关 | `WorldView` | 未排期 |
| CS0219 / CS0109 | 各 1 | 未使用变量 / 未隐藏基类成员 | `WorldView` | 未排期 |

> 计数口径：按 `文件(行,列): warning CODE` **唯一化**后计数。原始日志中每条出现两次（内联 + 汇总），未去重会得到 2 倍值。

---

## 2. B2 测试基线

### 命令

```bash
dotnet build WorldView.sln -c Debug
DOTNET_CLI_UI_LANGUAGE=en-US dotnet exec tests/WorldView.Tests/bin/Debug/net8.0/WorldView.Tests.dll
```

> **为何不用 exe 直跑、也不用 `dotnet test`**：
> - `WorldView.Tests.exe` 直跑在本机失败于 `hostfxr.dll 10.0.12` 加载被拒（`HRESULT 0x80070005`），**沙箱内外均复现**（非沙箱限制）。
> - `dotnet test` 的 VSTest 路径在本机 testhost 拉起必崩（`Win32Exception 5`），且项目已切 xunit.v3 + Microsoft.Testing.Platform（`OutputType=Exe`）。
> - 加 `DOTNET_CLI_UI_LANGUAGE=en-US` 是防中文输出乱码。

### 结果（2026-10-10，连跑 4 次）

| 项 | 值 |
| --- | --- |
| **总数** | **351**（4 次全一致） |
| **成功** | 343（成功数随 flaky 波动） |
| **稳定失败** | **7** |
| **flaky 失败** | **1~4（不定）** |
| 跳过 | 0 |
| 单次耗时 | 约 1 分钟 |

4 次实测 failed = **9 / 8 / 8 / 11**。

> ⚠️ **B2 基线不是一个数字**，而是「**稳定 7 + flaky 1~4**」。任何把 B2 基线写成单个失败数的记录都是错的。

### 2.1 稳定失败 7 条（每次必失败）

定性见《长期执行计划》**§6.4 主表**（本文不重抄，避免双源漂移）。逐条对应关系：

| # | 测试 | §6.4 行 | 判定 |
| --- | --- | --- | --- |
| 1 | `Editing.UndoStackTests.Apply_Undo_Loop_100_Times_Has_No_Residue` | 1 | 测试断言过期 |
| 2 | `Editing.UndoStackTests.Apply_Undo_Redo_Loop_100_Times_Keeps_Counter_Symmetric` | 2 | 测试断言过期 |
| 3 | `Editing.BrushShapesTests.Round_Radius_1_Covers_Exactly_Five_Cells` | 3 | 产品实现偏离规格 |
| 4 | `Editing.BrushShapesTests.Round_Radius_2_Covers_Expected_Cells` | 4 | 产品实现偏离规格 |
| 5 | `Storage.YamlSchemasTest.Deserialize_GridCoord_As_Array` | 5 | 测试笔误 |
| 6 | `Storage.ProjectLoaderTests.Load_Sample_World_Refs_Do_Not_Contain_DotDot_Or_Absolute` | 6 | sample_world 数据违规 |
| 7 | `Validation.ProjectValidatorTests.CharacterIndex_Missing_File_Is_Warning_Only` | 7 | 产品 bug（一行） |

### 2.2 flaky 集 6 条（间歇失败）

定性见《长期执行计划》**§6.4.1**（含已确认机制与已排除项）。概览：

| # | 测试 | 状态 |
| --- | --- | --- |
| F1 | `BackupServiceTests.PreOpenRotation_Keeps_Most_Recent_10` | 机制已确认（静默吞删除失败） |
| F2 | `BackupServiceTests.Delete_Removes_Content_And_Manifest_Entry` | 机制已确认（同上） |
| F3 | `BackupServiceTests.List_Returns_Manifest_In_CreatedAt_Order` | 待定性 |
| F4 | `ProjectWriteCoordinatorTests.WriteFileAtomic_Overwrites_Existing_File` | 待定性 |
| F5 | `RecentProjectServiceTests.RecordOpened_Dedupes_By_Path` | 待定性 |
| F6 | `RecentProjectServiceTests.RecordOpened_Caps_At_10_Oldest_Evicted` | 待定性 |

**处置纪律**：flaky 项**既不得**被当作「已知通过」而忽略，**也不得**被当作「必须修完才能提交」的红线；其修复与定性各单开工作项，排在 `P0-C2` 之后。

---

## 3. B3 / B4 口径

### B3 — Godot 工程完整性校验

```bash
"C:\App\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" \
  --headless --path D:\kudi-project\world-view --check-only --quit-after 120
```

**判据**：退出码 0，且 stderr 无**场景/脚本解析错误**。

- **必须带 `--quit-after 120`**：autoload `McpInteractionServer`（TCP 服务器）会在校验完成后挂住主循环，裸 `--check-only` 永不返回。
- **C# 运行时异常不计 B3 红**（《长期执行计划》§0.6.1）：`--check-only` 会实例化主场景节点，`MarkerLayer._Ready()` 的 `InvalidCastException` 属已登记运行时缺陷（§6.1-9），不阻塞校验。
- 2026-10-10 实测：**退出码 0**（stderr 含 §6.1-9 的两条 `InvalidCastException`，按上述口径不计红）。

### B4 — 工作树卫生

```bash
git status --short
```

**判据**：
- 与本任务无关的改动不得混入同一提交；
- **产物类不提交**：`*.log`、`TestResults/`、`*.trx`、`screenshot.png(.import)`、`.godot/`、`obj/`、`bin/`、`.dsh_tmp/`、`.tmp-pmtests/`；
- **`.uid` 例外**：Godot 4.7 用 `*.uid` 做跨机器资源稳定引用，**必须提交**（见 AGENTS §6.1 忽略策略）；
- `.superpowers/sdd/` 台账：仅 `progress.md` 与 `task-*-report.md` 入库，其余忽略。

---

## 4. 口径说明

1. **历史计数一律作废**：《长期执行计划》§1 快照、§6.1-4 及各 sdd 台账中出现的 `184/20`、`318/344`、`26 baseline 失败`、`344/337/7`、`19 条转绿` 等数字，均为**各自时点的临时记录，口径不一**。**自本文件起，一切基线数字以本文件为准。**
2. **警告数只认全量口径**：见 §1 的 `--no-incremental` 说明。
3. **失败数只认「稳定 7 + flaky 1~4」**：见 §2 的警示框。
4. **基线失败是债务不是豁免**：7 条稳定失败 + 6 条 flaky 均已归属工作项；修复项按《长期执行计划》§6.4 注记排在 `P0-C2` 之后，其中改 `scripts/` / `tests/` 者须按 §0.3 走**重档**规格化门。
5. **变更流程**：基线数字变动须重新采集、更新本文件、并在《长期执行计划》§12 追加一行。

---

## 5. 变更记录

| 日期 | 变更 |
| --- | --- |
| 2026-10-10 | 首版：`P0-C2` 采集并落盘。B1 = 0 error / 184 warnings（全量）；B2 = 351 总 / 343 成功 / 稳定失败 7 / flaky 1~4；B3 退出码 0；确立 B1 全量口径（Ruling R5）与「失败 = 稳定 7 + flaky」表述（Ruling R6）。 |

- 2026-10-10：B1 由 184 重设为 **185**（底部工具区重构新增 `scripts/Ui/Chrome/ToolPaletteSection.cs`，`WorldView.csproj` 重复 glob 导致每新增 `scripts/**/*.cs` 恰 +1 CS2002；见 `.superpowers/sdd/2026-10-10-bottom-tools-zone/progress.md` R12）。
- 2026-10-10（追加修复）：B1 由 **185** 重设为 **48**（= CS2002 ×1 + 其余 11 类 ×47）。根因：`WorldView.csproj` 用 `scripts\**\*.cs` 与 `scripts\Ui\Theme\Palette.cs` 把同一批源文件重复 include，于是每新增一个 `scripts/**/*.cs` 恰 +1 CS2002；删掉这两条反斜杠重复项后 CS2002 **138 → 1**（余 1 条来自 `scripts/Ui/Theme/Palette.cs` 被 `scripts/**/*.cs` 与显式 Include 同时收录，恒定）。采集命令 `dotnet build WorldView.sln -c Debug -m:1 --no-incremental`：冷树实测 **48 个警告 / 0 个错误**（exit 0）；`WorldView.Generator` 已是最新时同命令只重报 40（少 CS0219 ×1 + RS2008 ×7，两者都属该生成器项目），门是「不高于」，故基线取完整值 48。
