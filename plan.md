# World View — Product Development Roadmap

- Version: 1.0
- Date: 2026-11-26
- Status: Project-level execution baseline; phase details are refined through versioned design contracts before implementation
- Product: World View — local-first desktop software for creating, editing, reconstructing and presenting fictional or historical worlds and maps
- Target platforms: Windows and macOS
- Application direction: C# / Avalonia desktop application; local project data
- Planning authority: This file is the top-level development sequence. Domain-specific contracts remain authoritative for their own semantics.
- Scope: Product design, architecture, implementation, verification, integration and release preparation

---

## 1. Purpose

The repository has accumulated substantial domain, application, map-editor, interaction, terrain, map-generation and rendering design documents. Those documents describe important parts of the product, but they do not by themselves provide one end-to-end development sequence.

This roadmap connects them into a product delivery plan. It defines:
- what must be designed before code is written;
- the order in which software capabilities are implemented;
- the sequential order in which work packages are designed, implemented, reviewed and integrated;
- the dependencies and integration gates between work packages;
- what evidence is required before a phase is considered complete.

This is a living execution plan. A phase is not complete merely because its design document or developer prompt exists.

## 2. Product and engineering principles

### 2.1 Product shape

World View is a local-first desktop application for a single primary user. It is not a SaaS platform and does not require a server-side multi-tenant architecture or real-time multi-user collaboration.

The product should allow a creator to:
1. create and manage a local world/map project;
2. create, edit, organize and inspect map content;
3. work with geography, terrain, natural features, settlements, buildings and interiors at appropriate scopes;
4. preserve formal data, history and project files reliably;
5. view maps at multiple scales with useful visual styles;
6. generate or transform content with optional AI assistance;
7. export useful map and layout outputs.

Manual/core map creation must remain usable without an LLM, external Agent, MCP, Codex or Claude Code.

### 2.2 Authority and architecture

- Core Domain owns formal map identity, geometry, relationships, commands, history and authoritative state.
- Application services coordinate use cases and persistence; UI code must not write database records directly.
- Map Engine owns spatial calculations, coordinate transforms, geometry operations and derived spatial indexes.
- Presentation/Renderer consumes immutable, versioned presentation snapshots; it never owns formal map truth.
- Field Packages and other assets have explicit immutable/versioned ownership and integrity rules.
- AI proposes or requests controlled operations through application/domain interfaces. It does not directly mutate authoritative storage.
- Avalonia is the desktop shell/viewport host. Skia is the leading V1 2D rendering candidate behind a replaceable boundary, subject to platform proof.

### 2.3 Project stage constraints

This project is still under development and has not been formally released. There are no production customers, formal users or historical business datasets that require compatibility guarantees.

Therefore:
- prefer the best coherent architecture over preserving unused interim implementations;
- remove or reshape unused tables, APIs and migrations when a better design is justified;
- do not introduce dual-write, dual-schema, complex backfills or compatibility layers without a real need;
- still protect active developer/test assets, build infrastructure and external dependencies;
- destructive changes require dependency checks and tests, not assumptions.

### 2.4 Single-machine execution and runtime asynchrony

Development and integration work is executed sequentially. Do not plan concurrent multi-agent work, simultaneous implementation branches or parallel work-package lanes as a project requirement. Complete, review and integrate one work package before starting the next dependent package.

The application is single-user/single-machine. Runtime asynchronous operations still interleave: UI events, background preparation, cancellation, file operations and window lifecycle can race. Handle those lifecycle and thread-safety cases correctly, but do not introduce distributed, multi-user or generalized concurrency infrastructure.

## 3. Delivery strategy

Use a design-first, vertical-slice delivery strategy:

1. Establish product behavior and acceptance scenarios.
2. Confirm the owning architecture and contracts for each capability.
3. Implement the smallest end-to-end slice through UI → Application → Domain → persistence/read model → presentation where applicable.
4. Add automated tests and failure-path evidence with the implementation, not afterward.
5. Integrate and verify on Windows and macOS.
6. Expand capability only after the vertical slice is stable.

