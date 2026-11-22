# ADR-003 — EF Core as the 1.0 Core Domain Persistence Authority

- Status: Accepted
- Decision date: 2026-11-22
- Supersedes: the migration-strategy portions of ADR-003 V0.1
- Scope: WorldView 1.0 Core Domain persistence and schema lifecycle

## Context

WorldView has not shipped a prior version and there is no production data to preserve. The current SQL bootstrap and the first EF Core foundation were created during development. They are not a compatibility contract and must not constrain the 1.0 schema.

The Domain-owned persistence port and Infrastructure-owned SQLite adapter remain the intended dependency boundary. The persistence implementation, however, should not retain two competing schema authorities.

## Decision

**EF Core is the sole ORM and schema-migration authority for the complete WorldView 1.0 Core Domain.**

1. `WorldView.Domain` and `WorldView.Application` MUST NOT reference EF Core packages or types.
2. EF Core and `Microsoft.EntityFrameworkCore.Sqlite` are owned by `WorldView.Infrastructure`.
3. `ICoreDomainDatabase` remains the application-facing persistence port. Application services do not receive `DbContext`.
4. One `CoreDomainDbContext` owns the complete relational model for all Core Domain aggregates. Every persisted table, key, relationship, index, check constraint, and delete behavior must be represented in the model or explicitly documented as provider-specific migration SQL.
5. EF Core Migrations MUST create the complete 1.0 schema from an empty SQLite database. A fresh database must not require a legacy SQL bootstrap before EF migrations run.
6. The old checksummed SQL migration ledger (001–010) is development history only. It is not a compatibility baseline, is not run by the 1.0 application, and must not be extended as a second migration system. Its schema definitions may be used as source material while reconciling the final 1.0 model.
7. The no-op EF baseline that merely records pre-existing SQL tables is not part of the 1.0 architecture. Replace it with a real initial EF migration that creates the complete schema from empty.
8. No compatibility migration, dual-write path, legacy database detection, or old-schema upgrade path is required before 1.0. Development databases may be deleted and recreated.
9. After 1.0 ships, every schema change must be represented by a forward EF migration. Destructive changes after release require an explicit product/data migration decision.
10. SQLite-specific SQL is allowed inside EF migrations or narrowly scoped persistence operations for invariants EF cannot express faithfully, including deferred foreign keys, triggers, partial indexes, exact CHECK clauses, and compare-and-swap operations.
11. All multi-row domain writes remain atomic. EF change tracking does not replace domain validation or database constraints.
12. Use short-lived DbContext instances. Do not retain tracked entities across service calls.
13. The existing SQLite connection ownership and serialization policy remains authoritative: runtime EF sessions use the package database's already-open connection without owning or disposing it, and execute inside the same connection-access critical section.
14. IDs remain canonical lowercase hyphenated UUID text; timestamps remain UTC Unix milliseconds; JSON payloads remain explicit text unless a separately accepted decision changes the contract.
15. Malformed persisted data must fail closed. Do not silently repair, normalize, or reinterpret invalid values on read.

## Complete 1.0 model scope

The initial EF model and initial migration must include all tables currently defined by Core Domain schema decisions and all accepted extensions, including at minimum:

- Project and Map identity
- History and Branch
- Calendar and Calendar Configuration
- Object Type, Object identity, Object Definition, ID Allocator, and ID Reservation
- Map Object Registration
- Map Initial State and Initial Object membership
- Asset metadata and Definition-to-Asset typed links
- Field Dataset and immutable Field Revision

Any additional accepted Core Domain table introduced before 1.0 must be included in the same DbContext and migration authority.

## Required relational guarantees

- Project-scoped composite foreign keys prevent cross-project references.
- Map-local Object ownership and registration scope are enforced at the database boundary.
- Initial State membership must reference an Object registered in the same Map.
- Asset publication/integrity invariants are revalidated inside the write transaction.
- Published Assets and Field Revisions remain immutable at the database boundary where specified.
- Field Dataset publication creates/updates Dataset, inserts immutable Revision, and advances the current pointer atomically with expected-current compare-and-swap.
- Revision lineage and unique per-Dataset revision numbering are database-enforced.
- Foreign keys are enabled on every SQLite connection.
- No destructive cascade is introduced for identity/history records unless explicitly authorized by a domain decision.

## Implementation sequence

### Phase 1 — replace the development bootstrap

- Remove the legacy SQL migration source from production startup and project creation/open flows.
- Remove the no-op EF baseline migration.
- Build a real initial EF migration that creates the complete 1.0 schema from an empty database.
- Include provider-specific triggers, partial indexes, deferred constraints, and CHECK clauses in reviewed migration SQL.
- Prove fresh database creation and full schema integrity using real SQLite.

### Phase 2 — complete EF model

- Map every Core Domain table into the single DbContext.
- Use explicit entity configurations and explicit conversions; do not rely on conventions for persisted names, UUID representation, enum values, or delete behavior.
- Keep Domain and Application independent from EF Core.

### Phase 3 — move persistence operations to EF

- Replace hand-written SQL row mapping and CRUD operations aggregate by aggregate.
- Preserve `ICoreDomainDatabase` as the port.
- Use EF transactions for aggregate writes; retain narrowly scoped SQL only where required for precise SQLite semantics.
- Add real-SQLite integration tests for each vertical slice, including transaction rollback, concurrency, and database-level constraints.

### Phase 4 — 1.0 release gate

- A fresh empty database is created entirely by EF migrations.
- No runtime code invokes the legacy SQL migration ledger.
- No production persistence operation depends on legacy SQL bootstrap-created schema.
- Every Core Domain table is represented by the EF model and initial migration.
- Build, all tests, migration tests, and SQLite integrity / foreign-key audits pass on CI.

## Explicit non-goals

- No compatibility with unreleased development database layouts.
- No migration from the old SQL bootstrap ledger.
- No parallel SQL and EF schema authorities.
- No EF Core dependency in Domain or Application.
- No blanket generic repository abstraction around DbContext.
- No weakening of relational constraints to simplify ORM mapping.
