# World View — ME-02 Hydrology & Drainage Authoring

- Version: 0.1
- Date: 2026-11-21
- Status: **Hydrology Specialist Workflow Working Baseline** — creator workflow, logical analysis pipeline and V1 network semantics recommendation; exact algorithms, numeric thresholds, field persistence and executable descriptors remain open
- Parent: [ME-02 Terrain & Natural Geography Editor](world-view-me-02-terrain-and-natural-geography-editor-v0.1.md)
- Depends on: ME-02-01 through ME-02-04; Natural Geography, Climate & Hydrology Model V1.0; Core Domain P2-09-20 V0.2 and P2-09-22 V0.2; ME-01-04/05/06/07; Map Engine spatial/geometry interaction contracts
- Scope: Drainage analysis, watershed and depression interpretation, river/lake authoring, derived flow networks, formal River relationships, validation and bounded recomputation

## 1. Purpose and recommendation

This document defines the hydrology workflow that turns land/water and elevation inputs into explainable drainage analysis, reviewable river candidates and explicitly authored formal River connectivity.

**Recommendation:** V1 hydrology should use a deterministic terrain-first pipeline with:
- explicit elevation coverage, no-data and map-edge handling;
- depression detection and a reviewable depression policy;
- a simple single-flow-direction drainage model as the initial default;
- derived flow direction, accumulation, drainage basin and channel candidates;
- creator-reviewed River/Lake semantic Objects and explicitly accepted River-to-River / River-to-Lake relationships;
- relative water-supply/flow classes only where inputs justify them;
- bounded, provenance-preserving recomputation.

The system must not draw rivers merely to connect settlements, treat every depression as an error, force all water to an ocean, or present an incomplete formal relationship graph as a complete hydrological network.

The current Core Domain relationship seed supports `hydrology.flows_to` from River to River and River to Lake. Ocean, Estuary, Watershed and Lake-to-River endpoint semantics are not in the current seed set. This is a real V1 boundary, not something the Editor may bypass with generic graph edges.

## 2. Goals and non-goals

### 2.1 Goals

- Let a creator understand where water is likely to collect and flow without requiring GIS or hydrology expertise.
- Derive drainage from elevation, basin structure and water-balance assumptions.
- Distinguish real closed basins/lakes from data artefacts and unresolved depressions.
- Identify candidate headwaters, channels, confluences, outlets and watershed divides.
- Support creator-drawn and AI-proposed River/Lake geometry while preserving formal Object identity.
- Make upstream/downstream meaning explicit and independent of line digitization order.
- Validate formal River relationships against terrain evidence and descriptor constraints.
- Preserve incomplete/uncertain outputs as derived analysis or Candidates instead of inventing formal facts.
- Support cross-Working-Scope networks and local refinement without duplicating River Objects.
- Recompute only affected hydrological and dependent climate/ecology outputs.
- Explain assumptions, resolution, limitations and likely causes of anomalies.

### 2.2 Non-goals for V1

- Full hydrodynamic simulation, Navier–Stokes flow, exact velocity or discharge prediction.
- Daily rainfall/runoff forecasting, flood-risk certification or dam/reservoir engineering.
- Detailed groundwater/aquifer modelling, karst systems, spring-source simulation or subsurface flow networks.
- Full erosion, sediment transport, delta evolution or geological-time river migration.
- Universal river topology supporting arbitrary distributaries, braided channels, anabranches and tidal estuaries.
- A formal Watershed Object Type or `water.drains_to` relationship.
- Formal River-to-Ocean, River-to-Estuary or Lake-to-River relationships until their endpoint types and semantics are adopted.
- Automatic creation of formal River/Lake Objects from every detected channel or depression.
- A generic graph database or a second network persistence model.

## 3. User-facing mental model

The creator should not need to understand flow-direction raster algorithms, accumulation thresholds or topological graph theory. The product should communicate the result in familiar concepts:

- **Where water gathers** — basins, low areas and closed depressions.
- **Where water tends to travel** — downhill paths and drainage corridors.
- **Where rivers may begin** — headwater areas supported by terrain and water supply.
- **Where streams meet** — confluences and downstream channels.
- **Where water ends up** — a lake, closed inland basin, map-edge/outlet area or unresolved destination.
- **How reliable the interpretation is** — coverage, terrain detail, assumptions and uncertainty.

