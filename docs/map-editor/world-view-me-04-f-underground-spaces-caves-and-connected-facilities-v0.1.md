# World View — ME-04-F Underground Spaces, Caves & Connected Facilities

- Version: 0.1
- Date: 2026-11-21
- Status: Underground domain and cross-scope connectivity working baseline
- Depends on: ME-01-04/05/06; ME-03-H; ME-04-A/B/C/D/E; ME-05-02/03/04/05/06; Core Domain Object/Relationship/Command/Asset contracts
- Scope: Natural cave systems, constructed underground buildings/facilities, subsurface networks, vertical reference, entrances, connectivity and uncertainty
- Authority: Product/domain working baseline only; does not authorize Core Domain registry, schema, command, relationship endpoint, API or 3D geometry changes

## 1. Executive decisions

ME-04-F adopts three distinct modeling paths rather than one generic underground Object model.

1. **Natural Cave System** is a first-class semantic Map Object candidate. A cave system has independent natural identity, may extend across Sites/Localities, may have multiple entrances and branches, and is not owned by a Building. It owns a bounded internal survey model of chambers, passages, entrances and survey stations.
2. **Constructed underground space inside one Building** reuses the existing Building → Level → Space → Opening/Connection model from ME-04-B/C/D/E. Basements, bunkers, underground rooms, building-integrated tunnels and below-grade service areas do not require a new generic Room or Facility taxonomy.
3. **Independent cross-Building subsurface infrastructure** requires a separately typed network/complex model only when it has identity and lifecycle independent of any one Building (for example, a metro tunnel network, utility tunnel system or connected mine infrastructure). It must not be faked as one Building-owned connector or as a set of geometric intersections.

Additional rules:
- Underground does not automatically mean 3D-capable. V1 remains 2D plan geometry plus explicit vertical/survey metadata where the typed contract supports it.
- Cave extent, surveyed passage geometry, physical void, traversability and current accessibility are separate facts.
- Geometric continuity, overlapping projections, shared coordinates or nearest entrances never create a formal connection.
- Unknown depth, vertical datum, passage continuation or safety condition remains Unknown; it is never guessed.
- This document is a domain working baseline, not an enabled Cave/Network descriptor or implementation authorization.

## 2. Underground vocabulary

| Concept | Meaning | Domain owner |
|---|---|---|
| Natural Cave System | A naturally formed, independently meaningful subsurface system | Candidate top-level Cave Object |
| Cave Chamber | A surveyed or interpreted chamber/void within a Cave System | Cave-owned child identity |
| Cave Passage | A natural passage segment connecting chambers or survey locations | Cave-owned child identity |
| Cave Entrance | A natural or modified surface opening into a Cave System | Cave-owned child identity with surface anchor |
| Survey Station | Stable reference point used by cave survey evidence | Cave-owned child identity |
| Cave Connection | Explicit cave-internal connection between chambers/passages | Cave-owned typed connection |
| Underground Building | A Building whose Levels/Spaces are partly or wholly below surrounding terrain | Existing Building model |
| Underground Facility | A constructed facility with independent identity, e.g. mine, metro station or utility plant | Building or bounded facility Object, depending on identity |
| Subsurface Network | Independent linear/branched constructed infrastructure connecting multiple Buildings/sites | Candidate top-level Network Object |
| Geological Void | A physical cavity or discontinuity without established traversability | Geological evidence, not automatically a Space |
| Survey Evidence | Measurement, scan, map, profile or source observation | Immutable evidence Asset / provenance reference |
| Access State | Current open/closed/blocked/flooded/unsafe/unknown condition | Contextual state, not physical geometry |

A cave chamber is not automatically a Room. A cave passage is not automatically a traversable route. A geological void is not automatically a mapped Cave System. A tunnel is not automatically a Building.

## 3. Natural Cave System identity and ownership

### 3.1 Why a Cave System is a top-level semantic Object

