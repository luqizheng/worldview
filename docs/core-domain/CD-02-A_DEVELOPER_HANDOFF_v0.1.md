# CD-02-A — Developer Handoff V0.1（CD-02-B 执行契约）

- **Date:** 2026-11-22
- **Companion:** [CD-02-A 主文档](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md)（权威边界与决策依据）、[CD-02-A 关系图](CD-02-A_OBJECT_AUTHORITY_MODEL_v0.1.md)
- **Baseline:** master `3159270`；本任务未修改任何生产代码（审查时点测试基线 172/172）

本文件的目标：下一位开发人员**不需要重新研究前置讨论**即可直接执行 CD-02-B。遇到本文件与前置文档冲突时：冻结 ADR > P2-09-07（Accepted semantic contract）> 其他 P2-09 契约 > 本文件；发现矛盾时停止受影响切片并报告（P2-09-43 §2 优先级规则沿用）。

---

## 1. 30 秒上下文

CD-01/CD-01-F1 已落地 6 表 + 统一连接协调器。CD-02-B 要在这之上追加 **8 张表**，实现 Object Type 播种、ID Reservation 状态机、Object 身份/定义、Map 注册、Initial State——**全部经 Domain Services 单事务写入，经 `SqliteConnectionAccess` 协调器**。不实现 Command/Event/Asset 权威记录/Registry compiler。

## 2. 开工前必读（按序）

| 文档 | 读什么 |
|---|---|
| [CD-02-A 主文档](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md) | §4 五层分离/四层存在、§5 Reservation 状态机、§10 Initial State、§13 事务契约、§14 校验规则、§18 Open Decisions（**OD-1/OD-2 不得自行发明 typed schema**）、§21 实施契约 |
| P2-09-04 V0.2 | Reservation 七条硬约束（§7）+ 绑定/重试语义（§5/§6） |
| P2-09-07 V0.2 | Shared Definition / Initial State 的 Accepted 语义（§2/§3/§6/§7 Forbidden representations） |
| P2-09-11 V0.11 §4.1/§4.2 | 8 张新表的权威列定义与不变量（本契约未复述的列级细节以此为准） |
| P2-09-18 V0.2 §4/§5/§9/§10/§11 | 类型播种、三域分离、identity/History 在场分离、pin 规则、禁 `core_object_attribute` |
| CD-01-F1 Handoff §2 | 协调器红线（新适配器必须经 `GetAccess()`；事务不越临界区） |

## 3. 实施顺序（建议 5 个切片）

```
S1 表结构：4 条新迁移（core_object_type → core_object+definition+allocator+reservation
            → core_map_object_ref → core_map_initial_state+initial_object）
S2 类型播种 + ID Reservation：ObjectTypeSeedingService + ObjectIdReservationService
                              （Reserve/Bind/Abandon 状态机 + request_key 幂等）
S3 Object 正式创建：ObjectCreationService（Bound→Inserted 原子：identity active +
                    definition 首行 + 注册 + 可选 Initial State 成员）
S4 Definition 编辑 + Map 注册：CAS 编辑、scope 校验、注册/取消注册
S5 Initial State：MapInitialStateService（baseline CAS + 成员行 + fingerprint）
```

每个切片：Domain 记录/规则/服务 → 端口方法 → Infrastructure 适配器 → 测试。S1 完成即提交（迁移可独立验证），S2–S5 可逐片提交。

## 4. 代码落点（沿用 CD-01 模式）

| 内容 | 文件 |
|---|---|
| 迁移 SQL 常量 | `WorldView.Domain/Persistence/CoreDomainSchema.cs`（追加进 `Migrations` 列表，依赖序） |
| 新记录/规则/服务 | `WorldView.Domain/Objects/`（新目录）、`WorldView.Domain/Maps/`（注册与 Initial State 服务） |
| 端口扩展 | `WorldView.Domain/Persistence/ICoreDomainDatabase.cs`（新增方法；结构写=单事务计划模式） |
| 适配器 | `WorldView.Infrastructure/Persistence/CoreDomain/SqliteCoreDomainDatabase.cs`（或按端口拆分新文件；一律 `_access.Execute`） |
| 组合根 | 本阶段无 UI 入口要求（同 CD-01 Map/Branch 服务先例）；Application 层零改动 |

## 5. 关键实现规则（逐条）

1. **迁移**：追加式，编号接续 005 起；不 ALTER 既有 6 表；列定义以 P2-09-11 §4 为准 + 本文件 §6 差异注记；checksum 机制照旧。
2. **类型播种**（OD-4 建议案）：Project 创建事务内插入十行 `core_object_type`（type_key 取 P2-09-21 V0.1：landmass/mountain_range/mountain_peak/river/lake/terrain_region/settlement/road/political_region/point_of_interest；全部 allocatable；allocation_format_version=1）；schema/semantics pin 值（schema_version=2、semantics_id=`world-view.object-type.<key>.v1`）来自 P2-09-26 V0.2，**在 Bound 时写入 core_object**，不在 core_object_type 行上钉死可变值。
3. **Reservation**：状态机与原子边界见主文档 §5/§13.2；`allocated_id_value` = 最小十进制文本（OD-3）；`request_key` 提供时 UNIQUE 幂等；失败注入测试必须覆盖 TX-55 全路径。
4. **Bound pin**：`core_object` 行写入 `object_type_schema_version` 与 `object_type_semantics_id`（来自该类型的 descriptor 声明）；读取侧不认识的 pin → fail-closed。
5. **scope 规则**：CHECK + 服务双保险；map_local 注册非 owner Map 在服务层拒绝（DB 触发器纵深防御可留待后续切片，服务层校验为最低要求）。
6. **Initial State**：成员行必须有对应注册行（服务层校验 + 完整性测试）；`content_fingerprint` 由 Domain 规范化序列化计算（成员集合按 object_identity_id 排序、canonical JSON、不含时间戳/审计字段）；CAS 用 expected fingerprint。
7. **fingerprint 是 Domain 职责**：Application/UI 永不自算指纹。
8. **lifecycle**：`core_object.lifecycle_status` CHECK(draft/active/abandoned/retired)；draft→active 仅经 Insert 路径；retired 本阶段无服务入口（仅保留枚举与 CHECK）。
9. **无 Command 形态**：Initial State/Definition 编辑是独立 Domain 服务路径，不得实现成 Command/Event。

