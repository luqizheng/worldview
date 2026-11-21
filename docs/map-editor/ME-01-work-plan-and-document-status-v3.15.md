# Map Editor Work Plan and Document Status

- Version: 3.15
- Date: 2026-11-21
- Status: Active design work plan
- Supersedes: ME-01-work-plan-and-document-status-v3.14.md (retained for history)

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
| ME-02-07 | [Calculation Contracts](world-view-me-02-calculation-contracts-v0.4.md) | 0.5 | Calculation Architecture Working Baseline | Reproducible inputs, determinism, tile/halo/resolution, bounded invalidation, Run/Artifact lifecycle, atomic publication, cancellation/recovery and diagnostics |
| ME-02-08 | [UX Scenarios & Acceptance](world-view-me-02-ux-scenarios-and-acceptance-v0.2.md) | 0.3 | Natural Geography UX Working Baseline | Story-led initialization, direct authoring, terrain/hydrology/climate flows, local refinement, recovery, Candidate review, Inspector, diagnostics, accessibility and end-to-end acceptance |
| ME-02-09 | [Cross-architecture Review](world-view-me-02-cross-architecture-review-v0.2.md) | 0.2 | Cross-architecture Review Working Baseline | Reconciliation with Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; explicit implementation gates and package disposition |

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
| ME-02-H | **Complete** — Cross-architecture review | Reconciled Core Domain, Map Engine, Map Generation, ME-01 and specialist ME-02 contracts; Field Dataset authority decision adopted in ME-05-01 V0.5 — [ME-02-09](world-view-me-02-cross-architecture-review-v0.2.md) |

ME-02-01 V0.6 is the reconciled product/domain working baseline. ME-02-A through ME-02-H are complete as specialist design working baselines. The source Field Dataset authority/product-semantic gate H-G01 is closed by ME-05-01 V0.5. Physical schema and provider-proof designs are documented in P2-09-11 V0.11 and P2-09-12 V0.6; the recommended Field Descriptor Registry V0.1 and 32 validation vectors are also documented. Immutable payload package/tiling, development implementation and Windows/macOS execution evidence remain open. Exact algorithms, physical field persistence implementation, numeric parameters and technology choices remain open until their respective gates are completed.

## ME-03 — Settlements & Urban Geography Editor (specialist design working baselines complete; integration review next)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-03-00 | [Settlements & Urban Geography Editor](world-view-me-03-settlements-and-urban-geography-editor-v0.2.md) | 0.2 | Specialist Workstream Scope / Design Plan | Inherited architecture constraints and exact descriptor-registry endpoint constraints |
| ME-03-A | [Domain Inventory & Descriptor Reconciliation](world-view-me-03-a-domain-inventory-and-descriptor-reconciliation-v0.1.md) | 0.1 | Domain Inventory Working Baseline | Exact Settlement/Road/PoliticalRegion descriptors, relationship endpoint pairs, urban concept disposition and cross-domain gaps |
| ME-03-B | [Settlement Model & Authoring Workflows](world-view-me-03-b-settlement-model-and-authoring-workflows-v0.1.md) | 0.1 | Specialist Domain Working Baseline | Settlement identity, anchor/extent, classification, multiscale representation, authoring lifecycle and temporal paths |
| ME-03-C | [Political & Administrative Geography](world-view-me-03-c-political-and-administrative-geography-v0.1.md) | 0.1 | Specialist Domain Working Baseline | PoliticalRegion territory semantics, boundaries, adjacency, settlement containment, hierarchy limits and temporal border changes |
| ME-03-D | [Land Use & Functional Areas](world-view-me-03-d-land-use-and-functional-areas-v0.1.md) | 0.1 | Specialist Domain Working Baseline + Core Domain Gap Proposal | Human-use classification, actual/designated/proposed basis, overlap, terrain separation and proposed functional_area type |
| ME-03-E | [Road & Path Network Authoring](world-view-me-03-e-road-and-path-network-authoring-v0.1.md) | 0.1 | Specialist Domain Working Baseline + Core Domain Gap Assessment | Road segment granularity, explicit junction connectivity, grade-separated crossings, settlement access limits and identity-lineage gates |
| ME-03-F | [Settlement Placement & Urban Spatial Validation](world-view-me-03-f-settlement-placement-and-urban-spatial-validation-v0.1.md) | 0.1 | Specialist Spatial-Validation Working Baseline | Terrain/water/climate context, possible road access, hard/soft/derived constraints, explainable diagnostics, uncertainty and freshness |
| ME-03-G | [UX Scenarios, Acceptance & Figma Handoff](world-view-me-03-g-ux-scenarios-acceptance-and-figma-handoff-v0.1.md) | 0.1 | Urban Geography UX Working Baseline | Unified Canvas/Inspector journeys, Candidate review, diagnostics, multi-scale behavior, recovery, acceptance scenarios and Figma master prompt |
| ME-03-H | [Cross-architecture Integration Review](world-view-me-03-h-cross-architecture-integration-review-v0.1.md) | 0.1 | Cross-architecture Review Working Baseline | Reconciles Core Domain, AI/Map Generation, ME-01/02/05, Map Engine, formal write paths, Road endpoint blocker, freshness and implementation gates |

