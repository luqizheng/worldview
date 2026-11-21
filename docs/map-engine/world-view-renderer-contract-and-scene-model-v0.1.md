# World View — Renderer Contract & Scene Model V0.1

- Project: World View
- Document type: Technical architecture contract
- Version: V0.1
- Date: 2026-11-21
- Status: Recommended implementation baseline
- Depends on:
  - World View Rebuild — Architecture Baseline V1.0
  - World View Map Rendering & Visualization Architecture V0.3
  - World View Core Domain — P2-09-08 Command Descriptor and Deterministic Replay Contract V0.1
- Scope: Presentation scene, renderer-neutral contracts, snapshot lifecycle, rendering backend boundary, batching, invalidation, resource ownership, and threading.
- Non-goals: Defining the Core Domain schema, choosing a final GIS projection library, implementing a complete geometry kernel, freezing public plugin APIs, or selecting a custom GPU API.

---

## 1. Executive decisions

This document converts the V0.3 rendering architecture into an implementable contract.

| Area | V1 decision |
|---|---|
| Formal state authority | Core Domain remains the sole authority for Map, Object, Geometry Components, History, Branch, and Historical World State |
| Presentation input | Renderer consumes immutable presentation snapshots; it never queries or mutates Core Domain |
| Scene ownership | World View owns a retained presentation scene and its indexes/caches |
| Renderer contract | Backend-neutral render commands and resource handles; no Skia types in scene/domain contracts |
| Initial backend | Skia-based 2D backend is the leading V1 implementation |
| Desktop host | Avalonia hosts the application UI and the map viewport |
| Scene granularity | Render items represent visual primitives or batches, not one Avalonia Control per domain object |
| Updates | Replace or incrementally update immutable scene snapshots; never mutate a snapshot while a renderer is consuming it |
| Coordinates | Domain CRS coordinates, projected map coordinates, viewport logical pixels, and device pixels remain distinct |
| GPU resources | Backend-owned, disposable, reconstructible caches; never persisted as formal map data |
| Threading | Domain evaluation and scene preparation may run in background; Avalonia UI integration follows UI-thread rules; backend resource operations obey backend/thread affinity |
| Extensibility | Backend replacement is possible behind the contract, but V1 does not introduce a general-purpose renderer plugin system |

The renderer is a presentation mechanism. It is not a second map model, a GIS authority, a history engine, or an authoring service.

## 2. Architectural position

### 2.1 Pipeline

```text
User / AI Assistant
        |
        v
Core Domain Services
        |
        v
Historical World State
        |
        v
Presentation Projection
        |
        v
Immutable Scene Snapshot
        |
        +----------------------+
        |                      |
        v                      v
Scene Indexes / Caches     Interaction Queries
        |                      |
        v                      |
Renderer-neutral Draw Plan    |
        |                      |
        v                      |
Rendering Backend <------------+
        |
        v
Avalonia Map Viewport
```

The pipeline is intentionally one-way for formal data:

- Core Domain produces the authoritative state.
- Presentation Projection converts that state into view-oriented data.
- Scene construction organizes that data for drawing and interaction.
- Renderer consumes the scene and produces pixels.
- Interaction Controller converts user gestures into semantic requests and calls Domain Services through approved application interfaces.

A selection highlight, hover state, drag preview, or snap guide may exist only in presentation/interaction state. It must not be mistaken for a persisted World Object or State Change.

### 2.2 Three ownership layers

**Layer A — Formal domain ownership**

Owned by Core Domain:
- Map, Branch, Object identity and relationships.
- Geometry Components and their formal coordinates/CRS.
- Historical Time and reconstructed World State.
- Commands, Events and State Changes.
- Domain validation and spatial/business invariants.

**Layer B — Presentation ownership**

Owned by World View Presentation:
- Render item identity and visual classification.
- Layer visibility and visual ordering as currently resolved for the view.
- Projected geometry prepared for display.
- Label candidates and placements.
- Viewport-specific spatial indexes.
- Scene snapshots, render batches and invalidation metadata.
- Selection/hover/preview overlays where appropriate.

