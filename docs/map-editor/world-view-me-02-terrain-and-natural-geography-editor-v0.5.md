# World View — ME-02 Terrain & Natural Geography Editor

- Version: 0.5
- Date: 2026-11-21
- Status: **Natural Geography Editor Working Baseline** — product/domain workflow baseline reconciled with ME-02-A through ME-02-G; calculation execution, provenance, determinism, invalidation, artifact lifecycle and atomic publication are governed by ME-02-F; creator workflows, interaction states and acceptance are governed by ME-02-G; exact algorithms, raster encoding, numeric parameters, UI controls and implementation libraries remain subject to validation
- Supersedes: ME-02 V0.4 (retained for history)
- Supersedes: ME-02 V0.3 (retained for history)
- Supersedes: ME-02-01 V0.2 (retained for history)
- Supersedes: ME-02-01 V0.1 (retained for history)
- Scope: Terrain, elevation, land/water, landforms, mountain systems, coasts, watersheds, rivers, lakes, climate, soil/water indicators, biomes and ecological suitability authoring
- Parent: ME-01 Map Editor Product and Architecture Working Baseline
- Depends on: ME-01-01 through ME-01-07; ME-02-02 through ME-02-05; Natural Geography, Climate & Hydrology Model V1.0; Map Rendering & Visualization Architecture V0.3; Spatial Presentation and Editing Interaction Contract V0.1; Map Generation Specification/Workflow/Session Lifecycle V0.2; Core Domain P2-09-15 through P2-09-28
- Target: C# + Avalonia desktop application, Windows/macOS, local-first, single-user

## 1. Executive recommendation

Build Terrain & Natural Geography as a **specialist editing mode inside the unified Map Editor**, not as a separate GIS application or an independent map/data model.

The mode should let creators describe, sketch, inspect, refine and validate the physical geography of a world in ordinary creative language. The application translates that intent into structured spatial data, continuous physical fields, derived analyses and reviewable proposals. Deterministic geometry, terrain, drainage and climate algorithms perform calculations; the built-in AI Assistant helps interpret intent, plan work, explain causes, ask clarifying questions and coordinate tools.

The central design decision is to keep four kinds of content distinct:

1. **Semantic geographic Objects** — independently meaningful features such as Landmass, Mountain Range, Mountain Peak, River, Lake and Terrain Region. They have stable Core Domain identities and formal geometry components.
2. **Continuous physical fields and source datasets** — elevation, land/water masks, temperature estimates, precipitation estimates, soil/water indicators and similar spatially sampled data. They are not one semantic Object per cell.
3. **Derived analytical results** — watersheds, drainage networks, climate classifications, biome suitability, slope/aspect and consistency diagnostics. They carry source fingerprints, method/version, parameters, scale and freshness; they are not automatically formal world facts.
4. **Presentation** — hillshade, contour lines, hypsometric tint, river styling, biome colors, labels and fantasy-atlas symbols. Presentation never becomes physical truth.

V1 should use a **physically informed, deterministic, explainable model**, not a full coupled Earth-system simulator. The product must disclose assumptions, resolution and uncertainty rather than invent scientific precision.

## 2. Goals and non-goals

### 2.1 Goals

- Provide a natural-geography workflow understandable to a world creator who does not know GIS.
- Support both story-led creation and direct visual authoring: start from prose, rough sketches, existing map data or a combination.
- Author world-scale and regional-scale land/water structure and progressively refine local terrain.
- Represent elevation and other continuous fields without turning raster cells into semantic Objects.
- Create and edit semantic geographic Objects while preserving identity and geometry/component boundaries.
- Derive drainage, river networks, climate indicators and ecological suitability from causal inputs.
- Explain why a result is plausible, uncertain, unusual or inconsistent.
- Preserve creator-confirmed facts and explicit supernatural/engineered exceptions.
- Show cross-scale impact and perform bounded incremental recomputation.
- Support Natural Geography and Fantasy Cartography presentations over the same underlying spatial data.
- Work without AI/network access for manual editing, local calculations and inspection.

### 2.2 Non-goals for V1

- A professional GIS desktop replacement, geodatabase administration tool or geospatial ETL suite.
- Full global atmospheric/ocean general circulation simulation or daily weather prediction.
- Full geological-time plate tectonic, erosion, glaciation or volcanism simulation.
- Exact discharge, groundwater, flood forecasting or soil chemistry at arbitrary precision.
- An unrestricted raster algebra or user-programmable scientific workflow engine.
- A general-purpose plugin framework for adding arbitrary physical models.
- A universal ontology of every landform, ecosystem, species or hydrological feature.
- Automatic conversion of every derived feature into a formal Object.
- Automatic changes to confirmed creator facts to make a model look scientifically tidy.
- Mandatory AI involvement in every geometry operation.

## 3. Product experience and user workflows

### 3.1 Canvas-centered workspace

The natural-geography mode uses the shared ME-01 desktop editor shell:

- **Center:** map Canvas, with active physical-data view and direct editing/selection.
- **Right contextual panel:** selected feature/field inspector, source and provenance, properties, validation findings and relevant layer controls.
- **Bottom AI conversation:** natural-language request, explanation, Guided Initialization questions and task feedback.
- **Shared navigation:** World / Region / Locality / Site working scopes, map navigation, layers, timeline and common selection model.

The mode changes available tools, contextual inspector sections and default layer presets. It does not create a separate selection model, map identity, object registry or history system.

### 3.2 Two equally supported starting paths

**Path A — Creator describes a world**

