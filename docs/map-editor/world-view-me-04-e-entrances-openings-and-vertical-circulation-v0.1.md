# ME-04-E — Entrances, Openings & Vertical Circulation

- Version: 0.1
- Status: Specialist Domain / Access Working Baseline
- Date: 2026-11-21
- Depends on: ME-04-A through ME-04-D, ME-05 shared geometry and spatial-operation contracts
- Scope: Building-owned entrances, boundary openings, interior access connections and vertical circulation
- Authority: Product/domain working baseline only; does not authorize Core Domain registry, schema, command, relationship or geometry-format changes

## 1. Purpose

ME-04-E defines how a Building expresses physical openings and intentional movement connections between its interior Spaces, Levels and exterior access points.

It closes a semantic gap left by ME-04-D: two Spaces may touch or share a boundary, but that fact alone does not mean a person can move between them. Likewise, a door symbol, window, stair graphic or geometric gap must not silently become a formal access connection.

The model therefore separates:

1. physical boundary/opening structure;
2. semantic Space and Level identity;
3. explicit access connectivity;
4. current operational condition (open, closed, locked, blocked, damaged, unknown);
5. time-specific historical state;
6. derived reachability/query results.

The target is a comprehensible creator workflow and a small, typed Building-owned access model. It is not a BIM, building-code, evacuation or indoor-navigation system.

## 2. Inherited constraints and current capability

ME-04-A established that current V1 Core Domain registries do not contain formal Building, Level, Space, Entrance, Opening or Cave Object Types, nor the interior relationship endpoint pairs needed here. ME-04-B/C/D provide product/domain working baselines, not enabled descriptors.

Consequently:

- No new Object Type, Relationship Type, Command, endpoint pair, schema or API is enabled by this document.
- Existing generic relationships such as `network.connects_to` or `spatial.contains` must not be repurposed for Space-to-Space or Building-interior access.
- ME-03-H identified a Road endpoint-identity gap. A Building entrance must not claim a formal Road junction or reuse Road connectivity without a separately adopted endpoint contract.
- ME-05 currently provides a 2D geometry baseline. This document does not introduce authoritative 3D solids, volumetric topology or 3D geometry payloads.
- Where a required typed descriptor or operation is absent, the UI must show Unsupported/Unavailable rather than silently persisting an informal relation.

## 3. Canonical model

### 3.1 Building-owned access catalog

The recommended model is a Building-owned typed child structure containing:

- **Entrance** — a stable, creator-recognizable access point at the Building exterior boundary or another explicitly designated exterior access location.
- **Opening** — a physical or authored interruption in a boundary component. It may be passable or non-passable.
- **Access Connection** — an explicit semantic connection between two supported endpoints, such as Space↔Space or Entrance↔Space.
- **Vertical Connector** — a stable connector representing a stair, elevator, ladder or ramp route between Levels, with explicit intermediate landings/segments when needed.
- **Access State** — time/context-specific operational condition, separate from the identity and geometry of the physical feature.

These are proposed Building-owned child records, not top-level Map Objects. Each child has a stable Building-scoped identity. Exact identifiers, payload shape, geometry roles and command names require Core Domain adoption.

### 3.2 Identity and ownership

- entrance_id, opening_id, access_connection_id and vertical_connector_id are stable within the owning Building.
- IDs are not derived from labels, coordinates, geometry hashes, boundary ordinals, renderer nodes or display order.
- Rename, relabel, change of operational state or geometry refinement does not change identity.
- A retired feature's ID is never silently reused.
- Each record belongs to exactly one Building in V1.
- Cross-Building facilities, shared tunnels, bridges between separate Buildings and campus-wide connector ownership are deferred to an explicit facility/site contract; do not assign one connector to multiple Building owners.

### 3.3 Physical structure versus semantic connection

An Opening describes a physical boundary feature. An Access Connection describes an explicitly authored movement relation.

