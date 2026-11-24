# PRA-01 — M1 Implementation Profile V1

- **Date:** 2026-11-24
- **Status:** Approved M1 implementation profile; applies to WP-30A planning and implementation
- **Repository:** `https://github.com/luqizheng/worldview`
- **Pre-flight baseline:** `28897fc6c96a0e448a377950392f403956af0357`
- **Current documentation baseline when authored:** `a814b7cfa4f554415d286bdf9e7b5cb3942b8f73`
- **Parent contract:** [PRA-01 Presentation Context & Immutable Scene Snapshot Contract V0.1](PRA-01_PRESENTATION_CONTEXT_AND_IMMUTABLE_SCENE_SNAPSHOT_CONTRACT_v0.1.md)
- **Developer handoff:** [PRA-01 Developer Handoff V0.1](PRA-01_DEVELOPER_HANDOFF_v0.1.md)
- **Product sequence:** [Product Implementation Roadmap V1 — WP-30](../map-editor/implementation-roadmap-v1.md)
- **Scope:** M1 Basic Map Application only; this profile does not authorize complete Map Rendering or Map Authoring.

---

## 1. Purpose and authority

This profile resolves the M1-specific implementation choices left open by PRA-01 V0.1 and the WP-30 pre-flight audit.

It is an implementation profile, not a replacement renderer architecture. Apply the documents in this order:

1. Core Domain contracts remain authoritative for Map, Branch, History, Historical Time and any future World State revision.
2. The product roadmap remains authoritative for WP-30/M1 scope and the twelve M1 acceptance items.
3. PRA-01 V0.1 remains authoritative for immutable context/snapshot consistency, complete-scene assembly, stale-result rejection, atomic publication and reader lifetime.
4. **This profile specializes PRA-01 for M1** where the current product has no World State reconstruction read model, no configured projection definition, no style system, no font configuration system and no Map extent/bounds.
5. The developer handoff provides implementation guidance where it does not conflict with the above.

If implementation discovers a conflict with Core Domain authority or a missing authoritative input, stop and report it. Do not invent domain data to satisfy a presentation field.

### 1.1 Product constraints

- Local single-user desktop software.
- Windows and macOS are supported targets.
- No multi-user collaboration or distributed coordination.
- No historical user data exists; this is an active development-stage product.
- Preserve only the in-process concurrency and thread-safety required by the UI, renderer and immutable publication lifecycle.
- Do not introduce a general-purpose scheduler, message broker, distributed cache, event bus or plugin framework.

### 1.2 Approved framework/package baseline

The pre-flight audit verified the following at its source baseline:

| Component | M1 baseline |
|---|---|
| Target framework | `net11.0` |
| Local SDK observed | `11.0.100` |
| CI SDK line | `11.0.x` |
| Avalonia | `12.1.6` |
| Avalonia.Skia | `12.1.6` (transitive at pre-flight) |
| SkiaSharp | `3.119.4` (transitive at pre-flight) |

Do not upgrade or downgrade .NET, SDK, Avalonia or Skia as part of WP-30A/B. Do not change the SDK pinning policy incidentally. If source code directly uses SkiaSharp, declare the dependency explicitly in the owning Desktop project while retaining the already resolved compatible version; do not rely on an accidental transitive compile dependency as the long-term declaration.

The older PRA-01 contract header mentions .NET 10. For this repository, that text is superseded by the verified repository target `net11.0`; it is not an instruction to retarget the application.

---

## 2. Approved rendering host and backend direction

M1 adopts a combined architecture; these are complementary roles, not mutually exclusive alternatives.

### 2.1 Responsibilities

| Element | Responsibility | Must not own |
|---|---|---|
| `MapViewportControl` | Avalonia custom Control; input, focus, size/DPI notifications, viewport interaction state and invalidation requests | Domain state, persistence, scene preparation |
| `ICustomDrawOperation` | Submit a bounded draw operation through Avalonia's rendering pipeline; observe the relevant render-operation lifetime | Map queries, mutable ViewModel access, formal state |
| SkiaSharp | Execute the initial 2D drawing operation | Presentation identity, Domain geometry authority, persistence |
| Presentation layer | Build and publish immutable, renderer-neutral scene snapshots | Avalonia, Skia, database or ViewModel dependencies |

The viewport control is the host and input boundary. The custom draw operation is the rendering-pipeline integration point. Skia performs the actual 2D drawing.

### 2.2 Mandatory constraints

