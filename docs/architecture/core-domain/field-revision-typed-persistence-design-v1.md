# Field Revision Typed Persistence Design V1

- Status: Accepted implementation design; schema generation and implementation remain pending
- Date: 2026-11-22
- Scope: Core Domain Field Revision metadata persistence and its spatial-reference boundary
- Parent: [Field Payload Storage Implementation Plan V1](field-payload-storage-implementation-plan-v1.md)

## 1. Purpose

Define the next safe persistence step after the typed Field Revision projection. The design must remove the legacy manifest from the long-term internal authority path without inventing spatial ownership relationships, losing exact numeric meaning, or bypassing the accepted EF Core migration workflow.

This document is a design contract. It does not claim that typed SQLite persistence, payload BLOB/file storage, or the required migration has been implemented.

## 2. Verified current implementation

At the current PR head:

- `core_map_field_dataset` is Map-owned and uniquely identifies one registered field key per Map.
- `core_map_field_revision` stores immutable revision identity, lineage, a generic Asset reference, content fingerprint, `manifest_json`, provenance and creation time.
- `FieldRevisionMetadata` is a Domain typed projection created from the validated manifest.
- The projection carries descriptor pins, Map Space and spatial-reference IDs, grid, coverage, edge, no-data, value profile, planes, category codes, codecs and tile-hash metadata.
- SQLite still stores `manifest_json`; reads reconstruct the typed projection from it.
- The current Core Domain EF model has no formal Map Space or Spatial Reference entity/table. The IDs are currently manifest values, not enforced relational references.
- The current schema has no dedicated Field Payload row; the revision still references `core_asset`.

The typed projection is therefore a compatibility bridge, not typed persistence.

## 3. Spatial ownership boundary

### 3.1 Keep the concepts distinct

The following identities must not be collapsed:

- **Map ID**: the owner of the Map Source Field Baseline and its Field Datasets.
- **Map Space ID**: identifies the spatial domain/frame in which the field grid is defined. It is not a viewport, working scope, zoom level, tile set, or Map Extent.
- **Spatial Reference ID**: identifies the immutable coordinate-reference definition used to interpret coordinate values. The accepted ME-05-04 / P2-09-25 contract describes this definition as project-scoped, locally available and immutable once referenced.
- **Grid metadata**: describes one Field Revision's sampling lattice within that declared spatial context. It is revision data, not the definition of the spatial reference itself.

A Map Space and a Spatial Reference are not interchangeable. Two fields may refer to the same spatial reference while having different grids. A coordinate-reference conversion must create a new immutable reference identity and new converted payload/revision evidence; it must not reinterpret existing coordinate strings in place.

### 3.2 What can be enforced now

The current codebase does not contain authoritative Map Space or Spatial Reference records. Therefore this design explicitly forbids:

- inventing a foreign key from `map_space_id` to `core_map.map_id`;
- treating `spatial_reference_id` as an EPSG code or assuming it is globally resolvable;
- creating placeholder rows from IDs found in manifests;
- inferring a Map's spatial reference from its current Field Revision;
- silently changing or normalizing either identifier during persistence.

For the first typed-persistence migration, preserve both IDs exactly as validated opaque identifiers on the immutable Field Revision. Validate their presence, length and equality against the submitted typed metadata. Enforce the existing Field Dataset → Map ownership boundary. Do not claim referential integrity to a spatial catalog that does not yet exist.

### 3.3 Follow-up owner contract

Before introducing relational foreign keys, a separate Core Domain decision must define:

1. whether Map Space has a persisted owner record, and its exact scope/lifecycle;
2. how a Map binds to one or more Map Spaces, if multiple are permitted;
3. how project-scoped immutable Spatial Reference definitions are created, versioned, retained and referenced;
4. whether the spatial-reference definition is represented by typed columns plus a bounded versioned parameter document, and how its canonical fingerprint is computed;
5. how a Map Space declares its valid coordinate frame and compatibility with a Spatial Reference;
6. how migration/backfill handles legacy identifiers whose definitions are not available locally.

Until then, the Field Revision snapshot is the authority for the IDs it used; it is not the authority for the definitions behind those IDs.

## 4. Typed relational persistence shape

### 4.1 Revision scalar metadata

The following values are immutable and belong directly to the Field Revision row because they describe the exact interpretation of that revision and are read together:

- field key, descriptor schema version and semantics ID;
- Map Space ID and Spatial Reference ID;
- grid width/height and tile width/height;
- origin X/Y and spacing X/Y as canonical decimal **TEXT**, not SQLite REAL;
- axis orientation and cell registration;
- coverage mode and optional mask-plane name;
- Map edge semantics;
- no-data policy, mask-plane name, valid value and invalid value;
- value profile ID and profile scope;
- tile hash algorithm and version.

