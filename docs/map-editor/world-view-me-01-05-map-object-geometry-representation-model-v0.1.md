# World View — ME-01-05 Map Object, Geometry & Representation Model

- Version: 0.1
- Date: 2026-11-21
- Status: Architecture Working Baseline — recommended conceptual model; exact Core Domain schema, geometry payload format, renderer scene contract and asset serialization remain open
- Scope: Semantic map objects, stable identity, ObjectState, geometry ownership/components, spatial relationships, presentation representations, layers, symbols, labels and derived render data
- Depends on: ME-01-01 Editor Overall Architecture; ME-01-02 World Knowledge Model & Story-to-Spatial Bridge; ME-01-03 Guided Initialization; ME-01-04 Multi-scale Spatial Model & Global Consistency; Core Domain P2-09 logical persistence model and Command/Event/State Change contract; Map Engine spatial presentation/editing interaction contract

## 1. Executive recommendation

Adopt a strict three-part model:

1. **Semantic Object** — what exists in the modeled world, with a stable Core Domain identity and Object Type.
2. **Geometry and spatial state** — where and in what spatial form the Object exists at the selected Map/Branch/Historical Time.
3. **Presentation** — how the Object is drawn, labelled, symbolized, grouped and filtered in a particular view, scale and style.

These parts are related by explicit references, but none is a substitute for another.

A mountain range is one semantic Object; its geometry may contain multiple ridge lines, area outlines and elevation references; a natural-geography view may show relief and elevation, while a fantasy-cartography view shows mountain symbols. These are coordinated views of one modeled feature, not competing map facts.

V1 should not introduce a second object registry in the editor, a general-purpose CAD/BIM object graph, a renderer-owned semantic scene, or a presentation-specific copy of formal geometry. Core Domain remains the only authority for formal Object identity, ObjectState, formal geometry references, relationships and history. Map Engine and Editor derive their working representations from that authority.

## 2. Goals and non-goals

### 2.1 Goals

- Preserve stable semantic identity while geometry and visual style evolve.
- Represent objects whose geometry is absent, approximate, multipart, nested, networked or progressively refined.
- Support multiple geometry components for one semantic Object without confusing components with separate Objects.
- Support multiple coordinated presentations of the same formal spatial facts.
- Allow a symbol, label, texture or render primitive to refer back to its source Object and geometry.
- Keep display LOD, projected geometry, hit-test indexes and render meshes derived and rebuildable.
- Preserve Map/Branch/Historical Time reconstruction and Core Domain Command semantics.
- Support vector and raster content without treating every raster cell or display primitive as a semantic Object.
- Keep V1 understandable and implementable by a small team.

### 2.2 Non-goals for V1

- A universal ontology or arbitrary user-defined type system.
- A full CAD/BIM parametric constraint model.
- A general-purpose scene graph as the formal domain model.
- Arbitrary many-to-many mesh ownership or shared mutable geometry editing.
- A complete cartographic symbol authoring suite.
- A universal topology fabric connecting every geometry automatically.
- Automatic semantic Object creation from every geometry component, story mention, symbol or raster feature.
- A separate Object database for each editor mode or presentation profile.
- Treating renderer nodes, GPU buffers, tiles, labels or hit-test entries as formal world entities.

## 3. The three-part model

### 3.1 Semantic Object

A Semantic Object represents an entity in the modeled world: mountain range, river, lake, region, settlement, road, building, floor, room, cave, political territory, point of interest or another supported domain concept.

The Object has:
- a stable identity allocated and registered by Core Domain;
- an Object Type defining its semantic category and permitted domain behavior;
- lifecycle/availability semantics governed by Core Domain;
- relationships to other formal Objects where the domain requires them.

Object identity does not contain mutable current geometry, current attributes, current location or presentation settings. Those belong to the applicable state and related records/assets.

### 3.2 Geometry and spatial state

Geometry describes spatial form. It may be:
- absent because the Object is not spatially located or has not yet been spatialized;
- approximate or provisional in an explicitly staged Candidate;
- formal and authoritative after Core Domain application;
- composed of one or more components;
- represented by vector geometry, raster references, or a combination where the Object Type supports it.

