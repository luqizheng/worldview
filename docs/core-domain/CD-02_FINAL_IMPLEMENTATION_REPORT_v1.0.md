# CD-02 Final Implementation Report V1.0 — Core Domain Object, Registry, Geometry & Initial State Foundation

- **Date:** 2026-11-22
- **Baseline in:** master `a773698`（CD-02-A 交付后）
- **Baseline out:** master `0bf8bf7` + 本次提交（见 §15）
- **Author:** CD-02 完整阶段委托执行（端到端工程交付）
- **最终结论：PASS WITH CONDITIONS**（条件全部为已登记的后续阶段依赖，无未声明债务；见 §12/§13）

---

## 1. CD-02 目标与实际完成范围

CD-02 的目标（委托 §四）：建立可供地图编辑器正式使用的 Core Domain Object Foundation。逐项完成情况：

| 委托目标组 | 状态 | 交付报告 |
|---|---|---|
| A. Object Identity & Persistence | ✅ 完成 | [CD-02-B](CD-02-B_IMPLEMENTATION_REPORT_v0.1.md) |
| B. Registry（十类型兼容 + 采纳扩展） | ✅ 完成（V2 激活缺口如实登记） | [CD-02-C](CD-02-C_IMPLEMENTATION_REPORT_v0.1.md) |
| C. Initial State | ✅ 完成 | CD-02-B |
| D. Geometry | ✅ 完成（split/merge 按契约 deferred） | [CD-02-D](CD-02-D_IMPLEMENTATION_REPORT_v0.1.md) |
| E. Asset | ✅ 完成（core_asset + definition typed link；initial/command links 按触发条件未启用） | CD-02-D |
| F. Building/Cave | ✅ 完成（55/55 P2-09-40 向量执行通过） | [CD-02-E](CD-02-E_IMPLEMENTATION_REPORT_v0.1.md) |
| G. ME-03/04/05 衔接 | ✅ 领域 API 可调用性证明 + 缺口清单 | [CD-02-F](CD-02-F_MAP_EDITOR_INTEGRATION_REPORT_v0.1.md) |

**不属于本阶段（继承 CD-02-A §15/§19 与 P2-09 契约）**：Command/Event 写路径（CD-03+）、Relationship 持久化、`core_command_asset`/`core_map_initial_object_asset`/`core_checkpoint`、Field Dataset 表、Registry V2 激活、AI 表。以上均未实现且未伪装。

## 2. 最终数据库 Schema

10 条 core-domain 迁移（追加式，编号 001–010；从未 ALTER 既有表）：

**表数量更正：** 上表共创建 **16 张表**（CD-01 六张 + CD-02-B 八张 + CD-02-D 两张）；10 是迁移条目数量，不是表数量。

| # | 表 | 阶段 |
|---|---|---|
| 001–004 | core_project / core_map / core_history / core_branch / core_calendar / core_calendar_configuration | CD-01 |
| 005 | core_object_type | CD-02-B |
| 006 | core_object / core_object_definition / core_object_id_allocator / core_object_id_reservation | CD-02-B |
| 007 | core_map_object_ref（+ map_local scope 触发器） | CD-02-B |
| 008 | core_map_initial_state / core_map_initial_object（成员→注册复合 FK） | CD-02-B |
| 009 | core_asset（内容寻址 UNIQUE；publication/integrity CHECK） | CD-02-D |
| 010 | core_object_definition_asset（四元组 UNIQUE；owner/asset 复合 FK） | CD-02-D |

关键约束：reservation 状态机 CHECK（Reserved⇒identity NULL、Bound/Inserted⇒非空）；R-scope 配对 CHECK；lifecycle CHECK；成员行→注册行复合 FK（P3⇒P2 数据库层强制）；placement payload/version 成对 CHECK；hash 算法 CHECK（sha-256）。

## 3. Domain API（`ICoreDomainDatabase` 端口 + Domain 服务）

- **结构**：InsertProject(ProjectCreationPlan)（身份+12类型+allocator 同事务）/ CreateMapStructure / CreateBranchStructure
- **Object Type**：ListObjectTypes / FindObjectType / FindObjectTypeByTypeKey
- **Reservation**：FindReservation / **ReserveObjectId**（request_key 幂等）/ **BindReservation**（registry pin）/ **AbandonReservation**
- **Object/Definition**：FindObject / ReadObjectDefinition / **InsertObject**（TX-55 原子）/ **EditObjectDefinition**（CAS）
- **注册**：FindMapObjectRef / ListMapObjectRefs / **RegisterMapObject** / **UnregisterMapObject**
- **Initial State**：FindMapInitialState / ListInitialObjects / **EditMapInitialState**（CAS 全量重写）
- **Asset**：FindAsset / FindAssetByContentHash / ListAssets / **RegisterAsset**（幂等）/ UpdateAssetIntegrityState / **FindOrphanAssetIds**
- **Definition Asset link**：FindDefinitionAssetLink / ListDefinitionAssetLinks / **LinkDefinitionAsset** / **UnlinkDefinitionAsset**
- **Registry 运行时**（Domain 静态）：ObjectTypeRegistry（Require/ValidatePin/ValidateDefinition/ValidateGeometryRole/InitialStateIncludes）+ JsonSchemaSubset（Draft 2020-12 有界执行）+ BuildingCaveAggregateValidator（三层校验 + 稳定错误码）
- **Geometry**：GeometryRules（manifest 校验/三方一致性）/ GeometryAssetEnvelope（P2-09-25 有界校验）/ GeometryOperationRules（split/merge 显式拒绝）
- 异常族：CoreDomainDataException（损坏 fail-closed）/ CoreDomainConflictException（语义冲突）/ RegistryException（稳定错误码）

