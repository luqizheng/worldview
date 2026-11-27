# ARCH-01 Decision Gate — Recommended V1 Authoring Defaults

- Version: 0.1
- Date: 2026-11-27
- Status: Decision proposal — awaiting product-owner confirmation
- Baseline: master `4f456bfd444e2e0cb6d59b167de8d6a06c0e66dd`
- Parent audit: [ARCH-01](ARCH-01-cross-domain-dependency-and-reconciliation-v0.1.md)
- Product authority: [PRODUCT-01](../product/PRODUCT-01-v1-scope-and-acceptance.md)

This addendum narrows the explicit non-decisions left by ARCH-01. It does not change runtime behavior or authorize implementation. The recommendations minimize new domain concepts, reuse the existing descriptor catalog and preserve the product's map-first workflow.

## 1. First V1 authoring subset

**Recommendation: adopt exactly three existing built-in Object Types for the first authoring slice.**

| User-facing geometry | Existing Object Type | Formal geometry role | Existing declared family | Why this is the smallest coherent mapping |
|---|---|---|---|---|
| Point | `point_of_interest` | `location` | `point` | One required point component; no elevation/vertical datum requirement |
| Line | `river` | `centerline` | `line_string` / `multi_line_string` | One or more required centerline components; no assumption that vertex order defines flow direction |
| Area | `landmass` | `extent` | `polygon` / `multi_polygon` | One or more required polygonal extents; multipart geometry is explicitly allowed |

Reasons to prefer these over a new generic Point/Line/Polygon Object Type:
- All three are already present in the Core Domain V0.2 built-in descriptor catalog and project seed list.
- The catalog explicitly declares the required geometry roles, cardinality and geometry families.
- `mountain_peak` is a valid point alternative, but its definition requires elevation, unit and vertical-reference metadata. It introduces a vertical-datum dependency that is unnecessary for the first general point tool.
- Creating generic UI-defined identities would contradict PRODUCT-01's explicit prohibition on invented generic Object identities.

V1-A should initially expose only the required geometry role for each selected type. Do not expose river reaches, additional component roles, relationships, split/merge, hydrology, elevation or specialist validation in this slice. Definition fields should use the already declared typed schema; no arbitrary attributes.

## 2. Default Map Spatial Reference

**Recommendation: every new Map receives an immutable 2D Local Cartesian Spatial Reference at creation.**

Proposed profile:
- Kind: `LocalCartesian`
- Dimension: `2`
- Axis order: `XY`
- Coordinate unit: `world-unit` (explicitly abstract; not metres, feet, degrees or a promise of physical measurement)
- Wrap behavior: `None`
- Vertical reference: absent
- Origin: Map-local origin; screen pixels are never stored as geometry
- Precision: a named, versioned decimal precision profile; numeric tolerances and valid extent are frozen by DESIGN-EXTENT / PRA-03 before authoring is enabled
- Provenance: built-in World View default profile, versioned and fingerprinted in the Project

Why:
- World View targets fictional and historical worlds; a Map should not silently assume WGS84, longitude/latitude or a real-world projection.
- A local Cartesian frame is the least-assumptive correct 2D space for the first map-editor slice.
- An abstract world-unit prevents incorrect length/area claims. Real-world metres, feet and geographic CRS can be selected through later explicit map-creation profiles.
- The definition remains immutable and project-scoped; changing it after geometry exists requires a separately designed coordinate conversion, never an in-place mutation.

The default profile ID, numeric tolerance, valid coordinate domain and display/measurement wording must be finalized in DESIGN-EXTENT. Do not guess them in the UI.

## 3. Temporal write path — critical product decision

The existing Core Domain contract distinguishes:
- Map Initial State: facts at time zero, edited by a baseline service and explicitly **not** by a post-zero Command;
- Map/Branch History: post-zero changes, persisted as typed Commands and reconstructed deterministically.

The current Map creation workflow establishes a day-zero Calendar Configuration. The PRODUCT-01 manual authoring journey does not yet say whether a new map is authored at time zero or at a later historical time. This choice changes the formal write model and Undo/Redo design.

**Recommendation: the first V1 authoring workflow edits the Map's time-zero Initial State.** This best matches a map-creation/editor product: users are constructing the starting map, not implicitly advancing its world timeline.

However, the existing Initial State implementation is a mutable compare-and-swap full-set baseline. It does not provide formal undo/redo or a baseline revision history. Therefore, if this recommendation is accepted, FOUNDATION-01 / the owning Core Domain package must design a Core-Domain-owned, durable and reversible baseline-edit operation model before V1 claims edit/undo/redo. It must:
- record each accepted baseline edit as an immutable revision or equivalent formal operation;
- support deterministic undo/redo without writing UI-only state or pretending a time-zero edit is a historical Command;
- preserve atomicity across geometry Asset publication, Object identity/registration and baseline membership;
- retain stable identities and immutable assets; undo removes/reverts membership, not identity allocation history;
- recover the active baseline and revision cursor after close/reopen;
- reject stale revision application and failed publication without partial formal state.

This is not currently implemented and is not authorized by existing Initial State CAS alone.

Alternative: choose a non-zero authoring time and route every create/edit/delete through typed Commands in the root History. This reuses the historical Command model more directly, but changes the initial editing semantics, requires an explicit default historical time and still requires the missing Command execution/replay implementation. Do not mix the two paths implicitly.

## 4. Layer organization

**Recommendation: initial V1 layer/group membership, ordering and visibility are workspace-local Presentation state**, not formal Core Domain facts, unless the product later requires them to be shared by maps, histories or exports as authored semantics.

- Keep Object existence and semantic relationships in Core Domain.
- Keep visibility, ordering, expanded/collapsed state and active layer selection in the Map Workspace presentation model.
- Persist workspace preferences locally only if the existing workspace persistence boundary supports it; do not add Core Domain tables for a cosmetic layer panel.
- A layer is not an Object Type, a semantic relationship or a second ownership registry.
- E2E-06 requires at least two usable groups/layers, hide/show and deterministic ordering, but not a generic GIS layer-management framework.

## 5. Decision request

Please confirm or amend these four items before FOUNDATION-01 crosses the formal write-model boundary:

1. **Object Types:** accept `point_of_interest/location`, `river/centerline`, `landmass/extent` as the first point/line/area set?
2. **Spatial Reference:** accept the 2D Local Cartesian `world-unit` default, with exact precision/extent finalized in DESIGN-EXTENT?
3. **Temporal authoring:** accept time-zero Initial State editing, with a Core-Domain-owned durable baseline revision/undo-redo model required before V1 acceptance? Or choose non-zero History Commands instead?
4. **Layers:** accept workspace-local Presentation state for layer membership/order/visibility in the first V1 slice?

Until these are confirmed, safe preparatory work may continue, but schema/API choices that encode these semantics must remain unimplemented.
