# CD-02-B — Object Persistence & Initial State Foundation Implementation Report V0.1

- **Date:** 2026-11-22
- **Baseline:** master `a773698`（CD-02-A 交付后）→ 本阶段交付 `95f22e4`
- **Companion:** [CD-02-A 主文档](CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md)（权威边界）、[CD-02-A Developer Handoff](CD-02-A_DEVELOPER_HANDOFF_v0.1.md)（执行契约）、[CD-02-B Developer Handoff](CD-02-B_DEVELOPER_HANDOFF_v0.1.md)（下阶段交接）
- **结论：PASS WITH CONDITIONS**（条件与遗留项见 §7；macOS 未验证）

---

## 1. 交付范围

按 CD-02-A §21 实施契约与 Handoff §3 五个切片完成 Object Persistence & Initial State Foundation：

| 切片 | 交付 | 状态 |
|---|---|---|
| S1 表结构 | 4 条新迁移（005–008），8 张表 | ✅ |
| S2 播种 + Reservation | Project 创建事务内十类型 + allocator 播种（OD-4）；Reserve/Bind/Abandon 状态机 + request_key 幂等 | ✅ |
| S3 Object 正式创建 | Bound→Inserted 原子（identity active + definition 首行 + 注册 + 可选 Initial State 成员，TX-55） | ✅ |
| S4 Definition + 注册 | expected-hash CAS 编辑；注册/取消注册（scope 校验 + 触发器纵深防御） | ✅ |
| S5 Initial State | 每 Map 唯一 baseline、成员全量重写、CAS(expected fingerprint)、Domain 规范化指纹 | ✅ |

**明确不做（继承 CD-02-A §15/§19）：** Command/Event、`core_asset` + typed asset links、checkpoint、Registry compiler/V2 loader、Geometry payload、Relationship、Building/Cave descriptor、AI 表。OD-1/OD-2 未发明 typed schema（payload 仅结构层校验：canonical JSON + hash + unknown-field 由 canonical 化保持）。

## 2. 数据库 Schema（最终）

迁移追加（编号接续 CD-01 的 001–004；不 ALTER 既有表）：

| 迁移 | 表 | 关键约束 |
|---|---|---|
| `005_core_object_type` | `core_object_type` | PK object_type_id；UNIQUE(project_id,object_type_id)、(project_id,type_key)；allocatable CHECK(0,1) |
| `006_core_object_identity` | `core_object` | PK object_identity_id；UNIQUE(project,identity)、(project,type,identity)；CHECK scope/owner 配对 + lifecycle(draft/active/abandoned/retired)；owner_map_id 复合 FK |
| 〃 | `core_object_definition` | PK/FK (project,identity)→core_object（单行语义）；schema_version/payload_json/hash |
| 〃 | `core_object_id_allocator` | PK (project,type)；next_sequence CHECK(>=1)；allocator_format_version |
| 〃 | `core_object_id_reservation` | PK reservation_id；CHECK 状态机（Reserved⇒identity NULL、Bound/Inserted⇒非空、Abandoned 皆可）+ abandon_reason 仅 Abandoned；UNIQUE×4（(project,reservation_id)/(project,type,sequence)/(project,type,id_value)/(project,request_key)/(project,type,identity)） |
| `007_core_map_object_ref` | `core_map_object_ref` | UNIQUE(project,map,identity)；map_local 跨 Map 注册 INSERT/UPDATE 触发器纵深防御 |
| `008_core_map_initial_state` | `core_map_initial_state` | UNIQUE(project,map)（每 Map 恰一 baseline）；baseline_schema_version + content_fingerprint |
| 〃 | `core_map_initial_object` | 复合 PK(initial_state,identity)；复合 FK→initial_state、→core_map_object_ref（P3⇒P2 数据库层强制）、→core_object；placement payload/version 成对 CHECK |

迁移机制照旧：`CoreDomainSchema.Migrations` 依赖序 + 共享 ledger + checksum fail-closed；并发应用恰一次、失败回滚由既有 CD-01-F1 测试模式覆盖（计数断言 4→8 更新）。

## 3. Domain 交付（`WorldView.Domain/Objects/`）

