# P2-09-12 V0.6 — Executable Coverage Matrix

- Version: 0.1
- Date: 2026-11-22
- Status: Repository-grounded coverage inventory; not a declaration of full P2-09-12 compliance
- Specification: `docs/architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.6.md`
- Implementation baseline: CD-02-G1 `ab6d00f7afc13ba352df0c3b92eecd3a146afd99`
- Test branch head at matrix creation: `e07f3668ef217ce6ed93e834892074fd507ef40a`
- CI evidence for that test head: run `44978789680`, Windows/macOS Debug/Release, 386/386 in each configuration

## 1. How to read this matrix

- **Implemented**: the exact requirement has a production path and a test that proves the required behavior.
- **Partial**: real code/tests cover only a subset of the acceptance clause.
- **Gap**: the production capability exists or is relevant, but no test proves the required behavior.
- **Blocked**: the production participant or persistence model required by the test does not exist.
- **Not implemented**: the corresponding production capability is absent from the inspected repository baseline.

A passing CI run proves the tests that were actually executed. It does not upgrade a Partial/Gap/Blocked row to Implemented.

## 2. Provider, ownership and transaction boundary

| TX | Requirement | Existing production/test evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-01 | Core Domain and AI repository receive the exact same connection and transaction instances | `SqliteConnectionAccess` serializes access to the package DB connection; no Application Commit Coordinator or AI Assistant repository implementation exists in `WorldView/src` | **Blocked** | Implement the real AI submission persistence participant and coordinator, then instrument exact connection/transaction identity |
| TX-02 | No nested transaction or hidden commit in either repository | Core Domain adapters use the shared package DB access path; no AI repository/coordinator boundary exists | **Blocked** | Prove all real participants use the coordinator-supplied transaction and cannot open/commit independently |
| TX-03 | FK enabled on every application connection | `PersistenceConcurrencyTests.ConnectionPolicy_EveryOpenedPackageConnectionEnablesForeignKeysAndBoundedTimeouts` opens two package DB connections, checks `foreign_keys=1`, `busy_timeout=5000`, `CommandTimeout=5`, and proves an invalid FK insert fails with SQLITE_CONSTRAINT_FOREIGNKEY | **Partial** | Enumerate and test every actual writer/read/migration/recovery connection type; current test proves two package connections from one factory |
| TX-04 | Writer ownership and separate independent reader connections | `PersistenceConcurrencyTests.Concurrent_CompositionRootAdapterPair_QueriesSucceedOnSharedConnection`, `Concurrent_ReadersAndWriters_NeverObservePartialMapStructure`, and shared-coordinator tests prove safe serialized access through one connection | **Partial** | Spec requires independent reads on separate connections; current readers use adapters over the same package connection. Add explicit separate-reader connection policy and tests, or revise the spec with an approved architecture decision |

## 3. Cross-module atomicity and crash reconciliation

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-10 | Successful Core + AI commit | No formal Command tables or AI submission tables in current Core Domain schema | **Blocked** | Implement both persistence participants and one shared transaction |
| TX-11 | Failure after Core write, before AI write rolls back both | No AI participant / cross-module coordinator | **Blocked** | Inject failure into the real integrated commit path |
| TX-12 | Failure after AI write, before commit rolls back both | No AI participant / cross-module coordinator | **Blocked** | Add failure injection after real AI evidence write |
| TX-13 | Nested Core operation failure rolls back entire cross-module transaction | Core-only mid-transaction rollback tests exist; no integrated AI transaction | **Partial** | Repeat through the real coordinator with AI rows in the same transaction |
| TX-14 | Constraint failure cannot leave AI success evidence | Core constraints are tested; AI success evidence schema is absent | **Blocked** | Add real cross-module constraint failure test |
| TX-15 | Cancellation before commit prevents partial commit | No final submission coordinator or AI evidence path | **Blocked** | Define cancellation barrier and test rollback after writes begin |
| TX-16 | Cancellation after successful commit preserves formal writes | No final submission coordinator or AI evidence path | **Blocked** | Test cancellation after commit but before response delivery |
| TX-20 | Process termination before commit rolls back both sides | No cross-module submission path or process-level crash harness | **Blocked** | Add subprocess crash-injection test against integrated commit |
| TX-21 | Crash after commit, before response reconciles without duplicates | No submission idempotency/outcome-unknown persistence | **Blocked** | Implement stable submission key and restart reconciliation |
| TX-22 | Repeated reconciliation is idempotent | No AI submission attempt/reconciliation model | **Blocked** | Implement and test real reconciliation |
| TX-23 | No blind replay while outcome is unknown | No AI submission attempt state machine | **Blocked** | Implement outcome-unknown gate and history reconciliation |

