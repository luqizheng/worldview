# World View P2-09-01 — Persistence Ownership Model

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — recommended, not yet frozen as physical schema or implementation authorization
- Scope: Ownership boundaries for the new C# + Avalonia, single-user, local-first World View application

## 1. Purpose

This document defines who owns persisted information, who may write it, and how the Core Domain and AI Assistant cooperate when a formal map change is submitted.

It is a logical ownership contract. It does not define physical table names, SQL columns, foreign keys, migration scripts, or a provider-specific implementation.

## 2. Architecture assumptions

1. The target application runtime is C# + Avalonia for Windows and macOS.
2. Rust + Bevy is discontinued because of build-time cost. Existing Rust code is historical reference material only; it is not the runtime or persistence foundation for the new Core Domain.
3. World View is a single-user, local-first desktop application. Multi-user concurrency, distributed execution, and multi-instance coordination are out of scope for V1.
4. Core Domain is the sole authority for formal map data and historical semantics.
5. AI Assistant plans and executes work in its own staging space. It cannot directly mutate formal map data.
6. SQLite is the recommended authoritative local relational database. Large immutable payloads and Checkpoint files may live in a project-local asset store.
7. The database and asset store together form one Project persistence/backup unit, although filesystem publication and SQLite commits are not one native atomic transaction.

## 3. Ownership principles

- **One fact, one authority.** The same formal fact must not be independently writable in Core Domain and AI Assistant stores.
- **Shared database does not mean shared ownership.** A common SQLite file is a deployment and transaction choice, not permission for modules to write each other's records.
- **Domain Services own formal writes.** Core Domain persistence is written only through Core Domain Domain Services/repositories.
- **AI Assistant owns its execution evidence.** AI Assistant may persist Tasks, plans, tool operations, candidates, validations, selections, proposals, authorization evidence, submission attempts, and reports, but not formal map history or state.
- **Cross-boundary coordination is not a third domain authority.** The application Commit Coordinator coordinates a transaction and calls the two owners; it does not define map semantics or write either module's tables directly.
- **Derived data remains derived.** World State and Checkpoints do not replace authoritative history. Indexes, previews, caches, and generated summaries are rebuildable.
- **No speculative physical coupling.** Until Core Domain keys and schema are designed, AI records retain opaque Core Domain references. Physical foreign keys may be added only after stable key contracts are known and justified.

## 4. Logical ownership matrix

| Data category | Authority / owner | Permitted writer | Notes |
|---|---|---|---|
| Project identity and project-level Core Domain metadata | Core Domain | Core Domain Domain Services | One Project database is the persistence boundary. |
| Map and Branch identity/metadata | Core Domain | Core Domain Domain Services | Includes parent History and fork-boundary semantics. |
| Object identity and Object ID reservation lifecycle | Core Domain | Core Domain Domain Services | Reserved IDs remain consumed according to the accepted reservation rules; AI cannot allocate or recycle IDs independently. |
| Shared Object definition/identity and shared base data | Core Domain | Core Domain Domain Services | A shared definition/base-data edit is shared by all Maps that reference that Shared Object. |
| Time-zero initial map state | Core Domain | Core Domain Domain Services | Time zero means the map's initial point, not a later timeline Event. Shared-object initial/base state is common to referencing Maps. |
| Events and post-zero Event-driven changes | Core Domain | Core Domain Domain Services | An Event after time zero belongs to the currently active Map/Branch history. Other Maps are silent/inactive and do not execute that Event. Their reconstructed runtime states therefore do not change merely because another Map executed an Event. |
| Formal Commands and ordering constraints | Core Domain | Core Domain Domain Services | Commands remain editable/deletable/insertable under Core Domain rules; no Command Revision Chain is introduced. |
| State Changes and World State reconstruction | Core Domain | Core Domain reconstruction services | State Changes may be zero, one, or many per Command; World State is reconstructed, not independently authored by AI. |
| Checkpoint metadata and validity | Core Domain | Core Domain Checkpoint services | Checkpoint payload may be file-backed; it remains a derived cache. |
| AI Task, Task Plan, Task Step, Tool Call, Operation, Task Log, intervention state | AI Assistant | AI Assistant services | These describe AI execution, not formal map history. |
| Candidate Sets, Object Candidates, candidate versions, staging geometry and delivery manifests | AI Assistant | AI Assistant services | Candidate data remains staging and cannot become formal World State by direct persistence. |
| Validation reports and selection snapshots | AI Assistant | AI Assistant services | Bound to exact candidate versions and validator/version evidence. |
| Change Sets, typed Command Proposals and proposal versions | AI Assistant | AI Assistant services | Proposals are not Core Domain Commands. |
| Human authorization evidence | AI Assistant | AI Assistant authorization service | Immutable evidence bound to exact proposal manifest/hash and target context. |
| Submission attempts, idempotency identities and reconciliation evidence | AI Assistant | AI Assistant submission service | AI owns attempt evidence; Core Domain remains authority for whether Commands were committed. |
| Application Reports | AI Assistant | AI Assistant reporting service | Must distinguish Command persistence, reconstruction, and playback outcomes. |
| Accepted Core Domain Command references | AI Assistant stores reference; Core Domain owns target | AI Assistant records reference returned by Core Domain | AI must not invent or treat the reference as a formal Command record. |
| Core Domain authoritative asset metadata/references | Core Domain | Core Domain asset services | Includes references required to reconstruct formal map content. |
| AI staging assets and temporary generated payloads | AI Assistant | AI Assistant asset/staging services | Lifecycle is tied to Candidate/Task; only promoted/published assets become Core Domain-referenced assets. |
| Immutable asset file bytes | Asset Store infrastructure; semantic ownership follows referencing domain | Asset Store service under the owning domain's request | Content-addressed and immutable. The file store is not a second semantic authority. |
| Application preferences and window/UI state | Application shell | Application settings service | Kept outside formal map history and AI execution evidence. |

