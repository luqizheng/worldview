# World View — ME-06-B Input Routing & Gesture Lifecycle

- Version: 0.1
- Date: 2026-11-21
- Status: Input/gesture interaction working baseline; concrete Avalonia bindings and platform behavior require implementation proof
- Depends on: ME-06-00 V0.1; ME-06-A V0.1; ME-01-06/07; ME-05-02/03/04/06; Map Engine immutable scene/query contracts
- Authority: Shared Editor interaction contract only. It does not freeze Avalonia APIs, renderer input APIs, Core Domain operations, schemas or platform-specific shortcut bindings.

## 1. Executive disposition

ME-06-B defines one shared input-routing and gesture-lifecycle model for all specialist editing modes.

The implementation should use a **small priority router plus tool-owned gesture handlers and explicit transient sessions**. It must not become a general workflow engine, event-bus maze or independent state machine per specialist editor.

The central rule is:

> An input event is routed to exactly one owner; a gesture captures one immutable interaction context; all intermediate work remains transient; only an explicitly completed and preflighted Typed Intent may cross the application boundary.

V1 interaction is single-user and single-active-pointer for geometry authoring. Mouse, keyboard, pen and touch are normalized into a common logical input model, but the exact device-specific bindings remain subject to Avalonia/Windows/macOS proof. Multi-touch navigation may be supported by the input adapter; simultaneous multi-pointer geometry authoring is not required.

## 2. Goals and non-goals

### 2.1 Goals

- Deterministic routing between text controls, panels, modal surfaces, Canvas navigation, active tools and global commands.
- One active gesture owner at a time.
- Predictable temporary navigation while a specialist tool remains armed.
- Explicit pointer capture, completion, cancellation and focus-loss behavior.
- Immutable context capture at gesture start.
- Transient geometry/edit sessions with local undo, separate from formal history.
- Clear treatment of stale query results and context changes during a gesture.
- Keyboard and non-pointer paths for core operations.
- Shared behavior across ME-02, ME-03 and ME-04 without duplicating interaction runtimes.

### 2.2 Non-goals

- Arbitrary gesture recording, macros or scripting.
- Multi-user cursor arbitration, object locks or collaboration.
- Simultaneous independent geometry gestures from multiple pointers.
- A universal drag/drop framework for the entire application.
- Free-form user-configurable shortcut editor in V1.
- Platform-specific input behavior frozen before the Avalonia spike.
- Automatically completing or submitting partial geometry after capture loss.

## 3. Canonical input routing

### 3.1 Input classes

The Input Adapter normalizes platform events into a bounded set:

| Input class | Examples | Primary meaning |
|---|---|---|
| Pointer | Move, down, up, cancel, enter/leave, wheel | Canvas targeting and gesture progression |
| Keyboard | Key down/up, Escape, Enter, modifiers, navigation shortcuts | Tool actions, completion, cancellation and global commands |
| Text input | Text composition, committed text, paste | Focused text control only |
| Pen | Contact, hover, pressure where supported, barrel button, cancel | Pointer-like spatial input with device metadata |
| Touch | Contact, move, release, cancel, pinch/pan gesture | Pointer-like action or viewport navigation |
| Accessibility action | Activate, increment/decrement, move focus, invoke command | Semantic equivalent of supported tool/panel action |
| Window/focus lifecycle | Focus gained/lost, deactivation, capture lost, close | Safe suspension/cancellation and cleanup |

The adapter preserves device identity and normalized coordinates, but tool/domain code must not depend on raw OS event objects.

### 3.2 Routing priority

At each input dispatch, the router resolves one target using this priority:

1. **Text composition/input ownership.** A focused text field receives text editing, IME composition and paste. Canvas tools do not intercept these.
2. **Modal or blocking surface.** An active modal/confirmation surface receives its declared keys and pointer events. Background Canvas tools cannot act through it.
3. **Captured gesture owner.** If a gesture has valid pointer/keyboard capture, subsequent events for that gesture go to its owner until terminal completion/cancellation.
4. **Focused panel/control.** The focused Inspector, navigator, list or tool option control receives its declared interaction.
5. **Temporary navigation override.** A declared navigation modifier or navigation device gesture routes to Canvas navigation without changing the armed specialist tool.
6. **Active Canvas tool.** The armed tool receives eligible Canvas input.
7. **Canvas selection/navigation fallback.** Unclaimed Canvas input uses the shared default behavior.
8. **Global editor command.** Global shortcuts are considered only when no higher-priority owner consumes the input.

