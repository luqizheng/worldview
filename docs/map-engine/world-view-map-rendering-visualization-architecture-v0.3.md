# World View — Map Rendering & Visualization Architecture
## V0.3 — Internal Renderer, Scene, Spatial Pipeline and Animation Boundaries

- Date: 2026-11-21
- Status: Architecture working baseline; technology choices remain subject to implementation spikes
- Target application: C# + Avalonia, Windows/macOS, single-user and local-first
- Supersedes: V0.2 working draft where this document gives more specific internal architecture
- Scope: Map viewport rendering, scene construction, spatial/coordinate pipeline, interaction/hit testing, cache/invalidation, animation integration, narration integration, export and technical validation

---

## 1. Executive decisions

1. Keep **C# + Avalonia** as the desktop application direction.
2. Use **Skia-based custom 2D rendering** as the leading V1 renderer candidate. Keep the renderer behind a World View-owned interface so the backend can be replaced.
3. Use a **World View-owned retained scene model with batched/immediate drawing**. Do not create one Avalonia Control/Visual per map object.
4. Keep formal map data, geometry/spatial operations, presentation scene, animation pose and pixels as separate concerns.
5. The renderer is a read-only consumer of immutable presentation snapshots. It never writes formal map data.
6. Core Domain remains authoritative for Map, Object, Geometry, History, Branch, Command, Event, State Change and reconstructed World State.
7. Timeline owns the selected Historical Time and playback progression. Animation Runtime evaluates visual poses for that time; it does not create an alternate history authority.
8. Narration is an independent presentation subsystem synchronized by shared timeline/event references. It does not depend on the map renderer.
9. Mapsui remains an optional candidate for standard basemaps, tile sources and GIS interoperability. It is not the core World View map editor or domain model.
10. Do not introduce a dedicated Vulkan/Metal/WGPU rendering stack for V1 unless measured requirements prove the default Avalonia/Skia path insufficient.

These are architecture recommendations, not claims that a technical spike or performance benchmark has already passed.

## 2. Product and domain constraints

World View is a local-first desktop application. The UI and rendering implementation must not dictate domain semantics.

The presentation layer consumes the Core Domain's selected Map/Branch World State at a Historical Time. It must not:
- invent a second Map/Branch/Unit history model;
- treat screen coordinates as authoritative geometry;
- persist animation frames as State Changes;
- mutate formal Object or Geometry data while drawing;
- use cached render output as a source of truth;
- silently change historical meaning when rendering or interpolation algorithms are upgraded.

The existing Core Domain baseline establishes the priority: given a Map/Branch and Historical Time, reconstruct the corresponding World State. The UI must be built on that result rather than changing the domain model for rendering convenience.

The AI Assistant also remains outside the rendering authority boundary. It requests formal operations through internal tools and Domain Services; it does not draw directly into the renderer or write formal database records.

## 3. Logical architecture

```text
Avalonia Desktop Shell
  |
  +-- Map Editor / Layout Workspace UI
  |     |
  |     +-- Map Viewport Control
  |     +-- Input & Interaction Controller
  |     +-- Selection / Editing Tools
  |     +-- Timeline Controls
  |     +-- Narration Panel
  |
  +-- Presentation Coordination
        |
        +-- Domain Read Model Adapter
        |     +-- Selected Map / Branch
        |     +-- Historical World State Snapshot
        |     +-- Object / Geometry / Asset references
        |
        +-- Spatial Presentation Services
        |     +-- CRS / Projection Adapter
        |     +-- Coordinate Transform Pipeline
        |     +-- Viewport Culling
        |     +-- Hit Testing / Spatial Index
        |
        +-- Scene Builder
        |     +-- Layer Ordering
        |     +-- Geometry Render Items
        |     +-- Symbol Render Items
        |     +-- Label Candidates
        |     +-- Decoration Items
        |     +-- Selection / Editing Overlays
        |
        +-- World View Render Scene (immutable snapshot)
        |     |
        |     +-- Static Scene
        |     +-- Dynamic Scene
        |     +-- Overlay Scene
        |     |
        |     +-- Renderer Interface
        |            +-- Skia Backend (V1 candidate)
        |            +-- Future backend(s), only if justified
        |
        +-- Timeline & Animation Runtime
        |     +-- Historical Time
        |     +-- Deterministic Pose Evaluation
        |     +-- Unit Position / Heading / Trail
        |     +-- Event Synchronization
        |
        +-- Narration System
              +-- Character / Event / Scene Narration
              +-- Timeline Synchronization
              +-- Independent reading/playback controls

Core Domain Services
  +-- Map / Object / Geometry
  +-- History / Branch / Command / Event
  +-- Historical World State Reconstruction
  +-- Formal validation and authoring operations

Asset & Project Storage
```

