# World View AI Assistant Task Planning & Execution Engine V1.0

- 文档类型：架构设计基线
- 版本：V1.0
- 日期：2026-11-20
- 状态：整体架构审查完成，作为 V1.0 实施基线
- 适用范围：World View 单机应用内置 AI Assistant 的任务规划、执行、验证、候选成果管理、正式应用与恢复

## 1. 目的与范围

本文定义 World View 内置 AI Assistant 的 Task Planning & Execution Engine V1.0。目标是让 AI 能够理解用户意图、制定计划、调用内部工具、生成和验证候选地图数据，并在用户明确授权后，通过正式领域服务将选定结果应用到目标 Branch。

本引擎属于单机桌面应用内部能力，不是独立服务平台。V1.0 优先保证简单、可理解、可维护和故障后可恢复，不引入与当前产品规模不匹配的分布式基础设施。

### 1.1 产品与架构约束

1. World View 是单机应用；当前不考虑多人协作、多用户并发、分布式执行或多实例竞争。
2. 内置 AI Assistant 是用户主要交互入口。LLM 负责理解、分析和提出结构化行动，不直接操作数据库或绕过领域服务。
3. Task Planning & Execution Engine 负责计划、协调、执行、验证、恢复和交付。
4. World View Domain Services 是正式地图数据的唯一业务修改入口。
5. AI 生成结果先进入 Candidate，不直接成为正式地图数据。
6. 正式地图修改必须通过 Change Set，并经过明确的人类授权和原子应用。
7. Generation Session、Candidate Set 与 Branch 正式历史相互区分。
8. AI 可以在原目标、范围、验收条件、授权和预算内自主调整执行方法；不得擅自改变这些约束。
9. 技术方案优先采用本地持久化、短事务和确定性恢复，避免过度抽象。

### 1.2 V1.0 不负责的内容

- 多人协作、分布式任务调度和多实例竞争。
- 通用 DAG 工作流引擎。
- 通用事件溯源系统。
- AI 自主正式应用地图数据。
- 自动融合不同候选几何版本。
- 复杂候选分支合并、多人冲突解决。
- 跨多个独立 Change Set 的自动批量事务。
- 无法保证回滚的外部不可逆副作用。

## 2. 总体架构

V1.0 采用一个统一的 Task Planning & Execution Engine，内部按职责划分逻辑模块，不要求拆成独立进程或服务。

### 2.1 逻辑模块

1. **Task Planner**：分析意图、收集上下文、生成计划并检查计划。
2. **Task Coordinator**：管理 Task 生命周期、选择步骤、协调工具调用、处理暂停/取消/恢复和重新规划。
3. **Task Executor**：执行已授权的内部工具调用，统一进行参数、权限、前置条件和副作用控制。
4. **Result Validator**：组织结果验证并生成 Validation Report；不直接修改数据。
5. **Candidate Management**：管理 Candidate Set、Object Candidate 和候选版本。
6. **Candidate Selection**：记录用户对候选结果的选择，并固化 Selection Snapshot。
7. **Change Set Management**：将选择快照转为正式变更，检查授权、版本和应用条件。
8. **Application Recovery Coordinator**：在程序启动或故障后核验未完成操作并协调恢复。
9. **World View Domain Services**：负责正式地图领域规则和正式数据修改。

这些是逻辑职责边界，不意味着每个模块都要建立独立服务、线程或数据库。

### 2.2 执行主链

用户请求 → AI Assistant → Task Planner → Task Coordinator → Tool Executor → 内部 Tool / Domain Services → Tool Result Handler → Result Validator → Candidate Management → 用户选择 → Selection Snapshot → Change Set → 人类授权 → Domain Services Atomic Apply。

正式地图应用是独立提交边界。Task 完成仅表示 AI 工作和候选成果已交付，不代表正式地图已经修改。

## 3. Task 与执行约束

### 3.1 Task 的定义

Task 表示一项完整的用户工作。简单任务也使用 Task，可以只有一个 Step。Task 不复制地图数据，而是保存对 Branch、Generation Snapshot、Candidate Set、对象身份和相关版本的引用。

Generation Session 是可选关联；非地图生成任务不必创建 Generation Session。

### 3.2 Task Execution Contract

Task 创建或计划确定时，形成持久化执行约束：

