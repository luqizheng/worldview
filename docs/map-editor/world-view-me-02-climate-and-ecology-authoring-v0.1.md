# World View — ME-02 Climate & Ecology Authoring

- Version: 0.1
- Date: 2026-11-21
- Status: **Climate & Ecology Specialist Workflow Working Baseline** — causal domain model, creator workflow, V1 calculation strategy and ecological interpretation baseline; exact numerical calibration, field storage, classification thresholds and executable services remain open
- Parent: [ME-02 Terrain & Natural Geography Editor](world-view-me-02-terrain-and-natural-geography-editor-v0.2.md)
- Depends on: ME-02-01 through ME-02-05; Natural Geography, Climate & Hydrology Model V1.0; ME-01-02/03/04/05/06/07; Core Domain field/Asset and typed Command contracts; Map Engine immutable scene and spatial query contracts
- Scope: Planetary/season assumptions, atmospheric and ocean influence, temperature and precipitation estimates, climate classification, soil-water context, biome/ecological suitability, creator-authored ecology, validation and bounded recalculation

## 1. Purpose and executive recommendation

This document defines how a creator authors, inspects and refines climate and ecological geography in World View.

**Core recommendation:** V1 should implement a layered, deterministic, physically informed climate model. It should calculate broad seasonal temperature and precipitation patterns from declared world parameters, latitude, elevation, land/ocean distribution, simplified circulation, moisture sources, prevailing winds and terrain barriers. It should then derive climate classifications and biome/ecological suitability from those physical estimates plus soil/water context.

The product must not treat climate as a manually painted independent color layer, nor pretend to run a full atmospheric/ocean simulation. The model is an explainable world-building model, not a weather forecast system.

The model separates four authorities:

1. **Creator/world facts and constraints** — confirmed planetary parameters, intended climate/ecology facts, protected regions and explicit supernatural/engineered exceptions.
2. **Physical source inputs** — world frame, land/water, elevation, seasonal/planet parameters, authored circulation assumptions, soil/surface properties and water sources.
3. **Derived physical and ecological analysis** — seasonal temperature/precipitation estimates, moisture balance, climate classes, biome suitability and diagnostics.
4. **Presentation** — climate palettes, biome colors, wind arrows, seasonal overlays, labels and fantasy-atlas styling.

Derived analysis never silently overwrites creator facts. A confirmed biome or special habitat may intentionally differ from the natural suitability estimate; the system should show and explain the difference.

## 2. Goals and non-goals

### 2.1 Goals

- Let creators reason about climate in ordinary language without requiring meteorology or GIS knowledge.
- Make climate outcomes causally explainable rather than arbitrary.
- Support Earth-like defaults while allowing explicit non-Earth planetary parameters.
- Represent seasonality and hemispheric differences, not just one annual climate color.
- Account for latitude, axial tilt, elevation, land-sea contrast, prevailing winds, moisture sources, ocean-current influence and mountain rain shadows.
- Derive climate classification only after underlying temperature and precipitation estimates exist.
- Distinguish climate from biome, biome suitability and actual creator-confirmed ecosystems.
- Incorporate water balance, soil/infiltration and broad surface conditions where available.
- Support species/habitat constraints as World Knowledge without forcing implausible physical geography.
- Preserve creator intent and explain natural-model deviations.
- Recalculate only affected areas and dependent outputs.
- Keep the workflow usable offline and without an AI provider.

### 2.2 Non-goals for V1

- Global atmospheric or ocean general circulation model (GCM).
- Daily/hourly weather, storm tracks, exact wind speed, rainfall forecasts or climate prediction.
- Detailed cloud microphysics, convection, fronts, cyclones or severe-weather simulation.
- Exact ocean temperature/current strength without a calibrated physical model.
- Detailed groundwater, aquifer, soil chemistry, carbon cycle or ecosystem population dynamics.
- Full vegetation growth, succession, wildfire, disease, migration or food-web simulation.
- Species-level biodiversity prediction from climate alone.
- Exact agricultural yield or human comfort/health prediction.
- Automatic generation of confirmed forests, deserts, species or ecosystems from a climate class.
- An unrestricted scientific raster algebra/workflow engine or user-programmable model language.

## 3. Creator mental model and workflow

The creator should see familiar questions rather than scientific parameter panels:

- “Why is this region cold?”
- “Why does the western side of this mountain range receive more rain?”
- “The interior should be dry, but the coast should be humid.”
- “Make the northern and southern hemispheres have opposite seasons.”
- “This island should have a mild maritime climate.”
- “Show me where forests are plausible, not where they are already confirmed.”
- “This desert is supernatural. Keep it even though the natural model predicts more rainfall.”
- “The high plateau should be cold, but its valleys should be warmer.”
- “What changes if I move this mountain chain farther inland?”

