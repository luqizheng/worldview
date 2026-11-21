# World View — ME-02-H Cross-architecture Consistency Review

- Version: 0.2
- Date: 2026-11-21
- Status: **Cross-architecture Review Working Baseline** — ME-02 design package reconciled against current Core Domain, Map Engine, Map Generation and ME-01 contracts; the source-field product/domain authority gate is closed by ME-05-01 V0.2; implementation gates remain
- Parent: ME-02 Terrain & Natural Geography Editor V0.5
- Reviewed package: ME-02-A through ME-02-G; ME-02-F V0.3; ME-02-G V0.2
- Scope: Formal authority, physical/derived field persistence, calculation lifecycle, history/Branch/shared definitions, spatial and renderer boundaries, AI task integration, specialist semantics and implementation gates

## 1. Executive conclusion

ME-02-A through ME-02-G form a coherent Natural Geography editor product and domain workflow baseline. The package is consistent with the central World View architecture:

- one unified Map Space and editor shell;
- stable semantic Object identity separated from Geometry Components and continuous physical fields;
- Core Domain authority for formal map facts and history;
- Map Engine / Spatial Services authority for deterministic calculations and derived outputs;
- AI Assistant as intent interpreter, planner, explainer and coordinator, not numerical truth or direct database writer;
- Candidate/authorization/submission only where a result is proposed for formal adoption;
- renderer-neutral immutable scenes and rebuildable presentation caches;
- local-first single-user desktop architecture.

**No new top-level subsystem or distributed infrastructure is required.** Calculation execution belongs inside the Map Engine / Spatial Services boundary. UX uses the existing Editor and AI Map Task lifecycle.

The original review identified one unresolved domain boundary: the authority and persistence lifecycle of creator-authored/imported source Field Datasets. This has now been resolved by ME-05-01 V0.2, adopted on 2026-11-21: Core Domain owns Map-scoped Field Dataset identity, immutable Field Revisions and typed Core Asset references; each new current revision applies retroactively to every Branch and Historical Time of its Map. It is not a Command/Event/State Change and does not affect another Map. P2-09-11 V0.11 and P2-09-12 V0.5 reconcile the logical schema and provider-proof design; ME-02-F V0.3 aligns calculation fingerprints and freshness. Implementation, Field Descriptor Registry, payload package/tiling and actual provider/platform tests remain open.

The package is suitable for the next bounded design/implementation-planning phase. The H-G01 product/domain authority gate is closed; implementation readiness remains subject to the explicit sub-gates below.

## 2. Review scope and evidence

The review reconciles the following current repository baselines:

### 2.1 ME-01 and ME-02

- ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1.
- ME-01-05 Map Object, Geometry & Representation Model V0.1.
- ME-01-06 Editor Interaction Framework V0.1.
- ME-01-07 Cross-architecture Consistency Review V0.1.
- ME-02 V0.5 overall Terrain & Natural Geography Editor baseline.
- ME-02-A Domain Inventory and Core Domain Mapping V0.1.
- ME-02-B Land/Water & Elevation Authoring V0.1.
- ME-02-C Terrain Feature Authoring V0.1.
- ME-02-D Hydrology & Drainage Authoring V0.1.
- ME-02-E Climate & Ecology Authoring V0.1.
- ME-02-F Calculation Contracts V0.2.
- ME-02-G UX Scenarios & Acceptance V0.2.

### 2.2 Cross-architecture sources

- Core Domain P2-09-08 Command Descriptor and Deterministic Replay Contract V0.2.
- Core Domain P2-09-11 Physical Schema Design V0.10.
- Core Domain P2-09-15 Command Descriptor Catalog V0.5.
- Core Domain P2-09-16 Shared Object Current-Definition and Checkpoint Coherence Contract V0.1.
- Core Domain P2-09-17 Command Payload Envelope and Validation Contract V0.5.
- Core Domain P2-09-19 Geometry Component Identity and Payload Contract V0.2.
- Core Domain P2-09-20 Relationship Descriptor and Temporal Contract V0.2.
- Core Domain P2-09-22 V1 Built-in Relationship Type Seed Catalog V0.2.
- Core Domain P2-09-25 V1 Geometry Asset Encoding and Coordinate Precision Decision V0.1.
- Map Rendering & Visualization Architecture V0.3.
- Renderer Contract & Scene Model V0.1.
- Spatial Presentation & Geometry Interaction Contract V0.1.
- Map Generation Specification, Workflow and Session Lifecycle V0.2.

