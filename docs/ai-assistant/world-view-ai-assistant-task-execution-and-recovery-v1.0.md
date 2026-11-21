# World View — AI Assistant Task Execution & Recovery

- 文档版本：V1.0
- 日期：2026-11-20
- 状态：架构设计基线（Task 级整体回滚规则已由用户明确选择；实现细节待工程验证）
- 适用范围：World View 单用户、单机桌面软件
- 上位文档：`world-view-ai-assistant-task-model-and-lifecycle-v1.0.md`、`world-view-ai-assistant-task-planning-engine-v1.0.md`、`world-view-ai-assistant-tool-contract-v1.0.md`、`world-view-internal-tool-catalog-v1.0.md`

## 1. 目的

本文定义 Task Coordinator、LLM Tool Calling、Tool Executor 的配合方式，以及超时、取消、失败、重复调用和程序异常退出时的恢复机制。

核心目标是：执行路径简单、状态可追踪、失败后不遗留本次 Task 的部分修改，并且不因响应丢失而重复执行写入操作。

## 2. 总体执行流程

```text
Task Coordinator
      |
      v
AI Assistant / LLM
  (proposes tool call)
      |
      v
Tool Executor
  - Registry lookup
  - Input validation
  - Permission / precondition checks
  - Persist Tool Call
      |
      v
Internal Tool
      |
      v
Domain Services (where domain mutation is required)
      |
      v
Tool Result Handler
  - Validate output
  - Persist result reference
  - Update Tool Call
      |
      v
Task Coordinator
  - Continue
  - Re-plan
  - Wait for user
  - Fail / Cancel and rollback
```

以上是一个单机程序内部的逻辑职责划分，不要求拆成独立服务或进程。

## 3. 执行职责

### Task Coordinator

- 选择当前可执行 Step；
- 向 AI Assistant 提供当前 Step 目标、输入和可用工具；
- 接收 Tool Result；
- 决定继续、重新规划、等待用户或终止 Task；
- 在失败/取消时启动 Task 级回滚。

### AI Assistant / LLM

- 根据 Step 目标判断下一步动作；
- 提出工具调用及结构化参数；
- 解释工具结果；
- 在授权范围内提出方法调整或新计划。

LLM 不直接执行内部函数、不写数据库、不控制恢复状态，也不能自行宣告 Undo 成功。

### Tool Executor

- 校验工具、输入、权限和前置条件；
- 创建 Tool Call 记录；
- 调用本机工具；
- 处理超时和取消信号；
- 验证并保存结果；
- 对写入操作应用幂等保护。

### Tool Result Handler

- 统一处理成功、失败、拒绝和超时结果；
- 保存小型结果或大型数据引用；
- 更新 Tool Call；
- 将结构化结果交还 Coordinator。

## 4. Task 级整体原子性

用户明确选择：Task 失败时采用整个 Task 回滚，而不是只撤销失败的最后一个 Step。

规则：

1. Task 执行期间产生的所有可回滚修改都归属于该 Task。
2. 任一步发生不可恢复的执行失败、超时或用户取消，停止后续 Step。
3. 当前工具停止产生新修改后，撤销该 Task 已产生的全部可回滚修改。
4. 回滚成功后才将 Task 标记为 `Failed` 或 `Cancelled`。
5. 回滚失败则进入 `Recovery Required`，保留恢复记录，禁止继续产生新修改。
6. Task Log、错误信息和恢复审计记录保留，不随候选数据清理。

### 4.1 暂存优先

优先让 Task 在专属暂存区、Generation Session 或候选数据中工作，不在 Task 执行过程中直接修改正式地图。

- 候选和临时数据带有 Task 来源归属。
- Task 成功后，候选可进入验证、预览和用户决策流程。
- Task 失败/取消时，清理或撤销本 Task 创建的候选和临时修改。
- 正式地图应用仍须通过 Change Set + Domain Services。

### 4.2 正式提交边界

Task 原子性不意味着把所有后续正式地图编辑都包进一个无限期数据库事务。正式 Change Set 应用是独立、明确的提交边界。

已经完成并提交的正式地图应用，不会因为之后另一个 Task 失败而被自动撤销。不可逆外部副作用也不能被本机 Undo 虚假承诺可恢复。

## 5. 超时处理

工具超时表示在预定时间内没有获得确定结果，不等于工具一定没有执行成功。

处理顺序：

1. 标记调用进入超时处理，不立即重新发起同一写操作。
2. 请求当前工具停止。
3. 查询 Tool Call / operation_id，确认操作是否仍在运行、已经成功或已经失败。
4. 确认工具已停止产生修改。
5. 如果 Task 无法继续，启动整个 Task 回滚。
6. 回滚完成后将 Task 标记为 Failed。

