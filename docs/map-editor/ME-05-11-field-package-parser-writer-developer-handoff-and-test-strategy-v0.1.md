# World View — ME-05-11 Field Package Parser / Writer Developer Handoff & Test Strategy

- Version: 0.1
- Date: 2026-11-21
- Status: **Developer handoff / implementation plan; not implementation evidence or test report**
- Scope: WVFLDPK 1.0 production reader, writer, conformance suite and Windows/macOS verification
- Owner: Development team; Map Engine field I/O implementation
- Normative contracts: ME-05-07 V0.5; ME-05-08 V0.3; ME-05-09 V0.1; ME-05-10 V0.1; P2-09-34 V0.1; P2-09-35 V0.1; P2-09-36 V0.1
- Related Core Domain contracts: ME-05-01 V0.5; ME-05-05 V0.3; P2-09-11 V0.11; P2-09-12 V0.6; P2-09-30 V0.1

## 1. Purpose and handoff boundary

This document converts the package format and parser/writer contracts into a sequenced implementation plan, code-boundary checklist, test strategy and developer delivery requirements.

It is an implementation handoff, not permission to change domain authority or silently settle deferred technical choices. The development team owns product code, package/library selection within the gates below, implementation, test execution and evidence. The design baseline remains owned by the World View architecture documentation.

**Implementation target:** a small .NET 11 library/component that reads and writes the WVFLDPK 1.0 immutable package format, plus automated tests and platform evidence. It is not a separate service/process and must not depend on UI, SQLite repositories or editor view models.

**Not included:** Core Domain Field Dataset publication implementation, new database schema, UI workflows, GIS import/export, production precision/codec decisions without the required benchmark gate, or changing the binary format.

## 2. Normative precedence and change control

Use these sources in this order for the relevant concern:

1. ME-05-08 V0.3 — binary header, directory, offsets, alignment and package layout.
2. P2-09-34 V0.1 + ME-05-09 V0.1 — manifest shape and exact RFC 8785 JCS UTF-8 bytes.
3. ME-05-10 V0.1 — parser/writer algorithms, API semantics, error taxonomy, resource/concurrency rules.
4. P2-09-35 V0.1 — 28 manifest validation vectors.
5. P2-09-36 V0.1 — 41 parser/writer conformance vectors.
6. ME-05-07 V0.5 — logical field/tile/coverage/halo semantics.

If implementation discovers a contradiction, stop that slice and report the exact conflicting clauses and a minimal proposed resolution. Do not locally reinterpret a normative byte or domain rule. A byte-layout change requires an explicit ME-05-08 versioned amendment and regenerated fixtures/vectors. A domain-semantic change requires the relevant owner review; the parser library must not make that decision.

The current golden packages are design reference bytes. They are not evidence that a production reader or writer exists or passes.

## 3. Deliverable structure

Keep the implementation in one library/assembly unless the existing solution structure gives a clear reason otherwise. Suggested logical namespaces/components:

| Component | Required responsibility | Explicit exclusion |
|---|---|---|
| PackageHeaderReader | Exact 72-byte header read and validation | Manifest interpretation |
| StrictManifestReader | Strict UTF-8, duplicate-key rejection, schema validation, JCS byte equality | Core Domain persistence |
| PackageDirectoryReader | Directory parsing, dense-grid coverage, ordering, bounds and overlap checks | Payload decompression |
| FieldPackageReader | Immutable opened index and random tile access | Publication/current pointer |
| TileCodecRegistry | Explicit codec ID/version/parameter resolution | Implicit codec fallback |
| TileDecoder | Bounded decode, decoded length, mask and tile-hash checks | Repairing corrupt data |
| PackageIntegrityVerifier | Streaming SHA-256 of complete package | Mutating package |
| FieldPackageWriter | Deterministic new-package construction with bounded memory | Updating published package |
| PackageValidationPolicy | Explicit resource and compatibility ceilings | Silent policy relaxation |

These may be classes in one assembly. Use interfaces at I/O and codec seams so tests can supply in-memory/faulting sources. The package component receives immutable expected descriptor/owner metadata from its caller; it must not query Core Domain tables itself.

### 3.1 API surface to implement

Equivalent names are acceptable; behavior is normative.

**Reader**
- `OpenAsync(source, expectedFieldContext, options, cancellationToken)`
- `Manifest` / `ReadManifest()`
- `GetTileAddress(tileX, tileY)`
- `ReadTileAsync(tileX, tileY, cancellationToken)`
- `VerifyPackageHashAsync(expectedSha256, cancellationToken)`
- `DisposeAsync()`

