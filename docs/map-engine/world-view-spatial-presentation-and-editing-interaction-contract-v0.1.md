# World View — Spatial Presentation and Editing Interaction Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended technical baseline
- Scope: Coordinate spaces, CRS/projection boundary, spatial indexes, hit testing, snapping and edit-preview interaction
- Depends on: World View Architecture Baseline V1; Map Rendering & Visualization Architecture V0.3; Renderer Contract and Scene Model V0.1; Core Domain Geometry and Object Operation boundaries
- Decision owner: World View architecture

---

## 1. Executive decisions

This document defines how World View translates map geometry into interactive screen space and how the editor finds, selects and snaps to geometry.

Recommended V1 decisions:

1. **A Map owns an explicit coordinate reference definition.** The system must not assume every map is Earth longitude/latitude. Fictional worlds and schematic maps need a valid local Cartesian coordinate system.
2. **Formal geometry remains in its authoritative domain coordinate reference.** Screen pixels and viewport coordinates are never persisted as formal geometry.
3. **Coordinate conversion is an explicit pipeline.** Domain coordinates, projected/map coordinates, viewport logical pixels and device pixels are separate spaces with named transforms.
4. **CRS transformation and camera transformation are different operations.** A camera pan/zoom must not rewrite geometry or be confused with a projection change.
5. **Spatial indexes are derived acceleration structures.** They are rebuildable and never become authoritative geometry or topology.
6. **Hit testing uses a two-stage algorithm.** A spatial index finds conservative candidates; precise geometry tests determine the actual hit.
7. **Line and Bézier geometry are both first-class.** The editor must not permanently flatten Bézier curves merely to support indexing or rendering.
8. **Snapping is a presentation/interaction service with explicit policies.** It returns a candidate and explanation; it does not silently edit formal geometry.
9. **Visual proximity is not topology.** Snapping to a point or edge does not automatically create shared topology, merge geometry components or merge Object identities.
10. **All formal edits go through application/domain operations.** Interaction creates an edit intent/preview; Core Domain services validate and persist the authorized Command or geometry operation.

The V1 target is a robust desktop map editor, not a full GIS topology suite. The design should support real-world maps and fictional/local maps without imposing unnecessary geospatial complexity on every project.

## 2. Spatial architecture

```text
Core Domain
  └─ authoritative geometry + Map coordinate reference definition
       │
       ▼
Spatial Presentation
  ├─ CRS / projection adapter
  ├─ coordinate transform pipeline
  ├─ projected geometry cache
  ├─ spatial index (broad phase)
  ├─ precise hit testing (narrow phase)
  └─ snapping candidate service
       │
       ▼
Interaction Controller
  ├─ pointer and keyboard input
  ├─ selection policy
  ├─ edit preview
  └─ edit intent
       │
       ▼
Application / Domain Services
  ├─ validate geometry operation
  ├─ validate topology/domain constraints
  └─ create formal Command / State Changes as appropriate
```

Spatial Presentation may consume neutral Render Items and source geometry references from the Presentation Scene, but it is not owned by the renderer.

### 2.1 Core Domain owns

- formal geometry values and geometry component identity;
- the Map's coordinate reference definition and its semantic meaning;
- geometry validity and domain constraints;
- formal topology relationships where the domain explicitly models them;
- geometry operations and Object operations;
- historical persistence of formal geometry changes.

### 2.2 Spatial Presentation owns

- coordinate transforms used for display and interaction;
- projected/viewport geometry caches;
- derived spatial indexes;
- approximate geometry used only for acceleration;
- hit-test and snap candidate calculations;
- interaction tolerance and ranking policy;
- diagnostics for coordinate/projection failures.

### 2.3 Interaction Controller owns

- pointer/keyboard interaction state;
- active tool and current selection;
- preview handles and temporary geometry;
- user-visible snap choice and override;
- construction of an edit intent for application services.

It does not persist formal geometry directly.

## 3. Coordinate reference model

### 3.1 Do not force every Map into geographic coordinates

World View must support at least two broad map coordinate families:

**A. Local Cartesian Map**

Suitable for:
- fictional worlds;
- hand-authored fantasy maps;
- city/building/interior plans whose geometry is authored in local units;
- schematic maps where Earth coordinates are irrelevant.

