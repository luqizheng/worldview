# World View — Spatial Chunk & Level-of-Detail Contract

- Version: 0.1
- Date: 2026-11-27
- Status: Architecture design baseline; implementation parameters and performance thresholds require benchmark evidence
- Revision R1 (2026-11-27): clarified that the Spatial Index is a planned Map Engine service with a scoped interim-query boundary (§4.5); defined the complete deterministic input set of the LOD selector including Previous LOD State and LOD Policy Version (§7.4); scoped the Units/moving-entities display row as a future extension example (§7.3); documented the PRA-04/PRA-05 label responsibility split (§8)
- Scope: Vector presentation partitioning, render-fragment lifecycle, multi-scale visual representation, LOD selection, label density, cache invalidation and validation
- Target: C# desktop application, Avalonia host, replaceable 2D renderer backend, Windows/macOS, single-user/local-first
- Depends on: Map Rendering & Visualization Architecture V0.3; ME-05 coordinate, geometry and Field Package contracts; ME-06 spatial presentation/interaction contracts; ME-07 Rendering & Technical Architecture; PRA-01 Presentation Context & Immutable Scene Snapshot Contract
- Authority: Presentation/renderer architecture only. This contract does not change Core Domain object identity, geometry authority, Field Dataset semantics, history, or formal map data.

## 1. Purpose

This document defines how World View partitions dense vector presentation data into spatial chunks and how the same authoritative map content is represented at different viewing scales.

The product must support maps containing hundreds of thousands of formal map objects without requiring all objects to be prepared or drawn in every frame. The renderer therefore needs bounded viewport queries, local scene preparation, reusable derived fragments and scale-dependent representation.

The design must preserve the following invariant:

> Spatial Chunk and LOD are presentation mechanisms. Neither creates, deletes, merges, splits, re-identifies or changes the semantics of formal map objects.

This is a companion contract to the existing rendering architecture, not a second domain or geometry model.

## 2. Normative terminology

- **Map Space**: the map's declared spatial coordinate space and reference definition.
- **Spatial Partition**: the deterministic scheme used to divide Map Space into addressable regions.
- **Render Chunk**: one region of a Spatial Partition used to organize presentation preparation and static render caches.
- **Source Feature**: a formal Domain Object or Geometry Component that contributes to map presentation.
- **Render Fragment**: a derived, local presentation representation of part of a Source Feature.
- **Spatial Index**: a broad-phase query structure over authoritative or derived geometry bounds. It is a planned Map Engine-provided service, not an existing implementation (see §4.5).
- **LOD**: a presentation policy selecting geometric detail, symbols, visibility and labels for a view scale.
- **Scale Bucket**: a stable range of effective map-to-viewport scale used for cache reuse and transition control.
- **Large Feature**: a feature whose extent or chunk intersection count makes ordinary per-chunk handling inefficient.
- **Presentation Context**: the immutable, versioned set of inputs to which prepared scene products and snapshots are bound.

The following concepts are deliberately distinct:

1. Field Package Tile: an internal storage/processing partition of an immutable Field Revision.
2. Spatial Index Entry: an index record used to find candidate features.
3. Render Chunk: a presentation partition used for local scene preparation and caching.
4. LOD Representation: a scale-specific visual expression of a feature.

They must not share identity or lifecycle merely because all can be described using spatial coordinates.

## 3. Authority and ownership

| Concern | Authority | Rule |
|---|---|---|
| Object and Geometry Component identity | Core Domain | Remains stable and authoritative |
| Formal geometry and topology | Core Domain + Geometry Services | Never replaced by render fragments |
| Map Space and coordinate reference | Map/Core spatial contracts | Chunk addressing is interpreted in an explicit Map Space |
| Spatial predicates and exact hit testing | Spatial/Geometry Services | Renderer does not define semantic hit behavior |
| Spatial index | Map Engine Spatial Services (planned service; not yet implemented) | Derived query structure; rebuildable; Render Chunks own no independent general-purpose spatial index authority (§4.5) |
| Partition scheme and Render Chunk assignment | Presentation Spatial Services | Derived from Map Space and source geometry |
| Render Fragments and LOD representations | Scene Builder / Presentation | Immutable, derived and context-bound |
| Label placement | Label Layout subsystem | Independent of drawing and formal label semantics |
| Pixel rendering and backend resources | Renderer backend | Consumes one immutable Scene Snapshot |
| Field tile storage and decode | ME-05 Field Package / Map Engine | Not owned or redefined by Render Chunk |