Avoid two extremes:
- do not attempt to implement every domain subsystem before a usable application exists;
- do not build UI prototypes that bypass domain authority and later require a rewrite.

Each work package must contain both a design output and an implementation/verification output, unless it is explicitly an architecture-only or research spike.

## 4. Current baseline and in-flight work

The following is the planning baseline inferred from the repository documentation and known current work. It is not a substitute for reviewing current source code and PRs before starting each task.

| Area | Existing baseline | Roadmap treatment |
|---|---|---|
| Core Domain CD-01/CD-02 | Persistence, object/type authority, geometry/asset foundation and Building/Cave descriptor adoption are documented as implemented; CD-02 final report records 381/381 tests with conditions | Do not restart. Audit remaining gaps and close only the dependencies needed by the next vertical slice |
| Application Foundation AF-01/AF-02 | Project package, application/session lifecycle and Asset Store foundation are documented as implemented, with platform and integration conditions | Reuse; verify current code and close real gaps |
| ME-01 through ME-06 | Broad map-editor, terrain, geometry, interaction, Building/Cave and acceptance contracts exist | Reconcile into a product capability sequence; do not blindly re-author |
| ME-07 / PRA | Rendering architecture and implementation sequencing exist; PRA-01 is the snapshot foundation; PRA-02 onward require evidence and dependency gates | Execute in the order defined below |
| Spatial Chunk & LOD | Contract V0.1 merged by PR #41 at `73da06f0ef143c9a2df9a690aaed0acf1268e6e6`; it is a presentation design contract, not an implementation | Use as PRA-05 design authority; implementation remains gated by PRA-02/03 correctness and representative fixtures |
| WP-RA-02-A | Desktop presentation integration merged by PR #40 at `6ec34ea`; BASELINE-01 source audit verified the integrated pipeline and test evidence | Reuse the existing implementation; do not reimplement PRA-01/viewport plumbing |
| macOS | Existing reports identify macOS evidence as incomplete in several foundational areas | Treat macOS as a first-class validation lane, not a late optional check |

At the start of each phase, verify the actual default-branch code, open PRs, CI and relevant implementation reports. Never infer implementation completion from a document's existence.

## 5. Product milestones

| Milestone | Product outcome | Exit condition |
|---|---|---|
| M0 — Executable roadmap | One approved project sequence, ownership and task rules | This plan reviewed and merged; in-flight work reconciled |
| M1 — Reliable project foundation | Create/open/save/close a local project and map without bypassing Core Domain | End-to-end project lifecycle passes on Windows and macOS |
| M2 — Usable map workspace | Open a map, navigate, inspect layers/objects and perform basic manual edits | A user can create, edit, save, close and reopen a basic map |
| M3 — Production map viewport | Correct, responsive multi-scale 2D renderer with vector and raster presentation | Renderer correctness, lifecycle and benchmark gates pass |
| M4 — Geography authoring | Create and edit land/water, elevation, terrain, rivers and other natural geography | Terrain/field authoring vertical slices pass and persist correctly |
| M5 — Settlement and built environment | Create settlements, roads, buildings, interiors and caves | Exterior/interior authoring flows pass through Domain Services |
| M6 — World creation workflow | Guided initialization, map generation and optional AI assistance | Generated proposals can be reviewed, validated and applied safely |
| M7 — Authoring and export workflow | Layout, annotation, print/export and project interchange | Reproducible exports and project recovery pass |
| M8 — V1 release candidate | Stable Windows/macOS application with documented limitations | Release acceptance matrix passes; no blocking P0/P1 issues |

Milestones are capability outcomes, not calendar promises. A milestone may contain several design and implementation packages.

## 6. Phased development roadmap

### Phase 0 — Establish the project execution baseline

**Goal:** turn the current collection of design documents and active PRs into one controlled development programme.

