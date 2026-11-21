# CD-01 — Core Domain Persistence Design V0.1

- **Date:** 2026-11-21
- **Status:** Implemented design baseline（本文件描述 CD-01 已实现的最小 Core Domain 持久化；变更须经新 ADR 或修订本文件）
- **Architecture authority:** [P2-09-11 Physical Schema V0.11](../architecture/core-domain/p2-09-11-physical-schema-design-v0.11.md)（提案，本设计取其最小子集落地）、[P2-09-12 Provider/Transaction Proof Spec V0.6](../architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.6.md)、[P2-09-13 Integrated Schema Inventory V0.6](../architecture/core-domain/p2-09-13-integrated-schema-inventory-migration-graph-v0.6.md)、[P2-09-14 Historical Time/Calendar Validation V0.2](../architecture/core-domain/p2-09-14-historical-time-calendar-validation-contract-v0.2.md)、ADR-001/002/005（冻结）、[AF-01](../application/AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md)
- **Implementation report:** [CD-01 Implementation Report V0.1](CD-01_IMPLEMENTATION_REPORT_v0.1.md)
- **Scope:** Project Identity 持久化、Map 持久化与查询、Branch/Historical Time Workspace 上下文解析、AF-02 四端口接入

---

## 1. 目标与边界

CD-01 让 Core Domain 成为真正运行的、具有持久化能力的领域权威：

```
Application Host → Project Package → Project Session
    → Core Domain（Project Identity / Map Query / Branch Context / Historical Time Context）
    → Persistence Ports（ICoreDomainDatabase，Domain 拥有）
    → Infrastructure Adapters（Microsoft.Data.Sqlite）
    → project.sqlite
```

**不做**（沿用 CD-01 提示 §9）：Object/Command/Event/Asset/Checkpoint 表、Event Replay、World State Reconstruction、Presentation Snapshot、多进程写协调、EF Core。

## 2. 分层与职责

| 层 | 内容 | 关键类型 |
|---|---|---|
| Domain（零依赖） | 实体/值对象/规则/领域服务 + 持久化端口 + 迁移 SQL 常量 | `HistoricalTime`、`ProjectRecord`、`MapRecord`/`HistoryRecord`/`BranchRecord`/Calendar 记录、`ProjectRules`/`MapRules`/`BranchRules`、`MapService`/`BranchService`、`BranchLineageResolver`、`WorkspaceContextRules`、`ICoreDomainDatabase`、`CoreDomainSchema` |
| Application（AF-02，零改动） | 四端口契约、用例、会话 | `IProjectCreationService` 等 |
| Infrastructure | SQLite 适配器 + 迁移登记 | `CoreDomainMigrationSource`、`SqliteCoreDomainDatabase`、四个端口适配器 |
| Desktop | 组合根注册 | `Program.cs` |

- 领域规则**全部**在 Domain：身份单行校验、Map 创建不变量、ADR-005 分叉规则、世系环/跨地图检测、时间解析与规范化。
- Infrastructure 不做业务判断；行数据不可解析时抛 `CoreDomainDataException`（fail-closed）。
- 事务边界归持久化端口实现方（与 AF-02 端口文档一致）；域服务只构建"计划"（`MapCreationPlan`/`BranchCreationPlan`），适配器按依赖序单事务写入。

## 3. Schema（module_id = `core-domain`，4 条迁移）

迁移 SQL 为 Domain 拥有的常量（`WorldView.Domain/Persistence/CoreDomainSchema.cs`），经 `IMigrationSource` → `ApplicationBootstrapper` 执行：每条迁移与 ledger 插入同事务、checksum（SHA-256）不匹配/未知迁移 fail-closed（AF-02 已验证的机制）。

| 迁移 | 表 | 要点 |
|---|---|---|
| `001_core_project` | `core_project` | project_id TEXT PK（canonical 小写连字符 UUID）、name、format_version、时间戳。**单行规则由服务层强制**（读 0 行→NotEstablished、>1→MultipleProjects、UUID 不可解析→Invalid） |
| `002_core_map_identity` | `core_map` | PK map_id、FK→core_project、`UNIQUE(project_id,map_id)` |
| `003_core_history_branch` | `core_history` + `core_branch` | kind CHECK（map_root/branch_owned + owner 组合 CHECK）、`UNIQUE(project_id,owner_map_id)`（一 Map 一根历史）；branch：`parent_history_id≠own_history_id` CHECK、parent_at 时间对 CHECK、复合 FK×2 |
| `004_core_calendar` | `core_calendar` + `core_calendar_configuration` | partial unique index 每 Map 恰一 default；configuration origin CHECK、生效区间时间对、复合 FK |

