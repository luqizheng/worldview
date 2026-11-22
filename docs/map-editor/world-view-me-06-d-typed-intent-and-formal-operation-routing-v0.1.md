# World View — ME-06-D Typed Intent & Formal Operation Routing

- Version: 0.1
- Date: 2026-11-21
- Status: Typed Intent and formal-operation routing working baseline; concrete Core Domain adoption, payload schemas and runtime handlers remain separate gates
- Depends on: ME-06-00/A/B/C V0.1; ME-01-05/06/07; ME-05-02/03/04/05/06; Core Domain P2-09-08 V0.2 and P2-09-15 V0.1; specialist ME-02/03/04 contracts
- Authority: Editor/Application integration contract. This document does not register Command descriptors, authorize new Object/Relationship Types, freeze APIs/DTOs/DDL, or claim handlers are implemented.

## 1. Executive disposition

ME-06-D defines the boundary between a creator action and a formal domain operation.

Adopt this chain:

**Gesture / Inspector action → Typed Intent → preflight & capability resolution → optional Candidate/authorization → formal write-path selection → Core Domain Domain Service → authoritative result → scene/read-model reconciliation.**

A Typed Intent is a validated, typed request describing what the user means to do. It is **not** a Core Domain Command, persisted State Change, database patch, authorization grant or guarantee that the operation is supported.

Core Domain remains the sole authority for Object identity, Object ID reservation/allocation, descriptor registration, formal geometry/relationship references, history and persisted Commands. Editor, Map Engine and AI Assistant never bypass Domain Services.

The four formal write paths remain distinct:
1. Project-level Shared Object current-definition change.
2. Map Initial State baseline authoring.
3. Post-zero Map/Branch History Command.
4. Map Source Field Baseline publication.

Event/narrative authoring remains its own Core Domain Event path; it is not a fifth generic geometry write path and must not be silently substituted for a Command.

## 2. Goals and non-goals

### Goals
- Give manual tools, Inspector actions and AI-proposed operations a common typed application boundary.
- Separate user-facing tool keys and intent names from Core Domain Command Type IDs.
- Resolve the exact formal write path from target ownership and temporal intent, never from UI mode alone.
- Validate descriptor capability, context, target identity, source revisions, payload and authorization before apply.
- Preserve idempotency and authoritative reconciliation.
- Make unsupported, blocked, stale and preview-only outcomes explicit.

### Non-goals
- Generic CRUD/JSON Patch endpoint.
- Editor-owned Command registry or duplicate Object registry.
- Dynamic plugin discovery or user-defined executable descriptors in V1.
- A second persistence authority in Editor or AI staging.
- Automatic conversion of every gesture into a historical Command.
- Inventing descriptors for proposed Building/Cave/FunctionalArea/RoadEndpoint capabilities.
- Freezing final wire DTOs, SQL schema, class names or method signatures.

## 3. Vocabulary and authority

| Term | Meaning | Authority |
|---|---|---|
| Tool Definition | Built-in interaction affordance, gesture and capability declaration | Shared Editor Tool Catalog |
| Typed Intent | Immutable typed request representing a user's intended semantic operation | Application integration contract |
| Candidate | Reviewable proposal package with content/version/hash and evidence | AI Assistant / Editor staging |
| Authorization | Permission bound to exact proposal/operation hash, target context, scope and risk | Authorization service / human policy |
| Formal Operation | Application request resolved to a supported Domain Service and write path | Application Services |
| Command Descriptor | Registered semantic Command contract, schema and execution semantics | Core Domain |
| Command | Persisted author-intended historical operation in exactly one History | Core Domain |
| State Change | Deterministic result derived from Command execution/reconstruction | Core Domain |
| Asset Publication | Immutable payload publication and authoritative reference adoption | Asset Store + owning Domain Service |

Tool Key is not a Command Type ID. Intent kind is not a descriptor. Candidate ID is not an Object ID. Renderer node, tile index or local draft ID is never a formal target identity.

## 4. Canonical Typed Intent envelope

The following is a conceptual envelope, not a frozen serialization schema. Required fields vary by intent family.

