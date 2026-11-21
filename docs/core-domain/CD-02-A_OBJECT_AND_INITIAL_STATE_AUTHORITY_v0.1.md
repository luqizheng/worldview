# CD-02-A — Core Domain Object & Initial State Authority Closure V0.1

- **Date:** 2026-11-22
- **Status:** Architecture closure — 权威边界与实施契约（本文件闭合 Object/Initial State 的领域权威关系与数据边界；CD-02-B 按此实施）
- **Baseline:** `luqizheng/worldview` @ `master` `3159270`（CD-01-F1 交付后）
- **Companion:** [CD-02-A Object Authority Model（关系图）](CD-02-A_OBJECT_AUTHORITY_MODEL_v0.1.md)、[CD-02-A Developer Handoff](CD-02-A_DEVELOPER_HANDOFF_v0.1.md)
- **主要依据（均为仓库内正式文档）：** P2-09-04 V0.2（ID Reservation）、P2-09-07 V0.2（Shared Definition / Initial State，Accepted semantic contract）、P2-09-11 V0.11（物理表提案）、P2-09-12 V0.6（事务证明规格）、P2-09-13 V0.7（集成清单/迁移图）、P2-09-15 V0.5（Command Catalog）、P2-09-16 V0.1（Definition/Checkpoint Coherence）、P2-09-18/19/20 V0.2（Object/Geometry/Relationship 契约）、P2-09-21 V0.1（内置类型种子目录）、P2-09-24 V0.1（字段目录）、P2-09-26 V0.2（Descriptor Registry 现行目录）、P2-09-37 V0.7 / P2-09-42 V0.9 / P2-09-43 V0.6（Building/Cave 采纳包，Proposal）、P2-09-45 V0.3 / P2-09-46 V0.2 / P2-09-47 V0.2（Registry Format V2，设计态）、ME-05-02 V0.1（Shared Geometry）、ME-03-H / ME-04-H V0.1（跨架构评审）、CD-01 / CD-01-F1 全系列

---

## 1. Purpose / Scope

CD-02-A 不实现功能。它的产出是一套与当前代码一致、可直接指导 CD-02-B 实施的权威闭合：

1. Object Identity（全局身份 vs 类型身份、Reservation 生命周期）；
2. Object Definition（权威存储、CAS、Schema 版本、与 Asset 的引用）；
3. Object Type Descriptor / Registry（V2 边界、legacy 兼容、编译/运行时分工）；
4. Object ID Allocation / Reservation（原子性、消耗规则、并发）；
5. Map Object Registration / Presence（四层存在区分）；
6. Map Initial State（唯一权威、payload 边界、Branch 继承、Checkpoint 感知）；
7. History / Branch 与 Initial State 的关系；
8. Object Definition Asset 引用（typed link 物理/事务边界）；
9. Geometry Component 与 Object 的所有权关系；
10. 后续 Object Persistence 的事务与一致性要求（CD-02-B 最小建表契约）。

**不在本阶段范围：** Command/Event 持久化、Geometry split/merge、Relationship 持久化、Registry V2 激活、Building/Cave descriptor、AI 表。见 §19 Deferred Scope。

## 2. Existing Baseline

- **代码基线（master `3159270`）：** CD-01 + CD-01-F1 已落地——6 张域表（`core_project`/`core_map`/`core_history`/`core_branch`/`core_calendar`/`core_calendar_configuration`）经 4 条 `core-domain` 迁移；Project Identity 单行规则；Map/Branch 创建领域服务与单事务结构写；`SqliteConnectionAccess` 统一连接协调器（1 连接 = 1 gate = 1 disposed 状态，Monitor 重入提供 `InSnapshotScope` 原子快照）；`FileAssetStore` 为 SHA-256 内容寻址字节库。172/172 测试通过（Domain 60 / Application 44 / Infrastructure 68）。
- **文档基线：** P2-09-07 V0.2 为 Accepted semantic contract；P2-09-04 V0.2 / P2-09-11 V0.11 / P2-09-13 V0.7 / P2-09-15 V0.5 / P2-09-16 V0.1 / P2-09-18/19/20 V0.2 为 Recommended 契约；P2-09-26 V0.2（format_version=1）为现行 descriptor registry 工作目录；P2-09-26 V0.3 / P2-09-45 V0.3（Registry Format V2）为 proposal，十项验收门未执行，**未激活**；Building/Cave 采纳包（P2-09-37–43）为 proposal，55 vectors 未执行。
- **CD-01 登记的 open gate（与本文相关）：** Initial State payload 形状未定（`core_map_initial_state` 未建）；`core_map.lifecycle_status` 词汇未冻结；Calendar 为结构占位。

## 3. Actual Code Audit

审查方式：逐文件阅读 `WorldView/` 真实源码（Domain 11 文件 / Application 21 文件 / Infrastructure 9 文件 / 测试 17 文件，全量清单经 `find` 实测），不以设计文档替代代码证据；`dotnet test WorldView.sln` 实测 172/172 通过。

### 3.1 能力盘点表

| Capability | Current code | Current migration | Tests | Status |
|---|---|---|---|---|
| Object Identity | **无**（无 `core_object` 行模型/记录/服务；仅 `CoreDomainSchema.cs` 注释声明"刻意排除"） | 无 | 无 | **Missing** |
| Object Definition | **无**（无 definition payload/hash/schema version 代码） | 无 | 无 | **Missing** |
| Object Type Registry | **无**（无 `core_object_type`；无 descriptor 加载/编译代码；十类型仅存在于 P2-09-26 V0.2 JSON 与 P2-09-24 目录文档） | 无 | 无 | **Missing** |
| ID Reservation | **无**（无 allocator/reservation 表、服务或状态机代码） | 无 | 无 | **Missing** |
| Map Registration | **无**（无 `core_map_object_ref`；Map↔Object 关系代码不存在） | 无 | 无 | **Missing** |
| Initial State | **无**（`core_map_initial_state`/`core_map_initial_object` 未建；CD-01 §8 登记为 open gate） | 无 | 无 | **Missing** |
| Geometry Component | **无**（无组件身份/role/payload 代码；无 geometry Asset 编码） | 无 | 无 | **Missing** |
| Asset Link | **部分（底层）**：`FileAssetStore`+`IAssetStore` 提供内容寻址不可变字节放置（SHA-256、幂等发布、哈希篡改拒绝）；**无** `core_asset` 权威记录行、无任何 typed link 表 | 无 | AF-01 既有 Asset Store 测试 | **Partial**（字节层就绪；Core 权威记录与 typed link 缺失） |
| Relationship | **无** | 无 | 无 | **Missing** |
| （参照）Project Identity | `ProjectRecord`/`ProjectRules`/`InsertProject`（事务内 COUNT 单行规则） | `001_core_project` | ProjectRulesTests + 持久化/并发/隔离测试 | **Implemented** |
| （参照）Map / History / Branch 结构 | `MapRecord`/`HistoryRecord`/`BranchRecord`/`MapRules`/`BranchRules`/`MapService`/`BranchService`/`BranchLineageResolver`/`WorkspaceContextRules` | `002`–`004` | 172 中的 60 Domain + 68 Infrastructure | **Implemented** |
| （参照）连接协调/事务机制 | `SqliteConnectionAccess` + `InSnapshotScope`；事务整体入临界区 | —（机制） | PersistenceConcurrencyTests 16 条 + Integrity 15 条 | **Implemented** |

