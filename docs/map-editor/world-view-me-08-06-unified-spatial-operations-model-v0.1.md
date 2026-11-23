# World View — ME-08-06 Unified Spatial Operations Model

- Version: 0.1
- Date: 2026-11-23
- Status: Unified Spatial Operations Working Baseline
- Workstream: ME-08 — Spatial Operations & Domain Integration
- Depends on: ME-08-00 V0.1; ME-04-B through ME-04-H; ME-05-02 through ME-05-06; ME-06-A through ME-06-G; ME-01-05/06/07
- Authority: Product / Editor integration contract. This document does not register Core Domain Object Types, Relationship Types, Command Types, persistence tables, APIs or executable schemas.

## 1. Purpose

ME-08-06 defines a unified model for spatial editing operations across world-map, Building interior, Cave, Site composition and future specialist scenes.

The objective is to provide one predictable operation lifecycle without flattening specialist domain semantics into generic geometry CRUD.

**Unified interaction does not mean unified domain meaning.** A Door, Cave Passage, Road Junction, Building Footprint, Room boundary and Spatial Group remain different concepts and retain their own capabilities and validation rules.

This contract applies to a single-user desktop editor for Windows and macOS. Collaborative editing, multi-user conflict resolution and distributed locking are out of scope.

## 2. Governing principles

1. Every formal spatial change has an explicit target, operation kind, editing context and authority scope.
2. The active editor scene is not itself a write-authority selector. World Map, Building Interior and Cave scenes must resolve the intended formal write context explicitly.
3. Organization, geometry, semantic identity, domain relationships and presentation remain separate.
4. Editor staging and previews are not formal world state.
5. Map Engine calculations and spatial-query results are evidence or derived data, not formal semantic facts.
6. Core Domain remains the only authority for formal Object identity, typed relationships, Commands, write paths and History.
7. A formal operation must be supported by adopted descriptors and registered operation/Command contracts. A UI affordance cannot create missing domain capability.
8. A failed or cancelled operation leaves formal state unchanged. Derived caches may be invalidated and rebuilt independently.
9. Geometry split/merge remain deferred under ME-05-02 until identity lineage and topology semantics are explicitly designed.
10. No generic geometry replacement, arbitrary JSON patch or universal spatial CRUD operation is introduced.

## 3. Four layers of editor action

| Layer | Target | Examples | Formal spatial mutation |
|---|---|---|---|
| View / interaction | Editor view state | Select, pan, zoom, switch Level, enter/leave Interior scene | No |
| Organization | Spatial Site / Spatial Group membership and hierarchy | Group, ungroup, reparent, reorder, assign member | Only if a supported formal organization relation is changed |
| Geometry | Geometry Component or geometry-owning entity | Draw, adjust, refine, move, rotate, remove component | Yes, after formal application |
| Domain relation | Typed connection or other adopted relationship | Create Door, bind Entrance, connect Cave Passage, change traversal direction | Yes, after domain validation and formal application |

A UI action must declare which layer it affects. A view-only action must never generate a formal Command. A group reorganization must not silently move member geometry. A geometric intersection must not silently create a relationship or connection.

## 4. Unified operation lifecycle

Every operation that may change formal domain state follows this lifecycle:

1. **Establish context** — capture active Map, optional Branch/Historical Time, scene, selected Level or Cave frame, Working Scope, target owner and intended temporal/authority scope.
2. **Create Typed Intent** — describe the semantic operation and typed targets; do not serialize a database patch or assume a Core Domain Command exists.
3. **Resolve capability** — verify that the target Object Type, component role, relationship endpoints and operation are adopted and enabled.
4. **Build Preview / Staging** — keep proposed geometry, membership, endpoint changes and dependent effects outside formal state.
5. **Validate** — run common payload/component/spatial checks and specialist domain checks; attach evidence and diagnostics.
6. **Review / authorize** — use ME-06 Candidate and Change Set rules where required by operation risk, AI origin, dependency closure or formal policy.
7. **Re-resolve before apply** — re-resolve target identities, source fingerprints, query evidence and coordinate transforms against the current authoritative snapshot. Discard stale evidence and regenerate preview where necessary.
8. **Resolve formal write path** — select exactly one supported Core Domain write path from ownership and temporal intent, never from the visible scene alone.
9. **Apply through Core Domain** — invoke the adopted Domain Service / registered Command path; Editor and Map Engine do not write formal state directly.
10. **Reconcile authoritative result** — read back formal identities, component references, relationships and effective state; report success only after authoritative reconciliation.
11. **Refresh derived state** — invalidate/rebuild render geometry, spatial indexes, hit-test data, measurements and other disposable projections.

