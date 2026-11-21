# World View — ME-04-D Rooms, Spaces & Interior Geometry

- Version: 0.1
- Date: 2026-11-21
- Status: Interior space and plan geometry working baseline
- Depends on: ME-04-00/A/B/C, ME-01 object/geometry model, ME-05-02/03/04/05/06, Core Domain descriptor/command/asset contracts
- Scope: Product/domain semantics and typed adoption requirements. This document does not authorize new Core Domain descriptors, schema, commands, relationship endpoint pairs or 3D geometry.

## 1. Executive decision

Adopt a **Building-owned Interior Space model**.

- A Building remains the one top-level semantic Map Object.
- Each interior Space receives a stable space_id scoped to its owning Building.
- A Space may be classified as a room, corridor, lobby, service area, storage area, circulation area, void/atrium or another bounded interior-space kind.
- A Space is a typed child record, not a separate top-level Map Object in V1.
- Spaces can be independently named, selected, referenced by creator knowledge and connected through explicit interior connection records without introducing a second top-level Object graph.
- Level identity remains Building-scoped under ME-04-C. A Space references one or more Levels explicitly.
- Formal interior geometry is Building-owned, immutable Asset-backed geometry with typed coordinate context. It is not a CAD document or an untyped floor-plan image.
- Geometric adjacency is not traversability. Door/opening/access connections are defined in ME-04-E.

This is a proposed Core Domain Building-payload extension. The current V1 registry does not contain a Building descriptor or interior-space child contract. No formal Space creation/editing is enabled until Core Domain adopts the typed contract.

## 2. Why Space is a Building-owned child, not a top-level Map Object

Interior spaces are meaningful and must have stable identity: a named throne room, workshop, cellar, corridor or atrium can be referenced in stories and connected to other spaces. However, their ownership and lifecycle are normally inseparable from the Building.

Making every room a top-level Map Object would require:
- a generic Building→Room relationship and Floor→Room relationship;
- a second Object ownership and presence lifecycle for every interior subdivision;
- object-level creation, identity reservation, history, deletion and selection for ordinary rooms;
- a large top-level object list for spaces that users naturally understand as parts of one Building;
- a generic relationship graph for room adjacency, access and containment before those semantics are ready.

Instead:
- Building owns a typed interior-space catalog.
- Every Space has a stable space_id unique within its Building.
- References use (building_object_id, space_id) and never labels or list positions.
- Space IDs remain stable through rename, geometry refinement, plan-view changes and reordering.
- A Space cannot be independently shared between Buildings.
- Copying a Building creates new child Space IDs; geometry Asset deduplication does not merge identity.
- External story/knowledge references may point to the scoped child identity through a typed reference contract; they do not require a top-level Map Object.

A child identity is not a weak display label. It is durable domain identity with explicit owner, lifecycle and reference integrity.

### 2.1 When a future concept should become a top-level Object

Promote a concept to an independent Object only if it needs independent ownership, presence/lifecycle, cross-Building identity, independent spatial placement or first-class domain relationships that cannot be represented cleanly as a Building-owned child.

A room being named or appearing in a story is not by itself sufficient. A room can be named and referenced through its stable child identity.

## 3. Space vocabulary and bounded classification

A Space is a semantically meaningful interior area or volume associated with one Building. It is not necessarily enclosed by physical walls and is not automatically a traversable area.

Recommended typed fields:

| Field | Meaning | Rule |
|---|---|---|
| space_id | Stable Building-scoped identity | Required, immutable |
| space_label | Creator-facing name, e.g. Great Hall, Kitchen, West Corridor | Optional; mutable |
| space_kind | Bounded classification | Required |
| level_refs | One or more Building-scoped Level references | Required for positioned Spaces |
| plan_slices | Per-Level plan footprint/boundary references | Required for positioned Spaces |
| vertical_span_local | Optional BLVF vertical interval | Required only when multi-level extent is explicitly modeled |
| space_status | Current semantic state such as active/retired in the Building definition | Typed lifecycle; not a presentation visibility flag |
| description_ref | Optional knowledge/narrative reference | Does not replace identity |

