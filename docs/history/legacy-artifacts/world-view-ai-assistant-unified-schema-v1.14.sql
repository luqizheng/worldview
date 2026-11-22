-- World View AI Assistant unified schema prototype V1.14
-- SQLite 3.46.1 tested; Core Domain FKs intentionally not mapped.
PRAGMA foreign_keys = ON;
BEGIN IMMEDIATE;

CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY CHECK (version >= 1),
    migration_id TEXT NOT NULL UNIQUE,
    applied_at INTEGER NOT NULL
);

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
    UNIQUE (candidate_id, object_id),
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
    FOREIGN KEY (task_id, candidate_version_id)
        REFERENCES object_candidate_versions(task_id, candidate_version_id) ON DELETE RESTRICT,
    UNIQUE (task_id, validation_report_id),
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

CREATE TABLE validation_report_items (
    report_item_id TEXT PRIMARY KEY,
    validation_report_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    candidate_version_id TEXT NOT NULL,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, validation_report_id)
        REFERENCES validation_reports(task_id, validation_report_id) ON DELETE RESTRICT,
    FOREIGN KEY (task_id, candidate_version_id)
        REFERENCES object_candidate_versions(task_id, candidate_version_id) ON DELETE RESTRICT,
    UNIQUE (validation_report_id, candidate_version_id)
);

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

CREATE TABLE tasks (
    task_id TEXT PRIMARY KEY,
    title TEXT NOT NULL CHECK (length(trim(title)) > 0),
    user_request TEXT NOT NULL CHECK (length(trim(user_request)) > 0),
    intent_type TEXT NOT NULL CHECK (intent_type IN (
        'general_conversation', 'information_request',
        'map_operation', 'complex_map_creation'
    )),
    status TEXT NOT NULL CHECK (status IN (
        'created', 'planning', 'ready', 'running', 'waiting_for_user',
        'paused', 'rolling_back', 'recovery_required',
        'completed', 'failed', 'cancelled'
    )),
    complexity TEXT NOT NULL CHECK (complexity IN ('simple', 'moderate', 'complex')),
    current_contract_version_number INTEGER NOT NULL DEFAULT 1 CHECK (current_contract_version_number >= 1),
    current_plan_version_number INTEGER CHECK (
        current_plan_version_number IS NULL OR current_plan_version_number >= 1
    ),
    created_at INTEGER NOT NULL,
    started_at INTEGER,
    finished_at INTEGER,
    updated_at INTEGER NOT NULL,
    error_summary TEXT,
    FOREIGN KEY (task_id, current_contract_version_number)
        REFERENCES task_contract_versions(task_id, version_number)
        DEFERRABLE INITIALLY DEFERRED,
    FOREIGN KEY (task_id, current_plan_version_number)
        REFERENCES task_plan_versions(task_id, version_number)
        DEFERRABLE INITIALLY DEFERRED,
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR finished_at >= created_at),
    CHECK (status NOT IN ('completed', 'failed', 'cancelled') OR finished_at IS NOT NULL)
);
CREATE INDEX idx_tasks_status_updated ON tasks(status, updated_at DESC);
CREATE INDEX idx_tasks_created_at ON tasks(created_at DESC);

CREATE TABLE task_contract_versions (
    contract_version_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    contract_data TEXT NOT NULL CHECK (json_valid(contract_data)),
    change_reason TEXT,
    approval_evidence TEXT CHECK (
        approval_evidence IS NULL OR json_valid(approval_evidence)
    ),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) DEFERRABLE INITIALLY DEFERRED,
    UNIQUE (task_id, version_number),
    UNIQUE (task_id, contract_version_id),
    CHECK (version_number = 1 OR (
        change_reason IS NOT NULL AND approval_evidence IS NOT NULL
    ))
);

CREATE TABLE task_plan_versions (
    plan_version_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    plan_summary TEXT NOT NULL,
    planning_reason TEXT,
    status TEXT NOT NULL CHECK (status IN ('active', 'superseded')),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id) DEFERRABLE INITIALLY DEFERRED,
    UNIQUE (task_id, version_number),
    UNIQUE (task_id, plan_version_id)
);
CREATE UNIQUE INDEX idx_task_plan_one_active
    ON task_plan_versions(task_id) WHERE status = 'active';
CREATE INDEX idx_task_plan_history
    ON task_plan_versions(task_id, version_number);

