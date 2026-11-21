# World View Project Documentation

This repository is the version-controlled documentation baseline for the World View project.

## Documentation map

- [Migration Manifest](MIGRATION-MANIFEST.md) — source scope, imported inventory, and remaining binary archives.

- `architecture/core-domain/`: Core Domain, Map/Branch history, persistence, Historical Time, schema and reconstruction contracts.
- `application/`: Application Host, Project Package, Project Session, Map Workspace Session and desktop application lifecycle. See [AF-01 Application Foundation Architecture V0.1](application/AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md) and [AF-01 Developer Handoff V0.1](application/AF-01_DEVELOPER_HANDOFF_v0.1.md). [AF-02 Developer Implementation Prompt V0.1](application/AF-02_DEVELOPER_IMPLEMENTATION_PROMPT_v0.1.md) — executable implementation scope, codebase preflight, acceptance tests and delivery requirements.
- `adr/`: accepted, frozen, amended, and rejected architecture decisions.
- `P2-01-D01/`: historical-state and command-domain discussion record.
- `ai-assistant/`: AI product architecture, task planning/execution, context, tools, providers, authorization, proposal/application, database schemas, and recovery.
- `map-editor/`: shared desktop editor architecture (ME-01), Terrain & Natural Geography Editor (ME-02: land/water, elevation, terrain features, hydrology, climate, ecology, calculation contracts, UX acceptance and cross-architecture review), Shared Geometry & Spatial Operations (ME-05), world-knowledge/story-spatial bridge, Guided Initialization, multi-scale spatial consistency, map Object/geometry/presentation model, Canvas-centered interaction framework, and specialist work plan and status.
- [ME-03 Settlements & Urban Geography Editor V0.2](map-editor/world-view-me-03-settlements-and-urban-geography-editor-v0.2.md) — specialist scope and work plan, reconciled with exact Core Domain descriptor-registry endpoints.
- [ME-03-A Domain Inventory & Descriptor Reconciliation V0.1](map-editor/world-view-me-03-a-domain-inventory-and-descriptor-reconciliation-v0.1.md) — exact Settlement/Road/PoliticalRegion capabilities, enabled relationship endpoint pairs and urban-domain gaps.
- [ME-03-B Settlement Model & Authoring Workflows V0.1](map-editor/world-view-me-03-b-settlement-model-and-authoring-workflows-v0.1.md) — settlement identity, anchor/extent, classification, multiscale representation and authoring lifecycle.
- [ME-03-C Political & Administrative Geography V0.1](map-editor/world-view-me-03-c-political-and-administrative-geography-v0.1.md) — PoliticalRegion territory semantics, boundaries, adjacency, settlement containment and temporal border changes.
- [ME-03-D Land Use & Functional Areas V0.1](map-editor/world-view-me-03-d-land-use-and-functional-areas-v0.1.md) — separates human land use from terrain/land cover and presentation; proposes a bounded FunctionalArea concept without changing the current registry.
- [ME-03-E Road & Path Network Authoring V0.1](map-editor/world-view-me-03-e-road-and-path-network-authoring-v0.1.md) — defines explicit road connectivity, road-segment granularity, grade-separated crossings and current Core Domain network gaps.\n- [ME-03-F Settlement Placement & Urban Spatial Validation V0.1](map-editor/world-view-me-03-f-settlement-placement-and-urban-spatial-validation-v0.1.md) — defines explainable settlement-placement checks across terrain, water, climate and possible road access, with hard/soft constraints, uncertainty and freshness.
- [ME-03 Settlements & Urban Geography Editor V0.1](map-editor/world-view-me-03-settlements-and-urban-geography-editor-v0.1.md) — specialist workstream scope, inherited Core Domain vocabulary, settlement/political/land-use/road semantics, work packages and open gates.
- `map-generation/`: map generation specification, generation session lifecycle, and related source README.
- `map-engine/`: renderer contract, map visualization, geometry interaction, natural geography, Unit animation, and production presentation architecture. Key current documents: [Production Renderer Architecture V0.2](map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md), [PRA-01 Presentation Context & Immutable Scene Snapshot Contract V0.1](map-engine/PRA-01_PRESENTATION_CONTEXT_AND_IMMUTABLE_SCENE_SNAPSHOT_CONTRACT_v0.1.md), and [PRA-01 Developer Handoff V0.1](map-engine/PRA-01_DEVELOPER_HANDOFF_v0.1.md).
- `history/`: reserved for superseded or historical source documents retained for traceability.
- `README-implementation-handoff-v1.0.md`: implementation handoff guidance and scope boundary.

