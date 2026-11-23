# P2-09-12 V0.7 — TX-Case Coverage Matrix

- Version: 0.1
- Date: 2026-11-22
- Status: Active implementation coverage inventory; not a completion claim
- Specification: `docs/architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.7.md`
- Current test branch: `test/p2-09-12-provider-transaction-proof`
- Current candidate: `da59981fe39f4f5ef81cf5642b9eedf102dfa7dc`
- Previous validated candidate: `415524861e63ab3adb15e84f5eaaaeaf1d7decd5`

## Status vocabulary

- **PASS (current)** — the exact TX acceptance clause has an executable test and the current candidate's required CI matrix passed.
- **PARTIAL** — some lower-level behavior is tested, but one or more required acceptance clauses remain uncovered.
- **BLOCKED** — required production participant/capability does not exist.
- **GAP** — capability exists or is expected but no adequate test/implementation was found.
- **NOT SPECIFIED** — the V0.7 document has no case with that number; no case is inferred.

A test name is evidence only for the behavior asserted by that test. A passing test suite does not promote an entire TX section to PASS.

## 1. Connection and transaction ownership

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-01 Shared connection identity | No Application Commit Coordinator and no AI submission repository. Current Core Domain adapters share a per-package-database `SqliteConnectionAccess`, but this is not the cross-module final commit boundary. | BLOCKED | Implement real AI submission participant and coordinator; instrument exact connection and transaction object identity. |
| TX-02 No nested transaction / hidden commit | No cross-module coordinator/repository call path exists. | BLOCKED | Add transaction-aware repository APIs and prove only coordinator commits/rolls back. |
| TX-03 Foreign keys on every connection | `PersistenceConcurrencyTests.ConnectionPolicy_EveryOpenedPackageConnectionEnablesForeignKeysAndBoundedTimeouts` opens two separate `SqlitePackageDatabase` instances, checks `foreign_keys=1`, `busy_timeout=5000`, `CommandTimeout=5`, and verifies an invalid FK insert is rejected. `PersistenceDomainIntegrityTests` also proves real FK violations are rejected. Candidate `e07f366`: Windows/macOS Debug/Release all passed (386/386 per configuration). | PASS for every connection factory currently implemented | Reopen this case if distinct read, recovery or other connection factories are introduced; packaged native-runtime verification remains a cross-cutting gate. |
| TX-04 Connection ownership / threading | Same-connection concurrency tests exercise the re-entrant per-connection coordinator. No independent read connection is implemented; current read adapters use the same package DB connection. | PARTIAL | Reconcile V0.7's independent-reader requirement with the single-connection architecture; do not claim TX-04 complete. |

## 2. Atomicity and failure injection

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-10 Successful Core + AI commit | No AI submission persistence boundary or Application Commit Coordinator. | BLOCKED | Implement real participants and one shared transaction. |
| TX-11 Failure after Core write, before AI write | Core-only rollback tests exist; no AI participant to assert cross-module rollback. | PARTIAL | Add failure injection through the real coordinator after TX-01/02 exist. |
| TX-12 Failure after AI write, before commit | No AI submission repository/coordinator. | BLOCKED | Implement and inject immediately before the single commit. |
| TX-13 Failure inside nested Core operation | `PersistenceConcurrencyTests.Transaction_MidwayUniqueViolation_RollsBackEntirelyAndConnectionStaysUsable` and `Cd02ObjectPersistenceTests.InsertObject_MidTransactionFailure_RollsBackCompletely_RetryReusesSameReservation` cover Core-only atomic rollback. | PARTIAL | Prove the same failure rolls back the integrated AI/Core transaction. |
| TX-14 Constraint failure | Core Domain unique/FK/check constraints and transaction rollback have tests (`PersistenceDomainIntegrityTests`, `Cd02ObjectPersistenceTests`, `Cd02AssetPersistenceTests`). No AI success evidence exists to assert absent. | PARTIAL | Repeat real FK/UNIQUE/CHECK/trigger failures through the integrated coordinator and inspect both modules. |
| TX-15 Cancellation before commit | No integrated submission cancellation write barrier/coordinator exists. | BLOCKED | Implement cancellation barrier and prove full rollback before commit. |
| TX-16 Cancellation after commit | No AI submission outcome/response reconciliation boundary exists. | BLOCKED | Prove committed formal writes remain committed when response delivery is cancelled. |

