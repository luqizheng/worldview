# PRA-01 Implementation Audit V0.1

- **Audit date:** 2026-11-22
- **Repository:** `luqizheng/worldview`
- **Audited ref:** `master`
- **Audited tree:** `0d13b17b06007deb9637436295ecd3c91a0807bc`
- **Scope:** Actual C# source, solution/project composition, desktop composition root, map workspace/session code, tests, and PRA-01 design/handoff.
- **Verdict:** **NOT IMPLEMENTED** — the repository contains adjacent workspace/domain foundations, but no PRA-01 Presentation Context / immutable Scene Snapshot runtime.
- **Evidence boundary:** This is a source-tree and source-file audit. No local build or test command was executed as part of this audit. Existing README test/build claims are recorded as repository-reported evidence, not independently re-run evidence.

---

## 1. Executive conclusion

PRA-01 is not implemented in the audited repository revision.

The production solution contains four application projects:

1. `WorldView.Desktop`
2. `WorldView.Application`
3. `WorldView.Domain`
4. `WorldView.Infrastructure`

There is no Presentation or MapEngine production project, no renderer-neutral scene model, no presentation coordinator, no scene assembler, and no atomic scene snapshot publisher. The test solution contains Domain, Application, and Infrastructure test projects; there is no Presentation/MapEngine test project.

The current code does implement useful prerequisites:
- Project and Map Workspace session lifecycle;
- Map summary query;
- Map/Branch/Historical Time context resolution;
- Branch lineage validation;
- per-workspace mutable view state;
- cancellation of derived workspace work on context switch/close.

These are not equivalent to PRA-01. In particular, a validated workspace context is not a Presentation Context, a mutable WorkspaceViewState is not a SceneSnapshot, and a CancellationTokenSource is not latest-request-wins coordination or stale-publication protection.

**Implementation status:** no PRA-01 acceptance criterion is evidenced as implemented end-to-end. Some lifecycle primitives are reusable, but the presentation consistency mechanism must still be built.

## 2. Repository and project evidence

### 2.1 Solution composition

`WorldView/WorldView.sln` declares four production projects and three test projects:

| Area | Projects |
|---|---|
| Production | Desktop, Application, Domain, Infrastructure |
| Tests | Domain.Tests, Application.Tests, Infrastructure.Tests |

The audited source tree contains 28 production C# files and 13 test C# files. No source path or project is named Presentation, MapEngine, Renderer, or Scene.

### 2.2 Dependencies

- `WorldView.Desktop.csproj` references Avalonia 12.1.6, Desktop, Fluent theme, Inter fonts, MVVM toolkit and DI. It does not reference SkiaSharp directly or a Presentation/MapEngine project.
- `WorldView.Application.csproj` references Domain only.
- `WorldView.Domain.csproj` has no UI or third-party framework dependency.
- `WorldView.Infrastructure.csproj` references SQLite and Application.

The project graph is consistent with the current application/Core Domain foundation, not with a production rendering pipeline. The initial technical baseline explicitly keeps 2D/3D rendering, geometry, geospatial processing and spatial-index choices open pending evaluation.

### 2.3 Composition root

`WorldView/src/WorldView.Desktop/Program.cs` registers project/package persistence, Core Domain services, session registry and the project workspace ViewModel factory. It does not register any Presentation Coordinator, SceneAssembler, SceneSnapshotPublisher, renderer backend, or viewport service.

### 2.4 Desktop viewport

`WorldView/src/WorldView.Desktop/Views/ProjectWindow.axaml` is a text-oriented project/map/workspace management window. Its own comment states that map rendering belongs to stages after PRA-01/PRA-02. There is no map Canvas or custom draw operation in this view.

## 3. Existing adjacent code — what it does and does not prove

### 3.1 Workspace context

`WorldView.Application/Abstractions/IMapQueryService.cs` defines:
- `IMapQueryService`: list/get Map summaries;
- `IWorkspaceContextResolver`: resolve Map, Branch and canonical Historical Time;
- `ValidatedWorkspaceContext`: MapId, nullable BranchId and canonical time string.

This context does **not** contain the PRA-01 presentation identity fields such as WorldStateRevision, projection identity/version, style revision, viewport generation, font configuration revision, conditional terrain revision, assembler version or canonical context fingerprint.

### 3.2 Core Domain context resolution

`WorldView.Domain/Workspace/WorkspaceContextResolution.cs` resolves Map ownership, root history, Branch lineage and canonical Historical Time. Its documentation explicitly says this stage does not perform Event Replay or World State reconstruction.

This is valuable authoritative context resolution, but it does not provide the immutable World State presentation projection required by PRA-01.

### 3.3 Workspace session and view state

`WorldView.Application/Sessions/MapWorkspaceSession.cs`:
- owns one validated Map/Branch/time context;
- owns a per-workspace `WorkspaceViewState`;
- cancels `_derivedWorkCts` on context switch and close.

