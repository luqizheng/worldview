# World View — ME-03-C Political & Administrative Geography

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist domain working baseline; no Core Domain descriptor, relationship endpoint, schema or implementation change is authorized
- Depends on: ME-03-00 V0.2; ME-03-A V0.1; ME-03-B V0.1; P2-09-18 through P2-09-27; ME-01; ME-05-01 through ME-05-06
- Scope: PoliticalRegion territory identity, boundary authoring, region classification, administrative geography, containment/adjacency, political authority distinction and temporal boundary semantics

## 1. Executive decision

V1 uses the existing `political_region` Object Type to represent a named political or administrative territory as a spatial fact. It does not use that Object as a proxy for a government, polity, ruler, institution, sovereign actor or controlling faction.

Current descriptor:
- one stable Core Domain Object identity;
- one or more Polygon/MultiPolygon `boundary` geometry components;
- required `canonical_name`;
- required bounded `region_class` = country / province / state / district / other;
- no enabled runtime attributes;
- invariant: boundary does not imply control.

V1 supports territory boundary authoring and historical revision, plus the currently registered PoliticalRegion-to-PoliticalRegion symmetric adjacency relation. It does not support formal parent/child administrative hierarchy, political control, sovereignty, ownership or jurisdiction relationships because the current registry does not enable those semantics/endpoints.

Keep four concepts distinct:
1. **Territory** — where a PoliticalRegion is located.
2. **Administrative classification** — the creator's broad category for that territory.
3. **Political actor** — who governs or claims it.
4. **Authority/control fact** — who actually controls it, under what claim or legal arrangement, and during which period.

Only the first two are represented by the current PoliticalRegion descriptor.

## 2. Current descriptor and relationship baseline

### 2.1 PoliticalRegion Object Type

P2-09-26 V0.2 currently registers:

| Concern | Registered contract | Design consequence |
|---|---|---|
| Stable key | `political_region`, schema version 2 | Reuse existing type |
| Geometry | `boundary` 1..N Polygon/MultiPolygon | At least one polygonal component |
| Definition | Required `canonical_name` | Named territory |
| Classification | `region_class`: country / province / state / district / other | Broad category, not an administrative tree |
| Runtime attributes | None enabled | No formal historical control/status/leader field |
| Invariant | `boundary_does_not_imply_control` | Geometry cannot prove political authority |
| Definition editing | Shared or Map-local definition service, per scope policy | Definition edits are not History Commands |
| Initial placement | Map Initial State service at time zero | No generic Command for time-zero membership |
| Post-zero creation | History-local creation for a pre-bound identity, with reservation | Follow existing identity and History rules |

The boundary role allows 1..N polygonal components. Multiple components may describe disconnected pieces of the same named territory (for example, islands or exclaves) only when the creator considers them one territorial identity. Separate meaningful territories remain separate Objects. Geometry component count does not create administrative subdivision or political membership.

### 2.2 Registered relationships

The current P2-09-27 V0.2 registry enables:

| Relationship | Endpoint pair | Meaning | ME-03-C usage |
|---|---|---|---|
| `spatial.adjacent_to` | PoliticalRegion ↔ PoliticalRegion | Symmetric explicit meaningful adjacency | Confirmed territorial neighbors |
| `spatial.contains` | PoliticalRegion → Settlement | Strict spatial containment | Territory contains Settlement spatially; not jurisdiction or allegiance |

The registry does **not** enable:
- PoliticalRegion → PoliticalRegion `spatial.contains`;
- PoliticalRegion → PoliticalRegion `structural.part_of`;
- PoliticalRegion → Polity/Government `political.controlled_by`;
- PoliticalRegion → Character/Faction/Culture membership or allegiance;
- directed sovereignty, claim, occupation or administrative-parent relationships.

The exact machine-readable endpoint matrix takes precedence over broader seed-catalog candidates.

## 3. PoliticalRegion semantics

### 3.1 What the Object represents

