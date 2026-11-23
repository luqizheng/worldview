# World View — ME-08-08 Cross-Architecture Integration Review & Implementation Handoff

- Version: 0.1
- Date: 2026-11-23
- Status: Cross-Architecture Review Working Baseline; implementation gates remain open
- Workstream: ME-08 — Spatial Operations & Domain Integration
- Scope: ME-08-00, ME-08-06, ME-08-07 reconciled with ME-01, ME-03, ME-04, ME-05, ME-06, Core Domain, Map Engine and desktop application boundaries
- Platform: Single-user desktop application for Windows and macOS
- Authority: Integration review and implementation handoff only. This document does not adopt new Core Domain descriptors, Commands, Relationship Types, schemas, APIs or persistence structures.

## 1. Executive disposition

ME-08-00, ME-08-06 and ME-08-07 are conceptually aligned with the established World View architecture. They can be retained as product/domain/editor UX working baselines. No second editor runtime, generic geometry CRUD layer, universal persistent topology graph, independent history mechanism or collaboration subsystem is required.

The central integration decision is:

**ME-08 supplies composition and interaction semantics; specialist workstreams define domain meaning; Core Domain alone owns formal spatial facts and write authority; Map Engine supplies deterministic spatial computation and disposable presentation acceleration; Editor owns interaction and staging.**

ME-08 is therefore accepted as a design package subject to the gates in this review. It is not a statement that all described entities and operations are currently enabled or implemented.

### 1.1 Highest-priority conclusions

1. **P0 — Building/Cave and interior child aggregates are not formally enabled by the current Core Domain V1 registries.** Building Interior and Cave authoring must remain read-only or transient/proposal-only until the ME-04-H adoption gates are satisfied.
2. **P0 — Connection UX must not imply that every connection can be persisted.** Road endpoint identity remains blocked by ME-03-H. Building access and Cave connection records remain gated by ME-04-H. The shared connection protocol is an integration shape, not a generic Relationship descriptor.
3. **P1 — Spatial Site and Spatial Group are not automatically formal Core Domain entities.** Their product semantics are accepted, but durable organization persistence needs an explicit owner/adoption decision. Until then, use references and/or editor-local organization only where clearly labeled.
4. **P1 — Compound Site/template operations must respect one authoritative atomic commit boundary.** A single visual preview cannot make Shared Definition, Map Initial State, History and Field Baseline writes atomic.
5. **P1 — The unified operation lifecycle must reuse ME-06 and Map Engine contracts.** No parallel Candidate, Change Set, freshness, query, history or interaction runtime may be introduced.
6. **P1 — The UX must distinguish formal apply from reconciliation and derived refresh.** Renderer/index failure after a successful formal commit does not mean the domain operation failed.
7. **P1 — Windows/macOS behavior is a verification gate.** Design coverage is not runtime accessibility, precision or platform-conformance evidence.

## 2. Reviewed baselines and authority

| Source | Relevant contract consumed | Review disposition |
|---|---|---|
| ME-08-00 V0.1 | Spatial Site/Group/Entity composition, dedicated 2D Building scene, extensible templates/modules, typed two-endpoint connection semantics | Retain; no new formal descriptors implied |
| ME-08-06 V0.1 | Typed operation lifecycle, operation families, coordinate isolation, formal write routing, split/merge deferral | Retain; align exact UX state handling with ME-06 |
| ME-08-07 V0.1 | Shared editor surface, eight scenario families, diagnostics, recovery and acceptance matrix | Retain; capability-gated tool exposure |
| ME-01-04/05/06/07 | Multi-scale scope, object/geometry/representation separation, shared interaction runtime, cross-architecture constraints | Controlling shared editor contracts |
| ME-03-H V0.1 | Settlement/Road/PoliticalRegion descriptor limits; Road endpoint identity blocker; temporal and validation gates | Road formal connectivity remains disabled |
| ME-04-H V0.1 | Building/Cave top-level identity proposals, typed child aggregates, geometry ownership, access endpoints, local frames | Building/Cave formal editing remains gated |
| ME-05-06 V0.5 | Formal spatial authority, geometry operations, predicate outcomes, coordinate precision, Asset publication, Map Engine boundary | Controlling geometry/spatial service contract |
| ME-06-G V0.1 | Shared interaction ownership, Change Set atomicity, Candidate/Authorization/Apply separation, freshness, recovery | Controlling operation/review lifecycle |
| Core Domain P2-09-15/18/19/20/26/27 and related contracts | Registered Commands, Object/Relationship descriptors, component identity, endpoint/temporal semantics | Machine-readable adopted registries and descriptors take precedence |
| Map Engine renderer/scene/spatial presentation contracts | Immutable scene generations, typed transforms, hit-test/snap, query freshness and derived caches | Reuse; no duplicate spatial runtime |

