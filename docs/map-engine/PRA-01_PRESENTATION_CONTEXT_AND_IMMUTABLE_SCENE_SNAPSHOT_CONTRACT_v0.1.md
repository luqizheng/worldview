# PRA-01 — Presentation Context & Immutable Scene Snapshot Contract V0.1

- **Date:** 2026-11-21
- **Status:** Recommended implementation contract; requires implementation review before code is treated as conformant
- **Parent architecture:** [Production Renderer Architecture V0.2](PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md)
- **Work package:** PRA-01
- **Target:** C# / .NET 10; renderer-host-independent Presentation layer
- **Scope:** Presentation evaluation identity, immutable scene snapshot, asynchronous result consistency, atomic publication, stale-result handling, and lifecycle.
- **Non-goals:** Avalonia/Skia drawing implementation (PRA-02), projection algorithms (PRA-03), label layout (PRA-04), renderer batching/LOD (PRA-05), Core Domain persistence changes.

---

## 0. Purpose and decision summary

PRA-01 establishes the consistency foundation shared by every presentation subsystem. It ensures that geometry, Unit poses, labels, decorations, and styles shown in one frame all describe the same Map, Branch, Historical Time, World State revision, and presentation configuration.

The key invariant is:

> A rendered frame consumes exactly one complete immutable Scene Snapshot. Every component in that snapshot was produced for the same Presentation Context fingerprint. A partial, stale, or mixed-context snapshot is never published.

V1 uses a straightforward in-process design:
- immutable context and snapshot value objects;
- stable context fingerprint;
- monotonically increasing snapshot version;
- background preparation using immutable inputs;
- one atomic reference publication point;
- latest-request-wins publication for the active viewport;
- cancellation as an optimization, not as the correctness mechanism.

No message broker, distributed cache, database snapshot table, event bus, or general-purpose scheduling framework is required.

## 1. Ownership and dependency rules

### 1.1 Ownership

- **Core Domain / Domain Services:** authoritative Map, Branch, Historical Time interpretation, World State, and formal revision identity.
- **Presentation Coordinator:** requests authoritative inputs, creates Presentation Context, coordinates derived preparation, and decides whether a complete result is still publishable.
- **Presentation subsystems:** produce immutable, context-stamped derived outputs.
- **Scene Assembler:** validates compatible outputs and creates a complete immutable Scene Snapshot.
- **Snapshot Publisher:** atomically exposes the latest complete snapshot.
- **Renderer:** reads one snapshot reference and draws it; it does not participate in preparation or publication.

### 1.2 Forbidden dependencies

PRA-01 types must not depend on:
- Avalonia Controls or ViewModels;
- SkiaSharp types;
- database connections, ORM entities, or persistence repositories;
- mutable Core Domain entities;
- UI dispatcher or window objects.

The application boundary may adapt Domain Service results into immutable presentation inputs. The Presentation assembly must not bypass that boundary to query persistence.

### 1.3 Authority

Presentation Context and Scene Snapshot are derived presentation identities, not new domain authorities. Their IDs, versions, and fingerprints must never be written back as substitutes for Map, Branch, Event, Object, or World State identity.

## 2. Presentation Context

### 2.1 Required fields

A context is an immutable value object containing:

| Field | Required | Notes |
|---|---:|---|
| MapId | Yes | Stable domain identity |
| BranchId | Yes | Explicit branch; do not infer from UI selection later |
| HistoricalTime | Yes | Canonical typed domain time, not display text |
| WorldStateRevision | Yes | Stable revision/fingerprint for the reconstructed state |
| ProjectionDefinitionId | Yes | Explicit coordinate interpretation |
| ProjectionDefinitionVersion | Yes | Changes when transform semantics change |
| StyleRevision | Yes | Effective style/symbol configuration revision |
| ViewportGeneration | Yes | Changes when viewport-dependent inputs change |
| FontConfigurationRevision | Yes | Required when text shaping/layout participates |
| TerrainRevision | Conditional | Required when terrain influences this evaluation |
| SceneAssemblerVersion | Yes | Changes when assembly semantics change |
| ContextFingerprint | Derived | Stable digest of all relevant fields |