The Renderer must not query Domain Services, open Field Packages, perform database access, or mutate spatial indexes during a draw callback.

## 4. Spatial partition contract

### 4.1 V1 partition strategy

V1 uses a **fixed, regular grid in Map Space** as the default Render Chunk partition strategy.

Reasons:
- deterministic addressing and repeatable assignment;
- simple local invalidation after geometry edits;
- stable boundaries independent of object density;
- straightforward debugging and cache accounting;
- bounded implementation complexity for a single-machine application.

A hierarchical partition may be introduced later if measurements demonstrate that a single fixed grid cannot handle the observed density variance or scale range. An adaptive quadtree is not a V1 prerequisite.

The initial implementation uses one partition level. A future hierarchical partition level is a partitioning concern and must never be confused with visual LOD.

### 4.2 Coordinate basis

Chunk boundaries are defined in Map Space, never in viewport logical pixels or device pixels.

- For a Map Space with a stable planar coordinate system, use that declared coordinate basis.
- Where a projection is required, the partition definition must pin the projection definition/version and its valid area.
- Projection or Map Space definition changes invalidate affected partition assignments, projected bounds, fragments and chunk caches.
- Chunk coordinates use double-precision calculations for boundary assignment; conversion to float/device coordinates occurs only at the renderer boundary.
- Geographic wrap/seam behavior is explicit in the Map Space contract. It must not be inferred from tile or chunk edges.

The partition origin, chunk width/height, axis orientation and numeric boundary rules are configuration values of a versioned partition scheme. Their numeric values are intentionally not frozen here; they must be selected by benchmark.

### 4.3 Chunk address

A logical Chunk Address consists of:

```text
MapSpaceId
PartitionSchemeId
PartitionSchemeVersion
PartitionLevel
ChunkX
ChunkY
```

V1 uses PartitionLevel = 0. PartitionLevel is reserved for future hierarchical partitioning; it is not LOD.

A Chunk Address must not include:
- viewport identity or viewport position;
- zoom value or DPI;
- renderer backend or GPU resource identity;
- style revision;
- current selected object;
- cache file path or process-local memory address.

Those values may affect a particular cached presentation product but not the underlying spatial address.

### 4.4 Deterministic assignment and boundaries

Use half-open cell semantics for point-to-chunk assignment:

`[minX, maxX) × [minY, maxY)`

This ensures a point exactly on a shared boundary has one deterministic owning chunk for indexing purposes.

For extended geometries:
- every chunk whose closed bounds intersect the geometry's conservative envelope is a candidate;
- exact intersection/clipping is performed by geometry services;
- a geometry touching a boundary may be referenced by adjacent chunks where required for correct rendering;
- chunk assignment is not object ownership and does not imply that the feature belongs to a particular region.

Boundary calculations must use a documented rounding policy and stable numeric tolerances. No locale-dependent or screen-space rounding is permitted.

### 4.5 Spatial index relationship

The Spatial Index and Render Chunk partition solve different problems.

- Spatial Index: find candidate Source Features intersecting a query envelope.
- Chunk assignment: determine which local presentation regions may contain derived fragments.
- Chunk cache: reuse prepared static presentation products for a region.

The Spatial Index is a follow-up Map Engine service. It is **not** part of the current code baseline, and this contract does not describe it as an existing capability. Render Chunks do not own an independent, general-purpose spatial index authority.

While the Map Engine Spatial Index is not yet available, an implementation package may use a scoped interim candidate query instead, subject to **all** of the following constraints:

- the interim query lives entirely inside the corresponding implementation package;
- it does not become a second, long-term spatial index architecture;
- it preserves an explicit replacement boundary for the future Map Engine Spatial Index: call sites must be swappable to the real service without changing this contract;
- it does not change where precise geometric judgment, object identity or coordinate semantics are decided; those authorities remain with Geometry/Spatial Services and Core Domain.

