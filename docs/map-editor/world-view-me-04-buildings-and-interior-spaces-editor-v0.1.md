# World View — ME-04 Buildings & Interior Spaces Editor

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist workstream scope and design plan
- Scope: Building/site association, building identity and footprint, floors/levels, rooms/interior spaces, openings/entrances, vertical circulation, underground spaces and creator workflows

## 1. Purpose

ME-04 defines how creators author buildings and structured interior/underground spaces inside the one shared World View Map Editor.

It is a specialist editing mode, not a separate CAD/BIM application, independent map, object database, geometry store or runtime. It preserves the shared Map Space, stable Object identity, formal geometry ownership, typed relationships, Map/Branch/Time semantics and AI Candidate/application boundary.

The creator should be able to move naturally from a locality into a site, select a building, inspect its exterior, switch to a floor plan, define rooms and entrances, and describe connections between spaces without needing to understand GIS or CAD terminology.

## 2. Product scope

ME-04 covers:
1. Building identity, definition, footprint and exterior representation.
2. Building association with a site/locality and surrounding settlement context.
3. Floor/level organization and vertical reference.
4. Rooms and other semantically meaningful interior spaces.
5. Walls, openings, doors, windows, entrances and portals where supported.
6. Vertical circulation such as stairs, lifts and ramps.
7. Underground structures, caves, basements, tunnels and connected facilities.
8. Spatial validation, access and connectivity evidence.
9. Canvas/Inspector workflows, Candidate review, diagnostics, recovery and Figma handoff.

The workstream distinguishes a semantic space from the geometry used to depict it, and a physical connection from mere geometric contact or overlap.

## 3. Inherited architecture constraints

- One continuous Map Space and one authoritative Core Domain world state.
- World / Region / Locality / Site are working scopes, not separate map types or mandatory parent-child Object hierarchies.
- Core Domain owns formal Object identity, ObjectState, Geometry Component identity/membership, typed relationships, Commands, write-context routing and history.
- Map Engine owns deterministic geometry/spatial computation, transforms, measurements, scene snapshots, spatial indexes and presentation-side acceleration.
- Editor owns interaction, temporary geometry, selection, tools, preview, staging and diagnostics; it cannot bypass Core Domain Domain Services.
- AI Assistant interprets intent, proposes, validates and explains; it does not directly mutate formal Core Domain state.
- Semantic Object, Geometry Component and Presentation remain distinct.
- Geometry coincidence, snapping, overlap or containment evidence does not by itself create a semantic relationship.
- Formal writes use registered typed operations and the correct authority path: Project-shared definition, Map-local definition, Map Initial State, or post-zero Map/Branch History Command.
- Post-zero history changes remain local to the selected Map/Branch History. Current shared-definition changes follow their separate retroactive definition semantics.
- Geometry split/merge and full 3D/solid geometry are not assumed to be available merely because the editor needs them.
- Prefer a small, explicit, maintainable V1 capability set over a general-purpose BIM/CAD platform.

## 4. Current Core Domain baseline and implications

The current recommended V1 Object Type Registry contains ten types: Landmass, MountainRange, MountainPeak, River, Lake, TerrainRegion, Settlement, Road, PoliticalRegion and PointOfInterest. It contains no Building, Floor, Room, Entrance, Door, Stair, Cave or Facility Object Type.

The current recommended Relationship Registry contains five types:
- spatial.contains, with explicit endpoint pairs that do not include Building, Floor or Room;
- structural.part_of, currently enabled only for MountainPeak → MountainRange;
- spatial.adjacent_to, for PoliticalRegion and TerrainRegion;
- network.connects_to, only Road ↔ Road;
- hydrology.flows_to, River → River/Lake.

ME-04 must not represent a room as a PointOfInterest, a building-to-site association as an unsupported containment edge, a door/portal as a Road network node, a floor hierarchy as an unsupported part-of relationship, or room connectivity as geometric touching.

Any new Object/Relationship type or endpoint pair is a Core Domain proposal requiring explicit descriptor, temporal, cardinality, validation, persistence and fixture design. A specialist document cannot make it executable by naming it.

## 5. Working conceptual decomposition

These concepts are evaluated without presuming all become independent semantic Objects:

| Concept | Initial modeling question |
|---|---|
| Building | Stable semantic entity with exterior footprint and potentially multiple interior representations |
| Floor / Level | Independently meaningful spatial level, or typed substructure within a Building |
| Room / Space | Semantic enclosed/usable space that may need stable identity and explicit connectivity |
| Wall / Partition | Geometry/structure defining boundaries, not automatically an independent Object |
| Door / Opening / Portal | Opening in a boundary; typed component or semantic Object depending on identity/connectivity needs |
| Entrance | Building access location and access semantics, not merely a point symbol |
| Stair / Lift / Ramp | Vertical circulation connection between specific spaces/levels |
| Cave / Underground Space | Natural or constructed space with geometry, vertical placement and possible multi-entrance connectivity |
| Site | Working scope/context; not automatically a persistent Object or mandatory containment parent |

The detailed disposition is owned by ME-04-A through ME-04-F and the cross-architecture review.

## 6. Vertical and local coordinate principles

ME-04 distinguishes:
1. Map/world vertical reference: a declared absolute or world-relative elevation reference when one exists.
2. Building-local vertical frame: a local reference used to organize floors and internal vertical offsets relative to a building.
3. Floor label/order: creator-facing names such as Basement 2, Ground Floor, Level 3 or Mezzanine. A label/order is not a physical elevation.

Floor number must never be silently treated as altitude. A building-local floor plan may use a local 2D coordinate context; transforming it to Map coordinates must be explicit and reversible within declared accuracy. The exact typed contract is an ME-04-C/ME-05 integration output.

The current shared geometry baseline is 2D. Full 3D solids, volumetric topology and general BIM parametrics remain outside the assumed V1 capability until a separately justified and tested extension is designed.

## 7. Initial work packages

| Work item | Focus | Main output |
|---|---|---|
| ME-04-A | Domain Inventory & Descriptor Reconciliation | Exact current Object/Relationship/Command capabilities; concept disposition and gaps |
| ME-04-B | Building Model & Authoring Workflows | Building identity, footprint, exterior/interior association, authoring lifecycle |
| ME-04-C | Floors, Levels & Vertical Organization | Floor semantics, local vertical frame, world elevation relationship and multi-level workflows |
| ME-04-D | Rooms, Spaces & Interior Geometry | Room identity, floor plans, boundaries, partitions and room containment/connectivity |
| ME-04-E | Entrances, Openings & Vertical Circulation | Doors/openings, building access, stairs/lifts/ramps and typed connectivity |
| ME-04-F | Underground Spaces, Caves & Connected Facilities | Natural/constructed underground spaces and cross-level/site connections |
| ME-04-G | UX Scenarios, Acceptance & Figma Handoff | Unified Canvas/Inspector flows, diagnostics, Candidate review, recovery and Figma prompt |
| ME-04-H | Cross-architecture Integration Review | Core Domain/ME-01/02/03/05/Map Engine reconciliation, findings, gates and final disposition |

## 8. Required design outputs

ME-04 must deliver:
- exact inventory of current descriptors and enabled endpoint pairs;
- semantic distinctions between Object, Component, local coordinate context and Presentation;
- explicit temporal/write-path disposition for each authoring operation;
- bounded V1 capability matrix and deferred capabilities;
- vertical/local coordinate and measurement requirements;
- spatial validation and connectivity semantics with uncertainty/freshness;
- Canvas/Inspector UX and acceptance scenarios;
- implementation gates with no implied descriptor/schema/library approval.

## 9. Out of scope unless separately adopted

- Full IFC/BIM interoperability or authoring parity.
- Parametric CAD constraints and construction documentation.
- Structural engineering, load, fire, evacuation or building-code compliance claims.
- Detailed MEP systems, HVAC, plumbing, electrical circuits and asset management.
- Full indoor navigation/routing or pedestrian simulation.
- Full 3D solid modeling, volumetric Boolean operations and general-purpose mesh editing.
- Automatic conversion of 2D floor plans into authoritative 3D solids.
- Automatic inference of doors, stairs or traversability from visual proximity alone.

## 10. Status

ME-04-00 establishes the specialist scope and inherited constraints only. It does not add Core Domain descriptors, schema, commands, APIs, geometry formats or implementation authorization. ME-04-A through ME-04-H will refine and reconcile the design.