## 4. Write serialization and SQLite read behavior

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-30 | One application write coordinator serializes Project DB writes | `Concurrent_TwoWritersOnSameConnection_BothCommitFully`, `Concurrent_InsertProjectRace_ExactlyOneWinsWithDefinedDomainFailure`, `Concurrent_ReadersAndWriters_NeverObservePartialMapStructure`, and `Concurrent_PackageDbOperationsAndCoreAccess_ShareSingleCoordinator` exercise shared connection serialization and transaction completeness | **Partial** | Evidence is at the shared-connection coordinator boundary, not a distinct application-level submission/write coordinator |
| TX-31 | Independent reader connections remain isolated from writer transaction | No separate application read-connection implementation/test was found; current reader tests use the shared package DB access coordinator | **Gap** | Implement or explicitly supersede the separate-reader requirement, then test concurrent reads during a short write |
| TX-32 | Bounded busy/locked behavior and no unsafe retry | `BusyTimeout_ExternalWriterLock_FailsWithinBoundWithoutPartialWrite` holds a real external SQLite write lock, observes SQLITE_BUSY within a bounded interval, and proves no partial write. Production default timeout and command timeout are asserted before the test shortens the contention window | **Partial** | No automatic retry exists by design; no transient retry/idempotency proof. Add retries only after stable operation keys exist |

## 5. Migration and schema integrity

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-40 | Fresh bootstrap creates all Core + AI tables, indexes, triggers and FKs | `SqlitePackageDatabaseTests.FreshFile_BootstrapCreatesMetadataAndLedger`, `CoreDomainPersistenceTests.FreshBootstrap_AppliesCoreDomainMigrations`, `Cd02ObjectPersistenceTests.ProjectCreation_SeedsTwelveTypes_TwelveAllocators`, and migration concurrency tests prove current package/Core bootstrap | **Partial** | Current schema is Core Domain only; no AI Assistant production migrations, packaged runtime validation, or exhaustive expected-object inventory test |
| TX-41 | Repeated bootstrap is idempotent | `SqlitePackageDatabaseTests.Revalidation_AppliesNoPendingMigrations_AndPasses`, `CoreDomainPersistenceTests.Reopen_IsIdempotentAndPassesValidation`, `PersistenceConcurrencyTests.Migration_SecondValidation_AppliesNothing` | **Implemented for current Core/package schema** | Repeat after AI migrations are introduced |
| TX-42 | Checksum mismatch and unknown migration fail closed | `SqlitePackageDatabaseTests.LedgerChecksumMismatch_FailsClosed`, `UnknownLedgerMigration_FailsClosed`, `CoreDomainPersistenceTests.TamperedLedgerChecksum_FailsClosed`, `UnknownLedgerMigration_FailsClosed` | **Implemented for current migration ledger** | Add packaged-runtime migration checks when the desktop bundle is available |
| TX-43 | Full foreign-key and cross-table invariant audit | `Cd02AssetPersistenceTests.SchemaAudit_ForeignKeyCheckAndIntegrityCheck_NoViolationsAfterObjectAssetLink`; `Cd02AggregateVectorConformanceTests.Suite_Has55Vectors_AllExecuted`; Object, Map, Branch, reservation and Initial State persistence tests cover selected invariants | **Partial** | No single full audit covers every invariant enumerated in P2-09-12 §8; map each invariant to a named test and add missing cross-table audit cases |

