# World View — ME-06 Shared Interaction & Tool Contracts

- Version: 0.2
- Date: 2026-11-21
- Status: Specialist workstream scope and design plan
- Product target: C# + Avalonia desktop application, Windows/macOS, local-first, single-user
- Depends on: ME-01-01 through ME-01-07; ME-02-G/H; ME-03-G/H; ME-04-G/H; ME-05-02 through ME-05-06; ME-06-E; Map Engine immutable scene/query contracts; Core Domain typed Command and persistence contracts
- Scope: Concrete shared tool capability declarations, input routing, gesture lifecycle, typed-intent mapping, spatial query freshness, Candidate review interaction and cross-architecture acceptance
- Authority: Product/application interaction working plan only. It does not authorize Core Domain descriptors, Commands, schema, APIs, renderer contracts or plugin extensibility.

## 1. Purpose

ME-01-06 established the shared Editor Interaction Framework. Specialist packages ME-02 through ME-04 have since supplied concrete creator workflows and domain-specific tools. ME-06 converts those conceptual rules into implementable, testable contracts shared by all professional editing modes.

The workstream addresses the gap between:
- a user gesture or explicit UI action;
- a specialist tool's domain intent;
- Map Engine spatial queries and geometry operations;
- Application Services and Core Domain typed operations;
- AI Candidate review and authorization;
- the editor's visible result, error and recovery behavior.

ME-06 is not a second editor architecture. It refines ME-01-06 into a bounded set of shared contracts and conformance scenarios. Specialist modes declare capabilities against the shared contracts; they do not create independent interaction state machines or bypass formal write boundaries.

## 2. Executive decisions

1. **One shared interaction runtime.** All specialist modes use the same active context, selection references, input routing, gesture capture, transient edit session, query freshness and result reconciliation.
2. **Tools are finite built-ins in V1.** A tool is a declared interaction capability, not an arbitrary plug-in or dynamically loaded code module.
3. **Tool, intent and formal operation are separate.** A tool interprets input; a typed intent expresses domain meaning; an adopted descriptor/Domain Service determines whether formal application is supported.
4. **No generic CRUD escape hatch.** A tool may only produce intents mapped to an explicit application operation and, for formal writes, an adopted Core Domain capability.
5. **All asynchronous spatial results are freshness-bound.** Hit-test, snap, measurement, topology and validation results must carry their source scene/query context and be re-resolved before a formal apply.
6. **Transient interaction never mutates formal state.** Gesture previews and local undo remain Editor-owned; AI proposals remain AI Assistant-owned; formal state remains Core Domain-owned.
7. **Candidate review is shared but risk-based.** The shared UI supports exact-version review, partial decisions where dependencies permit, stale detection and authorization invalidation. It does not force every simple manual edit through a Candidate.
8. **Failure is an explicit outcome.** Cancelled, stale, unsupported, blocked, rejected and applied are distinct states; the UI never reports success before authoritative confirmation.
9. **Domain semantics remain specialist-owned.** ME-06 defines how tools declare and route operations, not what a river, road, Building, Space, Cave or political region means.
10. **Keep V1 small.** No user-authored scripting, arbitrary plug-in runtime, macro engine, collaborative cursor/locking system or universal workflow graph.

## 3. Relationship to existing architecture

### 3.1 ME-01-06 versus ME-06

ME-01-06 remains the product/interaction architecture baseline. ME-06 supplies concrete contracts and testable behavior for its key seams:

| Concern | Existing baseline | ME-06 responsibility |
|---|---|---|
| Shared Canvas, selection and tool principles | ME-01-06 | Concrete state/context and conformance rules |
| Specialist creator journeys | ME-02-G, ME-03-G, ME-04-G | Cross-specialist operation/gesture matrix |
| Query/scene generation freshness | ME-01-07, ME-03-H, ME-05-06 and Map Engine | Shared freshness token, stale-result handling and apply-time re-resolution |
| Typed formal write path | Core Domain contracts and ME-01-07 | Tool → typed intent → Application Service mapping |
| AI Candidate lifecycle | AI Assistant contracts and specialist UX packages | Shared review interaction and exact-version decision behavior |
| Geometry/predicate execution | ME-05 and Map Engine | Consumer-side request/result contract; no duplicate geometry engine |
| Renderer scene presentation | Map Engine PRA contracts | Input and query consumption only; no formal domain authority |

