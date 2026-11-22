# World View — ME-03-H Cross-architecture Integration Review

- Version: 0.1
- Date: 2026-11-21
- Status: **Cross-architecture Review Working Baseline** — ME-03-A through ME-03-G reconciled against current Core Domain, AI Assistant / Map Generation, ME-01, ME-02, ME-05 and Map Engine contracts; implementation gates remain open
- Scope: Urban specialist domain, formal write paths, geometry and relationship contracts, placement validation, freshness, rendering/query generations, AI proposal lifecycle, asset publication and implementation readiness
- Authority: This review does not freeze an ADR, change a Core Domain descriptor, authorize a schema migration or claim implementation/test completion

## 1. Executive disposition

ME-03-A through ME-03-G form a coherent specialist product/domain/UX working package. The central product architecture is compatible with the wider World View baseline:

- One unified Map Editor and shared Canvas/Inspector interaction model.
- Existing formal Object Types are reused: Settlement, Road and PoliticalRegion.
- Core Domain remains the authority for Object identity, definitions, formal geometry, relationships, Map Initial State and Map/Branch History.
- Map Engine and Spatial Services provide deterministic geometry, topology, measurement and presentation queries; they do not own formal map truth.
- ME-02 owns natural-geography source fields and derived physical analysis; ME-03 consumes those outputs without redefining their algorithms.
- AI Assistant interprets creator intent, stages proposals where needed, explains evidence and submits only through the local Tool Gateway / Domain Services boundary.
- FunctionalArea remains proposal-only. Road-to-Settlement access and administrative hierarchy remain unsupported.

**Overall decision: accept ME-03-A through ME-03-G as the Urban Geography design working baseline, subject to the clarifications and gates in this review.** Do not begin formal Road connectivity implementation against the current relationship descriptor. Other manual authoring paths may proceed only where their Core Domain operation and persistence gates are independently satisfied.

### 1.1 Highest-priority findings

1. **P0 — Road endpoint connectivity is not representable unambiguously by the current relationship descriptor.** Formal Road-to-Road connection application must remain disabled until endpoint identity and relationship uniqueness are revised and validated.
2. **P1 — Settlement creation UX must not imply one generic create operation.** Object identity reservation/binding, Object definition, geometry ownership and Map membership / time-zero placement / post-zero creation have distinct Core Domain authorities.
3. **P1 — ME-03-G must inherit risk-based direct-operation behavior.** Candidate/Change Set is not mandatory for every manual edit or simple low-risk AI operation.
4. **P1 — Placement validation must not be treated as an implemented general rule engine.** ME-03-F defines product semantics and bounded rule families; each executable rule still needs a deterministic algorithm, evidence contract and freshness proof.
5. **P1 — Validation freshness must bind to both domain/calculation inputs and the active Map/Branch/Time context.** Renderer Scene Snapshot freshness alone is not proof that a placement finding is current.
6. **P1 — Project-shared definition, Map-local definition, Map Initial State and History-local Commands must remain distinct in every create/edit flow.**

## 2. Review scope and source baseline

This review was checked against the current GitHub files, not only the earlier Work Plan snapshot.

| Area | Current source baseline |
|---|---|
| ME-03 specialist domain | ME-03-A through ME-03-F V0.1 |
| ME-03 UX | ME-03-G V0.1 |
| Shared editor | ME-01-01 through ME-01-07; especially ME-01-06 Interaction Framework V0.1 |
| Core Object descriptors | P2-09-18 V0.2; P2-09-26 Object Type Registry V0.2 |
| Core Relationship descriptors | P2-09-20 V0.2; P2-09-27 Relationship Type Registry V0.2 |
| Core Commands and payloads | P2-09-15 Command Catalog V0.5; P2-09-17 Payload/Validation Contract V0.5 |
| Geometry component authority | P2-09-19 V0.2; ME-05-02 V0.1 |
| Spatial predicates and measurement | ME-05-03 V0.1; ME-05-04 V0.1 |
| Asset publication | ME-05-01 V0.5; ME-05-05 V0.3; ME-05-07 V0.3; ME-05-08 V0.1 |
| Natural geography | ME-02 overall V0.6; ME-02-F Calculation Contracts V0.4; ME-02-G UX V0.2; ME-02-H review V0.2 |
| Map Engine | Production Renderer Architecture V0.2; PRA-01 Immutable Scene Snapshot Contract V0.1 |
| AI proposal lifecycle | Map Generation Specification V0.2; Workflow V0.2; Session Lifecycle V0.2; AI Assistant Logical Schema V1.16 |

The machine-readable P2-09-26 and P2-09-27 registries take precedence over broader seed-catalog suggestions and specialist UX language. A recommended descriptor is not an enabled descriptor until adopted by Core Domain.

