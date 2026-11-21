# World View — ME-06-C Spatial Query Freshness

- Version: 0.1
- Date: 2026-11-21
- Status: Spatial-query consistency working baseline; runtime implementation and platform proof remain open
- Depends on: ME-06-00 V0.1; ME-06-A V0.1; ME-06-B V0.1; ME-01-04/05/06/07; ME-05-02/03/04/06; specialist ME-02/03/04 contracts; Map Engine immutable scene/query contracts
- Authority: Shared query-consistency contract. It does not freeze a spatial-index implementation, geometry library, database schema, renderer API or Core Domain descriptor.

## 1. Executive disposition

ME-06-C defines how asynchronous spatial queries remain consistent with the exact map context and source geometry from which they were requested.

The recommended V1 design is deliberately small:

1. Every query captures an immutable **Query Context Token**.
2. Every interactive request within a gesture has a monotonically increasing **Request Sequence**.
3. Results are accepted only if both the query context and request sequence are still current.
4. A query result is evidence for preview/validation, never a lock, authorization or formal world fact.
5. Before formal application, the Application Service re-resolves stable domain references and revalidates all required source revisions and invariants against the current authoritative state.

This is a consistency protocol, not a distributed concurrency framework. V1 is local-first and single-user; no server lease, object lock, CRDT or multi-user conflict protocol is introduced.

## 2. Query classes and authority

| Query class | Examples | Owner | Authority and allowed effect |
|---|---|---|---|
| Scene interaction | Hit-test, selection candidate, hover | Map Engine | Presentation query; returns stable Object/Component references plus scene generation |
| Geometry assistance | Snap candidate, nearest point, projection, intersection witness | Map Engine / Spatial Services | Read-only evidence; may guide a transient draft only |
| Spatial predicate | Contains, intersects, touches, disjoint, distance/proximity | Map Engine / Spatial Services | Typed geometric result; no semantic relationship creation |
| Specialist validation | River flow topology, settlement placement, building-space constraints | Specialist validation using spatial evidence | Can block or qualify a Typed Intent; cannot mutate formal state |
| Measurement | Distance, length, area, bearing | Map Engine / Measurement service | Derived measurement with units, CRS, precision and error context |
| Field sampling | Elevation, land/water and other source-field samples | Map Engine / Field service | Read-only sample pinned to Map Source Field Revision |
| Deterministic analysis | Drainage, climate, ecology, placement rules | Calculation subsystem | Run/Derived Artifact with pinned inputs and algorithm fingerprint; not an interactive query shortcut |

A geometric predicate does not create a formal relationship. A snap candidate does not move geometry until the user explicitly accepts it into a transient draft. A specialist validation result does not itself authorize the corresponding domain operation.

## 3. Immutable Query Context Token

Every asynchronous spatial query MUST capture the following fields. Fields not applicable to a query class are explicitly absent with a declared reason; they are not silently defaulted.