Coordinates are X/Y values in a named local coordinate reference. The unit may be map units, metres, feet or another explicitly defined unit. If the map has no real-world scale, its unit is a drawing unit and must not be presented as a real physical distance.

**B. Geographic / Earth-referenced Map**

Suitable for:
- real-world geography;
- historical maps anchored to real locations;
- maps that need standard GIS data or basemap interoperability.

The Map has an explicit geographic CRS and a selected projected/display CRS for planar drawing and measurement operations where appropriate.

Do not silently convert a local fictional map into latitude/longitude. Do not label arbitrary local units as metres or kilometres.

### 3.2 Coordinate reference definition

Conceptual Map-owned definition:

```csharp
public sealed record MapCoordinateReference(
    string CoordinateReferenceId,
    CoordinateReferenceKind Kind,
    string UnitId,
    AxisDefinition AxisDefinition,
    CoordinateReferenceMetadata Metadata);

public enum CoordinateReferenceKind
{
    LocalCartesian,
    Geographic,
    Projected
}
```

This is an architectural shape, not a frozen physical schema.

The definition must establish:
- stable coordinate reference identity;
- coordinate family;
- axis order and axis direction;
- unit;
- origin/anchor semantics where applicable;
- geographic datum/CRS identifier where applicable;
- projection metadata where applicable;
- precision and valid-coordinate constraints.

A display name such as “World Coordinates” is not sufficient metadata to perform a transformation.

### 3.3 V1 default

- A new fictional/local map uses Local Cartesian coordinates.
- A real-world map must choose or import an explicit geographic/projected CRS.
- The application may offer a suitable display projection for a geographic map, but the selection and transformation must be explicit and recorded as Map/application configuration.
- The map's formal geometry is not rewritten merely because the user changes the viewport or display projection.

The exact CRS library and supported CRS catalogue remain a technical selection to validate in the implementation spike.

## 4. Coordinate spaces and transforms

World View uses four explicit coordinate spaces.

| Space | Meaning | Persistent? |
|---|---|---:|
| Domain Coordinates | Formal geometry coordinates in the Map's authoritative CRS | Yes, through Core Domain |
| Projected Map Coordinates | Coordinates used by spatial presentation for planar rendering/indexing | No, derived |
| Viewport Coordinates | Logical pixels inside the Avalonia map viewport | No |
| Device Coordinates | Physical display pixels after DPI scaling | No |

### 4.1 Transform pipeline

```text
Domain Coordinates
   │  CRS / projection transform
   ▼
Projected Map Coordinates
   │  camera transform: pan / zoom / rotation
   ▼
Viewport Logical Pixels
   │  device scale factor
   ▼
Device Pixels
```

The reverse pipeline is used for pointer interaction:

```text
Device Pixels
   │  inverse DPI scale
   ▼
Viewport Logical Pixels
   │  inverse camera transform
   ▼
Projected Map Coordinates
   │  inverse projection (when supported)
   ▼
Domain Coordinates
```

### 4.2 Transform responsibilities

**CRS / projection transform**
- converts formal domain coordinates to a planar presentation coordinate system;
- may be nonlinear;
- may be undefined or singular in some regions;
- may require adaptive geometry densification for display;
- is invalidated when CRS/projection configuration changes.

**Camera transform**
- applies pan, zoom and optional viewport rotation;
- should be an efficient affine transform in V1;
- changes frequently during interaction;
- does not change formal geometry or the projection cache.

**DPI transform**
- converts logical UI pixels to physical device pixels;
- follows Avalonia/platform scale behavior;
- must not be manually applied twice.

### 4.3 Transform API contract

Conceptual operations:

```csharp
public interface ICoordinateTransform
{
    TransformResult<DomainPoint> ToDomain(ProjectedMapPoint point);
    TransformResult<ProjectedMapPoint> ToProjected(DomainPoint point);
}

public interface ICameraTransform
{
    ViewportPoint ToViewport(ProjectedMapPoint point);
    ProjectedMapPoint ToProjected(ViewportPoint point);
}
```

Every transform must return an explicit failure result for out-of-domain, singular, invalid or non-finite coordinates. Never silently return `(0, 0)` or an unmarked approximate point.

### 4.4 Precision and round-trip behavior

