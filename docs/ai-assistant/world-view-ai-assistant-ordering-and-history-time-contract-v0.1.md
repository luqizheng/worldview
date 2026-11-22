# World View AI Assistant — Ordering and Historical Time Integration Contract

- Version: 0.1
- Date: 2026-11-20
- Status: Design working baseline — pending formal Core Domain review
- Scope: AI Assistant Task Planning & Execution Engine V1.0
- Storage: Single-user, local-first desktop application

## 1. Purpose and authority

This contract translates P2-01-D01 D01-11~28 into the AI Assistant integration boundary. Those source records remain Proposed / Discussion, not a frozen physical Core Domain contract.

Core Domain alone owns formal Commands, explicit ordering constraints, Historical Time interpretation, dependency evaluation, conflict status, reconstruction and playback. AI Assistant owns draft proposals, explanations, user-interaction state and immutable authorization evidence. AI must not maintain a parallel authoritative ordering/dependency graph.

## 2. Three distinct ordering layers

Every ordering relation shown in a proposal must be classified:

1. **Existing authoritative constraints** — persisted Core Domain constraints, including applicable inherited parent-map constraints. They are read-only to AI Assistant and must never be deleted, rewritten, reversed or silently replaced.
2. **Deterministic derived order** — Core Domain may derive order from dependencies and safe technical tie-breakers only where the result is semantically invariant. This is not a new user-authored constraint and must not be persisted merely because AI displayed a sorted list.
3. **Proposed new user constraints** — AI may recommend a relation or translate a user instruction. A recommendation is not authoritative. Accepted relations are submitted through a Core Domain-supported typed authoring operation and included in the exact authorized manifest.

A proposal must preserve relation endpoints, direction, provenance and relevant Core Domain references. A numeric rank or one-time sorted sequence is not a substitute for an explicit relation.

## 3. Historical Time

Historical Time is when a Command takes effect in the modeled world. Proposal creation, editing, authorization and submission timestamps are transaction time and must never substitute for Historical Time.

D01-21 records per-Command precision up to seconds. Insufficient precision does not eliminate the need for ordering constraints. Different precision levels do not imply priority: a year-precision Command is not automatically earlier than a date- or second-precision Command.

The Core Domain supplies the canonical time representation, precision semantics, calendar interpretation and duration contract. The D01 direction—integer cumulative days plus integer within-day offset, currently seconds—is a semantic clue, not permission for AI Assistant to define the physical schema.

For a Branch-owned Command/State Change, Core Domain enforces:

`effective_time > parent_at`

AI Assistant must not silently shift a time at or before `parent_at`. Selecting an earlier fork or another History is a separate user decision.

## 4. Dependencies and automatic ordering

D01-11 recognizes explicit user dependencies and dynamically discovered implicit dependencies. Explicit dependencies are authoritative user data; implicit dependencies are derived and recalculable. Core Domain owns dependency evaluation and explanation.

Ordering first respects dependencies. A deterministic technical tie-breaker is permitted only when it cannot change semantic results. If multiple possible orders produce different world states and intent is unclear, pause the affected Commands and ask the user. Unrelated Commands may still receive independent diagnostics, while full history reconstruction remains Fail-Fast at the first execution error.

## 5. Ambiguity, conflict and cycles

### 5.1 Time changes

If editing Historical Time conflicts with an explicit ordering relation, retain the relation, issue a diagnostic, pause affected Commands pending user handling, and do not silently remove or rewrite the relation. Unrelated diagnostics may continue.

### 5.2 Cyclic constraints

D01-24 explicitly permits saving a new relation even if it creates a cycle. Preserve the user-authored constraint; mark the cycle as conflicted; pause Commands in the cycle and Commands depending on them; do not silently remove constraints; continue diagnostics for unrelated Commands. The user resolves the cycle and affected history is reconstructed again.

A cycle is therefore not automatically a malformed proposal or database transaction failure. Core Domain must distinguish structural acceptance/persistence from successful ordering evaluation and reconstruction. If the cycle is known before authorization, the user-facing proposal must explain the consequence.

### 5.3 Deleting a constraint

After a user deletes a relation, Core Domain rechecks explicit and implicit dependencies. If order remains determinable, processing may continue; otherwise affected Commands are warned and paused. AI must not assume deleting one relation resolves ambiguity.

## 6. Branch inheritance

1. Parent-map ordering constraints are inherited according to Core Domain Branch rules.
2. A Branch may add its own constraints.
3. A Branch cannot override, replace or rewrite inherited parent constraints.
4. If inherited and Branch-owned relations conflict or form a cycle, preserve all relations, mark the Branch conflict, and pause affected Commands and their dependency chain.
5. This does not freeze all Branch authoring; unrelated work may continue.
6. AI must not create an override flag or replacement relation to bypass inheritance.
7. If the user wants a different combined ordering, the separate operation is to create a new combined map from parent and child Branches. This is not an in-Branch override, Git merge or rebase.

Core Domain supplies inherited relation identity and lineage. AI must not synthesize them.

## 7. Command editing and ordering impact

D01-08 and D01-13~16 establish that Commands can be edited, deleted, inserted at arbitrary historical times and have Historical Time changed. These are Core Domain authoring operations, not AI-side row updates.