However:
- `WorkspaceViewState` is intentionally mutable;
- camera values, zoom, active tool and selected IDs are mutable;
- selected IDs use a mutable `HashSet<Guid>`;
- there is no immutable snapshot ownership transfer;
- there is no request sequence, latest-request-wins gate, stale context fingerprint check, scene assembly or atomic publication.

The cancellation token is a useful lifecycle primitive, but cancellation is cooperative and cannot substitute for correctness checks at assembly/publication time.

### 3.4 Tests

The current tests include:
- `WorkspaceContextRulesTests`: Map/Project ownership, Branch lineage, invalid Branch and Historical Time validation;
- `SessionUseCaseTests`: workspace independence, close/switch lifecycle and late context-resolution completion protection;
- Core Domain SQLite persistence and end-to-end package/session tests.

These tests do not exercise Presentation Context fingerprinting, immutable scene ownership, mixed-context rejection, deterministic scene assembly, atomic snapshot publication, reader capture semantics, stale scene rejection, latest-request-wins, independent viewport publishers or renderer read behavior.

## 4. PRA-01 acceptance matrix

| # | PRA-01 requirement | Audit status | Evidence / gap |
|---:|---|---|---|
| 1 | Immutable PresentationContext | **Not implemented** | No type/project; workspace context is a different, narrower contract |
| 2 | Canonical deterministic ContextFingerprint | **Not implemented** | No presentation fingerprint or canonical serializer |
| 3 | Context-stamped immutable result envelope | **Not implemented** | No presentation-stage result envelope |
| 4 | Immutable SceneSnapshot with stable ordering | **Not implemented** | No scene model or scene content collection |
| 5 | SceneAssembler validates required/optional outputs | **Not implemented** | No assembler or stage-result compatibility checks |
| 6 | Per-viewport atomic SceneSnapshotPublisher | **Not implemented** | No publisher or atomic snapshot reference |
| 7 | Latest-request-wins / stale-result rejection | **Not implemented** | No presentation request sequence or publication guard |
| 8 | Cancellation, shutdown and last-known-good snapshot | **Partial prerequisite only** | Workspace cancellation/close exists; no snapshot retention or late worker publication guard |
| 9 | Request/assembly/publication diagnostics | **Not implemented** | General application logging exists, but no PRA-01 diagnostics model/counters |
| 10 | PRA-01 unit/concurrency tests | **Not implemented** | Existing tests cover session/domain behavior, not presentation consistency |

No acceptance criterion is complete as a PRA-01 capability. Item 8 has adjacent lifecycle support only.

## 5. Findings and implementation risks

### PRA01-AUD-01 — No Presentation runtime boundary

**Severity: P0 implementation blocker**

There is no Presentation assembly or equivalent isolated module. Adding presentation contracts directly into Desktop ViewModels would violate the PRA-01 dependency constraints and make future renderer replacement and concurrency testing harder.

**Required action:** add a renderer-host-independent `WorldView.Presentation` project, referenced by Application (or an explicitly designed application adapter boundary) and consumed by Desktop. Keep Avalonia, Skia, database and ViewModel dependencies out of Presentation.

### PRA01-AUD-02 — No authoritative World State presentation input

**Severity: P0 upstream dependency**

Current Domain workspace resolution validates identity and time but explicitly does not reconstruct World State. Map query returns only summaries.

**Required action:** define the smallest Application/Domain read boundary that returns an immutable, revision-stamped presentation projection for a validated Map/Branch/Historical Time. Do not let Presentation query SQLite or infer World State from mutable UI/session data. This input boundary may be introduced as a narrow contract even if the full Event Replay implementation is a separately gated dependency; in that case rendering must remain blocked rather than use fabricated data.

### PRA01-AUD-03 — Mutable workspace state is not a snapshot

**Severity: P1 correctness boundary**

`WorkspaceViewState` is mutable and includes a mutable HashSet. It must remain editor/session transient state and must not be passed to background presentation workers as a live shared object.

**Required action:** capture the relevant values into immutable request inputs at request creation. Any mutable builders must transfer ownership or defensively copy before publication.

### PRA01-AUD-04 — No atomic publication or stale-result correctness guard

**Severity: P0 correctness blocker**

Workspace cancellation alone does not stop a late worker from publishing an obsolete result. There is currently no publisher, desired-context identity, monotonically increasing request sequence, or atomic exchange.

**Required action:** implement per-viewport coordination; validate both current context fingerprint and latest request sequence immediately before publication; atomically exchange one complete immutable snapshot reference. Keep cancellation as an optimization only.

### PRA01-AUD-05 — No rendering consumer

**Severity: P1 downstream dependency**

The current ProjectWindow has no map viewport. A reader-capture contract cannot be verified until a renderer consumer exists.

**Required action:** PRA-01 can implement/test a renderer-neutral snapshot publisher without Avalonia/Skia. PRA-02 should add the viewport consumer and prove one-reference-per-frame reads; do not pull backend drawing into PRA-01.