状态词只用 Implemented / Partial / Missing / Blocked / Not Verified；本表无 Not Verified 项（所有"存在"判断均经代码阅读与测试实证，所有"缺失"判断经全源码 grep 复核：`core_object*`/`Reservation`/`GeometryComponent`/`Relationship`/`Initial State` 在 `src/` 零命中）。

### 3.2 与设计文档的差距要点

1. P2-09-11 V0.11 的 22 表基线是提案清单；当前实现为有意子集（6 表）。CD-02-B 按 §17 最小清单增量建表，不一次性实现 22 表。
2. `FileAssetStore` 只是字节层（AF-01 边界）；Core Asset 权威记录（`core_asset`）与 typed link 均未实现——不得把 FileAssetStore 误判为 Asset 权威已实现（BUILDING_CAVE_GAP_AUDIT §2.3 同一结论）。
3. CD-01-F1 的连接协调器与事务机制可直接复用，但其 **V1 append-only 前提将被 CD-02-B 打破**（Reservation 状态转换、`core_object.lifecycle_status` 转换、Definition 编辑都是 UPDATE 路径）——见 §13.3 红线。
4. Map/Branch 创建服务无 Application 用例与 UI 入口（CD-01 报告 §10.3），Object 创建服务将面临同样状态；CD-02-B 契约只要求 Domain 服务 + 测试，不要求 UI。

## 4. Domain Authority Model（权威关系总纲）

1. **Core Domain 是正式 Object、Definition、ID 分配、Map 注册、Initial State 的唯一权威。** Application 只协调；AI Proposal/Candidate 不是领域数据；一切正式写入经 Domain Services（继承 §4.1 既定约束，与 ME-03-H Gate G-A、ME-04-H §6.4 一致）。
2. **权威分离五层（P2-09-18 §2.6，闭合采纳）：** ① Object Type 定义数据（descriptor/registry，编译期）② Object Identity（`core_object`）③ Project 共享当前定义（`core_object_definition`）④ Map Initial State（`core_map_initial_state`/`core_map_initial_object`）⑤ Map/Branch 运行时状态（未来 Command 路径）。五层不得合并，运行时状态永不算三层。
3. **存在四层区分（本闭合的核心结论之一，禁止合并为单一 `Object.Status`）：**
   | 层 | 权威落点 | 语义 |
   |---|---|---|
   | P1 在 Project 中存在 | `core_object` 行（lifecycle：draft/active/abandoned/retired） | 身份已分配并绑定/插入 |
   | P2 被 Map 注册 | `core_map_object_ref` | Map 显式纳入该对象（可先于 time-zero 在场） |
   | P3 在 Initial State 中出现 | `core_map_initial_object` | time-zero 成员 + 初始 placement/事实 |
   | P4 历史时间中被创建/激活/停用 | 未来 Command 路径（`core.object.create`/`deactivate`/`activate`，CD-03+） | post-zero 在场与可用性 |
   P1⇏P2⇏P3：Project-shared Object 可被多 Map 注册而不在任何 Initial State 中（P2-09-16 §6 末验收 4；P2-09-11 §2 第 8 条）；P3 蕴含对应 Map 的 P2（`core_map_initial_object` 必须有对应 `core_map_object_ref` 行，P2-09-11 §4.1）。
4. **Identity 级生命周期与 History 级在场分离（P2-09-18 §9）：** `core_object.lifecycle_status` 只表达身份级状态；post-zero `core.object.deactivate` 只改所选 History 的运行时可用性，永不改身份行。
5. 权威关系图见 [CD-02-A_OBJECT_AUTHORITY_MODEL_v0.1.md](CD-02-A_OBJECT_AUTHORITY_MODEL_v0.1.md)。

## 5. Object Identity

**双表示分离（P2-09-04 §3，闭合采纳）：**

| 表示 | 生成 | 用途 | 生命周期 |
|---|---|---|---|
| `allocated_id_value`（TEXT） | Object Type 分配器：分配格式 + `allocated_sequence` 派生，Reservation 创建时冻结 | 领域可见的正式 Object ID（UI/AI 引用） | 永不复用、永不改写 |
| `object_identity_id`（UUID TEXT） | Core Domain 在 Reserved→Bound 时生成 | 数据库/API 关系键 | 与 reservation 1:1 绑定；不物理删除 |