Authority precedence:
1. Adopted Core Domain descriptors, Command catalog, typed payload validators and persistence contracts determine formal capability.
2. ME-05/Map Engine contracts determine deterministic spatial computation and derived evidence.
3. ME-01/ME-06 determine shared interaction and proposal/application lifecycle.
4. ME-02/03/04 determine specialist domain meaning and validation.
5. ME-08 composes those capabilities into scene and workflow semantics.

A product or UX document cannot override a missing or narrower descriptor.

## 3. Cross-architecture ownership matrix

| Concern | Authority | ME-08 usage | Prohibited shortcut |
|---|---|---|---|
| Object identity, lifecycle, temporal state and formal history | Core Domain | Resolve Site members and operation targets by stable identity | Editor-generated permanent IDs |
| Geometry Component identity, role and formal Asset references | Core Domain typed owner/Command contracts | Stage typed geometry operations | Renderer IDs or local component registries |
| Geometry transforms, predicates, measurement, hit-test and snap | Map Engine / Spatial Services | Supply deterministic preview and validation evidence | Editor-local geometry engine with competing semantics |
| Building/Cave/Space/access meaning | ME-04 specialist + adopted Core Domain contract | Scene navigation, typed tools and specialist diagnostics | Generic Object CRUD or arbitrary JSON |
| Road and urban connectivity | ME-03 specialist + adopted Relationship descriptor | Endpoint selection and preview | Persisting a screen coordinate or Road Object pair as endpoint identity |
| Site/Group organization | Product semantics in ME-08; formal owner not yet adopted | Organize references and navigate composed scenes | Assuming group membership is physical containment |
| Candidate, Selection, Change Set, Authorization | AI Assistant / ME-06 contracts | Review AI-originated or compound proposals | A second ME-08 approval subsystem |
| Formal apply and write-path selection | Core Domain Application/Domain Services | Submit only adopted typed operations | UI scene decides temporal write authority |
| Scene rendering, indexes, tessellation and hit-test caches | Map Engine / presentation | Reflect reconciled formal state | Writing derived output back as formal geometry |
| Templates/themes/modules | Resource/content layer; instantiated facts owned by domain | Generate typed staged proposals | Persisting a template as a substitute for resulting entities |
| Local drafts and transient gestures | Editor session | Recover or discard uncommitted work | Presenting local draft as formal map state |

## 4. Formal write-path and atomicity reconciliation

The four formal authorities remain distinct:

| Formal path | Scope | ME-08 example | Required UX wording |
|---|---|---|---|
| Project-level Shared Object definition | Current shared definition, potentially retroactive across referencing Maps | Edit shared base Building/feature definition where supported | “Changes shared definition” with cross-Map impact |
| Map Initial State | One Map's time-zero baseline | Place/create an adopted object in the initial map | “Changes this Map's starting state” |
| Post-zero Map/Branch History Command | Selected Map/Branch and historical point | Move or adjust an entity at a selected time | “Creates a change in this Map/Branch history” |
| Map Source Field Baseline | Owning Map's source field revision, retroactive across its Branches/times | Publish revised terrain/elevation source field | “Updates this Map's source field baseline” |

