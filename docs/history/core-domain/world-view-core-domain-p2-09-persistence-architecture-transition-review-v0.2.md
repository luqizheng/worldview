# World View — Core Domain P2-09 Persistence Architecture Transition Review

- Version: 0.2
- Date: 2026-11-21
- Status: Architecture transition review — recommendation pending runtime-boundary validation; not a frozen physical schema or implementation authorization
- Supersedes: P2-09 Persistence Architecture Proposal V0.1 as the current recommendation baseline; V0.1 remains historical
- Scope: New Core Domain persistence architecture and AI Assistant V1.0 integration, grounded in the inspected `world-view-rs` source implementation
- Deployment constraints: Single-user, local-first Windows/macOS desktop application; simple, maintainable architecture

## 1. Executive conclusion

The P2-09 V0.1 proposal recommended SQLite as the preferred direction before the actual repository implementation had been inspected. The subsequent source audit changes the framing and the recommendation status.

The current `world-view-rs` implementation is not SQLite-backed. It uses:

- YAML files as the authoritative project store;
- a directory-per-project structure;
- versioned `terrain.bin` raster assets as a documented binary exception;
- per-file temporary-write + rename for atomic replacement;
- an in-memory `wv-edit::History` Undo/Redo stack;
- a mutable in-memory `wv_core::World` aggregate.

The newer accepted Core Domain design describes a different architecture generation: Map/Branch/History, authoring Commands, State Changes, deterministic World State reconstruction, and derived Checkpoints. These structures are not represented by the inspected current `wv-core` / `wv-edit` public model.

Therefore P2-09 is not simply a database choice for adding AI tables. It is a **Core Domain persistence architecture transition** that must account for both:

1. the transition from the current YAML-first implementation to the new authoritative History/Command model; and
2. the desktop runtime boundary between the accepted C# + Avalonia direction and the existing Rust codebase.

### Current recommendation

**Keep SQLite as the preferred candidate for the new Core Domain's authoritative metadata/history store, but do not mark it selected or frozen yet.** The reason is not that the AI prototype already uses SQLite. The reason is that the new formal history model, cross-entity identity constraints, Command submission, and AI submission evidence need a reliable local transaction boundary that the current multi-file YAML save path does not provide.

Large immutable geometry/raster/payload assets and Checkpoint payloads should remain file-backed where that is simpler and appropriate. SQLite should own their references, integrity metadata and publication state.

Before choosing the SQLite provider or finalizing a shared transaction, the project must settle the C#/Rust Domain Services ownership boundary. A C# AI Assistant and a Rust Core Domain cannot simply be assumed to share one SQLite connection or transaction.

## 2. Evidence from the current implementation

Repository inspected: `luqizheng/world-view-rs`, default branch `main`, latest commit returned by the connected repository view during the audit: `268fd2a7789bcaed096f8da30b8877601fe37688` (2026-11-12).

Relevant inspected files:

- `Cargo.toml`, `AGENTS.md`
- `crates/wv-core/src/lib.rs` and `crates/wv-core/Cargo.toml`
- `crates/wv-io/src/lib.rs`, `project.rs`, `atomic.rs`, `terrain_bin.rs`, `error.rs`, `Cargo.toml`
- `crates/wv-edit/src/lib.rs`, `history.rs`, `Cargo.toml`
- `crates/wv-app/src/lib.rs`, `Cargo.toml`
- `crates/wv-ui/Cargo.toml`

The source audit is limited to the visible `main` snapshot and these files; unpushed local changes and private worktrees were not available.

### 2.1 YAML is the current source of truth

`AGENTS.md` explicitly states that YAML is authoritative and Bevy ECS is a projection. `wv-core::World` is an in-memory working aggregate; `wv-io` owns filesystem access and project load/save.

The inspected manifests do not declare a SQLite provider or SQL persistence layer for the Core Domain. The project schema marker `CURRENT_SCHEMA_VERSION = 2` is a project-file schema version, not a SQL migration ledger.

`Project::open` rejects an older project schema and documents that the v1-to-v2 time model has no automatic migration. It gives manual correction instructions instead.

### 2.2 Current persistence shape

- Project metadata: `world.yaml`.
- Map metadata and entities: directory/file-based YAML.
- Many entities: one YAML file per entity.
- Terrain raster: `maps/<map_id>/terrain.bin`, with `RasterRef` metadata.
- Terrain binary format: `WVTR`, version 2, width/height, zstd-compressed bincode payload.
- Other data: YAML remains the documented default; terrain raster is the binary exception.

This asset mechanism is useful and should be considered for reuse or compatibility, but it is not yet a general content-addressed immutable asset store or Checkpoint persistence subsystem.

### 2.3 Current atomicity boundary

