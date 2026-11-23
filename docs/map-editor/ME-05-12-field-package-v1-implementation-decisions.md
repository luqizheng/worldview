# Field Package V1 Implementation Decisions

**Status:** Implementation baseline for Issue #14  
**Target:** WVFLDPK 1.0 / WorldView 1.0  
**Runtime:** .NET 11, Windows and macOS  
**Depends on:** PR #11 EF Core / Field Payload baseline

This document supersedes conflicting implementation details in ME-05-10 V0.1 and ME-05-11 V0.1 where they conflict with the project-level single-machine constraint or the decisions below. The 72-byte header, 16-byte directory header, 72-byte directory entry, little-endian fields, absolute offsets, 8-byte alignment, zero padding, canonical manifest contract and golden/conformance vectors remain normative.

## 1. Single-machine execution model: no concurrent package operations

WorldView is a single-machine desktop application. Field Package V1 does not introduce concurrent package reads, concurrent tile decoding, parallel writer pipelines, worker pools, or thread-safe reader instances.

The following earlier recommendations are superseded:

- ME-05-10 §4: concurrent positional reads are not a V1 requirement.
- ME-05-10 §9: concurrent tile decodes = 2 is removed. The effective decode concurrency is one.
- ME-05-10 §11: the reader does not promise concurrent evaluation or safe simultaneous operations.

V1 execution rules:

1. One PackageReader instance accepts one active operation at a time.
2. Tile reads are sequential. A caller must complete one read before starting the next.
3. The reader is not thread-safe; this is documented rather than hidden behind a lock or semaphore.
4. Writer tile ingestion and encoding are sequential and deterministic.
5. Cancellation is checked between I/O, validation and decode stages. Cancellation never publishes a partial package.
6. A single seekable source is sufficient. V1 does not need positional-read APIs or memory mapping.
7. Tests verify sequential random access, repeated reads, disposal and cancellation. They do not test concurrent reads.

This is an explicit product constraint, not a temporary performance limitation.

## 2. JSON, JCS and JSON Schema dependency review

### 2.1 Canonicalization

Keep the existing dependency:

- Package: MackySoft.Json.Canonicalization 0.1.0
- Purpose: RFC 8785 JCS canonical UTF-8 bytes and strict JCS input validation
- License: MIT (the package family repository is MIT-licensed)
- Decision: retain for the current Field Manifest canonicalizer; do not add a second JCS implementation to the Domain layer.

The canonicalizer remains responsible for duplicate-property rejection, I-JSON/binary64 number rules, Unicode validity and exact canonical-byte comparison. The manifest reader must compare original bytes with canonical bytes; it must never normalize a non-canonical package and continue.

### 2.2 JSON Schema validation

Use:

- Package: Corvus.Text.Json 5.8.11
- Purpose: JSON Schema Draft 2020-12 validation and optional parsed-document support
- License: Apache-2.0
- Runtime mode: Corvus runtime evaluator with code generation disabled
- Schema source: the repository's P2-09-34 schema embedded as an assembly resource

The runtime evaluator is compiled once from the embedded schema and reused for sequential validations. Set JsonSchemaCodeGeneration.Disabled; V1 does not require runtime IL generation, Roslyn source generation, a generated model hierarchy, or a separate validator process.

The evaluator's dialect is Draft 2020-12. Schema resolution is local-only for this manifest: external network resolution is disabled. Unknown remote references fail closed.

### 2.3 Rejected validator option

JsonSchema.Net was reviewed but is not selected. Its NuGet license metadata alone is not sufficient for a commercial distribution decision: the upstream binary distribution also carries a separate EULA / Open Source Maintenance Fee condition. WorldView avoids that distribution uncertainty and uses the Apache-2.0 Corvus runtime evaluator instead.

### 2.4 Newtonsoft.Json

Newtonsoft.Json remains the general JSON.NET choice where the Application layer needs mutable JSON object models or ordinary serialization. The package reader's canonicality and JSON Schema checks are separate format-validation responsibilities; they do not change the project's general JSON.NET preference.

## 3. Manifest validation pipeline

The reader must apply these checks in order:

1. Enforce the manifest byte budget (1 MiB), strict UTF-8, no BOM and no trailing newline/bytes.
2. Parse the JSON and reject duplicate property names.
3. Require byte-identical RFC 8785 JCS canonical UTF-8 using FieldManifestCanonicalizer.RequireCanonicalUtf8.
4. Validate the parsed instance against the embedded P2-09-34 Draft 2020-12 schema using Corvus.Text.Json with code generation disabled.
5. Apply semantic checks that JSON Schema cannot express or that depend on external immutable context: descriptor identity, owner context, Map Space, Spatial Reference, coordinate precision profile, field semantics, plane ordering, mask semantics, codec registry uniqueness and tile-grid arithmetic.
6. Only after all checks succeed may the reader expose a validated manifest to the directory reader.

The schema is embedded from the canonical repository document, not copied into a second hand-maintained schema string. Tests verify the embedded resource bytes equal the canonical schema file.

## 4. Fixture-only profiles and production writer gate

The schema intentionally distinguishes value_encoding.profile_scope = golden_fixture_only from production. A fixture profile is valid for conformance tests and golden package reproduction; it is not automatically a supported production encoding.

V1 behavior:

- The conformance reader may be explicitly configured to accept golden_fixture_only profiles for test fixtures.
- The normal production reader rejects a fixture-only profile unless an explicit import/test policy allows it.
- The fixture writer is test-only and can reproduce the canonical golden fixtures byte-for-byte.
- The user-facing production writer remains disabled until production profiles are formally selected and registered.

The following are not inferred from fixture values and remain product decisions:

1. Elevation sample precision, quantization and maximum numeric error budget.
2. Land/water category code assignments and unknown/outside-coverage semantics for production data.
3. Production codec ID, codec name/version, parameters and deterministic-output guarantee.
4. Production tile dimensions after representative Windows/macOS workload benchmarks.

No float32, uint8, codec-0-only, or 256 × 256 fixture setting is promoted to a production default by implementation convenience.

## 5. Sequential resource policy

V1 policy defaults:

| Resource | V1 limit |
|---|---:|
| Manifest bytes | 1 MiB |
| Directory entries | 4,000,000 |
| Single decoded tile | 256 MiB |
| Single stored tile | 256 MiB |
| Concurrent tile decodes | 1 (no concurrent operation support) |
| Temporary spool free-space reserve | 2 GiB |
| Package length | Int64 checked arithmetic and available-disk-space bound |

Limits are checked before allocation, seek or decode. A policy rejection is a typed failure; the reader must not skip validation, reduce precision, omit a tile or return partial bytes.

## 6. Import/export and authority boundary

- Field Package remains an import/export interchange artifact, never a mandatory runtime storage container.
- SQLite remains authoritative for Dataset / Revision identity, ownership, lifecycle, lineage, current pointer and payload metadata.
- Field Payload BLOB / ManagedFile bytes remain authoritative runtime content.
- Import validates package structure and manifest, decodes only the requested tile stream as required by the importer, then publishes raw/decoded Field Payload through the existing Field Payload publication protocol.
- Export reads an immutable Field Revision payload through the storage-kind-neutral reader and writes a new temporary package.
- The package writer never opens SQLite or changes a current pointer.
- A failed package export leaves only a temporary artifact that can be safely removed. A failed Field Payload DB publication follows the existing orphan-recovery contract.

## 7. Required implementation sequence

1. Embed the canonical manifest schema and add the Corvus runtime validator.
2. Implement typed WVFLDPK header/directory readers and checked arithmetic helpers.
3. Implement dense-grid directory validation and sequential tile reads for codec 0.
4. Implement tile decoded-length, mask and hash verification.
5. Implement full-package streaming hash verification.
6. Regenerate the three canonical golden binary fixtures from the normative fixture manifests and compare exact bytes.
7. Implement a bounded-memory fixture writer for golden reproduction only.
8. Add all P2-09-35 manifest vectors and P2-09-36 package conformance vectors, plus truncation, overflow, corruption, resource-limit and cancellation tests.
9. Run Windows/macOS Debug and Release CI.
10. Keep the production writer disabled until the production precision/category/codec decisions in §4 are confirmed.

No implementation, benchmark or platform pass is claimed by this baseline document.
