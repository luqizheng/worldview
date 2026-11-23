# World View — Product Implementation Roadmap V1

- **Version:** 1.0
- **Date:** 2026-11-23
- **Status:** Active implementation planning baseline
- **Repository:** https://github.com/luqizheng/worldview
- **Target:** Local-first single-user desktop application for Windows and macOS
- **Primary implementation stack:** C# / .NET / Avalonia; Avalonia + Skia is the leading 2D viewport direction
- **Authority:** This roadmap organizes implementation work. It does not supersede accepted ADRs, Core Domain contracts, renderer contracts, or specialist product/domain baselines.

---

## 1. Purpose

This document defines the implementation order for the complete World View desktop product.

The product has several distinct subsystems. They must not be collapsed into one implementation stream:

1. **Desktop Application** — application startup, windows, navigation, dialogs, preferences, lifecycle and platform integration.
2. **Map Management** — Projects and Maps as managed domain resources: create, list, open, close, rename, duplicate, delete where formally supported, and manage Map/Branch context.
3. **Map Rendering** — read-only presentation of an authoritative Map/Branch/Historical-Time state in a viewport.
4. **Map Authoring** — tools and workflows that propose, validate, review and formally apply changes to map data.
5. **Core Infrastructure** — Core Domain, persistence, Asset storage, application services, immutable read projections, coordinate/spatial services, test and packaging foundations used by the four product subsystems.

**Map Rendering and Map Authoring are separate subsystems.** A renderer displays a coherent map state; it does not create or edit formal map data. An authoring workflow stages and validates an operation, then routes an approved typed operation through Core Domain. A blank viewport with pan/zoom is a rendering milestone, not an authoring milestone.

The first product objective is a usable desktop application that can create and manage Projects/Maps, open a Map, display a basic viewport and reopen the same data after restart. Complex terrain, building, cave, road, connection and template editing are later work.

## 2. Product constraints and implementation principles

### 2.1 Product constraints

- Local-first, single-user desktop application.
- Windows and macOS are the required desktop targets.
- No cloud synchronization, multi-user collaboration, collaborative cursors, distributed locks or distributed conflict-resolution subsystem in this roadmap.
- Low-concurrency local persistence; retain correct transaction and integrity boundaries, but do not add enterprise/distributed coordination without evidence.
- The project is still in development and has no historical production user data. Model and schema corrections may be made directly during development when justified; do not build a legacy-data migration program for nonexistent production data. Once a released package format/schema is declared supported, normal forward upgrade and backup safety still apply.
- Formal Map/Object/Geometry/History facts remain owned by Core Domain and its Domain Services.
- Windows/macOS support is established by evidence on both platforms, not inferred from shared source code.

### 2.2 Non-negotiable boundaries

| Concern | Owner | Must not become |
|---|---|---|
| Desktop lifecycle, windows, dialogs, navigation | Desktop Application | A second owner of map facts |
| Project Package and Project Session lifecycle | Application / Infrastructure | A separate Map persistence unit |
| Map, Object, Branch, Historical Time and formal state | Core Domain | Renderer or ViewModel state |
| Read projections and presentation context | Application / Presentation | Direct SQL reads from UI |
| Projection, spatial predicates, hit-test/snap and derived spatial data | Map Engine / spatial services | Formal semantic relationship authority |
| Scene assembly and immutable scene publication | Presentation subsystem | A second history or geometry authority |
| Drawing a published scene | Renderer backend | Database access or formal mutation |
| Selection, tools, staging, preview and review | Map Authoring / Editor | A generic CRUD bypass around Domain Services |
| AI suggestions | AI Assistant | Direct formal Core Domain writes |

### 2.3 Keep implementation proportionate

