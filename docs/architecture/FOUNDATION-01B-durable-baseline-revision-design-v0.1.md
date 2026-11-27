# FOUNDATION-01B — Durable Time-Zero Baseline Revision Design v0.1

- Version: 0.1
- Status: Technical implementation design for review
- Parent contract: `docs/core-domain/FOUNDATION-01-time-zero-baseline-contract-v1.0.md`
- Product decisions: `docs/architecture/ARCH-01-decisions-accepted-v1.0.md`

## 1. Source audit: current implementation

The current master implementation still represents Initial State as one mutable baseline:

- `MapInitialStateService.EditInitialState` accepts an expected content fingerprint and a complete member collection. If a baseline exists, it calls `EditMapInitialState`; if absent, it creates one.
- `SqliteCoreDomainDatabase.EditMapInitialState` rechecks the fingerprint in a transaction, deletes every row in `core_map_initial_object`, inserts the replacement set, then overwrites `baseline_schema_version`, `content_fingerprint` and `updated_at_utc_ms` on the single baseline row.
- `InsertObject` can append an Initial State member through `AppendInitialMemberInDbContext`; that helper creates a baseline lazily when absent and rewrites the same mutable baseline fingerprint.
- `core_map_initial_state` has one row per `(project_id,map_id)`, containing the mutable schema version and fingerprint. `core_map_initial_object` is keyed by `(initial_state_id,object_identity_id)` and points to Map registration and Object identity.
- `FindMapInitialState` and `ListInitialObjects` expose only the current mutable state. There is no revision identity, parent relation, active cursor, redo choice or durable Undo/Redo API.
- `MapCreationPlan` currently creates Map, Spatial Reference, root History, Calendar and day-zero Calendar Configuration, but no Initial State baseline.
- Domain tests explicitly prove mutable CAS replacement; Infrastructure tests prove the current transaction and membership invariants. Those tests must be replaced/extended to prove immutable revision history and cursor semantics.

The old implementation is therefore not suitable for the accepted durable baseline contract. FOUNDATION-01B will replace the mutable representation rather than layering a UI undo stack over it.

## 2. Selected storage strategy: immutable full snapshots

**Decision: store one complete immutable snapshot for every committed revision.**

Each revision owns its complete resolved time-zero member set. Reads of any revision are direct and deterministic; no replay chain is required.

Rationale:

1. V1 scope is deliberately small: POI, River and Landmass, with one required geometry role per type.
2. Full snapshots make Undo/Redo cursor-only transitions and make reopen behavior straightforward to verify.
3. Snapshot reads do not depend on replay order, operation interpreter versions, or historical code remaining executable.
4. Failed edits can roll back a single revision insert and cursor update without changing any previous state.
5. Snapshot duplication is acceptable for the first desktop authoring slice. Compaction/delta storage is a later measured optimization, not a prerequisite.

No mutable current-member table will remain authoritative. The active state is always resolved from the active immutable revision.

## 3. Logical schema

### 3.1 Baseline identity

Retain `core_map_initial_state` as the stable Map-owned baseline identity only:

- `initial_state_id` primary key;
- `project_id`, `map_id`;
- `created_at_utc_ms`;
- unique `(project_id,map_id)`.

Remove the mutable baseline fingerprint/schema-version fields from this table. Those values belong to immutable revisions.

### 3.2 Immutable revision

Add `core_map_initial_state_revision`:

- `revision_id` primary key;
- `project_id`, `map_id`, `initial_state_id`;
- `revision_number` (root is 0; subsequent revisions monotonically increase per baseline);
- nullable `parent_revision_id` (NULL only for revision zero);
- `baseline_schema_version`;
- `content_fingerprint`;
- nullable `operation_kind` and `operation_payload_json`;
- `created_at_utc_ms`.

Required constraints:

- unique `(project_id,initial_state_id,revision_id)`;
- unique `(project_id,initial_state_id,revision_number)`;
- composite FK to the owning baseline;
- composite self-FK `(project_id,initial_state_id,parent_revision_id)` to a revision of the same baseline;
- revision zero iff parent and operation metadata are NULL; non-root revisions require a parent and complete operation metadata.

