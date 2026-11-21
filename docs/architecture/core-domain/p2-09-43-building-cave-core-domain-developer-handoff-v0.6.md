# World View P2-09-43 — Building & Cave Core Domain Developer Handoff

- Version: 0.6
- Date: 2026-11-22
- Status: Developer handoff / implementation plan — no production implementation or test completion is implied
- Scope: Core Domain support for Building and natural Cave System Object Types, typed child aggregates, Object-owned geometry context and immutable Asset links
- Normative design inputs: P2-09-37 V0.6; P2-09-38 V0.5; P2-09-39 V0.5; P2-09-40 V0.6; P2-09-41 V0.4; P2-09-42 V0.9; P2-09-45 V0.3; P2-09-46 V0.2; P2-09-47 V0.2
- Existing authority contracts: P2-09-11 V0.11; P2-09-13 V0.7; P2-09-15 V0.5; P2-09-17 V0.5; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-26 V0.2; P2-09-27 V0.2
- Specialist product contracts: ME-04-A through ME-04-H

## 1. Purpose and delivery boundary

This handoff converts the Building/Cave adoption proposal into a sequenced development plan and a concrete verification checklist.

The development team owns product code, migration implementation if later authorized, test execution, provider proof and delivery evidence. This document does not authorize a schema migration or registry activation by itself.

**Target outcome:** Core Domain can recognize and validate Building and Cave System as two new top-level Object Types; persist their current typed definitions and child aggregates through existing owner payloads; validate child identity/reference integrity; own Geometry Components at the top-level Object; publish immutable geometry/survey Assets through existing typed links; and safely support the currently declared Object/geometry operations.

**Not in this handoff:**
- UI implementation or Figma work;
- full 3D geometry, solids, BIM/IFC or volumetric topology;
- historical creation/retirement of Level, Space, Opening, Chamber, Passage or other child records;
- RoadEndpoint or Road-to-Entrance relationship repair;
- cross-Building Subsurface Network;
- cave safety, geotechnical, flood, structural, fire, accessibility or evacuation simulation;
- speculative new persistence tables.

## 2. Normative inputs and conflict handling

The implementation must use the exact versions listed in this document. P2-09-37 through P2-09-42 are proposals and drafts; they become implementation inputs only after the project owner adopts the relevant package.

Precedence:
1. Accepted Core Domain ADRs and existing authority contracts.
2. P2-09-11/13 physical/logical persistence and migration boundaries.
3. P2-09-15/17 Command catalog and payload contract.
4. P2-09-18/19/20 Object, Geometry and Relationship contracts.
5. P2-09-37/38/39/40/41/42 Building/Cave adoption artifacts.
6. ME-04-A through ME-04-H product/domain requirements.

If a contradiction is found, stop the affected slice and report the exact clause and a minimal resolution. Do not locally reinterpret Object ownership, History, Asset authority or geometry component identity.

## 3. Implementation sequence