The assistant translates these requests into:
- target Working Scope and calculation context;
- relevant source inputs and their freshness;
- confirmed facts, soft preferences and protected content;
- assumptions the creator has or has not specified;
- a proposed analysis or Candidate;
- a causal explanation of the result and uncertainty.

### 3.1 Two entry paths

**Climate-first inspection**
1. Creator selects a world or region and asks for climate analysis.
2. System inspects available world parameters, elevation, land/water, circulation and moisture inputs.
3. Assistant identifies missing inputs that materially affect the requested result.
4. Deterministic services derive seasonal physical estimates.
5. Creator inspects causes, classifications and confidence.
6. Creator may refine inputs or accept selected derived outputs for presentation/use by downstream analysis.

**Ecology-first authoring**
1. Creator describes an intended biome, habitat or ecosystem.
2. Assistant distinguishes a confirmed world fact from a preference or desired natural outcome.
3. System evaluates climate, water, soil and elevation suitability.
4. It explains which inputs support or conflict with the intended ecosystem.
5. Creator may change physical inputs, adjust a soft preference, or preserve the intended ecology as an explicit exception.
6. The actual ecosystem fact remains separate from the derived suitability layer.

### 3.2 Progressive workflow, not a wizard

The recommended dependency order is:
1. world/planet and season assumptions;
2. land/water and elevation;
3. broad circulation and prevailing wind;
4. moisture sources and ocean influence;
5. temperature and precipitation seasonality;
6. water balance and soil context;
7. climate classification;
8. biome/ecological suitability;
9. habitat and ecological consistency review.

A creator may inspect or author any layer at any time. If upstream inputs are missing, the UI should explain the limitation rather than block unrelated manual work.

## 4. Climate and ecology authority model

| Content | Representation | Authority |
|---|---|---|
| Planet size/scale, axial tilt, orbital/season convention | Typed world parameters / creator facts | Core Domain or approved World Knowledge according to existing authority contract |
| Latitude and spatial reference | Map Space/world frame | Shared spatial model |
| Land/ocean/lake mask | Authored/imported physical field and semantic geography context | Declared source authority |
| Elevation | Continuous physical field | Immutable source Asset / field contract |
| Prevailing wind/circulation assumptions | Authored physical assumptions or derived vector fields | Explicit source/provenance; not decorative presentation |
| Ocean-current influence | Simplified authored/derived physical vector or influence field | Derived or creator-specified assumption |
| Temperature | Seasonal continuous derived field | Rebuildable derived analysis |
| Precipitation | Seasonal continuous derived field | Rebuildable derived analysis |
| Moisture balance / aridity indicators | Derived continuous/categorical fields | Rebuildable derived analysis |
| Soil / infiltration / surface water | Authored/imported or derived field, with source class | Declared field authority |
| Climate classification | Derived categorical layer | Scheme/version dependent |
| Biome suitability | Derived categorical/probability/range layer | Not actual ecosystem truth |
| Actual forest/desert/wetland/ecosystem fact | Creator-confirmed fact, formal TerrainRegion where its descriptor supports it, or World Knowledge | Creator/domain authority |
| Species habitat preferences | World Knowledge constraints | Story/world knowledge authority |
| Habitat suitability | Derived intersection of ecology conditions and species preferences | Derived analysis |
| Climate/biome palette, wind arrows, seasonal animation | Presentation | Presentation only |

### 4.1 Climate is not a TerrainRegion by default

Climate zones and biome suitability remain derived layers in V1. Do not automatically create a TerrainRegion Object for each climate class or biome cell.

A creator-authored actual forest, desert, wetland or other bounded terrain fact may use the existing TerrainRegion type and its registered classification only where the type's semantics fit. Do not extend its enum or use it as a generic storage bucket without a Core Domain descriptor decision.

### 4.2 Actual ecology versus suitability

The product must display distinct concepts:
- **Natural suitability** — what the current physical model suggests could exist.
- **Creator-confirmed ecosystem** — what the creator says actually exists.
- **Species habitat suitability** — whether a species' known preferences intersect local ecological conditions.
- **Unresolved / unknown** — insufficient evidence.

A high biome suitability score does not prove that a forest exists. A low suitability score does not invalidate a creator-confirmed magical forest, irrigated oasis or specially adapted ecosystem.

## 5. Planetary frame and season model

### 5.1 Explicit world parameters

The model uses Earth-like defaults only where the creator has not specified alternatives. Parameters must be versioned and explicit where they materially affect outputs:
- world/map scale and spatial reference;
- planetary radius or adopted map-scale convention;
- axial tilt;
- orbital-year and season convention;
- rotation direction and broad rotation assumptions where circulation uses them;
- solar baseline assumptions;
- atmospheric baseline assumptions;
- gravity and lapse-rate assumptions when materially different from Earth;
- global water availability / ocean coverage assumptions if not fully represented by the map.