This is a logical decomposition. V1 should remain a modular desktop application, not split these modules into processes or services.

## 4. Responsibility boundaries

### 4.1 Core Domain

Owns:
- formal Map, Branch, Object and Geometry identity;
- formal geometry and attributes;
- Commands, Events, State Changes and history rules;
- deterministic historical reconstruction;
- authoritative movement/track inputs where those are part of domain semantics.

Does not own:
- screen-space layout;
- transient selection handles;
- frame-by-frame drawing state;
- render caches;
- pixel buffers.

### 4.2 Spatial and Geometry Services

Own:
- CRS interpretation and projection transforms;
- spatial predicates and measurements;
- geometry validation and editing calculations;
- snapping and topology assistance;
- coordinate conversion contracts used by presentation.

Must remain independent of Skia and Avalonia.

The geometry engine must support the product's own geometry model, including mixed straight segments and Bézier curves. A GIS library may help with standard spatial operations, but must not silently replace World View's geometry semantics.

### 4.3 Presentation Scene

Owns:
- the visual representation of a particular immutable World State snapshot;
- layer ordering and visibility;
- styles, symbols and labels as presentation data;
- viewport-specific culling;
- derived render bounds;
- transient overlays;
- render cache keys and invalidation metadata.

Does not own formal map meaning or persistence.

### 4.4 Renderer

Owns:
- converting render items to drawing commands;
- batching and drawing paths, fills, strokes, symbols and text;
- clipping and compositing;
- renderer-specific resource management;
- rendering to an interactive viewport or export target.

Does not own:
- domain queries;
- geometry editing rules;
- hit-test semantics;
- timeline progression;
- narration;
- formal object identity.

### 4.5 Interaction Controller

Owns:
- pointer/keyboard gesture interpretation;
- tool state (select, pan, draw, edit node, snap);
- conversion of input positions into map coordinates;
- hit-test requests;
- preview overlays;
- command/proposal creation through application/domain services.

The controller does not write database tables or modify formal geometry directly.

### 4.6 Timeline, Animation and Narration

Timeline owns selected Historical Time and playback progression.

Animation Runtime maps the selected time and domain movement inputs into a visual pose snapshot. It does not own a second Unit history.

Narration owns textual/audio-ready narrative segments and presentation state. It may synchronize with Historical Time, Event identity or scene markers, but remains usable when the map viewport is hidden.

## 5. Coordinate pipeline

Four coordinate spaces must be explicit.

1. **Domain coordinates** — coordinates and CRS attached to authoritative geometry.
2. **Map/projected coordinates** — coordinates used by projection and spatial presentation services.
3. **Viewport coordinates** — logical pixel coordinates within the map viewport.
4. **Device coordinates** — physical pixels after DPI/device scaling.

The normal pipeline is:

```text
Domain Geometry
   -> CRS / Projection Transform
   -> Map Presentation Coordinates
   -> Viewport Transform (pan / zoom / rotation)
   -> Logical Pixel Coordinates
   -> Device Scale
   -> Renderer
```

Reverse interaction pipeline:

```text
Pointer Device Position
   -> Logical Viewport Position
   -> Inverse Viewport Transform
   -> Inverse Projection (where defined)
   -> Domain / Map Coordinate
   -> Spatial Hit Test / Geometry Operation
```

Rules:
- Persist only domain coordinates and their declared CRS.
- Never persist screen coordinates as geometry.
- Keep map calculations in double precision where practical; convert to float/device coordinates only at the rendering boundary.
- Zoom and pan must not mutate geometry.
- Projection-specific interpolation must be explicit. A path may require planar interpolation, geodesic interpolation or a domain-defined movement rule; the renderer must not guess.
- If a projection is undefined or unsuitable for a region, report that limitation instead of drawing misleading spatial measurements.

## 6. Scene model

### 6.1 Retained scene, batched drawing

Use a World View-owned scene model that stores renderable items and derived metadata. It is not a copy of the domain model and is not a second authoritative object graph.

Do not map every Object, Geometry Component, label or vertex to an Avalonia Control. That would create excessive visual-tree and layout overhead and would couple domain object count to UI element count.