| Slice | Focus | Required work | Exit condition |
|---|---|---|---|
| S0 — Repository preflight | Existing Core Domain architecture | Inspect current Object registry loading, descriptor validation, definition payload serialization/hash, Initial State, Asset links, Command handlers, migrations and tests | Short preflight report; exact implementation locations and dependency versions |
| S1 — Registry format extension | Descriptor metadata | Define strict registry schema support for definition_schema_ref, asset_contract and child_aggregate_contract; embed schemas as offline application resources | Existing ten descriptors still load unchanged; malformed/unknown descriptor fields reject |
| S2 — Building/Cave descriptors | Two new Object Types | Add building and cave_system descriptors using P2-09-41; pin schema/semantics identities and declared geometry roles | Registry tests prove both descriptors and preserve all existing type semantics |
| S3 — Aggregate schema validation | Typed owner payloads | Load P2-09-38/39 schemas; validate canonical definition payloads, unknown fields, enums, role-specific component context and 2D geometry dimension | Valid base fixtures pass; schema-invalid vectors reject with stable error codes |
| S4 — Child identity/reference services | Building/Cave aggregates | Allocate opaque owner-scoped child IDs; validate uniqueness, owner identity, no-reuse, retirement and all intra-aggregate references | Duplicate, dangling, cross-owner and retired-ID reuse tests reject deterministically |
| S5 — Geometry component context | Object-owned geometry | Keep component identity in the top-level Object typed owner payload; validate role/family/dimension/coordinate context/child context and component membership | Building/Cave components round-trip; no generic polymorphic owner table or component table |
| S6 — Asset link integration | Immutable geometry/survey bytes | Enable core_object_definition_asset for current Building/Cave definition geometry; keep core_map_initial_object_asset disabled unless Initial State adds distinct geometry Assets; use core_command_asset for formal History geometry inputs; verify owner payload, typed link and core_asset hash agreement | Publication/read-back, link mismatch, failure and orphan recovery tests pass |
| S7 — Building aggregate operations | Building definition and supported geometry | Add typed definition update/CAS, Level/Space/Plan/Boundary/Access validation, existing geometry Commands for declared roles only | Stale definition rejected; child references remain coherent; no unsupported child History CRUD |
| S8 — Cave aggregate operations | Cave definition and survey | Add Cave Survey Frame, Chamber/Passage/Entrance/Station/Connection validation, uncertainty and partial survey behavior | Partial/unknown survey valid; invalid frame/endpoints/uncertainty reject |
| S9 — Initial State and Object presence | Time-zero and post-zero boundaries | Verify Map registration, time-zero membership/placement, pre-bound identity and post-zero core.object.create semantics | No time-zero Command; no post-zero create before identity binding; retry is idempotent |
| S10 — Conformance and platform proof | Full verification | Execute P2-09-40 V0.6 vectors, existing P2-09-28 registry vectors, current Object/Geometry suites and Windows/macOS provider tests | Test evidence and compatibility report delivered; no unexecuted vector claimed passed |

S0–S6 establish the descriptor, typed aggregate and immutable geometry foundation. S7 and S8 add specialist owner validation. S9 confirms formal authority routing. S10 is mandatory before production-ready status.

## 4. Persistence and migration guardrails

### 4.1 First implementation should reuse existing tables

P2-09-13 V0.7 currently defines 22 Core Domain baseline tables, two required typed links and up to three optional typed links. The Building/Cave proposal does not add a new domain table.

Use:
- core_object and existing Object ID reservation/binding;
- core_object_definition for current typed definition payload and definition hash;
- core_map_object_ref for Map registration;
- core_map_initial_object for time-zero membership/placement;
- core_object_definition_asset when current definitions reference formal geometry/survey Assets;
- core_map_initial_object_asset only when Map-specific Initial State payloads reference distinct formal Assets;
- core_command_asset for formal geometry Command inputs.

Before proposing a migration, verify whether the optional typed Asset link tables already exist in the actual code/schema. If missing, propose only the required typed link with exact FK/uniqueness and migration dependency; do not add generic asset-owner infrastructure.

### 4.2 No child tables or polymorphic geometry owner

Do not add:
- one table per Level/Space/Opening/Passage/Station;
- a generic child_entity table;
- a generic owner_type/owner_id geometry table;
- a new Geometry Component table;
- a generic JSON Patch Command.

Typed child records remain in the strict owner payload. Geometry Component metadata remains in the top-level Object owner payload. Actual immutable Asset references remain relationally verifiable through the typed link tables.

If implementation proves a concrete query/integrity requirement cannot be satisfied, document the query, constraint, scale and alternatives before proposing a table.

## 5. Command and temporal boundaries

Allowed initial behavior:
- reserve/bind Building or Cave Object identity through existing Core Domain services;
- create/update current typed definition through scope-routed definition service with expected definition hash;
- register Object to Map;
- establish Map Initial State presence/placement;
- introduce an already Bound Object into post-zero History through existing core.object.create where all descriptor preconditions pass;
- adjust/refine/add/remove declared geometry components through existing Commands only where role, child context, cardinality, Asset and reference validation pass.

Not enabled:
- post-zero create/retire/rename Level or Space as an independent temporal child operation;
- add/remove Openings or Vertical Connectors as historical child operations;
- historical cave-in, flooding, collapse, excavation or passage reconfiguration;
- generic nested aggregate patch Commands.

