# World View AI Assistant — Candidate Selection & Formal Application Database Schema V1.0

- Document: Candidate Selection, Change Set, Authorization and Application database schema
- Version: 1.0
- Status: Proposed implementation baseline
- Storage: SQLite, single-user, local-first desktop application
- Depends on: `world-view-ai-assistant-task-execution-database-schema-v1.0.md` and `world-view-ai-assistant-candidate-and-validation-database-schema-v1.0.md`

## 1. Purpose and scope

This document completes the database design for the handoff from AI-generated candidate results to formal map changes. It defines:

1. immutable user selection snapshots;
2. versioned Change Sets and their items;
3. explicit human authorization bound to an exact Change Set version;
4. application attempts and reports.

It does not define or duplicate World View Core Domain tables. Actual Object Identity, Branch, Branch Revision, Branch Object State and geometry/history tables must be mapped from the real Core Domain schema before implementation. Their names and key shapes are deliberately not guessed here.

The authority chain is:

`Candidate Versions` → `Validation Reports` → `Selection Snapshot` → `Change Set Version` → `Human Authorization` → `Domain Services Atomic Apply` → `Application Report`.

An AI Assistant may prepare and validate these records, but cannot create human authorization or bypass Domain Services to write formal map data.

## 2. Global conventions

Use the conventions established by the Task Execution schema:

- IDs are canonical lowercase UUID strings stored as `TEXT`. Generate through a maintained .NET UUID library; UUID v7 is the preferred implementation choice where supported consistently.
- All timestamps are UTC Unix epoch milliseconds stored as `INTEGER`.
- Booleans are `INTEGER` constrained to 0/1.
- JSON is `TEXT` with `json_valid()` checks; relational identity and membership must use columns and foreign keys, not JSON.
- Large geometry and other payloads live in the Local Asset Store; the database stores stable references.
- Enable `PRAGMA foreign_keys = ON` on every connection.
- Use `ON DELETE RESTRICT` (SQLite default when omitted) for durable business records. Do not physically delete selection, authorization, or application history.
- State vocabulary is constrained with `CHECK`; legal state transitions are enforced by application services.
- Core Domain foreign keys remain unresolved until the actual schema is inspected.

## 3. Core decisions

### 3.1 Selection is an immutable snapshot

A user changing their mind creates a new `selection_snapshot`; it never edits an earlier snapshot. The snapshot records the exact candidate versions considered and each version's disposition. The exact candidate version, not merely the long-lived candidate ID, is the evidence of what was selected.

Within one snapshot, at most one candidate version for a given formal `object_id` may have disposition `selected`, even if competing versions came from different Candidate Sets.

### 3.2 Selection is not application

`accepted` means the user selected a candidate version. It does not mean the formal map has changed. Formal changes require a Change Set, a separate explicit human authorization, and successful Domain Services application.

### 3.3 Authorization is version-bound

Authorization binds to one immutable Change Set version, its content hash, target Branch and base Branch revision. Any change to items, target Branch, base revision, or other application-relevant content creates a new Change Set version and makes the previous authorization inapplicable. Never silently carry authorization forward.

### 3.4 Atomic application

All formal map mutations in one Change Set are applied atomically. The formal map mutations, Change Set Applied state, and successful Application Report must commit in the same SQLite transaction. If the transaction fails, none of those changes may remain committed.

### 3.5 Formal reversal is a new Change Set

Once applied, a Change Set is not undone by Task rollback or by deleting its report. A requested reversal is represented by a new reverse Change Set, validated and authorized through the same process.

## 4. Entity overview

- `selection_snapshots`: immutable, Task-scoped record of one complete selection decision.
- `selection_items`: exact candidate versions included in that decision and their selected/rejected/not-selected disposition.
- `change_sets`: stable identity and lifecycle state for a formal application proposal.
- `change_set_versions`: immutable application-relevant header for each revision, including target Branch, base Branch revision and content hash.
- `change_items`: immutable operations belonging to one exact Change Set version.
- `change_authorizations`: human authorization evidence bound to one exact version and hash; it is never created by AI.
- `application_reports`: durable record of each application attempt and its outcome.

