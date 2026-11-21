# World View AI Assistant — Failure Recovery & Human Intervention V1.0

**Status:** V1.0 architecture baseline  
**Scope:** AI Assistant Task Planning & Execution Engine, Phase 08  
**Date:** 2026-11-20

## 1. Purpose

This document defines how World View handles work that cannot proceed normally because generated results need repair, the current execution method is unsuitable, user intent is ambiguous, additional authorization is required, or execution cannot reliably continue.

Phase 08 is distinct from Phase 06 (Tool Execution & Recovery): Phase 06 protects execution consistency and reverses a failed/cancelled Task's reversible writes; Phase 08 coordinates intelligent recovery and human decisions when the work itself encounters a problem.

## 2. Architectural constraints

- World View is a single-user, local-first desktop application. Do not introduce distributed recovery infrastructure, multi-agent recovery governance, or a general workflow engine.
- Task Coordinator owns recovery classification and coordination. No separate Recovery Agent is required.
- AI may change methods and execution steps within the existing user goal, scope, authorization, and acceptance criteria.
- AI must not silently change the user's goal, expand or narrow scope, lower acceptance criteria, or bypass required human confirmation.
- Existing Task, Task Step, Task Log, Tool Call, Tool Operation, Plan Version, Object ID Reservation, Candidate, Validation Report, and Change Set boundaries remain authoritative in their respective domains.
- Formal map changes continue to pass through Change Set and Domain Services.

## 3. Four-level recovery strategy

### 3.1 Automatic repair

Use for explicit, bounded defects whose repair does not change user intent or acceptance criteria. Examples include correcting a serialization format, filling a deterministically derivable field, or recomputing invalid derived geometry. Repairs are executed through authorized tools/domain operations and must be revalidated.

### 3.2 Replanning within constraints

Use when the current method is unsuitable but another method can satisfy the same goal, scope, authorization, and acceptance criteria. Examples include changing information sources, splitting work into smaller batches, changing tool order, or replacing a failed approach. Preserve completed work when its inputs and outputs remain valid. Record a new Plan Version when the plan materially changes.

### 3.3 Human intervention

Use when a decision belongs to the user: ambiguity in intent, a consequential semantic choice, missing user-only information, authorization beyond the current grant, or a proposed baseline change. Create a Human Intervention Request and move the Task to `Waiting for User`.

### 3.4 Unable to continue

Use when no reliable path remains within the user's constraints. Do not fabricate success, weaken acceptance criteria, or silently deliver an incomplete result as complete. Explain completed work, unresolved work, reasons, and any safely retainable handoff information. If the Task is terminated as Failed, apply the Phase 06 Task-level rollback policy to reversible writes.

## 4. Human Intervention Request

A Human Intervention Request is a lightweight child record of a Task, not a separate approval workflow or independent task-management system.

### 4.1 Suggested fields

- `request_id`
- `task_id`
- `request_type`
- `status`
- `title`
- `description`
- `reason`
- `evidence_refs`
- `options`
- `recommended_option`
- `required_decision`
- `response`
- `created_at`
- `resolved_at`

Large source documents and map data are referenced, not copied into the request record.

### 4.2 Request types

- `Clarification`: the user's request is ambiguous.
- `Decision`: multiple materially different, reasonable choices exist.
- `Authorization`: the next action exceeds current authorization.
- `Baseline Change`: goal, scope, or acceptance baseline needs to change.
- `External Input`: required information must come from the user.

### 4.3 Lifecycle

- `Open`: awaiting user response.
- `Resolved`: the user has provided a decision or information sufficient to close the request.
- `Withdrawn`: the request is no longer applicable or the issue was resolved without the requested user decision.

A request must not be marked Resolved if the response is insufficient. A later distinct decision may create a new request. Unrelated decisions should be separate requests so each can be independently answered and audited.

### 4.4 User-facing presentation

Show the issue, why AI cannot decide, investigation already performed, evidence, available options and consequences, AI recommendation and rationale, and the exact response needed. Users may select an option or provide free-form information. Authorization and baseline changes must explicitly describe what changes and their impact; a generic “Continue” action is not blanket authorization.

## 5. Resuming a Task after user intervention

A user response normally resumes the original Task rather than creating a new Task.

1. Persist the response against the corresponding request.
2. Re-read Task state, current Plan Version, completed Steps, Tool Calls, Tool Operations, and relevant data records.
3. Determine which completed outputs remain valid under the user's decision.
4. Reuse unaffected results.
5. Create a new Plan Version only when the plan materially changes; preserve prior versions and the original request.
6. Resume from the earliest affected incomplete or invalidated Step.
7. Revalidate changed outputs and any dependent aggregate result.

If the user's response changes a formal goal, scope, or acceptance baseline, record that change explicitly and follow the applicable baseline-change confirmation rules.

## 6. Recovery consistency checks

Recovery must be based on persisted records and actual domain data, not transient in-memory progress or an LLM's recollection.

Before resuming, Task Coordinator checks:

- Task state and latest Plan Version;
- pending and completed Task Steps;
- Tool Calls in Running, Interrupted, TimedOut, or otherwise uncertain states;
- Tool Operation state and whether its side effect already occurred;
- Object Draft persistence;
- Object ID Reservation and binding state;
- candidate dataset insertion state;
- Validation Report applicability to the current data version;
- unresolved Human Intervention Requests.