- Use `double` for coordinate calculations in V1.
- Keep formal coordinate precision policy in Core Domain/geometry contracts.
- Do not round formal geometry to screen pixels.
- Round-trip transforms are subject to the declared projection's numerical accuracy; tests must define tolerances per CRS.
- Display simplification and pixel snapping must never modify formal coordinate values.
- A coordinate copied from a pointer is converted back to Domain Coordinates before it can become an edit proposal.

### 4.5 Projection changes

Changing the viewport zoom/pan:
- updates camera transform;
- updates visible bounds;
- does not rebuild formal geometry;
- usually does not invalidate projected geometry.

Changing the display projection:
- increments projection generation;
- invalidates projected geometry caches and projection-dependent indexes;
- rebuilds display representations;
- does not alter the authoritative Domain Coordinates.

If the user intends to permanently reproject formal geometry, that is a separate explicit domain/application operation with validation and history semantics. It is not a view preference.

## 5. Geometry support and derived representations

### 5.1 Formal geometry remains authoritative

The Spatial layer consumes formal geometry values or immutable geometry payloads. It does not replace them with a screen-optimized version.

V1 formal path support includes:
- line segments;
- quadratic/cubic Bézier segments as allowed by the formal geometry model;
- mixed paths containing line and Bézier segments;
- open/closed paths;
- multi-part and ring structures where supported by the domain geometry contract.

### 5.2 Derived geometry representations

Spatial Presentation may derive:

1. **Conservative bounds** — for spatial-index lookup.
2. **Adaptive polyline approximation** — for fast distance estimation and some display operations.
3. **Segment-level bounds** — for individual line and curve segments.
4. **Projected geometry** — for the active display projection.
5. **LOD geometry** — for low-zoom rendering only.

Each derived representation records the source geometry revision, projection generation and approximation tolerance/LOD parameters required to determine whether it remains valid.

### 5.3 Bézier curve handling

For a Bézier curve:
- preserve original control points in the formal geometry;
- compute conservative bounds from the curve's actual extrema or a guaranteed conservative control hull;
- use adaptive subdivision for broad-phase refinement and approximate nearest-point estimates;
- use a precise closest-point solver or bounded iterative refinement for final hit/snap distance where required;
- report approximation tolerance and confidence to the caller.

A fixed number of line segments is not sufficient for all zoom levels and curvature. Adaptive subdivision should be based on projected deviation in screen space, subject to a maximum recursion/segment limit and safe fallback.

For editing a control point, the interaction layer must retain the original curve segment identity and parameter/handle semantics. It must not turn a curve into a set of unrelated line segments.

## 6. Spatial index

### 6.1 Purpose

The spatial index is a broad-phase acceleration structure. It answers:

> Which geometry or render items might be near this point or viewport?

It does not answer:
- whether two geometries are topologically connected;
- whether an edit is legal;
- whether an Object can be selected under current tool policy;
- which formal Object identity should be created or deleted.

### 6.2 V1 recommendation

Use an R-tree-style bounding-box index for 2D presentation geometry.

Reasons:
- supports viewport range queries;
- supports point/radius queries;
- handles unevenly distributed map content better than a uniform grid in many map-editing cases;
- can index render items or geometry segments;
- is a derived structure that can be rebuilt.

Do not make the exact library choice part of the domain contract. Benchmark a maintained .NET implementation against a small in-house implementation only if needed. Prefer an established library if its lifecycle, correctness and performance fit.

### 6.3 Index granularity

Use a two-level strategy where measurements justify it:

- item-level bounds to find candidate Objects/Geometry Components;
- segment-level bounds for complex paths, especially long curves or dense boundaries.

Do not index every Bézier subdivision point as if it were formal geometry. Segment entries refer back to the original segment identity and parameter interval.

### 6.4 Index invalidation

Invalidate or update index entries when:
- source geometry revision changes;
- projection generation changes;
- an item becomes hidden/removed from the active presentation scene;
- the relevant derived bounds change.

Camera pan/zoom alone normally does not invalidate map-space index entries. It changes the query region and screen-space tolerance conversion.

### 6.5 Large or pathological geometry

A very large geometry may span most of the map and produce many false-positive candidates. Mitigations:
- segment-level index entries;
- separate indexes by geometry family/layer where useful;
- viewport culling;
- candidate limits with deterministic continuation/refinement;
- metrics for candidate count and narrow-phase cost.

