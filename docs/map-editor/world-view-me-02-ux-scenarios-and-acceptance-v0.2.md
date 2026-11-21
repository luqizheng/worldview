# World View — ME-02-G UX Scenarios & Acceptance

- Version: 0.2
- Date: 2026-11-21
- Status: **Natural Geography UX Working Baseline** — end-to-end creator scenarios, interaction behavior and acceptance criteria, reconciled with the three formal authoring targets in ME-01-07; final visual design, control styling, exact shortcuts and usability thresholds remain Figma/usability validation work
- Supersedes: ME-02-G V0.1 (retained for history)
- Parent: ME-02 Terrain & Natural Geography Editor V0.4
- Depends on: ME-01-01 through ME-01-07; ME-02-A through ME-02-F; Map Generation Specification/Workflow/Session Lifecycle V0.2; Map Rendering & Visualization Architecture V0.3; Renderer Contract & Scene Model V0.1; Spatial Presentation & Geometry Interaction Contract V0.1
- Target: C# + Avalonia desktop application, Windows/macOS, local-first, single-user
- Scope: Natural Geography creator journeys, Canvas/Inspector/AI conversation behavior, calculation progress and freshness, diagnostics, Candidate review, accessibility and acceptance scenarios

## 1. Purpose

ME-02-A through ME-02-F define the domain concepts, authoring operations, scientific constraints and calculation lifecycle for Terrain & Natural Geography. ME-02-G translates those contracts into coherent creator-facing workflows and testable UX acceptance.

The goal is not to teach the creator GIS, hydrology or climatology. The application should let a creator express a world-building intention, work directly on the map, understand the consequences of changes and decide which proposed formal facts to adopt.

The UX must consistently distinguish:
- what the creator said or drew;
- what is a confirmed formal world fact;
- what the system calculated from available inputs;
- what the AI inferred or proposes;
- what is unknown, incomplete, stale or uncertain;
- what is merely a visual presentation choice.

**Primary recommendation:** keep one shared Canvas-centered editor shell. Natural Geography supplies mode-specific tools, inspector sections, analysis views and diagnostics; it does not create a separate workspace, selection model, history, layer authority or task system.

## 2. UX principles and product boundaries

1. **Creator language first.** Use “mountain height”, “where water gathers”, “rainfall”, “cold season” and “forest suitability” before exposing technical terms.
2. **Canvas remains primary.** The map is the main work surface. Panels explain and support it; they must not turn the application into a form-heavy GIS console.
3. **One persistent AI conversation entry point.** The creator can describe intent, ask why a result occurred, request analysis or refine a candidate without switching to a separate AI product.
4. **Contextual Inspector.** The right panel follows the current selection, field, tool, calculation result or diagnostic.
5. **Progressive disclosure.** Show essential state first; expose assumptions, provenance, numeric parameters and technical diagnostics on demand.
6. **Preview before consequence.** Transient sketch/brush preview never silently becomes formal map data.
7. **No hidden destructive recomputation.** Recalculation updates derived outputs only; it does not overwrite protected creator facts or silently replace formal Objects.
8. **Keep the last valid result.** A calculation failure must not blank the map or destroy the previous usable analysis.
9. **Explain uncertainty honestly.** Incomplete coverage, approximate inputs and model limitations are visible and actionable.
10. **Confirmation is contextual.** Confirm semantic/high-impact formal changes, not every harmless view or derived-only refresh.
11. **Same object, multiple views.** Natural Geography and Fantasy Cartography are presentations of the same map facts.
12. **No mandatory AI/network dependency.** Manual authoring, inspection and local calculation remain usable offline.

## 3. Shared workspace composition

### 3.1 Desktop layout

The shared editor shell remains:

- **Main Canvas:** dominant central map surface, with active mode overlay, direct authoring, selection, preview and impact visualization.
- **Contextual Inspector (right):** current selection/result/tool properties, source/provenance, validation and relevant actions.
- **AI Conversation (bottom):** persistent natural-language input and response stream; can expand for longer exchanges but should not displace the Canvas as the main surface.
- **Tool shelf:** compact mode-specific tools near the Canvas; only relevant tools are prominent.
- **Layer/content navigator:** visibility and ordering of authored inputs, derived analysis and presentation layers.
- **Shared status/context strip:** active Map/Branch/time, Working Scope, calculation activity and important stale/diagnostic indicators.

