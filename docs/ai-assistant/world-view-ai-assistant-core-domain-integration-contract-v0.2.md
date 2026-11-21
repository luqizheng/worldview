# World View AI Assistant — Core Domain Integration Contract V0.2

- Version: 0.2
- Date: 2026-11-20
- Status: **Design working baseline — pending full Core Domain integration review; not an implementation authorization**
- Scope: AI Assistant Task Planning & Execution Engine V1.0 ↔ World View Core Domain
- Supersedes: V0.1 for current design discussions; V0.1 is retained as historical record
- Related authority: `world-view-rs/adr/ADR-001` through `ADR-005`; `world-view-rs/P2-01-D01` D01-01~143; AI Assistant DB prototype V1.14

## 1. Purpose and decision hierarchy

This document corrects the assumptions in V0.1 and defines the integration boundary around World View's history-command model. It does not redefine Core Domain concepts or invent its physical schema.

Decision hierarchy:

1. Accepted / Frozen Core Domain ADRs are authoritative.
2. User-confirmed D01 discussion decisions are design inputs, but remain Proposed / Discussion until formally reviewed/frozen. They must not silently override a frozen ADR.
3. AI Assistant documents describe how the assistant integrates with Core Domain; they cannot establish new Core Domain semantics.
4. V1.14 is a historical SQLite prototype, not an implementation baseline.

The AI Assistant must remain agnostic wherever the Core Domain has not yet exposed a stable domain contract. It must not fill those gaps with shadow domain entities.

## 2. Corrected authority model

| Concern | Authority | AI Assistant responsibility |
|---|---|---|
| Map, Branch, History and lineage | Core Domain | Hold references returned by Core Domain; never reproduce lineage rules |
| Formal Object identity and permanent reservation | Core Domain | Request reservation; retain opaque reservation and Object references |
| Command authoring history | Core Domain | Prepare proposals; submit only through Domain Services |
| Event and State Change semantics | Core Domain | Supply proposal context only where supported; never treat derived State Change as a second command ledger |
| World State and historical reconstruction | Core Domain | Request preview/rebuild/status; never reconstruct independently |
| Checkpoint creation, validity, reuse and invalidation | Core Domain | Consume availability/status results only |
| Candidate Set, Candidate, Candidate Version | AI Assistant | Own generated/staged results and immutable candidate evidence |
| Validation and Selection evidence | AI Assistant | Own reports and immutable selection snapshots |
| Command Proposal and Change Set preparation | AI Assistant | Own draft proposals and exact content manifest; no authority over persisted Commands |
| Authorization to submit AI-prepared Commands | Human user | Capture explicit human approval for exact proposal content and target context |
| Formal History writes | Core Domain Domain Services | Submit authorized proposal through one transaction-aware boundary |
| Application attempt evidence | AI Assistant integration layer, with atomicity placement to be verified | Record exactly what Core Domain accepted and the returned references |

No AI Assistant table may become a second authority for Object Identity, Branch, History, Command, Event, State Change, World State, or Checkpoint.

## 3. Core history semantics the integration must preserve

### 3.1 Command, State Change and World State are distinct

- **Command** is the authoritative record of an author's history-creation action.
- **State Change** expresses the state-change semantics represented or produced by a Command. It is derived/executable history meaning, not automatically a second independent authoring ledger.
- **World State** is the result of interpreting and executing the applicable Commands at a requested Historical Time.

A Command is not required to map one-to-one to State Changes. It may produce no State Change, one State Change, or multiple derived State Changes over its execution.

AI Assistant must therefore not persist a generated geometry or attribute snapshot as though it were the authoritative resulting World State. It may persist it as Candidate evidence and use it as input to a Core Domain-supported Command Proposal.

### 3.2 Commands are author-editable history, not immutable version entities

D01-08 records that an author may directly modify an existing Command and that a Command Revision chain is not required. D01-13~16 also allow editing, deleting and inserting Commands, followed by rebuilding the affected history.