### 3.3 Immutable revision members

Replace `core_map_initial_object` with `core_map_initial_state_revision_object`:

- `project_id`, `map_id`, `initial_state_id`, `revision_id`, `object_identity_id`;
- canonical membership payload;
- optional placement payload and paired schema version;
- immutable creation timestamp;
- primary key `(revision_id,object_identity_id)`;
- composite FK to the owning revision;
- composite FK to `core_map_object_ref` and `core_object`.

This preserves the current P3⇒P2 registration invariant for every revision snapshot. Revision membership is never updated or deleted after commit.

### 3.4 Mutable cursor

Add `core_map_initial_state_cursor`, one row per baseline:

- `project_id`, `map_id`, `initial_state_id`;
- `active_revision_id`;
- nullable `selected_redo_child_revision_id`;
- `cursor_version` (starts at 0 and increments on every append/Undo/Redo/redo-selection transition);
- `updated_at_utc_ms`.

The cursor has composite FKs to the baseline and to revisions belonging to that same baseline. In addition, `selected_redo_child_revision_id` must either be NULL or identify a direct child of `active_revision_id` in the same baseline; this cross-row predicate is validated in every cursor transaction. It is the only mutable authoritative row in the revision model.

## 4. Operation representation and normalization

**Decision: do not create a separate operation table in V1.** One accepted edit creates exactly one revision and exactly one operation description; there is no independent operation lifecycle or many-to-many relation. Store the operation descriptor on the immutable revision row.

- Revision zero is the baseline root and has no edit operation.
- Every non-root revision has `operation_kind = 'snapshot_replace'`.
- `operation_payload_json` is canonical JSON containing deterministic sorted `added`, `removed` and `updated` Object identity sets, plus parent and child fingerprints.
- The full child snapshot contains the resulting values; the parent snapshot contains the previous values. Together with the operation descriptor they explain the exact transition without making the operation payload a second copy of all state.

This keeps the operation/revision relationship normalized for the V1 one-operation-per-revision invariant. A separate operation table is only justified if a later product decision permits multiple independently addressable operations inside one revision.

## 5. Revision zero and Map creation

**Decision: revision zero is represented as the immutable baseline root, not as an explicit edit operation.**

FOUNDATION-01B extends `MapCreationPlan` with pre-generated baseline and revision-zero identities. Map creation's existing SQLite transaction will additionally insert:

1. baseline identity;
2. revision zero with `revision_number=0`, no parent, empty full snapshot and its deterministic fingerprint;
3. cursor with active revision zero, no redo child and cursor version 0.

All Map-owned rows, including the FOUNDATION-01A Spatial Reference, root History, Calendar and day-zero configuration, remain one all-or-nothing transaction.

The current lazy baseline creation path will be removed. A Map without a baseline/cursor after migration is a data-integrity error, not a normal state.

## 6. Cursor and Undo/Redo semantics

Every cursor mutation requires both:

- `expected_active_revision_id`;
- `expected_cursor_version`.

Both are checked inside the same SQLite write transaction. A mismatch is a typed stale-state conflict and produces no revision or cursor mutation.

### Append edit

- Read the active revision snapshot.
- Validate the complete new member set and membership invariants.
- Create a new immutable child of the active revision, with the next per-baseline revision number and deterministic operation descriptor.
- Insert the complete snapshot members.
- Set active revision to the new child, clear selected redo child, increment cursor version.
- Preserve every previous revision and descendant.

### Undo

- Require the expected active revision/cursor version.
- Require a parent revision.
- Move active revision to the parent.
- Set selected redo child to the revision just left.
- Increment cursor version.
- Do not create a revision or mutate any snapshot.

### Redo