- **全局身份与类型身份的关系：** 分配命名空间 = Project + 稳定 Object Type 身份（P2-09-04 §2）；`object_identity_id` 项目内全局唯一，`allocated_id_value` 在 (project, object_type) 内唯一。不同 Project 可独立分配同类型同值（V1 不要求跨 Project 唯一）。Branch 不是分配命名空间（Branch 继承身份）。
- **Child ID（Building/Cave 未来）：** 只在所属顶层 Object 内稳定作用域，不是全局 Core Object ID，不占用 Object Type allocation，永不进 `core_object`（P2-09-37 §2.2、P2-09-42 §3.3、P2-09-45 §5.9）——本阶段仅在契约中保留该边界，不实现。
- **Reservation 状态机（P2-09-04 §1/§5/§7，闭合采纳）：**
  ```
  Reserved ──bind──▶ Bound ──insert──▶ Inserted（终态）
     │                  │
     └── abandon ──────▶ Abandoned（终态；Bound→Abandoned 保留 identity 行为 abandoned 墓碑）
  ```
  - **Reserved→Bound（同事务）：** Core Domain 生成 `object_identity_id`、插入 `core_object` 行（lifecycle=`draft`、钉住 `object_type_schema_version` + `object_type_semantics_id`）、把 identity key 写回 reservation。draft 身份不得出现在 Map membership / Initial State（P2-09-04 §5；P2-09-11 §5 不变量 9）。
  - **Bound→Inserted（与正式创建同事务原子）：** identity 行 draft→active、reservation→Inserted、必需 definition/map 注册/初始事实同事务提交（P2-09-12 TX-55）。
  - **终态不可重开；Abandoned ID 永不复用；Task rollback/cancellation/Undo 不逆转已提交的 Reservation（P2-09-04 §1.4/1.5）。**
- **哪些操作消耗 ID：** Reservation 插入提交即消耗（allocator 前进 + reservation 插入同一事务，两者要么都提交要么都不发生）。后续 bind/insert/abandon 不再消耗新 ID。
- **失败事务的处理：** ① Reservation 创建事务失败 → 无痕回滚，sequence 未消耗（可重试，`request_key` 幂等防止重复分配，P2-09-04 §6）；② 绑定/插入事务失败 → 已提交的 reservation 保持原状态，重试复用**同一** reservation/identity，绝不分配替代 ID（TX-55 语义）；③ commit 后响应丢失 → 按 `request_key` 对账返回同一分配结果（TX-21/22）。
- **并发重复分配风险：** V1 单进程单写者——`SqliteConnectionAccess` 串行化使 allocator 读改写天然互斥；数据库层由 `UNIQUE(project_id,object_type_id,allocated_sequence)` + `UNIQUE(project_id,object_type_id,allocated_id_value)` 兜底；allocator `next_sequence` 仅在与 reservation 插入相同事务内前进、永不回退（P2-09-11 §4.2）。多连接同文件写由 SQLite 文件锁 + 串行协调兜底（CD-01-F1 已验证模式）。

## 6. Object Definition

- **权威存储：** `core_object_definition`（P2-09-11 §4.2）——每个 identity 至多一行"当前定义"；active Object 恰好一行；draft/abandoned 可无行。无可回放版本链（P2-09-07 §2；P2-09-16 §8 禁止 `shared_object_definition_version`/`object_version`/per-Map copy 表）。
- **Identity↔Definition 引用：** PK/FK `(project_id, object_identity_id)` → `core_object`；一行一身，天然防重。
- **Schema Version：** `definition_schema_version` 必须与该 Object 在 Bound 时钉住的 `object_type_schema_version` 一致（P2-09-18 §10 pin 规则；不支持的版本 fail-closed，OT-05/06）。`core_object.object_type_semantics_id` 同时钉住解释语义；旧 Object 永不被最新 descriptor 静默重释（P2-09-18 §10；P2-09-45 §5.11）。
- **CAS / 更新冲突：** 采用 P2-09-37 §3.3 的 expected-hash CAS 模式——definition 编辑请求携带 `expected_definition_hash`，不匹配即 typed conflict（stale 更新拒绝）；提交时重写 `definition_payload_json` + `definition_hash` + `definition_schema_version`。无 revision 表；事务时间审计元数据（`updated_at_utc_ms`）不是历史流（P2-09-07 §2）。
- **Retroactive 语义（P2-09-07 §2 Accepted + P2-09-16 §2，闭合采纳）：** 定义编辑是 Project 级 authoring，**不是** Command/Event/State Change；任何历史时间的重建都解析当前定义；V1 不提供 as-of 定义查询。运行时 History Command 永不改 definition 字段，Definition 编辑永不覆盖 Map/Branch 运行时值（P2-09-16 §3 单 owner 分类）。
- **与 Geometry Asset 的引用：** 经 `core_object_definition_asset` typed link（owner keys + `asset_id` + `role`，见 §14）。当前 V1 十类型的 definition payload 尚不含正式 geometry Asset 引用（geometry payload 编码与 schema artifact 未冻结，P2-09-19 §12），该 link 属**条件性 typed link，CD-02-B 不建**，启用触发条件见 §17.3。
- **Project-level Shared vs Map-specific：** `core_object.sharing_scope` = `project_shared`（`owner_map_id` 必空）| `map_local`（`owner_map_id` 必非空，CHECK 强制）。map_local 的 definition 只影响 owner Map 及其后代 Branch；project_shared 的 definition 编辑影响所有引用 Map 及其后代 Branch（P2-09-07 §2/§6）。需要不同"共享定义"的 Map 使用 distinct map-local Object，不得为一个 Map 改 Project-shared 定义（P2-09-37 §7.3）。

## 7. Object Type Descriptor / Registry