The interim query stores conservative bounds only and must be rebuildable from source geometry and partition configuration.

The chunk-to-feature membership structure may be a compact derived lookup. It must be rebuildable from source geometry and partition configuration.

The index (or interim query) stores conservative bounds. Precise geometry intersection, hit testing, snapping and topology decisions remain in the Geometry/Spatial Services.

## 5. Render Fragment contract

### 5.1 Source identity

A Render Fragment must retain enough provenance to resolve back to its source:

- Map identity and Presentation Context fingerprint;
- Source Object ID, where applicable;
- Geometry Component ID, where applicable;
- source geometry revision/fingerprint;
- Render Item Kind;
- Layer ID;
- Chunk Address;
- Fragment identity;
- LOD/representation identity;
- style dependency revision where style has already been resolved.

A Fragment ID is deterministic for equal source identity, component, partition address, clipping algorithm version and fragment ordinal/content fingerprint. It must not depend on memory address, collection order that is not contractually stable, or random identifiers.

Fragments are not persisted as formal map objects and do not receive Domain Object IDs.

### 5.2 Fragment generation

- Source geometry remains complete and authoritative.
- Fragment geometry is a derived clipped or locally bounded representation.
- Fragment generation must not mutate source geometry or its control points.
- Fragment results are immutable once published into a Scene Snapshot.
- A fragment cache key includes source geometry revision, Map Space/projection fingerprint, partition scheme version, chunk address and clipping algorithm version.
- Style and LOD are separate dependencies unless the fragment geometry itself is simplified for a particular LOD.

Fragment generation should be lazy or demand-driven for visible chunks. The implementation must avoid eagerly fragmenting every feature in the entire map at startup.

### 5.3 Cross-boundary correctness

The clipping/drawing contract must prevent visible seams and duplicated visual weight:

- polygon fills must meet without gaps or overlaps that create visible slivers;
- shared polygon boundaries must use consistent coordinates and topology;
- polylines must remain visually continuous across chunk boundaries;
- strokes must not be double-painted at seams;
- Bézier source curves remain authoritative; any flattened/clipped curves are derived approximations;
- symbols must be assigned/drawn once according to a deterministic anchor/ownership rule;
- labels must be placed by the global viewport label-layout pass, not independently emitted by every chunk;
- hit-test candidates from adjacent chunks must be deduplicated by source identity before exact testing.

For strokes, the implementation may use overlap/expanded fragment bounds or a centralized seam-aware draw rule. It must prove that the chosen technique does not create doubled alpha, thicker seams or clipped caps/joins.

### 5.4 Large Features

A feature whose extent intersects a large number of chunks must not cause full geometry duplication in every chunk.

V1 supports a Large Feature path:
- retain the feature once in the spatial index, or in the scoped interim candidate query per §4.5 while the Spatial Index is not yet available;
- identify it as a large-feature candidate based on measured extent/intersection cost;
- query it through a dedicated candidate list for visible viewports/chunks;
- clip or derive only the portion needed for the current viewport;
- keep its fragments subject to the same source identity, context and cache rules.

The threshold for Large Feature classification is an implementation parameter determined by benchmark, not a hard-coded product rule.

## 6. Viewport query and chunk preparation

For each Scene preparation request:

1. Capture one immutable Presentation Context.
2. Convert the viewport's visible bounds into the relevant Map Space.
3. Expand the query bounds by a conservative presentation margin for stroke width, symbols, shadows, labels and other offscreen anchors that may still affect visible output.
4. Query the Spatial Index — or, while it is not yet available, the scoped interim candidate query defined in §4.5 — for candidate source features.
5. Resolve candidate features to intersecting Render Chunks.
6. Select the required LOD/Scale Bucket for the captured view.
7. Reuse compatible cached fragments/chunk products.
8. Build missing or invalid products in background work.
9. Assemble all required products against the same Presentation Context.
10. Publish only through the existing coordinator/publisher after freshness and completeness checks.

Pan should update the visible chunk set and reuse compatible cached content. It must not invalidate the entire static scene.

