# World View P2-09-42 — Building/Cave Descriptor Registry Integration Contract

- Version: 0.7
- Date: 2026-11-21
- Status: Registry integration proposal — not a frozen ADR, adopted registry version, DDL or implementation authorization
- Depends on: P2-09-18 V0.2; P2-09-19 V0.2; P2-09-26 V0.2; P2-09-37 V0.6; P2-09-38 V0.5; P2-09-39 V0.5; P2-09-40 V0.6; P2-09-41 V0.4

## 1. Purpose

P2-09-41 proposes Building and Cave System Object Type descriptors using the existing V1 descriptor conventions plus three additional metadata blocks: a definition schema reference, an Asset contract and a child aggregate contract.

This document checks that proposal against P2-09-18 and P2-09-26 and defines the minimum registry-format integration required before those descriptors can be merged into the compiled Core Domain registry.

**Disposition:** keep P2-09-41 as a proposal extension. Do not append it directly to P2-09-26 V0.2. The current registry format does not yet declare all three additional blocks, and the new payload schemas/vectors remain design artifacts rather than executed proof.

## 2. Existing descriptor shape

P2-09-26 V0.2 descriptors currently use:
- type_key;
- schema_version;
- semantics_id;
- allocatable;
- geometry;
- definition;
- runtime;
- invariants;
- command_capabilities;
- definition_edit_policy;
- initial_state_policy;
- runtime_attribute_change_policy.

P2-09-18 V0.2 requires the executable descriptor model to cover more than the current compact registry shape, including typed definition/runtime/Initial State schemas, relationship participation, lifecycle, command capabilities, Asset contract and compatibility.

The existing ten Object Type entries must remain valid and retain their pinned semantics. A registry-format extension must be additive and must not reinterpret an existing type.

## 3. Required registry-format additions

The Building/Cave extension needs three additional descriptor blocks.

### 3.1 definition_schema_ref

Purpose: bind the complete typed Object definition payload to a strict machine-readable schema.

Rules:
- The reference is a build-time artifact reference, not a runtime file path or network URL.
- The schema is embedded/bundled with the compiled application and available offline.
- The compiled descriptor pins schema version and content hash.
- The schema must reject unknown fields and unsupported versions.
- The existing inline definition field catalog remains useful for compact field metadata and UI projection; it does not replace the full nested aggregate schema.
- Schema reference changes require a new descriptor schema version and compatibility review.

For the current proposal:
- Building → P2-09-38 V0.5.
- Cave System → P2-09-39 V0.5.

### 3.2 asset_contract

Purpose: declare which immutable Asset links and payload roles the Object Type may reference.

Required declaration:
- formal payload bytes are immutable Core Assets;
- the top-level Object remains the owner of every Geometry Component;
- child context is typed metadata inside the top-level Object owner payload;
- typed relational Asset links must agree with component asset_id and hash;
- no path, URL, mutable filename or arbitrary JSON-only Asset reference is authoritative;
- media/format compatibility is pinned to the adopted ME-05 package contract.

Building/Cave formal geometry in current definition payloads requires core_object_definition_asset. Map-specific Initial State Assets require core_map_initial_object_asset only when the Initial State payload actually references distinct formal Assets. History geometry Commands use core_command_asset.

Do not enable unused typed link tables merely for symmetry. P2-09-13 remains the source for table/link inventory and migration dependency ordering.

### 3.3 child_aggregate_contract

Purpose: describe stable child identities and their typed containment inside one top-level Object.

Required declaration:
- bounded child sections;
- child ID namespace scoped to the owner Object;
- uniqueness and no-reuse rules;
- copy semantics (new owner and new child IDs);
- allowed typed cross-references between child sections;
- retirement and unresolved-reference policy;
- whether child-level temporal Commands are enabled.

Child IDs are not Object IDs, do not appear in core_object, do not use the Object Type allocator and are never accepted by APIs expecting Object identity.

A generic owner_type/owner_id field is prohibited. Child geometry context must be a type-specific structure validated by the owning Object Type schema.

## 4. Descriptor compatibility and runtime packaging

At application build time:
1. Load the Object Type descriptor catalog.
2. Resolve each definition_schema_ref to the exact bundled schema artifact.
3. Verify the schema hash/version recorded by the descriptor.
4. Validate every descriptor entry against the registry-format schema.
5. Validate geometry role/family/dimension/cardinality and Command capability references.
6. Run the complete existing ten-type registry conformance suite plus Building/Cave descriptor and aggregate vectors.
7. Embed the resolved descriptor and schema resources into the application package.

