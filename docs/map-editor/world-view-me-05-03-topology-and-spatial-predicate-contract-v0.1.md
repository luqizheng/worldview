# World View — ME-05-03 Topology & Spatial Predicate Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 spatial-query contract; domain semantics and numerical policy baseline, not a frozen algorithm implementation or universal topology engine
- Depends on: ME-05-02 V0.1; ME-01-04/05/07; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-25 V0.1; Map Engine Spatial Presentation & Geometry Interaction Contract V0.1; specialist ME-02/03/04 contracts

## 1. Purpose

Define a small, reusable set of spatial predicates and validation outcomes for all specialist editors. The goal is reliable spatial reasoning without introducing a universal topology graph, a GIS standards clone or implicit semantic relationship creation.

**A spatial predicate answers a geometric question. It does not create a world fact.** Formal semantic relationships remain explicit Core Domain data governed by Relationship Type descriptors and typed operations.

V1 operates on 2D formal vector geometry in an explicitly declared coordinate context. Spatial queries are read-only. Any geometry produced by an analysis is derived/candidate data until a separately supported formal operation accepts it.

## 2. Three different concepts

| Concept | Question | Authority / persistence |
|---|---|---|
| Geometric predicate | Do these formal geometries intersect, contain, touch or remain disjoint? | Map Engine / Spatial Services; read-only |
| Domain topology constraint | Does this spatial arrangement satisfy a specialist domain rule, such as a valid river junction? | Specialist domain validation; result may block a typed operation |
| Semantic relationship | Does the world formally state that River A flows into River B, Road A connects to Road B, or Building A is accessed from Road B? | Core Domain typed Relationship |

Examples:
- Two river centerlines intersecting is a geometric fact; it does not establish a flow direction.
- Two road lines crossing is not enough to create a traversable junction.
- Two region polygons sharing a boundary is a geometric adjacency candidate; it does not establish administrative or political adjacency.
- A building footprint touching a road does not establish an entrance or access relationship.
- A snap preview does not create a shared node or relationship.

## 3. Query input and result contract

A spatial query must be evaluated against one immutable, explicitly identified source snapshot.

Required input context:
- Map identity and selected Map/Branch read context;
- scene/snapshot generation and source geometry fingerprints;
- Object IDs and Geometry Component IDs for all operands;
- each operand's spatial-reference identity, coordinate units, axis order and geometry family;
- requested predicate and declared boundary policy where relevant;
- explicit distance threshold and units for proximity queries;
- numerical robustness profile and any operation-specific error bound;
- algorithm/semantics version where persisted derived artifacts depend on reproducibility;
- cancellation/request token for obsolete interactive requests.

Result envelope:
- result classification: `True`, `False` or `Indeterminate` where numerical certainty cannot be established;
- predicate and boundary policy applied;
- source snapshot/generation and source fingerprints;
- coordinate/measurement context;
- diagnostics, uncertainty/error information and algorithm version;
- optional derived witness geometry (e.g. intersection points/segments) clearly marked non-authoritative.

Do not collapse `Indeterminate` into `False`. Do not convert missing geometry, invalid geometry, outside coverage or unsupported coordinate contexts into an ordinary negative predicate result; return a typed unavailable/invalid outcome with diagnostics.

## 4. V1 predicate vocabulary

The following definitions are the product-level contract. Concrete algorithms may use a robust computational-geometry library, but must preserve these semantics.

| Predicate | V1 meaning | Notes |
|---|---|---|
| `equals` | Same represented spatial point-set under the declared geometry semantics | Not byte equality, vertex-order equality or Asset-hash equality |
| `disjoint` | The two represented point-sets have no point in common | Symmetric |
| `intersects` | The two represented point-sets share at least one point | Includes touch, cross and overlap cases |
| `touches` | Boundaries intersect while interiors do not intersect | Boundary policy and geometry family must be declared |
| `crosses` | Their interiors intersect in a lower-dimensional result than at least one operand, under supported family pairing | V1 supports only explicitly implemented family pairs; unsupported pairs return Unsupported |
| `overlaps` | Same-dimensional interiors intersect, neither geometry fully covers the other, and the intersection is not merely a boundary touch | Symmetric; family-pair support is explicit |
| `within` | Every point of A lies in B's closure and A's interior has an interior point in common with B | Directional; inverse of `contains` under same policy |
| `contains` | Inverse of `within`; boundary-only contact does not by itself satisfy containment | Directional |
| `covers` | Every point of A lies in B's closure, including boundary-only inclusion | Directional; distinct from `contains` |
| `covered_by` | Inverse of `covers` | Directional |
| `boundary_intersects` | The formal boundaries share at least one point | Does not imply adjacency or connectivity |
| `point_on` | A point lies on the declared geometry boundary or path within the exact supported predicate semantics | Interior/boundary classification returned separately where applicable |
| `distance` | Minimum separation between two represented point-sets under a declared measurement model | Returns value + units + measurement model; not a topological predicate |
| `within_distance` | Distance is less than or equal to an explicit threshold in declared units | Proximity query only; does not imply adjacency |

