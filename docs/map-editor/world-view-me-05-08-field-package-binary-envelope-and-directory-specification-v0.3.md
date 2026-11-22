# World View — ME-05-08 Field Package Binary Envelope & Directory Specification

- Version: 0.3
- Date: 2026-11-21
- Status: Recommended V1 byte-level envelope; canonical manifest and baseline golden fixtures supplied; production parser/writer and platform proof remain open
- Owner: Map Engine field I/O; Core Domain retains Field Revision and Core Asset authority
- Depends on: ME-05-01 V0.5; ME-05-05 V0.3; ME-05-07 V0.5; P2-09-11 V0.11; P2-09-12 V0.6; P2-09-32 V0.1; P2-09-34 V0.1; P2-09-35 V0.1; ME-05-09 V0.1; ME-05-10 V0.1; P2-09-36 V0.1

## 1. Purpose

ME-05-07 establishes one seekable immutable package Asset per Field Revision. This document specifies a small byte-level V1 envelope so a reader can locate and validate one tile without decoding unrelated tiles.

The format is a World View-owned binary container, not a GIS interchange format. It does not select a production compression codec, elevation numeric width, or a raster/GIS library.

## 2. V1 package layout

A package contains four ordered regions:

1. Fixed-size header.
2. Canonical UTF-8 JSON manifest.
3. Fixed-schema tile directory.
4. Tile payload blocks.

All offsets are absolute byte offsets from the beginning of the package. All fixed-width integers in the header and directory are unsigned unless explicitly stated, and are encoded little-endian. The package is a single file whose declared package length must equal the physical file length.

### 2.1 Fixed header — 72 bytes

| Offset | Width | Field | V1 rule |
|---:|---:|---|---|
| 0 | 8 | magic | ASCII bytes WVFLDPK followed by one zero byte |
| 8 | 2 | format_major | 1 |
| 10 | 2 | format_minor | 0 |
| 12 | 4 | header_length | 72 |
| 16 | 4 | header_flags | 0; unknown nonzero bits rejected |
| 20 | 4 | reserved | Must be zero |
| 24 | 8 | manifest_offset | 72 |
| 32 | 8 | manifest_length | Positive byte length |
| 40 | 8 | directory_offset | 8-byte aligned; after manifest |
| 48 | 8 | directory_length | Must match directory header and entry count |
| 56 | 8 | payload_offset | 8-byte aligned; after directory |
| 64 | 8 | package_length | Exact physical file length |

The header has no embedded self-hash. The complete Core Asset content hash covers the header and every subsequent byte. This avoids a self-referential hash field.

### 2.2 Canonical manifest

The manifest is UTF-8 JSON and is not null-terminated. It uses canonical key ordering, invariant numeric serialization, no duplicate keys, and no NaN/Infinity tokens. The initial application release has one canonical manifest schema; the `manifest_schema.version` value identifies that single current contract and does not introduce a legacy/new compatibility split.

Required logical groups:

- format identity and manifest schema version;
- Field Descriptor key, schema version and semantics ID;
- Map Space and spatial-reference identity plus `coordinate_precision_profile_id` for all fields; elevation additionally requires `vertical_reference_id`;
- grid dimensions, origin, spacing, axis orientation and cell registration;
- coverage and Map edge semantics;
- tile width/height and expected tile count;
- value encoding, units, vertical reference and no-data rules;
- codec profile registry, including codec ID 0 for none;
- tile-hash algorithm identity/version;
- canonicalization and byte-order declarations.

Field Dataset ID, Field Revision ID and Core Asset ID remain authoritative in Core Domain owner metadata. They may be repeated as diagnostic manifest fields only if the reader verifies equality; they do not become package-owned identities. The package hash is stored in Core Asset metadata and must not be embedded in the manifest because that would be self-referential.

The manifest does not contain tile byte offsets. Those belong only to the tile directory.

### 2.3 Directory header — 16 bytes

| Offset within directory | Width | Field | V1 rule |
|---:|---:|---|---|
| 0 | 2 | directory_version | 1 |
| 2 | 2 | directory_flags | 0 |
| 4 | 4 | entry_size | 72 |
| 8 | 8 | entry_count | Exact number of tile entries |

