# World View — ME-04-H Cross-architecture Integration Review

- Version: 0.1
- Date: 2026-11-21
- Status: Cross-architecture Review Working Baseline — ME-04-A through ME-04-G reconciled; formal adoption and implementation gates remain open
- Scope: Buildings, Levels, Spaces, openings/access, vertical circulation, natural Caves and underground facilities
- Authority: This review does not freeze an ADR, modify Core Domain registries/schema/commands/relationships, authorize APIs or 3D geometry, or claim implementation/test completion

## 1. Executive disposition

ME-04-A through ME-04-G form a coherent specialist product/domain/UX working baseline, subject to the findings and gates below.

The integrated model is:

1. **Building** — one stable top-level semantic Object identity.
2. **Level** — stable Building-scoped typed substructure with a Building-local Vertical Frame (BLVF), not a top-level Map Object.
3. **Space** — stable Building-scoped typed child identity; Room/Corridor/Lobby/Void are bounded kinds, not separate Object Types.
4. **Plan / Space Level Slice / Boundary** — Building-owned typed plan records and geometry components, using Building-local 2D coordinates; not separate semantic Map Objects.
5. **Entrance / Opening / Access Connection / Vertical Connector** — Building-owned typed access records with explicit endpoints and independent operational state; not inferred from geometry.
6. **Natural Cave System** — separate top-level semantic Object candidate with Cave-owned chamber/passage/entrance/station records and a Cave Survey Frame.
7. **Constructed underground spaces** — reuse Building/Level/Space/Access models.
8. **Independent cross-Building subsurface infrastructure** — bounded Subsurface Network proposal; deferred until a concrete use case and cross-aggregate endpoint model are adopted.

The core consistency principle is that **semantic identity, geometry, boundary structure, spatial adjacency, explicit connectivity, operational condition and historical state are separate concerns**.

The current Core Domain V1 registries do not enable Building or Cave Object Types, Building child aggregates, interior geometry roles, access child records or their commands. Therefore ME-04 is a design baseline only. Formal creation/editing remains unavailable until Core Domain adopts and tests the required typed contracts.

## 2. Review sources and authority

Reviewed baseline:
- ME-04-A Domain Inventory & Descriptor Reconciliation V0.1.
- ME-04-B Building Model & Authoring Workflows V0.1.
- ME-04-C Floors, Levels & Vertical Organization V0.1.
- ME-04-D Rooms, Spaces & Interior Geometry V0.1.
- ME-04-E Entrances, Openings & Vertical Circulation V0.1.
- ME-04-F Underground Spaces, Caves & Connected Facilities V0.1.
- ME-04-G UX Scenarios, Acceptance & Figma Handoff V0.1.
- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
- ME-01-05 Map Object, Geometry & Representation Model V0.1.
- ME-01-06 Editor Interaction Framework V0.1.
- ME-03-H Cross-architecture Integration Review V0.1.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- ME-05-05 Geometry & Field Asset Package and Publication Contract V0.3.
- ME-05-06 Cross-architecture Integration Review V0.4.
- Core Domain P2-09-15 Command Descriptor Catalog V0.5.
- Core Domain P2-09-18 Object Type Descriptor / Attribute Contract V0.2.
- Core Domain P2-09-19 Geometry Component Identity / Payload Contract V0.2.
- Core Domain P2-09-20 Relationship Descriptor / Temporal Contract V0.2.
- Core Domain P2-09-26 Object Type Descriptor Registry V0.2.
- Core Domain P2-09-27 Relationship Type Descriptor Registry V0.2.

Core Domain remains authoritative for Object identity, definition ownership, Initial State, History Commands, formal geometry references, relationships and immutable Asset ownership. Map Engine / Spatial Services remain read-only for formal domain truth. AI Assistant owns proposal staging and must use the same formal application boundary as manual authoring.

## 3. Integrated authority and persistence map