A Cave System can:
- retain identity while passages are newly surveyed, blocked, flooded or collapsed;
- extend across multiple Sites, Localities or Working Scopes;
- have multiple entrances and branches;
- be referenced independently by maps, stories, exploration records and geological knowledge;
- contain natural chambers and passages that are not subordinate to one Building;
- have a lifecycle and survey provenance independent of nearby Buildings.

Therefore, a Cave System should be a top-level semantic Object candidate, unlike ordinary Rooms and Building-owned Spaces.

A Cave System ID is allocated only by Core Domain. It is not derived from a cave name, entrance coordinate, survey file or geometry hash. Its identity is independent of the currently known extent; survey expansion does not create a new Cave System automatically.

### 3.2 Ownership scope

Use the existing Project-shared versus Map-local Object ownership model; do not invent a third scope.

- Default to Map-local when the Cave System is authored only within one Map.
- Use Project-shared only when multiple Maps intentionally refer to the same Cave System identity and shared-definition semantics are desired.
- A Cave System crossing a Site/Locality boundary within one Map remains one Cave System; Working Scope boundaries do not split it.
- Two nearby entrances or overlapping survey extents do not prove that two records are the same cave.
- Two maps showing the same cave coordinates do not automatically share identity.

A cave system may be partially surveyed. Its known geometry must not be presented as a complete physical extent unless the survey declares completeness.

### 3.3 Cave-owned child identities

Recommended bounded child records:
- `chamber_id`: stable identity for a named or separately surveyed chamber/void;
- `passage_id`: stable identity for a passage segment or branch;
- `entrance_id`: stable identity for a surface/constructed access opening;
- `station_id`: stable identity for a survey reference station;
- `connection_id`: stable identity for an explicit internal connection.

These IDs are scoped to one Cave System. They are not top-level Map Object IDs and are not interchangeable with Building Space IDs or ME-04-E Building Entrance IDs.

A chamber/passsage may be split or consolidated by survey interpretation, but identity changes require explicit lineage/provenance. Geometry split/merge operations do not silently create, retire or merge semantic child identities.

## 4. Cave structure: physical void versus surveyed route

A Cave System has at least two distinct representations:

1. **Physical void / chamber-passage structure** — the creator's current interpretation of the natural subsurface space.
2. **Survey route / observation structure** — measured stations, centerlines, cross-sections, elevations, uncertainty and source evidence.

These representations may correlate but are not interchangeable:
- a passage centerline is not the full passage volume;
- a chamber footprint is not a volumetric boundary;
- a survey line does not prove that a person can traverse the route;
- a mapped void may be blocked, flooded, unstable or inaccessible;
- a survey may stop at a blockage or unknown continuation.

V1 formal cave geometry should be deliberately bounded:
- 2D horizontal plan projection for known chamber footprints and passage centerlines where supported;
- explicit survey station references and measured vertical values only under a typed Cave Survey Frame;
- optional profile/cross-section evidence stored as immutable source/evidence Assets, not misrepresented as formal 3D geometry;
- no authoritative volumetric mesh, solid, arbitrary XYZ polyline or collision model.

A future full 3D cave model requires a separate ME-05 spatial-reference, precision, geometry, topology and Asset-format extension.

## 5. Cave Survey Frame and vertical reference

A Building-local Vertical Frame cannot be reused for a natural Cave System. A Cave needs its own survey context.

### 5.1 Cave Survey Frame (CSF)

A Cave System may define one or more versioned Cave Survey Frames. A frame declares:
- stable `survey_frame_id` and owning Cave System;
- horizontal coordinate context (Map reference or Cave-local Cartesian frame);
- vertical reference identity, unit and positive direction where known;
- local origin and transform/binding to a Map vertical reference where available;
- precision/uncertainty profile and survey method;
- version and provenance.

The frame must not assume sea level, terrain surface or a universal underground zero. If the vertical datum is unknown, local relative elevations may still be recorded where the survey method supports them, but absolute world altitude remains Unknown.

### 5.2 Relative and absolute elevation

Keep these values distinct:
- relative depth below a named entrance/station;
- local survey elevation within one Cave Survey Frame;
- absolute elevation in a declared Map vertical reference;
- terrain elevation from a Map-owned Field Dataset.