- Prefer explicit, maintainable local components over distributed or generalized enterprise frameworks.
- Use the existing SQLite/Core Domain ownership and transaction boundaries; do not add concurrency machinery merely because it is common in server software.
- Do not introduce a generic GIS clone, arbitrary plug-in runtime, universal geometry CRUD API, global persistent topology graph or full 3D editor as a prerequisite for V1.
- Do not treat design completion, a proposed schema, a test specification or a Figma frame as implementation evidence.
- Do not implement specialist tools until their exact descriptors, typed operations and formal write paths are adopted and verified.

## 3. Current known baseline (planning input, not a fresh code audit)

The repository already contains substantial design and partial implementation foundations. The following is a planning summary of the documentation and implementation records reviewed for this roadmap; WP-00 must verify the current checked-out source, tests and platform evidence before implementation tasks are issued.

| Area | Known baseline | What must not be assumed |
|---|---|---|
| Desktop application | AF-01 architecture and AF-02 implementation report describe Avalonia shell, Project Package lifecycle, sessions, bootstrap and Asset Store | The report's historical Core Domain gate must be rechecked against the later CD-01/CD-02 implementation; do not assume every UI flow is now connected |
| Core Domain | CD-01/CD-02 implementation records describe Project/Map/Branch foundations, Object/Initial State, registry, geometry/Asset foundations and Building/Cave typed aggregate adoption | Command/Event history writes, relationships, Field Dataset, checkpoints, all specialist write paths and macOS proof are not implied complete |
| Map management | AF-01/AF-02 define Project and Map Workspace lifecycle and Core Domain read boundaries | Full Map create/list/open/close/rename/copy/delete and Branch management must be source-audited individually |
| Map Rendering | Production Renderer Architecture V0.2, renderer/scene contracts, PRA-01 handoff/audit and ME-07 package plan exist | A viewport, Skia drawing, scene publication or cross-platform renderer is not complete merely because contracts exist |
| Map Authoring | ME-01/ME-02/ME-03/ME-04/ME-05/ME-06/ME-08 provide substantial design baselines | Designed tools do not imply adopted descriptors, Commands, persistence or working UX |
| Platform | Windows CI/build evidence exists in implementation records | macOS is unverified wherever a specific macOS runtime/build/test report is absent |

Source documents include:
- `docs/application/AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md`
- `docs/application/AF-01_DEVELOPER_HANDOFF_v0.1.md`
- `docs/application/AF-02_DEVELOPER_IMPLEMENTATION_PROMPT_v0.1.md`
- `docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md`
- `docs/architecture-baseline-v1.0.md`
- `docs/map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md`
- `docs/map-engine/world-view-map-rendering-visualization-architecture-v0.3.md`
- `docs/map-engine/PRA-01_IMPLEMENTATION_AUDIT_v0.1.md`
- `docs/map-editor/world-view-me-07-rendering-and-technical-architecture-v0.1.md`
- `docs/map-editor/world-view-me-08-08-cross-architecture-integration-review-and-implementation-handoff-v0.1.md`
- `docs/implementation/IMPLEMENTATION-LOG.md`

## 4. Delivery strategy and milestones

The implementation order is deliberately product-first:

```text
WP-00 Whole-product inventory
        |
        v
WP-10 Desktop application foundation
        |
        v
WP-20 Project and Map management
        |
        v
WP-30 Basic Map Rendering
        |
        +----> M1 Basic Map Application
        |
        v
WP-40 Complete rendering foundation
        |
        v
WP-50 Map Authoring framework
        |
        v
WP-60 Specialist authoring capabilities
```

Core Infrastructure work is a cross-cutting dependency and is scheduled only where the next user-visible milestone requires it. It is not a reason to build a large abstract platform in advance.

### M0 — Implementation readiness

**Goal:** establish what actually exists and what is missing across the whole product.

M0 is complete when WP-00 produces a source-backed capability inventory, dependency map, risk list and a bounded next implementation slice. No production code changes are required in WP-00.

### M1 — Basic Map Application

**Goal:** a user can create a Project, create a Map, find it in the Map list, open it, see a basic map viewport, navigate the viewport, close the application and reopen the same Map.

