# World View P2-09-11 — Core Domain Relational Physical Schema Design V0.10

- Version: 0.10
- Date: 2026-11-21
- Status: **Object Type / Geometry Component reconciled physical-schema proposal — recommended relational ownership, key strategy and constraint intent; not executable DDL or implementation authorization**
- Supersedes: P2-09-11 V0.9 for relationship descriptor/fingerprint integration; V0.9 is retained as history
- Depends on: P2-09-01~10; P2-09-03 V0.2; P2-09-04 V0.2; P2-09-05 V0.3; P2-09-13 V0.4; P2-09-16 V0.1; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.1; P2-09-15 V0.4; P2-09-17 V0.4; ADR-001/002/003 Amendment V1.3/005; P2-08 V0.3; AI Assistant Logical Schema V1.16

## Change summary from V0.9

1. Incorporate the Relationship Descriptor and Temporal Contract without adding a standalone relationship table.
2. Clarify that baseline, Shared Object definition and History-local relationship facts remain in their owning typed payloads.
3. Extend Checkpoint fingerprints with relationship type schema/semantics identifiers and the relationship-affecting portions of Initial State/Command payloads.
4. Confirm `core_command_object_ref` source/target roles cover relationship establishment/termination; the required typed-link inventory remains unchanged.
5. Preserve the 20 Core baseline tables + 2 required typed links + 0–3 optional typed links; no new domain table is introduced.

## Change summary from V0.8

1. Pin Object Type descriptor schema and execution-semantics identity on each bound Object identity, preventing silent reinterpretation after application upgrades.
2. Separate Object Type stable identity and Project-configurable allocation/display format version from executable descriptor semantics.
3. Record Geometry Component identity/role metadata in existing typed owner payloads and immutable Asset references; no new Geometry Component table is introduced.
4. Require `core_command_object_ref` for the enabled V1 Object/Geometry/Relationship Command descriptors and `core_command_asset` for formal geometry Command inputs.
5. Expand Checkpoint reconstruction fingerprints to include the pinned Object Type semantics that affect Object/Command interpretation.
6. Retain the 20 Core Domain baseline tables, require 2 descriptor-driven typed links (`core_command_object_ref`, `core_command_asset`), and leave up to 3 additional typed links conditional; no new domain table is added.

## Change summary from V0.7

1. Preserve `core_object.sharing_scope` and add nullable `owner_map_id` so Map-local Object ownership is enforceable while Project-shared Objects remain reusable across Maps.
2. Specify SQLite CHECK/FK/trigger and Domain Service responsibilities for Object-to-Map registration, including composite-FK NULL behavior.
3. Expand Checkpoint reconstruction-input fingerprint requirements and stale-publication protection in alignment with P2-09-16.
4. Reconcile AI integration to the exact 28-table V1.16 inventory; `submission_command_refs` remains the existing opaque Command-receipt evidence table, not a new table.
5. Reorder migration dependencies so Map identity exists before `core_object.owner_map_id` can reference it.
6. Keep Calendar Configuration overlap/gap behavior and provider-specific DDL validation as explicit open gates.

## 1. Purpose and boundaries

V0.7 converts the earlier table-group proposal into a more complete relational design and resolves several avoidable ambiguities. It is still a design artifact: no SQL migration or implementation is authorized by this document.

The target remains one local Project database using SQLite, with Core Domain and AI Assistant as separate logical owners. Core Domain alone owns formal map and history facts. AI Assistant owns task execution and immutable proposal/authorization/submission evidence. The application coordinates short transactions and migration order but owns neither domain's facts.

Non-goals: multi-user concurrency, multiple application instances, distributed transactions, event-sourcing infrastructure, generic workflow engines, a second shared-object history, Object/Branch/Command revision chains, or persisted World State authority.

## 2. Physical design decisions recommended by V0.7

1. Use stable UUID text primary keys (`TEXT`, canonical lowercase hyphenated UUID) for Core Domain entities and AI-owned entities. Do not use display labels as keys.
2. Keep `project_id` on Project-owned records even though one database normally contains one Project. Use composite uniqueness/FKs where needed to prevent accidental cross-Project references and to keep the schema portable to a future project-container migration.
3. Distinguish the stable internal Object identity key from the Object Type-assigned ID value. `object_identity_id` is the relational key; `allocated_id_value` is the permanent value allocated within an Object Type namespace. The UI may display the allocated value. This avoids using mutable display formatting as a relational key. The domain/API must expose both consistently.
4. Use Project + stable Object Type identity as the allocation namespace in V1. A Project is the persistence unit; different Projects may independently allocate the same Object Type-local value. Map and Branch are not allocation namespaces. This is the recommended interpretation of the confirmed rule “Object IDs are allocated by Object Type”, not a claim that the user separately specified global namespace scope.
5. Store canonical Historical Time as two SQLite `INTEGER` columns: `day_number` and `offset_seconds`, with `0 <= offset_seconds < 86400`. Do not collapse to a scalar or floating point. Use the same pair for Branch fork boundaries, Command/Event times and State Change effective times. Declared precision is stored separately.
6. Persist Commands as authoring authority. Events are narrative records. State Changes are generated in memory from Commands in V1; a persisted materialization is deferred unless later profiling proves it necessary. World State is never persisted as authority.
7. Store one current Shared Object definition. Its edits are project-level authoring outside Historical Time and are retroactive for reconstruction: all Map/Branch reconstruction uses the current definition. No definition version chain or replay stream is added.
8. Map object registration/reference and time-zero membership are separate relations. A Project-shared Object may be referenced by multiple Maps without being present in any Map's Initial State; a Map-local Object is owned by exactly one Map and may be registered only there.
9. Use typed asset-link tables for known owner types rather than an unrestricted `(owner_type, owner_id)` polymorphic FK. Add a new typed link only when a real domain payload requires it.
10. Keep one migration ledger with `module_id` namespace. Core Domain and AI Assistant author their own immutable migrations; Application Bootstrap runs them in an explicit dependency order.