Recommended natural-language interactions:
- “Show me the major river systems on this continent.”
- “Why does this river stop in the middle of the plain?”
- “This basin should be an inland salt lake with no ocean outlet.”
- “This river should join the main river from the north.”
- “Keep the existing river course, but check whether it flows uphill anywhere.”
- “The northern tributaries should be seasonal; the main river should remain perennial.”
- “Do not change the confirmed lake shoreline. Recalculate the drainage around it.”
- “Show the watershed boundaries that feed this lake.”

The Assistant should translate these requests into explicit analysis scope, assumptions, protected facts and candidate operations. It must not invent an exact discharge or claim that a river is perennial without adequate water-balance support.

## 4. Hydrology content and authority categories

Hydrology uses the same ME-01 / ME-02 authority separation.

| Content | Primary category | Formal status |
|---|---|---|
| Elevation field | Authored/imported physical field | Source input; immutable published Asset under the field contract |
| Land/water mask | Authored/imported physical field and/or derived mask | Source or derived field with declared authority |
| River | Semantic Object | Core Domain identity, centerline component(s), typed attributes |
| Lake | Semantic Object | Core Domain identity, shoreline component(s), typed attributes |
| River flow connection | Semantic Relationship | Core Domain `hydrology.flows_to` only for supported endpoint pairs |
| Watershed / drainage basin | Derived analytical result | Derived polygon/field; no formal Watershed Object in V1 |
| Flow direction | Derived field | Rebuildable analysis |
| Flow accumulation | Derived field | Rebuildable analysis |
| Drainage divide | Derived line/area | Derived analysis; not automatically a formal Object |
| Channel / stream candidate | Candidate | Not a formal River until creator/domain adoption |
| Headwater / confluence candidate | Derived analysis | Evidence for review; not automatically a formal Object or relationship |
| Seasonal/perennial tendency | Derived estimate or explicit creator constraint | Must show source and uncertainty |
| Floodplain / wetland tendency | Derived result | Limited suitability/diagnostic overlay; no detailed flood simulation |
| River styling, arrows and animated flow | Presentation | Never formal hydrological truth |

A formal River Object represents a creator/domain-meaningful river or river entity. It is not created for every raster channel segment, cell, stream-order change or tile boundary. A River may have multiple geometry components only as permitted by its Core Domain descriptor.

## 5. Inputs and preconditions

A hydrology calculation request must declare all relevant inputs and their provenance.

### 5.1 Required inputs

- Map Space and spatial reference.
- Map Extent and explicit map-edge semantics (closed boundary, wrap/seam, partial coverage, unknown/outside).
- Elevation field identity, content fingerprint, vertical unit/reference, coverage, resolution and no-data semantics.
- Land/water mask or equivalent lake/ocean/land context, including its authority and freshness.
- Calculation Working Scope and required context buffer.
- Algorithm identifier/version and all parameters.
- Depression treatment policy.
- Flow-direction method and tie-breaking semantics.
- Channel extraction method and thresholds.
- Creator-protected Objects/geometry/fields and hard constraints.

### 5.2 Optional but important inputs

- Lake shoreline Objects and water-body classification.
- Existing River centerlines and explicitly accepted flow relationships.
- Seasonal precipitation, snowmelt and broad water-availability estimates.
- Soil/infiltration class where available.
- Explicit springs, groundwater, canals, dams or other engineered/magical water-source explanations represented as World Knowledge or supported domain facts.
- Creator-authored intended source, destination, seasonal behavior or named river-system grouping.
- Existing neighboring-region derived outputs for boundary consistency.

### 5.3 Missing or weak inputs

If elevation is missing, partial, too coarse or has unresolved vertical reference:
- do not calculate a high-confidence drainage network;
- show the affected coverage and missing-data boundary;
- permit manual River/Lake sketching as provisional or authored geometry;
- mark flow relationships as unverified or require an explicit creator statement plus supported domain validation;
- do not treat no-data as zero elevation, sea level or a sink.

If land/water or map-edge semantics are unresolved, outlet classification must remain unresolved where it depends on those facts.

## 6. V1 calculation pipeline

### 6.1 Pipeline overview

```text
Map Frame + Elevation + Land/Water + Existing Objects
                         |
             Input/coverage validation
                         |
             Depression identification
                         |
           Creator/domain depression policy
                         |
          Hydrologically conditioned surface
                         |
              Flow direction field
                         |
             Flow accumulation field
                         |
          Drainage basin / divide analysis
                         |
        Headwater and channel candidates
                         |
       Confluence / outlet / sink analysis
                         |
       Water-balance and seasonality estimate
                         |
       River-network diagnostics and Candidate review
                         |
       Explicit Object / relationship adoption
```

