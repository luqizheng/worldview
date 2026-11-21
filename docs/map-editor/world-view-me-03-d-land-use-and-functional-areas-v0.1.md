# World View — ME-03-D Land Use & Functional Areas

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist domain working baseline and Core Domain gap proposal; no Object Type, Relationship Type, executable descriptor, schema or implementation change is authorized
- Depends on: ME-03-00 V0.2; ME-03-A/B/C V0.1; ME-02 V0.3 and its specialist contracts; ME-05-01 through ME-05-06; P2-09-18 through P2-09-27
- Scope: Human land-use and functional-area semantics, distinction from terrain/land cover and presentation, spatial extent, overlap, mixed use, temporal change, validation and creator workflows

## 1. Executive decision

V1 should treat human land use / functional areas as a distinct spatial domain concept, not as a TerrainRegion classification, Settlement, PoliticalRegion, PointOfInterest, or renderer layer.

The current Core Domain registry has no suitable formal Object Type for human land use:
- `terrain_region` represents physical terrain/ecological/land-cover classes (plain, hill, mountain, desert, wetland, forest, tundra, other);
- `settlement` represents an inhabited place with one required point anchor and optional extent;
- `political_region` represents a territorial unit;
- `point_of_interest` represents a named point location;
- no existing type represents a polygonal area according to its human function or planned designation.

**Recommended domain direction:** propose one bounded `functional_area` Object Type for creator-authored, spatially bounded human-use/function areas, with a typed classification and explicit basis (actual / designated / proposed). This is a recommendation for a later Core Domain descriptor proposal, not an enabled V1 type. Until the registry change is reviewed and adopted, such areas remain Candidates or World Knowledge references; they must not be persisted by overloading `terrain_region`.

The design deliberately does not introduce separate ResidentialZone, CommercialZone, IndustrialZone, AgriculturalZone and similar Object Types. One typed Object Type with a bounded vocabulary is simpler and supports future extension under versioned descriptors.

## 2. The three dimensions must remain separate

### 2.1 Physical terrain and land cover

Owned by ME-02:
- terrain form: plain, hill, mountain, desert;
- physical/ecological cover: forest, wetland, tundra and related classes;
- elevation, slope, soil/water/climate inputs and derived ecological suitability;
- derived terrain and land-cover analysis.

A forest is a physical/ecological cover fact. It is not automatically a forestry land-use area. A wetland is not automatically a conservation reserve. A mountain is not a recreation zone.

### 2.2 Human use and designated function

Owned by ME-03-D:
- residential, commercial, industrial, agricultural and mixed-use areas;
- civic, institutional, recreational, transport, utility and special-purpose areas;
- creator-designated future development or planned zones;
- actual land-use patterns where the creator intends to represent human activity.

A forest plantation, logging district, protected forest, military training area or resort may occupy the same physical terrain class but have different human-use semantics.

### 2.3 Cartographic presentation

Owned by Editor/Map Engine presentation:
- fill color, hatch, outline, opacity, label, icon and visibility;
- thematic map styling and legend grouping;
- simplified display geometry at different scales.

A green fill or "residential" legend entry does not create a formal FunctionalArea. Presentation styles may visualize formal Objects, derived analysis or temporary Candidates, but cannot become semantic authority.

## 3. Existing Core Domain inventory and exact limits

### 3.1 Existing Object Types

| Existing type | Current registered geometry / definition | Why it is not the land-use model |
|---|---|---|
| `terrain_region` | 1..N Polygon/MultiPolygon extent; optional name; bounded `terrain_class`; optional immutable source dataset | Physical terrain/ecology/land-cover dimension only |
| `settlement` | Required Point anchor; optional Polygon/MultiPolygon extent; required name and settlement class | Inhabited place identity, not every functional zone inside or outside a settlement |
| `political_region` | 1..N Polygon/MultiPolygon boundary; required name and region class | Territorial/administrative unit, not a human-use classification |
| `point_of_interest` | Required Point location; required name and bounded POI category | Named location, not an area |
| `road` | Linear transport feature under its own descriptor | Network feature, not a land-use area |

Do not:
- add residential/commercial/industrial values to `terrain_class`;
- create one Settlement per neighborhood or land-use polygon;
- create a PoliticalRegion for a commercial district merely to obtain polygon geometry;
- use a PointOfInterest at the centroid as a substitute for an area;
- use a presentation-only layer as the only source of a creator-confirmed functional area.

