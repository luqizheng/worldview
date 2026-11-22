# CD-02-G3 — Atomic Geometry Component Publication Contract

- Version: 0.1
- Date: 2026-11-22
- Status: Adopted implementation contract; draft runtime implementation is tracked in PR #7 and remains unmerged
- Applies to: Building/Cave typed aggregate Definition publication in V1
- Depends on: P2-09-19 V0.2; P2-09-26 V0.2; P2-09-41 V0.4; P2-09-42 V0.9 proposal; P2-09-13 V0.7; ME-05-02 V0.1; ME-05-05 V0.3

## 1. Decision

A formal Geometry Component manifest and its typed relational Asset references are one published Definition aggregate. They must become visible together or not at all.

For the adopted Building/Cave typed aggregate profile:

1. The owner payload is authoritative for component identity, geometry role/family, dimension, spatial reference, geometry schema version, Asset ID, content hash, provenance and validity metadata.
2. The core_object_definition_asset role is the typed-link purpose discriminator: geometry_component. It is not the Object Type's geometry role.
3. The actual geometry_role remains in each owner payload component record and is validated against the adopted Object Type descriptor/schema.
4. There is one core_object_definition_asset row per distinct Asset ID per owner Object. If multiple component records reference the same Asset ID, they must pin the same content hash and share that one link row. A component identity is never inferred from the relational link.
5. The distinct Asset ID set in the complete owner manifest must exactly equal the distinct Asset ID set in the owner's geometry_component typed links. Missing and unexpected links are both rejected.
6. Every referenced Asset must exist in the same Project, be Verified, and have an authoritative core_asset.content_hash exactly equal to the payload's pinned hash.
7. Definition CAS update and complete geometry Asset-link reconciliation execute in one coordinated SQLite write transaction.
8. A definition with a new or changed formal geometry manifest must not be committed before its complete typed-link set. The previous two-step “edit payload, then add link” path is not an accepted publication workflow.

This decision follows the separation already established by P2-09-19: component semantics belong to the typed owner payload; immutable bytes belong to Core Asset; the relational link establishes owner-to-Asset reference integrity. Keeping the descriptor geometry role in the link row duplicates component semantics and makes a single Asset referenced by multiple component roles require multiple redundant link rows.

## 2. Scope and compatibility

### 2.1 Current V1 publication scope

This contract is executable first for Building and Cave System because their adopted typed aggregate schemas explicitly contain geometry_components and their child-context rules.

The ten legacy compact Object Types in P2-09-26 V0.2 do not yet persist the common Geometry Component manifest as a typed definition schema. Their current in-memory GeometryComponentManifest validation is not proof of owner-payload persistence. They must not be silently treated as fully published generic geometry aggregates. A future descriptor/schema revision must adopt the manifest payload before enabling the same atomic publication workflow for those types.

### 2.2 Initial State and Command owner contexts

- Map Initial State geometry Asset links remain disabled under the current Building/Cave Initial State payload because it contains membership/placement, not distinct formal geometry Asset references.
- core_command_asset remains the required link for formal geometry Commands when the Command subsystem is implemented.
- This contract does not add a component table, generic owner table, Command table, Initial State Asset table or any other DDL.

### 2.3 Existing link rows

The geometry_component role is the adopted typed-link value for this profile. Before production rollout against databases that may already contain legacy descriptor-role rows, the implementation must either:
- provide a deterministic data migration that maps the existing typed aggregate geometry link rows to geometry_component without uniqueness loss; or
- reject/require explicit package migration before enabling the new publication path.

Do not silently reinterpret old rows at read time.

## 3. Publication API boundary

Introduce one Domain operation for the complete Definition publication, conceptually:

PublishDefinitionWithGeometryAssets(projectId, objectIdentityId, expectedDefinitionHash, canonicalDefinitionPayload, nowUtcMs)

The operation owns:
- Object lifecycle and Object Type pin validation;
- expected Definition hash CAS;
- typed aggregate schema validation;
- complete Geometry Component collection validation;
- Asset existence, integrity state and hash validation;
- complete typed-link set equality;
- atomic Definition update and typed-link reconciliation.

The operation must not accept a caller-supplied partial list of links that can disagree with the payload. The link set is derived from the validated canonical owner payload.

Asset file publication and byte hashing happen before this short DB transaction. The operation only references already published immutable Core Assets and re-reads their authoritative metadata inside the transaction.

## 4. Transaction algorithm

Within one SqliteConnectionAccess critical section and one SQLite transaction:

1. Re-read the Object identity, Object Type pin, lifecycle state and current Definition row.
2. Require active Object and exact expectedDefinitionHash match.
3. Canonicalize the supplied payload and validate it using the pinned typed aggregate schema.
4. Validate the complete Geometry Component manifest collection:
   - component IDs are non-empty and unique within the owner snapshot;
   - role is declared for this Object Type;
   - role min/max and any combined minimum are satisfied;
   - family is permitted for that role;
   - dimension and geometry schema version are supported;
   - spatial reference and content hash are valid;
   - child context resolves within the same typed aggregate.
5. Build a map of distinct asset_id to content_hash. If one Asset ID appears more than once with different hashes, reject.
6. Re-read every referenced core_asset row in the same transaction. Require same Project, integrity_state='verified', and exact content hash.
7. Build the expected typed-link set as one row per distinct Asset ID with role='geometry_component'.
8. Read the existing owner link set. The current Building/Cave profile owns only geometry_component links; reject any legacy descriptor-role or unknown-purpose row. A future typed Asset purpose requires an explicit schema/validator extension and is not silently preserved.
9. Replace/reconcile the geometry-component link set and update the Definition payload/hash/version in the same transaction.
10. Commit once. Any validation, FK, uniqueness, CAS or storage failure rolls back both Definition and link changes.

The adapter must not trust Domain service pre-reads for mutable facts. It re-reads lifecycle, Definition CAS, Asset integrity and Asset hashes inside the transaction.

## 5. Edit and removal semantics

- EditDefinition remains available for non-geometry Definition edits only when the complete geometry manifest and its typed-link set remain unchanged.
- Any add/remove/replace of a Geometry Component or any change to a component's Asset ID/hash must use the complete atomic publication operation.
- Removing a component removes the corresponding geometry Asset link only when no remaining component in the same owner references that Asset.
- If one Asset is still referenced by another component, its single owner link remains.
- A failed edit leaves the previous Definition and previous link set unchanged.
- A retry uses the same stable component IDs when the logical component is unchanged; it must not allocate a replacement component ID merely because publication failed.

## 6. Prohibited workflows

- Committing a new formal manifest and adding its typed Asset link in a later transaction.
- Committing a typed link before the matching owner manifest exists.
- Treating a non-empty link list as the only trigger for set-equality validation; an empty link list must fail when the owner manifest references Assets.
- Ignoring a missing AssetCatalog entry or silently accepting an unverified Asset.
- Overwriting duplicate manifest Asset IDs with last-write-wins hash behavior.
- Storing Object Type geometry role in the typed-link purpose field for this Building/Cave profile.
- Adding a generic Geometry Component table or untyped Asset owner table.
- Running file publication, LLM work, user interaction or network operations while the SQLite write transaction is open.

## 7. Required tests

| Test | Required assertion |
|---|---|
| Initial empty geometry set | Definition with no geometry components commits with no geometry link rows |
| Successful first publication | Definition and all distinct geometry_component links commit together |
| Missing Asset | Reject; prior Definition and links unchanged |
| Asset not Verified | Reject; prior Definition and links unchanged |
| Content hash mismatch | Reject; prior Definition and links unchanged |
| Missing link prevention | No committed manifest can reference an Asset without its typed link |
| Unexpected link prevention | No extra geometry link can exist without a manifest Asset reference |
| Duplicate component ID | Reject before persistence |
| Duplicate Asset ID / same hash | Allow one distinct Asset link for the owner |
| Duplicate Asset ID / different hashes | Reject |
| Multiple component roles sharing one Asset | One generic typed link; roles remain in owner payload |
| Stale Definition CAS | Reject without modifying Definition or links |
| Component removal | Remove link only when no remaining component references the Asset |
| Mid-transaction failure | Roll back both Definition and all link changes |
| Cross-Project Asset | Reject |
| Concurrent publication | Exactly one expected-hash CAS succeeds |
| Legacy typed-link row | Require deterministic migration or fail closed |

Run the suite against real SQLite and the same SqliteConnectionAccess used by the package database. Include Windows/macOS Debug and Release CI.

## 8. Acceptance boundary

This contract closes the semantic ambiguity for Building/Cave Definition geometry Asset links. It does not claim that the API, migration, tests, Initial State Assets, formal Geometry Commands or generic legacy Object Type manifest persistence are implemented.

The implementation is accepted only when:
- the exact role vocabulary is reflected consistently in Domain, Infrastructure, tests and documentation;
- the full Definition + typed-link set is atomic;
- all required failure-injection and CAS tests pass;
- any pre-existing role-row migration is deterministic and tested;
- no caller can bypass the complete publication boundary through a single-link public API.
