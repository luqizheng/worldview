# World View — ME-01-03 Guided Initialization Specification

- Version: 0.1
- Date: 2026-11-21
- Status: Design Working Baseline — product flow and lifecycle discussed; detailed UI, tool contracts, and physical persistence remain open
- Scope: Story-led world initialization, progressive map generation, review, application, and incremental refinement

## 1. Product goal

Guided Initialization helps a creator move from an incomplete story premise to a coherent initial world map, then progressively enrich individual regions. It is a conversation-led, adaptive workflow, not a one-click map generator and not a static GIS questionnaire.

The creator describes story, characters, species, cultures, factions, history, and broad environmental ideas in ordinary language. World View helps organize that knowledge, identifies important gaps and contradictions, interprets spatial implications, and gradually turns the resulting plan into editable map data.

## 2. Core product principles

1. **Story-first**: creators do not need to understand GIS, geology, climate modeling, or cartographic parameters.
2. **Adaptive conversation**: questions are selected using the current map, confirmed world knowledge, creator goals, and previous answers.
3. **Progressive maturity**: the creator can generate a useful initial world skeleton before every detail is known.
4. **Global consistency**: local generation must consider relevant world- and region-scale constraints.
5. **Creator authority**: AI may infer and recommend, but may not silently rewrite confirmed canon or apply high-impact formal changes outside authorization.
6. **Visible results**: generation produces reviewable, editable spatial candidates and map previews, not merely textual suggestions.
7. **One underlying spatial model**: natural-geography and fantasy-cartography views are different presentations of related authoritative spatial data, not disconnected maps.

## 3. End-to-end lifecycle

### 3.1 Story Intake

The creator describes the broad story background and desired outcome. Input may be incomplete, contradictory, or non-spatial. The system should accept prose, incremental additions, existing documents, and references to current map content.

### 3.2 Knowledge Extraction

The system extracts or updates Knowledge Entities, Assertions, Relations, provenance, status, and historical validity. It must distinguish creator facts, creator intent, AI inference, and pending information.

### 3.3 Adaptive Clarification

The assistant identifies missing information, contradictions, and ambiguities. It asks only when the answer materially affects important spatial structure, creator intent, or generation correctness. It must allow the creator to answer “unknown”, skip, defer, supplement, or revise earlier answers.

Do not ask the creator to supply professional GIS parameters when a meaningful creative question or an explainable AI recommendation is sufficient.

### 3.4 Spatial Interpretation

World Knowledge is passed through the Story-to-Spatial Bridge. The result is a set of Spatial Intents, hard/soft/derived constraints, candidate object types, spatial relations, and dependencies. Narrative entities are not automatically converted to map objects.

### 3.5 Generation Planning

The assistant determines:
- target scale and geographic scope;
- required generation stages;
- dependencies and ordering;
- algorithms/domain services required;
- existing confirmed data that must be preserved;
- validation and acceptance conditions;
- questions that block only a subset of the work.

A plan may contain independent work that can proceed while unresolved questions remain elsewhere.

### 3.6 Candidate Generation

Deterministic geography/geometry services produce spatial candidates. LLM supports semantic interpretation, planning, and explanation; it does not replace hydrology, geometry, topology, or climate computation.

Candidates remain separate from formal Core Domain map data until the established validation, selection, authorization, and application flow is complete.

### 3.7 Validation

Validate at least:
- geometric validity and topology;
- terrain and elevation continuity;
- hydrological plausibility and drainage connectivity;
- climate causality and regional consistency;
- ecological suitability against both environmental conditions and creator-defined preferences;
- consistency with confirmed world knowledge;
- cross-scale relationships and affected existing objects.

Validation findings must identify severity, evidence, affected scope, and whether they block application or are advisory.

### 3.8 Preview and Review

Show the creator:
- the map preview at the relevant scale;
- what is new, changed, preserved, or unresolved;
- which world-knowledge assertions and constraints informed the result;
- important alternatives and trade-offs;
- validation findings;
- downstream impact on existing regions or objects.

