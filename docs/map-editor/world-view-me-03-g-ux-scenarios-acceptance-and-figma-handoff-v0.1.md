# World View — ME-03-G UX Scenarios, Acceptance & Figma Handoff

- Version: 0.1
- Date: 2026-11-21
- Status: **Urban Geography UX Working Baseline** — workflows and interaction semantics recommended; no Core Domain descriptor, relationship endpoint, schema, calculation algorithm or implementation change is authorized
- Parent: ME-03 Settlements & Urban Geography Editor V0.2
- Depends on: ME-03-A through ME-03-F; ME-01-01 through ME-01-06; ME-02 natural-geography contracts; ME-05-02 through ME-05-05; Core Domain P2-09-26 and P2-09-27
- Scope: Creator journeys, shared Canvas/Inspector behavior, Candidate review, diagnostics, multi-scale interaction, error/authorization semantics, acceptance and Figma handoff

## 1. Purpose and disposition

ME-03-G translates the specialist domain contracts in ME-03-A through ME-03-F into a coherent creator-facing experience. It defines how a creator authors settlements, political territories and road networks; understands spatial evidence; and reviews AI-assisted proposals before any formal map change.

The product remains **one unified Map Editor** with professional editing modes. Urban Geography is a mode within the shared editor, not a separate GIS application, map file, selection system, geometry authority or history system.

This is a UX working baseline, not a frozen Avalonia specification, executable tool API, Core Domain descriptor change or implementation authorization.

### 1.1 UX outcomes

A creator should be able to:
1. Place and refine settlements without understanding GIS terminology.
2. Understand why a location is plausible, risky, uncertain or unsupported.
3. Create and inspect roads while explicitly confirming physical network continuity.
4. Draw political territory without implying sovereignty, control or administrative hierarchy.
5. Explore proposed land-use areas without being told that an unsupported formal Object Type is enabled.
6. Move between World, Region, Locality and Site scales without creating duplicate semantic Objects.
7. Ask AI to analyze or propose changes, inspect provenance and preview exact impact before formal application.
8. Recover safely from stale evidence, invalid geometry, failed publication or authorization rejection.

### 1.2 Governing interaction principles

- Canvas is the primary work surface; tools and panels support it.
- Use creator language: “place”, “boundary”, “connected road”, “nearby water”, “possible access”, “needs review”. GIS terminology is not a prerequisite.
- Distinguish creator assertion, formal fact, derived evidence, AI inference and unknown information.
- Geometry coincidence, overlap, proximity or snapping never silently creates a formal semantic relationship.
- No gesture silently changes Map Extent, Working Scope, spatial reference or formal history context.
- Candidate review, authorization and formal application are separate stages.
- The UI must never imply that an unavailable Core Domain capability can be formally saved.

## 2. Shared editor shell and information architecture

### 2.1 Desktop composition

Use the shared ME-01-06 shell:

| Region | Responsibility | Urban-mode content |
|---|---|---|
| Left navigation rail | Shared mode navigation | Urban Geography mode and other shared editor modes |
| Left tool/layer panel | Active tool, layer visibility, object browsing | Settlements, Political Regions, Roads; proposal-only Functional Areas |
| Central Canvas | Primary spatial authoring and navigation | Map, selection, previews, validation overlays, endpoint handles |
| Right Contextual Inspector | Selected Object, geometry, diagnostics and actions | Identity, geometry roles, findings, relationship evidence, Candidate review |
| Bottom AI conversation | Persistent natural-language entry point | Analysis, explanation and proposal requests |
| Shared status area | Current scope and operation feedback | World/Region/Locality/Site, active edit session, stale state |

The Canvas remains dominant. The Inspector scrolls independently. AI conversation may expand for longer reviews but must not permanently displace the Canvas.

### 2.2 Left panel structure

- **Objects:** Settlements; Political Regions; Roads & Paths.
- **Analysis & Proposals:** Placement findings; possible-access observations; Functional Area proposals (explicitly not a formal enabled Object layer).
- **Layers:** Shared layer visibility and presentation controls.
- **Active Tool:** Tool options and concise contextual help.

Do not create a separate Urban database, Road Network Manager application or GIS layer manager.

### 2.3 Inspector context priority

1. Selected formal Object or geometry component.
2. Active tool configuration when nothing is selected.
3. Candidate review when a proposal is selected.
4. Diagnostic detail when a finding is selected.
5. General help only when no more specific context exists.

