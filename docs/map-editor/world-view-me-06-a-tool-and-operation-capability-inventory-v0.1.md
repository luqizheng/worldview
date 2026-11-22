# World View — ME-06-A Tool & Operation Capability Inventory

- Version: 0.1
- Date: 2026-11-21
- Status: Capability inventory working baseline; runtime implementation evidence is not established by this document
- Depends on: ME-06-00 V0.1; ME-01-05/06/07; ME-02-A/H and ME-02-G; ME-03-A/G/H; ME-04-A/G/H; ME-05-02/03/04/05/06; Core Domain P2-09-15/20/26/27 and P2-09-37
- Authority: Editor/Application capability inventory only. This document does not adopt Core Domain descriptors, authorize Commands/schema/API changes, or claim runtime implementation.

## 1. Executive disposition

ME-06-A converts the specialist editor workflows into one finite built-in tool catalog and maps each user intent to its query, validation and formal-write boundary.

**The critical finding is that the design contracts are substantially defined, but the repository evidence reviewed here does not establish a complete, executable end-to-end tool path for any catalog row.** The Core Domain catalogs P2-09-15, P2-09-26 and P2-09-27 are explicitly recommended working catalogs rather than frozen executable registries; ME-05-06 likewise says the geometry, serializers, algorithms, schemas and platform tests are not thereby implemented. Therefore no row is marked `Enabled` under the strict runtime definition below.

This is not a claim that no code exists anywhere. It means ME-06-A has not been supplied with verifiable implementation evidence (registered runtime descriptor, typed operation handler, persistence/replay path and tests) sufficient to mark a capability Enabled. The correct runtime state is `Unverified` until that evidence is attached.

The design disposition is nevertheless clear:
- Shared geometry component operations are the first bounded formal-edit family, subject to the exact Object Type role/cardinality and write-context rules.
- Natural geography authoring can be split into creator-authored semantic Objects, source Field Dataset editing, and deterministic derived analyses. These are different tool families and write paths.
- Settlement, Road and PoliticalRegion are the only relevant existing urban semantic types in the current recommended registry. FunctionalArea and road endpoint semantics remain gaps.
- Building and Cave System are now covered by P2-09-37 as an adoption proposal, but the proposal does not make them executable. Level, Space, plans, boundaries and access remain typed aggregate/child capabilities requiring adopted services.
- Geometry predicates, hit-test, snap, measurement and validation evidence are read-only. They never create semantic relationships by coordinate coincidence.
- V1 tools are built-in and statically declared. No arbitrary plug-in runtime is introduced.

## 2. Status vocabulary and evidence standard

The status is about **runtime capability**, not whether a product workflow has been designed.

| Status | Meaning | Minimum evidence |
|---|---|---|
| `Enabled` | A user-facing tool can complete its declared path in the application | Adopted descriptor; typed intent and handler; correct Domain Service/Command; persistence and replay/publication path; automated tests; platform evidence where applicable |
| `Preview Only` | Read-only query or transient preview is available; no formal apply is implied | Implemented query/preview path with freshness and typed outcomes; no formal write claim |
| `Blocked` | A known domain/descriptor/identity/authority gap makes formal application unsafe or unrepresentable | Explicit P0/P1 finding or missing required typed contract |
| `Unsupported` | Deliberately outside V1 or not part of the product contract | Explicit non-goal/deferred decision |
| `Not Applicable` | The capability does not apply to the row | Clear scope distinction |
| `Unverified` | Design describes the capability, but sufficient implementation evidence is absent | Evidence gap recorded; do not infer from a Figma control or catalog entry |

No `Enabled` state is assigned in this inventory because no end-to-end runtime proof bundle was available in the reviewed evidence. `Preview Only` below is a product capability ceiling for the intended tool contract; it is not a claim that a running preview implementation has passed tests. Where that distinction matters, the row says so explicitly.

## 3. Shared tool contract

Every built-in tool declaration must identify:

1. Stable `tool_key`, owning editor mode and human-readable creator intent.
2. Gesture/action shape: click, point, path, area, brush, selection, inspector action or explicit command.
3. Allowed target Object Types, Geometry Component roles and coordinate context.
4. Typed Intent family; a Typed Intent is not a Core Domain Command.
5. Read dependencies: Map/Branch/Historical Time, Working Scope, scene generation, object/component revisions, Field Revision fingerprints, spatial reference, precision and algorithm version as relevant.
6. Query/operation dependencies and supported result classifications.
7. Validation rules, uncertainty, coverage and diagnostics.
8. Formal apply route: Project-shared definition, Map-local definition, Map Initial State, post-zero selected-History Command, or Map Source Field Baseline.
9. Authorization/risk policy, cancellation, local uncommitted undo and terminal outcome.
10. Reconciliation and idempotency behavior after submission.

The following rules apply to every specialist mode:
- No generic CRUD, arbitrary JSON patch or direct database write.
- Formal writes resolve the exact target context and revalidate source fingerprints at apply time.
- Editor transient state, AI Candidate staging and formal Core Domain state remain separate.
- A Candidate is risk-based, not mandatory for every low-risk manual edit.
- Local Undo only reverses uncommitted editor work. Formal reversal uses the owning history/definition mechanism.
- A stale asynchronous result is discarded; a preview is never silently upgraded into an apply authorization.
- Geometry split/merge is not emulated by sequences of add/remove.
- A geometry predicate, snap, intersection or shared coordinate never creates a semantic relationship.
- Field Revision publication updates the owning Map's source baseline retroactively across that Map's Branches and Historical Times; it is not a historical event and does not affect another Map.

## 4. Shared built-in tool catalog

The rows below are functional tools, not a button-by-button UI specification. A single tool may expose multiple gestures while preserving one intent and apply contract.

| Tool key | Mode / creator intent | Gesture and target | Typed Intent / query | Formal route and validation | Runtime state |
|---|---|---|---|---|---|
| `editor.select` | All modes — select semantic feature/component | Click, marquee; selectable scene items | Selection intent; Map Engine hit-test against immutable scene generation | No formal write; stale hit-test discarded | Unverified |
| `editor.pan_zoom_scope` | All modes — navigate map and change working scale/scope | Pointer drag, wheel, scope selector | View intent; viewport transform and scope resolution | Presentation/session state only | Unverified |
| `editor.inspect` | All modes — inspect object and evidence | Select + Inspector action | Read intent; resolve Object/Component/relationship and provenance | Read-only; no formal write | Unverified |
| `editor.measure` | All modes — measure distance/area/bearing | Point/path/area | Measurement intent; declared measurement model, units, CRS and error bound | Read-only result with source fingerprints; no formal write | Preview Only* |
| `editor.draw_geometry` | All modes — sketch or refine temporary geometry | Point/path/area/freehand where supported | Geometry draft intent; coordinate transform, snap and spatial predicates | Transient Edit Session; formal operation selected only after Object role/context resolution | Unverified |
| `editor.snap` | All modes — propose alignment to existing geometry | Pointer proximity | Snap query; hit-test/snap tolerance in viewport pixels, target geometry and generation | Preview only; never moves source or creates topology by itself | Preview Only* |
| `editor.validate_spatial` | All modes — explain spatial validity | Selection or proposed geometry | Predicate/validation query; typed True/False/Indeterminate/Unsupported/InvalidInput/StaleSnapshot outcomes | Read-only evidence; no automatic relationship creation | Preview Only* |
| `editor.review_candidate` | All modes — accept/reject proposal | Review panel; versioned Candidate | Candidate decision intent; exact proposal hash/version and dependency closure | Authorization then appropriate Domain Service; authorization invalidated on change | Unverified |

* `Preview Only` describes the maximum permitted authority of the tool. Concrete implementation and platform test evidence remains unverified.*

## 5. Specialist tool-to-operation matrix

### 5.1 Terrain & Natural Geography (ME-02)

