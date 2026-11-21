# World View — ME-03-E Road & Path Network Authoring

- Version: 0.1
- Date: 2026-11-21
- Status: Specialist domain working baseline and Core Domain gap assessment; no Object Type, Relationship Type, executable descriptor, schema or implementation change is authorized
- Depends on: ME-03-00; ME-03-A/B/C/D; ME-01 shared editor/domain contracts; ME-05-01 through ME-05-06; P2-09-18 through P2-09-27
- Scope: Road and path semantics, road-segment granularity, junction formation, grade separation, connectivity, access distinction, network validation, creator workflows and V1 limits

## 1. Executive decisions

1. Reuse the existing `road` Object Type and `network.connects_to` relationship. Do not create a second Path or Street Object Type merely for visual or naming differences.
2. For V1 network authoring, a Road Object should represent a **network segment between adjacent junctions or terminal points**, not necessarily an entire named road from origin to destination. Multiple Road Objects may share a canonical name and road class.
3. A formal road connection is an explicit accepted Road-to-Road `network.connects_to` relationship. Geometry intersection, endpoint proximity, snapping, line overlap or shared screen pixels are evidence only; none creates connectivity automatically.
4. A junction is a creator-recognized network node represented in V1 by a cluster of explicitly connected Road segment endpoints. Do not add a standalone Junction Object Type in this work package.
5. At-grade intersections require authored/split segment endpoints at the intended connection point. Grade-separated crossings (bridge over road, tunnel under road, elevated crossing) remain geometrically crossing but are not connected unless an explicit transition/ramp connection is authored.
6. Road access to a Settlement is not supported by the current relationship registry. Do not overload `network.connects_to`; keep access as a spatial observation/Candidate until a distinct typed access relation is reviewed.
7. The current descriptor does not encode travel direction, access permission, speed, cost, capacity, lane count, surface, bridge/tunnel type or condition. These are not implied by road class or connectivity.
8. Keep V1 focused on creator-authored static geography and explicit connectivity. No traffic simulation, turn restrictions, route scheduling, dynamic congestion or general-purpose GIS network engine.

## 2. Current Core Domain baseline

### 2.1 Road Object

The current P2-09-26 V0.2 registry declares:
- stable Object Type key: `road`;
- geometry role: `centerline`, 1..N LineString/MultiLineString components;
- optional `canonical_name`;
- bounded `road_class`: `trail`, `local`, `regional`, `major`, `highway`, `other`;
- runtime `condition_code` disabled pending temporal contract;
- invariant: snapping does not establish connectivity;
- typed geometry operations and the existing Initial State / History-local lifecycle.

A Road is a semantic transport feature with identity, not a renderer line. Multiple centerline components do not by themselves create a junction or connected network. The existing descriptor does not define a route-level identity, path direction, travel cost, legal access, capacity or transport mode.

### 2.2 Network relationship

The current P2-09-27 V0.2 registry declares `network.connects_to`:
- symmetric Road ↔ Road only;
- canonical unordered endpoint pair uniqueness;
- distinct active endpoints required;
- Initial State and History-local temporal classes;
- explicit physical network continuity at a junction;
- geometry precondition is evidence only; touching/snapping alone is insufficient.

The broader relationship seed catalog contains Road → Settlement as a candidate concept, but the active descriptor does not enable it. The executable registry takes precedence over catalog suggestions.

### 2.3 Consequences

The current registry can express explicit Road-to-Road continuity, but it does not provide:
- a standalone Junction Object;
- a relationship endpoint coordinate or junction identifier;
- multiple independently addressable connection locations for the same Road pair;
- Road-to-Settlement access;
- directed travel or permission semantics;
- dedicated Bridge, Tunnel, Ferry or Crossing types.

This work package does not silently fill these gaps by using arbitrary JSON metadata or presentation fields.

## 3. Road identity and network granularity

### 3.1 Recommended V1 meaning

A Road Object is one continuous named/classified transport segment between two adjacent network nodes (junctions or terminal points), or a terminal segment whose end is not connected to another Road.

The same real-world named road may be represented by many Road Objects:
- all may share the same `canonical_name`;
- each has its own stable identity and centerline;
- each connects to neighboring segments through explicit relationships;
- a road name is not a route identity or a network path.