This review evaluates design compatibility and stated authority. It does not claim code, SQL migrations, scientific algorithms, Figma screens or Windows/macOS tests have been implemented or passed.

## 3. Cross-architecture alignment matrix

| Area | Existing authority | ME-02 disposition | Review |
|---|---|---|---|
| Formal Map/Object/Geometry/history | Core Domain P2-09 | ME-02 uses typed formal operations and immutable references | Aligned |
| Object identity vs geometry | ME-01-05; P2-09-19 | Geometry edits do not infer semantic Object split/merge | Aligned |
| Formal operation catalog | P2-09-15 V0.5 | No generic CRUD; use enabled typed descriptors only | Aligned; descriptor gates remain |
| Shared Object definition | P2-09-16 | Capture current definition in calculation input identity; map-local runtime remains separate | Aligned |
| Branch/history/checkpoints | P2-09-08/11/16 | Historical context and lineage participate in freshness identity | Aligned |
| Immutable formal Assets | P2-09-11/17/19/25 | Formal adopted payloads use Core Asset and typed owner links | Aligned |
| Source physical Field Dataset | ME-05-01 V0.2 adopted contract | Core owner, Map-wide retroactive baseline semantics and typed Asset ownership defined; implementation still pending | **Domain gate closed; implementation sub-gates open** |
| Derived calculation outputs | ME-02-F V0.2 | Map Engine-owned derived artifact catalog/file namespace, not Core Asset by default | Clarified |
| Calculation execution | Map Engine / Spatial Services | Bounded local calculation subsystem | Aligned |
| AI task and Candidate | Map Generation V0.2 | Calculation Run is an internal execution unit; Candidate only for formal adoption | Aligned |
| Scene/render caches | Map Engine V0.3 / renderer contract | Consume immutable derived publication; rebuildable, non-authoritative | Aligned |
| Coordinate and spatial queries | Spatial Interaction Contract V0.1 | Typed coordinate spaces, scope and snapshot generation preserved | Aligned |
| UX / formal target selection | ME-01-07 and ME-02-G V0.2 | Explicit Initial State / History / Shared Definition target shown before write | Aligned |
| Scientific semantics | ME-02-D/E | Deterministic, explainable, bounded model; no invented precision | Aligned |
| Desktop platform/runtime | ME-01 / Map Engine | C# + Avalonia, local-first; renderer/library not frozen | Aligned; spike required |

## 4. Findings and required dispositions

### H-01 — Source Field Dataset authority adopted; implementation readiness remains gated

**Disposition: P0 product/domain gate CLOSED; implementation gate remains**

ME-02-B initially left the Field Dataset owner and persistence lifecycle open. ME-05-01 V0.2 now defines the adopted Map-owned Dataset/immutable Revision contract and typed Asset ownership. P2-09-11 V0.11 adds the two Core tables and current-revision composite FK; P2-09-12 V0.5 adds provider-level verification cases. This closes the authority/product-semantic gap without introducing a generic polymorphic Asset owner or field Command history.

A creator-authored elevation field or land/water mask is a source input, not a Derived Artifact and not merely a renderer cache. Treating it as a formal world fact requires an explicit authority and lifecycle.

**Disposition:**
1. **Closed:** ME-05/Core Domain have defined and adopted the bounded Field Dataset owner contract.
2. **Open:** Field Descriptor Registry and canonical manifest schema, immutable payload package/tiling, deferred composite-FK and immutability-trigger provider validation, and development implementation/test execution.
2. The contract must identify Map scope, spatial reference, coverage, resolution, units, no-data semantics, immutable Asset references and provenance.
3. It must define whether authored fields belong to Map Initial State, Project-shared definition or another explicit owner. Do not duplicate ownership.
4. It must define how field updates are represented: Initial State baseline authoring and/or a registered post-zero typed Command. Post-zero field editing must not be implemented through generic CRUD or by overwriting an Asset.
5. Until adopted, imports/edits may be held as provisional staging data, but must not be presented as formally adopted map facts.
6. The source-field contract must define whether field content is Map/Branch-specific or intentionally shared. Elevation and land/water inputs must not accidentally become Project-shared merely because multiple Maps reuse an Asset.

