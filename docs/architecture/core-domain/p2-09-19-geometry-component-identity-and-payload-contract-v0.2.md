# World View P2-09-19 — Geometry Component Identity and Payload Contract V0.2

- Version: 0.2
- Date: 2026-11-21
- Status: Command-catalog-reconciled recommended V1 vector-geometry contract — component identity, roles, immutable payload references and history boundaries; binary encoding and specialist 3D formats remain open
- Supersedes: P2-09-19 V0.1; V0.1 is retained as history
- Depends on: P2-09-02 V0.1; P2-09-06 V0.1; P2-09-08 V0.2; P2-09-11 V0.9; P2-09-15 V0.3; P2-09-17 V0.3; P2-09-18 V0.2; ME-01-05 V0.1; Spatial Presentation & Geometry Interaction Contract V0.1

## 1. Purpose

Define a bounded Geometry Component contract so Core Domain can preserve stable component identity, typed geometry roles, deterministic historical edits and immutable geometry payloads without introducing a general-purpose CAD/BIM model or a separate geometry database.

A Geometry Component is a formal spatial part of a semantic Object. It is not a semantic Object identity, a renderer node, a screen-space path, a raster cell or an AI Candidate.

This contract is a recommended V1 baseline. It does not freeze a binary geometry file format, a universal topology framework, arbitrary parametric CAD operations or full 3D/solid support.

## 2. Core decisions

1. Core Domain owns formal Geometry Component identity, membership, role, coordinate context, validity and historical applicability.
2. One semantic Object may own zero, one or multiple formal Geometry Components according to its Object Type.
3. A Geometry Component has a stable opaque identity scoped to one Object identity. It is not an Object ID and does not consume the Object Type allocator.
4. In-place adjustment/refinement preserves the Geometry Component identity.
5. Adding a component allocates a new component identity; removing a component ends its membership in the selected History state. Component identities are never reused.
6. Geometry split/merge and semantic Object split/merge are distinct. Geometry split/merge remain deferred until component lineage/mapping rules are designed.
7. Geometry payload bytes are immutable Assets. Formal references are owned by Core Domain and linked through existing typed Asset-reference tables.
8. Geometry changes in a Map/Branch History are History-local runtime changes. They do not edit the Project-level Shared Object current definition.
9. Project-level Shared Object base-geometry edits use the separate Shared Object definition Domain Service and follow the accepted retroactive-current-definition rule.
10. Projected geometry, render meshes, LOD, hit-test data, snap candidates and temporary previews are derived, disposable and non-authoritative.
11. V1 formal vector geometry uses a 2D working plane with explicit coordinate-reference context. 3D/solid geometry is deferred to specialist design.
12. Raster datasets are immutable spatial Assets/Datasets, not Geometry Components for every cell.

## 3. Geometry Component identity

### 3.1 Identity rules

- Each component receives a Core Domain-generated opaque ID when it is first formally created.
- Component ID is unique within its owning Object identity; globally unique UUIDs are recommended for simpler references.
- The same component ID remains stable through vertex/control-point edits, refinement, and geometry Asset replacement.
- Component ID is not derived from list position, geometry hash, role name, coordinates or display order.
- Component ID is never reused after removal.
- A component cannot be transferred to a different semantic Object by changing its owner field. Cross-Object transfer requires an explicit domain operation and is outside the minimum V1 set.
- A component may be referenced by multiple historical Commands as the same component evolves through the selected History.
- The component identity is not a separate formal Object and does not have its own Object ID reservation or lifecycle table.

### 3.2 Persistence without a new component table

V1 does not add a `core_geometry_component` table.

Component identity and metadata are stored inside the owning typed Core Domain payload:
- Project-shared current definition payload for base geometry components;
- Map Initial State payload for time-zero component membership/placement;
- Command payloads for post-zero History-local component additions, edits and removals.

The applicable immutable geometry bytes are stored as Core Assets and referenced through the existing typed link:
- `core_object_definition_asset`;
- `core_map_initial_object_asset`;
- `core_command_asset`.