## Current persistence design baseline

- [P2-09-05 Historical Time / Calendar Contract V0.3](architecture/core-domain/world-view-core-domain-p2-09-05-historical-time-ordering-persistence-contract-v0.3.md) — canonical Map-relative time and Calendar persistence baseline.
- [P2-09-08 Command Descriptor / Replay Contract V0.2](architecture/core-domain/p2-09-08-command-descriptor-replay-contract-v0.2.md) — typed Command descriptor and deterministic replay rules.
- [P2-09-11 Physical Schema V0.11](architecture/core-domain/p2-09-11-physical-schema-design-v0.11.md) — current reconciled Core Domain relational proposal including the adopted Map-owned Field Dataset/Revision tables; not executable DDL.
- [P2-09-12 Provider / Transaction Proof Spec V0.6](architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.6.md) — Windows/macOS SQLite shared-transaction, reservation, typed-link, Asset, Field Dataset/Revision, relationship and Object-presence failure-injection test specification; tests have not been claimed as executed.
- [P2-09-13 Integrated Schema Inventory V0.6](architecture/core-domain/p2-09-13-integrated-schema-inventory-migration-graph-v0.6.md) — Core/AI table inventory and corrected migration dependency graph.
- [P2-09-14 Historical Time / Calendar Validation Contract V0.2](architecture/core-domain/p2-09-14-historical-time-calendar-validation-contract-v0.2.md) — validation matrix for Calendar conversion, precision intervals, reforms, Branch inheritance and sustained Commands; recommended, not a frozen ADR.
- [P2-09-15 Command Descriptor Catalog V0.5](architecture/core-domain/p2-09-15-command-descriptor-catalog-v0.5.md) — bounded V1 Command types, History-local Object activation/deactivation, deferred families and non-Command operation boundaries.
- [P2-09-16 Shared Definition / Checkpoint Coherence V0.1](architecture/core-domain/world-view-core-domain-p2-09-16-shared-definition-checkpoint-coherence-contract-v0.1.md) — retroactive current-definition and Checkpoint freshness rules.
- [P2-09-17 Command Payload / Validation Contract V0.5](architecture/core-domain/p2-09-17-command-payload-envelope-and-validation-contract-v0.5.md) — canonical payload envelope, typed Object/Asset references and descriptor-level validation.
- [P2-09-18 Object Type Descriptor / Attribute Contract V0.2](architecture/core-domain/p2-09-18-object-type-descriptor-and-attribute-contract-v0.2.md) — typed built-in Object Type schemas, definition/runtime/Initial State separation and compatibility pins.
- [P2-09-19 Geometry Component Identity / Payload Contract V0.2](architecture/core-domain/p2-09-19-geometry-component-identity-and-payload-contract-v0.2.md) — stable component IDs, roles, immutable geometry Assets and History-local operations.
- [P2-09-20 Relationship Descriptor / Temporal Contract V0.2](architecture/core-domain/p2-09-20-relationship-descriptor-and-temporal-contract-v0.2.md) — relationship identity, typed endpoints, cardinality, temporal semantics and endpoint activity policy.
- [P2-09-21 V1 Built-in Object Type Seed Catalog V0.1](architecture/core-domain/p2-09-21-v1-built-in-object-type-seed-catalog-v0.1.md) — ten recommended initial semantic Object Types and explicit deferred concepts.
- [P2-09-22 V1 Built-in Relationship Type Seed Catalog V0.2](architecture/core-domain/p2-09-22-v1-built-in-relationship-type-seed-catalog-v0.2.md) — five recommended initial relationship descriptors and their endpoint/temporal boundaries.
- [P2-09-23 Command Payload JSON Schema V0.1](architecture/core-domain/p2-09-23-command-payload-json-schema-v0.1.json) — machine-readable V1 payload envelope and operation discriminators; cross-record validation remains in Core Domain.
- [P2-09-24 V1 Built-in Object Type Field Catalog V0.1](architecture/core-domain/p2-09-24-v1-built-in-object-type-field-catalog-v0.1.md) — first-pass typed fields, geometry role cardinalities, runtime candidates and relationship participation for ten seed types.
- [P2-09-25 V1 Geometry Asset Encoding Decision V0.1](architecture/core-domain/p2-09-25-v1-geometry-asset-encoding-and-coordinate-precision-decision-v0.1.md) — recommended canonical JSON geometry Asset envelope and explicit precision/format validation gates.