Rules:
- The visible scene, selected Level or active tool never independently selects the write path.
- Every formal Typed Intent carries explicit target identity, owner, Map/Project scope, temporal context and expected source revision/fingerprint.
- A single Change Set may contain multiple operations only if one owning authority can commit them atomically under one compatible context and authorization scope.
- Cross-authority compound proposals are split into separately authorized Change Sets with separately reported outcomes. If dependencies require all-or-none behavior across independent authorities, block until a Core Domain-owned coordinator is designed and proven.
- Never simulate cross-boundary atomicity with sequential writes and compensating rollback.
- AI acceptance is not application. “Applied” requires formal commit confirmation; “Reconciled” requires authoritative read-back and scene projection.

## 5. Capability disposition matrix

The following is the safe ME-08 implementation posture against the reviewed design baselines. “Available” still requires verification against the current checked-out registry/runtime; this review does not claim code-level implementation.

| Capability | UX/design status | Safe implementation posture | Gate for formal enablement |
|---|---|---|---|
| World Map navigation, pan/zoom, scene selection | Established shared Editor/Map Engine contract | Implement as view-only interaction | Platform interaction tests |
| Typed selection, Inspector, context breadcrumb | Established shared Editor contract | Implement read-only first | Stable read projections and identity references |
| Existing adopted Object inspection | Existing Core Domain types | Read-only; author only through already adopted descriptor/Command paths | Exact descriptor and formal path test |
| Generic geometry adjustment/refinement/add/remove | Bounded Core Domain/ME-05 operation family | Enable only for exact adopted role, owner, geometry family and write path | Descriptor, payload, Asset and replay tests |
| Geometry split/merge | Explicitly deferred | No formal tool; optional deterministic preview only if supported | New lineage/topology/identity contract and adopted Commands |
| Building object creation/footprint | ME-04 proposal; not enabled in reviewed V1 registry | Read-only proposal/transient prototype only | ME-04-H H-01 plus footprint role/Commands and persistence |
| Building Levels/Spaces/interior plans | ME-04 typed child aggregate proposal | Read-only/transient; no generic Object fallback | ME-04-H H-02 through H-07 and complete aggregate tests |
| Building access/Doors/Openings/Vertical Connectors | ME-04 typed access proposal | Endpoint/geometry preview only; no formal apply | Typed endpoint union, boundary anchors, lifecycle and Commands |
| Natural Cave System/chambers/passages | ME-04 proposal | Survey/geometry proposal only; no formal apply | Cave descriptor, survey frame, child aggregate and temporal contract |
| Spatial Site composition | Product baseline | Navigation/composition over existing identities; no assumed persisted Site entity | Explicit formal Site identity/ownership decision if durable semantics required |
| Spatial Group hierarchy | Product baseline | Editor-local organization or reference-only presentation, clearly labeled | Explicit owner and typed persistence contract before formal world data |
| Door/Entrance/Cave Passage shared connection UX | Shared protocol baseline | Domain-specific tools; each gated independently | Adopted specialist endpoint/relationship descriptors |
| Road connectivity | ME-03-H P0 blocker | Preview only; formal connection disabled | Stable RoadEndpoint identity, uniqueness, lifecycle and replay contract |
| River connectivity | Specialist endpoint semantics remain owned by ME-02 | No inferred connectivity | Explicit River endpoint/network contract and descriptor adoption |
| Template/theme browsing | Resource/content concern | Implement resource browsing and static preview | Resource schema/package validation |
| Template instantiation | Compound typed proposal | Staged preview only until every required member is supported | Capability closure, dependency validation and atomic boundary |
| Compound geometric transform of Group/Site | Not a default Group operation | Do not implement as drag/drop membership | Explicit typed member set, transform frame and all-member validation |
| Local gesture cancel/undo | ME-06 interaction contract | Implement in transient Editor session | Gesture/focus/cancel tests |
| Committed undo/reversal | Core Domain History | Use only adopted compensating/reverse Command | Descriptor-specific inverse/reversal semantics |
| Windows/macOS accessibility and recovery | ME-08-07 requirements | Must be tested on both platforms | End-to-end evidence, not design assertion |