They are not interchangeable:
- an opening may be a window and therefore non-traversable;
- a doorway may exist but be locked or blocked;
- two Spaces may be connected through an opening only when an explicit Access Connection is authored;
- a passage may connect Spaces without being a conventional door;
- a stair/shaft may physically exist while one or more landings or endpoint Spaces remain unknown.

The existence of geometry never creates a formal connection automatically.

## 4. Boundary openings

### 4.1 Stable boundary anchoring

Where an Opening is placed on an ME-04-D boundary, it must reference:
- the owning Building;
- a stable boundary component identity;
- a stable opening identity;
- an explicit anchor on that boundary (for example, a component-relative parameter interval or a typed geometry sub-reference);
- the boundary/geometry revision against which the anchor was authored.

The anchor must not rely solely on an absolute screen coordinate or a transient vertex index. If the boundary is edited, the system must re-resolve the anchor against the new component revision. If it cannot do so unambiguously, mark the opening stale and require review; do not silently move it to a nearby segment.

For an opening on an exterior boundary, the same principle applies to a typed Building footprint/exterior-boundary component when that component role has been adopted.

### 4.2 Bounded opening vocabulary

Recommended V1 opening kinds:

| Kind | Meaning | Traversable by default? |
|---|---|---|
| door | Door leaf or door assembly in a boundary | Potentially; requires explicit access connection |
| archway | Permanent open passage framed by an arch | Potentially; requires explicit access connection |
| passage | Authored opening without a door leaf | Potentially; requires explicit access connection |
| window | Glazed or open window feature | No |
| hatch | Access panel/opening in a floor, ceiling or platform | No, unless an explicit supported connector uses it |
| service_opening | Utility/maintenance opening | No |
| other | Explicit creator-defined bounded description | Unknown until classified |

This vocabulary describes the opening's physical role, not its current state or code compliance. A window must not become traversable merely because its geometry is large enough. A door must not be assumed passable without an explicit connection and current access evidence.

### 4.3 Door and opening state

Operational state is independent of Opening identity and geometry.

Recommended bounded state vocabulary:
- open;
- closed;
- locked;
- blocked;
- damaged;
- destroyed;
- unknown.

The exact state machine and allowed transitions require Core Domain adoption. In particular:
- closed does not mean the physical opening ceased to exist;
- locked does not mean a wall replaced the door;
- blocked does not mean the opening geometry should be deleted;
- destroyed may affect the physical feature's current condition, but identity/history remains traceable;
- unknown must never be treated as open.

Where door-leaf direction, swing or double-door behavior is shown, it is presentation/operational detail and must not redefine the boundary opening identity.

## 5. Access Connections

### 5.1 Explicit endpoint model

The recommended V1 Access Connection endpoints are:

- Space endpoint: (Building ID, Space ID), optionally qualified by a Level ID when the Space has multiple Level Slices;
- Entrance endpoint: (Building ID, Entrance ID);
- Exterior endpoint: a Building-owned explicit exterior access anchor, where no formal external Map Object relation is yet available.

A connection has two explicit endpoints and a declared connection kind. Endpoint type compatibility, cardinality, direction and uniqueness must be validated by a future typed descriptor.

Do not use polygon touch, shared boundary, overlap, line intersection, door symbol or visual proximity to create an Access Connection.

### 5.2 Connection kinds

Recommended bounded V1 kinds:
- doorway;
- open_passage;
- archway;
- hatch_access (only with a supported vertical/plan endpoint contract);
- other_explicit.

The Access Connection references the relevant Opening where one exists. An open passage may be represented by a typed opening without a door leaf. The connection remains a separate semantic record.

### 5.3 Directionality

The model should support:
- bidirectional connection;
- A-to-B only;
- B-to-A only;
- unknown direction.

Direction is an authored movement constraint, not inferred from geometry. V1 need not implement a general directed indoor route planner. If direction is unsupported by the adopted descriptor, retain the connection as direction-unknown and do not claim directional reachability.

### 5.4 Exterior access and ME-03 Road integration