This granularity is recommended because the current relationship key identifies a unique unordered Road pair, not multiple distinct junctions between the same pair. Keeping one segment between consecutive junctions avoids representing two separate connections between the same pair of Road Objects.

### 3.2 Segment boundaries

A segment should normally end at:
- a road junction;
- a road terminus;
- a transport-mode transition that the current model can represent as a separate Road segment;
- a meaningful creator-selected break in the network.

Do not split a Road merely because:
- its line bends;
- the display tile changes;
- the name label repeats;
- the map scale changes;
- a renderer needs a shorter path.

A geometry component split is not automatically a semantic Road Object split. Object identity changes only through an explicit Core Domain operation.

### 3.3 Multiple centerline components

The descriptor permits multiple centerline components, but V1 network authoring should prefer a single continuous centerline per Road segment. Multiple components may be retained only where they are genuinely one semantic segment under an explicitly documented case; they must not be used to hide disconnected network pieces or encode multiple junctions ambiguously.

## 4. Junction semantics without a Junction Object

### 4.1 V1 junction representation

A junction is a creator-confirmed grouping of Road segment endpoints at one physical network location. It is derived from:
- the Road endpoint geometries;
- explicit pairwise `network.connects_to` relationships;
- the creator's accepted at-grade / transition intent;
- a domain validation result that the endpoints are compatible.

The junction grouping is a network interpretation and Editor/Map Engine view, not a new persistent Object Type in V1.

### 4.2 Connection acceptance

For a proposed Road A ↔ Road B connection:
1. identify the intended endpoint of each segment;
2. validate endpoint coordinates in the appropriate spatial reference;
3. apply the declared domain connection tolerance, not the screen snap radius;
4. show the proposed connection point and the two endpoint identities to the creator;
5. ask whether this is a traversable at-grade connection or a deliberate transport transition;
6. after explicit confirmation, submit the registered typed relationship operation;
7. re-resolve endpoint and Object references against the current scene/domain generation before formal application.

If the existing relationship operation cannot persist enough evidence to disambiguate which endpoints are connected, the editor must not invent hidden relationship metadata. The Core Domain descriptor/API must be extended before that scenario is enabled.

### 4.3 Multi-arm junction

A three-way or four-way junction is a set of Road segments sharing a common endpoint location and connected by explicit pairwise relationships. For N arms, the relationship graph must make the intended connected group unambiguous.

V1 can show the connected endpoint cluster as one junction marker in the editor, even though the formal stored relationships are pairwise Road-to-Road edges. Do not infer a connection between two arms merely because both are near the same marker; each required edge must be explicitly established and validated.

### 4.4 Same Road pair at multiple locations

The active relationship descriptor allows one active unordered pair per relationship type. It cannot encode two separate junctions between the same two Road Objects.

The V1 segment-between-junctions rule prevents this in ordinary authored networks. If a valid geography genuinely requires two distinct connections between the same pair of semantic Road Objects, split the geometry into separately identified Road segments at those junctions or defer the case for a descriptor redesign. Never store duplicate relationship rows or smuggle junction coordinates into arbitrary metadata.

## 5. Crossings and grade separation

### 5.1 Geometric crossing is not connectivity

The following are distinct:
- at-grade intersection: roads meet at the same traversable level and are intentionally connected;
- bridge crossing: one road passes over another without a connection;
- tunnel crossing: one road passes below another without a connection;
- elevated crossing: roads overlap in plan view but occupy different vertical levels;
- interchange: roads are connected by explicit ramps/links, not by every crossing point;
- near miss: geometries are close but do not meet within domain tolerance.

The shared topology layer reports geometric intersections and uncertainty. ME-03 applies transport meaning.

### 5.2 V1 handling

V1 supports:
- explicit at-grade Road-to-Road connections;
- non-connected geometric crossings;
- creator annotations/World Knowledge notes for bridge/tunnel/interchange story context, provided they are not represented as unsupported executable network facts.

V1 does not formally model:
- vertical clearance or elevation profiles;
- Bridge/Tunnel Objects;
- ramp/merge/diverge semantics;
- multi-level interchange topology;
- ferry services as scheduled transport edges.

