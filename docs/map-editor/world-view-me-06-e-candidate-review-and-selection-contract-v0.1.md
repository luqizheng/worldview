# World View — ME-06-E Candidate Review & Selection Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Candidate review interaction working baseline; no database migration, authorization-policy change or runtime implementation is implied
- Depends on: ME-06-00 V0.2; ME-06-A/B/C/D V0.1; ME-04-G/H; ME-05-06 V0.5; AI Assistant Candidate/Validation Schema V1.0; Selection/Application Schema V1.0; Command Proposal & Authorization Design V0.1; P2-09-13/15/17/26/27
- Authority: Shared Editor review interaction and AI selection boundary. Candidate/Selection/Change Set evidence belongs to AI Assistant; formal map changes belong to Core Domain Domain Services.

## 1. Executive disposition

Adopt one shared Candidate Review flow for AI-generated or otherwise review-required proposals:

**Candidate Set → exact Candidate Versions → validation/evidence inspection → dependency-aware selection → immutable Selection Snapshot → Change Set construction → preflight → explicit human authorization → atomic Core Domain submission → authoritative reconciliation.**

The most important separation is:

- **Candidate** is a proposal/staging result.
- **Selection** is a human decision about exact Candidate Versions.
- **Change Set** is the exact batch of formal operations prepared from that selection.
- **Authorization** grants permission to submit one sealed Change Set version.
- **Application** occurs only when Core Domain confirms its atomic formal write.

Selecting or accepting Candidates never changes formal Map data. The review UI must not collapse these steps into one ambiguous “Accept and Apply” action.

ME-06-E defines the shared interaction and selection rules. It does not introduce new Core Domain Object/Command/Relationship types, a generic Candidate dependency database, or an alternative formal-write path.

## 2. Existing authority and schema baseline

The current AI Assistant contracts already establish the required evidence model:

- `candidate_sets` represent independent coherent alternatives produced or assembled by a Task.
- `object_candidates` provide stable candidate identities; they are not formal Object identities.
- `object_candidate_versions` are immutable payload versions. Editing creates a new version.
- `validation_reports` and `validation_report_items` pin exact versions examined by a validation run.
- `validation_issues` are immutable findings. A repair creates a new Candidate Version and a new Validation Report.
- `selection_snapshots` and `selection_items` preserve one immutable human decision over exact Candidate Versions.
- `change_set_versions` are immutable, hashed application batches.
- `change_authorizations` bind a human decision to one exact sealed Change Set version and hash.
- Submission evidence distinguishes committed, rejected, unknown and recovery-required outcomes.

The Candidate Review UI must use these existing authorities. V1 adds no Candidate Review, Candidate Dependency, Review Session or Candidate Comparison tables.

A review panel's temporary expansion, filter, hover, preview camera and draft selection state is presentation/session state. Only finalizing a Selection Snapshot creates durable decision evidence.

## 3. Candidate Set and Candidate Version semantics

### 3.1 Candidate Sets are alternatives, not shared mutable pools

One Candidate Set represents one coherent alternative result. Different Candidate Sets from the same Task remain independent alternatives; editing one does not mutate another.

The user may compare multiple Candidate Sets. The user may also explicitly select compatible Candidate Versions from more than one set when that combination is coherent, belongs to the same Task/target context, passes dependency closure and contains no identity conflict. Cross-set composition is never inferred from visual similarity.

Candidate Set status is a summary convenience:
- `generating`: candidate content is still being produced;
- `validating`: a validation run is in progress;
- `ready`: exact versions have completed validation and may be reviewed;
- `under_review`: a human review session is active;
- `accepted`: the finalized selection accepts the complete relevant set according to its selection manifest;
- `partially_accepted`: the finalized selection accepts a coherent subset;
- `rejected`: the user explicitly rejects the set;
- `superseded`: the set is no longer the current alternative.

The authoritative item-level decision is the Selection Snapshot, not this mutable set status.

### 3.2 Candidate Versions are immutable

The review surface always identifies:
- Candidate Set ID/name;
- Candidate ID;
- exact Candidate Version ID and version number;
- exact payload content hash;
- formal Object ID or permanent reservation reference, when applicable;
- base Object/Branch context used to generate the version;
- source/provenance references;
- latest applicable Validation Report and its exact report-item membership.

