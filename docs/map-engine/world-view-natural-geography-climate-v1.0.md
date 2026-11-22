# World View — Natural Geography, Climate & Hydrology Model V1.0

- Version: 1.0
- Date: 2026-11-21
- Status: Scientific and logical design baseline
- Scope: Macro and regional natural-geography generation, climate causality, hydrology, biomes, validation and incremental recomputation
- Product target: Single-user, local-first C# + Avalonia desktop application
- Parent: Guided Initialization & Story-to-World Modeling Architecture V1.0

## 1. Executive decision

World View's natural-geography system uses Earth science as its governing theoretical basis. Climate is not an arbitrary color layer or a free-form LLM invention. Cold, heat, aridity, humidity, rainfall, snow, vegetation and drainage must have explainable causes consistent with physical geography and climate science.

The product does **not** need a full Earth-system numerical simulator in V1. It should implement a layered, deterministic, physically informed model whose inputs, assumptions, resolution and limitations are explicit. The LLM explains and coordinates the workflow; deterministic domain algorithms calculate geometry, drainage, climate indicators and validation wherever feasible.

## 2. Scientific scope and honesty

### 2.1 Earth-based theoretical rules

The model follows established Earth mechanisms:
- solar radiation, latitude, season and axial-tilt effects;
- atmospheric circulation, pressure belts, prevailing winds and seasonal migration of circulation patterns;
- land-sea thermal contrast and continentality;
- ocean heat transport and currents;
- elevation-dependent temperature and atmospheric pressure;
- moisture transport, evaporation, condensation and precipitation;
- orographic uplift, windward precipitation and leeward rain-shadow effects;
- watershed divides, gravity-driven drainage, infiltration, runoff and evaporation;
- soil-water balance, vegetation and biome suitability;
- snow accumulation/melt, seasonal flow and floodplain behavior where modeled.

### 2.2 What the model must not claim

A simplified procedural model is not a weather forecast model or a high-resolution coupled ocean-atmosphere general circulation model (GCM). It cannot credibly predict exact local weather, storm tracks, daily precipitation or long-term climate response at arbitrary resolution.

UI and output metadata must label results as procedural estimates / physically informed classifications at the selected map scale. Detailed scientific simulation may be a later optional subsystem, not a prerequisite for world-map creation.

### 2.3 Planet parameters

Use Earth-like defaults for a conventional Earth-like world where the creator has not supplied alternatives. Store assumptions explicitly, including at least:
- planetary radius/scale model or map-scale convention;
- axial tilt and orbital-year/season convention;
- rotation direction/period where relevant to circulation assumptions;
- atmospheric baseline and water availability assumptions;
- gravity/temperature-lapse assumptions if materially different from Earth.

Do not silently assume that a fantasy planet has Earth's exact size or orbit. The climate mechanisms remain Earth-based; unusual planetary parameters must be creator canon or explicit scenario assumptions.

## 3. Required input layers

The climate/hydrology model depends on a consistent physical stack:

1. Map/world frame: coordinate reference, scale, latitude range, extent and edge semantics.
2. Land-water mask: ocean, inland sea, lake, land and unknown/outside-map distinction.
3. Elevation model: continuous elevation field plus semantic terrain features (mountains, plateaus, basins, plains, valleys, cliffs).
4. Surface properties: broad land cover, soil/infiltration classes, snow/ice and water bodies where available.
5. Planet/season parameters: Earth-like defaults or explicitly authored alternatives.
6. Atmospheric circulation assumptions: global/seasonal pressure and wind pattern at the chosen resolution.
7. Ocean circulation assumptions: simplified current directions and relative thermal influence where ocean extent permits.
8. Water sources and sinks: precipitation, evaporation, runoff, infiltration, groundwater/spring assumptions where represented.

A map may have incomplete input layers. The system must mark outputs as provisional and identify which missing inputs materially limit confidence; it should not fabricate precision.

## 4. Dependency order and feedback loops

The preferred calculation order is:

```text
World frame + planetary assumptions
        ↓
Land / ocean geometry + elevation structure
        ↓
Latitude / seasonal solar baseline
        ↓
Atmospheric circulation + prevailing wind fields
        ↓
Moisture-source and ocean-current influence
        ↓
Orographic lifting / rain-shadow + regional precipitation
        ↓
Temperature field (latitude + elevation + maritime/continental effects)
        ↓
Water balance + snow / runoff / infiltration
        ↓
Climate classification
        ↓
Soil / vegetation / biome suitability
        ↓
Ecological and human-geography suitability checks
```

This is a logical dependency order, not a one-way physical truth. Terrain affects circulation and precipitation; climate and water alter vegetation and surface properties; snow/ice and lakes can feed back on local conditions. V1 should support bounded iterative recalculation rather than pretend that all layers can be solved independently in one pass.

## 5. Climate model decomposition

### 5.1 Solar and seasonal baseline

Derive broad temperature-energy patterns from latitude, season, axial tilt assumptions and elevation. Do not use a fixed horizontal climate banding independent of topography or sea influence. Seasonal outputs should be represented as ranges or seasonal profiles, not one annual color alone.

### 5.2 Atmospheric circulation

For an Earth-like rotating world, use a simplified multi-cell circulation baseline with latitude-dependent pressure/wind belts and seasonal migration. The model should permit regional prevailing-wind adjustments where justified by land-sea geometry, mountain barriers and seasonal circulation. Wind direction is a physical input to rainfall and rain-shadow analysis, not a decorative arrow layer.

### 5.3 Ocean influence

Where ocean geometry is available, estimate broad warm/cold current influence and maritime moderation. In V1, current paths may be simplified and creator-editable. Do not assign precise ocean current temperatures or strengths without a supported model. Currents should be consistent with prevailing winds, rotation/Coriolis tendencies, basin geometry and major ocean circulation patterns at a broad scale.

### 5.4 Temperature

Temperature estimates should combine:
- latitude/solar baseline;
- seasonal cycle;
- elevation lapse;
- maritime versus continental influence;
- broad warm/cold current influence;
- broad air-mass advection where modeled.

High elevation generally reduces temperature; low latitude generally increases solar energy; nearby ocean often moderates seasonal extremes. These are tendencies, not unconditional rules: local circulation and geography can modify them.

### 5.5 Precipitation and aridity

Precipitation estimates should combine:
- moisture availability over ocean/lakes/evapotranspiration sources;
- prevailing wind and air-mass transport;
- convergence and broad uplift zones;
- windward orographic uplift;
- leeward rain shadows;
- seasonal circulation shifts;
- temperature-dependent moisture capacity and condensation assumptions.

Aridity must be explained by moisture deficit, not merely assigned because a region is labeled desert. A cold desert, rain-shadow steppe and hot subtropical desert have different causal profiles and must remain distinguishable.

### 5.6 Climate classification

Classify climate only after deriving temperature seasonality and precipitation seasonality/amount. A climate classification scheme may be selected for V1 implementation (Köppen-style categories are a reasonable familiar presentation option), but the scheme is a classification layer, not the physical model itself. Store underlying variables and assumptions so the classification can be changed without regenerating geography.

### 5.7 Biomes and ecosystems

Biome suitability follows climate, water availability, soil and elevation/ecological context. Do not equate biome with climate: the same broad climate may support different vegetation depending on soil, disturbance, water table and altitude. Species habitat preferences are separate world-knowledge constraints evaluated against biome suitability; a species preference does not force the climate to become physically implausible.

## 6. Hydrology and drainage

### 6.1 Terrain-first drainage

Rivers must follow terrain and watershed logic. They must not be drawn merely to connect settlements. The hydrology pipeline should:
1. identify drainage basins and divides from elevation;
2. resolve sinks/depressions as closed basins, lakes, wetlands, endorheic drainage, or data errors according to context;
3. derive flow direction and accumulation;
4. establish headwaters and tributary confluences;
5. derive main channels and outlets to ocean/lake/closed basin;
6. estimate perennial/seasonal/intermittent behavior from climate and basin properties;
7. estimate floodplains, wetlands and navigability only at supported resolution.

