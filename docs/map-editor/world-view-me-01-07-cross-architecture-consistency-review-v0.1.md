# World View — ME-01-07 Cross-architecture Consistency Review

- Version: 0.1
- Date: 2026-11-21
- Status: **Cross-architecture Review Working Baseline** — consistency findings and recommended dispositions; not a frozen API/schema or implementation authorization
- Scope: Reconcile ME-01-01 through ME-01-06 with Core Domain, AI Assistant, Map Generation, Map Engine and persistence baselines
- Review package: ME-01-01 through ME-01-06
- Purpose: Identify aligned contracts, semantic conflicts, missing interaction contracts, implementation gates and required follow-up

## 1. Executive conclusion

The ME-01 package is **conceptually consistent with the current World View architecture direction**: one local-first desktop editor, one formal Core Domain authority, one shared spatial/object model, an optional built-in AI Assistant, and a Map Engine that owns deterministic spatial/presentation computation without becoming a second domain authority.

The review found no reason to replace the unified-editor architecture or introduce a second map model. ME-01-01 through ME-01-06 can remain working baselines. However, the package is **not ready to be treated as a fully frozen implementation contract**. Several important boundaries need explicit implementation constraints, and a small number of ME-01 statements need tighter qualification before V1 tool delivery.

The most important findings are:

1. **V1 formal geometry operations are narrower than the ME-01-06 conceptual tool list.** Core Domain's recommended Command Catalog defers formal geometry component split/merge pending component identity and topology rules. These tools must not be exposed as supported formal V1 operations merely because their interaction has been described.
2. **Three authoring paths must remain distinct:** Map Initial State baseline authoring, post-zero Map/Branch historical Commands, and Project-level Shared Object current-definition authoring. A generic Save/Apply/Undo flow cannot safely represent all three.
3. **The Map Engine already defines the technical interaction primitives.** ME-01-06 should consume its typed coordinate pipeline, immutable scene generations, hit-test/snap references, logical-pixel tolerances and preview lifecycle; it must not implement competing spatial behavior in the UI.
4. **AI Candidate acceptance is a decision/evidence step, not a persistence guarantee.** Final application must recheck the exact proposal, authorization, target context and Core Domain invariants; success is reported only after the formal transaction is confirmed.
5. **Timeline, Unit animation and narration are separate synchronized concerns.** The editor may coordinate them but must not turn animation frames or narration state into formal World History.

Recommended disposition: retain ME-01-01 through ME-01-06 as working baselines, use the findings below as binding design constraints for subsequent ME-02 through ME-07 work, and close the specified gaps in the relevant specialist/API contracts before implementation claims are made.

## 2. Review scope and evidence

### 2.1 ME-01 documents reviewed

| Document | Current status | Main review focus |
|---|---|---|
| ME-01-01 Editor Overall Architecture V0.1 | Architecture Working Baseline | Unified editor, mode boundaries, subsystem ownership |
| ME-01-02 World Knowledge and Story-Spatial Bridge V0.1 | Design Working Baseline | Story facts, creator intent, inference, provenance and uncertainty |
| ME-01-03 Guided Initialization V0.1 | Design Working Baseline | Adaptive conversation, initialization plan, Candidate review and formal baseline application |
| ME-01-04 Multi-scale Spatial Model & Global Consistency V0.1 | Architecture Working Baseline | Map Space, scales, Working Scope, cross-boundary dependencies and bounded recalculation |
| ME-01-05 Map Object, Geometry & Representation Model V0.1 | Architecture Working Baseline | Semantic identity, ObjectState, geometry components, representations, presentation and layers |
| ME-01-06 Editor Interaction Framework V0.1 | Editor Interaction Working Baseline | Canvas interaction, selection, tools, Edit Sessions, AI review, diagnostics and history interaction |

### 2.2 Cross-architecture sources reviewed

**Core Domain and persistence**
- [P2-09-08 Command Descriptor and Deterministic Replay Contract V0.2](../architecture/core-domain/p2-09-08-command-descriptor-replay-contract-v0.2.md)
- [P2-09-11 Physical Schema Design V0.8](../architecture/core-domain/p2-09-11-physical-schema-design-v0.8.md)
- [P2-09-15 Command Descriptor Catalog V0.1](../architecture/core-domain/p2-09-15-command-descriptor-catalog-v0.1.md)
- [P2-09-16 Shared Object Current-Definition and Checkpoint Coherence Contract V0.1](../architecture/core-domain/world-view-core-domain-p2-09-16-shared-definition-checkpoint-coherence-contract-v0.1.md)
- [Core Domain architecture and ADR baseline](../README.md)

