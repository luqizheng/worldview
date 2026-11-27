# FOUNDATION-01 — Core Domain Source Audit and Gap Matrix

- Version: 0.1
- Date: 2026-11-27
- Baseline: `master` after PR #48 (accepted decision baseline)
- Status: Source-audit report; implementation authorization is limited to the decisions already accepted in ARCH-01
- Scope: Map creation, Spatial Reference ownership, Object identity/registration, Initial State, Geometry Asset publication, SQLite transaction boundaries
- Execution: Single-machine, single-user desktop application; no distributed or multi-user concurrency assumptions

## 1. Executive result

FOUNDATION-01 is required before the first general-purpose geometry-authoring vertical slice. The current source contains reusable Core Domain and SQLite foundations, but the accepted ARCH-01 decisions are not implemented end-to-end.

The most important gaps are:

1. A newly created Map is persisted with `SpatialReferenceId = null`; the Map creation transaction does not create and bind the accepted default Local Cartesian Spatial Reference.
2. Initial State is represented as one mutable baseline plus membership rows. Its edit operation is CAS-protected replacement, not an immutable revision chain with a durable active cursor.
3. Initial State membership has no formal integration with independent immutable Geometry Asset references. The existing Building/Cave publication path is specialized and cannot be treated as the generic POI/River/Landmass authoring path.
4. No Core Domain-owned deterministic Undo/Redo contract exists for time-zero edits. UI-local undo would violate the accepted decision.

This report does not claim that all related code has been exhaustively enumerated. It records verified entry points and the architectural implications, and identifies the remaining implementation discovery required before schema changes.

## 2. Binding product decisions

The following are accepted and binding from `docs/architecture/ARCH-01-decisions-accepted-v1.0.md`:

- Initial object types: POI, River and Landmass, limited to their required geometry roles.
- Default Map Spatial Reference: immutable 2D Local Cartesian, XY, abstract `world-unit`, no wrap, no vertical reference, Map-local origin. It is not WGS84, metres, feet or screen pixels.
- Time-zero edits: Core Domain-owned durable baseline revisions and deterministic Undo/Redo; immutable persisted revisions; stable identity is never recycled; active revision/cursor survives close/reopen; stale revisions are rejected; publication is all-or-nothing.
- Layer/group membership, order, visibility, expanded/collapsed and active layer are Workspace-local presentation state, not Core Domain semantic layer tables.

Precision profile, tolerances and valid extent remain open for DESIGN-EXTENT / PRA-03. FOUNDATION-01 must not invent those values.

## 3. Verified source findings

### 3.1 Map creation and Spatial Reference

- `WorldView.Domain.Maps.MapService.CreateMap` verifies Project existence and name uniqueness, creates a `MapCreationPlan`, then calls `ICoreDomainDatabase.CreateMapStructure(plan)`.
- `MapRecord.SpatialReferenceId` is nullable.
- The current Map creation plan initializes `SpatialReferenceId` as null.
- The current create-map path does not create a Spatial Reference definition or bind it to the new Map.
- Spatial Reference definition/registration/binding primitives exist in the Core Domain and SQLite adapter. They can be reused, but their current separate operations do not prove an atomic Map + default Spatial Reference creation boundary.
- Therefore the default must be added to the Map creation plan and persisted inside the same transaction as Map, root History, Calendar and day-zero Calendar Configuration.

Required invariant after implementation: every newly created Map has exactly one immutable default Spatial Reference binding before the create operation commits. No externally observable successful Map may have a null Spatial Reference.

### 3.2 Object identity and registration

- Existing Core Domain services and SQLite writes provide Object Type lookup, descriptor pinning, ID reservation, identity binding, definition persistence, Map registration and Initial State membership checks.
- The Object/Reservation/Registration/Initial State adapter performs compound writes in SQLite transactions and re-reads transition state inside the transaction.
- These facilities are a sound base for stable Object identity and registration.
- The new authoring path must reuse descriptor validation and identity rules; it must not create a parallel identity registry or bypass registration.

### 3.3 Initial State

- `MapInitialStateService` currently exposes compare-and-swap editing based on the expected fingerprint.
- The persistence operation rewrites the baseline/member representation; it does not append an immutable revision.
- There is no durable revision graph/cursor or Core Domain Undo/Redo operation in the current Initial State service contract.
- This is incompatible with the accepted decision and must be redesigned directly; there is no released user data requiring a compatibility layer.

### 3.4 Geometry Asset

- Geometry envelope validation exists and requires a Spatial Reference identifier, axis order, unit, coordinate precision profile identifier, dimension and geometry family.
- Geometry Assets are immutable/content-addressed and the existing file/database publication pattern separates byte publication from the short metadata transaction.
- Building/Cave has a specialized geometry publication path. It does not prove that the generic Initial State path can publish POI/River/Landmass geometry.
- The old Initial State contract explicitly said `core_map_initial_object_asset` was not enabled. The accepted geometry-authoring and durable revision decisions now require revisiting this ownership boundary.
- Asset publication must never overwrite bytes. Failed DB publication may leave an unreferenced immutable file for later safe reconciliation, but no failed operation may leave a formal membership/revision/asset link partially committed.

## 4. Gap matrix

