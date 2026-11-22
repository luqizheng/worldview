# World View — 实施与工程状态记录

- **更新**：2026-11-22
- **状态**：CD-02-B 至 CD-02-G 已提交；CD-02-G1 与 CD-02-G2 Asset/Geometry integrity hardening 已实现；PR #1–#7 均保持 open、未合并。Provider/Geometry follow-up CI：PR #4 386/386、PR #5 389/389、PR #6 402/402、PR #7 397/397，均已通过 Windows/macOS Debug/Release；PR #3 文档修订 CI 正在运行。PR #7 仍需在 G2 Verified-evidence 接口上 rebase 并修正测试 fixture 后才可进入集成验收。
- **当前 Map Editor 工作计划**：ME-01-work-plan-and-document-status-v1.4.md
- **权威工程目录**：`WorldView/`
- **权威项目文档目录**：仓库根 `docs/`

> 本文件记录当前仓库可核验的工程事实。它取代 2026-11-20 的“技术栈未定 / 实施暂停 / 仓库零代码”状态描述；旧记录仅作为历史背景保留在 Git 历史中。

## 1. 当前已提交工程基线

| 项目 | 当前状态 | 依据 / 边界 |
|---|---|---|
| 主语言与目标框架 | C# / .NET 11 / `net11.0` | `WorldView/docs/architecture/initial-tech-baseline.md` |
| 桌面 UI | Avalonia UI 12.1.6，Windows + macOS 目标 | 同上；不代表两个平台均已完成发布验证 |
| UI 组织 | MVVM；CommunityToolkit.Mvvm | 工程基线 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 工程基线 |
| 工程结构 | Desktop / Application / Domain / Infrastructure + 三个测试项目 | `WorldView/WorldView.sln` |
| 工程脚本 | Windows PowerShell 与 Unix shell 构建/运行/测试脚本 | `WorldView/scripts/` |
| 示例垂直切片 | MapProject 创建、内存 Repository、基础测试 | 仅用于验证工程分层，不是正式地图领域模型 |
| Core Domain 设计 | Command、Object、Geometry、Relationship、History、Persistence 与 Descriptor 设计包 | `docs/architecture/core-domain/` |
| Map Editor 设计 | ME-01 共享编辑器基线；ME-02-A 至 ME-02-H 自然地理完整设计包 | `docs/map-editor/` |

工程初始化提交及其验证记录见：
- `WorldView/docs/decisions/ADR-0001-project-bootstrap.md`
- `WorldView/docs/development/build-and-test.md`
- `WorldView/docs/development/dev-environment.md`

ADR-0001 记录初始化阶段的构建与测试结果。该历史记录不应被解释为当前 HEAD 已在所有目标操作系统上重新执行验证。

## 2. 已确定与尚未确定的技术边界

### 已确定
- C# / .NET 11、Avalonia UI、MVVM、DI 与分层 Solution。
- Windows 与 macOS 是桌面目标平台。
- Domain 不依赖 UI 或 Infrastructure；Application 依赖 Domain；Infrastructure 实现 Application 定义的接口。
- 新架构工程代码位于 `WorldView/`；仓库根 `docs/` 是项目设计文档权威位置。
- Map Editor 使用统一 Canvas-centered editor shell；Map Engine / Spatial Services 负责确定性空间计算，Core Domain 拥有正式地图事实与历史。

### 尚未确定 / 仍需专项评估
- 本地数据库与项目文件格式的最终物理方案及迁移实现。
- 地图 2D / 3D 渲染技术。
- Geometry / GIS 计算引擎与空间索引。
- 栅格/物理场 Asset 的实际编码、分块、缓存与读写方案。
- AI 模型接入方式、Agent Runtime。
- Task Planning & Execution Engine 的具体实现。
- Windows / macOS 的完整打包、安装、升级、备份恢复和性能验证。

这些事项应按 `WorldView/docs/roadmap/tech-assessment-backlog.md` 与对应设计文档推进，不得因工程骨架已存在而默认视为已选型。

## 3. 当前实现成熟度