## 4. Registry 状态

- **运行时目录**：P2-09-26 V0.2（十基础类型）+ P2-09-41 V0.4（Building/Cave 采纳对）经嵌入资源合并 = 12 类型；两工件**未被修改**；十基础类型 pin（schema_version=2 + semantics_id v1）逐类型测试锁定。
- **typed payload 校验**：简单类型 = descriptor 字段校验（六种声明形式）；采纳类型 = P2-09-38/39 V0.5 真实 schema 执行（JsonSchemaSubset 有界 Draft 2020-12；不支持关键词 fail-closed）+ 跨记录层。
- **Registry V2**：**未激活**——十项验收门（P2-09-45 §8）未执行，缺口清单见 CD-02-C §6。
- **schema/semantics pin**：Bound 时写入 core_object 行，读侧未知 pin fail-closed（篡改测试覆盖）。

## 5. Object 与 Initial State 实现

- 四层存在（P1 身份/P2 注册/P3 成员/P4 未来 Command）独立落表，测试锁定 P1⇏P2⇏P3、P3⇒P2（DB+服务双保险）、draft/abandoned 不可入注册/成员。
- Reservation：全状态机（4 合法/5 非法转换测试）、request_key 幂等（TX-21/22）、并发 8 线程唯一分配、allocator 永不回退、TX-55 中途失败注入零半成品+同 ID 重试。
- Initial State：每 Map 唯一 baseline、Domain 规范化指纹（顺序无关、无审计字段）、CAS stale 拒绝、全量重写、Branch 创建零触碰（继承=解析时语义）。

## 6. Geometry / Asset 实现

- Geometry Component manifest（P2-09-19 §6.2 十字段）+ descriptor 依据校验（role/family/dimension）+ manifest↔link↔asset 三方一致性（权威 hash 从 core_asset 解析）。
- Asset 权威元数据：内容寻址幂等登记、完整性状态扫描（corrupt/missing 阻断新引用）、孤儿对账；字节层不可变（FileAssetStore）+ "file existence never implies publication"。
- `worldview.geometry-json` envelope 结构校验（P2-09-25 推荐方向，隔离替换点）；split/merge 类型化拒绝。

## 7. Building/Cave 实现

- 采纳对进入运行时目录（12 类型播种）；child aggregate 留在 owner typed payload（无新表、无顶层子类型）；P2-09-38/39 V0.5 真实 schema 执行 + 稳定错误码（CD_BLD_*/CD_CAVE_*）+ publication/measurement 层。
- **55/55 P2-09-40 V0.6 向量真实执行通过**；发现一处已发布工件漂移（向量 patch 用 V0.4 时代字段名）并显式适配（§见 CD-02-E 报告 4）。

## 8. ME-03/04/05 集成情况

端到端证明（ME-04 建筑创作全链 + ME-03 对象原语，全部经 Domain Services；UI 直写架构上不可能）。缺口如实登记：Relationship（ME-03 Road connectivity P0）、Command 写路径（ME-05 History-local）、Field Dataset、Checkpoint、spatial reference 定义实体——均为后续阶段，详见 [CD-02-F §4](CD-02-F_MAP_EDITOR_INTEGRATION_REPORT_v0.1.md)。

## 9. 迁移历史

001–004（CD-01）→ 005–008（CD-02-B）→ 009–010（CD-02-D）。全部追加式；fresh bootstrap 全量应用 + ledger 记录、重开幂等、失败回滚、双连接并发恰一次（复用 CD-01-F1 测试模式，计数断言随迁移数更新）。**既有数据库兼容**：采纳前创建的数据库需显式补齐 building/cave_system 类型行（当前无真实用户数据；已登记为条件）。

## 10. 测试清单与真实结果

| 套件 | 数量 | 结果 |
|---|---|---|
| **全量（Debug）** | **381** | ✅ 381/381 |
| **全量（Release）** | **381** | ✅ 381/381 |
| Domain（规则/registry/几何/服务流） | 175 | ✅ |
| Application（既有） | 44 | ✅ |
| Infrastructure（真实 SQLite） | 162 | ✅ |
| 其中 CD-02 阶段新增 | ~209 | ✅ 全部真实执行 |