## 5. Shared Object and Event ownership clarification

The current user-confirmed business explanation distinguishes two different kinds of change:

1. **Shared Object definition/base-data change:** the Shared Object is genuinely shared. A change to the shared object itself is common to every Map that references it.
2. **Post-zero Event-driven runtime change:** once a Map's timeline has started, a later Event changes the object as part of the currently active Map's history. Only one Map is active at a time. Other Maps remain silent and do not execute that Event; their own reconstructed histories are unaffected by that Event.

Time zero is the initial point of the Map, not an arbitrary later timestamp. The persistence model must not collapse shared definition/base data and post-zero Event effects into one undifferentiated mutable row or one globally propagated Event stream.

The exact Core Domain representation and reconstruction projection for this distinction must be reflected in the formal P2-08/ADR resolution. AI Assistant consumes that Core Domain contract and must not implement its own Shared/Local history composition.

## 6. Persistence topology

Recommended V1 topology:

```text
C# + Avalonia application
  ├── Core Domain module
  │     ├── Domain Services / repositories
  │     └── Core Domain-owned records
  ├── AI Assistant module
  │     ├── Task Planning & Execution Engine
  │     ├── AI-owned repositories
  │     └── Candidate / proposal / authorization evidence
  ├── Application Commit Coordinator
  │     └── coordinates the final authorized transaction only
  └── Project Persistence Infrastructure
        ├── one project-local SQLite database
        └── project-local immutable asset store
              ├── formal Core Domain assets
              ├── AI staging assets
              └── file-backed Checkpoint payloads
```

The single SQLite database is recommended because the final authorized submission needs a simple local transaction boundary. It does not erase logical module ownership.

## 7. Write and transaction boundaries

### 7.1 Normal AI work

- AI Task execution, planning, tool calls, candidate generation, validation and selection write only AI-owned records and staging assets.
- These operations do not hold a Core Domain write transaction while LLM inference, generation, long validation, or filesystem staging is running.
- Candidate creation, acceptance, or selection does not itself modify formal Map/Branch history.

### 7.2 Formal application

The application Commit Coordinator may coordinate a short transaction only after explicit human authorization has been verified against the exact sealed proposal manifest, hash and target context.

Within that transaction:

1. AI submission service verifies and records the attempt identity/evidence through its repository.
2. Core Domain Domain Services validate and persist the complete typed Command batch through Core Domain repositories.
3. AI submission service records the accepted Core Domain references and submission outcome.
4. The transaction commits once.

The coordinator does not write Core Domain or AI tables directly. If any required write fails before commit, the transaction rolls back as a whole.

The actual ability to share one SQLite connection/transaction across these services must be proven with the selected .NET provider. If that cannot be achieved, do not claim atomicity; stop and design an explicit recoverable protocol before implementation.