V1 does not expose a general-purpose DE-9IM relation matrix or arbitrary user-defined spatial predicates. It may be considered later if concrete product workflows require it.

### 4.1 Boundary semantics

For polygonal geometry:
- interior excludes the polygon boundary;
- boundary includes exterior rings and interior-ring (hole) boundaries;
- holes are excluded from the polygon interior;
- the polygon closure includes its boundary but not the interior of holes.

A boundary-only contact is not an interior overlap. A point exactly on a boundary is not automatically classified as inside for every operation. The predicate's boundary policy must be explicit; default point-in-area UX should report `Boundary` separately from `Inside` and `Outside`.

For lines, the geometry model's declared endpoints and path semantics define boundary/interior classification. Multipart components are evaluated as the union of their member point-sets, while component identities remain separately addressable.

## 5. Adjacency and connectivity

### 5.1 Geometric adjacency candidate

For two valid polygonal regions, the reusable geometric adjacency candidate is:
- their boundaries share a non-zero-length segment;
- their interiors do not overlap;
- the shared segment is not merely a point contact.

Point-touching polygons may be reported as `touching-at-point`, not promoted to the default area-adjacency candidate. A distance threshold may produce a `nearby` candidate, never an adjacency fact.

This query returns candidate evidence only. It does not create a formal relationship.

### 5.2 Network connectivity

A line intersection is not a network connection. V1 network connectivity is determined by explicit domain endpoint/node semantics and registered relationship types, not by arbitrary coordinate coincidence.

A domain may use a spatial predicate as:
- a precondition (e.g. proposed junction locations must coincide within a declared domain tolerance);
- a validation diagnostic;
- a derived candidate generator;
- or not at all.

The Relationship Type descriptor declares which role topology plays. The shared spatial layer cannot infer a universal network rule.

### 5.3 No persistent universal topology graph

V1 does not add a universal persistent node/edge topology registry. Geometry Components and typed semantic relationships remain the formal source of truth. Map Engine may maintain disposable indexes or query accelerators. A specialist domain may define typed nodes/edges if its actual product semantics require them.

## 6. Numerical robustness and tolerance separation

The following tolerances are not interchangeable:

1. **Numerical robustness tolerance:** internal algorithm guard for floating-point uncertainty.
2. **Domain validation tolerance:** accepted geometric validity/precision rule defined by the geometry contract or Object Type.
3. **Hit-test tolerance:** pointer selection distance in logical viewport pixels.
4. **Snap tolerance:** maximum screen-space distance for proposing a snap.
5. **Proximity threshold:** explicit creator/domain distance threshold in map/measurement units.
6. **Measurement error bound:** accuracy declaration for a computed length, area or distance.

Rules:
- Never reuse one global epsilon for all six meanings.
- Hit-test and snap tolerances are evaluated in viewport logical pixels and converted conservatively through the current view transform by Map Engine.
- Snap tolerance does not alter formal geometry and does not establish topology.
- Topological predicates do not silently move coordinates or snap near vertices together. Use robust predicates over the stored precision model.
- If rounding/precision makes the classification uncertain, return `Indeterminate` with diagnostics.
- A creator-requested proximity threshold is explicit and unit-bearing; it must not be silently substituted for a topological predicate.
- Formal coordinate normalization is a separate, explicit geometry operation, not a side effect of query evaluation.

## 7. Coordinate-reference compatibility

Spatial predicates require operands to be in a compatible spatial reference and coordinate context.

- Same spatial-reference identity and compatible coordinate definitions may be evaluated directly.
- Different references require an explicit Map Engine transform into a declared common analysis space.
- A transform must validate its domain, units, axis order, wrap/discontinuity and precision behavior.
- Transforming for a query does not rewrite either formal geometry payload.
- If a reliable common analysis space cannot be established, return Unsupported/Indeterminate rather than compare raw coordinate tuples.
- LocalCartesian fictional maps are first-class; Earth geodesic assumptions must not be applied to them.

Planar distance/area/length calculations are only valid where the declared coordinate model supports those units and measurement semantics. Geographic coordinates require an explicit geographic measurement model and stated accuracy; ME-05-04 will define the shared measurement contract. Do not report square degrees as creator-facing area or degrees as ordinary distance.

## 8. Geometry validity diagnostics

V1 common diagnostics include:
- non-finite coordinate;
- unsupported geometry family/dimensionality;
- invalid or missing spatial-reference context;
- empty geometry where the Object Type requires a non-empty component;
- degenerate point/line/ring;
- unclosed polygon ring where closure is required by the canonical model;
- self-intersecting ring or invalid hole relationship;
- duplicate/consecutive vertices that collapse a segment under the declared precision model;
- invalid multipart component;
- out-of-domain coordinate or unresolved wrap/seam behavior;
- numerical predicate indeterminate.

