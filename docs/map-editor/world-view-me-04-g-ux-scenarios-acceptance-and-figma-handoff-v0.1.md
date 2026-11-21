# World View — ME-04-G UX Scenarios, Acceptance & Figma Handoff

- Version: 0.1
- Date: 2026-11-21
- Status: Buildings & Interior Spaces UX Working Baseline — product interaction and acceptance contract; not an implementation specification
- Parent: ME-04 Buildings & Interior Spaces Editor
- Depends on: ME-04-A through ME-04-F; ME-01-01 through ME-01-06; ME-03-H; ME-05-02 through ME-05-06; Core Domain descriptor/command/Asset contracts
- Scope: Building, Level, Space, entrance, opening, vertical circulation, Cave and underground facility journeys; shared Canvas/Inspector behavior; AI Candidate review; diagnostics; acceptance; Figma handoff
- Authority: Does not authorize Core Domain registry/schema/command/relationship/API changes, full 3D geometry, BIM, indoor routing or safety/compliance capabilities

## 1. Purpose and UX disposition

ME-04-G translates the specialist domain baselines ME-04-B through ME-04-F into a coherent creator-facing experience inside the **one shared World View Editor**.

Building & Interior Spaces is an editing mode, not a separate CAD/BIM application, GIS editor, map file, geometry authority, Object identity system or History system.

The creator must be able to:
- create and inspect one Building identity through exterior and interior views;
- organize Building-owned Levels without confusing floor order with altitude;
- author named Spaces and plan regions without creating a top-level Map Object for every room;
- define openings and access connections explicitly;
- understand multi-storey circulation without pretending that the V1 model is a full 3D building;
- author and inspect natural Cave Systems separately from constructed underground Buildings;
- understand unsupported capabilities and uncertainty without being misled;
- use AI for analysis and proposals while retaining control of formal application.

### 1.1 Governing principles

1. Canvas-first, contextual Inspector, persistent but non-dominant AI conversation, consistent with ME-01-06.
2. One stable Building identity across Exterior, Footprint, Interior and presentation views.
3. Space identity, geometry, boundary, adjacency, opening and traversability remain visibly distinct.
4. Level label/order, Building-local elevation and Map/world vertical position remain distinct.
5. Cave System, constructed Building and independent Subsurface Network are different concepts and must not be presented as interchangeable.
6. Preview, Candidate, authorization and formal application are distinct states.
7. Unsupported Core Domain capabilities remain clearly unavailable; no fake Save or successful application.
8. Ordinary authoring should be direct; not every manual action requires AI or a Candidate.
9. Technical diagnostics are available on demand, while default language remains creator-friendly.
10. No UI control implies structural, fire, accessibility, geological or human-safety certification.

## 2. Shared desktop composition

Use the shared ME-01-06 desktop shell. Do not design a separate Building application.

| Region | Responsibility | ME-04 content |
|---|---|---|
| Left navigation rail | Shared modes and project navigation | Buildings & Interior mode; Natural Geography and Urban modes remain available |
| Left content/tool panel | Objects, Building contents, layers and active tools | Buildings, Levels, Spaces, Openings, Connectors, Caves; active tool options |
| Central Canvas | Dominant spatial work surface | Map/exterior, footprint, floor plan, cave plan, previews and validation overlays |
| Right Contextual Inspector | Current selection and task context | Building/Level/Space/Opening/Connector/Cave details, diagnostics, provenance and actions |
| Bottom AI conversation | Natural-language analysis/proposal entry | Building creation, room layout, access analysis, cave survey interpretation |
| Shared status/context strip | Current Map/Branch/Time/Scope and operation feedback | Active Building, Level, plan revision, frame/link state, staging/apply status |

The Canvas remains visually dominant. The Inspector scrolls independently. AI conversation may expand for a proposal review but must not permanently displace the spatial work surface.

### 2.1 Left panel hierarchy

Recommended default hierarchy:

- **Buildings**
  - Building list / current selection
  - Exterior / Footprint / Interior view switch
  - Levels
  - Spaces
  - Openings & Connections
- **Underground**
  - Cave Systems
  - Survey / Chambers / Passages / Entrances
  - Subsurface Network (only if enabled by a future adopted capability; otherwise show explanatory unavailable state)
- **Layers**
  - Footprints
  - Interior plans
  - Boundaries / partitions
  - Openings / connectors
  - Cave survey
  - Derived diagnostics
