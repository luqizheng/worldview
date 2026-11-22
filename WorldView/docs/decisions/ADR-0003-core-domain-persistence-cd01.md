# ADR-0003：Core Domain 最小持久化（CD-01）——schema 子集、事务边界与 open-gate 占位决策

- 状态：Accepted（CD-01 实施任务授权范围内；标注"占位"的项待对应文档 gate 关闭后修订）
- 日期：2026-11-21
- 决策依据：P2-09-11 V0.11（schema 提案，取最小子集）、P2-09-12 V0.6（provider/事务规范）、
  P2-09-13 V0.6（迁移依赖图）、P2-09-14 V0.2（Historical Time 契约）、ADR-001/002/005（冻结）、
  AF-01 V0.1、AF-02 实施报告 V0.1
- 设计/报告：仓库根 `docs/core-domain/CD-01_PERSISTENCE_DESIGN_v0.1.md`、
  `CD-01_IMPLEMENTATION_REPORT_v0.1.md`

## 背景

AF-02 后四个 Core Domain 边界端口无真实实现，真实读路径 fail-closed。P2-09-11 V0.11
明文声明"非可执行 DDL、非实施授权"；其 §10/§11 列出多项 open gate。CD-01 授权建立
最小但真实的领域持久化，并要求对未决语义"记录问题、不得擅自猜测"。

## 决策

1. **落地 schema 最小子集（6 表 4 迁移，module_id=`core-domain`）**：`core_project`、
   `core_map`、`core_history`、`core_branch`、`core_calendar`、`core_calendar_configuration`。
   DDL 严格取自 P2-09-11 §4 对应表（键/约束/命名），作为 Domain 拥有的迁移常量
   （`WorldView.Domain/Persistence/CoreDomainSchema.cs`）经共享 ledger 执行；
   依赖序遵循 P2-09-13 §5（project → map → history/branch → calendar）。
   **刻意排除** `core_map_initial_state`（payload 形状 open gate）与全部 Object/Command/
   Event/Asset/Checkpoint/AI 表。
2. **事务边界**：结构写（InsertProject / CreateMapStructure / CreateBranchStructure）
   各自单事务、失败整体回滚；Map 创建同事务写入 map+根历史+default Calendar+day-zero
   Configuration（§4.1 不变量）；Branch 与自有 History 同事务（无循环 FK）。
3. **PRAGMA 政策（有据可依部分）**：`foreign_keys=ON` 每连接（沿 AF-02）+ 有界
   `busy_timeout=5000`；**journal_mode 保持默认**——WAL 依赖备份/恢复证明
   （P2-09-12 §10 gate 8 未关），不启用。V1 写入协调 = 适配器实例内串行 + 进程内
   同包防双开；P2-09-12 TX-30/31/32 完整协调器**不在 CD-01 声称范围**。
4. **open-gate 占位（显式登记，非业务规则发明）**：
   - Calendar 结构占位：建表并满足创建不变量；`calendar_type=NULL`、
     `rules_schema_version=0`、`rules_payload_json='{}'`；语义待 P2-09-14 gates。
   - `lifecycle_status`：DDL 无 CHECK；服务统一写 `'active'`（词汇表未冻结）。
   - 端口历史时间文本格式：`"{day}:{offset}"`（null → 纪元），解析归 Domain
     `HistoricalTime`；Application 只透传。
   - Project 创建不自动建 Map（无 ADR 授权的"初始地图"规则；空列表合法）。
5. **影子模型收敛**：删除 AF-02 `MapProject` 示例三件套及其测试（自标注示例、未接入
   组合根）；真实 Project 持久化为其替代。
6. **端口接线**：四 AF-02 端口接真实实现；Map Query / Workspace Resolver 会话绑定
   （按 `session.Database` 构造）；Application/Session 层源代码零改动（AF-02 设计兑现）。

## 后果

- 真实"创建 → 打开 → 身份 → 地图 → 工作区"链路可用且经真实 SQLite 测试（139/139）。
- Calendar/Initial State/lifecycle 枚举的占位内容后续修订时走新迁移或域服务版本化，
  不回改既有行（对齐 P2-09-14 provenance 原则）。
- PRA-01 可基于 `ResolvedWorkspaceContext` 与 `IMapQueryService` 投影接入呈现层。
- 风险：Map/Branch 创建暂无 UI 用例——属后续任务，非本 ADR 范围。
