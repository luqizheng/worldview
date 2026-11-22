# World View — Core Domain Implementation Reality Audit

- Version: 0.1
- Date: 2026-11-21
- Status: Source-code audit — evidence-based working baseline; not an implementation authorization
- Scope: Existing `world-view-rs` implementation versus the accepted Core Domain direction and AI Assistant Task Planning & Execution Engine V1.0 integration requirements
- Repository inspected: `luqizheng/world-view-rs`, default branch `main`
- Repository snapshot: latest commit returned by the connected GitHub repository view during this audit: `268fd2a7789bcaed096f8da30b8877601fe37688` (2026-11-12)

## 1. Executive conclusion

The inspected `world-view-rs` source is a functioning Rust workspace with a Bevy/egui application, a YAML-first project persistence model, an in-memory editing/Undo stack, and an optional HTTP/MCP integration path. It is **not yet an implementation of the newer frozen Core Domain history architecture described by ADR-001/002/003/005 and the P2-01/D01 design package**.

The most important finding is therefore broader than “no Core Domain SQL migrations were found”:

> The current source implementation and the newer Core Domain design baseline are different architectural generations. The new Map/Branch/History/Command/State Change reconstruction model is not represented by the inspected `wv-core` and `wv-edit` implementation structures.

The current repository does not use SQLite as its authoritative project store. `AGENTS.md` and `wv-core` explicitly identify YAML on disk as source of truth. `wv-io` writes entity YAML files and a versioned binary raster payload. Its file-level atomic rename does not make a multi-file project save atomic. `wv-edit::Transaction` provides in-memory command compensation, not a durable Core Domain history transaction.

Consequently:

1. The V1.15 AI Assistant logical schema remains a valid AI-owned logical design candidate, but its physical integration cannot be finalized against the current source implementation as if Core Domain SQLite tables already existed.
2. P2-09 must be treated as a **Core Domain persistence architecture transition**, not merely as “add AI tables to the existing database”.
3. Before implementation, the project must establish how the accepted C# + Avalonia desktop direction relates to the existing Rust + Bevy/egui application and Rust domain crates.
4. P2-08 remains a separate domain-semantic blocker: the frozen ADR-003 Shared/Local History behavior must be reconciled formally with later D01-70/76 discussion before the persistence ownership model is frozen.

This audit is based on the repository's visible `main` branch snapshot and the specific files listed below. It does not inspect unpushed local changes, private branches, or unlisted local worktrees.

## 2. Evidence scope

### 2.1 Repository and workspace

The connected repository is `luqizheng/world-view-rs`, default branch `main`. Its root `Cargo.toml` declares a Rust workspace with ten crates, including:

- `wv-core`
- `wv-edit`
- `wv-io`
- `wv-app`
- `wv-render`
- `wv-ui`
- `wv-terrain`
- `wv-world-cell-map`
- `wv-overture`
- `wv-default-theme`

The workspace is Rust 2021 and uses Bevy 0.19 / bevy_egui 0.42 in the application/rendering stack.

### 2.2 Files fetched from the repository

The following source/configuration files were directly inspected:

- `Cargo.toml`
- `AGENTS.md`
- `crates/wv-core/Cargo.toml`
- `crates/wv-core/src/lib.rs`
- `crates/wv-io/Cargo.toml`
- `crates/wv-io/src/lib.rs`
- `crates/wv-io/src/project.rs`
- `crates/wv-io/src/atomic.rs`
- `crates/wv-io/src/terrain_bin.rs`
- `crates/wv-io/src/error.rs`
- `crates/wv-edit/Cargo.toml`
- `crates/wv-edit/src/lib.rs`
- `crates/wv-edit/src/history.rs`
- `crates/wv-app/Cargo.toml`
- `crates/wv-app/src/lib.rs`
- `crates/wv-ui/Cargo.toml`

The GitHub code-search index reported unavailable for this repository. The audit therefore used the workspace manifest, directly fetched known source paths and the Library design package; it does not claim an exhaustive indexed search of every source file.

## 3. Confirmed implementation facts

### 3.1 Current persistence is YAML-first, not SQLite

`AGENTS.md` explicitly states:

- YAML is the source of truth.
- Bevy ECS is a projection, not the authoritative store.
- `wv-app` may hold a mutable working copy but is not the authoritative persistence layer.
- `wv-io` owns filesystem access and project load/save.

`wv-core/src/lib.rs` describes `World` as an in-memory aggregate. `wv-io/src/project.rs` persists project metadata and entities into a directory structure, generally as YAML files. Large terrain raster data is the documented binary exception.