| Field | Required meaning | Freshness purpose |
|---|---|---|
| `query_id` | Unique query identity | Diagnostics and cancellation |
| `gesture_session_id` | Gesture/edit session that requested the query; null for non-gesture reads | Prevents cross-gesture result delivery |
| `tool_key` + `tool_version` | Declared built-in tool identity/version | Prevents interpretation under changed tool semantics |
| `project_id`, `map_id` | Exact owner context | Prevents cross-Project/Map reuse |
| `branch_id`, `historical_time` | Exact read context where the query depends on historical Map state | Prevents time/Branch leakage |
| `working_scope_id` + `scope_revision` | Explicit creator working scope | Prevents reuse after scope definition changes |
| `scene_generation` | Immutable Map Engine scene snapshot generation | Prevents stale hit-test/snap against replaced scene |
| `request_sequence` | Monotonic sequence within query stream/gesture | Prevents older pointer sample overwriting newer sample |
| `target_refs[]` | Stable Object ID + Geometry Component ID + expected component/object revision where available | Prevents renderer-node identity or retargeting |
| `source_fingerprints[]` | Immutable fingerprints/revisions for every formal geometry/field input | Prevents source mutation from silently validating old evidence |
| `spatial_reference_id` | Immutable CRS/spatial-reference identity per operand/context | Prevents mixing incompatible coordinate systems |
| `transform_revision` | Version of coordinate transform pipeline used for conversion | Prevents stale transformed coordinates |
| `precision_profile_id` | Declared precision/numerical robustness profile | Ensures consistent tolerance semantics |
| `coordinate_space` | Explicit input/output space (Domain, Map, Viewport, Device or Building-local as applicable) | Prevents coordinate-space confusion |
| `algorithm_id` + `algorithm_version` | Stable query/validation algorithm identity and version | Reproducibility and semantic compatibility |
| `parameters_fingerprint` | Canonical fingerprint of thresholds, boundary rules, filters and other query parameters | Prevents same query ID with changed semantics |
| `coverage_fingerprint` | Relevant source coverage/tile/halo/no-data coverage identity, where applicable | Prevents use outside valid input coverage |
| `requested_at_generation` | Generation of relevant immutable source snapshot | Diagnostics and comparison |
| `cancellation_token` | Cancellation identity for obsolete work | Resource cleanup; not correctness proof |

The token is an immutable value captured at dispatch. It is not updated in place when the user pans, changes scope, switches Map, edits a source, or changes a query parameter. A new semantic context requires a new token.

### 3.1 Stable references, not renderer IDs

Renderer nodes, spatial-index entries, tile-local row numbers and GPU handles are ephemeral. Query results must resolve back to stable Object/Component identities before they are exposed to tool logic. If a source component cannot be resolved, the result is unavailable/stale rather than a new identity inferred from geometry bytes.

### 3.2 Fingerprint contents

A source fingerprint should bind the smallest authoritative source unit sufficient for the query:
- formal Object/Component identity and expected revision;
- immutable geometry Asset content hash and declared geometry role;
- Field Dataset ID, Field Revision ID and immutable package/tile fingerprints for field-dependent work;
- Map/Branch/Time state generation where the query depends on historical Object state;
- spatial-reference identity and relevant transform/precision profile.

Do not hash presentation caches or viewport pixels as a substitute for domain source identity. A content hash alone does not replace the owner identity or temporal context.

## 4. Request sequence and latest-wins policy

Within one gesture/query stream, the coordinator assigns monotonically increasing sequence values. For pointer-driven requests:

1. A new meaningful pointer sample or parameter change increments the sequence.
2. The coordinator may cancel earlier work to conserve resources.
3. Cancellation is best-effort only; correctness does not depend on the worker stopping.
4. A result is eligible only when its sequence equals the currently expected sequence for that stream.
5. Older results are discarded even if they finish successfully.
6. A result from another gesture/session is always discarded.

Latest-wins applies to ephemeral interactive assistance such as hover, snap candidate and live preview. It does **not** mean that a user-requested deterministic calculation may be silently replaced: calculation Runs follow ME-02-07 lifecycle, input snapshot, cancellation and publication contracts.

Debouncing or coalescing may reduce redundant requests, but it cannot change the sequence or freshness rules. Do not impose an arbitrary fixed delay as a correctness mechanism.

## 5. Freshness state model

| State | Meaning | Consumer behavior |
|---|---|---|
| `Current` | Token matches active context and all required sources are unchanged | May use result for its declared read/preview purpose |
| `Superseded` | A newer request sequence exists | Discard result |
| `StaleScene` | Scene generation has been replaced | Discard and re-query if still useful |
| `StaleContext` | Map/Branch/Time/Scope/tool/coordinate context changed | Discard; gesture may need cancellation |
| `StaleSource` | A target Object/Component or Field Revision changed | Discard; re-resolve and re-query |
| `StaleTransform` | Spatial reference or transform revision changed | Discard; transform and re-query |
| `StaleParameters` | Query parameters, boundary policy or precision profile changed | Discard; issue new request |
| `Cancelled` | Work cancelled or gesture ended | Discard result; release resources |
| `Unavailable` | Required source, coverage, algorithm or coordinate capability unavailable | Return typed diagnostic; never convert to False |
| `InvalidInput` | Input geometry/context violates query contract | Return typed diagnostic |
| `Failed` | Unexpected execution error | Report failure; do not substitute an empty/negative result |