Keep the current spatial task in focus; do not show unrelated full Object forms simultaneously.

## 3. Interaction state model

### 3.1 Three editing layers

| Layer | Meaning | Authority |
|---|---|---|
| Editor transient state | Pointer drag, vertex preview, snap guide, temporary measurement, uncommitted gesture | Editor-owned and disposable; not a Core Domain fact |
| AI Candidate | Proposed Object/geometry/field/relationship operation with provenance, validation and version/hash | AI Assistant staging; not formal map state |
| Formal Core Domain state | Accepted Object definition, geometry, relationship or history change | Core Domain authority |

A preview must communicate which layer it belongs to. Transient edits must not look like accepted map facts.

### 3.2 Candidate lifecycle

The UI represents these stages independently:

1. **Proposed** — AI or a creator workflow prepared a Candidate.
2. **Reviewed** — the user inspected proposed changes and evidence.
3. **Accepted / Rejected / Needs changes** — a decision about Candidate content.
4. **Authorized** — permission/confirmation granted for the exact Candidate version/hash, target Map/Branch/time context and operation.
5. **Applying** — Core Domain validation and formal operation are in progress.
6. **Applied / Failed / Stale** — authoritative result reconciled and shown.

Accepting a Candidate is not the same as authorizing or applying it. A single visible confirmation may combine interactions only if the underlying stages, exact target and Candidate hash remain separately recorded.

### 3.3 Undo, redo and history

- Local Undo/Redo applies only to the current editor gesture/edit session and transient geometry changes.
- Formal map history changes use explicit Core Domain history operations and are labelled as such.
- Ctrl+Z must not imply rollback of an already applied formal Command.
- Cancelling an AI task or closing a Candidate does not undo a previously applied formal change.
- If a formal history operation is unavailable, do not offer a misleading undo affordance.

## 4. Creator journeys

### 4.1 Create a Settlement by direct placement

1. Choose **Settlement** from Objects or the quick-create tool.
2. Click the intended location on Canvas.
3. Show a transient anchor preview and open the Inspector.
4. Enter canonical name and settlement class: village, town, city, capital or other.
5. Optionally add an extent later; do not require an area before creation.
6. Run or inspect available placement checks.
7. Review hard constraints, soft preferences, derived evidence and unknowns.
8. Confirm the target operation context (Map Initial State or applicable history context) when required.
9. Apply through the registered typed Core Domain operation.
10. Show the created Object and stable identity; keep it selected.

**Acceptance:** anchor-only Settlement is valid; extent is optional and independently editable; multi-scale representations do not create duplicate identities; unsupported formal operations retain a draft/Candidate and never claim success.

### 4.2 Create a Settlement with AI assistance

Example request: “在北部河谷、靠近主要河流但避开陡坡的位置，规划一座中型城镇。”

1. AI interprets the request into explicit creator intent and constraints.
2. System identifies the relevant map scope and available natural-geography evidence.
3. It proposes one or more candidate anchors; optional extents are visually distinguished from required anchors.
4. Each proposal shows source evidence, assumptions, freshness and uncertainty.
5. User compares candidates on the same Canvas and selects one for review.
6. Inspector explains why each location passed, warned, was blocked or remains unknown.
7. User may edit the location or constraints; this creates a new Candidate version.
8. User reviews the exact proposed change and target context before authorization/application.

**Acceptance:** AI never writes directly to Core Domain; Candidate locations are not formal Settlements; river proximity does not prove potable water, year-round supply, navigability or irrigation; a new Candidate version invalidates authorization bound to the previous version/hash.

### 4.3 Edit Settlement anchor or extent

1. Select a Settlement.
2. Inspector identifies the required anchor and optional extent separately.
3. Choose **Move anchor** or **Edit extent**.
4. Show transient handles and geometry preview.
5. Revalidate dependent findings and mark old evidence stale when source geometry changes.
6. Preview the exact formal change and target context.
7. Apply using the supported typed operation.

**Acceptance:** moving anchor does not silently alter extent; editing extent does not change Object identity; geometry preview does not silently change name/classification; failed validation leaves previous formal state intact.

### 4.4 Review placement findings

Use a compact, explainable finding list:

- **Blocked:** valid, fresh and supported deterministic hard constraint violated.
- **Warning:** soft preference or meaningful risk deserves attention.
- **Pass:** supported check completed and passed.
- **Unknown:** evidence missing, incomplete or outside coverage.
- **Unsupported:** system cannot evaluate with available capability.
- **Acknowledged exception:** creator intentionally accepts a warning where policy permits; underlying evidence remains unchanged.

