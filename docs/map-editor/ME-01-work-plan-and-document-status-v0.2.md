# ME-01 Work Plan and Document Status

This document tracks the Map Editor ME-01 design package. The editor design is a product/architecture workstream; implementation is performed by the development team.

## Current design documents

| ID | Document | Version | Status | Responsibility |
|---|---|---:|---|---|
| ME-01-01 | [Editor Overall Architecture](world-view-me-01-01-editor-overall-architecture-v0.1.md) | 0.1 | Architecture Working Baseline | Unified editor, modes, shared spatial model and subsystem boundaries |
| ME-01-02 | [World Knowledge and Story-Spatial Bridge](world-view-me-01-02-world-knowledge-and-story-spatial-bridge-v0.1.md) | 0.1 | Design Working Baseline | Story facts, creator intent, inference, provenance and spatial intent |
| ME-01-03 | [Guided Initialization](world-view-me-01-03-guided-initialization-v0.1.md) | 0.1 | Design Working Baseline | Adaptive conversation, staged generation, review and incremental refinement |
| ME-01-04 | [Multi-scale Spatial Model & Global Consistency](world-view-me-01-04-multiscale-spatial-consistency-v0.1.md) | 0.1 | Architecture Working Baseline | Spatial scopes, resolution/LOD, cross-scale relations, constraints and bounded recalculation |
| ME-01-05 | [Map Object, Geometry & Representation Model](world-view-me-01-05-map-object-geometry-representation-model-v0.1.md) | 0.1 | Architecture Working Baseline | Semantic identity, ObjectState, geometry association/components, spatial relationships, presentation, layers and derived render data |
| ME-01-06 | [Editor Interaction Framework](world-view-me-01-06-editor-interaction-framework-v0.1.md) | 0.1 | Editor Interaction Working Baseline | Canvas navigation/scope, shared selection and tools, transient edit sessions, Candidate review, diagnostics, permissions and history interaction |

## Remaining ME-01 work

| ID | Planned deliverable | Main questions |
|---|---|---|
| ME-01-07 | Cross-architecture Consistency Review | Reconcile ME-01-01 through ME-01-06 with Core Domain, AI Assistant, Map Generation, Map Engine and persistence contracts; record gaps and required follow-up |

ME-01-06 is complete as an Editor Interaction Working Baseline. Its detailed Avalonia controls, input bindings, exact tool/API types, geometry provider and view-configuration persistence remain open implementation/design gates.

## Working sequence and status rules

1. ME-01-01 through ME-01-05 establish the product, spatial and object/presentation architecture baseline.
2. ME-01-06 defines the shared Canvas-centered interaction framework over that model.
3. ME-01-07 performs the cross-document consistency review before ME-01 is treated as complete.
4. ME-02 through ME-07 then develop specialist editors, shared geometry, interaction/tool details and rendering/technical architecture.

Documents marked Working Baseline or Proposal are not frozen physical schemas and do not authorize implementation by themselves. Accepted/Frozen ADRs and explicitly accepted domain contracts remain binding within their scope. A newer document supersedes an older one only where it explicitly states the scope of supersession.

## Documentation authority

The GitHub repository `luqizheng/worldview` is the authoritative, version-controlled location for World View project documents. Update the repository first; do not treat a separate project documentation library or local working copy as the formal source. Before each edit, fetch the current GitHub version and preserve concurrent changes. Retain prior versioned documents where a new version supersedes them.

Local files may be used temporarily for drafting or processing, but the final artifact and its status must be committed to GitHub.
