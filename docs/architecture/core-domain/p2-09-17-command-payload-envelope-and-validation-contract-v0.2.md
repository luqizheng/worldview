# World View P2-09-17 — Command Payload Envelope and Descriptor Validation Contract V0.2

- Version: 0.2
- Date: 2026-11-21
- Status: Reconciled payload contract — canonical envelope and descriptor-level validation baseline; concrete Object Type and Geometry Component schemas remain domain-owned extensions
- Supersedes: P2-09-17 V0.1; V0.1 is retained as history
- Depends on: P2-09-03 V0.2; P2-09-05 V0.3; P2-09-08 V0.2; P2-09-11 V0.8; P2-09-15 V0.1; P2-09-14 V0.1; P2-09-04 V0.2; ME-01-05 V0.1

## 1. Purpose

Define the canonical persisted payload contract for the recommended V1 Command descriptors. This document closes the gap between the Command catalog and the physical Command envelope without introducing a generic workflow engine, a second history store, or a table per Command type.

The payload is an immutable-at-replay-time authoring input inside an editable Command record. Ordinary Command edits replace the authoritative payload under Core Domain rules; no Command Revision Chain is introduced. A payload must be sufficient to reproduce the same domain operation under its recorded descriptor and execution-semantics versions.

This contract does not freeze Object Type-specific attribute vocabularies, all relationship types, geometry binary encoding, or Unit movement. Those remain separately governed domain contracts.

## 2. Canonical Command envelope

The relational Command row remains the authoritative envelope. Its logical fields are:

| Field | Required | Meaning |
|---|---|---|
| command_id | Yes | Stable identity of this authoring Command |
| history_id | Yes | Exactly one owning Map-root or Branch-owned History |
| command_type_id | Yes | Stable registered descriptor ID |
| descriptor_schema_version | Yes | Version used to validate and interpret payload structure |
| execution_semantics_id | Yes | Exact deterministic handler semantics compatibility identifier |
| payload_json | Yes | Canonical descriptor-specific payload |
| effective_time | Descriptor-dependent | Normalized Map-relative start boundary and precision interval |
| duration_or_end | Descriptor-dependent | One authoritative sustained execution boundary, never two |
| time_calendar_id / time_input_json | Optional | Original Calendar interpretation provenance only |
| execution_schedule_json | Descriptor-dependent | Only for descriptors with explicit deterministic pause/resume schedules |
| created_at / updated_at | Yes | Transaction-time audit metadata, never Historical Time |

The physical columns are defined in P2-09-11. This document defines their logical relationship and payload rules.

### 2.1 Canonical payload root

Every payload root is a JSON object with these reserved members:

- `payload_schema_version`: positive integer, equal to the descriptor's payload schema version.
- `operation`: descriptor-owned object containing only fields declared by that Command Type.
- `provenance`: optional structured authoring evidence when required by the descriptor; not an execution input unless explicitly declared.
- `extensions`: prohibited in V1 persisted payloads. Unknown fields are rejected, not silently retained.

The envelope's descriptor schema version is authoritative. The payload's `payload_schema_version` is a consistency assertion and must match it. It is not a second independently selectable version.

No untyped arbitrary `metadata`, `patch`, `properties`, or `resulting_state` object is allowed as a universal escape hatch.

## 3. Canonical serialization rules

1. UTF-8 JSON with deterministic canonical serialization.
2. Object keys are serialized in ordinal lexicographic order; arrays retain declared semantic order.
3. Numbers use invariant JSON number formatting. NaN, positive/negative infinity and non-finite values are rejected.
4. Historical Time values are integer day/offset pairs; never floating-point timestamps or local-time strings.
5. IDs are opaque stable identifiers, not display names or allocation sequence guesses.
6. Enumerated values use stable descriptor-defined codes, not localized labels.
7. Optional fields are omitted when absent; explicit null is permitted only where the descriptor schema declares nullable semantics.
8. Duplicate JSON object keys are rejected before deserialization.
9. Unknown fields, unknown enum codes, invalid units, invalid coordinate references and unsupported schema versions are rejected.
10. Geometry coordinates and numeric arrays use the canonical coordinate/precision rules of the formal Geometry Component model; screen-space values are never serialized as domain coordinates.
11. Asset references use immutable Core Asset identity and content hash validation. A path, URL or mutable filename alone is not a replayable Asset reference.
12. Canonical payload bytes are hashed for AI Proposal sealing and Checkpoint input fingerprints as applicable; hash algorithm/version is explicit at the owning contract.

Canonical serialization is a semantic compatibility boundary. A serializer upgrade that changes the canonical bytes without changing meaning must be explicitly versioned/migrated; it must not accidentally create a different payload interpretation.

## 4. References and ownership

### 4.1 Object references