The existing Dataset descriptor pin remains authoritative for Dataset compatibility. The Revision also stores its descriptor snapshot so that an old immutable revision remains independently interpretable and auditable.

### 4.2 Ordered and repeatable metadata

Use child tables rather than opaque JSON arrays for repeatable semantic records:

- `core_map_field_revision_plane`: one row per plane, keyed by revision + ordinal; stores name, sample format and bytes per cell.
- `core_map_field_revision_category_code`: one row per category key; stores category key and integer code.
- `core_map_field_revision_codec`: one row per codec, keyed by revision + ordinal; stores codec ID, name and version.

Codec `parameters` may remain a bounded, validated JSON extension value in V1 because its internal parameter vocabulary is codec-specific and not currently queryable by Core Domain. It must not be used to hide core Field semantics. Its schema/version and canonicalization requirements must be explicit.

Every child row has a restrictive foreign key to its owning revision. Ordinals are stable and unique within a revision. Category keys are unique within a revision. The write transaction inserts the revision and all child rows together.

### 4.3 Values that do not belong in this relational model

- Package envelope values such as manifest format major/minor, manifest schema identifier, byte order and JCS canonicalization are interchange/package concerns, not additional Field Dataset authority columns.
- `ProvenanceJson` remains a separate provenance/audit value; it is not a substitute for typed Field metadata.
- Payload bytes and payload storage location belong to the dedicated Field Payload model in the parent implementation plan, not to these metadata child tables.
- Renderer indexes, decoded tiles and other disposable caches do not belong in Core Domain tables.

## 5. Manifest V1/V2 semantic reconciliation

The audit confirmed an existing conformance gap, not merely a future persistence concern:

- P2-09-30 requires elevation unit (`m` or `ft`) and stable `vertical_reference_id`.
- P2-09-31 FDV-004 and FDV-005 expect rejection when either is missing.
- P2-09-34 Manifest V1 has no fields for either value, and the current publication validator therefore cannot enforce those vectors.
- ME-05-04 defines a versioned precision profile per Spatial Reference. The Field manifest must pin the selected coordinate precision profile so a revision remains interpretable against the immutable definition.
- P2-09-30 requires resolution and grid alignment semantics. The current grid properties already carry these semantics, but the registry/vector wording had not mapped the concepts to concrete fields.

### 5.1 V2 manifest contract authored

Two new artifacts define the proposed compatible V2 shape and validation cases:

- [P2-09-36 Field Package Canonical Manifest Schema V0.2](p2-09-36-field-package-canonical-manifest-schema-v0.2.json)
- [P2-09-37 Field Package Canonical Manifest V2 Validation Vectors V0.2](p2-09-37-field-package-canonical-manifest-v2-validation-vectors-v0.2.json)

V2 adds these explicit semantics:

- `field.unit`: required for elevation (`m` or `ft`); omitted for categorical `land_water`.
- `spatial.vertical_reference_id`: required for elevation; a distinct stable identity, never inferred from horizontal CRS, unit or source file.
- `spatial.coordinate_precision_profile_id`: required for all fields; pins the coordinate quantization profile selected by the immutable Spatial Reference definition.
- `value_encoding.profile_id` remains the sample representation/encoding profile. It is not the coordinate precision profile.
- Resolution is represented by `grid.spacing_x`, `grid.spacing_y`, `grid.width` and `grid.height`. Tile dimensions describe package tiling, not semantic resolution.
- Grid alignment is represented by `grid.origin_x`, `grid.origin_y`, `grid.axis_orientation` and `grid.cell_registration`. These exact fields are not duplicated into a second redundant alignment object.

The WVFLDPK binary envelope remains format 1.0. Its version is independent from `manifest_schema.version`; V2 does not change the binary header, tile layout or existing golden package bytes.

### 5.2 Compatibility and publication policy

- Existing Manifest V1 revisions remain readable as legacy records.
- V1 is not accepted for new formal elevation publication after the V2 validator is implemented.
- New formal publication uses Manifest V2.
- V1 backfill must not fabricate unit, vertical reference or coordinate precision profile. Such revisions remain explicitly marked as legacy/incomplete for those semantics.
- V2 IDs are only syntactically validated until authoritative Spatial Reference / Vertical Reference records and lookup services exist. Production publication must eventually resolve them to immutable locally available definitions; a non-empty string alone is not proof of a valid definition.
- The current golden fixtures remain V1 byte-exact references. New V2 package fixtures must be added separately after the package writer is implemented; existing fixture hashes must not be silently rewritten.

### 5.3 Remaining implementation gate

The schema and vectors are design artifacts, not executable validation. The next code slice must:

1. Add an explicit manifest-version dispatch: V1 legacy read path versus V2 new-publication path.
2. Enforce V2 unit, vertical-reference and coordinate-precision-profile requirements in Domain validation.
3. Preserve these fields in the typed projection and later relational persistence.
4. Define immutable Spatial Reference and Vertical Reference owner records and local resolution before enabling production publication.
5. Execute V2 validation vectors in tests and keep V1 golden fixtures byte-identical.

No missing value is synthesized, and no spatial foreign key is introduced before the owner contract exists.

## 6. Backfill and dual-read rules

1. Read every existing manifest through the existing strict validator before projection.
2. Verify Dataset descriptor pins match the Revision snapshot.
3. Project every value without lossy numeric conversion. Decimal grid values remain exact strings.
4. Insert the revision scalar fields and all child rows in one transaction per revision or one bounded migration transaction appropriate to the package size.
5. Reject malformed, inconsistent, duplicate or unsupported values. Do not repair silently.
6. Preserve the original manifest during the transition for rollback, diagnostics and interchange compatibility.
7. Verify a typed read produces the same semantic projection as the validated manifest.
8. Only after all readers/writers and package interchange consumers have migrated may the internal manifest column be retired.

If a manifest is valid under its current schema but lacks a later-required semantic field, preserve that field as explicitly unavailable under the legacy schema version. Do not pretend the row satisfies the newer semantic contract.

## 7. Payload relationship

The accepted storage baseline supersedes the older assumption that every Field Revision payload must be a generic Core Asset:

- one immutable Field Revision owns one logical immutable Field Payload;
- the payload may be raw SQLite BLOB bytes or a managed external file;
- payload metadata and the owner relationship are stored in the dedicated Field Payload model;
- the payload storage choice does not change Field Dataset or Revision identity;
- the generic `core_asset` model remains for generic project assets and existing typed Asset consumers.

During transition, the current Asset reference remains intact until the new payload publication path, backfill, integrity checks and orphan handling are proven. Do not create both a generic Asset payload and a Field Payload as two competing authorities for new revisions.

## 8. EF Core migration requirements

The next schema change must be generated using the real EF Core CLI workflow:

```bash
dotnet ef migrations add FieldRevisionTypedMetadata   --project src/WorldView.Infrastructure   --startup-project src/WorldView.Infrastructure   --context CoreDomainDbContext
```

The exact command path must be checked against the repository's solution working directory and existing migration documentation before execution.

- Do not edit the generated migration or `CoreDomainDbContextModelSnapshot` by hand.
- Do not add a second migration ledger.
- Do not alter the initial migration.
- Do not use `EnsureCreated`, suppress pending-model warnings, or skip migration checks.
- Update runtime model configuration, generated migration and generated snapshot as one schema change.
- Include migration/backfill tests against real temporary SQLite databases.
- The current available GitHub workflow can validate code on Windows/macOS, but it does not prove that the developer EF CLI was run correctly. If no environment with the pinned .NET SDK and EF CLI is available to execute the command, stop before changing the EF schema and request that execution environment rather than fabricating generated artifacts.

## 9. Required acceptance tests

- Typed metadata write/read round trip without consulting `manifest_json`.
- Exact preservation of decimal origin/spacing strings, including lexical precision.
- Plane and codec ordering survives persistence.
- Category-code key/value uniqueness and descriptor-specific validation.
- Invalid enum, range, required-field and cross-field combinations are rejected.
- Map Space and Spatial Reference IDs are preserved exactly and are not accidentally joined to Map ID or EPSG aliases.
- Cross-Project or wrong-Map Dataset ownership is rejected.
- Legacy manifest backfill is deterministic and idempotent.
- Malformed or semantically inconsistent legacy rows fail with an actionable diagnostic and do not partially publish.
- Legacy rows lacking unit/vertical/precision fields remain explicitly incomplete; no fabricated values appear.
- Revision metadata and its payload reference publish atomically with the Dataset current-revision compare-and-swap.
- Windows and macOS produce equivalent typed values and hashes for the same canonical source.

## 10. Next implementation order

1. Reconcile the ME-05-01 / P2-09-34 semantic gap and define the next manifest schema/validation vectors.
2. Define the Map Space and Spatial Reference owner records and their project/map scope; only then add relational foreign keys.
3. Add typed revision scalar/child persistence and generate the EF migration with the real CLI.
4. Backfill existing rows and prove typed reads no longer depend on `manifest_json`.
5. Implement the dedicated BLOB / managed-file Field Payload adapter and publication/recovery protocol.
6. Migrate all production consumers, then retire the legacy manifest/JCS internal path where no interchange contract requires it.

No universal BLOB/file threshold is introduced here. The application remains a single-user desktop application on Windows and macOS; no distributed storage or multi-node coordination is implied.