Do not infer Earth-sized radius, Earth's axial tilt or Earth's exact orbital calendar from the mere presence of familiar continents or seasons. If a parameter is unknown, use a named default profile and expose it in provenance.

### 5.2 Season representation

V1 should use a compact **seasonal profile** rather than a mandatory 12-month weather model.

Recommended baseline:
- four normalized seasonal phases (or equivalent creator-defined seasonal phases);
- season phase boundaries derived from the world/orbit convention;
- hemisphere-aware seasonal phase offsets;
- seasonal outputs summarized as representative values/ranges, not daily forecasts.

The model must not assume that all worlds have four named seasons or that both hemispheres share the same season at the same time. The four-phase profile is an internal baseline for familiar Earth-like worlds; alternate calendars/season systems may define a different phase count later.

Seasonal values should retain the phase definition and parameter version. Annual summaries are derived from the seasonal profile and must not replace it.

### 5.3 Latitude and solar baseline

Solar energy baseline depends on latitude, season and axial tilt. The model should capture:
- broad equator-to-pole energy gradient;
- seasonal migration of solar exposure;
- polar day/night tendencies where the selected planetary model supports them;
- hemispheric phase opposition;
- differences between annual mean and seasonal amplitude.

Latitude bands are not final climate zones. Elevation, ocean influence, wind and moisture can materially alter regional outcomes.

## 6. Atmospheric circulation and prevailing winds

### 6.1 V1 model level

Use a simplified, deterministic circulation baseline for an Earth-like rotating world. It should represent broad latitude-dependent pressure/wind belts and seasonal migration, not simulate fluid dynamics.

The system may derive broad prevailing-wind vectors from:
- rotation direction and broad Coriolis tendency;
- latitude band and season;
- simplified circulation-cell assumptions;
- broad land/sea thermal contrast;
- major mountain barriers and regional channeling where justified.

A creator may author regional prevailing-wind adjustments as explicit physical assumptions. These must be visible as overrides/inputs, not hidden changes to a derived vector field.

### 6.2 Wind semantics

Keep these separate:
- prevailing wind direction (physical driver);
- seasonal wind direction changes (physical driver);
- local topographic channeling (limited derived tendency);
- storm/wave weather (not simulated in V1);
- wind arrows, animated particles and map glyphs (presentation).

V1 should prefer broad directional classes and confidence rather than unsupported precise wind speeds.

### 6.3 Seasonal monsoon-like patterns

The model may support a broad seasonal reversal or strengthening of prevailing wind where land-sea thermal contrast and seasonal circulation justify it. It should not label a region "monsoon" merely because rainfall varies seasonally; the cause should include a plausible seasonal wind/moisture reversal or convergence pattern.

Detailed monsoon dynamics and daily onset/withdrawal are out of scope.

## 7. Ocean influence and moisture sources

### 7.1 Maritime versus continental influence

Ocean and large-water-body proximity can moderate seasonal temperature range and provide moisture. The model should consider:
- distance/path from a moisture-bearing water body;
- prevailing wind direction relative to the water source;
- broad warm/cold current influence where available;
- landmass size and inland distance;
- terrain barriers between source and target.

Straight-line distance alone is insufficient. A coastal region downwind of a dry continental interior may not receive the same moisture as a windward coast exposed to an ocean source.

### 7.2 Ocean currents

V1 currents are broad influence assumptions, not simulated ocean circulation. They may be:
- creator-authored broad current paths/directions;
- generated from simplified basin-scale rules as a candidate;
- assigned broad warm/cold influence classes only when supported by declared inputs.

Current influence must be consistent at least at a broad level with ocean-basin geometry, rotation/Coriolis tendencies and prevailing winds. Do not display exact current temperatures or strengths.

### 7.3 Inland lakes and seas

Large inland water bodies may moderate nearby temperatures and contribute moisture, but their effect should be scaled by size, season and local circulation assumptions. Small ponds must not be treated like oceans.

The model should not assume a lake is permanent or a source of unlimited moisture if its water balance is unresolved.

### 7.4 Moisture transport

V1 moisture transport should be a deterministic, coarse directional influence model:
1. identify moisture-source regions (ocean, sufficiently large water bodies, broad evapotranspiration assumptions);
2. use prevailing wind direction and seasonal phase to determine likely downwind transport;
3. attenuate influence with travel distance and intervening terrain/land context;
4. apply broad convergence/uplift and orographic effects;
5. produce seasonal precipitation estimates with uncertainty.

This is not atmospheric parcel tracking. It must not claim exact moisture flux.

## 8. Temperature estimation

### 8.1 Causal factors

Seasonal temperature estimates combine:
- solar/latitude baseline;
- axial tilt and season;
- elevation/lapse tendency;
- maritime versus continental moderation;
- broad warm/cold current influence;
- broad air-mass advection where modeled;
- persistent snow/ice influence only where represented and supported.

### 8.2 Elevation