Instead:
- build render items from a World State snapshot and presentation configuration;
- group items by layer, render pass, style/material and visibility;
- cull items outside the viewport;
- batch compatible paths and symbols;
- send compact immutable render snapshots to the backend.

### 6.2 Render item families

Initial families:
- raster/tile image;
- polygon fill;
- polyline/stroke;
- Bézier path;
- point/symbol;
- text/label;
- animated Unit pose;
- route/trail;
- selection and editing overlay;
- map decoration (legend, scale bar, north arrow, grid, frame, title, attribution).

A render item references stable domain identities where applicable, but its geometry/style payload is a presentation projection, not the authoritative object record.

### 6.3 Render passes

Recommended pass order:
1. background and raster/terrain;
2. polygon fills and area tints;
3. boundaries, rivers, roads and other strokes;
4. point symbols and static object markers;
5. labels and annotations;
6. animated Unit symbols and trails;
7. selection, handles, snapping guides and transient previews;
8. viewport-only UI adornments.

Map Decorations belong to the layout/presentation system, not to ordinary Map Objects. A Layout Workspace may compose the map viewport with legend, scale bar, north arrow, grid, frame, title and attribution.

## 7. Viewport and interaction

The Map Viewport is one custom high-performance surface embedded in the normal Avalonia UI.

Avalonia remains responsible for:
- application shell and panels;
- menus, dialogs, property inspectors and lists;
- accessibility and normal control interactions;
- layout workspace panels and timeline controls.

The map viewport is responsible for:
- high-density map rendering;
- pointer and keyboard gesture capture;
- pan/zoom/rotation;
- visual selection and editing feedback.

Hit testing should be performed by World View spatial services, not by relying on Avalonia's visual-tree hit testing for every map feature.

Use a spatial index (for example, an R-tree/STRtree-style index where appropriate) over presentation bounds or domain geometry envelopes. Hit testing should:
1. convert the pointer to map coordinates;
2. query a small candidate set from the spatial index;
3. apply precise geometry/symbol hit tests;
4. order results by visual z-order and tool-specific priority;
5. return stable domain references to the interaction controller.

Snapping should query candidate vertices, edges, intersections and configured guides through the spatial/geometry services. It must not be implemented as renderer-only pixel heuristics.

## 8. Layering, caching and invalidation

### 8.1 Separate static and dynamic content

At minimum maintain:
- static scene: terrain, stable boundaries, static fills and most labels;
- edit scene: geometry currently being edited and preview geometry;
- dynamic scene: animated Units, moving markers and trails;
- overlay scene: selection, handles, snapping guides and temporary interaction feedback.

Dynamic updates must not force a rebuild of unrelated static content.

### 8.2 Cache levels

Candidate cache levels:
- source asset cache (raster, icon, font/image resources);
- geometry-to-render-path cache;
- per-layer render cache;
- viewport/tile cache for expensive static content;
- label layout cache;
- symbol atlas/cache.

Cache keys must include all inputs that affect the result, such as:
- source content identity or revision/context token;
- style identity/version;
- projection and transform context;
- scale bucket where scale-dependent simplification or symbol sizing applies;
- relevant viewport/tile identity;
- font and text-layout inputs.

A cache is always disposable and rebuildable. It must never be treated as authoritative data.

### 8.3 Invalidation causes

Examples:
- geometry edit: invalidate affected geometry item and intersecting layer/tile caches;
- style edit: invalidate items using that style;
- layer visibility/order change: invalidate composition/order as required;
- projection change: invalidate projected geometry and spatial presentation caches;
- viewport pan: reuse static content where possible, update visible set;
- zoom change: recull and regenerate scale-dependent labels/symbols; do not invalidate unrelated source assets;
- animation time change: update dynamic pose/trail snapshot only;
- selection change: update overlay only.

Start with simple per-layer and viewport-region invalidation. Add fine-grained dirty rectangles only when profiling shows a measurable benefit.

## 9. Text and label layout

Text placement is a subsystem, not a side effect of drawing every label at its anchor point.

Responsibilities:
- font shaping and measurement;
- multilingual text;
- line wrapping where relevant;
- collision detection and decluttering;
- priority and visibility rules;
- scale-dependent display;
- curved-path labels if required;
- stable placement during small viewport changes where practical.

Separate:
- semantic label content and priority;
- label placement candidates in map coordinates;
- final viewport-specific placement;
- glyph rendering.

Cache shaped text and layout results using stable keys. Avoid allocating new text-layout objects for every frame when the content and style have not changed.