## 5. Schema

### 5.1 `selection_snapshots`

One record represents one immutable selection decision for one completed Task's candidate delivery or later candidate-editing work associated with that Task.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `selection_snapshot_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `snapshot_number` | INTEGER | No | Positive, unique within Task |
| `parent_snapshot_id` | TEXT | Yes | Prior snapshot from which the user revised the selection; same Task |
| `status` | TEXT | No | `finalized` only; draft UI state is not persisted as an authoritative snapshot |
| `decision_summary` | TEXT | Yes | Human-readable summary |
| `decision_source` | TEXT | No | `user` or `system_default_confirmed_by_user`; AI cannot be the decision source |
| `created_at` | INTEGER | No | Snapshot creation time |

```sql
CREATE TABLE selection_snapshots (
    selection_snapshot_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    snapshot_number INTEGER NOT NULL CHECK (snapshot_number >= 1),
    parent_snapshot_id TEXT,
    status TEXT NOT NULL CHECK (status = 'finalized'),
    decision_summary TEXT,
    decision_source TEXT NOT NULL CHECK (decision_source IN ('user', 'system_default_confirmed_by_user')),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id),
    FOREIGN KEY (task_id, parent_snapshot_id)
        REFERENCES selection_snapshots(task_id, selection_snapshot_id),
    UNIQUE (task_id, snapshot_number),
    UNIQUE (task_id, selection_snapshot_id),
    CHECK (parent_snapshot_id IS NULL OR parent_snapshot_id != selection_snapshot_id)
);
CREATE INDEX idx_selection_snapshots_task ON selection_snapshots(task_id, snapshot_number DESC);
```

The snapshot header and all items are inserted in one transaction. The snapshot is visible only after the transaction commits. A snapshot with no items is not valid for formal application; enforce this in the selection service before commit.

### 5.2 `selection_items`

One row records the disposition of one exact candidate version in the snapshot. Include the competing versions that the user explicitly rejected or left unselected as part of the decision set; do not infer the historical decision from mutable `selection_status` on candidate versions.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `selection_item_id` | TEXT | No | Primary key |
| `selection_snapshot_id` | TEXT | No | Parent snapshot |
| `task_id` | TEXT | No | Duplicated for same-Task composite integrity |
| `candidate_id` | TEXT | No | Candidate identity |
| `candidate_version_id` | TEXT | No | Exact immutable version |
| `object_id` | TEXT | No | Formal Object Identity reference; external Core Domain mapping pending |
| `disposition` | TEXT | No | `selected`, `rejected`, `not_selected` |
| `decision_note` | TEXT | Yes | Optional reason/evidence summary |
| `created_at` | INTEGER | No | Creation time |

```sql
CREATE TABLE selection_items (
    selection_item_id TEXT PRIMARY KEY,
    selection_snapshot_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    candidate_id TEXT NOT NULL,
    candidate_version_id TEXT NOT NULL,
    object_id TEXT NOT NULL,
    disposition TEXT NOT NULL CHECK (disposition IN ('selected', 'rejected', 'not_selected')),
    decision_note TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, selection_snapshot_id)
        REFERENCES selection_snapshots(task_id, selection_snapshot_id),
    FOREIGN KEY (candidate_id, object_id)
        REFERENCES object_candidates(candidate_id, object_id),
    FOREIGN KEY (candidate_id, candidate_version_id)
        REFERENCES object_candidate_versions(candidate_id, candidate_version_id),
    UNIQUE (selection_snapshot_id, candidate_version_id),
    UNIQUE (selection_snapshot_id, selection_item_id)
);
CREATE UNIQUE INDEX idx_selection_one_selected_version_per_object
    ON selection_items(selection_snapshot_id, object_id)
    WHERE disposition = 'selected';
CREATE INDEX idx_selection_items_snapshot
    ON selection_items(selection_snapshot_id, disposition);
CREATE INDEX idx_selection_items_candidate
    ON selection_items(candidate_id, candidate_version_id);
```