The pipeline is dependency-aware. Not every request needs every stage. For example, checking an existing River for uphill segments may require elevation sampling and local flow evidence without rebuilding all climate outputs.

### 6.2 Input normalization and validation

Before calculation:
1. Verify the active Map/Branch/Time and exact source fingerprints.
2. Verify coordinate reference, vertical unit/reference, resolution and no-data semantics.
3. Verify coverage and map-edge/wrap context.
4. Check for seams, invalid values and unresolved overlaps between source fields.
5. Resolve whether the task is world-scale, regional or local and include required parent/neighbor context.
6. Record protected geometry/Objects and creator-authored exceptions.

No-data cells remain unknown. The algorithm may identify a boundary between known and unknown coverage, but it must not infer a coastline or drainage outlet from that boundary alone.

### 6.3 Depression identification and treatment

A depression is not automatically an error. It may represent:
- a real closed basin;
- a lake or wetland basin;
- an endorheic drainage system;
- a karst/groundwater sink (not fully simulated in V1);
- an elevation-data artefact;
- a tile seam or incomplete coverage artefact;
- an unresolved creator-intended feature.

V1 treatment classes:

| Class | Meaning | Default handling |
|---|---|---|
| Confirmed lake basin | A formal Lake or creator-confirmed water body occupies the depression | Preserve lake/shoreline; use declared lake outlet semantics only if supported |
| Confirmed closed basin | Creator intends internally drained terrain / endorheic system | Preserve the sink; flow accumulation may terminate there |
| Candidate lake/wetland | Terrain suggests a basin but semantic intent is not confirmed | Show Candidate; do not create a formal Lake |
| Data artefact candidate | Depression is likely caused by noise, seam or low-quality source | Offer a conditioning preview; preserve original elevation |
| Unknown | Insufficient coverage or evidence | Keep unresolved; do not silently fill or breach |

**Recommended V1 algorithm policy:** use a deterministic priority-flood-style depression analysis to identify connected sinks and spill elevations. Do not globally fill all depressions. Any conditioning (fill/breach/flat resolution) is a derived calculation surface, not a replacement of the authored elevation field. Preserve:
- original elevation fingerprint;
- conditioning method/version;
- changed cells/area and maximum/summary elevation adjustment;
- selected depression policy;
- parameters and affected coverage.

The default should be *identify first, condition only where justified*. A creator-confirmed closed basin must not be filled merely to make a continuous river network.

### 6.4 Flow direction

**Recommended V1 default:** deterministic single-flow-direction routing using a D8-style neighborhood model on the conditioned analysis surface, with explicit, stable tie-breaking and flat-area resolution.

Rationale:
- small, understandable and testable initial implementation;
- naturally produces a directed acyclic downstream routing graph when every routed step has a strictly decreasing rank/elevation or a deterministic flat-resolution order;
- sufficient for macro/regional river-corridor candidates and first-pass basin analysis;
- does not claim to represent all braided/distributary channel physics.

Constraints:
- Flow direction is derived from the analysis surface, not from River geometry digitization order.
- Flat areas require deterministic routing that does not create cycles or arbitrary sinks.
- The algorithm must respect no-data boundaries, map wrap/seams and coverage.
- A cell with no valid downstream neighbor may be a confirmed sink, a map-edge outlet candidate or unresolved; it is not automatically an error.
- Multi-flow-direction methods may be evaluated later if representative workloads show D8 artefacts that materially harm creator outcomes. Do not implement multiple competing algorithms in V1 without a demonstrated need.

The exact neighborhood metric, flat-resolution implementation and numeric tolerances are Map Engine/Spatial Services technical gates; this document recommends the method family, not an executable implementation.

### 6.5 Flow accumulation and basin analysis

Flow accumulation derives the upstream contributing area or weighted contribution under the selected method. V1 should treat it as a relative terrain-support indicator, not measured discharge.

Outputs may include:
- flow accumulation field;
- derived drainage divides;
- drainage basin polygons;
- basin area and coverage summaries;
- candidate stream corridors;
- candidate headwater areas;
- candidate confluences;
- candidate terminal sinks/outlets.

Basin boundaries and drainage networks may cross Working Scopes and political/terrain regions. They must be computed with parent/neighbor context and must not be clipped to the Canvas viewport.