Higher elevation generally reduces temperature. V1 may use a declared lapse-rate profile or broad elevation bands, but:
- lapse-rate assumptions must be explicit and versioned;
- do not use a universal exact lapse rate as a law for all planets and seasons;
- local inversions and valley cold-air pooling are not guaranteed by the coarse model;
- where resolution cannot support local effects, label them as unavailable rather than inventing them.

### 8.3 Maritime and continental effects

Large water bodies tend to reduce seasonal temperature extremes relative to inland areas. The model should represent this as broad moderation, not as a fixed coastal temperature offset. Prevailing wind exposure and water-body size matter.

### 8.4 Outputs

V1 temperature output should include:
- seasonal mean/representative estimate or range;
- annual mean summary;
- seasonal amplitude summary;
- source/assumption provenance;
- uncertainty/coverage status.

Do not show high-precision decimal values by default. Creator-facing presentation should prefer plain-language classes and ranges appropriate to the model resolution.

## 9. Precipitation, aridity and rain shadows

### 9.1 Causal factors

Seasonal precipitation estimates combine:
- moisture-source availability;
- prevailing wind and seasonal direction;
- broad convergence/uplift zones;
- orographic uplift and windward exposure;
- leeward rain-shadow attenuation;
- temperature-dependent moisture capacity/condensation assumptions;
- seasonal circulation shifts;
- land/sea distribution and inland distance.

### 9.2 Orographic effect

A rain-shadow result is only justified when:
- a moisture-bearing wind approaches a terrain barrier;
- the windward side has a plausible uplift/exposure relationship;
- the leeward side lies downstream of that barrier;
- the model resolution can resolve the barrier and its orientation.

A mountain range alone does not guarantee a rain shadow. A dry wind, wrong wind orientation or coarse unresolved ridge should not create a confident rain-shadow result.

The UI should show the causal chain: moisture source → prevailing wind → uplift/barrier → windward precipitation → leeward moisture reduction. It should distinguish a likely rain shadow from a creator-confirmed arid region.

### 9.3 Aridity

Aridity must be based on moisture deficit, not a desert label. The model should distinguish broad causes such as:
- subtropical descending/dry circulation tendency;
- rain shadow;
- continental interior moisture depletion;
- cold-desert conditions;
- seasonal dry period;
- local water deficit caused by soil/infiltration context.

V1 may use broad moisture-balance indicators based on precipitation and estimated evaporative demand. If potential evapotranspiration is too weakly supported, expose the estimate as relative moisture balance rather than presenting a formal aridity index with false precision.

### 9.4 Precipitation outputs

V1 outputs:
- seasonal precipitation estimate/range or relative class;
- annual total/range only when the underlying estimates support aggregation;
- wet/dry season pattern;
- moisture-source and windward/leeward evidence;
- uncertainty, source coverage and model resolution.

Do not imply exact local rainfall measurements.

## 10. Water balance and soil context

Climate and hydrology share physical drivers but remain distinct derived analyses.

Water balance may use:
- seasonal precipitation;
- temperature/evaporative-demand proxy;
- basin contributing area and drainage context;
- snow accumulation/melt tendency;
- soil/infiltration class;
- surface-water presence;
- broad groundwater/spring assumptions where explicitly represented.

Outputs may include relative wetness/moisture availability, runoff tendency, seasonal water stress and support for perennial/seasonal river behavior. Hydrology remains governed by ME-02-D: climate outputs inform water supply but do not replace terrain-first flow routing.

Soil and infiltration:
- may be creator-authored/imported broad classes or derived candidates;
- should have explicit source and classification scheme;
- affect runoff, water retention, vegetation suitability and wetland tendency;
- must not be inferred from climate alone as a confirmed soil fact.

V1 does not simulate detailed pedogenesis, soil chemistry, nutrient cycling or groundwater flow.

## 11. Climate classification

### 11.1 Classification is downstream of physical estimates

A climate class is a summary of temperature and precipitation patterns. It is not the physical cause and must not replace underlying seasonal fields.

V1 should implement a compact, versioned classification scheme with broad, understandable families:
- tropical/warm-humid;
- arid/semi-arid;
- temperate;
- continental/cold seasonal;
- polar/high-elevation;
- and optional finer subtypes where input resolution and thresholds justify them.

A Köppen-style classification may be offered as a named, versioned scheme if its required temperature/precipitation thresholds are fully implemented and tested. Do not claim strict Köppen-Geiger equivalence if the model only supports approximate broad families.

### 11.2 Classification rules

- Scheme identifier and version are part of the derived output.
- Threshold tables are data/configuration owned by the classification service, not scattered UI constants.
- The scheme can be rerun over existing temperature/precipitation fields without recalculating physical climate.
- Unknown/insufficient inputs yield unresolved classes, not guessed classes.
- Transitional boundaries should not be presented as precise natural borders.
- The creator can inspect the underlying seasonal values and causal factors for any classified cell/region.