- Do not create one Avalonia Control per Map Object or scene item.
- Do not leak Avalonia or Skia types into Domain, Application presentation contracts or the renderer-neutral Presentation project.
- Do not query SQLite, call Domain services, perform file I/O or mutate formal state from a draw callback.
- A draw operation captures exactly one immutable `SceneSnapshot` reference and uses that reference for its complete draw.
- Backend resources must have explicit ownership, disposal and recreation behavior. They are not stored in the logical immutable scene snapshot.
- Respect Avalonia UI/render thread boundaries. This is required desktop lifecycle correctness, not multi-user concurrency.

The exact Skia lease acquisition and disposal implementation belongs to WP-30B/PRA-02, not WP-30A.

---

## 3. M1 PresentationContext profile

PRA-01's context identity rules remain in force. M1 specializes fields whose authoritative systems do not yet exist by using explicit typed states rather than fabricated identifiers.

### 3.1 Authoritative identity inputs

The M1 context must capture:

- MapId from the real Map workspace being opened.
- BranchId from the resolved Core Domain workspace context (including the explicit root-history/default branch semantics already defined by Domain).
- Typed canonical `HistoricalTime` from Core Domain, preserving its exact day and offset precision.
- RootHistoryId from the resolved Core Domain workspace context.
- A typed World State evaluation state, defined below.
- A typed projection configuration state, defined below.
- Explicit version tokens for the M1 neutral style and font baselines.
- ViewportGeneration from the viewport adapter.
- SceneAssemblerVersion from the implementation's explicit schema/algorithm version.

Do not infer BranchId, HistoricalTime or RootHistoryId from labels, UI text or a second persistence query.

### 3.2 WorldStateRevision: explicit NotEvaluated state

The current product has no authoritative World State reconstruction read model. M1 therefore must not fabricate a WorldStateRevision by concatenating RootHistoryId, HistoricalTime, a timestamp, a random value, a process counter or an arbitrary hash.

Represent the World State identity as a discriminated/typed state with at least:

- `NotEvaluated`: no authoritative World State revision was evaluated for this presentation request.
- `Evaluated(revision identity)`: reserved for a future authoritative read model that supplies a real stable revision identity.

M1 uses `NotEvaluated`.

Requirements:

1. `NotEvaluated` is an explicit semantic state, not null, empty string, integer zero or a fake revision.
2. The canonical context serialization and ContextFingerprint must distinguish `NotEvaluated` from every `Evaluated` value.
3. MapId, BranchId, RootHistoryId and typed HistoricalTime remain independently present in the context. `NotEvaluated` does not erase historical context.
4. M1 may publish only scene content that does not claim to represent reconstructed World State. The M1 blank scene is eligible.
5. Any future scene component that requires evaluated World State must declare that requirement and must not be emitted while the state is `NotEvaluated`.
6. A future real revision must come from an authoritative Core Domain/Application read path. Presentation must not become its authority.

This profile intentionally specializes the generic PRA-01 required field. It does not redefine the meaning of a real World State revision.

### 3.3 ProjectionDefinition: explicit NotConfigured state

M1 has no configured ProjectionDefinition authority, and `MapRecord.SpatialReferenceId` may be null. Pan and Zoom for the blank viewport use a local 2D viewport transform; they do not require a geographic projection.

Represent projection identity as a typed state with at least:

- `NotConfigured`.
- `Configured(definition id, definition version)`, reserved for a future authoritative projection definition.

M1 uses `NotConfigured` unless a real configured definition is supplied by an authoritative read projection.

Requirements:

- Do not substitute a guessed CRS, EPSG code, map projection or default geographic coordinate system.
- `NotConfigured` is distinct from a configured definition in canonical serialization and ContextFingerprint.
- M1's viewport camera transform is a screen-navigation transform, not a geographic projection.
- Projection-dependent scene content and spatial operations remain unavailable until the authoritative projection contract exists.

### 3.4 Style and font configuration revisions

M1 has no user-configurable style or font configuration system. It still needs stable context identity for the fixed neutral rendering baseline.

Use explicit, named, schema-versioned built-in tokens, for example:

- `BuiltinNeutralStyle/v1`
- `BuiltinFontBaseline/v1`

These are presentation algorithm/configuration identities, not Domain revisions. Do not use an ambiguous integer zero or imply that a formal style/font subsystem exists.

If the neutral style or font behavior changes in a way that changes output, increment the corresponding token/version.

### 3.5 ViewportGeneration

The viewport adapter owns a monotonically increasing generation for one viewport instance.

Increment it whenever a viewport-dependent presentation input changes, including logical size, device scale/DPI where relevant, camera transform or visible region. It is process-local presentation state, not a persisted Map revision.