- `user_goal`：用户原始目标。
- `scope`：任务范围。
- `acceptance_criteria`：验收条件。
- `permissions`：允许的操作与授权边界。
- `constraints`：其他不可违反的限制。
- `budget`：时间、调用次数、资源或其他预算上限。

Plan Version 可以变化，但必须遵守同一 Execution Contract。改变目标、扩大范围、降低验收标准、突破授权或预算上限，必须通过 Human Intervention Request 请求用户决定。

### 3.3 Task 生命周期

状态：

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

约束：

1. `Completed` 表示候选成果已经完成交付，不表示正式地图已应用。
2. `Failed` 和 `Cancelled` 只有在 Task 级整体 Undo 成功后，才能成为最终状态。
3. `Recovery Required` 表示系统无法确认数据一致性或无法安全完成恢复；相关写入必须被阻止。
4. `Rolling Back` 期间禁止该 Task 产生新写入。
5. 已完成 Task 的候选成果成为持久交付成果；之后编辑候选结果不重新打开原 Task。

### 3.4 Task Step

Step 是具体执行单元，至少包含：

- `objective`
- `input`
- `action`
- `acceptance_criteria`

状态：`Pending`、`Running`、`Completed`、`Failed`、`Skipped`。

V1.0 默认顺序执行。依赖通过执行顺序和前序输出引用表达，不建设 DAG。动态重新规划保留仍然有效的已完成步骤；只有输入失效、输出不再适用或验收要求变化时才重做。

### 3.5 计划生成

Planner 内部包含四个逻辑环节，不拆成独立 Agent 或服务：

1. Intent Analyzer
2. Context Builder
3. Plan Generator
4. Plan Validator

意图类别：General Conversation、Information Request、Map Operation、Complex Map Creation。复杂度 Simple / Moderate / Complex 只影响规划深度，不改变权限边界。

## 4. Tool Execution Engine

### 4.1 工具架构

- **Tool Registry**：静态注册内置工具，启动时加载。
- **Tool Executor**：统一验证工具、参数、权限、前置条件并执行。
- **Tool Contract**：定义工具输入、输出、操作类型和执行约束。
- **Tool Result Handler**：验证结果、保存结果引用并更新调用记录。

业务工具类别：Information、Analysis、Generation、Validation、Candidate Management。

### 4.2 操作级别

- `Read`：只读。
- `Generate`：生成或修改 Generation Session / Candidate 中的数据，不修改正式地图。
- `Apply`：正式数据应用，不作为普通 AI Tool 调用；必须由 Change Set Management 经 Domain Services 执行。

LLM 只提出结构化 Tool Call。Coordinator 决定执行哪个 Step；Executor 执行工具；Domain Services 保证正式领域规则。LLM 不直接访问内部函数或数据库。

### 4.3 Tool Definition

静态定义字段：

- `tool_id`、`name`、`description`、`category`
- `input_schema`、`output_schema`
- `operation_type`
- `preconditions`、`permissions`、`side_effects`
- `timeout`、`retry_policy`、`version`

Tool Definition 在代码中静态维护，不建立动态 Tool Definition 数据库表。

V1.0 只实现实际需要的 JSON Schema 子集：`type`、`properties`、`required`、`description`、`enum`、`minimum`、`maximum`、`items`、`additionalProperties`。

### 4.4 Tool Call 持久化

Tool Call 至少保存：

- `call_id`
- `task_id`
- `step_id`
- `tool_id`
- `status`
- `input_ref`
- `output_ref`
- `error`
- `started_at`
- `finished_at`

状态：`Created`、`Running`、`Succeeded`、`Failed`、`TimedOut`、`Interrupted`。

`Interrupted` 表示程序退出时调用结果不确定，不等于确定失败。恢复时必须先核验 operation_id 和实际副作用。

### 4.5 幂等性与副作用记录

- `call_id` 标识一次调用记录。
- `operation_id` 标识一个业务动作。
- 每个有副作用的业务动作使用稳定 `operation_id`。
- 同一业务动作重试复用 operation_id；不同动作使用不同 operation_id。
- 执行前持久化操作意图、目标、输入引用和恢复信息。
- 重试前先查询 operation_id：已成功则复用结果；仍在执行则等待或安全停止；明确失败才可按策略重试；状态不确定先核验或回滚，不盲目重放。
- 本地候选数据写入尽可能与 operation 记录在同一数据库事务中。
- 所有有副作用的内部 Tool 必须经过统一的 Tool Execution / Operation Recording 路径，不得绕过。