A watershed remains a derived analysis object with provenance. Do not allocate a formal Object ID or create `water.drains_to` relationships in V1.

### 6.6 Channel and river candidates

Channel candidates are extracted from flow accumulation and terrain context using scale-aware thresholds. Thresholds must consider:
- analysis resolution and cell area;
- target map scale and intended visible river hierarchy;
- local terrain and basin context;
- available water-balance evidence;
- creator-specified minimum/maximum river density or protected corridors.

There must not be one universal accumulation threshold for all map scales. Store the selected threshold and method/version with every result.

Candidate classes may include major channel, tributary corridor and minor stream corridor. These are analysis labels, not formal Object Types or guaranteed physical discharge classes.

A candidate centerline may be generalized for display, but the adopted River geometry must retain a declared geometry precision and source lineage. Do not overstate local channel accuracy where the source elevation is coarse.

### 6.7 Confluences, sinks and outlets

The analysis should identify:
- where candidate channels converge;
- where an existing River approaches or crosses a derived drainage divide;
- where a channel reaches a known Lake shoreline;
- where a channel reaches a map edge;
- where a channel terminates in a closed basin;
- where flow destination cannot be resolved.

A geometric crossing is not automatically a confluence. A formal confluence/flow relation requires:
- compatible River Object endpoints;
- valid active endpoints in the target History;
- explicit creator/authorized acceptance;
- the registered relationship descriptor's continuity, cardinality and cycle rules.

V1 cannot formally represent every outlet:
- River→River is supported by the current seed matrix.
- River→Lake is supported by the current seed matrix.
- River→Ocean/Estuary is unsupported.
- Lake→River outflow is unsupported.
- River→generic sink/wetland/watershed is unsupported unless a supported endpoint Object Type and descriptor are later adopted.

An unsupported destination may be represented as derived terminal evidence, an unresolved outlet diagnostic or World Knowledge. It must not be encoded as an invented Relationship Type or a fake PointOfInterest Object.

## 7. Formal River network semantics

### 7.1 River Object meaning

A River Object is an independently meaningful river or named/creator-defined river entity. Its centerline geometry represents its course; the geometry's vertex order does not determine upstream/downstream semantics.

One River Object may cover multiple Working Scopes and may contain multiple geometry components only where its Object Type descriptor allows them. Do not create a new River Object for each viewport, tile, regional analysis partition or detected stream-order segment.

The creator may choose semantic River identity and grouping. The algorithm may suggest likely groupings but cannot decide them solely from connected pixels.

### 7.2 `hydrology.flows_to` direction

The relationship is directed:
- source = upstream River;
- target = downstream River or receiving Lake.

It records semantic hydrological connectivity, not a general geometric connection. It is separate from:
- River centerline geometry;
- flow-direction raster;
- derived channel continuity;
- a confluence candidate;
- map rendering arrows.

The Core Domain Relationship Type descriptor remains the authority for endpoint types, cardinality, identity, temporal behavior and active endpoint policy.

### 7.3 Recommended V1 network policy

The following is the specialist recommendation for ME-02 and must be adopted in the executable relationship descriptor before formal use:

1. **Directed acyclic graph (DAG):** a formal River flow network must contain no directed cycles.
2. **Confluences allowed:** multiple upstream River Objects may flow to one downstream River Object, subject to descriptor cardinality.
3. **One downstream receiver per River:** in the initial simple model, a River may have at most one formal downstream `flows_to` relationship at a time. This keeps the V1 graph unambiguous and avoids unsupported distributary branching.
4. **River→Lake terminal receiver:** a River may flow into a Lake. The Lake is a terminal node in the formal V1 flow graph because Lake→River outflow is not supported.
5. **No inferred relationship:** geometry intersection, endpoint snapping, raster connectivity or proximity alone does not establish `flows_to`.
6. **No implicit flow direction:** a reversed centerline does not reverse or rewrite the formal relationship.
7. **Active endpoints required:** follow P2-09-22's `active_endpoints_required` recommendation. A relationship cannot be established or remain operational if either endpoint is inactive in the selected History; endpoint deactivation must be explicitly reconciled under Core Domain rules rather than silently cascading.
8. **No implicit Object split/merge:** network segmentation or confluence detection does not split/merge River Object identities.

This policy is deliberately narrower than real-world hydrology. Distributaries, braided networks, bifurcations, deltas, tidal reversals and lake outflows remain derived/World Knowledge/unsupported topology until a richer domain model is justified.

