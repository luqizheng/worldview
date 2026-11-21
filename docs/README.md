# World View Project Documentation

This repository is the version-controlled documentation baseline for the World View project.

## Documentation map

- [Migration Manifest](MIGRATION-MANIFEST.md) — source scope, imported inventory, and remaining binary archives.

- `architecture/core-domain/`: Core Domain, Map/Branch history, persistence, Historical Time, schema and reconstruction contracts.
- `adr/`: accepted, frozen, amended, and rejected architecture decisions.
- `P2-01-D01/`: historical-state and command-domain discussion record.
- `ai-assistant/`: AI product architecture, task planning/execution, context, tools, providers, authorization, proposal/application, database schemas, and recovery.
- `map-editor/`: shared desktop editor architecture (ME-01), Terrain & Natural Geography Editor (ME-02: land/water, elevation, terrain features, hydrology, climate, ecology, calculation contracts, UX acceptance and cross-architecture review), world-knowledge/story-spatial bridge, Guided Initialization, multi-scale spatial consistency, map Object/geometry/presentation model, Canvas-centered interaction framework, and specialist work plan and status.
- `map-generation/`: map generation specification, generation session lifecycle, and related source README.
- `map-engine/`: renderer contract, map visualization, geometry interaction, natural geography, and Unit animation.
- `history/`: reserved for superseded or historical source documents retained for traceability.
- `README-implementation-handoff-v1.0.md`: implementation handoff guidance and scope boundary.

## Current persistence design baseline

- [P2-09-05 Historical Time / Calendar Contract V0.3](architecture/core-domain/world-view-core-domain-p2-09-05-historical-time-ordering-persistence-contract-v0.3.md) — canonical Map-relative time and Calendar persistence baseline.
- [P2-09-08 Command Descriptor / Replay Contract V0.2](architecture/core-domain/p2-09-08-command-descriptor-replay-contract-v0.2.md) — typed Command descriptor and deterministic replay rules.
- [P2-09-11 Physical Schema V0.10](architecture/core-domain/p2-09-11-physical-schema-design-v0.10.md) — current reconciled Core Domain relational proposal; not executable DDL.
- [P2-09-12 Provider / Transaction Proof Spec V0.4](architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.4.md) — Windows/macOS SQLite shared-transaction, reservation, typed-link, Asset, relationship and Object-presence failure-injection test specification; tests have not been claimed as executed.
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

- [P2-09-26 V1 Object Type Descriptor Registry V0.1](architecture/core-domain/p2-09-26-v1-object-type-descriptor-registry-v0.1.json) — machine-readable descriptors for ten built-in Object Types, typed fields, geometry roles and Command capability boundaries.
- [P2-09-27 V1 Relationship Type Descriptor Registry V0.1](architecture/core-domain/p2-09-27-v1-relationship-type-descriptor-registry-v0.1.json) — endpoint matrices, direction, cardinality, cycle and endpoint-activity policies for five candidate Relationship Types.
- [P2-09-28 Domain Descriptor Validation Vectors V0.2](architecture/core-domain/p2-09-28-domain-descriptor-validation-vectors-v0.2.json) — reconciled positive/negative Object, Relationship and cross-cutting fixtures; design-only, not executed.
- [P2-09-29 Domain Descriptor Cross-Registry Consistency Contract V0.1](architecture/core-domain/p2-09-29-domain-descriptor-cross-registry-consistency-contract-v0.1.md) — Object/Relationship/Command scope alignment, endpoint/cardinality semantics, Map/Branch isolation and checkpoint compatibility.
- [ME-02-F Calculation Contracts V0.1](map-editor/world-view-me-02-calculation-contracts-v0.1.md) — deterministic calculation requests, provenance, freshness, dependency invalidation, checkpoint coherence, validation severity and atomic output publication.

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
- Map rendering, geospatial computation, and renderer technology remain separately evaluated. No final rendering-engine selection is implied here.

## Migration status

The repository contains the existing ADR and P2-01-D01 package plus the currently identified Core Domain, AI Assistant, Map Editor, and Map Engine design documents. It is a curated documentation baseline, not a byte-for-byte mirror of the source Library. Older README snapshots, duplicate copies, transient drafts, and unrelated project material are intentionally not bulk-imported. Any remaining source documents should be added only after checking their authority, current status, and relationship to this baseline.

## Design principles

- Prefer simple, explicit, maintainable components over distributed or enterprise-scale infrastructure.
- Preserve the distinction between confirmed decisions, recommendations, proposals, and validation gates.
- Keep product/domain design separate from implementation execution. Development implementation is owned by the development team.
