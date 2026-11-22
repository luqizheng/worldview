# World View — ME-02 Domain Inventory & Core Domain Mapping

- Version: 0.1
- Date: 2026-11-21
- Status: **Domain Inventory Working Baseline** — specialist mapping recommendation; not an accepted Object Type/Relationship Type catalog or physical field schema
- Parent: [ME-02 Terrain & Natural Geography Editor](world-view-me-02-terrain-and-natural-geography-editor-v0.1.md)
- Depends on: Core Domain P2-09-18 V0.2, P2-09-19 V0.2, P2-09-20 V0.2, P2-09-21 V0.1, P2-09-22 V0.2, P2-09-24 V0.1, P2-09-25 V0.1; Natural Geography, Climate & Hydrology Model V1.0; ME-01-05/07

## 1. Purpose and recommendation

This inventory maps the content of the Terrain & Natural Geography Editor to the current Core Domain seed catalogs and separates formal semantic Objects from continuous physical fields, derived analytical outputs, World Knowledge and presentation.

**Recommendation:** ME-02 should not introduce a broad new family of formal Object Types in its first V1 slice. The existing seed types cover the core authored semantic features required for an initial natural-geography workflow:

- `landmass`
- `mountain_range`
- `mountain_peak`
- `river`
- `lake`
- `terrain_region`

The editor must use physical fields and derived analytical outputs for continuous or computational content. It should not model elevation, raster cells, every detected ridge, every watershed or every climate pixel as semantic Objects.

This is a mapping baseline for specialist work. P2-09-21 and P2-09-24 remain recommended catalogs; this document does not promote them to frozen ontology or executable descriptors.

## 2. Classification rules

Every natural-geography concept is assigned to one primary authority category:

| Category | Meaning | Formal persistence / identity |
|---|---|---|
| Semantic Object | Independently meaningful world feature with stable identity | Core Domain Object Type, Geometry Components, typed attributes and relationships |
| Physical field / source dataset | Continuous or sampled physical quantity, mask or measured/imported data | Immutable Asset / field dataset contract; exact field owner and tiling remain open |
| Derived analytical result | Reproducible result calculated from pinned inputs and method | Derived Asset/cache or Candidate; never a second authority |
| World Knowledge | Story, creator intent, ecological preference, historical explanation or unresolved concept | AI/World Knowledge assertion and evidence; not automatically a Map Object |
| Presentation | Visual style, symbol, contour, hillshade, label, overlay | Presentation configuration or renderer-derived output; not formal World History |
| Temporary interaction | Brush preview, selection, sketch, snap candidate, in-progress geometry | Editor/Edit Session transient state |

A concept may have more than one representation, but each representation must retain its category and source relationship. For example, a creator-authored Mountain Range Object can be used as an input to a derived ridge/wind-exposure analysis; the analysis does not replace the Object.

## 3. Semantic Object mapping

### 3.1 Existing V1 seed types

| Type key | Natural-geography responsibility | Geometry / attribute mapping | Enabled interaction intent |
|---|---|---|---|
| `landmass` | Continents, islands, archipelagos as creator-defined semantic units | `extent` 1..N Area; `canonical_name`; `landform_class` | Create, adjust/refine, add/remove component under descriptor rules; initial Map membership |
| `mountain_range` | Named/meaningful mountain system | `extent` 0..N Area; `ridge` 0..N Line/Curve; at least one total; name and range class | Create, adjust/refine, add/remove declared geometry component; one identity across scopes |
| `mountain_peak` | Independently meaningful summit | `location` exactly 1 Point; elevation value + unit + vertical reference | Create/place; adjust location; typed elevation only with valid reference |
| `river` | Named/meaningful river or river system | `centerline` 1..N Line/Curve; optional `reach`; name and bounded river class | Create/refine centerline; add/remove declared components; establish explicit flow relationships |
| `lake` | Inland water body | `shoreline` 1..N Area; name and water-body class | Create/refine shoreline; explicit River→Lake flow relation where valid |
| `terrain_region` | Classified terrain/land-cover/ecological area | `extent` 1..N Area; optional name; required bounded `terrain_class` | Create/adjust region boundary and registered classification |