A PoliticalRegion is one creator-recognized named territory with a polygonal boundary. It may represent a country/kingdom territory, province, state, district or another explicitly territorial unit, including a historical or fictional territory.

The descriptor does not distinguish whether the territory is internationally recognized, claimed, effectively controlled, legally administered, disputed, occupied, autonomous or defunct. Those are separate political facts and cannot be inferred from the Object Type or its boundary.

### 3.2 Territory is not a political actor

A country-shaped PoliticalRegion is not the country as a governing organization or polity. A capital Settlement is not the government. A ruler or faction mentioned in the story is not automatically a formal map Object.

The system must not:
- store the ruler/government name as an untyped scalar on PoliticalRegion;
- infer sovereignty from Settlement containment;
- treat a boundary as proof of actual control;
- equate a region label with legal recognition;
- convert narrative allegiance into a spatial relationship without an approved endpoint and temporal contract.

World Knowledge remains the default home for political actors and narrative relations until a formal political actor model is designed.

### 3.3 Region classification

The existing `region_class` is one bounded definition classification:
- **Country** — country-scale territorial unit in the creator's world;
- **Province** — province-like territorial unit;
- **State** — state-like territorial unit;
- **District** — smaller territorial or administrative area;
- **Other** — territory not described by the above.

These labels are creator vocabulary, not a universal constitutional hierarchy. "State" may be a first-level subdivision in one world and a sovereign state in another. The editor must not hard-code a global country → state → province → district order.

Changing region_class is a definition edit under the current descriptor, not a time-varying historical attribute change. A historical change in administrative status requires a future temporal fact model.

## 4. Boundary identity and geometry

### 4.1 Boundary role

The PoliticalRegion boundary represents the territory's spatial extent. It is not automatically a cadastral property boundary, surveyed legal border, wall/fence, effective-control area, cultural-influence area or ecological region.

The UI should use creator language such as **Territory Boundary** or **Region Boundary**, and allow approximate, schematic and detailed authoring without implying legal precision.

### 4.2 Multiple components

The descriptor allows 1..N Polygon/MultiPolygon components. One territory may have disconnected islands or exclaves as multiple components if the creator intends one territorial identity.

Rules:
- component identity and lifecycle follow ME-05-02;
- geometry split/merge does not silently create or retire PoliticalRegion identity;
- a disconnected component is not automatically a separate administrative unit;
- multiple components do not establish control over the space between them;
- separate political entities remain separate Objects.

### 4.3 Boundary precision and provenance

A boundary may be creator-drawn, source-derived, refined from later evidence or schematic for storytelling. ME-05 coordinate/precision and Asset contracts remain authoritative. ME-03-C introduces no confidence scalar, legal status or source schema. Expose provenance and uncertainty where shared contracts support them; never fabricate coordinate precision or legal certainty.

## 5. Administrative subdivision and hierarchy

### 5.1 No formal parent-child hierarchy in current V1

The current relationship registry has no PoliticalRegion → PoliticalRegion containment or part-of endpoint pair. Therefore V1 must not persist a formal administrative tree.

In particular:
- a province polygon inside a country polygon is not automatically a formal child;
- overlapping boundaries do not establish parent-child membership;
- `spatial.adjacent_to` is not a hierarchy relation;
- Settlement containment does not prove that one PoliticalRegion is subordinate to another;
- the UI must not display an authoritative expandable administrative tree based solely on geometry or region_class.

Creators may author separately named PoliticalRegion Objects and inspect their spatial arrangement. The editor may show a derived containment candidate labelled "spatially inside", but cannot save it as an unsupported formal relationship.

### 5.2 Why not overload spatial.contains

The current `spatial.contains` is explicitly spatial containment with a bounded endpoint matrix. It does not represent administrative membership or territorial subordination. Extending it to PoliticalRegion → PoliticalRegion would change semantics, endpoint matrix, cardinality and potentially cycle rules.

