# PRODUCT-01 — V1 Product Scope & End-to-End Acceptance

- Version: 0.1
- Date: 2026-11-27
- Status: Product scope baseline — user approved
- Repository: https://github.com/luqizheng/worldview
- Baseline: master `89fb266516c9f8c3efe40ca7658fb5a6eafb9c89` (PR #44)
- Target: single-user, local-first Windows/macOS desktop application
- Product stack: C# / .NET / Avalonia; renderer backend remains behind the Map Engine/Presentation boundary
- Authority: product-level V1 scope and acceptance; domain-specific contracts remain authoritative for formal data semantics

---

## 1. Product decision

**V1 is a basic manual Map Editor, not merely a map-management/viewer shell.**

Delivery is staged through two capability levels:

- **M1 — Basic Map Application:** project and map lifecycle, map workspace, viewport presentation, navigation, save/close/reopen.
- **V1 — Basic Map Editor:** all M1 capabilities plus a minimum complete manual authoring loop: create, select, inspect, edit, delete, save, close and reopen supported map content.

M1 is an internal delivery gate and usable foundation. It is not a substitute for the V1 product outcome. V1 must demonstrate real user-authored content persisted through authoritative application/domain services; a blank viewport with placeholder controls does not satisfy V1.

The user explicitly approved this two-level scope on 2026-11-27.

## 2. Product promise

A creator can use World View without an AI service, external Agent, MCP, cloud account or network connection to:

1. create and manage a local project;
2. create and manage a map in that project;
3. open the map in its own Map Workspace;
4. navigate and inspect a real map canvas;
5. create and manipulate a bounded set of supported basic map elements manually;
6. organize and inspect that content;
7. save, close and reopen the project and recover the same formal map state.

The product is local-first and single-user. Do not introduce server-side multi-tenancy, collaboration, distributed execution or generalized concurrency infrastructure. Runtime asynchronous operations still require correct cancellation, UI-thread affinity, stale-result rejection and deterministic resource disposal.

## 3. M1 — Basic Map Application scope

### Included

- Application startup and clean exit on Windows and macOS.
- Create a local project package; validate and open an existing project package.
- Project list / project workspace sufficient to create, open and close projects.
- Create, list, rename where supported, and open maps through formal application services.
- One Map Workspace window per open map session; repeated open activates the existing window.
- Multiple different maps may be open at once.
- A real viewport host with truthful empty, loading, error and ready states.
- Basic pan/zoom and view reset, subject to viewport interaction contracts.
- Map identity and active project/map context are visible and readable.
- Explicit save/close behavior and project/map round-trip persistence.
- Diagnostics for failed open/save/close and retry where the owning lifecycle contract permits it.
- Windows and macOS build, automated test and manual GUI acceptance evidence.

### Excluded from M1

- Editing map content.
- Professional terrain, settlement, building or interior toolsets.
- AI-assisted initialization or generation.
- Advanced renderer optimization, large-map LOD/chunking and high-density benchmarks.

M1 is complete only when the project/map lifecycle and a genuine viewport work end-to-end on both target platforms. A renderer that only clears the background is not a completed map presentation.

## 4. V1 — Basic Map Editor scope

V1 includes all M1 capabilities and the following minimum manual-authoring set.

### 4.1 Basic content authoring

Support only geometry/content types whose formal domain contracts and application operations are explicitly implemented and verified. The initial set should be the smallest useful subset, expected to include:

- point-like content;
- line-like content;
- area/polygon-like content;
- a bounded set of names and simple descriptive properties.

The exact mapping to formal Object Types, Geometry roles, coordinate spaces and Commands is an ARCH-01 / FOUNDATION-01 decision. UI must not invent generic object identities or persist geometry directly. If a type lacks a formal operation, it is not silently simulated in the UI; record it as a scope blocker or defer it.

Required interactions for each supported type:

- create using a direct pointer/tool interaction;
- show a transient preview before formal application;
- commit or cancel the proposed change;
- select and deselect;
- inspect identity and supported properties;
- edit supported properties and geometry;
- delete through an explicit formal operation;
- receive clear validation feedback for rejected input.

### 4.2 Selection and editing

- Deterministic hit testing for supported geometry.
- Clear selected/hovered/preview visual states.
- Basic geometry manipulation appropriate to the supported type (e.g. move point, edit line/polygon vertices) only where formal geometry operations support it.
- Invalid or out-of-bounds operations are rejected without corrupting the prior valid state.
- Selection is a transient editor state, not formal domain data.

### 4.3 Layers and organization

- A minimal layer/content navigator for supported map content.
- Show/hide content layers and deterministic ordering where the presentation contract supports it.
- Distinguish layer visibility/order (presentation state) from formal object existence and semantic relationships.
- Do not build a generic GIS layer-management framework in V1.

### 4.4 History and persistence

- Formal edits flow through Application Services and authoritative Core Domain Commands; UI does not write persistence records.
- Each accepted edit is atomic and has deterministic history semantics.
- Provide basic Undo/Redo for the supported editing operations if the Core Domain command/history contract supports it. Otherwise, ARCH-01 must explicitly identify the missing formal capability and FOUNDATION-01 must close it before V1 acceptance; do not implement UI-only fake undo.
- Save, close and reopen preserves the accepted formal content and its identities.
- Cancelled previews and rejected edits create no formal change.
- Interrupted or failed writes leave the last valid state recoverable; retries do not duplicate an edit.

## 5. Explicit V1 non-goals

The following are not required for V1 acceptance and must not expand the initial vertical slice:

- professional Terrain & Natural Geography workflows: elevation sculpting, hydrology, climate, ecology and derived scientific analysis;
- full Settlements & Urban Geography workflows and road-network analysis;
- full Buildings & Interior Spaces workflows, floors, rooms, caves and vertical circulation;
- AI chat, AI generation, guided initialization and external Agent integration;
- multi-user collaboration, cloud sync, server-side services and distributed rendering;
- generic GIS/CAD/BIM feature parity;
- complete topology editing, advanced snapping, complex geometry repair and exhaustive spatial analysis;
- production-scale Spatial Chunk/LOD/batching/cache optimization before correctness and representative content gates;
- animation, advanced timeline authoring and Field Package raster authoring;
- print layout, cartographic composition and advanced export.

These capabilities may be designed independently, but their existence in older design documents does not make them V1 commitments.

## 6. End-to-end acceptance scenarios

Acceptance is based on observable user outcomes and persisted formal state, not the existence of APIs, placeholder controls or design documents.

### E2E-01 — Create and reopen a project

1. Start the desktop application on a clean user-data location.
2. Create a project with a readable name and choose a local path.
3. Close the project and application.
4. Restart and open the same package.
5. Verify project identity, metadata and map list are intact.

**Pass:** no manual database editing; invalid package paths/formats produce actionable errors; Windows and macOS both pass.

### E2E-02 — Create and open a map

1. In an open project, create a map with a readable name.
2. Open it from the project workspace.
3. Verify one independent Map Workspace appears with correct project/map identity.
4. Open the same map again and verify the existing workspace is activated.
5. Open a second map and verify it has an independent workspace.

**Pass:** no duplicate session/window for the same map; different maps remain isolated; closing a map workspace does not close the project.

### E2E-03 — Navigate a real map viewport

1. Open a newly created empty map.
2. Verify the canvas renders its defined background and map extent/state, not fabricated sample content.
3. Pan, zoom in/out and reset the view.
4. Verify pointer-to-map coordinate behavior is stable and viewport transforms are correct.
5. Resize and minimize/restore the window.

**Pass:** interaction is responsive and consistent; no stale asynchronous result overwrites a newer view; Windows/macOS GUI evidence is recorded.

### E2E-04 — Create and inspect basic content

1. Choose one supported basic geometry/content tool.
2. Draw a point, line or area using the supported authoring gesture.
3. Inspect the transient preview and affected area before committing.
4. Commit the edit.
5. Select the resulting content and inspect its formal identity and supported properties.

**Pass:** formal content is created only through the authoritative application/domain path; preview/cancel does not persist; selected content corresponds to the committed identity.

### E2E-05 — Edit and delete content

1. Select existing supported content.
2. Change a supported property and/or geometry.
3. Verify the canvas and inspector reflect the accepted result.
4. Delete the content through the explicit delete action.
5. Verify it is no longer present in the current formal read model.

**Pass:** each operation is validated and atomic; rejected changes preserve the previous valid state; no direct UI persistence exists.

### E2E-06 — Organize content

1. Create content in at least two supported layers/groups.
2. Hide and show a layer.
3. Change ordering if ordering is part of the accepted presentation contract.
4. Verify visibility/order changes do not delete, duplicate or alter formal objects.

**Pass:** presentation state and formal domain state remain distinct and consistent.

### E2E-07 — Save, close and reopen edited map

1. Create and edit several supported objects.
2. Save and close the map workspace and project.
3. Restart the application and reopen the project/map.
4. Verify geometry, properties, identities and supported organization state.
5. Select an object and confirm its identity is stable.

**Pass:** persisted formal state round-trips; no manual repair or data reconstruction is needed.

### E2E-08 — Cancel, reject and recover

1. Start a geometry gesture and cancel before commit.
2. Attempt an invalid geometry/property operation.
3. Inject or simulate a persistence failure during a formal write.
4. Retry or reopen according to the owning recovery contract.
5. Verify the last valid formal state remains intact.

**Pass:** cancellation/rejection produces no formal mutation; failed writes do not leave partial state or duplicate changes; diagnostics are actionable.

### E2E-09 — Platform and lifecycle verification

Run E2E-01 through E2E-08 on Windows and macOS. Additionally:

- open multiple different map windows;
- repeatedly open/close a map workspace;
- close a project with multiple map workspaces;
- test window focus/minimize/restore and native file dialogs;
- verify no resource leaks or stale UI/session registrations after close.

**Pass:** all platform-specific lifecycle and file-system behavior is documented; automated tests and GUI evidence agree.

## 7. Cross-cutting acceptance requirements

- **Truthful UI:** no fake map data, fake editing controls or placeholder success states.
- **Authority:** all formal writes use Application/Domain Services; presentation and editor state never become formal truth.
- **Determinism:** hit-test and command outcomes are stable for the same input/state.
- **Atomicity:** formal edits either complete fully or leave the previous valid state.
- **Recovery:** save/open/close failures are visible and retryable where safe.
- **Lifecycle:** stale/cancelled work cannot publish after its owning session closes or context changes.
- **Platform parity:** Windows and macOS both support the accepted core flows.
- **Offline operation:** all M1 and V1 manual workflows function without network or AI services.
- **Accessibility baseline:** keyboard access for essential commands, visible focus, readable status/error text, high-DPI-safe layout and non-color-only selection/error cues.
- **Test evidence:** domain/application tests, persistence integration tests, UI/ViewModel tests and manual GUI runs are recorded per scenario.

## 8. Delivery gates and dependency order

This is a sequential programme; do not run work packages in parallel.

1. **PRODUCT-01 (this document):** product scope and user outcomes approved.
2. **ARCH-01:** map every acceptance flow to owning modules, contracts and formal operations; resolve ownership conflicts and identify missing domain commands.
3. **FOUNDATION-01:** verify/fix only the technical foundation required for the first vertical slice; establish reproducible Windows/macOS build/test baseline and persistence round-trip.
4. **M1 vertical slice:** project/map lifecycle + genuine viewport + navigation + round-trip.
5. **V1 authoring contract:** choose the smallest supported point/line/area content subset and define create/select/edit/delete/history semantics against formal domain types.
6. **V1 authoring vertical slice:** one content type end-to-end, then extend to the remaining accepted basic types.
7. **V1 acceptance:** execute all applicable E2E scenarios on Windows and macOS and close blocking defects.

Do not start broad PRA-05 optimization as a substitute for basic viewport correctness and content rendering. Follow the PRA-02 → map extent/scene contracts → minimal geometry projection/drawing → PRA-03 spatial correctness → PRA-04 label foundation → PRA-05 performance sequence already recorded in the post-PR-41 baseline.

## 9. Open decisions for ARCH-01 / FOUNDATION-01

These are implementation/architecture questions, not reasons to reopen the approved product boundary:

1. Which existing formal Object Types and geometry roles provide the smallest point/line/area authoring subset?
2. Which create/update/delete Commands and history operations already exist in source, and which are genuinely missing?
3. What is the smallest correct map extent/coordinate-space/geometry contract for a new map?
4. What minimum renderer path can project and draw persisted geometry without coupling formal domain semantics to Avalonia/Skia?
5. What is the minimum layer/content organization contract already supported by formal data and presentation state?
6. What exact save semantics are owned by Core Domain, package persistence and Asset Store, and what recovery evidence exists?
7. Which existing UI, map workspace and renderer contracts are accepted/frozen versus proposals requiring reconciliation?

ARCH-01 must answer these by inspecting the current source and accepted contracts. Do not infer implementation from design-document presence.

## 10. Definition of done

PRODUCT-01 is complete when:

- the M1/V1 boundary and non-goals are approved;
- the E2E acceptance scenarios are testable and unambiguous;
- each scenario has an identified owning architecture boundary or an explicit ARCH-01 question;
- the dependency order and acceptance evidence are recorded;
- `plan.md` points to this product baseline and no longer describes Phase 0 as pending PR #44.

**Approval:** user approved the M1 → V1 two-level product scope on 2026-11-27. Detailed implementation contracts remain subject to ARCH-01 and FOUNDATION-01.