A current definition update is retroactive under the accepted Shared Object model. It must not simulate a time-specific construction/geological event. Future child History Commands require a separate deterministic payload/replay design and explicit adoption.

## 6. Test strategy

### 6.1 Schema and registry tests

- Load all ten existing Object Type descriptors and verify no semantic changes.
- Load Building and Cave descriptors with exact pinned schema/semantics IDs.
- Verify schemas are bundled/offline and hash-pinned.
- Reject unknown descriptor fields, unknown definition fields, unknown enums and unsupported schema versions.
- Verify role-specific geometry child_context and geometry-family combinations, plus exact P2-09-19 canonical owner metadata names and required fields.
- Verify 3D dimension and unsupported roles reject.

### 6.2 Aggregate tests

Building:
- duplicate Level/Space/Plan/Slice/Boundary/Entrance/Opening/Connection/Connector IDs;
- Level Slice references missing or foreign Level;
- Space/region/boundary child context mismatch;
- shared boundary participant count and loop consistency;
- Opening anchor reversed/out-of-range/stale;
- Access Connection endpoint missing, incompatible or self-linked;
- Vertical Connector ordered stops, same-Building Level ownership and minimum stop count;
- missing vertical datum and negative local elevation behavior.

Cave:
- duplicate Chamber/Passage/Entrance/Station/Connection IDs;
- station references missing Survey Frame or wrong geometry component;
- absolute altitude without compatible vertical reference;
- negative uncertainty;
- passage width/height unit missing or invalid;
- unresolved connection endpoint;
- crossing geometry without a connection remains disconnected;
- partial survey and unknown continuation remain valid;
- multiple entrances to one internal network remain distinct.

### 6.3 Asset and concurrency tests

- payload asset_id/content_hash equals typed Asset link;
- immutable Asset bytes verified before owner commit;
- publication succeeds but owner transaction fails: no formal reference and safe orphan reconciliation;
- stale definition hash rejects update;
- concurrent plan updates do not lose writes;
- failed component update leaves old formal state intact;
- retry with the same stable operation/submission identity does not duplicate child IDs, component membership or Object presence.

### 6.4 Authority and History tests

- Project-shared Building/Cave definition update affects all referencing Maps according to current-definition semantics.
- Map-local definition update affects only owner Map.
- Map Initial State remains time-zero baseline, not a Command.
- post-zero Object creation requires Bound identity and selected History.
- no child History mutation is accepted through an unregistered generic operation.
- geometry Commands remain selected-History-local and preserve component identity for adjust/refine.
- Field Dataset revision changes do not rewrite Building/Cave vertical frames or survey measurements.

### 6.5 Platform proof

Run the same required suite on Windows and macOS:
- canonical definition bytes/hash identical;
- strict schema validation outcomes and error codes identical;
- geometry component metadata and Asset references round-trip identically;
- child ID uniqueness and CAS behavior verified;
- cancellation, failed publication and retry behavior stable.

Record OS, architecture, .NET SDK/runtime, provider/library versions, test command, test count and result. A design vector is passed only when an automated test actually executes it.

## 7. Required developer deliverables

- Repository preflight note.
- Registry-format schema and bundled resource implementation.
- Building/Cave descriptor entries with pinned schema/semantics IDs.
- Strict aggregate validators and stable error codes.
- Child identity/reference service behavior.
- Geometry component child-context integration.
- Typed Asset link and publication integration.
- Any migration proposal, with proof of necessity and migration ordering.
- Automated tests mapping every P2-09-40 vector ID.
- Existing P2-09-28 and Core Domain regression test results.
- Windows/macOS CI evidence.
- Known limitations and deferred child History operations.
- Updated architecture/implementation documentation and Git commit references.

## 8. Definition of done and status language

- **Schema wired:** descriptors load schemas offline and strict validation is active.
- **Building aggregate implemented:** Building definitions/children/geometry/Asset links pass their automated tests.
- **Cave aggregate implemented:** Cave definitions/survey/children/geometry/Asset links pass their automated tests.
- **Core Domain integration complete:** authority, Initial State, Commands, CAS, Asset publication and regression suites pass.
- **Cross-platform verified:** required Windows and macOS suites pass with deterministic canonical payload/hash behavior.
- **Production-ready:** all applicable gates above are complete; remaining deferred capabilities are explicit and unavailable rather than silently approximated.

