# World View P2-09-12 — .NET SQLite Provider and Cross-Module Transaction Proof Test Specification V0.4

- Date: 2026-11-21
- Version: 0.4
- Status: Object-presence/relationship-activity-reconciled development verification specification — test design only; no implementation code or test execution performed by this document
- Supersedes: P2-09-12 V0.3; V0.3 is retained as history
- Depends on: P2-09-01; P2-09-04 V0.2; P2-09-09 V0.1; P2-09-10 V0.1; P2-09-11 V0.10; P2-09-13 V0.6; P2-09-15 V0.5; P2-09-17 V0.5; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-21 V0.1; P2-09-22 V0.2; AI/Core Integration Review V0.8; AI Assistant Logical Schema V1.16
- Target: Avalonia 12 desktop application, .NET 10, Microsoft.Data.Sqlite 11.x with bundled SQLitePCLRaw e_sqlite3

## 1. Purpose

Define the minimum evidence required before World View may claim that Core Domain formal writes and AI Assistant submission evidence share one atomic SQLite transaction on the actual Windows/macOS application runtime.

This is a handoff specification for the development team. It does not implement the tests, claim that they have passed, or authorize the final integrated Schema V2.0.

## 2. Architecture claim to prove

The final authorized AI submission has one short persistence boundary:

1. Application Commit Coordinator opens one `SqliteConnection`.
2. It begins one `SqliteTransaction`.
3. Core Domain Domain Services persist formal Commands, accepted ordering relations and required Core references using that exact connection and transaction.
4. AI Assistant repositories persist the submission attempt outcome, exact returned Command references and minimum immutable submission evidence using the same connection and transaction.
5. The coordinator commits once.

The following are prohibited:

- Core Domain opening a second connection inside the final commit path;
- AI Assistant opening a second connection inside the final commit path;
- either repository creating or committing an independent nested transaction;
- describing two separate commits plus compensation as atomic;
- holding the write transaction open during LLM generation, user interaction, long validation, geometry generation, asset staging or network work.

The coordinator owns the physical transaction boundary but does not become the authority for Core Domain or AI Assistant facts.

## 3. Test runtime and package matrix

Tests must execute through the exact provider and native SQLite library packaged by the desktop application, not through SQLite CLI or Python `sqlite3` alone.

| Dimension | Required verification |
|---|---|
| Target framework | Application and test host use the selected .NET 10 target |
| UI/runtime | Avalonia 12 application package can load the same persistence assembly |
| Provider | Pinned Microsoft.Data.Sqlite 11.x package; exact patch recorded in lock file |
| Native SQLite | Bundled SQLitePCLRaw `e_sqlite3`; verify actual native engine version at runtime |
| Windows | Packaged desktop/test host starts, opens DB, executes migration and transaction suite |
| macOS | Packaged desktop/test host starts, opens DB, executes migration and transaction suite on each supported RID/architecture |
| SQLite features | Verify foreign keys, triggers, JSON1, FTS5 and R*Tree availability where used by the final schema |

Do not assume that a successful `net10.0` build proves Avalonia packaging or native library loading. Both Windows and macOS packaged-runtime tests are required.

## 4. Connection and transaction ownership tests

### TX-01 — Shared connection identity

- Begin a final submission through the Application Commit Coordinator.
- Instrument the Core Domain repository and AI submission repository to record the connection instance and transaction instance they receive.
- Assert both receive the exact same `SqliteConnection` object and exact same `SqliteTransaction` object.
- Assert neither opens a new connection for any write in the commit path.

**Pass:** one connection instance and one transaction instance are shared by all participants.

### TX-02 — No nested transaction or hidden commit

- Invoke representative nested Core Domain service calls and AI repository methods during the submission.
- Verify they participate in the supplied transaction and do not independently begin, commit or roll back another transaction.
- Verify no helper method silently switches to a default connection factory.

**Pass:** only the coordinator controls commit/rollback.

### TX-03 — Foreign-key enforcement on every connection

- Open every connection type used by the application (writer, read connection, migration connection, recovery connection).
- Query `PRAGMA foreign_keys` and verify it is enabled.
- Attempt invalid cross-project and missing-parent references.

**Pass:** every connection enforces the intended foreign-key constraints; invalid references fail.

### TX-04 — Connection ownership and threading

