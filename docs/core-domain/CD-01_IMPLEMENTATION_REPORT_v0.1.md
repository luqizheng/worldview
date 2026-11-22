# CD-01 — Core Domain Persistence Implementation Report V0.1

- **Date:** 2026-11-21
- **Status:** Implementation report — verdict **PASS WITH CONDITIONS**（见 §12）
- **Design:** [CD-01 Persistence Design V0.1](CD-01_PERSISTENCE_DESIGN_v0.1.md)
- **Repository:** `luqizheng/worldview` @ `master`（文档与 `WorldView/` 源代码同仓库）

---

## 1. Executive Summary

CD-01 完成 Core Domain 最小真实持久化并点亮 AF-02 四端口：

- **4 条 core-domain 迁移**（`core_project`/`core_map`/`core_history`/`core_branch`/`core_calendar`/`core_calendar_configuration`）经共享 `schema_migration` ledger 单事务应用，checksum 管理，fail-closed。
- **Project Identity** 真实创建/读取/校验（单行规则、UUID 解析、损坏分类）。
- **Map 持久化与查询**（项目范围隔离、创建不变量同事务：map+根历史+default Calendar+day-zero Configuration）。
- **Branch/Historical Time 上下文解析**（世系环/跨地图/悬空拒绝、`"day:offset"` 规范时间）。
- **端到端真实链路通过测试**：Create Package → Persist Identity → Close → Reopen → Read Identity → Query Maps → Resolve Context → Open Workspace；多 Project 隔离通过。
- 验证：Debug/Release 构建 0 错误；**139/139 测试通过**（Domain 58 / Application 44 / Infrastructure 37，全真实 SQLite/文件系统链路不以替身冒充）。

## 2. Codebase Readiness Audit（实施前）

| 项 | 审计结论 |
|---|---|
| AF-02 四端口定义/Session/用例/Bootstrap/`SqlitePackageDatabase` | Implemented and Verified（70 测试基线） |
| Core Domain 领域实体/服务/领域表/迁移 | **Missing**（P2-09-11 V0.11 为提案；明确不当作可执行 DDL） |
| P2-09-12 V0.6 事务证明 | Designed only（规范声明未执行） |
| `MapProject` 示例三件套 | 影子模型风险（未接入组合根、仅自引用）→ CD-01 删除 |
| Provider | Microsoft.Data.Sqlite 11.0.0 + 捆绑 SQLitePCLRaw（符合 P2-09-12 目标） |

采用文档版本：P2-09-11 V0.11、P2-09-12 V0.6、P2-09-13 V0.6、P2-09-14 V0.2、P2-09-15 v0.5（存在）、ADR-001~005、AF-01 V0.1、AF-02 报告 V0.1。

## 3. Adopted Architecture

见 [CD-01 Persistence Design V0.1](CD-01_PERSISTENCE_DESIGN_v0.1.md)。要点：Domain 零依赖拥有全部规则与迁移 SQL；`ICoreDomainDatabase` 为 Domain 端口；Infrastructure 适配器 + 事务；组合根接线；Application/Session 层零改动。

## 4. Implemented Domain Entities / Services（均实现且已验证）

`HistoricalTime`（解析/比较/规范化）、`ProjectRecord`/`RawProjectIdentityRow`/`ProjectRules`、`MapRecord`/`HistoryRecord`/`BranchRecord`/`CalendarRecord`/`CalendarConfigurationRecord`、`MapRules`/`MapCreationPlan`、`BranchRules`/`BranchCreationPlan`、`MapService`/`BranchService`、`BranchLineageResolver`/`BranchLineage`、`WorkspaceContextRules`/`ResolvedWorkspaceContext`、`CoreDomainDataException`。

## 5. Database Schema / Migration（已实现且已验证）

4 条迁移（`001_core_project`→`004_core_calendar`），DDL 见 `WorldView.Domain/Persistence/CoreDomainSchema.cs`。验证：新库应用 4 条 + ledger 记录；重开幂等；checksum 篡改 → `LedgerChecksumMismatch`；未知迁移 → `LedgerUnknownMigration`；FK/UNIQUE/partial index 约束行为（default Calendar 唯一、owner 组合 CHECK、跨表 FK 拒绝）。

## 6. Persistence Ports / Adapters（已实现且已验证）

`ICoreDomainDatabase`（Domain）→ `SqliteCoreDomainDatabase`（Infrastructure，真实 SQLite，单事务结构写、fail-closed 行映射）；`CoreDomainMigrationSource`（SHA-256 checksum）；`ISqliteConnectionProvider`（程序集内部连接暴露 + `InternalsVisibleTo` 测试）；`SqlitePackageDatabase` 增 `busy_timeout=5000`。

## 7. AF-02 Integration（已实现且已验证）

四端口全部接真实实现并在组合根注册（`Program.cs`）；Map Query/Context Resolver 会话绑定；Application/Session 层源代码零改动。AF-02 的 fail-closed 占位状态（`projectCreationService: null` 等）已移除。

## 8. Test Results

```
命令：dotnet test WorldView.sln（SDK 11.0.100 / net11.0 / x64 / Windows）
总计 139，失败 0，成功 139，跳过 0
  - WorldView.Domain.Tests          58 通过（全部为 CD-01 新增：HistoricalTime 解析/比较/
                                     Project 身份规则/Branch 世系/Workspace 解析/Map-Branch 创建规则）
  - WorldView.Application.Tests     44 通过（AF-02 既有 48 - 示例测试 4；无回归）
  - WorldView.Infrastructure.Tests  37 通过（AF-02 既有 14 + CD-01 新增 23：
                                     CoreDomainPersistenceTests 21 条 + Cd01EndToEndTests 2 条）
```