The inspected root workspace manifest and the relevant `wv-core`, `wv-io`, `wv-edit`, and `wv-app` manifests do not declare a SQLite provider or SQL persistence layer. No Core Domain SQL migration set was identified in the inspected Library inventory either.

### 3.2 Existing project schema and migration behavior

`wv-io/src/lib.rs` declares `CURRENT_SCHEMA_VERSION = 2`.

`Project::open` in `wv-io/src/project.rs` reads `world.yaml` and rejects a project whose schema version is older or newer than the current version. The v1-to-v2 time-model change is explicitly documented as having no automatic migration; users receive manual correction guidance.

This `schema_version` is a project-file format version. It is not a relational database migration ledger and does not provide incremental SQL migration infrastructure.

### 3.3 Existing asset persistence

`wv-io/src/terrain_bin.rs` defines the versioned `WVTR` raster format:

- magic header `WVTR`;
- format version 2;
- width and height;
- zstd-compressed bincode payload.

The module documentation states that terrain raster is the sole binary-data exception and that other data should remain YAML. `RasterRef` carries the file reference and metadata.

This is useful existing asset-persistence infrastructure, but it is not a general immutable content-addressed asset store or a Checkpoint store.

### 3.4 Existing file writes are atomic per file, not atomic across a project save

`wv-io/src/atomic.rs` writes a sibling temporary file and renames it over the target. The source comments explicitly state:

- this protects against a partially written individual file;
- it is not `fsync` and does not guarantee power-loss durability;
- it does not provide a multi-file transaction.

`Project::save_world` in `wv-io/src/project.rs` explicitly documents that if an individual file write fails, it returns immediately and does not roll back files already written. Previously written files remain.

Therefore, the existing persistence path cannot currently guarantee an atomic multi-entity formal application spanning multiple YAML files.

### 3.5 Existing Edit Transaction / History is not the new formal historical Command model

`wv-edit/src/history.rs` implements:

- an in-memory `Transaction` containing `EditCommand` objects;
- sequential command application to a mutable `World`;
- reverse-order compensation if an in-memory command fails;
- an in-memory Undo/Redo stack;
- a maximum of 300 retained Undo transactions.

This `wv-edit::History` is an editing-session Undo/Redo stack. It is not a durable Core Domain `History` entity, does not persist formal historical Commands, and is not the Map/Branch history authority defined by ADR-001/002/003/005.

The distinction is essential: identical names do not imply identical domain semantics.

### 3.6 The inspected `wv-core` model is not yet the newer Map/Branch/History model

`wv-core/src/lib.rs` exports `World`, `MapNode`, `Timeline`, `TimeSpan`, event/person/faction/geometry models and related data. The inspected public module structure does not expose the newer formal `History`, `Branch`, persistent authoring `Command`, or `World State Reconstruction` domain services described in the newer Core Domain design package.

The current time implementation in `wv-core/src/time/mod.rs` uses a day-count `TimePoint` and half-open `TimeSpan` intervals `[from, to)`. `TimelineQuery::at` projects active spans at a day value. This is not, by itself, the later Command-driven historical reconstruction contract.

The existing map hierarchy (`MapNode`) must not be treated as equivalent to the newer Branch inheritance model. Map nesting and Branch history inheritance are different concepts.

### 3.7 Existing MCP support is only a partial foundation for the new AI Assistant

`wv-app` has an optional `http` feature built around Axum/Tokio and documents an MCP endpoint for external LLM clients. The application module describes this as LLM-controlled editing through the current GUI-held `WorldProjection` and `wv-edit::History` handles.

This is useful prior work for tool transport and command dispatch, but it is not the new embedded AI Assistant Task Planning & Execution Engine. The inspected source does not establish the new persistent Task/Plan/Step/Operation lifecycle, Candidate/Validation/Selection evidence model, typed Command Proposal authorization, or formal Application Recovery protocol as implemented capabilities.

## 4. Architecture-generation gap

The newer Core Domain design package defines a different authority model:

- Map and Branch use a unified formal History entity.
- Branch inherits parent history through `parent` + `parent_at`, without copying ancestor Commands.
- Commands are authoritative authoring history and may be edited, deleted or inserted.
- State Changes are distinct from Commands and may be zero, one or many per Command.
- World State is reconstructed from authoritative history and a selected Historical Time.
- Checkpoints are derived caches, not history authority.

The inspected current code instead centers on a mutable `World` aggregate, YAML files as source of truth, time-span projections and a transient in-memory Undo stack.

