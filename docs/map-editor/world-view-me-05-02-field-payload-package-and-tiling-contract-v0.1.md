# World View — ME-05-02 Field Payload Package & Tiling Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 technical contract; logical package and tiling rules established, physical container/codec/precision require implementation proof
- Owner: Map Engine / Spatial Services (package format, tile access, sampling); Core Domain (Field Dataset/Revision identity, immutable Asset publication and retention)
- Depends on: ME-05-01 V0.3; P2-09-11 V0.11; P2-09-12 V0.6; P2-09-30 Field Descriptor Registry V0.1; P2-09-31 Field Dataset Validation V0.1; ME-02-F V0.3; P2-09-25 Geometry Asset Encoding V0.1; Production Renderer Architecture V0.2

## 1. Purpose and decision summary

This contract defines how one immutable Field Revision payload is packaged, tiled, read, validated, staged and published. It does not change the adopted Core Domain authority model:

- One Field Revision references exactly one logical immutable Field Package Asset.
- The package may contain a manifest, deterministic tile directory and many tile payload blocks.
- Tiles are storage/processing partitions, not Objects, Geometry Components, Commands, State Changes, Events, Branches or independent Field Revisions.
- The Field Revision and its package are immutable after publication.
- A local edit creates a new package and a new Field Revision; it never modifies a published package or tile in place.
- The Map Source Field Baseline remains Map-owned and retroactive across all Branches and Historical Time points.

**Recommended V1 direction:** a single seekable package file with a small canonical manifest and tile directory followed by independently addressable tile payload blocks. The package is the Core Asset; tile payloads are internal package members. This avoids a directory-of-files lifecycle, per-tile Core Asset ownership tables and a generic tile registry while still allowing random tile reads and streaming publication.

The package envelope is World View-owned and versioned. It is not GeoTIFF, COG, Zarr, GeoPackage or a general-purpose raster format. Import/export adapters may support external formats later without making them authoritative.

## 2. Goals and non-goals

### Goals

1. Read one or a small set of tiles without loading the full field.
2. Stream a complete package through validation, hashing and publication with bounded memory.
3. Support local authoring by staging changed tiles and carrying unchanged tile payloads forward into a new immutable package.
4. Provide deterministic tile addressing, ordering, edge behavior and package hashing.
5. Keep spatial meaning in the canonical field manifest and Field Descriptor, not in file names or tile coordinates alone.
6. Support calculation neighborhoods/halos without treating tile boundaries as physical boundaries.
7. Keep Core Domain, Map Engine, Editor/AI staging and Renderer responsibilities separate.
8. Preserve cross-platform read/write compatibility across Windows and macOS.

### Non-goals

- A GIS interchange format or a replacement for external raster formats.
- A general-purpose multidimensional scientific data cube.
- Branch-local or time-varying Field Revision storage.
- Independent per-tile Core Domain identity, revision history or permissions.
- A renderer texture/GPU cache format.
- Selection of a specific compression library, numeric storage width or .NET package.
- Mandatory multiresolution pyramids or automatic resampling.
- Distributed storage, remote streaming or collaborative writes.

## 3. Authority and ownership boundaries

| Concern | Authority | Rule |
|---|---|---|
| Field Dataset / Field Revision identity | Core Domain | Existing ME-05-01 contract |
| Package Asset identity, immutable publication and retention | Core Domain + Core Asset Store | One typed Field Revision → one package Asset |
| Package envelope, manifest grammar and tile addressing | Map Engine field I/O contract | Versioned, deterministic, read-only after publication |
| Field semantics, categories, units and no-data | Core Field Descriptor Registry | Package cannot redefine descriptor semantics |
| Staging and changed-tile production | Editor / Map Engine authoring services | Provisional only |
| Formal publication | Core Domain Field Dataset Domain Service | Validates staged package and advances current revision by CAS |
| Calculation tile scheduling and halo assembly | Calculation / Spatial Services | Algorithm-specific, deterministic and context-aware |
| Display textures, mipmaps and GPU buffers | Renderer / Presentation | Derived, disposable caches only |