## 6. Findings and required dispositions

### W-01 — Building and Cave formal authoring are blocked by missing adopted descriptors and aggregates
**Severity: P0 — blocks formal Building/Cave editing**

ME-04-H establishes a coherent semantic model but confirms that the reviewed Core Domain V1 registries do not enable Building/Cave Object Types, Building child aggregates, interior geometry roles, access records or their Commands.

Required disposition:
- Treat Building/Cave design and UX as valid but proposal-only until the ME-04-H adoption path is completed.
- Preserve Building as one top-level identity with typed Building-owned Level/Space/plan/access children; do not create every Room/Door/Stair as a top-level Object to reuse generic CRUD.
- Preserve Cave System as its own semantic identity and its survey frame; do not reinterpret cave coordinates as Building-local or Map coordinates.
- Reconcile component owner metadata and stable child IDs before formal interior geometry.
- Enable capabilities independently only after their exact descriptor, persistence, validator and replay tests pass.

### W-02 — Connection protocol is not a universal persisted Edge
**Severity: P0 — blocks generic connection persistence**

ME-08's shared connection protocol is an integration vocabulary (identity, typed endpoints, anchors, optional geometry, traversal semantics and constraints). It does not create a generic relationship type or make every specialist endpoint pair valid.

Required disposition:
- Keep Door, Entrance/Access Connection, Vertical Connector, Cave Passage, Road connection and future Portal/Tunnel semantics specialist-owned.
- Ordinary connection operations have two typed endpoints.
- Multi-way junctions use an explicit intermediate spatial entity plus pairwise connections, where that entity has independently adopted semantics.
- Proximity, overlap, intersection, shared coordinates or snap evidence never creates a formal connection.
- A connection tool is enabled only for an adopted endpoint pair and formal write path.

### W-03 — Road connectivity remains a hard formal blocker
**Severity: P0 — no formal Road-to-Road connectivity**

ME-03-H documents that the current Road-to-Road relationship endpoint pair cannot identify the exact endpoints/components and cannot disambiguate multiple junctions between the same Road Object pair.

Required disposition:
- Keep Road geometry editing separate from Road network connectivity.
- Allow endpoint/junction preview only as transient evidence.
- Do not persist coordinates, line-array indices, renderer IDs or Road Object pair alone as durable endpoint identity.
- Resolve stable RoadEndpoint identity, remapping under geometry edits, relationship uniqueness, lifecycle and deterministic replay in ME-03/Core Domain before enablement.

### W-04 — Site and Group product semantics do not yet imply formal persistence
**Severity: P1 — organization authority decision required before durable organization data**

ME-08 accepts Spatial Site composition and nested Spatial Groups as product concepts. Existing reviewed sources do not establish a universal persisted Site/Group entity or generic parent-child relationship.

Required disposition:
- A Site may be a composition/navigation context over existing typed entity references.
- A Group may be editor-local organization until a formal owner/typed relation is adopted.
- Never represent Group membership as physical containment, geometry ownership, access or traversability.
- If users must share/persist Groups across Maps or Branches, define the exact scope and temporal behavior before implementation; do not create an ad hoc JSON blob or generic hierarchy table.
- Do not duplicate an entity merely because it appears in multiple Site/Group views.

### W-05 — Template instantiation needs dependency closure and authority-aware commit boundaries
**Severity: P1 — blocks “one-click complete Site” claims**

Templates are recipes/resources. Their output is a staged set of typed domain entities and relationships, not one magical template entity.