The creator can edit candidates, request alternatives, change constraints, or defer decisions.

### 3.9 Authorization and Apply

Formal map application reuses the existing AI Assistant Candidate / Validation / Selection / Change Set / authorization / Core Domain Domain Services flow. The authorization must bind to the exact proposal content and target context according to the existing integration contract. LLM never writes formal map tables directly.

Low-risk operations may follow existing authorization policy; high-impact, ambiguous, canon-changing, or difficult-to-reverse operations require explicit creator confirmation. Do not introduce a blanket “every operation requires approval” rule.

### 3.10 Incremental Refinement

After the initial map exists, the creator can select a region and continue through the same conversation-led process to enrich terrain, water, ecology, settlements, roads, buildings, and history. The workflow must preserve the global map context and re-evaluate only affected dependencies.

## 4. Progressive generation stages

The recommended default order for initial world creation is:

1. **Macro-geographic skeleton** — continents, oceans, coastlines, major mountain systems, major rivers, plains, plateaus, and broad political regions where appropriate.
2. **Natural environment refinement** — climate, precipitation, forests, lakes, wetlands, deserts, grasslands, and ecological regions.
3. **Human and cultural geography** — cities, settlements, fortresses, roads, trade routes, species/cultural territories, and important sites.

This is a default dependency order, not a rigid wizard. Existing creator-provided maps or known world facts may change the order. Each stage produces independently reviewable results.

## 5. Multi-scale model

Use one spatial model with four nested working scales:

- `World`: continents, oceans, global climate drivers, major mountain systems, macro-regions;
- `Region`: regional terrain, watersheds, climate, ecosystems, political/cultural regions;
- `Locality`: cities, villages, roads, land use, local terrain and building groups;
- `Site`: individual buildings, floors, rooms, caves, underground spaces, and local facilities.

These are scopes and levels of detail, not four independent map databases. Spatial references must remain coherent across scales.

## 6. Dependency and impact analysis

A local edit must not trigger unconditional regeneration of the entire world. The system should maintain explicit or deterministically derivable dependencies between source data and derived results.

For a new mountain range, possible affected outputs include:
- elevation and terrain shape;
- windward/leeward precipitation;
- drainage divides, river sources, and watersheds;
- regional climate and ecological suitability;
- settlements, routes, and habitat assumptions where they intersect the affected area.

The impact analyzer should:
1. identify direct and transitive dependencies;
2. calculate a bounded affected region;
3. preserve unaffected confirmed content;
4. distinguish required recalculation from optional recommendations;
5. show affected objects and assumptions before high-impact application;
6. avoid silently changing unrelated creator-confirmed data.

V1 should prefer bounded dependency records and deterministic invalidation/recalculation over a generic workflow/DAG platform.

## 7. Adaptive questioning policy

Ask when:
- missing information changes major world or regional layout;
- plausible answers produce materially different results;
- a contradiction affects confirmed canon or spatial structure;
- proceeding would cause substantial rework;
- creator intent cannot be safely inferred.

Do not ask when:
- a deterministic calculation can resolve the issue;
- the detail is low impact and can remain pending;
- a soft preference can be represented as a recommendation;
- an explainable default can be proposed without changing confirmed canon.

Question behavior:
- ask one focused question at a time by default;
- explain why the answer matters in creator-friendly language;
- offer “not sure”, skip, and defer paths;
- do not repeat already confirmed information;
- revisit earlier answers only when new information creates a material conflict;
- record the answer as evidence and update only the affected assertions/relations.

## 8. Natural geography and climate responsibility

Use Earth-based natural climate and geography principles as the default physical model. Relevant drivers include latitude/solar radiation, elevation, atmospheric circulation, prevailing winds, monsoon patterns where applicable, distance from oceans, land-sea thermal contrast, ocean currents, orographic precipitation, rain shadow, hydrology, seasonality, soils, and vegetation.