- Attempt concurrent access to the critical transaction connection from multiple threads.
- Verify the application write coordinator prevents concurrent use.
- Verify independent reads use separate connections and never share the writer's active transaction connection.

**Pass:** one owner serializes writes; no connection/command/reader object is concurrently shared.

## 5. Atomicity and failure-injection tests

Every test starts from a known database state and checks both Core Domain tables and AI Assistant tables after completion or rollback.

### TX-10 — Successful commit

- Persist at least one Core Command and any accepted ordering relation through Core Domain services.
- Persist the corresponding submission attempt outcome and returned Command references through AI repositories.
- Commit once.

**Pass:** all expected Core and AI rows are visible after commit, with exact cross-references and no partial state.

### TX-11 — Failure after Core write, before AI write

- Inject an exception after Core Domain has written its formal rows but before AI submission evidence is written.
- Roll back through the coordinator.

**Pass:** no Core Command/ordering rows and no AI success evidence remain committed.

### TX-12 — Failure after AI write, before commit

- Write Core Domain rows.
- Write AI submission evidence.
- Inject an exception immediately before commit.

**Pass:** neither side is committed.

### TX-13 — Failure inside a nested Core Domain operation

- Inject failure after one Core Domain row has been written but before the complete formal operation finishes.
- Ensure the exception propagates to the coordinator.

**Pass:** the entire cross-module transaction rolls back; no partial formal operation remains.

### TX-14 — Constraint failure

- Cause a real FK, UNIQUE, CHECK or trigger violation in a Core Domain write.
- Verify AI success evidence is not committed.

**Pass:** a rejected Core write cannot leave a successful AI submission report or returned Command mapping.

### TX-15 — Cancellation before commit

- Request cancellation after writes begin but before commit.
- Verify the cancellation write barrier prevents any further writes and the coordinator rolls back.

**Pass:** no partial commit; the attempt is safely retryable only after normal reconciliation rules.

### TX-16 — Cancellation after commit

- Request cancellation after the database commit has succeeded but before the UI receives the response.

**Pass:** the operation remains committed; Task cancellation/Undo does not remove formal Commands or reservation consumption.

## 6. Crash and uncertain-outcome tests

### TX-20 — Process termination before commit

- Terminate the application process after Core and AI writes but before transaction commit.
- Restart and inspect the database.

**Pass:** neither Core nor AI success evidence is committed.

### TX-21 — Process termination after commit, before response

- Terminate the process immediately after SQLite commit but before the caller receives success.
- Restart and reconcile using the stable submission idempotency key and exact request hash.

**Pass:** the system discovers the already-committed outcome and does not insert duplicate Commands or duplicate submission evidence.

### TX-22 — Repeated reconciliation

- Reconcile the same uncertain attempt multiple times.

**Pass:** reconciliation is idempotent; it returns the same authoritative outcome and does not mutate the original authorization/proposal evidence.

### TX-23 — No blind replay

- Keep an attempt in `outcome_unknown` and attempt to submit it again.

**Pass:** the coordinator blocks blind replay until Core Domain History has been queried and the prior outcome is resolved.

## 7. SQLite write serialization and read behavior

### TX-30 — Single writer coordinator

- Trigger two independent write requests from application services.
- Verify they are serialized by the application write coordinator.
- Verify the second write does not use a second competing transaction against the same Project database.

**Pass:** writes are serialized; no distributed lock or multi-instance coordination is introduced.

### TX-31 — Reader isolation

- Keep normal UI read operations active while a short write transaction commits.
- Verify reads use separate connections and do not use the writer connection concurrently.

**Pass:** read activity does not violate connection ownership or corrupt the transaction.

### TX-32 — Busy/locked handling

- Exercise a controlled writer-lock conflict and a transient busy condition.
- Verify the configured timeout/retry behavior is bounded and deterministic.
- Verify no retry repeats a non-idempotent write without the stable operation/submission key.

**Pass:** transient lock handling never produces duplicate Commands or false success.

## 8. Migration and schema verification tests

### TX-40 — Fresh bootstrap

- Create a new Project database.
- Run the shared migration ledger and module-owned migrations in declared dependency order.
- Verify every expected table, index, trigger and FK exists.

**Pass:** Core Domain and AI Assistant initialize into one database with no shadow domain tables and no missing dependencies.

### TX-41 — Idempotent bootstrap