| Field | Meaning / rule |
|---|---|
| intent_id | Unique identity for one immutable intent instance |
| intent_kind + intent_schema_version | Stable application intent family/version; distinct from Command Type ID |
| origin | Manual tool, Inspector, AI Candidate, guided initialization or other registered source |
| tool_key + tool_version | Optional source tool identity; never a formal domain type |
| target_context | Project, Map, optional Branch/Historical Time, Working Scope and intended owner scope |
| target_refs[] | Stable Object IDs and Geometry Component IDs, with expected revisions |
| operation_payload | Typed operation-specific data, not arbitrary key/value patch |
| source_refs[] | Immutable geometry/field Asset references, Field Dataset/Revision and source fingerprints |
| query_evidence[] | ME-06-C token IDs, request sequences, freshness outcomes and predicate witnesses |
| spatial_context | Spatial-reference IDs, transform revisions, coordinate spaces, precision profile, units and error bounds |
| temporal_intent | Initial State, post-zero effective time/Branch, Shared Definition or Field Baseline intent |
| descriptor_binding | Exact Core Domain descriptor ID/schema/semantics version after resolution; never user-invented |
| validation_evidence[] | Specialist rule outcomes, algorithm versions, source fingerprints, coverage and uncertainty |
| risk_classification | Derived by policy from operation semantics/impact; caller cannot lower it |
| proposal_binding | Candidate ID/version/hash when applicable |
| authorization_binding | Exact authorization evidence if required; binds intent/proposal hash, target context, scope and risk |
| idempotency_key | Stable key for one apply attempt/retry lineage; cannot be reused with changed payload/context |
| created_at | Audit metadata only; never a hidden deterministic Command input |

The envelope is immutable after sealing. Any semantic payload, target, context, source fingerprint or descriptor binding change creates a new intent/proposal version and invalidates dependent authorization.

### 4.1 Typed payload requirements

- Payloads are discriminated by intent kind and schema version.
- Each kind declares exact fields, units, coordinate space, cardinality, nullability and validation.
- Unknown fields and undeclared attributes are rejected.
- Geometry payloads reference staged immutable Assets or typed geometry values; renderer buffers and screen-space paths are invalid.
- Relationship payloads name the registered relationship type, direction, endpoint Object identities and typed attributes.
- Historical operations include explicit canonical time and target History context; no UI wall-clock default.
- Evidence cannot replace apply-time revalidation.

## 5. Intent construction

### Manual tools
The tool handler converts a completed transient Edit Session into a Typed Intent only after semantic completion. It uses the captured ME-06-B context, resolves stable targets, converts geometry through ME-05 coordinate/precision contracts, references staged immutable Assets, attaches current ME-06-C evidence and specialist validation, declares temporal/write-path intent, then seals the payload.

A click, drag, snap, sketch or local undo alone is not a formal operation.

### Inspector actions
Inspector controls use the same boundary. A local display preference remains presentation state. A domain-owned attribute change creates an intent only if the Object Type declares that attribute mutable and a formal operation exists.

### AI-originated operations
AI may propose only supported intent schemas and exact capability metadata. AI cannot invent schema versions, Command Type IDs, Object IDs or relationship IDs; claim an unadopted descriptor is available; decide its own authorization or lower its risk; or write Commands, State Changes, Assets or relationships directly.

## 6. Formal write-path resolution

Resolve the write path from authoritative target ownership and temporal semantics. The Editor cannot choose a path merely because the active mode is Terrain, Urban or Building.

| Path | Appropriate intent | Owner / persistence meaning | Required guard |
|---|---|---|---|
| Project-level Shared Object current definition | Change shared base attributes/geometry explicitly allowed by Object Type | Shared-definition service; retroactive current definition for referencing Maps | Shared identity, expected definition revision/fingerprint, allowed fields/geometry role, P2-09-16 coherence |
| Map Initial State | Add/remove/modify time-zero membership, initial geometry, attributes or structural relationships | Map baseline service; no time-zero historical Command | Map identity, expected baseline revision, Object reservation lifecycle, descriptor/type and baseline invariants |
| Post-zero Map/Branch History | Historical Object lifecycle/attribute/geometry/relationship operation | Core Domain typed Command in exactly one History | Registered descriptor, schema/semantics version, History/Branch boundary, stable targets, deterministic payload, expected source state |
| Map Source Field Baseline | Publish authoritative elevation/land-water/other Field Revision for one Map | Field Dataset/Revision service; retroactive across all Branches and Historical Times in that Map | Map ownership, expected current Field Revision CAS, package validation and immutable Asset publication |
| Event / narrative record (separate) | Record a historical/story Event | Core Domain Event service | Event contract; never a generic substitute for a geometry Command |
| Presentation-only change | Selection, visibility, style, label layout, Canvas view, transient draft | Editor/presentation state | No formal World History mutation |