## 12. Biome and ecological suitability

### 12.1 Biome is not climate

Biome suitability depends on multiple conditions:
- temperature and seasonality;
- precipitation and moisture balance;
- soil and water retention;
- elevation and broad terrain context;
- snow/ice and growing-season constraints;
- disturbance or special world rules where known.

The same climate family can support different vegetation depending on soil, water table, elevation, fire/disturbance and species composition.

### 12.2 V1 suitability model

V1 should use a transparent rule-based suitability model with broad classes (for example: strongly suitable, suitable, marginal, unsuitable, unknown) rather than a black-box ecosystem simulator.

For each biome profile, declare:
- compatible temperature/season range;
- precipitation/moisture range;
- soil/infiltration preferences where known;
- elevation/terrain constraints where relevant;
- optional disturbance/water-table assumptions;
- provenance/version and known limitations.

A profile is a reusable analysis definition, not a formal world Object. Keep the initial profile catalog small and general; do not create a huge universal ecology ontology.

### 12.3 Actual biome / creator-authored ecosystem

The creator may author a confirmed ecosystem or vegetation region even when natural suitability disagrees. The system should:
- preserve the confirmed fact;
- display the natural suitability conflict separately;
- identify possible explanations (irrigation, groundwater, unusual soil, migration, adaptation, magic, deliberate cultivation);
- allow the creator to add an explicit causal note/rule if desired;
- avoid silently changing the physical climate to force agreement.

A suitability result must never automatically create a formal forest/desert/biome Object.

### 12.4 Species and habitat

Species preferences belong to World Knowledge, not the climate model. Habitat suitability may evaluate:
- species' preferred temperature/moisture ranges;
- vegetation/food or shelter conditions where represented;
- elevation/terrain limits;
- water access and seasonal conditions;
- creator-authored adaptations or magical constraints.

The result is a compatibility assessment, not proof of population presence, abundance, migration or survival. A species mentioned in a story does not automatically create a spatial habitat Object.

## 13. V1 calculation architecture and workflow

### 13.1 Logical calculation pipeline

```text
World Frame + Planet/Season Parameters
                 |
        Land/Water + Elevation
                 |
     Solar / Latitude / Season Baseline
                 |
   Broad Circulation + Prevailing Wind
                 |
   Ocean / Inland Water Moisture Influence
                 |
     Temperature + Precipitation Estimates
                 |
     Water Balance + Soil/Surface Context
                 |
          Climate Classification
                 |
       Biome / Habitat Suitability
                 |
      Causal Validation and Diagnostics
                 |
       Creator Review / Presentation
```

This is a dependency graph, not a mandatory all-or-nothing batch. A request to rerun a climate classification scheme need not recalculate temperature. A change to biome palette must not recalculate climate.

### 13.2 Deterministic stages

Recommended V1 stage boundaries:
1. **World parameter resolver** — resolve explicit creator parameters and named defaults.
2. **Solar/season service** — broad seasonal insolation baseline by latitude and world parameters.
3. **Circulation service** — broad seasonal pressure/wind tendencies.
4. **Moisture influence service** — source proximity, wind direction, broad current and terrain attenuation.
5. **Temperature estimator** — combine solar, elevation, maritime and broad advection effects.
6. **Precipitation estimator** — combine moisture availability, wind, convergence and orographic effects.
7. **Water-balance estimator** — combine precipitation, temperature proxy, drainage, soil and snow assumptions.
8. **Classification service** — versioned climate class rules.
9. **Ecology suitability service** — rule-based biome and habitat compatibility.
10. **Validator/explainer inputs** — structured causal evidence, conflicts, severity and suggested repairs.

Each stage must declare input fingerprints, algorithm/version, parameters, coverage and output fingerprint. LLM must not calculate raster values or invent numeric physical results.

### 13.3 Iteration and feedback

Terrain, climate, water and ecology can influence one another. V1 must avoid an unbounded circular solver.

Use controlled iteration:
- establish a pinned baseline from creator-confirmed inputs;
- compute a dependency-ordered pass;
- identify feedback-relevant changes (e.g. snow/ice or surface-water assumption);
- allow only explicitly declared bounded follow-up passes;
- record pass number, source versions and stopping reason;
- stop when configured convergence/iteration limits are reached or when the creator must resolve a semantic choice.

Climate output must not feed back to rewrite elevation or confirmed land/water geometry. Vegetation suitability must not silently alter precipitation. Any future feedback from actual vegetation or snow/ice requires a declared physical mechanism and separate versioned stage.

## 14. Scale, resolution and uncertainty

Use multi-resolution outputs consistent with ME-01-04:
- **World scale:** broad seasonal temperature/precipitation and circulation patterns; large climate families.
- **Region scale:** mountain rain shadows, continentality, regional wet/dry seasons and biome mosaics.
- **Locality/Site scale:** only effects supported by source resolution; local valley inversions, microclimates and fine habitat conditions are not guaranteed by V1.