A routing decision is final for one event. The same pointer-down must not both select an Object and begin a drawing gesture. A tool cannot observe text composition or steal input from a modal.

### 3.3 Focus and ownership indicators

The UI must make these states visible:
- which control currently has keyboard focus;
- which Canvas tool is armed;
- whether a gesture is actively captured;
- whether the current gesture is paused, stale or cancelled;
- whether a modal/confirmation surface blocks Canvas input.

Focus changes do not silently transfer an in-progress gesture to another control.

## 4. Tool arming and context capture

### 4.1 Arming

Arming a tool changes the active tool in the shared Interaction Runtime. It does not begin a gesture and does not mutate Map data.

A tool may declare:
- required active mode;
- required target selection or context;
- allowed input devices and gestures;
- temporary navigation modifiers;
- supported completion action;
- cancellation action;
- transient preview type;
- required query capabilities;
- whether a keyboard-only invocation path exists.

If a required capability is unavailable, the tool may remain inspectable but must expose Preview Only, Blocked or Unsupported status consistent with ME-06-A. It must not pretend the gesture can be formally applied.

### 4.2 Context snapshot

At the first accepted gesture input, capture a Gesture Context Snapshot containing the relevant subset of:
- Project, Map, Branch and Historical Time identities;
- Working Scope identity and revision;
- active specialist mode and tool key/version;
- selected Object/Component stable references;
- relevant layer/editability context;
- spatial-reference identity and transform version;
- precision profile;
- immutable scene generation;
- query request sequence baseline;
- current target and source revision fingerprints;
- gesture/session ID.

The snapshot is immutable for the gesture. Tool code may append derived evidence references, but it cannot silently replace the captured Map/Branch/Time or retarget the gesture.

### 4.3 Context changes

If Map, Branch, Historical Time, Working Scope, mode, active tool or required target revision changes incompatibly:
- mark the gesture stale or cancel it according to whether a safe transient continuation exists;
- discard or clearly preserve the draft as a detached, non-applicable recovery item;
- never silently retarget the operation;
- require a new gesture and new context snapshot before apply.

A Canvas pan/zoom does not by itself change Working Scope. It may change the device-to-map transform used for subsequent pointer positions; an in-progress geometry gesture must consistently account for the transform revision or be safely cancelled/rebased before continuing.

## 5. Gesture lifecycle

### 5.1 Shared state model

| State | Entry condition | Allowed behavior | Exit |
|---|---|---|---|
| Idle | No active gesture | Route ordinary input | Tool action starts |
| Armed | Built-in tool selected | Show affordance and options; no formal mutation | First accepted gesture input, tool switch or cancel |
| Capturing | Tool accepts initial input and captures ownership/context | Collect normalized input; maintain capture | Preview, completion, cancellation, capture loss |
| Previewing | Tool has enough input to render a transient result | Update draft/overlay and read-only query requests | Continue, complete, cancel, stale |
| AwaitingCompletion | Tool requires explicit finish or confirmation | Show current draft and valid completion action | Complete, cancel, stale |
| IntentReady | Tool emits a typed intent from the captured snapshot | No geometry mutation; prepare preflight | Preflight, cancel |
| Preflight | Application boundary checks capability, target, freshness, invariants and authorization | Read-only validation and evidence collection | Applying, blocked, stale, rejected |
| Applying | Supported application service has accepted the operation | Disable duplicate submit; await authoritative result | Reconciling, failed |
| Reconciling | Formal operation result is returned; read model/scene refresh pending | Resolve stable references and scene generation | Applied, failed, pending reconciliation |
| Terminal | Completed/applied, cancelled, failed, stale, blocked, unsupported or rejected | Show truthful outcome; release capture and transient resources | Return to Armed or Idle |

Not every tool uses every state. A click-to-select action may complete without entering Previewing. A polygon sketch normally enters Capturing and Previewing, then AwaitingCompletion.

### 5.2 State invariants