| Concept / data | Recommended owner | Identity | Formal write authority |
|---|---|---|---|
| Building | Core Domain Object | Core Object ID | Building descriptor + definition / Initial State / History services |
| Building footprint | Building geometry component | Component ID scoped to Building | Registered geometry operation / publication path |
| Level catalog | Building typed definition payload | level_id scoped to Building | Typed Building definition service; later temporal commands only if adopted |
| BLVF | Building typed definition payload | vertical_frame_id scoped to Building | Typed Building definition service |
| Space catalog | Building typed interior payload | space_id scoped to Building | Typed Building Space service |
| Plan / Level Slice | Building typed plan payload | plan/region IDs scoped to Building | Typed plan service + immutable Asset publication + CAS |
| Boundary / partition | Building geometry/plan component | boundary/component ID scoped to Building | Typed geometry/plan operation |
| Entrance / Opening | Building typed access payload | IDs scoped to Building | Typed Building access service |
| Access Connection / Vertical Connector | Building typed access payload | IDs scoped to Building | Typed Building access service; selected-History state only when supported |
| Cave System | Core Domain Object | Core Object ID | Cave descriptor + definition / Initial State / History services |
| Cave chamber/passage/entrance/station | Cave typed child payload | IDs scoped to Cave | Typed Cave service |
| Cave survey frame | Cave typed definition payload | survey_frame_id scoped to Cave | Typed Cave definition service |
| Subsurface Network | Deferred candidate Core Object | Network Object ID if adopted | Future network descriptor and typed network services |
| Geometry / survey bytes | Core Asset Store | Immutable Asset ID/hash | Asset publication coordinator |
| Adjacency, containment, reachability candidates | Map Engine / Spatial Services | Derived artifact/query result | Read-only calculation; no formal write |
| AI/imported proposals | AI Assistant staging | Candidate/proposal ID + version/hash | Candidate review/authorization/application lifecycle |
| Display, active Level, view mode, labels, selection | Editor / Presentation | UI-local identity | Presentation state only |

Building/Cave child identities are durable domain identities, but are not global Core Object IDs. Any API requiring a Core Object ID must not receive a child ID. External knowledge references require an explicitly typed composite reference such as (building_object_id, space_id) or (cave_object_id, passage_id).

## 4. Findings and required dispositions

### H-01 — Building and Cave are absent from the enabled V1 Object Type Registry

**Severity: P0 — blocks formal Building and Cave creation**

P2-09-26 V0.2 contains the existing ten recommended V1 Object Types and does not enable Building or Cave System. ME-04-B and ME-04-F correctly treat these as proposed semantic Objects rather than repurposing Settlement, PointOfInterest or another existing type.

Required disposition:
- Prepare separate, typed Building and Cave System descriptor proposals.
- Define bounded definition fields, geometry roles, ownership scope, identity/lifecycle rules and command capabilities.
- Preserve Project-shared versus Map-local ownership; no third scope.
- Define whether conceptual/unplaced Buildings and partially surveyed Cave Systems may exist without formal geometry.
- Add positive/negative registry and validation vectors before enabling either type.

Interim behavior: Building/Cave drafts and AI proposals remain staging data. No fake Object Type, generic metadata record or hidden layer may be presented as formal domain data.

### H-02 — Building-owned Level/Space/access aggregates need a typed persistence contract

**Severity: P0 — blocks formal interior authoring**

ME-04-C/D/E propose Level, Space, plan, boundary, Entrance, Opening, Access Connection and Vertical Connector records as typed children of a Building. This avoids top-level Object proliferation and preserves one Building identity, but the current Core Domain descriptor/schema baseline does not define these child aggregates.

Required disposition:
- Define one versioned Building interior/access aggregate with explicit typed substructures, not arbitrary unvalidated JSON dictionaries.
- Define stable child IDs, owner scoping, no-reuse after retirement, copy semantics, lifecycle and reference integrity.
- Define Level→Space Level Slice and Space→plan/boundary references.
- Define access endpoint unions and type compatibility.
- Define validation, revision identity, optimistic concurrency/CAS and idempotent application.
- Keep child identity distinct from Core Object identity and from geometry Component identity.

