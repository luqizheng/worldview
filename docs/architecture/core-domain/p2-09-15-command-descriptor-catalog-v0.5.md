# World View P2-09-15 — Core Domain Command Descriptor Catalog V0.5

- Version: 0.5
- Date: 2026-11-21
- Status: Object-presence-lifecycle-reconciled recommended V1 catalog baseline — descriptor families and scope decisions; payload schemas and handler algorithms remain subject to bounded follow-up design
- Supersedes: P2-09-15 V0.4; V0.4 is retained as history
- Depends on: P2-09-03 V0.2; P2-09-05 V0.3; P2-09-08 V0.2; P2-09-11 V0.10; P2-09-14 V0.1; P2-09-17 V0.4; P2-09-18 V0.2; P2-09-19 V0.2; P2-01-D01 D01-01~16 and D01-50~64; ME-01-05 V0.1; Spatial Presentation & Geometry Interaction Contract V0.1; P2-09-20 V0.2

## 1. Purpose and authority

This document turns the existing Command/State Change and Object/Geometry architecture into a bounded, reviewable V1 Command Descriptor Catalog.

The catalog is owned by Core Domain. It defines the formal authoring operations that can enter Map/Branch History. It does not define UI button names, generic database CRUD endpoints, editor gestures, AI tool names, or a plugin extension system.

The catalog is a recommended design baseline, not a frozen domain taxonomy or implementation authorization. The Command IDs below are proposed stable identifiers; they become reserved only when adopted in a reviewed Core Domain release.

## 2. Non-negotiable command model

1. A Command records the author's intended domain operation. It does not store the resulting World State.
2. A Command belongs to exactly one History and is interpreted at canonical Map-relative Historical Time.
3. A Command can produce zero, one, or many derived State Changes. State Changes are not independently authored in V1.
4. Command payloads are typed and validated by a statically registered Core Domain descriptor.
5. Persisted Commands retain descriptor schema version and exact execution-semantics compatibility identifier.
6. Editing, deleting or inserting a Command edits authoring history under Core Domain rules; no Command Revision Chain is introduced.
7. A Branch-owned Command must satisfy the strict post-fork boundary rule.
8. Deterministic execution may use only captured inputs, applicable history, explicit ordering and immutable referenced assets. It must not rely on wall-clock time, UI state, network lookups or unseeded randomness.
9. Geometry Operations and Object Operations remain separate. Geometry changes never infer semantic Object creation, deletion, split, merge or relationship changes.
10. An Event is not a Command. Event narration and Event-driven effects retain their separate Core Domain contracts.

## 3. Catalog scope and classification

The catalog distinguishes four operation classes:

| Class | Persistence path | Historical meaning |
|---|---|---|
| Initial State authoring | Map Initial State / baseline Domain Services | Establishes the Map's time-zero starting facts; not a post-zero Command |
| Formal historical Command | Typed Core Domain Command in a History | Author-intended operation taking effect after time zero |
| Event / narrative record | Core Domain Event | Historical/story record; may be associated with effects but is not itself a generic Command |
| Presentation / editor interaction | Editor or presentation state | View, styling, selection, temporary geometry and interaction state; not World History |

A single user workflow may cross more than one class, but each persisted fact must use its correct authority.

**History-locality rule:** every post-zero Command in this catalog changes runtime state only in the selected Map/Branch History. It must not mutate the Project-level Shared Object current definition or globally change `core_object.lifecycle_status`. Project-level Shared Object definition edits use the separate Shared Object definition Domain Service and follow the accepted retroactive-current-definition rule. A Project-shared Object may therefore have different runtime states in different Maps.

## 4. Recommended V1 Command catalog

### 4.1 Core Object lifecycle and semantic state

| Proposed Command Type ID | Display intent | V1 recommendation | Target and time | Primary references | State Change behavior |
|---|---|---|---|---|---|
| `core.object.create` | Create a semantic Object in the world | Include, with strict preconditions | One Map/Branch History; post-zero only | Object Type, Bound reservation/Object identity and optional immutable geometry/Assets | Usually one creation/availability transition plus zero or more descriptor-derived initialization effects |
| `core.object.deactivate` | Deactivate an Object in this Map/Branch History | Include as a History-local runtime operation, not identity retirement | One History; post-zero only | Existing Object identity and optional reason/evidence | One History-local availability transition; identity and provenance remain |
| `core.object.activate` | Reactivate a previously deactivated Object in this Map/Branch History | Include; only for an Object previously present and deactivated in this History | One History; post-zero only | Existing Object identity and prior History-local presence | Restores runtime availability in this History without creating a new identity |
| `core.object.attribute.change` | Change a typed semantic attribute | Include only for Object-Type-declared mutable attributes | One History; post-zero only | One Object identity; typed before-condition/expected value where required | Zero or one semantic attribute transition; reject unknown fields and untyped arbitrary patches |