### 3.2 Existing relationships

The current registry's relevant pairs do not include FunctionalArea:
- `spatial.contains` currently enables PoliticalRegion → Settlement, Landmass → MountainRange, Landmass → Settlement, Settlement → PointOfInterest and TerrainRegion → PointOfInterest;
- `spatial.adjacent_to` enables PoliticalRegion ↔ PoliticalRegion and TerrainRegion ↔ TerrainRegion;
- `structural.part_of` enables MountainPeak → MountainRange; Settlement → Settlement remains deferred;
- `network.connects_to` enables Road ↔ Road only.

Therefore V1 cannot formally link a FunctionalArea to Settlement, PoliticalRegion, TerrainRegion, Road or PointOfInterest through these existing endpoint pairs. Geometric overlap/containment may be queried and displayed as spatial evidence, but cannot be saved as an unsupported formal relationship.

## 4. Proposed FunctionalArea concept

### 4.1 Identity and meaning

A FunctionalArea is one creator-recognized spatial area whose defining meaning is a human use, intended function or planned designation.

It has:
- one stable Core Domain Object identity;
- one or more Polygon/MultiPolygon extent components;
- one required bounded `function_class`;
- one required bounded `function_basis`;
- optional canonical name;
- no implied owner, legal zoning authority, operator, population or economic output.

The Object represents the area as a semantic unit, not a particular rendering polygon. Geometry component split/merge follows ME-05-02 and must not silently create/retire FunctionalArea identity.

### 4.2 Proposed bounded classification vocabulary

Initial recommended `function_class` values:

| Class | Creator-facing meaning |
|---|---|
| `residential` | Housing and primarily residential use |
| `commercial` | Shops, offices, markets and commercial activity |
| `industrial` | Manufacturing, processing, workshops and industrial activity |
| `agricultural` | Cropland, orchards, pasture and managed food production |
| `mixed_use` | Deliberately mixed residential/commercial or other combined use |
| `civic_institutional` | Schools, government, hospitals and civic institutions as areas |
| `recreational` | Parks, sports grounds and leisure areas |
| `transport` | Airports, rail yards, ports and other transport land-use areas |
| `utility_infrastructure` | Power, water, waste and other utility/infrastructure areas |
| `conservation` | Human-designated protected or managed conservation use |
| `military_security` | Military bases, restricted/security use areas |
| `special_purpose` | A bounded use not covered above, with an explicit creator label |
| `other` | Fallback only when no listed class fits |

This is a small initial vocabulary, not an exhaustive real-world zoning code. Do not encode detailed zoning subcategories, building codes, permitted-use tables or legal restrictions in V1.

### 4.3 Basis: actual, designated or proposed

The same geometry may have a different meaning depending on whether it records present use or intended future use. The recommended `function_basis` is:

- `actual` — creator asserts this is how the area is currently used at the selected world time;
- `designated` — creator asserts the area is formally or informally set aside for a function, whether or not current use fully matches;
- `proposed` — a planning Candidate that has not yet become an adopted world fact.

A proposed area must remain a Candidate and cannot be formally applied as an adopted FunctionalArea until explicitly accepted. If the creator wants to preserve an unapproved planning option, keep it in AI Candidate staging or World Knowledge rather than treating it as current world state.

A designated industrial area may currently be farmland. This is not a contradiction: one FunctionalArea can express the designation and another can express actual use if the creator needs both dimensions. V1 should not automatically derive one from the other.

### 4.4 No separate zoning/legal regime

`designated` does not imply statutory zoning, enforceable regulation, ownership, permit status or a government authority. These are future legal/administrative semantics and must not be fabricated by a land-use editor.

## 5. Geometry, granularity and area identity

### 5.1 Polygon extent

FunctionalArea is area-based and therefore needs Polygon/MultiPolygon extent. A Point anchor is not a sufficient representation.

A single FunctionalArea may have multipart geometry when the creator regards disconnected parcels as one coherent functional area (for example, a group of fields operated as one estate). Separate named or independently meaningful areas should be separate Objects.

The number of components does not define a parent/child hierarchy, parcel ownership or a network.

### 5.2 Creator-chosen granularity

V1 should not force a universal minimum area or parcel size. A creator may represent:
- a whole industrial district at Region scale;
- a neighborhood-scale residential area;
- a single large park or agricultural estate;
- a site-scale campus or facility grounds.

