# World View — ME-03 Settlements & Urban Geography Editor

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist workstream scope and design plan; no new Object Type, Relationship Type, executable descriptor, schema or implementation is authorized by this document
- Depends on: ME-01-01 through ME-01-07; ME-02 V0.6 and ME-02-A through ME-02-H; ME-05-01 through ME-05-06; Core Domain P2-09-18 through P2-09-27
- Supersedes: none

## 1. Purpose

ME-03 defines the specialist product and domain behavior for authoring settlements and human-shaped spatial organization in the unified World View editor.

It covers:
- villages, towns, cities, capitals and other inhabited places;
- settlement placement, extent and progressive refinement;
- political and administrative territories;
- urban districts and human land-use / functional areas;
- roads, paths and the semantic connectivity of transport networks;
- creator-facing spatial validation and AI-assisted planning.

ME-03 is a specialist mode within the single shared Map Editor. It does not create a separate city editor application, independent map database, parallel Object registry, separate geometry engine or generic GIS workflow.

The goal is to let a world creator describe a place in ordinary language — for example, a port city on a sheltered coast, a market town at a river crossing, or a chain of settlements along a mountain pass — and then author, inspect and refine the resulting world facts with clear provenance and controlled formal application.

## 2. Product and architecture invariants

The following are inherited constraints, not reopened decisions:

1. One unified Map Space and one shared Map Editor shell serve all specialist modes.
2. World / Region / Locality / Site are Working Scopes, not separate maps, Object Types or administrative levels.
3. Core Domain owns formal Object identity, ObjectState, geometry references, relationships, Commands and history.
4. Map Engine supplies deterministic geometry operations, coordinate transforms, predicates, measurements, spatial indexes and query freshness. It does not create formal semantic facts.
5. Editor owns creator interaction, tools, transient edit sessions, previews, diagnostics and Candidate review.
6. AI Assistant interprets creator intent, plans work and proposes Candidates; it does not write Core Domain directly or invent spatial measurements.
7. Formal changes use registered typed Core Domain operations and the correct authority path: Map Initial State, Map/Branch History, or Project-shared definition where applicable.
8. Geometric coincidence, overlap, containment or proximity does not by itself establish a formal semantic relationship.
9. A Candidate, derived analysis, presentation layer or visual grouping is not a formal world fact.
10. The same Object identity is reused across scales and presentations; no scale-specific duplication is introduced merely to simplify display.

## 3. Existing Core Domain vocabulary to consume

ME-03 begins from the current recommended V1 seed catalogs. It must validate and refine their specialist applicability rather than silently replacing them.

### 3.1 Existing Object Type seeds

| Stable type key | Current meaning | ME-03 responsibility |
|---|---|---|
| `settlement` | Inhabited place; anchor plus optional extent | Main settlement authoring and placement |
| `road` | Transport feature with centerline geometry | Road and path semantics, network connectivity |
| `political_region` | Political or administrative territory | Political/administrative geography |
| `point_of_interest` | Named map-relevant location | Relevant civic, transport and service locations where the bounded POI category is sufficient |
| `terrain_region` | Terrain/ecological/land-cover region | Consume natural-geography context; do not overload terrain classification with human land-use meaning without an explicit contract |

The current Settlement classification candidates are village / town / city / capital / other. These are classifications of one Settlement type, not separate Object Types. The current Road seed has a centerline and bounded road class. The current PoliticalRegion seed represents a territory; it does not by itself represent a government, polity, institution or controlling actor.

### 3.2 Existing Relationship Type seeds

| Relationship Type | Existing bounded semantics | ME-03 usage constraint |
|---|---|---|
| `spatial.contains` | Directed container → contained | Use only for explicitly confirmed containment with an allowed endpoint pair |
| `structural.part_of` | Directed part → whole | Use only when both endpoints are independently meaningful semantic Objects |
| `spatial.adjacent_to` | Symmetric meaningful adjacency | Not a generic distance/proximity result |
| `network.connects_to` | Explicit network/access connectivity | Road endpoint coincidence or snapping is insufficient |

The endpoint matrix, cardinality, temporal class, endpoint activity, cycle policy and typed attributes must be checked against the current descriptor owner before any relationship is treated as enabled. The seed catalog is not proof that every candidate endpoint pair is already executable.

### 3.3 Concepts not to promote accidentally

