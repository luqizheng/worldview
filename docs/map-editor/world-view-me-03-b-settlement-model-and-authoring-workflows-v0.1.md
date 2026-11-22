# World View — ME-03-B Settlement Model & Authoring Workflows

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist domain working baseline; no Core Domain descriptor, schema, relationship endpoint or implementation change is authorized
- Depends on: ME-03-00 V0.2; ME-03-A V0.1; ME-01-01 through ME-01-07; ME-02; ME-05-01 through ME-05-06; P2-09-18 through P2-09-27
- Scope: Settlement identity, classification, anchor/extent, authoring lifecycle, multiscale representation, validation and creator-facing workflows

## 1. Executive decision

V1 should model a settlement as one stable Core Domain Object with:
- one required Point geometry role: `anchor`;
- zero or one Area geometry role: `extent`;
- one required definition field: `canonical_name`;
- one bounded definition classification: `settlement_class` = village / town / city / capital / other.

The anchor identifies where the inhabited place is located. The optional extent describes the area understood to belong to, or represent, that settlement for map-authoring purposes. The two roles are complementary, not alternatives.

A settlement can be valid and useful with only an anchor. Its extent can be added, adjusted or refined later without changing the settlement's identity.

The V1 product must not invent a Settlement hierarchy relationship. The current relationship registry explicitly defers Settlement → Settlement for `structural.part_of`; spatial containment also does not enable Settlement → Settlement. Classification, visual nesting and geographic containment must not be used as substitutes for an unavailable semantic hierarchy.

## 2. Current descriptor baseline

The current P2-09-26 V0.2 Settlement descriptor is the authoritative starting point:

| Concern | Current registered contract | ME-03-B interpretation |
|---|---|---|
| Type key | `settlement`, schema version 2 | Reuse one formal Object Type |
| Identity | Core Domain stable Object identity | Never replace identity when refining geometry or changing display scale |
| Anchor | Exactly 1 Point | Required settlement location |
| Extent | 0..1 Polygon/MultiPolygon | Optional mapped settlement footprint |
| Definition | Required `canonical_name` | Stable canonical name |
| Classification | `settlement_class`: village / town / city / capital / other | Broad creator-facing settlement kind |
| Population estimate | Runtime candidate disabled | Do not treat as an enabled formal mutable field |
| Condition code | Runtime candidate disabled | Do not treat as an enabled formal mutable field |
| Runtime attribute change | Disabled in current seed descriptor | No invented historical population/condition editing capability |
| Geometry operations | Declared component operations, subject to registered capability and scope | Reuse shared Core Domain geometry operations; no local CRUD path |

The current descriptor registry is a recommended baseline, not a frozen implementation schema. This document makes no descriptor mutation.

## 3. Semantic model

### 3.1 What a Settlement represents

A Settlement is an inhabited place recognized by the creator as one meaningful place in the world. It may be:
- a village, town, city or capital;
- a seasonal or abandoned inhabited place only if the domain's active/presence semantics explicitly support the intended state;
- a compact settlement with a clearly bounded footprint;
- a dispersed settlement whose exact extent is approximate or not yet known.

A Settlement is not:
- a PoliticalRegion or government;
- a land-use district;
- a collection of every building inside its extent;
- a road junction or transport node;
- a visual map symbol;
- a Working Scope (World / Region / Locality / Site);
- a population simulation container.

### 3.2 Anchor semantics

The anchor is the authoritative spatial locator for the settlement.

Creator-facing guidance:
- place the anchor at the meaningful center or representative location of the settlement;
- for a linear or dispersed settlement, choose the principal inhabited center rather than an arbitrary geometric centroid;
- for a port, river town or mountain settlement, place it at the inhabited place, not automatically on the water, road centerline or mountain peak;
- if the settlement is known only from a story or approximate map reference, the anchor may carry creator-declared approximation/provenance through the owning geometry/source contract; the editor must not fabricate precision.

The anchor is not necessarily:
- the geometric centroid of the extent;
- the town hall, castle, station, harbor or other facility;
- the exact population center;
- the road access point.

Those may be separate PointOfInterest Objects where the existing POI semantics fit and the creator intends an independently meaningful place.

### 3.3 Extent semantics

The extent is an optional polygonal description of the settlement's mapped footprint.

It can describe:
- the approximate built-up or inhabited footprint;
- the overall settlement area as the creator understands it;
- a generalized boundary appropriate to the current source knowledge.

