# World View — ME-03-A Domain Inventory & Descriptor Reconciliation

- Version: 0.1
- Date: 2026-11-21
- Status: Domain inventory working baseline; recommendations and gaps only, no descriptor or schema change authorized
- Depends on: ME-03-00 V0.2; P2-09-18 V0.2; P2-09-20 V0.2; P2-09-21 V0.1; P2-09-22 V0.2; P2-09-24 V0.1; P2-09-26 V0.2; P2-09-27 V0.2; ME-02 V0.6; ME-05-01 through ME-05-06
- Scope: Current Settlement, Road and PoliticalRegion capabilities; urban domain concepts; exact registered relationship endpoint matrices; specialist gaps

## 1. Executive findings

The current Core Domain registry already provides a deliberately small foundation for settlement and urban geography. ME-03 should extend the product workflow around these types before proposing new semantic types.

Five findings govern all subsequent ME-03 work:

1. **Settlement is one Object Type, not five.** Village, town, city, capital and other are bounded values of `settlement_class`. Settlement has one required point `anchor` and an optional area `extent`.
2. **PoliticalRegion is a territory, not a governing actor.** Its `region_class` includes country, province, state, district and other. Its boundary does not imply control, sovereignty, ownership or jurisdiction.
3. **Current network connectivity is Road-to-Road only.** `network.connects_to` does not currently allow Road-to-Settlement. It represents explicit physical continuity at a junction, not access, route direction, permission or traffic behavior.
4. **Settlement hierarchy is not enabled.** The Settlement-to-Settlement pair for `structural.part_of` is explicitly deferred. Spatial containment is not a substitute for semantic hierarchy.
5. **Human land use is not represented by the current TerrainRegion type.** TerrainRegion is a bounded physical terrain/ecological/land-cover classification. Residential, commercial, industrial and agricultural use require a separate semantic analysis.

The machine-readable P2-09-26 and P2-09-27 registries are the precise current source for endpoint pairs and enabled descriptor shape. A broader seed-catalog candidate is not equivalent to an enabled descriptor.

## 2. Existing Object Type inventory

### 2.1 Settlement

Current descriptor:
- Stable key: `settlement`; schema version 2; semantics ID `world-view.object-type.settlement.v1`.
- Geometry: `anchor` exactly 1 Point; `extent` 0..1 Polygon/MultiPolygon.
- Definition: required `canonical_name`; bounded `settlement_class` = village / town / city / capital / other.
- Runtime candidates: `population_estimate` is disabled pending provenance and temporal contract; `condition_code` is disabled pending vocabulary.
- Invariants: anchor and extent are complementary; political status is not a scalar.
- Formal geometry edit capabilities are the shared adjust/refine/add/remove component operations under declared role/cardinality constraints.
- Runtime attribute mutation is not enabled by the current seed descriptor.

Implications:
- A Settlement can be represented as a known location before its detailed urban extent is authored.
- Anchor and extent are not competing geometries: the anchor locates the inhabited place; the extent describes its mapped footprint.
- A Settlement with no extent is not incomplete or invalid if its anchor is known.
- The classification does not encode population size, legal status, urban morphology or administrative rank beyond its bounded semantics.
- Population values must not be displayed as formal runtime facts until provenance, time scope and Commands are specified.

### 2.2 Road

Current descriptor:
- Stable key: `road`; schema version 2; semantics ID `world-view.object-type.road.v1`.
- Geometry: `centerline` 1..N LineString/MultiLineString.
- Definition: optional `canonical_name`; bounded `road_class` = trail / local / regional / major / highway / other.
- Runtime candidate: `condition_code` is disabled pending temporal contract.
- Invariant: snapping does not establish connectivity.
- Runtime attribute mutation is not enabled by the current seed descriptor.

Implications:
- A Road is a semantic transport feature with a stable identity, not simply a line drawn on the Canvas.
- A Road may have multiple centerline components only within its descriptor semantics; this does not by itself create a network junction.
- The current type does not encode traffic direction, travel cost, speed, legal access, capacity or route schedules.
- Bridge, tunnel, ferry and crossing semantics are not automatically represented by the current Road class.
- Any new transport concept must be justified by concrete creator workflows and a bounded Core Domain representation.

### 2.3 PoliticalRegion