## 6. 测试最低集（验收清单）

- [ ] 新迁移：fresh bootstrap 全量应用 + ledger 记录；重开幂等；失败回滚；并发应用恰一次（复用 CD-01-F1 测试模式）。
- [ ] 类型播种：新建 Project → 十行类型存在；重复创建 Project 仍单行；十 type_key 精确匹配。
- [ ] Reservation：Reserve→Bind→Insert 全链；五条非法转换全部拒绝；allocator 不回退；并发 Reserve 无重复 sequence/id_value；`request_key` 重试幂等；Bind/Insert 中途失败注入 → 无半成品、重试复用同一 reservation/identity。
- [ ] Object 创建：Bound→Inserted 原子（identity active + definition + 注册同事务）；draft 身份不可注册/不可入 Initial State（负向）。
- [ ] scope：project_shared 无 owner_map / map_local 必有 owner_map；跨 Map 注册 map_local 拒绝；project_shared 多 Map 注册成功。
- [ ] Definition：active Object 恰一行；CAS stale 拒绝；hash 一致性；schema_version 与 pin 不符拒绝。
- [ ] Initial State：每 Map 恰一 baseline；成员行缺注册行拒绝；fingerprint 确定性（同内容同指纹、顺序无关）；CAS 编辑。
- [ ] 协调器继承：新写路径全部在单事务临界区内；并发读写混合无 torn state（复用 PersistenceConcurrencyTests 模式）。
- [ ] 回归：既有 172 测试零回归。

## 7. 红线（违反即 fail）

1. 不建：`core_asset`/typed asset links/command、event/checkpoint/field dataset/AI 表/任何 per-child 或 polymorphic owner 表。
2. 不实现：registry compiler、Command 写路径、Geometry payload、Relationship、Building/Cave。
3. 不发明：Initial State / Definition typed schema（OD-1/OD-2）、第二套 Object 状态表、WAL、事件溯源。
4. 不绕过：`SqliteConnectionAccess` 协调器；事务不越临界区；禁止缓存裸连接。
5. 不合并：四层存在/P1–P4；五层权威；identity 生命周期 vs History 在场。
6. 不改：Initial State 不得作为 Command；Definition 变更不得解释成历史时间点变更。

## 8. 本阶段（CD-02-A）验证执行记录

| 检查 | 结果 |
|---|---|
| 引用文件路径有效性 | 主文档头部全部依据文档、三份新文档间的全部链接与内联代码路径均经脚本实测存在（含 P2-09-16 实际文件名 `world-view-core-domain-p2-09-16-...v0.1.md`——注意它不在 `p2-09-16-...` 前缀命名下） |
| Schema 文件合法性 | P2-09-46 V0.2、P2-09-26 V0.2、P2-09-47 V0.2 JSON 解析通过；P2-09-26 V0.2 十类型 schema_version=2 / semantics_id 经脚本核对 |
| Mermaid 图结构 | 关系图文档 5 幅图围栏配对/子图闭合/状态图语法经脚本核对通过 |
| 新旧文档交叉引用 | 主文档 ↔ 关系图 ↔ Handoff 三向引用一致；对 P2-09 契约的章节号引用经代理摘要逐条核对 |
| 架构决策冲突检查 | 与 ADR-001~005/ADR-0003/P2-09 系/ME-05-02/CD-01 系逐项核对无冲突；唯一张力点（CD-01-F1 append-only 前提）已在主文档 §13.3 显式响应 |
| Migration 与实际代码一致性 | 4 条既有迁移与 `CoreDomainSchema.cs` 逐条一致；CD-02-B 增量为纯追加 |
| CD-01-F1 事务机制继承检查 | §5.1/§13.3：协调器接入、事务临界区、InSnapshotScope、短事务约束逐条进入 CD-02-B 契约与测试清单 |
| 生产代码变更 | **无**（本阶段零代码变更；`dotnet test` 实测 172/172 作为基线记录） |
| JSON/脚本验证工具 | 验证脚本在仓库外临时目录执行（`python -I`），未写入仓库 |

## 9. 给下一位开发的提醒

1. **先读主文档 §18**：OD-1/OD-2 是有意留白——payload typed 校验等 Registry V2 schema artifacts，别在 CD-02-B 里发明 JSON Schema。
2. **事务模式参考**：`SqliteCoreDomainDatabase.InsertProject`（事务内 COUNT 校验）与 `CreateMapStructure`（依赖序多语句单事务）就是你要复刻的模式；区别是 CD-02-B 有状态机 UPDATE——前置状态必须在事务内重读校验。
3. **P2-09-16 实际文件名**带 `world-view-core-domain-` 前缀（与本目录其他 P2-09 文档不同），别按文件名模式猜路径。
4. **ME-01 工作计划当前版是 v3.18**（不是 v3.9）；若需同步 ME 侧状态以 v3.18 为准。
5. 十类型是"推荐种子目录"（P2-09-21 V0.1），不是冻结 ADR——若用户裁决变更种子集，只改播种列表，表结构不受影响。