The lifecycle reuses ME-06 Typed Intent, preflight, Candidate review, immutable Selection Snapshot, Change Set, authorization, freshness and recovery contracts. ME-08 does not create a parallel transaction or approval system.

## 5. Operation families

The following are product-level operation families, not a declaration that every operation is currently enabled in Core Domain.

### 5.1 Geometry component operations

- **Create component:** propose geometry for a descriptor-permitted role; formal creation allocates component identity through Core Domain.
- **Adjust component:** edit vertices/control points while preserving component identity and role.
- **Refine component:** adopt a better geometry payload for the same component with source/provenance evidence.
- **Move / rotate:** transform formal geometry in its declared coordinate context using an explicit transform and precision policy.
- **Remove component:** remove component membership only when descriptor cardinality and domain constraints permit it.
- **Split / merge:** deferred; never emulate with chained add/remove operations.

Scaling is not a universal default operation. It may change domain meaning, dimensions, minimum clearances or measurement assumptions. A specialist descriptor may enable a bounded scale operation with explicit anchor, axis, unit, constraints and validation; otherwise it is unsupported.

### 5.2 Spatial entity operations

- Create an adopted typed entity.
- Relocate an entity within a declared coordinate context.
- Reconfigure its supported geometry or internal composition.
- Remove an entity from a selected Initial State or History context through the corresponding formal lifecycle operation.
- Duplicate an entity by creating a new identity and explicitly handling its owned components and relationships.

Duplicate must not clone stable Object IDs, Geometry Component IDs, relationship IDs or historical identity. External relationships are not copied by default. Each copied child/connection requires explicit inclusion rules and validation.

### 5.3 Organization operations

- Create / remove a Spatial Group.
- Add / remove a member.
- Reparent a Group.
- Reorder members.
- Move an entity between Groups.
- Compose or reorganize Spatial Site membership.

Organization operations affect membership and hierarchy only. They do not imply physical containment, adjacency, geometry ownership, access or traversability.

If organization membership is represented only as Editor navigation state, it remains presentation state. If it is formal domain data, it requires an adopted typed relationship or domain structure. ME-08 does not assume a new persisted Group descriptor or generic parent-child relationship.

### 5.4 Domain connection operations

- Create a domain-specific connection.
- Adjust its geometry or endpoint anchors.
- Rebind one endpoint to another compatible typed endpoint.
- Change directionality, traversal semantics, operational state or constraints.
- Remove/disable the connection under its domain lifecycle.

The shared connection protocol supplies common integration concepts (identity, typed endpoints, anchors, optional geometry, traversal semantics and constraints). It does not replace the Door, Entrance, Vertical Connector, Cave Passage, Tunnel, Portal or Road-specific rules.

Normal connections retain exactly two endpoints. Multi-way junctions use an intermediate spatial entity and pairwise connections, as confirmed in ME-08-00.

A geometric crossing, shared coordinate, nearby anchor or visual gap is not enough to create or rebind a formal connection.

### 5.5 Template and module instantiation

Instantiating a Residence, Underground Level, Cave layout, Academy module or War Academy composition may create several typed entities, geometry components, groups and connections.

