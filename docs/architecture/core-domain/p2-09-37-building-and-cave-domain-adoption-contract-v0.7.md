# World View P2-09-37 — Building & Cave Core Domain Adoption Contract

- Version: 0.7
- Date: 2026-11-22
- Status: Core Domain adoption proposal — typed Object/aggregate/geometry extension; not a frozen ADR, executable descriptor registry, DDL or implementation authorization; machine-readable payload schemas and validation vectors are now drafted
- Depends on: P2-09-11 V0.11; P2-09-13 V0.7; P2-09-15 V0.5; P2-09-17 V0.5; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-26 V0.2; P2-09-27 V0.2; ME-04-A through ME-04-H; ME-05-02 through ME-05-06
- Purpose: Define the minimum Core Domain changes required to adopt Building and natural Cave System without adding generic child-Object graphs, polymorphic geometry ownership or speculative persistence tables

## 1. Executive recommendation

Adopt **Building** and **Cave System** as two new allocatable top-level Core Domain Object Types, with typed nested aggregates for their subordinate domain structures.

- Building owns its current Level/BLVF, Space, Plan/Level Slice, Boundary, Entrance, Opening, Access Connection and Vertical Connector definitions.
- Cave System owns its current Cave Survey Frame, Chamber, Passage, Entrance, Survey Station and Cave Connection definitions.
- Child identities are stable and scoped to their owning Object. They are not global Core Object IDs and do not consume Object Type ID allocation.
- Formal Geometry Components remain owned by the top-level Building or Cave Object. Typed component context links each component to its child meaning (e.g. Space + Level Slice or Cave Passage).
- Immutable geometry/survey payload bytes remain Core Assets. Use existing typed Asset links; do not store payload bytes or filesystem paths inside mutable JSON.
- V1 uses existing Core Domain owner payloads and Command envelopes. **No new Core Domain table is recommended for the initial adoption.**
- Building/Cave definition and child-catalog edits use typed definition services with compare-and-swap. Map Initial State owns time-zero presence and any Map-specific initial membership/placement. Existing geometry Commands may edit already-defined geometry components only where the descriptor enables the exact role.
- V1 does not enable historical creation/retirement/restructuring of Levels, Spaces, Openings, Cave Chambers or Cave Passages. Those require later typed History Commands and deterministic replay semantics.
- Cross-Building Subsurface Network remains deferred. Do not add a generic facility graph.

This contract reconciles ME-04-H with the existing Core Domain model. It is a recommended adoption path, not permission to update the compiled registry or database schema yet.

## 2. Domain model and identity boundaries

### 2.1 Top-level Objects

Add two candidate Object Type keys:

| Type key | Semantics | Allocation | Ownership scope |
|---|---|---|---|
| building | One stable constructed Building identity | Allocatable | Existing Project-shared or Map-local |
| cave_system | One stable natural Cave System identity | Allocatable | Existing Project-shared or Map-local |

Do not create Object Types for Level, Room, Space, Entrance, Door, Stair, Cave Chamber, Cave Passage or Survey Station in V1.

Why:
- Building has independent world identity, Map presence, external references and lifecycle.
- Cave System has independent natural identity and may span Sites/Localities with multiple entrances.
- The subordinate records are structurally owned by one Building or Cave System and do not need independent Map placement or Object-level ownership.
- The composite child identity is sufficient for creator knowledge and internal connections when a typed reference contract is used.

### 2.2 Child identity

Child IDs are opaque, stable, never reused and scoped to exactly one owner Object.

| Child kind | Identity | Owner |
|---|---|---|
| Level | level_id | Building |
| Vertical frame | vertical_frame_id | Building |
| Space | space_id | Building |
| Plan / Level Slice | plan_id / slice_id | Building |
| Boundary / partition | boundary_id | Building |
| Entrance | entrance_id | Building or Cave System, never both |
| Opening | opening_id | Building |
| Access Connection | access_connection_id | Building |
| Vertical Connector | vertical_connector_id | Building |
| Cave Survey Frame | survey_frame_id | Cave System |
| Chamber | chamber_id | Cave System |
| Passage | passage_id | Cave System |
| Survey Station | station_id | Cave System |
| Cave Connection | cave_connection_id | Cave System |