M1 is the first end-to-end product milestone. It does not require specialist map authoring.

### M2 — Readable Map Viewer

**Goal:** render real adopted map content reliably, with layers, styles, symbols, labels and scale-aware presentation, while keeping the rendering pipeline independent from formal map data.

### M3 — Safe Map Authoring Foundation

**Goal:** introduce a reusable editor interaction framework with selection, Inspector, tool routing, transient geometry, preview/staging, validation, review, apply/cancel and recovery. Only formally adopted operations are enabled.

### M4 — Specialist Map Authoring

**Goal:** deliver specialist workflows in dependency order: natural geography, settlements/roads, buildings/interiors/caves, spatial sites/groups, typed connections and templates/modules. Each capability is enabled only when its Core Domain and spatial-service gates are closed.

Milestones are acceptance outcomes, not calendar estimates. Do not declare a milestone complete from documentation or a successful build alone.

## 5. Work packages

### WP-00 — Whole-product implementation inventory

**Priority:** P0  
**Type:** Audit / no business-code changes  
**Entry:** Current master source and documentation are available.

Scope the entire repository, not only ME-08 or Map Authoring:

1. Desktop application: startup, main window, navigation, dialogs, settings, lifecycle and Windows/macOS packaging.
2. Project management: create/open/close/recent/backup/recovery.
3. Map management: create/list/open/close/rename/copy/delete and Project/Map/Branch selection.
4. Core Domain: Project/Map/Branch/Calendar, Object/Initial State, definitions, registries, Geometry/Asset, Commands/Events, relationships, Field Dataset, checkpoints and history reconstruction.
5. Map Rendering: PRA-01 snapshot preparation/publication, viewport, Skia drawing, transforms, layers, labels, symbols, raster/field rendering, LOD, caches and export.
6. Map Authoring: selection, Inspector, tools, transient sessions, validation, Candidate/Change Set, formal apply, cancellation, undo/recovery and specialist tools.
7. Infrastructure: SQLite/schema/migrations, Asset Store, package format, logging, configuration, diagnostics and performance.
8. Tests and evidence: unit/integration/UI tests, Windows and macOS builds/runs, smoke tests, CI and known gaps.

For every capability, use one explicit status:
- **Implemented + verified**
- **Implemented, verification missing**
- **Partial**
- **Designed only**
- **Missing**
- **Blocked by dependency**
- **Not applicable to current milestone**

Every “implemented” claim must identify source paths and tests. Every platform claim must identify the exact platform evidence. Record contradictions between implementation reports and current source rather than silently choosing one.

**Deliverables:**
- `docs/implementation/WHOLE_PRODUCT_CAPABILITY_INVENTORY_v1.0.md`
- `docs/implementation/whole-product-capability-inventory-v1.0.json`
- A dependency graph and P0/P1 blocker list.
- A recommended WP-10/WP-20/WP-30 implementation slice with acceptance tests.

**Restrictions:** no business-code refactor, schema change or broad cleanup in WP-00. Small audit scripts/tests may be used if they do not alter product behavior. Stop and report before implementing a newly discovered architectural gap.

### WP-10 — Desktop application foundation

**Goal:** make the application shell dependable and usable independently of map rendering.

Scope:
- Verify startup/shutdown, exception handling and dependency composition.
- Verify Start surface, Create/Open Project flows and recent Project behavior.
- Verify Project Package validation, session ownership, close/reopen and recovery behavior.
- Establish the main desktop layout, navigation, menus, toolbar/status surfaces and settings entry points.
- Establish window resize, high-DPI, keyboard/focus, native file-picker and platform-specific shortcut behavior.
- Ensure UI code uses Application/Core Domain services and never issues SQL.
- Verify Windows and macOS packaging/startup paths.

Reuse AF-01/AF-02. Do not rewrite a working application foundation simply to match a new folder layout. Only close audited gaps.