### PRA01-AUD-06 — No PRA-01-specific tests or audit evidence

**Severity: P1 quality gate**

Current test coverage does not include PRA-01 acceptance scenarios. Repository README reports 139 tests passing on Windows for the current baseline, but this audit did not execute those tests and none of the listed tests proves PRA-01 behavior.

**Required action:** add deterministic unit and concurrency tests from the PRA-01 handoff. Use barriers/task completion sources rather than timing sleeps. Record exact build/test commands and results in an implementation report.

## 6. Recommended implementation sequence

Keep the work package small and avoid coupling it to Skia, projection, labels or spatial indexing.

1. **PRA-01-S0 — Project boundary and context primitives**
   - Add `WorldView.Presentation` and test project.
   - Define immutable PresentationContext and canonical fingerprint serialization/version.
   - Add test vectors for each identity field, null/default distinction, culture/time-zone independence and Historical Time precision.
2. **PRA-01-S1 — Immutable result and scene model**
   - Define context-stamped result envelope, immutable scene item contracts and stable ordering keys.
   - Prove ownership against mutation of source builders and nested payloads.
3. **PRA-01-S2 — Scene assembly**
   - Define required/optional stage declarations.
   - Reject missing required outputs, failed outputs and any context/schema mismatch.
   - Ensure deterministic order independent of worker completion order.
4. **PRA-01-S3 — Publisher and coordinator**
   - Per-viewport request sequencing, desired-context tracking, monotonic published version and atomic reference exchange.
   - Reject stale/superseded/wrong-context candidates; preserve last-known-good snapshot.
5. **PRA-01-S4 — Lifecycle and diagnostics**
   - Cancellation, shutdown, disposal and late completion tests.
   - Independent viewport tests and publication diagnostics/counters.
6. **PRA-01-S5 — Integration and implementation report**
   - Wire a minimal Application adapter without exposing UI/persistence to Presentation.
   - Run Debug/Release builds and the complete deterministic test matrix.
   - Leave Avalonia/Skia drawing to PRA-02.

**Dependency gate:** if authoritative World State reconstruction/read projection is not yet available, implement the renderer-neutral contracts and tests with explicit test fixtures only. Do not connect fabricated or placeholder map content to the production viewport.

## 7. Scope guardrails

PRA-01 should not:
- add Skia/Avalonia drawing APIs or backend resource handles;
- implement projection, geometry indexing, hit-testing or snapping;
- implement label shaping/layout or animation algorithms;
- add persistence tables for presentation snapshots;
- mutate Core Domain schemas or historical semantics;
- turn WorkspaceViewState into a second domain model;
- add a message bus, distributed cache, generalized scheduler or plugin framework.

## 8. Sources inspected

The audit inspected the repository tree at the audited commit and the following source/design artifacts:

- `WorldView/WorldView.sln`
- `WorldView/src/WorldView.Desktop/Program.cs`
- `WorldView/src/WorldView.Desktop/Views/ProjectWindow.axaml`
- `WorldView/src/WorldView.Desktop/WorldView.Desktop.csproj`
- `WorldView/src/WorldView.Application/WorldView.Application.csproj`
- `WorldView/src/WorldView.Domain/WorldView.Domain.csproj`
- `WorldView/src/WorldView.Infrastructure/WorldView.Infrastructure.csproj`
- `WorldView/src/WorldView.Application/Abstractions/IMapQueryService.cs`
- `WorldView/src/WorldView.Application/Sessions/MapWorkspaceSession.cs`
- `WorldView/src/WorldView.Application/Sessions/ProjectSession.cs`
- `WorldView/src/WorldView.Domain/Workspace/WorkspaceContextResolution.cs`
- `WorldView/tests/WorldView.Domain.Tests/Workspace/WorkspaceContextRulesTests.cs`
- `WorldView/tests/WorldView.Application.Tests/UseCases/SessionUseCaseTests.cs`
- `WorldView/docs/architecture/initial-tech-baseline.md`
- `WorldView/docs/roadmap/tech-assessment-backlog.md`
- `docs/map-engine/PRA-01_PRESENTATION_CONTEXT_AND_IMMUTABLE_SCENE_SNAPSHOT_CONTRACT_v0.1.md`
- `docs/map-engine/PRA-01_DEVELOPER_HANDOFF_v0.1.md`
- `docs/map-engine/PRODUCTION_RENDERER_ARCHITECTURE_v0.2.md`

The repository's current README reports 139/139 Windows tests passing for the CD-01 baseline. That result is quoted as repository documentation only; it was not re-executed during this source audit.

---

## 9. Final disposition

**PRA-01 status: NOT IMPLEMENTED.**

The correct next action is to schedule PRA-01 implementation from S0 through S5. Do not advance to PRA-02 renderer integration on the assumption that immutable scene publication already exists. The current Core Domain/session foundation can be reused, but it does not satisfy the presentation consistency contract.