The template is a creation recipe, not the resulting formal spatial entity. Instantiation must:
- resolve every requested entity/relationship type against adopted capabilities;
- create a dependency-closed staged proposal;
- distinguish required from optional members;
- validate local geometry, containment claims, endpoint compatibility and coordinate frames;
- preview the complete result and its impact;
- submit through one dependency-closed Change Set when the operation is formally supported.

If any required member or relationship is unsupported or blocked, the editor must not report the complete template as successfully instantiated. It may offer a clearly labeled partial preview, but must not silently publish an incomplete composition.

## 6. Coordinate and context rules

### 6.1 Explicit operation frame

Every geometry-affecting intent declares:
- source and target spatial-reference identities;
- coordinate context / local frame identity;
- units, axis order, dimensionality and precision profile;
- transform identity/revision where a transform is required;
- transform direction and anchor/pivot where relevant;
- measurement/error policy required by the operation.

Screen pixels, viewport coordinates and renderer-local coordinates are never formal geometry coordinates.

### 6.2 World Map and Building Interior isolation

Entering a Building Interior editor changes the active editing context, not the Building identity or formal coordinate ownership.

- Building footprint and exterior placement use the relevant Map/world coordinate context.
- Interior plan geometry uses the declared Building-local frame and Level binding.
- Editing a Room boundary changes the applicable local geometry; it does not move the Building footprint.
- Moving/rotating the Building in world space does not rewrite every interior component's local coordinates. The explicit local-to-world transform/binding changes according to its adopted contract.
- A local-space endpoint and a world-space endpoint may be connected only through an explicit, typed boundary anchor and supported transform/binding.
- Cave survey coordinates remain in their declared Cave frame; they are not silently treated as Building-local or Map coordinates.

### 6.3 Group operations versus entity transformations

Moving a Group in the hierarchy changes membership only. A future “transform all members” action, if supported, must be a separately named compound geometry operation with an explicit target set, common transform frame, per-entity capability checks and full preview. It must never be the implicit behavior of drag-and-drop reparenting.

## 7. Formal write-context routing

Every formal mutation resolves exactly one of the established authority paths:

| Intended effect | Formal path |
|---|---|
| Change Project-level Shared Object definition | Shared Object definition Domain Service |
| Establish or edit time-zero Map content | Map Initial State authoring service |
| Change content after time zero in the active Map/Branch | Registered Core Domain Command in that History |
| Publish a Map Source Field Dataset revision | ME-05-01 Field publication service; not a vector geometry operation |

The operation must not infer the path from the active scene or generic Save button.

The confirmed Shared / Local History semantics remain unchanged:
- a Shared Object definition is shared and a change to that definition is visible to Maps that reference it under the shared-definition contract;
- a post-zero event/Command changes only the selected active Map/Branch runtime state;
- inactive Maps remain static references and are not implicitly started or mutated;
- a Map-local historical change must not be written back into the shared base definition.

Event/narrative authoring remains its own Core Domain Event path, not a generic spatial Command substitute.

## 8. Validation model

Validation is composed in layers. A layer may return Pass, Warning, Blocked, Unknown, Unsupported or Indeterminate as appropriate; these outcomes must not be collapsed into a boolean.

| Layer | Owner | Example checks |
|---|---|---|
| Intent / capability | Application + Core Domain descriptors | Intent schema, operation capability, adopted Object/Relationship/Command type |
| Identity / ownership | Core Domain | Target exists, component belongs to Object, owner scope and lifecycle valid |
| Payload / Asset | Core Domain + Asset services | Geometry family, canonical payload, immutable Asset integrity, reference validity |
| Coordinate / precision | Map Engine + coordinate contract | Compatible frames, valid transform, units, axis order, precision and error bounds |
| Shared spatial | Map Engine | Geometry validity, containment/intersection/distance predicates, freshness |
| Specialist domain | ME-02/03/04 or future domain owner | Building/Level/Space rules, cave passage rules, Road endpoint rules, connection semantics |
| History / temporal | Core Domain | Correct Map/Branch/time, Initial State boundary, descriptor/semantics compatibility |
| Authorization | Shared authorization / human policy | Exact proposal scope/hash, required human confirmation and risk policy |

