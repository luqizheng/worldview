# Field Payload Storage Implementation Plan V1

**Status:** Accepted implementation plan  
**Decision date:** 2026-11-22  
**Parent baseline:** [Field Data Storage Baseline V1](field-data-storage-baseline-v1.md)  
**Target:** WorldView 1.0 Core Domain / Map Engine persistence

## 1. Purpose

Translate the accepted SQLite + filesystem hybrid storage decision into an incremental implementation that does not break the current EF Core migration baseline, Field Dataset identity, immutable revision lineage, Field Payload integrity checks, or compare-and-swap publication semantics.

This document records the accepted storage contract and the implementation boundary. Completed items are listed explicitly; remaining work is not implied to be complete.

## 2. Storage contract

A Field Revision owns one immutable logical payload. The payload may be physically stored in either:

- **SQLite BLOB**: raw bytes in a BLOB column, with no Base64 or JSON wrapping.
- **Managed external file**: raw bytes under the package's managed storage root, referenced by a normalized relative storage key.

Both forms expose the same logical metadata:

- payload ID and owning Project / Dataset / Revision;
- storage kind;
- payload format identifier and format version;
- byte length;
- hash algorithm and content hash;
- publication/integrity state and creation time.

The storage choice must not change Field Dataset or Revision identity, lineage, or semantic meaning.

### 2.1 Initial storage policy

- Prefer SQLite BLOB for small and medium payloads when transactional publication and simple backup/restore are beneficial.
- Use external files for large payloads, tiled collections, streaming/random-access workloads, or when database growth makes BLOB storage undesirable.
- Do not set a universal threshold in this document. Establish defaults only after representative workload measurements on Windows and macOS.
- Do not create one filesystem file per tiny tile by default. A revision may use a packed payload file or a bounded collection of larger chunks when that is more efficient.
- Store exact bytes. Compression, if used, is an explicit payload codec with a versioned contract, not an implicit storage-layer transformation.

## 3. Proposed relational model

The first schema iteration should introduce a dedicated `core_field_payload` table rather than overloading `core_asset` semantics. Existing `core_asset` remains the authority for generic project assets and their current external-file lifecycle.

Proposed columns:

| Column | Type | Meaning |
|---|---|---|
| `field_payload_id` | TEXT UUID | Stable payload identity |
| `project_id` | TEXT UUID | Owning Project |
| `field_dataset_id` | TEXT UUID | Owning Dataset |
| `field_revision_id` | TEXT UUID | Owning immutable Revision |
| `storage_kind` | TEXT | `sqlite_blob` or `managed_file` |
| `payload_format` | TEXT | Stable format identifier |
| `format_version` | TEXT | Format version |
| `byte_length` | INTEGER | Exact stored payload byte length |
| `hash_algorithm` | TEXT | Initially `sha-256` |
| `content_hash` | TEXT | Lowercase SHA-256 hex |
| `payload_blob` | BLOB NULL | Present only for `sqlite_blob` |
| `relative_path` | TEXT NULL | Present only for `external_file` |
| `integrity_state` | TEXT | `verified`, `corrupt`, `missing` |
| `created_at_utc_ms` | INTEGER | Creation time |

Required database invariants:

1. Exactly one of `payload_blob` and `relative_path` is populated, consistent with `storage_kind`.
2. `byte_length > 0`; for BLOB rows it equals SQLite's BLOB byte length.
3. Hash algorithm and hash syntax are constrained; the hash is computed over the exact payload bytes.
4. The owner tuple is unique for the intended one-payload-per-revision V1 contract. If multi-part payloads become necessary, introduce an explicit part ordinal/key rather than silently weakening uniqueness.
5. Project / Dataset / Revision ownership is enforced by foreign keys and checked again at the Domain boundary.
6. External paths are normalized relative paths under the managed package root; absolute paths, drive-qualified paths, UNC paths, and traversal segments are rejected.
7. Published payload bytes and their identity metadata are immutable. Integrity state may change through explicit integrity scans.

Implemented table name is `core_field_payload`. The generated EF Core migration and snapshot are committed; do not hand-edit the snapshot or create a second migration ledger.

## 4. Revision metadata replaces the internal JSON Manifest

The internal `FieldRevisionRecord.ManifestJson` / `PublishFieldRevisionPlan.ManifestJson` contract is transitional. Replace its authority with typed, validated metadata.

The initial typed metadata contract must cover every semantic dimension currently validated by the Field manifest, including:

- descriptor key, descriptor schema version, and semantics ID;
- Map Space identity and authoritative spatial-reference identity/version;
- grid width/height, origin, spacing, axis orientation, and cell registration;
- coverage mode and explicit validity-mask representation;
- plane identity/order, sample encoding, bytes per sample/cell, and no-data semantics;
- tile dimensions/layout and edge behavior;
- codec identity/version;
- payload format/version and content integrity.