## 10. Animation Runtime integration

### 10.1 Authority

Timeline selects Historical Time. Core Domain reconstructs the authoritative World State at that time. Animation Runtime consumes that state plus applicable movement/track semantics and produces an immutable visual pose snapshot.

Animation Runtime does not:
- create formal Commands or State Changes;
- persist frame positions;
- own Unit identity or history;
- mutate the Core Domain World State;
- accumulate frame delta as the source of truth for a Unit's historical position.

### 10.2 Deterministic time seeking

A visual pose must be computed from the selected time and declared movement inputs, not from the number of frames previously played.

Therefore:
- seeking directly to time T must yield the same pose as playing to T;
- pause/resume must not introduce drift;
- reverse playback must be supported by evaluating an earlier time;
- frame-rate variation must not change historical position;
- multiple Units must be evaluated against the same timeline time.

### 10.3 Movement and interpolation

Movement semantics must be explicit and domain-aware:
- route-following movement;
- position-to-position movement;
- time-phased continuous Command;
- discrete teleport/relocation;
- stationary intervals;
- direction/heading changes;
- optional speed/easing profiles where the domain allows them.

Interpolate in the declared map/world coordinate model, never in screen pixels. Derive heading from the movement tangent or the domain-provided orientation rule. Convert heading to screen orientation only after the map projection and viewport transform are known.

A render-frame interpolation is a visual sampling step. It is not itself a State Change. If a continuous Command produces time-phased domain State Changes under Core Domain semantics, those remain Core Domain outputs; the renderer must not manufacture additional historical transitions.

### 10.4 Trails

Distinguish:
- authoritative route/track geometry;
- completed trail derived for the selected time;
- future/planned route;
- transient visual tail or glow.

Only the first category is formal domain data. Other display forms are derived visualizations.

## 11. Narration integration

Narration is independent from the renderer.

It may reference:
- Event identity;
- Character identity;
- Map/Branch context;
- Historical Time or time interval;
- scene/sequence marker.

Timeline may coordinate narration progression and map playback, but the user must be able to:
- read narration while the map is hidden;
- pause map animation without losing the narration position;
- manually move to previous/next narration segment;
- enable/disable automatic narration advancement;
- inspect an event's text independently of animation.

The renderer must never own narration text, playback state or audio policy.

## 12. Mapsui position

Mapsui is a credible optional component for standard mapping capabilities: tile layers, vector styling, projections and touch handling. Its published project is MIT-licensed and includes Avalonia UI packages, a Skia renderer, tile support and NetTopologySuite integration.

Recommended use:
- prototype standard raster/vector basemap integration;
- evaluate tile source and cache handling;
- compare projection interoperability;
- reuse only isolated components whose semantics fit World View.

Not recommended as the V1 core editor:
- it should not own World View's Map/Object/Geometry model;
- it should not dictate Branch or Historical Time semantics;
- it should not replace custom geometry editing, snapping, topology and editing overlays without a demonstrated fit.

Keep it behind an optional basemap adapter until a spike proves that adopting more of it reduces total complexity.

## 13. Avalonia and Skia integration

Recommended first implementation path:
- Avalonia for the application shell, panels and normal controls;
- one custom MapViewport control;
- Skia drawing through the documented Avalonia custom-rendering integration;
- World View-owned renderer interface and scene snapshots.

Do not assume that a custom Skia draw operation automatically benefits from Avalonia's normal scene-graph caching. Avalonia's documentation explicitly notes that `ICustomDrawOperation` bypasses scene-graph caching and should be used only when Skia-level control is needed.

`CompositionCustomVisualHandler` is a candidate for a later animation/render-thread spike. It is not a prerequisite for the first renderer. Keep its communication boundary message-based and pass immutable render/pose data; do not read Avalonia UI state or call Domain Services from the render callback.

Do not directly own Metal/Vulkan/D3D devices in V1. First test the supported Avalonia backend behavior on Windows and macOS. Introduce a lower-level GPU backend only if measured workloads justify the extra platform and resource-lifetime complexity.

## 14. Export and Layout Workspace

Interactive rendering and export should share the same presentation semantics but not necessarily the same target surface.

Use a renderer abstraction that can target:
- interactive viewport;
- high-resolution raster image;
- PDF/vector-capable output where supported;
- print/layout composition.

Export must not capture a screenshot of the interactive window as its only implementation. It should render the requested map/layout at the target resolution and page size from a stable presentation snapshot.

