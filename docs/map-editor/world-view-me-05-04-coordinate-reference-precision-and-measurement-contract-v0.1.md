# World View — ME-05-04 Coordinate Reference, Precision & Measurement Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 coordinate and measurement contract; exact numeric profiles and implementation libraries remain validation gates
- Depends on: ME-05-02 V0.1; ME-05-03 V0.1; P2-09-19 V0.2; P2-09-25 V0.1; Map Engine Spatial Presentation & Geometry Interaction Contract V0.1; ME-01-04 V0.1

## 1. Purpose

Define how World View interprets, transforms, stores and measures formal coordinates without assuming that every Map is an Earth longitude/latitude dataset.

The governing rules are:

1. Every formal Geometry Asset has an explicit immutable spatial-reference identity.
2. Domain coordinates, projected Map coordinates, viewport logical pixels and device pixels are distinct typed spaces.
3. Coordinate-reference changes require explicit conversion and new immutable geometry Assets; they never reinterpret old coordinate tuples in place.
4. Precision is selected per spatial reference and authoring scale, not by one global decimal-place count.
5. Curves remain formal geometry; flattening is a derived presentation approximation.
6. Measurements must declare their coordinate/measurement model, units and error bound. Unsupported or ambiguous results are reported, not guessed.

This contract is a shared service boundary, not a mandate to use a particular GIS library.

## 2. Spatial-reference model

A spatial reference is an immutable, project-scoped definition with a stable ID. It declares:

- reference kind: `Geographic`, `Projected` or `LocalCartesian`;
- coordinate unit and axis order;
- origin and basis/transform parameters for local coordinates;
- datum, ellipsoid and projection parameters where applicable;
- valid coordinate domain;
- wrap/periodicity/discontinuity behavior;
- supported dimensionality and optional vertical reference;
- measurement capabilities (distance, area, direction, scale) and their limitations;
- definition version and provenance.

EPSG identifiers may be recognized as external aliases but are not required. The complete World View definition must remain locally available and immutable once referenced by formal geometry.

A Map's spatial reference is domain metadata, not a renderer or viewport preference. Changing it is a domain-level conversion/migration with validation and provenance.

### 2.1 Map Space is not always Earth space

- **LocalCartesian** is the default suitable choice for fictional maps that have no Earth-geographic meaning.
- **Projected** is suitable when the Map uses a defined planar coordinate system and unit.
- **Geographic** is suitable when the Map intentionally represents a planet or region using angular coordinates and a declared datum/ellipsoid.

Do not attach an Earth datum, infer north, or use longitude/latitude distance formulas for a local fictional coordinate system.

## 3. Typed coordinate pipeline

| Coordinate space | Meaning | Owner |
|---|---|---|
| Domain Coordinates | Authoritative Geometry Component tuples in the declared spatial reference | Core Domain |
| Map Coordinates | Coordinates in the current 2D working projection/plane | Map Engine / Spatial Services |
| Viewport Coordinates | Logical pixels relative to the map viewport | Editor interaction |
| Device Coordinates | Physical pixels after DPI scaling | Renderer backend |

Public APIs should use distinct types (e.g. `DomainPoint`, `MapPoint`, `ViewportPoint`, `DevicePoint`) or equivalent wrappers. A transform declares its input and output types. No implicit casts or generic untyped `double x,y` interchange across boundaries.

Projection and viewport transform are separate:
- Projection maps Domain Coordinates to the working Map plane and may be nonlinear.
- Viewport transform applies pan, zoom and rotation.
- DPI scaling converts logical viewport pixels to physical device pixels.

Camera movement and DPI changes do not alter formal geometry or its coordinate reference.

## 4. Coordinate transformation contract

The replaceable projection/transform service must provide:
- explicit forward and inverse transformations;
- source and target spatial-reference identities;
- deterministic definition/version identity for cache keys;
- declared units and axis behavior;
- validity domain and explicit transform failure;
- wrap/discontinuity behavior;
- measurement capability declarations;
- optional conservative error bounds where the transform is approximate.

Rules:
1. No silent clamping, invented coordinates or silent dropping of invalid vertices.
2. A transform outside its valid domain returns a typed failure/diagnostic.
3. Different spatial references may be compared only after an explicit transform into a declared common analysis space.
4. A query-time transform does not rewrite either source Asset.
5. A formal conversion creates a new immutable Geometry Asset and a typed formal operation in the selected authority context.
6. A spatial-reference definition change creates a new definition identity/version. Existing Assets retain the old identity and interpretation.
7. If conversion fails for any coordinate/segment, the operation fails as a whole unless a separately specified partial-conversion workflow explicitly exists.

### 4.1 Wrap and discontinuity