A viewport query must account for features whose geometry or visual footprint intersects the viewport even when their anchor or nominal origin lies outside it.

The Renderer consumes the published immutable snapshot. It must not discover missing chunks or start preparation from the draw callback.

## 7. Level-of-Detail model

### 7.1 Product levels

V1 defines four conceptual presentation levels. These are representation policies, not separate maps, data copies or domain scopes.

| LOD | Conceptual view | Primary purpose |
|---|---|---|
| LOD-0 | World | Overall geography, major regions, coastlines, major rivers and routes |
| LOD-1 | Region | Regional structure, major settlements, principal roads/rivers, terrain character |
| LOD-2 | Settlement | Town/city layout, neighborhoods, local roads, building groups and selected individual landmarks |
| LOD-3 | Local detail | Individual buildings, detailed paths/boundaries, local symbols and fine annotations |

Building interiors are separate Building-local scenes/editors. They are not LOD-4 of the world map and must not be placed into world-map coordinates.

The product may support continuous zoom between these conceptual levels. The active representation changes according to scale policy; the view is not required to snap to four discrete zoom stops.

### 7.2 LOD responsibilities

LOD may change:
- geometry simplification and tessellation detail;
- feature visibility;
- symbol choice and size policy;
- grouping into non-authoritative visual aggregates;
- label eligibility and density;
- raster/field display resolution and derived sampling;
- decoration visibility.

LOD must not:
- change formal Object or Component identity;
- merge formal objects in Core Domain;
- rewrite attributes, relationships or geometry;
- create semantic settlements/regions from visual clusters;
- alter authoritative Field Revision data;
- affect history, Branches or historical reconstruction;
- make hit testing return a synthetic aggregate as though it were a formal object.

A visual aggregate must carry the identities of its represented source features or a queryable membership reference. It is presentation-only and is not selectable as a new formal object unless an explicit interaction resolves to one or more underlying source objects.

### 7.3 Feature representation policy

Feature classes need explicit scale policies. Initial policy guidance:

| Feature class | World | Region | Settlement | Local detail |
|---|---|---|---|---|
| Coastline / major water boundary | Simplified continuous line/fill | More detailed | Detailed | Full permitted geometry |
| Major rivers | Major network and width hierarchy | Main network | Local network | Full geometry |
| Minor streams | Usually hidden or generalized | Selectively shown | Shown by scale/importance | Detailed |
| Roads | Major route hierarchy | Primary/secondary | Local street network | Full editable geometry |
| Region boundaries | Major political/geographic divisions | Regional boundaries | Selected local boundaries | Full geometry where relevant |
| Buildings | Settlement density / area impression | Settlement footprint or landmarks | Building groups + landmarks | Individual buildings |
| Vegetation / terrain | Broad field/biome expression | Regional texture and contours | Local terrain expression | Detailed derived presentation |
| Point features | Major landmarks only | Important POIs | More POIs | Full eligible symbol set |
| Units / moving entities *(extension example, see note)* | Aggregated or hidden by policy | Individual important units or groups | Individual units | Individual units and trails |

These are starting display policies. Exact class thresholds, symbol definitions and visibility ranges belong to the style/scale configuration, not renderer code.

The **Units / moving entities** row is an illustrative display policy for a future dynamic-presentation extension. It is **not** a delivery commitment of the first usable map editor, and it does not justify introducing a dynamic simulation system in V1. If such display is added later, it must reuse the existing Dynamic/Overlay scene scopes (§10.2) rather than introducing a new scene kind.

### 7.4 Scale selection and hysteresis

The LOD selector is a pure, deterministic function of the following inputs:

- **Presentation Context**;
- **View Scale** — the effective map-to-viewport scale derived from the active view transform and logical viewport pixels. Device pixels are never a semantic scale input;
- **Previous LOD State** — the LOD previously selected for this viewport, used for hysteresis;
- **LOD Policy Version**.

Selection rules:

- For identical complete inputs, the LOD selection result must be identical. Selection must not depend on set traversal order, dictionary enumeration order or thread completion order.
- Previous LOD State belongs to viewport presentation state. It is not formal map data and must not be persisted as such.
- A newly created viewport, or a viewport without a valid Previous LOD State, applies an explicit initial-selection rule: select the LOD for the current View Scale directly, without hysteresis.
- Scale thresholds are configurable and versioned. The same Presentation Context and View Scale resolve to the same LOD policy.
- Use hysteresis around LOD transition boundaries to prevent rapid flicker when the user pans/zooms around a threshold. Hysteresis behavior is defined entirely by the inputs above; hysteresis state is viewport presentation state, not formal map data.
- If a viewport jumps across multiple levels, the selector may directly choose the target LOD without preparing every intermediate level.
- When the LOD Policy Version changes, all existing Previous LOD States are invalidated and reinitialized through the initial-selection rule. No stale-state hysteresis may survive a policy version change.

Numeric scale thresholds are deliberately deferred until representative map fixtures and viewport measurements exist.

### 7.5 Geometry simplification

Simplification is a derived presentation operation.

- Preserve the source geometry and its revision.
- Use a documented error metric in map units or logical screen pixels, converted through the active transform.
- Define maximum visual deviation per LOD; do not choose tolerance solely from raw coordinate magnitude.
- Preserve topology for shared boundaries and connected networks where required.
- Avoid independent simplification of adjacent polygon boundaries that creates gaps/overlaps.
- Preserve endpoints, junctions, important bends, semantic anchors and geometry constraints.
- Bézier curves remain formal curves; flattening and simplification are view-dependent derived forms.
- Never simplify formal geometry in place or write simplified output back to Domain storage.

For shared boundaries, generate one consistent generalized boundary representation or use a topology-aware process so adjacent polygons reuse the same result.

### 7.6 LOD transitions

Transitions should prioritize visual stability and correctness over animation complexity.

- A representation switch must not show duplicate labels or duplicate symbols for the same feature.
- If cross-fade is used, the two representations must share source provenance and have a bounded overlap period.
- Avoid cross-fading high-volume geometry when it causes unnecessary overdraw or memory pressure.
- Hysteresis is the default anti-flicker mechanism; animated cross-fade is optional and must be justified by visual testing.
- The previous complete Scene Snapshot may remain visible while a new LOD snapshot is prepared, but a single frame must never mix incompatible Presentation Contexts.
- If the new LOD preparation fails, keep the last known-good snapshot and report diagnostics; do not publish a partial scene.

## 8. Labels and decluttering

Labels are not ordinary per-chunk geometry and must not be laid out independently in each chunk.

The label pipeline is:
1. produce context-stamped label candidates from visible source features;
2. resolve text shaping and measurement using the selected font/text context;
3. calculate candidate placements in viewport-aware coordinates;
4. apply priority, collision and decluttering rules across the viewport;
5. publish final immutable label placements with the same Presentation Context as the scene.

Responsibility split with PRA-04:

- **PRA-04** owns text shaping, font metrics, glyph drawing and the label-layout foundation capabilities.
- **PRA-05** (the implementation package for this contract) consumes those capabilities; it organizes label candidates and collaborates on caching in Chunk/LOD scenarios.
- PRA-05 must not rebuild a full text system inside the chunk/LOD pipeline.
- Final label placement is always decided by the viewport-wide layout pass; no chunk may generate final labels independently.

Rules:
- Semantic label text, language and priority remain separate from placement.
- Label candidate identity is tied to source identity and label role, not its current pixel position.
- Labels may be suppressed at smaller scales without changing semantic data.
- Priority must be deterministic for equal inputs; tie-breaking must not depend on hash-map iteration order.
- A label must not be emitted once per intersecting chunk.
- Curved-path labels use the same source path identity and a derived placement.
- CJK, mixed-script shaping, fallback fonts, right-to-left support where required, DPI and clipping are covered by text subsystem validation.
- Small viewport movements should avoid unnecessary label churn where possible, but stable placement must never override a higher-priority collision or correctness rule.

Label layout cache keys include text, font/shaping context, style, source geometry revision, scale bucket, viewport layout context and relevant collision candidates.

## 9. Raster/Field LOD interaction