- Inserting a Command retains later Commands and triggers dependency reevaluation from the earliest affected point.
- Changing Historical Time rebuilds from the earlier of old and new times.
- Deleting a Command does not automatically delete dependent Commands; they remain and are re-evaluated.
- Edits/deletions can invalidate derived State Changes and affected Branch Checkpoints.
- AI must use exact Core Domain-supported authoring operations, never generic CRUD mutation of current World State.

## 8. Proposal manifest and authorization

Where applicable, the sealed manifest includes:
- exact typed proposal and descriptor/version references;
- exact formal Commands being edited/deleted;
- exact source Candidate Versions;
- target Map, selected Branch and Core Domain-resolved History target;
- canonical Historical Time, precision and duration;
- references to existing authoritative ordering constraints used as preconditions;
- proposed relations, endpoints and direction;
- provenance for each proposed relation (explicit user instruction or AI recommendation);
- explicit acceptance evidence where required;
- Core Domain dependency/ordering diagnostics;
- target-context precondition and preflight references;
- asset and Object reservation references.

The content hash covers every semantic field affecting formal results: target, time, precision, payload, relation endpoints/direction, accepted constraints and referenced assets. Display-only formatting is excluded. Any post-authorization semantic change invalidates authorization and requires a new sealed version and authorization.

## 9. Core Domain integration capabilities

Core Domain should expose conceptual capabilities to:
1. Return target context, inherited constraints and Branch boundary.
2. Describe supported Command authoring types and descriptors.
3. Validate proposed Commands/relations without persistence.
4. Explain dependency chains and result-changing ambiguity.
5. Distinguish structural invalidity, semantic ambiguity, cyclic conflict and reconstructability.
6. Persist the authorized batch atomically.
7. Return exact persisted Command and constraint references.
8. Report reconstruction and valid playback boundary separately.

Preflight is not a guarantee that full reconstruction succeeds. A permitted cyclic constraint must not be silently removed. The exact UX and whether a descriptor permits persistence of a conflict are Core Domain contract questions.

## 10. Application Report

Do not collapse these outcomes into one `applied` boolean:
- proposal authorized;
- formal Commands persisted;
- ordering constraints persisted;
- dependency/ordering evaluation completed or conflicted;
- World State reconstruction completed or failed;
- playback availability and valid time boundary.

If Commands persist but reconstruction encounters a cycle or Fail-Fast error, Commands remain authoritative history. The report records conflict/failure and valid playback boundary; it must not claim the formal write was rolled back. Task Undo after formal submission does not delete or rewrite persisted Core Domain Commands.

## 11. AI Assistant data model impact

Store proposal/evidence, not duplicate Core Domain ordering authority. Conceptual AI-side data:
- `ordering_intents` inside immutable Change Set versions, with proposed relations and provenance;
- opaque read-only references to Core Domain constraints;
- immutable preflight diagnostics tied to exact proposal/context;
- immutable user acceptance evidence;
- stable submission-attempt identity and manifest hash;
- Application Report dimensions for persistence, ordering evaluation, reconstruction and playback.

Do not create AI-owned formal `commands`, `command_dependencies`, `branch_ordering_constraints` or an independent ordering graph. Exact SQL schema and foreign keys wait for Core Domain physical schema and transaction contract.

## 12. Required verification scenarios

1. Dependencies determine same-time order.
2. A result-invariant tie receives a deterministic Core Domain tie-breaker.
3. A result-changing ambiguity pauses for user decision.
4. Different time precision does not create priority.
5. An accepted explicit relation survives insertion of another Command.
6. A time edit conflicting with a relation retains it and pauses affected Commands.
7. A cycle can persist when Core Domain permits; cycle and dependents become conflicted.
8. Unrelated Commands remain eligible for independent diagnostics.
9. Deleting a relation triggers dependency reevaluation.
10. Parent constraints are inherited and cannot be overridden by Branch.
11. Conflicting Branch constraints are retained; unrelated Branch authoring continues.
12. `effective_time <= parent_at` is rejected without time adjustment.
13. Arbitrary-time insertion retains later Commands and re-evaluates dependencies.
14. Deleting a Command does not automatically delete dependents.
15. Manifest hash changes when accepted relations or semantic time changes.
16. Lost submission response is reconciled without duplicate Commands/constraints.
17. Persisted Commands remain after reconstruction failure.
18. Full reconstruction stops at first execution error while independent diagnostics can continue where allowed.

## 13. Open Core Domain integration gates

- **A — Ordering descriptor:** supported types, constraint identity, endpoints, direction and provenance.
- **B — Dependency evaluation:** authoritative versus derived dependencies, explanations and deterministic tie-breakers.
- **C — Historical Time:** canonical representation, precision, duration and calendar semantics.
- **D — Branch inheritance:** inherited constraint identity and dynamic reevaluation.
- **E — Conflict persistence:** exact contract for saving cyclic constraints and representing conflicted Commands without misreporting transaction failure.
- **F — Atomic submission:** atomic Command/constraint persistence and idempotent reconciliation.
- **G — Reconstruction reporting:** Fail-Fast diagnostics, independent diagnostic behavior and playback boundary.

## 14. Conclusion

Preserve Core Domain authoritative constraints; use deterministic derived order only when semantically invariant; obtain user decisions for result-changing ambiguity; represent accepted new relations as typed Core Domain authoring data; never override inherited Branch constraints; and report persistence separately from conflict evaluation and reconstruction.

This is a design integration baseline, not a final physical database schema or implementation authorization.