### 3.2 Ownership boundaries

| Owner | Owns | Must not own |
|---|---|---|
| Avalonia Input Adapter | Device event normalization, focus and pointer capture | Domain meaning or persistence |
| Shared Interaction Runtime | Active context, routing, gesture sessions, transient selection/edit state | Formal Map state or AI proposal authority |
| Tool Definition Catalog | Static built-in tool declarations and capability metadata | Dynamic plug-in loading or Core Domain descriptor authority |
| Specialist Tool Handler | Domain-specific interpretation and UX defaults | Independent selection/history/persistence systems |
| Map Engine | Scene snapshots, rendering, hit-test/snap/geometry/spatial query execution | Semantic Object identity, authorization or formal history |
| Application Services | Context validation, operation routing, authorization handoff and reconciliation | Duplicate Core Domain invariants |
| Core Domain | Stable identity, typed descriptors, formal Commands/state and authoritative persistence | Gesture/viewport state |
| AI Assistant | Task, Candidate, evidence, review decisions, authorization and submission record | Direct Core Domain writes |
| Editor Presentation | Canvas overlays, Inspector, status and diagnostics | Invented operation success or hidden state mutation |

## 4. Canonical interaction concepts

### 4.1 Active Interaction Context

A single active context is captured for each interaction session. It includes:
- Project and Map identity;
- Branch and Historical Time;
- Working Scope identity/revision;
- active professional mode and tool;
- selection references;
- relevant layer visibility/editability context;
- spatial-reference and precision profile identities where applicable;
- immutable scene generation used by the current Canvas;
- current interaction/session identity.

Canvas View is presentation state and does not silently replace Working Scope. Export Area is not an interaction boundary.

A context change that invalidates a gesture or edit session must cancel or mark it stale before submission. It must not silently retarget the operation.

### 4.2 Tool Definition

A built-in Tool Definition should declare, at minimum:
- stable tool key and user-facing label;
- owning specialist mode(s);
- supported target kinds and selection levels (Object/component/vertex/Candidate/reference);
- allowed input gestures and keyboard actions;
- gesture lifecycle and completion/cancel rules;
- transient preview strategy;
- required spatial queries and their freshness dependencies;
- typed intent family it may emit;
- required context/preconditions;
- validation and risk classification;
- supported apply route or explicit Unsupported disposition;
- local undo/recovery behavior;
- accessible name, keyboard path and non-color status cues.

This is a logical contract, not a commitment to a specific serialized registry or runtime plug-in system. The initial implementation may use compiled, statically registered definitions.

### 4.3 Typed Intent

A Typed Intent is an application-level statement of what the user wants, independent of the pointer gesture that produced it.

Examples:
- Create a supported semantic Object at a selected location;
- Update a specific geometry component;
- Refine a boundary;
- Create an explicit typed relationship;
- Update a Building-owned Level/Space/Opening child aggregate;
- Change presentation only;
- Set a temporary Working Scope;
- Accept/reject an exact AI Candidate item.

Intent must identify target references, operation parameters, active context, base revisions and required evidence. It must not carry raw renderer node IDs as domain identity.

A Typed Intent is not a Core Domain Command. Application Services map it to an adopted formal operation or return Unsupported/Blocked.

### 4.4 Formal capability gate

A tool may be visible for inspection or preview even when formal apply is unavailable. The UI must distinguish:
- supported preview and supported apply;
- supported preview but formal apply unavailable;
- unsupported query/geometry capability;
- invalid current context;
- missing authorization.

A missing descriptor or endpoint pair is not solved by writing generic JSON, calling generic CRUD or reusing an unrelated relationship type.

## 5. Shared operation lifecycle

The shared interaction lifecycle is:

1. **Idle** — no active pointer/keyboard gesture.
2. **Armed** — a tool is active and declares its available actions.
3. **Capturing** — input is captured by the intended tool/target.
4. **Previewing** — transient visual feedback updates; no formal mutation.
5. **Awaiting completion** — the tool awaits explicit completion or confirmation.
6. **Intent built** — a typed intent is assembled with captured context and dependencies.
7. **Preflight** — target references, freshness, capability, invariants and authorization are checked.
8. **Applying** — Application Services invoke the supported Domain Service path.
9. **Reconciling** — authoritative result is read back and editor projections refresh.
10. **Completed / Cancelled / Failed / Stale / Unsupported** — terminal interaction outcome with an actionable explanation.

Not every tool uses every state. The implementation should use small explicit state objects and avoid a generic workflow engine.

### 5.1 Cancellation and capture loss

- Escape cancels the most local transient interaction first.
- Pointer capture loss, window deactivation, focus change or device disconnect must terminate or safely suspend the active gesture.
- A partially drawn geometry must not be auto-submitted on capture loss.
- A completed formal operation is not undone by Escape or by cancelling an AI task.
- If the system cannot safely resume a gesture, discard its preview and explain what was retained.

### 5.2 Local undo

Local undo applies only to the current uncommitted Edit Session. Once Core Domain confirms a formal operation, it leaves the local transient undo stack. Formal reversal requires an adopted formal inverse/history mechanism. The UI must not imply that a local Ctrl+Z has reverted an already-applied formal Command.

## 6. Spatial query freshness contract

### 6.1 Query request context

Every asynchronous spatial query that can influence interaction or formal intent must capture the relevant immutable context:
- Map, Branch and Historical Time;
- Working Scope identity/revision;
- immutable scene generation;
- query request generation/sequence;
- target Object/component revision fingerprints where applicable;
- spatial-reference identity and transform version;
- precision profile;
- algorithm/provider version and parameters where applicable;
- query geometry and declared coordinate space.

Only dependencies relevant to a particular query need be included, but omitted dependencies must be justified by the query contract.

### 6.2 Result handling

- Results for an obsolete request generation are discarded.
- Results from an old immutable scene generation are not presented as current selection/snap/validation truth.
- A newer pointer position supersedes older hit-test/snap results; the UI must not let a late response move the snap target backward.
- A result may be shown as historical/reference evidence only when clearly labelled as such.
- Unknown/Indeterminate/Unsupported remain distinct from False/Pass.
- Before formal apply, re-resolve target identity and current revisions against authoritative services. A query token is evidence of what was inspected, not a write lock or authorization.

### 6.3 Freshness versus authorization

Freshness proves that the preview/query was computed against a known context. Authorization proves that the actor may request a particular operation. Neither implies the other. Both are rechecked at the appropriate application boundary.

### 6.4 Scene refresh

When a formal operation changes the Map:
- Core Domain remains authoritative;
- the editor waits for or requests the corresponding projection/scene generation;
- selection is re-resolved by stable Object/component identity;
- deleted/retired/replaced references are cleared or surfaced as stale;
- old scene query results are not merged into the new scene as if current.

## 7. Tool-to-operation mapping

Each tool must have a reviewed mapping row with:
- tool key and specialist owner;
- input/gesture;
- target kind;
- Typed Intent;
- required Map Engine query/operation;
- formal Core Domain capability or explicit non-persistent outcome;
- validation and authorization requirements;
- freshness dependencies;
- cancellation and local undo semantics;
- expected success/failure/stale/unsupported UI outcome.

The mapping must preserve these distinctions:
- create semantic Object versus add geometry to an existing Object;
- update geometry versus update presentation;
- geometric coincidence versus formal topology/relationship;
- Map Initial State versus post-zero History Command;
- Map Source Field Baseline versus historical event;
- Building-owned child aggregate versus top-level Map Object;
- AI Candidate decision versus formal application.

A tool must not be considered implemented merely because its Canvas gesture renders. It is complete only when its intent mapping, capability gate, failure behavior and authoritative reconciliation are defined.

