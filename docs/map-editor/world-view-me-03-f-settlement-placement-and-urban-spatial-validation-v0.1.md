# World View — ME-03-F Settlement Placement & Urban Spatial Validation

- Version: 0.1
- Date: 2026-11-21
- Status: **Specialist Spatial-Validation Working Baseline** — product and validation semantics recommended; no Core Domain descriptor, schema, relationship endpoint, calculation algorithm or implementation change is authorized
- Parent: [ME-03 Settlements & Urban Geography Editor](world-view-me-03-settlements-and-urban-geography-editor-v0.2.md)
- Depends on: ME-03-A through ME-03-E; ME-02 Terrain & Natural Geography; ME-01-02 World Knowledge and Story-Spatial Bridge; ME-01-04 Multi-scale Spatial Consistency; ME-05-03 Spatial Predicates; ME-05-04 Coordinate Reference, Precision & Measurement; Core Domain P2-09-26 Object Type Registry V0.2 and P2-09-27 Relationship Registry V0.2
- Scope: Settlement placement, terrain/water/climate context, possible road access, hard/soft/derived constraints, explainable diagnostics, uncertainty, and bounded revalidation

## 1. Purpose and disposition

ME-03-F defines how World View evaluates whether a proposed or existing Settlement location is spatially plausible in the context of its natural geography and authored urban geography.

The product must help a creator answer questions such as:

- Is this town placed on a plausible area of land?
- Is the site too steep, exposed, isolated, or poorly supplied with water for the intended settlement?
- Does the nearby river or lake provide useful geographic context, and is that conclusion actually supported?
- Is the settlement close to a connected road network, or is it merely near a road line?
- Which conditions are confirmed facts, which are creator preferences, which are deterministic checks, and which remain uncertain?
- If the creator intentionally chooses an unusual location, what should be acknowledged rather than silently “corrected”?

**Recommendation:** use a small, explainable validation pipeline that combines shared spatial predicates with ME-02 source/derived evidence and ME-03 semantic interpretation. It is not a general-purpose GIS suitability engine, population model, economic model, or automatic settlement-placement optimizer.

The system should produce a set of named findings and candidate alternatives, not a single opaque “suitability score”. A summary may group findings by severity, but must preserve the individual evidence and reason.

ME-03-F does not add Object Types, Relationship Types, runtime attributes, Command descriptors, physical tables or a new spatial analysis engine.

## 2. Existing domain baseline and hard boundaries

### 2.1 Settlement remains the existing semantic Object

The current Core Domain `settlement` descriptor remains authoritative:

- exactly one required Point geometry role: `anchor`;
- zero or one optional Polygon/MultiPolygon geometry role: `extent`;
- required `canonical_name`;
- one bounded `settlement_class`: village / town / city / capital / other;
- population and condition runtime fields remain disabled;
- runtime attribute mutation remains disabled.

The anchor is the settlement's representative inhabited location. It is not automatically the geometric centroid, road access point, riverbank, or administrative center. The optional extent is the creator's mapped footprint, not a legal boundary. An anchor-only Settlement is valid.

Placement validation must therefore work with an anchor alone. Extent-aware checks are additional evidence, not a prerequisite for evaluating a Settlement.

### 2.2 Reuse current natural-geography sources

ME-03 consumes, but does not redefine or recalculate, ME-02 content:

| Input | Role in placement validation | Important limitation |
|---|---|---|
| Elevation Field Dataset / Revision | Landform and relative elevation context; terrain-derived slope where supported | Respect field coverage, units, vertical reference, resolution and no-data |
| Land/water Field | Detect known land, water, unknown and outside-coverage | Unknown/no-data/outside coverage are not land or water |
| Terrain features / TerrainRegion | Named landform context such as plain, hill, mountain, wetland or forest | Classification is not a complete engineering/site survey |
| River and Lake Objects | Creator-authored water geography and proximity context | Formal hydrology relations remain owned by ME-02 |
| Drainage / watershed / channel analysis | Derived drainage context and water-collection evidence | A derived basin/channel is not automatically a formal River, Lake or Watershed Object |
| Seasonal climate estimates | Temperature, precipitation, moisture and seasonal context | Estimates retain source assumptions, coverage, algorithm and uncertainty |
| Biome / habitat suitability | Ecological context where relevant to the story | Suitability is not proof of an actual ecosystem or resource |