This does not mean the current code is defective relative to the earlier architecture it implements. It means the newer Core Domain design has not yet been mapped to, or implemented by, this codebase. The development plan must explicitly account for that transition rather than assuming AI Assistant can simply attach to existing `wv-core` types.

## 5. Platform and runtime boundary

The inspected `main` branch is a Rust + Bevy/egui application. The accepted product direction recorded in the project decision context is C# + Avalonia for the Windows/macOS desktop application, while the map rendering engine, geometry engine and geospatial data technology remain separate evaluations.

The repository snapshot does not demonstrate that this C# + Avalonia target has already been implemented or that a stable cross-language boundary exists.

Before implementation, the architecture must select and document one of these integration shapes:

1. **Rust Core retained behind a stable native boundary** — C# / Avalonia owns the desktop shell and calls Rust Domain Services through a deliberately designed interop boundary.
2. **Core Domain ported to C#** — the new C# application owns the formal domain model and persistence; Rust code is retained only where justified for rendering/geometry or is retired incrementally.
3. **A deliberately split domain/engine boundary** — only if the split has clear ownership and does not create two authorities for Map, Branch, History, Commands or World State.

This audit does not choose among them. A split that duplicates domain authority is unacceptable. The choice should be made before the AI Assistant implementation binds to a language-specific service or storage API.

## 6. Impact on AI Assistant development readiness

| Area | Evidence-based status | Consequence |
|---|---|---|
| Task Planner / Coordinator / Executor architecture | Designed in AI Assistant documents | Can proceed to contract refinement; not yet evidenced as implemented |
| Candidate / Validation / Selection logical model | V1.15 logical baseline exists | Keep logical baseline; physical FK mapping is not available |
| Formal Map/Branch/History authority | New design documented, not represented by inspected current code | Requires Core Domain transition design and service contract |
| Shared / Local History semantics | Frozen ADR-003 conflicts with later D01-70/76 discussion | P2-08 remains a domain-semantic blocker |
| Current Core Domain persistence | YAML directory + binary raster files | SQLite proposal is a migration/transition, not an additive integration |
| Atomic formal apply | Current multi-file save is not atomic | New formal application boundary cannot rely on existing file save behavior |
| Durable formal Command history | Not evidenced in inspected source | Must be designed/implemented by Core Domain, not AI Assistant |
| C# / Avalonia integration | Product direction accepted; no matching implementation found in inspected `main` snapshot | Runtime/interop boundary must be specified |
| MCP/tool transport | Optional Rust Axum HTTP/MCP path exists | Reusable conceptually, but not equivalent to the new embedded engine |

## 7. Required work before implementation

### Gate G0 — Confirm the implementation baseline

- Confirm whether the GitHub `main` snapshot is the intended source baseline or whether newer local/unpushed code exists.
- Identify the target branch/commit and the authoritative working tree for the C# + Avalonia direction.
- Inventory all relevant source crates and tests after the target baseline is confirmed.

### Gate G1 — Decide the application/Core Domain runtime boundary

- Define whether the new Core Domain is Rust-retained, ported to C#, or deliberately split.
- Define the sole owner of formal Map/Object/Branch/History/Command/State Change/World State data.
- Define the Domain Services call boundary used by the AI Assistant.
- Ensure the renderer/UI never becomes a second domain authority.

### Gate G2 — Complete P2-08 Shared / Local History reconciliation

- Formally reconcile ADR-003 with D01-70/76.
- Decide whether true global Shared Object mutation remains a product capability and how it is represented in one selected Branch reconstruction path.
- Define ownership, propagation, historical edits, deletion and multi-Map operation semantics.
- Publish a new/revised accepted ADR before freezing physical ownership/FK relationships.

### Gate G3 — Reframe and complete P2-09 persistence architecture

Evaluate two realistic paths against the new Domain Model and the current file-based implementation:

**Option A — SQLite becomes the authoritative metadata/history store**
- Define how current YAML projects are imported/migrated and whether YAML remains an export/interchange format.
- Keep large immutable geometry/raster/payload assets file-backed where appropriate.
- Prove the selected .NET or Rust SQLite provider, connection/transaction ownership, crash behavior and Windows/macOS packaging.
- Define a single transaction boundary for formal Command submission plus the minimum AI submission evidence.

**Option B — retain file-based YAML as the authoritative store**
- Define durable Command/History persistence in the file model.
- Design a crash-safe multi-file commit protocol or a single authoritative package/journal format.
- Prove atomic publication, recovery, rollback, concurrency assumptions and reconciliation with AI submission evidence.
- Avoid assuming per-file rename provides project-wide atomicity.