Additional fields may be added only when they materially affect derived presentation output. Do not include unrelated UI state merely because it is available.

### 2.2 Historical time

HistoricalTime must preserve the Core Domain's canonical semantics and precision. It must not be a localized date string, formatted label, floating-point frame counter, or wall-clock timestamp unless the domain contract explicitly defines it that way.

Two contexts that refer to the same displayed text but different canonical time values are different contexts.

### 2.3 Viewport generation

ViewportGeneration is a monotonically increasing generation assigned by the viewport/application adapter. Increment it whenever a presentation result can change because of:
- logical viewport width or height;
- camera/map-to-viewport transform;
- device scaling / DPI when it affects output;
- clipping or visible region;
- other explicitly declared viewport-dependent inputs.

Pure pointer hover or transient toolbar state must not increment this generation unless it changes scene output.

### 2.4 Fingerprint construction

ContextFingerprint is a deterministic digest over a canonical, versioned serialization of the context's relevant fields.

Requirements:
1. Field order is fixed.
2. Numeric and time values use invariant canonical encodings.
3. Null/absent values are distinguishable from default values.
4. Strings use a documented normalization/encoding policy; identifiers must not be culture-sensitive.
5. The serialization schema has an explicit version.
6. Hash algorithm is a standard collision-resistant digest available in .NET; SHA-256 is sufficient for V1.
7. The fingerprint must not include process ID, thread ID, wall-clock request time, random values, or object memory addresses.

The implementation must provide test vectors proving equal semantic contexts produce equal fingerprints and any relevant field change changes the fingerprint.

### 2.5 Equality

Context equality is based on the canonical context identity, not reference equality. The fingerprint is a fast identity token, but debug/test builds should be able to compare the canonical fields to detect accidental fingerprint omissions.

## 3. Immutable preparation inputs and outputs

### 3.1 Input envelope

Every preparation request carries:
- immutable PresentationContext;
- immutable World State presentation projection or stable read-only input reference;
- immutable view/style/font/terrain inputs required by that stage;
- cancellation token;
- request sequence/generation for the active viewport.

Workers must not read mutable ViewModels or query changing UI state while executing.

### 3.2 Output envelope

Every subsystem output is wrapped in a context-stamped immutable result:

- ContextFingerprint;
- subsystem name and output schema/version;
- immutable payload;
- diagnostics/timing metadata where useful;
- completion status.

An output must never be silently relabelled with a newer fingerprint after computation.

### 3.3 Subsystem results

Examples include:
- SpatialPresentationResult;
- AnimationPoseResult;
- LabelLayoutResult;
- DecorationLayoutResult;
- StyleResolutionResult.

The concrete payload contracts are defined by their own subsystem contracts. PRA-01 only requires immutable ownership and context identity.

### 3.4 Immutability requirements

After publication from a worker:
- collections cannot be mutated through retained writable references;
- arrays are privately owned or safely immutable;
- mutable builders are not exposed;
- referenced payload objects obey the same immutability rule;
- disposal of backend resources is not part of the immutable logical snapshot payload.

Use immutable collections or defensive ownership transfer. An interface typed as IReadOnlyList is not sufficient if the underlying List remains mutable elsewhere.

## 4. Scene Snapshot contract

### 4.1 Required metadata

A SceneSnapshot contains:
- ContextFingerprint;
- the immutable PresentationContext or a safe immutable identity projection;
- SnapshotVersion (monotonically increasing within the publisher lifetime);
- SceneAssemblerVersion;
- creation/completion diagnostics;
- immutable ordered scene content;
- optional diagnostic counts (feature count, label count, Unit count, decoration count).

### 4.2 Scene content

SceneSnapshot may contain renderer-neutral draw descriptions:
- geometry/path descriptions;
- symbol/marker descriptions;
- glyph-run or shaped-glyph descriptions according to the approved Text & Label contract;
- Unit visual poses and trails;
- decoration layout descriptions;
- layer/order/clip metadata;
- stable source provenance identifiers.

It must not contain:
- Avalonia controls, DrawingContext, ImmediateDrawingContext;
- SKCanvas, SKPath, SKPaint, SKTextBlob, GPU handles or other backend-bound resources;
- database handles or mutable Domain entities;
- live ViewModels, event subscriptions, delegates that close over mutable application state;
- unowned memory buffers whose lifetime is external and undocumented.

