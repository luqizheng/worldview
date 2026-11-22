# Map Editor Work Plan and Document Status

- Version: 0.9
- Date: 2026-11-21
- Status: Active design work plan
- Supersedes: ME-01-work-plan-and-document-status-v0.8.md (retained for history)

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
| ME-02-07 | [Calculation Contracts](world-view-me-02-calculation-contracts-v0.2.md) | 0.2 | Calculation Architecture Working Baseline | Reproducible inputs, determinism, tile/halo/resolution, bounded invalidation, Run/Artifact lifecycle, atomic publication, cancellation/recovery and diagnostics |
| ME-02-08 | [UX Scenarios & Acceptance](world-view-me-02-ux-scenarios-and-acceptance-v0.2.md) | 0.2 | Natural Geography UX Working Baseline | Story-led initialization, direct authoring, terrain/hydrology/climate flows, local refinement, recovery, Candidate review, Inspector, diagnostics, accessibility and end-to-end acceptance |
| ME-02-09 | [Cross-architecture Review](world-view-me-02-cross-architecture-review-v0.1.md) | 0.1 | Cross-architecture Review Working Baseline | Reconciliation with Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; explicit implementation gates and package disposition |

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
| ME-02-H | **Complete** — Cross-architecture review | Reconciled Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; source Field Dataset authority remains an explicit ME-05/Core Domain implementation gate — [ME-02-09](world-view-me-02-cross-architecture-review-v0.1.md) |

ME-02-01 V0.6 is the reconciled product/domain working baseline. ME-02-A through ME-02-H are complete as specialist design working baselines. The source Field Dataset authority/persistence contract remains a P0 implementation gate for ME-05 and Core Domain; renderer, algorithm, descriptor and platform validation gates remain with their owners. Exact algorithms, physical field persistence, numeric parameters and technology choices remain open until their respective gates are completed.

## Subsequent specialist workstreams

| Workstream | Planned responsibility | Entry condition |
|---|---|---|
| ME-03 Settlements & Urban Geography | Settlements, political/administrative regions, land use, roads and transport networks | ME-01 shared baseline; ME-02 natural geography constraints |
| ME-04 Buildings & Interior Spaces | Building/site association, floors, rooms, entrances, vertical and underground spaces | Shared Object/geometry and local coordinate contracts |
| ME-05 Shared Geometry & Spatial Operations | Component identity, geometry operations, topology, physical field/Asset persistence and spatial service contracts | Findings from ME-01/ME-02/ME-03/ME-04 |
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