CREATE TABLE task_steps (
    step_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    plan_version_id TEXT NOT NULL,
    step_number INTEGER NOT NULL CHECK (step_number >= 1),
    objective TEXT NOT NULL,
    input_data TEXT CHECK (input_data IS NULL OR json_valid(input_data)),
    action_description TEXT NOT NULL,
    acceptance_criteria TEXT NOT NULL CHECK (json_valid(acceptance_criteria)),
    status TEXT NOT NULL CHECK (status IN ('pending', 'running', 'completed', 'failed', 'skipped')),
    result_summary TEXT,
    output_refs TEXT CHECK (output_refs IS NULL OR json_valid(output_refs)),
    error_summary TEXT,
    started_at INTEGER,
    finished_at INTEGER,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, plan_version_id)
        REFERENCES task_plan_versions(task_id, plan_version_id),
    UNIQUE (task_id, step_id),
    UNIQUE (plan_version_id, step_number),
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR ((started_at IS NOT NULL AND finished_at >= started_at) OR (status = 'skipped' AND started_at IS NULL AND finished_at >= created_at))),
    CHECK (status NOT IN ('completed', 'failed', 'skipped') OR finished_at IS NOT NULL)
);
CREATE INDEX idx_task_steps_execution
    ON task_steps(task_id, plan_version_id, status, step_number);

CREATE TABLE tool_calls (
    call_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT NOT NULL,
    tool_id TEXT NOT NULL,
    tool_version TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN (
        'created', 'running', 'succeeded', 'failed', 'timed_out', 'interrupted'
    )),
    input_data TEXT CHECK (input_data IS NULL OR json_valid(input_data)),
    output_data TEXT CHECK (output_data IS NULL OR json_valid(output_data)),
    error_code TEXT,
    error_message TEXT,
    started_at INTEGER,
    finished_at INTEGER,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    UNIQUE (task_id, call_id),
    CHECK (started_at IS NULL OR started_at >= created_at),
    CHECK (finished_at IS NULL OR (started_at IS NOT NULL AND finished_at >= started_at)),
    CHECK (status NOT IN ('succeeded', 'failed', 'timed_out') OR finished_at IS NOT NULL)
);
CREATE INDEX idx_tool_calls_task ON tool_calls(task_id, created_at);
CREATE INDEX idx_tool_calls_step ON tool_calls(step_id, created_at);
CREATE INDEX idx_tool_calls_recovery ON tool_calls(status)
    WHERE status IN ('created', 'running', 'interrupted');

CREATE TABLE task_operations (
    operation_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT NOT NULL,
    call_id TEXT,
    operation_sequence INTEGER NOT NULL CHECK (operation_sequence >= 1),
    operation_type TEXT NOT NULL,
    target_type TEXT NOT NULL,
    target_ref TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('prepared', 'completed', 'failed', 'uncertain')),
    undo_status TEXT NOT NULL CHECK (undo_status IN (
        'not_required', 'pending', 'undoing', 'undone', 'undo_failed', 'uncertain'
    )),
    input_ref TEXT,
    output_ref TEXT,
    before_state_ref TEXT,
    after_state_ref TEXT,
    recovery_data TEXT CHECK (recovery_data IS NULL OR json_valid(recovery_data)),
    error_message TEXT,
    created_at INTEGER NOT NULL,
    completed_at INTEGER,
    undone_at INTEGER,
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    FOREIGN KEY (task_id, call_id) REFERENCES tool_calls(task_id, call_id),
    UNIQUE (task_id, operation_sequence),
    UNIQUE (task_id, operation_id),
    CHECK (completed_at IS NULL OR completed_at >= created_at),
    CHECK (status <> 'completed' OR completed_at IS NOT NULL),
    CHECK (undone_at IS NULL OR undone_at >= created_at),
    CHECK (undo_status != 'undone' OR undone_at IS NOT NULL)
);
CREATE INDEX idx_task_operations_task ON task_operations(task_id, operation_sequence);
CREATE INDEX idx_task_operations_step ON task_operations(step_id, operation_sequence);
CREATE INDEX idx_task_operations_call ON task_operations(call_id);
CREATE INDEX idx_task_operations_undo ON task_operations(task_id, undo_status)
    WHERE undo_status IN ('pending', 'undoing', 'undo_failed', 'uncertain');