Backend-specific resources are prepared and cached behind a backend resource owner in later work packages. They are not part of the logical snapshot's cross-thread contract.

### 4.3 Ordering and determinism

Scene ordering must be explicit and stable. Equal context inputs must produce the same logical scene ordering and stable IDs. Parallel worker completion order must not influence final order.

Where exact floating-point bitwise identity is not practical across supported hardware, the contract must identify which outputs are semantically deterministic and which rasterization differences are tolerated. Do not hide nondeterminism behind unstable collection iteration.

## 5. Scene assembly

### 5.1 Required inputs

SceneAssembler accepts the expected PresentationContext and the required subsystem results.

### 5.2 Compatibility validation

Before assembly, it verifies:
- every required result has exactly the expected ContextFingerprint;
- required outputs are complete and successful;
- subsystem schema/algorithm versions are supported;
- no required input is missing;
- stable source references are valid for the assembly contract.

A mismatch is a rejected assembly, not a warning that may be ignored.

### 5.3 Optional outputs

Some scene layers may be optional (for example, a disabled decoration layer). Optionality must be explicit in the assembly request. Missing required output must never be interpreted as an empty successful result.

### 5.4 Failure behavior

If assembly fails:
- no partial SceneSnapshot is produced;
- the previous published snapshot remains available;
- the failure is recorded with context fingerprint and stage;
- retry is controlled by the coordinator, not by the renderer callback.

## 6. Atomic publication and snapshot reading

### 6.1 Publisher

The publisher owns:
- current immutable snapshot reference;
- monotonic SnapshotVersion counter;
- active request sequence/generation;
- atomic publish operation;
- diagnostics for accepted/rejected publications.

### 6.2 Publication algorithm

For a completed candidate snapshot:
1. Verify candidate assembly completed successfully.
2. Verify its ContextFingerprint equals the currently desired context fingerprint.
3. Verify its request sequence is still the latest active request for this viewport.
4. Verify candidate version is newer than the currently published version.
5. Atomically exchange the snapshot reference.
6. Record publication metadata.

If any check fails, reject the candidate and retain the current snapshot.

The latest-request-wins rule prevents a slow older computation from replacing a newer requested view. Cancellation should be requested for obsolete work, but correctness must not depend on cancellation arriving in time.

### 6.3 Version assignment

SnapshotVersion is assigned by the publisher, not by workers. It is strictly increasing for each successful publication within the publisher lifetime. Gaps are allowed if a candidate is built and rejected; reusing an earlier version is not.

### 6.4 Reader contract

A renderer or other reader:
- obtains the current snapshot reference once at the start of an operation/frame;
- retains that local reference for the complete operation;
- never separately reads geometry, labels, poses, or decorations from different publishers;
- never mutates the snapshot;
- never blocks waiting for a newer snapshot.

A reader may continue using an older snapshot while a newer one is being prepared or published.

### 6.5 Atomicity mechanism

Use a simple .NET atomic reference operation (for example, Volatile.Read and Interlocked.Exchange) over a reference to an immutable snapshot holder. Do not introduce locks around the render path unless a measured and documented requirement proves they are necessary.

## 7. Request lifecycle and stale work

### 7.1 State model

A request may move through:

`Created → Preparing → Assembling → Ready → Published`

Terminal alternatives:
- `Cancelled`
- `Superseded`
- `Failed`
- `RejectedStale`

These are request lifecycle states, not Core Domain entity statuses.

### 7.2 Latest-request-wins

For one active viewport, the newest requested context supersedes older unpublished requests. Different maps/windows may use independent coordinators/publishers; do not create a global singleton that causes one map to supersede another.

### 7.3 Cancellation

Cancellation is cooperative and used to reduce wasted work. Every stage must still validate context/request identity before its output can be assembled or published.

### 7.4 Last-known-good behavior

While a new context is preparing or fails, keep displaying the last complete published snapshot. The UI may show a loading/stale indicator outside the renderer. Never combine the old geometry with new labels to make partial progress appear.