If creator scenarios require administrative hierarchy, a future Core Domain proposal must decide:
- whether the relation means administrative subdivision, constitutional membership or mere territorial nesting;
- whether a territory may have one or multiple parents;
- whether hierarchy can change over time;
- how disputed/overlapping claims are represented;
- whether membership is History-local or a shared stable definition;
- how cycles and enclaves are handled;
- whether typed jurisdiction attributes are required.

Do not enable an endpoint pair until those semantics are explicit.

## 6. Adjacency and border-neighbor semantics

The current `spatial.adjacent_to` descriptor enables symmetric PoliticalRegion ↔ PoliticalRegion relationships.

ME-03-C interprets it narrowly as creator-confirmed meaningful territorial adjacency, normally territories sharing a boundary under agreed spatial predicate and precision rules.

It does not mean administrative parent/child, political alliance, diplomacy, access, road connection, proximity without a shared boundary, or control/disputed claim.

Rules:
1. Map Engine computes candidate boundary contact using ME-05 predicates and declared precision/tolerance.
2. A result may be True / False / Indeterminate / Unsupported; uncertainty is not coerced to True.
3. The editor shows a candidate neighbor relation, but does not auto-create it from touching polygons.
4. Formal establishment requires explicit creator intent and the registered typed operation.
5. The symmetric relationship is canonicalized; no reverse duplicate is persisted.
6. If a boundary is edited, re-evaluate relevant adjacency evidence and show conflicts. Do not silently end or create relationships.
7. A gap, point-only touch or cartographic overlap is not automatically meaningful adjacency; exact policy belongs to ME-05 predicate semantics plus this specialist rule.

Adjacency is Initial State or History-local under the current registry. It is not a Project-shared relationship by default.

## 7. PoliticalRegion-to-Settlement containment

The current registry enables PoliticalRegion → Settlement `spatial.contains`.

The relationship means only that the Settlement's anchor and optional extent satisfy the registered strict spatial containment precondition within the PoliticalRegion boundary, plus creator-confirmed intent.

The pair rule permits at most one containing PoliticalRegion per `region_class` for a Settlement at a given History/time; different region classes may coexist. This permits nested classifications only where region classes and descriptor validation allow them. It does not create a PoliticalRegion hierarchy.

UI language should say "Located within this territory", "Spatially contained by this region" or "Territory assignment confirmed". Avoid "Owned by", "Governed by", "Belongs to" or "Under the sovereignty of".

If the creator intends political jurisdiction or allegiance, that fact is not represented by the current relation. Keep it in World Knowledge with provenance or record it as a deferred domain requirement.

A geometry change that invalidates containment must produce a conflict requiring explicit resolution. It must not silently rewrite the relationship.

## 8. Boundary and political-history semantics

### 8.1 Historical boundary changes

A boundary revision representing a territorial change at a historical time is a Map/Branch History-local geometry change:
- it applies only to the selected Map/History path;
- it follows Branch inheritance and the established parent-at boundary;
- it does not mutate other Maps;
- it does not rewrite Project-shared definitions;
- it is recorded through registered geometry refinement/adjustment operations and Asset publication.

Examples: a border moves after a war; a province loses a valley in a treaty; an exclave is added/removed at a particular date; an alternate-history Branch restores an earlier border.

### 8.2 Current base-definition corrections

A correction to the current shared/base definition of a Project-shared PoliticalRegion is not a historical event. It follows the owning shared-object or Map-local definition service and its retroactive-current-definition semantics.

Examples: correcting a canonical-name typo or replacing an inaccurate current base outline with a better source.

Do not encode a historical border change as a shared-definition edit. Do not encode a current source correction as a historical war/treaty event.

### 8.3 Initial State and later introduction

- A PoliticalRegion present at time zero is registered through Map Initial State.
- A PoliticalRegion introduced after time zero uses existing identity reservation and History-local creation.
- A boundary revision after time zero is a selected-History geometry operation.
- Object identity remains stable when the boundary changes.