Recommended initial space_kind vocabulary:
- room — recognizable bounded room or chamber;
- corridor — elongated interior circulation space;
- lobby — entrance or distribution hall;
- service — utility, plant or service area;
- storage — storage area;
- circulation — shared circulation space not better classified as corridor/lobby;
- void — intentional unoccupied opening through a floor plate;
- atrium — named multi-level open interior volume;
- other — creator-defined semantics with a bounded description.

The enum is a proposed seed, not an enabled registry. Do not create a new kind for every room use (bedroom, kitchen, throne room, library) in V1. Room use/function can be a separate bounded optional classification or creator-authored label, and must not be conflated with geometry or access.

### 3.1 Space, room, void and floor area are not synonyms

- A **Room** is a Space with room semantics.
- A **Corridor** or **Lobby** is a Space but need not be a Room.
- A **Void** is an intentional absence of floor area, not a walkable Space.
- An **Atrium** is an authored multi-level interior volume; its floor plates and open void must not be confused.
- A **floor-plan polygon** is geometry. It becomes a formal Space footprint only when bound to a typed Space/Level reference.
- A **wall** is not a Space and is not automatically an independent semantic Object.

This vocabulary prevents the editor from treating every polygon as a room or every open area as traversable.

## 4. Identity, ownership and lifecycle

### 4.1 Identity

space_id is stable and Building-scoped. It must not be derived from:
- room label or room number;
- polygon coordinates or geometry hash;
- geometry Asset hash;
- display order;
- Level label;
- renderer node or selected Canvas tab.

Rename, renumber, reclassify, boundary adjustment and plan refinement preserve the Space ID.

Retiring a Space and creating a different Space must not reuse its ID. Merging two spaces or splitting one space is not a generic geometry edit: it changes semantic identity and references and requires explicit typed domain operations. Formal split/merge is deferred until Core Domain defines identity mapping and reference migration.

### 4.2 Ownership

A Space belongs to exactly one Building. It may reference multiple Levels within that Building when a deliberately multi-level space is supported. It cannot belong to two Buildings, even if two Buildings share the same physical complex or geometry.

Cross-Building connected facilities are handled through explicit facility/connector contracts in ME-04-E/F, not by assigning one Space to multiple Building owners.

### 4.3 Lifecycle

Distinguish:
- **retire Space** — the semantic child is no longer part of the current Building definition;
- **remove a plan slice** — a specific Level footprint association is removed;
- **hide in current editor view** — presentation-only;
- **remove geometry component** — typed geometry lifecycle;
- **historical disappearance/reconfiguration** — selected History operation, only if a typed command is adopted.

Retirement must not cascade-delete referenced knowledge or connector evidence. It must mark references unresolved/dormant according to an explicit Core Domain policy.

## 5. Floor plan and coordinate model

### 5.1 Building-local horizontal frame

Each interior plan uses the Building-local horizontal coordinate context established by ME-04-B and ME-05-04:
- stable horizontal frame identity;
- explicit units, axis orientation, origin and precision profile;
- explicit transform to the Map spatial reference;
- transform version/fingerprint and accuracy metadata;
- no silent reinterpretation of local coordinates as Map coordinates.

The Building-local horizontal frame and the Building-local Vertical Frame (BLVF) from ME-04-C are coordinated but distinct. V1 floor plans remain 2D. Level elevation is a scalar in BLVF; it is not encoded as a third coordinate in each 2D vertex.

### 5.2 Per-Level plan slices

A positioned Space is represented by one or more **Space Level Slices**:
- each slice references one stable (building_object_id, level_id);
- each slice has a typed plan-boundary reference in the Building's interior plan;
- the slice may carry explicit local vertical clearance metadata when supported;
- a regular Room normally has one Level Slice;
- a deliberately multi-level Space may have slices on multiple Levels, but must declare its vertical continuity semantics.

The Space identity remains the same across all its slices. Do not create duplicate room identities merely because an atrium or hall appears on multiple floor plans.

### 5.3 Geometry authority

A formal Space footprint is an interior clear-area boundary in the Level's Building-local 2D plan. It describes the authored area of the Space, not wall centerlines, wall thickness, structural faces or a 3D solid.

The plan boundary is an immutable Asset-backed geometry payload referenced by typed Building-owned component/plan records. Geometry components and plan slices must have stable identities and explicit roles. The exact role keys and payload schema require Core Domain adoption.