1. Creator describes the world, story, landmass arrangement or desired natural environment.
2. AI extracts confirmed facts, preferences, creative freedom, exclusions, protected facts and uncertainties.
3. Guided Initialization asks only questions that materially affect geography or the next calculation.
4. The system proposes a coarse world frame, land/water structure and physical assumptions.
5. The creator reviews the proposal on the Canvas and edits it directly or through conversation.
6. The system derives first-pass terrain, drainage and climate indicators where inputs support them.
7. Results are progressively refined by region.

**Path B — Creator draws first**

1. Creator sketches landmasses, coastlines, mountain systems, lakes or rivers.
2. The editor preserves the sketch as authored geometry or a clearly marked provisional input.
3. The assistant identifies missing physical context and asks targeted questions.
4. The system proposes elevation interpretation, drainage and climate analysis.
5. The creator reviews and adopts or revises results.
6. Derived results are updated without replacing protected source geometry.

The two paths can be mixed at any time. Neither is a separate product workflow or data model.

### 3.3 Natural-language task examples

The creator should be able to ask:

- “Make this continent broader in the north and leave a large inland basin in the center.”
- “I want a high mountain chain along the western coast. What would that do to rainfall?”
- “This river should begin in the mountains and reach the inland lake.”
- “The northern region should be cold and mostly coniferous forest, but the southern coast should be warm and wet.”
- “Show me where a rain shadow is likely.”
- “Increase detail around this valley without changing the shape of the surrounding mountain system.”
- “Keep the confirmed coastline and city locations unchanged; adjust the terrain between them.”

The AI must translate these into explicit scope, constraints, candidate operations and deterministic calculations. It must not pretend a prose request alone contains enough information to calculate exact elevations or rainfall.

### 3.4 Progressive authoring, not a rigid wizard

Natural geography supports progressive maturity:

1. World frame and planetary assumptions.
2. Land/water mask and major landmasses.
3. Coarse elevation and major terrain structure.
4. Mountain systems, plateaus, basins and major valleys.
5. Drainage basins, lakes and river networks.
6. Broad atmospheric circulation and ocean influence.
7. Temperature and precipitation estimates.
8. Climate classification and biome suitability.
9. Regional/local refinement and diagnostics.

This is a dependency-aware default sequence, not a mandatory wizard. Users may jump to an independent task, and the system should explain when missing upstream inputs limit confidence.

## 4. Domain content model

### 4.1 Semantic geographic Objects

The editor uses Core Domain's typed Object and Geometry contracts. The current recommended V1 seed set relevant to this mode includes:

| Object Type | Formal meaning | Geometry roles | V1 editor behavior |
|---|---|---|---|
| Landmass | Independently meaningful continuous land body or authored archipelago unit | `extent`: Area, 1..N | Create/edit land extent; multipart identity follows descriptor semantics |
| Mountain Range | Named/meaningful mountain system | `extent`: Area 0..N; `ridge`: Line/Curve 0..N; at least one overall | Draw/adjust range footprint or ridge; preserve one semantic identity across working scopes |
| Mountain Peak | Independently meaningful summit | `location`: Point exactly 1 | Place peak and author typed elevation with unit and vertical reference |
| River | Independently meaningful river course/network entity | `centerline`: Line/Curve 1..N; optional reach components | Draw/refine centerline; flow direction is a separate explicit hydrology relation |
| Lake | Inland water body | `shoreline`: Area 1..N | Create/edit shoreline; water-level temporal model is deferred |
| Terrain Region | Semantically useful classified terrain area | `extent`: Area 1..N | Author or classify bounded terrain regions; classification schema remains controlled |
| Point of Interest | Generic location only when no specialist type is available | `location`: Point exactly 1 | Not a fallback for all natural features; use sparingly |

The current Object Type seed/field catalog is a recommended bounded baseline, not a frozen ontology. ME-02 must define any specialist additions with semantic identity, typed fields, geometry roles, validation, relationship participation and compatibility rules before enabling them.

### 4.2 Concepts that are not automatically formal Objects in V1

| Concept | V1 representation | Reason |
|---|---|---|
| Coastline | Derived boundary of land/water geometry; may be authored indirectly through Landmass extent | Avoid duplicating the same boundary as an independent competing Object |
| Watershed / drainage basin | Derived analytical polygon/region with provenance; formal Object only after a dedicated type is designed | Current V1 seed catalog does not include Watershed |
| Elevation | Continuous field / raster-like Asset plus optional typed peak elevation attributes | Elevation varies continuously; not one Object per cell |
| Slope / aspect / hillshade | Derived fields or renderer overlays | Rebuildable analysis/presentation |
| Climate zone | Derived classification layer; optionally creator-authored classification intent as a constraint | Must remain traceable to physical inputs and classification scheme |
| Biome / ecological suitability | Derived categorical/probability layer | Suitability is not actual species distribution or a confirmed habitat fact |
| Soil / infiltration / water balance | Continuous or categorical fields with source/provenance | Do not create one Object per cell |
| Glacier, wetland, volcano, plateau, canyon, desert, forest | Initially typed derived/authoring feature layer or explicit candidate; promote to formal Object only when independent semantic identity and V1 Object Type contract are justified | Avoid premature Object Type proliferation |
| Ocean current / prevailing wind | Derived or authored physical vector fields/assumptions | Physical drivers, not decorative line symbols |
| Ocean / sea | Land/water mask classification and physical field context in initial V1 | A formal Ocean Object Type is not in current seed set |

This table does not prohibit future specialist Objects. It prevents the UI from silently assigning formal identity to a concept whose Core Domain semantics have not been designed.