Required disposition:
- Template manifest declares required/optional members, types, coordinate assumptions, dependencies and connection endpoint requirements.
- Resolve every required type/operation against adopted capabilities before enabling Apply.
- Required unsupported members block complete instantiation. Optional omissions must be explicit and visible.
- A compound Change Set is atomic only within one supported authoritative commit boundary.
- If a template crosses Project Shared Definition, Map Initial State, History or Field Baseline, split into independently authorized commits or block when dependency closure requires atomicity.

### W-06 — ME-08 operation lifecycle must reuse ME-06 and Map Engine, not duplicate them
**Severity: P1 — implementation architecture constraint**

ME-06-G already owns shared interaction/selection/gesture state, Candidate and Change Set lifecycle, authorization, freshness and recovery semantics. ME-05/Map Engine already own spatial queries, transforms, predicates, measurement, scene generations and disposable derived indexes.

Required disposition:
- ME-08 specialist tools contribute typed tool definitions, target filters, specialist validation and UX guidance to the shared runtime.
- Consume immutable scene generation and query tokens; discard stale hit/snap results.
- Re-resolve authoritative identities, descriptors, source fingerprints and write context at apply time.
- Use ME-06 Change Set/authorization/recovery rules; no parallel ME-08 approval/transaction system.
- Keep query/predicate outcomes True/False/Indeterminate and InvalidInput/Unsupported/OutsideDomain/Cancelled/StaleSnapshot distinct.

### W-07 — Group transform and entity relocation must remain separate operations
**Severity: P1 — prevents destructive UX ambiguity**

A Group reparent or membership drag is organization-only. A geometric relocation changes formal geometry under a coordinate frame and write path.

Required disposition:
- Default drag/drop in the Group navigator changes membership only.
- A Group/Site-wide transform is a distinct compound operation with explicit member selection, pivot, transform frame, affected relationships, validation and preview.
- A Building move in Map coordinates does not rewrite Building-local Room geometry.
- A Level switch or scene transition never changes geometry.

### W-08 — Apply, reconciliation and derived refresh are separate observable states
**Severity: P1 — correctness and recovery constraint**

Formal commit, authoritative read-back and Map Engine refresh are different steps.

Required disposition:
- “Applied” only after Core Domain confirms commit.
- “Reconciled” only after authoritative identities/state are read back.
- If render/index refresh fails after commit, retain Applied and show Pending Reconciliation/Refresh; do not offer a blind retry of the formal operation.
- If commit outcome is unknown, reconcile using stable operation/submission identity before retry.
- Local undo/redo is limited to uncommitted Editor state; committed reversal uses adopted History semantics.

### W-09 — Single-user scope removes collaboration features, not local correctness controls
**Severity: P1 — product constraint**

The product is single-user desktop, so distributed locks, multi-user presence, collaboration conflict UX and online concurrency protocols are out of scope.

Required disposition:
- Keep local expected-revision/fingerprint checks, query freshness, apply-time re-resolution and idempotent recovery.
- No collaboration subsystem or distributed lock manager is introduced for ME-08.
- Local session interruption, crash recovery and safe application-close behavior remain required.

### W-10 — Windows/macOS parity requires runtime evidence
**Severity: P1 — release gate**

ME-08-07 specifies keyboard-only workflows, focus, screen reader semantics, non-color diagnostics, pointer capture loss and platform-specific modifier keys.

Required disposition:
- Validate on Windows and macOS independently.
- Essential create/select/inspect/review/apply/cancel flows must have keyboard paths.
- Test high DPI, zoom, pointer cancellation, window deactivation, dialogs, scene switching and app shutdown.
- Do not claim platform conformance from shared source code or design review alone.

## 7. Implementation handoff: staged work packages

The implementation plan deliberately separates the shared Editor foundation from specialist formal capabilities. This allows useful progress without faking unsupported Building/Cave/Road operations.

