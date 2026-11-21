# CD-01-F1 — Database Access Coordination & Persistence Integrity Verification Report V0.1

- **Date:** 2026-11-22
- **Status:** 验证报告 — 结论 **PASS WITH CONDITIONS**（见 §15）
- **Upstream:** [CD-01 Implementation Report V0.1](CD-01_IMPLEMENTATION_REPORT_v0.1.md)
- **Repository:** `luqizheng/worldview` @ `master`

---

## 1. 审查范围与实际 Commit SHA

| 项 | 值 |
|---|---|
| 审计基线（CD-01 交付后 master） | `0d13b17`（docs: record final CD-01 commit SHAs after rebase） |
| 实施修复 commit | `4b0c322`（fix: unified connection access coordinator; lineage initial-own validation; integrity tests） |
| 验证对象 | CD-01 全部正式持久化边界：`SqlitePackageDatabase`、`SqliteCoreDomainDatabase`、Core Domain 适配器、`ProjectSession` 生命周期、Bootstrap/Migration 基础设施、`CoreDomainSchema` DDL、`BranchLineageResolver`/`WorkspaceContextRules` |
| 验证方式 | 全部基于当前 master 实际代码调用链审计 + 仓库外独立复现实验 + 真实 SQLite 测试（不引用历史报告） |

## 2. Connection Ownership 结构（审计事实）

1. **一个 `ProjectSession` 恰好一个 `SqlitePackageDatabase`，后者持有恰好一个 `SqliteConnection`**（`Pool=False`；`ProjectSession.cs:48`，`SqlitePackageDatabase.OpenAsync`）。
2. **创建者**：`SqlitePackageDatabase.OpenAsync`（PRAGMA `foreign_keys=ON`、`busy_timeout=5000`；调用方为 Open/Create 用例与 `CoreDomainProjectCreationService`）。
3. **释放者**：`ProjectSession.CloseAsync` → `Database.DisposeAsync()` → 连接释放（关闭永不删包）。
4. **Core Domain 适配器直接共享同一 Connection**：经 `ISqliteConnectionProvider` 取出裸连接。
5. **存在多个适配器分别包装同一 Connection**（审计确认的结构缺陷）：Desktop 组合根 `Program.cs:109-110` 中 `CoreDomainMapQueryService` 与 `CoreDomainWorkspaceContextResolver` 各自 `new SqliteCoreDomainDatabase(session.Database)`，**每个实例持有自己的 `_gate`**（修复前 `CoreDomainAdapters.cs:119,153`）；`CoreDomainProjectIdentityReader.ReadAsync` 每次调用再建新实例（`:85`）。
6. **修复前 Connection 的访问没有共同协调机制**（详见 §4）。

## 3. Adapter 与 Connection 的关系（修复前）

```
ProjectSession.Database (SqlitePackageDatabase ── _connection ── SqliteConnection)
 ├── _gate #1（包数据库自身操作；ExecuteMigrationAsync 事务体在锁外！）
 ├── SqliteCoreDomainDatabase#A（MapQueryService）── _gate #2（仅护自己）
 └── SqliteCoreDomainDatabase#B（ContextResolver）── _gate #3（仅护自己）
```

同一物理连接上并存 3+ 个互不相干的锁域；`GetOpenConnection()` 把裸连接交出后，所有者对其使用一无所知。

## 4. 原有锁机制的实际问题（含独立复现证据）

### 4.1 复现实验（仓库外独立工程，Microsoft.Data.Sqlite 11.0.0，Barrier 对齐、无 Sleep）

复刻"两个适配器各持独立 `_gate` 共享一连接"的原结构：

| 场景 | 结果 |
|---|---|
| Write∥Write（200 轮×2 线程） | **202/400 失败**：`SqliteException: 'cannot start a transaction within a transaction'` / `'not an error'`（≈50% 写入被破坏性拒绝） |
| Query∥Write（200 轮） | 本轮 0 读失败（SQLite serialized 模式下多活动语句"碰巧可用"——这解释了 CD-01 的 139 个串行测试为何全过） |
| Dispose∥在飞读（100 轮） | **99/100** 在飞操作撞上 `ObjectDisposedException: 'SQLitePCL.sqlite3'`（原生句柄级失控异常，非定义行为） |