No write should begin until the affected Task's state is understood. Reuse existing Tool Operation IDs for retries of the same business operation. Do not allocate a new Object ID for an already reserved Draft. Check for existing candidate insertion before attempting it again.

## 7. Reuse, invalidation, and superseded results

- Completed Steps may be reused when their inputs and outputs remain valid.
- A completed Step must be re-evaluated if relevant inputs, user decisions, or upstream outputs change.
- A successful Tool Call with a valid persisted result is reused rather than repeated.
- An interrupted operation with uncertain side effects must be inspected before retry.
- Reserved Object IDs remain reserved and are never recycled, including after Task failure or cancellation. Abandoned reservations remain permanently unavailable.
- Existing Object-to-ID bindings are retained; recovery must not allocate replacement IDs for already bound objects.
- Candidate outputs that are no longer current are retained as history and marked `Superseded` (or an equivalent explicit non-current state); they must not be treated as current candidates.
- Validation Reports tied to changed data versions become stale and must be regenerated before the affected result is considered valid.

## 8. Task states and recovery outcomes

Do not create a separate Task state for every recovery scenario. Continue using the established Task lifecycle, including:

- `Running`
- `Waiting for User`
- `Paused`
- `Rolling Back`
- `Recovery Required`
- `Completed`
- `Failed`
- `Cancelled`

`Paused` is a user-controlled pause. `Waiting for User` means a specific Human Intervention Request is outstanding. A process crash may leave a persisted Task apparently Running; startup recovery must inspect its Tool Calls and actual side effects before resuming. `Recovery Required` means the system cannot establish a safe consistent state automatically and must block further writes in the affected scope.

## 9. Partial work and Task-level rollback compatibility

Phase 08 does not override the Phase 06 Task-level rollback rule. If a Task ultimately becomes Failed or Cancelled, all reversible writes produced by that Task are rolled back as a unit. Do not leave partial writes behind merely because some intermediate outputs were useful.

If useful partial information must be handed to the user after failure, preserve it only through an explicit, safe handoff/archive mechanism that is outside the rolled-back write set and does not masquerade as an applied candidate or formal map modification. The implementation must define this boundary before enabling such handoff. Otherwise, report the partial progress in the Task's retained diagnostic/history records without retaining rolled-back domain data.

A Task that remains active and is awaiting repair or user input may retain its own staged intermediate data, subject to the established Task ownership and recovery rules. Candidate readiness and formal application remain separate milestones.

## 10. Mandatory safety rules

1. Do not resume from in-memory progress alone.
2. Do not start new writes while the affected Task is in rollback or unresolved recovery.
3. Do not blindly replay uncertain Tool Calls or Tool Operations.
4. Do not bypass Tool Operation idempotency, Object ID Reservation, Object binding, or Domain Services.
5. Do not claim rollback succeeded until its completion is verified.
6. Do not silently alter user goals, scope, authorization, or acceptance criteria.
7. Revalidate after repairs or data changes.
8. Preserve Task Logs, request/response history, Plan Versions, and recovery audit information.

## 11. Phase 08 decision register

| ID | Decision | Baseline |
|---|---|---|
| FR-01 | Four recovery levels: automatic repair, constrained replanning, human intervention, unable to continue | Adopt |
| FR-02 | Task Coordinator owns recovery coordination | Adopt |
| FR-03 | No independent Recovery Agent | Adopt |
| FR-04 | AI may change methods only within goal, scope, authorization, and acceptance constraints | Mandatory |
| FR-05 | Goal/scope/acceptance changes require explicit user decision where applicable | Mandatory |
| FR-06 | Human Intervention Request is a Task child record | Adopt |
| FR-07 | Five basic intervention request types | Adopt |
| FR-08 | Request lifecycle: Open, Resolved, Withdrawn | Adopt |
| FR-09 | User response normally resumes the original Task | Adopt |
| FR-10 | Reuse unaffected completed work | Adopt |
| FR-11 | Recovery checks persisted state and actual side effects before resuming | Mandatory |
| FR-12 | Preserve Plan Version history | Adopt |
| FR-13 | Superseded outputs remain historical but are not current candidates | Adopt |
| FR-14 | Stale validation reports cannot authorize changed data | Mandatory |
| FR-15 | Phase 06 Task-level rollback remains authoritative on final failure/cancellation | Mandatory |
| FR-16 | No new writes during rollback or unresolved Recovery Required state | Mandatory |
| FR-17 | Reserved Object IDs are never recycled | Confirmed product rule |

## 12. Relationship to adjacent phases

- Phase 03: respects Planner and dynamic replanning boundaries.
- Phase 05: uses Tool Contract, Tool Call, Tool Operation, and idempotency semantics.
- Phase 06: preserves Task-level rollback, interruption recovery, and `Recovery Required` behavior.
- Phase 07: relies on Result Validation and Candidate Management; data changes invalidate applicable reports.
- Formal map application remains a separate Change Set + Domain Services boundary.

---

**Implementation note:** This is a logical architecture baseline for a single-process local application. It does not prescribe microservices, distributed locks, distributed queues, or a general-purpose workflow engine.