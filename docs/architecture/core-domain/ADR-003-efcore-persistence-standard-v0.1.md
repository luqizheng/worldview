# ADR-003 — EF Core as the Core Domain Persistence Standard

- Status: Accepted
- Decision date: 2026-11-22
- Scope: WorldView Core Domain persistence across all aggregates
- Initial implementation: `feat/efcore-core-domain-foundation`

## Context

The current Core Domain persistence adapter is a large hand-written SQLite implementation. It provides precise control over transactions, composite foreign keys, CHECK constraints, partial indexes, and triggers, but duplicates row mapping, parameter binding, transaction plumbing, and migration bookkeeping as the domain grows.

WorldView has already committed to a Domain-owned persistence port (`ICoreDomainDatabase`) and an Infrastructure-owned SQLite adapter. That boundary remains valuable and must not be replaced by exposing EF Core to Domain or Application.

## Decision

Adopt **Entity Framework Core as the standard ORM and schema-migration system for all Core Domain aggregates**.

1. `WorldView.Domain` and `WorldView.Application` MUST NOT reference EF Core packages or EF Core types.
2. EF Core is owned by `WorldView.Infrastructure`; the provider is `Microsoft.EntityFrameworkCore.Sqlite`.
3. `ICoreDomainDatabase` remains the application-facing persistence port. Services do not receive `DbContext`.
4. A single `CoreDomainDbContext` model owns relational mapping for Core Domain tables. Entity configurations are explicit and centralized; conventions must not silently change persisted names, key formats, nullability, or delete behavior.
5. EF Core Migrations become the schema evolution authority after the legacy-schema baseline has been established. Existing SQL migrations are retained as the compatibility/bootstrap baseline during the phased transition; they are not to be rewritten or checksum-mutated in place.
6. SQLite-specific SQL remains permitted inside EF migrations or narrowly scoped persistence operations for invariants that EF cannot express faithfully: deferred constraints, triggers, partial indexes, exact CHECK clauses, and carefully reviewed compare-and-swap statements.
7. All multi-row domain writes remain atomic. EF change tracking is not a substitute for domain validation or database constraints.
8. Use short-lived DbContext instances. Do not retain tracked entities across service calls.
9. The existing SQLite connection ownership and serialization policy remains authoritative during the transition. EF must use the already-open connection without owning or disposing it; EF operations execute inside the same connection-access critical section. Do not introduce a second connection to the same package database for ORM work.
10. IDs remain canonical lowercase hyphenated UUID text; timestamps remain UTC Unix milliseconds; JSON payloads remain explicit text unless a later decision changes the contract.

## Required persistence guarantees

- Project-scoped composite foreign keys must continue preventing cross-project references.
- Published Assets and Field Revisions remain immutable at the database boundary where specified.
- Field Dataset publication must create the Dataset when needed, insert the immutable Revision, and advance the current pointer in one transaction with an expected-current compare-and-swap.
- Revision lineage and unique per-Dataset revision numbering remain database-enforced.
- Asset publication/integrity checks are re-read inside the write transaction.
- Existing fail-closed data mapping semantics are preserved: malformed persisted data must not be silently repaired or normalized on read.
- Foreign keys remain enabled on every SQLite connection.
- No destructive cascade is introduced for identity/history records unless a domain decision explicitly authorizes it.

## Migration strategy

This is a **strangler migration**, not a big-bang rewrite.

### Phase 0 — foundation (this change)

- Add EF Core SQLite provider and design-time tooling dependencies to Infrastructure only.
- Add the CoreDomainDbContext and explicit mappings for Project, Map, Asset, Field Dataset, and Field Revision.
- Add a no-op EF baseline migration and model snapshot for the legacy Project/Map/Asset tables. The baseline must not recreate or alter them. Add the first non-empty EF migration for Field Dataset/Revision, including deferred current-pointer FK and immutable-revision triggers.
- Add a connection-sharing session that creates/disposes a short-lived DbContext inside the existing SQLite connection coordinator lock. Apply EF migrations after legacy bootstrap when the Core Domain adapter is first constructed.
- Prove the EF model against a real SQLite database created by the existing bootstrap path.

### Phase 1 — complete model coverage

Field Dataset/Revision is the first new EF-managed aggregate and is implemented alongside the foundation. Map every remaining existing Core Domain table into the one DbContext, including History, Branch, Calendar, Object identity/type/allocator/reservation, Map registration, Initial State, and Definition typed links. Keep table/column names and storage contracts byte-for-byte compatible.

### Phase 2 — vertical-slice adapter migration

Move read/write operations from hand-written command/reader code to EF Core in bounded aggregate slices. Preserve `ICoreDomainDatabase` and existing service contracts. For complex CAS and multi-row writes, use EF transactions plus narrowly scoped SQL where that produces clearer and safer semantics. Every slice must have real-SQLite parity tests before switching production code.

### Phase 3 — migration authority cutover

After the complete model and migration baseline cover the full schema, move new schema evolution to EF Core Migrations. Keep historical SQL scripts immutable for existing package bootstrap compatibility. Define an explicit upgrade path for databases with each legacy migration-ledger version. Do not run EF `EnsureCreated` against package databases.

### Phase 4 — retire legacy adapter plumbing

Remove hand-written persistence paths only after all callers are migrated, parity tests pass, migration/upgrade tests pass, and no direct SQL consumers remain. Retain raw SQL only for database-specific invariants and optimized operations.

## Explicit non-goals

- No Domain/Application dependency on EF Core.
- No database-provider switch in this ADR; SQLite remains the active provider.
- No weakening of database constraints to make the ORM model easier.
- No blanket generic repository abstraction around DbContext.
- No automatic schema creation via `EnsureCreated`.
- No simultaneous rewrite of all Core Domain services.

## Acceptance criteria for Phase 0

- Infrastructure builds with EF Core SQLite.
- EF Core context can use the existing opened SQLite connection without owning it.
- Existing bootstrap creates a fresh database; EF baseline migration applies without changing existing table definitions.
- EF can read the seeded Project/Map/Asset rows with canonical UUIDs and enum/string state mapping.
- Existing Core Domain test suite remains unchanged and passes.
- Tests prove the connection remains open after DbContext disposal and that EF and legacy SQL share the same serialized connection.