CREATE TABLE task_logs (
    log_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT,
    call_id TEXT,
    operation_id TEXT,
    event_type TEXT NOT NULL,
    severity TEXT NOT NULL CHECK (severity IN ('debug', 'info', 'warning', 'error')),
    message TEXT NOT NULL,
    details TEXT CHECK (details IS NULL OR json_valid(details)),
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id),
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    FOREIGN KEY (task_id, call_id) REFERENCES tool_calls(task_id, call_id),
    FOREIGN KEY (task_id, operation_id) REFERENCES task_operations(task_id, operation_id)
);
CREATE INDEX idx_task_logs_task_time ON task_logs(task_id, created_at);
CREATE INDEX idx_task_logs_severity ON task_logs(task_id, severity, created_at);

CREATE TABLE human_intervention_requests (
    request_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL,
    step_id TEXT,
    request_type TEXT NOT NULL CHECK (request_type IN (
        'clarification', 'decision', 'authorization', 'baseline_change', 'external_input'
    )),
    status TEXT NOT NULL CHECK (status IN ('open', 'resolved', 'withdrawn')),
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    reason TEXT NOT NULL,
    evidence TEXT CHECK (evidence IS NULL OR json_valid(evidence)),
    options TEXT CHECK (options IS NULL OR json_valid(options)),
    recommended_option TEXT CHECK (
        recommended_option IS NULL OR json_valid(recommended_option)
    ),
    required_decision TEXT NOT NULL,
    response TEXT CHECK (response IS NULL OR json_valid(response)),
    created_at INTEGER NOT NULL,
    resolved_at INTEGER,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id),
    FOREIGN KEY (task_id, step_id) REFERENCES task_steps(task_id, step_id),
    CHECK (resolved_at IS NULL OR resolved_at >= created_at),
    CHECK (status != 'resolved' OR (resolved_at IS NOT NULL AND response IS NOT NULL)),
    CHECK (status != 'withdrawn' OR resolved_at IS NOT NULL)
);
CREATE INDEX idx_human_requests_task ON human_intervention_requests(task_id, created_at);
CREATE INDEX idx_human_requests_open ON human_intervention_requests(status, created_at)
    WHERE status = 'open';

CREATE TABLE task_delivery_records (
    delivery_id TEXT PRIMARY KEY,
    task_id TEXT NOT NULL UNIQUE,
    delivered_candidates TEXT NOT NULL CHECK (json_valid(delivered_candidates)),
    delivery_summary TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (task_id) REFERENCES tasks(task_id)
);

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
    FOREIGN KEY (task_id, candidate_id)
        REFERENCES object_candidates(task_id, candidate_id),
    FOREIGN KEY (candidate_id, object_id)
        REFERENCES object_candidates(candidate_id, object_id),
    FOREIGN KEY (task_id, candidate_version_id)
        REFERENCES object_candidate_versions(task_id, candidate_version_id),
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

CREATE TABLE change_set_versions (
    change_set_version_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL CHECK (version_number >= 1),
    target_branch_id TEXT NOT NULL,
    base_branch_revision_id TEXT NOT NULL,
    content_hash TEXT,
    status TEXT NOT NULL DEFAULT 'draft' CHECK (status IN ('draft', 'sealed')),
    change_summary TEXT,
    created_at INTEGER NOT NULL,
    FOREIGN KEY (change_set_id) REFERENCES change_sets(change_set_id),
    UNIQUE (change_set_id, version_number),
    UNIQUE (change_set_id, change_set_version_id),
    UNIQUE (change_set_id, version_number, content_hash),
    UNIQUE (change_set_id, version_number, content_hash, target_branch_id),
    CHECK ((status = 'draft' AND content_hash IS NULL) OR (status = 'sealed' AND length(content_hash) = 64))
);
CREATE INDEX idx_change_set_versions_branch
    ON change_set_versions(target_branch_id, base_branch_revision_id);