Rules:
- Never claim finer precision than the coarsest material input or model capability.
- Do not infer exact local rainfall from broad global wind bands.
- Regional refinement must preserve macro boundary conditions and neighboring context.
- A field sample must disclose resolution, unit/range, no-data and source provenance.
- Unknown and no-data remain distinct from zero precipitation or neutral temperature.
- Derived boundaries are approximate transition zones, not exact ecological walls.

## 15. Validation and causal diagnostics

### 15.1 Climate diagnostics

- High-elevation region warmer than lowlands without an explicit circulation/inversion/planet explanation.
- Strong rain shadow without a moisture-bearing prevailing wind and resolved barrier.
- Wet climate with no plausible moisture source or convergence/uplift evidence.
- Arid region without a plausible moisture deficit mechanism.
- Maritime temperature moderation applied across an unresolved land barrier or without meaningful water-body scale.
- Seasonal pattern inconsistent with the declared axial tilt/season convention.
- Opposite hemispheres assigned identical seasonal phase when the world model implies seasonal opposition.
- Climate classification generated from missing or stale temperature/precipitation inputs.
- Fine-grained climate class shown from coarse or incomplete inputs.

### 15.2 Ecology diagnostics

- Biome suitability incompatible with temperature/moisture/soil profile.
- Confirmed ecosystem differs from suitability estimate.
- Species habitat preferences conflict with local conditions.
- Wetland suitability without plausible water supply/low-gradient/water-table evidence.
- Snow/ice persistence unsupported by seasonal temperature/energy assumptions.
- Forest/vegetation suitability shown as actual forest presence.
- Creator-confirmed magical/engineered exception lacks explicit cause/provenance (warning, not automatic rejection).

### 15.3 Severity

| Severity | Meaning | Example |
|---|---|---|
| Blocking | Invalid/stale inputs or unsafe formal application | Wrong world frame, missing required inputs for a claimed output, stale source fingerprint |
| Warning | Physical/ecological inconsistency needing review but potentially intentional | Strong rain shadow without clear moisture path, unsuitable confirmed biome, weak water supply |
| Information | Assumptions, scale, method, uncertainty or normal variation | Broad seasonal profile, coarse circulation model, approximate climate boundary |

A warning must not automatically reject a creator-confirmed fact. Explicit world rules may explain exceptions. A blocking result prevents an invalid calculation or formal operation, not creative authorship itself.

## 16. Incremental recomputation

| Changed input | Direct outputs to invalidate/recompute | Downstream outputs |
|---|---|---|
| Planet radius / axial tilt / season convention | Solar and seasonal baseline | Circulation, temperature, precipitation, water balance, climate classes, ecology suitability |
| Latitude/world frame | Solar baseline and affected spatial mapping | All climate/ecology outputs in affected coverage |
| Land/ocean/water mask | Maritime influence, moisture-source context, coastal effects | Temperature, precipitation, water balance, climate class, ecology |
| Elevation | Temperature/lapse, wind exposure, orographic precipitation | Water balance, climate class, biome/habitat suitability |
| Prevailing wind/circulation | Moisture transport, orographic exposure, seasonal rainfall | Water balance, climate class, ecology |
| Ocean-current influence | Maritime temperature and moisture influence | Temperature, climate class, ecology |
| Seasonal precipitation inputs/assumptions | Precipitation, moisture balance | Hydrology seasonal tendency, climate class, ecology |
| Soil/infiltration | Water retention/runoff proxy | Water balance, wetland/vegetation suitability |
| River/lake/snow-water context | Local moisture/water balance | Local ecology suitability |
| Climate classification scheme/version | Climate classes only | Any explicitly dependent reports; no physical field recalculation |
| Biome profile/version | Biome suitability only | Species habitat compatibility |
| Species preference | Habitat suitability for affected species | No climate recalculation |
| Creator-confirmed ecosystem fact | Conflict/diagnostic overlays | No physical climate recalculation |
| Presentation palette/labels | Presentation only | No physical calculation |

Recalculation must use bounded dependency regions and required neighbor/parent context. A local edit must not trigger a whole-world rebuild unless the changed input has a genuine global dependency (for example axial tilt or world-frame change).

## 17. AI Assistant responsibilities and safety boundaries

AI Assistant may:
- interpret natural-language climate/ecology intent;
- extract confirmed facts, preferences, constraints and exceptions;
- ask focused questions about materially missing world parameters;
- explain causal relationships in creator-friendly language;
- select/sequence deterministic calculation services;
- compare candidate results and existing creator facts;
- propose edits or exceptions for review;
- summarize uncertainty and identify inconsistent inputs.

