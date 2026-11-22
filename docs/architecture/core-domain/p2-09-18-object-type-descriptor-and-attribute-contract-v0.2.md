# World View P2-09-18 — Object Type Descriptor and Attribute Contract V0.2

- Version: 0.2
- Date: 2026-11-21
- Status: Schema-reconciled recommended V1 Object Type contract — typed built-in descriptor model; concrete specialist type catalog remains subject to ME-02/ME-03/ME-04 domain work
- Supersedes: P2-09-18 V0.1; V0.1 is retained as history
- Depends on: P2-09-02 V0.1; P2-09-04 V0.2; P2-09-07 V0.2; P2-09-11 V0.9; P2-09-15 V0.3; P2-09-17 V0.3; P2-09-19 V0.1; ME-01-05 V0.1

## 1. Purpose

Define the Core Domain Object Type contract required by Object identity allocation, Object creation, typed attribute changes, geometry validation, relationship validation and deterministic reconstruction.

An Object Type is a domain category and behavior contract. It is not merely a UI label, a generic JSON schema, a presentation symbol, or an AI-generated category.

V1 should provide a bounded built-in typed registry compiled into the application. The registry is owned by Core Domain. The Project database stores the Project's Object Type identities and user-configurable display/allocation metadata, but it does not become a user-authored executable ontology.

This document does not establish an exhaustive Object Type taxonomy. ME-02/ME-03/ME-04 may add specialist types after their domain rules are designed.

## 2. Core decisions

1. Every formal Object has exactly one stable Object Type identity.
2. Object Type defines semantic category, identity allocation namespace, allowed definition schema, runtime attribute schema, geometry roles/families, relationship participation and domain validation.
3. V1 uses a statically registered built-in descriptor catalog; arbitrary user-defined executable Object Types and a universal ontology are out of scope.
4. Project-level Object Type configuration may change display names and permitted ID presentation/allocation settings, but cannot change stable type identity or reinterpret existing Objects.
5. Each bound Object identity pins the Object Type descriptor schema and execution-semantics compatibility identity used to create and interpret it.
6. Object Type definition data, Object identity, Project-shared current definition, Map/Branch runtime state and Presentation remain separate.
7. Initial State facts belong to the Map's time-zero baseline. Post-zero runtime changes belong to Commands in the selected Map/Branch History.
8. A Project-shared Object may have one current shared definition and different runtime states in different Maps/Branches.
9. A Map-local runtime Command must never mutate Project-level Shared Object definition or global Object identity lifecycle.
10. Object Type-specific validation belongs to Core Domain, not the Editor, AI Assistant, renderer or generic JSON parser.

## 3. Object Type descriptor model

Each built-in Object Type descriptor must define:

| Descriptor area | Required declaration |
|---|---|
| Stable identity | Stable type key, descriptor schema version and type semantics compatibility identifier |
| Human metadata | Default display name, description, category/group and localization keys |
| Identity allocation | Whether the type is allocatable, allocation namespace (Project + Object Type), display grammar contract and reserved-value rules |
| Definition schema | Typed fields for the current Object definition, required/optional rules and validation |
| Runtime state schema | Typed fields that may vary in a Map/Branch History, with deterministic application semantics |
| Initial State schema | Fields and references required to establish time-zero membership/placement/relationships |
| Geometry contract | Allowed geometry families/dimensions, roles, component cardinality, coordinate-reference constraints and validation |
| Relationship contract | Allowed incoming/outgoing relationship types, endpoint roles, cardinality and temporal behavior |
| Lifecycle contract | Identity-level lifecycle support and History-local runtime availability semantics |
| Command capabilities | Exact registered Command descriptors the Object Type may participate in, including source/target roles |
| Asset contract | Allowed immutable Asset media/format types and required provenance |
| Compatibility | Schema migration and historical semantics retention policy |

A descriptor is executable domain metadata owned by Core Domain. UI metadata can be projected from it, but UI labels and inspector layouts are not the authority for validation.

## 4. Stable identity and Project configuration

### 4.1 Stable type identity

- `type_key` is stable, non-localized and never reused within the Project.
- Display name, icon, category label and help text are not identity.
- Renaming a display name does not change the type key, Object IDs, Object references or historical meaning.
- A built-in type cannot be deleted while any reservation, Object identity, definition, Command or historical evidence references it.
- A deprecated type may be marked non-allocatable for new Objects while remaining readable/replayable for existing Objects.
- Replacing a type with another type is not a display rename. It requires a separate explicit migration/re-authoring process with validation and provenance.

