# World View — ME-07 Rendering & Technical Architecture

- Version: 0.1
- Date: 2026-11-21
- Status: Technical workstream scope and integration plan; renderer/library choices and platform performance remain subject to proof
- Product target: C# desktop application, Avalonia UI, Windows/macOS, local-first
- Depends on: ME-01-01 through ME-01-07; ME-05-02 through ME-05-08; ME-06-00 through ME-06-G; Core Domain spatial/persistence contracts; AF-01/AF-02; existing Map Engine/PRA documents
- Authority: Renderer and Map Engine implementation planning only. This document does not replace Core Domain geometry authority, freeze a public renderer API, authorize schema changes or claim performance/platform conformance.

## 1. Purpose and executive disposition

ME-07 turns the existing renderer and Map Engine architecture documents into a bounded, evidence-driven implementation and platform-validation plan.

The current repository already contains substantial production-facing contracts:
- Production Renderer Architecture V0.2;
- Renderer Contract & Scene Model V0.1;
- PRA-01 Presentation Context & Immutable Scene Snapshot Contract V0.1 and Developer Handoff;
- Spatial Presentation & Geometry Interaction contracts;
- Animation Runtime Contract V0.1;
- Map Rendering & Visualization Architecture V0.3.

ME-07 must reconcile and sequence these artifacts rather than create a competing renderer architecture or duplicate the spatial/geometry domain model.

**Working direction:** Avalonia owns the desktop shell and viewport hosting; a backend-neutral retained presentation scene is prepared from authoritative Core Domain inputs; Skia is the leading initial 2D drawing backend behind a replaceable boundary. No renderer library owns formal geometry, topology, Map history, field truth or semantic labels.

The renderer is a deterministic presentation subsystem. For any captured Presentation Context, it prepares one coherent immutable Scene Snapshot; a frame reads one snapshot and never combines independently versioned scene parts.

## 2. Important integration findings and gates

### ME07-F01 — Target framework mismatch

**Severity: P1 — resolve before renderer implementation is integrated**

Production Renderer Architecture V0.2 names C# / .NET 10 / Avalonia 12 / Skia. The AF-02 implementation report records the application repository using .NET SDK 11.0.100 and net11.0 as the target framework. PRA-01 correctly says to use the repository's currently approved target and not upgrade frameworks as part of its work package.

Therefore the production renderer documents must not independently pin .NET 10 while the actual application foundation targets net11.0. Before PRA-02 or renderer package implementation, record one repository-wide approved target framework and compatible Avalonia/Skia package matrix. Do not downgrade or upgrade the application as an incidental renderer task.

### ME07-F02 — Skia is a leading backend, not domain or renderer API authority

Skia may implement the initial 2D backend, but Skia types must not leak into Core Domain, Presentation Context, immutable scene contracts, spatial-service contracts or specialist editor intents. Keep backend resources disposable and reconstructible. The renderer boundary must remain replaceable without changing formal geometry or tool semantics.

### ME07-F03 — Existing spatial contracts are inputs, not duplicated in the renderer

ME-05 and the Spatial Presentation & Geometry Interaction contracts own coordinate, CRS, precision, predicate, measurement, hit-test/snap semantics and geometry preservation rules. ME-07 may implement adapters/indexes/approximations but cannot redefine those semantics. Domain coordinates, projected map coordinates, viewport logical pixels and device pixels remain distinct typed spaces.

### ME07-F04 — Immutable snapshot consistency is the first implementation gate

PRA-01 is the correct first runtime foundation. No Avalonia/Skia drawing integration should bypass its immutable Presentation Context, context fingerprints, complete-scene assembly, latest-request-wins publication, stale-result rejection, atomic reference exchange, per-viewport publication and reader lifetime rules.

### ME07-F05 — Raster/field rendering is not raster authority

Terrain/elevation/land-water source fields remain Core Domain Field Dataset/Revision and immutable Asset concerns under ME-05. The renderer samples and displays field packages; it does not author or persist source truth. Derived calculation artifacts remain separately versioned and fingerprinted. Vector semantic Objects remain independently identifiable and selectable over raster presentation.

### ME07-F06 — V1 is a 2D editing renderer

The current specialist contracts establish a 2D map plane and Building-local 2D interior plans with scalar vertical organization. ME-07 must not silently introduce authoritative 3D solids, BIM, volumetric terrain or 3D editing. 3D visualization may be considered later as a distinct derived presentation capability after product need and data contracts are established.

## 3. Ownership boundaries

