# World View — Guided Initialization & Story-to-World Modeling Architecture V1.0

- Version: 1.0
- Date: 2026-11-21
- Status: Product and logical architecture baseline
- Scope: World View Map Editor Guided Initialization, story understanding, world knowledge structuring, initial world-map formation, and progressive regional enrichment
- Product target: Single-user, local-first C# + Avalonia desktop application
- Related baselines: AI Assistant Product Architecture V1.0; Map Generation Specification V0.2; Map Generation Workflow V0.2; Generation Session Lifecycle V0.2; Core Domain identity/history/persistence contracts

## 1. Executive decision

World View Guided Initialization is a conversation-led world-building workflow. It accepts an incomplete story background or other creator intent, progressively identifies the world's entities and relationships, derives a coherent geographic framework, produces the first editable world map, and then supports iterative enrichment of individual regions.

It is not a GIS questionnaire, a one-click image generator, or a requirement that the creator complete a world bible before starting. The creator may begin with a short story premise, a character, a species, a conflict, a place, a hand-drawn map, or a partial geography. The AI Assistant carries the burden of analysis, domain translation, geographic reasoning, and technical planning.

**Core product rule: the creator supplies narrative intent; World View supplies structured world understanding, geographic reasoning, and editable spatial results.**

## 2. Product outcomes

Guided Initialization must:

1. Accept incomplete natural-language stories and evolving creator notes.
2. Understand both explicit geography and non-geographic story content that may imply spatial facts.
3. Extract characters, species, groups, places, environments, resources, historical events, conflicts, migrations, and relationships.
4. Distinguish story facts, creator intent, AI inference, AI proposals, unknowns, and contradictions.
5. Ask only questions whose answers materially affect world structure, geography, important canon, or high-impact downstream results.
6. Autonomously infer routine or technically derivable details, state assumptions, and keep them revisable.
7. Build a coherent macro world framework before demanding detailed regional information.
8. Generate a first editable world map from the accepted framework.
9. Let creators enrich regions one by one through further conversation and story development.
10. Preserve global consistency when local regions or story facts change.
11. Keep story knowledge and spatial map objects separate but explicitly linked.
12. Produce structured, editable map objects and relationships, never only a flattened picture.

## 3. Unified conversation: story understanding and spatial modeling

The conversation is one continuous creative process. Story understanding and spatial modeling are cooperating capabilities within the same AI Assistant, not separate user-facing modes or agents.

### 3.1 Story understanding

The assistant identifies, where present:

- Characters: protagonist, supporting characters, historical figures, groups of people, and their identities/roles.
- Species and populations: physical traits, habitat preferences, social organization, settlements, mobility, resources, and inter-species relations.
- Groups and powers: nations, alliances, clans, institutions, armies, religious bodies, trade organizations, and their authority boundaries.
- Places: continents, regions, settlements, landmarks, routes, interiors, underground spaces, and places mentioned only in narrative.
- Environment: landforms, climate, water, vegetation, ecosystems, hazards, and resources.
- History: settlement, migration, war, conquest, disasters, political change, construction, abandonment, and time-dependent boundaries.
- Relations: inhabits, owns, controls, borders, trades with, travels through, originated in, migrated from/to, fought, allied with, protects, and other typed relations.

The assistant must not assume every story entity is a map object. A character may be a non-spatial entity; a war may be an event with a geographic footprint; a forest may be both a natural geographic object and a habitat; a place name may refer to a region, settlement, building, or merely a narrative reference. The semantic type must be resolved before map creation.

### 3.2 Spatial modeling

The assistant evaluates which extracted facts have spatial implications and maps them to:

- world/map frame and extent;
- continents, islands, oceans, coastlines and inland waters;
- terrain regions, elevation structures, mountain systems and watersheds;
- climate zones and ecological regions;
- species habitats, historical territories and migration corridors;
- countries, borders, settlements, facilities and transport networks;
- underground networks and vertical/remote spaces;
- local places and structures as regional detail is added.

Spatial inference must preserve provenance. The assistant may propose a plausible location or geometry, but must not rewrite an explicit creator statement as though it were a confirmed fact.