- It must not be generated from wall-clock time or random values.
- It must not be shared globally across independent Map windows/viewports.
- A request captures the generation and all viewport-dependent values as immutable values at request creation.
- Do not pass the mutable `WorkspaceViewState` reference to preparation code.
- Pan/Zoom may update the viewport's camera state, but must not write Map data or change Domain revisions.

### 3.6 ContextFingerprint

Use deterministic canonical serialization and SHA-256 as specified by PRA-01 V0.1, with an explicit serialization schema version.

The canonical input must include every M1 context identity field, including the typed states `NotEvaluated` and `NotConfigured`, RootHistoryId, typed HistoricalTime, the built-in style/font tokens, ViewportGeneration and SceneAssemblerVersion.

It must exclude process ID, thread ID, timestamps, random IDs, object addresses and mutable object references.

Provide test vectors for:
- repeated equivalent contexts;
- `NotEvaluated` versus `Evaluated`;
- `NotConfigured` versus `Configured`;
- each required identity field changing independently;
- culture/time-zone changes;
- canonical HistoricalTime precision.

---

## 4. Application boundary and context acquisition

The existing Domain resolver is the authority for workspace identity. The Presentation layer must not bypass Application to query Infrastructure or SQLite.

### 4.1 Required context mapping change

The pre-flight audit found that Domain `ResolvedWorkspaceContext` already contains RootHistoryId and typed `HistoricalTime`, while Application `ValidatedWorkspaceContext` currently narrows these to MapId, BranchId and a canonical time string.

WP-30A must extend the Application validated context/port mapping to preserve:

- MapId;
- BranchId;
- RootHistoryId;
- typed canonical HistoricalTime.

Keep the existing validated ownership and session boundary. Map/Project cross-ownership checks and Branch lineage validation remain in Domain.

Do not add a Presentation-to-Domain direct dependency merely to recover fields discarded by Application. Do not re-query persistence from the Presentation project.

### 4.2 Mapping and lifetime

- Resolve the context through the existing session-bound Application port and existing atomic Domain snapshot scope.
- Preserve the resolved immutable values through the Application boundary.
- Build a PresentationContext from a captured validated context plus captured presentation configuration/viewport inputs.
- The PresentationContext must not retain a live `ProjectSession`, `MapWorkspaceSession`, `WorkspaceViewState`, ViewModel, database object or mutable Domain entity.
- When a workspace closes, its coordinator/publisher must stop accepting new requests, cancel outstanding preparation and reject late publication.

The Application boundary extension is a required WP-30A deliverable and must have focused tests.

---

## 5. M1 SceneSnapshot content and blank-map semantics

M1 must produce a real, complete, immutable presentation snapshot for the opened Map workspace. It does not need to render actual Map Objects.

### 5.1 Blank scene

The M1 scene is deliberately minimal:

- A neutral background/empty-scene presentation description.
- Stable layer/order metadata sufficient for the renderer to draw the blank scene.
- Stable source/provenance identity tied to the Map/context where applicable.
- No fabricated geometry, extent, bounds, labels, terrain, roads, buildings, caves or other Map content.

A neutral background may be a renderer-owned viewport clear operation if the architecture treats clearing as render-surface behavior; if represented in the scene, it must remain a renderer-neutral immutable description. Choose one representation and test it. Do not create two competing background authorities.

### 5.2 Map bounds and default camera

The current `MapRecord` has no extent/bounds/width/height. Therefore:

- Do not add Map bounds or extent fields/schema for M1.
- Do not invent a frame rectangle or pretend a default rectangle is formal Map geometry.
- Start with a deterministic neutral default camera/transform suitable for an empty viewport.
- The viewport may display an origin/crosshair only if explicitly treated as a UI navigation aid, not as formal Map content. Prefer no origin marker for the initial blank scene.
- Record that formal Map frame/bounds are unavailable from the current read projection.

### 5.3 Immutability

Snapshot payload collections must be privately owned or immutable. An `IReadOnlyList<T>` backed by a retained mutable List is insufficient.

The snapshot must not contain:
- Avalonia controls or DrawingContext;
- Skia canvas, paints, paths, GPU handles or backend resources;
- database handles or mutable Domain entities;
- ViewModels, sessions, event subscriptions or closures over mutable state;
- unowned mutable buffers.

The renderer captures one snapshot reference once per draw operation. A blank snapshot is still subject to the same ownership and publication guarantees as a populated future snapshot.

---

## 6. WP-30A runtime shape: minimal in-process coordination

Implement the PRA-01 correctness invariants without building a general-purpose scheduling framework.

### 6.1 Required components

The implementation may use different internal names, but must provide equivalent behavior for:

- immutable PresentationContext and canonical ContextFingerprint;
- context-stamped immutable result envelope;
- immutable SceneSnapshot and stable scene ordering;
- SceneAssembler with required/explicit-optional input validation;
- one publisher per active viewport, with monotonic snapshot versions and atomic reference exchange;
- one lightweight request coordinator per active viewport;
- explicit request lifecycle and bounded diagnostics;
- shutdown/disposal and late-result rejection.

### 6.2 Coordinator simplification

- One coordinator serializes request identity and publication decisions for its viewport.
- A new request supersedes the previous unpublished request for that same viewport.
- Independent viewport instances have independent request sequences and publishers.
- Use cooperative cancellation to save work, but never rely on cancellation for correctness.
- Before publication, verify the request is still current, the context fingerprint matches, the candidate is complete and the publisher is not disposed.
- The coordinator/publisher is the only publication authority.
- Workers/preparation functions never publish directly.
- Do not add a generic task queue, scheduler abstraction, worker pool, event bus or cross-viewport coordination service.
- Do not introduce parallel subsystem pipelines for M1's single blank-scene output. The contracts should permit future context-stamped outputs, but M1 may prepare the required blank-scene result in one straightforward pipeline.
- Use deterministic asynchronous tests with TaskCompletionSource/barriers where interleaving must be proven. Do not use arbitrary sleeps.

### 6.3 Atomic publication and readers

Retain PRA-01's atomic reference semantics:

- A reader obtains the current immutable snapshot reference once at the beginning of a read/draw operation.
- The reader uses that local reference for the entire operation.
- Publication atomically replaces the reference; no reader sees a partially mutated snapshot.
- SnapshotVersion is assigned by the publisher and strictly increases for successful publications within that publisher lifetime.
- A failed, incomplete, mismatched, superseded or late candidate leaves the previous complete snapshot unchanged.
- Do not put locks, waits or preparation work on the render-reader path.

### 6.4 Last-known-good policy

Within one unchanged Map/Branch/HistoricalTime context, retain the last complete snapshot while a replacement is prepared or fails.

On a semantic context switch to another Map, Branch or HistoricalTime, do not display the previous context as if it belonged to the new one. Clear or replace the whole snapshot atomically according to the Application presentation policy. Never mix old and new scene components.

---

## 7. Explicit M1 scope boundary

### In scope for WP-30A

- Presentation project and test project.
- Context identity and canonical fingerprint.
- Application validated workspace context extension.
- Immutable result envelope and minimal blank-scene snapshot.
- Scene assembly and compatibility validation.
- Per-viewport atomic publisher and lightweight latest-request-wins coordinator.
- Cancellation, close/shutdown/disposal and late-result rejection.
- Bounded diagnostics sufficient to inspect request/publication outcomes.
- Deterministic tests and implementation report.
- Debug and Release build/test integration in the existing solution/CI matrix.

### Out of scope for WP-30A

- Avalonia Control, `ICustomDrawOperation`, Skia drawing or GPU resources.
- Projection algorithms, CRS conversion, spatial indexes, hit testing or snapping.
- Real World State reconstruction/Event Replay.
- Map extent/bounds or new persistence schema.
- Style editor, symbol system, font configuration system or label placement.
- Terrain, raster, vector Object rendering, decorations or animation.
- Map Authoring, geometry editing, selection tools or formal Map mutations.
- Generic scheduling, distributed/multi-user concurrency infrastructure or plugin framework.
- Performance optimization beyond basic correctness and avoiding obvious unbounded retention.

---

## 8. WP-30A required test matrix

Tests belong in a renderer-host-independent `WorldView.Presentation.Tests` project, plus focused Application tests for the validated context mapping. Follow the repository's existing xunit.v3 / Microsoft Testing Platform conventions and ensure the solution-level CI discovers the new project.

| Area | Required proof |
|---|---|
| Context identity | Equivalent contexts have equal fingerprints; every identity field changes the fingerprint; null/typed-state distinctions are preserved |
| Canonicalization | Culture/time-zone independent serialization; HistoricalTime precision preserved; stable SHA-256 test vectors |
| World State state | `NotEvaluated` differs from every evaluated revision; M1 blank scene is allowed; World-State-dependent content is rejected/omitted when not evaluated |
| Projection state | `NotConfigured` differs from configured projection; no implicit CRS/default projection is introduced |
| Immutability | Mutating source builders/collections after construction cannot mutate result or snapshot; no backend/mutable references leak |
| Assembly | Matching inputs assemble; mismatched fingerprints/schema versions and missing required inputs are rejected; explicit optional inputs may be absent |
| Ordering | Scene ordering and stable IDs do not depend on input enumeration or task completion order |
| Publication | Readers see an old or new complete snapshot; each reader captures one reference; versions increase monotonically |
| Freshness | Slow old request cannot replace newer request; wrong-context and superseded candidates are rejected |
| Lifecycle | Cancel, workspace close, shutdown, dispose and late completion cannot publish after closure |
| Viewport isolation | Independent coordinators/publishers do not supersede or overwrite each other |
| Failure behavior | Failed assembly/publication leaves the last complete valid snapshot unchanged |

