# World View — ME-06-F Recovery, Accessibility & Acceptance Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Shared recovery/accessibility/acceptance working baseline; no runtime test pass or implementation completion is implied
- Depends on: ME-06-00 V0.2; ME-06-A/B/C/D/E V0.1; ME-01-07; ME-05-06 V0.5; AF-01/AF-02 application lifecycle; Core Domain P2-09-12/13/15/17; AI Assistant Candidate/Selection/Submission contracts
- Authority: Shared Editor/Application recovery, accessibility and conformance contract. Domain-specific invariants remain with their owning specialist/Core Domain contracts.

## 1. Executive disposition

Adopt one shared recovery model for every editor mode. Recovery must preserve the distinction between:
- transient interaction state;
- AI Candidate/Validation/Selection evidence;
- staged immutable Assets;
- sealed Change Set and human authorization;
- committed Core Domain facts;
- derived Map Engine scene/query state.

The UI must report the furthest authoritative stage reached, not the last visible animation or attempted button action.

**V1 recovery rule:** retry only when the same immutable intent/proposal, target context and idempotency key are known. If the outcome is unknown, reconcile before retry. If the Core Domain commit is confirmed, never treat a failed scene refresh or later Task failure as a failed formal operation.

Accessibility is part of operation correctness: the primary workflows for selection, geometry authoring, Candidate review, confirmation, cancellation and diagnostics must remain operable without pointer precision and must expose status beyond color.

## 2. Recovery authority and non-goals

| State/data | Owner | Recovery rule |
|---|---|---|
| Active gesture and transient Edit Session | Editor Interaction Runtime | Cancel or preserve only as non-applicable draft; never auto-submit |
| Hit-test/snap/measurement/query result | Map Engine + ME-06-C | Discard stale result and re-query under a new valid token |
| Candidate payload/version | AI Assistant | Preserve immutable prior version; repair creates a new version |
| Validation Report/Issue | AI Assistant | Finalized evidence is immutable; rerun validation for new version |
| Selection Snapshot | AI Assistant | Immutable; revised decision creates a child snapshot |
| Change Set version/hash | AI Assistant | Immutable after seal; material change creates a new version |
| Human authorization | Human/Authorization service | Exact-version/hash binding; revocation/stale context blocks submission |
| Formal Command/definition/Field Revision | Core Domain owning service | Authoritative; no Editor/AI rollback |
| Core Asset bytes | Core Asset Store | Immutable; unreferenced bytes use safe orphan reconciliation |
| SceneSnapshot/spatial index/GPU buffers | Map Engine/Renderer | Disposable; rebuild from formal authority |

Non-goals:
- distributed retry orchestration, message brokers or multi-process recovery infrastructure;
- automatic semantic repair or AI authorization;
- silent rebase of stale formal operations;
- automatic undo of committed Core Domain Commands;
- multi-user collaboration, distributed locks or concurrent reviewers in V1;
- claiming accessibility conformance without platform/tool evidence.

## 3. Unified outcome vocabulary

The following outcomes are shared UI/application concepts. Domain-specific error codes remain typed and are mapped into these categories without losing their original code.

| Outcome | Formal mutation | Meaning | Default user action |
|---|---|---|---|
| CompletedLocally | No | Transient editor action completed | Continue editing or explicitly prepare intent |
| PreviewOnly | No | Query/preview exists but formal apply is unavailable | Inspect/export draft or leave unsupported state |
| Blocked | No | Known invariant, policy or adoption prerequisite prevents action | Resolve named prerequisite |
| Unsupported | No | No adopted capability exists for requested operation | Use supported workflow or defer |
| Unverified | No | Runtime evidence is insufficient | Fail closed; do not apply |
| InvalidInput | No | Typed input/schema/geometry is invalid | Correct draft and revalidate |
| Rejected | No | Human or authoritative validator rejected exact proposal/operation | Inspect reason; revise into new version if appropriate |
| Stale | No | Context, target, source, dependency or descriptor changed | Refresh/rebuild; re-review and reauthorize |
| Cancelled | No, unless commit was already confirmed | Uncommitted interaction was cancelled | Resume from safe draft or start again |
| Failed | No authoritative success | Attempt failed and no commit is confirmed | Retry only after classifying cause |
| OutcomeUnknown | Unknown | Process stopped or response lost before commit outcome was established | Reconcile; do not blindly retry |
| RecoveryRequired | Unknown/partially known infrastructure state | Automatic safe recovery cannot establish authoritative state | Block duplicate submission; inspect recovery diagnostics |
| PendingReconciliation | Yes, commit confirmed | Formal data committed; read model/scene refresh incomplete | Reconcile only; do not resubmit |
| Applied | Yes | Formal operation committed and authoritative read-back succeeded | Continue |
| Reconciled | Yes | Scene/read model reflects authoritative committed state | Resume normal interaction |

