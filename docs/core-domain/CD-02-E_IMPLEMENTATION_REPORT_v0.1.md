# CD-02-E — Building/Cave Typed Aggregate Adoption Implementation Report V0.1

- **Date:** 2026-11-22
- **Baseline:** master `ae64daa`（CD-02-D 交付后）
- **Companion:** [CD-02-D Report](CD-02-D_IMPLEMENTATION_REPORT_v0.1.md)、[P2-09-43 V0.6（S0–S10）](../architecture/core-domain/p2-09-43-building-cave-core-domain-developer-handoff-v0.6.md)
- **结论：PASS WITH CONDITIONS**（P2-09-40 向量执行发现工件漂移并显式适配，见 §4；macOS 未验证）

---

## 1. 采纳授权与范围

项目所有者通过本次 CD-02 委托采纳 P2-09-37~42 采纳包（P2-09-43 §2："they become implementation inputs only after the project owner adopts the relevant package"）。实现严格按 P2-09-43 §3 S1–S8 切片推进；**不建任何新表**（§4.1：全部复用 CD-02-B/D 已交付表）、**不建 child 顶层类型**（P2-09-37 §2.1：Building/Cave System 两个顶层类型；Level/Space/Chamber 等为 owner payload 内 child aggregate）、**不建第二套建筑/洞穴模型**。

## 2. 交付内容（按 S 切片）

| 切片 | 交付 |
|---|---|
| S1 Registry 格式扩展 | descriptor 允许键扩展 `definition_schema_ref`/`asset_contract`/`child_aggregate_contract`（P2-09-41 V0.4 块）；采纳扩展以嵌入资源加载（**不修改任何 docs 工件**），与基础目录合并为运行时目录（键集冲突 fail-closed）；十基础 descriptor 加载行为不变 |
| S2 Building/Cave descriptors | `building`/`cave_system` 进入运行时目录（schema_version=1、semantics_id 模板校验、geometry role+dimension 声明、child aggregate sections）；播种 12 类型（十基础 + 采纳对）+ 12 allocator 行 |
| S3 Aggregate schema validation | `JsonSchemaSubset`（有界 Draft 2020-12 子集执行器，Domain 内无第三方依赖）：执行 P2-09-38/39 **V0.5 真实工件**（经 csproj 链接嵌入）；不支持关键词 fail-closed；violation 映射到 P2-09-40 稳定错误码 |
| S4 Child identity | child ID 唯一性/引用解析/跨 owner 拒绝（聚合校验层）；owner-scoped 身份语义（`child_aggregate_contract.child_identity_scope=building_object_id`）由 schema+跨记录层强制；ID 不复用语义由 owner payload 全量重写 + 唯一性约束承载（P2-09-37 §2.3） |
| S5 Geometry component context | `child_context`（space/level/slice/boundary/entrance + cave 系）schema oneOf 分支 + 聚合层引用解析；role-family 约束（BLD-022/023/024、CAV-019/020/021 全部通过） |
| S6 Asset link integration | Building/Cave definition geometry 走 `core_object_definition_asset`（CD-02-D 已交付）；publication 层校验 typed link 清单 ≡ 组件 asset_id 集合、core_asset content_hash ≡ 组件 pinned hash（BLD-015/028/029、CAV-013/026/027 全部通过）；`core_map_initial_object_asset` 维持不启用 |
| S7 Building aggregate | definition 创建/CAS 编辑路由到聚合校验（`ObjectTypeRegistry.ValidateDefinition` 统一入口）；Level/Space/Plan/Boundary/Access 规则经 schema+跨记录层落地 |
| S8 Cave aggregate | Survey Frame 必需、station→point component 上下文匹配、坐标上下文解析、连接端点解析、跨 owner 端点拒绝、绝对高程无 datum 拒绝（measurement 层） |
| S9 Initial State / presence 边界 | 既有 CD-02-B 语义测试覆盖（time-zero 走 Initial State、无 time-zero Command、Bound 后 post-zero create 属 Command 阶段）；`core.object.create` 写路径属 CD-03+（descriptor command_capabilities 原样保留） |
| S10 Conformance | **P2-09-40 V0.6 全部 55 向量真实执行通过**（向量运行器 + 三层校验器），见 §4 |