关键证据（全部真实执行，无 mock 冒充）：状态机全转换+非法拒绝；TX-55 ABORT 触发器失败注入→零半成品→同 ID 重试；8 线程并发 Reserve 唯一性；request_key 幂等；CHECK/FK/UNIQUE 负向（raw SQL）；CAS stale 拒绝；hash 篡改读侧 fail-closed；pin 篡改 fail-closed；并发读写无撕裂；12 类型播种/幂等；聚合校验 55 向量；孤儿对账；跨 Project 隔离。

## 11. 构建结果

- Debug build：✅ 0 错误
- Release build：✅ 0 错误
- Windows / .NET 11.0.100 / Avalonia 12.1.6（既有栈）
- **macOS：未验证**（延续 CD-01/CD-01-F1 既有条件；无 macOS 环境）

## 12. 未完成事项（Blocked / Deferred）

| 项 | 类别 | 依赖 |
|---|---|---|
| Command/Event 写路径（core.command.*） | Deferred | CD-03+ Command 阶段 |
| Relationship 持久化 + Road connectivity（ME-03 P0） | Deferred | Relationship 阶段 |
| `core_command_asset` / `core_map_initial_object_asset` | Deferred（触发条件未满足/未到阶段） | Command 阶段 / Initial State payload 引入 geometry Asset |
| `core_checkpoint` | Deferred | Checkpoint 阶段（指纹契约已锁定） |
| Field Dataset/Revision 表（ME-05-01） | Deferred | Field Dataset 阶段 |
| Registry V2 激活（十验收门） | Deferred | P2-09-45 §8 十门；缺口清单 CD-02-C §6 |
| 几何 split/merge | Deferred（显式拒绝边界已实现） | 组件 lineage/映射规则设计 |
| P2-09-12 完整事务证明套件 | 未执行（延续 CD-01-F1 条件） | 专项执行 |
| macOS 验证 | 未执行 | macOS 环境 |
| Application 用例/UI 入口 | Deferred | ME 编辑器实施（ME-01 工作计划 v3.18） |
| 采纳前数据库的类型行补齐 | Deferred | 存量数据库出现时（当前无真实用户数据） |

## 13. 已知限制

1. geometry payload 编码按 P2-09-25 **Recommended 方向**实现（非冻结 ADR；替换点已隔离于 `GeometryAssetEnvelope`）。
2. P2-09-40 向量工件漂移的规范化适配层为临时桥（上游修订后移除）。
3. OD-1/OD-2 typed 内容 schema 仍开放（结构层 + registry 字段校验已覆盖当前全部写路径；Initial State payload 维持结构层）。
4. OD-3 显示语法为最小占位（十进制 sequence 文本）。
5. retired lifecycle 无服务入口（按契约仅保留枚举/CHECK）。

## 14. 架构决策引用

ADR-001~005（分叉/统一 History/命令驱动播放/批量修复/Branch 分叉九条）、ADR-0003（CD-01 持久化）、CD-02-A（Object/Initial State 权威闭合——本报告的全部实现契约来源）、P2-09-04/07/11/13/15/16/18/19/20/21/24/25/26/37–43/45–47、ME-03-H/ME-04-H/ME-05-01/05-02、CD-01/CD-01-F1 报告。冲突检查：各阶段报告逐项核对，无冲突；CD-01-F1 条件 1（UPDATE 路径）由 CD-02-B §4 正式响应并测试。

## 15. Git commit 列表

| Commit | 内容 |
|---|---|
| `13bd8cd` | 迁移 005–008（8 张表） |
| `60af79b` | Domain 记录/规则/服务 + 端口 + 适配器（S2–S5） |
| `95f22e4` | CD-02-B 真实 SQLite 验证套件 + Reserve 读回缺陷修复 |
| `648f9dd` | CD-02-B 报告/交接文档 |
| `cb85f67` | CD-02-C registry 运行时 + typed definition 校验 |
| `ae64daa` | CD-02-D geometry & asset foundation |
| `2e79fc4` | CD-02-E Building/Cave 采纳（S1–S10）+ 55 向量执行 |
| `0bf8bf7` | CD-02-F ME 集成证明 |
| （本次提交） | CD-02-G 最终验收报告 + 索引同步 |

## 16. 最终验收结论

**PASS WITH CONDITIONS**。

- 委托 §十二完成标准 1–17 逐项核对：1–11 ✅（每项见 §1 表与分阶段报告）；12 ✅（真实 SQLite 测试证据）；13 ✅（Debug/Release 双构建）；14 ✅（381/381，Debug+Release 双跑）；15 ✅（文档与代码同步，见 docs/core-domain/ 六份报告 + README/IMPLEMENTATION-LOG）；16 ✅（全部已推送 GitHub）；17 ✅（本报告）。
- Conditions：§12 未完成事项全部为已登记的后续阶段依赖，且在对应设计契约中有明确交付位置；无未声明债务、无伪装完成项。