It does not automatically mean:
- legal municipal boundary;
- administrative jurisdiction;
- land ownership;
- zoning boundary;
- exact built-up footprint derived from satellite imagery;
- the sum of all residential/commercial land-use polygons.

The Inspector should label it in creator language such as **Settlement Area** or **Mapped Extent**, not imply a legal boundary.

A creator may intentionally draw an approximate or schematic extent. Its epistemic quality and source/provenance must remain distinguishable from a precise surveyed boundary if such metadata is supported by the geometry/source contract. ME-03-B does not invent a new confidence field.

### 3.4 Anchor and extent consistency

When an extent exists:
- the anchor should normally lie within or on the settlement extent;
- a point outside the extent is a diagnostic requiring creator attention, not an automatic geometry rewrite;
- if the extent is approximate or intentionally schematic, the editor may allow an acknowledged exception;
- the Map Engine provides the point-in-area predicate and its True / False / Indeterminate / Unsupported outcome;
- ME-03 adds the specialist interpretation, but does not implement a second spatial predicate engine.

A False result should be explained as "The settlement marker is outside its mapped area." An Indeterminate result should explain why the check cannot be decided from current geometry/reference/precision. Neither should silently move the anchor or reshape the polygon.

## 4. Classification and hierarchy

### 4.1 V1 classification

The existing `settlement_class` is a broad creator-facing classification:
- **Village** — small inhabited place;
- **Town** — intermediate inhabited place;
- **City** — major urban settlement;
- **Capital** — settlement designated as a capital by the creator's world facts;
- **Other** — inhabited place not well described by the above.

These are descriptive categories, not computed population bands. The application must not infer a class from population, extent area, building density or visual scale.

Capital is a political/story designation, not necessarily the largest city or a formal government Object. The current Settlement classification alone does not say what entity it is the capital of.

Do not create City, Town, Village or Capital Object Types.

### 4.2 Classification is not a parent/child hierarchy

A city may be larger than a town, but that does not establish a formal part-of relation. A capital may be a city, but the existing enum uses one selected class; the editor must not assume it can encode multiple simultaneous labels unless the descriptor is deliberately revised.

For V1:
- each Settlement has one current `settlement_class` value;
- no formal Settlement → Settlement hierarchy is enabled;
- no parent settlement field is added to the Object;
- no nested settlement tree is fabricated from overlapping extents;
- a separate Settlement may be created only when it is independently meaningful as an inhabited place, not merely because a neighborhood or district is visible.

If a creator needs a capital containing suburbs, satellite towns or multiple separately named settlements, each independently meaningful inhabited place may be a Settlement Object. Their geographic proximity can be inspected, but the application must not persist a hierarchy relationship until a compatible descriptor and temporal contract are adopted.

### 4.3 Urban neighborhoods and districts

Neighborhood, borough, ward, suburb and city district are ambiguous terms:
- they may mean an administrative division;
- a named social or historical locality;
- a morphological part of the built-up area;
- a land-use zone.

They are not automatically Settlements. They are not automatically PoliticalRegions. They are not part of the V1 Settlement hierarchy.

ME-03-C and ME-03-D own the later analysis of administrative territories and human functional areas. Until then, the editor may show creator notes or World Knowledge references, but must not formalize a district as a Settlement merely to obtain a polygon.

## 5. Multiscale representation

### 5.1 One Object across all Working Scopes

World / Region / Locality / Site are editor Working Scopes. They do not create separate Settlement identities.

The same Settlement Object may appear:
- at World scope as a small marker or label;
- at Region scope as a more prominent symbol, optionally with a generalized extent;
- at Locality scope with its settlement extent and nearby context;
- at Site scope as a named location anchor, while buildings and facilities are authored as their own domain Objects in their respective specialist modes.

No scope transition duplicates the Settlement Object or allocates a new Object ID.

### 5.2 Geometry versus cartographic representation

Formal geometry remains the geometry owned by the Core Domain Object.

Scale-dependent symbol size, label placement, decluttering, simplified outlines, halo, visibility and emphasis are presentation/derived representations. They must not overwrite formal anchor/extent geometry.

A generalized outline may be generated for display at small scales, but:
- it is derived from the authoritative extent;
- it is disposable/rebuildable;
- it retains source fingerprint and algorithm/version metadata where the derived artifact contract requires;
- it is never submitted as a replacement for the authoritative geometry merely because it looks better at a particular zoom.