At runtime:
- Core Domain reads only the compiled descriptor/schema resources.
- No dynamic network/schema download is permitted.
- Each bound Object pins type schema version and semantics compatibility identity.
- Existing Object definitions are never silently upgraded to the latest schema.
- An unsupported schema version is rejected or handled by an explicit migration path.

Checkpoint reconstruction fingerprints must include the pinned Object Type descriptor schema/semantics, current definition hash and all referenced immutable Asset hashes. P2-09-11 V0.11 already defines this responsibility; Building/Cave adoption must populate the existing fingerprint inputs rather than invent a second fingerprint system.

## 5. Geometry component child-context contract

P2-09-19 V0.2 remains authoritative:
- component identity is scoped to the top-level Object;
- component identity and role metadata live in the typed owner payload;
- geometry bytes live in immutable Assets;
- no Geometry Component table is introduced.

For Building, the typed child context may identify:
- Level ID;
- Space ID;
- Plan/Level Slice ID;
- Boundary ID;
- Entrance ID.

For Cave System, the typed child context may identify:
- Survey Frame ID is carried by `coordinate_context_id`, not by `child_context`;
- Chamber ID;
- Passage ID;
- Cave Entrance ID;
- Survey Station ID.

The context is validated against the same top-level Object aggregate. It is not a generic polymorphic owner and does not transfer Asset ownership to a child.

A component may have only the child-context fields allowed by its declared role. For example:
- Building space_region requires Space + Level Slice context.
- Building boundary requires Boundary context.
- Cave passage_plan requires Passage context.
- Cave survey_station requires Survey Station context.

The final schema must encode these role/context combinations with oneOf/conditional rules or equivalent compiled validation. A permissive bag of optional child IDs is not sufficient as the final executable descriptor.

## 6. Command capability integration

P2-09-41 reuses existing Command descriptors:
- core.object.create;
- core.object.activate / core.object.deactivate;
- core.geometry.adjust_component / refine_component / add_component / remove_component.

It does not add a new Command type.

Before enabling geometry Commands:
- the target component role must be enabled for that Object Type;
- the component and child context must resolve within the same owner Object;
- source Asset fingerprint and expected aggregate/geometry revision must match;
- add/remove must preserve all plan, boundary, Opening and connector references;
- remove must not leave dangling child references or violate minimum geometry cardinality;
- all geometry remains 2D in V1.

Building/Cave child catalog lifecycle is not a generic Command payload. Post-zero creation/retirement of Levels, Spaces, Openings, Cave Chambers or Passages remains disabled until separate deterministic History Command designs are adopted.

The current runtime schema is empty for both proposed Object Types. Operational door state, access state, cave flood/blockage state and historical construction/geological changes must not be placed in current definition fields as a shortcut.

## 7. Relationship Registry boundary

The registry extension must not add generic Relationship endpoint pairs for:
- Building→Level;
- Building→Space;
- Level→Space;
- Space↔Space adjacency/access;
- Cave Chamber↔Passage;
- Cave Entrance↔Building Entrance.

These are nested aggregate ownership or typed child-connection records, not Core Object-to-Object Relationships.

Building/Cave may participate in a future cross-domain Relationship only after the exact endpoint Object Types and semantics are adopted. Road access remains blocked by ME-03-H's RoadEndpoint identity finding. Cross-Building subsurface infrastructure remains a separate deferred Subsurface Network proposal.

## 8. Required amendments before registry merge

Before publishing a new P2-09-26 registry version:
1. Define the registry-format schema additions: definition_schema_ref, asset_contract and child_aggregate_contract.
2. Refine the Building/Cave JSON Schemas so geometry role/context combinations are strict.
3. Validate all 53 P2-09-40 vectors and add missing cases discovered by implementation.
4. Define exact Object definition and Map Initial State geometry Asset-link roles.
5. Confirm that the existing 20 Core Domain tables remain sufficient; update P2-09-13 only if an actual integrity/query requirement demands a new table.
6. Reconcile the Building/Cave Command capability map with P2-09-15/17 and the actual geometry handler contracts.
7. Run all existing P2-09-28 Object/Relationship registry vectors and all existing Object Type compatibility tests.
8. Record Windows/macOS canonical payload hash and geometry validation evidence.

A descriptor extension is not adopted merely because its JSON parses. It must pass registry-format, schema, cross-record, asset-link, command-capability and compatibility validation.

## 9. Current disposition

- P2-09-37 V0.6: Core Domain adoption architecture proposal.
- P2-09-38 V0.5: Building typed aggregate schema draft aligned to P2-09-19 geometry metadata.
- P2-09-39 V0.5: Cave System typed aggregate schema draft aligned to P2-09-19 geometry metadata.
- P2-09-40 V0.6: 53 design-only validation vectors; not executed.
- P2-09-41 V0.4: proposed Building/Cave Object Type descriptor extension.
- P2-09-42 V0.7: this registry integration and compatibility review.

