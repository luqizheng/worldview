# World View — Spatial Presentation & Geometry Interaction Contract V0.1

- Date: 2026-11-21
- Status: Recommended V1 implementation contract; algorithm/package choices remain subject to technical spikes
- Scope: map-coordinate transformation, viewport projection, spatial indexing, hit testing, snapping, geometry interaction and precision policy
- Depends on: World View Core Domain architecture baseline; Geometry Component model; Renderer Contract & Scene Model V0.1; Core Domain P2-09-08 deterministic replay contract
- Audience: application, spatial/geometry, interaction, renderer and test implementation

---

## 1. Executive decisions

1. **Core Domain remains the authority for formal geometry and topology.** Presentation services may calculate bounds, screen-space approximations, hit candidates and snap suggestions, but may not commit or silently normalize formal geometry.
2. **Use a typed coordinate pipeline.** Domain coordinates, map/projected coordinates, viewport logical pixels and device pixels are distinct types and must not be mixed implicitly.
3. **V1 uses a 2D map plane for editing.** A map's spatial reference is explicit. It may be geographic, projected, or a declared local/cartesian coordinate system; do not assume every fictional world is Earth longitude/latitude.
4. **Projection is a replaceable service contract.** It owns forward/inverse transforms, validity domains, units, wrap behavior and scale/measurement capabilities. Renderer and interaction tools do not implement projections independently.
5. **Use a two-stage spatial query.** A conservative broad-phase spatial index finds candidates; a precise narrow-phase evaluator performs geometry-specific hit testing and nearest-point calculations.
6. **Bézier curves remain first-class geometry.** Flattening is a view-dependent approximation for indexing and screen-space interaction, never a rewrite of the authoritative curve.
7. **Interaction tolerance is specified in logical screen pixels.** It remains usable across zoom levels and DPI settings; convert tolerance through the current transform rather than storing it in domain units.
8. **Snapping produces a typed proposal, not a topology mutation.** The user/tool may accept a snap candidate, but Core Domain validates and commits the resulting operation.
9. **Determinism is explicit.** Candidate ranking and tie-breaking must be stable for the same scene snapshot, viewport transform, pointer position and tool configuration.
10. **V1 avoids a general-purpose GIS/topology framework in the editor core.** Implement the minimum spatial primitives required by World View, and use external libraries only behind adapters where they materially reduce risk.

## 2. Product and domain constraints

### 2.1 Formal geometry is not presentation geometry

The Core Domain owns:
- Geometry Component identity and membership;
- authoritative line and Bézier control-point data;
- geometry operations and their semantic rules;
- shared boundaries/topology where the domain model requires them;
- validation of geometry invariants;
- Commands and State Changes that author geometry history.

Spatial Presentation owns derived information only:
- projected coordinates for the current map view;
- screen-space path approximations;
- conservative bounds and spatial-index entries;
- hit-test and snap candidates;
- temporary drag previews;
- visual handles, guides and interaction overlays.

No presentation cache or spatial index may become a second source of truth.

### 2.2 Preserve Object / Geometry separation

One semantic Object may contain multiple Geometry Components. A geometry component can be selected or edited without changing Object identity. Splitting or joining geometry does not automatically create, delete, merge or split semantic Objects. Object Operations remain separate Core Domain operations.

### 2.3 Historical state input

The spatial pipeline consumes a resolved, immutable presentation projection for the selected Map/Branch and Historical Time. It must not query history tables or independently reconstruct Branch inheritance. A change of Historical Time or Branch causes the caller to request a new authoritative world-state projection and then rebuild/update presentation data.

## 3. Coordinate reference contract

### 3.1 Coordinate spaces

| Space | Meaning | Owner / use |
|---|---|---|
| Domain Coordinates | Formal Geometry Component coordinates in the map's declared spatial reference | Core Domain |
| Map Coordinates | Coordinates after the map's selected projection/working transform | Spatial Presentation |
| Viewport Coordinates | Logical pixels relative to the map viewport | Interaction and layout |
| Device Coordinates | Physical pixels after DPI scaling | Renderer backend |