- Every Object reference in a Command must be declared in the descriptor's reference contract.
- The owning History and Project are resolved by Core Domain; callers cannot supply a different Project scope inside payload.
- Object references required for integrity are also represented through `core_command_object_ref` typed links with descriptor-defined roles.
- The payload may carry operation-specific role labels or structured parameters, but it must not create a competing, unvalidated Object registry.
- Core Domain validates Project ownership, Object lifecycle, sharing scope, Map registration and History visibility before persistence and during reconstruction.
- For `core.object.create`, the target Object identity must already be Bound to a valid Core Domain reservation. The reservation ID is an opaque reference in the payload; the Object identity is verified through the typed Object reference.
- A Map-local Object must belong to the Map owning the target History. A Project-shared Object may be used only where its registration and domain contract permit it.

### 4.2 Asset references

- Immutable Assets required to execute or reproduce a Command are declared through `core_command_asset` with a descriptor-defined role.
- The payload may carry content hashes/provenance required by the operation, but the relational link is the authoritative Core Asset identity reference.
- Core Domain verifies Project ownership, publication state, hash and format compatibility.
- Candidate/temporary Asset paths are not valid formal Command references until the Asset is published and immutable.
- If the descriptor needs no Asset, it must not create an empty or placeholder link.

### 4.3 Relationship references

Relationship Commands use typed endpoint Object references. Relationship type, direction, cardinality and temporal semantics are validated by the registered relationship contract. Coordinate coincidence or geometry intersection is never a substitute for an explicit relationship proposal.

## 5. Descriptor payload schemas

The following are recommended logical payload contracts. Field names are stable proposals for review; each descriptor must publish a machine-readable schema from the statically registered catalog before implementation. No generic Command payload table or per-type SQL table is required.

### 5.1 `core.object.create`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| reservation_id | Opaque ID | Must resolve to a Bound reservation in the same Project |
| object_type_id | Opaque ID | Must equal the bound Object identity's Object Type |
| object_identity_id | Opaque ID | Must be the identity bound to reservation_id |
| creation_mode | Enum | Descriptor-defined semantic creation mode; not an Initial State operation |
| initial_runtime_attributes | Typed object | Only fields allowed by the Object Type's creation contract |
| initial_geometry_roles | Array | Zero or more typed geometry roles with immutable Asset references as required |
| initial_relationships | Array | Optional explicitly declared relationship proposals; each validated separately |

Rules:
- Command target History must be post-zero; Map Initial State authoring is a separate path.
- Object identity must be draft/Bound before Command preflight.
- Insertion, reservation transition to Inserted and Command application commit atomically.
- The Object Type definition is not copied into the payload. The payload stores only the per-instance operation inputs.
- Geometry roles and Assets must match the Object Type's allowed geometry families and coordinate context.
- No implicit relationship is created from spatial proximity.

### 5.2 `core.object.deactivate`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| object_identity_id | Opaque ID | Exactly one active target Object |
| deactivation_reason_code | Stable enum | Optional only if Object Type permits omission |
| retirement_attributes | Typed object | Optional descriptor-approved terminal facts; no arbitrary patch |

Rules:
- Only an active Object visible to the target History can be deactivated in that History.
- Deactivation changes only the selected Map/Branch runtime state. It must not mutate the Project-level `core_object.lifecycle_status`, Shared Object definition, physical identity row or reservation. The same Project-shared Object may remain active in another Map/History.
- Existing historical references remain resolvable. Whether an Object can be retired while referenced by active relationships is Object Type/relationship validation, not a generic database cascade.
- The Command must not erase unrelated relationships or Assets implicitly. Reactivation, if supported, is a separate explicit history operation and is not implied by deleting/editing this Command.

### 5.3 `core.object.attribute.change`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| object_identity_id | Opaque ID | Exactly one active target Object |
| attribute_key | Stable key | Must be declared mutable by the Object Type |
| new_value | Typed value | Must satisfy the declared attribute schema |
| expected_value | Optional typed value | Optional optimistic precondition; if supplied, compare against reconstructed state at the effective boundary |

Rules:
- One Command changes one semantic runtime attribute key in the selected History. It never edits the Project-level Shared Object current definition. Multiple changes use multiple Commands unless a domain-specific atomic operation is later defined.
- Unknown, immutable or presentation-only attributes are rejected.
- No generic JSON Patch, arbitrary property bag or blind replacement of the whole Object definition is permitted.
- Deterministic replay derives the prior value from applicable history; the Command does not need to persist a mutable resulting World State snapshot.

### 5.4 `core.geometry.adjust_component`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| object_identity_id | Opaque ID | One formal target Object |
| geometry_component_id | Opaque ID | Component must belong to target Object and selected geometry role |
| geometry_role | Stable key | Must be a declared authoritative/alternate authoring role |
| edit_operation | Typed enum/object | Only registered geometry edit operations and parameters |
| resulting_geometry_asset_id | Opaque ID | Immutable published geometry Asset used as deterministic formal input |
| source_geometry_fingerprint | Hash descriptor | Must match the geometry source against which the edit was authored |

