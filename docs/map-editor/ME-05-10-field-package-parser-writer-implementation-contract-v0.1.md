# World View — ME-05-10 Field Package Parser / Writer Implementation Contract

- Version: 0.1
- Date: 2026-11-21
- Status: Recommended V1 implementation contract; not an implementation or test report
- Scope: WVFLDPK 1.0 package reader, writer, validation, random tile access and deterministic serialization
- Depends on: ME-05-07 V0.4; ME-05-08 V0.2; ME-05-09 V0.1; P2-09-32 V0.1; P2-09-33 V0.1; P2-09-34 V0.1; P2-09-35 V0.1; P2-09-11 V0.11; P2-09-12 V0.6
- Runtime baseline: C# / .NET 11; Windows and macOS

## 1. Purpose and non-goals

This contract turns the WVFLDPK 1.0 byte layout and canonical manifest into implementable .NET component boundaries and deterministic acceptance behavior.

The parser/writer is a Map Engine infrastructure component. It does not own Field Dataset, Field Revision, Core Asset identity, Map membership, publication authority or the current-revision pointer. Core Domain remains the authority for those records and for final publication authorization.

V1 scope:
- open and structurally validate a seekable immutable package;
- parse and validate the canonical manifest;
- index the complete tile directory;
- read one requested tile without decoding unrelated tile payloads;
- verify decoded tile bytes before returning them;
- write a new package deterministically with bounded memory;
- verify complete package bytes against a Core Asset hash when requested;
- expose typed failures without substituting data.

Non-goals:
- selecting a production compression codec or elevation precision profile;
- GIS import/export, reprojection, resampling or pyramid generation;
- editing a published package in place;
- owning Core Domain publication transactions;
- memory-mapping as a required implementation technique;
- implementing a general-purpose archive framework.

## 2. Normative dependencies and conflict resolution

The implementation MUST use the 72-byte header, 16-byte directory header, 72-byte directory entry, absolute offsets, little-endian fixed-width integers, 8-byte alignment, zero padding and exact physical package length defined by ME-05-08 V0.2.

The manifest MUST conform to P2-09-34 and RFC 8785 JCS UTF-8 rules defined by ME-05-09. Descriptor-level and owner-metadata checks remain separate from JSON Schema validation.

Clarifications made explicit by this contract:
1. The directory is complete and dense for the native grid: every tile coordinate in the grid tile matrix occurs exactly once. A tile is not omitted to represent no coverage. Per-cell validity and coverage are represented by declared mask planes.
2. The expected tile count is derived, not stored in the manifest: ceil(width / tile_width) × ceil(height / tile_height), with checked arithmetic. It MUST equal directory entry_count.
3. A reader opening a package validates all header, manifest, directory and payload ranges before returning a reader handle. It does not eagerly decode every tile.
4. A tile read decodes only that tile and verifies its SHA-256 over canonical decoded bytes before exposing the buffer.
5. Full package SHA-256 is a separate streaming integrity operation used at import/publication boundaries. Random tile access does not imply a full-file hash scan.
6. The manifest schema's explicit_mask coverage mode names a coverage plane. It does not replace the required validity-mask plane; the two masks have distinct semantics and must not be conflated.
7. Manifest bytes in a package are the exact JCS bytes. A reader MUST reject non-canonical bytes rather than silently normalizing them and continuing.

If an older working document uses looser wording, this implementation contract and the current schema/byte-envelope versions govern parser behavior. Any byte-layout change requires an explicit ME-05-08 version update.

## 3. Component boundaries

Keep the implementation small and separable. The following logical components may be classes in one assembly; V1 does not require separate services or processes.

| Component | Responsibility | Must not own |
|---|---|---|
| PackageHeaderReader | Read and validate fixed header | Manifest semantics |
| ManifestReader | Strict UTF-8, duplicate-key detection, JSON Schema and JCS byte comparison | Core Domain records |
| PackageDirectoryReader | Parse entries, prove dense grid coverage, validate ranges/order | Tile decoding |
| TileCodecRegistry | Resolve codec ID/version/parameters and decode/encode | Choosing a new codec implicitly |
| TileReader | Read one stored tile, decode, validate decoded length/hash | Unrelated tiles |
| PackageIntegrityVerifier | Stream whole package hash | Mutating package |
| PackageWriter | Produce deterministic temporary package | Core Domain current pointer |
| PackageValidationPolicy | Resource limits and compatibility policy | User-facing fallback behavior |

The package library MUST accept streams/handles and immutable descriptor inputs through interfaces. It MUST NOT reach directly into UI, editor view models, SQLite repositories or Core Domain tables.