- **Active Tool**
  - Tool-specific settings and concise help

Do not expose raw child IDs, schema names, geometry Asset IDs or database ownership fields in the primary navigation.

### 2.2 Inspector priority

1. Selected formal entity or component.
2. Active tool configuration when no entity is selected.
3. Selected Candidate and its proposed changes.
4. Selected diagnostic/evidence item.
5. General contextual help.

Avoid displaying complete Building, Level, Space and connector forms simultaneously. Show the selected object with compact related-content navigation.

## 3. Shared interaction and visual state model

### 3.1 Three state layers

| Layer | Examples | Authority |
|---|---|---|
| Editor transient state | Drawing boundary, dragging vertex, active snap guide, temporary measurement, selected Level | Editor-owned and disposable |
| AI Candidate / imported proposal | Suggested floor plan, proposed Space boundaries, inferred cave passage, proposed connector endpoints | AI Assistant staging; not formal map state |
| Formal domain state | Building identity, Level catalog, Space records, plan revision, adopted opening/connection, Cave System | Core Domain authority through supported typed operations |

Use consistent visual differentiation across all ME-04 tools. Transient geometry must not look like saved geometry. Candidate overlays must be visibly proposed and selectable for review.

### 3.2 Candidate lifecycle

Represent separately:
1. Proposed.
2. Reviewed.
3. Accepted / Rejected / Needs changes.
4. Authorized for exact Candidate version/hash and target context.
5. Applying through Core Domain validation.
6. Applied / Failed / Stale after authoritative read-back.

Accepting a plan is not the same as authorizing or applying it. Changing a boundary, Level reference, Building target or connector endpoint creates a new Candidate version and invalidates authorization bound to the previous version.

### 3.3 Operation context

Where a formal operation is supported, make the target context clear:
- current Building definition (Project-shared or Map-local ownership, described in creator language);
- Map Initial State;
- selected Map/Branch History and Historical Time.

Do not silently choose a context from the current viewport or selected tab. Do not expose raw database scope names as the default UI.

### 3.4 Undo and recovery

- Local Undo/Redo applies to the active uncommitted gesture/edit session.
- Formal changes use supported Core Domain operations and are not undone by cancelling the tool or AI Task.
- A failed Asset publication or stale CAS update must leave no formal reference and must present a recoverable state.
- A stale geometry/boundary anchor must be re-resolved or require review; never silently relocate an opening.
- If a formal operation is not enabled, the UI must not show a misleading Save/Apply success path.

## 4. Creator journeys

### 4.1 Create a Building from the Map

1. Choose **Create Building** in the active Map context.
2. Enter a name and optional broad building use/class.
3. Create a stable Building identity through Core Domain when the descriptor is enabled.
4. Draw/import a footprint or leave the Building unplaced if the adopted workflow permits it.
5. Inspect overlap, Map extent and nearby terrain/Settlement/Road context as spatial evidence.
6. Choose whether this is initial Map content or a selected History change when applicable.
7. Apply through the supported typed path and reconcile authoritative state.
8. Open the Building Inspector with the same stable identity.

Acceptance:
- one Building identity is retained across all views;
- identical geometry does not merge Buildings;
- spatial proximity does not establish ownership, access or Settlement containment;
- if the descriptor is unavailable, retain a draft/Candidate and explain the capability gap.

### 4.2 Inspect and refine a Building footprint

1. Select the Building on Canvas or in the list.
2. Inspector identifies the Building and active Exterior/Footprint view.
3. Start a transient geometry edit.
4. Show geometry preview and validation feedback.
5. Preserve the Building ID and footprint component identity when the operation is an adjustment/refinement.
6. Publish immutable geometry and apply the typed operation only when supported.
7. Re-read formal component membership and refresh derived spatial evidence.

Acceptance:
- editing the footprint does not create a new Building;
- derived 3D/massing preview is not formal geometry;
- stale selection/component references are rejected or re-resolved.

### 4.3 Create and organize Levels

1. Open the Building's Interior view.
2. Show a Floors/Levels panel with creator-friendly labels and kind icons.
3. Add Ground Floor, Basement, Upper Floor, Mezzanine or Custom Level.
4. Assign a stable Building-scoped Level ID.
5. Optionally set Building-local elevation and vertical extent in a clearly labelled advanced section.
6. Show a vertical stack ordered by physical local elevation, while allowing creator display order/labels to differ.
7. Show “World elevation not linked” when no compatible Map vertical reference exists.

