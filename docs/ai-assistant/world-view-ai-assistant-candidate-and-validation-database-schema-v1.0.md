# World View AI Assistant — Candidate Management & Result Validation Database Schema V1.0

- Document: Candidate Management and Result Validation database schema
- Version: 1.0
- Status: Proposed implementation baseline for the scope covered here
- Storage: SQLite, single-user, local-first desktop application
- Scope: Candidate Sets, Object Candidate identity/versioning, candidate edits, Validation Reports and immutable validation scope snapshots
- Depends on: `world-view-ai-assistant-task-execution-database-schema-v1.0.md`

## 1. Purpose and boundaries

This document defines the second database group for the World View AI Assistant Task Planning & Execution Engine. It covers the candidate-result lifecycle and validation evidence. It does not define formal map objects, Branches, Branch Object States, geometry authority, or formal map history.

The AI Assistant database and World View Core Domain may share one SQLite database, but they have distinct ownership boundaries. Candidate payloads are not formal map data. Formal map changes remain exclusively controlled by Change Set + World View Domain Services. This document deliberately does not invent Core Domain table names or foreign-key targets: the actual Core Domain schema was not available for inspection during this design pass. `object_id` and future Branch references therefore remain externally mapped identifiers until the real schema is reviewed.

Candidate Management tables depend on the Task Execution tables, especially `tasks` and `task_delivery_records`, defined in the preceding schema document.

## 2. Global conventions

| Concern | Convention |
|---|---|
| IDs | UUID stored as canonical lowercase TEXT with hyphens; generate with a maintained UUID library (UUID v7 preferred when consistently supported) |
| Time | UTC Unix epoch milliseconds stored as INTEGER |
| Boolean | INTEGER constrained to 0 or 1 |
| Status | TEXT with CHECK constraints; legal transitions enforced by application state machines |
| JSON | TEXT with `json_valid()` checks; JSON is for variable structured data, not relational links |
| Large candidate payloads | Local Asset Store; database stores stable `payload_ref` / `geometry_ref` references |
| Foreign keys | `PRAGMA foreign_keys = ON` on every SQLite connection |
| FK deletion | `ON DELETE RESTRICT`; important history is not physically deleted |
| Transactions | Short SQLite transactions for atomic state changes; never one huge transaction for a long-running Task |

The deployed SQLite build must support JSON functions and deferred foreign keys. `STRICT` tables are optional pending provider/ORM compatibility testing. The DDL below uses ordinary tables and explicit constraints.

## 3. Entity overview

1. `candidate_sets` — independent alternative result sets produced or assembled by a Task.
2. `object_candidates` — stable candidate identity within one Candidate Set, linked to a formal Object Identity when applicable.
3. `object_candidate_versions` — immutable, versioned candidate data. Each edit creates a new row.
4. `candidate_edit_records` — audit link between source and result versions for a user/AI/system edit.
5. `validation_reports` — one validation run over one candidate version or a Candidate Set snapshot.
6. `validation_report_items` — immutable list of exact candidate versions included in a validation run.
7. `validation_issues` — individual Error/Warning/Info findings produced by a report.

A Task can produce multiple Candidate Sets. Candidate Sets are independent: mutable candidate data is not shared between alternatives. Different candidate versions of the same formal Object reuse the same `object_id`; distinct formal Objects must have distinct Object IDs. A newly reserved Object ID remains consumed even if the candidate is later rejected or abandoned, consistent with the Object ID Reservation policy.

## 4. Schema

### 4.1 `candidate_sets`