## 3. Cross-architecture alignment matrix

| Concern | Owning authority | ME-03 usage | Review disposition |
|---|---|---|---|
| Settlement / Road / PoliticalRegion identity | Core Domain Object Type Registry and reservation services | Specialist authoring and Inspector | Aligned; create orchestration clarification required |
| Definition fields | Core Domain Shared Object / Map-local definition services | Name and classification editing | Aligned only when routed by sharing scope; not History Commands |
| Time-zero placement and membership | Core Domain Map Initial State service | Initial settlement/road/region placement | Aligned; must not be represented as a time-zero Command |
| Post-zero creation and edits | Core Domain registered Commands in selected History | History-local Object presence and geometry changes | Aligned; target History and pre-bound identity required |
| Formal Geometry Components | Core Domain; immutable Geometry Assets | Anchor, extent, centerline and boundary | Aligned; no Editor-owned formal geometry store |
| Formal relationships | Core Domain Relationship Registry and typed payloads | Political containment/adjacency; Road connectivity | Political relationships aligned within exact endpoints; Road endpoint gap blocks connection apply |
| Natural-geography source fields | Core Domain Field Dataset/Revision authority | Placement evidence | Aligned; Map-wide retroactive source semantics must be honored |
| Derived calculations | ME-02 Calculation subsystem / Spatial Services | Terrain, water, climate and drainage findings | Aligned conceptually; executable algorithms and platform proof remain gated |
| Spatial predicates / measurement | Map Engine Spatial Services under ME-05 contracts | Point-in-area, adjacency candidates, proximity and distance | Aligned; exact reference, tolerance and query-generation contracts required |
| Scene rendering / hit-test / snap | Map Engine presentation subsystem | Canvas and geometry interaction | Aligned; immutable generation freshness must be checked before formal submission |
| Candidate / authorization / submission | AI Assistant and Tool Gateway | Complex AI-assisted proposals | Aligned with risk-based workflow; Candidate is not mandatory for every edit |
| FunctionalArea | Not currently enabled in Core Domain | Proposal overlay only | Correctly deferred |
| Road-to-Settlement access | No enabled Relationship Type | Nearby road / possible access observation | Correctly deferred |
| Administrative hierarchy / political control | No enabled Object/Relationship semantics | World Knowledge and creator narrative only | Correctly deferred |

## 4. Findings and required dispositions

### H-01 — Settlement creation must preserve identity, definition and Map presence as distinct operations

**Severity: P1 — workflow/implementation gate**

The current Settlement descriptor defines canonical_name and settlement_class as definition fields. The Object identity/reservation lifecycle is owned by Core Domain. The Map Initial State service owns time-zero membership and initial placement. A post-zero core.object.create Command is History-local and requires an already Bound Object identity; it is not a time-zero operation and does not copy the Object Type definition into its payload.

ME-03-G currently presents a creator-friendly single “Create Settlement” journey, which is correct as UX, but its wording can be read as one generic typed Object-creation write.

**Required interpretation:**

1. The UI may offer one simple “Create Settlement” workflow.
2. The application use case must resolve or create the stable Object identity through the Core Domain reservation/binding lifecycle.
3. canonical_name and settlement_class are written through the correct definition service for the Object's sharing scope (Project-shared or Map-local).
4. Initial placement and Map membership at time zero go through Map Initial State.
5. A post-zero creation uses the registered History-local creation Command against a Bound identity and selected History.
6. Geometry Assets and component ownership follow the relevant definition / Initial State / History-local geometry path; no Candidate or temporary Asset is referenced as a formal Asset.
7. If the workflow spans more than one durable operation, its interruption and retry behavior must be reconciled by stable operation identity. The UI reports success only after the authoritative records can be re-read.

The creator should not have to understand these internal service names, but the UI must accurately identify the resulting scope and effect: shared definition, this Map's starting state, or a change from a selected historical point.

**Acceptance:** no time-zero Command is generated; no post-zero create is submitted before identity binding; definition fields are not written through runtime attribute Commands; retry cannot allocate duplicate Object identities or duplicate formal membership.

### H-02 — Road connectivity lacks stable endpoint identity and cannot be safely applied

<badge> P0 — blocks formal Road connection </badge>

The current P2-09-27 V0.2 descriptor defines network.connects_to as a symmetric Road ↔ Road relationship. Its uniqueness is one canonical unordered Road pair per relationship type. The descriptor's endpoints are semantic Object identities; it does not declare a typed Geometry Component endpoint reference or a junction/connection-location identity.

ME-03-E correctly requires the creator to identify the intended endpoint of each Road segment and explicitly warns that the current operation must not invent hidden relationship metadata. ME-03-G correctly says that formal application must be disabled when endpoint evidence cannot be persisted.