V1 does not require a full global atmospheric simulation. It should use established, explainable approximations and deterministic algorithms appropriate to the map's scale and data resolution. The system must explain causal assumptions and distinguish natural-model outputs from supernatural/world-specific modifiers.

LLM responsibilities:
- interpret creator intent and narrative constraints;
- identify relevant causal factors;
- plan analysis and explain results;
- identify contradictions and request clarification.

Geography services responsibilities:
- geometry and terrain calculations;
- hydrology and watershed derivation;
- climate and ecological suitability calculations;
- topology and spatial validation;
- deterministic bounded recalculation.

## 9. Map presentation

The same underlying spatial facts may be presented through multiple coordinated views:

- **Natural Geography View** — elevation, terrain, mountain systems, rivers, lakes, coastlines, climate, and ecology;
- **Fantasy Cartography View** — mountain symbols, forest textures, stylized rivers, political boundaries, settlements, fortresses, and labels.

A fantasy symbol is a representation of an underlying spatial object or feature, not a separate competing geography. Presentation style must not become the authority for geometry or world facts.

## 10. Lifecycle and execution architecture boundary

Guided Initialization is a product workflow built on existing AI Assistant infrastructure, not a new independent task engine.

- AI Assistant remains the single user-facing assistant.
- Task Planning & Execution Engine manages planning, execution, retries, pauses, and recovery.
- Generation Session is used where long-running or multi-stage generation needs persisted lifecycle and candidate isolation.
- Candidate and Validation components manage generated results and quality evidence.
- Selection and Change Set components capture the exact chosen formal changes.
- Core Domain Domain Services remain the only path for formal map application and historical recording.

Do not introduce external Agent/MCP execution as a prerequisite, a second map history, or a generic workflow orchestration platform.

## 11. V1 scope and exclusions

**V1 includes**
- story-led adaptive initialization;
- structured extraction of world knowledge;
- story-to-spatial interpretation;
- world/region/locality/site scopes;
- staged candidate generation and review;
- basic geography, hydrology, climate, and ecological consistency checks;
- bounded impact analysis and recalculation;
- reuse of existing authorization/application lifecycle.

**V1 excludes**
- full Earth-system climate simulation;
- fully autonomous canon authoring;
- automatic global regeneration after every local edit;
- arbitrary user-defined simulation engines;
- a second map database/history authority;
- mandatory approval for every trivial low-risk operation;
- requiring the creator to complete a fixed questionnaire before any map can be produced.

## 12. Acceptance criteria

1. A creator can begin with a short, incomplete story premise.
2. The assistant adaptively asks only material questions and supports unknown/skip/defer.
3. The system distinguishes facts, intent, inference, and pending information.
4. A first world map skeleton can be produced before all details are known.
5. The creator can select one region and enrich it without losing world-scale context.
6. Natural geography and fantasy cartography remain coordinated presentations of one spatial model.
7. Generated results are editable candidates with validation evidence, not flattened images only.
8. Local changes identify relevant downstream impacts without regenerating unrelated regions.
9. Confirmed creator constraints are not silently changed.
10. Formal map changes are applied only through the established Core Domain boundary.

## 13. Open implementation decisions

The product lifecycle and major principles are sufficiently discussed for this working baseline. Remaining work includes:
- exact Guided Initialization state machine and persistence contract;
- question selection/ranking policy and prompt/tool contracts;
- spatial resolution and scale-transition rules;
- dependency graph representation and bounded invalidation strategy;
- geography algorithms and numerical validation thresholds;
- candidate preview UX and map diff presentation;
- detailed authorization risk matrix;
- integration tests across Task, Generation Session, Candidate, Change Set, and Core Domain.

These are subsequent ME-01 and ME-02/ME-05/ME-06 design tasks, not reasons to delay recording this product baseline.