### 4.6 Task Operation Record

每个可回滚副作用都保存操作记录，至少包括：

- `operation_id`、`task_id`、`tool_call_id`
- 操作类型和目标引用
- 操作前状态引用、操作后状态引用
- 执行状态、Undo 状态
- 创建和完成时间

Task Undo 依据实际写入记录、operation_id 和恢复信息，不只依赖 Step 状态。按依赖逆序回滚。Undo 必须可重入：已回滚跳过；未回滚继续；结果不确定先核验；失败进入 Recovery Required。

## 5. Object Identity 与 Object ID

### 5.1 身份与状态分离

- **Object ID**：稳定、全局唯一的对象标识。
- **Object Identity**：代表跨 Branch 稳定的逻辑对象身份。
- **Branch Object State**：某个 Object 在某个 Branch 历史位置的有效状态。
- **Object Candidate**：尚未正式应用的候选数据版本。
- **Formal Object Version**：正式地图中的对象状态版本。

一个 Object Identity 可以有多个 Candidate Version 和多个 Branch-specific Formal Version。

### 5.2 Object ID Reservation

Object ID 通过按 Object Type 区分的分配机制预留。预留记录至少具有：

- `Reserved`
- `Bound`
- `Inserted`
- `Abandoned`

ID 一经预留即永久消耗。Reserved、Bound、Inserted、Abandoned 均表示该 ID 已占用；Abandoned 永远不能重新分配。候选数据回滚不删除 ID Reservation 历史。

### 5.3 Object Binding

Object Binding 绑定 Object ID 到 Object Identity，不绑定某一份 Candidate 数据。一个正式 Object ID 在 World View 生命周期内最多代表一个逻辑 Object Identity。

不同正式 Object 必须使用不同 Object ID。同一个 Object 的不同候选版本共享 Object ID，但各自有独立 Candidate ID 和数据版本。

## 6. Result Validation

### 6.1 结果阶段严格区分

- `Execution Succeeded`：工具调用成功。
- `Validation Passed`：结果通过相应验证。
- `Candidate Ready`：结果已整理为可检查的候选集合。
- `Applied`：用户授权后，正式变更已通过 Change Set 应用。

这些状态不能互相替代。

### 6.2 五层验证

1. Structural Validation：字段、类型、必填项和结构。
2. Identity Validation：ID、类型匹配、唯一性和引用有效性。
3. Geometry Validation：几何合法性、坐标参考系统和精度。
4. Domain Validation：领域规则、时间和对象关系。
5. Semantic Validation：用户意图、历史背景、任务目标和验收条件。

前四层以确定性程序检查为主。Domain Validation 的领域规则权威实现属于 Domain Services；Validator 组织检查并形成报告，不重复实现领域规则。正式应用时 Domain Services 再次执行必要领域检查。

Semantic Validation 可由 AI 辅助，但必须区分来源支持、部分支持、来源冲突、AI 推断和无法确定。AI 判断不能冒充事实证明。

### 6.3 严重程度与报告

Issue 严重程度：`Error`、`Warning`、`Info`。

Validation Report 至少保存：

- `validation_id`
- `candidate_set_id`
- `validator_version`
- `validation_status`
- `started_at`、`finished_at`
- `issue_count`、`issues`
- `validated_object_refs`

Issue 至少保存：`issue_id`、`object_id`、`rule_id`、`severity`、`description`、`evidence`、`suggested_action`、`resolution_status`。

报告必须绑定被验证对象的精确数据版本。数据变化后旧报告对新版本失效。Validator 只检查，不直接修复；修复由 Task Execution Engine 协调，修复后重新验证。

支持 Object 独立验证和 Candidate Set 整体验证。正式应用前还需检查 Change Set 整体和跨对象关系。

## 7. Candidate Management

### 7.1 Candidate Set

Candidate Set 是一组等待用户检查和决定是否采用的地图对象及其候选修改方案，不是新的正式地图。