Do not silently truncate candidates in a way that can hide a closer valid snap target. If a safety cap is reached, refine the query or report degraded interaction state.

## 7. Hit testing

### 7.1 Two-stage hit testing

**Stage 1 — Broad phase**
- convert pointer location to Projected Map Coordinates;
- derive a conservative map-space query region corresponding to the desired screen tolerance;
- query the spatial index;
- filter hidden, locked, non-selectable or inactive items according to the current interaction policy.

**Stage 2 — Narrow phase**
- calculate precise or tolerance-bounded distance to candidate geometry;
- test fill containment for closed areas;
- test symbol/label interaction bounds;
- apply z-order and selection policy;
- return a ranked set of candidates, not just the first arbitrary index result.

### 7.2 Screen-space tolerance

Hit tolerance should be specified in logical screen pixels because users perceive selection difficulty in pixels, not map units.

For example, a default selection tolerance might be around 6–8 logical pixels, adjustable for accessibility and tool type. This is an initial UX default, not a frozen product constant.

For a rotated or nonuniform transform, convert a screen-space tolerance region through the inverse camera transform rather than assuming one fixed map-unit radius.

### 7.3 Geometry-specific tests

| Geometry family | Narrow-phase test |
|---|---|
| Point/symbol | Distance to anchor or symbol interaction shape |
| Line segment | Closest point on segment |
| Bézier curve | Closest point on curve with bounded solver |
| Closed fill | Point-in-ring / fill-rule containment, plus boundary tolerance |
| Multi-part geometry | Evaluate parts and preserve part identity |
| Text label | Label layout's interaction rectangle/shape, then source reference |

For line and curve hit tests, distinguish:
- hit on stroke;
- hit on fill;
- hit on control handle;
- hit on vertex/node;
- hit on label/symbol.

These are different interaction targets and may have different priorities.

### 7.4 Candidate result

Conceptual result:

```csharp
public sealed record HitTestCandidate(
    SourceObjectRef Source,
    GeometryTargetRef? GeometryTarget,
    HitTargetKind TargetKind,
    double ScreenDistance,
    int ZIndex,
    HitConfidence Confidence);
```

The result must preserve enough source identity to let the Interaction Controller select the correct Object/Geometry Component/segment/handle. A screen pixel coordinate alone is not a valid edit target.

### 7.5 Selection policy

Selection policy is tool-aware:

- Select tool prefers visible selectable Object bodies and symbols.
- Vertex tool prefers vertices and control handles.
- Geometry edit tool prefers the active Geometry Component and its segments.
- Label tool prefers text candidates.
- A locked layer may remain visible but is excluded from editing targets.
- A hidden layer is excluded from normal hit testing unless a dedicated hidden-object selection workflow exists.

If multiple candidates are within tolerance, return a small ranked candidate list for cycling/chooser UI rather than making an unstable choice based on index iteration order.

## 8. Snapping

### 8.1 Snap target types

V1 snap target types:

1. Grid intersections.
2. Existing geometry vertices/nodes.
3. Segment points on lines.
4. Nearest points on Bézier curves.
5. Geometry intersections.
6. Shared or adjacent boundary candidates where the formal geometry model exposes them.
7. User-created guides/auxiliary lines.
8. Optional angle/axis constraints for drawing tools.

A snap target must identify its source and type. It must not be represented only as an unlabelled coordinate.

### 8.2 Snap query pipeline

1. Interaction Controller requests snapping for a pointer position and active tool.
2. Spatial Presentation converts pointer and tolerance into the correct coordinate spaces.
3. Broad-phase indexes find nearby geometry candidates.
4. Snap providers calculate precise target points.
5. Policy filters disabled/ineligible target types.
6. Candidates are ranked deterministically.
7. The chosen candidate is returned with target type, source identity, screen distance and confidence.
8. UI displays a snap marker/guide and lets the user override or cycle candidates where needed.
9. On confirmation, Interaction Controller creates an edit intent; Domain Services validate the formal operation.

### 8.3 Ranking policy

Use a deterministic, explainable ranking policy.

Recommended order when enabled:
- explicit tool constraint / user-locked snap;
- existing vertex/node;
- exact geometry intersection;
- shared boundary node where the domain exposes one;
- nearest point on segment/curve;
- grid intersection;
- guide/auxiliary line;
- free point (no snap).

