# World View AI Assistant — Database Implementation Handoff V1.0

- Status: Recommended engineering handoff; implementation is gated on Core Domain schema inspection
- Applies to: C# desktop application, SQLite, single-user/local-first
- Schema baseline: Unified Database Schema V1.14
- Required inputs: unified V1.14 SQL bootstrap, V1.14 test suite, actual Core Domain migrations

## 1. Implementation decision

### 1.1 Persistence technology

Recommended V1 implementation:

- Use `Microsoft.Data.Sqlite` as the SQLite ADO.NET provider.
- Use explicit SQL and small focused repositories; do not introduce Entity Framework Core or a generic ORM for this schema in V1.
- Do not add Dapper initially. Add it only if measured query-mapping repetition creates a real maintenance problem.
- Keep the AI Assistant tables and Core Domain tables in the same SQLite database file when that is how the current Core Domain is persisted. Atomic Apply requires one database transaction and one transaction-aware connection across both domains.
- If the existing Core Domain uses another database file or persistence engine, stop before implementing Atomic Apply and resolve the transaction boundary first. Do not pretend two independent SQLite files form one atomic transaction.

This is a technology recommendation, not a claim that the selected .NET provider has already passed the V1.14 test suite. Provider-specific validation is an explicit implementation gate.

### 1.2 Dependency direction

Keep the layers small:

- `WorldView.Domain`: domain concepts and deterministic rules; no SQLite dependency.
- `WorldView.Application`: use cases, orchestration and service contracts.
- `WorldView.Infrastructure.Persistence.Sqlite`: connection creation, migrations and SQL repositories.
- `WorldView.Infrastructure.Assets`: staged/content-addressed asset operations.
- `WorldView.Desktop`: Avalonia UI and user interaction.

Do not make LLM/Assistant code depend directly on repositories or SQLite connections. The Assistant proposes structured actions; application services validate and execute them.

## 2. Persistence infrastructure

Implement only these foundational pieces first:

1. `SqliteConnectionFactory`: opens the existing application database and enables `PRAGMA foreign_keys = ON` on every connection.
2. `SqliteMigrationRunner`: checks `schema_migrations`, applies the initial bootstrap once, and refuses to silently rerun it over an unknown/partially initialized database.
3. `SqliteTransactionRunner`: creates short explicit transactions and ensures all participating repositories receive the same connection and transaction.
4. `SqliteDatabaseHealthCheck`: runs `PRAGMA foreign_key_check` and `PRAGMA integrity_check` at appropriate startup/diagnostic boundaries.
5. `SqliteRepositoryBase` only if it removes meaningful repeated code. Avoid a large generic data-access framework.

The V1.14 SQL file is a one-time initial bootstrap. It is not an incremental upgrade script. Future changes must be new numbered migrations; never edit a migration already applied to a user's database.

Every database connection must enable foreign keys. Journal mode, synchronous mode, busy timeout, backup behavior and file-lock handling must be tested with the actual provider and Windows/macOS packaging before final release. Do not choose aggressive performance settings before durability tests.

## 3. Repository boundaries

Use focused repositories that map to the existing table groups:

- `TaskRepository`: Tasks, Contracts, Plan Versions, Steps, Logs, Intervention Requests, Delivery Records.
- `ToolExecutionRepository`: Tool Calls and Task Operations.
- `CandidateRepository`: Candidate Sets, Object Candidates, Candidate Versions, Candidate Edit Records.
- `ValidationRepository`: Validation Reports, Report Items, Issues.
- `SelectionRepository`: Selection Snapshots and Items.
- `ChangeSetRepository`: Change Sets, Versions and Items.
- `AuthorizationRepository`: human authorization evidence and revocation.
- `ApplicationReportRepository`: application attempts and outcomes.

Repositories perform persistence and database-level invariants. They do not decide whether a candidate semantically satisfies the user request or whether a Branch update is valid under Core Domain rules.

## 4. Transaction-aware service contracts

The following are the required transaction boundaries. A service may use multiple repositories, but all writes in one boundary must share one connection and transaction.

### 4.1 Create Task

`CreateTask(originalRequest, executionContract)`:

- Insert Task and Contract Version 1.
- Set the current contract pointer.
- Commit both or neither.
- Preserve the original user request; do not replace it with an AI paraphrase.