- A story-mentioned person, family, culture, faction or government remains a World Knowledge concept unless explicitly modeled as a supported formal spatial Object.
- A city district is not automatically a PoliticalRegion or Settlement. Its semantic meaning must be established first.
- A commercial, residential or industrial zone is not automatically a TerrainRegion. Terrain/land-cover and human land-use are different classification dimensions.
- A road intersection is not automatically a formal junction or network connection.
- A road route is not a Unit's movement route; movement, duration, path execution and transport simulation remain outside this workstream unless separately authorized by their domain contracts.
- A settlement's visual marker, label, icon or scale-dependent footprint is presentation, not a second Object.

## 4. Domain boundaries and design questions

The following are explicit investigation topics. They are not pre-decided schema changes.

### 4.1 Settlement identity, hierarchy and extent

ME-03 must determine:
- what makes a Settlement independently meaningful;
- how its anchor and optional extent are authored and refined;
- whether village / town / city / capital is sufficient for the initial hierarchy;
- when a parent/child settlement relation is meaningful versus mere containment;
- how a settlement may be represented at World, Region, Locality and Site scales without duplicate identity;
- how population estimates, condition and other changing facts are sourced and temporally scoped;
- how a settlement can be placed before its detailed boundary is known.

Default design constraint: use the existing Settlement type and bounded classification first. Do not introduce separate City, Town, Village, Capital or District types merely to mirror familiar labels.

### 4.2 Political and administrative geography

ME-03 must distinguish:
- a territory or administrative boundary;
- a political entity / governing actor;
- control, sovereignty, membership and succession;
- administrative subdivision;
- historical boundary change versus a current shared/base definition change.

The current PoliticalRegion Object is a territory, not the actor that governs it. The existing `political.controlled_by` relationship is explicitly deferred because the seed catalog has no formal Polity/Government endpoint. ME-03 must not fabricate such an endpoint or store political control as an unqualified scalar attribute. If the creator's scenarios require a governing actor, define the minimal semantic prerequisite and record the required Core Domain follow-up.

Time-varying sovereignty, control and succession must be modeled as temporal facts in the selected Map/Branch History once supported; they must not mutate a Project-shared definition to encode a historical event.

### 4.3 Land use and functional areas

ME-03 must decide how to represent human use of land, including candidate concepts such as:
- residential, commercial and industrial areas;
- agricultural land and managed fields;
- ports, markets, civic areas and transport facilities;
- mixed-use and special-purpose zones.

The design must distinguish:
- physical terrain / land cover (ME-02);
- human use or planned function (ME-03);
- political / administrative territory;
- visual style and cartographic presentation.

Do not assume that every functional zone needs a new Object Type. Evaluate whether it is best represented as a bounded typed Object, a relationship, a formal attribute, or a derived/presentation-only grouping. Any overlap, nesting, temporal validity and cardinality rules must be explicit. The same area may have distinct physical and human-use classifications without conflating them.

### 4.4 Roads, paths and transport network semantics

ME-03 must establish:
- the semantic meaning and identity of a Road;
- whether paths, tracks, streets, highways, bridges, ferries and other transport links fit a bounded Road classification or require distinct concepts;
- how a road is represented as one semantic feature with one or more geometry components;
- what constitutes a network endpoint, junction, access point or connection;
- whether an intersection is at-grade, bridge, tunnel, crossing without connection, or merely geometric overlap;
- whether a connection is symmetric or directed and whether direction/cost/capacity are within V1;
- how road connectivity to Settlements is represented and validated;
- how network edits behave across Map Initial State and History-local changes.

The initial design should prefer the existing Road Object and `network.connects_to` relationship where their semantics fit. A junction Object, explicit network-node identity, directed edge graph or separate route entity must not be added unless concrete editing scenarios prove it necessary and the Core Domain descriptor/relationship model can support it.

No generic routing engine, traffic simulation, congestion model, travel-time prediction, logistics simulation or autonomous Unit movement is part of the default V1 scope.

### 4.5 Settlement placement and natural-geography constraints

ME-03 consumes ME-02 facts and ME-05 spatial services to provide creator-facing placement guidance.

Candidate constraints include:
- slope and terrain suitability;
- water access and floodplain awareness where supported;
- coast, river and lake proximity;
- mountain passes and barriers;
- local climate/ecological context;
- access to roads, crossings and neighboring settlements.