If no extent exists, small-scale display uses the anchor symbol. It must not invent an extent.

### 5.3 Progressive detail

The editor supports progressive authoring:
1. Name and place the anchor.
2. Choose a broad settlement class.
3. Optionally add a settlement extent.
4. Refine the extent as better information becomes available.
5. Add independently meaningful facilities, roads and other features in their owning specialist workflows.

The absence of later detail is not an error. A settlement can remain anchor-only indefinitely.

## 6. Relationship and containment boundaries

### 6.1 PoliticalRegion containment

The current registry enables `spatial.contains` for PoliticalRegion → Settlement.

This relationship means that the Settlement's anchor and optional extent satisfy the registered strict spatial containment precondition within the PoliticalRegion boundary. It does not mean:
- sovereignty;
- legal jurisdiction;
- ownership;
- government control;
- cultural affiliation;
- historical allegiance.

At a given History/time, the current pair rule permits at most one containing PoliticalRegion per `region_class` for a Settlement; different nested region classes may coexist. This must be validated against the exact current registry and descriptor implementation.

A geometry containment candidate may be shown to the creator. Formal relationship creation still requires explicit semantic intent and the correct registered Core Domain operation. A boundary edit that causes the predicate to change must not silently create or delete political facts.

### 6.2 Landmass containment

The registry enables Landmass → Settlement containment. It is a physical/geographic containment relation, not political membership.

If the Map does not have an applicable Landmass Object or the geometry evidence is Indeterminate, the editor must not fabricate one.

### 6.3 Settlement-to-Settlement

No Settlement → Settlement spatial containment or structural hierarchy relation is enabled in the current registry.

Consequently:
- overlapping Settlement extents are not automatically parent/child;
- one settlement anchor inside another settlement extent does not create a relationship;
- nearby settlements do not become a formal metropolitan area;
- a city and a satellite town remain independent Settlement Objects unless a future domain contract adds an explicit supported relationship.

## 7. Authoring workflows

### 7.1 Quick place — anchor-only

Scenario: "Add a small fishing village on the sheltered bay."

1. Creator invokes Add Settlement or asks AI to place one.
2. Editor enters a placement preview; no formal Object is created yet.
3. Creator places or adjusts the anchor on the map.
4. Creator supplies/confirms canonical name and settlement class.
5. The system shows relevant contextual evidence (coast, water, terrain, nearby settlements) as observations, not automatic facts.
6. Creator reviews the Candidate, including assumptions and target Map/History/time.
7. The appropriate Core Domain path applies the initial placement or post-zero creation under the current descriptor and authorization rules.
8. The settlement appears with a marker and label. No extent is required.

### 7.2 Add or refine settlement extent

Scenario: "Show the approximate built-up area of this town."

1. Select an existing Settlement.
2. Choose Add Settlement Area when no extent exists, or Refine Settlement Area when it does.
3. Draw or edit a polygon using the shared geometry tools.
4. Map Engine validates geometry type, coordinate reference, precision and topology preconditions.
5. ME-03 specialist validation checks anchor/extent consistency and reports uncertainty.
6. Preview the result independently of the formal Object state.
7. Review the proposed geometry operation and target History context.
8. Apply only through the registered Core Domain geometry operation and asset publication protocol.

The geometry editor must respect the descriptor's 0..1 extent cardinality. A second independent extent is not created by using another component ID. If the creator needs a disconnected settlement footprint, ME-03 must determine whether MultiPolygon is the correct single extent representation under the current geometry contract; it must not add multiple extent components contrary to the descriptor cardinality.

### 7.3 Refine an approximate settlement

Scenario: "We now know the eastern edge follows the river."

1. Preserve the existing Settlement Object identity.
2. Refine the current extent component using a source-fingerprinted geometry refinement proposal.
3. Show the previous and proposed outlines, changed area and relevant source evidence.
4. Revalidate anchor/extent consistency and any registered PoliticalRegion/Landmass containment facts affected by the change.
5. Do not automatically end or establish relationships as a side effect. Report relationship conflicts for explicit resolution.
6. Submit through the correct Map/Branch History operation.

A refinement changes geometry state, not Object identity or Settlement classification.

### 7.4 Change the settlement class

Changing Village to Town or Town to City is a definition-field change under the current descriptor model, not a geometry operation. The current seed descriptor does not enable runtime attribute mutation for `settlement_class`.

