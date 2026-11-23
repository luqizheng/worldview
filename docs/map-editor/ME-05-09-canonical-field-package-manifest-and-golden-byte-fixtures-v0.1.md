# World View — Canonical Field Package Manifest & Golden Byte Fixtures

- Version: 0.1
- Date: 2026-11-21
- Status: **Recommended initial-release serialization contract; canonical schema updated; binary golden packages require regeneration against the completed manifest**
- Owner: Map Engine field I/O; Core Domain retains Field Dataset, Field Revision and dedicated Field Payload authority
- Depends on: ME-05-01 V0.5; ME-05-07 V0.3; ME-05-08 V0.1; P2-09-30; P2-09-31; P2-09-32; P2-09-33

## 1. Deliverables

This package closes the design-level definition of the canonical manifest and supplies byte-exact reference fixtures for the existing WVFLDPK 1.0 envelope.

| Artifact | Purpose |
|---|---|
| P2-09-34 JSON Schema V0.1 | Strict machine-readable manifest shape |
| P2-09-35 Validation Vectors V0.1 | Positive/negative manifest and canonicalization cases |
| `fixtures/field-package-v1/*.manifest.json` | Canonical manifest sidecars; directory name denotes WVFLDPK envelope major version 1, not a dual manifest schema |
| `fixtures/field-package-v1/*.wvfld` | Raw binary golden packages |
| `fixtures/field-package-v1/golden-fixtures.json` | Lengths, SHA-256 hashes, tile hashes and fixture inventory |

The `field-package-v1` fixture directory name refers only to the WVFLDPK binary envelope major version 1.0; it does not indicate a second manifest compatibility track. The schema validates structure and basic value domains. The Field Descriptor Registry and Core Domain publication service remain responsible for descriptor compatibility, Map ownership, category semantics, coverage correctness and cross-record checks.

## 2. Canonicalization and manifest bytes

Canonical manifest serialization is **RFC 8785 JSON Canonicalization Scheme (JCS)** encoded as UTF-8:

1. Parse strict UTF-8 JSON; reject BOM, invalid UTF-8, duplicate object keys, comments, trailing commas, NaN and Infinity.
2. Validate the parsed JSON against P2-09-34 and the pinned Field Descriptor.
3. Serialize using RFC 8785 JCS. No BOM and no trailing newline are emitted.
4. Place the exact resulting byte sequence at header `manifest_offset` (72) for `manifest_length` bytes.
5. The next section begins at the 8-byte-aligned offset defined by ME-05-08; all padding bytes are zero.

All V1 property names are ASCII. JCS key ordering therefore agrees with the current ordinal-key-order requirement. Implementations must use a conforming JCS implementation or prove byte equivalence; generic dictionary iteration or culture-sensitive serialization is not acceptable.

Spatial origin and spacing values are canonical decimal **strings**, not JSON floating-point numbers. This preserves exact coordinate text and avoids platform-dependent floating-point formatting. Integer dimensions, versions, flags and category codes remain JSON integers within the safe-integer range.

## 3. Canonical manifest shape

Unknown top-level and nested properties are rejected. Required groups:

- `manifest_schema`: stable schema ID and version 1.
- `format`: package major/minor 1.0.
- `field`: field key, descriptor schema version and semantics ID pinned to P2-09-30; elevation additionally requires `unit` (`m` or `ft`).
- `spatial`: Map Space and spatial-reference identities plus `coordinate_precision_profile_id` for every field; elevation additionally requires `vertical_reference_id`. Owning Map / Dataset / Revision / Asset IDs remain Core Domain metadata, not package authority.
- `grid`: dimensions, tile dimensions, origin, spacing, axis orientation and cell registration.
- `coverage`: `full_grid` or an explicitly named coverage-mask plane. Partial coverage cannot be inferred from missing tiles.
- `map_edge_semantics`: explicit bounded/wrap/unknown behavior; never inferred from file or viewport edges.
- `value_encoding`: ordered sample planes and profile identity; categorical fields also carry stable category codes.
- `no_data`: explicit validity-mask plane with valid=0 and invalid=1. Invalid samples are not valid zero.
- `codec_registry`: codec ID 0 is `none`; every codec used by directory entries must be registered and version-pinned.
- `tile_hash`: SHA-256 version 1 over canonical decoded tile bytes.
- `canonicalization` and `byte_order`: explicit serialization and sample byte-order declarations.