This keeps one authority for each context and avoids creating a second component registry. A separate component table may be considered only if later query workloads prove that component-level relational queries are necessary and cannot be served by the typed payloads.

## 4. Geometry role and capability

### 4.1 Geometry role

Every formal component has exactly one declared role within its owning Object Type descriptor.

Examples include:
- point location / anchor;
- centerline / path;
- boundary / outline;
- footprint;
- ridge / crest;
- entrance;
- floor plan;
- elevation surface reference.

The examples are illustrative. The Object Type descriptor declares the roles it permits. Role IDs are stable, non-localized keys; display labels may be localized.

V1 does not permit a component's role to be silently changed by an ordinary geometry edit. A role change requires a separately validated domain operation and is deferred until a real use case requires it.

### 4.2 Geometry family and dimensionality

The shared V1 vector foundation should support:
- Point;
- LineString / connected line path;
- Bézier or equivalent first-class curve/path where supported by the geometry model;
- Polygon / area boundary;
- multipart forms of the above where permitted by the Object Type.

The Object Type descriptor constrains the allowed family, dimensionality, component count and role combinations.

V1 does not require a universal 3D solid, volumetric mesh, parametric CAD feature or unrestricted geometry collection. Structured interior geometry may use an explicit local 2D coordinate context; vertical/3D semantics remain ME-04/ME-05 specialist work.

### 4.3 Coordinate context

Every formal geometry payload must identify:
- stable spatial-reference/coordinate-definition identity;
- coordinate units and axis order;
- geometry family and dimensionality;
- coordinate precision/serialization schema version;
- wrap/discontinuity behavior where applicable;
- validity domain or explicit local-coordinate definition.

The map may use Geographic, Projected or LocalCartesian coordinates. Do not assume fictional-world coordinates are longitude/latitude or force an Earth datum onto a local map.

The projection pipeline, viewport coordinates and device pixels are not formal geometry coordinates. Screen-space values must never be persisted as authoritative domain geometry.

## 5. Representation categories and authority

A geometry representation must declare one of these categories:

| Category | Formal authority | May be used as reconstructed Object geometry? |
|---|---|---|
| Authoritative | Core Domain | Yes |
| Alternate authoring geometry | Core Domain, only where Object Type explicitly supports the role | Yes, for its declared role |
| Derived | Rebuildable service/cache | No, unless explicitly adopted through a formal Command |
| Candidate | AI/user staging | No, until validated and formally applied |
| Reference | External/imported evidence | No, until explicitly adopted |

Derived geometry must retain source fingerprints, method/version, parameters, coordinate context and provenance. Candidate geometry remains AI/user staging data until accepted through the normal Core Domain application boundary.

A raster dataset is a separate spatial Asset/Dataset with its own georeferencing, schema and provenance. Raster pixels do not become component identities or semantic Objects.

## 6. Immutable geometry payload contract

### 6.1 Asset unit

Recommended V1 asset granularity is one immutable Asset per formal Geometry Component payload.

This gives a clear component-level content hash and allows an in-place edit to publish a new Asset while preserving the component ID. A payload may bundle small internal structures required by the geometry family, but the Asset's declared media/format version must identify its encoding.

Do not bundle an entire Map scene, renderer mesh or unrelated Object components into one opaque mutable file.

### 6.2 Required logical manifest

The Core Domain typed owner payload must retain, at minimum:

| Field | Meaning |
|---|---|
| geometry_component_id | Stable component identity |
| geometry_role | Object-Type-declared role key |
| geometry_family | Point, line/curve, area or permitted multipart family |
| dimension | Supported coordinate dimension |
| spatial_reference_id | Exact domain coordinate definition |
| geometry_schema_version | Canonical geometry payload schema |
| asset_id | Immutable Core Asset reference |
| content_hash | Expected hash of exact geometry bytes |
| provenance | Authored/imported/refined source evidence as required |
| validity_metadata | Domain validation result/metadata required by the type |

The manifest is logical metadata. The exact binary/container representation is a technical decision and must not become a second metadata authority.

### 6.3 Canonical encoding and determinism

