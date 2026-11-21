# World View — ME-01-04 Multi-scale Spatial Model & Global Consistency

- Version: 0.1
- Date: 2026-11-21
- Status: Architecture Working Baseline — recommended V1 model; physical schema, exact geometry contracts, algorithms and numerical thresholds remain open
- Scope: Multi-scale spatial scopes, representation detail, cross-scale relationships, progressive refinement, global/regional consistency, bounded dependency impact and incremental recalculation
- Depends on: ME-01-01 Editor Overall Architecture; ME-01-02 World Knowledge Model & Story-to-Spatial Bridge; ME-01-03 Guided Initialization; Core Domain P2-09 logical persistence model and Command/Event/State Change contract; Map Engine natural geography and spatial interaction contracts; AI Assistant Task Planning, Candidate/Validation and Command Proposal boundaries

## 1. Executive recommendation

World View should use **one continuous Map Space, one authoritative Core Domain world state, and multiple linked spatial scopes with scale-appropriate representations**.

World, Region, Locality and Site are not four map types, independent databases, or mutually exclusive partitions. They are working scopes used to navigate, author, validate and present the same world at different extents and levels of detail.

V1 should adopt five rules:

1. **One spatial truth:** every formal spatial fact belongs to the shared Map/Core Domain model. Scale, viewport, layer and visual style do not create alternate facts.
2. **Scope is not resolution:** a Region is a meaningful spatial scope; a zoom level or geometry simplification is a representation choice. Do not conflate them.
3. **Progressive refinement:** coarse content can exist before detailed content. Refinement adds detail while preserving identity, parent context and explicit constraints.
4. **Bounded consistency:** local changes trigger checks only for their direct and transitive dependants within a justified impact boundary, not unconditional world regeneration.
5. **Derived until adopted:** analysis and generated outputs remain derived/Candidate data until the creator chooses to establish them as formal map facts through Core Domain.

The system should not attempt a general-purpose GIS topology server, global simulation framework, arbitrary user-defined dependency graph, or distributed recomputation engine in V1.

## 2. Goals and non-goals

### 2.1 Goals

- Represent world-scale, regional, local and site-scale work in one Map Space.
- Support different coordinate extents and useful detail at each scale without splitting the world into disconnected maps.
- Preserve semantic Object identity as geometry and detail are refined.
- Allow the creator to start with a story, sketch, rough world outline or detailed local area.
- Keep a selected locality consistent with relevant regional and world-scale facts.
- Make the effects of upstream edits understandable and reviewable.
- Recalculate or revalidate bounded affected content without touching unrelated regions.
- Preserve confirmed creator intent and distinguish it from AI inference and derived analysis.
- Respect Map, Branch and Historical Time reconstruction semantics owned by Core Domain.

### 2.2 Non-goals for V1

- A complete GIS geodatabase, geospatial ETL suite or professional cartographic production system.
- A mandatory global equal-area/equidistant tiling scheme.
- Infinite zoom or arbitrary precision.
- Full plate tectonic, atmospheric, oceanic, hydrological or ecological simulation.
- A generic graph-processing platform or user-programmable rule engine.
- Automatic resolution of every conflict between creator statements.
- Automatic creation of semantic Objects merely because a derived feature or story mention exists.
- Full-world recalculation after every local edit.
- Cross-Map live spatial dependency propagation.
- A separate scale-specific map file or spatial database.

## 3. Four working scopes

The following scopes are the default authoring and reasoning levels. They are navigation and analysis concepts, not a mandatory object hierarchy.

| Scope | Typical content | Typical questions |
|---|---|---|
| World | world extent, oceans, continents, major landmasses, global climate drivers, large mountain systems and broad circulation assumptions | Where are the major land and water masses? What global factors constrain regional climate? |
| Region | mountain systems, major watersheds, regional climate, ecological zones, political/cultural regions | How does this region drain? What climate and terrain patterns are plausible here? |
| Locality | city and settlement areas, local terrain, roads, land use, river reaches, districts and building groups | Where should this town sit? How do roads, water and terrain relate locally? |
| Site | building footprint, floors, rooms, caves, entrances, facilities and detailed local geometry | How are spaces connected? Does this building fit its site and access constraints? |

### 3.1 Scopes may overlap and cross boundaries

A scope is a bounded question or authoring context, not necessarily a permanent parent object.

Examples:

- A watershed may cross several political regions.
- A mountain range may cross multiple regional authoring scopes.
- A trade route may connect localities across regions.
- A city may be inside a political region while its watershed and ecological context extend far beyond it.
- A building site is usually inside a locality, but an underground cave system may extend beyond the visible site boundary.

Therefore, the model must support:
- containment where it is semantically correct;
- spatial intersection/overlap where relevant;
- network or flow relationships across scopes;
- explicit cross-boundary references.

Do not force all spatial relationships into one parent-child tree.

### 3.2 Scope boundary versus Map Extent

Keep these concepts separate:

- **Map Extent:** the formal spatial coverage of the Map.
- **Working Scope:** the subset currently being authored, inspected or analyzed.
- **Canvas View:** the viewport currently visible on screen.
- **Export Area:** a region selected for output.

Zooming or changing the Working Scope does not change Map Extent. Extending Map Extent is a formal authoring operation. An analysis may use a buffer outside the visible Working Scope when needed to evaluate boundary effects, but it must not silently expand the Map's formal extent.

## 4. Scale, resolution and representation detail

### 4.1 Three independent dimensions

The implementation must distinguish:

1. **Geographic extent** — how much of the world is included.
2. **Data resolution / detail** — the smallest spatial variation the source data can meaningfully represent.
3. **Display scale / level of detail (LOD)** — how much detail is drawn at the current viewport.

These dimensions are related but not interchangeable. A world-scale view may display a simplified coastline derived from detailed source geometry. A locality may show a detailed river reach while still referencing the same river Object and broader river network.

### 4.2 Authoritative geometry and display generalization

For V1:

- Core Domain owns the formal geometry or its authoritative immutable asset reference according to the applicable object contract.
- Map Engine may create simplified, tiled, clipped or otherwise optimized geometry for display and analysis.
- Display generalizations are derived assets, not new semantic Objects and not replacements for authoritative geometry.
- A simplified line at world scale must retain a traceable link to the source geometry/object.
- Editing a display-only simplification must not silently overwrite authoritative geometry. The editor must route the edit to a valid authoring representation or explicitly promote a new geometry result through the formal change path.

Do not require every Object to store four separate copies of geometry for the four scopes.

### 4.3 Progressive geometry refinement

Refinement should preserve identity by default:

- A rough continent outline can be refined into a more detailed coastline while retaining the same semantic identity where the domain semantics allow.
- A rough mountain-belt region can gain ridge lines, peaks and elevation detail without automatically creating a new mountain-range Object for every segment.
- A river can gain tributaries and more detailed channel geometry while preserving its identity and network relationships.
- A city region can gain districts, roads and buildings as additional semantic Objects when the creator or a justified workflow establishes them.

Geometry subdivision is not automatically semantic decomposition. A new semantic Object requires an explicit type/identity creation operation through Core Domain.

### 4.4 Resolution is explicit where it matters

V1 should not demand one universal numeric resolution for all fictional worlds. Instead, every generated or imported dataset that depends on resolution should carry a small, explicit resolution descriptor appropriate to its data kind, for example:

- source scale / intended use;
- cell size or sampling interval for raster data;
- geometric tolerance or precision class for vector data;
- vertical/elevation interval where relevant;
- coverage and no-data boundary.

The exact units and physical representation belong to ME-05 and Map Engine contracts. Unknown resolution must remain unknown; do not fabricate precision from a visual preview.

## 5. Cross-scale relationships

### 5.1 Relationship categories

Use a small set of explicit relationship semantics rather than treating every association as containment:

| Relationship | Meaning | Example |
|---|---|---|
| Contains / is within | semantic or administrative containment | City within a region; room within a floor |
| Part of / component of | domain decomposition | Tributary as a component of a river network; district as part of a city |
| Intersects / overlaps | spatial overlap without ownership | Watershed intersects a political boundary |
| Connects to | network or access relationship | Road connects two settlements; corridor connects rooms |
| Flows to / drains to | directed hydrological relation | Stream flows into a river; watershed drains toward a lake |
| Derived from / constrained by | analytical provenance or dependency | Biome suitability derived from climate and terrain |
| Adjacent to / near | spatial relation without containment | Two neighboring regions |

The exact relation vocabulary and persistence shape are owned by Core Domain / ME-05. This table defines the semantics ME-01 requires, not a final schema.

### 5.2 Parent context and local detail

A local scope inherits relevant context from its containing or associated larger scopes, but inherited context is not copied into a second local authority.

Examples:

- A locality reads the regional elevation/climate context relevant to it.
- A building site reads local terrain, access and settlement context.
- A region reads world-scale latitude, ocean distance, circulation and large terrain context.