Do not implement each Room, Door or Stair as a top-level Map Object merely to reuse existing Object CRUD. That would contradict the accepted bounded Building aggregate model and introduce unnecessary ownership/relationship graphs.

### H-03 — Geometry Component ownership must be reconciled with nested child geometry

**Severity: P0 — blocks formal interior plan/boundary geometry**

P2-09-19 V0.2 defines stable Geometry Component identity scoped to one semantic Object and immutable Asset-backed payloads. ME-04-D/E require geometry anchors and plan regions associated with Building-scoped child identities such as Space, Level Slice and Boundary.

The consistent V1 direction is:
- geometry components remain owned by the top-level Building Object;
- typed Building component-owner metadata identifies the relevant child context (e.g. Space ID + Level ID + region/boundary role);
- component identity remains stable under supported adjustment/refinement;
- the typed Building aggregate validates that all child references resolve;
- Opening anchors reference a stable Building-owned boundary component and its source geometry revision.

This is a proposed owner-payload extension, not currently enabled by P2-09-19 or P2-09-26. Core Domain must define the exact typed ownership payload and prove that it does not create polymorphic string owners or ambiguous Asset ownership.

Do not create a generic owner_type / owner_id polymorphic geometry table as a shortcut. Do not store plan geometry bytes inside mutable Building JSON.

### H-04 — Generic geometry Commands are not sufficient without Building role/capability declarations

**Severity: P0 — blocks formal plan edit Commands**

P2-09-15 V0.5 and P2-09-26 V0.2 allow only declared geometry roles, families, dimensions, cardinalities and operation capabilities. The presence of generic adjust/refine/add/remove geometry Commands does not enable interior geometry.

Required Building descriptor capabilities must explicitly cover:
- footprint;
- supported interior plan-region geometry;
- shared boundary/partition geometry;
- optional entrance/opening anchor geometry where the chosen ownership model requires it.

Each operation must validate owner Object, child context, component role, source fingerprint, coordinate frame, family/dimension, cardinality and relevant plan/Level revision. Split/merge remain deferred and cannot be emulated as identity-changing side effects of add/remove.

### H-05 — Level identity and vertical reference semantics are coherent but need a typed adoption package

**Severity: P1 — blocks formal Level catalog**

ME-04-C correctly separates Level identity/order, Building-local elevation and Map/world vertical position. It does not confuse Level labels, display order, renderer Z or terrain elevation.

Required disposition:
- Stable Building-scoped level_id and explicit retirement/no-reuse policy.
- BLVF identity, unit, up-axis, precision profile and version.
- Optional explicit binding to a stable Map vertical reference with transform provenance/error.
- Level elevation values reference the BLVF version.
- A Map Field Revision may stale terrain comparison evidence but never rewrites BLVF or Level elevations.
- No world elevation is assumed when no compatible vertical reference exists.

The first formal implementation may support Level catalog and local elevation only. It must not claim 3D geometry or volumetric validation.

### H-06 — Space identity and multi-Level plan semantics are coherent; global relationship endpoints are not needed for V1

**Severity: P1 — blocks formal Space/plan aggregate**

ME-04-D's Building-scoped Space ID model avoids a top-level Object for every room while still supporting names, creator knowledge references and multi-Level Spaces.

Required disposition:
- Stable (Building Object ID, Space ID) reference contract.
- Explicit Level Slice references to Level IDs owned by the same Building.
- One Space may have multiple Level Slices, including a declared multi-Level atrium/void; no inference from overlapping plan polygons.
- Space kind, Room use, Void semantics and partial-plan completeness remain distinct.
- Typed external references must not treat space_id as a Core Object ID.

V1 does not need generic spatial.contains, structural.part_of or network.connects_to endpoint pairs for Spaces. Interior adjacency may remain derived Spatial Services evidence. If a later use case requires globally addressable Space relationships, revisit the identity model explicitly.

### H-07 — Shared boundary and Opening anchoring require stable component revision semantics

**Severity: P1 — blocks reliable Opening placement/edit**

ME-04-D uses stable shared boundary components so adjacent Space boundaries do not drift independently. ME-04-E anchors Openings to those components.

