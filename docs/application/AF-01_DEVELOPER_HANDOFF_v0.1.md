# AF-01 — Developer Handoff V0.1

- **Date:** 2026-11-21
- **Status:** Ready for AF-02 implementation planning
- **Architecture:** [AF-01 Application Foundation Architecture V0.1](AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md)
- **Related:** ME-01-01, ME-01-07, P2-09-11 V0.11, P2-09-12 V0.6, Production Renderer Architecture V0.2, PRA-01 Contract V0.1

---

## 1. Objective

Implement the smallest real desktop application foundation that can create/open a World View Project Package, establish an isolated Project Session, list Maps through Core Domain read services, open an isolated Map Workspace Session, and close resources safely.

This is AF-02 implementation work. It is not a renderer or map-editing task.

## 2. Mandatory first action: inspect the real codebase

Before changing code:

1. Inspect repository structure, solution/projects, target frameworks, package versions and dependency lock files.
2. Inspect current Git branch, working-tree status and uncommitted changes.
3. Identify existing Avalonia application startup/window implementation.
4. Identify actual Core Domain projects, persistence adapters, migration files, read services and Project/Map creation services.
5. Identify actual Asset Store implementation and immutable Asset contracts.
6. Compare actual code with P2-09-11 V0.11 and P2-09-12 V0.6. Record what is implemented, proposed only, missing or inconsistent.
7. Preserve all unrelated user changes. Do not edit GLOSSARY.md, docs/map-replacement/, or unrelated files.
8. Do not assume the documentation repository contains the application source code. Confirm the actual implementation repository/solution before editing.

If the source code is not available in the current workspace, do not fabricate an implementation or claim code changes. Produce a codebase-access report and the exact missing access requirement.

## 3. Required vertical slice

Implement in this order:

### A. Application Host

- Avalonia 12 desktop startup and clean shutdown.
- Explicit dependency composition.
- Process-level exception/logging boundary.
- Start surface with Create Project, Open Project and Recent Projects entry points.
- No domain SQL in ViewModels or code-behind.

### B. Project Package

Implement the AF-01 V1 directory package shape:

```text
<MyWorld>.wvproject/
  manifest.json
  project.sqlite
  Assets/
    sha256/
  Staging/
  Backups/
```

- Manifest schema must be explicitly versioned and bounded.
- Manifest paths must be relative and confined within the package.
- Database remains one logical Project per package.
- Asset bytes remain immutable and content-addressed.
- Do not embed large geometry/raster payloads into relational rows.
- Do not create speculative domain tables from the P2-09-11 proposal.

### C. Package validation and bootstrap

- Validate package suffix/layout, manifest, SQLite integrity/identity and supported versions.
- Use the actual implemented migration ledger and module-owned migrations.
- Do not silently skip unknown migrations, checksum mismatches or incompatible newer versions.
- For supported upgrades, create and verify a recoverable backup before applying migrations.
- Do not claim schema migration complete if the required Core Domain migration code is absent.
- Keep Application Bootstrap as migration orchestrator, not domain schema owner.

### D. Project Session

- One Project Session per opened Project Package.
- Own its package path, database factory, Asset Store adapter, Domain Service scope, write coordinator and child workspaces.
- No static global current database/Project/Asset root.
- Prevent two independent writable Project Sessions for the same package in one process.
- Dispose partially opened resources in reverse order on failure.

### E. Map Workspace Session

- One workspace per Map/Branch/Time context.
- Obtain Map identity and summaries through Core Domain query services.
- Validate Branch and canonical Historical Time through Core Domain.
- Keep viewport, selection, active tool and transient state independent between workspaces.
- Multiple Map workspaces in one Project share Project Session infrastructure but not mutable workspace state.
- Do not duplicate authoritative Map or World State models in the application layer.

### F. Close and recovery

- Close workspaces before their Project Session.
- Cancel child work and stop accepting new requests.
- Do not abandon active SQLite transactions.
- Release Asset Store and database resources only after dependent work stops.
- Failed open/migration must leave the original package recoverable.
- Closing a session never deletes its Project Package.