A new version is created for every material edit, including geometry, attributes, classification, source provenance, target context, dependency manifest or formal intent change. The previous version remains inspectable and is never silently overwritten.

A new version is unvalidated until a new Validation Report completes. Previous selection or authorization does not carry forward.

### 3.3 Candidate preview is not formal Map content

Candidate geometry and attributes render as a clearly distinct overlay. The user can isolate, compare, hide and inspect them without changing the formal scene.

Candidate payload Assets remain AI staging Assets. They do not become Core Assets or formal Geometry Components merely because they are visible in the Canvas. Formal Asset adoption occurs only through the owning publication/Domain Service path during application.

## 4. Candidate dependency and conflict contract

Partial selection is safe only when the selected subset is dependency-closed. The existing AI database has no separate Candidate Dependency table. V1 therefore uses a small typed dependency manifest inside each immutable Candidate Version payload manifest.

### 4.1 Required dependency references

A Candidate Version may declare `required_candidate_versions[]`, each containing:
- exact `candidate_id`;
- exact `candidate_version_id`;
- dependency reason/category;
- whether the dependency is mandatory for semantic validity.

The dependency list is immutable with that Candidate Version and is included in its payload hash. A dependency must pin an exact version, not a mutable Candidate ID/current-version pointer.

Examples:
- a Building Space Region Candidate requires its Building Level and Level Slice Candidates if those are not already present in the formal target state;
- an Opening Candidate requires its source Boundary Candidate Version;
- an Access Connection Candidate requires its endpoint Space/Entrance Candidates;
- a Cave Connection Candidate requires the referenced Chamber/Passage endpoint Candidates.

References to already formal Objects/children belong in typed target/source references and are re-resolved against Core Domain. They are not represented as Candidate dependencies.

### 4.2 Dependency rules

- A selected Candidate Version must include every required Candidate Version in the same finalized Selection Snapshot, unless the dependency is proven to exist in the current formal target state.
- Dependency references must belong to the same Task and compatible target Map/Branch/History context.
- Missing, superseded, stale or incompatible required versions block finalization.
- Dependency cycles are invalid in V1. Mutually dependent records should be represented as one typed aggregate Candidate rather than a cyclic graph.
- Candidate dependency is not Command ordering. The Change Set's formal ordering intent remains a separate descriptor-validated concern.
- A dependency edge cannot be silently dropped to make a partial selection pass.

The review UI must show both the direct dependencies and the transitive closure of every selected item. It must explain which dependency is already satisfied by formal target state and which one must be included in the selection.

### 4.3 Conflict rules

- At most one Candidate Version for a given formal `object_id` may be selected in one Selection Snapshot. The existing partial unique index remains authoritative.
- Competing Candidate Versions for the same Object are shown as alternatives and compared side by side.
- A Candidate Version that conflicts with another selected item through a typed validation rule is blocked even if the Object IDs differ.
- The review UI never resolves a semantic conflict by silently choosing the newest, highest-scoring or AI-preferred version.
- The user may choose one alternative, request a revision, or reject the conflicting proposal set.

## 5. Validation and evidence presentation

A Candidate is eligible for selection only when the exact Candidate Version has an applicable completed Validation Report.

### 5.1 Required validation states

| Validation state | Review behavior |
|---|---|
| No report / outdated report | Show Not Validated or Stale; selection is blocked |
| Running | Show progress; finalization is blocked |
| Passed | Eligible, subject to dependency and target checks |
| Passed with Warnings | Eligible only after required warning acknowledgement |
| Failed | Block selection; show Errors and repair actions |
| Cancelled | Not eligible; rerun validation if the user wants to continue |

Error findings block selection. Warnings are never silently treated as a clean pass. A warning acknowledgement records the exact issue/report/version and is included in the Selection Snapshot/Change Set evidence where it affects authorization.

A repair creates a new Candidate Version and new Validation Report. The UI must not mutate an Error Issue into a success state.

### 5.2 Evidence shown per item