Presentation data is derived and disposable. It may be rebuilt from formal state plus view configuration.

**Layer C — Backend resource ownership**

Owned by the rendering backend:
- Skia paths, paints, images, shaders, text blobs and GPU-backed resources.
- Backend-specific caches and surfaces.
- Device/context-bound objects.
- Backend synchronization and disposal.

These resources are never exposed as domain objects and never become persisted truth. If the backend is recreated, they can be rebuilt from presentation data and source assets.

## 3. Contract principles

1. **Immutable consumption.** A renderer may consume a snapshot for the duration of a render operation without observing mutation.
2. **Explicit coordinate space.** Every spatial value has a declared coordinate space; no unlabelled `Point` or `Rect` crosses subsystem boundaries.
3. **No hidden domain reads.** Rendering must not call Core Domain services while painting.
4. **No backend leakage.** Scene and interaction contracts do not expose `SKCanvas`, `SKPath`, `SKPaint`, `SKImage`, or Avalonia drawing objects.
5. **Derived caches are disposable.** Cache loss affects performance, not correctness.
6. **Stable identity.** Render items can be correlated with their source Object/Geometry Component where one exists.
7. **Deterministic scene construction.** Given the same World State, view configuration, projection configuration, and presentation algorithm version, scene construction should produce equivalent scene content and ordering.
8. **Partial failure isolation.** A failed tile, image, label, or individual render item must not corrupt the whole scene.
9. **Explicit invalidation.** Changes identify which derived products are stale; avoid global rebuilds when a bounded update is possible.
10. **No premature GPU framework.** Use Avalonia's supported rendering integration and Skia first; do not directly own Metal/Vulkan/D3D devices in V1.

## 4. Core contracts

The following C#-style declarations are conceptual contracts. They are not final public API signatures and should be refined against the implementation spike.

### 4.1 Identifiers and coordinate wrappers

```csharp
public readonly record struct MapId(Guid Value);
public readonly record struct BranchId(Guid Value);
public readonly record struct ObjectId(Guid Value);
public readonly record struct GeometryComponentId(Guid Value);
public readonly record struct RenderItemId(Guid Value);
public readonly record struct SceneRevision(long Value);

public readonly record struct DomainPoint(double X, double Y, CrsId Crs);
public readonly record struct MapPoint(double X, double Y, MapSpaceId Space);
public readonly record struct ViewportPoint(double X, double Y);
public readonly record struct DevicePoint(double X, double Y);

public readonly record struct DomainEnvelope(
    double MinX, double MinY, double MaxX, double MaxY, CrsId Crs);

public readonly record struct MapEnvelope(
    double MinX, double MinY, double MaxX, double MaxY, MapSpaceId Space);

public readonly record struct ViewportRect(
    double X, double Y, double Width, double Height);
```

Rules:
- Domain coordinates retain the CRS declared by the formal geometry.
- Map coordinates are the output of the active projection/Map Space transform.
- Viewport coordinates use Avalonia logical pixels (DIPs), not physical device pixels.
- Device coordinates are used only at the backend boundary when required.
- Coordinate wrappers must not be implicitly interchangeable.
- A transform must declare source and target spaces and its validity/precision characteristics.

### 4.2 Scene identity and provenance

Every render item must have:
- A stable `RenderItemId` within its owning scene identity.
- A render-item kind.
- Optional source references to `ObjectId` and `GeometryComponentId`.
- A layer/order key.
- A declared geometry coordinate space.
- A bounds/envelope in the same declared space.
- A presentation revision or content fingerprint sufficient for cache invalidation.

Render items created solely for decoration, selection, labels, or interaction feedback may have no domain Object reference. Such items must carry an explicit presentation-only provenance category.

A render item ID is not a domain Object ID. It must never be written back as a formal Object identity.

### 4.3 Immutable scene snapshot

Conceptual shape:

```csharp
public sealed record SceneSnapshot(
    SceneIdentity Identity,
    SceneRevision Revision,
    HistoricalViewContext HistoricalContext,
    ViewConfigurationRevision ViewRevision,
    ProjectionDescriptor Projection,
    ImmutableArray<RenderLayerSnapshot> Layers,
    SceneBounds Bounds,
    SceneBuildDiagnostics Diagnostics);
```