ME-03-A through ME-03-H are complete as specialist product/domain/UX and cross-architecture design working baselines. Formal Road connectivity remains blocked by the endpoint-identity gate in ME-03-H; other capabilities remain subject to their shared implementation gates. The workstream consumes existing `settlement`, `road` and `political_region` Object Types and bounded relationship descriptors; it does not silently introduce Object Types, Relationship Types, executable descriptors or schema changes. FunctionalArea remains proposal-only until separately adopted. Formal Road connectivity remains constrained by the endpoint-evidence gap.

### ME-03 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-03-A | **Complete** — Domain inventory and descriptor reconciliation | Existing Object/Relationship/World Knowledge/derived/presentation mapping and exact descriptor gaps |
| ME-03-B | **Complete** — Settlement model and authoring workflows | Identity, classification, anchor/extent, multi-scale behavior and temporal paths |
| ME-03-C | **Complete** — Political and administrative geography | Territory/actor distinction, boundary, adjacency/containment and hierarchy limits |
| ME-03-D | **Complete** — Land use and functional areas | Human-use semantics distinct from terrain/land cover and presentation; FunctionalArea proposal |
| ME-03-E | **Complete** — Road and path network authoring | Road semantics, junction/crossing/connectivity, access distinction and Core Domain gaps |
| ME-03-F | **Complete** — Settlement placement and urban spatial validation | Natural-geography constraints, possible access, uncertainty, diagnostics and freshness |
| ME-03-G | **Complete** — UX scenarios and acceptance | Creator journeys, Canvas/Inspector, Candidate review, recovery, acceptance and Figma handoff |
| ME-03-H | **Complete** — Cross-architecture integration review | Core Domain / AI / ME-01 / ME-02 / ME-05 / Map Engine reconciliation, findings and implementation gates |


