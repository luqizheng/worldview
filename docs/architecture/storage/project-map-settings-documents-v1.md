# Project / Map Settings Documents V1

**Status:** Implementation baseline for Issue #13  
**Target:** WorldView 1.0 (fresh-schema development baseline)  
**Applies to:** Windows and macOS desktop builds

## 1. Authority and scope

- SQLite is authoritative for settings-document identity, owner, version, current-version selection, relative path, schema version, byte length, SHA-256, and creation time.
- The exact UTF-8 document bytes on the package-managed filesystem are authoritative for settings content.
- A settings document is not stored as an authoritative JSON body in SQLite. SQLite stores only metadata and references.
- Project and Map settings are separate document kinds with separate owner constraints.
- Published document versions are immutable. Editing settings creates a new version; it never overwrites a published version.
- This is the V1 development baseline. There is no released historical database and no legacy migration/backfill path to preserve.
- The application is a single-machine desktop application. This contract does not introduce distributed locking, leases, or multi-writer concurrency machinery.

## 2. Stable format identifiers

The document envelope uses these exact identifiers:

| Owner | format |
|---|---|
| Project | worldview.project-settings |
| Map | worldview.map-settings |

The envelope's schemaVersion is an integer. The initial schema version is 1. It is distinct from the SQLite migration version and the package format version.

## 3. JSON document envelope

Project Settings V1:

    {
      "format": "worldview.project-settings",
      "schemaVersion": 1,
      "projectId": "canonical-lowercase-guid",
      "settings": {}
    }

Map Settings V1:

    {
      "format": "worldview.map-settings",
      "schemaVersion": 1,
      "projectId": "canonical-lowercase-guid",
      "mapId": "canonical-lowercase-guid",
      "settings": {}
    }

Rules:

1. UTF-8 without BOM; one complete JSON object; no trailing non-whitespace bytes.
2. Property names are case-sensitive and duplicate property names are rejected at every object depth.
3. format, schemaVersion, owner IDs, and settings are required. Unknown top-level properties are rejected in V1.
4. schemaVersion must equal 1. Unknown versions fail closed; the reader must not guess or silently coerce.
5. IDs must be canonical lowercase GUID strings in D format and must match the SQLite owner row.
6. settings must be a JSON object. It is intentionally an extensible payload whose concrete preference keys are introduced by the owning feature, not guessed by the persistence layer. Empty object is valid.
7. Non-finite numbers are not valid JSON. Serialization is deterministic for the same logical settings model and serializer configuration; the persisted-byte SHA-256 is calculated over the exact emitted UTF-8 bytes, not over a reserialized read model.
8. The reader validates the envelope and owner identity before exposing the settings object to callers.

## 4. Version and ownership model

Use two revision tables and one current-version reference on each owner:

- core_project_settings_revision: immutable Project Settings versions.
- core_map_settings_revision: immutable Map Settings versions.
- core_project.current_settings_revision_id: nullable FK to a revision owned by the same Project.
- core_map.current_settings_revision_id: nullable FK to a revision owned by the same Map and Project.

Each revision row contains:

- revision ID (canonical GUID text primary key);
- project ID and, for Map Settings, map ID;
- monotonically increasing positive owner-local version number;
- schema version (positive integer);
- relative path;
- hash algorithm (sha-256);
- lowercase 64-character SHA-256;
- positive byte length;
- created-at UTC Unix milliseconds.

Required relational invariants:

- Unique (project_id, version) for Project Settings.
- Unique (project_id, map_id, version) for Map Settings.
- Composite foreign keys ensure Map Settings belongs to a Map in the same Project.
- Composite current-reference foreign keys ensure a Project or Map cannot point at another owner's revision.
- Current reference is either null or references exactly one revision belonging to that owner.
- Revision rows are immutable after insert. A new save inserts a new revision and changes only the owner's current reference in one SQLite transaction.
- No separate is_current flag is stored on revision rows; the owner pointer is the single current-version authority.

A project/map may initially have no settings revision. The first save creates version 1.

## 5. Package-relative file layout

Published files use a content-addressed, immutable path:

Settings/sha256/{first-two-hex}/{next-two-hex}/{full-lowercase-sha256}.json

Example:

Settings/sha256/ab/cd/abcdef...64-hex-digits....json

Staging files are created in a dedicated sibling directory:

Settings/.staging/{random-guid}.tmp

Path rules:

- Resolve paths against the canonical package root.
- Reject rooted paths, empty paths, dot/dot-dot segments, alternate separators, and any normalized result outside the package root.
- Reject symbolic links/reparse points in every existing path component from package root to the file.
- Create staging and final directories under the package root only.
- Staging and final publication must be on the same volume/filesystem so the final rename is atomic on supported Windows/macOS filesystems.
- A content-addressed file that already exists must be revalidated by exact length and SHA-256 before reuse.

## 6. Publication protocol

1. Validate the settings model and serialize the complete envelope to UTF-8 bytes.
2. Compute byte length and SHA-256 while writing a uniquely named staging file.
3. Flush buffered data and request durable flush to the filesystem.
4. Re-read the staging file and verify exact length and SHA-256.
5. Atomically rename the staging file to its content-addressed final path. Never overwrite an existing final path; verify and reuse it if it already exists.
6. Begin one SQLite transaction.
7. Re-read the owner's current version inside the transaction; allocate current + 1 (or 1 when absent).
8. Insert the immutable revision metadata and update the owner current-revision pointer in the same transaction.
9. Commit. Only after commit may the new version be reported as current.

Failure semantics:

- Failure before final rename: clean up this operation's staging file when safe; recovery removes leftovers after restart.
- Cancellation or SQLite failure after final rename: retain the final file. It is an unreferenced recoverable orphan, not an eagerly deleted file.
- SQLite rollback must leave the prior current pointer unchanged and no partial revision row.
- Never commit a current pointer before the corresponding file has been published and verified.
- If a final content-addressed file exists but fails verification, fail closed; do not overwrite or silently repair it.

## 7. Read protocol

1. Read the current revision metadata from SQLite.
2. Validate owner IDs, schema version, hash algorithm, path confinement, and expected filename/hash agreement.
3. Open the exact referenced file without following symlinks/reparse points.
4. Verify byte length and SHA-256 before parsing or returning content.
5. Parse and validate the JSON envelope; its format, schema version, and owner IDs must match the SQLite metadata.
6. Return a typed envelope/read model only after every check succeeds.

Missing bytes, hash mismatch, malformed JSON, owner mismatch, or unsupported schema version are explicit integrity/format failures. Do not substitute defaults or silently fall back to an older revision.

## 8. Startup recovery and garbage collection

Recovery runs after the package database is open and migrated, and before the Project session reaches Ready.

1. Enumerate only the Settings staging directory and remove stale staging files.
2. Read the complete authoritative set of revision references from SQLite.
3. Verify every referenced settings file (length, hash, path, envelope, owner).
4. Enumerate canonical content-addressed files under Settings.
5. Identify files absent from the authoritative reference set as orphans.
6. Before deleting orphans, perform a fresh SQLite reference scan. Delete only files still unreferenced.
7. Never delete a file referenced by any Project or Map Settings revision, including non-current historical revisions. Historical versions remain readable.
8. Do not automatically delete old revisions in V1. Retention/pruning is a separate product decision.

A recovery failure that prevents proving reference safety fails closed: the package does not reach Ready and no content-addressed file is collected.

## 9. Failure and platform test matrix

Required tests:

- Project and Map Settings V1 serialization/deserialization round-trip.
- Stable format IDs, schema version validation, duplicate-key rejection, unknown top-level property rejection, malformed/trailing JSON rejection.
- Canonical owner GUID validation and Project/Map owner mismatch rejection.
- Empty settings object accepted; non-object settings rejected.
- Path traversal, rooted path, separator tricks, and symlink/reparse-point escape rejected.
- Exact bytes, byte length, SHA-256 and content-addressed path agree after publication.
- Existing identical file is verified and reused; conflicting/corrupt existing file fails closed.
- Cancellation before rename leaves no committed revision and staging is recoverable.
- Cancellation or DB transaction failure after rename leaves prior current reference intact and final file recoverable as an orphan.
- Successful publication inserts revision metadata and changes the current pointer atomically.
- Missing/corrupt referenced document is reported and never silently replaced.
- Recovery preserves current and historical referenced documents and collects only unreferenced files after a fresh reference scan.
- Windows and macOS Debug and Release CI.

## 10. Explicit non-goals for V1

- No cross-device synchronization or distributed conflict resolution.
- No concurrent multi-process writers, file leases, or distributed locks.
- No JSON document body stored in SQLite as a second authority.
- No automatic history pruning or settings merge engine.
- No speculative project/map preference keys in this persistence layer.
- No legacy database migration or historical settings backfill.