`SceneSnapshot` is immutable after publication. It may reference immutable geometry buffers and immutable asset descriptors. It must not expose mutable collections or backend-owned resources.

`HistoricalViewContext` records the Map/Branch and Historical Time used to produce the scene. It is provenance for the view, not a replacement for Core Domain history.

A scene snapshot may be retained by the viewport while a newer snapshot is being built. Publication is atomic: the viewport sees either the previous complete snapshot or the new complete snapshot, never a half-built scene.

### 4.4 Render layer and item model

A layer snapshot describes presentation grouping and ordering, not a new domain layer authority.

```csharp
public sealed record RenderLayerSnapshot(
    RenderLayerId Id,
    int ZOrder,
    bool IsVisible,
    BlendMode BlendMode,
    ImmutableArray<RenderBatchDescriptor> Batches);
```

A batch descriptor groups compatible render items by:
- primitive family (fill, stroke, marker, image, text, etc.);
- compatible style/material;
- coordinate space;
- clipping and compositing requirements;
- asset/resource dependencies.

Do not batch items whose ordering, blending, clipping, hit-testing identity, or selection behavior requires independent handling.

The initial implementation should support:
- Path fill batches.
- Path stroke batches.
- Marker/symbol batches.
- Raster/image batches.
- Text/label batches.
- Overlay batches.

The batch is an optimization boundary, not a semantic grouping of domain Objects.

## 5. Geometry presentation contract

### 5.1 Formal geometry versus display geometry

Formal Geometry Components remain Core Domain-owned. Presentation may create derived forms:
- projected line segments;
- projected Bézier curves;
- flattened polylines for hit testing;
- simplified geometry for low zoom levels;
- tessellated fill meshes;
- screen-space stroke outlines;
- label anchor candidates.

Derived geometry must retain provenance back to its source component and the transform/algorithm revision used to create it.

Never replace formal geometry with a simplified or flattened representation.

### 5.2 Straight segments and Bézier curves

The formal model supports straight segments and Bézier curves. The presentation layer must preserve curve semantics as long as the target backend can render them efficiently.

Recommended pipeline:
1. Read immutable formal geometry from the World State projection.
2. Transform/control-point project into Map Space using the declared projection pipeline.
3. Where the projection is nonlinear, adaptively subdivide curves in source parameter space until projected error is below a defined tolerance.
4. Generate backend-neutral path segments or immutable vertex buffers.
5. Generate separate approximations for hit testing and spatial indexing when required.

Do not assume that transforming only the Bézier control points produces the exact projection of the original curve under a nonlinear map projection. For nonlinear transforms, adaptive subdivision is required.

### 5.3 Geometry tolerance

Tolerance must be expressed in a named space and purpose:
- projection approximation tolerance;
- screen-space rendering tolerance;
- hit-test tolerance;
- snapping tolerance;
- topology/domain tolerance.

Do not reuse one global epsilon for all purposes.

Screen-space tolerance may depend on zoom and DPI. Domain/topology tolerance must not depend on viewport zoom.

## 6. Coordinate and viewport contract

### 6.1 Coordinate pipeline

```text
Formal Geometry
(Domain CRS)
      |
      v
CRS / Map Space Transform
      |
      v
Map Coordinates
      |
      v
Camera Transform
(pan / zoom / rotation)
      |
      v
Viewport Logical Pixels
      |
      v
DPI / backend scaling
      |
      v
Device Pixels
```

The camera transform is view state. It must never rewrite formal geometry.

### 6.2 Projection descriptor

The projection contract must identify:
- source CRS;
- target Map Space;
- projection/transformation implementation ID and version;
- axis order and units;
- valid area/extent;
- whether the transform is invertible in the active area;
- whether it is conformal/equal-area/distance-preserving, where known;
- stated numerical precision and failure behavior.

A transform may fail or be undefined outside its valid area. The system must report or clip such cases deliberately; it must not silently emit invalid coordinates.