| Subsystem | Owns | Must not own |
|---|---|---|
| Core Domain | Formal Object/Component identity, geometry references, Field Dataset/Revision, history, relationships and typed operations | Renderer caches, screen-space geometry or GPU resources |
| Map Engine spatial services | Coordinate transforms, spatial predicates, measurement, indexes, hit-test/snap evaluation and deterministic derived geometry | Formal Object identity, semantic relationship creation or persistence |
| Presentation Coordinator | Presentation Context, source acquisition, scheduling, freshness, complete-scene assembly and publication decision | Domain mutation or renderer-specific drawing |
| Presentation subsystems | Context-stamped geometry, labels, animation poses, decorations, style and raster display data | Independent context/version authority |
| Scene Assembler/Publisher | Completeness/compatibility validation and atomic immutable snapshot publication | Incremental mutation of published snapshots |
| Renderer backend | Consume one immutable snapshot; draw; own disposable backend resources | Domain queries/writes, historical evaluation or semantic editing |
| Avalonia host | Window/view lifecycle, UI thread, input/focus integration and viewport surface | Map domain or presentation truth |

One renderer frame captures one Scene Snapshot reference for its entire draw. Workers never publish directly; only the coordinator/publisher decides whether a result is still current.

## 4. Work packages and sequence

ME-07 uses existing PRA numbering where available. These are bounded implementation/design packages, not independent engines.

| Package | Focus | Baseline / output | Entry condition |
|---|---|---|---|
| ME-07-00 | Workstream scope and integration plan | This document | Existing renderer/PRA documents inventoried |
| PRA-01 | Presentation Context & Immutable Scene Snapshot | Existing contract V0.1 + developer handoff; implementation report required | Approved target framework recorded; immutable input adapters available |
| PRA-02 | Avalonia viewport host, custom draw operation and Skia resource lifetime | New implementation contract and platform spike report | PRA-01 implementation tests pass |
| PRA-03 | Projection adapter, spatial index and geometry interaction integration | Existing spatial-presentation contracts reconciled with ME-05/ME-06; library spike | Typed CRS/precision contracts and PRA-01 scene identity |
| PRA-04 | Text shaping, glyph drawing and label layout | Existing Production Renderer Architecture; detailed contract as needed | Stable scene/style/label inputs and HarfBuzz package matrix |
| PRA-05 | Batching, level of detail, caches and performance | New measurable workload/benchmark contract | PRA-02/03 correctness and representative map fixtures |
| PRA-06 | Animation presentation runtime | Existing Animation Runtime Contract V0.1 | Context fingerprint and historical-time source semantics integrated |
| PRA-07 | Terrain, raster and Field Package visualization | ME-05 Field Package contracts + Map Engine renderer contract | Verified package parser/tiling and Field Revision read path |
| PRA-08 | Decorations, symbols and remaining presentation composition | Existing visualization architecture + scene model | Scene layers and style/symbol resolution contract reconciled |

PRA-01 is the first implementation slice. Do not implement all packages in parallel before the immutable snapshot foundation is proven.

## 5. Technical decision policy

The following are working decisions or constraints, not claims of tested capability:

1. Avalonia remains the desktop UI host.
2. Skia is the leading V1 2D rendering backend, behind a backend-neutral contract.
3. No one-control-per-Object design; render items are visual primitives/batches.
4. Use immutable scene snapshots and disposable backend resources.
5. Keep renderer callbacks and backend resource lifetimes compliant with Avalonia/backend thread-affinity rules.
6. Use explicit typed coordinate transforms; no implicit CRS conversion or raw longitude/latitude Euclidean measurement.
7. Preserve Bézier curves as formal geometry; flatten only into view-dependent derived approximations for indexing/rendering.
8. Keep hit/snap tolerances in logical screen pixels and convert through the active transform.
9. Treat raster fields as sampled presentation inputs; keep source and derived data authority outside the renderer.
10. Do not introduce a general renderer plugin system, GIS framework dependency, distributed cache, event bus or generalized workflow engine in V1.

Library/package selection beyond the current leading direction requires a narrow technical spike with build, runtime, correctness, resource-lifetime and cross-platform evidence.

## 6. Required technical spikes

### Spike S1 — Framework/package alignment
- Verify repository target framework and SDK from the actual solution/build configuration.
- Verify Avalonia version and supported Windows/macOS target matrix.
- Verify Skia binding/package version compatibility and native runtime packaging.
- Record the approved matrix in one decision record; no project-wide upgrade as a side effect.

### Spike S2 — Avalonia/Skia viewport lifecycle
- Host a custom viewport draw surface.
- Verify custom draw operation lifetime and resource disposal.
- Verify UI-thread and render-thread assumptions.
- Exercise resize, DPI change, window close/reopen and device/resource loss.
- Confirm backend resources can be reconstructed from immutable scene data.

### Spike S3 — Snapshot publication and rendering
- Render from one captured immutable snapshot.
- Force out-of-order preparation completion.
- Verify latest-request-wins and no stale publication.
- Verify a failed candidate leaves last-known-good snapshot intact.
- Verify multiple viewports do not share mutable publication state.

### Spike S4 — Spatial interaction integration
- Verify pan/zoom and typed coordinate conversion.
- Verify broad-phase index plus precise narrow-phase geometry hit testing.
- Verify Bézier hit/snap without mutating formal curves.
- Verify logical-pixel tolerance across zoom and DPI.
- Verify query results bind to ME-06-C context tokens and are re-resolved before apply.

