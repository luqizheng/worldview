# 最终报告：PR #36 / PR #38 完成情况与地图工作启动门槛判定

日期：2026-11-26
交付目录：`docs/implementation/pr36-pr38-completion/`（本报告 + INITIAL-STATUS + PR-36/PR-38 报告 + INTEGRATION-REVIEW + ACCEPTANCE-RESULTS）

## 一、结论先行

| 问题 | 结论 |
|---|---|
| PR #36 是否完成 | ✅ **实现完成**。与已合并 master（PR #31）完成集成（4 处冲突解决 + 语义收敛 + 测试对齐），新 head `e2c3d4f` 已推送，CI 4/4 绿，Windows 本地 Debug/Release 各 947/947 |
| PR #38 是否完成 | ✅ **实现完成**。审计通过（入口受控、Debug/Release 编译隔离、确定性、无生产污染），唯一缺口（验收文档未记载注入入口）已补写，新 head `d7cb7e0` 已推送 |
| 两个 PR 的最新 HEAD | PR #36 = `e2c3d4f`；PR #38 = `d7cb7e0`（均已推送至各自 head 分支） |
| 测试结果 | Windows 本地实测：PR36 分支 947/947（Debug+Release 双配置）；组合态 951/951（Debug）/947/947（Release）；PR38 分支 Debug 57/57、Release 53/53 Desktop.Tests |
| CI 状态 | **两个 PR 最新 HEAD 均 4/4 全绿**（Win/macOS × Debug/Release）：PR #36 `e2c3d4f`、PR #38 `d7cb7e0`（13:20 查证） |
| Windows 人工验收 | NOT_RUN（沙箱无法启动 GUI；自动化等效覆盖已通过） |
| macOS 人工验收 | NOT_RUN（无 macOS 硬件；CI 覆盖构建/测试维度） |
| 尚未解决的问题 | 见 §三（无 P0/P1；2 项 P2 需用户操作/裁决，2 项 P3） |
| 地图工作启动门槛 | **未满足（4/8）**，见 §四 |

## 二、本次工作内容摘要

1. **真实状态审计**（INITIAL-STATUS.md）：GitHub API 实时核实——PR #31 已于 2026-11-26T04:20Z 合并；PR #36/#38 均 Open Draft、无 review；PR #36 基于 `7b03057` 不含 PR #31 实现，与 master 有 4 处冲突；PR #38 base 指向已合并分支。
2. **PR #36 集成**（独立 worktree `worldview-wt-pr36-completion`）：
   - 解决 4 处冲突（MapWorkspaceSession / ProjectSession / ProjectWorkspaceViewModel / Fakes），逐文件裁决依据见 PR-36 报告 §2.1；
   - 恢复 PR #31 的两个测试接缝（`WorkspaceCreatedHookForTests`、会话级 `DisposeCount`——后者仅在真实 CTS Dispose 成功后递增）；
   - VM 合并两侧语义：PR #31 的 C2 竞态防护/Binding 登记/孤儿回收 + PR #36 的 B4 原子关闭（VM 不再调用 DisposeAsync，孤儿回收改走会话侧原子关闭）；
   - 语义收敛裁决：项目级关闭采用统一裁决 2.3（收集-继续），"子失败阻止项目 Closed""重试不重复释放"两项不变量保留——**P2，建议用户复核**（PR-36 报告 §2.2）；
   - 测试对齐 6 处：5 个编排测试改用不同地图构造多工作区（B3 同图去重使同图重开短路，原数据构造失效），A-10 断言按收敛语义更新；测试意图与断言强度不减；
   - 文档留痕：acceptance-criteria.md（R3 注记 + A-10 行）、IMPLEMENTATION-LOG.md（R3 条目）。