## 4. Real read-path requirement

The minimum end-to-end proof is:

```text
Open .wvproject package
  -> validate manifest/database
  -> establish Project Session
  -> Core Domain Map Query Service
  -> show Map summaries
  -> open Map Workspace Session
  -> close workspace and Project Session
```

A placeholder map list, direct SQL query from the UI, or hard-coded Map does not satisfy this requirement.

If Core Domain read services or authorized executable schema are missing, implement only the independent shell/package boundary and report the precise dependency blocker. Do not invent a shadow schema or pretend the read path works.

## 5. Constraints

- Target .NET 10 / Avalonia 12.
- Verify the actual repository target and pinned packages before changing them.
- Microsoft.Data.Sqlite 11.x is the current P2-09-12 proof target, not permission to upgrade dependencies blindly.
- Prefer explicit SQL behind Core Domain-owned persistence contracts; no ORM/generic repository framework without concrete evidence.
- One Project database per package; one serialized writer coordinator per database.
- No multi-process lock service, cloud sync, collaboration, message broker or general event bus.
- No renderer, Skia viewport, PRA-01, spatial index, label layout, animation or terrain rendering in AF-02.
- Do not implement unapproved physical schema proposals as if they were final DDL.
- Preserve existing tests and unrelated modifications.

## 6. Required tests

At minimum:

**Package**
- valid package accepted;
- missing/invalid manifest rejected;
- manifest path traversal rejected;
- missing database rejected;
- corrupt SQLite rejected;
- unsupported newer package/database version rejected without write;
- supported migration backup and failure recovery verified;
- package creation never overwrites existing destination;
- temporary package cleanup on creation failure.

**Session isolation**
- two different Project Sessions have distinct database and Asset Store scopes;
- same package cannot be opened twice as independent writable sessions;
- two Map Workspace Sessions in one Project have independent Map/Branch/Time/view state;
- closing one workspace does not close another;
- closing a Project closes all child workspaces first.

**Lifecycle**
- partial open failure disposes acquired resources in reverse order;
- cancellation during open does not expose a Ready session;
- close stops new work and safely drains/cancels owned work;
- failed migration leaves the original package recoverable;
- missing referenced Asset is reported as integrity failure, not replaced silently.

**Real integration**
- create/open a package using the actual provider/runtime;
- query Maps through Core Domain read service;
- open and close a Map Workspace Session;
- record Windows runtime result;
- macOS runtime must be run on macOS before any pass claim.

Use deterministic test control for races and cleanup. Do not rely on arbitrary sleeps.

## 7. Deliverables

1. Application startup/shell and dependency composition.
2. Versioned Project Package manifest contract and implementation.
3. Package validator and bootstrap integration.
4. Project Session and Map Workspace Session lifecycle.
5. Core Domain Map read-path integration where the actual implementation supports it.
6. Automated tests and provider/runtime evidence.
7. `docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md` with exact code paths, commands, test/build results, implementation gaps and commit SHA.
8. Update documentation indexes only after the actual implementation report exists.

## 8. Definition of done

AF-02 is complete only when the real package -> Project Session -> Core Domain Map query -> Map Workspace Session path works, session isolation and cleanup tests pass, Windows runtime evidence exists, and no unapproved domain schema or second authority was introduced.

A window that launches, an empty SQLite file, or a hard-coded map list is not completion.

## 9. Final report

Report:

1. verdict: PASS / PASS WITH CONDITIONS / BLOCKED;
2. actual repository and branch;
3. implementation modules and paths;
4. package format and manifest version;
5. Core Domain services/schema reused;
6. exact real read-path evidence;
7. test count and results;
8. Debug/Release build results;
9. Windows/macOS runtime evidence (do not infer one from the other);
10. unresolved blockers/deviations;
11. preserved unrelated working-tree changes;
12. commit SHA and repository links.
