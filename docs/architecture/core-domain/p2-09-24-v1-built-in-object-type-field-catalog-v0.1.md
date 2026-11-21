# World View P2-09-24 — V1 Built-in Object Type Field and Capability Catalog V0.1

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended specialist descriptor baseline; not an accepted ontology freeze or implementation authorization
- Depends on: P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-21 V0.1; P2-09-22 V0.2; ME-02/ME-03 domain contracts

## 1. Purpose

Turn the ten proposed V1 Object Type seeds into bounded, reviewable descriptor field sets. This catalog distinguishes:
- **Definition attributes**: current shared/base facts owned by the Object definition.
- **Initial State attributes**: time-zero membership and initial runtime facts for one Map.
- **Runtime attributes**: historically mutable facts changed only through typed Commands.
- **Geometry roles**: formal spatial components, each with stable component identity and immutable Asset payload.
- **Relationships**: explicit typed edges; never inferred solely from geometry coincidence.

The field sets below are deliberately small. They are not an invitation to create an unrestricted ontology or arbitrary JSON attributes.

## 2. Common descriptor requirements

Every descriptor must additionally declare stable `type_key`, `schema_version`, `semantics_id`, identity allocation policy, lifecycle policy, allowed Command set, Asset formats, relationship endpoint matrix, and compatibility policy.

Every attribute requires a stable key, primitive/typed value, unit where relevant, scope, mutability, validation, default/requiredness, and compatibility behavior. The following catalog gives the initial semantic inventory, not the final serialized schema.

| Type key | Geometry roles and cardinality | Definition attributes | Initial State / runtime candidates | Validation constraints |
|---|---|---|---|---|
| `landmass` | `extent` 1..N; Polygon/MultiPolygon | `canonical_name` (string), `landform_class` (enum: continent/island/archipelago/other) | Initial: Map membership and placement only; runtime: none in base V1 | Valid area geometry; multipart allowed; no automatic ocean Object or political relation; do not split identity for islands in one authored archipelago |
| `mountain_range` | `extent` 0..N Area; `ridge` 0..N Line/Curve; at least one total | `canonical_name` (string), `range_class` (enum: range/massif/system/other) | Initial: membership; runtime: none in base V1 | One semantic range may cross working scopes; peak membership is explicit relationship; geometry edits do not create peaks |
| `mountain_peak` | `location` exactly 1 Point | `canonical_name` (string), `elevation_value` (decimal), `elevation_unit` (enum), `vertical_reference_id` (typed reference) | Runtime candidate: `elevation_value` only if historical elevation is required | Elevation must include unit and vertical reference; renderer Z is not elevation; no implicit range relationship |
| `river` | `centerline` 1..N Line/Curve; optional `reach` 0..N | `canonical_name` (string), `river_class` (bounded enum) | Runtime candidates: `flow_condition` deferred; no generic simulation state | Direction is not inferred from line vertex order; explicit `hydrology.flows_to`; tributaries are separate Objects only when independently meaningful |
| `lake` | `shoreline` 1..N Polygon/MultiPolygon | `canonical_name` (string), `water_body_class` (bounded enum) | Runtime candidates: water level / seasonal state deferred | Water-level datum and shoreline temporal model required before enabling historical level changes; islands/multipart subject to type semantics |
| `terrain_region` | `extent` 1..N Polygon/MultiPolygon | `canonical_name` optional; `terrain_class` required enum; optional `source_dataset_id` Asset ref | Runtime candidates: none in base V1 | Classification is bounded; suitability/climate/slope are derived analysis unless explicitly adopted; raster cells are not Objects |
| `settlement` | `anchor` exactly 1 Point; optional `extent` 0..1 Polygon/MultiPolygon | `canonical_name` (string), `settlement_class` (enum: village/town/city/capital/other) | Runtime candidates: `population_estimate` (non-negative integer with provenance), `condition_code` (bounded enum) | Anchor and extent are complementary; population must be time-scoped and sourced; political status is a relationship, not a static definition |
| `road` | `centerline` 1..N Line/Curve | `canonical_name` optional, `road_class` (bounded enum) | Runtime candidate: `condition_code` (bounded enum) | Connectivity requires explicit `network.connects_to`; snapping/touching is insufficient; movement routes are a separate Unit contract |
| `political_region` | `boundary` exactly 1..N Polygon/MultiPolygon | `canonical_name` (string), `region_class` (bounded enum) | Runtime candidate: no control/sovereignty scalar in base V1 | Control, sovereignty, membership and succession require temporal relationships / ME-03; containment does not imply ownership |
| `point_of_interest` | `location` exactly 1 Point | `canonical_name` (string), `poi_category` (bounded enum) | Runtime candidate: none in base V1 | Not a fallback for Character, Species, Culture, Event or unspatialized concepts; type conversion requires explicit migration |