结论：**缺陷真实且可稳定复现**，属潜伏数据竞争——当前唯一调用点 `ProjectWorkspaceViewModel`（UI 线程串行 await）掩盖了它，但 Task 形端口契约允许任意线程调用，任何后台读写路径立即触发。

### 4.2 逐项锁审计结论（修复前）

| 锁 | 保护范围 | 问题 |
|---|---|---|
| `SqlitePackageDatabase._gate` | 元数据/ledger/quick_check | `ExecuteMigrationAsync` 仅开头持锁，**迁移事务体完全在锁外**（原 `:251-295`）——迁移可与其他操作交错 |
| `SqliteCoreDomainDatabase._gate` ×N | 仅本实例操作 | 实例间互不相干；双写者命中 §4.1 场景 1 |
| `ProjectSession._stateGate` | 会话状态机 | 与数据库操作无协调：`CloseAsync` 直接 Dispose 连接，在飞适配器操作被从下面抽走连接 |
| `DisposeAsync` | 自身字段翻转 | 锁外 `connection.DisposeAsync()`：在飞读/写撞原生释放（§4.1 场景 3） |

具体风险清单（全部成立）：两 Query Service 并发 ✗、Query 与 Write 并发 ✗、双 Write 并发 ✗、事务执行期间其他 Adapter 进入 ✗、Dispose 与在飞操作竞争 ✗、Migration 与其他操作并发 ✗。

## 5. 选定的 V1 并发访问模型及理由

**模型：Connection Owner 提供统一 Connection Access Coordinator（任务 §4 推荐方案）。**

- 新增 `SqliteConnectionAccess`（`WorldView.Infrastructure/Persistence/SqliteConnectionAccess.cs`，internal）：独占持有唯一 `lock` 门 + 连接引用 + disposed 状态。
- **一个 SQLite 连接恰有一个协调器；所有使用该连接者共享它**：`SqlitePackageDatabase` 全部操作（含迁移事务与 Dispose）、全部 `SqliteCoreDomainDatabase` 实例、测试数据注入，一律经 `Execute<T>` 在单一临界区内完整执行（命令创建→执行→读取器物化→事务 Begin/Commit/Rollback 全程持锁）。
- **Monitor 同线程可重入** → `SqliteCoreDomainDatabase.InSnapshotScope<T>(work)`：多步读序列（如 `WorkspaceContextRules.TryResolve` 的 Map→根历史→世系链）组成单一原子快照，无需改动 Domain 端口。
- **Dispose 协调**：锁内翻转状态（此后入口立即 `ObjectDisposedException`，ObjectName=`SqliteConnectionAccess`，带中文消息——区别于旧模型原生失控异常）；锁外等待在飞操作退出后释放连接。**不存在连接提前释放**。
- **迁移遵循同一协调**：`ExecuteMigrationAsync` 事务整体进入临界区（修复锁外执行缺陷）。跨连接（同一文件两个实例）由 SQLite 文件级写锁 + ledger 主键唯一性 fail-closed 兜底（测试验证）。
- 不引入连接池/WAL/分布式组件；连接策略（Pool=False、FK=ON、busy_timeout=5000、journal 默认）与 CD-01/ADR-0003 完全一致。

**理由**：V1 无删除/更新路径（结构 append-only），单连接串行即满足全部正确性需求；重入快照以约 60 行代码消除撕裂读，成本远低于跨调用 Unit-of-Work 抽象。串行吞吐在桌面单用户场景下不是约束。

**Domain 侧修复**：`BranchLineageResolver.TryResolve` 补齐**初始 Branch 自身一致性校验**（自有历史必须存在且 `branch_owned`；此前只校验上溯中间节点，任务 §6.3 指出项）。此为校验加强，不改变 ADR-002/005 语义。

## 6. 修改的文件与代码（commit `4b0c322`）

