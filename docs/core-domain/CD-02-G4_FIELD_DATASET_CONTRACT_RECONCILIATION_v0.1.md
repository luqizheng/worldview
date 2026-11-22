# CD-02-G4 — Field Dataset Contract Reconciliation and Implementation Readiness

- Version: 0.1
- Date: 2026-11-22
- Status: Repository-grounded design gap assessment; no Field Dataset runtime implementation
- Scope: ME-05-01 Field Dataset authority, P2-09-30/31 descriptor and validation artifacts, P2-09-34/35 canonical manifest, ME-05-07/08 package contracts, current Core Domain persistence

## 1. Executive conclusion

The Field Dataset authority model is sufficiently defined to guide persistence, but the current manifest and descriptor artifacts are not yet mutually consistent enough for a complete Field Revision publication implementation.

The most important mismatch is semantic, not merely missing code:

- The adopted Field Dataset contract and Field Descriptor Registry require elevation `unit` and `vertical_reference_id`.
- The current canonical Field Package manifest schema and both golden manifests contain neither field.
- The canonical manifest schema also omits source/derivation provenance, precision profile, tile manifest and content fingerprint fields that ME-05-01 V0.5 describes as manifest content. Some of these may intentionally live in the Field Revision's separate `provenance_json`, descriptor/profile registry or binary tile directory, but the ownership boundary is not explicit.

A second implementation blocker is Map Space authority: the Field Package manifest carries `spatial.map_space_id`, but the active Core Domain Map model has no Map Space identity or compatibility registry. The current Asset byte verification proves only that stored bytes match their SHA-256; it does not prove that a Field Package binary envelope, canonical manifest and tile directory conform to ME-05-08/P2-09-36.

**Recommendation:** reconcile the Field Descriptor ↔ canonical manifest ↔ Field Revision metadata contract first, then implement the Field Dataset/Revision tables and CAS publication. Do not create a partial publisher that silently drops unit, vertical-reference, provenance or Map Space semantics.

## 2. Artifacts inspected

### Domain and physical authority

- `docs/map-editor/world-view-me-05-01-field-dataset-authority-and-persistence-contract-v0.5.md`
- `docs/architecture/core-domain/p2-09-11-physical-schema-design-v0.11.md`
- `docs/architecture/core-domain/p2-09-13-integrated-schema-inventory-migration-graph-v0.7.md`

### Descriptor and validation artifacts

- `docs/architecture/core-domain/p2-09-30-v1-field-descriptor-registry-v0.1.json`
- `docs/architecture/core-domain/p2-09-31-field-dataset-validation-vectors-v0.1.json`

### Package and canonical manifest artifacts

- `docs/map-editor/world-view-me-05-07-field-payload-package-and-tiling-contract-v0.5.md`
- `docs/map-editor/world-view-me-05-08-field-package-binary-envelope-and-directory-specification-v0.3.md`
- `docs/map-editor/ME-05-09-canonical-field-package-manifest-and-golden-byte-fixtures-v0.1.md`
- `docs/architecture/core-domain/p2-09-34-field-package-canonical-manifest-schema-v0.1.json`
- `docs/architecture/core-domain/p2-09-35-field-package-canonical-manifest-validation-vectors-v0.1.json`
- `docs/architecture/core-domain/p2-09-36-field-package-parser-writer-conformance-vectors-v0.1.json`
- Golden manifests:
  - `docs/architecture/core-domain/fixtures/field-package-v1/minimal-elevation-2x2.manifest.json`
  - `docs/architecture/core-domain/fixtures/field-package-v1/land-water-4x2-two-tiles.manifest.json`

### Current implementation inventory

- Core Domain currently has 16 tables across 10 migrations at the G2 baseline.
- No `core_map_field_dataset` or `core_map_field_revision` table/migration exists.
- No Field Dataset/Revision Domain records, service, persistence port or SQLite adapter exists.
- `MapRecord` contains Map identity/name/lifecycle data but no Map Space ID.
- `FindOrphanAssetIds` currently considers the Core Object Definition Asset-link table; it does not yet account for Field Revision Asset references.
- No Checkpoint persistence/fingerprint integration exists.
- G2 Asset verification reads immutable Asset bytes and verifies the stored SHA-256/length; it does not parse the Field Package envelope or tile directory.