Consequences:

- Do not introduce `ObjectVersion`, `BranchRevision`, `CommandRevision`, or a Git-like revision chain as AI Assistant domain entities.
- The Core Domain Command is authoritative history, but it is not necessarily immutable for its entire lifetime.
- AI-owned proposal snapshots, content hashes, human authorization evidence and application-attempt records are immutable evidence and must not be confused with Core Domain Command versioning.
- A later author edit/delete is a normal Core Domain history-editing operation. It is not a mutation of the old AI authorization evidence.

### 3.3 Branch semantics are not an open AI Assistant decision gate

The frozen ADR rules apply:

- Map and Branch use the same History entity model.
- A Branch owns its own History and references its parent History plus `parent_at`; it does not copy ancestor Commands.
- A Branch inherits parent history through the defined fork boundary.
- Parent changes within the inherited boundary affect descendants; parent changes after the boundary do not.
- Branch-owned State Changes must satisfy `effective_time > parent_at`.
- Branch changes do not propagate to parent or sibling Branches.
- No Merge, Rebase, Cherry-pick or conflict-resolution model is introduced.

The user-confirmed D01-71 describes the inherited boundary as closed: parent history at `time <= parent_at` is inherited; later parent history is not. D01-76~100 and D01-101~143 add dynamic invalidation, path reconstruction and checkpoint reuse rules. These are to be implemented by Core Domain, not AI Assistant.

**V0.1's statement that Branch semantics still require reconciliation is withdrawn.** AI Assistant does not need a separate Branch policy choice.

### 3.4 Shared Object scope remains a Core Domain semantic integration gate

The frozen ADR-003 describes shared-object changes and Map-local changes as distinct scopes. Later D01-70/76 discussion clarifies that Shared Object identity does not imply one shared mutable runtime state, that a Branch-local change is a Command in that Branch's own History, and that the selected Branch is rebuilt as one continuous inheritance path. D01-76 also rejects treating Shared Object History and Branch Local History as two independent histories interleaved by AI Assistant.

The exact relationship between the frozen ADR-003 shared-history wording and the later D01-76 clarification must be resolved in the Core Domain's formal domain review. Until then:

- AI Assistant must not create a third `SharedObjectHistory` authority or an independent shared-state overlay.
- AI Assistant must not guess whether an intended edit is globally shared or local to the selected Map/Branch.
- Core Domain must resolve and return the writable History target/scope for a proposed operation.
- If the user's intent does not determine the scope and the distinction changes other Maps/Branches, the assistant must ask the user before authorization.
- The target scope and resolved History reference must be included in the exact proposal content and authorization.

This is a Core Domain semantic gate, not permission for AI Assistant to choose a model.

## 4. Candidate → Command Proposal boundary

### 4.1 Candidate remains a generated result

A Candidate represents a proposed result produced by AI generation or editing. Examples include a proposed new POI geometry, a proposed boundary shape, a revised attribute set, or an analysis result.

Candidate data:

- remains outside formal map/history storage;
- is versioned by the AI Assistant Candidate subsystem;
- is validated against its exact Candidate Version;
- may be selected, rejected or retained as an alternative;
- does not itself create or modify a Core Domain Command.

### 4.2 Command Proposal is an AI-owned envelope, not a Core Domain Command

A Command Proposal records the assistant's proposed authoring action and its provenance. It is not a persisted Core Domain Command and must not claim to be one.

Conceptual contents:

- proposal identity and source Task;
- exact Candidate Set / Candidate / Candidate Version references, where applicable;
- target Map and selected Branch references;
- Core Domain-resolved writable History target/scope;
- Core Domain command type identifier and descriptor/schema version, supplied by Core Domain;
- proposed command payload in the Core Domain-defined shape;
- Historical Time and any duration/end-time fields required by that command type;
- proposed explicit ordering constraints or ordering intent, clearly distinguished from already-authoritative user constraints;
- Object Identity and Object Reservation references, where applicable;
- target-context precondition reference returned by Core Domain;
- validation/preflight results and diagnostics;
- a human-readable explanation of the intended historical effect.

