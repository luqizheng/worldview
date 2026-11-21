# TD-03 — 生成 → 候选 → 验证 → 变更集管线技术设计提案

- **状态**：Proposed / 待人工确认
- **版本**：V0.1（2026-11-20）
- **上游**：sprint 任务书 Phase 2~5、Baseline"AI Agent 不得直接修改数据库 / 必须经 Candidate Set 与 Change Set"、ADR-003（命令驱动播放）、P2-01（StateChange 模型）
- **范围**：Generation Spec / Session / Task / Candidate 数据模型与生命周期、验证体系、Change Set 语义（Diff / Apply / 基准检查 / 冲突检测 / 正式历史落档）、应用服务层与 HTTP API + MCP 形态。**不含** AI 生成算法本身（生成内容由 AI Assistant 经 MCP/API 提交，本系统只管管线）。

---

# 1. 总管线（架构门禁）

```text
Generation Spec（意图/约束）
   └─▶ Generation Session（一次生成会话）
          └─▶ Generation Tasks（任务引擎执行，TD-02）
                 └─▶ Candidate（候选内容，只能进 Candidate Set）
                        └─▶ Validation（几何/拓扑/语义）
                               └─▶ Change Set（草稿 → 校验通过）
                                      └─▶ Apply（单事务原子落库 = 正式 State Change）
```

**铁律**：从 Candidate 到正式数据的唯一通路是
`Candidate → Validation 通过 → Change Set → Apply（经 Domain Services）`。
AI 输出永远不能跳过任何一道门（Baseline 约束）。

# 2. 数据模型

```text
GenerationSpec:
  spec_id, name, description,
  target_map_id, target_history_id,        # 生成内容的目标历史（Branch 须先选定，ADR-003 A10-08）
  constraints: JSON,                       # 主题/风格/范围等约束，管线不解释，透传给生成方
  created_at

GenerationSession:
  session_id, spec_id,
  status: draft | running | completed | failed | canceled | superseded,
  task_ids: [TaskId],                      # 关联任务引擎
  created_at / finished_at

Candidate:
  candidate_id, session_id,
  kind: object_create | property_set | geometry_replace | object_destroy,   # 与 ChangeType 一一对应
  target: ObjectId,
  effective_time: (calendar, ordinal),
  payload: JSON,
  status: pending | validated(passed|warnings) | rejected | submitted | withdrawn,
  validation_result_id?, changeset_id?,    # 提交后回填
  submitted_at?

ChangeSet:
  changeset_id,
  target_history_id,
  base: { history_fingerprint, at_time },  # 基准：创建时的历史状态指纹（§5）
  status: draft | validated | applied | rejected | superseded,
  item_ids: [candidate_id],
  proposed_changes: [StateChange 草稿],    # 由候选合成，Apply 前只读
  validation_result_id?,
  applied_at?, applied_change_ids?: [change_id]   # 落档回填（溯源，非版本实体）

ValidationResult:
  result_id, target_type (candidate|changeset), target_id,
  overall: passed | passed_with_warnings | failed,
  checks: [ { check_id, name, level(error|warning), passed, message, detail } ],
  ran_at, tool_version                     # 校验器版本，保证结果可解释
```

迁移编号沿用 TD-01：`0003_generation.sql`、`0004_changeset.sql`。

# 3. 生命周期与状态机

## 3.1 Session

```text
draft ──start──▶ running ──所有任务终结──▶ completed | failed
running ──cancel──▶ canceled
任一时刻基准历史变化（他人编辑/应用了其他变更集）──▶ superseded（会话产出候选需重新校验才能提交）
```

## 3.2 Candidate

```text
pending ──run_validation──▶ validated(passed|passed_with_warnings) | rejected
validated ──submit_to_changeset──▶ submitted（回填 changeset_id）
pending/validated ──withdraw──▶ withdrawn（终态，留痕）
rejected ──编辑内容后重新校验──▶ pending
```

- ** rejected 只因校验**；"AI 内容质量"不做系统级判定（避免发明产品规则）。
- 候选创建时做**结构预检**（payload 模式、时间窗）——同一套写入级规则（P2-01 §4.3），尽早失败。

## 3.3 ChangeSet

```text
draft ──add_candidates(均须 validated 通过)──▶ draft
draft ──run_validation(整体校验)──▶ validated | rejected（打回 draft）
validated ──apply──▶ applied（终态）| rejected（基准变化/冲突时整体拒绝）
draft/validated ──supersede──▶ superseded
```

# 4. 验证体系（sprint Phase 3）

| 层 | 内容 | 失败级别 |
| --- | --- | --- |
| Geometry | 结构合法性：环闭合、≥3 个不重复点、线 ≥2 点、坐标为有限数、多边形自相交（第一版 O(n²) 线段相交检测）、bbox 合理性 | error |
| Topology | 第一版最小集：几何类型与对象类型相容（boundary 须 LineString/Polygon 等）；共享边界的复杂拓扑约束**不在本版**（几何基线 §11 留待专项） | error |
| Semantic | payload 模式符合 ChangeType 注册表；时间窗（epoch / branch `> parent_at`）；日历匹配；目标对象存在性检查 | 时间窗/模式 = error；存在性 = **warning**（ADR-003 A10-09：可写入、可执行、不复活、持续警告） |
| Candidate | 结构预检 + 上述三层按候选内容执行 | 汇总为 ValidationResult |
| ChangeSet 整体 | 条目间一致性：同对象同时刻顺序、目标互斥、基准指纹一致 | error |

