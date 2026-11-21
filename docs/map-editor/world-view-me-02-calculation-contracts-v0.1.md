# World View — ME-02-F Calculation Contracts

- Version: 0.1
- Date: 2026-11-21
- Status: **Calculation Architecture Working Baseline** — logical contracts and lifecycle recommendations; concrete field formats, algorithms, thresholds, worker sizing and storage layout remain implementation/technical-validation gates
- Parent: ME-02 Terrain & Natural Geography Editor V0.3
- Depends on: ME-01-04 through ME-01-07; ME-02-A through ME-02-E; Map Rendering & Visualization Architecture V0.3; Renderer Contract & Scene Model V0.1; Spatial Presentation & Geometry Interaction Contract V0.1; Map Generation Specification/Workflow/Session Lifecycle V0.2; Core Domain P2-09-08, P2-09-11, P2-09-15 and immutable Asset/Checkpoint contracts
- Target: C# + Avalonia desktop application, Windows/macOS, local-first, single-user

## 1. Purpose and executive recommendation

This document defines the shared calculation contract used by natural-geography analysis and future spatially derived products. ME-02-B through ME-02-E define what is authored and calculated; ME-02-F defines how calculations are identified, executed, invalidated, validated, retained and made available without compromising formal map state.

**Recommendation:** implement a bounded, local calculation subsystem within the Map Engine / spatial services boundary. It is not a new general-purpose workflow platform, distributed scheduler or independent authority for map facts.

The V1 model has four distinct concepts:

1. **Calculation Definition** — a versioned, application-owned description of a supported calculation family, its inputs, parameters, dependencies, output contract and validation rules. It is code/configuration, not a user-programmable arbitrary DAG.
2. **Calculation Run** — one execution attempt against a captured input context. It owns execution status, cancellation, diagnostics and resource accounting.
3. **Derived Artifact** — an immutable result set produced by a successful run, with complete provenance, coverage, resolution, units, no-data semantics and freshness identity.
4. **Active Derived Publication** — a small pointer/manifest selecting the artifact currently used by a derived view or downstream calculation. It is not a Core Domain formal fact and is switched atomically only after validation and freshness recheck.

Do not create a separate persistent entity for every conceptual term unless implementation evidence requires it. V1 may store definitions in code, runs in a compact local job store, artifacts as immutable files/assets, and active publication metadata in a small project-local index. Reuse the existing project-local SQLite and immutable Asset Store direction; do not introduce a second database or competing asset authority.

A completed run does not imply that its artifact is current, published, or formally adopted. Those are separate facts.

## 2. Goals and non-goals

### 2.1 Goals

- Make supported calculations reproducible from explicit inputs, algorithm versions, parameters and deterministic seeds.
- Keep physical source fields, derived analytical fields, semantic Objects and presentation separate.
- Support bounded dependency invalidation and local incremental recomputation.
- Preserve previous valid results while replacements are running or fail.
- Prevent obsolete runs from publishing over newer inputs or newer results.
- Support cancellation, failure, restart recovery and stale-result reconciliation.
- Define tile boundaries, neighboring context, resolution and no-data behavior explicitly.
- Bind outputs to Map, Branch, historical-time and Shared Object definition context where applicable.
- Provide useful diagnostics and explainable provenance to the creator.
- Work offline and remain useful without AI/provider access.
- Keep the implementation small enough for a single-user desktop application.

### 2.2 Non-goals for V1

- Distributed workers, cloud compute, cluster scheduling or remote execution.
- A general-purpose workflow/DAG authoring platform.
- Arbitrary user scripts or unrestricted raster algebra.
- Full coupled physical simulation or unbounded iterative solvers.
- Bitwise-identical floating-point results across every CPU, OS and runtime unless a specific algorithm proves that property.
- A persistent authoritative cache or spatial index that cannot be rebuilt.
- Automatic conversion of derived results into Core Domain Objects or Commands.
- A second task authority competing with Map Generation / AI Assistant task sessions.
- GPU compute as a required V1 execution path.

## 3. Authority and responsibility boundaries