**Design and planning**
- Maintain this root `plan.md` as the top-level sequence.
- Reconcile the current authoritative docs, accepted decisions, proposals and historical records.
- Build a dependency map between Core Domain, Application Foundation, Map Engine, Field, Editor UI, Renderer, Map Generation and AI Assistant.
- Identify open PRs and in-flight work that could overlap with planned tasks.
- Define a consistent work-package template: objective, baseline SHA, scope, non-goals, design references, file ownership, dependencies, implementation, tests, acceptance and handoff.

**Implementation/process**
- Reconcile the completed WP-RA-02-A / PR #40 integration, the merged PR #41 contract and BASELINE-01 audit as the current baseline.
- Review and merge this roadmap through its own PR.
- Create the first ordered execution queue only after checking current branch/PR state.

**Exit criteria**
- One agreed roadmap exists in the default branch.
- In-flight tasks have explicit status and do not conflict with new work.
- The next executable work package has a known baseline and acceptance criteria.

### Phase 1 — Product requirements and end-to-end UX design

**Goal:** define the first usable product, not just subsystem APIs.

**Design work**
1. Define the V1 user journeys:
   - create a project;
   - create/open a world map;
   - navigate and change map scale;
   - create and edit basic map objects/geometries;
   - organize layers and inspect object properties;
   - save, close and reopen;
   - recover from invalid input or interrupted operations.
2. Define the initial map-authoring capability set and explicitly defer advanced features.
3. Define the main desktop shell and map workspace information architecture.
4. Define empty, loading, error, read-only, editing, saving and recovery states.
5. Define keyboard, pointer, accessibility and high-DPI behavior.
6. Produce low-fidelity interaction flows/wireframes before detailed visual polish.

**Implementation work**
- No broad UI implementation before the main flows and interaction contracts are approved.
- A small disposable UI spike is allowed only to validate a risky Avalonia interaction or rendering assumption.

**Exit criteria**
- A testable V1 scope and acceptance scenario set exists.
- The basic map workspace and object-editing flows are unambiguous.
- Product-critical unknowns are recorded with owners and resolution gates.

### Phase 2 — Architecture reconciliation and technical foundation audit

**Goal:** verify that the existing architecture can support the Phase 1 journeys without contradictory ownership or missing formal operations.

**Design work**
- Map each user journey to Application Services, Domain Services, persistence, spatial services and presentation.
- Reconcile ME-01/02/03/04/05/06/07 with Core Domain and AF-01/02.
- Identify required formal operations and their atomicity/history semantics.
- Verify project package, Asset Store, database, migrations and recovery boundaries.
- Verify target framework, SDK, Avalonia and Skia package matrix from the actual repository. Resolve the recorded framework mismatch at repository level before renderer integration.
- Identify which existing design contracts are Accepted/Frozen versus working proposals.

**Implementation work**
- Close only proven foundational defects and missing ports required by the first vertical slice.
- Add architecture-level integration tests for Project → Map → Domain read/write → persisted state → reopen.
- Do not add new infrastructure or services where in-process modules suffice.

**Exit criteria**
- No unresolved ownership conflict blocks the first vertical slice.
- Repository build/test baseline is reproducible.
- Windows and macOS validation environments are identified.
- Required missing Domain operations are prioritized and bounded.

### Phase 3 — Core Domain and persistence completion for map authoring

**Goal:** provide complete authoritative operations needed by the initial map editor.

**Design work**
- Review outstanding Core Domain gaps, including commands/events/state changes, relationships, checkpoints, field dataset authority and formal geometry operations where required by the actual source baseline.
- Define atomic write boundaries and historical reconstruction behavior for each required operation.
- Define validation/error contracts and stable identity rules.
- Reassess any interim schema/API that is unused and can be simplified before release.

**Implementation work**
- Implement missing domain operations in dependency order.
- Ensure UI and AI paths both call the same Application/Domain Services.
- Add real database tests, transaction rollback/failure injection, idempotency and reopen/reconstruction tests.
- Keep migrations and schema changes direct and clean where no released data exists.

