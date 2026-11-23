# World View — ME-08-07 Spatial Operation UX & Scenario Matrix

- Version: 0.1
- Date: 2026-11-23
- Status: Spatial Operation UX Working Baseline
- Workstream: ME-08 — Spatial Operations & Domain Integration
- Depends on: ME-08-00 V0.1; ME-08-06 V0.1; ME-01-05/06/07; ME-02-08/09; ME-03-G/H; ME-04-G/H; ME-05-02/03/04/06; ME-06-A through ME-06-G
- Authority: Product / Editor UX integration contract. This document does not authorize new Core Domain Object Types, Relationship Types, Commands, registries, persistence tables, APIs or executable schemas.

## 1. Purpose

ME-08-07 translates the ME-08-06 unified spatial-operation lifecycle into concrete desktop-editor journeys, interaction patterns, scenario coverage and acceptance criteria.

The target is a single-user desktop application for Windows and macOS. Collaborative editing, multi-user presence, distributed locks and conflict-resolution UX are out of scope. Local source-fingerprint and query-freshness checks remain required to prevent stale edits and accidental overwrites.

The design supports:
- World Map authoring;
- dedicated 2D Building Interior authoring;
- natural Cave and underground-space authoring;
- Spatial Site composition across one or many buildings/outdoor entities;
- nested Spatial Group organization;
- layout/theme/module instantiation;
- domain-specific connection editing (Door, Opening, Entrance, Vertical Connector, Cave Passage, Tunnel, Portal and future adopted types).

This is a UX and scenario contract, not a declaration that every listed operation is currently implemented or formally enabled.

## 2. UX principles

1. **Context is visible and explicit.** The user can always identify the active Map, temporal scope, scene, selected Level/Cave frame and current tool.
2. **The scene switch is real.** Opening a Building enters a dedicated 2D interior editor; it is not an overlay that exposes all interior geometry on the World Map.
3. **One operation, one declared intent.** The UI distinguishes view changes, organization changes, geometry changes and domain-relationship changes.
4. **Preview before semantic commitment.** Multi-object, connection, template, AI-originated and other high-impact changes show a reviewable preview before formal application.
5. **Domain vocabulary remains visible.** Users create a Door or Cave Passage, not an anonymous “line connection”.
6. **No silent semantic inference.** Geometric contact, overlap, adjacency or proximity does not silently create containment, access, traversability, road connectivity or ownership.
7. **Progressive disclosure.** Common operations remain direct; coordinate precision, endpoint identity, validation evidence and advanced constraints are available in Inspector/details panels.
8. **Failure is recoverable and explained.** Rejected operations retain diagnostics and, where safe, the staging preview. Cancelled staging never changes formal state.
9. **Keyboard and pointer parity.** Core operations have keyboard-accessible alternatives; tooltips, focus, selection and diagnostics remain usable without relying only on color.
10. **No false completion.** The UI reports formal success only after authoritative Core Domain reconciliation; renderer refresh is a separate status.

## 3. Shared editor frame

The shared desktop frame is consistent across scenes while scene-specific tools and inspectors change.

### 3.1 Persistent regions

| Region | Responsibility | Required behavior |
|---|---|---|
| Application / document header | Project, Map and save/reconciliation status | Show active document and formal write outcome; do not imply every view edit is a save |
| Context breadcrumb | World Map → Site → Building → Level / Cave frame | Show current scene and ownership path; each navigation step is explicit |
| Tool rail | Finite built-in tool set | Tool availability is capability-driven; unsupported tools are disabled with an explanation |
| Main Canvas | Current scene's spatial content | Only render the active scene's appropriate spatial representation |
| Inspector | Selected entity/component/relationship and properties | Show typed identity, coordinate frame, domain details and applicable operations |
| Layer / Structure panel | Map layers, Levels, Groups and Site membership | Clearly distinguish visual layers, organizational Groups and physical/domain entities |
| Diagnostics panel | Validation, uncertainty, freshness and blockers | Distinguish error, warning, unknown, unsupported and stale evidence |
| Operation tray | Current gesture, staging, Candidate or Change Set | Show operation scope, dependencies, preview, review and apply/cancel actions |
| Contextual command area | Search, command palette and help | Commands are scoped to the active scene and capability set |

The AI assistant may explain or propose operations in the shared interaction surface. AI proposals enter the same Candidate/Change Set review path and never bypass formal authorization.

### 3.2 Scene-specific context strip