- **Registry Format V2 的加载与验证边界（P2-09-45 §5/§7，闭合采纳）：** Registry 是**编译期**权威——JSON Schema（P2-09-46）只查结构，registry compiler 查跨记录（artifact 存在性/精确字节/SHA-256/引用矩阵），产出确定性嵌入式 bundle；**运行时只加载编译后 bundle**，无反射插件、无网络 schema fetch、无用户 authored handler；一切未知键/不支持版本/缺失引用 fail-closed + 稳定错误码。
- **`legacy_compact_v1` 与 `typed_v2`（P2-09-45 §4）：** dual-profile。legacy profile 仅限既有十 key（allowlist，`legacy_profile_new_types_allowed=false`），为每 key 钉住 `schema_version=2` + `semantics_id=world-view.object-type.<key>.v1`（已核对 P2-09-26 V0.2 实际值）；typed_v2 要求 15 个完整 block、空 schema 也必须 pin 真实 artifact。两 profile 互斥且 descriptor 内不可缺块。
- **当前权威目录：** P2-09-26 V0.2（format_version=1，十类型，全 allocatable）是现行工作目录；P2-09-26 V0.3 是 V2 migration preview（未激活、不含 Building/Cave）；V2 激活受 P2-09-45 §8 十项验收门约束（P2-09-46 过真实 Draft 2020-12 meta-schema、P2-09-47 34 vectors 生产级执行、十类型语义回归等，全部未执行）。
- **Descriptor 与 Definition/Runtime/Initial State Schema 的关系（P2-09-18 §5）：** descriptor 声明三个独立数据域的 typed schema 引用；definition 属性描述当前共享定义；runtime 属性 History-local；Initial State 属性属 time-zero baseline 路径。三域不得合并为通用属性字典；无通用 `core_object_attribute` 表、无第二 schema 权威（P2-09-18 §11）。
- **Schema 引用固定版本与 hash（P2-09-45 §3.1/§5.3）：** `artifact_id` + `schema_version` + 小写 SHA-256 三元组在 descriptor 内**有意冗余重复**，compiler 必须解析到恰一个 artifact 且三值全匹配；路径仅为 build-time 仓库引用（拒绝对路径/URL/`..`）；运行时永不下载。
- **Registry Compiler 职责：** 结构之外的全部跨记录验证（P2-09-45 §5 十二项：type_key 唯一、legacy pin 匹配 allowlist、schema 引用三元组全匹配、Command/Relationship 引用存在于 catalog 且策略兼容、geometry role/family/dimension 校验、typed link 表/role 匹配实际所有权、child aggregate 留在单一 owner、policy 不越权越时界、pin 有 reader/迁移、fail-closed）。
- **本阶段边界（CD-02-B 不实现 registry compiler / V2 loader）：**
  1. CD-02-B 落地 `core_object_type` 表行（Project 级配置权威：type_key、显示名、allocatable、分配格式配置及版本），Project 创建时播种十内置类型行（P2-09-18 §4.2"Core Domain 播种内置类型行"+ P2-09-21 V0.1 种子目录）。
  2. descriptor 的可执行权威在编译期 registry——CD-02-B 不实现 compiler，十类型语义按 P2-09-26 V0.2 descriptor 的显式声明对待；未知 type_key 不静默映射（OT-01）。
  3. **十种 Object Type 语义兼容（硬约束）：** 十 key = landmass / mountain_range / mountain_peak / river / lake / terrain_region / settlement / road / political_region / point_of_interest；Bound 时钉住 schema_version=2 + 对应 semantics_id；显示名/分配格式可改但永不改写已分配值；任何 schema/semantics 变更需显式迁移（P2-09-18 §10；P2-09-45 §4.1/§6）。
  4. **Building/Cave descriptor 不启用**：P2-09-41 V0.4 不是完整 typed_v2 descriptor（P2-09-45 §4.3），legacy allowlist 不含它们（P2-09-46 `legacy_profile_new_types_allowed=false`）；P2-09-26 V0.3 preview 明确不加不激活。

## 8. ID Allocation / Reservation（CD-02-B 实施契约摘要）

物理设计按 P2-09-04 §4 + P2-09-11 §4.2 闭合：

- `core_object_id_allocator`：PK `(project_id, object_type_id)`；`next_sequence INTEGER NOT NULL CHECK(>=1)`；`allocator_format_version`。仅在 reservation 插入同事务内前进。
- `core_object_id_reservation`：`reservation_id` PK；FK→core_object_type；`allocated_sequence`/`allocated_id_value`（UNIQUE 双约束）；`reservation_state` CHECK（Reserved⇒identity NULL；Bound/Inserted⇒identity 非 NULL；Abandoned 皆可）；`request_key`（提供时 UNIQUE）；`abandon_reason`；时间戳。可选复合 FK `(project_id,object_type_id,object_identity_id)`→core_object + 该列非空时 UNIQUE（每 identity 至多一条 reservation 溯源）。
- 七条硬约束（P2-09-04 §7 逐条继承）：类型内唯一分配；allocator 不回退；一 identity 至多绑一 reservation；一 reservation 至多绑一 identity；reservation/identity 的 Project+Type 必须匹配；状态转换仅限 §5 状态机；identity 行在 reservation 证据保留期间不物理删除。
- 显示语法：`allocated_id_value` 存 TEXT，格式变更永不改写既有值（P2-09-04 §8）。CD-02-B 采用**最小分配格式**：十进制 sequence 文本（`allocation_format_version=1`），逐类型显示语法（前缀等）留待 Object Type 配置设计——见 §18 OD-3。

## 9. Project / Map Registration

- **纳入机制：** `core_map_object_ref`（P2-09-11 §4.1）——Map 对正式 Object 的显式引用/注册；`UNIQUE(project_id,map_id,object_identity_id)`；不复制 `sharing_scope`/`owner_map_id`（scope 从 `core_object` 解析，注册行只是关系事实）。
- **一个 Object 可否被多 Map 引用：** project_shared Object 可被同 Project 任意数量 Map 注册；map_local Object 只能注册到 owner Map（Domain Service 校验 + INSERT/UPDATE 触发器纵深防御，P2-09-11 §4.1）。
- **Project-level Shared Object 的多 Map 存在语义：** 同一共享对象在各 Map 可有独立 membership/placement/运行时状态；一个 Map 的 membership 变更不创建/删除共享身份、不影响其他 Map（P2-09-07 §3 验收 6；P2-09-16 §3）。
- **Registration ≠ Initial State membership：** 注册只表达"Map 纳入"；time-zero 在场另经 `core_map_initial_object` 表达。注册而不在 Initial State = 合法（对象在 post-zero 历史中出现，P2-09-16 §6 末验收 4）；`core_map_object_ref` 不得被当作初始成员表使用。
- **time 0 不存在、后续历史出现的表达：** P1（active identity）+ P2（注册）+ 无 P3 行 + 未来 post-zero `core.object.create` Command（CD-03+）。本阶段只落地 P1/P2/P3 的持久化能力；P4 的 Command 路径留待 Command 阶段，本闭合仅锁定"不得用改 Initial State 或改 definition 来伪造历史时间内出现"的红线。

## 10. Map Initial State