The editor should warn about scale mismatch and excessive detail, but must not invent a fixed square-kilometer threshold. Precision and measurement follow ME-05-04; display simplification is derived presentation.

### 5.3 FunctionalArea is not a building

Buildings and interior spaces belong to the later Buildings & Interior Spaces specialist editor. FunctionalArea may spatially surround many buildings or contain facilities conceptually, but the current registry has no enabled FunctionalArea-to-building relationship. Do not treat a campus or factory site as the building itself.

## 6. Overlap, nesting and mixed-use semantics

### 6.1 Overlap is permitted, not automatically meaningful

Different human-use areas may overlap because they describe different functions, planning layers or levels of detail. Examples:
- a designated conservation area overlaps an actual forestry use area;
- a planned transport corridor crosses current agricultural land;
- a civic campus lies inside a larger mixed-use district.

Overlap is not automatically an error and does not establish semantic containment, ownership or legal priority.

### 6.2 Same-basis/same-class overlap

For two FunctionalAreas with the same `function_class` and `function_basis`:
- exact duplicates should be flagged for review;
- partial overlaps should be warnings unless a specialist rule says otherwise;
- the system must not auto-merge their identities;
- the system must not silently clip or subtract geometry.

The creator may intentionally model separate facilities, estates or neighborhoods with overlapping approximate outlines. Preserve intent and show evidence.

### 6.3 Mixed use

Use `mixed_use` when the creator intentionally treats the area as one coherent mixed-use district and does not need separate function polygons.

If different uses need independent geometry, identity, temporal history or styling, create separate FunctionalArea Objects. Do not introduce a complex per-polygon stack of arbitrary use percentages in V1.

### 6.4 No formal nesting relationship in V1

The existing relationship registry does not enable FunctionalArea-to-FunctionalArea containment or part-of. The editor may display a derived "inside/overlaps" spatial observation, but must not persist a formal parent/child or administrative hierarchy. Do not overload `spatial.contains` or `structural.part_of`.

## 7. Relationship to terrain, settlements and political regions

### 7.1 TerrainRegion

TerrainRegion and FunctionalArea are independent classification dimensions:
- a forest TerrainRegion may overlap a forestry FunctionalArea;
- a wetland TerrainRegion may overlap a conservation FunctionalArea;
- a plain TerrainRegion may overlap agricultural and residential FunctionalAreas;
- an industrial FunctionalArea may occupy flat terrain or a reclaimed coast.

The editor can overlay both and calculate spatial intersection/coverage using ME-05. No formal relationship is currently enabled between these types.

### 7.2 Settlement

A FunctionalArea may lie within or cross a Settlement extent. It may represent a neighborhood, industrial estate, park or commercial center inside an urban settlement.

However:
- the current registry does not enable Settlement → FunctionalArea containment;
- a FunctionalArea does not become a child Settlement;
- a Settlement's optional extent is not the sum of its land-use polygons;
- an area outside a Settlement may still be agricultural, industrial, recreational or transport use;
- geometry alone cannot establish a formal relationship.

A future relationship proposal should consider whether Settlement → FunctionalArea means "spatially within the settlement" or "functionally part of the settlement"; these are not identical. Do not enable the pair until the meaning, cardinality and temporal behavior are specified.

### 7.3 PoliticalRegion

A FunctionalArea may be spatially within a PoliticalRegion, but this does not mean it is owned, governed or legally zoned by that region. Current PoliticalRegion → Settlement containment cannot be repurposed for FunctionalArea.

### 7.4 Roads and transport

A transport FunctionalArea can describe an airport, rail yard, port complex or logistics district as an area. The Road Object and Road-to-Road `network.connects_to` remain the transport network. FunctionalArea geometry does not create road access or network continuity.

## 8. Temporal semantics and history

### 8.1 Actual use changes over time

A change from farmland to industrial use is a time-varying world fact. Once a FunctionalArea type and descriptor are adopted, a change in actual use should be represented through the selected Map/Branch History, not by retroactively editing the shared definition.

Examples:
- an agricultural area becomes a residential district after urban expansion;
- a military base is decommissioned and becomes a public park;
- a port district expands in one alternate-history Branch.

The system must preserve the selected Map/Branch isolation and branch inheritance rules. A historical change in function may be expressed as a typed attribute change only if the adopted descriptor explicitly enables that runtime field; otherwise use a typed replacement/transition workflow approved by Core Domain. This document does not authorize either command.

