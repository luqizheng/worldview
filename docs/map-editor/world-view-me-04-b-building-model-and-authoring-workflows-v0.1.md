# World View — ME-04-B Building Model & Authoring Workflows

- Version: 0.1
- Date: 2026-11-21
- Status: Building domain and authoring working baseline
- Depends on: ME-04-00, ME-04-A, ME-01 shared editor architecture, ME-03 settlement workflows, ME-05 geometry/asset contracts and Core Domain descriptor/command contracts

## 1. Executive decision

A **Building is a first-class semantic Object** with one stable identity. It is not a footprint layer, a CAD document, a floor-plan file, or a collection of unrelated geometry records.

The same Building identity is inspected and edited through coordinated views: exterior/site view; footprint and building-outline view; interior/floor-plan view; and optional derived exterior massing or visual presentation.

Changing the active view does not create another Building, change its identity, or create a new Map. Formal geometry remains owned by the Building semantic Object through typed Geometry Components and explicit coordinate contexts.

The current Core Domain V1 registry has no Building descriptor. This document defines the target product/domain contract, **not an enabled or implemented Object Type**. Formal creation and editing remain blocked until Core Domain adopts the descriptor, component roles, authority paths and typed commands.

## 2. Why Building is a semantic Object

A Building has meaning beyond its visible outline: it can be named and classified; referenced by stories, events, places and other world knowledge; retain identity when footprint or appearance changes; contain floors, rooms, entrances and connections; and be present, absent, damaged, rebuilt, relocated or repurposed in different Map/Branch histories.

A footprint alone cannot carry these meanings. Conversely, not every line or polygon used to depict a building deserves an independent Object identity.

**Identity rule:** geometry edits, footprint refinement, view switching, floor-plan revisions and derived presentation changes do not silently replace Building identity. Identity changes only through an explicit domain lifecycle operation.

## 3. Building identity and ownership scope

### 3.1 Identity

The proposed Building descriptor must support:
- stable Object ID allocated through the Core Domain identity reservation/binding service;
- canonical name as a definition field, subject to the shared Object naming contract;
- optional creator-facing building class/use classification;
- an explicit ownership scope consistent with the existing Project-shared versus Map-local Object model;
- a lifecycle/presence model independent from geometry.

The editor must never generate durable identity from a display label, geometry hash, floor-plan filename or renderer node.

### 3.2 Project-shared or Map-local

ME-04 does not invent a third ownership scope.

- **Project-shared Building:** use only when multiple Maps intentionally refer to the same semantic Building definition/identity. A shared definition correction affects all Maps that use that shared Object, according to existing shared-definition semantics.
- **Map-local Building:** use when the Building is meaningful only within one Map's world representation. Its definition is not silently shared with other Maps.

The creator-facing default should be Map-local unless the product's existing Object creation context explicitly establishes a shared Object workflow. This is a product default recommendation, not a new Core Domain ownership rule. The UI should explain the effect in creator language, not expose raw database scope fields.

Do not automatically share Buildings merely because two Maps show the same coordinates or reuse the same geometry Asset. Content deduplication is not identity sharing.

## 4. Building definition versus geometry versus presentation

| Layer | Examples | Authority |
|---|---|---|
| Semantic identity | Object ID, stable identity, ownership scope | Core Domain |
| Definition | Canonical name, building class/use, creator-authored descriptive fields | Core Domain definition service |
| Formal spatial geometry | Footprint, approved exterior outline, supported building-owned plan components | Core Domain Geometry Component references and immutable Assets |
| Map/History presence | Whether Building exists in a Map starting state or selected later History | Map Initial State / History Commands |
| Derived geometry | Simplified outline, triangulated/massing preview, display LOD | Map Engine derived artifact/cache |
| Presentation | Color, icon, labels, selected floor, visibility, active editing view | Editor/presentation state |
| Narrative/world knowledge | Ownership, occupants, story significance, historical descriptions not part of spatial definition | World Knowledge / narrative model |

A renderer mesh, symbol or floor-plan thumbnail is never authoritative Building geometry.

## 5. Proposed Building geometry component roles

The descriptor proposal should start with a small role vocabulary. These are proposed role names, **not current registry-enabled roles**.