**Writer**
- `CreateAsync(destination, manifestInput, tileSource, codecPolicy, options, cancellationToken)`
- `ValidateWrittenPackageAsync(tempPackage, expectedContext, cancellationToken)`

**Random access source**
- physical `Length`
- exact positional read equivalent to `ReadExactlyAtAsync(offset, destination, cancellationToken)`

Do not expose mutable package bytes, partially decoded tiles, or pooled buffers without an explicit lease/lifetime contract. An opened reader is immutable; concurrent reads must not share a mutable stream position unsafely.

## 4. Sequenced implementation slices

Each slice must be merged only after its own tests pass. Avoid implementing the entire writer before the reader can validate fixtures.

| Slice | Scope | Required output / exit condition |
|---|---|---|
| S0 — Repository preflight | Inspect solution layout, target frameworks, nullable/warnings policy, test conventions, CI matrix and existing hashing/JSON dependencies | Short preflight note; no unrelated refactor; proposed package-library list with licenses |
| S1 — Test corpus import | Add the three exact `.wvfld` golden packages, manifest sidecars and inventory as test assets; verify fixture hashes before use | Tests fail clearly if any fixture differs from inventory; no fixture rewriting |
| S2 — Binary primitives | Exact reads, little-endian integer helpers, checked arithmetic, alignment and zero-padding helpers | Unit tests for short reads, overflow, alignment boundaries and endianness |
| S3 — Strict manifest | Strict UTF-8 and duplicate-key detection, schema validation, JCS canonicalization and exact-byte comparison | All P2-09-35 cases pass; no parser normalization of package bytes |
| S4 — Header and directory open | Header validation, section layout, directory header/entries, derived dense tile matrix, edge dimensions, payload bounds/non-overlap/padding | Reader opens all golden packages structurally without decoding tile payloads; negative structural tests pass |
| S5 — Codec seam and tile read | Codec ID 0 (none) first; bounded decoder contract; exact decoded length; mask validation; SHA-256 tile hash | Read every golden tile and compare exact decoded bytes; corrupt tile returns typed error and no buffer |
| S6 — Whole-package integrity | Streaming package hash with cancellation and bounded buffers | Golden package hashes match inventory; one-byte mutation is detected |
| S7 — Deterministic writer core | Canonical manifest input, canonical tile ordering, spool strategy, checked offsets, zero padding, final header | Writer produces valid package and does not buffer entire field/package in memory |
| S8 — Production-reader round trip | Reopen writer output through production reader, read/verify every tile, calculate package hash | Golden-input write output is byte-identical where the selected profile/codec is deterministic; otherwise exact limitation is documented and gate remains open |
| S9 — Robustness and concurrency | Fault injection, cancellation, concurrent positional reads, disposal/buffer lifetime, resource ceilings | No partial publication/output, no races, no unbounded allocation, stable typed errors |
| S10 — Platform/performance proof | Windows + macOS CI, deterministic byte comparison, memory/random-read/write benchmarks | Evidence report with runtime/OS/build/fixture/workload/measurements; no unsupported performance claims |

S0–S6 establish a useful reader before writer complexity is introduced. S7–S8 complete the writer path. S9–S10 are mandatory before calling the implementation production-ready.

## 5. Test project and test layers

Use the repository's existing test framework if established. Do not add a second test framework merely for this component.

### 5.1 Unit tests

Test pure helpers independently:
- exact-read behavior for partial/zero reads;
- little-endian read/write for every field width;
- checked add/multiply/alignment and conversion overflow;
- canonical tile coordinate ordering and edge-size derivation;
- expected tile count = ceil(width / tile_width) × ceil(height / tile_height);
- decoded byte length from ordered planes and masks;
- typed error mapping and retry classification.

### 5.2 Manifest conformance

Execute all 28 P2-09-35 vectors, preserving their IDs in test names or test-case metadata. Add explicit tests for:
- invalid UTF-8 and BOM;
- duplicate object keys at every nesting level;
- trailing newline, whitespace/property-order mismatch and non-JCS bytes;
- NaN/Infinity and unsafe numeric values;
- unknown properties and unsupported schema/codec/hash versions;
- descriptor/owner-context mismatch;
- fixture-only profiles accepted for fixture tests but never treated as production profile selection.

The package reader rejects non-canonical manifest bytes. It must not silently parse and reserialize a malformed package into an accepted representation.

### 5.3 Reader and binary conformance

Execute all 41 P2-09-36 vectors, retaining stable vector IDs. Also ensure P2-09-33 binary-envelope cases are represented where not already covered by P2-09-36.