Rules:
- Object identity and Object ID allocation remain Core Domain responsibilities. Creation must use the existing reservation lifecycle; the editor or AI cannot invent an Object ID.
- A reservation becomes Bound before insertion as required by P2-09-04. The transition to Inserted and formal creation Command application must be atomic under the Core Domain/Application transaction contract.
- Initial membership and initial attributes at time zero are authored through the Initial State baseline path, not by creating a time-zero Command.
- Deactivation changes only runtime availability in the selected History. It does not mutate the Project-level `core_object.lifecycle_status`, Shared Object current definition, identity row, reservation or historical provenance.
- Activation is permitted only when the same Object was previously present and deactivated in the selected History. It restores the last valid runtime state; it does not create a new identity or copy state from another Map.
- Commands targeting an inactive Object are rejected unless a specific descriptor explicitly permits dormant-object handling.
- Relationship Type descriptors define whether an endpoint may be inactive. Deactivation must not silently cascade-end relationships; if an active relationship requires active endpoints, the deactivation is rejected until the relationship is explicitly ended. A relationship that permits dormant endpoints retains its own state and is revalidated on activation.
- Attribute changes are not a generic JSON patch. Each Object Type declares the allowed attribute keys, value types, invariants and deterministic effect semantics. If an attribute is not historically mutable, it is not exposed through this descriptor.

### 4.2 Geometry Operations

| Proposed Command Type ID | Display intent | V1 recommendation | Target and time | Primary references | State Change behavior |
|---|---|---|---|---|---|
| `core.geometry.adjust_component` | Adjust vertices/control points of a formal Geometry Component | Include as a bounded geometry-edit intent | One History; post-zero only | Object identity, component identity, typed geometry input or immutable geometry asset | Replaces the applicable component state at the Command's effective time |
| `core.geometry.refine_component` | Adopt a refined geometry in place of an approximate/coarse component | Include only when source/target role and provenance are explicit | One History; post-zero only | Object/component, source fingerprint, refined immutable geometry asset, method/provenance | Changes authoritative geometry reference; must not change Object identity |
| `core.geometry.add_component` | Add a new formal Geometry Component to an existing Object | Include, subject to Object Type role/cardinality validation | One History; post-zero only | Object identity, declared role, new component identity and immutable geometry Asset | Adds component membership only; does not create a semantic Object |
| `core.geometry.remove_component` | Remove one Geometry Component from an Object's runtime state | Include, subject to minimum-geometry and reference validation | One History; post-zero only | Object identity and existing component identity | Ends component membership in this History; does not delete its immutable Asset or semantic Object |
| `core.geometry.split_component` | Split one geometry component into multiple components | Defer from the minimum V1 catalog pending component identity and topology rules | One History; post-zero only if later adopted | Object/component and deterministic split result | Geometry component cardinality changes only; never creates semantic Objects |
| `core.geometry.merge_components` | Merge compatible components of the same Object | Defer from the minimum V1 catalog pending component identity and topology rules | One History; post-zero only if later adopted | Object and source component identities | Geometry component cardinality changes only; never merges semantic Objects |

Rules:
- The descriptor must identify the semantic Object and exact Geometry Component(s). A free-floating geometry edit is not a formal Command.
- A temporary drag, snapping suggestion, hit-test result, projected path, screen-space curve approximation or renderer mesh is not a Command payload.
- A snap is a proposal. Core Domain validates the accepted formal geometry and any explicitly requested topology/relationship effect.
- Geometry split/merge affects geometry decomposition only. It must not allocate, retire, merge or split semantic Object identities.
- Geometry refinement must retain source provenance/fingerprint and the adopted geometry asset. Derived LOD, clipping, simplification and render approximations are not formal historical changes unless explicitly adopted as authoritative geometry.
- Add Component allocates a new opaque component ID through Core Domain and validates the Object Type's allowed role and component cardinality. Remove Component validates that the Object remains valid after removal; component IDs are never reused and immutable Assets remain available while referenced by history.
- Component add/remove changes only the selected History's runtime geometry membership. It never changes semantic Object identity, Project-level Shared Object definition or another Map's runtime state.
- Geometry split/merge remains deferred; it must not be emulated by chaining add/remove in a way that loses source/result component lineage.
- Do not create a generic `replace_geometry` database CRUD Command. The accepted intent must state what domain operation is occurring and validate its semantics.

