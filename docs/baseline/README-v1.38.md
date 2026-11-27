# World View AI Assistant — Design Package V1.38

- Date: 2026-11-21
- Status: Design package index
- Scope: World View Core Domain persistence and AI Assistant Task Planning & Execution Engine V1.0 integration
- Deployment target: Single-user, local-first Windows/macOS desktop application

## 1. Current baseline

- AI Assistant logical database V1.16 is the integrated AI schema baseline; it is not executable SQL.
- Core Domain relational schema P2-09-11 V0.7 is a detailed proposal, not executable DDL.
- P2-09-13 V0.1 is the integrated logical inventory and migration dependency proposal.
- P2-09-12 V0.2 is a development verification specification; no provider or cross-module transaction tests are claimed to have passed.
- Avalonia 12 + .NET 10 + Microsoft.Data.Sqlite 11.x remains the recommended runtime/provider direction, subject to packaged Windows/macOS verification.
- Core Domain owns formal map/history facts. AI Assistant owns task execution, staging and immutable proposal/authorization/submission evidence.
- One project-local SQLite file plus immutable Asset Store files and file-backed Checkpoints is the recommended persistence unit.
- No implementation authorization has been granted.

## 2. P2-09 integrated design documents

| Document | Status | Purpose |
|---|---|---|
| `world-view-core-domain-p2-09-11-physical-schema-design-v0.7.md` | Detailed relational proposal | Core tables, stable keys, constraints, Calendar, reservations, assets, Checkpoints and migration boundaries |
| `world-view-core-domain-p2-09-ai-schema-integration-review-v0.8.md` | Detailed integration review | AI/Core references, immutable authorization/revocation evidence and published Asset references |
| `world-view-ai-assistant-unified-logical-database-schema-v1.16.md` | Integrated AI logical schema | 28 AI-owned tables; not executable SQL |
| `world-view-core-domain-p2-09-13-integrated-schema-inventory-migration-graph-v0.1.md` | Integrated logical inventory | 20 Core baseline tables + up to 5 conditional links, 28 AI tables, one shared ledger |
| `world-view-core-domain-p2-09-12-provider-transaction-proof-test-spec-v0.2.md` | Development verification specification | Provider, shared transaction, crash recovery, migration and backup/restore tests |
| `world-view-core-domain-p2-09-14-historical-time-calendar-validation-contract-v0.1.md` | Recommended validation baseline | Canonical time, Calendar conversion, interval rules, sustained Commands and schema gaps |

## 3. Current semantic and persistence decisions

- Map roots and Branch-owned histories use one unified History entity model.
- Branch inheritance is zero-copy and includes the closed fork boundary. Branch-owned Command precision intervals must begin strictly after `parent_at`.
- Command is authoritative authoring history; Event is optional narrative history; State Change is generated deterministically in memory in V1; World State is reconstructed.
- Shared Object definition edits are project-level and retroactive for historical reconstruction. No replayable Shared Object definition history is introduced in V1.
- Post-zero Event-driven runtime changes belong only to the active Map/Branch History path.
- Object ID reservations use `Reserved`, `Bound`, `Inserted`, `Abandoned`; committed IDs are never recycled and Task Undo does not reverse reservation consumption.
- Each Map has one default Calendar and optional auxiliary Calendars. Calendar Configurations are time-effective display rules; Calendar reform does not rebuild World State.
- AI Assistant never writes formal Core Domain tables directly.

## 4. Remaining gates

1. Reconcile P2-09-14's Calendar conversion provenance and sustained end-time precision amendments into P2-09-11.
2. Complete the concrete V1 Command descriptor catalog and typed payload schemas without inventing unsupported command types.
3. Validate Calendar conversion and interval rules against representative custom Calendar fixtures and D01 examples.
4. Confirm exact Object Type display grammar; Project + Object Type allocation scope and separate stable UUID storage key remain the recommended V1 design.
5. Execute P2-09-12 shared-transaction proof tests on packaged Windows/macOS runtimes.
6. Verify WAL durability, backup/restore and Asset Store recovery.
7. Generate executable integrated DDL/migrations only after all design and verification gates close.

## 5. Implementation readiness

**Not ready for full implementation.** P2-09-11 V0.7 is a detailed relational proposal, AI Schema V1.16 is logical, P2-09-13 is an integrated inventory, and P2-09-12 is a test specification. No provider integration test, cross-module transaction proof, final migration set or integrated Schema V2.0 has been approved. Development implementation remains the responsibility of the development team after design gates and authorization are complete.

## BASELINE-01 — 2026-11-27 Master audit

- [Audit report](BASELINE-01_AUDIT_REPORT.md)
- [Capability matrix](BASELINE-01_CAPABILITY_MATRIX.md)
- [Dependency map](BASELINE-01_DEPENDENCY_MAP.md)
- [Gap register](BASELINE-01_GAP_REGISTER.md)
- [Execution proposal](BASELINE-01_EXECUTION_PROPOSAL.md)
- [Post-merge update and PRA-05 readiness](BASELINE-01_POST_MERGE_UPDATE.md)