Local data may refine or qualify larger-scale assumptions. It may not silently contradict a confirmed hard constraint. Where a detailed local fact exposes a conflict with a coarse assumption, the system records the conflict and asks whether the coarse assumption should be revised.

### 5.3 No automatic identity explosion

A scope boundary, tile boundary, watershed boundary or LOD transition must not create new semantic Object identities by itself.

Semantic decomposition is appropriate only when the creator's world model benefits from independently meaningful objects, such as districts, tributaries, buildings or rooms. It is not a side effect of clipping, tiling, simplification or rendering.

## 6. Constraint model and evidence classes

Consistency requires knowing what kind of information a rule represents. V1 should use the following conceptual classes, aligned with ME-01-02 World Knowledge and the Guided Initialization workflow.

### 6.1 Hard constraints

A hard constraint is a confirmed condition that a proposal must preserve unless the creator explicitly changes it.

Examples:
- a confirmed island must remain surrounded by water;
- a mountain range is established as a fixed world fact for the current authoring baseline;
- a named settlement must remain at its confirmed location;
- a site must remain inside a specified territory where that containment is canonically required.

A hard constraint can block application. Only an explicit creator decision can amend a creator-confirmed hard constraint. The system must show the affected downstream consequences before such an amendment is applied.

### 6.2 Soft preferences

A soft preference expresses desired qualities rather than an absolute rule.

Examples:
- a capital should be near a major river;
- settlements should generally favor fertile lowlands;
- a route should avoid steep terrain when a reasonable alternative exists.

Soft preferences guide ranking and recommendation. They can be traded off and do not independently block a result unless the creator explicitly promotes them to hard constraints.

### 6.3 Derived results

A derived result is computed from known inputs using a deterministic or explicitly versioned analysis method.

Examples:
- drainage direction;
- watershed boundary;
- estimated precipitation distribution;
- ecological suitability;
- terrain slope or visibility.

Derived results must record their source inputs, method/algorithm identity and relevant parameter context. They are invalidatable and rebuildable. They are not automatically creator-confirmed facts.

### 6.4 Unresolved and inferred information

- **Inferred:** AI's best interpretation, with provenance and uncertainty; not a confirmed fact.
- **Unresolved:** insufficient or conflicting evidence; it remains open.
- **Unknown by design:** the creator has chosen not to specify it yet.

Unresolved information should block only work that materially depends on it. Independent work may proceed. Confidence scores do not replace provenance, confirmation state or impact analysis.

## 7. Global-to-local consistency model

### 7.1 Context envelope for a local operation

Every regional/local/site generation or edit operation should receive a bounded context envelope assembled from authoritative and derived sources:

- target Map, Branch and Historical Time;
- target Working Scope and its formal extent;
- relevant parent/associated scope references;
- nearby formal Objects and geometry within a justified buffer;
- applicable hard constraints and soft preferences;
- relevant derived context (terrain, drainage, climate, ecology, access, etc.);
- known cross-boundary relationships;
- unresolved assumptions that materially affect the operation;
- source/provenance and freshness status of each input.

The context envelope is a read/analysis package, not a new authority. It should be produced by application services / Context Builder from Core Domain and Map Engine outputs. It must not be assembled by unrestricted LLM access to persistence.

### 7.2 Boundary conditions

Local generation must not behave as if the visible canvas were the whole world.

Where relevant, analysis uses:
- the actual world/map boundary;
- neighboring terrain and hydrological context;
- upstream/downstream river context;
- regional wind, ocean and elevation context;
- nearby settlements, roads and land-use constraints.

If context outside the Map Extent is unknown, the system must mark the boundary assumption. It must not invent neighboring geography merely to make a calculation appear complete.

### 7.3 Coarse-to-fine workflow

Recommended default progression:

1. Establish a coarse world skeleton and major constraints.
2. Establish regional terrain, major watersheds and climate drivers.
3. Refine selected localities and networks.
4. Add site/building/interior detail.
5. Re-run only affected cross-scale checks after each meaningful edit.

This is not a rigid top-down lock. A creator may start with a city, castle, island or story location. The system can build a provisional local context, mark larger-scale assumptions as unresolved, and progressively reconcile them when the broader world is defined.

## 8. Dependency and impact model

### 8.1 V1 recommendation: bounded dependency records

Use explicit dependency records for important derived outputs and deterministic dependency discovery for obvious spatial relations. Do not introduce a general-purpose graph engine.