A floor-plan raster, screenshot, imported CAD file or AI-generated drawing may be used as an underlay or Candidate, but is not formal Space geometry until explicitly traced/adopted and validated.

### 5.4 Walls and partitions

V1 does not create independent top-level Wall Objects.

Recommended baseline:
- Space clear-area boundaries are authoritative for Space footprints.
- Wall/partition depiction may be represented by typed Building-owned boundary/partition components when the Core Domain geometry-role contract is adopted.
- A shared boundary between two adjacent Spaces should have one stable boundary identity where it represents the same authored partition, rather than two unrelated copies that can drift.
- Space boundary loops reference ordered boundary component identities.
- A boundary component may distinguish partition, exterior_boundary, virtual_boundary, open_edge or unknown semantics only after the bounded vocabulary is adopted.
- Wall thickness, construction material, load-bearing status, structural engineering and full wall solids are outside V1.

The boundary graph is a Building-owned plan structure, not a generic global topology graph. It exists to keep adjacent Space outlines coherent and to provide stable anchors for future openings. It must not be mistaken for a physical wall/BIM model.

### 5.5 Open and irregular spaces

A Space can be non-rectangular, curved, concave or irregular if the geometry family and precision contract support it. An enclosed Space requires a valid closed boundary. An explicitly open area may use a declared open-edge boundary model; it must not be silently auto-closed.

Holes/voids inside a Space footprint require explicit ring semantics. A visual gap, missing geometry or NoData region is not automatically a void or atrium.

## 6. Adjacency, containment and traversability

These are distinct concepts:

1. **Geometric contact** — two Space footprints touch or share a boundary.
2. **Interior adjacency** — the two Spaces are declared adjacent under an explicit boundary/plan rule.
3. **Opening** — a physical or authored opening exists in a boundary.
4. **Traversable connection** — movement between Spaces is explicitly permitted/possible under a typed access contract.

V1 rules:
- polygon touch/intersection does not create an adjacency record;
- shared boundary geometry is evidence of adjacency, not a traversable connection;
- a door symbol or gap in a line does not create an access connection;
- a wall/partition does not by itself prove separation if the plan is incomplete;
- no generic spatial.contains or network.connects_to relationship is repurposed for Room/Space pairs;
- no global room adjacency graph is required merely to draw a floor plan.

Where needed for plan diagnostics, adjacency may be a derived query result with freshness and evidence, not a formal Core Domain Relationship. Formal openings and movement connections are ME-04-E.

## 7. Multi-level spaces, voids and vertical extents

ME-04-C establishes Level identity and BLVF. ME-04-D consumes those references without introducing authoritative 3D solids.

### 7.1 Single-level Space

Default: one Space belongs to one Level and has one plan slice. This covers ordinary rooms, corridors, service rooms and most interior areas.

### 7.2 Multi-level Space

A single Space identity may span multiple Levels only when the creator explicitly declares it, such as:
- an atrium open across several floors;
- a double-height hall;
- a multi-level gallery;
- a tall chamber with floor openings.

Each occupied floor plane has an explicit slice or an explicit absence/void declaration. The Space also has a BLVF vertical interval when its vertical extent is known.

A multi-level Space is not inferred because polygons overlap on different floor plans. It must not be inferred from room names or visual alignment.

### 7.3 Void semantics

A void describes a deliberate absence of floor surface at a Level. It is not a Room, a traversable route or a hole in the Building footprint. It must reference the relevant Level and the associated multi-level Space/atrium context where applicable.

V1 may represent voids as explicit typed plan regions/annotations with bounded semantics, but must not claim volumetric collision or fall-safety validation.

### 7.4 Split-level and sloped interior

Split-level geometry remains explicit:
- use distinct Level Slices where separate floor elevations and plans are meaningful;
- do not use renderer Z-order or display_order as physical elevation;
- do not silently flatten a sloped floor into one horizontal Level if the elevation variation is semantically important;
- arbitrary per-vertex Z and full sloped 3D surfaces are deferred.

## 8. Boundary consistency and validation

