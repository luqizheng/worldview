# World View — ME-04-C Floors, Levels & Vertical Organization

- Version: 0.1
- Date: 2026-11-21
- Status: Vertical organization and Building-local frame working baseline
- Depends on: ME-04-00/A/B, ME-05-04 Coordinate Reference / Precision / Measurement, P2-09-30 Field Descriptor Registry and Core Domain Object/Geometry/Command contracts
- Scope: Product/domain semantics and typed service requirements. This document does not authorize registry, schema, command, API or 3D implementation.

## 1. Executive decision

Adopt a **Building-local Vertical Frame (BLVF)** as the canonical reference for organizing floors and interior levels.

Keep three concepts separate:

1. **Level identity and order** — which logical level the creator means (Basement 2, Ground Floor, Mezzanine, Level 2, Roof Level).
2. **Building-local vertical position** — the level's elevation relative to the Building's own vertical origin.
3. **Map/world vertical position** — the Building's vertical origin expressed in a Map's explicitly declared vertical reference, when one exists.

A floor label or ordinal is not an elevation. A Building-local elevation is not a world altitude. A renderer's Z coordinate is neither.

**V1 modeling recommendation:** Floors/Levels are Building-owned typed substructures with stable Building-scoped Level IDs, not independent top-level Map Objects. They may be referenced by interior plans and vertical connectors. This is a proposed Core Domain owner-payload extension, not a currently supported registry feature. Do not create fake Object IDs or store these records as opaque JSON blobs.

A formal Building Level catalog must not be enabled until Core Domain adopts typed level records, identity, validation, revision and history semantics.

## 2. Why levels are not independent Map Objects in V1

A Level is meaningful within exactly one Building. It does not normally have an independent map identity, location, ownership scope or lifecycle separate from that Building.

Making every floor a top-level Object would introduce a second identity/ownership graph, generic Building→Level relationships absent from the current registry, object-tree and lifecycle complexity for floors and mezzanines, and pressure to make every room, wall and stair a separate Object.

Instead:
- Building retains one stable Core Domain Object identity.
- Each Level receives a stable level_id unique within that Building.
- Level references are typed and scoped as (building_object_id, level_id).
- The level catalog is owned by the Building's typed domain payload.
- A Level is not independently shared between Buildings or Maps.
- Copying a Building creates a new Building identity and new level IDs.
- Geometry Asset deduplication does not merge Level identities.

This keeps the model compact while allowing plans and connectors to refer to a precise level.

### 2.1 Why a Level still needs identity

A display label is mutable and can be duplicated. An ordinal can change when a mezzanine is added or levels are renumbered. Neither can safely anchor geometry or connector references.

level_id remains stable when the label changes, the floor is renumbered, its elevation is adjusted, its plan geometry is refined or the active view changes. Retiring a level and creating a different level must not silently reuse the old ID.

## 3. Level vocabulary

A Level is a logical vertical stratum or usable floor-like space within a Building, not necessarily a complete physical storey.

Recommended typed fields:

| Field | Meaning | Rule |
|---|---|---|
| level_id | Stable ID scoped to the owning Building | Required, immutable |
| level_label | Creator-facing label, e.g. Ground Floor, Basement 1, Mezzanine | Required, mutable |
| level_kind | basement, ground, storey, mezzanine, attic, roof, platform, other | Required, bounded enum |
| display_order | UI ordering hint | Not a physical height |
| elevation_local | Reference-plane height in BLVF units | Required when spatially positioned |
| vertical_extent_local | Optional usable vertical interval/clearance | Optional; not a solid model |
| is_primary_access_level | Creator-designated main access level | Optional hint; not inferred from lowest elevation |
| plan_refs | Typed references to plan/geometry records defined by ME-04-D | Empty until ME-04-D is adopted |

Exact field names and serialization must be reconciled by Core Domain. Do not put untyped arbitrary dictionaries into a definition JSON field.

