# World View P2-09-22 — V1 Built-in Relationship Type Seed Catalog V0.1

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended initial relationship-type seed set — bounded geography/settlement network semantics; specialist political, ecological and movement relationships remain deferred
- Depends on: P2-09-20 V0.1; P2-09-21 V0.1; P2-09-18 V0.2; P2-09-19 V0.2; ME-01-04 V0.1; ME-01-07 V0.1

## 1. Purpose

Select the smallest useful built-in Relationship Type set needed by the first World View geography and settlement workflows. The seed catalog translates ME-01-04's cross-scale relationship categories into explicit Core Domain semantics without turning every geometric predicate or narrative association into a persisted relationship.

This is a recommended seed set, not an exhaustive ontology. Each relationship type must still have its endpoint matrix, cardinality, cycle rules, temporal class and typed attribute schema validated before it is enabled.

## 2. Selection principles

1. A formal relationship records semantic/domain meaning, not merely a geometric calculation.
2. Every relationship has typed source and target Object endpoints.
3. Directionality and symmetry are properties of the Relationship Type descriptor.
4. Inverse display wording does not create a second persisted relationship.
5. Cross-scale relationships are allowed; Region/Locality/Site scopes do not impose a parent-child ontology.
6. A geometric predicate may be used as validation evidence, but it does not automatically create a relationship.
7. Only relationships with concrete endpoint Object Types in the V1 seed catalog may be enabled.
8. Time-varying relationships belong to the selected Map/Branch History; Project-shared definition relationships are used only for truly shared base semantics.
9. Do not model World Knowledge relations such as character membership, species culture or migration intent as Core Domain spatial relationships unless the relevant entities are explicitly spatialized as formal Objects.
10. No relationship table is added; the P2-09-20 typed payload/Command model remains authoritative.

## 3. Recommended initial Relationship Type set

| Stable Relationship Type ID | Direction / symmetry | Candidate endpoint pairs | Primary semantics | Temporal class | Initial status |
|---|---|---|---|---|---|
| `spatial.contains` | Directed: container → contained | PoliticalRegion → Settlement; Landmass → MountainRange; Settlement → PointOfInterest; TerrainRegion → PointOfInterest | Explicit semantic/spatial containment | Initial State or History-local where containment changes | Include with bounded endpoint matrix |
| `structural.part_of` | Directed: part → whole; acyclic where declared | MountainPeak → MountainRange; component Settlement → parent Settlement only if hierarchy is explicitly modeled | Structural composition/part-whole semantics | Usually Initial State; History-local only for real domain change | Include for approved pairs |
| `spatial.adjacent_to` | Symmetric | PoliticalRegion ↔ PoliticalRegion; TerrainRegion ↔ TerrainRegion; Landmass ↔ Landmass | Explicit meaningful adjacency, not arbitrary distance | Usually Initial State; History-local if boundaries/adjacency change | Include with explicit confirmation |
| `network.connects_to` | Symmetric by default; descriptor may constrain typed endpoint role | Road ↔ Road; Road ↔ Settlement | Formal network/access connectivity | Initial State or History-local | Include with bounded endpoint matrix |
| `hydrology.flows_to` | Directed: upstream → downstream | River → River; River → Lake | Hydrological flow connection | Initial State or History-local | Include only after ME-02 flow rules |

These are five seed descriptors, not five universal relationship tables. Each is a statically registered type contract.

## 4. Endpoint and validation notes

### 4.1 spatial.contains

- The source is the container; the target is the contained Object.
- `within` is the inverse UI phrase for the same relationship; do not persist both directions.
- Endpoint compatibility is explicit. A geometric polygon containing a point is not enough to create the relationship.
- Containment may be semantic, administrative or site/structure containment; if those meanings require different cardinality or temporal rules, use separate Relationship Type IDs rather than one generic relation.
- A containment cycle is rejected unless a specialist type explicitly defines a non-tree semantic where cycles are valid.
- A single target may be contained by multiple containers only where the endpoint/semantic rule allows it (for example, overlapping administrative and ecological scopes are distinct types, not duplicate instances of one type).

### 4.2 structural.part_of

- The source is the part; the target is the whole.
- It is not equivalent to geometric containment.
- A component/part relation is valid only when both Objects have independently meaningful semantic identities.
- The descriptor defines whether a part may have one or multiple wholes and whether cycles are forbidden. The initial built-in pairs should be acyclic.
- Geometry component split/merge does not create or remove this relationship automatically.

### 4.3 spatial.adjacent_to

- Symmetric; Core Domain canonicalizes endpoint ordering and rejects duplicate reverse pairs.
- Adjacency must have a declared meaning (shared boundary, direct neighborhood, or another explicit semantic predicate).
- A mere distance threshold or viewport proximity is not sufficient.
- If adjacency is fully derivable from geometry and not creator-confirmed as a world fact, keep it as a derived spatial predicate rather than a formal relationship.

### 4.4 network.connects_to

- Represents explicit network connectivity or access, not geometric touching.
- The descriptor must declare whether connectivity is bidirectional or whether a specialist directed network type is required.
- A Road endpoint snapped to another Road/Settlement does not automatically establish connectivity.
- If the relationship changes with bridge closure, road damage or access control, those are History-local relationship changes or separate typed runtime state operations.

### 4.5 hydrology.flows_to

- Directed from upstream/source to downstream/receiver.
- The initial V1 endpoint set is River→River and River→Lake; ocean/outlet/estuary endpoints remain deferred until a suitable formal Object Type exists.
- Geometry digitization direction is not authoritative flow direction.
- The type must validate network continuity and the permitted cycle/distributary policy. Do not assume all river networks are simple trees.
- Flow relationships may cross PoliticalRegion/TerrainRegion/working-scope boundaries; they are not forced into containment.