### 4.3 Four categories of content state

Every natural-geography content item must be identifiable as one of:

- **Creator-authored input:** explicit geometry, value, assumption, classification or constraint supplied by the creator.
- **Formal Core Domain fact:** an adopted Object, geometry component, typed attribute or relationship.
- **Derived result:** reproducible algorithm output from pinned inputs and method/version.
- **AI Candidate / interpretation:** proposed feature, geometry, parameter, relation or explanation not yet formally applied.

These categories must remain distinguishable in the Inspector, overlays and validation reports. “Visible on the map” must not imply “formal fact”.

### 4.4 Physical field model

A physical field is a spatially sampled or continuous value domain, potentially stored in tiles or immutable Assets. Initial field families:

- elevation;
- land/water/unknown mask;
- temperature estimate (annual/seasonal);
- precipitation estimate (annual/seasonal);
- moisture / aridity indicators;
- soil / infiltration classes;
- snow/ice indicators where modeled;
- wind direction/strength class where supported;
- derived slope, aspect and shaded-relief inputs.

Field metadata must include:
- stable field identity within its owning dataset/task context;
- spatial reference identity and axis/unit/dimension metadata;
- geographic coverage and data resolution;
- value units, no-data/unknown semantics and categorical/continuous type;
- source Asset(s) and content hash;
- derivation method/version and parameters, if derived;
- generation time and source-state fingerprint;
- uncertainty/quality metadata where available.

The exact tile layout, raster encoding and field persistence owner remain technical design gates. Do not store a huge raster in a Command JSON payload. Use immutable Asset conventions and derived cache rules.

## 5. Spatial and scientific model

### 5.1 World frame and map edges

Before physical calculations, the system needs a declared Map Space and coordinate reference. The creator should see friendly concepts such as “world size”, “north/south extent”, “map wraps around”, and “this edge is a boundary”, rather than CRS terminology.

Required distinctions:
- geographic/world frame and coordinate reference;
- Map Extent;
- Working Scope;
- Canvas View;
- Export Area;
- map-edge semantics (closed boundary, wrap/seam, unknown/outside coverage).

The edge of a Canvas or exported image is never automatically a coastline. A map boundary may cut through land or ocean; the system must not infer a coast from missing data.

For planet-like maps, wrap/seam behavior must be explicit. A flat fictional local map may use a Local Cartesian reference. Do not require EPSG or WGS 84.

### 5.2 Elevation and terrain

Elevation is the base continuous field for much of terrain analysis. It is not a collection of per-cell semantic Objects.

V1 authoring should support:
- broad elevation massing and regional gradients;
- creator-defined highlands, lowlands, plateaus and basins as constraints or derived classifications;
- ridge/valley structure and major escarpments where resolution supports them;
- contour/hillshade visualization as derived presentation;
- elevation sampling/inspection with units and vertical reference;
- local refinement with boundary conditions from parent/neighbor regions.

The system should not promise detailed realistic erosion, tectonics or exact elevation from a vague prose description. If the creator draws a mountain range without height data, it can be stored as a semantic range candidate/formal object with approximate geometry while elevation remains absent/provisional.

### 5.3 Land/water and coastline

Land/water structure is a foundational physical input.

- Landmass geometry and/or an authored land/water mask define the broad spatial division.
- Coastline is derived from the land/water boundary, subject to the selected resolution and map-edge rules.
- Island and archipelago identity follows semantic Object intent, not raster connected-component count alone.
- A single Landmass Object may have multipart geometry where descriptor semantics allow it.
- The system must not create a new Landmass Object merely because a raster operation splits a connected region.
- Lake shoreline is the formal geometry role of a Lake Object.
- Inlets, estuaries, deltas, tidal zones and ocean outlets require explicit future endpoint/feature semantics; do not fake them by creating unsupported relationship types.

### 5.4 Mountain systems and geomorphology

Mountain ranges, peaks and elevation fields have related but distinct meanings:

- Mountain Range is a semantic geographic Object with area/ridge geometry.
- Mountain Peak is an independent Object only when the summit has creator/domain significance; local elevation maxima are not automatically named peaks.
- A peak belongs to a range only through an explicit, valid `structural.part_of` relationship; proximity alone is insufficient.
- A plateau is a broad elevated surface, not a synonym for a mountain range.
- A basin may be internally drained or externally drained.
- A cliff/escarpment requires a supported elevation gradient and continuity.
- Ridge orientation and continuity can affect drainage divides, wind exposure and travel corridors.

V1 may use deterministic terrain classification and diagnostics, but must not claim geological history was simulated. Tectonics, volcanism, glaciation and erosion may be explicit creator-authored explanations or future model inputs.

### 5.5 Hydrology and drainage

Rivers must be derived and validated against terrain, watersheds and water balance; they must not be routed merely to connect settlements.

The recommended hydrology pipeline is:

1. inspect elevation coverage and precision;
2. identify drainage basins and divides;
3. detect sinks/depressions;
4. classify depressions as closed basin, lake/wetland candidate, endorheic system or unresolved data issue;
5. derive flow direction and accumulation;
6. identify headwater areas and tributary confluences;
7. derive candidate channels and outlets;
8. compare water supply, seasonality and basin properties;
9. generate flow-network validation diagnostics;
10. present candidates for review and formal adoption where supported.

