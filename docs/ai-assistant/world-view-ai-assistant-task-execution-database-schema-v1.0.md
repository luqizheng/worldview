# World View AI Assistant — Task Execution Database Schema V1.0

- Document: Task Execution database schema
- Version: 1.0
- Status: Proposed implementation baseline
- Storage: SQLite, single-user, local-first desktop application
- Scope: Task Execution table group only

## 1. Purpose and boundaries

This document defines the database schema for the AI Assistant Task Execution domain. It covers task records, immutable execution contracts and plans, steps, tool calls, side-effect/undo records, logs, human intervention requests, and delivery snapshots.

The schema belongs to the AI Assistant domain. It does not redefine World View Core Domain tables such as Object Identity, Branch, Branch Object State, Geometry, Object Relationships, or Map History. Before implementation, the actual Core Domain schema must be inspected and its real identifiers and foreign-key targets mapped. Do not invent replacement Core Domain tables.

The AI Assistant and World View Core Domain may use the same SQLite database, but formal map data remains owned by World View Core Domain and may only be modified through Domain Services and Change Sets.

## 2. Global database conventions

| Concern | Convention |
|---|---|
| Primary IDs | UUID stored as canonical lowercase TEXT with hyphens |
| Time | UTC Unix epoch milliseconds stored as INTEGER |
| Boolean | INTEGER constrained to 0 or 1 |
| Status | TEXT with CHECK constraints; transitions enforced by application state machines |
| JSON | TEXT with `json_valid()` checks; use only for variable structured payloads, not core relationships |
| Large data | Local Asset Store; database stores stable references |
| Foreign keys | Enabled on every SQLite connection (`PRAGMA foreign_keys = ON`) |
| Deletion | No physical deletion of important task history; use lifecycle/retention policies instead |
| Transactions | Short SQLite transactions around each atomic state transition or operation record update |

The deployed SQLite build must support JSON functions. `STRICT` tables may be enabled after confirming the selected SQLite provider and ORM/driver support; it is not a prerequisite for this logical schema. All DDL below uses ordinary tables plus explicit constraints for broad compatibility.

## 3. Entity overview

- `tasks`: task identity, current lifecycle state, and pointers to current contract/plan versions.
- `task_contract_versions`: immutable snapshots of the execution boundary. Added to preserve the original and any explicitly user-approved changes without silently overwriting the contract.
- `task_plan_versions`: immutable plan history.
- `task_steps`: steps belonging to one exact plan version.
- `tool_calls`: individual tool invocation records.
- `task_operations`: durable side-effect and idempotency records; the authoritative basis for Task-level Undo.
- `task_logs`: diagnostic and audit trail, not authoritative state.
- `human_intervention_requests`: one outstanding decision per request.
- `task_delivery_records`: immutable record of exactly what a completed Task delivered.

## 4. Schema

### 4.1 `tasks`

Purpose: one record for each user request that enters the Task execution flow.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `task_id` | TEXT | No | Primary key |
| `title` | TEXT | No | Non-empty display title |
| `user_request` | TEXT | No | Original user request; do not replace with an AI paraphrase |
| `intent_type` | TEXT | No | `general_conversation`, `information_request`, `map_operation`, `complex_map_creation` |
| `status` | TEXT | No | `created`, `planning`, `ready`, `running`, `waiting_for_user`, `paused`, `rolling_back`, `recovery_required`, `completed`, `failed`, `cancelled` |
| `complexity` | TEXT | No | `simple`, `moderate`, `complex` |
| `current_contract_version_number` | INTEGER | No | Current execution contract version number for this Task |
| `current_plan_version_number` | INTEGER | Yes | Current plan version number; NULL before a plan exists |
| `created_at` | INTEGER | No | Creation time |
| `started_at` | INTEGER | Yes | First execution start time |
| `finished_at` | INTEGER | Yes | Terminal completion/failure/cancellation time |
| `updated_at` | INTEGER | No | Last update time |
| `error_summary` | TEXT | Yes | Short terminal or current error summary |