### 4.2 Project-level Object Type row

The existing `core_object_type` row is Project-scoped and stores:
- stable `object_type_id` and `type_key`;
- Project-specific display name/code;
- allocatable flag;
- ID allocation format configuration and its format version;
- creation/update metadata.

It does not store executable handler code or an unrestricted user-defined schema.

The static registry provides the authoritative descriptor schema, semantics and validation. On Project creation, Core Domain seeds the built-in Object Type rows required by the enabled application catalog. Unknown stored type keys are not silently mapped to another type.

### 4.3 Object ID allocation

- Allocation namespace remains Project + stable Object Type identity, as recommended by P2-09-04 V0.2.
- Each Object Type declares whether it allocates formal user-visible IDs.
- Non-allocatable support types may exist for internal/domain purposes only if their identity lifecycle is explicit; do not allocate meaningless visible IDs by default.
- The allocation formatter creates an immutable `allocated_id_value` when the reservation is created.
- Changing display name, abbreviation or future allocation formatting does not rewrite previously allocated values.
- The exact human-facing ID grammar remains a separate bounded UX/domain decision; the Object Type descriptor declares allowed format parameters and compatibility, not an assumption that all types use numeric sequences.
- Reserved/Bound/Inserted/Abandoned semantics and permanent consumption remain governed by P2-09-04.

## 5. Definition, runtime state and Initial State

These are separate data domains and must not be collapsed into one generic attribute dictionary.

### 5.1 Object definition attributes

Definition attributes describe the current shared/base definition of the Object identity.

Examples may include canonical name, semantic category details, stable classification and base characteristics, where the Object Type declares them.

- A Project-shared Object has one current Project-level definition.
- Definition edits use the Shared Object definition Domain Service, not a Map/Branch post-zero Command.
- Under the accepted current-definition model, a definition edit applies retroactively to historical reconstruction for every referencing Map/Branch.
- Definition edits invalidate all affected Checkpoints according to P2-09-16.
- A Map-local Object definition is scoped to its owner Map.
- Definition schema and immutable Asset references are versioned and hash-validated; no replayable Object Definition Revision Chain is introduced in V1.

### 5.2 Runtime attributes

Runtime attributes describe the Object's state in a particular Map/Branch History and Historical Time.

Examples may include:
- current availability or operational condition;
- a historically changing name/title where the domain permits;
- ownership/control or other values better modeled as explicit relationships;
- condition/state fields changed by author Commands or Event Effects.

Rules:
- Runtime attribute changes are History-local and reconstructed from Commands/Events.
- A Command changing a runtime attribute in one Map must not alter the same Shared Object's runtime state in another Map.
- A runtime attribute must not be written into `core_object_definition` merely because the Object is shared.
- If a value represents a relationship, model it as a typed relationship rather than a duplicate scalar attribute.
- The descriptor declares whether an attribute is mutable, immutable, derived, presentation-only or relationship-backed.
- V1 has no generic arbitrary JSON Patch. A Command may change only a declared runtime attribute with a typed value and deterministic semantics.

### 5.3 Initial State attributes

Initial State attributes define the starting value at the Map's time-zero boundary.

- They are stored through the Map Initial State baseline-authoring path.
- They are not post-zero Commands or Events.
- They may include Map-specific placement, initial runtime attributes and initial relationships.
- Editing the baseline invalidates the Map's affected Checkpoints and descendant Branch reconstructions while preserving later Commands/Events for revalidation.
- A Project-shared Object's common definition remains separate from Map-specific initial membership and placement.

## 6. Attribute schema contract

Every attribute declaration must include:

| Property | Requirement |
|---|---|
| Stable key | Non-localized identifier; never inferred from display text |
| Value type | Explicit primitive or typed value object; no unconstrained any/object |
| Cardinality | Required, optional, nullable and collection rules stated separately |
| Domain meaning | Clear semantic definition and units where relevant |
| Scope | Definition, Initial State, History-local runtime, derived, relationship-backed or presentation-only |
| Mutability | Immutable, definition-editable, baseline-editable or Command-mutable |
| Validation | Range, enum, cross-field and Object Type-specific invariants |
| Defaulting | Explicit default source and deterministic behavior; no UI-only implicit default |
| Serialization | Canonical representation and schema version |
| Compatibility | Migration/handler behavior when schema or semantics change |

### 6.1 Supported V1 value categories