## 3. Key and data conventions

### 3.1 Entity keys

- Primary entity keys: UUID text.
- Foreign keys: same UUID text representation; enable `PRAGMA foreign_keys=ON` on every connection and verify it.
- Add `UNIQUE(project_id, entity_id)` to Project-owned parent tables when a composite FK is needed to enforce same-Project ownership.
- Avoid `ON DELETE CASCADE` for formal identity/history/evidence. Use `RESTRICT`/`NO ACTION`; lifecycle and deletion rules belong to Domain Services.
- IDs are generated once and remain stable. Editing a Command does not create a new Command identity or revision entity.

### 3.2 Transaction timestamps

Operational timestamps (`created_at_utc_ms`, `updated_at_utc_ms`, `submitted_at_utc_ms`, etc.) are UTC Unix milliseconds stored as SQLite `INTEGER`. They are never used as Historical Time or semantic ordering tie-breakers.

### 3.3 Historical Time

Use `day_number INTEGER NOT NULL CHECK(day_number >= 0)` and `offset_seconds INTEGER NOT NULL CHECK(offset_seconds BETWEEN 0 AND 86399)`. Lexicographic comparison is `(day_number, offset_seconds)`. Day zero is Map-relative. Branches inherit the exact root Map time axis.

Represent every author-entered time precision as a half-open internal interval `[start,end)`. Persist the lower boundary in the existing `effective_day_number/effective_offset_seconds` pair and the exclusive upper boundary in `precision_range_end_day_number/precision_range_end_offset_seconds`. For second precision the interval covers exactly one second; for minute/day/year precision it covers the full represented period. Require both upper-bound columns and enforce `end > start`. The interval columns, not a representative timestamp or precision label, are the ordering authority. `time_calendar_id` preserves interpretation provenance; once normalized, the internal interval is authoritative and must not be recomputed after a Calendar display change. `time_input_json` is optional original-input/display evidence only, not a substitute for normalized interval columns. This follows D01-21/22 and D01-37/38/39/46, which remain Proposed / Discussion rather than frozen physical contracts.

### 3.4 Payloads

- Small typed payloads may be JSON text, with explicit schema/descriptor version and canonical serialization.
- Large geometry/raster/binary content is immutable files in the Project Asset Store.
- Relational identity, ownership, uniqueness and FK integrity must not be hidden inside JSON.
- Hash algorithm and canonical JSON algorithm are pinned by a later package-level contract; SHA-256 is the recommended asset integrity hash.

## 4. Core Domain tables

The names below are proposed stable SQL names. Final column nullability and check syntax must be verified against the selected SQLite/provider version.

### 4.1 Project, Map, Calendar and Initial State

#### `core_project`

- PK: `project_id TEXT`.
- Fields: `project_name`, `project_format_version`, `created_at_utc_ms`, `updated_at_utc_ms`.
- One logical row per Project database in V1; application identity check rejects a second unrelated Project in the same file.

#### `core_map`

- PK: `map_id TEXT`; FK `project_id -> core_project`.
- Fields: `project_id`, `map_name`, `lifecycle_status`, `created_at_utc_ms`, `updated_at_utc_ms`.
- Unique `(project_id, map_id)`.
- Exactly one root History, one default Calendar and its day-zero Calendar Configuration are created in the same Core Domain creation transaction. The root History is resolved through `core_history.owner_map_id` (unique); the default Calendar is resolved through the unique `core_calendar` row with `calendar_role='default'`. Integrity checks reject a Map missing any of these.
- A Map may also have auxiliary Calendars. Branches inherit the Map's time axis and Calendar context through the History path; they do not create a separate Map or time axis.

#### `core_calendar`

- PK: `calendar_id TEXT`; composite FK `(project_id,map_id) -> core_map`; unique `(project_id,calendar_id)` and `(project_id,map_id,calendar_id)`.
- Fields: `project_id`, `map_id`, `calendar_role` (`default`/`auxiliary`), `calendar_type`, `calendar_name`, `created_at_utc_ms`, `updated_at_utc_ms`.
- Partial unique index `(project_id,map_id) WHERE calendar_role='default'` permits exactly one default Calendar per Map; Core Domain creation/integrity checks ensure at least one.
- Auxiliary Calendars are display alternatives and do not alter internal Historical Time or Command order.

#### `core_map_initial_state`

- PK: `initial_state_id TEXT`; unique FK `(project_id,map_id) -> core_map`; unique `(project_id,map_id,initial_state_id)` for child composite FKs.
- Fields: `project_id`, `map_id`, `baseline_schema_version`, `content_fingerprint`, `created_at_utc_ms`, `updated_at_utc_ms`.
- One logical mutable time-zero baseline per Map. It is not a Command, Event or post-zero State Change.
- Baseline edits occur only through dedicated Core Domain baseline-authoring services and invalidate affected Checkpoints.

#### `core_map_object_ref`

