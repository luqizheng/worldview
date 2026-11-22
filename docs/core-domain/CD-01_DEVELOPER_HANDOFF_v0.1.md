# CD-01 — Developer Handoff / Technical Notes V0.1

- **Date:** 2026-11-21
- **Audience:** CD-01 之后阶段的开发者（PRA-01 Presentation 集成、后续 Core Domain 写路径、AI Assistant 持久化）
- **Design:** [CD-01 Persistence Design V0.1](CD-01_PERSISTENCE_DESIGN_v0.1.md) · **Report:** [CD-01 Implementation Report V0.1](CD-01_IMPLEMENTATION_REPORT_v0.1.md)

---

## 1. 后续开发如何使用 Core Domain 服务

### 读路径（UI/用例）

- **地图列表/单图查询**：`IMapQueryService`（会话绑定实例，见 §2）。永远传 `session.ProjectId`；不存在/跨项目一律返回 null。
- **打开工作区**：`ProjectSession.OpenWorkspaceAsync(mapId, branchId, timeInput, mapQuery, resolver)`（AF-02 既有）。`branchId = null` → 根历史；`timeInput = null` → 纪元；否则 `"day:offset"`（如 `"1200:43200"`）。
- **项目身份**：`IProjectIdentityReader.ReadAsync(session.Database)`，或在 Ready 会话上直接读 `session.ProjectId / ProjectDisplayName`。

### 写路径（后续用例/测试）

```csharp
// 会话内（地图/分支创建——尚无 UI 入口，PRA-01 前后按需加用例）：
var core = new SqliteCoreDomainDatabase(session.Database);
var mapPlan = MapService.CreateMap(core, session.ProjectId!.Value, "地图名", nowUtcMs);
var branchPlan = BranchService.CreateBranch(
    core, session.ProjectId!.Value, mapPlan.RootHistory.HistoryId,
    "分支名", new HistoricalTime(1200, 0), nowUtcMs);
```

- `MapService.CreateMap` 单事务写入 map+根历史+default Calendar+day-zero Configuration（P2-09-11 §4.1 不变量）；名称非空白自动 trim。
- `BranchService.CreateBranch` 校验父历史存在且祖先链有效（无环/不悬空）后单事务写入；`parent_at` 任意合法历史时间（ADR-005 规则 1）；**不复制祖先历史**。
- 纯规则（不落库）用 `MapRules`/`BranchRules`/`ProjectRules`/`BranchLineageResolver`/`WorkspaceContextRules`（静态方法，可直接单测）。

### 时间类型

`WorldView.Domain.Time.HistoricalTime`：构造即校验（day≥0、offset 0..86399）；`TryParse("d:o")`/`Canonical()`；字典序 `CompareTo/IsBefore/IsAfter`；`Epoch`。**任何新的时间解析/规范化逻辑只允许加在此类型或其后续版本**。

## 2. Application 如何访问正式领域能力（组合根形态，`Program.cs`）

| 服务 | 注册方式 |
|---|---|
| `CoreDomainMigrationSource` | 单例，登记进 `ApplicationBootstrapper`（core-domain 4 条迁移随包创建/打开自动应用） |
| `IProjectCreationService` → `CoreDomainProjectCreationService` | 单例 |
| `IProjectIdentityReader` → `CoreDomainProjectIdentityReader` | 单例 |
| `IMapQueryService` / `IWorkspaceContextResolver` | **会话绑定**：在 VM 工厂闭包内 `new CoreDomainMapQueryService(session.Database)` 等——必须用本会话数据库，不得跨会话复用 |

## 3. 不允许绕过的边界

1. **UI/ViewModel/Renderer 禁止直接 SQL 或直接依赖 Infrastructure**；一切领域数据经四端口或 Domain 服务。
2. **Application 不得重新实现领域规则**（时间解析、世系解析、身份校验、创建不变量）——Domain 独占。
3. **新领域表必须走 `CoreDomainSchema` 迁移 + ledger**，禁止 `CREATE TABLE` 散落在适配器；迁移不可变（checksum），改结构 = 新迁移 ID。
4. **不得让 manifest 或 Bootstrap 元数据成为第二身份权威**；身份只有 `core_project` 行。
5. **测试替身不得作为集成证据**；持久化断言用真实 SQLite（模式见 `CoreDomainPersistenceTests`：temp 目录 + `SqlitePackageDatabaseFactory` + 真实 bootstrapper）。
6. **写入协调**：V1 一切写路径经 `SqliteCoreDomainDatabase`（实例内串行 + 单事务）；P2-09-12 完整协调器落地前，不得引入并发写连接。

## 4. 后续阶段衔接点

- **PRA-01**：Presentation Context 应消费 `ResolvedWorkspaceContext`（含世系/根历史 ID）与 `IMapQueryService` 投影；Scene Snapshot 由 Presentation 派生，Core Domain 不建快照表。
- **Command/Event 阶段**：`core_history` 已就绪为归属作用域；新表按 P2-09-11 §4.4 增量迁移；ADR-003 单线播放约束在 Workspace Context（选定 Branch + 单一时间）上已具备数据基础。
- **AI Assistant 阶段**：其 28 张表用独立 module_id（`ai-assistant`）登记自己的 `IMigrationSource`；跨模块共享事务按 P2-09-12 §2 模式（同一连接/事务传入）扩展 `ICoreDomainDatabase` 之外的仓储，**不得**让 Bootstrap 写领域表。
- **Calendar 阶段**：替换 `calendar_type` NULL 与 rules v0/`'{}'` 占位为新迁移 + 域服务；已有行不得重写（P2-09-14：provenance 不回改）。

## 5. 常见任务速查

- 加一张领域表：在 `CoreDomainSchema.Migrations` 追加 `("005_xxx", SQL)`；checksum 自动管理；新库自动应用、旧库打开时自动升级（每条迁移一个事务）。
- 排查"打开包失败"：看 `PackageValidationReport.Issues`（`LedgerChecksumMismatch` = 迁移被改过；`ProjectIdentityUnavailable` 已不应出现——身份服务已注册；`MultipleProjects`/`Invalid` = 身份行损坏）。
- 临时手工检查库：可用任意 SQLite 工具读 `schema_migration`/`core_project`，但**生产代码路径禁止**。