### 4.3 Spatial and semantic relationships

| Proposed Command Type ID | Display intent | V1 recommendation | Target and time | Primary references | State Change behavior |
|---|---|---|---|---|---|
| `core.relationship.establish` | Establish a typed relationship between formal Objects | Include for relationship kinds explicitly registered by the relevant Object Types | One History; post-zero only | Source/target Object identities, relationship type and typed attributes | One relationship activation transition |
| `core.relationship.end` | End a historically active relationship | Include for the same registered relationship kinds | One History; post-zero only | Existing relationship identity or stable endpoint/type key | One relationship termination transition |

Rules:
- Relationship type, directionality, cardinality, allowed endpoint types, temporal behavior and invariants are defined by Core Domain descriptors/Object Type contracts.
- Proximity, coordinate coincidence, intersection, snapping or a geometry join does not automatically establish a semantic/topological relationship.
- Initial structural relationships at time zero belong in Initial State.
- A time-varying relationship such as political control, road connectivity or river flow may be changed through a registered historical relationship Command only when its domain contract is defined.
- Do not create a universal arbitrary relationship JSON editor as a substitute for domain validation.

### 4.4 Calendar reform

| Proposed Command Type ID | Display intent | V1 recommendation | Target and time | Primary references | State Change behavior |
|---|---|---|---|---|---|
| `core.calendar.reform` | Establish or change a Calendar rule set effective from a historical boundary | Include as a specialized Command only after P2-09-14 validation gates close | One Map/Branch History; effective Historical Time | Calendar identity, validated Calendar Configuration payload and source Command association | No World State State Change; atomically manages the separate Calendar Configuration |

Rules:
- This Command records the historical reform action; Calendar Configuration stores the effective interpretation rules.
- Creation, retiming and deletion of the Command-managed Configuration occur through one Core Domain authoring transaction.
- Calendar reform must not rewrite already normalized Command/Event canonical time intervals, reorder history or trigger World State reconstruction.
- Gap/fallback behavior, non-Gregorian conversion fixtures and Branch inheritance remain open in P2-09-14. Therefore the descriptor is a catalog candidate but must not be enabled for persisted V1 authoring until those validation gates are closed.

## 5. Explicitly deferred Command families

The following are meaningful domain operations, but their required domain semantics are not sufficiently specified to register as V1 persisted Command descriptors yet.

| Candidate family | Reason for deferral | Required prerequisite |
|---|---|---|
| `core.object.semantic_split` | Semantic split creates multiple identities and requires explicit continuity/provenance/attribute/relationship allocation rules | Object Type-specific split contract, reservation cardinality, source identity lifecycle and relationship remapping |
| `core.object.semantic_merge` | Semantic merge changes identity cardinality and cannot be inferred from geometry merge | Object Type-specific merge contract, surviving identity rule, retired identity provenance and relationship reconciliation |
| `core.unit.move` | Movement is continuous and interacts with routes, terrain, obstacles, pause/resume, dependencies and timeline animation | Unit/Group domain model, route representation, execution phases, failure/repair and deterministic movement algorithm |
| `core.unit.wait` / `core.unit.travel` | These are possible author-intent operations, not generic movement implementation steps | Explicit Unit behavior and duration semantics; author-authored waiting must remain distinct from system-invented waiting |
| Terrain/erosion/hydrology evolution | No bounded deterministic V1 domain contract or simulation scope is established | Explicitly approved domain scope, captured inputs, deterministic algorithm/version and performance bounds |
| Arbitrary bulk transform / map-wide rewrite | Too broad to validate and replay safely as one generic Command | A specific author-intent operation with bounded targets, deterministic parameters and reviewable impact |
| Automatic geometry-to-Object extraction | Candidate generation is not formal identity creation | Candidate/Validation workflow and explicit human adoption through Object Operations |

