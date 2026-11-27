# PR #36 / PR #38 初始状态审计

审计时间：2026-11-26 12:30 (GMT+8)
审计方式：GitHub API 实时查询 + 本地 `git fetch` 后实际检查（不依赖旧交付报告）。

## 一、仓库与分支基线

| 项目 | 状态 |
|---|---|
| origin/master HEAD | `8f11250` — Merge pull request #31（2026-11-26T04:20:39Z 合并） |
| PR #31 | **已合并**（closed, merged 2026-11-26T04:20Z），head `b7e4045`，CI 4/4 绿（Win/macOS × Debug/Release） |
| PR #34 / #35 | 已合并（`8bdbad1` / `7b03057`），位于 PR #36 分支的历史基线内 |
| 本地 master | 落后远端（本地在 `7b03057`，缺 PR #31 合并），仅作参考，不在本地 master 上操作 |
| 主工作区未提交内容 | `WorldView/AGENTS.md`（M）、`.workbuddy/`、`docs/implementation/wp-ui-03-project-workspace-audit/`（untracked）——**不触碰** |

## 二、PR 状态对照表

| 项目 | PR #36 | PR #38 |
|---|---|---|
| 当前状态 | Open，**Draft**，无 review、无评论 | Open，**Draft**，无 review、无评论 |
| 标题 | feat(wp-ui-04b): Map Workspace Session lifecycle | WP-30B Phase 3A Debug fault injection (Draft) |
| 最新 HEAD | `0479ff5`（feat/wp-ui-04b-map-session-lifecycle） | `8b13c8a`（feat/wp-30b-phase3a-debug-fault） |
| 目标分支（base） | master @ `7b03057`（已过期，master 已推进到 `8f11250`） | **feat/wp-30b-phase3a-lifecycle @ `5cef2ea` —— 该 base 分支属已合并的 PR #31，需要改 base 为 master** |
| CI | 4/4 绿（windows/macos × Debug/Release，head `0479ff5`） | 4/4 绿（head `8b13c8a`；历史累计 8 条含 stacked matrix） |
| Review 状态 | 无任何 review 记录 | 无任何 review 记录 |
| 未解决讨论 | 无（issue comments 为空，无 review threads） | 无 |
| 已实现范围 | B1 状态机（Created/Opening/Ready/Closing/CloseFailed/Closed，分步释放+重试）、B2 真实 MapName、B3 同图去重、B4 原子关闭（Close→Dispose→Remove 双成功才移除）、B5 守卫；R1 修复两项 P2（取消语义、Dispose 接缝）；R2 文档一致性；自述 900/900 测试通过 | `DebugCloseFault`（Debug-only 一次性关闭故障注入，68 行）、`MapViewportBinding` 接入（17 行改动）、`ProjectWindow.axaml.cs` 手动验收入口（45 行）、CI workflow 加 stacked matrix、`DebugCloseFaultTests`（327 行） |
| 未实现范围 | A-4 / A-12~A-15 属 WP-UI-04C（窗口层：MapWorkspaceWindow/Registry、Desktop 窗口去重），PR 明确 out-of-scope 且未开始 | base 分支错误（指向已合并的 #31 分支）；PR body 仍写"Draft，请勿合并"，目标定义不完整 |
| 与 master 合并性 | **4 个文件冲突**：`MapWorkspaceSession.cs`、`ProjectSession.cs`、`ProjectWorkspaceViewModel.cs`、`Testing/Fakes.cs`（merge-tree 实测；PR #31 改了同一批文件） | merge-tree 实测**与 master 干净合并**（b7e4045 已在 master 历史中，PR diff 实际只剩 2 个自有提交） |
| 阻塞项 | ① 与 master 的 4 处冲突必须解决并重跑全量测试；② 分支基于过期 master，未含 PR #31 的 Viewport/Presentation Runtime 实现——"与 PR #31 生命周期集成"无法在当前分支上验证 | ① base 指向已合并分支（需改 base 为 master 或等价 rebase）；② 目标语义需按任务书对齐（Debug/Release 边界、确定性测试、生产代码零污染） |
| 下一步 | 建独立 worktree → 分支基于 `0479ff5` 合并 origin/master → 解决 4 处冲突（逐冲突裁决，保留 PR #31 语义 + PR #36 B1-B5 语义）→ 全量测试 → 生命周期专项审计（任务书第四节的 5 项特别检查） | 建独立 worktree → 分支基于 `8b13c8a` → 审计 Debug/Release 边界与确定性 → 补齐测试缺口 → 推送后改 base 为 master |