Recommended minimal value categories:
- Boolean;
- bounded integer;
- finite decimal/real with declared units and precision policy;
- stable enum code;
- text with explicit normalization/length constraints;
- stable Object reference;
- immutable Asset reference;
- canonical Historical Time interval where the domain field genuinely represents time;
- bounded typed arrays/records whose nested schema is declared.

Avoid:
- arbitrary nested JSON blobs;
- untyped dictionaries;
- UI widget state;
- renderer colors/styles as world facts unless a specialist domain contract explicitly makes them semantic;
- machine-local DateTime or locale-dependent values;
- raw geometry arrays inside ordinary semantic attribute values when geometry belongs in the Geometry Component model.

### 6.2 Derived and relationship-backed values

A derived attribute is computed from authoritative definition, Initial State, Commands, Events or explicit relationships. It is not independently editable and is not persisted as a second authority.

A relationship-backed value must resolve through the relationship descriptor/Domain Service. Do not store both an authoritative relationship and a separate scalar field that can contradict it.

## 7. Geometry capability contract

Object Types declare geometry capability rather than each defining a separate geometry engine.

A descriptor must declare:
- whether geometry is prohibited, optional or required;
- allowed geometry families (point, line/curve, area and supported multipart forms);
- allowed coordinate reference kind/identity;
- allowed geometry roles (for example centerline, footprint, boundary, ridge, entrance, floor plan);
- component cardinality and whether multiple components are meaningful;
- dimensionality supported by the type;
- required topology/validity checks;
- allowed immutable Asset formats and geometry schema versions.

Rules:
- One semantic Object may have multiple Geometry Components.
- A Geometry Component is not automatically a semantic Object.
- Geometry split/merge does not implicitly create/delete/merge Object identities.
- A formal component adjustment/refinement Command is History-local runtime geometry change; it does not rewrite the Project-level Shared Object definition.
- Changing a Project-level Shared Object base geometry, if permitted by its descriptor, uses the separate Shared Object definition service and applies according to the accepted current-definition semantics.
- Derived projection, LOD, hit-test geometry, render meshes and temporary editor geometry are not formal geometry attributes.

## 8. Relationship capability contract

Object Type descriptors declare allowed relationship participation, but the registry does not define one universal relationship ontology.

Each registered relationship descriptor defines:
- stable relationship type ID;
- source and target Object Type constraints;
- directionality and self-reference policy;
- cardinality and uniqueness rules;
- whether it is structural/Initial State or time-varying/History-local;
- typed relationship attributes;
- cycle/acyclicity rules where required;
- establishment/termination semantics;
- deterministic effect and validation rules.

Spatial coincidence, adjacency candidates, snapping and geometric intersection may inform a proposal but cannot establish a formal relationship without explicit accepted domain intent.

## 9. Identity lifecycle versus runtime availability

These concepts must remain distinct:

- `core_object.lifecycle_status` describes the Project-level identity registry lifecycle: draft, active, abandoned or retired.
- Map/Branch runtime availability is reconstructed from the selected History and may differ across Maps for the same Project-shared Object.
- A post-zero `core.object.deactivate` Command changes only runtime availability in the selected History.
- It must not set the Project-level identity row to retired or change another Map's runtime state.
- Identity retirement is a separate Core Domain lifecycle operation with restrictive preconditions and is not the normal meaning of a post-zero in-world removal.
- A retired identity is not physically deleted while reservation/history evidence remains.

This distinction is required for Shared Object identity/base definition to coexist with independent Map/Branch runtime histories.

## 10. Versioning and compatibility

The Object Type registry must distinguish:
1. stable Object Type identity (`type_key`);
2. descriptor schema version (structure and validation contract);
3. execution-semantics compatibility identifier (behavior that affects deterministic state interpretation);
4. Project-configurable display/allocation format version (presentation/allocation configuration).

Rules:
- Persist the descriptor schema version and semantics identifier used when an Object identity is bound.
- Existing Objects continue to resolve using their pinned compatible descriptor semantics.
- A display-name or help-text change does not require a semantic version.
- An attribute schema change that preserves meaning may use an explicit compatible migration.
- A change in attribute meaning, geometry interpretation, relationship behavior or deterministic validation requires a new semantics identifier.
- Historical handlers must remain available for as long as persisted Objects/Commands require them, or a controlled migration must prove semantic preservation.
- Do not silently reinterpret old Objects with the newest descriptor.
- This is not an Object Version or Object Revision Chain. It is a compatibility pin for the Object Type contract used to interpret a stable identity.

## 11. Physical schema impact