Deferred does not mean permanently excluded. A family enters the catalog only after its domain contract, payload, replay semantics and failure behavior are specified.

## 6. Operations that are not Commands

The following must not be persisted as generic formal Commands:

| Operation | Correct authority/path | Reason |
|---|---|---|
| Create Map / Project | Project and Map lifecycle Domain Services | Container creation, not an in-world historical event |
| Create Branch / choose fork boundary | Branch Domain Service | Establishes History inheritance, not a Command inside that History |
| Edit time-zero membership, placement, attributes or initial relationships | Map Initial State authoring path | Defines the starting baseline, not post-zero history |
| Edit Shared Object current definition | Shared Object definition Domain Service | Project-level current definition with retroactive applicability under the accepted model; not a Map-local post-zero Command |
| Add/edit/delete Event narration | Event Domain Service | Event is a separate historical/narrative record, not a generic Command |
| Change layer visibility, symbol, label styling, color, line width, view mode or LOD | Presentation settings/services | Presentation does not change World State by default |
| Select, hover, drag preview, snap candidate, temporary control point, viewport pan/zoom | Editor interaction state | Ephemeral interaction state, not formal history |
| Generate/rebuild derived geometry, spatial indexes, hit-test data, render meshes or checkpoint payloads | Derived-data services | Rebuildable output, not authoring authority |
| AI Candidate generation or rejection | AI Assistant Candidate/Validation lifecycle | Staging evidence is not formal Core Domain history until explicitly adopted |

If a presentation field is later declared a true world-domain attribute, it must be introduced through an explicit Object Type/Domain contract rather than silently turning every style edit into history.

## 7. Common descriptor contract

Every enabled descriptor must expose the following metadata and validation behavior, as required by P2-09-08:

1. Stable Command Type ID; display name is not identity.
2. Descriptor schema version.
3. Exact execution-semantics/handler compatibility identifier.
4. Human-readable intent and scope.
5. Supported History target (Map root History and/or Branch History).
6. Typed payload schema, required/optional fields and canonical serialization.
7. Historical Time requirements, precision behavior and continuous-duration policy.
8. Object, relationship and immutable Asset reference declarations.
9. Preconditions and Object Type/relationship invariants.
10. Explicit and derived dependency/order declarations.
11. Deterministic execution inputs and algorithm compatibility requirements.
12. State Change cardinality and generation semantics (zero/one/many).
13. Command edit/delete/insert and reconstruction impact behavior.
14. Failure behavior and diagnostic information; no automatic author-intent rewrite.
15. Checkpoint fingerprint contribution declaration.
16. AI proposal capability metadata, including whether the descriptor is enabled for AI proposal and whether human confirmation is mandatory.

The descriptor validates domain meaning; a JSON schema alone is insufficient. A payload that is structurally valid but violates Object Type, History, ownership, time, topology or authorization invariants must be rejected before persistence.

## 8. V1 registration and compatibility policy

- V1 uses a statically registered Core Domain catalog compiled with the application. No runtime plugin marketplace, user-defined executable Command handlers or database-driven generic workflow engine.
- AI Assistant obtains descriptors from Core Domain at runtime and may submit only descriptors currently enabled for the target Map/History.
- Persist the stable type ID, descriptor schema version and execution semantics identifier on every Command.
- A display text or documentation-only change does not require a new semantic identifier.
- A payload schema change that preserves meaning may use an explicit compatible payload migration. A change in execution meaning requires a new semantics identifier.
- Historical reconstruction must execute the recorded semantics, not whichever handler happens to be newest.
- If a required historical handler is unavailable, reconstruction must report an unsupported-history condition; it must not silently substitute a newer handler.
- Descriptor manifest persistence is not required as a new database table in V1. The compiled catalog and persisted identifiers are sufficient for the initial design; a reproducible release manifest may be retained as application build metadata.
- Do not introduce a generic Command extension framework or per-Command database table. Keep the persisted Command envelope typed by stable ID and schema version, with descriptor-specific payload validation in Core Domain.

## 9. Persistence and schema impact

The catalog does not require a new Core Domain table.