### Spike S5 — Text and labels
- Verify HarfBuzz shaping path and glyph drawing contract.
- Verify CJK text, mixed scripts, fallback fonts, RTL where supported, DPI and clipping.
- Verify label layout is derived, deterministic for pinned inputs and does not mutate semantic names/Objects.

### Spike S6 — Raster/Field visualization
- Verify Field Package parsing and tile addressing from ME-05 contracts.
- Verify no-data/coverage/halo behavior and source fingerprint binding.
- Verify viewport sampling and colorization are presentation-only.
- Verify Field Revision changes invalidate dependent scene products in the same Map.

Each spike must record exact machine/OS, SDK/package versions, commands, results, limitations and reproducible fixtures. Do not convert a successful Windows-only result into a Windows/macOS support claim.

## 7. Performance and correctness acceptance

Performance targets must be established from representative workloads, not guessed from generic GIS benchmarks. The benchmark fixture set should include:
- sparse semantic map with many labels;
- dense urban map with high object/component counts;
- high-resolution terrain/raster Field package;
- mixed vector/raster map with selection and transient geometry overlays;
- large but bounded scene with multiple visible layers;
- rapid pan/zoom and repeated pointer hit/snap queries;
- historical-time/animation scene refresh;
- multiple viewports if the product supports them in V1.

Measure at minimum:
- first usable frame and full scene preparation latency;
- frame-time distribution (median, p95, p99), not only average FPS;
- pan/zoom responsiveness and input-to-visible-preview latency;
- snapshot preparation, assembly and publication latency;
- memory footprint and retained snapshot/resource count;
- cancellation and stale-work discard cost;
- raster tile decode/sample/upload latency;
- text shaping/label layout time;
- resource disposal/recreation under resize/device/window lifecycle.

Correctness gates are mandatory before performance tuning:
- no mixed-context frame;
- no stale snapshot publication;
- no formal geometry mutation by renderer;
- no coordinate-space confusion;
- no invalid asset/source read;
- no use-after-dispose or cross-thread resource violation;
- deterministic interaction tie-breaking for pinned inputs;
- no renderer cache treated as formal persistence.

## 8. Cross-platform evidence policy

Windows and macOS are separate acceptance targets. For each platform record:
- clean restore/build;
- application startup and viewport creation;
- pan/zoom, resize, DPI/scale behavior;
- pointer and keyboard interaction;
- renderer resource disposal and window lifecycle;
- text shaping and non-Latin text;
- raster/vector scene correctness;
- representative benchmark results;
- crash/log/diagnostic evidence.

A platform is Unverified until its own evidence exists. No inferred parity from shared C# source or a successful Windows build.

## 9. Implementation acceptance and handoff

ME-07 design planning is complete when:
1. existing renderer documents are mapped to bounded PRA packages;
2. subsystem ownership and immutable snapshot authority are explicit;
3. target-framework mismatch is recorded as a pre-implementation gate;
4. Skia remains a replaceable backend, not a domain dependency;
5. spatial, field, animation and text dependencies are traced to their owning contracts;
6. technical spikes and reproducible acceptance evidence are specified;
7. performance is measured against representative workloads after correctness is established.

An implementation package is complete only when its own source changes, tests, build evidence, platform scope and implementation report are reviewed. Design documents and handoff prompts alone do not prove implementation.

## 10. Immediate next step

Begin with **PRA-01 implementation evidence review and S1 framework/package alignment**. The PRA-01 contract and handoff already exist; do not rewrite them. Verify the actual repository state and developer implementation report first. If PRA-01 has not been implemented, issue the bounded developer task from the existing handoff. Do not start PRA-02 until PRA-01 immutable snapshot tests pass.

## 11. Source baselines reviewed

- Production Renderer Architecture V0.2.
- PRA-01 Presentation Context & Immutable Scene Snapshot Contract V0.1.
- PRA-01 Developer Handoff V0.1.
- Renderer Contract & Scene Model V0.1.
- Spatial Presentation & Geometry Interaction Contract V0.1.
- Spatial Presentation & Editing Interaction Contract V0.1.
- Map Rendering & Visualization Architecture V0.3.
- Animation Runtime Contract V0.1.
- ME-01-05/06/07.
- ME-05-02/03/04/05/06/07/08.
- ME-06-A/B/C/D/E/F/G.
- AF-02 Implementation Report V0.1.

## 12. Change control

ME-07 refines renderer and presentation implementation planning only. Any later change must preserve Core Domain authority, ME-05 coordinate/precision/Asset contracts, ME-06 freshness and interaction semantics, PRA-01 immutable snapshot consistency, and AF-01/AF-02 application lifecycle. Framework/package changes require an explicit repository-level compatibility decision, not an incidental renderer implementation change.