**Exit criteria**
- All operations required by the basic manual map-authoring scenarios have formal Domain Services.
- Formal writes are atomic and history behavior is deterministic.
- No UI-direct persistence path exists.
- Tests prove success, rejection, rollback, retry and recovery behavior.

### Phase 4 — Desktop application shell and project lifecycle

**Goal:** make the application reliably usable as a Windows/macOS desktop program.

**Design work**
- Confirm AF-01/AF-02 lifecycle contracts and project package format.
- Define startup, project selection, create/open/close, session ownership, unsaved changes and shutdown.
- Define error reporting, diagnostics and local file recovery behavior.

**Implementation work**
- Verify and complete the existing Application Foundation rather than rebuilding it.
- Complete project create/open/save/close and map workspace session wiring.
- Integrate real Core Domain services with project package and Asset Store.
- Add UI shell, navigation, project/map workspace and basic inspector surfaces.
- Validate clean install/build/startup and project round-trip on Windows and macOS.

**Exit criteria**
- A project can be created, closed, reopened and validated without manual database intervention.
- Session shutdown/cancellation does not leak resources or publish late results.
- Platform-specific packaging and file-system behavior are documented.

### Phase 5 — Map Engine spatial and geometry foundation

**Goal:** make spatial behavior correct and reusable independently of UI and renderer backend.

**Design work**
- Reconcile CRS/Map Space, precision, coordinate transforms and geometry contracts.
- Define geometry service APIs for predicates, measurement, clipping, simplification, hit testing and snapping.
- Define spatial-index lifecycle and invalidation.
- Define source geometry versus derived geometry rules, including Bézier handling and topology preservation.
- Define deterministic interaction result ordering and query freshness.

**Implementation work**
- Implement/verify typed coordinate spaces and transform pipeline.
- Implement geometry operations and validation required by the editor.
- Implement spatial index adapters and broad-phase/narrow-phase query path.
- Implement hit-test/snap candidate evaluation through spatial services.
- Add property-based, boundary, precision, topology and deterministic tests.

**Exit criteria**
- Geometry semantics do not depend on Avalonia or Skia.
- Queries return stable source identities and are revalidated before formal apply.
- Boundary, precision and topology tests pass.

### Phase 6 — Production map rendering

**Goal:** deliver a correct, multi-scale, high-density 2D viewport.

**Design sequence**
1. Review and close PRA-01 Presentation Context & Immutable Scene Snapshot implementation.
2. Complete the Avalonia viewport host and Skia lifecycle spike (PRA-02).
3. Integrate projection, spatial culling and geometry interaction (PRA-03).
4. Implement text shaping and viewport-wide label layout (PRA-04).
5. Implement batching, Spatial Chunks, LOD, cache and performance work (PRA-05).
6. Integrate animation presentation (PRA-06) when timeline/domain movement inputs are ready.
7. Integrate Field Package raster presentation (PRA-07) when Field read contracts are ready.
8. Complete symbols, decorations and scene composition (PRA-08).

**Post-PR #41 implementation checkpoint (2026-11-27; source audit @ `6ec34ea`, current master @ `73da06f`)**