## 3. Attribute type policy

- Names are Unicode strings with normalization defined by the application, but display aliases/localizations are not identity.
- Enumerations use stable codes. Adding an enum value is a schema compatibility change; removing/reinterpreting a value requires migration.
- Elevation and population are typed values with explicit units/reference/provenance. No unitless numeric attributes.
- Asset references are immutable Core Asset IDs plus content hash, never paths or mutable filenames.
- Relationship-backed facts must not be duplicated as scalar attributes.
- Derived/presentation-only values are not persisted as authored semantic attributes.
- Runtime attributes remain minimal in V1. A candidate listed above is not enabled until its temporal semantics and command validation are specified.

## 4. Geometry and relationship matrix

| Object Type | Geometry minimum | Relevant V1 relationships | Explicit non-implications |
|---|---|---|---|
| Landmass | One extent | `spatial.contains`, `spatial.adjacent_to` | Coastline contact does not imply adjacency unless confirmed |
| Mountain Range | One extent or ridge | `structural.part_of`, `spatial.contains` | Peak proximity does not imply membership |
| Mountain Peak | One location | `structural.part_of` | Elevation similarity does not imply same range |
| River | One centerline | `hydrology.flows_to`, optionally `network.connects_to` | Digitization direction does not imply flow direction |
| Lake | One shoreline | `hydrology.flows_to` (where source/target matrix permits) | River geometry intersection does not imply inlet/outlet |
| Terrain Region | One extent | `spatial.contains`, `spatial.adjacent_to` | Raster overlap does not imply semantic containment |
| Settlement | One anchor | `spatial.contains`, `network.connects_to` | Road snapping does not imply access/connectivity |
| Road | One centerline | `network.connects_to`, `spatial.adjacent_to` | Geometric touch does not imply network connection |
| Political Region | One boundary | `spatial.contains`, `spatial.adjacent_to` | Containment does not imply political control |
| Point of Interest | One location | `spatial.contains`, `spatial.adjacent_to` | Being inside a region does not imply membership/ownership |

This matrix is a candidate participation map. P2-09-22's exact endpoint type matrix, cardinality, cycle and temporal rules remain the executable relationship authority.

## 5. Deliberately deferred fields and types

- Lake water-level simulation, river flow simulation, terrain climate/suitability, road traffic/capacity, political sovereignty/control scalar fields.
- Character, Species, Population, Culture, Faction, Unit, Building and Watershed as new formal Object Types.
- Universal arbitrary attributes, custom executable Object Types and user-authored validation scripts.

## 6. Release gates

Before any type is enabled for formal authoring, its descriptor must have:
1. complete field schema and requiredness;
2. exact geometry role cardinality and geometry family matrix;
3. exact relationship endpoint participation;
4. lifecycle and Command capability rules;
5. deterministic validation and compatibility identifier;
6. representative invalid/valid fixtures.

No runtime candidate field is enabled merely because it appears in this document.
