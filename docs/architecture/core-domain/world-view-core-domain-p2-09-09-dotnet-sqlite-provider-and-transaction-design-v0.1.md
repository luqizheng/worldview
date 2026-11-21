# World View Core Domain — P2-09-09 .NET SQLite Provider and Transaction Design

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended technology baseline — package/runtime compatibility and transaction proof still required
- Target: C# + Avalonia desktop application on Windows and macOS

## 1. Executive recommendation

Use **Microsoft.Data.Sqlite 11.0.0** as the application SQLite ADO.NET provider, with its default **SQLitePCLRaw.bundle_e_sqlite3** native SQLite bundle, subject to the application's final .NET target framework and Windows/macOS packaging tests.

Use direct ADO.NET and explicit SQL repositories for the Core Domain and AI Assistant persistence modules. Do not make EF Core the primary persistence abstraction for V1.0.

This is a recommended package/architecture baseline, not proof that the final application has passed cross-platform or atomicity tests.

## 2. Evidence and compatibility

The Microsoft documentation describes Microsoft.Data.Sqlite as a lightweight ADO.NET provider that can be used independently or underneath EF Core. The main package uses SQLitePCLRaw and defaults to a bundled SQLite build; the default bundle supplies a consistent SQLite version across supported platforms and includes FTS4, FTS5, JSON1 and R*Tree.

As of 2026-11-21, NuGet lists Microsoft.Data.Sqlite 11.0.0 as the latest stable release, published 2026-11-10. EF Core 11 breaking-change documentation states that Microsoft.Data.Sqlite 11.0 targets .NET 10.0 only. Therefore the application should target .NET 10.0 if adopting version 11.0.0; otherwise choose the latest supported Microsoft.Data.Sqlite patch aligned with the actual target framework rather than forcing a runtime upgrade solely for the database package.

References:
- Microsoft.Data.Sqlite overview: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
- Custom SQLite versions and default bundle: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions
- NuGet package: https://www.nuget.org/packages/Microsoft.Data.Sqlite/
- EF Core 11 breaking changes: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/breaking-changes

## 3. Why direct Microsoft.Data.Sqlite ADO.NET

Advantages for this application:

1. One explicit SqliteConnection can be opened by the application persistence boundary and passed into the Core Domain transaction context and AI submission evidence repository.
2. One SqliteTransaction can be passed to every command participating in the final authorized submission.
3. SQL schema, triggers, constraints and migrations remain explicit and reviewable.
4. The provider is lightweight and does not require an ORM model to own the database lifecycle.
5. The same provider/native SQLite bundle can be packaged with the desktop app on Windows and macOS, subject to RID-specific verification.

EF Core remains an available option for future read models or ordinary CRUD if it demonstrates material productivity benefits. It should not be introduced in V1.0 merely for abstraction. Mixing EF Core contexts with direct ADO.NET inside the critical commit path would add transaction-sharing complexity without a demonstrated need.

## 4. Native SQLite packaging

Recommended default:

- Package: Microsoft.Data.Sqlite 11.0.0, when the app targets net10.0.
- Native bundle: default SQLitePCLRaw.bundle_e_sqlite3 dependency supplied by the main Microsoft.Data.Sqlite package.
- Do not separately add a competing SQLitePCLRaw provider or system-SQLite bundle unless a concrete platform requirement proves it necessary.

The bundled native library is preferred over relying on each operating system's system SQLite because it gives a consistent engine/extension baseline across Windows and macOS. This reduces differences in JSON1, FTS5, R*Tree, compile options and engine behavior.

Package/runtime version must be pinned centrally and upgraded deliberately. The build must record the actual SQLite runtime version and compile options in diagnostics so bug reports can be tied to the engine shipped with that application build.

## 5. Persistence architecture

Use a small application-owned persistence boundary:

- ProjectDatabase owns database file location, connection creation, connection configuration and lifecycle.
- ApplicationWriteCoordinator serializes short write units and creates the connection/transaction context.
- Core Domain repositories and services own all Core Domain writes.
- AI Assistant repositories own only AI tables.
- Application Commit Coordinator coordinates the exact authorized submission but does not issue direct Core Domain SQL or become a third data owner.
- MigrationRunner invokes module-owned migrations in deterministic order.

The critical write path must receive an explicit transaction context containing the already-open SqliteConnection and SqliteTransaction. Services must not open a second connection, start a nested independent transaction, or silently commit early.

This is a transaction-sharing design requirement. It must be verified with an integration test using the exact provider and repository/service composition before the architecture claims atomicity.

## 6. Atomic submission transaction

The final formal application transaction is short and synchronous with respect to database ownership:

1. Open one SqliteConnection.
2. Begin one SqliteTransaction.
3. Verify exact human authorization, proposal hash, target History context and preconditions.
4. Invoke Core Domain Domain Services using the same connection and transaction; those services validate and persist formal Commands and any required Object insertion/reservation transition.
5. Persist the minimum AI-owned submission evidence and returned Core Domain references using the same connection and transaction.
6. Commit once.
7. Dispose transaction and connection.

If any step fails before commit, roll back the whole transaction. The AI Assistant does not issue direct SQL against Core Domain-owned tables; Core Domain services remain the only writers.