Do not describe P2-09-37 through P2-09-42, the 51 vectors or this handoff as implementation completion. No test is passed until it has actually run and evidence is recorded.

## 9. Deferred capabilities

- child-level historical lifecycle Commands;
- cross-Building Subsurface Network;
- RoadEndpoint identity and formal Road-linked access;
- full 3D geometry, solids and volumetric topology;
- BIM/IFC and structural engineering semantics;
- cave safety/geotechnical/flood simulation;
- production geometry/survey encoding and precision profiles beyond existing adopted ME-05 contracts.

**Handoff conclusion:** developers can begin repository preflight and schema/descriptor test scaffolding. Registry activation and production implementation remain gated by explicit adoption and the conformance work in S10.


## 10. V0.3 Geometry Component owner metadata and typed Asset links

Implement the Building/Cave geometry owner payload using the exact P2-09-19 V0.2 field names:
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

Keep the ME-04-specific frame binding as `coordinate_context_id`, separate from `spatial_reference_id`. Keep role-specific `child_context` typed and owner-scoped. All references from Boundary, Entrance, Opening, Cave Entrance, Survey Station and Level Slice records must use explicit geometry-component identity naming.

The immutable geometry Asset body follows the ME-05-04/P2-09-25 envelope and must not duplicate mutable component identity, role or child context. Opening anchors bind `geometry_component_id` and `source_content_hash`; a changed source boundary requires explicit re-resolution.

Typed Asset links follow P2-09-13 V0.7: `core_command_asset` is required for formal geometry Commands; `core_object_definition_asset` is required for Building/Cave definition geometry in this adoption profile. `core_map_initial_object_asset` is not required under the current membership/placement-only Initial State payload. Typed link rows contain `asset_id` and `role`, not component ID or content hash; resolve the hash from `core_asset` and validate the component-to-Asset mapping in the owner payload. Verify the real schema before proposing a migration.

P2-09-40 V0.6 contains 55 design-only vectors. Each vector must map to an automated test and execute before Core Domain integration can be called complete.


## 11. V0.5 Conformance runner and typed Asset-link clarification

Implement the P2-09-40 V0.6 vector contract exactly: RFC 6902 patch application; Draft 2020-12 schema validation; aggregate validation; publication/link/CAS validation; and separate spatial- and measurement-semantics checks. Empty patches are permitted when the vector supplies explicit external context.

The definition Asset link row is an owner/asset/role relation, not a component row. Use `role=geometry_component`; resolve the Asset hash from `core_asset`. The owner payload is authoritative for the mapping from `geometry_component_id` to `asset_id` and expected `content_hash`. For this adoption profile, `core_object_definition_asset` must be enabled; `core_command_asset` remains required; `core_map_initial_object_asset` is not needed by the current Initial State shape.

P2-09-44 V0.2 records the audit performed and explicitly distinguishes structural checks from production validator and platform test evidence.


## 12. V0.6 Integrated inventory and Registry Format V2 alignment

Use P2-09-13 V0.7, reconciled to P2-09-11 V0.11, as the current integrated table inventory: 22 Core Domain baseline tables, two required typed links and up to three optional typed links. The 22 baseline includes the Map-owned `core_map_field_dataset` and `core_map_field_revision`; neither is a Building/Cave persistence addition.

The Building/Cave adoption adds no Core Domain table. Enable the existing `core_object_definition_asset` typed link for current definition geometry; retain `core_command_asset` for formal geometry Commands; do not enable `core_map_initial_object_asset` while Initial State remains membership/placement only.

Registry Format V2 (P2-09-45 V0.3 / P2-09-46 V0.2 / P2-09-47 V0.2) is a compiled descriptor/schema compatibility change only. It does not create a persistence migration. The ten existing type pins remain unchanged; Building/Cave are not enabled until complete typed_v2 descriptors and their conformance gates pass.