Where a bridge or tunnel is represented as part of a Road's centerline, its existence does not create a connection to the crossed Road. If the road transitions onto a connected surface road at either end, those endpoint connections must be authored explicitly. The current model cannot validate engineering-grade vertical clearance.

### 5.3 Explicit level metadata is not a shortcut

Do not add an unregistered `layer`, `bridge`, `tunnel` or `z_level` attribute to Road. Such metadata would not be validated by the current descriptor and could be mistaken for executable connectivity semantics.

## 6. Road, path, trail and transport mode

The existing `road_class` is broad and includes `trail`. Use it for creator-facing transport hierarchy, not for a complete transportation taxonomy.

V1 interpretation:
- `trail`: footpath, animal trail or informal path where represented as a line network;
- `local`: local street, lane or minor connector;
- `regional`: regional road;
- `major`: major arterial or principal road;
- `highway`: controlled-access or high-capacity road in broad cartographic terms;
- `other`: creator-defined road-like feature not covered above.

Do not create a separate `path` Object Type solely because the feature is called a path, trail, lane or street. If a future mode requires materially different semantics (for example, pedestrian-only access, railway, waterway, animal migration route), it needs a separate domain decision and descriptor rather than an unbounded interpretation of `road_class`.

Road class does not guarantee actual capacity, legal classification, surface, speed or access restrictions.

## 7. Road access to Settlements and facilities

### 7.1 Access is not physical Road continuity

A Settlement may be close to a Road, touch its geometry, or have its anchor placed beside a Road. None proves that the Settlement has an entrance, road access or an authorized route.

`network.connects_to` remains Road ↔ Road. Do not add Settlement to that relation in application code without a registry change.

### 7.2 V1 access workflow

V1 may provide a derived access-candidate panel:
- nearest Road segments to the Settlement anchor or extent;
- distance from a creator-selected access point to the Road;
- whether the candidate Road belongs to an explicitly connected network component;
- uncertainty or missing access evidence.

The result is labeled **possible access / spatial proximity**, not formal access. Creator may add a World Knowledge fact or leave it unresolved. A formal Settlement access relation requires a separate Core Domain proposal with direction, endpoint semantics, cardinality, time behavior and whether the access point itself needs identity.

### 7.3 Facilities and entrances

PointOfInterest may represent a named transport or civic location when its registered category fits, but it is not a road junction or entrance model. Building entrances belong to ME-04 and must not be invented in ME-03-E.

## 8. Network analysis and validation

### 8.1 Deterministic validation

The editor may compute:
- endpoint coincidence/proximity candidates;
- connected components from accepted `network.connects_to` edges;
- isolated Road segments;
- dangling termini;
- degree of a derived junction endpoint cluster;
- geometric crossings without connectivity;
- duplicate or ambiguous Road pair candidates;
- disconnected centerline components;
- stale relationship evidence after geometry edits.

These are deterministic results based on formal geometry and accepted relationships. Every result must identify source Object IDs, geometry revisions, relationship revisions, spatial reference, tolerance and query/scene generation.

### 8.2 Validation severity

| Finding | Default severity | Meaning |
|---|---|---|
| Road endpoints coincide but no relationship exists | Suggestion | Possible connection; creator intent is unknown |
| Explicit relationship endpoints do not satisfy tolerance | Blocking for relationship application | Formal connection evidence is invalid or stale |
| Two roads cross in plan view without connection | Informational | Could be grade-separated or intentionally disconnected |
| Road segment has one unconnected endpoint | Informational | May be a valid terminus |
| Road segment has no connection at either endpoint | Warning | May be isolated or intentionally standalone |
| Same pair has multiple distinct candidate crossings | Blocking ambiguity | Current relation cannot identify multiple junctions for the same pair |
| Connected relationship references inactive endpoint | Blocking | Existing descriptor requires active endpoints |
| Geometry edit changes endpoint location | Revalidation required | Existing relation must not be assumed valid without re-resolution |

No geometry edit may silently establish, remove or retarget a formal relationship. If an endpoint moves away from its connected partner, the editor must surface stale connectivity and require a typed correction.

### 8.3 Connected components are derived

Connected-component membership is computed from the active formal relationship graph. It is not an independently authored Object or a route. A connected component can contain several disconnected-by-mode or access-restricted subgraphs only when those semantics are explicitly modeled later; V1 makes no claim about legal traversability.