一个 Task 可以生成多个 Candidate Set，例如历史资料严格型、区域覆盖型、细节丰富型。不同 Candidate Set 独立保存，不共享可变候选数据。

Candidate Set 至少保存：`candidate_set_id`、`task_id`、`name`、`description`、`generation_method`、`status`、`created_at`、`updated_at`。

### 7.2 Object Candidate

Object Candidate 至少保存：

- `candidate_id`
- `candidate_set_id`
- `object_id`
- `object_version`
- `candidate_status`
- `validation_report_ref`
- `source_refs`
- `created_at`

同一 Object Identity 在不同 Candidate Set 中可以有不同候选版本，共用 Object ID，但 Candidate ID 和数据版本独立。若生成的是不同逻辑对象，必须分配不同 Object ID。

### 7.3 候选版本规则

候选数据不可变。用户修改候选对象时创建新 Candidate Version，不覆盖历史版本。修改后旧 Validation Report 不再证明新版本有效；新版本必须重新验证。

候选对象修改不会自动继承旧版本的接受状态。修改后的版本必须重新由用户接受。

候选编辑记录可用于 Task 完成后的轻量编辑；复杂 AI 修改工作创建新 Task，不重新打开原 Task。

### 7.4 Candidate Set 状态

- `Generating`
- `Validating`
- `Ready`
- `Under Review`
- `Accepted`
- `Partially Accepted`
- `Rejected`
- `Superseded`

Validation Status 与 Selection Status 分离。Validation Passed 不代表用户接受；用户接受也不能绕过 Error。Candidate Set 被修改后，应重新计算选择状态，必要时回到 Under Review。旧选择历史保留在 Selection Snapshot。

## 8. Candidate Selection 与 Selection Snapshot

### 8.1 选择层级

用户可以：

1. 接受或拒绝整个 Candidate Set。
2. 接受或拒绝单个 Object Candidate。
3. 在同一 Object 的多个候选版本之间选择。
4. 跨 Candidate Set 组合不同对象。

同一个 Object ID 在最终选择结果中只能有一个生效候选版本。系统提供基础差异比较，包括几何、属性、来源、版本和验证差异；V1.0 不自动融合几何。

### 8.2 Selection Snapshot

用户完成选择后创建不可变 Accepted Selection Snapshot，至少保存：

- `selection_id`
- `task_id`
- `candidate_set_refs`
- `selected_object_candidates`
- `rejected_object_candidates`
- `source_candidate_versions`
- `created_at`

Selection Snapshot 不可修改。重新选择必须创建新快照。Change Set 必须引用精确 Selection Snapshot。

Accepted 不等于 Applied。用户可以接受候选结果但拒绝或暂缓正式应用；此时候选和 Selection Snapshot 保留，正式地图不变。

## 9. Change Set 与正式应用

### 9.1 Change Set 模型

Change Set 至少保存：

- `change_set_id`
- `source_task_id`
- `selection_snapshot_id`
- `target_branch_id`
- `base_branch_revision`
- `version`
- `status`
- `created_at`
- `applied_at`

Change Item 至少保存：

- `change_item_id`
- `change_set_id`
- `operation_type`
- `object_id`
- `source_candidate_id`（适用时）
- `source_version`（适用时）
- `target_object_version`
- `status`

V1.0 操作类型：`Create`、`Update`、`Delete`、`Replace Geometry`。

Delete 直接作为 Change Item 表达，不需要创建空 Object Candidate。删除前检查其他对象依赖，由 Domain Services 执行最终领域约束。Replace Geometry 只改变几何，不改变 Object Identity；身份拆分/合并属于 Object Operations。

### 9.2 正式应用前检查

1. Selection Check
2. Identity Check
3. Version Check
4. Domain Check
5. Change Set Check

Validation Report 不是授权凭证。正式应用必须重新检查必要领域规则和版本。

### 9.3 人类授权

正式应用必须获得明确的人类授权。授权记录至少绑定：

- Change Set ID
- Change Set Version
- 变更内容摘要或稳定哈希
- Target Branch
- 授权时间和授权来源

Change Set 内容、版本或目标版本发生变化时，原授权自动失效，必须重新检查并授权。不能在用户授权后悄悄增加、删除或改变 Change Item。

### 9.4 Atomic Apply