AI Assistant must not:
- invent temperature, rainfall, wind or ecological numeric results;
- pretend to run a physical model when no calculation service ran;
- write derived raster/field data directly to Core Domain tables;
- convert suitability into confirmed biome/species presence;
- silently change creator-confirmed world facts to improve model consistency;
- treat story mentions as automatic spatial object creation;
- bypass typed Commands, authorization, Candidate version/hash binding or formal application checks.

LLM output is an interpretation/plan/explanation. Deterministic services own numerical calculation and structured validation evidence.

## 18. Candidate review and formal adoption

Most climate and ecology results remain derived analysis and do not need Core Domain Object creation.

For operations that affect formal content (e.g. editing a creator-authored TerrainRegion, changing an approved world parameter, or applying a supported formal field/Asset reference):
1. show the exact target, scope and source;
2. distinguish physical input changes from derived recalculation;
3. show protected facts and affected dependent layers;
4. display method/version and expected impact;
5. validate the Candidate against current state;
6. bind authorization to exact proposal version/hash and target context;
7. apply only through the appropriate Core Domain service/typed Command;
8. report applied only after transaction success.

Climate and biome classification layers must not be written as generic JSON patches or disguised formal Objects. Physical field persistence follows the ME-02-B/ME-05 Asset and immutable publication contracts. This document does not invent a new climate-specific persistence authority.

## 19. Error handling and recovery

- **Missing planet parameters:** use a named Earth-like default profile only where permitted; disclose the default and let creator override.
- **Missing elevation:** calculate latitude/season baseline where possible; mark orographic and elevation-dependent results unavailable/low confidence.
- **Missing land/water:** do not invent ocean influence or moisture-source paths.
- **Missing wind:** use only a declared broad default circulation model; mark regional wind/rain-shadow results provisional.
- **Incomplete coverage:** preserve no-data/unknown and show exact affected coverage.
- **Stale input:** invalidate dependent results and rerun; do not publish stale outputs.
- **Algorithm failure:** retain previous valid derived version and mark new run failed; never partially replace a published field.
- **Contradictory creator facts:** show conflict and ask only when the conflict blocks the requested task; do not silently choose.
- **Unsupported local precision:** provide broad regional output and explain why local detail is unavailable.
- **AI/provider unavailable:** deterministic calculations and manual authoring remain available.
- **User cancellation:** stop unfinished calculations and candidates; already applied formal data remains unchanged.

## 20. Acceptance criteria

1. Climate is derived from explicit physical inputs, not arbitrary independent labels.
2. Planet parameters and defaults are explicit, versioned and visible in provenance.
3. Seasonal outputs respect the selected world/hemisphere season convention.
4. Latitude and axial tilt influence solar baseline.
5. Elevation and declared lapse assumptions influence temperature estimates.
6. Maritime/continental influence depends on water-body scale, exposure and terrain context.
7. Rain-shadow analysis requires a plausible moisture-bearing wind and resolved barrier.
8. Aridity is explained by moisture deficit and causal evidence.
9. Climate classes are downstream, versioned and independently rerunnable.
10. Climate classification does not automatically create TerrainRegion Objects.
11. Biome suitability considers climate, water, soil and terrain context.
12. Suitability is visibly distinct from creator-confirmed ecosystem facts.
13. Species habitat analysis does not imply species presence or population.
14. Creator-confirmed facts and supernatural/engineered exceptions remain protected.
15. LLM does not calculate or invent numeric physical fields.
16. Every derived output records source fingerprints, method/version, parameters, coverage and uncertainty.
17. Unknown/no-data is not silently converted to zero or a neutral class.
18. Resolution and output detail never exceed supported input/model capability.
19. Input changes invalidate only declared dependent outputs; style-only changes do not recalculate climate.
20. Climate/ecology calculations remain available without AI/network access.
21. Formal changes use registered Core Domain descriptors and the correct authoring/history target.
22. A cancelled task does not undo previously applied formal changes.

## 21. Open gates and ownership

| ID | Gate | Owner | Closure evidence |
|---|---|---|---|
| CE-01 | Select compact V1 climate classification scheme and threshold tables | Natural Geography Services | Versioned specification, test fixtures and explicit limitations |
| CE-02 | Define world parameter schema and Earth-like default profile | Core Domain + World Knowledge | Typed parameter contract, defaults and migration/validation rules |
| CE-03 | Define four-phase seasonal profile and alternate season extension boundary | Natural Geography Services | Hemisphere/axial-tilt fixtures and versioned phase semantics |
| CE-04 | Parameterize broad circulation and seasonal wind baseline | Natural Geography Services | Deterministic representative-world test corpus |
| CE-05 | Define moisture-source transport and terrain attenuation method | Natural Geography Services | Explainable source-to-region evidence and uncertainty tests |
| CE-06 | Define temperature and precipitation estimator contracts | Natural Geography Services | Input/output units, assumptions, calibration bounds and reproducibility tests |
| CE-07 | Define soil/water-balance proxy limits | Natural Geography + Hydrology | Shared causal vocabulary and cross-model validation |
| CE-08 | Define initial biome profile catalog and suitability rule contract | World Knowledge + Natural Geography | Small versioned profile set and explainability tests |
| CE-09 | Define field Asset metadata, tiled coverage and atomic publication | Core Domain + ME-05 | Immutable Asset contract and stale-publication tests |
| CE-10 | Define dependency/freshness fingerprint and bounded recomputation | Map Engine + Calculation Services | Invalidation graph fixtures and no-global-recompute tests |
| CE-11 | Define UI language for natural suitability vs actual creator-confirmed ecology | Editor UX | Usability scenarios and no-semantic-confusion acceptance |
| CE-12 | Reconcile all ME-02 specialist contracts and close cross-architecture findings | ME-02-H | Traceable consistency review and resolved/deferred gate register |

