# 跨 PR 集成审查报告（PR #31 + #36 + #38）

审查时间：2026-11-26 13:10 (GMT+8)
审查方式：组合态构建 + 全量测试（Windows Debug/Release 本地实测）+ 静态对账。

## 一、组合态验证

**组合分支**：`feat/wp-30b-pr38-completion` @ `D:/kudi-projects/worldview-wt-pr38-completion`，
= PR #38 head `8b13c8a` + merge(PR #36 最终分支 `e2c3d4f`)，**merge-tree 与实际合并均零冲突**。

| 配置 | Domain | Application | Infrastructure | Presentation | Desktop | 合计 |
|---|---|---|---|---|---|---|
| Debug | 266 | 190 | 318 | 111 | 66（含 4 个注入测试） | **951/951 通过，0 失败，0 跳过** |
| Release | 266 | 190 | 318 | 111 | 62 | **947/947 通过，0 失败，0 跳过** |

组合态已提交（含 PR38 验收文档第 9 节），保留在本地 worktree 作为集成证据；**未推送**（避免 PR #38 的 diff 在 PR #36 合并前混入 PR #36 的变更）。

## 二、生命周期语义一致性对账

| 边界 | 结论 |
|---|---|
| Session ↔ Project | ✅ 一致。`MapWorkspaceSession.DisposeAsync` internal，唯一所有者 ProjectSession；`ProjectSession.CloseWorkspaceAsync` 原子 Close→Dispose→Remove；项目级关闭经统一裁决 2.3 收集-继续排空子工作区（LIFO）→ AssetStore → Database，阶段级完成标志防重复释放 |
| Session ↔ Viewport（Presentation Runtime） | ✅ 一致。Binding 由 VM 在打开登记时唯一创建（每工作区 1:1）；C1/C2 均"presentation 先于 Application"（ADR-10 §7.1）；Binding 关闭失败不阻断会话关闭（阶段隔离），会话关闭成功而 Binding 失败时集合不清空（P1-01 第 5 条不变量） |
| 关闭流程确定性 | ✅。关闭请求启动前可取消（P2-01）；进入 Closing 后释放到底；重复请求幂等短路（Closing/Closed）；失败保留进度可重试且不重复释放；C2 并发入口共享同一编排任务 |
| 异常不破坏资源释放 | ✅。所有生命周期收尾日志 best-effort（DEF-03）；事件通知逐订阅者隔离（至多一次）；收集-继续聚合传播原始异常 |
| Debug Fault Injection 不污染 Release | ✅。整类型 `#if DEBUG` 编译隔离；组合态 Release 产物无 `DebugCloseFault` 符号；Release 测试套件（62）不含注入测试 |
| 无虚假通过 | ✅。未删除任何测试；A-10 与 5 个编排测试的修改均为语义对齐（B3 去重 + 统一裁决 2.3），断言强度不减（多 Binding 顺序/聚合异常 2 内层/孤儿计数/OBJECT 探针全部保留）；未引入 skip/TODO 测试 |

## 三、PR 间重叠与顺序

- PR #36 与 PR #38 文件集不相交（`Sessions/`+`ViewModels/` vs `Rendering/DebugCloseFault`+`ProjectWindow`+workflow），merge-tree 及实际合并零冲突。
- **建议合并顺序**：PR #36（先）→ PR #38（后，base 改指 master）。PR #38 合并前需用户把 base 从 `feat/wp-30b-phase3a-lifecycle` 改为 master（`gh` 未认证，本机无法代执行）。
- PR #38 的 CI workflow 分支列表新增了 stacked 分支名，合并后属无害冗余，可在后续清理（P3，不阻塞）。

## 四、已识别的待决事项（无 P0/P1）

| 级别 | 事项 |
|---|---|
| P2（待用户复核） | 项目级关闭语义按统一裁决 2.3 收敛（详见 PR-36 报告 §2.2）：子工作区失败不再阻断兄弟工作区与 AssetStore/Database 释放。若用户要求恢复 PR #34 fail-fast，需回改 `ProjectSession.ExecuteCloseAsync` 与 A-10 断言 |
| P2 | PR #38 base 分支指向已合并的 PR #31 分支，需用户在 GitHub UI 改为 master |
| P3 | CI workflow 中 stacked 分支名合并后可清理 |
| P3 | C1（关闭单个工作区命令）路径上 Binding 关闭异常直接逃逸命令 Task（PR #31 既有行为，非本次引入）；人工验收若命中可另行记录缺陷（PHASE3A-MANUAL-ACCEPTANCE §7 已有对应说明） |

**结论：无未解决的 P0/P1/P2 阻塞项**（上表 P2 为待用户裁决/操作项，非实现缺陷）。

## 五、测试矩阵执行情况

| 要求 | 状态 | 说明 |
|---|---|---|
| Windows Debug | ✅ 本地 951/951（组合态）、947/947（PR36 分支）、57/57 Desktop（PR38 分支） | MTP 实测 |
| Windows Release | ✅ 本地 947/947（组合态）等 | MTP 实测 |
| macOS Debug | 🟡 以 GitHub Actions CI 为准 | PR #36 head `e2c3d4f`：4/4 绿（含 macOS Debug）；PR #38 head `d7cb7e0`：进行中；本机无 macOS 硬件，真实桌面验收不可替代 |
| macOS Release | 🟡 以 GitHub Actions CI 为准 | 同上 |
| 专项测试 | ✅ 生命周期/编排/故障注入专项全部通过 | ProjectSessionWorkspaceLifecycleTests / MapWorkspaceSessionLifecycleTests / ProjectCloseOrchestrationTests / DebugCloseFaultTests |

详见 ACCEPTANCE-RESULTS.md。
