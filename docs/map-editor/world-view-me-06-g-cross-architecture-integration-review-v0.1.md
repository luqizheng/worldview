# World View — ME-06-G Cross-architecture Integration Review

- Version: 0.1
- Date: 2026-11-21
- Status: Cross-architecture integration review working baseline; runtime implementation and platform conformance remain open
- Scope: ME-06-00/A/B/C/D/E/F against ME-01, ME-02/03/04, ME-05, Core Domain, AI Assistant, Map Engine and AF-01/AF-02
- Authority: Integration review and implementation gates only. This document does not authorize new Core Domain descriptors, schema migrations, runtime capability claims or platform conformance claims.

## 1. Executive disposition

ME-06-00 and ME-06-A through ME-06-F form a coherent shared interaction contract for the unified World View editor. No second editor runtime, alternate domain authority, generic CRUD path or distributed collaboration layer is required.

The architecture is ready to serve as the shared Editor/Application interaction working baseline, subject to the explicit integration gates in this review. Design completion does not mean the tools are implemented or Enabled.

The most important integration conclusion is a required V1 constraint:

**One sealed Change Set must map to one authoritative atomic commit boundary.** It may contain multiple typed operations only when the owning Core Domain/Application transaction can commit all of them atomically under one compatible authority and context. Operations that cross independent authority boundaries (for example, Project-level Shared Object definition, Map Initial State, post-zero History and Map Source Field Baseline) must not be placed in one Change Set and described as all-or-none unless an owning coordinator explicitly provides and proves that cross-boundary atomic transaction. In the absence of such proof, split them into separately authorized Change Sets with separately reported outcomes; if semantic dependencies require atomicity across those boundaries, block the combined operation.

This constraint reconciles ME-06-D's distinct write-path ownership with ME-06-E's all-or-none Change Set semantics.

## 2. Architecture consistency matrix

| Concern | Authority | ME-06 contract | Review disposition |
|---|---|---|---|
| Stable Object/Component identity, descriptors, formal state and history | Core Domain | A/D | Aligned; Editor/AI never allocate formal identity or write persistence directly |
| Shared input, selection, transient gestures and local undo | Editor Interaction Runtime | B/F | Aligned; one shared runtime, one gesture owner, no formal mutation from transient state |
| Scene generation, hit-test, snap, predicates and spatial computation | Map Engine / ME-05 spatial services | C/F | Aligned; immutable query context, latest-wins, stale discard and apply-time re-resolution |
| Specialist semantic meaning and validation | ME-02/03/04 | A/C/D/E | Aligned; shared tools route specialist intents but do not redefine domain semantics |
| Candidate, Validation, Selection, Change Set and Authorization evidence | AI Assistant | E/F | Aligned at conceptual boundary; serializer/schema compatibility and executable validation remain gates |
| Immutable Asset bytes and publication | Asset Store + owning Domain Service | D/F | Aligned; staged bytes are not formal authority until adopted |
| Application lifecycle and local sessions | AF-01/AF-02 Application Host | F | Partially verified; AF-02 exists with Core Domain read/write dependencies explicitly fail-closed |
| Keyboard, screen reader, focus, device and platform behavior | Editor + Avalonia/platform adapters | B/E/F | Contract defined; Windows/macOS runtime evidence still required |

## 3. Cross-architecture findings

### G-01 — Shared interaction ownership is consistent

**Severity: Pass / architectural invariant**

One shared Interaction Runtime owns active context, selection references, input routing, gesture capture, transient edit sessions and editor-local recovery. Specialist modes contribute bounded tool definitions and handlers. Map Engine owns deterministic spatial computation and presentation acceleration. Core Domain owns formal facts. AI Assistant owns proposal and decision evidence.

No specialist editor may create its own parallel selection/history/persistence runtime. No renderer node, spatial-index entry, tile index or local draft identity may become a formal Object/Component identity.

**Required preservation:** ME-06-00/A/B remain the shared contract for all specialist modes; ME-02/03/04 may extend typed tool semantics but not ownership boundaries.

### G-02 — Change Set atomicity must match one authoritative commit boundary

**Severity: P0 integration constraint before cross-path batch implementation**

ME-06-D defines four distinct formal write paths:
1. Project-level Shared Object current definition.
2. Map Initial State.
3. Post-zero Map/Branch History Command.
4. Map Source Field Baseline publication.