- **唯一权威：** 每 Map 恰好一个逻辑 Initial State——`core_map_initial_state`（对 `(project_id,map_id)` 唯一）+ `core_map_initial_object`（复合 PK `(initial_state_id, object_identity_id)`）。Initial State **不是** Command/Event/post-zero State Change（P2-09-11 §4.1；P2-09-15 §3/§4.1/§6/CMD-05：time-zero 经 Command 路径创建对象被拒绝）。
- **Payload 结构（结构层闭合；内容层见 §18 OD-1）：**
  - Map 级行：`baseline_schema_version` + `content_fingerprint`（规范化序列化的确定性哈希）+ 时间戳。
  - 对象级行：`initial_membership_payload_json`（该 Map 的 time-zero 初始事实——membership/placement/初始值/必需引用，P2-09-07 §3）+ 可选初始 placement/relationship payload 与 schema version。
  - **不是共享定义副本**（P2-09-07 §3；P2-09-37 §7.3）：payload 只含 Map 特有初始事实，共享字段解析到 `core_object_definition`。
  - 大二进制内容不入 JSON；正式几何 payload 若未来启用则经 `core_map_initial_object_asset`（当前**不启用**，P2-09-42 §11.1/Gap Audit §4.7）。
- **Membership 与 placement：** membership（该对象在 time-zero 在场）为必选语义；placement/context/初始值为 Map 特有事实，按 P2-09-24 各类型"Initial State attributes"候选列最小化记录。未启用 typed schema 前，payload 内容校验为结构级（canonical JSON + fingerprint），typed 校验待 descriptor Initial State schema artifact（§18 OD-1）。
- **独立 Initial State Object Asset link：** 不需要（决定继承 P2-09-42 §11.1/§12 + Gap Audit §4.7 + P2-09-43 §10/§12：当前 Initial State payload 只含 membership/placement）。若未来 payload 引入独立正式 geometry Asset 才 revisit——本闭合把它登记为条件性 typed link 的启用触发条件（§17.3）。
- **与后续 Command 的边界：** time-zero 一切（成员/placement/属性/初始结构关系）走 baseline authoring 路径；post-zero 一切走 Command 路径（CD-03+）。改 Initial State 不等于伪造历史变化：baseline 编辑使受影响 Checkpoint/Branch 失效并按 fail-fast 重建，既有 Command/Event 保留且在新 baseline 下重新验证（P2-09-07 §3/§6）。
- **CAS / immutable snapshot：** Initial State 是**可变的逻辑基线**（P2-09-11 §4.1），不是不可变快照链；CAS 以 `expected content_fingerprint` 守卫 baseline 编辑（模式同 Definition CAS）。不建 revision/version 表（P2-09-16 §8）。不可变快照若未来需要，经 Asset Store 机制另行设计——本阶段不做。
- **Branch 如何继承：** Branch 继承 root Map baseline，**不复制** Initial State、不产生第二 time-zero（ADR-002/005；P2-09-07 §3）。继承边界为闭区间 `effective_time <= parent_at`；Branch 自身变更严格 `> parent_at`（ADR-005）。任何分叉时间（含 day 0 之后）的 Branch 都继承同一 baseline；Baseline 归属 Map 而非 History 路径，世系解析复用 CD-01 的 `core_history`/`core_branch` 结构。
- **Checkpoint 如何感知：** 重建输入指纹至少包含 Map Initial State（`content_fingerprint` + baseline_schema_version）及其引用的不可变 Asset 身份/hash、可达共享定义（identity + schema version + canonical 内容 + asset hash）、世系路径与 fork 边界、Command/Event 及其 payload/semantics 标识（P2-09-16 §4；P2-09-11 §4.6）。Baseline 编辑 → 受影响 Map 及其后代 Branch 的 Checkpoint 置 stale → 指纹不匹配的重建结果不得发布为 current（P2-09-16 §5 in-flight 协议）。`core_checkpoint` 表属后续阶段；本闭合锁定指纹组成契约。

## 11. History / Branch Integration

- Map 与 Branch 统一为 History（ADR-002）：`core_history`（map_root / branch_owned）+ `core_branch`（parent_history_id + parent_at 闭边界）——CD-01 已实现，本闭合不改动其结构。
- Initial State 挂在 Map（`core_map_initial_state.project_id+map_id`），不挂在 root History；Branch 经世系解析到达 root Map 即解析同一 baseline（§10）。
- Shared Object 可存在于 Project scope 而不属于任何 Map 的 Initial State（§9）；当前只有一个被激活的 Map，其他共享 Map 静默存在（P2-09-07 §4）。
- Definition 编辑的受影响范围 = 所有引用/可解析该对象的 Map 及其后代 Branch 路径；Initial State 编辑的受影响范围 = 该 Map 及其根下所有 Branch；Map-local Command 编辑只影响所属路径（P2-09-16 §6 表，闭合采纳）。
- 分叉规则九条（ADR-005）与本闭合无冲突；本闭合新增约束：Branch 创建不触碰 Initial State/Definition/Reservation 的任何行（继承是解析时语义，不是写入时复制）。

## 12. Geometry / Asset Ownership

- **所有权（P2-09-19 §2/§3；ME-05-02 §2/§3，闭合采纳）：** Geometry Component 属于顶层语义 Object；组件身份由 Core Domain 在首次正式创建时生成 opaque ID，scope 于所属 Object，不是 Object ID、不消耗 allocator、移除后永不复用、不得跨 Object 转移（跨 Object 转移需独立显式领域操作，V1 不做）。每组件在其 Object Type descriptor 内恰有一个声明 role；role 不被普通几何编辑静默改变。
- **无组件表：** V1 不新增 `core_geometry_component` 表（P2-09-19 §3.2；P2-09-11 V0.8 变更摘要 3；ME-05-02 §3.1）；组件身份/role/坐标 context 等逻辑 manifest（10 个规范字段，P2-09-19 §6.2）存于 owner typed payload：共享当前 definition payload（base geometry）/ Map Initial State payload（time-zero）/ Command payload（post-zero）。
- **Asset 归属三分（本闭合对 §F 的直接回答）：**
  | 用途 | Asset 载体 | typed link | 当前状态 |
  |---|---|---|---|
  | Definition Geometry（共享 base geometry） | 不可变 Core Asset（每组件一 Asset，推荐粒度，P2-09-19 §6.1） | `core_object_definition_asset`（owner keys + asset_id + role） | **条件性，CD-02-B 不建**（十类型 definition 暂无正式 geometry 引用） |
  | Command Geometry（post-zero 几何） | 同上 | `core_command_asset` | Command 阶段必需（P2-09-13 §3.2）；CD-02-B 不建 |
  | Initial State Asset | 同上 | `core_map_initial_object_asset` | **不启用**（当前 payload 无独立 geometry Asset 引用；revisit 触发条件见 §17.3） |