The package reader is not a second authority. It interprets bytes according to the Field Revision's pinned descriptor and manifest. It cannot publish, replace or mutate formal Field Revisions.

## 4. Logical package model

A Field Package contains four logical sections:

1. **Header** — magic identifier, package format version, section offsets/lengths and integrity metadata needed to locate the remaining sections safely.
2. **Canonical manifest** — field identity/interpretation metadata, grid definition, coverage, tile dimensions, value encoding profile, no-data rules, and package compatibility.
3. **Tile directory** — a canonical ordered index from integer tile coordinates to payload byte ranges, encoded/unencoded lengths, codec identity and tile content hash.
4. **Tile payload region** — independently decodable tile blocks in the order declared by the directory.

The physical container must support random access by offset. A reader must be able to validate the header and directory, then read an individual tile without scanning or decompressing all preceding tile payloads.

### 4.1 Package identity and hashes

- Core Asset content hash covers the exact complete published package bytes.
- The package manifest records the package format/version and canonical field manifest fingerprint.
- Every tile entry carries a hash of the canonical uncompressed tile bytes.
- The directory and manifest are included in the package-level hash; changing tile order, codec parameters, manifest values or payload bytes changes the package hash.
- Tile hashes support lazy integrity verification and diagnosis; they do not replace the Core Asset package hash.
- Hash algorithm/version is inherited from the Core Asset contract; SHA-256 remains the current recommendation until that contract is frozen.
- Do not use file path, creation time, machine ID, random temporary ID or locale-formatted values in canonical hashed content.

### 4.2 Deterministic serialization

- Manifest is canonical UTF-8 JSON with ordinal key ordering, invariant numeric serialization and no duplicate keys.
- Tile directory ordering is fixed: resolution level (V1 only level 0), tile row (Y), then tile column (X).
- Tile payload byte order is explicitly declared; V1 package encoding must use a platform-independent canonical byte order.
- Numeric payloads reject NaN and infinities. If negative zero is semantically equivalent to zero for a field descriptor, normalize it before hashing.
- Category codes are stable descriptor-defined codes; never derive codes from localized display labels or incidental enumeration order.
- Compression codec ID, codec version and all behavior-affecting parameters are pinned in the tile entry/package manifest. A codec upgrade that changes bytes produces a new package fingerprint.
- No timestamps, filesystem metadata or nondeterministic padding are included in canonical sections. Any required alignment padding is zero-filled and specified.

## 5. Grid and tile addressing

A Field Revision describes one authoritative native grid for its declared Map Space.

### 5.1 V1 grid model

- One Field Revision has one native resolution/grid level (level = 0).
- Grid origin, axis orientation, cell registration, dimensions, spacing, spatial reference and coverage are declared in the canonical manifest.
- Grid cell coordinates are integer column/row indices relative to the declared grid origin.
- Tile coordinates are integer tile_x/tile_y indices; tile origin is derived from tile coordinates and the fixed tile width/height.
- A tile contains a rectangular block of cells. Edge tiles may have smaller valid width/height; padding outside valid dimensions is never treated as data.
- V1 does not store authoritative overviews/pyramids. Renderer mipmaps and coarse display levels are derived caches. Any resampled source grid is a new explicit Field Revision with transformation provenance.

### 5.2 Tile dimensions

Use a dataset-wide tile dimension recorded in the manifest. Recommend **256 × 256 cells** as the initial implementation candidate because it balances index size, local reads, edit granularity and neighborhood access for the expected desktop/local-first workload.

This is a benchmarkable recommendation, not a frozen universal constant. The implementation proof must compare at least 128 × 128, 256 × 256 and 512 × 512 on representative elevation and categorical fields. Once published, tile dimensions are immutable within that Field Revision. Different revisions may use a different tile dimension only through an explicit package revision; readers must not assume a global constant.

### 5.3 Tile completeness and coverage

