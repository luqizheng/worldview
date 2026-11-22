# World View — ME-05-02 Shared Geometry Model & Component Operations

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 shared geometry domain contract; aligned with current Core Domain descriptors, not a frozen ADR, executable API or implementation authorization
- Depends on: ME-01-04/05/06/07; ME-05-01 V0.3; P2-09-15 V0.5; P2-09-17 V0.5; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-25 V0.1; Map Engine Spatial Presentation & Geometry Interaction Contract V0.1

## 1. Purpose

Define the shared vector-geometry model and its formal lifecycle across all specialist editors. ME-05-02 consolidates and applies existing Core Domain contracts; it does not create a second geometry registry, a generic geometry CRUD API or a general-purpose GIS/CAD model.

The governing rule is:

**Geometry Component identity belongs to a semantic Object, while its geometry payload is an immutable Asset. A geometry edit changes the spatial representation in one explicit authority context; it does not implicitly change semantic Object identity or world relationships.**

## 2. Ownership and concepts

| Concept | Meaning | Formal authority |
|---|---|---|
| Semantic Object | Stable world fact, identified by Object ID and Object Type | Core Domain |
| Geometry Component | Typed spatial part belonging to one semantic Object | Core Domain |
| Component role | Stable Object-Type-declared meaning such as footprint, centerline, boundary or entrance | Object Type descriptor |
| Geometry payload | Canonical coordinates and geometry-family data for one component state | Immutable Core Asset |
| Geometry reference | Typed link from the owning definition, Initial State or Command to an Asset | Core Domain |
| Render/interaction representation | Projected paths, tessellation, LOD, hit-test and snap data | Map Engine; derived |
| Candidate geometry | Uncommitted user/AI proposal or temporary editing result | Editor / AI staging |

A Geometry Component is not a semantic Object, renderer node, screen-space path, raster cell or AI Candidate. A Field Dataset (ME-05-01) is a separate Map-owned source field, not a Geometry Component for every raster cell.

## 3. Component identity and lifecycle

The V1 identity rules follow P2-09-19:

1. Core Domain allocates an opaque component ID at first formal creation.
2. Component identity is stable through vertex/control-point edits, refinement and geometry Asset replacement.
3. Identity is not derived from list position, geometry hash, role, coordinates or display order.
4. Component IDs are never reused after removal.
5. A component cannot be transferred to another semantic Object by changing an owner field. Cross-Object transfer requires a separate explicit domain operation and is outside the minimum V1 set.
6. One Object may own zero, one or multiple components according to its Object Type descriptor.
7. Each component has exactly one declared role in its owning Object Type. Ordinary geometry editing cannot silently change that role.
8. Component membership and payload are interpreted in the selected formal authority context; there is no global mutable component state shared by all Maps.

### 3.1 No independent component table in V1

V1 does not add `core_geometry_component`.

Component identity and metadata are stored in the owning typed Core Domain payload:
- Project-shared current definition payload for base geometry components;
- Map Initial State payload for time-zero membership and placement;
- Command payloads for post-zero History-local additions, edits and removals.

Immutable geometry bytes are stored as Core Assets and referenced through existing typed links:
- `core_object_definition_asset`;
- `core_map_initial_object_asset`;
- `core_command_asset`.

This avoids duplicating authority in a second registry. A separate query projection or index may be introduced only if measured workloads prove that typed-payload reconstruction is insufficient; it must remain derived and rebuildable, not a second write authority.

## 4. Geometry families and coordinate context

The shared V1 vector foundation supports, subject to Object Type capability:
- Point;
- LineString / connected line path;
- Polygon / area boundary;
- first-class Bézier or equivalent curve/path where the canonical geometry model supports it;
- multipart forms where explicitly permitted by the Object Type.

Every formal geometry payload declares stable spatial-reference identity, coordinate units and axis order, family/dimensionality, precision and serialization schema version, and wrap/discontinuity or validity-domain behavior where applicable.