- [P2-09-26 V1 Object Type Descriptor Registry V0.2](architecture/core-domain/p2-09-26-v1-object-type-descriptor-registry-v0.2.json) — machine-readable descriptors for ten built-in Object Types, typed fields, geometry roles and Command capability boundaries.
- [P2-09-27 V1 Relationship Type Descriptor Registry V0.2](architecture/core-domain/p2-09-27-v1-relationship-type-descriptor-registry-v0.2.json) — endpoint matrices, direction, cardinality, cycle and endpoint-activity policies for five candidate Relationship Types.
- [P2-09-28 Domain Descriptor Validation Vectors V0.2](architecture/core-domain/p2-09-28-domain-descriptor-validation-vectors-v0.2.json) — reconciled positive/negative Object, Relationship and cross-cutting fixtures; design-only, not executed.
- [P2-09-29 Domain Descriptor Cross-Registry Consistency Contract V0.1](architecture/core-domain/p2-09-29-domain-descriptor-cross-registry-consistency-contract-v0.1.md) — Object/Relationship/Command scope alignment, endpoint/cardinality semantics, Map/Branch isolation and checkpoint compatibility.
- [P2-09-30 V1 Field Descriptor Registry V0.1](architecture/core-domain/p2-09-30-v1-field-descriptor-registry-v0.1.json) — Core-owned elevation and land/water field semantics, Map-wide revision scope, units, vertical reference, categories, no-data and provenance.
- [P2-09-31 Field Dataset Validation Vectors V0.1](architecture/core-domain/p2-09-31-field-dataset-validation-vectors-v0.1.json) — 32 design-only positive/negative Field Dataset and Field Revision validation fixtures; not executed.
- [P2-09-32 Field Package/Tiling Validation V0.1](architecture/core-domain/p2-09-32-field-package-tiling-validation-vectors-v0.1.json) — 45 design-only package/container, tile-index, integrity, random-read, local-edit, halo, cache and cross-platform fixtures; not executed.
- [ME-02-F Calculation Contracts V0.4](map-editor/world-view-me-02-calculation-contracts-v0.4.md) — pins Field Package format and source package identity in calculation provenance; V1 freshness remains conservatively Dataset-revision based.
- [ME-05-01 Field Dataset Authority & Persistence Contract V0.4](map-editor/world-view-me-05-01-field-dataset-authority-and-persistence-contract-v0.4.md) — adopts the Field Package logical boundary and records remaining byte-layout, codec, precision and implementation gates.
- [ME-02-G UX Scenarios & Acceptance V0.2](map-editor/world-view-me-02-ux-scenarios-and-acceptance-v0.2.md) — end-to-end natural-geography creator journeys, Inspector, diagnostics, formal authoring targets, failure/recovery and Figma handoff.
- [ME-02-H Cross-architecture Review V0.2](map-editor/world-view-me-02-cross-architecture-review-v0.2.md) — Core Domain/Map Engine/Map Generation/ME-01 reconciliation, implementation gates and ME-02 package disposition.
- [ME-02 Overall Baseline V0.6](map-editor/world-view-me-02-terrain-and-natural-geography-editor-v0.6.md) — reconciled natural-geography product/domain baseline through ME-02-H.
- [ME-05-03 Topology & Spatial Predicate Contract V0.1](map-editor/world-view-me-05-03-topology-and-spatial-predicate-contract-v0.1.md) — geometric predicates, boundary semantics, adjacency candidates, topology/relationship separation, numerical certainty and query freshness.
- [ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1](map-editor/world-view-me-05-04-coordinate-reference-precision-and-measurement-contract-v0.1.md) — immutable spatial-reference definitions, typed coordinate pipeline, explicit transforms, per-reference precision profiles, curve preservation and measurement accuracy semantics.
- [ME-05-05 Geometry & Field Asset Package and Publication Contract V0.2](map-editor/world-view-me-05-05-geometry-and-field-asset-package-publication-contract-v0.2.md) — immutable vector/field payload distinction, typed ownership, atomic publication, stale-write rejection, Asset retention and recovery.
- [ME-05-06 Cross-architecture Integration Review & V1 Gates V0.2](map-editor/world-view-me-05-06-cross-architecture-integration-review-v0.2.md) — Core Domain/Map Engine/editor/specialist ownership reconciliation, design findings and implementation acceptance gates.
- [ME-05-07 Field Payload Package & Tiling Contract V0.1](map-editor/world-view-me-05-07-field-payload-package-and-tiling-contract-v0.1.md) — one seekable immutable package Asset per Field Revision, deterministic tile directory, random tile reads, streaming local edits, halo and cache rules; byte-level format and performance proof remain open.
- [ME-05-02 Shared Geometry Model & Component Operations V0.1](map-editor/world-view-me-05-02-shared-geometry-model-and-component-operations-v0.1.md) — shared component identity/lifecycle, formal geometry operation boundary, write-context routing and immutable Asset ownership.
- [Map Editor Work Plan V2.2](map-editor/ME-01-work-plan-and-document-status-v2.1.md) — current workstream status and specialist design inventory.