These checks must disclose their source coverage, assumptions, precision and uncertainty. A suitability result is guidance, not an assertion that a settlement exists or that a creator's fictional exception is invalid. The system must preserve confirmed creator facts even when they contradict ordinary physical expectations, while making the conflict understandable.

ME-03 must not duplicate elevation, hydrology or climate algorithms. It consumes ME-02 outputs through their published contracts.

## 5. Shared spatial services and integration rules

ME-03 consumes, rather than redefines:

- ME-05-02: stable geometry component identity, typed geometry operations and write-context separation;
- ME-05-03: spatial predicates, boundary semantics, adjacency candidates, topology/relationship separation and indeterminate outcomes;
- ME-05-04: coordinate-reference identity, typed coordinate pipeline, precision and measurement contracts;
- ME-05-05: immutable geometry/field Asset publication and recovery protocol;
- ME-05-06: Core Domain / Map Engine / Editor / AI ownership boundaries and implementation gates.

Specialist validation may be stricter than a shared predicate. For example:
- a geometric line intersection may be a road-crossing candidate, not proof of a junction;
- a settlement anchor inside a political boundary may be evidence for a containment proposal, not proof of administrative membership;
- two road endpoints within a tolerance may be a connection candidate, not a formal network edge;
- a settlement near a river may satisfy a proximity query, but not imply a port, crossing, water-access right or historical relationship.

All query results used for editing must retain scene generation/source fingerprint freshness. Before formal application, Core Domain re-resolves all targets and validates the current descriptor, history context and authorization.

## 6. Creator-facing workflow principles

ME-03 is story-led and scenario-led. The creator should not need to understand GIS terminology to use it.

A creator should be able to express:
- "Place a small fishing village on this sheltered bay."
- "This market town grew where the old road crosses the river."
- "The capital sits inland, protected by the surrounding hills."
- "Show the residential and commercial parts of the city."
- "Connect these settlements with an old trade road."
- "This province used to include the valley, but the border changed later."

The editor should translate these intentions into explicit proposed Objects, geometry, relationships and validations. It must show:
- what is a creator-confirmed fact;
- what is an inferred assumption;
- what is a derived suitability or spatial query result;
- what is a Candidate awaiting review;
- what will become formal state and which Map/Branch/time it affects.

The shared desktop shell remains Canvas-centric, with the existing layer/navigation framework, contextual Inspector and bottom AI conversation. ME-03 should contribute tools, inspector sections, diagnostics and mode-specific layer presets, not another application shell.

## 7. Recommended work packages and sequence

| Work item | Focus | Required output |
|---|---|---|
| ME-03-A | Domain inventory and descriptor reconciliation | Map every settlement/urban concept to existing Object Types, attributes, geometry roles, relationships, World Knowledge, derived results or deferred concepts; identify exact descriptor gaps |
| ME-03-B | Settlement model and authoring workflows | Settlement identity/classification, anchor/extent, hierarchy boundary, placement/refinement and multi-scale behavior |
| ME-03-C | Political and administrative geography | Territory versus governing actor, administrative subdivision, temporal control/succession prerequisites and boundary-change semantics |
| ME-03-D | Land use and functional areas | Human-use classification, area/overlap/nesting semantics, relationship to TerrainRegion and presentation |
| ME-03-E | Road and path network authoring | Road semantics, segment/component behavior, junction/crossing/connectivity model, explicit network relationships and bounded validation |
| ME-03-F | Settlement placement and urban spatial validation | Natural-geography constraints, access/connectivity checks, uncertainty, diagnostics and bounded recommendations |
| ME-03-G | UX scenarios and acceptance | End-to-end creator journeys, Canvas/Inspector interactions, Candidate review, failure/uncertainty states and Figma handoff |
| ME-03-H | Cross-architecture integration review | Reconcile Core Domain, ME-01, ME-02, ME-05, Map Engine and AI; findings, implementation gates and package disposition |

The work sequence may be adjusted when an earlier package exposes a necessary dependency. A domain inventory precedes any proposal to add Object Types or Relationship Types. UX acceptance is developed from actual specialist workflows, not as an isolated screen catalog.

## 8. Explicit V1 non-goals