The result payload preserves the query's own semantic classification (e.g. True/False/Indeterminate for predicates) separately from freshness state. A result can be semantically True but freshness StaleSource; it is still unusable for the current operation.

## 6. Invalidation matrix

| Change event | Queries that become stale | Minimum response |
|---|---|---|
| New scene generation | All scene-bound hit-test/snap/selection queries against prior generation | Discard old results; re-resolve stable selection refs |
| New pointer sample in same stream | Prior sequence in that stream | Discard late prior responses |
| Gesture cancelled/interrupted/completed | All queries scoped to that gesture | Cancel best-effort; discard all later responses |
| Map switch | All Map-scoped queries | Cancel and discard; do not transfer |
| Branch or Historical Time switch | Queries dependent on historical Object/relationship state | Discard; source-field baseline-only queries remain bound to their Map Revision and must not be retargeted |
| Working Scope or scope revision change | Queries whose clipping, candidate set or validation depends on scope | Discard and re-query |
| Target Object/Component revision change | Queries using that target | Discard and re-resolve |
| Geometry Asset replacement | Queries using that component payload | Discard; read new immutable Asset and re-query |
| Map Source Field Revision publication | All queries/analyses depending on that Field Dataset in that Map, across Branches and Historical Times | Invalidate Map-wide; re-query from new baseline |
| Spatial-reference or transform revision change | Queries using affected coordinate conversion | Discard and recompute |
| Precision profile change | Queries whose tolerance/numerical policy depends on it | Discard and recompute |
| Algorithm/semantics version change | Cached results whose meaning depends on that algorithm | Do not reuse across versions |
| Query parameter/boundary policy change | Same query stream with changed semantics | Increment sequence and reissue |
| Coverage/tile/halo change | Queries depending on affected coverage | Discard affected results and recompute with valid coverage |

A presentation-only viewport pan/zoom does not automatically invalidate domain geometry predicates if their inputs are already in Map/Domain coordinates. It does invalidate or require recomputation of device/viewport-dependent hit-test, pixel-radius snap and screen-space overlays. Working Scope changes are separate from Canvas viewport changes.

## 7. Coordinate and tolerance discipline

- Each operand's coordinate space and spatial-reference identity are explicit.
- Transform chains are explicit and versioned; no silent CRS conversion.
- Domain geometry is never compared directly with viewport/device coordinates.
- Pixel hit radius is a presentation parameter. It is transformed through the current view scale for query execution and is never reused as a domain topology tolerance.
- Domain precision profile and operation-specific error bound are explicit; no global fixed decimal count.
- Geographic longitude/latitude coordinates are not measured with raw Euclidean formulas.
- Building-local interior plan geometry uses its declared Building-local frame and is not treated as Map XY without an explicit transform.
- Measurement results carry units, coordinate reference, precision and error/uncertainty bounds.
- If the numerical engine cannot establish the required classification, return Indeterminate/Unavailable, not a guessed True/False.

## 8. Query result envelope

Every result must carry:
- exact Query Context Token or its immutable token ID plus retrievable token;
- request sequence and gesture session ID;
- freshness classification;
- query semantic classification and typed diagnostics;
- stable Object/Component references for all witnesses;
- source fingerprints actually read by the worker;
- algorithm/version and canonical parameter fingerprint;
- coordinate/measurement context and error/uncertainty information;
- coverage information where field/tiled data is involved;
- optional witness/preview geometry clearly marked derived and non-authoritative.

A worker must report the exact source fingerprints it actually read, not merely echo the requested fingerprints. If the worker discovers a different revision while resolving the request, it returns StaleSource or restarts under a new token; it cannot label the mixed-source result Current.

