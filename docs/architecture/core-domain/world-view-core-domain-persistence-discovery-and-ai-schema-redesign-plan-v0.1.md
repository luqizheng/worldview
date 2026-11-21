# World View — Core Domain Persistence Discovery and AI Assistant Schema Redesign Plan

- Version: 0.1
- Date: 2026-11-21
- Status: Design audit and recommended next-step baseline
- Scope: AI Assistant Task Planning & Execution Engine V1.0 integration with World View Core Domain
- Storage target: Single-user, local-first desktop application

## 1. Executive conclusion

The current project Library contains a substantial **Core Domain architecture and domain-model design**, but the inspected materials do not contain an actual Core Domain physical database schema or migration set.

This is not simply a missed filename. The upstream design documents explicitly separate Domain Model from Persistence:
- `phase2-p2-01-domain-model-proposal-v0.1.md` says its scope is domain semantics only and explicitly excludes database design, API design, UI and code migration.
- The same document identifies P2-08 Shared/Local History and P2-09 Persistence as outside its scope.
- `architecture-baseline-v1.0.md` §35 states the storage capabilities that must be supported, but does not define physical tables, keys, SQL DDL or migrations.
- `phase2-domain-model-historical-state-engine-v1.0.md` defines the domain and reconstruction model, not a physical persistence implementation.

Therefore, there is no verified Core Domain migration set to map against at this stage. We must not invent foreign keys or claim to have inspected physical Core Domain tables.

**Recommendation:** do not produce another executable AI Assistant schema that pretends Core Domain keys and transaction ownership are known. Advance the AI schema to a V1.15 *logical redesign* now, then produce the final integrated SQLite DDL/migration package only after the Core Domain P2-09 persistence design and transaction boundary have been specified.

## 2. Materials inspected

### 2.1 `/world-view-rs`

Inspected Library inventory and relevant documents:
- `architecture-baseline-v1.0.md`
- `phase2-domain-model-historical-state-engine-v1.0.md`
- `phase2-p2-01-domain-model-proposal-v0.1.md`
- `world-view-p2-01-architecture-conflict-audit-v0.1.md`
- `adr/` and the accepted ADR-001~005 records
- `P2-01-D01/` discussion records, including D01-01~143

The `/world-view-rs` Library currently exposes architecture, domain-model, ADR and discussion documents. The inspected directory inventory did not expose Core Domain `.sql` files, migration scripts, a database project, or a physical schema specification.

### 2.2 `/World View/Map Generation`

Inspected the AI Assistant design package, including:
- AI Assistant product/architecture and task lifecycle documents;
- Task Execution, Candidate/Validation and Selection/Application schema drafts;
- unified SQLite schema V1.14 and its SQL/test prototype;
- Core Domain Integration Contract V0.2;
- Command Proposal and Authorization Design V0.1;
- Ordering and Historical Time Integration Contract V0.1;
- Command Edit and Reconstruction Lifecycle V0.1;
- README V1.14 and README V1.15.

This directory contains the AI Assistant's own SQLite prototype. It is not the Core Domain database schema.

### 2.3 What is known versus unknown

| Area | Evidence status | Conclusion |
|---|---|---|
| Core Domain Map / Branch / History semantics | Documented | Domain model and frozen ADRs exist |
| Command / State Change / World State semantics | Documented, with D01 discussion status caveat | Authoritative concepts are known; D01-01~143 are not formally frozen as a whole |
| Branch inheritance | Frozen ADR rules plus later D01 discussion | AI must not implement Branch semantics; formal reconciliation remains a Core Domain owner task |
| Object identity concept | Documented | Stable Object identity; no Object Version entity |
| Object reservation physical mechanism | Not found | No table/key can be assumed |
| Core Domain physical schema | Not found in inspected Library inventory | P2-09 persistence design remains a dependency |
| Core Domain migration history | Not found | No actual migration number/order can be mapped |
| Core Domain database provider/runtime | Not confirmed by inspected Core Domain docs | SQLite is the AI prototype target, not proof of Core Domain provider selection |
| Core Domain transaction ownership | Not documented | Atomic AI/Core Domain submission remains unproven |
| Command descriptor catalog and payload schema | Not found as physical/implementation contract | Must be supplied by Core Domain |
| Checkpoint storage schema | Not found | Only domain/discussion semantics are available |