正式应用全部成功或全部回滚，不支持部分提交。正式对象状态变更与 Change Set 的 `Applied` 状态必须在同一个数据库事务中提交。

Change Set 状态：`Draft`、`Validating`、`Ready`、`Applying`、`Applied`、`Failed`、`Stale`、`Cancelled`。

进入 Applying 后，不允许用户修改内容或中途取消事务。程序崩溃后先核验数据库事务最终结果，不盲目重放。

### 9.5 版本冲突

Change Set 必须记录 Target Branch、Base Branch Revision、Object ID、Expected Object Version，以及必要的继承来源/关系版本。

版本检查基于目标 Branch 中对象的有效状态，而不是简单使用全局最新 Object Version。只检查相关对象和依赖；无关对象变化不应自动阻断应用。

若目标对象版本已变化，Change Set 标记为 Stale。AI 可分析差异并提出更新方案；若需要改变原任务目标、范围或基线，则请求用户决定。更新后的 Change Set 必须重新验证并重新授权。

### 9.6 正式应用后的撤销

正式应用成功后，不得通过原 Task Undo 撤销。用户要求撤销时，创建新的 Formal Reversal Change Set：引用原 Application，基于原应用前后版本生成反向变更，检查当前版本和依赖，重新验证并取得用户授权，再通过新的 Atomic Apply 执行。

如果对象已被后续操作修改，不得直接覆盖恢复旧快照，必须先分析冲突。

## 10. Branch 继承与 Object State

Object Identity 是跨 Branch 稳定身份。Branch Object State 记录对象在某个 Branch 历史位置的有效状态。

子 Branch 从祖先 Branch 的分叉点继承当时有效的对象状态，不重新分配 Object ID，也不复制祖先 Command 本身。子 Branch 后续修改不影响祖先 Branch；祖先 Branch 在分叉点之后新增或修改的内容，不自动改变已经建立的子 Branch 历史。

若未来需要将祖先后续变化引入子 Branch，应作为独立的继承更新或合并工作处理，检查版本和语义冲突。

Change Set 应明确目标 Branch 和该 Branch 中对象的有效版本。Create 创建新身份及目标 Branch 状态；Update 保持身份不变并创建新 Branch 状态版本；Delete 只结束目标 Branch 中的有效存在，不全局销毁身份；Replace Geometry 保持身份不变并创建新几何状态版本。

## 11. Task Undo、失败与取消

### 11.1 Task 级整体 Undo

Task 前面多个 Step 成功、后续 Step 失败时，Task 的全部可回滚修改整体撤销，而不是只撤销最后一个 Step。

- Undo 是确定性程序机制，不由 AI 临时决定。
- Task 失败且 Undo 成功：`Failed`。
- 用户取消且 Undo 成功：`Cancelled`。
- Undo 失败或状态不确定：`Recovery Required`。
- Task Log、Tool Call、操作审计和 Object ID Reservation 不随候选数据回滚删除。
- 已成功提交的正式 Change Set 不属于 Task Undo 范围。
- 外部不可逆副作用不承诺由本机 Undo 恢复。

### 11.2 回滚流程

1. Task 进入 `Rolling Back`。
2. 禁止该 Task 新写入。
3. 确保当前工具不再产生新副作用。
4. 按操作依赖逆序读取 Task Operation Record。
5. 逐项执行可重入 Undo。
6. 每次 Undo 的数据修改与 Undo 状态记录在同一个短数据库事务中提交。
7. 全部完成后更新 Task 为 Failed 或 Cancelled。
8. 任何步骤无法确认或失败，进入 Recovery Required。

不把整个 Task Undo 包在一个超大数据库事务中。

### 11.3 取消执行中的 Tool Call

用户取消时：停止启动新 Step → 请求当前 Tool Call 取消 → 等待其确认不再产生写入 → 核验 operation_id 和实际副作用 → 执行整体 Undo。不能在工具仍可能写入时并行 Undo。

### 11.4 部分结果

Task 未完成失败或取消时，不得因为部分候选结果看起来有价值而擅自保留为交付成果。只有成功完成 Task Completion Commit，候选成果才能成为持久交付成果。

## 12. Human Intervention 与恢复决策

### 12.1 恢复层级