Composite reference examples:
- Building child: (building_object_id, space_id)
- Cave child: (cave_object_id, passage_id)

A child ID alone is not globally resolvable. Every read/write API and external reference must include and validate its owner Object identity.

### 2.3 Identity lifecycle

- Child ID allocation is performed by the owning Core Domain aggregate service.
- IDs are not derived from names, floor labels, array indexes, geometry coordinates, Asset hashes or renderer nodes.
- Rename/reclassification/geometry refinement preserves child identity.
- Retirement preserves tombstone/reference integrity; IDs are never reused.
- Copying a Building/Cave creates new top-level Object identity and fresh child identities; immutable Asset bytes may be deduplicated independently.
- Reparenting a child to another Building/Cave is not a field update. V1 does not support child transfer; create a new child identity under the new owner with explicit provenance if needed.

## 3. Persistence strategy — no new tables in the first adoption

### 3.1 Reuse existing Core Domain owners

Use existing authoritative rows and payload boundaries:

| Existing owner | Building/Cave use |
|---|---|
| core_object | Stable Building/Cave identity, type pin, sharing scope and owner Map |
| core_object_definition | Current typed Building/Cave definition and nested child aggregate |
| core_map_object_ref | Map registration of the Building/Cave Object |
| core_map_initial_object | Map-specific time-zero membership/placement and supported initial-state facts |
| core_command | Existing History Commands for Object presence and declared geometry component operations |
| core_object_definition_asset | Immutable geometry/survey Assets referenced by current definition payload |
| core_map_initial_object_asset | Immutable Assets referenced by Map-specific Initial State payload, only where required |
| core_command_asset | Immutable geometry Assets used by formal History geometry Commands |

The existing 22 Core Domain baseline tables remain sufficient for the initial adoption; the two Map Field Dataset/Revision tables are already part of the P2-09-11 V0.11 baseline and are unrelated to Building/Cave child aggregates. The existing typed Asset links are reused. `core_object_definition_asset` is required for formal geometry Assets in the Building/Cave current definition payload; `core_map_initial_object_asset` remains unnecessary because the current Initial State payload contains only membership/placement.

No child table per Level/Space/Entrance/Passage is introduced in V1. No generic polymorphic owner table is introduced.

### 3.2 Typed payload, not arbitrary JSON

P2-09-11's definition_payload_json and initial_membership_payload_json remain storage envelopes. Their contents must be governed by:
- a versioned Object Type descriptor;
- a strict, type-specific JSON Schema or equivalent compiled validator;
- canonical serialization and deterministic hash;
- explicit unknown-field rejection;
- cross-record/domain validation in Core Domain services.

This is a typed aggregate document, not a free-form JSON dictionary. Unknown fields, unrecognized child kinds, duplicate child IDs, invalid owner references and unsupported schema versions are rejected.

The exact schema artifacts and validation vectors are subsequent deliverables under this contract.

### 3.3 Aggregate revision and concurrency

V1 keeps the existing one-current-definition model:
- one current definition row per Object;
- definition_schema_version and definition_hash pin the current typed payload;
- updates use expected definition_hash / revision guard;
- write the complete validated aggregate atomically;
- a stale update fails with a typed conflict and must not overwrite newer data.

Do not introduce a per-child revision table or general event-sourced aggregate framework. Geometry payload Assets remain immutable; aggregate metadata points to exact Asset IDs/hashes.

## 4. Building aggregate contract

### 4.1 Definition payload sections

The Building definition schema should contain bounded top-level sections:

1. identity-independent definition fields: canonical_name, building_class/use and optional description;
2. horizontal_frame: stable local frame identity, units, origin/orientation, precision profile and transform binding;
3. vertical_frame: BLVF identity, unit, up-axis, local origin and precision profile;
4. levels: stable Level records;
5. spaces: stable Space records and bounded kinds/use labels;
6. plans: plan identity/revision metadata and Level Slices;
7. boundaries: shared boundary/partition records;
8. access: Entrance, Opening, Access Connection and Vertical Connector records;
9. geometry_components: typed component metadata and child context references;
10. provenance/compatibility metadata required by the descriptor.

Do not add structural engineering, fire-code, acoustic, BIM/IFC or safety-certification fields.

### 4.2 Level and BLVF rules