Current descriptor:
- Stable key: `political_region`; schema version 2; semantics ID `world-view.object-type.political_region.v1`.
- Geometry: `boundary` 1..N Polygon/MultiPolygon.
- Definition: required `canonical_name`; bounded `region_class` = country / province / state / district / other.
- No runtime attributes are currently enabled.
- Invariant: boundary does not imply control.

Implications:
- PoliticalRegion represents the spatial territory or boundary itself.
- A PoliticalRegion can represent an administrative subdivision only where the creator's intended meaning fits the bounded type and relationship rules.
- A district-class PoliticalRegion is not automatically a city district, neighborhood or land-use zone.
- The type does not identify a government, polity, ruler, institution, legal system or controlling faction.
- Boundary geometry is not proof of sovereignty, membership, ownership or political control.

### 2.4 PointOfInterest and TerrainRegion

These existing types may support bounded ME-03 scenarios but must not be stretched beyond their descriptor meaning.

- `point_of_interest` has one required Point location and a bounded category: landmark / natural_feature / facility / heritage / other. It may represent a named civic or transport point only when the category fits; it is not a substitute for a semantically rich facility or network node if such concepts become necessary.
- `terrain_region` has an area extent and bounded `terrain_class`: plain / hill / mountain / desert / wetland / forest / tundra / other. It represents physical terrain/ecological/land-cover classification, not human zoning or land use.

## 3. Exact registered Relationship Type inventory

The endpoint pairs below are taken from P2-09-27 V0.2, not inferred from the broader seed catalog.

| Relationship Type | Exact relevant registered endpoint pair(s) | Current meaning | ME-03 disposition |
|---|---|---|---|
| `spatial.contains` | PoliticalRegion → Settlement; Landmass → Settlement; Settlement → PointOfInterest | Strict spatial containment; not ownership, sovereignty, jurisdiction or control | Usable only for the exact endpoint pair and declared containment semantics |
| `structural.part_of` | Settlement → Settlement is deferred, not enabled | Explicit semantic part/whole relation | Do not use for settlement hierarchy until separately adopted and enabled |
| `spatial.adjacent_to` | PoliticalRegion ↔ PoliticalRegion | Explicit meaningful adjacency, normally shared boundary or declared predicate | Potential administrative-border workflow; not generic proximity |
| `network.connects_to` | Road ↔ Road only | Explicit physical continuity between distinct Road Objects at a junction | Usable for explicit road-to-road connectivity; not road access to settlements |
| `hydrology.flows_to` | Not an ME-03 relationship | Directed River → River/Lake flow | Owned by ME-02; consume only as context |

Additional exact constraints:
- `spatial.contains` allows PoliticalRegion → Settlement, but does not represent political control or legal jurisdiction. The current pair rule permits at most one containing PoliticalRegion per region_class for a Settlement at a given History/time; different nested classes may coexist.
- `structural.part_of` currently enables MountainPeak → MountainRange only. Settlement → Settlement appears only in `deferred_endpoint_pairs`.
- `spatial.adjacent_to` currently enables PoliticalRegion ↔ PoliticalRegion and TerrainRegion ↔ TerrainRegion. The broader catalog's Landmass adjacency candidate is not enabled.
- `network.connects_to` currently enables Road ↔ Road only. The catalog explicitly defers Road → Settlement as a distinct directed access relation if ME-03 requires it.
- The relationship registry's generic `core.relationship.establish` capability does not bypass endpoint matrices, cardinality, temporal class, endpoint activity or relationship-specific validation.

## 4. Urban concept disposition