The current P2-09-11 V0.9 schema has:
- `core_object_type` for Project-scoped Object Type identity/configuration;
- `core_object` for stable identity and lifecycle;
- `core_object_definition` for the current definition payload/schema/hash;
- `core_object_id_allocator` and `core_object_id_reservation` for allocation;
- `core_map_initial_object` for Map-specific time-zero membership/initial facts;
- typed Command/Object/Asset links where enabled by P2-09-15/P2-09-17.

Implemented in P2-09-11 V0.9:
- `core_object` now includes `object_type_schema_version` and `object_type_semantics_id`, pinned when the reservation becomes Bound;
- `core_object_type.type_key` remains the stable Project-level identity;
- display/allocation configuration version remains separate from semantic descriptor version;
- reconstruction input fingerprints include the pinned Object Type semantics identity where it affects interpretation.

Do not add a universal `core_object_attribute` table or a separate Object Type JSON schema authority in V1. Definition and baseline payloads remain typed, versioned payloads validated by Core Domain descriptors; runtime values are derived from authoritative Commands/Events.

The exact first built-in Object Type seed set and provider-tested migration remain open; the schema intent is represented in P2-09-11 V0.9 and must be validated by the development team's migration/provider proof.

## 12. Initial built-in type strategy

V1 should begin with a small built-in set required by the product's actual first authoring workflows. The examples below are candidate semantic families, not a frozen taxonomy:

- geographic/natural features: mountain range, river, lake, terrain region;
- human-made places/infrastructure: settlement, road, building;
- spatial/interior structures: floor, room, cave system;
- political/cultural geography: political region;
- reference/annotation entities: point of interest.

Do not create a separate type merely because two Objects have different symbols or colors. Do not combine semantically different Objects solely because they share one geometry family.

Specialist types may extend a common capability contract, but each must declare actual domain invariants. A generic `Feature` type with arbitrary fields is not the default fallback for every unmodeled concept.

## 13. Verification scenarios

| ID | Scenario | Expected result |
|---|---|---|
| OT-01 | Unknown type key loaded from Project database | Explicit unsupported-type diagnostic; no silent remapping |
| OT-02 | Display name changes | Stable type key, Object IDs and historical meaning unchanged |
| OT-03 | Type allocation format changes | Existing allocated_id_value values unchanged |
| OT-04 | New reservation for non-allocatable type | Rejected |
| OT-05 | Bound identity references wrong Object Type | Rejected atomically |
| OT-06 | Object Type schema version unsupported | Object cannot be edited/reconstructed silently; compatibility diagnostic |
| OT-07 | Attribute key not declared by Object Type | Rejected |
| OT-08 | Definition attribute changed through History Command | Rejected; use Shared Object definition service |
| OT-09 | Runtime attribute changed in one Map | Other Maps' runtime states unchanged |
| OT-10 | Initial State attribute changed | Baseline updated; affected Branches/Checkpoints invalidated, later Commands retained for revalidation |
| OT-11 | Presentation-only property changed | No formal Command/State Change |
| OT-12 | Geometry component split | No semantic Object identity allocation |
| OT-13 | Geometry family violates Object Type | Rejected by Core Domain |
| OT-14 | Relationship endpoint type is incompatible | Rejected |
| OT-15 | Object deactivated in one Map | Shared identity remains active in another Map |
| OT-16 | Object Type semantic handler upgraded | Existing Object pinned to old compatible semantics or explicit controlled migration required |
| OT-17 | Type deprecated | Existing Objects remain readable/replayable; new allocation may be disabled |
| OT-18 | Arbitrary user-defined executable Object Type submitted in V1 | Rejected/not supported |

## 14. Remaining design gates

1. Select the minimum built-in Object Type seed set required by the first complete Map Editor workflows.
2. Define the first concrete type descriptors and typed attribute sets (start with PointOfInterest, TerrainRegion, River, Lake, Settlement and Road only if their domain contracts are ready).
3. Finalize the Object Type allocation-format editing rules and display grammar without rewriting historical allocated IDs.
4. Add pinned Object Type descriptor schema/semantics identifiers to the physical schema and Checkpoint fingerprint contract.
5. Define specialist relationship descriptors only as their domain semantics become concrete.
6. Coordinate Geometry Component role/identity design with the P2-09-19 V0.2 Geometry Component contract.

## 15. Status

P2-09-18 V0.1 establishes the recommended V1 Object Type descriptor model and its boundaries with identity allocation, current definition, Initial State, runtime history, geometry, relationships and presentation.

It intentionally does not freeze an exhaustive Object Type taxonomy or every specialist attribute. It is a design contract, not executable schema, implementation code or implementation authorization.
