# Map Editor Work Plan and Document Status

- Version: 2.1
- Date: 2026-11-21
- Status: Active design work plan
- Supersedes: ME-01-work-plan-and-document-status-v2.0.md (retained for history)

This document tracks the Map Editor product/architecture and specialist editor design workstreams. Design completion does not imply implementation completion or frozen APIs/schemas. The GitHub repository `luqizheng/worldview` is the authoritative project documentation location.

## ME-01 — Shared Editor Product and Architecture

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-01-01 | [Editor Overall Architecture](world-view-me-01-01-editor-overall-architecture-v0.1.md) | 0.1 | Architecture Working Baseline | Unified editor, modes, shared spatial model and subsystem boundaries |
| ME-01-02 | [World Knowledge and Story-Spatial Bridge](world-view-me-01-02-world-knowledge-and-story-spatial-bridge-v0.1.md) | 0.1 | Design Working Baseline | Story facts, creator intent, inference, provenance and spatial intent |
| ME-01-03 | [Guided Initialization](world-view-me-01-03-guided-initialization-v0.1.md) | 0.1 | Design Working Baseline | Adaptive conversation, staged generation, review, application and incremental refinement |
| ME-01-04 | [Multi-scale Spatial Model & Global Consistency](world-view-me-01-04-multiscale-spatial-consistency-v0.1.md) | 0.1 | Architecture Working Baseline | Spatial scopes, resolution/LOD, cross-scale relations, constraints and bounded recalculation |
| ME-01-05 | [Map Object, Geometry & Representation Model](world-view-me-01-05-map-object-geometry-representation-model-v0.1.md) | 0.1 | Architecture Working Baseline | Semantic identity, ObjectState, geometry association/components, relationships, presentation, layers and derived render data |
| ME-01-06 | [Editor Interaction Framework](world-view-me-01-06-editor-interaction-framework-v0.1.md) | 0.1 | Editor Interaction Working Baseline | Canvas navigation/scope, shared selection/tools, transient edit sessions, Candidate review, diagnostics and history interaction |
| ME-01-07 | [Cross-architecture Consistency Review](world-view-me-01-07-cross-architecture-consistency-review-v0.1.md) | 0.1 | Cross-architecture Review Working Baseline | Alignment with Core Domain, AI Assistant, Map Generation, Map Engine and persistence; findings F-01 to F-11 |

ME-01 is complete as a shared product/architecture working baseline. The findings and follow-up gates in ME-01-07 remain constraints for specialist design and implementation.