| Component | Owns | Must not own |
|---|---|---|
| Core Domain | Formal Object identity/state, formal geometry references, relationships, Map/Branch history, Initial State, immutable formal Asset references and formal Commands | Calculation scheduling, mutable derived cache state, renderer state |
| Map Engine / Spatial Services | Supported calculation algorithms, spatial/field processing, dependency evaluation, derived artifact production, geometry/spatial queries | Formal Object identity allocation, authorial history, approval authority |
| Calculation subsystem | Run lifecycle, input snapshots/fingerprints, bounded scheduling, cancellation, validation orchestration, artifact publication pointer | A second formal map-state model or general workflow engine |
| Editor | User controls, progress/diagnostics, layer/view selection, scope selection and result inspection | Running long calculations on the UI thread or mutating formal data directly |
| AI Assistant | Interpret intent, propose a calculation plan and parameters, explain assumptions/results, ask questions and coordinate a task | Numerical truth, direct artifact mutation, direct Core Domain writes |
| Map Generation / AI task layer | User task/session, Candidate, Change Set, decision/authorization and submission evidence when formal adoption is needed | Replacing calculation run status or owning derived artifact freshness |

The calculation subsystem is a capability inside the Map Engine / spatial services boundary. It may have internal services/classes, but V1 should not introduce an independently deployed Calculation Server or a new architectural top-level platform.

### 3.1 Two separate application paths

**Derived-only path:** calculate -> validate -> publish derived artifact -> refresh dependent views. No Candidate approval or Core Domain Command is required merely to display an analytical layer.

**Formal-adoption path:** calculate -> create reviewable Candidate/Change Set as appropriate -> validate -> user/system authorization under existing policy -> submit through Core Domain Domain Services -> record formal history and submission evidence. The derived artifact remains provenance/evidence; it is not itself the formal write.

A publication pointer switch is not a formal map-history operation. It changes which derived result a view consumes, not what the world formally is.

## 4. Calculation Definition and supported dependency model

A Calculation Definition is a registered, versioned calculation family. It declares:

- Stable definition ID and semantic version.
- Input roles and accepted source categories.
- Required and optional input coverage.
- Parameter schema, units, defaults and bounds.
- Output artifact types, value types, units and no-data semantics.
- Spatial footprint and required neighboring/halo context.
- Resolution behavior and supported scale range.
- Declared upstream dependencies and downstream invalidation categories.
- Determinism profile and seed policy.
- Validation rules and diagnostic severity mapping.
- Whether the result is derived-only or may feed a formal-adoption Candidate.

Example bounded families include elevation derivatives (slope/aspect/hillshade), terrain feature analysis, drainage routing/accumulation, climate circulation and temperature/precipitation estimation, climate classification, biome suitability and habitat suitability.

### 4.1 Dependency graph is declarative and bounded

The dependency graph is a finite, application-defined domain graph. It is not a user-authored arbitrary DAG and does not support unrestricted loops.

A representative natural-geography dependency chain is:

`World Frame / Planet Parameters -> Land-Water & Elevation -> Terrain Derivatives -> Drainage -> Water/Moisture Indicators -> Climate -> Climate Classification -> Biome / Habitat Suitability`

Actual dependencies are more selective. For example, a presentation-only color change must not invalidate climate; a river symbol change must not rerun drainage; a local elevation edit should invalidate only the spatially affected terrain, drainage and downstream products plus their required neighboring context.

Definitions must distinguish:
- **Required dependency:** calculation cannot run without a valid input.
- **Optional dependency:** calculation can run with a declared fallback and lower confidence.
- **Independent input:** a source fact or parameter that does not derive from another calculation.
- **Presentation dependency:** affects display only and must not invalidate physical analysis unless the definition explicitly says otherwise.

The engine rejects dependency cycles at definition registration/startup. No runtime user input can create an arbitrary cycle.

## 5. Input Snapshot and reproducibility identity

Every run operates on a captured, immutable logical input snapshot. It must not read a moving mixture of source revisions while executing.

The snapshot records, as applicable:

- Project and Map identity.
- Map frame / spatial reference / projection definition version.
- Branch identity and resolved history/checkpoint context.
- Canonical historical-time context when the calculation depends on time.
- Shared Object current-definition revision/fingerprint when a shared definition is an input.
- Source field/Asset content hashes and source authority category.
- Relevant formal geometry/component revisions or immutable Asset hashes.
- Upstream Derived Artifact IDs and content hashes.
- Calculation Definition ID and algorithm semantic version.
- Canonically serialized parameter values and units.
- Deterministic seed, if randomness is intentionally used.
- Requested coverage, Working Scope and resolution profile.
- Required neighboring context / halo policy version.
- Relevant global parameters such as axial tilt, world frame or planetary assumptions.

The snapshot must explicitly distinguish Map Extent, Working Scope, Canvas View and Export Area. A viewport pan/zoom is not a calculation input unless the operation is explicitly a viewport-dependent presentation task.

### 5.1 Fingerprints

Use a canonical, versioned fingerprint envelope. At minimum:

- `input_fingerprint`: hashes the complete logical input snapshot.
- `definition_fingerprint`: hashes definition identity, algorithm version and output contract.
- `parameter_fingerprint`: hashes normalized parameters and units.
- `artifact_content_hash`: hashes the produced immutable output payload(s).

A run identity is not merely a timestamp or a random job ID. The run record has a unique local ID for lifecycle tracking, while reproducibility and cache reuse are determined by the fingerprints.

Canonicalization rules must define stable field ordering, numeric encoding, units, null/no-data representation, string normalization and asset ordering. A change to canonicalization semantics requires a version change.

### 5.2 Determinism profile

V1 algorithms must be deterministic for the same declared inputs, algorithm version, parameters and seed under the supported runtime contract.

Do not claim universal cross-platform bitwise identity for floating-point operations without proof. Define one of two profiles per algorithm:

- **Exact deterministic:** byte-identical output is required and verified within the declared platform/runtime envelope.
- **Numerically deterministic:** output must remain within explicit numerical tolerance and produce identical categorical decisions where the algorithm contract requires them. Tolerances, comparison method and affected fields must be documented and tested.

For stochastic algorithms, the seed is mandatory and captured. Unseeded randomness, wall-clock time, network lookups, UI state and iteration-order accidents are prohibited inputs.

A change in algorithm semantics, physical assumptions, parameter interpretation, units, no-data handling or serialization that can alter results requires a new algorithm/contract version. A mere refactor that preserves verified semantics need not invalidate every artifact if compatibility is proven.

## 6. Spatial coverage, tiles, resolution and edge context

Calculations must declare their spatial coverage and resolution rather than silently treating a visible Canvas as the whole world.

### 6.1 Coverage

An artifact records:
- Map-space coverage geometry or canonical tile set.
- Coverage completeness: complete, partial, clipped-at-map-edge, or unresolved.
- No-data regions and reason codes.
- Resolution / sampling interval and its unit or scale definition.
- Spatial reference / map-frame identity.
- Whether the result is suitable for downstream use at the requested scale.

Partial coverage must never be presented as complete. A downstream calculation must either reject insufficient coverage, use an explicitly declared fallback, or propagate partial coverage and uncertainty.

### 6.2 Tile boundaries and halo

For algorithms whose neighborhood operations cross tile boundaries, each output tile must be calculated with a declared halo (neighboring source samples or geometry context) sufficient for that algorithm's support radius.

- Halo width is part of the Calculation Definition and algorithm version.
- The artifact stores the effective halo/context policy and the core output coverage separately.
- Halo samples are inputs to computation but are not duplicated into the tile's authoritative output coverage.
- Tile edges must not create artificial ridges, drainage divides, climate discontinuities or ecological seams.
- A missing neighbor/halo is an explicit boundary condition, not silently treated as a natural edge.
- Map-edge behavior (closed boundary, open edge, wrap/seam or unknown) comes from the Map Frame contract and is never inferred from tile layout.

For global or wrap-aware calculations, required neighboring tiles may cross the map seam according to the spatial frame. A local calculation that cannot obtain required context must report partial/blocked status rather than inventing continuity.

### 6.3 Resolution and multi-scale semantics

Resolution is part of artifact identity. A coarse artifact is not interchangeable with a fine artifact.

Each definition specifies supported resolution profiles and any deterministic aggregation/downsampling semantics. Downsampling must preserve the meaning of the field:
- continuous fields: declared aggregation/interpolation method and units;
- categorical fields: declared majority/priority/area-weighted policy, not numeric averaging of category IDs;
- directional/network fields: topology-aware rules, not generic averaging;
- extensive quantities: conservation-aware aggregation where relevant.

