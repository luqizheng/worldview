# World View P2-09-10 — SQLite Migration, Backup and Recovery Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended contract; implementation and provider-specific behavior require verification
- Scope: One local Project persistence unit comprising SQLite, immutable Asset Store files and file-backed Checkpoints
- Depends on: P2-09-01 Persistence Ownership; P2-09-06 Asset/Checkpoint Contract; P2-09-09 .NET SQLite Provider and Transaction Design; ADR-001/002/003 Amendment V1.3/005

## 1. Goals and non-goals

Guarantee understandable project open/upgrade/backup/restore/recovery behavior for a single-user, local-first desktop application.

V1 does not introduce distributed migrations, online multi-instance upgrades, a migration service, message queues, cross-database transactions, or an enterprise backup platform. One Project is upgraded and written by one application instance.

## 2. Project persistence unit

A consistent Project persistence unit contains:

1. one authoritative SQLite database file;
2. the project-local immutable Asset Store;
3. file-backed Checkpoint payloads and their metadata (metadata resides in SQLite; payloads reside in the Checkpoint store or designated Asset Store area);
4. the schema/migration ledger and required project format metadata.

AI Assistant and Core Domain tables may share the SQLite file but retain separate logical ownership and migration ownership. The application-level migration runner coordinates order; it does not own domain tables.

## 3. Migration ownership and ledger

- Core Domain supplies ordered Core Domain migrations and owns their SQL/schema semantics.
- AI Assistant supplies ordered AI migrations and owns its tables.
- A single application bootstrap runner executes both streams in a declared dependency order.
- One migration ledger records at minimum `module_id`, `migration_id` or monotonic module version, checksum, applied timestamp and application/schema compatibility metadata.
- Migration identity is immutable. Editing an already-applied migration is forbidden; corrections are new forward migrations.
- Before applying a migration, verify the stored checksum for every previously applied migration. A mismatch is a hard failure; do not continue or silently rewrite the ledger.
- Unknown newer schema/module versions fail closed with a clear compatibility error. Never downgrade automatically.
- A partially applied migration must be transactionally rolled back where SQLite supports transactional DDL for the statements used. Non-transactional filesystem steps require an explicit resumable protocol and completion marker.
- The runner must not infer successful initialization from the presence of tables. Empty, partially initialized, unknown, or inconsistent databases are classified explicitly and opened read-only/recovery mode or rejected; never auto-stamp them as current.

## 4. Bootstrap and upgrade sequence

Recommended sequence:

1. Acquire the single-process Project write/open guard.
2. Open SQLite with the selected provider and verify basic integrity, application ID and database identity.
3. Read the migration ledger without modifying it; classify fresh, supported-current, supported-upgrade, newer-unsupported, checksum-mismatch or partial/corrupt state.
4. Verify Asset Store directory identity and required metadata; do not delete or rewrite files during schema migration.
5. Apply required Core Domain migrations in order, then AI migrations in declared dependency order (or the explicit interleaving declared by the integration contract). Each migration is atomic where possible and records its checksum only after successful application.
6. Run post-migration structural checks: required tables/indexes/triggers, foreign-key check, schema invariants and module compatibility checks.
7. Run lightweight asset-reference integrity checks; deep hashing may be scheduled separately but must complete before a backup is declared verified.
8. Mark Project open for normal writes only after all required checks pass.

If any step fails, stop normal writes. Preserve the database and files for diagnosis; do not attempt an improvised reverse migration.

## 5. Transaction and migration safety

- Schema migrations run before normal domain operations and while the Project is unavailable for writes.
- Each migration has a deterministic ID and checksum and is applied once.
- Keep schema changes and ledger insertion in the same SQLite transaction whenever supported.
- Avoid long-running filesystem copy/hash operations inside a SQLite write transaction.
- If a migration must create or move files, use a staged protocol: write to a temporary path, verify, publish immutable file, then commit the database reference/marker; recovery must recognize and clean only unreferenced temporary/orphan files.
- Migration failure must leave either the pre-migration state or a clearly classified recoverable state. No silent partial success.

## 6. Backup contract

### 6.1 Consistency requirement

A backup is valid only when the SQLite database and all referenced immutable assets/required Checkpoint payloads represent one consistent Project point. Copying a live SQLite main file and the Asset Store independently is not sufficient, especially in WAL mode.

### 6.2 Recommended V1 backup procedure