Warnings and uncertainty require operation-specific policy. A warning may be accepted only where the relevant domain contract permits an acknowledged exception. Unknown or Unsupported is never a Pass. A numerically Indeterminate predicate cannot be treated as a successful hard constraint.

## 9. Operation risk and review policy

Review depth is based on semantic impact, not simply the number of pointer gestures.

- **Low impact:** view-only actions and reversible staging changes need no formal review.
- **Bounded direct edit:** a simple manual geometry adjustment may use a direct typed operation if the descriptor, write path and validation gates are already adopted and the operation policy permits it.
- **Review required:** AI-proposed geometry, entity duplication, connection endpoint changes, multi-object edits, template instantiation and changes with dependent relationships require preview and explicit review.
- **Human confirmation required:** any operation whose adopted policy requires human approval, any high-impact semantic change, and any operation with unresolved material warnings or acknowledged exceptions.
- **Unsupported:** no preview can authorize an operation whose formal capability or descriptor is not adopted.

AI can propose, explain and assemble a Candidate. It cannot bypass review, allocate formal identities, choose an unauthorized write path or directly mutate Core Domain state.

## 10. Cancellation, undo and recovery

Three concepts must remain distinct:

1. **Cancel staging:** discard the current uncommitted preview/gesture. Formal state remains unchanged.
2. **Undo local edit:** revert a transient Editor edit before submission. This does not create History.
3. **Undo a committed change:** use the existing History/Branch semantics and an explicitly supported compensating or reverse operation. The Editor must not invent an inverse Command or rewrite old history.

After a formal submission:
- success is shown only after authoritative reconciliation;
- validation rejection leaves formal state unchanged and retains useful diagnostics;
- Asset publication without formal reference adoption is handled by the existing orphan-reconciliation contract;
- an unknown submission outcome is reconciled before retry, using ME-06 idempotency/recovery rules;
- renderer/index/cache failure does not invalidate a successful formal geometry commit; derived state is rebuilt;
- application restart during staging may recover or discard temporary work, but temporary files never prove formal publication.

Because the product is single-user, no collaborative locks or multi-user conflict UI is introduced. Source revision/fingerprint and query-freshness checks remain necessary to prevent stale local operations, outdated previews and accidental overwrites.

## 11. Failure outcomes

| Condition | Required behavior |
|---|---|
| Operation unsupported by descriptor | Block before formal submission; explain missing capability |
| Target identity or component reference missing | Reject with typed diagnostic; do not guess by display name or geometry |
| Wrong editing frame / transform unavailable | Block or return Unsupported; do not reinterpret coordinates |
| Geometry invalid | Reject or retain as preview with diagnostics; no silent repair |
| Domain connection endpoints incompatible | Block connection creation/rebinding |
| Group reparent attempted as geometry move | Perform membership-only operation or require a separately named compound transform |
| Required template member unsupported | Do not claim full instantiation; retain preview and report blocker |
| Query evidence stale | Re-resolve and regenerate preview |
| Formal write path ambiguous | Ask for explicit scope or block; never infer from scene |
| Formal submission rejected | Keep authoritative state unchanged |
| Commit outcome unknown | Reconcile before retry |
| Derived cache rebuild fails | Preserve formal result; mark derived view stale and rebuild |

