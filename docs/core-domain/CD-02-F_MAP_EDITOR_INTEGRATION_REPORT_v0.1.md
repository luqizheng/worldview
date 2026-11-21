# CD-02-F — Map Editor Domain Integration Audit & Report V0.1

- **Date:** 2026-11-22
- **Baseline:** master `2e79fc4`（CD-02-E 交付后）
- **Companion:** [CD-02-E Report](CD-02-E_IMPLEMENTATION_REPORT_v0.1.md)、ME-03-H/ME-04-H/ME-05-02 设计契约
- **结论：PASS WITH CONDITIONS**（当前代码库无编辑器 UI——本阶段交付领域 API 可调用性证明与依赖缺口清单，见 §3/§4）

---

## 1. 现状事实

当前 `WorldView/` 生产代码**尚无 Map Editor UI**（ME-01~ME-04 为设计包；Desktop 仅有 Start/Project 窗口）。因此 CD-02-F 的可执行内容不是"修改编辑器代码调用 Core Domain"，而是：

1. 审计 ME-03/04/05 正式契约所需的领域能力是否**已经以 Core Domain Services 形式可调用**（UI 直写 SQLite 在架构上即不可能——唯一写入口是 `ICoreDomainDatabase` 端口 + Domain 服务）；
2. 以端到端集成测试**证明** ME 创作路径的领域能力真实可达；
3. 对尚不可达的部分给出**依赖缺口清单**（不伪造完成）。

## 2. 端到端集成证明（新增测试，真实执行）

`Cd02MapEditorIntegrationTests`（真实 SQLite + 真实迁移 + Domain 服务）：

- **ME-04 建筑创作全链**：包 bootstrap → 12 类型播种 → Reserve(request_key)/Bind(registry pin) → **typed Building 聚合 definition**（P2-09-38 schema 校验通过）+ Map 注册 + time-zero 成员（TX-55 单事务）→ 几何 Asset 发布/登记/`core_object_definition_asset` link（footprint role 经 registry 校验）→ definition CAS 编辑 → Initial State 指纹核验 → Branch 创建（**不触碰** Initial State/Definition 行——继承为解析时语义）→ 孤儿对账。
- **ME-03 基础原语**：settlement/road/river 三类型对象创建→注册→Initial State 全链（P2⇒P3 完整）。
- 全部操作经 Domain 服务/端口；无任何 UI/直写 SQL 路径（架构保证：唯一写入口是端口）。

## 3. ME-03/04/05 契约对齐表

| ME 模块需求 | Core Domain 能力 | 状态 |
|---|---|---|
| ME-04：Building/Level/Space/Access 权威 | building/cave_system 顶层类型 + child aggregate 聚合校验（CD-02-E）+ definition CAS | ✅ 可调用 |
| ME-04：Building 几何（footprint/space_region/boundary/entrance_anchor） | geometry role/family 校验（registry）+ Asset link + envelope 校验（CD-02-D） | ✅ 可调用 |
| ME-04/03：对象身份/注册/time-zero | Reservation 状态机 + Map 注册 + Initial State CAS（CD-02-B） | ✅ 可调用 |
| ME-03：settlement/road/river 等对象 | 十基础类型 typed definition + 创建/注册/成员链 | ✅ 可调用 |
| ME-03：**Road connectivity**（network.connects_to、RoadEndpoint ME-03-H H-02 P0） | **Relationship 持久化不存在**（P2-09-20 V1 明确无关系表） | ⛔ 依赖 Relationship 阶段 |
| ME-05：Shared geometry 写入（四 context） | Shared Definition/Initial State 路径可调用；**post-zero Command 路径不存在**（CD-03+） | ⚠️ 两类可调用；Command 类依赖 Command 阶段 |
| ME-05：几何 split/merge | 显式拒绝边界（P2-09-19 §7.5 deferred） | ⛔ 按契约 deferred |
| ME-05-01：Field Dataset/Revision | **表未建**（CD-02-A §15 排除；Field Dataset 阶段交付） | ⛔ 依赖 Field Dataset 阶段 |
| ME-05：spatial reference 定义/精度 | envelope 结构校验可调用；**项目级 spatial reference 定义实体未实现**（P2-09-25 §4 契约存在，实施属 Map spatial 契约阶段） | ⚠️ 结构层就绪；定义实体待专项 |
| Checkpoint（baseline/definition 编辑失效） | 指纹组成契约锁定（CD-02-A §10）；`core_checkpoint` 表未建 | ⛔ 依赖 Checkpoint 阶段 |

## 4. 依赖缺口清单（如实报告，不伪造完成）

1. **Relationship 持久化与 Road connectivity**（ME-03 P0）：P2-09-20 V1 无关系表；`core_command_object_ref` 系未建。依赖：Relationship descriptor/表/服务阶段 + Command 阶段。
2. **Command 写路径**（ME-05 History-local 几何编辑、ME-04 post-zero 操作）：`core.command.*` 系全部属 CD-03+；当前只有 baseline authoring + Shared Definition 路径。
3. **Field Dataset/Revision**（ME-05-01）：表未建（P2-09-11 §4.1 有提案）；依赖 Field Dataset 专项阶段。
4. **`core_checkpoint`**：未建（指纹契约已锁定）。
5. **UI/Application 用例层**：ME 编辑器本身未实现；Domain 服务无 UI 入口（与 CD-01 先例一致）。ME-01 工作计划 v3.18 为编辑器实施权威。

以上缺口均已在对应契约登记为后续阶段（CD-03+/ME 系列），不属于 CD-02 范围。

## 5. Git

本阶段单 commit（端到端集成测试 + 本报告）。
