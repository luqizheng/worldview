# P0-C2 验收基线固化 — 实施 Plan（轻档）

## Goal

按 `docs/superpowers/specs/2026-10-10-p0-c2-acceptance-baseline.md` 产出 `docs/acceptance/baseline_v0.1.md`，固化 B1 警告基线、B2 测试基线（含 7 条已知失败的逐条定性与归属），并回写《长期执行计划》§5 / AGENTS §6.2，使后续所有工作项的 §0.5 自检有可对照真源。

## Architecture

纯文档流水线，三个采集任务 → 一个落盘任务 → 一个回写任务。数据流单向：**命令输出 → 记录 → 基线文档 → 回写索引**。无代码依赖、无接口交叉（Pre-flight 记为「无接口交叉」）。

## Tech Stack

命令与工具沿用《长期执行计划》§0.5 与 AGENTS §6.3：`dotnet build`（B1）、`WorldView.Tests.exe`（MTP 直跑，B2）、Godot `--headless --check-only --quit-after 120`（B3）、`git status --short`（B4）。不引入新工具。

## Spec 链接

`docs/superpowers/specs/2026-10-10-p0-c2-acceptance-baseline.md`（轻档：目标 / 非目标 / 不涉及 / 验收 / 风险）

## Global Constraints

逐条摘自《长期执行计划》§2 与 AGENTS.md §2/§5：

1. 单机可运行：零网络依赖——采集全程离线，不拉任何远程数据。
2. YAML 为主：本项不产出 YAML；基线文档为 Markdown。
3. 数字纪律：数字必须有命令输出佐证，禁止「应该没问题」（§8-4）；禁止新造魔数。
4. 禁止过度构建：只做 §5 `P0-C2` 行定义的交付物，不顺手修测试、不顺手改源码。
5. 版本纪律：改设计先升版本号；本项不改任何规格文档的语义，仅回写状态列。
6. 语言纪律：文档中文，命令与路径英文原样。
7. 提交纪律：每 task 一次提交，提交前过 B1–B4，产物类文件不入库。
8. **留痕纪律（§0.3/§0.4）**：开工先建台账；每 task 必写 `task-N-report.md` 并在 `progress.md` 追加 ledger 行；缺 report 视同未完成。

## Task 1 — B1 警告基线采集

Files:
- 读：`WorldView.sln`（构建入口，不改）
- 写：`.superpowers/sdd/2026-10-10-p0-c2-acceptance-baseline/task-1-report.md`

- [ ] 跑 `dotnet build D:\kudi-project\world-view\WorldView.sln -c Debug`，记录：error 数（应为 0）、warning 总数、按 CS 编号分组的警告清单。
- [ ] 若 error ≠ 0：停止，项转 `blocked`，失败输出写入《长期执行计划》§6.3，会话结束。
- [ ] 写 `task-1-report.md`：命令原文 + 输出摘要 + 警告分组表 + 采集时间。

验证：命令输出中有 error 行数为 0 的明确结果；`task-1-report.md` 存在且含分组表。

## Task 2 — B2 测试基线采集

Files:
- 读：`tests/WorldView.Tests/bin/Debug/net8.0/WorldView.Tests.exe`（MTP 直跑入口，不改）
- 写：`.superpowers/sdd/2026-10-10-p0-c2-acceptance-baseline/task-2-report.md`

- [ ] 先 `dotnet build` 确保 exe 为最新，再跑 `& "D:\kudi-project\world-view\tests\WorldView.Tests\bin\Debug\net8.0\WorldView.Tests.exe"`。
- [ ] 记录：总数 / 通过 / 失败 / 跳过，以及失败测试的完整名称清单。
- [ ] 与 §6.4 已实测量（344 总 / 337 成功 / 7 失败）对账；不符则记差异，**以本次实测为准**。
- [ ] 写 `task-2-report.md`。

验证：失败清单与 §6.4 表 7 行逐条同名（或差异已显式记录并说明原因）。

## Task 3 — 失败定性核对（**§6.4 已完成，本 task 只做核对与引用**）

Files:
- 读：《长期执行计划》§6.4 判定表
- 写：`.superpowers/sdd/2026-10-10-p0-c2-acceptance-baseline/task-3-report.md`

- [ ] 核对 §6.4：7 条已全部定性（过期断言 2 + 产品偏离规格 2 + 测试笔误 1 + 示例数据违规 1 + 产品 bug 1），每条带依据行号与修法。
- [ ] 基线文档直接引用 §6.4，不重复抄写；若 Task 2 出现 §6.4 未覆盖的新失败，补定性并回写 §6.4。
- [ ] 写 `task-3-report.md`：核对结论 + 差异（如有）。

验证：基线文档引用的失败条数 = Task 2 实测失败数。

## Task 4 — 落盘 `docs/acceptance/baseline_v0.1.md`

Files:
- 写：`docs/acceptance/baseline_v0.1.md`（新建）
- 参照（格式）：`docs/acceptance/map_editor_mvp_check.md`

- [ ] 四节齐全：**B1 基线**（error/warning 数 + 采集日期 + 命令原文）、**B2 基线**（总数/通过/失败 + 失败清单与 §6.4 引用）、**B3/B4 口径**（命令原文 + 判据，照抄 §0.5）、**口径说明**（历史计数作废声明）。
- [ ] 明写「基线失败 = 已知债务，非豁免」，每条标注归属工作项。
- [ ] 格式沿用 `map_editor_mvp_check.md` 的对照表风格。
- [ ] 写 `task-4-report.md`。

验证：文档存在、四节齐全、含命令原文与采集日期。

## Task 5 — 回写与提交

Files:
- 改：`docs/长期执行计划_v0.1.md`（§5 `P0-C2` 行 ST/HIS + AGENTS §6.2）
- 改：`AGENTS.md`（§6.2 当前进度段）
- 写：`.superpowers/sdd/2026-10-10-p0-c2-acceptance-baseline/progress.md`（收口段）

- [ ] 跑 §0.5 B1–B4 全链（B1 起用刚固化的警告基线数对照）。
- [ ] 《长期执行计划》§5 `P0-C2` 行 `ST` → `done`；HIS 登记 spec / plan / 台账三路径。
- [ ] AGENTS.md §6.2 同步一句。
- [ ] `progress.md` 写收口段（结论 + 遗留 + spec 验收逐条对照）。
- [ ] 提交：`docs(baseline): P0-C2 验收基线固化 — B1 警告 N / B2 失败 M`。

验证：`git status --short` 无本任务无关改动；三路径可点击；台账无缺件。