## 3. Core Domain architecture implications for persistence

### 3.1 Do not persist computed World State as authoritative state

The domain model treats ObjectState and World State as computed results of Initial State plus applicable history. The storage model must preserve the authoritative source data needed for deterministic reconstruction, not turn every computed state into an independent Object Version or World State Version.

Checkpoint is a derived cache and remains outside formal history authority.

### 3.2 Preserve History as the common Map/Branch history concept

The domain model describes Map root History and Branch-owned History as the same kind of entity. A Branch references parent History plus `parent_at` and owns its own History. Physical persistence must preserve that model without copying ancestor Commands or creating a second Branch-local version authority.

The exact storage layout and shared-history scope must be derived from accepted ADRs and the final D01 formal review, not guessed from an AI schema.

### 3.3 Keep Command and State Change distinct

The later D01 discussion describes Command as the author's history-authoring action and State Change as the state-change meaning produced/represented by Commands. A Command may produce zero, one or multiple State Changes.

The final persistence design must reconcile this with the earlier domain-model proposal's History → StateChange model. Do not collapse Command and State Change into one table simply because an AI proposal is called a Command Proposal.

### 3.4 No version-chain workaround

The following must not be introduced to support AI stale detection:
- Object Version;
- World State Version;
- Branch Revision Chain;
- Command Revision Chain;
- AI-owned Checkpoint versions.

Use opaque Core Domain context/precondition references at the integration boundary until the final physical keys and freshness semantics are defined.

## 4. Recommended persistence topology

For the single-user, local-first desktop target, the recommended direction is:

**One SQLite database file, with clear ownership boundaries and one transaction-aware persistence boundary for formal application.**

- Core Domain tables and AI Assistant tables remain separate in ownership and naming.
- Domain Services remain the only writers of Core Domain formal tables.
- AI Assistant repositories write only AI-owned tables.
- The final authorized submission is coordinated by an application/domain-service transaction boundary that can atomically persist the Core Domain Commands and the minimum AI submission evidence.
- Long-running AI inference, generation, validation and asset staging happen outside the short write transaction.
- No attached second database, cross-database best-effort sequence, message broker, outbox, distributed transaction or event-sourcing framework is introduced for V1.0.

This is a recommended target, not a verified current implementation fact. It must be proven against the actual Core Domain storage implementation before it becomes an accepted implementation baseline.

If the Core Domain later proves to use a separate persistence file or cannot share a transaction-aware connection, the architecture must be revisited. We must not call two independent commits atomic.

## 5. V1.15 AI Assistant logical schema redesign

V1.15 should be a logical schema redesign and consistency baseline, not a final executable migration package.

### 5.1 Reuse without unnecessary redesign

Retain the established groups, subject to normal integrity review:
- Task execution: Tasks, contract versions, plan versions, steps, tool calls, operations, logs, human interventions and delivery records.
- Candidate/validation: Candidate Sets, Candidates, immutable Candidate Versions, edit records, validation reports, report items and issues.
- Selection: immutable Selection Snapshots and exact Selection Items.

Candidate and validation evidence remain AI-owned and do not become formal Core Domain state.

### 5.2 Replace CRUD application items with typed Command Proposal items

V1.14's CRUD-oriented `change_items` is not a valid final Core Domain integration model.

Redesign it conceptually as `command_proposal_items`, each containing:
- proposal item identity and stable order within the sealed manifest;
- Core Domain-supplied Command type identifier;
- descriptor/schema version;
- payload in the Core Domain-defined shape;
- exact Candidate/Candidate Version provenance, where applicable;
- resolved History target/scope reference;
- Historical Time and precision using the Core Domain canonical contract once defined;
- Object identity/reservation references, where applicable;
- asset references and hashes;
- preflight result/diagnostic references.