## 3. Crash and uncertain-outcome handling

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-20 Process termination before commit | No process-kill harness around a cross-module submission transaction. | BLOCKED | Add packaged-process crash injection after both modules write but before commit. |
| TX-21 Process termination after commit, before response | No stable AI submission idempotency/reconciliation boundary. | BLOCKED | Add crash-after-commit test and exact request-hash reconciliation. |
| TX-22 Repeated reconciliation | No AI submission reconciliation service or immutable attempt evidence. | BLOCKED | Implement idempotent reconciliation and prove repeated calls do not mutate authorization/proposal evidence. |
| TX-23 No blind replay | No `outcome_unknown` submission state or replay gate. | BLOCKED | Implement state machine and prove unresolved attempts cannot be resubmitted blindly. |

## 4. Write serialization and read behavior

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-30 Single writer coordinator | `PersistenceConcurrencyTests.Concurrent_TwoWritersOnSameConnection_BothCommitFully`, `Concurrent_InsertProjectRace_ExactlyOneWinsWithDefinedDomainFailure`, and `Concurrent_PackageDbOperationsAndCoreAccess_ShareSingleCoordinator` prove serialization at the shared physical-connection coordinator. | PARTIAL | V0.7 requires an application write coordinator over independent requests; decide whether the per-connection coordinator is the approved equivalent and document that decision. |
| TX-31 Reader isolation | No independent application read connection; concurrent readers use the same serialized connection. | GAP | Implement separate reader ownership or formally revise the requirement. If separate readers are adopted, decide journal/WAL and backup/restore policy first. |
| TX-32 Busy/locked handling | `PersistenceConcurrencyTests.BusyTimeout_ExternalWriterLock_FailsWithinBoundWithoutPartialWrite` holds an external write transaction, expects bounded `SQLITE_BUSY`, and checks no competing row was partially written. `SqliteConnectionAccess` now sets `DefaultTimeout=5` as well as `PRAGMA busy_timeout=5000`; without this, the observed command wait was about 30 seconds. Candidate `da59981`: Windows/macOS Debug/Release all passed (385/385 per configuration). | PARTIAL | The controlled lock-conflict failure is proven. No automatic retry is implemented; do not retry non-idempotent writes until a stable operation/submission key exists. The spec's transient-busy/retry/idempotency clauses remain open. |

## 5. Migration and schema verification

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-40 Fresh bootstrap | `SqlitePackageDatabaseTests.FreshFile_BootstrapCreatesMetadataAndLedger`, migration transaction tests, and Core Domain end-to-end tests cover current Core Domain bootstrap. No AI Assistant module migrations exist. | PARTIAL | Verify full expected schema objects and eventual Core + AI migration dependency order. |
| TX-41 Idempotent bootstrap | `SqlitePackageDatabaseTests.Revalidation_AppliesNoPendingMigrations_AndPasses`, `NewMigration_AppliedOnRevalidation`, and `PersistenceConcurrencyTests.Migration_SecondValidation_AppliesNothing`. | PARTIAL | Add complete schema/checksum snapshot assertions and include AI module when implemented. |
| TX-42 Checksum mismatch / unknown migration | `SqlitePackageDatabaseTests.LedgerChecksumMismatch_FailsClosed` and `UnknownLedgerMigration_FailsClosed`. | PASS for current Core migration ledger behavior | Extend to full packaged startup and any future module migration source. |
| TX-43 Foreign-key and invariant audit | `PersistenceDomainIntegrityTests`, `Cd02AggregateVectorConformanceTests`, `Cd02ObjectPersistenceTests`, and `Cd02AssetPersistenceTests` cover selected lineage, scope, reservation, initial-state and typed Asset invariants. `Cd02AssetPersistenceTests.SchemaAudit_ForeignKeyCheckAndIntegrityCheck_NoViolationsAfterObjectAssetLink` additionally runs real SQLite `PRAGMA foreign_key_check` and `PRAGMA integrity_check` after an Object + Asset typed-link flow. Candidate `e07f366`: Windows/macOS Debug/Release all passed (386/386 per configuration). | PARTIAL | Build a one-to-one audit assertion inventory for every V0.7-listed invariant, including relationship temporal rules, command links and checkpoint coherence. |