- Reject NaN, infinities, invalid coordinate arrays and unsupported geometry types.
- Use deterministic coordinate and structure serialization under an explicit geometry schema version.
- Preserve Bézier control points as formal geometry; screen-space flattening is derived only.
- Do not normalize or reorder coordinates in ways that change authored semantics unless the geometry descriptor explicitly defines that canonicalization.
- Ring orientation, closure, self-intersection and topology rules are validated according to the geometry family and Object Type descriptor.
- Store the exact published Asset hash in the owning typed link/payload evidence.
- An Asset is immutable after publication. Editing creates a new Asset; it never overwrites the old bytes.

The selected binary geometry format, precision quantization policy and geometry library remain open technical evaluation items. They must be chosen behind a stable Core Domain geometry payload contract.

## 7. Geometry Component operations

### 7.1 In-place adjustment

An adjustment:
- targets one existing component by stable ID;
- declares the component role and source geometry fingerprint;
- supplies the exact accepted new geometry Asset;
- preserves component identity and role;
- validates family, dimension, coordinate reference and Object Type constraints;
- changes only the selected Map/Branch History runtime state.

Replay uses the captured Asset and descriptor semantics. It does not rerun pointer interaction, snapping or an unseeded geometry algorithm.

### 7.2 Refinement

A refinement replaces an approximate/coarse component with an accepted refined component:
- preserves the component ID and role;
- records the source fingerprint;
- records method/version and all parameters/captured inputs needed for provenance;
- references the immutable refined geometry Asset;
- validates that the refined output is compatible with the Object Type and coordinate context.

If AI or an external service generated the refined output, replay consumes the accepted immutable Asset; it does not call that service again.

### 7.3 Add component

Adding a component:
- allocates a new component ID through Core Domain;
- validates that the Object Type permits the role and component cardinality;
- references one immutable geometry Asset;
- changes component membership only in the selected History;
- does not create a semantic Object.

This operation is recommended as a V1 Command descriptor because multipart/compound Objects need an explicit authoring path. It must not be implemented as generic JSON array insertion.

### 7.4 Remove component

Removing a component:
- targets one existing component ID;
- changes component membership only in the selected History;
- does not delete the immutable Asset bytes if they remain referenced by earlier history, another owner or checkpoint;
- validates minimum geometry requirements and Object Type invariants after removal;
- does not retire or delete the semantic Object.

The component ID remains consumed and is not reused.

### 7.5 Deferred split and merge

Geometry split/merge is deferred from the minimum V1 catalog.

Before enabling it, define:
- source-to-result component identity mapping;
- whether one source component retains identity and how other result IDs are allocated;
- provenance links between source and result components;
- deterministic handling of multipart geometry and component order;
- topology/validity rules for each family;
- edit/delete/replay behavior when later Commands reference affected component IDs.

A geometry split/merge never implies semantic Object split/merge. Semantic Object split/merge requires a separate Object Operation contract and Object ID reservation/lifecycle rules.

## 8. History, Shared Object and Checkpoint rules

- Initial State component membership and geometry references are part of the Map's time-zero baseline.
- Post-zero component add/adjust/refine/remove operations are Commands in the currently selected Map/Branch History.
- A History-local geometry edit to a Project-shared Object affects only that Map/Branch runtime state.
- A Project-level Shared Object base-geometry edit uses the Shared Object definition Domain Service and applies retroactively to all referencing Maps according to the accepted current-definition model.
- Branches inherit geometry-affecting Commands through the established closed fork boundary; Branch-owned geometry changes must occur strictly after `parent_at`.
- Geometry Command edits/deletes/insertions invalidate affected Checkpoints and rebuild from the earliest affected Historical Time.
- Checkpoint fingerprints include the exact immutable geometry Asset hashes and descriptor/handler semantics that affect reconstruction.
- Derived render geometry, projection caches, LOD, hit-test indexes and snap candidates are excluded from authoritative history and Checkpoint inputs unless a specific derived artifact is explicitly adopted as formal input.

## 9. Typed Asset reference matrix

