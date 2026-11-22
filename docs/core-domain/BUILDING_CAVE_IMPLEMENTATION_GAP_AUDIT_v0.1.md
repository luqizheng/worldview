# Building/Cave Core Domain Implementation Gap Audit V0.1

- Date: 2026-11-22
- Repository: `luqizheng/worldview`
- Audited ref: `master` @ `aa70a3ec80a6039c36353121fe4e81cc641f5f46`
- Scope: P2-09-37 through P2-09-47 against the actual `WorldView/src` and `WorldView/tests` tree
- Status: Repository-source audit; no local build or test execution is claimed

## 1. Executive verdict

**Building/Cave implementation has not started in the current production source tree.** The repository contains the CD-01 minimum Core Domain persistence foundation and the Building/Cave design package, but no production Object Type descriptor registry, Object identity/definition model, Geometry Component model, Command/Event model, Initial State model, registry compiler, or typed Asset-link persistence.

The current implementation is not a failed Building/Cave implementation; it is an earlier, intentionally bounded Core Domain stage. CD-01 explicitly excluded these capabilities. The next work must therefore be staged from the existing code rather than treating the Building/Cave proposal as an incremental two-type registry patch.

This audit inspected the GitHub source tree and fetched the relevant source/test/document files. It did not execute `dotnet build`, `dotnet test`, SQLite migrations, JSON Schema validation, or Windows/macOS tests. The CD-01 test counts below are claims recorded in the repository's implementation report, not tests rerun by this audit.

## 2. Audited repository facts

### 2.1 Source tree

The current `WorldView/src/WorldView.Domain` tree contains:
- Maps: Map/History/Branch records, creation rules/services and Branch lineage.
- Persistence: `CoreDomainSchema` and `ICoreDomainDatabase`.
- Projects: Project identity record/rules.
- Time: `HistoricalTime`.
- Workspace: workspace context resolution.

The current Infrastructure tree contains:
- SQLite package database and Core Domain SQLite adapter/migration source.
- Core Domain AF-02 adapters.
- File-backed content-addressed Asset Store.

No production source paths or types were found for:
- Object Type descriptors or a compiled Object Type registry;
- Registry Format V2 parser/compiler or offline schema artifact loader;
- Object identity, allocator/reservation, Object definition or typed payload validation;
- Geometry Component identity/context/operations;
- Relationship descriptors or Relationship persistence;
- Command/Event persistence or handlers;
- Map Initial State persistence;
- `core_asset` metadata or typed Core Domain Asset-link repositories;
- Building/Cave aggregate services or validators.

The tests tree contains CD-01 Domain, Application and Infrastructure tests. No Object Type registry, Registry V2, Building/Cave schema, Geometry Component, Asset-link, or aggregate conformance test suite is present.

### 2.2 Implemented persistence boundary

`CoreDomainSchema.Migrations` currently contains exactly four Core Domain migrations:
1. `001_core_project`
2. `002_core_map_identity`
3. `003_core_history_branch`
4. `004_core_calendar`

They create six tables:
- `core_project`
- `core_map`
- `core_history`
- `core_branch`
- `core_calendar`
- `core_calendar_configuration`

The source comments explicitly exclude `core_map_initial_state`, all `core_object*` tables, all `core_command*` tables, `core_asset` and `core_checkpoint`. No Building/Cave persistence tables or migrations exist.

The CD-01 adapter exposes Project, Map, History and Branch reads/writes. It does not expose Object, Geometry, Command, Initial State or Asset-link repositories.

### 2.3 Asset boundary

`IAssetStore` and `FileAssetStore` implement immutable, content-addressed file byte storage using SHA-256. `Program.cs` registers `IAssetStoreFactory`, and the package-open flow creates the file store.

This is only the byte store. The current source does not implement:
- Core Asset metadata rows / `core_asset`;
- typed relational links such as `core_object_definition_asset` or `core_command_asset`;
- owner-payload-to-Asset identity/hash validation;
- Building/Cave Geometry Component publication/read-back;
- Asset orphan recovery integrated with a formal Core Domain publication transaction.

Do not treat the existing FileAssetStore as proof that the P2-09-37 Asset integration is implemented.

## 3. P2-09-43 S0–S10 implementation status

| Slice | Status | Source evidence / gap |
|---|---|---|
| S0 Repository preflight | **Partial / preflight confirms absence** | Existing source has no Object registry, definition, Initial State, Command handlers or typed Asset links to extend. The implementation locations for these capabilities must be established in a fresh implementation design. |
| S1 Registry format extension | **Not implemented** | No Registry V2 model, parser/compiler, offline schema resource loader or registry tests. |
| S2 Building/Cave descriptors | **Not implemented** | No compiled descriptor registry; P2-09-41 remains a proposal and P2-09-42 says it is incomplete as typed_v2. |
| S3 Aggregate schema validation | **Not implemented** | P2-09-38/39 JSON Schemas are design artifacts; no production Draft 2020-12 validator integration or stable domain error mapping. |
| S4 Child identity/reference services | **Not implemented** | No owner-scoped child identity allocator, no-reuse service or aggregate reference validator. |
| S5 Geometry component context | **Not implemented** | No formal Geometry Component model or geometry command path in source. |
| S6 Asset link integration | **Not implemented** | File byte store exists, but Core Asset metadata, typed links, payload/link/hash consistency and publication transaction are absent. |
| S7 Building aggregate operations | **Not implemented** | No Building definition CAS, Level/Space/Plan/Boundary/Access validator or supported geometry operation routing. |
| S8 Cave aggregate operations | **Not implemented** | No Cave Survey Frame, Chamber/Passage/Entrance/Station/Connection validator or uncertainty/partial-survey handling. |
| S9 Initial State and Object presence | **Not implemented** | CD-01 deliberately has no Initial State table and no Object identity/presence model. |
| S10 Conformance/platform proof | **Not implemented** | No P2-09-40 vector runner, P2-09-28 registry suite, aggregate tests, or Windows/macOS Building/Cave evidence. |