Required disposition:
- One shared boundary component identity where a single authored boundary separates two Spaces.
- Space boundary loops reference ordered boundary component identities and direction/orientation.
- Opening anchor is component-relative and bound to a source component revision/fingerprint.
- Boundary edits re-resolve anchors deterministically; ambiguous anchors become Stale and require review.
- A shared boundary edit validates every affected Space loop atomically.
- A door/window symbol is geometry or a physical Opening record, not a traversable connection by itself.

The exact component topology and sub-reference scheme require ME-05/Map Engine technical proof. Do not use transient vertex indexes or screen coordinates as durable anchors.

### H-08 — Access Connections and Vertical Connectors are Building-owned semantics, not generic Core Relationships

**Severity: P1 — blocks formal interior access graph**

ME-04-E's access catalog is an aggregate-owned structure with typed endpoints such as (Building ID, Space ID), (Building ID, Entrance ID) and Level/landing references.

This is distinct from Core Domain's generic Object-to-Object Relationship Registry:
- endpoints are nested child identities, not Core Object IDs;
- one connection may include an Opening reference and operational state;
- vertical connectors may have ordered stops/segments;
- geometry supports and validates the connection but does not create it.

Required disposition:
- Define a bounded endpoint union, connection kind, directionality and uniqueness rules.
- Define endpoint activity and retirement behavior.
- Define current operational state separately from physical Opening/Connector identity.
- Define explicit source/target and ordered multi-Level stops.
- Keep derived reachability separate from persisted access facts.
- Do not reuse network.connects_to or spatial.contains for interior access.

### H-09 — Road and external surface endpoint integration remains blocked by ME-03-H

**Severity: P0 — blocks formal Road-linked Building/Cave access**

ME-03-H found that the current Road-to-Road relationship uses Road Object pairs and cannot identify exact Road endpoint/junction identity. ME-04-E correctly uses an explicit exterior anchor and defers Road linkage.

Required disposition:
- Keep Building/Cave Entrance to nearby Road as spatial evidence only.
- Do not create a Road relationship from nearest-line snapping or geometric touch.
- Any future Road-linked access must use a stable RoadEndpoint or Junction identity with endpoint-pair uniqueness and geometry-remapping semantics.
- Reconcile RoadEndpoint with Building Entrance and Cave Entrance endpoint types only after each identity model is explicit.

This is an inherited P0 blocker, not a defect to solve inside ME-04 by adding hidden relationship attributes.

### H-10 — Natural Cave System is a separate Object; cave children need their own survey aggregate

**Severity: P0 — blocks formal Cave authoring**

ME-04-F correctly distinguishes natural Cave System from Building-owned constructed underground Spaces.

Required Cave descriptor/aggregate:
- top-level Cave System Object ID and Project-shared/Map-local ownership;
- typed Cave-owned Chamber, Passage, Entrance, Station and Connection IDs;
- Cave Survey Frame with explicit horizontal/vertical references, units, precision and uncertainty;
- 2D plan/passage roles only where the shared geometry contract supports them;
- survey evidence/provenance as immutable Assets;
- explicit cave-internal connections and partial-survey semantics;
- current physical/access condition separate from geometry and identity.

Cave child IDs are not global Object IDs. A Cave entrance near a Road or Building does not create a formal relationship. A Cave-to-Building connector is cross-aggregate and requires a separately owned typed connector/network contract.

### H-11 — Cross-Building Subsurface Network is correctly deferred

**Severity: P1 — feature unavailable until separate adoption**

A single Building-owned connector cannot validly connect multiple Building aggregates. ME-04-F proposes a Subsurface Network only for independent infrastructure with identity/lifecycle of its own.

Required gate before adopting:
- demonstrate a concrete product need that cannot be served by one Building;
- define a top-level Network Object and typed child nodes/segments/shafts/portals;
- define supported endpoint references to Building Entrance, Cave Entrance and other adopted endpoints;
- define Project/Map ownership, cross-Map sharing, history, direction and operational state;
- reconcile with RoadEndpoint and ME-04-E access semantics.