The exact descriptor controls role cardinality, allowed geometry family, attribute mutability, asset formats and operation availability. The UI must not infer permissions from the table alone.

### 3.2 Concepts not promoted to Object Types in this V1 inventory

| Concept | Recommended V1 home | Why not a new Object Type now |
|---|---|---|
| Continent / Island / Archipelago | Landmass `landform_class` plus multipart geometry | Difference is a bounded classification/semantic grouping, not necessarily a distinct type |
| Forest / Desert / Plain / Tundra / Wetland | TerrainRegion classification where appropriate | Avoid one Object Type per symbol/biome category |
| Coastline | Derived land/water boundary; optional supported geometry role only if Core Domain adopts it | Duplicating Landmass boundary creates competing geometry authority |
| Watershed / Drainage Basin | Derived polygon/analysis result | No current built-in Object Type or `water.drains_to` Relationship Type |
| River Reach | River Geometry Component role only if descriptor supports it | A reach is not automatically an independently meaningful River Object |
| Ridge / Crest | MountainRange geometry role or derived terrain feature | Avoid automatic Object identity for every extracted line |
| Plateau / Valley / Canyon / Escarpment | Derived classification or explicit candidate; TerrainRegion where its bounded classification is sufficient | Specialist geomorphology semantics and attributes are not yet specified |
| Glacier / Ice Sheet / Volcano | Explicit candidate or derived layer initially | Requires physical behavior, temporal semantics and Object Type validation |
| Ocean / Sea / Estuary / Delta | Land/water field and derived physical context; Candidate where needed | Formal Object Type and flow endpoint semantics are not in the current seed catalog |
| Biome / Climate Zone | Derived classification layer | Must retain causal variables and classification scheme; not a new Object by default |
| Soil Region | Derived or imported categorical field | Soil profile and temporal semantics not yet defined |
| Spring / Groundwater / Aquifer | Deferred candidate/physical field | Source/sink and subsurface semantics not modeled in current V1 seed set |
| Species / Population / Habitat | World Knowledge by default; suitability overlay for environmental fit | Preference/suitability is not actual spatial distribution |
| Wind / Ocean Current | Physical vector field or authored model assumption | Physical driver, not semantic Object by default |

A future Object Type proposal must demonstrate independent identity, creator-facing value, descriptor completeness, relationship/command needs and a reason it cannot be represented by an existing type, field or derived layer.

## 4. Physical field inventory

The following is a logical field inventory, not a final database schema or raster encoding decision.

| Field family | Core meaning | Value form / units | Source or derivation | Initial V1 role |
|---|---|---|---|---|
| Land/water mask | Land, ocean/water, inland water, unknown/outside | Categorical | Creator geometry, imported data or derived rasterization | Foundational input |
| Elevation | Surface elevation | Continuous; explicit length unit and vertical reference | Creator control inputs, imported elevation or deterministic interpolation | Foundational input |
| Slope | Local gradient magnitude | Continuous; angular or dimensionless convention declared | Derived from elevation | Diagnostic/derived |
| Aspect | Direction of slope orientation | Circular value with declared angular convention | Derived from elevation | Derived input to exposure |
| Hillshade | Illumination-like relief presentation | Display intensity | Derived from elevation + display light parameters | Presentation only |
| Contours | Equal-elevation lines | Elevation interval and unit | Derived from elevation | Presentation/inspection |
| Temperature | Seasonal/annual estimate | Temperature unit and period semantics | Latitude, season, elevation, maritime/current influence, circulation | Derived physical estimate |
| Precipitation | Seasonal/annual estimate | Length/time or declared relative class | Moisture source, winds, uplift, season, temperature | Derived physical estimate |
| Wind | Prevailing direction / broad strength class | Direction convention + bounded class | Planet parameters, circulation assumptions, land/sea and mountain effects | Derived or explicitly authored assumption |
| Moisture/aridity | Water availability / deficit indicator | Dimensionless/index or declared class | Precipitation, evaporation, soil/water assumptions | Derived |
| Soil/infiltration | Broad permeability / water retention | Bounded categorical or numeric class | Imported/creator-authored or derived assumption | Optional input |
| Snow/ice | Seasonal/persistent snow or ice indicator | Categorical/continuous coverage | Temperature, precipitation, season, elevation | Derived; limited V1 |
| Biome suitability | Environmental suitability by registered biome class | Categorical or score with method definition | Climate, water, soil, elevation and ecology constraints | Derived overlay, not actual species occupancy |