### 3.1 Plane order and decoded tile length

`value_encoding.planes` is ordered. Each plane declares `name`, `sample_format` and `bytes_per_cell`. A tile's decoded bytes are the concatenation of complete planes in manifest order, each plane traversed row-major (X-fastest), with exactly `valid_width × valid_height × bytes_per_cell` bytes. No row padding or implicit alignment is permitted in decoded tile bytes.

The explicit validity mask is one uint8 value per cell and is a required plane for V1 source fields. The `no_data.mask_plane.plane_name` must resolve to exactly one plane. A reader verifies mask values are exactly 0 or 1. Tile hash covers the complete concatenated decoded representation, including the validity mask.

For `land_water`, category codes are descriptor-pinned and distinct: land, water, unknown and outside_coverage. No-data remains represented by the validity mask, not by a fifth category. For `elevation`, sample values must be finite; invalid cells are identified by the mask and their sample bytes remain included in the tile hash.

The schema permits `golden_fixture_only` profiles so reference fixtures are executable test data. This does **not** select float32 elevation or uint8 category storage as the production precision/encoding profile. Production profiles require a separate precision/codec decision and descriptor update.

## 4. Golden fixtures

Three deterministic reference packages are supplied:

| Fixture | Purpose | Package bytes | SHA-256 |
|---|---|---:|---|
| `minimal-elevation-2x2` | One uncompressed 2×2 elevation tile, includes valid zero | 1,340 | `2d62b3c2d95821d79d4cd402b68e85e454309fcf8e18b18033d06b62894a5d14` |
| `land-water-4x2-two-tiles` | Two tiles, canonical X ordering, all four registered categories | 1,488 | `593662e70b053b2dcbaff2da46d3cff61ae515a5904c95b4fccef14b8f5ed409` |
| `edge-elevation-3x2` | Non-square grid and a 1×2 right-edge tile | 1,426 | `9f082b4624dd8bf4d7cece31f1352d0731986e308ee9de2d5499d960cb9db2d3` |

The `.wvfld` binaries and the manifest lengths/hashes in the original fixture inventory were generated before the completed semantic fields were added. They are retained only as superseded format-layout reference artifacts and are not current canonical-manifest golden packages. Regenerate the binary packages and inventory through the production writer before claiming golden-package conformance. The sidecar manifests now show the current semantic shape. Fixture Map Space, Spatial Reference, vertical-reference and precision-profile IDs are synthetic; they test schema shape, not geographic correctness or production precision.

## 5. Binary envelope alignment

The golden packages use ME-05-08 V0.1 without changing its 72-byte header, 16-byte directory header or 72-byte tile entries:

- Header magic is exactly `WVFLDPK\\0`.
- Fixed-width fields are little-endian.
- Manifest starts at byte 72.
- Directory and payload sections start at the next 8-byte boundary.
- Every tile payload begins at an 8-byte boundary; all padding is zero.
- Package length equals physical file length; no trailing bytes.
- Directory entries are ordered by (tile_y, tile_x).
- Tile SHA-256 covers decoded tile bytes; the interchange package SHA-256 covers the complete exported package bytes.

## 6. Parser/writer conformance

A production reader/writer is conformant only when it:

1. Parses all three golden packages and reproduces the exact manifest bytes.
2. Recomputes every tile hash and complete interchange-package hash exactly.
3. Re-serializes each manifest to byte-identical JCS bytes.
4. Writes byte-identical packages for identical semantic input, codec version and parameters.
5. Rejects every negative case in P2-09-35 and P2-09-33.
6. Produces identical output on Windows and macOS for the same fixture inputs.
7. Reads one requested tile without decoding unrelated tile payloads.
8. Enforces resource limits and checked arithmetic before allocation or seeking.

The existing binary fixture files are superseded reference data until regenerated with the current canonical manifest. Copying them into an application does not demonstrate writer correctness.

## 7. Status boundary

**Completed:** canonical manifest semantic shape, strict JSON Schema, canonicalization profile, decoded plane byte layout and design validation vectors. The old binary golden fixtures are superseded pending regeneration.

**Still open:** regenerate binary golden packages and their SHA-256 inventory using the current manifest; production JCS library selection, production elevation precision and category encoding profile, codec/library selection, parser/writer implementation, fuzzing, performance benchmarks, and Windows/macOS execution evidence.

No parser, writer, provider or platform test pass is claimed by this design package.