**AI Assistant and Map Generation**
- [AI Assistant Product Architecture V1.0](../ai-assistant/world-view-ai-assistant-product-architecture-v1.0.md)
- [AI Assistant User Experience V1.0](../ai-assistant/world-view-ai-assistant-user-experience-v1.0.md)
- [AI Assistant Unified Logical Database Schema V1.16](../ai-assistant/world-view-ai-assistant-unified-logical-database-schema-v1.16.md)
- [Map Generation Specification V0.2](../map-generation/02-map-generation-specification-v0.2.md)
- [Map Generation Workflow V0.2](../map-generation/03-map-generation-workflow-v0.2.md)
- [Generation Session Lifecycle V0.2](../map-generation/04-generation-session-lifecycle-v0.2.md)
- [Map Generation documentation index](../map-generation/README.md)

**Map Engine**
- [Map Rendering & Visualization Architecture V0.3](../map-engine/world-view-map-rendering-visualization-architecture-v0.3.md)
- [Renderer Contract & Scene Model V0.1](../map-engine/world-view-renderer-contract-and-scene-model-v0.1.md)
- [Spatial Presentation & Geometry Interaction Contract V0.1](../map-engine/world-view-spatial-presentation-and-geometry-interaction-contract-v0.1.md)

These documents have different authority levels. Recommended baselines and working contracts are not silently promoted to accepted ADRs or frozen physical schemas by this review.

## 3. Cross-architecture alignment matrix

| Concern | ME-01 position | Controlling / adjacent architecture | Review result |
|---|---|---|---|
| Desktop product shape | One C# + Avalonia editor, local-first | Project architecture baseline; Map Engine V0.3 | Aligned |
| Formal map authority | Core Domain owns Object identity, geometry, history and reconstructed state | P2-09-08, P2-09-11, P2-09-15 | Aligned |
| AI authority | AI interprets, plans and stages; Domain Services apply | AI Product Architecture, Internal Tool Catalog, Generation Workflow | Aligned |
| Editor/renderer boundary | Editor owns interaction; Map Engine owns projection, hit testing, rendering and spatial calculations | Renderer Contract; Spatial Presentation Contract | Aligned; refine integration contract |
| Object/geometry separation | Geometry operations do not silently create or merge semantic Objects | P2-09-15; ME-01-05 | Aligned |
| Geometry editing | Typed geometry intent, transient preview, final domain validation | P2-09-08, P2-09-15, Spatial Presentation Contract | Partially aligned; V1 operation catalog is narrower |
| Map scale and scope | Continuous Map Space; separate Map Extent, Working Scope, Canvas View and Export Area | ME-01-04; AI Context Builder | Aligned |
| Candidate lifecycle | Staged, versioned, validated, reviewed and submitted through formal services | AI schema V1.16; Map Generation Workflow and Session Lifecycle | Aligned; exact binding and outcome semantics must be enforced |
| Initial baseline editing | Dedicated baseline authoring, distinct from post-zero history | P2-09-11; P2-09-15 | Missing explicit interaction distinction |
| Shared Object definition | One current Project-level definition, retroactive to all referencing Maps/Branches | P2-09-11; P2-09-16 | Missing explicit editor workflow distinction |
| Presentation state | Styles, labels, layer visibility and view state are not automatically formal world history | ME-01-05; Renderer Contract; P2-09-15 | Aligned; persistence owner remains open |
| Undo/Redo | Gesture cancel, local Edit Session undo and formal history reversal are distinct | P2-09-08; P2-09-15; Generation Session Lifecycle | Aligned; exact inverse support remains descriptor-specific |
| Timeline and animation | Historical Time is authoritative; animation pose is derived | Map Rendering V0.3; P2-09-08 | Aligned; editor UI coordination needs later design |
| Narration | Independent presentation subsystem synchronized with timeline/events | Map Rendering V0.3 | Not contradicted; ME-01 does not yet define its interaction surface |
| Spatial queries | Typed coordinate spaces, immutable snapshots, deterministic hit/snap candidates | Spatial Presentation Contract; Renderer Contract | Aligned; explicit generation-token integration needed |
| Technology choice | C# + Avalonia target; renderer/geometry libraries remain under evaluation | Map Rendering V0.3 recommends Skia-based 2D as leading candidate | Aligned if candidate is not misreported as frozen |