- The tile directory must contain exactly one entry for every tile intersecting the declared rectangular storage grid.
- Duplicate, overlapping, missing or out-of-range tile entries are invalid.
- The manifest's coverage is distinct from the rectangular storage grid. Cells outside declared coverage must resolve through explicit coverage semantics; they must not be inferred from a missing tile.
- A partial-coverage dataset may contain cells outside its valid coverage. Those cells must preserve the descriptor's explicit outside_coverage / no-data distinction.
- A tile edge, package edge, grid edge or coverage edge is never automatically a coastline, watershed divide, outlet or other geographic feature.
- Wrapped/seam behavior is declared by Map edge semantics and grid definition, never inferred by the tile reader.

## 6. Tile payload semantics

A tile is a rectangular cell block with a declared field encoding. It is not a self-describing semantic field: its descriptor and manifest supply meaning.

### 6.1 Elevation

- Stores finite numeric values under a pinned elevation precision profile.
- Unit and vertical_reference_id are mandatory at the Field Revision manifest level.
- No-data is encoded distinctly from valid zero elevation.
- Storage width (for example, 32-bit or 64-bit float, or a quantized integer profile) remains a precision/performance gate; it must be explicitly identified and validated, never guessed by the reader.
- Quantization scale/offset and rounding mode, if used, are explicit and included in the manifest and fingerprint.
- Decoded values must be checked for finite range and descriptor constraints.

### 6.2 Land/water

- Stores stable registered category codes from the Field Descriptor Registry.
- land, water, unknown and outside_coverage remain distinct categories.
- No-data remains a separate invalid/absent sample state; it is not an alias for any category.
- Category code mapping is pinned by descriptor semantics/version. Display text is not serialized as the category identity.

### 6.3 Cell order and edge tiles

- Cell traversal order is fixed and declared by the package format. Recommended V1 order is row-major, left-to-right within each row, then top-to-bottom.
- Tile directory records valid width/height for edge tiles.
- Padding bytes/cells, if the physical encoding requires alignment, are deterministic and ignored by logical sampling; they must never be exposed as valid cells.
- Tile decode must validate expected decoded byte count from dimensions and value encoding before allocating or exposing samples.

## 7. Compression and physical container policy

The logical contract requires independently decodable tile payloads. It does not yet freeze a codec.

- Each tile is an independent compression unit. One tile must be readable without decompressing unrelated tiles.
- The package must support an uncompressed codec (none) for fixtures, diagnostics and fallback.
- A production codec may be selected only after measuring compression ratio, decode latency, random-read cost, CPU/memory use, deterministic output and Windows/macOS support.
- Codec ID, exact codec version/profile and parameters are part of package compatibility. Unsupported codec versions fail clearly; they are never silently decoded with a “close enough” implementation.
- Compression must be bounded against decompression bombs: maximum declared uncompressed size, dimension-derived expected size and allocation limits are validated before decode.
- The package reader must reject malformed offsets, integer overflow, overlapping payload ranges, out-of-file ranges, inconsistent lengths and unknown required flags.
- Package writer must produce deterministic output for equal canonical manifest and equal canonical tile bytes under the same format/codec version and parameters.
- The package is a single logical Core Asset file. Temporary package construction and final immutable publication follow the Core Asset Store's atomic file-publication contract.

A minimal binary envelope with fixed-width header fields, canonical manifest bytes, a fixed-schema tile directory and contiguous payload blocks is preferred over a general archive/ZIP dependency. The exact byte-level header layout and codec implementation are implementation gates and must be specified before production code is merged.

## 8. Random access, caching and resource limits