A placement result must identify the exact source revision/fingerprint and derived-artifact provenance used. Stale, incomplete, unsupported or out-of-coverage inputs must be visible in the result.

### 2.3 Reuse current urban-domain semantics

- Road and Road-to-Road `network.connects_to` remain the only enabled transport network model in this workstream.
- PoliticalRegion containment and adjacency keep their existing narrow meanings; they do not imply jurisdiction, ownership, service provision or political control.
- FunctionalArea remains a ME-03-D proposal, not an enabled Object Type.
- Settlement-to-Settlement hierarchy remains deferred.
- Road-to-Settlement access remains unsupported by the current Relationship Registry.

A nearby road is not proof of access. A nearby river is not proof of potable water, navigability, irrigation, or year-round supply. A nearby PoliticalRegion is not proof of governance or service coverage.

## 3. Placement validation mental model

The creator should see a practical explanation in ordinary language. The system may use spatial and physical calculations internally, but must not require the creator to understand GIS terminology.

A validation result is composed of independent findings:

1. **Location facts** — what is explicitly known about the proposed anchor and extent.
2. **Physical context** — land/water, elevation, terrain, water bodies, drainage and climate evidence.
3. **Urban context** — nearby Settlements, Roads, possible network proximity, and authored FunctionalArea candidates where available.
4. **Constraints** — creator-confirmed requirements and preferences.
5. **Interpretation** — specialist rules that explain what the evidence means for this Settlement class and creator intent.
6. **Uncertainty and coverage** — what cannot be established from current sources.
7. **Recommended next actions** — inspect, clarify, adjust, accept as intentional exception, or leave unresolved.

Do not collapse these dimensions into one Boolean “valid/invalid” state.

### 3.1 Validation outcome vocabulary

Each finding uses one of the following outcomes:

| Outcome | Meaning | Product behavior |
|---|---|---|
| Pass | The tested condition is supported and satisfied | Show the supporting evidence |
| Warning | A soft preference or plausibility concern is not met | Explain trade-off; do not block by default |
| Blocked | A confirmed hard constraint is violated, or a required formal precondition fails | Prevent the affected formal operation until resolved or explicitly amended by the creator |
| Unknown | Evidence is missing, stale, incomplete or insufficient | State what is unknown; do not invent a result |
| Unsupported | The requested analysis is outside V1 capability or lacks a supported method | Explain the boundary and offer a simpler supported check |
| Acknowledged exception | The creator intentionally accepts a non-blocking anomaly or overrides a creator-owned constraint through the explicit change path | Preserve the acknowledgement and affected evidence; do not rewrite the underlying source fact |

These are finding outcomes, not new Core Domain Object lifecycle states. They belong to validation/diagnostic and Candidate evidence.

A validation summary may say “2 warnings, 1 unknown”, but it must not turn Unknown into Pass or Blocked.

## 4. Constraint and evidence classes

ME-03-F adopts the ME-01-02 and ME-01-04 distinction. Constraint authority must remain explicit.

### 4.1 Confirmed hard constraints

A hard constraint is an explicit creator-confirmed condition that the placement proposal must preserve unless the creator deliberately changes that condition.

Examples:

- the Settlement must remain on a confirmed island or landmass;
- the anchor must remain inside a creator-confirmed placement boundary;
- the Settlement must not occupy a protected or forbidden area declared by the creator;
- a named capital or sacred location must remain at its confirmed location;
- a specific water body or coastline must remain unchanged.

A hard constraint can block the affected Candidate/application. Only an explicit creator-authorized change can amend it. The system must show consequences and affected dependent checks before applying the amendment.

ME-03-F does not invent a new persistence model for hard constraints. It consumes constraints represented by the existing World Knowledge / Spatial Intent and formal domain contracts. If a constraint cannot be represented with current authority and provenance, keep it as an unresolved creator instruction or Candidate input; do not silently persist it as a new formal attribute.

### 4.2 Soft preferences

Soft preferences guide recommendations and may be traded off. They do not block by default.

Examples:

- prefer gentle terrain;
- prefer access to a reliable water source;
- prefer proximity to an established road network;
- prefer a sheltered valley or coastal plain;
- avoid flood-prone lowlands where the available analysis supports that concern;
- keep a reasonable distance from a neighboring city;
- prefer a climate suitable for the creator's intended settlement role.

