# World View AI Assistant — Design Package V1.37

- Date: 2026-11-21
- Status: Design package index
- Scope: World View Core Domain persistence and AI Assistant Task Planning & Execution Engine V1.0 integration
- Deployment target: Single-user, local-first Windows/macOS desktop application

## 1. Current baseline

- AI Assistant logical database V1.16 is the integrated schema baseline; it is not executable SQL. V1.15 remains its predecessor.
- AI Assistant SQLite V1.14 remains a historical prototype, not the final integrated database.
- C# + Avalonia is the target desktop direction; Rust + Bevy is retired as the target runtime.
- Shared Object definition is one current project-level value shared by all referencing Maps and used for historical reconstruction. Post-zero Event-driven runtime changes remain local to the active Map/Branch History.
- Core Domain alone owns formal map/history facts. AI Assistant owns task execution, staging and immutable proposal/authorization/submission evidence.
- One project-local SQLite file plus immutable Asset Store files and file-backed Checkpoints is the recommended persistence unit.
- No implementation authorization has been granted.

## 2. P2-09 detailed design

| Document | Status | Purpose |
|---|---|---|
| `world-view-core-domain-p2-09-13-integrated-schema-inventory-migration-graph-v0.1.md` | Integrated logical inventory | 20 Core baseline tables + up to 5 conditional links, 28 AI tables, one shared ledger and migration dependency graph |
| `world-view-ai-assistant-unified-logical-database-schema-v1.16.md` | Integrated AI logical schema | 28 AI-owned tables, Core identity references, authorization revocation and normalized published Asset references |
| `world-view-core-domain-p2-09-11-physical-schema-design-v0.7.md` | Detailed relational proposal | Core tables, keys, constraints, indexes, transaction, Calendar and migration boundaries |
| `world-view-core-domain-p2-09-ai-schema-integration-review-v0.8.md` | Detailed integration review | Core reference lifecycle/FK policy, authorization revocation, asset publication and AI table normalization |
| `world-view-core-domain-p2-09-12-provider-transaction-proof-test-spec-v0.2.md` | Development verification specification | Required Windows/macOS provider, shared transaction, crash recovery, migration and backup/restore proof |
| `world-view-core-domain-p2-09-consistency-review-v0.1.md` | Cross-contract review | P2-09-01~10 consistency findings |
| `world-view-core-domain-p2-09-09-runtime-compatibility-assessment-v0.1.md` | Compatibility recommendation | Avalonia 12 + .NET 10 + Microsoft.Data.Sqlite 11.x; packaged runtime verification pending |
| `world-view-core-domain-p2-09-10-migration-backup-recovery-contract-v0.1.md` | Operational design baseline | Migration ledger, bootstrap, backup/restore, crash recovery and WAL safety |

## 3. P2-09 logical contracts

- `world-view-core-domain-p2-09-01-persistence-ownership-v0.1.md`
- `world-view-core-domain-p2-09-02-logical-persistence-model-v0.1.md`
- `world-view-core-domain-p2-09-03-command-event-state-change-persistence-contract-v0.2.md`
- `world-view-core-domain-p2-09-04-object-id-reservation-persistence-contract-v0.2.md`
- `world-view-core-domain-p2-09-05-historical-time-ordering-persistence-contract-v0.3.md`
- `world-view-core-domain-p2-09-06-asset-checkpoint-persistence-contract-v0.1.md`
- `world-view-core-domain-p2-09-07-shared-definition-initial-state-contract-v0.2.md`
- `world-view-core-domain-p2-09-08-command-descriptor-and-deterministic-replay-contract-v0.1.md`
- `world-view-core-domain-p2-09-09-dotnet-sqlite-provider-and-transaction-design-v0.1.md`

## 4. Current semantic and persistence decisions

- Map roots and Branch-owned histories use one unified History entity model.
- Branch inheritance is zero-copy and includes the closed fork boundary. Branch-owned Command precision intervals must begin strictly after `parent_at`.
- Command is authoritative authoring history; Event is optional narrative history; State Change is generated deterministically in memory from Commands in V1; World State is reconstructed.
- Shared Object definition edits are project-level and retroactive for historical reconstruction. No replayable Shared Object definition history is introduced in V1.
- Post-zero Event effects belong only to the active Map/Branch History path.
- Object ID reservations use `Reserved`, `Bound`, `Inserted`, `Abandoned`; committed IDs are never recycled and Task Undo does not reverse reservation consumption.
- Each Map has one default Calendar and optional auxiliary Calendars. Calendar Configurations are time-effective display rules; calendar reform does not rebuild World State.
- AI Assistant never writes formal Core Domain tables directly.

## 5. Remaining gates

1. Complete the concrete V1 Command descriptor catalog and typed payload schemas.
2. Validate half-open Historical Time precision intervals and Calendar Configuration interval recalculation against complete Calendar descriptors and D01 examples.
3. Verify exact Object Type display grammar; the recommended namespace is Project + Object Type with a stable internal UUID storage key.
4. Execute P2-09-12 shared-transaction proof tests on packaged Windows/macOS runtimes.
5. Verify WAL durability, backup/restore and Asset Store recovery.
6. Generate executable integrated DDL/migrations only after all gates close.

## 6. Implementation readiness

**Not ready for full implementation.** The Core physical schema and AI logical schema are detailed design baselines, not executable SQL. P2-09-12 is a test specification, not test evidence. No cross-module transaction proof, final migration set or integrated Schema V2.0 has been approved. Development implementation remains the responsibility of the development team after design gates and authorization are complete.