Validation is scoped to a specific Building definition revision, Level catalog revision, plan geometry Asset fingerprints, horizontal frame version, BLVF version where relevant, and algorithm version.

Required checks include:
- all referenced Building, Level, Space and boundary IDs resolve;
- each plan slice references a Level owned by the same Building;
- geometry family/dimension/coordinate context is supported by the declared component role;
- enclosed boundary loops are closed and valid under ME-05 topology predicates;
- no self-intersection or invalid ring structure where the selected geometry contract forbids it;
- duplicate or contradictory boundary references are detected;
- shared boundary references resolve consistently for all participating Spaces;
- overlap/containment diagnostics distinguish intentional overlap, void, multi-level alignment and invalid geometry;
- stale geometry/query generations are rejected before formal apply.

Validation must not assume:
- every pair of adjacent polygons is connected;
- every gap is a door;
- every line is a wall;
- every closed polygon is a Room;
- every room has exactly one entrance;
- every Space is accessible or safe.

Use outcomes Pass, Warning, Blocked, Unknown, Unsupported and AcknowledgedException. A partial or ambiguous plan must not be reported as fully valid.

## 9. History, definition and write authority

Keep these write paths separate:

| Change | Authority |
|---|---|
| Project-shared Building's current interior definition | Shared Object definition service, if Building is Project-shared |
| Map-local Building's current interior definition | Map-local Object definition service |
| Initial Spaces and plan placement at time zero | Map Initial State / typed Building initialization contract |
| Post-zero Space creation, retirement or reconfiguration | Selected Map/Branch History typed command, only after adoption |
| Plan boundary geometry adjustment/refinement | Registered typed geometry operation on the owning Building component |
| Presentation color, labels, selection and active Level | Editor/Presentation state |
| Imported drawing or AI-proposed room layout | Candidate/staging until validated and explicitly accepted |
| Map elevation Field Revision | Map Source Field Baseline; never a room/Building construction event |

Current V1 registries have no Building/Space descriptors or interior commands. Generic geometry commands are not sufficient unless the Building descriptor declares the exact component role, geometry family, dimension, cardinality and allowed command capabilities.

Do not use a shared-definition edit to simulate a historical room addition. Do not use a Map Source Field Revision to simulate an interior construction change. Cancellation of an AI task must not roll back a formally applied Domain Command.

## 10. AI assistance and controlled authoring

AI may:
- propose room/space boundaries from a plan image or natural-language description;
- suggest labels and bounded Space kinds;
- identify likely shared boundaries, overlaps, gaps and invalid rings;
- explain why a boundary is ambiguous or unsupported;
- propose multi-level Space slices for creator review;
- generate a Candidate plan for preview and comparison.

AI must not:
- directly write Building or Space records;
- infer traversability from adjacency or a door-like symbol;
- infer exact wall thickness, material, load-bearing status, code compliance or accessibility;
- invent missing dimensions or world elevations as formal facts;
- silently merge/split Space identities or rewrite external references;
- publish Asset payloads or bypass Core Domain validation/authorization.

Candidate acceptance, authorization, submission and formal application remain distinct. Authorization binds the exact proposal version/hash and target Building/Level context. Before apply, re-resolve Building identity, Level IDs, component references, geometry fingerprints and editor/query generation.

Low-risk direct manual edits may use a supported typed editor command without forcing every interaction through an AI Candidate. Unsupported domain operations remain blocked regardless of user or AI origin.

## 11. V1 scope and deferrals

| Capability | V1 recommendation | Status |
|---|---|---|
| Building-owned Space catalog | Adopt | Requires Core Domain typed payload |
| Stable Building-scoped Space IDs | Required | Requires identity/reference contract |
| Room/corridor/lobby/service/void kinds | Bounded initial vocabulary | Requires descriptor adoption |
| Single-Level Space footprint | Core authoring target | Requires typed plan-slice and geometry roles |
| Multi-Level Space identity | Supported semantic target | Requires explicit Level slices/vertical span contract |
| 2D Building-local floor plan | Required | Uses ME-04-B/C and ME-05-04 coordinate contexts |
| Shared boundary component identity | Recommended | Requires typed boundary/plan component contract |
| Wall thickness / full wall solids | Deferred | No V1 BIM/3D model |
| Generic room adjacency graph | Not required | Derived evidence only where useful |
| Formal traversable connections | ME-04-E | No inference from geometry |
| Formal Space split/merge | Deferred | Needs identity/reference migration contract |
| Structural, fire, acoustic or accessibility compliance | Out of scope | No engineering validation |
| Independent top-level Room Map Objects | Not recommended for V1 | Avoid object/relationship explosion |

