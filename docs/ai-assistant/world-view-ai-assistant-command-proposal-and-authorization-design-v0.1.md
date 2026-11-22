# World View AI Assistant — Command Proposal & Authorization Design V0.1

- Version: 0.1
- Status: Design working baseline — pending Core Domain contract verification
- Scope: AI-owned Command Proposal, Change Set sealing, human authorization, submission evidence
- Architecture: single-user, local-first desktop application
- Supersedes: no prior document; complements `world-view-ai-assistant-core-domain-integration-contract-v0.2.md`
- Implementation authorization: Not granted

---

## 1. Purpose

This document defines the AI Assistant-side contract for turning validated Candidate results into a human-authorized batch of typed Core Domain Commands.

It deliberately does **not** define Core Domain Command types, payload schemas, History storage, World State reconstruction, or Branch semantics. Those remain owned by World View Core Domain.

The design separates five facts that must never be conflated:

1. A Candidate is a generated result.
2. A Command Proposal is an AI-owned description of a proposed authoring action.
3. A Change Set is a sealed batch of exact proposals prepared for one application decision.
4. Authorization is a human decision bound to the exact sealed content and target context.
5. Submission is a Core Domain operation that may persist Commands; reconstruction/playback is a separate Core Domain outcome.

A Task being Completed means its candidate delivery is complete. It does not imply authorization, formal Command persistence, or successful World State reconstruction.

## 2. Authority boundaries

| Concern | Authority |
|---|---|
| Candidate Set, Candidate, Candidate Version | AI Assistant |
| Validation reports and immutable selection snapshots | AI Assistant |
| Draft Command Proposal and sealed Change Set snapshot | AI Assistant |
| Supported Command types, descriptor and payload schema | Core Domain |
| History target/scope resolution | Core Domain |
| Branch inheritance and effective target context | Core Domain |
| Object identity and permanent reservation | Core Domain |
| Command validation and persistence | Core Domain Domain Services |
| Human authorization to submit AI-prepared Commands | Human user |
| State Change derivation, World State and reconstruction | Core Domain |
| Checkpoint creation, reuse and invalidation | Core Domain |

AI Assistant must not create shadow tables or parallel authorities for Map, Branch, History, Command, Event, State Change, World State, Object identity, or Checkpoint.

## 3. Command Proposal contract

### 3.1 Definition

A Command Proposal is an AI-owned envelope that describes one intended Core Domain authoring action. It is not itself a Core Domain Command and must never be represented as a persisted formal Command.

One proposal may reference zero, one, or multiple Candidate Versions, depending on the Core Domain command descriptor. The relationship must be validated against that descriptor; no universal one-Candidate-to-one-Command assumption is made.

### 3.2 Conceptual fields

The following is a logical contract, not a final SQL schema or API wire format.

| Field | Meaning | Authority / constraint |
|---|---|---|
| `proposal_id` | Stable AI Proposal identity | AI Assistant |
| `task_id` | Source Task | AI Assistant |
| `change_set_id` | Owning Change Set | AI Assistant |
| `source_candidate_refs[]` | Exact Candidate and Candidate Version references | AI Assistant; immutable once sealed |
| `target_map_ref` | Target Map identity | Opaque Core Domain reference |
| `selected_branch_ref` | Selected Branch, if applicable | Opaque Core Domain reference |
| `resolved_history_target_ref` | Core Domain-resolved writable History target | Core Domain-issued opaque reference |
| `target_scope_descriptor` | Human-readable explanation of resolved scope | Derived from Core Domain response; not an independent scope authority |
| `command_type_id` | Supported Core Domain Command type | Core Domain catalog |
| `command_descriptor_version` | Descriptor/schema version used to prepare the payload | Core Domain catalog |
| `command_descriptor_digest` | Optional digest of exact descriptor contract | Core Domain-issued or deterministically derived from its canonical descriptor |
| `payload` | Typed payload conforming to the descriptor | Core Domain-defined schema |
| `historical_time` | Intended world-history effective time | Core Domain canonical representation |
| `duration_fields` | Optional duration/end-time data where required | Descriptor-defined |
| `object_refs[]` | Existing formal Object references affected | Core Domain identity |
| `object_reservation_refs[]` | Permanent reservations for new Object identities | Core Domain-issued |
| `target_context_ref` | Exact target authoring-context precondition | Core Domain-issued opaque token |
| `ordering_intent` | Proposed ordering relations and their provenance | Must preserve Core Domain and user-authored constraints |
| `preflight_result_ref` | Exact Core Domain preflight result/token | Core Domain-issued |
| `explanation` | User-readable statement of intended historical effect | AI-authored; shown before authorization |
| `created_at` | Transaction time of proposal creation | AI Assistant clock; never substitutes for Historical Time |