## 22. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| CE-T01 | Same latitude, different elevation | Higher terrain is generally cooler under declared lapse assumptions |
| CE-T02 | Northern and southern hemispheres in opposite seasonal phases | Seasonal profile respects hemisphere phase offset |
| CE-T03 | Moist ocean wind meets a resolved mountain barrier | Windward uplift/rainfall and leeward attenuation are explained |
| CE-T04 | Dry wind crosses a mountain range | No confident rain-shadow claim solely from the mountain |
| CE-T05 | Coastal region behind a major mountain barrier | Maritime moisture influence is attenuated where justified |
| CE-T06 | Large inland sea versus small lake | Influence scales with water-body size and declared model assumptions |
| CE-T07 | Inland continental region with weak moisture supply | Aridity explanation identifies inland depletion/circulation causes where supported |
| CE-T08 | Same climate class but different soil/water retention | Biome suitability may differ |
| CE-T09 | Creator confirms a magical forest in a naturally arid region | Forest fact remains; suitability conflict and exception are visible |
| CE-T10 | Species prefers cool wet highlands | Habitat suitability evaluates preference; no species Object is auto-created |
| CE-T11 | Climate classification scheme changes | Reclassifies existing physical fields without recalculating them |
| CE-T12 | Only biome palette changes | No climate/ecology physical calculation is triggered |
| CE-T13 | Local mountain elevation changes | Only affected climate/moisture/ecology outputs and dependent drainage context are invalidated |
| CE-T14 | Axial tilt changes | Global seasonal dependencies are invalidated with bounded staged recalculation |
| CE-T15 | Missing wind or elevation input | Unsupported rain-shadow/elevation detail is marked unresolved, not fabricated |
| CE-T16 | Climate calculation source changes before publication | Stale result is rejected; prior valid output remains available |
| CE-T17 | User cancels after a formal world-parameter change has already applied | Applied parameter remains; unfinished derived calculations stop |
| CE-T18 | Coarse global data viewed at Site scale | UI discloses coarse resolution and does not imply microclimate precision |

## 23. Source alignment and authority

This document is subordinate to:
- ME-01-01 through ME-01-07 — shared editor, story/knowledge bridge, guided initialization, spatial consistency, Object/Geometry, interaction and cross-architecture constraints.
- ME-02-01 V0.2 — overall natural geography product/domain baseline.
- ME-02-02 — formal Object/field/derived-layer/World Knowledge inventory.
- ME-02-03 — land/water, elevation and field source contract.
- ME-02-04 — landmass, mountain and terrain feature authoring.
- ME-02-05 — terrain-first hydrology, River/Lake semantics and drainage analysis.
- Natural Geography, Climate & Hydrology Model V1.0 — Earth-science causal baseline.
- Core Domain typed Object/Relationship/Command, Asset and immutable publication contracts.
- Map Engine scene, geometry, spatial query and freshness contracts.
- Map Generation and AI Assistant Candidate/authorization/submission contracts.

Where this document recommends a specialist behavior not yet represented by a registered Core Domain descriptor or physical Asset contract, it remains a design recommendation and must not be implemented through generic writes.

## 24. Status and next work

ME-02-E establishes the Climate & Ecology specialist workflow and V1 causal model baseline. It does not freeze numerical climate algorithms, calibration, classification thresholds, field persistence, raster libraries or renderer technology.

Next:
- **ME-02-F — Calculation Contracts:** common derived-field lifecycle, determinism, provenance, fingerprints, freshness, incremental invalidation, tiling/halo context, atomic publication and validation severity across terrain, hydrology, climate and ecology.
- **ME-02-G — UX Scenarios & Acceptance:** end-to-end creator workflows, layer/Inspector language, diagnostics, review, manual fallback and failure recovery.
- **ME-02-H — Cross-architecture Review:** reconcile ME-02 with Core Domain, Map Engine, Map Generation, AI Assistant and ME-01 findings before implementation handoff.