## 9. Apply-time re-resolution and preflight

A query result is never a formal write precondition by itself. Before a Typed Intent can be applied:

1. **Resolve current authority context.** Re-read Project/Map/Branch/Historical Time/Working Scope and verify the intended write path.
2. **Resolve stable targets.** Re-resolve every Object ID and Geometry Component ID through Core Domain/Application Services; reject missing, replaced or incompatible targets.
3. **Check source revisions.** Compare expected Object/Component revisions, immutable Asset hashes and Field Revision IDs/fingerprints with the authoritative current state.
4. **Check spatial context.** Verify spatial-reference identity, transform version, precision profile, units and coordinate-space declarations.
5. **Re-run critical predicates/validation.** Re-execute any predicate or domain rule whose truth is required for correctness using current source snapshots and declared algorithm versions.
6. **Check semantic constraints.** Apply specialist rules (e.g. River DAG, Road endpoint identity, Building-local boundary/access rules) independently of geometric predicates.
7. **Check capability and authorization.** Verify the exact adopted descriptor/Domain Service, risk gate and any required human decision.
8. **Apply atomically through the owning service.** The query subsystem does not write Core Domain or publish Assets.
9. **Reconcile.** Return authoritative operation result, refresh scene generation and re-resolve selection by stable identity.

If any required input changed, return Stale and require a new query/gesture or explicit rebase flow. Do not silently rerun with new inputs and apply under the user's old confirmation. Re-running may update the preview, but a changed proposal must be reviewed/authorized again where authorization is required.

### 9.1 No time-of-check/time-of-use shortcut

A query that passed at time T does not guarantee that the operation is valid at apply time T+1. The application service must perform source comparison and critical validation within the same authoritative operation boundary (transaction or equivalent serialized commit boundary) that protects the formal write. The editor cannot provide this guarantee by checking immediately before calling the service.

## 10. Caching policy

V1 cache policy is conservative:
- Cache only immutable results keyed by the complete semantic token/fingerprints and algorithm version.
- Do not cache gesture-local hover/snap results beyond the active sequence.
- Do not reuse screen-space hit-test results across view-transform changes.
- Do not reuse predicate/validation results when source geometry, field baseline, coordinate reference, precision, parameters or relevant temporal context changes.
- Cache invalidation is an optimization; token comparison remains the correctness gate.
- Cache absence or eviction affects performance only, never semantic behavior.
- Derived artifacts follow their owning calculation contracts and publication lifecycle, not the interactive query cache.

A cache key must include every input that can change the answer. If the complete key is too expensive or impossible to define, do not cache that query class in V1.

## 11. Cancellation, resource limits and diagnostics

- Gesture cancellation cancels all child queries best-effort and immediately makes their results ineligible.
- New pointer samples may supersede outstanding requests; the coordinator should bound outstanding work per gesture/query stream.
- Workers must be cancellable at safe checkpoints; cancellation does not imply rollback of already completed immutable reads.
- Query work must not block the UI thread.
- Query results should be immutable values; no mutable geometry buffer is shared between worker and renderer.
- Diagnostics include query ID, gesture ID, sequence, scene generation, token mismatch category, source fingerprints and algorithm version.
- Logs should be concise and local; no telemetry service or distributed tracing infrastructure is required for V1.