## 8. Candidate review interaction contract

Candidate review is a shared interaction surface, not a separate editor or a mandatory wrapper for all edits.

Required shared behaviors:
- identify the exact Candidate and version/hash;
- distinguish Candidate overlay from formal Map content and reference evidence;
- select, isolate, compare and inspect Candidate items;
- show added/modified/removed/relationship impact and provenance;
- show validation, uncertainty, dependencies and unsupported items;
- permit partial decisions only where dependency closure and validation prove the subset coherent;
- invalidate prior acceptance/authorization when material proposal content or target context changes;
- revalidate Candidate freshness and authorization before submission;
- show Selection Snapshot accepted/partially accepted decisions separately from atomic Change Set submission outcomes; show Applied, Rejected, Failed, Stale, Outcome Unknown or Recovery Required only from authoritative evidence;
- retain exact submission evidence and reconcile the Canvas from Core Domain.

The review UI must not claim that “Accept” directly mutates the Map. Confirming a selection finalizes an immutable Selection Snapshot only. Authorization is a separate explicit action bound to an exact sealed Change Set version/hash; formal application may still fail.

## 9. Error, recovery and status vocabulary

Shared terminal outcomes:
- **Cancelled** — user/system ended an uncommitted interaction; no formal mutation.
- **Failed** — an operation attempt failed; report whether any formal sub-operation was already applied.
- **Stale** — captured context or dependency changed; rebase only when provably safe.
- **Blocked** — a known invariant or policy prevents the operation.
- **Unsupported** — no adopted capability exists for the requested operation.
- **Rejected** — Candidate or operation was explicitly rejected by the authorized decision-maker.
- **Applied** — authoritative Core Domain confirmation and reconciliation succeeded.
- **Pending reconciliation** — formal submission may have succeeded but authoritative read-back/projection is not yet confirmed; do not display Applied.

Errors should identify the affected target, user-understandable reason, whether formal state changed, and the next safe action. Never collapse all failures into a generic “Save failed”.

## 10. Accessibility and input parity

V1 must support equivalent core workflows through pointer and keyboard:
- tool activation and focus indication;
- selection and selection modification;
- gesture start/complete/cancel;
- accessible alternatives for operations that cannot be performed by pointer alone;
- Candidate item navigation and accept/reject decisions;
- diagnostics navigation and recovery actions.

Color is never the only way to distinguish formal content, Candidate, selection, warning or failure. Focus order, visible focus, hit target size, keyboard cancellation and screen-reader labels are part of acceptance, not polish-only work.

Touch/pen-specific gestures may be added where platform behavior justifies them, but they must map to the same typed intents and freshness rules.

## 11. V1 non-goals

- User-authored tool scripting or arbitrary third-party plug-in loading.
- A generic visual workflow or node graph.
- Macro recording, gesture replay or automation marketplace.
- Multi-user collaboration, shared cursor, distributed locking or live merge.
- Universal CAD/BIM/GIS command coverage.
- Automatic semantic relationship creation from snapping or geometric coincidence.
- A generic CRUD/JSON-patch persistence layer.
- An editor-side duplicate of Core Domain history.
- AI Candidate staging for every manual edit.
- Claiming formal apply from a preview, renderer callback or AI response.

## 12. Work sequence and deliverables

| Work item | Focus | Main deliverable |
|---|---|---|
| ME-06-00 | Shared Interaction & Tool Contracts scope | This workstream scope, boundaries, dependency map and package plan |
| ME-06-A | Tool and operation capability inventory | Cross-specialist tool/gesture/intent/Map Engine/Core Domain mapping; explicit unsupported matrix |
| ME-06-B | Input routing and gesture lifecycle | Focus, pointer capture, cancellation, transient edit session and local undo contract |
| ME-06-C | Spatial query freshness | Scene/query generation tokens, late-result suppression, dependency fingerprints and apply-time re-resolution |
| ME-06-D | Typed Intent and formal operation routing | Intent envelope, capability gate, Application Service routing, authorization and idempotent reconciliation boundaries |
| ME-06-E | Candidate review interaction | Exact-version review, dependency-closed partial selection, immutable Selection Snapshot, Change Set authorization and atomic submission outcomes |
| ME-06-F | Error recovery, accessibility and acceptance | Failure taxonomy, keyboard/pointer parity, recovery matrix and executable acceptance scenarios |
| ME-06-G | Cross-architecture integration review | Reconcile ME-01/02/03/04/05, Core Domain, AI Assistant, Map Engine and desktop application; findings and implementation gates |