## 6. Asset and Checkpoint consistency

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-50 Asset publication before DB commit | FileAssetStore byte/hash verification and Core Asset metadata/Definition-link tests exist. No AI `submission_asset_refs` or integrated staged-publication coordinator exists. | PARTIAL | Implement temporary write → verify → immutable publish and the short Core + AI DB reference transaction. |
| TX-51 Asset FK and retention | Core Asset and Definition typed-link FK behavior exists; AI submission reference table does not exist. | PARTIAL | Add AI submission Asset FK and deletion-retention tests after AI schema exists. |
| TX-52 DB failure after Asset publication | Orphan scan identifies unreferenced Assets, but no end-to-end injected DB failure after file publication and safe orphan reconciliation proof was found. | GAP | Inject DB failure after immutable publish; prove orphan retention and safe cleanup policy. |
| TX-53 Checkpoint publication failure | No current Checkpoint persistence/publication implementation was found in the active Core Domain source. | GAP | Implement rebuildable Checkpoint publication and failure-injection tests without changing history authority. |
| TX-54 Project backup and restore | No approved backup/restore implementation or WAL-consistent restore test was found. | GAP | Define backup procedure and verify DB + immutable Asset consistency on Windows/macOS. |

## 7. Object, Geometry and Relationship cases

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-55 Bound reservation and formal Object creation atomicity | `Cd02ObjectPersistenceTests.InsertObject_MidTransactionFailure_RollsBackCompletely_RetryReusesSameReservation` proves Core-only reservation/identity/object rollback and retry reuse. | PARTIAL | Add formal Command and AI submission evidence to the same transaction. |
| TX-56 Geometry Command typed Asset reference atomicity | `Cd02AssetPersistenceTests` proves Definition Asset links, Verified-state recheck, typed manifest consistency and transaction-time metadata checks. No formal Command Asset/Object reference persistence path exists. | PARTIAL | Implement Command typed links and integrated failure injection; do not equate Definition Asset integrity with Command publication. |
| TX-57 Relationship ID preflight and sealed payload | No relationship preflight/Command submission implementation found in active source. | GAP | Implement canonical preflight identity and immutable proposal binding. |
| TX-58 Relationship termination origin and Branch scope | Relationship descriptors/catalog artifacts exist, but no formal relationship history/termination persistence service or Command path found. | GAP | Implement temporal relationship persistence and ancestor/Branch visibility tests. |
| TX-59 Object Type semantics pinning | Registry pin validation, unknown type fail-closed, and persisted unknown-pin rejection tests exist (`ObjectTypeRegistryTests`, `Cd02ObjectPersistenceTests.ReadObject_UnknownPin_FailClosed`). | PARTIAL | Add application registry upgrade/reconstruction tests proving historical pinned semantics are not silently substituted. |
| TX-60 Required typed-link enforcement | Definition Asset links are checked against Building/Cave manifests on link insertion, and Definition CAS preserves every existing link. However, the new manifest and its complete link set can currently be committed in separate transactions; the API does not require every newly added manifest Asset to have a link at Definition commit. No formal Command Object/Asset link tables or AI evidence exist in active source. | PARTIAL — current link integrity is defensive but publication is not atomic | Add one Definition + complete Asset-link publication operation; later implement required Command typed links and cross-Project/wrong-History negative tests. |
| TX-61 History-local Object activation/deactivation | No formal Command-based History-local activation/deactivation persistence found. | GAP | Implement and prove Map/Branch-local behavior. |
| TX-62 Relationship endpoint activity policy | No relationship temporal persistence/service found. | GAP | Implement and prove active-required vs dormant-allowed endpoint policy. |

## 8. Field Dataset / Field Revision publication