### 6.1 Time-zero is not a historical Command
An operation establishing the Map's starting facts belongs to Map Initial State. Do not create a Command at time zero to bypass baseline services.

### 6.2 Shared Object is not Map-local history
A Project-level Shared Object definition change modifies the shared current definition. A post-zero change in one Map/Branch is a History Command scoped to that History. Never silently convert one into the other.

### 6.3 Field Revision is not a historical terrain Event
Field publication updates the owning Map's current source baseline and invalidates dependent analysis across that Map's Branches and Historical Times. A historical terrain transformation, if supported, must be an explicit historical domain operation.

## 7. Descriptor resolution and capability gate

Before preflight succeeds, Application Services query the Core Domain-owned static descriptor catalog and resolve:
- exact stable Command Type ID for historical operations;
- descriptor schema and execution-semantics/handler compatibility versions;
- allowed History scope and time requirements;
- target Object Types and relationship endpoint constraints;
- typed payload schema and validation;
- required Asset/source references;
- preconditions, deterministic replay and State Change semantics;
- adoption/implementation availability.

The caller supplies an intent kind, not an arbitrary Command Type ID. Application Services map the intent kind to an exact registered descriptor. If no unique safe mapping exists, return Unsupported or Blocked and persist nothing.

| Capability outcome | Meaning | Apply behavior |
|---|---|---|
| Enabled | Adopted descriptor + owning service/handler + validation and persistence path verified | Continue through full preflight |
| PreviewOnly | Preview/query exists but no formal apply path adopted | Preview only |
| Blocked | Known prerequisite/adoption gap or safety invariant | Return blocker and remediation gate |
| Unsupported | Product/domain does not support operation | No apply |
| Stale | Captured context/source/evidence no longer current | Rebuild intent; old authorization invalid |
| Unverified | Evidence insufficient to claim runtime capability | Fail closed |

Enabled is an evidence-backed runtime state, not a design-document label. ME-06-A remains the capability source; this document does not upgrade its states.

## 8. Preflight and validation order

Application Services perform preflight in this order:

1. Envelope schema, required fields, canonical units and immutable sealing.
2. Exact Project/Map/Branch/Historical Time/Working Scope and temporal meaning.
3. Stable Object/Component identities and expected revisions.
4. Exactly one formal write path; reject ambiguous ownership.
5. Exact adopted descriptor and owning service; reject PreviewOnly/Blocked/Unsupported/Unverified.
6. Immutable Asset hashes, Field Revision, package/tile/coverage metadata and publication status.
7. CRS, transforms, coordinate spaces, precision, tolerances and ME-06-C query freshness.
8. Deterministic specialist rules and their source fingerprints/algorithm versions; preserve True/False/Indeterminate/Unavailable.
9. Canonical time, one History owner, Branch fork boundary, ordering and replay requirements.
10. Risk derived from operation/impact; verify exact authorization if required.
11. Expected revisions/CAS at the authoritative write boundary; prepare idempotent apply.
12. Invoke exactly one owning Domain Service transaction/atomic publication boundary.

A failed preflight has no formal side effect. It may preserve a safe transient draft and diagnostics.

## 9. Candidate review and authorization

Candidate review is not operation application.

- Low-risk, deterministic, directly authored manual edits may proceed without a separate Candidate UI when policy permits.
- AI-originated proposals, high-impact operations, uncertain bulk changes and operations requiring explicit human decision use Candidate review according to risk policy.
- Candidate review accepts/rejects one immutable proposal version/hash; it does not guarantee target/source freshness.
- Authorization, where required, binds exact intent/proposal hash, target context, scope and risk.
- Payload, target, write path, source fingerprint, descriptor version or relevant context changes invalidate authorization.
- Human authorization is never inferred from opening a Candidate, preview interaction or AI self-approval.
- After Candidate acceptance, ME-06-C freshness and Application preflight still run.
- Formal apply is reported only after the owning service confirms success.

ME-06-E defines review UI and partial acceptance. This document defines the operation boundary it calls.

## 10. Asset publication and atomicity

Follow ME-05-05:
1. Stage and validate payload.
2. Write immutable Asset/package.
3. Read back and verify content hash/manifest.
4. Enter the short authoritative transaction.
5. Compare expected owner revision (CAS).
6. Adopt the Asset/Field Revision reference and formal operation atomically.
7. Reconcile; recover or collect orphaned staged Assets under retention policy if commit fails.

The owning service differs by write path. A staged Asset is not formal authority until adopted. Do not update a Core Domain row and then hope file writing succeeds.

