# World View — ME-01-06 Editor Interaction Framework

- Version: 0.1
- Date: 2026-11-21
- Status: **Editor Interaction Working Baseline** — product and interaction architecture recommendation; concrete Avalonia controls, input bindings, API types and renderer contracts remain open
- Scope: Canvas-centered desktop interaction across navigation, selection, tools, editing sessions, AI-assisted review, diagnostics, history and accessibility
- Product target: Single-user, local-first Windows/macOS desktop application; C# + Avalonia
- Depends on: ME-01-01 through ME-01-05; Core Domain P2-09 persistence and Command/history contracts; AI Assistant product/tool/authorization contracts; Map Generation Specification, Workflow and Session Lifecycle V0.2; Map Engine geometry and rendering contracts
- Supersedes: None

## 1. Executive recommendation

Adopt a **Canvas-centered interaction model with one shared interaction state per active editing context**. All professional editing modes use the same navigation, selection, gesture, transient-edit, command-routing and history interaction foundation. A mode contributes domain-specific tools, defaults, inspector sections and validation feedback; it does not create a second selection model, geometry authority, edit session or history system.

The primary desktop composition is:

1. **Main Canvas** — dominant spatial work surface, with direct manipulation and contextual overlays.
2. **Contextual Inspector / Context Panel** — selected Object or geometry details, current tool options, diagnostics, AI explanations and relevant actions.
3. **AI Conversation** — persistent conversational entry point, preferably docked along the bottom edge of the Canvas/work area; it can expand for longer tasks without displacing the Canvas as the main work surface.

A compact tool shelf and layer/content navigator support the Canvas. Timeline/Branch context and status/diagnostics remain visible but should not compete with the spatial work surface.

The interaction architecture separates three state domains:

- **Editor interaction state:** viewport, selection, active mode/tool, hover, gesture, transient edit session and local preview. It is short-lived UI/application state.
- **AI Candidate staging state:** proposed changes, validation, provenance, uncertainty, review decisions and authorization evidence. It belongs to the AI Assistant task/proposal lifecycle.
- **Formal Core Domain state:** authoritative Map/Object/geometry/relationship/history data, changed only through Core Domain Domain Services and the applicable formal operation contract.

These states may reference one another, but they are not interchangeable and must not be persisted or undone as if they were one state machine.

The editor should make routine work direct and low-friction. It must not force every action through AI, a Candidate, a modal wizard or an approval dialog. Conversely, a preview, a chat response or an AI task completion is never proof that formal map data has changed.

## 2. Goals and non-goals

### 2.1 Goals

- Make the map Canvas the primary place to navigate, inspect, select, create and edit spatial content.
- Support World, Region, Locality and Site working scales without splitting one Map Space into unrelated maps.
- Keep Map Extent, Working Scope, Canvas View and Export Area distinct in both behavior and UI.
- Provide consistent Object and geometry-component selection across professional modes.
- Support common direct-manipulation tools: point/line/area creation, vertex editing, transform/move, geometry split/merge and object-level operations where domain-supported.
- Keep gesture previews transient until a valid operation is committed through the application/domain boundary.
- Make AI-generated changes understandable, spatially inspectable, selectively acceptable/rejectable and recoverable.
- Show validation issues and spatial impact in a way that helps creators locate and understand them.
- Preserve Core Domain authority for identity, formal geometry, Commands, Branches and Historical Time.
- Work without a configured LLM; AI is an optional capability layered over ordinary manual editing.
- Keep the interaction model small enough for a single-user desktop product and a small implementation team.

### 2.2 Non-goals for V1

- A general-purpose GIS command suite or CAD/BIM-grade constraint editor.
- A programmable plugin architecture for arbitrary third-party tools.
- A universal gesture/macro recording engine.
- Complex multi-user selection, collaborative cursors, locking or merge resolution.
- A full node-based visual workflow editor.
- An approval dialog for every low-risk edit.
- Treating every manual gesture as an AI Task or Candidate.
- Treating every AI response as a formal change proposal.
- Exposing coordinate systems, projections, topology internals or specialist GIS parameters as prerequisites for ordinary creators.
- A single giant tool state machine shared with unrelated application workflows.

## 3. Interaction principles

### 3.1 Canvas first, context nearby

The Canvas is the primary spatial reference. Tool settings and selected-object details belong in nearby contextual panels, not in a sequence of blocking dialogs. The UI should keep the user's spatial context visible while explaining a change.

### 3.2 One shared interaction foundation

Selection, viewport navigation, snapping feedback, gesture lifecycle, transient previews and command routing are shared capabilities. Specialist modes register supported operations and presentation behavior against this foundation; they do not own parallel interaction state.

### 3.3 Explicit scope

Every operation resolves against an explicit active Map, Branch, Historical Time and Working Scope. The visible Canvas viewport is not implicitly the full operation scope. If a task targets a region beyond the current viewport, the editor must not silently clip it to what happens to be visible.

### 3.4 Preview is not persistence

Pointer movement, hover, snapping suggestions, rubber-band selection, vertex drag previews and AI Candidate overlays are transient or staged representations. They do not mutate formal geometry or history.

### 3.5 Typed intent, not raw gestures