The names above are semantic types, not interchangeable `double x, double y` pairs. Public APIs should use distinct structs or equivalent type wrappers such as `DomainPoint`, `MapPoint`, `ViewportPoint` and `DevicePoint`.

A transform must declare its input and output spaces. No implicit cast is permitted between spaces.

### 3.2 Map spatial reference

Each Map must expose a spatial-reference descriptor through Core Domain or a domain-owned metadata service. At minimum it identifies:
- spatial reference kind: `Geographic`, `Projected`, or `LocalCartesian`;
- stable CRS/definition identifier, or an explicit local-coordinate definition;
- axis order and coordinate units;
- coordinate validity domain;
- wrap/periodicity behavior, if any;
- datum/ellipsoid information where relevant;
- whether the coordinate system supports meaningful distance, area, direction and scale calculations;
- definition/version provenance sufficient to preserve historical interpretation.

For a fictional map, `LocalCartesian` is a valid first-class choice. The system must not force longitude/latitude or attach an Earth datum to coordinates that have no Earth meaning.

A map's spatial reference is not a renderer setting. Changing it is a domain-level transformation/migration with explicit validation and provenance, not a viewport preference.

### 3.3 Projection service

Define an interface conceptually equivalent to:

```csharp
public interface IMapProjection
{
    SpatialReferenceId SourceReference { get; }
    MapCoordinateSystem TargetSystem { get; }

    TransformResult<MapPoint> Forward(DomainPoint point);
    TransformResult<DomainPoint> Inverse(MapPoint point);

    ProjectionCapabilities Capabilities { get; }
    ProjectionExtent? GetValidExtent();
}
```

The exact API may differ, but it must expose:
- forward and inverse transformation;
- explicit failure for invalid/out-of-domain coordinates;
- no silent clamping or invented coordinates;
- declared units and axis behavior;
- wrap/discontinuity handling;
- capability flags for distance/area/direction/scale;
- a stable projection-definition identity for cache keys.

The projection service is deterministic for a given definition/version. If an algorithm upgrade changes historical domain interpretation, it must not silently alter formal geometry. Presentation-only projection improvements may change display output but must not rewrite domain coordinates.

### 3.4 Working map plane

V1 editing and hit testing operate in a 2D map plane. For geographic coordinates, the projection service maps the geographic domain into a suitable 2D working plane for the current map extent. For projected and local Cartesian maps, the working plane may be identity or a declared transform.

Do not use raw longitude/latitude Euclidean distance as a universal distance metric. Near wrap boundaries, poles, or large extents, the projection adapter must provide valid behavior or report that a requested operation is unsupported/ambiguous.

### 3.5 Antimeridian and wrap handling

For periodic coordinate systems:
- projection output must identify discontinuities or wrapped segments;
- render paths must be split at discontinuities rather than drawing a long chord across the map;
- broad-phase bounds must not span the entire world because a short segment crosses the wrap boundary;
- inverse transformation must preserve the intended branch of the wrapped coordinate where possible.

V1 may limit editing across a projection discontinuity if a correct continuous representation is not yet available. It must show an explicit diagnostic rather than corrupt geometry.

## 4. Numeric precision and tolerance policy

### 4.1 Separate authoritative and presentation precision

- Formal coordinate precision and serialization rules belong to Core Domain.
- Spatial Presentation uses double precision for map-space calculations in V1.
- Viewport and device coordinates use floating-point values appropriate to rendering.
- Screen-space approximations never overwrite the authoritative geometry.

Do not quantize formal coordinates merely because the renderer uses pixels or a raster grid.

### 4.2 Tolerance categories

Keep separate tolerances for:
1. **Render approximation tolerance** — maximum visual deviation when flattening a curve/path.
2. **Hit-test tolerance** — user-friendly pointer distance for selecting a rendered feature.
3. **Snap tolerance** — maximum screen-space distance for proposing a snap.
4. **Domain validation tolerance** — Core Domain geometry/invariant rules.
5. **Numerical robustness tolerance** — algorithm-specific guards against floating-point error.