- level_id is stable within Building.
- level_label, level_kind and display_order are mutable definition fields.
- elevation_local is a scalar in the owning BLVF and is never derived from display_order.
- vertical_extent_local is optional and explicit.
- BLVF identity/version, units, positive axis and precision profile are explicit.
- Optional Map vertical binding references a stable compatible Map vertical reference with transform provenance and error/quality.
- Missing Map vertical binding means world altitude Unknown, not zero.
- Map elevation Field Revision changes may stale terrain comparison evidence; they never rewrite BLVF or Level values.

### 4.3 Space and plan rules

- space_id is stable within Building.
- Room, Corridor, Lobby, Service, Void, Atrium and other bounded kinds are values of a typed Space classification; they are not Object Types.
- Each Space Level Slice references a Level owned by the same Building.
- One Space may have multiple Level Slices only when explicitly declared.
- A plan region is 2D Building-local geometry; no arbitrary Z coordinates.
- Plan completeness is explicit: complete, partial or conceptual.
- Space boundary loops reference stable Building-owned boundary component IDs and their declared orientation/order.
- Shared boundary identity is represented once when the same authored boundary serves adjacent Spaces.
- A hole/void is explicit and does not automatically create another Space.
- Adjacency is derived evidence; no traversability is inferred.

### 4.4 Access rules

- Building Entrance, Opening, Access Connection and Vertical Connector are typed Building-owned child records.
- Access Connection endpoint is a bounded union of Building Space endpoint, Building Entrance endpoint or supported exterior anchor.
- Vertical Connector references ordered Level stops and optional Space/landing endpoints.
- Opening anchors reference a stable boundary component plus source component revision/fingerprint and component-relative anchor.
- Boundary edit re-resolves anchors; ambiguous remap becomes Stale and requires review.
- Physical Opening/Connector identity is separate from current operational state.
- Access state values such as open, closed, locked, blocked, damaged and unknown are not geometry fields.
- Reachability is a derived query over explicit connections, never a persisted inferred graph.

## 5. Cave System aggregate contract

### 5.1 Definition payload sections

The Cave System definition schema should contain:
1. canonical_name, cave classification and optional description;
2. survey_frames: versioned Cave Survey Frame definitions;
3. chambers: stable Cave-owned Chamber records;
4. passages: stable Cave-owned Passage records;
5. entrances: stable Cave-owned Cave Entrance records;
6. survey_stations: stable Survey Station records;
7. connections: explicit typed cave-internal connections;
8. geometry_components: typed component metadata and child context;
9. survey completeness, uncertainty and provenance metadata.

### 5.2 Cave Survey Frame

A Survey Frame declares:
- stable survey_frame_id and schema/version identity;
- horizontal coordinate context (Map reference or Cave-local Cartesian frame);
- optional vertical reference identity and unit;
- local origin and explicit transform/binding where known;
- precision/uncertainty profile and survey method;
- provenance and compatibility identity.

A frame without a compatible absolute vertical reference may store supported relative measurements, but absolute world altitude remains Unknown. Do not assume sea level, terrain surface or entrance height.

### 5.3 Cave child records

- Chamber and Passage IDs are stable within one Cave System.
- Passage centerline and chamber plan footprint are 2D geometry components only where the declared role/family supports them.
- Survey Station coordinates/measurements must carry frame identity and uncertainty.
- Passage width/height observations are measurements, not full cross-section/volume geometry unless a future contract explicitly supports that.
- Cave Entrance is Cave-owned and may reference an explicit internal endpoint plus a surface anchor.
- A Building Entrance is a different child identity. A Cave Entrance and Building Entrance require a separately owned cross-aggregate connector to be formally linked.
- Cave connections require explicit compatible endpoint references; line crossings, overlap and nearest points are evidence only.
- Partial survey extent and unknown continuation remain explicit.

### 5.4 Natural/constructed distinction

A natural Cave System remains a Cave Object even if it has a constructed access entrance or tunnel modification. A constructed basement, bunker room or underground station interior remains a Building/Level/Space model. A constructed cross-Building tunnel network is not folded into either aggregate; it requires a separate Subsurface Network adoption decision.

## 6. Geometry Component and Asset ownership

### 6.1 Top-level owner remains the Object