| 层次 | 状态 | 说明 |
|---|---|---|
| 工程骨架 | 已提交 | Solution、项目分层、DI/MVVM 示例与测试项目 |
| 正式 Core Domain 实现 | CD-02-B 至 CD-02-G 已实现（Object/Initial State/Registry/Asset/Geometry/Building-Cave + ME 领域 API 证明）；G1/G2 integrity hardening 已交付 | CD-02 Final Report、CD-02-G1/G2 reports；G2 CI run 44975176429：Windows/macOS Debug/Release 393/393 each |
| 持久化实现 | 已实现（SQLite 迁移 001–010，共 16 张表 + 统一连接协调器） | CD-01/CD-01-F1/CD-02-B/CD-02-D；示例内存 Repository 仅为工程分层验证 |
| 地图编辑器 UI | 尚未在当前工程骨架中体现 | Avalonia 主窗口为工程启动示例，不是 ME-01/ME-02 产品界面 |
| 地图渲染与空间计算 | 尚未在当前工程骨架中体现 | 技术评估与服务契约仍待完成 |
| AI Assistant / Generation | 尚未在当前工程骨架中体现 | 需遵循 Proposal / Candidate / Change Set 权限边界 |

## 4. 当前设计包状态

### Core Domain Descriptor Closure
- P2-09-26 Object Type Registry V0.2：10 个 Object Type，推荐目录，非冻结 ADR。
- P2-09-27 Relationship Type Registry V0.2：5 个 Relationship Type，推荐目录，非冻结 ADR。
- P2-09-28 Validation Vectors V0.2：53 个设计测试向量，尚未对实现执行。
- P2-09-29 Cross-Registry Consistency Contract V0.1：Object / Relationship / Command / History scope 一致性工作契约。
- 这些目录和向量不是可直接运行的验证器；实际验证逻辑归 Core Domain。

### Map Editor
- ME-01 共享编辑器产品与架构工作基线已建立。
- ME-02-A 至 ME-02-H 已完成为产品、领域、UX 和跨架构设计工作基线。
- 当前 ME-02 总体基线为 V0.6，Map Editor 工作计划为 V1.0。
- ME-02-F V0.4 已按 ME-05-01 V0.4 对齐 source Field Dataset、Derived Artifact、Core Asset、Map-wide baseline fingerprint 与 freshness。
- ME-02-G V0.2 已明确 Initial State、History-local、Project-shared 三种正式写入目标。
- ME-02-H V0.2 已将原 H-G01 产品/领域权威门槛标记为关闭；Field Dataset 的开发实现门槛仍保留。
- ME-05-01 V0.4 已更新（V0.2 首次采纳产品语义） Map-owned Field Dataset / immutable Field Revision / typed Core Asset ownership；用户于 2026-11-21 确认 Field Revision 更新作为 Map-wide baseline，追溯影响该 Map 全部 Branches 与 Historical Times。

## 5. 关键未关闭门槛

| Gate | Owner | 阻塞范围 |
|---|---|---|
| H-G01 — Field Dataset authority / Map-wide baseline semantics | **Closed — ME-05-01 V0.2** | Domain contract adopted |
| H-G01-I — Field payload package / physical provider implementation / cross-platform validation | ME-05 + Core Domain + Map Engine + Development | Production source-field publication and editing |
| H-G02 — Derived Artifact namespace/catalog 实现 | Map Engine | 派生栅格/分析输出生产与发布 |
| H-G03 — Calculation Definition / input fingerprint 精确序列化 | Map Engine + Core Domain | 可复现计算与缓存身份 |
| H-G04 — Hydrology/climate influence 与 coverage 声明 | ME-02 specialists + Map Engine | 生产级增量重算 |
| H-G05 — Formal Object/Geometry/Relationship descriptor enablement | Core Domain | 正式专业编辑操作开放 |
| H-G06 — Scene builder 与 active artifact generation 一致性 | Map Engine | 派生图层渲染集成 |
| H-G07 — 正常/异常/恢复场景 Figma 原型 | Product UX | UX 实现准备 |
| H-G08 — Windows/macOS 数值与存储故障注入验证 | Development team | 平台与生产就绪 |

H-G01 产品/领域语义门槛已关闭；H-G01-I 仍是正式 field publication 的实现前置门槛。ME-02 设计包完成不代表实现或平台测试完成。

## 6. 后续实施入口