1. **自动修复**：问题明确、处理确定，不改变目标。
2. **重新规划**：在原目标、授权和验收范围内更换方法。
3. **请求用户介入**：意图缺失、重要语义判断、超授权、基线变化或需要外部输入。
4. **无法继续**：没有符合原要求的安全可行路径，进入失败或恢复所需状态。

Task Coordinator 统一协调，不增加独立 Recovery Agent。

### 12.2 Human Intervention Request

作为 Task 子记录，至少保存：

- `request_id`、`task_id`
- `request_type`、`status`
- `title`、`description`、`reason`、`evidence`
- `options`、`recommended_option`
- `required_decision`、`response`
- `created_at`、`resolved_at`

类型：`Clarification`、`Decision`、`Authorization`、`Baseline Change`、`External Input`。

状态：`Open`、`Resolved`、`Withdrawn`。

一条请求原则上只代表一个待处理决策。用户拒绝推荐方案不等于取消 Task；AI 应判断是否存在其他合规路径。用户回答后继续原 Task，按影响更新 Plan Version 并重做受影响步骤。

### 12.3 恢复检查

恢复必须以持久化记录和实际数据为准，不信任内存执行进度。检查：Task、Plan Version、未完成 Step、Interrupted Tool Call、operation_id、Object Draft、ID Reservation、Object Binding、候选数据、Validation Report、Human Intervention Request、Change Set 和资产引用。

- 已完成且输入未变的 Step 保留。
- 已完成但依赖变化的 Step 重新评估。
- 已成功 Tool Call 复用结果。
- 未知结果先核验。
- 已预留 Object ID 继续使用，不能重新分配。
- 已绑定 Object Identity 不重新分配。
- 已插入候选数据先检查是否存在。
- 过期 Validation Report 重新验证。
- 已解决人工请求作为明确输入。
- 旧候选版本保留并标记 Superseded；新结果创建新版本。
- 恢复期间禁止相关 Task 新写入。

## 13. 数据持久化与技术基线

### 13.1 三层存储

1. **本地关系数据库**：Task、执行记录、身份、候选、验证、选择、Change Set、Branch 状态和审计，是状态权威。
2. **本地资产存储**：大型几何数据、图片、导入文件和生成文件，保存资产本体。
3. **派生数据与缓存**：缩略图、预览缓存和可重建索引；丢失后可重建，不作为正式数据权威。

原则：数据库决定数据是否存在、属于谁、处于什么状态；资产存储保存数据本身。

### 13.2 数据库推荐

V1.0 推荐 SQLite 作为嵌入式关系数据库，不需要独立数据库服务器。启用外键约束，使用明确的事务和持久化配置。WAL、同步级别及并发配置应在目标操作系统和文件系统上测试后确定。

SQLite 负责事务性元数据和关系状态，不等同于地图几何计算引擎。空间数据格式、空间索引和大型几何资产组织仍需结合地图引擎单独评估。

### 13.3 核心逻辑表清单

**Task Execution**：`tasks`、`task_plan_versions`、`task_steps`、`task_logs`、`tool_calls`、`task_operations`、`human_intervention_requests`、`task_delivery_records`。

**Object Identity**：`object_id_reservations`、`object_identities`、`object_bindings`。

**Candidate Management**：`candidate_sets`、`object_candidates`、`candidate_edit_records`。

**Validation**：`validation_reports`、`validation_issues`。

**Selection**：`selection_snapshots`、`selection_items`。

**Change Set**：`change_sets`、`change_items`、`change_authorizations`、`application_reports`。

**Asset Management**：`assets`、`asset_references`、`asset_staging_records`。

以上为逻辑表清单，不代表需要多个数据库或服务。具体字段和索引可在数据库详细设计阶段展开。

### 13.4 必要数据库约束

- Object ID Reservation 的 Object ID 唯一且不可复用。
- Object Identity 的 Object ID 唯一。
- Tool Call ID 唯一。
- operation_id 唯一。
- Candidate ID 唯一；同一逻辑对象的候选版本号唯一。
- Selection Snapshot ID 唯一。
- Change Set 的版本可被精确引用。
- 授权记录绑定精确 Change Set 版本和内容摘要。
- 同一 Branch 同一时间最多一个正式写入事务。
- 候选版本不可变。
- 正式 Branch 状态必须保留 Branch 归属，不得简化成全局单一当前版本。