- PK: `map_object_ref_id TEXT`.
- FKs: `(project_id,map_id) -> core_map`; `(project_id,object_identity_id) -> core_object`.
- Fields: `project_id`, `map_id`, `object_identity_id`, `created_at_utc_ms`. Do not duplicate `sharing_scope` or `owner_map_id` on this link.
- Unique `(project_id,map_id,object_identity_id)`.
- Insert/update validation must enforce: a `project_shared` Object may be registered by any Map in the same Project; a `map_local` Object may be registered only when `map_id = core_object.owner_map_id`.
- Enforce this invariant in the Core Domain Map/Object registration service and add SQLite trigger defense-in-depth for INSERT and UPDATE. The trigger must reject cross-Map registration of a Map-local Object. Keep the separate Object FK so existence is always checked; do not depend on a nullable composite FK to encode the shared/local rule.
- Represents a Map's explicit reference/registration to an Object. It does not imply that the Object is present in time-zero Initial State.

#### `core_map_initial_object`

- PK: composite `(initial_state_id, object_identity_id)`.
- Composite FKs `(project_id,map_id,initial_state_id) -> core_map_initial_state`, `(project_id,map_id,object_identity_id) -> core_map_object_ref`, and `(project_id,object_identity_id) -> core_object`.
- Fields: `project_id`, `map_id`, `initial_state_id`, `object_identity_id`, `initial_membership_payload_json`, optional initial placement/relationship payload and schema version.
- Only Objects present in the Map's time-zero state appear here. Must also have a `core_map_object_ref` for that Map; this cross-table invariant is enforced by Core Domain service and verified by integrity tests.
- Contains Map-specific initial-state facts, not copies of the current shared definition.

### 4.2 Object identity, current definition and ID reservation

#### `core_object_type`

- PK: `object_type_id TEXT`; FK `project_id`; unique `(project_id,object_type_id)`.
- Fields: `project_id`, stable `type_key`, mutable display name/code, allocation-format definition, `allocation_format_version`, allocatable flag, timestamps.
- The row stores Project configuration and stable Object Type identity only. Executable Object Type descriptor schema/semantics are supplied by the compiled Core Domain registry; this row is not a user-authored executable schema.
- Unique `(project_id, type_key)`; display name/code is not the stable key.

#### `core_object`

- PK: `object_identity_id TEXT`; FK `(project_id,object_type_id) -> core_object_type`; FK `(project_id,owner_map_id) -> core_map`; unique `(project_id,object_identity_id)` and `(project_id,object_type_id,object_identity_id)`.
- Fields: `project_id`, `object_type_id`, `object_type_schema_version`, `object_type_semantics_id`, `sharing_scope` (`project_shared`/`map_local`), nullable `owner_map_id`, `lifecycle_status` (`draft`,`active`,`abandoned`,`retired`), `created_at_utc_ms`, `updated_at_utc_ms`.
- `object_type_schema_version` and `object_type_semantics_id` are pinned when the reservation becomes Bound. They identify the exact Object Type contract used to create/interpret this identity; display/allocation format version is not a substitute.
- Keep `sharing_scope` as the authoritative field name. Enforce the paired rule with a CHECK: `project_shared` requires `owner_map_id IS NULL`; `map_local` requires `owner_map_id IS NOT NULL`.
- `owner_map_id` is a same-Project Map FK. SQLite composite-FK semantics skip enforcement when any child-key component is NULL; that is intentional for `project_shared`, while the CHECK makes `map_local` non-null so its FK is enforced. Do not rely on the composite FK alone to enforce the scope rule.
- `project_shared` Objects have Project ownership and may be referenced by any Map in the same Project. `map_local` Objects have exactly one owner Map and may be registered only in that Map.
- `object_identity_id` is an internal stable storage key. The Object Type-assigned `allocated_id_value` remains the permanent domain-facing Object ID stored on its reservation and exposed by Core Domain. This is a relational key strategy, not a second user-facing identity or an Object Version.
- A `draft` identity row is created by Core Domain only when a reservation becomes Bound. It is not yet an inserted world Object and cannot appear in Map membership or Initial State. On insertion it becomes `active`; if the bound reservation is abandoned it remains an `abandoned` tombstone. Retired/abandoned identity rows are not physically deleted while reservation evidence is retained.
- Core Domain verifies that the pinned Object Type schema/semantics are registered and supported before binding or reconstruction. Existing identities are not silently upgraded to the latest descriptor.
- Contains stable semantic identity and its Object Type compatibility pin only. No mutable runtime state, Object Version or Branch state.

#### `core_object_definition`

- PK/FK `(project_id,object_identity_id) -> core_object`.
- Fields: `project_id`, `object_identity_id`, `definition_schema_version`, `definition_payload_json`, `definition_hash`, `updated_at_utc_ms`.
- At most one current definition per Object identity; an active Object must have exactly one, while a draft/abandoned identity may not yet have one. Definition payload schema and allowed fields are validated against the Object's pinned Object Type descriptor. For `project_shared` Objects this is the common Project-level definition used by every referencing Map; for `map_local` Objects it is available only to the owner Map.
- Definition edits update the current row; no replayable definition history is retained in V1. A Project-shared definition edit invalidates reconstruction caches/checkpoints for every Map/Branch that references the Object, including historical-time reconstructions. A Map-local definition edit affects only its owner Map and descendant Branch paths.
- Runtime state changed by a post-zero Event remains in the active Map/Branch History and is not written into this definition row.

#### `core_object_id_allocator`

- PK: `(project_id, object_type_id)`; FK to `core_object_type`.
- Fields: `next_sequence INTEGER NOT NULL CHECK(next_sequence >= 1)`, `allocator_format_version`, timestamps.
- `next_sequence` advances only in the same short transaction that inserts a reservation. It never moves backwards.
- The allocation formatter derives `allocated_id_value` from the stable Object Type format and sequence; exact display grammar remains a separate Object Type contract.