Therefore V1 must not expose a fake historical "change class at year X" control or route the change through a generic Object attribute Command. The owning Core Domain definition-edit policy must determine whether the field is Project-shared or Map-local and apply the established retroactive-current-definition semantics. If historical classification is required, it needs a separately designed temporal fact/attribute contract.

### 7.5 Rename

`canonical_name` is a required definition field. Rename follows the owning definition-edit service and its scope policy. It must not be represented as a history-local runtime attribute change unless the descriptor explicitly enables that behavior in a future version.

Localized aliases, historical names and time-varying names are not part of the current descriptor and remain future design topics.

### 7.6 Create at time zero versus create after time zero

The current Object Type descriptor distinguishes:
- Map Initial State service for time-zero map membership and initial placement;
- post-zero History-local creation for an already bound identity with identity reservation;
- Object identity allocation/reservation from the geometry editor.

ME-03 must use these Core Domain paths. It must not create a Settlement by writing rows directly, nor treat the same UI gesture as having identical temporal effects in every History context.

The UI should clearly identify whether the creator is:
- adding a settlement to the Map's initial world state; or
- introducing an already identity-bound Settlement into the selected History after time zero.

A cancellation before formal application discards the Candidate only. A successful formal Command is not rolled back merely by cancelling the AI task or closing the editor.

## 8. Spatial validation and creator diagnostics

| Check | Evidence | Result behavior |
|---|---|---|
| Anchor is inside/on extent | ME-05 point-in-area predicate | False → warning; Indeterminate → explain unavailable certainty; no silent move |
| Extent is valid polygonal geometry | Geometry validation | Block invalid geometry application; preserve editable preview |
| Extent cardinality | Settlement descriptor 0..1 | Reject a second independent extent component |
| PoliticalRegion contains Settlement | Registered spatial predicate and explicit relationship intent | Candidate only; no inferred sovereignty or auto-link |
| Landmass contains Settlement | Registered spatial predicate | Candidate/diagnostic only; no invented Landmass |
| Nearby water/road/terrain | ME-02 facts and ME-05 spatial queries | Contextual observations, not automatic port/access/road relationships |
| Settlement overlaps another Settlement | Geometry predicate | Informational unless a concrete conflict rule exists; no inferred hierarchy |
| Settlement anchor precision | Geometry/source metadata and CRS contract | Expose approximation/uncertainty; do not manufacture precision |

Creator-authored fictional exceptions must remain possible. A suitability warning is not a prohibition. Only invalid geometry, descriptor violations, stale references, authorization failures or other deterministic formal invariants block application.

## 9. Candidate, authorization and formal application

Every AI-assisted or multi-step settlement authoring action follows the shared ME-01 interaction and Core Domain boundaries:

1. Intent interpretation.
2. Candidate construction (proposed name/class/anchor/extent/relationships and provenance).
3. Validation and conflict report.
4. Human review where required by the shared risk/authorization policy.
5. Authorization bound to proposal version/hash and target Map/History/time.
6. Typed Core Domain submission.
7. Reconciliation of idempotency/application result.
8. Formal state refresh.

The AI may suggest a location or classification, but cannot:
- allocate or invent a stable Object ID outside Core Domain reservation;
- write formal Object or geometry tables;
- infer formal containment or hierarchy solely from spatial predicates;
- change a definition field through an unsupported runtime Command;
- silently replace an existing anchor or extent.

## 10. UX scenarios and acceptance

### Scenario A — Place a village before knowing its boundary

- Creator can create a named Settlement with only anchor and class.
- Inspector clearly shows "Area not defined" as an allowed state, not an error.
- Canvas displays marker/label at every relevant scope.
- No synthetic polygon is generated.

### Scenario B — Refine a town's mapped area

- Selection always resolves to the same Settlement Object ID.
- Existing extent can be edited with shared geometry tools.
- Candidate preview and formal geometry remain separate.
- Anchor/extent diagnostics are visible and understandable.

### Scenario C — Show a capital at different scales

- World scope displays a capital marker/label.
- Region scope may display a derived generalized extent if one exists.
- Locality scope displays authoritative extent geometry.
- Site scope can show separately modeled facilities, not duplicate Settlement Objects.

### Scenario D — Place multiple nearby settlements