- **World Map:** Map identity, active Branch/time when applicable, map coordinate reference, selected region/scale.
- **Building Interior:** Building identity, Level, Building-local frame, optional vertical reference and return-to-World action.
- **Cave:** Cave System identity, survey frame, selected level/stratum/section where adopted, survey confidence/uncertainty.
- **Spatial Site:** Site identity, active sub-scene, selected Group and current membership scope.

The UI must not silently switch temporal write scope when changing scenes or selecting a different object.

## 4. Standard interaction patterns

### 4.1 Select and inspect

1. User selects an entity, component or relationship on Canvas or Structure panel.
2. Editor resolves a typed selection snapshot and displays its identity and owner.
3. Inspector shows semantic type, geometry role, coordinate context, relationships and supported operations.
4. If selection evidence is stale, the editor refreshes/re-resolves before offering a formal action.

Selection is not a formal mutation. Clicking a visual line or boundary must not assume a specific semantic object if hit-testing cannot resolve one uniquely; present a disambiguation list or report ambiguity.

### 4.2 Create or draw geometry

1. User selects a domain-specific creation tool.
2. Tool states the target type and coordinate context before drawing.
3. Gesture creates temporary geometry only.
4. User finishes the gesture; editor validates basic shape and descriptor capability.
5. Preview shows the proposed geometry and relevant domain constraints.
6. User confirms when the operation policy requires it.
7. Core Domain applies the supported typed operation; editor reconciles the formal result.

A screen-space snap or hit-test radius is interaction assistance, not a formal geometry tolerance. Snapping may propose a point/anchor but must not fabricate a typed endpoint.

### 4.3 Modify geometry

- Vertex/control-point edits preserve component identity and geometry role when the adopted operation contract says so.
- Transform tools expose the active coordinate frame and pivot/anchor.
- Local Building geometry is edited in Building-local coordinates; World Map geometry is edited in its declared Map/world frame.
- If a transform or conversion is unsupported, the UI blocks it rather than silently changing frames.
- Scale is not a universal toolbar action; it appears only for types whose adopted constraints explicitly allow it.

### 4.4 Create or edit a connection

Connection creation is a semantic workflow, not merely drawing a line.

1. Choose a typed connection tool (for example Door, Entrance, Vertical Connector or Cave Passage).
2. Select the first compatible typed endpoint.
3. Select the second compatible typed endpoint.
4. Define anchors and optional route/geometry, directionality and traversal constraints.
5. Review endpoint identities, coordinate frames and domain diagnostics.
6. Confirm and submit through the supported formal path.

Ordinary connections have exactly two endpoints. Multi-way junctions are modeled through an intermediate spatial entity and separate pairwise connections. If a candidate endpoint is only geometrically nearby and lacks a compatible typed identity, the UI must explain the missing semantic endpoint.

### 4.5 Organization and grouping

- Group creation, reparenting and membership changes occur in the Structure panel or equivalent explicit organization tool.
- Dragging an entity between Groups changes organization membership only.
- The UI must not animate or imply a physical move when only membership changes.
- A separate “Transform selected members” compound action may be offered only if adopted capabilities support it; it must list the exact target set, common frame and per-member result.
- Physical containment and access remain separate domain relationships.

### 4.6 Template / module instantiation

1. User selects a layout, theme or functional module from a type-specific library.
2. User chooses an insertion anchor and target context/frame.
3. Editor resolves the template's required and optional members.
4. A dependency-closed preview shows all entities, geometry, Groups and typed connections.
5. Diagnostics identify unsupported types, invalid endpoints, frame mismatch and spatial conflicts.
6. User may adjust supported parameters and placement.
7. Formal application occurs as one dependency-closed Change Set when supported by the existing atomic boundary.
8. If required members are unsupported or blocked, full instantiation is unavailable; a partial preview may be inspected but must be explicitly labeled and cannot be presented as a complete successful instance.

Themes change presentation only. Layout templates/module recipes create concrete instances. The UI must label the distinction.

### 4.7 Cancel, undo and recovery

- **Cancel gesture:** stop current pointer/keyboard gesture; no formal mutation.
- **Discard preview:** discard staged proposal; no formal mutation.
- **Undo transient edit:** revert a local unsubmitted adjustment.
- **Undo committed change:** invoke only an adopted History/Branch compensating or reverse operation; never rewrite prior history or invent a generic inverse.
- **Unknown result:** reconcile authoritative state before offering retry.
- **Derived refresh failure:** retain the formal success result, show presentation refresh as degraded, and offer/retry rebuild; do not report the formal commit as failed.