### 8.4 No route planning claim

V1 may highlight a connected path through the graph only as structural connectivity. It cannot promise shortest travel time, legal route, one-way compliance, vehicle suitability or accessibility because the necessary attributes are not in the current descriptor.

## 9. Temporal and authority semantics

Road authoring must use the existing distinct Core Domain write paths:

- **Map Initial State:** Roads and connections present at time zero.
- **History-local creation/change:** Roads introduced, removed from presence, geometrically changed or connected/disconnected at a selected Map/Branch history point, only through enabled typed operations.
- **Shared Object definition edit:** Current shared Road definition correction, retroactive according to the shared-definition contract; not an in-world historical road event.
- **Presentation:** line width, color, casing, labels and visibility; never changes formal network connectivity.

Examples:
- A new road built in a later era is a History-local creation.
- A bridge destroyed in one Branch may cause a History-local connectivity change only if the current model has an enabled typed operation for that relationship and the creator confirms the change.
- Correcting a misdrawn current base centerline is a definition correction, not a historical event.
- A road drawn on a map but not formally connected remains a disconnected Road segment.

The current `condition_code` is disabled. Do not use it for damage, closure, construction state or bridge condition until a temporal vocabulary and command contract are approved.

## 10. Creator workflows

### 10.1 Draw a connected road

1. Creator chooses **Road** and a broad road class.
2. Draws a centerline between intended junctions or termini.
3. Editor checks geometry validity, coordinate reference, precision and self-intersections.
4. Nearby road endpoints/intersections are shown as connection candidates, not auto-snapped semantic connections.
5. Creator chooses whether to connect to a specific Road segment endpoint.
6. Editor previews the junction cluster and all affected pairwise relationships.
7. Creator confirms the intended connection.
8. Formal application uses the registered typed operation, correct Map/Branch/time context and current object/scene generation.

### 10.2 Draw a crossing bridge or tunnel

1. Draw the crossing Road geometry.
2. Editor identifies plan-view intersection with another Road.
3. Creator marks the crossing as non-connected in the current V1 network model.
4. The editor shows a crossing marker with an explanatory note, not a junction.
5. If the crossing road connects to other road segments at its ends, those connections are authored separately.

### 10.3 Split a Road at a new junction

1. Select the existing Road and the intended new junction location.
2. Preview the proposed split and resulting segment identities.
3. Preserve the original semantic Road identity according to the Core Domain split policy; do not assume geometry component split equals Object split.
4. If Core Domain has no registered typed Object split/replacement operation, retain the operation as a Candidate and do not mutate the formal Object.
5. After an approved split policy exists, connect each new segment endpoint explicitly to the other incident segments.

This is a key implementation gate: the current geometry component operations do not automatically authorize splitting one Road Object into multiple Road Objects or define identity lineage.

### 10.4 Review a Settlement's possible access

1. Select a Settlement.
2. Editor displays nearby Roads and distances as spatial evidence.
3. Creator identifies a plausible entrance/access location if known.
4. Editor shows whether the candidate Road is in an accepted connected component.
5. Result remains "possible access" until a formal access relation is introduced and confirmed by the Core Domain.

## 11. AI Assistant responsibilities

AI may:
- interpret creator descriptions such as "connect the market road to the north gate";
- propose road class and candidate geometry;
- identify likely at-grade intersections and possible grade-separated crossings;
- explain disconnected components, dangling endpoints and ambiguous junctions;
- compare candidate network layouts and summarize deterministic validation.

AI must not:
- infer formal connectivity from line intersection, proximity or snapping;
- invent bridge/tunnel elevation or clearance;
- claim a Settlement has road access based only on proximity;
- add unsupported attributes or relationships;
- split or merge formal Road identities without an enabled typed operation and authorization;
- promise route feasibility, travel time or legal access without a dedicated model.

## 12. V1 non-goals

- Traffic flow, congestion, capacity, speed and travel-time simulation.
- One-way rules, turn restrictions, tolls, access permissions and vehicle profiles.
- Public transit schedules and service frequency.
- Formal railway, shipping lane, ferry timetable, air route or animal migration networks.
- Engineering-grade bridge/tunnel profiles, vertical clearance and structural properties.
- Dedicated Junction, Interchange, Bridge, Tunnel, Entrance or AccessPoint Object Types.
- Automatic junction inference or relationship creation from geometry.
- Full routing engine or navigation instructions.
- Automatic Road split/merge identity migration without a Core Domain contract.
- Legal road hierarchy, ownership, maintenance authority or road-condition history.