The inspector presents:
- proposed semantic meaning and creator-readable explanation;
- added/modified/removed geometry and attributes relative to the pinned base;
- exact source Candidate Version and content hash;
- formal target identity/reservation and target Map/Branch/Time;
- source/provenance and derivation method/version;
- validation status, Errors, Warnings, uncertainty and coverage;
- required dependency closure and conflicting items;
- unsupported or preview-only operations;
- predicted formal write path and capability status;
- known downstream effects and stale evidence.

The user must be able to inspect the actual geometry/attribute payload and not only an AI-generated summary.

### 5.3 Comparison

Comparison is based on stable identity and exact version references:
- Candidate Version against its pinned formal base;
- competing versions for the same formal Object;
- alternative Candidate Sets;
- selected subset against the full proposed set.

Geometry comparison uses the existing Map Engine scene/query generation and ME-05 spatial services. It does not infer semantic identity, connectivity, containment, access or topology from visual overlap.

A stale base, changed target context or changed source fingerprint must be visibly marked and revalidated before selection finalization.

## 6. Selection Snapshot contract

### 6.1 Finalization is the durable decision point

Draft checkboxes and temporary review choices are local UI state. When the user confirms the selection, the Selection service writes one immutable Selection Snapshot and all Selection Items in one transaction.

The snapshot records the complete decision set considered for that review, not only selected items:
- `selected`: exact version chosen for possible formal application;
- `rejected`: exact version explicitly rejected;
- `not_selected`: exact version considered but not chosen.

This preserves the decision history and avoids inferring prior decisions from mutable `selection_status` convenience fields.

### 6.2 Finalization preconditions

Before writing a Selection Snapshot, the service verifies:
1. all Candidate Versions belong to the Task's delivered Candidate Sets or explicitly permitted later editing work;
2. all selected versions have completed applicable validation reports with exact report-item membership;
3. no selected version has an unresolved Error;
4. warning acknowledgement requirements are satisfied;
5. there is at most one selected version per formal Object ID;
6. every required Candidate dependency is selected or already satisfied by current formal target state;
7. dependency graph is acyclic and all references are same-Task/context compatible;
8. new Object identities have valid permanent Core Domain reservations;
9. the target context and relevant source/base fingerprints remain current;
10. the finalized snapshot contains at least one selected item.

If any precondition fails, no finalized snapshot is written. The review session remains editable and explains the blocker.

### 6.3 Revising a decision

A finalized Selection Snapshot is immutable. If the user changes their mind:
- create a new snapshot with `parent_snapshot_id` pointing to the previous snapshot;
- include the exact versions reconsidered and their new dispositions;
- rerun dependency/validation/freshness checks;
- create a new Change Set from the new snapshot.

Do not update or delete an earlier Selection Snapshot. A selection change does not release or reuse Object ID reservations.

## 7. Selection is not authorization or application

After snapshot finalization:
1. The AI Assistant builds a draft Change Set from the exact selected Candidate Versions.
2. Application Services map each intended operation to an adopted Core Domain descriptor and typed payload.
3. Core Domain preflight resolves the exact target scope, descriptor compatibility, History, reservations, source fingerprints and write path.
4. The Change Set is sealed and hashed.
5. The user reviews the exact Change Set and authorizes that immutable version/hash when policy requires.
6. Submission invokes the owning Core Domain Domain Service through the established atomic commit boundary.
7. The UI reports Applied only after authoritative commit confirmation and read-back reconciliation.

Opening a Candidate, selecting it, marking a Candidate Set accepted, or finalizing a Selection Snapshot is not human authorization to write formal map data.

### 7.1 V1 atomicity: partial acceptance, not partial application

V1 distinguishes:
- **Partially accepted:** the user selected a dependency-closed subset of a Candidate Set. Non-selected items are not part of the Change Set.
- **Applied:** the complete sealed Change Set was committed atomically by Core Domain.
- **Rejected/Failed/Stale/Outcome Unknown/Recovery Required:** the authoritative submission outcome states defined by the existing application contract.

