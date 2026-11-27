# FOUNDATION-01 — Time-Zero Baseline Revision and Geometry Publication Contract

- Version: 1.0
- Date: 2026-11-27
- Status: Authoritative implementation contract for FOUNDATION-01; supersedes conflicting legacy Initial State clauses
- Parent decisions: `docs/architecture/ARCH-01-decisions-accepted-v1.0.md`
- Source audit: `docs/architecture/FOUNDATION-01-source-audit-and-gap-matrix-v0.1.md`

## 1. Purpose and authority

This contract defines the Core Domain authority for Map time-zero Initial State after the accepted ARCH-01 decision. It replaces the legacy assumption that Initial State is one mutable baseline row whose members are overwritten in place.

The Map's time-zero state is the initial formal state of the world. Editing it is not a post-zero Command/Event and must not be represented as one. Core Domain owns immutable baseline revisions, revision navigation, active cursor state, stable Object identity, geometry Asset references and atomic publication.

## 2. Scope and invariants

The contract applies to the first supported authoring types:

| Semantic type | Geometry role | Accepted geometry families |
|---|---|---|
| `point_of_interest` | `location` | `point` |
| `river` | `centerline` | `line_string`, `multi_line_string` |
| `landmass` | `extent` | `polygon`, `multi_polygon` |

Only the listed required geometry roles are enabled. This contract does not enable additional relationships, split/merge, hydrology or elevation behavior.

Normative invariants:

1. A Map has one logical time-zero baseline and one active revision cursor.
2. Every committed revision is immutable and permanently addressable.
3. Revision IDs, Object identity IDs and Geometry Component IDs are never recycled.
4. Undo/Redo changes the active cursor only; it never deletes or rewrites committed revisions.
5. Every cursor transition validates the caller's expected active revision/cursor and rejects stale requests.
6. The active revision and cursor survive close/reopen exactly.
7. A revision is a complete, deterministic representation of the Map's time-zero state. A reader must never observe a partially published revision.
8. Geometry bytes are immutable and content-addressed. A changed geometry creates a new Asset; it never overwrites an existing Asset.
9. The Asset reference set, revision content, membership and active cursor are committed atomically in the database.
10. Failed publication leaves no formal revision, membership or typed Asset link. An unreferenced immutable file may remain and is eligible only for later safe reconciliation.
11. Initial State edits are not Commands, Events, Event Effects or post-zero State Changes.
12. Workspace layer/group/presentation state is not stored in these revisions.

## 3. Revision model

### 3.1 Logical model

The logical model consists of:

- **Baseline**: the Map-owned identity for the time-zero state.
- **Revision**: immutable content plus immutable lineage metadata.
- **Edit operation**: a durable description of one accepted time-zero authoring change, sufficient to explain the transition from its parent revision.
- **Cursor**: the Map-owned pointer to the currently active revision and its navigation position.

The initial Map creation creates revision zero (empty baseline) and sets it active in the same transaction as Map creation. The revision-zero content is immutable. The first user edit creates a child revision; it does not mutate revision zero.

### 3.2 Revision lineage

A revision has exactly one parent except revision zero. Each successful edit appends a child to the active revision. Undo moves the cursor to the parent. Redo moves to the selected committed child on the current lineage.

If a new edit is made after Undo, it creates a new child from the current active revision. Existing descendants remain immutable and addressable but are no longer the active redo path. The active redo choice must be deterministic and persisted; the implementation must not infer it from row order or timestamps.

This is a revision tree, not a linear mutable stack. It supports deterministic redo after reopen without deleting abandoned future branches.

### 3.3 Complete-state semantics

A revision must resolve to one complete baseline state. Implementation may choose:

- immutable full snapshots per revision; or
- immutable operation records with deterministic reconstruction from revision zero.

The chosen representation must be recorded in the implementation design before schema migration. It must provide bounded, deterministic reads and integrity verification. Do not use a hybrid where some fields are snapshot-owned and others depend on undocumented mutable current rows.

The logical revision identity and content fingerprint are separate: identity establishes lineage; fingerprint validates canonical content.

## 4. Geometry Component and Asset ownership

1. Geometry Component identity belongs to its top-level semantic Object and is stable across ordinary geometry refinement.
2. Each first-slice descriptor declares exactly one required geometry role as listed in §2.
3. Geometry component manifests are part of the revision's authoritative content.
4. Each referenced geometry payload is stored as an immutable Geometry Asset with a content hash and the Map's bound Spatial Reference identity.
5. The revision's typed Asset link must identify the owning project, Map, revision, Object identity, component identity, role and Asset identity, subject to the final schema normalization.
6. Asset bytes are published to the Asset Store before opening the short database write transaction.
7. The database transaction revalidates the expected cursor, Object state, descriptor pin, Map registration, Spatial Reference identity, Asset metadata/hash and geometry role before committing revision content, typed links, membership and cursor.
8. A geometry edit creates a new Asset. The previous revision continues to reference its original Asset.
9. Removing an Object or component from a new revision removes it from that revision's resolved membership; it does not recycle identities or delete assets referenced by older revisions.
10. Asset garbage collection is a separate reconciliation process and may remove bytes only after proving no formal revision or other owner references them.

## 5. Spatial Reference

Every newly created Map receives an immutable default Spatial Reference:

- Local Cartesian;
- two-dimensional;
- XY axis order;
- abstract `world-unit`;
- no wrap;
- no vertical reference;
- Map-local origin;
- not screen pixels, metres, feet, WGS84 or another geographic CRS.

The definition is created and bound atomically with Map creation. Once geometry exists, the Map's Spatial Reference cannot be directly replaced. Coordinate transformation is a separate future capability.

Numeric precision profile, tolerances and valid extent are deliberately not specified here. Geometry publication must reject a payload that lacks a valid precision profile identifier until DESIGN-EXTENT defines the applicable profile. This contract does not authorize placeholder physical units or invented numeric ranges.

## 6. Core Domain operations

The Domain API must expose intent-level operations equivalent to:

- create empty baseline revision as part of Map creation;
- read active baseline revision and cursor;
- append an edit against an expected active revision/cursor;
- undo against an expected active revision/cursor;
- redo against an expected active revision/cursor and an explicit selected child when multiple redo children exist;
- resolve a revision into a complete immutable baseline state.

Exact type and method names are implementation details, but UI/Application code must not manipulate revision tables or cursor fields directly.

All operations validate project and Map ownership, revision lineage, descriptor pins, membership invariants and Spatial Reference identity in Core Domain.

## 7. Atomicity and failure behavior

The write protocol is:

1. Validate input and prepare canonical geometry payloads outside the database write transaction.
2. Publish immutable Asset bytes and verify their content hashes.
3. Enter the coordinated SQLite write boundary.
4. Re-read current Map, Spatial Reference, active cursor, expected revision, Object identity, descriptor pin and registration.
5. Reject stale or invalid requests before formal mutation.
6. Insert immutable revision/edit-operation metadata and typed Asset links; update the active cursor/redo selection in the same transaction.
7. Commit.
8. On any database failure, roll back all formal rows. Do not delete potentially shared Asset bytes inside rollback handling.

No long-running geometry processing, file I/O, UI interaction or asynchronous wait may occur while holding the SQLite write transaction.

Because this is a single-machine application, the design requires transaction serialization and stale-state validation, not distributed locks or multi-user conflict resolution.

## 8. Undo and Redo semantics

- Undo selects the parent revision of the active revision.
- Redo selects a persisted child revision previously reachable from the active revision.
- A redo selection is explicit when more than one child exists.
- Appending an edit after Undo creates a new child; it does not erase existing descendants.
- Undo/Redo do not create new geometry Assets or mutate old revisions.
- The active cursor transition is atomic and validates the expected current cursor.
- Reopening the project restores the exact active revision and redo choices.

The UI may display a convenient undo history, but it is a projection of Core Domain state and cannot be authoritative.

## 9. Initial Map creation

Map creation must atomically establish:

- Map identity and Map Space identity;
- immutable default Spatial Reference definition and Map binding;
- root History;
- default Calendar and day-zero Calendar Configuration;
- empty Initial State baseline identity;
- immutable revision zero and active cursor.

A failure at any point leaves none of these Map-owned structures committed. The Project itself remains intact.

## 10. Legacy contract supersession

The following legacy clauses are superseded:

- `CD-02-A_OBJECT_AND_INITIAL_STATE_AUTHORITY_v0.1.md` §10 and §13.2: mutable single-baseline/member replacement is no longer the editing model.
- The legacy declaration that `core_map_initial_object_asset` is not enabled is superseded for the first geometry-authoring slice.
- Legacy statements that Initial State revision history or undo/redo are deferred do not apply to the accepted time-zero model.

The following legacy rules remain in force unless separately superseded:

- Object identity, descriptor pinning, reservation and Map registration ownership;
- stable identity and no identity reuse;
- canonical serialization and fingerprint validation;
- coordinated SQLite access and short write transactions;
- immutable Asset bytes and safe orphan reconciliation;
- Branches resolve the same Map time-zero baseline through Map lineage; no baseline copy is created per Branch.

## 11. Required verification

1. New Map creation produces exactly one default Local Cartesian Spatial Reference and revision zero.
2. Failure injection at every Map-creation write stage leaves no partial Map-owned structures.
3. First edit creates a new immutable revision; revision zero remains unchanged.
4. Undo/Redo across multiple edits restores exact canonical state and works after close/reopen.
5. Editing after Undo creates a new branch while preserving prior descendants.
6. Stale append/undo/redo requests are rejected without partial writes.
7. Geometry edit creates a new immutable Asset and older revisions retain old bytes.
8. Failure after Asset publication but before DB commit leaves no formal references and is safe to reconcile.
9. Invalid descriptor/role/family/Spatial Reference/hash is rejected.
10. POI, River and Landmass support only the accepted required geometry roles and families.
11. No Initial State edit creates Command/Event/Effect/State Change rows.
12. Workspace layer presentation changes do not create Core Domain revisions.
13. Windows and macOS test suites pass.

## 12. Open implementation design gate

Before schema implementation, the implementation note must choose and justify:

- full immutable snapshot versus deterministic operation-chain storage;
- revision and operation table normalization;
- redo-child selection persistence;
- whether revision zero has an explicit edit-operation row or is represented as the baseline root;
- exact Asset typed-link cardinality and indexes;
- how revision content is canonicalized and fingerprinted.

These are technical design choices within the accepted product decision. They do not reopen the accepted POI/River/Landmass, Local Cartesian, durable revision/Undo/Redo or Workspace Presentation decisions.