A terrain Field Revision is contextual source evidence. It does not rewrite Cave survey values. A change in terrain Field Revision may stale a terrain-to-cave comparison but does not alter Cave geometry, station elevations or survey provenance.

Do not calculate absolute cave depth by subtracting values expressed in unrelated vertical references. A transform must be explicit, versioned and accuracy-bearing.

### 5.3 Survey uncertainty

Survey data must preserve measurement quality:
- source and method;
- station/segment positional uncertainty;
- vertical uncertainty;
- date/time and survey provenance where available;
- confidence/quality classification;
- frame identity/version.

Unknown or approximate measurements must not be serialized as exact zero-error coordinates. The exact numeric precision profile is a technical gate, not a value to guess in this document.

## 6. Cave chambers, passages and explicit connectivity

### 6.1 Chamber model

A Chamber is a Cave-owned child record with stable identity and bounded attributes such as label, chamber kind, descriptive notes and one or more 2D plan footprints where supported.

A Chamber may have:
- no known footprint yet;
- one known plan region;
- multiple disjoint regions explicitly declared as one chamber;
- partial/uncertain boundary;
- associated survey stations and observations.

A chamber footprint is not a closed 3D solid and does not imply safe floor area or usable volume.

### 6.2 Passage model

A Passage is a Cave-owned child record representing a natural passage segment/branch. It may reference:
- a 2D centerline or plan path;
- ordered survey stations;
- optional width/height observations with units and uncertainty;
- explicit start/end chamber or station references;
- known direction only when meaningful and supported;
- current physical/access condition as separate state/evidence.

Passage width and height observations do not establish a complete passage cross-section unless the survey contract explicitly says so. Missing dimensions remain Unknown.

### 6.3 Explicit cave connectivity

Cave connections are typed child records between compatible endpoints:
- Chamber ↔ Chamber through a Passage;
- Chamber ↔ Passage;
- Passage ↔ Passage at an explicitly surveyed junction;
- Cave Entrance ↔ Chamber/Passage;
- Survey Station ↔ Passage segment as a measured anchor.

Endpoint references must resolve within the same Cave System unless a future cross-system connector contract is explicitly adopted.

Do not create a connection from:
- crossing or overlapping plan centerlines;
- nearest-neighbor station coordinates;
- two polygons touching;
- a passage endpoint being visually close to a chamber;
- AI inference without reviewed evidence.

A query may propose a possible junction with source survey evidence and uncertainty. Formal connection requires an explicit typed operation and endpoint validation.

## 7. Cave entrances and surface integration

A Cave Entrance is a Cave-owned identity representing the interface between the subsurface system and a surface location.

It may include:
- entrance kind (natural opening, sinkhole access, cliff opening, shaft, excavated access, other);
- stable Cave Entrance ID;
- cave-internal endpoint (Chamber/Passage/Station);
- optional Map-space surface anchor/geometry reference;
- surface-location uncertainty and provenance;
- access/condition evidence (open, sealed, collapsed, flooded, obstructed, unknown).

The surface anchor is not automatically a Building Entrance or Road Network node. It may be near a Road, River, Settlement, Building or Site without a formal relationship to any of them.

Where a cave entrance is physically inside a Building or constructed facility, ME-04-E's Building Entrance model may describe the Building-side access. The Cave Entrance remains the Cave-owned endpoint. A typed cross-domain connector must explicitly join them; neither child ID may be substituted for the other.

Multiple Cave Entrances may lead to the same internal chamber or passage. One entrance may become blocked without changing the Cave System identity or deleting the internal network.

## 8. Constructed underground spaces and facilities

### 8.1 Reuse Building for building-integrated underground space

For a basement, bunker room, underground archive, underground station building, cellar or service level that belongs to one Building:
- use the existing Building Object identity;
- use ME-04-C Building-owned Levels with negative/relative BLVF elevations where appropriate;
- use ME-04-D Spaces and plan regions;
- use ME-04-E Openings, Access Connections and Vertical Connectors;
- bind Building vertical origin to a Map vertical reference only through an explicit transform;
- do not create a separate Cave or Facility Object merely because the Building is below ground.