Two independent limitations remain:

1. **Endpoint ambiguity:** a Road Object may have one or more centerline Geometry Components and two endpoints per LineString component. A Road-to-Road Object pair alone cannot identify which two endpoints form the connection.
2. **Pair uniqueness ambiguity:** the current canonical unordered Road-pair uniqueness permits only one relationship for a given pair of Road Objects. It cannot represent two distinct physical junctions between the same two Road Objects.

A junction marker in the Editor or a coordinate in the Scene Snapshot is not a durable formal endpoint identity.

**Recommended Core Domain follow-up (not authorized by this review):**

- Define a typed RoadEndpoint reference with stable identity, not a screen coordinate or list index.
- The endpoint identity must resolve to an owning Road Object and a Geometry Component and remain stable across supported geometry edits; endpoint reversal, component replacement and geometry refinement need explicit preservation/remapping rules.
- A formal connection must identify both exact RoadEndpoint references and the creator-confirmed connection semantics.
- Relationship uniqueness must be based on the canonical endpoint pair (or an explicitly defined Junction identity), not merely the Road Object pair, so multiple distinct junctions can be represented where the domain permits them.
- Define endpoint activity, temporal behavior, relationship termination/re-establishment, geometry-edit revalidation and deterministic replay.
- Update the machine-readable relationship descriptor, typed Command payload schema, Core Domain validators and validation vectors together.

Do not introduce a Junction Object solely to work around the gap unless the specialist domain review demonstrates that junction identity has independent semantic value.

**Interim product behavior:** Road centerline creation and editing may be exposed when their own geometry/write-path gates pass. Snap candidates and junction previews remain transient. Formal network.connects_to application remains disabled until the endpoint contract is adopted and tested. The user may keep a non-authoritative connection proposal/World Knowledge note, but it must not appear as a formal connected network.

**Acceptance:** one pair of Roads with two candidate crossings is unambiguous; multi-arm junctions are represented by explicit endpoint edges; geometry changes cannot silently retarget a connection; stale endpoint references are rejected; no endpoint identity is derived from array index, coordinate rounding or screen pixels.

### H-03 — PoliticalRegion relationship semantics are usable only within the exact registry contract

**Severity: P1 — formal relationship acceptance gate**

The current registry supports:

- spatial.contains: PoliticalRegion → Settlement, directed, strict spatial containment. The Settlement anchor and optional extent must lie within the source region extent. At a given History/time, at most one containing PoliticalRegion per region_class is allowed for a Settlement; different region classes may coexist.
- spatial.adjacent_to: PoliticalRegion ↔ PoliticalRegion, symmetric, one canonical unordered Object pair, explicit meaningful adjacency; proximity alone is insufficient.

These relationships are sufficient for bounded territory containment and object-level adjacency. They do not represent administrative parent/child hierarchy, political control, ownership, sovereignty, legal jurisdiction or claims.

**Required UX and service behavior:**

- A geometric predicate produces evidence or an adjacency candidate; it does not create the relationship.
- Containment validation must check the full registered semantics, including anchor and optional extent. An extent that merely overlaps or partly crosses a PoliticalRegion is not strict containment.
- The per-region_class cardinality is validated against the selected History/time before apply.
- An adjacency candidate must use a supported predicate and declared boundary semantics. Point-touch, proximity and visual contact must not be promoted to meaningful adjacency without the applicable predicate result and creator confirmation.
- Boundary edits trigger revalidation of affected relationship evidence. They do not silently create, delete or retarget formal relationships.
- A confirmed relationship is stored only through the registered typed relationship operation in Map Initial State or selected History-local scope.

**Acceptance:** the Inspector labels containment as spatial only; no administrative tree is generated from nesting; no control/ownership language is inferred; stale geometry cannot validate a new relationship.

### H-04 — FunctionalArea remains outside formal Map Object capability

**Severity: P1 — formal-save prohibition until adoption**

ME-03-D recommends a bounded FunctionalArea concept but explicitly records that the current Object Type Registry does not enable it and that the current relationship registry has no corresponding endpoints.

ME-03-G's proposal-only presentation is correct.

**Required behavior:**

- FunctionalArea remains an AI Candidate, creator draft or World Knowledge proposal; it is not a formal Core Domain Object.
- Do not reuse terrain_region: physical terrain/land cover and human use are different meanings.
- Do not write an unregistered type into an Object payload, relationship payload, generic JSON metadata or hidden layer.
- The distinction among actual, designated and proposed remains visible in the proposal semantics.
- Formal save, formal selection as an Object, history commands and authoritative spatial relationships remain unavailable until a separate Core Domain adoption package defines descriptor fields, geometry roles, write paths, temporal semantics, compatibility and validation vectors.

