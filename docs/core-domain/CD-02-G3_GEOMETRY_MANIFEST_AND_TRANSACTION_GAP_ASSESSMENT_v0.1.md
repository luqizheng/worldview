# CD-02-G3 — Geometry Manifest and Provider Transaction Implementation Gap Assessment

- Version: 0.1
- Date: 2026-11-22
- Status: Repository-grounded assessment; no implementation or provider-proof claim
- Baseline: `cf5dd1a313b34ab322bb60d1fd1f1bb99d1fb285`
- Scope: Geometry Component manifest authority and P2-09-12 V0.7 provider/cross-module transaction proof readiness

## 1. Executive conclusion

The repository has enough adopted design material to define a bounded, reusable Geometry Component manifest contract, but it does **not** yet justify adding a universal generic manifest payload or generic geometry table.

The existing contracts already establish the logical owner fields and ownership boundaries:

- P2-09-19 V0.2 defines the logical Geometry Component manifest and one immutable Asset per formal component payload.
- P2-09-26 Registry V0.2 declares Object-Type-specific geometry roles, cardinality, allowed families and 2D dimension.
- P2-09-41 V0.4 adds Building/Cave-specific owner payload fields and typed aggregate validation.
- ME-05-02 V0.1 and ME-05-05 V0.3 define component operations, immutable publication, and separation of Core Assets, Derived Artifacts and presentation caches.

The common logical record and a first validation layer are already implemented: `GeometryComponentManifest`, `GeometryRules.ValidateManifest`, `GeometryRules.ValidateManifestConsistency`, and `GeometryAssetEnvelope`. The remaining bridge is owner-context integration: persist/deserialize that record through the adopted typed definition, Initial State and Command schemas; validate role cardinality and owner membership as a complete set; and enforce exact typed-link ↔ manifest ↔ Asset agreement atomically for each owner context. The current implementation/test must not be described as if the manifest record itself were absent. It also does not yet reject `Guid.Empty` for component/Asset IDs, and the generic consistency helper permits a null typed link; those are explicit validation/owner-policy gaps, not proof that the record is missing.

P2-09-12 V0.7 is explicitly a **test specification**, not an implementation report. The current repository has real SQLite persistence and concurrency tests, but no AI Assistant source module or Application Commit Coordinator was found in the source tree. Consequently, the cross-module atomicity tests cannot honestly be marked implemented or passed yet.

## 2. Repository evidence inspected

### Design contracts

- `docs/architecture/core-domain/p2-09-19-geometry-component-identity-and-payload-contract-v0.2.md`
- `docs/architecture/core-domain/p2-09-26-v1-object-type-descriptor-registry-v0.2.json`
- `docs/architecture/core-domain/p2-09-41-building-cave-object-type-descriptor-extension-v0.4.json`
- `docs/architecture/core-domain/p2-09-46-object-type-descriptor-registry-format-v2-json-schema-v0.2.json`
- `docs/architecture/core-domain/p2-09-12-provider-transaction-proof-test-spec-v0.6.md`
- `docs/map-editor/world-view-me-05-02-shared-geometry-model-and-component-operations-v0.1.md`
- `docs/map-editor/world-view-me-05-05-geometry-and-field-asset-package-publication-contract-v0.3.md`

### Current implementation/test inventory

Relevant production code includes:

- `WorldView/src/WorldView.Infrastructure/Persistence/SqliteConnectionAccess.cs`
- `WorldView/src/WorldView.Infrastructure/Persistence/SqlitePackageDatabase.cs`
- Core Domain SQLite adapters under `WorldView/src/WorldView.Infrastructure/Persistence/CoreDomain/`
- Asset byte verification use case and FileAssetStore under Application/Infrastructure.

Relevant existing tests include:

- `PersistenceConcurrencyTests.cs`
- `SqlitePackageDatabaseTests.cs`
- `Cd01EndToEndTests.cs`
- `Cd02ObjectPersistenceTests.cs`
- `Cd02AssetPersistenceTests.cs`
- `Cd02AggregateVectorConformanceTests.cs`
- `Cd02MapEditorIntegrationTests.cs`
- `AssetIntegrityVerificationTests.cs`
- `FileAssetStoreTests.cs`