不得仅因收到超时就假设“什么都没发生”。

## 6. 用户取消

取消顺序：

1. 停止接受新的 Step 和 Tool Call；
2. 向当前工具发送取消请求；
3. 等待工具确认停止，或通过工具自身的安全终止机制确保不再写入；
4. 执行 Task 级整体回滚；
5. 验证恢复；
6. 恢复成功后标记 `Cancelled`。

取消不是在 Undo 与正在写入的工具之间并行竞速。必须先确保工具不再产生修改，再开始回滚。

## 7. 程序异常退出与启动恢复

World View 启动时检查上次进程遗留的未完成 Task 和 Tool Call。

建议流程：

1. 找出 `Running`、`Rolling Back` 或存在 `Interrupted` Tool Call 的 Task。
2. 读取持久化执行记录和 Task 修改归属。
3. 对结果不确定的写入调用，先按 operation_id 查询实际效果。
4. 确保没有遗留执行进程继续写入。
5. 若 Task 尚未完成最终成功提交，则按 Task 级规则执行整体回滚。
6. 验证恢复结果。
7. 成功后将 Task 归类为异常失败；失败则保留为 `Recovery Required`。
8. 不盲目重放崩溃前正在执行的写入 Tool Call。

V1.0 的保守策略是先恢复到一致状态，再由用户重新发起任务，而不是自动从任意中间步骤续跑。

## 8. Undo / Rollback 机制

Undo 必须由确定性程序实现，不能让 LLM 临场决定要删除或修改哪些数据。

建议：

- 每个可回滚写入在执行前登记 Task 所属和恢复所需信息；
- 恢复信息必须先持久化，再允许对应写入生效；
- Undo 按 Task 的操作记录逆序执行，或通过任务专属暂存数据整体丢弃；
- Undo 每个恢复动作都应可检测、可重入；
- 恢复完成后进行状态校验；
- 保留回滚日志和原始错误。

如果 Undo 自身失败，不能报告恢复成功。进入 `Recovery Required`，阻止进一步写入，等待后续恢复处理。

## 9. 重复调用与幂等

场景：工具已创建候选对象，但成功响应在返回途中丢失。若直接重试，就会创建重复对象。

规则：

1. 每次 Tool Call 有唯一 `call_id`。
2. 每个写入业务操作有稳定 `operation_id` / 幂等键。
3. 写入前持久化操作意图和恢复信息。
4. 重试前查询该 operation_id 是否已有结果。
5. 已成功则复用结果；仍在执行则先等待或停止；明确失败才按策略重试。
6. 结果不确定时不得盲目重复执行。
7. Task 正在回滚时，拒绝该 Task 的新写入调用。

V1.0 使用本机数据库和持久化记录实现，不建设分布式幂等服务。

## 10. Task / Step / Tool Call 状态同步

- Coordinator 是 Task 和 Step 状态转换的唯一协调者。
- Tool Executor 负责更新 Tool Call 的执行状态。
- Tool Result Handler 将 Tool Call 结果交回 Coordinator。
- Coordinator 根据 Step 验收条件决定 Step 状态及 Task 后续状态。
- Tool Call 成功不自动意味着 Step 成功；Step 必须满足 Acceptance Criteria。
- Step 成功也不自动意味着 Task 完成；Task 必须完成全部计划步骤及任务级验收。
- 进入回滚后，不允许 Coordinator 再派发新的正常写入步骤。

## 11. 状态建议

Task 增加或明确以下恢复状态：

- `Rolling Back`：正在执行 Task 级回滚。
- `Recovery Required`：回滚失败或无法确认恢复完整性，需要恢复处理。

终态规则：

- 执行失败且回滚成功：`Failed`。
- 用户取消且回滚成功：`Cancelled`。
- 全部步骤与任务级验收成功：`Completed`。
- 回滚尚未成功：保持 `Recovery Required`，不得伪装为 Failed/Cancelled 已完成恢复。

## 12. 验收标准

1. 所有工具调用统一经过 Tool Executor。
2. LLM 不能绕过工具边界直接修改正式数据。
3. 超时后先检查实际执行状态，不盲目重试。
4. 用户取消先停止工具，再执行整体回滚。
5. Task 任一步失败时，撤销本 Task 全部可回滚修改。
6. 软件重启后可识别未完成 Task，并先恢复一致状态。
7. Undo 失败会阻止后续写入并进入 `Recovery Required`。
8. 写入重试不会重复创建候选或对象。
9. Task、Step、Tool Call 的状态职责明确。
10. 正式地图变更仍通过 Change Set + Domain Services，不被 Task 内部回滚机制绕过。