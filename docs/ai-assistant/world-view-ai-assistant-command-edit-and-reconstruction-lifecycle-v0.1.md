# World View AI Assistant — Command Edit and Reconstruction Lifecycle

- Version: 0.1
- Date: 2026-11-21
- Status: Design working baseline — pending formal Core Domain review
- Scope: AI Assistant Task Planning & Execution Engine V1.0
- Storage target: Single-user, local-first desktop application
- Related contracts:
  - `world-view-ai-assistant-core-domain-integration-contract-v0.2.md`
  - `world-view-ai-assistant-command-proposal-and-authorization-design-v0.1.md`
  - `world-view-ai-assistant-ordering-and-history-time-contract-v0.1.md`

## 1. Purpose

This document defines how AI-owned Command Proposals and their authorization evidence interact with author-editable Core Domain Commands, history reconstruction, Branch inheritance, and derived Checkpoints.

It closes a lifecycle gap in the AI Assistant design: authorization is immutable evidence for one exact proposed submission, while Core Domain history remains author-editable. A later edit to a submitted Command must not rewrite the old authorization record, but neither may that old authorization be treated as proof that the newly edited history remains valid.

This is an integration contract, not a replacement for Core Domain authoring or reconstruction rules.

## 2. Authority and source status

### 2.1 Ownership

| Concern | Authority |
|---|---|
| Formal Map, Branch, History and Command records | Core Domain |
| Command edit, delete, insertion and Historical Time change | Core Domain authoring services |
| Explicit/implicit dependency evaluation and ordering | Core Domain |
| World State reconstruction and valid playback boundary | Core Domain |
| Checkpoint creation, invalidation, verification, reuse and eviction | Core Domain |
| Candidate Sets, Candidate Versions and validation evidence | AI Assistant |
| Command Proposal snapshots, Change Set versions and content hashes | AI Assistant |
| Human authorization evidence for an exact proposal | AI Assistant integration boundary; human is the authorizer |
| Submission/reconciliation evidence | AI Assistant integration boundary, with atomic persistence placement still to be verified |

AI Assistant must not directly mutate Core Domain tables or maintain shadow Commands, Branch revisions, Object versions, World States, Checkpoints, or a parallel dependency graph.

### 2.2 Status of source decisions

The relevant P2-01-D01 discussion record covers D01-01~143 and is marked **Proposed / Discussion** overall. Many entries record choices or confirmations made by the user, but the discussion record itself explicitly says that it is not a frozen formal architecture baseline and does not authorize implementation.

This contract therefore:
- preserves the meaning of the recorded decisions;
- identifies them as stage-confirmed discussion rules where applicable;
- does not relabel them as Accepted/Frozen ADRs;
- leaves unresolved implementation details with Core Domain review.

Frozen ADR-001~005 remain authoritative within their accepted scope. In particular, Branch inheritance semantics in those ADRs must not be silently replaced by a discussion-stage interpretation.

## 3. Three independent lifecycle dimensions

The system must keep three dimensions separate.

### 3.1 Proposal and authorization lifecycle

This answers: *What exact content did the AI propose, and what exact content did the human authorize?*

A proposal version is an immutable snapshot once sealed. Authorization binds to that exact version, canonical content hash, resolved target History context, and accepted ordering/time semantics.

### 3.2 Formal authoring-history lifecycle

This answers: *Which Commands currently exist in the authoritative Core Domain History, and what author edits have been made to them?*

Core Domain Commands are authoritative authoring records but are not immutable version entities. The author may directly edit or delete an existing Command, or insert a new Command. No Command Revision chain is required by D01-08.

### 3.3 Reconstruction and playback lifecycle

This answers: *How much of the current authoritative history has been successfully reconstructed, and what timeline range is safe to play?*

A persisted Command does not imply a successful rebuild. A rebuild failure does not delete the Command. Playback is limited to the most recent successfully reconstructed valid range.

These dimensions must never be collapsed into one generic `applied` or `completed` status.