Field Package tiles remain governed by ME-05. The Renderer may request only the tiles/samples needed for the viewport and selected display resolution.

- Native Field Revision data remains immutable and is not rewritten for display.
- Renderer mipmaps, resampled display levels, colorized tiles and GPU textures are derived caches.
- Field Tile dimensions and Render Chunk dimensions are independent.
- Raster display resolution is selected from source resolution, effective viewport scale and quality policy.
- No-data, outside-coverage and valid zero/category values remain distinct.
- Tile halos/neighborhoods required for filtering are read according to Field/Calculation contracts, not invented from Render Chunk boundaries.
- A Field Revision fingerprint is part of all dependent raster presentation cache keys.
- Field Revision changes invalidate dependent raster products and any vector/raster composition products that actually depend on them; unrelated vector geometry caches remain reusable.

V1 does not require storing authoritative multiresolution pyramids in the Field Package.

## 10. Cache architecture and invalidation

### 10.1 Cache layers

| Cache | Content | Core dependencies |
|---|---|---|
| Projected Geometry Cache | Source geometry projected into Map Space | Geometry revision, CRS/projection fingerprint, projection algorithm version |
| Simplified Geometry Cache | LOD-specific derived geometry | Projected geometry key, LOD policy version, simplification algorithm version |
| Fragment Cache | Geometry clipped/bounded for a Render Chunk | Simplified/projected geometry key, partition version, Chunk Address, clipping version |
| Chunk Scene Cache | Composed static render items for Layer × Chunk × LOD | Fragment keys, style revision, symbol/material compatibility |
| Raster Display Cache | Colorized/resampled Field display tiles | Field Revision/package fingerprint, tile address, display style and sampling policy |
| Label Layout Cache | Shaped and placed labels | Candidate set, text/font context, style, scale and viewport layout context |
| Symbol/Font Resource Cache | Backend-independent or backend resource assets | Asset identity/version, renderer compatibility |

Caches are disposable, bounded and rebuildable. No cache is a formal source of map truth.

### 10.2 Invalidation matrix

| Change | Required invalidation |
|---|---|
| Add/delete/move/resize feature | Old and new spatial extents; affected chunks/layers; spatial index entries |
| Geometry component edit | Component's old/new bounds and intersecting chunks |
| Style change | Render products using that style; geometry fragments remain reusable if style-independent |
| Layer visibility/order | Scene composition and visible set; source geometry cache remains reusable |
| Projection / Map Space change | Projected geometry, index bounds, chunk assignment, fragments and affected scene products |
| Partition scheme change | Chunk membership, fragments and chunk caches |
| LOD policy change | Simplified geometry, scale-dependent fragments, chunk scene and label layout; viewport Previous LOD States are invalidated and reinitialized (§7.4) |
| Pan | Visible chunk set and viewport-dependent labels; do not clear static source caches |
| Zoom | LOD/scale selection, scale-dependent geometry/symbol/label products; preserve unrelated source caches |
| Unit movement / animation time | Dynamic Scene only |
| Selection / hover | Overlay Scene only |
| Edit preview | Edit Scene and affected preview products only |
| Field Revision change | Dependent raster display and dependent derived composition products |

For geometry edits, invalidate the union of the old and new conservative bounds, expanded by the maximum relevant visual footprint. A move must invalidate both the old and new areas.

Start with per-layer and affected-chunk invalidation. Fine-grained dirty rectangles or GPU render-target caching are later optimizations, not V1 requirements.

### 10.3 Memory budget and eviction

- All caches have explicit byte/item budgets.
- Cache accounting includes retained geometry arrays, render items, raster buffers, glyph/symbol resources and backend allocations where observable.
- Eviction may discard any derived cache entry that can be reconstructed.
- A cache entry currently referenced by an immutable published snapshot must remain alive until the snapshot/reader lifetime contract releases it.
- Resource disposal follows the renderer backend's thread-affinity and lifecycle rules.
- Repeated pan/zoom across a large map must not cause unbounded retention of every visited chunk.
- Cache pressure must not cause mutation of a published snapshot.

The exact memory budget and eviction policy are selected from Windows/macOS benchmark evidence and realistic minimum hardware, not guessed here.

