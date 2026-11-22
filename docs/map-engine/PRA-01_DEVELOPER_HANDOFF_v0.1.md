# PRA-01 Developer Handoff V0.1

- **Date:** 2026-11-21
- **Status:** Ready for implementation planning
- **Read first:** `PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md`
- **Detailed contract:** `PRA-01_PRESENTATION_CONTEXT_AND_IMMUTABLE_SCENE_SNAPSHOT_CONTRACT_v0.1.md`
- **Work package:** Presentation Context & Immutable Scene Snapshot

---

## 1. Objective

Implement the smallest reliable in-process foundation that guarantees every rendered frame reads one complete immutable Scene Snapshot, and every component in that snapshot belongs to one identical Presentation Context.

This is a Presentation-layer implementation task. Do not implement Avalonia drawing, Skia resources, projection algorithms, spatial indexes, label placement, animation algorithms, or Core Domain persistence changes in PRA-01.

## 2. Required deliverables

1. Immutable `PresentationContext` with canonical identity and deterministic `ContextFingerprint`.
2. Context-stamped immutable result envelope for presentation-stage outputs.
3. Immutable `SceneSnapshot` and explicit scene ordering.
4. `SceneAssembler` that validates required/optional outputs and rejects mixed fingerprints.
5. Per-viewport `SceneSnapshotPublisher` with monotonic version and atomic reference exchange.
6. Latest-request-wins request coordination and stale-result rejection.
7. Cancellation, shutdown, disposal, and last-known-good behavior.
8. Diagnostics for request, stage, assembly, and publication outcomes.
9. Unit and concurrency tests covering the acceptance criteria.
10. Short implementation report documenting files, public/internal APIs, tests, build commands, results, and any deviations.

## 3. Implementation constraints

- Target the repository's currently approved .NET target; do not upgrade frameworks or packages as part of PRA-01.
- Keep PRA-01 contracts free of Avalonia, SkiaSharp, database/ORM, ViewModel, and UI dispatcher references.
- Do not add a distributed cache, message broker, event bus, or generalized workflow engine.
- Workers receive immutable inputs and never publish directly.
- Only the coordinator/publisher may decide whether a candidate is current and publishable.
- Cancellation is an optimization; context and request identity checks are the correctness guard.
- A reader captures one snapshot reference once and uses it for its entire read/draw operation.
- A failed or stale candidate never replaces the last complete snapshot.
- Do not edit unrelated user changes, glossary files, migration material, or unrelated directories.
- Do not move/delete existing source documents as part of this task.

## 4. Suggested implementation shape

Names are suggestions, not mandated public API:

- `PresentationContext`
- `ContextFingerprint`
- `PresentationResult<T>`
- `SceneSnapshot`
- `SceneAssembler`
- `SceneSnapshotPublisher`
- `PresentationRequestCoordinator`
- `PresentationRequestId` / request sequence
- immutable scene item collection and stable ordering key

Prefer a small number of explicit types over a generic framework. Use immutable collections or defensive ownership transfer; `IReadOnlyList<T>` over a mutable List is not sufficient if another reference can still mutate it.

Use .NET atomic reference primitives such as `Volatile.Read` and `Interlocked.Exchange` where appropriate. Keep locks and waits out of the render-reader path.

## 5. Required tests

At minimum, implement the test matrix in the PRA-01 contract:

- fingerprint stability and field sensitivity;
- culture/time-zone-independent canonicalization;
- immutable ownership of input/output collections;
- mixed-context and missing-required-output assembly rejection;
- optional-output handling;
- atomic old-or-new snapshot visibility under concurrent readers;
- monotonic versions;
- slow old request cannot overwrite newer context;
- stale candidate rejection;
- cancellation and shutdown with in-flight workers;
- late completion after disposal;
- independent viewport coordinators;
- deterministic scene ordering regardless of worker completion order.

Tests must be deterministic. Avoid arbitrary sleeps; use barriers, task completion sources, or controlled schedulers to force race order.

## 6. Definition of done

PRA-01 is done only when:
- all required types and behavior are implemented;
- all tests above pass;
- Debug and Release builds pass;
- no prohibited dependency leaks into PRA-01;
- no partial or stale snapshot can be published;
- the developer report lists actual commands and results;
- the implementation is committed on the intended branch with a clear commit message.

Do not claim macOS runtime or renderer integration validation in this work package; those belong to later gates.

## 7. Explicitly out of scope

- Avalonia `ICustomDrawOperation` and Skia lease management (PRA-02).
- Glyph drawing, HarfBuzz integration, TextLayout, label placement (PRA-04).
- Spatial projection/index/hit-testing (PRA-03).
- Batching, LOD, backend resource cache (PRA-05).
- Animation Runtime implementation (PRA-06).
- Terrain and Decorations (PRA-07/PRA-08).
- UI design or Figma work.

## 8. Developer completion report format

Report:
1. commit SHA and branch;
2. files added/changed;
3. architecture mapping for each invariant;
4. build commands and exact results;
5. test count and coverage matrix;
6. concurrency/race tests and how they force interleavings;
7. any deviation or unresolved issue;
8. confirmation that unrelated working-tree changes were preserved.
