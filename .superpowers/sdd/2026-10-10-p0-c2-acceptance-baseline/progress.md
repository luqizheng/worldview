# SDD ledger — plan: docs/superpowers/plans/2026-10-10-p0-c2-acceptance-baseline.md

Branch: `deepseek-build`. BASE before Task 1: `1098e5340ada997ec4bb5429cb8d7b3d6a3b1b0f`.
Spec (binding authority): `docs/superpowers/specs/2026-10-10-p0-c2-acceptance-baseline.md`（轻档）。
档位：**轻档**（不改 `scripts/` / `scenes/` / `crates/` / `tests/`）。

## Pre-flight scan (run before Task 1)

**无接口交叉。** 本项不产出代码、不改变任何函数签名或类型；五个 task 全是「命令输出 → 文本记录」的单向数据流，文件写入互不重叠：

| Producer → Consumer | 交叉物 | Finding |
| --- | --- | --- |
| T1 → T4 | B1 警告数/分组 | OK（T4 只读 T1 的 report） |
| T2 → T3 | 失败清单 | OK（T3 用 T2 的清单核对 §6.4） |
| T2 → T4 | B2 计数 | OK |
| T3 → T4 | §6.4 引用锚点 | OK |
| T4 → T5 | 基线文档路径 | OK（T5 把它写进 §5 HIS） |
| T5 → 既有文件 | `docs/长期执行计划_v0.1.md` §5 / `AGENTS.md` §6.2 | **并发风险** → Ruling R2 |

共享文件告警：`docs/长期执行计划_v0.1.md` 已被多个会话并发编辑（2026-10-09 ~ 10-12 至少两次覆写）。T5 写入该文件前必须重读实况并复核 `P0-C2` 行未被他人改动。

## Rulings

- **R1 — 采集与落盘必须同一会话内完成。** 本仓多次出现「读到的数字与实况不符」（§1 快照、§6.1-4 计数、P0-C1 状态均为实例）。T1/T2 采集后若跨会话再落盘，数字会漂移。Cost if wrong：基线文档数字过期，需重跑采集。
- **R2 — T5 改计划文件前先重读。** 并发会话已两次覆写该文件（含本轮我的编辑一度丢失）。T5 必须以「Read 最新内容 → Edit 精确匹配」方式改，禁止用记忆中的文本做 Edit。Cost if wrong：Edit 失败（可见）或被并发覆写（需重做）。
- **R3 — 基线失败不等于豁免。** §6.4 的 7 条失败是**已知债务**，各自归属到修复工作项；基线文档必须显式写明，防止后续会话把「基线红」当默认通过。Cost if wrong：7 条债务被永久固化。
- **R4 — 台账目录本地忽略。** `.superpowers/sdd/.gitignore` 为 `*`，本目录内容不入版本库。若用户要求跨机器可查，需单独决策放开（见 §6.3 登记）。Cost if wrong：换机/clone 后台账丢失，但 spec 与 plan 在库内仍可查。

## Ledger

| Task | 状态 | 产出 | commit |
| --- | --- | --- | --- |
| T1 B1 警告基线采集 | ✅ 完成 | `task-1-report.md` | — |
| T2 B2 测试基线采集 | 待开工 | `task-2-report.md` | — |
| T3 失败定性核对（§6.4 已完成，只核对） | 待开工 | `task-3-report.md` | — |
| T4 落盘 `docs/acceptance/baseline_v0.1.md` | 待开工 | 基线文档 | — |
| T5 回写与提交 | 待开工 | §5 ST=done + progress 收口段 | — |

### T1 结果摘要（2026-10-10）

- 命令：`dotnet build WorldView.sln -c Debug --no-incremental`
- **B1 基线 = 0 error / 184 warnings**（全量口径）
- 构成：CS2002 ×137（`WorldView.csproj` 源文件重复指定，配置缺陷）+ 其余 11 类 ×47
- **新增 Ruling R5**：警告数必须用 `--no-incremental` 采集——增量构建只报被重编项目的警告，同一棵树实测增量 19 / 全量 184，差 10 倍不可比。已回写 §0.5-B1。
- **新增发现**：CS2002 重复编译项缺陷（137 文件）→ 登记《长期执行计划》§6.3，超出本项范围，不在 P0-C2 内修。

## 收口段（T5 完成后填写）

（待填：结论 / 已知遗留 / spec 验收逐条对照）