An Entrance may connect an interior Space to an explicit exterior endpoint. This lets creators describe a front door, rear entrance, loading access or courtyard entrance without fabricating a Road relationship.

Rules:
- an Entrance is a Building-owned identity, not a Road Object;
- an exterior endpoint may be a Building-local anchor or a typed Map-space reference only after the relevant contract exists;
- geometric proximity to a Road, Path, Settlement or site boundary is evidence for review, not a formal connection;
- do not reuse `network.connects_to` until ME-03's Road endpoint identity gap and a compatible cross-domain endpoint contract are resolved;
- a Building entrance can exist even when no Road or Path is mapped;
- multiple entrances may lead to different Spaces and exterior access contexts.

A future integration may explicitly connect a Building Entrance to a stable RoadEndpoint or another adopted exterior access endpoint. It must not infer a junction from nearest-line snapping alone.

## 6. Vertical circulation

### 6.1 Connector identity and kinds

A Vertical Connector is a stable Building-owned child record. Recommended kinds:

- stair;
- elevator;
- ladder;
- ramp;
- other_explicit.

It references stable source and target Level IDs within the same Building. The Level IDs come from ME-04-C and must not be replaced by labels or display order.

The connector may also reference:
- one or more intermediate Level IDs;
- explicit landing identities;
- ordered connector segments;
- optional plan geometry/component references;
- explicit endpoint Space IDs or access nodes;
- direction/capability metadata only when supported.

### 6.2 Multi-storey continuity

A stair spanning Ground, Level 1 and Level 2 should normally be one logical Vertical Connector with an ordered route/segment structure and explicit landings, not three unrelated top-level connectors and not a collection of independent Map Objects.

Recommended shape:
- one stable vertical_connector_id;
- ordered route stops, each referencing a Level and optional landing/Space endpoint;
- ordered segments between consecutive stops;
- segment kind/geometry reference where supported;
- explicit route continuity and endpoint completeness.

A connector is not considered continuous merely because its drawn segments overlap in plan or appear vertically aligned. Every segment must have resolvable adjacent stops and a valid Building-local vertical context.

V1 remains 2D plan plus scalar Level elevation. A stair's 3D volume, slope surface, riser geometry and collision model are deferred. A schematic connector route is not a full 3D stair model.

### 6.3 Connector semantics

- **Stair**: a fixed route between Levels; no inference of exact rise/run, safety or code compliance.
- **Elevator**: a vertical transport connector with explicit served Levels; do not infer that every Level is served.
- **Ladder**: a vertical access connector; do not imply safe or public access.
- **Ramp**: an inclined route concept; V1 does not validate slope, landing dimensions or accessibility.
- **Shaft/void**: a spatial opening or void, not itself a traversable connector. An elevator or ladder may use a shaft, but the shaft geometry does not create access.

A vertical connector may be physically present while unavailable, out of service, locked or blocked. Physical existence and current reachability remain separate.

### 6.4 Endpoint and Level validation

Before accepting a connector:
- source and target Levels must resolve within the owning Building;
- every intermediate Level must resolve within the same Building;
- route order must be deterministic and contain no accidental duplicate stops;
- every segment must connect the two consecutive declared stops;
- a multi-level route must not skip an intermediate Level unless explicitly declared;
- plan anchors and geometry references must resolve against current component revisions;
- missing vertical datum must not be replaced with zero;
- no world altitude or full 3D geometry is inferred from renderer Z.

Cross-Building connectors are Unsupported in V1 unless a later facility contract explicitly adopts them.

## 7. Access graph and reachability

The recommended formal access graph is a Building-owned typed structure:
- nodes: supported Space/Entrance/exterior access endpoints;
- edges: explicit Access Connections and Vertical Connector route segments;
- edge attributes: kind, direction, referenced Opening, current state and provenance.

It is not a generic graph inferred from all spatial geometry. It is populated only by explicit typed authoring operations.

A reachability query may answer whether a route is supported by the currently authored graph and state evidence. It must return:
- reachable, with the explicit connection chain and evidence;
- not reachable within the known modeled graph;
- indeterminate, where state, endpoint, direction or coverage is unknown;
- unsupported, where the requested route requires a capability not implemented.

