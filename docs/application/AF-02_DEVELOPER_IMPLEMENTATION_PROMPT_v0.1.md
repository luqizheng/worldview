# AF-02 — Application Foundation Implementation Prompt V0.1

- **Date:** 2026-11-21
- **Status:** Developer execution prompt — implementation must begin with codebase audit
- **Architecture authority:** [AF-01 Application Foundation Architecture V0.1](AF-01_APPLICATION_FOUNDATION_ARCHITECTURE_v0.1.md)
- **Handoff reference:** [AF-01 Developer Handoff V0.1](AF-01_DEVELOPER_HANDOFF_v0.1.md)
- **Related:** ME-01-01, ME-01-07, P2-09-11 V0.11, P2-09-12 V0.6, Production Renderer Architecture V0.2, PRA-01 Contract V0.1

---

## 1. Mission

Implement the minimum real World View desktop application foundation defined by AF-01.

The intended end-to-end vertical slice is:

```text
Start desktop application
  -> Create or Open a .wvproject package
  -> Validate package and supported database state
  -> Establish an isolated Project Session
  -> Query Maps through Core Domain read services
  -> Open an isolated Map Workspace Session
  -> Close workspace and Project Session safely
```

This task is an application-lifecycle and data-entry foundation. It is not a map-rendering or map-editing task.

**Do not begin by creating files or writing code. First inspect the actual source-code repository and report what already exists.**

## 2. Repository and workspace preflight — mandatory

The documentation repository is:

`https://github.com/luqizheng/worldview`

It is the formal architecture/documentation repository. Do not assume it is also the application source-code repository.

Before implementation:

1. Identify the actual source-code repository and the local workspace containing the solution.
2. Record repository URL, branch, latest commit and working-tree status.
3. Inspect all uncommitted, untracked and user-owned changes.
4. Preserve unrelated work. Do not reset, clean, checkout over, or overwrite user changes.
5. Do not modify `GLOSSARY.md`, `docs/map-replacement/`, or unrelated documentation/code.
6. Identify solution files, projects, target frameworks, SDK pinning, package lock files and CI/build scripts.
7. Identify the current Avalonia startup path, main window, dependency composition and logging/error boundary.
8. Identify Core Domain projects, domain contracts, read services, write services, persistence adapters, migration ledger and actual migration files.
9. Identify Project creation/query capabilities, Map query capabilities, Branch/Time validation services and Asset Store implementation.
10. Identify existing automated tests and Windows/macOS build/runtime evidence.

Create a concise **Codebase Readiness Audit** before making implementation changes. Classify each AF-01 dependency as:
- Implemented and verified in code;
- Implemented but unverified;
- Designed/documented only;
- Missing;
- Conflicting with AF-01.

If the source repository or workspace is unavailable, stop implementation. Report the exact repository/workspace access required. Do not simulate a source-code checkout or claim implementation.

## 3. Architecture authority and decision rules

Follow AF-01 V0.1 unless actual code inspection reveals a concrete conflict.

The authority boundaries are mandatory:

- Core Domain owns formal Project/Map/Branch/Object/History identity, validation, Commands, Events and World State semantics.
- Core Domain Domain Services are the only formal write path.
- Core Domain read services own query semantics and provide immutable read projections.
- Application coordinates use cases, Project Sessions, Map Workspace Sessions and resource lifetimes.
- Persistence adapters implement approved Core-owned persistence contracts.
- Application Bootstrap coordinates module-owned migrations; it does not invent domain schema.
- UI/ViewModels do not issue SQL and do not become a second domain authority.
- Presentation does not access SQLite or own file/package lifecycle.
- Renderer does not open files, query databases or reconstruct historical state.

When actual code conflicts with AF-01:
1. Do not silently rewrite either architecture or existing code.
2. Record the exact conflict, affected contracts, evidence and options.
3. Continue only with independent, non-conflicting work.
4. Stop before any change that would alter formal domain semantics, schema authority or data-loss guarantees.

### Important persistence gate

P2-09-11 V0.11 is a reconciled physical-schema proposal, not executable DDL or proof of implementation. P2-09-12 V0.6 is a provider/transaction proof specification, not evidence that tests have run.

Therefore:
- Inspect the actual source schema and migrations.
- Do not generate production tables merely by copying proposed inventories.
- Do not create shadow Project/Map tables to make the UI appear functional.
- Do not bypass missing Core Domain services with direct SQL in Application or ViewModels.
- If the approved Core Domain creation/query path is absent, report a blocker and implement only independent shell/package work that does not fabricate domain truth.

## 4. Implementation scope

Implement only the smallest set of capabilities required for the vertical slice.

### 4.1 Application Host

- Real Avalonia 12 desktop startup and clean shutdown, reusing the existing application structure where possible.
- Explicit dependency composition with clear lifetime scopes.
- Process-level exception and logging boundary.
- A simple start surface with Create Project, Open Project and Recent Projects entry points.
- Async open/validation operations must not block the UI thread.
- No generic plugin system, event bus, workflow framework or service locator.