Historical geometry Commands reference immutable Assets required for deterministic replay. Map Source Field Baseline publication updates the Map-wide current Field Revision, not Branch-local history.

## 11. Idempotency, concurrency and reconciliation

- Every apply attempt carries an idempotency key bound to immutable intent hash and target context.
- Retrying the same key with identical payload/context returns the prior authoritative outcome or safely resumes reconciliation.
- Reusing a key with changed payload, target, context or descriptor binding is an idempotency conflict.
- Concurrent source changes are detected by expected revision/CAS at the authoritative transaction boundary, not only by UI pre-check.
- A successful domain commit followed by failed scene refresh is PendingReconciliation, not Failed and not safe to resubmit as a new operation.
- Application Services return formal operation identity and affected stable Object/Component refs; Editor refreshes scene generation and resolves selection by stable identity.
- Cancellation before commit may abort staged work. After authoritative commit, cancelling the UI task does not roll back the formal change; reversal uses the relevant formal history mechanism.

## 12. Failure and outcome vocabulary

| Outcome | Formal mutation? | Required behavior |
|---|---|---|
| CompletedLocally | No | Keep local/presentation result; never imply persisted world change |
| PreviewOnly | No | Explain no formal apply capability |
| Blocked | No | Name missing descriptor/adoption/invariant gate |
| Unsupported | No | Explain unsupported operation; no generic CRUD fallback |
| Unverified | No | Fail closed pending implementation evidence |
| Stale | No | Invalidate old evidence/authorization; refresh and rebuild |
| Rejected | No | Show schema/domain/authorization reason; preserve safe draft |
| Cancelled | No, unless commit confirmed | Clear/recover transient state; committed changes remain |
| Failed | No authoritative success | Report failure; retain safe draft/evidence |
| Applied | Yes, service confirmed | Show operation identity and begin reconciliation |
| PendingReconciliation | Yes, commit confirmed | Prevent duplicate apply; reconcile read model/scene |
| Reconciled | Yes | Restore normal interaction with stable refs |

Optimistic rendering or a success toast cannot substitute for authoritative service confirmation.

## 13. Operation routing examples

| Creator action | Intent family | Expected path | V1 disposition |
|---|---|---|---|
| Adjust supported geometry component at historical time | Geometry component adjustment | Post-zero History Command | Only with exact registered descriptor and handler; otherwise Unverified/Blocked |
| Refine approximate component with provenance | Geometry refinement | Post-zero History Command | Requires descriptor, source fingerprint and immutable Asset adoption |
| Create Settlement at Map start | Object creation + baseline geometry | Map Initial State | Requires reservation lifecycle and baseline service; never time-zero Command |
| Create Settlement at later time | Object creation | Post-zero History Command | Requires adopted create descriptor and bound reservation |
| Change Shared Object base definition | Shared definition update | Project-level Shared Object service | Must not create Map-local Command |
| Publish elevation tiles | Field Revision publication | Map Source Field Baseline | Map-wide; package validation and CAS required |
| Draw Road endpoint junction | Road endpoint relationship | Relationship operation in Initial State or History | Blocked until stable endpoint identity/typed payload adopted (ME-03-H P0) |
| Connect Rooms through Door | Interior access relationship | Building/interior owning service and temporal path | Blocked until Building/Space/Opening/Access descriptors/endpoints adopted |
| Add Building or Cave | Object creation/interior structure | Initial State or post-zero path by intended time | Blocked until Core Domain adoption gates close |
| Draw Functional Area | Functional-area semantic intent | Initial State or post-zero path | Proposal-only pending Object Type/descriptor adoption |
| Move renderer symbol/change label placement | Presentation intent | Editor/presentation state | No formal history unless domain-owned attribute explicitly edited |
| Run drainage/climate/placement analysis | Calculation request | Calculation Run → Derived Artifact | Not a Core Domain Command; follows ME-02-07 |

Examples are routing guidance, not enabled-capability claims.

