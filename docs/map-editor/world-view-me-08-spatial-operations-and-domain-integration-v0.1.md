# ME-08 — Spatial Operations & Domain Integration

- Version: 0.1
- Date: 2026-11-23
- Status: Spatial Operations / Domain Integration Working Baseline
- Workstream: ME-08
- Repository: `luqizheng/worldview`

## 1. Purpose and scope

ME-08 defines how specialist spatial domains are organized and connected through a coherent spatial-operation model. It builds on, and does not replace, the shared geometry and spatial-query contracts in ME-05, the Building/Cave specialist model in ME-04, the urban and road model in ME-03, and the shared interaction contracts in ME-06.

The current decision package focuses on:

1. scene composition for buildings, outdoor spaces, natural spaces and special facilities;
2. extensible spatial grouping without erasing domain-specific entity types;
3. the dedicated interior-editing scene and its relationship to the world-map scene;
4. domain-specific connection objects governed by a shared connection protocol; and
5. explicit endpoint semantics that prevent geometric coincidence from silently becoming traversability.

This is a product/domain working baseline. It does not authorize new Core Domain Object Types, Relationship Types, Commands, registry entries, persistence tables, APIs or executable schemas. Those require a separate descriptor and Core Domain adoption review.

## 2. Inherited architecture constraints

The following existing boundaries remain authoritative:

- Core Domain owns formal object identity, typed geometry ownership/references, formal relationships, write-context routing and history semantics.
- Map Engine performs deterministic coordinate transforms, spatial predicates, measurement, field sampling, indexing, hit-testing and other derived spatial calculations. Derived structures remain rebuildable.
- Editor owns editing context, tools, transient geometry, previews, staging, diagnostics and user review.
- AI may propose and explain changes but does not directly mutate formal domain state or invent numeric spatial results.
- Geometry coincidence, intersection, contact or adjacency does not by itself create a semantic relationship or traversable connection.
- Formal geometry, domain relationships, presentation and derived caches remain separate concepts.
- ME-04's Building-local frame, Level, Space, Entrance, Opening, Access Connection, Vertical Connector and Cave System concepts remain the specialist source of truth for their stated scope. ME-08 composes and integrates them; it does not redefine them.
- Existing history, Branch and write-context authority remains in Core Domain. Scene switching is an editor-context transition, not a new history mechanism.

## 3. Scene composition model

### 3.1 Spatial Site

A **Spatial Site** is a scene-level composition boundary for a coherent place or designed environment. Examples include a small residence, a castle complex, a magic academy, a war academy, a dungeon complex or a connected cave site.

A Spatial Site may be represented by one spatial entity or by a composition of multiple entities. It is an organizational/domain concept and must not be treated as a replacement for the geometry or identity of its members.

A Spatial Site may have an exterior/world-map representation, but that representation is not required to expose all of its interior geometry.

### 3.2 Spatial Group

A **Spatial Group** organizes members into meaningful named or typed collections. Spatial Groups may be nested to arbitrary practical depth.

Examples:

- Magic Academy → Teaching District → Advanced Research Area
- War Academy → Training District → Tactical Simulation Area
- Castle → Main Complex → Underground Complex
- Dungeon → Upper Region → Monster Lair

Spatial Group is an organizational construct. It is not automatically a physical space, geometry owner, navigable node, or domain entity.

### 3.3 Spatial Entity

A **Spatial Entity** is a typed domain object representing an actual or designed spatial thing. The type remains explicit and determines its domain rules, geometry ownership, editing behavior and validation.

Examples include:

- Building
- Level / Floor
- Room / Space
- Outdoor Space, such as a courtyard, plaza or training ground
- Natural Space, such as a cave chamber or underground lake
- Special Facility, where a concrete domain type has been adopted

The list is illustrative, not an instruction to add all types to the current registry. Existing ME-04 Building, Level, Space and Cave System semantics must be reused where applicable. New entity types require descriptor reconciliation and adoption.