V1 formal vector editing is a 2D working plane. Geographic, Projected and LocalCartesian coordinate definitions are valid; fictional-world maps must not be forced into Earth longitude/latitude. Screen logical pixels and device pixels are never formal geometry coordinates. Projection and viewport conversion are explicit service operations.

3D solids, volumetric meshes, unrestricted parametric CAD and universal geometry collections are deferred to specialist design.

## 5. Formal authoring contexts

Every write must select exactly one authority context before validation:

| Context | Formal write path | Meaning |
|---|---|---|
| Project-level Shared Object definition | Shared Object definition Domain Service | Changes the shared base definition under the accepted retroactive-current-definition rule |
| Map Initial State | Map Initial State authoring service | Establishes time-zero geometry for that Map |
| Post-zero Map/Branch History | Registered Core Domain Command | Changes runtime geometry only in the selected History |
| Source Field Dataset | ME-05-01 Field publication service | Updates Map-wide source-field baseline; not vector geometry history |

A generic Save/Apply operation must not silently choose the context. The Editor must show the creator which scope is being changed.

### 5.1 Shared Object and Map-local runtime state

- A Project-level Shared Object base-geometry edit changes its current shared definition for all Maps that reference it, subject to the shared-definition contract.
- A post-zero Command that adjusts a shared Object's geometry changes only that selected Map/Branch runtime state.
- Other Maps' runtime geometry is not modified by a Map-local Command.
- Only the activated Map is interactive; inactive Maps remain static references and are not implicitly started or mutated.
- A Shared Object's shared definition and its per-Map historical runtime geometry must never be conflated into one mutable geometry blob.

### 5.2 History and replay

Post-zero geometry Commands belong to exactly one History and obey Branch fork-boundary rules. Replay uses captured immutable geometry Asset references, typed payloads and pinned descriptor semantics. It does not rerun pointer gestures, snapping, AI, projection or an unseeded geometry algorithm.

A formal write re-resolves Object and Component references, checks expected source fingerprints, validates the exact target context and only then commits through Core Domain.

## 6. V1 geometry operations

The recommended command set is the one already recorded in P2-09-15:

| Command Type ID | Intent | V1 disposition |
|---|---|---|
| `core.geometry.adjust_component` | Adjust vertices/control points of one formal component | Include as bounded History-local edit |
| `core.geometry.refine_component` | Adopt a refined geometry for an existing component | Include only with explicit source/target role and provenance |
| `core.geometry.add_component` | Add a new component to an existing Object | Include subject to Object Type role/cardinality validation |
| `core.geometry.remove_component` | Remove one component from selected History state | Include subject to minimum-geometry and reference validation |
| `core.geometry.split_component` | Split one component into multiple components | Deferred |
| `core.geometry.merge_components` | Merge compatible components | Deferred |

Rules:
- A formal Command identifies the semantic Object and exact component(s); free-floating geometry is not a formal Command.
- Add allocates a new opaque component ID through Core Domain.
- Adjust/refine preserves component ID and role.
- Remove ends component membership in the selected History but does not retire the Object or delete immutable Asset bytes still referenced by history/checkpoints/audit.
- Split/merge are not emulated by chaining add/remove. Their identity lineage and topology semantics must be designed before formal support.
- No generic `replace_geometry` CRUD Command is introduced.

## 7. Validation gates

Validation is layered and owned by the correct subsystem:

1. **Payload:** supported family, finite coordinates, canonical schema, coordinate context, precision and Asset integrity.
2. **Component:** ID uniqueness, owner Object, role, membership and lifecycle.
3. **Object Type:** allowed family, required/optional roles, cardinality and cross-component constraints.
4. **Spatial:** family-specific structural validity, degeneracy, ring closure, self-intersection policy, bounds and declared precision/tolerance.
5. **History:** target Map/Branch, fork boundary, descriptor schema/semantics compatibility and source fingerprint freshness.
6. **Authorization:** actor capability, operation scope and required human confirmation.
7. **Specialist domain:** river, road, land/water, building access or interior constraints from the relevant specialist contract.