The Layout Workspace composes Map Decorations independently from ordinary Map Objects. Scale bars must use the declared CRS and measurement capability; never display a plausible-looking but invalid real-world distance.

## 15. Threading and ownership

Recommended ownership:
- Avalonia controls and input state: UI thread only;
- Core Domain reads/reconstruction: application/domain service boundary;
- expensive projection, scene construction, label layout and spatial index rebuilds: background work where safe;
- render scene and animation pose passed to rendering: immutable snapshots;
- renderer resources: owned and disposed by renderer/backend lifecycle;
- render callback: no direct database access, no domain writes, no UI control access.

Avoid sharing mutable geometry collections across UI, background and render threads. Publish a new immutable snapshot when relevant content changes.

Start with a simple background-task model and explicit cancellation/version checks. Do not introduce a general-purpose job scheduler or complex lock-free architecture before profiling requires it.

## 16. V1 technical spikes

The following are validation scenarios, not promised performance targets.

### Spike A — Avalonia/Skia surface
- one custom map viewport;
- pan, zoom and resize;
- raster image, polygon, polyline, Bézier and text;
- verify Windows and macOS behavior;
- inspect DPI scaling, clipping and resource disposal.

### Spike B — Scene model and culling
- large synthetic feature sets at several scales;
- layer visibility and order changes;
- spatial-index hit testing;
- viewport culling;
- geometry/style invalidation;
- compare full redraw with layer/region caching.

### Spike C — Editing feedback
- select and drag an object;
- edit nodes and Bézier handles;
- snap to node, edge and intersection;
- preview geometry without formal mutation;
- submit a formal operation through Domain Services.

### Spike D — Timeline and Unit animation
- multiple Units on independent paths;
- simultaneous playback;
- pause, reverse and arbitrary time seeking;
- pan/zoom during playback;
- deterministic pose comparison at identical Historical Time;
- verify no frame writes to formal history.

### Spike E — Text and export
- multilingual labels and collision handling;
- map decorations and layout;
- high-resolution raster export;
- PDF/print output;
- compare interactive and exported layout semantics.

### Spike F — Platform and lifecycle
- Windows and macOS;
- software/GPU backend behavior as supported by Avalonia;
- window resize, suspend/resume, display DPI changes;
- renderer disposal/recreation;
- memory growth under repeated pan/zoom/playback/export.

Record frame time, CPU/GPU utilization where measurable, memory, allocations, cache hit rate, input latency and visual correctness. Do not freeze arbitrary object-count promises before these tests.

## 17. Recommended implementation order

1. Define renderer-neutral geometry/style/render-item contracts.
2. Implement coordinate transforms and viewport state.
3. Implement static scene builder and basic Skia renderer.
4. Add spatial index and hit testing.
5. Add geometry editing overlays and snapping feedback.
6. Add label placement and caching.
7. Add Timeline-driven deterministic Unit pose evaluation.
8. Add narration synchronization.
9. Add Layout Workspace and export targets.
10. Run cross-platform profiling and decide whether render-thread composition or a lower-level GPU backend is needed.

## 18. Open decisions deliberately deferred

Do not freeze these before spikes:
- final Avalonia custom rendering API and exact package versions;
- whether the viewport uses `Render()`/DrawingContext or `ICustomDrawOperation` for the initial path;
- whether `CompositionCustomVisualHandler` is needed for animation;
- exact SkiaSharp version/backend;
- whether Mapsui is adopted for any production basemap layer;
- exact geometry and spatial-index library combination;
- numeric performance targets and cache granularity;
- any 3D engine or 3D renderer.

## 19. Current recommendation

Proceed with a renderer-neutral World View scene architecture and an Avalonia + Skia 2D spike. Keep Mapsui optional and keep the rendering backend replaceable. Establish coordinate, history and animation authority boundaries before optimizing. Do not introduce 3D or a separately managed GPU stack until real product requirements and measurements justify them.

---

## References

- Avalonia custom rendering: https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering
- Avalonia architecture and rendering pipeline: https://docs.avaloniaui.net/docs/fundamentals/architecture
- Avalonia threading model: https://docs.avaloniaui.net/docs/app-development/threading
- Avalonia native interop / Skia custom drawing example: https://docs.avaloniaui.net/docs/app-development/native-interop
- Mapsui documentation: https://mapsui.com/
- Mapsui repository and license: https://github.com/Mapsui/Mapsui
- SkiaSharp release notes: https://mono.github.io/SkiaSharp/docs/releases/4.152.0.html