The exact left/right placement of navigator/tool shelf can be finalized in Figma, but the Canvas, contextual Inspector and bottom AI conversation relationship is the product baseline.

### 3.2 Persistent context

At all times where relevant, the user can identify:
- active Map and Branch/history context;
- current Working Scope (World / Region / Locality / Site);
- current mode and active tool;
- selected Object/geometry/field/result;
- whether displayed content is authored, formal, derived, candidate or presentation;
- whether derived data is current, recalculating, stale, partial or unavailable.

Do not overload the status strip with technical fingerprints or raw job IDs. Those belong in expandable diagnostics.

### 3.3 Layer grouping

The natural-geography layer navigator should group content by meaning and authority:

1. **Authored & Formal Geography** — landmass, mountain range/peak, river/lake, authored geometry and physical source fields.
2. **Derived Analysis** — elevation derivatives, drainage, climate, classification and ecological suitability.
3. **Presentation** — hillshade styling, contours, palettes, labels, symbols and fantasy cartography.

Layer visibility is a view choice. Hiding a layer does not delete or deactivate its source data. A derived layer may be unavailable or stale without affecting formal Object visibility.

### 3.4 Status language

Prefer creator-facing labels:
- “Up to date”
- “Updating this area”
- “Some areas need more detail”
- “Based on older terrain data”
- “Partially calculated”
- “Could not update — previous result is still shown”
- “Needs your review”
- “Confirmed world fact”
- “AI suggestion”

Technical detail can show the underlying run/artifact IDs and fingerprints in an advanced diagnostic section.

## 4. Scenario A — Start from a story description

**Creator intent:** “I want a large continent with a cold north, a warm southern coast, a high mountain chain in the west and a dry inland basin.”

### Flow

1. Creator opens a new Map and starts the built-in Guided Initialization conversation.
2. AI acknowledges the creative intent and separates known facts, preferences, creative freedom and unresolved choices.
3. AI asks only questions that materially affect the next world-frame or geography decision. It avoids asking the creator to select a CRS, projection or raster resolution.
4. Based on answers, the system proposes a world frame and coarse land/water arrangement.
5. Canvas displays a clearly marked proposal, with the proposed extent and key geographic assumptions.
6. Inspector summarizes the proposal in creator language: world size/shape, wrap behavior, major landmass placement and unresolved choices.
7. Creator can edit the sketch directly, answer another question, mark an assumption as protected, or reject part of the proposal.
8. The system generates an editable initialization proposal. Formal application follows the applicable Initial State authorization path.
9. After application, derived analysis is offered progressively; missing elevation or planetary assumptions are explained rather than fabricated.

### UX requirements

- Questions are adaptive and do not repeat already confirmed facts.
- The creator may say “I don't know”, skip, revise or contradict an earlier answer.
- The assistant distinguishes “you specified” from “I suggest”.
- No character, civilization, war or story event is automatically turned into a geographic Object merely because it appears in prose.
- The preview communicates what will become formal and what remains an assumption or derived output.
- A user can exit the conversation and continue by drawing.

### Acceptance

- A story-only start can reach a reviewable coarse geography proposal without technical GIS input.
- Reopening the flow preserves confirmed answers and does not ask them again unnecessarily.
- Rejecting or cancelling the proposal leaves formal map state unchanged.
- Applying the proposal creates only the explicit supported formal facts; analysis layers remain derived.
- The UI identifies unresolved physical inputs that limit later calculations.

## 5. Scenario B — Draw land, water and elevation directly

**Creator intent:** sketch a coastline, add islands, create a broad plateau and raise a mountain range.

### Flow