1. 已完成：ME-05-01 V0.2 采纳 Map-wide retroactive Field Baseline 语义；P2-09-11 V0.11 和 P2-09-12 V0.6 已更新逻辑 Schema 与 Provider Proof 设计。
2. ME-05 与 Core Domain 共同定义 Field Dataset/source-field 权威、Map scope、不可变 payload、coordinate context、tiling 和编辑生命周期。
3. Map Engine 完成 Derived Artifact namespace/catalog、Calculation Definition fingerprint 与 SceneSnapshot 消费契约。
4. Core Domain 只启用 payload、验证规则和测试向量已齐备的 Object / Geometry / Relationship descriptors。
5. 建立正式 Core Domain 最小可验证垂直切片：Object identity / descriptor validation / Command append / deterministic replay。
6. 再扩展 Geometry、History reconstruction、Map Editor 和 AI Proposal / Change Set 应用链路。
7. 每个实现切片都应有自动化测试、平台验证记录和对应设计文档状态更新。

## 7. 记录规则

- 只记录已在仓库中核验的工程事实；计划、提案、设计规格与已实现能力必须明确区分。
- 测试只有在对应代码和环境中实际执行后才能标记通过。
- 每次实施状态变化后更新本文件及对应工作计划。
- 正式完成以文档、实现、测试证据和 GitHub 提交共同为准。


## 2026-11-21 Field Dataset authority decision update

- **Adopted:** ME-05-01 V0.2 — Map-owned Field Dataset, immutable Field Revision, typed Core Asset ownership; publishing a new current revision applies retroactively to all Branches and Historical Time points of that Map only.
- **Schema design:** P2-09-11 V0.11 adds `core_map_field_dataset` and `core_map_field_revision`, same-Dataset current-revision FK, immutable Asset ownership, baseline-aware Checkpoint fingerprints and CAS publication requirements. This is not executable DDL.
- **Provider proof design:** P2-09-12 V0.6 adds TX-65 through TX-71 for uniqueness, typed Asset ownership, immutability, CAS, retroactive Branch/Time behavior, cross-Map isolation, orphan recovery and Checkpoint invalidation. These tests have not been executed.
- **Calculation alignment:** ME-02-F V0.3 pins source Field Dataset/Revision identities and Map Source Field Baseline fingerprints in calculation inputs/freshness.
- **Status:** H-G01 product/domain authority gate closed. P2-09-30 Field Descriptor Registry V0.1 and P2-09-31 32 validation vectors are documented as recommended design artifacts. Payload package/tiling, actual provider validation, implementation and Windows/macOS execution remain open. No production field publication capability is claimed.


## 2026-11-21 Field Descriptor follow-up

- **P2-09-30 V0.1:** recommended Core Domain static Field Descriptor Registry for `elevation` and `land_water`, including units, vertical reference, registered categories, no-data semantics, provenance, compatibility and Map-wide revision scope.
- **P2-09-31 V0.1:** 32 Field Dataset/Revision validation vectors covering descriptor, manifest, no-data/coverage, CAS, immutability, cross-Map isolation, retroactive Branch/Time behavior, adoption and Checkpoint freshness. Design fixtures only; not executed.
- **ME-05-01 V0.3:** records the descriptor/validation artifacts and remaining implementation work.
- **Remaining:** immutable field payload package/tiling, Core Domain service implementation, actual bundled SQLite deferred-FK/trigger validation, Asset orphan recovery, and Windows/macOS runtime test execution.


## 2026-11-21 Field Package and Tiling design

- **ME-05-07 V0.1:** recommends one seekable immutable Field Package Asset per Field Revision, with canonical manifest, deterministic tile directory and independently decodable tile payloads. Tiles are storage/processing partitions, not Core Domain identities.
- **Package direction:** random tile reads; bounded-memory streaming; local edits build a complete new immutable package; unchanged tiles may be copied through only when encoding compatibility is exact. No published package is mutated.
- **Tile direction:** one native source grid in V1, 256 × 256 candidate tile size benchmarked against 128 and 512; row-major X-fastest cell order; explicit edge dimensions; no authoritative stored pyramids; halo cells are read from neighbors, not duplicated in source storage.
- **Codec/precision:** no compression library or elevation numeric width selected. Independent per-tile compression and a none-codec fixture are required; production choice waits for deterministic cross-platform and performance evidence.
- **P2-09-32 V0.1:** 45 package/tile validation vectors; design fixtures only, not executed.
- **ME-02-F V0.4:** pins package format and package Asset identity in calculation provenance; V1 freshness conservatively invalidates Dataset-dependent calculations on a new Field Revision. Tile hashes are not used to silently narrow freshness.
- **Remaining:** byte-level header/directory specification, tile-size and codec/precision benchmarks, Core Domain publication/provider validation, implementation and Windows/macOS execution.