**Exit criteria:**
- App starts and shuts down cleanly on Windows and macOS.
- Create/Open/Close Project workflows pass.
- Invalid or corrupt packages fail safely without destructive repair.
- Project sessions and child Map workspaces close in the correct order.
- Main navigation and core shell surfaces are usable at supported window sizes and DPI settings.

### WP-20 — Project and Map management

**Goal:** make Project and Map lifecycle usable without a renderer or authoring tools.

Scope:
- Create and open a Project Package.
- List Maps from Core Domain read projections.
- Create a Map through the formal Core Domain service.
- Open and close a Map Workspace Session.
- Display Map identity, selected Branch and Historical Time context.
- Provide explicit Map/Branch selection and safe context switching.
- Implement rename/copy/delete only where the formal Core Domain semantics and ownership rules already support them; otherwise mark the action unavailable and record the owning design gate.
- Persist only appropriate application preferences (for example, recent Project and last-opened Map) outside formal world history.
- Ensure close/reopen and restart restore access to the same formal Map data.

**Exit criteria:**
- Project and Map operations use formal Application/Core Domain services.
- No shadow Map table/model or ViewModel-owned formal Map state exists.
- A Map can be created, listed, opened, closed and reopened.
- Branch and Historical Time context are resolved by Core Domain.
- Failure paths do not leave a half-open workspace or corrupt the package.

### WP-30 — Basic Map Rendering (M1 viewport)

**Goal:** display an empty but real Map workspace in a functioning viewport.

Scope:
- Complete or verify PRA-01 Presentation Context and immutable Scene Snapshot foundation.
- Implement the Avalonia viewport host and the initial Skia drawing path according to the approved framework/package matrix.
- Establish viewport coordinate state and explicit map-to-screen / screen-to-map transforms.
- Implement pan, zoom, resize and DPI-aware viewport behavior.
- Render a neutral background and map frame/bounds when those are available from formal read projections; do not invent formal geometry for a blank map.
- Display basic loading/empty/error states.
- Capture exactly one immutable scene snapshot per frame.
- Keep renderer callbacks read-only and bounded; no database, file I/O, Domain Service, ViewModel or formal mutation in the draw callback.
- Keep scene snapshots and backend resources disposable/rebuildable.

**M1 acceptance:**
1. Launch World View on Windows.
2. Create a Project Package.
3. Create a Map through Core Domain.
4. See the Map in the Project's Map list.
5. Open the Map workspace.
6. See the empty Map viewport (not a placeholder browser page).
7. Pan and zoom the viewport; resize the window and maintain correct transforms.
8. Close the Map and Project, exit the application, restart and reopen the same Map.
9. Verify Map identity and persisted data are unchanged.
10. Repeat the same user flow on macOS.
11. Verify no map data is created or changed by pan/zoom/rendering.
12. Run automated tests for snapshot consistency, transform correctness, session lifecycle and platform-specific viewport behavior.

**Explicitly out of scope for M1:** terrain painting, drawing or editing Objects, roads, rivers, buildings, interior plans, caves, spatial groups, typed connections, templates, AI-generated map changes, timeline animation, 3D rendering, export/layout and GIS basemap integration.

**Exit criteria:** all twelve acceptance items pass, with Windows and macOS evidence recorded. A successful CI build alone is insufficient.

### WP-40 — Complete the read-only rendering foundation (M2)

**Goal:** render actual adopted Map content and provide a stable, performant viewer.

Sequence (reuse ME-07/PRA work packages; do not duplicate their contracts):
1. PRA-03 projection adapter, coordinate transforms, spatial index and viewport culling.
2. Basic render-item/scene-layer composition and visibility/order.
3. PRA-04 text shaping and label placement, multilingual font fallback and decluttering.
4. Symbols, styles, map decorations and scale-aware representation.
5. PRA-05 LOD, batching, invalidation and measured cache policy.
6. PRA-07 raster/Field Package visualization only after parser/writer and Field Dataset read path are verified.
7. PRA-06 deterministic animation presentation only after authoritative Historical Time/Unit movement inputs are available.
8. Export/layout only after the same scene semantics can be rendered to a stable non-interactive target.