The AI Assistant must not invent a generic command vocabulary or payload schema. Core Domain must provide the supported command descriptors, validation rules and submission contract. If a command type is not exposed by Core Domain, AI Assistant cannot synthesize a guessed payload and submit it.

### 4.3 Candidate operations are not formal CRUD operations

V1.14's `Create / Update / Delete / Replace Geometry` labels are not a sufficient formal application model. They may remain as Candidate editing or UI intent labels, but they must be translated into the exact Core Domain-supported Command type and payload before authorization.

In particular:

- replacing geometry does not imply replacing the formal Object or its identity;
- geometric split/merge remains distinct from Object Operations and cannot silently create/delete semantic Objects;
- a delete is an explicit historical Command, not deletion of an AI-owned row or direct removal from current World State;
- an attribute or geometry edit is not a direct overwrite of current Object state;
- object creation requires Core Domain-issued identity reservation when the domain operation requires a permanent Object ID.

### 4.4 Command Proposal generation and Core Domain validation

Recommended integration capability boundary (exact API shape remains implementation-specific):

1. Query Core Domain for command types supported in the target Map/Branch and their descriptors.
2. Ask Core Domain to resolve the target History scope and current authoring context.
3. Translate selected Candidate evidence into one or more typed Command Proposals.
4. Ask Core Domain to validate proposal structure and target compatibility without persisting Commands.
5. Ask Core Domain for a preview/impact explanation where supported.
6. Resolve ordering ambiguity and obtain human decisions where ordering can change the result.
7. Seal the exact proposal manifest and present it for human authorization.

Preflight validation is not a guarantee that full historical reconstruction will succeed. Only the Core Domain history rebuild can establish the resulting valid playback range.

## 5. Historical Time and ordering

### 5.1 Historical Time is not transaction time

The Command's Historical Time expresses when the proposed action is intended to take effect in the world. The time at which the user created, edited, approved or submitted the proposal is separate system/transaction time.

Never substitute `created_at`, `approved_at` or `submitted_at` for Historical Time.

The D01 internal-time direction is integer cumulative days plus an integer within-day offset (currently seconds), with calendar dates as display expressions. AI Assistant should carry the Core Domain canonical time representation opaquely until the physical type is confirmed.

### 5.2 Branch time boundary

For a Branch-owned Command/State Change, Core Domain must enforce:

`effective_time > parent_at`

AI Assistant should request the target Branch's fork boundary and preflight the proposed time, but Core Domain remains the final authority. A proposal at or before `parent_at` must not be silently shifted to a later time. The correct alternative may be to target an earlier fork or another History, which requires an explicit user decision.

### 5.3 Same-time ordering

D01-17~28 establish that:

- users may specify ordering;
- absent user ordering, the application may derive a deterministic default from dependencies and safe technical tie-breakers;
- if order changes the semantic result and intent is unclear, the relevant Commands must wait for user decision;
- explicit user ordering is formal data and must be preserved;
- AI Assistant must not silently rewrite or discard inherited ordering constraints.

Therefore a proposal may contain suggested ordering, but it must distinguish:

1. already-existing authoritative ordering constraints;
2. a deterministic order that is semantically result-invariant;
3. a proposed new user ordering constraint requiring explicit acceptance.

Any accepted ordering constraint that affects the result must be included in the authorized content hash. The detailed Core Domain ordering model remains a design dependency; AI Assistant must not invent a parallel ordering engine.

## 6. Target-context references and stale detection

### 6.1 No domain version entities

V1.14 fields `base_branch_revision_id` and `expected_object_state_version_id` must not be treated as Core Domain Object/Branch Version foreign keys. The Core Domain baseline explicitly excludes Object Version and Revision Chain concepts.