**Required prerequisite adjustment to the Candidate schema:** add `UNIQUE(candidate_id, object_id)` to `object_candidates`. The Candidate schema already uses `candidate_id` as the candidate identity and `object_id` as its formal identity reference; this composite unique key lets the selection item prove that its stored `object_id` matches its candidate. The `object_candidate_versions` table must expose `UNIQUE(candidate_id, candidate_version_id)` (as specified in the preceding design) so the composite foreign key is valid.

The partial unique index is the database-level guard against selecting two versions of the same formal Object across different Candidate Sets. Candidate version `selection_status` may be maintained as a UI convenience, but it is not historical authority; this immutable snapshot is.

Before finalizing a snapshot, the Selection service must verify:

1. all referenced versions belong to the Task's delivered Candidate Sets or explicitly permitted later edits;
2. every selected version has a successful validation report for that exact version;
3. no selected version has an unresolved Error issue;
4. Warning acceptance follows the product's warning policy and any required explicit acknowledgement;
5. at most one selected version exists per `object_id` (also enforced by the partial unique index);
6. newly created Object IDs have valid permanent reservations; identity/Branch references are mapped to actual Core Domain records.

A validation report for a Candidate Set must have a fixed `validation_report_items` membership list. The service must verify that the selected version is included in an applicable completed report; it must not infer report membership from the Candidate Set's current contents.

### 5.3 `change_sets`

Stable identity and current lifecycle state for a formal map application proposal. The detailed content belongs to immutable `change_set_versions` and `change_items`.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `change_set_id` | TEXT | No | Primary key |
| `source_task_id` | TEXT | No | Task that produced the candidate work |
| `selection_snapshot_id` | TEXT | No | Exact immutable selection snapshot |
| `current_version_number` | INTEGER | No | Current immutable Change Set version |
| `status` | TEXT | No | `draft`, `validating`, `ready`, `applying`, `applied`, `failed`, `stale`, `cancelled` |
| `created_at` | INTEGER | No | Creation time |
| `updated_at` | INTEGER | No | Last lifecycle update |
| `applied_at` | INTEGER | Yes | Successful application time |

```sql
CREATE TABLE change_sets (
    change_set_id TEXT PRIMARY KEY,
    source_task_id TEXT NOT NULL,
    selection_snapshot_id TEXT NOT NULL,
    current_version_number INTEGER NOT NULL CHECK (current_version_number >= 1),
    status TEXT NOT NULL CHECK (status IN (
        'draft', 'validating', 'ready', 'applying', 'applied', 'failed', 'stale', 'cancelled'
    )),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    applied_at INTEGER,
    FOREIGN KEY (source_task_id, selection_snapshot_id)
        REFERENCES selection_snapshots(task_id, selection_snapshot_id),
    FOREIGN KEY (change_set_id, current_version_number)
        REFERENCES change_set_versions(change_set_id, version_number)
        DEFERRABLE INITIALLY DEFERRED,
    UNIQUE (source_task_id, change_set_id),
    CHECK (applied_at IS NULL OR applied_at >= created_at),
    CHECK (status != 'applied' OR applied_at IS NOT NULL)
);
CREATE INDEX idx_change_sets_status_updated ON change_sets(status, updated_at DESC);
CREATE INDEX idx_change_sets_task ON change_sets(source_task_id, created_at DESC);
```

The deferred composite FK permits inserting the header and first version in one transaction. A Change Set's source Task and Selection Snapshot are fixed for its lifetime. If the user changes the selection, create a new Selection Snapshot and a new Change Set rather than repointing an existing Change Set.

### 5.4 `change_set_versions`