### 7.4 Formal graph versus derived hydrology graph

The derived hydrology graph may be richer than the formal relationship graph. It may contain:
- cell-to-cell flow routing;
- channel segments;
- candidate junctions;
- watershed-to-channel associations;
- terminal sink/outlet evidence;
- multiple possible routes under uncertainty.

The formal Core Domain graph contains only creator-adopted relationships that fit registered Object Types and Relationship Type descriptors.

The UI must clearly label these:
- **Analyzed flow** — derived from elevation and calculation inputs;
- **Proposed connection** — Candidate awaiting review;
- **Confirmed flow relationship** — formal Core Domain relationship;
- **Unresolved outlet** — terminal evidence without supported formal endpoint;
- **Stale analysis** — source inputs changed since calculation.

Never show the derived graph with the same visual treatment as the formal relationship graph.

### 7.5 River network editing and relationship review

For a creator request such as “join this tributary to the main river”:
1. identify the selected upstream and downstream River Objects;
2. show the intended source→target direction in plain language and on the Canvas;
3. verify that geometry/terrain provides plausible continuity;
4. check existing relationships, active endpoint state, cycles and descriptor cardinality;
5. show any conflicts or unsupported cases;
6. request confirmation when the operation is high-impact or the direction/receiver is ambiguous;
7. establish the relationship through Core Domain using Initial State or selected History semantics;
8. report success only after the formal transaction is confirmed.

For multiple independent low-risk relationships in an authorized task, a coherent validated batch may be applied. Partial application is permitted only when dependency analysis proves the accepted subset remains valid.

## 8. River and Lake authoring workflows

### 8.1 Sketch-first River

1. Creator draws an approximate centerline or describes the route.
2. Editor shows it as a transient geometry preview.
3. System samples elevation and derives likely downstream direction, basin context and possible endpoints.
4. Assistant explains conflicts, uncertain sections and candidate confluences.
5. Creator may refine the geometry or preserve it as approximate if the descriptor allows.
6. Creator supplies/accepts River identity and required attributes.
7. Core Domain allocates identity and applies formal creation through the correct authoring target.
8. Flow relationships are reviewed separately; River creation does not automatically create every nearby connection.

### 8.2 Analysis-first River adoption

1. Creator requests major rivers or drainage analysis for a scope.
2. Calculation services derive basin and channel candidates.
3. Results display method, scale, source elevation, threshold, water-balance evidence and confidence.
4. Candidate channels are grouped for creator review; the system avoids overwhelming the creator with every minor branch.
5. Creator accepts, rejects, edits or defers selected candidates.
6. Accepted candidates become formal River Objects through the Core Domain object creation path.
7. Relationships are established only for supported, validated source/target pairs.
8. Unsupported terminal paths remain visible as derived outlet evidence.

### 8.3 Lake authoring

A Lake is a formal Object with shoreline geometry under its Object Type descriptor. Hydrology may provide:
- depression/basin evidence;
- likely contributing drainage basin;
- candidate inflowing Rivers;
- seasonal water-balance context;
- possible overflow/spill path evidence.

A River→Lake relationship may be formally established where the creator intends that River to enter that Lake and the descriptor validates the endpoints.

V1 does not model dynamic lake levels, reservoir operations, outlet channels or Lake→River outflow as formal flow relations. A creator can still author a lake shoreline and separate downstream River geometry, but the missing semantic connection must remain explicit and unresolved rather than fabricated.

### 8.4 Existing network refinement

When elevation or land/water changes:
- recompute affected derived drainage outputs;
- compare with existing formal River geometry and relationships;
- report uphill segments, new/removed candidate channels, changed basin membership and unsupported outlets;
- never silently move confirmed River geometry or change formal relationships;
- present proposed corrections as a Candidate/Change Set when they affect formal content.

## 9. Water balance and seasonal behavior

V1 water availability is an estimate, not a full hydrological simulation.

Potential inputs:
- seasonal precipitation;
- basin contributing area;
- elevation and snow accumulation/melt indicators;
- broad evaporation/temperature estimates;
- soil/infiltration class;
- known lake/wetland context;
- explicit groundwater/spring or engineered supply assumptions.

Recommended outputs:
- relative contributing-area class;
- broad water-supply confidence;
- likely perennial / seasonal / intermittent tendency, only where supported;
- uncertainty and missing drivers.