```sql
CREATE TABLE tasks (
    task_id TEXT PRIMARY KEY,
    title TEXT NOT NULL CHECK (length(trim(title)) > 0),
    user_request TEXT NOT NULL CHECK (length(trim(user_request)) > 0),
    intent_type TEXT NOT NULL CHECK (intent_type IN (
        'general_conversation', 'information_request',
        'map_operation', 'complex_map_creation'
    )),
    status TEXT NOT NULL CHECK (status IN (
        'created', 'planning', 'ready', 'running', 'waiting_for_user',
        'paused', 'rolling_back', 'recovery_required',
        'completed', 'failed', 'cancelled'
    )),
    complexity TEXT NOT NULL CHECK (complexity IN ('simple', 'moderate', 'complex')),
    current_contract_version_number INTEGER NOT NULL DEFAULT 1 CHECK (current_contract_version_number >= 1),
    current_plan_version_number INTEGER CHECK (
        current_plan_version_number IS NULL OR current_plan_version_number >= 1
    ),
    created_at INTEGER NOT NULL,
    started_at INTEGER,
    finished_at INTEGER,
    updated_at INTEGER NOT NULL,
    error_summary TEXT,
    FOREIGN KEY (task_id, current_contract_version_number)
        REFERENCES task_contract_versions(task_id, version_number)
        DEFERRABLE INITIALLY DEFERRED,
    FOREIGN KEY (task_id, current_plan_version_number)
        REFERENCES task_plan_versions(task_id, version_number)
        DEFERRABLE INITIALLY DEFERRED,
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR finished_at >= created_at),
    CHECK (status NOT IN ('completed', 'failed', 'cancelled') OR finished_at IS NOT NULL)
);
CREATE INDEX idx_tasks_status_updated ON tasks(status, updated_at DESC);
CREATE INDEX idx_tasks_created_at ON tasks(created_at DESC);
```

The composite foreign keys ensure that the current contract and plan belong to the same Task. SQLite permits references to tables created later; migrations must create the referenced tables before inserting rows that use these pointers. The initial Task and its first contract version should be inserted in one deferred-FK transaction.

### 4.2 `task_contract_versions`

Purpose: preserve the exact execution boundary: User Goal, Scope, Acceptance Criteria, Permissions, Constraints, and Budget. The contract is not silently rewritten by the AI. An explicitly approved change creates a new immutable version.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `contract_version_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `version_number` | INTEGER | No | Starts at 1; unique per Task |
| `contract_data` | TEXT | No | JSON containing the six contract sections |
| `change_reason` | TEXT | Yes | Why a new version was created |
| `approval_evidence` | TEXT | Yes | JSON reference/evidence for explicit user approval; required for versions after V1 |
| `created_at` | INTEGER | No | Creation time |

```sql
CREATE TABLE task_contract_versions (
    contract_version_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    contract_data TEXT NOT NULL CHECK (json_valid(contract_data)),
    change_reason TEXT,
    approval_evidence TEXT CHECK (
        approval_evidence IS NULL OR json_valid(approval_evidence)
    ),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) DEFERRABLE INITIALLY DEFERRED,
    UNIQUE (task_id, version_number),
    UNIQUE (task_id, contract_version_id),
    CHECK (version_number = 1 OR (
        change_reason IS NOT NULL AND approval_evidence IS NOT NULL
    ))
);
```

The contract data is immutable after insertion. Enforce this through repository/application rules and a database trigger in the implementation migration. A new approved contract version updates `tasks.current_contract_version_number` in the same transaction. The approval evidence must identify the user's explicit decision; an AI recommendation is not approval.

### 4.3 `task_plan_versions`

Purpose: immutable history of complete execution plans. Replanning creates a new version; it never overwrites the previous plan.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `plan_version_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `version_number` | INTEGER | No | Unique per Task, starts at 1 |
| `plan_summary` | TEXT | No | Human-readable summary |
| `planning_reason` | TEXT | Yes | Reason for this plan/replan |
| `status` | TEXT | No | `active` or `superseded` |
| `created_at` | INTEGER | No | Creation time |

```sql
CREATE TABLE task_plan_versions (
    plan_version_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    plan_summary TEXT NOT NULL,
    planning_reason TEXT,
    status TEXT NOT NULL CHECK (status IN ('active', 'superseded')),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) DEFERRABLE INITIALLY DEFERRED,
    UNIQUE (task_id, version_number),
    UNIQUE (task_id, plan_version_id)
);
CREATE UNIQUE INDEX idx_task_plan_one_active
    ON task_plan_versions(task_id) WHERE status = 'active';
CREATE INDEX idx_task_plan_history
    ON task_plan_versions(task_id, version_number);
```

At most one plan version per Task may be active. Switch versions in one short transaction: supersede old version, insert new active version, update the Task pointer. The previous version and its steps remain available.