- Only one active gesture owner exists per active Canvas interaction context.
- Only the owner may mutate its transient draft.
- Pointer movement updates preview only; it does not issue formal Commands.
- A typed intent is emitted once for one completed gesture.
- A failed preflight cannot enter Applying.
- A terminal state always releases pointer/keyboard capture and disposes obsolete query subscriptions.
- Repeated completion input cannot duplicate an operation.
- A new gesture cannot begin while the prior gesture is Applying or awaiting authoritative reconciliation unless the tool explicitly declares a safe independent interaction.
- The formal operation result, not the gesture animation, determines success.

## 6. Gesture families and completion semantics

| Gesture family | Examples | Completion | Cancellation / local undo |
|---|---|---|---|
| Single activation | Select Object, pick endpoint, choose reference | Accepted pointer-up/click or semantic Activate | Escape clears current pending pick; selection follows selection rules |
| Point placement | Peak, entrance marker, junction proposal | Explicit click/tap accepted with valid target context | Escape discards point draft |
| Path construction | Road, River, ridge, wall boundary | Explicit finish action; double-click may be a candidate binding only after platform validation | Escape removes current draft; local undo removes last uncommitted vertex |
| Area construction | Landmass, PoliticalRegion, Settlement extent, room outline | Explicit finish action and geometry validation | Escape discards draft; local undo removes last uncommitted vertex |
| Vertex/component edit | Move vertex/control point, adjust component | Explicit commit action or accepted release according to tool risk policy | Escape restores uncommitted base snapshot; local undo restores prior draft step |
| Brush/field edit | Elevation or land-water brush | Stroke end creates a transient field-edit batch | Escape discards current uncommitted stroke/batch; no partial Field Revision publication |
| Pan/zoom | Navigate Canvas | Continuous view update | No formal edit; gesture ends on release |
| Marquee selection | Select multiple Objects/components | Pointer release | Escape clears marquee preview; selection changes only after resolved result |
| Inspector action | Change option, set classification where supported | Explicit control action | Control-local undo only before formal application |

Specific device bindings (e.g. double-click completion, right-click finish, Space+drag, middle-button drag) remain configurable implementation choices until validated on Windows/macOS. The semantic completion action must be stable even if the physical binding changes.

## 7. Pointer capture, focus loss and cancellation

### 7.1 Capture rules

- A gesture captures the initiating pointer/device when the platform supports it.
- Captured movement is routed to the gesture owner even when the pointer leaves the Canvas bounds.
- The router records the capture owner and initiating device identity.
- A second pointer cannot mutate the active geometry draft.
- A navigation override may temporarily route a distinct navigation gesture only where the platform adapter can preserve both gesture identities safely; otherwise it is deferred until the current gesture ends.
- Capture release is mandatory on every terminal path.

### 7.2 Capture loss and interruption

On pointer capture loss, window deactivation, application focus loss, device disconnect, modal takeover or unexpected tool disposal:
1. Stop accepting new geometry samples.
2. Cancel pending preview/query requests that are no longer useful.
3. Mark the gesture Interrupted.
4. If the draft is safely serializable as a detached local recovery item, preserve it as non-applicable draft state; otherwise discard it with a clear message.
5. Release all remaining capture/focus ownership.
6. Never auto-complete, auto-close a polygon or submit a partial field stroke.
7. Resume only through an explicit user action that revalidates the original context; otherwise start a new gesture.

Interruption is not a formal failure and is not a formal Map change.

### 7.3 Escape and cancellation ordering

Escape cancels the most local transient layer first:
1. active text composition or control-local editing (the control owns its standard behavior);
2. current gesture/draft;
3. current uncommitted Edit Session step;
4. active temporary navigation override;
5. armed tool (disarm);
6. non-destructive transient selection/overlay where appropriate.

Escape must never implicitly undo an already-applied Core Domain operation or close the project while a local gesture can still be cancelled.

## 8. Keyboard, pen, touch and accessibility parity

### 8.1 Keyboard

- Every essential tool must have a discoverable keyboard path or an accessible command surface.
- Completion, cancellation, vertex navigation and selection must not require pointer precision alone.
- Text inputs retain standard typing, IME and paste behavior.
- Shortcut defaults are proposed by the application but must be checked for Windows/macOS conflicts before freezing.
- Key repeat must not create duplicate semantic operations.
- Enter/Space activation from accessibility controls must invoke the same semantic action as pointer activation, not synthesize a fake pointer coordinate.

### 8.2 Pen and touch