Every change to application-relevant content creates a new immutable version. The content hash is computed by application code over a canonical serialization of the header and ordered items; do not hash arbitrary JSON serialization whose property order is unstable.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `change_set_version_id` | TEXT | No | Primary key |
| `change_set_id` | TEXT | No | Parent Change Set |
| `version_number` | INTEGER | No | Positive, unique per Change Set |
| `target_branch_id` | TEXT | No | Core Domain Branch reference; actual FK mapping pending |
| `base_branch_revision_id` | TEXT | No | Exact Branch revision used to prepare the Change Set; actual FK mapping pending |
| `content_hash` | TEXT | No | SHA-256 hex digest of canonical version header + ordered items |
| `change_summary` | TEXT | Yes | Human-readable summary |
| `created_at` | INTEGER | No | Version creation time |

```sql
CREATE TABLE change_set_versions (
    change_set_version_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    target_branch_id TEXT NOT NULL,
    base_branch_revision_id TEXT NOT NULL,
    content_hash TEXT NOT NULL CHECK (length(content_hash) = 64),
    change_summary TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (change_set_id) REFERENCES change_sets(change_set_id),
    UNIQUE (change_set_id, version_number),
    UNIQUE (change_set_id, change_set_version_id),
    UNIQUE (change_set_id, version_number, content_hash)
);
CREATE INDEX idx_change_set_versions_branch
    ON change_set_versions(target_branch_id, base_branch_revision_id);
```

`target_branch_id` and `base_branch_revision_id` are intentionally plain references until actual Core Domain identifiers are known. At implementation time, add real foreign keys if the Core Domain schema exposes stable referenced keys.

### 5.5 `change_items`

One row is one formal map operation in one exact Change Set version. Rows are immutable after the version is sealed and hashed.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `change_item_id` | TEXT | No | Primary key |
| `change_set_id` | TEXT | No | Parent Change Set |
| `version_number` | INTEGER | No | Exact Change Set version |
| `item_number` | INTEGER | No | Positive stable order within version |
| `operation_type` | TEXT | No | `create`, `update`, `delete`, `replace_geometry` |
| `object_id` | TEXT | No | Formal Object Identity reference; actual FK mapping pending |
| `candidate_id` | TEXT | Yes | Source candidate identity, when applicable |
| `candidate_version_id` | TEXT | Yes | Exact source candidate version, when applicable |
| `expected_object_state_version_id` | TEXT | Yes | Exact current target Branch object-state version expected for update/delete/replace |
| `payload_ref` | TEXT | Yes | Staged complete payload reference for create/update |
| `geometry_ref` | TEXT | Yes | Geometry payload reference for create/update/replace geometry |
| `attributes` | TEXT | Yes | Optional JSON attribute patch or payload metadata |
| `created_at` | INTEGER | No | Creation time |

```sql
CREATE TABLE change_items (
    change_item_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL,
    item_number INTEGER NOT NULL CHECK (item_number >= 1),
    operation_type TEXT NOT NULL CHECK (operation_type IN (
        'create', 'update', 'delete', 'replace_geometry'
    )),
    object_id TEXT NOT NULL,
    candidate_id TEXT,
    candidate_version_id TEXT,
    expected_object_state_version_id TEXT,
    payload_ref TEXT,
    geometry_ref TEXT,
    attributes TEXT CHECK (attributes IS NULL OR json_valid(attributes)),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (change_set_id, version_number)
        REFERENCES change_set_versions(change_set_id, version_number),
    FOREIGN KEY (candidate_id, candidate_version_id)
        REFERENCES object_candidate_versions(candidate_id, candidate_version_id),
    UNIQUE (change_set_id, version_number, item_number),
    UNIQUE (change_set_id, version_number, change_item_id),
    CHECK (
        (operation_type = 'create'
            AND expected_object_state_version_id IS NULL
            AND candidate_id IS NOT NULL AND candidate_version_id IS NOT NULL
            AND payload_ref IS NOT NULL)
        OR
        (operation_type = 'update'
            AND expected_object_state_version_id IS NOT NULL
            AND candidate_id IS NOT NULL AND candidate_version_id IS NOT NULL
            AND payload_ref IS NOT NULL)
        OR
        (operation_type = 'delete'
            AND expected_object_state_version_id IS NOT NULL
            AND candidate_id IS NULL AND candidate_version_id IS NULL)
        OR
        (operation_type = 'replace_geometry'
            AND expected_object_state_version_id IS NOT NULL
            AND candidate_id IS NOT NULL AND candidate_version_id IS NOT NULL
            AND geometry_ref IS NOT NULL)
    )
);
CREATE INDEX idx_change_items_object
    ON change_items(object_id, change_set_id, version_number);
CREATE INDEX idx_change_items_candidate
    ON change_items(candidate_id, candidate_version_id)
    WHERE candidate_id IS NOT NULL;
```