## 14. Acceptance and conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| D-01 | Unregistered intent kind | Unsupported; no mutation |
| D-02 | Caller supplies invented Command Type ID | Rejected; service resolves only from catalog |
| D-03 | Undeclared payload field/wrong schema version | Rejected before persistence |
| D-04 | Shared Definition vs Map History ownership ambiguous | Blocked; no guessed path |
| D-05 | Time-zero creation submitted as historical Command | Rejected; use Initial State |
| D-06 | Field Revision submitted as Branch-local edit | Rejected; use Map Source Field Baseline |
| D-07 | Evidence stale after Candidate acceptance | Stale; old authorization invalid |
| D-08 | Road junction lacks stable endpoint IDs | Blocked with ME-03-H P0 |
| D-09 | Building/Cave references unadopted Object Type | Blocked |
| D-10 | Identical retry uses same key and payload | Same authoritative result; no duplicate |
| D-11 | Same key reused with changed payload | Idempotency conflict |
| D-12 | Commit succeeds but scene refresh fails | PendingReconciliation |
| D-13 | User cancels after authoritative commit | Formal operation remains applied |
| D-14 | AI proposal requires human authorization | No apply until exact authorization |
| D-15 | Preview-only/unverified handler requested for apply | Fail closed |
| D-16 | Spatial predicate is Indeterminate | Preserve uncertainty |
| D-17 | Geometry component operation attempts semantic Object split | Reject; separate domain operation required |
| D-18 | Calculation output submitted as ordinary geometry Command | Reject; use calculation Run/Artifact lifecycle |

## 15. Implementation decomposition

Recommended logical responsibilities within the local desktop application:
- Intent Schema Catalog: static, bounded schemas for supported intent kinds.
- Intent Builder: constructs immutable intents from manual/Inspector/AI adapters.
- Capability Resolver: queries Core Domain's adopted descriptor/service registry.
- Write-Path Resolver: selects exactly one formal owner/path from target and temporal semantics.
- Preflight Coordinator: validates context, target, source, spatial evidence, specialist rules, history and risk.
- Authorization Verifier: validates exact immutable binding when required.
- Operation Gateway: invokes owning Domain Service and idempotency contract.
- Reconciliation Handler: refreshes read models/scene and resolves stable selections.
- Diagnostics Mapper: translates typed outcomes into creator-facing explanations.

These are logical responsibilities, not separate processes or a generic orchestration framework. A small number of application services is sufficient.

## 16. Acceptance criteria

ME-06-D is complete as a design working baseline when:
1. Tool/intent/descriptor/Command/State Change/Candidate/authorization identities are distinct.
2. Four formal write paths and separate Event/presentation/calculation paths are unambiguous.
3. Every formal operation resolves through a Core Domain-owned adopted descriptor or owning baseline/field/shared-definition service.
4. Intent schemas are typed/versioned; generic CRUD/JSON Patch is excluded.
5. Preflight re-resolves targets/source fingerprints, and commit uses authoritative CAS/transaction semantics.
6. Candidate acceptance and authorization remain separate from formal apply.
7. Idempotent retry, cancellation-after-commit and PendingReconciliation are defined.
8. Road endpoint, Building/Cave and FunctionalArea blockers remain fail-closed.
9. Runtime Enabled claims require adopted descriptors, handlers, persistence and tests; design documentation alone is insufficient.

**Disposition:** ME-06-D is complete as a Typed Intent/formal operation routing design working baseline. It does not register new Core Domain descriptors or imply runtime implementation. Next: ME-06-E — Candidate Review Interaction, defining review granularity, dependency-safe partial acceptance, authorization binding, stale refresh and rejection/revision behavior.

## 17. Source baselines reviewed

- ME-06-00 Shared Interaction & Tool Contracts V0.1.
- ME-06-A Tool & Operation Capability Inventory V0.1.
- ME-06-B Input Routing & Gesture Lifecycle V0.1.
- ME-06-C Spatial Query Freshness V0.1.
- ME-01-05 Map Object, Geometry & Representation Model V0.1.
- ME-01-06 Editor Interaction Framework V0.1 and ME-01-07 Cross-architecture Consistency Review V0.1.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract.
- ME-05-05 Geometry & Field Asset Package Publication V0.3.
- ME-05-06 Cross-architecture Integration Review V0.4.
- Core Domain P2-09-08 Command Descriptor and Deterministic Replay Contract V0.2.
- Core Domain P2-09-15 Command Descriptor Catalog V0.1.
- ME-02-07 Calculation Contracts V0.4; ME-03-H and ME-04-H specialist adoption gates.

## 18. Change control

ME-06-D refines the shared Editor/Application boundary. Core Domain owns descriptor adoption and persistence authority; ME-02/03/04 own specialist semantics; ME-05 owns shared geometry/asset/coordinate contracts; ME-06-C owns query freshness. If implementation changes internal service boundaries, preserve semantic distinctions, fail-closed capability gates, write-path ownership, authorization binding and authoritative reconciliation.
