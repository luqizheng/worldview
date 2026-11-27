# PRA-05 Entry Assessment V0.1 — Post PR #41

- Date: 2026-11-27
- Repository: `luqizheng/worldview`
- Source audit baseline: `6ec34ead1540b1c7a95f0076784452b0fbf20ae3`
- Current master at assessment: `73da06f0ef143c9a2df9a690aaed0acf1268e6e6`
- Design contract: [Spatial Chunk & LOD Contract V0.1](../map-engine/world-view-render-spatial-chunk-and-lod-contract-v0.1.md)
- Integration plan: [ME-07 Rendering & Technical Architecture](../map-editor/world-view-me-07-rendering-and-technical-architecture-v0.1.md)
- Decision: **DESIGN INPUT READY / IMPLEMENTATION NOT ADMITTED**

## 1. Executive decision

PR #41 is merged. Spatial Chunk & LOD Contract V0.1 is now the authoritative presentation design input for PRA-05. The contract is not implementation evidence and does not mean that Spatial Index, Render Chunk, Fragment, LOD selection, batching, cache or benchmark infrastructure exists.

**PRA-05 implementation must not start yet.** ME-07 explicitly requires PRA-02/03 correctness and representative map fixtures. The BASELINE-01 source audit found that the current renderer publishes and consumes a real immutable snapshot, but the production scene is still blank and the renderer only clears the background. Coordinate transforms, spatial index, hit testing, snapping, map-content scene items and actual geometry drawing are missing. These are upstream capabilities, not details that PRA-05 should invent or absorb.

## 2. Baseline facts

| Area | Audited status | PRA-05 implication |
|---|---|---|
| PRA-01 snapshot pipeline | Implemented; production-connected by PR #40; immutable context, assembly, publisher, latest-request-wins and lifecycle tests | Reuse; do not rebuild |
| PRA-02 viewport/Skia host | Avalonia/Skia integration and snapshot consumption are implemented; lifecycle integration tests exist; GUI manual acceptance is not evidenced, macOS local GUI run is unverified | Close platform/lifecycle acceptance and real viewport behavior before relying on it as a proven runtime base |
| WP-30C camera/input | No pan/zoom input path; camera values are placeholders and renderer does not consume them | Must be implemented and verified before multi-scale/chunk behavior can be meaningfully validated |
| Map extent / coordinate transform | No adopted Map extent/bounds contract and no coordinate-transform mathematics | Design and implement the minimum authoritative spatial extent and transform semantics before chunk addressing |
| Scene item/content projection | Scene item model contains only the blank background item; no Domain map-content read projection or geometry drawing | Define the minimal immutable point/line/polygon scene schema and produce representative real map fixtures |
| PRA-03 spatial services | Spatial Index, geometry predicates/measurement, hit testing and snapping are not implemented | Hard entry gate for candidate query, chunk membership, clipping and interaction correctness |
| PRA-04 text/labels | Text shaping, glyph drawing and viewport-wide label layout are not implemented | Required before PRA-05 claims integrated label candidate/caching behavior; keep final label layout viewport-wide |
| PRA-05 contract | PR #41 merged; fixed-grid baseline, source identity/provenance, deterministic LOD inputs, cache invalidation and ownership boundaries specified | Design gate closed; implementation gate remains closed |
| Test fixtures | No representative authored map fixtures proving dense vector/raster workloads were identified in the audit | Create deterministic fixtures before benchmark claims or numeric tuning |

## 3. Contract boundaries to preserve

The merged contract establishes these constraints for implementation:

1. Render Chunks are presentation-derived partitions, not Domain storage units or Field Package tiles.
2. Spatial Index is a future Map Engine service; a Render Chunk must not become a second authoritative general-purpose index. Any temporary candidate query remains scoped and replaceable.
3. LOD selection is deterministic from Presentation Context, View Scale, Previous LOD State and LOD Policy Version. Previous LOD state is viewport presentation state; policy-version changes invalidate it.
4. Source identity and provenance survive fragmentation and LOD derivation. Rendering must not mutate formal geometry or semantic identity.
5. Final label placement is viewport-wide. Chunks may contribute candidates and caches but must not independently finalize labels per chunk.
6. Chunk dimensions, LOD thresholds, simplification tolerances, cache budgets and performance targets are evidence-driven, not guessed in advance.
7. Moving entities/units are future presentation examples only; they are not part of the first usable editor or a V1 simulation requirement.