Rules:
- The payload represents an explicit accepted domain edit, not pointer movement or a screen-space preview.
- The geometry Asset contains the exact formal coordinate data to be adopted. The handler validates its format, coordinate reference, geometry family, precision and domain invariants.
- The source fingerprint prevents applying a stale edit against a different geometry input.
- Component identity remains stable for an in-place adjustment.
- The Command changes geometry only in the selected History's runtime state; it does not edit the Project-level Shared Object definition, change semantic Object identity, membership or relationships unless a separate explicitly registered Object/Relationship Command is also submitted.
- The deterministic handler must not re-run an unseeded interactive algorithm or query current external data during replay.

### 5.5 `core.geometry.refine_component`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| object_identity_id | Opaque ID | One formal target Object |
| geometry_component_id | Opaque ID | Existing target component |
| geometry_role | Stable key | Existing formal role |
| source_geometry_fingerprint | Hash descriptor | Exact coarse/approximate source identity |
| refined_geometry_asset_id | Opaque ID | Immutable published refined geometry Asset |
| refinement_method_id | Stable ID | Registered deterministic method or explicit author-authored result |
| refinement_parameters | Typed object | Complete parameters and captured inputs required to reproduce/validate the result |
| coordinate_reference_id | Stable ID | Exact domain spatial reference used by the geometry |

Rules:
- The refined result replaces the applicable authoritative component in the selected History's runtime state at the Command's effective time; the previous geometry remains derivable from earlier history. It does not retroactively rewrite the Project-level Shared Object definition.
- Source fingerprint and coordinate reference are mandatory.
- If the refined result was produced by an AI or external tool, the exact accepted output Asset and required provenance are captured; replay does not call the external generator again.
- Derived LOD or presentation simplification is not eligible for this Command.

### 5.6 `core.relationship.establish`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| relationship_type_id | Stable ID | Registered relationship contract |
| source_object_identity_id | Opaque ID | Valid source endpoint |
| target_object_identity_id | Opaque ID | Valid target endpoint; must differ unless self-relations are explicitly allowed |
| direction | Enum | Defined by relationship type |
| relationship_attributes | Typed object | Only attributes declared by relationship type |
| relationship_key | Stable key | Descriptor-local key for identifying this relationship effect in subsequent history |

Rules:
- Both endpoints are also represented by typed Command Object reference links with roles `source` and `target`.
- Endpoint Object Types, same-Project/Map scope, cardinality, direction, acyclicity (where required), and temporal compatibility are validated.
- The relationship is not created merely because geometries touch, intersect or snap.
- Relationship key is scoped to the establishing Command and is not a separate global Object identity.
- Initial structural relationships belong to Initial State, not this post-zero Command.

### 5.7 `core.relationship.end`

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| establishing_command_id | Opaque ID | Must identify a visible prior relationship.establish Command |
| relationship_key | Stable key | Must match one relationship effect produced by that Command |
| end_reason_code | Stable enum | Optional only if relationship contract permits omission |

Rules:
- The establishing Command must be visible on the selected History inheritance path and must not be after the end Command.
- Relationship identity is resolved from the establishing Command and relationship_key; no new independent relationship registry is introduced by this payload contract.
- If the establishing Command is edited/deleted, dependent relationship-end Commands are revalidated and may become blocked; they are not silently deleted or rewritten.

### 5.8 `core.calendar.reform` (disabled until validation gates close)

Required operation fields:

| Field | Type | Rule |
|---|---|---|
| calendar_id | Opaque ID | Calendar belonging to the Map owning the target History |
| effective_from | Canonical time boundary | Must match the Command's validated effective boundary |
| rules_schema_version | Positive integer | Supported Calendar rule schema |
| rules_payload | Typed object | Fully validated rule set, not free-form text |
| configuration_origin | Literal enum | Must be command_managed |

Rules:
- The Command and command-managed Calendar Configuration are created/updated/deleted atomically.
- This Command produces no World State State Change.
- It must not reinterpret already persisted Command/Event canonical intervals.
- Do not enable until P2-09-14 conversion, gap/fallback and Branch inheritance validation gates close.

## 6. Commands that remain outside this payload contract

No V1 payload schema is defined here for semantic Object split/merge, Unit movement, terrain evolution, map-wide bulk transformations, or automatic geometry extraction. Their prerequisite domain contracts are listed in P2-09-15.

Event payloads are governed by the separate Event schema/descriptor contract. This document does not convert Events into Commands.

Initial State baseline payloads are governed by the Initial State authoring contract, not by Command payload schemas.