This was the only P0 cross-architecture domain gate in the ME-02 package. Its product/domain portion is now closed; implementation readiness is tracked by the remaining ME-05/Core Domain sub-gates.

### H-02 — Derived Artifact storage must remain distinct from Core Domain Assets

**Severity: P1; resolved in ME-02-F V0.2**

P2-09-11 `core_asset` and typed Asset links represent immutable content referenced by formal domain payloads and enabled Commands. A calculation output used only for analysis is not automatically a formal Core Asset.

ME-02-F V0.1 used “Asset Store” language that could be misread as requiring every derived raster/analysis output to enter `core_asset`.

**Disposition:**
- ME-02-F V0.2 clarifies that derived payloads use a Map Engine-owned derived-data file namespace and artifact catalog, with metadata in the existing project-local SQLite database under Calculation subsystem ownership.
- Do not create `core_asset` or formal typed links for every derived output.
- If a creator adopts a derived geometry/field as a formal input, publish the exact accepted bytes through the Core Domain Asset contract and reference them through the appropriate typed owner/Command link.
- Renderer SceneSnapshot, spatial indexes and viewport caches remain a third separate rebuildable representation.

No new database or distributed storage service is introduced.

### H-03 — Calculation Run must not become a second AI Task Session

**Severity: P1; aligned**

Map Generation V0.2 defines AI Map Task Session, Task Plan/Step, Candidate, Validation Report and Change Set. It explicitly permits simple direct operations and does not require every operation to create a full Session/Candidate/Change Set.

ME-02-F Calculation Run has a narrower purpose: one deterministic execution attempt against an immutable input snapshot.

**Disposition:**
- A Calculation Run may be an internal Task Step of an AI Map Task Session.
- A user-requested derived-only calculation may create the minimum run record without a user-visible AI task abstraction.
- The AI task owns user intent, task progress, candidate/review/authorization where needed.
- The calculation subsystem owns execution status, input snapshot, algorithm version, artifact production and publication.
- Do not duplicate task planning, user decision, Candidate or Change Set state in the calculation catalog.
- UI presents one coherent task/result experience; internal run details remain advanced diagnostics.

### H-04 — Calculation output must not enter deterministic history replay as an implicit side effect

**Severity: P1; aligned**

P2-09-08 requires persisted Commands to replay deterministically from captured inputs, applicable history, explicit ordering and immutable referenced Assets. External generation or mutable current calculations must not be rerun during historical reconstruction.

**Disposition:**
- A derived calculation result is not a Command or State Change.
- If the user formally adopts a derived geometry/field result, the exact accepted immutable payload is published as a formal Asset and referenced by a registered typed Command or Initial State payload.
- Historical replay consumes that immutable accepted payload and registered handler semantics; it does not rerun the original calculation or call the AI/algorithm service to regenerate geometry.
- Changing a calculation algorithm version invalidates/recomputes derived artifacts, but never silently changes the meaning of an existing formal Command.
- Formal Command compatibility and Calculation Definition compatibility remain separate version domains.

### H-05 — Branch, historical-time and Shared Object inputs are correctly separated

**Severity: P1; aligned**

P2-09-16 establishes one current Project-level Shared Object definition and Map/Branch-specific runtime history. A shared definition change intentionally affects all referencing Maps and historical reconstructions; an Event Effect changes only its owning Map/Branch runtime state.

ME-02-F binds calculations to Map/Branch/history/time context and captures Shared Object current-definition fingerprints where consumed.

**Disposition:**
- Preserve both Shared Object definition inputs and selected Map/Branch runtime inputs in the calculation snapshot.
- A Map-local event/command affecting terrain or a River must not mutate a Project-shared base definition or another Map's runtime state.
- A Shared Object definition change invalidates calculations for every Map/Branch whose relevant inputs resolve that definition.
- A Branch-local change invalidates only the affected Branch and descendants that inherit it.
- Safe cross-Branch artifact reuse is permitted only when complete resolved input fingerprints, frame, coverage, resolution and definition compatibility prove equivalence.
- Do not add Shared Object history/version tables to the calculation subsystem.

### H-06 — Initial State, History-local and Project-shared formal writes must remain distinct

**Severity: P1; aligned in ME-02-G V0.2**

ME-01-07 requires three different formal authoring paths:
1. Map Initial State baseline authoring;
2. post-zero Map/Branch historical Commands;
3. Project-shared Object current-definition authoring.

