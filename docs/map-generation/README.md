# World View — Map Generation Documentation

- 文档状态：目录索引与版本使用说明
- 版本：V1.6
- 日期：2026-11-20
- 目录：`/World View/Map Generation/`

## 1. 目录用途与产品前提

World View 是面向个人创作者的单用户、单机软件。地图、世界设定、地图对象、图层、布局、任务记录和正式历史由本机 World View 管理。用户自行选择并配置主流 LLM 供应商及模型；World View 不默认提供托管模型服务、不替用户购买模型额度，也不将单一供应商设为唯一来源。

AI Assistant 内置于地图编辑器，使用用户配置的 LLM 完成理解、规划和创作辅助，并通过本机 World View 内部工具/API 请求 Domain Services 执行正式地图操作。LLM 不直接读写正式地图数据。

核心地图编辑能力不依赖模型连接即可使用；配置模型后启用 AI 辅助能力。核心地图制作不依赖 Codex、Claude Code、外部 Agent、MCP 或开发者工具。

## 2. 当前有效文档与阅读顺序

| 顺序 | 文档 | 当前版本 | 职责 |
|---|---|---|---|
| 1 | `world-view-ai-assistant-single-user-local-first-architecture-v1.1.md` | V1.1 | 单机单用户形态、用户自带 LLM、本机数据权威和隐私边界 |
| 2 | `world-view-ai-assistant-product-architecture-v1.0.md` | V1.0 | AI Assistant 总体产品定位、内部架构、权限、知识来源和质量保证 |
| 3 | `world-view-ai-assistant-user-experience-v1.0.md` | V1.0 | 用户自带 LLM 连接、任务交互、进度、预览、确认和异常恢复 |
| 4 | `world-view-llm-provider-integration-architecture-v1.0.md` | V1.0 | Provider Adapter、模型能力检测、工具调用安全、凭据存储、上下文和网络边界 |
| 5 | `world-view-provider-adapter-contract-and-tool-calling-protocol-v1.0.md` | V1.0 | 统一消息/请求/响应契约、Adapter 接口、能力快照、工具调用闭环、错误、取消、幂等与并发 |
| 6 | `world-view-internal-tool-catalog-v1.0.md` | V1.0 | AI Assistant 可调用的本机地图领域工具、风险等级、预览/候选与正式应用边界 |
| 7 | `world-view-ai-assistant-context-builder-v1.0.md` | V1.0 | 任务驱动的本机上下文检索、空间/历史/Canon 解析、来源标注、预算与隐私控制 |
| 8 | `02-map-generation-specification-v0.2.md` | V0.2 | AI 如何理解用户地图任务、提取约束、来源和验收条件 |
| 9 | `03-map-generation-workflow-v0.2.md` | V0.2 | 地图任务如何规划、执行、验证、预览并正式应用 |
| 10 | `04-generation-session-lifecycle-v0.2.md` | V0.2 | 复杂任务的后台生命周期、候选隔离、用户决策、并发保护和审计 |

阅读关系：单机架构补充约束产品形态和数据权威；总体架构定义 AI Assistant 与本机领域服务边界；用户体验定义用户如何配置和使用 AI；Provider Integration 定义供应商接入与安全架构；Adapter Contract 定义内部模型协议；Internal Tool Catalog 定义可调用的地图能力；Context Builder 定义向模型提供哪些本机信息以及如何保护数据；02/03/04 分别细化任务规格、执行流程和生命周期。

## 3. 当前架构边界