## 4. Canonical knowledge classification and authority

Every extracted or inferred statement must carry a knowledge status and provenance. Minimum classifications:

| Classification | Meaning | Default authority |
|---|---|---|
| Creator-confirmed fact | Explicitly stated or explicitly confirmed by creator | Canonical world knowledge |
| Creator intent | Desired tone, outcome, or direction that may not yet specify a fact | Constraint on proposals; not automatically a fact |
| AI-derived inference | Reasoned consequence of known facts and domain rules | Provisional; explainable and revisable |
| AI proposal | Suggested content not yet accepted | Candidate only |
| Unknown / unresolved | Information not supplied and not reliably derivable | Must remain unknown |
| Conflict | Two or more statements or constraints cannot all hold as currently interpreted | Must be surfaced and resolved or explicitly scoped |
| Superseded | Formerly accepted statement replaced by a later explicit correction | Retained as provenance/history, not active truth |

Each knowledge item should retain at least: stable knowledge identity, subject, predicate/type, value or target, source conversation/message or imported source, author (creator/AI/import), status, confidence where meaningful, valid time/history scope if applicable, affected region/object references, and supersession/conflict links.

Do not equate model confidence with truth. Confidence is diagnostic metadata, not a substitute for creator confirmation or source evidence.

### 4.1 Authority hierarchy

1. Explicit current creator confirmation and protected canon.
2. Existing formal World View world-knowledge and map-domain records.
3. Explicit creator statements not yet promoted to canon.
4. Deterministic domain/geographic derivations from accepted premises.
5. AI proposals and stylistic suggestions.
6. Unresolved assumptions.

A later creator correction may supersede an earlier creator statement, but the assistant must identify consequential downstream effects. AI inference cannot silently override creator-confirmed facts.

### 4.2 Chat is a source, not the canonical store

Conversation is the authoring interface and provenance source. It is not itself the authoritative world model. The assistant must extract and organize durable knowledge into structured records, link them to spatial objects where applicable, and preserve the source trail. Summaries and retrieval indexes are rebuildable aids, not canon authority.

## 5. Guided questioning and autonomous reasoning policy

The assistant should not run a fixed questionnaire. It selects the next question dynamically from current knowledge, map state, creator goal, unresolved conflicts, and the expected impact of an answer.

### 5.1 Ask only when

- the answer changes the macro spatial structure or the world-map frame;
- a creator-only story decision cannot be derived from geography or known canon;
- multiple materially different interpretations remain plausible;
- a contradiction affects protected canon or a high-impact map change;
- a critical physical-world parameter is required for a reliable natural-geography model and no safe default exists;
- the creator must authorize a consequential change.

### 5.2 Infer without asking when

- the detail follows from accepted physical/geographic rules;
- the detail is low-impact, reversible, and does not alter story canon;
- several reasonable implementations produce the same user-visible result;
- a standard default can be stated and changed later;
- the user has delegated technical choices to the assistant.

The assistant should state the important assumptions in ordinary language, not burden the creator with GIS, meteorology, geometry, database, or rendering terminology.

### 5.3 Question design

- Ask one high-value question at a time by default.
- Offer a small set of understandable examples only when they help; always allow free text, uncertainty, skip, correction, or “recommend for me”.
- Do not repeat settled information.
- Do not ask the creator to select implementation technologies or professional geoscience parameters.
- Do not continue questioning after sufficient information exists to create a useful first proposal.

## 6. End-to-end initialization lifecycle

### Stage 0 — Entry and intent framing

Accept any starting point: story premise, prose excerpt, world notes, existing map, sketch, imported geography, or a direct map goal. Determine whether the user wants a new world, a region, a map reconstruction, or an extension to an existing map.

### Stage 1 — Story and world decomposition

Extract knowledge candidates and relations. Identify narrative entities, environment, history, spatial references, and unknowns. Present a concise evolving world brief rather than a giant form. Ask only for macro decisions that cannot be inferred safely.

### Stage 2 — World frame and macro layout

Establish the minimum frame needed for a first map: world/map extent, coordinate/scale assumptions, land-water arrangement, major regions, known anchors, and edge semantics. Map extent is not world boundary; a clipped map edge must not be interpreted as a coastline or end of the world unless explicitly stated.