## 11. Snapshot consistency and asynchronous preparation

Every prepared product is bound to the Presentation Context that produced it.

The context includes, as applicable:
- Map/Branch/Historical Time and World State revision/fingerprint;
- Map Space and projection definition/version;
- partition scheme version;
- source geometry revisions/fingerprints;
- LOD policy and scale bucket;
- style/symbol/font revisions;
- Field Revision fingerprints for dependent raster products;
- viewport generation and viewport layout context;
- relevant renderer compatibility version.

Rules:
- One rendered frame captures one immutable Scene Snapshot.
- The snapshot contains a coherent set of static, dynamic, edit and overlay products for its context.
- Scene Builder workers do not publish directly.
- A result completing after its request/context becomes stale is discarded.
- A new snapshot is published atomically only after completeness and compatibility validation.
- A failed or cancelled candidate leaves the previous complete snapshot available.
- Each viewport owns its publication state; immutable derived caches may be shared only when their keys and lifetimes prove compatibility.
- Closing a viewport invalidates its publication generation; late work cannot resurrect it.

Single-user desktop software still needs these rules because background preparation, user input, cancellation and window lifecycle can interleave.

## 12. Performance and benchmark plan

No frame-rate, object-count or latency target is claimed by this design document. Establish targets from measured representative workloads after correctness gates pass.

### 12.1 Required fixtures

Use deterministic synthetic and authored fixtures covering at least:
- 100,000, 300,000 and 500,000 formal vector objects;
- dense urban building/road networks;
- long rivers/roads crossing many chunks;
- large polygons and sparse large features;
- high label density and multilingual labels;
- mixed vector and Field Package raster content;
- multiple LOD transitions during continuous zoom;
- repeated pan across previously unseen and cached regions;
- geometry edits near chunk boundaries;
- animation and selection overlays over a dense static map.

The counts above are benchmark fixture sizes, not a claim that every object is rendered simultaneously.

### 12.2 Parameters to compare

- chunk width/height and grid origin;
- alternative fixed-grid sizes;
- ordinary versus Large Feature handling thresholds;
- fragment generation and clipping strategies;
- LOD scale thresholds and hysteresis bands;
- simplification error tolerances and topology-preservation cost;
- cache budgets and eviction strategies;
- raster display tile/cache interaction.

Field Package tile sizes (128, 256 and 512 cell candidates) are benchmarked under ME-05 and must not be conflated with vector Render Chunk dimensions.

### 12.3 Measurements

Record at minimum:
- visible chunks per viewport;
- candidate source features per viewport;
- exact visible features after geometry culling;
- fragments per source feature and fragment count distribution;
- number of Large Features queried and their preparation cost;
- scene preparation and publication latency (median, p95, p99);
- pan/zoom input-to-visible-update latency;
- LOD switch latency and visual stability;
- label candidate/layout counts and time;
- cache hit/miss/eviction rate and retained bytes;
- allocations and garbage-collection pressure;
- frame-time distribution and overdraw where measurable;
- edit invalidation area and rebuild cost;
- Windows/macOS parity and resource lifecycle behavior.

Correctness gates precede performance optimization:
- no mixed-context frame;
- no stale result publication;
- no formal geometry mutation;
- no cross-chunk seam or duplicated stroke;
- no duplicate label/symbol caused by chunk overlap;
- deterministic LOD and tie-breaking for pinned inputs;
- no stale selection/hit-test identity;
- no unbounded cache growth;
- no use-after-dispose or thread-affinity violation.

## 13. Failure handling and diagnostics

The system must distinguish:
- missing/invalid source geometry;
- unsupported projection or out-of-domain coordinates;
- failed fragment generation;
- failed simplification/topology validation;
- missing style/symbol/font resources;
- stale asynchronous result;
- cache eviction;
- renderer backend resource failure.

A stale result or cache miss is not a domain-data error. It should be discarded/rebuilt without changing formal map state.

Diagnostics should include Map/Context fingerprint, partition scheme version, Chunk Address, source identity/revision, LOD policy version and failure category where available. Avoid logging unbounded per-feature details in normal operation.