## 2026-11-21 ME-05 package integration follow-up

- **ME-05-05 V0.2:** aligns the shared Geometry/Field Asset publication contract with the Field Package logical/tile model.
- **ME-05-06 V0.2:** cross-architecture review now includes ME-05-07; Gate B is limited to byte-level header/directory, codec/precision and performance proof plus implementation verification.
- **ME-05-07 V0.1:** remains the detailed logical Field Package and tiling contract; it does not freeze the byte-level container, codec or numeric precision.
- **Work plan:** ME-01-work-plan-and-document-status-v1.3.md tracks ME-05-07 and the remaining gates.


## 2026-11-21 Field Package binary envelope design

- **ME-05-08 V0.1:** specifies the V1 seekable package byte envelope: 72-byte fixed header, canonical manifest, 16-byte directory header, 72-byte tile entries and independently addressable payload blocks. Fixed-width header/directory integers are little-endian; offsets are absolute from package start; alignment is 8 bytes.
- **P2-09-33 V0.1:** 28 binary-envelope validation vectors for header, section offsets, directory entries, tile ranges, hashes and cross-platform serialization. Design fixtures only; not executed.
- **ME-05-07 V0.3:** integrates the byte-level envelope into the logical Field Package/tiling contract. Parser/writer, codec, tile-size and precision proof remain open.
- **ME-05-05 V0.3 / ME-05-06 V0.4 / ME-05-01 V0.5:** cross-document package ownership, integration review and Field Dataset status aligned to the new specification.
- **Remaining:** implement reader/writer and run all package fixtures; benchmark 128/256/512 tile sizes; choose codec and elevation precision from measured results; complete provider/runtime and Windows/macOS validation.

## 2026-11-21 CD-01 Core Domain persistence implemented

- **CD-01 V0.1 (design/report/handoff):** `docs/core-domain/` — minimal real Core Domain persistence landed in `WorldView/`: 6 domain tables (`core_project`, `core_map`, `core_history`, `core_branch`, `core_calendar`, `core_calendar_configuration`) via 4 `core-domain` module migrations through the shared `schema_migration` ledger (P2-09-11 V0.11 minimal subset; checksum-managed, fail-closed).
- **Real chain lit up:** create project → persist identity → reopen → read identity → project-scoped map query → Branch lineage + canonical Historical Time (`day:offset`) workspace resolution → open workspace. AF-02's four boundary ports wired to real implementations in the composition root; Application/Session layer unchanged.
- **ADR-0003:** records schema-subset adoption, single-transaction structural writes, PRAGMA policy (fk=ON + bounded busy_timeout, journal mode default), and explicit open-gate placeholders (Calendar structural row with `rules_schema_version=0`/`'{}'`, `lifecycle_status='active'`, no auto initial map).
- **Convergence:** `MapProject` sample trio deleted (shadow model superseded); domain model now lives in `WorldView.Domain`.
- **Verification:** Debug/Release builds 0 errors; 139/139 tests (Domain 58 / Application 44 / Infrastructure 37, real SQLite end-to-end and two-project isolation included); Windows Release smoke run OK. macOS unverified; P2-09-12 transaction-proof suite remains to be executed (next stages).
- Earlier log row "正式 Core Domain 实现 | 尚未在当前工程骨架中体现" is superseded by this entry.

## 2026-11-22 CD-01-F1 database access coordination & persistence integrity verified