Every finding shows plain-language statement, outcome, evidence source/revision, affected feature/area, explanation/limitation, freshness and available action. Do not collapse findings into one opaque suitability score.

### 4.5 Create roads and confirm connectivity

1. Choose **Road** and draw a centerline.
2. Enter optional canonical name and road class.
3. Preview endpoint snap candidates and nearby crossings.
4. At each intended junction, explicitly confirm which Road segment endpoints form a physical connection.
5. At-grade crossings require authored/split segment endpoints at the intended connection.
6. Grade-separated crossings display as crossings without connection by default.
7. Review the network change preview, affected Road segments and explicit relationships.
8. Apply only if typed operations can represent the endpoint evidence.

**Acceptance:** snapping is a preview aid, not a formal connection; line crossing does not create network connectivity; a connection is Road-to-Road physical continuity, not legal access, direction or traffic rules. If the current relationship contract cannot persist which endpoints connect, disable formal connection application and explain the capability gap. Never hide endpoint data in opaque metadata. Do not offer formal road split/merge before identity-lineage and typed-operation contracts are adopted.

### 4.6 Inspect possible Settlement access

1. Select a Settlement and open **Nearby roads** / **Possible access**.
2. Show nearby Road geometry, supported distance and available connected-component evidence.
3. Label results “nearby road” or “possible access”, never confirmed formal access.
4. Explain that the current registry has no Road-to-Settlement access relationship.

**Acceptance:** no Road-to-Settlement relationship is created; proximity does not prove physical access, public right of way, navigability or route availability; unsupported routing returns Unsupported.

### 4.7 Draw and edit a Political Region

1. Choose **Political Region** and draw a boundary.
2. Enter canonical name and region class: country, province, state, district or other.
3. Inspector distinguishes territory boundary from political actor and control facts.
4. Show geometric adjacency candidates and Settlement containment observations.
5. User explicitly confirms supported PoliticalRegion adjacency or PoliticalRegion-to-Settlement containment where registry permits.
6. Review and apply boundary and relationship changes separately.

**Acceptance:** boundary does not imply sovereignty, control, ownership or jurisdiction; adjacency is a candidate until explicitly confirmed; point-touch is not silently treated as area adjacency; containment does not create administrative hierarchy. Do not offer PoliticalRegion-to-PoliticalRegion contains/part_of.

### 4.8 Explore Functional Area proposals

1. User asks AI to identify or draw a residential, industrial, agricultural or other functional area.
2. Editor creates a proposal overlay with proposed classification and basis.
3. Inspector explains that FunctionalArea is a recommended concept but is not an enabled Core Domain Object Type.
4. User may revise, compare or retain it in the proposal/knowledge workflow.
5. Do not expose “Save as formal Functional Area” until the owning descriptor and persistence contracts are adopted.

**Acceptance:** proposal is not misrepresented as terrain_region; actual, designated and proposed are not conflated; no formal Object or relationship is written using an unregistered type.

### 4.9 Navigate across scales

Use the shared World / Region / Locality / Site model.

1. User navigates into a Locality.
2. The same Settlement identity remains selected when represented.
3. Detailed view may reveal extent, roads and context without duplicating the Settlement.
4. Scale changes Canvas presentation/LOD and working context only.
5. Explicit Working Scope changes are visible; Map Extent is not silently changed.

**Acceptance:** scale navigation is not Object creation; one Settlement identity may have multiple scale-appropriate presentations; out-of-scope Objects are hidden/simplified, not deleted; viewport navigation never changes formal geometry.

## 5. Canvas and Inspector behavior

### 5.1 Canvas visual grammar

- Formal Objects: normal map styling and stable selection outline.
- Editor transient geometry: dashed/ghosted preview with active handles.
- AI Candidates: distinct proposal outline/fill and explicit Candidate label.
- Derived evidence: subordinate overlays with source/legend access.
- Stale evidence: warning label and reduced visual emphasis; never presented as current.
- Unknown/unsupported areas: neutral hatching or no-data overlay, not a red failure surface.
- Confirmed network connections: visible endpoint/junction marker; nearby unconnected crossings remain distinct.

Do not encode meaning by color alone; pair color with icon, line pattern, label or accessible text.

### 5.2 Inspector sections