Mandatory groups:
- golden open without eager tile decode;
- header truncation, magic/version/flags/reserved/length errors;
- section offset, alignment, arithmetic overflow and nonzero padding;
- directory entry count, canonical order, duplicates, missing coordinates and out-of-grid coordinates;
- incorrect edge tile dimensions and decoded length;
- unknown/ambiguous codec references;
- payload out-of-range, overlap, gaps/trailing bytes and malformed stored lengths;
- tile decode failure, invalid mask, hash mismatch and unsupported codec;
- complete package hash match/mismatch.

### 5.4 Writer tests

- reject missing, duplicate and out-of-grid tile source coordinates;
- emit canonical row-major tile directory ordering;
- emit exact JCS manifest bytes and zero-filled alignment padding;
- produce exact header/directory lengths and offsets;
- deterministic output for identical input and pinned codec implementation/version/parameters;
- cancellation and I/O failure leave no publishable final artifact;
- reopen with the production reader and verify every tile before returning success;
- compare output package bytes with golden bytes when the input fixture and writer profile are identical.

A round-trip test alone is insufficient: reader and writer can share the same defect. Golden bytes and independently asserted expected fields/hashes are mandatory.

### 5.5 Fault, fuzz and adversarial tests

Add bounded fuzz/property-based tests for:
- arbitrary/truncated header and directory bytes;
- random offset/length combinations and overflow edges;
- manifest parser duplicate-key/invalid UTF-8 inputs;
- tile payload corruption and adversarial compressed streams;
- maximum policy boundaries and one-over-limit cases.

Fuzzing must be resource-bounded and reproducible from recorded seeds. A fuzz crash is a defect, not an expected parser exception. No fuzz input may trigger unbounded memory allocation, unbounded CPU work or writes outside the designated temporary directory.

## 6. Golden fixture handling

The test corpus is immutable and must be copied byte-for-byte from:
`docs/architecture/core-domain/fixtures/field-package-v1/`

Expected package lengths, package SHA-256 values, manifest sidecars and tile hashes come from `golden-fixtures.json`. Tests must verify the inventory before exercising parser behavior.

Rules:
1. Never regenerate or “fix” a golden fixture as part of ordinary test execution.
2. Never use production fixture-only sample profiles to infer the final elevation precision or land/water storage encoding.
3. A fixture update requires a deliberate contract change, new versioned inventory, explanation and regenerated conformance evidence.
4. Keep negative/mutated fixtures generated in test code or a separate derived test directory; do not overwrite the three canonical golden packages.
5. Record whether tests compare package bytes, canonical manifest bytes, decoded tile bytes or hashes; these are distinct assertions.

## 7. Dependency and codec decision gates

Before adding dependencies, inspect existing repository dependencies and license policy. Record exact package name, version, license, supported target frameworks, Windows/macOS support, maintenance status and known security advisories.

**JCS:** use a conforming RFC 8785 implementation or provide a documented byte-equivalence proof against an independent reference corpus. Generic JSON serialization is not sufficient.

**JSON Schema:** choose a validator that implements the schema dialect/features used by P2-09-34. Record dialect compatibility and ensure unknown-property rejection is preserved.

**Hashing:** use .NET/platform cryptographic SHA-256 APIs unless a measured reason requires otherwise.

**Codec:** implement codec ID 0 (none) for golden fixtures first. A production codec is a separate gate requiring deterministic output, bounded decompression, stable version/parameters, acceptable license, maintained Windows/macOS support and benchmark evidence. Do not silently assign a codec ID or make fixture-only profiles production defaults.

**Precision and tile size:** do not freeze elevation numeric width/quantization, land/water production plane encoding or tile dimensions in this handoff. These require a separate documented decision informed by representative datasets and accuracy/performance measurements. Compare 128×128, 256×256 and 512×512 tile candidates as specified by ME-05-07.

## 8. Resource, security and operational requirements

Start with ME-05-10's recommended policy defaults, all configurable through an explicit policy object:

| Resource | Initial ceiling (benchmark/policy confirmation required) |
|---|---:|
| Manifest bytes | 1 MiB |
| Directory entries | 4,000,000 |
| Single decoded tile | 256 MiB |
| Single stored tile | 256 MiB |
| Concurrent tile decodes | 2 |
| Temporary spool free-space reserve | 2 GiB |

Before allocating, validate dimensions, derived lengths, checked arithmetic, configured ceilings and current memory/concurrency budget. Limits are policy, not permission to skip validation. Exceeding a limit returns a typed failure; never clamp, truncate, reduce precision or substitute data.

Additional requirements:
- use positional reads or serialize access to a mutable stream position;
- never trust manifest/directory sizes before bounds checks;
- do not decompress before validating expected decoded size and policy;
- do not expose a tile until decoded length, masks and hash all pass;
- honor cancellation between I/O/decode stages;
- delete or quarantine incomplete temporary output;
- never patch a published package in place;
- do not log arbitrary payload bytes or sensitive filesystem paths;
- ensure disposal does not invalidate a buffer still held by a consumer.

