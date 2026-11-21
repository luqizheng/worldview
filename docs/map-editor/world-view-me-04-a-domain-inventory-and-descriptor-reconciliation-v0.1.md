# World View — ME-04-A Domain Inventory & Descriptor Reconciliation

- Version: 0.1
- Date: 2026-11-21
- Status: Domain Inventory Working Baseline

## 1. Purpose and conclusion

This document inventories the current formal domain surface before designing building and interior workflows.

**Conclusion:** the current recommended V1 Core Domain registries do not provide formal Building, Floor, Room, Entrance, Opening, VerticalCirculation, Cave or Facility Object Types. The current Relationship Registry has no building/interior endpoint pairs. Generic geometry components can carry bounded spatial shapes, but they do not create semantic identities or structural/connectivity relationships.

ME-04 can define product workflows and candidate domain concepts, but formal application of unsupported concepts is blocked until Core Domain adopts the required typed descriptors and operations. The editor must not fake support by storing arbitrary attributes, opaque metadata, presentation objects or misusing existing Object/Relationship Types.

## 2. Exact current Object Type inventory

The current P2-09-26 V0.2 recommended registry declares ten allocatable types:

| Object Type | Relevant geometry / fields | ME-04 relevance |
|---|---|---|
| landmass | extent Polygon/MultiPolygon; canonical_name, landform_class | Spatial context only |
| mountain_range | extent and/or ridge; canonical_name, range_class | Spatial context only |
| mountain_peak | location Point; canonical_name, elevation_value/unit/vertical_reference_id | Existing vertical-reference precedent, not a building-level model |
| river | centerline/reach; canonical_name, river_class | External environmental context |
| lake | shoreline; canonical_name, water_body_class | External environmental context |
| terrain_region | extent Polygon/MultiPolygon; terrain_class | Ground/site context |
| settlement | anchor Point, optional extent; canonical_name, settlement_class | Urban context; no formal building endpoint |
| road | centerline; optional name/class | External access context; no Building access relation |
| political_region | boundary; canonical_name, region_class | Context only |
| point_of_interest | location Point; canonical_name, poi_category | Not a substitute for Building/Room/Entrance |

The descriptor contract mentions building, floor, room and cave as examples of possible specialist semantic Objects, but these examples are not enabled entries in the current V1 registry. A taxonomy discussion is not an allocatable descriptor.

No current Object Type can be repurposed as a generic building/interior container without changing its declared semantics.

## 3. Exact current Relationship inventory

The current P2-09-27 V0.2 registry declares:

| Relationship | Enabled endpoint pairs | ME-04 disposition |
|---|---|---|
| spatial.contains | PoliticalRegion→Settlement; Landmass→MountainRange; Landmass→Settlement; Settlement→PointOfInterest; TerrainRegion→PointOfInterest | No Building/Floor/Room containment support |
| structural.part_of | MountainPeak→MountainRange | No Building→Floor, Floor→Room or Room→Building support |
| spatial.adjacent_to | PoliticalRegion↔PoliticalRegion; TerrainRegion↔TerrainRegion | Not interior adjacency |
| network.connects_to | Road↔Road | Not doors, rooms, corridors or stairs |
| hydrology.flows_to | River→River/Lake | Not underground/facility connectivity |

The existing relationship semantics are deliberately explicit:
- geometric containment is validation evidence, not an automatic relationship;
- spatial containment is not structural composition;
- structural part-of is not generic containment;
- Road connectivity is physical Road continuity only;
- snapping/touching/overlap does not establish connectivity.

No current endpoint pair can be inferred from a visually plausible floor plan.

## 4. Command and geometry capability inventory

### 4.1 Current bounded command surface

The recommended V1 command catalog includes Object presence lifecycle, selected runtime-attribute changes only where explicitly enabled, geometry component adjustment/refinement/addition/removal, and registered relationship establishment/termination.

The bounded post-zero geometry operations are:
- core.geometry.adjust_component;
- core.geometry.refine_component;
- core.geometry.add_component;
- core.geometry.remove_component.

Formal geometry split/merge are deferred. Object creation introduces the presence of an already Bound identity in a selected post-zero History; identity reservation/binding and definition ownership are separate. Time-zero membership/placement belongs to Map Initial State. Current shared definition edits are not History Commands.

The existence of a generic geometry command does not make every Building/Floor/Room type editable. The Object Type descriptor must permit the role, family, dimension, cardinality and operation.

### 4.2 Geometry component contract