| Tool key | Creator intent / target | Gesture and query dependencies | Typed Intent and formal operation | Validation / freshness / authorization | Runtime state |
|---|---|---|---|---|---|
| `terrain.land_water_field` | Paint/import land-water source field | Brush, polygon import, field package; Map extent, field descriptor, CRS, precision, tile/halo and current Field Revision | Field revision draft → Map Source Field Baseline publication; not a History Command | Validate coverage, no-data, units/categories, package integrity, expected current revision/CAS; invalidate dependent analyses across all Branches/Times of this Map | Unverified |
| `terrain.elevation_field` | Create/import/edit elevation source | Brush, raster import, control points; vertical reference, units, coverage, precision and Field Revision | Field revision draft → Map Source Field Baseline publication | Validate finite values, vertical reference, coverage, no-data, tiling/halo, source fingerprint and CAS; no invented altitude | Unverified |
| `terrain.semantic_feature` | Create/edit Landmass, MountainRange, MountainPeak or TerrainRegion | Point/path/area; exact type descriptor geometry role and cardinality | Object definition / Map Initial State / post-zero Object lifecycle and geometry component intents | Exact Object Type registry policy; stable identity reservation/binding; geometry Asset publication; no implicit Object split/merge | Unverified |
| `hydrology.river_lake` | Create/refine River or Lake geometry | Path/area; terrain/elevation/land-water Field Revision and spatial reference | River/Lake Object + geometry intent; `hydrology.flows_to` relationship intent only for registered endpoints | River DAG and current endpoint constraints; no River branching/delta, Lake outflow or sea endpoint unless adopted; relationship is explicit | Unverified |
| `hydrology.drainage_analysis` | Generate drainage direction, basins and channels | Explicit analysis action; pinned elevation/land-water inputs and algorithm parameters | Deterministic Calculation Run → Derived Artifact; optional Candidate only when author proposes formal semantic adoption | Depression treatment, coverage, uncertainty, algorithm version, input fingerprints, bounded invalidation and atomic artifact publication | Preview Only* |
| `climate.analyze` | Generate seasonal climate, biome/habitat suitability | Explicit analysis action; terrain/elevation, latitude/planet assumptions, moisture/ocean context, season model and algorithm version | Deterministic Calculation Run → Derived Artifact; suitability is not actual species presence or population | Causal input provenance, four normalized seasonal phases, hemisphere phase, coverage, uncertainty and deterministic fingerprints | Preview Only* |
| `terrain.feature_candidate` | Suggest terrain feature/landform candidates | Select region or request AI assistance; source field/feature fingerprints | Candidate intent only; no automatic formal Object creation | AI may explain and stage; numeric/physical results come from deterministic services; human review according to risk | Unverified |

Notes:
- Semantic Object creation and source Field Dataset publication are separate formal write paths.
- Derived drainage/climate outputs are not Core Assets or formal Objects by default.
- Candidate detection cannot silently promote every detected ridge, watershed, biome or climate pixel into a semantic Object.
- Field edits use ME-05 package/tiling/publication contracts; calculations use ME-02 reproducibility and bounded invalidation contracts.

### 5.2 Settlements & Urban Geography (ME-03)