ME-06-E correctly requires one sealed Change Set to be all-or-none. Those statements are compatible only when all operations in that Change Set are handled by one owning atomic commit boundary.

V1 rules:
- A Change Set may contain multiple operations if they share one compatible authoritative transaction/atomic publication boundary, one target context and one coherent authorization scope.
- Do not assume that a generic Application Service call makes Project-level, Map baseline, History and Field Revision writes one transaction.
- Do not mix operations that require independent commit protocols into one Change Set merely because they originated in one Candidate Set.
- When separate Change Sets are needed, each receives its own sealed hash, authorization (where required), idempotency lineage and terminal outcome.
- If a semantic dependency requires operations from different atomic boundaries to succeed together, return Blocked until a Core Domain-owned coordinator/transaction contract is explicitly designed and tested. Do not simulate atomicity with sequential commits and compensating rollback.

This is a restriction on Change Set composition, not a change to the four formal write paths.

### G-03 — Candidate dependency manifest needs serializer and validator adoption

**Severity: P1 implementation gate**

ME-06-E places exact required Candidate Version references in the immutable Candidate Version payload manifest rather than introducing a dependency table. This is the preferred bounded V1 design, but ME-06-E itself identifies a required reconciliation with the actual Candidate payload serializer and validator.

Before implementation:
- define the typed manifest shape and canonical hashing rules in the owning AI Assistant schema;
- validate same-Task and compatible target context;
- validate exact version existence and dependency graph acyclicity;
- resolve whether each dependency is satisfied by a selected Candidate Version or by a freshly resolved formal target reference;
- include the manifest and transitive dependency closure in Candidate content/version hashes and Selection/Change Set evidence;
- reject unknown dependency kinds and mutable current-version pointers.

Until that schema/validator evidence exists, dependency-closed selection remains a design contract, not an Enabled runtime feature. No new database table is required by this review.

### G-04 — Candidate, Selection, Change Set, Authorization and Apply remain separate

**Severity: Pass / mandatory invariant**

The state/evidence chain is:

Candidate Set → immutable Candidate Version → applicable Validation Report → immutable Selection Snapshot → sealed Change Set version/hash → exact human Authorization when policy requires → submission → authoritative Core Domain commit → reconciliation.

Selection is not authorization. Authorization is not application. Candidate Set summary status is not authoritative item-level decision evidence. A Candidate preview is not a formal Map object or Core Asset.

A material change to payload, selected versions, dependency manifest, target context, write path, descriptor semantics, source fingerprints or Change Set operation set creates a new sealed version/hash and invalidates prior authorization. No approval is silently inherited.

### G-05 — Query freshness is necessary but not sufficient

**Severity: Pass / mandatory invariant**

ME-06-C correctly treats query evidence as freshness-bound evidence rather than a lock or authorization. ME-06-D correctly requires authoritative re-resolution and preflight at the commit boundary.

The Application boundary must validate both:
- the query token and scene/source context used to form the intent; and
- the current authoritative Object/Component revisions, descriptor, temporal owner, Field Revision and formal write preconditions.

A Current query result does not guarantee the formal write will pass. Commit-time CAS and domain validation remain authoritative. Indeterminate/Unsupported spatial outcomes must not be coerced into success.

### G-06 — Asset publication and domain commit ordering is consistent, with one boundary rule

**Severity: Pass with implementation gate**

ME-05-05, ME-05-06, ME-06-D and ME-06-F align on immutable staging:
1. stage and validate;
2. write immutable content;
3. read back and verify hash/manifest;
4. enter the owning short authoritative transaction;
5. compare expected revision/CAS;
6. adopt the Asset/Revision reference;
7. reconcile orphaned bytes safely if adoption fails.

A staged Asset is not formal authority. For a Change Set that references Assets, every required Asset must be verified before the owning atomic commit. Asset publication must not create a false impression that a formal Object/Field Revision has already been committed.

For Map Source Field Baseline, publication remains Map-wide and retroactive across Branches and Historical Times in that Map; it is not a Branch-local Command and does not affect another Map.

### G-07 — Identity reservation lifecycle remains Core Domain-owned

**Severity: P1 implementation gate**

ME-06-E requires a valid permanent reservation before a new-Object Candidate can be finalized. Rejection/supersession does not silently release or reuse a reservation. This preserves identity safety.

