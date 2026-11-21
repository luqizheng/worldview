# World View P2-09-13 — Integrated Core Domain + AI Assistant Schema Inventory and Migration Dependency Graph V0.1

- Date: 2026-11-21
- Status: Integrated logical inventory and migration dependency proposal — not executable SQL and not implementation authorization
- Depends on: P2-09-01~12; Core Domain P2-09-11 V0.7; AI Assistant Schema V1.16; AI/Core Integration Review V0.8

## 1. Purpose

Consolidate the current Core Domain and AI Assistant logical persistence designs into one Project-local SQLite inventory and deterministic migration dependency graph. This document is the bridge between the separate domain-owned schema proposals and the later executable Schema V2.0 package.

It does not freeze concrete SQL types, trigger bodies, indexes or migration scripts. Those remain subject to the final Command descriptor catalog, Calendar conversion validation and provider proof results.

## 2. Ownership and one-database rule

One Project has one local SQLite database. It contains two logically independent persistence owners:

- Core Domain owns formal Project/Map/Calendar/Object/History/Command/Event/Asset/Checkpoint facts.
- AI Assistant owns Task/Plan/Tool execution/Candidate/Validation/Selection/Proposal/Authorization/Submission evidence.

Application Bootstrap owns only:
- the shared `schema_migration` ledger;
- migration dependency ordering and startup orchestration;
- the physical connection/transaction coordinator for authorized cross-module commits.

The coordinator is not a third domain authority. Core Domain repositories do not write AI tables; AI repositories do not write Core Domain tables.

## 3. Logical table inventory

### 3.1 Core Domain — 20 baseline tables

| # | Table | Responsibility |
|---:|---|---|
| 1 | `core_project` | Project identity and lifecycle |
| 2 | `core_map` | Map identity and lifecycle |
| 3 | `core_calendar` | Map-owned default/auxiliary Calendar identities |
| 4 | `core_calendar_configuration` | History-scoped, time-effective Calendar display rules |
| 5 | `core_map_initial_state` | One authoritative time-zero baseline per Map |
| 6 | `core_map_object_ref` | Map reference/membership registration for formal Objects |
| 7 | `core_map_initial_object` | Object membership and initial placement/relationships at time zero |
| 8 | `core_object_type` | Project-owned Object Type definitions and allocation policy |
| 9 | `core_object` | Stable Object identity and lifecycle (draft/active/abandoned/retired) |
| 10 | `core_object_definition` | One current shared/local base definition per active Object |
| 11 | `core_object_id_allocator` | Object Type-scoped allocation state |
| 12 | `core_object_id_reservation` | Permanent Object ID allocation and binding lifecycle |
| 13 | `core_history` | Unified Map-root / Branch-owned History identity |
| 14 | `core_branch` | Parent History, exact fork boundary and own History reference |
| 15 | `core_command` | Authoritative author-editable operation history |
| 16 | `core_event` | Optional narrative historical occurrences |
| 17 | `core_command_event` | Optional explanatory/grouping association between Commands and Events |
| 18 | `core_command_order_constraint` | Explicit user-authored ordering/dependency relations |
| 19 | `core_asset` | Immutable formal Asset metadata and content identity |
| 20 | `core_checkpoint` | File-backed reconstruction acceleration metadata |

### 3.2 Conditional Core Domain typed link tables — up to 5

These are normalized reference tables and should be added only when the concrete Command/Event/Asset payload catalog requires them. Do not create speculative generic link frameworks.

| Table | Purpose | Inclusion condition |
|---|---|---|
| `core_command_object_ref` | Typed Object references from Commands | Command descriptors need relational Object references beyond typed payload validation |
| `core_event_object_ref` | Event participants/target Objects | Event model requires indexed/validated Object participation |
| `core_object_definition_asset` | Immutable Assets used by Object definitions | Object definition schema supports formal Asset references |
| `core_map_initial_object_asset` | Assets used by Map Initial State object membership/placement | Initial State supports formal Asset references |
| `core_command_asset` | Immutable Assets used by formal Commands | Command descriptors require formal Asset references |

### 3.3 AI Assistant — 28 domain tables

| Group | Tables | Count |
|---|---|---:|
| Task execution | `tasks`, `task_contract_versions`, `task_plan_versions`, `task_steps`, `tool_calls`, `task_operations`, `task_logs`, `human_intervention_requests`, `task_delivery_records` | 9 |
| Candidate / validation | `candidate_sets`, `object_candidates`, `object_candidate_versions`, `candidate_edit_records`, `validation_reports`, `validation_report_items`, `validation_issues` | 7 |
| Selection | `selection_snapshots`, `selection_items` | 2 |
| Proposal / submission | `change_sets`, `change_set_versions`, `command_proposal_items`, `proposal_ordering_relations`, `change_authorizations`, `authorization_revocations`, `submission_attempts`, `submission_command_refs`, `submission_asset_refs`, `application_reports` | 10 |
| **Total AI-owned domain tables** |  | **28** |

### 3.4 Shared Application metadata

- `schema_migration`: one shared ledger table with `module_id`, `migration_id`, checksum, applied timestamp and relevant execution metadata.
- It is owned/orchestrated by Application Bootstrap and is not counted as a Core Domain or AI Assistant domain table.
- Do not create separate Core and AI migration-ledger tables.

### 3.5 Inventory count

- Core Domain: 20 baseline tables + 0–5 conditional typed link tables.
- AI Assistant: 28 domain tables.
- Application Bootstrap: 1 shared migration ledger.
- Expected total: **49 baseline tables + 0–5 conditional Core link tables**, before final DDL-specific migration metadata or indexes/triggers (which are not domain tables).