**Acceptance:** normal formal Object queries never return FunctionalArea proposals; the UI does not show a successful formal-save state; proposal provenance remains available in AI staging/knowledge.

### H-05 — Settlement placement rules are product semantics, not yet a general executable validation engine

**Severity: P1 — algorithm and rule-catalog gate**

ME-03-F defines a small explainable validation pipeline and outcome vocabulary. It does not authorize a general-purpose GIS suitability engine or an opaque scoring/rule framework.

The following distinctions must survive implementation:

- Creator-confirmed hard constraints are authoritative creator intent.
- Soft preferences guide recommendations and do not block by default.
- Derived physical evidence is only as reliable as its source coverage, units, resolution, algorithm and provenance.
- A deterministic derived rule may block only if its rule contract explicitly declares it a hard precondition and all required inputs are valid and fresh.
- AI inference is explanation/proposal, not physical evidence.
- Unknown, Unsupported and Indeterminate are first-class outcomes; they must not be converted to pass/fail.

**Required bounded implementation contract:**

For each enabled placement check, document:
1. stable rule/check ID and version;
2. required source fields/Objects and supported geometry roles;
3. required Map/Branch/Time and spatial scope;
4. deterministic algorithm/method and parameter profile;
5. units, measurement model, tolerance and error behavior;
6. coverage/no-data behavior;
7. outcome mapping and whether the result can block a specific operation;
8. input fingerprint and invalidation dependencies;
9. representative positive, negative, boundary, unknown and unsupported fixtures.

Start with a small catalog: anchor/extent consistency; known land/water classification; supported elevation/slope context; formal River/Lake proximity; and bounded Road proximity/connected-component observation. Do not claim engineering feasibility, flood safety, potable water, reliable supply or route accessibility.

Until a check's contract and implementation are verified, show Unknown or Unsupported rather than a fabricated Pass or Blocked.

### H-06 — Validation freshness must include formal context and source provenance

**Severity: P1 — freshness gate**

A placement finding is not fresh merely because it was calculated recently or because the renderer currently displays a new frame.

Its input identity must cover the relevant subset of:

- Project, Map and Map Space identity;
- selected Branch and canonical Historical Time where runtime facts are involved;
- Settlement Object identity and anchor/extent component identities plus exact geometry Asset fingerprints;
- relevant formal Object/Relationship state and source fingerprints;
- Map-owned Field Dataset IDs, current Field Revision IDs and package fingerprints;
- relevant derived Artifact fingerprints, algorithm/version and parameters;
- spatial-reference identity, precision profile, measurement model and tolerance;
- creator constraint/specification version;
- query scope, coverage and any required context/halo.

The validation subsystem owns the authoritative evidence fingerprint. The AI Candidate binds to the exact validation result and target context. The Editor displays the result and its freshness; it does not calculate freshness from timestamps or renderer versions alone.

A Map Source Field Revision is a Map-wide baseline correction: it affects all Branches and Historical Time points of its owning Map, retroactively. Therefore it invalidates dependent placement findings for every affected Branch/time in that Map, not just the currently visible time. It does not affect another Map, even if Asset bytes are deduplicated.

**Acceptance:** changing one required source invalidates dependent findings; unrelated checks remain valid when their declared dependencies are unchanged; a Candidate based on an old Field Revision or Branch/Time context cannot be applied with stale evidence.

### H-07 — Map Engine query and renderer generations must be re-resolved before formal apply

**Severity: P1 — interaction integration gate**

Production Renderer Architecture V0.2 and PRA-01 require one immutable Scene Snapshot per frame, built for one Presentation Context fingerprint and atomically published. Hit-test and snap candidates are derived presentation/query results, not formal geometry or relationships.

Urban editing must follow this sequence:

1. Capture the current Map/Branch/Time and viewport/query generation.
2. Perform hit-test, snap or spatial candidate query against a coherent immutable input snapshot.
3. Return candidate identity, source context, geometry/endpoint reference and declared tolerance/provenance.
4. Discard results from an obsolete query generation or mismatched context.
5. On user commit, re-resolve Object and Geometry Component references against current authoritative domain state.
6. Re-run required domain validation and expected-source-fingerprint/CAS checks.
7. Submit the typed Core Domain operation; never submit the screen coordinate or stale scene result as authority.

The last-known-good Scene Snapshot may remain visible while a new snapshot is prepared, but the UI must indicate that interactive query results from the old context are not eligible for formal submission.

**Acceptance:** stale hit/snap results are rejected; a frame never mixes geometry from one context with labels or overlays from another; failed scene preparation leaves the previous complete snapshot intact; no renderer callback performs domain reads or writes.