The operation-specific `CHECK` encodes the V1.0 minimum shape. Domain Services must additionally verify that the candidate version's `object_id` equals the `change_items.object_id`, that a Create uses a valid reserved new identity, and that Update/Delete/Replace Geometry target an object currently effective in the target Branch at the expected state version. These cross-table/domain checks belong in the Change Set validation service and Domain Services, not in duplicated ad hoc SQL logic.

`Replace Geometry` preserves Object Identity and changes only geometry. Object split/merge operations are not represented by these four item types; they belong to the separately governed Object Operations domain.

### 5.6 `change_authorizations`

A row is explicit human authorization for one exact immutable Change Set version. AI recommendations, automated policy checks, candidate acceptance and default UI actions are not authorization.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `authorization_id` | TEXT | No | Primary key |
| `change_set_id` | TEXT | No | Parent Change Set |
| `version_number` | INTEGER | No | Exact authorized version |
| `content_hash` | TEXT | No | Exact hash reviewed by the user |
| `target_branch_id` | TEXT | No | Exact target Branch reviewed |
| `decision` | TEXT | No | `authorized` or `denied` |
| `authorized_by` | TEXT | No | `human_user` in V1 single-user model; never `ai` or `system` |
| `decision_evidence` | TEXT | No | JSON UI confirmation evidence and user-visible summary/version/hash |
| `decided_at` | INTEGER | No | Decision time |
| `revoked_at` | INTEGER | Yes | Optional later user revocation time |
| `revocation_reason` | TEXT | Yes | Optional reason |

```sql
CREATE TABLE change_authorizations (
    authorization_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL,
    content_hash TEXT NOT NULL,
    target_branch_id TEXT NOT NULL,
    decision TEXT NOT NULL CHECK (decision IN ('authorized', 'denied')),
    authorized_by TEXT NOT NULL CHECK (authorized_by = 'human_user'),
    decision_evidence TEXT NOT NULL CHECK (json_valid(decision_evidence)),
    decided_at INTEGER NOT NULL,
    revoked_at INTEGER,
    revocation_reason TEXT,
    FOREIGN KEY (change_set_id, version_number, content_hash)
        REFERENCES change_set_versions(change_set_id, version_number, content_hash),
    UNIQUE (authorization_id, change_set_id, version_number),
    CHECK (revoked_at IS NULL OR revoked_at >= decided_at),
    CHECK (revoked_at IS NULL OR decision = 'authorized')
);
CREATE INDEX idx_change_authorizations_version
    ON change_authorizations(change_set_id, version_number, decided_at DESC);
```

Before application, require a matching authorization row with `decision='authorized'`, no revocation, exact current version number, exact content hash and exact target Branch. The application service must recompute the canonical content hash and compare it to the stored hash; it must not trust a stale in-memory hash. A new Change Set version cannot inherit a previous version's authorization.

The `authorized_by` value is a V1 single-user actor marker, not an account identity or authentication system. If the application later adds multiple local profiles, replace it with the actual local principal reference without changing the human-only rule.

### 5.7 `application_reports`