Hydrological network rules:
- River geometry digitization order is not authoritative flow direction.
- Formal `hydrology.flows_to` is directed from upstream/source to downstream/receiver.
- Current V1 endpoint matrix is River→River and River→Lake. Ocean/outlet/estuary endpoints remain deferred.
- Flow continuity, cycle/distributary policy and endpoint constraints must follow the Core Domain Relationship Type descriptor and ME-02 specialist validation.
- A river may cross Working Scope and political/terrain-region boundaries.
- Do not force drainage into a tree if a supported distributary/delta case requires a richer model; where V1 semantics cannot represent it, flag the limitation instead of inventing relationships.
- River flow class is not exact discharge. Relative classes are acceptable only with a declared method and resolution.
- Springs, groundwater and engineered canals are not silently inferred as facts.

### 5.6 Climate and atmospheric circulation

Use Earth science as the governing theoretical basis, with Earth-like defaults only when the creator has not specified alternatives.

The V1 model should account for:
- latitude, solar radiation, season and axial tilt assumptions;
- simplified atmospheric circulation and prevailing winds;
- land/sea thermal contrast and continentality;
- broad ocean-current influence where supported;
- elevation-dependent temperature;
- moisture sources, transport and convergence;
- orographic uplift and rain-shadow effects;
- evaporation, condensation, precipitation and seasonal water balance.

Temperature and precipitation must not be arbitrary independent color layers. They should be derived from declared causal inputs and assumptions.

A simplified model must not claim to be a weather forecast or high-resolution coupled climate simulator. Outputs should be labelled as physically informed estimates/classifications at the selected scale, with explicit uncertainty and missing inputs.

Climate classification (for example, a Köppen-style scheme) is a derived classification over underlying variables. The classification scheme and thresholds remain a ME-02/Map Engine decision gate; store underlying variables so a classification can be changed without regenerating terrain.

### 5.7 Biomes and ecology

Biome suitability is derived from climate, water availability, soil, elevation and ecological context. It is not identical to climate classification.

V1 should:
- derive broad biome suitability overlays where inputs support them;
- distinguish suitability from actual species/population distribution;
- preserve creator-authored species adaptation or magical ecology as explicit World Knowledge constraints;
- report when a desired biome conflicts with physical inputs;
- never force climate or terrain to become implausible simply because a story species prefers a certain habitat.

A species preference is not proof that the species actually lives in that region. Actual territory/population remains a separate world-knowledge and future spatialized-domain decision.

## 6. Editing tools and interaction model

All tools use the shared ME-01 Interaction Framework and Map Engine contracts. This section describes specialist capabilities, not a new generic tool framework.

### 6.1 Shared tool groups

| Tool group | Main user intent | Main behavior |
|---|---|---|
| Land / Water | Shape broad land and water distribution | Sketch/edit landmass extents or land/water mask candidates; preview coastline consequences |
| Elevation | Sculpt broad terrain form | Raise/lower/smooth/interpolate selected field regions with explicit strength and scope |
| Terrain Features | Mark ranges, peaks, basins, valleys and terrain regions | Create/edit supported semantic Objects or provisional classifications |
| Hydrology | Analyze or refine lakes and rivers | Derive drainage, inspect flow, edit river geometry and explicitly establish flow relationships |
| Climate | Inspect and guide physical climate drivers | View seasonal temperature/precipitation/wind/moisture estimates; author constraints/assumptions |
| Ecology | Inspect derived biome suitability | View suitability and explain conflicts; do not treat suitability as actual population |
| Diagnostics | Find and understand problems | Highlight affected layers/features, explain cause, severity, uncertainty and possible repairs |

### 6.2 Land/water editing

Support:
- freehand sketch and point/curve-based geometry;
- polygon creation and vertex/control-point editing;
- erase/cut/reshape as previewed geometry intents;
- coastline smoothing/generalization as derived or explicitly adopted geometry refinement;
- import of supported vector/raster sources through controlled import workflows.

Do not expose geometry split/merge as formal V1 operations unless Core Domain has adopted the relevant descriptors. Editing one Landmass component must preserve semantic Object identity and component identity rules.

### 6.3 Elevation editing

Recommended V1 field operations:
- raise/lower a bounded area;
- smooth or soften a local gradient;
- create a broad ridge/valley profile;
- interpolate a transition between authored control regions;
- apply a bounded procedural brush with deterministic seed and explicit parameters;
- inspect elevation profile along a drawn line;
- undo/cancel preview before publishing a new immutable field Asset.

Each operation must state:
- target field and source fingerprint;
- spatial scope and boundary context;
- units and value range;
- algorithm/version and parameters;
- whether the operation is manual-authored, deterministic-derived or AI-proposed;
- affected dependent analyses.

Brush movement is transient preview. A completed operation publishes a new immutable field Asset only through the appropriate application/persistence boundary; it does not create a formal Command per pointer sample.

### 6.4 Terrain feature authoring

Creator may:
- draw a Mountain Range footprint or ridge;
- place a Mountain Peak and provide elevation with unit/vertical reference;
- create or edit a Terrain Region classification;
- inspect candidate plateaus, basins, valleys and escarpments;
- explicitly associate a peak with a mountain range using the supported relationship service.

Derived landform detection may propose candidates but cannot automatically assign formal Object identity or relationships.

### 6.5 Hydrology editing

Creator may:
- sketch or refine a River centerline;
- draw or adjust a Lake shoreline;
- request drainage basin/flow analysis;
- inspect upstream/downstream context and likely confluences;
- explicitly accept a supported River→River or River→Lake flow relationship;
- mark a segment or area as protected from automatic modification.

The editor must display flow direction as a derived/relationship overlay, not infer it from line digitization direction. If a proposed network cannot be represented by current Relationship Type rules, it remains a diagnostic/candidate rather than a partially fabricated formal graph.

