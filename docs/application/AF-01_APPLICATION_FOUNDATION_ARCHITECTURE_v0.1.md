# AF-01 — Application Foundation Architecture V0.1

- **Date:** 2026-11-21
- **Status:** Architecture Working Baseline — recommended V1 application structure and lifecycle contract; implementation must reconcile against the actual codebase before changing code
- **Product:** World View local-first desktop application
- **Target stack:** C# / .NET 10 / Avalonia 12
- **Related baselines:** ME-01-01 Editor Overall Architecture V0.1; ME-01-07 Cross-architecture Review V0.1; P2-09-11 Physical Schema V0.11; P2-09-12 Provider / Transaction Proof Spec V0.6; Production Renderer Architecture V0.2; PRA-01 Contract V0.1

---

## 0. Executive decision

World View needs a real application lifecycle and project-data entry point before PRA-01 is implemented.

AF-01 establishes the minimum application architecture around four distinct concepts:

1. **Application Host** — starts the desktop application, composes dependencies, owns application-wide services and shuts them down.
2. **Project Package** — the portable on-disk container for one logical Project, its SQLite database and immutable file-backed Assets.
3. **Project Session** — the in-memory lifetime of one opened Project Package and its shared persistence/Domain Service resources.
4. **Map Workspace Session** — the in-memory working context for one Map, selected Branch, Historical Time and view state inside a Project Session.

The key rule is:

> Opening a Project opens its package and establishes a Project Session. Opening a Map establishes a Map Workspace Session inside that Project Session. A Map is a Core Domain entity, not an independent file or a second persistence unit.

The application shell must not become a second owner of formal Map data. Core Domain and its Domain Services remain the only authority for formal Map/Object/History facts. Application services coordinate lifecycle and use cases; Presentation consumes immutable read projections; Avalonia displays and routes interaction.

AF-01 is deliberately smaller than the complete editor. It does not implement map editing, rendering, spatial algorithms, AI workflows or the full Core Domain schema.

## 1. Goals and non-goals

### 1.1 Goals

- Define startup, shutdown and dependency composition for the desktop application.
- Define one portable Project Package containing one logical Project.
- Define safe create, open, validate, close, backup and recovery boundaries.
- Define Project Session and Map Workspace Session ownership and lifecycle.
- Support multiple open Projects and multiple Map workspaces without cross-session state leakage.
- Keep SQLite, Asset Store and Core Domain access behind application/domain services.
- Provide a real path from opening a Project Package to reading Map summaries through Core Domain read services.
- Establish the data-source boundary required by PRA-01.
- Keep V1 local-first, single-user/small-team and low-concurrency.

### 1.2 Non-goals

- Implementing the full Core Domain physical schema from a proposal.
- Replacing or redesigning Core Domain persistence.
- Defining new Map, Object, Branch, Command or Historical Time semantics.
- Implementing a map canvas or Skia drawing.
- Implementing PRA-01 itself.
- Implementing map import/export, cloud sync, collaboration or multi-user locking.
- Building a plugin, event-bus, generic workflow or distributed application framework.
- Persisting transient UI state as formal world history.

## 2. Canonical ownership and layers

The minimum dependency direction is:

```text
Avalonia Desktop Shell
        |
        v
Application Services / Session Coordinator
        |
        +----------------------+
        |                      |
        v                      v
Core Domain Services      Project Storage Boundary
        |                      |
        v                      v
Core Domain Model         SQLite + Core Asset Store
        |
        v
Immutable Read Projections
        |
        v
Presentation (later: PRA-01)
        |
        v
Avalonia Viewport / Skia (later: PRA-02)
```

The diagram is a responsibility map, not permission for Presentation to query SQLite.

| Layer | Owns | Must not own |
|---|---|---|
| Desktop Shell | Windows, menus, dialogs, tabs, visual navigation, user preferences | Formal Map data, SQL, history reconstruction |
| Application | Use-case orchestration, Project Session, Map Workspace Session, lifecycle, error translation | A second domain model or formal facts |
| Core Domain | Project/Map/Branch/History identity, Commands, Events, Initial State, World State reconstruction, validation and formal writes | Avalonia controls or file dialogs |
| Persistence / Asset adapters | SQLite connection/repository implementation and immutable Asset byte access, behind Core-owned contracts | Domain semantics or independent write authority |
| Presentation | Context-bound derived immutable output and Scene Snapshot | Domain persistence, formal writes, mutable UI state |
| Renderer | Drawing one complete Scene Snapshot | File I/O, SQL, history reconstruction, formal edits |

