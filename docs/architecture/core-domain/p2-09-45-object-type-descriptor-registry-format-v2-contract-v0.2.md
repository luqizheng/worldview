# World View P2-09-45 — Object Type Descriptor Registry Format V2 Contract

- Version: 0.2
- Date: 2026-11-22
- Status: Proposed registry-format contract; not an activated registry, frozen ADR, DDL or implementation authorization
- Depends on: P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-26 V0.2; P2-09-27 V0.2; P2-09-28 V0.2; P2-09-41 V0.4; P2-09-42 V0.7

## 1. Decision summary

Adopt a versioned Registry Format V2 that supports two explicitly tagged descriptor profiles:

1. `legacy_compact_v1`: a compatibility profile for the ten existing P2-09-26 V0.2 descriptors. It preserves their current schema version, semantics ID, field catalog, geometry declarations, policies and Command capability data. It is not available to newly introduced Object Types.
2. `typed_v2`: the complete Object Type descriptor profile required by P2-09-18. It explicitly declares human metadata, identity/allocation contract, definition/runtime/Initial State schema references, geometry, Relationship participation, lifecycle, Command capabilities, Asset contract, child aggregate contract and compatibility behavior.

The format version and registry content version are separate:
- `format_version = 2` means the document follows this structural contract.
- `registry_version` identifies the published catalog revision.
- `descriptor_schema_version` versions the descriptor structure.
- `schema_version` remains the Object Type definition payload schema version pinned by `core_object`.
- `semantics_id` remains the deterministic interpretation compatibility identity.
- Project-configurable display/allocation format version remains separate and does not change domain semantics.

This separation is mandatory. Do not repurpose the existing `schema_version` field to mean registry or descriptor structure version.

## 2. Why a dual-profile migration is necessary

P2-09-26 V0.2 contains ten descriptors with a compact, stable shape. P2-09-18 V0.2 requires more executable domain metadata than those entries currently carry. Building/Cave additionally require strict nested definition schemas, explicit child identity contracts and typed Asset-link behavior.

A wholesale rewrite of the ten entries would create an unnecessary semantic migration. Treating their compact fields as if they were complete typed schemas would silently invent rules. V2 therefore preserves the ten existing entries as an explicitly tagged legacy profile while requiring every new type to use `typed_v2`.

The V2 migration preview is P2-09-26 V0.3 Proposal. It changes only the registry envelope/profile tags and compatibility metadata for the existing ten types. It does not add Building or Cave System, and it does not activate a new registry.

## 3. Registry envelope

The V2 registry retains the existing `world-view-domain-descriptor-registry` format identifier and uses:
- `format_version: 2`;
- incremented `registry_version`;
- `schema_artifacts`: offline schema artifact inventory;
- `object_types`: keyed descriptor map;
- `compatibility_policy`: explicit legacy allowlist and pins, typed-profile minimum, migration policy and compiled-only runtime resolution.

Unknown fields are rejected at the envelope and descriptor levels. The JSON Schema is P2-09-46 V0.2.

### 3.1 Schema artifact inventory

Each schema artifact declares:
- stable `artifact_id`;
- repository-relative `path`;
- artifact `schema_version`;
- lowercase SHA-256 of the exact UTF-8 file bytes;
- JSON Schema dialect (Draft 2020-12 for V1 typed payload schemas).

Every typed descriptor schema reference repeats the artifact ID, schema version and SHA-256. The registry compiler must resolve exactly one artifact and require all three values to match. This deliberate duplication makes the descriptor's schema pin inspectable without trusting a mutable path.

Paths are build-time repository/bundle references only. Reject absolute paths, URL schemes, parent traversal, missing artifacts, duplicate artifact IDs and hash mismatches. The compiled application embeds the resolved schema bytes; runtime never downloads or dynamically resolves a schema.

The registry JSON Schema checks structure. The registry compiler checks artifact existence, exact bytes and cross-record references.

## 4. Descriptor profile contracts

### 4.1 legacy_compact_v1

Allowed only for the ten existing keys:
- `landmass`;
- `mountain_range`;
- `mountain_peak`;
- `river`;
- `lake`;
- `terrain_region`;
- `settlement`;
- `road`;
- `political_region`;
- `point_of_interest`.