## 3. Concrete contract mismatches

### 3.1 Elevation unit and vertical reference are required by the descriptor, absent from the canonical manifest

P2-09-30 V0.1 declares:

- `elevation.unit.required = true`, allowed values `m` and `ft`;
- `elevation.vertical_reference.required = true`.

P2-09-31 FDV-004 and FDV-005 explicitly require missing unit and missing vertical reference to be rejected.

However, P2-09-34 V0.1's strict manifest schema has no `unit` or `vertical_reference_id` property in the `field`, `spatial` or `value_encoding` objects. The two ME-05-09 golden manifests also omit them. The schema's `additionalProperties: false` means these fields cannot simply be added by an implementation without a schema revision.

**Recommendation:** resolve this with one explicit authority, not an implicit profile convention. Preferred option is to add `unit` and `vertical_reference_id` to the `field` object in a new canonical manifest schema version, with conditional rules:
- `elevation`: unit required (`m` or `ft`); vertical reference required as a stable non-empty ID.
- `land_water`: both prohibited.

If vertical reference is intended to be owned by Map Space rather than the Field Package, then revise ME-05-01 and P2-09-30 so the descriptor no longer claims a manifest-level vertical-reference field, and make FDV-005 validate the target Map Space's vertical-reference authority instead. In either case, update the golden manifests, canonical bytes, package fixtures and MNF/FDV vectors in the same revision. Do not hide these semantics inside a fixture-only `value_encoding.profile_id`.

### 3.2 Provenance ownership is ambiguous

ME-05-01 V0.5 describes source and derivation provenance as part of the Field Revision manifest and also defines a separate `provenance_json` persistence column.

P2-09-34 V0.1 canonical package manifest contains neither `source_provenance` nor `derivation_provenance`. P2-09-11 V0.11 has a separate Field Revision `provenance_json` column.

**Recommendation:** keep package manifest focused on deterministic field interpretation and package layout; store immutable source/derivation provenance in `core_map_field_revision.provenance_json`. Update ME-05-01 to state this split explicitly and define one strict provenance schema:
- source kind: authored, imported, or adopted-derived;
- adopted-derived requires source artifact ID + hash and transformation parameters;
- a derived artifact is not a Field Dataset until explicitly adopted and published.

### 3.3 Precision profile and value type are represented indirectly

ME-05-01 describes explicit `value_type` and `precision_profile`. P2-09-34 instead uses `value_encoding.profile_id`, `profile_scope`, and one or more named planes with sample formats/bytes per cell. The Field Descriptor Registry defines value semantics independently.

**Recommendation:** make the descriptor + pinned `value_encoding.profile_id` the authority for value type and precision. Define a versioned Value Encoding Profile Registry with explicit unit, vertical-reference, numeric range, precision and plane contracts. Do not duplicate loosely synchronized `value_type` and `precision_profile` strings in a second manifest location.

### 3.4 Tile manifest and content fingerprint boundaries need one definition

ME-05-01 describes `tile_manifest` and `content_fingerprint` in the Field Revision manifest. ME-05-07/08 and P2-09-34 model the tile directory as a binary package section and the canonical JSON manifest as a separate section. P2-09-34 has no `tile_manifest` or `content_fingerprint` root property.

**Recommendation:**
- The binary tile directory is the authoritative tile manifest; do not duplicate it as a JSON root property.
- Store the exact canonical manifest JSON in `core_map_field_revision.manifest_json`.
- Store source/derivation provenance in `provenance_json`.
- Define a versioned semantic content fingerprint over the canonical manifest plus canonical tile identity/hash records and the pinned field descriptor/semantics. Keep this separate from the whole-package Asset SHA-256.
- Add explicit golden fingerprint vectors before implementation. The exact composition algorithm is currently unspecified and must not be guessed in production code.

