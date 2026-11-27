# PR #36 实现交付报告

PR：feat(wp-ui-04b): Map Workspace Session lifecycle（WP-UI-04B）
分支：`feat/wp-ui-04b-map-session-lifecycle`
工作分支/工作区：`feat/wp-ui-04b-pr36-completion` @ `D:/kudi-projects/worldview-wt-pr36-completion`（独立 worktree）

## 一、最终状态

| 项目 | 值 |
|---|---|
| PR #36 最新 HEAD | `e2c3d4f`（已推送至 PR head 分支；旧 head `0479ff5`） |
| 提交结构 | `ba2c18d`（合并 origin/master `8f11250`，解决 4 处冲突）→ `eec99f0`（R3 测试对齐 + 文档）→ `e2c3d4f`（.gitignore 根级 .testtmp） |
| CI | 推送后自动触发（Win/macOS × Debug/Release）；推送前本地等效验证全部通过 |
| 测试（Windows Debug，本地实测） | **947/947 通过，0 失败，0 跳过**（Domain 266 + Application 190 + Infrastructure 318 + Presentation 111 + Desktop 62） |
| 测试（Windows Release，本地实测） | **947/947 通过，0 失败，0 跳过** |
| Draft 状态 | 维持 Draft（合并授权归用户） |

## 二、主要缺口与修复：与已合并 master（PR #31）的集成

审计发现 PR #36 分支基于 `7b03057`（PR #35），**不含 PR #31 的实现**，且 merge-tree 实测与最新 master 存在 4 处冲突文件。这是本阶段的核心工作。

### 2.1 冲突解决（逐文件裁决依据）

| 文件 | 裁决 |
|---|---|
| `MapWorkspaceSession.cs` | 保留 04B-R1 设计：`internal DisposeAsync`（ProjectSession 唯一所有者）+ `_closeCompleted`/`_disposeCompleted` 分步标志。master 侧会话级 `DisposeCount`/`_disposed` 无任何测试消费者，被 04B 标志体系取代；其中 `DisposeCount` 计数接缝在释放步骤真实成功后恢复（仅真实 CTS Dispose 成功后递增），供 PR #31 的 `ProjectCloseOrchestrationTests` 直接计数断言使用 |
| `ProjectSession.cs` | 保留 04B B2（真实 MapName 校验）+ B3（早检查 + 锁内权威复查同图去重）；恢复 PR #31 的 `WorkspaceCreatedHookForTests` 测试接缝（`ProjectCloseOrchestrationTests` 在锁内捕获实例） |
| `ProjectWorkspaceViewModel.cs` | **合并两侧语义**：master 的 C2 竞态防护（`_lifecycleGate` 注册前复查、Binding 登记、`DiscardOrphanWorkspaceAsync` 孤儿回收）+ 04B B4（VM 消费 `CloseWorkspaceAsync` 布尔结果、仅成功移除 UI 集合、永不调用 `DisposeAsync`）。孤儿回收改为经会话侧原子关闭（Close→Dispose→Remove），不再直接 Dispose——与 R1 P2-02 的 internal 边界一致 |
| `Testing/Fakes.cs` | 双方测试替身全部保留（`StubContextResolver` + `FakeWorkspaceContextResolver` + `FakeAppLog`） |

### 2.2 语义收敛裁决（P1 级，已在文档留痕，建议用户复核）

**项目级关闭语义**：PR #36 分支沿用了 PR #34 的 fail-fast 语义（子工作区关闭失败 → 兄弟工作区不触及、AssetStore/Database 不释放）；PR #31（已合并基线）按 **WP-30B 统一裁决 2.3** 演进为收集-继续（失败不阻断其他安全清理阶段，阶段级完成标志 + 重试只补未完成步骤）。

**裁决：采用 PR #31 基线语义（统一裁决 2.3）**，理由：
1. 任务书明确"PR #31 已经合并，是当前生命周期实现的基线"。
2. 统一裁决 2.3 是用户既有裁决，非本阶段新决策。
3. 关键不变量两侧均保留："子工作区失败阻止项目 Closed"（CloseFailed + 诊断 + 失败工作区保留待重试）、"重试不重复释放已完成步骤"。

留痕：`acceptance-criteria.md` R3 注记 + A-10 行更新；`IMPLEMENTATION-LOG.md` R3 条目。

### 2.3 测试对齐（非降低断言）

master 的 `ProjectCloseOrchestrationTests` 多工作区场景原本**用同一张地图打开 2 次**（B3 之前的合法行为）。合并后 B3 早检查将其短路为复用既有工作区，导致 5 个测试失败（2 个超时 + 索引越界 + 聚合异常类型 + 兄弟工作区被关）。修复方式：测试数据改用**两张不同地图**——多 Binding 顺序、聚合异常（2 个 InnerException）、在途打开抢救、孤儿清理的覆盖全部保留。A-10 兼容性测试按 2.2 的收敛语义更新断言（不变量保留）。

## 三、任务书特别检查项结论（第四节 5 项）

| # | 检查项 | 结论 |
|---|---|---|
| 1 | Session 已关闭但异步操作仍修改状态 | ✅ 未发现。`SwitchContextAsync` 晚到达结果在锁内按 `_state != Ready` 丢弃；派生工作经 CTS 取消，关闭后 token 访问抛 `ObjectDisposedException`（测试有探针） |
| 2 | 旧 Session 延迟回调污染新 Session | ✅ 未发现。B3 去重使同图只有一个活动 Session；在途打开经"注册前复查 + 孤儿就地回收"覆盖（专项测试）；无跨 Session 事件订阅 |
| 3 | 资源释放失败导致其他资源未释放 | ✅ 未发现。VM C2 排空阶段隔离（单 Binding 失败继续其余）；ProjectSession 收集-继续覆盖 AssetStore/Database 阶段；工作区内部分步标志保证重试不重复释放 |
| 4 | 异常被吞但状态已不一致 | ✅ 未发现。单工作区关闭失败 → CloseFailed + `CloseFailureDiagnostic` 可观察；VM 失败路径保留集合项与重试入口；事件通知逐订阅者隔离为**有意设计**（fire-and-forget 收尾不得被订阅者异常阻断，PR #31 已裁决） |
| 5 | 仅单元测试、缺真实桌面验证的关键路径 | 🟡 A-4/A-12~A-15 与 M-1~M-12 属 WP-UI-04C/04D（窗口层），**未开始**——PR #36 明确 out-of-scope，不算 PR #36 缺口；已在 ACCEPTANCE-RESULTS 记 NOT_RUN |

## 四、范围外事项（本阶段明确不做）

- WP-UI-04C（MapWorkspaceWindow/Registry、Desktop 窗口去重）、04D（人工验收清单执行）。
- 地图渲染/制作/Renderer/Authoring——任务书禁止，未触碰。
- PR 合并与 Draft 摘除——由用户执行。

## 五、遗留事项

1. CI 结果需在 GitHub Actions 上确认（推送于 2026-11-26 ~12:55，4 matrix 全绿为通过标准）。
2. macOS Debug/Release 只能由 CI 覆盖（本机为 Windows），结果以 CI 为准。
3. 语义收敛裁决（2.2）如用户否决（即要求恢复 PR #34 fail-fast），需要回改 `ExecuteCloseAsync` 与 A-10——建议在合并前明确。