## 4. Cross-module key and reference rules

| AI reference | Core relationship |
|---|---|
| `object_identity_id` | Enforced FK to `core_object`; Core retains draft/active/abandoned/retired identities as tombstones |
| `reservation_id` | Enforced FK to permanent `core_object_id_reservation` |
| `core_asset_id` | Enforced FK from `submission_asset_refs` to `core_asset`; referenced Assets cannot be garbage-collected |
| `target_map_id` | Opaque UUID evidence; Core validates current existence/lifecycle at preflight |
| `target_branch_id` | Opaque UUID evidence; Core validates current existence/lifecycle at preflight |
| `target_history_id` | Opaque UUID evidence; Core resolves current writable History at preflight |
| `core_command_ref` | Opaque UUID evidence; Commands may be edited/deleted while AI evidence must remain |
| target History/effective Object/lineage context | Opaque precondition token/hash, not revision/version entities |

Never cascade-delete AI evidence from a Core lifecycle operation. Use restrictive FKs only for permanent Core identities whose retention policy guarantees the reference remains valid.

## 5. Migration dependency graph

```text
Application Bootstrap
  └── 000_shared_schema_migration_ledger
       ├── Core Domain migration namespace
       │    ├── 001_core_project
       │    ├── 002_core_object_type_identity_allocator_reservation
       │    ├── 003_core_map_history_branch_initial_state
       │    ├── 004_core_calendar_identity
       │    ├── 005_core_command_event_ordering
       │    ├── 006_core_calendar_configuration
       │    ├── 007_core_asset_checkpoint_and_typed_links
       │    └── 008_core_integrity_indexes_triggers
       │
       └── AI Assistant migration namespace
            ├── 101_ai_task_execution
            ├── 102_ai_candidate_validation
            ├── 103_ai_selection
            ├── 104_ai_change_set_proposal_authorization
            ├── 105_ai_submission_revocation_and_asset_refs
            └── 106_ai_core_reference_constraints_and_indexes
```

The numbers are illustrative migration ordering slots, not frozen migration filenames.

### 5.1 Dependency notes

1. Project identity exists before any Project-scoped row.
2. Object Type exists before allocator, reservation and Object identity rows.
3. Map exists before its root History, Initial State and Calendar identity; Map creation creates these related rows in one Core Domain transaction.
4. Branch-owned History is created before its Branch row within one transaction; `core_history` does not point back to `core_branch`, avoiding a circular FK.
5. Calendar identity requires Map; Calendar Configuration requires Calendar, History and Command tables.
6. Command/Event/ordering tables require History. `core_command_event` references Command and Event in the same History.
7. Asset metadata exists before typed Core asset links and AI `submission_asset_refs`.
8. AI base tables can be created after Core keys are available; AI integration FKs are added only after their Core target tables exist.
9. The shared migration ledger is created before either module's first migration and records module namespace + checksum.

## 6. Bootstrap and migration execution rules

- Application Bootstrap loads Core Domain and AI migration manifests.
- Each module owns its migration SQL and migration IDs; Bootstrap owns ordering and ledger writes.
- Before applying a migration, verify dependencies and checksum state.
- Apply one migration transaction at a time where SQLite DDL semantics permit; record the ledger row only after the migration succeeds.
- If a migration fails or the database contains an unknown/partial state, fail closed and mark the Project as requiring recovery; do not silently skip, rewrite or guess.
- Re-running bootstrap against a valid initialized database is idempotent.
- The migration checksum is immutable after application. A changed migration requires a new migration ID.
- Do not allow normal Project writes until all required Core and AI migrations and structural integrity checks pass.

## 7. Final authorized submission transaction

The final AI submission is the only required cross-module atomic commit boundary:

1. Outside the transaction, finish LLM work, validation, authorization UX, geometry generation and staging Asset preparation.
2. Publish immutable Asset files to the Asset Store and verify content hashes.
3. Application Commit Coordinator opens one SQLite connection and one transaction.
4. Revalidate exact sealed proposal hash, human authorization, revocation absence, target History context, Object identities/reservations and published Asset hashes.
5. Core Domain Domain Services persist formal Commands, required Command/Event/order relations, Object insertion/ID state transitions and Core Asset metadata/references.
6. AI repositories persist `submission_attempts`, exact `submission_command_refs`, `submission_asset_refs` and the final known-outcome report.
7. Commit once.
8. If any DB write fails before commit, roll back both modules. Already published but unreferenced immutable files remain orphans for safe cleanup; never delete by assumption.

No LLM call, user interaction, long validation, file staging or network request occurs inside this transaction.

## 8. Remaining gates before executable Schema V2.0

1. Complete the concrete V1 Command descriptor catalog and typed payload schemas.
2. Validate Calendar period-to-internal-interval conversion and Calendar Configuration overlap/recalculation against complete D01 examples.
3. Validate final Core table constraints/triggers and decide which conditional typed link tables are actually required by the Command/Event catalog.
4. Generate executable DDL and module-owned migration scripts from the approved relational design.
5. Execute P2-09-12 provider/transaction proof tests on packaged Windows and macOS runtimes.
6. Finalize WAL/PRAGMA durability policy and prove Project database + Asset Store backup/restore.
7. Only after all gates pass, freeze integrated Schema V2.0 and prepare the developer implementation handoff.

## 9. Status

This document consolidates the integrated logical inventory and dependency graph. It is not executable SQL, does not claim transaction proof, and does not authorize implementation. The development team remains responsible for implementation and test execution after the design gates are closed.