Fields such as `target_context_ref`, `command_type_id`, and `historical_time` must be populated from Core Domain contracts. AI Assistant must not synthesize their authoritative values.

### 3.3 No generic CRUD command model

The old V1.14 `Create / Update / Delete / Replace Geometry` vocabulary may remain as Candidate editing or UI intent labels only.

Before authorization, every proposal must resolve to a concrete Core Domain-supported Command type and descriptor. In particular:

- Geometry replacement does not replace Object identity.
- Geometry split/merge does not implicitly create/delete semantic Objects.
- Formal deletion is a historical Command, not deletion of an AI-owned record.
- Attribute and geometry changes are not direct writes to current World State.
- New Object identity is allocated/reserved only through Core Domain.
- A command type not exposed by Core Domain cannot be guessed or submitted.

## 4. Proposal lifecycle

### 4.1 States

Recommended AI-side lifecycle:

| State | Meaning |
|---|---|
| `draft` | Proposal is being prepared and may change |
| `preflighted` | Core Domain has validated descriptor shape and current target compatibility |
| `sealed` | Proposal content is included in an immutable Change Set version |
| `authorized` | Human authorized the exact sealed version |
| `submitted` | Submission attempt was accepted as committed by Core Domain |
| `rejected` | Proposal was rejected before authorization or by explicit human decision |
| `superseded` | A newer proposal version replaces this proposal content |
| `stale` | Target context or descriptor precondition no longer matches |
| `recovery_required` | Submission outcome cannot yet be established safely |

State transitions are enforced by application services. SQL CHECK constraints alone are not the lifecycle implementation.

### 4.2 Editing rules

- A `draft` proposal may be edited.
- Any content change after preflight invalidates the prior preflight result.
- Any content change after sealing creates a new immutable Change Set version; it does not mutate the sealed version.
- Any content change after authorization invalidates that authorization for the changed content.
- A stale proposal must be refreshed, re-preflighted, resealed, and re-authorized.
- No authorization is carried forward to a modified or refreshed proposal.

## 5. Change Set and immutable proposal manifest

### 5.1 Change Set meaning

A Change Set is the user-facing unit of one formal application decision. It groups one or more typed Command Proposals intended to be submitted atomically through Core Domain.

It is not a Core Domain History, a transaction log, or a generic workflow.

### 5.2 Change Set version

Each seal operation creates an immutable `change_set_version` containing:

- Change Set identity and monotonically increasing AI-side version number;
- ordered list of proposal identities and exact proposal content;
- exact Candidate Version references and Validation Report references used as evidence;
- exact Selection Snapshot reference, if selection produced the batch;
- target Map, selected Branch and Core Domain-resolved History target references;
- target-context precondition token(s);
- Command type IDs and descriptor versions/digests;
- canonical Command payloads and Historical Time values;
- accepted ordering constraints and their provenance;
- Object identity and reservation references;
- durable asset references and content hashes required by the proposals;
- user-visible explanations shown during authorization;
- canonicalization algorithm identifier and manifest content hash;
- creation timestamp (transaction time).

The sealed manifest is immutable. A changed proposal requires a new Change Set version.

### 5.3 Content hash

The manifest hash must cover every value that can change the meaning or authorization boundary of the submission, including:

1. target Map/Branch/History context;
2. target-context precondition references;
3. exact proposal order within the batch;
4. Command type identifiers and descriptor versions/digests;
5. canonical payload bytes;
6. Historical Time and duration fields;
7. Object identity and reservation references;
8. accepted ordering constraints;
9. source Candidate Version and relevant validation evidence references;
10. durable asset content hashes;
11. the human-readable proposal explanations shown to the user.

The hash must use a documented deterministic canonicalization format. JSON text serialization without a canonicalization rule is insufficient.

Volatile display-only data (for example, progress text, UI expansion state, current selection highlight, or a regenerated preview thumbnail) must not alter the manifest unless it changes the actual submitted payload or the evidence presented for authorization.

The hash is an integrity binding for AI-owned proposal evidence. It is not a Core Domain revision, Branch revision, Object version, or substitute for target-context validation.

## 6. Human authorization

### 6.1 Authorization binding

A human authorization record must bind to exactly:

- Change Set ID;
- immutable Change Set version;
- manifest content hash;
- target Map/Branch/History context references;
- target-context precondition reference(s);
- exact Command descriptor versions/digests;
- exact accepted ordering constraints;
- authorization decision;
- authorization transaction timestamp.

The authorization must not mean “approve this Candidate Set generally” or “allow AI to make similar changes later.” It authorizes only the exact manifest.

### 6.2 User-facing confirmation content

Before authorizing, the UI must show:

- target Map and selected Branch;
- the History target/scope resolved by Core Domain, using a clear human-readable explanation;
- the count and summary of proposed Commands;
- the intended Historical Time for each proposal or grouped time range;
- affected Object identities and any newly reserved identities;
- the accepted execution ordering where it is semantically relevant;
- validation errors/warnings and preflight diagnostics;
- known limitations of preview or reconstruction;
- the exact action that will occur: submit/persist Commands, not “rebuild history” unless that is a separate explicitly requested action.

The user must be able to inspect individual proposals and reject or revise the batch before authorization.

### 6.3 No AI self-approval

AI Assistant may recommend that a batch is ready. It cannot create the human authorization record on behalf of the user, auto-approve a semantic scope ambiguity, or expand an existing authorization.

## 7. Ordering contract

Ordering must follow ADR-003 and the applicable D01 decisions. AI Assistant stores proposal intent and accepted user decisions; it does not become a second ordering engine.

Three categories must remain distinct:

1. **Existing authoritative constraints** — already persisted by Core Domain or inherited under its rules. Read-only to AI Assistant; never silently rewritten.
2. **Deterministic default order** — a Core Domain/App-derived order where the result is semantically invariant and no user decision is required.
3. **New explicit user ordering** — a user decision that affects semantic outcome. It must be shown clearly, explicitly accepted, and included in the sealed manifest hash.

Additional rules:

- AI may suggest an order but must label it as a suggestion.
- If order changes the semantic result and user intent is unclear, pause for user decision.
- Do not add explicit ordering constraints merely to freeze a harmless deterministic default.
- Do not overwrite existing user-specified ordering constraints when new proposals are inserted.
- Inherited ordering constraints must be obtained from Core Domain; AI Assistant must not reconstruct them from local copies.
- Cyclic or contradictory ordering constraints are rejected by Core Domain validation; AI Assistant may explain and request correction but cannot silently rewrite the user's constraints.
- A proposal at or before a Branch `parent_at` boundary must not be silently shifted. Core Domain validates `effective_time > parent_at`.

## 8. Shared Object and History scope gate

The AI Assistant must not decide whether an operation belongs to a globally shared history or a Map/Branch-local history.

The source materials contain a semantic boundary that requires formal Core Domain reconciliation:

- Frozen ADR-003 A10-02 describes Shared POI modifications as shared-history actions affecting maps that reference the POI, while Map-local modifications affect only the current map.
- User-confirmed D01-70 states that Shared Object identity does not mean all Branches share one mutable runtime state; local modifications are Commands in the current Branch.
- User-confirmed D01-76 clarifies that reconstruction follows the selected Branch inheritance path continuously and rejects interpreting Shared Object as an independent third authoritative history; the same Shared Object may have different runtime states in different Branches.

These statements must be reconciled by the Core Domain owner without the AI Assistant inventing a dual-history overlay or overriding the frozen ADR. Until that formalization is complete:

1. AI Assistant requests Core Domain to resolve the writable History target and scope for each operation.
2. The returned target/scope is stored as an opaque reference and explained to the user.
3. If Core Domain cannot resolve the scope deterministically, the proposal is blocked.
4. If choosing the scope would change effects in other Maps/Branches, ask the user to clarify intent before authorization.
5. AI Assistant must not create `SharedObjectHistory`, shared-state overlay, or independent merge/replay logic.

This is a Core Domain semantic integration gate, not a reason to invent a new AI-side scope model.

## 9. Preflight, sealing and submission protocol

Recommended sequence:

1. Resolve the user's intent and identify any semantic ambiguity.
2. Ask Core Domain to resolve the target Map/Branch/History and return the exact target-context reference.
3. Obtain the supported Command descriptor catalog for that target.
4. Generate or select Candidate Versions.
5. Validate Candidates against exact versions.
6. Build typed Command Proposals using only Core Domain descriptors and payload contracts.
7. Ask Core Domain to preflight each proposal and the batch. Preflight must not persist formal Commands.
8. Resolve ordering decisions and any required human clarifications.
9. Stage and hash-check all referenced assets.
10. Seal the immutable Change Set version and calculate its manifest hash.
11. Present the exact manifest and impact explanation to the user.
12. Capture explicit human authorization bound to that version and hash.
13. Immediately before submission, Core Domain re-resolves target context, descriptor compatibility, reservations and authorization preconditions.
14. Core Domain atomically persists all Commands and accepted ordering data, or persists none.
15. Record the exact submission outcome and returned formal Command references.
16. Report reconstruction/playback status separately. Do not imply a successful rebuild merely because persistence succeeded.

A preflight result is not a guarantee that a future full-history reconstruction will succeed.

## 10. Stale context and authorization invalidation

`target_context_ref` is an opaque Core Domain-issued precondition. It must become invalid whenever a relevant change alters the effective authoring context for the selected target, including applicable inherited-history or lineage changes.

A per-Object context reference may improve diagnostics but cannot replace the complete target History context.

The following are not acceptable substitutes:

- AI-side revision counters;
- checkpoint IDs or hashes;
- UI cache keys;
- globally latest Object state;
- timestamps alone;
- locally computed Branch fingerprints.

If the target context is stale:

1. Core Domain rejects the entire batch before persisting any Command.
2. AI Assistant refreshes target context and re-runs preflight.
3. A new immutable Change Set version is sealed.
4. The user must authorize the new version again.
5. The old authorization remains historical evidence but cannot authorize the new content.

No automatic rebase or authorization carry-forward is allowed.

## 11. Submission result and Application Report

Application Report records submission attempts and outcomes. It must distinguish at least these dimensions:

| Dimension | Example values | Meaning |
|---|---|---|
| Submission attempt | `started`, `committed`, `rejected`, `outcome_unknown`, `recovery_required` | Whether the Core Domain batch write occurred |
| Command persistence | `none`, `all_committed` | Atomic batch result; partial success is forbidden |
| Rebuild | `not_requested`, `pending`, `succeeded`, `failed` | Separate reconstruction outcome |
| Playback | `available`, `limited`, `unavailable`, `unknown` | Core Domain-reported playback availability |
| Diagnostics | structured Core Domain references/details | Exact failure or warning evidence |

Rules:

- `committed` means the authorized Command batch is durably persisted. It does not mean reconstruction succeeded.
- `rebuild_failed` does not delete or rewrite persisted Commands.
- Under Fail-Fast reconstruction, Core Domain reports the first unexecutable Command and the valid playback boundary.
- The AI Assistant must not auto-repair, skip, delete or rewrite formal Commands.
- A later Task failure does not undo a committed Core Domain batch.
- Task Undo applies only to AI-owned reversible staging work.

### 11.1 Uncertain commit outcome

If the process stops or the response is lost during submission:

- do not blindly replay the batch;
- reconcile using the stable submission attempt identity and authoritative Core Domain History;
- if committed, record the exact returned Command references;
- if definitely not committed, a retry may be considered under the same idempotency contract;
- if the outcome cannot be established, mark `recovery_required` and block duplicate submission for the affected target until reconciled.

## 12. Atomicity and transaction ownership

The authorized batch must be all-or-nothing: all Commands and accepted ordering data persist, or none do.

The preferred V1 arrangement is one local transactional resource and one transaction-aware Domain Services boundary. AI Assistant must not claim atomicity until the actual Core Domain persistence implementation proves it.

If Application Report metadata cannot be committed in the same transaction, it must be designed as recoverable evidence linked to the stable submission attempt, without pretending that two independent commits are atomic.

Do not introduce distributed transactions, a message broker, outbox infrastructure, or compensating Command workflows to bridge a local transaction boundary.

No LLM inference, network request, long-running geometry generation, or asset staging belongs inside the short write transaction.

## 13. Proposed AI-side data model impact

This section is conceptual and does not prescribe final table names or SQL types.

| Existing V1.14 concept | Recommended disposition |
|---|---|
| `selection_snapshots`, `selection_items` | Retain as immutable selection evidence |
| `change_sets` | Retain as AI-owned application decision container |
| `change_set_versions` | Retain as immutable sealed proposal manifest versions |
| `change_items` | Redesign as typed Command Proposal items; remove CRUD authority semantics |
| `change_authorizations` | Retain; bind to exact version, hash, target context and accepted ordering |
| `application_reports` | Redesign as submission-attempt evidence with separate persistence/rebuild/playback dimensions |
| `base_branch_revision_id` | Remove; use only Core Domain-issued opaque target-context references |
| `expected_object_state_version_id` | Remove as a domain version FK; optionally replace with an opaque Core Domain context precondition |
| `target_branch_id` | Retain as an opaque Core Domain Branch reference, pending physical schema mapping |
| Object ID / reservation references | Retain as opaque Core Domain references, pending physical schema mapping |