The header directory_length must equal 16 + entry_count × 72, using checked arithmetic. It must not be trusted before overflow checks.

### 2.4 Tile directory entry — 72 bytes

Entries are sorted by tile_y ascending, then tile_x ascending. V1 has one native grid level, so no level field is needed.

| Offset within entry | Width | Field | Meaning |
|---:|---:|---|---|
| 0 | 4 | tile_x | Zero-based tile column |
| 4 | 4 | tile_y | Zero-based tile row |
| 8 | 2 | valid_width | Valid cell columns, including edge-tile size |
| 10 | 2 | valid_height | Valid cell rows, including edge-tile size |
| 12 | 2 | codec_id | Index into manifest codec registry; 0 = none |
| 14 | 2 | entry_flags | 0 in V1; unknown bits rejected |
| 16 | 8 | payload_offset | Absolute byte offset |
| 24 | 8 | stored_length | Encoded payload length |
| 32 | 8 | decoded_length | Canonical uncompressed tile byte length |
| 40 | 32 | tile_hash | Hash of canonical uncompressed tile bytes |

The 72-byte entry deliberately omits a resolution-level field because V1 packages contain one authoritative native grid. A future multi-level package requires a new format version or an explicitly versioned directory extension; readers must not infer extra levels.

## 3. Section placement and alignment

- Header begins at byte 0 and occupies bytes 0–71.
- Manifest begins at byte 72.
- Directory begins at the first 8-byte-aligned offset at or after the end of the manifest.
- Payload begins at the first 8-byte-aligned offset at or after the end of the directory.
- Each tile payload begins at an 8-byte-aligned offset.
- Any alignment padding is zero-filled and included in the complete package Asset hash.
- Tile payload blocks must not overlap. Their ranges must lie entirely within the payload region and package length.
- The writer emits payload blocks in the same canonical order as directory entries. Readers validate non-overlap and bounds, not trust writer intent.
- No trailing bytes are permitted after package_length.

## 4. Tile payload encoding boundary

The directory describes where a tile is stored and how it is decoded; the Field Descriptor and manifest define the logical sample encoding.

- Tile samples use the manifest-declared canonical byte order. V1 numeric binary samples use little-endian.
- Cell order is row-major, X-fastest.
- Edge tiles declare valid_width and valid_height; padding is not sample data.
- decoded_length must equal the exact size derived from valid dimensions and the manifest-declared sample/mask layout.
- Elevation no-data representation is explicit and distinct from valid zero. The exact numeric width/quantization profile remains a descriptor/precision decision.
- Land/water category codes are stable descriptor-defined integers. No-data remains distinct from land, water, unknown and outside_coverage.
- Tile hash covers the complete canonical decoded tile representation, including any masks required by the manifest, in a fixed declared plane order.
- Codec 0 means the stored bytes are exactly the canonical decoded tile bytes, so stored_length must equal decoded_length.
- Other codec IDs resolve through the manifest registry. Codec version and deterministic parameters are pinned there; unsupported required codecs are rejected.

## 5. Reader validation order

A reader must validate before exposing tile data:

1. Read exactly 72 header bytes; reject short reads.
2. Validate magic, major/minor version, header length, flags and reserved bytes.
3. Check package_length against the actual file length.
4. Validate manifest offset/length and directory offset/length using checked arithmetic and section-order constraints.
5. Read and parse the manifest under an explicit maximum manifest-size budget; reject non-canonical or semantically invalid content.
6. Validate directory header, entry size, entry count and exact directory length.
7. Validate canonical tile ordering, unique coordinates, expected grid coverage, edge dimensions and codec references.
8. Validate every payload range, alignment, non-overlap and decoded-length bound before allocating decode buffers.
9. On tile read, decode only the requested tile and verify tile_hash before returning samples.
10. At publication/import integrity verification, stream the complete package and compare its exact byte hash with the Core Asset content hash.

Integer overflow, truncation, invalid ranges, decompression-size excess, unknown required flags or incompatible codecs are typed package errors. They must never cause a fallback value to be returned.

## 6. Writer and immutable publication