The V2 compatibility policy pins each key's current `schema_version` and `semantics_id`. The compiler rejects:
- a legacy descriptor key outside the allowlist;
- a changed schema version or semantics ID without an explicit registered migration;
- missing/unknown descriptor profile;
- unknown fields;
- unsupported Command references.

No legacy adapter may infer missing definition/runtime/Initial State schema rules, relationship participation, Asset semantics or child aggregate rules from display names or arbitrary free text. It exposes only the capabilities explicitly declared by the legacy descriptor and its known compatibility adapter.

### 4.2 typed_v2

A typed descriptor requires the following blocks.

| Block | Required purpose |
|---|---|
| `metadata` | Default display name, description, category and localization key; presentation changes do not change semantics |
| `identity` | Allocatable flag, Project + Object Type allocation namespace and existing reservation lifecycle policy |
| `schemas` | Separate definition, runtime and Initial State schema references, each pinned by artifact ID, schema version and SHA-256 |
| `geometry` | Enabled flag and role-specific family, dimension and min/max cardinality |
| `relationship_participation` | Explicit incoming/outgoing Relationship Type IDs; empty arrays mean no participation |
| `lifecycle` | Existing Core Object identity lifecycle and exact History presence Command references |
| `command_capabilities` | Structured policies referencing registered Commands only |
| `definition_edit_policy` | Shared versus Map-local definition service routing and retroactive-current-definition behavior |
| `initial_state_policy` | Time-zero baseline service, allowed facts and no post-zero Command confusion |
| `runtime_attribute_change_policy` | Explicit mutable fields and selected-History scope; empty/disabled is explicit |
| `asset_contract` | Immutable payload ownership, typed Asset-link tables/roles, SHA-256 and relational/hash verification |
| `child_aggregate_contract` | Explicit enabled/disabled declaration; when enabled, child sections, owner namespace, no-reuse, copy, cross-reference and retirement rules |
| `descriptor_schema_version` | Version of the typed descriptor structure, independent of the definition payload schema version |
| `schema_version` | Definition payload schema version persisted on the bound Object |
| `semantics_id` | Deterministic domain interpretation identity pinned on the bound Object |

A typed descriptor must not omit a schema reference because a schema is empty. An empty runtime contract uses a real strict empty-object schema artifact. Initial State uses a real type-specific or explicitly shared Initial State schema artifact. Empty means explicitly no fields; it does not mean an absent validation contract.

V1 formal geometry is dimension 2 only. Geometry role family/cardinality is checked against the Core Geometry contract and registered geometry Commands.

### 4.3 Building/Cave readiness

P2-09-41 V0.4 is not yet a `typed_v2` descriptor. It has a definition schema reference and proposed Asset/child blocks, but it does not yet carry the complete V2 metadata, identity, runtime schema reference, Initial State schema reference, Relationship participation, lifecycle and descriptor compatibility fields.

Do not wrap it with `descriptor_profile: typed_v2` until all required fields and cross-registry references are supplied. The existing Building/Cave definition schemas can be reused after their conformance suite is executed; the missing shared empty-runtime and appropriate Initial State schema artifacts must be specified and pinned first.

## 5. Cross-registry compiler validation

JSON Schema validation is necessary but insufficient. The Core Domain registry compiler must additionally verify:

1. The `object_types` map key equals descriptor `type_key`; keys are unique and stable.
2. Legacy profile keys and their schema/semantics pins match the explicit allowlist.
3. Every schema reference resolves to exactly one artifact with matching ID/version/hash; every artifact path is safe and present.
4. Every Command capability and lifecycle Command reference exists in P2-09-15/P2-09-17's registered Command catalog and its policy is compatible with the Command contract.
5. Every Relationship participation ID exists in P2-09-27 and permits the declared endpoint direction/type.
6. Geometry role keys, families, dimensions and cardinalities are supported; finite max must be >= min.
7. Enabled geometry Commands have compatible roles and cannot bypass minimum/maximum cardinality or same-owner child-context validation.
8. Asset link table/role requirements match P2-09-11/P2-09-13 and the actual payload ownership. Component-to-Asset mapping stays in the typed owner payload; the typed link row does not acquire a component ID or duplicate hash.
9. Child aggregate references remain inside one owning Object; child IDs never enter the Object allocator or `core_object`.
10. Definition, Initial State and runtime policies do not cross their authority/time boundaries.
11. Every descriptor schema/semantics pin required by `core_object` and Checkpoint fingerprints has a retained compatible reader/handler or explicit migration.
12. All unknown keys, unsupported versions, missing references and ambiguous mappings fail closed with stable error codes.