### WP-00 — Capability inventory and contract map
**Priority:** P0 prerequisite  
**Scope:**
- Read the current Core Domain Object/Relationship registries and Command catalog from the checked-out repository.
- Generate a machine-readable matrix of descriptor × geometry role × operation × write path × validator × platform status.
- Map every ME-08-07 tool to either ViewOnly, TransientPreview, FormalEnabled or Unsupported.
- Add tests that assert unsupported tools fail closed.
**Exit criteria:** no ME-08 formal tool is enabled by UX declaration alone; inventory cites exact registry/Command evidence.

### WP-01 — Shared spatial context and navigation
**Priority:** P0 foundation  
**Scope:**
- Implement active Project/Map/Branch/Time context display and explicit target write-context resolution.
- Implement World Map / Site / Building Interior / Level / Cave navigation state and breadcrumb.
- Preserve view/camera/selection on context return where references remain valid.
- Prompt on context switch when a gesture or staged proposal is active.
**Exit criteria:** context switch never creates a formal mutation; coordinate frame and temporal scope are inspectable; stale selection is safely cleared/re-resolved.

### WP-02 — Shared selection, Inspector and operation surface
**Priority:** P0 foundation  
**Scope:**
- Reuse one shared selection/gesture/Inspector runtime.
- Support typed selection references for Object, Component, Relationship, Site/Group reference, derived item and transient Candidate.
- Implement tool availability states and explanation of unsupported capability.
- Implement operation review surface and shared diagnostic presentation.
**Exit criteria:** no renderer/cache identity is mistaken for formal identity; all core workflows can inspect target type, owner and coordinate frame.

### WP-03 — Transient operation sessions and preview
**Priority:** P0 foundation  
**Scope:**
- Implement tool arming, gesture capture, transient geometry, staging, preview and cancel.
- Consume Map Engine coordinate transforms, hit-test/snap references and immutable scene generation.
- Bind previews to source fingerprints and active context.
- Keep local undo/redo inside the uncommitted Editor session.
**Exit criteria:** gesture cancel/discard leaves formal state unchanged; stale query or source snapshot invalidates preview; no formal writes occur from Canvas handlers.

### WP-04 — Formal operation adapter for already-adopted capabilities
**Priority:** P0, but strictly bounded  
**Scope:**
- Route only exact adopted typed operations to Core Domain services/Commands.
- Keep Shared Definition, Initial State, History and Field Baseline write paths separate.
- Re-resolve identities, descriptor, source revision, authorization and idempotency at apply.
- Reconcile authoritative result before updating selection/scene.
**Exit criteria:** positive and negative descriptor tests; no generic CRUD fallback; unknown outcome reconciles before retry; Applied and Reconciled states are distinct.

### WP-05 — Windows/macOS interaction and accessibility proof
**Priority:** P1 platform gate  
**Scope:**
- Keyboard-only create/select/inspect/review/apply/cancel paths for supported operations.
- Focus restoration, screen-reader labels, non-color diagnostics and high-contrast checks.
- DPI/zoom, pointer capture loss, app deactivation and shutdown recovery.
**Exit criteria:** separate Windows and macOS test evidence; no platform-specific loss of semantic behavior.

### WP-06 — Building/Cave adoption integration
**Priority:** P1 specialist dependency; do not start formal UI enablement before Core Domain adoption  
**Scope:**
- Consume ME-04-H approved Building/Cave descriptors and typed aggregate contracts.
- Integrate Building-local frame, Level, Space, plan/boundary components, access endpoints and Cave survey frame.
- Enable capability slices only as each typed aggregate/Command is implemented and tested.
**Exit criteria:** ME-04-H H-01 through H-07 and remaining applicable gates closed by Core Domain evidence; Building/Cave end-to-end tests.