#### `core_object_id_reservation`

- PK: `reservation_id TEXT`; unique `(project_id,reservation_id)` for AI-side references.
- FKs: `(project_id,object_type_id) -> core_object_type`; optional `(project_id,object_type_id,object_identity_id) -> core_object` once Bound.
- Fields: `project_id`, `object_type_id`, optional `object_identity_id`, `allocated_sequence INTEGER`, `allocated_id_value TEXT`, `reservation_state` (`Reserved`,`Bound`,`Inserted`,`Abandoned`), `request_key TEXT`, `abandon_reason`, `created_at_utc_ms`, `updated_at_utc_ms`.
- Unique `(project_id,object_type_id,allocated_sequence)` and `(project_id,object_type_id,allocated_id_value)`.
- Unique `(project_id,request_key)` when a request key is supplied; this supports idempotent reservation response recovery.
- Unique `(project_id,object_type_id,object_identity_id)` when non-null: at most one reservation provenance per Object identity. CHECK `Reserved` requires null identity; `Bound` and `Inserted` require a non-null identity; `Abandoned` may retain a bound identity or remain unbound. The referenced `core_object` row is created in the same transaction that binds the reservation.
- At `Reserved -> Bound`, Core Domain creates a `core_object` row with lifecycle `draft`, then stores the same identity key and transitions the reservation to `Bound` in one transaction. At `Bound -> Inserted`, the identity row becomes `active` and the reservation becomes `Inserted` atomically. At `Bound -> Abandoned`, the identity row becomes an `abandoned` tombstone; at `Reserved -> Abandoned`, no identity row exists.
- `Reserved -> Bound -> Inserted`, or `Reserved/Bound -> Abandoned`; terminal states cannot reopen. Core Domain services enforce transition rules; CHECK/trigger defense-in-depth may enforce the allowed state graph.
- Reservation allocation and allocator advancement commit together. Object insertion and `Bound -> Inserted` commit together. Task Undo never recycles a committed reservation.

### 4.3 Unified History and Branch

#### `core_history`

- PK: `history_id TEXT`; FK `project_id`.
- Fields: `project_id`, `history_kind` (`map_root`/`branch_owned`), nullable `owner_map_id`, `created_at_utc_ms`.
- CHECK `map_root` requires non-null `owner_map_id`, while `branch_owned` requires null `owner_map_id`; unique `(project_id,owner_map_id)` and `(project_id,history_id)`; composite FK `(project_id,owner_map_id) -> core_map`.
- A root History is owned by exactly one Map. A Branch-owned History is linked from `core_branch.own_history_id`; its `history_kind` must be `branch_owned`, enforced by Core Domain service and integrity checks. Commands/Events/order constraints are scoped to this History.

#### `core_branch`

- PK: `branch_id TEXT`; FK `project_id`.
- Fields: `project_id`, `parent_history_id`, `own_history_id`, `parent_day_number`, `parent_offset_seconds`, `branch_name`, `lifecycle_status`, timestamps.
- Composite FKs `(project_id,parent_history_id)` and `(project_id,own_history_id) -> core_history`; unique `(project_id,own_history_id)`; CHECK parent History != own History, parent day is non-negative, and offset range. `own_history_id` must reference a `branch_owned` History; this owner-kind invariant is checked by Core Domain services.
- Branch row and its own History are created in one transaction. This avoids a circular FK because `core_history` does not point back to `core_branch`.
- Branch-of-Branch is supported. The parent is a History, not necessarily a Map root.
- Ancestor Commands/Events/State Changes/ordering constraints are not copied. Inheritance is computed through `parent_history_id + parent_at`.
- Cycles and owner-kind consistency are enforced by Core Domain services and verified by recursive integrity checks. A database FK alone cannot guarantee acyclic ancestry.
- Ancestor history at or before the closed fork boundary is inherited. Branch-owned State Changes must be strictly later than `parent_at`.

### 4.4 Command, Event and ordering

#### `core_command`

- PK: `command_id TEXT`; FK `(project_id,history_id) -> core_history`; unique `(project_id,command_id)` and `(project_id,history_id,command_id)` for same-Project and same-History references.
- Fields: `project_id`, `history_id`, `command_type_id`, `descriptor_schema_version`, `execution_semantics_id`, `payload_json`, `effective_day_number`, `effective_offset_seconds`, `precision_range_end_day_number`, `precision_range_end_offset_seconds`, `declared_precision_code`, optional `time_calendar_id`, optional `time_input_json`, optional `execution_end_day_number/execution_end_offset_seconds`, optional `duration_seconds`, optional descriptor-specific `execution_schedule_json`, `created_at_utc_ms`, `updated_at_utc_ms`.
- CHECK effective day and precision-range end day are non-negative; all four interval offsets are in `[0,86399]`; the precision-range end pair is required and strictly greater than the start pair. `execution_end_day_number/execution_end_offset_seconds` must be both null or both non-null, with non-negative day and valid offset. Sustained Commands use either an explicit execution-end pair or a descriptor-defined duration, not two independently authoritative values. CHECK duration is non-negative and not simultaneously authoritative with an explicit execution-end pair. The execution end must not precede the effective start; descriptor validation handles coarse-start ambiguity. `time_calendar_id`, when present, is a same-Project FK to `core_calendar`; Core Domain verifies it belongs to the Map owning the target History.
- Index `(project_id,history_id,effective_day_number,effective_offset_seconds)`.
- Payload and execution semantics are versioned. A handler upgrade must not silently reinterpret old Commands.
- Command identity remains stable across ordinary edits; no Command Revision Chain.
- Optional narrative/grouping Event associations are persisted through `core_command_event`; they do not determine execution order.