A conceptual dependency edge means:

> Derived output B depends on source A under a known spatial/temporal scope and method.

Examples:
- precipitation estimate depends on elevation, latitude, wind assumptions and ocean-distance context;
- watershed output depends on elevation surface and drainage boundary assumptions;
- biome suitability depends on climate, terrain, water availability and soil assumptions;
- a route recommendation depends on terrain, water crossings, settlement locations and creator preferences.

Dependency records should be coarse enough to maintain, but specific enough to invalidate only affected outputs.

### 8.2 Dependency metadata

A derived result should conceptually retain:

- result identity/type;
- source Object/asset/constraint references;
- source content fingerprints or Core Domain context references;
- Map/Branch/Historical Time context;
- spatial coverage and optional buffer;
- algorithm/method identifier and version;
- parameters and boundary assumptions;
- generation timestamp (transaction time);
- validity state: current, stale, invalid, unavailable or superseded;
- diagnostic/provenance references.

This is a derived-data contract, not a mandate to create a new Core Domain entity. Derived caches may live in Map Engine/application-owned storage. Formal semantic facts remain Core Domain-owned.

### 8.3 Impact propagation

When a source changes:

1. Identify the changed source and its affected spatial/time scope.
2. Find direct dependent results whose coverage intersects the source impact.
3. Traverse only relevant downstream dependencies.
4. Expand the impact boundary where an algorithm requires a buffer or network reach (for example, upstream drainage).
5. Mark affected derived results stale; do not delete their provenance.
6. Recompute only required outputs, in dependency order.
7. Re-run relevant validators against confirmed constraints and neighboring formal content.
8. Present material conflicts and affected formal Objects to the creator.
9. Keep unaffected results current.

A dependency can be spatially local, network-connected, regional or world-scale. The impact analyzer must use the dependency's actual semantics rather than assuming every dependency is a simple geometric overlap.

### 8.4 Example: changing a mountain range

A mountain-range edit may affect:

- local elevation and slope;
- ridge continuity and adjacent terrain;
- windward/leeward precipitation;
- drainage divides and river sources;
- downstream watershed and river connectivity;
- regional climate and ecological suitability;
- settlement suitability and route recommendations.

It does **not** automatically require:
- regenerating every continent coastline;
- rebuilding unrelated watersheds;
- relocating every settlement in the world;
- changing confirmed political boundaries.

The impact analyzer should identify the affected region and any connected hydrological network reach, mark only relevant derived results stale, and surface formal-object conflicts for review.

### 8.5 Example: editing a building

A room-layout edit may affect:
- floor-plan geometry and room connectivity;
- doors, corridors, stairs and vertical circulation;
- building-level accessibility or structural validation.

It should not trigger terrain, climate or world-scale hydrology analysis unless the edit changes the building footprint/site relationship or another explicit dependency.

## 9. Deterministic calculation versus LLM planning

### 9.1 Deterministic services

Use deterministic, testable Map Engine / domain services for:
- coordinate transformations and measurements;
- geometry validity, clipping, buffering and spatial predicates;
- topology and network connectivity;
- terrain derivatives such as slope and aspect;
- drainage and watershed analysis;
- climate approximations based on explicit natural drivers;
- ecological suitability calculations;
- dependency intersection and invalidation;
- bounded spatial consistency checks.

Where a result depends on an algorithm version, parameters and input data, those must be identifiable so the result can be reproduced or explicitly invalidated.

### 9.2 AI Assistant

The LLM may:
- interpret story and creator intent;
- identify relevant scales and spatial dependencies;
- select suitable analysis capabilities;
- plan operation order;
- determine which unresolved questions materially block progress;
- explain causal results in creator-friendly language;
- propose alternatives and summarize trade-offs;
- interpret validation findings and prepare Candidates / Command Proposals.

The LLM must not invent numeric terrain, hydrology or climate outputs as if they were calculated by a physical model. It must not bypass deterministic validators or turn an inference into a confirmed constraint.

### 9.3 Task planning

The AI Assistant Task Planning & Execution Engine coordinates the work. A plan may contain:
- independent operations that can proceed in parallel;
- dependent operations that must wait for upstream results;
- analysis-only steps;
- clarification steps;
- candidate-generation steps;
- validation and review steps.

This uses the existing Task/Plan/Step and Candidate/Validation lifecycle. ME-01-04 does not create another workflow engine.

## 10. Incremental recalculation lifecycle

V1 should follow this simple lifecycle:

1. **Source changed** — a formal Core Domain projection changed, a source asset was replaced, or an assumption/parameter changed.
2. **Impact identified** — calculate the affected spatial/time scope and dependency closure.
3. **Stale marked** — mark affected derived results stale; unaffected outputs remain current.
4. **Plan formed** — choose the minimum necessary deterministic operations and validations.
5. **Recalculate** — rebuild required derived outputs in dependency order.
6. **Validate** — compare results with hard constraints, soft preferences and neighboring formal content.
7. **Review** — present material conflicts, unresolved boundary assumptions and downstream consequences.
8. **Apply if needed** — only selected formal changes are converted to Core Domain-supported Commands and pass the existing authorization/submission boundary.

A recalculation can finish without any formal map change. A new derived climate estimate or watershed analysis is not automatically a historical State Change.

### 10.1 Failure and partial completion

- A failed derived calculation must not corrupt authoritative Core Domain data.
- The last known derived result may remain available for inspection but must be clearly marked stale/invalid.
- A partial run must identify which outputs are current and which remain stale.
- Retrying a deterministic calculation must not create duplicate formal Objects or Commands.
- A failure in one independent region should not invalidate successful unrelated calculations.
- If required upstream data is missing, report the exact missing dependency or boundary assumption rather than silently substituting invented data.

## 11. Historical Time and Branch consistency

Spatial and environmental results are meaningful only in a defined world-state context.

- Every formal read and analysis must identify the selected Map/Branch and Historical Time.
- A derived result must not be reused across different Branches or Historical Times unless its inputs are proven identical under Core Domain reconstruction semantics.
- Branches inherit parent history only through the Core Domain fork boundary. ME-01-04 must not implement its own Branch overlay or history merge.
- An ancestor-history edit may invalidate derived results for descendants whose inherited state changes.
- A Branch-local edit must not invalidate its parent or sibling Branches unless they share an explicitly shared non-historical input whose own authority rules require it.
- Shared Object base data and post-zero Map/Branch runtime changes must not be conflated. The Core Domain's current scope-resolution contract decides which history is affected.
- Transaction timestamps for derived computation are not Historical Time.

If an analysis is intended to describe a stable physical property across a period, its temporal assumptions must be explicit. Do not silently treat a present-day derived result as valid for every historical period.

## 12. Conflict handling and creator authority

When a local edit conflicts with larger-scale context:

1. Determine whether the conflict is geometric, topological, physical/causal, temporal, or semantic.
2. Determine whether the larger-scale input is confirmed, inferred, derived or unresolved.
3. If the conflict involves only stale derived output, recalculate it.
4. If a soft preference is violated, show the trade-off and offer alternatives; do not block automatically.
5. If a hard confirmed constraint is violated, block the conflicting formal application or request an explicit creator decision to change the constraint.
6. If two confirmed constraints conflict, preserve both records and ask the creator to resolve the contradiction.
7. If a conflict is low-impact and can remain unresolved, allow independent work to continue.

The system must never silently move a confirmed settlement, reroute a confirmed river, rewrite a confirmed mountain range or reinterpret canon merely to make a generated result appear consistent.

## 13. Preview, diagnostics and review UX requirements

For a generation/edit operation, the creator should be able to understand:

- target scope and the larger context used;
- what was preserved, added, changed, inferred and left unresolved;
- which derived outputs were recalculated;
- which downstream areas or Objects may be affected;
- which hard constraints were checked;
- which soft preferences were optimized or traded off;
- whether any source data was stale or unavailable;
- whether the result is analysis-only, a Candidate, or a formal applied change.

Diagnostics should be spatially inspectable: the user can select an affected area/object and see the cause, source, method and proposed remedy. Do not expose GIS jargon as a prerequisite for understanding the result.

## 14. V1 architecture and implementation boundary

### Core Domain
- owns formal Map/Branch/Object identities, authoritative geometry references, formal relationships, Commands and historical reconstruction;
- resolves Map/Branch/Historical Time context and authoring target;
- applies formal changes only through Domain Services;
- does not treat derived caches as formal history.

### Map Engine
- owns deterministic geometry/spatial algorithms and derived analysis computation;
- produces reproducible, bounded outputs and validation evidence;
- may cache display and analysis artifacts, but cannot own formal world facts.

### AI Assistant
- interprets intent, builds context, plans tasks, explains and stages Candidates;
- uses existing Task Planning, Candidate/Validation, Change Set and authorization mechanisms;
- cannot directly mutate Core Domain persistence or self-authorize formal changes.