P2-09-19 V0.2 establishes:
- stable Geometry Component identity scoped to one semantic Object;
- one declared role per component;
- immutable Asset-backed geometry payloads;
- typed coordinate context and dimensionality;
- V1 formal vector geometry as a 2D working plane;
- explicit local 2D coordinate contexts may be used for structured interiors;
- full 3D/solid geometry and specialist vertical semantics remain open/deferred to ME-04/ME-05.

The role examples include entrance and floor plan, but examples are not enabled roles on a Building descriptor. ME-04-A records them as requirements to evaluate, not currently executable roles.

## 5. Concept disposition matrix

This is an initial modeling recommendation for further ME-04 analysis, not a frozen Core Domain taxonomy.

| Concept | Candidate representation | Why | Current formal status |
|---|---|---|---|
| Building | Semantic Object | Stable identity, shared definition, location, history and references from other concepts | Missing Object Type |
| Building footprint | Building Geometry Component role | Exterior plan geometry belongs to Building spatial state | Role/descriptor missing |
| Exterior massing | Derived/presentation representation initially | Current 3D/solid geometry not supported by shared V1 contract | Derived only; no formal 3D |
| Floor / Level | Typed Building substructure or semantic Object; decide in ME-04-C | Needs stable references, order, vertical offset and relation to rooms | Unresolved; no Object Type/relationship |
| Room / Space | Likely semantic Object where independently named/referenced/connected | Stable identity and explicit room graph may be needed | Missing Object Type/relationships |
| Wall / Partition | Geometry component or structured boundary within a floor plan | Usually defines spaces rather than being an independent Object | No formal type; bounded geometry only after Floor/Building descriptor |
| Door / Window / Opening | Typed opening/portal component or semantic Object depending on identity and graph needs | Must distinguish opening geometry from traversability and connection | Unresolved; no type/role/relationship |
| Entrance | Building component or semantic Object, depending on multi-access identity | Access point must be referable and validated against outside context | No enabled descriptor role |
| Stair / Lift / Ramp | Explicit vertical transition/connection concept | Must connect specific spaces/levels; cannot be inferred from shape alone | Missing typed connection model |
| Cave / Natural underground space | Semantic Object if independently meaningful and connected | May cross site boundaries and have multiple entrances | Missing Object Type |
| Basement / Tunnel / Constructed underground space | Semantic space or connected facility subtype | Requires vertical placement and explicit connection semantics | Missing descriptor |
| Site | Working Scope/context by default | ME-01-04 Site is a working scope, not an automatic Object parent | No Site Object assumed |

Semantic Object identity is justified by independent meaning, references, lifecycle or relationship needs—not merely because a shape is visible or has a distinct symbol.

## 6. Cross-cutting gaps

### G1 — Building semantic descriptor

No Building Object Type, definition fields, geometry roles, invariant set, initial-state policy or command capability entry exists.

ME-04-B must define stable canonical identity/name policy, creator-facing building category/use, required/optional footprint semantics, association to Settlement or Site context without inventing an unsupported relationship, and separation of exterior geometry, interior plan geometry and presentation.

### G2 — Floor and vertical organization

No formal floor/level descriptor or building-local vertical frame exists.

ME-04-C must define floor ordering independent of altitude, local vertical origin/unit, floor elevation/height relative to the building-local frame, optional mapping to world/map vertical reference, sloped terrain/split-level/mezzanine/basement semantics, identity/lifecycle and room ownership, and explicit unknown/approximate vertical information.

### G3 — Room and interior topology

No Room Object Type or room/floor containment/connectivity relationships exist.

ME-04-D must define room identity/naming, room boundary geometry and open/irregular spaces, interior coordinate frame, adjacency versus traversable connection, multi-level/cross-building spaces, and boundary validity/uncertainty.

### G4 — Openings, entrances and vertical circulation

No Door/Entrance/Opening types or typed connection endpoints exist. Existing Road connectivity is not reusable.

ME-04-E must distinguish opening from traversability, identify exact source/target space endpoints, represent closed/locked doors without erasing the physical opening, represent stairs/lifts/ramps as transitions between levels, distinguish physical/intended/historical access, and validate geometry plus endpoint identity before formal application.

### G5 — Underground spaces

No Cave or underground-facility Object Type exists. 2D geometry alone cannot establish depth, vertical relation or connectivity.

ME-04-F must distinguish natural caves from constructed facilities, define local vertical/height model, multi-entrance and multi-level connectivity, site-boundary crossing, cave geometry versus navigable path/chamber, and unknown/unsafe/blocked passages without inventing traversability.