### 3.1 Floor labels are independent of elevation

A mezzanine may sit between numbered storeys. A basement's negative local elevation does not determine its label. A roof terrace can be a Level if it represents an authored usable/accessed stratum; a decorative roof surface is not automatically a Level. A split-level floor may need multiple explicitly named Levels until ME-04-D adopts bounded local sub-plane support.

Do not infer level names from elevation signs, nor elevation from floor numbering. A sloped site may have multiple entrances at different world heights without automatically requiring multiple Ground Floor levels; entrance semantics are handled in ME-04-E.

## 4. Building-local Vertical Frame (BLVF)

### 4.1 Frame definition

Every Building with vertically positioned Levels has one explicit BLVF identity.

The BLVF defines:
- stable vertical_frame_id;
- owning Building Object ID;
- vertical unit (canonical calculations in metres; creator display may use metres or feet);
- local vertical origin and positive axis convention;
- local vertical precision profile;
- frame definition version;
- optional binding to a Map vertical reference;
- transform and provenance when a world binding exists.

Recommended convention: local z = 0 is the Building's creator-designated vertical origin plane. Positive local Z points upward. This is not an assumption that the origin is at ground level, sea level or the lowest floor.

The frame is stable across viewport/camera changes and is not derived from renderer transform nodes.

### 4.2 Local and world vertical relationship

When a Map declares a compatible vertical reference and the Building has an explicit binding:

z_world = z_origin_world + z_local

Here z_local is the Level/feature coordinate in the Building-local frame; z_origin_world is the Building frame origin expressed in the Map's declared vertical reference; and z_world is a coordinate in that same reference.

This is an explicit transform with units, reference identity, version and error/quality metadata. It is not raw addition across unrelated datums.

If no compatible Map vertical reference or reliable origin binding exists:
- BLVF remains usable for internal organization;
- interior plans can be authored in local coordinates;
- world altitude is Unknown/Unsupported, not zero;
- no sea-level or terrain elevation is silently assumed.

### 4.3 Relation to terrain elevation Field

The current elevation Field Descriptor requires a stable vertical_reference_id and a unit. It is a Map-owned source Field Dataset whose revisions form a Map-wide source baseline.

A sampled terrain elevation near the Building is contextual evidence, not automatically the Building's vertical origin:
- the footprint may cover sloped ground;
- the Building may be elevated on stilts or a platform;
- the building datum may be set at a finished floor, foundation or another creator-chosen reference;
- field coverage may be partial or no-data;
- terrain may change through a Map Source Field Baseline revision.

Therefore:
- show terrain elevation samples and source/vertical-reference provenance as context;
- allow an explicit, reviewed binding from Building-local origin to Map vertical reference;
- do not silently set z_origin_world from the footprint centroid or one raster cell;
- do not let a Field Revision silently rewrite Building-local floor elevations;
- when the elevation Field Revision changes, mark dependent terrain-to-building comparison evidence stale, but keep Building levels and BLVF values unchanged.

This respects ME-05 Map-wide source-field semantics and avoids confusing physical source data with authored building structure.

## 5. Coordinate dimensions and 2D/3D boundary

ME-05-04 currently defines V1 authoring as a 2D working plane. It explicitly says vertical reference, 3D precision and solid geometry require a separate extension.

ME-04-C therefore does not claim full 3D coordinate support.

V1 interior authoring target:
- each Level has a 2D plan in a Building-local horizontal frame;
- each Level has a scalar elevation_local locating its reference plane in the BLVF;
- vertical separation between Levels is derived from declared reference-plane elevations;
- optional clearance/vertical extent is scalar metadata, not a closed 3D volume;
- 2D plan geometry remains 2D and is not encoded as fake XYZ geometry.

Future 3D geometry requires an explicit typed 3D spatial-reference and geometry contract, dimensional precision, transform, measurement, topology and Asset encoding. It cannot be smuggled into the existing 2D envelope by adding arbitrary Z values.