| Tool key | Creator intent / target | Gesture and query dependencies | Typed Intent and formal operation | Validation / freshness / authorization | Runtime state |
|---|---|---|---|---|---|
| `urban.settlement` | Create/place/edit Settlement anchor and optional extent | Point + optional area; terrain, water, climate and map scope | Settlement Object/Initial State/History geometry intent | Existing descriptor: anchor exactly one Point, extent optional Area; no runtime attribute mutation unless descriptor enables it | Unverified |
| `urban.political_region` | Create/edit PoliticalRegion territory | Area boundary; region class and spatial predicates | PoliticalRegion Object/Initial State/History geometry intent; explicit `spatial.contains` or `spatial.adjacent_to` only for enabled pairs | Territory is not government/control; containment/adjacency must follow exact endpoint/cardinality policy | Unverified |
| `urban.functional_area` | Author residential/commercial/industrial/agricultural use | Area; intended/actual/proposed use basis and overlap policy | Candidate/proposal only until FunctionalArea Object Type and typed operations are adopted | Must not misuse TerrainRegion; overlapping uses and temporal basis need declared semantics | Blocked |
| `urban.road_path` | Create/edit Road centerline | Path; Road descriptor and spatial reference | Road Object/geometry component intent | Road centerline 1..N; exact role/family/cardinality and asset publication; no inferred connectivity | Unverified |
| `urban.road_junction` | Connect two Roads at an explicit junction | Endpoint selection + junction point; stable endpoint identity and topology evidence | Typed RoadEndpoint relationship intent | **Blocked:** ME-03-H P0; current `network.connects_to` is Road↔Road Object Pair only and cannot identify stable endpoints or multiple junctions for the same pair. Do not apply formal connectivity | Blocked |
| `urban.placement_validation` | Evaluate settlement suitability and explain constraints | Select proposed location/extent; ME-02 source and derived evidence | Deterministic validation query; optional Candidate context, no automatic formal write | Rule catalog/algorithms/fingerprints/coverage/uncertainty must be implemented; source Field Revision changes invalidate all dependent Map Branches/Times | Preview Only* |

Notes:
- Existing registry types are Settlement, Road and PoliticalRegion; PointOfInterest and TerrainRegion may be read context only within their bounded semantics.
- `network.connects_to` does not mean access, route permission, direction or Road-to-Settlement access.
- Settlement hierarchy and Road-to-Settlement access are unsupported until separately adopted.
- FunctionalArea is a proposal, not an enabled type.

### 5.3 Buildings & Interior Spaces (ME-04)

| Tool key | Creator intent / target | Gesture and query dependencies | Typed Intent and formal operation | Validation / freshness / authorization | Runtime state |
|---|---|---|---|---|---|
| `building.create_edit` | Create/edit Building and footprint | Area or import; Map CRS plus Building-local frame | Building Object + definition/Initial State/History + geometry Asset intent | P2-09-37 proposes adoption but Building descriptor and operations are not confirmed as executable; identity and geometry roles must be adopted | Blocked |
| `building.levels` | Add/reorder Level and set vertical frame/elevation | Inspector action; Building ID, level IDs, BLVF and optional Map vertical reference | Building typed aggregate service; not independent top-level Object | Requires adopted Level payload schema, stable Building-scoped IDs, vertical-reference validation and history policy | Blocked |
| `building.spaces` | Create/edit Room, Corridor, Lobby or Void | Area/path within selected Level plan; Building-local coordinates | Building Space aggregate service; typed Space/Level Slice intent | Requires adopted child identity, plan ownership, boundary component and validation services; no generic Object/relationship CRUD | Blocked |
| `building.boundaries` | Draw/refine shared walls/partitions/boundaries | Path/area in Building-local plan | Building plan geometry service + immutable Asset publication | Shared boundary identity and Space Slice references must reconcile atomically; geometry contact is not traversability | Blocked |
| `building.openings` | Add Door, Window, Archway, Passage and Entrance | Select wall/boundary + place opening | Typed access-record service; Opening separate from Access Connection | Requires stable owner-scoped IDs, boundary attachment and typed operation; Door state is separate from identity | Blocked |
| `building.access_connections` | Connect Space to Space or exterior endpoint | Explicit endpoint selection | Typed Access Connection intent | Requires adopted endpoint types, direction, cardinality and external endpoint model; never reuse Road `network.connects_to` | Blocked |
| `building.vertical_connector` | Create stair/elevator/ladder/ramp route across Levels | Select Level endpoints + path | Typed Vertical Connector service | Requires BLVF, Level endpoint resolution, vertical extent and reachability validation | Blocked |
| `cave.system` | Create/edit natural Cave System | Survey path/area; Cave Survey Frame and vertical reference | Cave System Object + Cave-owned typed child aggregate | P2-09-37 is an adoption proposal, not an executable descriptor/handler; survey geometry and child services remain gated | Blocked |
| `underground.facility` | Author constructed underground floors/spaces | Reuse Building/Level/Space model | Same Building aggregate operations, with underground placement context | Must not create a second interior model; requires Building adoption and vertical datum rules | Blocked |
| `subsurface.network` | Connect independent cave/facility systems | Explicit network endpoints | Deferred typed Network Object/relationship family | Cross-aggregate endpoint semantics and concrete use case deferred | Unsupported |