3. **PR #38 审计与补强**（独立 worktree `worldview-wt-pr38-completion`）：
   - 8 项审计全部 PASS（见 PR-38 报告 §二）：`#if DEBUG` 编译隔离经 Release DLL 符号扫描实证（零命中）；
   - 补写 `PHASE3A-MANUAL-ACCEPTANCE.md` §9：注入入口用法/一次性语义/通过标准/失败判定（此前文档完全未记载该入口）；
   - 与 PR #36 的故障覆盖对账：释放失败（PR36 接缝）/关闭失败（PR38 真实调用缝）/项目关闭失败（PR36 收集-继续）组合后无缺口。
4. **跨 PR 集成**：PR38 分支合并 PR36 最终分支零冲突；组合态 Debug 951/951、Release 947/947 全绿；生命周期语义逐项对账一致；无虚假通过（未删测试/未降断言/无 skip）。

## 三、尚未解决的问题

| 级别 | 事项 | 责任方 |
|---|---|---|
| P2 | PR #38 base 改指 master（现指向已合并的 PR #31 分支）。`gh` 未认证无法代执行；GitHub PR 页面一键操作，merge-tree 已验证改 base 后干净合并 | 用户 |
| P2 | 项目级关闭语义收敛裁决复核（统一裁决 2.3 vs PR #34 fail-fast，见 PR-36 报告 §2.2）。若否决需回改 ExecuteCloseAsync 与 A-10 | 用户裁决 |
| P3 | CI workflow 的 stacked 分支名合并后清理；C1 路径 Binding 异常逃逸命令 Task（PR #31 既有行为）如人工命中另行记录 | 用户后续安排 |

## 四、地图工作启动门槛判定

| # | 门槛条件 | 状态 |
|---|---|---|
| 1 | PR #36 全部目标实现完成 | ✅ |
| 2 | PR #38 全部目标实现完成 | ✅ |
| 3 | 两个 PR 的 CI 全部通过 | ✅（两 PR 最新 head 均 4/4 绿） |
| 4 | 两个 PR 的必要人工验收完成 | ❌ NOT_RUN（Windows GUI 受沙箱限制、macOS 无硬件；且 04C/04D 窗口层人工清单本就未到执行阶段） |
| 5 | 跨 PR 集成审查通过 | ✅ |
| 6 | 无未解决的 P0/P1/P2 问题 | 🟡 无实现缺陷，但存在 2 项待用户操作的 P2（base 改指、语义裁决复核） |
| 7 | 两个 PR 的交付报告齐全 | ✅ |
| 8 | master 与最终实现的依赖关系明确 | ✅（合并顺序建议：#36 → 改 base → #38；零冲突已实证） |

**判定：4/8 满足，暂不满足启动门槛。** 剩余路径全部在用户侧：
1. 复核语义收敛裁决（PR-36 报告 §2.2）；
2. PR #38 改 base 为 master；
3. 等待/确认 PR #38 CI 全绿；
4. （可选，约 5 分钟）在 Debug 构建上按 `PHASE3A-MANUAL-ACCEPTANCE.md` §9 执行注入路径人工走查；
5. 决定是否摘除 Draft 并合并（合并授权在用户，本次未合并、未动 Draft）。

以上完成后即满足全部 8 项门槛，可向我提交地图工作启动裁决。

## 五、后续建议操作（按序）

1. 复核 PR-36-IMPLEMENTATION-REPORT.md §2.2 的语义裁决 → 接受或要求回改。
2. GitHub 上将 PR #38 base 改为 master → 确认 CI 4/4 绿。
3. （可选）按 §9 清单做注入路径人工走查（Windows Debug 构建）。
4. 逐个摘除 Draft、Review、合并：先 #36 后 #38。
5. 合并完成后向我确认，我将提交地图工作启动建议（WP-RA-00 / WP-40 / ME-08 的启动规划，届时再议）。

**约束遵守声明**：本次未启动任何地图相关工作（WP-RA-00/WP-40/ME-08/地图渲染/Authoring 均未触碰）；未合并任何 PR；未修改主工作区已提交外的任何文件（主工作区仅新增本交付目录 6 份报告，均为新文件）；全部代码改动在独立 worktree 进行；未删除任何分支或测试资产。