规则：`passed_with_warnings` 允许继续提交与应用（对齐 A10-06"警告不阻止"）；任何 `error` 阻断该级流转。

# 5. Change Set 语义（sprint Phase 4）

## 5.1 基准版本检查（Branch 基准）

- 创建 Change Set 时记录 `base = (history_fingerprint, at_time)`。
- 指纹 = 该 History 当前全部变更的确定性哈希（排序后内容哈希；**不是版本号**，不构成版本实体）。
- Apply 时重算指纹：不一致（基准后历史被修改）→ **整体拒绝**（BaseVersionMismatch），要求重建变更集。这是"版本检查"语义的最小实现，且不引入 Version 表。

## 5.2 冲突检测

第一版定义（同图单用户下的"陈旧写"防护）：

1. **基准漂移**：§5.1 指纹不一致 → 拒绝；
2. **条目间冲突**：同 ChangeSet 内同对象同 `(time)` 两条同类写且顺序未定 → 校验失败打回；
3. **跨变更集并发**：Apply 持有目标 History 的写锁（单进程互斥），串行化 Apply；Apply 前再次校验基准指纹。

不引入 Merge/Conflict Resolution 语义（ADR-001 §16 禁令不变；此处"冲突"仅指陈旧写拒绝）。

## 5.3 Apply（正式落档）

1. 前置：status=validated、基准指纹一致、无条目冲突；
2. 合成 `proposed_changes` → 逐条过 Domain Services 写入级校验（P2-01 §4.3）；
3. **单事务**全部写入 `state_changes`（携带 `provenance.changeset_id`）；任一失败整体回滚；
4. 回填 `applied_at / applied_change_ids`，候选置 submitted、变更集置 applied；
5. Apply 不产生任何新领域实体（无 WorldVersion）；正式历史 = 新增的 State Change 记录。

# 6. 应用服务层与 API/MCP（sprint Phase 5）

## 6.1 应用服务层（唯一入口）

HTTP 与 MCP 共用同一组服务对象，禁止各自直连仓储：

```text
ApplicationServices:
  MapService        create_map / create_branch / set_branch_status / delete_branch / copy_object
  QueryService      get_world_state(history, t) / get_object_state(...) / list_maps / list_branches
  HistoryService    append_event / append_state_change(仅限系统内部与用户直改，AI 不可用) / list_changes / list_events
  GenerationService spec/session/candidate 的创建、查询、状态流转
  ValidationService run_candidate_validation / run_changeset_validation / get_result
  ChangeSetService  create / add_candidates / validate / diff / apply
```

- `HistoryService.append_state_change` 面向**用户手工编辑**与 Change Set Apply 内部调用；
- **AI/MCP 面不暴露**直接 append 通道——AI 侧工具只到 Candidate 为止（门禁由服务层物理保证）。

## 6.2 HTTP API 形态（localhost, JSON）

```text
GET/POST   /maps                    GET /maps/{id}
GET        /maps/{id}/world-state?history_id=&at=     （history_id 缺省=根历史）
GET/POST   /maps/{id}/branches      POST /branches/{id}/status   DELETE /branches/{id}
GET        /histories/{id}/changes  GET /histories/{id}/events
GET/POST   /generation/specs        GET/POST /generation/sessions   POST /sessions/{id}/start
GET/POST   /candidates              POST /candidates/{id}/validate   POST /candidates/{id}/submit
GET/POST   /validation-results
GET/POST   /changesets              POST /changesets/{id}/validate
POST       /changesets/{id}/diff    POST /changesets/{id}/apply
GET/POST   /tasks                   POST /tasks/{id}/pause|resume|cancel
```

- 只读查询 GET；所有写操作经应用服务层校验并返回结构化错误（code/message）。
- 无认证（单机 localhost，架构约束）。

## 6.3 MCP 工具面

| 工具 | 说明 |
| --- | --- |
| list_maps / get_world_state / get_object_state | 读 |
| create_generation_spec / start_session / submit_candidate | AI 生成内容唯一入口（止于 Candidate） |
| validate_candidate / get_validation_result | 校验 |
| create_changeset / add_candidates_to_changeset / validate_changeset / diff_changeset | 变更集准备 |
| apply_changeset | 仅在用户显式批准后由用户侧调用；AI Assistant 不得自行动手（产品约束，实现上不做强制，登记为使用规约） |
| list_tasks / pause_task / resume_task / cancel_task | 任务观测与控制 |

- MCP 走 stdio JSON-RPC；工具实现 = 应用服务层薄封装。
- `apply_changeset` 对 AI 暴露与否是**产品决策**：缺省在 MCP 面提供该工具但文档明示"须用户批准"；如需硬门禁（MCP 面不提供 apply），待用户裁决。

# 7. 待确认问题（实现前需裁决）

| # | 问题 | 本提案缺省 |
| --- | --- | --- |
| 1 | AI 是否可经 MCP 直接 apply_changeset | 工具存在但规约要求用户批准；硬禁止需另裁 |
| 2 | 候选被拒绝后 AI 是否自动重生成 | 不自动；由会话外循环决定 |
| 3 | Change Set 指纹算法 | 排序后内容 SHA-256（确定性） |
| 4 | validated 候选的有效期（基准漂移后是否需重新校验） | 提交进 ChangeSet 时强制重新整体校验 |
| 5 | 一个 ChangeSet 能否跨多个 History | 第一版：单目标 History（原子性简单） |