AI Assistant may store opaque precondition references returned by Domain Services, but these are integration tokens, not domain version entities.

### 6.2 Proposed opaque references

Conceptual names only; actual names and representation must be supplied by Core Domain:

- `TargetHistoryContextRef`: identifies the exact resolved target Map/Branch/History context and inherited path used to prepare the proposal.
- `EffectiveObjectContextRef`: identifies the target Object's effective context in that selected Branch, if a per-object check is needed.
- `ObjectIdRef`: stable formal Object identity.
- `ObjectReservationRef`: proof of a permanent Core Domain reservation for a new Object identity.
- `BranchIdRef` and `MapIdRef`: stable Core Domain identities.

These references do not imply a `Revision` table, an `ObjectVersion` table, or a Branch-local state-version entity.

### 6.3 Required stale-detection guarantee

The target context reference must be issued by Core Domain and must become invalid whenever a relevant change to the selected target's effective authoring context occurs, including inherited-history or lineage changes under the adopted Branch rules.

A per-object context reference is an additional diagnostic/precondition, not a replacement for the complete target-history context.

A checkpoint identifier, checkpoint file hash, UI cache key, global latest Object state, or locally computed revision counter is not an acceptable substitute.

At final submission, Core Domain re-resolves the target and checks the exact precondition. If stale:

- reject the entire submission;
- refresh context and revalidate;
- create a new proposal/Change Set content version;
- require new human authorization.

No automatic rebase or authorization carry-forward is allowed.

## 7. Revised end-to-end application protocol

1. **Resolve intent** — establish whether the user wants a shared-scope or Map/Branch-local historical effect when that distinction applies.
2. **Resolve target** — Core Domain resolves Map, selected Branch, writable History scope, inheritance path and target context.
3. **Generate** — AI Assistant creates Candidate Sets and immutable Candidate Versions outside formal History.
4. **Validate Candidate** — validators check geometry, identity, domain and semantic constraints against the exact Candidate Version.
5. **Select** — finalize an immutable Selection Snapshot. Selection means “use this candidate as proposal input”; it does not mean “write to the map.”
6. **Translate** — generate typed Command Proposals using Core Domain-provided command descriptors and payload contracts.
7. **Preflight** — Core Domain validates command shape, target compatibility, time boundary and available preconditions. This does not promise successful full reconstruction.
8. **Resolve ordering** — use existing constraints and safe deterministic ordering; ask the user when different orders produce different semantic outcomes.
9. **Seal** — create an immutable Change Set content snapshot containing exact target scope, typed Command Proposals, Historical Time, accepted ordering constraints, Candidate provenance, asset references and context preconditions. Compute a canonical content hash.
10. **Authorize** — a human explicitly approves the exact sealed content, target History scope, time and ordering semantics. AI/system actors cannot authorize.
11. **Stage assets** — ensure every referenced asset is durable and hash-verified before the final write transaction.
12. **Final recheck and submit** — Domain Services recheck authorization, content hash, target context, Object reservations and command constraints inside the final transaction, then persist all accepted Commands and required ordering data atomically.
13. **Record outcome** — record exact Core Domain Command identifiers/references and the commit outcome. No partial Change Set submission is allowed.
14. **Rebuild separately** — historical reconstruction follows the Core Domain authoring/rebuild rules. In accordance with D01-62, normal post-edit rebuild is user-triggered; a failed-history repair flow follows D01-97. Command persistence must not be mislabeled as successful World State reconstruction.

## 8. Command submission and rebuild are separate outcomes

A critical distinction:

- **Submitted/Persisted** means Core Domain accepted the Commands into the authoritative History.
- **Rebuild successful** means the selected history has been successfully reconstructed under the current Commands.
- **Playback available** means the timeline can be played through the last successfully rebuilt time.

These are not interchangeable.