The Building's BLVF remains independent of terrain elevation. A Building may be partially buried, elevated, built into a cliff or on sloped terrain.

### 8.2 Independent constructed facility

A constructed underground facility that has independent identity and operation (e.g. a mine complex, utility plant or underground transit station) may be represented as:
- an ordinary Building when its spatial/functional identity is building-like and its interior can use the Building model; or
- a separately typed Facility Object only when its identity, ownership, lifecycle or cross-Building network semantics cannot be represented by one or more Buildings.

Do not add a generic Facility type simply to group several Buildings. Use an explicit typed Facility/Complex concept only after a concrete product workflow proves the need and its member/endpoint semantics are defined.

### 8.3 Linear or cross-Building subsurface infrastructure

A tunnel/utility/metro network that connects multiple Buildings or Sites cannot be owned by one Building's child Access Connection.

If such a network is in scope, it needs a separate top-level infrastructure identity and typed child segments/nodes, with explicit endpoint references to supported Building Entrances, Cave Entrances or other adopted endpoints. Candidate concept: `Subsurface Network`.

V1 should not introduce a universal network framework. A bounded Subsurface Network proposal must define:
- stable network identity and ownership scope;
- typed segment/shaft/portal child identities;
- supported endpoint kinds and directionality;
- explicit junction identity (not coordinate coincidence);
- access/operational state distinct from physical geometry;
- cross-Map/Project sharing semantics;
- lifecycle/history and reference integrity.

Until adopted, cross-Building underground connectivity is Unsupported. Do not split one continuous tunnel into unrelated Building-owned connectors merely to make the UI look connected.

## 9. Natural versus constructed underground semantics

| Concern | Natural Cave System | Constructed underground Building / Network |
|---|---|---|
| Origin | Natural geological formation | Human-designed/constructed |
| Primary identity | Cave System Object | Building or adopted Subsurface Network Object |
| Internal spaces | Cave-owned chambers/passages | Building-owned Spaces / network segments |
| Vertical context | Cave Survey Frame | Building-local Vertical Frame or network survey frame |
| Geometry meaning | Surveyed void/centerline/observations | Designed/constructed footprint and infrastructure geometry |
| Connectivity | Explicit cave junctions/passages | ME-04-E Building connectors or adopted network edges |
| Condition | Flooded, collapsed, blocked, unknown | Open, closed, locked, damaged, operational, unknown |
| Safety | Unknown unless supported evidence exists | No code/safety certification implied |
| Terrain relation | Geological and topographic context | Site/terrain evidence and explicit vertical binding |

A natural cave later modified by construction remains a Cave System with explicitly identified constructed access/structures; it does not automatically become a Building. A constructed tunnel passing through a natural cave does not merge the two identities.

## 10. Spatial scopes and cross-boundary continuity

Cave systems and subsurface networks may cross Site/Locality boundaries. Working Scope is not an ownership boundary.

Rules:
- one Cave System may be visible in several scopes while retaining one identity;
- a Site boundary does not clip or split the formal Cave System;
- map viewport clipping is presentation only;
- an incompletely surveyed continuation beyond the current scope is Unknown, not a formal continuation;
- cross-Map references require explicit shared identity or an adopted cross-Map reference contract;
- cross-Building connections require typed endpoints and an adopted network/connector contract;
- no cross-scope link is inferred from coordinates alone.

For a cave system that crosses a Map's formal extent, the creator may record a partial known extent and external continuation evidence. Extending Map Extent is a separate formal Map operation; the Cave System must not silently enlarge it.

## 11. History, lifecycle and access state

Keep separate:
- Cave System identity and current surveyed definition;
- chamber/passage/entrance child identity;
- current survey revision and evidence;
- Map Initial State;
- selected Map/Branch History changes;
- current physical condition;
- current access/operational state;
- derived reachability results.

A new survey may correct or extend the current Cave definition according to the Cave ownership scope. It is not automatically a historical geological event. A cave-in, excavation, flooding event or constructed blockage at a specific historical time is a time-specific world change and requires a typed History operation when supported.