A validity result is distinct from a semantic Object Type result. A structurally valid polygon may still violate a specialist rule (for example, a Lake crossing an incompatible land/water boundary). Specialist validation composes these outcomes without changing the shared predicate definitions.

Automatic repair is not a hidden validation behavior. A repair tool may create a Candidate geometry with before/after diagnostics; only a supported formal operation can publish it.

## 9. Specialist-domain application

| Domain | Shared spatial capability | Specialist rule remains owned by |
|---|---|---|
| Terrain / natural geography | polygon validity, line intersection, containment, distance, area candidate | ME-02: land/water, mountain/terrain regions, river/lake semantics, drainage |
| Settlements / urban geography | point-in-region, polygon adjacency candidates, line intersections and distance | ME-03: settlement placement, administrative/land-use relations, road junction/connectivity |
| Buildings / interiors | footprint containment, entrance-to-access proximity, local 2D containment | ME-04: building/site association, floor/room containment, vertical and underground connectivity |

The shared layer returns facts and diagnostics. It does not decide what a river, road, building entrance or administrative boundary means.

## 10. Freshness and interaction integration

- Every query result is bound to an immutable scene/snapshot generation.
- A result for an obsolete generation is discarded.
- Candidate geometry or hit-test references must be re-resolved before formal submission.
- Source fingerprints must still match at write time.
- Spatial index results are broad-phase candidates only; a narrow-phase evaluator applies the precise predicate.
- The Map Engine owns query scheduling, cancellation, index freshness, hit-test and snap mechanics. ME-05 defines domain meaning and result contract, not a competing interaction subsystem.

## 11. Failure and recovery

| Condition | Result |
|---|---|
| Missing formal geometry | Unavailable with reason; not False |
| Invalid geometry | Invalid with diagnostics; do not run unsupported predicate |
| Different incompatible spatial references | Unsupported or Indeterminate |
| Obsolete scene generation | Discard result |
| Query cancelled | No result; no formal mutation |
| Predicate numerically ambiguous | Indeterminate with error/precision evidence |
| Unsupported geometry-family pair | Unsupported with capability code |
| Derived witness geometry generated | Candidate/derived only |
| Snap candidate accepted in UI but source changed | Re-query; do not apply stale reference |

## 12. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| TP-001 | Two polygons share a non-zero-length edge, interiors disjoint | Geometric adjacency candidate |
| TP-002 | Two polygons touch at one point | Point-touch; not default area adjacency |
| TP-003 | Two polygons overlap in area | Intersects + overlaps |
| TP-004 | One polygon fully contains another | Contains/within according to direction |
| TP-005 | A point lies on polygon boundary | Boundary classification, not silently Inside |
| TP-006 | Two road lines cross without shared node semantics | Geometric intersection only; no network connection |
| TP-007 | Two river lines touch | No automatic flows-to relationship |
| TP-008 | Snap candidate within screen tolerance | Preview only; no formal coordinate or relationship change |
| TP-009 | Near but disjoint polygons under explicit threshold | Within-distance candidate; not intersects/adjacent |
| TP-010 | Polygon hole contains queried point | Point is Outside polygon interior |
| TP-011 | Same shape with different vertex ordering | Topological equals may be True; byte equality remains separate |
| TP-012 | Source precision makes orientation ambiguous | Indeterminate with diagnostic |
| TP-013 | Different CRS without common transform | Unsupported/Indeterminate |
| TP-014 | Query result from old scene generation | Discarded |
| TP-015 | Invalid self-intersecting polygon | Invalid; no ordinary predicate result |
| TP-016 | Derived intersection geometry shown in UI | Candidate only; no Core Domain write |

## 13. Status and next work

This contract defines shared V1 predicate vocabulary and topology boundaries. It does not freeze a geometry library, implement universal DE-9IM, create a persistent topology graph or authorize formal split/merge.

Next:
- ME-05-04 Coordinate Reference, Precision & Measurement Contract: CRS/local coordinate semantics, transforms, curve preservation, length/area/distance models and error declarations.
- ME-05-05 Geometry/Field Asset Package & Publication Integration: immutable package/tiling layout, manifests, content fingerprints, ownership, publication and recovery.
- ME-05-06 Map Engine / Core Domain Integration & Cross-architecture Verification.

## References

- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
- ME-01-07 Cross-architecture Consistency Review V0.1.
- P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- P2-09-20 Relationship Descriptor and Temporal Contract V0.2.
- P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- Map Engine Spatial Presentation & Geometry Interaction Contract V0.1.
- ME-02-B through ME-02-H specialist natural-geography contracts.