Notes:
- Level and Space are Building-owned typed children, not independent Map Object Types under the current ME-04 design.
- Building-local 2D plan geometry is distinct from Map coordinates. Full authoritative 3D/BIM solids are outside V1.
- Entrance/Opening/Access Connection/Vertical Connector are distinct records and intents.
- No interior formal write is enabled by the existence of generic geometry commands.

## 6. Operation and write-path mapping

| Intent family | Formal authority path | Core Domain operation family | Critical gate |
|---|---|---|---|
| Project-shared semantic definition edit | Project-level Shared Object definition service | Typed definition service, not History Command | Expected definition revision/fingerprint; retroactive current-definition semantics |
| Map-local semantic definition edit | Owner-Map definition service | Typed definition service | Map scope and expected definition revision |
| Time-zero Object placement/initial geometry | Map Initial State service | Baseline typed operation | Expected baseline revision; identity reservation/binding; descriptor validation |
| Post-zero Object creation/presence | Selected Map/Branch History | `core.object.create` with already Bound identity | Post-zero only; identity lifecycle and deterministic replay |
| Post-zero geometry component edit | Selected Map/Branch History | `core.geometry.adjust_component`, `refine_component`, `add_component`, `remove_component` | Exact registered descriptor; role/cardinality; immutable Asset; source fingerprint; no split/merge |
| Relationship establishment/termination | Initial State or selected History, per type temporal class | `core.relationship.establish` / `core.relationship.end` | Exact endpoint pair, relationship ID, cardinality, direction, temporal and endpoint activity policy |
| Source physical field update | Map Source Field Baseline | Field Dataset/Revision publication service | Map-wide retroactive scope; expected current Revision CAS; Asset package integrity |
| Deterministic analysis | Calculation subsystem / Map Engine | Run → Derived Artifact | Pinned inputs, deterministic algorithm/version, coverage, uncertainty, cancellation and atomic publication |
| Presentation and selection | Editor/Map Engine | View/selection/query operations | No formal domain write |

The four formal write paths are non-interchangeable: Project Shared Definition, Map Initial State, post-zero Map/Branch History, and Map Source Field Baseline. A tool declaration must select exactly one path for each persisted effect; composite workflows may contain multiple explicit stages.

## 7. Capability and adoption gates

| Gate | Required work | Blocks |
|---|---|---|
| G-01 Runtime evidence baseline | For each claimed enabled tool, link descriptor registration, typed handler, persistence/replay/publication implementation and automated tests | All `Enabled` claims |
| G-02 Geometry operation adoption | Adopt P2-09-15/19 descriptor and payload semantics; implement four bounded component operations and Asset publication/reconciliation | Formal geometry edit |
| G-03 Field package runtime | Implement Field Descriptor Registry, package parser/tiling, validation, CAS publication and provider tests | Elevation/land-water formal edits |
| G-04 Specialist semantic descriptors | Adopt/implement River/Lake/Settlement/Road/PoliticalRegion descriptors, relationships and typed operations in runtime | Semantic authoring and relationship edits |
| G-05 Road endpoint identity | Add stable RoadEndpoint identity and relationship uniqueness/endpoint semantics; resolve ME-03-H P0 | Formal Road junction |
| G-06 FunctionalArea adoption | Decide bounded Object Type, temporal/overlap semantics and typed operations | Formal land-use authoring |
| G-07 Building/Cave adoption | Reconcile P2-09-37 with ME-04-H H-01…H-17; adopt descriptors, child aggregate payloads, services, persistence and tests | All Building/Cave formal authoring |
| G-08 Spatial query implementation | Implement CRS-aware predicates, measurement, hit-test/snap freshness and typed result outcomes | Spatial preview/validation tools |
| G-09 Deterministic analysis | Implement ME-02 calculation contracts and ME-03 placement validation rule catalog with fingerprints/coverage/uncertainty | Hydrology/climate/placement analysis |
| G-10 Candidate authorization | Implement exact proposal version/hash, context binding, risk-based review and idempotent reconciliation | AI proposal application |
| G-11 Platform proof | Verify input/query/render interaction on Windows and macOS with supported Avalonia/Map Engine stack | Release readiness |