No AI-side `commands`, `branch_revisions`, `object_versions`, `branch_object_states`, `shared_object_histories`, or `checkpoints` shadow tables are permitted.

## 14. Failure and recovery matrix

| Failure point | Required behavior |
|---|---|
| Candidate validation fails | Do not create a sealable proposal for the failing Candidate Version |
| Command descriptor unavailable | Block proposal creation; do not guess a payload |
| History scope unresolved | Ask for clarification or stop; do not choose a scope silently |
| Preflight rejects payload | Keep diagnostic; edit draft and re-preflight |
| Ordering ambiguity changes outcome | Require explicit user decision |
| Asset hash mismatch | Block sealing/submission; repair staging asset |
| Target context stale before authorization | Refresh, create new Change Set version, authorize again |
| Target context stale at final submit | Reject entire batch; no Commands persisted |
| Authorization/hash mismatch | Reject entire batch |
| Reservation invalid/abandoned | Reject affected proposal/batch; do not allocate replacement identity silently |
| Atomic submission fails | Confirm no Commands persisted; retain report and diagnostic |
| Commit outcome unknown | Reconcile; block blind retry |
| Commit succeeds, rebuild fails | Keep Commands; report reconstruction failure and valid playback boundary |
| Task fails after commit | Task failure does not undo formal Commands |

## 15. Required design verification tests

Before the next schema can be approved, test at least:

1. Proposal references an exact Candidate Version and cannot drift to a later Candidate Version.
2. A Command Proposal cannot use an unsupported or stale descriptor version.
3. Payload validation is performed by Core Domain, not by an AI-owned generic CRUD schema.
4. One proposal can reference multiple Candidate Versions only when its descriptor permits it.
5. Sealed manifest hash changes when any semantic field changes.
6. Display-only UI changes do not alter the manifest hash.
7. Any post-authorization payload, target, time, ordering or asset change invalidates authorization.
8. Existing authoritative ordering constraints are preserved.
9. New user ordering constraints are explicitly accepted and included in the hash.
10. Semantically ambiguous same-time ordering pauses for user decision.
11. Branch `effective_time <= parent_at` is rejected without silent adjustment.
12. Shared/Map-local scope cannot be guessed by AI Assistant.
13. Stale target context rejects the whole batch.
14. Authorization mismatch rejects the whole batch.
15. Batch persistence is all-or-nothing.
16. A lost response after commit is reconciled without duplicate Commands.
17. Rebuild failure does not delete persisted Commands.
18. Task Undo after formal commit does not undo Core Domain Commands.
19. Object reservation remains consumed after Candidate rejection or Task rollback.
20. Application Report distinguishes persistence, rebuild and playback outcomes.

## 16. Open integration gates

This design is sufficiently specific to guide the next Core Domain contract review, but it is not yet a final database implementation baseline.

The following remain mandatory:

- **Gate A — Shared Object semantics:** reconcile ADR-003 A10-02 with D01-70/D01-76 in the formal Core Domain model.
- **Gate B — Command descriptors:** inspect actual Command type catalog, descriptor schema, payloads, validation and persistence contract.
- **Gate C — Physical schema:** inspect actual Core Domain migrations and key mappings.
- **Gate D — Transaction ownership:** prove atomic batch persistence and the placement/recovery of submission evidence.
- **Gate E — Time and ordering:** confirm canonical Historical Time, duration and ordering constraint representation.
- **Gate F — Submission idempotency:** define stable attempt identity and authoritative reconciliation query.

No implementation should begin on the assumption that these gates have already been resolved.

## 17. Current conclusion

The AI Assistant formal-application boundary is now defined as:

**Candidate → typed Command Proposal → Core Domain preflight → immutable Change Set manifest → explicit human authorization → atomic Core Domain submission → separate reconstruction/playback reporting.**

The AI Assistant owns the proposal and authorization evidence. Core Domain owns formal history semantics and all formal writes. Human authorization applies only to the exact sealed manifest.

V1.14 remains a historical SQLite prototype and must not be used unchanged as the implementation baseline. The next step is to reconcile the open Core Domain contracts, then redesign and test the unified schema.