## 4. Findings and required dispositions

Severity:
- **P0 — blocking semantic conflict:** must be resolved before implementation of the affected path.
- **P1 — required V1 boundary:** architecture is viable, but the implementation must honor the disposition below.
- **P2 — follow-up design:** can be completed in the relevant specialist workstream without blocking the shared editor foundation.

### F-01 — Formal geometry split/merge is not in the current minimum V1 Command Catalog

- Severity: **P1**
- Sources: ME-01-05; ME-01-06; P2-09-08 V0.2; P2-09-15 V0.1; Spatial Presentation & Geometry Interaction Contract V0.1.
- Observation: ME-01-06 lists Split Geometry and Merge Geometry as built-in tool examples. P2-09-15 explicitly defers formal geometry component split/merge from the minimum V1 catalog pending stable component identity and topology rules.
- Risk: UI affordances could imply a supported operation that Core Domain cannot author or replay deterministically. An editor-only mutation would violate formal geometry authority.
- Disposition:
  1. Keep split/merge as conceptual future tools, not V1 committed operations.
  2. V1 may provide read-only preview of a split/merge calculation only if the Map Engine can produce a deterministic, inspectable result and no formal write is implied.
  3. Do not enable formal submission until Core Domain adopts a typed descriptor, component identity behavior, provenance rules and replay semantics.
  4. Semantic Object split/merge remains a separate Object Operation and must never be inferred from geometry decomposition.
- Owner: Core Domain + Map Editor specialist design.
- Closure gate: adopted descriptor and tests for identity, component references, topology, history and deterministic replay.

### F-02 — Three formal authoring paths need distinct UI and application semantics

- Severity: **P1**
- Sources: P2-09-11 V0.8; P2-09-15 V0.1; P2-09-16 V0.1; ME-01-05; ME-01-06.
- Observation: The Core Domain distinguishes (a) Map Initial State baseline authoring, (b) post-zero Map/Branch Commands, and (c) Project-level Shared Object current-definition edits. P2-09-16 states that a Shared Object definition edit is outside Historical Time and affects all referencing Maps and historical reconstructions.
- Risk: A generic “Apply to Map” or Undo action could write a baseline fact as a historical Command, write a historical change into a shared definition, or suggest that a Project-level definition edit is Branch-local.
- Disposition:
  1. The application operation contract must declare its authoring target: Initial State, Map/Branch History, or Shared Definition.
  2. UI wording and confirmation summaries must identify the scope and historical effect.
  3. Initial State authoring uses dedicated Core Domain baseline services; it is not represented as a post-zero Command.
  4. Post-zero edits use registered typed Commands in the selected History and obey Branch fork/time constraints.
  5. Shared Object definition edits use a dedicated Core Domain definition-authoring service; they are Project-level and retroactive under P2-09-16.
  6. The UI must explain the cross-Map and historical-reconstruction impact of a Shared Object definition change.
- Owner: Core Domain + Editor Application.
- Closure gate: operation-routing contract and tests for all three paths, including Branch and shared-reference cases.

### F-03 — UI tools must map to registered semantic Command descriptors, not generic CRUD

- Severity: **P1**
- Sources: P2-09-08 V0.2; P2-09-15 V0.1; ME-01-05; ME-01-06.
- Observation: Core Domain owns a statically registered descriptor catalog. A Command records author intent and deterministic execution semantics; generic Create/Update/Delete/Replace Geometry persistence commands are explicitly rejected.
- Risk: Tool names or gesture handlers may become de facto database operations with no stable historical meaning.
- Disposition:
  1. Every formal-write tool declares the typed intent family it can produce.
  2. A UI gesture may be translated into different intents based on explicit user/tool context, but it must not submit an untyped patch.
  3. Object creation, geometry adjustment/refinement, relationship establishment/end and presentation-only changes remain separate operation families.
  4. Unsupported operation types are disabled or clearly preview-only.
  5. The descriptor catalog, not the editor, decides whether an operation is valid in Initial State or post-zero History.
- Owner: Core Domain descriptor catalog + Editor Application.
- Closure gate: operation-to-descriptor mapping table for every V1 tool.