Purpose: represent one coherent alternative result set. `accepted` means the user accepted the candidate selection for this set; it does not mean the formal map was changed.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `candidate_set_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `name` | TEXT | No | Non-empty display name |
| `description` | TEXT | Yes | Explanation of the alternative |
| `generation_method` | TEXT | No | `ai_generated`, `user_created`, `mixed` |
| `status` | TEXT | No | `generating`, `validating`, `ready`, `under_review`, `accepted`, `partially_accepted`, `rejected`, `superseded` |
| `created_at` | INTEGER | No | Creation time |
| `updated_at` | INTEGER | No | Last state/update time |

```sql
CREATE TABLE candidate_sets (
    candidate_set_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    name TEXT NOT NULL CHECK (length(trim(name)) > 0),
    description TEXT,
    generation_method TEXT NOT NULL CHECK (generation_method IN (
        'ai_generated', 'user_created', 'mixed'
    )),
    status TEXT NOT NULL CHECK (status IN (
        'generating', 'validating', 'ready', 'under_review', 'accepted',
        'partially_accepted', 'rejected', 'superseded'
    )),
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) ON DELETE RESTRICT,
    UNIQUE (task_id, candidate_set_id),
    CHECK (updated_at >= created_at)
);
CREATE INDEX idx_candidate_sets_task_created
    ON candidate_sets(task_id, created_at DESC);
CREATE INDEX idx_candidate_sets_status_updated
    ON candidate_sets(status, updated_at DESC);
```

### 4.2 `object_candidates`

Purpose: stable identity for a candidate object inside one Candidate Set. This is not a formal map Object Identity. `object_id` is a reference to the existing Core Domain identity when one exists, or to a previously reserved identity for a new object. No FK is declared until the actual Core Domain schema is mapped.

The owning `task_id` is deliberately repeated so composite foreign keys can prevent cross-Task candidate-set attachment. `current_version_number` points to the current version number for convenient reads. Its composite FK is deferred because candidate identity and its first version are created in the same transaction.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `candidate_id` | TEXT | No | Primary key; candidate identity |
| `task_id` | TEXT | No | Owning Task |
| `candidate_set_id` | TEXT | No | Owning Candidate Set |
| `object_id` | TEXT | Yes | Existing/reserved formal Object ID; NULL only for candidates not yet bound to an identity |
| `current_version_number` | INTEGER | Yes | Current version number; NULL only during initial construction |
| `created_at` | INTEGER | No | Creation time |
| `updated_at` | INTEGER | No | Last update time |

```sql
CREATE TABLE object_candidates (
    candidate_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    candidate_set_id TEXT NOT NULL,
    object_id TEXT,
    current_version_number INTEGER,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, candidate_set_id)
        REFERENCES candidate_sets(task_id, candidate_set_id) ON DELETE RESTRICT,
    UNIQUE (task_id, candidate_id),
    UNIQUE (candidate_id, current_version_number),
    CHECK (current_version_number IS NULL OR current_version_number >= 1),
    CHECK (updated_at >= created_at),
    FOREIGN KEY (candidate_id, current_version_number)
        REFERENCES object_candidate_versions(candidate_id, version_number)
        DEFERRABLE INITIALLY DEFERRED
);
CREATE INDEX idx_object_candidates_set
    ON object_candidates(candidate_set_id, created_at);
CREATE INDEX idx_object_candidates_object
    ON object_candidates(object_id) WHERE object_id IS NOT NULL;
```

Implementation note: a formal Object ID is never generated by inserting arbitrary text here. It must originate from the Core Domain's Object ID Reservation mechanism. Rejected candidates do not release the reservation. Candidate identity and formal Object identity remain distinct concepts.

### 4.3 `object_candidate_versions`

Purpose: immutable data versions for a candidate identity. A version is never overwritten. A geometry or attribute edit creates a new version and updates the parent candidate's current-version pointer in the same transaction.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `candidate_version_id` | TEXT | No | Primary key |
| `candidate_id` | TEXT | No | Candidate identity |
| `version_number` | INTEGER | No | Starts at 1; unique per candidate |
| `payload_ref` | TEXT | No | Reference to complete candidate payload in Asset Store |
| `geometry_ref` | TEXT | Yes | Optional geometry asset reference |
| `attributes` | TEXT | Yes | JSON attributes when appropriate |
| `source_refs` | TEXT | Yes | JSON source/evidence references |
| `base_object_version_ref` | TEXT | Yes | Exact formal Branch/Object state version used as the base; NULL for a new object |
| `selection_status` | TEXT | No | `undecided`, `accepted`, `rejected`, `superseded` |
| `created_at` | INTEGER | No | Version creation time |

```sql
CREATE TABLE object_candidate_versions (
    candidate_version_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    candidate_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    payload_ref TEXT NOT NULL CHECK (length(trim(payload_ref)) > 0),
    geometry_ref TEXT,
    attributes TEXT CHECK (attributes IS NULL OR json_valid(attributes)),
    source_refs TEXT CHECK (source_refs IS NULL OR json_valid(source_refs)),
    base_object_version_ref TEXT,
    selection_status TEXT NOT NULL DEFAULT 'undecided' CHECK (selection_status IN (
        'undecided', 'accepted', 'rejected', 'superseded'
    )),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, candidate_id)
        REFERENCES object_candidates(task_id, candidate_id) ON DELETE RESTRICT,
    UNIQUE (candidate_id, version_number),
    UNIQUE (candidate_id, candidate_version_id),
    UNIQUE (task_id, candidate_version_id)
);
CREATE INDEX idx_candidate_versions_history
    ON object_candidate_versions(candidate_id, version_number DESC);