## ME-04 — Buildings & Interior Spaces Editor (specialist design baseline complete through ME-04-H; Core Domain adoption and implementation open)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-04-00 | [Buildings & Interior Spaces Editor](world-view-me-04-buildings-and-interior-spaces-editor-v0.1.md) | 0.1 | Specialist Workstream Scope / Design Plan | Building/site scope, exterior/interior model, vertical/underground principles, work packages and inherited architecture constraints |
| ME-04-A | [Domain Inventory & Descriptor Reconciliation](world-view-me-04-a-domain-inventory-and-descriptor-reconciliation-v0.1.md) | 0.1 | Domain Inventory Working Baseline | Exact current Object/Relationship/Command capabilities, semantic concept disposition, vertical-coordinate gaps and unsupported operations |
| ME-04-B | [Building Model & Authoring Workflows](world-view-me-04-b-building-model-and-authoring-workflows-v0.1.md) | 0.1 | Building Domain / Authoring Working Baseline | Stable Building identity, ownership scope, footprint component, coordinated exterior/interior views, creation/edit/history workflows and Core Domain adoption gates |
| ME-04-C | [Floors, Levels & Vertical Organization](world-view-me-04-c-floors-levels-and-vertical-organization-v0.1.md) | 0.1 | Vertical Organization Working Baseline | Building-scoped Level identity, Building-local Vertical Frame, optional Map vertical-reference binding, terrain-elevation evidence and 2D/3D boundary |
| ME-04-D | [Rooms, Spaces & Interior Geometry](world-view-me-04-d-rooms-spaces-and-interior-geometry-v0.1.md) | 0.1 | Interior Space / Geometry Working Baseline | Building-owned Space identities, Level Slices, Building-local 2D plans, shared boundary components, adjacency versus traversability |
| ME-04-E | [Entrances, Openings & Vertical Circulation](world-view-me-04-e-entrances-openings-and-vertical-circulation-v0.1.md) | 0.1 | Access / Vertical Circulation Working Baseline | Stable Entrance/Opening/Access Connection/Vertical Connector concepts, boundary anchors, explicit endpoints, multi-storey routes, operational state and reachability limits |
| ME-04-F | [Underground Spaces, Caves & Connected Facilities](world-view-me-04-f-underground-spaces-caves-and-connected-facilities-v0.1.md) | 0.1 | Underground Domain / Cross-scope Connectivity Working Baseline | Natural Cave System identity, cave survey frame, Building reuse for underground rooms, independent subsurface networks and explicit connectivity/uncertainty |
| ME-04-G | [UX Scenarios, Acceptance & Figma Handoff](world-view-me-04-g-ux-scenarios-acceptance-and-figma-handoff-v0.1.md) | 0.1 | Buildings & Interior Spaces UX Working Baseline | Building/Level/Space/Cave creator journeys, shared Canvas/Inspector, Candidate review, diagnostics, recovery, accessibility, acceptance matrix and Figma master prompt |
| ME-04-H | [Cross-architecture Integration Review](world-view-me-04-h-cross-architecture-integration-review-v0.1.md) | 0.1 | Cross-architecture Review Working Baseline | Reconciles Building/Cave identities, typed child aggregates, geometry ownership, access endpoints, history/Asset authority, 2D boundary and Core Domain adoption/implementation gates |

ME-04-00 and ME-04-A establish the scope and current descriptor inventory. ME-04-B establishes the Building semantic-object and authoring working baseline; ME-04-C establishes Building-local vertical organization and the Level working model; ME-04-D establishes Building-owned Spaces and 2D interior plan geometry; ME-04-E establishes explicit entrances, openings, access connections and vertical circulation; ME-04-F establishes natural Cave System identity, cave survey semantics, reuse of Building/Level/Space for constructed underground rooms, and the bounded proposal for independent cross-Building subsurface networks; ME-04-G translates the complete specialist model into creator journeys, interaction semantics, acceptance criteria and Figma handoff. These are product/domain working baselines only: none authorizes Core Domain registry/schema/command changes, new Relationship endpoint pairs, APIs or full 3D geometry. The current V1 registries still lack formal Building/Floor/Room/Entrance/Cave types and interior access endpoint pairs. Road integration remains blocked by ME-03-H's stable Road endpoint identity gap. Formal historical interior/access operations remain unavailable until typed Commands are adopted. ME-04-H reconciles the complete specialist package and records H-01 through H-17 plus a phased Core Domain adoption path. ME-04 is complete as a specialist design working baseline; Core Domain adoption, implementation and platform verification remain separate work.