`wv-io::atomic` writes a sibling temporary file and renames it over one target file. Its source comments explicitly state that it is not `fsync` and does not guarantee power-loss durability.

`Project::save_world` explicitly returns on a file IO failure without rolling back files already written. Therefore the current persistence path offers individual-file replacement safety, not project-wide multi-file atomicity.

`wv-edit::Transaction` applies commands to an in-memory `World` and compensates in memory on failure. `wv-edit::History` is an in-memory Undo/Redo stack capped at 300 transactions. Neither is a durable formal Command History transaction.

### 2.4 New Core Domain is not yet represented in the inspected source model

The newer design package defines formal Map/Branch/History, persistent authoring Commands, State Changes and reconstruction. The inspected `wv-core` public model instead exports `World`, `MapNode`, `Timeline`, `TimeSpan` and related entities. Its time model uses day-count points and half-open intervals `[from, to)`.

Do not treat current `MapNode` nesting as Branch inheritance, current `wv-edit::History` as formal History, or current `EditCommand` as the new persistent authoring Command.

## 3. Persistence requirements imposed by the new Core Domain

The new persistence design must preserve these authority rules:

1. Core Domain is the sole authority for Map, Object identity, Branch, History, formal Command, State Change, World State and Checkpoint metadata.
2. AI Assistant owns Task execution, Candidate/Validation/Selection data, immutable Command Proposal snapshots, human authorization evidence, submission attempts and Application Reports.
3. AI Assistant never writes formal Core Domain tables directly; it calls Domain Services.
4. A Command may produce zero, one or multiple State Changes; they are not one-to-one records by assumption.
5. World State is reconstructed from authoritative history and Historical Time; it is not a mutable authoritative snapshot table.
6. Checkpoints are derived, rebuildable caches and cannot become history authority.
7. Branch inheritance follows the accepted ADR rules; AI Assistant does not construct the inherited Command set itself.
8. Shared/Local ownership and propagation remain unresolved until P2-08 is formally reconciled.
9. Formal submission and the minimum AI submission evidence need a well-defined atomic/recoverable commit boundary.
10. Long-running LLM, generation, validation and asset staging work must happen outside the short final persistence transaction.

## 4. Persistence topology options

### Option A — SQLite authoritative metadata/history database + file-backed immutable assets

**Description**

- One local SQLite database is the authoritative store for Core Domain metadata/history and AI Assistant operational/evidence records.
- Core Domain and AI Assistant have separate logical table namespaces and repository ownership.
- Domain Services are the only writers of Core Domain records.
- AI repositories are the only writers of AI-owned records.
- One application-level transaction coordinator performs the final authorized submission using one transaction-aware database connection.
- Large geometry/raster/payload data and Checkpoint payloads remain immutable files where that is simpler and more efficient.

**Advantages**

- Strong local transactions for Command batches, identity reservation, proposal submission evidence and related metadata.
- Natural relational constraints for History/Branch/Command ownership and AI evidence references.
- Easier startup recovery and uncertain-submission reconciliation than coordinating multiple independently updated YAML files.
- Keeps large payloads out of long SQLite write transactions.
- Fits a single-user desktop product without introducing distributed infrastructure.

**Costs and risks**

- Requires a deliberate migration/import path from the current YAML project format.
- Requires a stable runtime/provider decision before connection and transaction ownership can be designed.
- A single database file becomes a critical project asset and needs tested backup/restore and corruption recovery.
- SQLite transactions cannot atomically commit a filesystem asset rename; assets must be staged and published before the database reference is committed, with orphan cleanup.
- If Core Domain and AI Assistant live in different runtimes, one shared transaction cannot be assumed. A transaction coordinator boundary must be designed explicitly.

**Assessment**

Preferred candidate for the new Core Domain, subject to G1 runtime-boundary validation and provider/transaction tests. Not yet selected.

### Option B — Retain YAML-first authoritative project storage

**Description**

- YAML remains the formal source of truth.
- New History, Branch and Command records are persisted as structured project files.
- AI Assistant operational/evidence records are also file-backed or stored in a separate local database, with an explicit cross-store commit protocol.
- Formal submission uses a journal/staging/manifest protocol to publish a multi-file change as one recoverable project transaction.

**Advantages**

- Preserves the current project format and human-readable file workflow.
- Reduces the need for an immediate wholesale data-store transition.
- Existing YAML and raster serialization code can be reused.

**Costs and risks**

- The current per-file atomic rename does not provide a multi-file transaction.
- A durable transaction journal, generation/manifest pointer or equivalent publication protocol would need to be designed and tested.
- Cross-reference integrity, unique identity reservation and concurrent file publication become application responsibilities.
- If AI evidence is in SQLite while formal Commands remain YAML, there is no native atomic commit across both stores.
- A correct recovery protocol may become more complex than using SQLite for authoritative metadata.