Do not hold the transaction open during LLM calls, candidate generation, geometry processing, validation, tool execution, asset generation or full history reconstruction. Those phases occur before or after the short commit transaction.

A successful commit means the formal Command batch and submission evidence were persisted. It does not mean history reconstruction or playback succeeded; those remain separate Core Domain outcomes.

## 7. Connection and concurrency rules

Microsoft.Data.Sqlite connection, command and reader objects are not thread-safe. Therefore:

- Never use one SqliteConnection, SqliteCommand or SqliteDataReader concurrently from multiple threads.
- The critical transaction is owned by one execution flow and passed explicitly through service calls.
- A single application-level write coordinator serializes write transactions; no distributed lock is needed.
- Independent read operations may use their own short-lived connections.
- Do not share a connection object across background Task workers.
- Do not keep a database transaction open while waiting for AI or user interaction.

SQLite permits concurrent readers but only one transaction can have pending writes. Keep write units short and use bounded busy timeout/retry behavior for transient lock contention. Retries must never blindly repeat a potentially committed business operation; use the existing stable submission/reservation identities and reconcile uncertain outcomes first.

## 8. SQLite configuration baseline

Recommended settings to validate in the prototype:

- Foreign keys enabled on every connection.
- WAL journal mode for normal desktop operation, subject to backup/restore and filesystem tests.
- Synchronous durability setting selected explicitly after crash/power-loss testing; do not weaken durability for benchmark gains without a documented product decision.
- Busy timeout configured to a bounded value suitable for a local desktop app.
- Private cache/default cache; do not enable shared-cache mode as a shortcut for transaction sharing.
- Explicit transaction mode selected for the short write path and tested under expected read/write patterns.
- Database and Asset Store must be backed up as one consistent Project unit.

Exact PRAGMA values remain a P2-09 implementation test result, not frozen by this document.

## 9. Migration strategy

Use explicit, versioned SQL migration files with module ownership:

- Core Domain migrations are authored and owned by Core Domain.
- AI Assistant migrations are authored and owned by AI Assistant.
- Application bootstrap runs both sets in a deterministic order.
- One migration ledger records module ID, migration version, checksum and applied time.
- Reapplying an already applied migration is a no-op only when its checksum matches.
- Unknown, modified or partially applied migrations fail closed and require recovery; never silently continue.
- A migration must be transactional where SQLite/provider behavior allows. File/asset migrations require a documented staged publication and recovery protocol.
- Do not use EF Core migrations as a second competing migration authority for these modules.

The migration ledger and bootstrap implementation are specified in the later integrated physical schema work.

## 10. Mandatory provider/transaction proof tests

Before freezing provider and final DDL, build a minimal executable test harness against the exact app target framework and package lock:

1. Windows x64: load bundled native SQLite and execute core SQL features.
2. macOS Apple Silicon: load bundled native SQLite and execute core SQL features.
3. macOS Intel if that architecture remains a supported distribution target.
4. Verify SQLite runtime version and FTS5, JSON1, R*Tree availability.
5. Verify foreign key enforcement on every newly opened connection.
6. Verify triggers, CHECK constraints, partial indexes, deferred constraints if used, and JSON validation functions.
7. Open one connection, begin one transaction, invoke a fake Core Domain service write and an AI repository write through the same transaction, then commit; verify both are visible.
8. Repeat with an injected failure after the Core Domain write but before AI evidence write; verify neither is committed.
9. Repeat with failure after AI evidence write but before commit; verify neither is committed.
10. Verify nested service calls do not create or commit an independent transaction.
11. Verify no repository accidentally opens a second connection during the critical commit path.
12. Simulate process termination before commit and after commit; reconcile uncertain outcomes by stable IDs without duplicate Command insertion.
13. Verify WAL backup/restore and database + Asset Store recovery as one Project unit.
14. Verify concurrent readers do not use the transaction's connection object and the write coordinator serializes writes.

Passing unit tests against SQLite CLI or Python sqlite3 is not sufficient. The tests must run through Microsoft.Data.Sqlite and the packaged native library on target platforms.

## 11. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Different native SQLite builds on Windows/macOS | Use the bundled e_sqlite3 build; pin package versions and verify runtime engine |
| Provider/runtime target mismatch | Align package major version with app TFM; verify Avalonia target compatibility before pinning |
| False atomicity from separate connections | Explicitly pass one connection and transaction through all commit participants; test with injected failures |
| Connection used concurrently | Single-owner transaction flow; separate connections for independent reads |
| Long transaction causes lock contention | Keep only final persistence work in transaction; all generation/reconstruction outside |
| Migration drift | Module-owned SQL migrations plus checksum ledger and fail-closed behavior |
| WAL/asset backup inconsistency | Consistent backup protocol for database and immutable Asset Store |
| Silent replay behavior change after upgrade | Persist Command semantics identifiers and retain/migrate historical handlers explicitly |

## 12. Status

Recommended provider direction: **Microsoft.Data.Sqlite + bundled SQLitePCLRaw e_sqlite3 + direct ADO.NET + explicit module-owned SQL migrations**.

Not yet frozen:
- final .NET target framework and Avalonia compatibility;
- exact package lock and native SQLite build;
- WAL/durability PRAGMA values;
- actual transaction-sharing proof;
- physical schema, migration ledger DDL and backup implementation.

No implementation authorization is granted by this design note.