P2-09-19 V0.2 remains authoritative: a Geometry Component is owned by one semantic Object and component identity is stored in that Object's typed owner payload. No new Geometry Component table is introduced.

For Building and Cave, component metadata includes:
- component_id;
- declared role;
- geometry family and dimension;
- coordinate-reference / local-frame identity;
- immutable Asset ID and content hash;
- typed child context (e.g. level_id + space_id + slice_id; boundary_id; passage_id; chamber_id; entrance_id; station_id);
- source/compatibility version as required.

The child context is validated against the same typed owner aggregate. It does not change the top-level Asset owner or create a polymorphic geometry owner.

### 6.2 Initial candidate geometry roles

Building:
- footprint: Polygon / MultiPolygon, 2D;
- space_region: Polygon / MultiPolygon, 2D;
- boundary: LineString / MultiLineString, 2D;
- partition_annotation: LineString / MultiLineString, 2D;
- entrance_anchor: Point or supported boundary-relative anchor representation, 2D.

Cave System:
- chamber_plan: Polygon / MultiPolygon, 2D;
- passage_plan: LineString / MultiLineString, 2D;
- cave_entrance_anchor: Point, 2D;
- survey_station: Point, 2D.

These are candidate role keys, not enabled registry values. No 3D/solid role is introduced. The final role catalog must be frozen in the versioned descriptor registry and covered by validation vectors.

### 6.3 Asset links

Adopt the existing typed links:
- core_object_definition_asset for immutable Assets referenced by the current Building/Cave definition payload;
- core_map_initial_object_asset only when Map-specific initial membership/placement payload references distinct formal Assets;
- core_command_asset for formal geometry Command input Assets.

The JSON payload records the typed component/child context and expected hash; the relational link records the Asset identity and owner context. Core Domain validates both agree. A path, URL or mutable filename is never a formal Asset reference.

## 7. Commands, definition updates and temporal limits

### 7.1 Supported V1 write paths

| Operation | V1 authority |
|---|---|
| Reserve/bind Building or Cave Object identity | Existing Core Domain identity reservation/binding |
| Create current Building/Cave definition | Typed Object definition service |
| Correct current Level/Space/access/survey child catalog | Typed owner aggregate definition service + CAS |
| Register Object to Map | Existing Map/Object registration service |
| Establish time-zero presence/placement | Map Initial State authoring service |
| Introduce an already Bound Building/Cave into post-zero History | Existing core.object.create only if its descriptor and complete definition/geometry preconditions are satisfied |
| Adjust/refine an existing declared geometry component in History | Existing geometry Command only where Object descriptor explicitly enables that role and component context |
| Add/remove child Space/Level/Opening/Passage in post-zero History | Not enabled in V1; requires future typed History Command |
| Historical demolition, cave-in, flooding, closure or reconstruction | Not enabled until specific deterministic History Commands are adopted |

### 7.2 Current definition semantics

A current definition update is retroactive under the accepted Shared Object model:
- Project-shared Building/Cave updates affect every Map/Branch that references that Object, including historical-time reconstruction.
- Map-local Building/Cave updates affect only the owner Map.
- Asset references are immutable; new geometry means new Asset bytes and a new aggregate reference.
- There is no replayable definition revision chain in V1.

This is appropriate for corrections to the current semantic definition or survey interpretation. It must not be used to encode a time-specific construction event or geological event.

### 7.3 Initial State semantics

- Map Initial State determines whether the Object is present at time zero and stores Map-specific initial membership/placement.
- Shared Object definition is not copied into Initial State.
- If a Map requires a different interior/cave definition, use a distinct Map-local Object or another explicitly adopted domain mechanism; do not mutate a Project-shared definition for one Map.
- A time-zero aggregate snapshot is validated against the Object's pinned descriptor schema.

### 7.4 History deferral

V1 does not add generic child-record CRUD Commands. The first implementation can support current Building/Cave definitions and time-zero setup, plus declared geometry component edits where existing geometry Commands suffice.

Future History commands must define:
- exact child identity and owner references;
- before/after aggregate invariants;
- deterministic replay semantics and descriptor compatibility;
- Branch fork-boundary behavior;
- child retirement/reference handling;
- relationship/connection effects;
- idempotent submission and undo/recovery rules.

Do not create a generic JSON Patch Command or mutable child-array update in History.