通用约定：UUID TEXT 主键；运营时间戳 `*_utc_ms`；历史时间 `(day_number, offset_seconds)` 整数对；无 `ON DELETE CASCADE`（默认 NO ACTION，生命周期归 Domain Services）；每连接 `PRAGMA foreign_keys=ON`。

## 4. 权威关系

- **Project Identity 权威 = `core_project` 行**（Core Domain 创建/读取）。manifest 仍只是包元数据（AF-01 边界不变）。
- **Map 权威 = 该 Project 数据库内的 `core_map`**；Map 不是第二持久化单元。
- **Workspace 上下文权威 = Domain 解析服务**：根历史经 `core_history.owner_map_id` 唯一解析；Branch 世系沿 `parent_history_id` 动态上溯（环/悬空/branch_owned 无主/跨 Map 全部拒绝）；不复制祖先历史（ADR-002/005）。

## 5. Historical Time 语义

- 值对象 `HistoricalTime(long DayNumber, int OffsetSeconds)`：day ≥ 0、offset ∈ [0,86399]、字典序全序、纪元 (0,0)。
- **端口文本格式 `"{day}:{offset}"`**（如 `"1200:43200"`；前导零可解析、规范化输出不带）。`historicalTimeInput = null` → 纪元。解析/规范化由 Domain 独占；Application 只透传字符串。
- `ValidatedWorkspaceContext.CanonicalHistoricalTime` = 规范化文本。

## 6. 事务与写入协调

- 结构写操作各自单事务：`InsertProject`（事务内 COUNT 校验单行）、`CreateMapStructure`（map→history→calendar→configuration 依赖序）、`CreateBranchStructure`（history→branch）。失败整体回滚，不留半成品。
- PRAGMA：`foreign_keys=ON`（每连接，AF-02 已有）+ `busy_timeout=5000`（有界，CD-01 新增）。**journal_mode 保持默认**——WAL 依赖备份/恢复证明（P2-09-12 §10 gate 8 未关），不启用。
- V1 写入协调：`SqliteCoreDomainDatabase` 实例内 lock 串行 + 应用级单会话写路径（进程内同包防双开由 Registry 保证）。P2-09-12 TX-30/31/32 的完整 Serialized Writer Coordinator 与多连接读隔离属其证明测试范围，CD-01 **不声称**已完成该证明。

## 7. AF-02 四端口接线

| 端口 | 实现 | 生命周期 |
|---|---|---|
| `IProjectCreationService` | `CoreDomainProjectCreationService`：路径圈禁（复用 `IsRelativePathConfinedToPackage`）→ 自开数据库 → `ProjectRules.CreateProject` → `InsertProject` 单事务 → 释放句柄 | 单例 |
| `IProjectIdentityReader` | `CoreDomainProjectIdentityReader`：读原始行 → `ProjectRules.ValidateIdentityRows` → 类别映射 | 单例 |
| `IMapQueryService` | `CoreDomainMapQueryService`（会话绑定 `session.Database`）：项目范围查询；不存在/跨项目返回 null | 每会话 |
| `IWorkspaceContextResolver` | `CoreDomainWorkspaceContextResolver`（会话绑定）：调 `WorkspaceContextRules` → 映射结果 | 每会话 |

组合根：core-domain 迁移源登记进 `ApplicationBootstrapper`；VM 工厂闭包内按会话构造后两个端口。Application/Session 层零改动。

## 8. CD-01 落地时显式决策的 open gate（不发明规则，只做最小占位并登记）

| Open gate（文档来源） | CD-01 决策 |
|---|---|
| Calendar 规则内容/类型词汇未设计（P2-09-14 gates） | 建表并满足 §4.1 创建不变量；`calendar_type` 存 NULL、`rules_schema_version=0`、`rules_payload_json='{}'`（结构性占位） |
| `core_map.lifecycle_status` 枚举未冻结 | DDL 不加 CHECK；服务统一写 `'active'` |
| Initial State payload 形状未定（P2-09-11 gate 4） | `core_map_initial_state` 不建表（后续迁移补） |
| WAL/备份（P2-09-12 gate 8） | journal_mode 默认；仅 busy_timeout |
| 端口时间输入格式未定义（AF-02 不透明占位） | `"day:offset"` 规范文本 |
| 自动初始 Map 无 ADR 依据 | Project 创建只写身份行；Map 经领域服务显式创建 |

## 9. 影子模型收敛

`MapProject`/`MapProjectService`/`IMapProjectRepository`/`InMemoryMapProjectRepository` 等 AF-02 自标注"架构示例"代码已**删除**（CD-01 §3.2）：真实 Project 持久化落地后保留示例即构成第二套 Project 模型。