## 6. Asset, Checkpoint and backup/restore

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-50 | Publish immutable Asset before DB commit and atomically persist Core + AI references | `FileAssetStoreTests.PutAsync_PublishesContentAddressedImmutableBytes`, `AssetIntegrityVerificationTests` (Verified/Missing/Corrupt), and `Cd02AssetPersistenceTests` (metadata, Verified-only Definition link, manifest/link/hash consistency) | **Partial** | No AI `submission_asset_refs`, no integrated staged publication coordinator, no formal Command Asset link |
| TX-51 | AI submission Asset FK and retention | Core Asset + Definition Asset FK exist; AI submission refs table does not | **Blocked** | Add AI reference schema and prove missing/deletion FK behavior |
| TX-52 | DB failure after Asset publication leaves safe orphan | `Cd02AssetPersistenceTests.OrphanScan_IdentifiesUnreferencedAssets` and FileAssetStore immutability tests prove orphan identification and immutable bytes | **Partial** | No injected crash/failure between file publication and DB commit, nor safe cleanup/reconciliation proof |
| TX-53 | Checkpoint publication failure cannot corrupt formal history | Checkpoint persistence/publication implementation not present in the inspected source/schema | **Not implemented** | Implement rebuildable Checkpoint publication and failure/recovery tests |
| TX-54 | Consistent Project backup/restore preserves DB + Asset Store | No Project backup/restore implementation or integration test found | **Not implemented** | Define WAL-safe backup procedure and restore validation including Asset hashes and incomplete Checkpoints |

## 7. Object, Geometry, Relationship and History acceptance

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-55 | Reservation + formal Object creation + Command + AI evidence are atomic | `Cd02ObjectPersistenceTests.InsertObject_MidTransactionFailure_RollsBackCompletely_RetryReusesSameReservation`, `Bind_MidTransactionFailure_RollsBack_RetrySameIdentity`, reservation idempotency/concurrency and lifecycle tests prove Core-only reservation/Object rollback | **Partial** | No formal Command persistence and no AI submission evidence; extend the same failure points through the real integrated coordinator |
| TX-56 | Geometry Command and typed Asset link commit atomically with AI evidence | `Cd02AssetPersistenceTests.ManifestConsistency_FullChain_RealDb`, `LinkDefinitionAsset_RequiresVerifiedAsset_AndRechecksMutableStateInTransaction`, and Definition Asset FK/role tests prove Definition-context Asset linking | **Partial** | No Command Asset/Object reference tables or AI evidence; Initial State/Command owner contexts are not implemented |
| TX-57 | Relationship preflight ID and sealed payload remain stable across retry | No formal relationship entity, preflight service, or Command persistence in current Core Domain schema | **Blocked** | Implement relationship identity and proposal preflight before transaction proof |
| TX-58 | Relationship termination origin and Branch scope | No formal relationship persistence/history command implementation | **Blocked** | Implement relationship lifecycle and branch-local termination semantics |
| TX-59 | Object Type semantics pinning survives registry changes | `Cd02ObjectPersistenceTests.ReadObject_UnknownPin_FailClosed`, Object Type registry descriptor/pin validation, and persisted Object schema/semantics pin fields | **Partial** | Unknown pin rejection is tested; compatibility across actual registry upgrades and explicit migration policy are not |
| TX-60 | Required typed Command Object/Asset links are enforced | Definition Asset link is enforced and tested; no `core_command_object_ref` or `core_command_asset` production tables | **Partial** | Add Command typed references and negative missing/cross-Project/wrong-History tests |
| TX-61 | History-local Object activation/deactivation | No Command/State Change runtime activation persistence found | **Not implemented** | Implement History-local runtime activity and tests across two Maps/Branches |
| TX-62 | Relationship endpoint activity policy | Relationship model/Command persistence absent | **Not implemented** | Implement active-required/dormant-allowed policies and Branch-local tests |