### WP-07 — Site/Group organization persistence decision
**Priority:** P1 product/domain decision before durable shared organization  
**Scope:**
- Decide whether Site/Group are editor-local navigation organization, project-shared organization, Map-local baseline data or History-temporal data.
- If formal, define typed ownership, identity, membership constraints, lifecycle and temporal semantics.
- Until decision/adoption, keep organization non-authoritative and visibly labeled.
**Exit criteria:** no persisted Group/Site data without an explicit owner and lifecycle contract.

### WP-08 — Typed connection enablement
**Priority:** P1 specialist-by-specialist  
**Scope:**
- Integrate adopted Door/Opening/Entrance/Vertical Connector/Cave Passage contracts individually.
- Keep Road connectivity blocked until ME-03-H endpoint identity and uniqueness gates are closed.
- Keep River network connectivity gated by ME-02 endpoint semantics.
- Validate endpoint compatibility, anchors, frame transforms, direction and operational constraints.
**Exit criteria:** each enabled connection type has exact endpoint descriptor, lifecycle, validation vectors and deterministic replay tests.

### WP-09 — Template/resource and compound instantiation
**Priority:** P2, after WP-04 and specialist capability slices  
**Scope:**
- Implement extensible resource manifests for layout templates, themes and functional modules.
- Generate typed staged proposals with required/optional dependency closure.
- Review all affected entities, connections, coordinate frames and write boundaries.
- Apply only within supported atomic boundaries.
**Exit criteria:** unsupported required member blocks complete apply; partial preview is explicitly labeled; no cross-authority false atomicity.

## 8. Recommended implementation order and dependencies

`WP-00 → WP-01 → WP-02 → WP-03 → WP-04 → WP-05`

Specialist and advanced branches:
- `WP-06` depends on WP-00 through WP-04 plus Core Domain ME-04 adoption.
- `WP-07` is a product/domain decision; its formal persistence branch depends on explicit owner semantics and WP-04.
- `WP-08` depends on WP-00 through WP-04 and each specialist descriptor adoption; Road and River remain independently gated.
- `WP-09` depends on WP-00 through WP-04, WP-07 where durable organization is required, and all required specialist capability slices.

The first useful implementation milestone is WP-00 through WP-03: a functioning, safe, cross-platform editor interaction foundation with navigation, typed inspection and transient spatial editing, without claiming unsupported formal domain writes.

## 9. Definition of Done for ME-08 design package

ME-08 is complete as a design/integration working package when:
- ME-08-00, ME-08-06 and ME-08-07 remain mutually consistent;
- ownership and formal write-path boundaries align with ME-01/05/06;
- Building/Cave/Road/connection gaps are explicitly gated by ME-03-H/ME-04-H;
- Site/Group persistence is not assumed;
- compound operations respect ME-06 atomicity;
- operation state and recovery semantics distinguish formal apply, reconciliation and derived refresh;
- implementation work packages and exit criteria are actionable.

This Definition of Done does not claim runtime implementation, Core Domain adoption, test completion or Windows/macOS conformance.

## 10. Explicit non-goals

- No collaboration, multi-user presence, distributed locking or online conflict-resolution system.
- No universal persistent topology graph or generic Edge/Connection table.
- No generic spatial CRUD or arbitrary JSON patch path.
- No Editor-owned Object/Component identity or History.
- No formal Building/Cave/Road connection writes before their descriptor gates.
- No assumption that Site/Group is already a persisted domain entity.
- No automatic semantic connection from geometry proximity.
- No formal geometry split/merge in V1.
- No full 3D Building/Cave authoring requirement.
- No claim that design handoff equals implementation completion.

## 11. Completion criteria

ME-08-08 is complete as a cross-architecture handoff when:
- reviewed source baselines and authority precedence are recorded;
- ownership, write-path and atomicity matrices are explicit;
- P0/P1 findings have owners and fail-closed dispositions;
- capability availability is separated from UX design availability;
- work packages have dependencies and testable exit criteria;
- the implementation order is suitable for local desktop development.

The next step is implementation WP-00, beginning with a repository-grounded capability inventory rather than enabling UI tools from design documents alone.