**Overall:** S0 can be considered complete as a source-tree discovery exercise, but the implementation preconditions for S1–S10 are not present. No slice should be marked implemented.

## 4. Design-to-code alignment and important constraints

1. P2-09-37 V0.7 recommends two top-level Object Types, Building and Cave System, with owner-scoped typed child aggregates. It explicitly avoids generic child Object graphs, a polymorphic Geometry owner and new tables for the initial adoption.
2. P2-09-41 V0.4 is not an enabled registry. P2-09-42 V0.9 explicitly states it is incomplete as a typed_v2 descriptor and must not be appended to the active registry.
3. P2-09-45/46/47 define a proposed Registry Format V2 package, but the vectors are design-only and no production schema/compiler execution is evidenced.
4. P2-09-40 V0.6 contains 55 design-only aggregate vectors. They have not been executed by a production validator or Core Domain test runner.
5. P2-09-13 V0.7 and P2-09-11 V0.11 both describe the 22-table Core Domain logical baseline. This is a proposal inventory, not the current code schema. The six-table CD-01 implementation is an intentional subset.
6. P2-09-13 V0.7 identifies `core_object_definition_asset` as conditional in the general catalog but required for Building/Cave formal geometry stored in current Object definitions. The current source has no such table.
7. `core_map_initial_object_asset` is not required by the current Building/Cave proposal because the proposed Initial State payload contains membership/placement rather than separate formal geometry Asset references. Do not add it speculatively.
8. The current Initial State payload shape remains an open gate in CD-01. Do not invent an Initial State schema merely to fill the typed_v2 descriptor block; resolve the authority contract first.
9. Existing ten Object Type compatibility is a hard requirement for Registry V2. No existing registry implementation is present in this codebase to migrate or regress yet.

## 5. Recommended implementation order

The correct next step is not to implement Building and Cave aggregates immediately. Establish the missing shared Core Domain foundation in this order:

### Phase A — Core Domain capability baseline
- Reconcile the current CD-01 code with the latest P2-09-11 V0.11 / P2-09-13 V0.7 logical inventory without claiming the 22-table proposal is implemented.
- Establish and adopt the Object Type descriptor/runtime/Initial State authority boundaries from P2-09-18 and related contracts.
- Define the minimum Object identity, definition payload, allocation/reservation and Map registration/presence implementation slices.
- Resolve the Initial State payload open gate before wiring any typed_v2 Initial State schema reference.

### Phase B — Registry V2 foundation
- Implement strict Registry Format V2 parsing and Draft 2020-12 JSON Schema validation.
- Implement offline, hash-pinned schema artifact loading and registry cross-record compiler checks.
- Preserve the ten existing legacy descriptor semantics exactly when those descriptors are introduced.
- Execute P2-09-47 vectors and P2-09-28 compatibility tests; do not activate specialist descriptors before the gates pass.

### Phase C — Shared geometry and Asset foundation
- Implement formal Object-owned Geometry Component identity and metadata according to P2-09-19.
- Implement Core Asset metadata and the typed link contracts needed by the adopted payloads.
- Integrate content-addressed file publication with relational metadata/link validation and recovery behavior.
- Implement the applicable formal Geometry Command input path, rather than creating a separate Building/Cave geometry persistence model.

### Phase D — Building/Cave typed aggregates
- Complete P2-09-41 as full typed_v2 descriptors with definition/runtime/Initial State schema pins and all required descriptor blocks.
- Wire P2-09-38/39 schemas into offline strict validation.
- Implement owner-scoped child identity/reference validation and typed component child-context validation.
- Add Building and Cave definition CAS and only the supported existing geometry operations.
- Execute all P2-09-40 vectors and preserve explicit deferrals for child-level historical lifecycle operations.

### Phase E — Integration proof
- Verify Map registration, Initial State membership/placement and post-zero Object creation semantics.
- Run all legacy registry, schema, aggregate, Asset publication, CAS, failure/recovery and persistence regression suites.
- Record deterministic Windows and macOS payload/hash evidence before production-ready status.

## 6. CD-01 verification boundary

The repository's CD-01 Implementation Report V0.1 records 139/139 tests passing and Debug/Release builds with zero errors on Windows/.NET 11.0.100. It also explicitly records that the P2-09-12 full transaction-proof suite was not executed and macOS was not verified.

This audit did not rerun those commands. The report is evidence for the stated CD-01 scope only; it is not evidence for any Building/Cave, Registry V2, Geometry Component, Object, Initial State or typed Asset-link capability.

## 7. Final disposition

- Keep P2-09-37 through P2-09-47 as the current design proposal package.
- Do not activate Building/Cave descriptors.
- Do not add Building/Cave-specific tables.
- Do not claim Registry V2 or Building/Cave production implementation has started.
- Begin with the shared Object/Initial State/Registry foundations and then proceed through P2-09-43 S0–S10.