## 12. Conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| C-01 | Pointer sample N starts slow snap; sample N+1 starts faster snap | N result is discarded even if it arrives last |
| C-02 | Hit-test returns after scene generation replacement | StaleScene; no selection change |
| C-03 | Map changes while query is running | StaleContext; result cannot enter new Map |
| C-04 | Branch/Time changes while historical relationship query runs | StaleContext; no cross-time reuse |
| C-05 | Field Revision is published while drainage/placement query runs | StaleSource; all dependent Branch/Time views in that Map use the new source baseline |
| C-06 | Component Asset changes while snap query runs | StaleSource; stable component is re-resolved and queried again |
| C-07 | CRS/transform changes during a geometry gesture | StaleTransform; no reuse of old converted coordinates |
| C-08 | User changes snap threshold or boundary policy | StaleParameters; new sequence and query |
| C-09 | Predicate returns Indeterminate | UI explains uncertainty; does not treat it as False |
| C-10 | Query cannot cover requested area/tile halo | Unavailable with coverage diagnostic; no fabricated negative result |
| C-11 | Old gesture query returns after gesture cancellation | Discarded; cannot update preview |
| C-12 | User confirms a proposal, then source geometry changes before apply | Apply-time preflight returns Stale; old authorization is not reused |
| C-13 | Geometry touches another object after snap preview | No semantic relationship is created without explicit typed relationship operation |
| C-14 | Viewport zoom changes but Map geometry does not | Domain predicate may remain valid; pixel-radius hit-test/snap must be recomputed |
| C-15 | A query worker reads a newer source than its token requested | StaleSource or restart under new token; never return Current with mixed revisions |

## 13. Implementation decomposition

Keep the implementation small and layered:
- **Query Context Factory:** builds immutable tokens from the active interaction context and authoritative source snapshots.
- **Query Coordinator:** assigns sequences, tracks gesture ownership, cancellation and latest-wins eligibility.
- **Map Engine Query Adapter:** resolves scene generation and executes hit-test, snap, predicate and measurement requests.
- **Specialist Validation Adapter:** invokes deterministic domain-specific validators with pinned spatial evidence.
- **Freshness Evaluator:** compares result token, sequence and source fingerprints with the active context.
- **Application Preflight:** re-resolves stable references and checks source versions inside the formal operation boundary.
- **Diagnostics Presenter:** explains stale/unavailable/indeterminate outcomes in creator language.

These are logical responsibilities, not a mandate for separate processes, services or message brokers. A few small classes within the desktop application are sufficient.

## 14. Acceptance criteria

ME-06-C is complete as a design contract when:
1. All asynchronous query classes have an immutable token and typed freshness outcome.
2. Gesture-scoped requests have monotonically increasing request sequences and latest-wins behavior.
3. Map/Branch/Time/Scope/scene/source/CRS/transform/precision/algorithm/parameter changes have explicit invalidation rules.
4. Query results preserve semantic classification separately from freshness classification.
5. Stable Object/Component identities are used; renderer/index/tile IDs are never treated as domain identity.
6. Apply-time preflight re-resolves targets and critical evidence within the authoritative operation boundary.
7. Field Revision invalidation is Map-wide across Branches and Historical Times, consistent with ME-05-01 baseline semantics.
8. Cache policy cannot weaken freshness correctness.
9. Cancellation and diagnostics are bounded and do not require distributed infrastructure.
10. Conformance scenarios cover late results, source mutation, time context, coordinate changes, uncertainty, coverage and TOCTOU.

**Disposition:** ME-06-C is complete as a spatial-query freshness design working baseline. It does not mean the query coordinator, spatial algorithms or apply-time transactional preflight are implemented. Next: ME-06-D — Typed Intent & Formal Operation Routing.

## 15. Source baselines reviewed

- ME-06-00 Shared Interaction & Tool Contracts V0.1.
- ME-06-A Tool & Operation Capability Inventory V0.1.
- ME-06-B Input Routing & Gesture Lifecycle V0.1.
- ME-01-04/05/06/07 multi-scale context, Object/geometry model, interaction framework and consistency review.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement working contract.
- ME-05-06 Cross-architecture Integration Review V0.4.
- ME-02-07 Calculation Contracts V0.4 and specialist ME-02/03/04 validation constraints.
- Map Engine immutable scene/query and geometry interaction contracts.

## 16. Change control

This document refines the shared query-freshness seam only. Specialist semantic rules remain in ME-02/03/04; coordinate/precision authority remains in ME-05; formal write authority remains in Core Domain. If implementation proves a different concurrency or query scheduling mechanism, preserve the token, freshness, apply-time validation and outcome semantics while adapting internal mechanics.