覆盖对照（CD-01 §8）：创建后重开读取同身份 ✓；多 Map 查询 ✓；不存在 Map → null ✓；跨 Project 查询拒绝 ✓（GetMap null + E2E 断言）；Branch 上下文正确解析 ✓（根/一/二级 + 显式时间）；非法时间拒绝 ✓（格式/越界）；迁移从初始到目标 ✓（新库 4 条 + 重开幂等）；事务失败回滚 ✓（Map 创建中途 UNIQUE 冲突 → 无残留；FK 拒绝）；损坏数据库识别 ✓（AF-02 既有 quick_check + 身份行不可解析 Invalid/MultipleProjects）；schema 不兼容安全失败 ✓（checksum/未知迁移/更高版本均 AF-02+CD-01 覆盖）；双 Project 隔离 ✓；关一开一 ✓；不误用上一会话连接 ✓（每会话独立 DB 实例 + 会话绑定适配器）；Workspace 不跨 Project 污染 ✓。

测试边界（如实声明）：Domain.Tests 中 `FakeCoreDomainDatabase` 仅用于纯规则单测；所有持久化/集成证据来自真实 SQLite 测试；E2E 不使用任何端口替身（AF-02 的 `RealPackageFlowTests` 替身流程测试保留，作为用例编排回归）。

## 9. Debug / Release Build Results

| 项 | 结果 |
|---|---|
| `dotnet build WorldView.sln`（Debug） | 成功，0 错误（存量警告 91 条：AF-02 既有测试的 xUnit1051/1031 与 `TextBox.Watermark` 过时提示；非 CD-01 引入，未顺手重构） |
| `dotnet build WorldView.sln -c Release` | 成功，0 错误 |
| Release 运行冒烟 | 进程启动、主窗口 "World View" 渲染、6 秒存活正常关闭（AF-02 已验证 WM_CLOSE 干净退出；本次冒烟确认 CD-01 组合根接线后启动无异常） |
| macOS | **未验证**（无 macOS 环境；构建/运行均不声称） |

## 10. Known Limitations

1. **P2-09-12 TX 证明未执行**：完整 Serialized Writer Coordinator、共享事务（Core+AI）、崩溃对账等属其证明测试范围；CD-01 只实现并测试了 V1 单写路径。
2. **Calendar 为结构性占位**：`calendar_type` NULL、rules v0/`'{}'`；Calendar 语义（转换/配置/显示）未实现。
3. **Map/Branch 创建暂无 Application 用例与 UI 入口**：领域服务已就绪（测试与后续用例使用）；UI 仅能打开既有包并查询。
4. **`core_map_initial_state` 未建**（payload open gate）；未来 Map 完整性检查接入时需补迁移。
5. **macOS 未验证**。
6. UI 时间输入格式为 `"day:offset"` 规范文本（无人类友好日历显示——Calendar 未实现前的预期状态）。

## 11. Architecture Deviations

| 偏差 | 理由 |
|---|---|
| Map/Branch 创建服务无对应 P2-09-13 槽位文档先行授权 | 属 CD-01 提示 §4.3/§8.2 的测试必需能力；结构完全遵循 P2-09-11 §4 不变量，未新增业务规则 |
| AF-02 报告称 Debug 构建 0 警告，本次构建存在 91 条存量警告 | 均位于 AF-02 既有文件（未触碰）；不在 CD-01 顺手修复（提示 §12：不进行无关重构） |

## 12. Definition of Done 与判定

全部 16 项 DoD 满足（见提示 §13 清单：核实模型 ✓ / Schema Proposal 关系明确（最小子集+登记决策）✓ / 持久化实现 ✓ / Schema+Migration 验证 ✓ / Identity 创建读取 ✓ / Map 查询 ✓ / Branch/时间上下文 ✓ / 四端口 ✓ / 组合根 ✓ / Create-Close-Reopen ✓ / Query-Workspace ✓ / 隔离 ✓ / 事务回滚 ✓ / Debug+Release ✓ / 文档与代码一致 ✓ / 文档入库 ✓（见 §13））。

**Verdict: PASS WITH CONDITIONS** — 条件：§10.1 TX 证明未执行（后续阶段）、§10.5 macOS 未验证。核心持久化与真实业务链路已完成且经真实数据库测试验证；不以 Shell/Mock 通过为由宣称超出范围的能力。

## 13. Git Commit SHAs

| Commit | 内容 |
|---|---|
| [`c744771`](https://github.com/luqizheng/worldview/commit/c744771) | refactor(WorldView): replace MapProject sample with core domain model and CD-01 schema |
| [`8b6da8f`](https://github.com/luqizheng/worldview/commit/8b6da8f) | feat(WorldView.Infrastructure): implement CD-01 core domain SQLite persistence |
| [`b63cde2`](https://github.com/luqizheng/worldview/commit/b63cde2) | feat(WorldView.Desktop): register CD-01 core domain services; end-to-end chain and isolation tests |
| [`886ad24`](https://github.com/luqizheng/worldview/commit/886ad24) | docs(core-domain): CD-01 设计/报告/交接 + ADR-0003 + 索引 |

注：四个提交在推送前经 rebase 对齐远端（远端新增了无关的文档提交），SHA 以 rebase 后为准。