Display LOD is not calculation resolution. Renderer simplification and scene LOD must not change the source artifact or be reused as scientific input unless explicitly declared.

## 7. Invalidation and incremental recomputation

The engine computes invalidation from declared dependencies, source fingerprints and spatial influence. It must not rely on manually maintained ad-hoc UI refresh rules.

### 7.1 Change classes

| Change | Expected invalidation |
|---|---|
| Canvas pan/zoom/selection | No physical calculation invalidation |
| Presentation style, label or symbol change | Presentation caches only |
| Local geometry/elevation edit | Affected local calculation coverage plus algorithm-specific halo and downstream dependent coverage |
| River/Lake formal relationship change | Hydrology-derived products and declared dependent climate/ecology products where relevant |
| Local climate/ecology parameter change | Only products depending on that parameter and affected scope |
| Global axial tilt / planetary season parameter | All dependent seasonal climate and downstream classification/suitability coverage |
| Map frame / projection / wrap semantics change | Spatially dependent calculations whose definitions declare frame dependence |
| Branch/history context change | Results bound to the old context are not silently reused as current |
| Shared Object current-definition change | Only artifacts whose input snapshot references that shared revision |
| Algorithm version change | Artifacts produced by incompatible prior versions become stale for that definition |

### 7.2 Bounded influence

A change has:
1. a directly changed source footprint;
2. a definition-specific influence expansion (including halo, flow-network reach or regional/global context);
3. a downstream dependency closure;
4. an output coverage to recompute.

For terrain derivatives, influence is local to the algorithm neighborhood. For drainage, a local elevation change can propagate downstream through the connected drainage network and may affect basin interpretation beyond the edited tile. For climate, mountain/wind/moisture dependencies can propagate across a region or larger domain. The definition must declare this behavior; the engine must not assume every algorithm is strictly tile-local.

Global inputs may legitimately invalidate broad or world-scale coverage. Local changes must not cause indiscriminate full-world recomputation where the dependency contract permits a bounded update.

### 7.3 Staleness

An artifact is stale when one or more declared input fingerprints, algorithm compatibility, required coverage or context no longer match the current resolved inputs.

Stale does not mean corrupt or deleted. Retain old artifacts for comparison/recovery according to retention policy, but do not present them as current without an explicit historical/stale label.

Freshness is evaluated against the target context, not wall-clock age. A year-old artifact can still be current if its inputs are unchanged; a one-second-old artifact can already be stale.

## 8. Run lifecycle and artifact lifecycle

Run status and artifact status are separate state machines. Do not combine them into one overloaded status field.

### 8.1 Calculation Run status

Recommended states:

- `queued`: accepted by the local scheduler, not yet executing.
- `running`: computation is executing.
- `cancelling`: cancellation requested; worker is reaching a safe stop boundary.
- `succeeded`: computation and required output validation completed; immutable artifact is available but not necessarily active.
- `failed`: execution or required validation failed.
- `cancelled`: stopped without publishing a new active artifact.
- `interrupted`: process ended before a terminal result could be recorded; recovery must reconcile it.

A run may additionally carry a derived freshness result such as current/obsolete, but freshness is not an execution status. Avoid a Run status of `stale` that obscures whether it actually succeeded.

### 8.2 Derived Artifact state

Artifact state is best represented by immutable artifact metadata plus separate publication/freshness facts, not a mutable multi-purpose status field.

- **Produced:** output bytes and metadata have been durably written and validated.
- **Current:** its input/definition fingerprints match a target context.
- **Stale:** its declared inputs or compatibility no longer match that target context.
- **Published:** an active publication pointer selects it for a particular derived view/context.
- **Superseded:** a newer artifact is active for the same logical output/context.
- **Rejected/invalid:** validation failed; it cannot become active.

An artifact can be Produced and Current but not Published; Published and later Stale; or retained as Superseded for history/comparison. These are independent properties.

### 8.3 Lifecycle