### 6.3 Camera state

Camera state includes:
- center in Map Coordinates;
- zoom or scale parameter;
- rotation;
- viewport logical size;
- DPI scale;
- optional constraints (minimum/maximum zoom, bounds).

The camera is owned by the viewport/presentation layer. Camera changes generally do not require rebuilding formal or projected geometry, but they may invalidate screen-space labels, hit-test structures, level-of-detail choices, and viewport culling.

### 6.4 DPI

All user-facing interaction tolerances are specified in logical pixels unless a specific physical-pixel requirement is documented. The backend performs device scaling. Do not multiply domain coordinates by DPI.

## 7. Scene construction and lifecycle

### 7.1 Build stages

1. Capture a stable World State reference and view configuration revision.
2. Resolve visible Objects and Geometry Components.
3. Convert formal geometry to projected presentation geometry.
4. Compute world/map bounds and render-item metadata.
5. Build or update spatial indexes.
6. Generate level-of-detail representations where justified.
7. Generate label candidates and initial placements.
8. Assemble immutable render layers and batches.
9. Run consistency checks and diagnostics.
10. Publish the complete snapshot atomically.

Scene construction should be cancellable. If the user changes Map, Branch, Historical Time, projection, or other material view configuration during construction, the stale build must not replace the newer requested scene.

### 7.2 Snapshot lifecycle

A snapshot moves through:

```text
Building -> Validated -> Published -> Retained -> Released
```

- Building snapshots are private to the builder.
- Validated snapshots satisfy structural invariants.
- Published snapshots become visible to the viewport atomically.
- Retained snapshots may remain in use by an in-flight render operation.
- Released snapshots are no longer referenced by consumers; managed immutable data can then be collected and backend caches may be evicted.

Use reference-counting or a simpler lease mechanism only if profiling proves ordinary managed lifetime insufficient. Do not introduce a complex custom memory manager preemptively.

### 7.3 Snapshot replacement

The viewport may hold `CurrentSnapshot` and a pending replacement. The old snapshot remains valid until the renderer has finished the operation that references it.

No consumer may mutate an already published snapshot. Incremental updates produce a new snapshot or a new immutable snapshot segment; they do not modify published arrays in place.

### 7.4 Snapshot revision

Scene revision is monotonic within a scene identity. It is not a formal map revision or a Core Domain version.

A revision may change because of:
- Historical Time / Branch state change;
- visible object or geometry changes;
- layer visibility/order/style changes;
- projection change;
- asset change;
- label-placement change;
- view-specific overlays.

Camera-only changes should not force a new complete scene snapshot unless the chosen implementation stores camera-dependent data in that snapshot. Prefer keeping camera state separate.

## 8. Invalidation and incremental updates

### 8.1 Invalidation categories

| Change | Required invalidation |
|---|---|
| Historical World State changed | Affected domain-derived render items, indexes, labels and dependent batches |
| One Geometry Component changed | Its projected geometry, bounds, hit-test representation, affected batch and labels |
| Object attribute/style changed | Affected style/material batch and label content |
| Layer visibility/order changed | Layer composition and culling; geometry buffers can usually be retained |
| Camera pan | Viewport culling and screen-space overlays; usually not projected geometry |
| Camera zoom | Culling, LOD, label placement, screen-space stroke/symbol sizing |
| Camera rotation | View transform, labels and screen-space overlays |
| Viewport resize/DPI change | View transform, device surfaces, screen-space labels and overlays |
| Projection change | All projected geometry, bounds, spatial indexes and label anchors |
| Asset content changed | Dependent image/symbol resources and batches |
| Backend/device reset | Backend resources and surfaces only; scene/domain data remain valid |

### 8.2 V1 update policy

Start with full snapshot rebuilds for major context changes and bounded incremental rebuilds for local edits only after the data flow is stable.

Do not build a generic reactive dependency graph in V1. Use explicit invalidation categories and a small set of dependency records.

### 8.3 Atomic publication

A local update must not publish a partially updated set of dependent products. For example, new projected geometry with an old spatial index can cause hit-test errors. Build the affected set, validate consistency, then publish as one coherent snapshot revision.