1. Creator selects Land/Water or Elevation tool.
2. Tool options show a small set of creator-facing controls, such as brush size, strength, smoothing and affected scope.
3. Canvas shows a transient preview while the pointer moves or a stroke is in progress.
4. Inspector displays the active field's authority, coverage, resolution and no-data status in plain language.
5. Creator completes the gesture; a local preview and impact summary appear.
6. The system checks map-edge behavior, boundary context, valid coverage and protected source regions.
7. Creator commits or cancels the edit according to the operation's formal authoring path.
8. The field/source revision is published immutably; dependent analyses are marked as updating or stale.
9. The previous valid derived result remains visible until a replacement is validated and atomically published.

### UX requirements

- Landmass semantic identity is not derived from connected raster component count.
- Coastline is shown as a derived boundary, not silently authored as a second competing Object.
- Unknown/no-data is visually distinguishable from zero elevation or water.
- Brush preview is not formal persistence; cancellation leaves source data unchanged.
- Local refinement shows whether the edit reaches tile boundaries or requires more neighboring context.
- The user sees the affected area and major dependent analyses before committing a high-impact change.

### Acceptance

- Cancelled strokes leave no formal field revision.
- A committed local edit records its source/provenance and correct spatial coverage.
- Tile-edge edits do not introduce visible seams in the accepted result.
- Missing neighboring context is reported as incomplete, not treated as a natural boundary.
- A derived coastline refresh does not change Landmass Object identity.
- The user can distinguish zero, unknown and no-data values.

## 6. Scenario C — Author and refine terrain features

**Creator intent:** create a mountain range, place a named peak and classify a plateau.

### Flow

1. Creator selects Mountain Range and sketches its footprint/ridge.
2. Inspector asks for only meaningful semantic properties (such as name) and shows geometry roles.
3. The system may offer detected ridges/peaks as suggestions, clearly marked as derived candidates.
4. Creator chooses whether to create a formal Mountain Range, adjust its geometry or keep it as a candidate.
5. Creator places a Mountain Peak and optionally establishes the supported structural relationship to the range.
6. Terrain Region classification is authored or derived within the bounded vocabulary.
7. Terrain/elevation consistency checks run and show relevant warnings.

### UX requirements

- A local elevation maximum is not automatically a named Mountain Peak.
- Proximity does not imply a formal peak-belongs-to-range relationship.
- A plateau is not presented as a mountain range.
- Geometry edit and semantic Object operation are visually distinct.
- The UI does not offer formal geometry split/merge if the Core Domain descriptor gate is not closed.
- Derived feature detection never overwrites creator-authored geometry without an explicit adoption path.

### Acceptance

- A semantic Object can span multiple Working Scopes without duplicate identities.
- A peak can exist without a range relationship.
- The relationship is only created through the supported typed operation.
- Detected feature candidates can be accepted, edited or dismissed independently.
- Geometry preview cancellation does not alter formal geometry or Object identity.

## 7. Scenario D — Analyze drainage and author rivers/lakes

**Creator intent:** “Show the major rivers. Keep this inland basin closed, and make this northern river join the main river.”

### Flow

1. Creator requests drainage analysis for the current Working Scope.
2. The Inspector checks and summarizes prerequisites: elevation coverage, land/water inputs, map-edge behavior and known lakes/depressions.
3. If critical inputs are missing, the system explains the blocker and offers the smallest useful next action; it does not run an apparently complete analysis.
4. Calculation progress is shown by meaningful phases/areas, while the prior valid result remains available.
5. The Canvas displays derived flow tendencies, basin boundaries and channel candidates with clear visual distinction from formal Rivers.
6. A detected depression is classified as closed/possible/unresolved according to evidence; it is not automatically filled.
7. Creator selects a channel candidate and adopts/refines it as a formal River where supported.
8. Creator reviews each supported River-to-River or River-to-Lake relationship. Unsupported ocean/estuary endpoint semantics are not represented as fake generic edges.
9. Validation explains uphill segments, unsupported endpoints, cycles or water-balance concerns.
10. Accepted formal changes route through Core Domain; derived flow fields remain analysis artifacts.

### UX requirements