### 13.5 必须原子提交的事务

1. Object ID Reservation 及其预留记录。
2. 候选对象元数据、版本引用、operation 记录和相应调用结果（在可行范围内）。
3. Task Completion Commit：完成条件检查、候选交付固化、Task Delivery Record 和 Task Completed。
4. Selection Snapshot 及其所有 Selection Items。
5. Change Set Authorization 及其精确版本/内容摘要绑定。
6. Atomic Apply：正式地图数据、Change Set Applied 状态和 Application Report。

Task Undo 使用多个短事务逐项提交，不使用一个超大事务。

### 13.6 Task Completion Commit

Task 完成不是单独更新状态字段。必须在一个本地数据库事务中：检查必要 Step、检查候选交付条件、固化 Candidate Set 交付状态、保存 Task Delivery Record、更新 Task 为 Completed。全部成功才提交；否则 Task 仍未完成，按恢复规则处理。

### 13.7 文件资产协调

普通文件系统与数据库不能天然组成一个原子事务。采用 Staged Asset + Database Commit：

1. 新文件先写入临时区。
2. 校验文件完整性并计算内容哈希。
3. 通过确定性步骤写入内容寻址的正式资产区。
4. 确认正式资产可读后，再提交数据库中的 Asset、业务引用和操作记录。
5. 数据库事务失败时，未被引用的正式资产可成为孤儿文件，由恢复流程检查并清理。
6. 临时文件按明确状态清理。

资产清理必须先检查数据库引用，不能在数据库提交前删除可能被有效记录引用的资产。内容哈希用于完整性检查和去重。

## 14. 单机并发与写入协调

- 不同 Task 的独立候选生成可以并行。
- 对不同数据版本的读取和验证可以并行，但必须绑定明确数据快照。
- 同一 Branch 同一时间只允许一个正式地图写入事务。
- 不同 Branch 在技术允许时可独立写入。
- 候选版本不可变；编辑创建新版本。
- 当前编辑指针只能受控更新；基础版本变化时重新比较，不静默覆盖。
- 使用进程内 Branch Write Coordinator 排队，并以数据库事务和唯一约束作为最终保护。
- 正式应用开始时再次检查 Branch Revision 和相关对象版本。
- 不因无关对象变化而阻断 Change Set。

## 15. Application Recovery Coordinator

V1.0 使用统一的 Application Recovery Coordinator，不为 Task、Candidate、Change Set 分别建设互不相干的恢复引擎。

启动恢复检查顺序：

1. 检查数据库完整性。
2. 检查资产存储和资产引用。
3. 查找未完成 Task。
4. 检查 Interrupted Tool Calls。
5. 核验 operation_id 和实际写入。
6. 检查 Task Undo 状态。
7. 检查未完成 Change Set。
8. 核验正式事务最终提交结果。
9. 检查孤儿资产和临时资产。
10. 对确定状态执行安全恢复；不确定状态进入 Recovery Required。

恢复原则：

- 数据库持久化状态优先于内存状态。
- 不确定是否写入成功时先核验，不直接重放。
- 恢复期间阻止受影响对象或 Branch 的危险写入。
- 确定性恢复由程序执行；需要语义判断时才交给 AI 或用户。
- 不要求所有 Task 恢复完毕后才能打开应用。
- 可采用局部隔离：状态确定的功能正常开放；不确定的 Task 被锁定；受影响 Branch 暂停正式写入；不相关 Branch 和只读功能仍可使用。
- 数据库自身完整性检查失败时进入更严格的安全模式。

## 16. 状态机关键约束汇总

### Task

- 正常路径：Created → Planning → Ready → Running → Completed。
- 执行中可进入 Waiting for User、Paused。
- 失败或取消先进入 Rolling Back。
- Undo 成功后进入 Failed 或 Cancelled。
- 状态不确定或 Undo 失败进入 Recovery Required。

### Tool Call

Created → Running → Succeeded / Failed / TimedOut / Interrupted。
Interrupted 必须先核验实际副作用。

### Candidate Set

Generating → Validating → Ready → Under Review → Accepted / Partially Accepted / Rejected；被新方案替代时 Superseded。候选修改可能使其回到 Under Review。