## 4. Recommended .NET API shape

Names are illustrative contract names; implementation may choose equivalent names while preserving semantics.

### 4.1 Reader

- OpenAsync(source, ownerContext, options, cancellationToken) -> PackageReader
- ReadManifest() -> ValidatedFieldManifest
- GetTileAddress(tileX, tileY) -> TileAddress
- ReadTileAsync(tileX, tileY, cancellationToken) -> DecodedTile
- VerifyPackageHashAsync(expectedSha256, cancellationToken) -> IntegrityResult
- DisposeAsync()

OpenAsync requires a seekable random-access source. A non-seekable import stream must first be copied to a bounded temporary file while hashing; it must not be buffered entirely in memory.

OpenAsync validates the complete structural index before returning. It does not decode all tile data. ReadTileAsync returns a buffer only after codec decode, exact decoded-length validation, plane/mask validation and tile-hash verification all succeed.

DecodedTile owns its returned memory and documents its lifetime. The caller must dispose/release it or use an explicit pooled-buffer lease. A buffer must never be returned to a pool while a consumer can still access it.

### 4.2 Writer

- CreateAsync(destination, manifestInput, tileSource, codecPolicy, options, cancellationToken) -> PackageWriteResult
- ValidateWrittenPackageAsync(tempPackage, expectedMetadata, cancellationToken) -> PackageValidationResult

The writer accepts canonical decoded tile inputs in canonical directory order or a tile source addressable by coordinate. It MUST reject missing, duplicate or out-of-grid tile coordinates.

The writer MUST NOT require the entire field or package in RAM. It may use bounded temporary spool storage. A two-phase strategy is recommended:
1. validate manifest and tile plan; encode each tile once into a temporary spool while recording stored length, decoded length and decoded hash;
2. compute all offsets with checked arithmetic, then write header, canonical manifest, zero padding, directory and spooled payloads to the final temporary package;
3. finalize the header with exact lengths, flush and close;
4. reopen through the production reader, read and verify every tile, then compute the complete package SHA-256;
5. return a completed temporary package result to the Core Domain publication coordinator.

If a codec cannot guarantee deterministic output for identical bytes and pinned parameters, it is not eligible for V1 deterministic writing.

### 4.3 Random-access source

The reader should depend on a minimal abstraction equivalent to:
- Length: Int64
- ReadExactlyAtAsync(offset, destination, cancellationToken)

The implementation may use FileStream with explicit seek/read coordination or .NET RandomAccess over a safe file handle. It MUST avoid shared mutable stream-position races. Concurrent reads are permitted only when the underlying source guarantees positional-read safety.

## 5. Reader open algorithm

The order is normative:

1. Obtain physical file length. Reject files shorter than 72 bytes.
2. Read exactly 72 header bytes.
3. Validate magic, format major/minor, header length, flags, reserved bytes and exact package length.
4. Validate manifest offset equals 72; manifest length is positive and within policy; compute end offsets with checked unsigned arithmetic.
5. Validate directory offset is the required 8-byte alignment after manifest; all intervening padding bytes are zero.
6. Read manifest bytes under the manifest budget. Reject invalid UTF-8, BOM, duplicate JSON keys, trailing newline, non-canonical JCS bytes, unknown properties and schema violations.
7. Validate manifest against the supplied Field Descriptor and owner context: field key, schema version, semantics ID, Map Space, spatial reference and expected grid metadata must match.
8. Validate directory offset/length, directory version, flags, entry size and entry count using checked arithmetic.
9. Derive tile columns, tile rows and expected entry count with checked arithmetic. Reject count mismatch or count beyond policy.
10. Validate entries in canonical (tile_y, tile_x) order; require each coordinate exactly once and within grid bounds.
11. Derive each edge tile's exact valid width/height and compare with the entry.
12. Derive exact decoded length from valid dimensions and ordered plane widths; compare with entry decoded_length.
13. Resolve every codec ID against exactly one manifest registry entry. Require codec ID 0 to mean none; reject duplicate IDs and ambiguous codec definitions.
14. Validate payload start alignment, every payload range, stored length, package bounds, non-overlap and zero-filled gaps/padding. Reject trailing bytes and unreferenced payload-region bytes.
15. Return the reader only after the entire structural index passes.

The reader MUST use checked arithmetic for every multiplication, addition, alignment operation and conversion to platform-sized allocation values. It MUST validate bounds before seeking, slicing, allocating or decoding.

## 6. Tile read algorithm