These values have different semantics and must not share one global epsilon.

### 4.3 Screen-space interaction tolerances

Hit and snap tolerances are configured in logical viewport pixels, for example a default hit radius in the 6–10 logical-pixel range and a snap radius in the 8–12 range. These are initial UX defaults to validate, not domain constants.

At the current viewport transform:
- pointer tolerance is evaluated in viewport space;
- map-space query bounds are derived by inverse-transforming the tolerance region, conservatively;
- DPI scaling is applied only at the viewport-to-device boundary;
- changing zoom does not change the perceived pointer target size.

For a non-uniform or nonlinear view transform, do not approximate the inverse tolerance with a single scalar map-unit radius unless a conservative bound is proven. Use the transformed query polygon or a conservative expanded bound.

### 4.4 Robustness

- Reject NaN and infinite coordinates at API boundaries.
- Avoid equality comparisons on arbitrary floating-point geometry values.
- Use scale-aware robust predicates for segment intersection and orientation tests.
- Where a result is numerically indeterminate, return an explicit `Indeterminate`/diagnostic outcome rather than guessing.
- Do not silently snap or round coordinates to hide precision problems.

## 5. Geometry representation and projection pipeline

### 5.1 Supported V1 geometry primitives

The editor pipeline must support:
- points;
- straight line segments;
- Bézier curve segments;
- ordered paths composed of line and Bézier segments;
- closed paths/rings where permitted by the formal geometry model;
- multipart Geometry Components.

The existing formal geometry model remains authoritative for exact segment/control-point representation. The renderer-facing representation may be tessellated or batched, but must retain a stable mapping back to source component and segment identities.

### 5.2 Pipeline

```text
Core Domain resolved World State
        |
        v
Immutable Geometry Component projection
        |
        v
Spatial-reference validation
        |
        v
Domain -> Map projection
        |
        +--> conservative Map-space bounds
        |
        +--> screen-independent geometry cache
        |
        v
Viewport transform (pan / zoom / rotation)
        |
        +--> screen-space path approximation
        +--> hit-test / snap query transforms
        |
        v
Viewport logical pixels
        |
        v
DPI scale
        |
        v
Device pixels / Renderer backend
```

Projection and viewport transform are separate:
- Projection changes the coordinate representation and may be nonlinear.
- Viewport transform changes camera/view state only.
- DPI scaling changes physical raster resolution only.

### 5.3 Geometry cache identity

A projected-geometry cache key must include:
- source Geometry Component identity;
- source geometry content/effective-state fingerprint supplied by Core Domain;
- spatial-reference definition identity;
- projection definition/version;
- any relevant wrap/discontinuity policy.

A screen-space tessellation cache additionally includes:
- viewport transform or its relevant linear/nonlinear parameters;
- render approximation tolerance;
- device scale only if the cached artifact is device-resolution-specific.

Do not use a UI selection state or arbitrary timestamp as a substitute for a geometry content fingerprint.

## 6. Spatial index contract

### 6.1 Purpose

The spatial index is an acceleration structure for finding possible features near:
- a pointer;
- a snap target;
- a viewport rectangle;
- a lasso/selection polygon;
- an invalidation region.

It is derived and disposable. Rebuilding it must not change formal map state or query semantics.

### 6.2 Two-level strategy

Recommended V1:
- **Map-space index** for geometry component bounds, suitable for viewport-independent candidate discovery.
- **Optional screen-space acceleration** for heavily tessellated/large visible scenes, built per viewport snapshot when profiling proves useful.

Start with one in-memory packed R-tree (or equivalent static bounding-volume hierarchy) per immutable scene snapshot. Do not make SQLite R*Tree the mandatory interactive index: the interactive index is snapshot-scoped and frequently replaced with presentation state. A persistent R*Tree may be evaluated later for large offline datasets or import/search workloads.