ME-02-G V0.1 described formal application generically. V0.2 adds an explicit review/Inspector requirement.

**Disposition:**
- Every formal review/apply surface identifies the target authority and temporal/cross-map effect before submission.
- “Save to map” must not hide the actual target.
- If no supported target or descriptor exists, the action is unavailable.
- Confirmation and authorization bind to the exact target context, proposal version/hash and impact.
- A calculation-only artifact publication is not one of these formal writes.

### H-07 — ME-02 formal operations must use the current Core Domain descriptor catalog

**Severity: P1; aligned with explicit gates**

P2-09-15 V0.5 is the current recommended Command Catalog; P2-09-17 V0.5 defines typed payload and immutable Asset validation. P2-09-19 defines Geometry Component identity and bounded add/remove/adjust/refine operations. P2-09-20 and P2-09-22 define relationship identity and endpoint semantics.

**Disposition:**
- All formal Object, geometry and relationship writes route through enabled typed descriptors and Core Domain services.
- No generic Object CRUD, geometry JSON patch, arbitrary relationship editor or direct table write.
- Geometry split/merge remains deferred; geometry operations do not infer semantic Object split/merge.
- Formal River flow relationships use only registered `hydrology.flows_to` endpoints River→River and River→Lake, with descriptor-defined cardinality/cycle policy.
- Ocean, Estuary, Watershed and Lake→River formal endpoints remain unsupported until their types and semantics are adopted.
- Flow direction in a derived raster/graph does not itself create formal River relationships.
- ME-02-D/E can create analysis and Candidates, but formal adoption is limited by the enabled catalog.

### H-08 — Physical fields, semantic Objects and derived analysis are not interchangeable

**Severity: P1; aligned**

ME-01-05 and ME-02 distinguish semantic Objects, continuous physical fields, derived analytical outputs and presentation.

**Disposition:**
- Never create one semantic Object per raster cell.
- A Mountain Range, River or Lake retains semantic identity independent of raster components, LOD or analysis segmentation.
- Watersheds, drainage divides, flow direction, climate classes and biome suitability remain derived unless a future Object Type is explicitly adopted.
- Climate/biome suitability never silently becomes actual forest/species presence.
- A creator-confirmed magical/ecological exception remains a world fact even if the physical suitability model disagrees.

### H-09 — Derived publication and immutable renderer SceneSnapshot are separate stages

**Severity: P1; aligned**

Map Engine V0.3 and Renderer Contract V0.1 require renderer-neutral immutable scene snapshots, generation/revision consistency, rebuildable caches and no renderer-side formal mutation.

**Disposition:**
1. Calculation subsystem validates and atomically publishes a Derived Artifact.
2. Presentation/Scene builder reads a consistent active derived publication snapshot and creates a new immutable SceneSnapshot.
3. Renderer consumes the immutable SceneSnapshot only.
4. Hit testing and snapping use compatible scene/spatial-index generations and preserve source Object/Artifact provenance.
5. A stale or superseded scene/query result is discarded; a calculation artifact does not mutate a live scene in place.
6. Scene rebuild failure does not invalidate the successfully published Derived Artifact or formal map state.

Do not merge calculation artifact status with renderer cache status.

### H-10 — Spatial scope, map extent, resolution and display LOD remain distinct

**Severity: P1; aligned**

ME-01-04 and the Spatial Presentation Contract distinguish Map Extent, Working Scope, Canvas View, Export Area, data resolution and display LOD.

**Disposition:**
- Calculation requests declare Map Extent/Working Scope and required context; Canvas View is not silently treated as the input extent.
- Tile/halo and wrap behavior follow the Map Frame contract.
- Artifact resolution is independent of zoom/display LOD.
- Derived data may be available only for partial coverage; the UI must not display it as complete.
- Coarse-to-fine aggregation is field-semantic and versioned; categorical, continuous and network fields cannot use one generic resampling rule.

### H-11 — Hydrology and climate calculation semantics are compatible

**Severity: P1; aligned**

ME-02-D defines terrain-first drainage, legitimate closed basins, a bounded single-flow-direction V1 analysis and limited formal River relationships. ME-02-E defines seasonal, causal climate estimates and the distinction between suitability and actual ecology. ME-02-F V0.2 supplies their shared calculation contract.