The implementation must nevertheless define the Core Domain-owned reservation lifecycle:
- reservation owner and binding to Candidate/Task/Project/target context;
- expiry/abandonment semantics, if any;
- idempotent conversion from reservation to formal Object identity;
- recovery when Candidate/Task terminates before apply;
- safe cleanup that never reassigns an identity already made permanent.

Editor and AI Assistant may display reservation state but cannot allocate, release or recycle Object IDs themselves. Until the owner contract and recovery path are verified, new-Object formal creation remains subject to the relevant capability gate.

### G-08 — Gesture interruption and cancellation semantics are consistent

**Severity: Pass / mandatory invariant**

ME-06-B and ME-06-F preserve a strict boundary:
- gesture interruption cancels or preserves only a non-applicable transient draft;
- local Undo/Redo applies only to uncommitted Editor state;
- cancellation before authoritative commit may stop staged work;
- cancellation after confirmed commit cannot undo formal state;
- unknown commit outcome requires reconciliation before retry;
- confirmed commit with stale/failed scene refresh is PendingReconciliation.

No automatic geometry completion, auto-submit or formal rollback is permitted after focus loss, capture loss, application shutdown or AI Task cancellation.

### G-09 — Recovery outcome taxonomy is coherent

**Severity: Pass / mandatory invariant**

The shared outcomes CompletedLocally, PreviewOnly, Blocked, Unsupported, Unverified, InvalidInput, Rejected, Stale, Cancelled, Failed, OutcomeUnknown, RecoveryRequired, PendingReconciliation, Applied and Reconciled preserve the furthest authoritative stage.

In particular:
- OutcomeUnknown is not Failed;
- PendingReconciliation means the formal commit is confirmed;
- Applied requires authoritative commit confirmation;
- Reconciled means the scene/read model reflects the authoritative result.

Retry is allowed only with the same immutable payload/context and idempotency key after the prior outcome is known or reconciliation proves it safe.

### G-10 — Accessibility is part of the shared operation contract

**Severity: Pass / platform proof required**

Keyboard navigation, non-pointer action paths, visible focus, screen-reader announcements, high-contrast/status redundancy and safe focus behavior are specified across B/E/F. Status cannot depend on color alone. Candidate decision state and formal application state must be announced separately.

The design does not claim WCAG/platform conformance. Windows and macOS must each produce evidence for keyboard-only completion, screen-reader labeling/order, focus restoration, high-contrast rendering, pointer/pen/touch interruption and modal routing.

### G-11 — AF-02 application foundation does not yet prove formal Map operations

**Severity: P1 implementation gate**

AF-02 reports the desktop application shell, Project Package, Project/Map Workspace Session lifecycle, bootstrap ledger/migrations and content-addressed Asset Store implemented, but with a PASS WITH CONDITIONS verdict. The real Project identity → Map list → Workspace read path remains fail-closed until Core Domain persistence is integrated. Core Domain write paths and the ME-06 operation gateway are not proven by the AF-02 report.

Therefore:
- ME-06 runtime tools must not be marked Enabled from the existence of the application shell or Asset Store alone;
- Application Services must bind to the real Core Domain ports/services, not Editor-owned SQL or shadow tables;
- formal Map/Branch/Field operations remain blocked or unverified until owning services, transactions, replay/read-back and tests are present.

### G-12 — Specialist capability blockers remain authoritative

**Severity: Pass / preserve existing blockers**

ME-06 does not resolve specialist domain adoption gaps:
- Road endpoint connectivity remains blocked until stable endpoint identity and typed relationship payload/uniqueness are adopted (ME-03-H P0).
- Building, Level, Space, Opening, Access and Cave capabilities remain blocked until the Core Domain adoption package is implemented and tested.
- FunctionalArea remains a proposal until its Object Type and operations are adopted.
- Geometry split/merge remains unsupported unless a dedicated identity/lifecycle operation is adopted.
- Drainage/climate/placement outputs follow Calculation Run/Derived Artifact contracts, not ordinary geometry Commands.

The ME-06 capability inventory's Unverified/Blocked states must not be upgraded by UI or design completion.

## 4. Integrated end-to-end operation contract

The complete shared flow is:

1. Capture: one active Editor gesture/action captures immutable interaction context.
2. Interpret: a built-in specialist handler produces a typed intent; no formal identity is invented.
3. Query/validate: Map Engine and specialist validators return freshness-bound evidence.
4. Stage: AI Assistant may assemble immutable Candidate Versions and validation reports; staged Assets remain non-authoritative.
5. Review: user inspects exact versions, evidence, dependencies and conflicts; selection is dependency-closed.
6. Finalize selection: AI Assistant writes an immutable Selection Snapshot; this is not authorization.
7. Prepare: Application/AI builds a Change Set whose operations share one atomic commit boundary and compatible context.
8. Preflight: resolve exact descriptors, reservations, stable targets, source fingerprints, write path, temporal context, risk and CAS expectations.
9. Authorize: human authorization binds the exact sealed Change Set version/hash where policy requires.
10. Publish/apply: verified Assets are adopted and the owning Domain Service commits the whole Change Set atomically.
11. Reconcile: authoritative operation results refresh Map read models and scene generation; stable identities restore selection.
12. Recover: unknown outcomes reconcile before retry; confirmed commits are never resubmitted.

Low-risk direct manual operations may bypass Candidate UI only where policy permits, but they still pass Typed Intent, capability resolution, preflight, authorization policy and formal Domain Service boundaries.

## 5. Consolidated implementation gates

| Gate | Required evidence | Blocks |
|---|---|---|
| GATE-01 Core Domain service adoption | Adopted descriptors, baseline/field/shared-definition services, transaction and replay contracts | All formal writes without a verified owner |
| GATE-02 Change Set atomic-boundary enforcement | Service-level validation that all operations share one atomic commit boundary; negative tests for mixed boundaries | Multi-operation Change Set submission |
| GATE-03 Candidate dependency schema | Canonical payload manifest, hash inclusion, validator and dependency closure tests | Partial/dependency-aware selection |
| GATE-04 Object reservation lifecycle | Owner API/service, idempotent bind/consume, abandonment recovery and non-reuse tests | New Object Candidate finalization/application |
| GATE-05 Operation Gateway | Typed intent schemas, capability resolver, preflight, idempotency, authoritative outcomes | Formal tool application |
| GATE-06 Asset publication integration | Stage/read-back/hash/CAS/adoption/orphan recovery tests | Geometry/Field formal publication |
| GATE-07 Query freshness integration | Immutable token, sequence, invalidation and apply-time re-resolution tests | Asynchronous geometry-assisted apply |
| GATE-08 Candidate/Selection/Authorization integration | Exact-version reports, immutable snapshots, Change Set hash binding, revocation/stale tests | Review-required AI application |
| GATE-09 Recovery integration | OutcomeUnknown reconciliation, PendingReconciliation, crash/restart and no-duplicate tests | Safe retry and crash recovery |
| GATE-10 Windows/macOS input and accessibility | Platform build/run, keyboard/screen-reader/focus/device interruption evidence | Platform conformance claims |
| GATE-11 Specialist descriptor adoption | Road endpoint, Building/Cave, FunctionalArea and other typed capability evidence | Affected specialist operations |
| GATE-12 End-to-end conformance suite | Automated unit/service/UI/integration tests mapped to A-F scenarios | Marking any capability Enabled |

These gates are evidence requirements, not a new project-management workflow or separate runtime service fleet.

## 6. V1 implementation sequence

1. Close Core Domain authority and persistence dependencies first; keep all unsupported paths fail-closed.
2. Implement the shared Interaction Runtime and input adapter with transient-only behavior.
3. Implement query context/freshness and the spatial service boundary.
4. Implement the smallest adopted typed-intent operation path, including preflight, CAS, idempotency and reconciliation.
5. Integrate Candidate/Validation/Selection/Change Set/Authorization using the actual AI Assistant schemas; add dependency manifest only after schema adoption.
6. Enforce one atomic commit boundary per Change Set before enabling multi-operation submission.
7. Integrate immutable Asset publication and recovery.
8. Add Windows/macOS keyboard, pointer, pen/touch and assistive-technology evidence.
9. Enable specialist tools one by one only after descriptor, handler, persistence, tests and platform evidence meet ME-06-A's Enabled standard.

Do not implement a generic workflow engine, distributed coordinator, arbitrary plugin runtime or universal transaction abstraction to bypass these gates.