### 3.5 Map Space identity and compatibility are not implemented

The canonical manifest requires `spatial.map_space_id` and `spatial.spatial_reference_id`. The active Core Domain `MapRecord` has no Map Space identity, and no Map Space registry/entity was found in the current source tree.

FDV-008 requires rejecting a spatial reference incompatible with the target Map Space. The current code has no authority to make that decision.

**Recommendation:** either:
1. introduce a Map Space identity/compatibility authority before enabling Field Dataset publication; or
2. explicitly define Map Space IDs as opaque, externally validated IDs and require a versioned Map Engine validation evidence record at publication.

Do not claim FDV-008 passes by merely checking that the two strings are non-empty.

### 3.6 Existing JSON validation/canonicalization primitives are not yet compatible

The current Domain `JsonSchemaSubset` supports `allOf`, `oneOf`, `$defs` and `$ref`, but its supported-keyword set does not include `if`, `then` or `not`. P2-09-34 V0.1 uses those keywords for field-specific conditional rules (including the land/water category-code requirement and the elevation prohibition). Passing the current schema directly to `JsonSchemaSubset` therefore fails closed before manifest validation can run.

The existing `CanonicalJson` utility is a deterministic Core Domain serializer, but it has not been proven RFC 8785 JCS-compatible. P2-09-34 and ME-05-09 explicitly require `RFC8785-JCS-UTF8`; property sorting alone is not sufficient. Exact string escaping, IEEE-754 number formatting, duplicate-key rejection, UTF-8/BOM handling and no-trailing-newline behavior must match the golden fixtures.

**Recommendation:** either extend `JsonSchemaSubset` with standards-correct `if/then/else/not` semantics and add isolated tests, or introduce a dedicated Field Package Manifest validator. Implement a JCS canonicalizer and prove byte-for-byte equality against the ME-05-09 golden manifests before using it for Field Revision persistence.

### 3.7 Package integrity is not package conformance

G2 verifies that Asset bytes match the registered SHA-256 and byte length. It does not prove:
- the WVFLDPK binary header is valid;
- the canonical manifest section is the manifest being persisted;
- the directory bounds, offsets, tile coordinates and hashes are valid;
- every tile payload hash matches the directory;
- the package conforms to the golden byte fixtures.

These are ME-05-08 / P2-09-36 parser-writer responsibilities. A Field Revision must not be formally published from byte-hash verification alone.

### 3.8 Parser/writer implementation contract is referenced but absent

P2-09-36 V0.1 declares its contract as `ME-05-10 Field Package Parser / Writer Implementation Contract V0.1`, but no ME-05-10 file is present in the current repository tree. ME-05-09 V0.1 explicitly leaves production JCS library selection, production precision/codec profiles and parser/writer implementation open.

The current package artifacts therefore provide a schema, golden fixtures and conformance vectors, but not the complete implementation contract needed to choose resource limits, stream ownership, temporary-file publication, cancellation behavior, codec registration and cross-platform error semantics.

There is also a stale dependency chain: ME-05-09 V0.1 declares dependencies on ME-05-07 V0.3 and ME-05-08 V0.1, while the repository's latest package/tiling and binary-envelope documents are ME-05-07 V0.5 and ME-05-08 V0.3. The golden fixture contract must be explicitly reconciled against those newer versions before a parser can be considered conformant.


**Recommendation:** author ME-05-10 V0.1 as a real repository artifact before implementing the production reader/writer. It must define parser/writer APIs, JCS implementation selection, resource limits, decoded-tile memory budget, temporary output ownership, cancellation/cleanup, codec registry behavior and stable error codes. Then execute P2-09-35 and P2-09-36 against the golden packages.

## 4. Persistence model that is ready to implement

The physical schema already gives a bounded two-table shape:

### `core_map_field_dataset`