The index must support:
- point/radius query via conservative bounds;
- rectangle query;
- stable result identifiers;
- cancellation for obsolete viewport/scene requests;
- snapshot identity and generation tracking.

### 6.3 Index entries

An entry should identify:
- Map ID / Branch context token as needed by the snapshot;
- Object ID;
- Geometry Component ID;
- primitive/segment reference where available;
- conservative Map-space bounds;
- optional geometry-family/type discriminator;
- source geometry fingerprint.

One Object may produce many index entries. Do not collapse multiple Geometry Components into one opaque object-level box if that harms query selectivity.

### 6.4 Conservative bounds

Bounds must never exclude a point that belongs to the represented geometry.

For line segments, use endpoint bounds.

For Bézier curves:
- compute a conservative control-hull bound in the coordinate space where the curve is represented;
- if the projection is nonlinear, do not assume projecting only the four control points bounds the projected curve;
- adaptively subdivide the curve through the projection service and union conservative subcurve bounds, or use a proven projection-specific bound;
- if no conservative bound can be established, mark the entry as requiring a slower fallback query rather than inserting an unsafe narrow bound.

Index false positives are acceptable; false negatives are not.

### 6.5 Snapshot consistency

Every query must bind to one immutable scene/index generation. A query started against generation N must not return candidates from generation N+1 mixed with N. On scene replacement, obsolete query results are discarded or explicitly marked stale.

## 7. Hit-testing contract

### 7.1 Two-stage evaluation

1. **Broad phase:** transform the pointer tolerance region into Map space and query the spatial index.
2. **Narrow phase:** evaluate actual projected geometry against the pointer in viewport space using the current transform and tolerance.

The narrow phase returns the closest valid hit(s), not merely the first index result.

### 7.2 Geometry-specific tests

- Point: screen-space distance to the symbol's actual hit shape.
- Line: closest point on the projected segment/polyline.
- Bézier: closest point on the projected curve within a declared numerical tolerance.
- Filled area/ring: point-in-fill test plus boundary-distance handling.
- Multipart component: evaluate each relevant part and preserve the source part/segment identity.

The visible symbol/stroke width and interaction target may differ. Use an explicit hit-shape policy; do not make thin one-pixel lines impossible to select.

### 7.3 Bézier narrow phase

Preferred approach:
1. Evaluate a screen-space adaptive subdivision using a maximum flatness/deviation error in logical pixels.
2. Use the resulting polyline for a fast nearest-distance estimate.
3. Refine the nearest parameter on the original projected curve using a bounded numerical minimization/Newton-style method where derivatives are stable.
4. Fall back to subdivision-only distance with a reported precision bound if refinement is unstable.

The algorithm must return:
- source segment identity;
- curve parameter `t` in `[0,1]` when applicable;
- closest viewport point;
- pointer-to-geometry distance;
- approximation/refinement error bound;
- whether the result is exact, bounded approximation, or indeterminate.

The curve itself is never replaced by the flattened polyline.

### 7.4 Hit result

Conceptual result:

```csharp
public sealed record HitResult(
    ObjectId ObjectId,
    GeometryComponentId ComponentId,
    GeometryPartId? PartId,
    GeometrySegmentId? SegmentId,
    HitFeatureKind FeatureKind,
    double? SegmentParameter,
    ViewportPoint ClosestPoint,
    double DistanceLogicalPixels,
    HitPrecision Precision,
    SceneGeneration SceneGeneration);
```

A hit result is a reference into the queried snapshot, not a durable domain command. Before applying an edit, the Interaction Controller must verify the source context is still current and ask Core Domain to validate the operation.

### 7.5 Deterministic hit ranking

Rank candidates by:
1. interaction-tool-specific priority (e.g. active handles before body selection);
2. distance in logical pixels;
3. explicit layer/interaction priority;
4. stable Object ID;
5. stable Geometry Component ID;
6. stable segment/part ID.