## 4. Proposal freshness and authorization validity

### 4.1 Before authorization

A draft proposal may be edited freely within the Task's authorized goal, scope and budget.

Any semantic change to:
- Command type or payload;
- Candidate Version reference;
- target Map, Branch or resolved History scope;
- Historical Time, precision or duration;
- Object identity/reservation reference;
- explicit ordering intent or accepted ordering relation;
- asset reference or asset content hash;
- Core Domain target-context precondition;

creates a new proposal-content version or otherwise changes the sealed manifest. It must not mutate a previously sealed snapshot in place.

### 4.2 After authorization but before submission

Authorization applies only to the exact sealed proposal content and exact target context.

If any semantic field changes, or Core Domain reports that the target context is stale:
1. mark the old authorization as no longer usable for submission;
2. retain it as immutable historical evidence;
3. create a new proposal-content version;
4. re-run relevant Core Domain preflight and validation;
5. obtain new human authorization.

Do not carry authorization forward, rebase it automatically, or infer approval from the fact that the user approved an earlier version.

A display-only change that does not affect the canonical semantic manifest need not invalidate authorization. The canonicalization rules must explicitly exclude presentation-only fields.

### 4.3 At final submission

Immediately before writing, Core Domain must revalidate:
- the exact authorized content hash;
- the target History context and applicable inheritance path;
- command descriptors and payload validity;
- Branch Historical Time constraints;
- Object identity/reservation preconditions;
- accepted ordering constraints and their preconditions.

A stale target or mismatch rejects the entire batch. No partial Command persistence is allowed.

If the commit result is uncertain, reconcile against authoritative Core Domain History and exact proposal/submission identifiers. Never blindly replay the batch.

## 5. What happens after formal submission

### 5.1 Successful submission

A successful submission means that Core Domain persisted the authorized Commands (and accepted formal ordering data, where included) into authoritative History.

The AI Assistant stores immutable submission evidence:
- submitted proposal/version identity and content hash;
- human authorization evidence;
- submission attempt identity;
- exact Core Domain Command references returned;
- target Map/Branch/History references;
- commit outcome and timestamp;
- any Core Domain diagnostics.

This evidence describes what was submitted at that time. It does not freeze those Commands against later author edits.

### 5.2 Later author edits or deletes

If the user later edits, deletes or changes the Historical Time of a submitted Command:
- the operation is a Core Domain authoring operation;
- Core Domain updates the authoritative Command according to its own rules;
- the original AI proposal and authorization evidence remain unchanged;
- the old submission report remains a historical record of the original submission;
- current history/reconstruction status is refreshed from Core Domain;
- affected derived State Changes, World State results, dependency evaluations and Checkpoints are invalidated/recomputed according to Core Domain rules.

The AI Assistant must not:
- rewrite the old proposal to resemble the new Command;
- alter or revoke the historical fact that the earlier content was authorized and submitted;
- create a fake new authorization record for the user's manual edit;
- automatically generate a compensating Command;
- directly update/delete the formal Command.

If the user asks AI to make a subsequent change, AI creates a new proposal and obtains authorization for that new operation. The new proposal may refer to the current Command identity, but must not pretend that it is the original submitted content.

### 5.3 Command insertion

A newly inserted Command is a new authoring operation. If AI-originated, it requires its own proposal and authorization under the normal flow. If user-authored directly, it is recorded by Core Domain without inventing an AI authorization.

The insertion may affect ordering, dependencies, derived State Changes, reconstruction and descendant Branches. AI must query Core Domain for the resulting impact rather than calculate a shadow history.

### 5.4 Distinguish historical authorization from current authorization

An old authorization is neither “invalidated history” nor “current permission to edit.” It is immutable evidence that a particular payload was approved for a particular submission attempt.

It must never be reused as permission for a later edit, resubmission or different target context.

## 6. Command edits and reconstruction behavior

The following rules are recorded in D01-08 and D01-13~16. They remain discussion-stage rules pending formal Core Domain review.