Prefer normalized columns/relations for values that are queried, constrained, or independently versioned. A compact typed value object may be stored as a binary payload only if its wire format is separately versioned and validated. Do not replace JSON with an opaque undocumented byte array.

`ProvenanceJson` is distinct from Field layout/encoding metadata. It may remain JSON if it is useful as human-readable audit/provenance content; it must not be used as a substitute for typed spatial or payload metadata.

## 5. Settings documents

Project and Map settings are external versioned documents. Add a settings-document reference model that records:

- owner kind and owner ID;
- normalized relative path/storage key;
- format identifier and schema version;
- byte length and SHA-256 hash;
- creation/update timestamps.

Write protocol:

1. Serialize and validate the new settings document.
2. Write to a unique staging file beneath the package's managed settings directory.
3. Flush and close; verify byte length and hash.
4. Atomically replace the immutable/versioned final file on the same filesystem.
5. Update the SQLite settings reference in a transaction.
6. Clean up stale staging files and unreferenced prior versions through a recoverable maintenance operation.

The project package's package-level manifest remains a separate concern and is not implicitly replaced by this settings model.

## 6. Publication and recovery

### 6.1 BLOB publication

Insert the BLOB row and immutable Field Revision metadata in the same SQLite transaction that advances the Dataset current-revision pointer using the existing CAS token. A failed transaction must leave no published revision or orphan BLOB row.

### 6.2 External-file publication

SQLite and filesystem changes are not one atomic transaction. Use this protocol:

1. Stage under a unique path inside the managed package root.
2. Stream bytes while calculating SHA-256 and byte length.
3. Flush/close and verify the staged file.
4. Atomically rename to an immutable content-addressed or payload-ID final path on the same filesystem.
5. In one SQLite transaction, insert payload metadata, insert the immutable Revision, and CAS-advance the Dataset pointer.
6. If the database transaction fails, leave no authoritative reference; enqueue or discover the final file as an orphan and clean it safely.
7. On startup/integrity scan, reconcile DB references against file existence, length, and hash. Never silently mark missing/corrupt bytes as verified.

Crash recovery must explicitly cover termination after staging, after final rename, during the DB transaction, and after commit but before staging cleanup.

## 7. Migration sequence

1. **Model:** add typed Field layout metadata and payload-reference records using EF-owned generated migrations/snapshots.
2. **Storage adapters:** implement SQLite BLOB and managed external-file storage behind the Field Payload metadata contract.
3. **Publication:** insert BLOB payload metadata/bytes, immutable revision and Dataset CAS in one SQLite transaction; external files use staged publication followed by the SQLite reference transaction.
4. **Unified reads and recovery:** route reads by storage kind, verify hash/length, then add crash recovery and orphan reconciliation.
5. **Settings files:** store Project/Map settings as versioned external documents with staged writes and path/hash references.
6. **Retire transitional references:** generic Asset coupling has been removed from Field Revision publication. Retire internal ManifestJson persistence only after all readers/writers and package interchange consumers use their declared authority.

This is a fresh-schema V1 release with no historical user databases to preserve. The initial schema and migration target models are kept aligned with the current EF model; no data backfill or legacy dual-reference migration is required. Any later released-schema evolution must use the normal EF Core migration workflow.

## 7.1 Completed implementation — typed projection and persistence

The following work is implemented in PR #11:

- The validated canonical Manifest is projected into typed descriptor, spatial identity, grid, coverage, edge, no-data, plane, category-code, codec and tile-hash metadata.
- Grid dimensions remain integer values and coordinate/origin/spacing values remain exact decimal strings; they are not converted to binary floating point.
- Map Space is a stable Map-owned identity. A Map binds to an immutable project-scoped Spatial Reference definition.
- The Spatial Reference definition owns a typed coordinate precision profile and optional vertical-reference identity.
- Domain validation and the SQLite publication transaction compare Map Space, Spatial Reference, coordinate precision profile and vertical-reference identities against the owning Map and immutable definition.
- Field Revision scalar metadata is stored in typed SQLite columns. Ordered planes/codecs and category codes are stored in dedicated child tables.
- SQLite reads reconstruct FieldRevisionMetadata from typed relational rows and do not parse `manifest_json`.
- `manifest_json` remains a canonical interchange/diagnostic copy while remaining package consumers are migrated; it is not the typed metadata read authority.
- EF migrations `20261122140144_MapSpatialReferenceOwnership` and `20261122141903_FieldRevisionTypedMetadata` were generated by the real EF CLI. Their Designer files and the generated model snapshot are committed.

## 7.2 Implemented Field Payload persistence slice

The following implementation is committed in PR #11:

- `CoreFieldPayloadEntity` and the `core_field_payload` EF mapping now represent one physical payload per immutable Field Revision.
- The table stores payload identity, Project / Dataset / Revision ownership, storage kind, format/version, SHA-256, byte length, integrity state, and either a raw SQLite BLOB or a managed relative path.
- CHECK constraints enforce supported storage kinds, the BLOB/path exclusivity rule, BLOB byte length, positive payload length, SHA-256 syntax/algorithm, and integrity-state values.
- A unique index enforces one payload per Field Revision. A composite FK binds the payload to the same Project, Dataset and Revision.
- Migration `20261122154650_FieldPayloadStorage` introduced the dedicated payload table. For the current no-history V1 baseline, the initial schema and migration target models were then aligned to remove the transitional Field Revision → generic Asset column/FK/index; the current model snapshot matches the EF configuration.
- `PublishFieldRevisionPlan` now carries a Field Payload registration plan and optional raw bytes. SQLite BLOB registrations are rechecked for owner IDs, byte length and SHA-256 inside the publication transaction.
- BLOB bytes and Field Payload metadata are inserted in the same SQLite transaction as the immutable Field Revision and Dataset current-pointer CAS. A failed publication rolls back the BLOB row with the Revision.
- `FindFieldPayload` and `ReadFieldPayloadBlob` expose metadata and exact raw BLOB readback. Readback verifies byte length and SHA-256 before returning a copy.
- `FieldPayloadReader` is the storage-kind-neutral Application read API. It returns a read-only memory stream for verified BLOB bytes and verifies ManagedFile length/SHA-256 incrementally before reopening the immutable content-addressed file for streaming consumption.
- `FieldPayloadPublisher` streams ManagedFile bytes to the content-addressed store, then publishes the payload metadata, immutable Revision and Dataset CAS through the authoritative SQLite transaction. A failed DB publication deliberately retains the final file as a recoverable orphan; it never eagerly deletes potentially shared content.
- `FieldPayloadIntegrityReconciler` verifies all project payloads, updates `verified` / `missing` / `corrupt` state, reports unreferenced managed files, and offers an explicit collection operation that performs a fresh DB reference scan before deleting only unreferenced content-addressed files. Referenced corrupt/missing files are retained.
- Infrastructure tests cover BLOB and ManagedFile readback, managed-file publication orchestration, same-length hash corruption, missing files, integrity-state updates and safe orphan collection. Domain test doubles implement the payload reconciliation port methods.

The generic Asset dependency has now been removed from Field Revision records, publication plans, Domain validation, SQLite persistence and the fresh V1 schema. `core_field_payload` is the sole Field byte-storage reference; generic Assets remain available for unrelated project assets and typed consumers.

## 7.3 Remaining implementation boundary

The repository does not yet provide:

- startup crash recovery wiring and automatic crash-window repair at application startup;
- Project / Map settings-document persistence as versioned external files;
- production package writer integration and regenerated binary golden fixtures from the current canonical Manifest contract.

Generic Assets remain valid for generic project assets and definition links. They are no longer part of Field Revision publication or Field Payload orphan/integrity semantics.

## 7.4 Next implementation sequence

1. **Completed:** remove the generic Asset reference from Field Revision records, plans, EF schema and orphan-Asset logic. The fresh V1 schema uses `core_field_payload` as the only Field byte-storage reference.
2. **Completed:** add the storage-kind-neutral Application read API for verified BLOB and ManagedFile content.
3. **Completed:** integrate ManagedFile staging, streaming hash/length verification, atomic content-addressed publication and the SQLite payload/Revision/Dataset transaction. Cross-resource crash recovery and orphan cleanup remain separate work.
4. **Partially completed:** explicit DB/file integrity reconciliation, integrity-state transitions and safe orphan collection are implemented. Add startup crash recovery wiring so the reconciliation pass is scheduled when a package is opened and crash windows are repaired before normal editing.
5. Add Project/Map settings-document storage as a separate follow-up slice.
6. Complete the production package writer and regenerate binary golden fixtures from the current canonical Manifest contract.
7. Retire transitional Manifest persistence only after every production reader/writer and interchange consumer has moved to its declared authority.

## 8. Required tests

- BLOB write/read byte-for-byte round trip, empty payload, and large payload boundaries.
- External-file write/read byte-for-byte round trip and streaming without loading the whole file into memory.
- Storage-kind / BLOB / path CHECK constraints.
- Hash and byte-length mismatch rejection.
- Path traversal, absolute path, drive path, UNC path, symlink/reparse-point escape, and package-root confinement.
- Failure injection at every external-file publication crash window.
- Missing, truncated, corrupt, and orphan external-file detection and recovery.
- BLOB and external-file publication both preserve immutable Revision lineage and Dataset CAS behavior.
- Settings document schema-version validation, atomic replacement, and hash verification.
- Windows and macOS filesystem behavior, including atomic rename constraints and case sensitivity assumptions.
- Existing package import/export compatibility tests remain separate from internal persistence tests.

## 9. Explicit non-goals

- No universal BLOB/file size threshold before measurement.
- No mandatory binary Field Package container format.
- No requirement to store every Field tile as an individual file.
- No requirement to convert all SQLite metadata into binary.
- No removal of JCS until its production consumers and any retained interchange contract are accounted for.
- No changes to the accepted EF Core migration ownership rules.