The editor must make the target Map/History/time visible before formal application.

### 8.4 Control and sovereignty changes

The current PoliticalRegion descriptor has no runtime attributes and the relationship registry has no political actor endpoint. V1 therefore cannot formally record "Actor A takes control of Region B", independence, claims, occupation or administrative-status changes.

These may be expressed as story knowledge and event narratives, but must be labelled World Knowledge rather than formal spatial relationships. A future political model must define actor identity, claims versus effective control, temporal intervals, simultaneous disputes, succession and evidence/provenance.

## 9. Authoring workflows

### 9.1 Create a territory

1. Creator selects Add Political Region or describes the territory to AI.
2. Editor starts a boundary Candidate; no formal Object is created yet.
3. Creator draws one or more polygonal components.
4. Creator enters canonical name and region class.
5. Editor validates geometry family, minimum component count, coordinate reference, precision and topology through shared services.
6. Editor shows nearby regions and possible boundary contacts as spatial observations.
7. Creator reviews identity, boundary, classification, target Map/History/time and relationship proposals.
8. Core Domain reserves identity and applies through Map Initial State or the appropriate post-zero History path.

### 9.2 Refine a boundary

1. Select the PoliticalRegion by stable Object identity.
2. Refine one or more existing boundary components using ME-05 geometry tools.
3. Preview old/new boundaries and affected spatial predicates.
4. Identify Settlements whose registered containment relationships may become invalid.
5. Identify adjacency relationships whose geometry evidence may change.
6. Do not automatically establish/end relationships. Present conflicts for explicit resolution.
7. Submit through the selected Map/Branch History context.

### 9.3 Create neighboring regions

1. Create two separate PoliticalRegion Objects.
2. Draw boundaries independently or derive candidate shared-border evidence.
3. Map Engine evaluates adjacency under ME-05 predicate rules.
4. Editor presents candidate relationship with evidence and uncertainty.
5. Creator confirms whether the territories are meaningful neighbors.
6. Core Domain establishes one canonical symmetric `spatial.adjacent_to` relationship if descriptor and temporal scope permit.

### 9.4 Represent subdivisions without hierarchy

V1 can author a country-class PoliticalRegion and several province-class PoliticalRegion Objects as separately named territories. It can display boundaries together and evaluate Settlement containment for each class.

It cannot persist a formal country-contains-province relation. The UI must not imply geometry-derived nesting is an authoritative administrative tree. If a creator requires a legally meaningful hierarchy, mark it unsupported/deferred and preserve the intended relation as World Knowledge.

### 9.5 Record historical border changes

1. Select target Map and Branch/History.
2. Select historical time or event context.
3. Refine the PoliticalRegion boundary in a Candidate.
4. Preview affected Settlements and region adjacency.
5. Confirm this is an in-world territorial event, not a current base-map correction.
6. Submit as a History-local geometry change.
7. Verify sibling Maps and unrelated Branches remain unchanged.

## 10. Creator-facing UX and diagnostics

The PoliticalRegion Inspector should show:
- Name and Region class;
- Territory boundary preview and component list;
- Boundary source/provenance and approximation status where supported;
- Current Map/Branch/History context;
- spatially contained Settlements, separated by region class;
- confirmed adjacent PoliticalRegions;
- derived spatial candidates clearly separated from formal relationships;
- unsupported political facts (control, sovereignty, hierarchy) as deferred/World Knowledge, not fake fields.

Suggested diagnostics:

| Technical condition | Creator-facing message |
|---|---|
| Invalid boundary | "This territory outline cannot be applied yet. Review the highlighted geometry." |
| Predicate Indeterminate | "The current map data cannot confirm whether these territories share a border." |
| Point-only touch | "These territories meet at one point; this is not currently treated as a confirmed shared border." |
| Settlement falls outside confirmed region | "This settlement is no longer inside the territory linked to it. Review the boundary or its territory assignment." |
| Geometry suggests nested region | "This region appears to lie inside another territory, but administrative hierarchy is not modeled yet." |
| User asks who controls a territory | "The map records the territory boundary, but not its governing actor or current control." |
| User asks historical sovereignty | "This political fact needs a time-aware political actor model; it cannot be stored as a current region attribute." |