### 6.6 Climate and ecology interaction

The creator should work primarily through explanatory controls and natural language, not expert climate-model parameters.

Examples:
- “Show summer and winter rainfall separately.”
- “Why is this region dry?”
- “What changes if the prevailing wind comes from the west?”
- “Compare the current climate estimate with the confirmed cold northern setting.”
- “Show areas suitable for temperate forest, without changing the formal world yet.”

Advanced numeric inputs may exist in an inspector for expert users, but they are not required for normal creation. Every manual override must be explicit, sourced and distinguishable from derived estimates.

## 7. Calculation and dependency architecture

### 7.1 Responsibility split

| Component | Responsibility | Must not do |
|---|---|---|
| Core Domain | Formal Object identity, typed geometry components, attributes, relationships, Initial State/History semantics and Asset references | Own climate algorithms or renderer caches |
| Geometry / Spatial Services | Coordinate transforms, geometry validity, spatial predicates, raster/vector spatial operations, terrain/hydrology primitives | Become a competing persistence authority |
| Natural Geography Calculation Services | Deterministic elevation, drainage, climate-indicator and biome-suitability calculations | Claim unsupported physical precision or write Core Domain tables directly |
| Map Engine / Presentation | Render physical fields, vector Objects, hillshade, contours, overlays and scene snapshots | Mutate formal data or calculate domain truth during painting |
| AI Assistant | Understand user intent, ask questions, plan tasks, choose appropriate tools, explain outputs, stage Candidates and coordinate review | Invent numeric simulation outputs or bypass validation |
| Editor | Canvas interaction, field/feature selection, transient previews, inspector and layer visibility | Own formal Object identity or field authority |

All modules remain within the local desktop application. Do not split them into services/processes for V1.

### 7.2 Deterministic calculation pipeline

A calculation request should identify:
- Map and applicable Map/Branch/Historical Time context where relevant;
- target Working Scope and any required context buffer;
- input Object/Geometry/Asset identities and content fingerprints;
- coordinate reference and map-edge semantics;
- source field resolution and coverage;
- algorithm identifier/version;
- explicit parameters, units and random seed where applicable;
- hard constraints, protected inputs and creator-authored exceptions;
- expected output field/geometry families and acceptance checks.

A calculation result should identify:
- output candidate/derived Asset references;
- output coverage and resolution;
- source fingerprints;
- algorithm/version/parameters;
- assumptions and uncertainty;
- validation diagnostics;
- stale/fresh status;
- whether the result is preview-only, derived cache, or eligible for formal adoption.

The same pinned inputs and deterministic algorithm version must produce reproducible outputs within the declared numerical/serialization contract.

### 7.3 Dependency order

Recommended dependency graph:

```text
World Frame + Planet Assumptions
              |
Land / Water Mask + Elevation
              |
       +------+------+
       |             |
Drainage /       Latitude / Season
Watersheds             |
       |        Atmospheric Circulation
       |             |
       |       Wind + Ocean Influence
       |             |
       |       Moisture / Orographic
       |        Precipitation
       |             |
       +-------> Temperature / Water Balance
                         |
                Climate Classification
                         |
                Soil / Biome Suitability
                         |
               Ecological / Settlement
                    Suitability
```

This is a logical dependency structure, not a one-pass physical model. Bounded feedback may be needed (for example snow/ice, lakes or vegetation effects), but V1 should use explicit limited iteration with termination criteria rather than an unrestricted solver.

### 7.4 Bounded incremental recomputation

| Changed input | Re-evaluate dependent outputs |
|---|---|
| World frame / latitude / season assumptions | Solar baseline, seasonal temperature, circulation, climate classification, biome suitability |
| Land/water extent or coastline | Maritime influence, moisture sources, broad ocean influence, coastal climate/ecology |
| Elevation | Slope/aspect, temperature lapse, wind exposure, orographic precipitation, drainage, watersheds, biome suitability |
| Mountain ridge/extent | Relevant divides, windward/leeward precipitation, drainage and local terrain context |
| River/Lake geometry or flow relation | Network continuity, downstream context, affected basin/water diagnostics and dependent local overlays |
| Prevailing wind | Moisture transport, orographic rainfall, aridity, climate classification and biome suitability |
| Soil/infiltration field | Runoff, water balance, wetlands/groundwater indicators and vegetation suitability |
| Cartographic style only | No physical calculation invalidation |

Only affected outputs within the dependency boundary should be recomputed. Regional refinement must use neighboring/parent context and must not treat the Canvas viewport as the entire world.

### 7.5 Freshness and stale results

Derived outputs are fresh only when all declared source fingerprints and algorithm/parameter versions match. If an input changes during calculation:
- discard or mark stale the affected result;
- preserve useful task context;
- re-evaluate only invalidated steps;
- never overwrite newer formal data with an old candidate;
- show which inputs changed and why a preview is no longer applicable.

## 8. Validation and diagnostics

### 8.1 Validation layers

1. **Input / structural:** missing fields, unsupported geometry family, invalid units, malformed Asset, unknown CRS.
2. **Geometry:** invalid rings, self-intersections where prohibited, invalid curves, unsupported precision or out-of-domain coordinates.
3. **Terrain:** implausible gradients, discontinuities, unsupported cliffs, suspicious sinks or resolution limitations.
4. **Hydrology:** uphill flow, broken channel continuity, unexplained inland termination, invalid confluence, unsupported cycle/distributary, implausible source/outlet.
5. **Climate:** rain shadow without moisture-bearing wind/barrier context, unexplained aridity, inconsistent seasonal temperature/precipitation assumptions.
6. **Ecology:** biome suitability conflicts with climate/water/soil inputs, species preference misrepresented as actual distribution.
7. **Global/cross-scale consistency:** boundary discontinuities, inconsistent neighboring regions, impacts beyond the selected scope, stale derived outputs.
8. **Creator intent / canon:** conflict with confirmed statements, protected objects, explicit exclusions or approved supernatural/engineered exceptions.