### F-04 — Immutable scene generation and query freshness must be carried into editor interaction

- Severity: **P1**
- Sources: Renderer Contract & Scene Model V0.1; Spatial Presentation & Geometry Interaction Contract V0.1; ME-01-06.
- Observation: Map Engine queries are bound to immutable scene/index generations. A HitResult is a reference into the queried snapshot, not a durable Command. Obsolete query results must be discarded when scene generation or request token changes.
- Risk: Delayed hit-test/snap results could select or edit geometry from an old Branch, Historical Time, projection or geometry snapshot.
- Disposition:
  1. Every interaction query carries scene identity/generation and active Map/Branch/Time context.
  2. The controller accepts a result only if the query token and scene generation still match the active snapshot.
  3. Before formal application, re-resolve Object/Component references and validate source geometry fingerprint/context through Domain Services.
  4. On Branch/Time/scene replacement, cancel obsolete pointer queries and active geometry gestures.
  5. Do not persist RenderItemId, screen coordinates, hit-test order or snap candidate IDs as formal geometry identity.
- Owner: Map Engine + Editor Application.
- Closure gate: stale-query, stale-scene, Branch-switch and pointer-race tests.

### F-05 — Map Extent, Working Scope, Canvas View and Export Area must remain separate through every API

- Severity: **P1**
- Sources: ME-01-04; ME-01-06; AI Context Builder; Map Rendering V0.3.
- Observation: ME-01-04 distinguishes four spatial concepts. Map Engine camera/viewport state is view-only. AI Context Builder explicitly warns that current viewport must not constrain a user-requested broader task.
- Risk: An API that accepts only a viewport rectangle could accidentally limit generation/validation/export or treat a camera movement as a scope change.
- Disposition:
  1. Read/query/task contracts explicitly identify which spatial scope they use.
  2. Canvas View is used for rendering and pointer interaction only.
  3. Working Scope is the default authoring/analysis context only when the user has not specified a broader/narrower target.
  4. Map Extent describes intended map coverage, not a clipping rule for all operations.
  5. Export Area belongs to layout/export and does not alter authoring scope.
  6. A temporary task selection is not a formal Region Object unless the user explicitly creates one.
- Owner: Editor Application + AI Context Builder + Map Engine.
- Closure gate: scope contract and tests proving viewport changes do not alter operation scope.

### F-06 — Candidate acceptance, authorization and formal application must be separate observable steps

- Severity: **P1**
- Sources: AI Assistant Logical Schema V1.16; Internal Tool Catalog V1.0; Map Generation Workflow V0.2; Generation Session Lifecycle V0.2; ME-01-06; P2-09-11 V0.8.
- Observation: AI proposal/authorization/submission evidence is separate from Core Domain data. Authorization binds to exact proposal version/hash and target context; the final application transaction rechecks it and persists formal Commands plus immutable submission evidence.
- Risk: The UI could show “Accepted” as if the map were already updated, reuse approval after a Candidate changes, or retry an uncertain submission and duplicate Commands.
- Disposition:
  1. Distinguish “review decision recorded”, “submission in progress”, “applied”, “rejected”, “stale”, “failed” and “outcome unknown / reconciling”.
  2. Bind review/authorization to exact Candidate or proposal version, semantic content hash, target History and context precondition.
  3. Recheck authorization and current target context immediately before formal application.
  4. Report “Applied” only after the Core Domain/Application transaction confirms success.
  5. On uncertain commit response, reconcile by idempotency/submission reference before retry.
  6. Partial acceptance is permitted only where dependency and validation rules prove the selected subset coherent.
  7. AI Task cancellation stops unfinished work but does not reverse already committed Commands.
- Owner: AI Assistant + Editor Application + Core Domain transaction integration.
- Closure gate: acceptance/submission state mapping and atomicity/idempotency proof tests.

### F-07 — Geometry interaction must consume the Map Engine's typed coordinate and precision contracts