## 5. Explicitly deferred Relationship Types

| Candidate | Reason for deferral | Future prerequisite |
|---|---|---|
| `political.controlled_by` | No formal Polity/Government Object Type is in the initial seed set; a PoliticalRegion boundary alone is not the controlling actor | ME-03 political entity/authority semantics and temporal succession model |
| `ecology.habitat_of` / `species.prefers` | Species and habitat preference are World Knowledge facts by default, not proof of actual territory | Explicit spatialized species/population domain and distinction between preference, suitability and actual distribution |
| `movement.migrates_to` | A narrative movement/migration fact is not a static spatial relationship; may need Event/Unit movement | Event/Unit movement semantics, route and duration contract |
| `movement.travels_along` | Requires Unit/Group, route and deterministic movement phases | Future Unit/Movement contract |
| `spatial.intersects` | Usually a geometric predicate, not a semantic relationship | Keep derived unless a specialist domain makes intersection itself a confirmed fact |
| `spatial.near` | Distance/proximity is scale- and threshold-dependent and often derived | Explicit domain meaning and precision/threshold policy |
| `political.belongs_to` | Could refer to Character, Culture, Faction or Polity in World Knowledge rather than formal map Object | World Knowledge relationship contract or explicit spatialized Object endpoints |
| `water.drains_to` for watersheds | Watershed Object Type is not in the initial seed set | ME-02 Watershed semantics and flow-network validation |

## 6. Temporal and persistence rules

- Initial State relationships are part of the Map time-zero baseline and are inherited by Branches through the existing Map/History baseline semantics.
- A truly Project-shared structural relationship is stored in one explicit Shared Object definition owner payload; it must not be duplicated across both endpoint definitions.
- Time-varying relationships are established/ended by `core.relationship.establish` / `core.relationship.end` Commands in the selected History.
- A Branch may end an inherited relationship only after its exact `parent_at`; the end is Branch-local.
- Relationship IDs are generated by Core Domain and are never reused after formal termination/removal.
- Relationship schema/semantics identifiers participate in Checkpoint fingerprints when the relationship affects reconstructed World State.
- Typed endpoint references use the required `core_command_object_ref` link for Command-based relationship changes.
- No standalone relationship table, generic graph database or second ordering/dependency graph is introduced.

## 7. Relationship Type descriptor template

Every enabled seed type must fill this complete descriptor record:

| Field | Required content |
|---|---|
| relationship_type_id | Stable non-localized ID |
| schema_version / semantics_id | Exact schema and deterministic semantics identity |
| source_type_keys / target_type_keys | Explicit allowed endpoint type matrix |
| directionality | Directed or symmetric; canonical endpoint ordering |
| self_reference | Allowed or forbidden |
| cardinality | Per-source/per-target active relationship limits |
| uniqueness | Exact duplicate definition |
| temporal_class | Initial State, Shared Definition or History-local |
| attributes | Typed relationship attribute schema |
| geometry_preconditions | Optional explicit topology/geometry checks |
| cycle_policy | Allowed, forbidden or bounded specialist policy |
| establishment/end | Preconditions and state transition behavior |
| branch_behavior | Inheritance and local termination behavior |
| compatibility | Historical semantics retention/migration rule |

No type is enabled while required descriptor fields remain unspecified.

## 8. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| RTS-01 | Store contains and inverse within phrases | One directed relationship; inverse phrase is a view |
| RTS-02 | Symmetric adjacency submitted in reverse endpoint order | Same canonical identity/order; no duplicate reverse relation |
| RTS-03 | Polygon contains a point but creator did not confirm semantic containment | No formal relationship automatically created |
| RTS-04 | Road endpoints snap together | No connects_to relation without explicit accepted intent |
| RTS-05 | River line direction conflicts with flows_to | Relationship direction follows explicit hydrology semantics, not digitization order |
| RTS-06 | Flow graph includes a forbidden cycle | Rejected with an explainable relationship path |
| RTS-07 | Political control is proposed without a formal Polity endpoint | Deferred/unsupported; no fabricated Object |
| RTS-08 | Character/species relationship is described in story | Stored as World Knowledge, not Core Domain spatial relationship by default |
| RTS-09 | Branch ends an inherited adjacency/containment relationship | Branch-only effect after parent_at |
| RTS-10 | Relationship type semantics version changes | Existing relationships replay with pinned semantics or explicit migration |
| RTS-11 | Derived intersects/near predicate changes after geometry edit | No formal relationship Command unless explicitly adopted |
| RTS-12 | Same relationship descriptor replayed with same inputs | Identical relationship state and cardinality result |

## 9. Remaining design gates

1. Finalize exact endpoint type matrices and cardinalities for each enabled seed type.
2. Validate hydrological flow cycle/distributary policy with ME-02.
3. Decide which containment/part-of pairs are baseline facts versus History-local changes.
4. Confirm whether adjacency is formal or derived for each initial workflow.
5. Defer political control until ME-03 defines the formal political actor model.
6. Confirm World Knowledge-to-Core Domain reference behavior for character/species/place entities without creating shadow Object identities.

## 10. Status

P2-09-22 V0.1 recommends five initial Relationship Type descriptors aligned with ME-01-04's multi-scale spatial model. It explicitly keeps geometry predicates, story knowledge relations and specialist political/ecological/movement semantics outside the initial Core Domain relationship seed set.

It is a recommended domain catalog, not an executable ontology, SQL migration, implementation code or implementation authorization.