Do not claim that these tests prove Avalonia/Skia rendering or macOS GUI behavior; those belong to later phases.

---

## 9. WP-30A definition of done

WP-30A is complete only when all of the following are demonstrated:

1. The Presentation and Presentation.Tests projects are integrated into the solution and existing CI build/test flow.
2. The Application validated context preserves typed HistoricalTime and RootHistoryId from the authoritative Domain resolution.
3. M1 context represents World State as `NotEvaluated` and projection as `NotConfigured`, without fabricated revisions or CRS.
4. Canonical fingerprint test vectors pass and include all M1 identity fields.
5. The blank-scene snapshot is immutable by ownership and carries one coherent context identity.
6. Scene assembly rejects incomplete or mixed-context required inputs.
7. Per-viewport publication is atomic, monotonic and latest-request-wins.
8. Cancellation, shutdown, disposal and late-result cases are tested deterministically.
9. The Presentation assembly has no Avalonia, Skia, database/ORM, ViewModel or UI dispatcher dependency.
10. Debug and Release builds/tests pass on the repository's approved `net11.0` target.
11. An implementation report records source files, public/internal APIs, test mapping, exact commands/results, deviations and unresolved issues.
12. Unrelated working-tree content, especially `docs/map-editor/implementation/`, remains untouched.

Passing WP-30A does not complete WP-30 or M1. It authorizes planning/entry into WP-30B only after review.

---

## 10. WP-30B and WP-30C handoff boundaries

### WP-30B — Avalonia viewport and Skia drawing

Entry conditions:

- WP-30A definition of done is reviewed and accepted.
- The Presentation snapshot reader contract is available to the real viewport.
- A narrow Avalonia/Skia runtime proof covers custom draw operation creation, execution, disposal and resource recreation on supported platforms as appropriate.

Deliverables:

- `MapViewportControl` and the `ICustomDrawOperation` integration.
- Skia draw path for the M1 blank scene only.
- Correct UI/render thread boundaries and disposable backend resource ownership.
- One-snapshot-per-draw capture.
- Loading/empty/error presentation states as needed.
- No actual Map Object rendering or authoring tools.

### WP-30C — Viewport navigation and platform verification

Entry conditions:

- WP-30B blank viewport opens and draws correctly.
- Camera and transform behavior is isolated from formal Map data.

Deliverables:

- Pan, Zoom, resize and DPI-aware viewport behavior.
- Explicit, tested map-local/viewport transform and inverse behavior; M1 does not imply geographic projection.
- Verify pan/zoom/resize do not write Map data or alter Domain revisions.
- Windows and macOS GUI evidence for the official roadmap M1 acceptance flow.
- Automated transform, snapshot, lifecycle and platform-appropriate viewport tests.

WP-30B and WP-30C must not be pulled into WP-30A as incidental scope.

---

## 11. Relationship to the official M1 acceptance criteria

The only authoritative M1 acceptance numbering is the twelve-item list in `docs/map-editor/implementation-roadmap-v1.md`, WP-30.

This profile contributes primarily to:

- **#5** — opening the real Map workspace and acquiring validated context;
- **#6** — a real empty viewport is enabled by the complete blank-scene snapshot, but the visible UI is WP-30B;
- **#7** — snapshot/context correctness is foundational; actual Pan/Zoom/Resize behavior is WP-30C;
- **#8/#9/#11** — lifecycle and proof that navigation/rendering do not mutate formal Map data;
- **#12** — snapshot consistency tests in WP-30A, transform and viewport tests in WP-30C.

It does not mark any acceptance item complete. Windows/macOS GUI execution evidence remains separately required.

---

## 12. Change control

- This profile is limited to M1 and WP-30A/B/C. It does not amend the full renderer architecture for M2+.
- A future World State revision must be sourced from an authoritative Core Domain/Application read model.
- A future projection definition must be sourced from an adopted projection contract and read path.
- Changes to canonical context identity, snapshot completeness, publication freshness or reader lifetime require review against PRA-01 V0.1.
- If an implementation needs to weaken an invariant or add a new authority, stop and record an ADR/design change before coding.
- Do not expand this profile into Map Authoring, complete rendering, terrain, labels, spatial operations or 3D visualization.