#### `core_event`

- PK: `event_id TEXT`; FK `(project_id,history_id) -> core_history`; unique `(project_id,event_id)` and `(project_id,history_id,event_id)` for same-Project and same-History references.
- Fields: `project_id`, `history_id`, `event_type_id`, `event_schema_version`, `payload_json`, `historical_day_number`, `historical_offset_seconds`, `precision_range_end_day_number`, `precision_range_end_offset_seconds`, `declared_precision_code`, optional `time_calendar_id`, `created_at_utc_ms`, `updated_at_utc_ms`.
- CHECK historical day and precision-range end day are non-negative; both offsets are in `[0,86399]`; the Event precision interval is required and its exclusive end is strictly greater than its start. `time_calendar_id`, when present, is a same-Project FK to `core_calendar`; Core Domain verifies it belongs to the Map owning the target History.
- Event does not directly mutate World State. An Event may have zero or many related State Changes. Event association is explanatory, not ordering authority.
- Post-zero Event effects belong only to the currently active Map/Branch History path. An Event targeting a Shared Object does not globally mutate its runtime state in other Maps.

#### `core_command_event`

- PK: `(project_id,history_id,command_id,event_id)`.
- Composite FK `(project_id,history_id,command_id) -> core_command`; composite FK `(project_id,history_id,event_id) -> core_event`.
- Optional explanatory/grouping relation: one Command may be associated with zero or more Events, and one Event may be associated with zero or more Commands.
- An Event with no associated Command is valid narrative history. A Command may create an Event without generating State Changes.
- The relation does not determine execution order. Runtime State Changes generated by a Command receive Event association according to the Command descriptor/handler; State Changes are not persisted in V1.

#### Typed Object reference links

- `core_command_object_ref(project_id,history_id,command_id,object_identity_id,role)` links Commands to formal Objects; FKs point to the Command in its owning History and to `core_object` in the same Project. **Required for the enabled V1 Object, Geometry and Relationship Command descriptors.**
- `core_event_object_ref(project_id,history_id,event_id,object_identity_id,role)` links narrative Events to participant/target Objects; FKs point to the Event in its owning History and to `core_object` in the same Project.
- The required Command Object link roles include target/source/endpoint roles declared by Object, Geometry and Relationship descriptors. Relationship establishment and termination use source/target references; relationship identity and origin remain in typed payloads, not a separate table.
- These links make Object identity references verifiable without parsing JSON. Add only the roles required by the concrete Command/Event catalog. `core_event_object_ref` remains conditional on the enabled Event participant/target reference contract.

#### `core_calendar_configuration`

- PK: `calendar_configuration_id TEXT`.
- Composite FKs `(project_id,map_id,calendar_id) -> core_calendar` and `(project_id,owner_history_id) -> core_history`; optional `(project_id,owner_history_id,source_command_id) -> core_command`.
- Fields: `project_id`, `map_id`, `calendar_id`, `owner_history_id`, optional `source_command_id`, `configuration_origin` (`user_created`/`command_managed`), `effective_from_day_number`, `effective_from_offset_seconds`, optional `effective_until_day_number/effective_until_offset_seconds`, `rules_schema_version`, `rules_payload_json`, `created_at_utc_ms`, `updated_at_utc_ms`.
- CHECK start day is non-negative and offset is in `[0,86399]`; end pair must be both null or both non-null; if present, end is strictly greater than start.
- `configuration_origin='command_managed'` requires `source_command_id`; `user_created` requires it to be null. The source Command must belong to the same History as the configuration.
- Unique `(project_id,map_id,calendar_id,owner_history_id,effective_from_day_number,effective_from_offset_seconds)`.
- Configurations are display rules effective over Historical Time, not World State and not a replayable Calendar History. Branch-owned configurations are local to that Branch History and inherit only through the selected Branch path.
- Deleting a Calendar reform Command explicitly deletes its corresponding command-managed configuration in the same Core Domain transaction; retiming the Command updates its configuration interval and recalculates adjacent command-managed intervals. User-created configurations are not deleted as a side effect.
- Calendar-only changes do not invalidate World State Checkpoints or trigger World State reconstruction. They only refresh date interpretation for affected intervals.


#### `core_command_order_constraint`

- PK: `constraint_id TEXT`; composite FK `(project_id,owner_history_id) -> core_history`; composite FKs `(project_id,before_command_id)` and `(project_id,after_command_id) -> core_command`.
- Fields: `project_id`, `owner_history_id`, `before_command_id`, `after_command_id`, `relation_kind` (typed ordering/prerequisite), `provenance`, `created_at_utc_ms`, optional `updated_at_utc_ms`.
- CHECK before != after; unique `(project_id,owner_history_id,before_command_id,after_command_id,relation_kind)`.
- Persist user-authored relations as endpoints + direction, never only as numeric rank.
- Cycles/conflicts are retained as authored data; current conflict status is derived. Do not add a persisted authoritative dependency graph or auto-delete cyclic relations.
- A Branch may add relations owned by its own History, including relations to visible inherited Commands. It cannot rewrite inherited relations. Core Domain validates that both endpoints are visible on the selected inheritance path.

### 4.5 State Change runtime generation