- Pen contact maps to pointer-like gesture input; pressure/tilt are optional metadata and cannot change formal semantics unless a tool explicitly declares and validates them.
- Touch single-contact actions may map to pointer-like gestures where appropriate.
- Pinch/pan is navigation and must not accidentally create geometry points.
- Device cancellation and palm rejection must be handled by the Input Adapter where platform support exists.
- V1 does not require multi-touch geometry construction.

### 8.3 Accessibility

- Every armed tool has an accessible name, purpose, state and completion/cancel instructions.
- Focus order remains predictable between Canvas, tool shelf, Inspector and AI conversation.
- Selection, validity, blocked state and stale state cannot be communicated by color alone.
- Keyboard users can inspect the same target metadata and validation summary available to pointer users.
- A non-pointer path must exist for the primary completion and cancellation action of every supported tool.

## 9. Interaction with asynchronous spatial queries

- Queries are requested only after sufficient valid draft input exists.
- Each request is tagged with gesture/session ID, query sequence, captured context and relevant source fingerprints.
- A newer pointer sample supersedes older pending snap/hit-test/preview requests.
- Late results for a prior sequence, scene generation or gesture are discarded.
- Query responses may update preview guidance but may not change the user's captured input coordinates without an explicit snap acceptance action.
- Query cancellation does not cancel the gesture unless the tool requires that query to continue.
- Before IntentReady → Preflight, the tool captures the exact evidence IDs/fingerprints used to explain the proposed operation.
- Preflight re-resolves all formal targets and required source revisions. A query result is neither a lock nor authorization.

Detailed freshness tokens and invalidation rules are specified by ME-06-C; this document defines their consumption by the gesture lifecycle.

## 10. Application boundary and reconciliation

The gesture handler emits a Typed Intent only after semantic completion. The Application Services boundary then:
1. verifies the intent's captured Map/Branch/Time/Working Scope context;
2. resolves stable Object/Component references (never renderer node IDs);
3. verifies the capability gate from ME-06-A;
4. revalidates query evidence and source fingerprints;
5. routes to the correct formal write path or returns Preview Only / Blocked / Unsupported;
6. checks authorization and risk requirements;
7. invokes the supported Domain Service;
8. returns a typed outcome;
9. reconciles the authoritative read model/scene generation;
10. resolves or clears the Editor selection by stable identity.

The input/gesture subsystem never writes Core Domain tables, creates Object IDs, allocates relationship IDs or publishes Assets directly.

## 11. Outcome model and user feedback

| Outcome | Meaning | UI behavior |
|---|---|---|
| Completed locally | Transient/view-only interaction completed | Keep local result; do not imply formal persistence |
| Applied | Formal operation confirmed by authoritative service | Show applied state and reconciled scene |
| Pending reconciliation | Apply accepted but authoritative projection/scene not yet refreshed | Show pending state; prevent duplicate submit |
| Cancelled | User cancelled before formal apply | Clear/preserve draft per recovery rule |
| Interrupted | Capture/focus/device loss; no formal apply | Explain whether draft was recovered or discarded |
| Stale | Captured context or evidence no longer current | Keep diagnostic context; require refresh/new gesture |
| Blocked | Known domain capability gap | Explain missing formal capability; do not offer fake apply |
| Unsupported | Product does not support the requested gesture/operation | Offer supported alternative if one exists |
| Rejected | Validation/authorization rejected the intent | Show actionable reason; retain draft only when safe |
| Failed | Unexpected application/engine failure | Preserve safe local draft; report no success |
| Reconciled | Authoritative result and scene/read model are synchronized | Restore normal interaction |

A UI animation or disabled button is not an authoritative result. The application must not report Applied until the formal service confirms it.