1. Resolve target Map/Branch/time/shared-definition context.
2. Build and validate immutable input snapshot and fingerprints.
3. Check for a compatible current artifact; reuse only if all fingerprints and coverage/resolution requirements match.
4. Create Run record and enqueue bounded work.
5. Execute using immutable inputs; report progress and diagnostics.
6. Write outputs to a temporary/unpublished location.
7. Validate output structure, coverage, no-data, numerical/domain invariants and provenance.
8. Finalize immutable artifact payload(s) and content hashes.
9. Re-resolve current inputs and verify the original input/definition fingerprints are still valid for the intended target.
10. If still current, atomically switch the relevant active publication pointer; otherwise retain as obsolete/unpublished and schedule reconciliation if still needed.
11. Mark Run terminal and expose result/diagnostics to Editor/AI task layer.

No partially written artifact may become active. A failed or cancelled replacement leaves the prior active artifact untouched.

## 9. Atomic publication and obsolete-run protection

Asset file writes and SQLite transactions cannot be assumed to form one cross-resource transaction. Follow the existing Core Domain immutable Asset publication principle:

1. Write output to a temporary file/location.
2. Flush and validate the payload; compute its content hash.
3. Move/publish it to its immutable content-addressed location.
4. In a short SQLite transaction, revalidate the target publication generation and expected input fingerprint, then insert artifact metadata and switch the active pointer atomically.
5. If the transaction fails, the prior pointer remains active. The immutable file may be an orphan and is cleaned only by a conservative proven-unreferenced asset cleanup.
6. If the process crashes after file publication but before metadata commit, startup reconciliation can identify the unreferenced immutable file; it must not infer that it was published.
7. If metadata commits but the process crashes before UI notification, the committed pointer remains authoritative and the UI reloads it.

### 9.1 Compare-and-swap publication

The publication operation must include:
- target logical output key (definition + Map/context + coverage/resolution profile);
- expected prior publication generation or pointer revision;
- new artifact ID/content hash;
- exact input and definition fingerprints;
- current-context validation result.

If another run has already published a newer artifact, an older run cannot replace it merely because it finished later. Publication is rejected as obsolete unless it is explicitly requested for a different historical context.

### 9.2 Old-result availability

While a replacement runs:
- keep the last valid active artifact visible;
- label it as recalculating/outdated when its input context has changed;
- do not silently blend old and new tiles unless the artifact contract explicitly supports mixed-generation coverage and the UI makes that state clear;
- after successful publication, swap the selected artifact generation atomically.

If the new run fails, keep the old result visible with a clear stale/failed-refresh diagnostic. Never blank the map merely because recomputation failed.

## 10. Cancellation, restart and recovery

Cancellation is cooperative and bounded:
- Check cancellation at safe tile/chunk/phase boundaries.
- Do not expose partial output as a completed artifact.
- Delete temporary output when safe; immutable already-published but unreferenced files follow conservative orphan cleanup.
- Cancellation does not roll back formal Core Domain changes already committed by a separate adoption path.

On application startup:
- Runs left in `running` or `cancelling` become `interrupted` after process ownership is confirmed gone.
- Verify any completed artifact payloads against hashes and metadata.
- Reconcile active pointers against artifact existence, context fingerprints and definition compatibility.
- Preserve valid active artifacts.
- Mark missing/corrupt/incompatible artifacts unavailable or stale; do not fabricate replacements.
- Requeue only idempotent calculations whose input snapshot remains valid and whose policy permits automatic recovery.
- Otherwise expose a recoverable failure and allow the user/AI task layer to request a new run.

A crash during publication must resolve to either the previous pointer or the fully committed new pointer, never a half-switched state.

## 11. Validation and diagnostics contract

Validation is layered and reports structured diagnostics with stable codes, severity, affected coverage, source references and suggested action.

| Severity | Meaning | Publication behavior |
|---|---|---|
| `error` | Output is structurally invalid, violates a hard invariant, has incompatible inputs, or cannot be interpreted safely | Reject artifact; do not publish |
| `warning` | Output is usable but has material limitations, anomalies or uncertainty | Publish only if definition policy allows; clearly disclose |
| `info` | Non-blocking observation, assumption or provenance note | Does not block publication |