- **CD-01-F1 V0.1 (report/handoff):** `docs/core-domain/` — independent verification of CD-01's real persistence boundary on master `0d13b17`; fixes landed in commit `4b0c322`.
- **Audit finding confirmed by out-of-repo reproduction:** the composition root's two adapters each held an independent instance `_gate` over one shared `SqliteConnection` (plus a per-call instance in the identity reader); migration transactions ran outside the package-db lock; dispose raced in-flight operations. Old model measured ~50% write failures under two concurrent writers (`cannot start a transaction within a transaction`) and 99/100 native `ObjectDisposedException('SQLitePCL.sqlite3')` under dispose races; pure read concurrency merely appeared to work, which is why the 139 sequential tests passed.
- **V1 access model:** new internal `SqliteConnectionAccess` — one coordinator per connection; every use (package-db ops, core-domain adapters, migration transactions, dispose, test injection) executes fully inside one critical section; Monitor reentrancy provides `InSnapshotScope` atomic multi-step reads (workspace resolution); dispose flips state under lock then releases the connection after in-flight ops exit; post-close entry fails closed with `ObjectDisposedException` (ObjectName=`SqliteConnectionAccess`). `ExecuteMigrationAsync` moved entirely under coordination. Public APIs and composition root wiring unchanged.
- **Domain fix:** `BranchLineageResolver` now validates the initial branch's own history (exists and is `branch_owned`) before walking ancestors.
- **Verification:** 33 new real-SQLite tests (16 concurrency: shared connection/write-write/write-race/partial-write visibility, transactions incl. midway rollback + paired-count isolation, dispose races, migrations incl. two-connection same-file exactly-once; 15 integrity: two-package project isolation, composite-FK cross-project writes, planted corruption — cycle/dangling/ownerless/cross-map root/initial-own/depth-limit, schema CHECK/FK/UNIQUE/partial-index negatives; 2 domain lineage). Full suite 172/172; Debug and Release builds 0 errors.
- **Verdict:** PASS WITH CONDITIONS — cross-call composite writes rely on V1 append-only semantics (revisit when delete/update lands); coordinator is serial per connection (mind long transactions); new adapters must go through the coordinator; P2-09-12/WAL open gates unchanged.

## 2026-11-22 CD-02 complete — final acceptance recorded

- **Verdict:** PASS WITH CONDITIONS ([CD-02 Final Implementation Report V1.0](../core-domain/CD-02_FINAL_IMPLEMENTATION_REPORT_v1.0.md)).
- **Final HEAD:** master with commits 13bd8cd/60af79b/95f22e4/648f9dd/cb85f67/ae64daa/2e79fc4/0bf8bf7 (+ final report commit).
- **Verification:** 381/381 tests Debug AND Release; both builds zero errors; architecture boundary review clean (single write port, no second authority, no duplicate state, composite FKs everywhere, coordinator bypass absent, registry compat locked, geometry/object identity separation tested).
- **Deferred (all registered):** Command/Event write paths (CD-03+), relationships, field datasets, checkpoints, Registry V2 activation gates, geometry split/merge design, macOS verification, P2-09-12 full proof suite.

## 2026-11-22 CD-02-F Map editor domain integration audited

- **Evidence:** Cd02MapEditorIntegrationTests — ME-04 building authoring path end-to-end on real SQLite (bootstrap -> seeding -> reserve/bind -> typed Building aggregate definition -> registration -> initial state -> geometry asset publish/register/link -> CAS edit -> branch creation touching nothing) and ME-03 primitives (settlement/road/river full chain). All through Domain services; UI-direct writes architecturally impossible (single write port).
- **Gaps listed honestly:** relationship persistence (ME-03 Road connectivity P0), Command write paths (ME-05 History-local), Field Dataset tables (ME-05-01), core_checkpoint — all registered as later stages, not faked.
- **Report:** `docs/core-domain/CD-02-F_MAP_EDITOR_INTEGRATION_REPORT_v0.1.md`.

## 2026-11-22 CD-02-E Building/Cave typed aggregate adoption implemented

- **Adoption:** project owner adopted P2-09-37~42 package via the CD-02 delegation; implementation followed P2-09-43 S1-S10. No new tables; no top-level child types; building/cave_system descriptors merged into the runtime registry from the unmodified P2-09-41 V0.4 artifact (embedded resource), 12-type catalog with per-descriptor schema versions (base=2, adopted=1).
- **Schemas executed, not transcribed:** P2-09-38/39 V0.5 aggregate schemas run through a bounded Draft-2020-12 subset validator (JsonSchemaSubset) that fails closed on unsupported keywords; definition validation routes aggregate types through the schema + cross-record layers, simple types through descriptor field validation.
- **Conformance:** all 55 P2-09-40 V0.6 vectors executed and passing. Finding: partial vector patches use P2-09-38 V0.4-era field names (boundary_component_ids/region_component_ids/component_id) vs V0.5 names — explicit audited normalization in the runner; upstream vector revision recommended (refinement anticipated by the adoption gate).
- **Tests:** 379/379 total; Release build clean.
- **Report:** `docs/core-domain/CD-02-E_IMPLEMENTATION_REPORT_v0.1.md`.