- Require the expected active revision/cursor version and an explicit selected child revision ID.
- Validate that the selected revision is a direct child of the current active revision and belongs to the same baseline.
- Move active revision to that child, clear selected redo child and increment cursor version.
- When multiple children exist, the caller must select one explicitly. Undo sets the selected redo child to the revision just left. A dedicated `SelectRedoChild(expectedActiveRevisionId, expectedCursorVersion, childRevisionId)` operation persists a user's explicit branch choice without moving the active revision; it requires the target to be a direct child and increments `cursor_version`. Redo consumes the selected child (or accepts an explicit child ID in the same request), moves the active pointer and clears the selection. This makes a branch choice made in the history UI survive close/reopen. If there is only one child, the UI may offer a direct Redo action; if there are multiple children and no selected child, it must require an explicit choice.

A new edit after Undo creates another child and preserves the old descendants. No revision is deleted or overwritten.

## 7. Canonical content and fingerprint

V1 revision content is a canonical manifest:

- structural baseline schema version;
- members sorted by canonical Object identity UUID;
- canonical membership JSON;
- canonical optional placement JSON and schema version;
- an explicit empty geometry-component collection reserved for FOUNDATION-01C.

Canonical JSON continues to use the existing Domain `CanonicalJson` rules (ordinal object-key ordering, deterministic scalar serialization). The fingerprint is SHA-256 over the canonical manifest and a versioned composition marker.

FOUNDATION-01C will add the accepted geometry-component manifest and typed Asset references to the same revision manifest before geometry authoring is enabled. It must version the fingerprint composition deliberately; it must not silently reinterpret an existing fingerprint.

## 8. Typed Geometry Asset link decision for FOUNDATION-01C

One immutable revision-component reference row identifies exactly one Asset for one component role in one revision:

- project, Map, baseline, revision;
- Object identity;
- stable Geometry Component identity;
- geometry role;
- Asset identity and content hash.

Uniqueness: `(project_id,revision_id,object_identity_id,component_id,role)`. A revision may contain multiple components for an Object only if its accepted descriptor later permits them; the first POI/River/Landmass descriptors each require one role. Add an index by `(project_id,asset_id)` for reference checks and by `(project_id,revision_id)` for snapshot resolution.

Asset bytes remain immutable/content-addressed. 01B does not create or publish geometry Asset rows; 01C implements the typed link and atomic Asset/revision publication.

## 9. Migration and existing development data

The migration replaces the mutable baseline/member schema with the revision/cursor schema.

For any existing Map that already has a mutable Initial State row, the migration preserves its current canonical member set as revision zero and sets that revision active. For a Map with no baseline, it creates an empty revision zero. This does not claim to reconstruct edits that predate the revision model; the repository has no released customers or historical production data. It preserves useful development state rather than silently dropping it.

No dual-write or compatibility layer will remain after the migration.

## 10. API and service boundary

Core Domain will expose intent-level operations to:

- read the active baseline snapshot and cursor;
- list revision metadata/children for history presentation;
- append a complete time-zero snapshot against expected revision and cursor;
- Undo against expected revision and cursor;
- persist an explicit redo-child selection against expected revision and cursor;
- Redo to the selected or explicitly supplied direct child against expected revision and cursor;
- read any immutable revision snapshot.

Application/UI code will not read or mutate revision/cursor tables directly and will not maintain an authoritative local Undo stack.

The existing Object insertion path that adds an Initial State membership must append an immutable child revision in the same transaction as Object activation, reservation transition and registration. It must not bypass revision/cursor authority.

## 11. Verification gates

- New Map creates exactly one baseline, revision zero, empty snapshot and cursor in the Map transaction.
- Failure at each Map-creation stage leaves no partial Map-owned rows.
- First membership edit creates revision one; revision zero remains unchanged.
- Append/Undo/Redo preserve the complete revision tree and exact fingerprints.
- Multiple redo children require explicit selection; a selection made in the history UI is persisted and survives reopen.
- Stale expected revision or cursor version is rejected with no partial writes, including redo-child selection.
- Edit-after-Undo creates a new child and preserves the abandoned descendants.
- Object insertion with Initial Membership appends a revision atomically with identity/registration/reservation changes.
- Existing development baseline data is migrated into revision zero without loss.
- No time-zero edit writes Command/Event/Effect/State Change rows.
- Windows/macOS Debug and Release CI pass.