This is a default priority policy, not a claim that every tool must enable every snap type. Tool configuration may override the order explicitly.

Within the same target class:
1. smallest screen-space distance;
2. eligible active/visible layer preference;
3. stable source identity tie-breaker.

Never depend on R-tree traversal order or hash-map enumeration order for the selected snap result.

### 8.4 Snap tolerance and hysteresis

- Use logical screen pixels for user-facing snap tolerance.
- Use a separate acquisition tolerance and release tolerance to avoid flicker while moving near competing targets.
- Keep the chosen target stable while it remains within the release tolerance, unless a higher-priority explicit constraint appears.
- Allow temporary modifier-key bypass and explicit cycling among candidates.
- High-DPI displays must not make snapping unexpectedly harder or easier.

### 8.5 Intersection snapping

Intersection snapping must distinguish:
- line-line intersection;
- line-Bézier intersection;
- Bézier-Bézier intersection;
- endpoint coincidence;
- near intersection within a visual tolerance.

An approximate near-intersection must not be reported as an exact formal intersection. Candidate results should indicate exact/approximate status and numerical tolerance.

If an intersection is computed from projected geometry under a nonlinear projection, convert/validate the resulting point against the source-domain geometry before proposing a formal edit.

## 9. Snapping is not topology

This distinction is mandatory.

Examples:
- Snapping a new road endpoint to a river line does not automatically connect the road and river in the domain model.
- Snapping a region boundary to another region boundary does not automatically create a shared-edge topology record.
- Snapping two Object geometries to the same coordinate does not merge their Object identities.
- Geometric split/merge operations do not silently create/delete/merge semantic Objects.

The interaction layer may offer an explicit topology-aware operation when the user requests it. That operation must go through the relevant Geometry/Topology Domain Service, which validates and creates formal topology facts where supported.

A normal snap only chooses a coordinate/target reference for the current edit intent.

## 10. Edit preview and commit boundary

### 10.1 Preview

During pointer movement:
- update transient preview geometry;
- show candidate snap markers and guides;
- perform validation that is cheap and deterministic;
- do not write formal geometry or Commands on every pointer move;
- do not reserve formal Object IDs merely because the user is hovering or previewing.

### 10.2 Commit

When the user confirms:
1. Interaction Controller emits a typed edit intent.
2. Application layer resolves the target Map/Branch and current authoring context.
3. Domain Services validate geometry, topology, identity and historical constraints.
4. If valid, the operation is translated into the appropriate formal Command/State Change behavior.
5. A successful commit publishes a new authoritative state and causes the Presentation Scene/index to update.

If the target state changed since the preview was created, the application must detect staleness and revalidate. It must not blindly apply a stale pointer-derived coordinate edit.

### 10.3 Geometry versus Object operations

Maintain the established distinction:
- Geometry Operations alter geometry structure/shape.
- Object Operations alter semantic Object identity/relationships.
- Geometry split does not automatically create Objects.
- Geometry merge does not automatically merge Objects.

Spatial interaction may present both kinds of operations, but it must call the correct domain service.

## 11. Layer, visibility and editability policy

Each spatial candidate must be evaluated against current presentation and tool policy:

- visible/hidden;
- selectable/non-selectable;
- editable/locked;
- active layer preference;
- layer order/z-order;
- object type/tool compatibility;
- temporary preview versus formal scene item.

A hidden or locked item must not unexpectedly capture pointer interaction. If the product later adds “select hidden/locked object” modes, these must be explicit modes with clear visual feedback.

## 12. Coordinate and geometry failure behavior

The system must handle:
- points outside a projection's valid area;
- singular inverse transforms;
- NaN/infinite coordinates;
- degenerate line segments;
- zero-length or invalid Bézier segments;
- extremely large coordinate magnitudes;
- near-coincident intersections;
- curves with high curvature or pathological control points;
- stale projection/index generations.

Rules:
- return typed failure/diagnostic results;
- do not substitute arbitrary coordinates;
- do not crash the whole editor because one candidate is invalid;
- exclude invalid candidates with diagnostics where safe;
- formal geometry validity remains the Domain Service's authority.

## 13. Performance and caching strategy

Recommended V1 cache layers:

| Cache | Key dependencies | Invalidated by |
|---|---|---|
| Projected geometry | Source geometry revision + projection generation | Geometry or projection change |
| Segment bounds | Geometry revision + projection generation | Geometry or projection change |
| Spatial index | Scene generation + projection generation + indexed visibility policy | Geometry/scene/projection/index policy |
| Screen tessellation | Geometry revision + projection generation + zoom/LOD bucket + tolerance | Geometry/projection/LOD change |
| Snap candidate cache (optional) | Pointer region + scene/index generation + tool snap policy | Pointer movement, scene/index/policy change |

Do not introduce all caches prematurely. Begin with projected geometry, bounds and one spatial index. Add segment indexes and snap caches only when profiling demonstrates need.

## 14. Test and verification plan

### SP-01 — Coordinate transform correctness
- local Cartesian identity and affine transforms;
- geographic/projected round trips for selected supported CRS;
- invalid/out-of-range coordinates;
- DPI and camera transforms;
- pointer-to-domain conversion.

### SP-02 — Geometry accuracy
- line segment closest point;
- quadratic/cubic Bézier closest point;
- extrema and conservative bounds;
- mixed line/curve paths;
- closed-ring containment;
- degenerate geometry.

### SP-03 — Hit testing
- screen-space tolerance at multiple zoom levels;
- z-order and selection policy;
- locked/hidden layer exclusion;
- overlapping Objects and candidate cycling;
- high-DPI behavior.

### SP-04 — Snapping
- grid/node/segment/curve/intersection/guide candidates;
- deterministic ranking under repeated runs;
- acquisition/release hysteresis;
- modifier-key bypass and candidate cycling;
- exact versus approximate intersection reporting.

### SP-05 — Topology boundary
- ordinary snap does not create topology;
- explicit topology operation validates and records the relationship;
- geometry split does not create semantic Objects;
- geometry merge does not merge Object identities.

### SP-06 — Invalidation and performance
- geometry edit updates affected bounds/index entries;
- projection change invalidates projected data and indexes;
- camera movement does not rebuild map-space indexes;
- large dense map queries remain responsive;
- fallback full rebuild produces the same query results.

### SP-07 — Stale edit protection
- preview created from one scene generation;
- source geometry changes before commit;
- commit detects stale target and revalidates/rejects safely.

## 15. Acceptance criteria

The Spatial Presentation contract is ready for implementation when:

1. Local Cartesian and explicit geographic/projected maps are both representable.
2. Formal geometry is never stored in viewport/device coordinates.
3. CRS and camera transforms are separate and independently invalidated.
4. Transform failures are explicit and never silently replaced with arbitrary coordinates.
5. Line and Bézier geometry are supported by the same interaction pipeline.
6. Spatial index results are only broad-phase candidates.
7. Final hit tests use precision geometry or a documented bounded solver.
8. Selection and snapping tolerances are screen-space based and DPI-consistent.
9. Snap results carry source identity and target type.
10. Snap candidate ordering is deterministic.
11. Ordinary snapping does not create formal topology.
12. Preview does not write formal geometry or allocate permanent Object IDs.
13. Formal commits pass through the appropriate Domain Services.
14. Projection and geometry cache invalidation are verified.
15. A stale preview cannot silently overwrite newer authoritative state.

## 16. Decisions deliberately deferred

The following remain technical or product-detail gates, not blockers to the architecture:

- exact CRS/projection library and supported CRS catalogue;
- exact R-tree package/implementation;
- exact Bézier closest-point numerical algorithm;
- final default hit/snap pixel tolerances and accessibility settings;
- whether topology-aware snapping is included in the first editing toolset;
- exact formal geometry precision and coordinate serialization;
- exact segment-index granularity;
- advanced geodesic editing and measurement behavior;
- 3D spatial interaction (not part of this 2D V1 contract).

## 17. Final recommendation

Adopt this Spatial Presentation contract as the V1 baseline.

The key separation is:

- **Core Domain** says what the geometry is and what edits are valid.
- **Spatial Presentation** says where geometry appears and which geometry is near the pointer.
- **Interaction Controller** says what the user is trying to do.
- **Domain Services** decide whether the intended operation is valid and commit it.

This design supports fictional maps, real-world maps, dense city maps and detailed building/interior maps without confusing screen pixels, geometric proximity, topology and Object identity.