- Run the bootstrap process again against the initialized database.

**Pass:** no migration is reapplied; schema and checksums remain unchanged.

### TX-42 — Checksum mismatch / unknown migration

- Change an already-applied migration checksum or supply an unknown newer migration.

**Pass:** startup fails closed; it does not silently repair, skip or rewrite schema history.

### TX-43 — Foreign-key and invariant audit

- Run SQLite `foreign_key_check` and the Core Domain cross-table integrity checks.
- Include Branch acyclicity, one root History per Map, one default Calendar per Map, reservation lifecycle/identity consistency, pinned Object Type schema/semantics, Shared/Map-local Object registration, Calendar Configuration source Command consistency, Map Initial State membership, relationship origin/endpoint/temporal integrity, required typed Command Object/Asset links and Checkpoint fingerprint coherence.

**Pass:** no invalid references or cross-table invariant violations remain.

## 9. Asset and Checkpoint consistency tests

### TX-50 — Asset publication before DB commit

- Write a candidate Asset to a temporary file, validate/hash it, and atomically publish it to the immutable Asset Store.
- Begin the short DB transaction and persist Core Asset metadata, `core_command_asset` / other required typed Core Asset links, AI `submission_asset_refs`, Core Command Object references and the AI submission outcome evidence.

**Pass:** committed Core Asset rows and AI `submission_asset_refs` point to a fully published immutable file; the stored hash matches the sealed proposal manifest.

### TX-51 — Asset reference FK and retention

- Attempt to insert an AI `submission_asset_refs` row with a missing `core_asset_id`.
- Attempt to delete a Core Asset row that is referenced by a committed `submission_asset_refs` row.

**Pass:** the missing asset reference is rejected by the FK; a referenced Core Asset cannot be garbage-collected.

### TX-52 — DB failure after Asset publication

- Publish an immutable Asset file.
- Inject a failure before the DB transaction commits.

**Pass:** no committed DB reference exists; the file remains an unreferenced orphan until safe cleanup proves it is unused. The system never deletes a file based only on a failed transaction assumption.

### TX-53 — Checkpoint publication failure

- Write and validate a Checkpoint payload file.
- Inject a failure before complete Checkpoint metadata is committed.

**Pass:** the Checkpoint is not visible as complete/current; formal history remains intact and can be reconstructed without it.

### TX-55 — Bound reservation and formal Object creation atomicity

- Start from a committed Bound reservation and its draft `core_object` identity row.
- In one shared SQLite transaction, persist the Object creation Command, required `core_command_object_ref`, Map registration, required definition/initial runtime facts, transition the Object identity to active, transition the reservation to Inserted, and persist the AI submission evidence.
- Inject failures after each individual Core write and before the final commit.

**Pass:** success commits all related facts together; every injected pre-commit failure leaves the reservation Bound and the identity draft, with no formal Command, active identity, Map registration or AI success evidence. Retry reuses the same reservation and identity; it never allocates a replacement ID.

### TX-56 — Geometry Command typed Asset reference atomicity

- Publish the exact immutable geometry Asset and verify its content hash.
- Persist the geometry Command, `core_command_asset`, `core_command_object_ref`, AI submission evidence and returned Command receipt in one short transaction.
- Inject failure after the Command row but before the typed Asset link, and after the typed link but before AI evidence.

**Pass:** either all DB facts commit or none do; an orphaned published file remains unreferenced and is cleaned only by the safe orphan process. No Command can commit without its required geometry Asset link.

### TX-57 — Relationship ID preflight and sealed payload

- Ask Core Domain to preflight a relationship.establish proposal and return its canonical payload with a Core Domain-generated `relationship_id`.
- Seal/authorize that exact canonical payload and submit it.
- Inject failure before commit and retry the same sealed proposal using the same relationship ID.

**Pass:** the committed Command contains the exact authorized relationship ID/payload; failed application leaves no formal relationship; retry does not create a second relationship identity or mutate the sealed proposal.

### TX-58 — Relationship termination origin and Branch scope

- Test relationship.end against both a Map Initial State relationship and a visible establishing Command on an ancestor History path.
- Test a Branch ending an inherited relationship strictly after `parent_at`.
- Attempt to end an unknown, already-ended, non-visible or cross-Project relationship.