If product semantics require hiding the old map immediately after switching Map/Branch, that is an application presentation policy; it must still clear or replace the complete snapshot atomically, not mutate its contents.

## 8. Concurrency and lifecycle

- One coordinator serializes request identity and publication decisions for one viewport.
- Independent preparation stages may run concurrently when their inputs are immutable.
- Workers never publish directly; only the coordinator/publisher does.
- No render callback waits on worker tasks or UI dispatch.
- Shutdown stops new requests, cancels outstanding work, prevents late publication, and releases coordinator-owned resources.
- A disposed publisher rejects all future publication attempts.
- Snapshot logical data remains valid while referenced; backend resource lifetimes are handled separately.

## 9. Diagnostics and observability

Each request should record:
- request sequence;
- ContextFingerprint;
- MapId / BranchId / HistoricalTime identity;
- start/end timestamps and stage durations;
- completion/failure/cancellation/supersession outcome;
- candidate SnapshotVersion, if assigned;
- publication accepted/rejected reason;
- subsystem result fingerprints and schema versions.

Counters:
- requested;
- completed;
- published;
- superseded;
- cancelled;
- rejected for stale context;
- assembly failures;
- publication failures.

Diagnostics must not require logging full map geometry or user content.

## 10. Required tests

### 10.1 Context identity
- equal contexts produce equal fingerprints across repeated runs;
- changing each required field changes the fingerprint;
- null and default values are distinguished;
- culture/time-zone changes do not alter canonical fingerprint;
- canonical HistoricalTime precision is preserved.

### 10.2 Immutability
- source builder mutation after result creation cannot mutate the result;
- scene collections cannot be mutated through leaked references;
- nested payloads follow the same ownership rule.

### 10.3 Assembly
- matching subsystem results assemble successfully;
- any single mismatched fingerprint is rejected;
- missing required output is rejected;
- explicitly optional output may be omitted;
- worker completion order does not change final scene ordering.

### 10.4 Atomic publication
- concurrent readers observe either the old complete snapshot or the new complete snapshot, never a mixture;
- each reader captures one reference for its full operation;
- snapshot versions strictly increase;
- older request completing later cannot replace newer published context;
- candidate for wrong context is rejected;
- failed/partial candidate leaves last-known-good snapshot intact.

### 10.5 Lifecycle
- cancellation before assembly;
- cancellation during assembly;
- supersession while worker is running;
- shutdown with outstanding workers;
- late worker completion after disposal;
- independent viewport coordinators do not supersede each other.

### 10.6 Determinism
- same immutable inputs produce equivalent logical scene output;
- stable ordering and stable source provenance are maintained;
- parallel execution schedule does not change final ordering.

## 11. Acceptance criteria

PRA-01 is complete only when:

1. PresentationContext and canonical fingerprint are implemented and covered by test vectors.
2. Context-stamped immutable result envelopes exist for the required stage outputs or a shared generic abstraction with equivalent guarantees.
3. SceneAssembler rejects mixed-context or incomplete required inputs.
4. SceneSnapshot is immutable by ownership, not merely by interface typing.
5. Publisher performs atomic latest-request-wins publication.
6. Readers can capture one snapshot reference without locks or blocking.
7. stale work, cancellation, shutdown, and independent viewport cases are tested.
8. diagnostics expose request and publication outcomes.
9. unit/integration tests pass on the supported target framework.
10. no Avalonia/Skia/database/ViewModel dependency leaks into PRA-01 contracts.

## 12. Deferred decisions

The following are intentionally not fixed by PRA-01:
- concrete hash wrapper type and serialization library, provided canonical test vectors pass;
- R-tree/BVH implementation;
- projection library;
- renderer batching representation;
- Skia resource cache implementation;
- UI loading indicator behavior;
- persistence of diagnostics;
- cross-process or distributed snapshot sharing.

These must not be used as reasons to complicate PRA-01's in-process consistency contract.

## 13. Change control

Any change to context identity, snapshot completeness, atomic publication, latest-request-wins behavior, or reader lifetime requires architecture review against Production Renderer Architecture V0.2. Internal class names, collection types, and implementation details may be chosen by developers when all invariants and tests remain satisfied.