- **Geometry Component identity ↔ Asset payload 映射：** owner payload 的 manifest 持有 `geometry_component_id → asset_id + expected content_hash` 映射（权威）；typed link 行只含 owner keys + `asset_id` + `role`，**不得**塞入 content hash 或 component ID（P2-09-37 §17/§12；任务 §4.6）；两侧在 Core Domain 验证下必须一致；权威 hash 从 `core_asset` 解析比对。
- **发布/写入/失败恢复事务要求（P2-09-12 TX-50/56；ME-05-02 §8）：** 先发布不可变字节（staging → hash → 原子发布）→ 再开 DB 短事务写 typed link 与 owner payload → "file existence never implies successful publication"；失败后文件成为未引用孤儿，仅在安全对账证明未用后清理；被任何 formal definition/Initial State/Command/Checkpoint/audit 引用期间 Asset 字节必须保持可用。
- **split/merge：** 从最小 V1 catalog 推迟（P2-09-19 §2.6/§7.5；P2-09-15 §4.2；ME-05-02 §6）；几何 split/merge 绝不隐式创建/删除/合并 Object identity；不得用 add/remove 链式模拟。in-place 调整/refinement 保留组件 ID 与 role；add 分配新 ID；remove 只结束所选 History 的 membership（组件与 Object 身份均不复用、不退役）。
- **ME-05 一致性：** 四个正式写 context（Shared Definition / Map Initial State / post-zero Command / Source Field Dataset）与 ME-05-02 §5 完全一致；Editor 每次写必须显式选 context；Map Engine 只读已发布字节；Candidate 几何不得直接作正式 Command Asset；正式矢量编辑 V1 为 2D working plane。

## 13. Persistence / Transaction Boundaries

### 13.1 复用的机制（CD-01-F1 继承）

- 单连接统一协调：所有新适配器经 `ISqliteConnectionProvider.GetAccess()` 接入 `SqliteConnectionAccess`；绝不绕过协调器持有裸连接（CD-01-F1 Handoff 红线 1）。
- 事务生命周期整体在一个 `Execute` 临界区内；禁止跨 await 持有事务对象（红线 2）。
- 连接策略不变：`Pool=False`、`foreign_keys=ON`、`busy_timeout=5000`、journal 默认（WAL 仍属 open gate）。
- 多步复合读用 `InSnapshotScope`；写路径按任务粒度保持短事务（禁止 LLM/交互/长校验/文件 staging 期间持写事务，P2-09-12 §2）。

### 13.2 CD-02-B 新写入路径的事务契约

| 写路径 | 原子边界（单事务内全部完成） |
|---|---|
| Reserve ID | allocator 读改写（next_sequence 前进）+ reservation 插入；`request_key` 幂等 |
| Bind | `core_object` draft 行插入（含 schema/semantics pin）+ reservation 状态 Reserved→Bound + identity 回写 |
| Insert（正式创建） | identity draft→active + reservation Bound→Inserted + `core_object_definition` 首行 + 必需 `core_map_object_ref` + 必需 `core_map_initial_object`（若 time-zero 在场） |
| Definition 编辑 | CAS 校验（expected hash）+ payload/hash/version 重写；Project-shared 编辑另按 §10 失效受影响 Checkpoint 元数据（checkpoint 表落地后） |
| Initial State 编辑 | CAS 校验（expected fingerprint）+ baseline/成员行重写 |
| Map 注册 / 取消注册 | `core_map_object_ref` 单行写 + scope 规则校验 |

每个边界失败整体回滚、不留半成品；Bound 之前的失败永不消耗已提交的 reservation；重试复用同一 reservation/identity（TX-55）。

### 13.3 对 CD-01-F1 条件 1 的正式响应（重要）

CD-02-B 引入本仓库首批 UPDATE 路径（reservation 状态转换、`core_object.lifecycle_status` draft→active、definition/baseline 重写），**打破 CD-01-F1 §15 条件 1 的 V1 append-only 前提**。按该条件要求：所有复合写（读-校验-写、跨行状态机转换）必须整体包入单一事务临界区（`SqliteConnectionAccess.Execute` 内 Begin→校验→写→Commit/Rollback），并补充负向测试（中途约束违规→整体回滚→无状态机半成品）。状态机转换（Reserved→Bound 等）在事务内重读当前状态行校验前置状态，不依赖调用方传值。此为 CD-02-B 验收项（见 Handoff §6）。

## 14. Validation Rules（CD-02-B 必须实现的校验清单）

1. **R-身份：** `object_identity_id` 为 canonical 小写连字符 UUID；`(project, identity)` 唯一；`(project, type, identity)` 唯一。
2. **R-钉住：** Bound 时 `object_type_schema_version` 与 `object_type_semantics_id` 非空且与该类型 descriptor 声明一致；不支持版本 fail-closed。
3. **R-scope：** `project_shared⇒owner_map_id IS NULL`、`map_local⇒owner_map_id 非空`（CHECK + 服务双保险）。
4. **R-注册：** map_local 只可注册 owner Map；`core_map_initial_object` 行必须有对应 `core_map_object_ref` 行（跨表不变量，Service + 完整性测试）。
5. **R-状态机：** reservation 五种非法转换全部拒绝；identity lifecycle 只允许 draft→active、draft→abandoned、active→retired（retired 为独立受控操作，V1 不开放 UI 入口）。
6. **R-成员前置：** draft/abandoned identity 不得进入 membership/Initial State；active 且未注册的对象不得写入 Initial State 成员行。
7. **R-定义单行：** active Object 恰一行 definition；hash 与 payload 一致；schema_version 与 pin 一致；CAS 不匹配拒绝。
8. **R-baseline 唯一：** 每 Map 恰一行 initial_state；fingerprint 由 Core Domain 规范化序列化计算（键序/数值表示固定；不含 created_at/updated_at/审计文本，P2-09-16 §4 规范化要求）。
9. **R-fail-closed：** 行数据不可解析/不变量破坏 → `CoreDomainDataException`；未知 type_key → 拒绝；未知字段 → 拒绝（payload 结构层）。
10. **R-事务：** §13.2 每个写路径的事务原子性 + 中途失败回滚测试。

## 15. Migration Impact