Until then, cross-Building tunnel/utility/metro connectivity is Unsupported. Do not introduce a universal graph engine or generic Facility Object prematurely.

### H-12 — Current-definition and History semantics must be separated for every child aggregate

**Severity: P1 — authority-routing gate**

The accepted Core Domain rules remain unchanged:
- Project-shared Building/Cave definition corrections affect all Maps using that shared identity, retroactively under current-definition semantics.
- Map-local definition corrections affect only that Map-local Object.
- Map Initial State owns time-zero Building/Cave membership and initial child catalog/placement.
- Post-zero construction, demolition, cave-in, closure or historical reconfiguration must be a typed Command in the selected Map/Branch History, if that command is adopted.
- Map-wide Field Revision remains a source baseline correction, not a historical geological or construction event.

A child aggregate update must declare whether it is a current definition revision, Initial State content or a selected-History runtime change. A mutable current child payload cannot silently simulate a historical room addition or cave collapse.

V1 should initially enable current Building/Cave definitions and supported time-zero setup only where Core Domain can guarantee correct authority. Historical interior child mutations remain unavailable until their typed Command payloads and replay semantics are adopted.

### H-13 — Immutable Asset publication and aggregate CAS are mandatory

**Severity: P1 — publication gate**

Interior plan, boundary, Cave survey and supported passage geometry bytes must use immutable Core Assets. Formal owner references are committed only after payload validation, Asset integrity verification and authoritative publication coordination.

Required sequence:
1. Stage new immutable payloads.
2. Validate geometry family, coordinate frame, owner/child references and resource policy.
3. Publish and verify immutable Assets.
4. Commit typed Building/Cave aggregate revision and Asset references using optimistic concurrency/CAS.
5. Re-read authoritative aggregate state.
6. Invalidate derived geometry, adjacency, reachability and presentation artifacts by their declared fingerprints.

A failed metadata commit must not create a formal child reference. Unreferenced Assets are handled by orphan reconciliation, not destructive in-place rollback. A concurrent plan edit must not silently overwrite another update.

### H-14 — Map Engine remains read-only for domain truth; derived spatial results need freshness

**Severity: P1 — query/presentation gate**

Map Engine / Spatial Services may provide:
- projection and local-frame transforms;
- geometry validation and spatial predicates;
- shared-boundary and adjacency candidates;
- hit-test/snap results;
- derived plan display, 2D overlays and optional massing previews;
- read-only reachability over explicitly adopted connections;
- derived cave survey visualizations.

They may not create Space, Opening, Connection, Cave or Network identities; mutate formal geometry; or infer formal connectivity from geometric coincidence.

Every query/diagnostic must bind to Building/Cave definition revision, geometry Asset fingerprints, Level catalog/Survey Frame version, active Map/Branch/Time, scene/query generation and algorithm version as applicable. Stale evidence must be refreshed or shown Stale before application.

### H-15 — AI Candidate lifecycle is aligned; capability and safety claims need strict limits

**Severity: P1 — proposal/application gate**

ME-04-G aligns with ME-01-06 and existing AI proposal architecture:
- simple low-risk supported manual edits may use direct typed operations;
- complex AI-generated layouts, multiple Spaces, uncertain survey interpretation or broad changes use Candidate staging;
- Candidate review, acceptance, authorization and application are distinct;
- authorization binds exact Candidate version/hash and target Building/Cave/Map/Branch/Time;
- apply re-resolves all IDs, component references, source fingerprints and operation scope;
- authoritative read-back is required before reporting success.

AI may suggest geometry and classification but may not claim structural integrity, fire compliance, accessibility compliance, cave safety, geotechnical stability, flood safety or legal access. Unsupported/Unknown must remain visible.

### H-16 — V1 must remain a 2D authoring model with explicit vertical metadata

**Severity: P1 — prevents false 3D capability**

ME-05-04 and the shared geometry baseline define V1 formal vector editing as a 2D working plane. ME-04-C/F may record scalar local elevations, vertical references, survey stations and evidence, but these do not authorize arbitrary 3D vertices or solids.