## ME-02 — Terrain & Natural Geography Editor

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-02-01 | [Terrain & Natural Geography Editor](world-view-me-02-terrain-and-natural-geography-editor-v0.6.md) | 0.6 | Natural Geography Editor Working Baseline | Product workflows, semantic Objects vs physical fields vs derived layers, terrain/hydrology/climate/ecology scope, scientific rules, calculation boundaries and V1 capability limits |
| ME-02-02 | [Domain Inventory & Core Domain Mapping](world-view-me-02-domain-inventory-and-core-domain-mapping-v0.1.md) | 0.1 | Domain Inventory Working Baseline | Maps natural-geography concepts to formal Objects, physical fields, derived analysis, World Knowledge and presentation; records V1 descriptor and persistence gaps |
| ME-02-03 | [Land/Water & Elevation Authoring](world-view-me-02-land-water-and-elevation-authoring-v0.1.md) | 0.1 | Specialist Authoring Workflow Working Baseline | World frame/map-edge semantics, land/water representation, elevation field authoring, preview, validation and persistence gates |
| ME-02-04 | [Terrain Feature Authoring](world-view-me-02-terrain-feature-authoring-v0.1.md) | 0.1 | Specialist Feature Workflow Working Baseline | Landmass, Mountain Range/Peak and Terrain Region authoring, feature detection candidates, semantic relations and validation |
| ME-02-05 | [Hydrology & Drainage Authoring](world-view-me-02-hydrology-and-drainage-authoring-v0.1.md) | 0.1 | Hydrology Specialist Workflow Working Baseline | Terrain-first drainage, depression policy, derived basins/channels, River/Lake authoring, formal flow network semantics and validation |
| ME-02-06 | [Climate & Ecology Authoring](world-view-me-02-climate-and-ecology-authoring-v0.1.md) | 0.1 | Climate & Ecology Specialist Workflow Working Baseline | Planet/season assumptions, circulation, ocean/moisture influence, temperature/precipitation, climate classification, biome/habitat suitability and causal diagnostics |
| ME-02-07 | [Calculation Contracts](world-view-me-02-calculation-contracts-v0.3.md) | 0.3 | Calculation Architecture Working Baseline | Reproducible inputs, determinism, tile/halo/resolution, bounded invalidation, Run/Artifact lifecycle, atomic publication, cancellation/recovery and diagnostics |
| ME-02-08 | [UX Scenarios & Acceptance](world-view-me-02-ux-scenarios-and-acceptance-v0.2.md) | 0.2 | Natural Geography UX Working Baseline | Story-led initialization, direct authoring, terrain/hydrology/climate flows, local refinement, recovery, Candidate review, Inspector, diagnostics, accessibility and end-to-end acceptance |
| ME-02-09 | [Cross-architecture Review](world-view-me-02-cross-architecture-review-v0.2.md) | 0.1 | Cross-architecture Review Working Baseline | Reconciliation with Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; explicit implementation gates and package disposition |

### ME-02 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-02-A | **Complete** — Domain inventory against Core Domain catalogs | Natural feature → Object / field / derived layer / Knowledge mapping; descriptor gaps — [ME-02-02](world-view-me-02-domain-inventory-and-core-domain-mapping-v0.1.md) |
| ME-02-B | **Complete** — Land/water and elevation authoring | World frame, map-edge semantics, field/Asset logical contract, editing scenarios — [ME-02-03](world-view-me-02-land-water-and-elevation-authoring-v0.1.md) |
| ME-02-C | **Complete** — Terrain feature authoring | Landmass, Mountain Range/Peak, Terrain Region tool and validation contract — [ME-02-04](world-view-me-02-terrain-feature-authoring-v0.1.md) |
| ME-02-D | **Complete** — Hydrology & drainage authoring | Terrain-first drainage, depression treatment, flow routing, derived basin/channel outputs, formal River network limits — [ME-02-05](world-view-me-02-hydrology-and-drainage-authoring-v0.1.md) |
| ME-02-E | **Complete** — Climate & ecology authoring | Seasonal causal climate model, circulation/moisture, temperature/precipitation, climate classification, biome/habitat suitability — [ME-02-06](world-view-me-02-climate-and-ecology-authoring-v0.1.md) |
| ME-02-F | **Complete** — Calculation contracts | Reproducible input snapshots, determinism profiles, tile/halo/resolution semantics, bounded invalidation, separate Run/Artifact lifecycle, atomic publication, cancellation/recovery and validation — [ME-02-07](world-view-me-02-calculation-contracts-v0.2.md) |
| ME-02-G | **Complete** — UX scenarios and acceptance | End-to-end creator workflows, Inspector, diagnostics, Candidate review, accessibility and Figma handoff — [ME-02-08](world-view-me-02-ux-scenarios-and-acceptance-v0.2.md) |
| ME-02-H | **Complete** — Cross-architecture review | Reconciled Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; Field Dataset authority decision adopted in ME-05-01 V0.3 — [ME-02-09](world-view-me-02-cross-architecture-review-v0.2.md) |

ME-02-01 V0.6 is the reconciled product/domain working baseline. ME-02-A through ME-02-H are complete as specialist design working baselines. The source Field Dataset authority/product-semantic gate H-G01 is closed by ME-05-01 V0.3. Physical schema and provider-proof designs are documented in P2-09-11 V0.11 and P2-09-12 V0.6; the recommended Field Descriptor Registry V0.1 and 32 validation vectors are also documented. Immutable payload package/tiling, development implementation and Windows/macOS execution evidence remain open. Exact algorithms, physical field persistence implementation, numeric parameters and technology choices remain open until their respective gates are completed.