### H-08 — Candidate/Change Set use is risk-based, not universal

**Severity: P1 — UX policy clarification**

ME-01-06 and Map Generation Workflow V0.2 explicitly allow clear low-risk manual edits and simple AI operations to call Domain Services directly. Complex generation, broad geometry changes, uncertain interpretation, multiple affected Objects, protected setting impact or operations requiring explicit review use Candidate/Change Set staging.

ME-03-G's Candidate lifecycle is correct for proposals that enter staging, but it must not be interpreted as requiring every direct Settlement placement or geometry adjustment to create a full AI Task Session, Candidate Set and Change Set.

**Required policy:**

- Manual low-risk direct placement/edit: transient preview as needed, domain validation, then direct typed operation when intent and authorization are clear.
- Simple low-risk AI action: direct typed operation only if the request is unambiguous, within authorization and passes preflight.
- Multi-location AI placement, broad geometry changes, ambiguous constraints, multiple dependent relationships, protected facts or material cross-boundary impact: Candidate/Change Set with provenance, validation and exact target binding.
- Human confirmation is requested according to risk/authority policy, not as a blanket requirement for every operation.
- A Candidate is always separate from formal map data. Acceptance, authorization, submission and reconciliation retain their distinct underlying evidence where required.
- Candidate partial acceptance is permitted only when dependency and validation rules prove the selected subset coherent.

**Acceptance:** simple manual editing remains usable without AI; no unnecessary Candidate/session records are created for every low-risk action; complex AI proposals remain isolated and reviewable.

### H-09 — Shared definition, Map Initial State, History and source-field baseline must not be conflated

**Severity: P1 — authority routing gate**

Every formal urban write must identify one of the applicable authority paths:

| Path | Meaning | Urban examples |
|---|---|---|
| Project-shared Object definition | Changes the current shared definition of that Object for every Map using it, under the accepted shared-definition semantics | Correct shared canonical name/classification or shared base definition geometry |
| Map-local Object definition | Changes a Map-specific Object definition where the Object's ownership scope is Map-local | Map-local name/classification or definition geometry |
| Map Initial State | Changes the starting membership/placement/initial relationships of one Map at time zero | Place an existing/bound Settlement in the Map's starting state; establish initial supported containment/adjacency |
| Map/Branch History Command | Changes runtime state from a post-zero point only in the selected History | Create/deactivate an Object in that History; adjust geometry; establish/end a supported time-varying relationship |
| Map Source Field Baseline | Publishes a new physical source Field Revision for one Map; retroactively applies to all its Branches and Historical Time points | Correct elevation or land/water source field; not a historical terrain event |

A geometry edit after time zero must not silently rewrite a Project-shared Object definition. A shared-definition correction must not be disguised as a History Command. A Field Revision must not be recorded as a historical event. A historical terrain change must use the appropriate Map/Branch History operation.

The UI should show a creator-facing consequence label such as “Affects this Map from its starting state”, “Changes this History from the selected time”, or “Updates the shared definition across Maps”. It must not force the creator to choose raw database scopes.

### H-10 — Asset publication and operation reconciliation remain formal write prerequisites

**Severity: P1 — persistence gate**

ME-05-05 V0.3, ME-05-07 V0.3 and ME-05-08 V0.1 define immutable geometry/field payload ownership and the Field package contract. The formal sequence remains staged payload → validation → immutable Asset publication/read-back → short Core Domain transaction with typed owner link/CAS → authoritative re-read → derived/presentation invalidation.

Urban authoring must not:
- reference an AI Candidate or temporary staging path as a formal Core Asset;
- mutate published geometry bytes in place;
- update a formal owner reference before Asset integrity is verified;
- garbage-collect Assets still referenced by history, Initial State, definitions, Checkpoints or retained evidence;
- report success from file existence alone.

If the UI loses the response after commit, the application reconciles using the stable operation/submission identity and re-reads the authoritative owner/Command state before retry. This prevents duplicate Object creation, duplicate relationship establishment or duplicate geometry Commands.

Field Revision package publication is separate from geometry Asset publication. A Field package remains one immutable package per Field Revision; its tile members are not semantic Objects or independent revisions.

### H-11 — CRS, precision and measurement are shared services, not specialist-local shortcuts

**Severity: P1 — spatial service gate**

ME-03 must consume ME-05-03/04 and the selected Map Engine Spatial Services implementation.

