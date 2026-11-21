# World View P2-09-29 — Domain Descriptor Cross-Registry Consistency Contract

- Version: 0.1
- Date: 2026-11-21
- Status: **Recommended cross-registry working contract** — not a frozen ADR or implementation authorization
- Depends on: P2-09-15 V0.5 Command Catalog; P2-09-17 V0.5 Command Payload Contract; P2-09-18 V0.2 Object Type Contract; P2-09-19 V0.2 Geometry Component Contract; P2-09-20 V0.2 Relationship Contract; P2-09-22 V0.2 Relationship Seed Catalog; P2-09-23 V0.1 Command Payload JSON Schema; P2-09-26 V0.2 Object Type Registry; P2-09-27 V0.2 Relationship Registry; P2-09-28 V0.2 Validation Vectors; ME-02-04/05/06/07

## 1. Purpose

This contract closes consistency gaps among the Core Domain Command catalog, Object Type descriptors, Relationship Type descriptors and validation fixtures. The registries are static machine-readable catalogs, not executable validators. Core Domain owns runtime validation, including cross-record invariants that JSON Schema cannot express.

## 2. Authority and scope matrix

The same semantic Object identity can be Project-shared while its presence, placement, runtime attributes, geometry membership and relationships differ by Map/Branch.

| Fact | Authority | Time behavior | Mutation route |
|---|---|---|---|
| Object identity and type binding | Core Domain Project identity registry | Stable identity; reservation lifecycle | Reservation / identity Domain Services |
| Project-shared current definition | Shared Object definition | Retroactive for every referencing Map/Branch | Shared Object definition Domain Service |
| Map-local definition | Owner Map | Current definition scoped to one Map | Map-local definition Domain Service |
| Membership, placement and initial runtime values | Map Initial State | Time-zero baseline; inherited by Branches | Map Initial State Domain Service |
| Post-zero presence/runtime/geometry | Selected Map/Branch History | History-local deterministic Commands | Core Domain Command services |
| Project-shared base relationship | One owning Shared Object definition | Retroactive current-definition | Shared Object definition Domain Service |
| Map-specific initial relationship | Map Initial State | Time-zero Map baseline | Map Initial State Domain Service |
| Post-zero relationship change | Selected Map/Branch History | History-local Commands | Relationship Commands |
| Derived terrain/hydrology/climate/ecology | Derived Analysis | Rebuildable, fingerprinted output | Calculation services; separate adoption boundary |
| Presentation | Editor/presentation subsystem | Not formal World History by default | Presentation services |

Every fact has exactly one authoritative owner. The same attribute or relationship must not be duplicated as authoritative data in multiple scopes.

## 3. Object Type descriptor rules

### 3.1 Definition, Initial State and runtime

- Definition fields describe the current base definition of an Object identity.
- Initial State fields describe Map-specific time-zero membership, placement and initial runtime values.
- Runtime fields describe values that may change within one Map/Branch History after time zero.
- Derived and presentation fields are not writable Object attributes unless separately promoted by an explicit domain contract.
- Project-shared definition edits are retroactive under the accepted current-definition model. They are not core.object.attribute.change Commands and do not create a revision chain.

### 3.2 Command capabilities

Command capabilities are structured policies, not generic CRUD flags.

- core.object.create introduces the presence of an already Bound identity into a selected post-zero History. It does not allocate/bind identity or create relationships implicitly.
- Map Initial State creation/membership is a separate baseline operation; a time-zero Command is invalid.
- core.object.activate/deactivate alter only selected-History presence. They do not retire Project identity or cascade relationship changes.
- core.object.attribute.change is enabled only for fields explicitly declared as History-local runtime mutable. Definition fields use the definition service; Initial State fields use baseline authoring.
- V1 seed descriptors currently enable no runtime attribute fields for historical mutation. The generic Command descriptor may exist, but each Object Type rejects it until typed runtime fields and deterministic semantics are registered.
- Geometry Commands change only declared Geometry Component roles in the selected History. They do not change semantic Object identity or infer relationship changes.
- Relationship Commands are enabled only where the Relationship Type descriptor permits the endpoint pair, storage scope, temporal class and endpoint activity policy.

## 4. Relationship semantic distinctions

### 4.1 spatial.contains

Strict spatial containment supported by target extent/anchor within source extent. It does not mean ownership, sovereignty, jurisdiction, political control or structural composition.

V1 endpoint pairs:
- PoliticalRegion → Settlement
- Landmass → MountainRange
- Landmass → Settlement
- Settlement → PointOfInterest
- TerrainRegion → PointOfInterest

Geometry validates a proposal but never creates the relationship. Pair-specific cardinality is in P2-09-27 V0.2.

### 4.2 structural.part_of

Explicit semantic composition/part-whole membership, not geometric containment.

V1 pair: MountainPeak → MountainRange. A peak has at most one active parent range; a range may contain many peaks. Settlement hierarchy is deferred to ME-03.

### 4.3 spatial.adjacent_to

Explicit adjacency between compatible regions, normally a shared boundary or domain-specific predicate. Distance alone is insufficient.

V1 pairs:
- PoliticalRegion ↔ PoliticalRegion
- TerrainRegion ↔ TerrainRegion