- Reader opens the package read-only and reads header, manifest and directory first.
- Tile reads seek directly to the selected payload range, decode only that tile and verify its uncompressed tile hash before returning samples.
- Repeated reads may use a bounded in-memory decoded-tile cache keyed by package content hash + tile coordinate + decoder compatibility version.
- Cache entries are disposable and must be evicted under memory pressure; cache contents are never persisted as formal field data.
- Cache size is governed by a memory budget, not by an unbounded “keep all visited tiles” policy.
- Reader supports concurrent immutable reads. It must not share mutable decode buffers across callers without explicit ownership/lifetime protection.
- UI and Renderer callbacks must not perform package I/O, decompression, hashing or database access. These operations run in background services and publish immutable prepared results.
- File handles and memory-mapped views, if used, have explicit lifetime/disposal rules. The choice between buffered seek reads and memory mapping is benchmark-driven.
- Package readers must not expose writable pointers or mutable views into published Asset bytes.

## 9. Calculation tiles and halo semantics

Storage tiles and calculation tiles are related but not identical.

- Calculation requests declare a working scope, expanded calculation scope, algorithm version and required neighborhood/halo.
- A calculation tile may align with storage tiles, span multiple storage tiles or be smaller than one storage tile.
- Halo cells are read from neighboring storage tiles or declared boundary policy. Halo samples are not duplicated into the authoritative package.
- The algorithm declares halo width/context radius and whether the dependency can propagate beyond the local tile.
- Tile boundaries are processing partitions only. Adjacent calculation outputs must use the same source fingerprints, algorithm/semantics versions, parameters and boundary rules.
- Stitching must be deterministic and preserve shared-edge consistency.
- If required neighbor data is unavailable, output is incomplete/boundary-uncertain; the service must not invent values to make the result appear seamless.
- Hydrology and climate may require basin/global context; a tile-local run must not claim global correctness without the algorithm's declared proof.
- A tile read or calculation never turns no_data into zero or outside_coverage into water/land.

## 10. Local editing and new Revision construction

A local edit is staged as a set of changed tiles against a pinned base Field Revision.

1. The editor captures the expected current Field Revision ID and package fingerprint.
2. The edit session identifies affected storage tiles plus any algorithm/validation context needed around them.
3. Only changed tile payloads are re-encoded. Unchanged tiles are copied through as their exact canonical uncompressed content (or exact encoded block when the package/codec compatibility permits).
4. The writer builds a complete new package in a temporary location, writing a new manifest/directory and streaming tile blocks. It must not load the whole field into memory.
5. The new package is fully validated: directory coverage, dimensions, descriptor values, tile hashes, package hash, coverage and manifest coherence.
6. Core Domain preflight rechecks Map ownership, descriptor compatibility and expected current revision.
7. The immutable package file is published; then one short SQLite transaction inserts the new Field Revision, typed Asset reference, CAS-updates the current pointer and records audit.
8. If CAS fails, the package is not made current. It remains an unreferenced orphan/candidate subject to safe reconciliation; the edit must be rebased/reviewed.
9. After commit, only calculations/Checkpoints that depend on the Field Dataset are marked stale across every Branch and Historical Time of the Map.
10. The prior package and revision remain immutable and retained under the established Asset retention policy.

The writer may reuse exact encoded tile blocks only when codec ID/version/parameters and all tile encoding semantics are identical. Otherwise it must decode and re-encode through the canonical writer. It must never copy bytes whose interpretation cannot be proven compatible.

## 11. Validation and failure behavior

Reject a package when any of the following holds:

- unsupported magic, format version or required feature flag;
- malformed header, invalid section offsets/lengths or integer overflow;
- manifest is not canonical or disagrees with the Field Revision/descriptor;
- duplicate, missing, overlapping, out-of-range or unordered tile directory entries;
- tile dimensions or expected decoded byte counts are inconsistent;
- tile payload ranges overlap or extend outside the package;
- tile hash or complete package hash fails;
- unknown required codec or incompatible codec version;
- decompressed size exceeds declared/dimension-derived limits;
- numeric samples contain NaN/Infinity or violate the pinned precision profile;
- category codes are unknown or no-data/category/coverage states are conflated;
- a partial coverage or edge rule is omitted or contradictory;
- package grid/CRS/Map Space is incompatible with the owning Map;
- a published package is modified in place.