### 4.2 Replace Plan

`ReplacePlan(taskId, plan)`:

- Insert the new Plan Version and its Steps.
- Mark the prior active plan superseded.
- Update the Task current-plan pointer.
- Commit as one transaction.
- Do not rewrite old plan definitions or move old Steps between plan versions.

### 4.3 Create Candidate Set and first versions

`CreateCandidateSet(taskId, set, candidates)`:

- Create Candidate Set, Candidate identities and first Candidate Versions.
- Update each Candidate current-version pointer.
- Use the deferred composite FK transaction tested by V1.14.
- Candidate Object ID may remain NULL until the Core Domain reservation/binding workflow assigns it. A bound Object ID is never changed or cleared.

### 4.4 Edit Candidate

`CreateCandidateVersion(candidateId, expectedCurrentVersion, stagedAssets, edit)`:

- Stage and validate assets before opening the database write transaction.
- Verify the expected current version has not changed.
- Insert the immutable new Candidate Version and Candidate Edit Record.
- Update the Candidate current-version pointer.
- Commit the database records together.
- Revalidation is required; do not carry validation or acceptance state to the new version.

### 4.5 Finalize Validation Report

`FinalizeValidationReport(reportId, exactManifest, issues, counts, result)`:

- Create the report as Running.
- Insert the exact report-item manifest and issue rows.
- Update counts and terminal status in the same transaction.
- The database triggers require a non-empty manifest, enforce single-object report cardinality/matching, verify issue counts, and freeze finalized evidence.
- Candidate Set membership must also be checked by the Validation service because the current schema intentionally avoids another denormalized membership column.

### 4.6 Finalize Selection Snapshot

`FinalizeSelection(taskId, dispositions)`:

Before writing, verify:

- Candidate and Candidate Version belong to this Task.
- Candidate Version is the exact current/selected version intended by the decision.
- The Candidate has a valid Object ID binding/reservation.
- The selected version has an applicable completed Validation Report and no blocking Error.
- The version was delivered by this Task or is an explicitly allowed post-delivery edit.
- At most one version per Object ID is selected.

Then insert the immutable Selection Snapshot and all Selection Items in one transaction. A new user decision creates a new snapshot, never edits an earlier one.

### 4.7 Build and seal Change Set

`BuildChangeSet(selectionSnapshot, targetBranch, baseRevision, items)`:

- Resolve only exact selected Selection Items from the source snapshot.
- Create Change Set and Draft Version.
- Insert all Change Items.
- Verify candidate/object identity, operation shape, target Branch effective state and expected object-state versions.
- Canonicalize the immutable version header and ordered items; compute SHA-256 in application code.
- Store the hash and transition Draft → Sealed.
- Only then mark the Change Set Ready after all domain checks pass.
- A changed item/header/target creates a new Change Set Version and requires new validation/authorization.

The V1.14 database can prove Task, Candidate, Object and Branch authorization relationships. It cannot prove the Change Items are exactly the items selected in the referenced Selection Snapshot; the Change Set service must enforce that exact membership.

### 4.8 Authorize Change Set

`AuthorizeChangeSet(userDecision, exactVersion, hash, targetBranch)`:

- This operation is reachable only from an explicit human UI action.
- Verify the Change Set is Ready, the version is current and Sealed, the hash is recomputed and matches, and the target Branch is exact.
- Insert immutable human authorization evidence.
- The database rejects AI/system actor values, non-current/non-Ready versions, unsealed versions, duplicate active authorizations and cross-Branch authorization.
- Revocation is a one-way state change; it cannot be silently reversed.

### 4.9 Atomic Apply

`ApplyChangeSet(changeSetId, exactVersion, authorizationId)`:

Before the write transaction:

- Ensure all assets are durable and hash-verified.
- Load the immutable Change Set and validation result.

Inside one short write transaction:

1. Recheck the Change Set is Ready and the exact version is current and Sealed.
2. Recheck authorization is active, human-created, unrevoked and bound to the exact version/hash/Branch.
3. Recheck Branch Revision, expected Object State Versions, Object Identity reservations and all Core Domain preconditions.
4. Invoke transaction-aware Domain Services using the same connection and transaction.
5. Persist the resulting Branch Revision ID in `application_reports.result_branch_revision_id`.
6. Mark the Application Report Applied and Change Set Applied.
7. Commit once.