| 文件 | 内容 |
|---|---|
| `ObjectRecords.cs` | 7 个持久化记录 + ObjectSharingScope/ObjectLifecycleStatus/ReservationState 枚举（fail-closed 解析） |
| `BuiltinObjectTypes.cs` | 十类型种子目录（P2-09-21 V0.1）+ pin 值（P2-09-26 V0.2：schema_version=2、`world-view.object-type.<key>.v1`）+ `ObjectAllocationFormat`（OD-3 最小格式：十进制 sequence 文本，version=1） |
| `CanonicalJson.cs` | canonical JSON（键 ordinal 排序、无空白、整数 Int64/非整数 double 往返）+ SHA-256 + `InitialStateFingerprint`（组成版本标记 + baseline_schema_version + identity 排序 + membership/placement canonical payload；不含审计字段） |
| `ObjectRules.cs` | R-scope/R-状态机（身份级）/R-钉住/R-定义/type_key 规范化；`ValidateKnownPin` 读侧 fail-closed |
| `ReservationRules.cs` | Reservation 状态机（4 条合法转换；5 条非法拒绝）+ request_key/abandon_reason 规范化 |
| `ObjectPlans.cs` | 端口计划/请求记录（ProjectCreationPlan/ObjectIdReservationRequest/ObjectBindRequest/ObjectInsertPlan/ObjectDefinitionEditPlan/MapObjectRegistrationPlan/InitialObjectWrite/EditInitialStatePlan） |
| `ObjectTypeSeedingService.cs` | 种子行构建（十行全 allocatable、最小分配格式） |
| `ObjectIdReservationService.cs` | Reserve（type_key 解析 + 幂等）/Bind（生成 identity + 目录推导 pin + scope 校验）/Abandon |
| `ObjectCreationService.cs` | InsertObject（TX-55 前置校验：仅 Bound、draft、definition schema 与 pin 一致、P3⇒P2、placement 成对） |
| `ObjectDefinitionService.cs` | EditDefinition（expected-hash CAS；canonical 化 + hash 在 Domain 计算） |
| `MapRegistrationService.cs` / `MapInitialStateService.cs` | 注册（scope/重复校验）；Initial State CAS（建立 expected=null、编辑 expected 必须匹配当前指纹；成员前置重验） |

**端口扩展（`ICoreDomainDatabase`）：** Object Type 读取×3、Reservation×4、Object/Definition×4、注册×4、Initial State×3；新增 `CoreDomainConflictException`（状态/CAS/重复冲突，区别于数据损坏）。`InsertProject(ProjectRecord)` → `InsertProject(ProjectCreationPlan)`（身份 + 十类型行 + allocator 行同事务，OD-4）。

## 4. Infrastructure 交付

`SqliteCoreDomainDatabase` 拆分 partial（原文件 Project/Map/Branch + 新 `SqliteCoreDomainDatabase.Objects.cs`）。全部写路径模式：**单临界区 Begin → 事务内重读前置状态校验（状态机 UPDATE 不依赖调用方传值）→ 写 → Commit/Rollback**（CD-02-A §13.3 对 CD-01-F1 条件 1 的正式响应）。要点：

- Reserve：事务内 request_key 对账（TX-21/22）→ allocator 读改写（next_sequence 同事务前进）→ reservation 插入；
- Bind/Abandon：事务内 `ReservationRules.ValidateTransition`（重读行）→ identity 行写入/墓碑降级（仅 draft→abandoned）；
- InsertObject：事务内重读 reservation(Bound)+identity(draft) → 注册（scope 校验 + 触发器兜底）→ definition 首行（canonical+hash）→ 成员行 + 基线自动建立 + `InitialStateFingerprint.Compute` 写回 → identity→active + reservation→Inserted（UPDATE 带前置条件，行数≠1 即回滚）；
- EditObjectDefinition：事务内 CAS 比对（无并发窗口）；
- EditMapInitialState：事务内 CAS → 成员 active+注册重验 → 全量 delete+insert → 指纹重算写回；
- 读侧 fail-closed：`FindObject` JOIN type 行校验已知 pin；`ReadObjectDefinition` 校验 hash≡payload；行映射枚举解析拒绝未知值。

## 5. 测试与真实结果

| 套件 | 数量 | 结果 |
|---|---|---|
| 全量（Domain 129 + Application 44 + Infrastructure 95） | **268** | ✅ 268/268（Debug，真实执行） |
| 新增 Domain 测试 | 69 | ✅（规则表/canonical JSON/指纹确定性/服务流 Fake 路径） |
| 新增 Infrastructure 测试（`Cd02ObjectPersistenceTests`） | 27 | ✅（真实 SQLite，见下） |
| Release 构建 | — | ✅ 0 错误 |