A preference must not be promoted to a hard constraint by AI inference. The creator may explicitly confirm a preference as a hard constraint through the applicable World Knowledge / intent workflow.

### 4.3 Derived constraints and deterministic rules

Derived constraints are consequences of confirmed data or deterministic calculations, not creator-authored facts.

Examples:

- a point is outside a known land polygon;
- an anchor lies outside its own declared Settlement extent;
- a point falls in a known water cell;
- a proposed extent intersects a confirmed water body;
- the nearest connected Road component is separated by a known water barrier;
- a local slope estimate exceeds a declared rule threshold.

Derived constraints may block a particular operation only when the rule is deterministic, its inputs are valid and fresh, and the domain contract explicitly defines it as a hard precondition. Otherwise they produce a warning, unknown or unsupported finding.

### 4.4 Creator assertions, AI inference and physical evidence

Keep these sources distinct:

- **Creator assertion:** an intentional fact or constraint supplied by the creator.
- **Formal map fact:** a Core Domain Object, geometry, Field Revision or accepted relationship.
- **Derived physical evidence:** deterministic ME-02 output with source fingerprints and method provenance.
- **Spatial predicate evidence:** ME-05 query result bound to the relevant geometry and scene/source generation.
- **AI inference:** an interpretation or hypothesis that explains or connects evidence; never a substitute for a confirmed fact.
- **Unknown:** a missing or insufficiently supported proposition.

Confidence scores do not replace source type, provenance, confirmation state, temporal scope or impact analysis.

## 5. V1 validation dimensions

### 5.1 Anchor and extent consistency

Always validate the Settlement's own anchor/extent relationship when an extent exists:

- anchor inside or on extent: Pass;
- anchor outside extent: Warning by default, because the extent may be approximate or schematic;
- anchor outside a creator-confirmed strict extent constraint: Blocked;
- predicate Indeterminate: Unknown with the reason;
- unsupported geometry/reference combination: Unsupported.

Never move the anchor or reshape the extent automatically to make the check pass.

An anchor-only Settlement remains eligible for all checks that require only a point.

### 5.2 Land and water placement

For the anchor and, where useful, the Settlement extent:

- known land: Pass for a land-placement expectation;
- known water: Blocked only if a confirmed hard constraint requires land; otherwise Warning or intentional water-settlement scenario requiring creator intent;
- unknown cell / no-data / outside coverage: Unknown;
- partial field coverage: report the evaluated and unevaluated area separately.

Do not assume every Settlement must be terrestrial. Ports, stilt settlements, floating settlements, underwater settlements and other unusual concepts may be valid creator intent, but V1 must not claim the relevant engineering or hydrological feasibility. If the required domain semantics are absent, retain a creator-authored exception/World Knowledge explanation rather than fabricate a formal physical model.

Land/water classification must come from the authoritative Field Dataset or other declared formal source, not from renderer color or a visual palette.

### 5.3 Elevation, slope and terrain form

The system may use ME-02 elevation and supported terrain derivatives to report:

- local elevation range and variation;
- local slope class or slope estimate where the calculation contract supports it;
- whether the anchor/extent intersects a known mountain, steep terrain, wetland, plain or other TerrainRegion;
- whether a proposed footprint spans sharply different terrain conditions.

Rules:

- distinguish point sample from area summary;
- preserve units, vertical reference, resolution and method;
- report the evaluated area and coverage;
- do not present a coarse field as a site survey;
- do not invent engineering thresholds or universal “buildable slope” values;
- any threshold used for a warning must be a declared, versioned recommendation profile or creator preference, not an implicit universal fact.

A steep site may be intentional (fortress, cliff town, mountain monastery). It should produce an explained warning, not automatic rejection, unless a creator-confirmed hard constraint says otherwise.

### 5.4 Water availability and hydrological context

V1 may provide explainable proximity/context checks against formal River/Lake geometry and available ME-02 drainage outputs:

- nearest known River/Lake and measured distance;
- whether the water feature is formal or only a derived channel candidate;
- whether the nearby water body's source and freshness are known;
- whether drainage analysis suggests a basin, channel or water-collection context;
- whether seasonal precipitation/moisture estimates indicate a broad wet/dry tendency.

Important limits:

- proximity does not prove access to water;
- proximity does not prove water quality, drinkability, perennial flow, navigability, irrigation capacity or legal rights;
- a derived channel candidate is not a formal River;
- a River's formal downstream relationship is not a guarantee of local water availability;
- exact flow, discharge, flood level, groundwater, aquifer and water-demand calculations are out of scope.

Use wording such as “near a mapped river”, “within the analyzed drainage area”, “water availability is uncertain” rather than “has reliable drinking water”.

If the creator asks for a water-dependent settlement plan, the Assistant may ask for or propose an explicit creator assumption (for example, wells, cisterns, canals, magical supply). Such assumptions remain World Knowledge/Spatial Intent until a supported formal domain model exists.

### 5.5 Climate and ecological context

V1 may summarize the existing ME-02 seasonal estimates and suitability outputs:

- broad seasonal temperature/precipitation pattern;
- moisture/aridity tendency;
- climate class and its method/version;
- biome or habitat suitability where relevant;
- missing seasonal phases, incomplete coverage or unsupported physical assumptions.

Do not claim that a high biome suitability means a forest or resource actually exists. Do not reject a confirmed Settlement because its climate is unusual. Irrigation, trade, technology, adaptation, magic and creator-defined exceptions may explain a settlement in an otherwise difficult environment.

A climate or ecological warning must identify its cause (for example, cold seasonal profile, low precipitation estimate, or limited source coverage), not merely show an opaque score.

### 5.6 Road proximity and possible access

Because the current Relationship Registry does not enable Road → Settlement access, V1 may report only spatial evidence:

- nearest Road segment(s) to the Settlement anchor or extent;
- distance and measurement model;
- whether the Road belongs to an explicitly connected Road component;
- possible barriers such as known water or steep terrain, where supported;
- whether the Road geometry or connectivity evidence is stale or ambiguous.

Label the result **Possible road access** or **Road proximity**, never “connected to road” or “has road access” as a formal fact.

A connected Road component means only structural continuity in the accepted Road graph. It does not establish route direction, travel permission, transport mode, passability, travel time, capacity or a valid path from the Settlement anchor to the network.

A straight-line nearest-road distance is not a route distance. If the required path/access model is unavailable, say so.

A formal Settlement access relationship requires a separate Core Domain proposal defining direction, endpoint semantics, cardinality, temporal behavior, access-point identity and validation. ME-03-F does not authorize that extension.

### 5.7 Nearby Settlements and urban context

V1 may report:

- nearby Settlement anchors/extents and distances;
- possible spatial clusters;
- proximity to existing Roads and formal water features;
- overlapping or adjacent extents as geometric evidence;
- possible conflicts with confirmed hard constraints.

Do not infer Settlement hierarchy, dependency, trade, rivalry, shared administration, population, service area or urban agglomeration as formal facts.

Settlement proximity is not `structural.part_of`. Overlapping extents do not create a parent/child city structure. Any clustering or density summary is a derived observation with declared scope and scale.

### 5.8 Land use and functional-area context

If ME-03-D's proposed FunctionalArea concept is not yet adopted and enabled, ME-03-F must not treat it as formal Core Domain input.

Creator-drawn or AI-proposed functional areas may be displayed as Candidate/context with explicit status. TerrainRegion remains physical terrain/ecological/land-cover classification, not residential/commercial/industrial zoning.

When a formal FunctionalArea model becomes available, integration must be added through a later reviewed contract rather than assumed here.

## 6. Explainable rule model

V1 should use a small catalog of specialist checks, not a configurable general-purpose rule engine.

Each validation finding should carry, where applicable:

- stable rule/check key and version;
- target Settlement Object ID and anchor/extent role;
- result outcome;
- plain-language summary and explanation;
- source Object/Field Dataset/Field Revision/Derived Artifact references;
- source fingerprints and analysis provenance;
- evaluated spatial scope and coverage;
- spatial reference, units and measurement method where relevant;
- applicable creator constraint/preference reference;
- predicate/calculation status, including Indeterminate/Unsupported/StaleSnapshot;
- suggested next action;
- whether the finding blocks the current operation or is advisory.

Rule thresholds must be explicit and versioned. Do not hide numeric thresholds in prompts or model-generated prose. Do not let the LLM invent numeric measurement values.

### 6.1 Example V1 checks