- Severity: **P1**
- Sources: Spatial Presentation & Geometry Interaction Contract V0.1; Renderer Contract & Scene Model V0.1; Map Rendering V0.3; ME-01-06.
- Observation: Domain CRS coordinates, Map coordinates, viewport logical pixels and device pixels are distinct. Hit/snap tolerances are screen-logical-pixel values; formal geometry precision and topology tolerances belong to Core Domain. Bézier curves remain first-class.
- Risk: Editor code may compare coordinates from different spaces, scale tolerances incorrectly with zoom/DPI, flatten Bézier geometry into formal data, or treat renderer pixels as domain coordinates.
- Disposition:
  1. Use typed coordinate wrappers and explicit transforms for all interaction boundaries.
  2. Keep pointer tolerance in logical viewport pixels and convert conservatively through the active transform.
  3. Preserve formal line/Bézier representation; flatten only for derived indexing/render/hit-test approximations.
  4. Keep render, hit-test, snap, numerical robustness and domain-validation tolerances separate.
  5. Treat snap as a typed proposal; Core Domain validates any accepted formal operation.
  6. A spatial reference change is a domain transformation/migration, not a viewport preference.
- Owner: Map Engine + Geometry/Spatial Services + Editor Application.
- Closure gate: coordinate-space compile-time/type tests, DPI/zoom tests, Bézier projection/hit-test tests and snap non-mutation tests.

### F-08 — Timeline, animation and narration need coordinated but independent UI contracts

- Severity: **P2**
- Sources: Map Rendering & Visualization Architecture V0.3; Core Domain P2-09-08; ME-01-01; ME-01-06.
- Observation: Timeline owns selected Historical Time and playback progression. Animation Runtime derives visual poses from selected time and domain movement inputs. Narration is an independent presentation subsystem that may synchronize with time/Event references and remains usable when the map viewport is hidden.
- Risk: A single “play map” interaction could conflate historical reconstruction, Unit movement animation and narration playback, or persist frame samples as formal State Changes.
- Disposition:
  1. Keep one authoritative selected Historical Time.
  2. Timeline changes request the correct Core Domain reconstructed state; animation evaluates a derived pose for that time.
  3. Narration has independent play/pause/visibility controls while optionally following timeline/Event synchronization.
  4. Animation frames, trails used only as display effects and narration presentation state do not create formal State Changes.
  5. Later timeline UX design should explicitly define synchronization and independent playback controls.
- Owner: Timeline / Animation / Narration product design.
- Closure gate: dedicated timeline interaction design and deterministic seek/play/pause tests.

### F-09 — Presentation configuration persistence remains an explicit ownership decision

- Severity: **P2**
- Sources: ME-01-05; ME-01-06; Renderer Contract & Scene Model V0.1; P2-09-11 V0.8.
- Observation: Layer visibility/order, style profiles, label placement, recent viewport and panel layout are not automatically formal world facts. ME-01-05 leaves persistence ownership for layers/styles open; renderer scenes are rebuildable from formal state plus view configuration.
- Risk: UI preferences may be stored in Core Domain history, or important project presentation settings may be lost because they were treated as disposable transient state.
- Disposition:
  1. Classify each setting as per-user editor preference, Map/project presentation configuration, export/layout configuration or formal domain fact.
  2. Persist each category through one explicit owner; do not create duplicate settings in Core Domain, Editor and renderer caches.
  3. Keep renderer caches, projected geometry, hit indexes and GPU resources rebuildable and non-authoritative.
  4. Do not add a separate layer/style database authority until a concrete ownership contract requires it.
- Owner: Editor Application + Presentation Architecture.
- Closure gate: ME-05/ME-06 persistence ownership decision before production schema.

### F-10 — Story-derived facts and Candidate overlays must retain knowledge provenance

- Severity: **P1**
- Sources: ME-01-02; ME-01-03; AI Assistant Product Architecture; Map Generation Specification V0.2; ME-01-06.
- Observation: Story statements, confirmed creator intent, existing map facts, source material, AI inference and AI-created content are distinct. Candidate visualization cannot flatten them into a single “AI generated” category.
- Risk: A plausible spatial interpretation could be presented as a confirmed world fact; a story mention could create an Object or spatial event without creator intent.
- Disposition:
  1. Candidate items retain provenance, authority/status, uncertainty and unresolved assumptions.
  2. The Inspector/AI panel must expose the basis for important proposed objects and relationships.
  3. Story mentions do not automatically create map Objects; war/event narration does not automatically create a battlefield or Unit movement.
  4. Guided Initialization must preserve confirmed settings and distinguish questions, inferences and proposed spatial consequences.
  5. A visual overlay must not imply that an inferred relationship is formal until applied through the relevant domain operation.