**Disposition:**
- Hydrology must declare downstream propagation; a local terrain edit cannot be invalidated by a naive fixed-radius-only model.
- Closed basins are not automatically errors or filled.
- Climate calculations declare global planetary inputs and local/regional influence separately.
- Rain shadows require moisture source, wind direction and terrain barrier evidence.
- Climate classification scheme/version changes invalidate classifications, not unrelated physical source fields.
- Species preference changes invalidate habitat suitability only, not climate.
- No exact discharge, flood forecast, weather prediction or population prediction is claimed by V1.

### H-12 — Calculation and publication failure handling is consistent with Core Asset/Checkpoint transaction rules

**Severity: P1; aligned after F V0.2**

P2-09-11 requires immutable file publication before short SQLite reference transactions; Checkpoint publication revalidates captured fingerprints before marking complete. ME-02-F uses the same safe ordering for derived artifact publication but keeps the derived catalog outside Core Domain formal Asset authority.

**Disposition:**
- No long calculation, raster processing or large file write occurs inside the final SQLite write transaction.
- Derived artifact bytes are finalized and hash-verified before the short publication transaction.
- Publication uses compare-and-swap against the expected prior pointer/generation and current input fingerprint.
- A failed or obsolete run cannot replace the last valid artifact.
- Startup reconciliation treats unreferenced files as unpublished orphans; it never infers successful publication from file existence alone.
- Core Domain Checkpoints continue to use their own Core Asset and fingerprint contract; calculation artifacts are not substituted for Checkpoints.

### H-13 — AI Assistant remains an interpreter/coordinator, not a numerical engine

**Severity: P1; aligned**

ME-02-A through ME-02-G consistently assign intent extraction, questions, task planning, explanation, candidate composition and user interaction to AI Assistant. Deterministic Map Engine/Spatial Services own numerical computation and validation evidence.

**Disposition:**
- LLM does not calculate raster values, invent numeric climate/hydrology results, or claim a physical model ran when no service executed.
- AI may propose algorithm/parameter choices within registered supported definitions and user authorization.
- AI cannot directly write formal Core Domain data or mutate Derived Artifact bytes.
- AI-generated content retains provenance and certainty classification.
- Story references do not automatically create Objects, events or battlefields.

### H-14 — UX contract preserves shared interaction and hides implementation complexity

**Severity: P2; aligned**

ME-02-G V0.2 reuses the shared ME-01 shell and explicitly distinguishes Initial State, History-local and Project-shared writes.

**Disposition:**
- One Canvas, one selection model, one contextual Inspector, one bottom AI conversation entry point and shared layer/navigation conventions.
- Show creator-language progress, freshness, uncertainty, affected region and next action.
- Expose run IDs, hashes, algorithm versions and numerical diagnostics only through advanced detail.
- Normal, partial, stale, warning, failed, cancelled and recovery states are required in the Figma handoff.
- Formal/derived/candidate/presentation distinctions cannot rely on color alone.

### H-15 — Renderer, geometry and numerical implementation libraries remain open

**Severity: P2; intentionally deferred**

Map Engine V0.3 recommends a renderer-neutral scene architecture and an Avalonia + Skia 2D spike, but does not freeze SkiaSharp, Mapsui, a CRS library, a geometry engine, raster encoding or GPU compute. ME-02-F likewise does not freeze a raster format, compression, numeric thresholds or CPU/GPU implementation.

**Disposition:**
- Keep the contracts independent of concrete libraries.
- V1 baseline is deterministic CPU calculation and one 2D renderer spike.
- Any GPU path must preserve declared semantics/tolerances and have a CPU fallback.
- Run representative Windows/macOS spikes before fixing performance promises or package dependencies.
- 3D/globe editing, full GIS tooling, general plugin systems and distributed compute remain outside current V1 scope.

## 5. Consolidated implementation gates

| Gate | Owner | Blocks |
|---|---|---|
| H-G01 — Field Dataset authority and Map/Branch/History semantics | **Closed by ME-05-01 V0.2** | Domain contract adopted; no longer blocks schema design |
| H-G01-I — Field Descriptor, payload package and provider implementation | ME-05 + Core Domain + Map Engine + Development | Production field publication and editing |
| H-G02 — Derived Artifact storage namespace and catalog implementation | Map Engine | Derived field production/publication implementation |
| H-G03 — Exact Calculation Definition/input fingerprint serialization | Map Engine + Core Domain | Reproducibility and cache identity implementation |
| H-G04 — Hydrology/climate influence and coverage declarations | ME-02 specialists + Map Engine | Production incremental recomputation |
| H-G05 — Formal Object/Geometry/Relationship descriptor enablement | Core Domain | Formal authoring operation availability |
| H-G06 — Scene builder consumes active artifact generation consistently | Map Engine | Derived overlay integration |
| H-G07 — Figma prototypes for normal/exception/recovery scenarios | Product UX | UX implementation readiness |
| H-G08 — Windows/macOS numerical and storage fault-injection validation | Development team | Platform/production readiness |