Validation categories:
- input completeness and authority;
- fingerprint/context consistency;
- geometry/topology and spatial coverage;
- numeric range, units and no-data;
- tile seam/halo consistency;
- algorithm-specific physical/domain invariants;
- uncertainty and approximation disclosure;
- output serialization and content-hash integrity;
- downstream compatibility.

Examples:
- Elevation has unresolved no-data gaps across a required drainage path: error or partial-coverage warning according to the definition's minimum-coverage rule.
- A closed depression is detected: not automatically an error; classify as confirmed/possible/unresolved under hydrology policy.
- A rain shadow lacks a plausible moisture source or wind direction: reject the claimed causal interpretation or report low-confidence/unsupported, rather than assert it as a physical result.
- Biome suitability conflicts with a creator-confirmed magical forest: report a suitability conflict, never overwrite the creator fact.
- A tile boundary produces a discontinuity beyond tolerance: error and block publication for affected coverage.

Diagnostics should explain causes in creator language. Technical detail remains available in an expandable diagnostic view; users should not need to understand raster algorithms to act.

## 12. Resource scheduling and threading

V1 uses a bounded in-process worker pool. Keep scheduling simple:
- UI thread never performs long-running calculation.
- Use a small configurable/conservative worker limit based on CPU and memory; do not saturate every core by default.
- Avoid running multiple memory-heavy world-scale jobs simultaneously.
- Support cancellation and priority for interactive/local tasks over background broad recomputation.
- Bound queued work and coalesce superseded requests for the same logical output/context.
- Use immutable input snapshots and worker-owned buffers; do not share mutable arrays between concurrent runs without explicit synchronization.
- Serialize publication transactions through the local database transaction boundary.
- CPU deterministic execution is the V1 baseline.
- GPU acceleration is deferred. If later added, it must satisfy the same output semantics, validation tolerances, provenance and fallback behavior; GPU availability must not be required to open or edit a project.

Progress should report meaningful phases/coverage, not false precision in percentage where total work is unknown.

## 13. Persistence and retention

Recommended V1 persistence mapping:

| Data | Recommended storage | Authority |
|---|---|---|
| Calculation Definition | Versioned application code/schema | Application contract |
| Run metadata, status, parameters, fingerprints, diagnostics summary | Existing project-local SQLite | Calculation execution record |
| Immutable derived payloads | Project-local Asset Store, content-addressed files | Immutable derived artifact bytes |
| Artifact metadata and source references | SQLite metadata referencing immutable payloads | Derived artifact catalog |
| Active publication pointer/generation | SQLite transactionally updated metadata | Current derived-view selection |
| Temporary work files | Project-local temp/cache directory | Disposable |
| Derived caches and renderer scenes | Rebuildable local cache | Never formal authority |

Do not place large mutable raster arrays in SQLite JSON columns. Do not create one database row per pixel/cell. Exact binary encoding, tiling, compression, checksums and Asset metadata extension are implementation gates under ME-05/ME-07 and Core Domain Asset review.

Retention:
- Keep the active artifact and any artifacts referenced by current views/tasks/checkpoints or explicit user pins.
- Old artifacts may be retained for comparison and rollback of derived-view selection, subject to storage budget.
- Cache eviction must never delete formal source Assets or required historical/checkpoint Assets.
- Garbage collection deletes only artifacts proven unreferenced by active pointers, retained task evidence and required project metadata.
- Rebuilding a derived artifact must not mutate the source snapshot or formal history.

## 14. Branch, historical time and Shared Object context

Every artifact must declare which world context it represents.

- Map/Branch identity and resolved history/checkpoint context are part of the input identity whenever the calculation reads historical state.
- Historical-time-dependent calculations include the canonical time value and relevant calendar/temporal interpretation version.
- A Branch change does not allow an artifact from another Branch to be silently treated as current, even if some visible source data appears identical. Safe reuse is allowed only when the complete resolved input fingerprints prove equivalence under the definition contract.
- Shared Object current-definition data is explicitly distinguished from Map/Branch historical state. If a calculation consumes a Shared Object's current definition, capture its revision/fingerprint and invalidate dependent artifacts when that definition changes.
- A map-specific historical event or state change must not mutate the shared definition artifact or another map's result.
- Artifacts may be reused across contexts only when their full input, definition, frame, coverage and resolution fingerprints match. Reuse is a cache optimization, not a semantic assumption.