### 4.2 Project Package

Use the AF-01 recommended directory package unless preflight reveals an existing approved format that must be preserved:

```text
<MyWorld>.wvproject/
  manifest.json
  project.sqlite
  Assets/
    sha256/
  Staging/
  Backups/
```

Implement a versioned, bounded manifest. The manifest must not duplicate authoritative Core Domain Project identity or domain state.

Validate:
- expected package suffix and directory shape;
- manifest presence, syntax and supported package format version;
- relative database path confined within the package;
- no absolute path or path traversal;
- database presence and SQLite integrity;
- supported database/schema version using actual bootstrap metadata;
- Project identity through the supported Core Domain boundary;
- Asset Store root and referenced Asset integrity according to explicit/lazy validation policy.

Reject unsupported newer formats for writing. Do not silently repair or rewrite a package during inspection.

If a different package format already exists and is an approved baseline, do not replace it unilaterally. Report the conflict and recommend the least disruptive reconciliation.

### 4.3 Create Project

Create Project must be an application use case.

Where the actual Core Domain service exists:
1. Validate destination and prevent overwrite.
2. Create a temporary sibling package directory.
3. Create package metadata and initialize the approved database/bootstrap path.
4. Run only authorized, implemented module migrations.
5. Invoke the Core Domain Project creation service.
6. Initialize the Asset Store adapter.
7. Verify package/database identity and required invariants.
8. Publish the completed package to its final path using a safe same-volume rename where supported.
9. Establish and expose the Project Session only after success.

On failure, dispose acquired resources and clean only the temporary package created by this operation.

If Core Domain Project creation is not implemented, do not insert a Project row manually or invent a replacement schema. Report the blocker.

### 4.4 Open Project and Project Session

A Project Session represents one successfully opened Project Package.

It owns or coordinates:
- ephemeral Session ID;
- canonical package path and validated manifest;
- Project ID resolved through Core Domain;
- database connection factory / persistence scope;
- Asset Store adapter;
- Core Domain service scope;
- serialized write coordinator where required by the existing persistence contract;
- child Map Workspace Sessions;
- lifecycle and diagnostics.

The Session ID is process-local and is never a replacement for Project ID.

Lifecycle:

```text
Opening -> Validating -> Migrating (if required) -> Ready
    |           |                |
    +-----------+----------------+-> Failed / Cancelled

Ready -> Closing -> Closed
```

A partially opened session must never be exposed as Ready. On failure, dispose acquired resources in reverse order.

V1 must prevent two independent writable Project Sessions for the same package within one process. Multiple different Project Sessions may be open concurrently and must not share database or Asset Store state.

Do not add multi-process locking or collaborative editing.

### 4.5 Map Workspace Session

A Map Workspace Session is one Map/Branch/Historical Time working context inside a Project Session.

It must:
- resolve Map identity and summary through Core Domain read services;
- validate Branch and canonical Historical Time through Core Domain;
- hold independent view/camera and transient editor state;
- own or coordinate its future Presentation Coordinator/Publisher scope;
- cancel its own derived work when context changes or workspace closes.

It must not:
- own a database connection independently of Project Session policy;
- copy the authoritative Map or World State into a mutable application model;
- create a second history reconstruction engine;
- share selection/tool/camera/presentation mutable state with another workspace.

Multiple Map Workspace Sessions in one Project must be isolated. Closing one workspace must not close sibling workspaces or the Project Session.

### 4.6 First Map read path

Implement a real Map list and Map Workspace opening flow through Core Domain read services.

The first UI can be deliberately simple:
- Project name/identity;
- list of Maps;
- selected Map name/identity;
- selected Branch and Historical Time context;
- close action.

A text-based Map summary is sufficient. Do not implement map rendering.

A hard-coded Map, fake repository, direct SQL from UI/Application, or placeholder list is not an acceptable end-to-end proof. Test doubles are allowed only for isolated unit tests, not as evidence of real integration.

If the real Core Domain read service is missing, record the blocker precisely and do not claim this acceptance item passed.

### 4.7 Close and shutdown

- Stop accepting new open requests during shutdown.
- Close Map Workspace Sessions before their Project Session.
- Cancel workspace-owned derived work.
- Complete or safely cancel staged operations.
- Do not abandon active SQLite transactions.
- Dispose resources in reverse dependency order.
- Closing a Session never deletes the package.
- Do not silently convert transient UI state into formal Domain writes.

## 5. Explicit non-goals

Do not implement in this task:

- PRA-01 Presentation Context / Scene Snapshot;
- PRA-02 Avalonia / Skia production viewport;
- map rendering or drawing tools;
- Geometry editing, Spatial Index, Hit Testing or Snap;
- Text & Label layout;
- Animation Runtime or Terrain rendering;
- import/export unless already required by an existing approved package contract;
- cloud sync, collaboration, multi-user or multi-process coordination;
- new Core Domain semantics or unapproved database schema;
- generic repository/ORM/framework abstractions without a demonstrated need.