- Flow direction is visually distinct from River line geometry.
- Formal relationship direction is explicit: source/upstream to receiver/downstream.
- The UI does not imply that a derived channel candidate is already a formal River.
- Closed inland basins are first-class valid interpretations, not errors.
- Unsupported network topology is explained as a current capability boundary.
- No exact discharge/flood forecast is shown unless a validated model actually supports it.

### Acceptance

- Analysis is blocked or marked partial when required elevation coverage is inadequate.
- A legitimate endorheic basin is preserved.
- A derived channel does not automatically create a formal River.
- Formal River connectivity uses only registered endpoint/type/cardinality rules.
- A local upstream elevation edit invalidates affected downstream drainage results.
- If recalculation fails, the last valid drainage overlay remains available and is labelled accurately.

## 8. Scenario E — Understand climate and ecological suitability

**Creator intent:** “Why is the land east of the mountains dry? Could this region support coniferous forest?”

### Flow

1. Creator selects a region or asks a natural-language question.
2. The assistant identifies relevant inputs and displays the calculation coverage and assumptions.
3. Climate layers show seasonal temperature/precipitation patterns and their confidence/coverage.
4. Creator selects a location/region; Inspector explains the main causal factors in creator language (latitude/season, elevation, ocean distance, prevailing wind, moisture source, mountain barrier).
5. Creator switches between seasonal phases and climate classification.
6. Biome suitability is displayed separately from actual creator-authored ecosystem/forest facts.
7. If the creator has specified a magical forest or other exception, the system preserves it and explains the difference between physical suitability and world fact.
8. Creator may request a local recalculation or revise a source assumption.

### UX requirements

- Climate is not presented as an arbitrary independent paint layer.
- Seasonal phase names and hemisphere behavior are understandable.
- Rain-shadow explanation requires a plausible moisture source, wind direction and terrain barrier.
- Suitability is not interpreted as actual species presence or population.
- Classification scheme and approximation are disclosed.
- A climate result with insufficient physical input is visibly incomplete/uncertain.

### Acceptance

- Inspector can explain the major input causes behind a displayed result.
- A rain-shadow claim without required causal support is not presented as established.
- Biome suitability never overwrites creator-confirmed ecosystem facts.
- A global planetary parameter change visibly indicates broad recalculation impact.
- A local terrain edit invalidates only the dependent regional outputs permitted by the model.
- Seasonal outputs distinguish hemisphere phase mapping correctly.

## 9. Scenario F — Local refinement and global impact

**Creator intent:** increase detail around one valley without changing the surrounding mountain system.

### Flow

1. Creator changes Working Scope to the valley's locality.
2. The editor displays parent/neighbor context sufficient to understand boundaries.
3. Creator refines local elevation/terrain.
4. Before formal application, the system summarizes the spatial impact: edited area, neighboring context and affected derived products.
5. The system protects explicitly confirmed surrounding geometry.
6. After commit, only declared dependent coverage is invalidated and recalculated.
7. The UI shows local progress and identifies any broader downstream hydrology/climate propagation.
8. Unaffected results remain current and visible.

### UX requirements

- Working Scope is not mistaken for Map Extent.
- Parent/neighbor context is visible without making the whole world editable.
- The impact summary distinguishes direct edit footprint from derived influence area.
- The UI does not promise that all hydrology/climate effects are local if the dependency model says otherwise.
- A protected object is not silently changed by a local refinement.

### Acceptance

- A local edit does not alter unrelated formal geometry.
- Boundary context prevents artificial seams.
- Direct and propagated impact areas are distinguishable.
- Only declared dependent outputs are marked stale.
- A change that legitimately propagates through a drainage network or regional climate model shows that broader impact.

## 10. Scenario G — Calculation failure, cancellation and stale results

**Creator intent:** continue working safely when analysis is slow, interrupted or no longer current.

### Flow