## 15. Integration with ME-02-D and ME-02-E

### 15.1 Hydrology

Hydrology definitions must declare:
- elevation/land-water inputs and required coverage;
- depression policy/version and map-edge behavior;
- flow-routing algorithm/version and tie-breaking rules;
- single-flow-direction assumptions and limitations;
- drainage-network influence behavior, including downstream propagation;
- flow accumulation/channel threshold parameters and units;
- watershed/channel outputs as derived artifacts, not formal Objects;
- River/Lake formal relationships only through registered Core Domain descriptors.

A local elevation change may require recomputing downstream drainage beyond the edited tile. The engine must not use a naive fixed-radius invalidation for a network-propagating calculation.

### 15.2 Climate and ecology

Climate definitions must declare:
- planetary frame, axial tilt/season assumptions and hemisphere phase mapping;
- circulation, wind/moisture-source and ocean influence model versions;
- elevation, land/water, hydrology and terrain barrier inputs where relevant;
- seasonal phase and temporal profile;
- resolution and uncertainty policy;
- classification scheme/version and thresholds;
- distinction between biome/habitat suitability and creator-confirmed ecosystem/species presence.

A global planetary parameter change can invalidate broad climate and downstream ecology products. A local terrain change should invalidate only the affected climate/ecology influence domain where the model supports bounded propagation.

## 16. Interface-level contract (illustrative)

The following is a conceptual boundary, not a frozen public API or serialization schema:

```csharp
public interface ICalculationDefinition
{
    CalculationDefinitionId Id { get; }
    SemanticVersion AlgorithmVersion { get; }
    InputContract Inputs { get; }
    OutputContract Outputs { get; }
    DependencyContract Dependencies { get; }
    DeterminismContract Determinism { get; }

    Task<CalculationOutput> ExecuteAsync(
        CalculationInputSnapshot input,
        CalculationParameters parameters,
        ICalculationExecutionContext context,
        CancellationToken cancellationToken);
}

public interface ICalculationCoordinator
{
    Task<CalculationRunHandle> StartAsync(
        CalculationRequest request,
        CancellationToken cancellationToken);

    Task<CalculationRunSnapshot> GetRunAsync(
        CalculationRunId runId,
        CancellationToken cancellationToken);

    Task CancelAsync(
        CalculationRunId runId,
        CancellationToken cancellationToken);
}

public interface IDerivedArtifactPublisher
{
    Task<PublicationResult> TryPublishAsync(
        ValidatedDerivedArtifact artifact,
        PublicationPrecondition precondition,
        CancellationToken cancellationToken);
}
```

The actual implementation may use application services, records and internal interfaces rather than preserve these exact names. The non-negotiable semantics are immutable inputs, explicit cancellation, validation before publication, compare-and-swap freshness protection and no direct Core Domain mutation.

## 17. Acceptance criteria and verification scenarios

The contract is ready for implementation planning when the following behaviors are testable:

1. Same input snapshot, definition version, parameters and seed produce results satisfying the declared determinism profile.
2. Changing a relevant input changes the input fingerprint; changing an irrelevant presentation property does not.
3. A run never mixes source revisions captured at different times.
4. A local tile calculation reads the declared halo and produces seam-consistent output.
5. Missing required halo or coverage produces an explicit blocked/partial result, not a fabricated edge.
6. Coarse and fine resolution artifacts are distinct and cannot be substituted silently.
7. A local elevation edit invalidates its terrain derivatives and declared downstream drainage/climate/ecology influence, not unrelated world regions.
8. A global axial-tilt change invalidates all dependent seasonal products.
9. A Branch/time/shared-definition context change cannot silently reuse incompatible artifacts.
10. Two overlapping runs completing out of order cannot allow the older input snapshot to overwrite a newer publication.
11. While a replacement is running or fails, the last valid artifact remains available.
12. Cancellation leaves no active partial artifact.
13. A crash before publication leaves the old pointer; a crash after transaction commit leaves the complete new pointer.
14. Startup recovery identifies interrupted runs and reconciles artifacts/pointers without inventing success.
15. Corrupt or hash-mismatched payloads are never published.
16. Derived-only publication creates no Core Domain Command or formal Object.
17. Formal adoption goes through existing Candidate/authorization/submission and Core Domain Domain Services.
18. UI remains responsive during calculation and progress is meaningful.
19. Bounded worker scheduling prevents unbounded queue/memory growth.
20. AI/provider absence does not prevent manual calculation, inspection or recovery.