A single Change Set is all-or-none. V1 does not support partially applying some Change Set items while silently failing others. This follows the Core Domain + AI atomic submission transaction in P2-09-13 and the existing Change Set/Submission contracts.

If the user wants only a subset, construct and authorize a Change Set containing that subset before submission. If independent Change Sets are submitted separately, report each result separately; never summarize several independent transactions as one atomic partial success.

The phrase “partially applied” must not be used for a single Change Set in V1.

## 8. Review interaction layout and actions

The shared Candidate Review surface uses three coordinated regions:

1. **Candidate Set / item navigator** — alternatives, Candidate items, selected/rejected/not-selected state, dependency blockers and validation badges.
2. **Canvas / comparison surface** — formal base, Candidate overlay, isolate/side-by-side comparison, changed geometry and spatial evidence.
3. **Inspector / decision panel** — exact version/hash, provenance, validation issues, dependencies, target context, operation mapping and decision rationale.

The layout may collapse to two regions on narrow windows, but the information and authority boundaries remain the same.

### 8.1 Primary actions

| Action | Meaning | Durable effect |
|---|---|---|
| Inspect | Read Candidate payload/evidence | None |
| Compare | Compare exact versions/alternatives | None |
| Select | Mark exact Candidate Version for current draft decision | Local review state only |
| Reject | Mark exact version rejected in current draft decision | Local review state until snapshot finalizes |
| Not selected | Record considered but not chosen | Local review state until snapshot finalizes |
| Edit / Request revision | Create a new Candidate Version or new Task work | New immutable version; old version retained |
| Revalidate | Run validator against exact version | New Validation Report |
| Confirm selection | Finalize immutable Selection Snapshot | AI Assistant evidence only |
| Prepare Change Set | Build formal operation batch from selected versions | New draft Change Set |
| Authorize & submit | Explicit human authorization then formal submission | Authorization evidence and attempted Core Domain application |

The UI must separate “Confirm selection” from “Authorize & submit”. It must not label selection finalization as “Apply to Map”.

### 8.2 Keyboard and accessibility

- Candidate navigation, compare, select/reject and issue inspection must be keyboard accessible.
- Focus order must remain stable when filters, validation panels or Candidate overlays change.
- Status cannot be conveyed by color alone; use text/icon/shape and accessible labels.
- Selection state and formal application state must be separately announced to assistive technology.
- Destructive-looking rejection must not delete Candidate data or release reservations.

## 9. State and invalidation matrix

| Change | Candidate Version | Validation | Selection Snapshot | Authorization |
|---|---|---|---|---|
| Candidate payload edit | New version | Old report not applicable to new version | Old snapshot unchanged; new snapshot required | Old Change Set authorization cannot cover new version |
| Required dependency version changes | Existing dependent version becomes stale/incompatible | Revalidate dependent closure | New snapshot required | New Change Set version and authorization |
| Formal base Object/Branch state changes | Candidate remains immutable | Base validation may become stale | Finalization/preflight blocked until refreshed | Invalid |
| Target Map/Branch/History changes | Candidate remains immutable | Target-dependent evidence stale | New snapshot required | Invalid |
| Validator/descriptor semantics change | Candidate remains immutable | Existing report may be incompatible | Revalidate against supported version | New preflight/authorization required |
| Selection choice changes | Candidate Versions unchanged | Reports remain tied to exact versions | New immutable snapshot | New Change Set version/authorization |
| Preview camera/filter changes | Unchanged | Unchanged | Unchanged | Unchanged |

No old authorization is silently carried to a modified or refreshed proposal.

## 10. Failure and recovery

- Validation failure: retain Candidate and diagnostics; do not finalize the selected item.
- Missing dependency: explain the required Candidate or formal target reference; do not auto-add it without user visibility.
- Dependency cycle: block finalization and request an aggregate-level correction.
- Stale base/target: preserve Candidate evidence, mark Stale and require refresh/revalidation.
- Asset preview failure: show the payload as unavailable; do not treat it as an empty geometry or valid Candidate.
- Selection transaction failure: no finalized Selection Snapshot is visible; keep local draft review state when safe.
- Change Set sealing failure: preserve the Selection Snapshot; retry preparation without changing its decision.
- Authorization failure/revocation: do not submit; require a new explicit authorization if the proposal changes.
- Submission outcome unknown: block duplicate submission and use the established reconciliation path.
- Commit confirmed but scene refresh failed: show PendingReconciliation, not Failed; do not resubmit.
- User cancels review: discard only ephemeral draft choices; finalized snapshots and prior evidence remain.