Geometry does not itself determine semantic identity. A polygon drawn on the canvas is not automatically a city, region or lake. The domain operation that creates or associates formal geometry must specify the target Object and validate its type-specific constraints.

### 3.3 Presentation

Presentation describes how an Object is perceived in a view. It may include:
- symbol and icon;
- stroke, fill, texture and pattern;
- label content and placement;
- z-order and visual grouping;
- scale-dependent visibility and simplification;
- style profile and mode-specific emphasis.

Presentation can vary without changing the formal Object or its geometry. Hiding a symbol, changing its color, changing a label font or switching from natural geography to fantasy cartography must not create a formal historical State Change unless the user explicitly edits a domain-owned attribute.

## 4. Object identity, Object Type and ObjectState

### 4.1 Identity authority

Core Domain owns:
- Object Type identity and definition authority;
- stable Object identity;
- Object ID reservation and allocation;
- Object lifecycle and deletion/availability semantics;
- ObjectState reconstruction;
- formal geometry and relationship references;
- Command, Event, State Change and historical persistence.

The Editor must never allocate, recycle or infer a formal Object ID. AI Assistant must not keep an independent registry of formal map Objects.

### 4.2 Object Type

Object Type defines the semantic kind and the domain contract applicable to an Object.

Examples:
- MountainRange
- River
- Lake
- TerrainRegion
- Settlement
- Road
- Building
- Floor
- Room
- CaveSystem
- PoliticalRegion
- PointOfInterest

The examples are illustrative, not a frozen taxonomy. ME-02, ME-03 and ME-04 may refine specialist types under Core Domain governance.

An Object Type should define or reference:
- required/optional semantic attributes;
- allowed geometry families and dimensionality;
- expected spatial relationship types;
- validation rules and invariants;
- whether geometry is optional, single-component or multipart;
- whether it can contain or be part of other Objects;
- default editing mode and inspector contribution where appropriate.

Do not encode all behavior in a UI-only type registry. Editor metadata may improve usability, but formal invariants belong to Core Domain/domain services.

### 4.3 ObjectState

ObjectState is the reconstructed state of an Object in the selected Map/Branch and Historical Time. It is not a second identity and must not be stored as a mutable current-state column on the identity record.

A reconstructed state may include:
- semantic attributes;
- formal geometry references/components;
- formal spatial/domain relationships;
- status and availability at that time;
- references to immutable assets required to reconstruct the state.

The exact initial-state and historical State Change representation remains governed by Core Domain P2-09. This document does not introduce Object Version or a parallel editor revision chain.

### 4.4 Initial state and later changes

The initial formal definition and later historical changes are distinct:
- initial authoring establishes the formal starting state through the applicable Core Domain creation/baseline path;
- later in-world changes are expressed through the existing Command/State Change/History model;
- editor-local undo reverts an uncommitted interaction operation only;
- undoing a committed world change requires a valid Core Domain history operation, not deleting an editor buffer.

Changing the visual style or presentation profile is not a world-history change by default.

## 5. Geometry ownership and association

### 5.1 Ownership rule

A formal geometry value/component is associated with an Object through an explicit Core Domain relationship. Core Domain owns the authoritative semantic reference and its historical applicability. Large immutable geometry payload bytes may live in the project-local Asset Store under the existing P2-09 asset contract.

The Map Engine may build derived geometry for rendering, indexing and analysis, but it does not own the formal geometry.

### 5.2 One Object may have multiple geometry components

A semantic Object can have multiple geometry components where that matches its domain meaning.

Examples:
- a mountain range may contain multiple ridge lines, peaks and a generalized extent;
- an island group may have multiple polygon components while remaining one semantic Object if the domain says it is one group;
- a river may have multiple connected reaches, tributary Objects, or a multipart geometry representation depending on semantic modeling;
- a city may have a primary extent plus separately modeled districts and building Objects;
- a building may have a footprint, floor outlines and vertical/interior structures.

