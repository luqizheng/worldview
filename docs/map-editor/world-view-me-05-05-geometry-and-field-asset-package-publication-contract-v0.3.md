# World View — ME-05-05 Geometry & Field Asset Package and Publication Contract

- Version: 0.3
- Date: 2026-11-21
- Status: Recommended V1 Asset lifecycle integration contract; payload formats and provider execution remain open gates
- Depends on: ME-05-01 V0.5; ME-05-02 V0.1; ME-05-04 V0.1; P2-09-11 V0.11; P2-09-12 V0.6; P2-09-19 V0.2; P2-09-25 V0.1; ME-02-F V0.4

## 1. Purpose

Define the shared lifecycle boundary for immutable vector-geometry Assets and Map-owned Field Revision packages. Both use the Core Asset Store's immutable content-addressed byte storage, but they remain different domain payload types with different owners, manifests, revision semantics and query services.

**Shared byte storage does not mean shared domain authority.** A Geometry Component Asset is not a Field Dataset; a Field Revision package is not a collection of semantic Objects; a Derived Artifact is not automatically a Core Asset.

This document integrates existing authority and transaction contracts. It does not select a raster format, compression codec, tile size, geometry library or physical directory layout.

## 2. Three storage classes

| Class | Content | Authority / lifecycle | May be formal world input? |
|---|---|---|---|
| Core Asset | Immutable payload bytes referenced by formal Core Domain records | Core Asset Store + typed Core Domain owner links | Yes, only through a typed owner/reference |
| Derived Artifact | Immutable calculation output with input fingerprint, algorithm provenance and freshness | Calculation subsystem / ME-02-F | No; adoption is a separate Core Domain publication |
| Presentation cache | Projected geometry, tessellation, spatial indexes, scene snapshots, GPU buffers, labels and hit-test structures | Map Engine / Renderer; disposable and rebuildable | Never |

No generic Asset owner table, arbitrary Asset CRUD endpoint or single mutable "map data file" is introduced.

## 3. Vector Geometry Asset contract

### 3.1 Unit of publication

Recommended V1: one immutable Geometry Asset represents one formal Geometry Component payload state. A multipart geometry remains one component payload when the Object Type permits it; do not split its parts into unrelated components just because the renderer or storage package wants to tile them.

The Core Domain owner payload retains component ID, role, owning Object, Asset ID/hash, and the applicable Object Type / Command / semantics versions. The Asset contains geometry bytes and coordinate interpretation metadata, not mutable Object identity or role authority.

### 3.2 Envelope and payload

P2-09-25 recommends a versioned canonical World View JSON envelope. Its logical fields include:
- format ID and format version;
- spatial-reference ID;
- axis order and coordinate unit;
- dimensionality and geometry family;
- precision-profile ID;
- canonical geometry body (Point, LineString, Polygon, multipart or supported Curve);
- optional source/provenance manifest.

Canonical UTF-8, stable key ordering, invariant numeric serialization, semantic array ordering and content hashing are required by the adopted serializer version. Unknown geometry/curve types are rejected, not silently downgraded.

The envelope does not duplicate mutable Component ID, role, Object Type or history context. Those belong to the Core Domain owner payload.

### 3.3 Publication and edit behavior

- A component adjustment/refinement publishes a new immutable Asset while preserving Component ID and role.
- Adding a component allocates a new Component ID and publishes its first Asset.
- Removing a component ends membership in the selected formal context; it does not mutate or immediately delete its former Asset.
- A formal geometry conversion (including coordinate-reference conversion) publishes a new Asset with explicit provenance.
- Project-level Shared Object definition edits, Map Initial State authoring and post-zero History Commands use their distinct Core Domain write paths.

The exact canonical geometry grammar, numeric profiles and serializer compatibility tests remain P2-09-25 validation gates.

## 4. Field Revision package contract

### 4.1 One revision, one immutable package

A Field Revision owns one immutable logical payload package. The package may contain a manifest and multiple tile/chunk payloads; it is not assumed to be one giant in-memory array or one specific raster file.