- stable dataset identity;
- Project + Map ownership;
- one unique row per Map + built-in field key;
- pinned descriptor schema version and semantics ID;
- current Field Revision pointer;
- created/updated timestamps.

### `core_map_field_revision`

- immutable revision identity and monotonically increasing revision number;
- same-dataset parent revision reference;
- same-Project immutable Core Asset reference;
- semantic content fingerprint;
- canonical manifest JSON and provenance JSON;
- immutable creation timestamp.

The implementation should use one SQLite transaction for:
1. re-reading Map and Asset metadata;
2. verifying the Asset is `Verified` and its hash matches the package evidence;
3. inserting the immutable Field Revision;
4. creating the Dataset on first publication or compare-and-swapping its current pointer;
5. committing both together.

The current revision pointer must never be committed as null. Revision rows must reject UPDATE/DELETE. The Asset orphan scan must treat every Field Revision Asset reference as live. No Branch, Event, Command, State Change or Initial State table is needed for this authority.

## 5. Implementable now vs blocked

| Area | Readiness | Reason |
|---|---|---|
| Dataset uniqueness and Map ownership | Ready | ME-05-01 and P2-09-11 define the two-table identity model. |
| Revision immutability and monotonic numbering | Ready | Immutable revision + parent pointer + unique revision number are explicit. |
| Current pointer CAS | Ready | Explicit expected-current revision contract; SQLite transaction can enforce it. |
| Same-Project Asset FK and Verified-state recheck | Ready | Core Asset metadata and G2 verification path exist. |
| Asset orphan retention | Ready | Extend existing orphan query to include Field Revision references. |
| Descriptor pinning | Ready | P2-09-30 has two V1 descriptors and explicit schema/semantics IDs. |
| Canonical manifest validation | Ready after schema reconciliation | P2-09-34 schema and P2-09-35 vectors exist, but unit/vertical-reference conflict must be fixed first. |
| Source/derivation provenance | Ready after ownership split | Separate `provenance_json` column exists; schema needs to be finalized. |
| Map Space compatibility | Blocked | No Map Space authority or validation evidence exists. |
| Binary package conformance | Blocked | ME-05-08/P2-09-36 parser-writer implementation is absent. |
| Semantic content fingerprint | Blocked pending exact algorithm | No golden fingerprint composition vectors exist. |
| Checkpoint invalidation/reuse | Blocked | No Checkpoint persistence or dependency fingerprint integration exists. |

## 6. Recommended execution sequence

1. **Contract reconciliation:** revise ME-05-01 and P2-09-34 so descriptor, manifest, provenance and binary tile-directory responsibilities are unambiguous. Add golden manifest/fingerprint vectors.
2. **Map Space authority:** define Map Space identity and spatial-reference compatibility evidence.
3. **Package reader/validator:** implement ME-05-08/P2-09-36, using G2 Asset byte verification plus binary/header/directory/tile hash validation.
4. **Core Domain persistence:** add the two Field Dataset tables, immutable revision triggers, CAS service, same-Project Asset FK and orphan retention.
5. **Application publication use case:** verify package bytes and manifest with the reader, then call the short Core Domain transaction.
6. **Checkpoint integration:** include current Field Revision ID, Asset hash, descriptor version and semantic content fingerprint in the Map Field Baseline fingerprint; invalidate only dependent artifacts.

## 7. Stop conditions

- Do not persist a Field Revision that lacks a required elevation unit or vertical reference.
- Do not treat Asset SHA-256 verification as Field Package format validation.
- Do not infer Map Space compatibility from a non-empty string.
- Do not invent a semantic content-fingerprint algorithm without golden vectors.
- Do not introduce Branch-local Field overrides or model Field Revision as Command/Event/Initial State.
- Do not claim TX-65–71 complete from the existence of Asset byte verification alone.

## 8. Status

This document is an implementation-readiness assessment. It adds no runtime behavior, no tables and no test claims. It prevents the next Field Dataset implementation from encoding inconsistent manifest semantics while preserving the already-defined two-table Core Domain persistence path.