## ME-03 — Settlements & Urban Geography Editor (design workstream active)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-03-00 | [Settlements & Urban Geography Editor — Scope and Work Plan](world-view-me-03-settlements-and-urban-geography-editor-v0.2.md) | 0.2 | Specialist Workstream Scope / Design Plan | Inherited architecture constraints and exact descriptor-registry endpoint constraints |
| ME-03-A | [Domain Inventory & Descriptor Reconciliation](world-view-me-03-a-domain-inventory-and-descriptor-reconciliation-v0.1.md) | 0.1 | Domain Inventory Working Baseline | Exact Settlement/Road/PoliticalRegion descriptors, relationship endpoint pairs, urban concept disposition and cross-domain gaps |
| ME-03-B | [Settlement Model & Authoring Workflows](world-view-me-03-b-settlement-model-and-authoring-workflows-v0.1.md) | 0.1 | Specialist Domain Working Baseline | Settlement identity, anchor/extent, classification, multiscale representation, authoring lifecycle, temporal paths and acceptance scenarios |
| ME-03-C | [Political & Administrative Geography](world-view-me-03-c-political-and-administrative-geography-v0.1.md) | 0.1 | Specialist Domain Working Baseline | PoliticalRegion territory semantics, boundaries, adjacency, settlement containment, administrative hierarchy limits and temporal border changes |
| ME-03-D | [Land Use & Functional Areas](world-view-me-03-d-land-use-and-functional-areas-v0.1.md) | 0.1 | Specialist Domain Working Baseline + Core Domain Gap Proposal | Human-use classification, actual/designated/proposed basis, overlap, terrain separation, temporal semantics and proposed functional_area type |
| ME-03-E | [Road & Path Network Authoring](world-view-me-03-e-road-and-path-network-authoring-v0.1.md) | 0.1 | Specialist Domain Working Baseline + Core Domain Gap Assessment | Road segment granularity, explicit junction connectivity, grade-separated crossings, settlement access limits, network validation and identity-lineage gates |

ME-03 is opened as a specialist design workstream. ME-03-A through ME-03-E are complete as specialist working baselines; ME-03-F through ME-03-H remain planned. The design must consume the existing `settlement`, `road` and `political_region` Object Type seeds and the bounded relationship catalog; it must not silently introduce new Object Types, Relationship Types, executable descriptors or schema changes. Domain inventory and descriptor reconciliation are the first work item.

### ME-03 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-03-A | Domain inventory and descriptor reconciliation | Existing Object/Relationship/World Knowledge/derived/presentation mapping and exact descriptor gaps |
| ME-03-B | Settlement model and authoring workflows | Identity, classification, anchor/extent, hierarchy and multi-scale behavior |
| ME-03-C | Political and administrative geography | Territory/actor distinction, subdivision and temporal control prerequisites |
| ME-03-D | Land use and functional areas | Human-use semantics distinct from terrain/land cover and presentation |
| ME-03-E | Road and path network authoring | Road semantics, junction/crossing/connectivity and explicit network relationships |
| ME-03-F | Settlement placement and urban spatial validation | Natural-geography constraints, access, uncertainty and diagnostics |
| ME-03-G | UX scenarios and acceptance | Creator journeys, Canvas/Inspector, Candidate review and Figma handoff |
| ME-03-H | Cross-architecture integration review | Core Domain / ME-01 / ME-02 / ME-05 / Map Engine / AI reconciliation and implementation gates |

## ME-03 — Settlements & Urban Geography Editor (design workstream opened)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-03-00 | [Settlements & Urban Geography Editor — Scope and Work Plan](world-view-me-03-settlements-and-urban-geography-editor-v0.1.md) | 0.1 | Specialist Workstream Scope / Design Plan | Inherited architecture constraints, existing Core Domain vocabulary, settlement/political/land-use/road boundaries, work packages and open gates |