Do not use spatial-index traversal order as a tie-breaker. If the product wants topmost visual object to win, encode draw-order priority explicitly in the ranking contract.

## 8. Snapping contract

### 8.1 Snap candidates

V1 snap kinds:
- existing geometry vertex/node;
- line-segment endpoint;
- Bézier endpoint;
- point on line segment;
- point on Bézier curve;
- boundary/edge;
- intersection;
- configured grid point;
- optional construction guide.

A snap candidate contains:
- snap kind;
- source Object / Geometry Component / segment identity when applicable;
- source parameter (`t`) for curve/segment snaps;
- target point in Map and Viewport space;
- logical-pixel distance;
- candidate provenance;
- scene generation;
- whether it represents an existing formal node or only a computed geometric location.

### 8.2 Snap is a proposal

Snapping must not:
- mutate formal geometry during pointer movement;
- merge Object identities;
- create a shared topological node by implication;
- change the geometry of a neighboring Object;
- bypass Core Domain geometry operations.

Pointer movement updates only a transient preview. On user acceptance, the Interaction Controller builds the relevant typed domain operation and calls Domain Services. Core Domain validates whether the result creates or preserves topology under its formal rules.

### 8.3 Candidate generation and ranking

1. Query broad-phase index using snap tolerance.
2. Generate supported snap features for each candidate geometry.
3. Project candidates into viewport space.
4. Discard candidates outside the logical-pixel snap radius.
5. Apply tool-specific filters (e.g. do not snap to the geometry currently being dragged unless explicitly enabled).
6. Rank deterministically.

Recommended ranking order:
- active tool's explicit snap priority;
- snap-kind priority (configurable but deterministic);
- logical-pixel distance;
- existing formal vertex/node before computed point on curve when priorities otherwise tie;
- visual/layer priority if relevant;
- stable Object / Component / segment IDs.

Do not combine all candidate types into one opaque score with arbitrary weights. A transparent lexicographic comparator is easier to debug and test.

### 8.4 Intersections

For line-line intersections, use robust segment predicates and retain the source parameters for both segments.

For curve intersections:
- use adaptive subdivision to find candidate parameter intervals;
- refine intersections on the original curves;
- report tangencies and near-coincident cases as potentially ambiguous;
- never create a topological connection merely because two rendered strokes visually touch.

If the intersection is ill-conditioned or multiple solutions are too close to distinguish at the current precision, return an ambiguity diagnostic and require explicit user selection or a higher-precision operation.

### 8.5 Grid snapping

Grid snapping is a map/display aid. Grid origin, spacing, orientation and coordinate basis must be explicit. A grid in geographic coordinates is not automatically a uniform metric grid. If a grid's physical spacing is requested, require a CRS/projection capability that supports that interpretation.

## 9. Editing and drag-preview lifecycle

### 9.1 Pointer interaction

The interaction controller owns the gesture state machine:
- Idle;
- Hovering;
- Pressed / potential gesture;
- Dragging;
- Previewing;
- Awaiting domain validation;
- Committed / Cancelled / Rejected.

The renderer only displays the immutable scene plus transient preview overlays.

### 9.2 Drag preview

During a drag:
- keep the formal geometry unchanged;
- retain the original source snapshot and source geometry fingerprint;
- calculate preview geometry in a transient interaction model;
- update hit/snap candidates against a clearly defined preview policy;
- show invalid/ambiguous feedback without writing domain state.

On pointer release:
1. revalidate the target Map/Branch/Historical Time and source geometry context;
2. construct the exact supported Core Domain operation proposal;
3. request preflight validation;
4. obtain any required user confirmation;
5. submit through Domain Services;
6. receive the new authoritative projection/snapshot;
7. replace the previous presentation snapshot.

Cancel discards the preview only.

### 9.3 Stale context

If the selected Branch, Historical Time, geometry fingerprint or scene generation changes during a gesture:
- cancel or freeze the gesture;
- do not apply against stale geometry;
- explain that the editing context changed;
- require a fresh selection/gesture.