## Core Domain — Building & Cave Adoption (proposal stage)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| P2-09-37 | [Building & Cave Core Domain Adoption Contract](../architecture/core-domain/p2-09-37-building-and-cave-domain-adoption-contract-v0.5.md) | 0.5 | Core Domain Adoption Proposal | Two top-level Object Types, typed child aggregates, canonical geometry owner metadata and Building/Cave frame-context distinction |
| P2-09-38 | [Building Typed Aggregate Schema](../architecture/core-domain/p2-09-38-building-typed-aggregate-schema-v0.5.json) | 0.5 | Draft JSON Schema | Strict Building payload schema with canonical Geometry Component names and explicit typed references from boundaries, entrances, openings and Level Slices |
| P2-09-39 | [Cave System Typed Aggregate Schema](../architecture/core-domain/p2-09-39-cave-system-typed-aggregate-schema-v0.5.json) | 0.5 | Draft JSON Schema | Strict Cave payload schema with canonical Geometry Component names and explicit Survey Station/Entrance references |
| P2-09-40 | [Building/Cave Aggregate Validation Vectors](../architecture/core-domain/p2-09-40-building-cave-aggregate-validation-vectors-v0.5.json) | 0.5 | Design-only Validation Vectors | 51 schema, aggregate, identity, endpoint, Asset-link and CAS cases; not executed, with canonical geometry reference naming |
| P2-09-41 | [Building/Cave Object Type Descriptor Extension](../architecture/core-domain/p2-09-41-building-cave-object-type-descriptor-extension-v0.4.json) | 0.4 | Proposed Registry Extension | Building and Cave descriptors pin the latest strict aggregate schemas and canonical geometry metadata |
| P2-09-42 | [Building/Cave Descriptor Registry Integration Contract](../architecture/core-domain/p2-09-42-building-cave-descriptor-registry-integration-contract-v0.6.md) | 0.6 | Registry Integration Review | Final field-vocabulary reconciliation, geometry Asset envelope and conditional typed Asset-link gates |
| P2-09-43 | [Building & Cave Core Domain Developer Handoff](../architecture/core-domain/p2-09-43-building-cave-core-domain-developer-handoff-v0.4.md) | 0.4 | Developer Handoff / Implementation Plan | S0–S10 implementation sequence with normalized component references, canonical metadata and 51-vector test requirements |

P2-09-37 translates ME-04-H into a minimum Core Domain extension proposal. P2-09-38/39 V0.5 now use the canonical P2-09-19 Geometry Component owner fields and normalized references; P2-09-40 V0.5 contains 51 design-only vectors; P2-09-41/42 V0.4/V0.6 record descriptor and typed Asset-link integration; P2-09-43 V0.4 is the updated developer handoff. The canonical fields are geometry_component_id, geometry_role, spatial_reference_id, geometry_schema_version, asset_id/content_hash, provenance and validity_metadata; coordinate_context_id remains a separate Building/Cave frame reference. All vectors remain unexecuted. Next gate is production-validator execution, existing P2-09-28/Core Domain regression, actual optional typed-link inventory verification and Windows/macOS evidence before a registry V0.3 proposal. No DDL or registry activation is authorized by these drafts. The existing 20-table Core Domain baseline remains unchanged unless an evidenced integrity/query requirement proves a new table necessary.