“Not reachable in the modeled graph” must not be presented as proof that no physical route exists in the real or fictional Building. Incomplete plans and unmodeled openings are common.

V1 recommendation: provide local connection inspection and simple bounded reachability checks only. General route optimization, navigation instructions, crowd simulation and emergency evacuation are deferred.

## 8. State, time and history semantics

Keep these layers distinct:

| Change | Meaning / authority |
|---|---|
| Correct current Building opening/connector definition | Building definition service, shared or Map-local according to Building ownership |
| Initial entrances, openings and access graph at time zero | Building initialization + Map Initial State contract |
| Construct/remove/relocate an opening after time zero | Selected Map/Branch History typed command, if adopted |
| Door opens/closes/locks/unlocks during history | Selected Map/Branch History state command, if adopted |
| Temporary obstruction or elevator outage | Time/context-specific Access State; history semantics require typed support |
| Change of label, icon, selection or display | Editor/Presentation state |
| Map elevation Field Revision | Map Source Field Baseline; does not create/destroy Building access features |

A shared definition correction is not a historical event. A Map Source Field Revision is not a construction event. A current operational state change must not mutate the Opening's stable identity or delete its geometry.

Current registries do not provide the required Building child catalog and access-state commands. Until Core Domain adopts them, the editor must not claim formal historical door locking, connector outage, opening destruction or access-graph mutation. It may display imported/reference information as non-authoritative context, clearly marked as such.

## 9. Validation and diagnostics

Validation is scoped to:
- Building definition revision;
- Space and Level catalog revisions;
- boundary and geometry component Asset fingerprints;
- Building-local horizontal frame and BLVF versions;
- opening/connector/access graph revision;
- Map/Branch/Time context for history-dependent state;
- algorithm version and query generation.

Required checks:
1. all Building, Space, Level, boundary, Opening, Entrance and connector references resolve;
2. every Space/Level endpoint belongs to the same Building;
3. boundary anchors remain valid against the current component revision;
4. opening intervals are within their referenced boundary and do not conflict unless explicitly allowed;
5. an Opening's kind is compatible with its access role;
6. an Access Connection references compatible endpoints and, where applicable, a valid Opening;
7. no duplicate/conflicting endpoint connection is introduced unless the descriptor allows it;
8. Vertical Connector route stops and segments are ordered and continuous;
9. all Level references belong to the owning Building;
10. direction and current state are known before claiming directed reachability;
11. stale geometry/query generations are rejected before formal apply;
12. exterior Road/Path claims require an adopted endpoint contract and stable endpoint evidence.

Use outcomes Pass, Warning, Blocked, Unknown, Unsupported and AcknowledgedException. Unknown or Unsupported is not Pass.

Do not validate or claim:
- fire-code compliance;
- accessibility compliance;
- structural adequacy or load capacity;
- evacuation capacity/time;
- safe stair/ramp dimensions;
- elevator certification;
- collision-free 3D navigation;
- real-world building safety.

## 10. UX workflows

### 10.1 Add an entrance

1. User selects the Building and chooses Add Entrance.
2. The Canvas highlights eligible exterior boundary components.
3. User places the entrance anchor; the Inspector asks for a plain-language label and entrance role (main, service, emergency, private, other).
4. The editor creates a staged candidate with stable-identity reservation only through the future Core Domain service.
5. User optionally identifies the interior Space reached by this entrance.
6. The system shows that no Road/Path connection is implied.
7. On supported Core Domain adoption, validation and explicit apply create the formal Building-owned record.

### 10.2 Add a door or passage between Spaces

1. User selects a shared boundary or chooses a boundary segment.
2. The editor shows adjacent Space identities as context, not as an automatic connection.
3. User chooses Door, Archway or Passage and places an opening anchor.
4. User explicitly chooses whether it connects the two Spaces.
5. The Inspector separates physical opening details from movement direction and current state.
6. Validation checks boundary anchor freshness and endpoint ownership.
7. If typed operations are not adopted, formal apply remains disabled and the UI explains why.