### 8.2 Designation changes

A planned/designated function may change at a specific historical time. It must not be conflated with actual-use change. A future descriptor must specify whether `function_basis` is definition-time classification or a History-local mutable field. Recommendation: keep basis and class in typed, history-aware semantics if the product needs to show designation and actual use evolving independently.

### 8.3 Current base-definition correction

Correcting a mistaken current base classification or boundary is a definition correction under the owning shared-object or Map-local definition policy. It is not a historical event. This follows ME-01 and ME-05-01 rules.

### 8.4 Time zero and later introduction

A FunctionalArea present at time zero belongs in Map Initial State. A FunctionalArea introduced after time zero requires reserved identity and the correct History-local creation path. Formal temporal behavior must be settled in the eventual Core Domain descriptor.

## 9. Authoring workflows

### 9.1 Create an actual-use area

1. Creator chooses **Add Functional Area** or describes the area in ordinary language.
2. AI identifies likely function class and asks only when the distinction changes the result (for example, actual use versus planned designation).
3. Editor creates a Candidate with a polygonal extent.
4. Creator chooses function class and confirms `actual` or `designated` basis.
5. Editor validates geometry, coordinate reference, precision, minimum extent and topology through ME-05 services.
6. Editor overlays TerrainRegion, Settlement, PoliticalRegion and Road context as spatial evidence only.
7. Editor reports overlaps, gaps, scale mismatch and possible duplicate areas without silently changing geometry or relationships.
8. Creator reviews name, class, basis, geometry, target Map/Branch/time and provenance.
9. After the Core Domain descriptor is formally adopted, the appropriate Initial State or History-local operation may apply it. Until then it remains a Candidate/World Knowledge representation.

### 9.2 Plan a future functional area

1. Creator describes a desired future industrial, residential, transport or conservation area.
2. AI creates a `proposed` Candidate, not a formal world fact.
3. Editor overlays existing terrain, water, settlements, roads and political territories.
4. Deterministic spatial analysis reports constraints and uncertainty; AI explains them in creator language.
5. Creator may revise, accept as a designated area, or discard the proposal.
6. Formal adoption follows the descriptor and temporal contract once approved.

### 9.3 Change land use at a historical time

1. Select Map, Branch/History and target time.
2. Select the affected FunctionalArea identity or create a new Candidate for the changed area.
3. Preview the old/new function and geometry, plus overlaps and affected derived results.
4. Confirm whether this is an in-world transition or a correction to current base data.
5. Apply only through the adopted typed Core Domain operation; never overwrite another Map or sibling Branch.

## 10. Derived analysis and AI responsibilities

The editor may provide deterministic derived analysis:
- percentage/area coverage by function class within a selected scope;
- intersections with terrain, water, Settlement extents and PoliticalRegion boundaries;
- approximate built-up/functional density if a declared method and input data exist;
- fragmentation and patch-size summaries;
- candidate land-use suitability from slope, water, climate, access and creator constraints.

Derived outputs must carry source fingerprints, algorithm/method version, parameters, spatial reference, coverage and uncertainty. They are not formal world facts until explicitly reviewed and adopted through a supported workflow.

AI Assistant may:
- translate story intent into candidate functional areas;
- explain trade-offs and spatial conflicts;
- propose class/basis and identify ambiguous distinctions;
- compare alternatives and summarize deterministic analysis;
- prepare a Candidate for creator review.

AI must not:
- invent physical measurements or suitability scores;
- infer actual use solely from terrain or color;
- auto-apply a functional area or formal relationship;
- convert a proposed zone into an actual-use fact;
- claim legal zoning, ownership or regulatory status without a dedicated model and evidence.

## 11. Creator-facing UX

Functional Area Inspector should show:
- name (optional in the proposed model);
- function class;
- basis: actual / designated / proposed;
- boundary preview and component list;
- Map/Branch/time context;
- provenance and source, when available;
- overlap summary with other FunctionalAreas;
- terrain, Settlement, PoliticalRegion and Road context as derived spatial observations;
- freshness and uncertainty of derived analysis.

Suggested creator-facing language:

| Condition | Message |
|---|---|
| Terrain class mistaken for human use | "Forest describes the physical land cover. If this area is used for forestry or protected as a reserve, record that as a separate human-use area." |
| Actual and planned uses differ | "This area is designated for industry, but its current use is still agricultural. Keep the two facts separate." |
| Areas overlap | "These areas overlap. That may be intentional because they describe different uses or planning layers; review the overlap before applying." |
| Candidate has no formal type yet | "This functional area can be drafted and reviewed, but the current map model does not yet support saving it as a formal object." |
| User implies legal zoning | "The map can describe a designated function, but does not yet model legal zoning rules or the authority that enforces them." |
| Geometry intersects a road | "This area touches or crosses a road geometrically. Road access or network connection has not been established." |

## 12. V1 non-goals

- Full municipal zoning law, permitted-use tables, building codes and development-control regulations.
- Land ownership, cadastral parcels, leases, taxation or property rights.
- Population, employment, economic output or traffic-flow simulation.
- Automatic land-use inference from terrain class, satellite imagery or renderer color.
- Arbitrary per-polygon percentages, multiple simultaneous function stacks or unlimited custom executable categories.
- Building footprints, floor plans or interior facility modeling.
- Formal FunctionalArea-to-Settlement/PoliticalRegion/TerrainRegion/Road relationships before endpoint and temporal contracts are adopted.
- Automatic geometry union, clipping, overlap subtraction or identity merging.
- Universal minimum area thresholds or legal boundary precision.

## 13. Core Domain proposal and open gates

Before enabling `functional_area`, Core Domain owners must review:

1. Stable Object Type key and semantics ID; recommended key: `functional_area`.
2. Geometry role: `extent` 1..N Polygon/MultiPolygon.
3. Definition fields: optional canonical name; bounded `function_class`; `function_basis` semantics and temporal mutability.
4. Whether actual/designated facts are one type with basis or two related semantic types. Current recommendation: one type with basis, preserving independent overlapping Objects when both facts coexist.
5. Identity lifecycle, time-zero and post-zero creation, activation/deactivation and history replay.
6. Geometry and Asset publication operations, source provenance and validation.
7. Whether any relationships are necessary in V1. Initial recommendation: no new relationship is required to author standalone areas; spatial overlays remain derived.
8. Whether Settlement → FunctionalArea containment should be proposed later, with exact meaning, cardinality and temporal class.
9. Overlap diagnostics and duplicate detection policy.
10. Schema versioning, descriptor compatibility and migration/replay behavior.

No registry or schema change is included in this ME-03-D working baseline.

## 14. Cross-architecture acceptance criteria

ME-03-D is acceptable when:
1. Human use is distinct from physical terrain/land cover.
2. Functional area is distinct from Settlement, PoliticalRegion, Road and PointOfInterest.
3. Cartographic styling is not semantic authority.
4. The proposed model uses one bounded typed concept rather than one Object Type per use class.
5. Actual, designated and proposed basis are explicitly distinguished.
6. Area geometry and identity follow ME-05 component/Asset contracts.
7. Overlap and nesting do not silently create semantic relationships.
8. No unsupported relationship endpoint is treated as enabled.
9. Historical use/designation changes are Map/Branch-local and separated from current base-definition corrections.
10. Derived analysis is reproducible and not silently promoted to formal fact.
11. AI prepares candidates and explanations but does not bypass creator confirmation or Core Domain authorization.
12. The UI explains distinctions in creator language without requiring GIS expertise.
13. The Core Domain proposal is separately reviewed before any implementation.

## 15. Status and references

ME-03-D establishes the specialist land-use domain baseline and recommends a future `functional_area` Object Type proposal. It does not alter the current Core Domain registry and does not authorize implementation.

References:
- ME-03-00 V0.2 — Settlements & Urban Geography Editor.
- ME-03-A V0.1 — Domain Inventory & Descriptor Reconciliation.
- ME-03-B V0.1 — Settlement Model & Authoring Workflows.
- ME-03-C V0.1 — Political & Administrative Geography.
- ME-02 V0.3 and ME-02 specialist contracts — Terrain, ecology, fields and derived analysis.
- ME-05-01 through ME-05-06 — Field, geometry, topology, coordinate, Asset and integration contracts.
- P2-09-18 V0.2 — Object Type Descriptor / Attribute Contract.
- P2-09-22 V0.2 — V1 Built-in Relationship Type Seed Catalog.
- P2-09-26 V0.2 — V1 Object Type Descriptor Registry.
- P2-09-27 V0.2 — V1 Relationship Type Descriptor Registry.