## 7. Validation pipeline

Core Domain validates in this order before persisting a Command:

1. Resolve the target Project, Map and History from trusted application context.
2. Resolve the exact registered Command descriptor and enabled status.
3. Validate descriptor schema version and canonical JSON shape; reject unknown/duplicate fields.
4. Resolve every Object reference and validate Project, Map registration, sharing scope, lifecycle and History visibility.
5. Resolve every Asset reference and verify immutable publication, content hash and format.
6. Validate Object Type, geometry role, relationship type and descriptor-specific domain invariants.
7. Normalize Historical Time through Core Domain Calendar services; persist canonical half-open interval and provenance.
8. Validate Branch fork boundary, duration/end exclusivity, explicit ordering constraints and deterministic prerequisites.
9. Validate authorization/application preconditions where the Command originated from AI Proposal.
10. Persist the Command and all required Core-owned side effects atomically. On any failure, persist none of the formal Command application.

Reconstruction repeats the deterministic validation necessary to ensure the stored payload and recorded handler semantics remain supported. It must not use today's mutable UI settings or external network data.

## 8. Payload editing and compatibility

- A Command edit replaces its authoritative payload and updates the same Command identity under Core Domain rules.
- Revalidate the complete payload, references, time and ordering after every edit.
- Rebuild from the earlier affected Historical Time and invalidate affected Checkpoints.
- The old payload is not retained as a Command Revision Chain. AI Proposal and authorization evidence remains immutable and is not reused for the edited content.
- A payload schema migration must declare source/target schema versions and prove semantic preservation.
- If semantic meaning changes, register a new execution semantics identifier; do not silently reinterpret existing payloads.
- Unknown or unavailable historical handler semantics produce an explicit unsupported-history diagnostic.

## 9. Physical persistence implications

- Continue using the existing `core_command.payload_json` field; no per-Command-type table is introduced.
- Keep descriptor identity/version and execution semantics identifier in dedicated columns as specified by P2-09-11.
- Use `core_command_object_ref` and `core_command_asset` for typed integrity references required by enabled descriptors.
- The exact conditional typed-link inventory must be reconciled against the enabled descriptor set before DDL. The current recommended minimum requires Object references; geometry descriptors require immutable Command Asset references.
- No State Change table is added in V1.
- The relationship effect identity described here is derived from its establishing Command; it does not create a new standalone relationship history table in V1.

## 10. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| PAY-01 | Payload has unknown or duplicate field | Reject before persistence |
| PAY-02 | Payload schema version differs from descriptor column | Reject |
| PAY-03 | Non-canonical numeric value or non-finite geometry coordinate | Reject |
| PAY-04 | Object reference belongs to another Project | Reject |
| PAY-05 | Map-local Object is referenced from another Map | Reject |
| PAY-06 | Candidate/unpublished Asset is referenced by formal Command | Reject |
| PAY-07 | Geometry edit source fingerprint is stale | Reject and request re-authoring |
| PAY-08 | Geometry component belongs to another Object | Reject |
| PAY-09 | Geometry split is submitted as semantic Object split without Object operation | Reject/misclassification |
| PAY-10 | Relationship proposed from proximity only | No automatic relationship; require typed explicit proposal |
| PAY-11 | Relationship end references unknown or invisible establishing Command | Reject or mark dependent Command blocked during rebuild |
| PAY-12 | Object creation uses Reserved but not Bound reservation | Reject |
| PAY-13 | Object creation is submitted at time zero | Reject; use Initial State authoring |
| PAY-14 | Calendar reform submitted while P2-09-14 gates remain open | Descriptor disabled/rejected |
| PAY-15 | Handler upgrade changes historical interpretation | Require new semantics ID or controlled migration |
| PAY-16 | Command edit changes canonical payload | Same Command identity; affected reconstruction invalidated; no revision chain |

## 11. Remaining design gates

1. Finalize Object Type schema/version and mutable attribute declarations.
2. Finalize Geometry Component identity, roles, asset media formats, coordinate reference and canonical geometry serialization.
3. Confirm the exact transaction sequence for Bound reservation, Command creation and Inserted transition using the provider proof specification.
4. Finalize relationship type registry, temporal semantics and relationship-end dependency rules.
5. Complete Calendar validation gates before enabling Calendar reform.
6. Define the AI Proposal-to-Command payload sealing boundary using the exact canonical serialization/hash contract.
7. Reconcile the required typed Object/Asset link tables against the enabled descriptor subset.

## 12. Status

P2-09-17 V0.2 defines the recommended V1 Command payload envelope and descriptor-level validation pipeline. It narrows the payload shape without pretending Object Type vocabularies, geometry encoding, all relationship types or Unit movement are already frozen.

It is a design contract, not executable JSON Schema, SQL, implementation code, provider test evidence or implementation authorization.