## 5. Scenario matrix

| ID | Scene / scenario | User goal | Primary interaction | Required preview / review | Key validation | Formal result boundary |
|---|---|---|---|---|---|---|
| UX-01 | World Map — create feature | Draw a supported region/line/point entity | Typed tool → draw → inspect | Geometry preview; policy-dependent confirmation | Geometry validity, frame, descriptor, spatial constraints | Initial State or active History path |
| UX-02 | World Map — adjust boundary | Refine a selected feature | Select → edit controls → finish | Before/after geometry and affected evidence | Component identity/role, precision, freshness | Registered adjust/refine operation only |
| UX-03 | World Map — relocate Building | Move a Building on the Map | Select Building → move/rotate | Footprint preview and transform summary | Map frame, transform, dependent external anchors | Correct Map/History or shared-definition path |
| UX-04 | Enter Building Interior | Edit rooms and levels | Open Building → dedicated scene | No formal preview required for scene switch | Building identity and interior capability availability | View-only; no write |
| UX-05 | Interior — create Room | Add a room on selected Level | Select Level → room tool → draw | Room geometry and local context preview | Building-local frame, Level binding, space constraints | Supported Building/Initial State or History path |
| UX-06 | Interior — edit shared boundary | Adjust boundary shared by adjacent Spaces | Select boundary → edit | Both affected Spaces and resulting boundaries | Shared component ownership, topology and domain validity | Typed component operation only |
| UX-07 | Interior — create Door | Connect two compatible Spaces through an Opening | Door tool → endpoint A/B → anchor | Endpoint identities, opening location, traversal preview | Endpoint type, Level/frame compatibility, cardinality | Adopted typed relationship/Command only |
| UX-08 | Interior — vertical circulation | Connect Levels | Vertical Connector tool → endpoints | Vertical path, levels and direction | Building vertical frame, level references, endpoint compatibility | Adopted typed connection operation only |
| UX-09 | Cave — create Passage | Connect two cave spaces | Passage tool → endpoints → survey geometry | Survey path and endpoint evidence | Cave survey frame, uncertainty, compatible endpoints | Adopted Cave connection path only |
| UX-10 | Cave — uncertain survey | Record incomplete/uncertain passage evidence | Survey tool → mark evidence/uncertainty | Uncertainty and provenance shown explicitly | No false precision; supported uncertainty model | Formal evidence only if adopted; otherwise staged note/candidate |
| UX-11 | Site — compose Academy | Create multi-building academy site | Site composer → choose modules → place | Whole composition, required/optional members and links | Dependency closure, typed capability, frame and site constraints | One supported Change Set |
| UX-12 | Site — organize Groups | Move buildings between functional Groups | Structure panel → reparent | Membership diff; no geometry movement | Group capability and membership rules | Organization relation only if formally adopted |
| UX-13 | Site — transform selection | Move several selected entities together | Explicit compound transform | Exact target list, common frame, per-object diff | Every member's capability and frame compatibility | One supported compound Change Set; otherwise unavailable |
| UX-14 | Template — instantiate Residence | Create a multi-room residential layout | Template library → anchor → configure | Complete dependency-closed layout | Required member support, geometry and connection rules | One supported Change Set |
| UX-15 | Template — unsupported member | Inspect template with unsupported Portal/type | Template preview | Partial preview clearly marked; no full apply | Unsupported type and dependency closure | No complete formal application |
| UX-16 | Connection — rebind endpoint | Change a Door/Passage endpoint | Inspector → choose compatible endpoint | Before/after connectivity and affected routes | Typed endpoint, frame, cardinality, freshness | Adopted typed rebind operation only |
| UX-17 | Organization vs physical containment | Place entity in Group without changing geometry | Reparent in Structure | Membership-only diff | No implicit containment or transform | Organization change only |
| UX-18 | AI-proposed spatial change | Review AI-generated layout/geometry | Candidate → inspect → select dependencies | Exact version, provenance, diagnostics, diff | Candidate freshness, authorization, domain validation | Human-authorized Change Set |
| UX-19 | Validation failure | Correct invalid geometry or connection | Open diagnostic → focus target → edit | Retained staging and diagnostic details | Typed failure, no silent repair | No formal mutation until valid |
| UX-20 | Stale query / selection | Apply after source changes locally | Re-resolve → regenerate preview | Fresh evidence and updated diff | Scene generation/source fingerprint | Stale proposal rejected or refreshed |
| UX-21 | Cancel / discard | Abandon current operation | Cancel / discard staging | Clear indication of discarded draft | Formal state unchanged | No write |
| UX-22 | Unknown submission outcome | Recover after interruption | Reconcile status → retry only if safe | Authoritative result and operation identity | Idempotency and no duplicate application | Existing recovery contract |
| UX-23 | Derived cache failure | Continue after renderer/index failure | Rebuild derived state | Formal success remains visible | Cache generation and rebuild diagnostics | No change to formal commit |
| UX-24 | Cross-platform parity | Complete same workflow on Windows/macOS | Keyboard/pointer workflows | Same semantic preview and result | Coordinate/serialization and interaction conformance | Identical formal semantics |