## 8. Field Dataset / Field Revision acceptance

The inspected baseline has no Field Dataset/Field Revision production types, migrations, repositories or Checkpoint fingerprint implementation. The P2-09-34 manifest schema and ME-05-09 fixtures are design artifacts, not a persistence implementation.

| TX | Requirement | Existing evidence | Status | Remaining work |
|---|---|---|---|---|
| TX-65 | Dataset uniqueness and Map ownership | No `core_map_field_dataset` table or Field Dataset service | **Not implemented** | Implement Map-owned dataset identity and unique Map/field key |
| TX-66 | Revision typed Asset ownership and immutability | Generic Core Asset byte verification exists; no Field Revision owner or immutability constraints | **Not implemented** | Implement immutable revisions, typed Asset ownership and retention FK |
| TX-67 | Same-Dataset current pointer and CAS | No Field Dataset current pointer or revision CAS | **Not implemented** | Implement same-dataset FK + expected-revision CAS |
| TX-68 | Map-wide retroactive Branch/Historical Time semantics | No Field Baseline resolver or Field Revision integration | **Not implemented** | Integrate current Field Revision with all History/Time contexts of owning Map |
| TX-69 | Cross-Map isolation despite byte deduplication | No Field Dataset identity or current pointer | **Not implemented** | Prove Map ownership isolation with shared immutable Asset bytes |
| TX-70 | Asset publication crash/orphan recovery for Field Revision | Generic orphan scan exists; no Field Revision publication transaction | **Not implemented** | Add failure injection around revision insert/current-pointer update and safe orphan reconciliation |
| TX-71 | Field Baseline-aware Checkpoint fingerprint | No Field Baseline fingerprint or Checkpoint implementation | **Not implemented** | Add dependency-aware fingerprinting and stale-publication prevention |

## 9. Runtime and platform proof gaps

The current repository CI uses .NET 11 Windows/macOS build-and-test jobs and Microsoft.Data.Sqlite 11.x. P2-09-12 V0.6 names .NET 10, Avalonia 12, and bundled SQLitePCLRaw `e_sqlite3`. CI also installs .NET 10 runtime alongside the .NET 11 SDK, but the project target and test assemblies are `net11.0`.

The current CI proves library/application build and test execution on Windows and macOS runners. It does **not** prove:
- packaged desktop startup on supported RIDs/architectures;
- the exact bundled native SQLite binary used by the distributed app;
- runtime SQLite version and JSON1/FTS5/R*Tree feature availability;
- WAL durability and `synchronous` crash-loss trade-offs;
- process-termination crash recovery;
- consistent Project backup/restore.

Before claiming provider-proof completion, reconcile the .NET target mismatch in the spec and test the actual packaged runtime, not only the solution test host.

## 10. Immediate next slices

1. Finish and retain the provider-only slice: TX-03 partial, TX-32 bounded failure partial, TX-43 selected audit partial; keep TX-04/TX-31 explicitly open.
2. Add a named test mapping for every remaining TX-43 invariant instead of treating the 55 Building/Cave vectors as a full database audit.
3. Implement the real AI submission persistence participant and Application Commit Coordinator before writing TX-01/02/10–23.
4. Implement formal Command typed Object/Asset references before claiming TX-55/56/60 complete.
5. Implement Field Dataset/Revision persistence and Field Baseline-aware Checkpoint invalidation before attempting TX-65–71.
6. Add packaged Windows/macOS runtime, native SQLite feature, crash recovery and backup/restore proof.

## 11. Non-claims

This matrix does not claim P2-09-12 V0.6 complete. It distinguishes real Core-only persistence evidence from the still-missing AI/Core atomic transaction, relationship/Command, Checkpoint, Field Dataset, packaged runtime and durability capabilities.