CREATE INDEX idx_candidate_versions_selection
    ON object_candidate_versions(selection_status, created_at);
```

`selection_status` is a current convenience/UI state, not historical proof of what the user selected at an earlier time. The later immutable Selection Snapshot will be the authoritative record of a particular user decision. Validation state is not stored on this table; it is represented by Validation Reports tied to exact candidate versions.

### 4.4 `candidate_edit_records`

Purpose: record how one immutable candidate version was transformed into another. The source and result versions must belong to the same candidate identity. One result version can have at most one edit record as its creation cause.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `edit_record_id` | TEXT | No | Primary key |
| `candidate_id` | TEXT | No | Candidate identity |
| `source_version_id` | TEXT | No | Version before edit |
| `result_version_id` | TEXT | No | Newly created version |
| `editor_type` | TEXT | No | `user`, `ai_assistant`, `system` |
| `edit_reason` | TEXT | No | Why the edit was made |
| `change_summary` | TEXT | Yes | Human-readable summary |
| `created_at` | INTEGER | No | Edit time |

```sql
CREATE TABLE candidate_edit_records (
    edit_record_id TEXT PRIMARY KEY,
    candidate_id TEXT NOT NULL,
    source_version_id TEXT NOT NULL,
    result_version_id TEXT NOT NULL,
    editor_type TEXT NOT NULL CHECK (editor_type IN ('user', 'ai_assistant', 'system')),
    edit_reason TEXT NOT NULL,
    change_summary TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (candidate_id, source_version_id)
        REFERENCES object_candidate_versions(candidate_id, candidate_version_id) ON DELETE RESTRICT,
    FOREIGN KEY (candidate_id, result_version_id)
        REFERENCES object_candidate_versions(candidate_id, candidate_version_id) ON DELETE RESTRICT,
    UNIQUE (result_version_id),
    CHECK (source_version_id <> result_version_id)
);
CREATE INDEX idx_candidate_edits_history
    ON candidate_edit_records(candidate_id, created_at DESC);