## 9. Renderer-neutral backend contract

### 9.1 Interface boundary

Conceptual interface:

```csharp
public interface IMapRenderBackend : IDisposable
{
    BackendCapabilities Capabilities { get; }

    void Render(
        RenderFrame frame,
        IRenderTarget target,
        CancellationToken cancellationToken);

    void InvalidateResources(ResourceInvalidation invalidation);

    void HandleDeviceLost();
}
```

The final interface may be asynchronous or split into frame preparation and execution depending on Avalonia integration. The important rule is that it consumes backend-neutral frame data and owns all backend-specific resources.

### 9.2 Render frame

A render frame contains:
- immutable scene snapshot reference;
- camera/view transform;
- viewport logical bounds;
- DPI scale;
- clipping region;
- transient presentation overlays;
- frame diagnostics/options.

The frame must be self-contained for the duration of rendering. It must not require a live Core Domain query.

### 9.3 Backend capabilities

Capabilities should be descriptive rather than used to fork application logic. Examples:
- path fill and stroke;
- Bézier path support;
- image sampling;
- text shaping/rasterization;
- clipping and blend modes;
- maximum texture/image dimensions;
- GPU acceleration availability;
- device-loss recovery support.

The application should define graceful fallbacks for optional capabilities. V1 should require only the capabilities supported by the chosen Skia path.

### 9.4 Resource handles

Presentation data may reference logical resource keys, such as `ImageAssetKey` or `SymbolDefinitionKey`. The backend maps these to private resources.

Rules:
- A resource key is not a raw pointer or backend object.
- Backend handles cannot be persisted.
- Handles are valid only for the backend/device generation that created them.
- Device loss invalidates affected handles and triggers reconstruction.
- Resource disposal is owned by the backend.
- Shared resources use explicit lifetime management; avoid disposing a resource while an in-flight frame still references it.

### 9.5 Error isolation

A missing or corrupt image should produce a diagnostic and a fallback marker/placeholder, not abort the entire map render. A malformed individual render item should be skipped with diagnostics if it can be safely isolated. Structural scene corruption should fail validation before publication.

## 10. Batching and draw ordering

### 10.1 Batching rules

Batch when items share:
- primitive type;
- compatible paint/style;
- coordinate transform;
- blend and clipping state;
- compatible ordering;
- compatible resource dependencies.

Do not batch across:
- required z-order boundaries;
- different blend modes;
- distinct clipping regions;
- items requiring independent interaction identity if the batch cannot preserve item-level lookup;
- overlays that must remain above or below specific families.

### 10.2 Ordering

Use explicit stable ordering keys, for example:

```text
RenderPass
  -> Layer ZOrder
    -> SubLayer Order
      -> Feature Priority
        -> Stable Tie Breaker
```

Do not rely on dictionary iteration order or nondeterministic parallel build completion to determine visual stacking.

### 10.3 Initial render passes

V1 baseline:
1. Background / map canvas.
2. Raster and basemap imagery.
3. Terrain and area fills.
4. Boundaries and polygon outlines.
5. Rivers, roads, routes and other linework.
6. POI and Unit symbols.
7. Labels and text.
8. Animation-specific visual overlays.
9. Selection, hover, snapping and editing overlays.
10. Screen-space UI adornments.

The final order can be refined for product semantics, but it must be explicit and testable.

## 11. Text and labels

Text layout is a presentation concern, not a Core Domain concern.

The label pipeline should separate:
1. label content and semantic priority;
2. anchor candidate generation;
3. text shaping and measurement;
4. collision/declutter selection;
5. final placement and rendering.

The text engine must support Unicode and complex scripts. The backend may use Skia/HarfBuzz or another suitable shaping path, but label placement should not depend on backend-specific glyph objects.

A label placement cache is keyed by the inputs that affect it, including text/style, anchor geometry, camera scale/rotation, viewport size and font availability.

Labels may be hidden or repositioned as the camera changes. Such changes do not modify formal map data.

## 12. Interaction integration

Renderer and interaction are separate consumers of the same scene data.