Field metadata must pin spatial reference, axis/unit/dimension, coverage, resolution, no-data semantics, source Asset hashes, algorithm/version/parameters, and uncertainty/quality where available.

### 4.1 Field persistence decision

Do not add one row or Object per raster cell. Do not embed large raster payloads inside Command JSON. Use immutable Asset references for source/adopted data and rebuildable derived caches for calculation/render outputs.

The following remain open:
- whether an authored elevation field is owned by a Map-level physical-field aggregate or another Core Domain typed owner;
- how field identity, coverage, tiling, resolution and no-data semantics are represented;
- how field updates are represented in Initial State versus historical authoring;
- whether a small number of field operations require dedicated typed Commands or only Initial State/asset-authoring services;
- cache retention and invalidation policy.

These are ME-05/ME-07 gates. ME-02 must not invent a parallel raster database or claim field persistence is already solved by generic Asset storage.

## 5. Derived analytical result inventory

| Derived result | Inputs | Output | Formal status |
|---|---|---|---|
| Coastline boundary | Land/water mask, map-edge and wrap semantics | Line/Curve geometry or render path | Derived; not a new Object by default |
| Land connectivity/island candidates | Land/water mask, chosen connectivity rule, resolution | Connected-region candidates | Analysis only; creator decides Landmass identity |
| Slope/aspect | Elevation field, neighborhood/algorithm parameters | Continuous fields | Derived |
| Hillshade/contours | Elevation, display/interval parameters | Render/inspection output | Presentation-derived |
| Ridge/peak candidates | Elevation, scale, detection method | Candidate lines/points | Candidate; never auto-create MountainRange/Peak Objects |
| Watershed divides/basins | Elevation, depression policy, drainage method | Flow direction, accumulation, basin polygons | Derived; no formal Watershed Object in V1 |
| River channel candidates | Flow accumulation, basin context, water balance, threshold parameters | Candidate centerlines/network | Candidate; formal River Objects only after explicit adoption |
| River flow network diagnostics | River geometry, candidate/confirmed relationships, terrain context | Continuity, uphill, cycle and outlet findings | Validation output |
| Temperature/precipitation | Planet/world frame, season, elevation, winds, ocean influence and moisture assumptions | Seasonal/annual fields | Derived estimate |
| Climate class | Temperature/precipitation seasonal summaries and selected scheme | Categorical layer | Derived classification |
| Biome suitability | Climate, water, soil, elevation, creator/ecology constraints | Suitability classes/scores | Derived overlay |
| Cross-layer consistency | Inputs, constraints, existing Objects and derived outputs | Diagnostics/impact set | Validation output |

Each derived result must state whether it is complete, partial, provisional, stale or invalid; coverage and resolution must be explicit.

## 6. Relationship mapping and current limitations

The current recommended Relationship Type seed catalog provides:

| Relationship | Endpoint / semantics | ME-02 usage |
|---|---|---|
| `hydrology.flows_to` | Directed River→River and River→Lake; upstream to downstream | Primary formal hydrological connectivity when its descriptor and validation gates are enabled |
| `structural.part_of` | MountainPeak→MountainRange among the relevant seed pairs | Explicit peak membership; proximity alone is insufficient |
| `spatial.contains` | Only registered endpoint pairs, including Landmass→MountainRange | Explicit semantic containment; geometric containment alone does not create it |
| `spatial.adjacent_to` | Registered symmetric pairs, including Landmass↔Landmass | Only where adjacency has an explicit meaningful definition; not a generic geometric predicate |

Important limitations:
- `water.drains_to` for Watershed is explicitly deferred because Watershed is not a seed Object Type.
- Ocean/outlet/estuary endpoints for `hydrology.flows_to` are deferred.
- River geometry direction does not determine flow direction.
- A geometry intersection, snap or shared boundary does not automatically establish a relationship.
- Distributary/delta network policy must be settled with ME-02 hydrology validation before enabling complex network cases.
- A derived watershed graph may be used internally for analysis without creating formal Core Domain relationship rows.

Do not invent a generic graph edge or use `network.connects_to` as a substitute for hydrology semantics.

## 7. Command and authoring path mapping

### 7.1 Current operation families

The current P2-09-15 V0.5 catalog includes bounded operations relevant to ME-02:
- `core.object.create` and the applicable History-local presence operations;
- `core.object.attribute.change` for Object-Type-declared mutable attributes;
- `core.geometry.adjust_component`;
- `core.geometry.refine_component`;
- `core.geometry.add_component`;
- `core.geometry.remove_component`;
- `core.relationship.establish`;
- `core.relationship.end`.

Exact descriptor payload and operation eligibility must be checked per Object Type, History and authoring target. The catalog does not make every operation available for every type or at time zero.

### 7.2 Deferred operations

- `core.geometry.split_component` and `core.geometry.merge_components` remain deferred.
- No generic raster-paint CRUD Command is implied by the vector geometry catalog.
- Terrain evolution/erosion/hydrology simulation Commands are deferred.
- No `core.watershed.create`, `core.climate_zone.create`, `core.biome.create` or generic field-patch Command is assumed.
- No flow relationship to unsupported Ocean/Estuary endpoints may be persisted.

### 7.3 Three authoring targets

Every formal operation must route to one of:
1. Map Initial State baseline authoring;
2. Map/Branch historical Command;
3. Project Shared Object current-definition authoring.

A field or geometry edit at time zero must not be represented as a post-zero Command. A shared Object definition edit must not be mistaken for a Map-local change. Derived calculations do not become formal history unless the creator explicitly adopts an eligible result through the correct service.

## 8. V1 support matrix

| Capability | Design support | Formal V1 status | Gate |
|---|---|---|---|
| Landmass geometry | Yes | Existing seed Object; exact descriptor adoption required | Core Domain descriptor |
| Mountain Range geometry | Yes | Existing seed Object | Geometry role/cardinality and commands |
| Mountain Peak point/elevation | Yes | Existing seed Object; elevation typed with unit/reference | Vertical reference/precision validation |
| River centerline | Yes | Existing seed Object | Geometry role and operation descriptor |
| Lake shoreline | Yes | Existing seed Object | Geometry role and operation descriptor |
| Terrain Region classification | Yes | Existing seed Object | Bounded enum/classification semantics |
| Elevation field | Yes | Field Asset persistence not yet specified | ME-05/ME-07 |
| Land/water mask | Yes | Field/geometry ownership needs contract | ME-02-B / ME-05 |
| Coastline derivation | Yes | Derived only by default | Map-edge/field contract |
| Watershed analysis | Yes | Derived only | Depression/flow algorithm policy |
| Formal Watershed Object | Deferred | Not in seed catalog | Explicit Object Type proposal |
| River→River / River→Lake flow | Yes | Supported relationship seed subject to descriptor gates | ME-02-D |
| River→Ocean / River→Estuary | Deferred | Unsupported endpoint matrix | Future Object/relationship contract |
| Climate/precipitation estimates | Yes | Derived field, not formal Object | ME-02-E calculation contract |
| Climate classification | Yes | Derived layer | Classification scheme/thresholds |
| Biome suitability | Yes | Derived overlay | Vocabulary and provenance |
| Actual species distribution | Deferred | World Knowledge unless explicitly spatialized later | Ecology/World Knowledge domain |
| Formal geometry split/merge | Deferred | Not enabled | Core Domain descriptor/identity rules |