## 8. Relationship Registry boundary

No new generic Relationship Type endpoint pairs are required for the initial Building/Cave aggregate.

- Level belongs to Building through typed ownership, not structural.part_of.
- Space belongs to Building and references Level through typed child records, not spatial.contains.
- Interior adjacency is a derived spatial query, not spatial.adjacent_to.
- Access Connection is a typed Building aggregate record, not network.connects_to.
- Cave Chamber/Passage connections are Cave-owned typed records, not generic Object Relationships.
- Building Entrance to Road is blocked by ME-03-H RoadEndpoint identity gap.
- Cave Entrance to Building Entrance and cross-Building subsurface infrastructure require a separately owned typed cross-aggregate connector/network contract.

This avoids forcing nested identities into Object-to-Object relationship payloads.

## 9. Descriptor and compatibility requirements

The new Building and Cave descriptors must define:
- stable type_key and semantics_id;
- descriptor schema version;
- allocatable identity policy and Object ID namespace;
- definition schema, runtime schema and Initial State schema;
- geometry roles, family/dimension/cardinality and coordinate contexts;
- child aggregate schemas and cross-record invariants;
- allowed Commands and exact role capabilities;
- Asset media/format and typed-link requirements;
- compatibility and historical read/replay policy;
- unknown-field rejection and canonical serialization rules.

A bound Object pins its descriptor schema and semantics identity. A future descriptor version must not silently reinterpret existing Building/Cave payloads or historical Commands.

## 10. Migration and table impact

The initial adoption should not add a Core Domain table.

Required changes are:
1. Extend the compiled Object Type descriptor registry with Building and Cave System entries.
2. Extend the type-specific typed definition/Initial State payload schemas.
3. Enable existing typed Asset links required by the adopted formal geometry payloads.
4. Update Core Domain aggregate validation and identity/reference services.
5. Extend checkpoint reconstruction fingerprints to include the new descriptor semantics and all reachable current definition/Asset hashes; the existing fingerprint model already has this responsibility.
6. Add versioned validation vectors and provider/asset publication tests.

P2-09-13 V0.7's baseline table count and migration graph remain unchanged unless later implementation proof demonstrates a concrete normalized table is necessary. Any proposed new table must state the query/integrity requirement that cannot be met by the existing typed aggregate payload and link model.

## 11. Implementation sequence

| Slice | Scope | Gate |
|---|---|---|
| CD-BUILD-01 | Building/Cave Object descriptor proposals and compatibility pins | Descriptor schemas and catalog vectors validate |
| CD-BUILD-02 | Typed Building definition aggregate: frame, Levels, Spaces, plans and child IDs | Owner/child reference and CAS tests pass |
| CD-BUILD-03 | Building geometry roles, Asset links and shared boundaries | Asset read-back, component ownership and boundary consistency tests pass |
| CD-BUILD-04 | Building access aggregate: Entrance, Opening, Access Connection, Vertical Connector | Endpoint/anchor/state validation tests pass |
| CD-CAVE-01 | Cave Object descriptor and Survey Frame | Frame/uncertainty/owner tests pass |
| CD-CAVE-02 | Cave child aggregate and geometry roles | Chamber/Passage/Entrance/Station/Connection vectors pass |
| CD-CAVE-03 | Cave survey Asset publication and partial-survey validation | Hash/provenance/partial coverage tests pass |
| CD-HISTORY-01 | Future historical child operations | Separate Command design, replay proof and explicit adoption required |
| CD-NETWORK-01 | Future Subsurface Network | Only after concrete cross-Building product need and endpoint design |

Building and Cave may share validation infrastructure but must not be collapsed into one polymorphic aggregate schema.

## 12. Required validation and proof package

The next machine-readable artifacts should include:
- Building descriptor schema and valid/invalid definition vectors;
- Cave System descriptor schema and valid/invalid definition vectors;
- Level/BLVF reference and unit vectors;
- Space/Level Slice/plan/boundary vectors;
- shared boundary and Opening anchor remapping vectors;
- Access Connection endpoint and Vertical Connector route vectors;
- Cave Survey Frame, uncertainty, partial coverage and connection vectors;
- cross-owner/cross-Map reference rejection vectors;
- Asset hash/link mismatch, failed publication, CAS conflict and idempotent retry vectors;
- Windows/macOS canonical payload and geometry validation evidence.

