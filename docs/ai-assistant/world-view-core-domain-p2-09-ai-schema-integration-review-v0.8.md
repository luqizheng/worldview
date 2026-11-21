# World View P2-09 — Core Domain / AI Assistant Schema Integration Review V0.8

- Date: 2026-11-21
- Status: Detailed integration and normalization review — recommended mapping; not executable DDL or implementation authorization
- Supersedes: Integration Review V0.7
- Depends on: Core Domain P2-09-11 V0.7; AI Assistant Logical Schema V1.16; P2-09-01/09/09/10

## 1. Executive conclusion

AI Assistant V1.16 has a coherent bounded-context structure and exactly 28 logical domain tables: the V1.15 26-table base plus `authorization_revocations` and `submission_asset_refs`. Its Core Domain references should be classified by lifecycle rather than converted wholesale into restrictive foreign keys. Core Domain entity IDs are proposed as UUID text, but Commands and target Map/Branch/History rows may later be edited, retired or deleted. AI authorization and submission evidence must survive those lifecycle changes.

The recommended integrated model therefore uses:

- Core Domain as the sole authority for formal Map/Object/History/Command/Event/State Change/Reservation/Asset/Checkpoint facts;
- AI Assistant as the sole authority for Task/Candidate/Validation/Selection/Proposal/Authorization/Submission evidence;
- stable UUID text references for Core entities;
- restrictive cross-module FKs only for Core records whose permanent retention is guaranteed and where the FK cannot block valid Core lifecycle operations;
- immutable AI evidence even after the referenced Core entity is changed or deleted.

## 2. Exact AI logical table inventory

The V1.15 base table count is 26, not an estimate:

| Group | Tables | Count |
|---|---|---:|
| Task execution | `tasks`, `task_contract_versions`, `task_plan_versions`, `task_steps`, `tool_calls`, `task_operations`, `task_logs`, `human_intervention_requests`, `task_delivery_records` | 9 |
| Candidate / validation | `candidate_sets`, `object_candidates`, `object_candidate_versions`, `candidate_edit_records`, `validation_reports`, `validation_report_items`, `validation_issues` | 7 |
| Selection | `selection_snapshots`, `selection_items` | 2 |
| Proposal / submission base | `change_sets`, `change_set_versions`, `command_proposal_items`, `proposal_ordering_relations`, `change_authorizations`, `submission_attempts`, `submission_command_refs`, `application_reports` | 8 |
| **V1.15 base total** |  | **26** |

V1.16 adds two justified tables to that base: `authorization_revocations` for immutable human withdrawal of unconsumed permission, and `submission_asset_refs` for normalized references to formal Core Assets returned during submission. The integrated V1.16 total is **28 AI-owned domain tables**.

## 3. Immutable evidence and lifecycle corrections

### 3.1 `change_authorizations` must actually be immutable

V1.15 describes `change_authorizations` as immutable but also places mutable `revoked_at` and `revocation_reason` fields on the same row. Those statements conflict.

Recommended V1 resolution:

- Keep `change_authorizations` as one immutable human decision bound to one sealed `change_set_version_id`, exact content hash and exact target context.
- Remove `revoked_at` and `revocation_reason` from that row.
- Add append-only `authorization_revocations(authorization_revocation_id, authorization_id, revoked_by_actor_id, reason, revoked_at_utc_ms)`, unique on `authorization_id` so one authorization can be revoked once.
- Only a human may create a revocation row. AI/background workers cannot.
- A revoked authorization cannot be reused. Reauthorization requires a new sealed proposal version and a new human decision, even if the semantic content is unchanged.
- Revocation is allowed only before a submission attempt enters `submitting`. If a submission outcome is already `outcome_unknown`, reconcile first; do not claim the authorization can be revoked while a commit may already have succeeded.
- Revocation and submission-start are serialized through the single application write coordinator and checked in one short transaction, so they cannot race past one another.
- A successful submission is not retroactively revocable. The revocation record is immutable evidence of withdrawing permission before the commit boundary.