**Assessment**

Viable only if the project deliberately values YAML as the long-term authority enough to accept and validate a more elaborate multi-file commit protocol. Do not mistake the existing file-level rename helper for that protocol.

### Option C — Hybrid authority split

**Description**

Use SQLite for formal history/metadata and AI evidence, while keeping immutable large assets and Checkpoint payloads in the filesystem. YAML may remain a readable export/interchange format or a legacy import format.

**Advantages**

- Preserves human-readable export and project interchange without forcing YAML to be the transactional authority.
- Gives formal Commands and AI submission evidence one transaction boundary.
- Keeps large binary payloads out of the database.

**Risks**

- Must be explicit which representation is authoritative; SQLite and YAML cannot both be writable sources of truth.
- Requires deterministic export/import and reconciliation rules.
- Requires asset/database backup consistency.

**Assessment**

This is the preferred shape if Option A is selected: SQLite is the only writable authority for formal metadata/history; YAML is export/interchange or legacy import, not a parallel writable authority. File assets remain content-addressed and immutable.

## 5. Recommended target shape (provisional)

```text
C# / Avalonia Desktop Application
  ├── AI Assistant Task Planning & Execution Engine
  ├── Application / Commit Coordinator
  └── Domain Services API boundary
          │
          ▼
  Core Domain Persistence Boundary
    ├── SQLite: authoritative metadata/history + AI evidence
    └── Local immutable asset store
          ├── geometry / raster / payload files
          └── Checkpoint payload files
```

This diagram is a target responsibility model, not a decision that all components must be implemented in C#. The runtime boundary must be decided first. The formal application commit coordinator must be able to coordinate Core Domain and AI repositories in one real transaction; if that cannot be proven, this diagram is not approved.

### 5.1 Transaction ownership rule

One component must own the physical SQLite connection and final write transaction. It coordinates two logically separate repository operations:

- Core Domain repository: persists formal Commands, reservations and authoritative metadata through Domain Services.
- AI Assistant repository: persists submission-attempt status and returned Core Domain references.

Neither subsystem bypasses its own domain/repository rules. The coordinator does not become a second authority; it only owns the cross-boundary commit.

If Rust owns Core Domain persistence and C# owns AI Assistant persistence, the design must explicitly explain how the same connection/transaction is shared or how the commit is made recoverable. Separate commits must never be described as atomic.

### 5.2 Asset publication protocol

1. Write a new immutable asset to a temporary file in the destination filesystem.
2. Validate its format and semantic payload.
3. Compute a cryptographic content hash and verify size.
4. Flush and atomically publish the immutable file.
5. Start the short SQLite transaction.
6. Persist the Core Domain references and AI submission evidence that point to the published asset.
7. Commit.
8. If the DB transaction fails, leave an unreferenced immutable file for later safe orphan cleanup; do not delete a file unless the DB confirms no references.

Never overwrite a published immutable asset in place.

### 5.3 Checkpoint publication protocol

- Checkpoint payloads are derived files, not formal history.
- SQLite metadata records Checkpoint identity, selected History/Branch context, Historical Time, complete inherited-path context supplied by Core Domain, format/version, hash/size, publication status and integrity status.
- Write to a temporary file, validate, hash, publish atomically, then mark the Checkpoint complete in a short DB transaction after revalidating the required history context.
- Logical invalidation and physical eviction are separate operations.
- A corrupt/missing Checkpoint must cause reconstruction from authoritative history, never loss or corruption of formal history.

## 6. Runtime boundary is a prerequisite, not a provider detail

The inspected repository is Rust + Bevy/egui. The accepted desktop direction is C# + Avalonia. Before choosing Microsoft.Data.Sqlite, rusqlite or another provider, the project must decide where Core Domain and AI persistence execute.

The architecture must choose among:

1. Rust Core Domain retained behind a narrow native API; C# owns Avalonia and possibly the AI engine.
2. New Core Domain ported to C#; Rust remains only for selected geometry/rendering algorithms if justified.
3. A deliberately split model with a single formal authority and a single commit owner.

The selected shape must specify:

- which runtime owns Domain Services;
- which runtime owns the AI Assistant persistence repositories;
- where the SQLite connection and final transaction live;
- how the other runtime calls that boundary;
- how large assets cross the boundary without copying huge payloads unnecessarily;
- Windows/macOS packaging, native library loading and shutdown/recovery behavior.

Do not expose internal Rust structs directly as a long-lived public ABI. If Rust is retained, define a versioned, opaque, narrow boundary with explicit ownership and error semantics.

## 7. Migration and compatibility strategy

If Option A/C is selected:

1. Freeze the new Core Domain logical persistence model after P2-08 and runtime-boundary decisions.
2. Inventory actual YAML project layouts and all serialized entities/asset references.
3. Define a deterministic importer from current `schema_version = 2` projects into the new authoritative model.
4. Preserve original source files during import; never destructively rewrite the only copy.
5. Validate imported IDs, references, geometry, time spans and map hierarchy.
6. Define how the current day-resolution `TimePoint` / half-open `TimeSpan` data maps to the new Historical Time model. Do not silently reinterpret time precision or interval endpoints.
7. Define whether existing projects are migrated in place, copied into a new project format, or supported through a read-only compatibility layer.
8. Publish a migration report with counts, warnings and unresolved data; require user confirmation for lossy or ambiguous transformations.
9. Test backup/restore and interrupted import at every boundary.
10. Only then define incremental database migrations and release compatibility policy.

If Option B is selected, design and test the multi-file journal/manifest protocol before any AI formal apply implementation.

V1.14 remains a historical AI SQLite prototype. V1.15 remains a logical AI schema baseline. Neither is a production database migration nor proof of Core Domain transaction behavior.

## 8. Required validation tests

### Database / transaction

- Provider supports required foreign keys, deferred constraints, JSON validation where used and transaction isolation.
- Core Domain + AI repositories use the same physical connection and transaction for final formal submission.
- Rollback after failure at each statement leaves neither partial formal Commands nor a falsely successful AI report.
- Process termination before commit, after commit and before UI acknowledgement can be reconciled without blind replay.
- Unknown/partial schema initialization is rejected safely.

### Filesystem assets

- Temporary write failure, validation failure, hash mismatch, rename failure and process termination.
- Database failure after asset publication leaves only a recoverable orphan.
- Orphan cleanup never deletes referenced assets.
- Backup/restore includes database and all referenced asset payloads consistently.

### Domain integration

- Command edit/delete/insert followed by rebuild.
- Branch inherited-history changes and checkpoint invalidation.
- Shared/Local behavior only after P2-08 formal acceptance.
- Object ID reservation and permanent non-reuse.
- Historical Time precision and interval-boundary compatibility.

### Platform

- Windows and macOS file replacement/rename semantics.
- SQLite journal/synchronous settings and backup behavior.
- Native interop loading and clean shutdown if Rust and C# coexist.

## 9. Gates and status

| Gate | Requirement | Status | Blocks |
|---|---|---|---|
| G1 | C#/Rust runtime and Domain Services ownership boundary | OPEN | Provider choice and shared transaction design |
| G2 | P2-08 Shared/Local History formal reconciliation | OPEN | Physical ownership/FKs and final History schema |
| G3 | Choose SQLite authority vs YAML transaction protocol | OPEN; SQLite preferred candidate | Physical Core Domain schema |
| G4 | Command catalog, Historical Time, ordering, reconstruction and reservation contracts | OPEN | Final AI/Core Domain integration contract |
| G5 | Core Domain + AI physical schema and migrations | BLOCKED by G1–G4 | Implementation authorization |
| G6 | Provider, crash, asset and platform validation | NOT STARTED | Release readiness |

## 10. Final recommendation

- Do not treat the existing YAML persistence as if it were a SQLite database awaiting AI tables.
- Do not discard YAML compatibility without a deliberate project migration/import plan.
- Keep SQLite as the preferred candidate for the new authoritative metadata/history store because the new formal application path needs durable cross-entity transactions.
- Keep large immutable assets and Checkpoint payloads file-backed.
- Do not freeze provider, table names, FKs, migration order or transaction atomicity until the C#/Rust runtime boundary and P2-08 semantics are resolved.
- The next design deliverable should resolve G1 and compare it against Option A/C. Only after that should the project generate physical DDL.

**P2-09 status: OPEN — design transition in progress; no implementation authorization.**

## Appendix A — Direct source references

- Repository: <https://github.com/luqizheng/world-view-rs>
- Workspace manifest: <https://github.com/luqizheng/world-view-rs/blob/main/Cargo.toml>
- Architecture rules: <https://github.com/luqizheng/world-view-rs/blob/main/AGENTS.md>
- Core model: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-core/src/lib.rs>
- Project persistence: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/project.rs>
- Atomic file writes: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/atomic.rs>
- Raster format: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/terrain_bin.rs>
- In-memory edit history: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-edit/src/history.rs>
- Application/MCP boundary: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-app/src/lib.rs>

Related design records:
- P2-08 Shared / Local History Reconciliation Review V0.1
- Core Domain Implementation Reality Audit V0.1
- Core Domain Integration Contract V0.2
- AI Assistant Logical Database Schema V1.15
- AI Assistant SQLite Prototype V1.14 (historical only)