| Check | Evidence | Default interpretation |
|---|---|---|
| Anchor within own extent | ME-05 point-in-area predicate | Warning if outside; Blocked only under confirmed strict constraint |
| Anchor on known land | Land/water Field sample | Pass on known land; Unknown on unknown/no-data/outside |
| Extent overlaps known water | Polygon/mask predicate | Warning or Blocked according to creator constraint |
| Terrain steepness | Elevation-derived slope, if supported | Advisory unless a confirmed threshold applies |
| Terrain-region context | Anchor/extent versus TerrainRegion geometry | Descriptive context; no automatic semantic relationship |
| River/Lake proximity | ME-05 distance measurement to formal geometry | Context only; never proof of water access |
| Drainage context | ME-02 derived basin/channel result | Explain source and uncertainty |
| Seasonal climate context | ME-02 seasonal estimate | Advisory; creator intent may justify exception |
| Possible road access | Road proximity + accepted Road connectivity graph | Spatial evidence only |
| Nearby Settlement context | Anchor/extent distance and overlap | Descriptive; no hierarchy inference |
| Hard-constraint compliance | Confirmed creator constraint + current geometry | Block only on deterministic, supported violation |

### 6.2 No opaque composite score

Do not define a universal 0–100 settlement suitability score in V1.

If a future UI needs a compact summary, it may present grouped counts or a clearly named qualitative summary (for example, “mostly compatible, with two warnings and one unknown”). It must remain traceable to individual findings and must not imply scientific calibration.

## 7. Placement and validation workflow

### 7.1 New Settlement placement

1. Creator selects a Settlement class and places the anchor, or describes the intended place to the Assistant.
2. Assistant resolves relevant story facts and confirmed constraints; unresolved assumptions remain explicit.
3. Editor captures the intended Working Scope and target Map/History context.
4. Validation service reads current Settlement/nearby geometry and the relevant ME-02 source/derived evidence.
5. ME-05 supplies point/area predicates, distance and measurement evidence.
6. ME-03 specialist checks produce findings with provenance and uncertainty.
7. Assistant explains the result in creator language and may suggest alternative locations or trade-offs.
8. Creator selects/edits a candidate or explicitly accepts an advisory exception.
9. Formal application follows the existing Core Domain creation/geometry write path and authorization rules.
10. After application, the system re-resolves target identity and source fingerprints; stale evidence requires revalidation.

The Assistant must not create the Settlement merely because it inferred a plausible location from narrative.

### 7.2 Validate an existing Settlement

1. Select a Settlement and choose “Check location” or request a natural-language review.
2. System resolves its current anchor/extent and the current source revisions.
3. It runs only checks relevant to the selected Settlement and requested scope.
4. Findings are grouped by location, terrain, water, climate, roads and creator constraints.
5. The creator can inspect each evidence source on the Canvas.
6. Geometry changes are proposed as Candidates; no automatic relocation or geometry correction occurs.

### 7.3 Compare alternative locations

V1 may compare a small set of explicit candidate anchors or extents. For each candidate, show:

- hard-constraint pass/fail/unknown;
- terrain and land/water context;
- nearest formal River/Lake context;
- seasonal climate summary;
- possible Road proximity;
- known trade-offs and unknowns.

Do not run an unconstrained search over the entire Map, generate hundreds of candidate sites, or claim a globally optimal location. AI may suggest a few alternatives within a creator-selected scope; deterministic checks evaluate them.

## 8. Diagnostics, freshness and bounded revalidation

### 8.1 Input freshness

A validation result is valid only for the exact inputs it evaluated. Its evidence should identify:

- Settlement anchor/extent geometry fingerprints;
- relevant Field Dataset and Field Revision identities/fingerprints;
- relevant ME-02 Derived Artifact identities, algorithm versions and provenance;
- relevant River/Lake/Road/Settlement geometry fingerprints;
- relevant formal relationship revision/context for Road connectivity;
- spatial-reference and measurement method versions;
- validation rule catalog version;
- target Map/History/Working Scope.

If any required source changes, affected findings become stale. Do not silently reuse old measurements.

### 8.2 Bounded invalidation

Re-run only checks whose dependencies intersect the changed source and spatial scope:

- Settlement anchor change: rerun all anchor-dependent checks for that Settlement.
- Settlement extent change: rerun area overlap, coverage and extent-dependent checks.
- Elevation Field Revision change: rerun affected terrain checks within the changed coverage; downstream ME-02 outputs follow their own dependency graph.
- Land/water Field Revision change: rerun affected land/water checks and dependent hydrology/climate outputs as defined by ME-02.
- River/Lake geometry change: rerun proximity/context checks for nearby Settlements only.
- Road geometry/connectivity change: rerun possible-access/proximity checks for affected nearby Settlements.
- Climate/biome artifact change: rerun climate/ecology context checks for affected coverage.
- unrelated Map or Branch history change: do not invalidate unrelated Map-local results.

Map Source Field Baseline revisions have their established Map-wide retroactive scope across all Branches and Historical Times of that Map. This is not a History-local event. Post-zero Settlement/Road/River geometry changes remain scoped by their formal Map/Branch History semantics.

### 8.3 Partial coverage and boundary behavior

If a validation area crosses a field/analysis boundary:

- report the covered and uncovered portions;
- use a buffer only when the underlying ME-02/ME-05 contract permits it;
- do not silently expand Map Extent;
- do not infer neighboring terrain, climate, roads or water beyond coverage;
- do not classify unknown/outside coverage as negative evidence.

## 9. AI Assistant responsibilities and limits

AI Assistant may:

- interpret creator intent and story context;
- identify relevant constraints/preferences and ask for missing critical assumptions;
- select supported deterministic checks;
- explain physical and spatial evidence in ordinary language;
- propose a small number of alternative locations;
- summarize trade-offs and uncertainty;
- detect contradictions between creator intent and derived evidence;
- prepare a Candidate for review.

AI Assistant must not:

- fabricate elevation, slope, distance, rainfall, water supply, road access or climate values;
- promote an inference or preference into a confirmed fact;
- claim an actual forest, water source, population, economic role or administrative status from suitability/proximity alone;
- create formal relationships from geometric coincidence;
- bypass Core Domain descriptors or mutate formal state directly;
- silently relocate a Settlement or reshape its extent;
- treat a Candidate or validation report as a formal world fact.

LLM-generated prose is explanatory. Numeric and predicate evidence must come from deterministic services and retain source provenance.

## 10. Creator-facing interaction requirements

Use creator language in the UI:

- “Terrain around this location” rather than “slope raster statistics”.
- “Nearest mapped river” rather than “nearest hydrological feature geometry”.
- “Possible road access” rather than “network endpoint reachability”.
- “Not enough map information to check” rather than an unexplained null result.
- “This location conflicts with a confirmed constraint” rather than a generic validation error.

The Canvas should show the selected Settlement, relevant evidence overlays and the spatial extent actually evaluated. The Inspector should separate:

1. Placement summary;
2. Hard constraints;
3. Terrain and water;
4. Climate/ecology;
5. Roads and nearby places;
6. Unknowns and source freshness;
7. Suggested next actions.

Selecting a finding should highlight its evidence on the Canvas without changing formal geometry. Candidate alternatives must be visually distinct from formal map content.

The system must not make the creator choose a projection, precision profile, GIS predicate or calculation algorithm during ordinary placement.

## 11. Acceptance scenarios

| ID | Scenario | Expected behavior |
|---|---|---|
| F-01 | Place a village on known gentle land with current elevation and land/water sources | Show supported terrain/land evidence and pass applicable checks |
| F-02 | Place a Settlement anchor in known water | Report the known-water conflict; block only if a confirmed land-only constraint applies |
| F-03 | Place an anchor in unknown/no-data/outside-coverage area | Return Unknown; never treat it as land or water |
| F-04 | Anchor lies outside an approximate Settlement extent | Show a warning; do not move either geometry |
| F-05 | Anchor lies outside a creator-confirmed strict extent | Block the affected operation and identify the constraint |
| F-06 | Settlement is near a formal River | Report distance and source; explicitly state that water access/reliability is not proven |
| F-07 | Settlement is near a derived channel candidate only | Label it as a candidate, not a formal River |
| F-08 | Settlement is close to a Road in a connected Road component | Show possible road access/proximity only; do not create a relationship |
| F-09 | Road geometry crosses the Settlement anchor but no access relation exists | Do not claim formal access |
| F-10 | Settlement lies in a dry or cold climate profile | Explain seasonal evidence and trade-offs; do not reject a creator-confirmed intentional settlement |
| F-11 | Climate/field source is stale or incomplete | Mark affected checks stale/unknown and identify the missing source |
| F-12 | AI suggests an alternative site | Present it as a Candidate with the same explainable checks; no automatic formal creation |
| F-13 | Creator accepts an advisory warning | Preserve acknowledgement in Candidate/application evidence; do not rewrite physical source facts |
| F-14 | Elevation Field Revision changes | Invalidate affected terrain-dependent checks according to coverage and dependency scope |
| F-15 | Unrelated Map/Branch history changes | Do not invalidate unrelated Map-local validation results |
| F-16 | Candidate location is outside the selected Working Scope but within Map Extent | Require explicit scope change or supported expanded analysis; do not silently change the formal Map Extent |