- Geometry comparisons require a common declared spatial-reference identity or an explicit supported transform.
- Screen snap radius is a UI interaction tolerance; it is not the domain connection tolerance.
- Raw longitude/latitude Euclidean distance/area is prohibited.
- Distance/proximity results must identify measurement model, units, source fingerprints and accuracy/error metadata.
- Curve geometry remains formal geometry; renderer tessellation cannot be reused as authoritative measurement input.
- Unsupported transform, out-of-domain input or insufficient numerical certainty returns Unsupported / OutsideDomain / Indeterminate as appropriate.
- No silent CRS conversion, coordinate rounding or false precision.

The selected geometry/projection library and Windows/macOS numerical proof remain implementation gates; ME-03 must not pick a local substitute.

### H-12 — Multi-scale UX is aligned, but scope and extent remain separate

**Severity: P2 — acceptance gate**

ME-03-B and ME-03-G correctly preserve one Settlement identity across World / Region / Locality / Site. ME-01-04/06 remain authoritative for the separation among Map Extent, Working Scope, Canvas View, Export Area, data resolution and display LOD.

- Zoom/pan changes Canvas View only.
- Changing Working Scope is explicit.
- Map Extent is not changed by navigation or AI placement.
- Export Area is an explicit output selection.
- LOD/generalized geometry is derived presentation and never overwrites formal anchor/extent.
- An Object hidden at a scale is not deleted or deactivated.

### H-13 — Political adjacency is object-level; endpoint-specific boundary provenance is not currently formalized

**Severity: P2 — bounded V1 limitation**

The current spatial.adjacent_to descriptor is symmetric, object-pair unique and supports PoliticalRegion ↔ PoliticalRegion. This is adequate for one formal “these territories are adjacent” relationship per pair. It does not identify a particular pair of boundary components or preserve multiple distinct shared-border segments as separate formal relationships.

V1 should keep the relationship at the Object-pair semantic level. If later product requirements need border-segment identity, border dispute segments, crossing points or boundary-specific temporal changes, that is a new domain contract, not an extra Editor-only property.

### H-14 — ME-03 design completion does not close implementation or platform proof

**Severity: P1 — implementation readiness gate**

The design package does not establish that the Core Domain APIs, geometry engine, spatial predicates, Asset reader/writer, calculation algorithms, AI staging, renderer or Avalonia controls have been implemented or tested.

No ME-03 implementation gate is marked passed by this review. Existing Core Domain, ME-02 and ME-05 gates remain independently owned and must be evidenced by implementation and test results.

## 5. Consolidated implementation gates

| Gate | Priority | Owner | Required evidence | Blocks |
|---|---|---|---|---|
| G-A Formal urban write routing | P1 | Core Domain / Application | Operation matrix and tests for identity reservation/binding, shared/map-local definition, Map Initial State and post-zero Commands; interruption/idempotency proof | Formal Settlement/Road/PoliticalRegion creation and edits |
| G-B Road endpoint relationship | P0 | Core Domain + ME-03 + Map Engine | Stable RoadEndpoint identity; typed endpoint-pair payload; multiple-junction uniqueness; descriptor/schema/fixtures; replay and stale-geometry tests | Formal Road connectivity and connected-network claims |
| G-C Political relationship validation | P1 | Core Domain + Spatial Services | Exact containment/adjacency validation against registry cardinality, temporal and geometry semantics | Formal PoliticalRegion containment/adjacency apply |
| G-D Placement rule catalog | P1 | ME-03 + ME-02 + Spatial Services | Bounded versioned checks, deterministic methods, thresholds/profiles, outcomes and fixtures | Any placement finding presented as executable Pass/Blocked |
| G-E Evidence freshness and invalidation | P1 | ME-02 + Core Domain + Spatial Services | Input fingerprints, Field Revision/Branch/Time invalidation matrix, bounded recompute and stale Candidate rejection | Apply based on placement/urban validation |
| G-F Spatial service correctness | P1 | Map Engine / Spatial Services | CRS transform, precision, tolerance, topology and measurement fixtures; Windows/macOS numerical proof | Geometry-dependent validation and measurement |
| G-G Renderer/query generation integration | P1 | Map Engine + Editor | Immutable snapshot and query-generation tests; stale hit/snap rejection; authoritative re-resolution before commit | Interactive geometry/connectivity submission |
| G-H Asset and transaction proof | P1 | Core Domain / Application / Map Engine | Immutable Asset read-back, typed owner transaction, CAS, orphan recovery, idempotent reconciliation and provider tests | Formal geometry/field publication |
| G-I AI proposal and authorization | P1 | AI Assistant / Application | Risk-based direct-vs-Candidate routing, exact proposal hash/target binding, authorization recheck, submission/reconciliation tests | Complex AI-assisted urban changes |
| G-J UX and cross-platform acceptance | P2 | Editor / QA | ME-03-G scenarios plus failure/accessibility/Windows/macOS interaction tests | Production release readiness |