Acceptance:
- label and display order are not treated as altitude;
- a missing vertical datum is Unknown, not zero;
- terrain elevation is shown as sourced context, not copied silently;
- split-level/overlapping Levels are warnings or explicit supported arrangements, not rejected by a simplistic one-floor-per-height rule.

### 4.4 Create Spaces and room boundaries

1. Select a Level.
2. Choose Room, Corridor, Lobby, Service Area, Void or another supported Space kind.
3. Draw a plan region in the Building-local 2D frame.
4. Enter an optional label/use.
5. Show closure, self-intersection, overlap, sliver and frame diagnostics.
6. Assign a stable Space ID and typed Level Slice when supported.
7. Preview the Space in relation to neighboring Spaces and the Building footprint.
8. Apply through the Building-owned plan/Space service.

Acceptance:
- Space ID is stable through rename and geometry refinement;
- a polygon is not a Space until explicitly assigned;
- a hole is not automatically another Space;
- partial plan gaps are interpreted according to declared plan completeness;
- Spaces remain Building-owned child records, not top-level Map Objects.

### 4.5 Refine a shared boundary or partition

1. Select a boundary shared by adjacent Spaces.
2. Show the affected Spaces and shared-boundary identity.
3. Edit the boundary once; preview all affected Space loops.
4. Validate closure, overlap, gaps and boundary references.
5. If the shared component cannot be updated consistently, reject the edit or present a repair Candidate.
6. Keep openings anchored to the boundary revision; mark stale openings for review when anchors cannot be re-resolved.

Acceptance:
- adjacent Space boundaries do not drift through duplicated independent edits;
- a boundary edit does not split/merge Space identities;
- a wall/partition line does not imply an access connection.

### 4.6 Add an Opening and Access Connection

1. Select a boundary or Building exterior access point.
2. Choose Door, Archway, Passage, Window, Hatch or another supported opening kind.
3. Anchor the Opening to a stable boundary component and revision.
4. Select explicit source and target endpoints.
5. Configure direction only when supported.
6. Set operational state separately (Open, Closed, Locked, Blocked, Damaged, Unknown).
7. Preview the explicit connection and any derived reachability result.
8. Apply through the supported typed operation.

Acceptance:
- a window is not traversable by default;
- a door symbol alone does not create connectivity;
- closed/locked/blocked state does not delete the Opening;
- unknown is not treated as open;
- stale boundary anchors require review.

### 4.7 Create a multi-storey vertical connector

1. Select Stair, Elevator, Ladder or Ramp.
2. Choose source and target Level IDs.
3. Add intermediate stops/landings where required.
4. Reference the connected Space endpoints when known.
5. Show the ordered route in the Level stack and plan views.
6. Validate that every segment joins consecutive declared stops and all Levels belong to the same Building.
7. Show schematic route only; do not imply full 3D geometry or engineering validation.

Acceptance:
- connector identity is stable across plan refinement;
- Level labels are not endpoint IDs;
- visually aligned stairs without explicit stops are not a formal connector;
- no universal indoor-routing or evacuation capability is implied.

### 4.8 Author a Cave System

1. Choose **Create Cave System** in the relevant Map scope.
2. Enter a name and available classification/description.
3. Create stable Cave System identity.
4. Add known Cave Entrances, Chambers, Passages and Survey Stations.
5. Select or define a Cave Survey Frame; show whether the vertical datum is linked or relative/unknown.
6. Import survey plans/profiles as evidence or Candidate overlays.
7. Assign explicit connections between compatible cave endpoints.
8. Review partial survey extent, uncertainty and unresolved branches.
9. Apply only supported Cave operations.

Acceptance:
- a Cave System may cross multiple Site/Locality scopes without splitting identity;
- multiple entrances may reach one internal system;
- crossing survey lines do not create a junction;
- incomplete survey does not imply no continuation;
- survey evidence is not presented as full 3D cave geometry.

### 4.9 Model a constructed underground Building

1. Create or select an ordinary Building.
2. Add Basement/Underground Levels in its Building-local Vertical Frame.
3. Create Spaces and plan regions using the same Building interior workflow.
4. Add entrances/openings/connectors through ME-04-E semantics.
5. Optionally compare the Building origin/Levels with terrain elevation evidence.
6. Keep terrain binding explicit and provenance-bearing.