The geometry model must preserve component identity and role where edits need to target a particular component. Component identity is not automatically a semantic Object identity.

### 5.3 Geometry role and dimension

A geometry association should be interpretable by:
- geometry family (point, line, curve/path, area, surface, volume-like or structured interior geometry as supported);
- spatial dimension / coordinate context;
- semantic role (for example footprint, centerline, boundary, ridge, entrance, floor plan, elevation surface);
- component identity and ordering where meaningful;
- validity and precision metadata;
- provenance and source reference.

Do not force every object into one generic polygon field. Equally, do not create a separate bespoke geometry engine for each Object Type. Use a shared geometry foundation plus type-specific validation.

### 5.4 Geometry can be absent or uncertain

An Object may be known in World Knowledge before its precise location or geometry is established. It may have:
- no spatial association yet;
- a named or approximate location;
- a broad region association;
- a provisional candidate geometry;
- a confirmed formal geometry.

These states must be explicit. Do not invent precise coordinates to fill a required field. A story mention is not an instruction to create a map Object; a knowledge entity can remain non-spatial.

### 5.5 Geometry identity and change

Geometry component identity is useful for stable editing targets and traceability, but it must not be confused with semantic Object identity.

A geometry edit may:
- adjust coordinates/control points;
- add or remove a component;
- reorder components where order is meaningful;
- replace one geometry payload with another;
- change the semantic role of a component through a validated domain operation.

The formal change must retain enough before/after and provenance context under Core Domain history semantics to reconstruct the requested historical state.

## 6. Geometry split, merge and semantic Object operations

This distinction is mandatory.

### 6.1 Geometry operations

Geometry Operations change spatial shape or decomposition:
- split one line/polygon geometry into multiple components;
- merge compatible geometry components;
- simplify or densify geometry;
- adjust vertices or Bézier control points;
- clip, buffer or transform geometry as a validated operation;
- replace an approximate geometry with a refined one.

A geometry split or merge does not automatically create, delete, merge or split semantic Objects.

### 6.2 Object operations

Object Operations change semantic identity or relationships:
- create a new Object with a new Core Domain identity;
- establish a semantic part/whole relationship;
- split a semantic entity into two Objects;
- merge two semantic entities into one where domain semantics permit;
- retire/delete an Object under Core Domain lifecycle rules;
- reassign an Object to a different semantic parent/relationship.

These operations require explicit domain intent and validation. They cannot be inferred merely because a geometry tool produced two shapes or joined two lines.

### 6.3 Examples

- Splitting a river's drawn line to edit a bend does not create two rivers.
- Creating a tributary as a separately named and independently meaningful watercourse is an Object creation plus a flow/network relationship.
- Merging two adjacent polygon rings for display does not merge the two regions' identities.
- Dividing a city outline into districts does not automatically create district Objects unless the user or workflow explicitly establishes those semantic entities.
- Joining two road segments geometrically does not automatically create a road-network connection unless the topology/domain operation establishes it.

The UI must make the operation type clear before application: geometry edit, relationship edit, or semantic Object operation.

## 7. Spatial relationships are not geometry

A relationship expresses domain meaning that may be supported by geometry but cannot be inferred from proximity alone.

Examples:
- contains / is within;
- part of / component of;
- intersects / overlaps;
- adjacent to;
- connects to;
- flows to / drains to;
- belongs to / controlled by (where temporal/domain semantics apply);
- derived from / constrained by.

ME-01-04 defines the cross-scale relationship requirements. Core Domain owns formal relationships and their historical applicability. Map Engine may calculate candidate spatial predicates, but a computed intersection or touching coordinate does not automatically establish a formal semantic relationship.

### 7.1 Topology versus spatial coincidence

Keep separate:
- **geometric coincidence** — coordinates happen to touch or overlap;
- **topological fact** — a formal connectivity/adjacency/shared-boundary relation;
- **semantic relation** — what the objects mean in the world.

Snapping two endpoints to the same coordinate is not sufficient to assert a connected road or river network. A topology-aware tool may propose a formal relationship through a Domain Service.