## 2026-11-22 CD-02-D Geometry & Asset foundation implemented

- **Code:** migrations 009-010 (`core_asset`, `core_object_definition_asset`); `WorldView.Domain/Assets/` (AssetRecord/AssetRules/AssetService — content-addressed idempotent registration, path confinement, corrupt/missing assets blocked from new formal references); `WorldView.Domain/Geometry/` (GeometryComponentManifest per P2-09-19 s6.2, descriptor-driven manifest validation, manifest/link/asset consistency, worldview.geometry-json envelope structural validator per P2-09-25 recommended direction, operation policy with explicit split/merge rejection per P2-09-19 s7.5).
- **Not built:** core_map_initial_object_asset (trigger not fired), core_command_asset (Command stage), core_geometry_component table (V1 forbids), geometry Command write paths (CD-03+).
- **Tests:** 322/322 total (41 new; real-SQLite full-chain evidence: publish bytes -> register metadata -> link -> envelope validation -> three-way consistency); Release clean.
- **Report:** `docs/core-domain/CD-02-D_IMPLEMENTATION_REPORT_v0.1.md`.

## 2026-11-22 CD-02-C Registry runtime & typed schema implemented

- **Code:** `WorldView.Domain/Registry/ObjectTypeRegistry.cs` + embedded P2-09-26 V0.2 descriptor registry (csproj resource link — single data source, no copy drift); pin authority migrated from BuiltinObjectTypes constants to the registry; typed definition-payload validation wired into ObjectCreationService/ObjectDefinitionService; adapter Bind writes registry-derived pins.
- **Inventory (R1):** P2-09-26 V0.2 descriptors are complete and machine-readable (12 top-level keys per type; six definition declaration forms; geometry role/min/max/families; unknown_fields=reject declared). P2-09-24 field catalog consistent. Initial-state typed schema does not exist (OD-1 stays open, structural validation only). P2-09-28 vectors are prose fixtures — definition-field subset encoded as unit vectors; geometry/relationship subsets deferred.
- **Tests:** 281/281 total (13 new registry tests incl. programmatic valid-payload vectors for all ten types); Release build clean.
- **Not activated:** Registry V2 (P2-09-45 §8 ten acceptance gates unexecuted — gap list in CD-02-C report §6); registry compiler not implemented; runtime disabled_pending_* fields untouched.
- **Report:** `docs/core-domain/CD-02-C_IMPLEMENTATION_REPORT_v0.1.md`.

## 2026-11-22 CD-02-B Object Persistence & Initial State Foundation implemented

- **Code:** commits `13bd8cd`（migrations 005–008）、`60af79b`（Domain services + port + adapter）、`95f22e4`（real-SQLite verification suite）on master.
- **Schema:** 8 new tables (`core_object_type`, `core_object`, `core_object_definition`, `core_object_id_allocator`, `core_object_id_reservation`, `core_map_object_ref`, `core_map_initial_state`, `core_map_initial_object`) via append-only migrations; map_local scope defense-in-depth triggers; member→registration composite FK (P3⇒P2 at the database level).
- **Domain:** `WorldView.Domain/Objects/` — records, ObjectRules/ReservationRules, canonical JSON + InitialStateFingerprint (Domain-owned), ten-type seed catalog pinned to P2-09-26 V0.2, seeding (OD-4: inside the Project creation transaction via `ProjectCreationPlan`), reservation state machine with request_key idempotency, TX-55 atomic object creation, definition expected-hash CAS, map registration service, initial state CAS baseline service.
- **Infrastructure:** all composite writes run inside single `SqliteConnectionAccess` critical sections with transaction-internal state re-reads (CD-01-F1 condition 1 response); read-side fail-closed pin validation; `CoreDomainConflictException` for semantic conflicts.
- **Tests:** 268/268 total (96 new). Real SQLite evidence: seeding counts, allocator non-rollback, 8-thread concurrent reserve uniqueness, all five illegal reservation transitions rejected, ABORT-trigger mid-transaction failure injection with zero half-states and same-ID retry, scope/service+trigger rejections, CHECK/FK/UNIQUE negatives, CAS stale rejections, fingerprint determinism, pin-tamper read fail-closed, concurrent read/write no torn state.
- **Report/handoff:** `docs/core-domain/CD-02-B_IMPLEMENTATION_REPORT_v0.1.md` (PASS WITH CONDITIONS: P2-09-12 proof suite + macOS unverified; no UI entry; OD-1/OD-2 open), `docs/core-domain/CD-02-B_DEVELOPER_HANDOFF_v0.1.md` (CD-02-C registry runtime contract).
- **Defect found & fixed by the suite:** ReserveObjectId post-insert read-back used request_key lookup (null crash for keyless reserves) — now reads by reservation id.