| Case | Current evidence | Status | Remaining work |
|---|---|---|---|
| TX-65 Field Dataset uniqueness and Map ownership | ME-05-01 V0.5 contract and validation vectors exist; no `core_map_field_dataset` implementation or test found. | GAP | Implement Map-owned dataset identity and unique Map/field-key constraint. |
| TX-66 Field Revision Asset ownership and immutability | Asset byte verification exists, but no Field Revision entity/table or typed Asset ownership path exists. | GAP | Implement immutable revision metadata and exact Asset hash/ownership validation. |
| TX-67 Current pointer and CAS | No Field Dataset current-revision pointer/CAS implementation found. | GAP | Implement same-Dataset FK and stale revision compare-and-swap tests. |
| TX-68 Map-wide retroactive semantics | No Field Revision resolution in Branch/History workspace context found. | GAP | Implement Map-wide current-revision resolution across all Branches and Historical Time targets. |
| TX-69 Cross-Map isolation | Asset content-addressed deduplication exists, but Field Dataset ownership isolation is not implemented. | GAP | Prove same bytes do not share Dataset identity or propagate revision pointers across Maps. |
| TX-70 Asset publication crash/orphan recovery | Asset orphan scanning exists; Field Revision publication and crash/reconciliation orchestration do not. | GAP | Add staged publication failure injection and safe orphan reconciliation. |
| TX-71 Field Baseline-aware Checkpoint fingerprint | No Field Dataset dependency or Field Baseline fingerprint integration found. | GAP | Add dependency-aware fingerprinting and stale publication revalidation. |

## 9. Numbering gaps in V0.7

The V0.7 specification does not define TX-05 through TX-09, TX-24 through TX-29, TX-33 through TX-39, TX-44 through TX-49, TX-63 or TX-64. This matrix does not infer or create requirements for those numbers.

## 10. Cross-cutting release blockers

1. **AI/Core atomic commit boundary absent:** TX-01/02 and cross-module TX-10–23 cannot pass until real AI submission persistence and an Application Commit Coordinator exist.
2. **Reader architecture mismatch:** TX-04/TX-31 require independent read connections, while current implementation serializes all operations on one package DB connection. Decide whether to implement readers or formally revise the spec; if WAL is needed, close backup/restore first.
3. **Packaged runtime proof remains open:** P2-09-12 V0.7 now aligns the target with repository .NET 11, Avalonia 12.1.6 and Microsoft.Data.Sqlite 11.0.0. CI still does not prove packaged Avalonia startup or the resolved native SQLite runtime/features on each supported RID; capture the resolved SQLitePCLRaw version in a lock/restore artifact.
4. **Packaged native runtime absent from CI proof:** current CI builds/tests solution projects; it does not prove Avalonia packaged startup or SQLitePCLRaw e_sqlite3 feature availability on every supported RID.
5. **Field Dataset not implemented:** TX-65–71 are unimplemented; Asset byte verification alone does not satisfy them.
6. **No full TX acceptance:** no claim of P2-09-12 completion is valid until all critical clauses are proven or formally superseded.

## 11. CI evidence

| Commit | Scope | Windows | macOS |
|---|---|---|---|
| `415524861e63ab3adb15e84f5eaaaeaf1d7decd5` | TX-32 test + command timeout alignment | Debug/Release passed, 384 tests per configuration | Debug/Release passed, 384 tests per configuration |
| `da59981fe39f4f5ef81cf5642b9eedf102dfa7dc` | Adds TX-03 two-connection FK/timeout test | Debug/Release passed, 385 tests per configuration | Debug/Release passed, 385 tests per configuration |
| `e07f3668ef217ce6ed93e834892074fd507ef40a` | Adds TX-43 global FK/integrity audit after Object + Asset link | Debug/Release passed, 386 tests per configuration | Debug/Release passed, 386 tests per configuration |

The exact current candidate SHA `da59981` has completed the full Windows/macOS Debug/Release matrix. The run proves the listed test suites on those CI hosts; it does not prove packaged Avalonia startup or the cross-module TX-01/02/10–23 boundary.
