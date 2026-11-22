# World View — AI Assistant Tool Contract & Registry

- 文档版本：V1.0
- 日期：2026-11-20
- 状态：架构设计基线（具体 Schema 方言和 API 细节待工程验证）
- 适用范围：World View 单用户、单机桌面软件
- 上位文档：`world-view-internal-tool-catalog-v1.0.md`、`world-view-provider-adapter-contract-and-tool-calling-protocol-v1.0.md`、`world-view-ai-assistant-task-model-and-lifecycle-v1.0.md`

## 1. 目的与边界

本文定义本机 AI 工具的统一契约、静态注册表、工具目录视图、执行结果和错误模型。

`world-view-internal-tool-catalog-v1.0.md` 定义地图领域中有哪些工具及其业务边界；本文定义这些工具如何被程序统一描述、验证和调用。两份文档互补，不重复维护工具业务清单。

LLM 只能提出结构化工具调用请求。Tool Executor 负责校验和执行。LLM 不直接调用内部函数、不访问数据库，也不拥有任意文件、Shell、SQL 或 HTTP 能力。

## 2. Tool Definition

每个工具在程序代码中静态定义，建议包含：

- `tool_id`
- `name`
- `description`
- `category`
- `input_schema`
- `output_schema`
- `operation_type`
- `preconditions`
- `permissions`
- `side_effects`
- `timeout`
- `retry_policy`
- `version`

V1.0 不为 Tool Definition 建立数据库表。程序启动时加载静态定义并注册到 Tool Registry。

## 3. 输入 Schema

使用受限 JSON Schema 子集描述工具输入，只实现实际需要的能力：

- `type`
- `properties`
- `required`
- `description`
- `enum`
- `minimum` / `maximum`
- `items`
- `additionalProperties`

验证分两层：

1. 结构验证：类型、必填字段、范围、未知字段和基本格式。
2. 业务验证：目标对象存在、Branch/Session 状态、地图修订、领域约束和操作前置条件。

结构验证通过不代表业务操作一定允许。

## 4. 输出 Schema 与 Tool Result

每个工具声明输出结构。大型几何或候选数据不直接发送给 LLM；工具返回摘要和本机数据引用。

统一 Tool Result 状态：

- `Succeeded`
- `Failed`
- `Rejected`
- `TimedOut`

含义：

- `Succeeded`：执行成功，输出符合声明结构。
- `Failed`：执行过程中发生错误。
- `Rejected`：请求因权限、参数或前置条件不满足而未执行。
- `TimedOut`：在规定时间内未能取得确定结果；必须进一步检查调用状态和副作用，不能直接盲目重试。

Tool Call 持久化状态另有 `Created`、`Running`、`Succeeded`、`Failed`、`TimedOut`、`Interrupted`。Tool Result 描述调用结果，Tool Call 描述调用记录生命周期，两者不是同一枚举。

## 5. 错误模型

基础错误类型：

- `Invalid Input`
- `Not Found`
- `Permission Denied`
- `Precondition Failed`
- `Execution Error`
- `Timeout`

结构化错误建议包含：

- `code`
- `type`
- `message`
- `retryable`
- `details`

`retryable` 只是提示，不单独决定是否重试；还需检查工具重试策略、操作副作用、调用是否已经成功、输入是否可修复以及 Task 回滚状态。

## 6. 前置条件与权限

通用前置条件由 Tool Executor 检查：

- 工具已注册且当前可用；
- 输入结构正确；
- 调用方拥有对应权限；
- 调用未超过时间预算；
- Task 当前状态允许执行。

领域前置条件由具体工具和 Domain Services 检查：

- 地图、对象、图层是否存在；
- Branch / Generation Session 是否处于允许状态；
- 地图修订是否匹配；
- 候选是否有效、未过期；
- 几何、拓扑和语义约束是否满足。

工具必须声明操作类型和副作用。操作类别沿用 Internal Tool Catalog 的 Read、Generate、Apply 分层。Apply 不作为普通 LLM Tool Executor 的自由调用能力，正式应用走 Change Set + Domain Services 流程。

## 7. Tool Registry 与 Catalog

- Tool Registry：程序内部静态注册表，保存完整 Tool Definition。
- Tool Catalog：Registry 面向 Planner / LLM 提供的工具能力视图。
- Catalog 根据当前 Task 需要提供工具子集，不把全部工具一次性暴露给模型。
- Planner 可按分类、描述和能力标签筛选工具。
- V1.0 不建设向量数据库、独立工具搜索引擎或动态插件市场。

五类工具业务类别：

1. Information Tools
2. Analysis Tools
3. Generation Tools
4. Validation Tools
5. Candidate Management Tools

这些是逻辑分类，不代表五个独立子系统。

## 8. Tool Executor 的职责

统一 Tool Executor 负责：

1. 根据 `tool_id` 从 Registry 查找定义；
2. 验证输入 Schema；
3. 检查权限和通用前置条件；
4. 创建并持久化 Tool Call；
5. 调用具体内部工具；
6. 接收并验证输出；
7. 保存输出或数据引用；
8. 更新 Tool Call 状态；
9. 将结果交给 Task Coordinator。

具体领域规则仍由工具实现和 Domain Services 负责，不能在 Executor 中复制一套地图业务逻辑。

## 9. Tool Call、幂等与重试

每次调用有唯一 `call_id`。写入工具还必须使用稳定的本机 `operation_id` / 幂等键。

重试规则：

- 只读工具通常可按策略重新执行。
- 写入工具重试前必须先查询上次操作结果。
- 如果上次操作已成功，复用原结果，不重复创建数据。
- 如果上次操作仍在执行，先停止或等待，不并行发起重复调用。
- 如果结果不确定，进入检查/恢复流程，不盲目重放。
- 幂等保护是本机持久化机制，不引入分布式幂等服务。

## 10. 与 Task Log 的关系

Tool Call 保存一次调用的状态、输入/输出引用、错误和时间。Task Log 只记录重要业务事件，例如“工具调用失败并触发 Task 回滚”。避免将相同的完整数据同时复制到两处。

## 11. 验收标准

1. 模型只能调用当前暴露且已注册的工具。
2. 每个工具具有输入、输出、权限、副作用和版本契约。
3. 结构验证与业务验证分离。
4. 工具结果状态和 Tool Call 生命周期状态不混淆。
5. Apply 不作为普通 AI 工具自由调用。
6. 写入工具有本机幂等保护，超时后不盲目重复执行。
7. 不引入通用工具搜索引擎或分布式执行基础设施。