### 3.4 Group membership is not spatial containment

The following relations must remain distinct:

- **Organization / membership**: an entity is listed under a Spatial Group or Site.
- **Spatial containment**: one geometry or spatial extent is inside another.
- **Adjacency**: two spaces satisfy a defined spatial adjacency predicate.
- **Connection / traversability**: an explicit domain connection permits movement or another defined transition.

For example, a library may be organized under an academy's teaching district without its geometry necessarily being contained by the district boundary. Each claim must be represented and validated separately.

A single entity must not be duplicated merely because it appears in more than one organizational view. References to the same stable entity identity are preferred over copied entities.

## 4. Scene composition patterns

Both single-entity and multi-entity scenes are supported.

### 4.1 Single-building scene

A small residence may be represented as one Building with its own local spatial context, Levels and Spaces. The Spatial Site may simply organize that Building.

### 4.2 Multi-entity scene

A larger academy or castle may combine:

- multiple Buildings;
- outdoor spaces such as courtyards, plazas and training grounds;
- natural spaces such as caves or underground water areas;
- adopted special facilities;
- paths and explicit connections between these entities.

A Magic Academy may include a teaching building, library, dormitory, magic tower, central courtyard, training ground and underground research area. A War Academy may include barracks, tactical classrooms, weapon storage, parade grounds, training arenas and simulation facilities.

The academy itself is not forced into the Building type. It is a Spatial Site and/or a higher-level domain concept composed of multiple typed entities.

## 5. Interior editor context and scene switching

### 5.1 World-map representation

On the world map, a Building is represented at exterior scale by its footprint, outline, symbol or other appropriate presentation. The world map does not display the full interior plan as ordinary world-map geometry.

World-map operations may include selecting the Building, moving or rotating it, editing its exterior placement/footprint where supported, and inspecting its explicit relationships with roads, terrain, regions and other world objects.

### 5.2 Dedicated interior editing scene

Selecting/opening a Building transitions the editor to a dedicated Building Editor scene. This is a scene/context switch, not merely zooming into a world-map layer.

The primary editing mode is a **2D plan editor**:

- show the active Level's 2D plan by default;
- allow switching between Levels, including above-ground and underground Levels;
- edit Spaces, boundaries, walls, openings, entrances and vertical-circulation elements using their ME-04 semantics;
- support multiple layout templates and scene/design themes;
- preserve a clear path back to the world-map context.

The spatial model must not be constrained to ordinary rectangular rooms. Cave and underground layouts may be irregular, vertically connected or naturally surveyed. The 2D plan is the primary editing representation; it does not prohibit future additional views.

### 5.3 Context isolation and return behavior

Entering an interior editor changes the active editing context. It must not create a copied Building or copied geometry.

On return to the world map, the editor should restore the previous world-map camera/view and selection context where still valid, then refresh the Building's exterior presentation from authoritative state.

Interior edits affect Building-local interior structures. They do not silently alter the world-space footprint. Exterior placement or footprint changes affect world-space representation through the appropriate formal operation.

Indoor doors, stairs and corridors do not automatically become world-road network edges. Cross-context access requires explicit boundary anchors and typed connections.

## 6. Layout templates, themes and functional modules

The editor must support multiple reusable design layouts. A template provides a starting arrangement; it does not lock the user into the template or replace the formal spatial entities created from it.

The design system separates four concerns:

| Concern | Responsibility | Examples |
|---|---|---|
| Spatial kind | Broad spatial structure and editing behavior | Building interior, underground level, cave |
| Layout template | Initial arrangement of spaces and circulation | Residence, courtyard plan, maze, dungeon |
| Scene theme | Visual language, atmosphere and presentation defaults | Medieval, magical, ruined, subterranean |
| Functional module | Reusable functional composition | Classroom, dormitory, laboratory, armory, library |

Examples of template/theme/module combinations:

- **Residence**: living room, kitchen, bedrooms, storage and optional courtyard.
- **Underground level**: cellar, storage, secret passage, hidden entrance and altar.
- **Cave**: irregular chambers, narrow passages, underground lake, vertical opening and mineral vein.
- **Magic Academy**: teaching, dormitory, library, laboratory, summoning hall and magic tower modules.
- **War Academy**: training ground, tactical classroom, armory, command room, simulation battlefield and officer quarters.

A Magic Academy or War Academy is a higher-level functional scene composition, not merely a room-layout template. It may span several Buildings and outdoor spaces.

Templates, themes and modules must be extensible data/resources rather than closed enums embedded in the core spatial model. User-defined and future content packs should be possible. Loading a template must not silently introduce unadopted formal domain types.

## 7. Connection model

### 7.1 Domain-specific connection objects

Connections retain explicit domain identity and semantics. They are not all collapsed into a generic undifferentiated Edge.

Examples:

| Connection concept | Domain object / ME-04 concept | Typical use |
|---|---|---|
| Door / Opening | Door or Opening, subject to adopted descriptor semantics | Room-to-room access |
| Entrance | Entrance / Access Connection | Building interior to exterior/world context |
| Vertical circulation | Vertical Connector, such as a stairway or elevator | Between Levels |
| Cave passage | Cave-domain passage concept, subject to descriptor adoption | Between cave chambers |
| Tunnel | Typed tunnel/access concept where adopted | Constructed underground connection |
| Portal | Typed magical connection concept, subject to future domain adoption | Non-ordinary transition between spaces |

The table describes conceptual domain categories. It does not assert that every row is currently an adopted Core Domain Object Type.

### 7.2 Shared connection protocol

Domain-specific connection objects should follow a shared behavioral contract where applicable:

- stable connection identity;
- typed endpoint references;
- endpoint anchors or attachment locations when required;
- connection geometry or spatial extent when required;
- traversal/transition semantics, including directionality;
- operational state and constraints;
- domain-specific properties and validation.

The protocol defines common concepts and validation hooks; it does not require every connection type to store identical fields. A Portal may have destination and activation semantics that a Door does not have. A Stairway may have vertical extent and Level bindings that a Cave Passage does not have.

### 7.3 Geometry does not imply connection

The following are explicitly prohibited:

- two spaces touching does not automatically create a Door;
- two geometries intersecting does not automatically create a traversable route;
- an opening-looking gap in a wall does not become a formal Opening without an explicit domain operation;
- a geometric crossing of routes does not automatically create a network junction;
- visual proximity does not imply access.

Connection creation and modification must be explicit domain operations with appropriate validation.

## 8. Connection cardinality and multi-way junctions

A normal connection object has **two endpoints**. This is the default contract for doors, entrances, passages, stairs and portals.

A multi-way spatial junction should normally be represented by an actual intermediate Spatial Entity, not by making one connection object connect an arbitrary number of spaces.

Examples:

- A central hall is a Room/Space; each adjoining room connects to it through its own Door/Opening.
- A cave junction chamber is a cave Space; each passage connects the chamber to another chamber.
- A portal hall is a spatial entity; each Portal connects that hall to one destination endpoint according to its own semantics.

This preserves clear identity, local validation, traversability and future route analysis. It also prevents a connection from becoming an ambiguous multi-node container.

If a future domain requires a genuinely multi-party relation that is not representable as an intermediate space plus pairwise connections, it must define a separate typed construct with explicit semantics. It must not silently weaken the default two-endpoint contract.

## 9. Validation responsibilities

Validation should distinguish at least the following categories:

1. **Identity and reference validity**: referenced entities and anchors exist and have compatible types.
2. **Geometry validity**: connection geometry is valid in its declared spatial frame and precision profile.
3. **Attachment validity**: anchors attach to the intended boundary, Level or endpoint region.
4. **Domain validity**: Door, Entrance, Stairway, Cave Passage or Portal satisfies its own rules.
5. **Spatial relation validity**: containment, adjacency and intersection claims are checked independently.
6. **Traversal validity**: direction, access state and constraints are evaluated explicitly.
7. **Context validity**: local Building/Cave coordinates and world-space references are not mixed without a declared transform/binding.
8. **Freshness validity**: query-derived evidence is re-resolved at application time under ME-06 freshness rules.

A validation result must distinguish invalid, uncertain, unsupported and not-yet-adopted cases. It must not fabricate a successful connection from incomplete geometric evidence.

## 10. Open boundaries and non-decisions

The following are intentionally not decided by this ME-08 baseline:

- whether Road and River endpoint identity uses a dedicated stable endpoint model; the earlier ME-03 road endpoint blocker remains open and must be resolved in its owning workstream;
- the complete catalog and Core Domain adoption of Outdoor Space, Special Facility, Cave Passage, Tunnel and Portal types;
- a universal 3D geometry authoring model; ME-04's current 2D interior-plan boundary remains in force;
- automatic generation of functional layouts or complete academy/castle designs;
- arbitrary third-party plugin loading or executable user scripts;
- physical schema, migration, API and persistence implementation details.

The desktop product remains a single-user local application for Windows and macOS. Collaboration and concurrent multi-user editing are out of scope for this design.

## 11. Confirmed decisions

| Decision | Confirmed baseline |
|---|---|
| ME-08-01 / Building editing scene | Opening a Building switches from the world-map scene to a dedicated interior-editing scene; interior editing is not an in-place world-map layer. |
| ME-08-01 / primary interior mode | A — dedicated 2D plan editor, with Level switching. |
| ME-08-01 / design variety | Multiple reusable layouts are required, including residence, underground-level and cave designs; fantasy scene design and future extensibility are required. |
| ME-08-02 | C — support both a single Building scene and a multi-entity Spatial Site composition. |
| ME-08-03 | C — Spatial Groups may nest to arbitrary practical depth, while spatial entities retain explicit domain types. |
| ME-08-04 | B — domain-specific connection objects follow a shared connection protocol. |
| ME-08-05 | C — normal connections have two endpoints; multi-way junctions are modeled through an intermediate spatial entity. |

## 12. Acceptance criteria for this working baseline

The design is considered internally consistent when:

- a small residence can be edited as one Building without requiring a multi-building site model;
- a Magic Academy or War Academy can compose multiple Buildings, outdoor spaces and special facilities under one Spatial Site;
- Spatial Groups can be nested without being mistaken for physical spaces;
- one spatial entity can be referenced from organizational views without duplicating its identity;
- opening a Building switches to a dedicated 2D interior editor and returning restores the world-map context;
- multiple templates, themes and functional modules can be combined without hard-coding future scene categories into the core model;
- Doors, Openings, Entrances, Vertical Connectors and future connection types preserve their domain-specific semantics;
- every normal connection has two endpoints;
- multi-way junctions are represented by an intermediate spatial entity and pairwise connections;
- geometric contact or intersection never silently creates semantic connectivity;
- the design remains compatible with ME-04 local coordinate frames and ME-05 geometry/precision/predicate boundaries.

## 13. Related documents

- ME-01 shared editor architecture and object/geometry model.
- ME-03-E Road & Path Network Authoring and ME-03-H Cross-architecture Integration Review.
- ME-04-00 through ME-04-H Buildings & Interior Spaces specialist design package.
- ME-05-02 Shared Geometry Model & Component Operations.
- ME-05-03 Topology & Spatial Predicate Contract.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract.
- ME-06-B Input Routing & Gesture Lifecycle.
- ME-06-C Spatial Query Freshness.
- ME-06-D Typed Intent & Formal Operation Routing.
- ME-06-G Cross-architecture Integration Review.

---

ME-08-0.1 records confirmed product/domain decisions only. It is not implementation authorization or a frozen Core Domain contract.