## 三、依赖关系与任务边界判定

1. **PR #36 ↔ PR #38 实现重叠有限**：PR #36 改 `Application/Sessions`（MapWorkspaceSession、ProjectSession）与 ViewModel；PR #38 改 `Desktop/Rendering`（MapViewportBinding）与 ProjectWindow。两者不同层，无直接代码冲突。
2. **PR #36 的 `DisposeFailureHookForTests`（internal 测试接缝，CTS Dispose）与 PR #38 的 `DebugCloseFault`（MapViewportBinding 关闭故障）是两个独立故障注入面**。PR #38 完成时需结合 PR #36 最终实现检查测试缺口（任务书第三节要求）。
3. **合并顺序建议**：PR #36 先合并（解决冲突 + 集成验证），PR #38 随后（改 base 后与两者组合做集成审查）。若顺序颠倒，PR #36 合并时仍需再对账一次。
4. **PR #38 base 修正**：`b7e4045` 已在 master 历史内，GitHub 上改 base 为 master 即可，无需 rebase（merge-tree 已验证干净）。改 base 需仓库写权限 API 调用，若无凭据则列为待用户操作项。
5. **生命周期语义依赖**：PR #36 的 ProjectSession 关闭编排（Close→Dispose→Remove）建立在 PR #31 的"订阅者异常隔离、通知至多一次"（`b7e4045`、`07eb375`）之上，但 PR #36 当前分支不含这些提交——合并 master 后需确认异常隔离语义与 B4 原子关闭语义组合正确。

## 四、本地 worktree 清单（现状）

| 路径 | 分支 | 说明 |
|---|---|---|
| D:/kudi-projects/worldview | feat/wp-ui-04b-map-session-lifecycle（= PR #36 head `0479ff5`） | 主工作区，含未提交文件，**本次工作不在此分支上直接改代码** |
| D:/kudi-projects/worldview-wp30b-debug-fault | feat/wp-30b-phase3a-debug-fault（旧 `bb0a9a4`） | 旧 worktree，落后 PR #38 head |
| D:/kudi-projects/worldview-wp30b-p1-fix | fix/wp-30b-p1-review-fixes | 历史遗留 |
| D:/kudi-projects/worldview-wp30b-ui01 | detached `5cef2ea` | 历史遗留 |
| D:/kudi-projects/worldview-wt-phase3a | feat/wp-30b-phase3a-lifecycle（= PR #31 分支） | 历史遗留 |
| D:/kudi-projects/worldview-wt-pr31-p2fix | detached `b7e4045` | 历史遗留 |

计划新增（遵守独立 worktree 约束）：
- `../worldview-wt-pr36-completion`，新分支基于 `origin/feat/wp-ui-04b-map-session-lifecycle`（PR #36 head），完成合并 master 与审计后推送回 PR #36 head 分支。
- `../worldview-wt-pr38-completion`，新分支基于 `origin/feat/wp-30b-phase3a-debug-fault`（PR #38 head），完成审计与补强后推送回 PR #38 head 分支。

## 五、审计初步结论（细节在各自实现报告中展开）

- PR #36 实现完成度较高（B1-B5 + R1/R2 已做，CI 绿，测试自述 900/900），**主要缺口是与已合并 master（PR #31）的集成**：4 处冲突 + 分支未含 PR #31 语义。
- PR #38 实现较小（单文件故障注入 + 接入 + 测试），CI 绿，**主要缺口是 base 指向错误与 Debug/Release 边界、确定性、生产污染的正式审计**。
- 两 PR 均为 Draft 且无 review，合并授权归用户，本次工作止步于"实现完成 + 交付报告"。
