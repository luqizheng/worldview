# WP-RA-00 — ME-08 地图制作准备度（AUTHORING_READINESS）

基线：`20c6fcca`。ME-08 依据：`docs/map-editor/world-view-me-08-spatial-operations-and-domain-integration-v0.1.md`（working baseline，自述"不授权实现"）+ ME-06 合同 v0.3 + roadmap WP-50/WP-60。

标记：✅ 已实现且有测试 · 🟡 部分实现 · 📄 仅设计文档 · ❌ 未实现 · ⛔ 被领域能力阻塞。

---

## 1. 交互框架项（ME-06 / WP-50 范围）

| 能力 | 设计依据 | 代码现状 | 可先行？ |
|---|---|---|---|
| Selection | ME-06-C / ME-06-E | ❌ 无代码（Desktop 14 个源文件中无任何 Selection 实现） | ✅ 可先建（只读，不写域） |
| 只读 Inspector（type-aware） | ME-06 / ME-01-06 | ❌ | ✅ 可先建（消费读投影，依赖切片 B 的内容投影） |
| Tool Registry / 能力声明 | `world-view-me-06-shared-interaction-and-tool-contracts-v0.3.md` | 📄 仅合同 | ✅ 可先建（声明式，未启用的能力显式不可用） |
| Input/Gesture Routing | ME-06-B | 📄 | ✅ 可先建 |
| Transient Operation / Preview / Staging | ME-06-D / PRA §5.5 | 📄 | ✅ 可先建（presentation-only，不触碰正式状态） |
| Validation（编辑期诊断） | ME-08 §9 | 📄 | ✅ 可先建（框架级） |
| Candidate / Change Set 审阅 | ME-06-E | 📄 | ✅ 可先建 |
| Freshness / Apply-time re-resolution | ME-06-C | 📄 | ✅ 可先建（框架级） |
| Apply / Cancel（正式写路由） | ME-06-D / roadmap WP-50 | ❌ | ⛔ **全部等待领域能力**（见 §2） |
| Undo（提交后） | roadmap WP-50 | ❌ | ⛔ 等待 adopted Core Domain History 操作 |

**判定：ME-08 的通用交互框架可以在零领域能力下以"只读 + 暂存"形态先行建立（即 WP-50），且 roadmap §6.8 明确要求被阻塞能力保持只读/不可用、禁止影子持久化。但当前连框架代码也不存在，"先建框架"是待办而非现状。**

## 2. 正式领域操作项（ME-08 产品/域决策 → Core Domain 门槛）

| ME-08 能力 | 设计状态 | Core Domain 写入支持 | 门槛 |
|---|---|---|---|
| Selection 后移动/旋转 Object（world map） | ME-08 §5.1 | ❌ 无 Object placement Command（Initial Object 的 placement payload 是 time-zero 基线，非可编辑 Command） | 需 typed Object placement Operation + Command 持久化 |
| Building Exterior footprint 编辑 | ME-08 §5.1 | ❌ | 同上 + Building 聚合 Command |
| Building Interior（2D plan、Level 切换） | ME-08-01（决策已确认：方案 A） | ❌ `BuildingCaveAggregateValidator` 只是 publication 校验器；无 Interior Command | ME-04 全套 Command/描述符采纳 |
| Road / Connection | ME-08 §7 | ❌ Relationship 数据模型不存在；Road endpoint 身份为 ME-08 §10 明示开放阻塞 | ME-03-E endpoint 决议 + typed Connection 协议采纳 |
| Spatial Site / Spatial Group | ME-08 §3 | ❌ 无组织/成员关系实体 | 新类型描述符采纳评审（ME-08 §1 明示需独立采纳） |
| Template / 复合操作 | ME-08 §6 | ❌ | 依赖上述全部 + 原子边界证明 |
| Cave passage / Portal 等 | ME-08 §7.1 | ❌ "not yet adopted" 显式状态 | 类型化采纳 |

## 3. 现有领域能力盘点（可作为 ME-08 地基的部分）

| 能力 | 证据 | 对 ME-08 的意义 |
|---|---|---|
| Object / Definition / Reservation / Initial State 写路径 | `Cd02ObjectPersistenceTests` 27 项、`Cd01EndToEndTests` | Object 存在层完整；但"编辑现有 Object"的 Command 化为零 |
| Geometry 规则与原子发布 | `GeometryAndAssetRulesTests` 33、`GeometryAtomicPublicationTests` 8 | ME-05 几何权威已备；Map Engine 侧谓词/度量/索引为零 |
| Field Dataset 持久化与包 | 43 + 47 项测试 | ME-05 地基最完整的一块 |
| Branch / History 实体与分叉规则 | `BranchLineageTests` 8、`MapBranchRulesTests` 12 | 结构在；Command/Event 存储不在 |
| Workspace 上下文解析 | `ValidatedWorkspaceContextMappingTests` | 编辑上下文身份链路已备 |

## 4. 结论（对应用户验收关注点）

1. **"通用交互框架先行"与"领域能力先行"不是二选一**：roadmap 已裁定 WP-50（框架）→ WP-60（专项）顺序，ME-06 合同完备度高，框架可先行且不必等待任何 Core Domain 新能力。
2. **不可为"尽快看到编辑器界面"提前引入不完整写入机制**：当前唯一正确的写入相关现状是——没有任何正式编辑写入路径存在，任何"临时写入通道"都将制造第二事实源，直接违反 roadmap §2.2 与 ME-08 §2。
3. **ME-08 正式操作的最小解锁链**：typed Operation/Command 采纳 → Command/Event 持久化（与 WP-40 的历史重建共享同一基建）→ Relationship 模型 → 逐类型 Connection/Interior 采纳。这条链同时是 WP-40 完整目标的依赖，二者应共用同一批领域能力投资。