**Settlement:** Identity (name, class, stable reference); Location (creator-facing map location; raw coordinates only in advanced diagnostics); optional Extent; Placement findings; Nearby context (terrain, water, climate, roads with evidence labels); supported History; AI explanation/provenance.

**Road:** identity/classification, centerline, endpoint handles, confirmed connections, nearby unconnected crossings and capability limitations.

**Political Region:** identity/classification, boundary, supported adjacency/containment relationships, and explicit note that territory is not control.

**Functional Area proposal:** proposal-only state, classification/basis, provenance and unavailable formal-save explanation.

## 6. AI conversation and proposal review

### 6.1 Conversation behavior

The persistent AI entry point accepts natural-language requests. Ask a follow-up only when essential creator intent is ambiguous; otherwise make bounded assumptions visible and present a reviewable proposal.

Examples:
- “在这条河的北岸找一个适合小镇的位置。”
- “把这条道路延伸到东侧村庄附近，但不要自动连接。”
- “显示与这个城镇相邻的道路，并说明哪些只是距离较近。”
- “解释为什么这个地点被标记为未知，而不是不适合。”

### 6.2 Proposal review layout

Show concise title/intent; target Map/Branch/historical context; proposed operations and affected Objects; before/after Canvas preview; validation and freshness; assumptions/unsupported questions; Candidate version/hash and authorization status.

Actions:
- **Accept proposal**
- **Request changes**
- **Reject**
- **Authorize & Apply** — separate when required by policy.

“Accept proposal” means the content is accepted for progression; it does not claim formal map state has changed.

### 6.3 AI explanation rules

AI may explain evidence and propose alternatives. It must not invent terrain/water/climate/population/road facts; turn unknown into pass/fail; infer connectivity from geometric crossing; infer Road access from proximity; infer political control from a boundary; infer administrative hierarchy from containment; claim Functional Area was formally saved; or claim success before Core Domain confirms it.

## 7. Freshness, errors and recovery

### 7.1 Stale evidence

A finding is stale when required source geometry/field revision, calculation artifact, algorithm version, spatial reference or query snapshot changes.

- Mark stale immediately when invalidation is known.
- Explain which source changed.
- Offer **Re-check** only where the calculation/query is supported.
- Do not use stale findings to approve/block a new formal operation as if current.
- Preserve old results for provenance, labelled historical evidence.

Settlement geometry edits invalidate dependent findings. Source Field Revision may invalidate natural-geography findings under ME-02/ME-05 freshness rules. Revalidation is bounded to affected scope.

### 7.2 Error and recovery states

| Condition | UI response | Formal-state guarantee |
|---|---|---|
| Invalid geometry | Explain defect; highlight segment/ring; retain preview | Invalid geometry not applied |
| Unsupported predicate/transform | Show Unsupported and reason | No guessed result or silent conversion |
| Indeterminate numerical result | Show uncertainty and diagnostic | No false pass/fail |
| Stale Candidate | Freeze Apply; offer refresh/rebuild | Old authorization not reused |
| Authorization denied/expired | Explain permission requirement; preserve Candidate | No formal write |
| Asset publication failure | Show retry/recovery; retain previous reference | No partial formal publication |
| Concurrent/stale target revision | Reject stale apply; reload/rebase preview | No lost update |
| Apply timeout/unknown response | Reconcile by operation/idempotency identity before retry | No duplicate formal operation |
| AI task cancelled | Stop staging; preserve prior formal changes | No implicit rollback |
| Unsupported formal operation | Retain draft/Candidate and explain gap | No fake success |

Boundary replacement, deleting a formal Object, changing a confirmed connection or applying a history-local change shows affected Objects and target context before confirmation. Use a second confirmation only for materially destructive actions or where authorization policy requires it.

## 8. Interaction matrix

| Context | Primary action | Preview | Validation | Confirmation / apply |
|---|---|---|---|---|
| Empty Canvas, Settlement tool | Place anchor | Ghost anchor | Placement checks | Typed Object creation |
| Selected Settlement | Move anchor / edit extent | Geometry handles | Revalidate dependent findings | Typed geometry operation |
| AI placement Candidate | Compare / edit | Candidate overlay | Evidence, uncertainty, freshness | Accept, authorize, apply |
| Road drawing | Draw centerline | Snap guides and crossings | Geometry and endpoint evidence | Create Road |
| Road endpoint pair | Confirm connection | Endpoint-pair highlight | Registry endpoint capability | Supported relationship only |
| Road near Settlement | Inspect possible access | Nearby-road highlight | Distance/component evidence | Read-only; no access relation |
| PoliticalRegion boundary | Draw/edit polygon | Boundary preview | Valid geometry and relation candidates | Boundary and supported relations separately |
| Functional Area proposal | Review | Proposal overlay | Proposal-only classification/basis | No formal save before adoption |
| Any Object at new scale | Navigate | LOD transition | Scope/visibility feedback | No domain write |

