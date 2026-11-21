# World View — 实施与工程状态记录

- **更新**：2026-11-21
- **状态**：新架构工程骨架已提交；ME-01 / ME-02 设计包已建立；正式领域实现与若干关键技术契约仍待实施
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
| 正式 Core Domain 实现 | 尚未在当前工程骨架中体现 | 领域设计文档不能替代实现 |
| 持久化实现 | 尚未在当前工程骨架中体现 | 当前示例 Repository 为内存实现 |
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