## 14. Decisions frozen by this contract

The following are the V1 architecture baseline:

1. Render Chunks are presentation-only; they are not formal map entities or storage tiles.
2. Field Package Tiles, Spatial Index Entries, Render Chunks and LOD representations are separate concepts.
3. V1 starts with a fixed regular grid in Map Space, one partition level.
4. Chunk identity is deterministic and independent of viewport, zoom, DPI, style and renderer backend.
5. Spatial Index performs broad-phase candidate lookup; Geometry Services perform exact spatial operations. The Spatial Index is a planned Map Engine service; §4.5 governs the scoped interim-query boundary until it is available.
6. One formal feature may produce multiple immutable Render Fragments; fragments preserve source identity and revision.
7. Large Features use a bounded special query/preparation path rather than unbounded per-chunk duplication.
8. LOD changes visual representation only and never changes formal identity or semantics.
9. V1 conceptual LOD levels are World, Region, Settlement and Local Detail; Building interiors remain separate scenes.
10. The LOD selector is a deterministic function of Presentation Context, View Scale, Previous LOD State and LOD Policy Version (§7.4); Previous LOD State is viewport presentation state, not formal map data.
11. Geometry simplification is derived, view-dependent and topology-aware where required.
12. Label layout is viewport-wide and independent of chunk drawing.
13. Static, Dynamic, Edit and Overlay scene products have separate invalidation scopes.
14. Every derived product is bound to a Presentation Context; only a complete compatible snapshot may be published.
15. Caches are bounded, disposable and rebuildable.
16. Performance parameters are benchmark-selected; no arbitrary chunk size, FPS target or cache budget is frozen here.

## 15. Deferred implementation parameters

The following must be decided by implementation evidence, not by architecture intuition:

- numeric Chunk width/height and grid origin;
- whether/when hierarchical partitioning is justified;
- Large Feature classification threshold;
- fragment clipping algorithm and seam strategy;
- exact LOD scale thresholds and hysteresis bands;
- per-feature-class visibility thresholds and symbol sets;
- simplification algorithms and numeric tolerances;
- cache byte budgets and eviction policy;
- whether static GPU render-target caching is worthwhile;
- minimum supported hardware and measurable performance acceptance targets.

Any proposed change to the frozen architecture baseline requires an explicit design review and must preserve the authority, identity, context and correctness invariants above.

## 16. Integration and next work

This contract is intended to become the detailed design input for **PRA-05 — Batching, Level of Detail, Caches and Performance** under ME-07.

Recommended implementation order:
1. Implement/verify PRA-01 immutable Presentation Context and Scene Snapshot foundation.
2. Complete framework/package and Avalonia/Skia lifecycle proof required by ME-07.
3. Implement deterministic Map Space partition addressing and chunk membership from source geometry.
4. Implement viewport candidate query and a minimal fixed-grid chunk scene. If the Map Engine Spatial Index is not yet available, the candidate query must follow the scoped interim-query constraints of §4.5 and preserve the replacement boundary for the future service.
5. Prove cross-chunk clipping, seam correctness, source identity and exact hit-test resolution.
6. Implement the four conceptual LOD policies with configurable thresholds.
7. Add topology-aware simplification and viewport-wide label layout.
8. Add bounded cache accounting, eviction and scoped invalidation.
9. Benchmark 100k/300k/500k fixtures on Windows and macOS and freeze numeric parameters in a follow-up decision record.

Do not start with GPU-specific chunk caches, adaptive quadtrees or a general-purpose job scheduler. Prove correctness and measure the simpler design first.

## 17. Source documents

- `docs/map-engine/world-view-map-rendering-visualization-architecture-v0.3.md`
- `docs/map-editor/world-view-me-07-rendering-and-technical-architecture-v0.1.md`
- `docs/map-editor/world-view-me-05-01-field-dataset-authority-and-persistence-contract-v0.5.md`
- `docs/map-editor/world-view-me-05-07-field-payload-package-and-tiling-contract-v0.5.md`
- Existing PRA-01, Spatial Presentation & Geometry Interaction, and Animation Runtime contracts referenced by ME-07.