## 6. Scene-specific UX contracts

### 6.1 World Map

- The World Map is the exterior/geographic authoring scene.
- Building interiors are represented by their adopted exterior anchor/footprint and appropriate symbol/summary, not by rendering all room geometry.
- Selecting a Building exposes an explicit “Open Interior” action when an interior model is available.
- Moving a Building is a world-frame operation; editing its interior is a separate local-frame operation.
- Map-scale/LOD representations are presentation choices and never replace formal geometry.

### 6.2 Building Interior

- Opens as a dedicated 2D plan editor.
- The context breadcrumb identifies Building and Level; switching Level is explicit.
- Rooms/Spaces, openings, doors and vertical connectors use domain-specific tools and Inspector forms.
- A Level change changes the editing slice/context; it does not imply moving geometry vertically.
- Vertical relationships are inspected through a dedicated connectivity view/list; V1 does not require a full 3D editor.
- Returning to World Map is explicit and preserves the prior viewport/selection where possible.

### 6.3 Cave and underground spaces

- Cave geometry is authored in its declared survey frame, not assumed to use Building-local coordinates.
- The Canvas may show a 2D plan/section appropriate to the selected survey slice; the view does not redefine the underlying survey coordinate system.
- Uncertainty, provenance and observed-versus-inferred evidence must remain visible.
- Passage connectivity is explicit and typed; a crossing or apparent contact is not a passage.
- Constructed underground facilities may reuse Building/Level/Space workflows only where the relevant ME-04 domain model applies.

### 6.4 Spatial Site and multi-building composition

- A Site is the composition context for a single Building or multiple spatial entities.
- The Site view may provide an overview of buildings, outdoor areas, paths and special facilities.
- Spatial Groups organize navigation and functional composition but do not automatically assert physical containment.
- Site-level operations must declare whether they affect membership, entity geometry or typed relationships.
- Future Academy, War Academy, Castle and other fantasy compositions are added through typed modules/templates, not hard-coded scene classes.

## 7. Inspector and diagnostics requirements

Inspector content is type-aware. Minimum fields include:
- display name and stable typed identity;
- owner/Site/Building/Level/Cave frame context;
- geometry component role and spatial reference where applicable;
- typed endpoints and traversal semantics for connections;
- organization memberships, visually separated from domain relationships;
- supported operations and why unsupported operations are unavailable;
- source/provenance and freshness state where relevant.

Diagnostics use explicit outcome classes:
- **Error / Blocked:** operation cannot proceed;
- **Warning:** operation may proceed only under the relevant policy;
- **Unknown / Indeterminate:** evidence is insufficient; never display as a pass;
- **Unsupported:** capability or descriptor is not adopted;
- **Stale:** source/query/selection evidence must be refreshed;
- **Info:** non-blocking explanation.

Every actionable diagnostic identifies the affected entity/component/relationship and, when possible, provides “Focus on Canvas” and a concise correction path. Color is never the sole carrier of meaning.

## 8. Operation risk and confirmation UX

| Risk class | Examples | UX treatment |
|---|---|---|
| View-only | Pan, zoom, scene switch, select Level | Immediate; no formal confirmation |
| Transient | Draw in-progress, move control point before finish | Immediate staging; Escape cancels |
| Bounded manual edit | Supported single-component adjustment | Inline preview; apply behavior follows adopted operation policy |
| Semantic / dependency-changing | Door endpoint rebind, duplicate entity, multi-object composition | Explicit before/after review |
| AI-originated | AI geometry/layout/relationship proposal | Candidate review and human authorization per ME-06 |
| Unsupported / blocked | Missing descriptor, incompatible endpoint, invalid frame | No Apply action; explain blocker |

Confirmation dialogs must not be used for every pointer gesture. Use an operation tray/review surface for material changes, with a clear summary of what changes and where.