A gesture expresses user intent. The application translates it into a typed operation such as CreateObject, UpdateGeometry, MoveGeometry, SplitGeometry, MergeGeometry, UpdatePresentation or CreateRelationship. A generic “draw feature” operation must not blur semantic object creation, geometry editing, relationship creation and presentation changes.

### 3.6 Contextual confirmation

Confirmation is based on risk, impact, ambiguity and authorization—not on whether an operation came from a human or AI. Clear low-risk actions can be applied directly. High-impact, destructive, ambiguous or baseline-changing actions require explicit review or confirmation according to the relevant domain contract.

### 3.7 Reversible locally, authoritative formally

An unfinished gesture can be cancelled. An uncommitted edit session can be reverted. A committed formal change is not erased by cancelling a tool or AI Task; it must be reversed through a valid formal operation or supported history mechanism.

### 3.8 Explain complexity in creator language

The product should translate technical diagnostics into understandable language. Coordinate values, geometric tolerances, CRS details and topology terms may be available in advanced diagnostics, but must not be required for ordinary world creation.

## 4. Interaction architecture

### 4.1 Logical flow

The recommended interaction flow is:

1. **Input Adapter** receives mouse, keyboard, touch/pen and accessibility actions.
2. **Interaction Router** determines whether the input belongs to Canvas navigation, the active tool, a panel or a global command.
3. **Tool Controller** applies the active tool's gesture rules and state transitions.
4. **Selection / Edit Session** holds transient targets, captured base context, preview changes and local undo checkpoints.
5. **Typed Intent Builder** converts the completed gesture or explicit action into a domain-oriented request.
6. **Application Services** resolve scope, call Map Engine geometry/spatial services as needed, and validate the operation contract.
7. **Core Domain Domain Services** validate formal invariants, authorization and current context, then persist the applicable formal operation.
8. **Projection / Map Engine refresh** updates authoritative read projections and rebuildable render/hit-test/analysis representations.
9. **Editor** reconciles selection and displays the resulting formal state or a clear failure/stale result.

AI-assisted changes use a parallel staging path: AI Assistant creates Candidate/proposal data; the editor visualizes and reviews it; accepted items are submitted through the existing authorization and Core Domain application path. AI must not bypass the same formal validation boundary used by manual editing.

### 4.2 Responsibility boundaries

| Component | Owns | Must not own |
|---|---|---|
| Input Adapter | Device-specific input normalization | Domain semantics |
| Interaction Router | Focus, routing and gesture capture | Formal map state |
| Tool Controller | Active tool behavior and transient gesture state | Object identity or persistence |
| Selection Service | Selected Object/component references and selection rules | A duplicate Object registry |
| Edit Session | Transient before/preview values, local operation stack and base-context reference | Formal Command history |
| Typed Intent Builder | Explicit operation type and parameters | Direct database writes |
| Application Services | Scope resolution, orchestration, conflict checks and service calls | A second formal map authority |
| Map Engine | Hit testing, snapping queries, geometry operations, rendering and spatial calculations | Formal Object identity/history |
| Core Domain | Formal identity, state, invariants, Commands and reconstruction | UI gesture state |
| AI Assistant | Task context, Candidate, validation evidence, authorization/submission workflow | Direct Core Domain writes |

### 4.3 Active editing context

At any moment, the editor has one explicit active context containing:

- Project and Map identity;
- selected Branch and Historical Time;
- active professional mode and presentation profile;
- Working Scope;
- current Canvas View / viewport;
- active layer/content visibility and editability filters;
- active tool and tool options;
- selection set;
- current editor interaction/edit session reference, if any.

The context is an application read/interaction model, not a replacement for Core Domain. A change to Map, Branch or Historical Time invalidates any edit session whose captured base context is no longer compatible.

### 4.4 Focus and input routing

Only one interaction target owns a given keyboard gesture at a time. Focus priority should be:

1. A focused text/input control receives text-editing keys.
2. An active modal or explicit confirmation surface receives its own keys.
3. An active Canvas tool receives tool-specific keys.
4. Global editor commands receive shortcuts when not consumed by the above.

The editor must visibly indicate Canvas focus and active tool. Escape cancels the most local transient operation first; it must not unexpectedly close the project or discard unrelated work.

## 5. Spatial navigation and scope

### 5.1 Four separate spatial concepts

The editor must expose and preserve the distinctions established by ME-01-04:

| Concept | Meaning | Typical interaction |
|---|---|---|
| Map Extent | The intended overall spatial coverage of the Map | Map setup, extent properties, fit-map |
| Working Scope | The area/context currently targeted for authoring, analysis or generation | Scope selector, region navigator, set scope from selection |
| Canvas View | What is currently visible on screen | Pan, zoom, rotate if supported, fit selection |
| Export Area | The area included in a map/export output | Export frame, print/layout settings |

Changing Canvas View must not silently change Working Scope, Map Extent or Export Area. Changing Working Scope must not crop or delete content outside it. Export Area is not an editing boundary.

### 5.2 Navigation gestures

V1 should provide predictable navigation:

- Pan: drag with a dedicated navigation gesture (recommended middle mouse button or Space + primary drag).
- Zoom: wheel/trackpad pinch, anchored around pointer position where feasible.
- Fit selection: frame the current selected Object(s).
- Fit Working Scope: frame the active authoring region.
- Fit Map: frame Map Extent.
- Back/forward view: return to recent Canvas View states.
- Optional rotate: defer unless the chosen renderer and product presentation require it; do not make rotation a prerequisite for core editing.

Navigation gestures must be available while a selection or drawing tool is active through a temporary navigation modifier. This avoids forcing tool switching for routine pan/zoom.

### 5.3 Multi-scale navigation

World / Region / Locality / Site are working scales, not four separate maps. The user should navigate continuously through zoom and scope transitions.

The UI may display a simple scale/context breadcrumb, for example World → Region → Locality → Site, but it must not imply that all spatial objects form a strict parent-child tree. Regions, watersheds, political areas and networks may overlap or cross scope boundaries.

When the user changes scale:

- preserve the same Map, Branch and Historical Time;
- update visible detail and LOD without creating new semantic Objects;
- preserve selection if the selected Objects remain resolvable;
- update available tool affordances based on supported detail and geometry;
- keep the active Working Scope unchanged unless the user explicitly chooses a new scope or invokes a clearly labelled “set scope to this area” action;
- show when an operation is broader than the visible Canvas.

### 5.4 Scope-setting interactions

Recommended V1 ways to set Working Scope:

- choose a known Region/Locality/Site from the semantic navigator;
- set scope from the current selection;
- draw a temporary scope boundary for a one-off task;
- choose the whole Map.

A temporary rectangular or polygonal scope boundary is a task/context selection, not automatically a formal geographic Object. If the user intends a permanent region, the editor must offer an explicit Create Region operation.

### 5.5 Coordinates and measurement

The editor may show a low-emphasis coordinate/status readout for advanced users, but ordinary interaction should use visual placement, snapping, named places and natural-language descriptions. Measurements must be labelled with their spatial reference and units when shown. If coordinate context or units are unavailable, the editor must not invent precise distance or area values.

## 6. Selection model

### 6.1 Selection targets

The shared selection system distinguishes:

- **Semantic Object selection** — selects the complete Object identity and its current reconstructed state.
- **Geometry component selection** — selects a specific geometry component/role belonging to an Object.
- **Vertex / control-point selection** — selects editable parts of a geometry during a geometry tool session.
- **Presentation item selection** — selects an editable label, annotation or style item only where it has a supported persistent owner.
- **Candidate item selection** — selects a staged AI proposal item, not a formal Object.
- **Reference / diagnostic selection** — focuses an evidence item or issue for inspection; it is not automatically editable.

Selection of a rendered symbol, label, raster pixel or hit-test primitive must resolve to an explicit source reference. Renderer nodes and visual glyphs are not semantic identity.

### 6.2 Selection operations

V1 supports:

- single click selection;
- modifier-assisted add/remove from selection;
- rectangular marquee selection;
- lasso selection where it is materially useful for irregular spatial groups;
- select all within current Working Scope and active filters;
- invert selection within the explicitly stated selection domain;
- clear selection;
- select related objects through a named relation or diagnostic result where supported.

The UI must communicate whether a multi-select action adds to, replaces or removes from the current selection. Selection filters should be simple and contextual: Object Type, visible/editable/reference/locked status, active layer and current mode.

### 6.3 Object versus component selection

A click on an Object body selects the Object by default. When a geometry-edit tool is active, the user can explicitly enter component/vertex editing. Avoid ambiguous click behavior that unexpectedly edits a vertex when the user intended to select an Object.

Where several Objects overlap, provide a compact hit stack or cycle-through affordance rather than selecting an arbitrary hidden item. Locked, reference and derived content may be selectable for inspection but not for direct editing.

### 6.4 Selection persistence and invalidation

Selection is editor state. It may survive mode changes and viewport navigation if references remain valid. It must be reconciled when:

- Map, Branch or Historical Time changes;
- an Object is deleted, retired or no longer present in the selected reconstructed state;
- a geometry component is replaced or no longer exists;
- a Candidate becomes stale or is applied/rejected;
- layer visibility/editability changes make a target unavailable for the active operation.

Do not silently retarget a stale selection to a different Object or geometry component.

### 6.5 Selection is not authorization

Being selected does not grant edit permission. Before creating a write intent, the application checks Object lifecycle, current state, layer interaction policy, Branch/time rules, user authorization and domain invariants.

## 7. Tool model and lifecycle

### 7.1 Small explicit tool model

V1 uses a finite set of built-in tools grouped into:

- Navigation;
- Selection;
- Create;
- Geometry Edit;
- Object Operation;
- Measurement / Inspection;
- AI Candidate Review.

Tools are not arbitrary plugins. Each tool declares its supported target kinds, gesture lifecycle, transient preview behavior, cancellation behavior, resulting typed intents and required validation.

### 7.2 Tool lifecycle

A typical tool lifecycle is:

1. **Inactive** — no gesture is captured.
2. **Ready** — tool is active and displays affordances.
3. **Gesture Started** — pointer/keyboard input is captured.
4. **Previewing** — transient geometry or selection preview is updated.
5. **Awaiting Completion** — tool awaits a finishing action such as click, double-click, Enter or pointer release.
6. **Intent Ready** — a typed operation is assembled for validation.
7. **Applying** — application/domain services validate and apply or reject.
8. **Completed** — editor reconciles authoritative state and returns to Ready or Selection.
9. **Cancelled / Failed / Stale** — transient state is cleared or retained safely for recovery, with a clear explanation.

Not every tool uses every state. The lifecycle is conceptual; it should not become a heavyweight generic workflow engine.

### 7.3 Recommended built-in tool set

| Tool | Primary behavior | Resulting intent family |
|---|---|---|
| Select | Select Object/component or Candidate | Selection only |
| Pan / Zoom | Navigate Canvas | View state only |
| Point Create | Place a supported point Object | Create Object + geometry |
| Line / Path Create | Draw line/curve geometry | Create Object or add geometry, explicitly chosen by tool context |
| Area Create | Draw polygon/area geometry | Create Object or add geometry, explicitly chosen by tool context |
| Vertex Edit | Move/add/remove supported vertices/control points | Update geometry |
| Move | Move selected geometry or Object placement according to semantics | Move geometry / update placement |
| Transform | Apply supported scale/rotate/transform | Update geometry |
| Split Geometry | Split one geometry component | Geometry operation |
| Merge Geometry | Merge compatible geometry components | Geometry operation |
| Measure | Inspect distance/area/angle where spatial context supports it | No formal change |
| Candidate Review | Inspect, compare, accept/reject staged items | AI proposal decision/submission flow |

Specialist modes may expose domain-specific tools such as river drawing, watershed delineation, road routing, floor-plan editing or room creation. Those tools reuse the shared lifecycle and must call the relevant deterministic Map Engine or Domain Service capability.

### 7.4 Semantic operation distinction

The tool must make clear whether the user is:

- creating a new semantic Object;
- adding/refining geometry on an existing Object;
- editing an existing geometry component;
- changing a formal semantic relationship;
- changing only presentation;
- creating a temporary measurement or scope;
- editing a staged Candidate.

Geometry split/merge is not Object split/merge. A semantic Object split/merge is a separate domain operation with explicit identity/lifecycle consequences and may require confirmation.

### 7.5 Snapping and inference

Snapping suggestions are transient interaction aids. V1 may support vertex, endpoint, edge and grid/guide snapping where meaningful. The UI should show the target being snapped to and allow temporary suppression or override.

Geometric coincidence or snapping does not automatically create a formal topology or semantic relationship. The user must invoke a supported relationship operation, or a deterministic rule explicitly defined by the domain may create it with visible feedback.

## 8. Transient edit sessions, preview and cancellation

### 8.1 Edit session purpose

An Edit Session groups one coherent uncommitted manual operation or short sequence of compatible operations. It captures:

- active Map/Branch/Historical Time and Working Scope;
- relevant base-state references/fingerprint;
- selected Object and geometry component references;
- original values needed for local rollback;
- transient preview values;
- local operation stack/checkpoints;
- validation and snapping feedback.

It is not a formal Command, Branch revision or AI Candidate. It is disposable editor/application state.

### 8.2 Preview behavior

During a gesture:

- render a preview overlay or temporary replacement representation;
- keep the last authoritative geometry available for comparison/recovery;
- avoid writing formal state on every pointer move;
- run cheap local checks continuously where useful;
- run complete domain validation before application;
- show invalid geometry or constraint violations before commit when deterministically detectable.

The preview must be visually distinct from formal content but should not be so visually noisy that it obscures the map.

### 8.3 Completion and commit

A gesture completion creates a typed intent. The application then:

1. verifies the captured Map/Branch/Time context is still current;
2. re-resolves the target Object/component;
3. verifies the target has not changed incompatibly;
4. validates geometry and domain invariants;
5. checks authorization and risk policy;
6. calls the appropriate Core Domain Domain Service;
7. reconciles the returned formal result;
8. clears or closes the Edit Session.

For a sequence of related edits, the application may group operations into a coherent user-visible action, but it must respect the formal Command contract and atomicity boundary. It must not claim atomicity if the underlying services cannot provide it.

### 8.4 Cancel behavior

- Escape cancels the current gesture first.
- A second Escape may cancel the enclosing uncommitted Edit Session after a clear indication if it contains meaningful work.
- Tool switching during a gesture asks whether to finish or discard only when the current gesture cannot be safely auto-cancelled.
- Closing the editor with an uncommitted session prompts to discard or return.
- Cancelling a preview never changes formal Core Domain state.
- Cancelling an AI Task stops unfinished work but does not undo any already committed formal map changes.

### 8.5 Stale edit sessions

If the active Map/Branch/Time, target Object state or required dependency changes after the session begins, the session becomes stale. The editor must not submit the old intent blindly. It should either rebase the preview when the application can prove that operation safe, or discard/restart the session with an explanation.

## 9. Undo, redo, save and formal history

### 9.1 Three different concepts

The UI must distinguish:

1. **Gesture cancellation** — abandons the current unfinished interaction.
2. **Editor Undo/Redo** — reverses/reapplies uncommitted operations inside the current transient edit session, or invokes a supported formal inverse operation where explicitly designed.
3. **Formal map history** — Core Domain Commands, Events, State Changes, Branches, reconstruction and Checkpoints.

These are not interchangeable.

### 9.2 V1 recommendation

- During an unfinished gesture, Escape cancels the gesture.
- Within an uncommitted Edit Session, provide local Undo/Redo for the session's transient operation stack.
- After a formal Command has been accepted by Core Domain, the editor must not pretend that removing it from a local stack restores formal state.
- A formal reversal must use the Core Domain-supported operation/history semantics. If no safe inverse is available, the UI should explain that the change is already applied and offer the supported alternative, not fabricate a local undo.
- Normal Command edits follow Core Domain rebuild/playback rules. The editor should display when reconstruction/playback is pending or failed and must not present an unreconstructed result as confirmed historical output.

### 9.3 Save semantics

The application should avoid a misleading generic “unsaved map document” concept if formal changes are already committed to the local database. The UI may still have unsaved editor preferences, layout, style configuration or an open transient session.

Use precise status language:

- “Editing preview” — transient state only;
- “Ready to apply” — validated intent, not yet formal;
- “Applied” — Core Domain confirmed the formal operation;
- “Rebuilding history” / “History needs attention” — formal change exists but derived reconstruction/playback is not yet current;
- “View settings changed” — presentation/editor configuration only.

### 9.4 Task cancellation

AI Task cancellation and editor Undo are separate. Task cancellation must never issue an implicit inverse Command for earlier applied work. If a task partially applied valid changes before cancellation, report exactly what was applied and what remains staged, failed or cancelled.

## 10. AI-assisted interaction and Candidate review

### 10.1 One conversational assistant

The user sees one built-in AI Assistant. Internal planning, tool calls and specialist processing remain implementation details unless a concise progress explanation helps the user. AI is not required for manual editing.

The AI conversation should understand current selection, active Map/Branch/Time, Working Scope, active mode and relevant visible context. Context is task-driven and minimized; the viewport alone must not silently constrain a request whose stated scope is broader.

### 10.2 When to use direct application versus Candidate staging

Follow the current AI Workflow risk-based policy:

- **Direct, low-risk operation:** clear intent, known target, supported operation, within existing authorization, easy to inspect/reverse and no major semantic ambiguity. Apply through Domain Services and report the result.
- **Candidate / Change Set:** complex generation, many objects, major geometry change, deletion, protected-object impact, formal world-setting change, uncertain interpretation, significant cross-boundary impact or any operation requiring explicit review/authorization.
- **Clarification first:** unresolved creator intent or conflicting constraints that cannot safely be decided within granted creative freedom.

Do not create a full Candidate/Change Set for every simple edit merely to satisfy a uniform workflow.

### 10.3 Candidate visualization

Candidate items must be visibly distinguishable from formal Objects and reference data. The review view should support:

- show/hide all Candidate overlays;
- show only selected Candidate groups/items;
- compare proposed and current geometry;
- inspect added, modified, removed and relationship changes;
- view uncertainty, provenance and assumptions;
- view validation status and unresolved conflicts;
- highlight affected existing Objects and spatial neighborhoods;
- isolate a Candidate item or group on the Canvas;
- jump from a Candidate list item to its spatial location.

Use a consistent visual grammar, for example: formal content uses normal profile styling; Candidate additions use a distinct outline/fill treatment; proposed removals use a restrained deletion marker; selected Candidate uses a stronger focus highlight; diagnostic overlays use separate issue symbols. Color must not be the only differentiator—use line style, pattern, icon and labels as redundant cues.

### 10.4 Accept, reject and partial decisions

The user can accept or reject an eligible Candidate item or group. Partial acceptance is allowed only when dependency and validation rules prove the selected subset is coherent.

The review panel should explain:

- what will be created, modified, removed or linked;
- why the change is proposed;
- which constraints it satisfies or violates;
- what existing objects/areas are affected;
- what remains uncertain;
- whether accepting one item requires another item;
- whether the current Candidate is stale.

A user decision applies to the exact Candidate version/hash and target context shown. If the proposal materially changes, prior acceptance/authorization cannot silently carry forward.

Rejecting a Candidate does not delete formal map content. It records a review outcome in the AI workflow and removes or hides the rejected staging overlay according to the task lifecycle.

### 10.5 Candidate acceptance is not direct persistence

The acceptance path is:

1. user reviews exact Candidate version and impact;
2. the required human decision/authorization is recorded by AI Assistant;
3. Tool Gateway rechecks authorization, Candidate integrity and target context;
4. Core Domain Domain Services revalidate current state, identity, geometry, relationships and history rules;
5. formal Commands/State Changes are persisted under the existing transaction/idempotency contract;
6. AI Assistant records immutable submission evidence and the exact outcome;
7. editor refreshes from Core Domain authoritative projections.

An accepted Candidate may still fail final application if the target context changed or a domain invariant no longer holds. The UI must report this as an application failure/stale conflict, not as a successful map update.

### 10.6 Guided Initialization

Guided Initialization is a first-class conversational workflow, not a static form or one-click generation action. The editor interaction should:

- keep the conversation visible while the Canvas remains available;
- let the AI ask adaptive questions based on current map and confirmed world knowledge;
- allow the user to say “not sure”, skip, add information, correct or overturn an earlier answer;
- show which statements are confirmed, inferred, proposed or unresolved;
- preview the resulting initialization plan spatially;
- allow review/edit of proposed objects, scope, relationships and assumptions;
- require the appropriate user confirmation before formal baseline application;
- never silently overwrite confirmed settings;
- allow the user to return to manual Canvas editing at any time.

The conversation is the primary interaction for eliciting intent; the Canvas is the spatial explanation and review surface. Do not replace the dialogue with a long questionnaire.

### 10.7 AI conversation and contextual panel layout

Recommended desktop arrangement:

- Canvas occupies the largest central area.
- A right-side contextual panel changes content based on selection, tool, Candidate review or diagnostic focus.
- AI conversation input is anchored along the bottom of the main work area, with a compact transcript and expandable conversation history.
- The AI panel may expand to show task steps, questions, results and recovery actions; it should not permanently consume the space needed for map inspection.
- When a user selects an Object, the conversation may use it as context, but selection does not imply permission to edit it.
- When a Candidate or issue is selected, the right panel shows its explanation, evidence, dependencies and actions.
- The user can pin or clear contextual references so a new conversation turn does not accidentally inherit irrelevant selection context.

Avoid presenting three competing sources of truth. The Canvas shows spatial facts and overlays; the Inspector shows formal selected-object properties; the AI panel explains intent, task progress and proposals. Any mismatch must be resolved against Core Domain authority.

## 11. Diagnostics, impact and conflict visualization

### 11.1 Diagnostic classes

The editor should distinguish:

- geometry validity issues;
- spatial/topological constraint issues;
- semantic relationship issues;
- cross-scale consistency issues;
- world-setting/Canon conflicts;
- source/provenance uncertainty;
- stale context or stale Candidate;
- reconstruction/Checkpoint/history issues;
- authorization or protected-scope violations.

Each issue has severity, source, affected references, explanation and a recommended next action. Severity labels should be restrained and consistent; do not turn every warning into a blocking error.

### 11.2 Spatial impact visualization

For a proposed or applied change, show the bounded affected area and affected Objects where the system has reliable dependency evidence. Distinguish:

- direct targets;
- dependent objects/derived results that may need recalculation;
- nearby context included for review;
- unaffected protected content.

Do not display an invented impact radius or claim that the whole world must be recalculated when the dependency model identifies a smaller scope. Conversely, do not imply that only the visible viewport is affected when shared boundaries, watersheds, routes or other cross-scope dependencies extend beyond it.

### 11.3 Issue navigation

Selecting an issue in the diagnostic list should:

- navigate the Canvas to the relevant location;
- highlight the affected Object/component/area;
- select the relevant semantic Object where one exists;
- focus the Inspector or AI explanation on the issue;
- preserve the user's previous Working Scope unless the user explicitly changes it.

If the issue has no reliable spatial location, show it in the relevant object/task context rather than fabricating a pin.

### 11.4 Conflict handling

A conflict should identify the conflicting rule or source, not merely display “failed”. Examples include a proposed river crossing protected terrain, a new settlement conflicting with confirmed world setting, or a Candidate based on stale geometry.

The user should be offered a meaningful path: revise the Candidate, change the task scope, resolve a creator decision, or abandon the proposal. The editor must not silently weaken hard constraints or overwrite protected content.

## 12. Permissions and confirmation

### 12.1 Permission checks

Before any formal write, the application rechecks:

- current Map/Branch/Time context;
- target Object lifecycle and editability;
- active layer lock/reference/derived policy;
- operation type and its domain-specific authorization;
- protected Objects and creator-confirmed settings;
- Candidate authorization where applicable;
- current base-state references and stale status.

Selection, visibility, mode activation and AI conversation do not grant write permission.

### 12.2 Confirmation policy

| Operation class | Recommended V1 behavior |
|---|---|
| Pan, zoom, selection, view/style-only change | No confirmation |
| Clear low-risk transient gesture | Cancel directly |
| Clear low-risk manual edit with valid target | Apply after domain validation; local preview as needed |
| Explicit create/update of a small number of Objects | Apply directly when intent and authorization are clear |
| Large batch generation or broad geometry change | Preview and review |
| Delete/retire formal Objects or remove important relationships | Explicit confirmation with impact summary |
| Change protected/confirmed world setting | Stop and request creator decision under the setting authority rules |
| Conflict with hard constraint or unresolved major ambiguity | Block dependent operation and ask |
| Candidate acceptance | Bind decision to exact version/hash/context; final Core Domain validation still required |
| Presentation-only profile/layer/label styling | No formal world-history confirmation unless the chosen setting is itself domain-authoritative |

Confirmation must describe the actual consequence, not a vague “Are you sure?” It must identify affected objects/areas and whether the operation is formal, staged or presentation-only.

## 13. Error handling and recovery

### 13.1 Error categories

The UI should separate:

- invalid gesture/input;
- unsupported operation for the selected Object Type or mode;
- geometry/semantic validation rejection;
- stale target or changed base context;
- permission/authorization denial;
- Candidate generation/validation failure;
- final submission failure;
- asset publication or file I/O failure;
- reconstruction/Checkpoint failure;
- model/provider/network unavailability.

### 13.2 Recovery behavior

- Preserve authoritative formal data on every failed preview or failed application.
- Retain a valid Candidate after a transient submission failure when safe, but mark it stale if its target context changed.
- Make retry idempotent; do not duplicate formal Objects or Commands after an uncertain response.
- If the final transaction outcome is unknown, reconcile using the exact operation/submission reference before offering retry.
- If immutable assets were published but no formal DB reference committed, treat them as unreferenced staging assets and let the established safe-cleanup process handle them.
- If history reconstruction fails after a formal Command was persisted, keep the formal Command and show the history/rebuild issue. Do not pretend the Command was rolled back.
- If the LLM or network is unavailable, keep manual editing, local save, import/export and existing map inspection operational.
- If a tool becomes invalid due to a context change, cancel its transient state and explain what must be restarted.

### 13.3 Partial completion

For multi-step AI tasks, report separately:

- completed and formally applied operations;
- completed but still staged Candidates;
- rejected or blocked items;
- failed steps;
- cancelled unfinished work;
- derived outputs awaiting rebuild.

Never summarize “task completed” when only generation or preview completed but formal application did not.

## 14. Presentation and interaction-state categories

The following table defines interaction-facing state categories and their authority:

| State category | Examples | Owner / authority | Persistence expectation |
|---|---|---|---|
| Formal domain state | Object identity, ObjectState, authoritative geometry, formal relationships, Commands | Core Domain | Persist under Core Domain contracts |
| AI task/proposal state | Spec, task progress, Candidate, validation, authorization, submission evidence | AI Assistant | Persist according to AI lifecycle contracts |
| Editor transient state | Hover, active gesture, temporary selection, preview geometry, local edit stack | Editor/Application | In-memory; discardable |
| Editor preference state | Panel layout, recent view, shortcut preferences | Application/UI settings | Local preference persistence |
| Presentation configuration | Profile, style, label placement, layer visibility/order | Existing presentation/configuration owner; not automatically Core Domain history | Persist only under the appropriate view/configuration contract |
| Derived spatial/render state | Hit-test index, projected geometry, LOD, tiles, labels, diagnostic overlays | Map Engine / rebuildable caches | Rebuildable; never formal authority |

The exact physical persistence owner for layer/style configuration remains open where ME-01-05 says it is not yet frozen. This document does not assign such data to Core Domain by implication.

## 15. Shared behavior across professional modes

All modes reuse:

- Canvas navigation and scope handling;
- selection and hit-testing semantics;
- input routing and focus;
- transient Edit Session lifecycle;
- typed intent and command routing;
- snapping feedback and diagnostics;
- Candidate overlays and review;
- formal history and stale-context handling.

Modes may specialize:

- available create/edit tools;
- supported geometry roles;
- Object Type-specific Inspector sections;
- tool defaults and snapping priorities;
- validation rules and diagnostic explanations;
- visual emphasis and layer presets.

A selected Object remains inspectable when switching to a mode that cannot edit it. The editor may explain that a specialist tool is available elsewhere, but it must not hide the Object or create a duplicate representation. Mode switching must not discard a valid transient session without a clear cancellation/recovery decision.

## 16. Keyboard, pointer, touch and accessibility

### 16.1 V1 input support

Prioritize mouse and keyboard on desktop. Trackpad pan/zoom should be supported where Avalonia/platform input permits. Pen/touch support may be added for direct drawing and navigation but must not be required for core workflows.

### 16.2 Shortcut principles

- Use a small, consistent shortcut set for selection, navigation, cancel, delete/backspace where safe, undo/redo, fit selection/map and tool activation.
- Avoid shortcuts that conflict with text entry or platform conventions.
- Show the active tool's available shortcuts in contextual help.
- Allow shortcut customization only if it can be implemented simply and reliably; a full macro system is out of scope.
- Destructive actions must not depend on an easy-to-trigger single key without an appropriate context/confirmation rule.

Exact key assignments are an implementation/UI design decision and are not frozen here.

### 16.3 Accessibility basics

- Every tool and important Canvas action must have a keyboard-accessible alternative.
- Selected Object, active tool, Candidate state and diagnostic severity must be conveyed through more than color alone.
- Panels and controls need meaningful accessible names and logical focus order.
- Focus must remain visible; keyboard users must be able to move between Canvas, Inspector, AI conversation and diagnostics.
- Provide text descriptions for important spatial overlays and issue lists.
- Avoid requiring precise pointer gestures for critical actions; offer numeric or stepwise alternatives where appropriate.
- Respect system text scaling and high-contrast settings to the extent supported by the chosen UI/rendering stack.

## 17. Implementation sequence

Recommended order:

1. **Active context contract:** Map/Branch/Time, Working Scope, Canvas View, mode and presentation context.
2. **Canvas navigation:** pan, zoom, fit selection/scope/map, recent view and scope breadcrumb.
3. **Shared hit-testing and selection:** Object/component selection, multi-select, marquee, filters and selection invalidation.
4. **Input routing and tool lifecycle:** focus, active tool, gesture capture, Escape and temporary navigation modifier.
5. **Transient Edit Session:** preview, local operation stack, cancellation and stale-context detection.
6. **Typed intent/application boundary:** create/update geometry, presentation-only changes and explicit Object-vs-geometry operations.
7. **Core Domain integration:** validation, authorization, formal application, authoritative refresh and history status.
8. **Candidate review foundation:** staged overlays, item selection, diff, accept/reject and partial-dependency handling.
9. **AI conversation integration:** contextual references, Guided Initialization, task progress and recovery.
10. **Diagnostics and impact view:** issue list, spatial focus, affected scope and conflict explanation.
11. **Specialist-mode tools:** add domain-specific behavior without duplicating shared interaction state.
12. **Accessibility and platform polish:** keyboard operation, focus, trackpad and optional pen/touch support.

Do not start by building a generic tool plugin framework. Implement the small shared contracts needed by the first concrete tools and extend only when real specialist workflows require it.

## 18. Acceptance criteria

1. The Canvas remains the primary spatial work surface in every professional mode.
2. Map Extent, Working Scope, Canvas View and Export Area are separate concepts and actions.
3. Zooming/panning does not silently change Working Scope or Map Extent.
4. World/Region/Locality/Site navigation preserves one Map Space and does not imply a strict tree for all spatial relationships.
5. Object and geometry-component selection are distinct and predictable.
6. Single, additive, subtractive, rectangular and lasso selection work within explicit filters/scope.
7. A hit-test/render item resolves to its formal source reference; no renderer primitive becomes a semantic Object.
8. Mode switching does not duplicate selection/edit state or lose a still-valid selected Object.
9. Every tool has a clear active state, gesture lifecycle, completion and cancellation behavior.
10. Pointer movement and preview do not write formal geometry or history.
11. Geometry split/merge is distinct from semantic Object split/merge.
12. Snapping and geometric coincidence do not silently create formal relationships.
13. A completed manual gesture becomes a typed intent and passes application/domain validation before formal application.
14. Stale edit sessions cannot overwrite changed targets or historical contexts.
15. Escape cancels the most local transient action first and does not erase formal map state.
16. Editor Undo/Redo does not masquerade as formal Core Domain history reversal.
17. AI Candidate overlays are visually distinct from formal Objects, references and diagnostics.
18. Candidate review supports inspect, compare, accept, reject and safe partial decisions where dependencies permit.
19. Acceptance binds to an exact Candidate version/hash and target context; final Core Domain validation remains mandatory.
20. Guided Initialization remains a dynamic conversation with spatial preview, not a static questionnaire or one-click generation.
21. AI task cancellation does not undo already applied formal changes.
22. Diagnostic selection navigates to a reliable spatial target without fabricating locations.
23. Impact visualization distinguishes direct targets, dependent outputs and unaffected/protected content.
24. Permissions are checked at formal application time; selection or AI conversation does not grant authority.
25. LLM/provider failure does not disable manual editing or local map operations.
26. Partial task completion clearly distinguishes applied, staged, failed, blocked and cancelled work.
27. Presentation-only changes do not create formal historical world changes by default.
28. Keyboard users can access essential navigation, selection, tool, cancellation and review actions.
29. The implementation does not introduce a generic plugin/macro framework or parallel interaction state per mode.

## 19. Open decisions and implementation gates

This working baseline intentionally does not freeze:

- exact Avalonia control composition, docking implementation and responsive panel behavior;
- exact shortcut map and platform-specific pointer gestures;
- whether Canvas rotation is included in V1;
- exact lasso algorithm and selection tolerance;
- supported geometry edit operations by Object Type;
- exact snapping candidate priorities and tolerances;
- exact local Edit Session data structure and whether a short sequence maps to one or multiple formal Commands;
- formal inverse/reversal support for each Command descriptor;
- exact Candidate diff visualization and partial-accept dependency contract;
- exact visual tokens for formal/Candidate/reference/diagnostic overlays;
- persistence ownership and schema for layer/style/view configuration;
- exact renderer-neutral hit-test and interaction query contract;
- performance thresholds for very large selection sets and complex geometry.

These require implementation-facing contracts and validation with the selected Map Engine and Core Domain services. No rendering engine, GIS library or geometry provider is selected by this document.

## 20. Relationship to the ME-01 package

- ME-01-01 defines the unified editor shell and subsystem boundaries.
- ME-01-02 defines story facts, creator intent, inference and Story-to-Spatial semantics.
- ME-01-03 defines Guided Initialization.
- ME-01-04 defines continuous Map Space, multi-scale scope and global consistency.
- ME-01-05 defines semantic Object, geometry and presentation separation.
- **ME-01-06 defines how users and the AI Assistant interact with those shared concepts.**
- ME-01-07 must verify this interaction framework against Core Domain, AI Assistant, Map Generation, Map Engine and persistence contracts, and record gaps rather than claiming interfaces are frozen.

ME-01-06 is an Editor Interaction Working Baseline. It is not a frozen Avalonia UI specification, tool API, database schema, geometry-engine contract or implementation authorization.