The exact order among these items may change based on dependencies, but static vector rendering and correctness come before raster, animation and export.

**Exit criteria:**
- One coherent immutable snapshot per frame.
- No stale or mixed-context publication.
- Deterministic presentation for pinned inputs within documented platform rasterization tolerances.
- Correct layer visibility/order, transforms, selection-independent rendering and read-only behavior.
- Representative performance evidence on both platforms before setting production scale claims.

### WP-50 — Map Authoring framework (M3)

**Goal:** build the shared authoring interaction foundation without enabling unsupported domain operations.

Scope:
- Shared spatial context and navigation.
- Selection and type-aware read-only Inspector.
- Tool capability declarations and input/gesture routing.
- Transient operation sessions and temporary geometry.
- Preview/staging and validation diagnostics.
- Candidate/Change Set review and authorization integration where applicable.
- Apply/Cancel and formal write-path routing through Core Domain.
- Local undo for uncommitted edits only; committed reversal uses adopted Core Domain History operations.
- Freshness checks, apply-time re-resolution, idempotency and unknown-outcome reconciliation.
- Keyboard/focus/accessibility and Windows/macOS parity.

Reuse ME-01-06, ME-05, ME-06 and ME-08 contracts. The editor must not create a second operation runtime or a generic persistence path.

**Exit criteria:**
- View-only actions remain distinct from formal mutations.
- Unsupported capabilities are clearly unavailable, not silently approximated.
- Cancel/discard leaves formal state unchanged.
- Apply requires an adopted typed operation and correct Core Domain write authority.
- Applied and Reconciled are distinct states.
- A failed renderer refresh does not misreport a successful formal commit as failed.

### WP-60 — Specialist authoring capabilities (M4)

**Goal:** deliver domain-specific tools on top of WP-50, each gated by its owning specialist and Core Domain contracts.

Recommended order:
1. **Natural geography (ME-02):** map frame, land/water and elevation, terrain features, hydrology, climate/ecology and derived analysis. Respect Field Dataset authority, package parsing and calculation contracts.
2. **Settlements and urban geography (ME-03):** settlement placement, political/administrative geography, land-use semantics, roads and paths. Road connectivity remains disabled until stable RoadEndpoint identity and formal endpoint relationships are adopted.
3. **Buildings and interior spaces (ME-04):** Building exterior and dedicated 2D interior scene; levels, spaces, openings and vertical circulation. Building/Cave design baselines do not by themselves enable formal authoring; verify current adopted descriptors, aggregate and Commands.
4. **Caves and underground facilities:** cave survey frames, 2D plan/section views, explicit uncertainty and typed passage connectivity; do not assume Building-local coordinates.
5. **Spatial Site and Group composition (ME-08):** Site navigation/composition and nested Groups; distinguish organization membership from physical containment and physical group movement.
6. **Typed connections:** enable each domain-specific connection independently after exact endpoint and Relationship descriptors are adopted. No universal persisted Edge.
7. **Templates, layouts, themes and modules:** dependency-closed preview and capability validation; required unsupported members block complete instantiation. Commit only within a proven authoritative atomic boundary.

The specialist order is a recommended dependency sequence, not permission to activate all features together.

**Exit criteria:** each specialist capability has its own implementation report, descriptor/Command evidence, domain validation tests, user workflow acceptance and Windows/macOS verification.

## 6. Dependencies and sequencing rules