- Owner: AI Assistant + World Knowledge / Guided Initialization.
- Closure gate: provenance-to-Candidate presentation mapping and tests for story-only, inferred and confirmed content.

### F-11 — Rendering technology recommendation must not be promoted to a frozen dependency

- Severity: **P2**
- Sources: Map Rendering V0.3; Renderer Contract V0.1; ME-01-01; ME-01-06.
- Observation: Skia-based custom 2D rendering is the leading V1 candidate, while the renderer-neutral scene boundary is the recommended architecture. Mapsui remains optional for basemaps/interoperability; no dedicated Vulkan/Metal/WGPU stack is recommended for V1 absent measured need.
- Risk: Specialist editor design could hard-code Skia or Mapsui types into domain/interaction contracts before the implementation spike.
- Disposition:
  1. Keep scene, interaction query and geometry service contracts renderer-neutral.
  2. Treat Skia as the leading backend candidate, not a final technology freeze.
  3. Use a technical spike to validate Avalonia integration, performance, text, raster, DPI and Windows/macOS packaging.
  4. Keep Mapsui optional and behind an adapter for concrete basemap/interoperability needs.
- Owner: Map Engine technical evaluation.
- Closure gate: recorded implementation spike and platform validation.

## 5. Consolidated architecture constraints

The following constraints should be treated as cross-workstream design rules from this review:

1. **One formal authority:** Core Domain owns formal Map/Object/Geometry/Relationship/History state. No Editor, AI or Map Engine subsystem may create a competing authority.
2. **Three state categories:** Editor transient state, AI Candidate/task state and formal Core Domain state remain separate.
3. **Three authoring paths:** Initial State baseline, Map/Branch historical Command and Project Shared Object definition use different domain services and user-visible scope.
4. **Typed operations:** Every formal edit resolves to a registered semantic Core Domain operation. No generic geometry CRUD or arbitrary JSON patch.
5. **Object/geometry separation:** Geometry component operations never imply semantic Object lifecycle changes.
6. **V1 catalog restraint:** Geometry component split/merge remain unavailable as formal V1 operations until their descriptors and identity/topology rules are adopted.
7. **Explicit spatial scope:** Map Extent, Working Scope, Canvas View and Export Area remain distinct across UI, AI context and service contracts.
8. **Renderer is read-only:** Rendering consumes immutable scene snapshots; it never reads history tables during painting or writes formal geometry.
9. **Interaction results are ephemeral references:** Hit-test/snap/selection results are bound to a scene generation and must be revalidated before formal application.
10. **Coordinate safety:** Domain, Map, viewport-logical and device-pixel coordinates are distinct typed spaces.
11. **AI is risk-based:** Simple low-risk authorized work may apply directly; complex/high-impact work uses Candidate/Change Set review.
12. **Acceptance is not persistence:** Candidate acceptance is bound to exact content/context; only confirmed Core Domain application is reported as applied.
13. **No implicit rollback:** Cancelling a gesture, Edit Session or AI Task does not undo formal Commands already committed.
14. **Presentation is not world history:** Layer visibility, style, label placement, viewport and animation samples do not become formal historical changes by default.
15. **Timeline is authoritative for time:** Animation and narration synchronize with the selected time but keep independent responsibilities.
16. **Provenance survives presentation:** Story fact, creator intent, source material, inference and AI creation remain distinguishable in review and inspection.
17. **Local-first resilience:** Manual editing, local persistence and export remain available when model/provider/network services are unavailable.
18. **Keep V1 bounded:** No generic plugin/macro system, separate rendering process, general-purpose GIS clone or enterprise collaboration architecture.

## 6. Required follow-up work and ownership