The canonical field manifest is authoritative for interpretation and declares, at minimum:
- Field Dataset ID and Field Revision ID in the Core Domain owner metadata;
- field key, Field Descriptor schema version and semantics ID;
- Map ID / Map Space ID;
- spatial-reference identity;
- coverage and edge semantics;
- resolution, grid alignment and sample registration;
- value type, units and vertical reference where relevant;
- no-data, unknown and outside-coverage distinctions;
- tile/chunk manifest and per-payload fingerprints;
- provenance, derivation information and package content fingerprint.

The logical Field Package and tiling rules are now defined by ME-05-07 V0.3: one seekable immutable package Asset per Field Revision, canonical manifest, deterministic tile directory, independently decodable tile payloads, bounded random reads and streaming new-package construction for local edits. The byte-level header/directory are specified by ME-05-08 V0.1. Parser/writer fixtures, production codec, tile-size benchmark and elevation precision profile remain implementation gates. A tile boundary is never a semantic coastline, region boundary, watershed divide or no-data boundary by implication.

### 4.2 Domain authority

Core Domain owns Field Dataset identity, Field Revision metadata, current pointer, schema/semantics compatibility, publication audit and typed Field Revision → Core Asset ownership. Map Engine owns field decoding, sampling, tiling algorithms and derived calculations; it cannot change published bytes or current revision pointers.

A Field Revision is a Map Source Field Baseline correction. Per ME-05-01 V0.5, publication affects every Branch and Historical Time of its owning Map, retroactively. It is not a History Command. Another Map is unaffected even when identical Asset bytes are deduplicated.

## 5. Asset identity, deduplication and retention

- Asset identity identifies immutable bytes; it is not a Geometry Component ID, Field Dataset ID or Field Revision ID.
- Content-addressed deduplication may allow multiple typed owners to reference identical immutable bytes.
- Typed owner references remain distinct and enforce their own scope and semantics. Shared bytes never make two Field Datasets or Geometry Components the same domain entity.
- Hashing uses the Core Asset Store's declared algorithm/version over exact canonical bytes; this document does not invent a new hash scheme.
- Published bytes are never edited in place.
- Retain Assets while referenced by current definitions, Initial State, Commands, Field Revisions, Checkpoints, audit evidence or retained calculation lineage.
- Garbage collection must first establish that no authoritative typed link or retention obligation remains. Use a safe grace/reconciliation period; do not delete by filename age or because an Asset is not the current pointer.
- A Derived Artifact or renderer cache must not be registered as a formal Core Asset unless the creator explicitly adopts its exact bytes through the appropriate Core Domain publication path.

## 6. Publication protocol

The common protocol is staged and owner-specific:

1. **Stage:** write a temporary payload/package outside formal authority; capture source context, expected revision/fingerprint and provenance.
2. **Validate:** check manifest/schema, spatial reference, coverage, payload integrity, units, precision, component role or Field Descriptor rules, and any specialist constraints.
3. **Finalize bytes:** canonicalize where required, write immutable Asset bytes, read back and verify byte length/content hash/package manifest.
4. **Commit metadata:** in a short project-local SQLite transaction, create/resolve Core Asset metadata, insert the typed owner reference and update the appropriate formal owner metadata/pointer or append the registered History Command. Use the relevant uniqueness and compare-and-swap conditions.
5. **Confirm:** report formal success only after the transaction commits and the owner reference is re-readable.
6. **Invalidate derived data:** mark only affected calculation outputs, Checkpoints and presentation caches stale according to their dependency fingerprints; do not invalidate unrelated Map data.

The protocol shares the Asset Store mechanics but not a generic cross-domain "publish any bytes" command. Core Domain Domain Services own the final transaction for their entity type.

## 7. Concurrency and stale-write rules

| Target | Required concurrency guard | Stale behavior |
|---|---|---|
| Field Dataset current revision | Expected current Field Revision ID / CAS | Reject publication; rebase or review staged edit |
| Project-shared Object definition | Expected shared-definition revision/fingerprint | Reject stale definition edit |
| Map Initial State geometry | Expected baseline revision/fingerprint | Reject and rebuild preview |
| Post-zero geometry Command | Expected source state/fingerprint plus History/fork context | Reject stale Command; never overwrite current state |
| Asset bytes | Content hash and immutable write semantics | Existing identical bytes may be reused; conflicting bytes for same identity/hash are integrity failure |