## 9. Keyboard, pointer and accessibility

Exact Avalonia key bindings remain implementation work. Recommended semantics:

- Escape cancels active transient gesture or closes a non-applied preview; it does not reverse a formal Command.
- Enter confirms a focused action only when explicit and safe.
- Delete/Backspace opens review/confirmation; it never immediately deletes a formal Object.
- Standard zoom/pan gestures are shared across modes.
- Selection, geometry editing and relationship confirmation must not rely solely on color.
- Provide keyboard focus order, visible focus states, accessible names for icon-only controls and text alternatives for status symbols.
- Do not assign shortcuts to formal destructive operations without explicit confirmation.

Exact shortcut mapping, gesture arbitration and platform conventions remain ME-06/implementation work.

## 10. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| UX-URB-001 | Create Settlement with anchor only | Valid; extent optional |
| UX-URB-002 | Add extent later | Same Object identity |
| UX-URB-003 | AI proposes three locations | Comparable Candidates with evidence and assumptions |
| UX-URB-004 | Fresh supported hard constraint violated | Blocked with source and explanation |
| UX-URB-005 | Water nearby but supply unknown | Unknown/limited; no potable/year-round claim |
| UX-URB-006 | Soft preference not met | Warning, not automatic block |
| UX-URB-007 | Candidate edited after acceptance | New version; prior authorization invalid |
| UX-URB-008 | Candidate applied | Success only after Core Domain confirmation |
| UX-URB-009 | Road lines cross | No connection without explicit confirmation |
| UX-URB-010 | Endpoint snap suggested | Preview only |
| UX-URB-011 | Relationship API lacks endpoint identity | Disable apply and explain gap |
| UX-URB-012 | Road near Settlement | Possible access only; no formal relationship |
| UX-URB-013 | PoliticalRegion polygons share edge | Adjacency candidate; explicit confirmation |
| UX-URB-014 | PoliticalRegion contains Settlement | Spatial containment only; no jurisdiction claim |
| UX-URB-015 | User requests administrative hierarchy | Explain unsupported semantics; do not infer |
| UX-URB-016 | User requests Functional Area | Proposal-only; no formal Object save |
| UX-URB-017 | Navigate World to Locality | Same Settlement identity |
| UX-URB-018 | Map Extent differs from Working Scope | Scope navigation does not mutate Map Extent |
| UX-URB-019 | Source Field Revision changes | Dependent findings stale; bounded revalidation |
| UX-URB-020 | Candidate target revision changes | Stale apply rejected; preview refreshed |
| UX-URB-021 | Authorization expires | Apply disabled; reauthorization required |
| UX-URB-022 | Asset publication fails | Previous formal Asset remains authoritative |
| UX-URB-023 | Apply response ambiguous | Reconcile before retry; no duplicate |
| UX-URB-024 | AI task cancelled after formal apply | Prior formal change remains |
| UX-URB-025 | Escape during geometry drag | Only transient gesture cancelled |
| UX-URB-026 | Delete pressed on selected Object | Review/confirmation; no immediate write |
| UX-URB-027 | Finding Indeterminate/Unsupported | No misleading pass/fail |
| UX-URB-028 | Boundary edited near another boundary | No silent adjacency create/delete |
| UX-URB-029 | Grade-separated roads cross | Visible crossing, disconnected by default |
| UX-URB-030 | AI explains placement result | Claims traceable to sources, assumptions and freshness |

## 11. Figma handoff

### 11.1 Required frames

Create a desktop set for a 1440 × 900 reference viewport:

1. Urban Geography overview/empty state: shared shell, Objects/layers, dominant Canvas, empty Inspector, persistent AI input.
2. Settlement selected: required anchor and optional extent distinguished.
3. Placement analysis: Canvas evidence overlays and Inspector findings by outcome with source/freshness.
4. AI Candidate comparison: multiple candidate anchors, one selected, provenance and assumptions.
5. Candidate review & apply: before/after preview, operations, target context, authorization/apply distinction.
6. Road authoring: centerline, endpoint handles, snap preview, connected vs unconnected crossings.
7. Political Region authoring: boundary, adjacency/containment candidates, territory-vs-control explanation.
8. Functional Area proposal-only: proposal label and disabled/explained formal-save capability.
9. Stale/error state: stale findings, re-check, Unsupported/Indeterminate diagnostics and recovery.
10. Multi-scale navigation: same Settlement identity at World/Region/Locality scales.

### 11.2 Visual direction

- Professional desktop authoring tool; restrained, precise and calm.
- Dense but legible information hierarchy; avoid oversized cards and consumer-app styling.
- Neutral surfaces, subtle separators, consistent spacing and clear typography.
- Canvas receives most horizontal space; Inspector is compact, contextual and collapsible.
- Use status text plus icon/pattern; never color alone.
- Avoid decorative maps, fake dashboards, KPI tiles, excessive gradients, glow effects and floating-panel clutter.
- Use a coherent fictional sample region: river valley, town, several roads and two political boundaries. Illustrative, not real-world geography.
- Show realistic handles, endpoint markers, Candidate outlines, stale states, provenance and confirmation controls.

### 11.3 Figma AI master prompt

Use this as the primary generation brief:

    Design a high-fidelity desktop UI for “World View”, a local-first fictional-world map creation application. Create the “Urban Geography” professional editing mode inside ONE unified Map Editor. Do NOT design a separate GIS application.

    REFERENCE VIEWPORT
    - Desktop 1440 × 900.
    - Professional, restrained, information-dense but readable.
    - Main Canvas is the dominant area.
    - Three-part workbench: left navigation/tools/layers, central map Canvas, right contextual Inspector.
    - Persistent AI conversation input docked along the bottom of the work area.

    PRODUCT CONTEXT
    Creators build fictional worlds and do not need to understand GIS terminology. Use plain creator language. The editor shares one map, one object model, one selection model and one history system across professional modes.

    LAYOUT
    1. Left narrow navigation rail for shared editor modes.
    2. Left panel with Objects (Settlements, Political Regions, Roads & Paths), Analysis & Proposals, Layers and Active Tool.
    3. Large central Canvas with a fictional river valley, terrain context, settlements, roads and political boundaries.
    4. Right contextual Inspector that changes with the selected object or active proposal.
    5. Bottom AI conversation input with placeholder: “Ask AI to analyze or propose a map change…”.
    6. Compact status strip showing World / Region / Locality / Site scale, active edit context and operation feedback.

    CREATE THESE FRAMES
    A. Urban Geography overview with no selection.
    B. Selected town: required anchor and optional extent shown as separate concepts.
    C. Placement analysis: Blocked, Warning, Pass, Unknown and Unsupported findings as distinct text-labelled states; include evidence source and freshness.
    D. AI Candidate comparison: three candidate town locations on Canvas, one selected, with provenance and assumptions.
    E. Candidate review: before/after preview, proposed operations, target context, and separate “Accept proposal” versus “Authorize & Apply” actions.
    F. Road authoring: centerline, endpoint handles, snap preview, explicit connected junction versus unconnected crossing.
    G. Political Region editing: boundary, adjacency candidate and settlement containment observation. Include: “A territory boundary does not establish political control.”
    H. Functional Area proposal-only view: proposed residential or industrial area labelled “Proposal — not a formal map object”. Do not show an enabled formal Save action.
    I. Stale evidence and recovery: source change, stale findings, re-check action, Unsupported/Indeterminate diagnostic.
    J. Multi-scale navigation: same settlement identity at World, Region and Locality scales without duplication.

    DOMAIN RULES TO COMMUNICATE VISUALLY
    - A Settlement has one required anchor and an optional extent.
    - Settlement identity remains stable across map scales.
    - Snapping, proximity, line crossing and polygon overlap do not automatically create semantic relationships.
    - Road connectivity is explicitly confirmed between Road segment endpoints. A nearby road is only “possible access”, not a formal Settlement access relationship.
    - PoliticalRegion is a territory, not a political actor; boundary does not imply sovereignty, ownership, jurisdiction or control.
    - Administrative parent/child hierarchy is unavailable in this version.
    - Functional Area is proposal-only because its formal Object Type is not enabled.
    - AI suggestions are Candidates, not formal map facts.
    - Candidate acceptance is separate from authorization and formal application.
    - Stale evidence is visibly marked and cannot be treated as current.
    - Unknown and Unsupported must not look like failure or success.

    VISUAL SYSTEM
    - Calm neutral surfaces, subtle dividers, restrained accent color.
    - Clear typography and compact controls.
    - Canvas roughly 60–65% of workbench width; left panel 18–20%; Inspector 22–25%.
    - Icon plus text for status; never rely on color alone.
    - Reusable tool rows, object list items, Inspector sections, finding rows, provenance rows, Candidate cards and confirmation footer.
    - Make selected, hover, disabled, stale and destructive-confirmation states explicit.
    - Consistent spacing grid.

    DO NOT
    - Do not make GIS terminology the primary UI language.
    - Do not invent Road-to-Settlement access.
    - Do not imply geometric intersections create road connections.
    - Do not imply political boundaries establish control or hierarchy.
    - Do not present Functional Areas as formally saveable Objects.
    - Do not merge Candidate acceptance, authorization and application into one ambiguous success state.
    - Do not create a separate Urban GIS app, second map, KPI dashboard or enterprise administration interface.

    Deliver polished, consistent frames with realistic fictional map content and reusable component patterns. Prioritize interaction clarity and correct domain semantics over decoration.