Acceptance:
- basement rooms do not become Cave Objects;
- negative local elevation does not automatically imply absolute depth;
- a terrain Field Revision does not rewrite Building-local Levels;
- Building/Space identity remains consistent with the above-ground portions.

### 4.10 Cross-Building underground network

If the creator attempts to connect separate Buildings through a tunnel/utility/metro network:
- show the Subsurface Network capability as unavailable unless a future Core Domain contract is adopted;
- allow a Candidate or planning annotation only if clearly non-formal;
- do not create a fake connector owned by one Building;
- do not infer a formal network node from a snapped coordinate.

Acceptance:
- unsupported cross-Building connectivity is clearly blocked;
- the user can still preserve a planning note/Candidate without mistaking it for a formal network.

### 4.11 AI-assisted layout or survey interpretation

Example requests:
- “根据这张平面图，先识别可能的房间边界。”
- “检查地下室和地面入口之间是否有明确通路。”
- “根据洞穴测量草图整理可能的洞室和通道。”

Flow:
1. AI states the interpretation scope and source evidence.
2. AI produces one or more Candidate versions.
3. Canvas overlays proposed geometry distinctly from formal geometry.
4. Inspector lists proposed Space/Level/connector/cave changes and uncertainty.
5. User can accept/reject individual items or request changes.
6. Any edit creates a new Candidate version.
7. Authorization is bound to the exact Candidate and target context.
8. Core Domain validates/applies only supported typed operations.
9. UI reconciles authoritative state and shows partial failure/stale result clearly.

Acceptance:
- AI cannot create identity or write formal data directly;
- unsupported inferred doors, safety claims, structural attributes or 3D geometry are omitted or marked Unsupported;
- no success is reported before authoritative read-back.

## 5. Inspector content contracts

### 5.1 Building Inspector

Show:
- name and broad use/class;
- stable identity only in advanced diagnostics;
- ownership context in creator language;
- footprint status and spatial observations;
- Exterior / Footprint / Interior view shortcuts;
- Level count and Space count;
- plan revision and validation summary;
- provenance and current formal/staging state.

### 5.2 Level Inspector

Show:
- label and Level kind;
- physical vertical order/relative elevation where known;
- display order separately from physical elevation;
- Building-local frame link status;
- Map/world vertical binding status;
- terrain evidence freshness;
- associated plan regions and connectors.

### 5.3 Space Inspector

Show:
- label, kind and optional use;
- owning Building and Level(s);
- plan region count and geometry validity;
- adjacency candidates separately from formal Access Connections;
- boundary/opening references;
- multi-level/vertical span status;
- provenance and plan revision.

### 5.4 Opening / Connector Inspector

Show:
- feature kind and stable identity in advanced diagnostics;
- boundary anchor and source geometry revision;
- explicit source/target endpoints;
- direction where supported;
- physical/operational state separately;
- stale anchor or unresolved endpoint diagnostics;
- no safety or code-compliance badges.

### 5.5 Cave Inspector

Show:
- Cave System name and survey completeness;
- entrances, chambers, passages and survey stations;
- Cave Survey Frame and vertical datum status;
- known/unknown continuation;
- survey method, uncertainty and provenance;
- access/condition evidence separate from geometry;
- no inferred safety rating.

## 6. Diagnostics and status language

Use consistent outcomes:
- **Pass** — supported check succeeded for the exact current evidence.
- **Warning** — issue exists but does not necessarily block the operation.
- **Blocked** — a required invariant or authorization failed.
- **Unknown** — insufficient evidence to decide.
- **Unsupported** — the product/domain contract does not support the requested operation.
- **Stale** — evidence no longer matches the current source revision/context.
- **Acknowledged exception** — a warning was explicitly accepted where policy allows.

Do not use green “valid” indicators for Unsupported or Unknown. A stale result must not be shown as current. Every diagnostic should offer “why this matters”, source context and a way to locate the affected feature where possible.

## 7. Failure and recovery journeys

