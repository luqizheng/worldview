# World View — ME-02-F Calculation Contracts

- Version: 0.2
- Date: 2026-11-21
- Status: **Calculation Contract Working Baseline** — deterministic execution, provenance, freshness, invalidation and validation-severity contract; algorithms, numeric calibration, physical-field storage and performance thresholds remain separate gates
- Parent: [ME-02 Terrain & Natural Geography Editor V0.3](world-view-me-02-terrain-and-natural-geography-editor-v0.3.md)
- Depends on: ME-02-02 through ME-02-06; ME-01-04/05/07; Core Domain Asset / Checkpoint / History contracts; Map Engine renderer-neutral spatial and field contracts
- Scope: Shared calculation lifecycle for terrain, hydrology, climate and ecology derived analysis

## 1. Purpose and design decision

ME-02 calculation services produce explainable, reproducible derived analysis from declared source inputs. They do not own formal world facts, do not mutate Core Domain history, and do not silently replace creator-authored inputs.

V1 adopts a **deterministic, version-pinned, dependency-aware calculation model**:

1. Every calculation is an explicit request with a declared scope, input snapshot, algorithm identity/version, parameters and protected facts.
2. A calculation reads immutable source Assets and a consistent Core Domain view; it never reads mutable UI state or live external services during execution.
3. Outputs are immutable derived artifacts with provenance and freshness metadata.
4. Recalculation creates a new output revision; it never mutates the prior artifact in place.
5. Derived outputs are not Commands and do not become formal Object/Relationship facts unless a separate adoption operation is explicitly accepted through Core Domain.
6. A calculation result is usable only when its dependency fingerprint matches the current declared inputs and compatible descriptor/semantics versions.

This is a bounded analysis pipeline, not a general-purpose user-programmable raster algebra engine, distributed workflow engine or scientific simulation platform.

## 2. Authority boundaries

| Concern | Authority | Calculation service behavior |
|---|---|---|
| Object identity, definitions, Map Initial State and History | Core Domain | Read through a consistent snapshot; never mutate directly |
| Source physical fields and geometry | Declared source authority / immutable Asset Store | Read by stable Asset identity and content fingerprint |
| Creator facts, constraints and protected content | Core Domain and approved World Knowledge contracts | Treat as explicit inputs/constraints; never overwrite |
| Algorithm implementation and compatibility | Versioned calculation service registry | Resolve exact algorithm + semantics version before execution |
| Derived outputs | Derived Analysis subsystem | Own immutable output artifacts, provenance and freshness |
| Candidate review and adoption | Editor / AI Assistant staging plus Core Domain Domain Services | Produce reviewable candidates; adoption is a separate authorized operation |
| Styling, labels, overlays and animation | Presentation | Consume derived outputs without changing their authority |

A calculation may use a formal Object as an input or target, but it must not create, deactivate, redefine or establish formal relationships as a side effect. If the creator accepts a candidate River, TerrainRegion, geometry refinement or other formal fact, that adoption is a distinct Domain operation with its own validation and transaction.

## 3. Calculation request contract

Every calculation request must capture the following logical envelope. This is a domain contract, not a prescribed database table or wire-format schema.

| Field | Requirement | Meaning |
|---|---|---|
| `calculation_id` | Required, stable, unique | Identity of one calculation attempt |
| `calculation_kind` | Required registered key | e.g. elevation analysis, terrain classification, drainage, climate, biome suitability |
| `algorithm_id` | Required registered key | Named deterministic algorithm family |
| `algorithm_version` | Required | Exact implementation-compatible version |
| `semantics_id` | Required | Exact interpretation contract for the algorithm |
| `parameters` | Required typed canonical values | All behavior-affecting options, thresholds and policies |
| `target_map_id` | Required | Owning Map context |
| `history_context` | Required when inputs include historical state | Selected Map/Branch History and effective-time context |
| `working_scope` | Required | Requested spatial scope, with required calculation context/buffer |
| `input_manifest` | Required non-empty | Stable source references, content fingerprints, roles and coverage |
| `descriptor_manifest` | Required | Object/Relationship descriptor versions and semantics where relevant |
| `protected_facts` | Required, may be empty | Creator-confirmed facts and constraints the calculation may not silently replace |
| `requested_outputs` | Required | Named output products and expected logical types |
| `requested_by` | Required | User/system/AI task provenance |
| `determinism_profile` | Required | Numeric, ordering, tie-break and random-seed policy |
| `created_at` | Audit only | Must not affect calculation output |

The request is rejected before execution if a required input is missing, a reference cannot be resolved, a required algorithm version is unavailable, or the selected scope cannot provide the declared context.

## 4. Determinism and reproducibility

### 4.1 Deterministic execution

