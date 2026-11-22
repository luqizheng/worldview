# World View — AI Assistant Task Model & Lifecycle

- 文档版本：V1.0
- 日期：2026-11-20
- 状态：架构设计基线（包含已确认产品规则与待工程验证项）
- 适用范围：World View 单用户、单机桌面软件；AI Assistant 使用用户自行配置的 LLM
- 上位文档：`world-view-ai-assistant-product-architecture-v1.0.md`、`world-view-ai-assistant-context-builder-v1.0.md`、`world-view-internal-tool-catalog-v1.0.md`

## 1. 目的

本文定义 AI Assistant 执行用户工作时使用的 Task、Task Step、Task Log 与 Tool Call 数据模型，以及 Task 和 Step 的生命周期。

Task 是一次完整用户工作的管理单位。Task Planning & Execution Engine 负责规划、协调、执行和验证；地图正式数据仍由 World View Domain Services 管理。Task 记录工作过程和结果引用，不复制正式地图数据。

## 2. 核心设计约束

1. World View 是单用户、单机软件。V1.0 不设计多人并发、分布式执行、多实例竞争、分布式队列、租约或分布式锁。
2. 默认顺序执行。保留异步执行、取消、超时和异常退出恢复能力，但不引入通用 DAG 工作流引擎。
3. AI 可以在授权范围内调整执行方法；不得擅自改变用户目标、扩大范围、降低验收要求或突破授权。
4. Task 完成不等于正式地图已经修改。正式地图应用仍须经过 Change Set 与 Domain Services。
5. Task、Step、Tool Call 和日志只保存必要数据与引用；大型几何或候选内容由相应数据存储管理。

## 3. 核心实体

### 3.1 Task

Task 表示用户交给 AI Assistant 的一项完整工作。

建议字段：

- `task_id`
- `generation_session_id`（可选；非地图生成任务不必创建 Generation Session）
- `user_request`：用户原始请求
- `objective`：结构化目标
- `context_refs`：上下文引用
- `plan_version`
- `status`
- `result_refs`
- `created_at`、`started_at`、`finished_at`

Task 不直接复制地图、Branch、对象或历史数据；使用稳定引用和版本信息。

### 3.2 Task Step

Task Step 是 Task 中一个可执行、可验证的工作单元。

建议字段：

- `step_id`
- `task_id`
- `sequence`
- `name`
- `objective`
- `description`
- `input_refs`
- `action`
- `acceptance_criteria`
- `tool_refs` 或所需能力
- `output_refs`
- `status`
- `retry_count`
- `error`

默认使用 `sequence` 表达顺序，后续步骤通过前序步骤输出引用取得输入。V1.0 不建立通用依赖图。

### 3.3 Task Log

Task Log 记录重要业务事件和状态变化，例如：

- Task 创建、计划生成或计划版本变化；
- Step 开始、完成、失败或跳过；
- 用户确认、拒绝、取消；
- 回滚开始、完成或失败；
- Task 完成或终止。

Task Log 不记录每个内部函数调用，也不重复保存 Tool Call 的完整输入输出。

### 3.4 Tool Call

Tool Call 是一次具体工具调用的持久化执行记录，用于诊断、幂等保护和异常恢复。

建议字段：

- `call_id`
- `task_id`
- `step_id`
- `tool_id`
- `status`
- `input_ref`
- `output_ref`
- `error`
- `operation_id` / `idempotency_key`（写入工具需要）
- `started_at`
- `finished_at`

Tool Call 不是独立调度系统。它记录一次调用的当前状态和结果引用；Task Log 记录重要业务事件，两者职责不同。

## 4. Task 生命周期

建议状态：

- `Created`
- `Planning`
- `Ready`
- `Running`
- `Waiting for User`
- `Paused`
- `Rolling Back`
- `Recovery Required`
- `Completed`
- `Failed`
- `Cancelled`

其中 `Rolling Back` 和 `Recovery Required` 是失败恢复过程状态。只有恢复完成后，Task 才能进入 `Failed` 或 `Cancelled`。`Recovery Required` 表示自动恢复未完成，不得伪装成普通失败终态。

典型转换：

```text
Created -> Planning -> Ready -> Running -> Completed
                         |         |
                         |         +-> Waiting for User -> Running
                         |         +-> Paused -> Running
                         |         +-> failure/timeout/cancel -> Rolling Back
                         |                                      |
                         |                                      +-> Failed
                         |                                      +-> Cancelled
                         |                                      +-> Recovery Required
                         +-> Failed
```

## 5. Task Step 生命周期

Step 状态：

- `Pending`
- `Running`
- `Completed`
- `Failed`
- `Skipped`

Step 失败不必立即使整个 Task 进入 Failed。Coordinator 可以在安全范围内修正输入、重试或重新规划；如果 Task 无法继续，则启动 Task 级整体回滚。

## 6. 计划版本与动态重规划

Task Plan 有版本号。重新规划时创建新 Plan Version，并保留旧版本与变更原因。

允许 AI 在授权范围内调整：

- 工具选择；
- 执行顺序（不改变既定依赖和目标）；
- 参数和实现方法；
- 对失败步骤的安全重试或替代方法。

不得擅自调整：

- 用户原始目标；
- 交付范围；
- 验收标准；
- 权限边界；
- 需要人工确认的正式语义或基线。

已完成步骤只有在输入失效、输出不再适用或验收要求经授权变化时才应重做。

## 7. 重试与恢复

V1.0 不单独建立 Task Attempt 实体。通过 Tool Call、Step 的 `retry_count` 和 Task Log 保存足够的执行历史。

重试前必须判断：

1. 上一次调用是否仍在执行；
2. 上一次调用是否已经成功但响应丢失；
3. 是否产生了可复用结果；
4. 写入操作是否具备幂等保护；
5. 当前输入和业务前置条件是否仍然有效。

软件异常退出后，启动恢复流程检查未完成 Task 和 Tool Call。未能确认执行结果的写入调用不得盲目重放。

## 8. Task 原子性与正式地图边界

用户已明确选择 Task 级整体回滚：一个 Task 要么全部成功并保留其本次工作结果，要么失败/取消后撤销本 Task 已产生的全部可回滚修改。

V1.0 的实现边界：

- Task 执行期间产生的临时候选、暂存数据和 Task 私有工作结果纳入 Task 回滚范围。
- 失败时不只撤销最后一个 Step，而是撤销整个 Task 的修改。
- Task 的日志和失败诊断记录保留，不随候选数据一起删除。
- 正式地图应用是独立的 Change Set + Domain Services 流程，不作为普通 AI Tool Call 的任意写操作。已经完成并提交的正式应用不因后续另一个 Task 失败而自动撤销。
- 外部不可逆副作用不能承诺通过本机 Undo 恢复；应尽量避免纳入 Task 内部写操作，或明确要求独立确认。

## 9. 验收标准

1. 普通对话不必创建 Task；需要执行的工作由 Planner 决定是否创建 Task。
2. 简单和复杂任务使用同一 Task 模型，简单任务可只有一个 Step。
3. Task 与正式地图历史分离，不复制正式地图数据。
4. Task、Step、Tool Call 状态可持久化，并能在重启后识别未完成执行。
5. Task 计划变更保留版本和原因。
6. Task 完成不代表正式地图已应用。
7. 失败或取消时按 Task 边界整体回滚可回滚修改。
8. 恢复失败进入 `Recovery Required`，不得标记为已恢复。