Landmass ↔ Landmass is excluded: touching land areas form one continuous Landmass under this Object model.

### 4.4 network.connects_to

Symmetric physical continuity between two distinct Road Objects at an explicit network junction.

V1 pair: Road ↔ Road. Road ↔ Settlement is not this relation. Settlement access and entrances are deferred to ME-03 and must not be smuggled into a symmetric connection relation. Snapping or geometric touch alone is insufficient.

### 4.5 hydrology.flows_to

Directed formal River flow: source is upstream River; target is downstream River or receiving Lake.

V1 graph rules:
- River → River and River → Lake are the only endpoint pairs.
- Each River has at most one outgoing downstream receiver across both pairs.
- A River may have multiple incoming tributaries.
- A Lake is terminal and cannot be a source.
- Directed cycles are rejected.
- Distributaries, multiple downstream receivers, Lake outflow and River → Ocean/Estuary remain outside formal V1 relationships. Derived hydrology may represent richer evidence without promoting it to the formal graph.

## 5. Relationship storage and temporal scope

Every relationship instance is stored in exactly one owning scope:

1. shared_object_definition: stable base relationship between Project-shared Objects; one explicit owner endpoint; retroactive current-definition semantics.
2. map_initial_state: Map-specific relationship true at time zero; inherited by Branches through the Map baseline.
3. history_local: post-zero relationship change established/ended through Commands in the selected History.

Recommended allowed scopes:
- structural.part_of: shared definition or Map Initial State.
- spatial.contains, spatial.adjacent_to, network.connects_to and hydrology.flows_to: Map Initial State or History-local.

A descriptor permitting a scope does not make every instance global. Both endpoints, intended meaning and owner scope must satisfy the descriptor.

## 6. Command–descriptor cross-validation

Before persistence and replay, Core Domain validates:
1. Command type is registered and enabled.
2. Descriptor schema and execution-semantics versions are supported.
3. Payload schema version matches the envelope descriptor version.
4. Object references resolve to the same Project and permitted Map/History context.
5. Every referenced Object Type permits the operation and role.
6. Geometry role/family/cardinality is valid at the effective boundary.
7. Relationship endpoint pair, direction, symmetry, cardinality, cycle policy, temporal scope and endpoint activity pass.
8. The Command is post-zero and belongs to exactly one History.
9. A Branch-owned Command has effective_time > parent_at; inherited ancestor history is visible only through effective_time <= parent_at.
10. Shared-definition edits never enter the post-zero Command path.
11. The operation does not mutate another Map, parent Branch or sibling Branch.
12. Replay uses captured payloads/Assets and exact compatibility identifiers.

## 7. Object creation and geometry boundaries

- A reservation is created and bound before a post-zero create Command.
- core.object.create introduces the Bound identity into the selected History; it does not choose Project-shared versus Map-local identity scope. That scope is fixed by the identity/registration contract.
- Create payload may include typed initial runtime values and immutable geometry Assets only where the Object Type allows them.
- Initial time-zero membership and placement use Map Initial State authoring.
- Object creation never creates relationships from proximity, containment, overlap or snapping. Each relationship is separately authored in its proper scope.
- Geometry adjustment/refinement/add/remove changes component state only; semantic Object identity and Relationship state remain unchanged unless a separate explicit Domain operation is applied.

## 8. Map, Branch and checkpoint consistency

- Project-shared identity/base definition is shared; Map/Branch runtime state is isolated.
- Map Initial State is Map-specific and forms the root baseline for its Branches.
- Post-zero Commands and relationship changes belong to one History path.
- Child Branch inherits ancestor history through the inclusive boundary effective_time <= parent_at; child-owned changes must be strictly later than parent_at.
- Ancestor edits at or before the fork boundary are visible to the child; ancestor edits after the boundary are not.
- Sibling Branches never see one another's own changes.
- Shared-definition edits are retroactive and may invalidate checkpoints for every referencing Map/Branch, including reconstruction at earlier historical times.
- Checkpoint fingerprints include relevant Object Type and Relationship Type descriptor schema/semantics versions as well as source definition, Initial State, Command and Asset fingerprints.

## 9. Validation fixtures and status

P2-09-28 V0.2 adds fixtures for Road/Road versus Road/Settlement semantics; containment versus political control and structural part-of; River downstream cardinality, confluences and terminal Lakes; Shared definition retroactivity; Map/Branch relationship isolation and fork boundaries; descriptor-version checkpoint invalidation; Object creation and runtime attribute-change scope.

All vectors remain design fixtures. They have not been executed against a production Core Domain implementation.

## 10. Compatibility and adoption

The V0.2 registries are recommended catalogs, not frozen ADRs. Changes to endpoint semantics, allowed scopes, cardinality, cycle policy or Command capability must increment descriptor schema versions, change semantics IDs when domain meaning/replay interpretation changes, define migration/readers where persisted data exists, add validation vectors and checkpoint invalidation rules, and receive Core Domain owner adoption before implementation.

No standalone Relationship table, generic relationship CRUD layer, user-defined executable descriptor plugin system or general-purpose workflow engine is introduced.