| Core Domain authoring operation | Recorded impact |
|---|---|
| Edit Command payload/route | Invalidate affected derived results; rebuild from the Command's effective point; re-evaluate dependencies and warnings |
| Change Historical Time | Rebuild from the earlier of the old and new effective times |
| Delete Command | Delete only that Command; retain dependent Commands; re-evaluate dependencies; invalidate affected derived results |
| Insert Command at an arbitrary Historical Time | Rebuild from the earliest affected time; retain later Commands; re-evaluate dependencies |
| Edit ordering constraint | Re-evaluate explicit and implicit dependencies; preserve unresolved user intent rather than silently deleting constraints |

These operations are not AI-side CRUD against formal map state.

### 6.1 Normal editing mode

D01-62/63 record the following normal workflow:
- the user may make several Command edits without immediately rebuilding after each one;
- timeline playback is paused while authoring content is unrebuilt;
- the user explicitly triggers “rebuild / replay” after the editing batch;
- the system determines the earliest affected Historical Time and reuses only still-valid earlier results;
- the first reconstruction error stops the rebuild.

### 6.2 Failed-history repair mode

D01-97~100 record a distinct restricted repair mode after a rebuild has failed:
- the user may repair the failed Command and permitted earlier Commands;
- each saved repair triggers another rebuild attempt;
- Commands after the current failure point remain locked until the earlier history is valid;
- a descendant Branch blocked by an invalid ancestor is read-only and cannot repair its own Commands until the ancestor is repaired.

This is not a contradiction with normal editing mode. The two modes have different entry conditions:
- normal authoring: batch edits, then user-triggered rebuild;
- failed-history repair: save a permitted repair and immediately re-test the failed history.

## 7. Fail-Fast reconstruction and playback

D01-58/59 record compiler-like Fail-Fast behavior:
1. execute Commands in the applicable historical order;
2. stop at the first unexecutable Command;
3. report only the first error;
4. do not execute later Commands as if they formed a valid complete history;
5. retain later Commands in authoritative History;
6. allow the author to repair and rebuild.

D01-61 records that playback is available only through the latest successfully reconstructed time. The failing point is shown as an error; later unverified history must not be played.

Consequently, an Application Report must distinguish at least:

| Dimension | Example states |
|---|---|
| Submission | Not submitted / Persisted / Rejected / Outcome uncertain |
| Authoring context | Current / Stale / Reconciliation required |
| Reconstruction | Not requested / Pending / Running / Succeeded / Failed / Blocked |
| Playback | Available through time T / Unavailable beyond T |

Do not report “Apply succeeded” as “history rebuild succeeded.”

D01-62's user-triggered rebuild rule applies to ordinary author edits. Cache self-healing after Checkpoint corruption is a separate operation and may be automatic under D01-118/119.

## 8. Branch inheritance and invalidation

### 8.1 Selected-path reconstruction

D01-70/76 discussion decisions describe Shared Object identity as distinct from shared mutable runtime state. The selected Branch is reconstructed along its applicable inheritance path; the same Shared Object may therefore have different runtime states in different Branches.

D01-76 explicitly rejects an architecture that interleaves an independent Shared Object History with Branch-local History as two separate authoritative streams.

However, the overall D01 record remains Proposed / Discussion, and the exact integration with frozen ADR-003 shared-history scope still requires formal Core Domain resolution. AI Assistant must remain scope-agnostic and use the History target/scope returned by Core Domain.

### 8.2 Ancestor edits

D01-71 and D01-79/85/86/87/94/107 discussion rules describe:
- ancestor changes within the adopted inheritance boundary affect descendants;
- changes outside the inherited boundary do not affect that descendant;
- descendant-owned Commands are not automatically edited or deleted;
- affected Checkpoints become logically invalid along affected inheritance paths;
- all affected Branches may be marked stale, but only the active path is immediately rebuilt;
- inactive Branches rebuild on demand when selected;
- if an ancestor changes while a rebuild is running, the old task loses publication rights and its result must be discarded;
- publication requires a final complete inherited-path version check.