| Failure | User-facing behavior | Formal-state guarantee |
|---|---|---|
| Invalid plan boundary | Highlight issue and offer edit/review | No formal write |
| Level reference belongs to another Building | Explain owner mismatch | No formal write |
| Shared boundary update conflicts | Show affected Spaces and stale source | No partial boundary update |
| Opening anchor stale after geometry edit | Mark opening unresolved and request re-anchor | No silent relocation |
| Candidate changed after authorization | Require review/authorization again | Old authorization not reused |
| Asset publication succeeds but DB commit fails | Show recoverable save failure | No formal reference; safe orphan recovery |
| CAS/revision conflict | Offer refresh/rebase preview | No lost update |
| Unsupported 3D or cross-Building connector | Explain current capability boundary | No fake flattened/partial formal data |
| Missing vertical datum | Show relative-only/unknown state | No invented world altitude |
| Cave survey uncertainty | Preserve uncertainty and evidence | No false precision |
| App closes during transient edit | Restore or discard staging safely | No implied formal commit |

## 8. Accessibility and interaction quality

- All primary tools and Inspector actions must be keyboard reachable.
- Selected Building/Level/Space context must be announced meaningfully to assistive technology.
- Do not rely on color alone to distinguish formal geometry, Candidate overlays, warnings and unsupported data; combine line styles, icons and labels.
- Provide non-canvas selection paths through object/space lists and diagnostics.
- Ensure dense floor plans remain navigable at high DPI and with keyboard zoom/pan alternatives.
- Tooltips explain specialized terms such as “Building-local elevation” in creator language.
- Multi-step connector and Cave survey workflows must preserve focus and offer clear cancellation.
- Avoid modal dialogs for routine name/label changes; reserve blocking confirmation for risk, scope or irreversible operations.

## 9. Acceptance matrix

| ID | Area | Acceptance condition |
|---|---|---|
| G-001 | Shared shell | ME-04 uses the same Canvas, Inspector, AI and navigation foundation as other modes |
| G-002 | Building identity | Exterior/Footprint/Interior views preserve one Building identity |
| G-003 | Levels | Label/order and physical local elevation are visually distinct |
| G-004 | Vertical datum | Missing Map vertical reference is Unknown, not zero |
| G-005 | Spaces | Space identity is Building-scoped and stable through rename/refinement |
| G-006 | Boundaries | Shared boundary edits keep affected Space loops coherent |
| G-007 | Adjacency | Geometric adjacency is not displayed as traversable connection |
| G-008 | Openings | Opening identity/geometry and operational state are separate |
| G-009 | Connectors | Vertical route uses explicit Level/Space endpoints and ordered stops |
| G-010 | Cave identity | Cave System can cross Site scopes without identity duplication |
| G-011 | Cave survey | Unknown datum/partial survey/uncertainty remain visible |
| G-012 | Constructed underground | Basement uses Building/Level/Space rather than Cave model |
| G-013 | Cross-Building network | Unsupported network operation is not falsely offered as formal save |
| G-014 | AI Candidate | Candidate, review, authorization and application remain distinct |
| G-015 | Stale evidence | Stale geometry/anchor/transform requires refresh or review |
| G-016 | Formal authority | UI reports success only after Core Domain reconciliation |
| G-017 | History | Local Undo does not imply rollback of formal Commands |
| G-018 | Accessibility | Core workflows are keyboard accessible and not color-only |
| G-019 | Diagnostics | Unsupported/Unknown/Blocked/Stale are distinguishable |
| G-020 | Recovery | Publication/CAS failures do not leave partial formal state |

## 10. Figma handoff — master prompt

The following prompt is intended for Figma AI to create a coherent, editable desktop UI design. It describes screens and behavior, not backend implementation.

### Figma AI prompt

Design a professional desktop application UI for **World View — Buildings & Interior Spaces Editor**, integrated into an existing unified world-map editor. Do not create a standalone CAD, BIM, GIS or indoor-navigation product.

**Product context**
- Windows/macOS desktop, 16:10 or 16:9 wide layout.
- One shared Map Editor with a dominant central Canvas, left navigation/tool panel, right contextual Inspector and persistent AI conversation docked along the bottom.
- Professional, restrained, information-dense visual language. Prioritize clarity, spatial context and editing precision over decorative cards.
- Use a consistent component system for navigation, tabs, toolbars, Inspector sections, status chips, diagnostics, Candidate review and confirmation states.
- All screens must be editable Figma layers/components, with reusable variants and Auto Layout where appropriate.

