# World View P2-09-21 — V1 Built-in Object Type Seed Catalog V0.1

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended initial built-in Object Type seed set — bounded first-workflow catalog; specialist ME-02/ME-03/ME-04 types remain deferred
- Depends on: P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.1; ME-01-02 V0.1; ME-01-04 V0.1; ME-01-05 V0.1; ME-01-07 V0.1

## 1. Purpose

Select a small, useful initial set of built-in Core Domain Object Types for the first complete World View authoring workflows. The goal is to make the typed registry concrete enough to guide Object Type descriptors, geometry roles, relationship capabilities and Command payload validation without prematurely designing a universal ontology.

This is a recommended seed catalog, not an exhaustive taxonomy and not an implementation authorization. Each type remains subject to its own descriptor schema and validation rules before it is enabled in a released workflow.

## 2. Selection principles

1. Seed only semantic concepts required for initial geography, terrain, settlements, roads and map annotation workflows.
2. Do not create an Object Type solely because an item has a different symbol, color, layer or visual style.
3. Do not create generic `Feature`, `Entity`, `Place` or arbitrary user-defined Object Types as a fallback for unknown concepts.
4. Distinguish semantic Object Types from working scopes, map layers, geometry roles, raster datasets, Events and World Knowledge entities.
5. One Object may be represented at World, Region, Locality and Site scopes; those scopes are not separate Object Types or separate Map databases.
6. Geometry family and Object Type are independent dimensions. Two types may share geometry families while having different semantics and validation.
7. Stable type keys and semantic compatibility IDs are not localized display names.
8. The initial registry is statically compiled into Core Domain. Project rows configure display/allocation metadata but cannot define executable semantics.
9. Specialist types enter only when the corresponding ME-02/ME-03/ME-04 domain workflows and invariants are ready.

## 3. Recommended initial seed set

<text>
</text>

| Stable type key | Default display name | Primary geometry capability | Initial purpose | Initial status |
|---|---|---|---|---|
| `landmass` | Landmass | Area, multipart area | Continents, islands and archipelagos as meaningful large-scale land entities | Include |
| `mountain_range` | Mountain Range | Line/curve and/or area, multipart | Major mountain systems/ranges with ridge and extent components | Include |
| `mountain_peak` | Mountain Peak | Point; optional elevation/reference Asset | Independently meaningful named peaks | Include |
| `river` | River | Line/curve, multipart | Rivers and named river systems; flow/network semantics are explicit relationships | Include |
| `lake` | Lake | Area, multipart | Lakes and inland water bodies | Include |
| `terrain_region` | Terrain Region | Area, multipart | Typed terrain/ecological/land-cover region such as forest, desert, wetland or plain | Include |
| `settlement` | Settlement | Point anchor plus optional area extent | Villages, towns, cities and capitals | Include |
| `road` | Road | Line/curve, multipart | Roads and transport links between places | Include |
| `political_region` | Political Region | Area, multipart | Kingdoms, states, provinces and other political/administrative regions | Include |
| `point_of_interest` | Point of Interest | Point | Named map-relevant places/features that do not warrant a more specific Object Type | Include |

The initial seed set contains ten types. The display names and stable keys are recommendations; the keys become reserved only when adopted by a reviewed Core Domain release.

## 4. Type-specific semantic boundaries

### 4.1 Landmass

Purpose: represent one independently meaningful land entity at world or regional scale.

- Geometry role: `extent`; optional component roles such as `coastline` are geometry roles, not separate Object Types.
- Multipart geometry may represent islands that the creator considers one semantic archipelago or landmass group.
- A coastline simplification or display LOD is derived geometry, not a second Landmass.
- Ocean/water extent is not automatically represented by a Landmass Object.
- The type does not automatically create political, ecological or watershed relationships.

### 4.2 Mountain Range

Purpose: represent a named or otherwise independently meaningful mountain system/range.

- Geometry roles: `extent`, `ridge`, and optionally `crest` where the type descriptor supports them.
- Multiple ridge/extent components may belong to one Object.
- A range may cross multiple working Regions; do not duplicate its identity at every scale.
- Peaks may be separate `mountain_peak` Objects when they are independently meaningful.
- Splitting a ridge geometry does not split the Mountain Range identity.

### 4.3 Mountain Peak

Purpose: represent an independently meaningful peak or summit.

- Primary geometry role: `location`.
- Elevation is a typed attribute with explicit units and reference context; do not treat viewport height or renderer z as formal elevation.
- A peak may be related to a Mountain Range through a typed structural/spatial relationship where explicitly confirmed.
- A generated peak candidate remains staging data until accepted.

### 4.4 River

Purpose: represent a named river or river system whose identity is meaningful independently of its current line geometry.

