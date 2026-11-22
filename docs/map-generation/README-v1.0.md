# World View — Map Generation Documentation

- 文档状态：目录索引与版本使用说明
- 版本：V1.0
- 日期：2026-11-20
- 目录：`/World View/Map Generation/`

## 1. 目录用途

本目录记录 World View 内置 AI Assistant 的地图制作产品方向、任务理解、执行工作流和任务生命周期设计。

World View 的地图制作能力内置于产品本身。用户通过地图编辑器中的 AI Assistant 描述目标；AI Assistant 理解意图、组织任务、执行并检查结果；正式地图数据由 World View Domain Services 按领域规则管理。

核心地图制作不得依赖 Codex、Claude Code、外部 Agent、外部 HTTP/MCP 服务或开发者工具才能运行。

## 2. 当前有效文档与阅读顺序

建议按以下顺序阅读：

| 顺序 | 文档 | 当前版本 | 职责 |
|---|---|---|---|
| 1 | `world-view-ai-assistant-product-architecture-v1.0.md` | V1.0 | 总体产品定位、架构边界、用户交互、权限、知识来源、质量保证与第一版范围；本目录的总体方向依据 |
| 2 | `02-map-generation-specification-v0.2.md` | V0.2 | AI Assistant 如何理解用户地图任务、提取约束、来源和验收条件 |
| 3 | `03-map-generation-workflow-v0.2.md` | V0.2 | 地图任务如何规划、执行、验证、预览并正式应用 |
| 4 | `04-generation-session-lifecycle-v0.2.md` | V0.2 | 复杂任务的后台生命周期、候选隔离、用户决策、并发保护和审计 |

上述文档共同构成当前地图生成设计基线。总体架构文档定义产品与架构边界；02/03/04 分别细化任务规格、执行流程和生命周期，不应相互替代。

## 3. 版本优先级与历史文件

目录中的以下文件为历史版本：

- `02-map-generation-specification-v0.1.md`
- `03-map-generation-workflow-v0.1.md`
- `04-generation-session-lifecycle-v0.1.md`

这些 V0.1 文件保留用于追溯早期讨论，不是当前设计或实现依据。若其内容与对应 V0.2 或总体架构 V1.0 不一致，以当前版本为准。

特别是，V0.1 中涉及外部 Agent 执行、旧式 Orchestrator/Generator/Validator 分工，以及默认所有正式写入均需人工批准的表述，均不得覆盖当前基线。当前规则以 V1.0 和各 V0.2 文档为准：由一个内置 AI Assistant 统一承担用户可见职责；后台步骤可由内置模型、确定性算法或内部领域服务完成；是否需要预览或用户决策取决于风险、影响范围、可逆性、语义重要性和用户授权。

## 4. 当前架构边界

1. **统一入口**：用户面对 World View 内置 AI Assistant，不需要理解多个 Agent 或内部编排角色。
2. **内部执行**：AI Assistant 通过 World View 内部工具/API 调用领域能力；内部 API 是产品内部边界，不代表需要外部 HTTP/MCP 接入。
3. **数据权威**：LLM 不直接读写数据库。正式地图数据、权限、领域约束和历史规则由 Domain Services 负责。
4. **按需结构化**：简单、明确、低风险的编辑可以直接执行；复杂任务才按需建立 Spec、Plan、Session、Candidate 或 Change Set。
5. **按风险确认**：不要求每次变更都人工批准。高影响、重大歧义、正式设定冲突或超出授权范围的操作应暂停并请求用户决策。
6. **正式历史统一**：正式地图变更遵循既定 Command、State Change、Branch、Checkpoint 和历史重建机制；AI Task Session 不是另一套地图历史系统。
7. **可选扩展不构成依赖**：未来若支持外部 Agent、MCP 或开发者工具，只能作为可选扩展，不得成为核心地图制作的运行前提。

## 5. 当前不在本目录冻结的事项

各文档中的“待后续技术设计”仍需后续专题确定，包括但不限于：

- 内部工具/API 的正式契约与权限粒度；
- Spec、Session、Candidate、Change Set 的持久化结构；
- 任务状态机、并发、取消、重试和恢复语义；
- 几何、拓扑、语义和内容一致性校验的等级与阻断策略；
- 幂等、事务、审计与数据保留策略；
- 模型选择、资源预算、可观测性与本地/云端模型接入方式。

本索引只明确文档关系和当前架构方向，不将上述待设计事项提前冻结。

## 6. 维护规则

- 新设计应优先修订当前版本文档，不应继续扩展已被取代的 V0.1 方案。
- 产生新版本时保留旧版本作为历史记录，并更新本 README 的当前版本、阅读顺序和差异说明。
- 若总体架构方向变化，应先更新总体架构文档，再同步审查 02/03/04 文档。
- 不得将外部 Agent、Codex、Claude Code、MCP 或外部 HTTP 接入重新写成核心地图制作的必要条件，除非产品方向经用户明确重新决策。