ME-03 is opened as a specialist design workstream. ME-03-A through ME-03-H are planned and not yet complete. The initial design must consume the existing `settlement`, `road` and `political_region` Object Type seeds and the bounded relationship catalog; it must not silently introduce new Object Types, Relationship Types, executable descriptors or schema changes. Domain inventory and descriptor reconciliation are the first work item.

### ME-03 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-03-A | Domain inventory and descriptor reconciliation | Existing Object/Relationship/World Knowledge/derived/presentation mapping and exact descriptor gaps |
| ME-03-B | Settlement model and authoring workflows | Identity, classification, anchor/extent, hierarchy and multi-scale behavior |
| ME-03-C | Political and administrative geography | Territory/actor distinction, subdivision and temporal control prerequisites |
| ME-03-D | Land use and functional areas | Human-use semantics distinct from terrain/land cover and presentation |
| ME-03-E | Road and path network authoring | Road semantics, junction/crossing/connectivity and explicit network relationships |
| ME-03-F | Settlement placement and urban spatial validation | Natural-geography constraints, access, uncertainty and diagnostics |
| ME-03-G | UX scenarios and acceptance | Creator journeys, Canvas/Inspector, Candidate review and Figma handoff |
| ME-03-H | Cross-architecture integration review | Core Domain / ME-01 / ME-02 / ME-05 / Map Engine / AI reconciliation and implementation gates |

## ME-05 — Shared Geometry & Spatial Operations (design package complete; implementation gates open)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-05-01 | [Field Dataset Authority & Persistence Contract](world-view-me-05-01-field-dataset-authority-and-persistence-contract-v0.3.md) | 0.3 | **Adopted Domain Contract — product semantics confirmed; not frozen ADR or executable schema** | Map-owned source Field Dataset identity, immutable revisions, typed Core Asset ownership, Map-wide retroactive Branch/History semantics and calculation/checkpoint integration |
| ME-05-02 | [Shared Geometry Model & Component Operations](world-view-me-05-02-shared-geometry-model-and-component-operations-v0.1.md) | 0.1 | Recommended V1 shared geometry contract | Component identity/lifecycle, typed geometry operations, three formal write contexts, immutable Asset ownership and interaction boundary |
| ME-05-03 | [Topology & Spatial Predicate Contract](world-view-me-05-03-topology-and-spatial-predicate-contract-v0.1.md) | 0.1 | Recommended V1 spatial-query contract | Predicate vocabulary, boundary semantics, geometric adjacency candidates, topology-vs-relationship separation, numerical certainty and query freshness |
| ME-05-04 | [Coordinate Reference, Precision & Measurement Contract](world-view-me-05-04-coordinate-reference-precision-and-measurement-contract-v0.1.md) | 0.1 | Recommended V1 coordinate/measurement contract | Spatial-reference identity, typed coordinate pipeline, transforms, precision profiles, curve preservation, measurement semantics and accuracy/error declarations |
| ME-05-05 | [Geometry & Field Asset Package and Publication Contract](world-view-me-05-05-geometry-and-field-asset-package-publication-contract-v0.1.md) | 0.1 | Recommended V1 Asset lifecycle integration contract | Vector/field payload separation, immutable Asset ownership, publication, CAS, retention, orphan recovery and provider validation gates |
| ME-05-06 | [Cross-architecture Integration Review & V1 Gates](world-view-me-05-06-cross-architecture-integration-review-v0.1.md) | 0.1 | Cross-architecture Review Working Baseline | Reconciles Core Domain, Map Engine, ME-01/02 and future ME-03/04 consumers; records findings, ownership boundaries and implementation acceptance gates |

### ME-05 current closure and remaining gates

