# World View Core Domain — P2-09-03 Command / Event / State Change Persistence Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended logical contract; physical schema and command catalog remain open
- Depends on: ADR-001/002/003/005; P2-01 Domain Model Proposal; D01-01~143; P2-08 Shared Object / Map-local Event Semantics Resolution V0.2; P2-09-01 and P2-09-02.

## 1. Purpose

Define how authoring Commands, narrative Events, and executable State Changes relate in persistence without collapsing them into one record type or creating competing authorities.

This is a logical persistence contract. It does not prescribe SQL tables, columns, keys, payload JSON, or a final Command catalog.

## 2. Core decision

Use three distinct concepts:

- **Command** — the authoritative, author-editable record of an action that adds or changes formal history.
- **Event** — an optional narrative/historical occurrence that may explain a group of changes. It does not directly mutate World State.
- **State Change** — the typed, executable state-change meaning represented or produced by a Command. It is not an independent authoring ledger.

World State is reconstructed by applying the applicable State Changes in deterministic order. Commands remain the authority for what the author recorded; State Changes are the executable interpretation of those Commands.

## 3. Ownership and persistence roles

| Concept | Owner | Persistence role | Mutable? |
|---|---|---|---|
| Command | Core Domain | Authoritative formal history record | Yes, under Core Domain authoring rules; may be edited, deleted or inserted |
| Event | Core Domain | Authoritative narrative record associated with a History | Yes, under Core Domain authoring rules |
| State Change | Core Domain | Derived/executable history representation attributable to Commands | Rebuildable when source Commands change; not independently authored as a second ledger |
| World State | Core Domain | Derived reconstruction result | No direct persistence as authority |
| Reconstruction diagnostics | Core Domain | Derived diagnostics for a specific rebuild attempt | Retained as operational evidence according to later diagnostics design |

The logical model may persist State Change records for efficient reconstruction, indexing, Event association and inspection. If persisted, they are materialized derived records, not a second source of truth. Their provenance and rebuild lifecycle must be explicit.

## 4. Cardinality and association rules

### 4.1 Command to State Change

- One Command may produce zero, one, or many State Changes.
- A Command that is narrative-only or has no runtime effect may produce zero State Changes.
- A complex domain operation may produce several State Changes.
- Do not assume one Command equals one State Change.
- Recommended V1 provenance rule: every materialized State Change is attributable to exactly one producing Command. A Command can therefore have 0..N State Changes.
- If a future domain operation genuinely requires one State Change to be jointly produced by multiple Commands, introduce an explicit provenance model only after a concrete use case; do not pre-emptively add many-to-many complexity.

### 4.2 Event to State Change

Preserve the P2-01 relationship:

- An Event may be associated with zero, one, or many State Changes.
- A State Change may have no Event.
- Recommended V1 relationship: each State Change has zero or one explanatory Event; one Event may group many State Changes.
- Event association is explanatory/grouping metadata. It does not change State Change execution semantics or ordering authority.
- An Event with zero State Changes is valid narrative history.

### 4.3 Command to Event

Do not require a direct Command-to-Event relationship for all cases. The Command's generated State Changes may carry the optional Event association. If a Command creates an Event but no State Change, the Command may reference that Event directly as its narrative result. The final descriptor contract must define the exact shape per Command type.

## 5. History ownership and scope

- Commands and Events belong to one Core Domain History scope.
- State Changes inherit the History scope of their producing Command.
- Map root History and Branch-owned History remain the same History entity type.
- Branches do not copy ancestor Commands, Events or State Changes.
- A Branch references its parent History and `parent_at`; its own Commands/Events/State Changes are local to its own History.
- Ancestor history at or before the closed fork boundary is inherited; Branch-owned State Changes must satisfy `effective_time > parent_at`.
- The selected Map/Branch reconstruction projection is composed by Core Domain along one applicable inheritance path.

No AI-owned or separately replayed Shared Object History is introduced. Under P2-08 V0.2, shared definition/base data is a shared reconstruction input; post-zero Event-driven runtime changes belong to the currently active Map/Branch History.

## 6. Event semantics

An Event is a historical occurrence/narrative record. It may contain or reference:

- stable Event identity;
- owning History;
- Historical Time;
- title and description;
- participants and relevant Object references;
- optional related State Changes.

These are conceptual attributes, not a final field list.

Rules:

1. Event does not directly mutate World State.
2. Event may have no State Changes.
3. Event may explain multiple State Changes.
4. State Changes may exist without an Event.
5. A post-zero Event belongs to the currently active Map/Branch History; another inactive Map does not execute it.
6. Event targetting a Shared Object does not turn its runtime effect into a globally propagated Shared Object definition edit.
7. Event Historical Time and State Change effective time may be related by a Command descriptor, but equality must not be assumed for every future Command type without an explicit contract.

## 7. State Change semantics

A State Change is the smallest executable semantic unit used by reconstruction. It should describe a domain operation, not a generic row-level database update.

Conceptual information includes:

- stable identity within the owning History;
- producing Command provenance;
- optional Event association;
- target Object identity or other domain target;
- typed Change Type;
- typed payload;
- canonical effective Historical Time;
- deterministic ordering information;
- any domain-defined duration or dependency information.

A State Change is not an Object Version, World State Version, or snapshot of the entire object. It must not duplicate complete mutable World State unless a specific Change Type payload semantically requires a geometry/value replacement.

## 8. Command lifecycle and edit behavior

Commands are author-editable history, not immutable version entities.