## 3. 关键设计

- **执行真实 schema 工件**而非手抄规则：P2-09-38/39 V0.5 JSON 以嵌入资源 + `JsonSchemaSubset` 执行（P2-09-37 §3.2 允许"equivalent compiled validator"；本实现更忠实——直接执行已发布的 schema）。schema 引入新关键词时执行器 fail-closed 并提示扩展点。
- **稳定错误码**：`AggregateIssue.ErrorCode` 与 P2-09-40 vector_contract 对齐（CD_BLD_*/CD_CAVE_*）；schema violation → 错误码映射表按（keyword + 路径 + 属性）分发；oneOf 分支失败传播分支违规用于定位（satisfied==1 时不传播——其余分支失败非实例违规）。
- **definition 校验统一路由**：采纳类型（HasTypedAggregateSchema）→ 聚合校验；十基础类型 → descriptor 字段校验。ObjectCreationService/ObjectDefinitionService 均经此路由。

## 4. S10 一致性执行结果（重要发现）

**55/55 向量全部执行通过**（`Cd02AggregateVectorConformanceTests`，真实执行；含 2 项套件完整性检查共 57 断言）。执行中发现并显式适配一处**已发布工件漂移**：

- **漂移**：P2-09-40 V0.6 部分向量 patch 数据沿用 P2-09-38 V0.4 时代字段名（`boundary_component_ids`/`region_component_ids`/`component_id`），P2-09-38 V0.5 schema 已改名为 `boundary_geometry_component_ids`/`region_geometry_component_ids`/`geometry_component_id`（BLD-020 受影响；BLD-001 base fixture 使用新名）。
- **适配**：运行器内 `NormalizeLegacyFieldNames` 对 patch 值中三个 legacy 键做显式、可审计的规范化（注释引用本报告）；**不修改任何 docs 工件**。
- **处置建议**：向量套件上游修订（升 V0.7 使用 V0.5 字段名）后可移除适配层；此为 P2-09-43 adoption gate "execute and **refine** P2-09-40 vectors" 预期的精化输入。

另两处执行器实现修正（非工件问题）：oneOf 分支违规传播、JSON Pointer 路径规范化。

## 5. 明确不做（继承 P2-09-43 §1/§5/§9）

- post-zero child 历史 CRUD（Level/Space/Opening/Chamber 增删改的 Command）——CD-03+；`post_zero_child_lifecycle_commands=false`（child_aggregate_contract）原样保留。
- child ID 段间迁移/reparent（V1 不支持）；几何 split/merge（CD-02-D 已拒绝）。
- `core_map_initial_object_asset`（Initial State payload 无独立 geometry Asset 引用）；`core_command_asset`（Command 阶段）。
- Registry V2 激活声明（十验收门未执行；本阶段是 V1 运行时目录的采纳合并，非 V2 激活）。
- RoadEndpoint/跨 Building 地下网络/3D 固体几何。

## 6. 测试与真实结果

**全量 379/379 通过（Debug，真实执行）；Release 构建 0 错误。** 新增 57 个测试：

- 向量一致性：55 向量 × 真实执行（schema+aggregate+publication+measurement+spatial 全层）+ 套件计数 + 连通性策略断言。
- 12 类型目录：加载结构校验（逐 descriptor 版本：十基础=2、采纳对=1）、采纳块（definition_schema_ref/child_aggregate_sections）、播种 12+12、未知键仍 fail-closed。

## 7. 条件与遗留

1. **向量工件漂移**（§4）：适配层是临时桥；上游向量修订后应移除。
2. **既有 Project 的类型行补齐**：播种发生在 Project 创建事务；对采纳前创建的数据库，building/cave_system 行需要显式 ensure/migration（当前无真实用户数据，V1 无迁移负担；留待有存量数据库时处理）。
3. macOS 未验证；P2-09-12 完整证明套件未执行（延续）。
4. ME-04 编辑器侧（UI/交互）不在本阶段。

## 8. Git

本阶段单 commit（S1–S10 实现 + 向量运行器 + 文档）。