## 12. Explicit V1 non-goals

- Global or exhaustive optimal settlement-site search.
- Population density, carrying capacity, demographic growth or migration simulation.
- Economic viability, trade flow, tax base, labor market or service catchment simulation.
- Traffic simulation, shortest-time routing, transport capacity or travel-time promises.
- Engineering-grade slope stability, flood certification, geotechnical analysis or water-quality assessment.
- Groundwater, aquifer, well yield, irrigation capacity or potable-water guarantees.
- Automatic creation of formal Road access, Settlement hierarchy or FunctionalArea relationships.
- Universal suitability scoring or opaque machine-learning ranking.
- General-purpose GIS suitability model builder or configurable rule engine.
- Automatic relocation, terrain reshaping or formal relationship creation.

## 13. Open implementation gates

1. **Source contract:** confirm runtime APIs expose Field Dataset/Revision fingerprints, coverage, no-data and derived-artifact provenance to specialist validation.
2. **Spatial service:** validate point-in-area, area intersection, distance and measurement outcomes against ME-05-03/04, including Indeterminate, Unsupported and StaleSnapshot.
3. **Slope derivation:** choose and test a deterministic slope method only if the ME-02 calculation contract and selected Map Engine implementation support it; thresholds remain explicit and versioned.
4. **Freshness:** prove dependency-based invalidation for source Field Revision, geometry, Road connectivity and climate/hydrology artifacts.
5. **Validation evidence:** define the concrete serialized diagnostic/Candidate evidence shape within the existing AI Assistant and Core Domain ownership model; do not add an unowned persistence table.
6. **Creator constraints:** verify the existing World Knowledge / Spatial Intent contract can represent hard constraints, soft preferences, provenance and explicit creator amendments without duplicating authority.
7. **Performance:** benchmark local checks for representative Locality/Region scopes; do not introduce whole-Map scans for ordinary placement.
8. **UX:** validate that the findings can be understood without exposing GIS vocabulary or implying scientific certainty.

No implementation gate is marked passed by this document.

## 14. ME-03-F disposition

ME-03-F is complete as a **specialist spatial-validation working baseline**. It defines the recommended product semantics, evidence classes, supported V1 checks, workflow, uncertainty, freshness and boundaries.

It does not authorize:
- a Core Domain Object/Relationship descriptor change;
- a new runtime field or physical schema;
- a Road-to-Settlement access relationship;
- a FunctionalArea adoption;
- a new calculation engine or AI persistence model;
- production implementation claims.

ME-03-G should next convert the A–F domain/workflow baselines into end-to-end UX scenarios, Canvas/Inspector behavior, Candidate review and Figma handoff. ME-03-H should then perform the cross-architecture integration review and consolidate implementation gates.

## References

- ME-03-00 Settlements & Urban Geography Editor V0.2.
- ME-03-A Domain Inventory & Descriptor Reconciliation V0.1.
- ME-03-B Settlement Model & Authoring Workflows V0.1.
- ME-03-C Political & Administrative Geography V0.1.
- ME-03-D Land Use & Functional Areas V0.1.
- ME-03-E Road & Path Network Authoring V0.1.
- ME-02 Hydrology & Drainage Authoring V0.1.
- ME-02 Climate & Ecology Authoring V0.1.
- ME-01-02 World Knowledge and Story-Spatial Bridge V0.1.
- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- Core Domain P2-09-26 V1 Object Type Descriptor Registry V0.2.
- Core Domain P2-09-27 V1 Relationship Type Descriptor Registry V0.2.