Do not calculate exact cubic-metre-per-second discharge, flood recurrence intervals, channel velocity or navigability as if physically measured. If a numeric output is shown in a future version, it must have a calibrated model, units, validation dataset and uncertainty envelope.

A creator-authored seasonal or magical water-source rule may override a natural estimate, but it must be preserved as an explicit cause/constraint. The system should explain the difference between natural estimate and creator-confirmed exception rather than rewrite the underlying terrain/climate model.

## 10. Validation and diagnostics

### 10.1 Structural and source validation

- Elevation source, coordinate reference, vertical unit/reference and content fingerprint are valid.
- Coverage, resolution, no-data and edge semantics are explicit.
- No-data is not interpreted as sea level, zero elevation or a sink.
- Analysis surface records any depression conditioning and never overwrites the authored source.
- Source fingerprints still match at result publication and formal application.

### 10.2 Terrain and drainage validation

- Flow routing does not contain cycles.
- Routed cells have valid downstream rank/flat-resolution order.
- Candidate channels follow plausible terrain descent within source resolution and uncertainty.
- Flow does not cross a confirmed divide without a valid terrain/engineered/supernatural explanation.
- Closed basins are preserved where creator-confirmed.
- Unresolved map edges are not interpreted as ocean outlets.
- Basin boundaries and channel outputs are continuous across tiles/Working Scopes within declared precision.
- Tile seams do not create artificial sinks, divides or channel breaks.

### 10.3 River geometry and network validation

- River centerline is valid under its geometry descriptor.
- Digitization order is not treated as flow direction.
- A formal `flows_to` endpoint pair uses supported River/River or River/Lake types.
- Formal graph contains no directed cycle.
- Per-River downstream cardinality follows the adopted descriptor.
- A confluence is supported by terrain/network evidence or an explicit creator-authored exception.
- Geometric intersection does not automatically create a relationship.
- River/Lake endpoint activity and selected History constraints are valid.
- Unsupported Ocean/Estuary/Lake-outflow/distributary semantics remain unresolved/derived.

### 10.4 Water-balance validation

- Perennial tendency has adequate annual/seasonal water-source evidence.
- Seasonal/intermittent labels expose their basis and confidence.
- A large river in a strongly negative water-balance basin is flagged unless supported by snowmelt, groundwater, external inflow or explicit creator rule.
- Exact discharge is not invented.
- Lake persistence and river inflow/outflow assumptions are not represented as formal facts without supported domain semantics.

### 10.5 Severity

| Severity | Meaning | Example |
|---|---|---|
| Blocking | Unsafe/invalid formal operation or invalid source/descriptor | Unsupported endpoint pair, forbidden flow cycle, stale source fingerprint, invalid geometry |
| Warning | Physically unusual or weakly supported, but may be intentional | Uphill segment within coarse data uncertainty, perennial river with weak water supply, possible divide crossing |
| Information | Method, coverage and expected limitations | D8 method, threshold, resolution, partial coverage, closed-basin assumption |

A warning does not automatically block a creator-authored exception. The exception must be explicit, traceable and visible in diagnostics.

## 11. Incremental recomputation and dependency invalidation

| Changed input | Hydrology outputs to invalidate/recompute | Further dependent outputs |
|---|---|---|
| Elevation field or vertical reference | Conditioning, flow direction, accumulation, basins, divides, channel candidates, terrain-flow diagnostics | Temperature lapse, orographic precipitation, water balance, climate/ecology suitability |
| Land/water mask or map-edge semantics | Outlet classification, lake/ocean context, coastal drainage, affected basin boundaries | Maritime climate, moisture source, coastal ecology |
| Lake shoreline / lake presence | Local depression interpretation, candidate inflows and terminal receiver context | Local water balance, wetlands and ecology suitability |
| Soil/infiltration field | Water balance, runoff and seasonal tendency | Wetland/vegetation suitability |
| Seasonal precipitation / snowmelt inputs | Water balance and seasonal river tendency | Climate/ecology and settlement water availability |
| Formal River geometry | Geometry-to-drainage comparison and affected network diagnostics | Only dependent local overlays |
| Formal `flows_to` relationship | Formal network graph, downstream/upstream inspection and network validation | Dependent network summaries |
| Cartographic river style / flow animation | No hydrology calculation | Presentation only |

Recomputation must be bounded by actual dependencies and include sufficient halo/neighbor context. A local edit must not trigger an unbounded whole-world recalculation. Derived results become stale when any declared source fingerprint or algorithm/parameter version changes.

## 12. Candidate, review and formal application