## 9. Required ME-02 follow-up contracts

| ID | Gap | Required output | Owner |
|---|---|---|---|
| D-01 | Exact Object Type descriptor readiness for six natural-geography seed types | Type/role/attribute/command readiness matrix | Core Domain + ME-02 |
| D-02 | Land/water mask and elevation field persistence | Field dataset/Asset ownership, coverage, tile and update contract | Core Domain + Map Engine |
| D-03 | Field authoring versus Initial State/History semantics | Typed field-authoring service/operation contract | Core Domain |
| D-04 | Landmass/coastline/map-edge semantics | Land/water boundary and wrap/seam contract | Map Space + Map Engine |
| D-05 | Watershed has no formal Object/Relationship Type | Derived-only semantics and explicit future promotion criteria | ME-02 + Core Domain |
| D-06 | Hydrology flow endpoint and network policy | `hydrology.flows_to` validation, cycles/distributaries and unsupported outlet handling | ME-02 + Core Domain |
| D-07 | Elevation value precision and vertical references | Typed elevation unit/reference catalog and validation | Core Domain + Spatial Services |
| D-08 | TerrainRegion class vocabulary | Bounded initial terrain-class enum and compatibility policy | ME-02 + Core Domain |
| D-09 | Climate/biome output vocabulary | Versioned classification/suitability output contract | ME-02 |
| D-10 | Derived output freshness and reproducibility | Input fingerprint, algorithm version, parameters and invalidation contract | Calculation Services + Map Engine |

## 10. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| INV-01 | Creator asks for a mountain range | Propose/use MountainRange Object; do not create a generic Feature |
| INV-02 | Elevation brush paints a broad highland | Updates a field preview/Asset path, not one Object per cell |
| INV-03 | Algorithm detects 200 local peaks | Return candidates/analysis; do not allocate 200 formal MountainPeak identities |
| INV-04 | Land mask is clipped by map edge | Do not infer the edge is coastline |
| INV-05 | A watershed crosses three Working Scopes | One derived basin result with cross-scope coverage; no duplicate scope-local Objects |
| INV-06 | River line is digitized downstream-to-upstream | Flow relation remains independently explicit |
| INV-07 | River intersects a Lake shoreline | Do not create flows_to without supported/accepted endpoint semantics |
| INV-08 | AI proposes a climate zone conflicting with confirmed canon | Preserve canon; show conflict and alternative explanations |
| INV-09 | A forest suitability overlay is generated | Label as suitability, not actual forest or species occupancy |
| INV-10 | Elevation source Asset changes | Invalidate only dependent derived outputs and mark old results stale |
| INV-11 | User changes hillshade lighting | No terrain/climate recalculation and no formal World History change |
| INV-12 | Unsupported Watershed Object write is requested | Reject formal write; retain derived analysis or propose a future domain contract |

## 11. Disposition

The current Core Domain seed set is sufficient for the first natural-geography semantic Object workflows. The most important missing contract is **not another collection of Object Types**; it is the typed ownership and authoring lifecycle for continuous physical fields, especially elevation and land/water data.

The next ME-02 design work should therefore proceed in this order:
1. Close the six seed Object descriptor/operation mappings without expanding the ontology.
2. Define the land/water and elevation field ownership/Asset contract.
3. Define terrain feature workflows and validation.
4. Define hydrology network semantics against the enabled Relationship Type matrix.
5. Define climate/biome derived field contracts and reproducibility.

No schema, algorithm library, raster format, new Object Type or new Command is frozen by this inventory.