### Stage 3 — Macro natural-geography planning

Build a causal physical framework in dependency order:
1. world/map spatial frame and scale assumptions;
2. continental/island/ocean masses and coastline structure;
3. broad tectonic/structural setting and major elevation systems;
4. major mountain chains, plateaus, basins and lowlands;
5. drainage divides, watersheds, rivers, lakes and outlets;
6. latitude/seasonal baseline, atmospheric circulation and prevailing winds;
7. temperature, precipitation, climate regions and ocean-current influence;
8. broad biomes and ecological regions.

The assistant may internally iterate between terrain, hydrology, winds and climate because these systems constrain each other. The visible workflow remains understandable and staged.

### Stage 4 — First world-map proposal

Produce a coherent macro map with editable spatial objects and a readable cartographic presentation. Include an explanation of major decisions, assumptions, unresolved issues, and any deviations from creator intent. Validate geography and semantic constraints before presenting it.

### Stage 5 — Human geography and story anchors

Add or propose nations, historical territories, species ranges, major settlements, important landmarks, routes and story-critical places. Do not force all human geography into the first map if the creator has not supplied enough detail. Natural geography remains independent of political borders and story ownership.

### Stage 6 — Creator review and application

Show the map preview and structured changes. The creator can accept, reject, edit, or partially accept independent change groups. Apply only through the existing Candidate → Validation → Change Set/authorization → Domain Services flow. High-impact changes require explicit confirmation; low-risk reversible changes may follow the configured authorization policy.

### Stage 7 — Progressive regional enrichment

The creator chooses a region or continues a story conversation. The assistant reuses all relevant world knowledge, regional geography, history, species and existing map objects. It enriches the selected region from macro to local detail without rebuilding unrelated regions.

### Stage 8 — Continuous consistency maintenance

Any accepted change triggers impact analysis over dependent knowledge and spatial systems. Recompute only affected derived outputs, preserve unaffected confirmed content, explain material impacts, and request a decision only when creator intent or canon is genuinely ambiguous.

## 7. Progressive map detail and dependency levels

The map develops from coarse to fine:

| Level | Scope | Typical content |
|---|---|---|
| World / macro | Whole map | land-water frame, continental masses, major terrain systems, macro climate, major regions |
| Regional | One large region | watersheds, mountain subranges, climate/biome mosaics, regional borders, historical territories |
| Local | Selected locality | towns, villages, roads, fields, forests, wetlands, local landforms and facilities |
| Site / interior | Specific place | castle, building, floors, rooms, caves, dungeons, vertical and interior connections |

The creator may enter at any level, but the assistant must check required parent context. Missing macro context should be inferred where safe or recorded as provisional; it should not force a complete world map before local work can begin.

The agreed default initial-map generation order is:
1. macro geography skeleton;
2. natural environment refinement (climate, vegetation, water bodies, ecological regions);
3. human geography (settlements, borders, facilities, roads and trade routes).

This is a dependency-aware default, not a rigid pipeline. Story-critical anchors may be included earlier as constraints or provisional markers.

## 8. Global consistency and impact analysis

The assistant maintains a dependency graph between knowledge, spatial objects, and derived systems. Examples:

- elevation and mountain geometry → watersheds, river gradients, temperature lapse and orographic precipitation;
- coastline and ocean adjacency → maritime exposure, coastal climate, ports and sea routes;
- prevailing winds + moisture source + mountain barriers → precipitation distribution and rain shadows;
- climate + soil + water availability → biome suitability and agricultural viability;
- habitat preference + biome + water/resources → species range and settlement suitability;
- historical migration/conquest → time-scoped territory and population distribution;
- road/river/terrain constraints → route feasibility and travel estimates.

A local edit should trigger targeted dependency invalidation, not a full-world regeneration. The system should classify downstream impacts as:
- deterministic recomputation;
- AI reassessment/proposal;
- creator decision required;
- no meaningful impact.

Confirmed geometry or canon must be protected unless the creator explicitly authorizes its change.

## 9. Spatial data and presentation are separate