1. **产品形态**：单用户、单机个人软件，不引入 SaaS、Tenant、Team 或多人协作作为核心前提。
2. **模型所有权**：用户自行选择、配置并承担其 LLM 供应商的账号和费用关系；World View 提供统一连接配置和 Provider Adapter。
3. **本机数据权威**：地图正式数据、世界设定、权限边界、领域约束和历史规则由本机 World View 负责。
4. **统一交互入口**：用户面对一个内置 AI Assistant，不需要理解多个 Agent 或后台编排角色。
5. **受控执行**：LLM 不直接读写数据库；经内部工具/API 提出受控调用，由本机代码校验并调用 Domain Services。
6. **按需结构化**：简单、明确、低风险的编辑可以直接执行；复杂任务才按需建立 Spec、Plan、Session、Candidate 或 Change Set。
7. **按风险确认**：不要求每次变更都人工批准。高影响、重大歧义、正式设定冲突或超出授权范围的操作应暂停并请求用户决策。
8. **模型不可用时可继续工作**：没有配置模型或供应商连接失败时，手工地图编辑、保存、导入和导出仍应正常工作。
9. **正式历史统一**：正式地图变更遵循既定 Command、State Change、Branch、Checkpoint 和历史重建机制；AI Task Session 不是另一套地图历史系统。
10. **外部工具非依赖**：未来若支持外部 Agent、MCP 或开发者工具，只能作为可选扩展，不得成为核心地图制作的运行前提。
11. **最小必要数据传输**：云端模型请求仅发送当前任务所需上下文；不默认上传完整地图或全部本机文件。API Key 保存在本机安全凭据存储中。
12. **能力按实际验证**：供应商和模型能力以连接、模型、端点及适配器版本为维度验证；不能仅凭 API 兼容标签推断工具调用或结构化输出能力。
13. **协议与执行隔离**：Provider Adapter 只负责模型协议转换；Tool Gateway 校验模型请求；Domain Services 执行正式地图操作。模型工具调用不等于授权或执行成功。
14. **幂等与并发保护**：写操作必须绑定本机操作 ID 和地图基准状态；旧请求不得覆盖新地图状态，重试不得重复创建对象或历史事件。
15. **上下文最小化**：Context Builder 按任务检索本机权威数据，标注来源、正式性、Branch、时间和版本；不以整图上传或无边界聊天记忆代替上下文构建。
16. **发送前数据控制**：本机上下文发送到用户配置的远端模型前，执行必要性、敏感性和用户策略检查；Manifest 记录来源与发送范围。

## 4. 版本优先级与历史文件

以下文件为历史版本，保留用于追溯，不是当前设计或实现依据：

- `02-map-generation-specification-v0.1.md`
- `03-map-generation-workflow-v0.1.md`
- `04-generation-session-lifecycle-v0.1.md`
- `README-v1.0.md`
- `README-v1.1.md`
- `README-v1.2.md`
- `README-v1.3.md`
- `README-v1.4.md`

若旧版与当前 V1.0/V1.1/V0.2 文档不一致，以当前有效文档及其明确的上位约束为准。V0.1 中涉及外部 Agent 执行、旧式 Orchestrator/Generator/Validator 分工，以及默认所有正式写入均需人工批准的表述，均不得覆盖当前基线。

## 5. 当前不在本目录冻结的事项

- 首批正式支持的 LLM 供应商及模型兼容矩阵；
- 最终编程语言、SDK、统一契约的精确类型定义与 JSON Schema 方言；
- Windows/macOS/Linux 安全凭据存储的具体库和最低系统版本；
- 模型能力探测测试、缓存有效期和探测成本；
- 地图上下文裁剪、分块、摘要和多模态数据发送策略的具体参数；
- 工具调用轮数、并发数、payload 大小和预算的具体数值；
- Spec、Session、Candidate、Change Set 的持久化结构；
- 任务状态机、并发、取消、重试和恢复语义的工程参数；
- 几何、拓扑、语义和内容一致性校验的等级与阻断策略；
- 幂等、事务、审计与本地数据保留策略；
- Context Manifest 的最终持久化结构、保留期限和本地索引方案；
- 空间检索、几何简化、token 预算和供应商多模态能力的具体实现参数。

本索引只明确文档关系和当前架构方向，不将上述待设计事项提前冻结。

## 6. 维护规则

- 新设计应优先修订当前版本文档，不应继续扩展已被取代的旧方案。
- 产生新版本时保留旧版本作为历史记录，并更新本 README 的当前版本、阅读顺序和差异说明。
- 若单机产品定位、用户自带 LLM 的原则或总体架构方向变化，应先更新约束性补充文档，再同步审查用户体验、Provider Integration、Adapter Contract、Internal Tool Catalog、Context Builder 及 02/03/04 文档。
- 不得将 World View 托管 LLM、强制单一供应商、多人协作、外部 Agent/Codex/Claude Code/MCP 或外部 HTTP 接入重新写成核心地图制作的必要条件，除非产品方向经用户明确重新决策。