## 2026-11-22 CD-02-A Object & Initial State authority closure

- **CD-02-A V0.1 (main/model/handoff):** `docs/core-domain/` — architecture closure for the next Core Domain stage, based on a real source audit of master `3159270` (no production code change in this stage; baseline re-verified 172/172).
- **Code audit verdict:** Object Identity / Definition / Type Registry / ID Reservation / Map Registration / Initial State / Geometry / Relationship are all **Missing** in `src/` (grep-verified zero hits); Asset layer is **Partial** (FileAssetStore byte store only, no `core_asset` authority rows or typed links); connection coordinator and structural-write transaction patterns are **Implemented** and reused.
- **Key closure decisions (all derived from existing contracts, sources cited in the document):** five-layer authority separation (type registry / identity / current definition / initial state / runtime); four-layer existence distinction (project existence → map registration → time-zero membership → post-zero history) that must not collapse into one `Object.Status`; dual representation `allocated_id_value` (type-scoped, never reused) vs `object_identity_id` (relational UUID); reservation state machine Reserved→Bound→Inserted with Abandoned terminal outcome and permanent consumption; single current definition row with expected-hash CAS and retroactive current-definition semantics (no revision chain); initial state = one mutable logical baseline per map with content-fingerprint CAS, branch-inherited without copying, never a Command; no `core_map_initial_object_asset` enabled; no geometry component table — component identity lives in owner typed payloads; Registry Format V2 designed but not activated (P2-09-26 V0.2 remains the working catalog with ten pinned types schema_version=2 / semantics_id `world-view.object-type.<key>.v1`); Building/Cave descriptors stay excluded.
- **CD-02-B implementation contract:** 8 new append-only-namespace tables (`core_object_type`, `core_object`, `core_object_definition`, `core_object_id_allocator`, `core_object_id_reservation`, `core_map_object_ref`, `core_map_initial_state`, `core_map_initial_object`) in migrations appended after 004; no asset/command/event/checkpoint/AI tables; first UPDATE paths in the codebase (reservation transitions, identity lifecycle, definition/baseline CAS edits) formally answer CD-01-F1 §15 condition 1 — all composite writes must live inside single coordinator critical sections with in-transaction state-machine precheck and negative tests.
- **Open decisions recorded (OD-1..5):** Initial State / Definition typed payload validation gated on Registry V2 descriptor schema artifacts (must not be invented in CD-02-B); minimal decimal ID display grammar as versioned placeholder; built-in type seeding at project creation (recommended); `core_map.lifecycle_status` vocabulary remains CD-01 open gate.
- **Verification:** all referenced paths checked; registry/schema JSONs parsed and ten-type pins script-verified; Mermaid diagrams structure-checked; cross-references and conflicts against ADR-001~005/ADR-0003/P2-09 series/ME-05-02/CD-01 series checked; CD-01-F1 transaction inheritance encoded into the CD-02-B contract; no code tests were fabricated — no production code changed.


## 2026-11-22 CD-02-G1 Asset / Geometry integrity hardening

- **Scope:** formal Asset links now require Verified integrity; SQLite link insertion re-reads Object lifecycle, Asset state and registry role inside the same coordinated transaction; Building/Cave links require one matching owner geometry manifest entry (Asset ID + role + authoritative hash); Definition CAS cannot orphan or mutate an existing linked manifest.
- **Registry:** JsonSchemaSubset now recursively checks $defs for unsupported keywords; a referenced-definition negative test was added.
- **Tests / CI:** added real-SQLite direct-adapter/service state tests and strengthened ME-04 manifest/link/CAS integration tests. GitHub Actions .NET 11 Windows + macOS matrix passed on commit `1b0e8c09db650c5a822231e80582d2affb70e374`; both runs `44971620581` and `44971617115` completed successfully, including Debug Build/Test and Release Build/Test on both OSes.
- **Schema correction:** 10 migrations create 16 tables, not 14 (CD-01 six + CD-02-B eight + CD-02-D two).
- **Limitations:** Verified is still set through a persistence port; end-to-end IAssetStore byte verification must be wired before treating it as a trusted publication pipeline. Typed manifest/link transaction enforcement currently covers Building/Cave aggregate descriptors; simple types need a separately decided typed geometry payload contract.
- **Report:** [CD-02-G1 Asset / Geometry Integrity Hardening Report V0.1](../core-domain/CD-02-G1_ASSET_GEOMETRY_INTEGRITY_HARDENING_REPORT_v0.1.md). Implementation and configured Windows/macOS CI verification passed; PR #1 remains open for review. Remaining limitations above are not represented as closed.