### 4.4 `task_steps`

Purpose: concrete executable units within one exact plan version.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `step_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task; duplicated for composite integrity and efficient queries |
| `plan_version_id` | TEXT | No | Exact plan version |
| `step_number` | INTEGER | No | Positive order within the plan |
| `objective` | TEXT | No | Step objective |
| `input_data` | TEXT | Yes | Optional JSON inputs/references |
| `action_description` | TEXT | No | Intended action |
| `acceptance_criteria` | TEXT | No | JSON acceptance criteria |
| `status` | TEXT | No | `pending`, `running`, `completed`, `failed`, `skipped` |
| `result_summary` | TEXT | Yes | Result summary |
| `output_refs` | TEXT | Yes | JSON references to outputs/assets |
| `error_summary` | TEXT | Yes | Error summary |
| `started_at` | INTEGER | Yes | Start time |
| `finished_at` | INTEGER | Yes | Finish time |
| `created_at` | INTEGER | No | Creation time |
| `updated_at` | INTEGER | No | Update time |

```sql
CREATE TABLE task_steps (
    step_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    plan_version_id TEXT NOT NULL,
    step_number INTEGER NOT NULL CHECK (step_number >= 1),
    objective TEXT NOT NULL,
    input_data TEXT CHECK (input_data IS NULL OR json_valid(input_data)),
    action_description TEXT NOT NULL,
    acceptance_criteria TEXT NOT NULL CHECK (json_valid(acceptance_criteria)),
    status TEXT NOT NULL CHECK (status IN ('pending', 'running', 'completed', 'failed', 'skipped')),
    result_summary TEXT,
    output_refs TEXT CHECK (output_refs IS NULL OR json_valid(output_refs)),
    error_summary TEXT,
    started_at INTEGER,
    finished_at INTEGER,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, plan_version_id)
        REFERENCES task_plan_versions(task_id, plan_version_id),
    UNIQUE (task_id, step_id),
    UNIQUE (plan_version_id, step_number),
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR (started_at IS NOT NULL AND finished_at >= started_at))
);
CREATE INDEX idx_task_steps_execution
    ON task_steps(task_id, plan_version_id, status, step_number);
```

A Step belongs to one immutable plan version. A replan creates new Step rows under the new plan. Completed steps from an old plan are not silently moved or rewritten; the new plan may explicitly refer to their outputs through input references.

### 4.5 `tool_calls`

Purpose: record each invocation of a statically registered internal tool. A Tool Call may create zero or more Operations.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `call_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `step_id` | TEXT | No | Owning Step, must belong to same Task |
| `tool_id` | TEXT | No | Static tool registry identifier |
| `tool_version` | TEXT | No | Contract/implementation version used |
| `status` | TEXT | No | `created`, `running`, `succeeded`, `failed`, `timed_out`, `interrupted` |
| `input_data` | TEXT | Yes | JSON input or references |
| `output_data` | TEXT | Yes | JSON output or references |
| `error_code` | TEXT | Yes | Stable error code |
| `error_message` | TEXT | Yes | Diagnostic message |
| `started_at` | INTEGER | Yes | Start time |
| `finished_at` | INTEGER | Yes | Finish time |
| `created_at` | INTEGER | No | Record creation time |

```sql
CREATE TABLE tool_calls (
    call_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT NOT NULL,
    tool_id TEXT NOT NULL,
    tool_version TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN (
        'created', 'running', 'succeeded', 'failed', 'timed_out', 'interrupted'
    )),
    input_data TEXT CHECK (input_data IS NULL OR json_valid(input_data)),
    output_data TEXT CHECK (output_data IS NULL OR json_valid(output_data)),
    error_code TEXT,
    error_message TEXT,
    started_at INTEGER,
    finished_at INTEGER,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    UNIQUE (task_id, call_id),
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR (started_at IS NOT NULL AND finished_at >= started_at))
);
CREATE INDEX idx_tool_calls_task ON tool_calls(task_id, created_at);
CREATE INDEX idx_tool_calls_step ON tool_calls(step_id, created_at);
CREATE INDEX idx_tool_calls_recovery ON tool_calls(status)
    WHERE status IN ('created', 'running', 'interrupted');
```

`interrupted` is not proof that the tool failed or had no side effects. Recovery must inspect operation records and actual persisted state. Large payloads belong in the Asset Store and are referenced from JSON.

### 4.6 `task_operations`