For ReadTileAsync(x, y):

1. Resolve the already-validated directory entry; reject coordinates outside the grid.
2. Read exactly stored_length bytes at payload_offset, using bounded storage.
3. Resolve the pinned codec ID, version and parameters. Unsupported codecs return a typed error.
4. Decode into a bounded destination whose maximum permitted size was validated before allocation.
5. Require actual decoded byte count to equal decoded_length exactly; reject short output, excess output or trailing compressed input where the codec API exposes that condition.
6. Validate plane layout and mask bytes. The validity mask must contain only 0 or 1. Any coverage mask must follow its separately declared semantics and allowed values.
7. Compute SHA-256 over the complete canonical decoded tile byte sequence in manifest plane order; compare in constant time with the directory hash.
8. Only then expose DecodedTile to the caller.

A failed read returns no partially decoded tile. No-data cells remain represented by masks; the reader must not replace invalid samples with zero, interpolate them or invent a fallback.

## 7. Writer determinism and atomicity boundary

For identical canonical manifest bytes, decoded tile bytes, format version, codec implementation/version and codec parameters, the writer MUST produce byte-identical package bytes.

Determinism includes:
- canonical tile ordering;
- canonical JCS manifest bytes;
- exact header and directory field values;
- deterministic offsets and alignment;
- zero-filled padding;
- deterministic codec output;
- no timestamps, random IDs, machine paths or environment-specific metadata in the package.

The writer produces a new temporary immutable package. It never patches a published package. The Core Domain publication coordinator remains responsible for Core Asset registration, Field Revision insertion, typed ownership, compare-and-swap of the Map current pointer and orphan reconciliation. File creation and SQLite commit are coordinated by the existing publication protocol; the package writer does not open its own database transaction.

## 8. Package integrity vs tile integrity

These checks serve different purposes:

| Check | Input | When | Guarantee |
|---|---|---|---|
| Manifest canonicality | Manifest byte range | Every open | Exact canonical manifest representation |
| Structural validation | Header, directory, ranges | Every open | Safe index and legal package structure |
| Tile hash | One decoded tile | Every tile read | Requested decoded tile matches directory hash |
| Package hash | Entire package byte stream | Import/publication, explicit audit | Exact immutable package bytes match Core Asset hash |

A tile hash does not prove the complete package hash. A package hash does not replace tile hash verification after decoding. The expected package hash comes from Core Asset metadata, never from the package manifest.

## 9. Resource policy

Limits are policy inputs, not magic constants embedded in the binary format. The application must have a default profile and permit stricter limits for untrusted imports.

Recommended initial desktop defaults, subject to benchmark confirmation:

| Resource | Default policy |
|---|---:|
| Manifest bytes | 1 MiB |
| Directory entries | 4,000,000 |
| Single decoded tile | 256 MiB |
| Single stored tile | 256 MiB |
| Concurrent tile decodes | 2 |
| Temporary spool free-space reserve | 2 GiB |
| Package length | Int64 and available-disk-space bounded |

These are safety ceilings, not promises that all machines can process the maximum simultaneously. Before allocation, the implementation also checks current memory budget and concurrency slots. If a requested operation exceeds policy, it fails explicitly; it must not silently reduce precision, skip validation or return partial data.

Tile dimensions are additionally constrained by the uint16 valid_width/valid_height fields. Production tile dimensions remain a separate benchmark decision; 256 × 256 is a candidate, not a frozen requirement.

## 10. Typed error taxonomy

Use stable machine-readable error codes. Exception types may wrap these codes, but callers must not parse human-readable messages.