Each package must be grounded in current repository contracts and must label recommendations separately from adopted/frozen behavior. Do not duplicate domain-specific rules already owned by specialist packages.

## 13. Completion criteria

ME-06 is complete as a design working baseline when:
1. Every V1 shared and specialist tool in ME-02/03/04 has a traceable tool → intent → service/capability mapping or an explicit Unsupported disposition.
2. Input routing, focus, capture loss, cancellation and local undo have deterministic behavior.
3. Every asynchronous spatial query has an explicit freshness dependency profile and stale-result policy.
4. Formal apply re-resolves stable references and revalidates current context; renderer/query results cannot authorize writes.
5. Candidate review uses exact version/hash, dependency-closed partial selection, immutable Selection Snapshots and authorization invalidation; one Change Set is applied atomically or not at all.
6. Error outcomes distinguish Cancelled, Failed, Stale, Blocked, Unsupported, Rejected, Applied and Pending reconciliation; partial acceptance is not partial application.
7. Pointer/keyboard parity and non-color status communication have acceptance scenarios.
8. Core Domain, AI Assistant, Map Engine and desktop application ownership boundaries are reconciled.
9. Windows/macOS platform proof requirements are specified without claiming execution.
10. Work Plan and documentation index are updated and verified in GitHub.

## 14. Final disposition

ME-06 is the next shared workstream after the ME-04 specialist package reached its design review. It turns the existing interaction architecture and specialist UX into precise, testable tool/application seams. Its purpose is to prevent each editor mode from inventing its own gesture runtime, query freshness rules or formal write path.

ME-06-A through ME-06-E now establish the capability, input/gesture, query freshness, typed-intent routing and Candidate review baselines. ME-06-E resolves the distinction between dependency-aware partial selection and atomic Change Set application. The remaining work is ME-06-F recovery/accessibility/acceptance and ME-06-G cross-architecture integration. No ME-06 document by itself authorizes production implementation or changes to formal domain contracts.


## 11. V0.2 Candidate selection and atomic submission clarification

ME-06-E defines dependency-aware partial selection. This is a selection decision, not partial formal application.

V1 submission of one Change Set remains atomic and all-or-none through the Core Domain + AI Assistant transaction boundary:
- a Candidate Set may be `partially_accepted` when the user selects a dependency-closed subset;
- the resulting Change Set contains only the selected exact Candidate Versions;
- Core Domain either commits the entire sealed Change Set or commits none of it;
- do not show or persist a `partially_applied` outcome for one Change Set in V1.

If multiple independent Change Sets are submitted separately, report each transaction outcome separately. Do not aggregate them into one apparent partial transaction.


## 12. V0.2 Candidate Review integration note

ME-06-E V0.1 completes the Candidate Review design baseline. It reuses the existing AI Assistant Candidate/Validation and Selection/Application schema contracts and adds no V1 database table.

The key integration rule is that a dependency-closed subset may be **partially accepted**, but a single sealed Change Set remains an atomic submission. Core Domain commits every Command in the Change Set or none. The review UI must not present a partial Change Set application outcome in V1. Separate Change Sets, if submitted independently, are reported as separate transactions.

ME-06-E defines a typed, immutable `required_candidate_versions[]` dependency manifest inside the Candidate Version payload rather than introducing a generic Candidate Dependency table. Dependencies pin exact versions; cycles are rejected; formal Command ordering remains separate.

ME-06-A through ME-06-E are complete as design working baselines. Runtime implementation, executable UI/service conformance tests, Windows/macOS evidence and the remaining ME-06-F/G packages remain open.