The exact lineage/version mechanism is Core Domain-owned. AI Assistant must not implement it with its own Branch revision numbers or checkpoint-derived tokens.

### 8.3 Branch failure and inheritance blocking

D01-81/83/100 distinguish:
- **rebuild failure**: a Branch's own Command fails; that Branch may enter restricted repair mode;
- **inheritance blocked**: the Branch cannot obtain a valid ancestor state; it is read-only and cannot repair its own Commands.

A failed Branch blocks its own descendants, not unrelated Branches. Repair the ancestor first; then Core Domain propagates eligibility along the active path and leaves inactive paths for on-demand rebuild according to D01-82/94.

## 9. Checkpoint lifecycle and candidate isolation

Checkpoints are derived caches, never authoritative history.

The discussion decisions D01-77/88~93 and D01-101~123 record:
- Checkpoints are associated with a Branch and Historical Time;
- an effective Checkpoint is unique for a Branch/time point;
- a Checkpoint may be reused along a descendant's inheritance path when valid;
- it records the complete inherited-path version combination at generation time;
- the full path must be validated before use and again before publication;
- a logically invalid Checkpoint is never usable, even if its bytes remain temporarily present due to active references;
- referenced Checkpoints cannot be physically evicted until references are released;
- generated Checkpoints publish atomically; incomplete or failed output is never usable;
- corruption of one Checkpoint does not logically invalidate other Checkpoints;
- corruption does not propagate along the inheritance path;
- an active Branch's corrupt Checkpoint is automatically rebuilt; an inactive Branch's corrupt Checkpoint is handled on demand;
- no manual single-Checkpoint deletion is provided; users may request a full Checkpoint regeneration operation.

Candidate data and AI staging assets are not Checkpoints and must not be placed in the Core Domain Checkpoint namespace. Candidate validity is governed by exact Candidate Version and validation evidence. Checkpoint validity is governed by Core Domain's complete inherited-path validation.

The precise D01-77 restoration strategy remains explicitly unresolved in the source discussion record and must not be filled in by assumption.

## 10. AI-side persistence model implications

The AI Assistant database should preserve the following evidence without becoming a second Core Domain authority.

### 10.1 Retain

- immutable proposal/change-set content versions;
- canonical content hash and hash algorithm/version;
- human authorization bound to exact proposal version/hash and target context;
- submission attempt identity and idempotency identity;
- exact Core Domain Command references returned by submission;
- submission outcome and reconciliation state;
- a historical Application Report snapshot;
- latest observed Core Domain reconstruction/playback status as a refreshable read model, clearly marked as non-authoritative.

### 10.2 Do not add

- AI-owned Command revision chains;
- AI-owned Branch revision or Object version entities;
- AI-owned World State or Checkpoint tables;
- a second authoritative dependency/order graph;
- an AI-maintained “current Command content” copy that can diverge from Core Domain;
- a generic compensating-Command or reverse-Change-Set mechanism.

### 10.3 Suggested separation of records

The physical table names remain subject to schema redesign, but conceptually separate:
1. `proposal_versions`: immutable AI-owned proposal snapshots;
2. `proposal_authorizations`: immutable human approvals of exact snapshots;
3. `submission_attempts`: immutable attempt identity and commit/reconciliation outcome;
4. `submission_command_refs`: returned Core Domain Command references for a successful submission;
5. `application_reports`: historical submission result plus distinct rebuild/playback observations.

Do not duplicate fields unnecessarily. If an atomic single-database transaction can persist Core Domain Commands and minimal submission evidence together, prefer that. The exact transaction owner and connection boundary remain unverified.

A later user edit to a Core Domain Command does not mutate these records. The current Core Domain state is queried when needed.

## 11. Required integration capabilities