Do not silently rebase a drag onto a different historical state.

## 10. Threading, cancellation and resource ownership

- Core Domain calls remain behind Domain Services.
- Projection, index construction and expensive curve evaluation may run on background workers using immutable inputs.
- UI pointer feedback must be responsive; cancel obsolete queries when the pointer moves or the scene generation changes.
- Results are marshalled back to the UI/interaction owner and accepted only if their scene generation and request token still match.
- The spatial index is owned by its immutable presentation snapshot and disposed only after no query retains it.
- No mutable geometry buffers are shared between background construction and active rendering.
- Renderer receives render-ready immutable data; it does not own or mutate the spatial index.
- Do not access Avalonia UI objects from background workers or render-thread callbacks.

## 11. API boundary summary

Conceptual service boundaries:

```csharp
public interface IProjectionService
{
    ProjectionResult Project(
        SpatialReferenceDescriptor source,
        GeometrySnapshot geometry,
        ProjectionOptions options);
}

public interface ISpatialIndex
{
    SceneGeneration Generation { get; }
    IReadOnlyList<SpatialCandidate> Query(SpatialQuery query);
}

public interface IHitTestService
{
    HitTestResult HitTest(
        SceneSnapshot scene,
        ViewportTransform transform,
        ViewportPoint pointer,
        HitTestOptions options);
}

public interface ISnapService
{
    SnapResult FindCandidates(
        SceneSnapshot scene,
        ViewportTransform transform,
        ViewportPoint pointer,
        SnapOptions options);
}
```

These are responsibility sketches, not final public API signatures. Prefer concrete internal services over a generic plugin framework. All results carry snapshot/context identity.

## 12. Failure and diagnostic model

Return structured diagnostics for:
- unsupported or missing CRS definition;
- invalid coordinate/out-of-domain projection;
- wrap/discontinuity ambiguity;
- failed inverse transform;
- missing conservative bounds;
- stale scene generation;
- geometry with invalid/non-finite coordinates;
- indeterminate intersection;
- curve refinement non-convergence;
- unsupported snap kind for current tool;
- stale authoring context / failed Core Domain preflight.

Never silently:
- replace failed transforms with `(0,0)`;
- clamp coordinates into the projection domain;
- treat a failed exact curve test as a successful approximate hit without labeling its precision;
- snap to a visually nearby but geometrically unrelated feature;
- commit a preview after its source context becomes stale.

## 13. V1 implementation recommendation

### 13.1 Build in this order

1. Typed coordinate structs and explicit viewport transform.
2. Spatial-reference descriptor and projection adapter boundary.
3. Point/line/Bezier path projection and conservative bounds.
4. In-memory immutable R-tree/BVH for Map-space candidate queries.
5. Screen-space narrow-phase hit testing.
6. Deterministic hit ranking and stable source references.
7. Vertex/endpoint/segment/curve/grid snap candidates.
8. Line intersections, then curve intersections with ambiguity diagnostics.
9. Drag-preview lifecycle and Core Domain preflight integration.
10. Profiling and only then optional screen-space index or more advanced acceleration.

### 13.2 Keep external libraries behind adapters

Potential libraries may be evaluated for:
- CRS/projection implementations;
- robust computational geometry predicates;
- spatial index implementation.

Do not allow an external library's geometry classes to leak into Core Domain or public presentation contracts. Wrap it behind World View-owned types. Verify licensing, platform support, deterministic behavior and maintenance status before adoption.

Mapsui may be used for standard basemap/tile/projection integration where useful, but it is not the authoritative geometry editor, spatial interaction engine or Core Domain.

### 13.3 V1 non-goals

- 3D globe editing;
- universal geodesic editing for all geometry operations;
- arbitrary dynamic CRS plugin marketplace;
- fully general planar arrangement/topology engine;
- automatic topology repair during snapping;
- cross-map geometry mutation;
- persistent authoritative spatial index;
- GPU-based hit testing as the default path.