Required disposition:
- Building plans remain 2D Building-local geometry; BLVF stores typed scalar vertical context.
- Cave plans remain 2D survey projections; Cave Survey Frame stores explicit vertical references and survey observations.
- 3D solids, volumetric collision/topology, parametric BIM, cave volume and arbitrary XYZ geometry remain deferred.
- A 3D renderer preview is derived presentation only.
- Imported 3D source data may remain evidence or Unsupported staging, never silently flattened into formal geometry.

### H-17 — Multi-scale and Working Scope semantics remain consistent

**Severity: P2 — cross-scope behavior**

Building and Cave identities are not split by World/Region/Locality/Site Working Scope boundaries. A Cave System may span several Sites. A Building remains one identity across exterior/interior views. Canvas clipping and LOD are derived presentation and never change formal extent or geometry.

Extending Map Extent is a separate formal operation. A cave continuation beyond the current Map extent remains external/unknown evidence until the Map extent is explicitly extended.

## 5. Adopted product/domain baseline versus unavailable formal capabilities

| Capability | ME-04 design disposition | Current formal availability |
|---|---|---|
| Building identity and footprint | First-class Building Object | Not enabled; descriptor absent |
| Level catalog and BLVF | Building-owned typed child | Not enabled; typed aggregate absent |
| Space catalog and Level Slices | Building-owned typed child | Not enabled; typed aggregate absent |
| Interior plan/boundary geometry | Building-owned components with child context | Not enabled; component-owner extension absent |
| Entrance/Opening/Access Connection | Building-owned typed access records | Not enabled |
| Vertical Connector | Building-owned ordered connector | Not enabled |
| Natural Cave System | Top-level Cave Object + typed survey children | Not enabled; descriptor absent |
| Constructed basement/interior | Reuse Building/Level/Space | Not enabled until Building aggregate |
| Cross-Building Subsurface Network | Separate bounded proposal | Deferred / Unsupported |
| Interior adjacency candidates | Derived Spatial Services query | Algorithm/library proof remains open |
| Formal access/reachability | Explicit adopted connections only | Not enabled |
| Full 3D/BIM/cave volume | Deferred | Unsupported |

## 6. Recommended Core Domain adoption package

Do not scatter isolated schema changes across ME-04 documents. Prepare one coordinated Core Domain adoption package with separate versioned artifacts:

### 6.1 Object descriptors
- Building Object Type descriptor.
- Cave System Object Type descriptor.
- Explicit geometry role/family/dimension/cardinality declarations.
- Definition fields, ownership scope, lifecycle and compatibility semantics.

### 6.2 Typed aggregate payloads
- Building Level/BLVF catalog.
- Building Space/Plan/Boundary aggregate.
- Building Entrance/Opening/Access Connection/Vertical Connector aggregate.
- Cave Survey Frame and Cave Chamber/Passage/Entrance/Station/Connection aggregate.
- Stable child ID allocation, reference integrity and retirement rules.

### 6.3 Geometry / Asset integration
- Building-owned geometry component context for Space/Level/Boundary.
- Cave-owned geometry component context for cave plan/passage roles.
- Immutable Asset references and publication coordinator integration.
- Component-relative Opening anchor contract and geometry-revision remapping.

### 6.4 Commands and authority paths
- Building/Cave identity reservation/binding and definition services.
- Map Initial State setup semantics.
- Supported geometry component operations for declared roles.
- Typed child aggregate update operations with CAS.
- Selected-History commands only for explicitly adopted historical operations.
- No generic CRUD or generic polymorphic owner API.

### 6.5 Validation and conformance
- Object descriptor/aggregate validation vectors.
- Child identity/reference and owner-scope vectors.
- Geometry role, boundary loop and shared-boundary vectors.
- Access endpoint/Opening/connector vectors.
- Cave survey frame, uncertainty, partial coverage and explicit connectivity vectors.
- Publication failure, CAS conflict, cancellation and orphan recovery tests.
- Windows/macOS deterministic serialization and geometry predicate evidence.

