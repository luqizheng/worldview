# TD-02 — Task Planning & Execution Engine V1.0 技术设计提案

- **状态**：Proposed / 待人工确认
- **版本**：V0.1（2026-11-20）
- **上游**：sprint 任务书"Task Planning & Execution Engine V1.0"约束、ADR-004（仅批量修复 Undo/Redo，本引擎不与其重叠）、Baseline（单机 / 无分布式）
- **范围**：任务数据模型、生命周期状态机、四个组件职责、顺序调度、暂停/取消/失败/恢复、执行记录。**不含**具体任务类型的业务逻辑（生成/验证/应用在 TD-03 定义）。

---

# 1. 架构定位

```text
用户 / AI Assistant
      │  意图
      ▼
Task Planner        ── 规划：意图 → 任务图（DAG，第一版为顺序链）
      ▼
Task Coordinator    ── 调度：依赖就绪的任务按序派发（单执行线程，顺序执行）
      ▼
Task Executor       ── 执行：按任务类型调用注册的 Handler（Handler 调应用服务层）
      ▼
Result Validator    ── 校验：结果结构与成功判据；不合格判失败
      ▼
Task Store          ── 记录：任务 + 执行事件日志（持久化，供恢复与审计）
```

约束（已确认）：单机、单执行线程顺序执行、无分布式队列 / Worker 集群 / Multi-Agent Runtime。

# 2. 任务数据模型

```text
Task:
  task_id          : TaskId
  type             : str                    # 注册制：如 "generation.run"、"changeset.apply"
  payload          : JSON                   # 该类型的输入参数（模式校验）
  status           : pending | planned | queued | running
                     | paused | succeeded | failed | canceled | interrupted
  depends_on       : [TaskId]               # 第一版：顺序链
  goal_id          : str?                   # 一次规划产出的任务组标识
  attempts         : int
  max_attempts     : int = 1                # 第一版不自动重试
  result           : JSON?
  error            : { code, message, detail }?
  created_at / started_at / finished_at    # 认知时间，仅记录
  execution_log    : [TaskEvent]            # 追加式
TaskEvent:
  seq, at, kind (status_change | note | progress), from?, to?, message?
```

# 3. 生命周期状态机

```text
pending ──plan──▶ planned ──enqueue──▶ queued ──dispatch──▶ running ──▶ succeeded
   │                 │                  │                  │   └─validate_fail─▶ failed
   │                 │                  │                  ├──pause_request───▶ paused ──resume──▶ queued
   │                 │                  │                  ├──cancel_request──▶ canceled
   │                 │                  │                  └──crash(进程中断)─▶ interrupted ──recover──▶ queued
   └──cancel──▶ canceled        (failed/canceled 为终态；依赖它的任务置 skipped)
```

规则：

1. 所有转换必须经 Coordinator 并记录 TaskEvent；非法转换拒绝（InvalidTransitionError）。
2. `skipped` 不单设状态：依赖失败/取消的任务保持 `pending` 并记录跳过原因事件，其自身状态不再变化。
3. `interrupted` 仅由启动恢复流程写入（进程崩溃/强杀后 `running` 的任务），不与 `failed` 混用——区分"业务失败"与"系统中断"。

# 4. 组件职责与边界

## 4.1 Task Planner

- 输入：意图描述（结构化 request：goal_type + 参数）。
- 输出：任务图（第一版为线性链），每个任务带 `depends_on`。
- 规划是**确定性规则**：按 `goal_type` 查注册的规划模板（如"生成会话"模板 = run_generation → validate_candidate → 提交候选）。
- Planner 不执行、不知道 Handler 细节；模板由各业务模块注册（依赖倒置）。

## 4.2 Task Coordinator

- 持有唯一执行线程；从就绪队列按 `(goal_id, 创建序)` 取任务派发给 Executor。
- 顺序执行语义：同一时刻至多一个 `running`。
- 提供 `pause(task_id)` / `resume(task_id)` / `cancel(task_id)` 请求入口：写入协作式控制标记，真正转换由执行线程在检查点完成。
- 取消排队中任务 = 直接置 `canceled`；取消运行中任务 = 置协作标记，由 Handler 在检查点响应。

## 4.3 Task Executor

- Handler 注册表：`type → handler(payload, context, control) -> result`。
- Handler 契约：
  1. **纯编排**：只调用应用服务层，不得绕过 Domain Services 直接改库（架构约束）；
  2. **可中断**：在自然步骤边界调用 `control.check_point()`，响应 pause/cancel；
  3. **确定性输入输出**：同一 payload + 同一世界数据 → 同一 result。
- 异常分类：`DomainError` → 任务 `failed`（error.code 保留）；其他异常 → `failed`（code=internal_error，完整堆栈入执行日志）。

## 4.4 Result Validator

- 按任务类型注册的**结果判据**：必需字段、引用存在性、状态合法。
- 判据不合格 → `failed`（error.code=invalid_result）；合格 → `succeeded` 并固化 result。
- Validator 不做业务验证（几何/语义验证是任务类型本身，见 TD-03）。

# 5. 失败、恢复与持久化

1. **执行记录持久化**：Task 与 TaskEvent 即时落库（TD-01 迁移 `0002_tasks.sql`）。
2. **启动恢复**：进程启动时扫描 `running` 任务 → 置 `interrupted` → 由用户/调用方决定 `recover`（重新排队）或放弃；`interrupted` 任务不自动重跑（单机创作场景，防止意外重复副作用）。
3. **不引入**复杂恢复日志 / WAL 级自定义机制（D01-138~142 方向一致：轻量持久化 + 状态标记足够）。
4. **副作用幂等**：Handler 对可重复执行的操作必须幂等或带防重键（如 Change Set apply 以 changeset_id 防重）。

# 6. 与 ADR-004 的边界

- 本引擎的 pause/cancel 是**任务执行层**机制，不是 Undo/Redo。
- ADR-004 的批量修复 Undo/Redo 未来若实现，是**业务操作层**规则（栈顶、原子、整体拒绝），与本引擎状态机互不引用；两者不得互相覆盖。

# 7. 待确认问题（实现前需裁决）

| # | 问题 | 本提案缺省 |
| --- | --- | --- |
| 1 | 任务记录保留期限 | 永久保留（审计与恢复需要） |
| 2 | 恢复时 interrupted 任务的处理交互 | 不自动重跑；显式 recover 或放弃 |
| 3 | 同一 goal 链中部分失败后能否补跑剩余 | 第一版：整链不自动补跑，重新规划 |
| 4 | 执行线程与 UI 的进度通知机制 | TaskEvent 轮询（第一版）；事件推送留后 |
