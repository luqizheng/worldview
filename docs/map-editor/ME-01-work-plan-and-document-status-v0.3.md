# Map Editor Work Plan and Document Status

- Version: 0.3
- Date: 2026-11-21
- Status: Active design work plan
- Supersedes: ME-01-work-plan-and-document-status-v0.2.md (retained for history)

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
| ME-02-01 | [Terrain & Natural Geography Editor](world-view-me-02-terrain-and-natural-geography-editor-v0.1.md) | 0.1 | Natural Geography Editor Working Baseline | Product workflows, semantic Objects vs physical fields vs derived layers, terrain/hydrology/climate/ecology scope, scientific rules, calculation boundaries and V1 capability limits |

### ME-02 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-02-A | Domain inventory against Core Domain catalogs | Natural feature → Object / field / derived layer / Knowledge mapping; descriptor gaps |
| ME-02-B | Land/water and elevation authoring | World frame, map-edge semantics, field/Asset contract, editing scenarios |
| ME-02-C | Terrain feature authoring | Landmass, Mountain Range/Peak, Terrain Region tool and validation contract |
| ME-02-D | Hydrology | Drainage analysis, depression policy, river candidate/network validation and flow relationship contract |
| ME-02-E | Climate and ecology | Causal input/output model, seasonal summaries, classification and suitability overlays |
| ME-02-F | Calculation contracts | Determinism, provenance, freshness, dependency invalidation and validation severity |
| ME-02-G | UX scenarios and acceptance | End-to-end creator workflows, Inspector, diagnostics, Candidate review and accessibility |
| ME-02-H | Cross-architecture review | Reconciliation with Core Domain, Map Engine, Map Generation and ME-01 |

ME-02-01 establishes the product/domain working baseline. Detailed algorithms, physical field persistence, numeric parameters and technology choices remain open until their respective gates are completed.

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