No `WorldView/src/*Assistant*` module and no Application Commit Coordinator/transaction coordinator implementation were found in the inspected source tree at this baseline.

## 3. Geometry Manifest: authority and implementation boundary

### 3.1 Common logical envelope already established and partially implemented

P2-09-19 V0.2 requires the typed owner payload to retain:

- `geometry_component_id`
- `geometry_role`
- `geometry_family`
- `dimension`
- `spatial_reference_id`
- `geometry_schema_version`
- `asset_id`
- `content_hash`
- `provenance`
- `validity_metadata`

P2-09-41 V0.4 additionally lists Building/Cave owner fields including `coordinate_context_id` and `child_context`. Those extra fields are specific to that typed aggregate contract and must not be silently imposed on every Object Type.

The current code defines the ten-field `GeometryComponentManifest` record in `WorldView.Domain.Geometry.GeometryRules.cs`. `GeometryRules.ValidateManifest` checks declared role, allowed family, supported dimension, spatial-reference presence, schema version and hash syntax (and now rejects `Guid.Empty` component/Asset IDs via PR #5 (`b8133cd`), which passed Windows/macOS Debug/Release CI at 385/385 per configuration). `GeometryRules.ValidateManifestConsistency` checks Asset ID/hash and, when a link is supplied, link Asset ID/role. `GeometryAssetEnvelope.Validate` performs bounded JSON envelope/body validation.

There are two different persistence facts that must not be conflated:
- For Building/Cave typed aggregates, the G1 SQLite adapter reads the persisted `geometry_components` array from the current definition JSON and, in the same transaction as link insertion, requires exactly one component matching Asset ID, Object Type geometry role and authoritative Core Asset hash. Definition CAS also rechecks existing formal links against the proposed new payload.
- `Cd02AssetPersistenceTests.ManifestConsistency_FullChain_RealDb` for a legacy `river` Object constructs the manifest in memory; it does not serialize that manifest into the persisted River definition. The ten legacy compact descriptors do not yet provide typed definition schemas that persist a generic Geometry Component manifest. Thus the record and validation foundation exist, but owner-context persistence is only proven for Building/Cave, not all ten legacy Object Types.

### 3.2 Registry role declarations are constraints, not payload schemas

Registry V0.2's role descriptors provide:

- `min` / `max` cardinality;
- permitted geometry `families`;
- `dimension` (currently 2).

The Registry V2 JSON Schema validates this descriptor structure. It does not, by itself, define a universal serialized owner payload or a generic geometry-component manifest array.

Therefore:
- role declarations must remain the source of role/cardinality/family constraints;
- typed definition/initial-state/command schema artifacts remain the source of payload shape;
- the common manifest contract must define reusable validation semantics without replacing typed payload ownership;
- Building/Cave child context must remain a specialist extension, not a universal mandatory field.

### 3.3 Typed Asset-link role vocabulary must be reconciled

A second, concrete contract mismatch was found between the current G1 implementation and the latest registry-integration proposal:

- Current G1 code treats `core_object_definition_asset.role` as the Object Type's geometry role (for example `centerline` or `reach`). `SqliteCoreDomainDatabase.Assets.cs` validates it with `ObjectTypeRegistry.ValidateGeometryRole`; the Building/Cave adapter path then requires the same role in the owner manifest.
- P2-09-42 V0.9 proposes a different projection: the typed link row uses the generic role `geometry_component`; the exact component ID, Object Type geometry role, Asset ID and content hash remain together in the owner payload. One owner/Asset/generic-role link can cover multiple components reusing the same immutable Asset.
- P2-09-13 V0.7 describes the table's ownership purpose and conditional inclusion, but does not define this row-role vocabulary. P2-09-42 V0.9 is explicitly a proposal, not a frozen ADR or implementation authorization.

Do not silently switch the code or tests to either interpretation. Before broadening Geometry/Asset persistence, formally reconcile:
1. whether the relational `role` is a generic link-purpose discriminator or the descriptor geometry role;
2. the uniqueness rule when one Asset is referenced by multiple components or roles in one owner;
3. how every manifest component maps to exactly one Asset and how unexpected links are rejected;
4. how Building/Cave mandatory links and legacy ten-type optional-link semantics coexist.

The current implementation remains internally consistent with its existing tests, but the latest proposal and code are not yet semantically aligned. No schema migration appears necessary if the existing `role` column can carry the selected vocabulary; service validation and conformance tests would still need a deliberate update.

**Recommended G3 resolution (proposal, not yet adopted):** use the generic relational role `geometry_component` for the Definition Asset link; keep the actual Object Type geometry role exclusively in each owner manifest item. Create one typed link per distinct Asset ID per owner, while allowing multiple component manifest entries to reference that same immutable Asset. Validate exact set equality between distinct manifest Asset IDs and typed-link Asset IDs, and verify every component's pinned hash against the single authoritative `core_asset` row. This is consistent with the existing `AggregatePublicationContext` comment in `BuildingCaveAggregateValidator.cs` (which already describes `role = geometry_component`) and P2-09-42 V0.9. Other definition Asset kinds must declare their own explicit link-role contract rather than overloading geometry roles.


### 3.3.1 Definition payload and typed-link publication semantics need closure

The source review found an unresolved publication boundary in the current Building/Cave path:

- `EditObjectDefinition` updates the owner JSON and checks that every **existing** Asset link remains represented exactly once in the new payload.
- It does not require every Asset referenced by a new `geometry_components` entry to have a corresponding typed link.
- `LinkDefinitionAsset` separately reads the already-committed current definition, validates one matching manifest component and inserts one link in its own SQLite transaction.
- `BuildingCaveAggregateValidator.ValidatePublication` only compares the link set with the manifest Asset set when `AssetLinks.Count > 0`; an empty link list bypasses that equality check.

Therefore a Building/Cave definition containing a formal geometry manifest can be committed and become visible before its typed Asset link is committed. The existing `Me04_BuildingAuthoringPath_AllThroughDomainServices` integration test explicitly demonstrates this two-step workflow. P2-09-42 V0.9 says the typed link is required for formal geometry, but the inspected contract does not clearly state whether a persisted manifest-without-link is an allowed staging state. The current model has no explicit pending/publication state or visibility gate, so readers can encounter that intermediate state. This is not a SQLite transaction failure; it is an unresolved aggregate publication-state contract.

Recommended next implementation slice (unless an explicit staged publication state is adopted):
1. Introduce one Core Domain Domain Service / persistence-port operation for definition publication with its complete Asset-link set.
2. In one coordinated SQLite write transaction, verify owner lifecycle + expected definition hash, canonicalize/validate the typed payload, re-read every Asset's Verified state and authoritative content hash, compare the complete distinct owner-Asset link set with the manifest, update the definition, and replace/retain the typed links.
3. Reject missing, unexpected, duplicate or stale links before commit; a failed operation must leave both the previous definition and previous link set unchanged.
4. Keep the existing single-link helper out of the formal publication path, or make it an internal primitive that can only participate in the combined transaction.
5. Resolve the generic `geometry_component` versus descriptor-role vocabulary before choosing the exact link-set key.

Alternative: if two-step staging is intentional, add an explicit pending publication state and ensure no renderer, map query or downstream service treats the geometry as formally published until the complete Asset-link set is present. G1 currently proves safe individual link insertion and protects existing links during Definition CAS; it does not prove an atomic or explicitly staged aggregate publication lifecycle.


### 3.4 Remaining contract and implementation gaps


Do not recreate the already implemented record. The next contract revision should define:

1. Define the exact serialized owner-context schema and migration/version policy for the existing `GeometryComponentManifest` record.
2. Establish which Object Types and owner contexts currently persist manifests, and explicitly identify missing definition/Initial State/Command paths.
3. Validate the complete manifest collection against role `min`/`max` cardinality, not only each record independently.
4. Enforce owner Object identity, component-ID uniqueness/non-reuse, and History/Initial State scope as part of the owning Domain Service.
5. Make typed-link presence mandatory when the relevant owner schema requires it; the current `ValidateManifestConsistency` accepts a null link and therefore is not a complete publication gate by itself. Also reject empty component and Asset GUIDs at the manifest boundary.
6. Reconcile exact `asset_id` + `content_hash` and role with the matching typed relational Asset link atomically for definition, Initial State and Command contexts.
7. Define specialist schema extensions (e.g. Building/Cave child context) without changing common semantics, plus fail-closed compatibility rules.

Do not add a generic `geometry_components` property to an Object Type schema unless its adopted schema artifact explicitly defines that property or a new approved contract adds it. The presence of the C# manifest record does not itself prove that every Object Type's persisted JSON schema embeds an array of these records.

## 4. P2-09-12 V0.7: implementation readiness

### 4.1 Current evidence that can be extended

The current implementation provides a shared per-connection `SqliteConnectionAccess`, serialized connection access, transactional migrations, real SQLite persistence adapters, foreign-key enablement on the package connection, and concurrency/end-to-end tests.

These are useful foundations for TX-03, TX-30, TX-32, TX-40, TX-41, TX-42 and parts of TX-43. A new TX-03 test now opens two independent `SqlitePackageDatabase` instances against the same file and verifies per-connection `foreign_keys=ON`, `busy_timeout=5000`, `CommandTimeout=5`, and an actual rejected foreign-key write. A TX-32 test holds an external SQLite writer lock and verifies bounded `SQLITE_BUSY` failure with no partial write. These are not substitutes for test-by-test proof. The current connection coordinator still owns one connection per package database; TX-04/TX-31's independent application read-connection requirement is not implemented and must be reconciled with the actual UI read model before claiming full compliance.

### 4.2 Cross-module tests are blocked by missing implementation

P2-09-12 V0.7's core claim requires one Application Commit Coordinator to pass the same connection and transaction to Core Domain and AI Assistant repositories. The inspected source tree currently has no AI Assistant module and no such coordinator.

Therefore TX-01, TX-02, TX-10 through TX-23, TX-50 through TX-52, and the AI-dependent portions of TX-55 through TX-60 are blocked by missing production participants, not merely missing test cases.

Do not create fake AI repositories solely to make these tests green. First implement the actual AI submission boundary and its ownership contract; then instrument the real repositories to prove connection/transaction identity.

### 4.3 Specification/runtime version mismatch

P2-09-12 V0.7 now aligns with the repository's selected .NET 11 (`net11.0`), Avalonia 12.1.6 and Microsoft.Data.Sqlite 11.0.0 baselines. The exact transitive SQLitePCLRaw `e_sqlite3` version still needs to be captured in a lock/restore artifact.

Before claiming P2-09-12 compliance, add packaged application/native SQLite runtime checks on each supported Windows/macOS RID. A successful solution build/test alone does not prove packaged desktop startup or availability of every required native SQLite feature.

## 5. Initial test coverage classification

| P2-09-12 area | Current repository evidence | Classification |
|---|---|---|
| TX-01 / TX-02 shared transaction across Core + AI | No coordinator or AI repository found | Blocked — implementation prerequisite |
| TX-03 foreign-key enforcement | `ConnectionPolicy_EveryOpenedPackageConnectionEnablesForeignKeysAndBoundedTimeouts` opens two independent package DB connections, asserts FK=ON on both, and proves a real FK violation is rejected | Implemented for every currently implemented package connection constructor; no distinct reader/recovery connection type exists yet |
| TX-04 connection ownership/threading | Shared per-connection coordinator and same-connection concurrency tests exist; independent reader connection is not implemented | Partial; separate-reader requirement unresolved |
| TX-10–TX-23 cross-module atomicity/crash reconciliation | No AI submission persistence boundary found | Blocked — production AI/Core commit participants absent |
| TX-30 write serialization | `Concurrent_TwoWritersOnSameConnection_BothCommitFully`, `Concurrent_InsertProjectRace_ExactlyOneWinsWithDefinedDomainFailure`, `Concurrent_PackageDbOperationsAndCoreAccess_ShareSingleCoordinator` use real SQLite | Implemented evidence for same-connection serialization; multi-process coordination is out of scope |
| TX-31 independent reader isolation | All current application adapters share the package database connection; no dedicated read connection or active writer/read isolation test | Gap / architecture reconciliation required |
| TX-32 busy/locked handling | `BusyTimeout_ExternalWriterLock_FailsWithinBoundWithoutPartialWrite` holds a second connection's write transaction, asserts bounded SQLITE_BUSY and no partial row; production `DefaultTimeout=5` aligns command and PRAGMA timeouts | Bounded failure is tested; automatic retry is intentionally absent because no stable idempotency key is part of the write API. Spec's retry clause remains open |
| TX-40–TX-42 migrations/bootstrap | `SqlitePackageDatabaseTests` covers fresh bootstrap, migration transaction+ledger, idempotent revalidation, new migration, checksum mismatch, unknown migration, failed migration rollback and corrupt file quick check; concurrency suite adds cross-connection initialization race and concurrent migration/read | Strong current evidence; full schema/index/trigger/FK inventory and packaged runtime remain incomplete |
| TX-43 full cross-table audit | Domain integrity and aggregate tests exist; every listed invariant not yet mapped | Partial |
| TX-50–TX-54 Asset/checkpoint/backup/restore | Byte verification, FileAssetStore and Definition Asset metadata/link tests exist; complete staged publication orchestration, checkpoint and backup/restore proof incomplete | Partial / Gap |
| Geometry manifest validation (P2-09-19 / ME-05) | `GeometryComponentManifest`, per-record role/family/dimension/hash validation, envelope validation and one real-SQLite Definition-link three-way test exist | Implemented foundation; owner-context/cardinality/atomic-link coverage incomplete |
| TX-55–TX-60 formal command + AI evidence | Core object/geometry pieces exist, but integrated AI submission transaction absent | Blocked / Partial Core-only |
| TX-61 / TX-62 history-local activity/relationship endpoint policy | Must be mapped to current descriptor/service/test inventory before claiming coverage | Unverified |
| TX-65–TX-71 Field Dataset publication | Field Dataset implementation and package lifecycle must be checked independently; not inferred from Asset byte verification | Unverified |

This table is a planning classification, not a test result.

## 6. Recommended execution sequence

1. **G2 closeout:** Windows/macOS Debug/Release CI on documentation HEAD `cf5dd1a` passed; keep PR #2 unmerged.
2. **Geometry manifest integration:** extend the existing `GeometryComponentManifest` implementation rather than recreating it. Define owner-context serialization, whole-collection cardinality checks and mandatory typed-link semantics; reconcile them against all ten built-in Registry V0.2 descriptors and the Building/Cave extension. Add conformance vectors first.
3. **Geometry persistence hardening:** implement only the missing owner-context paths and adapter-level atomic checks after the contract is internally consistent. Add positive/negative tests for cardinality, component identity uniqueness, owner context, required link presence, exact Asset ID/hash/role pair and stale CAS.
4. **Provider proof baseline:** continue adding executable tests for capabilities already present. TX-03 and bounded-failure TX-32 now have explicit tests; next cover exact schema inventory/audit and remaining migration/runtime clauses. Keep TX-04/TX-31 blocked until the separate-reader architecture is implemented or the specification is formally revised.
5. **AI/Core commit boundary:** implement the real AI submission persistence model and Application Commit Coordinator as a separate architecture/implementation slice; then implement TX-01/02/10–23 and AI-dependent typed-link cases.
6. **Packaged runtime and durability:** add Windows/macOS packaged startup/native SQLite feature checks, then backup/restore and crash/reconciliation tests before any full atomicity claim.

## 7. Stop conditions

- Do not add a universal component table or generic untyped Asset owner table.
- Do not infer typed payload schema from role declarations alone.
- Do not label Core-only transaction tests as cross-module atomicity proof.
- Do not claim packaged-runtime or crash durability proof from ordinary `dotnet test`.
- Do not mark P2-09-12 V0.7 complete until all mandatory acceptance clauses are either proven or explicitly superseded by an approved architecture revision.

## 8. Status

This assessment is a repository-grounded planning artifact. It adds no runtime behavior. The TX-03 and TX-32 tests are present on the separate provider-proof branch; their latest cross-platform CI is tracked independently and must pass before they are counted as verified. A follow-up source inspection corrected the initial gap statement: the common C# manifest record and basic validation already exist; the unresolved work is typed owner-context persistence, complete collection/identity validation and mandatory atomic typed-link enforcement. Its purpose is to prevent duplicate abstractions, premature generic schema changes and false cross-module transaction claims while enabling the next implementation slices to proceed safely.