### 2.1 Core Domain authority

- All formal Map data and historical state are read through Core Domain read services and written through Core Domain Domain Services.
- The UI and Application layer must not query Core Domain tables directly.
- Presentation must not open SQLite or resolve Assets by directly querying tables.
- Application may coordinate a transaction boundary where existing contracts require it, but it does not own the facts written by participating modules.
- The application may hold immutable read projections and session references; these are not alternate authorities.

### 2.2 Persistence baseline reuse

The current physical schema proposal defines one local Project database, one logical Project per database, and large geometry/raster/binary content as immutable files in a Project Asset Store. Application Bootstrap coordinates module-owned migrations through one shared migration ledger.

These are the inputs AF-01 must respect. P2-09-11 V0.11 is explicitly a physical-schema proposal, not executable DDL. P2-09-12 V0.6 is a provider/transaction proof specification, not evidence that provider tests have passed. AF-02 must inspect the actual implemented schema and migration code; it must not turn proposed table inventories into production DDL without the required Core Domain implementation authorization.

## 3. Project Package V1

### 3.1 Recommended package shape

V1 uses a directory-based Project Package so that the SQLite database and file-backed immutable Assets can be moved, backed up and recovered together without embedding large binary payloads in SQLite.

Recommended package suffix: `.wvproject`.

Recommended internal layout:

```text
<MyWorld>.wvproject/
  manifest.json
  project.sqlite
  Assets/
    sha256/
      <content-hash>
  Staging/
  Backups/
```

- `manifest.json` identifies the package format and the relative database path. It is package metadata, not a second Project identity authority.
- `data/project.sqlite` is the single Project database. The database's Core Domain Project identity and format/version metadata remain authoritative for domain identity and database compatibility.
- `Assets/sha256/` stores immutable content-addressed Asset bytes. The Core Domain owns Asset identity, typed ownership and formal references; the storage adapter owns byte placement and retrieval.
- `Staging/` is temporary, non-authoritative import/publication workspace. It must be safe to clean after verifying no active operation depends on it.
- `Backups/` contains explicit backup artifacts, not a second live database.

The package directory is the unit of open, close, move, backup and recovery. Do not support moving only `project.sqlite` while leaving Assets behind.

The exact manifest JSON schema, package suffix registration and Asset hash/canonicalization details are implementation gates. Do not invent or duplicate domain metadata in the manifest.

### 3.2 Why a Project Package, not one Map file

- One Project database may contain multiple Maps.
- Map identity, Branch/History, Object references, shared definitions and Project-scoped assets have relationships that cannot be represented safely by treating every Map as an independent file.
- A Map is opened from the selected Project through Core Domain services; it is not copied into a separate Map file.
- Opening a Project does not automatically choose a Map unless a last-opened Map preference is available and valid.

### 3.3 Package validation

Opening a package must validate, in bounded stages:

1. Selected path exists and is a directory with the expected package suffix.
2. `manifest.json` exists, parses, has a supported package-format version and points only to an allowed relative path within the package.
3. `project.sqlite` exists and is a valid SQLite database.
4. Database identity and format metadata are readable through the supported bootstrap/metadata boundary.
5. The database contains exactly one logical Project as required by the current persistence model.
6. Required module migrations are known and internally consistent.
7. Asset root exists or can be safely initialized only for a newly created package; existing referenced Assets are checked lazily or by an explicit integrity scan, not all eagerly loaded into memory.
8. No unsupported newer package/database format is silently opened for writing.

Reject path traversal, absolute paths in manifest-controlled locations, symlink escapes where the platform allows detection, malformed manifests, database corruption and incompatible schema versions. Do not repair or rewrite a package during initial inspection.

For an older supported package version, migration is a separate explicit lifecycle stage: validate, create a recoverable backup, run ordered module migrations in a bounded transaction strategy, verify checksums/invariants, then mark the open successful. If safe backup or migration verification fails, leave the original package recoverable and do not report success.

### 3.4 Create Project

Create Project is an application use case, not a UI-side file-writing routine.

Recommended sequence:

1. User selects a destination and Project display name.
2. Application validates the destination and confirms no conflicting package exists.
3. Create a temporary sibling package directory.
4. Write the initial manifest and create the database using the approved Application Bootstrap.
5. Run only migrations that actually exist and are authorized for implementation.
6. Invoke Core Domain Project creation service so the Project identity is created through the formal domain boundary.
7. Initialize the Asset Store directory structure.
8. Verify package/database identity and required invariants.
9. Atomically rename the temporary package directory to the final `.wvproject` path where the filesystem supports same-volume atomic directory rename.
10. Establish the Project Session and expose it to the UI only after the operation succeeds.