## 12. Core Domain / Map Engine adoption gates

Before enabling formal Spaces:
1. Adopt a Building descriptor and typed Building-owned interior catalog; no arbitrary opaque JSON.
2. Define stable space_id allocation, uniqueness, retirement, reference integrity and copy semantics.
3. Define bounded Space kinds and the distinction between Room, circulation Space, Void and multi-level Atrium.
4. Define Level Slice references to ME-04-C Level IDs, including multi-level Space semantics.
5. Define Building-local horizontal coordinate context and explicit relationship to BLVF; do not imply full 3D.
6. Define typed geometry roles for Space plan boundaries and Building-owned boundary components, with stable component identity and immutable Asset references.
7. Define shared boundary ownership/reference rules and ordered boundary-loop semantics.
8. Define initial-state and selected-History operations for supported Space lifecycle and geometry changes.
9. Define how ME-04-E openings/connectors reference boundary components and Space IDs.
10. Add deterministic positive/negative fixtures for closed/open boundaries, shared edges, holes, invalid rings, overlap, dangling Level references, multi-level voids and stale geometry generations.
11. Validate serialization, geometry predicates and transforms consistently on Windows/macOS.

No ME-04 document authorizes registry, database schema, command catalog, relationship endpoint or 3D geometry changes.

## 13. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| D-01 | Create a named Great Hall in a Building | Stable Building-scoped Space ID; no top-level Map Object |
| D-02 | Rename a Space | Same Space ID and preserved plan/knowledge references |
| D-03 | Add a corridor between rooms | Corridor is a Space; no automatic access edges |
| D-04 | Draw two touching room polygons | Adjacency evidence only; no traversable connection |
| D-05 | Draw a door symbol on a shared boundary | No access connection until ME-04-E typed operation |
| D-06 | Refine a shared boundary | All referencing Space loops remain coherent or edit is rejected |
| D-07 | Create an irregular enclosed room | Valid closed boundary accepted within geometry contract |
| D-08 | Create an open-plan lobby | Explicit open/area semantics; no fake wall closure |
| D-09 | Add a hole/void in a floor plate | Explicit void semantics; not interpreted as missing geometry |
| D-10 | Create a double-height hall across two Levels | One Space ID with explicit Level Slices and vertical span |
| D-11 | Align polygons on two Levels without declaring continuity | Remain separate Spaces; no inferred atrium |
| D-12 | Retire a Space referenced by story knowledge | Reference remains traceable and is marked retired/unresolved per policy |
| D-13 | Split one Space into two | Blocked until typed split and reference migration contract exists |
| D-14 | Import a floor-plan image | Underlay/Candidate only; not formal geometry |
| D-15 | Change Map elevation Field Revision | Interior Space geometry and local coordinates remain unchanged |
| D-16 | Edit a Project-shared Building interior definition | Shared definition service and revision guard required |
| D-17 | Apply a post-zero room addition | Selected-History command required; otherwise Unsupported |
| D-18 | Renderer camera or Z-order changes | No formal Space/Level/geometry changes |
| D-19 | Space references Level from another Building | Reject dangling/cross-owner reference |
| D-20 | Boundary query returns Indeterminate | Show uncertainty; do not claim valid adjacency or closure |

## 14. Final recommendation

Adopt the Building-owned Interior Space model with stable Building-scoped Space IDs, explicit Level Slices and Building-local 2D plan geometry.

Use shared, stable boundary components to keep adjacent Spaces coherent and to provide future anchors for doors/openings. Do not create a generic top-level Room Object graph or a generic room adjacency network in V1.

Most importantly, keep **space identity, plan geometry, boundary structure, adjacency and traversability** separate. This gives the editor a coherent way to author complex interiors while leaving explicit access semantics to ME-04-E and full 3D/engineering behavior outside the V1 scope.