### Change Set

Draft → Validating → Ready → Applying → Applied；异常可进入 Failed、Stale 或 Cancelled。Applying 期间不可修改内容或中途取消事务。

## 17. V1.0 端到端故障验收场景

实现必须通过以下场景验证。每个场景都必须明确保存、回滚、恢复和人工介入边界。

| 场景 | 必须保证的结果 |
|---|---|
| AI 生成中途崩溃 | 核验 Tool Call 和 operation_id；候选数据整体回滚；已预留 ID 永久保留；正式地图不变 |
| 候选生成完成、Task 完成事务中崩溃 | Task Completion Commit 要么全部提交，要么全部回滚；不能出现半交付 |
| 用户修改已接受候选 | 创建新 Candidate Version；重新验证、重新接受；旧 Selection Snapshot 不变 |
| 正式应用事务中崩溃 | 正式数据和 Applied 状态同事务；提交成功则全部生效，未提交则全部回滚 |
| 两个 Task 操作同一 Branch | 候选可并行；正式写入串行；版本冲突按相关对象检查 |
| 子 Branch 修改继承对象 | Object Identity 继承；目标 Branch 状态独立；按目标 Branch 有效版本检查 |
| 用户取消时 Tool Call 正在执行 | 先停止新写入并确认工具停止，再整体 Undo |
| Task 部分生成后失败 | 不交付部分结果；整体回滚候选副作用；保留日志和 ID Reservation |
| 用户接受候选但拒绝正式应用 | 保留 Candidate 和 Selection Snapshot；正式地图不变 |
| 正式应用前目标对象被修改 | Change Set Stale；重新检查、必要时重规划并重新授权 |
| Change Set 授权后内容变化 | 原授权失效；新版本必须重新授权 |
| 正式应用成功后要求撤销 | 创建新的反向 Change Set；检查当前版本并重新授权，不执行原 Task Undo |

## 18. V1.0 实施优先级

### 必须实现

- Task、Step、Plan Version、Task Log、Tool Call。
- Task Execution Contract 和人工介入机制。
- Tool Registry、统一 Executor、Tool Contract 和 operation_id 幂等机制。
- Task 级整体 Undo 和 Recovery Required。
- Object ID Reservation 永久消耗及 Object Identity 分离。
- Candidate Set、多对象候选版本和 Validation Report。
- 用户选择、拒绝、跨方案组合和 Selection Snapshot。
- Change Set、精确版本授权和 Atomic Apply。
- Branch-scoped Object State 和版本冲突检查。
- Task Completion Commit 和 Task Delivery Record。
- 本地持久化、程序崩溃恢复和资产引用核验。

### 暂不实现

- DAG 工作流引擎。
- 通用 Recovery Agent 或独立工作流服务。
- 分布式锁、消息队列和分布式事务。
- 自动几何融合和复杂候选分支合并。
- 多人编辑和多用户冲突解决。
- AI 自主正式应用。
- 通用事件溯源。
- 多个独立 Change Set 的自动批量事务。

## 19. 后续单独技术评估项

以下内容不阻碍当前引擎架构成立，但需要在相关实现阶段单独评估：

1. SQLite 的目标平台事务、WAL、同步和备份配置。
2. 地图几何数据、空间索引和大型资产的具体存储格式。
3. 地图引擎与 Domain Services 的事务集成方式。
4. Geometry Validation、CRS 和精度处理实现。
5. LLM Provider 接口、结构化输出和 Tool Calling 协议。
6. 大型资产的导入、校验、备份和孤儿清理实现。

## 20. 最终架构原则

World View AI Assistant 可以自主规划和执行，但不能直接成为正式地图数据的写入者。Task Engine 管理工作，Candidate Management 管理候选，Validator 检查结果，Selection Snapshot 固化用户选择，Change Set 管理正式变更，Domain Services 保证正式地图规则。

Task 失败时整体回滚候选工作副作用；Task 完成时原子交付候选成果；正式应用时通过独立、经过授权的 Atomic Apply 提交；正式应用后的撤销通过新的反向 Change Set 完成。

整个 V1.0 以本地事务数据库、不可变版本记录、稳定 operation_id、明确的写入边界和统一恢复协调器实现可靠性，不依赖分布式基础设施或通用工作流平台。