V1 does not create `core_state_change`, `core_history_materialization` or `core_history_materialization_pointer` tables.

- Core Domain interprets each persisted Command through the exact registered descriptor/handler semantics and generates zero or more State Changes in memory.
- Every runtime State Change retains its source Command identity and effective History context for deterministic application and diagnostics.
- Optional Event association is resolved from the persisted `core_command_event` grouping and the Command descriptor's output semantics.
- Editing/deleting/inserting Commands invalidates affected Checkpoints; the next reconstruction regenerates State Changes from authoritative Commands.
- This avoids a second persisted derived ledger, generation pointers, stale materialization cleanup and extra migration complexity.
- If profiling later proves repeated Command interpretation is a material bottleneck, a State Change materialization may be introduced through a forward migration. It must remain rebuildable and non-authoritative.

### 4.6 Assets and Checkpoints

#### `core_asset`

- PK: `asset_id TEXT`; FK `project_id`; unique `(project_id,asset_id)` for composite owner FKs.
- Fields: `project_id`, `content_hash`, `hash_algorithm`, `byte_length`, `media_type`, `format_version`, `relative_path`, `publication_state`, `integrity_state`, `created_at_utc_ms`.
- Unique `(project_id,hash_algorithm,content_hash)`.
- Asset bytes are immutable and stored outside SQLite. Never overwrite a published file in place.

#### Typed asset links

Use only links needed by actual domain payloads. V1 known candidates:
- `core_object_definition_asset(project_id,object_identity_id,asset_id,role)`;
- `core_map_initial_object_asset(project_id,map_id,initial_state_id,object_identity_id,asset_id,role)`;
- `core_command_asset(project_id,history_id,command_id,asset_id,role)` **required for the enabled V1 geometry Command descriptors**.

Each uses composite FKs and a unique key on its owner/asset/role tuple. `core_command_asset` is required for V1 formal geometry Commands. `core_object_definition_asset` and `core_map_initial_object_asset` remain conditional on the adopted definition/Initial State geometry payloads; do not add them without a formal reference need. JSON may hold non-authoritative descriptive metadata, but actual Asset references must be relationally verifiable.

#### `core_checkpoint`

- PK: `checkpoint_id TEXT`; FK `(project_id,history_id) -> core_history`; composite FK `(project_id,asset_id) -> core_asset`.
- Fields: `project_id`, `history_id`, `asset_id`, `target_day_number`, `target_offset_seconds`, `lineage_fingerprint`, `reconstruction_input_fingerprint`, `format_version`, `checkpoint_status` (`building`,`complete`,`invalid`,`corrupt`), `created_at_utc_ms`, `completed_at_utc_ms`.
- CHECK target day is non-negative and target offset is in `[0,86399]`. A Checkpoint is usable only when complete, payload hash-valid, and both fingerprints match the current Core Domain reconstruction inputs.
- `lineage_fingerprint` covers the selected Map/History inheritance path, each Branch fork boundary, and the exact inherited History path for the target.
- `reconstruction_input_fingerprint` covers: Map Initial State and referenced immutable Asset hashes; every applicable Command and its descriptor schema/version plus exact execution-handler semantics identifier; applicable Event Effects and only those Event payload/schema/semantics portions that can affect the reconstructed World State; explicit ordering constraints; every reachable current Shared Object definition (stable identity, definition schema version, canonical definition hash) and its immutable Asset hashes; each referenced Object's pinned Object Type descriptor schema/semantics identifiers; and the reconstruction algorithm/compatibility identifier.
- Exclude transaction timestamps, audit text, UI/presentation metadata, narration-only Event content, and Calendar display configuration when they do not affect World State. A narration-only edit or Calendar Configuration edit must not invalidate World State Checkpoints. If an Event payload participates in deterministic state effects, include the effect-relevant payload and semantics in the fingerprint.
- Canonical serialization, field normalization and hash algorithm must be deterministic and versioned. The fingerprint is a derived cache-validation value, not a new authoritative revision/version entity.
- A Shared Object definition edit invalidates every affected Map/Branch Checkpoint, including historical target times. A Map-local definition edit affects only its owner Map and descendant Branch paths.
- Before publishing a Checkpoint as `complete`, revalidate the full current fingerprint after the payload is built. Protect final revalidation and metadata publication with a short transaction/serialization boundary so a concurrent definition or history edit cannot commit between the recheck and publication. A stale build must not become selectable.
- Missing/corrupt Checkpoints are rebuilt from authoritative inputs; they never invalidate formal history. See P2-09-16 V0.1 for the detailed coherence contract.

## 5. Cross-table invariants owned by Core Domain services

The following must be enforced in Domain Services and covered by database/integration tests; some cannot be expressed safely as a simple FK/CHECK:

1. A Map has exactly one root History, one Initial State, exactly one default Calendar and a day-zero configuration for that default Calendar.
2. A Branch owns exactly one History, references one parent History, inherits the parent time axis, and has no ancestry cycle.
3. Every Branch-owned Command precision interval must begin strictly after its exact `parent_at` boundary; every runtime State Change generated from it must also be later than that boundary.
4. A `core_map_initial_object` must have a matching Map/Object reference.
5. `core_object.sharing_scope='project_shared'` requires `owner_map_id IS NULL`; `sharing_scope='map_local'` requires a valid same-Project `owner_map_id`.
6. A Map-local Object may be registered only to its owner Map; a Project-shared Object may be referenced by multiple Maps in the same Project. Domain Services enforce this, with INSERT/UPDATE trigger defense-in-depth.
7. A Project-shared Object can exist at Project scope without being present in any Map's Initial State.
8. Project-shared Object definition edits update the single current definition and invalidate every referencing Map/Branch Checkpoint; Map-local definition edits affect only the owner Map and descendant Branch paths.
9. A reservation's Project/Object Type matches its Object; one reservation binds at most one Object; one Object has at most one reservation provenance; the bound Object pins a supported Object Type schema/semantics identifier. A Reserved reservation has no identity; a Bound reservation has a draft identity; an Inserted reservation has an active identity; a Bound reservation abandoned later retains an abandoned identity tombstone. Draft/abandoned identities are not visible in Map membership or Initial State.
10. `Inserted` reservation and formal Object insertion commit atomically.
11. Command, Event and State Change History ownership is consistent; Event/State Change associations are explanatory and do not override ordering.
12. A Branch-owned ordering relation may reference only Commands visible on its effective inheritance path; inherited relations cannot be overwritten.
13. A Checkpoint is published only after payload integrity and full current lineage/reconstruction-input validation. The final revalidation and metadata publication must be serialized against concurrent relevant edits.
14. No AI Assistant repository writes Core Domain tables directly.
15. A Calendar Configuration source Command belongs to the same History; deleting/retiming that Command updates only its own command-managed Calendar Configuration and affected adjacent intervals.
16. Calendar Configurations for the same Calendar/History scope must not overlap; gap/default fallback behavior remains subject to explicit Calendar validation.
17. Calendar Configuration edits never invalidate World State Checkpoints or trigger World State reconstruction.
18. Relationship identity, endpoint, origin and temporal activity are validated against the selected History path; no generic relationship registry/table is introduced in V1.

## 6. AI Assistant V1.16 integration and FK policy

AI Assistant Logical Schema V1.16's exact 28 AI-owned domain-table inventory is the integration baseline. Do not duplicate Core Domain entities. UUID text is the proposed Core entity key format, but **not every AI reference should become a database FK**. Core Commands and some target entities can be edited/deleted/retired under Core Domain rules; AI authorization and submission evidence must remain immutable after those lifecycle changes.

Use this policy:

- `object_candidates.object_identity_id`: stable Object reference. A Core FK is appropriate only if Object identity rows are retained as tombstones and never physically deleted; otherwise store the UUID as an opaque reference.
- `object_candidate_versions.base_effective_object_context_ref`: opaque context token, never an FK or Object Version.
- `change_set_versions.target_map_id`, `target_branch_id`, `target_history_id`: store exact Core UUID references, but do not make them restrictive FKs in V1. A stale/deleted target must make submission fail preflight, not prevent Core Domain lifecycle operations or delete AI evidence.
- `command_proposal_items.object_identity_id`: stable Object reference; FK only if Object identity retention is guaranteed. `object_reservation_id`: FK to `core_object_id_reservation` is recommended because reservation records are permanent. Asset references use a Core asset ID only for published immutable assets; staged AI assets remain AI-owned or in an AI staging store.
- `submission_command_refs.core_command_id`: immutable returned Command reference stored as opaque UUID text, **not an FK**. Core Commands are author-editable and may be deleted; the AI report must still preserve the exact original submission reference.
- `target_history_context_ref`, lineage fingerprints and effective-object context tokens remain opaque preconditions, not entity keys.

For references that are guaranteed permanent and should be protected from cleanup, use FKs with `ON DELETE RESTRICT`. For references whose target can be deleted/retired while the AI evidence must survive, retain the exact ID as opaque evidence and resolve it through Core Domain when needed. Never use `ON DELETE CASCADE` from Core Domain into AI evidence.

No AI-owned `maps`, `branches`, `histories`, `commands`, `events`, `state_changes`, `world_states` or `checkpoints` tables are allowed. V1.16 has exactly 28 AI-owned domain tables (9 Task execution, 7 Candidate/Validation, 2 Selection, 10 Proposal/Submission). `submission_command_refs` is the existing immutable opaque receipt for returned Core Command IDs; do not add a duplicate receipt table. `submission_asset_refs` is the existing normalized reference to retained immutable Core Assets. The shared `schema_migration` ledger is Application Bootstrap metadata and is not counted as an AI domain table. These logical tables are not executable DDL.

## 7. Migration ownership and bootstrap order

Use one physical ledger table, owned by the Application Persistence Bootstrap component solely as migration metadata:

`schema_migration(module_id, migration_id, checksum, applied_at_utc_ms, min_app_version, max_app_version, PRIMARY KEY(module_id,migration_id))`.

- `module_id` is a fixed namespace such as `core-domain` or `ai-assistant`.
- Core Domain owns its migration files and SQL semantics; AI Assistant owns its own migration files and SQL semantics.
- Application Bootstrap owns ledger orchestration only; it cannot mutate domain tables except by executing the module's migration scripts.
- Each migration and its ledger insertion run in one SQLite transaction wherever supported.
- Applied migration checksum mismatch, unknown newer migration, schema/ledger disagreement or partial initialization fails closed.

Recommended fresh bootstrap dependency order:

1. Application creates the shared ledger and validates Project database identity.
2. Core Domain creates `core_project` and then `core_map` identity structures.
3. Core Domain creates `core_object_type`, then `core_object` with `owner_map_id`, Object definitions, allocator and reservations. This order is mandatory because Map-local Object ownership now has a real FK to `core_map`.
4. Core Domain creates root/Branch History, Branch, Map Initial State, Map/Object reference and Initial Object structures; their data rows are created in the required Core Domain transactions.
5. Core Domain creates Calendar identity, Commands, Events, Command/Event associations and explicit ordering constraints.
6. Core Domain creates Calendar Configuration after Calendar, History and Command tables exist.
7. Core Domain creates immutable Asset metadata, Checkpoints and only the typed asset-link tables required by actual descriptors.
8. AI Assistant creates its 28 domain tables, separating base tables from Core-reference constraints where needed; permanent Object/Reservation/Asset FKs are added only after the corresponding Core tables exist.
9. Structural checks: `foreign_key_check`, required schema object inventory, owner-map registration triggers, cross-table invariant queries and module compatibility checks.
10. Project becomes writable only after all required steps pass.

If migrations need an interleaved order, encode explicit dependencies in migration metadata rather than relying on filename sorting. No EF Core migration stream is introduced.

## 8. Query/index strategy

Create indexes for real access paths only. Initial candidates:

- `core_map(project_id,lifecycle_status)`;
- `core_calendar(project_id,map_id,calendar_role)` with partial unique default index;
- `core_calendar_configuration(project_id,map_id,calendar_id,owner_history_id,effective_from_day_number,effective_from_offset_seconds)`;
- `core_history(project_id,owner_map_id)` unique; `core_branch(project_id,own_history_id)` unique;
- `core_branch(project_id,parent_history_id,parent_day_number,parent_offset_seconds)`;
- `core_command(project_id,history_id,effective_day_number,effective_offset_seconds,precision_range_end_day_number,precision_range_end_offset_seconds,command_type_id)`;
- `core_event(project_id,history_id,historical_day_number,historical_offset_seconds,precision_range_end_day_number,precision_range_end_offset_seconds)`;
- `core_command_order_constraint(project_id,owner_history_id,before_command_id)` and corresponding target index;
- `core_object(project_id,object_type_id,sharing_scope)`;
- `core_object_id_reservation(project_id,object_type_id,reservation_state)`;
- `core_map_initial_object(initial_state_id,object_identity_id)`;
- `core_asset(project_id,content_hash)` unique;
- `core_checkpoint(project_id,history_id,target_day_number,target_offset_seconds,checkpoint_status)`;
- `core_command_event(project_id,history_id,command_id)` and `(project_id,history_id,event_id)`.

Do not index arbitrary JSON fields without a measured query requirement. FTS5/R*Tree are optional feature-specific indexes, not mandatory infrastructure for this schema phase.

## 9. Critical transaction boundaries

- Reserve Object ID + advance allocator: one short transaction.
- Bind reservation to Object identity: one Core Domain transaction.
- Insert Object + transition reservation to Inserted + required formal creation history: one transaction where the domain operation requires all to appear together.
- Edit/create/delete Command: persist authoritative Command and invalidate affected Checkpoints atomically; runtime State Changes are regenerated during the next reconstruction.
- Create/delete/retime Calendar reform Command + its command-managed Calendar Configuration in one Core Domain transaction; recalculate adjacent display intervals without rebuilding World State.
- Final AI submission: exact authorization/context recheck, Core Domain writes and minimum AI submission evidence must use the same verified SqliteConnection + SqliteTransaction if atomicity is claimed.
- Asset publication is filesystem work and cannot be rolled back by SQLite. Publish immutable files first, then commit references; clean only proven unreferenced orphans.
- Checkpoint publication writes/validates the immutable payload first, then atomically publishes complete metadata after lineage revalidation.

No LLM generation, long validation, geometry processing, user interaction or network call occurs inside the final write transaction.

## 10. Remaining gates before executable DDL

1. Confirm exact Object Type display grammar and ensure Core Domain APIs expose both the stable internal identity key and the permanent allocated Object ID value. Project + Object Type namespace and the storage-key separation are the recommended V1 design.
2. Validate the half-open precision-interval conversion and Calendar Configuration interval recalculation against complete Calendar descriptors and D01 examples; D01 remains Proposed / Discussion.
3. Finalize concrete V1 Command Type IDs, payload schemas and execution semantics identifiers.
4. Confirm exact Initial State payload shape and whether typed asset-link tables are needed for each payload.
5. Verify deterministic in-memory State Change generation and historical handler compatibility; V1 does not persist a State Change table.
6. Prove Microsoft.Data.Sqlite shared-connection transaction behavior with the actual Core Domain service and AI repository composition.
7. Verify Avalonia 12/.NET 10/package/RID compatibility and packaged native SQLite on Windows/macOS.
8. Select WAL/synchronous/busy-timeout settings and prove backup/restore includes committed WAL content and required assets.
9. Define the backup manifest and final project directory layout.
10. Validate indexes with representative workloads and confirm no unnecessary schema complexity.

## 11. Status

V0.10 is the recommended reconciled relational design baseline for the next bounded schema review. It retains the V0.9 ownership, Object Type compatibility pins, Geometry Component Asset/reference requirements and Checkpoint freshness decisions, and incorporates the relationship identity/temporal contract without adding a relationship table. It does not add any new baseline domain table.

Open gates remain: final Object Type display grammar and first built-in type seed set; first enabled Relationship Type seed set and per-type cardinality/cycle rules; precise Calendar conversion and configuration gap/overlap behavior; machine-readable payload schemas and canonical serialization for enabled Command descriptors; final conditional definition/Initial State/Event typed-link selection; SQLite CHECK/trigger/provider validation; and packaged Windows/macOS shared-transaction proof.

It is **not** executable DDL, a provider-tested migration package, proof of cross-module atomicity, or implementation authorization. Development implementation and test execution remain the responsibility of the development team after the design gates close.