Interaction services may query:
- visible render-item bounds;
- projected geometry;
- spatial indexes;
- hit-test approximations;
- item-to-domain provenance;
- current camera transform.

A hit-test result should identify:
- the presentation render item;
- optional source Object and Geometry Component;
- hit part (fill, stroke, marker, label, handle);
- distance/tolerance information;
- coordinate result in a declared space.

The Interaction Controller then resolves the semantic operation and invokes the appropriate Domain Service. The renderer must not directly execute edits.

Hit testing must be refined by exact or sufficiently accurate geometry tests after broad-phase index filtering. A coarse screen-space bounding box is not a final hit result.

## 13. Threading and synchronization

### 13.1 Ownership

| Work | Preferred execution context |
|---|---|
| Avalonia control/layout/input | Avalonia UI thread |
| Core Domain reconstruction | Background task where service contract permits |
| Projection and scene building | Background task |
| Spatial index construction | Background task |
| Label candidate generation | Background task; final viewport-dependent placement may be recomputed as needed |
| Backend rendering | Avalonia-supported render integration; obey backend/thread affinity |
| UI state publication | UI thread |
| Backend resource disposal | Backend-defined owning context |

### 13.2 Thread-safety rules

- Published scene snapshots are immutable and safe for concurrent readers.
- Mutable builders are thread-confined.
- Do not access Avalonia controls or mutable UI view models from a render thread.
- UI-to-render communication uses supported Avalonia mechanisms.
- Render callbacks receive immutable messages/state snapshots rather than reaching into UI objects.
- Cancellation and stale-result checks are required for asynchronous scene builds.

### 13.3 Avalonia integration policy

Use ordinary Avalonia controls for application chrome, panels, toolbars, dialogs, inspectors and property editors.

Use a dedicated custom-rendered viewport for the map canvas. Prefer the simplest supported custom drawing integration that meets performance needs. Use lower-level Skia interop only where necessary and isolate it inside the backend adapter.

If a render-thread composition path is adopted, it must be proven with a small spike. Do not assume the UI thread and render thread share mutable state safely.

## 14. Failure and recovery

- **Scene build failure:** keep the last valid snapshot visible and surface a recoverable diagnostic.
- **Projection failure for one component:** isolate/skip the invalid component where safe; record the affected source identity and reason.
- **Backend resource failure:** use fallback rendering and schedule resource reconstruction.
- **Device loss:** recreate backend resources and render targets; do not rebuild Core Domain history unless scene data itself is stale.
- **Stale build completion:** discard it if its captured context revision no longer matches the latest requested context.
- **Application shutdown:** stop new work, cancel builders, wait for or safely detach in-flight frames, then dispose backend resources on the correct context.

## 15. Diagnostics and observability

V1 should collect lightweight diagnostics:
- scene build duration;
- number of visible and culled render items;
- number of batches by family;
- projection/curve subdivision counts;
- label candidates, accepted labels and collisions;
- spatial index build/update duration;
- frame duration and render duration;
- backend/resource cache hits and misses;
- skipped items and recoverable failures;
- snapshot revision and source context.

Diagnostics are for performance and correctness analysis. They should not become a complex telemetry subsystem or require a network service.

## 16. Verification requirements

### 16.1 Correctness

1. Rendering never mutates formal World State.
2. A scene snapshot can be rendered repeatedly without changing its content.
3. A renderer can operate without calling Core Domain services.
4. Same input context and algorithm versions produce equivalent scene ordering and render-item identity.
5. Domain coordinates cannot be accidentally passed as viewport coordinates through implicit conversions.
6. Projected curves remain within the configured approximation tolerance.
7. A stale scene build cannot replace a newer requested scene.
8. Snapshot replacement never exposes partially updated geometry/index combinations.
9. Backend device loss does not alter scene or domain state.
10. Render item identity does not allocate or impersonate a domain Object ID.

### 16.2 Interaction

1. Broad-phase spatial index returns candidates without false negatives within its declared bounds.
2. Narrow-phase hit test respects logical-pixel tolerance.
3. Hit results preserve source Object/Geometry Component provenance.
4. Selection/hover overlays do not create formal State Changes.
5. Hit testing and rendering use compatible projected geometry revisions.