### 7.2 Relationship time

Some relationships are stable structural facts; others change over historical time.

Examples:
- a room belongs to a floor as a structural relation;
- a city is politically controlled by a kingdom during a historical interval;
- a river flows into a lake under a given terrain/hydrology state;
- a road connects two settlements unless the route is destroyed or rerouted.

The Core Domain contract decides whether each relation is initial-state data, a historical State Change, or a stable/shared definition. The editor must not flatten all relationships into timeless Object metadata.

## 8. Multiple geometry representations for one Object

One Object may have more than one geometry representation for distinct purposes, but the system must identify which one is authoritative and why alternatives exist.

### 8.1 Representation categories

Recommended conceptual categories:

1. **Authoritative geometry** — the formal spatial shape used to reconstruct the ObjectState.
2. **Alternate authoring geometry** — a separately meaningful formal geometry role, such as a building footprint versus an interior floor plan, where the domain model explicitly supports it.
3. **Derived geometry** — generated from authoritative inputs for analysis, simplification, clipping, densification, projection or LOD.
4. **Candidate geometry** — staged AI/user proposal not yet formal.
5. **Reference geometry** — imported or linked evidence used for comparison, not adopted as formal truth.

The representation category must be explicit. A derived LOD must never be mistaken for an alternate authoritative shape. A Candidate must never be loaded as formal state.

### 8.2 Avoid duplicate authoritative geometries

Do not store multiple competing authoritative outlines for the same semantic role without a clear domain reason.

If an Object has multiple views:
- one role may be its authoritative ground footprint;
- another may be its floor-plan geometry;
- a third may be a derived generalized outline.

Those are not three interchangeable versions of the same geometry. They have distinct semantic roles and validity rules.

### 8.3 Source linkage

Every derived or candidate geometry must preserve:
- source Object and component references where applicable;
- source geometry/asset identity or fingerprint;
- generation method and version;
- parameters/tolerance;
- spatial coverage and coordinate reference;
- provenance and freshness/validation state.

This allows derived data to be rebuilt and candidates to be reviewed without becoming a competing authority.

## 9. Raster, vector and structured spatial content

### 9.1 Vector

Vector geometry is the primary representation for semantic Objects because it supports identity, typed attributes, relationships and editing.

V1 shared geometry foundation should support the required point, line/curve and area families, multipart structures and explicit coordinate-reference context. More advanced 3D/solid support is deferred to specialist design and implementation validation.

### 9.2 Raster

Raster is appropriate for continuous or sampled fields and image-like data:
- elevation/height fields;
- derived slope, aspect or climate grids;
- satellite/reference imagery where applicable;
- hand-painted terrain or texture inputs;
- simulation/analysis outputs.

A raster dataset is an asset/dataset with spatial metadata and provenance, not a semantic Object for every cell. It may be referenced by one or more formal domain concepts, but the raster itself does not silently define Object identity.

### 9.3 Vector-raster association

A formal Object may:
- reference a raster as evidence or source;
- use raster-derived values for analysis;
- define vector boundaries around raster patterns;
- be visualized using raster shading plus vector symbols.

The raster's pixels remain data values. If a user or AI extracts a lake boundary or forest region from a raster, the extraction is a Candidate until explicitly adopted as a formal semantic Object/geometry through Core Domain.

### 9.4 Structured interior geometry

Buildings and interiors need more than ordinary 2D map geometry. V1 should model floors, rooms, corridors, entrances, stairs and vertical connections as semantic Objects/relationships with geometry in an explicitly defined local coordinate context.

Do not force interior geometry into the same geographic CRS as the world map. The building/site provides an explicit spatial association or transform between local interior coordinates and the exterior footprint/site. The exact 2D/3D and vertical model belongs to ME-04 / ME-05.

## 10. Presentation model

### 10.1 Presentation is a view, not a world fact

A Presentation maps semantic Objects and spatial datasets into visual output. It may depend on:
- Object Type;
- selected Map/Branch/Historical Time;
- geometry role and current geometry;
- scale/LOD;
- active presentation profile;
- layer visibility/order;
- style and label rules;
- selection/highlight/diagnostic state.