### 6.2 Water-balance factors

River flow and lake persistence depend on precipitation, snowmelt, evaporation, infiltration, soil permeability, basin area, groundwater/spring assumptions and seasonal variation. The model should support relative flow classes at macro scale before attempting exact discharge.

### 6.3 Validation

Flag, at minimum:
- rivers flowing uphill or crossing divides without a valid connection;
- tributaries joining inconsistently or disconnected segments;
- unexplained inland river termination in a non-endorheic basin;
- implausible source/outlet relationships;
- perennial rivers in a strongly negative water-balance area without snowmelt/groundwater support;
- major settlements depending on water sources that do not plausibly exist;
- ports without navigable coastal access or a credible harbor/estuary context;
- ferry/ford objects without a traversable river crossing context.

These are warnings or blockers according to severity and map scale, not absolute prohibitions when a creator has supplied an explicit supernatural or engineered explanation.

## 7. Terrain and geomorphology constraints

- Mountain chains, plateaus, basins, plains, valleys and cliffs are semantic features supported by elevation geometry, not just symbols.
- Mountain orientation and continuity influence drainage divides, wind exposure and travel corridors.
- A plateau is an elevated broad surface; it is not synonymous with a mountain range.
- A basin may be internally drained or externally drained; do not assume every basin has a river outlet.
- Cliffs and escarpments need a plausible elevation gradient and spatial continuity.
- Coastlines must be derived from land-water geometry and elevation/sea-level assumptions; map frame edges are not automatically coastlines.
- Erosion, glaciation, volcanism and tectonic history may be used as explanatory geological processes when the world needs them, but V1 need not run a geological-time simulation.

## 8. Consistency rules between climate, water and ecology

The validator should assess relationships, not enforce simplistic one-to-one labels:

| Observed condition | Expected explanation/check |
|---|---|
| Cold region | high latitude, high elevation, cold-air influence, cold current, or explicit world rule |
| Hot region | strong solar input, low elevation, warm-air/current influence, or explicit world rule |
| Wet forest | sufficient precipitation and/or reliable soil-water availability, compatible temperature and soil |
| Dry steppe/desert | persistent moisture deficit, rain shadow, subtropical subsidence, continentality or other supported cause |
| Snow/ice | temperature and seasonal energy conditions support accumulation/persistence |
| Large perennial river | sufficient basin area and water supply, snowmelt or groundwater support |
| Extensive wetlands | low gradient, shallow water table/flooding and adequate water supply |
| Productive farmland | suitable temperature, soil, water and growing-season conditions or irrigation infrastructure |
| Species habitat | habitat preference intersects plausible ecological conditions; story-specific adaptations remain explicit |

The validator should return causal diagnostics: the conflicting layers, the likely cause, severity, confidence/limitations, and suggested repair options. It should not silently alter creator-confirmed story facts.

## 9. Resolution and scale strategy

Use multi-resolution spatial data:
- macro/world resolution for continental elevation, major ranges, large basins, broad wind and climate patterns;
- regional resolution for subranges, watersheds, regional climate and biome mosaics;
- local resolution for terrain detail, wetlands, floodplains, vegetation patches and settlement suitability.

Do not compute local precision from coarse global inputs. Regional refinement may improve local detail while preserving macro boundary conditions. The user should be able to choose a region and request higher detail without forcing a complete world-resolution rebuild.

## 10. Incremental recomputation

Changes invalidate only dependent outputs. Examples:
- change latitude/planet season assumptions → solar baseline, temperature seasonality, circulation shifts, climate classification and biome suitability;
- change elevation → temperature, wind exposure, orographic precipitation, watersheds, drainage and biome suitability;
- change coastline/ocean extent → maritime influence, moisture source, currents, ports and coastal ecosystems;
- change prevailing wind → moisture transport, windward/leeward precipitation, aridity and biome suitability;
- change soil/infiltration → runoff, wetlands, groundwater assumptions, agriculture and vegetation suitability;
- change only cartographic style → no physical geography recalculation.