## 2026-11-22 CD-02-G2 Asset byte verification

- **Implementation:** `VerifyAssetIntegrityUseCase` reads the current immutable Asset Store bytes, computes SHA-256 and byte length, and records Verified / Missing / Corrupt outcomes.
- **Persistence boundary:** the unrestricted `UpdateAssetIntegrityState` operation was removed. `RecordAssetIntegrityFailure` accepts only Corrupt/Missing. `MarkAssetVerified` requires expected/actual hash and byte-length evidence, re-reads the current Asset row and validates the evidence inside the same SQLite write transaction.
- **Negative coverage:** inconsistent evidence, stale expected metadata, and attempts to set Verified through the failure API are rejected; existing test doubles and Asset integration tests use the constrained contract.
- **CI evidence:** head `f0c3ca01e3c97427dc6191ac498e51d4620c317e`; run `44975176429`. Windows and macOS each passed Debug build/test and Release build/test. Each test configuration reported 393 passed, 0 failed, 0 skipped.
- **Security boundary:** `AssetVerificationEvidence` is a consistency-checked application/persistence DTO, not a cryptographic attestation. Arbitrary malicious in-process code with direct database-port access could fabricate matching values. The API prevents accidental/unqualified Verified writes and stale/mismatched evidence; it does not claim an OS/process security boundary.
- **Operational boundary:** byte verification is an explicit use case; registering metadata does not silently scan bytes. Asset Store read errors other than a missing-content result propagate and do not falsely mark the Asset.
- **Status:** G2 implementation and platform CI verified. Draft PR #2 remains open and unmerged pending review.


## 2026-11-22 CD-02-G3 / P2-09-12 follow-up

- **PR #3 — Architecture reconciliation:** Geometry Manifest and provider-proof gap assessment; adds the P2-09-12 V0.8 amendment. The amendment aligns TX-04/TX-31 with CD-01-F1's already-selected single-connection-per-ProjectSession model. V1 reads serialize behind writes; coherent multi-query reads use `InSnapshotScope`. A deterministic writer-held/read-blocked test remains required before TX-31 is closed.
- **PR #4 — Provider proof:** current package connections prove FK enforcement and bounded external-writer-lock failure. Exact head `d603afde`: Windows/macOS Debug/Release 386/386. TX-32 remains partial because automatic retry is intentionally absent until stable operation/idempotency keys exist.
- **PR #5 — Geometry Manifest identity:** reject empty component/Asset GUIDs; add owner-snapshot cardinality, combined minimum and duplicate component identity validation. Exact head `02299c52`: Windows/macOS Debug/Release 389/389.
- **PR #6 — JSON Schema conditionals:** implement bounded `if/then/else/not` support and canonical Field Package manifest fixture conformance. Exact head `0b1e42d8`: Windows/macOS Debug/Release 402/402. Does not implement Field Dataset persistence.
- **PR #7 — Atomic Building/Cave publication:** complete Definition + distinct `geometry_component` typed Asset-link set commits in one coordinated SQLite transaction; includes CAS, failure injection rollback, initial-creation bypass closure and recovery from a previously Corrupt linked Asset. Exact head `a8d31f9a`: Windows/macOS Debug/Release 397/397. This branch is based on PR #5 and pre-G2 interfaces; do not merge before G2 integration. Replace test-only direct Verified mutation with `VerifyAssetIntegrityUseCase`/evidence path during rebase.
- **Non-claims:** these PRs do not implement AI Assistant persistence/Application Commit Coordinator, shared Core+AI transaction, Command persistence, process-crash reconciliation, WAL backup/restore, Field Dataset/Revision persistence or packaged desktop native SQLite validation. All PRs remain unmerged.