Changing these presentation inputs does not change the formal world state unless the user separately edits a domain-owned property.

### 10.2 Presentation profile

A Presentation Profile is a coherent set of visual rules for a purpose. V1 should support at least:

- **Natural Geography** — relief, elevation, rivers, lakes, coastlines, climate and ecological overlays.
- **Fantasy Cartography** — stylized terrain symbols, forest patterns, rivers, settlements, fortresses, political boundaries and labels.

Later profiles may include political, cultural, historical, ecological, trade/transport or narrative views.

Profiles configure rendering and visibility. They do not own formal geometry or Object semantics.

### 10.3 Style rules

A style rule maps a typed Object or data item to visual properties, potentially conditioned on:
- Object Type or selected attributes;
- scale range / LOD;
- presentation profile;
- selection and diagnostic state;
- layer visibility and order.

A style rule may choose a symbol, stroke, fill, texture, label behavior or visibility. It must not rewrite Object attributes or geometry.

V1 should use a small built-in style vocabulary and data-driven rule definitions. Do not build a fully programmable styling language before the product requires it.

### 10.4 Symbols and textures

Symbols and textures are presentation assets:
- mountain glyphs;
- tree/forest patterns;
- settlement markers;
- fortress/castle icons;
- road/route strokes;
- water fills and line patterns.

A symbol may have a semantic anchor and orientation derived from geometry or attributes, but the symbol shape itself is not the Object geometry. A repeated forest texture does not create thousands of tree Objects.

### 10.5 Labels

Labels are presentation annotations associated with a source Object or dataset:
- text may come from a formal name attribute, localization, or a view-specific override;
- placement may be automatic or manually adjusted;
- collision avoidance and scale visibility are presentation behavior;
- a label's position/rotation is not the Object's authoritative geometry;
- clicking a label should resolve to the source Object or explicit annotation record.

If label placement is purely stylistic, it is view configuration. If the creator wants a persistent map annotation with independent meaning, create a formal annotation Object through the appropriate domain operation.

### 10.6 Visual grouping versus semantic containment

Layer groups, draw order, symbol clusters and visual proximity are presentation organization. They must not create semantic parent/child relationships.

Conversely, hiding or moving an Object to another layer does not change its semantic containment or historical existence.

## 11. Layer model and content references

### 11.1 Layer responsibility

The shared layer system organizes what is visible and editable in the current presentation. It is not the authority for formal Objects.

V1 layer kinds:
- **Vector Layer** — references formal Objects/geometry and vector annotations.
- **Raster Layer** — references spatial raster datasets/assets.
- **Group Layer** — organizes child layers and their visibility/order.

Layers may carry presentation defaults and editor organization metadata. Formal semantic relationships remain in Core Domain.

### 11.2 Layer membership

An Object may be displayed in different presentation contexts without duplicating the Object. Layer membership should be treated as a presentation association unless a specific domain contract explicitly gives it formal meaning.

Avoid a rule that every Object must belong to exactly one layer. A city may be visible in settlement, political and historical presentations. The same Object reference may appear through multiple view definitions.

### 11.3 Visibility, lock and editability

- Hidden: not normally rendered or hit-tested.
- Visible: rendered and eligible for normal hit-testing subject to tool policy.
- Locked: visible but not directly editable through ordinary geometry tools.
- Read-only/reference: visible for context but cannot be directly modified.
- Derived: displayable but not directly edited as formal geometry.
- Candidate: displayed in a review overlay and editable only within its staging workflow.

These are interaction/presentation states, not Object lifecycle states.

## 12. Derived render and interaction representations

The renderer and spatial interaction layer need efficient structures that are not formal world data.

Examples:
- Render Item / scene node;
- projected geometry;
- tessellated curve;
- mesh or GPU buffer;
- raster tile;
- LOD geometry;
- label layout;
- symbol instance;
- hit-test index entry;
- snap candidate;
- selection/highlight overlay.