1. A calculation starts; the user sees its scope, phase and current status.
2. The user can cancel a cancellable run.
3. If source inputs change while the run is executing, the result is marked as based on an older input snapshot and cannot overwrite a newer publication.
4. If a run fails, the Inspector explains the cause and suggests a retry or source correction.
5. The last valid artifact remains available, with an “outdated” or “previous result” label where appropriate.
6. After application restart, interrupted runs are reconciled. Valid active artifacts are restored; incomplete temporary output is not treated as success.
7. The creator can continue manual editing while unrelated background work is running.

### UX requirements

- Progress is meaningful, not falsely precise.
- Cancel is available only where the current phase can safely stop; the UI communicates when cancellation is completing.
- A failed refresh never blanks the old result.
- Stale data is never silently labelled “up to date”.
- Technical run IDs, hashes and stack details are hidden by default but available for diagnostics/export.
- Retry does not duplicate formal Objects or Commands.

### Acceptance

- Cancelled work publishes no partial active artifact.
- Older runs cannot replace newer results.
- Failure preserves the previous valid result.
- Restart recovery never fabricates a completed run.
- Manual editing and inspection remain responsive.
- A retry is idempotent with respect to formal map data.

## 11. Scenario H — Review AI proposals and adopt formal changes

**Creator intent:** accept the proposed river course but reject a proposed lake shoreline.

### Flow

1. AI proposes a set of map changes with provenance, scope and validation report.
2. The review view groups changes by semantic object/operation and spatial impact, not by raw database records.
3. Creator can inspect a candidate on the Canvas, compare before/after, accept/reject independent safe items or request revision.
4. The UI identifies dependencies between proposed items; partial acceptance is offered only where the remaining subset is valid.
5. Before formal application, the system rechecks target context, proposal version/hash and authorization.
6. Formal changes are submitted through existing Core Domain services.
7. The UI distinguishes accepted decision, submitted operation and successfully applied formal state.
8. Derived artifacts associated with the proposal remain separate and retain their provenance.

### UX requirements

- Candidate acceptance is not the same as formal application.
- Confirmation binds to the exact proposal version and target context.
- A changed proposal invalidates prior approval for the changed content.
- Rejection does not delete source facts or unrelated candidate items.
- Partial acceptance must not leave dangling or invalid relationships.
- Task cancellation does not undo already committed formal changes.

### Acceptance

- Before/after comparison identifies the exact affected Objects/geometries/relationships.
- User decisions are version-bound.
- A stale proposal is blocked or revalidated before application.
- Partial acceptance cannot violate Core Domain descriptors.
- UI reports formal application success only after the domain service confirms it.
- Derived-only result publication does not create a fake approval workflow.

## 12. Inspector contract

The Inspector is contextual and should avoid becoming a generic property grid.

### 12.1 Selected formal Object

Show:
- display name and Object Type;
- identity/status in creator-friendly terms;
- relevant typed attributes;
- geometry components and roles;
- supported relationships;
- provenance/source and formal/derived/candidate distinction;
- validation findings and applicable actions.

### 12.2 Selected physical field

Show:
- field meaning and units;
- authored/imported/derived source;
- coverage and resolution in understandable terms;
- no-data/unknown regions;
- source revision and freshness;
- dependent analyses and last update state;
- inspect/sample tool where appropriate.

### 12.3 Selected derived result

Show:
- what the result means;
- input sources and key assumptions;
- calculation method/version in advanced details;
- coverage/resolution/uncertainty;
- current/stale/partial state;
- diagnostics and explanation;
- actions such as recalculate, compare, inspect source or hide layer.

### 12.4 Selected Candidate

Show:
- proposed semantic change;
- creator intent it addresses;
- evidence and provenance;
- before/after preview;
- validation and impact;
- dependencies and partial-acceptance constraints;
- accept/reject/request-revision actions when authorized.

## 13. Diagnostics and impact visualization

Diagnostics are grouped by what the creator can do, not only by algorithm category.

Recommended groups:
- **Needs attention:** blocks a requested action or makes the result unsafe.
- **Uncertain result:** usable with disclosed limitations.
- **Suggested improvement:** optional refinement.
- **Protected fact conflict:** a derived suggestion conflicts with a creator-confirmed fact.
- **Outdated analysis:** source changed and dependent result needs refresh.