## 7. Acceptance and conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| M06G-01 | Specialist mode attempts to create a parallel selection/history runtime | Architecture test/review rejects duplicate authority |
| M06G-02 | Change Set combines operations owned by different atomic boundaries | Blocked unless an adopted coordinator proves one atomic commit |
| M06G-03 | Same-boundary Change Set contains multiple valid operations | Whole set commits or none |
| M06G-04 | Candidate dependency manifest is unknown/malformed/cyclic | Selection finalization blocked |
| M06G-05 | Candidate dependency points to an exact compatible version | Closure includes exact version or proves formal target already satisfies it |
| M06G-06 | New Object Candidate has no valid reservation | Finalization/application blocked |
| M06G-07 | Rejected Candidate is abandoned | Reservation follows Core Domain lifecycle; no Editor/AI ID recycling |
| M06G-08 | Query is Current but target revision changes before commit | Commit-time preflight/CAS rejects stale intent |
| M06G-09 | Asset write succeeds but authoritative adoption fails | No formal reference; orphan recovery policy applies |
| M06G-10 | Commit succeeds but scene refresh fails | PendingReconciliation; no duplicate submission |
| M06G-11 | Application closes during unknown submission outcome | Reopen reconciles before retry |
| M06G-12 | User cancels after commit confirmation | Formal operation remains applied |
| M06G-13 | AI Task fails after formal commit | Formal operation remains applied |
| M06G-14 | Screen reader reviews Candidate and applies authorized Change Set | Candidate/selection/authorization/application states are separately announced |
| M06G-15 | Windows path passes but macOS path has no evidence | No cross-platform conformance claim |
| M06G-16 | UI displays a tool whose descriptor/handler is unverified | Tool remains Unverified/Blocked; no formal apply |
| M06G-17 | Road/Building/FunctionalArea proposal has no adopted descriptor | Specialist operation remains blocked |
| M06G-18 | User selects a dependency-closed subset | Selection may be partial; resulting Change Set remains all-or-none |

## 8. Final review statement

ME-06-00 and ME-06-A through ME-06-F are mutually consistent as a shared interaction/review/recovery design package when the Change Set atomic-boundary rule in G-02 is applied. The package preserves Core Domain authority, AI Assistant evidence ownership, Map Engine spatial computation, immutable Asset publication and one shared Editor Interaction Runtime.

**ME-06-G is complete as a cross-architecture design review working baseline. ME-06-00/A/B/C/D/E/F/G are complete as design working baselines.** This does not mean runtime implementation, Core Domain adoption, Windows/macOS validation or accessibility conformance is complete.

The immediate implementation priority is not to build every specialist tool. It is to establish one smallest end-to-end, evidence-backed typed operation path, then enable additional tools only when their own descriptor, transaction, validation and platform gates close.

## 9. Source baselines reviewed

- ME-06-00 Shared Interaction & Tool Contracts V0.3.
- ME-06-A Tool & Operation Capability Inventory V0.1.
- ME-06-B Input Routing & Gesture Lifecycle V0.1.
- ME-06-C Spatial Query Freshness V0.1.
- ME-06-D Typed Intent & Formal Operation Routing V0.1.
- ME-06-E Candidate Review & Selection Contract V0.1.
- ME-06-F Recovery, Accessibility & Acceptance Contract V0.1.
- ME-01-01 through ME-01-07 shared Editor architecture.
- ME-02-G/H, ME-03-G/H and ME-04-G/H specialist UX/integration packages.
- ME-05-02 through ME-05-08 geometry, spatial, Asset and Field package contracts.
- Core Domain P2-09 Command Descriptor, Object/Relationship identity, temporal, persistence and submission contracts as referenced by the source packages.
- AI Assistant Candidate/Validation/Selection/Change Set/Authorization contracts.
- AF-01 Application Foundation Architecture and AF-02 Implementation Report V0.1.
- Map Engine immutable scene, query freshness and geometry interaction contracts.

## 10. Change control

ME-06-G closes the shared interaction design review. Any later implementation refinement must preserve the ownership boundaries, four formal write paths, dependency-closed selection, one atomic commit boundary per Change Set, exact-version authorization, freshness revalidation, immutable Asset adoption, unknown-outcome recovery and accessibility requirements. New domain capabilities must be adopted by Core Domain/specialist owners rather than introduced by Editor convenience code.