On failure, close all handles and remove only the incomplete temporary package created by this operation. Never delete or overwrite a pre-existing user package.

If the Core Domain Project creation/persistence service is not yet implemented, AF-02 must stop at the bootstrap boundary and report the blocker; it must not create a shadow Project row or invent a replacement schema.

## 4. Application Host and lifecycle

### 4.1 Startup

The application startup sequence:

1. Initialize logging and process-level exception handling.
2. Build the dependency container using explicit registrations; avoid service-locator access from domain/presentation code.
3. Initialize Avalonia and the desktop shell.
4. Initialize Application Bootstrap metadata and migration discovery without opening arbitrary Project files.
5. Restore only safe UI preferences and recent package paths; do not automatically run migrations on a recent item without user-visible open intent.
6. Show the start surface: Open Project, Create Project, and Recent Projects.
7. Open a Project only after an explicit user action or a clearly confirmed restore preference.

Startup must remain usable when the last-opened Project is missing, moved or corrupt.

### 4.2 Shutdown

Shutdown must:

1. Stop accepting new Project/Map open requests.
2. Ask active Map Workspace Sessions to end transient interactions and cancel their derived work.
3. Ask Project Sessions to flush only explicitly supported application metadata and close owned readers/writers.
4. Wait for bounded in-flight persistence operations to finish or fail safely; never abandon a half-finished SQLite transaction.
5. Dispose Asset Store and SQLite resources after dependent operations stop.
6. Dispose application-wide services and exit.

Do not automatically convert transient UI changes into formal Domain writes during shutdown.

### 4.3 Multiple Projects

V1 may keep multiple Project Sessions open in one process. Each Project Session has its own database connection factory, Asset Store root, Core Domain service scope, migration state and diagnostics scope.

No static global current database, current Project ID, Asset root or Domain Service instance may be shared across Project Sessions.

A single Project database has one serialized write coordinator. Independent read operations use appropriately owned read connections; a connection/command/reader is not concurrently shared between threads. Do not introduce multi-process write coordination in V1.

## 5. Project Session

A Project Session represents one successfully opened Project Package.

Suggested responsibilities:

- stable in-process Session ID;
- canonical package path;
- validated package manifest/version;
- Project identity obtained from Core Domain;
- database connection factory and owned persistence scope;
- Project Asset Store adapter;
- Core Domain read/write service scope;
- one application write coordinator where required by the persistence contract;
- open Map Workspace Sessions;
- lifecycle state and diagnostics;
- cancellation/disposal boundary.

A Project Session is not a persisted Core Domain entity. Its Session ID is ephemeral and must not be written as a substitute for Project ID.

Suggested lifecycle:

```text
Opening -> Validating -> Migrating (if required) -> Ready
    |           |                |
    +-----------+----------------+-> Failed / Cancelled

Ready -> Closing -> Closed
```

A failed or cancelled open must not be exposed as a Ready Project Session. A Project Session cannot close while it still owns active Map Workspace Sessions or in-flight persistence work.

## 6. Map Workspace Session

A Map Workspace Session is a selected Map work context within a Project Session.

It contains or coordinates:

- stable in-process Workspace Session ID;
- Project Session reference;
- Map ID resolved by Core Domain;
- selected Branch ID;
- canonical Historical Time;
- read-only World State / Map summary projections obtained from Core Domain;
- view/camera and presentation configuration references;
- transient editor state (selection, active tool, preview) owned by the editor/application;
- eventual Presentation Coordinator and Publisher scope (PRA-01), isolated per viewport/workspace.

It does not contain a copied authoritative Map model or a second history engine.

Rules:

1. A Map Workspace Session is created only after Core Domain confirms the Map exists and is accessible in the Project.
2. Branch and Historical Time are resolved/validated through Core Domain services.
3. Changing Map, Branch or Historical Time is an explicit context transition; dependent presentation work is invalidated and later rebuilt.
4. Two workspaces may view the same Map at different Branch/Time contexts without sharing mutable selection, viewport or presentation state.
5. Multiple Maps in one Project share the Project Session's storage/domain resource scope but keep independent workspace state.
6. Closing one Map Workspace Session must not close the Project Session while other workspaces remain open.
7. Closing the Project Session closes its child workspaces first.