- PRA-01: IMPLEMENTED and connected to the production workspace path (PR #40); snapshot/publication/lifecycle tests exist.
- PRA-02: Avalonia/Skia viewport integration skeleton and lifecycle path are implemented; actual GUI acceptance evidence remains incomplete, especially macOS local execution.
- PRA-03: NOT READY / NOT IMPLEMENTED as a complete capability. Coordinate transform mathematics, spatial index, geometry hit testing and snapping are absent.
- PRA-04: NOT STARTED. Text shaping, glyph drawing and viewport-wide label layout are absent.
- PRA-05: DESIGN INPUT READY (Spatial Chunk & LOD Contract V0.1 merged in PR #41); IMPLEMENTATION BLOCKED until PRA-02/03 correctness and representative map fixtures are demonstrated. PRA-04 label-shaping outputs must be integrated where labels participate in chunk/LOD presentation.

The renderer currently publishes and draws a real immutable snapshot, but the snapshot is a blank-scene presentation and the renderer clears the background only. Do not interpret PRA-01/PRA-02 integration as map-content rendering completion. Close viewport interaction/coordinate and content-pipeline prerequisites before Chunk/LOD implementation.

**Design constraints**
- Follow the merged Spatial Chunk & LOD Contract V0.1 (`73da06f`); it is a presentation contract, not Domain storage.
- Keep Field Package Tiles, Spatial Index entries, Render Chunks and LOD representations separate.
- Use a fixed-grid Render Chunk baseline first; defer numeric sizes to benchmarks.
- Use four conceptual display levels: World, Region, Settlement and Local Detail.
- Building interiors remain separate local scenes, not another world-map zoom level.
- No per-object Avalonia Control/Visual.
- No dedicated GPU API stack unless measured evidence proves the default path insufficient.

**Implementation and test sequence**
- Immutable snapshot and stale-result tests first.
- Viewport lifecycle, resize, DPI, resource disposal and device-loss tests.
- Correct vector drawing, clipping, seam handling and label uniqueness.
- Pan/zoom and LOD transitions.
- Raster/Field integration.
- 100k/300k/500k synthetic feature fixtures and authored dense-map fixtures.
- Windows and macOS performance/correctness evidence.

**Exit criteria**
- One frame consumes one complete immutable snapshot.
- No stale snapshot publication or mixed-context frame.
- No geometry/identity mutation by rendering.
- Cross-chunk seams and labels are correct.
- Performance targets are based on measurements on both platforms.

### Phase 7 — Map Editor UX and manual authoring vertical slices

**Goal:** connect the map viewport to real user operations.

**Design work**
- Finalize ME-06 tool, gesture, typed-intent, freshness and recovery contracts.
- Define layer panel, object selection, property inspector, context actions and editing modes.
- Define each tool's preview, validation, cancellation, apply and recovery flow.
- Define the smallest useful set of tools for the first usable map editor.

**Implementation work**
Deliver in thin vertical slices, each including UI + Application + Domain + tests:
1. viewport navigation and layer visibility;
2. select/inspect object and show properties;
3. create basic point/line/polygon geometry;
4. edit vertices/segments and validate geometry;
5. delete/restore or otherwise formally remove objects according to Domain history rules;
6. undo/redo or historical recovery only through the approved command/history model;
7. save/reopen and verify the same formal state.

**Exit criteria**
- Basic manual authoring works without AI or external services.
- Every formal edit passes through Domain Services.
- Cancellation, stale spatial query, failed apply and retry are observable and safe.

### Phase 8 — Terrain and natural geography authoring

**Goal:** deliver terrain and natural-world authoring using the existing ME-02/ME-05 contracts.

**Design work**
- Reconcile ME-02 terrain, land/water, hydrology, climate/ecology and calculation contracts.
- Confirm Field Dataset/Revision authority, Field Package format, tiling and no-data semantics.
- Define terrain editing workflows, previews, validation and provenance.
- Define how raster fields and vector features compose visually.

**Implementation work**
- Complete Field Dataset/Revision Domain and Asset integration where still missing.
- Implement Field Package read/write/validation and deterministic tiling.
- Implement land/water and elevation authoring vertical slices.
- Add terrain visualization, contours/hillshade/biome or other derived displays as separate presentation products.
- Add hydrology and further natural features in dependency order.
- Test large rasters, boundary halos, package corruption and interrupted writes.

**Exit criteria**
- Field source truth is versioned and integrity-checked.
- Editing and visualization do not confuse source fields with derived display caches.
- Large Field Packages can be read and rendered by viewport demand.

### Phase 9 — Settlements, buildings, interiors and caves

**Goal:** deliver built-environment authoring on top of the stable map and geometry foundation.

**Design work**
- Use ME-03 and ME-04 domain/UX contracts as the baseline.
- Confirm settlement/road/building relationship semantics and ownership.
- Confirm Building, Level, Space, Entrance, Opening, Cave and Subsurface Network boundaries.
- Define world-map exterior versus Building-local interior scene navigation.

**Implementation work**
- Implement settlement and road authoring.
- Implement Building footprint and exterior authoring.
- Implement Building-local levels, rooms/spaces, shared boundaries and access connections.
- Implement cave survey and underground-space workflows.
- Integrate selection/navigation between world map and separate interior scenes.
- Add consistency validation across map and building/cave aggregates.

**Exit criteria**
- Formal Building/Cave operations remain Core Domain-owned.
- Interior geometry remains in its declared local coordinate frame.
- Exterior/interior relationships are navigable and validated.

### Phase 10 — Guided initialization, map generation and AI Assistant

**Goal:** make complex map creation faster without making AI a dependency of core authoring.

**Design work**
- Reconcile Guided Initialization and Map Generation specification/workflow/session lifecycle.
- Define generation inputs, constraints, provenance, candidate review, validation and application.
- Reconcile AI Assistant context, provider, tool catalog, authorization, cancellation and recovery.
- Define offline/manual fallback behavior and user-configured provider boundaries.

**Implementation work**
- Implement guided map initialization.
- Implement generation task/session lifecycle and candidate isolation.
- Implement generation adapters/algorithms incrementally, each with deterministic fixtures and quality validation.
- Integrate optional user-configured LLM providers through Provider Adapters.
- Route all formal changes through the same Domain/Application write paths as manual editing.
- Add cancellation, retry, budget, provenance and stale-base tests.

**Exit criteria**
- AI and generation can propose but cannot bypass formal validation or authorization.
- Manual map creation remains fully usable without a configured model.
- Generated results are reproducible or carry explicit algorithm/seed/version provenance.

### Phase 11 — Layout, export, import and project interchange

**Goal:** turn editable maps into dependable outputs and portable local projects.

**Design work**
- Define Layout Workspace, page sizes, map frames, legends, scale bars, north arrows, typography and annotations.
- Define export snapshot semantics and raster/vector/PDF output scope.
- Define project backup/recovery and interchange format requirements.
- Define import validation, coordinate reference handling and unsupported-data reporting.

**Implementation work**
- Implement layout composition independent of interactive viewport screenshot capture.
- Implement high-resolution raster and PDF export from stable presentation snapshots.
- Implement project archive validation, backup/recovery and supported import/export formats.
- Test large export, cancellation, partial file cleanup and reproducibility.

**Exit criteria**
- Exports are based on a stable source snapshot and are reproducible for pinned inputs.
- Project recovery and interchange do not bypass Domain validation.
- Unsupported import content is reported rather than silently discarded.

### Phase 12 — Stabilization and V1 release candidate

**Goal:** prove the complete product is reliable on Windows and macOS.

**Design and release work**
- Freeze the V1 scope and list explicit deferred features.
- Define supported OS versions, minimum hardware, project format version and backup policy.
- Define diagnostics, crash recovery, logging/privacy and support bundle behavior.
- Review licensing, third-party notices, packaging and update strategy.

**Implementation and verification**
- Run clean Windows and macOS build/install/startup tests.
- Run complete project create/edit/save/reopen/export workflows.
- Run persistence integrity, migration, crash/interruption and recovery tests.
- Run renderer correctness and performance suites.
- Run accessibility, keyboard, high-DPI, localization and large-project tests.
- Fix all release-blocking defects; do not hide failures behind retries or skipped tests.

**Exit criteria**
- No unresolved P0/P1 defect.
- Required Windows/macOS acceptance matrix passes independently.
- All shipped capabilities have user-facing behavior, automated tests and recovery/error paths.
- Release notes clearly state supported features and limitations.

## 7. Single-work-package execution and runtime lifecycle

### 7.1 Development execution

- Execute one work package at a time. Finish its design, implementation, tests, independent review and integration before starting the next dependent package.
- Do not create parallel agent/worktree lanes as a project requirement. A short-lived branch is permitted for the active work package and its review; merge it before the next package.
- Keep file ownership and scope explicit so the active change remains reviewable.
- The project is a local-first, single-user desktop application. Do not add distributed coordination, multi-user collaboration or concurrency infrastructure without a separately approved product need.

### 7.2 Runtime asynchronous correctness

Single-user does not mean that the UI runtime is synchronous. UI events, background scene preparation, cancellation, file operations, window close and renderer resource disposal can interleave. The application must still prove:

- stale or cancelled work cannot publish after a newer request or after close;
- one frame consumes one complete immutable snapshot;
- UI/render thread affinity is respected;
- cancellation and disposal are deterministic and do not leak resources;
- file writes and project lifecycle transitions remain atomic and recoverable.

Use the smallest in-process mechanisms that satisfy these guarantees. Do not introduce a generalized scheduler, distributed queue, event bus or multi-user locking model.

### 7.3 Integration cadence

- Each work package is based on the latest integrated default-branch SHA.
- Run package-specific tests, then the full solution build/test matrix before merge.
- Review the diff and verify that unrelated working-tree content is untouched.
- Update this plan and the relevant contract only when a decision or dependency changes.

## 8. Standard work-package lifecycle

Every implementation package follows this lifecycle:

1. **Baseline audit** — inspect current code, docs, tests, PRs and actual branch SHA.
2. **Design** — state the problem, ownership, contracts, alternatives, decision and non-goals.
3. **Dependency review** — list prerequisites, consumers, owned files and integration order.
4. **Implementation plan** — split into small independently testable changes.
5. **Implementation** — use isolated branch/worktree; do not expand scope silently.
6. **Verification** — unit, integration, failure-injection, platform and performance tests appropriate to the change.
7. **Independent review** — inspect code and evidence, not only the delivery report.
8. **Integration** — merge only after review and CI; resolve branch drift.
9. **Documentation update** — record accepted decisions, actual implementation state and known limitations.
10. **Next package** — start only after dependencies and baseline are refreshed.

### Required delivery report

- work package ID and objective;
- baseline SHA and final HEAD SHA;
- branch/PR;
- files changed and design decisions;
- tests executed with exact commands and results;
- CI/platform evidence;
- failure-path and concurrency evidence where applicable;
- deviations, limitations and deferred items;
- known risks and suggested next package.

## 9. Quality gates applying to every phase

### Architecture
- one authoritative owner for each formal datum and operation;
- no renderer/UI direct persistence;
- no duplicate domain/history authority;
- no hidden cross-layer dependency;
- no unnecessary compatibility scaffolding for unreleased implementations.

### Correctness
- explicit validation and error outcomes;
- atomicity and rollback for formal writes;
- deterministic identity and historical behavior;
- boundary, precision and topology tests for spatial operations;
- stale/cancelled asynchronous work cannot publish or overwrite newer state.

### Performance and reliability
- measure representative workloads instead of promising unmeasured frame rates;
- bound memory, caches and background work;
- test large maps and large Field Packages;
- prove recovery from interrupted writes and failed exports.

### Platform
- Windows and macOS are independent acceptance targets;
- a build on one platform does not prove the other;
- verify file paths, permissions, DPI, input, fonts, native dependencies, packaging and resource disposal on both.

## 10. First execution queue

This queue is updated after BASELINE-01 and PR #41. Statuses below are grounded in the 2026-11-27 audit and current GitHub state. Continue sequentially; refresh the default-branch SHA before each work package.

| Order | Work package | Status | Deliverable / gate |
|---:|---|---|---|
| 1 | ROADMAP-01 — Review and merge this plan | COMPLETE — PR #42 | Root `plan.md` is the project execution baseline |
| 2 | BASELINE-01 — Audit master, open PRs, CI, code and docs | COMPLETE — source audit @ `6ec34ea` | Capability/dependency/gap evidence; post-merge addendum tracks PR #41 |
| 3 | WP-RA-02-A — Desktop presentation integration | COMPLETE — PR #40 | PRA-01 production pipeline connected; implementation evidence audited |
| 4 | WP-RA-03 — Spatial Chunk & LOD Contract V0.1 | COMPLETE — PR #41, merge `73da06f` | Contract is authoritative design input for PRA-05; no implementation implied |
| 5 | PRODUCT-01 — V1 product scope and end-to-end acceptance scenarios | PENDING | Prioritized V1 journeys, feature boundaries and acceptance matrix |
| 6 | ARCH-01 — Cross-domain dependency and architecture reconciliation | PENDING | Approved dependency map and missing-operation list |
| 7 | FOUNDATION-01 — Close only blocking Core Domain/Application gaps | PENDING | Project/map vertical-slice foundation proven on Windows/macOS |
| 8 | PRA-01 — Immutable Presentation Context and Scene Snapshot | COMPLETE — verified by BASELINE-01 | Snapshot, freshness, cancellation and publication tests pass |
| 9 | PRA-02 / WP-30C — Viewport lifecycle, interaction and camera transforms | PARTIAL / NEXT RENDERER GATE | Complete pan/zoom/input/transform path and obtain platform GUI evidence |
| 10 | DESIGN-EXTENT + DESIGN-SCENE — Map bounds and immutable SceneItem/style contract | PENDING | Freeze authoritative spatial extent and the minimal point/line/polygon scene schema |
| 11 | WP-40-B1 — Time-zero content projection and minimal geometry rendering | PENDING | Real Domain map content becomes immutable scene items and visible Skia geometry |
| 12 | PRA-03 — Projection, spatial index and geometry interaction | PENDING | Coordinate transforms, broad/narrow phase, hit-test/snap correctness |
| 13 | PRA-04 — Text shaping, glyph drawing and viewport-wide labels | PENDING | HarfBuzz/package proof, CJK/mixed-script shaping and deterministic label layout |
| 14 | PRA-05 — Batching, Spatial Chunks, LOD, caches and performance | DESIGN READY / IMPLEMENTATION BLOCKED | Implement only after PRA-02/03 correctness and representative fixtures; consume PRA-04 label capabilities where applicable |
| 15 | TERRAIN-01 — Field and natural geography vertical slices | PENDING | Versioned field authoring and viewport presentation |
| 16 | BUILT-01 — Settlement, road, Building and interior slices | PENDING | Exterior/interior authoring flows |
| 17 | GENERATION-01 / AI-01 / OUTPUT-01 / RELEASE-01 | PENDING | Guided generation, optional AI, export/recovery and Windows/macOS release acceptance |

No work package may bypass a failed prerequisite. PR #41 being merged closes only the PRA-05 design-input gate; it does not authorize PRA-05 implementation.

## 11. Change control and status reporting

- This file controls the overall sequence; domain contracts control detailed semantics within their scope.
- If a phase reveals a missing dependency, update this plan before starting work that relies on the new assumption.
- Changes to product scope or milestone order must include the reason, affected packages and dependency impact.
- Mark a task Complete only after code, tests, independent review and integration evidence exist.
- Keep statuses factual: Proposed, Designed, In Progress, In Review, Implemented, Verified, Blocked or Deferred.
- Do not mark a proposal as implemented or a platform as supported without evidence.
- Historical documents remain for traceability, but current accepted contracts and current code are the execution authority.

## 12. Immediate next action

After this roadmap is merged, execute **BASELINE-01**: inspect the latest default branch, all open map-related PRs (including renderer work), current build/test state and the actual source tree. Produce a dependency/status matrix and identify the single next work package. Do not start implementation before this audit is complete.