A renderer-valid path is not necessarily a formally valid geometry. Presentation may simplify or tessellate, but cannot normalize or silently repair the formal payload.

## 8. Asset and publication lifecycle

- Formal geometry payloads are immutable Core Assets.
- Core Domain owns typed Asset references and their lifecycle; Map Engine may decode/read but cannot rewrite published bytes.
- Publication stages and validates payload, writes and verifies immutable bytes, then commits typed references and formal history metadata through the owning Domain Service/transaction.
- If metadata commit fails after Asset write, no formal reference is created; the unreferenced Asset is reconciled safely.
- Existing Assets remain available while referenced by any formal definition, Initial State, Command, Checkpoint or audit evidence.
- Exact package, tiling, compression and random-access encoding remain open technical gates; this document does not freeze a binary layout.

## 9. Derived geometry and interaction

The following are derived and disposable: projected geometry, curve tessellation, clipping, LOD, render meshes, spatial indexes, hit-test candidates, snap candidates, selection overlays and temporary previews.

The Map Engine contract owns immutable scene generations, broad-phase index, narrow-phase hit evaluation, coordinate transforms and screen-space tolerances. The Editor consumes these APIs; it must not implement a competing geometry query engine.

A snap is only a proposal. It does not create a shared vertex, topology node or semantic relationship. Before applying an operation, the Editor re-resolves the source snapshot and discards stale generation results.

## 10. Failure and recovery

| Failure | Required behavior |
|---|---|
| Edit gesture cancelled | Discard preview; formal geometry unchanged |
| Invalid payload/role/cardinality | Reject before formal publication |
| Source fingerprint stale | Reject and require fresh preview/review |
| Branch fork boundary violated | Reject Command |
| Asset write succeeds, formal transaction fails | No typed reference; safe orphan reconciliation |
| Renderer/index rebuild fails | Formal geometry remains valid; rebuild derived data |
| App restarts during staging | Recover or discard staging; temp files do not prove formal publication |
| Shared Object definition changes concurrently | Revalidate expected definition revision; reject stale update |

## 11. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| GM-001 | Adjust existing component | Same component ID and role; new immutable payload |
| GM-002 | Refine approximate component | Same ID; source fingerprint and provenance retained |
| GM-003 | Add prohibited role | Rejected by Object Type descriptor |
| GM-004 | Remove required final component | Rejected |
| GM-005 | Remove component in one Branch | Parent/sibling Branches unchanged |
| GM-006 | Adjust shared Object in one Map History | Other Maps' runtime states unchanged |
| GM-007 | Edit shared base definition | All referencing Maps resolve current shared definition |
| GM-008 | Split preview submitted as formal split | Rejected while descriptor remains deferred |
| GM-009 | Stale hit-test generation used for write | Discarded/rejected |
| GM-010 | Renderer cache removed | Formal geometry reconstructable from Core Domain + Assets |
| GM-011 | Geometry role changed by ordinary drag | Rejected |
| GM-012 | Coordinate tuples reinterpreted without conversion | Rejected |

## 12. Status and follow-up

This document consolidates the shared V1 geometry model and current descriptor boundary. It does not add tables, descriptors, executable APIs or implementation authorization.

Next: ME-05-03 defines reusable spatial predicates and the boundary between geometric topology, domain-specific topology constraints and formal semantic relationships. ME-05-04 will reconcile coordinate/precision semantics and measurement operations with the Map Engine and P2-09-25 contracts.

## References

- ME-01-05 Map Object, Geometry & Representation Model V0.1.
- ME-01-07 Cross-architecture Consistency Review V0.1.
- ME-05-01 Field Dataset Authority & Persistence Contract V0.3.
- P2-09-15 Core Domain Command Descriptor Catalog V0.5.
- P2-09-17 Command Payload Envelope and Validation Contract V0.5.
- P2-09-18 Object Type Descriptor Contract V0.2.
- P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- P2-09-20 Relationship Descriptor and Temporal Contract V0.2.
- P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- Map Engine Spatial Presentation & Geometry Interaction Contract V0.1.