Command persistence, World State reconstruction, and playback remain distinct phases and must have distinct reported outcomes. Reconstruction failure does not erase already committed formal Commands.

### 7.3 Uncertain commit outcome

If the process terminates or the response is lost around commit, AI Assistant must reconcile using its stable submission/idempotency identity against Core Domain authoritative history before retrying. It must never blindly replay a potentially committed batch.

## 8. Asset ownership and publication

- Asset files are immutable after publication and addressed by a cryptographic content hash (recommended SHA-256).
- The owning domain controls semantic metadata and references; a shared Asset Store service handles byte storage, validation, publication and safe retrieval.
- AI-generated assets remain in AI staging ownership until the formal application process promotes/references them through Core Domain services.
- File publication and SQLite commit are not one atomic transaction. Publish and validate immutable bytes first, then commit references in SQLite. A failed DB transaction may leave an orphan file; safe cleanup may remove it only after confirming that no committed record references it.
- Checkpoint files are derived artifacts. Missing or corrupt files cause reconstruction from authoritative history, not loss of history.

## 9. Migration ownership

- Core Domain owns the schema definitions and migrations for Core Domain tables.
- AI Assistant owns the schema definitions and migrations for AI Assistant tables.
- The application bootstrap coordinates module migrations in a deterministic order and rejects unknown, partial, or incompatible schema states.
- A single migration ledger may record `(module_id, migration_version, checksum, applied_at)` with a uniqueness rule on `(module_id, migration_version)`. This avoids version collisions while preserving module ownership. The exact physical form is deferred to P2-09-06.
- Neither module may silently modify the other's tables through its migration scripts.
- Backup/restore treats the SQLite database and all referenced asset files as one Project consistency unit.

## 10. Cross-module references

Until Core Domain key types and physical schema are finalized:

- AI-owned records store opaque Core Domain identifiers and context tokens supplied by Domain Services.
- AI Assistant validates targets and preconditions by calling Core Domain services, not by interpreting Core Domain tables directly.
- Do not create speculative foreign keys to guessed Core Domain tables.
- Once stable keys and lifecycle semantics are frozen, add database-level foreign keys only where they reinforce a true ownership relationship and do not undermine module migration independence.

## 11. Explicitly rejected designs

- AI Assistant directly inserting/updating/deleting formal Core Domain records.
- Two independently writable copies of formal map history or World State.
- A generic shared repository that allows either module to bypass its Domain Services.
- Treating a shared SQLite file as permission for unrestricted cross-module SQL.
- A second database for AI operational state without a demonstrated need, because it complicates final submission consistency.
- A separate writable YAML authority alongside SQLite.
- A generic event bus, outbox, message broker, distributed transaction, or microservice layer for this single-user V1.
- AI-owned Shared Object history, Branch history, formal Command, World State, or Checkpoint authority tables.

## 12. Decisions and remaining evidence

### Recommended working decisions

1. One project-local SQLite database is the target persistence container.
2. Core Domain and AI Assistant retain strict logical data ownership and separate repository/service boundaries.
3. The application Commit Coordinator owns only cross-module transaction coordination, not domain records.
4. Core Domain owns formal map history, object identity, reservations, reconstruction and Checkpoint validity.
5. AI Assistant owns task execution and immutable proposal/authorization/submission evidence.
6. Immutable files are stored in a project-local Asset Store; semantic references are owned by the relevant domain.
7. Module migrations remain separately owned and are coordinated by application bootstrap.
8. Cross-module references remain opaque until stable Core Domain keys are defined.

### Evidence still required before physical schema freeze

- Select and validate the .NET SQLite provider/version for Windows and macOS.
- Demonstrate one physical connection and one transaction can be passed through Core Domain Domain Services and AI submission repositories.
- Define stable Core Domain primary-key types and reference lifecycle.
- Complete formal P2-08 ADR reconciliation for Shared Object definition/base data versus post-zero Event-driven changes.
- Define Core Domain Command/Event/State Change and Historical Time physical representation.
- Define Object ID reservation persistence and transaction semantics.
- Define Asset Store and Checkpoint metadata/payload contracts.
- Define module migration bootstrap, backup and crash-recovery tests.

## 13. Status

P2-09-01 is a recommended logical ownership baseline. It is suitable for review and for guiding P2-09-02, but it is not an executable schema, a frozen provider decision, or implementation authorization.

The next design item is **P2-09-02 — Core Domain Logical Persistence Model**.