## 12. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| SO-001 | Select a Building and switch to Interior | View/context transition only; no formal mutation |
| SO-002 | Move a Room boundary in Building-local coordinates | Local geometry changes only; Building world footprint unchanged |
| SO-003 | Move a Building on the world map | World placement/footprint operation; interior local coordinates are not rewritten |
| SO-004 | Drag an entity from one Spatial Group to another | Membership changes only; geometry unchanged |
| SO-005 | Transform all members of a Group | Requires explicit compound transform, declared target set and preview |
| SO-006 | Two spaces touch geometrically | No Door or Passage is created |
| SO-007 | Create a Door between two Rooms | Typed connection intent, compatible endpoints and specialist validation required |
| SO-008 | Connect three rooms through a hall | Hall is a spatial entity; each connection has two endpoints |
| SO-009 | Instantiate a Residence template | Staged set of adopted typed entities and relationships; dependency-closed validation and review |
| SO-010 | Instantiate a Magic Academy composition | Multiple Buildings/outdoor spaces/modules; unsupported required types block full formal instantiation |
| SO-011 | Duplicate a Building | New identity; child/connection copying follows explicit rules; no stable IDs cloned |
| SO-012 | Attempt formal split while split is deferred | Unsupported; cannot emulate with add/remove |
| SO-013 | Apply a stale snap/query result | Re-resolve evidence and regenerate preview or reject |
| SO-014 | Edit a Shared Object definition | Shared-definition semantics apply to referencing Maps |
| SO-015 | Add a post-zero spatial change in one active Map | Only selected Map/Branch History changes |
| SO-016 | Cancel a staged multi-object operation | No formal objects, relationships or Commands are created |
| SO-017 | Formal commit succeeds but renderer rebuild fails | Formal result remains successful; derived state is marked stale/rebuilt |
| SO-018 | Local app restarts during staging | Staging recovers or is discarded; no false formal-success claim |
| SO-019 | A local interior anchor connects to a world access point | Requires explicit typed boundary binding and transform |
| SO-020 | A template contains an unadopted Portal type | Unsupported item is shown; complete instantiation is blocked |

## 13. Explicit non-goals

ME-08-06 does not:
- define a generic universal spatial Object or Edge;
- introduce a universal topology graph;
- make Spatial Group a physical container by default;
- define arbitrary user scripting or third-party executable plugins;
- authorize new Object Types, Relationship Types, Commands, schemas, APIs or persistence tables;
- define complete 3D solid/BIM/CAD editing;
- enable formal geometry split/merge;
- add collaborative editing, multi-user locks or conflict resolution;
- replace ME-06 Candidate/Change Set/recovery contracts or Core Domain History.

## 14. Completion criteria and next work

ME-08-06 is a working baseline when:
- all formal spatial operations have explicit typed intent, target context and authority routing;
- view, organization, geometry and domain-relation changes are distinguishable;
- world/local coordinate boundaries are explicit;
- template instantiation is dependency-closed and reviewable;
- validation and failure outcomes are typed and explainable;
- staging, formal commit, History undo and derived-cache refresh are not conflated;
- no existing ME-04/05/06 authority boundary is bypassed.

Recommended next item: **ME-08-07 — Spatial Operation UX & Scenario Matrix**, translating this model into concrete creator workflows and tool behavior for World Map, Building Interior, Cave, Spatial Site composition, templates and connection editing. After that, perform an ME-08 cross-architecture review before implementation handoff.

## 15. Related documents

- ME-08-00 Spatial Operations & Domain Integration V0.1.
- ME-04-B Building Model & Authoring Workflows.
- ME-04-C Floors, Levels & Vertical Organization.
- ME-04-D Rooms, Spaces & Interior Geometry.
- ME-04-E Entrances, Openings & Vertical Circulation.
- ME-04-F Underground Spaces, Caves & Connected Facilities.
- ME-04-H Cross-architecture Integration Review.
- ME-05-02 Shared Geometry Model & Component Operations.
- ME-05-03 Topology & Spatial Predicate Contract.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract.
- ME-05-06 Cross-architecture Integration Review & V1 Gates.
- ME-06-D Typed Intent & Formal Operation Routing.
- ME-06-E Candidate Review & Selection Contract.
- ME-06-F Recovery, Accessibility & Acceptance Contract.
- ME-06-G Cross-architecture Integration Review.

---

ME-08-06 V0.1 is a product/editor integration working baseline. It is not a frozen Core Domain contract or implementation authorization.