```

Candidate edit transaction:

1. Verify the expected current version still matches `source_version_id`.
2. Write the new asset to a staged location, validate it, and calculate its content hash.
3. Promote the verified asset to the content-addressed Asset Store.
4. Insert a new `object_candidate_versions` row with `selection_status = 'undecided'`.
5. Insert the `candidate_edit_records` row.
6. Update `object_candidates.current_version_number` and `updated_at`.
7. Commit the database transaction.

If the database transaction fails after asset promotion, the unreferenced asset is an orphan candidate for later safe cleanup; do not delete it until confirming that no database reference exists. Every edit invalidates prior validation for the changed version and requires validation and user acceptance to be reconsidered. Prior versions remain unchanged.

### 4.5 `validation_reports`

Purpose: record a validation execution. A report covers either one candidate version or a Candidate Set validation run. The exact versions actually examined are fixed by `validation_report_items`; do not infer them from the Candidate Set's current contents later.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `validation_report_id` | TEXT | No | Primary key |
| `task_id` | TEXT | No | Owning Task |
| `candidate_set_id` | TEXT | Yes | Set scope; required for `candidate_set` scope |
| `candidate_version_id` | TEXT | Yes | Single-version scope; required for `object_candidate` scope |
| `validation_scope` | TEXT | No | `object_candidate`, `candidate_set` |
| `validator_version` | TEXT | No | Exact validator implementation/rules version |
| `status` | TEXT | No | `running`, `passed`, `passed_with_warnings`, `failed`, `cancelled` |
| `error_count` | INTEGER | No | Non-negative |
| `warning_count` | INTEGER | No | Non-negative |
| `info_count` | INTEGER | No | Non-negative |
| `summary` | TEXT | Yes | Human-readable summary |
| `started_at` | INTEGER | No | Start time |
| `finished_at` | INTEGER | Yes | Completion time; NULL only while running |

```sql
CREATE TABLE validation_reports (
    validation_report_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    candidate_set_id TEXT,
    candidate_version_id TEXT,
    validation_scope TEXT NOT NULL CHECK (validation_scope IN ('object_candidate', 'candidate_set')),
    validator_version TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN (
        'running', 'passed', 'passed_with_warnings', 'failed', 'cancelled'
    )),
    error_count INTEGER NOT NULL DEFAULT 0 CHECK (error_count >= 0),
    warning_count INTEGER NOT NULL DEFAULT 0 CHECK (warning_count >= 0),
    info_count INTEGER NOT NULL DEFAULT 0 CHECK (info_count >= 0),
    summary TEXT,
    started_at INTEGER NOT NULL,
    finished_at INTEGER,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) ON DELETE RESTRICT,
    FOREIGN KEY (task_id, candidate_set_id)
        REFERENCES candidate_sets(task_id, candidate_set_id) ON DELETE RESTRICT,
    CHECK (
        (validation_scope = 'object_candidate'
            AND candidate_set_id IS NULL AND candidate_version_id IS NOT NULL)
        OR
        (validation_scope = 'candidate_set'
            AND candidate_set_id IS NOT NULL AND candidate_version_id IS NULL)
    ),
    CHECK (finished_at IS NULL OR finished_at >= started_at),
    CHECK (status = 'running' OR finished_at IS NOT NULL)
);
CREATE INDEX idx_validation_reports_task
    ON validation_reports(task_id, started_at DESC);
CREATE INDEX idx_validation_reports_set
    ON validation_reports(candidate_set_id, started_at DESC)
    WHERE candidate_set_id IS NOT NULL;
CREATE INDEX idx_validation_reports_status
    ON validation_reports(status, started_at DESC);