| 文件 | 变更 |
|---|---|
| `Persistence/SqliteConnectionAccess.cs` | **新增**：统一协调器（Open/Execute/Execute(Action)/DisposeAsync） |
| `Persistence/SqlitePackageDatabase.cs` | 组合协调器；全部操作改经 `_access.Execute`；`ExecuteMigrationAsync` 事务整体入临界区；`DisposeAsync` 委托协调器；`ISqliteConnectionProvider.GetAccess()` |
| `CoreDomain/SqliteCoreDomainDatabase.cs` | 删除实例级 `_gate`；所有方法体委托 `_access.Execute`（写事务含提交/回滚整体在临界区）；新增 `internal InSnapshotScope<T>`；接口 `ISqliteConnectionProvider` 改为暴露协调器 |
| `CoreDomain/CoreDomainAdapters.cs` | `CoreDomainWorkspaceContextResolver.ResolveAsync` 的多步解析包入 `InSnapshotScope`（原子快照） |
| `Domain/Maps/BranchLineage.cs` | 初始 Branch 自有历史存在性 + `branch_owned` 校验（fail-closed） |
| 测试 | 新增 `PersistenceConcurrencyTests.cs`（16）、`PersistenceDomainIntegrityTests.cs`（15）、`BranchLineageTests.cs` +2；`CoreDomainPersistenceTests.cs` 原始 SQL 注入改走协调器 |

**公开接口兼容性**：`IPackageDatabase`、`ICoreDomainDatabase`、四个适配器构造签名、组合根接线全部不变（`Program.cs` 零改动）。变化仅 `ISqliteConnectionProvider`（Infrastructure internal，测试程序集经 `InternalsVisibleTo` 同步更新）。未降低任何既有测试覆盖。

## 7. 并发测试结果（Shared Connection，§5.1）

测试类 `PersistenceConcurrencyTests`（真实 SQLite 文件 + 真实适配器，Barrier 对齐起跑，无 Sleep）：

| 测试 | 结果 |
|---|---|
| `Concurrent_CompositionRootAdapterPair_QueriesSucceedOnSharedConnection`（组合根同构 MapQuery∥Resolver ×150 轮） | PASS |
| `Concurrent_TwoWritersOnSameConnection_BothCommitFully`（双 Core 实例并行建分支 ×100 轮；旧模型 ≈50% 失败场景） | PASS：200/200 全部提交，history/branch 严格成对 |
| `Concurrent_InsertProjectRace_ExactlyOneWinsWithDefinedDomainFailure` | PASS：恰一胜者，败者得 `CoreDomainDataException` |
| `Concurrent_ReadersAndWriters_NeverObservePartialMapStructure`（1 写 3 读×120 观测） | PASS：任何可见 Map 根历史同时可见；终态 4 表一一对应 |
| `Concurrent_PackageDbOperationsAndCoreAccess_ShareSingleCoordinator`（ledger∥integrity∥core 查询） | PASS |

无 connection concurrent-use 错误、无未预期事务冲突、无部分写入、无数据损坏、终态满足领域约束。

## 8. Transaction 测试结果（§5.2）

| 测试 | 结果 |
|---|---|
| `Transaction_Commit_PersistsWholeMapStructure` | PASS（4 表同事务全量落盘） |
| `Transaction_MidwayUniqueViolation_RollsBackEntirelyAndConnectionStaysUsable` | PASS：第 3 语句 UNIQUE 冲突 → 整体回滚（无半成品）、全表计数与操作前一致、异常原样传播（不谎报成功）、连接可继续用（事务资源已释放） |
| `Transaction_InProgress_BranchOwnedHistoriesAlwaysPairedWithBranch` | PASS：写事务进行中其他读（同快照）永不观察"history 已入、branch 未入"中间态 |
| `Transaction_RollbackOnDuplicateProject_LeavesDatabaseIdentical` | PASS：全表快照逐表一致 |

隔离边界说明：协调器串行化使外部操作无法切入在飞事务（结构性保证，由 §7 双写测试行为验证）；跨调用多步复合操作（如 `BranchService.CreateBranch` 的读-校验-写序列）依赖 V1 append-only 语义（见 §14 条件 1）。

## 9. Dispose Race 测试结果（§5.3）