For constructed underground Buildings, use the same shared-definition versus selected-History rules as ME-04-B/C/D/E. A later historical renovation or tunnel closure must not be simulated by retroactively changing a shared definition.

Access state is not geometry:
- a blocked passage remains a known passage;
- a flooded passage does not disappear;
- a sealed entrance retains identity;
- unknown condition is not open;
- a physically continuous passage may be unsafe or impassable.

No V1 document here enables cave-in simulation, structural stability analysis, flood simulation or human safety assessment.

## 12. Validation, uncertainty and safety claims

Required validation includes:
- stable child ID uniqueness and ownership within the Cave System;
- all chamber/passage/entrance/connection/station references resolve;
- explicit Cave Survey Frame identity and compatible horizontal/vertical units;
- no unsupported dimensionality or silent 3D-to-2D flattening;
- survey observations retain source, precision and uncertainty;
- connections have explicit compatible endpoints;
- passage segments and survey stations have consistent ordering where required;
- stale frame/geometry/measurement evidence is detected;
- cross-Building network endpoints are rejected unless the network contract is adopted.

Validation outcomes use Pass, Warning, Blocked, Unknown, Unsupported and AcknowledgedException. Numerical predicates may return Indeterminate.

AI and the Editor must never claim:
- a passage is traversable because a centerline exists;
- a cave is safe because geometry is valid;
- a tunnel is structurally stable;
- an underground room meets building/fire/accessibility codes;
- a cave has no continuation because the current survey ends;
- absolute depth is known without a compatible vertical reference.

## 13. AI assistance and evidence discipline

AI may:
- classify an imported survey as cave plan, passage centerline, cross-section or observation evidence;
- propose chamber/passage segmentation for review;
- identify possible survey junctions with evidence and uncertainty;
- compare cave entrances with terrain, rivers, roads and settlements as contextual observations;
- flag missing vertical datum, inconsistent station ordering or uncertain passage continuation;
- propose a Building/Level/Space model for a constructed underground facility.

AI must not:
- allocate Cave/child IDs outside Core Domain;
- convert a survey sketch into formal 3D geometry without an adopted contract;
- infer a cave connection from line crossings or nearest points;
- infer safe passage, air quality, structural stability, flood state or accessibility;
- invent absolute altitude/depth;
- create cross-Building connections without typed endpoint authorization;
- report formal publication before authoritative read-back.

Survey data, AI interpretation and creator-confirmed facts must retain distinct provenance. A derived cave extent or passage classification remains Candidate until accepted.

## 14. V1 scope and deferrals

| Capability | V1 recommendation | Status |
|---|---|---|
| Natural Cave System semantic identity | Adopt as top-level Object candidate | Requires Core Domain Object descriptor |
| Cave-owned chamber/passage/entrance/station IDs | Required | Requires typed child aggregate contract |
| 2D cave plan geometry | Bounded support target | Depends on ME-05 geometry/precision and survey evidence |
| Cave Survey Frame | Required where vertical values are recorded | Requires typed reference/transform contract |
| Survey uncertainty/provenance | Required | Typed evidence model needed |
| Explicit cave internal connectivity | Required for represented connections | Requires typed Cave connection records |
| Cave entrance surface anchor | Supported as explicit anchor/evidence | No automatic Road/Building relationship |
| Building basements and underground rooms | Reuse Building/Level/Space | No new generic Room/Floor model |
| Independent Subsurface Network | Proposal-only, need-driven | Requires separate Object and endpoint design |
| Cross-Building/cross-Site network routing | Deferred | Typed network endpoints required |
| Full 3D cave solids/volumetric topology | Deferred | Separate ME-05/Map Engine contract and technical proof |
| Cave safety/geotechnical/flood simulation | Out of scope | No engineering/safety engine |
| Universal indoor/subsurface routing engine | Out of scope | Explicit bounded connections only |

## 15. Core Domain and Map Engine adoption gates