```

For `object_candidate` scope, the referenced version's Task ownership is enforced by the required `validation_report_items` row and composite FK described below. The top-level `candidate_version_id` is a convenience pointer to the single item and must equal that item's candidate version. For `candidate_set` scope, all tested versions are represented by report items.

### 4.6 `validation_report_items`

Purpose: immutable manifest of exact candidate versions included in a validation run. This is essential for Candidate Set validation: the set can be edited later, but the report must continue to prove exactly which versions were tested.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `report_item_id` | TEXT | No | Primary key |
| `validation_report_id` | TEXT | No | Parent report |
| `task_id` | TEXT | No | Repeated to enforce same-Task ownership |
| `candidate_version_id` | TEXT | No | Exact immutable candidate version examined |
| `created_at` | INTEGER | No | Manifest creation time |

```sql
CREATE TABLE validation_report_items (
    report_item_id TEXT PRIMARY KEY,
    validation_report_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    candidate_version_id TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (validation_report_id)
        REFERENCES validation_reports(validation_report_id) ON DELETE RESTRICT,
    FOREIGN KEY (task_id, candidate_version_id)
        REFERENCES object_candidate_versions(task_id, candidate_version_id) ON DELETE RESTRICT,
    UNIQUE (validation_report_id, candidate_version_id)
);
```

The `task_id` column on `object_candidate_versions` is intentionally denormalized from its parent candidate. It is populated by the repository from the owning candidate and protected by the composite FK `(task_id, candidate_id)`. The unique key `(task_id, candidate_version_id)` allows the report-item FK to prove that a report cannot include a candidate version from another Task. This is the executable production form; do not replace it with an expression index or a subquery-based constraint.

For a single-object report, exactly one report item is required and its `candidate_version_id` must equal `validation_reports.candidate_version_id`. For a Candidate Set report, one or more report items enumerate the versions included. Enforce these scope/cardinality rules in the validation repository transaction and add migration-time triggers if database-level enforcement is required. Once the report is finalized, its item manifest is immutable.

### 4.7 `validation_issues`

Purpose: store the individual findings produced by a Validation Report. Issues are evidence and diagnostics, not repaired candidate data.

| Column | Type | Null | Rule |
|---|---|---:|---|
| `validation_issue_id` | TEXT | No | Primary key |
| `validation_report_id` | TEXT | No | Owning report |
| `issue_code` | TEXT | No | Stable machine-readable issue code |
| `severity` | TEXT | No | `error`, `warning`, `info` |
| `category` | TEXT | No | `structural`, `identity`, `geometry`, `domain`, `semantic` |
| `title` | TEXT | No | Short title |
| `description` | TEXT | No | Explanation |
| `target_ref` | TEXT | Yes | Candidate/object/component reference |
| `evidence` | TEXT | Yes | JSON evidence and provenance |
| `suggested_action` | TEXT | Yes | Optional repair suggestion |
| `created_at` | INTEGER | No | Finding time |

```sql
CREATE TABLE validation_issues (
    validation_issue_id TEXT PRIMARY KEY,
    validation_report_id TEXT NOT NULL,
    issue_code TEXT NOT NULL,
    severity TEXT NOT NULL CHECK (severity IN ('error', 'warning', 'info')),
    category TEXT NOT NULL CHECK (category IN (
        'structural', 'identity', 'geometry', 'domain', 'semantic'
    )),
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    target_ref TEXT,
    evidence TEXT CHECK (evidence IS NULL OR json_valid(evidence)),
    suggested_action TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (validation_report_id)
        REFERENCES validation_reports(validation_report_id) ON DELETE RESTRICT
);
CREATE INDEX idx_validation_issues_report_severity
    ON validation_issues(validation_report_id, severity);
CREATE INDEX idx_validation_issues_code
    ON validation_issues(issue_code);