Each diagnostic includes:
- concise creator-language summary;
- affected region/feature;
- cause and relevant input references;
- severity and whether it blocks publication/application;
- suggested next action;
- advanced technical detail where useful.

Canvas navigation from a diagnostic must focus the affected region and preserve the prior selection/context for return. Impact overlays distinguish the directly edited area from the propagated calculation influence area.

## 14. Confirmation, permissions and safety

Before any formal write, the review/Inspector must identify which of the three distinct authoring targets is being changed:

1. **Map Initial State baseline:** changes the time-zero starting facts through the Initial State authoring service; it is not a post-zero Command.
2. **Map/Branch historical state:** changes one selected History after time zero through a registered typed Core Domain Command; Branch fork-boundary and replay rules apply.
3. **Project-shared Object definition:** changes the single current Shared Object definition through its definition service; it intentionally affects every Map/Branch reconstruction that references it and requires the corresponding Checkpoint invalidation/reconciliation behavior.

The UI must not collapse these targets into a generic “Save to map” action. The target, temporal effect, cross-map impact and confirmation scope must be visible before application. If the selected Object/field has no supported authoring target or registered operation, the action is unavailable; the editor must not fall back to generic CRUD or silently choose another target.

- View changes, layer visibility, selection and derived-only recalculation do not require formal-world confirmation.
- Transient previews and cancelled gestures never write formal state.
- Low-risk formal operations may follow the configured authorization policy; high-impact semantic changes, protected facts, broad scope changes and uncertain partial adoption require contextual review.
- User confirmation is tied to exact operation/proposal content and target Map/Branch/time context.
- Permissions are checked at action time, not only when the panel was rendered.
- The editor never lets an AI-generated result bypass Core Domain validation.
- A task cancel does not reverse a formal operation already committed.
- The UI must never describe an AI proposal as a confirmed world fact.

## 15. Accessibility and interaction requirements

- All primary tools and review actions are keyboard reachable.
- Focus order follows Canvas/tool/Inspector/conversation and remains predictable.
- Selected features and diagnostic targets have non-color-only indicators.
- Derived/formal/candidate states use text/icon/line-style distinctions in addition to color.
- Layer visibility, progress and validation states are announced accessibly.
- Pointer hit targets and snapping remain usable at supported DPI/scaling levels.
- Keyboard cancellation and Escape behavior are consistent across tools.
- Undo/Redo scope is clear: local preview cancellation versus formal history operation.
- Tooltips and labels use creator language; technical terms have concise explanations.

## 16. Cross-mode consistency

Natural Geography must reuse the shared editor interaction framework:
- same navigation, Working Scope, selection and contextual Inspector framework;
- same transient Edit Session and preview/cancel semantics;
- same AI conversation and Candidate review model;
- same formal Core Domain application path;
- same timeline/Branch context;
- same layer/view separation and scene refresh rules.

Mode-specific differences belong in tool definitions, contextual inspector sections, domain diagnostics and layer presets. Do not implement a separate Natural Geography selection, history or calculation task system.

## 17. End-to-end acceptance matrix

| ID | Scenario | Must-pass outcome |
|---|---|---|
| G-01 | Story-led initialization | Adaptive conversation creates a reviewable proposal; no unrequested formal Objects |
| G-02 | Draw land/water/elevation | Preview cancellation is safe; no-data differs from zero; local boundary context is preserved |
| G-03 | Terrain features | Stable Object identity; candidates are not formal facts; relationships are explicit |
| G-04 | Drainage analysis | Terrain-first results; closed basins preserved; formal network limits respected |
| G-05 | Climate/ecology | Causal explanation, seasonality and suitability/fact distinction are visible |
| G-06 | Local refinement | Bounded invalidation with honest propagated impact |
| G-07 | Failure/cancellation/stale | Old valid result remains; obsolete run cannot publish; restart recovery is safe |
| G-08 | AI proposal review | Version-bound decisions, valid partial acceptance and explicit formal-application result |
| G-09 | Inspector | Correct context-specific details and provenance for Object/field/result/Candidate |
| G-10 | Diagnostics | Actionable creator-language explanation and spatial navigation |
| G-11 | Offline operation | Manual authoring, local calculations and inspection work without AI/network |
| G-12 | Accessibility | Keyboard, DPI, focus and non-color state communication pass |

