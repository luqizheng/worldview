# World View — Map Editor ME-01 Work Plan & Document Status

- Version: 0.2
- Date: 2026-11-21
- Status: Working plan — updated after ME-01-01 architecture draft

## 1. Recommended sequence

Proceed with ME-01 as the foundation before deepening the specialist editors. The recommended order is:

1. **ME-01-01 Overall Map Editor Architecture** — editor modes, shared canvas, spatial scopes, object model, layer/presentation model, AI integration, and boundaries with Core Domain and map-engine runtime.
2. **ME-01-02 World Knowledge Model & Story-to-Spatial Bridge** — define how story entities, assertions, relations, provenance, uncertainty, and time are interpreted as spatial intents. Conceptual baseline drafted.
3. **ME-01-03 Guided Initialization** — define the story-led adaptive process from intake to staged candidates, review, authorization, apply, and incremental refinement. Product-flow baseline drafted.
4. **ME-01-04 Spatial Scope & Multi-scale Consistency** — world/region/locality/site scopes, scale transitions, dependency tracking, impact analysis, bounded recalculation.
5. **ME-01-05 Map Object & Presentation Model** — semantic objects, geometry/identity separation, natural-geography and fantasy-cartography presentations, layer and symbol relationships.
6. **ME-01-06 Editor Interaction Framework** — shared canvas, selection, tools, properties, object tree, snapping, undo/redo, candidate review and AI conversation integration.
7. **ME-01-07 Cross-architecture consistency review** — reconcile ME-01 with Core Domain history, AI Assistant Task/Generation Session lifecycle, and the map-engine technology evaluation.

After ME-01 is coherent, proceed to ME-02 Terrain & Natural Geography, ME-03 Settlements, ME-04 Buildings & Interior Spaces, ME-05 Spatial/Geometry System, ME-06 Interaction & Tools, and ME-07 Rendering/Technology.

## 2. Why this order

- World Knowledge and Story-to-Spatial Bridge are prerequisites for story-led initialization; they should be captured before detailed generation algorithms.
- Guided Initialization depends on both the knowledge model and the existing AI Assistant execution lifecycle.
- Multi-scale consistency must be designed before specialist editors can safely modify shared spatial content.
- Object identity, geometry, and presentation boundaries must be settled before detailed tool and rendering decisions.
- Rendering technology should not dictate the semantic domain model.

## 3. Current document status

| Document | Status | Notes |
|---|---|---|
| ME-01-01 Overall Architecture v0.1 | Drafted | Unified editor shell, professional modes, shared spatial/presentation model, Core Domain / AI Assistant / Map Engine boundaries; detailed contracts remain open |
| ME-01-02 World Knowledge & Story-to-Spatial Bridge v0.1 | Drafted | Conceptual architecture is sufficiently discussed; physical schema and implementation details remain open |
| ME-01-03 Guided Initialization v0.1 | Drafted | Product lifecycle and major policies are sufficiently discussed; exact state machine/UI/tool contracts remain open |
| ME-01-04 Multi-scale Consistency | Planned | Next after overall architecture framing |
| ME-01-05 Object & Presentation Model | Planned | Must align with Core Domain Object identity and geometry semantics |
| ME-01-06 Interaction Framework | Planned | Depends on shared canvas and editing model |
| ME-01-07 Consistency Review | Planned | Required before calling ME-01 complete |

## 4. Next work

1. ME-01-04 Spatial Scope & Multi-scale Consistency — formalize cross-scale identity, containment, adjacency, resolution/LOD, dependency and bounded impact analysis.
2. ME-01-05 Map Object & Presentation Model — formalize semantic object families, geometry association, layer semantics, and coordinated natural/fantasy representations.
3. ME-01-06 Editor Interaction Framework — specify canvas interaction, selection, tools, inspector, object navigator, snapping, local Undo/Redo, and AI review surfaces.
4. ME-01-07 Cross-architecture consistency review — reconcile the complete ME-01 set against the latest Core Domain and AI Assistant baselines and Map Engine evaluation.

## 5. Persistence and authority rules

- World Knowledge must not become a shadow authority for formal Map Objects or geometry.
- AI Assistant owns task, candidate, validation, selection, proposal, authorization, and submission evidence according to its existing architecture.
- Core Domain remains the sole authority for formal map objects, stable identities, geometry, spatial relationships, history, and reconstructed world state.
- LLM does not write database tables directly; formal operations go through Domain Services.
- The physical persistence design for World Knowledge is deliberately not frozen by these conceptual documents.

## 6. Library publication status

The target Library folder `/World View/Map Editor/ME-01/` has been created. Upload of the two drafted documents was attempted on 2026-11-21 but was rejected by the Library upload quota (`throttled`, retry window reported as 21 hours). Therefore the documents are currently local generated drafts and are **not yet published to the Library**. Retry publication after the quota window; do not treat local drafts as Library records.