### 5.1 Horizontal frame remains distinct

Building-local horizontal plan coordinates and BLVF vertical coordinates form a coordinated local building frame, but have separate responsibilities:
- horizontal local frame: origin/orientation/units/precision and transform to Map 2D coordinates;
- vertical frame: origin/up direction/units/precision and optional transform to Map vertical reference.

A complete 3D transform may combine these later, but the V1 2D plan transform must not imply a 3D solid transform.

## 6. Vertical layout patterns

| Pattern | Representation |
|---|---|
| Standard floors | One Level per authored storey; ordered labels plus explicit local elevations |
| Basements | Levels with negative local elevations where appropriate; explicit labels/kinds |
| Mezzanine | Level with elevation between adjacent storey reference planes |
| Attic / roof level | Level only when it represents an authored usable/accessed stratum |
| Roof surface | Presentation or ME-04-D geometry, not automatically a Level |
| Split-level | Multiple Levels where each needs independent plan/connection semantics; otherwise defer bounded sub-plane support |
| Double-height room | One Room/space may span vertical clearance; ME-04-D must define cross-level room identity |
| Multi-wing building | Levels belong to one Building; wing partitioning remains plan organization until justified |
| Sloped terrain / stepped foundation | Building-local level elevations remain independent; terrain relation is explicit evidence |
| Elevated building | Building origin can be bound above terrain in the Map vertical reference |
| Underground facility | Negative local elevations and explicit world binding where known; underground topology handled in ME-04-F |

These patterns define intended coverage, not a claim that all are executable in V1.

## 7. Elevation, spacing and precision

- Store numeric elevations in a canonical unit (recommended metres); convert display units deterministically.
- Do not store floor height as an ordinal multiplier or derive it from number of floors.
- Do not round local elevations to whole metres or feet.
- Use a versioned Building-local vertical precision profile selected from supported smallest meaningful vertical detail and expected Building extent.
- Keep storage quantization precision, geometric validation tolerance, numerical epsilon, display rounding and measurement error separate, following ME-05-04.
- Validate finite values, unit, frame identity, valid range and monotonic/overlap rules explicitly.
- Adjacent floor elevations need not be equally spaced.
- Unusually close/overlapping level reference planes may produce warnings, but no universal storey height is imposed.
- vertical_extent_local is not inferred from the next floor elevation. Slabs, voids, mezzanines and double-height spaces make that unsafe.

No exact decimal scale or numeric tolerance is frozen here; these require representative fixtures and cross-platform tests.

## 8. Level ordering and overlap semantics

display_order is a UI organization hint and must not be used for physical calculations.

Physical ordering is by elevation_local within the same BLVF. Levels may share an elevation (e.g. split wings/platforms) only if ME-04-D defines their plan relationship and non-ambiguity. Their vertical extents may overlap (e.g. mezzanine, void or partial deck) when geometry/coverage explains it. They may be separated by large voids or have no defined vertical extent where only the reference plane is known.

V1 must not enforce exactly one Level per height or a global no-overlap rule. It should validate duplicate IDs and ambiguous plan ownership, and warn where a connector cannot resolve a unique target.

## 9. History and revision semantics

Separate:
- Building identity and BLVF identity;
- current Building Level catalog and its definition revision;
- Level geometry/plan revisions;
- Map Initial State;
- selected Map/Branch History changes;
- Map-wide elevation Field Revision.

Recommended authority routing:
- correcting a Project-shared Building's current Level definition: shared definition service, retroactive for all Maps using that shared Building;
- correcting a Map-local Building's current Level definition: Map-local definition service;
- creating the Building's initial Level catalog at time zero: Building definition/initial setup path coordinated with Map Initial State;
- a later construction/removal/reconfiguration event: selected History only, but only after Core Domain provides a typed Level-history operation;
- a Map elevation Field Revision: Map Source Field Baseline only, retroactive to every Branch and time of that Map, never a Building Level command.