Required scenario fixtures:
- tile-edge mountain ridge with slope/aspect calculation;
- river network crossing multiple tiles and a local upstream elevation edit;
- a legitimate endorheic basin that must not be auto-filled;
- climate rain-shadow calculation with and without a valid moisture source/wind direction;
- global axial-tilt change across both hemispheres;
- Branch-specific historical terrain change while Shared Object definition remains unchanged;
- Shared Object current-definition change affecting dependent map views;
- failed replacement after a previously published artifact;
- two concurrent runs with reversed completion order;
- process termination at each asset-publication/SQLite-commit boundary;
- partial coverage and no-data propagation into downstream products.

## 18. Open implementation gates

| Gate | Owner | Required evidence |
|---|---|---|
| ME02-F-G01: exact Calculation Definition and Run metadata schema | Map Engine + application architecture | Minimal schema, lifecycle tests, no competing task authority |
| ME02-F-G02: derived Asset envelope, tiling, compression and content addressing | Map Engine + Core Domain Asset owner | Representative field payloads, hash verification, crash-safe publication |
| ME02-F-G03: canonical input/parameter serialization | Core Domain + Map Engine | Cross-process/runtime fixture tests and versioning rules |
| ME02-F-G04: supported resolution profiles and aggregation rules | Natural Geography + Spatial Services | Field-specific semantics and seam tests |
| ME02-F-G05: dependency/influence declarations for each algorithm | ME-02 specialists | Hydrology network propagation and climate regional/global fixtures |
| ME02-F-G06: numerical determinism/tolerance profiles | Algorithm owners | Windows/macOS CPU comparison fixtures |
| ME02-F-G07: worker count, memory budgets and cancellation boundaries | Desktop/Map Engine implementation | Representative workload profiling |
| ME02-F-G08: SQLite metadata and immutable file publication recovery | Core Domain + Map Engine | Fault-injection tests at every commit boundary |
| ME02-F-G09: retention and orphan cleanup | Project persistence owner | Reference scan and safe-GC tests |
| ME02-F-G10: UI progress, stale labeling and diagnostics presentation | Editor UX (ME-02-G) | End-to-end creator scenarios |

These are professional implementation/validation gates, not questions that require the creator to choose GIS or numerical implementation details.

## 19. Source alignment and status

This contract is aligned with:
- ME-01-04 multi-scale spatial consistency: bounded dependency and influence, Map/Branch/time context and incremental recomputation.
- ME-01-05 Object/Geometry/Representation: derived outputs remain separate from semantic identity and formal geometry.
- ME-01-06 Editor interaction: transient editor state, Candidate staging and formal Core Domain state remain distinct.
- ME-01-07 cross-architecture findings: typed operations, immutable scene generations, explicit scopes, stale protection and AI submission boundaries.
- ME-02 V0.3 and ME-02-D/E: terrain-first hydrology, causal climate/ecology, uncertainty and creator-fact protection.
- Map Rendering & Visualization Architecture V0.3 and Renderer Contract V0.1: immutable presentation snapshots, cache invalidation, cancellation and renderer-independent data.
- Spatial Presentation & Geometry Interaction Contract V0.1: typed spatial references, snapshot consistency and no authoritative persistent spatial index.
- Map Generation workflow/session lifecycle: task/Candidate/authorization/application separation and idempotent formal application.
- Core Domain P2-09-08 / P2-09-11 / P2-09-15: deterministic replay inputs, immutable Assets, short transactions and typed formal Commands.

This document establishes the shared calculation architecture working baseline. It does not freeze a raster format, scientific algorithm, numerical parameter, public API, executable schema or rendering/compute library. Those remain owned by their specialist and implementation validation gates.

**Next work:** ME-02-G end-to-end UX scenarios and acceptance, followed by ME-02-H cross-architecture review.