The next step is a focused conformance pass: execute the 53 P2-09-40 vectors and existing P2-09-28 registry suite, finalize typed Asset-link semantics, and resolve any contradictions before generating the proposed P2-09-26 registry V0.3. Do not create DDL or implementation work from the current drafts.


## 10. V0.6 — Canonical Geometry Component owner metadata reconciliation

The V0.3 specialist schemas used an incomplete, incompatible vocabulary (`component_id`, `role`, `asset_sha256`) and omitted canonical owner metadata. The V0.5 schemas now conform to P2-09-19 V0.2.

Required canonical Geometry Component owner fields:
- `geometry_component_id`;
- `geometry_role`;
- `geometry_family`;
- `dimension`;
- `spatial_reference_id`;
- `geometry_schema_version`;
- `asset_id`;
- `content_hash`;
- `provenance`;
- `validity_metadata`.

Building/Cave context fields are additive and separately typed:
- `coordinate_context_id` identifies a Building-local horizontal frame or Cave Survey Frame; it is not the immutable spatial-reference identity.
- `child_context` identifies the allowed child aggregate record(s) for the component role.
- All child records that refer to formal geometry use explicit `geometry_component_id` naming.
- Opening anchors retain `source_content_hash` and require explicit remapping when the source boundary revision changes.

The immutable geometry Asset itself follows ME-05-04/P2-09-25: format/version, spatial-reference ID, axis order, coordinate unit, dimension, geometry family, precision profile, geometry body and optional source/provenance manifest. Mutable Object/Component identity, role and child context remain in the Core Domain owner payload.

P2-09-40 V0.6 contains 53 design-only vectors. JSON parsing and structural inspection have been performed; no production JSON Schema validator or Core Domain aggregate test suite has executed these vectors.

### 11.1 Typed Asset-link conditions

P2-09-13 V0.6 remains authoritative:
- `core_command_asset` is required for formal geometry Command Assets.
- The global inventory keeps `core_object_definition_asset` conditional across Object Types, but it is mandatory for this Building/Cave adoption profile because the adopted definition schema permits formal Geometry Components with immutable Asset references.
- `core_map_initial_object_asset` is not required by the current Building/Cave profile: Initial State contains Map membership/placement and does not carry distinct geometry Asset references. Revisit only if that payload changes.

The owner payload `asset_id/content_hash` pair and relational typed link must agree. The recommended V1 granularity is one immutable Asset per formal Geometry Component. Do not add a new component table or generic Asset owner table. Verify actual optional-link table presence and constraints in the implementation before proposing a migration.

### 11.2 Remaining adoption gate

Before proposing P2-09-26 V0.3:
1. Execute all 53 P2-09-40 V0.6 vectors against the strict schemas and aggregate validator.
2. Execute P2-09-28 V0.2 and all existing ten-type registry regression vectors.
3. Verify `core_object_definition_asset` and `core_command_asset` in actual Core Domain persistence, and confirm `core_map_initial_object_asset` remains unnecessary under the current Initial State payload.
4. Verify descriptor-format extension and offline schema hash pinning.
5. Reconcile Command handler payloads and the V1 2D geometry boundary.
6. Record Windows/macOS deterministic canonical payload and hash evidence.

No test pass, registry activation or DDL authorization is implied.


## 12. V0.7 — Asset-link row semantics and conformance-vector execution boundary

The P2-09-11 V0.11 typed link inventory defines `core_object_definition_asset` rows by owner identity, `asset_id` and `role`. The row does not contain `geometry_component_id` or `content_hash`.

For this adoption profile:

- Use `role = geometry_component` for a definition-owned formal Geometry Component Asset link.
- Keep `geometry_component_id`, `asset_id` and expected `content_hash` together in the typed Building/Cave owner payload.
- Resolve `asset_id` through `core_asset` and compare the authoritative Asset content hash with the owner payload.
- Verify that every unique Asset referenced by a formal component has the matching owner/asset/role link, and reject missing or unexpected links.
- If multiple component IDs reuse the same Asset, one owner/asset/role link row is sufficient; component-to-Asset identity remains explicit in the owner payload.

The current adoption therefore enables the existing `core_object_definition_asset` link; it does not add a component ID column or a new table. The Initial State Asset link remains out of scope unless a future Initial State payload introduces distinct immutable geometry Assets.

P2-09-40 V0.6 models the relational link projection separately from `core_asset` hash metadata. It also distinguishes spatial and measurement semantics tests from schema/aggregate validation. The exact runner contract and audit limitations are recorded in P2-09-44 V0.1.
