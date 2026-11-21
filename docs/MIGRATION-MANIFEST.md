# World View Documentation Migration Manifest

Last verified against `master`: 2026-11-21.

## Scope scanned

The migration review covered the two World View Library roots:

- `/world-view-rs` — Core Domain, ADRs, P2-01-D01, map rendering, editor, and architecture records.
- `/World View/Map Generation` — Map Generation, AI Assistant, persistence/schema, implementation handoff, validation artifacts, and versioned project README snapshots.

The repository also retains the pre-existing ADR and P2-01-D01 packages.

## Repository inventory

At the last verification, the repository contained 134 files in total, including 133 files under `docs/`.

| Area | Directory | Files |
|---|---|---:|
| Architecture decisions | `docs/adr/` | 6 |
| P2-01-D01 discussion | `docs/P2-01-D01/` | 21 |
| AI Assistant | `docs/ai-assistant/` | 24 |
| Core Domain and persistence | `docs/architecture/core-domain/` | 46 |
| Map Editor | `docs/map-editor/` | 6 |
| Map Engine | `docs/map-engine/` | 6 |
| Map Generation | `docs/map-generation/` | 6 |
| Historical reviews and artifacts | `docs/history/` | 7 |
| Baseline snapshots | `docs/baseline/` | 2 |

Counts are a snapshot and may change as documents are added or reorganized.

## Migration policy

- Current or materially relevant design documents are placed in their domain directories.
- Older review records and legacy SQL/Python artifacts are retained under `docs/history/`; they are not authoritative implementation instructions.
- Latest source README snapshots are retained under `docs/baseline/`; this repository's `docs/README.md` remains the navigation index.
- Superseded README snapshots are not copied wholesale. They are repetitive historical snapshots, not independent design authorities.
- Duplicate source copies are consolidated to one repository path.
- File names and version numbers are preserved where practical. A file's status must be read from its own metadata; repository presence does not mean Accepted/Frozen.

## Not yet migrated / limitations

Two binary ZIP archives are present in the source Library:

- `world-view-map-generation-v0.1.zip`
- `world-view-p2-09-next-documents-v1.39.zip` (and the earlier `world-view-p2-09-next-documents.zip`)

The file service did not provide an authorized raw-byte export path for these archives during this migration, so they were not copied into GitHub. Their contents have not been assumed to be fully represented by the extracted Markdown files. If their contents are needed, provide the ZIP files as conversation uploads or another accessible raw-file source so they can be inspected and reconciled.

The Map Generation Library also contains many historical README snapshots (V1.0 through V1.37, with gaps), which are intentionally not all duplicated in this repository. The latest source baseline README V1.38 and the Map Generation README V1.37 snapshot are preserved.

## Important validation boundary

This migration is a documentation consolidation, not an implementation activity. It does not claim that:

- any code has been implemented or built;
- the SQLite provider transaction proof tests have passed;
- any schema proposal is executable DDL;
- any proposal or review document has become an accepted ADR;
- all binary archives or every historical Library revision have been mirrored.

For navigation, see [Documentation Index](README.md).


## 2026-11-21 path and identity audit

A targeted audit found three repository path/content mismatches in the P2-09 area. The physical schema and integrated inventory files have been restored from their matching Library source documents. A duplicate path that contained the README V1.38 baseline rather than a P2-09-14 contract was removed; the authoritative README snapshot remains at `docs/baseline/README-v1.38.md`. No original standalone P2-09-14 source artifact was found. A new P2-09-14 V0.1 validation contract has since been authored in this repository from P2-09-05 V0.3 and the current persistence baseline; it is explicitly marked as newly authored, not recovered from the Library. The removed mislabeled path must not be treated as source evidence.