## ME-05 — Shared Geometry & Spatial Operations (design package complete; implementation gates open)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-05-01 | [Field Dataset Authority & Persistence Contract](world-view-me-05-01-field-dataset-authority-and-persistence-contract-v0.5.md) | 0.5 | **Adopted Domain Contract — product semantics confirmed; not frozen ADR or executable schema** | Map-owned source Field Dataset identity, immutable revisions, typed Core Asset ownership, Map-wide retroactive Branch/History semantics and calculation/checkpoint integration |
| ME-05-02 | [Shared Geometry Model & Component Operations](world-view-me-05-02-shared-geometry-model-and-component-operations-v0.1.md) | 0.1 | Recommended V1 shared geometry contract | Component identity/lifecycle, typed geometry operations, three formal write contexts, immutable Asset ownership and interaction boundary |
| ME-05-03 | [Topology & Spatial Predicate Contract](world-view-me-05-03-topology-and-spatial-predicate-contract-v0.1.md) | 0.1 | Recommended V1 spatial-query contract | Predicate vocabulary, boundary semantics, geometric adjacency candidates, topology-vs-relationship separation, numerical certainty and query freshness |
| ME-05-04 | [Coordinate Reference, Precision & Measurement Contract](world-view-me-05-04-coordinate-reference-precision-and-measurement-contract-v0.1.md) | 0.1 | Recommended V1 coordinate/measurement contract | Spatial-reference identity, typed coordinate pipeline, transforms, precision profiles, curve preservation, measurement semantics and accuracy/error declarations |
| ME-05-05 | [Geometry & Field Asset Package and Publication Contract](world-view-me-05-05-geometry-and-field-asset-package-publication-contract-v0.3.md) | 0.3 | Recommended V1 Asset lifecycle integration contract | Vector/field payload separation, immutable Asset ownership, publication, CAS, retention, orphan recovery and provider validation gates |
| ME-05-06 | [Cross-architecture Integration Review & V1 Gates](world-view-me-05-06-cross-architecture-integration-review-v0.5.md) | 0.5 | Cross-architecture Review Working Baseline | Reconciles Core Domain, Map Engine, ME-01/02/03/04 and ME-05-07 through ME-05-11 field package parser/writer contracts; records ownership boundaries and implementation acceptance gates |
| ME-05-07 | [Field Payload Package & Tiling Contract](world-view-me-05-07-field-payload-package-and-tiling-contract-v0.5.md) | 0.5 | Recommended V1 Field Package Working Contract | Single immutable package per Field Revision, canonical manifest, tile directory, random reads, local edits, halo and cache semantics |
| ME-05-08 | [Field Package Binary Envelope & Directory Specification](world-view-me-05-08-field-package-binary-envelope-and-directory-specification-v0.3.md) | 0.3 | Recommended V1 Byte-level Format Contract | Fixed header, manifest, directory entries, tile payload ranges, reader validation and writer publication protocol |
| ME-05-09 | [Canonical Field Package Manifest & Golden Byte Fixtures](ME-05-09-canonical-field-package-manifest-and-golden-byte-fixtures-v0.1.md) | 0.1 | Recommended V1 Serialization Contract | Strict JSON Schema, RFC 8785 JCS canonical bytes, three raw binary golden packages and exact hash inventory |
| ME-05-10 | [Field Package Parser / Writer Implementation Contract](ME-05-10-field-package-parser-writer-implementation-contract-v0.1.md) | 0.1 | Recommended V1 Implementation Contract | .NET reader/writer API, open/read/write algorithms, dense directory validation, integrity boundaries, typed errors, resource limits and platform conformance |
| ME-05-11 | [Field Package Parser / Writer Developer Handoff & Test Strategy](ME-05-11-field-package-parser-writer-developer-handoff-and-test-strategy-v0.1.md) | 0.1 | Developer Handoff / Implementation Plan | Sequenced implementation slices, code boundaries, test layers, golden fixture policy, dependency gates, Windows/macOS CI, benchmarks and definition of done |

### ME-05 current closure and remaining gates

- **ME-05-01 product/domain gate closed:** H-G01 adopted; Map Source Field Baseline is Map-owned and Field Revision updates apply retroactively to every Branch and Historical Time of that Map.
- **Design contracts drafted:** ME-05-02 geometry identity/operation boundary; ME-05-03 spatial predicate and topology boundary; ME-05-04 coordinate reference, precision and measurement semantics; ME-05-05 immutable geometry/field Asset package and publication lifecycle; ME-05-06 cross-architecture reconciliation; ME-05-07 V0.5; ME-05-08 V0.3; ME-05-09 canonical manifest and golden byte fixtures; ME-05-10 parser/writer implementation contract.
- **Existing Core Domain alignment:** P2-09-19 V0.2 stores component identity in typed owner payloads (no V1 component table); P2-09-15 V0.5 includes adjust/refine/add/remove and defers split/merge; P2-09-20 V0.2 keeps formal relationships typed and distinct from geometric coincidence.
- **Completed at design level:** canonical manifest schema (P2-09-34), 28 manifest validation vectors (P2-09-35), and three raw WVFLDPK 1.0 golden byte packages with verified package/tile hashes (ME-05-09). ME-05-07 V0.5 and ME-05-08 V0.3 reconcile their conformance requirements to these fixtures. These fixtures have not been run through a production parser/writer.
- **Completed at design level:** ME-05-10 V0.1 specifies reader/writer boundaries, algorithms, deterministic output, resource policy, typed errors and implementation acceptance. P2-09-36 adds 41 parser/writer conformance vectors; these remain unexecuted.
- **Developer handoff prepared:** ME-05-11 V0.1 converts ME-05-10 into S0–S10 implementation slices, automated test layers, golden fixture handling, dependency/codec gates, resource/security requirements, Windows/macOS CI and benchmark evidence. This is a handoff plan, not implementation or test completion.
- **Open:** production parser/writer code, JCS/JSON Schema library selection, production elevation precision and category encoding profile, codec/library selection, fuzzing, performance benchmarks, Windows/macOS byte-identical execution evidence, specialist topology validation and selected-library measurement/transform proof. ME-05-02/03 are recommended working contracts, not frozen ADRs, executable descriptors or implementation authorization.