| Creator concept | Current formal representation | Gap / decision required |
|---|---|---|
| Village / town / city / capital | Settlement + `settlement_class` | Define creator workflows and classification guidance; no new Object Types |
| Settlement location | Settlement `anchor` | Placement and coordinate context consume ME-05 |
| Urban footprint | Settlement optional `extent` | Define when extent is useful, approximate or confirmed |
| Parent city / subordinate town | No enabled Settlement→Settlement part-of relation | Decide whether hierarchy is truly semantic and define temporal/cardinality rules |
| Country / province / state / administrative district | PoliticalRegion + `region_class` | Define subdivision and boundary workflows; do not infer actor/control |
| Government / polity / ruler / controlling faction | World Knowledge by default; no formal Object Type | Determine whether target political workflows require a formal actor concept |
| Political control / sovereignty | No enabled relationship | Requires formal actor endpoint and temporal succession/control semantics |
| Residential / commercial / industrial / agricultural zone | No current dedicated formal representation | Decide if separate functional-area Object Type is required; keep distinct from TerrainRegion |
| Road / street / track / trail | Road + bounded `road_class` | Validate if the current enum is adequate; no new type by visual style alone |
| Road-to-road junction continuity | Road ↔ Road `network.connects_to` | Explicit junction evidence and formal relationship; geometry alone insufficient |
| Road access to settlement | No enabled relationship pair | Define separate access semantics if needed; do not overload `network.connects_to` |
| Bridge / tunnel / ferry / crossing | No dedicated current type/relationship | Decide whether represented as Road classification/component/POI or a new semantic concept |
| Neighborhood / urban district | Not automatically PoliticalRegion or Settlement | Clarify whether administrative, social, morphological or land-use meaning |
| Port / station / market / civic facility | Possibly bounded POI only | Determine if distinct identity, access and attributes justify a specialist Object Type |

## 5. Cross-domain dependencies

### ME-02 Natural Geography

ME-03 consumes:
- elevation and terrain source fields under ME-05-01 authority;
- terrain region and land/water features;
- hydrology and River/Lake network facts;
- climate/ecology outputs only with their coverage, provenance and uncertainty.

ME-03 must not recalculate or redefine these inputs. A settlement placement recommendation must identify which inputs are available, stale, incomplete or unsupported.

### ME-05 Shared Geometry & Spatial Operations

ME-03 consumes:
- geometry component identity and formal operations;
- point-in-area, line intersection, adjacency and distance predicates;
- True / False / Indeterminate / Unsupported outcomes;
- coordinate-reference and measurement semantics;
- immutable Asset publication and freshness rules.

ME-03 adds specialist validation only. It must not fork the shared predicate implementation or turn a derived predicate into a persisted relationship without explicit semantic confirmation.

### World Knowledge and AI

Story facts such as "the capital controls the coast", "the old road was abandoned", or "the market grew around a bridge" require provenance and temporal interpretation. They must not be converted directly into unsupported formal relationships or scalar attributes. AI should identify missing concepts and propose bounded alternatives rather than fabricate an enabled descriptor.

## 6. Recommended design order

1. Settlement workflows using current anchor/extent/classification.
2. PoliticalRegion boundary and subdivision workflows, explicitly separating territory from governing actor.
3. Road authoring and Road-to-Road connectivity using the exact current relationship descriptor.
4. Human land-use / functional-area modeling after a dedicated concept inventory.
5. Settlement placement validation using ME-02 inputs and ME-05 spatial services.
6. Only then propose the minimum required Object Type / Relationship Type changes with compatibility and temporal semantics.

## 7. Acceptance checks for this inventory

- No new Object Type is introduced merely to represent village/town/city/capital.
- No Settlement hierarchy is treated as supported by `structural.part_of` before the endpoint pair is enabled.
- No Road-to-Settlement access edge is treated as supported by current `network.connects_to`.
- No political actor/control fact is inferred from PoliticalRegion containment.
- No land-use category is written into `terrain_class`.
- Every proposed relationship is checked against the exact endpoint matrix in P2-09-27.
- Every temporal fact is assigned to the correct Initial State, History-local or shared-definition authority path.
- Spatial evidence remains distinct from formal semantic relationships.

## 8. Status

ME-03-A establishes the current specialist inventory and exact registry gaps. It does not authorize registry changes. ME-03-B through ME-03-H must use this inventory as their starting point and may propose changes only with explicit semantic, temporal, compatibility and validation contracts.

## References

- ME-03-00 Settlements & Urban Geography Editor Scope and Work Plan V0.1.
- P2-09-21 V1 Built-in Object Type Seed Catalog V0.1.
- P2-09-22 V1 Built-in Relationship Type Seed Catalog V0.2.
- P2-09-24 V1 Built-in Object Type Field and Capability Catalog V0.1.
- P2-09-26 V1 Object Type Descriptor Registry V0.2.
- P2-09-27 V1 Relationship Type Descriptor Registry V0.2.
- P2-09-20 Relationship Descriptor / Temporal Contract V0.2.
- ME-02 V0.6 and ME-05-01 through ME-05-06.