| Owner context | Existing typed link | Geometry use |
|---|---|---|
| Project-shared current Object definition | `core_object_definition_asset` | Base/definition geometry component Assets |
| Map Initial State | `core_map_initial_object_asset` | Time-zero component membership and geometry Assets |
| Post-zero Command | `core_command_asset` | Exact geometry input/result Assets required by the Command |

The payload manifest carries component identity and role; the typed link carries relationally verifiable Core Asset identity and owner context. Both must agree under Core Domain validation.

V1 should enable `core_command_asset` because the recommended geometry Command descriptors consume immutable formal geometry Assets. The remaining two links are enabled only when the corresponding Object definition/Initial State schemas require formal geometry Assets.

No generic polymorphic owner/asset link is introduced.

## 10. Physical schema impact

P2-09-11 V0.9 provides:
- immutable `core_asset` metadata;
- `core_object_definition_asset`;
- `core_map_initial_object_asset`;
- conditional `core_command_asset`;
- `core_command_object_ref`.

P2-09-11 V0.9 now records:
1. `core_command_asset` as required by the enabled V1 geometry descriptors;
2. component identity/role metadata in typed owner payloads rather than a new component table;
3. geometry schema version and exact Asset hashes in canonical payload/Checkpoint fingerprints;
4. pinned Object Type semantics identifiers from P2-09-18 in relevant reconstruction fingerprints;
5. binary format selection and provider-specific validation as explicit technical gates.

This contract does not authorize executable DDL or implementation.

## 11. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| GEO-01 | Component edited in place | Same component ID and role; new immutable Asset |
| GEO-02 | Component Asset bytes changed after publication | Integrity failure; no in-place overwrite |
| GEO-03 | Component ID derived from list index or geometry hash | Rejected as unstable identity |
| GEO-04 | Add component to a type that disallows the role/cardinality | Rejected |
| GEO-05 | Remove final required component | Rejected by Object Type invariant |
| GEO-06 | Remove component still referenced by prior history | Current membership changes; prior history and Asset remain reconstructable |
| GEO-07 | Geometry split operation submitted as semantic Object split | Rejected/misclassified |
| GEO-08 | Geometry split/merge changes semantic Object count | Prohibited |
| GEO-09 | Candidate geometry used as formal Command Asset | Rejected until published/adopted |
| GEO-10 | Geometry uses screen-space coordinates | Rejected |
| GEO-11 | Coordinate reference differs from target Map/role | Rejected unless an explicit validated transform is part of the operation |
| GEO-12 | Project-shared base geometry edit | Uses Shared Object definition service and invalidates all referencing Map/Branch Checkpoints |
| GEO-13 | History-local geometry edit on one Map | Other Maps' Shared Object runtime geometry remains unchanged |
| GEO-14 | Branch-owned geometry Command at/before parent_at | Rejected |
| GEO-15 | Same geometry Command replayed with same handler semantics and Assets | Identical formal geometry state |
| GEO-16 | Derived LOD/render mesh changes | No formal Command or World State change |

## 12. Remaining design gates

1. Select the canonical V1 geometry payload container/encoding and precision policy through a bounded technical evaluation.
2. Define exact coordinate-reference identity and serialization integration with the Map spatial-reference contract.
3. Finalize Object Type geometry role/cardinality declarations for the first built-in type set.
4. Use the registered `core.geometry.add_component` and `core.geometry.remove_component` descriptors and payload schemas in P2-09-15 V0.3 / P2-09-17 V0.3.
5. Confirm the minimum typed Asset links required by Object definition, Initial State and Command descriptors.
6. Defer geometry split/merge until component lineage and identity mapping rules are approved.
7. Keep full 3D/solid geometry and advanced topology outside V1 until ME-04/ME-05 domain requirements are complete.

## 13. Status

P2-09-19 V0.2 establishes stable Geometry Component identity, typed roles, immutable Asset references, History-local edits and a minimal V1 operation boundary. It deliberately avoids a new component table, a generic CAD model and speculative split/merge semantics.

It is a design contract, not an executable geometry file format, SQL migration, implementation code, provider test report or implementation authorization.