Do not collapse Stale, Failed, OutcomeUnknown, RecoveryRequired and PendingReconciliation into one generic error toast.

## 4. Failure classification and recovery matrix

### 4.1 Input and gesture failures

| Failure | Required behavior | Retry rule |
|---|---|---|
| Invalid geometry/input | Keep safe transient draft and diagnostics; no Typed Intent is emitted | Correct draft; validate again |
| User presses Escape before intent submission | Cancel current transient layer and release capture | New gesture only |
| Pointer capture/focus lost | Stop samples; mark Interrupted; discard or preserve a non-applicable local draft | Explicit user resume after context revalidation |
| Device disconnect/application deactivation | Never auto-close geometry or publish partial field stroke | Re-arm tool and begin a new gesture |
| Query returns after gesture cancellation | Discard by gesture/session/sequence token | New query under new gesture |
| User switches Map/Branch/Time/Scope mid-gesture | Cancel or mark Stale; never retarget | New gesture with a fresh context snapshot |

### 4.2 Query and validation failures

| Failure | Required behavior | Retry rule |
|---|---|---|
| Stale scene/source/transform | Discard result and mark Stale | Re-query with current token |
| Indeterminate/Unavailable predicate | Preserve uncertainty/coverage result; do not coerce to False | Change input or choose a supported operation |
| Validator Error | Candidate remains inspectable but cannot be selected for formal application | Repair creates a new Candidate Version and Validation Report |
| Validator Warning | Show exact warning and required acknowledgement policy | Acknowledge exact warning/report or revise |
| Validation cancelled | No successful validation evidence | Rerun against exact current version |
| Unsupported descriptor/handler | Fail closed; no generic CRUD fallback | Wait for adopted capability; do not retry unchanged |

### 4.3 Candidate and authorization failures

| Failure | Required behavior | Retry rule |
|---|---|---|
| Candidate edited after validation | Old version/report remain immutable; new version is unvalidated | Revalidate new version |
| Dependency missing/stale/cyclic | Block Selection Snapshot finalization and show dependency path | Add/repair dependency or revise Candidate aggregate |
| Selection changed after finalization | Preserve old snapshot; create child snapshot | New Change Set from new snapshot |
| Change Set content changed after sealing | New immutable Change Set version/hash | New human authorization required |
| Authorization revoked/denied | Do not submit | New explicit human decision if policy allows |
| Target context changed after authorization | Mark Stale and invalidate applicability | Re-preflight, reseal and reauthorize |
| Human confirmation UI cancelled | No authorization is recorded | Return to review; no submission |

### 4.4 Asset and formal commit failures

| Failure | Required behavior | Retry rule |
|---|---|---|
| Asset staging/write/hash failure | Do not create formal owner reference | Retry staging after checking local storage; same content hash if identical |
| Read-back verification fails | Asset is invalid for publication; no formal metadata commit | Recreate/reverify bytes |
| CAS conflict before metadata commit | No formal mutation | Refresh target and rebuild/review intent; new Change Set version and authorization where applicable |
| SQLite transaction fails before commit | All Core/AI database writes roll back together | Reconcile published but unreferenced immutable files; retry only after authoritative read |
| Process stops during commit and outcome is unknown | Do not assume rollback or success | Query authoritative submission/application evidence by idempotency key |
| Commit confirmed but scene refresh fails | Show PendingReconciliation | Rebuild read model/scene; never submit again |
| Renderer cache lost/corrupt | Formal data remains unchanged | Rebuild from Core Domain/Asset Store |
| Later AI Task fails after commit | Preserve formal operation and submission evidence | Task recovery is separate; no implicit undo |

## 5. Idempotency and retry rules

An apply attempt's idempotency key is bound to:
- immutable intent/proposal or sealed Change Set hash;
- exact target Map/Branch/History/write path;
- exact descriptor/handler compatibility;
- exact source/base fingerprints and Asset hashes.

Rules:
1. Same key + same immutable payload/context returns the prior known outcome or resumes safe reconciliation.
2. Same key + changed payload, target, context, descriptor or authorization binding is an idempotency conflict.
3. A new key is used only for a genuinely new semantic operation or a new immutable Change Set version.
4. A retry must never bypass a failed preflight or re-use stale authorization.
5. After authoritative commit, recovery is read-back/reconciliation, not a second apply.
6. OutcomeUnknown and RecoveryRequired block user-visible “Retry Apply” until the owning service resolves the original attempt.
7. A failed UI response does not prove a failed database transaction.