PRA-01 remains a later stage after the real Application -> Core Domain read path is established.

## 6. Tests and evidence

Write tests against the actual implementation. At minimum cover:

### Package tests
- valid package accepted;
- missing/malformed manifest rejected;
- path traversal and absolute manifest path rejected;
- missing database rejected;
- corrupt SQLite rejected;
- unsupported newer format rejected without writes;
- existing destination never overwritten;
- failed create cleans only its own temporary directory;
- package open does not eagerly load all Assets.

### Session isolation tests
- two Projects use distinct DB and Asset Store scopes;
- duplicate writable open of the same package is prevented;
- two workspaces have independent Map/Branch/Time/view state;
- closing one workspace leaves siblings alive;
- closing Project closes children first.

### Lifecycle tests
- partial-open failure disposes resources in reverse order;
- cancellation during open never exposes Ready;
- shutdown prevents new work;
- close does not abandon active transactions;
- late completion cannot resurrect a closed Session;
- failed migration preserves a recoverable original package.

### Core Domain integration tests
- create/open using the actual approved provider and bootstrap;
- list Maps through the real Core Domain query service;
- open a Map Workspace Session;
- close workspace and Project Session cleanly.

Use deterministic synchronization for concurrency tests. Avoid arbitrary sleeps as proof.

Record actual test commands, test counts, failures, build configurations and runtime environment. A build or publish for macOS is not a macOS runtime test.

## 7. Required implementation sequence

1. Complete and report the Codebase Readiness Audit.
2. Map AF-01 concepts to existing projects/classes/contracts.
3. Identify blockers and any architecture conflicts.
4. Propose the smallest implementation plan and file-change list.
5. Implement Application Host/start surface.
6. Implement or adapt package validation/create/open only against approved persistence boundaries.
7. Implement Project Session lifecycle and isolation.
8. Implement Map Workspace Session lifecycle.
9. Integrate the real Core Domain Map read path if available.
10. Implement safe close/shutdown.
11. Add and run tests.
12. Run Debug and Release builds.
13. Perform Windows runtime validation; perform macOS runtime validation only on actual macOS.
14. Review dependency direction, data authority and working-tree diff.
15. Write implementation report and update documentation index.
16. Commit and push only the authorized implementation changes to the actual source repository; update documentation in the documentation repository separately.

Do not combine unrelated refactors with this task.

## 8. Required deliverables

### Code
- Application Host and start surface;
- package manifest/validator and create/open lifecycle where supported;
- Project Session;
- Map Workspace Session;
- Core Domain Map read integration where supported;
- tests and any required minimal adapters.

### Documentation repository

Add:
`docs/application/AF-02_IMPLEMENTATION_REPORT_v0.1.md`

The report must include:
1. source-code repository URL, branch and commit;
2. Codebase Readiness Audit findings;
3. actual projects/classes and changed paths;
4. package format/manifest version;
5. actual Core Domain services and migrations reused;
6. real read-path evidence;
7. Project/Workspace Session ownership and lifecycle;
8. tests, commands, counts and results;
9. Debug/Release build results;
10. Windows/macOS runtime evidence separately;
11. deviations from AF-01 and reasons;
12. unresolved blockers/risks;
13. commit SHA(s).

Update `docs/README.md` only after the report exists. Do not claim AF-02 complete if the real Core Domain Map read path is blocked.

## 9. Git safety and submission

- Do not assume the documentation repository is the source-code repository.
- Do not create or push application source code into the documentation repository unless it is confirmed to be the actual source repository.
- Inspect branch and working tree before edits.
- Never reset, clean, force-push, or overwrite user changes.
- Commit only task-related files.
- Keep source-code implementation commits in the actual source repository.
- Keep AF-02 implementation report and documentation index in `luqizheng/worldview`.
- Provide exact commit SHAs and clickable GitHub links for each repository.

## 10. Definition of done and final report

Verdict must be one of:
- **PASS** — real package -> Project Session -> Core Domain Map query -> Map Workspace Session path works, required tests pass, and runtime evidence is recorded.
- **PASS WITH CONDITIONS** — application shell/package/session foundation works, but a clearly bounded Core Domain integration gate remains; do not claim full AF-02 completion.
- **BLOCKED** — source code unavailable, architecture conflict requires decision, or a critical persistence/domain prerequisite is missing.

Final response format:

1. Verdict and exact scope achieved.
2. Source repository and branch.
3. Codebase Readiness Audit summary.
4. Implemented modules and paths.
5. Real Core Domain read-path proof or explicit blocker.
6. Test/build/runtime evidence.
7. AF-01 deviations and unresolved risks.
8. Documentation report URL.
9. Source and documentation commit SHAs/links.
10. Recommended next step.

Do not report completion based solely on a launching window, an empty SQLite file, a mocked Map list, or successful compilation.