Terminology: use **Project Session** for the opened Project container and **Map Workspace Session** for one Map/Branch/Time working context. Avoid using the ambiguous phrase “Map Session” for the database owner.

## 7. Read path and write path

### 7.1 Read path

```text
Open Project Package
  -> Package Validator / Application Bootstrap
  -> Project Session
  -> Core Domain Map Query Service
  -> Immutable Map Summary / Read Projection
  -> Map Workspace Session
  -> (later) Presentation Context -> PRA-01 Scene Snapshot
```

The application must not read Map rows directly from SQLite to populate the UI. The Core Domain read service owns query semantics and returns immutable DTO/read projections.

The first AF-02 vertical slice should be intentionally small:

- create or open a valid Project Package;
- display Project identity/name;
- list Maps using a Core Domain query service;
- open one Map Workspace Session;
- display its Map name and selected Branch/Time context;
- close the workspace and Project cleanly.

Do not render the map yet. A text-based Map summary is sufficient to prove the application/data lifecycle before PRA-01 and PRA-02.

If the repository has no implemented Core Domain read service or no authorized executable persistence schema, the implementation must report that as an explicit dependency gate. It may build the shell and package validator with test doubles, but must not claim the real Map read path is complete.

### 7.2 Write path

Formal writes always go through Core Domain Domain Services. Application use cases may coordinate service calls and the transaction boundary where the existing cross-module contract requires it.

- No UI code issues SQL.
- No Project Session method directly mutates Core Domain tables.
- No generic repository exposed to ViewModels.
- No generic CRUD endpoint that bypasses registered typed Commands, Initial State services or Shared Definition authoring semantics.
- A file import first stages and validates immutable bytes; formal Asset ownership/publication is performed through the owning Core Domain service.
- Save means the specific formal Domain operation has committed. It does not mean serializing an in-memory Map object over the database.

## 8. File and resource lifecycle

### 8.1 Open

- Normalize and canonicalize the selected package path.
- Prevent opening the same package twice as two independent writable Project Sessions in one process; focus the existing session or open another read-only view if later supported.
- Open resources in a defined order: package validation, database metadata, migration/integrity, Asset Store, Domain Service scope, then Ready.
- On any failure, dispose partially opened resources in reverse order.
- Do not eagerly load all geometry/raster Assets into memory.

### 8.2 Close

- Close child Map Workspace Sessions first.
- Cancel presentation/calculation work owned by those workspaces.
- Complete or safely cancel staged operations.
- Dispose read resources and writer coordinator.
- Release Asset Store handles.
- Dispose database connections and session scope.
- Keep the Project Package on disk; closing a session never deletes the package.

### 8.3 Save and autosave

V1 uses SQLite transactions and immutable Asset publication for formal data. Do not serialize the entire Project or Map from a mutable ViewModel on every save.

- Formal domain operations commit through Domain Services.
- Asset bytes are written to a staging location, validated and hashed before publication; the formal typed reference/current pointer is committed only through the owning Domain Service.
- Unreferenced staged Assets may be cleaned by a bounded recovery/GC process only after reference/in-flight checks.
- UI preferences and transient workspace state are separate from formal world data. Their persistence format and autosave policy may be implemented later; they must not be mistaken for Map history.
- No generic “dirty Map object graph” save mechanism is introduced.

## 9. Error handling and recovery

User-visible failure categories:

- invalid or unsupported Project Package;
- unsupported package/database format version;
- corrupt SQLite database;
- missing or corrupt referenced Asset;
- migration/checksum/integrity failure;
- Project identity mismatch;
- path/permission/storage failure;
- active operation prevents safe close;
- Core Domain read/write validation failure.

Rules:

- Fail closed for incompatible schema or failed integrity checks; never silently skip migrations or repair authoritative data.
- Preserve the original package on failed open/migration.
- Provide a diagnostic ID and actionable message; detailed exception/SQL data goes to logs, not raw UI dialogs.
- Missing referenced immutable Assets are integrity errors. Do not substitute zero geometry, empty content or a different revision.
- Recovery and repair are explicit operations with backup and verification; no automatic destructive repair at startup.

## 10. Session-to-PRA integration

AF-01 establishes the ownership boundary PRA-01 will consume.

For each Map Workspace / viewport:

1. The Application layer asks Core Domain for an immutable read projection for the selected Map, Branch and canonical Historical Time.
2. It obtains formal revision/fingerprint and other versioned presentation inputs through supported services.
3. It constructs a Presentation Context from those authoritative values plus view/style/font/viewport inputs.
4. PRA-01 prepares context-stamped subsystem outputs and publishes one complete immutable Scene Snapshot.
5. The Renderer reads one snapshot; it never opens the Project Package or database.

A Project Session may own shared service infrastructure, but each Map Workspace / viewport has its own Presentation Request Coordinator and Snapshot Publisher. One Map or Project must not supersede another workspace's presentation requests.

PRA-01 must not be implemented by adding database or file ownership to the Presentation layer.

## 11. Technology and implementation constraints

- Target C# / .NET 10 / Avalonia 12, preserving the approved desktop direction.
- Use the provider and package versions that are actually approved/implemented by the repository; P2-09-12 currently specifies Microsoft.Data.Sqlite 11.x as the proof target, but AF-02 must verify the actual dependency lock and Windows/macOS runtime before changing versions.
- Prefer explicit SQL repositories behind Core Domain-owned persistence contracts; no ORM or generic persistence framework unless the existing codebase proves a concrete need.
- One shared migration ledger with module namespaces; Application Bootstrap orders module-owned migrations but does not own domain migration content.
- Do not assume proposed P2-09-11 tables are implemented.
- Keep filesystem access behind Project Package / Asset Store adapters.
- Use asynchronous file and UI APIs where appropriate; do not block the Avalonia UI thread on large file reads, migration or Asset hashing.
- Use bounded background work and cancellation; opening/closing sessions must be deterministic.
- Do not introduce distributed services, message brokers, generic event buses or multi-process locks.

## 12. AF-02 implementation gate and acceptance

AF-02 is the implementation work package that follows AF-01. It must begin with repository/codebase inspection and map the architecture to actual code before adding files.

Minimum acceptance:

- [ ] Desktop app starts and shuts down cleanly.
- [ ] Start surface offers Create/Open/Recent Project entry points.
- [ ] Project Package layout and manifest are validated.
- [ ] A new Project Package can be created without overwriting an existing path.
- [ ] A valid package can be opened and a Project Session established.
- [ ] Unsupported/corrupt packages fail safely with diagnostics.
- [ ] Multiple Project Sessions do not share database or Asset Store state.
- [ ] Multiple Map Workspace Sessions in one Project are independent.
- [ ] Map summaries are read through Core Domain services, not SQL from UI/Application.
- [ ] Project/Map workspace close disposes owned resources safely.
- [ ] SQLite write serialization and read ownership follow the actual persistence contract.
- [ ] No unapproved domain schema or shadow domain authority is introduced.
- [ ] Windows build/runtime validation is recorded; macOS claims require actual macOS execution.
- [ ] Tests cover package validation, session isolation, lifecycle failure cleanup and close behavior.
- [ ] Implementation report identifies blockers where the existing Core Domain schema/services are not yet executable.

AF-02 is not complete merely because a window launches or a SQLite file can be created. The real Project -> Core Domain Map query -> Map Workspace Session path must work against the authorized persistence implementation.

## 13. Decisions and open gates

### Recommended V1 decisions

1. One Project Package directory contains one SQLite Project database and one file-backed Asset Store.
2. One database contains exactly one logical Project and may contain multiple Maps.
3. A Project Session owns the opened package/resource lifetime.
4. A Map Workspace Session owns Map/Branch/Time/view context, not persistence.
5. Multiple Project Sessions and Map Workspace Sessions are isolated within one process.
6. Core Domain read/write services are the only formal map-data access path.
7. Application Bootstrap coordinates migrations; it does not author Core Domain or AI migrations.
8. Map is not a standalone file in V1.

### Explicitly open implementation gates

- Exact manifest schema and package suffix registration.
- Exact database migration state and executable Core Domain read services.
- Exact provider/package lock and Windows/macOS native runtime proof.
- Backup mechanism and recovery verification for each supported platform.
- UI preference/workspace-state persistence format.
- Recent-project metadata storage and missing-path behavior.
- Whether future users need read-only concurrent open of the same package from another process (not V1).

These gates must be resolved by codebase inspection and focused implementation tests, not by inventing new domain semantics.

## 14. Status

AF-01 V0.1 is an Application Foundation architecture working baseline. It defines the recommended Project Package, Application Host, Project Session and Map Workspace Session boundaries and the minimum real-file-to-Core-Domain read path.

It does not claim that an application framework, package format, SQLite schema, Core Domain services or PRA-01 have been implemented. The next work package is AF-02 Application Foundation Implementation.