- `core_command` remains the authoritative Command envelope and stores the stable type ID, descriptor schema version, execution semantics identifier, typed payload, canonical Historical Time and applicable duration/end-time data.
- `core_command_object_ref` is used only for formal Object references required by the descriptor; it must not become an unrestricted generic graph.
- `core_command_asset` is used only for immutable Asset references required to replay the descriptor.
- `core_command_event` remains an optional explanatory/association relation; it does not imply one-to-one Command/Event/State Change cardinality.
- Explicit ordering constraints remain in `core_command_order_constraint`; derived implicit dependencies are not persisted as authoritative relations.
- V1 State Changes remain deterministically generated in memory. Do not add a State Change table merely to accommodate the catalog.
- Calendar Configuration remains separately owned by Core Domain and is not a World State State Change.
- Conditional typed link tables in P2-09-11 must be enabled only when at least one registered descriptor needs that reference class and the physical FK/lifecycle rules are justified.

The physical payload envelope and exact serialized geometry format remain open implementation/schema details. This catalog does not authorize arbitrary JSON blobs without descriptor-specific canonical validation.

## 10. Verification matrix

| ID | Required scenario | Expected result |
|---|---|---|
| CMD-01 | Submit unknown or disabled type ID | Rejected before persistence |
| CMD-02 | Payload schema version mismatch | Rejected or passed through an explicit compatible migration |
| CMD-03 | Persisted Command replay | Uses recorded descriptor and handler semantics identifiers |
| CMD-04 | Create Object with no valid Bound reservation | Rejected; no identity invented |
| CMD-05 | Create Object at time zero through Command path | Rejected; use Initial State authoring |
| CMD-06 | Retire Object | Preserves identity/provenance and creates deterministic lifecycle effect |
| CMD-07 | Change undeclared or immutable Object attribute | Rejected by Object Type descriptor |
| CMD-08 | Geometry edit changes component shape | Does not change semantic Object identity |
| CMD-09 | Geometry split/merge | Does not allocate, merge or retire semantic Objects |
| CMD-10 | Snap candidate without accepted Core Domain operation | No formal geometry or topology mutation |
| CMD-11 | Establish relationship from coordinate coincidence alone | Rejected unless a typed relationship is explicitly proposed and validated |
| CMD-12 | Presentation style/visibility edit | No formal Command or World State reconstruction |
| CMD-13 | Calendar reform changes display rules | Canonical Command intervals and World State remain unchanged |
| CMD-14 | Unit movement descriptor before Unit contract exists | Not registered/enabled |
| CMD-15 | Branch Command at or before parent_at | Rejected |
| CMD-16 | Algorithm upgrade changes old Command meaning | Requires new semantics identifier or controlled migration; no silent reinterpretation |
| CMD-17 | AI proposes a descriptor not enabled for the target History | Rejected by Core Domain |
| CMD-18 | Command generates multiple intermediate results | Deterministic State Changes; animation frames remain presentation-only |

## 11. Remaining bounded design work

Before the catalog can be called frozen or executable:
1. Define concrete typed payload schemas for the enabled V1 descriptors, including canonical serialization and field-level invariants.
2. Specify Object Type registration and mutable-attribute declarations needed by `core.object.create` and `core.object.attribute.change`.
3. Specify Geometry Component identity, role, edit scope and immutable asset payload format for geometry descriptors.
4. Define relationship identity and temporal lifecycle rules for the registered relationship types.
5. Close P2-09-14 Calendar validation gates before enabling `core.calendar.reform`.
6. Define Unit/Group, route and deterministic movement semantics before adding movement Commands.
7. Reconcile descriptor compatibility/handler retention with release and database migration policy.
8. Update P2-09-11 conditional typed-link selection only after the descriptor reference matrix is reviewed.

## 12. Status

P2-09-15 V0.1 establishes a bounded recommended V1 Command catalog and, equally importantly, defines which editor/domain operations are not Commands. It does not claim that all candidate descriptors are already frozen or that their payloads/handlers are implemented.

The minimum V1 direction is deliberately small: Object creation/activation/deactivation/typed runtime-attribute change, bounded Geometry Component adjustment/refinement/addition/removal, explicitly registered relationship establishment/termination, and Calendar reform only after its validation gates close. Semantic split/merge and Unit movement remain deferred until their domain contracts are mature.

This document is a design artifact only. It contains no implementation code, executable DDL, test execution claim or implementation authorization.