Before formal Cave support:
1. Adopt a Cave System Object Type with explicit ownership scope, definition fields and lifecycle.
2. Define Cave-owned child identity records and referential integrity for chambers, passages, entrances, stations and connections.
3. Define Cave Survey Frame identity, horizontal/vertical reference binding, units, precision and uncertainty.
4. Define supported 2D geometry roles and immutable Asset ownership for cave plan/passage evidence.
5. Define typed explicit cave connectivity; no geometric inference.
6. Define entrance surface anchors and any permitted typed cross-domain endpoints.
7. Define current survey revision/publication and CAS semantics.
8. Define which changes are current definition corrections versus selected-History events.
9. Add validation vectors for partial surveys, unknown datum, uncertainty, disconnected endpoints, crossing-but-unconnected passages, multiple entrances and cross-scope continuity.
10. Validate deterministic serialization and geometry behavior on Windows/macOS.

Before formal Subsurface Network support:
1. Prove a concrete cross-Building use case that cannot use Building-owned ME-04-E connectors.
2. Define network identity, node/segment/shaft/portal ownership and endpoint types.
3. Define cross-Map/Project ownership and sharing semantics.
4. Define connection direction, operational state, history and lifecycle.
5. Reconcile with ME-03 Road endpoint identity and ME-04-E Building Entrance endpoints.
6. Keep it bounded to underground infrastructure; do not introduce a generic arbitrary graph platform.

No ME-04 document authorizes descriptor registry, schema, command catalog, relationship endpoint, API or 3D geometry changes.

## 16. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| F-01 | One Cave System crosses two Site scopes | One stable Cave identity; scope does not split it |
| F-02 | Two entrances are discovered | Two Cave Entrance IDs may reference the same internal network |
| F-03 | Two cave passage centerlines cross in plan | Candidate junction only; no connection inferred |
| F-04 | Survey ends at an unexplored passage | Known extent remains partial; continuation is Unknown |
| F-05 | Cave Survey Frame has no absolute datum | Relative survey values allowed where defined; world altitude Unknown |
| F-06 | Terrain Field Revision changes | Cave survey values remain unchanged; terrain comparison evidence may stale |
| F-07 | Passage becomes flooded or blocked | Passage identity/geometry retained; access state changes separately |
| F-08 | A cave chamber is split during reinterpretation | Explicit child identity lineage required; geometry split alone does not decide |
| F-09 | A basement is added to one Building | Reuse Building Level/Space model; no Cave Object |
| F-10 | A tunnel connects two Buildings | Unsupported until Subsurface Network/typed cross-Building connector is adopted |
| F-11 | A natural cave is modified by a constructed entrance | Cave identity remains; constructed access retains separate typed provenance |
| F-12 | A cave entrance is near a Road | Spatial evidence only; no Road connectivity created |
| F-13 | Cave plan imported from raster | Underlay/Candidate; not formal survey geometry |
| F-14 | Survey contains uncertain depth | Preserve uncertainty; do not invent precise altitude |
| F-15 | A Cave System is represented in two Maps | Shared identity only when explicitly adopted; coordinates do not merge records |
| F-16 | A 3D mesh is imported without 3D contract | Retain as evidence/unsupported source; do not flatten into authoritative 2D geometry |

## 17. Next work package and final recommendation

ME-04-F completes the underground semantic baseline for the specialist workstream. The next package should be **ME-04-G — UX Scenarios, Acceptance & Figma Handoff**, translating ME-04-B through ME-04-F into creator journeys while preserving the authority and capability limits.

After ME-04-G, ME-04-H must perform a cross-architecture integration review across Core Domain, ME-01/02/03/05 and Map Engine. It should consolidate all proposed descriptors, nested identities, geometry roles, endpoints, commands, persistence gaps and implementation gates without silently authorizing any of them.

Adopt the three-path model: natural Cave System as its own semantic identity; underground rooms within a Building reuse Building/Level/Space; independent cross-Building subsurface infrastructure remains a bounded proposal until a concrete requirement justifies its separate network identity. This avoids conflating geology, architecture and infrastructure while preserving a clear future extension path.