For the same canonical request, compatible source content and exact algorithm implementation, the service must produce the same logical output and output fingerprint.

The following are prohibited as implicit calculation inputs:

- wall-clock time, local time zone or machine locale;
- UI selection, viewport, zoom, cursor position or transient editor state;
- network responses, remote geocoding or unpinned external datasets;
- unordered database iteration or filesystem enumeration;
- unseeded randomness;
- platform-dependent iteration order;
- ambient environment variables or machine-specific paths;
- mutable source files read without a captured content fingerprint.

If stochastic generation is intentionally introduced later, its seed and generator version become explicit request inputs. It must still be reproducible from the captured request.

### 4.2 Canonicalization

Before hashing or execution, normalize:
- stable identifier ordering;
- parameter key ordering and canonical numeric representation;
- coordinate reference, units and vertical reference identifiers;
- no-data representation and coverage boundaries;
- geometry and field Asset fingerprints;
- temporal context using Core Domain canonical Historical Time, never display-calendar reinterpretation;
- algorithm, descriptor and semantics versions.

Do not round or quantize source values merely to obtain stable hashes. Any precision reduction must be an explicit, versioned algorithm parameter and must be reflected in provenance.

### 4.3 Numeric determinism

V1 algorithms must declare:
- numeric representation and precision;
- comparison tolerances;
- tie-breaking rules;
- treatment of equal-height / flat terrain;
- boundary and no-data behavior;
- overflow / invalid-value behavior;
- stable ordering for equivalent candidates.

Cross-platform floating-point behavior must be tested on Windows and macOS for representative workloads. Where exact bitwise equality is not realistic, define a stable canonical output quantization and tolerance contract explicitly; do not claim bitwise determinism without evidence.

## 5. Input manifest and provenance

Each input manifest entry records:

- logical input role (elevation, land/water mask, lake shoreline, River geometry, climate parameter profile, soil context, creator constraint, etc.);
- stable source identity (Object/Asset/field/configuration ID);
- immutable content fingerprint;
- source authority and provenance class (authored, imported, derived, approved knowledge);
- schema/descriptor/semantics version where applicable;
- spatial reference, units, vertical reference and resolution where applicable;
- coverage and no-data semantics;
- effective Map/Branch/History context where the input is history-dependent.

A filename, filesystem path, display name or mutable URL is not a sufficient source identity. The content hash is not a substitute for the stable source identity; both are needed.

For derived-on-derived calculations, the upstream derived artifact ID and fingerprint are inputs. The service must preserve the dependency chain rather than flattening it into an opaque final result.

## 6. Output artifact contract

Every completed calculation publishes zero or more immutable output artifacts. Each output records:

- output artifact ID and logical output role/type;
- immutable content Asset reference and content fingerprint;
- calculation ID and request fingerprint;
- algorithm ID/version and semantics ID;
- exact input manifest and upstream artifact references;
- canonical parameter snapshot;
- spatial extent, resolution, coordinate reference and coverage;
- no-data mask/meaning;
- output creation provenance;
- quality/uncertainty summary;
- validation diagnostics;
- freshness state at publication.

Output artifacts are immutable. A new calculation creates new artifacts and a new calculation result record; old outputs remain available for audit, comparison and historical explanation while referenced.

Derived outputs are cacheable and rebuildable, but they are not disposable if needed to explain a creator decision or an accepted adoption. Retention policy must respect provenance and references.

## 7. Freshness and dependency invalidation

### 7.1 Freshness states

| State | Meaning | Permitted use |
|---|---|---|
| `current` | All declared input and compatibility fingerprints match the current calculation context | May be shown as current analysis |
| `stale` | One or more relevant source inputs or semantics changed | Historical inspection only; do not present as current |
| `incomplete` | Required coverage/input is missing or unresolved | May be shown with explicit limitation; cannot support claims requiring missing data |
| `failed` | Calculation did not produce a valid complete output | Diagnostics only |
| `superseded` | A newer valid calculation for the same logical request/output scope exists | Retain for comparison/audit; default UI selects newer current result |

Freshness is evaluated against the declared dependency set, not against every change in the Project. Unrelated edits must not invalidate a result.

### 7.2 Dependency graph

Each calculation kind declares its input and output dependencies. The graph is a directed acyclic dependency graph of derived artifacts; cycles are rejected at registration.

Recommended ME-02 dependency families:

| Output family | Principal upstream inputs |
|---|---|
| Elevation-derived terrain metrics | Elevation, Map Space, coverage, algorithm parameters |
| Terrain feature candidates | Elevation-derived metrics, land/water, scope context, creator constraints |
| Hydrology | Elevation, land/water, Lake facts, map-edge semantics, climate/water-balance inputs when enabled, existing River facts |
| Climate | Planet/season parameters, latitude/world frame, land/water, elevation, circulation/moisture assumptions, ocean influence |
| Soil/water context | Source soil/surface fields, water bodies, precipitation/temperature outputs where supported |
| Biome/ecology suitability | Climate outputs, soil/water context, elevation, species/habitat knowledge constraints |
| Presentation overlays | Selected current derived outputs plus presentation configuration |

A dependency edge means the downstream result depends on the upstream content fingerprint, not merely that one calculation usually runs first.

### 7.3 Invalidation rules

When a source input changes:
1. identify the exact source identity and new content fingerprint;
2. traverse only registered downstream dependency edges;
3. mark affected outputs stale;
4. preserve unaffected outputs;
5. schedule or offer recalculation according to user intent and cost policy;
6. never silently recalculate and adopt formal Objects/Relationships.

When a derived output is replaced, only downstream outputs that reference the old artifact/fingerprint become stale.

Changing an algorithm implementation, algorithm version, semantics ID, parameter schema, or relevant Object/Relationship descriptor version invalidates outputs whose compatibility manifest pins the changed item. A compatible additive metadata change need not invalidate outputs if the registry explicitly declares it non-semantic.

### 7.4 Spatially bounded invalidation

For regional calculations, invalidation must account for the algorithm's declared influence radius and context dependencies. A local edit may require a larger recomputation area than the edited geometry itself.

Each algorithm declares:
- minimum context buffer or neighborhood radius;
- whether effects can propagate beyond the local scope;
- whether neighboring tiles/scopes must be read;
- whether global or basin-wide context is required;
- boundary reconciliation behavior.

Hydrology basin routing and climate moisture transport may have non-local dependencies. They must not be invalidated or recalculated using only the visible Canvas viewport. If the algorithm cannot safely bound the affected region, invalidate the full declared analysis domain rather than guessing a smaller region.

## 8. Scope, tiling and boundary consistency

Working Scope is a user/editor concept; calculation scope is the actual spatial domain needed for correct results. The two are related but not identical.

- The request declares the requested scope and the algorithm-expanded calculation scope.
- The service includes upstream/downstream or neighboring context required by the algorithm.
- Tile boundaries are storage/processing partitions, not semantic boundaries.
- A tile edge, viewport edge or no-data edge is never automatically a coastline, watershed divide, climate boundary or terminal outlet.
- Adjacent output tiles must use compatible source fingerprints, algorithm versions, parameters and boundary rules.
- Stitching/reassembly must be deterministic and preserve shared-edge consistency.
- If neighboring context is unavailable, the output is marked incomplete or boundary-uncertain; it must not fabricate a seamless result.

Global algorithms may produce regional views from a globally consistent result. Regional algorithms may not silently claim global correctness.

## 9. Checkpoint and cache coherence

A calculation output or derived checkpoint is reusable only when its complete compatibility fingerprint matches:

- Map and selected History context (where relevant);
- canonical effective-time context (where relevant);
- source Object/Asset/field fingerprints;
- upstream derived artifact fingerprints;
- algorithm ID/version and semantics ID;
- canonical parameters;
- Object Type and Relationship Type descriptor versions/semantics used in validation;
- spatial reference, units, precision and no-data rules;
- calculation scope and context/boundary manifest.

A matching time or filename alone is insufficient. A stale checkpoint may be retained for audit or historical comparison but must not be served as current.

Shared Object current-definition edits follow the accepted retroactive-current-definition model. Any calculation whose output depends on that definition must be invalidated by the changed definition fingerprint, even when the viewed historical time is earlier. History-local post-zero changes invalidate only calculations whose selected Map/Branch History path includes those changes.

## 10. Validation severity and result disposition

Validation findings use four severities:

| Severity | Meaning | Result disposition |
|---|---|---|
| `error` | Structural, authority, reference, determinism or hard domain invariant violation | Reject request/output; no publication as valid result |
| `blocking_warning` | Calculation can run, but output cannot support the requested claim or formal adoption | Publish only as incomplete/diagnostic; block affected adoption |
| `warning` | Material uncertainty, approximation or quality limitation | Publish with visible warning and provenance |
| `info` | Non-blocking explanation or informational diagnostic | Publish normally |

Examples:
- Missing required elevation or unresolved vertical reference for drainage: error or blocking warning according to whether any valid output can be produced.
- No-data incorrectly treated as sea level: error.
- Coarse terrain unable to resolve a narrow ridge: warning or blocking warning for a rain-shadow claim.
- Candidate River crossing a divide: warning requiring review, not automatic relationship creation.
- Climate class without adequate seasonal coverage: blocking warning for claims requiring seasonality.
- Creator-confirmed magical biome differing from natural suitability: informational divergence, not an error.
- Unknown optional soil data: warning or incomplete output, not a fabricated default.