The workflow distinguishes:
1. **Derived analysis** — deterministic output, reproducible from pinned inputs.
2. **Candidate** — selected proposed River/Lake geometry, grouping, attribute or relationship intent.
3. **Formal Core Domain state** — adopted Object/Geometry/Relationship through the correct domain operation.

Candidate review must display:
- proposed Object/relationship type and semantic meaning;
- source evidence and method/version;
- geometry and upstream/downstream direction;
- affected existing Objects/relationships;
- confidence, unsupported topology and unresolved endpoints;
- hard/soft validation findings;
- exact authoring target (Initial State, History-local or Shared Definition where applicable).

Acceptance is not persistence. The final application revalidates current state, exact proposal version/hash, endpoint activity, relationship cardinality/cycle rules and authorization. The UI reports “Applied” only after the Core Domain transaction succeeds. Cancelling the AI Task does not undo already committed Objects or relationships.

Low-risk, explicit manual River sketching may use the simple direct authoring flow. Large network generation, mass candidate adoption, destructive geometry changes or high-impact network rewiring should use reviewable Candidates/Change Sets.

## 13. Error and recovery behavior

- **Incomplete elevation:** preserve manual editing; show the unavailable drainage area and lower confidence.
- **No-data gap:** stop routing across unknown cells unless a declared boundary policy supports it; do not bridge silently.
- **Unresolved depression:** retain the sink and ask for creator intent only if it materially changes the result.
- **Conditioning artefact:** discard the derived conditioning preview without changing source elevation.
- **Stale source:** invalidate result and rerun affected steps; never publish over newer inputs.
- **Unsupported topology:** keep derived evidence, explain missing endpoint/relationship capability, do not write a fake relationship.
- **Formal relationship conflict:** show cycle/cardinality/endpoint reason and preserve existing formal network.
- **Partial tile failure:** retain valid unrelated outputs, mark exact failed coverage and do not present the result as complete.
- **AI/provider unavailable:** deterministic analysis and manual geometry authoring remain available.
- **User cancellation:** stop unfinished calculation/adoption steps; keep already applied formal data intact.

## 14. Acceptance criteria

1. Drainage analysis is terrain-first and uses explicit elevation, land/water, map-edge and no-data semantics.
2. A depression is classified before any fill/breach; confirmed closed basins are preserved.
3. Conditioning is derived and never overwrites the authored elevation source.
4. Flow direction and River geometry digitization order are independent.
5. V1 default routing is deterministic and produces an acyclic derived cell-flow graph.
6. Flow accumulation and channel extraction record method, parameters, resolution and source fingerprints.
7. Watersheds remain derived; no formal Watershed Object or `water.drains_to` relation is invented.
8. River candidates do not allocate formal Object identity until adoption.
9. Formal `hydrology.flows_to` is only River→River or River→Lake under the adopted descriptor.
10. Formal flow graph is acyclic; confluences may be supported subject to descriptor cardinality.
11. Each River has at most one formal downstream receiver in the recommended V1 policy.
12. Ocean/Estuary, Lake→River, distributary and delta relationships remain unsupported and visibly unresolved.
13. River/Lake Object creation and flow relationship establishment are separate operations.
14. Formal relationships require explicit accepted semantics; geometry intersection/snap alone is insufficient.
15. Cross-scope basin/network continuity is maintained without duplicate Objects.
16. Water-balance outputs are relative estimates with explicit assumptions, not fabricated exact discharge.
17. Changed inputs invalidate only declared dependent outputs; style-only changes do not trigger hydrology.
18. Stale Candidate or derived output cannot overwrite newer formal state.
19. Formal application uses the correct Core Domain Initial State/History/Shared Definition service.
20. Manual authoring remains possible without AI/network services.

## 15. Open gates and ownership