| Proposed role | Geometry / purpose | V1 recommendation |
|---|---|---|
| footprint | 2D Polygon/MultiPolygon of Building ground-plan extent | Primary exterior geometry; optional only for a not-yet-sited conceptual Building |
| exterior_outline | 2D LineString/MultiLineString where an explicit outline is useful independently of fill | Optional; avoid storing if derivable from footprint boundary |
| interior_plan | Building-local 2D plan geometry for an interior level/plan representation | Defer ownership granularity to ME-04-C/D; must have explicit level/plan identity and local coordinate context |
| entrance | Entrance location/geometry where it is a Building component rather than independently referable semantic Object | Candidate for ME-04-E; not enabled now |
| exterior_detail | Decorative or display-only detail | Presentation/derived by default, not formal Core Asset |

Rules:
1. A footprint is a 2D horizontal projection, not a 3D solid or complete wall/roof description.
2. Polygon/MultiPolygon support is conditional on explicit descriptor permission.
3. Separate footprint polygons do not imply separate Buildings. Courtyards, wings and detached annexes need explicit semantic policy in later design.
4. Do not persist both a polygon footprint and its exact boundary as separate authoritative geometry unless independently needed.
5. An absent footprint means “not yet spatially placed / not yet defined”, not “zero-sized Building”.
6. Geometry Component identity remains stable according to ME-05; replacing payload bytes creates a new immutable Asset revision, not a new Building identity.
7. Formal component add/remove/adjust/refine is allowed only where the Building descriptor and registered command contract explicitly enable that role and operation.

## 6. Building and Site / Settlement context

Site remains a Working Scope, not an automatically persisted parent Object.

A Building may be created while the user is working within a Site scope, but that does not create a formal Site Object or parent-child relationship. Likewise, a Building visually inside a Settlement extent does not create a formal Building-to-Settlement containment relationship.

V1 context behavior:
- show active Site/Locality/Settlement context as navigation and spatial evidence;
- allow the creator to place a Building footprint in Map coordinates through an explicit transform;
- show containment/overlap/proximity as spatial observations only;
- do not persist spatial.contains, structural.part_of or another generic relationship unless the exact endpoint pair and semantics are adopted by Core Domain;
- do not infer legal parcel, ownership, address, jurisdiction or access from geometry.

ME-04-H may recommend a Building→Settlement context relationship only if a real product need exists and its semantics differ from mere spatial containment. V1 should not add it merely to populate an object tree.

## 7. Exterior and interior editing views

### 7.1 One object, multiple coordinated views

The Editor presents the Building through one contextual Inspector and coordinated editing views. Exterior view selects and inspects the Building in the surrounding map. Footprint view draws/refines its 2D ground outline. Interior view enters the Building context and works on a selected floor/plan when ME-04-C/D capabilities exist. Presentation view previews supported derived exterior appearance; it is not authoritative 3D geometry.

The Inspector always identifies the same Building Object and indicates which view is active.

### 7.2 Local interior coordinate context

Interior plans should use an explicit Building-local 2D coordinate context, not pretend their coordinates are global Map coordinates.

The contract must define stable local frame identity and owner; origin and axis orientation; unit and precision profile; explicit transform to Map coordinates where placement is needed; transform version/fingerprint and accuracy; and behavior when Building footprint or Map placement changes.

The exact vertical/level binding is deferred to ME-04-C. The local frame must not be encoded as arbitrary JSON fields or silently derived from the current viewport.

### 7.3 Multiple plans and revisions

A Building may eventually have multiple floor plans, alternative plans or historical revisions. They must be explicitly distinguished by plan/level identity and time semantics. A filename, tab label or active Canvas view is not a sufficient identity.

ME-04-C/D will decide the minimal formal ownership model. ME-04-B does not assume a floor is already an Object or every Building has exactly one plan.

## 8. Authoring workflows

### 8.1 Create a Building

Creator workflow:
1. Start from the current Map and optional Site/Locality working context.
2. Choose “Create Building”.
3. Enter a name and optional class/use; Core Domain reserves and binds a stable Building identity.
4. Draw or import a footprint, or leave it unplaced if the workflow permits a conceptual Building.
5. Review spatial observations such as overlap with existing footprints or relation to nearby Settlements/Roads.
6. Choose the intended authority effect through creator-facing wording when necessary: starting state of this Map, or a change from a selected time in the active History.
7. Apply through the registered typed operation; reconcile the authoritative result before reporting success.