## 11. Conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| M06E-01 | Open a Candidate Set with two versions of one Object | Both exact versions are inspectable and comparable |
| M06E-02 | Edit a Candidate after validation | New immutable version; old report does not validate it |
| M06E-03 | Select a Candidate with a required unselected Candidate dependency | Finalization blocked with dependency path |
| M06E-04 | Required dependency already exists in current formal target state | Allowed only after Core Domain re-resolution proves it |
| M06E-05 | Dependency points to a superseded or missing Candidate Version | Finalization blocked |
| M06E-06 | Dependency cycle exists | Validation failure; no partial selection |
| M06E-07 | Select two versions of the same formal Object | Rejected by selection validation and database unique index |
| M06E-08 | Select a new Object Candidate without a permanent reservation | Finalization blocked |
| M06E-09 | Candidate has unresolved Error | Selection blocked |
| M06E-10 | Candidate has Warning without required acknowledgement | Selection blocked |
| M06E-11 | Warning acknowledged for exact report/version | Eligible subject to all other gates |
| M06E-12 | Candidate Set is partially accepted | Snapshot contains selected and non-selected/rejected decisions |
| M06E-13 | User changes a finalized selection | New snapshot references parent; prior snapshot unchanged |
| M06E-14 | Candidate preview is hidden or isolated | No formal Map state change |
| M06E-15 | User confirms selection | Selection Snapshot only; no Core Domain mutation |
| M06E-16 | Change Set is sealed after selection | Hash binds exact selected versions, payloads, evidence and target |
| M06E-17 | Candidate content changes after authorization | Old authorization cannot submit changed content |
| M06E-18 | Target context becomes stale | Preflight rejects; no formal mutation |
| M06E-19 | One item in a sealed Change Set fails Core Domain validation | Entire Change Set rejected; no item is partially persisted |
| M06E-20 | Change Set commits but scene refresh fails | PendingReconciliation; duplicate apply prevented |
| M06E-21 | User rejects a Candidate using the review UI | Decision evidence retained; Candidate and reservation are not deleted |
| M06E-22 | Candidate Sets are alternatives | No implicit cross-set merge or shared mutation |
| M06E-23 | Explicit cross-set selection has complete dependency closure | Allowed only after same-Task/context/conflict checks |
| M06E-24 | A user tries to apply a Candidate without a Change Set/authorization where required | Blocked; selection is not authorization |
| M06E-25 | Keyboard-only review | All review/decision actions remain reachable and status is announced |
| M06E-26 | Candidate status color is unavailable | Text/icon status remains understandable |
| M06E-27 | Submission outcome is unknown | No blind retry; reconciliation required |
| M06E-28 | Formal Command commits and AI Task later fails | Formal Command remains; Task failure does not roll it back |

## 12. Non-goals and deferred capabilities

- Arbitrary Candidate plug-ins or user-defined executable dependency expressions.
- Automatic semantic conflict resolution or AI self-approval.
- Candidate-to-Core Object identity creation outside Core Domain reservations.
- Partial application of one Change Set.
- Generic merge of independent Candidate Sets.
- Editing finalized Selection Snapshots or Validation Reports.
- Reusing reservations after rejection/supersession.
- Automatic undo of already committed Core Domain Commands.
- Collaborative multi-user review/locking in V1.
- A separate Candidate dependency database/table.

## 13. Status and next integration gate

ME-06-E establishes the shared review/selection interaction baseline. It is compatible with the current AI Assistant Candidate, Validation, Selection, Change Set and Authorization schemas without adding a V1 table.

Before runtime implementation, reconcile the typed dependency manifest with the actual Candidate payload serializer and validator, then map the 28 conformance scenarios into automated UI/service tests. Core Domain submission remains all-or-none per Change Set. No test is marked passed by this document.