Core Domain should expose conceptual capabilities for:
1. retrieving a Command by stable reference and current authored content;
2. editing/deleting/inserting Commands through domain authoring operations;
3. returning affected History/Branch scope and earliest affected Historical Time;
4. exposing authoring-context freshness and applicable inheritance-path identity/version;
5. reporting current reconstruction status and first failure diagnostic;
6. reporting valid playback boundary;
7. reporting Checkpoint validity and reuse eligibility;
8. reconciling a prior submission attempt against authoritative History;
9. preventing duplicate persistence for an already committed attempt.

These are capability requirements, not a prescribed API or table design. If a capability is absent, record it as a Core Domain integration gap; do not emulate it by direct SQL against Core Domain tables.

## 12. Verification scenarios

Before approving the redesigned database schema, verify at least:

1. Editing a draft proposal after sealing creates a new immutable proposal version.
2. Authorization binds to one exact proposal version and canonical hash.
3. A target-context change before submission rejects the old authorization.
4. An authorized proposal cannot be silently rebased to a changed Branch/History context.
5. A successful submission records exact Core Domain Command references.
6. A lost response after commit is reconciled without duplicate Commands.
7. A later manual edit to a submitted Command leaves original proposal and authorization evidence unchanged.
8. A later manual edit does not inherit the old AI authorization as permission for another submission.
9. A later AI-requested change creates a new proposal and authorization.
10. Deleting a Command does not automatically delete dependent Commands.
11. Inserting a Command at an earlier Historical Time retains later Commands.
12. Editing Historical Time rebuilds from the earlier of old/new times.
13. Normal editing pauses playback until the user triggers rebuild.
14. Failed-history repair uses the restricted repair permissions and immediate rebuild behavior.
15. Fail-Fast stops at the first execution error and preserves later Commands.
16. Playback never extends beyond the latest successfully rebuilt time.
17. An ancestor edit invalidates only affected inherited paths and their Checkpoints.
18. An obsolete rebuild task cannot publish after lineage changes.
19. An inactive Branch is not automatically rebuilt merely because its Checkpoint became invalid.
20. A descendant blocked by ancestor failure cannot edit its own Commands.
21. A corrupt Checkpoint does not invalidate unrelated Checkpoints.
22. Checkpoint publication is atomic and requires final full-path validation.
23. Candidate versions and assets cannot be mistaken for formal Checkpoints.
24. No AI-side table becomes a shadow Command, Branch, Object, World State or Checkpoint authority.

## 13. Open formalization gates

1. **D01 status gate:** formally review and freeze applicable D01 decisions before treating them as implementation authority.
2. **Shared Object gate:** reconcile ADR-003 shared-history scope with D01-70/76 selected-path reconstruction.
3. **Command contract gate:** inspect the actual Core Domain Command catalog, payload descriptors, authoring services and validation rules.
4. **Lineage gate:** define the exact Core Domain representation of inherited-path identity/version and stale-context preconditions.
5. **Rebuild gate:** inspect actual rebuild orchestration, state transitions, playback boundary and repair permissions.
6. **Checkpoint gate:** inspect physical storage, integrity verification, reference protection and publication transaction.
7. **Transaction gate:** prove how AI submission evidence and Core Domain Command persistence can share one local atomic transaction, or define a safe reconciliation contract if not.
8. **Schema gate:** inspect the actual Core Domain SQLite migrations and keys before designing final AI foreign keys or migration order.

## 14. Current conclusion

The correct lifecycle is:

- AI creates immutable proposal snapshots.
- Human authorization applies only to the exact sealed proposal and target context.
- Core Domain persists formal Commands.
- The author may later edit or delete those Commands under Core Domain rules.
- Historical authorization evidence remains immutable and is never reused for later changes.
- Reconstruction is a separate operation with Fail-Fast behavior.
- Playback is limited to successfully reconstructed history.
- Branch inheritance and Checkpoint validity remain exclusively Core Domain concerns.

This closes the proposal-to-history lifecycle at the design-contract level. It does **not** make the AI Assistant database implementation-ready: physical Core Domain schema, Command descriptors, transaction ownership and formal D01 review remain outstanding.