## 9. Integration boundary with Core Domain

The package library returns a validated temporary package result and package hash. It does **not**:
- create Field Dataset or Field Revision records;
- register Core Asset ownership;
- advance the current Field Revision pointer;
- perform SQLite transactions;
- decide Map ownership or descriptor authority;
- mark Branches/Checkpoints stale.

The Core Domain publication coordinator performs owner-context preflight, immutable Asset registration, typed Field Revision insertion and compare-and-swap current-pointer update under the established publication protocol. The package writer must not open its own SQLite transaction. A failed publication may leave an unreferenced immutable file for orphan reconciliation; it must not create a formal revision.

This handoff does not authorize changes to the adopted Map-wide, retroactive Field Revision semantics.

## 10. Windows/macOS CI and benchmark matrix

Run the same test corpus on supported Windows and macOS CI agents using the pinned .NET SDK/runtime configuration. Record OS version, architecture, SDK/runtime, build configuration and dependency versions.

Mandatory cross-platform checks:
- all golden fixtures open and all tiles decode to identical bytes;
- package and tile hashes match the same inventory;
- writer output is byte-identical across Windows/macOS for identical input and pinned deterministic codec;
- error codes and validation outcomes are stable across platforms;
- cancellation, concurrent reads and buffer lifetime tests pass on both platforms.

Benchmark representative elevation and categorical datasets at 128, 256 and 512 tile dimensions. Measure at minimum:
- reader open/index time and peak memory;
- random single-tile read latency (cold and warm where measurable);
- sequential full-field scan throughput;
- writer throughput and peak memory;
- package size/compression ratio for candidate codecs;
- concurrent read throughput and memory pressure;
- local edit/repackage cost for sparse and dense tile changes.

Do not invent target performance numbers before measuring representative workloads. Report raw results and methodology; architecture decisions follow the evidence.

## 11. Developer completion checklist

A developer handoff is complete only when all applicable items are delivered:

- [ ] Repository preflight note and implementation location.
- [ ] Dependency/license inventory and pinned package versions.
- [ ] Production reader and writer APIs with documented ownership/lifetime semantics.
- [ ] All three canonical golden packages included unchanged; inventory checks pass.
- [ ] All 28 P2-09-35 and 41 P2-09-36 vectors mapped to automated tests.
- [ ] Applicable P2-09-33 envelope cases covered.
- [ ] Additional fault-injection, fuzz/property, cancellation and concurrency tests.
- [ ] Typed WVPK error codes stable and documented.
- [ ] Reader verifies full structure before returning a handle; tile reads verify decoded bytes before exposure.
- [ ] Writer uses bounded-memory construction and production-reader reopen/verify.
- [ ] Whole-package hash is a separate streaming operation.
- [ ] Windows and macOS CI results attached.
- [ ] Cross-platform golden byte identity demonstrated for the selected deterministic profile.
- [ ] Benchmark report covers tile candidates and memory/random access/write workloads.
- [ ] No in-place mutation or Core Domain authority leakage.
- [ ] Known limitations and deferred codec/precision/tile decisions recorded.

## 12. Definition of done and status language

Use precise completion labels:

- **Slice implemented:** code exists and slice tests pass in the stated environment.
- **Reader implementation complete:** all reader, manifest, directory, tile, integrity and robustness acceptance tests pass.
- **Writer implementation complete:** deterministic bounded-memory writer, production-reader verification and writer acceptance tests pass.
- **Cross-platform verified:** the same required suite passes on Windows and macOS, including byte-identical output for the pinned deterministic profile.
- **Production-ready:** reader + writer + robustness + platform proof + performance/resource review are complete and all remaining technical gates are explicitly resolved or accepted.

Do not describe this document, the golden fixtures, or the 41 design vectors as implementation completion. A test is “passed” only when it has actually run and its environment/result is recorded.

## 13. Current deferred gates

The following remain separate decisions and must not be guessed by implementation:
- production codec and exact codec library/version;
- elevation precision/quantization and land/water production encoding;
- tile dimension selection based on benchmark;
- buffered seek versus memory-mapped I/O based on measurement;
- JCS and JSON Schema library selection, subject to conformance and license checks;
- any future multiresolution package format or GIS interchange support.

**Handoff conclusion:** the design package is sufficiently specified for the development team to begin S0–S6 reader implementation and test scaffolding. Production release remains gated by writer completion, robustness, Windows/macOS evidence, benchmark results and the deferred technical choices above.