### 10.3 Add a vertical connector

1. User chooses Stair, Elevator, Ladder or Ramp.
2. User selects the source Level and target Level using creator-facing labels backed by stable Level IDs.
3. For a multi-storey connector, user adds intermediate Levels and landings in route order.
4. User optionally links endpoint Spaces and plan anchors.
5. The editor previews the schematic route across Level plans.
6. Validation checks continuity and references without pretending to validate 3D structure or building codes.
7. Formal apply requires adopted Building-owned connector payload and typed commands.

### 10.4 Inspect access

Selecting a Space shows explicitly modeled entrances/connections and their state evidence. The UI distinguishes:
- “Connected in the authored model”;
- “Connection exists but is closed/locked/blocked”;
- “State unknown”;
- “No connection modeled”;
- “Route query unsupported”.

Never display a simple green “accessible” indicator when the plan or state coverage is incomplete.

## 11. AI assistance and authorization

AI may:
- suggest likely entrance locations from creator instructions or an imported plan;
- propose door/opening candidates on existing boundaries;
- suggest Space endpoints for creator review;
- identify dangling Level references or discontinuous stair routes;
- explain why geometric adjacency does not establish access;
- produce an immutable Candidate for a complete entrance/opening/connector proposal.

AI must not:
- infer formal access from touching polygons or visual symbols alone;
- infer a Road junction from nearest geometry;
- invent missing Level, Space or boundary IDs;
- mark unknown door state as open;
- infer code compliance, safety or accessibility;
- directly write Core Domain records, allocate IDs or publish Assets;
- rewrite historical access states without typed commands and authorization.

Candidate acceptance, authorization, submission and formal application remain distinct. Authorization binds exact proposal version/hash, target Building, Level/Space/boundary revisions and query generation. Before apply, re-resolve all references and reject stale proposals.

Low-risk manual edits need not be forced through AI Candidate review, but unsupported domain operations remain unavailable regardless of whether the actor is human or AI.

## 12. V1 scope and deferrals

| Capability | V1 recommendation | Status |
|---|---|---|
| Building-owned Entrance identity | Adopt as typed child | Requires Core Domain adoption |
| Boundary-anchored Opening | Adopt bounded kinds and stable anchors | Requires boundary component contract |
| Door/window/passage distinction | Required semantic vocabulary | Not current registry capability |
| Explicit Space-to-Space connection | Adopt bounded Access Connection | Requires endpoint descriptor |
| Entrance-to-exterior endpoint | Support explicit local endpoint first | No implied Road relation |
| Entrance-to-Space connection | Adopt | Requires typed endpoint pair |
| Stair/elevator/ladder/ramp | Adopt bounded connector kinds | Requires Building-owned payload |
| Multi-storey connector route | One stable identity with ordered stops/segments | Requires route schema and fixtures |
| Door operational state | Separate from opening identity | Requires temporal/runtime state contract |
| Basic authored-graph reachability | Limited, evidence-bearing query | No general indoor navigation |
| Road/Path formal junction | Deferred | ME-03-H endpoint identity gap |
| Cross-Building/campus connectors | Deferred | Requires facility/site contract |
| Full 3D stair/elevator geometry | Deferred | ME-05 3D extension and engine proof |
| Fire, safety, accessibility/code compliance | Out of scope | No engineering validation |
| Evacuation/crowd simulation | Out of scope | Not V1 |

## 13. Core Domain / Map Engine adoption gates

Before formal access authoring is enabled:

1. Adopt the Building descriptor and Building-owned child catalog contract from ME-04-B.
2. Adopt stable Entrance, Opening, Access Connection and Vertical Connector identity allocation, uniqueness, retirement and copy semantics.
3. Adopt ME-04-D stable boundary component identity and typed boundary-anchor references.
4. Adopt bounded opening kinds and distinguish physical Opening from semantic Access Connection.
5. Adopt supported endpoint kinds and endpoint-pair/cardinality/direction rules; do not use generic Relationship descriptors as a shortcut.
6. Adopt Space/Level references and multi-level Space qualification rules.
7. Adopt the Vertical Connector ordered stop/segment payload, including explicit landing and route-continuity semantics.
8. Adopt current Access State representation and the distinction between definition, Map Initial State and History-local state changes.
9. Define typed Commands for initial creation, supported definition edits and post-zero changes; pin temporal scope and idempotency.
10. Define Asset publication, CAS, stale-write rejection and recovery for geometry changes.
11. Add deterministic positive/negative fixtures for door/window distinction, boundary anchor invalidation, duplicate endpoints, locked/blocked states, unknown state, multi-storey stair routes, missing intermediate Levels, cross-Building references and stale query generations.
12. Define the future RoadEndpoint integration only after ME-03-H's endpoint identity blocker is resolved.
13. Prove deterministic serialization and relevant geometry/query behavior on Windows and macOS.

No ME-04 document alone authorizes registry, database schema, Command catalog, Relationship endpoint, API or 3D geometry changes.

## 14. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| E-01 | Add two entrances to one Building | Distinct stable Entrance IDs; labels do not define identity |
| E-02 | Add a window to a Space boundary | Opening exists; no traversable connection is created |
| E-03 | Add a door between two Spaces | Opening and Access Connection are separate explicit records |
| E-04 | Two Space polygons touch without a door | No access edge is inferred |
| E-05 | Door is closed | Opening identity/geometry remain; current reachability reflects closed state if supported |
| E-06 | Door is locked | Not treated as open; no identity mutation |
| E-07 | Door state is unknown | Reachability is indeterminate, not reachable |
| E-08 | Boundary geometry is refined | Opening anchor re-resolves or becomes stale for review; never silently jumps |
| E-09 | Archway connects two Spaces | Explicit connection exists without requiring a door leaf |
| E-10 | Entrance leads to an interior lobby | Entrance↔Space connection is explicit |
| E-11 | Entrance is near a Road | No Road junction is inferred |
| E-12 | Add a stair between two Levels | Stable connector references both Building-owned Level IDs |
| E-13 | Stair spans three Levels | One connector identity with ordered stops and segments |
| E-14 | Intermediate Level is missing | Connector validation is Blocked |
| E-15 | Elevator serves only selected Levels | Only declared stops are connected |
| E-16 | Ladder reaches a service platform | Explicit connector; no safety/accessibility claim |
| E-17 | Ramp is drawn between floors | Schematic route only; no code-compliance claim |
| E-18 | Connector references another Building's Level | Reject as Unsupported in V1 |
| E-19 | Door is historically locked after time zero | Requires selected Map/Branch typed History command; otherwise Unsupported |
| E-20 | Elevator outage is recorded | Operational state changes without deleting connector identity |
| E-21 | Access query has incomplete plan coverage | Indeterminate/Unknown; no false negative certainty |
| E-22 | AI proposes a doorway from an image | Candidate only; creator review and typed apply required |
| E-23 | Renderer Z/camera changes | No formal connector or Level changes |
| E-24 | Road endpoint descriptor is absent | Exterior access remains local; formal Road connectivity disabled |

## 15. Final recommendation

Adopt a small Building-owned access model with stable Entrance, Opening, Access Connection and Vertical Connector identities.

The decisive rule is that **physical opening, semantic connection, operational state and historical state are four separate concerns**. Geometry can anchor and validate them, but geometry alone must not create access semantics.

Use one stable Vertical Connector identity for a multi-storey route with explicit ordered Level stops and segments. Keep the V1 representation schematic and Building-local; do not claim full 3D or engineering validation.

Exterior access may be modeled explicitly without pretending it is connected to a Road. Formal Road integration remains gated by ME-03-H's stable endpoint contract. This preserves a useful Building authoring workflow without inventing Core Domain capabilities or weakening the existing spatial/history model.