## 18. Figma handoff guidance

The Figma design should produce a coherent Natural Geography mode within the shared editor shell, not a separate application.

Required screen/state designs:
1. Natural Geography default Canvas with layer navigator, contextual Inspector and bottom AI conversation.
2. Guided Initialization conversation with map proposal preview and confirmed/assumed/unresolved facts.
3. Land/water and elevation brush with transient preview and impact summary.
4. Terrain Object selection/creation with geometry roles and relationship inspector.
5. Drainage analysis with flow direction, basin/channel candidates and formal River distinction.
6. Climate seasonal view with causal explanation and biome suitability versus creator-confirmed ecology.
7. Calculation progress, stale result, failed refresh and preserved prior artifact states.
8. Diagnostics list with Canvas focus/impact overlay.
9. AI Candidate review with before/after, partial acceptance and authorization/apply states.
10. Field/Derived Artifact provenance and advanced technical details in progressive disclosure.

Visual direction:
- professional, restrained desktop authoring tool;
- compact but readable information density;
- Canvas remains visually dominant;
- creator-facing labels by default, technical details on demand;
- distinguish authored/formal/derived/candidate/presentation using consistent iconography, labels and visual treatments;
- avoid dashboard KPI cards, giant progress cards, GIS jargon-heavy forms and modal-heavy workflows;
- use inline contextual feedback for routine actions and focused dialogs only for meaningful decisions.

The Figma deliverable should include normal, empty, loading, partial, stale, warning, error, cancelled and recovery states where relevant. A single happy-path screen is insufficient.

## 19. Open usability and implementation gates

| Gate | Owner | Evidence required |
|---|---|---|
| ME02-G-UX01: information density and panel sizing | Figma / product UX | Desktop prototypes at supported window sizes |
| ME02-G-UX02: creator-language vocabulary | Product UX + domain specialists | Comprehension review without GIS knowledge |
| ME02-G-UX03: layer grouping and state distinctions | Editor UX | Formal/derived/candidate/presentation usability test |
| ME02-G-UX04: progress/stale/failure presentation | Editor + Map Engine | Simulated long-run and failure-state prototype |
| ME02-G-UX05: Candidate review and partial acceptance | Editor + AI Assistant | Complex multi-item review scenarios |
| ME02-G-UX06: diagnostic navigation and impact visualization | Editor + spatial services | Cross-scope and propagated-impact scenarios |
| ME02-G-UX07: accessibility and DPI behavior | Desktop UX/implementation | Keyboard, screen-reader labels and scaling tests |

These gates do not block the architecture baseline. They require prototype and implementation validation before declaring the UX implementation-ready.

## 20. Source alignment and status

This document is aligned with:
- ME-01-01 through ME-01-07 — unified editor shell, story/spatial bridge, Guided Initialization, multi-scale model, Object/Geometry separation, shared interaction and cross-architecture findings.
- ME-02 V0.6 and ME-02-A through ME-02-F — domain inventory, authoring, hydrology, climate/ecology and calculation lifecycle.
- Map Generation Specification/Workflow/Session Lifecycle V0.2 — task, Candidate, Change Set, authorization and formal application boundaries.
- Map Engine rendering and spatial interaction contracts — immutable scenes, snapshot consistency, transient preview and cache invalidation.

ME-02-G V0.2 establishes the Natural Geography UX working baseline and end-to-end acceptance scenarios, including explicit UI separation of Initial State, History-local and Project-shared formal authoring targets. It does not claim that Figma screens, usability tests, Avalonia controls or implementation tests have been completed.

**Package status:** ME-02-H cross-architecture consistency review is complete in ME-02-H V0.1. The UX baseline is ready for Figma prototyping; final visual design, usability validation and Avalonia implementation remain separate work.