## 12. Acceptance and conformance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| B-01 | Text field focused while Canvas tool is armed | Typing/IME/paste goes only to text control |
| B-02 | Modal confirmation open while drawing tool is armed | Canvas input cannot mutate the draft |
| B-03 | Start a path and move pointer outside Canvas | Captured gesture receives movement or safely interrupts; no accidental completion |
| B-04 | Pointer capture is lost mid-polygon | Draft is interrupted/recovered or discarded; never auto-submitted |
| B-05 | Switch Map/Branch/Time mid-gesture | Gesture becomes stale/cancelled; never silently retargets |
| B-06 | Pan/zoom while a tool is armed | Tool remains armed; Working Scope is unchanged |
| B-07 | A slow snap response arrives after a newer pointer sample | Older response is discarded |
| B-08 | Press Escape while an uncommitted geometry draft exists | Only local draft is cancelled first |
| B-09 | Press Escape after formal operation was applied | Formal operation remains; Escape cannot undo it |
| B-10 | Double-click completion event is delivered twice | One Typed Intent at most; no duplicate apply |
| B-11 | A blocked Road Junction gesture is completed | Preview/evidence may be shown; formal apply is blocked with ME-03-H reason |
| B-12 | User uses keyboard/accessibility action to complete a supported tool | Same semantic completion path as pointer input |
| B-13 | Two pointers contact Canvas during geometry authoring | Only captured pointer can edit; second cannot mutate draft |
| B-14 | App deactivates during Field brush stroke | Stroke is cancelled/interrupted; no partial Field Revision is published |
| B-15 | Scene refresh replaces selected renderer node | Stable Object/Component reference is re-resolved; renderer node identity is not persisted |

## 13. Implementation guidance and open proof

Recommended implementation decomposition:
- Avalonia Input Adapter: normalize device/window events and capture lifecycle.
- Shared Input Router: apply the priority table and maintain one gesture owner.
- Shared Gesture Session: immutable context snapshot, normalized samples, draft state and terminal outcome.
- Tool-specific Gesture Handler: domain-specific point/path/area/edit semantics.
- Transient Draft Store: in-memory Edit Session state only; detached recovery serialization is optional and non-authoritative.
- Query Coordinator: request sequence, cancellation and freshness envelope (detailed in ME-06-C).
- Typed Intent Builder: stable target references and captured evidence; no direct Domain writes.
- Application Operation Gateway: capability/preflight/authorization/routing/reconciliation.

Keep these as small collaborating services; do not introduce a generic workflow engine or dynamic event plugin system.

Platform proof required before freezing bindings:
- Windows: mouse, pen/touch where supported, keyboard focus, pointer capture loss, IME/text fields, high-DPI coordinate mapping.
- macOS: mouse/trackpad gestures, keyboard modifiers, focus/capture lifecycle, IME/text fields, Retina coordinate mapping.
- Avalonia/renderer integration: event ordering, capture semantics, device identity, viewport coordinate transforms and window deactivation.
- Verify the renderer and Input Adapter agree on logical coordinate spaces; device pixels must not leak into domain geometry.

## 14. Acceptance criteria

ME-06-B is complete as an interaction contract when:
1. Input routing priority has no ambiguous dual ownership.
2. Every ME-06-A tool family declares its gesture, completion, cancellation and target/context capture behavior.
3. Pointer capture loss, focus/window loss, context switch and duplicate completion have deterministic outcomes.
4. Transient draft, Typed Intent, formal application and authoritative reconciliation are separate.
5. Escape and local undo cannot reverse a confirmed formal operation.
6. Keyboard/accessibility paths exist for essential operations.
7. Async query results cannot roll back newer pointer state or cross gesture/context generations.
8. Windows/macOS proof requirements are explicit and no unverified binding is called frozen.

**Disposition:** ME-06-B is complete as an input-routing and gesture-lifecycle design working baseline. It does not mean Avalonia input integration or platform tests are implemented. Next: ME-06-C — Spatial Query Freshness, building the exact token, invalidation and apply-time re-resolution contract used by these gestures.

## 15. Source baselines reviewed

- ME-06-00 Shared Interaction & Tool Contracts V0.1.
- ME-06-A Tool & Operation Capability Inventory V0.1.
- ME-01-06 Editor Interaction Framework V0.1 and ME-01-07 Cross-architecture Consistency Review V0.1.
- ME-05-02 Shared Geometry Model & Component Operations V0.1.
- ME-05-03 Topology & Spatial Predicate Contract V0.1.
- ME-05-04 Coordinate Reference, Precision & Measurement Contract V0.1.
- ME-05-06 Cross-architecture Integration Review V0.4.
- Map Engine immutable scene, spatial query and geometry interaction contracts.

## 16. Change control

This document refines ME-01-06 and ME-06-00; it does not supersede their product/architecture authority. Specialist tool semantics remain in ME-02/03/04. If platform spikes demonstrate different event/capture behavior, update the adapter contract and acceptance cases without changing domain semantics or formal write authority.