**Create these core frames**
1. **Building Exterior / Footprint** — map context with selected Building footprint, surrounding Settlement/Road/terrain evidence, left Buildings panel and right Building Inspector.
2. **Building Interior — Level Plan** — same Building identity, Level selector/stack, central 2D plan, Space list, shared boundary editing and Space Inspector.
3. **Level Management** — compact Level list and vertical stack; distinguish display order, label and Building-local elevation; show “World elevation not linked” state.
4. **Space Authoring** — active Room/Corridor/Lobby/Void tool, boundary preview, validation overlay, Space Inspector and plan completeness status.
5. **Boundary & Opening Editing** — selected shared boundary, anchored Opening, affected Spaces, stale-anchor warning and explicit endpoint editor.
6. **Vertical Circulation** — schematic multi-Level route with Stair/Elevator/Ramp, ordered stops/landings, explicit endpoints and unsupported 3D disclaimer.
7. **Cave System Survey** — Cave plan with multiple entrances, chambers, passages, survey stations, partial-survey boundary and uncertainty overlays.
8. **Cave Inspector / Survey Frame** — survey frame, relative/absolute elevation status, source provenance, uncertainty and unknown continuation.
9. **AI Candidate Review** — proposed floor-plan/Space changes shown as distinct overlays; item-level accept/reject, evidence, uncertainty, exact target and authorization status.
10. **Validation & Recovery** — invalid geometry, stale boundary anchor, CAS conflict, unsupported cross-Building network and failed publication states.

**Shared layout requirements**
- Left panel: Buildings, Levels, Spaces, Openings & Connections, Caves, Layers and Active Tool.
- Central Canvas: large, unobstructed spatial area; overlays for selection, Candidate, validation, measurement and connector endpoints.
- Right Inspector: contextual content for the current selection only; do not show all entity forms at once.
- Bottom AI dock: conversational input and compact response; expands during Candidate review.
- Shared status strip: active Map/Branch/Time, Building, Level, plan revision and operation state.

**Visual semantics**
- Formal geometry, transient edit geometry and AI Candidate geometry must be visually distinguishable by more than color (line pattern, opacity, icon and label).
- Warnings, Blocked, Unknown, Unsupported and Stale must have distinct text labels and iconography.
- Adjacency candidate must not look like a confirmed connection.
- A door/opening must show physical feature and operational state separately.
- Unknown vertical datum must never be shown as zero elevation.
- Cave survey uncertainty and partial coverage must be legible.
- Avoid green “safe/compliant” badges; the product does not certify structural, fire, accessibility, geological or human safety.

**Interaction prototypes**
- Switch Exterior ↔ Footprint ↔ Interior without changing Building identity.
- Select a Level and update the plan context.
- Draw a Space boundary and show validation feedback before formal application.
- Edit a shared boundary and preview all affected Space regions.
- Place an Opening on a stable boundary anchor and choose explicit endpoints.
- Build a multi-storey connector through ordered Level stops.
- Select a Cave Entrance and highlight its internal endpoint.
- Review an AI Candidate and show that editing it invalidates prior authorization.
- Demonstrate an Unsupported cross-Building Subsurface Network state without offering a fake Save action.

**Content and constraints**
- Use realistic sample labels: “Old Mill Building”, “Ground Floor”, “Basement 1”, “Great Hall”, “West Corridor”, “Service Room”, “North Entrance”, “Stair A”, “Limestone Cave System”.
- Use plausible but fictional geometry and names.
- Do not invent database fields, APIs or backend behaviors beyond the product semantics above.
- Do not add full 3D BIM modeling, wall-solid editing, code compliance, evacuation routing, geological safety scoring or universal indoor routing.
- Preserve the shared World View Editor shell and component language.

Deliver a cohesive design system and all ten frames, with desktop variants for normal, selected, editing, Candidate, warning, unsupported and failed states. Prioritize the first five frames as the initial implementation design set; the remaining frames complete the specialist workflow coverage.

## 11. Final disposition

ME-04-G completes the creator-facing UX and acceptance baseline for the current ME-04 specialist domain proposals. It does not authorize implementation of unsupported domain records or capabilities.

ME-04-H must now reconcile the complete ME-04 package with Core Domain, AI Assistant, ME-01/02/03/05 and Map Engine. It must identify exactly which descriptors, child aggregates, geometry roles, endpoints, commands, schema changes and technical proofs would be required before implementation, and must preserve all current authority boundaries.