One user action may orchestrate several internal steps, but identity reservation/binding, definition, geometry Asset publication and Map presence are distinct authority operations. A failure after identity binding must be recoverable and must not create duplicate identity on retry.

### 8.2 Add a footprint to an existing Building

1. Resolve the selected stable Building identity.
2. Start a temporary geometry edit against the current scene/query generation.
3. Validate geometry family, coordinate context, precision and descriptor role.
4. Publish immutable geometry Asset and verify read-back.
5. Apply the registered typed geometry operation with compare-and-swap/revision guard.
6. Re-read authoritative component membership and reconcile the submission.
7. Invalidate dependent spatial observations and derived presentation.

A stale hit-test, stale selection or old component reference must be re-resolved before formal application.

### 8.3 Enter interior editing

1. Select a Building.
2. Open its Interior view.
3. If no supported interior plan/level exists, show an empty-state creation workflow rather than a fake floor.
4. Create/select a plan only when ME-04-C/D descriptor and coordinate-context requirements are satisfied.
5. Keep Building identity and context visible while editing.

Switching to the Interior view is not a formal domain mutation. Creating or revising formal plan geometry is.

### 8.4 Change Building definition

Name/class/use corrections follow Object ownership authority:
- Project-shared definition service for a Project-shared Building;
- Map-local definition service for a Map-local Building.

They are not History Commands unless the domain explicitly defines a particular field as temporal runtime state. The initial descriptor should keep name and broad classification as definition fields, not historical runtime attributes.

### 8.5 Historical change

A Building that appears, disappears, is rebuilt or changes geometry at a later time must use the selected Map/Branch History's registered typed Commands and supported temporal semantics.

The editor must not change shared definition to simulate a local historical event; rewrite Map Initial State to simulate a later event; apply a historical command to other Maps or unrelated Branches; or infer historical presence from geometry files.

### 8.6 Delete / remove

The UI must distinguish:
- remove from a selected History (temporal presence operation);
- remove from Map Initial State;
- retire/unbind/delete identity only through the Core Domain lifecycle contract;
- remove a geometry component while preserving Building identity;
- hide from the current view (presentation only).

A generic Delete button must not conflate these operations. The exact deletion/retirement command is a Core Domain adoption gate.

## 9. Spatial validation and evidence

Initial Building checks should remain small and explainable:
- invalid/empty/self-intersecting footprint where deterministic validation is supported;
- footprint overlaps another Building footprint;
- Building footprint lies partly/outside active Map extent;
- proximity to terrain/water/Settlement/Road context;
- transform unavailable or insufficiently accurate;
- stale geometry or source inputs.

Outcomes use explicit Pass / Warning / Blocked / Unknown / Unsupported / Acknowledged exception semantics as defined by shared validation contracts. An unimplemented check is Unsupported/Unknown, never a fabricated Pass.

Spatial overlap does not automatically mean two Buildings are the same or connected. Proximity to a Road does not establish access. A Building footprint inside a Settlement extent does not establish formal containment.

Evidence must bind to active Map/Branch/Time context, geometry revisions, coordinate reference and algorithm version. Freshness follows shared ME-03/ME-05 contracts.

## 10. AI assistance

AI may interpret a natural-language Building description; propose a footprint or classify an intended building type; identify inconsistencies between description, footprint and existing context; suggest an interior plan workflow; and explain spatial observations and uncertainty.

AI may not allocate Object IDs outside Core Domain; directly write formal Building definition or geometry tables; infer legal ownership, road access, structural safety or code compliance; create unsupported relationships; silently convert local plan coordinates to Map coordinates; or report formal application before submission reconciliation.

Simple low-risk manual footprint edits may use direct typed operations without a full Candidate. Complex AI-generated multi-component edits require immutable proposal, validation, risk-based authorization and submission evidence.

## 11. V1 scope and deferrals