The dependency engine must retain confirmed creator inputs and recompute derived layers from those inputs. Derived outputs need model/version/parameter provenance so they can be regenerated deterministically.

## 11. Data ownership and representation

Separate:
- semantic geographic objects (mountain range, river, lake, plateau, basin, biome region);
- continuous or tiled physical fields (elevation, temperature estimates, precipitation estimates, soil/water indicators);
- categorical derived layers (climate class, biome suitability);
- creator-authored world rules and exceptions;
- cartographic styles and symbols.

Large raster/field payloads should use the Core Domain Asset Store / immutable asset reference conventions where appropriate. Semantic objects and their identities remain Core Domain-owned. AI staging outputs remain candidates until formal application. Derived fields must be marked derived and reproducible; they must not become competing authorities to the underlying terrain and world rules.

## 12. Validation severity

- **Error / blocking**: impossible geometry/topology, invalid drainage direction, broken hard creator constraint, or a required layer missing for a claimed high-confidence result.
- **Warning**: physically unusual but possible, weakly supported climate/hydrology, scale limitations, or a likely mismatch needing review.
- **Information**: assumptions, model resolution, derivation provenance, and expected uncertainty.

An explicit creator-authored magic rule or engineered intervention may explain a physical exception. Such exceptions must be represented as explicit causes/overrides, not as hidden unexplained anomalies.

## 13. MVP implementation recommendation

Implement in this order:
1. stable Map Space, coordinate/scale and map-edge semantics;
2. land/water mask and elevation/terrain representation;
3. drainage basins, flow direction, river networks and lakes;
4. latitude/season baseline and simplified atmospheric circulation;
5. wind/moisture transport and orographic precipitation estimates;
6. temperature and precipitation seasonal summaries;
7. climate classification;
8. biome suitability and habitat overlays;
9. causal validation and incremental dependency invalidation;
10. synchronized natural-geography and fantasy-atlas presentations.

Use deterministic algorithms and tested GIS/raster primitives where appropriate. Do not build a bespoke full climate simulator or introduce a distributed geospatial service. Keep the model local, explainable, and replaceable behind domain-level interfaces. Exact library/engine selection remains a separate ME-07 technology evaluation and must not be frozen by this logical model.

## 14. Acceptance criteria

1. Climate outputs have explicit causal inputs and assumptions.
2. Temperature and precipitation are not assigned as arbitrary independent labels.
3. Elevation, latitude, ocean influence, wind, moisture transport and mountain barriers affect climate where relevant.
4. Rain shadows require a moisture-bearing wind and an appropriate barrier relationship.
5. Rivers derive from drainage and water-balance logic, not settlement placement.
6. Climate classes and biomes remain derived layers, separate from base terrain and authored story rules.
7. The system reports model resolution and does not claim unsupported scientific precision.
8. Explicit creator facts and exceptions remain protected and traceable.
9. Local edits trigger targeted recomputation and validation.
10. Results remain editable spatial data and can be presented in both physical and fantasy-atlas styles.

## 15. Decisions and deferred items

### Accepted baseline
- Use Earth science as the theoretical basis for climate and natural geography.
- Explain cold, heat, precipitation, aridity, water and biome outcomes through causal geographic factors.
- Use Earth-like planetary defaults only when the creator has not specified otherwise; record assumptions.
- Keep V1 physically informed and deterministic rather than claiming full numerical climate simulation.
- Derive rivers from terrain, watersheds and water balance, not from settlement connectivity.
- Separate physical fields, semantic geographic objects, climate/biome derived layers, creator world rules and cartographic styling.

### Deferred technical choices
- Exact raster resolution, tiling and coordinate reference strategy.
- Exact climate classification scheme and threshold tables.
- Selected GIS/raster/geometry libraries and renderer integration.
- Numerical parameterization and calibration datasets.
- Ocean-current approximation details and seasonal wind model fidelity.
- Detailed groundwater, soil, snowpack and flood simulation scope.