Failure handling:

| Failure | Required behavior |
|---|---|
| Edit cancelled before package publication | Discard temporary staging; formal Field Revision unchanged |
| Tile encode/decode or hash failure | Reject new package; preserve base Revision |
| Process crash during package build | Temporary file is not a formal Asset; clean only through safe temp/orphan recovery |
| Package file published, SQLite transaction fails | No Field Revision/current-pointer commit; reconcile proven orphan safely |
| CAS conflict | Do not advance current pointer; require rebase/review |
| Reader encounters corrupt tile | Return a typed read/integrity error; never substitute zero or silently skip |
| Cache entry stale/corrupt | Evict and rebuild from immutable package |
| Renderer device reset | Rebuild derived GPU resources; do not modify Field Package |

## 12. Versioning and compatibility

- Package format version is independent of Field Descriptor schema/semantics version, Field Revision number, Core Asset hash algorithm and calculation algorithm version.
- A breaking header, directory, tile encoding or manifest grammar change increments package format major version.
- A backward-compatible optional field/flag may increment minor version only when older readers can safely ignore it; unknown required flags are rejected.
- A Field Descriptor semantic change increments the descriptor schema/semantics version even if the package bytes are unchanged.
- Reader compatibility is explicit: supported package versions and codec profiles are declared by the application build.
- Old packages remain readable for as long as retained references require them. Migration produces a new immutable package and Field Revision; it never rewrites a published package in place.
- Calculation input fingerprints include package format version, Field Revision ID, package Asset hash, descriptor schema/semantics and canonical field manifest fingerprint.

## 13. Recommended V1 boundaries and deferred decisions

| Decision | V1 recommendation | Status |
|---|---|---|
| Logical package | One package Asset per Field Revision | Recommended baseline |
| Physical container | Minimal seekable World View package with header/manifest/directory/tile blocks | Needs byte-layout proof |
| Tile size | 256 × 256 candidate; benchmark 128/256/512 | Not frozen |
| Grid levels | One authoritative native level; no stored authoritative pyramids | Recommended |
| Tile payload order | Row-major, X-fastest | Recommended |
| Tile edge | Smaller valid edge dimensions, deterministic padding if needed | Recommended |
| Compression | Independent per-tile blocks; none fixture/fallback | Codec selection open |
| Elevation numeric width | Explicit precision profile; compare 32-bit/64-bit/quantized candidates | Open |
| Land/water category encoding | Stable descriptor-defined integer codes | Recommended |
| Byte order | Explicit canonical little-endian for numeric binary payloads | Recommended |
| Tile cache | Bounded in-memory decoded-tile cache | Recommended |
| Halo | Read neighboring storage tiles; do not store duplicate halo cells | Recommended |
| Partial edits | Build a new package by streaming changed and unchanged tile content | Recommended |
| Multi-resolution overviews | Derived caches or explicit resampled Field Revision | Deferred |

## 14. Required implementation evidence

Before declaring the package contract implementation-ready, the development team must provide:

1. A byte-level package header and directory specification, including overflow-safe parsing.
2. Canonical manifest and tile-directory serialization fixtures.
3. Representative elevation and land/water fixtures at tile interiors, edges, corners and partial coverage.
4. Deterministic package writer output across Windows and macOS for identical canonical input.
5. Random tile read tests proving no full-package decode is required.
6. Tile hash verification and package hash verification, including corruption injection.
7. Memory-bounded streaming package construction and local-edit copy-through tests.
8. Benchmark comparison for 128, 256 and 512 tile dimensions and selected codec candidates.
9. Numeric precision/error measurements for elevation storage candidates.
10. Cancellation, process-crash, CAS conflict and orphan-recovery tests.
11. Windows/macOS reader/writer compatibility and resource lifetime evidence.
12. Integration evidence that renderer callbacks never perform field I/O or decompression.

This is a technical design contract and recommendation. It is not executable code, a frozen byte-level file specification, a selected compression library or evidence of passing tests.