One row records one application attempt. Failed or interrupted attempts remain as history; a later retry creates a new report. Successful formal mutation and the successful report state must commit atomically.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `application_id` | TEXT | No | Primary key |
| `change_set_id` | TEXT | No | Parent Change Set |
| `version_number` | INTEGER | No | Exact applied version |
| `authorization_id` | TEXT | No | Exact human authorization used |
| `status` | TEXT | No | `prepared`, `applying`, `applied`, `failed`, `interrupted`, `recovery_required` |
| `applied_item_count` | INTEGER | No | Non-negative count |
| `failed_item_count` | INTEGER | No | Non-negative count |
| `started_at` | INTEGER | No | Attempt start |
| `finished_at` | INTEGER | Yes | Attempt finish |
| `error_code` | TEXT | Yes | Stable diagnostic code |
| `error_message` | TEXT | Yes | Diagnostic summary |
| `result_summary` | TEXT | Yes | Human-readable result |

```sql
CREATE TABLE application_reports (
    application_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL,
    authorization_id TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN (
        'prepared', 'applying', 'applied', 'failed', 'interrupted', 'recovery_required'
    )),
    applied_item_count INTEGER NOT NULL DEFAULT 0 CHECK (applied_item_count >= 0),
    failed_item_count INTEGER NOT NULL DEFAULT 0 CHECK (failed_item_count >= 0),
    started_at INTEGER NOT NULL,
    finished_at INTEGER,
    error_code TEXT,
    error_message TEXT,
    result_summary TEXT,
    FOREIGN KEY (change_set_id, version_number)
        REFERENCES change_set_versions(change_set_id, version_number),
    FOREIGN KEY (authorization_id, change_set_id, version_number)
        REFERENCES change_authorizations(authorization_id, change_set_id, version_number),
    CHECK (finished_at IS NULL OR finished_at >= started_at),
    CHECK (status NOT IN ('applied', 'failed', 'interrupted') OR finished_at IS NOT NULL),
    CHECK (status != 'applied' OR (failed_item_count = 0 AND applied_item_count >= 1))
);
CREATE INDEX idx_application_reports_change_set
    ON application_reports(change_set_id, started_at DESC);
CREATE INDEX idx_application_reports_recovery
    ON application_reports(status, started_at)
    WHERE status IN ('prepared', 'applying', 'interrupted', 'recovery_required');
```

`application_reports` is diagnostic/application history, not a copy of formal object state. Formal object data remains owned by Core Domain. The report must identify the exact Change Set version and authorization used.

## 6. Important prerequisite adjustments to the preceding Candidate schema

Before applying all database migrations together, reconcile the Candidate/Validation document with these constraints:

1. Add `UNIQUE(candidate_id, object_id)` to `object_candidates` so a selection item can verify its Object Identity reference.
2. Ensure `object_candidate_versions` has `UNIQUE(candidate_id, candidate_version_id)` as a composite FK target.
3. Finalize the current-version pointer design consistently. Prefer `current_version_number` with a composite FK `(candidate_id, current_version_number)` to `(candidate_id, version_number)` rather than a circular pointer to a version row, provided the final migration ordering and deferred FK test passes.
4. Strengthen validation report membership before the combined migration: add `UNIQUE(task_id, validation_report_id)` to `validation_reports`; in `validation_report_items`, replace the single-column report FK with `FOREIGN KEY (task_id, validation_report_id) REFERENCES validation_reports(task_id, validation_report_id)`, and retain `FOREIGN KEY (task_id, candidate_version_id) REFERENCES object_candidate_versions(task_id, candidate_version_id)`. This prevents a report for Task A from including an item belonging to Task B.
5. A finalized selection snapshot must refer only to candidate versions with completed applicable validation. This cross-table rule is checked by the Selection service in the same transaction; SQL CHECK constraints cannot express it safely.

## 7. Lifecycle and transaction rules

### 7.1 Selection Snapshot creation

1. Load the exact Candidate Versions the user is deciding on.
2. Verify version ownership, Object Identity mapping, completed validation reports and Error/Warning policy.
3. Verify only one version per `object_id` is selected.
4. Insert `selection_snapshots` and all `selection_items` in one transaction.
5. Optionally update mutable Candidate Version selection status as a UI projection in that transaction; the immutable snapshot remains the historical authority.
6. Commit. Never edit this snapshot later.

### 7.2 Change Set preparation