## 4. Sequential prerequisite work

Execute one package at a time. Do not create parallel implementation lanes.

| Step | Work package | Required output | Exit gate |
|---:|---|---|---|
| 1 | PRA-02 / WP-30C acceptance and camera/input completion | Viewport lifecycle proof, pan/zoom input, typed camera/viewport transform, DPI/resize behavior | Windows and macOS evidence; no stale frame/resource lifecycle defect |
| 2 | DESIGN-EXTENT | Accepted Map extent/bounds semantics, coordinate units and relationship to chunk grid/viewport | Domain/Map Engine ownership is explicit; no renderer-owned formal bounds |
| 3 | DESIGN-SCENE | Immutable scene item schema for minimum point/line/polygon geometry, stable identity/provenance and minimum style tokens | Schema and snapshot fingerprint/order rules reviewed and frozen |
| 4 | WP-40-B1 | Authoritative time-zero map-content read projection, scene item preparation and basic Skia geometry rendering | Real fixture maps render correctly; no fabricated content; round-trip and pixel tests pass |
| 5 | PRA-03 | Projection/coordinate transform implementation, spatial index adapter/service, broad/narrow query, hit-test/snap correctness | Deterministic spatial query and interaction tests pass at relevant zoom/DPI |
| 6 | PRA-04 | HarfBuzz/package proof, glyph drawing, CJK/mixed-script shaping and viewport-wide label layout | Stable label inputs and deterministic layout tests pass |
| 7 | PRA-05 implementation design refinement | Map contract clauses to concrete modules/APIs; define chunk membership, fragment, LOD policy, cache ownership and invalidation against implemented upstream types | Independent design review accepts implementation-level contract mapping |
| 8 | PRA-05 implementation | Fixed-grid chunking, deterministic LOD selection, batching and bounded cache/invalidation | Correctness tests pass before benchmark tuning; no per-chunk final label layout |
| 9 | PRA-05 benchmark report | Representative sparse/dense vector and mixed vector/raster fixtures; measured memory/frame/prepare/query costs on Windows and macOS | Numeric thresholds and budgets are justified by reproducible measurements |

This is a dependency order, not a parallel schedule. A step may be split into smaller serial commits if that improves reviewability.

## 5. PRA-05 implementation admission checklist

PRA-05 may be admitted only when all required items below have evidence:

- [ ] PRA-02 viewport host lifecycle and resource disposal are verified on Windows and macOS.
- [ ] WP-30C camera, pan/zoom, resize and DPI transform behavior is implemented and tested.
- [ ] Map extent/bounds and Map Space coordinate semantics are accepted.
- [ ] Real map-content projection exists; the scene is no longer blank-only.
- [ ] Immutable scene item geometry and stable source identity/provenance are frozen.
- [ ] Basic vector geometry drawing and clipping are correct.
- [ ] PRA-03 spatial candidate query and precise geometry correctness are implemented.
- [ ] Deterministic representative map fixtures exist, including dense and mixed workloads.
- [ ] PRA-04 label shaping/layout integration is available for any label-bearing Chunk/LOD path.
- [ ] Contract-specific tests cover deterministic LOD state, policy invalidation, fragment identity, chunk seam behavior and bounded cache invalidation.

Until these gates pass, only contract review, implementation mapping and test-fixture planning are allowed under PRA-05. Do not add Chunk/LOD production code, cache abstractions or guessed numeric thresholds.

## 6. Explicit non-goals for this readiness task

- No production source-code changes.
- No new database tables, migrations or persistence for Render Chunk/LOD/cache state.
- No GPU-specific renderer, adaptive quadtree or generalized job scheduler.
- No dynamic world simulation or moving-entity implementation.
- No performance target invention before representative measurements.
- No changes to formal Domain geometry authority.

## 7. Baseline and status notes

The source audit was performed against `6ec34ea`. PR #41 merged only documentation and did not change production source, so the code findings remain applicable at current master `73da06f`. The audit's original statement that PR #41 was unmerged and G-05 blocked is historical; G-05 is now closed as a design-input gap. The PRA-05 implementation gate remains open.

The project is a single-user, single-machine Windows/macOS desktop application. Development work proceeds sequentially; only necessary runtime asynchronous lifecycle and thread-safety behavior is in scope.