### 16.3 Performance and resilience

1. A large map can pan/zoom without rebuilding formal geometry.
2. Camera-only changes reuse projected geometry where valid.
3. Local geometry edits invalidate only affected dependent products where incremental update is enabled.
4. A missing image does not stop unrelated map rendering.
5. Cancellation prevents obsolete scene builds from consuming unbounded resources.
6. Repeated viewport resize and DPI changes do not leak backend resources.
7. Switching render backend/device generation invalidates backend resources but preserves scene data.

## 17. V1 implementation sequence

1. Define coordinate wrappers, projection descriptor and camera transform.
2. Build a minimal immutable SceneSnapshot with polygon, line, Bézier and marker render items.
3. Implement a renderer-neutral render frame and a single Skia backend.
4. Host the backend in a dedicated Avalonia map viewport.
5. Add deterministic ordering and basic batching.
6. Add viewport culling and a simple spatial index.
7. Add hit testing with broad-phase and narrow-phase stages.
8. Add snapshot replacement, cancellation and stale-build protection.
9. Add labels and Unicode text shaping.
10. Add backend resource lifecycle and device-loss tests.
11. Benchmark representative map sizes and zoom levels before introducing more complex incremental update or GPU-specific work.

## 18. Explicitly deferred decisions

The following are not frozen by this contract:
- exact Avalonia custom-rendering extension point (`Control.Render`, `ICustomDrawOperation`, or composition custom visual);
- exact SkiaSharp package version and GPU path;
- exact CRS/projection library;
- exact spatial-index implementation;
- exact geometry flattening and tessellation algorithms;
- exact label placement algorithm and font fallback policy;
- whether render batches are CPU command arrays, vertex buffers, or a hybrid;
- whether scene snapshot segments need reference counting;
- exact thresholds for full rebuild versus incremental update;
- custom GPU APIs or compute acceleration.

These decisions require a focused implementation spike and measurements, not speculative architecture.

## 19. Architecture risks and controls

| Risk | Control |
|---|---|
| Backend types leak into the application | Keep backend adapter private; enforce assembly/project dependency direction |
| Scene becomes a second domain model | Require provenance and rebuildability; no formal edits from scene |
| Excessive full-scene rebuilds | Measure first; add explicit invalidation and bounded incremental updates |
| Batching breaks ordering or hit testing | Stable order keys; preserve item-level provenance and narrow-phase queries |
| Nonlinear projection distorts curves | Adaptive subdivision in source parameter space |
| UI/render thread data races | Immutable snapshots and supported message passing |
| GPU resource lifetime bugs | Backend-owned handles, generation IDs, explicit disposal |
| Premature abstraction slows V1 | One backend, minimal capabilities, no plugin marketplace |
| Cache becomes authoritative | Every cache has a deterministic rebuild path and explicit invalidation |

## 20. Final recommendation

Adopt this document as the Renderer/Scene implementation contract for V1.

The next design artifact should be **Spatial Presentation & Geometry Interaction Contract V0.1**, which will specify projection behavior, spatial indexing, exact hit testing, snapping, Bézier approximation tolerances, and the relationship between domain/topology precision and screen-space interaction tolerance.

Only after those contracts are consistent should implementation begin with a narrow technical spike. The spike must validate Avalonia hosting, Skia drawing, scene snapshot replacement, pan/zoom, DPI behavior, and resource disposal on Windows and macOS. No performance claim should be treated as proven until measured on both platforms.

---

## References

- World View Rebuild — Architecture Baseline V1.0 (2026-11-18).
- World View Map Rendering & Visualization Architecture V0.3 (2026-11-21).
- World View Core Domain — P2-09-08 Command Descriptor and Deterministic Replay Contract V0.1 (2026-11-21).
- Avalonia documentation: Custom Rendering, Architecture, Threading, and Native Interop.
- SkiaSharp documentation and release notes.
- Mapsui documentation and repository, as an optional GIS/basemap integration reference only.