Purpose: durable record of each side effect that may need idempotency checking, recovery, or Undo. This table—not logs or Tool Call status—is the authoritative basis for Task-level rollback.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `operation_id` | TEXT | No | Primary key and stable business idempotency key |
| `task_id` | TEXT | No | Owning Task |
| `step_id` | TEXT | No | Owning Step, same Task |
| `call_id` | TEXT | Yes | Tool Call that produced it, same Task |
| `operation_sequence` | INTEGER | No | Unique monotonically increasing order within Task |
| `operation_type` | TEXT | No | Domain operation identifier |
| `target_type` | TEXT | No | Target category |
| `target_ref` | TEXT | No | Target identifier/reference |
| `status` | TEXT | No | `prepared`, `completed`, `failed`, `uncertain` |
| `undo_status` | TEXT | No | `not_required`, `pending`, `undoing`, `undone`, `undo_failed`, `uncertain` |
| `input_ref` | TEXT | Yes | Input reference |
| `output_ref` | TEXT | Yes | Output reference |
| `before_state_ref` | TEXT | Yes | State snapshot/reference before mutation |
| `after_state_ref` | TEXT | Yes | State snapshot/reference after mutation |
| `recovery_data` | TEXT | Yes | JSON sufficient for deterministic inspection/recovery/undo |
| `error_message` | TEXT | Yes | Execution or undo error |
| `created_at` | INTEGER | No | Prepared-record creation time |
| `completed_at` | INTEGER | Yes | Side-effect completion time |
| `undone_at` | INTEGER | Yes | Successful undo time |

```sql
CREATE TABLE task_operations (
    operation_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT NOT NULL,
    call_id TEXT,
    operation_sequence INTEGER NOT NULL CHECK (operation_sequence >= 1),
    operation_type TEXT NOT NULL,
    target_type TEXT NOT NULL,
    target_ref TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('prepared', 'completed', 'failed', 'uncertain')),
    undo_status TEXT NOT NULL CHECK (undo_status IN (
        'not_required', 'pending', 'undoing', 'undone', 'undo_failed', 'uncertain'
    )),
    input_ref TEXT,
    output_ref TEXT,
    before_state_ref TEXT,
    after_state_ref TEXT,
    recovery_data TEXT CHECK (recovery_data IS NULL OR json_valid(recovery_data)),
    error_message TEXT,
    created_at INTEGER NOT NULL,
    completed_at INTEGER,
    undone_at INTEGER,
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    FOREIGN KEY (task_id, call_id) REFERENCES tool_calls(task_id, call_id),
    UNIQUE (task_id, operation_sequence),
    UNIQUE (task_id, operation_id),
    CHECK (completed_at IS NULL OR completed_at >= created_at),
    CHECK (undone_at IS NULL OR undone_at >= created_at),
    CHECK (undo_status != 'undone' OR undone_at IS NOT NULL)
);
CREATE INDEX idx_task_operations_task ON task_operations(task_id, operation_sequence);
CREATE INDEX idx_task_operations_step ON task_operations(step_id, operation_sequence);
CREATE INDEX idx_task_operations_call ON task_operations(call_id);
CREATE INDEX idx_task_operations_undo ON task_operations(task_id, undo_status)
    WHERE undo_status IN ('pending', 'undoing', 'undo_failed', 'uncertain');
```

Rules:

1. Persist the Operation as `prepared`, including its stable `operation_id` and sufficient recovery data, before executing the side effect.
2. A safe retry of the same business action reuses the same `operation_id`; a different action gets a different ID.
3. A Tool Call can produce many Operations; each independently reversible side effect gets its own record.
4. On Task failure/cancellation, stop all possible writers first, then undo Operations in descending `operation_sequence` order.
5. Each undo action and its `undo_status` update commit in one short transaction. Do not wrap the entire Task rollback in one huge transaction.
6. If the effect or undo result cannot be determined, mark it uncertain and move the Task to `recovery_required`; do not blindly replay.
7. Formal map application is outside ordinary Task Undo. Reversal of an applied Change Set requires a new formal reverse Change Set.

### 4.7 `task_logs`