### G6 — Vertical coordinate contract

ME-05-04 defines immutable spatial references with optional vertical reference, but formal shared V1 editing remains a 2D working plane. Vertical reference, 3D precision and solid geometry require a separate ME-04/ME-05 extension.

ME-04 must not treat floor index as altitude, renderer Z-order as elevation, assume a universal sea-level datum, interpret local building coordinates as Map Coordinates, or persist arbitrary untyped 3D coordinate arrays.

## 7. Write-path and temporal rules

| Operation | Required authority path |
|---|---|
| Project-shared Building definition correction | Shared Object definition service, if Project-shared |
| Map-local Building definition correction | Map-local Object definition service |
| Time-zero Building/Floor/Room membership and placement | Map Initial State |
| Post-zero creation/presence/geometry/connectivity change | Selected Map/Branch History typed Command, if descriptor-enabled |
| Floor-plan local edits | Typed geometry operation on owning semantic Object and component |
| Presentation-only symbol, color, labels, view mode | Editor/Presentation state, not formal History |
| Vertical reference conversion | Explicit domain conversion with new immutable geometry payload and provenance; not a viewport change |

A Project-shared definition edit is retroactive under the shared-definition model; a post-zero history change is local to the selected Map/Branch History. These must not be conflated.

## 8. ME-04 design constraints and V1 recommendation

1. Keep one Building identity with coordinated exterior and interior editing views.
2. Use local 2D floor-plan geometry as the initial interior authoring foundation.
3. Model floors/rooms as semantic Objects only when independent identity, reference, lifecycle or connectivity requires it; otherwise prefer typed substructure.
4. Require explicit typed connections for traversability. Geometric adjacency is only evidence.
5. Keep building-local vertical organization separate from world elevation and floor labels.
6. Treat 3D massing as derived/presentation until the vertical/3D contract is adopted and tested.
7. Keep natural caves and constructed underground facilities semantically distinguishable.
8. Do not create a generic BIM object graph or universal indoor-routing engine in V1.

## 9. Acceptance checks for the inventory

| ID | Scenario | Expected result |
|---|---|---|
| A-01 | Creator opens Building tool against current V1 registry | UI shows design capability gap; it does not silently allocate another Object Type |
| A-02 | Creator draws a room polygon | Remains temporary geometry/Candidate until a supported semantic target exists |
| A-03 | User snaps two room boundaries together | No room connectivity is created from snapping |
| A-04 | User draws a door symbol between rooms | No traversable relationship is created without a typed connection operation |
| A-05 | User assigns Floor 2 | Floor label/order is not persisted as world altitude |
| A-06 | Building footprint is visually inside Settlement extent | Display spatial evidence only; no unsupported containment edge is stored |
| A-07 | Floor plan uses local coordinates | Coordinate context is explicit and cannot be confused with Map Coordinates |
| A-08 | Unsupported 3D solid is imported | Explicit Unsupported diagnostic; no silent flattening into formal 2D geometry |
| A-09 | Building definition is edited in a shared context | Correct Shared Definition service and revision guard are required |
| A-10 | Historical interior modification is applied | Only selected Map/Branch History changes after descriptor validation |

## 10. Open items for subsequent work packages

ME-04-B settles Building identity, definition, footprint and exterior/interior view relationship.
ME-04-C defines floor identity/substructure and local vertical coordinates.
ME-04-D settles room semantics and interior geometry.
ME-04-E defines openings and explicit connection semantics.
ME-04-F defines cave/underground scope and connectivity.
ME-04-G turns the resulting model into creator-facing journeys.
ME-04-H reconciles all proposals against Core Domain, AI Assistant, Map Engine, ME-05 and implementation constraints.

## References

- Core Domain P2-09-15 Command Descriptor Catalog V0.5.
- Core Domain P2-09-18 Object Type Descriptor / Attribute Contract V0.2.
- Core Domain P2-09-19 Geometry Component Identity / Payload Contract V0.2.
- Core Domain P2-09-20 Relationship Descriptor / Temporal Contract V0.2.
- Core Domain P2-09-26 V1 Object Type Descriptor Registry V0.2.
- Core Domain P2-09-27 V1 Relationship Type Descriptor Registry V0.2.
- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
- ME-01-05 Map Object, Geometry & Representation Model V0.1.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- ME-05-06 Cross-architecture Integration Review V0.4.
- ME-03-B Settlement Model & Authoring Workflows V0.1.
- ME-03-H Cross-architecture Integration Review V0.1.