| Priority | Follow-up | Owner/workstream | Required output |
|---|---|---|---|
| P1 | Map each V1 editor tool/gesture to an adopted Core Domain operation and descriptor | ME-02/03/04 + Core Domain | Tool-to-operation matrix |
| P1 | Define distinct Initial State, historical Command and Shared Definition authoring routes | Core Domain + Editor Application | Authoring-target and confirmation contract |
| P1 | Integrate scene generation/query tokens into selection and edit-session contracts | Map Engine + Editor Application | Interaction query integration contract and race tests |
| P1 | Define Candidate review decision to submission state mapping | AI Assistant + Editor Application | Candidate review/application UX and state mapping |
| P1 | Define provenance/status presentation for story-derived spatial Candidates | World Knowledge + AI Assistant | Candidate provenance/Inspector contract |
| P1 | Exclude unsupported geometry split/merge from formal V1 tool availability | Core Domain + Editor | V1 tool catalog qualification |
| P2 | Define timeline, animation and narration interaction coordination | Timeline / Animation / Narration | Timeline UX and synchronization contract |
| P2 | Decide persistence owner for layer/style/view configuration | Presentation Architecture | Configuration ownership contract |
| P2 | Complete Avalonia + Skia renderer and spatial interaction technical spikes | Map Engine | Windows/macOS spike report |
| P2 | Define exact shortcuts, touch/pen support and accessibility behavior | Editor UX | Interaction specification / usability review |

## 7. ME-01 package disposition

| Document | Disposition | Required action |
|---|---|---|
| ME-01-01 Overall Architecture | Retain | No structural rewrite required; keep Map Engine/AI boundaries |
| ME-01-02 World Knowledge Bridge | Retain | Ensure provenance survives Candidate and Inspector presentation |
| ME-01-03 Guided Initialization | Retain | Use the three authoring paths and exact Candidate application rules |
| ME-01-04 Multi-scale Consistency | Retain | Propagate the four spatial scopes into API/service contracts |
| ME-01-05 Object/Geometry/Representation | Retain | Align V1 geometry operation availability with the Core Domain catalog |
| ME-01-06 Interaction Framework | Retain with qualification | Treat split/merge as deferred formal operations; integrate scene-generation freshness and authoring-target distinctions in detailed contracts |
| ME-01-07 Cross-architecture Review | Complete as working baseline | Track F-01 through F-11 in subsequent specialist design and implementation gates |

No ME-01 document is promoted to Frozen by this review. No final geometry library, renderer backend, persistence schema, public API or Avalonia control contract is selected here.

## 8. Recommended next work sequence

1. **ME-02 Terrain & Natural Geography Editor:** define terrain/elevation, landform, coast, watershed, river, lake, climate and ecology authoring workflows; map every formal operation to the Core Domain catalog and deterministic Map Engine capabilities.
2. **ME-03 Settlements & Urban Geography Editor:** define settlement hierarchy, land use, road/route networks, political/administrative boundaries and cross-scope consistency.
3. **ME-04 Buildings & Interior Spaces Editor:** define building/site association, local coordinate frames, floors, rooms, entrances and vertical/underground connections.
4. **ME-05 Shared Geometry & Spatial Operations:** close geometry component identity, supported geometry operations, validation, topology and asset contracts; resolve the deferred split/merge decision only if justified.
5. **ME-06 Shared Interaction / Tool Contracts:** turn the working interaction model into concrete operation-to-tool, query-generation, gesture and Candidate review contracts.
6. **ME-07 Rendering / Technical Architecture:** complete renderer/geometry library evaluation, Avalonia integration, performance and Windows/macOS validation.

The specialist designs must use the findings in this review as constraints. They must not silently reopen formal Core Domain authority, stable Object identity, Branch/history semantics or the local-first product shape.

## 9. Review limitations

- This review reconciles the current repository documents and their stated authority/status. It does not assert that code, database migrations, renderer integration or platform tests have been implemented or passed.
- P2-09-11 V0.8 is a physical-schema proposal, not executable DDL. P2-09-12 provider/transaction proof remains a development gate.
- P2-09-15 is a recommended V1 Command Catalog; proposed IDs and deferred families must be adopted through the Core Domain's own decision process before they are treated as production descriptors.
- Map Engine V0.3 and its renderer/spatial contracts are working/recommended baselines; Skia and external geometry libraries remain subject to technical spikes.
- Exact layer/style/view configuration persistence, timeline UX and specialist tool payloads remain open.
- No implementation completion is claimed.

## 10. Final review statement

ME-01-01 through ME-01-07 now form a coherent **Map Editor product and architecture working baseline**. The shared editor, spatial model, semantic Object model, interaction framework and subsystem boundaries are sufficiently established to begin specialist editor design.

The remaining work is not to invent another top-level architecture. It is to close the bounded contracts listed in this review, validate the renderer/geometry implementation on the target desktop platforms, and ensure each specialist tool maps to an explicit Core Domain operation with deterministic validation and safe formal application.