- Creating a Command adds an authoring action to its History.
- Editing a Command changes the authoritative Command record under Core Domain rules.
- Deleting a Command removes that authoring action under Core Domain rules.
- Inserting a Command changes the History ordering/content under Core Domain rules.
- No Command Revision Chain is introduced.
- Each edit/delete/insert invalidates or regenerates the affected derived State Changes and reconstruction caches as required.
- Existing AI proposal snapshots, hashes, human authorization evidence and submission attempt evidence remain immutable and are not rewritten when a submitted Command is later edited.

A Command's database identity should remain stable across ordinary in-place edits if the Core Domain authoring contract permits it. Do not add revision IDs merely to support AI stale detection.

## 9. Persisted State Change materialization and rebuild

Recommended V1 approach:

1. Persist the Command as the authoritative authoring record.
2. Validate the Command against its registered descriptor and target History context.
3. Interpret/compile the Command into zero or more typed State Changes.
4. Persist the resulting State Change materialization with provenance to the producing Command and the source Command content/context used to generate it.
5. Reconstruction consumes only a complete, current State Change projection for the selected History path.
6. When a Command is edited, deleted or inserted, Core Domain invalidates affected State Change materializations and dependent Checkpoints, then regenerates the affected projection through its normal rebuild lifecycle.

Important constraints:

- Materialization publication must be atomic: consumers must never see a partially regenerated set as current.
- A failed rebuild must preserve authoritative Commands and diagnostics; it must not silently delete later Commands.
- Fail-Fast behavior remains: reconstruction stops at the first unexecutable Command/State Change boundary; later history remains persisted but is not treated as successfully reconstructed.
- A stale/incomplete State Change materialization must never be mistaken for current authoritative output.
- The precise fingerprint/generation token is Core Domain-owned; do not invent Branch Revision or Command Revision entities.

This approach avoids reinterpreting every Command from scratch for every playback while keeping Commands as the only authoring authority. If implementation evidence shows that persisting State Changes adds more complexity than value, a pure derived in-memory projection is acceptable, provided Event association, diagnostics and deterministic reconstruction remain correct. The logical authority rules do not change.

## 10. Ordering and Historical Time

- Command transaction time and Historical Time are distinct.
- Command creation/edit timestamps must never substitute for effective Historical Time.
- State Changes are ordered by Core Domain's canonical Historical Time plus its deterministic same-time ordering contract.
- Event display order does not automatically define State Change execution order.
- Event association does not override explicit ordering constraints.
- A Branch-owned State Change with `effective_time <= parent_at` is rejected; do not silently move it.
- Exact time encoding, sequence allocation, user ordering and dependency persistence are handled by the dedicated Historical Time / Ordering design item.

## 11. Validation boundaries

Separate structural validity from historical quality warnings:

- Command descriptor and payload structure are validated before persistence.
- Invalid target references or malformed required payloads are rejected according to Core Domain rules.
- Historical anomalies explicitly classified by ADR-003 A10-05~09 may remain as warnings and must not be silently repaired or discarded.
- A deleted Object followed by a later local property modification retains A10-09 semantics: the command remains executable as defined by Core Domain, it does not resurrect the Object, and a persistent quality warning remains while the original condition exists.
- A State Change's successful execution does not automatically clear a history-quality warning.

## 12. Initial State boundary

Time zero is the Map's first timeline point. Initial State is the starting reconstruction input, not a post-zero Event.

- Initial State data must be persisted as authoritative reconstruction input.
- Editing time-zero Initial State must use a dedicated Core Domain authoring operation and validation path.
- Do not disguise a time-zero baseline edit as a post-zero Event.
- Do not assume every Initial State edit must be represented by an ordinary Command/State Change pair; the exact baseline-authoring model is a separate bounded decision.
- Shared Object definition/base data remains common across referencing Maps; Map-specific initial-state data remains scoped to the Map.

## 13. Minimum verification scenarios

1. One Command produces zero State Changes and persists successfully.
2. One Command produces exactly one State Change.
3. One Command produces multiple State Changes, all with the same producing Command provenance.
4. One Event groups multiple State Changes.
5. An Event with zero State Changes is retained as valid narrative history.
6. A State Change without an Event is reconstructed correctly.
7. Editing a Command invalidates/rebuilds its affected State Change projection without a Command Revision Chain.
8. Deleting a Command does not leave its old State Changes executable.
9. Inserting a Command regenerates deterministic ordering and affected projection.
10. Failed rebuild preserves Commands and later history while reporting the first failure boundary.
11. A Branch inherits ancestor Event/State Change through the fork boundary without copying them.
12. A Branch-owned State Change at or before `parent_at` is rejected.
13. A post-zero Event in Map A does not change Map B's runtime state even when both reference the same Shared Object.
14. A Shared Object definition/base-data change is visible as shared data to all referencing Maps without being replayed as a Map A Event.
15. A10-09 delete-then-modify behavior is preserved without object resurrection or automatic warning removal.
16. AI Assistant cannot directly insert, edit or delete Commands, Events or State Changes.

## 14. Open items before physical DDL

1. Final Command type catalog, payload schemas and descriptor compatibility/versioning.
2. Whether State Change materialization is stored durably or generated as a rebuildable in-memory projection; recommended persisted materialization must be benchmarked against implementation simplicity.
3. Exact Event/Command link representation for narrative-only Commands.
4. Exact Initial State authoring model.
5. Canonical Historical Time and same-time ordering representation.
6. Shared definition edit history/as-of semantics and Checkpoint invalidation behavior.
7. Actual Core Domain key types and existing implementation/migration evidence.

## 15. Status

This contract establishes a recommended logical boundary: **Command is authoritative authoring history; Event is optional narrative history; State Change is a derived executable representation; World State is reconstructed output.**

It is ready to guide subsequent Object ID Reservation and Historical Time/Ordering persistence design. It is not a physical schema, frozen command catalog, or implementation authorization.