### 3.2 `submission_attempts` status normalization

Avoid storing the same reconciliation outcome twice in both `status` and `reconciliation_status`.

Recommended status set:
- `prepared`
- `submitting`
- `persisted`
- `rejected`
- `outcome_unknown`
- `reconciled_persisted`
- `reconciled_not_persisted`

The last two statuses make the result of an uncertain commit explicit. Retain `reconciled_at_utc_ms` and immutable diagnostic evidence as needed; remove a second status field that duplicates the same fact. Status transitions are controlled by the submission coordinator and must not permit blind retry while the outcome is unknown.

### 3.3 Version foreign keys within AI ownership

Normalize proposal children against `change_set_version_id`, not only `(change_set_id, version_number)` copied into every row:

- `change_set_versions` has PK `change_set_version_id` and unique `(change_set_id, version_number)`.
- `command_proposal_items` and `proposal_ordering_relations` reference the exact `change_set_version_id`.
- `change_authorizations` references the exact sealed `change_set_version_id`.
- `submission_attempts` references the exact `change_set_version_id` and `authorization_id`.
- `submission_command_refs` references the exact `submission_attempt_id` and proposal item.

These are AI-owned relationships and should use ordinary enforced FKs. Sealed proposal rows and authorization evidence remain immutable.

## 4. Core Domain reference classification

| AI logical reference | Integrated representation | FK policy |
|---|---|---|
| Target Map | `target_map_id TEXT` | Opaque UUID evidence; Core Domain validates existence/lifecycle at preflight. No restrictive FK. |
| Target Branch | `target_branch_id TEXT` | Opaque UUID evidence; no FK that blocks Branch lifecycle. |
| Target History | `target_history_id TEXT` | Opaque UUID evidence resolved by Core Domain; no FK that blocks History lifecycle. |
| Target History context | opaque context token/hash | Never an entity FK or History revision. |
| Object identity | `object_identity_id TEXT` | FK to `core_object`; P2-09-11 V0.7 retains draft/active/abandoned/retired identity rows as tombstones and does not physically delete them. |
| Object reservation | `reservation_id TEXT` | FK to `core_object_id_reservation` recommended; reservations are permanent and never recycled. |
| Candidate staging asset | AI-owned content hash + relative staging locator/manifest | Not a Core Asset FK. It remains AI staging evidence until publication. |
| Published Core Asset | `core_asset_id TEXT` in `submission_asset_refs` | FK to `core_asset`; metadata and bytes cannot be garbage-collected while any AI/Core reference remains. |
| Submitted Core Command | `core_command_id TEXT` | Opaque UUID evidence, not an FK. Commands may later be edited/deleted; AI submission evidence must remain. |
| Effective Object context | opaque context token/hash | Never an Object Version FK. |
| Lineage fingerprint | opaque hash/token | Never a Branch Revision FK. |

Never cascade-delete AI proposal, authorization, submission or report evidence when a Core row is removed. Use restrictive FKs only where the Core retention contract justifies them. Core Domain lifecycle must not be blocked by an old AI reference to a deletable target.

## 5. Asset ownership and publication

Keep candidate-generation assets distinct from formal Core Assets:

1. During Task/Candidate generation, AI Assistant owns staging asset bytes, hashes and their manifest references. They are not yet formal Core Domain assets.
2. Sealed proposal content binds to the exact asset hashes and manifest schema version, not to a mutable staging path.
3. Before final submission, validate and hash-check each required asset, then publish it immutably into the Core Asset Store.
4. Core Domain creates the formal `core_asset` metadata/reference through its service boundary. AI does not insert Core Asset rows directly.
5. If the final SQLite transaction fails after file publication, the immutable file may remain orphaned; cleanup occurs only after proving no committed Core reference exists.
6. The sealed proposal retains exact asset hashes; `submission_asset_refs` retains the exact returned Core Asset IDs and roles after publication. Candidate staging assets can be cleaned under AI retention policy only after no candidate/proposal evidence requires them.