| Capability | ME-04-B recommendation | Formal status |
|---|---|---|
| Building stable semantic identity | Required | Needs new Core Domain Object Type |
| Canonical name and broad building class/use | Small definition field set | Needs descriptor adoption |
| 2D ground footprint | Primary formal spatial component | Needs descriptor role and typed geometry command support |
| One Building, multiple coordinated editing views | Required UX | Editor behavior; no duplicate Object |
| Building-local 2D interior frame | Required contract for future interior plans | Coordinate/transform contract required |
| Site/Settlement context | Read-only spatial context initially | No new relationship assumed |
| Exterior 3D massing | Derived presentation only | No authoritative 3D solids in V1 |
| Independent wall/window/decoration Objects | Deferred | Avoid BIM-style object explosion |
| Multiple wings / detached annexes | Later semantic policy | Do not infer separate Building identities from polygon count |
| Building-to-Road access | Deferred to ME-04-E and Core Domain | No formal relation today |
| Full BIM/IFC, parametric construction model | Out of scope | Not V1 |

## 12. Core Domain adoption requirements

Before formal Building creation is enabled, Core Domain must adopt and test:
1. A building Object Type descriptor with stable ID and definition fields.
2. Explicit definition ownership scope and Object lifecycle/presence semantics.
3. A bounded geometry role set, at minimum a 2D footprint role with declared family, dimension and cardinality.
4. Supported coordinate context and precision profile.
5. Allowed geometry component commands and initial-state/history write routing.
6. Asset publication, read-back, CAS, idempotent reconciliation and stale-reference checks.
7. Versioned registry fixtures and cross-platform geometry validation.
8. Explicit rules for whether an unplaced conceptual Building is allowed and how it appears in queries.

No ME-04 document alone authorizes these changes.

## 13. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| B-01 | Create a Building in a Site working scope | One stable Building identity; Site remains context unless a formal relation is adopted |
| B-02 | Switch Exterior ↔ Interior view | Same Building identity and Inspector context |
| B-03 | Edit footprint | Geometry revision changes; Building identity remains stable |
| B-04 | Two Maps reuse identical footprint bytes | Asset dedup does not merge Building identities or ownership |
| B-05 | Shared Building definition changes | All Maps using that shared identity observe shared-definition semantics |
| B-06 | Map-local Building definition changes | Other Maps are not mutated |
| B-07 | Footprint lies inside Settlement extent | Spatial observation only; no unsupported relationship is written |
| B-08 | Building is created after time zero | Correct selected History-local presence command; no rewrite of Initial State |
| B-09 | Building is rebuilt at a later time | History-local geometry/state operation; other Maps/Branches unaffected |
| B-10 | Interior view is opened before any plan exists | Clear empty state; no phantom Floor or plan Object |
| B-11 | Footprint edit uses stale scene selection | Re-resolve identity/component/generation or reject stale operation |
| B-12 | Asset write succeeds but formal transaction fails | No formal reference; safe retry/reconciliation without duplicate Building |
| B-13 | User hides a Building | Presentation change only; no lifecycle mutation |
| B-14 | User removes Building from one History | Only selected History presence changes |
| B-15 | AI proposes a Building overlapping another | Candidate/diagnostic explains overlap; no automatic merge |
| B-16 | Building 3D preview is regenerated | Derived artifact changes; formal 2D geometry and identity unchanged |

## 14. Open decisions delegated to subsequent ME-04 work

- ME-04-C: floor/level identity, Building-local vertical frame, vertical offsets and relation to world vertical reference.
- ME-04-D: whether Rooms are independent Objects or a bounded Building/Floor substructure; plan ownership and room-boundary model.
- ME-04-E: entrance/opening identity and typed access/transition connections.
- ME-04-F: natural versus constructed underground semantic model and cross-site connections.
- ME-04-H: consolidated Core Domain descriptor/relationship/command proposal and implementation gates.

## 15. Final recommendation

Adopt the Building semantic Object and coordinated multi-view model as the ME-04 product/domain working baseline.

Keep the first implementation target deliberately small: stable Building identity, a formal 2D footprint, explicit local coordinate context for future interior plans, creator-friendly exterior/interior navigation, and correct authority routing.

Do not build a separate CAD/BIM subsystem, promote every visible architectural element to an Object, or treat a 3D renderer representation as authoritative world state.