| 测试 | 结果 |
|---|---|
| `Dispose_Afterwards_AdapterOperationsRefusedWithCoordinatorException` | PASS：关闭后 MapQuery/Resolver/Core 全部以 `ObjectDisposedException`（ObjectName=`SqliteConnectionAccess`）入口拒绝；重复关闭幂等；文件完整性 ok 且已提交数据完好 |
| `Dispose_DuringInFlightOperations_OutcomesAreControlledAndFileStaysIntact`（4 工人×25 轮读/写 + 并发 Dispose） | PASS：结果仅"完整完成"或"协调器入口拒绝"两类；无原生句柄异常；`quick_check` ok；branch 与 branch_owned history 严格成对（无半完成写入） |
| `Dispose_ConcurrentDoubleDispose_CompletesWithoutError` | PASS：并发双关闭均无异常 |

## 10. Migration 测试结果（§5.4）

| 测试 | 结果 |
|---|---|
| `Migration_PendingApplied_ConcurrentlyWithCoreReads_BothSucceed`（pending 迁移应用 ∥ 80 轮核心读） | PASS：迁移成功（恰 1 条 pending）、读全部成功、ledger 5 行无重复、integrity ok |
| `Migration_FailingScript_RollsBackSchemaAndLedger` | PASS：失败迁移整体回滚（探查表不存在、ledger 无记录）、bootstrap 报 InternalError、连接可继续用（core-domain 4 条随后正常补齐） |
| `Migration_TwoConnectionsSameFile_ConcurrentInitialize_AppliedExactlyOnce` | PASS：同文件两连接并发初始化，ledger 恰 4 行（`COUNT`==`COUNT(DISTINCT)`），至少一者成功，integrity ok |
| `Migration_SecondValidation_AppliesNothing` | PASS：已应用迁移不重复执行 |

既有迁移测试（fresh bootstrap 4 条、重开幂等、checksum 篡改/未知迁移 fail-closed）全部保留并通过。

## 11. Project Isolation 测试结果（§6.1/6.2）

| 测试 | 结果 |
|---|---|
| `CrossProject_Reads_ReturnNullOrRefusalOnEveryPort`（两独立包 A/B） | PASS：A 侧 FindMap/FindHistory/FindBranch/FindRootHistoryByMap/FindBranchByOwnHistory 拿 B 的 ID 全部 null；A 的 Resolver 对 B 的 map/branch/组合全部拒绝且不泄露存在性；B→A 反向同样拒绝；各自正向解析正常 |
| `CrossProject_Write_IsRejectedByCompositeSchemaConstraints` | PASS：伪造 B 项目行后，"A 项目声明 + B 的 map" 的 history 被复合 FK `(project_id, owner_map_id)` 拒绝；"A 项目 branch 引用 B 项目历史"被 `(project_id, parent_history_id)` 拒绝——直接传他 Project ID 绕过 Session 边界在数据库层不可行 |

Branch 归属（branch→map 经世系根唯一可达、跨 Map 根拒绝、悬空/无主/环拒绝）由 §12 覆盖。

## 12. Branch Lineage 测试结果（§6.3）

真实 SQLite（腐败数据经 FK 开启的协调器通道或显式 FK 关闭的裸连接注入，模拟外部工具破坏）：

| 场景 | 结果 |
|---|---|
| 三级链根→叶排序（B1,B2,B3；Depth=3） | PASS |
| planted 环（互为 parent，FK 满足） | PASS：拒绝，"环" |
| 悬空父历史（FK 关闭外部写入） | PASS：拒绝，"不存在" |
| branch_owned 历史无主 Branch | PASS：拒绝，"没有对应的 Branch（数据损坏）" |
| 终止于其他 Map 的根历史（同项目跨 Map） | PASS：拒绝，"跨地图" |
| **初始 Branch 自有历史缺失**（本次补齐的校验） | PASS：拒绝，"自有历史…不存在" |
| **初始 Branch 自有历史为 map_root**（本次补齐；schema 允许插入、服务路径造不出） | PASS：拒绝，"不是 branch_owned"；经 Workspace 端口同样拒绝 |
| 深度超限（planted 1001 级链） | PASS：拒绝，"深度超过上限" |

另：Domain 单元测试 `BranchLineageTests` 8/8（含 2 条新增初始一致性用例）。

## 13. Debug / Release Build 结果