Do not define a generic AI-owned Command vocabulary or payload schema. Unsupported Core Domain Command types must be rejected rather than guessed.

### 5.3 Preserve immutable proposal versions

Retain `change_sets` and `change_set_versions` conceptually:
- a Change Set is the submission package;
- each sealed version is an immutable proposal snapshot;
- the canonical hash covers every semantic field, including target scope, typed proposal payloads, time, accepted ordering intent, reservations and asset content references;
- display-only fields are excluded from the semantic hash;
- editing after sealing creates a new proposal version and requires new validation and authorization.

These are AI-owned proposal versions, not Core Domain Command revisions.

### 5.4 Authorization remains exact and immutable

Retain `change_authorizations` conceptually, but bind authorization to:
- exact Change Set version;
- exact canonical content hash;
- exact resolved target History/Branch context;
- exact accepted ordering/time semantics;
- human authorizer and transaction-time evidence.

Any semantic change or stale target context makes the old authorization unusable for submission. Keep it as historical evidence; do not delete or rewrite it.

### 5.5 Split submission attempt evidence from current reconstruction state

Redesign the current `application_reports` semantics.

Recommended logical records:
1. `submission_attempts` — one row per attempt, stable idempotency identity, exact authorized proposal reference/hash, attempt lifecycle and commit/reconciliation outcome.
2. `submission_command_refs` — exact Core Domain Command references returned for a successful submission.
3. `application_reports` — immutable report of what that attempt persisted/rejected/failed to establish.

Do not store `result_branch_revision_id` as if it were a known Core Domain key. Replace it with returned opaque Core Domain references after the physical contract exists.

Do not mutate an old Application Report when the user later edits a submitted Command. The report is historical evidence of the earlier submission.

Current reconstruction status and playback boundary should be queried from Core Domain. If a local cache is later justified, it must be explicitly a refreshable, non-authoritative read model with source timestamp/context; it must never become the source of truth.

### 5.6 Remove unsupported physical references

Remove or defer the following V1.14 assumptions:
- `base_branch_revision_id`;
- `expected_object_state_version_id`;
- `result_branch_revision_id` as a concrete FK;
- any guessed Object reservation FK;
- any guessed Branch/History/Command FK.

Use opaque text references at the logical integration layer until Core Domain defines stable identifiers and freshness semantics. Add physical foreign keys only when both schemas are in the same database and the exact target keys are known.

### 5.7 Ordering proposal data

AI may persist proposed ordering relations as part of the immutable proposal manifest, including:
- relation endpoints;
- direction;
- provenance;
- user instruction/acceptance evidence;
- Core Domain context references.

AI must not persist an authoritative dependency graph or copy inherited ordering constraints as AI-owned formal data. Existing Core Domain constraints are queried read-only and are represented in the proposal only as precondition/context evidence where required.

## 6. V1.14 disposition

| V1.14 component | V1.15 logical disposition |
|---|---|
| Task/Plan/Step/Tool/Operation/Log/Intervention | Retain, subject to lifecycle review |
| Task Delivery Records | Retain; exact delivered Candidate Version manifest remains immutable |
| Candidate Sets/Candidates/Candidate Versions/Edit Records | Retain |
| Validation Reports/Items/Issues | Retain |
| Selection Snapshots/Items | Retain |
| `change_sets` | Retain conceptually |
| `change_set_versions` | Retain as immutable AI proposal snapshots |
| `change_items` | Replace with typed Command Proposal items |
| `change_authorizations` | Retain, strengthen exact target-context binding |
| `application_reports` | Redesign around immutable submission-attempt evidence |
| `base_branch_revision_id` | Remove as assumed domain version FK |
| `expected_object_state_version_id` | Remove as assumed Object Version FK |
| `result_branch_revision_id` | Replace with opaque returned Core Domain refs until actual key known |
| AI-owned `commands` / `checkpoints` / `branch_revisions` / `object_versions` | Prohibited |