| Capability | Existing foundation | Gap against accepted decision | Required outcome |
|---|---|---|---|
| Map create transaction | Map + History + Calendar + configuration | No default Spatial Reference in plan/transaction | Atomic create with default SR |
| Spatial Reference | Immutable definition and binding primitives | Not initialized by Map creation; profile identity not finalized in code | Built-in immutable Local Cartesian profile |
| Object identity | Descriptor, reservation, binding, registration | Must be composed with geometry/revision publication | Reuse existing authority |
| Initial State | Mutable baseline + membership + fingerprint CAS | No immutable revisions or cursor | Durable revision model |
| Undo/Redo | No formal Initial State operation | No domain-owned deterministic cursor movement | Core Domain API and persistence |
| Geometry Asset | Immutable Asset foundation and specialized publisher | No generic baseline Asset reference integration | Typed immutable asset references |
| Recovery | SQLite transaction rollback and Asset publication protocol | Revision/asset recovery behavior not unified | All-or-nothing formal publication and safe orphan handling |
| Reopen | Existing Map and Initial State reads | No active revision/cursor to restore | Restore exact active revision and cursor |

## 5. Design constraints for implementation

1. Keep all formal authority in Core Domain. UI and Workspace may request edits but may not persist formal state directly.
2. A baseline revision is an immutable record of one complete time-zero state, not a mutable snapshot overwritten in place.
3. Revision identity and Object identity are never reused. Undo/Redo moves a durable cursor; it does not delete or rewrite revision history.
4. Each revision must be reconstructible from immutable content and explicit parent/operation semantics. The implementation must choose either full immutable snapshots or a rigorously deterministic operation chain; it must not mix ambiguous partial snapshots.
5. The active cursor update and any newly created revision/membership/asset references must commit atomically.
6. Stale expected revision/cursor requests must fail closed without changing formal state.
7. Geometry bytes are published immutably before the short SQLite transaction. The transaction then verifies all preconditions and commits asset metadata/link, revision, membership and cursor as one unit.
8. No file I/O, geometry processing, user interaction or await may occur while holding the SQLite write transaction.
9. Undo/Redo must remain deterministic after close/reopen and must not depend on an in-memory UI stack.
10. Layer presentation state remains outside Core Domain.
11. No distributed locking, multi-user conflict protocol, event sourcing framework or generalized concurrency layer is warranted for this single-machine product.
12. Existing unused tables and migrations may be reshaped or removed after dependency/test inspection; do not preserve them solely for compatibility.

## 6. Contract conflict and supersession

The legacy `CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md` describes a mutable single baseline and explicitly defers Initial State Asset links. Its statements that Initial State editing is a baseline/member rewrite and that no revision chain/Initial State Asset link is enabled are superseded by the accepted ARCH-01 decision.

The legacy document remains historical context for existing Object identity, registration, transaction and ownership rules. It must not be interpreted as authority to reject immutable baseline revisions or geometry Asset references.

This report does not silently rewrite the legacy contract. The new authoritative contract is published separately in `FOUNDATION-01-time-zero-baseline-contract-v1.0.md`.

## 7. Implementation sequence

### FOUNDATION-01A — Default Spatial Reference

- Define the built-in profile identity and immutable definition.
- Extend Map creation plan and persistence adapter so Map + Spatial Reference definition + binding + existing Map structure commit in one transaction.
- Ensure project-level identity uniqueness and Map ownership rules are validated within the transaction.
- Add success, rollback, duplicate/retry, isolation and reopen tests.

### FOUNDATION-01B — Durable baseline revisions

- Replace mutable baseline editing with immutable revision records and durable active cursor.
- Define revision parentage, operation semantics, Undo/Redo, stale rejection and reopen behavior.
- Rework schema directly; no historical-data migration is required.
- Add deterministic replay/reconstruction and failure-injection tests.

### FOUNDATION-01C — Geometry Asset publication

- Define typed geometry Asset references for Initial State revisions.
- Compose Object identity, Map registration, geometry Asset references, revision and cursor in one formal publication boundary.
- Implement POI/River/Landmass descriptor-specific geometry-role validation.
- Add rollback, orphan reconciliation, corruption, reopen and immutability tests.

The exact PR split may change if implementation discovery shows a tighter ownership boundary. The dependency order must not change.

## 8. Explicitly out of scope

- Precision profile numeric values, coordinate tolerances and valid extent (DESIGN-EXTENT / PRA-03).
- General-purpose geometry editing tools, rendering and hit-testing.
- Layer persistence in Core Domain.
- Command/Event timeline history for post-zero edits.
- Branch-specific baseline copies.
- PRA-05 chunk/LOD optimization.
- Multi-user, distributed or server-side concurrency.

## 9. Acceptance gate

FOUNDATION-01 is complete only when:

- New Maps always have the accepted default Spatial Reference atomically.
- The active time-zero baseline is represented by durable immutable revisions and cursor state.
- Core Domain Undo/Redo is deterministic and survives close/reopen.
- POI/River/Landmass geometry Asset publication is atomic with formal membership/revision changes.
- Stale requests, invalid descriptors, corrupt assets and injected failures leave no partial formal state.
- Windows and macOS build/test gates pass.
- Updated contracts and `plan.md` accurately reflect implementation status.