The adoption package must be reviewed against P2-09-15/17/18/19/20 and P2-09-26/27 before implementation. This ME-04-H review does not itself authorize the package.

## 7. Recommended implementation sequence

To avoid implementing the entire indoor/cave domain at once, use bounded slices:

| Phase | Scope | Exit gate |
|---|---|---|
| A — Building foundation | Building descriptor, identity, ownership, footprint, Asset publication and basic Inspector | Building identity and footprint round-trip with provider/asset tests |
| B — Levels and local frames | Typed Level catalog, BLVF, explicit optional Map vertical binding | Stable Level IDs, local elevation and deterministic frame validation |
| C — Spaces and plans | Space catalog, Level Slices, 2D plan regions, shared boundary components | Space/plan validation, shared-boundary consistency and CAS |
| D — Openings and access | Entrance, Opening, Access Connection and Vertical Connector | Explicit endpoints, stable boundary anchors and operational-state separation |
| E — Natural caves | Cave Object, Cave Survey Frame, survey children and internal connections | Partial survey, uncertainty, multi-entrance and explicit connectivity proof |
| F — Cross-Building network | Subsurface Network only if validated product demand exists | Separate architecture adoption; not assumed in V1 |

Phase A–C establish a useful Building/Interior baseline. Phase D adds access semantics. Phase E is a separate natural-geography Object family and may proceed independently once its own dependencies are ready. Phase F is deliberately not on the default V1 critical path.

## 8. Consolidated risk register

| ID | Risk | Severity | Mitigation |
|---|---|---|---|
| R-01 | Treating child IDs as global Object IDs | P0 | Typed composite references and API validation |
| R-02 | Polymorphic geometry ownership shortcut | P0 | Keep geometry Object-owned; typed child context in owner payload |
| R-03 | Formal UI enabled before descriptors/commands exist | P0 | Capability gating and explicit Unsupported state |
| R-04 | Road access inferred from proximity | P0 | Inherit ME-03-H RoadEndpoint blocker |
| R-05 | Shared boundary edits desynchronize adjacent Spaces | P1 | Stable shared component identity and atomic validation |
| R-06 | Opening anchor silently moves after geometry edit | P1 | Revision-bound anchor re-resolution |
| R-07 | Level label interpreted as altitude | P1 | Separate label/order/local elevation/world reference |
| R-08 | Cave survey treated as complete/precise | P1 | Completeness, uncertainty and provenance |
| R-09 | Adjacency mistaken for traversability | P1 | Explicit Access Connection records |
| R-10 | Historical changes written as current definitions | P1 | Authority-context routing and typed History gates |
| R-11 | 2D plan presented as full 3D model | P1 | Explicit dimensionality and derived-only 3D preview |
| R-12 | Cross-Building connector owned by one Building | P1 | Defer until Subsurface Network contract |
| R-13 | Asset publication succeeds but aggregate CAS fails | P1 | Immutable staging, CAS, authoritative reconciliation and orphan recovery |
| R-14 | AI claims safety/compliance without evidence | P1 | Prohibit safety/compliance conclusions; evidence-bearing Unknown/Unsupported |

## 9. Final disposition

Accept ME-04-A through ME-04-G as a coherent Buildings & Interior Spaces product/domain/UX working baseline, subject to H-01 through H-17 and the adoption gates in this review.

The recommended V1 model deliberately avoids a generic BIM graph:
- one Building Object owns Levels, Spaces, plans, boundaries and access records;
- one natural Cave System Object owns its survey children;
- independent cross-Building subsurface networks remain a separate, need-driven extension;
- geometry and spatial queries never create semantic identity or connectivity;
- formal writes remain Core Domain-owned and capability-gated.

ME-04 is complete as a specialist design working baseline after this review. It is **not** complete as Core Domain implementation, schema adoption, descriptor activation, API delivery or platform verification. The next step is to create a separately scoped Core Domain adoption package for Building and Cave descriptors/aggregates, then hand the phased implementation plan to development. No production code or migrations are authorized by ME-04-H itself.