## 9. Accessibility and desktop behavior

- All primary tools are reachable by keyboard and discoverable in a command palette.
- Escape cancels the current gesture; a second explicit action is required to discard a completed staged proposal.
- Focus is preserved when switching Canvas, Inspector and diagnostics.
- Selection and validation state use text/icon/shape in addition to color.
- Tooltips and labels identify the active coordinate frame and domain operation.
- High-DPI scaling and logical-pixel hit-test behavior remain consistent with ME-06/ME-07 contracts.
- Windows and macOS use the same semantic operation behavior; platform-specific shortcut labels may differ.
- Window resizing, multi-monitor placement and restart during staging must not corrupt formal state. Staging recovery follows ME-06-F; it is not treated as formal persistence.

## 10. Acceptance criteria

The ME-08-07 UX baseline is accepted when design prototypes and subsequent implementation tests demonstrate:

1. A user can distinguish World Map, Building Interior, Cave and Site contexts at all times.
2. Opening a Building enters a dedicated 2D interior scene and does not expose all interior geometry on the World Map.
3. A room edit cannot accidentally write Building footprint geometry or use the Map coordinate frame.
4. Group reparenting changes membership only and is visually distinguishable from physical movement.
5. Creating a Door/Passage requires two compatible typed endpoints and explicit domain semantics.
6. Multi-way junctions are represented by a spatial entity plus pairwise connections, not an ordinary connection with more than two endpoints.
7. Template instantiation previews all required members and dependency closure; unsupported required members block full application.
8. AI proposals use the same Candidate/Change Set authorization path as other AI changes.
9. Stale query/selection evidence is re-resolved before formal application.
10. Cancel/discard leaves formal state unchanged; unknown submission outcomes are reconciled before retry.
11. A renderer/cache failure cannot erase or misreport a successful formal commit.
12. Keyboard-only operation covers all primary workflows and diagnostics are not color-only.
13. Windows and macOS produce equivalent semantic outcomes and pass the same scenario suite.
14. The UI never advertises an operation as available unless its adopted descriptor/Command and formal write path support it.

## 11. Figma handoff package

The following frames should be prototyped as one coherent desktop application, using the shared editor frame and realistic domain data:

1. World Map — selected Building with “Open Interior” affordance.
2. Building Interior — Level plan, room selection and Inspector.
3. Building Interior — Door creation with two endpoint selection and review.
4. Cave — survey plan with uncertain Passage candidate and evidence panel.
5. Spatial Site — multi-building Academy composition and nested Groups.
6. Template library — Residence / Underground / Cave / Academy / War Academy categories with extensible module metadata.
7. Template instantiation — dependency-closed preview, blockers and partial-preview labeling.
8. Operation tray — before/after diff, validation diagnostics and Apply/Cancel.
9. Connection Inspector — typed endpoints, anchors, direction and traversal semantics.
10. Failure/recovery — stale evidence, blocked operation, unknown outcome and derived-cache rebuild states.

The Figma design must not invent controls for unsupported formal capabilities. Unsupported actions should be represented as disabled/explained states or clearly marked future concepts.

## 12. Out of scope and open gates

- Collaborative cursors, multi-user locks and conflict-resolution UI.
- Arbitrary user plug-ins or scripting as a V1 tool system.
- Full 3D interior editing.
- Generic universal geometry CRUD or arbitrary patch APIs.
- Formal Geometry split/merge before identity lineage and topology semantics are adopted.
- New Core Domain descriptors, relationship endpoint pairs, Commands, persistence tables or APIs.
- Assuming that a proposed Building/Cave/Road/Portal capability is formally enabled merely because its UX is designed.
- Final toolbar ordering, iconography, shortcut assignments and visual design tokens; these belong to the detailed interaction/Figma implementation pass.

## 13. Relationship to ME-08-06

ME-08-07 consumes the unified operation lifecycle, operation families, coordinate isolation, formal write routing, validation outcomes, risk/review policy and recovery rules from ME-08-06. It does not redefine them.

If a scenario appears to require a new operation or write path, the gap must be recorded for the owning Core Domain / ME-05 / ME-06 / specialist workstream. ME-08-07 cannot create a new formal capability through UX specification.

## 14. Completion status

ME-08-07 V0.1 establishes the UX working baseline and scenario/acceptance matrix. It is ready for Figma prototyping and cross-architecture review. Design completion does not mean that the controls, descriptors, Commands, persistence, or Windows/macOS runtime behavior are implemented or verified.