For periodic references:
- transforms identify wrap discontinuities;
- rendered paths are split at discontinuities rather than drawing long chords across the map;
- broad-phase bounds must not expand to the entire world merely because a short segment crosses the seam;
- inverse transforms preserve the intended wrap branch where possible;
- editing across a discontinuity may be disabled where continuity cannot be guaranteed, with an explicit diagnostic.

Antimeridian handling is relevant to Earth geographic Maps; analogous wrap rules may apply to custom periodic fictional worlds only when explicitly declared.

## 5. Precision and canonical serialization

### 5.1 Per-reference precision profiles

Do not use a single global decimal-place count. Each spatial reference selects a versioned precision profile that declares:
- coordinate unit and axis;
- numeric representation;
- maximum absolute quantization error;
- rounding mode;
- overflow and valid-domain checks;
- minimum supported authoring tolerance and smallest meaningful feature scale.

For LocalCartesian Maps, derive the profile from declared world extent and smallest supported feature size. P2-09-25 recommends quantization error no greater than one tenth of the minimum supported authoring tolerance. Exact values must be established by representative fixtures and platform tests, not guessed.

For Geographic references, angular precision must also declare maximum ground error over a stated latitude/domain; fixed decimal places do not have uniform metric meaning.

### 5.2 Authoritative serialization

The recommended V1 formal Asset representation is the versioned canonical World View JSON envelope from P2-09-25:
- canonical UTF-8;
- ordinal object-key ordering;
- invariant decimal serialization;
- no NaN or Infinity;
- stable array order unless the schema explicitly declares an order-insensitive collection;
- exact canonical bytes hashed by the Core Asset hash algorithm/version.

The envelope identifies format/version, spatial-reference ID, axis order, coordinate unit, dimension, geometry family, precision-profile ID, geometry body and optional provenance. Mutable Object/Component metadata remains in the Core Domain owner payload, not duplicated in the Asset.

This remains a recommended encoding direction pending serialization fixtures and Windows/macOS determinism evidence. It is not a frozen binary or optimized renderer format.

### 5.3 Precision categories must remain separate

| Category | Purpose | Must not be reused as |
|---|---|---|
| Storage/quantization precision | Canonical formal coordinate representation | Snap radius or render tolerance |
| Domain validation tolerance | Formal geometry invariants | Numerical epsilon |
| Numerical robustness guard | Stable computational predicates | User-visible acceptance threshold |
| Render approximation tolerance | Curve/path flattening deviation | Formal coordinate precision |
| Hit-test tolerance | Pointer selection in logical pixels | Map-unit distance |
| Snap tolerance | Screen-space snap proposal radius | Topology or domain validation |
| Measurement error bound | Declared uncertainty of length/area/distance | Geometry editing tolerance |

No hidden rounding, snapping or coordinate normalization is allowed as a side effect of serialization, projection, measurement or rendering.

## 6. Curves and geometry preservation

- Line and cubic Bézier segments are formal authoring geometry where supported by the geometry schema.
- Segment order and authored path direction are preserved.
- Unknown curve segment types are rejected rather than silently linearized.
- Tessellation/flattening is view-dependent derived data used for rendering, broad-phase bounds and interaction only.
- A formal conversion or simplification must publish a new Asset and retain provenance; it must not overwrite the original curve bytes.
- River centerline digitization direction is not automatically hydrological flow direction; semantic flow remains an explicit domain relationship.

V1 editing remains a 2D working plane. Vertical reference, 3D precision and solid geometry require a separate ME-04/ME-05 extension and are not implied by a 3D coordinate field in an Asset envelope.

## 7. Measurement service

Measurement is a read-only Map Engine / Spatial Services capability over immutable source geometry. It returns derived values, not formal Object attributes or history changes.

Each measurement result declares:
- measurement kind;
- source Object/Component IDs and Asset fingerprints;
- spatial-reference identity and measurement model;
- output value and unit;
- algorithm/semantics version;
- numerical error bound or quality classification;
- source snapshot/generation and cancellation token.

### 7.1 V1 measurement operations

| Operation | Definition | Capability requirement |
|---|---|---|
| Point-to-point distance | Distance between two points under declared model | Reference supports distance; model/units explicit |
| Geometry minimum distance | Minimum separation between two geometries | Compatible geometry and supported measurement model |
| Line length | Length along the formal path, including curves | Curve-aware measurement or declared bounded approximation |
| Polygon area | Interior area; holes subtract from exterior area | Valid polygon and supported area model |
| Polygon perimeter | Sum of declared boundary lengths, including hole boundaries where requested | Explicit boundary inclusion rule |
| Bearing/direction | Direction in a declared reference frame | Only when reference defines meaningful direction/north |