CREATE TABLE change_items (
    change_item_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
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
    FOREIGN KEY (task_id, change_set_id)
        REFERENCES change_sets(source_task_id, change_set_id),
    FOREIGN KEY (change_set_id, version_number)
        REFERENCES change_set_versions(change_set_id, version_number),
    FOREIGN KEY (task_id, candidate_version_id)
        REFERENCES object_candidate_versions(task_id, candidate_version_id),
    FOREIGN KEY (candidate_id, object_id)
        REFERENCES object_candidates(candidate_id, object_id),
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
    FOREIGN KEY (change_set_id, version_number, content_hash, target_branch_id)
        REFERENCES change_set_versions(change_set_id, version_number, content_hash, target_branch_id),
    UNIQUE (authorization_id, change_set_id, version_number),
    CHECK (revoked_at IS NULL OR revoked_at >= decided_at),
    CHECK (revoked_at IS NULL OR decision = 'authorized')
);
CREATE INDEX idx_change_authorizations_version
    ON change_authorizations(change_set_id, version_number, decided_at DESC);

CREATE TABLE application_reports (
    application_id TEXT PRIMARY KEY,
    change_set_id TEXT NOT NULL,
    version_number INTEGER NOT NULL,
    authorization_id TEXT NOT NULL,
    result_branch_revision_id TEXT,
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
    CHECK (status != 'applied' OR (failed_item_count = 0 AND applied_item_count >= 1 AND result_branch_revision_id IS NOT NULL))
);
CREATE INDEX idx_application_reports_change_set
    ON application_reports(change_set_id, started_at DESC);
CREATE INDEX idx_application_reports_recovery
    ON application_reports(status, started_at)
    WHERE status IN ('prepared', 'applying', 'interrupted', 'recovery_required');


-- Finalized validation manifests are immutable; report finalization checks exact scope.
CREATE TRIGGER trg_validation_report_finalize_guard
BEFORE UPDATE OF status ON validation_reports
WHEN OLD.status = 'running' AND NEW.status <> 'running'
BEGIN
  SELECT CASE
    WHEN (SELECT COUNT(*) FROM validation_report_items i WHERE i.task_id=OLD.task_id AND i.validation_report_id=OLD.validation_report_id) = 0
    THEN RAISE(ABORT, 'validation report requires a non-empty manifest') END;
  SELECT CASE
    WHEN OLD.validation_scope='object_candidate' AND (
      (SELECT COUNT(*) FROM validation_report_items i WHERE i.task_id=OLD.task_id AND i.validation_report_id=OLD.validation_report_id) <> 1
      OR NOT EXISTS (SELECT 1 FROM validation_report_items i WHERE i.task_id=OLD.task_id AND i.validation_report_id=OLD.validation_report_id AND i.candidate_version_id=OLD.candidate_version_id)
    ) THEN RAISE(ABORT, 'single-object report manifest must match top-level candidate version') END;