1. Create one Change Set from one exact Selection Snapshot.
2. Resolve each selected candidate into Create, Update or Replace Geometry as appropriate; represent user-requested removal as Delete only after confirming the target Branch state.
3. Capture target Branch and base Branch revision.
4. For Update/Delete/Replace Geometry, capture the exact expected target Branch object-state version.
5. Insert a new immutable Change Set version and its complete item list in one transaction.
6. Canonicalize and hash the version header plus ordered items. Persist the hash before the version can become Ready.

If the user or AI changes any item or application-relevant header field, create a new version, recalculate the hash and require fresh validation and authorization.

### 7.3 Change Set validation

Before setting Ready, verify:

- selection snapshot is finalized and selected versions remain valid;
- every item maps to the correct formal Object Identity;
- Create IDs are permanently reserved and not already bound to another Object;
- Update/Delete/Replace targets exist in the target Branch's effective state;
- expected object-state versions still match;
- target Branch base revision and required inherited-state references remain valid;
- geometry, attributes, relationships and domain constraints pass through Domain Services validation;
- no duplicate or contradictory operations target the same object in the same Change Set unless a deterministic normalization has reduced them to one final operation;
- the version content hash matches its canonical content.

Use Branch-scoped optimistic concurrency. Unrelated changes elsewhere in the database must not make this Change Set stale. Relevant target Branch/object/dependency changes do.

### 7.4 Human authorization

Show the user the target Branch, base revision, exact objects and operations, validation result, warnings, and consequences. The user must explicitly confirm this exact version. In one transaction, persist `change_authorizations` and move the Change Set to the appropriate authorized/ready-to-apply lifecycle state. Do not let an AI call, background process, or default setting create this row.

If authorization is revoked, do not apply. If content changes, create a new version and require new authorization. If the target Branch or relevant expected object state changes, mark the version stale and require revalidation and reauthorization.

### 7.5 Atomic Apply

Application is serialized per target Branch: only one formal write transaction may apply to a given Branch at a time. This is a local application-level coordination rule, not a distributed lock. Different Branches may proceed independently where the SQLite write model and Domain Services permit it; SQLite still serializes physical write transactions as required.

Recommended sequence:

1. Outside the formal write transaction, prepare and verify staged assets; ensure all referenced content is durable and hash-verified.
2. Create/commit an `application_reports` row in `prepared` or `applying` state so a crash is discoverable.
3. Enter the short SQLite write transaction. Recheck authorization, content hash, Change Set version, target Branch revision, expected object-state versions and Domain Services preconditions.
4. Invoke Domain Services using the same database transaction/connection. Apply every Change Item.
5. In the same transaction, update `change_sets.status='applied'`, set `applied_at`, and update the report to `applied` with counts and finish time. Commit once.
6. If any item fails, roll back the entire formal transaction. In a separate short transaction, record the failed attempt and diagnostics; no formal item may remain partially applied.

A database transaction must not remain open while AI inference, network calls, or slow asset generation occurs. Domain Services must expose a transaction-aware application boundary so formal data and the application marker/report share one commit.

### 7.6 Crash recovery

At startup, inspect reports in `prepared`, `applying`, `interrupted` or `recovery_required` states and reconcile them with the actual Change Set and Core Domain data.

- If the atomic formal transaction committed, the Change Set and report must both show Applied.
- If it did not commit, no formal Change Set mutation from that attempt may remain. Mark the attempt interrupted/failed after verification; a retry requires fresh precondition checks.
- If the database or Core Domain state cannot prove either outcome, mark `recovery_required` and block writes to the affected Branch until reconciled.
- Never blindly replay an uncertain application.

The application report row created before the atomic transaction is a recovery breadcrumb only. It does not mean any formal map change has occurred.

### 7.7 Reversal

A successful application is durable history. To reverse it, create a new reverse Change Set against the current Branch state. It must capture current expected object-state versions, pass validation, receive new human authorization, and apply atomically. Do not delete or rewrite the original Application Report.

## 8. State authority

