# Field Payload Storage Implementation Plan V1

**Status:** Accepted implementation plan  
**Decision date:** 2026-11-22  
**Parent baseline:** [Field Data Storage Baseline V1](field-data-storage-baseline-v1.md)  
**Target:** WorldView 1.0 Core Domain / Map Engine persistence

## 1. Purpose

Translate the accepted SQLite + filesystem hybrid storage decision into an incremental implementation that does not break the current EF Core migration baseline, Field Dataset identity, immutable revision lineage, Asset integrity checks, or compare-and-swap publication semantics.

This is an implementation plan, not a claim that the payload storage abstraction is already implemented.

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
| `payload_id` | TEXT UUID | Stable payload identity |
| `project_id` | TEXT UUID | Owning Project |
| `field_dataset_id` | TEXT UUID | Owning Dataset |
| `field_revision_id` | TEXT UUID | Owning immutable Revision |
| `storage_kind` | TEXT | `sqlite_blob` or `external_file` |
| `payload_format` | TEXT | Stable format identifier |
| `format_version` | TEXT | Format version |
| `byte_length` | INTEGER | Exact stored payload byte length |
| `hash_algorithm` | TEXT | Initially `sha-256` |
| `content_hash` | TEXT | Lowercase SHA-256 hex |
| `payload_blob` | BLOB NULL | Present only for `sqlite_blob` |
| `relative_path` | TEXT NULL | Present only for `external_file` |
| `integrity_state` | TEXT | `verified`, `unverified`, `corrupt`, `missing` |
| `created_at_utc_ms` | INTEGER | Creation time |

Required database invariants:

1. Exactly one of `payload_blob` and `relative_path` is populated, consistent with `storage_kind`.
2. `byte_length >= 0`; for BLOB rows it equals SQLite's BLOB byte length.
3. Hash algorithm and hash syntax are constrained; the hash is computed over the exact payload bytes.
4. The owner tuple is unique for the intended one-payload-per-revision V1 contract. If multi-part payloads become necessary, introduce an explicit part ordinal/key rather than silently weakening uniqueness.
5. Project / Dataset / Revision ownership is enforced by foreign keys and checked again at the Domain boundary.
6. External paths are normalized relative paths under the managed package root; absolute paths, drive-qualified paths, UNC paths, and traversal segments are rejected.
7. Published payload bytes and their identity metadata are immutable. Integrity state may change through explicit integrity scans.

The exact table and FK shape must be reconciled with the current generated EF Core model before implementation. Do not hand-edit the model snapshot or create a second migration ledger.

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

1. **Model first:** add typed Field layout metadata and payload-reference records without deleting the existing manifest field.
2. **Dual-read transition:** new code reads typed metadata when present and validates the legacy manifest for existing rows. New writes populate typed metadata and may temporarily retain the manifest for rollback/diagnostics.
3. **Backfill:** deterministically parse existing manifest rows into typed metadata. Reject malformed or semantically inconsistent rows; do not silently repair them.
4. **Storage adapter:** implement and test SQLite BLOB round trips and managed external-file round trips behind one storage interface.
5. **Publication switch:** publish new revisions using payload references and typed metadata under the existing SQLite transaction/CAS rules.
6. **Settings files:** migrate Project/Map settings with staged writes and path/hash references.
7. **Retire legacy path:** only after all readers, writers, tests, fixtures, and package interchange consumers have migrated, remove internal ManifestJson authority and JCS dependency if no explicit interchange contract still requires them.

Each schema change must use `dotnet ef migrations add <nameOfMigration>`; the initial migration remains unchanged and generated snapshots remain EF-owned.

## 7.1 Implementation progress — typed projection slice

The first Domain implementation slice now exists in `FieldRevisionMetadata.cs`:

- The validated manifest is projected into typed descriptor, spatial identity, grid, coverage, edge, no-data, value-plane, codec, and tile-hash metadata.
- Grid dimensions remain integer values and coordinate/origin/spacing values remain exact decimal strings; they are not converted to binary floating point.
- Value planes are represented as typed records, and category codes are exposed as a read-only dictionary.
- `PublishFieldRevisionPlan` and `FieldRevisionRecord` expose the typed projection. SQLite reads currently reconstruct it from the validated legacy manifest, and publication still writes `manifest_json`.
- Integration tests verify typed values and the projection after reloading a revision.

This is deliberately a compatibility bridge, **not yet the typed SQLite persistence migration**. No EF entity, generated migration, or model snapshot was changed in this slice. The next persistence slice must add normalized typed storage and payload references using the EF CLI migration workflow, then backfill and verify existing rows before retiring `manifest_json`.

## 7.2 Typed persistence design and spatial ownership gate

The next design artifact is [Field Revision Typed Persistence Design V1](field-revision-typed-persistence-design-v1.md). It records the verified current EF/domain boundary and specifies the initial relational shape:

- Immutable revision scalar metadata is stored on the revision row; ordered planes/codecs and category codes use typed child rows.
- Grid origin/spacing remain canonical decimal TEXT; no conversion to SQLite REAL is permitted.
- Map Space ID and Spatial Reference ID remain distinct. The current Core Domain model has no authoritative owner records for either, so the first metadata migration must preserve them as opaque validated identifiers and must not invent foreign keys to `core_map` or assume EPSG aliases.
- A separate owner contract is required before spatial catalog foreign keys are introduced. ME-05-04 describes Spatial Reference as an immutable project-scoped definition; the exact Map Space owner/lifecycle still needs reconciliation.
- The current P2-09-34 manifest schema omits dimensional unit, vertical-reference identity, precision profile and explicit resolution profile required by ME-05-01. These values cannot be backfilled from current manifests. Reconcile the semantic contract and validation vectors before declaring typed persistence complete.
- The accepted dedicated Field Payload model supersedes the older assumption that all Field Revision bytes must be represented as generic Core Assets; the transition must not create two competing payload authorities.

No EF schema or generated snapshot was changed for this design slice. The next code/schema change remains gated on resolving the manifest semantic gap and running the real EF CLI migration workflow.

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