Infrastructure 关键证据（全部真实执行）：

- **播种**：创建 Project → 十类型行 + 十 allocator 行；重开校验通过；`type_key` UNIQUE 负向。
- **Reservation**：顺序分配 1→2→3（放弃后 allocator 不回退）；request_key 幂等（重试同 reservation、不新分配、allocator 已前进）；跨类型同 request_key 拒绝；**8 线程并发 Reserve** → 8 个互异 sequence（恰为 1..8）+ 互异 id_value。
- **状态机**：Reserved→Abandoned→(Bind/Abandon 拒绝)；Bound→Abandoned→(Bind 拒绝)；Bound→Inserted→(Abandon/Bind 拒绝)；Abandoned 行事务内 Bind 被拒。
- **TX-55 中途失败注入**（ABORT 触发器）：definition 插入失败 → identity 仍 draft、reservation 仍 Bound、零半成品；移除触发器重试 → 复用同一 reservation/identity 成功。Bind 路径同理。
- **scope**：map_local 跨 Map 注册被服务拒绝**且**被 007 触发器拒绝（手工 INSERT ABORT）；project_shared 多 Map 注册成功；重复注册拒绝；有成员行时 Unregister 拒绝。
- **DB 约束负向**（raw SQL）：scope 配对×2、未知 lifecycle、Reservation 状态×2、abandon_reason、UNIQUE(sequence/id_value/request_key)、成员行无注册行复合 FK、每 Map 第二条 baseline UNIQUE——全部拒绝。
- **Initial State**：指纹确定性（键序/空白差异同指纹）；CAS stale 凭据拒绝；全量替换生效；draft 成员拒绝；placement payload/version 不成对拒绝。
- **Definition**：单行；CAS stale 拒绝；hash 篡改 → 读侧 fail-closed。
- **读侧 pin fail-closed**：篡改 `object_type_schema_version=99` → `FindObject` 抛冲突。
- **协调器继承**：并发 Reserve + 并发类型读取混合 10 次/60 次/60 次 → 无失败、无撕裂（10 行 reservation、allocator=11）。

测试中发现并修复的缺陷：`ReserveObjectId` 插入后按 request_key 读回（无 key 时参数为 null 崩溃）→ 改为按 reservation_id 读回。

## 6. 与既有决策的一致性

- 四层存在分离（P1 core_object / P2 core_map_object_ref / P3 core_map_initial_object / P4 未来 Command）逐层独立落表；registration ≠ membership（§9/§10）；draft/abandoned 不得进入 membership/注册（R-成员前置）。
- Reservation 语义完整继承 P2-09-04（七条硬约束 + 重试复用同一 reservation + Abandoned ID 永不复用）。
- Initial State 是可变逻辑基线（非快照链）；CAS 以 fingerprint 守卫；编辑是 baseline authoring 路径，非 Command（红线 9）。
- Branch 不复制 baseline（本阶段无 Branch 触碰 Initial State/Definition/Reservation 的任何路径；继承是解析时语义）。
- 无第二权威：Application 层零改动；UI 无入口（沿用 CD-01 先例，见 §7 条件 2）。

## 7. 条件与遗留项

1. **P2-09-12 完整事务证明套件未执行**（同 CD-01/CD-01-F1 既有条件；本阶段以 ABORT 触发器注入 + 并发测试覆盖了对应风险面，但未逐条执行 55 个 TX 场景）。macOS 未验证。
2. **无 Application 用例 / UI 入口**（同 CD-01 Map/Branch 服务先例；组合根未接线——按 Handoff §4 本阶段无 UI 入口要求）。
3. **OD-1/OD-2 未关闭**（typed payload 校验等 Registry schema artifacts；本阶段仅结构层）。OD-3 显示语法按最小格式占位；OD-4 已按建议实施；OD-5 维持 open gate。
4. `retired` lifecycle 保留枚举/CHECK，无服务入口（按契约）。

## 8. Git

| Commit | 内容 |
|---|---|
| `13bd8cd` | 迁移 005–008（8 张表）+ 既有计数断言更新 |
| `60af79b` | Domain 记录/规则/服务 + 端口 + 适配器 + Fake + Domain 测试 |
| `95f22e4` | 真实 SQLite 验证套件 + ReserveObjectId 读回缺陷修复 |