Gate G-B is a hard blocker only for formal Road connectivity. It does not block drawing and editing Road centerlines when the geometry write path is ready. G-D blocks claims of executable validation, not the display of clearly labelled source observations or unsupported checks.

## 6. V1 capability disposition

| Capability | V1 disposition | Formal status |
|---|---|---|
| Settlement with required anchor and optional extent | Supported by current descriptor | Enabled subject to write-path and geometry gates |
| Settlement class/name editing | Supported as definition fields | Definition service only; scope-aware |
| Settlement placement context | Bounded checks against ME-02 sources | Each check requires G-D/G-E/G-F evidence |
| Road centerline authoring | Supported by Road descriptor | Enabled subject to geometry/Asset/write-path gates |
| Road-to-Road connectivity | Semantically required but endpoint payload insufficient | Formal apply disabled until G-B closes |
| Nearby Road / possible Settlement access | Read-only derived observation | No formal access relationship |
| PoliticalRegion boundary authoring | Supported by current descriptor | Enabled subject to geometry/write-path gates |
| PoliticalRegion adjacency | Supported object-level relationship | Explicit confirmation and G-C validation |
| PoliticalRegion contains Settlement | Supported strict containment relationship | Explicit confirmation and G-C validation |
| Administrative hierarchy | Deferred | Not enabled |
| Political control / sovereignty / ownership | Deferred to World Knowledge or future political domain | Not enabled |
| FunctionalArea | Proposal-only | Not a formal Object Type |
| Settlement hierarchy | Deferred | Not enabled |
| Road routing, turn rules, traffic simulation | Deferred | Not enabled |

## 7. Required corrections / clarifications to ME-03-G

ME-03-G V0.1 remains a valid UX working baseline. Apply these integration clarifications when preparing Figma and implementation handoff:

1. Treat “Create Settlement” as one creator workflow that internally routes identity reservation/binding, definition fields and Map membership/placement through their separate authorities.
2. In the Road authoring frame, keep endpoint connection confirmation visible as a design target, but mark formal Apply as unavailable until G-B closes. Do not mock a successful persisted connection.
3. Ensure simple low-risk manual operations do not require a full Candidate/Change Set. Candidate review is used when risk/complexity warrants it.
4. For placement findings, show the method/source/freshness and do not show a Pass/Blocked result for an unimplemented rule.
5. Make the active Map/Branch/Time context and the scope effect of a formal operation visible in review surfaces.
6. When a source Field Revision changes, mark all dependent evidence stale across all Branches/Times of that Map, not only the currently visible time.
7. Re-resolve hit/snap/endpoint references against current domain state before formal apply.
8. Preserve the difference between “formal Object/relationship applied” and “derived observation displayed”.

These clarifications do not require a new parallel UI architecture or an immediate ME-03-G version fork.

## 8. Cross-package acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| ME03-H-001 | Create Settlement at Map time zero | Uses identity/definition authority plus Map Initial State; no time-zero Command |
| ME03-H-002 | Create Settlement after time zero | Bound identity required; History-local create Command; correct definition scope |
| ME03-H-003 | Retry interrupted creation | Reconcile operation identity; no duplicate Object identity or membership |
| ME03-H-004 | Edit shared Settlement definition | All Maps sharing that Object observe current-definition semantics |
| ME03-H-005 | Edit one Map/Branch runtime geometry | Other Maps/Branches are not silently mutated |
| ME03-H-006 | Road pair has two distinct crossings | Current descriptor cannot represent both; formal connection blocked until G-B |
| ME03-H-007 | Road geometry is reversed/refined after connection proposal | Endpoint identity must be preserved/remapped or proposal rejected stale |
| ME03-H-008 | Three Roads meet at one location | Every intended endpoint edge explicit; no implicit clique from proximity |
| ME03-H-009 | Settlement extent partly crosses PoliticalRegion boundary | Strict containment not established |
| ME03-H-010 | Settlement belongs to two PoliticalRegions of same region_class | Cardinality rejection at same History/time |
| ME03-H-011 | PoliticalRegion boundary moves | Adjacency/containment evidence revalidated; relationships not silently changed |
| ME03-H-012 | FunctionalArea proposal is queried as formal Object | It is absent from formal Object results |
| ME03-H-013 | Placement rule implementation is missing | Finding Unsupported/Unknown, not fabricated Pass/Blocked |
| ME03-H-014 | Creator hard constraint conflicts with AI proposal | Candidate blocked or clarification requested; no silent constraint weakening |
| ME03-H-015 | Map Field Revision changes | Dependent findings stale across every Branch/Time of that Map |
| ME03-H-016 | Same Field Asset bytes are deduplicated across Maps | Map-local Field Dataset authority and invalidation remain separate |
| ME03-H-017 | Candidate validation uses old Branch/Time context | Apply rejected stale; revalidate against current target |
| ME03-H-018 | Renderer snapshot changes during drag | Old hit/snap result cannot be submitted without re-resolution |
| ME03-H-019 | Geometry transform is unsupported | Unsupported/OutsideDomain; no silent coordinate conversion |
| ME03-H-020 | Geographic map requests raw Euclidean distance | Rejected or replaced with a supported geodesic method |
| ME03-H-021 | Geometry Asset write succeeds but DB transaction fails | No formal owner reference; safe orphan reconciliation |
| ME03-H-022 | Apply commits but UI acknowledgement is lost | Re-read authoritative state; no duplicate submission |
| ME03-H-023 | Simple manual low-risk placement | Direct typed operation allowed; no mandatory Candidate Session |
| ME03-H-024 | Complex AI settlement layout affects several Objects | Candidate/Change Set review and risk-based authorization |
| ME03-H-025 | User zooms from Region to Locality | Same Object identity; Map Extent and formal geometry unchanged |
| ME03-H-026 | A stale finding is selected while old Scene Snapshot remains visible | Clearly stale; cannot authorize or block a new formal operation as current evidence |

