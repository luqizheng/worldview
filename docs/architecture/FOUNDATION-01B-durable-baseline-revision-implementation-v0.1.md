# FOUNDATION-01B — Durable Baseline Revision Implementation v0.1

- Status: READY FOR INDEPENDENT REVIEW — implemented on branch `foundation-01b-durable-revisions`, PR #54
- Parent design: `docs/architecture/FOUNDATION-01B-durable-baseline-revision-design-v0.1.md`
- Parent contract: `docs/core-domain/FOUNDATION-01-time-zero-baseline-contract-v1.0.md`
- Product decisions: `docs/architecture/ARCH-01-decisions-accepted-v1.0.md` (decision 3)
- Supersedes: the mutable single-baseline/member editing model of CD-02-A §10/§13.2 (already superseded by the accepted contract)

## 1. Scope

This document records what PR #54 implements against the frozen FOUNDATION-01B design (v0.1, merged via PR #53). It covers the Domain model, the persistence port and SQLite adapter, the schema convergence migration, transaction/atomicity semantics, and the verification evidence. It does not extend scope into FOUNDATION-01C (Geometry Asset publication), which remains gated.

## 2. Storage convergence (design §3, §9)

Final schema after the full migration sequence:

| Table | Role | Mutability |
|---|---|---|
| `core_map_initial_state` | stable baseline identity only (`initial_state_id`, project, map, created-at) | immutable after creation |
| `core_map_initial_state_revision` | immutable revision row: lineage (`parent_revision_id`, per-baseline monotonic `revision_number`), `baseline_schema_version`, `content_fingerprint` (64-hex CHECK), `operation_kind`/`operation_payload_json` (root: NULL; non-root: required pair) | INSERT only; UPDATE/DELETE rejected by triggers |
| `core_map_initial_state_revision_member` | complete snapshot members per revision; composite FK to revision, `core_map_object_ref` (P3⇒P2), `core_object` | INSERT only; UPDATE/DELETE rejected by triggers |
| `core_map_initial_state_cursor` | the only mutable authoritative row: active revision, persisted redo-child selection, monotonic `cursor_version` | UPDATE by Domain adapter only |

`core_map_initial_object` and the mutable baseline fingerprint/schema-version columns are dropped by the convergence migration. No dual-write or compatibility layer remains.

### 2.1 Migration sequence

1. `20261128014741_CoreMapInitialStateRevisions` (unchanged, already applied where present): creates revision/member/cursor storage.
2. `20261128042050_CoreMapInitialStateConvergence` (new): retires the mutable storage and converts existing development data per design §9, in three ordered scripts:
   - **Preparation:** preserves old member rows and baseline fingerprints into temp tables, drops the interim revision tables and `core_map_initial_object`, rebuilds `core_map_initial_state` to the identity-only shape (via a converged table + rename so FK parents are never dropped while referenced), and inserts a fresh baseline identity for any Map that had none.
   - **Revision storage creation:** re-executes the shared `CoreMapInitialStateRevisionTablesSql` constant (single DDL source; identical shape to the interim migration).
   - **Backfill:** inserts revision zero for every baseline — revision zero id = initial state id (deterministic), `revision_number = 0`, no parent, no operation metadata — carrying the old baseline's `content_fingerprint` verbatim. The fingerprint composition (`initial-state-fingerprint-v1`) and member payloads are identical pre- and post-convergence, so the carried value **is** the Domain-recomputed fingerprint of the preserved member set; SQL never invents or recomputes fingerprints. Baselines created for previously baseline-less Maps use the Domain empty-snapshot fingerprint constant (`MapInitialStateService.EmptySnapshotFingerprint`). Members are copied verbatim with `revision_id = initial_state_id`. Every Map receives a cursor active on its revision zero (`cursor_version = 0`, no redo selection).
3. `Down()` remains unsupported (forward-only 1.0 schema).

A Map without baseline/revision zero/cursor after migration is impossible; the read contract treats absence as `CoreDomainDataException` (fail-closed), never lazy creation.

## 3. Domain model and port (design §6, §10)

`ICoreDomainDatabase` replaces the retired mutable operations (`FindMapInitialState`, `ListInitialObjects`, `EditMapInitialState`) with:

- `FindMapInitialStateBaseline` — baseline identity lookup (null when absent);
- `FindMapInitialStateRevisionSnapshot` — any immutable snapshot by revision id;
- `ListMapInitialStateRevisions` — revision metadata for history presentation;
- `FindMapInitialStateRevisionState` — active snapshot + cursor + direct children as one consistent read inside the connection-coordinator critical section;
- `AppendMapInitialStateRevision` — CAS append (expected active revision + cursor version);
- `MoveMapInitialStateCursor` — CAS Undo/Redo transition;
- `SelectMapInitialStateRedoChild` — CAS persisted branch choice without moving the active revision.

`MapInitialStateService` exposes intent-level operations (`ReadActiveState`, `ReadRevisionSnapshot`, `ListRevisions`, `AppendRevision`, `Undo`, `Redo`, `SelectRedoChild`, `BuildInsertObjectAppend`). Application/UI code reads and mutates only through these; there is no UI-local authoritative undo stack anywhere in the repository.

Semantics implemented exactly per design §6:

- Every cursor mutation validates both `expected_active_revision_id` and `expected_cursor_version` inside the write transaction; mismatches are typed `CoreDomainConflictException` with no partial writes.
- Append reads the authoritative parent snapshot inside the transaction, recomputes the fingerprint and canonical operation payload from the exact committed rows and rejects any mismatch with the Domain plan (design §7 double check); inserts the complete child snapshot; moves the cursor; preserves all prior revisions and descendants.
- Undo requires a parent (revision zero is not undoable), moves the cursor to the parent, and sets the selected redo child to the revision just left.
- Redo requires the selected or explicitly supplied target to be a direct child of the same baseline; a persisted selection resolves an implicit redo; exactly one child permits direct redo; multiple children without a selection are rejected with a typed error; redo clears the selection.
- `cursor_version` increments by exactly one per successful append/Undo/Redo/selection transition and is never rewritten.
- Edit-after-Undo creates a new child of the current active revision; abandoned descendants remain immutable and addressable.

## 4. Map creation atomicity (design §5, contract §9)

`MapCreationPlan` now carries pre-generated `MapInitialStateBaselineCreation` (baseline identity, revision-zero identity, schema version, empty-snapshot fingerprint). `SqliteCoreDomainDatabase.CreateMapStructure` inserts, in the existing single transaction: Spatial Reference definition and binding, Map, root History, default Calendar, day-zero configuration, baseline identity, revision zero (empty snapshot; fingerprint re-derived and compared against the Domain constant, fail-closed) and the active cursor. Failure at any stage rolls back every Map-owned row; there is no half-initialized Map and no post-creation fill-in path.

## 5. Object insertion atomicity (design §10, contract §7)

`ObjectCreationService.InsertObject` derives the revision-append context (`ObjectInsertInitialRevisionAppend`: expected active revision, expected cursor version, pre-generated child revision id, fingerprint, operation payload) from the active snapshot before opening the write boundary. The adapter's transaction then: re-reads reservation/identity/descriptor; registers the Map object; writes the definition first row; flushes; re-reads the cursor and re-validates the CAS credentials; re-derives the child snapshot from the authoritative parent rows (new member canonicalized, pairing checked); recomputes fingerprint/operation payload and rejects mismatches; inserts the revision + members; moves the cursor; activates the identity and transitions the reservation — all in one transaction. Failure leaves no partial identity, registration, membership, revision or cursor state, and identity/reservation are reusable for retry without new ID allocation. Asset bytes remain outside the write transaction (no Asset path is introduced by 01B).

## 6. Verification evidence

All suites green on Windows Debug and Release (xunit.v3/MTP; 2026-11-28):

| Project | Debug | Release |
|---|---|---|
| WorldView.Domain.Tests | 278/278 | 278/278 |
| WorldView.Infrastructure.Tests | 329/329 | 329/329 |
| WorldView.Application.Tests | 190/190 | 190/190 |
| WorldView.Presentation.Tests | 111/111 | 111/111 |
| WorldView.Desktop.Tests | 100/100 | 96/96 (4 Debug-only fault-injection tests, pre-existing) |

New/updated coverage keyed to the design §11 gates:

- **Map creation:** baseline + revision zero + cursor exist and are readable immediately after creation; per-Map row counts exactly one triple (`MapCreation_EstablishesBaselineRevisionZeroAndCursorAtomically`, `MapCreation_AllExistingMapsHaveBaselineRevisionZeroAndCursor`).
- **Convergence migration:** a database built to the pre-convergence shape (migrations applied only through the interim revision migration) with a mutable baseline + member row and a baseline-less Map converges losslessly: members preserved verbatim as revision zero members, carried fingerprint equals the Domain-recomputed value, baseline-less Maps receive empty revision zero + cursor, old table dropped, and the full append/Undo flow works afterward (`CoreMapInitialStateConvergenceMigrationTests.Convergence_PreservesMutableBaselineAsRevisionZeroAndFillsBaselinelessMaps`).
- **Database immutability behavior (real SQLite, not trigger existence):** committed revision UPDATE/DELETE rejected; revision-member UPDATE/DELETE rejected; rejected writes leave content unchanged; cursor remains mutable; legal revision INSERT works; FK/relational constraints enforced; trigger errors propagate as `SqliteException` (never swallowed).
- **Stale rejection:** stale append (wrong revision or version) and stale Undo produce typed conflicts with zero rows written (row counts asserted).
- **Insert transaction:** mid-transaction injected failure (`BEFORE INSERT` trigger) rolls back identity/reservation/definition/registration/revision/member/cursor completely; retry reuses the same reservation/identity.
- **Undo/Redo:** root not undoable; redo selection persists and constrains; edit-after-undo branches and preserves descendants; undo/redo are cursor-only (revision row counts constant); close/reopen restores cursor + selection (fresh adapter instance).
- **Domain rules:** canonical diff sets (added/removed/updated) are disjoint, sorted, and match the composition contract; fingerprint determinism; direct-child validation; redo target resolution; cursor version monotonicity (Domain tests + `InitialStateRevisionRulesTests`).
- **Migration consistency:** EF model ↔ snapshot differ is empty and `HasPendingModelChanges` is false (8 migrations asserted).

## 7. Out of scope / next

- FOUNDATION-01C owns the typed Geometry Asset link table, geometry-component manifest in the revision content, and the fingerprint composition version bump it requires (design §7/§8).
- No precision profile, extent, Command/Event/Effect or State Change rows are created for time-zero edits; workspace layer state remains presentation-only.