D01-58/59 define Fail-Fast reconstruction: the first unexecutable Command stops subsequent reconstruction; later Commands remain stored and are not automatically deleted. D01-62/63 define the normal user-triggered rebuild flow after editing. D01-96~100 define rebuild locking and restricted repair permissions.

Therefore:

- Do not require every Command Proposal to prove the entire future timeline will rebuild successfully before it can be authored, unless Core Domain's own command-authoring rules explicitly require that.
- Do not report a successful Command submission as a successful rebuild.
- If the subsequent rebuild fails, surface the Core Domain's first failure diagnostic; do not modify or remove the author's Commands automatically.
- Do not run a rebuild implicitly merely because an AI Task completed. The user-facing Apply action and the history rebuild action remain distinct unless the user explicitly requests both and Core Domain permits that sequence.

## 9. Editing, undo and recovery boundaries

### 9.1 Task Undo

Task-level Undo applies to reversible AI Assistant staging work: temporary assets, in-progress generation records and other AI-owned operations. It must not delete or rewrite a Core Domain Command that has already been committed.

A later failure in the AI Task does not roll back a separately committed formal Command submission.

### 9.2 Editing a submitted Command

After submission, any edit/delete of a Command is a Core Domain authoring operation under the Core Domain's own rules. It may invalidate derived State Changes, World State results and affected descendant Checkpoints. AI Assistant must request the Core Domain operation and report its result; it must not directly edit Command storage.

### 9.3 No automatic compensating Command

V1.14's generic “reverse Change Set” concept must not be treated as the default Undo mechanism. Because Core Domain Commands are author-editable and may be deleted or modified, reversal semantics must follow the actual Core Domain authoring model. AI Assistant must not invent a compensating Command or append an opposite operation unless the user explicitly asks and Core Domain defines that as a valid domain action.

### 9.4 Uncertain submission outcome

Never blindly replay a submission whose commit outcome is uncertain. Reconcile against authoritative Core Domain History and the exact submitted proposal identifiers. If certainty cannot be established, mark the attempt `recovery_required` and block duplicate submission for the affected scope until reconciled.

## 10. Transaction and asset boundary

The final submission must be atomic: all Commands and associated ordering data in the authorized Change Set are persisted, or none are.

The exact placement of AI Assistant application metadata remains open until the actual Core Domain persistence implementation is inspected. Preferred V1 arrangement is one local transactional resource and one transaction-aware connection. Do not simulate atomicity with two independent commits, best-effort sequencing, a message broker, outbox or distributed transaction framework.

Before the final transaction:

- stage generated assets;
- validate and hash-check assets;
- ensure asset references are durable.

Inside the short write transaction:

- recheck authorization and content hash;
- recheck target context and reservations;
- persist all Commands and accepted ordering data;
- persist the minimum submission outcome evidence atomically where possible.

No LLM inference, network request, long geometry generation or asset staging may run inside the write transaction.

## 11. V1.14 schema impact

V1.14 remains a historical prototype. The following semantic changes are required before a new schema can be approved:

| V1.14 element | V0.2 disposition |
|---|---|
| `tasks`, plans, steps, tool calls, operations, logs, interventions | Retain subject to normal consistency review |
| `candidate_sets`, `object_candidates`, `object_candidate_versions`, edit records | Retain; candidates remain non-formal proposal inputs |
| Validation reports/issues/items | Retain; bind evidence to exact Candidate Versions |
| Selection snapshots/items | Retain; selection is proposal-input selection only |
| `change_sets` | Retain conceptually as an immutable submission package; rename only if it materially improves clarity |
| `change_set_versions` | Retain as AI-owned immutable proposal-content snapshots, not Core Domain revisions |
| `change_items` | Redesign as typed Command Proposal items; remove CRUD-as-authority semantics |
| `change_authorizations` | Retain; bind to exact proposal content hash, resolved History target, Historical Time and accepted ordering constraints |
| `application_reports` | Redesign as submission-attempt evidence; distinguish command persistence from rebuild result |
| `base_branch_revision_id` | Remove; replace with opaque `target_history_context_ref` or Core Domain's exact equivalent |
| `expected_object_state_version_id` | Remove; replace only if Core Domain supplies an opaque Branch-effective object context reference |
| `target_branch_id` | Retain as an opaque Core Domain Branch reference; actual FK mapping pending |
| `object_id` and reservation fields | Retain as opaque Core Domain identity/reservation references; actual FK mapping pending |