The compiler emits a deterministic compiled descriptor bundle. No runtime reflection-based plugin loading, network schema fetch, dynamic executable Object Type or user-authored handler is introduced.

## 6. Compatibility and migration rules

- The ten existing descriptors retain their current `schema_version` and `semantics_id`; profile tagging alone is not a semantic change.
- Display metadata may change without a semantics ID change.
- A compatible payload schema change increments the definition `schema_version` and requires an explicit migration/reader path.
- A deterministic meaning change (field meaning, geometry interpretation, Relationship behavior, validation or replay effect) requires a new `semantics_id`.
- Old Object identities continue using their pinned schema/semantics compatibility. No silent upgrade to the newest descriptor is allowed.
- A legacy-to-typed migration is per Object Type and requires a proven field/schema mapping, canonical payload migration, immutable Asset-link verification and preservation of historical interpretation. It is not required merely to adopt Registry Format V2.
- Registry format migration itself does not alter Project rows, Object IDs, reservations, Commands, History, Shared Object definitions or Checkpoints. The compiled registry must continue resolving every persisted pin.

## 7. Build and runtime sequence

Build-time:
1. Validate the registry envelope and each descriptor against P2-09-46.
2. Resolve all schema artifacts from the checked-in offline bundle.
3. Verify exact SHA-256 and schema versions.
4. Run cross-registry Command and Relationship reference checks.
5. Run geometry, Asset, child aggregate, identity and compatibility checks.
6. Run all P2-09-47 vectors, the existing P2-09-28 suite and the existing ten-type compatibility regression suite.
7. Produce a deterministic embedded descriptor/schema bundle and record its registry version/hash and compiler version.

Runtime:
- Core Domain loads only the compiled bundle.
- The Project's `core_object_type` rows remain identity/display/allocation configuration, not executable schema authority.
- Bound Object pins continue to use `object_type_schema_version` and `object_type_semantics_id`.
- Unknown or unsupported pins fail closed with a compatibility diagnostic.

## 8. Verification and acceptance gates

The following are mandatory before the proposed Registry V0.3 can replace the V0.2 catalog:

- [ ] P2-09-46 passes a real JSON Schema Draft 2020-12 meta-schema validator.
- [ ] P2-09-47 vectors pass a production registry schema/compile runner with exact stable error codes.
- [ ] The ten existing descriptors pass unchanged semantic regression tests under `legacy_compact_v1`.
- [ ] P2-09-28 V0.2 Object/Relationship/cross-cutting vectors pass.
- [ ] Command references reconcile with P2-09-15 and P2-09-17.
- [ ] Relationship participation reconciles with P2-09-27.
- [ ] Schema artifact bundle is offline, complete, hash-pinned and deterministic on Windows/macOS.
- [ ] Building/Cave descriptors supply every typed_v2 block and pass P2-09-40 after it is executed.
- [ ] No persistence migration or new domain table is introduced by registry format alone.
- [ ] Existing bound Object schema/semantics pins remain resolvable.

No production validator, registry compiler, application build or cross-platform test is claimed as executed by this design task.

## 9. Artifacts

- P2-09-45 V0.1 — this Registry Format V2 contract.
- P2-09-46 V0.1 — strict JSON Schema Draft 2020-12.
- P2-09-47 V0.2 — 34 design-only conformance vectors.
- P2-09-26 V0.3 Proposal — migration preview for the existing ten legacy descriptors only; Building/Cave are deliberately not included.

The next task is to make the V2 contract executable in the developer test harness and complete the missing typed_v2 schema artifacts before revising the Building/Cave extension. Do not activate the registry or authorize implementation from these design artifacts alone.


## 10. V0.2 schema tightening

The V0.2 registry JSON Schema closes the `common_rules` object with an explicit property inventory and rejects parent-directory traversal in schema artifact paths at schema-validation time. Duplicate artifact IDs, descriptor-to-artifact hash agreement, artifact byte hashes and command/relationship references remain cross-record/compiler checks. P2-09-47 V0.2 points to this tightened schema; its 34 cases remain design-only and unexecuted.