## 9. Ownership and next actions

### 9.1 Work that belongs to ME-03

- Maintain the urban creator workflows and the bounded specialist validation catalog.
- Define the exact intended RoadEndpoint semantics jointly with Core Domain before formal Road connectivity is enabled.
- Keep FunctionalArea, Settlement hierarchy and political actor/control semantics deferred until their domain models are justified.
- Validate the user-facing wording and evidence presentation against actual Core Domain/Spatial Service outcomes.

### 9.2 Work that remains with shared architecture owners

- **Core Domain:** identity reservation, definition services, Initial State, Commands, relationship payloads, descriptor adoption, typed validation and transaction/idempotency.
- **ME-02:** physical source field semantics, derived calculation algorithms, input snapshots, deterministic calculation and bounded invalidation.
- **ME-05 / Spatial Services:** geometry predicates, topology, coordinate transforms, precision, measurements and immutable Asset contracts.
- **Map Engine:** immutable Scene Snapshot, query generation, hit-testing, snapping, rendering and platform proof.
- **AI Assistant / Map Generation:** task/specification, Candidate/Change Set, authorization, Tool Gateway, submission and reconciliation.
- **Editor:** unified Canvas/Inspector interaction, creator-facing language, transient previews and accessibility.

ME-03 must consume these shared contracts rather than create local substitutes.

## 10. ME-03 package disposition

| Work item | Disposition |
|---|---|
| ME-03-A Domain Inventory | Complete — current Object/Relationship registry reconciled |
| ME-03-B Settlement Model | Complete — stable identity, anchor/extent, classification and scale semantics |
| ME-03-C Political Geography | Complete — territory/actor distinction and bounded relationships |
| ME-03-D Functional Areas | Complete — proposal concept and formal registry gap documented |
| ME-03-E Road Network | Complete — road segment/junction semantics and endpoint blocker identified |
| ME-03-F Placement Validation | Complete — evidence/outcome/uncertainty contract; algorithms remain gated |
| ME-03-G UX Scenarios | Complete — shared editor journeys, acceptance and Figma handoff |
| ME-03-H Integration Review | Complete — cross-architecture findings, capability disposition and implementation gates |

**Final recommendation:** accept ME-03-A through ME-03-H as the Urban Geography product/domain/UX and integration working baseline. Do not describe ME-03 as implemented or production-ready. Formal Road connectivity remains blocked by G-B; placement validation and all other formal write paths remain subject to their respective shared implementation gates.

## References

- ME-03-A through ME-03-G, current V0.1 specialist documents.
- ME-01-01 through ME-01-07, shared editor architecture.
- ME-02 overall V0.6; ME-02-F Calculation Contracts V0.4; ME-02-G UX V0.2; ME-02-H Cross-architecture Review V0.2.
- ME-05-01 V0.5; ME-05-02 through ME-05-04 V0.1; ME-05-05 V0.3; ME-05-06 V0.4; ME-05-07 V0.3; ME-05-08 V0.1.
- P2-09-15 V0.5; P2-09-17 V0.5; P2-09-18 V0.2; P2-09-19 V0.2; P2-09-20 V0.2; P2-09-26 V0.2; P2-09-27 V0.2.
- Map Generation Specification V0.2; Workflow V0.2; Session Lifecycle V0.2; AI Assistant Logical Schema V1.16.
- Production Renderer Architecture V0.2; PRA-01 Immutable Scene Snapshot Contract V0.1.