1. Request a short application-level quiescence barrier: stop starting new write operations and wait for current short write transactions to finish. Do not wait for or hold transactions across AI generation or user interaction.
2. Use the provider-supported SQLite online backup mechanism or a documented safe checkpoint/close procedure. Do not manually copy only the main `.db` file while WAL may contain committed pages.
3. Capture a consistent inventory of immutable assets referenced by the database snapshot. Immutable assets can be copied by content-addressed identity; verify each copied file's size and cryptographic hash.
4. Include Checkpoint files only if the backup policy chooses to retain them. Since Checkpoints are derived, a backup may omit them if metadata is safely marked invalid/rebuildable on restore; otherwise include and verify them.
5. Write a backup manifest containing Project ID, database snapshot identity/hash, schema version, migration ledger/checksums, asset inventory and hashes, included Checkpoint inventory, application version and backup timestamp.
6. Publish the backup only after all required files and manifest are complete and verified. An incomplete backup is not presented as restorable.

### 6.3 Asset capture

Because published assets are immutable, the backup process may copy only assets referenced by the consistent database snapshot. If deduplication/content addressing is used, the manifest must identify each required content hash and relative path. Unreferenced orphan files need not be included in a normal backup, but must not be deleted as part of backup capture.

## 7. Restore contract

1. Restore into a new temporary Project directory; never overwrite the only source Project in place.
2. Verify manifest integrity, database hash, migration ledger/checksums and every required asset hash before opening for normal writes.
3. Open the restored database in validation mode; run SQLite integrity and foreign-key checks plus Core Domain/AI cross-module invariants.
4. If Checkpoints were omitted, mark all Checkpoint metadata invalid/rebuildable and reconstruct from authoritative inputs. If included, verify payload hashes and lineage/context before reuse.
5. Verify all authoritative asset references resolve. Missing/corrupt authoritative assets block normal project operation and require recovery; do not silently substitute empty geometry or defaults.
6. Only after verification succeeds, publish/rename the temporary restore directory as the active Project.

## 8. Crash recovery and startup classification

On startup, detect and classify:
- clean database with supported schema;
- SQLite WAL/journal recovery pending;
- interrupted migration;
- checksum mismatch;
- database integrity failure;
- missing/corrupt authoritative asset;
- missing/corrupt derived Checkpoint;
- orphan temporary asset/checkpoint files;
- newer unsupported schema.

Policy:
- Let SQLite/provider perform its documented journal/WAL recovery; never delete WAL/SHM/journal files as a generic repair.
- An interrupted transactional migration should appear unapplied after rollback; verify ledger and schema before retrying.
- If schema and ledger disagree, fail closed and preserve evidence.
- A missing/corrupt Checkpoint is recoverable by reconstruction and does not invalidate history.
- Missing/corrupt authoritative assets require explicit recovery; the project must not claim a valid reconstructed state.
- Orphan cleanup is conservative: prove absence of committed references before deleting, and use a retention grace period.
- Do not auto-downgrade, auto-rewrite checksums, or silently discard unknown tables/migrations.

## 9. WAL and durability policy

WAL mode, synchronous level, checkpoint cadence, busy timeout and backup API details must be selected and verified against the actual .NET provider/runtime and packaged SQLite native library on Windows and macOS. The architecture requires:
- one writer at a time and short write transactions;
- explicit handling of SQLITE_BUSY/locked conditions without blind replay of non-idempotent work;
- no concurrent use of one connection by multiple threads;
- checkpoint/backup procedures that include committed WAL content;
- documented durability trade-offs for the selected synchronous setting.

This contract does not assert that WAL or any specific PRAGMA has already been validated in the target application.

## 10. Required verification matrix

- fresh Project bootstrap applies Core and AI migrations in the declared order;
- each migration applies once and ledger checksum is stable;
- altered applied migration checksum fails closed;
- unsupported newer schema fails closed;
- injected failure midway through a migration leaves a classified recoverable state;
- process termination during a write transaction recovers to a valid committed-or-rolled-back state;
- backup taken with WAL activity restores all committed database changes and required assets;
- asset missing/hash mismatch blocks verified restore;
- omitted Checkpoints are rebuilt and do not affect formal history;
- corrupt Checkpoint is isolated from unrelated history and assets;
- restore into a new directory leaves source Project untouched;
- Windows and macOS packaged runtime behavior is verified with the selected provider/native library.

## 11. Open decisions / evidence gates

1. Exact provider/version and target framework compatibility.
2. Exact migration ledger DDL and module migration ordering.
3. Whether Checkpoint files are included or omitted by default in backup.
4. Concrete online backup API availability and behavior in the selected Microsoft.Data.Sqlite version.
5. WAL/PRAGMA values and power-loss durability target.
6. Project directory layout and atomic directory publication semantics on Windows/macOS.
7. Asset hash algorithm and manifest format (SHA-256 recommended, not frozen).
8. Cross-module transaction proof remains a separate P2-09-09 gate.

## 12. Status

This contract defines the recommended V1 operational behavior for migrations, backup, restore and crash recovery. It is not a provider-specific implementation, executable migration package, proof of atomicity, or implementation authorization.