### 8.2 Severity

- **Blocking error:** invalid geometry/asset, impossible or unsupported formal operation, broken hard constraint, invalid flow relation under enabled descriptor, stale base state that makes application unsafe.
- **Warning:** physically unusual but possible, weakly supported inference, likely inconsistency, low-resolution estimate or unresolved source ambiguity.
- **Information:** assumptions, model version, coverage, scale, provenance, expected uncertainty and derived dependencies.

A physical anomaly is not automatically an error if the creator has explicitly established a supernatural or engineered cause. The system should record the exception and show which natural assumptions it overrides.

### 8.3 Causal diagnostic presentation

A useful diagnostic explains:
- what was detected;
- where it occurs;
- which source fields/Objects are involved;
- the likely causal reason;
- confidence and resolution limitations;
- whether it conflicts with confirmed creator intent;
- possible repair options and their downstream effects.

Example: “The western slope receives moisture-bearing prevailing winds, but the current precipitation estimate is very low. The ridge and wind assumptions imply orographic rainfall here. Review the wind direction, moisture source or explicit world exception.”

The system must not silently repair the map while presenting a diagnostic.

## 9. AI Assistant and Guided Initialization

### 9.1 AI responsibilities

AI Assistant should:
- interpret ordinary-language geography descriptions;
- extract confirmed facts, creator intent, preferences, exclusions and creative freedom;
- identify unknowns and contradictions;
- choose which deterministic analysis is needed;
- ask adaptive questions only when answers materially affect the result;
- explain physical consequences in accessible language;
- produce structured Candidate/Change Set only when task complexity/risk warrants it;
- preserve provenance and uncertainty;
- propose alternatives when constraints conflict.

AI must not:
- invent exact elevation, rainfall, temperature, river discharge or scientific simulation output;
- use story text as proof of formal geometry;
- change confirmed world facts to satisfy a model;
- create formal Objects or relationships directly;
- imply a Candidate is applied before Core Domain confirms the transaction.

### 9.2 Guided Initialization behavior

When a request constitutes initial world/natural geography creation, start or continue the formal Guided Initialization conversation. It must not be replaced by a static GIS form or one-click generation.

The assistant adapts questions to the existing Map and confirmed world knowledge. It allows:
- “I don't know” / uncertain;
- skip or defer;
- add detail;
- correct earlier answer;
- reject an inference;
- change an earlier preference or intent;
- request alternatives.

Do not ask for GIS parameters when a natural-language answer is sufficient. For example, ask “Should the world wrap around like a globe, or is this a bounded region?” rather than requiring a CRS identifier.

After sufficient clarification, generate an editable plan and spatial preview. Confirmed facts remain protected. High-impact changes require the appropriate user decision; ordinary low-risk operations may execute under explicit authorization policy.

### 9.3 Provenance and certainty

Each important input/result must preserve applicable source categories:
- creator statement / creator intent;
- existing formal map;
- project/world setting;
- source material;
- AI inference;
- deterministic system-derived result;
- AI-created content within explicitly authorized creative freedom.

Do not collapse these into a single confidence score. Source type, confirmation status, uncertainty and method provenance are independent dimensions.

## 10. Core Domain application and persistence boundaries

### 10.1 Formal Object and geometry operations

- Core Domain owns stable Object identity, Object Type, Geometry Component identity, immutable geometry Asset references, formal relationships and history.
- Object Type descriptors define geometry family, role, cardinality, attributes, allowed commands and validation.
- Geometry edits preserve semantic Object identity and Geometry Component identity where the descriptor says the component is edited in place.
- Formal geometry changes publish immutable Assets; they do not mutate Asset bytes in place.
- Screen pixels, brush samples, temporary mesh, simplified paths and derived tiles are never formal geometry payloads.
- The editor cannot allocate formal Object IDs or create an independent Object registry.

### 10.2 Initial State versus historical changes

Natural geography is usually part of a Map's initial physical baseline, but not every edit is necessarily a time-zero baseline edit.

The application must route authoring explicitly:
1. **Map Initial State:** initial landmass, terrain, river and lake configuration for the Map's time-zero baseline, through dedicated baseline-authoring services.
2. **Map/Branch History:** later changes that genuinely occur at a historical time, through registered typed Commands in the selected History.
3. **Project Shared Object definition:** only when a geographic Object is intentionally Project-shared; edits affect its current definition across Maps according to Core Domain rules.

Do not use a generic Save/Apply operation that hides the target semantics. Do not write a time-zero fact as a post-zero Command.

### 10.3 Current V1 operation constraints

- Use only Core Domain registered typed operation descriptors.
- Current catalog includes bounded geometry component adjustment/refinement and relevant Object/relationship operations subject to their descriptor preconditions.
- Geometry component split/merge must remain unavailable as formal write until its exact descriptor, identity behavior and topology semantics are adopted.
- River flow relations use the enabled `hydrology.flows_to` descriptor and current endpoint matrix; do not create unsupported ocean outlet/estuary relationships.
- A geometry intersection, snapped endpoint, shared boundary or raster adjacency does not automatically create a formal relationship.
- A new formal Object Type or attribute requires specialist Core Domain descriptor design; the editor must not invent schema at runtime.