The gates are intentionally bounded. They do not justify a new distributed scheduler, general-purpose DAG engine, GIS clone, separate rendering process or independent world-state database.

## 6. Required changes to the ME-02 package

The review produced the following controlled updates:

1. **ME-02-F V0.2** clarifies the boundary between source Field Dataset, Derived Artifact, formal Core Asset and renderer cache. It adds an explicit source-field authority contract.
2. **ME-02-G V0.2** requires the UX to identify Initial State, History-local and Project-shared formal authoring targets before any formal write.
3. **ME-02 overall baseline V0.6** records ME-02-A through ME-02-H disposition and the remaining P0/P1 gates.
4. **Work Plan V1.1** marks ME-02-H complete and transfers the remaining implementation contracts to ME-05/ME-07 and Core Domain owners.
5. `docs/README.md` is updated to describe the complete ME-02 package.

Older versions remain in the repository for traceability. Newer documents supersede them only in the explicitly stated scope.

## 7. ME-02 package disposition

| Work item | Disposition |
|---|---|
| ME-02-A Domain Inventory | Complete — working baseline |
| ME-02-B Land/Water & Elevation | Complete — logical authoring baseline; formal Field Dataset authority adopted by ME-05-01 V0.2 |
| ME-02-C Terrain Feature Authoring | Complete — working baseline |
| ME-02-D Hydrology & Drainage | Complete — specialist workflow baseline; exact algorithms and descriptors remain gated |
| ME-02-E Climate & Ecology | Complete — specialist workflow baseline; exact calibration and field persistence remain gated |
| ME-02-F Calculation Contracts | Complete — V0.3 reconciled calculation baseline |
| ME-02-G UX Scenarios & Acceptance | Complete — V0.2 reconciled UX baseline |
| ME-02-H Cross-architecture Review | Complete — this review |

ME-02 is complete as a **product/domain/UX and cross-architecture design package**. This does not mean the natural-geography editor is implemented or that all source-field, asset-format, numerical, descriptor, renderer and platform gates are closed.

## 8. Recommended next work

1. **ME-05 Shared Geometry & Spatial Operations** should continue with Field Descriptor Registry, canonical manifest, immutable payload package/tiling and spatial service contracts. The source-field authority decision is complete; remaining work is implementation readiness rather than product-semantic discovery.
2. **ME-07 Rendering / Technical Architecture** should validate the renderer-neutral scene path, raster/derived artifact display, Avalonia hosting and Windows/macOS behavior.
3. **Core Domain** should enable only the specialist Object/Geometry/Relationship descriptors whose payload and validation gates are complete.
4. **Product UX/Figma** can proceed using ME-02-G V0.2 without waiting for numerical algorithm calibration, while preserving the stated data/authority states.

ME-03 Settlements & Urban Geography can begin domain inventory and workflow design against the ME-02 constraints. It must not assume that roads, settlement suitability, administrative regions or transport networks are formalized by the natural-geography derived layers.

## 9. Review limitations and final statement

This review verifies architecture and document consistency only. It does not claim:
- formal Field Dataset persistence has been designed at the logical schema/contract level; implementation and provider/platform validation have not been completed;
- raster storage/encoding or numerical algorithms have been selected;
- Core Domain descriptor catalogs are fully frozen or all operations enabled;
- Figma screens have been generated or usability tested;
- Avalonia/Skia/Mapsui/geometry library choices have been technically proven;
- Windows/macOS performance, numerical reproducibility or crash recovery tests have been executed.

**Final recommendation:** accept ME-02-A through ME-02-H as the Natural Geography editor design baseline, with H-G01 domain semantics closed and implementation sub-gates explicitly tracked. Keep the rest of the implementation gates with their specialist owners. Do not reopen the unified editor, Core Domain authority, local-first desktop model or deterministic calculation boundaries absent a concrete new finding.