- [AI Assistant Logical Schema V1.16](ai-assistant/world-view-ai-assistant-unified-logical-database-schema-v1.16.md) — 28 AI-owned domain tables.

**Source provenance note:** the earlier repository path assigned to P2-09-14 contained a duplicate README snapshot, not the validation contract. No matching standalone source artifact was located during the audit, so P2-09-14 V0.1 has now been authored as a new validation contract from P2-09-05 V0.3 and the current persistence baseline; it is not represented as a recovered Library original. Its unresolved Calendar semantics remain explicit.

## Authority and document status

- Accepted/Frozen ADRs define binding domain semantics unless explicitly amended by a later accepted amendment.
- Documents labelled Proposal, Discussion, Draft, Review, Work Plan, or Test Specification are not frozen merely because they are stored here.
- Newer documents supersede older versions only where their scope explicitly says so. Historical source files are retained for traceability rather than silently overwritten.
- The Core Domain owns formal map data and history. AI Assistant proposals are staged and must pass validation, authorization, and Core Domain Domain Services before formal application.
- This repository is the version-controlled project documentation baseline. It does not imply implementation completion, executable DDL readiness, or provider-test completion.

## Baseline snapshots

- [Latest source README V1.38](baseline/README-v1.38.md) — source-library baseline snapshot; this is distinct from this repository's navigation index.
- [Architecture baseline V1.0](architecture-baseline-v1.0.md) — architecture baseline snapshot.

## Current technical direction

- Desktop application: C# + Avalonia.
- Local-first, single-user/small-team, low-concurrency operating model.
- Core Domain is the authority for formal map state, Commands, and history semantics.
- AI Assistant coordinates analysis and proposals; it does not directly mutate formal Core Domain data.
- Production map presentation target: Avalonia 12 + Skia for the map viewport, as defined by [Production Renderer Architecture V0.2](map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md). Geospatial algorithms and data providers remain separately evaluated.

## Migration status

The repository contains the existing ADR and P2-01-D01 package plus the currently identified Core Domain, AI Assistant, Map Editor, and Map Engine design documents. It is a curated documentation baseline, not a byte-for-byte mirror of the source Library. Older README snapshots, duplicate copies, transient drafts, and unrelated project material are intentionally not bulk-imported. Any remaining source documents should be added only after checking their authority, current status, and relationship to this baseline.

## Design principles

- Prefer simple, explicit, maintainable components over distributed or enterprise-scale infrastructure.
- Preserve the distinction between confirmed decisions, recommendations, proposals, and validation gates.
- Keep product/domain design separate from implementation execution. Development implementation is owned by the development team.