No AI-owned `commands`, `branch_revisions`, `object_versions`, `branch_object_states`, `shared_object_histories` or `checkpoints` shadow tables are permitted.

## 12. Required Core Domain integration capabilities (conceptual)

The Core Domain integration boundary should provide the following capabilities. These are responsibilities, not a prescribed REST/API shape:

1. Resolve selected Map/Branch and writable History target.
2. Return the current target-history context reference and Branch fork boundary.
3. Describe supported Command types and payload contracts.
4. Validate a proposed Command without persisting it.
5. Preview intended effects/diagnostics where available.
6. Validate and persist an authorized batch of Commands atomically.
7. Return exact persisted Command references and commit outcome.
8. Expose current reconstruction status, first failure diagnostic and valid playback boundary.
9. Expose Checkpoint/path validity only through Core Domain-owned mechanisms.

If a capability does not exist, it is an integration design gap; AI Assistant must not emulate it by reading/writing Core Domain tables directly.

## 13. Open integration gates

### Gate A — Shared Object semantic formalization

Resolve the precise relationship between frozen ADR-003 shared-history scope and D01-70/76 selected-inheritance-path reconstruction. AI Assistant remains scope-agnostic and requires Core Domain to return the correct History target.

### Gate B — Command descriptor and persistence contract

Inspect the actual Core Domain Command model and determine its supported types, payload schema, dependencies, ordering representation, validation boundary and persistence operation. Do not invent these from the AI Assistant layer.

### Gate C — Core Domain physical schema and migrations

Inspect actual migrations/schema for Map, Branch, History, Command, Object identity/reservation, assets and Checkpoint storage. The currently retrieved Library materials establish domain architecture and discussion records; they have not yet established the actual physical migration set.

### Gate D — Transaction ownership

Prove that Domain Services can atomically persist the entire authorized Command batch and the required application evidence in the chosen local persistence resource. If not, resolve the architecture before implementation; do not claim atomicity.

### Gate E — Historical Time and ordering representation

Confirm the canonical internal time type, precision, duration representation and explicit ordering-constraint schema from Core Domain. The D01 decisions constrain semantics but do not supply the final physical schema.

## 14. Explicit prohibitions

1. No direct AI/LLM writes to Core Domain tables.
2. No CRUD-style direct mutation of formal Object state.
3. No AI-owned duplicate History, Command, Branch, Object, World State or Checkpoint authority.
4. No Object Version / Branch Revision / Revision Chain domain entities introduced to support Change Set staleness.
5. No checkpoint-derived or UI-derived stale token.
6. No silent conversion of Branch-local changes into shared/global changes.
7. No AI auto-rewrite, auto-delete or auto-repair of author Commands.
8. No automatic Command ordering where order changes semantic results and user intent is unknown.
9. No automatic Task Undo across an already committed Core Domain submission.
10. No generic workflow engine, DAG, distributed lock, message broker, event-sourcing framework or distributed transaction system for this single-user local-first V1.0.

## 15. Current conclusion

The AI Assistant integration model is now Command-oriented rather than CRUD-oriented. Candidate generation and validation remain AI Assistant responsibilities; typed Command Proposal preparation is an AI-owned staging step; Core Domain alone defines, validates, persists and reconstructs formal history.

V1.14 must not enter development unchanged. The Task/Planning/Tool/Candidate/Validation/Selection foundations remain candidates for reuse. The post-Selection submission model and its schema require redesign. Physical schema, exact command payloads, Shared Object scope formalization and transaction proof remain explicit design gates.