## 14. Required verification tests

### 14.1 Coordinate and projection tests

- Domain → Map → Domain round-trip within declared projection error bounds.
- Invalid/out-of-domain coordinates return explicit failure.
- Axis order and units are respected.
- Local Cartesian maps do not acquire Earth-specific semantics.
- Wrap-boundary paths render as intended and do not create world-spanning artifacts.
- Projection-definition changes invalidate projected geometry and relevant caches.
- Viewport pan/zoom/rotation never mutates Domain Coordinates.
- DPI changes alter device output but not logical-pixel hit/snap behavior.

### 14.2 Spatial index tests

- Every indexed geometry is discoverable by queries that intersect it.
- Bézier conservative bounds contain the full curve.
- Nonlinear projection bounds remain conservative.
- False positives are filtered by narrow phase.
- No false negatives are allowed.
- Snapshot replacement never mixes index generations.
- Cancellation prevents stale query results from being applied.

### 14.3 Hit-testing tests

- Point, line, Bézier, ring and multipart geometry hit cases.
- Stroke width and interaction target policy.
- Bézier nearest-point accuracy within declared screen-space error.
- Stable ranking for equal-distance and overlapping candidates.
- Topmost/layer priority behaves according to explicit policy.
- Same input snapshot and viewport produces identical candidate ordering.

### 14.4 Snapping tests

- Vertex, endpoint, segment, curve, boundary, intersection and grid candidates.
- Screen-space tolerance remains stable at multiple zoom and DPI levels.
- Existing formal nodes outrank computed locations according to policy.
- Curve intersection tangencies and near-coincident cases report ambiguity.
- Snap preview does not mutate formal geometry.
- Accepted snap still passes Core Domain validation.
- Geometry split/merge does not implicitly alter Object identity.

### 14.5 Editing lifecycle tests

- Cancelled drag leaves formal geometry unchanged.
- Stale Branch/Time/context rejects the gesture.
- Core Domain rejection leaves the prior scene intact and displays diagnostics.
- Successful operation publishes a new snapshot atomically.
- Undo/history behavior is owned by Core Domain, not the interaction preview.

## 15. Open decisions deliberately deferred

The following are intentionally not frozen by this contract:
- exact CRS/projection library and supported CRS catalog;
- exact spatial index package (R-tree versus BVH implementation);
- final hit/snap pixel defaults after usability testing;
- exact curve subdivision/refinement algorithm and error budget;
- support envelope for geographic editing near poles and wrap boundaries;
- exact topology validation algorithms and their Core Domain API;
- whether large scenes need a second screen-space index;
- public versus internal visibility of the conceptual interfaces.

These are implementation choices or technical-spike questions, not reasons to weaken the authority boundaries above.

## 16. Acceptance criteria

This contract is implementation-ready when:
- all coordinate spaces are explicit and type-safe;
- the projection service owns all domain/map transforms;
- spatial queries are snapshot-consistent and conservative;
- Bézier curves are selectable and snappable without replacing their formal representation;
- hit/snap tolerance is screen-space and DPI-aware;
- snap and drag previews cannot mutate formal data;
- every accepted edit is validated and committed by Core Domain;
- deterministic ranking and structured failure diagnostics are covered by tests.

---

## References and alignment

- World View Core Domain architecture baseline: formal World State is reconstructed by Core Domain; UI consumes resolved projections.
- World View Geometry model: Object identity and Geometry Components are separate; geometry operations do not imply Object operations.
- Core Domain P2-09-08 Command Descriptor and Deterministic Replay Contract V0.1: Commands express authoring intent; animation/render interpolation is not a State Change; historical semantics must not silently change.
- Renderer Contract & Scene Model V0.1: immutable presentation snapshots, renderer-neutral scene data, typed coordinate boundaries and explicit cache invalidation.
- Mapsui documentation: optional .NET/Avalonia GIS/tile integration only; not the World View domain or editor authority.