- 现有 4 条迁移（001–004）不动；CD-02-B 新迁移按依赖序追加（编号示意，非冻结文件名，P2-09-13 §5）：
  1. `core_object_type`（依赖：core_project）
  2. `core_object` + `core_object_definition` + `core_object_id_allocator` + `core_object_id_reservation`（依赖：core_object_type、core_map）
  3. `core_map_object_ref`（依赖：core_object）
  4. `core_map_initial_state` + `core_map_initial_object`（依赖：core_map_object_ref）
- 迁移机制照旧：`CoreDomainSchema.Migrations` 依赖序 + 共享 ledger + checksum fail-closed；并发应用/失败回滚语义已由 CD-01-F1 测试锁定。
- 对既有行的迁移风险：零——所有新表均为新命名空间，不 ALTER 既有 6 表；`core_project` 行不因类型播种而改变（类型行是新增插入）。
- 明确不建（本阶段）：`core_command` 系、`core_event` 系、`core_command_object_ref`、`core_command_asset`、`core_asset`、`core_checkpoint`、`core_map_field_dataset/revision`、`core_state_change`/materialization 两表、`core_event_object_ref`、`core_object_definition_asset`、`core_map_initial_object_asset`、全部 AI 表、任何 Building/Cave 或 per-child/polymorphic 表。

## 16. Compatibility

- 十内置类型语义 pin（schema_version=2 + semantics_id）在 Bound 时持久化；未来 registry/格式演进不改变既有 Bound Object 的解释（P2-09-18 §10；P2-09-45 §6/§11）。
- Registry Format V1→V2 迁移（若未来激活）只改编译目录，不改持久化表/migration/Object ID/reservation/Command/History/共享定义/Checkpoint（P2-09-45 §11）。
- CD-02-B 新表全部遵循既有约定：UUID TEXT PK、snake_case、`*_utc_ms`、无 ON DELETE CASCADE、复合 FK 防跨库误引用（`CoreDomainSchema.cs` 头注约定延续）。
- AF-02 端口与 Application 层不需要变更；新能力以新的 Domain 端口方法/服务呈现（沿用 CD-01 模式：Domain 拥有端口，Infrastructure 实现）。

## 17. 与 Building/Cave 采纳包的衔接（不激活，只留边界）

1. 不为 Building/Cave 建任何专用表；child aggregate（Level/Room/Space/Chamber…）未来进 owner typed payload，child ID 永不进 `core_object`/allocator（P2-09-37 §3/§10；P2-09-42 §3.3）。
2. `core_object_definition_asset` 在 Building/Cave adoption profile 下转为必需——该 profile 未采纳，故本阶段该表不建；启用触发条件：**任一已注册 descriptor 的 definition schema 正式引用 geometry Asset**（P2-09-13 §3.2 条件性规则）。
3. `core_map_initial_object_asset` 启用触发条件：**Initial State payload schema 引入独立正式 geometry Asset 引用**（P2-09-42 §11.1 "revisit only if that payload changes"）。
4. Building/Cave descriptor 的 typed_v2 化、55 vectors、Registry V2 激活全部 defer（P2-09-43 §3 S1–S10 顺序中 CD-02-B 仅覆盖其 S0/S9 的 Core Domain 基础部分）。
5. RoadEndpoint 缺口（ME-03-H H-02 P0）不阻塞本阶段；它阻塞的是 formal Road connectivity，届时走 Relationship/Command 阶段。

## 18. Open Decisions

| # | 问题 | 现状与建议 | 关闭条件 |
|---|---|---|---|
| OD-1 | **Initial State baseline payload 的 typed schema**（`baseline_schema_version` 词汇、各类型初始属性的精确 schema） | 结构层已闭合（§10）；typed 内容层不可从既有决策推导——descriptor Initial State schema artifact 尚不存在（P2-09-45 §9 留白；P2-09-24 只有候选字段目录）。CD-02-B 落地结构 + canonical fingerprint + 结构级 unknown-field 拒绝，payload typed 校验不得 invented | Registry V2 descriptor schema artifacts 定稿（或用户单独裁决最小 V1 initial-state schema） |
| OD-2 | **Definition payload 的 typed 校验权威** | 同上：CD-02-B 仅强制结构级不变量（canonical JSON、hash、schema_version 与 pin 一致、单行）；typed 字段校验等 schema artifact 钉死后启用，届时无需 schema 变更（校验是运行时行为） | 同 OD-1 |
| OD-3 | **Object ID 显示语法** | CD-02-B 采用最小分配格式（十进制 sequence 文本，`allocation_format_version=1`）作为占位实现；逐类型前缀/格式为 Object Type 配置契约（P2-09-04 §8 / P2-09-18 §14.3 未关门）。已分配值永不被格式变更改写 | Object Type allocation-format 设计关门（不阻塞 CD-02-B） |
| OD-4 | **十内置类型行的播种时机**（Project 创建事务内 vs 首次需要时） | 建议：Project 创建事务内播种（P2-09-18 §4.2 "Project 创建时 Core Domain 播种内置类型行"字面支持前者；且避免创建流程多事务）。CD-02-B 按建议实施；若用户倾向延迟播种属实施细节可调 | 用户裁决（非阻塞） |
| OD-5 | **`core_map.lifecycle_status` 词汇冻结** | 维持 CD-01 open gate（服务写 `'active'`、无 CHECK）；与 Object 的 lifecycle 无关 | P2-09-11 §10 门 5 或后续 ADR |

> 以上 Open Decision 均不阻塞 CD-02-B 的结构实施；OD-1/OD-2 是有意留给 descriptor/Registry V2 阶段的依赖门，不得在 CD-02-B 内自行发明 typed schema。

## 19. Deferred Scope（明确不做）

Command/Event/Order-constraint/Command-Event 持久化与 Command 描述符实现；`core.object.*`/`core.geometry.*` Command 路径（P4 存在层）；`core_asset` + 三个 typed asset link 表；`core_checkpoint` 与指纹的物理落地；Relationship 持久化（P2-09-20 §10 明确 V1 无关系表）；Geometry Component 的正式几何 payload 编码与 split/merge；Registry compiler / Registry V2 激活 / Building-Cave descriptor；Field Dataset/Revision 表；全部 AI Assistant 表；WAL/备份与 P2-09-12 完整证明套件；macOS 验证。

## 20. Acceptance Criteria（CD-02-A 自验，全部满足）