| ID | Gate | Owner | Closure evidence |
|---|---|---|---|
| HY-01 | Adopt exact `hydrology.flows_to` endpoint/cardinality/cycle/distributary policy | Core Domain + ME-02 | Completed registered descriptor and validation vectors |
| HY-02 | Confirm River geometry component roles and semantic grouping rules | Core Domain | Object Type descriptor readiness |
| HY-03 | Select exact depression detection/conditioning implementation | Map Engine / Spatial Services | Deterministic fixture suite, source-preservation proof |
| HY-04 | Validate D8-style flow routing and flat-resolution strategy on representative worlds | Map Engine / Spatial Services | Test corpus for basins, flats, seams, wraps and no-data |
| HY-05 | Define scale-aware channel extraction and generalization thresholds | Natural Geography Services | Parameterized reproducible fixtures at world/region/local scales |
| HY-06 | Define water-balance/perenniality estimate limits and vocabulary | Natural Geography Services + World Knowledge | Causal input/output contract and uncertainty tests |
| HY-07 | Define field Asset ownership, tile/halo and immutable publication contract | Core Domain + ME-05/ME-07 | Field dataset/persistence contract and atomic publication tests |
| HY-08 | Define derived drainage result fingerprint and invalidation record | Map Engine + Calculation Services | Freshness/recompute tests |
| HY-09 | Define Candidate-to-River/Lake adoption and relationship batch semantics | AI Assistant + Core Domain | Exact proposal binding and atomic/idempotent application tests |
| HY-10 | Define supported map-edge terminal evidence and future outlet semantics | Map Space + Core Domain | Explicit endpoint/edge policy |

## 16. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| HY-T01 | Flat plateau with a small data-noise pit | Pit is detected; source elevation unchanged; conditioning shown as derived |
| HY-T02 | Creator-confirmed endorheic basin | Depression preserved; drainage terminates internally; no forced ocean outlet |
| HY-T03 | Partial elevation coverage at map edge | Unknown edge remains unknown; no inferred coastline/outlet |
| HY-T04 | Two tributary Rivers join one main River | Derived confluence shown; formal upstream-to-downstream relations accepted if descriptor cardinality allows |
| HY-T05 | One River has two proposed downstream branches | Candidate flagged as unsupported by V1 single-receiver policy; no formal split |
| HY-T06 | Two Rivers have a proposed cycle | Formal relation rejected with the cycle path |
| HY-T07 | River centerline digitized downstream-to-upstream | Flow analysis/relationship direction remains upstream-to-downstream |
| HY-T08 | River enters a formal Lake | River→Lake relation may be established after explicit review and validation |
| HY-T09 | River reaches ocean edge | Outlet shown as derived/unresolved; no River→Ocean relation is fabricated |
| HY-T10 | Lake has an outflow River | Lake→River formal relation remains unsupported; explain the limitation |
| HY-T11 | Elevation changes in one region | Only affected basins/channels and declared climate dependencies are invalidated |
| HY-T12 | User changes river color or flow animation | No hydrology recomputation or formal history change |
| HY-T13 | AI Task cancelled after one River was formally applied | Applied River remains; unfinished candidates are cancelled |
| HY-T14 | Existing River is deactivated while flow relation is active | Core Domain rejects or requires explicit relationship reconciliation; no silent cascade |
| HY-T15 | D8 result changes after algorithm version update | Old derived result becomes stale and retains old provenance; new output is separately versioned |

## 17. Source alignment and authority

This contract is subordinate to the authority of:
- ME-01-01 through ME-01-07 — shared editor, spatial scopes, Object/Geometry model, interaction and cross-architecture findings.
- ME-02-01 — Terrain & Natural Geography Editor product/domain baseline.
- ME-02-02 — domain inventory and current formal Object/Relationship boundaries.
- ME-02-03 — land/water, elevation, no-data, map-edge and field Asset logical contract.
- ME-02-04 — Landmass, Mountain Range/Peak and Terrain Region authoring semantics.
- Natural Geography, Climate & Hydrology Model V1.0 — Earth-science causal basis and terrain-first drainage.
- Core Domain P2-09-20 V0.2 — relationship identity, temporal behavior, endpoint activity and lifecycle.
- Core Domain P2-09-22 V0.2 — recommended `hydrology.flows_to` endpoint matrix and explicit deferred endpoints.
- Core Domain P2-09-17 V0.5 — typed payload, immutable Asset and reference validation.
- Map Engine spatial/geometry interaction and renderer contracts — deterministic queries, immutable scenes and derived presentation.

This document recommends a narrower specialist V1 policy where Core Domain leaves a choice open. The policy is not executable until the Core Domain relationship descriptor is updated and adopted by its owner.

## 18. Status

ME-02-D establishes the Hydrology & Drainage authoring workflow and recommended V1 network policy. It is a specialist working baseline, not an executable hydrology engine, frozen relationship descriptor, physical field schema or selected GIS library.

The next work item is ME-02-E: Climate & Ecology authoring, which consumes the world frame, elevation, drainage, water-balance and seasonal inputs defined by this and the preceding ME-02 contracts.