### 11.4 Figma review checklist

- [ ] Frames use the shared Map Editor shell, not a parallel application.
- [ ] Canvas remains dominant and AI input accessible.
- [ ] Settlement anchor and optional extent are visually distinct.
- [ ] Candidate, transient preview and formal Object are distinguishable.
- [ ] Finding outcomes are not collapsed into one score.
- [ ] Road crossing and confirmed connectivity look different.
- [ ] Possible access is not shown as a formal relationship.
- [ ] Political territory is not labelled as control/ownership.
- [ ] Functional Area is clearly proposal-only.
- [ ] Stale evidence and unsupported capability are explicit.
- [ ] Multi-scale views preserve Object identity.
- [ ] Status remains understandable without color alone.

## 12. Implementation handoff boundaries

ME-03-G defines product and interaction behavior only. It does not authorize:
- Core Domain Object/Relationship descriptor changes or schema migrations.
- Formal Road endpoint relationship payload changes.
- FunctionalArea Object Type adoption.
- Road split/merge identity-lineage operations.
- Road-to-Settlement access semantics.
- Administrative hierarchy or political actor/control semantics.
- Geometry engine, renderer, projection or spatial library selection.
- Exact Avalonia control implementation, pointer bindings or keyboard shortcut map.
- Physical Asset encoding or performance claims.

These topics remain with their owning architecture workstreams and ME-03-H integration review.

## 13. ME-03-G disposition and next work

ME-03-G establishes the recommended UX working baseline and Figma handoff for ME-03-A through ME-03-F. It does not authorize Core Domain or implementation changes.

ME-03-A through ME-03-G are complete as specialist design working baselines after this document is recorded in the work plan. Next is **ME-03-H — Cross-architecture Integration Review**, reconciling urban UX with Core Domain, ME-01, ME-02, ME-05, Map Engine and AI Assistant contracts and defining explicit implementation gates.

## References

- ME-03-00 — Settlements & Urban Geography Editor V0.2.
- ME-03-A — Domain Inventory & Descriptor Reconciliation V0.1.
- ME-03-B — Settlement Model & Authoring Workflows V0.1.
- ME-03-C — Political & Administrative Geography V0.1.
- ME-03-D — Land Use & Functional Areas V0.1.
- ME-03-E — Road & Path Network Authoring V0.1.
- ME-03-F — Settlement Placement & Urban Spatial Validation V0.1.
- ME-01-01 — Editor Overall Architecture V0.1.
- ME-01-04 — Multi-scale Spatial Model & Global Consistency V0.1.
- ME-01-06 — Editor Interaction Framework V0.1.
- ME-05-03 — Topology & Spatial Predicate Contract V0.1.
- ME-05-04 — Coordinate Reference, Precision & Measurement Contract V0.1.
- ME-05-05 — Geometry & Field Asset Package and Publication Contract.
- ME-02-D / ME-02-E / ME-02-F / ME-02-G — Hydrology, climate/ecology, calculation and UX contracts.
- Core Domain P2-09-26 V0.2 — Object Type Descriptor Registry.
- Core Domain P2-09-27 V0.2 — Relationship Type Descriptor Registry.