### ME-05 work sequence

| Work item | Focus | Main output |
|---|---|---|
| ME-05-A | **Complete as working contracts** — Field Dataset authority and persistence semantics | ME-05-01 V0.5 |
| ME-05-B | **Complete as working contract** — Shared Geometry Model and Component Operations | ME-05-02 V0.1 |
| ME-05-C | **Complete as working contract** — Topology and Spatial Predicates | ME-05-03 V0.1 |
| ME-05-D | **Complete as working contract** — Coordinate Reference, Precision & Measurement | ME-05-04 V0.1 |
| ME-05-E | **Complete as working contract** — Geometry/Field Asset Package & Publication Integration | ME-05-05 V0.3 |
| ME-05-F | **Complete as working review** — Map Engine / Core Domain Integration & Cross-architecture Verification | ME-05-06 V0.5 | Typed API boundaries, specialist constraints, acceptance matrix and implementation gates |
| ME-05-G | **Complete as working contract** — Field Payload Package & Tiling | ME-05-07 V0.5 | Seekable package, tile addressing, random reads, local edits and halo/cache semantics |
| ME-05-H | **Complete as working specification** — Field Package Binary Envelope | ME-05-08 V0.3 | Byte-level header, manifest, directory, payload ranges and reader/writer validation |
| ME-05-I | **Complete as working specification** — Canonical Manifest & Golden Byte Fixtures | ME-05-09 V0.1; P2-09-34; P2-09-35 | Strict manifest schema, JCS byte rules, three raw binary fixtures, package/tile hashes; implementation execution remains open |
| ME-05-J | **Complete as working implementation contract** — Field Package Parser / Writer | ME-05-10 V0.1; P2-09-36 | .NET API boundary, reader/writer algorithms, integrity/error/resource/concurrency rules and 41 acceptance vectors; code and execution evidence remain open |
| ME-05-K | **Complete as developer handoff** — Parser / Writer implementation and test strategy | ME-05-11 V0.1 | S0–S10 developer sequence, test matrix, fixture/dependency policy, platform proof and delivery checklist; implementation remains open |