For one sealed Change Set, Core Domain submission remains all-or-none. A dependency-closed subset may be partially accepted before sealing; one Change Set is never partially applied in V1.

## 6. Undo, cancellation and recovery checkpoints

### 6.1 Local undo

Local Undo may reverse:
- current uncommitted geometry draft steps;
- current transient field brush stroke/batch;
- uncommitted Inspector draft changes;
- temporary selection/filter/overlay state.

Local Undo may not reverse:
- committed Core Domain Commands;
- published Field Revisions;
- finalized Selection Snapshots;
- sealed Change Set versions;
- human authorization or submission evidence.

Formal reversal requires an explicit supported inverse/new History operation or a new reverse Change Set under the normal authorization path.

### 6.2 Cancellation boundary

- Before authoritative commit: cancellation may abort staged work and discard transient state.
- During an authoritative transaction: cancellation is best-effort; determine commit outcome from the owning service.
- After commit confirmation: cancellation stops UI follow-up only; it does not roll back formal state.
- During PendingReconciliation: cancellation cannot mark the operation Failed or permit duplicate submission.

### 6.3 Recovery checkpoint

V1 does not persist every pointer movement or transient gesture sample. The Editor may preserve a compact non-applicable recovery draft only when it can safely store:
- active tool and draft type;
- stable target references;
- captured context token/fingerprints;
- draft payload and source Asset references;
- last completed local edit step;
- explicit reason for interruption.

On resume, the draft is Stale until target identity, source revisions, coordinate context and capability are revalidated. If safe serialization is not available, discard it with a clear message. Never silently reconstruct a formal intent from an incomplete gesture.

## 7. Accessibility and input parity acceptance

Accessibility is part of functional acceptance, not a visual polish phase.

### 7.1 Required parity

For every V1 core workflow, provide a discoverable keyboard/accessible route for:
- tool activation and focus indication;
- selecting and inspecting Objects/Components/Candidates;
- starting, completing and cancelling supported gestures;
- editing vertices/components through a non-pointer path where the tool supports editing;
- navigating validation issues and dependency blockers;
- comparing Candidate versions;
- selecting/rejecting Candidate Versions and finalizing a Selection Snapshot;
- reviewing the exact Change Set and invoking the explicit human authorization action;
- recovering from Stale, Blocked, Failed and PendingReconciliation outcomes.

A workflow that fundamentally requires precise pointer movement must provide an equivalent keyboard/accessibility alternative or be explicitly marked unsupported for that input mode.

### 7.2 Status communication

- Color is never the only signal for formal content, Candidate overlay, selection, warning, error, stale or applied state.
- Each status includes text and a semantic icon/shape plus an accessible name.
- Screen-reader announcements distinguish “Selection saved” from “Map change applied”.
- Validation Errors/Warn­ings expose severity, issue code/title, affected target and suggested action.
- Focus remains visible and stable when Canvas overlays or Inspector panels update.
- Keyboard cancellation never silently triggers a destructive domain action.
- High-contrast and display scaling must preserve hit targets, focus rings and status text.

### 7.3 Device behavior

- Mouse and keyboard are mandatory V1 input paths.
- Pen/touch support is required only where the platform adapter and tool explicitly declare it; unsupported multi-touch gestures must not accidentally create geometry.
- Pointer capture loss, device disconnect, DPI changes and window focus changes have explicit cancellation/revalidation behavior.
- Windows and macOS shortcut conflicts are checked separately; a shortcut is not frozen solely because it works on one platform.

## 8. Test evidence and acceptance levels

Do not use one “tests passed” label for several different proof levels.

| Level | Evidence | What it proves |
|---|---|---|
| Unit | Pure state transition, validator, idempotency and mapping tests | Local deterministic rules |
| Service integration | Application/AI/Core Domain/Asset Store transaction tests | Ownership, CAS, atomicity and recovery behavior |
| UI automation | Real desktop UI workflow with keyboard/pointer and accessibility tree | Interaction and status behavior |
| Platform integration | Packaged Windows/macOS runtime, actual SQLite provider and filesystem | Platform-specific lifecycle, durability and Asset behavior |
| Manual accessibility review | Keyboard-only and screen-reader walkthrough with recorded findings | Usability/accessibility gaps not covered by automation |

A mocked service test cannot prove SQLite transaction atomicity. A unit test cannot prove platform keyboard behavior. A Windows pass does not imply a macOS pass.

Every test report records:
- OS and version;
- CPU architecture;
- application build/commit;
- .NET SDK/runtime and relevant provider versions;
- test layer and exact test command;
- scenario IDs and total pass/fail/skip counts;
- failure logs/screenshots/accessibility tree where relevant;
- known limitations and untested platform/input modes.