- The writer first creates a temporary package and streams the manifest, directory and tile blocks in canonical order.
- It computes tile hashes over canonical decoded tile bytes and the Core Asset hash over the complete final package bytes.
- It writes zero alignment padding and finalizes the header only after all offsets and lengths are known.
- It reopens the temporary package with the production reader and validates every tile before Asset publication.
- It then publishes the immutable package file using the Core Asset Store protocol.
- The short SQLite transaction inserts Field Revision metadata and typed Asset ownership and advances the current pointer by compare-and-swap.
- A failed transaction leaves an unreferenced immutable Asset for safe orphan reconciliation; it does not create a formal Field Revision.
- A published package is never patched in place. Any correction creates a new package and Field Revision.

## 7. Versioning

- V1 is format_major 1, format_minor 0.
- A breaking header or directory change increments format_major.
- A backward-compatible optional manifest extension may increment format_minor only when old readers can safely ignore it.
- Unknown required flags, unsupported major versions and unknown required codec profiles are rejected.
- Package format version is independent of Field Descriptor version, Field Revision number, Core Asset hash version and calculation algorithm version.
- The exact package bytes and envelope format version are included in calculation provenance/fingerprints under ME-02-F. The manifest uses one canonical schema contract for the initial release.

## 8. Explicitly deferred

This specification does not select:
- production compression codec or library;
- elevation sample width, quantization scale or numeric error bound;
- tile dimension (256 × 256 remains the benchmark candidate; compare 128 and 512);
- buffered seek I/O versus memory mapping;
- a multi-resolution overview format;
- a GIS import/export format.

These choices require representative workload measurements and Windows/macOS deterministic fixtures.

## 9. Required byte-level fixtures

Before implementation readiness, provide fixtures for:

- minimal one-tile uncompressed package;
- multi-tile package with canonical ordering;
- partial edge tiles and non-square grid dimensions;
- manifest padding and directory/payload alignment;
- maximum supported integer/offset boundaries;
- duplicate and out-of-order directory entries;
- overlapping and out-of-range payload blocks;
- truncated header, manifest, directory and tile payload;
- decoded-length mismatch and tile-hash mismatch;
- unsupported major/minor/flags/codec combinations;
- exact package-hash match and mismatch;
- identical input producing byte-identical output on Windows and macOS.

This is a recommended byte-level specification. It is not executable code or evidence that a parser, writer, codec or platform test has been implemented.


## 10. Canonical manifest and baseline golden fixtures — supplied

ME-05-09 V0.1 and P2-09-34/P2-09-35 now supply the canonical manifest and baseline byte fixtures required by this envelope:

- Strict manifest shape: RFC 8785 JCS UTF-8, no BOM/trailing newline, duplicate keys rejected.
- Three raw WVFLDPK 1.0 packages: minimal elevation 2×2, two-tile land/water 4×2, and non-square edge elevation 3×2.
- Exact canonical manifest sidecars and package/tile SHA-256 inventory.
- Schema and 28 design validation vectors.

These fixtures cover a baseline valid package set. The remaining malformed/truncated/overflow/corruption variants in §9 are still required as negative parser tests. Cross-platform byte-identical writer output and production parser/writer conformance remain unproven.

**Status:** the design-level golden byte fixture prerequisite is partially/nominally supplied; production implementation and the full negative/fuzz suite remain open.


## 11. Parser/writer implementation contract reconciliation

ME-05-10 V0.1 is the normative implementation handoff for this byte envelope. It resolves the expected tile count as a derived dense grid count, defines structural validation before opening a reader, distinguishes random tile hash checks from full package hash verification, and specifies deterministic bounded-memory writing and typed errors.

The directory must contain every native-grid tile coordinate exactly once. No-coverage cells are represented by declared masks, never by omitting a directory entry. The manifest explicit coverage mask and validity mask are distinct concepts. Full package hash is checked by a streaming integrity operation at import/publication boundaries, not implicitly on each random tile read.

P2-09-36 provides 41 parser/writer conformance vectors. They are design acceptance cases, not executed tests. Production implementation, library/codec selection, precision profile and platform evidence remain open.