This keeps one metadata authority per asset lifecycle and avoids AI shadow rows for formal Core Assets.

## 6. AI table lifecycle and retention review

| Table group | Mutability / retention rule |
|---|---|
| `tasks` | Mutable lifecycle/current pointers; retain completed/failed task identity and terminal outcome. |
| `task_contract_versions` | Immutable snapshots; new semantic contract creates a new version. |
| `task_plan_versions` | Immutable once activated; revised plan creates a new version. |
| `task_steps` | Belong to one plan version; immutable once that plan version is activated. |
| `tool_calls` | Operational lifecycle may progress; retain invocation and result evidence. |
| `task_operations` | Operational lifecycle may progress for recovery/undo; stable operation identity and recovery data retained. |
| `task_logs` | Append-only. |
| `human_intervention_requests` | Request/response lifecycle; retain user decision evidence. |
| `task_delivery_records` | Immutable delivery manifest. |
| Candidate/validation tables | Candidate versions, edit records, finalized reports/items/issues are immutable evidence; candidate-set lifecycle may change. |
| Selection tables | Immutable snapshots and exact selected versions. |
| `change_sets` | Mutable current lifecycle/pointer only. |
| `change_set_versions` | Draft may be edited; sealed version immutable. Semantic edits after sealing create a new version. |
| `command_proposal_items` / `proposal_ordering_relations` | Mutable only while draft; immutable once their version is sealed. |
| `change_authorizations` | Immutable human decision. |
| `authorization_revocations` | Append-only, human-originated, as immutable human revocation evidence. |
| `submission_attempts` | Controlled operational status transitions; stable idempotency key; uncertain outcomes reconciled before retry. |
| `submission_command_refs` | Immutable returned Core Command IDs; retain even if Core Commands later change or disappear. |
| `submission_asset_refs` | Immutable returned Core Asset IDs with enforced Core Asset FK; prevent garbage collection while referenced. |
| `application_reports` | Immutable final report per attempt; describes persistence, not reconstruction/playback success. |

## 7. Migration and transaction boundaries

- Core Domain migrations create Core tables and stable identity keys first.
- AI base migrations create independent Task/Candidate/Validation/Selection tables.
- AI integration migrations add only justified permanent Core FKs (notably Object Reservation); deletable target/Command references remain opaque UUID evidence.
- Both modules retain ownership of their migration SQL. Application Bootstrap owns the shared ledger and deterministic orchestration only.
- The final authorized submission rechecks authorization, proposal hash, target context, reservations and asset integrity; calls Core Domain services; writes minimum AI submission evidence; and commits through the same verified connection/transaction.
- AI repositories never write Core Domain tables. Core Domain services never write AI tables. The coordinator passes the same transaction context to both owners.
- No LLM call, long validation, geometry generation, filesystem asset staging or user interaction occurs inside the final write transaction.

## 8. Remaining integration gates

1. Apply the `change_set_version_id` FK normalization and verify exact AI table/column count (28 including `authorization_revocations` and `submission_asset_refs`).
2. Add Object identity and permanent Reservation FKs; add Published Core Asset FK through `submission_asset_refs` with no-delete-while-referenced policy.
3. Keep target Map/Branch/History and submitted Command references opaque so immutable AI evidence cannot block Core lifecycle.
4. Validate AI staging-asset retention and Core Asset publication/orphan cleanup against P2-09-06/10.
5. Ensure `submission_asset_refs` is written in the same final transaction as Core Asset metadata and the AI submission outcome.
6. Prove the same Microsoft.Data.Sqlite connection/transaction can be passed through actual Core Domain Domain Services and AI repositories.

## 9. Status

This V0.8 review closes the logical AI/Core reference policy, adopts append-only human authorization revocation evidence, and adds normalized returned Core Asset references. It does not rewrite V1.15 in place, provide executable DDL, or authorize implementation. A versioned AI logical schema update should follow only after the remaining reference-retention rules are settled in the design baseline.