No silent last-write-wins for formal geometry or source fields.

## 8. Failure and recovery

| Failure point | Required behavior |
|---|---|
| Stage/validation failure | No Core Domain metadata or current pointer changes |
| Asset write fails | No typed owner reference |
| Asset write succeeds, SQLite transaction fails | Leave no formal reference; reconcile orphan bytes safely |
| CAS/fingerprint mismatch | Reject; retain staging for rebase/review if safe |
| Typed owner insert fails | Roll back formal metadata transaction; Asset may remain orphaned |
| Read-back hash mismatch | Quarantine/reject payload; never publish |
| Process stops after DB commit but before UI acknowledgement | Re-read formal owner state and reconcile idempotently; do not duplicate revision/Command |
| Cache/derived rebuild fails | Formal source remains published; derived result is stale/failed and independently rebuildable |
| Partial tile package missing/corrupt | Reject entire Field Revision publication; do not publish incomplete coverage as complete |

The durable commit point is the Core Domain metadata transaction plus verified typed references, not the existence of a staged file or Asset blob alone.

## 9. Verification scenarios

| ID | Scenario | Expected |
|---|---|---|
| AS-001 | Same canonical geometry bytes published twice | One immutable content identity may be reused; typed owners remain distinct |
| AS-002 | Geometry edit | New Asset, same Component ID/role |
| AS-003 | Multipart component stored | One formal component identity; renderer tiling does not create more components |
| AS-004 | Unknown geometry format version | Reject or require explicit migration; never reinterpret |
| AS-005 | Field package contains multiple tiles | One Field Revision with complete manifest and tile fingerprints |
| AS-006 | Missing tile in package | Entire publication rejected |
| AS-007 | Identical bytes referenced by two Maps' Field Revisions | Each Map retains its own Dataset/Revision identity and pointer |
| AS-008 | Update one Map Field Dataset | Other Map unchanged |
| AS-009 | Field revision publishes | All Branches/Times of owning Map see new source baseline; dependent calculations/checkpoints invalidated |
| AS-010 | Stale expected Field Revision | CAS rejects; newer pointer preserved |
| AS-011 | Asset bytes written, metadata transaction fails | No formal reference; safe orphan reconciliation |
| AS-012 | Derived geometry adopted | Exact accepted bytes published through Core Domain; Derived Artifact remains provenance |
| AS-013 | Unreferenced asset garbage collection | No deletion while any typed owner/audit/checkpoint reference remains |
| AS-014 | UI crashes after successful commit | Reconciliation finds existing publication; no duplicate formal revision |
| AS-015 | Asset hash read-back differs | Reject and quarantine; no publication |

## 10. Open technical gates

1. Finalize vector geometry JSON grammar and canonical serializer fixtures.
2. Finalize Field Revision package manifest and tile/chunk layout, compression and random-access strategy.
3. Benchmark vector and field payload sizes, load patterns and memory use across World/Region/Locality/Site workloads.
4. Validate Core Asset and typed-owner transaction behavior on the bundled SQLite provider, including composite-FK/trigger constraints from P2-09-11/12.
5. Implement safe orphan reconciliation and reference-aware retention/garbage collection.
6. Execute Windows/macOS interruption, corruption, concurrency and read-back integrity tests.
7. Keep derived-artifact storage and renderer cache lifecycles separate from Core Asset publication.

This document does not claim that any package format, provider behavior, implementation or test has been completed.

## References

- ME-05-07 Field Payload Package & Tiling Contract V0.3; ME-05-08 Field Package Binary Envelope & Directory Specification V0.1.

- ME-05-01 Field Dataset Authority & Persistence Contract V0.4.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- P2-09-11 Core Domain Physical Schema V0.11.
- P2-09-12 SQLite Provider / Transaction Proof V0.6.
- P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- ME-02-F Calculation Contracts V0.4.