- Each independently meaningful inhabited place has its own Settlement identity.
- Proximity/overlap is visible as spatial context.
- No parent-child or metropolitan relationship is created by proximity or overlap.

### Scenario E — Place a settlement in a PoliticalRegion

- Spatial containment may be checked.
- A creator can explicitly establish the registered PoliticalRegion → Settlement relationship where its preconditions and scope permit.
- UI language describes territory containment, not political allegiance or sovereignty.

### Scenario F — Change a settlement's classification

- The UI recognizes that classification is a definition field under the current descriptor.
- It routes through the correct definition-edit policy.
- It does not offer unsupported historical class changes.

### Scenario G — AI-assisted placement

- AI explains why it suggests a location using available terrain, water, climate and access context.
- Every observation indicates source availability and uncertainty.
- The creator can move the anchor or reject the suggestion without losing control.
- AI output remains a Candidate until the formal application boundary is crossed.

## 11. V1 non-goals

- Settlement parent/child hierarchy and metropolitan-area graphs.
- Multiple independent Settlement extent components beyond the registered 0..1 Polygon/MultiPolygon role.
- Population simulation, demographic growth, migration and carrying capacity.
- Automatic settlement classification from area/population/building density.
- Legal municipal boundaries, cadastral ownership and zoning.
- Historical or localized names, aliases and language variants.
- Historical population/condition/classification changes without a temporal attribute contract.
- Building-level urban morphology or procedural city generation.
- Road access, transport routes or network reachability as a prerequisite for Settlement validity.
- Automatic political membership, sovereignty or government control.
- Automatic conversion of derived/generalized cartography into formal geometry.

## 12. Cross-architecture acceptance criteria

ME-03-B is acceptable when:
1. Settlement uses the existing stable Object identity and descriptor.
2. Anchor is required and extent optional, with complementary semantics.
3. Anchor-only settlement is a valid first-class authoring state.
4. Extent cardinality and geometry family match the registered descriptor.
5. Classification values are descriptive and never inferred from size/population.
6. Settlement hierarchy is not represented as enabled in V1.
7. PoliticalRegion containment remains distinct from sovereignty/control.
8. Multiscale display uses one Object identity and derived presentation.
9. Geometry operations use ME-05 shared contracts and immutable Asset publication.
10. Time-zero and post-zero creation use the correct Core Domain services.
11. Definition edits are not routed through unsupported runtime attribute Commands.
12. Candidate, authorization, submission and applied states remain distinct.
13. AI suggestions disclose provenance/uncertainty and cannot bypass Core Domain.
14. The workflows remain usable without AI or GIS expertise.

## 13. Open follow-up gates

- Whether `canonical_name` and `settlement_class` should be Project-shared or Map-local definition fields under the Core Domain ownership policy.
- Whether a future temporal classification fact is genuinely required by creator scenarios.
- Whether historical/localized aliases warrant a separate naming model.
- Whether the 0..1 MultiPolygon extent is sufficient for dispersed or discontinuous settlements.
- Whether settlement anchor/extent provenance and confidence are already covered by the shared geometry/source contract or require a cross-cutting extension.
- Whether a future Settlement hierarchy is required; if so, define semantics and temporal scope before enabling any endpoint pair.
- Whether population estimates are needed and, if so, define provenance, time scope and mutation semantics before enabling the disabled runtime candidate.

## 14. Status and references

ME-03-B establishes the recommended V1 Settlement authoring model and creator workflows against the current descriptor baseline. It does not change Core Domain registries or authorize implementation. Open gates must be reconciled in ME-03-H and, where necessary, in the owning Core Domain contracts.

References:
- ME-03-00 V0.2 — Settlements & Urban Geography Editor.
- ME-03-A V0.1 — Domain Inventory & Descriptor Reconciliation.
- P2-09-18 V0.2 — Object Type Descriptor / Attribute Contract.
- P2-09-21 V0.1 — V1 Built-in Object Type Seed Catalog.
- P2-09-24 V0.1 — V1 Built-in Object Type Field and Capability Catalog.
- P2-09-26 V0.2 — V1 Object Type Descriptor Registry.
- P2-09-27 V0.2 — V1 Relationship Type Descriptor Registry.
- ME-01-05 — Map Object & Geometry Representation Model.
- ME-01-06 — Editor Interaction Framework.
- ME-05-02 through ME-05-06 — Geometry, topology, coordinate, Asset and integration contracts.