Measurement outputs are approximate numeric results with explicit error/quality metadata where the algorithm is approximate. They are never silently written back into a formal Object field. If a creator wants to preserve a measured value as a world fact, that is a separate typed attribute operation with provenance.

### 7.2 Measurement models

- **LocalCartesian:** planar Euclidean length/distance and planar area in declared units, provided the map definition states those measurements are meaningful.
- **Projected:** planar measurements are allowed only when the projection's declared distortion/validity characteristics satisfy the requested operation and accuracy.
- **Geographic:** raw angular-coordinate Euclidean distance/area is prohibited. Use a declared ellipsoidal/geodesic measurement implementation with stated accuracy, or return Unsupported.
- **Large extent / wrap / pole cases:** use an appropriate geodesic or partitioned model; if the selected service cannot guarantee a valid result, return Unsupported/Indeterminate.

The service must not automatically select a measurement model based solely on the visible viewport projection.

### 7.3 Curve measurement

Line segments may be measured directly in the selected measurement model. Bézier curve length generally requires numerical evaluation; the service must declare its integration/approximation method and error bound. A curve must not be flattened at an arbitrary renderer tolerance and reported as an exact length.

Polygon area involving curved boundaries must use a curve-aware area method or explicitly declare an approximation and error bound. Unsupported curve-area cases return Unsupported rather than a misleading exact value.

## 8. Result and error vocabulary

Common outcomes:
- `Success`: result meets declared semantics and accuracy requirement;
- `Approximate`: result is usable within declared error bound;
- `Unsupported`: required measurement/transform capability is not available;
- `OutsideDomain`: coordinate lies outside the declared valid transform domain;
- `Indeterminate`: numeric classification or error cannot satisfy the requested confidence;
- `InvalidInput`: malformed geometry, missing reference or invalid unit/context;
- `Cancelled` / `StaleSnapshot`: no current result is produced.

A numeric value without units, source snapshot, model and quality/error metadata is not a valid measurement result.

## 9. Integration and cache invalidation

Cache keys for projected geometry include source Geometry Component identity/fingerprint, spatial-reference identity, projection definition/version and wrap policy. Screen-space tessellation additionally includes viewport transform and render approximation tolerance (and device scale only when the artifact is device-resolution-specific).

Measurement cache keys include source Asset fingerprints, measurement model, algorithm/version, parameters and requested accuracy. A source Asset, spatial-reference definition, precision profile or algorithm change invalidates affected derived measurements. Camera pan/zoom alone does not invalidate formal measurements.

## 10. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| CRS-001 | LocalCartesian fictional Map | No Earth datum/longitude assumption introduced |
| CRS-002 | Same geometry with different CRS identity | Raw coordinate comparison rejected until explicit common transform |
| CRS-003 | Change CRS definition | New identity/version; existing Assets retain old interpretation |
| CRS-004 | Formal geometry conversion | New immutable Asset and explicit provenance |
| CRS-005 | Transform outside validity domain | Typed failure; no clamping or invented coordinate |
| CRS-006 | Antimeridian-crossing path | Correct wrap/discontinuity representation; no world-spanning chord |
| CRS-007 | Change viewport zoom/DPI | Formal coordinates and measurement values unchanged |
| CRS-008 | Curve render flattening | Formal Bézier control points preserved |
| CRS-009 | Unknown curve segment | Rejected; not silently linearized |
| CRS-010 | Precision profile selection | Error bounded by profile and smallest feature fixture |
| CRS-011 | Serializer Windows/macOS | Canonical byte/hash behavior verified or compatibility issue explicitly documented |
| CRS-012 | Local Cartesian length/area | Units and planar model explicit |
| CRS-013 | Geographic raw-coordinate distance request | Rejected; geodesic model required |
| CRS-014 | Bézier length | Curve-aware result with declared error bound |
| CRS-015 | Curve area unsupported by selected method | Unsupported, not false precision |
| CRS-016 | Old measurement after source Asset change | Stale/invalidate by source fingerprint |

## 11. Open technical gates

1. Determine actual per-reference precision profiles from representative World, Region, Locality and Site fixtures.
2. Validate canonical JSON serializer byte stability across supported .NET runtimes on Windows/macOS.
3. Benchmark geometry parse/validation and memory use for large World and detailed Site workloads.
4. Select/validate a projection and geometry computation implementation that supports required coordinate contexts without owning formal domain authority.
5. Prove curve length/area error bounds for the chosen implementation.
6. Confirm geodesic measurement capability and failure behavior for geographic Maps.
7. Validate wrap/seam and transform-domain fixtures.

No projection library, geometry package, decimal precision, binary format or renderer technology is frozen by this contract.

## References

- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- Map Engine Spatial Presentation & Geometry Interaction Contract V0.1.
- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