- Geometry roles: `centerline`; optional `reach` components when the descriptor explicitly supports them.
- Tributaries may be separate River Objects when independently named/meaningful, connected by explicit hydrological relationships.
- Flow direction is a typed relationship/descriptor fact, not inferred solely from line digitization direction.
- A river may gain more detailed reaches without changing its semantic identity.
- Geometry splitting does not create tributary Objects.

### 4.5 Lake

Purpose: represent an independently meaningful lake or inland water body.

- Geometry role: `shoreline` / `extent`.
- Multipart geometry may represent islands or disconnected components only where the Object Type's semantics permit them.
- A lake's connection to rivers, drainage or watersheds is represented through explicit relationship types, not arbitrary attributes.
- Water-level variation and shoreline change require a future specialist runtime-state contract; do not invent simulation behavior in the base type.

### 4.6 Terrain Region

Purpose: represent a spatially bounded region with a declared terrain/ecological/land-cover classification.

- Geometry role: `extent`.
- Initial classification is a typed enum/category governed by the Object Type schema; examples include forest, desert, plain, wetland, tundra or other registered classes.
- The type may reference immutable raster/elevation assets as evidence or source data, but raster cells do not become Objects.
- Derived suitability, climate, slope or ecological analysis is not automatically a formal Object attribute or Command.
- A region may cross working-scope boundaries; do not tile it into new semantic Objects just to match viewport or analysis chunks.

### 4.7 Settlement

Purpose: represent a named human/inhabited place.

- Geometry roles: `anchor` and optional `extent`.
- The anchor may be a point while the extent is an area; these roles are not competing geometries.
- Settlement hierarchy (village/town/city/capital) is a typed semantic classification, not a separate Object Type for each display scale.
- Population and political status are not automatically static definition attributes; if historically variable, they belong in History-local runtime state or typed relationships.
- Buildings and districts are separate Objects only when independently meaningful and explicitly created.

### 4.8 Road

Purpose: represent a road as a named or otherwise independently meaningful transport feature.

- Geometry role: `centerline`; multipart only where the Road descriptor explicitly allows it.
- Network connectivity is a formal typed relationship when the domain requires it; endpoint coincidence or snapping is not enough.
- A road route is not the same thing as a Unit's movement route. Unit movement remains deferred until its domain contract is complete.
- Road condition/availability may be History-local runtime state; a visual style change is not.

### 4.9 Political Region

Purpose: represent a political or administrative territory.

- Geometry role: `boundary` / `extent`.
- Political control, sovereignty, membership or succession across time should use typed temporal relationships or specialist Commands, not a mutable global definition field.
- A Political Region may contain Settlements but does not own their identity.
- Administrative subdivisions may be separate Political Region Objects only when the creator's world model requires them.

### 4.10 Point of Interest

Purpose: represent a named map-relevant location or feature that does not warrant a more specific semantic type.

- Primary geometry role: `location`.
- It is a formal spatial Object, not a generic fallback for characters, species, cultures, Events or unspatialized story concepts.
- Category is a bounded typed classification, not an arbitrary ontology.
- If a POI later gains a more specific semantic type, conversion requires an explicit controlled migration/re-authoring operation; do not silently change its stable type key.

## 5. Explicitly not in the initial seed set

| Concept | Why it is not a V1 seed type yet | Future home |
|---|---|---|
| Character / Person | World Knowledge entity by default; does not automatically become a spatial Object | World Knowledge; Unit/Character spatialization only when explicitly modeled |
| Species / Population / Culture / Faction | Narrative/knowledge concepts; habitat preference or membership does not automatically assert territory | World Knowledge and explicit spatial/semantic relationships |
| Event | Separate historical/narrative record, not a semantic Object Type or Command | Core Domain Event |
| Watershed | May be a derived hydrological analysis region or an explicit authored concept; requires bounded ME-02 semantics | ME-02 Terrain & Natural Geography |
| Climate Zone | Often a derived/analytical region tied to global drivers; requires deterministic model and provenance | ME-02 |
| Ecological Zone | Can be represented initially by TerrainRegion classification where sufficient; specialist ecology semantics deferred | ME-02 |
| Unit / Army / Group | Requires movement, route, duration, pause/resume, obstacles and deterministic execution semantics | Later Unit/Movement domain contract |
| Building / Floor / Room / Interior Space | Requires site association, local coordinate frames and structured interior geometry | ME-04 |
| Cave System | Requires underground/vertical topology and cross-site extent rules | ME-04 or specialist natural-geography design |
| District | Can be represented as a Political Region or Settlement substructure only after explicit semantic rules; not created by clipping | ME-03 |
| Continent / Island as separate types | Use Landmass with typed subtype/classification where the distinction is only a bounded category | Landmass descriptor |
| Forest / Desert / Plain as separate types | Use TerrainRegion with a bounded terrain classification | TerrainRegion descriptor |
| Coastline | Geometry role/component of Landmass or other relevant Object, not automatically a separate Object | Geometry role contract |