| 配置 | 结果 |
|---|---|
| `dotnet build -c Debug` | 0 error；警告全部为既有 xUnit 分析器提示（xUnit1030/1031/1051 等，与基线一致，非编译器警告） |
| `dotnet build -c Release` | 0 error（全量重建 234 条警告，逐条核对全部为上述既有 xUnit 分析器代码，零编译器警告），Desktop 产物完整 |
| `dotnet test`（全解决方案） | **172/172 通过**（Domain 60 / Application 44 / Infrastructure 68；CD-01 原 139 全保留 + 新增 33），Debug 配置实测 |

## 14. Schema 约束审计（§6.4：DB 级 vs 服务级边界）

**数据库级强制（负向测试实证，`PersistenceDomainIntegrityTests.Schema_*`）**：
- FK：`core_map→core_project`；`core_history→core_project`；复合 `(project_id, owner_map_id)`、`(project_id, parent/own_history_id)`、calendar 三列复合；会话连接 `PRAGMA foreign_keys=1` 实测。
- CHECK：`history_kind` 词汇表 + kind/owner 组合；`parent_day≥0`、`offset∈[0,86399]`、`parent≠own`；`calendar_role` 词汇表；配置 origin/source_command 配对；effective_until 成对可空；effective_from 非负。
- UNIQUE/PK：实体主键；`(project_id, entity_id)` 系列；`UNIQUE(project_id, owner_map_id)`（每 Map 恰一根历史）；partial index `ux_core_calendar_default_per_map`（每 Map 恰一 default Calendar，索引存在性实证）；`(project_id, own_history_id)`。

**服务级强制（有意不在 DB 层，边界说明）**：
- 一库一项目（0/1/N 行）：SQLite 跨行 CHECK 不可表达（无子查询/无触发器策略），由 `InsertProject` 事务内 COUNT 检查 + 读取侧 `ProjectRules.ValidateIdentityRows` 分类 fail-closed（测试覆盖双路径）。
- 世系合法（无环/可达根/不跨 Map）：声明式不可表达，由 `BranchLineageResolver` + `BranchService.ValidateAncestry` 强制（§12 全场景实测，含外部工具植入数据的读取防御）。
- `lifecycle_status` 词汇表：P2-09-11 open gate（未冻结，刻意无 CHECK，CD-01 服务统一写 `'active'`）。

## 15. 最终验收建议

### 结论：PASS WITH CONDITIONS

核心数据安全已证明：单连接统一协调、事务原子/隔离、Dispose 无失控竞争、Project 隔离无漏洞、Branch/History 不变量（含初始一致性）在真实 SQLite 下全部成立，并有确定性测试与独立复现实验支撑。以下为明确、可接受且不影响 V1 正确性的条件：

| # | 条件 | 适用范围 | 后续处理 |
|---|---|---|---|
| 1 | **跨调用复合写序列**（`MapService`/`BranchService` 的读-校验-写跨多个临界区）依赖 V1 append-only 语义（无删除/更新路径，交错读至多"略旧"且不可能产生悬空/坏世系——FK+服务校验双兜底） | 引入删除/更新/改名等变更路径前 | PRA-01 或后续工作包若引入变更操作，须将复合写包入 `InSnapshotScope` 或引入显式 Unit-of-Work，并补充相应测试 |
| 2 | **协调器为单连接串行模型**：长事务会阻塞同会话其他数据库操作（含 Dispose 等待） | 单机桌面单用户 V1 | 若出现后台批量任务与交互争用，评估按任务粒度拆分事务；不引入多连接/WAL 除非有证明 |
| 3 | `SqliteConnectionAccess` 为 Infrastructure internal：新适配器必须经 `ISqliteConnectionProvider.GetAccess()` 接入，绕过即失去协调（测试注入同受此约束） | 后续所有新增数据库适配器 | 列入 Developer Handoff 红线；P2-09-12 Writer Coordinator 正式化时以此为实现基线 |
| 4 | P2-09-12（Writer Coordinator 完整证明）、WAL、备份/恢复等 open gate 状态不变，本任务未提前实施 | — | 维持 ADR-0003 / CD-01 open-gate 决议 |

无未解决的数据完整性或并发安全缺陷。