If any step fails, roll back all formal mutations and the Applied markers. Record the failed attempt in a separate short transaction. Never perform AI inference, network access, slow geometry generation or asset staging inside this write transaction.

### 4.10 Startup recovery

`RecoverPendingOperations()`:

- Inspect nonterminal Tasks, interrupted Tool Calls, incomplete Task Operations, pending Undo, and Application Reports in Prepared/Applying/Interrupted/Recovery Required states.
- Reconcile against actual persisted data and Core Domain Branch state.
- If the commit result is certain, record the correct terminal outcome.
- If uncertain, mark Recovery Required and block only the affected write scope.
- Never blindly replay an uncertain Operation or formal Apply.

## 5. Domain Services transaction integration — hard gate

Before implementing Change Set Apply, inspect the actual Core Domain persistence code and answer:

1. Which database file contains Branch/Object state?
2. Which connection/provider is used?
3. Can Domain Services accept an existing `SqliteConnection` and `SqliteTransaction` (or an equivalent transaction context)?
4. Does every formal mutation participate in that same transaction?
5. Where is the resulting Branch Revision created and how is its ID returned?
6. Can any other code path write formal map tables directly?

If Domain Services open their own connection internally, refactor the boundary before implementing Apply. Do not attempt to coordinate two connections with compensating writes and call it Atomic Apply.

## 6. Automated tests required before implementation sign-off

### 6.1 Database tests

- Apply initial bootstrap to a new database and verify migration ledger.
- Refuse to apply migration 1 twice.
- Verify all foreign keys and `integrity_check`.
- Verify all deferred pointer cycles.
- Verify cross-Task, cross-Candidate, cross-Report and cross-Branch references are rejected.
- Verify immutable evidence triggers.
- Verify Draft → Sealed and authorization rules.
- Verify only one active application attempt per Change Set version.

### 6.2 Service tests

- Task creation and plan replacement are atomic.
- Candidate version edit creates a new immutable version and invalidates previous validation/acceptance.
- Selection requires exact validated versions and correct Object IDs.
- Change Set items exactly match selected Selection Items.
- Content changes invalidate authorization.
- Revoked authorization blocks application.
- A failed Change Item causes full rollback.
- Application Report, resulting Branch Revision and Change Set Applied marker commit together.
- A simulated exception before commit leaves no partial formal changes.

### 6.3 Process-crash tests

Use process termination at each durable boundary, not only thrown exceptions:

- before Operation preparation;
- after Operation preparation but before side effect;
- after side effect but before Operation completion record;
- during Task Undo between individual operations;
- before Atomic Apply commit;
- immediately after Atomic Apply commit but before UI notification.

Verify recovery against actual persisted state and never infer commit outcome from in-memory state or log text.

## 7. Work order for Codex

1. Inspect repository layout, current Core Domain schema/migrations and existing SQLite provider. Do not edit code yet.
2. Produce a short Core Domain mapping report for Object Identity, Reservation, Branch, Branch Revision, Branch Object State and formal geometry/attribute/relationship storage.
3. Confirm the actual provider supports deferred FKs, JSON functions and the required transaction sharing; add a provider-specific test harness.
4. Integrate the V1.14 bootstrap into the existing database migration system without creating duplicate Core Domain authorities.
5. Implement connection factory, migration runner, transaction context and database health checks.
6. Implement Task/Plan/Tool/Operation repositories and services.
7. Implement Candidate/Validation repositories and services.
8. Implement Selection/Change Set/Authorization repositories and services.
9. Integrate Domain Services with the shared transaction and implement Atomic Apply only after Step 2 and Step 3 pass.
10. Implement startup recovery and process-crash tests.
11. Run the complete regression suite on Windows and macOS targets.
12. Update the implementation report with exact tested provider/runtime versions and any deviations from V1.14.

## 8. Explicit non-goals

Do not add EF Core, a generic repository framework, a generic workflow engine, DAG orchestration, event sourcing, distributed locks, message queues, multi-user authorization infrastructure or a separate recovery microservice for V1.0.

The key engineering requirement is a small number of explicit, transaction-aware services with immutable evidence and deterministic recovery.