1. ✅ 已检查当前 master 真实代码（§3 盘点表全部经代码阅读/grep/测试实证，非文档推断）。
2. ✅ 已明确 Object Identity / Definition / Registration / Presence 的区别（§4.3 四层 + §5/§6/§9）。
3. ✅ 已明确 Initial State 与 History/Branch 的关系（§10/§11）。
4. ✅ 已明确 Geometry Component 与 Asset ownership（§12，与 ME-05-02/P2-09-19 一致）。
5. ✅ 已明确 Registry V2 兼容性边界（§7；V2 未激活、十类型 pin、Building/Cave 排除）。
6. ✅ 已识别全部未决业务问题（§18 五项 Open Decision，均为真实留白而非发明规则）。
7. ✅ 已形成最小化 CD-02-B 数据库实施契约（§21 + Handoff）。
8. ✅ 与既有正式架构决策无冲突（自检见 §22 验证记录；继承约束逐条注明出处）。
9. ⏳ 新增文档提交 GitHub（随本次提交完成）。
10. ⏳ 文档索引与实施日志同步（随本次提交完成）。

## 21. CD-02-B Implementation Contract（最小实施契约）

### 21.1 新增表（8 张，全部新命名空间）

| 表 | 关键约束（完整列定义以 P2-09-11 V0.11 §4 为准） |
|---|---|
| `core_object_type` | PK object_type_id；FK project_id；UNIQUE(project_id,object_type_id)、(project_id,type_key)；type_key/显示名/allocatable/allocation 格式配置+版本 |
| `core_object` | PK object_identity_id；FK→core_object_type；owner_map_id 复合 FK；UNIQUE(project_id,object_identity_id)、(project_id,object_type_id,object_identity_id)；CHECK scope/owner 配对；lifecycle CHECK(draft/active/abandoned/retired)；schema_version+semantics_id 钉住列 |
| `core_object_definition` | PK/FK (project_id,object_identity_id)→core_object；definition_schema_version/payload_json/hash；单行 |
| `core_object_id_allocator` | PK (project_id,object_type_id)；next_sequence CHECK(>=1)；allocator_format_version |
| `core_object_id_reservation` | PK reservation_id；FK→core_object_type；UNIQUE×4（sequence/id_value/request_key/identity 溯源）；state CHECK 状态机；可选复合 FK→core_object |
| `core_map_object_ref` | PK map_object_ref_id；复合 FK→core_map、→core_object；UNIQUE(project_id,map_id,object_identity_id) |
| `core_map_initial_state` | PK initial_state_id；对 (project_id,map_id) 唯一；baseline_schema_version + content_fingerprint |
| `core_map_initial_object` | 复合 PK (initial_state_id,object_identity_id)；复合 FK→initial_state、→core_map_object_ref、→core_object；membership payload + 可选 placement payload/version |

不建：`core_asset`、三个 typed asset link、command/event 系、checkpoint、field dataset 系、AI 系、任何 Building/Cave 或 polymorphic 表（§15）。

### 21.2 Domain 交付

- 记录类型：`ObjectTypeRecord`、`ObjectRecord`（含 pin/scope/lifecycle）、`ObjectDefinitionRecord`、`ReservationRecord`、`MapObjectRefRecord`、`MapInitialStateRecord`、`InitialObjectRecord` + 规则类（ReservationRules 状态机、ObjectRules scope/pin/lifecycle、InitialRules membership 不变量）+ 服务（ObjectTypeSeedingService、ObjectIdReservationService、ObjectCreationService、ObjectDefinitionService、MapRegistrationService、MapInitialStateService）。
- 端口扩展：`ICoreDomainDatabase` 增补上述能力端口方法（沿用"结构写=单事务计划"模式：`ReserveObjectIdPlan`/`BindReservationPlan`/`InsertObjectPlan`/`EditDefinitionPlan`/`EditInitialStatePlan` 等，事务边界归适配器）。
- Migration 常量追加进 `CoreDomainSchema.Migrations`（依赖序，§15）。

### 21.3 Infrastructure / 测试交付

- 适配器实现新端口（经协调器；复合写单事务临界区，§13.3）。
- 测试最低集：状态机全转换+非法转换拒绝；allocator 并发/回滚不回退；`request_key` 幂等；TX-55 式绑定/插入中途失败注入→无半成品、重试复用同 ID；scope/注册/成员前置负向；CAS 冲突；fingerprint 确定性（同内容同指纹）；新表 FK/UNIQUE/CHECK 负向；迁移失败回滚与幂等。

### 21.4 明确的实施红线

1. 不实现任何 Command/Event 写路径；Initial State 编辑不得走 Command 形态。
2. 不实现 registry compiler；type 语义以 P2-09-26 V0.2 descriptor 声明为界。
3. 不发明 Initial State / Definition 的 typed schema（OD-1/OD-2）。
4. 不为 Building/Cave 或 child aggregate 建表/建类型行。
5. 新适配器必须经 `SqliteConnectionAccess`；事务不越临界区（CD-01-F1 红线）。
6. 不引入 WAL、事件溯源、分布式事务、第二套 Object 状态表。

## 22. 验证记录（本阶段执行项）

1. 引用文件路径有效性：见 Handoff §8 检查结果（全部存在）。
2. Schema 文件合法性：P2-09-46 V0.2 JSON 经 Python `json.load` 解析通过；P2-09-26 V0.2 十类型 schema_version/semantics_id 经脚本核对（§7 引用值）。
3. Mermaid 图结构：语法检查通过（见关系图文档末尾验证记录）。
4. 新旧文档交叉引用：全部引用路径实测存在。
5. 架构决策冲突检查：与 ADR-001~005、ADR-0003、P2-09-04/07/11/13/15/16/18/19/20/42/45、ME-05-02、CD-01/CD-01-F1 逐项核对，无冲突；CD-01-F1 条件 1 的响应显式化于 §13.3。
6. Migration 与实际代码一致性：当前 4 条迁移与 `CoreDomainSchema.cs` 逐条一致；CD-02-B 增量为纯追加。
7. CD-01-F1 事务机制继承：§13.1/§13.3 逐条落实为 CD-02-B 契约与验收项。
8. **本阶段无生产代码变更**（不改 `WorldView/` 任何源文件）；基线测试实测 172/172 通过，作为审查时点的真实基线记录。