## ME-06 — Shared Interaction & Tool Contracts (design workstream in progress)

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-06-00 | [Shared Interaction & Tool Contracts](world-view-me-06-shared-interaction-and-tool-contracts-v0.3.md) | 0.3 | Specialist Workstream Scope / Design Plan | Shared tool capability declarations, input routing, gesture lifecycle, typed-intent mapping, query freshness, dependency-aware Candidate selection and atomic submission outcomes |
| ME-06-A | [Tool & Operation Capability Inventory](world-view-me-06-a-tool-and-operation-capability-inventory-v0.1.md) | 0.1 | Capability Inventory Working Baseline | Shared/specialist built-in tool catalog, Typed Intent mapping, formal write paths, runtime evidence states and adoption blockers |
| ME-06-B | [Input Routing & Gesture Lifecycle](world-view-me-06-b-input-routing-and-gesture-lifecycle-v0.1.md) | 0.1 | Input/Gesture Interaction Working Baseline | Shared input priority, tool arming, immutable gesture context, capture/interruption, cancellation, accessibility and typed-intent handoff |
| ME-06-C | [Spatial Query Freshness](world-view-me-06-c-spatial-query-freshness-v0.1.md) | 0.1 | Spatial-query Consistency Working Baseline | Immutable query context token, request sequencing, invalidation, stale-result handling and apply-time re-resolution |
| ME-06-D | [Typed Intent & Formal Operation Routing](world-view-me-06-d-typed-intent-and-formal-operation-routing-v0.1.md) | 0.1 | Typed Intent / Application Routing Working Baseline | Typed intent envelope, capability/descriptor resolution, four formal write paths, preflight, authorization binding, idempotency and reconciliation |
| ME-06-E | [Candidate Review & Selection Contract](world-view-me-06-e-candidate-review-and-selection-contract-v0.1.md) | 0.1 | Candidate Review / Selection Working Baseline | Exact-version review, typed dependency closure, validation evidence, immutable Selection Snapshots, Change Set authorization and atomic submission outcomes |
| ME-06-F | [Recovery, Accessibility & Acceptance Contract](world-view-me-06-f-recovery-accessibility-and-acceptance-contract-v0.1.md) | 0.1 | Recovery / Accessibility / Acceptance Working Baseline | Unified outcome and retry semantics, unknown-commit reconciliation, immutable Asset recovery, keyboard/screen-reader parity and Windows/macOS conformance scenarios |
| ME-06-G | [Cross-architecture Integration Review](world-view-me-06-g-cross-architecture-integration-review-v0.1.md) | 0.1 | Cross-architecture Integration Review Working Baseline | Reconciles ME-06-A–F with Core Domain, AI Assistant, Map Engine, ME-05 Assets, AF-02 lifecycle; defines Change Set atomic-boundary constraint and implementation gates |

ME-06 refines the conceptual interaction architecture in ME-01-06 using the concrete creator workflows in ME-02-G, ME-03-G and ME-04-G. ME-06-A through ME-06-G are complete as design working baselines; none implies runtime implementation completion. ME-06-A inventories capability and adoption blockers; ME-06-B defines shared input routing and gesture lifecycle; ME-06-C defines query freshness, invalidation and apply-time re-resolution; ME-06-D defines typed intent, descriptor/capability resolution, four formal write paths, preflight, authorization binding, idempotency and reconciliation; ME-06-E defines exact-version Candidate review, dependency-closed partial selection, immutable Selection Snapshots and atomic Change Set submission; ME-06-F defines recovery, retry/idempotency, unknown-outcome reconciliation, accessibility parity and platform acceptance evidence; ME-06-G reconciles A–F across architecture boundaries and establishes one atomic commit boundary per Change Set. Runtime implementation, Core Domain adoption, Windows/macOS validation and accessibility conformance remain open. V1 remains a finite built-in tool set; arbitrary plug-in loading, scripting and collaborative interaction are out of scope.

## Subsequent specialist workstreams

| Workstream | Planned responsibility | Entry condition |
|---|---|---|
| ME-03 Settlements & Urban Geography | **Complete as design working baseline** — Settlements, political/administrative regions, land use, roads and spatial validation | ME-01 shared baseline; ME-02 natural geography constraints; implementation gates tracked in ME-03-H |
| ME-04 Buildings & Interior Spaces | Building/site association, floors, rooms, entrances, vertical and underground spaces | Shared Object/geometry and local coordinate contracts |
| ME-05 Shared Geometry & Spatial Operations | Component identity, geometry operations, topology, physical field/Asset persistence and spatial service contracts | ME-05-01 Field Dataset authority contract; findings from ME-01/ME-02/ME-03/ME-04 |
| ME-06 Shared Interaction / Tool Contracts | **Started** — concrete tool-to-operation mapping, gesture/query freshness and Candidate review contracts | Specialist editor workflows; scope baseline ME-06-00 |
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