1. WP-00 precedes implementation task generation. It may be repeated for a subsystem when source evidence changes materially.
2. WP-10 and WP-20 can proceed with read-only/textual Map summaries before rendering exists.
3. WP-30 depends on a real Map Workspace context and immutable read projection; it does not depend on editing tools.
4. WP-40 depends on WP-30's snapshot/viewport foundation.
5. WP-50 depends on stable rendering context, spatial services and ME-06 interaction contracts; read-only selection/Inspector can be brought forward when safe.
6. WP-60 depends on WP-50 plus each specialist's formal domain capability gates.
7. Core Domain and Asset gaps are implemented only when required by a specific user-visible slice. Do not implement every proposed schema or Command simply because a design document exists.
8. A capability blocked by missing formal authority must remain read-only, transient or unavailable; do not create shadow persistence.
9. Platform validation is continuous. Do not defer all macOS validation until the end.
10. One bounded work package should be handed to local implementation tooling at a time, with an explicit acceptance checklist and report.

## 7. Cross-cutting quality gates

Every implementation package must include:
- Source-backed preflight and exact baseline commit.
- Scope and non-goals; no unrelated refactor.
- Tests at the appropriate Domain/Application/Infrastructure/Presentation/UI layer.
- Debug and Release build/test evidence.
- Windows and macOS evidence appropriate to the package.
- Failure-path and recovery coverage for persistence/lifecycle changes.
- Updated implementation log and a concise report stating implemented, partial, blocked and unverified items.
- A review of architecture boundaries and no second authority.

Performance work follows correctness. Measure representative fixtures and report median/p95/p99 where applicable; do not invent throughput or object-count promises.

## 8. How to declare completion

A work package is complete only when:
1. Its source changes are present on the target branch.
2. Automated tests and required builds pass.
3. Its stated platform evidence exists.
4. Acceptance criteria are demonstrated.
5. The implementation report and `IMPLEMENTATION-LOG.md` are updated.
6. Any remaining blocker is explicitly recorded.
7. The relevant roadmap/work-plan status is updated after verifying GitHub master.

A design document, task prompt, merged PR or green CI alone does not establish end-to-end product completion.

## 9. Relationship to existing design workstreams

This roadmap does not discard specialist design work. It changes the implementation sequence so product foundations arrive before complex authoring.

- ME-01 remains the shared editor/product architecture baseline.
- ME-02 through ME-04 remain specialist authoring baselines.
- ME-05 remains the shared geometry, coordinate, Asset and spatial-operation authority.
- ME-06 remains the shared interaction/tool contract.
- ME-07/PRA remains the rendering/presentation implementation stream.
- ME-08 remains the spatial composition and domain-integration stream.

The old ME-08 WP-00 through WP-09 sequence is a **specialist Map Authoring implementation route only**. It is not the whole-product roadmap and must not be used as the first implementation sequence for the desktop application. Preserve those contracts and gates; schedule them under WP-50/WP-60 where their dependencies belong.

## 10. Immediate next action

Start with **WP-00 — Whole-product implementation inventory**.

The inventory must inspect the current source tree, tests, project configuration and Windows/macOS evidence. It must specifically reconcile:
- AF-02's historical report against later Core Domain implementation records;
- the current Project/Map create/list/open/close path;
- PRA-01 implementation state and the PRA-01 audit findings;
- whether an Avalonia viewport and Skia renderer actually exist and run;
- the actual .NET SDK/TFM/Avalonia/Skia package matrix;
- existing authoring UI and which tools have formally adopted write paths;
- current CI and local platform verification gaps.

After WP-00, issue one implementation task for the highest-value M1 gap. Do not start specialist authoring merely because its design package is complete.

---

## 11. Change control

- This roadmap is a planning baseline, not an authorization to change every underlying architecture contract.
- If source inspection reveals a conflict, record it and stop before changing formal domain semantics.
- Accepted ADRs and adopted Core Domain contracts outrank this roadmap.
- Any new capability must have one clear authority and a tested read/write boundary.
- Revisions to this roadmap should preserve completed evidence and explicitly state which milestone or package is being reordered.