```

A repair never edits a Validation Issue into a success state. Repair creates a new candidate version, then a new Validation Report. When a report finishes, insert all issues, compute severity counts, and update report status/counts/finished time in one transaction. Error findings block formal application. Warnings may be allowed only under the product's explicit validation/authorization policy; a warning is never silently treated as an error-free pass.

## 5. Validation semantics and authority

The Validator organizes five validation layers:

1. Structural Validation — required fields, schema shape, payload integrity.
2. Identity Validation — Object ID reservations, candidate/object identity consistency, duplicate identity conflicts.
3. Geometry Validation — geometry validity and supported geometry constraints.
4. Domain Validation — authoritative domain rules, delegated to World View Domain Services where applicable.
5. Semantic Validation — AI-assisted interpretation, source consistency, and evidence-based reasoning.

The Result Validator checks and reports; it does not directly mutate candidate or formal map data. Deterministic rules should be checked by code. Domain Services remain the authoritative implementation of formal domain rules; the Validator must not create a second divergent copy of those rules. AI semantic judgments must distinguish source-supported, partially supported, conflicting sources, AI inference, and unknown/unresolved. An AI judgment is not proof of a fact.

A Validation Report is tied to exact candidate version IDs and an exact `validator_version`. A later candidate edit or validator change does not rewrite an earlier report. The UI may show the latest applicable report, but the historical report remains immutable evidence.

## 6. Critical transaction boundaries

### 6.1 Candidate Set and first candidate version creation

Create the Candidate Set, candidate identity, first candidate version, and current-version pointer in a short transaction. The circular current-version FK is deferred so the parent and first version can be inserted atomically. A Candidate Set can be persisted as `generating` while the Task is running.

### 6.2 Candidate version edit

Use the sequence described in section 4.4. The database transaction inserts the new version and edit record and advances the current-version pointer atomically. Asset staging/promotion is coordinated separately; unreferenced promoted assets are cleaned only after a reference check.

### 6.3 Validation start

Insert a `running` Validation Report and its complete `validation_report_items` manifest in one transaction before validation begins. The manifest fixes the exact versions to be tested. Do not derive the set membership again after validation starts.

### 6.4 Validation completion

In one transaction: insert all Validation Issues; calculate Error/Warning/Info counts; update report status and `finished_at`; update Candidate Set status if appropriate. The report and its item manifest become immutable after completion.

### 6.5 Task delivery integration

When Task Completion Commit occurs, the Task Execution transaction must also persist the exact Candidate Set and candidate-version references delivered in `task_delivery_records`. The delivery record is a snapshot, not a live query over current Candidate Set membership. Candidate edits after Task completion do not rewrite the delivery record.

## 7. Immutability and state authority

| Information | Authority |
|---|---|
| Candidate Set lifecycle | `candidate_sets.status` under Candidate Management state machine |
| Candidate identity | `object_candidates.candidate_id` |
| Current candidate version | `object_candidates.current_version_number` plus matching immutable version row |
| Candidate version data | immutable `object_candidate_versions` row and referenced Asset Store payload |
| Current selection convenience state | `object_candidate_versions.selection_status`; not historical authority |
| Edit history | `candidate_edit_records` |
| Validation result | immutable finalized `validation_reports` + exact `validation_report_items` + `validation_issues` |
| Formal Object Identity | World View Core Domain; never redefined by this schema |
| Formal Branch state | World View Core Domain; never redefined by this schema |

Use application repository rules and SQLite triggers to prevent updates/deletes to finalized version, edit, report manifest, issue, and delivery evidence. The only mutable candidate pointer is `object_candidates.current_version_number`; the only mutable current convenience selection state is the version's `selection_status`. These mutable fields do not rewrite history.

## 8. Core Domain mapping requirements (blocking before final implementation)

The actual Core Domain schema was not available in the Library at the time of this design. Before implementation:

1. Inspect the real Object Identity, Object ID Reservation, Branch, Branch Revision, and Branch Object State tables.
2. Map `object_candidates.object_id` to the actual Object Identity primary key and add the real FK if compatible.
3. Map `base_object_version_ref` to the actual Branch-scoped object-state version identifier; do not use a global object version if the domain model is Branch-scoped.
4. Confirm how new Object IDs are reserved and ensure a candidate cannot invent an unreserved ID.
5. Do not create duplicate Object Identity or Branch tables inside the AI Assistant schema.

Until that mapping is complete, these fields are opaque external references validated by the domain/application layer.

## 9. Deferred scope

The following tables are intentionally not included in this document because their detailed design has not yet been completed:

- Candidate Selection: `selection_snapshots`, `selection_items`.
- Formal application handoff: `change_sets`, `change_items`, human `change_authorizations`, `application_reports`.

Their architecture-level rules are already established: Selection Snapshot is immutable and references exact candidate versions; a single Object ID may have at most one selected effective version in a snapshot; Change Set binds an exact Selection Snapshot and target Branch revision; formal application requires explicit human authorization and is atomic through Domain Services. Those rules will be translated into a separate schema after the remaining detailed design and DDL review.

## 10. Implementation review checklist

- [ ] Inspect actual Core Domain schema and map Object Identity / Object ID Reservation / Branch-scoped version references.
- [ ] Verify the `object_candidate_versions.task_id` denormalized column and same-Task composite FK required by `validation_report_items`.
- [ ] Confirm deferred composite FK behavior for candidate identity/current version creation on the selected SQLite provider.
- [ ] Add tests that candidate versions are immutable and each edit creates a new version.
- [ ] Add tests that a rejected candidate does not release its Object ID reservation.
- [ ] Add tests that report item manifests are exact and immutable after report completion.
- [ ] Add tests for cross-Task and cross-Candidate-Set reference rejection.
- [ ] Add tests that Error findings block formal application and repairs require revalidation.
- [ ] Add tests for Asset Store staging, commit failure, orphan detection, and safe cleanup.
- [ ] Confirm Task Delivery Record and Task Completed are committed atomically with exact candidate version references.