| Code | Meaning | Retry guidance |
|---|---|---|
| WVPK_IO | Read/write/storage failure | Retry only if underlying cause is transient |
| WVPK_TRUNCATED | Required bytes unavailable | Not retryable without a new source |
| WVPK_MAGIC | Invalid magic | Not retryable |
| WVPK_VERSION | Unsupported package version | Requires compatible reader |
| WVPK_HEADER | Invalid header field/flags/reserved bytes | Not retryable |
| WVPK_LENGTH | Length mismatch or forbidden trailing bytes | Not retryable |
| WVPK_OVERFLOW | Checked arithmetic overflow | Not retryable |
| WVPK_LIMIT | Resource policy exceeded | Retry only with explicit policy change |
| WVPK_MANIFEST_ENCODING | Invalid UTF-8/BOM/duplicate keys | Not retryable |
| WVPK_MANIFEST_CANONICAL | Manifest bytes are not exact JCS | Re-serialize source and create a new package |
| WVPK_MANIFEST_SCHEMA | JSON Schema violation | Correct source manifest |
| WVPK_DESCRIPTOR_MISMATCH | Descriptor/owner metadata mismatch | Correct source metadata or package |
| WVPK_DIRECTORY | Invalid directory header/count/order/coverage | Not retryable |
| WVPK_TILE_RANGE | Out-of-range, overlapping or misaligned payload | Not retryable |
| WVPK_CODEC_UNSUPPORTED | Codec ID/version unavailable | Install supported codec or regenerate |
| WVPK_DECODE | Codec decode failure or invalid encoded stream | Not retryable for same bytes |
| WVPK_DECODED_LENGTH | Decoded byte count mismatch | Not retryable |
| WVPK_MASK | Invalid mask plane/value | Not retryable |
| WVPK_TILE_HASH | Decoded tile hash mismatch | Treat package as corrupt |
| WVPK_PACKAGE_HASH | Complete package hash mismatch | Treat package as corrupt |
| WVPK_CANCELLED | Operation cancelled | Caller may retry deliberately |

Errors must retain safe diagnostic context: package identifier if known, tile coordinate if applicable, byte offset and expected/actual values where safe. Do not include arbitrary payload bytes or sensitive filesystem details in user-facing messages.

## 11. Concurrency, cancellation and lifetime

- Reader instances are immutable after OpenAsync.
- Positional reads may execute concurrently subject to the configured decode limit.
- A single mutable seek position must never be shared across concurrent reads without serialization.
- Cancellation is honored between I/O and decode stages. A cancellation after a temporary write begins deletes or quarantines the incomplete temporary file; it never publishes it.
- DisposeAsync prevents new operations and waits for or safely cancels in-flight operations according to the implementation's documented lifetime policy.
- A decoded buffer remains valid until its lease is disposed; consumers must not retain references after release.

## 12. Required conformance suite

The implementation must include automated tests for all of the following groups:

A. Golden success:
- open all three ME-05-09 golden packages;
- reproduce exact manifest bytes and expected metadata;
- read every tile and match expected decoded bytes and tile hashes;
- verify complete package hashes against the golden inventory.

B. Manifest:
- all P2-09-35 cases;
- duplicate JSON keys, invalid UTF-8, BOM, trailing newline, non-JCS property order and numeric edge cases;
- descriptor and Core Domain owner-context mismatch.

C. Header and directory:
- all P2-09-33 cases;
- short header, invalid magic/version/flags/reserved bytes;
- arithmetic overflow, count mismatch, duplicate/out-of-order/missing tile coordinate;
- incorrect edge dimensions, invalid codec ID and nonzero padding.

D. Payload and decode:
- out-of-range, overlapping and unaligned payloads;
- short/long decode, decompression bomb, unsupported codec, trailing encoded data;
- decoded-length, mask and tile-hash mismatch;
- corruption of one tile must not cause a different tile to be returned as a fallback.

E. Writer:
- deterministic byte equality for repeated writes;
- all three golden outputs reproduced byte-for-byte by a conforming writer using the fixture-only profiles;
- bounded-memory test with a field larger than the configured in-memory budget;
- cancellation and disk-full cleanup;
- temporary package reopened and all tiles verified before publication handoff.

F. Platform:
- same fixture input yields identical package bytes on Windows and macOS;
- same malformed fixture yields the same stable error code;
- file-handle disposal, rename/replace and temporary-file cleanup behavior verified on both platforms.

The golden fixture-only sample profiles are test data. Passing their tests does not approve float32 elevation or uint8 categorical production profiles.

## 13. Implementation acceptance and delivery

A developer delivery is accepted only when it includes:
1. production reader/writer code and public API documentation;
2. automated test source and machine-readable test results;
3. byte-for-byte golden fixture comparison results;
4. Windows and macOS test evidence;
5. memory, random-access and large-package benchmark results;
6. dependency/license inventory for any JSON Schema, JCS or codec library;
7. known limitations and explicit list of unsupported codecs/profiles.

Design completion is not implementation completion. No parser, writer, fuzzing, benchmark or cross-platform pass is claimed by this document.

## 14. Remaining decisions deliberately not made here

- Production elevation precision/quantization and numeric error budget.
- Production land/water encoding profile if different from the fixture profile.
- Production compression codec and implementation.
- Production tile dimension after representative benchmarks.
- JCS and JSON Schema library selection after .NET compatibility, duplicate-key behavior, licensing and cross-platform byte tests.

These are technical selection gates. The developer must not silently make them by choosing a convenient default and marking the format production-ready.