END;
CREATE TRIGGER trg_validation_items_insert_open_only
BEFORE INSERT ON validation_report_items
WHEN (SELECT status FROM validation_reports WHERE task_id=NEW.task_id AND validation_report_id=NEW.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report manifest is finalized'); END;
CREATE TRIGGER trg_validation_items_update_finalized
BEFORE UPDATE ON validation_report_items
WHEN (SELECT status FROM validation_reports WHERE validation_report_id=OLD.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report manifest is immutable'); END;
CREATE TRIGGER trg_validation_items_delete_finalized
BEFORE DELETE ON validation_report_items
WHEN (SELECT status FROM validation_reports WHERE validation_report_id=OLD.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report manifest is immutable'); END;
CREATE TRIGGER trg_validation_issues_insert_open_only
BEFORE INSERT ON validation_issues
WHEN (SELECT status FROM validation_reports WHERE validation_report_id=NEW.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report issues are finalized'); END;
CREATE TRIGGER trg_validation_issues_update_finalized
BEFORE UPDATE ON validation_issues
WHEN (SELECT status FROM validation_reports WHERE validation_report_id=OLD.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report issues are immutable'); END;
CREATE TRIGGER trg_validation_issues_delete_finalized
BEFORE DELETE ON validation_issues
WHEN (SELECT status FROM validation_reports WHERE validation_report_id=OLD.validation_report_id) <> 'running'
BEGIN SELECT RAISE(ABORT, 'validation report issues are immutable'); END;

-- Change Set versions can be assembled only while draft. Sealing freezes the header and item list.
CREATE TRIGGER trg_change_version_seal_guard
BEFORE UPDATE OF status ON change_set_versions
WHEN OLD.status='draft' AND NEW.status='sealed'
BEGIN
  SELECT CASE WHEN NEW.content_hash IS NULL OR length(NEW.content_hash) <> 64
    THEN RAISE(ABORT, 'sealed Change Set version requires SHA-256 content hash') END;
  SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM change_items i WHERE i.change_set_id=OLD.change_set_id AND i.version_number=OLD.version_number)
    THEN RAISE(ABORT, 'sealed Change Set version requires at least one item') END;
END;
CREATE TRIGGER trg_change_version_no_update_after_seal
BEFORE UPDATE ON change_set_versions
WHEN OLD.status='sealed'
BEGIN SELECT RAISE(ABORT, 'sealed Change Set version is immutable'); END;
CREATE TRIGGER trg_change_version_no_delete_after_seal
BEFORE DELETE ON change_set_versions
WHEN OLD.status='sealed'
BEGIN SELECT RAISE(ABORT, 'sealed Change Set version is immutable'); END;
CREATE TRIGGER trg_change_items_insert_draft_only
BEFORE INSERT ON change_items
WHEN (SELECT status FROM change_set_versions WHERE change_set_id=NEW.change_set_id AND version_number=NEW.version_number) <> 'draft'
BEGIN SELECT RAISE(ABORT, 'Change Set items can only be added to draft versions'); END;
CREATE TRIGGER trg_change_items_update_draft_only
BEFORE UPDATE ON change_items
WHEN (SELECT status FROM change_set_versions WHERE change_set_id=OLD.change_set_id AND version_number=OLD.version_number) <> 'draft'
BEGIN SELECT RAISE(ABORT, 'sealed Change Set items are immutable'); END;
CREATE TRIGGER trg_change_items_delete_draft_only
BEFORE DELETE ON change_items
WHEN (SELECT status FROM change_set_versions WHERE change_set_id=OLD.change_set_id AND version_number=OLD.version_number) <> 'draft'
BEGIN SELECT RAISE(ABORT, 'sealed Change Set items are immutable'); END;
CREATE TRIGGER trg_authorization_sealed_version_only
BEFORE INSERT ON change_authorizations
WHEN (SELECT status FROM change_set_versions WHERE change_set_id=NEW.change_set_id AND version_number=NEW.version_number) <> 'sealed'
BEGIN SELECT RAISE(ABORT, 'only sealed Change Set versions can be authorized'); END;


CREATE TRIGGER trg_validation_report_no_update_after_final
BEFORE UPDATE ON validation_reports WHEN OLD.status <> 'running'
BEGIN SELECT RAISE(ABORT, 'finalized validation report is immutable'); END;
CREATE TRIGGER trg_validation_report_no_delete
BEFORE DELETE ON validation_reports
BEGIN SELECT RAISE(ABORT, 'validation report history cannot be deleted'); END;
CREATE TRIGGER trg_candidate_version_payload_immutable
BEFORE UPDATE OF candidate_version_id, task_id, candidate_id, version_number, payload_ref, geometry_ref, attributes, source_refs, base_object_version_ref, created_at ON object_candidate_versions
BEGIN SELECT RAISE(ABORT, 'candidate version payload is immutable; create a new version'); END;
CREATE TRIGGER trg_candidate_version_no_delete
BEFORE DELETE ON object_candidate_versions
BEGIN SELECT RAISE(ABORT, 'candidate version history cannot be deleted'); END;
CREATE TRIGGER trg_candidate_identity_immutable
BEFORE UPDATE OF candidate_id, task_id, candidate_set_id, created_at ON object_candidates
BEGIN SELECT RAISE(ABORT, 'candidate identity is immutable'); END;
CREATE TRIGGER trg_candidate_object_binding_immutable
BEFORE UPDATE OF object_id ON object_candidates
WHEN OLD.object_id IS NOT NULL AND NEW.object_id IS NOT OLD.object_id
BEGIN SELECT RAISE(ABORT, 'a bound Candidate Object ID cannot be changed or cleared'); END;
CREATE TRIGGER trg_candidate_identity_no_delete
BEFORE DELETE ON object_candidates
BEGIN SELECT RAISE(ABORT, 'candidate identity history cannot be deleted'); END;
CREATE TRIGGER trg_candidate_edit_no_update
BEFORE UPDATE ON candidate_edit_records
BEGIN SELECT RAISE(ABORT, 'candidate edit records are immutable'); END;
CREATE TRIGGER trg_candidate_edit_no_delete
BEFORE DELETE ON candidate_edit_records
BEGIN SELECT RAISE(ABORT, 'candidate edit records cannot be deleted'); END;
CREATE TRIGGER trg_contract_version_no_update
BEFORE UPDATE ON task_contract_versions
BEGIN SELECT RAISE(ABORT, 'task contract versions are immutable'); END;
CREATE TRIGGER trg_contract_version_no_delete
BEFORE DELETE ON task_contract_versions
BEGIN SELECT RAISE(ABORT, 'task contract versions cannot be deleted'); END;
CREATE TRIGGER trg_plan_version_content_immutable
BEFORE UPDATE OF task_id, version_number, plan_summary, planning_reason, created_at ON task_plan_versions
BEGIN SELECT RAISE(ABORT, 'plan version content is immutable'); END;
CREATE TRIGGER trg_plan_version_no_delete
BEFORE DELETE ON task_plan_versions
BEGIN SELECT RAISE(ABORT, 'plan version history cannot be deleted'); END;
CREATE TRIGGER trg_task_step_definition_immutable
BEFORE UPDATE OF task_id, plan_version_id, step_number, objective, input_data, action_description, acceptance_criteria, created_at ON task_steps
BEGIN SELECT RAISE(ABORT, 'task step definition is immutable; replan with a new plan version'); END;
CREATE TRIGGER trg_task_delivery_no_update
BEFORE UPDATE ON task_delivery_records
BEGIN SELECT RAISE(ABORT, 'task delivery records are immutable'); END;
CREATE TRIGGER trg_task_delivery_no_delete
BEFORE DELETE ON task_delivery_records
BEGIN SELECT RAISE(ABORT, 'task delivery records cannot be deleted'); END;
CREATE TRIGGER trg_task_log_no_update
BEFORE UPDATE ON task_logs
BEGIN SELECT RAISE(ABORT, 'task logs are append-only'); END;
CREATE TRIGGER trg_task_log_no_delete
BEFORE DELETE ON task_logs
BEGIN SELECT RAISE(ABORT, 'task logs are append-only'); END;
CREATE TRIGGER trg_selection_snapshot_no_update
BEFORE UPDATE ON selection_snapshots
BEGIN SELECT RAISE(ABORT, 'selection snapshots are immutable'); END;
CREATE TRIGGER trg_selection_snapshot_no_delete
BEFORE DELETE ON selection_snapshots
BEGIN SELECT RAISE(ABORT, 'selection snapshots cannot be deleted'); END;
CREATE TRIGGER trg_selection_item_no_update
BEFORE UPDATE ON selection_items
BEGIN SELECT RAISE(ABORT, 'selection items are immutable'); END;
CREATE TRIGGER trg_selection_item_no_delete
BEFORE DELETE ON selection_items
BEGIN SELECT RAISE(ABORT, 'selection items cannot be deleted'); END;
CREATE TRIGGER trg_authorization_content_immutable
BEFORE UPDATE OF change_set_id, version_number, content_hash, target_branch_id, decision, authorized_by, decision_evidence, decided_at ON change_authorizations
BEGIN SELECT RAISE(ABORT, 'authorization decision evidence is immutable'); END;
CREATE TRIGGER trg_authorization_no_delete
BEFORE DELETE ON change_authorizations
BEGIN SELECT RAISE(ABORT, 'authorization history cannot be deleted'); END;

CREATE UNIQUE INDEX idx_change_one_active_authorization
ON change_authorizations(change_set_id, version_number)
WHERE decision='authorized' AND revoked_at IS NULL;
CREATE TRIGGER trg_authorization_current_version_only
BEFORE INSERT ON change_authorizations
WHEN NEW.version_number <> (SELECT current_version_number FROM change_sets WHERE change_set_id=NEW.change_set_id)
  OR (SELECT status FROM change_sets WHERE change_set_id=NEW.change_set_id) <> 'ready'
BEGIN SELECT RAISE(ABORT, 'authorization requires the current sealed, validated Ready version'); END;
CREATE TRIGGER trg_authorization_revoke_once
BEFORE UPDATE OF revoked_at, revocation_reason ON change_authorizations
WHEN OLD.revoked_at IS NOT NULL OR NEW.revoked_at IS NULL
BEGIN SELECT RAISE(ABORT, 'authorization may be revoked once and cannot be unrevoked'); END;
CREATE TRIGGER trg_change_version_insert_before_applied_only
BEFORE INSERT ON change_set_versions
WHEN (SELECT status FROM change_sets WHERE change_set_id=NEW.change_set_id) IN ('applied', 'cancelled')
BEGIN SELECT RAISE(ABORT, 'applied/cancelled Change Set is closed; create a new Change Set'); END;
CREATE TRIGGER trg_change_set_current_version_immutable_after_apply
BEFORE UPDATE OF current_version_number ON change_sets
WHEN OLD.status='applied' AND NEW.current_version_number <> OLD.current_version_number
BEGIN SELECT RAISE(ABORT, 'an applied Change Set cannot change its current version'); END;
CREATE UNIQUE INDEX idx_application_one_active_attempt
ON application_reports(change_set_id, version_number)
WHERE status IN ('prepared','applying','recovery_required');
CREATE TRIGGER trg_application_report_authorized_only
BEFORE INSERT ON application_reports
WHEN NOT EXISTS (
 SELECT 1 FROM change_authorizations a
 JOIN change_set_versions v ON v.change_set_id=a.change_set_id AND v.version_number=a.version_number
 JOIN change_sets cs ON cs.change_set_id=a.change_set_id
 WHERE a.authorization_id=NEW.authorization_id AND a.change_set_id=NEW.change_set_id
 AND a.version_number=NEW.version_number AND a.decision='authorized' AND a.revoked_at IS NULL
 AND v.status='sealed' AND cs.current_version_number=NEW.version_number
 AND cs.status IN ('ready','applying')
)
BEGIN SELECT RAISE(ABORT, 'application attempt requires active authorization for current sealed version'); END;
CREATE TRIGGER trg_application_report_finalize_guard
BEFORE UPDATE OF status ON application_reports WHEN NEW.status='applied'
BEGIN
 SELECT CASE WHEN NEW.failed_item_count <> 0 THEN RAISE(ABORT, 'successful application cannot contain failed items') END;
 SELECT CASE WHEN NEW.applied_item_count <> (SELECT COUNT(*) FROM change_items i WHERE i.change_set_id=NEW.change_set_id AND i.version_number=NEW.version_number) THEN RAISE(ABORT, 'application item count must match sealed Change Set item count') END;
 SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM change_authorizations a WHERE a.authorization_id=NEW.authorization_id AND a.decision='authorized' AND a.revoked_at IS NULL) THEN RAISE(ABORT, 'successful application requires authorized decision') END;
END;
CREATE TRIGGER trg_change_set_applied_requires_report
BEFORE UPDATE OF status ON change_sets WHEN NEW.status='applied'
BEGIN
 SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM application_reports r WHERE r.change_set_id=NEW.change_set_id AND r.version_number=NEW.current_version_number AND r.status='applied') THEN RAISE(ABORT, 'Change Set cannot be Applied without successful Application Report') END;
END;
CREATE TRIGGER trg_applied_report_immutable
BEFORE UPDATE ON application_reports WHEN OLD.status='applied'
BEGIN SELECT RAISE(ABORT, 'successful Application Report is immutable'); END;
CREATE TRIGGER trg_application_report_no_delete
BEFORE DELETE ON application_reports
BEGIN SELECT RAISE(ABORT, 'application report history cannot be deleted'); END;
CREATE TRIGGER trg_validation_report_counts_match
BEFORE UPDATE OF status ON validation_reports WHEN OLD.status='running' AND NEW.status <> 'running'
BEGIN
 SELECT CASE WHEN NEW.error_count <> (SELECT COUNT(*) FROM validation_issues WHERE validation_report_id=OLD.validation_report_id AND severity='error') THEN RAISE(ABORT, 'validation error_count mismatch') END;
 SELECT CASE WHEN NEW.warning_count <> (SELECT COUNT(*) FROM validation_issues WHERE validation_report_id=OLD.validation_report_id AND severity='warning') THEN RAISE(ABORT, 'validation warning_count mismatch') END;
 SELECT CASE WHEN NEW.info_count <> (SELECT COUNT(*) FROM validation_issues WHERE validation_report_id=OLD.validation_report_id AND severity='info') THEN RAISE(ABORT, 'validation info_count mismatch') END;
END;

INSERT INTO schema_migrations(version, migration_id, applied_at) VALUES (1, '0001_unified_ai_assistant_schema_v1_14', CAST(strftime('%s','now') AS INTEGER) * 1000);
COMMIT;