| Concern | Authority |
|---|---|
| Historical user choice | Immutable `selection_snapshots` + `selection_items` |
| Current candidate UI selection indicator | Candidate version `selection_status` only as a derived/convenience projection |
| Change Set content | Immutable `change_set_versions` + `change_items` |
| Current Change Set lifecycle | `change_sets.status` and current version pointer |
| Human permission to apply | Exact matching, non-revoked `change_authorizations` row |
| Formal map state | World View Core Domain only |
| Application attempt history | `application_reports` |
| Whether application truly committed | Atomic agreement between Core Domain changes, Change Set state and Application Report, reconciled on recovery |

## 9. Core Domain mapping — mandatory before implementation

The actual Core Domain schema was not available for inspection during this design. Therefore this document intentionally does not invent foreign-key targets for:

- `object_id` / Object Identity;
- `target_branch_id`;
- `base_branch_revision_id`;
- `expected_object_state_version_id`;
- formal geometry, attributes and relationship records.

Before implementation, inspect the real Core Domain migrations/schema and add foreign keys only to actual stable unique keys. Also confirm whether Branch revision and Branch object-state identifiers are separate entities or one versioned record. The Change Set model must reference the exact effective state in the target Branch, not a global latest object version.

## 10. SQLite and migration review notes

- SQLite allows references to tables created later, but migrations should still create base tables before dependent tables where practical. The two intentional deferred pointer cycles (`change_sets` ↔ `change_set_versions`, and the existing `tasks` ↔ contract/plan version pattern) require explicit migration tests with `DEFERRABLE INITIALLY DEFERRED`.
- The `change_sets` current-version composite FK requires `UNIQUE(change_set_id, version_number)` on `change_set_versions`; that is present above.
- `change_authorizations` references the composite `(change_set_id, version_number, content_hash)` and therefore requires the matching unique key on `change_set_versions`; that is present above.
- `selection_items` requires the composite unique keys called out in Section 6.
- `validation_report_items` must use the two composite Task-scoped foreign keys specified in Section 6; the earlier Candidate/Validation draft currently has only a single-column report FK and must be migrated to this stronger form.
- `change_items` candidate version FK is nullable as a pair; application code must ensure both are either present or absent. The operation-specific CHECK enforces this for the four supported operations.
- Content hash calculation and verification are application responsibilities. SQLite does not need a custom hash extension.
- Use triggers or repository-level guards to reject UPDATE/DELETE on immutable snapshot/version/item/evidence rows after finalization. Keep mutable lifecycle pointers/status on their designated header rows only.
- Verify `json_valid()` availability in the deployed SQLite build.

## 11. Explicitly excluded complexity in V1.0

Do not add a generic workflow engine, DAG dependency model, distributed lock service, message broker, event-sourcing framework, generic approval-chain engine, partial Change Set commit, automatic authorization inheritance, or AI auto-apply mechanism. The single-user local-first application needs immutable records, short SQLite transactions, Domain Services validation, and straightforward recovery—not an enterprise orchestration platform.

## 12. Implementation readiness checklist

- [ ] Reconcile the Candidate/Validation schema prerequisites in Section 6.
- [ ] Inspect and map the actual Core Domain schema and stable identifiers.
- [ ] Test all composite and deferred foreign keys with `PRAGMA foreign_key_check`.
- [ ] Test the partial unique index: two selected candidate versions with the same Object ID in one snapshot must fail.
- [ ] Test cross-Task candidate/version/report references are rejected.
- [ ] Test immutable Selection Snapshot and Change Set Version records cannot be edited after finalization.
- [ ] Test authorization cannot be created by AI/system actors and cannot authorize another version/hash/Branch.
- [ ] Test any Change Set content change requires a new version and fresh authorization.
- [ ] Test Branch-scoped optimistic concurrency and stale detection.
- [ ] Test all-or-nothing formal application, including process termination before commit and after commit.
- [ ] Test Application Report recovery without blind replay.
- [ ] Test reverse Change Set behavior; original applied history remains intact.
- [ ] Verify formal map writes are impossible outside Domain Services.