## 7. Migration strategy

Do not modify the V1.14 prototype in place or label its bootstrap SQL an upgrade migration.

Recommended sequence after the logical model is reviewed:

1. Freeze the V1.15 logical schema and table/ownership contracts.
2. Complete Core Domain P2-09 physical persistence design and formal D01 review.
3. Confirm SQLite provider, connection lifetime and transaction ownership.
4. Produce one integrated initial database bootstrap for a new database, with separate Core and AI migration namespaces/IDs or a clearly coordinated migration ledger.
5. Define explicit incremental migrations for any existing V1.14 prototype databases only if they are expected to contain user data. Otherwise document V1.14 as a disposable design prototype and start new databases from the integrated bootstrap.
6. Test migration from empty database and any supported prior version.
7. Test process termination before/after each commit boundary.
8. Test submission reconciliation, Command edit/delete, rebuild failure, Branch invalidation and Checkpoint recovery against the real Domain Services.

The final migration ordering must respect Core Domain dependencies: Core Domain stable identity/History/Branch keys must exist before AI references them.

## 8. Required Core Domain P2-09 deliverables

The Core Domain persistence design must define, at minimum:

1. Map and Object identity tables and key types.
2. Object ID reservation lifecycle and uniqueness constraints.
3. History and Branch persistence, including `parent_history`, `parent_at` and accepted inheritance semantics.
4. Command persistence, supported Command descriptors and payload validation.
5. Explicit ordering relation persistence and derived dependency handling.
6. Event and State Change persistence/derivation boundary.
7. Historical Time and calendar representation.
8. Initial State persistence.
9. Geometry/attribute/relationship storage and asset references.
10. Checkpoint storage, lineage/version validation, integrity metadata, atomic publication and reference protection.
11. Domain Services transaction ownership and repository connection sharing.
12. Migration ledger/versioning and database upgrade policy.

These are not instructions to build an elaborate persistence platform. The implementation should remain a straightforward, maintainable local SQLite design.

## 9. Decision gates and next sequence

| Gate | Required evidence | Status |
|---|---|---|
| G1 — Physical Core Domain schema | P2-09 design plus actual DDL/migrations | Open; not present in inspected Library |
| G2 — Branch/Shared Object semantics | Formal reconciliation of ADR-003 with D01-70/76 and frozen ADR rules | Open |
| G3 — Command contract | Command catalog, payload schema, authoring and persistence operations | Open |
| G4 — Historical Time/ordering | Canonical time and ordering storage contract | Open |
| G5 — Checkpoint contract | Physical representation and lifecycle transaction design | Open |
| G6 — Transaction boundary | Proof Core and AI writes can share one SQLite transaction | Open |
| G7 — AI V1.15 logical schema | Apply this document's table disposition and lifecycle rules | Ready for design review |
| G8 — Integrated executable DDL | Complete G1~G6 first | Blocked |

Recommended order:
1. Finish the Core Domain semantic integration review (especially Shared Object scope).
2. Define the Core Domain P2-09 persistence model at the minimum necessary level.
3. Confirm the one-file SQLite / shared-transaction approach against the actual application architecture.
4. Finalize AI Assistant V1.15 logical schema.
5. Produce integrated DDL and migration tests as V2.0 implementation baseline.

## 10. Current conclusion

The audit found no actual Core Domain physical migrations to inspect in the currently available `/world-view-rs` and `/World View/Map Generation` Library materials. The upstream Core Domain documents explain why: Persistence is a later phase and was explicitly excluded from the current domain-model proposal.

The next correct action is therefore not to guess Core Domain tables. It is to define the missing Core Domain persistence contract, while redesigning the AI Assistant's post-Selection data model around typed Command Proposals, immutable authorization evidence, idempotent submission attempts and Core Domain-owned reconstruction status.

V1.14 remains a historical prototype. V1.15 should be the corrected logical schema baseline. V2.0 should be the first integrated executable SQLite schema and migration package after the Core Domain persistence and transaction gates are closed.