Severity is attached to a finding and its target scope. A warning in one region must not downgrade unrelated valid regions unless the calculation contract defines a global dependency.

## 11. Failure, cancellation and publication

A calculation has a bounded lifecycle:

`requested → validating → running → validating_outputs → completed | completed_with_warnings | incomplete | failed | cancelled`

Rules:
- Cancellation is cooperative and must not publish partially written output as a completed artifact.
- Temporary files/assets are staged separately from published immutable artifacts.
- Publication is atomic at the calculation-result boundary: either all declared required outputs become visible together, or none do.
- Optional outputs may be absent only when the request contract explicitly marks them optional and the result records why.
- Failure diagnostics include the stage, stable error code, affected scope, source references and retryability classification.
- Retry must reuse the same captured request if the intent is unchanged; a changed input requires a new request/fingerprint.
- Calculation failure never rolls back or mutates Core Domain facts because calculation is read-only with respect to formal world state.

## 12. Adoption boundary

Derived analysis may be:
- inspected;
- compared with prior calculations;
- used as an input to another calculation;
- transformed into editor Candidates;
- selectively adopted through a separate Core Domain operation.

It may not silently:
- create a formal Object or Relationship;
- modify Shared Object definition;
- alter Map Initial State;
- append a History Command;
- change Calendar or canonical Historical Time;
- overwrite source physical fields;
- mark a Candidate as accepted.

An adoption operation must revalidate the current target History, descriptor versions, endpoint visibility/activity, source fingerprints, user authorization and any required human confirmation at the time of application. A previously valid calculation is not permanent authorization to apply its output.

## 13. ME-02 algorithm-family requirements

| Family | Determinism-critical declarations | Required provenance / quality evidence |
|---|---|---|
| Elevation / terrain analysis | Neighborhood definition, edge handling, no-data, tie-breaks, numeric precision | Source elevation fingerprint, resolution, vertical reference, coverage |
| Terrain feature detection | Scale-aware thresholds, component connectivity, minimum feature size, simplification | Detection parameters, source terrain metrics, confidence and creator overrides |
| Hydrology / drainage | Depression policy, priority-flood ordering, flow direction, flat resolution, accumulation thresholds, outlet classification | Elevation + land/water fingerprints, conditioning deltas, flow method, basin coverage, uncertainty |
| Climate | Season phase model, latitude computation, circulation assumptions, moisture transport order, lapse profile, boundary behavior | Planet profile, season model, elevation/land-water inputs, wind/moisture assumptions, confidence |
| Ecology suitability | Classification scheme/version, suitability rule ordering, missing-data behavior, species knowledge version | Climate/soil/water fingerprints, scheme version, habitat constraints and divergence from confirmed ecology |

Numeric thresholds and calibrated scientific parameters remain in their specialist algorithm specifications, not in this shared lifecycle contract.

## 14. Acceptance checklist

A calculation family is not implementation-ready until all applicable items are satisfied:

- [ ] Request envelope and required input roles are typed and validated.
- [ ] Algorithm ID, version and semantics ID are stable and registered.
- [ ] Canonical parameter serialization and fingerprint rules are specified.
- [ ] No-data, coverage, coordinate reference, units and boundary behavior are explicit.
- [ ] Deterministic ordering, tie-break and numeric precision rules are specified.
- [ ] Input provenance and immutable output artifact lineage are retained.
- [ ] Dependency edges and invalidation scope are declared.
- [ ] Checkpoint compatibility includes relevant descriptor/semantics versions.
- [ ] Error / blocking-warning / warning / info disposition is specified.
- [ ] Cancellation and atomic output publication are specified.
- [ ] Representative deterministic fixtures exist, including boundary and incomplete-coverage cases.
- [ ] Windows and macOS repeatability tests are executed for the production implementation.
- [ ] Performance is measured against representative map sizes and declared memory bounds.
- [ ] Candidate generation is separated from formal Core Domain adoption.

## 15. Status and next gates

ME-02-F establishes the shared calculation lifecycle and cross-cutting contracts. It does **not** select a raster library, geometry engine, database schema, file format, threading model or final scientific algorithm implementation.

Next:
- ME-02-G: end-to-end UX scenarios and acceptance criteria for natural-geography authoring and calculation review.
- ME-02-H: cross-architecture review against Core Domain, Map Engine, Map Generation and ME-01.
- Specialist implementation gates: exact algorithms, numeric calibration, physical-field Asset encoding, tile storage, cache layout and cross-platform performance.

No executable calculation service or automated test pass is implied by this design document.