## 9. Conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| M06F-01 | Cancel a geometry gesture before completion | No Typed Intent or formal mutation |
| M06F-02 | Pointer capture lost during polygon drawing | Interrupted; no auto-close or apply |
| M06F-03 | Late snap result returns after gesture cancel | Discarded by session/sequence token |
| M06F-04 | Map/Branch/Time changes during gesture | Cancelled/Stale; never retargeted |
| M06F-05 | Query result is Indeterminate | Preserve uncertainty; no Boolean coercion |
| M06F-06 | Candidate repair after validation Error | New Candidate Version and new report |
| M06F-07 | Missing Candidate dependency | Selection finalization blocked |
| M06F-08 | User changes finalized selection | New immutable Selection Snapshot |
| M06F-09 | Candidate selection is partially accepted | Dependency-closed subset only; no formal Map mutation |
| M06F-10 | One Change Set contains several Commands and one is invalid | Entire Change Set rejected; no partial persistence |
| M06F-11 | Asset file written but DB transaction fails | No formal reference; orphan reconciliation |
| M06F-12 | Process exits during submission with unknown outcome | OutcomeUnknown; no blind retry |
| M06F-13 | Same idempotency key retried with identical payload | Same authoritative outcome; no duplicate |
| M06F-14 | Same idempotency key reused with changed payload | Idempotency conflict |
| M06F-15 | Commit succeeds but scene refresh fails | PendingReconciliation; duplicate apply blocked |
| M06F-16 | Renderer cache is deleted | Rebuild from formal authority; no formal data loss |
| M06F-17 | User cancels after commit confirmation | Formal change remains committed |
| M06F-18 | AI Task fails after successful formal apply | Core Domain Command remains |
| M06F-19 | Keyboard-only selection and inspection | All primary actions reachable with visible focus |
| M06F-20 | Keyboard-only Candidate review | Compare, select/reject, issue inspection and snapshot finalization reachable |
| M06F-21 | Screen reader announces selection finalization | Announces Selection Snapshot, not Map application |
| M06F-22 | Screen reader announces successful submission | Announces authoritative applied/reconciled state |
| M06F-23 | High-contrast mode | Status remains distinguishable without color |
| M06F-24 | Windows display scaling changes during a gesture | Coordinate/viewport handling is consistent or gesture safely cancels |
| M06F-25 | macOS window deactivation during a gesture | Capture released; no unintended completion |
| M06F-26 | Focus moves from Canvas to Inspector during text composition | IME/text editing is not mistaken for a geometry gesture |
| M06F-27 | User attempts Ctrl+Z after formal apply | UI does not imply committed Command was undone |
| M06F-28 | A tool has no keyboard alternative | Explicitly marked unsupported for keyboard-only workflow; not falsely accepted |
| M06F-29 | Stale authorization after target revision change | Submission blocked; new preflight/seal/authorization required |
| M06F-30 | Independent Change Sets commit separately | Each transaction outcome is reported independently; no synthetic partial batch status |

## 10. Implementation decomposition

Keep implementation responsibilities small and local:
- Outcome Mapper: maps typed Domain/Application results into shared UI states.
- Recovery Coordinator: classifies retry/reconcile/cancel behavior by commit stage.
- Draft Recovery Store: optional local-only persistence of safe non-applicable drafts.
- Accessibility Adapter: semantic labels, focus, announcements and keyboard command routing.
- Test Harness: deterministic fake query/Domain outcomes plus real provider/platform integration suites.
- Evidence Reporter: records platform/test provenance and scenario outcomes.

These are logical responsibilities, not separate processes or a generalized workflow engine.

## 11. Definition of done

ME-06-F is complete as a design baseline when:
1. Every shared failure/outcome state has a clear mutation/commit meaning.
2. Retry, idempotency, unknown outcome and PendingReconciliation rules are explicit.
3. Gesture interruption and local Undo cannot alter committed Core Domain state.
4. Candidate selection, authorization and atomic Change Set application remain distinct.
5. Asset write/DB failure and orphan recovery are covered.
6. Keyboard, screen-reader, focus, high-contrast and device interruption acceptance scenarios exist.
7. Windows/macOS evidence requirements distinguish unit, service, UI and platform integration proof.
8. Test reports cannot claim a platform or accessibility pass without matching evidence.
9. ME-06-00 and the Work Plan are updated with the F package and remaining G review.

**Disposition:** ME-06-F is complete as a recovery/accessibility/acceptance design working baseline. It does not claim implementation or runtime accessibility conformance. Next: ME-06-G — Cross-architecture Integration Review.