A hybrid is acceptable only if each data class has one unambiguous source of truth and the formal application boundary is recoverable. Do not decide based solely on the fact that the AI prototype already has SQLite DDL.

### Gate G4 — Freeze Core Domain integration contracts

- Command catalog and payload schemas.
- Target History resolution and opaque context/precondition references.
- Historical Time and ordering semantics compatible with the chosen runtime/domain model.
- Reconstruction/playback request and result contracts.
- Object identity reservation contract.
- Asset publication and Checkpoint lifecycle contracts.
- Submission idempotency and uncertain-result reconciliation.

### Gate G5 — Produce the integrated physical persistence design

Only after G1–G4:
- Produce Core Domain physical schema/migrations.
- Map AI-owned logical entities to actual Core Domain IDs and FKs where valid.
- Define migration ownership/versioning.
- Define the exact formal application transaction.
- Define backup/restore and data import compatibility.

### Gate G6 — Validate with focused prototypes and tests

- Empty database/project bootstrap and upgrade/import tests.
- Formal Command batch submission with human authorization.
- Crash at every persistence/publication boundary.
- Command edit/delete/insert and history rebuild.
- Branch inherited-history invalidation and Checkpoint recovery.
- Shared/Local behavior after P2-08 acceptance.
- Asset staging, orphan cleanup and backup/restore.
- Windows/macOS filesystem and SQLite behavior where applicable.

## 8. Recommended sequencing and current recommendation

1. Treat the current Rust code as the existing product implementation baseline, not as proof that the newer Core Domain history model is already implemented.
2. Resolve G1 (runtime/domain ownership) and G2 (P2-08) before final persistence table ownership and FKs.
3. Re-evaluate P2-09 against the actual YAML-first persistence behavior. SQLite remains a strong candidate for durable history and atomic formal application, but adopting it means an explicit Core Domain persistence transition and project migration strategy.
4. Keep the AI Assistant V1.15 logical schema as the AI-owned design baseline while keeping all Core Domain references opaque.
5. Do not issue implementation authorization for formal Command application or final database migrations until G1–G5 are closed.

## 9. Final assessment

**Development readiness: not ready for full implementation.**

The AI Assistant's internal planning/execution design is sufficiently mature to continue interface and contract work. However, the inspected source reveals a larger Core Domain implementation transition than previously visible from the Library documents alone.

The two known blockers remain:

- P2-08: Shared / Local History formal semantics.
- P2-09: Persistence architecture and transaction boundary.

Two additional integration prerequisites must be handled explicitly:

- the gap between the existing Rust/YAML/Bevy implementation and the newer Map/Branch/History Core Domain design;
- the accepted C# + Avalonia desktop target and its relationship to the existing Rust code.

The next design deliverable should be a **Core Domain Transition Architecture** that connects the current code reality, the accepted target domain model, the desktop runtime boundary and the persistence strategy. This is more valuable than generating final SQL DDL prematurely.

---

## Appendix A — Direct source references

Repository: <https://github.com/luqizheng/world-view-rs>

- Workspace manifest: <https://github.com/luqizheng/world-view-rs/blob/main/Cargo.toml>
- Repository architecture rules: <https://github.com/luqizheng/world-view-rs/blob/main/AGENTS.md>
- `wv-core` domain aggregate: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-core/src/lib.rs>
- `wv-core` manifest: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-core/Cargo.toml>
- `wv-io` manifest: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/Cargo.toml>
- `wv-io` project persistence: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/project.rs>
- `wv-io` atomic file write: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/atomic.rs>
- `wv-io` raster format: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-io/src/terrain_bin.rs>
- `wv-edit` in-memory Undo/Redo: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-edit/src/history.rs>
- `wv-app` application/MCP boundary: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-app/src/lib.rs>
- `wv-app` manifest: <https://github.com/luqizheng/world-view-rs/blob/main/crates/wv-app/Cargo.toml>

Related design records in the project Library:
- ADR-001 — Map History and Branch Model (Accepted / Frozen)
- ADR-002 — Unified History Entity (Accepted / Frozen)
- ADR-003 — Shared / Local History Command Playback (Accepted / Frozen)
- ADR-005 — Branch Fork Rules (Accepted / Frozen)
- P2-01 Domain Model Proposal V0.1
- D01-70 through D01-76 and subsequent discussion records
- AI Assistant Core Domain Integration Contract V0.2
- P2-08 Shared / Local History Reconciliation Review V0.1
- P2-09 Persistence Architecture Proposal V0.1