Every such representation must retain a source reference sufficient to identify its originating Object, geometry component, raster dataset or annotation where applicable.

Derived structures are:
- rebuildable from formal state and presentation configuration;
- versioned or fingerprinted against their source inputs;
- disposable caches;
- non-authoritative;
- prohibited from allocating semantic Object IDs.

The Map Engine may optimize the render scene and spatial indexes, but Core Domain remains the only authority for formal identity and geometry.

## 13. Editing and change flow

### 13.1 Editing an Object

A normal edit follows this boundary:

1. Editor reads the selected Map/Branch/Historical Time projection from Core Domain.
2. User selects an Object or geometry component.
3. Interaction Controller creates a transient edit session and preview.
4. Application layer constructs a typed edit intent.
5. Core Domain Domain Services validate identity, geometry, Object Type invariants, relationships and historical context.
6. If valid and authorized, the formal operation becomes a Core Domain Command/State Change under the existing contract.
7. New projections and dependent presentation/analysis caches are invalidated or refreshed.
8. The Editor displays the updated authoritative state.

Pointer movement and preview changes do not write formal geometry or Commands.

### 13.2 AI-assisted editing

AI Assistant may interpret story/creator intent and produce a Candidate with:
- target Object references or proposed new Object creation;
- geometry/attribute/relationship changes;
- provenance and source knowledge assertions;
- uncertainty and unresolved decisions;
- validation evidence and impact summary.

The Candidate remains AI-owned staging data until the existing review, authorization and submission flow completes. AI must not directly mutate Core Domain tables or assign final Object IDs. ID reservation/creation is delegated to Core Domain services according to the current identity contract.

### 13.3 Create new Object versus add geometry

The UI and operation contracts must distinguish:
- create a semantic Object;
- add or refine a geometry component on an existing Object;
- create a relationship between existing Objects;
- create a derived visual/analysis representation;
- create a non-formal Candidate.

Do not use one ambiguous "draw feature" operation for all five.

## 14. History, Branch and time semantics

- Formal geometry and semantic relationship changes follow the applicable Core Domain initial-state / Command / State Change contract.
- Object identity is stable across historical states; whether an Object exists/is available at a selected time is determined by Core Domain lifecycle and reconstruction.
- Geometry may change over Historical Time without changing Object identity.
- A new Object created in a historical period receives a new stable identity; its effective existence is determined by the formal history contract.
- A change to shared/base Object definition must follow the Core Domain shared-object authority rules; it must not be used to encode an in-world historical change.
- Branches and historical time are resolved only by Core Domain. Editor, Map Engine and AI Assistant must not implement independent history overlays.
- Presentation profile, layer visibility, selection, viewport and render cache changes are not historical world changes by default.
- A historical map presentation must render the ObjectState and geometry reconstructed for the selected Map/Branch/Historical Time, not the latest geometry cache without context.

## 15. V1 conceptual contracts

The following shapes communicate boundaries, not frozen C# types or database tables.

```text
SemanticObjectRef
  - ObjectId
  - ObjectTypeId

ReconstructedObjectState
  - SemanticObjectRef
  - selected Map / Branch / Historical Time context
  - semantic attributes
  - formal geometry associations
  - formal relationships
  - availability/lifecycle state

GeometryAssociation
  - GeometryComponentId
  - semantic role
  - geometry family / dimensionality
  - authoritative immutable payload reference or inline value
  - coordinate reference context
  - precision / validity metadata
  - provenance

PresentationBinding
  - source Object / geometry / raster reference
  - presentation profile
  - style rule
  - scale / LOD applicability
  - label / symbol behavior

DerivedRenderItem
  - source reference
  - derived geometry / symbol / raster tile / label layout
  - source fingerprints and presentation generation
  - cache validity metadata
```

The exact division between SQLite metadata and immutable Asset Store payloads follows Core Domain P2-09. Do not persist renderer-specific classes as domain records.

## 16. Recommended V1 implementation scope

Implement in this order:

1. **Identity-safe object projection:** consume Core Domain Object identity, Object Type and reconstructed ObjectState without creating editor-side identities.
2. **Shared geometry association:** support formal geometry references/components, roles, coordinate context and provenance.
3. **Geometry/Object operation split:** ensure geometry split/merge never silently changes semantic identity.
4. **Minimal spatial relation support:** use Core Domain relation types and deterministic geometry predicates without conflating them.
5. **Two presentation profiles:** natural geography and fantasy cartography using the same formal Objects and geometry.
6. **Data-driven symbol/style/label rules:** small built-in rule set with scale applicability.
7. **Layer references:** vector/raster/group organization, visibility, lock and reference behavior.
8. **Derived scene contract:** Render Items, LOD, tiles, labels and hit-test entries retain source references and are rebuildable.
9. **Historical rendering tests:** ensure selected Branch/Time reconstructs the correct geometry and attributes.
10. **Candidate review integration:** distinguish proposed Object creation, geometry edits, relationship edits and presentation-only changes.

Do not build a generic visual programming style engine, universal CAD constraint solver, or full 3D building geometry platform as part of this V1 slice.

## 17. Acceptance criteria

1. Every formal map Object has a stable identity allocated by Core Domain.
2. Object identity is distinct from ObjectState, geometry, presentation, layer membership and selection.
3. One Object can have multiple geometry components with stable component references where editing requires them.
4. Geometry may be absent, approximate, provisional or formal without inventing coordinates or semantic Objects.
5. Geometry split/merge does not silently create, delete, split or merge semantic Objects.
6. A formal Object's geometry and relationships reconstruct correctly for the selected Map/Branch/Historical Time.
7. Natural Geography and Fantasy Cartography presentations can display the same formal Object differently without duplicating its identity or authoritative geometry.
8. Symbols, labels, textures, render nodes and hit-test entries resolve back to their source Objects/data.
9. Raster datasets remain spatial assets/data, not one semantic Object per cell.
10. Layer grouping, visibility and draw order do not create or change semantic relationships.
11. Derived geometry/render representations record source context and can be invalidated/rebuilt.
12. Snapping and geometric coincidence do not silently create formal topology or semantic relationships.
13. Editor pointer movement and previews do not persist formal geometry.
14. AI Candidates remain staged until the existing authorization and Core Domain application flow completes.
15. Presentation-only changes do not create formal historical world changes by default.
16. A historical view renders the reconstructed state for the requested Branch and Historical Time, not an unqualified latest cache.

## 18. Open decisions for later design

Not frozen by this working baseline:

- exact Object Type registry and extensibility policy;
- exact Core Domain geometry association and component persistence schema;
- supported geometry families and validity constraints for V1;
- whether geometry component identity is a Core Domain first-class identity or a stable key within an immutable payload;
- geometry serialization and asset format;
- vector/raster dataset metadata contract and asset lifecycle;
- formal layer persistence ownership and whether any layer organization is project-wide or Map-specific;
- style-rule schema, label override persistence and profile configuration;
- renderer-neutral Presentation Scene / Render Item contract;
- 3D, volume, terrain mesh and building vertical-coordinate semantics;
- localization and historical naming/label rules;
- exact Object Type validation and specialist-mode extension boundaries.

These decisions are assigned to ME-05, ME-06, ME-07, ME-02/03/04 and the relevant Core Domain / Map Engine contracts. This document constrains those designs but does not freeze their physical schema.

## 19. Relationship to the ME-01 package

- ME-01-01 defines the unified editor and subsystem boundaries.
- ME-01-02 defines narrative knowledge and the Story-to-Spatial Bridge.
- ME-01-03 defines Guided Initialization and staged authoring.
- ME-01-04 defines cross-scale spatial scope, context and consistency.
- ME-01-05 defines semantic Object, geometry and presentation responsibilities.
- ME-01-06 will define interaction behavior over these concepts.
- ME-01-07 will verify consistency across the wider architecture and record unresolved contracts.

ME-01-05 is an Architecture Working Baseline, not a frozen Core Domain schema, renderer implementation contract or implementation handoff.