**Pass:** only valid visible active relationships can be ended; Branch termination remains local; parent/sibling histories remain unchanged; all invalid cases fail before formal persistence.

### TX-59 — Object Type semantics pinning

- Bind an Object identity under a specific Object Type schema version and semantics identifier.
- Upgrade the application registry with a changed Object Type handler.
- Reconstruct/edit the existing Object and attempt an unsupported type semantics ID.

**Pass:** the pinned historical semantics are used or an explicit compatibility migration is required; unsupported semantics fail closed and are never silently substituted.

### TX-61 — History-local Object activation/deactivation

- Create or baseline an Object in Map A and Map B with the same Project-shared identity.
- Deactivate the Object in Map A, then activate it again in Map A.
- Attempt activation for an Object never present in that History and for a globally retired identity.

**Pass:** only the selected History's runtime availability changes; Map B remains unchanged; activation requires a prior deactivation in the same History and never changes the Project-level identity lifecycle.

### TX-62 — Relationship endpoint activity policy

- Establish a relationship with `active_endpoints_required`, then attempt to deactivate one endpoint without ending the relationship.
- End the relationship and retry deactivation.
- Establish a `dormant_endpoint_allowed` relationship, deactivate one endpoint, then reactivate it.

**Pass:** active-required relations block deactivation until explicitly ended; dormant-allowed relations remain temporally active but dormant without cascade termination; reactivation revalidates and restores operational eligibility only when valid.

### TX-60 — Required typed-link enforcement

- Attempt to persist an enabled V1 Object/Geometry/Relationship Command without its required `core_command_object_ref`.
- Attempt to persist a formal geometry Command without `core_command_asset`.
- Attempt cross-Project or wrong-History Object/Asset references.

**Pass:** Core Domain validation and relational constraints reject missing or invalid typed references; no AI success evidence is committed.

### TX-54 — Project backup and restore

- Create a database containing committed Core/AI rows, WAL activity, immutable Assets and Checkpoints.
- Execute the approved consistent backup procedure.
- Restore into a clean Project directory.
- Run integrity checks and open the restored Project.

**Pass:** all committed DB references resolve to present, hash-valid Assets; incomplete/orphan Checkpoints are safely ignored or rebuilt; no committed Command or AI submission evidence is lost.

## 10. Runtime durability and PRAGMA verification

The exact PRAGMA policy remains a design gate. The development team must record and verify at runtime:

- `journal_mode` (recommended WAL, subject to backup/restore proof);
- `synchronous` durability setting and its crash-loss trade-off;
- `foreign_keys=ON` on every connection;
- bounded `busy_timeout` consistent with the single-writer coordinator;
- any required `temp_store`, cache or mmap options only if justified by measured behavior.

Do not copy a generic PRAGMA recipe without measuring the packaged provider and filesystem behavior on Windows and macOS.

## 11. Required test evidence

The development team should deliver:

1. Exact .NET target framework and Avalonia version.
2. Exact Microsoft.Data.Sqlite and SQLitePCLRaw package versions and lock file.
3. Runtime SQLite version and enabled feature results for each supported Windows/macOS RID.
4. Test report for TX-01~TX-62, including injected failure location and database assertions.
5. Evidence that both repositories received the same connection and transaction instances.
6. WAL/backup/restore test results and the chosen PRAGMA policy.
7. Known limitations and any failed tests, with a blocking issue rather than a false atomicity claim.

## 12. Acceptance criteria and stop conditions

The cross-module transaction claim is accepted only when:

- all critical atomicity tests pass on the packaged Windows and macOS runtimes;
- Core Domain and AI repositories demonstrably share the same connection and transaction;
- crash-after-commit reconciliation prevents duplicates;
- every connection enforces foreign keys;
- backup/restore preserves committed database and Asset Store consistency;
- no unresolved failure can leave one side committed while the other side reports success.

If any of these conditions cannot be met, stop before final DDL/implementation authorization and redesign the persistence boundary. Do not substitute two independent commits, compensating actions or a message queue and continue to call the operation atomic.

## 13. Status

This document is a test specification for the development team. It contains no executed tests, no claim of provider atomicity and no implementation authorization. The added Object reservation/Command, Geometry Asset, Relationship identity, Object Type semantics and typed-link cases are required before the corresponding V1 persistence claims can be accepted. Passing evidence is a mandatory gate before integrated Schema V2.0 is frozen.