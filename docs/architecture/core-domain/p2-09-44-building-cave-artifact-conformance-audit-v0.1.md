# World View P2-09-44 — Building/Cave Artifact Conformance Audit

- Version: 0.1
- Date: 2026-11-21
- Status: Design-artifact audit and test-runner contract; not production validation evidence, DDL authorization, registry activation or implementation approval
- Scope: P2-09-37 through P2-09-43, P2-09-11, P2-09-13, P2-09-18, P2-09-19 and P2-09-26

## 1. Executive result

The Building/Cave proposal remains viable with the existing Core Domain aggregate ownership model. It does not require a new Geometry Component table or generic Asset-owner table.

This audit found and corrected four contract-level issues:

1. Previous Asset-link vectors represented `geometry_component_id` and `content_hash` as if they were columns of the typed Asset-link table. The existing link shape has owner keys, `asset_id` and `role`; the Core Asset row supplies the authoritative content hash.
2. The Building no-implicit-adjacency case was incorrectly marked as an invalid aggregate. It is a valid aggregate/query result: touching geometry without an explicit connection remains disconnected.
3. Cave local-versus-absolute altitude interpretation and geometric crossing/connectivity are semantic checks, not ordinary aggregate schema failures.
4. Cave Survey Frame identity was described as a child-context field even though the schemas correctly carry it as `coordinate_context_id`.

The revised corpus is P2-09-40 V0.6 with 55 design-only vectors. Structural checks have been performed; no production validator or Core Domain test runner has executed them.

## 2. Schema review

### 2.1 JSON Schema structure

P2-09-38 and P2-09-39 V0.5 declare JSON Schema Draft 2020-12 and use role-specific `oneOf` branches for Geometry Components.

The base `child_context` schemas enumerate the union of permitted context keys. Each role branch narrows that object with its own `additionalProperties: false`, required keys and role/family constants. The base and branch property sets are compatible; no prior `allOf` / `additionalProperties` conflict was found in these V0.5 schemas.

The role mappings are:

- Building: footprint → empty context; space_region → Space + Level + Level Slice; boundary/partition_annotation → Boundary; entrance_anchor → Entrance.
- Cave: chamber_plan → Chamber; passage_plan → Passage; cave_entrance_anchor → Cave Entrance; survey_station → Survey Station.

The schemas are valid JSON and their local references/role branches were structurally inspected. A conforming Draft 2020-12 JSON Schema implementation must still execute positive and negative schema vectors before these schemas can be called validator-proven.

### 2.2 Coordinate context versus spatial reference

`coordinate_context_id` identifies the specialist-local working frame: Building horizontal frame or Cave Survey Frame.

`spatial_reference_id` identifies the exact immutable domain spatial-reference definition carried by the canonical Geometry Component contract.

They are not interchangeable. For Cave components, the Survey Frame ID is carried by `coordinate_context_id`; it is not duplicated into `child_context`. The `child_context` identifies semantic child records only.

## 3. Validation-vector review

### 3.1 Structural inspection

P2-09-40 V0.5 was inspected before revision:

- 51 vectors, 26 Building and 25 Cave;
- unique vector IDs;
- every `input_source` resolves to a declared base fixture;
- every listed RFC 6902 patch operation applied to its base fixture during the structural check;
- 40 distinct stable error codes were present.

This check did not evaluate JSON Schema validity or Core Domain semantics.

### 3.2 V0.6 corrections

P2-09-40 V0.6 contains 55 design-only vectors and introduces an explicit vector contract.

- `external_asset_links` models the actual typed-link row projection: `asset_id` and `role` only.
- `asset_catalog` models `core_asset` identity and content hash separately.
- Component identity and its Asset mapping remain in the owner payload; the relational link does not pretend to contain a component ID.
- BLD-020 is now a valid spatial-semantics case with expected connectivity false.
- CAV-007 is explicitly a spatial-semantics case and carries both referenced passages/components in its fixture patch.
- CAV-009 and CAV-015 are measurement-semantics cases distinguishing absolute-altitude interpretation from a relative local measurement.
- CAV-003 and BLD-010 were corrected so their negative aggregate tests are not pre-empted by malformed schema payloads.
- BLD-016 uses hash-shaped expected/current definition fingerprints.
- BLD-028 and CAV-026 cover Asset ID link success with Core Asset content-hash mismatch.

### 3.3 Required runner behavior

A test runner must:

1. Deep-copy the named base fixture.
2. Apply each vector's RFC 6902 patches in order.
3. Resolve the pinned Object Type schema and run Draft 2020-12 validation for the schema layer.
4. Run the typed aggregate validator for same-owner identity, reference and domain invariants.
5. For publication vectors, build the external typed-link and Core Asset inventory, validate owner/link/Asset consistency, then exercise CAS and rollback behavior where specified.
6. For spatial-semantics vectors, evaluate supplied geometry evidence but never synthesize formal connectivity from geometric predicates.
7. For measurement-semantics vectors, distinguish relative local measurement from absolute world altitude and require a compatible vertical reference for the latter.
8. Compare the actual stable error code with the vector's `error_code`; a generic rejection is not a pass.
9. Emit per-vector evidence including vector ID, schema/descriptor version, validator build, platform, actual error code and result.

A vector marked `schema+aggregate` must pass both stages. Empty `patches` is valid when the vector supplies explicit publication, spatial or measurement context.

## 4. Typed Asset-link and persistence conclusion

P2-09-11 V0.11 defines the typed definition Asset link with owner identity, `asset_id` and `role`, with a unique owner/asset/role key. This supports multiple immutable Assets per Object. It does not store `geometry_component_id` or a duplicate content hash.

For this Building/Cave adoption profile:

- `core_object_definition_asset` must be enabled because the adopted definition schema permits formal Geometry Components that reference immutable Assets.
- `core_command_asset` remains required for formal History geometry Commands.
- `core_map_initial_object_asset` is not required by the current proposal because Map Initial State stores membership/placement and does not introduce a separate geometry Asset payload. Revisit only if that payload changes.
- Use the stable link role `geometry_component` for definition-owned formal geometry Assets.
- The owner payload maps each `geometry_component_id` to `asset_id` and expected `content_hash`. The Core Domain publication service verifies the matching typed link and resolves the Asset row to compare its authoritative content hash.
- If several components reference the same Asset, one owner/asset/role link row is sufficient; the owner payload retains each component mapping.

The global P2-09-13 inventory remains conditional for optional links across all Object Types. This adoption profile makes the definition Asset link mandatory without changing the 20-table baseline or adding a new domain table.

## 5. Descriptor registry boundary

P2-09-26 V0.2 currently contains ten Object Types and a compact descriptor shape. P2-09-41 V0.4 adds proposed `definition_schema_ref`, `asset_contract` and `child_aggregate_contract` blocks.

These additions are not part of the current registry format. P2-09-41 must remain a proposal until a versioned registry-format contract/schema, offline schema hash pinning, descriptor validation and existing ten-type regression tests are implemented and proven. Do not append the proposal to the active registry or enable Building/Cave types yet.

## 6. Persistence and History disposition

- Keep the existing top-level Object as the owner of all Building/Cave Geometry Components.
- Keep child IDs scoped to their owning Building/Cave aggregate.
- Do not add a component table, per-child tables or polymorphic owner table for V1.
- Definition Asset links belong to the current Object definition; History geometry Command Assets belong to `core_command_asset`.
- Initial State remains a time-zero membership/placement owner, not a duplicate of the shared definition.
- Child-level post-zero lifecycle Commands remain deferred.
- Project-shared current definition updates retain the accepted retroactive semantics; Map-local definitions remain owner-Map scoped.

## 7. Verification performed and not performed

Performed in this audit session:

- fetched the current GitHub source artifacts and inspected their version references;
- parsed the relevant JSON artifacts;
- checked vector ID uniqueness, fixture source resolution and patch application;
- inspected schema Draft version, Geometry Component `oneOf` role branches and child-context property compatibility;
- compared the typed Asset-link logical shape in P2-09-11 with the vector representation and owner payload contract.

Not performed:

- production JSON Schema Draft 2020-12 validation;
- Core Domain aggregate validator execution;
- SQLite migration or typed-link persistence tests;
- CAS/concurrency/failure-injection tests;
- geometry engine predicates or measurement conversion execution;
- Windows/macOS .NET test runs;
- registry compilation or application tests.

No production test is reported as passed.

## 8. Required next gates

1. Developer repository preflight must confirm the actual migration/table state for `core_object_definition_asset` and `core_command_asset`.
2. Implement the test runner contract and execute all 55 P2-09-40 V0.6 vectors.
3. Execute the existing P2-09-28 registry suite and current Core Domain geometry regression suite.
4. Verify the exact definition Asset-link role and owner/asset/role uniqueness behavior in SQLite.
5. Prove canonical payload/hash and Asset publication behavior on Windows and macOS.
6. Only after those gates, propose a versioned registry-format extension and a P2-09-26 registry V0.3. Do not activate Building/Cave descriptors before that point.

## 9. Final disposition

The typed Building/Cave aggregate approach is coherent at the design level after the corrections above. The proposal can proceed to developer repository preflight and test-runner implementation. Registry activation, production implementation and any migration beyond enabling the existing typed link remain gated by the evidence listed above.