**Important V1 limitation:** current V1 command registry has no Building/Level descriptor or Level-specific temporal commands. Until adopted, the editor must not claim that a floor can be historically created, demolished or moved independently. Such edits remain unavailable or are modeled only as a supported whole-Building historical change if Core Domain explicitly permits it.

A definition correction must not simulate a historical floor addition. A source Field Revision must not simulate construction.

## 10. Vertical connections and access

ME-04-C defines Level references that ME-04-E will consume; it does not define stairs, elevators, ladders, ramps or shafts.

Every future vertical connector must reference:
- owning Building ID;
- source level_id;
- target level_id;
- a typed connector identity/geometry or component reference;
- connector direction/capability only when supported by its descriptor.

A connector is valid only if both Level references resolve within the same Building or an explicitly supported cross-Building facility contract. A stair symbol drawn near two floors does not create a formal connection.

ME-04-E must decide connector identity, geometry, endpoint semantics, direction, multi-storey continuity, landings, accessibility and history behavior. No relationship is enabled here.

## 11. UX and creator language

The creator should see a familiar Floors panel, not coordinate-system terminology.

Recommended presentation:
- floor list with editable labels and type icons;
- visual vertical stack ordered by local elevation, with labels and relative heights;
- add-level actions such as Basement, Ground Floor, Upper Floor, Mezzanine, Attic/Roof Level, Custom Level;
- optional “Set building height reference” action with plain-language explanation;
- “World elevation not linked” state when no vertical datum is available;
- terrain elevation shown as sourced context, not silently copied into the Building;
- warnings for duplicate labels, ambiguous order, invalid elevation or stale world binding.

The UI must distinguish “move this floor within the Building” from “move the whole Building vertically in the world”. These are separate operations with separate previews and confirmation where appropriate.

Do not expose raw datum IDs, CRS parameters or numeric precision profiles in the default workflow. Put provenance and technical detail in expandable diagnostics.

## 12. AI assistance and validation

AI may propose a Level list from a natural-language Building description; suggest labels and relative floor arrangement; identify inconsistent local elevations, duplicate IDs or missing references; explain local floor height versus world altitude; and suggest candidate connector endpoints for review.

AI must not invent absolute world elevation when no vertical reference is bound, infer floor heights from a generic Building category as formal fact, silently bind to terrain samples, infer structural safety/load capacity/code compliance/accessibility, or mutate formal Level records without Core Domain typed services and authorization.

Validation outcomes: Pass, Warning, Blocked, Unknown, Unsupported and AcknowledgedException. Evidence includes source revision, BLVF identity/version, Map/Branch/Time context, algorithm version and freshness. Missing vertical datum is never zero; unsupported 3D validation never passes.

## 13. V1 scope and deferrals

| Capability | V1 recommendation | Status |
|---|---|---|
| Building-local vertical frame | Required for Buildings with Levels | Requires Core Domain typed contract |
| Stable Building-scoped Level ID | Required | Requires typed owner-payload extension |
| Level labels/kinds/order | Required | Proposed bounded fields |
| Level reference-plane elevation | Required when positioned | Local vertical scalar; no 3D solid implication |
| Building-local 2D floor plans | Consumed by ME-04-D | Requires explicit horizontal/vertical binding |
| Map vertical reference binding | Optional, explicit | Depends on Map vertical-reference service |
| Terrain elevation comparison | Context/evidence only | Uses Field Dataset and its vertical_reference_id |
| Full 3D solids and volumetric topology | Deferred | Separate ME-05/Map Engine contract and technical proof |
| Structural/BIM attributes | Out of scope | No construction engineering model |
| Independent Floor Map Objects | Not recommended for V1 | Avoid object/relationship explosion |
| Independent historical Level lifecycle | Deferred pending typed commands | No fake History support |