## 13. Core Domain gaps and implementation gates

### 13.1 Gate E1 — Road segment and identity lineage

The current `road` descriptor permits multiple line components but does not define semantic segment splitting, identity lineage or replacement semantics. Before enabling split-at-junction as a formal operation, Core Domain must define:
- whether one Road identity can be replaced by two or more segment identities;
- how names and classifications propagate;
- how historical relationships are ended/re-established;
- how existing Branch histories replay;
- whether split is reversible and how undo/compensation works.

### 13.2 Gate E2 — Connection endpoint evidence

The current `network.connects_to` relation is Road-pair unique and has no declared junction endpoint reference. V1's segment-between-junctions rule is a workable authoring convention, but formal application must confirm that the Core Domain command can validate the intended endpoint pair. If it cannot, extend the typed relationship contract before production use; do not attach opaque metadata.

### 13.3 Gate E3 — Settlement access

If the product needs formal Road-to-Settlement access, propose a separate directed access relationship with:
- explicit Road endpoint and Settlement access-point semantics;
- cardinality (a Settlement may have multiple access points);
- Initial State and History-local behavior;
- distinction between physical access and legal permission;
- validation and evidence lifecycle.

Do not add Settlement to `network.connects_to`.

### 13.4 Gate E4 — Grade separation

A future model may need vertical level or typed crossing structures. This is not required for V1 structural connectivity, which simply leaves grade-separated crossings unconnected. Add a typed model only when real authoring scenarios require validation or editing of bridge/tunnel/interchange structure.

### 13.5 Gate E5 — Relationship revalidation

Geometry changes must revalidate affected Road connections. Define whether a failed precondition blocks the geometry edit, marks the relationship stale, or requires a combined geometry-and-relationship command. The Editor must never silently delete or retarget the relationship.

## 14. Cross-architecture acceptance criteria

ME-03-E is acceptable when:
1. Existing Road and `network.connects_to` descriptors are consumed exactly as registered.
2. Road Object identity is distinguished from a named road and from renderer line geometry.
3. V1 road segment granularity avoids multiple junction locations for one unique Road pair.
4. A formal junction requires explicit accepted Road-to-Road connectivity.
5. Geometric crossings and snapping never imply connectivity.
6. At-grade and grade-separated crossings are explained and visually distinguishable without fabricating unsupported persisted attributes.
7. Road-to-Settlement access remains a separate unsupported semantic until a descriptor is adopted.
8. Connected components are derived from accepted relationships.
9. Road geometry changes trigger freshness and connectivity revalidation.
10. Temporal writes use the correct Map Initial State, History-local or shared-definition authority path.
11. Road split/merge identity changes remain blocked until Core Domain defines typed lineage operations.
12. AI proposes and explains but does not bypass creator confirmation or Core Domain authorization.
13. UX uses creator language and does not require GIS/network-engine expertise.

## 15. Status and references

This document establishes the ME-03-E specialist working baseline and records Core Domain gaps. It does not change the active Object Type or Relationship Type registry and does not authorize implementation.

References:
- ME-03-A V0.1 — Domain Inventory & Descriptor Reconciliation.
- ME-03-B V0.1 — Settlement Model & Authoring Workflows.
- ME-03-C V0.1 — Political & Administrative Geography.
- ME-03-D V0.1 — Land Use & Functional Areas.
- ME-05-02 V0.1 — Shared Geometry Model & Component Operations.
- ME-05-03 V0.1 — Topology & Spatial Predicate Contract.
- ME-05-04 V0.1 — Coordinate Reference, Precision & Measurement Contract.
- ME-05-06 V0.1 — Cross-architecture Integration Review & V1 Gates.
- P2-09-26 V0.2 — V1 Object Type Descriptor Registry.
- P2-09-27 V0.2 — V1 Relationship Type Descriptor Registry.
- P2-09-22 V0.2 — V1 Built-in Relationship Type Seed Catalog.