Purpose: append-only diagnostic and audit trail. Logs never become the authority for Task, Tool Call, or Operation state.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `log_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `step_id` | TEXT | Yes | Related Step, same Task |
| `call_id` | TEXT | Yes | Related Tool Call, same Task |
| `operation_id` | TEXT | Yes | Related Operation, same Task |
| `event_type` | TEXT | No | Stable event identifier |
| `severity` | TEXT | No | `debug`, `info`, `warning`, `error` |
| `message` | TEXT | No | Human-readable message |
| `details` | TEXT | Yes | Optional diagnostic JSON |
| `created_at` | INTEGER | No | Event time |

```sql
CREATE TABLE task_logs (
    log_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT,
    call_id TEXT,
    operation_id TEXT,
    event_type TEXT NOT NULL,
    severity TEXT NOT NULL CHECK (severity IN ('debug', 'info', 'warning', 'error')),
    message TEXT NOT NULL,
    details TEXT CHECK (details IS NULL OR json_valid(details)),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id),
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    FOREIGN KEY (task_id, call_id) REFERENCES tool_calls(task_id, call_id),
    FOREIGN KEY (task_id, operation_id) REFERENCES task_operations(task_id, operation_id)
);
CREATE INDEX idx_task_logs_task_time ON task_logs(task_id, created_at);
CREATE INDEX idx_task_logs_severity ON task_logs(task_id, severity, created_at);
```

Do not write secrets, credentials, or unnecessary sensitive user content into logs. Large input/output data must be referenced, not duplicated.

### 4.8 `human_intervention_requests`

Purpose: persist a single decision/request that requires human input. A Task may have multiple requests over its lifetime, but each request should represent one decision.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `request_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `step_id` | TEXT | Yes | Related Step, same Task |
| `request_type` | TEXT | No | `clarification`, `decision`, `authorization`, `baseline_change`, `external_input` |
| `status` | TEXT | No | `open`, `resolved`, `withdrawn` |
| `title` | TEXT | No | Short title |
| `description` | TEXT | No | Problem description |
| `reason` | TEXT | No | Why AI cannot safely decide autonomously |
| `evidence` | TEXT | Yes | JSON evidence/references |
| `options` | TEXT | Yes | JSON choices |
| `recommended_option` | TEXT | Yes | JSON recommended choice and rationale |
| `required_decision` | TEXT | No | Exact decision requested |
| `response` | TEXT | Yes | JSON user response |
| `created_at` | INTEGER | No | Creation time |
| `resolved_at` | INTEGER | Yes | Resolution/withdrawal time |

```sql
CREATE TABLE human_intervention_requests (
    request_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT,
    request_type TEXT NOT NULL CHECK (request_type IN (
        'clarification', 'decision', 'authorization', 'baseline_change', 'external_input'
    )),
    status TEXT NOT NULL CHECK (status IN ('open', 'resolved', 'withdrawn')),
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    reason TEXT NOT NULL,
    evidence TEXT CHECK (evidence IS NULL OR json_valid(evidence)),
    options TEXT CHECK (options IS NULL OR json_valid(options)),
    recommended_option TEXT CHECK (
        recommended_option IS NULL OR json_valid(recommended_option)
    ),
    required_decision TEXT NOT NULL,
    response TEXT CHECK (response IS NULL OR json_valid(response)),
    created_at INTEGER NOT NULL,
    resolved_at INTEGER,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id),
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    CHECK (resolved_at IS NULL OR resolved_at >= created_at),
    CHECK (status != 'resolved' OR resolved_at IS NOT NULL),
    CHECK (status != 'withdrawn' OR resolved_at IS NOT NULL)
);
CREATE INDEX idx_human_requests_task ON human_intervention_requests(task_id, created_at);
CREATE INDEX idx_human_requests_open ON human_intervention_requests(status, created_at)
    WHERE status = 'open';
```

When a request is opened, the Task normally enters `waiting_for_user`. A user response resolves the request and allows the Coordinator to reassess the plan. Refusing a recommended option does not automatically cancel the Task. Preserve the original response; do not overwrite it with an AI paraphrase.

### 4.9 `task_delivery_records`

