# World View P2-09-13 — Integrated Core Domain + AI Assistant Schema Inventory and Migration Dependency Graph V0.3

- Version: 0.3
- Date: 2026-11-21
- Status: Command-catalog-reconciled integrated logical inventory and migration dependency proposal — not executable SQL and not implementation authorization
- Supersedes: P2-09-13 V0.2 for descriptor-driven typed-link selection gates; V0.2 is retained as history
- Supersedes: P2-09-13 V0.1 for migration dependency ordering and explicit ownership integration; V0.1 is retained as history
- Depends on: P2-09-01~12; Core Domain P2-09-11 V0.8; AI Assistant Schema V1.16; AI/Core Integration Review V0.8; P2-09-16 V0.1; P2-09-14 V0.1; P2-09-15 V0.2; P2-09-17 V0.2

## 1. Purpose

Consolidate the current Core Domain and AI Assistant logical persistence designs into one Project-local SQLite inventory and deterministic migration dependency graph. This document is the bridge between the separate domain-owned schema proposals and the later executable Schema V2.0 package.

It does not freeze concrete SQL types, trigger bodies, indexes or migration scripts. The bounded Command catalog and payload contract are now recorded in P2-09-15 V0.2 and P2-09-17 V0.2; the remaining physical decisions are conditional typed-link selection, Calendar validation and provider proof.

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
| 9 | `core_object` | Stable Object identity, `sharing_scope`, nullable `owner_map_id`, and lifecycle (draft/active/abandoned/retired) |
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

These are normalized reference tables and should be added only when the enabled descriptor/payload contracts require them. P2-09-15 V0.2 and P2-09-17 V0.2 provide the current reference matrix. Do not create speculative generic link frameworks.

| Table | Purpose | Inclusion condition |
|---|---|---|
| `core_command_object_ref` | Typed Object references from Commands | Required by the current Object/Geometry/Relationship Command descriptors; roles are descriptor-declared and validated |
| `core_event_object_ref` | Event participants/target Objects | Event model requires indexed/validated Object participation |
| `core_object_definition_asset` | Immutable Assets used by Object definitions | Object definition schema supports formal Asset references |
| `core_map_initial_object_asset` | Assets used by Map Initial State object membership/placement | Initial State supports formal Asset references |
| `core_command_asset` | Immutable Assets used by formal Commands | Required when geometry/other enabled Command descriptors consume immutable formal Asset inputs; expected for the current geometry descriptors |

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

Object ownership rule: `core_object.sharing_scope='project_shared'` requires `owner_map_id IS NULL`; `map_local` requires a non-null same-Project `owner_map_id`. A Map-local Object may be registered only to its owner Map; a Project-shared Object may be referenced by any Map in the same Project. Core Domain Services enforce the rule and SQLite INSERT/UPDATE triggers provide defense-in-depth. SQLite composite-FK NULL semantics must not be used as a substitute for the explicit CHECK and registration validation.

Never cascade-delete AI evidence from a Core lifecycle operation. Use restrictive FKs only for permanent Core identities whose retention policy guarantees the reference remains valid.

## 5. Migration dependency graph

The dependency graph is updated to honor the V0.8 `core_object.owner_map_id` foreign key. Map identity structures must exist before the Object identity table can be created with that FK.

```text
Application Bootstrap
  └── 000_shared_schema_migration_ledger
       ├── Core Domain migration namespace
       │    ├── 001_core_project
       │    ├── 002_core_map_identity
       │    ├── 003_core_object_type
       │    ├── 004_core_object_identity_owner_map_definition_allocator_reservation
       │    ├── 005_core_history_branch_initial_state_and_map_object_refs
       │    ├── 006_core_calendar_identity
       │    ├── 007_core_command_event_ordering
       │    ├── 008_core_calendar_configuration
       │    ├── 009_core_asset_checkpoint_and_typed_links
       │    └── 010_core_integrity_indexes_triggers
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
2. Map identity structures exist before `core_object.owner_map_id` can reference `(project_id,map_id)`.
3. Object Type exists before Object identity, allocator and reservation rows; `core_object` depends on both Project and Map identity structures.
4. Root/Branch History, Branch and Map Initial State structures depend on Map identity. Branch-owned History is created before its Branch row within one transaction; `core_history` does not point back to `core_branch`, avoiding a circular FK.
5. `core_map_object_ref` requires both Map and Object identity; `core_map_initial_object` requires Map Initial State, Map/Object reference and Object identity.
6. Calendar identity requires Map; Calendar Configuration requires Calendar, History and Command tables.
7. Command/Event/ordering tables require History. `core_command_event` references Command and Event in the same History.
8. Asset metadata exists before typed Core asset links and AI `submission_asset_refs`.
9. AI base tables may be created independently, but AI Core-reference FKs/indexes are applied only after the permanent Core Object, Reservation and Asset targets exist.
10. The shared migration ledger is created before either module's first migration and records module namespace + checksum.

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

1. Review and close the enabled V1 descriptor subset, finalize machine-readable payload schemas and canonical serialization, and confirm handler compatibility retention.
2. Validate Calendar period-to-internal-interval conversion and Calendar Configuration overlap/recalculation against complete D01 examples.
3. Validate final Core table constraints/triggers and finalize the conditional typed-link set from the P2-09-15/P2-09-17 descriptor reference matrix.
4. Generate executable DDL and module-owned migration scripts from the approved relational design.
5. Execute P2-09-12 provider/transaction proof tests on packaged Windows and macOS runtimes.
6. Finalize WAL/PRAGMA durability policy and prove Project database + Asset Store backup/restore.
7. Only after all gates pass, freeze integrated Schema V2.0 and prepare the developer implementation handoff.

## 9. Status

V0.3 retains the V0.2 logical inventory: 20 Core Domain baseline tables, up to 5 conditional typed links, exactly 28 AI Assistant domain tables, and one shared Application migration ledger (49 baseline tables plus 0–5 conditional Core links). It corrects the migration dependency order required by `core_object.owner_map_id` and records the explicit Shared/Map-local Object registration invariant.

The inventory is not executable SQL, does not claim transaction proof, and does not authorize implementation. Checkpoint fingerprint details are governed by P2-09-16 V0.1 and the reconciled P2-09-11 V0.8. The development team remains responsible for implementation and test execution after the design gates are closed.