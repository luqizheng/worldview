# World View Project Documentation

This repository is the version-controlled documentation baseline for the World View project.

## Documentation map

- [Migration Manifest](MIGRATION-MANIFEST.md) — source scope, imported inventory, and remaining binary archives.

- `architecture/core-domain/`: Core Domain, Map/Branch history, persistence, Historical Time, schema and reconstruction contracts.
- `adr/`: accepted, frozen, amended, and rejected architecture decisions.
- `P2-01-D01/`: historical-state and command-domain discussion record.
- `ai-assistant/`: AI product architecture, task planning/execution, context, tools, providers, authorization, proposal/application, database schemas, and recovery.
- `map-editor/`: desktop editor architecture, world-knowledge/story-spatial bridge, Guided Initialization, multi-scale spatial consistency, map Object/geometry/presentation model, and the ME-01 design work plan.
- `map-generation/`: map generation specification, generation session lifecycle, and related source README.
- `map-engine/`: renderer contract, map visualization, geometry interaction, natural geography, and Unit animation.
- `history/`: reserved for superseded or historical source documents retained for traceability.
- `README-implementation-handoff-v1.0.md`: implementation handoff guidance and scope boundary.

## Current persistence design baseline

- [P2-09-11 Physical Schema V0.8](architecture/core-domain/p2-09-11-physical-schema-design-v0.8.md) — current reconciled Core Domain relational proposal; not executable DDL.
- [P2-09-13 Integrated Schema Inventory V0.2](architecture/core-domain/p2-09-13-integrated-schema-inventory-migration-graph-v0.2.md) — Core/AI table inventory and corrected migration dependency graph.
- [P2-09-16 Shared Definition / Checkpoint Coherence V0.1](architecture/core-domain/world-view-core-domain-p2-09-16-shared-definition-checkpoint-coherence-contract-v0.1.md) — retroactive current-definition and Checkpoint freshness rules.
- [AI Assistant Logical Schema V1.16](ai-assistant/world-view-ai-assistant-unified-logical-database-schema-v1.16.md) — 28 AI-owned domain tables.
- [P2-09-05 Historical Time / Calendar Contract V0.3](architecture/core-domain/world-view-core-domain-p2-09-05-historical-time-ordering-persistence-contract-v0.3.md) — current canonical-time and Calendar persistence baseline.
- [P2-09-12 Provider / Transaction Proof Spec V0.2](architecture/core-domain/world-view-core-domain-p2-09-12-provider-transaction-proof-test-spec-v0.2.md) — required development verification; tests have not been claimed as executed.

**Source mismatch note:** the preserved README V1.38 snapshot mentions a standalone P2-09-14 Calendar validation contract, but the repository path previously assigned to it contained a duplicate README snapshot, and no matching standalone source artifact was located during the audit. Until that contract is authored and reviewed, use P2-09-05 V0.3 as the current Calendar persistence source and keep the remaining Calendar validation gates open.

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