## 8. Explicit non-goals and safety boundaries

- No arbitrary plug-in, user script, macro language or runtime-loaded tool definitions in V1.
- No universal GIS/CAD/BIM ontology or universal topology graph.
- No formal relationship inferred from visual contact, snapping or geometry predicates.
- No 3D solid/BIM authoring or physically exact simulation in this tool inventory.
- No River branching/delta, unsupported hydrology endpoints or unbounded network semantics.
- No direct LLM/database access, generic CRUD or arbitrary JSON patch.
- No claim that Figma controls, document examples or recommended catalogs constitute executable runtime capabilities.
- No cross-Map mutation from a Map-local History Command or Map Source Field Revision.

## 9. Acceptance criteria for ME-06-A

ME-06-A is complete as an inventory when:
1. Every ME-02/03/04 creator workflow is mapped to a stable built-in tool key and Typed Intent family.
2. Every tool declares target kinds, gesture/action, read dependencies, validation, formal write path (or read-only status), risk/authorization and cancellation/recovery semantics.
3. Formal operations are reconciled with the exact Core Domain catalog and specialist endpoint constraints.
4. Known blockers (Road endpoint identity; Building/Cave adoption; FunctionalArea; Field package runtime; calculation/query implementation) are explicit.
5. Runtime state is not overstated: `Enabled` requires implementation and test evidence.
6. The shared catalog remains finite and does not create a second editor runtime.

**Disposition:** ME-06-A is complete as a design inventory working baseline. It does not mean tool implementations are complete. Next: ME-06-B — Input Routing & Gesture Lifecycle, using this catalog as its stable functional inventory and explicitly separating tool arming, pointer capture, transient draft, query response, intent construction and apply/reconciliation.

## 10. Source baselines reviewed

- ME-06-00 Shared Interaction & Tool Contracts V0.1.
- ME-01-05/06/07 Object/geometry model, interaction framework and cross-architecture review.
- ME-02-A Domain Inventory; ME-02-G UX Scenarios; ME-02-H Cross-architecture Review.
- ME-03-A Domain Inventory; ME-03-G UX Scenarios; ME-03-H Cross-architecture Review.
- ME-04-A Domain Inventory; ME-04-G UX Scenarios; ME-04-H Cross-architecture Review.
- ME-05-02 Shared Geometry Model & Component Operations; ME-05-03 Topology & Spatial Predicate Contract; ME-05-04 Coordinate Reference/Precision/Measurement; ME-05-05 Asset Package/Publication; ME-05-06 Cross-architecture Review.
- Core Domain P2-09-15 Command Descriptor Catalog V0.5; P2-09-20 Relationship Descriptor/Temporal Contract V0.2; P2-09-26 Object Type Registry V0.2; P2-09-27 Relationship Registry V0.2; P2-09-37 Building/Cave Adoption Contract V0.2.

## 11. Change control

This inventory is subordinate to accepted ADRs and adopted Core Domain contracts. If the runtime registry, specialist descriptor adoption or implementation evidence changes, update the affected row with exact source/version and test evidence. Do not silently change a `Blocked` or `Unverified` row based on UI appearance alone.