## 14. Core Domain / Map Engine adoption gates

Before enabling formal Levels:
1. Define typed Building-owned Level record contract; no opaque unvalidated JSON.
2. Define level_id allocation, uniqueness within Building, immutable identity, retirement and reference integrity.
3. Define BLVF identity, units, up-axis, precision profile and versioning.
4. Define optional binding to a stable Map vertical reference and explicit transform/error contract.
5. Define relation between Building-local horizontal frame and BLVF without prematurely claiming 3D support.
6. Define typed commands for initial Level catalog creation and supported subsequent edits; declare shared definition versus History-local effects.
7. Define how Level references are pinned by ME-04-D plans and ME-04-E connectors.
8. Extend spatial-reference/geometry contracts before any authoritative 3D geometry is stored.
9. Add positive/negative validation fixtures: no datum, mixed units, duplicate IDs, stale transform, invalid elevation and partial Level overlaps.
10. Prove deterministic serialization and calculations on Windows/macOS.

No ME-04 document alone authorizes Core Domain registry, database schema, command catalog or 3D geometry changes.

## 15. Acceptance scenarios

| ID | Scenario | Expected result |
|---|---|---|
| C-01 | Create Ground Floor and two upper Levels | Stable Building-scoped IDs; labels/order do not encode elevation |
| C-02 | Rename Level 2 to Great Hall Level | Same level_id; no geometry identity loss |
| C-03 | Insert a mezzanine | Existing Level IDs remain stable; display order can change |
| C-04 | Add Basement 2 below Basement 1 | Explicit local elevation; no sea-level assumption |
| C-05 | Building has no world vertical binding | Interior Levels work locally; world altitude Unknown/Unsupported |
| C-06 | Bind Building origin to Map vertical reference | Explicit transform/provenance; local Level elevations unchanged |
| C-07 | Terrain Field Revision changes | Terrain comparison evidence becomes stale; BLVF and Level geometry unchanged |
| C-08 | Building stands on stilts above terrain | Explicit world origin offset; terrain sample is not silently used as floor zero |
| C-09 | Adjacent Levels have unequal heights | Valid; no uniform floor-height assumption |
| C-10 | Two Levels share a height | Requires explicit plan semantics; no automatic merge |
| C-11 | Split-level floor | Explicit Levels or later bounded sub-plane contract; no fake 3D geometry |
| C-12 | Floor plan references missing level_id | Reject/diagnose dangling reference |
| C-13 | Stair connector references another Building's Level | Reject unless cross-Building connector contract is adopted |
| C-14 | Renderer camera Z changes | No formal BLVF or Level value changes |
| C-15 | User moves a floor vertically inside Building | Only local Level elevation changes through typed operation |
| C-16 | User moves the whole Building vertically | Building-to-Map vertical binding changes; local Level elevations unchanged |
| C-17 | Historical floor addition requested before Level-history commands exist | Explain Unsupported; do not write shared definition as substitute |
| C-18 | Elevation sample is No-data/outside coverage | No altitude inferred; diagnostic identifies source coverage state |
| C-19 | Feet display selected | Deterministic conversion from canonical unit; stored value/reference unchanged |
| C-20 | Two Buildings have same local Level IDs | Valid because IDs are scoped by distinct Building identities |

## 16. Final recommendation

Adopt the Building-local Vertical Frame and Building-scoped Level substructure as the ME-04-C working baseline.

This provides enough semantic structure for basements, mezzanines, roof levels, uneven storeys and interior plans without prematurely introducing full 3D solids or a separate Floor Object graph.

Keep local floor organization usable even when world vertical reference is unknown. Bind to world elevation only through an explicit, typed, provenance-bearing transform. Treat terrain elevation as source evidence, not as an implicit Building datum.

The resulting model is intentionally 2D in plan plus explicit scalar vertical organization. Full 3D geometry, volumetric topology and engineering/BIM behavior remain separate future contracts.