Tests must include the existing Object Type and geometry conformance suites to ensure no regression to the ten existing types.

## 13. Risks and explicit non-goals

- Do not add one table per child type in V1 without a proven query/integrity need.
- Do not use a generic polymorphic owner_type/owner_id table.
- Do not turn every Room, Door, Stair, Cave Chamber or Passage into a top-level Object.
- Do not persist formal Asset references only as file paths or arbitrary JSON strings.
- Do not infer relationships, access or traversability from geometry.
- Do not use current definition edits for historical construction/geological changes.
- Do not claim 3D solids, cave volume, structural engineering, fire/accessibility compliance, evacuation or geotechnical safety.
- Do not adopt a cross-Building Subsurface Network in the initial Building/Cave package.

## 14. Final recommendation

Adopt the typed aggregate direction as the minimum Core Domain extension for Building and Cave.

Keep the existing Object/History/Asset authority architecture intact: two new top-level Object Types, typed nested child aggregates in existing owner payloads, Object-owned Geometry Components with child context, existing immutable Asset links, and no new domain tables in the first adoption.

The machine-readable schemas, descriptor extension and validation vectors are now drafted. P2-09-42 V0.7 records the required reconciliation with the canonical P2-09-19 Geometry Component owner metadata and ME-05-04/P2-09-25 spatial-reference envelope. The next gate is executing the 55-vector suite and existing registry regression suite; no registry activation or DDL is implied.


## 15. V0.5 Geometry Component owner-payload alignment

The Building/Cave schemas use the canonical P2-09-19 V0.2 Geometry Component field vocabulary:
- `geometry_component_id`;
- `geometry_role`;
- `geometry_family`; `dimension`; `spatial_reference_id`.
- `geometry_schema_version`.
- `asset_id`; `content_hash`.
- `provenance`; `validity_metadata`.

The specialist-specific `coordinate_context_id` remains a distinct Building-local horizontal frame or Cave Survey Frame reference; it does not replace `spatial_reference_id`. The role-specific `child_context` remains typed metadata inside the top-level Object owner payload.

All references to a formal geometry component in Boundary, Entrance, Opening, Cave Entrance, Survey Station and Level Slice records use explicit geometry-component naming. Opening anchors bind `geometry_component_id` and `source_content_hash`, so boundary Asset replacement makes the anchor stale until explicitly remapped.

The immutable geometry Asset follows the ME-05-04/P2-09-25 envelope. Mutable Object/Component identity, role and child context are not duplicated into the Asset bytes. P2-09-40 V0.6 contains 55 design-only validation vectors; none has been executed by a production validator.


## 16. V0.6 Asset-link and conformance-audit alignment

P2-09-44 V0.1 records the focused conformance audit. For the Building/Cave adoption profile, `core_object_definition_asset` is required because formal Geometry Component Assets are referenced by the current definition payload. `core_command_asset` remains required for History geometry Commands. `core_map_initial_object_asset` remains unnecessary under the current Initial State payload, which contains membership/placement rather than distinct geometry Assets.

The typed link row carries owner identity, `asset_id` and `role`; it does not carry `geometry_component_id` or a duplicate content hash. The owner payload retains the component-to-Asset mapping and expected hash. The publication service verifies the typed link and resolves the authoritative hash from `core_asset`.

P2-09-40 V0.6 contains 55 design-only vectors with explicit schema, aggregate, publication, spatial-semantics and measurement-semantics layers. These vectors have not been executed by a production validator or Core Domain test runner.


## 17. V0.7 — Integrated persistence inventory reconciliation

P2-09-13 V0.7 now reconciles to P2-09-11 V0.11's 22 Core Domain baseline tables, including `core_map_field_dataset` and `core_map_field_revision`. These two Map-owned Field Baseline tables are not Building/Cave tables and do not change the no-new-Geometry-Component-table decision.

The Building/Cave adoption still reuses the existing typed Asset-link design: `core_object_definition_asset` is required for formal Geometry Component Assets in the current definition payload; `core_command_asset` remains required for formal geometry Commands; `core_map_initial_object_asset` is not required by the current membership/placement-only Initial State payload. No new domain table is proposed.
