# ADR-0002：应用基础（AF-02）——Project Package 格式、SQLite 引入与 Core Domain 边界端口

- 状态：Accepted（AF-02 实施任务授权范围内；与 AF-01 V0.1 冲突项须用户裁决）
- 日期：2026-11-21
- 决策依据：`docs/application/AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md`（架构权威）、
  `docs/application/AF-02_DEVELOPER_IMPLEMENTATION_PROMPT_v0.1.md`（任务书）、
  P2-09-11 V0.11 §7（迁移 ledger 契约）、P2-09-12 V0.6（Provider 证明目标）
- 实施报告：`docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md`

## 背景

AF-01 确立了 Application Foundation 架构基线（Project Package / Project Session / Map
Workspace Session），AF-02 任务书授权实施。实施前的 Codebase Readiness Audit 确认：
正式 Core Domain 实现、持久化、迁移、Asset Store 均不存在（仅 `MapProject` 架构示例），
P2-09-11 V0.11 是 schema 提案而非可执行 DDL。AF-02 因此按"应用外壳/包/会话全部实现、
Core Domain 集成显式报告为门槛"的路线实施。

## 决策

1. **Project Package V1（目录式）**：`<名称>.wvproject/` 内含 `manifest.json`、
   `project.sqlite`、`Assets/sha256/`、`Staging/`、`Backups/`。manifest v1 仅含
   `packageFormatVersion`（=1）、`database.relativePath`、`createdAtUtc`；不承载任何
   领域身份（身份权威在数据库内 Bootstrap 元数据与 Core Domain）。
2. **引入 Microsoft.Data.Sqlite 11.0.0**（Infrastructure 层）：P2-09-12 V0.6 指定的
   证明目标为 11.x；连接禁用池化（`Pool=False`）以保证打开/关闭/目录重命名/临时清理的
   句柄行为完全确定；每个连接显式 `PRAGMA foreign_keys = ON`。
3. **Application Bootstrap 拥有的库内结构（非领域 schema）**：
   - `schema_migration(module_id, migration_id, checksum, applied_at_utc_ms,
     min_app_version, max_app_version, PRIMARY KEY(module_id, migration_id))`
     ——列名对齐 P2-09-11 V0.11 §7；
   - `wv_bootstrap_metadata(key, value)` ——数据库格式版本与创建时间。
   Core Domain / AI Assistant 领域表**绝不**由本阶段创建；模块迁移内容出现后经
   `IMigrationSource` 登记，由 Bootstrap 在单事务内执行并写 ledger（校验和不匹配 /
   未登记迁移 / 更高版本一律 fail-closed）。
4. **Core Domain 边界端口显式留空**：`IProjectIdentityReader`、`IMapQueryService`、
   `IWorkspaceContextResolver`、`IProjectCreationService` 定义于 Application
   Abstractions，运行时组合根刻意不注册——Create/Open 在门槛处 fail-closed 并给出
   明确诊断，不以占位数据、影子表或手工 SQL 冒充真实路径。
5. **测试栈**：xunit.v3 4.0.3 + Test.Sdk 18.12.0；.NET 11 SDK 下 `dotnet test` 经
   `global.json` 启用 Microsoft.Testing.Platform runner（VSTest 目标已被弃用）。
6. **启动面**：`StartWindow`（创建/打开/最近项目）为唯一主窗口；打开的项目以独立
   `ProjectWindow` 呈现文本式地图摘要（渲染属 PRA-01/PRA-02 之后阶段）。旧
   `MainWindow`/`MainViewModel` 示例被替换；`MapProject` 示例代码与测试保留。
7. **最近项目**：JSON 文件存储于平台应用元数据目录（Windows:
   `%LOCALAPPDATA%/WorldView/preferences/`），属应用级 UI 元数据，与世界数据严格分离。

## 与既有决策的关系

- ADR-0001（工程初始化）：分层与依赖方向不变；Infrastructure 新增 SQLite 包符合
  "正式持久化选型经 AF-01/AF-02 授权落地"的路径。`initial-tech-baseline.md` 已同步。
- 仓库根 ADR-001~005（领域层冻结决策）：本决策不触及任何领域语义。
- AF-01 中 "Target stack: .NET 10" 与实际仓库 net11.0（ADR-0001）不一致，按实际仓库
  目标实施；该差异已在 AF-02 实施报告中登记，待文档侧勘误。

## 后果

- 应用具备真实包格式、包校验、创建/打开/关闭生命周期与会话隔离；Core Domain 持久化
  落地后只需注册四个边界端口的真实实现即可点亮真实读路径，无需改动会话层。
- 真实"打开包 → 项目身份 → 地图列表"路径在 Core Domain 实现前保持 fail-closed
  （AF-02 验收口径：PASS WITH CONDITIONS）。