### Editor
- provides scope navigation, multi-scale canvas, object inspection, diagnostics, candidate preview and review;
- distinguishes formal objects from derived overlays and staged candidates;
- does not infer semantic containment from visual grouping.

### Persistence
- retain the existing one project-local SQLite authority plus immutable Asset Store/checkpoint direction;
- do not add a separate scale database or a global spatial server;
- derived indexes, LOD geometry and analysis products are rebuildable caches with explicit provenance/freshness;
- exact placement of derived cache metadata is a later module-ownership/schema decision.

## 15. Recommended V1 implementation slice

Implement in this order:

1. **Scope context:** Map/Branch/Time + World/Region/Locality/Site working scope and Map Extent separation.
2. **Spatial relationships:** support the minimum containment, intersection, adjacency, connectivity and flow semantics required by initial geography and settlements.
3. **Geometry detail metadata:** source geometry versus derived LOD/generalization, with traceability and explicit precision/coverage.
4. **Constraint/evidence classification:** hard, soft, derived, inferred and unresolved states linked to provenance.
5. **Bounded dependency invalidation:** explicit dependencies for high-value derived outputs; spatial/network-aware impact bounds.
6. **Deterministic recalculation:** geometry/topology first, then terrain/hydrology, then climate/ecology approximations.
7. **Review diagnostics:** stale status, affected scope, cause chain, constraint violations and candidate preview.
8. **Historical context tests:** Map/Branch/Time-aware cache keys and ancestor/branch invalidation behavior.

Do not implement a generic dependency editor or simulation designer before concrete use cases prove they are necessary.

## 16. Acceptance criteria

1. The same semantic Object can be inspected at World, Region, Locality and Site scales without creating duplicate identities.
2. Scope, Map Extent, Canvas View and Export Area remain distinct.
3. A Region or watershed can cross other scope boundaries without being forced into a single containment tree.
4. Geometry can be generalized for display without replacing authoritative geometry.
5. Local generation uses relevant parent, neighboring and cross-boundary context.
6. A local edit does not trigger unconditional whole-world recalculation.
7. A mountain-range edit invalidates only relevant terrain, climate, drainage, ecology and affected suitability outputs, including necessary connected downstream areas.
8. An unrelated building interior edit does not trigger natural-geography recalculation.
9. Hard constraints, soft preferences, derived results, inferred information and unresolved facts remain distinguishable.
10. A derived result records enough provenance and method context to determine whether it is stale and how it can be rebuilt.
11. Branch and Historical Time context are included in analysis and derived-cache validity.
12. Conflicts with confirmed creator constraints are surfaced and never silently overwritten.
13. Candidate/analysis results remain separate from formal Core Domain state until the existing authorization and application flow is completed.
14. A creator can start from a local story location before the global map is complete; missing broader context is explicitly marked rather than fabricated.

## 17. Open decisions for later work

The following are intentionally not frozen by this working baseline:

- exact coordinate reference and projection policy for fictional worlds;
- geometry precision classes and raster resolution metadata;
- formal Core Domain representation of cross-scale spatial relationships;
- whether scope definitions are persisted objects, derived query contexts or a combination;
- dependency metadata schema and ownership between Map Engine and application services;
- exact spatial/network impact-bound algorithms;
- numerical thresholds and validation severity policy for terrain, hydrology, climate and ecology;
- algorithm/version compatibility and deterministic reproducibility details;
- cache key and invalidation implementation;
- behavior for maps with intentionally incomplete or unknown outer boundaries;
- detailed UI for multi-scale navigation and impact review.

These decisions belong to ME-01-05, ME-01-06, ME-05 and ME-07, plus the relevant Core Domain / Map Engine implementation contracts. This document provides the product and architecture constraints they must satisfy.

## 18. Relationship to the ME-01 package

- ME-01-01 defines one editor, shared spatial model and the boundaries among Editor, Core Domain, Map Engine and AI Assistant.
- ME-01-02 defines world knowledge, provenance and story-to-spatial interpretation.
- ME-01-03 defines Guided Initialization, staged generation and incremental refinement.
- ME-01-04 defines how scopes, detail, constraints and dependencies remain coherent across scales.
- ME-01-05 will define the semantic Object, geometry and representation model in more detail.
- ME-01-06 will define the interaction framework for navigating, editing and reviewing across scales.
- ME-01-07 will verify consistency with the wider World View architecture.

ME-01-04 is a Working Baseline, not a frozen physical schema or implementation handoff.