This list prevents World Knowledge entities and analytical outputs from becoming formal Objects by accidental type promotion.

## 6. Geometry and relationship capability summary

| Object Type | Required geometry role | Optional roles | Relationship families likely required |
|---|---|---|---|
| Landmass | extent | coastline, island_component | contains/within, adjacent_to |
| Mountain Range | extent or ridge | ridge, crest | part_of, adjacent_to, flows_to (as source context, not automatic) |
| Mountain Peak | location | summit_reference | part_of / located_on |
| River | centerline | reach | flows_to, connects_to |
| Lake | shoreline/extent | island_component | flows_to/drains_to, adjacent_to |
| Terrain Region | extent | none initially | contains/within, adjacent_to, intersects as derived predicate |
| Settlement | anchor | extent | within, connects_to, controlled_by |
| Road | centerline | none initially | connects_to, within |
| Political Region | boundary/extent | none initially | contains/within, controlled_by, adjacent_to |
| Point of Interest | location | none initially | within, near/adjacent_to, part_of where meaningful |

The table lists likely capabilities, not automatic relationship creation. Each Relationship Type must be separately registered and validated under P2-09-20.

## 7. Project seed and compatibility policy

- On Project creation, Core Domain seeds the stable built-in Object Type rows required by the application version.
- Seeding is idempotent by stable `type_key`; a second startup must not create duplicates.
- Each Object identity pins its Object Type descriptor schema version and semantics identifier when Bound.
- Project display-name changes do not change stable type keys.
- A type may become non-allocatable/deprecated for new Objects while remaining resolvable for existing Objects and historical replay.
- Removing or replacing a built-in type requires a controlled migration and cannot invalidate historical Object IDs or Commands.
- User-created arbitrary executable Object Types are not supported in V1.
- Project-level ID allocation format changes do not rewrite already reserved `allocated_id_value` values.

## 8. First workflow mapping

The recommended seed set supports these first complete editor workflows:

1. **World skeleton:** Landmass, Mountain Range, Mountain Peak, River, Lake.
2. **Regional geography:** Terrain Region, River/Lake networks and explicit spatial/flow relationships.
3. **Settlement planning:** Settlement, Road, Point of Interest and explicit containment/connectivity relationships.
4. **Political geography:** Political Region with time-varying control relationships.
5. **Progressive refinement:** existing Objects gain geometry components and detail without automatic identity splitting or scale-specific duplication.

World/Region/Locality/Site remain working scopes, not Object Type categories.

## 9. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| OTS-01 | Project bootstraps built-in Object Types twice | One stable row per type key; no duplicates |
| OTS-02 | Unknown story Character mentioned during Guided Initialization | World Knowledge entity only; no automatic map Object |
| OTS-03 | Forest classification created | TerrainRegion with typed classification, not a new Forest type |
| OTS-04 | Continent and island group represented | Landmass type with permitted subtype/multipart geometry |
| OTS-05 | Mountain ridge geometry split | Same Mountain Range identity; no automatic new Objects |
| OTS-06 | River line direction reversed | Does not silently reverse formal flow relationship |
| OTS-07 | Road endpoints snapped | No automatic connects_to relationship |
| OTS-08 | Settlement shown at World and Locality scales | One Object identity with derived scale-appropriate representations |
| OTS-09 | Political control changes over Historical Time | History-local relationship change; Project-level definition unchanged |
| OTS-10 | User changes POI symbol/color | Presentation-only change; no formal Command |
| OTS-11 | Built-in type is deprecated | Existing Objects remain readable/replayable; new allocation may be disabled |
| OTS-12 | User submits arbitrary custom executable Object Type | Rejected/not supported in V1 |

## 10. Remaining design gates

1. Confirm the first concrete typed attributes and geometry-role cardinalities for each seed type.
2. Select the first enabled Relationship Type subset from P2-09-20 for these endpoint types.
3. Define the exact ID display grammar and per-type allocation format policy without changing existing allocated values.
4. Coordinate Landmass/Mountain Range/River/Lake/Terrain Region validation with ME-02.
5. Coordinate Settlement/Road/Political Region validation with ME-03.
6. Keep Building/Floor/Room/Cave and structured interior types deferred until ME-04.
7. Add specialist Unit types only after the movement/route/duration contract is complete.

## 11. Status

P2-09-21 V0.1 recommends a ten-type built-in seed set for the first complete World View authoring workflows and explicitly separates formal map Objects from World Knowledge entities, working scopes, derived analysis and presentation.

It is a bounded product/domain recommendation, not an exhaustive ontology, executable registry code, SQL seed migration, implementation authorization or claim that specialist ME-02/03/04 semantics are complete.
