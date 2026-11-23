# Field Data Storage Baseline V1

**Status:** Accepted  
**Decision date:** 2026-11-22  
**Scope:** WorldView project/map settings and Field Dataset / Field Revision persistence  
**Supersedes:** The assumption that an RFC 8785 JSON manifest is required as the internal Field Package storage contract.

## 1. Decision

WorldView uses a **SQLite + filesystem hybrid storage architecture**.

- SQLite is the authoritative store for domain records, relationships, lifecycle state, revision identity, ownership, storage references, and integrity metadata.
- SQLite BLOB columns may hold binary payloads, including Field data and tiles, where keeping the bytes in the database is practical and beneficial.
- External files hold settings and larger binary payloads where filesystem storage is more appropriate. SQLite stores the authoritative reference to each external file, together with its format/version and integrity metadata.
- Field Package is an import/export and interchange format. Internal Field persistence is not required to pass through a Field Package file.
- JSON is permitted for human-readable configuration, diagnostics, and interchange metadata where useful. JSON Manifest and RFC 8785 JCS are **not mandatory internal Field data storage formats or dependencies**.

This decision does not require every binary payload to be stored in SQLite, nor does it require every payload to be an external file. The storage backend is selected by the owning subsystem according to payload size, access pattern, transaction requirements, and lifecycle.

## 2. Authority and responsibility

### SQLite

SQLite owns the authoritative domain state, including:

- Field Dataset identity and ownership (Project / Map / Field key).
- Field Revision identity, immutable revision history, parent/current-revision relationships, and publication state.
- Field descriptor identity and schema version, semantics identity, and spatial ownership references.
- Payload storage kind (database BLOB or external file), storage key/path, payload format/version, byte length, and content hash.
- References from domain entities to stored payloads and settings files.
- Any state needed to validate that a referenced payload belongs to the expected Dataset, Revision, Map, and Project.

A database row must not be treated as valid merely because a path string exists. External payload references must be validated against their owner metadata and integrity metadata.

### Filesystem

The filesystem owns the bytes of external files, including:

- Project and map settings documents.
- Large Field payloads / tile collections when external storage is selected.
- Import/export packages and interchange artifacts.
- Other large immutable artifacts whose lifecycle is managed by a domain owner.

SQLite records the path or storage key and the metadata needed to resolve and verify the file. Paths must be confined to the owning project/package storage root; untrusted absolute paths and traversal paths must be rejected.

### SQLite BLOB

BLOB is a first-class storage option, not a fallback or an encoding workaround. It is suitable when its transactional and lifecycle advantages outweigh database growth, backup, and I/O costs. The bytes remain binary; they are not base64-encoded or expanded into JSON.

## 3. Field data model

Keep these concepts distinct:

1. **Field Dataset** — the map-owned logical source field and its descriptor/semantics identity.
2. **Field Revision** — an immutable published version of a Dataset, with ownership, lineage, fingerprint, and payload reference.
3. **Field payload** — the actual binary samples, validity masks, planes, and tiles. It may be stored as one or more SQLite BLOBs or as external files.
4. **Field Package** — a portable import/export representation that packages payload bytes and the metadata required to validate and import them.

A Field Revision must not require a JSON manifest string to be its canonical internal representation. Its authoritative metadata belongs in typed SQLite columns/relations. If a future interchange format uses JSON, that is an interchange decision and must not leak into the internal persistence contract.

The storage layer must preserve the field's semantic and spatial meaning independently of its physical storage choice: descriptor, dimensions, grid transform, axis orientation, cell registration, coverage/validity semantics, sample encoding, byte order, tile layout, and spatial-reference ownership must be represented and validated by the domain/package contracts. They do not have to be serialized as JSON.

## 4. Storage selection

Use the following policy:

- Store small and medium binary payloads in SQLite BLOBs when atomic publication with the corresponding domain records is valuable and database size/access characteristics remain acceptable.
- Store large payloads or tile collections as external files when streaming, random access, memory mapping, incremental reads, or database-size management make that preferable.
- Do not create a separate file for every tiny payload without a measured reason.
- Do not impose an arbitrary universal byte threshold in this baseline. Define and tune thresholds through implementation measurements and workload tests.
- Keep the storage abstraction independent of the chosen backend so a revision can be represented without changing its domain identity.

## 5. External-file consistency and safety

SQLite transactions do not include filesystem writes. External-file publication must therefore use a recoverable protocol:

1. Write to a unique temporary/staging path under the managed storage root.
2. Flush/close the file and verify expected length and content hash.
3. Atomically rename/replace into its immutable final content-addressed or revision-owned location on the same filesystem.
4. In a SQLite transaction, insert/update the payload reference and publish the corresponding domain revision.
5. On failure, leave no authoritative reference to an incomplete file; clean up unreferenced staging/final artifacts through a safe recovery or garbage-collection process.
6. On startup or explicit integrity scans, detect missing, truncated, hash-mismatched, and orphaned external files. Never silently treat them as valid.

The implementation must document crash windows and recovery behavior. It must not claim cross-resource atomicity that SQLite and the filesystem do not provide.

## 6. Settings files

Project and map settings are stored as files. SQLite stores their owner, relative path/storage key, format and schema version, and integrity metadata (at least content hash; byte length is recommended). Updates use staged writes and atomic replacement, with database metadata updated through a recoverable sequence. Settings files must be versioned and validated on load.

The project package's own manifest (for example, a package-level manifest.json describing the database path) is a separate packaging concern. This decision does not automatically remove or replace that package manifest.

## 7. Field Package and JSON/JCS

Field Package remains a transport/interchange boundary, not an internal database storage prerequisite. Its concrete V1 representation may be binary-native or use a small metadata document plus binary payloads; that format must be decided as a separate package-format specification.

The current JSON canonical manifest schema and RFC 8785 JCS implementation must not be expanded as a mandatory internal storage dependency. Before removing existing code, inspect every current consumer:

- FieldRevisionRecord.ManifestJson and PublishFieldRevisionPlan.ManifestJson.
- FieldDatasetService manifest parsing, canonicalization, and identity validation.
- Existing manifest schema/vector documents and tests.
- Dedicated Field Payload ownership, storage-kind and byte-integrity checks used by Field Revision publication.

Migrate these responsibilities into typed domain metadata and/or the explicit interchange reader/writer contract. Preserve any validation that protects field semantics, spatial ownership, dimensions, encoding, and payload integrity. Do not delete validation merely because JSON/JCS is no longer the internal representation.

The RFC 8785 canonicalizer and its package dependency may be removed only after no production internal path depends on them and any retained interchange contract has been explicitly specified and tested.

## 8. Required implementation follow-up

1. Model payload storage references explicitly, including storage kind, stable key/path, format/version, byte length, and hash.
2. Support SQLite BLOB and external-file payload implementations behind one storage abstraction.
3. Keep Field Dataset/Revision authority and publication concurrency in SQLite.
4. Replace internal manifest-string authority with typed persisted metadata; retain JSON only where an explicit interchange/debug contract requires it.
5. Implement external-file staging, verification, atomic publication, recovery, and orphan cleanup.
6. Store project/map settings as versioned files with SQLite path/hash metadata.
7. Add tests for BLOB round trips, external-file round trips, large/tiled payload streaming, path confinement, crash/recovery cases, hash mismatch, missing files, and orphan cleanup.
8. Reassess PR #11's JCS dependency and tests after the migration plan is implemented; do not remove them as a blind cleanup.

## 9. Non-goals

- This decision does not prescribe a universal Field Package binary wire format.
- It does not require all Field payloads to be BLOBs.
- It does not require all metadata in SQLite to be serialized as binary.
- It does not change the existing project package directory format or its package-level manifest without a separate decision.
- It does not authorize bypassing EF Core migrations or creating a parallel migration ledger.