Purpose: immutable snapshot of exactly which candidate sets and exact candidate versions were delivered when a Task completed. This record references Candidate Management entities; it does not duplicate geometry or candidate payloads.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `delivery_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task; unique, at most one delivery per Task |
| `delivered_candidates` | TEXT | No | JSON snapshot of Candidate Set IDs/versions, Object Candidate IDs/versions, and validation report references/versions |
| `delivery_summary` | TEXT | Yes | Human-readable delivery summary |
| `created_at` | INTEGER | No | Delivery time |

```sql
CREATE TABLE task_delivery_records (
    delivery_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL UNIQUE,
    delivered_candidates TEXT NOT NULL CHECK (json_valid(delivered_candidates)),
    delivery_summary TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id)
);
```

The delivery snapshot is immutable. Candidate edits after Task completion create new candidate versions and do not rewrite this record.

## 5. Critical transaction boundaries

### 5.1 Task creation

In one transaction, create the Task and its initial contract version (V1), and set the Task's current contract version pointer. A plan may not exist yet.

### 5.2 Plan creation and replacement

Create the plan version and its steps consistently. When replacing a plan, use one transaction to supersede the previous active plan, insert the new active plan and steps, and update `tasks.current_plan_version_number`. The prior plan and steps remain immutable historical records.

### 5.3 Tool side effects

Before a write-capable tool performs a side effect, persist the `prepared` Operation with its idempotency key and recovery information. After the effect is verified, update the Operation to `completed` and persist output references. Never infer side-effect completion solely from Tool Call status.

### 5.4 Task rollback

Set Task to `rolling_back` and establish a write barrier: no new Task writes and no tool capable of producing side effects may continue. Undo Operations in reverse order. Commit each individual undo and its status update in a short transaction. On any unresolved/failed undo, set `recovery_required`; preserve all records.

### 5.5 Task completion

In one database transaction: verify completion conditions; finalize the delivered candidate states; insert the immutable `task_delivery_records` row; set Task status to `completed` and `finished_at`; append the completion audit event. If any part fails, roll back the whole transaction.

## 6. State authority and immutability

| Information | Authority |
|---|---|
| Current Task state | `tasks.status`, changed only through Task Coordinator state machine |
| Current contract | `tasks.current_contract_version_number` + immutable `task_contract_versions` |
| Current plan | `tasks.current_plan_version_number` + active `task_plan_versions` row |
| Step state | `task_steps.status`, coordinated by Task Coordinator |
| Tool invocation state | `tool_calls.status` |
| Side-effect and Undo truth | `task_operations.status` and `undo_status`, reconciled against persisted domain state |
| Human decision state | `human_intervention_requests.status` and preserved response |
| Delivered result | immutable `task_delivery_records` snapshot |
| Diagnostic history | `task_logs`; never authoritative for recovery |

Application code must enforce legal state transitions. CHECK constraints restrict the allowed vocabulary but do not implement the transition graph. Immutable rows (contract versions, plan versions/steps after plan activation, delivery records, and logs) should be protected by repository rules and appropriate SQLite triggers.

## 7. Core Domain integration requirements

This schema intentionally does not define or assume the names of Core Domain tables. Before migrations are finalized:

1. Inspect the actual World View Core Domain schema.
2. Reuse its existing Object Identity and Branch tables; do not create duplicate identity or branch authorities.
3. Map Candidate, Operation target, and delivery references to actual Core Domain identifiers where appropriate.
4. Keep candidate data separate from formal map data.
5. Ensure formal map modifications remain exclusively through Change Set + Domain Services.

## 8. Implementation notes and deferred decisions

- UUID generation should use a maintained UUID library; UUID v7 is a suitable choice if supported consistently by the selected .NET runtime/library. The database stores canonical lowercase UUID text.
- Time values are UTC Unix milliseconds. Do not mix local timestamps, ISO strings, seconds, and milliseconds.
- JSON schemas are validated by application code in addition to `json_valid()` syntax checks.
- The `operation_type`, `target_type`, and reference formats are domain/tool contracts and should be catalogued in code; do not prematurely create a generic plugin framework.
- Asset files are staged and validated before database references are committed. Orphaned files can be cleaned only after confirming no database references exist.
- No Task Attempt table, DAG engine, distributed lock, message broker, or separate recovery service is required for this single-user local-first V1.0.
- The exact SQLite journal/synchronous settings and provider version must be tested on target Windows/macOS packaging before release.

## 9. Review checklist before implementation

- [ ] Inspect and map actual Core Domain schema and identifiers.
- [ ] Confirm SQLite provider supports required foreign keys, JSON functions, deferred constraints, and selected migration behavior.
- [ ] Add migration tests for composite same-Task foreign keys.
- [ ] Add tests for immutable contract/plan/delivery records.
- [ ] Add tests for idempotent Operation creation and interrupted Tool Calls.
- [ ] Add tests for reverse-order Task Undo and crash recovery between individual undo transactions.
- [ ] Add tests that Task completion and Delivery Record are atomic.
- [ ] Verify no AI Assistant path can write formal map tables except through Domain Services.