Unless a later approved domain contract changes the scope, ME-03 does not include:
- full GIS data management, arbitrary spatial SQL or user-authored geoprocessing;
- traffic flow, congestion, travel-time or economic simulation;
- vehicle/army/Unit movement and route execution;
- general-purpose logistics, trade-volume or population simulation;
- a universal graph database or persistent topology fabric;
- arbitrary custom executable Object Types or Relationship Types;
- a full cadastral parcel, zoning law, property ownership or land registry system;
- automatic conversion of every drawn boundary or intersection into a semantic Object;
- forced regular city grids or one universal urban morphology;
- hard physical rules that reject fictional, magical or intentionally exceptional creator-authored places.

## 9. Cross-architecture acceptance criteria

The ME-03 package is not complete until its review can demonstrate:

1. Every formal settlement, road and political territory uses Core Domain identity and the existing shared Object/State model.
2. Settlement classification is not confused with separate Object Types or Working Scopes.
3. Settlement anchor and extent are distinct, complementary geometry roles.
4. Political/administrative territory is not conflated with a governing actor or a time-varying control relationship.
5. Human land use is distinct from physical terrain/land cover and presentation.
6. Road connectivity is explicit; geometric overlap, endpoint snapping and proximity cannot silently create network relationships.
7. Crossings, bridges, tunnels and non-connecting intersections are representable or explicitly unsupported with understandable diagnostics.
8. A settlement placement suggestion cites its relevant natural-geography inputs and uncertainty and never overwrites creator-confirmed facts.
9. ME-03 consumes ME-05 predicates and coordinate/measurement contracts without local semantic forks.
10. All formal writes use the correct Core Domain target and registered typed operations.
11. Map/Branch history, Branch inheritance and shared-definition semantics remain intact.
12. Candidate acceptance, authorization, submission and successful application remain distinct states.
13. Multi-scale views refine one semantic Object rather than creating scale-specific duplicates.
14. The mode works within the shared editor shell and remains understandable to a creator without GIS expertise.
15. Offline/manual authoring remains possible; AI assistance is an enhancement, not a prerequisite.

## 10. Open gates and status discipline

The following are intentionally open until the relevant work package produces evidence:

- whether a dedicated land-use / functional-zone Object Type is required;
- whether Settlement-to-Settlement hierarchy needs an enabled formal Relationship Type;
- whether a PoliticalRegion can be used for specific administrative subdivisions without conflating territory and governing actor;
- whether a formal Polity/Government concept is necessary for the intended political workflows;
- whether road junctions require independent semantic identity or can be expressed through typed connectivity relationships;
- exact Road endpoint roles, connection cardinality, directionality and crossing semantics;
- exact enabled Relationship Type endpoint matrices and temporal classes;
- which settlement suitability checks can be supported by current ME-02 outputs;
- numeric thresholds, precision and algorithm/library choices;
- exact descriptors, Commands, schema and persistence changes;
- Windows/macOS runtime and performance evidence.

A recommendation in an ME-03 working document does not automatically adopt a Core Domain Object Type, enable a Relationship Type, freeze a schema/API or authorize implementation. Those changes require reconciliation with their owning contracts and explicit status recording.

## 11. References

- ME-01-01 through ME-01-07 — unified editor architecture, story/spatial bridge, initialization, multi-scale model, Object/geometry model, interaction and cross-architecture review.
- ME-02 V0.6 and ME-02-A through ME-02-H — natural geography, terrain, hydrology, climate/ecology, calculation and UX contracts.
- ME-05-01 through ME-05-06 — Field Dataset authority, shared geometry, predicates/topology, coordinate/measurement, Asset publication and cross-architecture integration.
- P2-09-18 V0.2 — Object Type Descriptor / Attribute Contract.
- P2-09-20 V0.2 — Relationship Descriptor / Temporal Contract.
- P2-09-21 V0.1 — V1 Built-in Object Type Seed Catalog.
- P2-09-22 V0.2 — V1 Built-in Relationship Type Seed Catalog.
- P2-09-24 V0.1 — V1 Built-in Object Type Field and Capability Catalog.
- P2-09-26 V0.2 — V1 Object Type Descriptor Registry.
- P2-09-27 V0.2 — V1 Relationship Type Descriptor Registry.

**Disposition:** ME-03 is opened as a specialist design workstream. This document establishes its scope, inherited constraints, work packages and open gates; it does not claim completion of any specialist domain contract or implementation.