The canonical result is structured spatial data: semantic objects, geometry, attributes, typed relationships, provenance, temporal scope, and constraints. Cartographic styling is a view over that data.

The initial map supports at least two coordinated presentations:
- Natural/physical geography view: relief, elevation, terrain, water, climate and ecology.
- Traditional fantasy atlas view: illustrated mountain/forest symbols, rivers, labels, borders, settlements and legend.

Both views reference the same spatial objects. Styling changes must not mutate terrain or semantic geography. A generated image may be used as optional reference/artwork, but cannot substitute for editable geometry and domain objects.

## 10. Formal integration boundaries

- AI Assistant is one user-facing assistant; internal steps and algorithms remain implementation details.
- LLM outputs structured proposals and explanations, not direct database writes.
- Deterministic algorithms perform geometry, topology, raster, hydrology and validation work wherever feasible.
- Candidate content remains separate from formal map data until validated and authorized.
- Formal changes are applied through World View Domain Services and Core Domain history/Command mechanisms.
- Map objects, world-knowledge facts, historical records and visual styling remain distinct authorities with explicit references.
- No second AI-owned map history, Object identity system, Branch state store, or World State authority is permitted.

## 11. MVP boundary

MVP should prioritize a dependable assisted workflow over a high-fidelity planet simulator.

Include:
- conversational story intake and incremental knowledge extraction;
- explicit/proposed/inferred/unknown/conflict distinctions;
- a small structured world-knowledge graph and source provenance;
- macro world frame and region anchors;
- assisted continent/coastline and broad terrain generation;
- first-pass physically consistent hydrology/climate/biome proposals;
- editable map preview, validation, partial acceptance and safe application;
- region selection and iterative enrichment;
- dependency-aware impact warnings and targeted regeneration.

Defer:
- full plate tectonic simulation over geological time;
- high-resolution numerical weather prediction or global climate circulation simulation;
- ecological population simulation;
- detailed civilization/economic simulation;
- automatic generation of every building/interior in the first initialization pass.

The MVP may use simplified, deterministic, physically informed models. It must expose their resolution and assumptions and must not claim scientific simulation accuracy it does not provide.

## 12. Acceptance criteria

1. A creator can start from a short story premise without preparing a complete world bible.
2. The assistant extracts story entities and relations and records their source and status.
3. The assistant does not turn every mentioned character/event into a map object.
4. Explicit creator facts cannot be silently overwritten by AI inference.
5. The assistant asks only high-impact or genuinely unresolvable questions and does not repeat settled answers.
6. It can produce a coherent macro map before detailed cities and buildings are specified.
7. The first result is editable structured spatial data, not only an image.
8. A creator can enrich one region without forcing regeneration of the entire world.
9. Local edits trigger dependency-aware checks and targeted recomputation.
10. Natural geography, story knowledge, political territories and cartographic styling remain distinct but linked.
11. All formal application uses existing Domain Services and authorization/history boundaries.
12. The workflow remains useful without an external Agent, MCP, Codex or Claude Code.

## 13. Decisions and deferred items

### Accepted baseline
- Conversation is both a world-creation interface and a story-decomposition interface.
- Story understanding and spatial modeling are integrated capabilities in one Guided Initialization flow.
- The creator provides narrative intent; AI handles technical/geographic analysis and routine derivation.
- Knowledge must distinguish confirmed facts, creator intent, inference, proposal, unknown and conflict.
- Initial maps form from macro geography, then natural-environment refinement, then human geography.
- Creators progressively enrich regions after the first world map exists.
- Earth-based natural science is the default basis for climate and physical geography; fictional exceptions must be explicit world rules.
- Map extent and world extent are separate.
- Structured spatial data is authoritative; map artwork is a presentation, not the data model.

### Deferred technical design
- Exact World Knowledge logical entities and persistence mapping to Core Domain.
- Knowledge graph query/index strategy and provenance schema.
- Exact terrain/raster resolution and map-scale policy.
- Algorithm selection and deterministic generation parameterization.
- Formal change-impact dependency representation and invalidation protocol.
- UI wireframes and interaction details for knowledge review and region enrichment.