### 10.4 Field and derived-result persistence

- Formal source datasets and adopted physical fields use immutable Core Asset references and the approved Asset Store conventions.
- Candidate output remains AI Task staging data until accepted/applied under the relevant workflow.
- Derived field caches are reproducible and disposable, with source hash, method/version and parameters.
- Presentation caches and renderer tiles are not formal data.
- The precise persistence owner for authored physical fields, field revisions and derived raster tiles must be resolved in a dedicated ME-05/ME-07 contract before physical schema implementation. Do not add a new raster database authority as a shortcut.

## 11. Import, external data and source evidence

V1 should support a bounded import pathway only after coordinate/format validation:
- import supported vector geometry into Candidate or explicit authoring input;
- import supported elevation/land-water raster or grid data as immutable source Assets;
- preserve original file, source metadata, license/source attribution where applicable, coordinate reference, units, resolution, coverage and transformation history;
- preview reprojection/resampling and data loss before adoption;
- never silently reinterpret unknown CRS or vertical datum;
- reject or quarantine invalid data rather than guessing.

External datasets do not automatically become confirmed world facts. Imported content retains source provenance and requires an explicit adoption path. Exact supported formats and import libraries remain technical evaluation gates.

## 12. Failure, recovery and safety

- Manual editing remains available if AI/provider/network is unavailable.
- Calculation failure must not corrupt formal map data or overwrite a prior valid field.
- Long-running calculations should expose stage, affected scope, inputs and cancellation boundary.
- Cancellation stops unfinished calculations and discards or preserves candidates as appropriate; it does not reverse already committed formal changes.
- A failed regional calculation should preserve unrelated regions and source inputs.
- Stale candidates must be revalidated against current Map/Branch/time and source fingerprints before application.
- A partial result must be labelled partial; no success claim for uncomputed coverage.
- Repeated deterministic calculations with the same pinned inputs must be reproducible within declared numerical tolerances.

## 13. V1 capability boundary

### Include in V1 design and implementation planning

- Semantic Objects from the current seed set: Landmass, Mountain Range, Mountain Peak, River, Lake, Terrain Region.
- Vector geometry authoring and refinement using the approved geometry component contract.
- Elevation and land/water fields with explicit CRS/units/coverage/resolution.
- Basic terrain visualization: hillshade, contours, elevation tint and profile inspection.
- Deterministic drainage/watershed analysis and river-flow validation at declared resolution.
- Simplified physically informed climate indicators: seasonal temperature/precipitation, broad wind/moisture and rain-shadow analysis.
- Derived climate classification and biome suitability overlays with provenance.
- Natural-language AI orchestration and Guided Initialization.
- Cross-scale validation, bounded recomputation and clear diagnostics.
- Natural Geography and Fantasy Cartography presentation profiles.

### Defer or gate

- Formal Watershed Object Type and `water.drains_to` relationship.
- Formal Ocean/Sea, Glacier, Wetland, Volcano, Plateau, Canyon, Forest, SoilRegion and Biome Object Types unless specialist review justifies them.
- Ocean outlet/estuary formal flow endpoints.
- Detailed groundwater, exact discharge, flood simulation and water-level histories.
- Full erosion/tectonic/glacial simulation.
- Full climate GCM/weather forecasting.
- 3D terrain editing and volumetric subsurface modeling.
- Advanced raster algebra, user scripting and arbitrary model plugins.
- Exact library, raster tile scheme, renderer backend and precision-profile numeric values pending ME-07 validation.

## 14. Recommended implementation/design sequence

1. **ME-02-A Domain inventory:** map all proposed natural features to existing Object Types, physical fields, derived layers or World Knowledge; identify only essential descriptor gaps.
2. **ME-02-B Land/water and elevation authoring:** define world frame, map-edge semantics, elevation field, land/water input and editing workflows.
3. **ME-02-C Terrain feature authoring:** define Mountain Range/Peak/Terrain Region workflows, identity and geometry-role validation.
4. **ME-02-D Hydrology:** define drainage analysis, depression policy, river candidate generation, flow network validation and enabled relationship semantics.
5. **ME-02-E Climate and ecology:** define causal input/output model, assumptions, seasonal summaries, classification and suitability overlays.
6. **ME-02-F Calculation contracts:** define deterministic input/output contracts, provenance, freshness, bounded invalidation and validation severity.
7. **ME-02-G UX scenarios and acceptance:** define end-to-end creator workflows, inspector patterns, diagnostics, Candidate review and accessibility.
8. **ME-02-H Cross-architecture review:** reconcile with current Core Domain descriptors, Map Engine, Map Generation and ME-01; update gaps and follow-up owners.

This sequence is a design work plan. It does not claim that algorithms, raster storage or renderer integration are implemented.

## 15. Acceptance criteria

A V1 natural-geography editor design is acceptable when:

1. Creator can begin from story/prose or a rough drawing and progressively produce a coherent natural-geography baseline.
2. User workflows do not require GIS/CRS/geology/climate expertise; technical assumptions are translated into understandable language.
3. Semantic Objects, continuous physical fields, derived analyses, Candidates and presentation remain separate.
4. Formal Object Types and geometry roles match Core Domain descriptors; unsupported concepts are not silently persisted as formal Objects.
5. Coastline does not arise merely from map/canvas edges; land/water and edge semantics are explicit.
6. Elevation and physical fields preserve spatial reference, units, resolution, coverage and source provenance.
7. River networks derive from terrain/drainage and do not use settlement placement as the primary physical basis.
8. Flow direction is independent of line digitization direction and uses only enabled typed relationships.
9. Climate estimates expose causal inputs, assumptions, scale and uncertainty; no unsupported numerical precision is claimed.
10. Biome suitability remains distinct from actual species distribution.
11. Confirmed creator intent and explicit exceptions are never silently overwritten by derived calculations.
12. Local changes trigger bounded dependent recalculation and preserve unrelated regions.
13. Stale results cannot overwrite newer formal map state.
14. AI Candidate acceptance and formal application follow Core Domain authorization and transaction contracts.
15. Manual editing and inspection remain functional without AI/provider access.
16. Natural Geography and Fantasy Cartography presentations share the same underlying formal spatial data.
17. No renderer, geometry library, raster format or physical schema is falsely labelled frozen without its validation gate.

## 16. Open decisions and explicit gates

| ID | Question | Recommended owner | Gate |
|---|---|---|---|
| ME02-G01 | Exact elevation/field tile storage and immutable Asset layout | Map Engine + Core Domain persistence | ME-05/ME-07 |
| ME02-G02 | Numeric coordinate precision profiles and vertical reference catalog | Spatial/Geometry Services | ME-05/ME-07; P2-09-25 validation |
| ME02-G03 | Land/water mask encoding, wrap/seam and map-edge semantics | Map Space + Map Engine | ME-02-B |
| ME02-G04 | Terrain brush/interpolation algorithms and deterministic parameter set | Natural Geography Calculation Services | ME-02-B |
| ME02-G05 | Depression filling/breaching and endorheic basin policy | Hydrology specialist | ME-02-D |
| ME02-G06 | River network cycle/distributary policy under `hydrology.flows_to` | Core Domain + Hydrology | ME-02-D; P2-09-22 gate |
| ME02-G07 | Whether Watershed should become a formal Object Type | Core Domain + ME-02 | Only if independent semantic identity is justified |
| ME02-G08 | Climate classification scheme and thresholds | Natural Geography science design | ME-02-E |
| ME02-G09 | Ocean current and seasonal wind approximation fidelity | Natural Geography science design | ME-02-E |
| ME02-G10 | Biome classification/suitability vocabulary and provenance | ME-02 + World Knowledge | ME-02-E |
| ME02-G11 | Import formats and reprojection/resampling library | Map Engine technical evaluation | ME-07 |
| ME02-G12 | Exact physical field persistence ownership and cache retention | Core Domain + Map Engine | ME-05/ME-07 |

No user decision is required at this stage: these are professional design/validation gates for the relevant specialist workstreams. Any future decision that changes creator-facing world semantics or product boundaries will be brought forward explicitly.

## 17. Source alignment

This working baseline is intentionally aligned with:

- ME-01-01 through ME-01-07 — unified editor, story/spatial bridge, Guided Initialization, multi-scale consistency, Object/Geometry/Representation, interaction and cross-architecture findings.
- Natural Geography, Climate & Hydrology Model V1.0 — Earth-science causal basis, deterministic physically informed estimates, terrain-first drainage, climate/hydrology/biome dependencies and bounded recalculation.
- Map Rendering & Visualization Architecture V0.3 — Core Domain authority, renderer read-only snapshots, spatial services independent of Skia/Avalonia, derived scenes and local desktop boundaries.
- Spatial Presentation and Editing Interaction Contract V0.1 — typed coordinate spaces, geometry queries, hit testing, snapping and preview lifecycle.
- Map Generation Specification/Workflow/Session Lifecycle V0.2 — simple direct tasks versus complex staged Candidates, risk-based confirmation, provenance, validation, idempotent formal application and cancellation semantics.
- ME-02-D Hydrology & Drainage Authoring V0.1 — terrain-first drainage, depression policy, derived watershed/channel analysis, River network semantics, unsupported outlet boundaries and V1 validation.
- ME-02-E Climate & Ecology Authoring V0.1 — causal climate estimation, seasonal profiles, atmospheric/ocean influence, climate classification, biome/habitat suitability and creator-fact protection.
- Core Domain P2-09-15 V0.5, P2-09-17 V0.5, P2-09-18 V0.2, P2-09-19 V0.2, P2-09-20 V0.2, P2-09-21 V0.1, P2-09-22 V0.2, P2-09-24 V0.1 and P2-09-25 V0.1 — formal command, typed object/geometry/relationship and immutable Asset boundaries.

The documents above retain their own authority/status. ME-02 does not promote a recommended catalog or a technical proposal into a frozen ADR, executable schema or implementation authorization.

## 18. Status statement

ME-02 V0.5 establishes the natural-geography editor's product workflow, domain content categories, scientific constraints, calculation boundaries and V1 capability scope, reconciled through ME-02-G.

ME-02-A through ME-02-G now have specialist working-baseline documents. ME-02-F defines reproducible input snapshots, determinism profiles, bounded dependency invalidation, tile/halo and resolution semantics, separate Run/Artifact lifecycles, freshness-safe atomic publication, cancellation/recovery, diagnostics and local resource scheduling. ME-02-G defines end-to-end creator journeys for story-led initialization, direct land/water/elevation authoring, terrain features, drainage, climate/ecology, local refinement, failure/recovery and AI Candidate review; it also defines Inspector, diagnostics, confirmation, accessibility, Figma handoff and acceptance scenarios. The remaining work is ME-02-H cross-architecture review. Exact geometry/raster persistence, scientific parameterization, algorithm implementation, final visual design and platform performance remain open engineering/design gates.