- **ME-05-01 product/domain gate closed:** H-G01 adopted; Map Source Field Baseline is Map-owned and Field Revision updates apply retroactively to every Branch and Historical Time of that Map.
- **Design contracts drafted:** ME-05-02 geometry identity/operation boundary; ME-05-03 spatial predicate and topology boundary; ME-05-04 coordinate reference, precision and measurement semantics; ME-05-05 immutable geometry/field Asset package and publication lifecycle; ME-05-06 cross-architecture reconciliation and implementation gates.
- **Existing Core Domain alignment:** P2-09-19 V0.2 stores component identity in typed owner payloads (no V1 component table); P2-09-15 V0.5 includes adjust/refine/add/remove and defers split/merge; P2-09-20 V0.2 keeps formal relationships typed and distinct from geometric coincidence.
- **Open:** exact immutable field/geometry payload package and tiling; finalize payload package/tiling and selected technical implementations; implementation and Windows/macOS numerical/provider execution evidence; specialist topology validation and selected-library measurement/transform proof. ME-05-02/03 are recommended working contracts, not frozen ADRs, executable descriptors or implementation authorization.

### ME-05 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-05-A | **Complete as working contracts** — Field Dataset authority and persistence semantics | ME-05-01 V0.3 |
| ME-05-B | **Complete as working contract** — Shared Geometry Model and Component Operations | ME-05-02 V0.1 |
| ME-05-C | **Complete as working contract** — Topology and Spatial Predicates | ME-05-03 V0.1 |
| ME-05-D | **Complete as working contract** — Coordinate Reference, Precision & Measurement | ME-05-04 V0.1 |
| ME-05-E | **Complete as working contract** — Geometry/Field Asset Package & Publication Integration | ME-05-05 V0.1 |
| ME-05-F | **Complete as working review** — Map Engine / Core Domain Integration & Cross-architecture Verification | ME-05-06 V0.1 | Typed API boundaries, specialist constraints, acceptance matrix and implementation gates |

## Subsequent specialist workstreams

| Workstream | Planned responsibility | Entry condition |
|---|---|---|
| ME-03 Settlements & Urban Geography | Settlements, political/administrative regions, land use, roads and transport networks | ME-01 shared baseline; ME-02 natural geography constraints |
| ME-04 Buildings & Interior Spaces | Building/site association, floors, rooms, entrances, vertical and underground spaces | Shared Object/geometry and local coordinate contracts |
| ME-05 Shared Geometry & Spatial Operations | Component identity, geometry operations, topology, physical field/Asset persistence and spatial service contracts | ME-05-01 Field Dataset authority contract; findings from ME-01/ME-02/ME-03/ME-04 |
| ME-06 Shared Interaction / Tool Contracts | Concrete tool-to-operation mapping, gesture/query freshness and Candidate review contracts | Specialist editor workflows |
| ME-07 Rendering / Technical Architecture | Renderer/geometry/raster library evaluation, Avalonia integration, performance and Windows/macOS validation | Renderer-neutral contracts and representative workloads |

The ordering may be refined where dependencies require it. Do not reopen settled Core Domain authority, stable Object identity, Branch/history semantics, local-first product shape or the shared spatial model without an explicit architecture finding.

## Status and authority rules

- Accepted/Frozen ADRs define binding decisions within their stated scope.
- Working Baseline, Proposal, Discussion, Review, Work Plan and Test Specification documents are not automatically frozen.
- A recommended catalog is not an executable descriptor registry until adopted by its owner.
- Physical schemas are not executable DDL until provider/runtime validation is complete.
- Renderer and geometry libraries are not frozen until technical spikes and platform validation are recorded.
- A newer document supersedes an older version only where it explicitly states the scope of supersession.
- Before every GitHub update, fetch the latest file and SHA; preserve concurrent edits; re-fetch and verify after commit.

## Documentation authority

The GitHub repository `luqizheng/worldview` is the authoritative, version-controlled location for World View project documents. Local files may be temporary working copies only. Formal completion requires the artifact and its status to be committed and verified in GitHub.