Unsupported political interpretation should not block valid geometry authoring. Explain the capability boundary and preserve the creator's story intent.

## 11. V1 non-goals

- Government, polity, ruler, faction or sovereign actor Object Types.
- Political control, sovereignty, occupation, claims, allegiance or legal jurisdiction relationships.
- Formal PoliticalRegion parent/child administrative hierarchy.
- Automatic country → state → province → district ordering.
- Historical region-class or name changes without temporal definition/alias model.
- Legal boundary certification, cadastral parcels or property ownership.
- Automatic boundary adjacency/containment relationship creation.
- Geopolitical dispute simulation or recognition rules.
- Voting, taxation, government services or administrative workflow.
- Automatic conversion of PoliticalRegion geometry into Settlement or land-use zone.

## 12. Cross-architecture acceptance criteria

ME-03-C is acceptable when:
1. PoliticalRegion uses existing Core Domain identity and descriptor.
2. Boundary geometry is 1..N Polygon/MultiPolygon and uses shared geometry/Asset contracts.
3. Territory is distinct from political actor and effective control.
4. Region class is descriptive, bounded and not treated as a universal hierarchy.
5. No PoliticalRegion → PoliticalRegion containment or part-of relation is implied as enabled.
6. PoliticalRegion ↔ PoliticalRegion adjacency remains symmetric, explicit and creator-confirmed.
7. PoliticalRegion → Settlement containment retains exact registry semantics and cardinality.
8. Geometric containment never implies sovereignty, ownership or jurisdiction.
9. Historical border changes use Map/Branch History-local operations; current base corrections use definition-edit policy.
10. Time-zero and post-zero Object creation use correct Core Domain services.
11. Boundary edits surface affected containment/adjacency conflicts without silently rewriting relationships.
12. Unsupported political facts remain explicit World Knowledge/deferred concepts.
13. The editor explains unsupported semantics in creator language and preserves story intent.
14. All formal writes remain governed by Candidate, authorization, typed Command and Asset publication boundaries.

## 13. Open gates for later Core Domain work

- Whether creator scenarios justify a formal Polity/Government actor type.
- If so, whether control, claim, sovereignty, administration and occupation are separate relationship types or a temporal political-status model.
- Whether administrative subdivision needs a dedicated relation distinct from spatial containment.
- Whether a PoliticalRegion may have multiple simultaneous claims/controllers and how disputes are represented.
- Whether historical/localized region names and class changes need a temporal naming/classification model.
- Whether adjacency needs a more precise shared-boundary policy for point contacts, enclaves and multipart regions.
- Whether political actor references belong to formal Core Domain Objects, World Knowledge entities or a defined bridge.

## 14. Status and references

ME-03-C establishes the V1 PoliticalRegion territory-authoring baseline and makes unsupported political semantics explicit. It does not change Core Domain registries or authorize implementation.

References:
- ME-03-00 V0.2 — Settlements & Urban Geography Editor.
- ME-03-A V0.1 — Domain Inventory & Descriptor Reconciliation.
- ME-03-B V0.1 — Settlement Model & Authoring Workflows.
- P2-09-18 V0.2 — Object Type Descriptor / Attribute Contract.
- P2-09-22 V0.2 — V1 Built-in Relationship Type Seed Catalog.
- P2-09-26 V0.2 — V1 Object Type Descriptor Registry.
- P2-09-27 V0.2 — V1 Relationship Type Descriptor Registry.
- ME-05-02 through ME-05-06 — Shared Geometry, Topology, Coordinate, Asset and Integration contracts.
